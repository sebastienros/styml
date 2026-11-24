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
