// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomDerivedTypeConverter()
        {
            string yaml =
                """
                        {
                                            "ListWrapper": [1, 2, 3],
                                            "List": [4, 5, 6],
                                            "DictionaryWrapper": {"key": 1},
                                            "Dictionary": {"key": 2}
                                        }
                    """;

            // Without converters, we deserialize as is.
            DerivedTypesWrapper wrapper = YamlSerializer.Deserialize<DerivedTypesWrapper>(yaml);
            int expected = 1;
            foreach (int value in wrapper.ListWrapper)
            {
                Assert.Equal(expected++, value);
            }
            foreach (int value in wrapper.List)
            {
                Assert.Equal(expected++, value);
            }
            Assert.Equal(1, wrapper.DictionaryWrapper["key"]);
            Assert.Equal(2, wrapper.Dictionary["key"]);

            string serialized = YamlSerializer.Serialize(wrapper);
            Assert.Contains("""
                "ListWrapper":[1,2,3]
                """, serialized);
            Assert.Contains("""
                "List":[4,5,6]
                """, serialized);
            Assert.Contains("""
                "DictionaryWrapper":{"key":1}
                """, serialized);
            Assert.Contains("""
                "Dictionary":{"key":2}
                """, serialized);

            // With converters, we expect no values in the wrappers per converters' implementation.
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.Converters.Add(new ListWrapperConverter());
            options.Converters.Add(new DictionaryWrapperConverter());

            DerivedTypesWrapper customWrapper = YamlSerializer.Deserialize<DerivedTypesWrapper>(yaml, options);
            Assert.Null(customWrapper.ListWrapper);
            expected = 4;
            foreach (int value in customWrapper.List)
            {
                Assert.Equal(expected++, value);
            }
            Assert.Null(customWrapper.DictionaryWrapper);
            Assert.Equal(2, customWrapper.Dictionary["key"]);

            // Clear metadata for serialize.
            options = new YamlSerializerOptions();
            options.Converters.Add(new ListWrapperConverter());
            options.Converters.Add(new DictionaryWrapperConverter());

            serialized = YamlSerializer.Serialize(wrapper, options);
            Assert.Contains("""
                "ListWrapper":[]
                """, serialized);
            Assert.Contains("""
                "List":[4,5,6]
                """, serialized);
            Assert.Contains("""
                "DictionaryWrapper":{}
                """, serialized);
            Assert.Contains("""
                "Dictionary":{"key":2}
                """, serialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomUnsupportedDictionaryConverter()
        {
            string yaml = """{"DictionaryWrapper": {"1": 1}}""";

            UnsupportedDerivedTypesWrapper_Dictionary wrapper = new UnsupportedDerivedTypesWrapper_Dictionary
            {
                DictionaryWrapper = new UnsupportedDictionaryWrapper()
            };
            wrapper.DictionaryWrapper[new int[,] { }] = 1;

            // Without converter, we throw.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<UnsupportedDerivedTypesWrapper_Dictionary>(yaml));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(wrapper));

            // With converter, we expect no values in the wrapper per converter's implementation.
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.Converters.Add(new UnsupportedDictionaryWrapperConverter());

            UnsupportedDerivedTypesWrapper_Dictionary customWrapper = YamlSerializer.Deserialize<UnsupportedDerivedTypesWrapper_Dictionary>(yaml, options);
            Assert.Null(customWrapper.DictionaryWrapper);

            // Clear metadata for serialize.
            options = new YamlSerializerOptions();
            options.Converters.Add(new UnsupportedDictionaryWrapperConverter());
            Assert.Equal("""{"DictionaryWrapper":{}}""", YamlSerializer.Serialize(wrapper, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomUnsupportedIEnumerableConverter()
        {
            string yaml = """{"IEnumerableWrapper": ["1", "2", "3"]}""";

            UnsupportedDerivedTypesWrapper_IEnumerable wrapper = new UnsupportedDerivedTypesWrapper_IEnumerable
            {
                IEnumerableWrapper = new StringIEnumerableWrapper(new List<string> { "1", "2", "3" }),
            };

            // Without converter, we throw on deserialize.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<UnsupportedDerivedTypesWrapper_IEnumerable>(yaml));
            // Without converter, we serialize as is.
            Assert.Equal("""{"IEnumerableWrapper":["1","2","3"]}""", YamlSerializer.Serialize(wrapper));

            // With converter, we expect no values in the wrapper per converter's implementation.
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.Converters.Add(new UnsupportedIEnumerableWrapperConverter());

            UnsupportedDerivedTypesWrapper_IEnumerable customWrapper = YamlSerializer.Deserialize<UnsupportedDerivedTypesWrapper_IEnumerable>(yaml, options);
            Assert.Null(customWrapper.IEnumerableWrapper);

            // Clear metadata for serialize.
            options = new YamlSerializerOptions();
            options.Converters.Add(new UnsupportedIEnumerableWrapperConverter());
            Assert.Equal("""{"IEnumerableWrapper":[]}""", YamlSerializer.Serialize(wrapper, options));
        }
    }

    public class ListWrapper : List<int> { }

    public class DictionaryWrapper : Dictionary<string, int> { }

    public class UnsupportedDictionaryWrapper : Dictionary<int[,], int> { }

    public class DerivedTypesWrapper
    {
        public ListWrapper? ListWrapper { get; set; }
        public List<int>? List { get; set; }
        public DictionaryWrapper? DictionaryWrapper { get; set; }
        public Dictionary<string, int>? Dictionary { get; set; }
    }

    public class UnsupportedDerivedTypesWrapper_Dictionary
    {
        public UnsupportedDictionaryWrapper? DictionaryWrapper { get; set; }
    }

    public class UnsupportedDerivedTypesWrapper_IEnumerable
    {
        public StringIEnumerableWrapper? IEnumerableWrapper { get; set; }
    }

    public class ListWrapperConverter : YamlConverter<ListWrapper>
    {
        public override ListWrapper Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartArray)
            {
                throw new YamlException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndArray)
                {
                    return null;
                }
            }

            throw new YamlException();
        }
        public override void Write(Utf8YamlWriter writer, ListWrapper value, YamlSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
    }

    public class UnsupportedIEnumerableWrapperConverter : YamlConverter<StringIEnumerableWrapper>
    {
        public override StringIEnumerableWrapper Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartArray)
            {
                throw new YamlException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndArray)
                {
                    return null;
                }
            }

            throw new YamlException();
        }
        public override void Write(Utf8YamlWriter writer, StringIEnumerableWrapper value, YamlSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
    }

    public class DictionaryWrapperConverter : YamlConverter<DictionaryWrapper>
    {
        public override DictionaryWrapper Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndObject)
                {
                    return null;
                }
            }

            throw new YamlException();
        }
        public override void Write(Utf8YamlWriter writer, DictionaryWrapper value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }

    public class UnsupportedDictionaryWrapperConverter : YamlConverter<UnsupportedDictionaryWrapper>
    {
        public override UnsupportedDictionaryWrapper Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndObject)
                {
                    return null;
                }
            }

            throw new YamlException();
        }
        public override void Write(Utf8YamlWriter writer, UnsupportedDictionaryWrapper value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }
}
