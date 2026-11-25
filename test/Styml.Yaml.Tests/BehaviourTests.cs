// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>Decisions D3 to D7 and the document rules of the YAML grammar.</summary>
    public class BehaviourTests
    {
        private static List<(YamlTokenType Type, string Value)> Tokens(string yaml, YamlReaderOptions options = default)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tokens = new List<(YamlTokenType, string)>();
            while (reader.Read())
            {
                tokens.Add((reader.TokenType, Encoding.UTF8.GetString(reader.ValueSpan.ToArray())));
            }
            return tokens;
        }

        [Theory]
        [InlineData("")]
        [InlineData("\n\n")]
        [InlineData("   ")]
        [InlineData("# a comment\n")]
        [InlineData("\ufeff")]
        public void D4_EmptyInputYieldsNoTokens(string yaml)
        {
            Assert.Empty(Tokens(yaml));
        }

        [Fact]
        public void D4_EmptyInputYieldsNoTokens_ByteByByte()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes("# one\n\n# two\n");
            YamlReaderState state = default;
            int offset = 0;
            for (int end = 0; end <= utf8.Length; end++)
            {
                var reader = new Utf8YamlReader(utf8.AsSpan(offset, end - offset), end == utf8.Length, state);
                Assert.False(reader.Read());
                offset += (int)reader.BytesConsumed;
                state = reader.CurrentState;
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(" \n")]
        [InlineData("# only a comment\n")]
        public void D4_DocumentStillRejectsEmptyInput_NodesAndTheSerializerReadNull(string yaml)
        {
            // The reader yields no token; a document needs a value, so it keeps the JSON grammar's error (decision 2,
            // as ruled). A node and the serializer read zero tokens as a null (M5c).
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlElement.Parse(yaml));
            Assert.Null(System.Text.Yaml.Nodes.YamlNode.Parse(yaml));
            Assert.Null(YamlSerializer.Deserialize<object>(yaml));
            Assert.Null(YamlSerializer.Deserialize<System.Text.Yaml.Nodes.YamlNode>(yaml));
        }

        [Fact]
        public void ExplicitEmptyDocumentIsOneNull()
        {
            Assert.Equal(new[] { (YamlTokenType.Null, "") }, Tokens("---\n"));
            Assert.Equal(new[] { (YamlTokenType.Null, "") }, Tokens("%YAML 1.2\n--- # nothing\n...\n"));
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.1")]
        [InlineData("1.2")]
        [InlineData("1.3")]
        [InlineData("1.99")]
        public void YamlDirective_AnyMinorOfMajor1ReadsAsYaml12(string version)
        {
            // A 1.1 document is read with the 1.2 rules, and so is a later minor: 1.3 is an unreleased draft meant to
            // make no significant change to the language.
            const string Document = "--- [true, 0o17, 017, yes]\n";
            var expected = new[]
            {
                (YamlTokenType.StartArray, "["), (YamlTokenType.True, "true"), (YamlTokenType.Number, "0o17"),
                (YamlTokenType.Number, "017"), (YamlTokenType.String, "yes"), (YamlTokenType.EndArray, "]"),
            };
            Assert.Equal(expected, Tokens(Document));
            Assert.Equal(expected, Tokens("%YAML " + version + "\n" + Document));
        }

        [Fact]
        public void YamlDirective_DoesNotSelectTheSchema()
        {
            const string Yaml = "%YAML 1.1\n--- yes\n";
            Assert.Equal(new[] { (YamlTokenType.String, "yes") }, Tokens(Yaml));
            Assert.Equal(new[] { (YamlTokenType.True, "yes") }, Tokens(Yaml, new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11 }));
        }

        [Theory]
        [InlineData("2.0")]
        [InlineData("10.0")]
        [InlineData("0.9")]
        public void YamlDirective_AnotherMajorIsRefused(string version)
        {
            string yaml = "%YAML " + version + "\n--- a\n";
            YamlException e = Assert.ThrowsAny<YamlException>(() => Tokens(yaml));
            Assert.Contains("Unsupported YAML version '" + version + "'", e.Message);
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>(yaml));
        }

        [Fact]
        public void YamlDirective_Version13ReadsThroughEveryLayer()
        {
            const string Yaml = "%YAML 1.3\n---\na: 1\n";
            using (YamlDocument document = YamlDocument.Parse(Yaml))
            {
                Assert.Equal(1, document.RootElement.GetProperty("a").GetInt32());
            }
            Assert.Equal(1, System.Text.Yaml.Nodes.YamlNode.Parse(Yaml)!["a"]!.GetValue<int>());
            Assert.Equal(1, YamlSerializer.Deserialize<Dictionary<string, int>>(Yaml)!["a"]);
        }

        [Fact]
        public void D3_CommentsAreSkippedByDefaultAndYieldedWithAllow()
        {
            const string Yaml = "# head\na: 1 # trailing\n# tail\n";
            Assert.Equal(new[] { (YamlTokenType.StartObject, ""), (YamlTokenType.PropertyName, "a"), (YamlTokenType.Number, "1"), (YamlTokenType.EndObject, "") }, Tokens(Yaml));
            Assert.Equal(new[] { (YamlTokenType.StartObject, ""), (YamlTokenType.PropertyName, "a"), (YamlTokenType.Number, "1"), (YamlTokenType.EndObject, "") },
                Tokens(Yaml, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip }));

            var comments = Tokens(Yaml, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow })
                .Where(t => t.Type == YamlTokenType.Comment).Select(t => t.Value).ToArray();
            Assert.Equal(new[] { " head", " trailing", " tail" }, comments);
        }

        [Theory]
        [InlineData("a: //x\n", "//x")]
        [InlineData("a: /* x */\n", "/* x */")]
        [InlineData("a: b#c\n", "b#c")]
        public void CommentsNeedAHashAfterWhitespace(string yaml, string value)
        {
            Assert.Equal((YamlTokenType.String, value), Tokens(yaml)[2]);
        }

        [Fact]
        public void D3_TrailingCommasAreAlwaysAllowedInFlowCollections()
        {
            Assert.Equal(2, Tokens("[1, 2,]").Count(t => t.Type == YamlTokenType.Number));
            Assert.Single(Tokens("{a: 1,}"), t => t.Type == YamlTokenType.PropertyName);
        }

        [Fact]
        public void D3_AllowMultipleValuesMeansMultipleDocuments()
        {
            Assert.ThrowsAny<YamlException>(() => Tokens("a\n---\nb\n"));
            Assert.Equal(new[] { (YamlTokenType.String, "a"), (YamlTokenType.String, "b") },
                Tokens("a\n---\nb\n", new YamlReaderOptions { AllowMultipleValues = true }));
        }

        [Fact]
        public void Section8_ByteOrderMarkIsAcceptedAtTheStart()
        {
            Assert.Equal(new[] { (YamlTokenType.String, "a") }, Tokens("\ufeffa"));
            Assert.Equal(new[] { (YamlTokenType.StartObject, ""), (YamlTokenType.PropertyName, "a"), (YamlTokenType.Number, "1"), (YamlTokenType.EndObject, "") }, Tokens("\ufeffa: 1\n"));
        }

        [Fact]
        public void D5_AliasIsAToken()
        {
            var tokens = Tokens("a: &x 1\nb: *x\n");
            Assert.Equal((YamlTokenType.Alias, "x"), tokens[4]);
        }

        [Fact]
        public void D5_AnchorsAndTagsAreExposed()
        {
            var reader = new Utf8YamlReader("--- !!map &m\nk: !local &v value\n"u8);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
            Assert.Equal("m", Encoding.UTF8.GetString(reader.Anchor.ToArray()));
            Assert.Equal("!!map", Encoding.UTF8.GetString(reader.Tag.ToArray()));
            Assert.False(reader.IsFlow);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal("v", Encoding.UTF8.GetString(reader.Anchor.ToArray()));
            Assert.Equal("!local", Encoding.UTF8.GetString(reader.Tag.ToArray()));
            Assert.Equal(YamlScalarStyle.Plain, reader.ScalarStyle);
        }

        [Fact]
        public void D5_SerializerAndDocumentReadAliases()
        {
            // M5b: the serializer reads aliases. A scalar replays through
            // the site's converter; an object member reads a YamlElement, alias or not.
            Assert.Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 }, YamlSerializer.Deserialize<Dictionary<string, int>>("a: &x 1\nb: *x\n"));
            Assert.Equal(new[] { "a", "a" }, YamlSerializer.Deserialize<List<string>>("[&x a, *x]"));
            Assert.All(YamlSerializer.Deserialize<List<object>>("[&x 1, *x]")!, o => Assert.Equal(1, Assert.IsType<YamlElement>(o).GetInt32()));

            // The DOM reads them (M5a); an alias to no anchor is an error of the input.
            using (YamlDocument document = YamlDocument.Parse("[&x 1, *x]"))
            {
                Assert.Equal(1, document.RootElement[1].GetInt32());
            }

            Assert.Throws<YamlException>(() => YamlDocument.Parse("[1, *x]"));
        }

        [Fact]
        public void D5_AliasOnASkippedMember()
        {
            // An unmapped member is skipped token by token; an alias there carries no value to convert.
            var address = YamlSerializer.Deserialize<SerializerTests.Address>("City: A\nExtra: *x\n");
            Assert.Equal("A", address!.City);
            var strict = new YamlSerializerOptions { UnmappedMemberHandling = System.Text.Yaml.Serialization.YamlUnmappedMemberHandling.Disallow };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<SerializerTests.Address>("City: A\nExtra: *x\n", strict));
        }

        [Fact]
        public void D5_YamlNodeRejectsAliases()
        {
            // Since M5a a node reads YAML, and an alias is a copy of the anchored node.
            Assert.Equal(1, (int)System.Text.Yaml.Nodes.YamlNode.Parse("[&x 1, *x]")![1]!);
            Assert.Equal(1, (int)System.Text.Yaml.Nodes.YamlNode.Parse("a: 1\n")!["a"]!);
            Assert.Throws<YamlException>(() => System.Text.Yaml.Nodes.YamlNode.Parse("[1, *x]"));
            Assert.Equal(2, System.Text.Yaml.Nodes.YamlNode.Parse("[1, 2]")!.AsArray().Count);
        }

        [Fact]
        public void D5_SerializerIgnoresAnchorsAndNonCoreTags()
        {
            var d = YamlSerializer.Deserialize<Dictionary<string, string>>("a: &x !local one\nb: !!str two\n");
            Assert.Equal("one", d!["a"]);
            Assert.Equal("two", d["b"]);
        }

        [Theory]
        [InlineData("yes", YamlTokenType.String)]
        [InlineData("No", YamlTokenType.String)]
        [InlineData("on", YamlTokenType.String)]
        [InlineData("1:30", YamlTokenType.String)]
        [InlineData("0b101", YamlTokenType.String)]
        [InlineData("010", YamlTokenType.Number)]
        public void D6_NoYaml11Forms(string scalar, YamlTokenType expected)
        {
            Assert.Equal(expected, Tokens(scalar)[0].Type);
        }

        [Fact]
        public void D6_TagHandlesAreNotExpanded()
        {
            var reader = new Utf8YamlReader("%TAG !e! tag:example.com,2000:\n--- !e!thing x\n"u8);
            Assert.True(reader.Read());
            Assert.Equal("!e!thing", Encoding.UTF8.GetString(reader.Tag.ToArray()));
        }

        [Fact]
        public void D6_LongImplicitKeysAreAccepted()
        {
            string key = new string('k', 5000);
            Assert.Equal(key, Tokens($"{key}: v\n")[1].Value);
            Assert.Equal(key, Tokens($"{{\"{key}\": 1}}")[1].Value);
        }

        [Fact]
        public void D7_MultiSegmentInputReads()
        {
            // M3c: a multi-segment sequence reads under the YAML grammar (it threw NotSupportedException until then).
            ReadOnlySequence<byte> sequence = BufferFactory.Create("a: 1\nb"u8.ToArray(), ": 2\n"u8.ToArray());
            Assert.False(sequence.IsSingleSegment);
            var reader = new Utf8YamlReader(sequence);
            var types = new List<YamlTokenType>();
            while (reader.Read())
            {
                types.Add(reader.TokenType);
            }
            Assert.Equal(new[] { YamlTokenType.StartObject, YamlTokenType.PropertyName, YamlTokenType.Number, YamlTokenType.PropertyName, YamlTokenType.Number, YamlTokenType.EndObject }, types);
        }

        [Fact]
        public void D7_SingleSegmentSequenceTakesTheSpanPath()
        {
            var reader = new Utf8YamlReader(new ReadOnlySequence<byte>("a: 1\n"u8.ToArray()));
            var types = new List<YamlTokenType>();
            while (reader.Read())
            {
                types.Add(reader.TokenType);
            }
            Assert.Equal(new[] { YamlTokenType.StartObject, YamlTokenType.PropertyName, YamlTokenType.Number, YamlTokenType.EndObject }, types);
        }

        [Fact]
        public void YamlDocument_ReadsBlockYamlSinceM5a()
        {
            using (YamlDocument mapping = YamlDocument.Parse("a: 1\n"))
            {
                Assert.Equal(1, mapping.RootElement.GetProperty("a").GetInt32());
            }

            using (YamlDocument sequence = YamlDocument.Parse("- 1\n"))
            {
                Assert.Equal(1, sequence.RootElement.GetArrayLength());
            }

            using (YamlDocument plain = YamlDocument.Parse("plain"))
            {
                Assert.Equal("plain", plain.RootElement.GetString());
            }

            // Collection keys are read since M5e.
            using (YamlDocument complex = YamlDocument.Parse("[a]: 1\n"))
            {
                Assert.Equal(YamlKeyKind.Complex, complex.RootElement.EnumerateObject().Single().KeyKind);
            }
        }

        [Fact]
        public void YamlDocument_AcceptsJsonShapedInput()
        {
            using YamlDocument doc = YamlDocument.Parse("{\"a\": [1, true, null, \"s\"]}");
            Assert.Equal(4, doc.RootElement.GetProperty("a").GetArrayLength());
        }

        private static class BufferFactory
        {
            internal static ReadOnlySequence<byte> Create(params byte[][] segments)
            {
                var first = new Segment(segments[0], 0);
                Segment last = first;
                for (int i = 1; i < segments.Length; i++)
                {
                    last = last.Append(segments[i]);
                }
                return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
            }

            private sealed class Segment : ReadOnlySequenceSegment<byte>
            {
                public Segment(byte[] data, long runningIndex)
                {
                    Memory = data;
                    RunningIndex = runningIndex;
                }

                public Segment Append(byte[] data)
                {
                    var next = new Segment(data, RunningIndex + Memory.Length);
                    Next = next;
                    return next;
                }
            }
        }
    }
}
