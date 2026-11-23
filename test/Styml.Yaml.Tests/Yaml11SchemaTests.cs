// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// The opt-in YAML 1.1 schema (M3d): YamlReaderOptions.Schema =
    /// YamlSchemaKind.Yaml11. The forms are those YamlDotNet 18.1.0 or SharpYaml 3.17.0 accept
    /// (docs/feature-parity.md); with the default Core schema none of them changes.
    /// </summary>
    public class Yaml11SchemaTests
    {
        private static readonly YamlReaderOptions s_yaml11 = new() { Schema = YamlSchemaKind.Yaml11 };

        private static Utf8YamlReader ValueReader(string scalar, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("a: " + scalar + "\n"), options);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            return reader;
        }

        [Theory]
        [InlineData("y", true)]
        [InlineData("Y", true)]
        [InlineData("yes", true)]
        [InlineData("Yes", true)]
        [InlineData("YES", true)]
        [InlineData("on", true)]
        [InlineData("On", true)]
        [InlineData("ON", true)]
        [InlineData("n", false)]
        [InlineData("N", false)]
        [InlineData("no", false)]
        [InlineData("No", false)]
        [InlineData("NO", false)]
        [InlineData("off", false)]
        [InlineData("Off", false)]
        [InlineData("OFF", false)]
        [InlineData("true", true)]
        [InlineData("FALSE", false)]
        public void Booleans(string scalar, bool expected)
        {
            Utf8YamlReader reader = ValueReader(scalar, s_yaml11);
            Assert.Equal(expected ? YamlTokenType.True : YamlTokenType.False, reader.TokenType);
            Assert.Equal(expected, reader.GetBoolean());

            // D2 still holds: the text stays readable as a string.
            Assert.Equal(scalar, reader.GetString());

            // The default schema leaves the 1.1 forms strings.
            if (scalar is not ("true" or "FALSE"))
            {
                Assert.Equal(YamlTokenType.String, ValueReader(scalar, default).TokenType);
            }
        }

        [Theory]
        [InlineData("0777", 511)]
        [InlineData("010", 8)]
        [InlineData("-010", -8)]
        [InlineData("0o17", 15)]
        [InlineData("0b1010", 10)]
        [InlineData("-0b11", -3)]
        [InlineData("0b1_0", 2)]
        [InlineData("0x1F", 31)]
        [InlineData("0x_1F", 31)]
        [InlineData("1_000", 1000)]
        [InlineData("+12", 12)]
        [InlineData("190:20:30", 685230)]
        [InlineData("-1:30", -90)]
        [InlineData("1:30:00", 5400)]
        [InlineData("09", 9)]
        [InlineData("0", 0)]
        public void Integers(string scalar, long expected)
        {
            Utf8YamlReader reader = ValueReader(scalar, s_yaml11);
            Assert.Equal(YamlTokenType.Number, reader.TokenType);
            Assert.Equal(expected, reader.GetInt64());
            Assert.True(reader.TryGetDouble(out double d));
            Assert.Equal(expected, d);
            Assert.True(reader.TryGetDecimal(out decimal m));
            Assert.Equal(expected, m);
            if (expected is >= int.MinValue and <= int.MaxValue)
            {
                Assert.Equal((int)expected, reader.GetInt32());
            }
            if (expected is >= short.MinValue and <= short.MaxValue)
            {
                Assert.True(reader.TryGetInt16(out short s));
                Assert.Equal(expected, s);
            }
            if (expected >= 0)
            {
                Assert.Equal((ulong)expected, reader.GetUInt64());
            }
        }

        [Fact]
        public void DefaultSchemaReadsLeadingZerosAsDecimal()
        {
            Assert.Equal(777, ValueReader("0777", default).GetInt32());
            Assert.Equal(YamlTokenType.String, ValueReader("0b1010", default).TokenType);
            Assert.Equal(YamlTokenType.String, ValueReader("1_000", default).TokenType);
            Assert.Equal(YamlTokenType.String, ValueReader("1:30", default).TokenType);
        }

        [Theory]
        [InlineData("1_000.5", 1000.5)]
        [InlineData("0777.5", 777.5)]
        [InlineData(".5", 0.5)]
        [InlineData("+.5", 0.5)]
        [InlineData("1.5e3", 1500)]
        [InlineData("1_0.2_5", 10.25)]
        public void Floats(string scalar, double expected)
        {
            Utf8YamlReader reader = ValueReader(scalar, s_yaml11);
            Assert.Equal(YamlTokenType.Number, reader.TokenType);
            Assert.Equal(expected, reader.GetDouble());
            Assert.True(reader.TryGetSingle(out float f));
            Assert.Equal((float)expected, f);
        }

        [Theory]
        [InlineData("190:20:30.15")]   // sexagesimal float: refused by both libraries
        [InlineData("1:60")]           // not a sexagesimal digit
        [InlineData("0b102")]
        [InlineData("_1")]
        [InlineData("yEs")]            // mixed case: not a 1.1 form (docs/feature-parity.md)
        [InlineData("NuLL")]
        [InlineData("0x")]
        public void NotResolved(string scalar)
        {
            Assert.Equal(YamlTokenType.String, ValueReader(scalar, s_yaml11).TokenType);
        }

        [Fact]
        public void Overflow()
        {
            Utf8YamlReader reader = ValueReader("0b" + new string('1', 70), s_yaml11);
            Assert.Equal(YamlTokenType.Number, reader.TokenType);
            Assert.False(reader.TryGetInt64(out _));
            Assert.False(reader.TryGetUInt64(out _));
            Assert.False(ValueReader("0777", s_yaml11).TryGetByte(out _));
            Assert.True(ValueReader("0377", s_yaml11).TryGetByte(out byte b));
            Assert.Equal(255, b);
        }

        [Fact]
        public void OnlyPlainValuesAreResolved()
        {
            var reader = new Utf8YamlReader("yes: no\n'on': \"off\"\nk: !!str y\nl: |\n  n\n"u8, s_yaml11);
            var tokens = new List<(YamlTokenType, string?)>();
            while (reader.Read())
            {
                tokens.Add((reader.TokenType, reader.TokenType is YamlTokenType.StartObject or YamlTokenType.EndObject ? null : reader.GetString()));
            }
            Assert.Equal(new (YamlTokenType, string?)[]
            {
                (YamlTokenType.StartObject, null),
                (YamlTokenType.PropertyName, "yes"), (YamlTokenType.False, "no"),
                (YamlTokenType.PropertyName, "on"), (YamlTokenType.String, "off"),
                (YamlTokenType.PropertyName, "k"), (YamlTokenType.String, "y"),
                (YamlTokenType.PropertyName, "l"), (YamlTokenType.String, "n\n"),
                (YamlTokenType.EndObject, null),
            }, tokens);
        }

        [Fact]
        public void CoreTagsAcceptThe11Forms()
        {
            Assert.Equal(YamlTokenType.True, ValueReader("!!bool yes", s_yaml11).TokenType);
            Assert.Equal(511, ValueReader("!!int 0777", s_yaml11).GetInt32());
            Assert.ThrowsAny<YamlException>(() => ValueReader("!!bool yes", default));
            Assert.ThrowsAny<YamlException>(() => ValueReader("!!int 0b1", default));
        }

        public class Settings
        {
            public bool Enabled { get; set; }
            public int Mode { get; set; }
            public string? Label { get; set; }
            public double Ratio { get; set; }
        }

        [Fact]
        public void SerializerThroughTheReader()
        {
            // Until M5 adds YamlSerializerOptions.Schema, the option is set on the reader.
            var reader = new Utf8YamlReader("Enabled: on\nMode: 0755\nLabel: no\nRatio: 1_0.5\n"u8, s_yaml11);
            Settings s = YamlSerializer.Deserialize<Settings>(ref reader)!;
            Assert.True(s.Enabled);
            Assert.Equal(493, s.Mode);
            Assert.Equal("no", s.Label);
            Assert.Equal(10.5, s.Ratio);

            // The same document with the default schema: 'on' is not a boolean.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Settings>("Enabled: on\n"));
        }

        [Fact]
        public void TheOptionIsCarriedByTheState()
        {
            byte[] yaml = "a: yes\nb: 0b11\n"u8.ToArray();
            ChunkingTests.AssertEverySplit(yaml, s_yaml11);
            var reader = new Utf8YamlReader(yaml, isFinalBlock: false, new YamlReaderState(s_yaml11));
            Assert.Equal(YamlSchemaKind.Yaml11, reader.CurrentState.Options.Schema);
        }

        [Fact]
        public void InvalidSchemaIsRejected()
        {
            var options = new YamlReaderOptions();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Schema = (YamlSchemaKind)4); // M5c: 2 and 3 are Json and Failsafe
            Assert.Equal(YamlSchemaKind.Core, options.Schema);
        }
    }
}
