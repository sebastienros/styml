// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates a <see cref="Dictionary{int, string}"> converter using a JSON object with property names representing keys.
        /// Sample JSON: {"1":"ValueOne","2":"ValueTwo"}
        /// </summary>
        internal class DictionaryInt32StringConverter : YamlConverter<Dictionary<int, string>>
        {
            public override Dictionary<int, string> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                var value = new Dictionary<int, string>();

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        return value;
                    }

                    string keyAsString = reader.GetString();
                    if (!int.TryParse(keyAsString, out int keyAsInt))
                    {
                        throw new YamlException($"Unable to convert \"{keyAsString}\" to System.Int32.");
                    }

                    reader.Read();
                    string itemValue = reader.GetString();

                    value.Add(keyAsInt, itemValue);
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<int, string> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (KeyValuePair<int, string> item in value)
                {
                    writer.WriteString(item.Key.ToString(), item.Value);
                }

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void IntToStringDictionaryObjectConverter()
        {
            const string yaml = """{"1":"ValueOne","2":"ValueTwo"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryInt32StringConverter());

            Dictionary<int, string> dictionary = YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, options);
            Assert.Equal("ValueOne", dictionary[1]);
            Assert.Equal("ValueTwo", dictionary[2]);

            string yamlSerialized = YamlSerializer.Serialize(dictionary, options);
            Assert.Equal(yaml, yamlSerialized);
        }
    }
}
