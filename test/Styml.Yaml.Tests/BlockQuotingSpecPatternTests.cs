// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// The default quoting set of block style (bounded to what the standards
    /// resolve) against the YAML 1.2 core schema and the YAML 1.1 type patterns, compiled as
    /// printed. Over every short string of an alphabet of the patterns' characters, every case of the keywords, and
    /// generated timestamps and near misses, in five positions (block item, block value, block key, flow item, flow
    /// key), written by the writer itself:
    /// <list type="bullet">
    /// <item>nothing a pattern matches is written plain;</item>
    /// <item>what is quoted with no pattern matching and no syntactic reason is the pinned list
    /// (<c>BlockQuotingSpecPatternTests.overquoted.txt</c>).</item>
    /// </list>
    /// </summary>
    public class BlockQuotingSpecPatternTests
    {
        // YAML 1.2.2, section 10.3.2 (the core schema), as printed.
        private static readonly (string Name, string Pattern)[] s_patterns =
        [
            ("1.2 null", "null|Null|NULL|~|"), // the last alternative is the empty string
            ("1.2 bool", "true|True|TRUE|false|False|FALSE"),
            ("1.2 int", "[-+]?[0-9]+"),
            ("1.2 int (base 8)", "0o[0-7]+"),
            ("1.2 int (base 16)", "0x[0-9a-fA-F]+"),
            ("1.2 float", @"[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?"),
            ("1.2 float (infinity)", @"[-+]?\.(inf|Inf|INF)"),
            ("1.2 float (not a number)", @"\.nan|\.NaN|\.NAN"),

            // The YAML 1.1 types (yaml.org/type/bool, null, int, float, merge, value, timestamp), as printed.
            ("1.1 bool", "y|Y|yes|Yes|YES|n|N|no|No|NO|true|True|TRUE|false|False|FALSE|on|On|ON|off|Off|OFF"),
            ("1.1 null", "~|null|Null|NULL|"),
            ("1.1 int (base 2)", "[-+]?0b[0-1_]+"),
            ("1.1 int (base 8)", "[-+]?0[0-7_]+"),
            ("1.1 int (base 10)", "[-+]?(0|[1-9][0-9_]*)"),
            ("1.1 int (base 16)", "[-+]?0x[0-9a-fA-F_]+"),
            ("1.1 int (base 60)", "[-+]?[1-9][0-9_]*(:[0-5]?[0-9])+"),

            // Printed as [-+]?([0-9][0-9_]*)?\.[0-9.]*([eE][-+][0-9]+)?. Read here as the rule
            // has it: [0-9.]* as [0-9]*, with a digit before the exponent, so
            // 1.2.3, '.', 1.., .1. and .e+0 stay plain. (PyYAML also takes underscores in the fraction, and Psych fails
            // on .e+0; QuoteForLenientReaders quotes both.)
            ("1.1 float (base 10)", @"[-+]?([0-9][0-9_]*\.[0-9]*|\.[0-9]+)([eE][-+][0-9]+)?"),
            ("1.1 float (base 60)", @"[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+\.[0-9_]*"),
            ("1.1 float (infinity)", @"[-+]?\.(inf|Inf|INF)"),
            ("1.1 float (not a number)", @"\.(nan|NaN|NAN)"),
            ("1.1 merge", "<<"),
            ("1.1 value", "="),
            ("1.1 timestamp",
                @"[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]"
                + @"|[0-9][0-9][0-9][0-9]-[0-9][0-9]?-[0-9][0-9]?([Tt]|[ \t]+)[0-9][0-9]?:[0-9][0-9]:[0-9][0-9](\.[0-9]*)?(([ \t]*)Z|[-+][0-9][0-9]?(:[0-9][0-9])?)?"),
        ];

        private static readonly Regex[] s_regexes =
            s_patterns.Select(p => new Regex(@"\A(?:" + p.Pattern + @")\z", RegexOptions.CultureInvariant | RegexOptions.Compiled)).ToArray();

        // The characters of the patterns: digits of each class (0; 1, binary and octal; 8, decimal only and above 5), the
        // signs, '.', '_', ':', the prefixes and exponents, and the one-character and two-character forms.
        private const string Alphabet = "018+-._:xboeE~<=";

        private enum Position { Item, Value, Key, FlowItem, FlowKey }

        // 1,118,480 strings.
        [Fact]
        public void ShortStrings_UpToFiveCharacters() => Check(Enumerate(5));

        [Fact]
        public void Keywords_InEveryCase() => Check(Keywords());

        [Fact]
        public void Timestamps_AndNearMisses() => Check(Timestamps());

        // Psych 3.1's ScalarScanner::TIME as printed in Ruby 2.6's psych/scalar_scanner.rb, with Ruby's \s (ASCII white
        // space) less the line feed, which makes a string a string before the pattern is tried.
        private static readonly Regex s_psychTime = new(
            @"\A-?[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}(?:[Tt]|[ \t\r\f\v]+)[0-9]{1,2}:[0-9][0-9]:[0-9][0-9](?:\.[0-9]*)?(?:[ \t\r\f\v]*(?:Z|[-+][0-9]{1,2}:?(?:[0-9][0-9])?))?\z",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// QuoteForLenientReaders and Psych's timestamps (for a reader that resolves beyond the standards):
        /// <see cref="YamlWriterHelper.IsPsychTimestamp"/> is the pattern exactly, over the timestamp
        /// generator with and without a leading '-' and Psych's extra zones, and with the option every match is quoted
        /// in every position. Without the option nothing changes (<see cref="Timestamps_AndNearMisses"/>).
        /// </summary>
        [Fact]
        public void PsychTimestamps_QuotedWithTheOption()
        {
            string[] zones = ["+0500", "-530", "+050", "+5:", "+05:", "-05:0", "+05:000", "+05000", " +0500", "\t-5:", "+", " ", "Z ", "\v+05", "\f-05:00"];
            var strings = new List<string>();
            foreach (string t in Timestamps())
            {
                strings.Add(t);
                strings.Add("-" + t);
                strings.Add("+" + t);
            }

            foreach (string z in zones)
            {
                strings.Add("2001-12-14 21:59:43" + z);
                strings.Add("-2001-1-4t1:59:43.5" + z);
                strings.Add("2001-12-14\r21:59:43" + z);
            }

            bool[] expected = strings.Select(s => s_psychTime.IsMatch(s)).ToArray();
            var failures = new List<string>();
            for (int i = 0; i < strings.Count; i++)
            {
                if (YamlWriterHelper.IsPsychTimestamp(Encoding.UTF8.GetBytes(strings[i])) != expected[i])
                {
                    failures.Add($"IsPsychTimestamp({Escape(strings[i])}) is not {expected[i]}");
                }
            }

            Assert.True(expected.Count(m => m) >= 3_000, $"the generator reaches the pattern: {expected.Count(m => m)} of {strings.Count}");
            Assert.Contains("-2001-12-14 21:59:43", strings.Where((_, i) => expected[i]));
            Assert.Contains("2001-12-14 21:59:43+0500", strings.Where((_, i) => expected[i]));
            foreach (Position position in Enum.GetValues(typeof(Position)))
            {
                bool[] quoted = WriteAll(strings, position, lenient: true);
                for (int i = 0; i < strings.Count; i++)
                {
                    if (expected[i] && !quoted[i])
                    {
                        failures.Add($"{position}: {Escape(strings[i])} matches Psych's TIME and is written plain with the option");
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)) + $"\n({failures.Count} in all)");
        }

        private static List<string> Enumerate(int maxLength)
        {
            var strings = new List<string>();
            var current = new List<string> { "" };
            for (int length = 1; length <= maxLength; length++)
            {
                var next = new List<string>(current.Count * Alphabet.Length);
                foreach (string prefix in current)
                {
                    foreach (char c in Alphabet)
                    {
                        next.Add(prefix + c);
                    }
                }

                strings.AddRange(next);
                current = next;
            }

            return strings;
        }

        private static List<string> Keywords()
        {
            var strings = new HashSet<string>();
            string[] words = ["y", "n", "yes", "no", "on", "off", "true", "false", "null", "inf", "nan", "infinity", "ye", "nul", "tru", "of", "yess", "nulll"];
            foreach (string word in words)
            {
                foreach (string cased in Cases(word))
                {
                    foreach (string prefix in new[] { "", ".", "+.", "-.", "+", "-", "~" })
                    {
                        strings.Add(prefix + cased);
                    }
                }
            }

            strings.Add("");
            strings.Add("~");
            strings.Add("~~");
            strings.Add("<<");
            strings.Add("<<<");
            strings.Add("<");
            strings.Add("==");
            return strings.ToList();
        }

        private static IEnumerable<string> Cases(string word)
        {
            for (int mask = 0; mask < 1 << word.Length; mask++)
            {
                char[] chars = word.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        chars[i] = char.ToUpperInvariant(chars[i]);
                    }
                }

                yield return new string(chars);
            }
        }

        // Each part of the timestamp pattern in a form that fits it and forms that do not.
        private static List<string> Timestamps()
        {
            string[] years = ["2001", "200", "20011"];
            string[] months = ["12", "1", "123", ""];
            string[] days = ["14", "4", "141"];
            string[] separators = ["T", "t", " ", "  ", "\t", " \t", "", "x"];
            string[] hours = ["21", "1", "213"];
            string[] minutes = ["59", "5"];
            string[] seconds = ["43", "4", "433"];
            string[] fractions = ["", ".", ".10", ".1x"];
            string[] zones = ["", "Z", " Z", "\tZ", "z", "+5", "-05", "+05:00", "-5:00", "+0500", " -5", "+5:0", "+123", "+"];
            var strings = new List<string>();
            foreach (string y in years)
            {
                foreach (string m in months)
                {
                    foreach (string d in days)
                    {
                        string date = y + "-" + m + "-" + d;
                        strings.Add(date);
                        strings.Add("-" + date);
                        strings.Add(y + "/" + m + "/" + d);
                        foreach (string sep in separators)
                        {
                            foreach (string h in hours)
                            {
                                foreach (string mi in minutes)
                                {
                                    foreach (string s in seconds)
                                    {
                                        foreach (string f in fractions)
                                        {
                                            foreach (string z in zones)
                                            {
                                                strings.Add(date + sep + h + ":" + mi + ":" + s + f + z);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return strings;
        }

        private static string? Match(string s)
        {
            for (int i = 0; i < s_regexes.Length; i++)
            {
                if (s_regexes[i].IsMatch(s))
                {
                    return s_patterns[i].Name;
                }
            }

            return null;
        }

        // A syntactic reason to quote in a position: the block plain check, and in flow the flow
        // indicators.
        private static bool SyntaxRequiresQuotes(string s, Position position)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s);
            if (!YamlWriterHelper.IsBlockPlainSyntax(utf8))
            {
                return true;
            }

            return position is Position.FlowItem or Position.FlowKey && !YamlWriterHelper.IsFlowPlain(utf8);
        }

        private static void Check(List<string> strings)
        {
            string?[] matches = strings.Select(Match).ToArray();
            var failures = new List<string>();
            SortedSet<string>? overQuoted = null;
            foreach (Position position in Enum.GetValues(typeof(Position)))
            {
                bool[] quoted = WriteAll(strings, position);
                var over = new SortedSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < strings.Count; i++)
                {
                    if (matches[i] is string name && !quoted[i])
                    {
                        failures.Add($"{position}: {Escape(strings[i])} matches {name} and is written plain");
                    }
                    else if (matches[i] is null && quoted[i] && !SyntaxRequiresQuotes(strings[i], position))
                    {
                        over.Add(strings[i]);
                    }
                }

                if (overQuoted is null)
                {
                    overQuoted = over;
                }
                else if (!overQuoted.SetEquals(over))
                {
                    failures.Add($"{position}: quotes without a pattern a different set than {Position.Item}: "
                        + string.Join(", ", over.Except(overQuoted).Concat(overQuoted.Except(over)).Take(10).Select(Escape)));
                }
            }

            Assert.True(failures.Count == 0, $"{failures.Count} failures:\n" + string.Join("\n", failures.Take(40)));

            // The other direction: what is quoted without a pattern and without a syntactic reason is pinned. To regenerate
            // the list, set STYML_PIN_OVERQUOTED to a file path: the sets found are appended to it.
            if (Environment.GetEnvironmentVariable("STYML_PIN_OVERQUOTED") is { Length: > 0 } pinPath)
            {
                File.AppendAllLines(pinPath, overQuoted!.Select(Escape));
            }

            var expected = new HashSet<string>(PinnedOverQuoted(), StringComparer.Ordinal);
            var inScope = new HashSet<string>(strings, StringComparer.Ordinal);
            string[] unexpected = overQuoted!.Where(s => !expected.Contains(s)).ToArray();
            string[] missing = expected.Where(s => inScope.Contains(s) && !overQuoted!.Contains(s)).ToArray();
            Assert.True(unexpected.Length == 0 && missing.Length == 0,
                $"Over-quoted but not pinned ({unexpected.Length}): {string.Join(", ", unexpected.Take(40).Select(Escape))}\n"
                + $"Pinned but not over-quoted ({missing.Length}): {string.Join(", ", missing.Take(40).Select(Escape))}");
        }

        private static IEnumerable<string> PinnedOverQuoted()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "BlockQuotingSpecPatternTests.overquoted.txt");
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length > 0 && line[0] != '#')
                {
                    yield return System.Text.Json.JsonSerializer.Deserialize<string>(line)!;
                }
            }
        }

        // Writes every string in one document per position, one per line, and reads back whether each was quoted.
        private static bool[] WriteAll(List<string> strings, Position position, bool lenient = false)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var w = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = lenient }))
            {
                if (position == Position.Key)
                {
                    w.WriteStartObject();
                }
                else
                {
                    w.WriteStartArray();
                }

                foreach (string s in strings)
                {
                    switch (position)
                    {
                        case Position.Item:
                            w.WriteStringValue(s);
                            break;
                        case Position.Value:
                            w.WriteStartObject();
                            w.WriteString("k", s);
                            w.WriteEndObject();
                            break;
                        case Position.Key:
                            w.WritePropertyName(s);
                            w.WriteNumberValue(0);
                            break;
                        case Position.FlowItem:
                            w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                            w.WriteStartArray();
                            w.WriteStringValue(s);
                            w.WriteEndArray();
                            break;
                        default:
                            w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                            w.WriteStartObject();
                            w.WritePropertyName(s);
                            w.WriteNumberValue(0);
                            w.WriteEndObject();
                            break;
                    }
                }

                if (position == Position.Key)
                {
                    w.WriteEndObject();
                }
                else
                {
                    w.WriteEndArray();
                }
            }

            int offset = position switch
            {
                Position.Item => 2, // "- "
                Position.Value => 5, // "- k: "
                Position.Key => 0,
                _ => 3, // "- [" or "- {"
            };
            string[] lines = Encoding.UTF8.GetString(buffer.WrittenSpan).Split('\n');
            Assert.Equal(strings.Count + 1, lines.Length);
            var quoted = new bool[strings.Count];
            for (int i = 0; i < strings.Count; i++)
            {
                char c = lines[i][offset];
                quoted[i] = c is '"' or '\'';
            }

            return quoted;
        }

        private static string Escape(string s) => System.Text.Json.JsonSerializer.Serialize(s);
    }
}
