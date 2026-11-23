// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Oracle 5: generated strings, weighted towards the hard cases,
    /// written in block style as values and as keys, with and without the literal hint, must read back as the same
    /// string under the Core and Yaml11 schemas. A value must come back as a <see cref="YamlTokenType.String"/> token:
    /// under D2, GetString succeeds on a number or boolean token too, so the text alone proves nothing. Also the
    /// derivation of the resolution check's first-byte prefilter and of the letter rule (variant D) by enumeration.
    /// </summary>
    public class BlockQuotingPropertyTests
    {
        private const int Seed = 20261119;

        [Fact]
        public void GeneratedStrings_ReadBackAsTheSameString() => Run(100_000, Seed);

        [Fact]
        [Trait("category", "OuterLoop")]
        public void GeneratedStrings_ReadBackAsTheSameString_TenMillion() => Run(10_000_000, Seed + 1);

        private static void Run(int count, int seed)
        {
            var random = new Random(seed);
            const int Batch = 500;
            var failures = new List<string>();
            var options = new[]
            {
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" },
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\r\n", IndentSize = 4, IndentSequences = true },
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default },

                // M4b: folding at a small width, and folded as the multi-line default.
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 10 },
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\r\n", MultiLineStringStyle = YamlScalarStyle.Folded, MaxLineWidth = 30 },

                // The quoting for lenient readers, which quotes more and must read back the same.
                new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = true },
            };
            for (int done = 0; done < count && failures.Count < 20; done += Batch)
            {
                string[] strings = new string[Math.Min(Batch, count - done)];
                for (int i = 0; i < strings.Length; i++)
                {
                    strings[i] = Generate(random);
                }

                YamlWriterOptions writerOptions = options[(done / Batch) % options.Length];
                Check(strings, writerOptions, YamlScalarStyle.None, flow: false, failures);
                Check(strings, writerOptions, YamlScalarStyle.Literal, flow: false, failures);

                // M4b: one more form per batch, in turn.
                (YamlScalarStyle hint, bool flow) = ((done / Batch) % 4) switch
                {
                    0 => (YamlScalarStyle.Folded, false),
                    1 => (YamlScalarStyle.SingleQuoted, false),
                    2 => (YamlScalarStyle.None, true),
                    _ => (YamlScalarStyle.SingleQuoted, true),
                };
                Check(strings, writerOptions, hint, flow, failures);
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // `hint` is written before every string (None: no hint); `flow` writes everything inside flow collections.
        private static void Check(string[] strings, YamlWriterOptions options, YamlScalarStyle hint, bool flow, List<string> failures)
        {
            string literal = hint.ToString() + (flow ? ", flow" : "");
            // As values: one sequence item each, and as mapping values after a key.
            string values = BlockWriterTests.Write(w =>
            {
                if (flow)
                {
                    w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                }

                w.WriteStartArray();
                for (int i = 0; i < strings.Length; i++)
                {
                    string s = strings[i];
                    if (hint != YamlScalarStyle.None)
                    {
                        w.WriteScalarStyle(hint);
                    }

                    WriteValue(w, s, i % 5);
                    w.WriteStartObject();
                    if (hint != YamlScalarStyle.None)
                    {
                        w.WriteScalarStyle(hint);
                    }

                    WriteNameAndValue(w, s, (i + 2) % 5);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }, options);

            // As keys (a hint before the key styles the key; a literal key falls back to the default).
            string keys = BlockWriterTests.Write(w =>
            {
                if (flow)
                {
                    w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                }

                w.WriteStartArray();
                for (int i = 0; i < strings.Length; i++)
                {
                    w.WriteStartObject();
                    if (hint != YamlScalarStyle.None)
                    {
                        w.WriteScalarStyle(hint);
                    }

                    WriteName(w, strings[i], (i + 1) % 5);
                    w.WriteNumberValue(1);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }, options);

            foreach (YamlSchemaKind schema in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                List<BlockWriterTests.Token> valueTokens;
                List<BlockWriterTests.Token> keyTokens;
                try
                {
                    valueTokens = BlockWriterTests.ReadTokens(values, schema);
                    keyTokens = BlockWriterTests.ReadTokens(keys, schema);
                }
                catch (Exception e)
                {
                    failures.Add($"{schema}, {literal}, width {options.MaxLineWidth}: the output does not read: {e.Message}\n{Escape(values)}");
                    return;
                }

                // [ (s { k s })* ]: five tokens per string
                for (int i = 0; i < strings.Length; i++)
                {
                    BlockWriterTests.Token item = valueTokens[1 + 5 * i];
                    BlockWriterTests.Token mapped = valueTokens[1 + 5 * i + 3];
                    if (item != new BlockWriterTests.Token(YamlTokenType.String, strings[i]) || mapped != item)
                    {
                        failures.Add($"{schema}, {literal}, width {options.MaxLineWidth}: value {Escape(strings[i])} read back as {item.Type} {Escape(item.Text)} / {mapped.Type} {Escape(mapped.Text)}");
                    }

                    // [ ({ key 1 })* ]
                    BlockWriterTests.Token key = keyTokens[1 + 4 * i + 1];
                    if (key != new BlockWriterTests.Token(YamlTokenType.PropertyName, strings[i]))
                    {
                        failures.Add($"{schema}, {literal}, width {options.MaxLineWidth}: key {Escape(strings[i])} read back as {key.Type} {Escape(key.Text)}");
                    }
                }
            }
        }

        // The input forms of every string overload family, each reaching its own block path: UTF-16 (string and span),
        // UTF-8, and pre-encoded text (default encoder, which escapes non-ASCII, and the relaxed one, which does not).
        private static readonly System.Text.Encodings.Web.JavaScriptEncoder s_relaxed = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        private static void WriteValue(Utf8YamlWriter w, string s, int form)
        {
            switch (form)
            {
                case 0: w.WriteStringValue(s); break;
                case 1: w.WriteStringValue(s.AsSpan()); break;
                case 2: w.WriteStringValue(Encoding.UTF8.GetBytes(s)); break;
                case 3: w.WriteStringValue(YamlEncodedText.Encode(s)); break;
                default: w.WriteStringValue(YamlEncodedText.Encode(s, s_relaxed)); break;
            }
        }

        private static void WriteName(Utf8YamlWriter w, string s, int form)
        {
            switch (form)
            {
                case 0: w.WritePropertyName(s); break;
                case 1: w.WritePropertyName(s.AsSpan()); break;
                case 2: w.WritePropertyName(Encoding.UTF8.GetBytes(s)); break;
                case 3: w.WritePropertyName(YamlEncodedText.Encode(s)); break;
                default: w.WritePropertyName(YamlEncodedText.Encode(s, s_relaxed)); break;
            }
        }

        // A key "k" and the string as its value, in one call.
        private static void WriteNameAndValue(Utf8YamlWriter w, string s, int form)
        {
            switch (form)
            {
                case 0: w.WriteString("k", s); break;
                case 1: w.WriteString("k".AsSpan(), s.AsSpan()); break;
                case 2: w.WriteString("k"u8, Encoding.UTF8.GetBytes(s)); break;
                case 3: w.WriteString(YamlEncodedText.Encode("k"), s.AsSpan()); break;
                default: w.WriteString("k".AsSpan(), YamlEncodedText.Encode(s, s_relaxed)); break;
            }
        }

        private static string Escape(string? s) => s is null ? "null" : System.Text.Json.JsonSerializer.Serialize(s);

        // -------------------------------------------------------------------------------------------------------------
        // The generator

        private static readonly string[] s_keywords =
        [
            "true", "false", "null", "y", "n", "yes", "no", "on", "off", "nan", "inf", "infinity", "+inf", "-inf",
            "+infinity", "-infinity", ".inf", "-.inf", "+.inf", ".nan", "~", "<<", "=", "Null", "NULL", "True", "TRUE",
        ];

        private static readonly string[] s_forms =
        [
            "12", "-12", "+12", "012", "0777", "0o17", "0x1F", "0X1f", "0b1010", "1_000", "1.5", "-1.5", ".5", "1.", "1e3",
            "1E-3", "1.0e+3", "190:20:30", "190:20:30.15", "1:30", "12:30:45", "-1:30", "2001-12-14", "2001-12-14t21:59:43.10-05:00",
            "2001-12-14 21:59:43.10 -5", "2002-12-14", "2001-1-1 1:02:03", "1.2.3", "1.10", "10:02:03", "01:02:03", "0.", "-0",
            "--port=8080", "-v", "-a b", "--", "-- a", "?x", "- a", "? a", ":x", "a:b", "a: b", "a:", "a #b", "a#b", "#a",
            "C:\\x", "a\"b", "\"a\"", "'a'", "a'b", "%x", "@x", "`x", "&a", "*a", "!a", "|", ">", "[a]", "{a}", "a,b",
            "---", "--- a", "...", "... a", "a\n", "\na", "a\n\nb", "a\r\nb", "\t", " ", "  ", "a ", " a", "é", "😀",
            "https://example.com/a?b=c#d", "100%", "a\u0085b", "a\u2028b", "a\u2029b", "\ufeff", "a\uffffb", "\u007f",
            "\u0000", "\u001f", "\u0080", "\u009f", "\u00a0", "a\\nb",
        ];

        private const string Alphabet =
            "abcxyzABCXYZTFNYOI0123456789-?:,[]{}#&*!|>'\"%@`.+_~=< \t\n\r\\/é😀\u0085\u2028\u2029\u0001\u007f\u0090\ufeff\uffff\u00a0";

        internal static string Generate(Random random)
        {
            int kind = random.Next(10);
            switch (kind)
            {
                case 0:
                    return RandomCase(random, s_keywords[random.Next(s_keywords.Length)]);
                case 1:
                case 2:
                    return s_forms[random.Next(s_forms.Length)];
                case 3:
                    // A form with a piece around it.
                    return Piece(random) + s_forms[random.Next(s_forms.Length)] + (random.Next(2) == 0 ? "" : Piece(random));
                case 4:
                    return random.Next(4) switch
                    {
                        0 => random.Next(-100000, 100000).ToString(Globalization.CultureInfo.InvariantCulture),
                        1 => (random.NextDouble() * 1000).ToString("R", Globalization.CultureInfo.InvariantCulture),
                        2 => "0x" + random.Next().ToString("X", Globalization.CultureInfo.InvariantCulture),
                        _ => $"{random.Next(1, 999)}:{random.Next(0, 60):D2}" + (random.Next(2) == 0 ? "" : $":{random.Next(0, 60):D2}"),
                    };
                case 5 when random.Next(50) == 0:
                    // Long enough for an explicit key.
                    return new string('k', 1020) + Piece(random) + Piece(random);
                default:
                    return Piece(random) + (random.Next(3) == 0 ? "" : Piece(random));
            }
        }

        private static string Piece(Random random)
        {
            int length = random.Next(0, 8);
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

        // -------------------------------------------------------------------------------------------------------------
        // The prefilter and the letter rule, derived

        [Fact]
        public void Prefilter_LetsThroughEveryStringThatResolves()
        {
            // Every string of one to three printable ASCII bytes, and the generator's forms and keywords.
            var candidates = new List<byte[]>();
            for (int a = 0x20; a < 0x7F; a++)
            {
                candidates.Add(new[] { (byte)a });
                for (int b = 0x20; b < 0x7F; b++)
                {
                    candidates.Add(new[] { (byte)a, (byte)b });
                    for (int c = 0x20; c < 0x7F; c++)
                    {
                        candidates.Add(new[] { (byte)a, (byte)b, (byte)c });
                    }
                }
            }

            foreach (string s in s_forms.Concat(s_keywords).Concat(s_keywords.Select(k => k.ToUpperInvariant())))
            {
                candidates.Add(Encoding.UTF8.GetBytes(s));
            }

            var failures = new List<string>();
            foreach (byte[] candidate in candidates)
            {
                ReadOnlySpan<byte> s = candidate;
                if (s.IsEmpty)
                {
                    continue;
                }

                bool resolves = YamlReaderHelper.ResolveYamlPlainScalar(s, yaml11: true) != YamlTokenType.String
                    || YamlWriterHelper.IsYamlKeywordOrLegacyForm(s);
                if (resolves && !YamlWriterHelper.PassesResolutionPrefilter(s[0]))
                {
                    failures.Add(Encoding.UTF8.GetString(candidate) + ": resolves, but its first byte skips the resolver");
                }

                // The letter rule: a letter-initial string resolves only as a keyword of at most five bytes, in a case the
                // standards list; the default quotes exactly those, and the option
                // quotes the lenient readers' forms on top.
                bool letter = (uint)((s[0] | 0x20) - 'a') <= 'z' - 'a';
                bool quoted = YamlWriterHelper.ResolvesToNonString(s);
                if (letter && resolves != quoted)
                {
                    failures.Add(Encoding.UTF8.GetString(candidate) + ": the letter rule disagrees with the resolver");
                }

                // With the option, everything the default quotes is still quoted.
                if (quoted && !YamlWriterHelper.ResolvesToNonString(s, lenient: true))
                {
                    failures.Add(Encoding.UTF8.GetString(candidate) + ": the option quotes less than the default");
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        // -------------------------------------------------------------------------------------------------------------
        // M4p candidate d: the non-letter path's byte table, against the chain it replaces

        private const string ResolvableByteList = "0123456789+-._:abcdefABCDEFoxinINTtZ \t";

        // One byte per class the matchers tell apart, an outsider letter of each case, the bytes the prefilter lets
        // through, and a byte at or above 0x80 (four-byte strings); a smaller set for five bytes
        private const string ClassRepresentatives = "05+-._:aefxoinINTtZ \tXgG~<=/,s\u00e9";
        private const string ClassRepresentatives5 = "0+-._:axinTZ \tg\u00e9";

        // A resolving witness for each letter and white-space byte of the table, longer than the enumeration reaches
        private static readonly string[] s_tableWitnesses =
        [
            "2001-12-14T21:59:43Z", "2001-12-14t21:59:43", "2001-12-14\t21:59:43", "2001-12-14 21:59:43.5 +05:00", ".NaN", ".NAN",
            ".nan", ".Inf", "-.INF", "+.inf", "0xAF", "0xbeef", "0x_", "0b__", "0b1_0", "0o17", "1:30.5", "1_0:30.5", "1e+3",
            "1E-3", "12:30:45",
        ];

        private static bool OldChain(ReadOnlySpan<byte> s) =>
            YamlReaderHelper.ResolveYamlPlainScalar(s, yaml11: true) != YamlTokenType.String || YamlWriterHelper.IsYamlKeywordOrLegacyForm(s);

        private static IEnumerable<byte[]> NonLetterCandidates()
        {
            // Every string of one to three printable ASCII bytes
            for (int a = 0x20; a < 0x7F; a++)
            {
                yield return new[] { (byte)a };
                for (int b = 0x20; b < 0x7F; b++)
                {
                    yield return new[] { (byte)a, (byte)b };
                    for (int c = 0x20; c < 0x7F; c++)
                    {
                        yield return new[] { (byte)a, (byte)b, (byte)c };
                    }
                }
            }

            // Every string of four bytes over the class representatives, and of five over the smaller set
            foreach ((int length, string set) in new[] { (4, ClassRepresentatives), (5, ClassRepresentatives5) })
            {
                byte[][] reps = set.Select(ch => Encoding.UTF8.GetBytes(ch.ToString())).ToArray();
                var index = new int[length];
                while (true)
                {
                    yield return index.SelectMany(i => reps[i]).ToArray();
                    int p = length - 1;
                    while (p >= 0 && ++index[p] == reps.Length)
                    {
                        index[p--] = 0;
                    }

                    if (p < 0)
                    {
                        break;
                    }
                }
            }

            // The forms and keywords, and each with every byte outside the table substituted at each position
            byte[] outside = Enumerable.Range(0x20, 0x5F).Select(b => (byte)b).Where(b => ResolvableByteList.IndexOf((char)b) < 0)
                .Concat(new byte[] { 0x80, 0xC3 }).ToArray();
            foreach (string form in s_forms.Concat(s_tableWitnesses).Concat(s_keywords).Concat(s_keywords.Select(k => k.ToUpperInvariant())))
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(form);
                yield return utf8;
                for (int i = 0; i < utf8.Length; i++)
                {
                    foreach (byte b in outside)
                    {
                        byte[] mutated = (byte[])utf8.Clone();
                        mutated[i] = b;
                        yield return mutated;
                    }
                }
            }
        }

        [Fact]
        public void NonLetterPath_EqualsTheChainItReplaces()
        {
            var failures = new List<string>();
            int checkedCount = 0;
            foreach (byte[] candidate in NonLetterCandidates())
            {
                ReadOnlySpan<byte> s = candidate;
                if (s.IsEmpty)
                {
                    continue;
                }

                bool old = OldChain(s);
                bool letter = (uint)((s[0] | 0x20) - 'a') <= 'z' - 'a';
                if (YamlWriterHelper.PassesResolutionPrefilter(s[0]) && !letter)
                {
                    checkedCount++;
                    if (YamlWriterHelper.NonLetterResolvesToNonString(s) != old)
                    {
                        failures.Add(Encoding.UTF8.GetString(candidate) + ": the non-letter path disagrees with the chain");
                    }

                    // With and without the option, the whole check equals the chain it replaced
                    bool lenientOld = old || YamlWriterHelper.QuotedForLenientReaders(s);
                    if (YamlWriterHelper.ResolvesToNonString(s) != old || YamlWriterHelper.ResolvesToNonString(s, lenient: true) != lenientOld)
                    {
                        failures.Add(Encoding.UTF8.GetString(candidate) + ": ResolvesToNonString disagrees with the chain");
                    }
                }

                // The table is the matchers' alphabet: a string with a byte outside it never resolves, whatever its first byte
                if (old && !letter && s[0] is not ((byte)'~' or (byte)'<' or (byte)'='))
                {
                    foreach (byte b in candidate)
                    {
                        if (!YamlWriterHelper.IsResolvableByte(b))
                        {
                            failures.Add(Encoding.UTF8.GetString(candidate) + $": resolves, with the byte 0x{b:X2} outside the table");
                            break;
                        }
                    }
                }
            }

            Assert.True(checkedCount > 500_000, $"{checkedCount} strings reached the non-letter path");
            foreach (string witness in s_tableWitnesses)
            {
                Assert.True(OldChain(Encoding.UTF8.GetBytes(witness)), witness + " resolves");
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        // -------------------------------------------------------------------------------------------------------------
        // M4p candidate e: the option's first-byte classes, against the four matchers they replace

        private const string LenientKeywordLetters = "yYnNoOtTfFiI";
        private const string LenientWholeSetBytes = "+-.,_<= \t0123456789";

        // One representative of each class and of each byte the reduced class 1 test looks at: keyword letters of both
        // cases, other letters, '_', space, tab, a sign, '.', ',', a digit, '<', '=', ':', NBSP, a two-byte letter, U+3000
        // and NEL (four-byte strings); a smaller set for five bytes
        private const string LenientRepresentatives = "yNiTaZ_ \t0+.,<=:\u00a0\u00e9\u3000\u0085";
        private const string LenientRepresentatives5 = "yNa_ \t0+\u00a0\u00e9";

        // Unicode white space the edge test knows, and other edge bytes
        private static readonly string[] s_lenientEdges = [" ", "\t", "\u00a0", "\u3000", "\u2028", "\u0085", "\u2000", "\u202f", "_", "\u00e9"];

        // Letter-initial strings of each class beside the keywords
        private static readonly string[] s_lenientWords =
        [
            "name", "type", "image", "nginx", "info", "off-by", "yesterday", "FEATURE_FLAGS", "t", "x", "a", "Infinity", "NaN",
            "nAn", "iNf", "tRuE", "yEs", "NuLL", "oN", "nO", "fAlSe", "infinityy", "infinit", "trues", "nul", "on_", "_on",
        ];

        // Every case of a keyword of up to eight letters, and the signed and dotted forms
        private static IEnumerable<string> EveryCase(string keyword)
        {
            int letters = keyword.Count(char.IsLetter);
            for (int mask = 0; mask < 1 << letters; mask++)
            {
                var chars = keyword.ToCharArray();
                int bit = 0;
                for (int i = 0; i < chars.Length; i++)
                {
                    if (char.IsLetter(chars[i]))
                    {
                        chars[i] = (mask & (1 << bit++)) != 0 ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
                    }
                }

                yield return new string(chars);
            }
        }

        private static IEnumerable<byte[]> LenientCandidates()
        {
            // Every string of one to three printable ASCII bytes
            for (int a = 0x20; a < 0x7F; a++)
            {
                yield return new[] { (byte)a };
                for (int b = 0x20; b < 0x7F; b++)
                {
                    yield return new[] { (byte)a, (byte)b };
                    for (int c = 0x20; c < 0x7F; c++)
                    {
                        yield return new[] { (byte)a, (byte)b, (byte)c };
                    }
                }
            }

            // Every string of four scalars over the representatives, and of five over the smaller set
            foreach ((int length, string set) in new[] { (4, LenientRepresentatives), (5, LenientRepresentatives5) })
            {
                byte[][] reps = set.Select(ch => Encoding.UTF8.GetBytes(ch.ToString())).ToArray();
                var index = new int[length];
                while (true)
                {
                    yield return index.SelectMany(i => reps[i]).ToArray();
                    int p = length - 1;
                    while (p >= 0 && ++index[p] == reps.Length)
                    {
                        index[p--] = 0;
                    }

                    if (p < 0)
                    {
                        break;
                    }
                }
            }

            // The keywords and IEEE names in every case, and the words, each with '_' inserted at each position, an edge
            // byte appended or prepended, and a sign or a dot prepended
            string[] bases = s_keywords.Where(k => k.Count(char.IsLetter) <= 8).SelectMany(EveryCase).Concat(s_lenientWords).Distinct().ToArray();
            foreach (string word in bases)
            {
                var variants = new List<string> { word, "+" + word, "-" + word, "." + word, "+." + word, "-." + word };
                for (int i = 0; i <= word.Length; i++)
                {
                    variants.Add(word.Insert(i, "_"));
                }

                foreach (string variant in variants.ToArray())
                {
                    foreach (string edge in s_lenientEdges)
                    {
                        variants.Add(variant + edge);
                        variants.Add(edge + variant);
                        variants.Add(edge + variant + edge);
                    }
                }

                foreach (string variant in variants)
                {
                    yield return Encoding.UTF8.GetBytes(variant);
                }
            }

            // The default set's forms and witnesses, with an edge byte appended or prepended
            foreach (string form in s_forms.Concat(s_tableWitnesses))
            {
                yield return Encoding.UTF8.GetBytes(form);
                foreach (string edge in s_lenientEdges)
                {
                    yield return Encoding.UTF8.GetBytes(form + edge);
                    yield return Encoding.UTF8.GetBytes(edge + form);
                }
            }
        }

        [Fact]
        public void LenientFirstByteClasses_EqualTheMatchersTheyReplace()
        {
            var failures = new List<string>();
            var reached = new int[3];
            var quoted = new int[3];
            foreach (byte[] candidate in LenientCandidates())
            {
                ReadOnlySpan<byte> s = candidate;
                if (s.IsEmpty)
                {
                    continue;
                }

                bool old = YamlWriterHelper.QuotedForLenientReadersAllMatchers(s);
                int lenientClass = YamlWriterHelper.LenientClassOf(s[0]);
                reached[lenientClass]++;
                if (old)
                {
                    quoted[lenientClass]++;
                }

                if (YamlWriterHelper.QuotedForLenientReaders(s) != old)
                {
                    failures.Add(Escape(Encoding.UTF8.GetString(candidate)) + $" (class {lenientClass}): the option's set is {old}, the first-byte path is not");
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));

            // Each class is reached by many strings, and holds strings the set quotes
            Assert.True(reached.Sum() > 1_000_000, $"{reached.Sum()} strings checked");
            for (int c = 0; c < 3; c++)
            {
                Assert.True(reached[c] > 10_000 && quoted[c] > 100, $"class {c}: {reached[c]} strings, {quoted[c]} quoted");
            }

            foreach (string witness in new[] { "tRuE", "t_rue", "true\u00a0", "\u00a0x", "x\u3000", "Infinity", "_1", "1,000", "-nan", "+.Inf" })
            {
                Assert.True(YamlWriterHelper.QuotedForLenientReaders(Encoding.UTF8.GetBytes(witness)), Escape(witness) + " is in the option's set");
            }
        }

        [Fact]
        public void Tables_MatchTheirCharacterLists()
        {
            for (int b = 0; b < 0x80; b++)
            {
                Assert.Equal("0123456789+-.~<=tTfFnNyYoO".IndexOf((char)b) >= 0, YamlWriterHelper.PassesResolutionPrefilter((byte)b));
                Assert.Equal(ResolvableByteList.IndexOf((char)b) >= 0, YamlWriterHelper.IsResolvableByte((byte)b));
                int lenientClass = LenientKeywordLetters.IndexOf((char)b) >= 0 ? 1 : LenientWholeSetBytes.IndexOf((char)b) >= 0 ? 2 : 0;
                Assert.Equal(lenientClass, YamlWriterHelper.LenientClassOf((byte)b));

                // The indicators of 5.2, through the plain check of a one-character string followed by 'x'.
                bool indicator = "-?:,[]{}#&*!|>'\"%@`".IndexOf((char)b) >= 0;
                if (b >= 0x21 && b < 0x7F)
                {
                    bool plain = YamlWriterHelper.IsBlockPlainSyntax(new[] { (byte)b, (byte)' ', (byte)'x' });
                    Assert.Equal(!indicator, plain);
                }
            }

            for (int b = 0x80; b < 0x100; b++)
            {
                Assert.Equal(2, YamlWriterHelper.LenientClassOf((byte)b));
            }
        }
    }
}
