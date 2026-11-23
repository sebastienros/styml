// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates a <see cref="Dictionary{int, string}"> converter using a JSON array containing KeyValuePair objects.
        /// Sample JSON: [{"Key":1,"Value":"One"},{"Key":2,"Value":"Two"}]
        /// </summary>
        internal class DictionaryInt32StringKeyValueConverter : YamlConverter<Dictionary<int, string>>
        {
            private YamlConverter<KeyValuePair<int, string>> _intToStringConverter;

            public DictionaryInt32StringKeyValueConverter(YamlSerializerOptions options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                _intToStringConverter = (YamlConverter<KeyValuePair<int, string>>)options.GetConverter(typeof(KeyValuePair<int, string>));

                // KeyValuePair<> converter is built-in.
                Debug.Assert(_intToStringConverter != null);
            }

            public override Dictionary<int, string> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartArray)
                {
                    throw new YamlException();
                }

                var value = new Dictionary<int, string>();

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndArray)
                    {
                        return value;
                    }

                    KeyValuePair<int, string> kvpair = _intToStringConverter.Read(ref reader, typeof(KeyValuePair<int, string>), options);

                    value.Add(kvpair.Key, kvpair.Value);
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<int, string> value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (KeyValuePair<int, string> item in value)
                {
                    _intToStringConverter.Write(writer, item, options);
                }

                writer.WriteEndArray();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void VerifyDictionaryInt32StringKeyValueConverter()
        {
            const string yaml = """[{"Key":1,"Value":"ValueOne"},{"Key":2,"Value":"ValueTwo"}]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryInt32StringKeyValueConverter(options));

            Dictionary<int, string> dictionary = YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, options);
            Assert.Equal("ValueOne", dictionary[1]);
            Assert.Equal("ValueTwo", dictionary[2]);

            string yamlSerialized = YamlSerializer.Serialize(dictionary, options);
            Assert.Equal(yaml, yamlSerialized);
        }
    }
}
