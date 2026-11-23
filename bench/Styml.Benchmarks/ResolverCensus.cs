// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4p candidate d: which of a document's strings reach the resolver of
// the block writer, and what a table of the bytes the resolvable forms can contain would do with them. The writer's
// internal matchers are reached through delegates, so the mode runs over any Styml.dll that has them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Styml.Benchmarks
{
    public static class ResolverCensus
    {
        private delegate bool SpanTest(ReadOnlySpan<byte> s);

        private delegate bool SpanTestLenient(ReadOnlySpan<byte> s, bool lenient);

        // The bytes a string whose first byte is not a letter can hold and still be read as a non-string by the YAML
        // 1.2 core schema, the YAML 1.1 types, or the keyword and legacy-form matcher (the patterns of the matchers):
        // digits, signs, '.', '_', ':' (sexagesimal, time), hex digits, 'o' and 'x' (prefixes), 'i', 'n', 'I', 'N' (.inf,
        // .nan with the hex letters), 'T', 't', 'Z', space and tab (timestamps), and '~', '<', '=' (null, merge, value).
        internal const string ResolvableBytes = "0123456789+-._:abcdefABCDEFoxinINTtZ \t~<=";

        public static int Run(TextWriter output)
        {
            Assembly styml = typeof(System.Text.Yaml.YamlSerializer).Assembly;
            Type writer = styml.GetType("System.Text.Yaml.YamlWriterHelper", throwOnError: true)!;
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            var plainSyntax = (SpanTest)writer.GetMethod("IsBlockPlainSyntax", Flags)!.CreateDelegate(typeof(SpanTest));
            var resolves = (SpanTestLenient)writer.GetMethod("ResolvesToNonString", Flags)!.CreateDelegate(typeof(SpanTestLenient));
            var lenientSet = (SpanTest)writer.GetMethod("QuotedForLenientReaders", Flags)!.CreateDelegate(typeof(SpanTest));

            output.WriteLine("Strings that pass the block plain check and reach the resolver's non-letter path (first byte a digit, sign, '.', '~', '<' or '='),");
            output.WriteLine("by occurrence; ruled out = holds a byte outside the table; passes: resolves (quoted) or not (the whole chain today).");
            output.WriteLine($"{"document",-11} {"strings",8} {"plain",8} {"reach",7} {"ruled out",10} {"resolves",9} {"chain",7}  chain strings (top)");
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                var strings = new List<string>();
                Walk(JsonDocument.Parse(doc.Json).RootElement, strings);
                int plain = 0, reach = 0, ruledOut = 0, resolving = 0, chain = 0;
                var chainStrings = new Dictionary<string, int>();
                foreach (string s in strings)
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(s);
                    if (utf8.Length == 0 || !plainSyntax(utf8))
                    {
                        continue;
                    }

                    plain++;
                    byte first = utf8[0];
                    if (first >= 0x80 || "0123456789+-.~<=".IndexOf((char)first) < 0)
                    {
                        continue;
                    }

                    reach++;
                    bool table = utf8.All(b => b < 0x80 && ResolvableBytes.IndexOf((char)b) >= 0);
                    bool nonString = resolves(utf8, false);
                    if (nonString && !table)
                    {
                        throw new InvalidOperationException("the table rules out a string that resolves: " + s);
                    }

                    if (!table)
                    {
                        ruledOut++;
                    }
                    else if (nonString)
                    {
                        resolving++;
                    }
                    else
                    {
                        chain++;
                        chainStrings[s] = chainStrings.TryGetValue(s, out int n) ? n + 1 : 1;
                    }
                }

                string top = string.Join(", ", chainStrings.OrderByDescending(p => p.Value).Take(6).Select(p => $"'{p.Key}' {p.Value}"));
                output.WriteLine($"{name,-11} {strings.Count,8} {plain,8} {reach,7} {ruledOut,10} {resolving,9} {chain,7}  {top}");
            }

            output.WriteLine();
            Lenient(output, plainSyntax, resolves, lenientSet);
            return 0;
        }

        // M4p candidate e: the strings that reach the option's set
        // (QuoteForLenientReaders on: plain by syntax, not resolved by the default check), by the first-byte class the
        // prefilter would give them. Class 0, a first byte no form of three of the four matchers can start with: only the
        // edge test runs. Class 1, a letter that starts a keyword (y n o t f i in either case): with no '_' and no space or
        // tab at the end, the edge test and the eight-letter keyword match run. Class 2 and the rest of class 1: the
        // whole set, as today.
        internal const string KeywordLetters = "yYnNoOtTfFiI";

        internal const string FullChainBytes = "0123456789+-.,_<= \t";

        private static void Lenient(TextWriter output, SpanTest plainSyntax, SpanTestLenient resolves, SpanTest lenientSet)
        {
            output.WriteLine("Strings that reach the option's set (plain by syntax, not resolved by the default check), by the prefilter's class:");
            output.WriteLine("edge only = class 0; keyword fast = class 1 without '_' and without a space or tab at the end; full = class 2 and the rest of class 1;");
            output.WriteLine("quoted = the option's set is true. Checked: on every string, the reduced test the class gives equals the set.");
            output.WriteLine($"{"document",-11} {"reach",8} {"edge only",10} {"kw fast",8} {"full",7} {"quoted",7}  strings of the full class (top)");
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                var strings = new List<string>();
                Walk(JsonDocument.Parse(doc.Json).RootElement, strings);
                int reach = 0, edgeOnly = 0, fast = 0, full = 0, quoted = 0;
                var fullFirst = new Dictionary<string, int>();
                foreach (string s in strings)
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(s);
                    if (utf8.Length == 0 || !plainSyntax(utf8) || resolves(utf8, false))
                    {
                        continue;
                    }

                    reach++;
                    bool set = lenientSet(utf8);
                    if (set)
                    {
                        quoted++;
                    }

                    byte first = utf8[0];
                    byte last = utf8[utf8.Length - 1];
                    bool edge = HasUnicodeWhiteSpaceAtAnEdge(s);
                    if (first < 0x80 && KeywordLetters.IndexOf((char)first) < 0 && FullChainBytes.IndexOf((char)first) < 0)
                    {
                        edgeOnly++;
                        Check(set, edge, s);
                    }
                    else if (first < 0x80 && KeywordLetters.IndexOf((char)first) >= 0 && Array.IndexOf(utf8, (byte)'_') < 0
                        && last is not ((byte)' ' or (byte)'\t'))
                    {
                        fast++;
                        Check(set, edge || (utf8.Length <= 8 && IsLetterKeyword(s)), s);
                    }
                    else
                    {
                        full++;
                        string key = s.Length > 24 ? s.Substring(0, 24) + "..." : s;
                        fullFirst[key] = fullFirst.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }

                string top = string.Join(", ", fullFirst.OrderByDescending(p => p.Value).Take(8).Select(p => $"'{p.Key}' {p.Value}"));
                output.WriteLine($"{name,-11} {reach,8} {edgeOnly,10} {fast,8} {full,7} {quoted,7}  {top}");
            }
        }

        private static void Check(bool set, bool reduced, string s)
        {
            if (set != reduced)
            {
                throw new InvalidOperationException("the reduced test differs from the option's set on: " + s);
            }
        }

        // The census's own forms of the edge test and the letter keywords, from the matchers' documentation: .NET's
        // char.IsWhiteSpace above ASCII at the first or last scalar, and y n no on yes off nan inf true null false infinity
        // in any case.
        private static bool HasUnicodeWhiteSpaceAtAnEdge(string s) =>
            (s[0] >= 0x80 && char.IsWhiteSpace(s[0])) || (s[s.Length - 1] >= 0x80 && char.IsWhiteSpace(s[s.Length - 1]));

        private static readonly HashSet<string> s_letterKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "y", "n", "no", "on", "yes", "off", "nan", "inf", "true", "null", "false", "infinity",
        };

        private static bool IsLetterKeyword(string s) => s_letterKeywords.Contains(s);

        private static void Walk(JsonElement e, List<string> strings)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty p in e.EnumerateObject())
                    {
                        strings.Add(p.Name);
                        Walk(p.Value, strings);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in e.EnumerateArray())
                    {
                        Walk(item, strings);
                    }

                    break;
                case JsonValueKind.String:
                    strings.Add(e.GetString()!);
                    break;
            }
        }
    }
}
