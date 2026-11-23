// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates a <see cref="Dictionary{TKey, TValue}"> converter where TKey is an <see cref="Enum"/> with the string
        /// version of the Enum is used.
        /// Sample JSON for <see cref="Dictionary{MyEnum, object}">: {"MyEnumValue":{"MyProperty":"myValue"}}
        /// Sample JSON for <see cref="Dictionary{MyEnum, int}">: {"MyEnumValue":42}
        /// </summary>
        /// <remarks>
        /// A case-insensitive parse is performed for the Enum value.
        /// A <see cref="YamlException"/> is thrown when deserializing if the Enum value is not found.
        /// </remarks>
        internal sealed class DictionaryEnumConverter : YamlConverterFactory
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

                return typeToConvert.GetGenericArguments()[0].IsEnum;
            }

            public override YamlConverter CreateConverter(Type type, YamlSerializerOptions options)
            {
                Type keyType = type.GetGenericArguments()[0];
                Type valueType = type.GetGenericArguments()[1];

                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(DictionaryEnumConverterInner<,>).MakeGenericType(new Type[] { keyType, valueType }),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: new object[] { options },
                    culture: null);

                return converter;
            }

            private class DictionaryEnumConverterInner<TKey, TValue> : YamlConverter<Dictionary<TKey, TValue>> where TKey : struct, Enum
            {
                private readonly YamlConverter<TValue> _valueConverter;
                private Type _keyType;
                private Type _valueType;

                public DictionaryEnumConverterInner(YamlSerializerOptions options)
                {
                    // For performance, use the existing converter if available.
                    _valueConverter = (YamlConverter<TValue>)options.GetConverter(typeof(TValue));

                    // Cache the key and value types.
                    _keyType = typeof(TKey);
                    _valueType = typeof(TValue);
                }

                public override Dictionary<TKey, TValue> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                    {
                        throw new YamlException();
                    }

                    Dictionary<TKey, TValue> value = new Dictionary<TKey, TValue>();

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

                        // For performance, parse with ignoreCase:false first.
                        if (!Enum.TryParse(propertyName, ignoreCase: false, out TKey key) &&
                            !Enum.TryParse(propertyName, ignoreCase: true, out key))
                        {
                            throw new YamlException($"Unable to convert \"{propertyName}\" to Enum \"{_keyType}\".");
                        }

                        // Get the value.
                        TValue v;
                        if (_valueConverter != null)
                        {
                            reader.Read();
                            v = _valueConverter.Read(ref reader, _valueType, options);
                        }
                        else
                        {
                            v = YamlSerializer.Deserialize<TValue>(ref reader, options);
                        }

                        // Add to dictionary.
                        value.Add(key, v);
                    }

                    throw new YamlException();
                }

                public override void Write(Utf8YamlWriter writer, Dictionary<TKey, TValue> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();

                    foreach (KeyValuePair<TKey, TValue> kvp in value)
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
        public static void VerifyDictionaryEnumToIntConverter()
        {
            const string Yaml = """{"One":1}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryEnumConverter());

            Dictionary<MyEnum, int> obj = YamlSerializer.Deserialize<Dictionary<MyEnum, int>>(Yaml, options);
            Assert.Equal(1, obj.Count);
            Assert.Equal(1, obj[MyEnum.One]);

            string yamlRoundTripped = YamlSerializer.Serialize(obj, options);
            Assert.Equal(Yaml, yamlRoundTripped);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void VerifyDictionaryEnumToIntConverterCaseInsensitive()
        {
            const string Yaml = """{"one":1}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryEnumConverter());

            Dictionary<MyEnum, int> obj = YamlSerializer.Deserialize<Dictionary<MyEnum, int>>(Yaml, options);
            Assert.Equal(1, obj.Count);
            Assert.Equal(1, obj[MyEnum.One]);

            // The serialized JSON is cased per the enum's actual vales.
            string yamlRoundTripped = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"One":1}""", yamlRoundTripped);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void VerifyDictionaryEnumToObjectConverter()
        {
            const string Yaml = """{"One":{"Value":"test"}}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryEnumConverter());

            Dictionary<MyEnum, Entity> obj = YamlSerializer.Deserialize<Dictionary<MyEnum, Entity>>(Yaml, options);
            Assert.Equal(1, obj.Count);
            Assert.Equal("test", obj[MyEnum.One].Value);

            string yamlRoundTripped = YamlSerializer.Serialize(obj, options);
            Assert.Equal(Yaml, yamlRoundTripped);
        }

        [Fact]
        public static void VerifyDictionaryEnumConverterFail()
        {
            const string Yaml = """{"BAD":2}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryEnumConverter());

            YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<MyEnum, int>>(Yaml, options));
            Assert.Contains($"Unable to convert \"BAD\" to Enum \"{typeof(MyEnum)}\".", ex.Message);
        }
    }
}
