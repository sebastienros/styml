// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4p candidate g: the rows that put the dictionary-key pool in a given
// state. The pool is reached by reflection (YamlSerializerOptions.CacheContext.DictionaryKeys), so that the same benchmark
// assembly runs over a Styml.dll without it, where every hook does nothing: the base rows read the same documents.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    /// <summary>The pool of an options instance, through reflection; every member is a no-op when Styml has no pool.</summary>
    public sealed class KeyPoolHook
    {
        private readonly object? _pool;
        private readonly Action? _clear;
        private readonly Func<bool>? _isAdding;
        private readonly Func<bool>? _isOff;
        private readonly Func<byte[], int>? _hashOf;

        public KeyPoolHook(System.Text.Yaml.YamlSerializerOptions options)
        {
            options.MakeReadOnly(populateMissingResolver: true);
            object? context = typeof(System.Text.Yaml.YamlSerializerOptions)
                .GetProperty("CacheContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(options);
            _pool = context?.GetType().GetProperty("DictionaryKeys", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(context);
            if (_pool is null)
            {
                return;
            }

            Type type = _pool.GetType();
            _clear = (Action)type.GetMethod("Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate(typeof(Action), _pool);
            _isAdding = (Func<bool>)type.GetProperty("IsAdding", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!.CreateDelegate(typeof(Func<bool>), _pool);
            _isOff = (Func<bool>)type.GetProperty("IsOff", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!.CreateDelegate(typeof(Func<bool>), _pool);
            _hashOf = (Func<byte[], int>)type.GetMethod("HashOf", BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate(typeof(Func<byte[], int>), _pool);
        }

        public bool Present => _pool is not null;

        public void Clear() => _clear?.Invoke();

        public bool IsAdding => _isAdding?.Invoke() ?? true;

        public bool IsOff => _isOff?.Invoke() ?? false;

        public int HashOf(byte[] key) => _hashOf?.Invoke(key) ?? 0;
    }

    /// <summary>
    /// The cold corpus rows: the deserialize rows of the three documents with the most keys, the pool cleared before each
    /// operation, so that every distinct key is added under the lock once per operation.
    /// </summary>
    public class DeserializeColdPool : Scenario
    {
        public static IEnumerable<string> Documents => new[] { "compose-m", "openapi-l", "k8s-l" };

        [ParamsSource(nameof(Documents))]
        public override string Document { get; set; } = null!;

        private KeyPoolHook _hook = null!;
        private KeyPoolHook _generatedHook = null!;

        public override void Setup()
        {
            base.Setup();
            _hook = new KeyPoolHook(Libraries.StymlOptions);
            _generatedHook = new KeyPoolHook(Libraries.StymlGenerated.Options);
        }

        [Benchmark(Baseline = true)]
        public object? Reflection_Yaml()
        {
            _hook.Clear();
            return Libraries.Styml(_doc.Yaml, _doc.Family, Libraries.StymlOptions);
        }

        [Benchmark, Input(Form.Json)]
        public object? Reflection_Json()
        {
            _hook.Clear();
            return Libraries.Styml(_doc.Json, _doc.Family, Libraries.StymlOptions);
        }

        [Benchmark]
        public object? SourceGen_Yaml()
        {
            _generatedHook.Clear();
            return Libraries.StymlGeneratedRead(_doc.Yaml, _doc.Family, Libraries.StymlGenerated);
        }
    }

    /// <summary>
    /// The worst cases: a <c>Dictionary&lt;string, string&gt;</c> of 5,000 keys that never repeat (more than the pool's
    /// 4,096 values), read by reflection from YAML, with the pool in each state the section names.
    /// </summary>
    public class DeserializeKeyPoolWorstCase
    {
        private const int UniqueKeys = 5_000;

        [Params("W-cold", "W-off", "W-frozen", "W-full-fill", "W-collide", "W-nonascii")]
        public string Document { get; set; } = null!; // the case, named Document for gate3-row and the dll swap

        private readonly System.Text.Yaml.YamlSerializerOptions _options = new();
        private KeyPoolHook _hook = null!;
        private byte[] _unique = null!;

        internal KeyPoolHook Hook => _hook;
        private byte[] _vocabulary = null!;

        [GlobalSetup]
        public void Setup()
        {
            WriteLibraries.SetSwitch();
            _hook = new KeyPoolHook(_options);

            // Keys of one width (10 bytes), so that the colliding keys and the base's keys cost the same to read
            _unique = Mapping(Enumerable.Range(0, UniqueKeys).Select(n => "u" + n.ToString("D9")));

            // 4,096 distinct keys, each read twice in a row (one-key mappings in a sequence, since a mapping can't repeat a
            // key), so that half the lookups add and the pool fills without the early switch-off
            var vocabulary = new StringBuilder();
            for (int n = 0; n < 4_096; n++)
            {
                string key = "v" + n.ToString("D9");
                vocabulary.Append("- ").Append(key).Append(": v\n- ").Append(key).Append(": v\n");
            }

            _vocabulary = Encoding.UTF8.GetBytes(vocabulary.ToString());

            switch (Document)
            {
                case "W-off":
                    _hook.Clear();
                    Read(_unique);
                    Check(_hook.IsOff || !_hook.Present, "the pool switched off");
                    break;
                case "W-frozen":
                    _hook.Clear();
                    for (int i = 0; i < 20 && _hook.IsAdding && _hook.Present; i++)
                    {
                        ReadVocabulary();
                    }

                    Check(!_hook.IsAdding || !_hook.Present, "the pool is frozen");
                    Check(!_hook.IsOff, "the pool is on");
                    break;
                case "W-collide":
                    _unique = Mapping(CollidingKeys(UniqueKeys));
                    break;
                case "W-nonascii":
                    // 5,000 keys of 11 bytes that start with a two-byte letter, read once before: A never pools them
                    // and never switches off on them, so each pays the lookup on every read; B pools up to its cap
                    _unique = Mapping(Enumerable.Range(0, UniqueKeys).Select(n => "é" + n.ToString("D9")));
                    _hook.Clear();
                    Read(_unique);
                    break;
            }
        }

        [Benchmark]
        public object? Read()
        {
            switch (Document)
            {
                case "W-cold":
                case "W-collide":
                    _hook.Clear();
                    return Read(_unique);
                case "W-full-fill":
                    _hook.Clear();
                    ReadVocabulary();
                    return Read(_unique);
                default:
                    return Read(_unique);
            }
        }

        private object? Read(byte[] yaml) => YS.Deserialize<Dictionary<string, string>>(yaml, _options);

        private object? ReadVocabulary() => YS.Deserialize<List<Dictionary<string, string>>>(_vocabulary, _options);

        private static byte[] Mapping(IEnumerable<string> keys)
        {
            var text = new StringBuilder();
            foreach (string key in keys)
            {
                text.Append(key).Append(": v\n");
            }

            return Encoding.UTF8.GetBytes(text.ToString());
        }

        // Keys that share one home slot in every table size up to 8,192 slots, for this pool's seed. Without a pool, keys
        // of the same width.
        private IEnumerable<string> CollidingKeys(int count)
        {
            if (!_hook.Present)
            {
                return Enumerable.Range(0, count).Select(n => "c" + n.ToString("D9"));
            }

            const int Mask = (1 << 13) - 1;
            var keys = new List<string>(count);
            int target = -1;
            for (long n = 0; keys.Count < count; n++)
            {
                string key = "c" + n.ToString("D9");
                int home = _hook.HashOf(Encoding.ASCII.GetBytes(key)) & Mask;
                if (target < 0)
                {
                    target = home;
                }

                if (home == target)
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        private static void Check(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Setup: expected " + what);
            }
        }
    }

    /// <summary>'key-pool-check': sets up each worst case, reads once, and prints the pool's state and the bytes one read allocates (no timing).</summary>
    public static class KeyPoolCheck
    {
        // The cases share one pool here (equivalent options share a caching context), unlike the benchmark, which runs
        // each case in a process of its own: every case's setup starts from a cleared pool.
        public static int Run(System.IO.TextWriter output)
        {
            foreach (string c in new[] { "W-cold", "W-off", "W-frozen", "W-full-fill", "W-collide", "W-nonascii" })
            {
                var row = new DeserializeKeyPoolWorstCase { Document = c };
                row.Setup();
                row.Read();
                long before = GC.GetAllocatedBytesForCurrentThread();
                row.Read();
                long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
                output.WriteLine($"{c,-12} pool present {row.Hook.Present}, adding {row.Hook.IsAdding}, off {row.Hook.IsOff}, {alloc} B per read");
            }

            return 0;
        }
    }
}
