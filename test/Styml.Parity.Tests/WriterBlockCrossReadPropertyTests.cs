// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// G5 from M4b: a cross-library property test, in two configurations
    /// (what no standard resolves is quoted only with QuoteForLenientReaders): with the
    /// option, every string must read back as itself (a hard oracle); without it, the classes of strings each library
    /// misreads are a pinned list (<see cref="DefaultMisreads"/>), so a change in the default set shows.
    /// </summary>
    /// <remarks> Generated strings, weighted
    /// towards the forms other readers resolve (signs, underscores, Unicode white space at an edge, number and keyword
    /// shapes), are written in block style as sequence items, mapping values and keys, with and without the literal hint.
    /// YamlDotNet and SharpYaml read each output into <c>object</c>, and every string must come back as a string with
    /// the same text. SharpYaml resolves plain scalars in such a read, so this checks the quoting against its resolver;
    /// YamlDotNet's checks the structure. The failures of this test defined the set of M4b's fixes.
    /// </remarks>
    public class WriterBlockCrossReadPropertyTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();

        [Fact]
        public void GeneratedStrings_ReadBackAsStrings_InYamlDotNetAndSharpYaml() => Run(20_000, 20261120, lenient: true);

        [Fact]
        [Trait("category", "OuterLoop")]
        public void GeneratedStrings_ReadBackAsStrings_InYamlDotNetAndSharpYaml_Large() => Run(1_000_000, 20261121, lenient: true);

        /// <summary>
        /// The default (standard) quoting: the classes of the strings YamlDotNet and SharpYaml misread, as library,
        /// position and class. Every misread string must fall in a class of <see cref="MisreadClass"/>; a string no
        /// class names is a gap in the default set and fails.
        /// </summary>
        [Fact]
        public void GeneratedStrings_Default_MisreadClassesArePinned()
        {
            SortedSet<string> classes = Misreads(20_000, 20261120);
            Assert.True(DefaultMisreads.SequenceEqual(classes), "The misread classes are now:\n" + string.Join("\n", classes.Select(c => "            " + Escape(c) + ",")));
        }

        // Pinned on 2026-11-19 (seed 20261120, 20,000 strings): SharpYaml's object read (it resolves plain scalars) and
        // YamlDotNet's keys. SharpYaml reads keys as strings into object, so its keys are not listed.
        // M5f: "a block scalar line that starts with a tab" left the list, since a first line that starts
        // with a tab is no longer written as a block scalar.
        internal static readonly string[] DefaultMisreads =
        [
            "SharpYaml, item: Unicode white space at an edge",
            "SharpYaml, item: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
            "SharpYaml, item: a keyword in a case no standard lists",
            "SharpYaml, item: a number with '_' where no standard has it",
            "SharpYaml, item: an IEEE name in a form no standard lists",
            "SharpYaml, value: Unicode white space at an edge",
            "SharpYaml, value: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
            "SharpYaml, value: a keyword in a case no standard lists",
            "SharpYaml, value: a number with '_' where no standard has it",
            "SharpYaml, value: an IEEE name in a form no standard lists",
            "YamlDotNet, key: a key with ' *'",
        ];

        private static SortedSet<string> Misreads(int count, int seed)
        {
            var random = new Random(seed);
            var classes = new SortedSet<string>(StringComparer.Ordinal);
            const int Batch = 250;
            for (int done = 0; done < count; done += Batch)
            {
                string[] strings = Enumerable.Range(0, Math.Min(Batch, count - done)).Select(_ => Generate(random)).Where(s => s != "<<").Distinct().ToArray();
                bool literal = (done / Batch) % 3 == 2;
                if (!BatchReadsBack(strings, literal, lenient: false))
                {
                    foreach (string s in strings)
                    {
                        foreach ((string library, string position) in Failures(s, literal, lenient: false))
                        {
                            classes.Add($"{library}, {position}: {MisreadClass(s, position == "key", literal && position != "key", flow: false)}");
                        }
                    }
                }
            }

            return classes;
        }

        /// <summary>
        /// The class of a string a lenient reader misreads, by the rules of <see cref="YamlWriterOptions.QuoteForLenientReaders"/>,
        /// or "outside the option's classes" followed by the string.
        /// </summary>
        internal static string MisreadClass(string s, bool key, bool blockScalar, bool flow)
        {
            if (blockScalar && s.Split('\n').Any(line => line.StartsWith('\t')))
            {
                return "a block scalar line that starts with a tab";
            }

            if (key && s.Contains(" *", StringComparison.Ordinal))
            {
                return "a key with ' *'";
            }

            if (flow && s.IndexOfAny(new[] { '?', ':' }) >= 0)
            {
                return "'?' or ':' in a flow plain scalar";
            }

            if (s.Length > 0 && ((s[0] > 0x7F && char.IsWhiteSpace(s[0])) || (s[^1] > 0x7F && char.IsWhiteSpace(s[^1]))))
            {
                return "Unicode white space at an edge";
            }

            string t = s.Trim();
            string unsigned = t.TrimStart('+', '-');
            string word = unsigned.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
            if (word is "nan" or "inf" or "infinity" or ".nan" or ".inf")
            {
                return "an IEEE name in a form no standard lists";
            }

            if (word is "true" or "false" or "null" or "y" or "n" or "yes" or "no" or "on" or "off")
            {
                return "a keyword in a case no standard lists";
            }

            if (unsigned.Length > 1 && unsigned[0] == '0' && "oOxXbB".IndexOf(unsigned[1]) >= 0)
            {
                return "a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'";
            }

            if (t.Contains('_', StringComparison.Ordinal))
            {
                return "a number with '_' where no standard has it";
            }

            return "outside the option's classes: " + Escape(s);
        }

        // The findings of M4a's review, each as item, value and
        // key, with and without the literal hint.
        [Theory]
        [InlineData("+NaN")]
        [InlineData("-NaN")]
        [InlineData("+nan")]
        [InlineData("+0o17")]
        [InlineData("-0o17")]
        [InlineData("0o1_7")]
        [InlineData("_1")]
        [InlineData("\u00a0")]
        [InlineData("0\u00a0")]
        [InlineData("\u00a00")]
        [InlineData("\u20030")]
        [InlineData("0\u3000")]
        [InlineData("\u00a0~")]
        [InlineData("a\u00a0")]
        [InlineData("a *b")]
        [InlineData("/ *a")]
        [InlineData("src/ *.cs")]
        [InlineData("a *")]
        [InlineData("\tx\n")]
        [InlineData("\n\tx\n")]
        [InlineData("a\n\tb")]
        public void AcceptanceFindings_ReadBackAsStrings(string s)
        {
            var failures = Check(s, literal: false, lenient: true).Concat(Check(s, literal: true, lenient: true)).ToList();
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        private static void Run(int count, int seed, bool lenient)
        {
            var random = new Random(seed);
            var failures = new SortedSet<string>(StringComparer.Ordinal);
            const int Batch = 250;
            for (int done = 0; done < count && failures.Count < 40; done += Batch)
            {
                // "<<" as a key is SharpYaml's own refusal (it takes a quoted "<<" as a merge key; M4.0).
                string[] strings = Enumerable.Range(0, Math.Min(Batch, count - done)).Select(_ => Generate(random)).Where(s => s != "<<").Distinct().ToArray();
                bool literal = (done / Batch) % 3 == 2;
                if (!BatchReadsBack(strings, literal, lenient))
                {
                    // Find the strings at fault, one by one.
                    foreach (string s in strings)
                    {
                        foreach (string failure in Check(s, literal, lenient))
                        {
                            failures.Add(failure);
                        }
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        private static string Items(string[] strings, bool literal, bool lenient) => Write(lenient, w =>
        {
            w.WriteStartArray();
            foreach (string s in strings)
            {
                if (literal)
                {
                    w.WriteScalarStyle(YamlScalarStyle.Literal);
                }

                w.WriteStringValue(s);
            }

            w.WriteEndArray();
        });

        private static string Values(string[] strings, bool literal, bool lenient) => Write(lenient, w =>
        {
            w.WriteStartObject();
            for (int i = 0; i < strings.Length; i++)
            {
                if (literal)
                {
                    w.WriteScalarStyle(YamlScalarStyle.Literal);
                }

                w.WriteString("k" + i.ToString(CultureInfo.InvariantCulture), strings[i]);
            }

            w.WriteEndObject();
        });

        private static string Keys(string[] strings, bool lenient) => Write(lenient, w =>
        {
            w.WriteStartObject();
            for (int i = 0; i < strings.Length; i++)
            {
                w.WriteNumber(strings[i], i);
            }

            w.WriteEndObject();
        });

        private static string Write(bool lenient, Action<Utf8YamlWriter> calls)
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = lenient }))
            {
                calls(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static bool BatchReadsBack(string[] strings, bool literal, bool lenient)
        {
            try
            {
                string items = Items(strings, literal, lenient);
                string values = Values(strings, literal, lenient);
                string keys = Keys(strings, lenient);
                return ItemsMatch(strings, s_yamlDotNet.Deserialize<object>(items)) && ItemsMatch(strings, SY.YamlSerializer.Deserialize<object>(items))
                    && ValuesMatch(strings, s_yamlDotNet.Deserialize<object>(values)) && ValuesMatch(strings, SY.YamlSerializer.Deserialize<object>(values))
                    && KeysMatch(strings, s_yamlDotNet.Deserialize<object>(keys)) && KeysMatch(strings, SY.YamlSerializer.Deserialize<object>(keys));
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The library and position of each misreading of one string.
        private static IEnumerable<(string Library, string Position)> Failures(string s, bool literal, bool lenient) =>
            Check(s, literal, lenient).Select(f => (f.Substring(0, f.IndexOf(',')), f.Substring(f.IndexOf(',') + 2, f.IndexOfAny(new[] { ',', ':' }, f.IndexOf(',') + 2) - f.IndexOf(',') - 2))).Distinct();

        private static IEnumerable<string> Check(string s, bool literal, bool lenient)
        {
            string[] one = { s };
            foreach ((string position, Func<string> write, Func<object?, bool> match) in new (string, Func<string>, Func<object?, bool>)[]
            {
                ("item", () => Items(one, literal, lenient), o => ItemsMatch(one, o)),
                ("value", () => Values(one, literal, lenient), o => ValuesMatch(one, o)),
                ("key", () => Keys(one, lenient), o => KeysMatch(one, o)),
            })
            {
                string yaml = write();
                foreach ((string library, Func<string, object?> read) in new (string, Func<string, object?>)[]
                {
                    ("YamlDotNet", y => s_yamlDotNet.Deserialize<object>(y)),
                    ("SharpYaml", y => SY.YamlSerializer.Deserialize<object>(y)),
                })
                {
                    string? failure = null;
                    try
                    {
                        object? read1 = read(yaml);
                        if (!match(read1))
                        {
                            failure = $"read back as {Describe(read1)}";
                        }
                    }
                    catch (Exception e)
                    {
                        failure = $"threw {e.GetType().Name}";
                    }

                    if (failure is not null)
                    {
                        yield return $"{library}, {position}{(literal && position != "key" ? ", literal" : "")}: {Escape(s)} written {Escape(yaml)} {failure}";
                    }
                }
            }
        }

        private static bool ItemsMatch(string[] strings, object? read) =>
            read is IList list && list.Count == strings.Length && Enumerable.Range(0, strings.Length).All(i => list[i] is string t && t == strings[i]);

        private static bool ValuesMatch(string[] strings, object? read)
        {
            if (read is not IDictionary map || map.Count != strings.Length)
            {
                return false;
            }

            for (int i = 0; i < strings.Length; i++)
            {
                if (map["k" + i.ToString(CultureInfo.InvariantCulture)] is not string t || t != strings[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool KeysMatch(string[] strings, object? read)
        {
            if (read is not IDictionary map || map.Count != strings.Length)
            {
                return false;
            }

            var keys = new List<object>();
            foreach (DictionaryEntry e in map)
            {
                keys.Add(e.Key);
            }

            return Enumerable.Range(0, strings.Length).All(i => keys[i] is string t && t == strings[i]);
        }

        private static string Describe(object? o) => o switch
        {
            null => "null",
            IList l => "list[" + string.Join(", ", l.Cast<object?>().Select(Describe)) + "]",
            IDictionary d => "map{" + string.Join(", ", d.Cast<DictionaryEntry>().Select(e => Describe(e.Key) + ": " + Describe(e.Value))) + "}",
            string s => "string " + Escape(s),
            _ => o.GetType().Name + " " + Convert.ToString(o, CultureInfo.InvariantCulture),
        };

        private static string Escape(string s) => System.Text.Json.JsonSerializer.Serialize(s);

        // -------------------------------------------------------------------------------------------------------------
        // The generator: Styml.Yaml.Tests' (BlockQuotingPropertyTests), with Unicode white space, underscores, signs and
        // the number and keyword shapes other readers accept.

        private static readonly string[] s_keywords =
        [
            "true", "false", "null", "y", "n", "yes", "no", "on", "off", "nan", "inf", "infinity", ".inf", ".nan", "~", "<<", "=",
        ];

        private static readonly string[] s_numbers =
        [
            "0", "1", "12", "012", "0777", "0o17", "0x1F", "0b1010", "1_000", "1.5", ".5", "1.", "1e3", "1E-3", "1.0e+3", "0.0",
            "190:20:30", "190:20:30.15", "1:30", "12:30:45", "2001-12-14", "2001-12-14t21:59:43.10-05:00", "1.2.3", "1.10",
            "0x_1F", "0b_1", "1__0", "_1", "1_", "__1", "0o1_7", "1e+3", "0.5e-3", "0e0", "1.5E3", "0xff", "0X1F",
            "1,000", "1 000", "∞", "-∞", "1e", "e3", "0.1.2", "$1", "1%", "(1)", "1-", "1+", "0x", "0o", "0b", "1d", "1f", "1m",
            "1L", "0.", "-.", "+.", "00", "-0x1", "1:2:3.4", "1.0E+400", "NaN", "Infinity", "-Infinity", "1e-400",
        ];

        private const string WhiteSpace = "        　\u0085​﻿";

        private const string Alphabet =
            "abcxyzABCXYZTFNYOI0123456789-?:,[]{}#&*!|>'\"%@`.+_~=< \t\n\r\\/é😀\u0001\u007f\u0090" + WhiteSpace;

        internal static string Generate(Random random)
        {
            switch (random.Next(10))
            {
                case 0:
                    return Sign(random) + RandomCase(random, s_keywords[random.Next(s_keywords.Length)]);
                case 1:
                case 2:
                    return Sign(random) + s_numbers[random.Next(s_numbers.Length)];
                case 3:
                    // A number or keyword with white space or a piece at an edge.
                    string core = random.Next(2) == 0 ? s_numbers[random.Next(s_numbers.Length)] : s_keywords[random.Next(s_keywords.Length)];
                    return Edge(random) + Sign(random) + core + Edge(random);
                case 4:
                    return random.Next(3) switch
                    {
                        0 => random.Next(-100000, 100000).ToString(CultureInfo.InvariantCulture),
                        1 => (random.NextDouble() * 1000).ToString("R", CultureInfo.InvariantCulture),
                        _ => Sign(random) + "0x" + random.Next().ToString("X", CultureInfo.InvariantCulture),
                    };
                case 5:
                    // Words separated by spaces, some starting with an indicator ("a *b").
                    int words = random.Next(1, 4);
                    return string.Join(" ", Enumerable.Range(0, words).Select(_ => (random.Next(3) == 0 ? "*&!|>%@".Substring(random.Next(7), 1) : "") + Piece(random, 4)));
                default:
                    return Piece(random, 8) + (random.Next(3) == 0 ? "" : Piece(random, 8));
            }
        }

        private static string Sign(Random random) => random.Next(4) switch { 0 => "+", 1 => "-", _ => "" };

        private static string Edge(Random random) => random.Next(3) switch
        {
            0 => WhiteSpace[random.Next(WhiteSpace.Length)].ToString(),
            1 => Piece(random, 2),
            _ => "",
        };

        private static string Piece(Random random, int max)
        {
            int length = random.Next(0, max);
            var builder = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                int index = random.Next(Alphabet.Length);
                char c = Alphabet[index];
                if (char.IsHighSurrogate(c))
                {
                    builder.Append(c).Append(Alphabet[index + 1]);
                }
                else if (!char.IsLowSurrogate(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static string RandomCase(Random random, string s)
        {
            char[] chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (random.Next(2) == 0)
                {
                    chars[i] = char.ToUpperInvariant(chars[i]);
                }
            }

            return new string(chars);
        }
    }
}
