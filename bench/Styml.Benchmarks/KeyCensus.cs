// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Styml.Benchmarks
{
    /// <summary>
    /// M4p candidate g, step 1 (the census): per corpus document and input form, the bytes one Styml deserialize allocates,
    /// and the dictionary-key strings it creates (count, distinct count, bytes), found by walking the result. A string's
    /// size is its object size on 64-bit: (22 + 2n) rounded up to 8.
    /// </summary>
    public static class KeyCensus
    {
        public static int Run(TextWriter output)
        {
            output.WriteLine($"{"document",-11} {"form",-4} {"alloc B",10} {"keys",7} {"distinct",8} {"key B",9} {"share",6} {"max len",7} {"<=32 B",6}");
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                foreach ((string form, byte[] input) in new[] { ("yaml", doc.Yaml), ("json", doc.Json) })
                {
                    Census(output, name, form, doc, input, generated: false);
                }
            }

            output.WriteLine();
            output.WriteLine("Source-generated (StymlGenerated):");
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                foreach ((string form, byte[] input) in new[] { ("yaml", doc.Yaml), ("json", doc.Json) })
                {
                    Census(output, name, form, doc, input, generated: true);
                }
            }

            return 0;
        }

        private static void Census(TextWriter output, string name, string form, Doc doc, byte[] input, bool generated)
        {
            object? Read() => generated
                ? Libraries.StymlGeneratedRead(input, doc.Family, Libraries.StymlGenerated)
                : Libraries.Styml(input, doc.Family, Libraries.StymlOptions);

            for (int i = 0; i < 20; i++)
            {
                Read();
            }

            const int Rounds = 50;
            long before = GC.GetAllocatedBytesForCurrentThread();
            object? result = null;
            for (int i = 0; i < Rounds; i++)
            {
                result = Read();
            }

            long alloc = (GC.GetAllocatedBytesForCurrentThread() - before) / Rounds;

            var keys = new List<string>();
            Walk(result, keys, new HashSet<object>(ReferenceEqualityComparer.Instance));
            var distinct = new HashSet<string>(keys, StringComparer.Ordinal);
            long keyBytes = 0;
            int maxLen = 0, short32 = 0;
            foreach (string k in keys)
            {
                keyBytes += (22 + 2L * k.Length + 7) & ~7L;
                maxLen = Math.Max(maxLen, System.Text.Encoding.UTF8.GetByteCount(k));
                if (System.Text.Encoding.UTF8.GetByteCount(k) <= 32)
                {
                    short32++;
                }
            }

            output.WriteLine($"{name,-11} {form,-4} {alloc,10} {keys.Count,7} {distinct.Count,8} {keyBytes,9} {100.0 * keyBytes / alloc,5:F1}% {maxLen,7} {short32,6}");
        }

        /// <summary>The dictionary keys of one deserialize of a document, in the order they are read.</summary>
        public static List<string> DictionaryKeys(Doc doc)
        {
            var keys = new List<string>();
            Walk(Libraries.Styml(doc.Yaml, doc.Family, Libraries.StymlOptions), keys, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return keys;
        }

        private static void Walk(object? value, List<string> keys, HashSet<object> seen)
        {
            if (value is null || value is string || value.GetType().IsPrimitive || value is Enum || value is decimal
                || value is DateTime || value is DateTimeOffset || value is Guid || value is TimeSpan || value is Uri)
            {
                return;
            }

            if (!value.GetType().IsValueType && !seen.Add(value))
            {
                return;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key)
                    {
                        keys.Add(key);
                    }

                    Walk(entry.Value, keys, seen);
                }

                return;
            }

            if (value is IEnumerable sequence)
            {
                foreach (object? item in sequence)
                {
                    Walk(item, keys, seen);
                }

                return;
            }

            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0 && property.CanRead)
                {
                    Walk(property.GetValue(value), keys, seen);
                }
            }
        }
    }
}
