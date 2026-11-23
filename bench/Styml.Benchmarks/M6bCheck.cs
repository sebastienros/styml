// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M6b's equal-work checks. They run in the host after M6a's, before any
// timing, and throw on the first document where two rows did different work.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Styml.Benchmarks.Model;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    internal static class M6bCheck
    {
        private static readonly YamlDotNet.Serialization.IDeserializer s_yamlDotNet = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        private static readonly PropertyInfo s_canUseSerializeHandler = typeof(System.Text.Yaml.Serialization.Metadata.YamlTypeInfo)
            .GetProperty("CanUseSerializeHandler", BindingFlags.NonPublic | BindingFlags.Instance)!;

        public static void Run(TextWriter log)
        {
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                Serialize(doc, log);
                DomCountsOf(doc, log);
            }
            foreach (string name in SerializeToString.StringDocuments)
            {
                Strings(Corpus.Get(name), log);
            }
            foreach (string name in Corpus.AnchorNames)
            {
                Anchors(Corpus.Get(name), log);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Every row's output, read back by Styml and by the library that wrote it, gives the input graph.

        private delegate object? Reader(byte[] output);

        private static void Serialize(Doc doc, TextWriter log)
        {
            string expected = Check.Canonical(Libraries.Json(doc.Json, doc.Family));
            var reflection = new SerializeYamlReflection { Document = doc.Name };
            reflection.Setup();
            var generated = new SerializeYamlSourceGen { Document = doc.Name };
            generated.Setup();

            Reader styml = output => Libraries.Styml(output, doc.Family, Libraries.StymlOptions);
            Reader yamlDotNet = output => Libraries.YamlDotNet(Encoding.UTF8.GetString(output), doc.Family, s_yamlDotNet);
            Reader sharpYaml = output => Libraries.SharpYaml(Encoding.UTF8.GetString(output), doc.Family);
            Reader vyaml = output => Libraries.VYaml(output, doc.Family);
            Reader stj = output => Libraries.Json(output, doc.Family);

            byte[] reflectionBlock = (byte[])reflection.Styml_Block();
            reflection.Styml_Block_Writer();
            byte[] reflectionWriter = reflection.Written;
            byte[] generatedBlock = (byte[])generated.Styml_Block();
            generated.Styml_Block_Writer();
            byte[] generatedWriter = generated.Written;

            var rows = new List<(string Label, byte[] Output, Reader[] Readers)>
            {
                ("reflection Styml_Block", reflectionBlock, new[] { styml }),
                ("reflection Styml_Block_Writer", reflectionWriter, new[] { styml }),
                ("reflection Styml_Json", (byte[])reflection.Styml_Json(), new[] { styml, stj }),
                ("reflection YamlDotNet", Encoding.UTF8.GetBytes((string)reflection.YamlDotNet()), new[] { styml, yamlDotNet }),
                ("reflection SharpYaml", RunChars(() => reflection.SharpYaml(), reflection), new[] { styml, sharpYaml }),
                ("reflection Stj", (byte[])reflection.Stj(), new[] { styml, stj }),
                ("generated Styml_Block", generatedBlock, new[] { styml }),
                ("generated Styml_Block_Writer", generatedWriter, new[] { styml }),
                ("generated Styml_Json", (byte[])generated.Styml_Json(), new[] { styml, stj }),
                ("generated YamlDotNet", Encoding.UTF8.GetBytes((string)generated.YamlDotNet()), new[] { styml, yamlDotNet }),
                ("generated SharpYaml", RunChars(() => generated.SharpYaml(), generated), new[] { styml, sharpYaml }),
                ("generated VYaml", Run(() => generated.VYaml(), generated), new[] { styml, vyaml }),
                ("generated Stj", (byte[])generated.Stj(), new[] { styml, stj }),
            };

            var failures = new StringBuilder();
            int reads = 0;
            foreach ((string label, byte[] output, Reader[] readers) in rows)
            {
                for (int r = 0; r < readers.Length; r++)
                {
                    string reader = r == 0 ? "Styml" : "its library";
                    reads++;
                    if (ReadBack(doc, label + " read by " + reader, output, readers[r], expected, allowVYaml: label.EndsWith("VYaml", StringComparison.Ordinal), log) is { } failure)
                    {
                        failures.AppendLine(failure);
                    }
                }
            }

            // The block rows write block style, the JSON-compatible rows JSON.
            foreach ((string label, byte[] output, _) in rows.Where(r => r.Label.Contains("Styml_Block", StringComparison.Ordinal)))
            {
                if (IsJson(output))
                {
                    failures.AppendLine($"  {label}: writes JSON, not block style");
                }
            }
            foreach ((string label, byte[] output, _) in rows.Where(r => r.Label.EndsWith("Styml_Json", StringComparison.Ordinal)))
            {
                if (!IsJson(output))
                {
                    failures.AppendLine($"  {label}: does not write JSON");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (M6b serialize):\n{failures}");
            }

            object graph = Libraries.Json(doc.Json, doc.Family)!;
            log.WriteLine($"{doc.Name}: {rows.Count} M6b serialize outputs, {reads} read-backs equal; block rows block, JSON rows JSON; " +
                $"writer row {(reflectionWriter.AsSpan().SequenceEqual(reflectionBlock) ? "=" : "!=")} block bytes (reflection), " +
                $"{(generatedWriter.AsSpan().SequenceEqual(generatedBlock) ? "=" : "!=")} (generated)");
            log.WriteLine($"{doc.Name}: generated Styml_Block {PathFor(graph, doc.Family, BlockLibraries.BlockGenerated, YamlWriteLibraries.BlockWriterOptions)}; " +
                $"generated Styml_Json {PathFor(graph, doc.Family, YamlWriteLibraries.JsonGenerated, new System.Text.Yaml.YamlWriterOptions { Style = System.Text.Yaml.YamlWriterStyle.JsonCompatible })}");
            log.WriteLine($"{doc.Name}: output bytes " + string.Join(", ", rows.Where(r => r.Label.StartsWith("generated", StringComparison.Ordinal)).Select(r => $"{r.Label["generated ".Length..]} {r.Output.Length:N0}")));
        }

        private static byte[] Run(Func<long> row, YamlWriteScenario scenario)
        {
            row();
            return scenario.Written;
        }

        private static byte[] RunChars(Func<long> row, YamlWriteScenario scenario)
        {
            row();
            return scenario.WrittenChars;
        }

        private static string? ReadBack(Doc doc, string label, byte[] output, Reader reader, string expected, bool allowVYaml, TextWriter log)
        {
            string actual;
            try
            {
                actual = Check.Canonical(reader(output));
            }
            catch (Exception e)
            {
                return $"  {label}: threw {e.GetType().Name}: {e.Message.Split('\n')[0]}";
            }
            // VYaml 1.6.1's plain scalar for a string that ends in a line break (WriteCheck's allowance, M4).
            if (actual != expected && allowVYaml && actual.Replace("\\n\"", "\"") == expected.Replace("\\n\"", "\""))
            {
                log.WriteLine($"{doc.Name}: {label}: the final line break of some strings dropped (VYaml's plain scalars)");
                return null;
            }
            if (actual != expected)
            {
                int at = 0;
                while (at < Math.Min(actual.Length, expected.Length) && actual[at] == expected[at])
                {
                    at++;
                }
                int from = Math.Max(0, at - 60);
                return $"  {label}: a different graph at char {at}: expected …{expected[from..Math.Min(expected.Length, at + 60)]}… got …{actual[from..Math.Min(actual.Length, at + 60)]}…";
            }
            return null;
        }

        private static bool IsJson(byte[] output)
        {
            try
            {
                using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(output, Dom.JsonOptions);
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        // The source-generated row's path: the generated fast path (SerializeHandler) or the metadata path, and whether
        // the handler alone writes the serializer's bytes.
        private static string PathFor(object graph, Family family, StymlWriteContext context, System.Text.Yaml.YamlWriterOptions writerOptions) => family switch
        {
            Family.K8s => PathOf(context.K8sList, (K8sList)graph, writerOptions),
            Family.Compose => PathOf(context.ComposeFile, (ComposeFile)graph, writerOptions),
            Family.Workflow => PathOf(context.Workflow, (Workflow)graph, writerOptions),
            Family.OpenApi => PathOf(context.OpenApiDocument, (OpenApiDocument)graph, writerOptions),
            _ => PathOf(context.ListRecord, (List<Record>)graph, writerOptions),
        };

        private static string PathOf<T>(System.Text.Yaml.Serialization.Metadata.YamlTypeInfo<T> info, T value, System.Text.Yaml.YamlWriterOptions writerOptions)
        {
            byte[] serialized = YS.SerializeToUtf8Bytes(value, info);
            bool fast = (bool)s_canUseSerializeHandler.GetValue(info)!;
            string handler = "no handler";
            if (info.SerializeHandler is { } serialize)
            {
                var buffer = new ArrayBufferWriter<byte>();
                using (var writer = new System.Text.Yaml.Utf8YamlWriter(buffer, writerOptions))
                {
                    serialize(writer, value);
                }
                handler = buffer.WrittenSpan.SequenceEqual(serialized) ? "handler bytes = serializer bytes" : "handler bytes != serializer bytes";
            }
            return (fast ? "fast path" : "metadata path") + " (" + handler + ")";
        }

        // -------------------------------------------------------------------------------------------------------------
        // The string rows write the bytes of the byte rows, as UTF-16.

        private static void Strings(Doc doc, TextWriter log)
        {
            var strings = new SerializeToString { Document = doc.Name };
            strings.Setup();
            var generated = new SerializeYamlSourceGen { Document = doc.Name };
            generated.Setup();
            var pairs = new List<(string Label, string Text, byte[] Bytes)>
            {
                ("Styml_Block", strings.Styml_Block(), (byte[])generated.Styml_Block()),
                ("YamlDotNet", strings.YamlDotNet(), Encoding.UTF8.GetBytes((string)generated.YamlDotNet())),
                ("SharpYaml", strings.SharpYaml(), RunChars(() => generated.SharpYaml(), generated)),
                ("VYaml", strings.VYaml(), Run(() => generated.VYaml(), generated)),
                ("Stj", strings.Stj(), (byte[])generated.Stj()),
            };
            var failures = new StringBuilder();
            foreach ((string label, string text, byte[] bytes) in pairs)
            {
                if (text != Encoding.UTF8.GetString(bytes))
                {
                    failures.AppendLine($"  {label}: the string row's text differs from the byte row's");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (SerializeToString):\n{failures}");
            }
            log.WriteLine($"{doc.Name}: {pairs.Count} string outputs equal the byte rows' text");
        }

        // -------------------------------------------------------------------------------------------------------------
        // Every DOM holds the document's scalars and collections (Styml's tokenize counts).

        private static void DomCountsOf(Doc doc, TextWriter log)
        {
            (long scalars, long starts, long aliases) = StymlTokenCounts(doc.Yaml);
            var expected = new DomCounts(scalars, starts, aliases);
            var results = new List<(string Label, DomCounts Counts)>
            {
                ("Styml document", Dom.CountStyml(doc.Yaml, expand: false)),
                ("Styml node", Dom.CountStymlNode(doc.Yaml)),
                ("YamlDotNet", Dom.CountYamlDotNet(doc.YamlText)),
                ("SharpYaml", Dom.CountSharpYaml(doc.YamlText)),
                ("STJ document", Dom.CountStj(doc.Json)),
                ("STJ node", Dom.CountStjNode(doc.Json)),
            };
            var failures = new StringBuilder();
            foreach ((string label, DomCounts counts) in results)
            {
                if (counts != expected)
                {
                    failures.AppendLine($"  {label}: {counts}; Styml's tokens give {expected}");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (DOM):\n{failures}");
            }
            log.WriteLine($"{doc.Name}: {results.Count} DOM walks equal ({expected}); SharpYaml syntax tree {Dom.SharpYamlSyntax(doc.YamlText):N0} tokens (not compared)");
        }

        private static (long Scalars, long Starts, long Aliases) StymlTokenCounts(ReadOnlySpan<byte> utf8)
        {
            var reader = new System.Text.Yaml.Utf8YamlReader(utf8, Libraries.StymlReader);
            long scalars = 0, starts = 0, aliases = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case System.Text.Yaml.YamlTokenType.StartObject:
                    case System.Text.Yaml.YamlTokenType.StartArray:
                        starts++;
                        break;
                    case System.Text.Yaml.YamlTokenType.Alias:
                        aliases++;
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
            return (scalars, starts, aliases);
        }

        private static (long, long, long) YamlDotNetTokenCounts(string text)
        {
            var parser = new YamlDotNet.Core.Parser(new StringReader(text));
            long scalars = 0, starts = 0, aliases = 0;
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case YamlDotNet.Core.Events.Scalar: scalars++; break;
                    case YamlDotNet.Core.Events.MappingStart:
                    case YamlDotNet.Core.Events.SequenceStart: starts++; break;
                    case YamlDotNet.Core.Events.AnchorAlias: aliases++; break;
                }
            }
            return (scalars, starts, aliases);
        }

        private static (long, long, long) SharpYamlTokenCounts(string text)
        {
            SharpYaml.IParser parser = SharpYaml.Parser.CreateParser(new StringReader(text));
            long scalars = 0, starts = 0, aliases = 0;
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case SharpYaml.Events.Scalar: scalars++; break;
                    case SharpYaml.Events.MappingStart:
                    case SharpYaml.Events.SequenceStart: starts++; break;
                    case SharpYaml.Events.AnchorAlias: aliases++; break;
                }
            }
            return (scalars, starts, aliases);
        }

        private static (long, long, long) VYamlTokenCounts(byte[] utf8)
        {
            var parser = VYaml.Parser.YamlParser.FromBytes(utf8);
            long scalars = 0, starts = 0, aliases = 0;
            while (parser.Read())
            {
                switch (parser.CurrentEventType)
                {
                    case VYaml.Parser.ParseEventType.Scalar: scalars++; break;
                    case VYaml.Parser.ParseEventType.MappingStart:
                    case VYaml.Parser.ParseEventType.SequenceStart: starts++; break;
                    case VYaml.Parser.ParseEventType.Alias: aliases++; break;
                }
            }
            return (scalars, starts, aliases);
        }

        // -------------------------------------------------------------------------------------------------------------
        // The anchors documents.

        private static void Anchors(Doc doc, TextWriter log)
        {
            bool merge = Corpus.IsMerge(doc.Name);
            var failures = new StringBuilder();

            // Tokens: one alias event per alias, none expanded.
            (long, long, long) tokens = StymlTokenCounts(doc.Yaml);
            foreach ((string label, (long, long, long) counts) in new[]
            {
                ("YamlDotNet", YamlDotNetTokenCounts(doc.YamlText)),
                ("SharpYaml", SharpYamlTokenCounts(doc.YamlText)),
                ("VYaml", VYamlTokenCounts(doc.Yaml)),
            })
            {
                if (counts != tokens)
                {
                    failures.AppendLine($"  tokens {label}: {counts} (scalars, starts, aliases); Styml has {tokens}");
                }
            }
            (long jsonScalars, long jsonStarts) = (StymlTokenCounts(doc.Json).Scalars, StymlTokenCounts(doc.Json).Starts);

            // DOM: Styml's alias rows and YamlDotNet's shared nodes, counted once; SharpYaml's expanded copies against
            // Styml's walk descending into aliases.
            var compact = new DomCounts(tokens.Item1, tokens.Item2, tokens.Item3);
            DomCounts stymlCompact = Dom.CountStyml(doc.Yaml, expand: false);
            DomCounts stymlExpanded = Dom.CountStyml(doc.Yaml, expand: true);
            DomCounts yamlDotNet = Dom.CountYamlDotNet(doc.YamlText);
            DomCounts sharpYaml = Dom.CountSharpYaml(doc.YamlText);
            DomCounts stymlNode = Dom.CountStymlNode(doc.Yaml);
            DomCounts stj = Dom.CountStj(doc.Json);
            if (stymlCompact != compact)
            {
                failures.AppendLine($"  DOM Styml document: {stymlCompact}; tokens give {compact}");
            }
            if (yamlDotNet != compact)
            {
                failures.AppendLine($"  DOM YamlDotNet: {yamlDotNet}; tokens give {compact}");
            }
            if (sharpYaml != stymlExpanded)
            {
                failures.AppendLine($"  DOM SharpYaml (expanded): {sharpYaml}; Styml expanded {stymlExpanded}");
            }
            if (!merge && stymlExpanded != stj)
            {
                failures.AppendLine($"  DOM Styml expanded: {stymlExpanded}; the expanded twin has {stj}");
            }

            // Deserialization: every graph equals System.Text.Json's graph of the expanded twin. Styml reads with its
            // default limits (MaxAliasExpansion unset), which is the alias bound's check.
            string expected = Check.Canonical(AnchorLibraries.Json(doc.Json));
            var reads = new List<(string Label, Func<object?> Read)>
            {
                ("Styml reflection", () => AnchorLibraries.Styml(doc.Yaml, merge)),
                ("Styml generated", () => AnchorLibraries.StymlGeneratedRead(doc.Yaml, merge)),
                ("YamlDotNet reflection", () => AnchorLibraries.YamlDotNet(doc.YamlText, merge, AnchorLibraries.YamlDotNetReflection)),
                ("YamlDotNet static", () => AnchorLibraries.YamlDotNet(doc.YamlText, merge, AnchorLibraries.YamlDotNetStatic)),
                ("STJ generated", () => AnchorLibraries.JsonGeneratedRead(doc.Json)),
            };
            if (!merge)
            {
                reads.Add(("SharpYaml reflection", () => AnchorLibraries.SharpYaml(doc.YamlText)));
                reads.Add(("SharpYaml generated", () => AnchorLibraries.SharpYamlGeneratedRead(doc.YamlText)));
                reads.Add(("VYaml generated", () => AnchorLibraries.VYaml(doc.Yaml)));
            }
            foreach ((string label, Func<object?> read) in reads)
            {
                try
                {
                    string actual = Check.Canonical(read());
                    if (actual != expected)
                    {
                        int at = 0;
                        while (at < Math.Min(actual.Length, expected.Length) && actual[at] == expected[at])
                        {
                            at++;
                        }
                        int from = Math.Max(0, at - 60);
                        failures.AppendLine($"  read {label}: a different graph at char {at}: expected …{expected[from..Math.Min(expected.Length, at + 60)]}… got …{actual[from..Math.Min(actual.Length, at + 60)]}…");
                    }
                }
                catch (Exception e)
                {
                    failures.AppendLine($"  read {label}: threw {e.GetType().Name}: {e.Message.Split('\n')[0]}");
                }
            }

            string serialized = merge ? "" : SerializeAnchors(doc, failures);
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (anchors):\n{failures}");
            }
            log.WriteLine($"{doc.Name}: {doc.Yaml.Length:N0} bytes, {tokens.Item3:N0} aliases; 4 token streams equal ({compact}); twin {jsonScalars:N0} scalars, {jsonStarts:N0} starts (not compared)");
            log.WriteLine($"{doc.Name}: DOM Styml document = YamlDotNet ({compact}); SharpYaml = Styml expanded ({stymlExpanded}); Styml node {stymlNode}{(merge ? "" : $"; expanded = twin {(stymlExpanded == stj ? "yes" : "no")}")}");
            log.WriteLine($"{doc.Name}: {reads.Count} graphs equal the expanded twin's, Styml with its default alias limits{(merge ? " and ResolveMergeKeys" : "")}");
            if (serialized.Length > 0)
            {
                log.WriteLine(serialized);
            }
        }

        // AnchorsSerialize: each output, read back by Styml, equals the input graph; the rows that keep references give
        // the shared instances back, shared.
        private static string SerializeAnchors(Doc doc, StringBuilder failures)
        {
            var scenario = new AnchorsSerialize { Document = doc.Name };
            scenario.Setup();
            ComposeFile graph = scenario.Graph;
            string expected = Check.Canonical(graph);
            (int blocks, int distinct) = Shared(graph);
            if (distinct >= blocks)
            {
                failures.AppendLine($"  serialize: the input graph shares no instance ({blocks} blocks, {distinct} distinct)");
            }

            scenario.SharpYaml();
            byte[] sharpYaml = scenario.WrittenChars;
            scenario.VYaml_Expanded();
            byte[] vyaml = scenario.Written;
            var rows = new List<(string Label, byte[] Output, bool KeepsReferences)>
            {
                ("Styml_Block", scenario.Styml_Block(), true),
                ("YamlDotNet", Encoding.UTF8.GetBytes(scenario.YamlDotNet()), true),
                ("SharpYaml", sharpYaml, true),
                ("VYaml_Expanded", vyaml, false),
                ("Stj_Expanded", scenario.Stj_Expanded(), false),
            };
            var parts = new List<string>();
            foreach ((string label, byte[] output, bool keeps) in rows)
            {
                ComposeFile? back;
                try
                {
                    back = AnchorLibraries.Styml(output, merge: false);
                }
                catch (Exception e)
                {
                    failures.AppendLine($"  serialize {label}: Styml's read-back threw {e.GetType().Name}: {e.Message.Split('\n')[0]}");
                    continue;
                }
                string actual = Check.Canonical(back);
                // VYaml 1.6.1's plain scalar for a string that ends in a line break (WriteCheck's allowance, M4).
                if (actual != expected && !(label.StartsWith("VYaml", StringComparison.Ordinal) && actual.Replace("\\n\"", "\"") == expected.Replace("\\n\"", "\"")))
                {
                    failures.AppendLine($"  serialize {label}: reads back to a different graph");
                }
                long aliases = StymlTokenCounts(output).Aliases;
                (int _, int backDistinct) = Shared(back!);
                if (keeps && (aliases == 0 || backDistinct != distinct))
                {
                    failures.AppendLine($"  serialize {label}: {aliases} aliases written, {backDistinct} distinct blocks read back; the input has {distinct}");
                }
                if (!keeps && aliases != 0)
                {
                    failures.AppendLine($"  serialize {label}: {aliases} aliases in an expanded row");
                }
                parts.Add($"{label} {output.Length:N0} bytes, {aliases} aliases");
            }
            string path = PathOf(AnchorLibraries.StymlPreserveGenerated.ComposeFile, graph, YamlWriteLibraries.BlockWriterOptions);
            return $"{doc.Name}: serialize, {blocks} shared-member blocks in {distinct} instances, every output reads back equal, shared where kept: {string.Join("; ", parts)}; Styml {path}";
        }

        // The services' environment and healthcheck blocks: how many, and how many distinct instances.
        private static (int Blocks, int Distinct) Shared(ComposeFile file)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int blocks = 0;
            foreach (ComposeService service in file.Services!.Values)
            {
                foreach (object? block in new object?[] { service.Environment, service.Healthcheck })
                {
                    if (block is not null)
                    {
                        blocks++;
                        seen.Add(block);
                    }
                }
            }
            return (blocks, seen.Count);
        }
    }
}
