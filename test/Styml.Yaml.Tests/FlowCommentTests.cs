// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5.0: with <see cref="YamlCommentHandling.Allow"/>, a comment at
    /// any position in a flow collection is a token, and the other tokens are those read with comments skipped. Before
    /// M5.0 a comment after a ',' (6HB6) or between a flow key and its ':' (K3WX) threw.
    /// </summary>
    public class FlowCommentTests
    {
        public static IEnumerable<object[]> Cases() => new[]
        {
            new object[] { "[ # c\n a, b]", "[ #c a b ]" },
            new object[] { "{ # c\n a: 1}", "{ #c a 1 }" },
            new object[] { "[a # c\n , b]", "[ a #c b ]" },
            new object[] { "[a, # c\n b]", "[ a #c b ]" },
            new object[] { "{a: 1, # c\n b: 2}", "{ a 1 #c b 2 }" },
            new object[] { "[a, # c\n # d\n b]", "[ a #c #d b ]" },
            new object[] { "[a, # c\n ]", "[ a #c ]" },
            new object[] { "{a: 1, # c\n }", "{ a 1 #c }" },
            new object[] { "{a # c\n : 1}", "{ a #c 1 }" },
            new object[] { "{\"a\" # c\n # d\n :1}", "{ a #c #d 1 }" },
            new object[] { "{a # c\n , b: 2}", "{ a #c ~ b 2 }" },
            new object[] { "{a: # c\n 1}", "{ a #c 1 }" },
            new object[] { "{a # c\n : # d\n , b: 2}", "{ a #c #d ~ b 2 }" },
            new object[] { "{? a # c\n : 1}", "{ a #c 1 }" },
            new object[] { "{[x] # c\n : 1}", "{ [x] #c 1 }" },
            new object[] { "[[a, # c\n b], # d\n {c: 1, # e\n }]", "[ [ a #c b ] #d { c 1 #e } ]" },
            new object[] { "k: [a, # c\n   b]\n", "{ k [ a #c b ] }" },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void CommentTokens_AtEveryFlowPosition(string yaml, string expected)
        {
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            Assert.Equal(expected, Tokens(input, YamlCommentHandling.Allow));
            Assert.Equal(expected.Replace(" #c", "").Replace(" #d", "").Replace(" #e", ""), Tokens(input, YamlCommentHandling.Skip));
            ChunkingTests.AssertEverySplit(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
        }

        [Theory]
        [InlineData("[a, # c\n , b]")]
        [InlineData("{a: 1, # c\n , b: 2}")]
        public void CommentAfterComma_ASecondCommaStillThrows(string yaml)
        {
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            Assert.Throws<YamlReaderException>(() => Tokens(input, YamlCommentHandling.Allow));
            Assert.Throws<YamlReaderException>(() => Tokens(input, YamlCommentHandling.Skip));
        }

        private static string Tokens(byte[] input, YamlCommentHandling comments)
        {
            var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = comments });
            var parts = new List<string>();
            while (reader.Read())
            {
                parts.Add(reader.TokenType switch
                {
                    YamlTokenType.StartArray => "[",
                    YamlTokenType.EndArray => "]",
                    YamlTokenType.StartObject => "{",
                    YamlTokenType.EndObject => "}",
                    YamlTokenType.Comment => "#" + reader.GetComment().Trim(),
                    YamlTokenType.Null => "~",
                    YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    _ => reader.GetString()!,
                });
            }

            return string.Join(" ", parts);
        }
    }
}
