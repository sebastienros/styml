// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// DeepEquals and the collection-key identity compare numbers by value at any size: an integer spelling by its decimal
    /// digits (not through a double, where every number past its range is infinity), a decimal spelling by System.Text.Json's
    /// digits comparison. The same on every target.
    /// </summary>
    public class NumberEqualityTests
    {
        private static readonly string s_sevens = new string('7', 20_000);

        private static bool Equal(string left, string right, YamlSchemaKind schema = YamlSchemaKind.Core)
        {
            var options = new YamlDocumentOptions { Schema = schema };
            using YamlDocument a = YamlDocument.Parse("a: " + left + "\n", options);
            using YamlDocument b = YamlDocument.Parse("a: " + right + "\n", options);
            Assert.Equal(YamlValueKind.Number, a.RootElement.GetProperty("a").ValueKind);
            Assert.Equal(YamlValueKind.Number, b.RootElement.GetProperty("a").ValueKind);
            return YamlElement.DeepEquals(a.RootElement, b.RootElement);
        }

        [Fact]
        public void TwentyThousandDigits_CompareByValue()
        {
            string sixes = s_sevens.Substring(0, s_sevens.Length - 1) + "6";

            // Decimal (JSON-spelled, and with leading zeros under the core schema).
            Assert.True(Equal(s_sevens, s_sevens));
            Assert.False(Equal(s_sevens, sixes));
            Assert.True(Equal("0" + s_sevens, s_sevens));
            Assert.False(Equal("0" + s_sevens, "0" + sixes));
            Assert.True(Equal("-0" + s_sevens, "-" + s_sevens));

            // Octal under YAML 1.1 (0777…7), against itself and its neighbour.
            Assert.True(Equal("0" + s_sevens, "0" + s_sevens, YamlSchemaKind.Yaml11));
            Assert.False(Equal("0" + s_sevens, "0" + sixes, YamlSchemaKind.Yaml11));
            Assert.False(Equal("0" + s_sevens, s_sevens, YamlSchemaKind.Yaml11));

            // Hex.
            string fs = new string('f', 5_000);
            Assert.True(Equal("0x" + fs, "0x" + fs.ToUpperInvariant()));
            Assert.False(Equal("0x" + fs, "0x" + fs.Substring(1) + "e"));
        }

        [Theory]
        [InlineData("0x10", "16", YamlSchemaKind.Core, true)]
        [InlineData("0o20", "16", YamlSchemaKind.Core, true)]
        [InlineData("+16", "16", YamlSchemaKind.Core, true)]
        [InlineData("016", "16", YamlSchemaKind.Core, true)]
        [InlineData("016", "14", YamlSchemaKind.Yaml11, true)]
        [InlineData("0b10000", "16", YamlSchemaKind.Yaml11, true)]
        [InlineData("1_6", "16", YamlSchemaKind.Yaml11, true)]
        [InlineData("1:30", "90", YamlSchemaKind.Yaml11, true)]
        [InlineData("-0x10", "-16", YamlSchemaKind.Yaml11, true)]
        [InlineData("0x10", "17", YamlSchemaKind.Core, false)]
        [InlineData(".5", "0.5", YamlSchemaKind.Core, true)]
        [InlineData("1.", "1.0", YamlSchemaKind.Core, true)]
        [InlineData("+1.5e3", "1500", YamlSchemaKind.Core, true)]
        [InlineData("1_0.5", "10.5", YamlSchemaKind.Yaml11, true)]
        [InlineData("0777.5", "777.5", YamlSchemaKind.Core, true)]
        [InlineData(".inf", ".Inf", YamlSchemaKind.Core, true)]
        [InlineData(".inf", "-.inf", YamlSchemaKind.Core, false)]
        [InlineData(".nan", ".NaN", YamlSchemaKind.Core, true)]
        [InlineData("0", "-0", YamlSchemaKind.Core, true)]
        [InlineData("0x0", "0", YamlSchemaKind.Core, true)]
        public void Spellings_CompareByValue(string left, string right, YamlSchemaKind schema, bool equal)
        {
            Assert.Equal(equal, Equal(left, right, schema));
            Assert.Equal(equal, Equal(right, left, schema));
        }

        [Fact]
        public void TheCollectionKeyIdentity_UsesTheSameRule()
        {
            string sixes = s_sevens.Substring(0, s_sevens.Length - 1) + "6";
            Assert.Contains("Duplicate", Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse("[0x10]: a\n[16]: b\n")).Message);
            // An implicit key is at most 1024 characters: the long ones are explicit.
            static string Key(string number, string value) => "? [" + number + "]\n: " + value + "\n";
            Assert.Contains("Duplicate", Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(Key("0" + s_sevens, "a") + Key(s_sevens, "b"))).Message);
            using YamlDocument distinct = YamlDocument.Parse(Key(s_sevens, "a") + Key(sixes, "b"));
            Assert.Equal(2, distinct.RootElement.GetPropertyCount());

            using YamlDocument a = YamlDocument.Parse(Key(s_sevens, "v"));
            using YamlDocument b = YamlDocument.Parse(Key("0" + s_sevens, "v"));
            using YamlDocument c = YamlDocument.Parse(Key(sixes, "v"));
            Assert.True(YamlElement.DeepEquals(a.RootElement, b.RootElement));
            Assert.False(YamlElement.DeepEquals(a.RootElement, c.RootElement));
        }
    }
}
