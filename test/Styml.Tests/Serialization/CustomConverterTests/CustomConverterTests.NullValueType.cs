// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Allow a conversion of "null" to a 0 value.
        /// </summary>
        private class Int32NullConverter : YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    return 0;
                }

                return reader.GetInt32();
            }

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(value);
            }
        }

        [Fact]
        public static void ValueTypeConverterForNull()
        {
            // Baseline
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("null"));
            Assert.Equal(1, YamlSerializer.Deserialize<int>("1"));

            var options = new YamlSerializerOptions();
            options.Converters.Add(new Int32NullConverter());

            Assert.Equal(0, YamlSerializer.Deserialize<int>("null", options));
            Assert.Equal(1, YamlSerializer.Deserialize<int>("1", options));
        }

        [Fact]
        public static void ValueTypeConverterForNullWithArray()
        {
            const string yaml = "[null, 1, null]";

            // Baseline
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>(yaml));

            var options = new YamlSerializerOptions();
            options.Converters.Add(new Int32NullConverter());

            int[] arr = YamlSerializer.Deserialize<int[]>(yaml, options);
            Assert.Equal(0, arr[0]);
            Assert.Equal(1, arr[1]);
            Assert.Equal(0, arr[2]);
        }

        /// <summary>
        /// Allow a conversion of empty string to a null DateTimeOffset?.
        /// </summary>
        public class YamlNullableDateTimeOffsetConverter : YamlConverter<DateTimeOffset?>
        {
            public override DateTimeOffset? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    return default;
                }

                string value = reader.GetString();
                if (value == string.Empty)
                {
                    return default;
                }

                return DateTimeOffset.ParseExact(value, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            public override void Write(Utf8YamlWriter writer, DateTimeOffset? value, YamlSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(value.Value.ToString("yyyy/MM/dd HH:mm:ss"));
                }
            }
        }

        private class ClassWithNullableAndYamlConverterAttribute
        {
            [YamlConverter(typeof(YamlNullableDateTimeOffsetConverter))]
            public DateTimeOffset? NullableValue { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ValueConverterForNullableWithYamlConverterAttribute()
        {
            ClassWithNullableAndYamlConverterAttribute obj;

            const string BaselineYaml = """{"NullableValue":"1989/01/01 11:22:33"}""";
            obj = YamlSerializer.Deserialize<ClassWithNullableAndYamlConverterAttribute>(BaselineYaml);
            Assert.NotNull(obj.NullableValue);

            const string Yaml = """{"NullableValue":""}""";
            obj = YamlSerializer.Deserialize<ClassWithNullableAndYamlConverterAttribute>(Yaml);
            Assert.Null(obj.NullableValue);

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "NullableValue":null
                """, yaml);
        }

        private class ClassWithNullableAndWithoutYamlConverterAttribute
        {
            public DateTimeOffset? NullableValue { get; set; }
            public List<DateTimeOffset?> NullableValues { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ValueConverterForNullableWithoutYamlConverterAttribute()
        {
            const string Yaml = """{"NullableValue":"", "NullableValues":[""]}""";
            ClassWithNullableAndWithoutYamlConverterAttribute obj;

            // The json is not valid with the default converter.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithNullableAndWithoutYamlConverterAttribute>(Yaml));

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.Converters.Add(new YamlNullableDateTimeOffsetConverter());

            obj = YamlSerializer.Deserialize<ClassWithNullableAndWithoutYamlConverterAttribute>(Yaml, options);
            Assert.Null(obj.NullableValue);
            Assert.Null(obj.NullableValues[0]);

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "NullableValue":null
                """, yaml);
            Assert.Contains("""
                "NullableValues":[null]
                """, yaml);
        }

        [YamlConverter(typeof(ClassThatCanBeNullDependingOnContentConverter))]
        private class ClassThatCanBeNullDependingOnContent
        {
            public int MyInt { get; set; }
        }

        /// <summary>
        /// Allow a conversion of ClassThatCanBeNullDependingOnContent to null when its MyInt property is 0.
        /// </summary>
        private class ClassThatCanBeNullDependingOnContentConverter : YamlConverter<ClassThatCanBeNullDependingOnContent>
        {
            public override ClassThatCanBeNullDependingOnContent Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    return null;
                }

                // Assume a single property.

                reader.Read();
                Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);

                reader.Read();
                int myInt = reader.GetInt16();

                reader.Read();
                Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

                if (myInt == 0)
                {
                    return null;
                }

                return new ClassThatCanBeNullDependingOnContent
                {
                    MyInt = myInt
                };
            }

            public override void Write(Utf8YamlWriter writer, ClassThatCanBeNullDependingOnContent value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                if (value.MyInt == 0)
                {
                    writer.WriteNull("MyInt");
                }
                else
                {
                    writer.WriteNumber("MyInt", value.MyInt);
                }

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ConverterForClassThatCanBeNullDependingOnContent()
        {
            ClassThatCanBeNullDependingOnContent obj;

            obj = YamlSerializer.Deserialize<ClassThatCanBeNullDependingOnContent>("""{"MyInt":5}""");
            Assert.Equal(5, obj.MyInt);

            string yaml;
            yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "MyInt":5
                """, yaml);

            obj.MyInt = 0;
            yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "MyInt":null
                """, yaml);

            obj = YamlSerializer.Deserialize<ClassThatCanBeNullDependingOnContent>("""{"MyInt":0}""");
            Assert.Null(obj);
        }
    }
}
