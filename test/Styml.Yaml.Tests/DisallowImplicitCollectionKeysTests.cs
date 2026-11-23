// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using StjReader = System.Text.Json.Utf8JsonReader;
using StjState = System.Text.Json.JsonReaderState;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// YamlReaderOptions.DisallowImplicitCollectionKeys (and the same option on
    /// YamlSerializerOptions) turns implicit collection keys off. A '['
    /// or '{' is then never probed, so partial data reads as with the JSON reader. The default keeps the keys.
    /// </summary>
    public class DisallowImplicitCollectionKeysTests
    {
        private static readonly YamlReaderOptions s_off = new() { DisallowImplicitCollectionKeys = true };
        private static readonly YamlReaderState s_offState = new(s_off);

        private static List<string> Tokens(byte[] utf8, bool final, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(utf8, final, new YamlReaderState(options));
            var tokens = new List<string>();
            while (reader.Read())
            {
                tokens.Add($"{reader.TokenType}@{reader.TokenStartIndex}-{reader.BytesConsumed}");
            }
            return tokens;
        }

        private static List<string> StjTokens(byte[] utf8, bool final)
        {
            var reader = new StjReader(utf8, final, default(StjState));
            var tokens = new List<string>();
            while (reader.Read())
            {
                tokens.Add($"{reader.TokenType}@{reader.TokenStartIndex}-{reader.BytesConsumed}");
            }
            return tokens;
        }

        [Theory]
        [InlineData("[{\"a\":1},{\"a\":2}", 9)]
        [InlineData("[", 1)]
        [InlineData("{\"a\":[1,2]", 6)]
        [InlineData("[\n{\"a\":1}", 5)]
        public void PartialDataReadsAsTheJsonReaderDoes(string json, int count)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(json);
            List<string> expected = StjTokens(utf8, final: false);
            Assert.Equal(count, expected.Count);
            Assert.Equal(expected, Tokens(utf8, final: false, s_off));

            // The default waits: the root '[' or '{' may still be a key.
            Assert.True(Tokens(utf8, final: false, default).Count < count);
        }

        [Theory]
        [InlineData("[a]: v\n")]
        [InlineData("{a: b}: v\n")]
        [InlineData("[[a]: b]")]
        [InlineData("{[a]: b}")]
        [InlineData("x: 1\n[a]: 2\n")]
        public void ImplicitCollectionKeysAreErrors(string yaml)
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() => Tokens(Encoding.UTF8.GetBytes(yaml), true, s_off));
            Assert.Contains("DisallowImplicitCollectionKeys", e.Message);
            Assert.Contains(Tokens(Encoding.UTF8.GetBytes(yaml), true, default), t => t.StartsWith("PropertyName", StringComparison.Ordinal));
        }

        [Fact]
        public void PropertiesOnTheirOwnLineBeforeACollectionKey()
        {
            // Telling that a mapping starts here would need the lookahead the option turns off, so this fails on the
            // second set of properties instead (an error either way).
            Assert.ThrowsAny<YamlException>(() => Tokens("&m\n&k [a]: 1\n"u8.ToArray(), true, s_off));
        }

        [Theory]
        [InlineData("? [a, b]\n: v\n")]
        [InlineData("{? [a] : b}")]
        [InlineData("? {a: 1}\n: v\n")]
        public void ExplicitKeysStillWork(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), s_off);
            bool sawComplex = false;
            while (reader.Read())
            {
                sawComplex |= reader.KeyKind == YamlKeyKind.Complex;
            }
            Assert.True(sawComplex);
        }

        [Fact]
        public void SerializerOption()
        {
            var options = new YamlSerializerOptions { DisallowImplicitCollectionKeys = true };
            Assert.Equal(new[] { 1, 2 }, YamlSerializer.Deserialize<int[]>("[1, 2]", options));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("[a]: 1\n", options));
            Assert.False(new YamlSerializerOptions().DisallowImplicitCollectionKeys);
            Assert.True(new YamlSerializerOptions(options).DisallowImplicitCollectionKeys);
        }

        [Fact]
        public void ChunkingInBothSettings()
        {
            foreach (string yaml in new[] { "[{\"a\":1},{\"a\":[2, 3]}]", "a: [b]\n? [c]\n: d\n", "[[a]: b, {c: d}]" })
            {
                ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), s_off);
                ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), default);
            }
        }

        [Fact]
        public void TheProbeCostsNothing()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("[[a], {\"b\": [1]}]", 2000)) + "]");
            var reader = new Utf8YamlReader(utf8, s_off);
            while (reader.Read())
            {
            }
            Assert.Equal(0, reader.YamlProbeBytesExamined);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Twins of the seven imported tests tagged in M3d for partial data at a collection that may be a key: the same
        // bodies and assertions, with the option set where the reader or the serializer is created.

        [Fact]
        public static void StateRecovery()
        {
            ReadOnlySpan<byte> utf8 = "[1]"u8;
            var yaml = new Utf8YamlReader(utf8, isFinalBlock: false, state: s_offState);

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.False(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.True(yaml.Read());

            Assert.Equal(2, yaml.BytesConsumed);
            Assert.Equal(1, yaml.TokenStartIndex);
            Assert.Equal(1, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(new byte[] { (byte)'1' }));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            YamlReaderState state = yaml.CurrentState;

            yaml = new Utf8YamlReader(utf8.Slice((int)yaml.BytesConsumed), isFinalBlock: true, state);

            Assert.Equal(0, yaml.BytesConsumed);    // Not retained
            Assert.Equal(0, yaml.TokenStartIndex);  // Not retained
            Assert.Equal(1, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.False(yaml.Read());
        }

        [Fact]
        public static void TrySkipPartialAndContinueWithWhiteSpace()
        {
            string yamlString = "[ 1, 2]";
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            // "[ 1, "
            var reader = new Utf8YamlReader(utf8Data.AsSpan(0, 5), isFinalBlock: false, state: s_offState);
            reader.Read();
            Assert.Equal(1, reader.BytesConsumed);
            Assert.False(reader.TrySkip());
            Assert.Equal(1, reader.BytesConsumed);

            // " 1, 2]"
            int previousConsumed = (int)reader.BytesConsumed;
            reader = new Utf8YamlReader(utf8Data.AsSpan(previousConsumed), isFinalBlock: true, reader.CurrentState);
            Assert.True(reader.TrySkip());
            Assert.Equal(utf8Data.Length - previousConsumed, reader.BytesConsumed);
        }

        [Fact]
        public static void SkipInvalid()
        {
            string yamlString = "[[[]], {\"a\":1, \"b\": 2}, 3, {\"a\":{}, {\"c\":[]} }, [{\"a\":1, \"b\": 2}, null]";
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, s_offState);
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);
                try
                {
                    Assert.True(yaml.IsFinalBlock);
                    yaml.Skip();
                    Assert.Fail("Expected YamlException was not thrown for invalid JSON payload when skipping.");
                }
                catch (YamlException) { }
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, s_offState);
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);
                try
                {
                    Assert.True(yaml.IsFinalBlock);
                    yaml.TrySkip();
                    Assert.Fail("Expected YamlException was not thrown for invalid JSON payload when skipping.");
                }
                catch (YamlException) { }
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: false, s_offState);
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);
                try
                {
                    Assert.False(yaml.IsFinalBlock);
                    yaml.TrySkip();
                    Assert.Fail("Expected YamlException was not thrown for invalid JSON payload when skipping.");
                }
                catch (YamlException) { }
            }
        }

        [Theory]
        [InlineData("[]", 1, 2)]
        [InlineData("[1, 2, 3, 4, 5]", 1, 15)]
        [InlineData("{\"foo\":1}", 2, 8)]
        [InlineData("{\"foo\":[1, 2, 3]}", 2, 16)]
        public static void BasicSkipTest(string yamlString, int readCount, int expectedConsumed)
        {
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yamlString), isFinalBlock: true, state: s_offState);
                for (int i = 0; i < readCount; i++)
                {
                    reader.Read();
                }

                reader.Skip();
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yamlString), isFinalBlock: true, state: s_offState);
                for (int i = 0; i < readCount; i++)
                {
                    reader.Read();
                }

                Assert.True(reader.TrySkip());
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yamlString), isFinalBlock: false, state: s_offState);
                for (int i = 0; i < readCount; i++)
                {
                    reader.Read();
                }

                Assert.True(reader.TrySkip());
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }

            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yamlString), s_off);
                for (int i = 0; i < readCount; i++)
                {
                    reader.Read();
                }

                reader.Skip();
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yamlString), s_off);
                for (int i = 0; i < readCount; i++)
                {
                    reader.Read();
                }

                Assert.True(reader.TrySkip());
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }
        }

        [Theory]
        [InlineData(YamlTokenType.EndObject)]
        [InlineData(YamlTokenType.EndArray)]
        public static void ParseReaderAtInvalidStart(YamlTokenType tokenType)
        {
            string yamlString;

            switch (tokenType)
            {
                case YamlTokenType.EndObject:
                    yamlString = "[ { } ]";
                    break;
                case YamlTokenType.EndArray:
                    yamlString = "[ [ ] ]";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tokenType));
            }

            byte[] yamlUtf8 = Encoding.UTF8.GetBytes(yamlString);
            YamlReaderState state = s_offState;

            Utf8YamlReader reader = new Utf8YamlReader(
                yamlUtf8,
                isFinalBlock: false,
                state);

            Assert.True(reader.Read(), "Move to first [");
            Assert.True(reader.Read(), "Move to internal start");
            Assert.True(reader.Read(), "Move to internal end");

            Exception? ex;

            long initialPosition = reader.BytesConsumed;

            try
            {
                using (YamlDocument.ParseValue(ref reader))
                {
                }

                ex = null;
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.NotNull(ex);
            Assert.IsAssignableFrom<YamlException>(ex);

            Assert.Equal(initialPosition, reader.BytesConsumed);

            Assert.False(YamlDocument.TryParseValue(ref reader, out YamlDocument? doc));
            Assert.Null(doc);
            Assert.Equal(initialPosition, reader.BytesConsumed);
        }

        public class SimpleTypeWithArray
        {
            public int[]? MyInt32Array { get; set; }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void NotEnoughData(bool isFinalBlock)
        {
            {
                byte[] utf8 = "\"start of string"u8.ToArray();

                var reader = new Utf8YamlReader(utf8, isFinalBlock, state: s_offState);
                Assert.Equal(0, reader.BytesConsumed);
                Assert.Equal(YamlTokenType.None, reader.TokenType);

                try
                {
                    YamlSerializer.Deserialize<SimpleTypeWithArray>(ref reader);
                    Assert.Fail("Expected ReadValue to throw YamlException for not enough data.");
                }
                catch (YamlException) { }

                Assert.Equal(0, reader.BytesConsumed);
                Assert.Equal(YamlTokenType.None, reader.TokenType);
            }

            {
                byte[] utf8 = "{"u8.ToArray();

                var reader = new Utf8YamlReader(utf8, isFinalBlock, state: s_offState);
                reader.Read();

                Assert.Equal(1, reader.BytesConsumed);
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);

                try
                {
                    YamlSerializer.Deserialize<SimpleTypeWithArray>(ref reader);
                    Assert.Fail("Expected ReadValue to throw YamlException for not enough data.");
                }
                catch (YamlException) { }

                Assert.Equal(1, reader.BytesConsumed);
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeserializeAsyncEnumerable_ShouldStreamPartialData(bool typeInfoOverload)
        {
            string yaml = YamlSerializer.Serialize(Enumerable.Range(0, 100));

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = 1,
                DisallowImplicitCollectionKeys = true,
            };
            options.MakeReadOnly(populateMissingResolver: true);

            IAsyncEnumerable<int?> asyncEnumerable = typeInfoOverload
                ? YamlSerializer.DeserializeAsyncEnumerable(stream, (System.Text.Yaml.Serialization.Metadata.YamlTypeInfo<int?>)options.GetTypeInfo(typeof(int?)))
                : YamlSerializer.DeserializeAsyncEnumerable<int?>(stream, options);
            await using IAsyncEnumerator<int?> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();

            for (int i = 0; i < 20; i++)
            {
                bool success = await asyncEnumerator.MoveNextAsync();
                Assert.True(success, "AsyncEnumerator.MoveNextAsync() should return true.");
                Assert.True(stream.Position < stream.Capacity / 2, "should have consumed less than half of the stream contents.");
            }
        }
    }
}
