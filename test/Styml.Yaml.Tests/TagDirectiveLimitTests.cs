// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A document holds at most 256 <c>%TAG</c> directives (a redefinition counts as one); the 257th is an error, so a
    /// hostile stream of directives costs no more than that bound. The yaml-test-suite's most is 2.
    /// </summary>
    public class TagDirectiveLimitTests
    {
        private static string Directives(int count, Func<int, string> handle) =>
            string.Concat(Enumerable.Range(0, count).Select(i => "%TAG " + handle(i) + " tag:h" + i + ".com,2000:\n"));

        private static void ReadAll(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            while (reader.Read())
            {
            }
        }

        [Fact]
        public void TwoHundredFiftySixHandles_AreRead()
        {
            string yaml = Directives(256, i => "!h" + i + "!") + "--- !h255!x\na: !h0!y 1\n";
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            Assert.True(reader.Read());
            Assert.Equal("tag:h255.com,2000:x", reader.GetExpandedTag());
            ReadAll(yaml);
            Assert.Equal("tag:h0.com,2000:y", YamlDocument.Parse(yaml).RootElement.GetProperty("a").GetExpandedTag());
        }

        [Theory]
        [InlineData(257)]
        [InlineData(100_000)]
        public void MoreThan256_IsAnError(int count)
        {
            string yaml = Directives(count, i => "!h" + i + "!") + "---\na: 1\n";
            YamlException error = Assert.ThrowsAny<YamlException>(() => ReadAll(yaml));
            Assert.Contains("more than 256 %TAG directives", error.Message);
            Assert.Equal(256, error.LineNumber);
        }

        [Fact]
        public void ARedefinition_CountsAndTheLatestPrefixWins()
        {
            string yaml = Directives(256, i => "!h!") + "--- !h!x\n";
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            Assert.True(reader.Read());
            Assert.Equal("tag:h255.com,2000:x", reader.GetExpandedTag());
            Assert.ThrowsAny<YamlException>(() => ReadAll(Directives(257, i => "!h!") + "---\n"));
        }

        [Fact]
        public void TheLimitIsPerDocument()
        {
            string yaml = Directives(256, i => "!h" + i + "!") + "--- 1\n...\n" + Directives(256, i => "!k" + i + "!") + "--- !k0!x 2\n";
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal("tag:h0.com,2000:x", reader.GetExpandedTag());
        }
    }
}
