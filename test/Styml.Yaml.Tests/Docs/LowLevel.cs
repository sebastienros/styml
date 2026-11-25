// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/dom.md and guide/reader-writer.md.
    public partial class LowLevelSamples
    {
        private const string Compose = """
            services:
              web:
                image: &image nginx:1.27
                ports: ["80:80"]
                command: !shell |
                  nginx -g 'daemon off;'
              admin:
                image: *image
                replicas: 2
            """;

        [Fact]
        public void Document()
        {
            // docs: dom-document text
            using YamlDocument document = YamlDocument.Parse(Compose);
            YamlElement services = document.RootElement.GetProperty("services");
            var lines = new List<string>();
            foreach (YamlProperty service in services.EnumerateObject())
            {
                YamlElement image = service.Value.GetProperty("image");
                string alias = image.IsAlias ? $" (alias of &{image.GetAnchor()})" : "";
                lines.Add($"{service.Name}: {image.GetString()}{alias}");
            }

            YamlElement command = services.GetProperty("web").GetProperty("command");
            lines.Add($"command: tag {command.GetTag()}, {command.ScalarStyle}, {command.GetString()!.TrimEnd()}");
            lines.Add($"ports: {services.GetProperty("web").GetProperty("ports").CollectionStyle}");
            bool scaled = services.GetProperty("admin").TryGetProperty("replicas", out YamlElement replicas) && replicas.GetInt32() > 1;
            lines.Add($"admin scaled: {scaled}");
            // docs: end
            Check("dom-document", string.Join("\n", lines) + "\n", defaultStyle: false);
        }

        [Fact]
        public void DocumentComments()
        {
            // docs: dom-document-comments yaml
            var options = new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Allow };
            using YamlDocument document = YamlDocument.Parse("""
                # Service settings
                name: web # the public name

                # Ports to expose
                ports: [80, 443]
                """, options);

            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream))
            {
                document.WriteTo(writer);
            }

            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            // docs: end
            Check("dom-document-comments", yaml);
        }

        [Fact]
        public void Node()
        {
            // docs: dom-node yaml
            YamlNode root = YamlNode.Parse(Compose)!;
            YamlObject web = root["services"]!["web"]!.AsObject();
            web["ports"]!.AsArray().Add("443:443");
            web["environment"] = new YamlObject { ["TIER"] = "front" };
            root["services"]!.AsObject().Remove("admin");
            string yaml = root.ToYamlString();
            // docs: end
            Check("dom-node", yaml);
        }

        [Fact]
        public void NodeComments()
        {
            // docs: dom-node-comments yaml
            YamlObject config = YamlNode.Parse("""
                # Service settings
                name: web # the public name
                ports: [80, 443]
                """, documentOptions: new() { CommentHandling = YamlCommentHandling.Allow })!.AsObject();

            config["name"] = "api"; // the entry keeps its comment
            config.SetComment("ports", YamlCommentPosition.Leading, "Ports to expose");
            config["replicas"] = 2;
            config.SetComment("replicas", YamlCommentPosition.Trailing, "one per zone");
            config.EndComment = "Generated";
            string yaml = config.ToYamlString();
            // docs: end
            Check("dom-node-comments", yaml);
        }

        [Fact]
        public void Create()
        {
            // docs: dom-create yaml
            var config = new YamlObject
            {
                ["name"] = "web",
                ["ports"] = new YamlArray(80, 443) { CollectionStyle = YamlCollectionStyle.Flow },
                ["limits"] = new YamlObject { ["cpu"] = 0.5, ["memory"] = "512Mi" },
            };
            config["limits"]!.Anchor = "limits";
            config["defaults"] = new YamlObject { ["inherit"] = true };
            config["defaults"]!.Tag = "!settings";
            string yaml = config.ToYamlString();
            int cpu = (int)(config["limits"]!["cpu"]!.GetValue<double>() * 1000);
            // docs: end
            Check("dom-create", yaml + "\n# " + cpu + "m\n");
        }

        [Fact]
        public void Styles()
        {
            // docs: dom-styles yaml
            YamlNode fromJson = YamlNode.Parse("""{"name": "web", "ports": [80, 443], "note": "kept as written"}""")!;
            string asIs = fromJson.ToYamlString();
            string block = fromJson.ToYamlString(new YamlSerializerOptions { IgnoreSourceStyles = true });
            // docs: end
            Check("dom-styles", asIs + "---\n" + block);
        }

        [Fact]
        public void Typed()
        {
            // docs: dom-typed text
            YamlNode node = YamlNode.Parse("replicas: 3\nratio: 0.75\nenabled: yes\nname: '3'\n")!;
            int replicas = node["replicas"]!.GetValue<int>();
            double ratio = (double)node["ratio"]!;
            string enabled = node["enabled"]!.GetValueKind().ToString();
            string name = node["name"]!.GetValue<string>();
            Service service = node.Deserialize<Service>(new YamlSerializerOptions { PropertyNameCaseInsensitive = true })!;
            // docs: end
            Check("dom-typed", $"{replicas}, {ratio.ToString(Globalization.CultureInfo.InvariantCulture)}, enabled is {enabled}, name '{name}', {service}\n", defaultStyle: false);
        }

        // docs: dom-typed-types
        public record Service(int Replicas, string Name);
        // docs: end

        [Fact]
        public void Reader()
        {
            // docs: reader text
            byte[] utf8 = Encoding.UTF8.GetBytes("""
                base: &b !point {x: 1, y: 2}
                copy: *b
                note: >
                  folded
                  text
                """);
            var reader = new Utf8YamlReader(utf8);
            var lines = new List<string>();
            while (reader.Read())
            {
                string indent = new(' ', reader.CurrentDepth * 2);
                string anchor = reader.Anchor.IsEmpty ? "" : $" &{Encoding.UTF8.GetString(reader.Anchor.ToArray())}";
                string tag = reader.Tag.IsEmpty ? "" : $" {reader.GetExpandedTag()}";
                string value = reader.TokenType switch
                {
                    YamlTokenType.PropertyName or YamlTokenType.String => $" {reader.GetString()!.Replace("\n", "\\n")} ({reader.ScalarStyle})",
                    YamlTokenType.Number => $" {reader.GetInt32()}",
                    YamlTokenType.Alias => $" *{Encoding.UTF8.GetString(reader.ValueSpan.ToArray())}",
                    YamlTokenType.StartObject or YamlTokenType.StartArray => reader.IsFlow ? " (flow)" : " (block)",
                    _ => "",
                };
                lines.Add($"{indent}{reader.TokenType}{anchor}{tag}{value}");
            }
            // docs: end
            Check("reader", string.Join("\n", lines) + "\n", defaultStyle: false);
        }

        [Fact]
        public void Writer()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // docs: writer yaml
            using var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { NewLine = "\n" }))
            {
                writer.WriteDirective("%YAML 1.2");
                writer.WriteDocumentStart();
                writer.WriteCommentValue("generated, do not edit", false);
                writer.WriteStartObject();

                writer.WritePropertyName("defaults");
                writer.WriteAnchor("defaults");
                writer.WriteStartObject();
                writer.WriteString("image", "nginx:1.27");
                writer.WriteEndObject();

                writer.WritePropertyName("web");
                writer.WriteTag("!service");
                writer.WriteStartObject();
                writer.WritePropertyName("base");
                writer.WriteAliasValue("defaults");
                writer.WritePropertyName("ports");
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray();
                writer.WriteNumberValue(80);
                writer.WriteNumberValue(443);
                writer.WriteEndArray();
                writer.WriteScalarStyle(YamlScalarStyle.Literal);
                writer.WriteString("script", "nginx -t\nnginx -s reload\n");
                writer.WriteEndObject();

                writer.WriteStartPropertyKey();
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray();
                writer.WriteStringValue("eu");
                writer.WriteStringValue("us");
                writer.WriteEndArray();
                writer.WriteEndPropertyKey();
                writer.WriteStringValue("two regions");

                writer.WriteEndObject();
                writer.WriteDocumentEnd();
            }

            string yaml = Encoding.UTF8.GetString(buffer.ToArray());
            // docs: end
            Check("writer", yaml, defaultStyle: false);
        }
    }
}
