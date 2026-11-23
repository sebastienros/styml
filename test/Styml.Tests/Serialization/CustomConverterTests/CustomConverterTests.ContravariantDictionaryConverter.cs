// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        // Test class for a contravariant converter (IDictionary->Dictionary).
        private class ContravariantDictionaryConverter : YamlConverter<IDictionary<string, long>>
        {
            private long _offset;

            public ContravariantDictionaryConverter(long offset)
            {
                _offset = offset;
            }

            public override bool CanConvert(Type typeToConvert)
            {
                // For simplicity, just support Dictionary not IDictionary.
                return typeToConvert == typeof(Dictionary<string, long>);
            }

            public override IDictionary<string, long> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                var value = new Dictionary<string, long>();

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        return value;
                    }

                    if (reader.TokenType != YamlTokenType.PropertyName)
                    {
                        throw new YamlException();
                    }

                    string key = reader.GetString();

                    reader.Read();
                    if (reader.TokenType != YamlTokenType.Number)
                    {
                        throw new YamlException();
                    }

                    long longValue = reader.GetInt64() + _offset;

                    value.Add(key, longValue);
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, IDictionary<string, long> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (KeyValuePair<string, long> item in value)
                {
                    writer.WriteNumber(item.Key, item.Value - _offset);
                }

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomDictionaryConverterContravariant()
        {
            const string Yaml = """{"Key1":1,"Key2":2}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ContravariantDictionaryConverter(10));

            Dictionary<string, long> dictionary = YamlSerializer.Deserialize<Dictionary<string, long>>(Yaml, options);
            Assert.Equal(11, dictionary["Key1"]);
            Assert.Equal(12, dictionary["Key2"]);

            Assert.Equal(Yaml, YamlSerializer.Serialize(dictionary, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassHavingDictionaryFieldWhichUsingCustomConverterTest()
        {
            const string Yaml = """{"MyInt":32,"MyDictionary":{"Key1":1,"Key2":2},"MyString":"Hello"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ContravariantDictionaryConverter(10));

            ClassHavingDictionaryFieldWhichUsesCustomConverter dictionary = YamlSerializer.Deserialize<ClassHavingDictionaryFieldWhichUsesCustomConverter>(Yaml, options);
            Assert.Equal(11, dictionary.MyDictionary["Key1"]);
            Assert.Equal(12, dictionary.MyDictionary["Key2"]);
            Assert.Equal(32, dictionary.MyInt);
            Assert.Equal("Hello", dictionary.MyString);

            Assert.Equal(Yaml, YamlSerializer.Serialize(dictionary, options));
        }

        private class ClassHavingDictionaryFieldWhichUsesCustomConverter
        {
            public int MyInt { get; set; }
            public Dictionary<string, long> MyDictionary { get; set; }
            public string MyString { get; set; }
        }
    }
}
