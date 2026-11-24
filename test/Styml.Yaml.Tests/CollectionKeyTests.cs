// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5e: collection keys in the reader's public API, the DOM,
    /// the nodes and the serializer, and the schema's number forms in typed keys. Rows are named after M5e's parts (B4 to
    /// E) and its review items (R1 to R7).
    /// </summary>
    public class CollectionKeyTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_json = new() { WriteStyle = YamlWriterStyle.JsonCompatible };

        private static YamlSerializerOptions With(YamlDuplicateKeyHandling handling) => new(s_block) { DuplicateKeyHandling = handling };

        private static string Write(YamlElement element)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                element.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        public class Point
        {
            public int X { get; set; }

            public int Y { get; set; }

            public override bool Equals(object? obj) => obj is Point p && p.X == X && p.Y == Y;

            public override int GetHashCode() => (X * 397) ^ Y;
        }

        public record Pair(string A, int B);

        public struct Cell
        {
            public int Row { get; set; }

            public int Column { get; set; }
        }

        public class Holder
        {
            public List<string>? V { get; set; }

            public Dictionary<List<string>, int>? M { get; set; }
        }

        public class Target
        {
            public int Key { get; set; }

            public int Other { get; set; }
        }

        public class Odd
        {
            [YamlPropertyName("[a, b]")]
            public int V { get; set; }
        }

        public class WithStringExtension
        {
            public int X { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object>? E { get; set; }
        }

        public class WithElementExtension
        {
            public int X { get; set; }

            [YamlExtensionData]
            public Dictionary<string, YamlElement>? E { get; set; }
        }

        public class WithNodeExtension
        {
            public int X { get; set; }

            [YamlExtensionData]
            public YamlObject? E { get; set; }
        }

        public enum Color
        {
            Red,
            Green,
            Blue,
        }

        // ------------------------------------------------------------------------------------------------------------
        // B4: Utf8YamlReader.CreateKeyReader, public

        [Fact]
        public void B4_KeyReader_ReadsTheKeyNodeOnly()
        {
            var reader = new Utf8YamlReader("? [a, {b: c}]\n: v\n"u8.ToArray());
            reader.Read();
            reader.Read();
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            Utf8YamlReader key = reader.CreateKeyReader();
            var tokens = new List<YamlTokenType>();
            while (key.Read())
            {
                tokens.Add(key.TokenType);
            }

            Assert.Equal(new[] { YamlTokenType.StartArray, YamlTokenType.String, YamlTokenType.StartObject, YamlTokenType.PropertyName, YamlTokenType.String, YamlTokenType.EndObject, YamlTokenType.EndArray }, tokens);
            reader.Read();
            Assert.Equal("v", reader.GetString());
        }

        [Theory]
        [InlineData("a: v\n")]
        [InlineData("x: &k a\n*k : v\n")]
        public void B4_KeyReader_NeedsACollectionKey(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            while (reader.Read() && !(reader.TokenType == YamlTokenType.PropertyName && (yaml.StartsWith("a", StringComparison.Ordinal) || reader.KeyKind == YamlKeyKind.Alias)))
            {
            }

            Assert.NotEqual(YamlKeyKind.Complex, reader.KeyKind);
            InvalidOperationException? e = null;
            try
            {
                reader.CreateKeyReader();
            }
            catch (InvalidOperationException caught)
            {
                e = caught;
            }

            Assert.Contains("Complex", e!.Message);
        }

        [Fact]
        public void B4_KeyReader_DepthAndPositions()
        {
            // The key reader's bound is the outer reader's less the current depth.
            var options = new YamlReaderOptions { MaxDepth = 4 };
            var reader = new Utf8YamlReader("a:\n  ? [[x]]\n  : v\n"u8.ToArray(), options);
            while (reader.Read() && reader.KeyKind != YamlKeyKind.Complex)
            {
            }

            Utf8YamlReader key = reader.CreateKeyReader();
            Assert.Equal(2, key.CurrentState.Options.MaxDepth);
            key.Read();
            Assert.Equal(0, key.TokenStartIndex);
        }

        [Fact]
        public void B4_KeyReader_MultiSegmentInput()
        {
            byte[] utf8 = "k:\n  [aaaa, bbbb]: x\n"u8.ToArray();
            var reader = new Utf8YamlReader(SequenceFactory.Uniform(utf8, 1).Value);
            while (reader.Read() && reader.KeyKind != YamlKeyKind.Complex)
            {
            }

            Utf8YamlReader key = reader.CreateKeyReader();
            var strings = new List<string>();
            while (key.Read())
            {
                if (key.TokenType == YamlTokenType.String)
                {
                    strings.Add(key.GetString()!);
                }
            }

            Assert.Equal(new[] { "aaaa", "bbbb" }, strings);
        }

        // ------------------------------------------------------------------------------------------------------------
        // B5: YamlProperty and the DOM

        [Fact]
        public void B5_PropertyApi()
        {
            using YamlDocument document = YamlDocument.Parse("[a, b]: x\ns: &v 1\n*v : y\n? {n: m}\n: z\n");
            YamlProperty[] properties = document.RootElement.EnumerateObject().ToArray();
            Assert.Equal(4, document.RootElement.GetPropertyCount());
            Assert.Equal(new[] { YamlKeyKind.Complex, YamlKeyKind.Scalar, YamlKeyKind.Alias, YamlKeyKind.Complex }, properties.Select(p => p.KeyKind));
            Assert.Equal(new[] { "[a, b]", "s", "1", "{n: m}" }, properties.Select(p => p.Name));
            Assert.True(properties[0].NameEquals("[a, b]"));
            Assert.False(properties[0].NameEquals("[a,b]"));

            Assert.Equal(YamlValueKind.Array, properties[0].GetKeyElement().ValueKind);
            Assert.Equal("b", properties[0].GetKeyElement()[1].GetString());
            Assert.Equal(YamlValueKind.String, properties[1].GetKeyElement().ValueKind);
            Assert.Equal("s", properties[1].GetKeyElement().GetString());
            Assert.Equal(YamlValueKind.Number, properties[2].GetKeyElement().ValueKind);
            Assert.Equal("m", properties[3].GetKeyElement().GetProperty("n").GetString());
            Assert.Equal(new[] { "x", "1", "y", "z" }, properties.Select(p => p.Value.ToString()));
        }

        [Theory]
        [InlineData("[a, b]: x\nc: 2\n", "? [a, b]\n: x\nc: 2\n")]
        [InlineData("? - a\n  - b\n: x\n", "? - a\n  - b\n: x\n")]
        [InlineData("k:\n  [1, 2]: v\n  z: 3\n", "k:\n  ? [1, 2]\n  : v\n  z: 3\n")]
        [InlineData("x: [ {[a]: 1} ]\n", "x: [{? [a] : 1}]\n")]
        [InlineData("[[a], {b: c}]: deep\n", "? [[a], {b: c}]\n: deep\n")]
        [InlineData("&k [a, b]: 1\nv: *k\n", "? &k [a, b]\n: 1\nv: *k\n")]
        [InlineData("v: &v [a, b]\n*v : 2\n", "v: &v [a, b]\n? *v\n: 2\n")]
        // M7b (item 1): a flow collection is written back in flow style, a key's included; a block key stays block.
        [InlineData("? - a\n  - b\n: {c: d}\n", "? - a\n  - b\n: {c: d}\n")]
        public void B5_WriteToCloneAndDeepEquals(string yaml, string written)
        {
            using YamlDocument document = YamlDocument.Parse(yaml);
            Assert.Equal(written, Write(document.RootElement));
            Assert.Equal(written, Write(document.RootElement.Clone()));
            using YamlDocument again = YamlDocument.Parse(written);
            Assert.True(YamlElement.DeepEquals(document.RootElement, again.RootElement));
            Assert.Equal(yaml.TrimEnd('\n'), document.RootElement.GetRawText());
        }

        [Fact]
        public void B5_JsonCompatibleWriter_Refuses()
        {
            using YamlDocument document = YamlDocument.Parse("[a]: 1\n");
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible });
            Assert.Throws<NotSupportedException>(() => document.RootElement.WriteTo(writer));
        }

        [Fact]
        public void B5_DeepEquals_IsStructuralForKeys()
        {
            Assert.True(YamlElement.DeepEquals(YamlDocument.Parse("[a, b]: 1").RootElement, YamlDocument.Parse("[a,b]: 1").RootElement));
            Assert.True(YamlElement.DeepEquals(YamlDocument.Parse("{x: 1, y: 2}: v").RootElement, YamlDocument.Parse("{y: 2, x: 1}: v").RootElement));
            Assert.False(YamlElement.DeepEquals(YamlDocument.Parse("[a, b]: 1").RootElement, YamlDocument.Parse("[a, c]: 1").RootElement));
            Assert.False(YamlElement.DeepEquals(YamlDocument.Parse("[a]: 1").RootElement, YamlDocument.Parse("\"[a]\": 1").RootElement));
            Assert.False(YamlElement.DeepEquals(YamlDocument.Parse("[1]: 1").RootElement, YamlDocument.Parse("[\"1\"]: 1").RootElement));
        }

        // R4: a scalar key whose text is a collection key's raw text.
        [Fact]
        public void R4_ScalarKeyWithTheSameText()
        {
            const string Yaml = "\"[a, b]\": 1\n[a, b]: 2\n";
            using YamlDocument document = YamlDocument.Parse(Yaml);
            Assert.Equal(2, document.RootElement.GetPropertyCount());
            Assert.Equal(1, document.RootElement.GetProperty("[a, b]").GetInt32());
            using YamlDocument firstWins = YamlDocument.Parse(Yaml, new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins });
            Assert.Equal(1, firstWins.RootElement.GetProperty("[a, b]").GetInt32());

            // A collection key matches a text lookup when no scalar key does.
            using YamlDocument only = YamlDocument.Parse("z: 0\n[a, b]: 2\nq: 3\n");
            Assert.Equal(2, only.RootElement.GetProperty("[a, b]").GetInt32());
            Assert.Equal(3, only.RootElement.GetProperty("q").GetInt32());
            Assert.Equal(0, only.RootElement.GetProperty("z").GetInt32());
            using YamlDocument onlyFirst = YamlDocument.Parse("[a, b]: 2\nq: 3\n", new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins });
            Assert.Equal(3, onlyFirst.RootElement.GetProperty("q").GetInt32());

            // YamlObject keeps them apart, and round-trips them.
            YamlObject node = YamlNode.Parse(Yaml)!.AsObject();
            Assert.Equal(1, (int)node["[a, b]"]!);
            Assert.Equal("\"[a, b]\": 1\n? [a, b]\n: 2\n", node.ToYamlString(s_block));
            Assert.Equal(1, node.Count);
            Assert.True(YamlNode.DeepEquals(node, node.DeepClone()));
            Assert.Equal("\"[a, b]\": 1\n? [a, b]\n: 2\n", node.DeepClone().ToYamlString(s_block));
            Assert.False(YamlNode.DeepEquals(node, YamlNode.Parse("\"[a, b]\": 1\n[a, c]: 2\n")));
        }

        // ------------------------------------------------------------------------------------------------------------
        // C: duplicates (R2), the bomb (R1), merges, alias keys in the DOM

        [Theory]
        [InlineData("[a, b]: 1\n[a,b]: 2\n")]
        [InlineData("[a, b]: 1\n[\"a\", 'b']: 2\n")]
        [InlineData("{x: 1, y: 2}: 1\n{y: 2, x: 1}: 2\n")]
        [InlineData("&k [a, b]: 1\n*k : 2\n")]
        public void C_Duplicates_InTheDom(string yaml)
        {
            Assert.Contains("Duplicate", Assert.Throws<YamlException>(() => YamlDocument.Parse(yaml)).Message);

            // LastWins and FirstWins keep every entry, as for scalar keys (M5c's DOM rule).
            using YamlDocument last = YamlDocument.Parse(yaml, new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins });
            Assert.Equal(2, last.RootElement.GetPropertyCount());
            using YamlDocument first = YamlDocument.Parse(yaml, new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins });
            Assert.Equal(2, first.RootElement.GetPropertyCount());
        }

        [Theory]
        [InlineData("[a]: 1\n\"[a]\": 2\n")]
        [InlineData("[1]: 1\n[\"1\"]: 2\n")]
        [InlineData("[a, b]: 1\n[a, c]: 2\n")]
        [InlineData("1: a\n0x1: b\n")]
        public void C_NotDuplicates_InTheDom(string yaml)
        {
            using YamlDocument document = YamlDocument.Parse(yaml);
            Assert.Equal(2, document.RootElement.GetPropertyCount());
        }

        // A number in a collection key by its value, as YamlElement.DeepEquals compares numbers and a YamlNode compares
        // keys: the DOM's duplicate check, deep equality and the serializer's object keys agree. A scalar key stays a
        // name (C_NotDuplicates_InTheDom: 1 and 0x1 are two keys).
        [Theory]
        [InlineData("[012]", "[10]", "[12]", YamlSchemaKind.Yaml11)]
        [InlineData("[-012]", "[-10]", "[-12]", YamlSchemaKind.Yaml11)]
        [InlineData("[0b101, 1_000]", "[5, 1000]", "[5, 1_001]", YamlSchemaKind.Yaml11)]
        [InlineData("[012]", "[12]", "[10]", YamlSchemaKind.Core)]
        [InlineData("[0x1F]", "[31]", "[30]", YamlSchemaKind.Core)]
        [InlineData("{n: 0o14}", "{n: 12}", "{n: 14}", YamlSchemaKind.Core)]
        [InlineData("[1.0]", "[1]", "[1.5]", YamlSchemaKind.Core)]
        [InlineData("[1e3]", "[1000]", "[1e4]", YamlSchemaKind.Core)]
        [InlineData("[.inf]", "[.Inf]", "[-.inf]", YamlSchemaKind.Core)]
        public void C_NumbersInKeys_CompareByValue(string key, string same, string other, YamlSchemaKind schema)
        {
            var documentOptions = new YamlDocumentOptions { Schema = schema };
            string Map(string k) => k + ": v\n";
            using YamlDocument a = YamlDocument.Parse(Map(key), documentOptions);
            using YamlDocument b = YamlDocument.Parse(Map(same), documentOptions);
            using YamlDocument c = YamlDocument.Parse(Map(other), documentOptions);
            Assert.True(YamlElement.DeepEquals(a.RootElement, b.RootElement));
            Assert.False(YamlElement.DeepEquals(a.RootElement, c.RootElement));
            Assert.True(YamlNode.DeepEquals(YamlNode.Parse(Map(key), documentOptions: documentOptions), YamlNode.Parse(Map(same), documentOptions: documentOptions)));
            Assert.False(YamlNode.DeepEquals(YamlNode.Parse(Map(key), documentOptions: documentOptions), YamlNode.Parse(Map(other), documentOptions: documentOptions)));

            string twice = key + ": 1\n" + same + ": 2\n";
            Assert.Contains("Duplicate", Assert.Throws<YamlException>(() => YamlDocument.Parse(twice, documentOptions)).Message);
            Assert.Equal(2, YamlDocument.Parse(key + ": 1\n" + other + ": 2\n", documentOptions).RootElement.GetPropertyCount());
            var options = new YamlSerializerOptions { Schema = schema };
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<object, object>>(twice, options));
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<object, object>>(key + ": 1\n" + other + ": 2\n", options)!.Count);
        }

        [Fact]
        public void C_NumbersInKeys_AnExponentPastIntsRange()
        {
            // The duplicate check runs at parse: such a number stands for itself by its text.
            Assert.Contains("Duplicate", Assert.Throws<YamlException>(() => YamlDocument.Parse("[1e99999999999]: 1\n[1e99999999999]: 2\n")).Message);
            using YamlDocument document = YamlDocument.Parse("[1e99999999999]: 1\n[1e99999999998]: 2\n");
            Assert.Equal(2, document.RootElement.GetPropertyCount());
        }

        private static string Bomb(string keys)
        {
            var builder = new StringBuilder("a0: &a0 [x, x]\n");
            for (int i = 1; i < 12; i++)
            {
                builder.Append($"a{i}: &a{i} [*a{i - 1}, *a{i - 1}, *a{i - 1}, *a{i - 1}]\n");
            }

            return builder.Append(keys).ToString();
        }

        // R1: aliases in a key count against the bound when the canonical form is computed.
        [Fact]
        public void R1_AliasBombAsAKey()
        {
            string yaml = Bomb("[*a11, 1]: v\n[*a11, 2]: w\n");
            Assert.Contains("MaxAliasExpansion", Assert.Throws<YamlException>(() => YamlDocument.Parse(yaml)).Message);

            // Not computed where nothing needs it.
            using (YamlDocument unchecked_ = YamlDocument.Parse(yaml, new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }))
            {
                Assert.Equal(14, unchecked_.RootElement.GetPropertyCount());
            }

            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<object, object>>(Bomb("? [*a11, 1]\n: v\n? [*a11, 2]\n: w\n"), s_block));
        }

        [Fact]
        public void R1_RecursiveAliasInAKey_IsRefused()
        {
            Assert.Throws<YamlException>(() => YamlDocument.Parse("&m {k: [*m]}: v\n[x]: w\n"));
        }

        [Fact]
        public void C_MergeRewrite_WithCollectionKeys()
        {
            const string Yaml = "base: &b {[a, b]: 1, c: 2}\nm:\n  <<: *b\n  c: 3\n";
            using YamlDocument document = YamlDocument.Parse(Yaml, new YamlDocumentOptions { ResolveMergeKeys = true });
            YamlProperty[] merged = document.RootElement.GetProperty("m").EnumerateObject().ToArray();
            Assert.Equal(new[] { YamlKeyKind.Complex, YamlKeyKind.Scalar }, merged.Select(p => p.KeyKind));
            Assert.Equal(1, merged[0].Value.GetInt32());
            Assert.Equal(3, merged[1].Value.GetInt32());

            // A collection key repeated across a source and the own keys is merge order, as scalar keys.
            using YamlDocument own = YamlDocument.Parse("base: &b {[a]: 1}\nm:\n  <<: *b\n  [a]: 2\n", new YamlDocumentOptions { ResolveMergeKeys = true });
            Assert.Equal(2, own.RootElement.GetProperty("m").EnumerateObject().Single().Value.GetInt32());
        }

        // ------------------------------------------------------------------------------------------------------------
        // B6: object keys (decided under the parity rule)

        [Fact]
        public void B6_ObjectKeys()
        {
            Dictionary<object, object> read = YamlSerializer.Deserialize<Dictionary<object, object>>("a: 1\n[b, c]: 2\n{d: e}: 3\n", s_block)!;
            object[] keys = read.Keys.ToArray();
            Assert.Equal("a", Assert.IsType<string>(keys[0]));
            Assert.Equal(YamlValueKind.Array, Assert.IsType<YamlElement>(keys[1]).ValueKind);
            Assert.Equal(YamlValueKind.Object, Assert.IsType<YamlElement>(keys[2]).ValueKind);
            Assert.True(read.ContainsKey("a"));

            // Duplicates: scalar keys by the dictionary's comparer, collection keys by their canonical form.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<object, object>>("a: 1\na: 2\n", s_block));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<object, object>>("[a, b]: 1\n[a,b]: 2\n", s_block));
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<object, object>>("[a, b]: 1\n[a,b]: 2\n", With(YamlDuplicateKeyHandling.LastWins))!.Values.Single().ToString() is "2" ? 2 : 0);
            Assert.Equal("1", YamlSerializer.Deserialize<Dictionary<object, object>>("[a, b]: 1\n[a,b]: 2\n", With(YamlDuplicateKeyHandling.FirstWins))!.Values.Single().ToString());

            // 1 and 0x1 are two text keys.
            Assert.Equal(new object[] { "1", "0x1" }, YamlSerializer.Deserialize<Dictionary<object, object>>("1: a\n0x1: b\n", s_block)!.Keys);
        }

        // ------------------------------------------------------------------------------------------------------------
        // B7: the serializer, read

        [Fact]
        public void B7_TypedKeys_Read()
        {
            Dictionary<List<string>, int> lists = YamlSerializer.Deserialize<Dictionary<List<string>, int>>("[a, b]: 1\n? - c\n: 2\n", s_block)!;
            Assert.Equal(new[] { "a,b", "c" }, lists.Keys.Select(k => string.Join(",", k)));
            Assert.Equal(new[] { 1, 2 }, YamlSerializer.Deserialize<Dictionary<int[], string>>("[1, 2]: x\n", s_block)!.Keys.Single());
            Assert.Equal(new Point { X = 1, Y = 2 }, YamlSerializer.Deserialize<Dictionary<Point, string>>("{X: 1, Y: 2}: x\n", s_block)!.Keys.Single());
            Assert.Equal(new Pair("p", 3), YamlSerializer.Deserialize<Dictionary<Pair, string>>("{A: p, B: 3}: x\n", s_block)!.Keys.Single());
            Assert.Equal(new Cell { Row = 1, Column = 2 }, YamlSerializer.Deserialize<Dictionary<Cell, string>>("{Row: 1, Column: 2}: x\n", s_block)!.Keys.Single());
            Assert.Equal(new[] { 1 }, YamlSerializer.Deserialize<Dictionary<HashSet<int>, string>>("[1]: x\n", s_block)!.Keys.Single());
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<Dictionary<string, int>, string>>("{a: 2}: x\n", s_block)!.Keys.Single()["a"]);
            Assert.Equal(YamlValueKind.Array, YamlSerializer.Deserialize<Dictionary<YamlElement, string>>("[1]: x\n", s_block)!.Keys.Single().ValueKind);
            Assert.IsType<YamlArray>(YamlSerializer.Deserialize<Dictionary<YamlNode, string>>("[1]: x\n", s_block)!.Keys.Single());
        }

        [Fact]
        public void B7_ScalarKeyTypes_RefuseACollectionKey()
        {
            foreach (Action read in new Action[]
            {
                () => YamlSerializer.Deserialize<Dictionary<string, int>>("[a, b]: 1\n", s_block),
                () => YamlSerializer.Deserialize<Dictionary<int, int>>("[1]: 1\n", s_block),
                () => YamlSerializer.Deserialize<Dictionary<Color, int>>("{a: b}: 1\n", s_block),
            })
            {
                YamlException e = Assert.Throws<YamlException>(read);
                Assert.Contains("collection", e.Message);
            }

            // A scalar key into a collection key type keeps System.Text.Json's refusal.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<Dictionary<List<string>, int>>("a: 1\n", s_block));
        }

        public sealed class CsvListConverter : YamlConverter<List<string>>
        {
            public override List<string> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != YamlTokenType.EndArray)
                {
                    list.Add(reader.GetString()!.ToUpperInvariant());
                }

                return list;
            }

            public override void Write(Utf8YamlWriter writer, List<string> value, YamlSerializerOptions options) => writer.WriteStringValue(string.Join(",", value));

            public override List<string> ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetString()!.Split(',').ToList();

            public override void WriteAsPropertyName(Utf8YamlWriter writer, List<string> value, YamlSerializerOptions options) => writer.WritePropertyName(string.Join(",", value));
        }

        [Fact]
        public void B7_CustomKeyConverter()
        {
            var options = new YamlSerializerOptions(s_block) { Converters = { new CsvListConverter() } };

            // A collection key through Read; a scalar key through ReadAsPropertyName; writing through WriteAsPropertyName.
            Assert.Equal("A,B", string.Join(",", YamlSerializer.Deserialize<Dictionary<List<string>, int>>("[a, b]: 1\n", options)!.Keys.Single()));
            Assert.Equal("a,b", string.Join(",", YamlSerializer.Deserialize<Dictionary<List<string>, int>>("a,b: 1\n", options)!.Keys.Single()));
            Assert.Equal("a,b: 1\n", YamlSerializer.Serialize(new Dictionary<List<string>, int> { [new() { "a", "b" }] = 1 }, options));
        }

        [Fact]
        public void B7_MemberSites()
        {
            Assert.Equal(2, YamlSerializer.Deserialize<Target>("[a, b]: 1\nOther: 2\n", s_block)!.Other);
            Assert.Contains("[a, b]", Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Target>("[a, b]: 1\n", new YamlSerializerOptions(s_block) { UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow })).Message);
            Assert.Equal(0, YamlSerializer.Deserialize<Odd>("[a, b]: 1\n", s_block)!.V);
            Assert.Equal(1, YamlSerializer.Deserialize<Odd>("\"[a, b]\": 1\n", s_block)!.V);
        }

        // R5: string-keyed extension data refuses a collection key; a YamlObject holds it.
        [Fact]
        public async Task R5_ExtensionData()
        {
            Assert.Contains("String", Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<WithStringExtension>("[a, b]: 1\nX: 2\n", s_block)).Message);
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<WithElementExtension>("[a, b]: 1\nX: 2\n", s_block));
            Assert.Equal(1, (int)YamlSerializer.Deserialize<WithStringExtension>("q: 1\nX: 2\n", s_block)!.E!["q"].ToString()!.Length);

            WithNodeExtension node = YamlSerializer.Deserialize<WithNodeExtension>("[a, b]: 1\nX: 2\nz: 3\n", s_block)!;
            Assert.Equal(2, node.X);
            Assert.Equal("X: 2\n? [a, b]\n: 1\nz: 3\n", YamlSerializer.Serialize(node, s_block));
            WithNodeExtension streamed = (await YamlSerializer.DeserializeAsync<WithNodeExtension>(new MemoryStream("[a, b]: 1\nX: 2\n"u8.ToArray()), new YamlSerializerOptions(s_block) { DefaultBufferSize = 1 }))!;
            Assert.Equal("X: 2\n? [a, b]\n: 1\n", YamlSerializer.Serialize(streamed, s_block));
        }

        // ------------------------------------------------------------------------------------------------------------
        // B7: the serializer, write

        [Fact]
        public void B7_Write()
        {
            Assert.Equal("? - a\n  - b\n: 1\n", YamlSerializer.Serialize(new Dictionary<List<string>, int> { [new() { "a", "b" }] = 1 }, s_block));
            Assert.Equal("? X: 1\n  \"Y\": 2\n: x\n", YamlSerializer.Serialize(new Dictionary<Point, string> { [new Point { X = 1, Y = 2 }] = "x" }, s_block));
            // M5f: the boxed int key is written by its runtime type, plain.
            Assert.Equal("? - a\n: 1\ns: 2\n3: 3\n", YamlSerializer.Serialize(new Dictionary<object, object> { [new List<string> { "a" }] = 1, ["s"] = 2, [3] = 3 }, s_block));
            Assert.Equal("k:\n  ? - 1\n  : x\n", YamlSerializer.Serialize(new Dictionary<string, Dictionary<int[], string>> { ["k"] = new() { [new[] { 1 }] = "x" } }, s_block));

            // The JSON-compatible writer has no collection keys (leg YJ).
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(new Dictionary<List<string>, int> { [new() { "a" }] = 1 }, s_json));

            // DictionaryKeyPolicy renames text keys only.
            var policy = new YamlSerializerOptions(s_block) { DictionaryKeyPolicy = YamlNamingPolicy.CamelCase };
            Assert.Equal("? - Ab\n: 1\n", YamlSerializer.Serialize(new Dictionary<List<string>, int> { [new() { "Ab" }] = 1 }, policy));
            Assert.Equal("ab: 1\n", YamlSerializer.Serialize(new Dictionary<string, int> { ["Ab"] = 1 }, policy));
        }

        [Fact]
        public void B7_RoundTrips()
        {
            Dictionary<object, object> read = YamlSerializer.Deserialize<Dictionary<object, object>>("a: 1\n[b, c]: 2\n", s_block)!;
            Assert.Equal("a: 1\n? [b, c]\n: 2\n", YamlSerializer.Serialize(read, s_block));
            var points = new Dictionary<Point, string> { [new Point { X = 1 }] = "a", [new Point { X = 2 }] = "b" };
            Assert.Equal(points, YamlSerializer.Deserialize<Dictionary<Point, string>>(YamlSerializer.Serialize(points, s_block), s_block));
            Assert.Equal(YamlValueKind.Array, YamlSerializer.Deserialize<Dictionary<YamlNode, int>>(YamlSerializer.Serialize(new Dictionary<YamlNode, int> { [new YamlArray(1, 2)] = 3 }, s_block), s_block)!.Keys.Single().GetValueKind());
        }

        [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
        [YamlDerivedType(typeof(Square), "square")]
        public class Shape
        {
            public int Size { get; set; }

            public override bool Equals(object? obj) => obj is Shape s && s.GetType() == GetType() && s.Size == Size;

            public override int GetHashCode() => Size;
        }

        public class Square : Shape
        {
        }

        public class Dog
        {
            public string? Name { get; set; }
        }

        [Fact]
        public void B7_PolymorphicAndTaggedKeys()
        {
            Assert.IsType<Square>(YamlSerializer.Deserialize<Dictionary<Shape, int>>("!square {Size: 2}: 1\n", s_block)!.Keys.Single());
            Assert.Equal("? !square\n  Size: 2\n: 1\n", YamlSerializer.Serialize(new Dictionary<Shape, int> { [new Square { Size = 2 }] = 1 }, s_block));
            var mapped = new YamlSerializerOptions(s_block);
            mapped.TagMappings.Add("!dog", typeof(Dog));
            Assert.Equal("d", Assert.IsType<Dog>(YamlSerializer.Deserialize<Dictionary<object, int>>("!dog {Name: d}: 1\n", mapped)!.Keys.Single()).Name);
        }

        // ------------------------------------------------------------------------------------------------------------
        // C and R3: alias keys share, anchors across keys

        [Fact]
        public void R3_AliasKeysShare()
        {
            const string Yaml = "&k [a, b]: 1\n*k : 2\n";
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<List<string>, int>>(Yaml, s_block));
            Dictionary<List<string>, int> last = YamlSerializer.Deserialize<Dictionary<List<string>, int>>(Yaml, With(YamlDuplicateKeyHandling.LastWins))!;
            Assert.Equal(2, last.Single().Value);
            Assert.Equal(1, YamlSerializer.Deserialize<Dictionary<List<string>, int>>(Yaml, With(YamlDuplicateKeyHandling.FirstWins))!.Single().Value);

            // As an object key: the anchored element, so a repeated key too.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<object, object>>(Yaml, s_block));
        }

        [Fact]
        public void C_AnchorsAcrossKeys()
        {
            // An anchor in a key, aliased later in a value (typed and object sites).
            Assert.Equal(new[] { "a", "b" }, YamlSerializer.Deserialize<Holder>("M:\n  &k [a, b]: 1\nV: *k\n", s_block)!.V);
            Assert.Equal(YamlValueKind.Array, Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Dictionary<string, object>>("m:\n  &k [a, b]: 1\nv: *k\n", s_block)!["v"]).ValueKind);

            // A value's anchor, aliased later as a key.
            Holder holder = YamlSerializer.Deserialize<Holder>("V: &v [a, b]\nM:\n  *v : 2\n", s_block)!;
            Assert.Same(holder.V, holder.M!.Keys.Single());
            // At an object site the value is copied, and an alias to an anchor outside it is expanded (M5b): a collection key.
            YamlProperty copied = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Dictionary<string, object>>("v: &v [a, b]\nm:\n  *v : 2\n", s_block)!["m"]).EnumerateObject().Single();
            Assert.Equal(YamlKeyKind.Complex, copied.KeyKind);
            Assert.Equal("b", copied.GetKeyElement()[1].GetString());
        }

        [Fact]
        public void D_Preserve_RepeatedKeyInstance()
        {
            var key = new List<string> { "a" };
            var options = new YamlSerializerOptions(s_block) { ReferenceHandler = ReferenceHandler.Preserve };
            string yaml = YamlSerializer.Serialize(new Dictionary<List<string>, List<string>> { [key] = key }, options);
            Assert.Equal("? &1\n  - a\n: *1\n", yaml);
            Dictionary<List<string>, List<string>> back = YamlSerializer.Deserialize<Dictionary<List<string>, List<string>>>(yaml, options)!;
            Assert.Same(back.Keys.Single(), back.Values.Single());
        }

        // ------------------------------------------------------------------------------------------------------------
        // D: write and read again

        [Fact]
        public void D_ReReads()
        {
            using YamlDocument document = YamlDocument.Parse("m:\n  [a, b]: 1\n");
            YamlElement m = document.RootElement.GetProperty("m");
            Assert.Equal(1, m.Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());
            Assert.Equal(1, m.Clone().Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());
            Assert.Equal(1, YamlNode.Parse("[a, b]: 1\n")!.Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());

            var value = new Dictionary<List<string>, int> { [new() { "a" }] = 1 };
            Assert.Equal(1, YamlSerializer.SerializeToElement(value, s_block).Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());
            Assert.Equal(1, YamlSerializer.SerializeToNode(value, s_block)!.Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());
            using YamlDocument written = YamlSerializer.SerializeToDocument(value, s_block);
            Assert.Equal(1, written.Deserialize<Dictionary<List<string>, int>>(s_block)!.Values.Single());

            // The JSON-compatible style cannot hold one on any path.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.SerializeToElement(value, s_json));
        }

        [Fact]
        public void D_MergeRewrite_OnAndOff()
        {
            const string Yaml = "base: &b {[a, b]: 1}\nm:\n  <<: *b\n  c: 2\nother: {[x]: 3}\n";
            Dictionary<string, Dictionary<object, object>> on = YamlSerializer.Deserialize<Dictionary<string, Dictionary<object, object>>>(Yaml, new YamlSerializerOptions(s_block) { ResolveMergeKeys = true })!;
            Assert.Equal(2, on["m"].Count);
            Assert.Equal(YamlValueKind.Array, Assert.IsType<YamlElement>(on["m"].Keys.First()).ValueKind);
            Dictionary<string, Dictionary<object, object>> off = YamlSerializer.Deserialize<Dictionary<string, Dictionary<object, object>>>(Yaml, s_block)!;
            Assert.Equal("<<", off["m"].Keys.First());

            // Outside merged mappings both read the same.
            Assert.True(YamlElement.DeepEquals((YamlElement)on["other"].Keys.Single(), (YamlElement)off["other"].Keys.Single()));
        }

        [Fact]
        public void D_AliasExpansionAtAnObjectSite()
        {
            YamlElement copy = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Dictionary<string, object>>("a: &x {[k]: 1}\nb: *x\n", s_block)!["b"]);
            Assert.Equal(YamlKeyKind.Complex, copy.EnumerateObject().Single().KeyKind);
        }

        [Fact]
        public async Task D_StreamsAndSequences_KeyLongerThanTheBuffer()
        {
            string yaml = "[" + string.Join(", ", Enumerable.Repeat("abcdefgh", 50)) + "]: 1\n? - c\n  - d\n: 2\n";
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
            Dictionary<List<string>, int> fromString = YamlSerializer.Deserialize<Dictionary<List<string>, int>>(yaml, s_block)!;
            Dictionary<List<string>, int> fromStream = (await YamlSerializer.DeserializeAsync<Dictionary<List<string>, int>>(new MemoryStream(utf8), new YamlSerializerOptions(s_block) { DefaultBufferSize = 1 }))!;
            var reader = new Utf8YamlReader(SequenceFactory.Uniform(utf8, 1).Value);
            Dictionary<List<string>, int> fromSequence = YamlSerializer.Deserialize<Dictionary<List<string>, int>>(ref reader, s_block)!;
            foreach (Dictionary<List<string>, int> read in new[] { fromStream, fromSequence })
            {
                Assert.Equal(fromString.Keys.Select(k => string.Join(",", k)), read.Keys.Select(k => string.Join(",", k)));
            }

            // An anchor in a key, through a 1-byte stream.
            Holder holder = (await YamlSerializer.DeserializeAsync<Holder>(new MemoryStream("M:\n  &k [a, b]: 1\nV: *k\n"u8.ToArray()), new YamlSerializerOptions(s_block) { DefaultBufferSize = 1 }))!;
            Assert.Equal(new[] { "a", "b" }, holder.V);
        }

        [Theory]
        [InlineData(YamlSchemaKind.Core)]
        [InlineData(YamlSchemaKind.Yaml11)]
        [InlineData(YamlSchemaKind.Json)]
        [InlineData(YamlSchemaKind.Failsafe)]
        public void D_Schemas(YamlSchemaKind schema)
        {
            // A collection key's scalars are typed by the schema as values are.
            Dictionary<YamlElement, string> read = YamlSerializer.Deserialize<Dictionary<YamlElement, string>>("[1, \"a\"]: \"x\"\n", new YamlSerializerOptions(s_block) { Schema = schema })!;
            YamlElement key = read.Keys.Single();
            Assert.Equal(schema == YamlSchemaKind.Failsafe ? YamlValueKind.String : YamlValueKind.Number, key[0].ValueKind);
            Assert.Equal("a", key[1].GetString());
        }

        // ------------------------------------------------------------------------------------------------------------
        // E and R6: the schema's number forms in typed keys

        [Theory]
        // key, schema, int key (null: an error)
        [InlineData("0x1", YamlSchemaKind.Core, 1)]
        [InlineData("0o7", YamlSchemaKind.Core, 7)]
        [InlineData("+1", YamlSchemaKind.Core, 1)]
        [InlineData("01", YamlSchemaKind.Core, 1)]
        [InlineData("017", YamlSchemaKind.Core, 17)]
        [InlineData("0777", YamlSchemaKind.Core, 777)]
        [InlineData("010", YamlSchemaKind.Core, 10)]
        [InlineData("1_000", YamlSchemaKind.Core, null)]
        [InlineData("\"0x1\"", YamlSchemaKind.Core, 1)]
        [InlineData("1.0", YamlSchemaKind.Core, null)]
        [InlineData("1e3", YamlSchemaKind.Core, null)]
        [InlineData("0x1", YamlSchemaKind.Yaml11, 1)]
        [InlineData("1_000", YamlSchemaKind.Yaml11, 1000)]
        [InlineData("017", YamlSchemaKind.Yaml11, 15)]
        [InlineData("0777", YamlSchemaKind.Yaml11, 511)]
        [InlineData("010", YamlSchemaKind.Yaml11, 8)]
        [InlineData("-017", YamlSchemaKind.Yaml11, -15)]
        [InlineData("0b101", YamlSchemaKind.Yaml11, 5)]
        [InlineData("0x1", YamlSchemaKind.Json, null)]
        [InlineData("1", YamlSchemaKind.Json, 1)]
        [InlineData("0x1", YamlSchemaKind.Failsafe, null)]
        [InlineData("017", YamlSchemaKind.Failsafe, 17)]
        public void E_IntKeys(string key, YamlSchemaKind schema, int? expected)
        {
            var options = new YamlSerializerOptions { Schema = schema };
            string yaml = key + ": \"v\"\n";
            if (expected is null)
            {
                Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, options));
                return;
            }

            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, options)!.Keys.Single());
            Assert.Equal((long)expected.Value, YamlSerializer.Deserialize<Dictionary<long, string>>(yaml, options)!.Keys.Single());

            // R6: where the schema reads the plain value, the key agrees with it.
            if (key[0] != '"' && schema is YamlSchemaKind.Core or YamlSchemaKind.Yaml11)
            {
                Assert.Equal(expected, YamlSerializer.Deserialize<List<int>>("[" + key + "]", options)![0]);
            }
        }

        [Theory]
        [InlineData(".inf", YamlSchemaKind.Core, double.PositiveInfinity)]
        [InlineData("-.inf", YamlSchemaKind.Core, double.NegativeInfinity)]
        [InlineData("1e3", YamlSchemaKind.Core, 1000.0)]
        [InlineData("0x10", YamlSchemaKind.Core, 16.0)]
        [InlineData("1_0.5", YamlSchemaKind.Yaml11, 10.5)]
        public void E_DoubleKeys(string key, YamlSchemaKind schema, double expected)
        {
            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<double, string>>(key + ": \"v\"\n", new YamlSerializerOptions { Schema = schema })!.Keys.Single());
        }

        [Fact]
        public void E_OtherKeyTypes()
        {
            Assert.Equal(-0.5m, YamlSerializer.Deserialize<Dictionary<decimal, string>>("-.5: v\n")!.Keys.Single());
            // A decimal key reads as a decimal value does: Styml reads 0x1 into decimal (both libraries refuse it).
            Assert.Equal(1m, YamlSerializer.Deserialize<Dictionary<decimal, string>>("0x1: v\n")!.Keys.Single());
            Assert.Equal(1m, YamlSerializer.Deserialize<List<decimal>>("[0x1]")![0]);
            Assert.Equal(Color.Green, YamlSerializer.Deserialize<Dictionary<Color, string>>("0x1: v\n")!.Keys.Single());
            Assert.Equal((Color)15, YamlSerializer.Deserialize<Dictionary<Color, string>>("017: v\n", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.Keys.Single());
            Assert.Equal(Color.Blue, YamlSerializer.Deserialize<Dictionary<Color, string>>("2: v\n")!.Keys.Single());
            Assert.True(YamlSerializer.Deserialize<Dictionary<bool, string>>("yes: v\n", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.Keys.Single());
            Assert.True(YamlSerializer.Deserialize<Dictionary<bool, string>>("True: v\n")!.Keys.Single());
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<bool, string>>("yes: v\n"));

            // M5f: a number key is written plain, as both libraries write it; it read "1" quoted before.
            Assert.Equal("1: a\n", YamlSerializer.Serialize(new Dictionary<int, string> { [1] = "a" }, s_block));
        }

        [Fact]
        public void E_Collisions()
        {
            const string Yaml = "1: a\n0x1: b\n";
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, string>>(Yaml));
            Assert.Equal("b", YamlSerializer.Deserialize<Dictionary<int, string>>(Yaml, With(YamlDuplicateKeyHandling.LastWins))![1]);
            Assert.Equal("a", YamlSerializer.Deserialize<Dictionary<int, string>>(Yaml, With(YamlDuplicateKeyHandling.FirstWins))![1]);
            using YamlDocument document = YamlDocument.Parse(Yaml);
            Assert.Equal(2, document.RootElement.GetPropertyCount());
        }

        [Fact]
        public void ExplicitBlockSequenceKey_EmptyEntry_DoesNotCorruptMetadataDb()
        {
            // Issue #2: "? -"
            using (YamlDocument doc = YamlDocument.Parse("? -"))
            {
                YamlProperty prop = Assert.Single(doc.RootElement.EnumerateObject());
                Assert.Equal(YamlKeyKind.Complex, prop.KeyKind);
                Assert.Equal(YamlValueKind.Array, prop.GetKeyElement().ValueKind);
                Assert.Equal(YamlValueKind.Null, Assert.Single(prop.GetKeyElement().EnumerateArray()).ValueKind);
                Assert.Equal(YamlValueKind.Null, prop.Value.ValueKind);

                using var ms = new MemoryStream();
                using (var writer = new Utf8YamlWriter(ms))
                {
                    doc.WriteTo(writer);
                }
                Assert.NotEmpty(ms.ToArray());
            }

            YamlNode? node1 = YamlNode.Parse("? -");
            Assert.NotNull(node1);
            Assert.NotEmpty(node1!.ToYamlString());

            object? obj1 = YamlSerializer.Deserialize<object>("? -");
            Assert.NotNull(obj1);

            // Issue #2: "? -\na:"
            using (YamlDocument doc = YamlDocument.Parse("? -\na:"))
            {
                YamlProperty[] props = doc.RootElement.EnumerateObject().ToArray();
                Assert.Equal(2, props.Length);
                Assert.Equal(YamlKeyKind.Complex, props[0].KeyKind);
                Assert.Equal(YamlValueKind.Array, props[0].GetKeyElement().ValueKind);
                Assert.Equal(YamlValueKind.Null, Assert.Single(props[0].GetKeyElement().EnumerateArray()).ValueKind);
                Assert.Equal(YamlValueKind.Null, props[0].Value.ValueKind);
                Assert.Equal(YamlKeyKind.Scalar, props[1].KeyKind);
                Assert.Equal("a", props[1].Name);
                Assert.Equal(YamlValueKind.Null, props[1].Value.ValueKind);

                using var ms = new MemoryStream();
                using (var writer = new Utf8YamlWriter(ms))
                {
                    doc.WriteTo(writer);
                }
                Assert.NotEmpty(ms.ToArray());
            }

            YamlNode? node2 = YamlNode.Parse("? -\na:");
            Assert.NotNull(node2);
            Assert.NotEmpty(node2!.ToYamlString());

            object? obj2 = YamlSerializer.Deserialize<object>("? -\na:");
            Assert.NotNull(obj2);

            // Trailing empty entry in a multi-item block sequence complex key is preserved.
            using (YamlDocument doc = YamlDocument.Parse("? - a\n  -\n: v\n"))
            {
                YamlProperty prop = Assert.Single(doc.RootElement.EnumerateObject());
                Assert.Equal(YamlKeyKind.Complex, prop.KeyKind);
                YamlElement[] items = prop.GetKeyElement().EnumerateArray().ToArray();
                Assert.Equal(2, items.Length);
                Assert.Equal("a", items[0].GetString());
                Assert.Equal(YamlValueKind.Null, items[1].ValueKind);
                Assert.Equal("v", prop.Value.GetString());
            }

            // Explicit empty key inside an explicit block mapping key.
            using (YamlDocument doc = YamlDocument.Parse("? ?\na:"))
            {
                YamlProperty[] props = doc.RootElement.EnumerateObject().ToArray();
                Assert.Equal(2, props.Length);
                Assert.Equal(YamlKeyKind.Complex, props[0].KeyKind);
                Assert.Equal(YamlValueKind.Object, props[0].GetKeyElement().ValueKind);
                Assert.Equal("a", props[1].Name);
            }
        }
    }
}
