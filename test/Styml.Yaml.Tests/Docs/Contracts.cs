// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/source-generation.md, guide/properties.md and guide/deserialization.md.
    public partial class ContractSamples
    {
        // docs: sourcegen-context
        public record Deployment(string Name, int Replicas, List<string> Hosts, DeploymentKind Kind);

        public enum DeploymentKind { Rolling, BlueGreen }

        [YamlSerializable(typeof(Deployment))]
        [YamlSourceGenerationOptions(
            PropertyNamingPolicy = YamlKnownNamingPolicy.SnakeCaseLower,
            UseStringEnumConverter = true,
            IndentSequences = true,
            MappingOrder = YamlMappingOrder.Sorted)]
        public partial class DeployContext : YamlSerializerContext;
        // docs: end

        [Fact]
        public void SourceGenerated()
        {
            // docs: sourcegen-use yaml
            var deployment = new Deployment("web", 3, ["a.example", "b.example"], DeploymentKind.BlueGreen);
            string yaml = YamlSerializer.Serialize(deployment, DeployContext.Default.Deployment);
            Deployment back = YamlSerializer.Deserialize(yaml, DeployContext.Default.Deployment)!;
            // docs: end
            Assert.Equal(deployment.Hosts, back.Hosts);
            Check("sourcegen-use", yaml);
        }

        [Fact]
        public void SourceGeneratedOptions()
        {
            var deployment = new Deployment("web", 3, ["a.example"], DeploymentKind.Rolling);
            // docs: sourcegen-options yaml
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = DeployContext.Default,
                WriteStyle = YamlWriterStyle.JsonCompatible,
            };
            string json = YamlSerializer.Serialize(deployment, options);
            // docs: end
            Check("sourcegen-options", json + "\n", defaultStyle: false);
        }

        [Fact]
        public void Chain()
        {
            // docs: sourcegen-chain text
            var options = new YamlSerializerOptions
            {
                TypeInfoResolverChain = { DeployContext.Default, new DefaultYamlTypeInfoResolver() },
            };
            string kinds = string.Join(", ", new[] { typeof(Deployment), typeof(Uri) }
                .Select(type => $"{type.Name}: {options.GetTypeInfo(type).Kind}"));
            // docs: end
            Check("sourcegen-chain", kinds + "\n", defaultStyle: false);
        }

        // docs: naming-types
        public class Endpoint
        {
            public string HostName { get; set; } = "";

            [YamlPropertyName("tcp-port")]
            public int Port { get; set; }

            public Protocol Protocol { get; set; }

            public Dictionary<string, string> RequestHeaders { get; set; } = new();
        }

        public enum Protocol { Http, [YamlStringEnumMemberName("h2")] Http2 }
        // docs: end

        [Fact]
        public void Naming()
        {
            var endpoint = new Endpoint { HostName = "api.example", Port = 443, Protocol = Protocol.Http2, RequestHeaders = { ["X-Trace"] = "on" } };
            // docs: naming yaml
            var options = new YamlSerializerOptions
            {
                PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower,
                DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseUpper,
                Converters = { new YamlStringEnumConverter() },
            };
            string yaml = YamlSerializer.Serialize(endpoint, options);
            // docs: end
            Check("naming", yaml);
        }

        // docs: ignore-types
        public class Account
        {
            public string User { get; set; } = "";

            [YamlIgnore]
            public string Password { get; set; } = "";

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public int FailedLogins { get; set; }

            public string? Email { get; set; }

            public List<string> Roles { get; set; } = [];
        }
        // docs: end

        [Fact]
        public void Ignore()
        {
            // docs: ignore yaml
            var account = new Account { User = "ada", Password = "secret" };
            string all = YamlSerializer.Serialize(account);
            string compact = YamlSerializer.Serialize(account, new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection,
            });
            // docs: end
            Check("ignore", all + "---\n" + compact);
        }

        // docs: fields-types
        public class Counter
        {
            public int Count;

            [YamlInclude]
            internal string Label { get; set; } = "hits";

            [YamlPropertyOrder(-1)]
            public string Id { get; set; } = "c1";
        }
        // docs: end

        [Fact]
        public void Fields()
        {
            // docs: fields yaml
            string yaml = YamlSerializer.Serialize(new Counter { Count = 7 }, new YamlSerializerOptions { IncludeFields = true });
            // docs: end
            Check("fields", yaml);
        }

        [Fact]
        public void Numbers()
        {
            // docs: numbers yaml
            var options = new YamlSerializerOptions
            {
                NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowNamedFloatingPointLiterals,
            };
            string yaml = YamlSerializer.Serialize(new { Port = 8080, Ratio = double.PositiveInfinity }, options);
            var back = YamlSerializer.Deserialize<Dictionary<string, double>>("Port: '8080'\nRatio: .inf\n",
                new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString })!;
            string read = $"read: {back["Port"]}, {double.IsPositiveInfinity(back["Ratio"])}";
            // docs: end
            Check("numbers", yaml + "# " + read + "\n");
        }

        // docs: required-types
        public class Job
        {
            [YamlRequired]
            public string Name { get; set; } = "";

            public required string Image { get; set; }

            public string? Schedule { get; set; }
        }
        // docs: end

        [Fact]
        public void Required()
        {
            // docs: required text
            string missing = Describe(() => YamlSerializer.Deserialize<Job>("Name: backup\n"));
            // docs: end
            Check("required", missing + "\n", defaultStyle: false);
        }

        // docs: nullable-types
        public record Volume(string Name, string Path, string? Description);
        // docs: end

        [Fact]
        public void Nullable()
        {
            // docs: nullable text
            string yaml = "Name: data\nPath: null\n";
            string lax = Describe(() => YamlSerializer.Deserialize<Volume>(yaml));
            string strict = Describe(() => YamlSerializer.Deserialize<Volume>(yaml, new YamlSerializerOptions
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true,
            }));
            // docs: end
            Check("nullable", "lax: " + lax + "\nstrict: " + strict + "\n", defaultStyle: false);
        }

        // docs: unmapped-types
        [YamlUnmappedMemberHandling(YamlUnmappedMemberHandling.Disallow)]
        public class Probe
        {
            public string Path { get; set; } = "/";

            public int Interval { get; set; }
        }
        // docs: end

        [Fact]
        public void Unmapped()
        {
            // docs: unmapped text
            string typo = Describe(() => YamlSerializer.Deserialize<Probe>("Path: /health\nIntervall: 30\n"));
            // docs: end
            Check("unmapped", typo + "\n", defaultStyle: false);
        }

        // docs: overflow-types
        public class Manifest
        {
            public string Kind { get; set; } = "";

            [YamlExtensionData]
            public Dictionary<string, YamlElement> Extra { get; set; } = new();
        }
        // docs: end

        [Fact]
        public void Overflow()
        {
            // docs: overflow yaml
            var manifest = YamlSerializer.Deserialize<Manifest>("""
                Kind: Service
                metadata:
                  name: web
                spec: {ports: [80]}
                """)!;
            string keys = string.Join(", ", manifest.Extra.Keys);
            manifest.Kind = "Deployment";
            string yaml = YamlSerializer.Serialize(manifest);
            // docs: end
            Check("overflow", "# extra: " + keys + "\n" + yaml);
        }

        // docs: immutable-types
        public class Point
        {
            [YamlConstructor]
            public Point(double x, double y) => (X, Y) = (x, y);

            public Point() : this(0, 0) { }

            public double X { get; }

            public double Y { get; }
        }

        public record Size(double Width, double Height = 1);
        // docs: end

        [Fact]
        public void Immutable()
        {
            // docs: immutable text
            Point point = YamlSerializer.Deserialize<Point>("X: 3\nY: 4\n")!;
            Size size = YamlSerializer.Deserialize<Size>("Width: 2\n")!;
            // docs: end
            Check("immutable", $"({point.X}, {point.Y}), {size}\n", defaultStyle: false);
        }

        // docs: populate-types
        public class Settings
        {
            public List<string> Hosts { get; } = ["localhost"];

            [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
            public Dictionary<string, int> Limits { get; set; } = new() { ["cpu"] = 1 };
        }
        // docs: end

        [Fact]
        public void Populate()
        {
            // docs: populate text
            string yaml = "Hosts: [a.example]\nLimits: {memory: 512}\n";
            Settings replaced = YamlSerializer.Deserialize<Settings>(yaml)!;
            Settings populated = YamlSerializer.Deserialize<Settings>(yaml, new YamlSerializerOptions
            {
                PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate,
            })!;
            // docs: end
            Check("populate", $"default: [{string.Join(", ", replaced.Hosts)}] {{{string.Join(", ", replaced.Limits.Select(p => p.Key + ": " + p.Value))}}}\n"
                + $"populate: [{string.Join(", ", populated.Hosts)}] {{{string.Join(", ", populated.Limits.Select(p => p.Key + ": " + p.Value))}}}\n", defaultStyle: false);
        }

        [Fact]
        public void CaseInsensitive()
        {
            // docs: case-insensitive text
            var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true };
            Probe probe = YamlSerializer.Deserialize<Probe>("path: /ready\nINTERVAL: 10\n", options)!;
            // docs: end
            Check("case-insensitive", $"{probe.Path} every {probe.Interval}s\n", defaultStyle: false);
        }

        [Fact]
        public void Schema()
        {
            // docs: schema-export yaml
            var options = new YamlSerializerOptions(YamlSerializerOptions.Default) { RespectNullableAnnotations = true };
            YamlNode schema = YamlSchemaExporter.GetYamlSchemaAsNode(options, typeof(Volume));
            string yaml = schema.ToYamlString();
            // docs: end
            Check("schema-export", yaml);
        }
    }
}
