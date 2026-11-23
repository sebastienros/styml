// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M7c, the follow-ups of M7b: flow
    /// collections wrapped at <see cref="YamlWriterOptions.MaxLineWidth"/>, the column kept across flushes, a
    /// copy made through an alias written without the anchor, a collection key changed into a
    /// duplicate, and <see cref="YamlSerializerOptions.IgnoreSourceStyles"/>.
    /// The libraries' behaviour is in Styml.Parity.Tests (WriteBackFollowUpsParityTests).
    /// </summary>
    public class WriteBackFollowUpsTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_width40 = new(s_block) { MaxLineWidth = 40 };

        private static string Items(int count) => string.Join(", ", Enumerable.Range(1, count).Select(i => "item" + i));

        // ------------------------------------------------------------------------------------------------------------
        // B: flow collections and MaxLineWidth

        [Theory]
        [InlineData("a: [ITEMS]", "a: [item1, item2, item3, item4, item5, item6,\n  item7, item8, item9, item10, item11, item12,\n  item13, item14]\n")]
        [InlineData("x:\n  a: [ITEMS]", "x:\n  a: [item1, item2, item3, item4, item5, item6,\n    item7, item8, item9, item10, item11, item12,\n    item13, item14]\n")]
        [InlineData("- [ITEMS]", "- [item1, item2, item3, item4, item5, item6,\n  item7, item8, item9, item10, item11, item12,\n  item13, item14]\n")]
        [InlineData("[ITEMS]", "[item1, item2, item3, item4, item5, item6,\n  item7, item8, item9, item10, item11, item12,\n  item13, item14]\n")]
        [InlineData("{a: [ITEMS]}", "{a: [item1, item2, item3, item4, item5, item6,\n    item7, item8, item9, item10, item11, item12,\n    item13, item14]}\n")]
        [InlineData("a: [[ITEMS], b]", "a: [[item1, item2, item3, item4, item5, item6,\n    item7, item8, item9, item10, item11, item12,\n    item13, item14], b]\n")]
        [InlineData("a: {k: [ITEMS], m: [ITEMS]}", "a: {k: [item1, item2, item3, item4, item5,\n    item6, item7, item8, item9, item10, item11,\n    item12, item13, item14], m: [item1, item2,\n    item3, item4, item5, item6, item7, item8,\n    item9, item10, item11, item12, item13,\n    item14]}\n")]
        public void B_FlowCollections_WrapAtTheWidth(string source, string expected)
        {
            // The layouts are YamlDotNet's and SharpYaml's at the same width (WriteBackFollowUpsParityTests).
            string yaml = source.Replace("ITEMS", Items(14));
            string written = YamlNode.Parse(yaml)!.ToYamlString(s_width40);
            Assert.Equal(expected, written);
            Assert.True(YamlNode.DeepEquals(YamlNode.Parse(yaml), YamlNode.Parse(written)));
            Assert.Equal(written, YamlNode.Parse(written)!.ToYamlString(s_width40));

            // Without the width, one line, as before.
            Assert.DoesNotContain("\n ", YamlNode.Parse(yaml)!.ToYamlString(s_block).TrimEnd('\n').Replace("x:\n  a", "x a"));
        }

        [Fact]
        public void B_FlowBreaks_StayInsideEveryReadersBounds()
        {
            // Keys and their values stay on one line: a break comes after a comma or a bracket only. A collection key is
            // explicit (? [...]), so its lines may continue.
            var node = YamlNode.Parse("{" + string.Join(", ", Enumerable.Range(1, 12).Select(i => $"key{i}: value{i}")) + "}")!;
            string written = node.ToYamlString(s_width40);
            foreach (string line in written.Split('\n').Where(l => l.Length > 0))
            {
                Assert.DoesNotMatch(@"^\s*value", line);
            }

            var keyed = YamlNode.Parse("? [" + Items(14) + "]\n: v\n")!;
            string keyedWritten = keyed.ToYamlString(s_width40);
            Assert.StartsWith("? [item1, item2, item3, item4, item5, item6,\n  item7", keyedWritten);
            Assert.True(YamlNode.DeepEquals(keyed, YamlNode.Parse(keyedWritten)));

            // Written by the writer directly, in a flow collection the caller asked for.
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 20 }))
            {
                writer.WriteStartObject();
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray("list");
                for (int i = 0; i < 8; i++)
                {
                    writer.WriteNumberValue(1000 + i);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            string direct = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
            // A line breaks once it is past the width, so it may end one item over it, as libyaml's do.
            Assert.Equal("list: [1000, 1001, 1002,\n  1003, 1004, 1005, 1006,\n  1007]\n", direct);
            Assert.Equal(new[] { 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007 }, YamlSerializer.Deserialize<Dictionary<string, int[]>>(direct)!["list"]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // The column kept across flushes

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(37)]
        public async System.Threading.Tasks.Task Column_IsKeptAcrossFlushes(int bufferSize)
        {
            var options = new YamlSerializerOptions(s_width40) { DefaultBufferSize = bufferSize };
            var text = new Dictionary<string, string> { ["key"] = string.Join(" ", Enumerable.Range(1, 60).Select(i => "word" + i)) };
            YamlNode flow = YamlNode.Parse("k: [" + Items(40) + "]")!;

            foreach (object value in new object[] { text, flow })
            {
                string whole = YamlSerializer.Serialize(value, s_width40);
                using var stream = new MemoryStream();
                YamlSerializer.Serialize(stream, value, options);
                Assert.Equal(whole, Encoding.UTF8.GetString(stream.ToArray()));
                using var asyncStream = new MemoryStream();
                await YamlSerializer.SerializeAsync(asyncStream, value, options);
                Assert.Equal(whole, Encoding.UTF8.GetString(asyncStream.ToArray()));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // C: a copy made through an alias

        [Theory]
        [InlineData("a: &x [1, 2]\nb: *x\n", "a: &x [1, 2]\nb: [1, 2]\nc: 3\n")]
        [InlineData("a: &x {k: v}\nb: *x\n", "a: &x {k: v}\nb: {k: v}\nc: 3\n")]
        [InlineData("a: &s 1\nb: *s\n", "a: &s 1\nb: 1\nc: 3\n")]
        [InlineData("a: &x !t [1]\nb: *x\n", "a: &x !t [1]\nb: !t [1]\nc: 3\n")]
        public void C_AliasCopy_HasNoAnchor(string source, string mutated)
        {
            // Untouched, the alias is written as an alias.
            YamlObject node = YamlNode.Parse(source)!.AsObject();
            Assert.Equal(source, node.ToYamlString(s_block));
            Assert.Null(node["b"]!.Anchor);

            // Once the root is edited, the alias is a copy: the anchor is written once, by the anchored node.
            node["c"] = 3;
            string written = node.ToYamlString(s_block);
            Assert.Equal(mutated, written);
            Assert.True(YamlNode.DeepEquals(node, YamlNode.Parse(written)));
        }

        [Fact]
        public void C_AnAliasInsideAnUneditedChild_RedefinesTheAnchorAtItsFirstUse()
        {
            // Two cases. A top-level alias copy drops the name; an alias inside a child that is
            // written as its element (unedited, or a copy's own content) is written as the anchored node with its
            // anchor, since that element's anchor is outside it. YAML allows a redefined anchor, and both re-read equal.
            YamlObject node = YamlNode.Parse("a: &s text\nb: *s\nc: [*s]\n")!.AsObject();
            node["z"] = 1;
            string written = node.ToYamlString(s_block);
            Assert.Equal("a: &s text\nb: text\nc: [&s text]\nz: 1\n", written);
            Assert.True(YamlNode.DeepEquals(node, YamlNode.Parse(written)));

            YamlObject clone = YamlNode.Parse("a: &x {k: &y v, l: *y}\nb: *x\n")!.AsObject().DeepClone().AsObject();
            clone["z"] = 1;
            string cloneWritten = clone.ToYamlString(s_block);
            Assert.Equal("a: &x {k: &y v, l: *y}\nb: {k: &y v, l: &y v}\nz: 1\n", cloneWritten);
            Assert.True(YamlNode.DeepEquals(clone, YamlNode.Parse(cloneWritten)));
        }

        [Fact]
        public void C_DeepClone_KeepsTheNodesOwnAnchor()
        {
            YamlObject node = YamlNode.Parse("a: &x [1, 2]\nb: *x\n")!.AsObject();

            // The anchored node's clone keeps its anchor; the alias copy's clone has none.
            Assert.Equal("&x [1, 2]\n", node["a"]!.DeepClone().ToYamlString(s_block));
            Assert.Equal("x", node["a"]!.DeepClone().Anchor);
            Assert.Equal("[1, 2]\n", node["b"]!.DeepClone().ToYamlString(s_block));
            Assert.Null(node["b"]!.DeepClone().Anchor);

            // An anchor or tag set on the node is kept by its clone, edited or not.
            YamlNode set = YamlNode.Parse("a: [1]\n")!["a"]!;
            set.Anchor = "z";
            Assert.Equal("&z [1]\n", set.DeepClone().ToYamlString(s_block));
            var created = new YamlArray(1, 2) { Anchor = "q", Tag = "!list" };
            YamlNode clone = created.DeepClone();
            Assert.Equal("q", clone.Anchor);
            Assert.Equal("!list", clone.Tag);
            Assert.Equal("&q !list\n- 1\n- 2\n", clone.ToYamlString(s_block));
        }

        // ------------------------------------------------------------------------------------------------------------
        // D: a collection key changed into a duplicate (documented, not checked)

        [Fact]
        public void D_KeyChangedIntoADuplicate_IsTheCallersToKeepDistinct()
        {
            YamlObject node = YamlNode.Parse("? [k]\n: x\n? [k, k2]\n: y\n")!.AsObject();
            node.CollectionKeys[0].Key.AsArray().Add("k2");

            // Written as it is, and refused on read under the default duplicate-key handling.
            string written = node.ToYamlString(s_block);
            Assert.Equal("? [k, k2]\n: x\n? [k, k2]\n: y\n", written);
            Assert.Throws<YamlException>(() => YamlDocument.Parse(written));

            // The check is made when an entry is added.
            Assert.Throws<ArgumentException>(() => node.AddCollectionKey(new YamlArray("k", "k2"), 1));
        }

        // ------------------------------------------------------------------------------------------------------------
        // The writer under Schema = Json (M5f's follow-up)

        [Fact]
        public void JsonSchema_StringValuesAreQuoted_AndReadBack()
        {
            var json = new YamlSerializerOptions(s_block) { Schema = YamlSchemaKind.Json };
            var value = new Dictionary<string, object> { ["s"] = "text", ["n"] = 1, ["b"] = true, ["z"] = null!, ["d"] = new DateTime(2020, 1, 2), ["g"] = Guid.Empty, ["list"] = new[] { "a", "b" }, ["ml"] = "a\nb", ["one"] = "x y" };
            string written = YamlSerializer.Serialize(value, json);
            Assert.Equal("s: \"text\"\n\"n\": 1\nb: true\nz: null\nd: \"2020-01-02T00:00:00\"\ng: \"00000000-0000-0000-0000-000000000000\"\nlist:\n- \"a\"\n- \"b\"\nml: |-\n  a\n  b\none: \"x y\"\n", written);
            Dictionary<string, object> back = YamlSerializer.Deserialize<Dictionary<string, object>>(written, json)!;
            Assert.Equal(new[] { "s", "n", "b", "z", "d", "g", "list", "ml", "one" }, back.Keys);

            // A DOM's plain source scalars, and a member's Plain, are quoted too; keys and block scalars are unchanged.
            Assert.Equal("a: \"b\"\nc: [\"d\"]\n", YamlNode.Parse("a: b\nc: [d]\n")!.ToYamlString(json));

            // The core schema's output is unchanged.
            Assert.Equal("s: text\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["s"] = "text" }, s_block));

            // A non-finite number has no JSON-schema form.
            string nan = YamlSerializer.Serialize(new Dictionary<string, double> { ["x"] = double.NaN }, json);
            Assert.Equal("x: .nan\n", nan);
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, double>>(nan, json));
        }

        // ------------------------------------------------------------------------------------------------------------
        // IgnoreSourceStyles

        public sealed class Nested
        {
            public bool flag { get; set; }
            public string? s { get; set; }
            public string? text { get; set; }
        }

        public sealed class Doc
        {
            public string? name { get; set; }
            public int[]? items { get; set; }
            public Nested? nested { get; set; }
            public List<string>? empty { get; set; }
        }

        private const string Json = """{"name": "x", "items": [1, 2], "nested": {"flag": true, "s": "12", "text": "a\nb"}, "empty": []}""";

        private static Doc Typed() => new() { name = "x", items = new[] { 1, 2 }, nested = new Nested { flag = true, s = "12", text = "a\nb" }, empty = new() };

        [Fact]
        public void IgnoreSourceStyles_JsonIn_BlockYamlOut_AsTheTypedGraph()
        {
            var options = new YamlSerializerOptions(s_block) { IgnoreSourceStyles = true };
            string typed = YamlSerializer.Serialize(Typed(), s_block);
            Assert.Equal("name: x\nitems:\n- 1\n- 2\nnested:\n  flag: true\n  s: \"12\"\n  text: |-\n    a\n    b\nempty: []\n", typed);

            // The node, the document and the element, each equal to the typed graph.
            Assert.Equal(typed, YamlNode.Parse(Json)!.ToYamlString(options));
            using YamlDocument document = YamlDocument.Parse(Json);
            Assert.Equal(typed, Write(document.RootElement.WriteTo, options.GetWriterOptions()));
            Assert.Equal(typed, Write(document.WriteTo, options.GetWriterOptions()));
            Assert.Equal(typed, YamlSerializer.Serialize(YamlSerializer.Deserialize<object>(Json, s_block), options));

            // Off (the default): the source's styles, a fixed point.
            string kept = YamlNode.Parse(Json)!.ToYamlString(s_block);
            Assert.Equal("{\"name\": \"x\", \"items\": [1, 2], \"nested\": {\"flag\": true, \"s\": \"12\", \"text\": \"a\\nb\"}, \"empty\": []}\n", kept);
            Assert.Equal(kept, YamlNode.Parse(kept)!.ToYamlString(s_block));

            // Edited, too.
            YamlObject edited = YamlNode.Parse(Json)!.AsObject();
            edited["more"] = "z";
            Assert.Equal(typed + "more: z\n", edited.ToYamlString(options));
        }

        [Fact]
        public void IgnoreSourceStyles_FlowYamlSource()
        {
            const string Flow = "{name: 'x', items: [1, 2], nested: {flag: true, s: '12', text: \"a\\nb\"}, empty: []}";
            var options = new YamlSerializerOptions(s_block) { IgnoreSourceStyles = true };
            Assert.Equal(YamlSerializer.Serialize(Typed(), s_block), YamlNode.Parse(Flow)!.ToYamlString(options));

            // A block source's literal, folded and quoted scalars come back as the writer chooses them.
            const string Styles = "a: 'single'\nb: \"double\"\nc: >-\n  folded\nd: |-\n  x\n  y\ne: plain\n";
            Assert.Equal("a: single\nb: double\nc: folded\nd: |-\n  x\n  y\ne: plain\n", YamlNode.Parse(Styles)!.ToYamlString(options));
        }

        [Fact]
        public void IgnoreSourceStyles_KeepsAnchorsAndTags_AndTheStylesSetExplicitly()
        {
            var options = new YamlSerializerOptions(s_block) { IgnoreSourceStyles = true };

            // Anchors, aliases and tags are content.
            Assert.Equal("a: &x !t\n- 1\nb: *x\n", YamlNode.Parse("a: &x !t [1]\nb: *x\n")!.ToYamlString(options));

            // A style the caller set on a node goes first; a source's does not.
            YamlObject node = YamlNode.Parse("{a: [1, 2], b: [3]}")!.AsObject();
            node["b"]!.AsArray().CollectionStyle = YamlCollectionStyle.Flow;
            Assert.Equal("a:\n- 1\n- 2\nb: [3]\n", node.ToYamlString(options));

            // A member's style and a converter's go first too; DefaultScalarStyle and MultiLineStringStyle apply.
            var holder = new StyledNode { Value = "v", Node = YamlNode.Parse("{\"k\": \"x\", \"m\": \"a\\nb\"}") };
            var styled = new YamlSerializerOptions(options) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted, MultiLineStringStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal("Value: \"v\"\nNode:\n  'k': 'x'\n  'm': \"a\\nb\"\n", YamlSerializer.Serialize(holder, styled));

            // The writer option alone does the same for a document written to a writer.
            using YamlDocument document = YamlDocument.Parse("{\"k\": [1]}");
            Assert.Equal("k:\n- 1\n", Write(document.WriteTo, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", IgnoreSourceStyles = true }));

            // Ignored in JSON-compatible style, and copied with the options.
            Assert.Equal("{\"k\":[1]}", Write(document.WriteTo, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible, IgnoreSourceStyles = true }));
            Assert.True(new YamlSerializerOptions(options).IgnoreSourceStyles);
            Assert.True(options.GetWriterOptions().IgnoreSourceStyles);
            YamlSerializer.Serialize(1, options);
            Assert.Throws<InvalidOperationException>(() => options.IgnoreSourceStyles = false);
        }

        public sealed class StyledNode
        {
            [YamlScalarStyle(YamlScalarStyle.DoubleQuoted)]
            public string? Value { get; set; }

            public YamlNode? Node { get; set; }
        }

        private static string Write(Action<Utf8YamlWriter> write, YamlWriterOptions options)
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options))
            {
                write(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }
    }
}
