// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates a <see cref="Dictionary{Guid, TValue}"> converter using a JSON object with property names representing keys.
        /// Sample JSON for <see cref="Dictionary{Guid, object}">: {"2E6E1787-1874-49BF-91F1-0F65CCB6C161":{"MyProperty":"myValue"}}
        /// Sample JSON for <see cref="Dictionary{Guid, int}">: {"2E6E1787-1874-49BF-91F1-0F65CCB6C161":42}
        /// </summary>
        internal sealed class DictionaryGuidConverter : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                if (!typeToConvert.IsGenericType)
                {
                    return false;
                }

                if (typeToConvert.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                {
                    return false;
                }

                return (typeToConvert.GetGenericArguments()[0] == typeof(Guid));
            }

            public override YamlConverter CreateConverter(Type type, YamlSerializerOptions options)
            {
                Type valueType = type.GetGenericArguments()[1];

                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(DictionaryGuidConverterInner<>).MakeGenericType(new Type[] { valueType }),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: new object[] { options },
                    culture: null);

                return converter;
            }

            private class DictionaryGuidConverterInner<TValue> : YamlConverter<Dictionary<Guid, TValue>>
            {
                private readonly YamlConverter<TValue> _valueConverter;

                public DictionaryGuidConverterInner(YamlSerializerOptions options)
                {
                    // For performance, use the existing converter if available.
                    _valueConverter = (YamlConverter<TValue>)options.GetConverter(typeof(TValue));
                }

                public override Dictionary<Guid, TValue> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                    {
                        throw new YamlException();
                    }

                    Dictionary<Guid, TValue> value = new Dictionary<Guid, TValue>();

                    while (reader.Read())
                    {
                        if (reader.TokenType == YamlTokenType.EndObject)
                        {
                            return value;
                        }

                        // Get the key.
                        if (reader.TokenType != YamlTokenType.PropertyName)
                        {
                            throw new YamlException();
                        }

                        string propertyName = reader.GetString();

                        // Parse guid in "D" format: 00000000-0000-0000-0000-00000000000
                        if (!Guid.TryParseExact(propertyName, format:"D", out Guid result))
                        {
                            throw new YamlException($"Unable to convert \"{propertyName}\" to a Guid.");
                        }

                        // Get the value.
                        TValue v;
                        if (_valueConverter != null)
                        {
                            reader.Read();
                            v = _valueConverter.Read(ref reader, typeof(TValue), options);
                        }
                        else
                        {
                            v = YamlSerializer.Deserialize<TValue>(ref reader, options);
                        }

                        // Add to dictionary.
                        value.Add(result, v);
                    }

                    throw new YamlException();
                }

                public override void Write(Utf8YamlWriter writer, Dictionary<Guid, TValue> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();

                    foreach (KeyValuePair<Guid, TValue> kvp in value)
                    {
                        writer.WritePropertyName(kvp.Key.ToString());

                        if (_valueConverter != null)
                        {
                            _valueConverter.Write(writer, kvp.Value, options);
                        }
                        else
                        {
                            YamlSerializer.Serialize(writer, kvp.Value, options);
                        }
                    }

                    writer.WriteEndObject();
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GuidToStringConverter()
        {
            Guid guid1 = Guid.NewGuid();
            Guid guid2 = Guid.NewGuid();

            string yaml = $"{{\"{guid1}\":\"One\",\"{guid2}\":\"Two\"}}";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryGuidConverter());

            Dictionary<Guid, string> dictionary = YamlSerializer.Deserialize<Dictionary<Guid, string>>(yaml, options);
            Assert.Equal("One", dictionary[guid1]);
            Assert.Equal("Two", dictionary[guid2]);

            string yamlSerialized = YamlSerializer.Serialize(dictionary, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        public static void GuidToEntityConverter()
        {
            Guid guid1 = Guid.NewGuid();
            Guid guid2 = Guid.NewGuid();

            Entity entity1 = new Entity();
            entity1.Value = "entity1";

            Entity entity2 = new Entity();
            entity2.Value = "entity2";

            var dictionary = new Dictionary<Guid, Entity>
            {
                [guid1] = entity1,
                [guid2] = entity2,
            };

            void Verify()
            {
                Assert.Equal("entity1", dictionary[guid1].Value);
                Assert.Equal("entity2", dictionary[guid2].Value);
            }

            // Verify baseline.
            Verify();

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryGuidConverter());

            string yaml = YamlSerializer.Serialize(dictionary, options);
            dictionary = YamlSerializer.Deserialize<Dictionary<Guid, Entity>>(yaml, options);

            // Verify.
            Verify();
        }
    }
}
