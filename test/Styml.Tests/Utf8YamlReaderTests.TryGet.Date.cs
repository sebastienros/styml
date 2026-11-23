// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Globalization;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class Utf8YamlReaderTests
    {
        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.ValidISO8601Tests), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsConversionToDateTime(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTime expected = DateTime.Parse(expectedString);

                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTime());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.ValidISO8601Tests), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsConversionToDateTimeOffset(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTimeOffset expected = DateTimeOffset.Parse(expectedString);

                    Assert.True(yaml.TryGetDateTimeOffset(out DateTimeOffset actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTimeOffset());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.ValidISO8601TestsWithUtcOffset), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsWithUTCOffsetToDateTime(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTime expected = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTime());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.ValidISO8601TestsWithUtcOffset), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsWithUTCOffsetToDateTimeOffset(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTimeOffset expected = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                    Assert.True(yaml.TryGetDateTimeOffset(out DateTimeOffset actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTimeOffset());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.InvalidISO8601Tests), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsInvalidConversionToDateTime(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                Assert.False(yaml.TryGetDateTime(out DateTime actualDateTime));
                Assert.Equal(default, actualDateTime);

                try
                {
                    DateTime value = yaml.GetDateTime();
                    Assert.Fail("Expected GetDateTime to throw FormatException due to invalid ISO 8601 input.");
                }
                catch (FormatException)
                { }
            }
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.InvalidISO8601Tests), MemberType = typeof(YamlDateTimeTestData))]
        public static void TestingStringsInvalidConversionToDateTimeOffset(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    Assert.False(yaml.TryGetDateTimeOffset(out DateTimeOffset actualDateTime));
                    Assert.Equal(default, actualDateTime);

                    try
                    {
                        DateTimeOffset value = yaml.GetDateTimeOffset();
                        Assert.Fail("Expected GetDateTimeOffset to throw FormatException due to invalid ISO 8601 input.");
                    }
                    catch (FormatException)
                    { }
                }
            }
        }

        [Fact]
        // https://github.com/dotnet/runtime/issues/30095
        public static void TestingDateTimeMinValue_UtcOffsetGreaterThan0()
        {
            string yamlString = """
                "0001-01-01T00:00:00"
                """;
            string expectedString = "0001-01-01T00:00:00";
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTime expected = DateTime.Parse(expectedString);

                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTime());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);

            // Test upstream serializer.
            Assert.Equal(DateTime.Parse(expectedString), YamlSerializer.Deserialize<DateTime>(yamlString));
        }

        [Fact]
        public static void TestingDateTimeMaxValue()
        {
            string yamlString = """
                "9999-12-31T23:59:59"
                """;
            string expectedString = "9999-12-31T23:59:59";
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTime expected = DateTime.Parse(expectedString);

                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    Assert.Equal(expected, yaml.GetDateTime());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);

            // Test upstream serializer.
            Assert.Equal(DateTime.Parse(expectedString), YamlSerializer.Deserialize<DateTime>(yamlString));
        }

        [Theory]
        [InlineData("""
            "\u001c\u0001"
            """)]
        [InlineData("""
            "\u001c\u0001\u0001"
            """)]
        public static void TryGetDateTimeAndOffset_InvalidPropertyValue(string testString)
        {
            var dataUtf8 = Encoding.UTF8.GetBytes(testString);
            var yaml = new Utf8YamlReader(dataUtf8);
            Assert.True(yaml.Read());

            Assert.False(yaml.TryGetDateTime(out var dateTime));
            Assert.Equal(default, dateTime);
            YamlTestHelper.AssertThrows<FormatException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetDateTime());

            Assert.False(yaml.TryGetDateTimeOffset(out var dateTimeOffset));
            Assert.Equal(default, dateTimeOffset);
            YamlTestHelper.AssertThrows<FormatException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetDateTimeOffset());
        }

        [Theory]
        [InlineData("\"2007-04-05T24:00:00\"", "2007-04-06T00:00:00")]
        [InlineData("\"2007-04-05T24:00:00.0000000\"", "2007-04-06T00:00:00")]
        [InlineData("\"2020-02-29T24:00:00+00:00\"", "2020-03-01T00:00:00+00:00")]
        public static void TestingStrings_Hour24_Valid(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    DateTime expected = DateTime.Parse(expectedString);
                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    DateTimeOffset expectedDto = DateTimeOffset.Parse(expectedString);
                    Assert.True(yaml.TryGetDateTimeOffset(out DateTimeOffset actualDto));
                    Assert.Equal(expectedDto, actualDto);
                }
            }
        }

        [Theory]
        [InlineData("\"2023-12-31T24:00:00Z\"", "2024-01-01T00:00:00.0000000Z")]
        public static void TestingStrings_Hour24_ValidUtc(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    // DateTime.Parse for strings with Z TZD returns DateTimeKind.Local,
                    // but the JSON reader returns DateTimeKind.Utc. Use ParseExact with RoundtripKind.
                    DateTime expected = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    Assert.True(yaml.TryGetDateTime(out DateTime actual));
                    Assert.Equal(expected, actual);

                    DateTimeOffset expectedDto = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    Assert.True(yaml.TryGetDateTimeOffset(out DateTimeOffset actualDto));
                    Assert.Equal(expectedDto, actualDto);
                }
            }
        }

        [Theory]
        [InlineData("\"2007-04-05T24:00:01\"")]  // Non-zero seconds
        [InlineData("\"2007-04-05T24:01:00\"")]  // Non-zero minutes
        [InlineData("\"2007-04-05T24:00:00.0000001\"")]  // Non-zero fraction
        [InlineData("\"9999-12-31T24:00:00\"")]  // Would overflow
        public static void TestingStrings_Hour24_Invalid(string testString)
        {
            var dataUtf8 = Encoding.UTF8.GetBytes(testString);
            var yaml = new Utf8YamlReader(dataUtf8);
            Assert.True(yaml.Read());

            Assert.False(yaml.TryGetDateTime(out var dateTime));
            Assert.Equal(default, dateTime);
            YamlTestHelper.AssertThrows<FormatException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetDateTime());

            Assert.False(yaml.TryGetDateTimeOffset(out var dateTimeOffset));
            Assert.Equal(default, dateTimeOffset);
            YamlTestHelper.AssertThrows<FormatException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetDateTimeOffset());
        }
    }
}
