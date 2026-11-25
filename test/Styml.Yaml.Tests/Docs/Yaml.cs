// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/writing-yaml.md, guide/scalar-styles.md, guide/schemas.md and guide/reading-yaml.md.
    public partial class YamlSamples
    {
        // docs: write-types
        public class Pipeline
        {
            public string Name { get; set; } = "ci";

            public string[] Branches { get; set; } = ["main", "release/*"];

            public List<Step> Steps { get; set; } = [new("restore", "dotnet restore"), new("test", "dotnet test --no-build")];
        }

        public record Step(string Name, string Run);
        // docs: end

        [Fact]
        public void BlockAndJson()
        {
            // docs: write-styles yaml
            var pipeline = new Pipeline();
            string block = YamlSerializer.Serialize(pipeline);
            string json = YamlSerializer.Serialize(pipeline, new YamlSerializerOptions
            {
                WriteStyle = YamlWriterStyle.JsonCompatible,
            });
            // docs: end
            Check("write-styles", block + "---\n" + json + "\n");
        }

        [Fact]
        public void Layout()
        {
            // docs: write-layout yaml
            var options = new YamlSerializerOptions
            {
                IndentSize = 4,
                IndentSequences = true,
                ExpandSequenceItems = true,
            };
            string yaml = YamlSerializer.Serialize(new Pipeline(), options);
            // docs: end
            Check("write-layout", yaml);
        }

        [Fact]
        public void LineWidth()
        {
            // docs: write-width yaml
            var options = new YamlSerializerOptions { MaxLineWidth = 50 };
            string yaml = YamlSerializer.Serialize(new
            {
                Summary = "Restores the packages, builds the solution in Release, and runs every test project in parallel.",
                Tags = new[] { "dotnet", "yaml", "serializer", "source-generator", "aot", "trimming" },
            }, options);
            YamlNode flow = YamlNode.Parse("tags: [dotnet, yaml, serializer, source-generator, aot, trimming]")!;
            string flowYaml = flow.ToYamlString(options);
            // docs: end
            Check("write-width", yaml + "---\n" + flowYaml);
        }

        // docs: comments-types
        public class ServerSettings
        {
            public bool Tls { get; set; } = true;

            [YamlComment("Requests per second.\nOver this limit, clients get a 429.")]
            public int RateLimit { get; set; } = 100;

            [YamlComment("The address the server listens on.")]
            public string Host { get; set; } = "0.0.0.0";

            [YamlPropertyOrder(-1)]
            public string Name { get; set; } = "api";
        }
        // docs: end

        [Fact]
        public void Comments()
        {
            // docs: comments yaml
            string declared = YamlSerializer.Serialize(new ServerSettings());
            string sorted = YamlSerializer.Serialize(new ServerSettings(), new YamlSerializerOptions
            {
                MappingOrder = YamlMappingOrder.Sorted,
            });
            // docs: end
            Check("comments", declared + "---\n" + sorted);
        }

        [Fact]
        public void Quoting()
        {
            // docs: quoting yaml
            string[] values =
            [
                "plain text", "42", "3.14", "true", "null", "~", "", "yes", "off", "2026-11-24",
                "0x1F", "1_000", "- item", "key: value", "a #comment", "@handle", "'quoted'", " padded ",
            ];
            string yaml = YamlSerializer.Serialize(values);
            // docs: end
            Check("quoting", yaml);
        }

        [Fact]
        public void Lenient()
        {
            // docs: lenient yaml
            string[] values = ["nan", "Infinity", "NULL", "True", "+12", "1e3"];
            string strict = YamlSerializer.Serialize(values);
            string lenient = YamlSerializer.Serialize(values, new YamlSerializerOptions { QuoteForLenientReaders = true });
            // docs: end
            Check("lenient", strict + "---\n" + lenient);
        }

        // docs: styles-types
        public class Release
        {
            public string Version { get; set; } = "2.1.0";

            [YamlScalarStyle(YamlScalarStyle.Folded)]
            public string Summary { get; set; } = "A release that fixes\nthe merge of anchors.";

            public string Notes { get; set; } = "- Faster reads\n- Smaller writes\n";

            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
            public string Codename { get; set; } = "it's alive";
        }
        // docs: end

        [Fact]
        public void Styles()
        {
            // docs: styles yaml
            string defaults = YamlSerializer.Serialize(new Release());
            string quoted = YamlSerializer.Serialize(new Release(), new YamlSerializerOptions
            {
                MultiLineStringStyle = YamlScalarStyle.DoubleQuoted,
                DefaultScalarStyle = YamlScalarStyle.DoubleQuoted,
            });
            // docs: end
            Check("styles", defaults + "---\n" + quoted);
        }

        [Fact]
        public void Schemas()
        {
            // docs: schemas text
            string yaml = "a: 12\nb: 0o14\nc: 012\nd: yes\ne: ~\nf: 1_000\ng: .inf\nh: 2026-11-24\n";
            string Read(YamlSchemaKind schema) => Describe(() =>
            {
                var values = YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml, new YamlSerializerOptions { Schema = schema })!;
                return string.Join(", ", values.Select(p => $"{p.Key}={Kind(p.Value)}"));
            });
            static string Kind(object? value) => value is YamlElement e ? e.ValueKind.ToString() : value?.GetType().Name ?? "null";

            string output = string.Join("\n", new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe }
                .Select(schema => $"{schema}: {Read(schema)}"));
            // docs: end
            Check("schemas", output + "\n", defaultStyle: false);
        }

        [Fact]
        public void Yaml11Values()
        {
            // docs: schemas-yaml11 text
            var options = new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 };
            var flags = YamlSerializer.Deserialize<Dictionary<string, bool>>("a: yes\nb: Off\nc: on\n", options)!;
            var numbers = YamlSerializer.Deserialize<Dictionary<string, int>>("octal: 012\nbinary: 0b101\nsexagesimal: 1:30\n", options)!;
            string output = string.Join(", ", flags.Select(p => $"{p.Key}={p.Value}")) + "\n"
                + string.Join(", ", numbers.Select(p => $"{p.Key}={p.Value}"));
            // docs: end
            Check("schemas-yaml11", output + "\n", defaultStyle: false);
        }

        // docs: typed-types
        public record Limits(int Cpu, string Memory, bool Burst);
        // docs: end

        [Fact]
        public void Typed()
        {
            // docs: typed text
            Limits plain = YamlSerializer.Deserialize<Limits>("Cpu: 0x10\nMemory: 512\nBurst: true\n")!;
            string quoted = Describe(() => YamlSerializer.Deserialize<Limits>("Cpu: '4'\nMemory: 512\nBurst: 'TRUE'\n"));
            Limits lenient = YamlSerializer.Deserialize<Limits>("Cpu: '4'\nMemory: 512\nBurst: 'TRUE'\n",
                new YamlSerializerOptions { AcceptLenientScalars = true })!;
            // docs: end
            Check("typed", $"plain: {plain}\nquoted: {quoted}\nlenient: {lenient}\n", defaultStyle: false);
        }

        [Fact]
        public void DuplicateKeys()
        {
            // docs: duplicates text
            string yaml = "image: nginx:1.26\nimage: nginx:1.27\n";
            string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, string>>(yaml));
            var last = YamlSerializer.Deserialize<Dictionary<string, string>>(yaml,
                new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins })!;
            // docs: end
            Check("duplicates", $"{error}\nLastWins: {last["image"]}\n", defaultStyle: false);
        }

        [Fact]
        public void Versions()
        {
            // docs: versions text
            static string Read(string version) => Describe(() =>
            {
                var values = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>($"%YAML {version}\n---\nanswer: yes\n")!;
                return values["answer"].ValueKind;
            });

            string output = string.Join("\n", new[] { "1.1", "1.2", "1.3", "2.0" }.Select(version => $"{version}: {Read(version)}"));
            // docs: end
            Check("versions", output + "\n", defaultStyle: false);
        }

        [Fact]
        public void Yaml13Draft()
        {
            // docs: versions-draft text
            static string Read(string yaml) => Describe(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { ApplyYaml13DraftRestrictions = true });
                while (reader.Read())
                {
                }
                return "read";
            });

            string[] documents = { "text\n", "---\ntext\n", "--- text\n", "&a\nkey: v\n", "--- &a\nkey: v\n", "[a, b]: v\n" };
            string output = string.Join("\n", documents.Select(yaml => $"{yaml.Replace("\n", "\\n")} => {Read(yaml)}"));
            // docs: end
            Check("versions-draft", output + "\n", defaultStyle: false);
        }

        [Fact]
        public void Empty()
        {
            // docs: empty text
            Step? fromEmpty = YamlSerializer.Deserialize<Step>("");
            Step? fromComments = YamlSerializer.Deserialize<Step>("# nothing configured yet\n");
            int[]? fromNull = YamlSerializer.Deserialize<int[]>("~");
            // docs: end
            Check("empty", $"{fromEmpty is null}, {fromComments is null}, {fromNull is null}\n", defaultStyle: false);
        }

        [Fact]
        public async Task Documents()
        {
            // docs: documents yaml
            string stream = """
                kind: Service
                name: web
                ---
                kind: Deployment
                name: web
                ...
                ---
                kind: Ingress
                name: web
                """;
            var kinds = new List<string>();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
            await foreach (var manifest in YamlSerializer.DeserializeAsyncEnumerable<Dictionary<string, string>>(input, topLevelValues: true))
            {
                kinds.Add(manifest!["kind"]);
            }

            using var output = new MemoryStream();
            await YamlSerializer.SerializeAsyncEnumerable(output, Manifests(), topLevelValues: true);

            static async IAsyncEnumerable<object> Manifests()
            {
                yield return new { kind = "ConfigMap", name = "settings" };
                await Task.Yield();
                yield return new { kind = "Secret", name = "tokens" };
            }
            // docs: end
            Check("documents", "# read: " + string.Join(", ", kinds) + "\n" + Encoding.UTF8.GetString(output.ToArray()));
        }

        [Fact]
        public void DocumentsReader()
        {
            // docs: documents-reader text
            byte[] utf8 = Encoding.UTF8.GetBytes("a: 1\n---\nb: 2\n");
            var reader = new Utf8YamlReader(utf8, new YamlReaderOptions { AllowMultipleValues = true });
            var roots = new List<string>();
            while (reader.Read())
            {
                using YamlDocument document = YamlDocument.ParseValue(ref reader);
                roots.Add(document.RootElement.EnumerateObject().First().Name);
            }
            // docs: end
            Check("documents-reader", string.Join(", ", roots) + "\n", defaultStyle: false);
        }

        [Fact]
        public void CollectionKeys()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // docs: collection-keys yaml
            string yaml = """
                ? [0, 0]
                : origin
                [1, 0]: east
                ? {x: 0, y: 1}
                : north
                """;
            var grid = YamlSerializer.Deserialize<Dictionary<List<int>, string>>("? [0, 0]\n: origin\n[1, 0]: east\n")!;
            string first = $"[{string.Join(", ", grid.Keys.First())}] -> {grid.Values.First()}";
            using YamlDocument document = YamlDocument.Parse(yaml);
            string kinds = string.Join(", ", document.RootElement.EnumerateObject().Select(p => $"{p.GetKeyElement().ValueKind} {p.KeyKind}"));
            string written = YamlSerializer.Serialize(grid);
            // docs: end
            Check("collection-keys", $"# {first}\n# {kinds}\n{written}");
        }

        [Fact]
        public void JsonInput()
        {
            // docs: json-input json
            string json = """
                // The service, from a JSON file with comments
                {
                  "name": "web",      /* the public name */
                  "ports": [80, 443,],
                  "url": "http://example.org", // slashes in a value are text
                }
                """;
            var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };
            var service = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>(json, options)!;
            string compact = YamlSerializer.Serialize(service, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            // docs: end
            Check("json-input", compact + "\n", defaultStyle: false);
        }

        [Fact]
        public void Depth()
        {
            // docs: depth text
            string deep = string.Concat(Enumerable.Repeat("[", 100)) + string.Concat(Enumerable.Repeat("]", 100));
            string error = Describe(() => YamlSerializer.Deserialize<object>(deep));
            object? read = YamlSerializer.Deserialize<object>(deep, new YamlSerializerOptions { MaxDepth = 128 });
            // docs: end
            Check("depth", error + "\nMaxDepth 128: " + read!.GetType().Name + "\n", defaultStyle: false);
        }
    }
}
