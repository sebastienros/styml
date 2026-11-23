// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests.Readme
{
    /// <summary>
    /// The README's samples. Each region between "// readme: name [language]" and "// readme: end" is the code the
    /// README shows, and Readme/expected/name.txt, when the marker names its language, is the output it shows;
    /// eng/docs/readme.py copies both into README.md and ReadmeDocTests fails when they drift. Outputs are compared with LF line breaks (the writer ends lines with
    /// Environment.NewLine unless NewLine is set). With STYML_README_RECORD set to the Readme folder, a sample writes
    /// its output there instead of comparing it.
    /// </summary>
    public partial class ReadmeSamples
    {
        // readme: types
        public record Service(string Name, string Image, int[] Ports, Dictionary<string, string> Environment);

        [YamlSerializable(typeof(Service))]
        [YamlSourceGenerationOptions(PropertyNamingPolicy = YamlKnownNamingPolicy.CamelCase, IndentSequences = true)]
        public partial class AppYamlContext : YamlSerializerContext;
        // readme: end

        private static Service Web() => new("web", "nginx:1.27", [80, 443], new() { ["TIER"] = "front", ["REPLICAS"] = "3" });

        [Fact]
        public void Reflection()
        {
            Service service = Web();
            // readme: reflection yaml
            string yaml = YamlSerializer.Serialize(service);
            Service back = YamlSerializer.Deserialize<Service>(yaml)!;
            // readme: end
            Assert.Equal(service.Name, back.Name);
            Check("reflection", yaml, defaultStyle: true);
        }

        [Fact]
        public void SourceGenerated()
        {
            Service service = Web();
            // readme: source-generated yaml
            string yaml = YamlSerializer.Serialize(service, AppYamlContext.Default.Service);
            Service back = YamlSerializer.Deserialize(yaml, AppYamlContext.Default.Service)!;
            // readme: end
            Assert.Equal(service.Ports, back.Ports);
            Check("source-generated", yaml, defaultStyle: true);
        }

        [Fact]
        public async Task Streams()
        {
            Service service = Web();
            // readme: streams text
            using var stream = new MemoryStream();
            await YamlSerializer.SerializeAsync(stream, service);
            stream.Position = 0;
            Service? back = await YamlSerializer.DeserializeAsync<Service>(stream);
            // readme: end
            Check("streams", $"{back!.Name}: {back.Ports.Length} ports", defaultStyle: false);
        }

        [Fact]
        public void OutputStyles()
        {
            var value = new { Name = "web", Ports = new[] { 80, 443 }, Env = new { Tier = "front" } };
            // readme: output-styles text
            var json = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible, WriteIndented = true };
            var indented = new YamlSerializerOptions { IndentSequences = true, IndentSize = 4 };
            string output = YamlSerializer.Serialize(value, json) + "\n" + YamlSerializer.Serialize(value, indented);
            // readme: end
            Check("output-styles", output, defaultStyle: true);
        }

        [Fact]
        public void ExpandedItems()
        {
            var value = new[] { new { Name = "a", Port = 80 }, new { Name = "b", Port = 81 } };
            // readme: expanded-items yaml
            string compact = YamlSerializer.Serialize(value);
            string expanded = YamlSerializer.Serialize(value, new YamlSerializerOptions { ExpandSequenceItems = true });
            // readme: end
            Check("expanded-items", compact + "\n" + expanded, defaultStyle: true);
        }

        [Fact]
        public void LineWidth()
        {
            // readme: line-width yaml
            var value = new
            {
                Description = "A long description that goes on well past the width the writer is given, so it folds.",
                Tags = new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta" },
            };
            var options = new YamlSerializerOptions { MaxLineWidth = 40 };
            string yaml = YamlSerializer.Serialize(value, options);
            string flow = YamlNode.Parse("tags: [alpha, beta, gamma, delta, epsilon, zeta, eta, theta]")!.ToYamlString(options);
            // readme: end
            Check("line-width", yaml + "\n" + flow, defaultStyle: true);
        }

        [Fact]
        public void NewLine()
        {
            // readme: new-line
            var options = new YamlSerializerOptions { NewLine = "\r\n" };
            string yaml = YamlSerializer.Serialize(new { A = 1, B = 2 }, options);
            bool crlf = yaml == "A: 1\r\nB: 2\r\n";
            // readme: end
            Assert.True(crlf || GrammarModeInitializer.ExpectJsonCompatibleWriter);
        }

        [Fact]
        public void Quoting()
        {
            // readme: quoting yaml
            var values = new Dictionary<string, string>
            {
                ["text"] = "plain text",
                ["number"] = "3",
                ["boolean"] = "true",
                ["yaml11"] = "yes",
                ["null"] = "null",
                ["date"] = "2001-12-14",
                ["indicator"] = "- dash",
                ["colon"] = "a: b",
                ["comment"] = "a #b",
            };
            string yaml = YamlSerializer.Serialize(values);
            string lenient = YamlSerializer.Serialize(new[] { "nan", "Infinity" }, new YamlSerializerOptions { QuoteForLenientReaders = true });
            // readme: end
            Check("quoting", yaml + "\n" + YamlSerializer.Serialize(new[] { "nan", "Infinity" }) + "\n" + lenient, defaultStyle: true);
        }

        // readme: styled
        public class Note
        {
            public string Title { get; set; } = "";

            [YamlScalarStyle(YamlScalarStyle.Folded)]
            public string Summary { get; set; } = "";

            public string Body { get; set; } = "";

            public string Signature { get; set; } = "";
        }
        // readme: end

        [Fact]
        public void ScalarStyles()
        {
            // readme: scalar-styles yaml
            var note = new Note
            {
                Title = "Release",
                Summary = "A summary on two lines\nthat a folded scalar keeps.",
                Body = "line one\nline two\n",
                Signature = "the team",
            };
            string literal = YamlSerializer.Serialize(note);
            string singleQuoted = YamlSerializer.Serialize(note, new YamlSerializerOptions { DefaultScalarStyle = YamlScalarStyle.SingleQuoted });
            // readme: end
            Check("scalar-styles", literal + "\n" + singleQuoted, defaultStyle: true);
        }

        // readme: comments
        public class Settings
        {
            [YamlComment("How many workers to start.")]
            public int Workers { get; set; } = 4;

            public List<string> Hosts { get; set; } = [];

            public string? Region { get; set; }

            public string Name { get; set; } = "api";
        }
        // readme: end

        [Fact]
        public void CommentsAndOrder()
        {
            // readme: comments-order yaml
            var options = new YamlSerializerOptions
            {
                MappingOrder = YamlMappingOrder.Sorted,
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection,
            };
            string yaml = YamlSerializer.Serialize(new Settings(), options);
            // readme: end
            Check("comments-order", yaml, defaultStyle: true);
        }

        [Fact]
        public void Schemas()
        {
            // readme: schemas text
            string yaml = "a: 12\nb: yes\nc: 0x1F\nd: ~\ne: 1_000\nf: 0b101\ng: 012\n";
            string Read(YamlSchemaKind schema) => Describe(() =>
                YamlSerializer.Serialize(YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml, new YamlSerializerOptions { Schema = schema }),
                    new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));
            string output = string.Join("\n", new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe }
                .Select(schema => $"{schema}: {Read(schema)}"));
            // readme: end
            Check("schemas", output, defaultStyle: false);
        }

        [Fact]
        public void TypedScalars()
        {
            // readme: typed-scalars text
            var plain = YamlSerializer.Deserialize<Dictionary<string, int>>("a: 12\nb: 0o14\nc: 0xC\n")!;
            string quoted = Describe(() => YamlSerializer.Deserialize<Dictionary<string, int>>("a: '12'\n"));
            string lenient = Describe(() => YamlSerializer.Deserialize<Dictionary<string, int>>("a: '12'\n",
                new YamlSerializerOptions { AcceptLenientScalars = true })!["a"]);
            string text = YamlSerializer.Deserialize<Dictionary<string, string>>("a: 12\nb: true\n")!["b"];
            object number = YamlSerializer.Deserialize<Dictionary<string, object>>("a: 12\n")!["a"];
            // readme: end
            Check("typed-scalars", $"{string.Join(", ", plain.Values)}\nquoted: {quoted}\nlenient: {lenient}\nstring: {text}\nobject: {number.GetType().Name} {number}", defaultStyle: false);
        }

        [Fact]
        public void DuplicateKeys()
        {
            // readme: duplicate-keys text
            string yaml = "a: 1\na: 2\n";
            string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, int>>(yaml));
            int last = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml,
                new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins })!["a"];
            int first = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml,
                new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins })!["a"];
            Service? empty = YamlSerializer.Deserialize<Service>("# nothing but a comment\n");
            // readme: end
            Check("duplicate-keys", $"Error: {error}\nLastWins: {last}\nFirstWins: {first}\nempty input: {(empty is null ? "null" : "a value")}", defaultStyle: false);
        }

        // readme: anchors-types
        public class Node
        {
            public string Name { get; set; } = "";
            public Node? Next { get; set; }
            public List<string>? Labels { get; set; }
        }
        // readme: end

        [Fact]
        public void Anchors()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // readme: anchors text
            var labels = new List<string> { "x", "y" };
            var shared = new Node { Name = "shared", Labels = labels };
            var root = new Node { Name = "root", Next = shared, Labels = labels };
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            string yaml = YamlSerializer.Serialize(root, options);
            Node back = YamlSerializer.Deserialize<Node>(yaml)!;
            bool sameList = ReferenceEquals(back.Labels, back.Next!.Labels);
            // readme: end
            Check("anchors", yaml + "\nsame list after reading: " + sameList, defaultStyle: true);
        }

        [Fact]
        public void AliasBound()
        {
            // readme: alias-bound text
            string bomb = "a: &a [x, x, x, x, x, x, x, x, x, x]\nb: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a, *a]\n"
                + "c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b, *b]\nd: [*c, *c, *c, *c, *c, *c, *c, *c, *c, *c]\n";
            string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, object>>(bomb,
                new YamlSerializerOptions { MaxAliasExpansion = 1000 }));
            // readme: end
            Check("alias-bound", error, defaultStyle: false);
        }

        [Fact]
        public void MergeKeys()
        {
            // readme: merge-keys text
            string yaml = """
                defaults: &defaults
                  image: nginx:1.27
                  restart: always
                web:
                  <<: *defaults
                  restart: never
                """;
            var options = new YamlSerializerOptions { ResolveMergeKeys = true };
            var config = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(yaml, options)!;
            string web = string.Join(", ", config["web"].Select(p => $"{p.Key}={p.Value}"));
            // readme: end
            Check("merge-keys", web, defaultStyle: false);
        }

        // readme: polymorphism-types
        [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
        [YamlDerivedType(typeof(Circle), "circle")]
        [YamlDerivedType(typeof(Square), "square", Tag = "!box")]
        public abstract class Shape;

        public class Circle : Shape { public double Radius { get; set; } }

        public class Square : Shape { public double Side { get; set; } }
        // readme: end

        [Fact]
        public void Polymorphism()
        {
            // readme: polymorphism text
            Shape[] shapes = [new Circle { Radius = 1.5 }, new Square { Side = 2 }];
            string yaml = YamlSerializer.Serialize(shapes);
            Shape[] back = YamlSerializer.Deserialize<Shape[]>(yaml)!;
            string types = string.Join(", ", back.Select(s => s.GetType().Name));
            // readme: end
            Check("polymorphism", yaml + "\n" + types, defaultStyle: true);
        }

        [Fact]
        public void TagMappings()
        {
            // readme: tag-mappings text
            var options = new YamlSerializerOptions();
            options.TagMappings.Add("!circle", typeof(Circle));
            object? mapped = YamlSerializer.Deserialize<object>("!circle {Radius: 3}", options);
            string unknown = Describe(() => YamlSerializer.Deserialize<object>("!System.IO.FileInfo {}", options)!.GetType().Name);
            // readme: end
            Check("tag-mappings", $"mapped: {mapped!.GetType().Name}\nunmapped type name: {unknown}", defaultStyle: false);
        }

        [Fact]
        public void CollectionKeys()
        {
            // readme: collection-keys text
            string yaml = "? [a, b]\n: pair\n[c, d]: implicit\nplain: text\n";
            var dictionary = YamlSerializer.Deserialize<Dictionary<List<string>, string>>("? [a, b]\n: pair\n")!;
            using YamlDocument document = YamlDocument.Parse(yaml);
            string kinds = string.Join(", ", document.RootElement.EnumerateObject().Select(p => p.KeyKind));
            var node = YamlNode.Parse(yaml)!.AsObject();
            var jsonStream = new YamlSerializerOptions { DisallowImplicitCollectionKeys = true };
            int[] numbers = YamlSerializer.Deserialize<int[]>("[1, 2, 3]", jsonStream)!;
            // readme: end
            Check("collection-keys", $"dictionary key: [{string.Join(", ", dictionary.Keys.Single())}]\nkey kinds: {kinds}\n"
                + $"string keys: {node.Count}, collection keys: {node.CollectionKeys.Count}\nwithout implicit collection keys: {numbers.Length} numbers", defaultStyle: false);
        }

        [Fact]
        public async Task Documents()
        {
            // readme: documents text
            string yaml = "a: 1\n---\na: 2\n---\na: 3\n";
            var values = new List<int>();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            await foreach (var item in YamlSerializer.DeserializeAsyncEnumerable<Dictionary<string, int>>(stream, topLevelValues: true))
            {
                values.Add(item!["a"]);
            }

            var output = new MemoryStream();
            await YamlSerializer.SerializeAsyncEnumerable(output, Items(), topLevelValues: true);
            static async IAsyncEnumerable<object> Items()
            {
                yield return new { a = 1 };
                yield return new { a = 2 };
                await Task.CompletedTask;
            }
            // readme: end
            Check("documents", $"read: {string.Join(", ", values)}\nwritten:\n{Encoding.UTF8.GetString(output.ToArray())}", defaultStyle: true);
        }

        [Fact]
        public void Dom()
        {
            // readme: dom yaml
            YamlNode root = YamlNode.Parse("""
                name: web   # not kept
                ports: [80, 443]
                note: 'kept quoted'
                """)!;
            root["ports"]!.AsArray().Add(8080);
            root["replicas"] = 3;
            string edited = root.ToYamlString();
            // readme: end
            Check("dom", edited, defaultStyle: true);
        }

        [Fact]
        public void DomStyles()
        {
            // readme: dom-styles yaml
            YamlNode fromJson = YamlNode.Parse("""{"name": "web", "ports": [80, 443]}""")!;
            string asIs = fromJson.ToYamlString();
            string block = fromJson.ToYamlString(new YamlSerializerOptions { IgnoreSourceStyles = true });
            YamlObject created = new() { ["tags"] = new YamlArray("a", "b") };
            created["tags"]!.AsArray().CollectionStyle = YamlCollectionStyle.Flow;
            // readme: end
            Check("dom-styles", asIs + "\n" + block + "\n" + created.ToYamlString(), defaultStyle: true);
        }

        [Fact]
        public void Reader()
        {
            // readme: reader text
            var reader = new Utf8YamlReader("base: &b !point {x: 1}\ncopy: *b\ntext: 'quoted'\n"u8);
            var lines = new List<string>();
            while (reader.Read())
            {
                string anchor = reader.Anchor.IsEmpty ? "" : " &" + Encoding.UTF8.GetString(reader.Anchor.ToArray());
                string tag = reader.Tag.IsEmpty ? "" : " " + reader.GetExpandedTag();
                string text = reader.TokenType switch
                {
                    YamlTokenType.Alias => " *" + Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    YamlTokenType.String or YamlTokenType.PropertyName or YamlTokenType.Number => $" {reader.GetString()} ({reader.ScalarStyle})",
                    _ => "",
                };
                lines.Add($"{reader.TokenType}{anchor}{tag}{text}");
            }
            // readme: end
            Check("reader", string.Join("\n", lines), defaultStyle: false);
        }

        [Fact]
        public void Writer()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // readme: writer yaml
            using var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { NewLine = "\n" }))
            {
                writer.WriteStartObject();
                writer.WriteCommentValue("generated", false);
                writer.WritePropertyName("base");
                writer.WriteAnchor("b");
                writer.WriteTag("!point");
                writer.WriteStartObject();
                writer.WriteNumber("x", 1);
                writer.WriteEndObject();
                writer.WritePropertyName("copy");
                writer.WriteAliasValue("b");
                writer.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                writer.WriteString("text", "quoted");
                writer.WriteStartPropertyKey();
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray();
                writer.WriteStringValue("a");
                writer.WriteStringValue("b");
                writer.WriteEndArray();
                writer.WriteEndPropertyKey();
                writer.WriteStringValue("a collection key");
                writer.WriteEndObject();
            }
            string yaml = Encoding.UTF8.GetString(buffer.ToArray());
            // readme: end
            Check("writer", yaml, defaultStyle: true);
        }

        [Fact]
        public void Json()
        {
            // readme: json json
            string json = """
                {
                  "name": "web", # YAML comments are allowed
                  "ports": [80, 443,],
                }
                """;
            var value = YamlSerializer.Deserialize<Dictionary<string, object>>(json)!;
            string jsonOut = YamlSerializer.Serialize(value, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            // readme: end
            Check("json", jsonOut, defaultStyle: false);
        }

        private static string Describe(Func<object?> read)
        {
            try
            {
                return read()?.ToString() ?? "null";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message.Split('\n')[0];
            }
        }

        // A sample whose code needs the writer's block default (anchors read back, the writer's block-only members) runs
        // in leg Y only: leg YJ makes the default JSON-compatible through a test-only switch.
        private static bool BlockDefaultOnly() => GrammarModeInitializer.ExpectJsonCompatibleWriter;

        // Compares a sample's output with Readme/expected/name.txt, with LF line breaks. A sample whose output shows the
        // writer's default style is compared in leg Y only: leg YJ flips that default through a test-only switch.
        private static void Check(string name, string output, bool defaultStyle)
        {
            output = output.Replace("\r\n", "\n");
            string? record = Environment.GetEnvironmentVariable("STYML_README_RECORD");
            if (!string.IsNullOrEmpty(record))
            {
                Directory.CreateDirectory(Path.Combine(record, "expected"));
                File.WriteAllText(Path.Combine(record, "expected", name + ".txt"), output);
                return;
            }

            if (defaultStyle && GrammarModeInitializer.ExpectJsonCompatibleWriter)
            {
                return;
            }

            string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Readme", "expected", name + ".txt")).Replace("\r\n", "\n");
            Assert.Equal(expected, output);
        }
    }
}
