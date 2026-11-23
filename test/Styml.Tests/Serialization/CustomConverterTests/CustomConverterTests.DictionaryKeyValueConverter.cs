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
        /// Demonstrates a <see cref="Dictionary{TKey, TValue}"> converter using a JSON array containing KeyValuePair objects.
        /// Sample JSON for <see cref="Dictionary{int, string}">: [{"Key":1,"Value":"One"},{"Key":2,"Value":"Two"}]
        /// </summary>
        internal sealed class DictionaryKeyValueConverter : YamlConverterFactory
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

                // Don't change semantics of Dictionary<string, TValue> which uses JSON properties (not array of KeyValuePairs).
                Type keyType = typeToConvert.GetGenericArguments()[0];
                if (keyType == typeof(string))
                {
                    return false;
                }

                return true;
            }

            public override YamlConverter CreateConverter(Type type, YamlSerializerOptions options)
            {
                Type keyType = type.GetGenericArguments()[0];
                Type valueType = type.GetGenericArguments()[1];

                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(DictionaryKeyValueConverterInner<,>).MakeGenericType(new Type[] { keyType, valueType }),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: new object[] { options },
                    culture: null);

                return converter;
            }

            private class DictionaryKeyValueConverterInner<TKey, TValue> : YamlConverter<Dictionary<TKey, TValue>>
            {
                private readonly YamlConverter<KeyValuePair<TKey, TValue>> _converter;

                public DictionaryKeyValueConverterInner(YamlSerializerOptions options)
                {
                    _converter = (YamlConverter<KeyValuePair<TKey, TValue>>)options.GetConverter(typeof(KeyValuePair<TKey, TValue>));

                    // KeyValuePair<> converter is built-in.
                    Debug.Assert(_converter != null);
                }

                public override Dictionary<TKey, TValue> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    if (reader.TokenType != YamlTokenType.StartArray)
                    {
                        throw new YamlException();
                    }

                    Dictionary<TKey, TValue> value = new Dictionary<TKey, TValue>();

                    while (reader.Read())
                    {
                        if (reader.TokenType == YamlTokenType.EndArray)
                        {
                            return value;
                        }

                        KeyValuePair<TKey, TValue> kv = _converter.Read(ref reader, typeof(KeyValuePair<TKey, TValue>), options);
                        value.Add(kv.Key, kv.Value);
                    }

                    throw new YamlException();
                }

                public override void Write(Utf8YamlWriter writer, Dictionary<TKey, TValue> value, YamlSerializerOptions options)
                {
                    writer.WriteStartArray();

                    foreach (KeyValuePair<TKey, TValue> kvp in value)
                    {
                        _converter.Write(writer, kvp, options);
                    }

                    writer.WriteEndArray();
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void IntStringKeyValuePairConverter()
        {
            const string yaml = """[{"Key":1,"Value":"One"},{"Key":2,"Value":"Two"}]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryKeyValueConverter());

            Dictionary<int, string> dictionary = YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, options);
            Assert.Equal("One", dictionary[1]);
            Assert.Equal("Two", dictionary[2]);

            string yamlSerialized = YamlSerializer.Serialize(dictionary, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NestedDictionaryConversion()
        {
            const string yaml = """[{"Key":1,"Value":[{"Key":10,"Value":11}]},{"Key":2,"Value":[{"Key":20,"Value":21}]}]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryKeyValueConverter());

            Dictionary<int, Dictionary<int, int>> dictionary = YamlSerializer.Deserialize<Dictionary<int, Dictionary<int, int>>>(yaml, options);
            Assert.Equal(11, dictionary[1][10]);
            Assert.Equal(21, dictionary[2][20]);

            string yamlSerialized = YamlSerializer.Serialize(dictionary, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        private class Entity
        {
            public string Value { get; set; }
        }

        private enum MyEnum
        {
            One = 1
        }

        private class ClassWithDictionaries
        {
            public Dictionary<bool, Entity> BoolKey { get; set; }
            public Dictionary<MyEnum, Entity> EnumKey { get; set; }
            public Dictionary<Guid, Entity> GuidKey { get; set; }
            public Dictionary<int, Entity> IntKey { get; set; }
            public Dictionary<float, Entity> FloatKey { get; set; }
            public Dictionary<double, Entity> DoubleKey { get; set; }
            public Dictionary<string, Entity> StringKey { get; set; }
        }

        [Fact]
        public static void AllPrimitivesConversion()
        {
            ClassWithDictionaries obj;
            Guid guid = Guid.NewGuid();

            void Verify()
            {
                Assert.Equal("test", obj.BoolKey[true].Value);
                Assert.Equal("test", obj.EnumKey[MyEnum.One].Value);
                Assert.Equal("test", obj.GuidKey[guid].Value);
                Assert.Equal("test", obj.IntKey[1].Value);
                Assert.Equal("test", obj.FloatKey[1.34f].Value);
                Assert.Equal("test", obj.DoubleKey[1.35].Value);
                Assert.Equal("test", obj.StringKey["key"].Value);
            }

            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryKeyValueConverter());

            obj = new ClassWithDictionaries
            {
                BoolKey = new Dictionary<bool, Entity> { [true] = new Entity { Value = "test" } },
                EnumKey = new Dictionary<MyEnum, Entity> { [MyEnum.One] = new Entity { Value = "test" } },
                GuidKey = new Dictionary<Guid, Entity> { [guid] = new Entity { Value = "test" } },
                DoubleKey = new Dictionary<double, Entity> { [1.35] = new Entity { Value = "test" } },
                FloatKey = new Dictionary<float, Entity> { [1.34f] = new Entity { Value = "test" } },
                IntKey = new Dictionary<int, Entity> { [1] = new Entity { Value = "test" } },

                // String is actually handled by built-in converter, not the custom converter.
                StringKey = new Dictionary<string, Entity> { ["key"] = new Entity { Value = "test" } },
            };

            // Verify baseline.
            Verify();

            string yaml = YamlSerializer.Serialize(obj, options);
            obj = YamlSerializer.Deserialize<ClassWithDictionaries>(yaml, options);

            // Verify.
            Verify();
        }

        [Fact]
        public static void EnumFail()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new DictionaryKeyValueConverter());
            options.Converters.Add(new YamlStringEnumConverter()); // Use string for Enum instead of int.

            // Baseline.
            Dictionary<MyEnum, int> dictionary = YamlSerializer.Deserialize<Dictionary<MyEnum, int>>("""[{"Key":"One","Value":100}]""", options);
            Assert.Equal(100, dictionary[MyEnum.One]);

            // Invalid JSON.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<MyEnum, int>>("{x}", options));

            // Invalid enum value.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<MyEnum, int>>("""[{"Key":"BAD","Value":100}]""", options));
        }
    }
}
