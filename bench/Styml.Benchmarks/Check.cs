// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The equal-work check. It runs before any timing and throws if two
// libraries did different work on a document.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SY = SharpYaml;

namespace Styml.Benchmarks
{
    internal static class Check
    {
        // One fixed System.Text.Json configuration: the model's [JsonPropertyName] names, camelCase otherwise, nulls left out.
        private static readonly JsonSerializerOptions s_canonical = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly HashSet<string> s_checked = new();

        public static string Canonical(object? graph) => graph is null ? "null" : JsonSerializer.Serialize(graph, graph.GetType(), s_canonical);

        /// <summary>Checks a document once per process. Program runs it in the host before BenchmarkDotNet starts; the measured
        /// child processes do not, so that the check's mixed workload does not warm one library's code for another row.</summary>
        public static void Document(string name, TextWriter? log = null)
        {
            lock (s_checked)
            {
                if (s_checked.Contains(name))
                {
                    return;
                }
                Doc doc = Corpus.Get(name);
                Graphs(doc, log);
                Tokens(doc, log);
                WriteCheck.Document(doc, log);
                s_checked.Add(name);
            }
        }

        private static void Graphs(Doc doc, TextWriter? log)
        {
            string expected = Canonical(Libraries.Json(doc.Json, doc.Family));
            var results = new List<(string Label, Func<object?> Read)>
            {
                ("Styml yaml reflection", () => Libraries.Styml(doc.Yaml, doc.Family, Libraries.StymlOptions)),
                ("Styml yaml reflection nokeys", () => Libraries.Styml(doc.Yaml, doc.Family, Libraries.StymlOptionsNoKeys)),
                ("Styml json reflection", () => Libraries.Styml(doc.Json, doc.Family, Libraries.StymlOptions)),
                ("Styml json reflection nokeys", () => Libraries.Styml(doc.Json, doc.Family, Libraries.StymlOptionsNoKeys)),
                ("Styml yaml generated", () => Libraries.StymlGeneratedRead(doc.Yaml, doc.Family, Libraries.StymlGenerated)),
                ("Styml yaml generated nokeys", () => Libraries.StymlGeneratedRead(doc.Yaml, doc.Family, Libraries.StymlGeneratedNoKeys)),
                ("Styml json generated", () => Libraries.StymlGeneratedRead(doc.Json, doc.Family, Libraries.StymlGenerated)),
                ("Styml json generated nokeys", () => Libraries.StymlGeneratedRead(doc.Json, doc.Family, Libraries.StymlGeneratedNoKeys)),
                ("Styml yaml from string", () => Libraries.StymlText(doc.YamlText, doc.Family, Libraries.StymlGenerated)),
                ("STJ generated", () => Libraries.JsonGeneratedRead(doc.Json, doc.Family)),
                ("STJ from string", () => Libraries.JsonGeneratedText(doc.JsonText, doc.Family)),
                ("YamlDotNet reflection", () => Libraries.YamlDotNet(doc.YamlText, doc.Family, Libraries.YamlDotNetReflection)),
                ("YamlDotNet static", () => Libraries.YamlDotNet(doc.YamlText, doc.Family, Libraries.YamlDotNetStatic)),
                ("SharpYaml reflection", () => Libraries.SharpYaml(doc.YamlText, doc.Family)),
                ("SharpYaml generated", () => Libraries.SharpYamlGeneratedRead(doc.YamlText, doc.Family)),
                ("VYaml generated", () => Libraries.VYaml(doc.Yaml, doc.Family)),
            };
            if (doc.MinJson is not null)
            {
                results.Add(("STJ minified", () => Libraries.Json(doc.MinJson, doc.Family)));
                results.Add(("Styml minified", () => Libraries.Styml(doc.MinJson, doc.Family, Libraries.StymlOptions)));
                results.Add(("Styml minified nokeys", () => Libraries.Styml(doc.MinJson, doc.Family, Libraries.StymlOptionsNoKeys)));
            }

            var failures = new StringBuilder();
            foreach ((string label, Func<object?> read) in results)
            {
                string actual;
                try
                {
                    actual = Canonical(read());
                }
                catch (Exception e)
                {
                    failures.AppendLine($"  {label}: threw {e.GetType().Name}: {FirstLine(e.Message)}");
                    continue;
                }
                if (actual != expected)
                {
                    int at = FirstDifference(expected, actual);
                    failures.AppendLine($"  {label}: graph differs at char {at}: expected …{Around(expected, at)}… got …{Around(actual, at)}…");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (deserialization):\n{failures}");
            }
            log?.WriteLine($"{doc.Name}: {results.Count} graphs equal ({expected.Length:N0} chars)");
        }

        private static void Tokens(Doc doc, TextWriter? log)
        {
            (long Scalars, long Starts) expected = StymlCounts(doc.Yaml, Libraries.StymlReader);
            var results = new List<(string Label, (long, long) Counts)>
            {
                ("Styml yaml nokeys", StymlCounts(doc.Yaml, Libraries.StymlReaderNoKeys)),
                ("Styml json", StymlCounts(doc.Json, Libraries.StymlReader)),
                ("Styml json nokeys", StymlCounts(doc.Json, Libraries.StymlReaderNoKeys)),
                ("STJ", JsonCounts(doc.Json)),
                ("YamlDotNet", YamlDotNetCounts(doc.YamlText)),
                ("SharpYaml", SharpYamlCounts(doc.YamlText)),
                ("VYaml", VYamlCounts(doc.Yaml)),
            };
            if (doc.MinJson is not null)
            {
                results.Add(("Styml minified", StymlCounts(doc.MinJson, Libraries.StymlReader)));
                results.Add(("STJ minified", JsonCounts(doc.MinJson)));
            }

            var failures = new StringBuilder();
            foreach ((string label, (long, long) counts) in results)
            {
                if (counts != expected)
                {
                    failures.AppendLine($"  {label}: {counts} (scalars, starts), Styml yaml has {expected}");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (tokenizing):\n{failures}");
            }
            log?.WriteLine($"{doc.Name}: {results.Count + 1} token streams equal ({expected.Scalars:N0} scalars, {expected.Starts:N0} collection starts)");

            // Tokenize + text: the measured methods themselves, with and without text; the difference is the summed length
            // of every scalar's text (keys included), which must be the same for every library.
            long expectedText = Libraries.StymlTokens(doc.Yaml, Libraries.StymlReader, text: true) - Libraries.StymlTokens(doc.Yaml, Libraries.StymlReader, text: false);
            var texts = new List<(string Label, long Length)>
            {
                ("Styml yaml nokeys", Libraries.StymlTokens(doc.Yaml, Libraries.StymlReaderNoKeys, text: true) - Libraries.StymlTokens(doc.Yaml, Libraries.StymlReaderNoKeys, text: false)),
                ("Styml json", Libraries.StymlTokens(doc.Json, Libraries.StymlReader, text: true) - Libraries.StymlTokens(doc.Json, Libraries.StymlReader, text: false)),
                ("Styml json nokeys", Libraries.StymlTokens(doc.Json, Libraries.StymlReaderNoKeys, text: true) - Libraries.StymlTokens(doc.Json, Libraries.StymlReaderNoKeys, text: false)),
                ("STJ", Libraries.JsonTokens(doc.Json, text: true) - Libraries.JsonTokens(doc.Json, text: false)),
                ("VYaml", Libraries.VYamlEvents(doc.Yaml, text: true) - Libraries.VYamlEvents(doc.Yaml, text: false)),
            };
            if (doc.MinJson is not null)
            {
                texts.Add(("Styml minified", Libraries.StymlTokens(doc.MinJson, Libraries.StymlReader, text: true) - Libraries.StymlTokens(doc.MinJson, Libraries.StymlReader, text: false)));
                texts.Add(("STJ minified", Libraries.JsonTokens(doc.MinJson, text: true) - Libraries.JsonTokens(doc.MinJson, text: false)));
            }
            foreach ((string label, long length) in texts)
            {
                if (length != expectedText)
                {
                    failures.AppendLine($"  {label}: scalar text {length:N0} chars, Styml yaml has {expectedText:N0}");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (tokenize + text):\n{failures}");
            }
            log?.WriteLine($"{doc.Name}: {texts.Count + 1} scalar texts equal ({expectedText:N0} chars)");
        }

        private static (long, long) StymlCounts(ReadOnlySpan<byte> utf8, System.Text.Yaml.YamlReaderOptions options)
        {
            var reader = new System.Text.Yaml.Utf8YamlReader(utf8, options);
            long scalars = 0, starts = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case System.Text.Yaml.YamlTokenType.StartObject:
                    case System.Text.Yaml.YamlTokenType.StartArray:
                        starts++;
                        break;
                    case System.Text.Yaml.YamlTokenType.PropertyName:
                    case System.Text.Yaml.YamlTokenType.String:
                    case System.Text.Yaml.YamlTokenType.Number:
                    case System.Text.Yaml.YamlTokenType.True:
                    case System.Text.Yaml.YamlTokenType.False:
                    case System.Text.Yaml.YamlTokenType.Null:
                        scalars++;
                        break;
                }
            }
            return (scalars, starts);
        }

        private static (long, long) JsonCounts(ReadOnlySpan<byte> utf8)
        {
            var reader = new Utf8JsonReader(utf8);
            long scalars = 0, starts = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        starts++;
                        break;
                    case JsonTokenType.PropertyName:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        scalars++;
                        break;
                }
            }
            return (scalars, starts);
        }

        private static (long, long) YamlDotNetCounts(string text)
        {
            var parser = new YamlDotNet.Core.Parser(new StringReader(text));
            long scalars = 0, starts = 0;
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case YamlDotNet.Core.Events.Scalar:
                        scalars++;
                        break;
                    case YamlDotNet.Core.Events.MappingStart:
                    case YamlDotNet.Core.Events.SequenceStart:
                        starts++;
                        break;
                }
            }
            return (scalars, starts);
        }

        private static (long, long) SharpYamlCounts(string text)
        {
            SY.IParser parser = SY.Parser.CreateParser(new StringReader(text));
            long scalars = 0, starts = 0;
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case SY.Events.Scalar:
                        scalars++;
                        break;
                    case SY.Events.MappingStart:
                    case SY.Events.SequenceStart:
                        starts++;
                        break;
                }
            }
            return (scalars, starts);
        }

        private static (long, long) VYamlCounts(byte[] utf8)
        {
            var parser = VYaml.Parser.YamlParser.FromBytes(utf8);
            long scalars = 0, starts = 0;
            while (parser.Read())
            {
                switch (parser.CurrentEventType)
                {
                    case VYaml.Parser.ParseEventType.Scalar:
                        scalars++;
                        break;
                    case VYaml.Parser.ParseEventType.MappingStart:
                    case VYaml.Parser.ParseEventType.SequenceStart:
                        starts++;
                        break;
                }
            }
            return (scalars, starts);
        }

        private static int FirstDifference(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                {
                    return i;
                }
            }
            return n;
        }

        private static string Around(string s, int at)
        {
            int start = Math.Max(0, at - 60);
            return s.Substring(start, Math.Min(s.Length, at + 60) - start);
        }

        private static string FirstLine(string s)
        {
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s[..nl];
        }
    }
}
