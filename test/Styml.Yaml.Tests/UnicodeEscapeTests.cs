// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Text;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public class UnicodeEscapeTests
    {
        [Theory]
        [InlineData("\"\\U1aaaaaaa\"")]
        [InlineData("\"\\U00110000\"")]
        [InlineData("\"\\U80000000\"")]
        [InlineData("\"\\UFFFFFFFF\"")]
        [InlineData("\"\\U0000D800\"")]
        [InlineData("\"\\U0000DFFF\"")]
        public void InvalidUnicode32Escape_ThrowsYamlException(string yaml)
        {
            Assert.ThrowsAny<YamlException>(() => YamlNode.Parse(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>(yaml));

            Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
                while (reader.Read())
                {
                }
            });
        }

        [Theory]
        [InlineData("k: 1\nv: \"ab\\U00110000\"\n")]
        [InlineData("k: 1\nv: \"ab\\U0000DC00\"\n")]
        [InlineData("k: 1\nv: \"ab\\U0000004G\"\n")]
        public void InvalidUnicode32Escape_ReportsTheEscapePosition(string yaml)
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
                while (reader.Read())
                {
                }
            });

            // The backslash of the escape, as for a non-hex digit.
            Assert.Equal(1, e.LineNumber);
            Assert.Equal(6, e.BytePositionInLine);
        }

        [Theory]
        [InlineData("\"\\uD83D\\uDE00\"", "\U0001F600")]
        [InlineData("\"\\x41\\uD83D\\uDE00\"", "A\U0001F600")]
        [InlineData("\"\\xFF\"", "\u00FF")]
        public void SurrogatePairAndHexByteEscapes_StillDecode(string yaml, string expected)
        {
            Assert.Equal(expected, YamlNode.Parse(yaml)?.GetValue<string>());
            Assert.Equal(expected, YamlSerializer.Deserialize<string>(yaml));
        }

        // A lone \u surrogate is still read as a token and rejected when the string is decoded, as in
        // System.Text.Json; \x forces the YAML unescaper rather than the JSON one.
        [Theory]
        [InlineData("\"\\x41\\uD800\"")]
        [InlineData("\"\\x41\\uDC00\"")]
        [InlineData("\"\\x41\\uD800\\x42\"")]
        public void LoneUnicode16SurrogateInYamlEscapes_ThrowsOnDecode(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.String, reader.TokenType);

            try
            {
                reader.GetString();
                Assert.Fail("GetString should throw.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [Theory]
        [InlineData("\"\\U00110000\"")]
        [InlineData("\"\\U0000D800\"")]
        public void InvalidUnicode32Escape_SplitInput_ThrowsYamlException(string yaml)
        {
            byte[] data = Encoding.UTF8.GetBytes(yaml);
            for (int split = 1; split < data.Length; split++)
            {
                Assert.ThrowsAny<YamlException>(() =>
                {
                    var first = new Utf8YamlReader(data.AsSpan(0, split), isFinalBlock: false, default);
                    while (first.Read())
                    {
                    }

                    var rest = new Utf8YamlReader(data.AsSpan((int)first.BytesConsumed), isFinalBlock: true, first.CurrentState);
                    while (rest.Read())
                    {
                    }
                });
            }
        }

        [Theory]
        [InlineData("\"\\U00000041\"", "A")]
        [InlineData("\"\\U0001F600\"", "\U0001F600")]
        [InlineData("\"\\U0010FFFF\"", "\U0010FFFF")]
        [InlineData("\"\\U0000D7FF\"", "\uD7FF")]
        [InlineData("\"\\U0000E000\"", "\uE000")]
        public void ValidUnicode32Escape_DecodesCorrectly(string yaml, string expected)
        {
            Assert.Equal(expected, YamlNode.Parse(yaml)?.GetValue<string>());

            using YamlDocument doc = YamlDocument.Parse(yaml);
            Assert.Equal(expected, doc.RootElement.GetString());

            Assert.Equal(expected, YamlSerializer.Deserialize<string>(yaml));
        }
    }
}
