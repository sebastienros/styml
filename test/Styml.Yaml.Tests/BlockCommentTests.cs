// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// With <see cref="YamlCommentHandling.Allow"/>, the comments the block grammar reads past while it looks for a
    /// node are tokens before that node: after properties alone on their line, after '- ' or an explicit key's ':' with
    /// the node on a later line, after a block scalar's header, and after '...'. The other tokens, and their positions,
    /// are those read with comments skipped.
    /// </summary>
    public class BlockCommentTests
    {
        public static IEnumerable<object[]> Cases() => new[]
        {
            // Properties alone on their line.
            new object[] { "base: &b # c\n  x: 1\n", "{ base #c &b{ x 1 } }" },
            new object[] { "base: &b\n  # c\n  x: 1\n", "{ base #c &b{ x 1 } }" },
            new object[] { "base: &b # c\n  # d\n\n  # e\n  x: 1\n", "{ base #c #d #e &b{ x 1 } }" },
            new object[] { "base: !!map # c\n  x: 1\n", "{ base #c { x 1 } }" },
            new object[] { "k: &a # c\n  v\n", "{ k #c &a:v }" },
            new object[] { "k: &a # c\n  - 1\n", "{ k #c &a[ 1 ] }" },
            new object[] { "k: &a # c\n  !!str # d\n  v\n", "{ k #c #d &a:v }" },
            new object[] { "&r # c\nx: 1\n", "#c &r{ x 1 }" },
            new object[] { "--- &r # c\nx: 1\n", "#c &r{ x 1 }" },
            new object[] { "- &a # c\n  x: 1\n", "[ #c &a{ x 1 } ]" },
            // The node is empty: the comment is read after it, as before.
            new object[] { "k: &a # c\nn: 1\n", "{ k &a:~ #c n 1 }" },

            // '- ' with the entry on a later line, or empty.
            new object[] { "- # c\n  x: 1\n", "[ #c { x 1 } ]" },
            new object[] { "- # c\n  # d\n  x: 1\n- y\n", "[ #c #d { x 1 } y ]" },
            new object[] { "- # c\n  v\n", "[ #c v ]" },
            new object[] { "- # c\n- b\n", "[ #c ~ b ]" },
            new object[] { "- a\n- # c\n", "[ a #c ~ ]" },
            new object[] { "k:\n- # c\n  - 1\n", "{ k [ #c [ 1 ] ] }" },

            // An explicit key's ':' with the value on a later line, or empty.
            new object[] { "? a\n: # c\n  v\n", "{ a #c v }" },
            new object[] { "? a\n: # c\n  - 1\n", "{ a #c [ 1 ] }" },
            new object[] { "? a\n: # c\nb: 1\n", "{ a #c ~ b 1 }" },
            new object[] { "? a # c\n: v\n", "{ a #c v }" },
            new object[] { "? # c\n  a\n: v\n", "{ #c a v }" },
            new object[] { "? # c\n  # d\n  - a\n: v\n", "{ #c #d - a v }" },
            new object[] { "? # c\n: v\n", "{ #c ~ v }" },
            new object[] { "? # c\n  'it''s'\n: v\n", "{ #c it's v }" },
            new object[] { "k: &a\n  # c\n  # d\n  # e\n  v\nl: 1\n", "{ k #c #d #e &a:v l 1 }" },

            // A block scalar's header.
            new object[] { "s: | # c\n  text\n", "{ s #c text\n }" },
            new object[] { "s: >- # c\n  text\n", "{ s #c text }" },
            new object[] { "- &a | # c\n  text\n", "[ #c &a:text\n ]" },
            new object[] { "| # c\n text\n", "#c text\n" },

            // The document end marker.
            new object[] { "a\n... # c\n", "a #c" },
            new object[] { "a\n... # c\n--- b\n", "a #c b" },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void CommentTokens_BeforeTheBlockNode(string yaml, string expected)
        {
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            var options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, AllowMultipleValues = true };
            Assert.Equal(expected, Tokens(input, options, out List<string> allowPositions));

            // Skipped, the same tokens without the comments, at the same positions.
            options.CommentHandling = YamlCommentHandling.Skip;
            Assert.Equal(StripComments(expected), Tokens(input, options, out List<string> skipPositions));
            Assert.Equal(skipPositions, allowPositions);

            options.CommentHandling = YamlCommentHandling.Allow;
            ChunkingTests.AssertEverySplit(input, options);
        }

        [Fact]
        public void CommentTokens_AfterAByteOrderMark()
        {
            byte[] input = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("&r # c\nx: 1\n")];
            var options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow };
            Assert.Equal("#c &r{ x 1 }", Tokens(input, options, out _));
            ChunkingTests.AssertEverySplit(input, options);
        }

        [Fact]
        public void CommentToken_IsAtItsBytes()
        {
            byte[] input = Encoding.UTF8.GetBytes("base: &b\n  # c\n  x: 1\n");
            var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.Comment, reader.TokenType);
            Assert.Equal(" c", reader.GetComment());
            Assert.Equal(11, reader.TokenStartIndex);

            // The node is read again from where the key ended.
            Assert.Equal(5, reader.BytesConsumed);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
            Assert.Equal("b", Encoding.UTF8.GetString(reader.Anchor.ToArray()));
        }

        private static string StripComments(string expected)
        {
            var parts = new List<string>();
            foreach (string part in expected.Split(' '))
            {
                if (!part.StartsWith("#", StringComparison.Ordinal))
                {
                    parts.Add(part);
                }
            }

            return string.Join(" ", parts);
        }

        private static string Tokens(byte[] input, YamlReaderOptions options, out List<string> positions)
        {
            var reader = new Utf8YamlReader(input, options);
            var parts = new List<string>();
            positions = new List<string>();
            while (reader.Read())
            {
                string anchor = reader.Anchor.IsEmpty ? "" : "&" + Encoding.UTF8.GetString(reader.Anchor.ToArray());
                string part = reader.TokenType switch
                {
                    YamlTokenType.StartArray => anchor + "[",
                    YamlTokenType.EndArray => "]",
                    YamlTokenType.StartObject => anchor + "{",
                    YamlTokenType.EndObject => "}",
                    YamlTokenType.Comment => "#" + reader.GetComment().Trim(),
                    YamlTokenType.Null => anchor.Length > 0 ? anchor + ":~" : "~",
                    YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    YamlTokenType.PropertyName when reader.ValueSpan.IsEmpty => "~",
                    _ => anchor.Length > 0 ? anchor + ":" + reader.GetString() : reader.GetString()!,
                };

                parts.Add(part);
                if (reader.TokenType != YamlTokenType.Comment)
                {
                    positions.Add($"{reader.TokenType}@{reader.TokenStartIndex}-{reader.BytesConsumed}");
                }
            }

            return string.Join(" ", parts);
        }
    }
}
