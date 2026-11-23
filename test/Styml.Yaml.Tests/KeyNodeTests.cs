// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Key nodes beyond a single-line scalar (M3d): explicit keys of any
    /// shape and collections used as keys (D10: one PropertyName with KeyKind.Complex and the raw key node as
    /// ValueSpan). Until M5 every consumer that needs a Complex key's text fails instead of binding it.
    /// </summary>
    public class KeyNodeTests
    {
        private static readonly YamlReaderOptions s_options = new() { AllowMultipleValues = true };

        private static List<string> Tokens(string yaml, YamlReaderOptions options = default)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tokens = new List<string>();
            while (reader.Read())
            {
                tokens.Add(Describe(ref reader));
            }
            return tokens;
        }

        private static string Describe(ref Utf8YamlReader reader)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex:
                    var events = new List<string>();
                    Utf8YamlReader keyReader = reader.CreateKeyReader();
                    while (keyReader.Read())
                    {
                        YamlEventTrace.AddEvent(ref keyReader, events);
                    }
                    return $"Key {{{Encoding.UTF8.GetString(reader.ValueSpan.ToArray())}}} {string.Join(" ", events)}";
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias:
                    return "Key *" + Encoding.UTF8.GetString(reader.ValueSpan.ToArray());
                case YamlTokenType.PropertyName:
                    return "Key " + reader.GetString();
                case YamlTokenType.String or YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False:
                    return reader.GetString()!;
                default:
                    return reader.TokenType.ToString();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scalar key nodes spanning lines (KeyKind.Scalar).

        [Theory]
        [InlineData("? first\n  second\n: v\n", "first second", YamlScalarStyle.Plain)]
        [InlineData("? first\n\n  second\n: v\n", "first\nsecond", YamlScalarStyle.Plain)]
        [InlineData("? \"quoted\n  key\"\n: v\n", "quoted key", YamlScalarStyle.DoubleQuoted)]
        [InlineData("? 'single\n  quoted'\n: v\n", "single quoted", YamlScalarStyle.SingleQuoted)]
        [InlineData("? |\n  literal\n  text\n: v\n", "literal\ntext\n", YamlScalarStyle.Literal)]
        [InlineData("? >-\n  folded\n  text\n: v\n", "folded text", YamlScalarStyle.Folded)]
        [InlineData("? |-\n  last\n", "last", YamlScalarStyle.Literal)]
        [InlineData("a: 1\n? long\n  key # comment\n: v\n", "long key", YamlScalarStyle.Plain)]
        public void ScalarKeysSpanningLines(string yaml, string key, YamlScalarStyle style)
        {
            ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), s_options);
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.PropertyName && reader.GetString() == key)
                {
                    Assert.Equal(YamlKeyKind.Scalar, reader.KeyKind);
                    Assert.Equal(style, reader.ScalarStyle);
                    Assert.True(reader.ValueTextEquals(key));
                    Assert.True(reader.Read());
                    return;
                }
            }
            Assert.Fail($"No key '{key}'.");
        }

        [Fact]
        public void ScalarKeysSpanningLines_Serializer()
        {
            var d = YamlSerializer.Deserialize<Dictionary<string, string>>("? first\n  second\n: v\n? |\n  block\n: w\n");
            Assert.Equal("v", d!["first second"]);
            Assert.Equal("w", d["block\n"]);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Collection keys (KeyKind.Complex).

        public static IEnumerable<object[]> ComplexKeyDocuments() => new[]
        {
            // Explicit keys: flow and block collections, compact collections, zero-indented sequences.
            new object[] { "? [a, b]\n: v\n", new[] { "StartObject", "Key {[a, b]} +SEQ [] =VAL :a =VAL :b -SEQ", "v", "EndObject" } },
            new object[] { "? {a: 1}\n: v\n", new[] { "StartObject", "Key {{a: 1}} +MAP {} =VAL :a =VAL :1 -MAP", "v", "EndObject" } },
            new object[] { "? - a\n  - b\n: v\n", new[] { "StartObject", "Key {- a\n  - b} +SEQ =VAL :a =VAL :b -SEQ", "v", "EndObject" } },
            new object[] { "?\n- a\n- b\n: v\n", new[] { "StartObject", "Key {- a\n- b} +SEQ =VAL :a =VAL :b -SEQ", "v", "EndObject" } },
            new object[] { "? a: b\n: v\n", new[] { "StartObject", "Key {a: b} +MAP =VAL :a =VAL :b -MAP", "v", "EndObject" } },
            new object[] { "? : x\n", new[] { "StartObject", "Key {: x} +MAP =VAL : =VAL :x -MAP", "Null", "EndObject" } },
            new object[] { "? &k [a] # c\n: v\n", new[] { "StartObject", "Key {&k [a]} +SEQ [] &k =VAL :a -SEQ", "v", "EndObject" } },
            new object[] { "? [a,\n   b]\n: v\n", new[] { "StartObject", "Key {[a,\n   b]} +SEQ [] =VAL :a =VAL :b -SEQ", "v", "EndObject" } },
            // Implicit keys in block context: the root, a later key, a nested mapping, a block sequence entry.
            new object[] { "[a, b]: v\n", new[] { "StartObject", "Key {[a, b]} +SEQ [] =VAL :a =VAL :b -SEQ", "v", "EndObject" } },
            new object[] { "{a: b}: v\n", new[] { "StartObject", "Key {{a: b}} +MAP {} =VAL :a =VAL :b -MAP", "v", "EndObject" } },
            new object[] { "x: 1\n[a]: 2\n", new[] { "StartObject", "Key x", "1", "Key {[a]} +SEQ [] =VAL :a -SEQ", "2", "EndObject" } },
            new object[] { "x:\n  [a]: 1\n", new[] { "StartObject", "Key x", "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "1", "EndObject", "EndObject" } },
            new object[] { "- [a]: 1\n  b: 2\n", new[] { "StartArray", "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "1", "Key b", "2", "EndObject", "EndArray" } },
            new object[] { "&m\n&k [a]: 1\n", new[] { "StartObject", "Key {&k [a]} +SEQ [] &k =VAL :a -SEQ", "1", "EndObject" } },
            new object[] { "[\"a]\", 'b'']']: v\n", new[] { "StartObject", "Key {[\"a]\", 'b'']']} +SEQ [] =VAL \"a] =VAL 'b'] -SEQ", "v", "EndObject" } },
            // Flow context: pairs in a sequence, flow mapping keys, explicit flow keys, keys spanning lines.
            new object[] { "[[a]: b]", new[] { "StartArray", "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "b", "EndObject", "EndArray" } },
            new object[] { "[{a: 1}: b, c]", new[] { "StartArray", "StartObject", "Key {{a: 1}} +MAP {} =VAL :a =VAL :1 -MAP", "b", "EndObject", "c", "EndArray" } },
            new object[] { "{[a]: b}", new[] { "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "b", "EndObject" } },
            new object[] { "{[a]:b, c: d}", new[] { "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "b", "Key c", "d", "EndObject" } },
            new object[] { "{? [a] : b}", new[] { "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "b", "EndObject" } },
            new object[] { "{[a,\n b]: c}", new[] { "StartObject", "Key {[a,\n b]} +SEQ [] =VAL :a =VAL :b -SEQ", "c", "EndObject" } },
            new object[] { "{[a]}", new[] { "StartObject", "Key {[a]} +SEQ [] =VAL :a -SEQ", "Null", "EndObject" } },
            // Nested: a collection key holding a collection key.
            new object[] { "? {[a]: b}\n: c\n", new[] { "StartObject", "Key {{[a]: b}} +MAP {} +SEQ [] =VAL :a -SEQ =VAL :b -MAP", "c", "EndObject" } },
            // A block scalar inside the key keeps its trailing line breaks (keep chomping) when read through the key reader.
            new object[] { "? - |+\n    a\n\n: v\n", new[] { "StartObject", "Key {- |+\n    a\n\n} +SEQ =VAL |a\\n\\n -SEQ", "v", "EndObject" } },
        };

        [Theory]
        [MemberData(nameof(ComplexKeyDocuments))]
        public void ReaderAcceptsCollectionKeys(string yaml, string[] expected)
        {
            Assert.Equal(expected, Tokens(yaml));
        }

        [Theory]
        [MemberData(nameof(ComplexKeyDocuments))]
        public void ReaderAcceptsCollectionKeys_EverySplit(string yaml, string[] expected)
        {
            _ = expected;
            ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), s_options);
        }

        public static IEnumerable<object[]> NotKeys() => new[]
        {
            // A collection that is not followed by ':' on its line is an ordinary value.
            new object[] { "[a, b]\n", new[] { "StartArray", "a", "b", "EndArray" } },
            new object[] { "[a]  # c\n", new[] { "StartArray", "a", "EndArray" } },
            new object[] { "[[a], {b: c}]", new[] { "StartArray", "StartArray", "a", "EndArray", "StartObject", "Key b", "c", "EndObject", "EndArray" } },
            new object[] { "[\"]: x\"]", new[] { "StartArray", "]: x", "EndArray" } },
            new object[] { "a: [b]\n", new[] { "StartObject", "Key a", "StartArray", "b", "EndArray", "EndObject" } },
            new object[] { "- [a]\n- b: [c]\n", new[] { "StartArray", "StartArray", "a", "EndArray", "StartObject", "Key b", "StartArray", "c", "EndArray", "EndObject", "EndArray" } },
            new object[] { "{\"a\": [1, {\"b\": 2}], \"c\": {}}", new[] { "StartObject", "Key a", "StartArray", "1", "StartObject", "Key b", "2", "EndObject", "EndArray", "Key c", "StartObject", "EndObject", "EndObject" } },
            // A ':' adjacent to a closing bracket in block context does not start a value.
            new object[] { "- [a]:b\n", null! },
        };

        [Theory]
        [MemberData(nameof(NotKeys))]
        public void CollectionsThatAreNotKeys(string yaml, string[]? expected)
        {
            if (expected is null)
            {
                Assert.ThrowsAny<YamlException>(() => Tokens(yaml));
                return;
            }
            Assert.Equal(expected, Tokens(yaml));
            ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), s_options);
        }

        [Theory]
        [InlineData("[a,\n b]: v\n")]           // an implicit key on two lines
        [InlineData("[[a,\n b]: c]")]           // the same, as a pair in a flow sequence
        [InlineData("x: [a]: b\n")]             // a mapping cannot start on the line of a value
        [InlineData("[a] : b: c\n")]            // two values on one line
        [InlineData("? [a] b\n: v\n")]          // content after an explicit key node
        [InlineData("? [a\n: v\n")]             // an unterminated key node
        [InlineData("?\t- a\n: v\n")]           // a tab as indentation of a compact key
        [InlineData("|\n  x\n: v\n")]           // a block scalar as an implicit key
        public void InvalidKeyNodesAreRejected(string yaml)
        {
            Assert.ThrowsAny<YamlException>(() => Tokens(yaml));

            // Also with a bare read loop, which never opens a Complex key's reader.
            Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), s_options);
                while (reader.Read())
                {
                }
            });
        }

        [Fact]
        public void ImplicitCollectionKeyLimit()
        {
            // At most 1024 characters, like libyaml's simple keys (and YamlDotNet's and SharpYaml's scanners).
            string atLimit = "[" + new string('a', 1022) + "]";
            Assert.Equal(1024, atLimit.Length);
            Assert.Equal("StartObject", Tokens(atLimit + ": v\n")[0]);
            Assert.Equal("StartObject", Tokens("[" + new string('\u00e9', 1022) + "]: v\n")[0]);
            Assert.ThrowsAny<YamlException>(() => Tokens("[" + new string('a', 1023) + "]: v\n"));

            // An explicit key has no limit.
            string longKey = "[" + new string('a', 5000) + "]";
            Assert.Equal("Key {" + longKey + "} +SEQ [] =VAL :" + new string('a', 5000) + " -SEQ", Tokens("? " + longKey + "\n: v\n")[1]);
        }

        [Fact]
        public void ComplexKeyTokenShape()
        {
            var reader = new Utf8YamlReader("x: 1\n&k [a, b]: 2\n"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            Assert.Equal("&k [a, b]", Encoding.UTF8.GetString(reader.ValueSpan.ToArray()));
            Assert.True(reader.ValueIsEscaped);
            Assert.False(reader.HasValueSequence);
            Assert.Equal(YamlScalarStyle.None, reader.ScalarStyle);
            Assert.True(reader.Anchor.IsEmpty);
            Assert.True(reader.Tag.IsEmpty);
            Assert.Equal(5, reader.TokenStartIndex);
            Assert.Equal(1, reader.CurrentDepth);
            Assert.Equal(15, reader.BytesConsumed);
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.None, reader.KeyKind);
            Assert.Equal("2", reader.GetString());
        }

        [Fact]
        public void ComplexKeyCountsTowardsMaxDepth()
        {
            Assert.ThrowsAny<YamlException>(() => Tokens("? [[[a]]]\n: v\n", new YamlReaderOptions { MaxDepth = 3 }));
            Assert.Equal(4, Tokens("? [[a]]\n: v\n", new YamlReaderOptions { MaxDepth = 3 }).Count);
        }

        [Fact]
        public void ComplexKeyErrorsHaveDocumentPositions()
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() => Tokens("a: 1\n? [b,\n   c}\n: v\n"));
            Assert.Equal(2, e.LineNumber);
        }

        [Fact]
        public void KeyReaderNeedsAComplexKey()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var reader = new Utf8YamlReader("a: 1"u8);
                reader.Read();
                reader.Read();
                reader.CreateKeyReader();
            });
        }

        [Theory]
        [InlineData("GetString")]
        [InlineData("CopyStringBytes")]
        [InlineData("CopyStringChars")]
        [InlineData("ValueTextEqualsBytes")]
        [InlineData("ValueTextEqualsChars")]
        [InlineData("ValueTextEqualsString")]
        public void ComplexKeyHasNoText(string accessor)
        {
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ReadComplexKey(accessor));
            Assert.Contains("KeyKind", e.Message);
        }

        [Theory]
        [InlineData("GetInt32")]
        [InlineData("TryGetInt64")]
        [InlineData("GetBoolean")]
        [InlineData("GetGuid")]
        [InlineData("GetBytesFromBase64")]
        [InlineData("GetComment")]
        public void ComplexKeyIsNotAValue(string accessor)
        {
            Assert.Throws<InvalidOperationException>(() => ReadComplexKey(accessor));
        }

        private static void ReadComplexKey(string accessor)
        {
            var reader = new Utf8YamlReader("[key]: 1\n"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            switch (accessor)
            {
                case "GetString": reader.GetString(); break;
                case "CopyStringBytes": reader.CopyString(new byte[16]); break;
                case "CopyStringChars": reader.CopyString(new char[16]); break;
                case "ValueTextEqualsBytes": reader.ValueTextEquals("[key]"u8); break;
                case "ValueTextEqualsChars": reader.ValueTextEquals("[key]".AsSpan()); break;
                case "ValueTextEqualsString": reader.ValueTextEquals("[key]"); break;
                case "GetInt32": reader.GetInt32(); break;
                case "TryGetInt64": reader.TryGetInt64(out _); break;
                case "GetBoolean": reader.GetBoolean(); break;
                case "GetGuid": reader.GetGuid(); break;
                case "GetBytesFromBase64": reader.GetBytesFromBase64(); break;
                case "GetComment": reader.GetComment(); break;
            }
        }

        [Fact]
        public void SkipOnComplexKeySkipsTheValue()
        {
            var reader = new Utf8YamlReader("[a]: {b: [c]}\nd: e\n"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            reader.Skip();
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);
            Assert.True(reader.Read());
            Assert.Equal("d", reader.GetString());
        }

        // ---------------------------------------------------------------------------------------------------------
        // Partial data: a '[' or '{' where a key may start needs its line, or 1024 characters.

        [Theory]
        [InlineData("[")]
        [InlineData("[1, 2")]
        [InlineData("[1, 2]")]
        [InlineData("{\"a\": [")]
        [InlineData("- {a: b}")]
        [InlineData("[[1], [2")]
        public void CollectionInKeyPositionWaitsForItsLine(string partial)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(partial), isFinalBlock: false, state: default);
            bool read = reader.Read();
            if (partial.StartsWith("-", StringComparison.Ordinal))
            {
                Assert.True(read);
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                read = reader.Read();
            }
            Assert.False(read);

            // With the rest of the line the decision is made.
            var complete = new Utf8YamlReader(Encoding.UTF8.GetBytes(partial + "\n"), isFinalBlock: false, state: default);
            Assert.True(complete.Read());
        }

        [Theory]
        [InlineData("a: [", YamlTokenType.StartArray)]
        [InlineData("a: {", YamlTokenType.StartObject)]
        public void CollectionInValuePositionIsReadWithoutTheNextByte(string partial, YamlTokenType expected)
        {
            // Only key positions need the decision; a flow collection as a block mapping value is a token at once.
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(partial), isFinalBlock: false, state: default);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(expected, reader.TokenType);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        [InlineData(0)]
        public async Task KeysLargerThanTheStreamBuffer(int bufferSize)
        {
            // The key node must be in the buffer before the PropertyName is emitted: the stream grows its buffer while
            // the reader makes no progress, and rolls back to the key's start each time.
            string key = "? " + new string('k', 3000) + "\n  " + new string('l', 3000) + "\n";
            string yaml = "a: 1\n" + key + ": v\nb: 2\n";
            var options = bufferSize == 0 ? new YamlSerializerOptions() : new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            Dictionary<string, string>? d = await YamlSerializer.DeserializeAsync<Dictionary<string, string>>(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), options);
            Assert.Equal("v", d![new string('k', 3000) + " " + new string('l', 3000)]);
            Assert.Equal("2", d["b"]);

            // A Complex key larger than the buffer is read, and then refused by a string key (M5e: a string key refuses a
            // collection key; KeyNodeTests' M5e rows read it into collection keys).
            string complex = "? [" + string.Join(", ", Enumerable.Repeat("x", 2000)) + "]\n: v\n";
            await Assert.ThrowsAnyAsync<YamlException>(() => YamlSerializer.DeserializeAsync<Dictionary<string, string>>(new MemoryStream(Encoding.UTF8.GetBytes(complex)), options).AsTask());
        }

        // ---------------------------------------------------------------------------------------------------------
        // Never binds silently (the same matrix as alias keys, AliasKeyTests).

        public class Target
        {
            public int Key { get; set; }
            public int Other { get; set; }
        }

        public record TargetRecord(int Key, int Other);

        public class WithExtensionData
        {
            public int Other { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object>? Extra { get; set; }
        }

        public class WithIntKeys
        {
            public Dictionary<int, int>? Map { get; set; }
        }

        [Theory]
        [InlineData("Other: 1\n[Key]: 2\n")]
        [InlineData("{\"Other\": 1, [\"Key\"]: 2}")]
        [InlineData("? [Key]\n: 2\n")]
        [InlineData("? Key\n: 2\n? {a: b}\n: 3\n")]
        public void SerializerNeverTakesAComplexKeyForAMember(string yaml)
        {
            // Never treat the key's raw YAML as its text: 'Key' must not be set from '[Key]'. Since M5e a
            // collection key at a member site names no member: skipped by default, an error under Disallow; a string key
            // and string-keyed extension data refuse it.
            int key = yaml.StartsWith("? Key", StringComparison.Ordinal) ? 2 : 0; // '? Key' is a scalar key
            Assert.Equal(key, YamlSerializer.Deserialize<Target>(yaml)!.Key);
            Assert.Equal(key, YamlSerializer.Deserialize<TargetRecord>(yaml)!.Key);
            Assert.Equal(key, YamlSerializer.Deserialize<Target>(yaml, new YamlSerializerOptions { PropertyNameCaseInsensitive = true })!.Key);
            Assert.Equal(key, YamlSerializer.Deserialize<Target>(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), new YamlSerializerOptions { DefaultBufferSize = 1 })!.Key);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Target>(yaml, new YamlSerializerOptions { UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow }));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<WithExtensionData>(yaml));
        }

        [Fact]
        public void ComplexKeys_ScalarKeyConvertersRefuse_ObjectMembersRead()
        {
            // A scalar key type refuses a collection key (M5e); an object site reads it into a YamlElement.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<WithIntKeys>("Map:\n  [1]: 2\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, int>>("[1]: 2\n"));
            YamlElement element = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("{[k]: 1}"));
            Assert.Equal(YamlKeyKind.Complex, element.EnumerateObject().Single().KeyKind);
            YamlElement nested = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Dictionary<string, object>>("{\"a\": {[k]: 1}}")!["a"]);
            Assert.Equal("k", nested.EnumerateObject().Single().GetKeyElement()[0].GetString());
        }

        [Fact]
        public void ComplexKeys_AtAMemberSite_Async()
        {
            Assert.Equal(1, YamlSerializer.DeserializeAsync<Target>(new MemoryStream("Other: 1\n[Key]: 2\n"u8.ToArray()), new YamlSerializerOptions { DefaultBufferSize = 1 }).AsTask().GetAwaiter().GetResult()!.Other);
        }

        [Fact]
        public void ComplexKeyInsideASkippedValueIsQuiet()
        {
            // The one quiet case: the serializer skips the value of a member the type does not have, and a skipped
            // subtree's keys are never read as text. With UnmappedMemberHandling.Disallow it is an error, as for any
            // unknown member.
            const string Yaml = "Other: 1\nUnknown: {[a]: 1}\n";
            Assert.Equal(1, YamlSerializer.Deserialize<Target>(Yaml)!.Other);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Target>(Yaml, new YamlSerializerOptions { UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow }));
        }

        [Theory]
        [InlineData("{[k]: 1}")]
        [InlineData("[{[k]: 1}]")]
        public void DocumentAndNodeReadComplexKeys(string yaml)
        {
            // M5e: the DOM and the nodes read collection keys, and write them back.
            var block = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
            using YamlDocument document = YamlDocument.Parse(yaml);
            YamlNode node = YamlNode.Parse(yaml)!;
            YamlNode serializedNode = YamlSerializer.Deserialize<YamlNode>(yaml)!;
            YamlElement element = YamlSerializer.Deserialize<YamlElement>(yaml);
            using YamlDocument serializedDocument = YamlSerializer.Deserialize<YamlDocument>(yaml)!;
            string expected = YamlSerializer.Serialize(document.RootElement, block);
            Assert.Contains("? ", expected);
            Assert.Equal(expected, node.ToYamlString(block));
            Assert.Equal(expected, serializedNode.ToYamlString(block));
            Assert.Equal(expected, YamlSerializer.Serialize(element, block));
            Assert.Equal(expected, YamlSerializer.Serialize(serializedDocument.RootElement, block));
        }
    }
}
