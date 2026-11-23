// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Generated tests of the implicit collection-key probe, with a
    /// fixed seed. The probe lexes on its own, so the differential test checks that it agrees with the reader: an
    /// implicit key <c>X: v</c> reads exactly as the explicit key <c>? X</c> then <c>: v</c>, which needs no probe, and a
    /// collection that is not followed by ':' is never read as a key. The cost test bounds the probe's work on random
    /// one-line input.
    /// </summary>
    public class GeneratedKeyProbeTests
    {
        private const int Seed = 20261118;

        /// <summary>One-line flow collections from a small grammar; each at most 1024 characters (an implicit key).</summary>
        private sealed class Generator
        {
            private readonly Random _random;

            public Generator(int seed) => _random = new Random(seed);

            private string Pick(params string[] choices) => choices[_random.Next(choices.Length)];

            /// <summary>A plain scalar with quotes, blanks, ':', '?' and '#' where they are not indicators.</summary>
            public string Plain()
            {
                var sb = new StringBuilder(Pick("a", "b", "x", "é", "日", "k1", "it"));
                int parts = _random.Next(4);
                for (int i = 0; i < parts; i++)
                {
                    sb.Append(Pick(
                        " \"b", " 'c", "'s", "\"q", " d", ":e", "?f", "#g", " h?", " i:j", "é", " 日本", "-k", " l\"m\"",
                        " n'", "o", "\" p", " :q"));
                }
                string text = sb.ToString();
                // A trailing or doubled blank, or ' #', ': ' would end or change the scalar.
                return text.Contains(" #") || text.Contains(": ") || text.EndsWith(" ") ? "a" : text;
            }

            public string DoubleQuoted()
            {
                var sb = new StringBuilder("\"");
                int parts = _random.Next(5);
                for (int i = 0; i < parts; i++)
                {
                    sb.Append(Pick("a", " ", "]:", "}:", "[", "]", "{", "}", ",", "\\\"", "#", "é", ":", "'", " # x", "] :"));
                }
                return sb.Append('"').ToString();
            }

            public string SingleQuoted()
            {
                var sb = new StringBuilder("'");
                int parts = _random.Next(5);
                for (int i = 0; i < parts; i++)
                {
                    sb.Append(Pick("a", " ", "]:", "}:", "[", "]", "{", "}", ",", "''", "#", "é", ":", "\"", "\\", " # x"));
                }
                return sb.Append('\'').ToString();
            }

            public string Scalar() => _random.Next(3) switch
            {
                0 => Plain(),
                1 => DoubleQuoted(),
                _ => SingleQuoted(),
            };

            public string Node(int depth) => depth >= 3 ? Scalar() : _random.Next(4) switch
            {
                0 => Sequence(depth + 1),
                1 => Mapping(depth + 1),
                _ => Scalar(),
            };

            public string Sequence(int depth)
            {
                int count = _random.Next(4);
                return "[" + string.Join(Pick(", ", ",", " , "), Enumerable.Range(0, count).Select(_ => Node(depth))) + "]";
            }

            public string Mapping(int depth)
            {
                int count = _random.Next(4);
                return "{" + string.Join(", ", Enumerable.Range(0, count).Select(_ => Scalar() + ": " + Node(depth))) + "}";
            }

            public string Collection()
            {
                while (true)
                {
                    string x = _random.Next(2) == 0 ? Sequence(0) : Mapping(0);
                    if (x.Length <= 900)
                    {
                        return x;
                    }
                }
            }
        }

        /// <summary>Every token, with key kinds, scalar text and, for a Complex key, its raw text and own events.</summary>
        private static List<string> Describe(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { MaxDepth = 256 });
            var tokens = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.PropertyName && reader.KeyKind == YamlKeyKind.Complex)
                {
                    var events = new List<string>();
                    YamlEventTrace.AddEvent(ref reader, events);
                    tokens.Add("Key{" + Encoding.UTF8.GetString(reader.ValueSpan.ToArray()) + "}" + string.Join(";", events));
                }
                else
                {
                    var events = new List<string>();
                    YamlEventTrace.AddEvent(ref reader, events);
                    tokens.Add(reader.TokenType + ":" + reader.KeyKind + ":" + string.Join(";", events));
                }
            }
            return tokens;
        }

        private static int ComplexKeys(List<string> tokens) => tokens.Count(t => t.StartsWith("Key{", StringComparison.Ordinal));

        public static IEnumerable<object[]> Batches() => Enumerable.Range(0, 20).Select(b => new object[] { b });

        [Theory]
        [MemberData(nameof(Batches))]
        public void ImplicitKeysReadAsTheirExplicitForm(int batch)
        {
            var generator = new Generator(Seed + batch);
            for (int n = 0; n < 200; n++)
            {
                string x = generator.Collection();
                string context = $"batch {batch}, case {n}: {x}";

                // The explicit forms need no probe; if they fail, the grammar produced invalid YAML.
                List<string> explicitRoot = Describe("? " + x + "\n: v\n");
                List<string> explicitEntry = Describe("[p, ? " + x + ": v, q]");
                List<string> explicitMapKey = Describe("{? " + x + ": v}");

                Assert.True(explicitRoot.SequenceEqual(Describe(x + ": v\n")), "root: " + context);
                Assert.True(explicitEntry.SequenceEqual(Describe("[p, " + x + ": v, q]")), "flow sequence entry: " + context);
                Assert.True(explicitMapKey.SequenceEqual(Describe("{" + x + ": v}")), "flow mapping key: " + context);
                Assert.Equal(1, ComplexKeys(explicitRoot));

                // Neighbours keep their own shape: a collection not followed by ':' is never a key.
                Assert.True(ComplexKeys(Describe(x + "\n")) == 0, "alone: " + context);
                Assert.True(ComplexKeys(Describe("[" + x + ", q]")) == 0, "entry: " + context);
                Assert.True(ComplexKeys(Describe("[" + x + ", " + x + ": v, " + x + "]")) == 1, "between neighbours: " + context);
                Assert.True(ComplexKeys(Describe("{k: " + x + ", " + x + ": v}")) == 1, "mapping value and key: " + context);

                if (n % 40 == 0)
                {
                    ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(x + ": v\n"), default);
                    ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes("[p, " + x + ": v, " + x + "]"), default);
                }
            }
        }

        /// <summary>Random one-line token soup (valid or not) and long generated lines.</summary>
        public static IEnumerable<object[]> CostInputs()
        {
            foreach (int size in new[] { 16 * 1024, 128 * 1024 })
            {
                foreach (bool final in new[] { true, false })
                {
                    for (int seed = 0; seed < 6; seed++)
                    {
                        yield return new object[] { "soup", size, final, seed };
                        yield return new object[] { "lines", size, final, seed };
                    }
                }
            }
        }

        private static string Soup(int size, int seed)
        {
            var random = new Random(Seed + seed);
            string[] tokens = { "[", "]", "{", "}", "\"", "'", ",", ":", " ", "#", "a", "é", "]:", "}:", ": ", "\\", "x \"y", "?", "[a]: b" };
            var sb = new StringBuilder();
            while (sb.Length < size)
            {
                sb.Append(tokens[random.Next(tokens.Length)]);
            }
            return sb.ToString();
        }

        private static string Lines(int size, int seed)
        {
            // A one-line flow sequence of generated collections, some of them keys, with "]:" strings mixed in.
            var generator = new Generator(Seed + 1000 + seed);
            var random = new Random(seed);
            var sb = new StringBuilder("[");
            while (sb.Length < size)
            {
                string x = generator.Collection();
                sb.Append(random.Next(4) switch
                {
                    0 => x + ": v",
                    1 => "\"]:\"",
                    _ => x,
                }).Append(", ");
            }
            return sb.Append("z]").ToString();
        }

        [Theory]
        [MemberData(nameof(CostInputs))]
        public void ProbeWorkIsLinearOnGeneratedInput(string kind, int size, bool final, int seed)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(kind == "soup" ? Soup(size, seed) : Lines(size, seed));
            var reader = new Utf8YamlReader(utf8, final, new YamlReaderState(new YamlReaderOptions { MaxDepth = 100_000 }));
            try
            {
                while (reader.Read())
                {
                }
            }
            catch (YamlException)
            {
                // Soup is mostly not YAML; the bound holds up to the error.
            }

            double ratio = (double)reader.YamlProbeBytesExamined / utf8.Length;
            Assert.True(ratio <= 4, $"{kind} {size} final:{final} seed:{seed}: {reader.YamlProbeBytesExamined:N0} bytes examined, {ratio:F2} per input byte.");
        }
    }
}
