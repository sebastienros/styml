// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A <see cref="YamlDocument"/> read with <see cref="YamlCommentHandling.Allow"/> keeps its comments, and a block
    /// writer writes them back where they were relative to the nodes. Every round trip is also a fixed point: the
    /// written text, read and written again, is the same text.
    /// </summary>
    public class DocumentCommentTests
    {
        private static readonly YamlDocumentOptions s_allow = new() { CommentHandling = YamlCommentHandling.Allow };

        public static IEnumerable<object[]> RoundTrips() => new[]
        {
            // The top and the bottom of the file are the root mapping's; blank lines are not kept.
            new object[] { "# header\n\n# second\nname: web # n\n# before port\nport: 80\n# footer\n", "# header\n# second\nname: web # n\n# before port\nport: 80\n# footer\n" },
            new object[] { "- a\n# between\n- b # after b\n", "- a\n# between\n- b # after b\n" },
            new object[] { "key:\n  # c\n  x: 1\n", "key:\n  # c\n  x: 1\n" },
            // After a key, the writer puts the comment on the next line.
            new object[] { "key: # opens\n  x: 1\n", "key:\n  # opens\n  x: 1\n" },
            new object[] { "key:\n  # c\n  value\n", "key:\n  # c\n  value\n" },
            new object[] { "outer:\n  inner: 1\n  # inner tail\n# outer tail\n", "outer:\n  inner: 1\n  # inner tail\n# outer tail\n" },
            new object[] { "a:\n- x\n# indentless\nb: 1\n", "a:\n- x\n# indentless\nb: 1\n" },
            new object[] { "a: [1, # in flow\n  2]\n", "a: [1, # in flow\n  2]\n" },
            new object[] { "b: {x: 1} # flow\nc: 2\n", "b: {x: 1} # flow\nc: 2\n" },
            new object[] { "a: &x 1 # anchored\nb: !t 2 # tagged\nc: *x # alias\n", "a: &x 1 # anchored\nb: !t 2 # tagged\nc: *x # alias\n" },
            new object[] { "s: |\n  text\n# after block\nt: 1\n", "s: |\n  text\n# after block\nt: 1\n" },
            new object[] { "- a\n# above item\n- x: 1\n  z: 2\n", "- a\n# above item\n- x: 1\n  z: 2\n" },
            new object[] { "#no space\n#  two spaces\nk: v\n", "# no space\n#  two spaces\nk: v\n" },
            // A scalar root: the writer can't end the root's line with a comment.
            new object[] { "# only scalar\nhello # eol\n# tail\n", "# only scalar\nhello\n# eol\n# tail\n" },
            new object[] { "[1, 2]\n# after flow root\n", "[1, 2]\n# after flow root\n" },
        };

        [Theory]
        [MemberData(nameof(RoundTrips))]
        public void Comments_AreWrittenBack(string yaml, string expected)
        {
            using YamlDocument document = YamlDocument.Parse(yaml, s_allow);
            string written = Write(document.RootElement);
            Assert.Equal(expected, written);
            AssertFixedPoint(written);

            using YamlDocument skipped = YamlDocument.Parse(yaml);
            Assert.True(YamlElement.DeepEquals(skipped.RootElement, document.RootElement));
            Assert.Equal(written, Write(document));
        }

        [Fact]
        public void DedentedComments_GoToTheirCollection()
        {
            string yaml = "a:\n  - x\n  - y\n  # in seq\n# top level\nb: 1\n";
            using YamlDocument document = YamlDocument.Parse(yaml, s_allow);
            string written = Write(document.RootElement, new YamlWriterOptions { IndentSequences = true });
            Assert.Equal(yaml, written);
        }

        [Fact]
        public void JsonComments_AreWrittenAsYamlComments()
        {
            using YamlDocument document = YamlDocument.Parse("{\"a\": 1, // json\n \"b\": [2, /* two */ 3]}\n", s_allow);
            string written = Write(document.RootElement);
            Assert.Contains("# json", written);
            Assert.Contains("# two", written);
            AssertFixedPoint(written);
        }

        [Fact]
        public void JsonCompatibleWriter_WritesNoComments()
        {
            using YamlDocument document = YamlDocument.Parse("# h\nname: web # n\nlist: [1, # one\n  2]\n# f\n", s_allow);
            Assert.Equal("{\"name\":\"web\",\"list\":[1,2]}", Write(document.RootElement, style: YamlWriterStyle.JsonCompatible));
        }

        [Theory]
        [InlineData(YamlCommentHandling.Disallow)]
        [InlineData(YamlCommentHandling.Skip)]
        public void OtherHandlings_KeepNoComments(YamlCommentHandling handling)
        {
            using YamlDocument document = YamlDocument.Parse("# h\nname: web # n\n# f\n", new YamlDocumentOptions { CommentHandling = handling });
            Assert.Equal("name: web\n", Write(document.RootElement));
        }

        [Fact]
        public void Element_WritesTheCommentsInsideIt()
        {
            string yaml = "outer: # o\n  # start\n  inner: 1 # i\n  # end\nnext: 2\n";
            using YamlDocument document = YamlDocument.Parse(yaml, s_allow);
            YamlElement outer = document.RootElement.GetProperty("outer");

            // The slot's own comment ("# o", after the key) belongs to the mapping that holds it.
            Assert.Equal("# start\ninner: 1 # i\n# end\n", Write(outer));
            Assert.Equal("# start\ninner: 1 # i\n# end\n", Write(outer.Clone()));

            // The raw text is the source's, as without the option.
            using YamlDocument skipped = YamlDocument.Parse(yaml);
            Assert.Equal(skipped.RootElement.GetProperty("outer").GetRawText(), outer.GetRawText());
        }

        [Fact]
        public void Clone_WithAnAliasOutside_KeepsTheComments()
        {
            using YamlDocument document = YamlDocument.Parse("base: &b 1\nobj:\n  # s\n  a: *b # c\n", s_allow);
            YamlElement clone = document.RootElement.GetProperty("obj").Clone();
            // The expanded alias redefines its anchor, as without comments.
            Assert.Equal("# s\na: &b 1 # c\n", Write(clone));
        }

        [Fact]
        public void Root_Clone_KeepsTheDocumentComments()
        {
            using YamlDocument document = YamlDocument.Parse("# top\nhello # eol\n", s_allow);
            Assert.Equal("# top\nhello\n# eol\n", Write(document.RootElement.Clone()));
        }

        [Fact]
        public void Property_WritesItsEntryComments()
        {
            // A comment above the first key would be the mapping's Start, which the mapping writes.
            using YamlDocument document = YamlDocument.Parse("a: 1 # one\n# above b\nb: # bk\n  x: 2\n", s_allow);
            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                writer.WriteStartObject();
                foreach (YamlProperty property in document.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            Assert.Equal("a: 1 # one\n# above b\nb:\n  # bk\n  x: 2\n", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public void Alias_ExpandedInAWrite_WritesNoCommentsOfTheAnchoredNode()
        {
            using YamlDocument document = YamlDocument.Parse("base: &b\n  x: 1 # x\nobj:\n  d: *b # d\n", s_allow);

            // Inside what is written, the alias stays an alias.
            Assert.Equal("base: &b\n  x: 1 # x\nobj:\n  d: *b # d\n", Write(document.RootElement));

            // Its anchor outside, the alias is expanded: a copy, with the alias's own comment only, which the writer
            // can't put at the end of a block mapping's line.
            Assert.Equal("d: &b\n  x: 1\n# d\n", Write(document.RootElement.GetProperty("obj")));
        }

        [Fact]
        public void MergeKeys_CopiedEntriesCarryNoComments()
        {
            string yaml = "base: &b\n  x: 1 # x\nderived:\n  <<: *b\n  # own\n  z: 2 # z\n";
            using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Allow, ResolveMergeKeys = true });
            string written = Write(document.RootElement);
            Assert.Equal("base: &b\n  x: 1 # x\nderived:\n  x: 1\n  # own\n  z: 2 # z\n", written);
            AssertFixedPoint(written);
        }

        [Fact]
        public void CommentsOnly_IsStillNoValue()
        {
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse("# nothing\n", s_allow));
        }

        [Fact]
        public void Reader_WithAllow_IsStillRefused()
        {
            var reader = new Utf8YamlReader("a: 1 # c\n"u8, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            try
            {
                YamlDocument.ParseValue(ref reader);
                Assert.Fail("Expected an ArgumentException.");
            }
            catch (ArgumentException e)
            {
                Assert.Contains("Allow", e.Message);
            }
        }

        /// <summary>
        /// Comments the reader does not return as tokens, so the document has none to keep: on a block scalar's header,
        /// between a node's anchor or tag alone on its line and the node, and after a sequence entry's indicator above
        /// the entry's content.
        /// </summary>
        [Theory]
        [InlineData("s: | # header\n  text\n", "s: |\n  text\n")]
        [InlineData("base: &b # after anchor\n  x: 1\n", "base: &b\n  x: 1\n")]
        [InlineData("base: &b\n  # below anchor\n  x: 1\n", "base: &b\n  x: 1\n")]
        [InlineData("- # after dash\n  x: 1\n", "- x: 1\n")]
        public void ReaderGaps_AreNotKept(string yaml, string expected)
        {
            using YamlDocument document = YamlDocument.Parse(yaml, s_allow);
            Assert.Equal(expected, Write(document.RootElement));
        }

        private static void AssertFixedPoint(string written)
        {
            using YamlDocument again = YamlDocument.Parse(written, s_allow);
            Assert.Equal(written, Write(again.RootElement));
        }

        // The style is always set: the JSON-compatible test leg changes the writer's default.
        private static string Write(YamlElement element, YamlWriterOptions options = default, YamlWriterStyle style = YamlWriterStyle.Block)
        {
            options.NewLine = "\n";
            options.Style = style;

            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, options))
            {
                element.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string Write(YamlDocument document)
        {
            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                document.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
