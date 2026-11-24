// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// JSON comments ('//' to the end of the line, '/* */') under <see cref="YamlCommentHandling.Skip"/> and
    /// <see cref="YamlCommentHandling.Allow"/>: read in flow collections and at the document level, never inside a
    /// block collection, and never with <see cref="YamlCommentHandling.Disallow"/>, the default.
    /// </summary>
    public class JsonCommentTests
    {
        // Input, the tokens with Allow, the tokens with Disallow as the reader gave them before JSON comments existed
        // (recorded from that build), and whether AllowMultipleValues is set. A comment token is '<text>@start'.
        public static IEnumerable<object[]> Cases() => new[]
        {
            new object[] { "{\"a\": 1 // c\n}", "{ a: 1 < c>@8 }", "{ a: '1 // c' }", false },
            new object[] { "{\"a\": 1, // c\n \"b\": 2}", "{ a: 1 < c>@9 b: 2 }", "{ a: 1 // c \"b\": 2 }", false },
            new object[] { "{\"a\": 1 /* c */}", "{ a: 1 < c >@8 }", "{ a: '1 /* c */' }", false },
            new object[] { "{\"a\": \"x\" // c\n}", "{ a: 'x' < c>@10 }", "{ a: 'x' !YamlReaderException", false },
            new object[] { "[1, 2] // c", "[ 1 2 ] < c>@7", "[ 1 2 ] !YamlReaderException", false },
            new object[] { "// c\n[1]", "< c>@0 [ 1 ]", "'// c [1]'", false },
            new object[] { "/* c */ [1] /* d */", "< c >@0 [ 1 ] < d >@12", "'/* c */ [1] /* d */'", false },
            new object[] { "{\"a\" /* k */ : /* v */ 1}", "{ a: < k >@5 < v >@15 1 }", "{ !YamlReaderException", false },
            new object[] { "[1 /* a */ , /* b */ 2]", "[ 1 < a >@3 < b >@13 2 ]", "[ '1 /* a */' '/* b */ 2' ]", false },
            new object[] { "[ // a\n [ // b\n 1 // c\n ] // d\n ]", "[ < a>@2 [ < b>@10 1 < c>@18 ] < d>@26 ]", "[ '// a' !YamlReaderException", false },
            new object[] { "{\"a\": {\"b\": [1, /* x */ 2]} // y\n}", "{ a: { b: [ 1 < x >@16 2 ] } < y>@28 }", "{ a: { b: [ 1 '/* x */ 2' ] } !YamlReaderException", false },
            new object[] { "[1, // c\r\n 2]\r\n", "[ 1 < c>@4 2 ]", "[ 1 '// c 2' ]", false },
            new object[] { "[1, # a\n 2, // b\n 3] # c", "[ 1 < a>@4 2 < b>@12 3 ] < c>@21", "[ 1 2 '// b 3' ]", false },
            new object[] { "[\"a // b\", 'c /* d */']", "[ 'a // b' 'c /* d */' ]", "[ 'a // b' 'c /* d */' ]", false },
            new object[] { "[http://x, a//b, a/*b*/c]", "[ 'http://x' 'a//b' 'a/*b*/c' ]", "[ 'http://x' 'a//b' 'a/*b*/c' ]", false },
            new object[] { "[a // b\n]", "[ 'a' < b>@3 ]", "[ 'a // b' ]", false },
            new object[] { "[a /* b */ c]", "[ 'a' < b >@3 !YamlReaderException", "[ 'a /* b */ c' ]", false },
            new object[] { "[1//c\n]", "[ 1 <c>@2 ]", "[ '1//c' ]", false },
            new object[] { "[true// c\n]", "[ true < c>@5 ]", "[ 'true// c' ]", false },
            new object[] { "[//c\n1]", "[ <c>@1 1 ]", "[ '//c 1' ]", false },
            new object[] { "{\"a\"://c\n1}", "{ a: <c>@5 1 }", "{ a: '//c 1' }", false },
            new object[] { "[/usr/bin, /]", "[ '/usr/bin' '/' ]", "[ '/usr/bin' '/' ]", false },
            new object[] { "[1, /* c", "[ 1 !YamlReaderException", "[ 1 '/* c' !YamlReaderException", false },
            new object[] { "[1] /* c", "[ 1 ] !YamlReaderException", "[ 1 ] !YamlReaderException", false },
            new object[] { "/*/ [1]", "!YamlReaderException", "'/*/ [1]'", false },
            new object[] { "// only", "< only>@0", "'// only'", false },
            new object[] { "/* a */", "< a >@0", "'/* a */'", false },
            new object[] { "/", "'/'", "'/'", false },
            new object[] { "[/", "[ '/' !YamlReaderException", "[ '/' !YamlReaderException", false },
            new object[] { "url: http://x // y\n", "{ url: 'http://x // y' }", "{ url: 'http://x // y' }", false },
            new object[] { "path: a // b\n# c\n", "{ path: 'a // b' < c>@13 }", "{ path: 'a // b' }", false },
            new object[] { "- a // b\n- /* c */\n", "[ 'a // b' '/* c */' ]", "[ 'a // b' '/* c */' ]", false },
            new object[] { "k: [1, // c\n  2]\n", "{ k: [ 1 < c>@7 2 ] }", "{ k: [ 1 '// c 2' ] }", false },
            new object[] { "k: [1] // c\n", "{ k: [ 1 ] !YamlReaderException", "{ k: [ 1 ] !YamlReaderException", false },
            new object[] { "k: [1, /* a\nb */ 2]\n", "{ k: [ 1 < a\\nb >@7 2 ] }", "{ k: [ 1 !YamlReaderException", false },
            new object[] { "k: [1,\n/* a */ 2]\n", "{ k: [ 1 < a >@7 2 ] }", "{ k: [ 1 !YamlReaderException", false },
            new object[] { "k: [1,\n// a\n 2]\n", "{ k: [ 1 < a>@7 2 ] }", "{ k: [ 1 !YamlReaderException", false },
            new object[] { "1 // c", "1 < c>@2", "'1 // c'", false },
            new object[] { "\"x\" // c", "'x' < c>@4", "'x' !YamlReaderException", false },
            new object[] { "true /* c */", "true < c >@5", "'true /* c */'", false },
            new object[] { "1\n// c\n", "1 < c>@2", "'1 // c'", false },
            new object[] { "a b // c\n d", "'a b' < c>@4 !YamlReaderException", "'a b // c d'", false },
            new object[] { "a // b: 1\n", "{ a // b: 1 }", "{ a // b: 1 }", false },
            new object[] { "// c\na: 1\n", "< c>@0 { a: 1 }", "!YamlReaderException", false },
            new object[] { "/* c */ a: 1\n", "< c >@0 { a: 1 }", "{ /* c */ a: 1 }", false },
            new object[] { "a: 1\n// c\n", "{ a: 1 !YamlReaderException", "{ a: 1 !YamlReaderException", false },
            new object[] { "{a // c\n : 1}", "{ a: < c>@3 1 }", "{ a // c: 1 }", false },
            new object[] { "{? a /* c */ : 1}", "{ a: < c >@5 1 }", "{ a /* c */: 1 }", false },
            new object[] { "{[x] // c\n : 1}", "{ K([x]) < c>@5 1 }", "{ !YamlReaderException", false },
            new object[] { "[&a /* c */ 1, !!str // c\n x]", "[ 1 'x' ]", "[ '/* c */ 1' '// c x' ]", false },
            new object[] { "[a, // c\n , b]", "[ 'a' < c>@4 !YamlReaderException", "[ 'a' '// c' 'b' ]", false },
            new object[] { "[1, // a\u2028b\n 2]", "[ 1 !YamlReaderException", "[ 1 '// a\\u2028b 2' ]", false },
            new object[] { "[1, /* a\u2028b */ 2]", "[ 1 < a\\u2028b >@4 2 ]", "[ 1 '/* a\\u2028b */ 2' ]", false },
            new object[] { "[1, 2 // ]: x\n]", "[ 1 2 < ]: x>@6 ]", "{ K([1, 2 // ]) 'x' !YamlReaderException", false },
            new object[] { "{\"a\": 1} // }: x", "{ a: 1 } < }: x>@9", "{ a: 1 } !YamlReaderException", false },
            new object[] { "[ /* ] */ 1]: v", "{ K([ /* ] */ 1]) 'v' }", "[ '/*' ] !YamlReaderException", false },
            new object[] { "[[ /* ] */ 1]: v]", "[ { K([ /* ] */ 1]) 'v' } ]", "{ !YamlReaderException", false },
            new object[] { "[a\n // c\n b]", "[ 'a' < c>@4 !YamlReaderException", "[ 'a // c b' ]", false },
            new object[] { "[a,\n// c\nb]", "[ 'a' < c>@4 'b' ]", "[ 'a' '// c b' ]", false },
            new object[] { "[1,\t// c\n2]", "[ 1 < c>@4 2 ]", "[ 1 '// c 2' ]", false },
            new object[] { "\"//\"", "'//'", "'//'", false },
            new object[] { "['/*', \"*/\"]", "[ '/*' '*/' ]", "[ '/*' '*/' ]", false },
            new object[] { "[1, /**/ 2]", "[ 1 <>@4 2 ]", "[ 1 '/**/ 2' ]", false },
            new object[] { "[1, /***/ 2]", "[ 1 <*>@4 2 ]", "[ 1 '/***/ 2' ]", false },
            new object[] { "[1, //\n 2]", "[ 1 <>@4 2 ]", "[ 1 '// 2' ]", false },
            new object[] { "{\"a\": /* c */}", "{ a: < c >@6 ~ }", "{ a: '/* c */' }", false },
            new object[] { "[/* c */]", "[ < c >@1 ]", "[ '/* c */' ]", false },
            new object[] { "{// c\n}", "{ < c>@1 }", "{ // c: ~ }", false },
            new object[] { "[1, 2] // c\r\n// d\r\n", "[ 1 2 ] < c>@7 < d>@13", "[ 1 2 ] !YamlReaderException", false },
            new object[] { "--- // c\n[1]\n", "< c>@4 [ 1 ]", "'// c [1]'", false },
            new object[] { "--- 1 // c\n", "1 < c>@6", "'1 // c'", false },
            new object[] { "[1] // c\n...\n", "[ 1 ] < c>@4", "[ 1 ] !YamlReaderException", false },
            new object[] { "|\n // x\n", "'// x\\n'", "'// x\\n'", false },
            new object[] { "{a: b // c\n, d: e /* f */}", "{ a: 'b' < c>@6 d: 'e' < f >@18 }", "{ a: 'b // c' d: 'e /* f */' }", false },
            new object[] { "[1, /* a\r\nb\rc\nd */ 2]", "[ 1 < a\\r\\nb\\rc\\nd >@4 2 ]", "[ 1 '/* a b c d */ 2' ]", false },
            new object[] { "[1] // a\n---\n// b\n[2]\n", "[ 1 ] < a>@4 < b>@13 [ 2 ]", "[ 1 ] !YamlReaderException", true },
            new object[] { "{\"a\": 1}\n// x\n--- /* y */\n{\"b\": 2}\n", "{ a: 1 } < x>@9 < y >@18 { b: 2 }", "{ a: 1 } !YamlReaderException", true },
            new object[] { "[1, 2]// c", "[ 1 2 ] < c>@6", "[ 1 2 ] !YamlReaderException", false },
            new object[] { "{\"a\":1}/* c */", "{ a: 1 } < c >@7", "{ a: 1 } !YamlReaderException", false },
            new object[] { "[1,2,/* c */]", "[ 1 2 < c >@5 ]", "[ 1 2 '/* c */' ]", false },
            new object[] { "{a: http://x/y, b: c:/* d */}", "{ a: 'http://x/y' b: 'c:/* d */' }", "{ a: 'http://x/y' b: 'c:/* d */' }", false },
            new object[] { "[a /b, c / d, e /]", "[ 'a /b' 'c / d' 'e /' ]", "[ 'a /b' 'c / d' 'e /' ]", false },
            new object[] { "[1//c\n, true/* c */, null// x\n, -1.5e3/**/, 01//c\n]", "[ 1 <c>@2 true < c >@12 ~ < x>@25 -1.5e3 <>@38 '01//c' ]", "[ '1//c' 'true/* c */' 'null// x' '-1.5e3/**/' '01//c' ]", false },
            new object[] { "{\"a\": 30//c\n}", "{ a: 30 <c>@8 }", "{ a: '30//c' }", false },
            new object[] { "0/**/", "0 <>@1", "'0/**/'", false },
            new object[] { "[1/2, 1/, 1//, tru//e\n, 1.//c\n]", "[ '1/2' '1/' 1 <, tru//e>@11 '1.//c' ]", "[ '1/2' '1/' '1//' 'tru//e' '1.//c' ]", false },
            new object[] { "[2020-01-01//c\n, 1 //c\n]", "[ '2020-01-01//c' 1 <c>@19 ]", "[ '2020-01-01//c' '1 //c' ]", false },
            new object[] { "{\"a\":1// c\n,\"b\":false/*x*/}", "{ a: 1 < c>@6 b: false <x>@21 }", "{ a: '1// c' b: 'false/*x*/' }", false },
            new object[] { "k: [1, // c\n2]\n", "{ k: [ 1 < c>@7 !YamlReaderException", "{ k: [ 1 !YamlReaderException", false },
            new object[] { "k: [1, /* c */\n2]\n", "{ k: [ 1 < c >@7 !YamlReaderException", "{ k: [ 1 !YamlReaderException", false },
            new object[] { "[1, // c\n---\n2]", "[ 1 < c>@4 !YamlReaderException", "[ 1 !YamlReaderException", false },
            new object[] { "[1, // c\n2]", "[ 1 < c>@4 2 ]", "[ 1 '// c 2' ]", false },
            new object[] { "k:\n  - [1, // c\n  2]\n", "{ k: [ [ 1 < c>@11 !YamlReaderException", "{ k: [ [ 1 !YamlReaderException", false },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void Allow_ReturnsCommentTokens(string yaml, string allow, string off, bool multiple)
        {
            _ = off;
            Assert.Equal(allow, Tokens(yaml, YamlCommentHandling.Allow, multiple));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Skip_ReadsTheSameTokensWithoutTheComments(string yaml, string allow, string off, bool multiple)
        {
            _ = off;
            string expected = Regex.Replace(allow, @"<[^>]*>@\d+ ?", "").TrimEnd();
            Assert.Equal(expected, Tokens(yaml, YamlCommentHandling.Skip, multiple));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Disallow_ReadsAsBefore(string yaml, string allow, string off, bool multiple)
        {
            _ = allow;
            Assert.Equal(off, Tokens(yaml, YamlCommentHandling.Disallow, multiple));
            Assert.Equal(off, Tokens(yaml, null, multiple));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void EverySplit_GivesTheOneShotTokens(string yaml, string allow, string off, bool multiple)
        {
            _ = allow;
            _ = off;
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            foreach (YamlCommentHandling handling in new[] { YamlCommentHandling.Disallow, YamlCommentHandling.Skip, YamlCommentHandling.Allow })
            {
                ChunkingTests.AssertEverySplit(input, new YamlReaderOptions { CommentHandling = handling, AllowMultipleValues = multiple });
            }
        }

        [Theory]
        [InlineData("// c", " c", 0, 4)]
        [InlineData("// c\n", " c", 0, 5)]
        [InlineData("// c\r\n1", " c", 0, 6)]
        [InlineData("//", "", 0, 2)]
        [InlineData("/**/", "", 0, 4)]
        [InlineData("/* c */ 1", " c ", 0, 7)]
        [InlineData("[1, /* a\nb */ 2]", " a\nb ", 4, 13)]
        [InlineData("[1, // é\n 2]", " é", 4, 10)]
        public void Allow_CommentToken_IsSystemTextJsons(string yaml, string text, int start, int consumed)
        {
            // As System.Text.Json: the value is the text without its delimiters, the token starts at the first '/',
            // and the token of a '//' comment consumes its line break.
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            while (reader.Read() && reader.TokenType != YamlTokenType.Comment)
            {
            }

            Assert.Equal(YamlTokenType.Comment, reader.TokenType);
            Assert.Equal(text, reader.GetComment());
            Assert.Equal(text, Encoding.UTF8.GetString(reader.ValueSpan.ToArray()));
            Assert.False(reader.HasValueSequence);
            Assert.Equal(start, reader.TokenStartIndex);
            Assert.Equal(consumed, reader.BytesConsumed);
        }

        [Theory]
        [InlineData("[1, /* a\nb */ @]", 1, 5)]
        [InlineData("[1, /* a\r\nb\rc */ @]", 2, 5)]
        [InlineData("[1, /* a */ @]", 0, 12)]
        [InlineData("[1, // a\n @]", 1, 1)]
        [InlineData("[1, /* a\n", 0, 4)]
        public void LinesAndColumns_CountThroughAComment(string yaml, long line, long column)
        {
            foreach (YamlCommentHandling handling in new[] { YamlCommentHandling.Skip, YamlCommentHandling.Allow })
            {
                YamlException e = Assert.ThrowsAny<YamlException>(() => Tokens(yaml, handling, false, rethrow: true));
                Assert.Equal(line, e.LineNumber);
                Assert.Equal(column, e.BytePositionInLine);
            }
        }

        [Fact]
        public void CommentAcrossSegments_IsAValueSequence()
        {
            byte[] input = Encoding.UTF8.GetBytes("[1, /* one two */ 2, // three\n 3]");
            for (int split = 0; split <= input.Length; split++)
            {
                SequenceFactory.Sequence sequence = SequenceFactory.Split(input, split);
                var reader = new Utf8YamlReader(sequence.Value, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
                var comments = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.Comment)
                    {
                        // The text between the delimiters lies in one segment (ValueSpan) or crosses the boundary.
                        bool crosses = reader.TokenStartIndex + 2 < split && split < reader.TokenStartIndex + 2 + reader.GetComment().Length;
                        Assert.Equal(crosses, reader.HasValueSequence);
                        comments.Add(reader.HasValueSequence ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray()) : Encoding.UTF8.GetString(reader.ValueSpan.ToArray()));
                        Assert.Equal(comments[comments.Count - 1], reader.GetComment());
                    }
                }

                Assert.Equal(new[] { " one two ", " three" }, comments);
            }
        }

        [Theory]
        [InlineData("[1, ", "/* c */ 2]")]
        [InlineData("[1, /", "* c */ 2]")]
        [InlineData("[1, /* c", " */ 2]")]
        [InlineData("[1, /* c *", "/ 2]")]
        [InlineData("[1, /", "/ c\n 2]")]
        [InlineData("[1, // c", "\n 2]")]
        [InlineData("[1, // c\r", "\n 2]")]
        [InlineData("[1 /", "/ c\n, 2]")]
        public void PartialData_WaitsForTheEndOfTheComment(string first, string rest)
        {
            foreach (YamlCommentHandling handling in new[] { YamlCommentHandling.Skip, YamlCommentHandling.Allow })
            {
                var options = new YamlReaderOptions { CommentHandling = handling };
                byte[] prefix = Encoding.UTF8.GetBytes(first);
                var reader = new Utf8YamlReader(prefix, isFinalBlock: false, new YamlReaderState(options));
                var tokens = new List<YamlTokenType>();
                while (reader.Read())
                {
                    tokens.Add(reader.TokenType);
                }

                // Nothing of an unfinished comment is consumed, and no token is made of it. A '//' comment whose
                // line ends in a CR is unfinished too: the LF of a CRLF may follow.
                int consumed = (int)reader.BytesConsumed;
                Assert.DoesNotContain(YamlTokenType.Comment, tokens);
                int slash = first.IndexOf('/');
                Assert.True(slash < 0 || consumed <= slash);

                byte[] remaining = Encoding.UTF8.GetBytes(first.Substring(consumed) + rest);
                reader = new Utf8YamlReader(remaining, isFinalBlock: true, reader.CurrentState);
                while (reader.Read())
                {
                    tokens.Add(reader.TokenType);
                    if (reader.TokenType == YamlTokenType.Comment)
                    {
                        Assert.Equal(" c", reader.GetComment().TrimEnd());
                    }
                }

                var expected = new List<YamlTokenType> { YamlTokenType.StartArray, YamlTokenType.Number, YamlTokenType.Number, YamlTokenType.EndArray };
                if (handling == YamlCommentHandling.Allow)
                {
                    expected.Insert(2, YamlTokenType.Comment);
                }
                Assert.Equal(expected, tokens);
            }
        }

        [Fact]
        public void PartialData_ALineCommentWithoutItsLineBreak_IsACommentOnlyInTheFinalBlock()
        {
            byte[] input = Encoding.UTF8.GetBytes("[1] // c");
            var options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow };
            var reader = new Utf8YamlReader(input, isFinalBlock: false, new YamlReaderState(options));
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.False(reader.Read());
            Assert.Equal(3, reader.BytesConsumed);

            reader = new Utf8YamlReader(input.AsSpan(3), isFinalBlock: true, reader.CurrentState);
            Assert.True(reader.Read());
            Assert.Equal(" c", reader.GetComment());
            Assert.False(reader.Read());
        }

        [Theory]
        [InlineData("[&a # c\n b]", "[ 'b' ]")]
        [InlineData("[!!str # c\n b]", "[ 'b' ]")]
        [InlineData("{? # c\n a: 1}", "{ a: 1 }")]
        [InlineData("[&a /* c */ b, !!str // d\n e]", "[ 'b' 'e' ]")]
        [InlineData("k: [a,\n# c\n  b]\n", "{ k: [ 'a' < c>@7 'b' ] }")]
        public void Allow_ReadsWhatSkipReads(string yaml, string allow)
        {
            // A comment between a node's properties (or '?') and its content has no token: it is skipped, also
            // with Allow. A comment at the start of a line inside a flow collection is not content, so the line's
            // indentation is not checked at it. Both threw with Allow before, for '#' comments.
            Assert.Equal(allow, Tokens(yaml, YamlCommentHandling.Allow, false));
            Assert.Equal(Regex.Replace(allow, @"<[^>]*>@\d+ ?", ""), Tokens(yaml, YamlCommentHandling.Skip, false));
            ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
        }

        // The serializer, the document and the nodes skip, as System.Text.Json's do.

        private const string Payload = """
            // the service
            {
              "name": "web", // its name
              "ports": [80, /* tls */ 443],
              "url": "http://example.org/a//b" /* slashes in a string are text */
            }
            """;

        public sealed class Service
        {
            public string? name { get; set; }
            public int[]? ports { get; set; }
            public string? url { get; set; }
        }

        [Fact]
        public void Serializer_Skip_ReadsAJsonPayloadWithComments()
        {
            var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };
            Service service = YamlSerializer.Deserialize<Service>(Payload, options)!;
            Assert.Equal("web", service.name);
            Assert.Equal(new[] { 80, 443 }, service.ports);
            Assert.Equal("http://example.org/a//b", service.url);

            Dictionary<string, object> map = YamlSerializer.Deserialize<Dictionary<string, object>>(Payload, options)!;
            Assert.Equal(3, map.Count);
        }

        [Fact]
        public async Task Serializer_Skip_ReadsAStreamInSmallBuffers()
        {
            var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip, DefaultBufferSize = 1 };
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Payload));
            Service service = (await YamlSerializer.DeserializeAsync<Service>(stream, options))!;
            Assert.Equal("web", service.name);
            Assert.Equal(new[] { 80, 443 }, service.ports);
        }

        [Fact]
        public void Serializer_Default_DoesNotReadJsonComments()
        {
            // Standard YAML: the '//' line and the object's first key make one plain scalar that spans lines.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Service>(Payload));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Service>(Payload, new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Disallow }));

            // In a value, the text is the value.
            Dictionary<string, string> map = YamlSerializer.Deserialize<Dictionary<string, string>>("{\"a\": b // c\n}")!;
            Assert.Equal("b // c", map["a"]);
        }

        [Fact]
        public void Serializer_Skip_BlockYamlKeepsItsSlashes()
        {
            var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };
            Dictionary<string, object> map = YamlSerializer.Deserialize<Dictionary<string, object>>(
                "url: http://x // y\npath: a /* b */ c\nlist:\n  - d // e\nflow: [f // g\n  , h//i]\n", options)!;
            Assert.Equal("http://x // y", map["url"].ToString());
            Assert.Equal("a /* b */ c", map["path"].ToString());
            Assert.Equal("d // e", ((YamlElement)map["list"])[0].GetString());
            Assert.Equal("f", ((YamlElement)map["flow"])[0].GetString());
            Assert.Equal("h//i", ((YamlElement)map["flow"])[1].GetString());
        }

        public sealed record Named(string Name, string[] Tags);

        [Fact]
        public void Serializer_Skip_ReadersOverASliceOfBlockYamlKeepItsSlashes()
        {
            // The serializer, the alias replay and the DOM read values again through readers over a slice of the
            // document. Such a reader starts at depth 0 inside a block collection: it is not at the document level.
            var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };

            Dictionary<string, string> aliased = YamlSerializer.Deserialize<Dictionary<string, string>>("a: &x b // c\nd: *x\n", options)!;
            Assert.Equal("b // c", aliased["a"]);
            Assert.Equal("b // c", aliased["d"]);

            // A flow collection keeps its comments when it is replayed.
            Dictionary<string, int[]> flow = YamlSerializer.Deserialize<Dictionary<string, int[]>>("a: &x [1, /* c */ 2] # d\nb: *x\n", options)!;
            Assert.Equal(new[] { 1, 2 }, flow["a"]);
            Assert.Equal(new[] { 1, 2 }, flow["b"]);

            // A constructor's arguments are read again from where each was found.
            Named named = YamlSerializer.Deserialize<Named>("Tags: [a // t\n  , b//c]\nName: n // m\n", options)!;
            Assert.Equal("n // m", named.Name);
            Assert.Equal(new[] { "a", "b//c" }, named.Tags);

            var documentOptions = new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Skip };
            using YamlDocument document = YamlDocument.Parse("k: b // c\nf: [1, /* c */ 2]\nl:\n  - d /* e */\n", documentOptions);
            Assert.Equal("b // c", document.RootElement.GetProperty("k").Deserialize<string>(options));
            Assert.Equal("b // c", document.RootElement.GetProperty("k").Deserialize<string>());
            Assert.Equal(new[] { 1, 2 }, document.RootElement.GetProperty("f").Deserialize<int[]>(options));
            Assert.Equal(new[] { "d /* e */" }, document.RootElement.GetProperty("l").Deserialize<string[]>(options));

            YamlNode node = YamlNode.Parse("k: b // c\nf: [1, /* c */ 2]\n", documentOptions: documentOptions)!;
            Assert.Equal("b // c", node["k"].Deserialize<string>(options));
            Assert.Equal(new[] { 1, 2 }, node["f"].Deserialize<int[]>(options));
        }

        [Fact]
        public void Reread_FollowsHowTheDocumentWasRead_NotTheOptionsOfTheReread()
        {
            var skip = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };

            // Read with JSON comments: an element's source has them, whatever options deserialize it.
            using (YamlDocument document = YamlDocument.Parse("// lead\n{\"f\": [1, /* c */ 2], \"g\": 3 // t\n}", new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Skip }))
            {
                Assert.Equal(new[] { 1, 2 }, document.RootElement.GetProperty("f").Deserialize<int[]>());
                Assert.Equal(new[] { 1, 2 }, document.RootElement.GetProperty("f").Deserialize<int[]>(skip));
                Assert.Equal(3, document.Deserialize<Dictionary<string, object>>()!.Count == 2 ? document.RootElement.GetProperty("g").Deserialize<int>() : -1);
                Assert.Equal(2, document.RootElement.Deserialize<Dictionary<string, YamlElement>>(skip)!.Count);
            }

            // Read without: its slashes are text, also when it is deserialized with Skip.
            using (YamlDocument document = YamlDocument.Parse("f: [a // b\n  , c]\ns: d // e\n"))
            {
                Assert.Equal(new[] { "a // b", "c" }, document.RootElement.GetProperty("f").Deserialize<string[]>(skip));
                Assert.Equal("d // e", document.RootElement.GetProperty("s").Deserialize<string>(skip));
                Assert.Equal("d // e", document.Deserialize<Dictionary<string, object>>(skip)!["s"].ToString());
            }

            // A node is written, then read: what the writer wrote has no JSON comments.
            Assert.Equal("b // c", YamlValue.Create("b // c").Deserialize<string>(skip));
            Assert.Equal(new[] { "b // c", "/* d */" }, new YamlArray("b // c", "/* d */").Deserialize<string[]>(skip));
            var flow = new YamlArray("b // c", "1 /* d */") { CollectionStyle = YamlCollectionStyle.Flow };
            Assert.Equal(new[] { "b // c", "1 /* d */" }, flow.Deserialize<string[]>(skip));

            // Merge keys are resolved by writing the input again.
            var merge = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip, ResolveMergeKeys = true };
            Dictionary<string, Dictionary<string, string>> merged = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                "a: &b\n  x: p // q\nd:\n  <<: *b\n  y: [r // s\n    ] # t\n".Replace("y: [r // s\n    ] # t", "y: u /* v */"), merge)!;
            Assert.Equal("p // q", merged["d"]["x"]);
            Assert.Equal("u /* v */", merged["d"]["y"]);
        }

        [Fact]
        public void Reader_Skip_AValueReadFromInsideABlockMappingKeepsItsSlashes()
        {
            byte[] input = Encoding.UTF8.GetBytes("k: b // c\nf: [1, // c\n  2]\n");
            var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            using (YamlDocument value = YamlDocument.ParseValue(ref reader))
            {
                Assert.Equal("b // c", value.RootElement.GetString());
            }

            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(new[] { 1, 2 }, YamlSerializer.Deserialize<int[]>(ref reader));

            reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal("b // c", YamlSerializer.Deserialize<string>(ref reader));
        }

        [Fact]
        public void Serializer_Allow_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Allow });
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Allow });
        }

        [Fact]
        public void Serializer_ReaderWithAllow_IsRefused()
        {
            // As System.Text.Json: a reader that returns comment tokens cannot feed the serializer or a document.
            byte[] input = Encoding.UTF8.GetBytes("[1, /* c */ 2]");
            Assert.Throws<ArgumentException>(() =>
            {
                var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
                YamlSerializer.Deserialize<int[]>(ref reader);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
                YamlDocument.ParseValue(ref reader);
            });

            var skip = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip });
            Assert.Equal(new[] { 1, 2 }, YamlSerializer.Deserialize<int[]>(ref skip));
        }

        [Fact]
        public void Document_Skip_ReadsAJsonPayloadWithComments()
        {
            using YamlDocument document = YamlDocument.Parse(Payload, new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Skip });
            Assert.Equal("web", document.RootElement.GetProperty("name").GetString());
            Assert.Equal(443, document.RootElement.GetProperty("ports")[1].GetInt32());
            Assert.Equal("http://example.org/a//b", document.RootElement.GetProperty("url").GetString());

            // The raw text of a collection keeps its comments, and reads again under the same option.
            string raw = document.RootElement.GetProperty("ports").GetRawText();
            Assert.Equal("[80, /* tls */ 443]", raw);

            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(Payload));
        }

        [Fact]
        public void Node_Skip_ReadsAJsonPayloadWithComments()
        {
            YamlNode node = YamlNode.Parse(Payload, documentOptions: new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Skip })!;
            Assert.Equal("web", (string?)node["name"]);
            Assert.Equal(443, (int)node["ports"]![1]!);

            Assert.ThrowsAny<YamlException>(() => YamlNode.Parse(Payload));
        }

        [Fact]
        public void ComplexKey_ItsReaderReadsCommentsAsTheReaderDoes()
        {
            byte[] input = Encoding.UTF8.GetBytes("{[a // c\n , b]: 1}");
            foreach (YamlCommentHandling handling in new[] { YamlCommentHandling.Skip, YamlCommentHandling.Allow })
            {
                var reader = new Utf8YamlReader(input, new YamlReaderOptions { CommentHandling = handling });
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
                Utf8YamlReader key = reader.CreateKeyReader();
                var parts = new List<string>();
                while (key.Read())
                {
                    parts.Add(key.TokenType is YamlTokenType.String ? key.GetString()! : key.TokenType.ToString());
                }
                Assert.Equal(
                    handling == YamlCommentHandling.Allow ? new[] { "StartArray", "a", "Comment", "b", "EndArray" } : new[] { "StartArray", "a", "b", "EndArray" },
                    parts);
            }

            // Off: the key's first entry is the plain scalar 'a // c'.
            var off = new Utf8YamlReader(input);
            Assert.True(off.Read());
            Assert.True(off.Read());
            Utf8YamlReader offKey = off.CreateKeyReader();
            Assert.True(offKey.Read());
            Assert.True(offKey.Read());
            Assert.Equal("a // c", offKey.GetString());
        }

        [Fact]
        public void Writer_RawValue_KeepsSlashesAsText()
        {
            // WriteRawValue reads its text for '#' comments with the library's own reader: '//' stays content.
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("k");
                writer.WriteRawValue("[a // b, c /* d */ e]");
                writer.WriteEndObject();
            }

            Dictionary<string, string[]> map = YamlSerializer.Deserialize<Dictionary<string, string[]>>(buffer.WrittenSpan)!;
            Assert.Equal(new[] { "a // b", "c /* d */ e" }, map["k"]);
        }

        private static string Tokens(string yaml, YamlCommentHandling? comments, bool multiple, bool rethrow = false)
        {
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            var parts = new List<string>();
            try
            {
                var options = new YamlReaderOptions { AllowMultipleValues = multiple };
                if (comments is { } handling)
                {
                    options.CommentHandling = handling;
                }

                var reader = new Utf8YamlReader(input, options);
                while (reader.Read())
                {
                    parts.Add(reader.TokenType switch
                    {
                        YamlTokenType.StartArray => "[",
                        YamlTokenType.EndArray => "]",
                        YamlTokenType.StartObject => "{",
                        YamlTokenType.EndObject => "}",
                        YamlTokenType.Comment => "<" + reader.GetComment() + ">@" + reader.TokenStartIndex,
                        YamlTokenType.Null => "~",
                        YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex => "K(" + Encoding.UTF8.GetString(reader.ValueSpan.ToArray()) + ")",
                        YamlTokenType.PropertyName => reader.GetString() + ":",
                        YamlTokenType.String => "'" + reader.GetString() + "'",
                        _ => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    });
                }
            }
            catch (YamlException e) when (!rethrow)
            {
                parts.Add("!" + e.GetType().Name);
            }

            return string.Join(" ", parts).Replace("\n", "\\n").Replace("\r", "\\r").Replace("\u2028", "\\u2028");
        }
    }
}
