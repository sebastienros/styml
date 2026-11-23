// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>Exit criterion 5: the scalar rules, including D2.</summary>
    public class TypedReadTests
    {
        private static Utf8YamlReader ReadValue(string yaml)
        {
            // "v: <scalar>" so that the scalar is in a block mapping value.
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("v: " + yaml));
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
            Assert.True(reader.Read());
            return reader;
        }

        [Theory]
        // Null
        [InlineData("null", YamlTokenType.Null)]
        [InlineData("Null", YamlTokenType.Null)]
        [InlineData("NULL", YamlTokenType.Null)]
        [InlineData("~", YamlTokenType.Null)]
        [InlineData("", YamlTokenType.Null)]
        // Booleans
        [InlineData("true", YamlTokenType.True)]
        [InlineData("True", YamlTokenType.True)]
        [InlineData("TRUE", YamlTokenType.True)]
        [InlineData("false", YamlTokenType.False)]
        [InlineData("False", YamlTokenType.False)]
        [InlineData("FALSE", YamlTokenType.False)]
        // Integers
        [InlineData("0", YamlTokenType.Number)]
        [InlineData("-12", YamlTokenType.Number)]
        [InlineData("+12", YamlTokenType.Number)]
        [InlineData("0o17", YamlTokenType.Number)]
        [InlineData("0x1F", YamlTokenType.Number)]
        [InlineData("0xff", YamlTokenType.Number)]
        // Floats
        [InlineData("1.5", YamlTokenType.Number)]
        [InlineData(".5", YamlTokenType.Number)]
        [InlineData("5.", YamlTokenType.Number)]
        [InlineData("-.5e3", YamlTokenType.Number)]
        [InlineData("1e3", YamlTokenType.Number)]
        [InlineData("1E-3", YamlTokenType.Number)]
        [InlineData(".inf", YamlTokenType.Number)]
        [InlineData("-.Inf", YamlTokenType.Number)]
        [InlineData("+.INF", YamlTokenType.Number)]
        [InlineData(".nan", YamlTokenType.Number)]
        [InlineData(".NaN", YamlTokenType.Number)]
        [InlineData(".NAN", YamlTokenType.Number)]
        // Strings: YAML 1.1 forms are not resolved (D6), malformed numbers stay strings.
        [InlineData("yes", YamlTokenType.String)]
        [InlineData("no", YamlTokenType.String)]
        [InlineData("on", YamlTokenType.String)]
        [InlineData("nULL", YamlTokenType.String)]
        [InlineData("tRUE", YamlTokenType.String)]
        [InlineData("0b101", YamlTokenType.String)]
        [InlineData("1_000", YamlTokenType.String)]
        [InlineData("1:30", YamlTokenType.String)]
        [InlineData("0x", YamlTokenType.String)]
        [InlineData("0o8", YamlTokenType.String)]
        [InlineData("1.2.3", YamlTokenType.String)]
        [InlineData(".", YamlTokenType.String)]
        [InlineData("-.nan", YamlTokenType.String)]
        [InlineData("inf", YamlTokenType.String)]
        [InlineData("1.10.1", YamlTokenType.String)]
        // Quoted and block scalars are always strings; a multi-line plain scalar too.
        [InlineData("'12'", YamlTokenType.String)]
        [InlineData("\"true\"", YamlTokenType.String)]
        [InlineData("|\n  12\n", YamlTokenType.String)]
        [InlineData("12\n  34", YamlTokenType.String)]
        // Core tags override resolution.
        [InlineData("!!str 12", YamlTokenType.String)]
        [InlineData("!!str true", YamlTokenType.String)]
        [InlineData("!!str", YamlTokenType.String)]
        [InlineData("! 12", YamlTokenType.String)]
        [InlineData("!!int \"12\"", YamlTokenType.Number)]
        [InlineData("!!float 12", YamlTokenType.Number)]
        [InlineData("!!bool 'false'", YamlTokenType.False)]
        [InlineData("!!null ''", YamlTokenType.Null)]
        // Non-core tags do not change resolution.
        [InlineData("!local 12", YamlTokenType.Number)]
        public void CoreSchemaResolution(string scalar, YamlTokenType expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(expected, reader.TokenType);
        }

        [Theory]
        [InlineData("!!int 12.5")]
        [InlineData("!!int abc")]
        [InlineData("!!float abc")]
        [InlineData("!!bool yes")]
        [InlineData("!!null x")]
        [InlineData("!!map x")]
        [InlineData("!!seq x")]
        public void CoreTagIsValidated(string scalar)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("v: " + scalar));
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            try
            {
                reader.Read();
                Assert.Fail("A tagged scalar that does not match its tag must be rejected.");
            }
            catch (YamlException)
            {
            }
        }

        [Theory]
        [InlineData("12", 12)]
        [InlineData("-12", -12)]
        [InlineData("+12", 12)]
        [InlineData("0x1F", 31)]
        [InlineData("0xff", 255)]
        [InlineData("0o17", 15)]
        [InlineData("2147483647", int.MaxValue)]
        [InlineData("-2147483648", int.MinValue)]
        [InlineData("0x7FFFFFFF", int.MaxValue)]
        public void Int32(string scalar, int expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(expected, reader.GetInt32());
            Assert.True(reader.TryGetInt32(out int value));
            Assert.Equal(expected, value);
            Assert.Equal((long)expected, reader.GetInt64());
        }

        [Theory]
        [InlineData("2147483648")]
        [InlineData("0x80000000")]
        [InlineData("1.5")]
        [InlineData(".inf")]
        public void Int32OutOfRangeOrNotInteger(string scalar)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.False(reader.TryGetInt32(out _));
        }

        [Theory]
        [InlineData("0xFF", (byte)255)]
        [InlineData("0o377", (byte)255)]
        [InlineData("+1", (byte)1)]
        public void Byte(string scalar, byte expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(expected, reader.GetByte());
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0x100")]
        public void ByteOutOfRange(string scalar)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.False(reader.TryGetByte(out _));
        }

        [Theory]
        [InlineData("0xFFFFFFFFFFFFFFFF", ulong.MaxValue)]
        [InlineData("0o1777777777777777777777", ulong.MaxValue)]
        [InlineData("+18446744073709551615", ulong.MaxValue)]
        public void UInt64(string scalar, ulong expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(expected, reader.GetUInt64());
        }

        [Theory]
        [InlineData("1.5", 1.5)]
        [InlineData(".5", 0.5)]
        [InlineData("-.5", -0.5)]
        [InlineData("+.5", 0.5)]
        [InlineData("5.", 5.0)]
        [InlineData("5.e2", 500.0)]
        [InlineData("1e3", 1000.0)]
        [InlineData("1E-3", 0.001)]
        [InlineData("0x10", 16.0)]
        [InlineData("0o10", 8.0)]
        [InlineData(".inf", double.PositiveInfinity)]
        [InlineData("+.Inf", double.PositiveInfinity)]
        [InlineData("-.INF", double.NegativeInfinity)]
        public void Double(string scalar, double expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(expected, reader.GetDouble());
            Assert.Equal((float)expected, reader.GetSingle());
        }

        [Theory]
        [InlineData(".nan")]
        [InlineData(".NaN")]
        [InlineData(".NAN")]
        public void NaN(string scalar)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.True(double.IsNaN(reader.GetDouble()));
            Assert.True(float.IsNaN(reader.GetSingle()));
            Assert.False(reader.TryGetDecimal(out _));
        }

        [Theory]
        [InlineData("1.25", "1.25")]
        [InlineData(".25", "0.25")]
        [InlineData("0x10", "16")]
        [InlineData("+3", "3")]
        public void Decimal(string scalar, string expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(decimal.Parse(expected, Globalization.CultureInfo.InvariantCulture), reader.GetDecimal());
        }

        // D2: string-shaped reads accept any plain scalar token.
        [Theory]
        [InlineData("1.10", YamlTokenType.Number, "1.10")]
        [InlineData("0x1F", YamlTokenType.Number, "0x1F")]
        [InlineData("True", YamlTokenType.True, "True")]
        [InlineData("false", YamlTokenType.False, "false")]
        [InlineData("2001-12-14", YamlTokenType.String, "2001-12-14")]
        public void StringReadsOfPlainScalars(string scalar, YamlTokenType type, string expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(type, reader.TokenType);
            Assert.Equal(YamlScalarStyle.Plain, reader.ScalarStyle);
            Assert.Equal(expected, reader.GetString());
            Assert.True(reader.ValueTextEquals(expected));
            Assert.True(reader.ValueTextEquals(Encoding.UTF8.GetBytes(expected)));
            Assert.False(reader.ValueTextEquals(expected + "x"));

            byte[] utf8 = new byte[64];
            Assert.Equal(expected, Encoding.UTF8.GetString(utf8, 0, reader.CopyString(utf8)));
            char[] chars = new char[64];
            Assert.Equal(expected, new string(chars, 0, reader.CopyString(chars)));
        }

        [Fact]
        public void NullReadsAsNullString()
        {
            Assert.Null(ReadValue("~").GetString());
            Assert.Null(ReadValue("").GetString());
            Assert.Null(ReadValue("null").GetString());
        }

        [Theory]
        [InlineData("2001-12-14T21:59:43.10-05:00")]
        [InlineData("'2001-12-14T21:59:43.10-05:00'")]
        [InlineData("|-\n  2001-12-14T21:59:43.10-05:00")]
        public void DateTimeOffsetFromAnyStringStyle(string scalar)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(new DateTimeOffset(2001, 12, 14, 21, 59, 43, 100, TimeSpan.FromHours(-5)), reader.GetDateTimeOffset());
        }

        [Theory]
        [InlineData("aGVsbG8=")]
        [InlineData("'aGVsbG8='")]
        [InlineData("\"aGVs\\\n  bG8=\"")]
        [InlineData("|-\n  aGVsbG8=")]
        public void Base64FromAnyStringStyle(string scalar)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal("hello", Encoding.UTF8.GetString(reader.GetBytesFromBase64()));
        }

        [Fact]
        public void QuotedContinuationLineMustBeIndented()
        {
            var reader = new Utf8YamlReader("v: \"a\nb\""u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            try
            {
                reader.Read();
                Assert.Fail("A quoted scalar's continuation line at the mapping's indentation must be rejected.");
            }
            catch (YamlException)
            {
            }
        }

        [Fact]
        public void GuidFromPlainScalar()
        {
            var guid = Guid.Parse("8b4e1f3a-26b4-4d43-a3a1-0e7a4e6b62d3");
            Assert.Equal(guid, ReadValue("8b4e1f3a-26b4-4d43-a3a1-0e7a4e6b62d3").GetGuid());
        }

        // A quoted "12" is a string: numeric reads reject it.
        [Fact]
        public void QuotedNumberIsNotANumber()
        {
            Utf8YamlReader reader = ReadValue("\"12\"");
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            try
            {
                reader.GetInt32();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [Theory]
        [InlineData("plain", YamlScalarStyle.Plain, "plain")]
        [InlineData("'it''s'", YamlScalarStyle.SingleQuoted, "it's")]
        [InlineData("\"a\\tb\\u00e9\\x41\\N\"", YamlScalarStyle.DoubleQuoted, "a\tb\u00e9A\u0085")]
        [InlineData("\"line\n\n  folded\"", YamlScalarStyle.DoubleQuoted, "line\nfolded")]
        [InlineData("\"join\\\n  ed\"", YamlScalarStyle.DoubleQuoted, "joined")]
        [InlineData("|\n  a\n   b\n\n", YamlScalarStyle.Literal, "a\n b\n")]
        [InlineData("|-\n  a\n", YamlScalarStyle.Literal, "a")]
        [InlineData("|+\n  a\n\n", YamlScalarStyle.Literal, "a\n\n")]
        [InlineData("|2\n   a\n", YamlScalarStyle.Literal, " a\n")]
        [InlineData(">\n  a\n  b\n\n  c\n", YamlScalarStyle.Folded, "a b\nc\n")]
        [InlineData(">\n  a\n    b\n  c\n", YamlScalarStyle.Folded, "a\n  b\nc\n")]
        [InlineData("multi\n  line\n\n  plain", YamlScalarStyle.Plain, "multi line\nplain")]
        public void ScalarStylesAndValues(string scalar, YamlScalarStyle style, string expected)
        {
            Utf8YamlReader reader = ReadValue(scalar);
            Assert.Equal(style, reader.ScalarStyle);
            Assert.Equal(expected, reader.GetString());
        }
    }
}
