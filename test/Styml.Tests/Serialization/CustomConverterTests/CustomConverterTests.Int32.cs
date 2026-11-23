// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Allow both string and number values on deserialize.
        /// </summary>
        private class Int32Converter : YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.String)
                {
                    string stringValue = reader.GetString();
                    if (int.TryParse(stringValue, out int value))
                    {
                        return value;
                    }
                }
                else if (reader.TokenType == YamlTokenType.Number)
                {
                    return reader.GetInt32();
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(value);
            }
        }

        [Fact]
        public static void OverrideDefaultConverter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new Int32Converter());

            {
                int myInt = YamlSerializer.Deserialize<int>("1", options);
                Assert.Equal(1, myInt);
            }

            {
                int myInt = YamlSerializer.Deserialize<int>("""
                    "1"
                    """, options);
                Assert.Equal(1, myInt);
            }
        }
    }
}
