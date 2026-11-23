// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        public enum MyBoolEnum
        {
            True = 1,   // JSON is "TRUE"
            False = 2,  // JSON is "FALSE"
            Unknown = 3 // JSON is "?"
        }

        // A converter for a specific Enum.
        private class MyBoolEnumConverter : YamlConverter<MyBoolEnum>
        {
            // CanConvert does not need to be implemented here since we only convert MyBoolEnum.

            public override MyBoolEnum Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                string enumValue = reader.GetString();
                if (enumValue == "TRUE")
                {
                    return MyBoolEnum.True;
                }

                if (enumValue == "FALSE")
                {
                    return MyBoolEnum.False;
                }

                if (enumValue == "?")
                {
                    return MyBoolEnum.Unknown;
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, MyBoolEnum value, YamlSerializerOptions options)
            {
                if (value is MyBoolEnum.True)
                {
                    writer.WriteStringValue("TRUE");
                }
                else if (value is MyBoolEnum.False)
                {
                    writer.WriteStringValue("FALSE");
                }
                else
                {
                    writer.WriteStringValue("?");
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomEnumConverter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new MyBoolEnumConverter());

            {
                MyBoolEnum value = YamlSerializer.Deserialize<MyBoolEnum>("""
                    "TRUE"
                    """, options);
                Assert.Equal(MyBoolEnum.True, value);
                Assert.Equal("""
                    "TRUE"
                    """, YamlSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum value = YamlSerializer.Deserialize<MyBoolEnum>("""
                    "FALSE"
                    """, options);
                Assert.Equal(MyBoolEnum.False, value);
                Assert.Equal("""
                    "FALSE"
                    """, YamlSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum value = YamlSerializer.Deserialize<MyBoolEnum>("""
                    "?"
                    """, options);
                Assert.Equal(MyBoolEnum.Unknown, value);
                Assert.Equal("""
                    "?"
                    """, YamlSerializer.Serialize(value, options));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullableCustomEnumConverter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new MyBoolEnumConverter());

            {
                MyBoolEnum? value = YamlSerializer.Deserialize<MyBoolEnum?>(@"null", options);
                Assert.Null(value);
            }

            {
                MyBoolEnum? value = YamlSerializer.Deserialize<MyBoolEnum?>("""
                    "TRUE"
                    """, options);
                Assert.Equal(MyBoolEnum.True, value);
                Assert.Equal("""
                    "TRUE"
                    """, YamlSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum? value = YamlSerializer.Deserialize<MyBoolEnum?>("""
                    "FALSE"
                    """, options);
                Assert.Equal(MyBoolEnum.False, value);
                Assert.Equal("""
                    "FALSE"
                    """, YamlSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum? value = YamlSerializer.Deserialize<MyBoolEnum?>("""
                    "?"
                    """, options);
                Assert.Equal(MyBoolEnum.Unknown, value);
                Assert.Equal("""
                    "?"
                    """, YamlSerializer.Serialize(value, options));
            }
        }

        // A custom enum converter that uses an array for containing bit flags.
        private sealed class YamlStringEnumArrayConverter : YamlConverterFactory
        {
            public YamlStringEnumArrayConverter() { }

            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert.IsEnum;
            }

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(YamlConverterEnumArray<>).MakeGenericType(typeToConvert),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: null,
                    culture: null);

                return converter;
            }
        }

        internal class YamlConverterEnumArray<T> : YamlConverter<T>
            where T : struct, Enum
        {
            public override bool CanConvert(Type type)
            {
                return type.IsEnum;
            }

            public override T Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartArray)
                {
                    throw new YamlException();
                }

                string enumString = default;

                bool first = true;
                while (true)
                {
                    reader.Read();

                    if (reader.TokenType == YamlTokenType.EndArray)
                    {
                        break;
                    }

                    if (reader.TokenType != YamlTokenType.String)
                    {
                        throw new YamlException();
                    }

                    string enumValue = reader.GetString();
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        enumString += ",";
                    }

                    enumString += enumValue.ToString();
                }

                if (!Enum.TryParse(enumString, out T value))
                {
                    throw new YamlException();
                }

                return value;
            }

            public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options)
            {
                string[] enumString = value.ToString().Split(',');

                writer.WriteStartArray();

                foreach (string enumValue in enumString)
                {
                    writer.WriteStringValue(enumValue.TrimStart());
                }

                writer.WriteEndArray();
            }
        }

        [Flags]
        [YamlConverter(typeof(YamlStringEnumArrayConverter))]
        public enum eDevice : uint
        {
            Unknown = 0x0,
            Phone = 0x1,
            PC = 0x2,
            Laptop = 0x4,
            Tablet = 0x8,
            IoT = 0x10,
            Watch = 0x20,
            TV = 0x40,
            Hub = 0x80,
            Studio = 0x100,
            Book = 0x200,
            MediaCenter = 0x400,
        }

        [Theory]
        [InlineData("""["PC","Tablet"]""")]
        [InlineData("""["Tablet","PC"]""")]
        public static void EnumValue(string yaml)
        {
            eDevice obj;

            void Verify()
            {
                Assert.Equal(eDevice.PC | eDevice.Tablet, obj);
            }

            obj = YamlSerializer.Deserialize<eDevice>(yaml);
            Verify();

            // Round-trip and verify.
            yaml = YamlSerializer.Serialize(obj);
            obj = YamlSerializer.Deserialize<eDevice>(yaml);
            Verify();
        }

        private class ConnectionList
        {
            public List<ConnectionFile> Connections { get; set; }
        }

        private class ConnectionFile
        {
            public eDevice Device { get; set; }
        }

        [Theory]
        [InlineData("""{"Connections":[{"Device":["PC","Tablet"]},{"Device":["PC","Laptop"]}]}""")]
        [InlineData("""{"Connections":[{"Device":["Tablet","PC"]},{"Device":["Laptop","PC"]}]}""")]
        public static void EnumArray(string yaml)
        {
            ConnectionList obj;

            void Verify()
            {
                Assert.Equal(2, obj.Connections.Count);
                Assert.Equal(eDevice.PC | eDevice.Tablet, obj.Connections[0].Device);
                Assert.Equal(eDevice.PC | eDevice.Laptop, obj.Connections[1].Device);
            }

            obj = YamlSerializer.Deserialize<ConnectionList>(yaml);
            Verify();

            // Round-trip and verify.
            yaml = YamlSerializer.Serialize(obj);
            obj = YamlSerializer.Deserialize<ConnectionList>(yaml);
            Verify();
        }
    }
}
