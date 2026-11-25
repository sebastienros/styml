// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/converters.md and guide/contracts.md.
    public partial class CustomizationSamples
    {
        // docs: converter-type
        // A temperature written as a scalar such as "21.5C" rather than as a mapping.
        public readonly record struct Temperature(double Celsius);

        public sealed class TemperatureConverter : YamlConverter<Temperature>
        {
            public override Temperature Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                string text = reader.GetString() ?? throw new YamlException("A temperature can't be null.");
                if (!text.EndsWith("C", StringComparison.Ordinal)
                    || !double.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double celsius))
                {
                    throw new YamlException($"'{text}' is not a temperature such as 21.5C.");
                }

                return new Temperature(celsius);
            }

            public override void Write(Utf8YamlWriter writer, Temperature value, YamlSerializerOptions options) =>
                writer.WriteStringValue(value.Celsius.ToString(CultureInfo.InvariantCulture) + "C");
        }
        // docs: end

        [Fact]
        public void Converter()
        {
            // docs: converter-use yaml
            var options = new YamlSerializerOptions { Converters = { new TemperatureConverter() } };
            var readings = new Dictionary<string, Temperature> { ["kitchen"] = new(21.5), ["attic"] = new(-3) };
            string yaml = YamlSerializer.Serialize(readings, options);
            var back = YamlSerializer.Deserialize<Dictionary<string, Temperature>>(yaml, options)!;
            string error = Describe(() => YamlSerializer.Deserialize<Temperature>("hot", options));
            // docs: end
            Assert.Equal(-3, back["attic"].Celsius);
            Check("converter-use", yaml + "\n# " + error + "\n");
        }

        // docs: converter-attribute
        public class Room
        {
            public string Name { get; set; } = "";

            [YamlConverter(typeof(TemperatureConverter))]
            public Temperature Target { get; set; }
        }
        // docs: end

        [Fact]
        public void ConverterAttribute()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            Assert.StartsWith("Name: lab\nTarget: 19C", YamlSerializer.Serialize(new Room { Name = "lab", Target = new(19) }, new YamlSerializerOptions { NewLine = "\n" }));
        }

        // docs: converter-yaml-type
        // A script written as a literal block scalar with a tag, whatever the writer's defaults.
        public record Script(string Shell, string Body);

        public sealed class ScriptConverter : YamlConverter<Script>
        {
            public override Script Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                // The tag is the shell: !bash, !pwsh. An untagged script is a shell script.
                string shell = reader.Tag.IsEmpty ? "sh" : reader.GetExpandedTag()!.TrimStart('!');
                return new Script(shell, reader.GetString() ?? "");
            }

            public override void Write(Utf8YamlWriter writer, Script value, YamlSerializerOptions options)
            {
                writer.WriteTag("!" + value.Shell);
                writer.WriteScalarStyle(YamlScalarStyle.Literal);
                writer.WriteStringValue(value.Body);
            }
        }
        // docs: end

        [Fact]
        public void YamlConverter()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // docs: converter-yaml yaml
            var options = new YamlSerializerOptions { Converters = { new ScriptConverter() } };
            var steps = new List<Script> { new("bash", "set -e\ndotnet test\n"), new("pwsh", "Write-Host 'done'\n") };
            string yaml = YamlSerializer.Serialize(steps, options);
            List<Script> back = YamlSerializer.Deserialize<List<Script>>(yaml, options)!;
            // docs: end
            Assert.Equal("pwsh", back[1].Shell);
            Assert.Equal(steps[0].Body, back[0].Body);
            Check("converter-yaml", yaml);
        }

        // IParsable<T> is .NET 7 and later: this sample is not compiled for .NET Framework.
#if NET
        // docs: converter-factory
        // One converter for every Range<T>: "1..5".
        public readonly record struct Range<T>(T Start, T End);

        public sealed class RangeConverterFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) =>
                typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Range<>);

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options) =>
                (YamlConverter)Activator.CreateInstance(typeof(RangeConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;

            private sealed class RangeConverter<T> : YamlConverter<Range<T>> where T : IParsable<T>
            {
                public override Range<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    string[] parts = reader.GetString()!.Split("..");
                    return new Range<T>(T.Parse(parts[0], CultureInfo.InvariantCulture), T.Parse(parts[1], CultureInfo.InvariantCulture));
                }

                public override void Write(Utf8YamlWriter writer, Range<T> value, YamlSerializerOptions options) =>
                    writer.WriteStringValue(FormattableString.Invariant($"{value.Start}..{value.End}"));
            }
        }
        // docs: end

        [Fact]
        public void Factory()
        {
            // docs: converter-factory-use yaml
            var options = new YamlSerializerOptions { Converters = { new RangeConverterFactory() } };
            var limits = new { Ports = new Range<int>(8000, 8099), Ratio = new Range<double>(0.5, 1.5) };
            string yaml = YamlSerializer.Serialize(limits, options);
            // docs: end
            Check("converter-factory-use", yaml);
        }
#endif

        // docs: contract-types
        public class Database
        {
            public string Host { get; set; } = "";

            public int Port { get; set; }

            public string ConnectionPassword { get; set; } = "";

            public string Notes { get; set; } = "";

            public List<string> Replicas { get; set; } = [];
        }
        // docs: end

        [Fact]
        public void Modifiers()
        {
            // docs: contract-modifiers yaml
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers = { HideSecrets, Document, SkipEmptyLists },
                },
            };

            var database = new Database { Host = "db.internal", Port = 5432, ConnectionPassword = "hunter2", Notes = "Primary.\nBacked up nightly.\n" };
            string yaml = YamlSerializer.Serialize(database, options);

            // Leaves out every property whose name contains "Password".
            static void HideSecrets(YamlTypeInfo typeInfo)
            {
                foreach (YamlPropertyInfo property in typeInfo.Properties.Where(p => p.Name.Contains("Password")).ToList())
                {
                    typeInfo.Properties.Remove(property);
                }
            }

            // Writes a comment above the port and the notes as a literal block: YAML's own contract metadata.
            static void Document(YamlTypeInfo typeInfo)
            {
                if (typeInfo.Type != typeof(Database))
                {
                    return;
                }

                foreach (YamlPropertyInfo property in typeInfo.Properties)
                {
                    if (property.Name == nameof(Database.Port))
                    {
                        property.Comment = "The PostgreSQL port.";
                    }
                    else if (property.Name == nameof(Database.Notes))
                    {
                        property.ScalarStyle = YamlScalarStyle.Literal;
                    }
                }
            }

            // Writes a list only when it has items.
            static void SkipEmptyLists(YamlTypeInfo typeInfo)
            {
                foreach (YamlPropertyInfo property in typeInfo.Properties.Where(p => p.PropertyType == typeof(List<string>)))
                {
                    property.ShouldSerialize = (_, value) => value is List<string> { Count: > 0 };
                }
            }
            // docs: end
            Check("contract-modifiers", yaml);
        }

        [Fact]
        public void PrivateFields()
        {
            // docs: contract-private yaml
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type != typeof(Session))
                            {
                                return;
                            }

                            // Adds the private fields of Session as properties, under their names without the underscore.
                            foreach (FieldInfo field in typeof(Session).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                            {
                                YamlPropertyInfo property = typeInfo.CreateYamlPropertyInfo(field.FieldType, field.Name.TrimStart('_'));
                                property.Get = field.GetValue;
                                property.Set = field.SetValue;
                                typeInfo.Properties.Add(property);
                            }
                        },
                    },
                },
            };

            string yaml = YamlSerializer.Serialize(new Session("ada", 3), options);
            Session back = YamlSerializer.Deserialize<Session>("user: bob\nattempts: 1\n", options)!;
            // docs: end
            Check("contract-private", yaml + "\n# " + back + "\n");
        }

        // docs: contract-private-type
        public class Session
        {
            private string _user;
            private int _attempts;

            public Session() : this("", 0) { }

            public Session(string user, int attempts) => (_user, _attempts) = (user, attempts);

            public override string ToString() => $"{_user}, {_attempts} attempts";
        }
        // docs: end
    }
}
