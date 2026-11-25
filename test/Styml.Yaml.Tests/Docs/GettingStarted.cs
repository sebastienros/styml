// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/getting-started.md, guide/system-text-json.md and guide/serialization.md.
    public partial class GettingStartedSamples
    {
        // docs: first-types
        public record Service(string Name, string Image, int[] Ports, Dictionary<string, string> Environment);
        // docs: end

        [Fact]
        public void FirstSerialize()
        {
            // docs: first-serialize yaml
            var service = new Service("web", "nginx:1.27", [80, 443], new() { ["TIER"] = "front", ["REPLICAS"] = "3" });
            string yaml = YamlSerializer.Serialize(service);
            // docs: end
            Check("first-serialize", yaml);
        }

        [Fact]
        public void FirstDeserialize()
        {
            // docs: first-deserialize text
            string yaml = """
                Name: api
                Image: example/api:2.4
                Ports: [8080]
                Environment:
                  LOG_LEVEL: debug
                """;
            Service service = YamlSerializer.Deserialize<Service>(yaml)!;
            Console.WriteLine($"{service.Name} on {service.Ports[0]}, {service.Environment.Count} variable");
            // docs: end
            Check("first-deserialize", $"{service.Name} on {service.Ports[0]}, {service.Environment.Count} variable\n", defaultStyle: false);
        }

        [Fact]
        public void FirstNode()
        {
            // docs: first-node yaml
            YamlNode config = YamlNode.Parse("""
                server:
                  port: 8080
                  hosts: [a.example, b.example]
                """)!;
            config["server"]!["port"] = 9090;
            config["server"]!["hosts"]!.AsArray().Add("c.example");
            string yaml = config.ToYamlString();
            // docs: end
            Check("first-node", yaml);
        }

        [Fact]
        public void SideBySide()
        {
            var service = new Service("web", "nginx:1.27", [80], new());
            // docs: stj-side-by-side
            // System.Text.Json
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string json = JsonSerializer.Serialize(service, jsonOptions);
            Service? fromJson = JsonSerializer.Deserialize<Service>(json, jsonOptions);

            // Styml
            var yamlOptions = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
            string yaml = YamlSerializer.Serialize(service, yamlOptions);
            Service? fromYaml = YamlSerializer.Deserialize<Service>(yaml, yamlOptions);
            // docs: end
            Assert.Equal(fromJson!.Name, fromYaml!.Name);
        }

        [Fact]
        public void JsonIsYaml()
        {
            // docs: json-is-yaml yaml
            var service = YamlSerializer.Deserialize<Service>("""
                {"Name": "web", "Image": "nginx:1.27", "Ports": [80, 443], "Environment": {"TIER": "front"}}
                """)!;
            string yaml = YamlSerializer.Serialize(service);
            string json = YamlSerializer.Serialize(service, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            // docs: end
            Check("json-is-yaml", yaml + "---\n" + json + "\n");
        }

        // docs: json-writer-type
        static void WriteYaml(Utf8YamlWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        WriteYaml(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        WriteYaml(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    // The number as written: 1.50 stays 1.50, and a big integer isn't rounded to a double.
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    writer.WriteBooleanValue(element.GetBoolean());
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }
        // docs: end

        [Fact]
        public void JsonWriter()
        {
            // docs: json-writer yaml
            using JsonDocument json = JsonDocument.Parse("""
                {"name": "web", "replicas": 3, "ratio": 1.50, "tags": ["front", "yes", "8080"], "script": "nginx -t\nnginx -s reload"}
                """);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { NewLine = "\n" }))
            {
                WriteYaml(writer, json.RootElement);
            }

            string yaml = Encoding.UTF8.GetString(buffer.ToArray());
            // docs: end
            Check("json-writer", yaml);
        }

        [Fact]
        public void JsonElementToYamlElement()
        {
            // docs: json-element text
            using JsonDocument json = JsonDocument.Parse("""
                {"name": "web", "ports": [80, 443], "tls": {"enabled": true}, "port": "8080", "port": "9090"}
                """);
            // JsonDocument keeps duplicate keys and finds the last one, where YAML rejects them by default.
            var options = new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            YamlElement element = YamlElement.Parse(json.RootElement.GetRawText(), options);

            YamlElement ports = element.GetProperty("ports");
            string line = $"{element.ValueKind}, {ports.GetArrayLength()} ports, last {ports[1].GetInt32()}, "
                + $"tls {element.GetProperty("tls").GetProperty("enabled").GetBoolean()}, "
                + $"port {element.GetProperty("port").GetString()} ({element.GetProperty("port").ValueKind})";
            // docs: end
            Check("json-element", line + "\n", defaultStyle: false);
            Assert.Equal(json.RootElement.GetProperty("port").GetString(), element.GetProperty("port").GetString());
        }

        // docs: reuse-options
        // Create the options once and reuse them: they cache the contracts of the types they serialize.
        private static readonly YamlSerializerOptions Options = new()
        {
            PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower,
            IndentSequences = true,
        };
        // docs: end

        [Fact]
        public void Defaults()
        {
            // docs: web-defaults text
            var options = new YamlSerializerOptions(YamlSerializerDefaults.Web);
            var line = $"{options.PropertyNamingPolicy == YamlNamingPolicy.CamelCase}, "
                + $"{options.PropertyNameCaseInsensitive}, {options.NumberHandling}";
            // docs: end
            Check("web-defaults", line + "\n", defaultStyle: false);
            Assert.NotNull(Options);
        }

        [Fact]
        public void Utf8()
        {
            var service = new Service("web", "nginx:1.27", [80], new() { ["TIER"] = "front" });
            // docs: utf8 text
            byte[] utf8 = YamlSerializer.SerializeToUtf8Bytes(service);
            Service back = YamlSerializer.Deserialize<Service>(utf8)!;

            YamlDocument document = YamlSerializer.SerializeToDocument(service);
            YamlElement element = YamlSerializer.SerializeToElement(service);
            YamlNode node = YamlSerializer.SerializeToNode(service)!;
            Service fromNode = node.Deserialize<Service>()!;
            // docs: end
            Check("utf8", $"{back.Name}, {document.RootElement.GetProperty("Image")}, {element.GetProperty("Ports")[0]}, {fromNode.Environment["TIER"]}\n");
        }

        [Fact]
        public async Task Streams()
        {
            var service = new Service("web", "nginx:1.27", [80], new());
            string path = Path.Combine(Path.GetTempPath(), "styml-docs-" + Guid.NewGuid().ToString("N") + ".yaml");
            try
            {
                // docs: streams text
                using (FileStream file = File.Create(path))
                {
                    await YamlSerializer.SerializeAsync(file, service);
                }

                using FileStream read = File.OpenRead(path);
                Service? back = await YamlSerializer.DeserializeAsync<Service>(read);
                // docs: end
                Check("streams", back!.Image + "\n", defaultStyle: false);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Errors()
        {
            // docs: errors text
            string yaml = """
                Name: web
                Ports: [80, http]
                """;
            try
            {
                YamlSerializer.Deserialize<Service>(yaml);
            }
            catch (YamlException e)
            {
                Console.WriteLine($"{e.Path}, line {e.LineNumber + 1}: {e.Message}");
            }
            // docs: end
            string output = "";
            try
            {
                YamlSerializer.Deserialize<Service>(yaml);
            }
            catch (YamlException e)
            {
                output = $"{e.Path}, line {e.LineNumber + 1}: {e.Message}\n";
            }

            Check("errors", output, defaultStyle: false);
        }

        [Fact]
        public void Types()
        {
            // docs: supported-types yaml
            var value = new
            {
                Date = new DateTimeOffset(2026, 11, 24, 9, 30, 0, TimeSpan.FromHours(1)),
                Time = new TimeSpan(1, 30, 0),
                Id = new Guid("8f2e5a3c-7b1d-4c6e-9a0f-1d2e3f4a5b6c"),
                Uri = new Uri("https://example.org"),
                Bytes = new byte[] { 1, 2, 3 },
                Nullable = (int?)null,
                Tuple = (Count: 3, Name: "three"),
                Set = new HashSet<string> { "a", "b" },
            };
            string yaml = YamlSerializer.Serialize(value, new YamlSerializerOptions { IncludeFields = true });
            // docs: end
            Check("supported-types", yaml);
        }
    }
}
