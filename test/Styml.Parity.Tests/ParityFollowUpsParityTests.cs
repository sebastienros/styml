// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using Xunit;
using SY = SharpYaml;
using YC = YamlDotNet.Core;
using YE = YamlDotNet.Core.Events;
using YD = YamlDotNet.Serialization;
using YR = YamlDotNet.RepresentationModel;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M7b, against YamlDotNet 18.1.0 and SharpYaml 3.17.0, run live:
    /// the five parity follow-ups, each beside what the libraries do. Item 1, flow collections kept on DOM write-back
    /// (both libraries keep them); item 2, a multi-line single-quoted string (YamlDotNet writes it, SharpYaml
    /// double-quotes it); item 3, a serializer-wide default scalar style (YamlDotNet's WithDefaultScalarStyle; SharpYaml
    /// has none); item 4, collection keys in the DOM (YamlDotNet's YamlMappingNode; SharpYaml's model writes them back);
    /// item 5, a key converter that writes a collection key (YamlDotNet's type converters; SharpYaml writes the key's
    /// text). The differences Styml keeps on purpose are asserted too, so a library that changes shows here.
    /// </summary>
    public class ParityFollowUpsParityTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();
        private static readonly SY.YamlSerializerOptions s_sharpYaml = new();
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        private static string YamlDotNetWriteBack(string yaml)
        {
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(yaml));
            var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            return Lf(writer.ToString());
        }

        private static string SharpYamlWriteBack(string yaml)
        {
            SY.Model.YamlStream stream = SY.Model.YamlStream.Load(new StringReader(yaml));
            var writer = new StringWriter();
            stream.WriteTo(writer, true);
            return Lf(writer.ToString());
        }

        private static string StymlWriteBack(string yaml) => YamlNode.Parse(yaml)!.ToYamlString(s_block);

        // ------------------------------------------------------------------------------------------------------------
        // Item 1: both libraries keep a flow collection on DOM write-back, and so does Styml

        [Theory]
        [InlineData("{a: 1, b: [1, 2]}", "{a: 1, b: [1, 2]}\n")]
        [InlineData("a: [1, 2]\nb:\n- 3\n", "a: [1, 2]\nb:\n- 3\n")]
        [InlineData("{\"a\": {\"b\": 1}}", "{\"a\": {\"b\": 1}}\n")]
        [InlineData("a: {}\nb: []\n", "a: {}\nb: []\n")]
        [InlineData("a:\n  b: [x, {c: d}]\n", "a:\n  b: [x, {c: d}]\n")]
        public void Item1_FlowCollections_AreKept(string yaml, string expected)
        {
            // YamlDotNet ends the document with "..."; SharpYaml indents a block sequence under its key.
            Assert.Equal(expected + "...\n", YamlDotNetWriteBack(yaml));
            Assert.Equal(expected, SharpYamlWriteBack(yaml).Replace("\n  - ", "\n- "));
            Assert.Equal(expected, StymlWriteBack(yaml));
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                var buffer = new Buffers.ArrayBufferWriter<byte>();
                using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
                {
                    document.RootElement.WriteTo(writer);
                }

                Assert.Equal(expected, Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
            }

            // Both libraries read Styml's write-back as the source.
            var source = new YR.YamlStream();
            source.Load(new StringReader(yaml));
            var back = new YR.YamlStream();
            back.Load(new StringReader(StymlWriteBack(yaml)));
            Assert.True(source.Documents[0].RootNode.Equals(back.Documents[0].RootNode));
            Assert.Equal(SharpYamlWriteBack(yaml), SharpYamlWriteBack(StymlWriteBack(yaml)));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 2: a multi-line single-quoted string, as YamlDotNet writes it

        public sealed class SingleQuotedYamlDotNet
        {
            [YD.YamlMember(ScalarStyle = YC.ScalarStyle.SingleQuoted)]
            public string? K { get; set; }
        }

        public sealed class SingleQuotedStyml
        {
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
            public string? K { get; set; }
        }

        [Theory]
        [InlineData("a\nb")]
        [InlineData("a\n\nb")]
        [InlineData(" a\nb ")]
        [InlineData("it's\nx")]
        [InlineData("\na")]
        public void Item2_MultiLineSingleQuoted_AsYamlDotNet(string value)
        {
            string styml = YamlSerializer.Serialize(new SingleQuotedStyml { K = value }, s_block);
            Assert.Equal(Lf(new YD.SerializerBuilder().Build().Serialize(new SingleQuotedYamlDotNet { K = value })), styml);
            Assert.Equal(value, s_yamlDotNet.Deserialize<SingleQuotedYamlDotNet>(styml).K);
            Assert.Equal(value, SY.YamlSerializer.Deserialize<Dictionary<string, string>>(styml, s_sharpYaml)!["K"]);
        }

        [Fact]
        public void Item2_TheDifferences()
        {
            // After a final line break YamlDotNet puts the closing quote at column 0, Styml at the content's column; both
            // read as "a\nb\n" in both libraries.
            string ydn = Lf(new YD.SerializerBuilder().Build().Serialize(new SingleQuotedYamlDotNet { K = "a\nb\n" }));
            string styml = YamlSerializer.Serialize(new SingleQuotedStyml { K = "a\nb\n" }, s_block);
            Assert.Equal("K: 'a\n\n  b\n\n'\n", ydn);
            Assert.Equal("K: 'a\n\n  b\n\n  '\n", styml);
            foreach (string yaml in new[] { ydn, styml })
            {
                Assert.Equal("a\nb\n", s_yamlDotNet.Deserialize<SingleQuotedYamlDotNet>(yaml).K);
                Assert.Equal("a\nb\n", SY.YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, s_sharpYaml)!["K"]);
            }

            // SharpYaml's model writes a single-quoted node with a line break double-quoted.
            var mapping = new SY.Model.YamlMapping();
            mapping.Add(new SY.Model.YamlValue("k"), new SY.Model.YamlValue("a\nb") { Style = SY.ScalarStyle.SingleQuoted });
            var writer = new StringWriter();
            mapping.WriteTo(writer, true);
            Assert.StartsWith("k: \"", writer.ToString());

            // A space next to a line break: single quotes' line folding would drop it, so Styml double-quotes the string.
            Assert.Equal("K: \"a \\nb\"\n", YamlSerializer.Serialize(new SingleQuotedStyml { K = "a \nb" }, s_block));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 3: DefaultScalarStyle beside YamlDotNet's WithDefaultScalarStyle

        public sealed class Holder
        {
            public string? Plain { get; set; } = "x";
            public string? Multi { get; set; } = "a\nb";
            public int Number { get; set; } = 1;
        }

        public sealed class StyledYamlDotNet
        {
            public string? Plain { get; set; } = "x";

            [YD.YamlMember(ScalarStyle = YC.ScalarStyle.Plain)]
            public string? Styled { get; set; } = "s";
        }

        public sealed class StyledStyml
        {
            public string? Plain { get; set; } = "x";

            [YamlScalarStyle(YamlScalarStyle.Plain)]
            public string? Styled { get; set; } = "s";
        }

        [Theory]
        [InlineData(YamlScalarStyle.SingleQuoted, YC.ScalarStyle.SingleQuoted, "Plain: 'x'\nMulti: 'a\n\n  b'\nNumber: 1\n")]
        [InlineData(YamlScalarStyle.DoubleQuoted, YC.ScalarStyle.DoubleQuoted, "Plain: \"x\"\nMulti: \"a\\nb\"\nNumber: 1\n")]
        [InlineData(YamlScalarStyle.Folded, YC.ScalarStyle.Folded, "Plain: x\nMulti: >-\n  a\n\n  b\nNumber: 1\n")]
        public void Item3_DefaultScalarStyle_BesideYamlDotNet(YamlScalarStyle style, YC.ScalarStyle libraryStyle, string expected)
        {
            string styml = YamlSerializer.Serialize(new Holder(), new YamlSerializerOptions(s_block) { DefaultScalarStyle = style });
            Assert.Equal(expected, styml);
            string ydn = Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(libraryStyle).Build().Serialize(new Holder()));

            // Both read each other's output to the same values.
            foreach (string yaml in new[] { styml, ydn })
            {
                Holder ydnRead = s_yamlDotNet.Deserialize<Holder>(yaml);
                Holder syRead = SY.YamlSerializer.Deserialize<Holder>(yaml, s_sharpYaml)!;
                Holder stymlRead = YamlSerializer.Deserialize<Holder>(yaml, s_block)!;
                foreach (Holder read in new[] { ydnRead, syRead, stymlRead })
                {
                    Assert.Equal("x", read.Plain);
                    Assert.Equal("a\nb", read.Multi);
                    Assert.Equal(1, read.Number);
                }
            }

            // The multi-line value is the same text in both writers (YamlDotNet's single-line Folded one differs, below).
            Assert.Contains(expected.Split('\n')[1], ydn);
        }

        [Fact]
        public void Item3_TheDifferences_AndThePrecedence()
        {
            // A member's style wins over the default in both.
            Assert.Equal("Plain: \"x\"\nStyled: s\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.DoubleQuoted).Build().Serialize(new StyledYamlDotNet())));
            Assert.Equal("Plain: \"x\"\nStyled: s\n", YamlSerializer.Serialize(new StyledStyml(), new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));

            // A dictionary's string keys are styled in both (fix A); "n" is quoted by both anyway.
            var map = new Dictionary<string, object> { ["k"] = "v", ["n"] = 1 };
            Assert.Equal("\"k\": \"v\"\n\"n\": 1\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.DoubleQuoted).Build().Serialize(map)));
            Assert.Equal("\"k\": \"v\"\n\"n\": 1\n", YamlSerializer.Serialize(map, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));

            // Where the style cannot hold the text, YamlDotNet picks single quotes or a block scalar; Styml falls through
            // to its own default (literal for a line break, plain for one line).
            Assert.Equal("Plain: x\nMulti: 'a\n\n  b'\nNumber: 1\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.Plain).Build().Serialize(new Holder())));
            Assert.Equal("Plain: x\nMulti: |-\n  a\n  b\nNumber: 1\n", YamlSerializer.Serialize(new Holder(), new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.Plain }));
            Assert.Equal("Plain: |-\n  x\nMulti: |-\n  a\n  b\nNumber: 1\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.Literal).Build().Serialize(new Holder())));
            Assert.Equal("Plain: x\nMulti: |-\n  a\n  b\nNumber: 1\n", YamlSerializer.Serialize(new Holder(), new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.Literal }));

            // SharpYaml has no serializer-wide scalar style.
            Assert.DoesNotContain(typeof(SY.YamlSerializerOptions).GetProperties(), p => p.Name.Contains("DefaultScalarStyle") || p.Name == "ScalarStyle");
        }

        public sealed class Kinds
        {
            public string S { get; set; } = "text";
            public int N { get; set; } = 3;
            public bool B { get; set; } = true;
            public string? Null { get; set; }
            public DateTime When { get; set; } = new DateTime(2020, 1, 2);
            public Guid Id { get; set; } = new Guid("01234567-89ab-cdef-0123-456789abcdef");
            public char C { get; set; } = 'c';
            public Dictionary<string, int> Map { get; set; } = new() { ["key"] = 1 };
        }

        [Fact]
        public void Item3_WhatTheDefaultReaches_BesideYamlDotNet()
        {
            // Fix A's probe. YamlDotNet's WithDefaultScalarStyle styles strings, chars and a dictionary's string keys; it
            // leaves member names, numbers, booleans and nulls alone, and writes dates and GUIDs plain.
            string ydn = Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.DoubleQuoted).Build().Serialize(new Kinds()));
            Assert.Equal("S: \"text\"\nN: 3\nB: true\nNull: \nWhen: 2020-01-02T00:00:00.0000000\nId: 01234567-89ab-cdef-0123-456789abcdef\nC: \"c\"\nMap:\n  \"key\": 1\n", ydn);

            // Styml: the same keys and the same untouched kinds; dates and GUIDs, which Styml writes as YAML strings, are
            // styled too, beyond YamlDotNet.
            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            string styml = YamlSerializer.Serialize(new Kinds(), options);
            Assert.Equal("S: \"text\"\n\"N\": 3\nB: true\n\"Null\": null\nWhen: \"2020-01-02T00:00:00\"\nId: \"01234567-89ab-cdef-0123-456789abcdef\"\nC: \"c\"\nMap:\n  \"key\": 1\n", styml);

            // Both libraries read Styml's output to the same values.
            Kinds fromYdn = s_yamlDotNet.Deserialize<Kinds>(styml);
            Kinds fromSy = SY.YamlSerializer.Deserialize<Kinds>(styml, s_sharpYaml)!;
            foreach (Kinds read in new[] { fromYdn, fromSy })
            {
                Assert.Equal(3, read.N);
                Assert.True(read.B);
                Assert.Null(read.Null);
                Assert.Equal(new DateTime(2020, 1, 2), read.When);
                Assert.Equal(new Kinds().Id, read.Id);
                Assert.Equal(1, read.Map["key"]);
            }

            // Number and boolean keys stay plain in both.
            var keys = new Dictionary<object, object> { ["k"] = "v", [1] = 2, [true] = "b" };
            Assert.Equal("\"k\": \"v\"\n1: 2\ntrue: \"b\"\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.DoubleQuoted).Build().Serialize(keys)));
            Assert.Equal("\"k\": \"v\"\n1: 2\ntrue: \"b\"\n", YamlSerializer.Serialize(keys, options));

            // A literal or folded default on a key: YamlDotNet double-quotes it, Styml writes it as its own choice (plain
            // here), under the fall-through rule; a key cannot be a block scalar in either.
            var literalMap = new Dictionary<string, string> { ["k"] = "v" };
            Assert.Equal("\"k\": |-\n  v\n", Lf(new YD.SerializerBuilder().WithDefaultScalarStyle(YC.ScalarStyle.Literal).Build().Serialize(literalMap)));
            Assert.Equal("k: v\n", YamlSerializer.Serialize(literalMap, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.Literal }));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 4: collection keys in the DOM

        private const string Keyed = "a: 1\n? [x, y]\n: 2\nb: 3\n";

        [Fact]
        public void Item4_CollectionKeys_BesideYamlDotNet()
        {
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(Keyed));
            var root = (YR.YamlMappingNode)stream.Documents[0].RootNode;
            YamlObject node = YamlNode.Parse(Keyed)!.AsObject();

            // YamlDotNet counts every entry; Styml's string-keyed Count does not include CollectionKeys.
            Assert.Equal(3, root.Children.Count);
            Assert.Equal(2, node.Count);
            Assert.Single(node.CollectionKeys);
            Assert.Equal(root.Children.Count, node.Count + node.CollectionKeys.Count);

            // Document order on enumeration (YamlDotNet) and on write-back (both).
            Assert.Equal(new[] { "a", "[ x, y ]", "b" }, root.Children.Select(p => p.Key.ToString()));
            Assert.Equal(new[] { "a", "b" }, node.Select(p => p.Key));
            Assert.Equal(Keyed + "...\n", YamlDotNetWriteBack(Keyed));
            Assert.Equal(Keyed, node.ToYamlString(s_block));

            // An added collection key goes after every entry, and a string key added next after it, in both.
            root.Children.Add(new YR.YamlSequenceNode(new YR.YamlScalarNode("z")), new YR.YamlScalarNode("4"));
            root.Children.Add(new YR.YamlScalarNode("c"), new YR.YamlScalarNode("5"));
            var saved = new StringWriter();
            stream.Save(saved, assignAnchors: false);
            node.AddCollectionKey(new YamlArray("z"), 4);
            node["c"] = 5;
            Assert.Equal("a: 1\n? [x, y]\n: 2\nb: 3\n? - z\n: 4\nc: 5\n...\n", Lf(saved.ToString()));
            Assert.Equal("a: 1\n? [x, y]\n: 2\nb: 3\n? - z\n: 4\nc: 5\n", node.ToYamlString(s_block));

            // Equality ignores the order of entries, collection keys included, in both.
            const string Reordered = "? [x, y]\n: 2\na: 1\nb: 3\n";
            var reordered = new YR.YamlStream();
            reordered.Load(new StringReader(Reordered));
            var original = new YR.YamlStream();
            original.Load(new StringReader(Keyed));
            Assert.True(original.Documents[0].RootNode.Equals(reordered.Documents[0].RootNode));
            Assert.True(YamlNode.DeepEquals(YamlNode.Parse(Keyed), YamlNode.Parse(Reordered)));
        }

        [Fact]
        public void Item4_SharpYamlsModel_WritesThemBack()
        {
            const string Yaml = "a: 1\n? [x, y]\n: 2\n";
            Assert.Equal(Yaml, SharpYamlWriteBack(Yaml));
            Assert.Equal(Yaml, StymlWriteBack(Yaml));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 5: a key converter that writes a collection key

        public sealed record Point(int X, int Y);

        private sealed class YamlDotNetPointConverter : YD.IYamlTypeConverter
        {
            public bool Accepts(Type type) => type == typeof(Point);

            public object ReadYaml(YC.IParser parser, Type type, YD.ObjectDeserializer rootDeserializer) => throw new NotSupportedException();

            public void WriteYaml(YC.IEmitter emitter, object? value, Type type, YD.ObjectSerializer serializer)
            {
                var point = (Point)value!;
                emitter.Emit(new YE.SequenceStart(null, null, false, YE.SequenceStyle.Flow));
                emitter.Emit(new YE.Scalar(point.X.ToString(Globalization.CultureInfo.InvariantCulture)));
                emitter.Emit(new YE.Scalar(point.Y.ToString(Globalization.CultureInfo.InvariantCulture)));
                emitter.Emit(new YE.SequenceEnd());
            }
        }

        private sealed class StymlPointConverter : YamlConverter<Point>
        {
            public override Point Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotSupportedException();

            public override void Write(Utf8YamlWriter writer, Point value, YamlSerializerOptions options)
            {
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray();
                writer.WriteNumberValue(value.X);
                writer.WriteNumberValue(value.Y);
                writer.WriteEndArray();
            }

            public override void WriteAsPropertyName(Utf8YamlWriter writer, Point value, YamlSerializerOptions options)
            {
                writer.WriteStartPropertyKey();
                Write(writer, value, options);
                writer.WriteEndPropertyKey();
            }
        }

        [Fact]
        public void Item5_KeyConverter_AsYamlDotNet()
        {
            var map = new Dictionary<Point, string> { [new Point(1, 2)] = "p" };
            string ydn = Lf(new YD.SerializerBuilder().WithTypeConverter(new YamlDotNetPointConverter()).Build().Serialize(map));
            string styml = YamlSerializer.Serialize(map, new YamlSerializerOptions(s_block) { Converters = { new StymlPointConverter() } });
            Assert.Equal("? [1, 2]\n: p\n", ydn);
            Assert.Equal(ydn, styml);

            // SharpYaml writes the key's text.
            Assert.Equal("\"Point { X = 1, Y = 2 }\": p\n", Lf(SY.YamlSerializer.Serialize(map)));

            // YamlDotNet's DOM reads Styml's key as a sequence.
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(styml));
            Assert.IsType<YR.YamlSequenceNode>(((YR.YamlMappingNode)stream.Documents[0].RootNode).Children.Single().Key);
        }
    }
}
