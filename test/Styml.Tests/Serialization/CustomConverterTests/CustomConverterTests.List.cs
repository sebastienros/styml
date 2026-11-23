// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        // A List{T} converter that used CreateConverter().
        private class ListConverter : YamlConverterFactory
        {
            int _offset;

            public ListConverter(int offset)
            {
                _offset = offset;
            }

            public override bool CanConvert(Type typeToConvert)
            {
                if (!typeToConvert.IsGenericType)
                    return false;

                Type generic = typeToConvert.GetGenericTypeDefinition();
                if (generic != typeof(List<>))
                    return false;

                Type arg = typeToConvert.GetGenericArguments()[0];
                return arg == typeof(int) ||
                    arg == typeof(long);
            }

            public override YamlConverter CreateConverter(Type type, YamlSerializerOptions options)
            {
                Type elementType = type.GetGenericArguments()[0];

                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(ListConverter<>).MakeGenericType(elementType),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    new object[] { _offset },
                    culture: null);

                return converter;
            }
        }

        // Demonstrates List<T>; Adds offset to each integer or long to verify converter ran.
        private class ListConverter<T> : YamlConverter<List<T>>
        {
            private int _offset;
            public ListConverter(int offset)
            {
                _offset = offset;
            }

            public override List<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartArray)
                {
                    throw new YamlException();
                }

                var value = new List<T>();

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndArray)
                    {
                        return value;
                    }

                    if (reader.TokenType != YamlTokenType.Number)
                    {
                        throw new YamlException();
                    }

                    if (typeof(T) == typeof(int))
                    {
                        int element = reader.GetInt32();
                        IList list = value;
                        list.Add(element + _offset);
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        long element = reader.GetInt64();
                        IList list = value;
                        list.Add(element + _offset);
                    }
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, List<T> value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (T item in value)
                {
                    if (item is int)
                    {
                        writer.WriteNumberValue((int)(object)item - _offset);
                    }
                    else if (item is long)
                    {
                        writer.WriteNumberValue((long)(object)item - _offset);
                    }
                    else
                    {
                        Assert.Fail();
                    }
                }

                writer.WriteEndArray();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ListConverterOpenGeneric()
        {
            const string yaml = "[1,2,3]";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ListConverter(10));

            {
                List<int> list = YamlSerializer.Deserialize<List<int>>(yaml, options);
                Assert.Equal(11, list[0]);
                Assert.Equal(12, list[1]);
                Assert.Equal(13, list[2]);

                string yamlSerialized = YamlSerializer.Serialize(list, options);
                Assert.Equal(yaml, yamlSerialized);
            }

            {
                List<long> list = YamlSerializer.Deserialize<List<long>>(yaml, options);
                Assert.Equal(11, list[0]);
                Assert.Equal(12, list[1]);
                Assert.Equal(13, list[2]);

                string yamlSerialized = YamlSerializer.Serialize(list, options);
                Assert.Equal(yaml, yamlSerialized);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ListConverterClosedGeneric()
        {
            const string yaml = "[1,2,3]";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ListConverter<int>(10));

            List<int> list = YamlSerializer.Deserialize<List<int>>(yaml, options);
            Assert.Equal(11, list[0]);
            Assert.Equal(12, list[1]);
            Assert.Equal(13, list[2]);

            string yamlSerialized = YamlSerializer.Serialize(list, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        /// <summary>
        /// Demonstrates polymorphic IList converter.
        /// </summary>
        private class IListConverter : YamlConverter<IList>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(IList).IsAssignableFrom(typeToConvert);
            }

            public override IList Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartArray)
                {
                    throw new YamlException();
                }

                var value = new List<int>();

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndArray)
                    {
                        return value;
                    }

                    int element = reader.GetInt32();
                    value.Add(element + 10);
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, IList value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (int item in value)
                {
                    writer.WriteNumberValue(item - 10);
                }

                writer.WriteEndArray();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ListConverterPolymorphic()
        {
            const string yaml = "[1,2,3]";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new IListConverter());

            IList list = YamlSerializer.Deserialize<IList>(yaml, options);
            Assert.Equal(11, list[0]);
            Assert.Equal(12, list[1]);
            Assert.Equal(13, list[2]);

            List<int> contraVariantList = YamlSerializer.Deserialize<List<int>>(yaml, options);
            Assert.Equal(11, contraVariantList[0]);
            Assert.Equal(12, contraVariantList[1]);
            Assert.Equal(13, contraVariantList[2]);

            string yamlSerialized = YamlSerializer.Serialize(list, options);
            Assert.Equal(yaml, yamlSerialized);

            yamlSerialized = YamlSerializer.Serialize(contraVariantList, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        private class MyModelWithConverterAttributes
        {
            [YamlConverter(typeof(ListToStringElementsConverter))]
            public List<string> ItemsList { get; set; }

            [YamlConverter(typeof(ListToArrayElementsConverter))]
            public string[] ItemsArray { get; set; }

            [YamlConverter(typeof(ListToDictionaryElementsConverter))]
            public Dictionary<string, string> ItemsDictionary { get; set; }
        }

        private class MyModelWithNoConverterAttributes
        {
            public List<string> ItemsList { get; set; }
            public string[] ItemsArray { get; set; }
            public Dictionary<string, string> ItemsDictionary { get; set; }
        }

        /// <summary>
        /// This converter coerces JSON array items into <see cref="List{string}"/> when deserializing.
        /// </summary>
        class ListToStringElementsConverter : YamlConverter<List<string>>
        {
            public override List<string> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                using (var doc = YamlDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.EnumerateArray().Select(e => e.ToString()).ToList();
                }
            }

            public override void Write(Utf8YamlWriter writer, List<string> value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value, options);
            }
        }

        /// <summary>
        /// This converter coerces JSON array items into <see cref="string[]"/> when deserializing.
        /// </summary>
        class ListToArrayElementsConverter : YamlConverter<string[]>
        {
            public override string[] Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                using (var doc = YamlDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.EnumerateArray().Select(e => e.ToString()).ToArray();
                }
            }

            public override void Write(Utf8YamlWriter writer, string[] value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value, options);
            }
        }

        /// <summary>
        /// This converter coerces JSON property values into <see cref="Dictionary{string, string}"> when deserializing.
        /// </summary>
        class ListToDictionaryElementsConverter : YamlConverter<Dictionary<string, string>>
        {
            public override Dictionary<string, string> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                var dictionary = new Dictionary<string, string>();

                using (var doc = YamlDocument.ParseValue(ref reader))
                {
                    foreach (YamlProperty property in doc.RootElement.EnumerateObject())
                    {
                        dictionary.Add(property.Name, property.Value.ToString());
                    }
                }

                return dictionary;
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<string, string> value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value, options);
            }
        }
    }
}
