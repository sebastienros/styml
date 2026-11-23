// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        [Theory]
        [MemberData(nameof(GetCustomKeysAndExpectedPropertyNameEncodings))]
        [Trait("category", "jsonoutput")]
        public static void CustomKeyConverter_Serialization<TKey>(TKey key, string expectedKeyEncoding)
        {
            var options = new YamlSerializerOptions { Converters = { new EmbeddedYamlKeyConverter<TKey>() } };
            var value = new Dictionary<TKey, byte> { [key] = 42 };

            string expectedYaml = $"{{\"{expectedKeyEncoding}\":42}}";
            string yaml = YamlSerializer.Serialize(value, options);

            Assert.Equal(expectedYaml, yaml);
        }

        [Theory]
        [MemberData(nameof(GetCustomKeysAndExpectedPropertyNameEncodings))]
        public static void CustomKeyConverter_Deserialization<TKey>(TKey key, string expectedKeyEncoding)
        {
            var options = new YamlSerializerOptions { Converters = { new EmbeddedYamlKeyConverter<TKey>() } };

            string yaml = $"{{\"{expectedKeyEncoding}\":42}}";
            var deserializedValue = YamlSerializer.Deserialize<Dictionary<TKey, byte>>(yaml, options);

            Assert.Equal(1, deserializedValue.Count);
            Assert.Equal(key, deserializedValue.Keys.First());
        }

        public static IEnumerable<object[]> GetCustomKeysAndExpectedPropertyNameEncodings()
        {
            yield return Wrap("key");
            yield return Wrap(42);
            yield return Wrap(true);
            yield return Wrap(new KeyValuePair<string, bool>("x", false));
            yield return Wrap(new int[] { 1, 2, 3 });

            static object[] Wrap<TKey>(TKey key) => new object[] { key, JavaScriptEncoder.Default.Encode(YamlSerializer.Serialize(key)) };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ExtensionDataProperty_Serialization_IgnoreCustomStringKeyConverter()
        {
            var options = new YamlSerializerOptions { Converters = { new EmbeddedYamlKeyConverter<string>() } };
            var value = new PocoWithExtensionDataProperty();

            string expectedYaml = """{"key":42}""";
            string yaml = YamlSerializer.Serialize(value, options);

            Assert.Equal(expectedYaml, yaml);
        }

        public class PocoWithExtensionDataProperty
        {
            [YamlExtensionData]
            public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object> { ["key"] = 42 };
        }

        [Theory]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.DoNothing)]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.HandleEntireParentObject)]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.HandleEntireProperty)]
        public static void InvalidCustomKeyConverter_Serialization(InvalidCustomKeyConverter.InvalidOperationType invalidOperationType)
        {
            var options = new YamlSerializerOptions { Converters = { new InvalidCustomKeyConverter { OperationType = invalidOperationType } } };
            var value = new Dictionary<string, int> { ["key"] = 42 };

            Assert.Throws<YamlException>(() => YamlSerializer.Serialize(value, options));
        }

        [Theory]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.HandleEntireParentObject, typeof(YamlException))]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.HandleEntireProperty, typeof(YamlException))]
        [InlineData(InvalidCustomKeyConverter.InvalidOperationType.ReturnNull, typeof(ArgumentNullException))]
        public static void InvalidCustomKeyConverter_Deserialization(InvalidCustomKeyConverter.InvalidOperationType invalidOperationType, Type exceptionType)
        {
            var options = new YamlSerializerOptions { Converters = { new InvalidCustomKeyConverter { OperationType = invalidOperationType } } };
            string yaml = """{"key1" : 1, "key2" : 2 }""";

            Assert.Throws(exceptionType, () => YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ObjectConverterHandlingStrings_DictionaryWithStringKey_DoesNotCauseStackOverflow()
        {
            // Regression test: A custom YamlConverter<object> that claims to handle strings
            // via CanConvert was causing StackOverflowException when serializing dictionaries
            // with string keys, due to infinite recursion in the CastingConverter.
            // The converter writes an empty string for property names and returns a constant on read,
            // which lets us verify the fallback converter is being used instead of the custom converter.
            var options = new YamlSerializerOptions { Converters = { new GenericObjectConverterHandlingStrings() } };
            var value = new Dictionary<string, int> { ["key"] = 123 };

            // The fallback StringConverter should be used for property names, preserving "key"
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"key":123}""", yaml);

            // Deserialization should also use the fallback converter for property names
            var deserialized = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options);
            Assert.Equal(123, deserialized["key"]);
        }

        public class GenericObjectConverterHandlingStrings : YamlConverter<object>
        {
            public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(string);
            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetString()!;
            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options) => writer.WriteStringValue(value?.ToString());

            // These methods write/read distinctive values to verify the fallback converter is used instead
            public override void WriteAsPropertyName(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
                => writer.WritePropertyName(string.Empty);
            public override object ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => "unexpected_key";
        }

        public class EmbeddedYamlKeyConverter<T> : YamlConverter<T>
        {
            public override T Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions _)
                => throw new NotSupportedException();

            public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions _)
                => throw new NotSupportedException();

            public override void WriteAsPropertyName(Utf8YamlWriter writer, T value, YamlSerializerOptions _)
                => writer.WritePropertyName(YamlSerializer.Serialize(value));

            public override T ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions _)
                => YamlSerializer.Deserialize<T>(reader.GetString());
        }

        public class InvalidCustomKeyConverter : YamlConverter<string>
        {
            public enum InvalidOperationType
            {
                DoNothing, HandleEntireProperty, HandleEntireParentObject, ReturnNull
            }

            public InvalidOperationType OperationType { get; init; }

            public override string Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotSupportedException();
            public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options)
                => throw new NotSupportedException();

            public override void WriteAsPropertyName(Utf8YamlWriter writer, string value, YamlSerializerOptions _)
            {
                switch (OperationType)
                {
                    case InvalidOperationType.HandleEntireProperty:
                        writer.WriteString("key", value);
                        break;

                    case InvalidOperationType.HandleEntireParentObject:
                        writer.WriteEndObject();
                        break;

                    case InvalidOperationType.DoNothing:
                    default:
                        break;
                }
            }

            public override string ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions _)
            {
                switch (OperationType)
                {
                    case InvalidOperationType.HandleEntireProperty:
                        reader.Read();
                        reader.Skip();
                        break;

                    case InvalidOperationType.HandleEntireParentObject:
                        while (reader.TokenType != YamlTokenType.EndObject)
                        {
                            reader.Read();
                            reader.Skip();
                        }
                        break;

                    case InvalidOperationType.ReturnNull:
                        return null;

                    default:
                        break;
                }

                return "key";
            }
        }
    }
}
