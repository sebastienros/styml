// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The write side of the equal-work check, and the output-hash mode of
// gate G2. Every Serialize row's output, read back by its own library, must give the graph the JSON twin gives; both
// Write rows' outputs must read back to it as JSON.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Styml.Benchmarks.Model;
using SY = SharpYaml;
using VY = VYaml.Serialization.YamlSerializer;

namespace Styml.Benchmarks
{
    internal static class WriteCheck
    {
        private static readonly YamlDotNet.Serialization.IDeserializer s_yamlDotNet = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        public static void Document(Doc doc, TextWriter? log)
        {
            string expected = Check.Canonical(Libraries.Json(doc.Json, doc.Family));
            object graph = Libraries.Json(doc.Json, doc.Family)!;
            var write = new Write { Document = doc.Name };
            write.Setup();
            var results = new List<(string Label, Func<object?> Read)>
            {
                ("Write Styml", () => Libraries.Json(write.Output(nameof(Write.Styml)), doc.Family)),
                ("Write Styml_Indented", () => Libraries.Json(write.Output(nameof(Write.Styml_Indented)), doc.Family)),
                ("Write Stj", () => Libraries.Json(write.Output(nameof(Write.Stj)), doc.Family)),
                ("Write Stj_Indented", () => Libraries.Json(write.Output(nameof(Write.Stj_Indented)), doc.Family)),
                ("Serialize Styml reflection", () => Libraries.Styml(WriteLibraries.Styml(graph, doc.Family, WriteLibraries.StymlOptions), doc.Family, Libraries.StymlOptions)),
                ("Serialize Styml generated", () => Libraries.Styml(WriteLibraries.StymlGeneratedWrite(graph, doc.Family, WriteLibraries.StymlGenerated), doc.Family, Libraries.StymlOptions)),
                ("Serialize STJ reflection", () => Libraries.Json(WriteLibraries.Json(graph, doc.Family), doc.Family)),
                ("Serialize STJ generated", () => Libraries.Json(WriteLibraries.JsonGeneratedWrite(graph, doc.Family), doc.Family)),
                ("Serialize YamlDotNet reflection", () => Libraries.YamlDotNet(WriteLibraries.YamlDotNet(graph, doc.Family, WriteLibraries.YamlDotNetReflection), doc.Family, s_yamlDotNet)),
                ("Serialize YamlDotNet static", () => Libraries.YamlDotNet(WriteLibraries.YamlDotNet(graph, doc.Family, WriteLibraries.YamlDotNetStatic), doc.Family, s_yamlDotNet)),
                ("Serialize SharpYaml reflection", () => Libraries.SharpYaml(WriteLibraries.SharpYaml(graph, doc.Family), doc.Family)),
                ("Serialize SharpYaml generated", () => Libraries.SharpYaml(WriteLibraries.SharpYamlGeneratedWrite(graph, doc.Family), doc.Family)),
                ("Serialize VYaml generated", () => Libraries.VYaml(WriteLibraries.VYaml(graph, doc.Family), doc.Family)),
            };
            var failures = new StringBuilder();
            foreach ((string label, Func<object?> read) in results)
            {
                string actual;
                try
                {
                    actual = Check.Canonical(read());
                }
                catch (Exception e)
                {
                    failures.AppendLine($"  {label}: threw {e.GetType().Name}: {e.Message.Split('\n')[0]}");
                    continue;
                }
                // VYaml 1.6.1 writes a string that ends in a line break as a plain scalar followed by a blank line, and
                // so reads it back without the line break. That is its own defect, not less work: the row is kept, and
                // the difference is allowed for VYaml only, when it is nothing else.
                if (actual != expected && label.StartsWith("Serialize VYaml", StringComparison.Ordinal)
                    && actual.Replace("\\n\"", "\"") == expected.Replace("\\n\"", "\""))
                {
                    log?.WriteLine($"{doc.Name}: {label} reads back with the final line break of some strings dropped (VYaml's plain scalars)");
                    continue;
                }
                if (actual != expected)
                {
                    int at = 0;
                    while (at < Math.Min(actual.Length, expected.Length) && actual[at] == expected[at])
                    {
                        at++;
                    }
                    int from = Math.Max(0, at - 60);
                    failures.AppendLine($"  {label}: the output reads back to a different graph at char {at}: expected …{expected[from..Math.Min(expected.Length, at + 60)]}… got …{actual[from..Math.Min(actual.Length, at + 60)]}…");
                }
            }
            if (failures.Length > 0)
            {
                throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (writing):\n{failures}");
            }
            log?.WriteLine($"{doc.Name}: {results.Count} written outputs read back equal");

            // The block-style rows (M4a) exist only from M4a's Styml.dll on.
            if (typeof(System.Text.Yaml.YamlWriterOptions).GetProperty("Style") is not null)
            {
                BlockDocument(doc, expected, log);
            }
        }

        // Block output, read back by Styml's YAML reader, must give the graph of the JSON twin too.
        private static void BlockDocument(Doc doc, string expected, TextWriter? log)
        {
            object graph = Libraries.Json(doc.Json, doc.Family)!;
            var write = new WriteBlock { Document = doc.Name };
            write.Setup();
            var results = new List<(string Label, byte[] Output)>
            {
                ("Write Styml_Block", write.Output()),
                ("Serialize Styml_Block reflection", WriteLibraries.Styml(graph, doc.Family, BlockLibraries.BlockOptions)),
                ("Serialize Styml_Block generated", WriteLibraries.StymlGeneratedWrite(graph, doc.Family, BlockLibraries.BlockGenerated)),
            };
            foreach ((string label, byte[] output) in results)
            {
                string actual = Check.Canonical(Libraries.Styml(output, doc.Family, Libraries.StymlOptions));
                if (actual != expected)
                {
                    throw new InvalidOperationException($"Equal-work check failed for {doc.Name} (block): {label} reads back to a different graph.");
                }
            }

            log?.WriteLine($"{doc.Name}: {results.Count} block outputs read back equal");
        }

        /// <summary>'hash': one SHA-256 per Styml row and document, of the bytes the row writes, so that two folders of
        /// the dll swap can be compared by diff (gate G2). Also prints whether the JSON-compatible switch is set.</summary>
        public static int Hash(TextWriter output)
        {
            WriteLibraries.SetSwitch();
            output.WriteLine($"# switch {WriteLibraries.JsonCompatibleSwitch} = {WriteLibraries.SwitchValue}");
            foreach (string name in Corpus.Names)
            {
                Doc doc = Corpus.Get(name);
                var write = new Write { Document = name };
                write.Setup();
                var reflection = new SerializeReflection { Document = name };
                reflection.Setup();
                var generated = new SerializeSourceGen { Document = name };
                generated.Setup();
                Print(output, "Write", "Styml", name, write.Output(nameof(Write.Styml)));
                Print(output, "Write", "Styml_Indented", name, write.Output(nameof(Write.Styml_Indented)));
                Print(output, "SerializeReflection", "Styml", name, (byte[])reflection.Styml());
                Print(output, "SerializeSourceGen", "Styml", name, (byte[])generated.Styml());
            }
            return 0;
        }

        private static void Print(TextWriter output, string className, string method, string document, byte[] bytes) =>
            output.WriteLine($"{className} {method} {document} {bytes.Length} {Convert.ToHexString(SHA256.HashData(bytes))[..16]}");
    }
}
