// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M7b: the five parity follow-ups of M5.
    /// Item 1, a collection's flow style kept on DOM write-back; item 2, a multi-line single-quoted string in the writer;
    /// item 3, <see cref="YamlSerializerOptions.DefaultScalarStyle"/> and its precedence; item 4,
    /// <see cref="YamlObject.CollectionKeys"/>; item 5, a key converter that writes a collection key. Every output is read
    /// back by Styml; the libraries' behaviour is in Styml.Parity.Tests (ParityFollowUpsParityTests).
    /// </summary>
    public class ParityFollowUpsTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_json = new() { WriteStyle = YamlWriterStyle.JsonCompatible };
        private static readonly YamlWriterOptions s_writer = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };

        private static string Write(Action<Utf8YamlWriter> write, YamlWriterOptions? options = null)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options ?? s_writer))
            {
                write(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static string WriteBack(YamlElement element, YamlWriterOptions? options = null) => Write(element.WriteTo, options);

        // ------------------------------------------------------------------------------------------------------------
        // Item 1: the collection style

        private const string Mixed = "a: [1, 2]\nb:\n- 3\nc: {x: 1}\nd:\n  u: 2\ne: 1\nf: [p: q, r]\n";

        [Fact]
        public void Item1_Element_ReportsItsSourceStyle()
        {
            using YamlDocument document = YamlDocument.Parse(Mixed);
            YamlElement root = document.RootElement;
            Assert.Equal(YamlCollectionStyle.Block, root.CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, root.GetProperty("a").CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Block, root.GetProperty("b").CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, root.GetProperty("c").CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Block, root.GetProperty("d").CollectionStyle);
            Assert.Null(root.GetProperty("e").CollectionStyle);

            // A single-pair mapping inside a flow sequence has no brackets of its own, and is flow, cloned too.
            Assert.Equal(YamlCollectionStyle.Flow, root.GetProperty("f")[0].CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, root.GetProperty("f")[0].Clone().CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, root.GetProperty("f").Clone()[0].CollectionStyle);
            using YamlDocument commented = YamlDocument.Parse("- [a: b # c\n  , d]\n");
            Assert.Equal(YamlCollectionStyle.Flow, commented.RootElement[0][0].CollectionStyle);

            // An alias gives its anchored node's style.
            using YamlDocument aliased = YamlDocument.Parse("a: &x [1]\nb: *x\nc: &y\n- 2\nd: *y\n");
            Assert.Equal(YamlCollectionStyle.Flow, aliased.RootElement.GetProperty("b").CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Block, aliased.RootElement.GetProperty("d").CollectionStyle);

            // JSON's collections are flow.
            using YamlDocument json = YamlDocument.Parse("{\"a\": [1, {\"b\": 2}]}");
            Assert.Equal(YamlCollectionStyle.Flow, json.RootElement.CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, json.RootElement.GetProperty("a")[1].CollectionStyle);
        }

        [Fact]
        public void Item1_Document_WritesBackItsStyles()
        {
            using YamlDocument document = YamlDocument.Parse(Mixed);
            string written = WriteBack(document.RootElement);
            Assert.Equal("a: [1, 2]\nb:\n- 3\nc: {x: 1}\nd:\n  u: 2\ne: 1\nf: [{p: q}, r]\n", written);
            using YamlDocument back = YamlDocument.Parse(written);
            Assert.True(YamlElement.DeepEquals(document.RootElement, back.RootElement));
            Assert.Equal(written, WriteBack(back.RootElement));
            Assert.Equal(written, WriteBack(document.RootElement.Clone()));

            // A nested element, and the root of a flow document.
            Assert.Equal("[1, 2]\n", WriteBack(document.RootElement.GetProperty("a")));
            using YamlDocument flowRoot = YamlDocument.Parse("{a: [b, {c: d}]}");
            Assert.Equal("{a: [b, {c: d}]}\n", WriteBack(flowRoot.RootElement));

            // The JSON-compatible writer writes flow whatever the source.
            Assert.Equal("{\"a\":[1,2],\"b\":[3],\"c\":{\"x\":1},\"d\":{\"u\":2},\"e\":1,\"f\":[{\"p\":\"q\"},\"r\"]}",
                WriteBack(document.RootElement, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }));
        }

        [Fact]
        public void Item1_Node_KeepsItsSourceStyle_AcrossMaterialization()
        {
            YamlObject node = YamlNode.Parse(Mixed)!.AsObject();
            Assert.Equal(YamlCollectionStyle.Block, node.CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, node["a"]!.AsArray().CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Block, node["b"]!.AsArray().CollectionStyle);
            Assert.Equal(YamlCollectionStyle.Flow, node["c"]!.AsObject().CollectionStyle);

            // Unchanged, the node writes its element.
            Assert.Equal("a: [1, 2]\nb:\n- 3\nc: {x: 1}\nd:\n  u: 2\ne: 1\nf: [{p: q}, r]\n", node.ToYamlString(s_block));

            // Edited, each collection keeps the style it was read with.
            node["a"]!.AsArray().Add(9);
            node["c"]!.AsObject()["z"] = "w";
            node["b"]!.AsArray().Add(4);
            Assert.Equal(YamlCollectionStyle.Flow, node["a"]!.AsArray().CollectionStyle);
            Assert.Equal("a: [1, 2, 9]\nb:\n- 3\n- 4\nc: {x: 1, z: w}\nd:\n  u: 2\ne: 1\nf: [{p: q}, r]\n", node.ToYamlString(s_block));

            // Cleared, too.
            YamlArray cleared = YamlNode.Parse("[1, 2]")!.AsArray();
            cleared.Clear();
            cleared.Add("x");
            Assert.Equal(YamlCollectionStyle.Flow, cleared.CollectionStyle);
            Assert.Equal("[x]\n", cleared.ToYamlString(s_block));
            YamlObject clearedObject = YamlNode.Parse("{a: 1}")!.AsObject();
            clearedObject.Clear();
            clearedObject["b"] = 2;
            Assert.Equal("{b: 2}\n", clearedObject.ToYamlString(s_block));
        }

        [Fact]
        public void Item1_Node_StyleIsSettable()
        {
            var created = new YamlObject { ["k"] = 1, ["list"] = new YamlArray(1, 2) };
            Assert.Equal(YamlCollectionStyle.Block, created.CollectionStyle);
            Assert.Equal("k: 1\nlist:\n- 1\n- 2\n", created.ToYamlString(s_block));
            created["list"]!.AsArray().CollectionStyle = YamlCollectionStyle.Flow;
            Assert.Equal("k: 1\nlist: [1, 2]\n", created.ToYamlString(s_block));
            created.CollectionStyle = YamlCollectionStyle.Flow;
            Assert.Equal("{k: 1, list: [1, 2]}\n", created.ToYamlString(s_block));

            // Inside a flow collection every collection is flow: Block on a child changes nothing there.
            created["list"]!.AsArray().CollectionStyle = YamlCollectionStyle.Block;
            Assert.Equal("{k: 1, list: [1, 2]}\n", created.ToYamlString(s_block));

            // Set on a parsed node without editing it, the style wins over the source's.
            YamlArray parsed = YamlNode.Parse("[1, 2]")!.AsArray();
            parsed.CollectionStyle = YamlCollectionStyle.Block;
            Assert.Equal("- 1\n- 2\n", parsed.ToYamlString(s_block));
            YamlObject parsedObject = YamlNode.Parse("a:\n  b: 1\n")!.AsObject();
            parsedObject["a"]!.AsObject().CollectionStyle = YamlCollectionStyle.Flow;
            Assert.Equal("a: {b: 1}\n", parsedObject.ToYamlString(s_block));

            // The JSON-compatible writer ignores it.
            Assert.Equal("{\"k\":1,\"list\":[1,2]}", created.ToYamlString(s_json));

            Assert.Throws<ArgumentOutOfRangeException>(() => created.CollectionStyle = (YamlCollectionStyle)2);
            Assert.Throws<ArgumentOutOfRangeException>(() => parsed.CollectionStyle = (YamlCollectionStyle)255);
        }

        [Fact]
        public void Item1_DeepClone_KeepsTheStyle_DeepEquals_IgnoresIt()
        {
            YamlObject node = YamlNode.Parse("a: [1, 2]\nb:\n  c: d\n")!.AsObject();
            node["b"]!.AsObject().CollectionStyle = YamlCollectionStyle.Flow;
            YamlNode clone = node.DeepClone();
            Assert.Equal("a: [1, 2]\nb: {c: d}\n", clone.ToYamlString(s_block));
            Assert.Equal(YamlCollectionStyle.Flow, clone["a"]!.AsArray().CollectionStyle);

            // The style is layout, not content.
            Assert.True(YamlNode.DeepEquals(YamlNode.Parse("a: [1, 2]\n"), YamlNode.Parse("a:\n- 1\n- 2\n")));
            using YamlDocument flow = YamlDocument.Parse("a: [1, 2]\n");
            using YamlDocument block = YamlDocument.Parse("a:\n- 1\n- 2\n");
            Assert.True(YamlElement.DeepEquals(flow.RootElement, block.RootElement));
        }

        [Fact]
        public void Item1_Serializer_WritesANodeMembersStyle()
        {
            var holder = new NodeHolder { Node = YamlNode.Parse("{a: [1, 2]}") };
            string yaml = YamlSerializer.Serialize(holder, s_block);
            Assert.Equal("Node: {a: [1, 2]}\n", yaml);
            Assert.True(YamlNode.DeepEquals(holder.Node, YamlSerializer.Deserialize<NodeHolder>(yaml, s_block)!.Node));
        }

        public sealed class NodeHolder
        {
            public YamlNode? Node { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 2: a multi-line single-quoted string

        [Theory]
        [InlineData("a\nb", "k: 'a\n\n  b'\n")]
        [InlineData("a\n\nb", "k: 'a\n\n\n  b'\n")]
        [InlineData("a\nb\n", "k: 'a\n\n  b\n\n  '\n")]
        [InlineData("\na", "k: '\n\n  a'\n")]
        [InlineData("it's\nhere", "k: 'it''s\n\n  here'\n")]
        [InlineData("a b\nc  d", "k: 'a b\n\n  c  d'\n")]
        // Not in single quotes: a tab, a space next to a line break, a carriage return.
        [InlineData("a\tb\nc", "k: \"a\\tb\\nc\"\n")]
        [InlineData("a \nb", "k: \"a \\nb\"\n")]
        [InlineData("a\n b", "k: \"a\\n b\"\n")]
        [InlineData("a\r\nb", "k: \"a\\r\\nb\"\n")]
        public void Item2_SingleQuotedHint_OverSeveralLines(string text, string expected)
        {
            string written = Write(w =>
            {
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("k", text);
                w.WriteEndObject();
            });
            Assert.Equal(expected, written);
            using YamlDocument back = YamlDocument.Parse(written);
            Assert.Equal(text, back.RootElement.GetProperty("k").GetString());
        }

        [Theory]
        [InlineData(3, false)]
        [InlineData(4, false)]
        [InlineData(2, true)]
        [InlineData(4, true)]
        public void Item2_SingleQuoted_ReadsBack_InEveryLayout(int indentSize, bool indentSequences)
        {
            var options = new YamlSerializerOptions(s_block) { IndentSize = indentSize, IndentSequences = indentSequences };
            var value = new Dictionary<string, List<Dictionary<string, string>>> { ["k"] = new() { new() { ["in"] = "a\n\nb\n" } } };
            var styled = new YamlSerializerOptions(options) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted };
            string yaml = YamlSerializer.Serialize(value, styled);
            Assert.Contains("'a\n", yaml);
            Assert.Equal("a\n\nb\n", YamlSerializer.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(yaml, options)!["k"][0]["in"]);
            Assert.Equal("a\n\nb\n", YamlSerializer.Deserialize<string>(YamlSerializer.Serialize("a\n\nb\n", styled), options));
        }

        [Fact]
        public void Item2_SingleQuoted_AtEveryPosition()
        {
            // At the root, in a sequence, nested, and inside a flow collection (double-quoted there).
            Assert.Equal("'a\n\n  b'\n", Write(w => { w.WriteScalarStyle(YamlScalarStyle.SingleQuoted); w.WriteStringValue("a\nb"); }));
            string nested = Write(w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("s");
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteStringValue("a\nb");
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("in", "c\nd");
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray("f");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteStringValue("e\nf");
                w.WriteEndArray();
                w.WriteEndObject();
            });
            Assert.Equal("s:\n- 'a\n\n  b'\n- in: 'c\n\n    d'\nf: [\"e\\nf\"]\n", nested);
            using YamlDocument back = YamlDocument.Parse(nested);
            Assert.Equal("a\nb", back.RootElement.GetProperty("s")[0].GetString());
            Assert.Equal("c\nd", back.RootElement.GetProperty("s")[1].GetProperty("in").GetString());
            Assert.Equal("e\nf", back.RootElement.GetProperty("f")[0].GetString());
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 3: DefaultScalarStyle

        public sealed class Sample
        {
            public string One { get; set; } = "hello";
            public string Multi { get; set; } = "a\nb";
            public string Spaced { get; set; } = "a \nb";
            public string Resolves { get; set; } = "true";
            public int Number { get; set; } = 3;
            public bool Flag { get; set; } = true;
            public string? Missing { get; set; }
            public Dictionary<string, string> Map { get; set; } = new() { ["key"] = "v" };
            public List<string> Items { get; set; } = new() { "i" };
        }

        public static IEnumerable<object[]> DefaultStyles() => new[]
        {
            new object[] { YamlScalarStyle.None, "One: hello\nMulti: |-\n  a\n  b\nSpaced: |-\n  a \n  b\nResolves: \"true\"\n" },
            new object[] { YamlScalarStyle.Plain, "One: hello\nMulti: |-\n  a\n  b\nSpaced: |-\n  a \n  b\nResolves: \"true\"\n" },
            new object[] { YamlScalarStyle.SingleQuoted, "One: 'hello'\nMulti: 'a\n\n  b'\nSpaced: |-\n  a \n  b\nResolves: 'true'\n" },
            new object[] { YamlScalarStyle.DoubleQuoted, "One: \"hello\"\nMulti: \"a\\nb\"\nSpaced: \"a \\nb\"\nResolves: \"true\"\n" },
            new object[] { YamlScalarStyle.Literal, "One: hello\nMulti: |-\n  a\n  b\nSpaced: |-\n  a \n  b\nResolves: \"true\"\n" },
            new object[] { YamlScalarStyle.Folded, "One: hello\nMulti: >-\n  a\n\n  b\nSpaced: >-\n  a \n\n  b\nResolves: \"true\"\n" },
        };

        [Theory]
        [MemberData(nameof(DefaultStyles))]
        public void Item3_EveryStyle_WhereItCanBeWritten(YamlScalarStyle style, string strings)
        {
            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = style };
            string yaml = YamlSerializer.Serialize(new Sample(), options);
            string quoted = style switch { YamlScalarStyle.SingleQuoted => "'v'", YamlScalarStyle.DoubleQuoted => "\"v\"", _ => "v" };
            string item = style switch { YamlScalarStyle.SingleQuoted => "'i'", YamlScalarStyle.DoubleQuoted => "\"i\"", _ => "i" };
            string key = style switch { YamlScalarStyle.SingleQuoted => "'key'", YamlScalarStyle.DoubleQuoted => "\"key\"", _ => "key" };

            // Member names, numbers, booleans and nulls are not styled; dictionary keys and values and sequence items are
            // (fix A: a dictionary's keys, as YamlDotNet's).
            Assert.Equal(strings + $"Number: 3\nFlag: true\nMissing: null\nMap:\n  {key}: {quoted}\nItems:\n- {item}\n", yaml);

            Sample back = YamlSerializer.Deserialize<Sample>(yaml, options)!;
            Assert.Equal(new Sample().Spaced, back.Spaced);
            Assert.Equal(new Sample().Multi, back.Multi);
            Assert.Equal("true", back.Resolves);
            Assert.Equal("v", back.Map["key"]);
        }

        public sealed class Typed
        {
            public DateTime When { get; set; } = new DateTime(2020, 1, 2);
            public DateTimeOffset WhenOffset { get; set; } = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));
#if NET
            public DateOnly Day { get; set; } = new DateOnly(2020, 1, 2);
            public TimeOnly Time { get; set; } = new TimeOnly(3, 4);
#endif
            public TimeSpan Span { get; set; } = TimeSpan.FromMinutes(90);
            public Guid Id { get; set; } = new Guid("01234567-89ab-cdef-0123-456789abcdef");
            public Uri Link { get; set; } = new Uri("http://x.test/a");
            public Version Release { get; set; } = new Version(1, 2);
            public char Letter { get; set; } = 'c';
            public DayOfWeek Day2 { get; set; } = DayOfWeek.Monday;
            public byte[] Bytes { get; set; } = new byte[] { 1, 2 };
            public object Boxed { get; set; } = new DateTime(2021, 1, 1);
            public int Number { get; set; } = 3;
            public bool Flag { get; set; } = true;
            public string? Missing { get; set; }
        }

        [Fact]
        public void Item3_TypedTextValuesAndKeys()
        {
            // Fix A: every value written as a YAML string takes a quoted default; numbers, booleans and nulls do not.
            var single = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted, Converters = { new YamlStringEnumConverter() } };
            string yaml = YamlSerializer.Serialize(new Typed(), single);
#if NET
            const string DayAndTime = "Day: '2020-01-02'\nTime: '03:04:00'\n";
#else
            const string DayAndTime = "";
#endif
            Assert.Equal(
                "When: '2020-01-02T00:00:00'\nWhenOffset: '2020-01-02T03:04:05+01:00'\n" + DayAndTime + "Span: '01:30:00'\n" +
                "Id: '01234567-89ab-cdef-0123-456789abcdef'\nLink: 'http://x.test/a'\nRelease: '1.2'\nLetter: 'c'\nDay2: 'Monday'\nBytes: 'AQI='\n" +
                "Boxed: '2021-01-01T00:00:00'\nNumber: 3\nFlag: true\nMissing: null\n",
                yaml);
            Typed back = YamlSerializer.Deserialize<Typed>(yaml, single)!;
            Assert.Equal(new Typed().WhenOffset, back.WhenOffset);
            Assert.Equal(new Typed().Id, back.Id);
            Assert.Equal(new byte[] { 1, 2 }, back.Bytes);
            Assert.Equal(DayOfWeek.Monday, back.Day2);

            // A plain, literal or folded default leaves a typed value in its typed form.
            string plain = YamlSerializer.Serialize(new Typed(), new YamlSerializerOptions(s_block) { Converters = { new YamlStringEnumConverter() } });
            foreach (YamlScalarStyle style in new[] { YamlScalarStyle.Plain, YamlScalarStyle.Literal, YamlScalarStyle.Folded })
            {
                Assert.Equal(plain, YamlSerializer.Serialize(new Typed(), new YamlSerializerOptions(s_block) { DefaultScalarStyle = style, Converters = { new YamlStringEnumConverter() } }));
            }

            // Keys: a dictionary's text keys take it, as YamlDotNet's do; number and boolean keys and member names do not.
            var keys = new Dictionary<object, object> { ["k"] = "v", [1] = 2, [true] = "b", [new DateTime(2020, 1, 2)] = 3, ['c'] = 4 };
            Assert.Equal("\"k\": \"v\"\n1: 2\ntrue: \"b\"\n\"2020-01-02T00:00:00\": 3\n\"c\": 4\n",
                YamlSerializer.Serialize(keys, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));
            // A collection key's scalars are values inside the key, and take it as values do.
            Assert.Equal("? - 'a'\n: 'v'\n", YamlSerializer.Serialize(new Dictionary<List<string>, string> { [new() { "a" }] = "v" }, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted }));
            // A literal or folded default cannot style a key (nor a one-line value), which is written as the writer chooses.
            Assert.Equal("k: v\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "v" }, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.Literal }));

            // A member's or a converter's hint goes first; the DOM's source style too.
            using YamlDocument document = YamlDocument.Parse("k: 2020-01-02\n");
            Assert.Equal("k: 2020-01-02\n", WriteBack(document.RootElement, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));
        }

        [Fact]
        public void Item3_Options()
        {
            Assert.Equal(YamlScalarStyle.None, new YamlSerializerOptions().DefaultScalarStyle);
            Assert.Equal(YamlScalarStyle.None, new YamlWriterOptions().DefaultScalarStyle);
            var writerOptions = new YamlWriterOptions { DefaultScalarStyle = YamlScalarStyle.Folded, MultiLineStringStyle = YamlScalarStyle.Literal, QuoteForLenientReaders = true };
            Assert.Equal(YamlScalarStyle.Folded, writerOptions.DefaultScalarStyle);
            Assert.Equal(YamlScalarStyle.Literal, writerOptions.MultiLineStringStyle);
            Assert.True(writerOptions.QuoteForLenientReaders);
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlWriterOptions { DefaultScalarStyle = (YamlScalarStyle)6 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { DefaultScalarStyle = (YamlScalarStyle)6 });

            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal(YamlScalarStyle.DoubleQuoted, new YamlSerializerOptions(options).DefaultScalarStyle);
            Assert.Equal(YamlScalarStyle.DoubleQuoted, options.GetWriterOptions().DefaultScalarStyle);
            YamlSerializer.Serialize("x", options);
            Assert.Throws<InvalidOperationException>(() => options.DefaultScalarStyle = YamlScalarStyle.Plain);

            // Ignored in JSON-compatible style.
            Assert.Equal("{\"One\":\"hello\"}", YamlSerializer.Serialize(new Dictionary<string, string> { ["One"] = "hello" }, new YamlSerializerOptions(s_json) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted }));
        }

        [Fact]
        public void Item3_TheWriterAndTheDom()
        {
            var writer = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal("k: \"v\"\nnum: 1\nq: \"w\"\n", Write(w =>
            {
                w.WriteStartObject();
                w.WriteString("k", "v");
                w.WriteNumber("num", 1);
                w.WritePropertyName("q");
                w.WriteStringValue("w".AsSpan());
                w.WriteEndObject();
            }, writer));

            // A node with no source style takes the default; a parsed one keeps its source's (see the precedence).
            var created = new YamlObject { ["k"] = "v", ["num"] = 1 };
            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted };
            Assert.Equal("'k': 'v'\n'num': 1\n", created.ToYamlString(options));
        }

        // The precedence (M7b's ordered list), highest first: (1) a converter's WriteScalarStyle, (2) a member's
        // [YamlScalarStyle], (3) a kept source style, (4) MultiLineStringStyle, set, for a string with a line break, (5)
        // DefaultScalarStyle, (6) the writer's default. One test per pair. A style that cannot hold the text gives way to
        // the writer's own choice for it, not to the next entry (Item3_AStyleThatCannotHoldTheText).

        private sealed class DoubleQuotingConverter : YamlConverter<string>
        {
            public override string? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetString();

            public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options)
            {
                writer.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                writer.WriteStringValue(value);
            }
        }

        public sealed class ConverterAndMember
        {
            [YamlConverter(typeof(DoubleQuotingConverter))]
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
            public string Both { get; set; } = "x";

            [YamlConverter(typeof(DoubleQuotingConverter))]
            public string Converter { get; set; } = "a\nb";

            [YamlScalarStyle(YamlScalarStyle.Plain)]
            public string Member { get; set; } = "m";

            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
            public string MemberMultiLine { get; set; } = "a\nb";

            [YamlScalarStyle(YamlScalarStyle.DoubleQuoted)]
            public YamlNode? MemberOverSource { get; set; } = YamlNode.Parse("'s'");

            public string Plain { get; set; } = "p";
        }

        [Fact]
        public void Item3_Precedence_ConverterAndMember()
        {
            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted, MultiLineStringStyle = YamlScalarStyle.Folded };
            string yaml = YamlSerializer.Serialize(new ConverterAndMember(), options);
            Assert.Equal(
                // 1 over 2.
                "Both: \"x\"\n" +
                // 1 over 4 and 5.
                "Converter: \"a\\nb\"\n" +
                // 2 over 5.
                "Member: m\n" +
                // 2 over 4.
                "MemberMultiLine: 'a\n\n  b'\n" +
                // 2 over 3.
                "MemberOverSource: \"s\"\n" +
                // 5 over 6.
                "Plain: 'p'\n",
                yaml);
            ConverterAndMember back = YamlSerializer.Deserialize<ConverterAndMember>(yaml, options)!;
            Assert.Equal("a\nb", back.Converter);
            Assert.Equal("a\nb", back.MemberMultiLine);
        }

        [Fact]
        public void Item3_Precedence_WriterHintAndSourceStyle()
        {
            // 1 over 3: a pending hint is not replaced by the DOM's source style.
            using YamlDocument single = YamlDocument.Parse("'x'");
            Assert.Equal("\"x\"\n", Write(w => { w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted); single.RootElement.WriteTo(w); }));

            // 1 over 5, and 1 over 4.
            var writer = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", DefaultScalarStyle = YamlScalarStyle.DoubleQuoted, MultiLineStringStyle = YamlScalarStyle.Literal };
            Assert.Equal("k: v\nm: 'a\n\n  b'\n", Write(w =>
            {
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.Plain);
                w.WriteString("k", "v");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("m", "a\nb");
                w.WriteEndObject();
            }, writer));
        }

        [Fact]
        public void Item3_Precedence_SourceStyle()
        {
            using YamlDocument document = YamlDocument.Parse("plain: y\nsingle: 'x'\nml: 'a\n\n  b'\nlit: |-\n  c\n  d\n");

            // 3 over 5.
            var defaultOnly = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal("plain: y\nsingle: 'x'\nml: 'a\n\n  b'\nlit: |-\n  c\n  d\n", WriteBack(document.RootElement, defaultOnly));

            // 3 over 4.
            var multiLine = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MultiLineStringStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal("plain: y\nsingle: 'x'\nml: 'a\n\n  b'\nlit: |-\n  c\n  d\n", WriteBack(document.RootElement, multiLine));

            // The node path keeps the same styles.
            YamlNode node = YamlNode.Parse("plain: y\nsingle: 'x'\n")!;
            Assert.Equal("plain: y\nsingle: 'x'\n", node.ToYamlString(new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));
        }

        [Fact]
        public void Item3_Precedence_MultiLineStringStyleAndDefault()
        {
            // 4 over 5 for a string with a line break; 5 for one without.
            var options = new YamlSerializerOptions(s_block) { MultiLineStringStyle = YamlScalarStyle.DoubleQuoted, DefaultScalarStyle = YamlScalarStyle.Literal };
            Assert.Equal("a: \"x\\ny\"\nb: z\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["a"] = "x\ny", ["b"] = "z" }, options));
            options = new YamlSerializerOptions(s_block) { MultiLineStringStyle = YamlScalarStyle.Folded, DefaultScalarStyle = YamlScalarStyle.SingleQuoted };
            Assert.Equal("'a': >-\n  x\n\n  y\n'b': 'z'\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["a"] = "x\ny", ["b"] = "z" }, options));

            // 4 over 6, and 5 over 6 (the writer's default would be literal and plain).
            options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            Assert.Equal("\"a\": \"x\\ny\"\n\"b\": \"z\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["a"] = "x\ny", ["b"] = "z" }, options));
        }

        public sealed class LiteralOneLine
        {
            [YamlScalarStyle(YamlScalarStyle.Literal)]
            public string K { get; set; } = "x";
        }

        [Fact]
        public void Item3_AStyleThatCannotHoldTheText()
        {
            var doubleQuoted = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };

            // A member's Literal on one line: written as the writer writes one line, not in the default's style.
            Assert.Equal("K: x\n", YamlSerializer.Serialize(new LiteralOneLine(), doubleQuoted));

            // A kept |- on one line: the same.
            using YamlDocument document = YamlDocument.Parse("k: |-\n  x\n");
            Assert.Equal("k: x\n", WriteBack(document.RootElement, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", DefaultScalarStyle = YamlScalarStyle.DoubleQuoted }));

            // A MultiLineStringStyle that is set and cannot hold the text (a leading space at the root): double-quoted,
            // not single-quoted by the default.
            Assert.Equal("\" a\\nb\"\n", YamlSerializer.Serialize(" a\nb", new YamlSerializerOptions(s_block) { MultiLineStringStyle = YamlScalarStyle.Literal, DefaultScalarStyle = YamlScalarStyle.SingleQuoted }));

            // The default itself: a plain or single-quoted default that cannot hold a line break leaves it to the
            // writer's literal; a literal default on one line leaves it to plain.
            Assert.Equal("'k': |-\n  a \n  b\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a \nb" }, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.SingleQuoted }));
            Assert.Equal("k: x\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "x" }, new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.Literal }));
        }

        [Fact]
        public void Item3_GeneratedContext_HonoursTheOption()
        {
            var options = new YamlSerializerOptions(s_block) { DefaultScalarStyle = YamlScalarStyle.DoubleQuoted };
            var context = new ParityFollowUpsContext(options);
            var reflection = new YamlSerializerOptions(options) { TypeInfoResolver = new Serialization.Metadata.DefaultYamlTypeInfoResolver() };
            Assert.Equal(YamlSerializer.Serialize(new Sample(), reflection), YamlSerializer.Serialize(new Sample(), context.Sample));
            Assert.StartsWith("One: \"hello\"\n", YamlSerializer.Serialize(new Sample(), context.Sample));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 4: CollectionKeys

        private const string Keyed = "a: 1\n[b, c]: 2\nd: 3\n? {x: 1}\n: 4\n";

        [Fact]
        public void Item4_Parsed_OrderCountEnumeration()
        {
            YamlObject node = YamlNode.Parse(Keyed)!.AsObject();
            IReadOnlyList<KeyValuePair<YamlNode, YamlNode?>> keys = node.CollectionKeys;
            Assert.Equal(2, keys.Count);
            Assert.Equal("[b, c]\n", keys[0].Key.ToYamlString(s_block));
            Assert.Equal(2, (int)keys[0].Value!);
            Assert.IsType<YamlObject>(keys[1].Key);
            Assert.Equal(4, (int)keys[1].Value!);
            Assert.Same(node, keys[0].Key.Parent);
            Assert.Same(node, keys[0].Value!.Parent);

            // The string-keyed view sees the string keys only.
            Assert.Equal(2, node.Count);
            Assert.Equal(new[] { "a", "d" }, node.Select(p => p.Key));
            Assert.False(node.ContainsKey("[b, c]"));

            // Written back in source order.
            Assert.Equal("a: 1\n? [b, c]\n: 2\nd: 3\n? {x: 1}\n: 4\n", node.ToYamlString(s_block));
            Assert.Empty(YamlNode.Parse("a: 1\n")!.AsObject().CollectionKeys);
        }

        [Fact]
        public void Item4_AddAndRemove()
        {
            YamlObject node = YamlNode.Parse("a: 1\n")!.AsObject();
            var key = new YamlArray("x", "w");
            node.AddCollectionKey(key, 5);
            Assert.Same(node, key.Parent);
            node["z"] = 6;
            Assert.Equal("a: 1\n? - x\n  - w\n: 5\nz: 6\n", node.ToYamlString(s_block));
            key.CollectionStyle = YamlCollectionStyle.Flow;
            Assert.Equal("a: 1\n? [x, w]\n: 5\nz: 6\n", node.ToYamlString(s_block));
            using (YamlDocument back = YamlDocument.Parse(node.ToYamlString(s_block)))
            {
                Assert.Equal(3, back.RootElement.GetPropertyCount());
            }

            // A null value, an object key.
            node.AddCollectionKey(new YamlObject { ["m"] = "o" }, null);
            Assert.Equal(2, node.CollectionKeys.Count);
            Assert.Equal("a: 1\n? [x, w]\n: 5\nz: 6\n? m: o\n: null\n", node.ToYamlString(s_block));

            // Errors: a null key, a scalar key, a deeply equal key, a key or value with a parent.
            Assert.Throws<ArgumentNullException>(() => node.AddCollectionKey(null!, 1));
            Assert.Throws<ArgumentException>(() => node.AddCollectionKey(YamlValue.Create("s"), 1));
            Assert.Throws<ArgumentException>(() => node.AddCollectionKey(new YamlArray("x", "w"), 1));
            var other = new YamlObject { ["l"] = new YamlArray("q") };
            Assert.Throws<InvalidOperationException>(() => node.AddCollectionKey(other["l"]!, 1));
            YamlNode owned = node["a"]!;
            Assert.Throws<InvalidOperationException>(() => node.AddCollectionKey(new YamlArray("q"), owned));
            Assert.Equal(2, node.CollectionKeys.Count);

            // Removing finds the key by DeepEquals, and detaches it.
            Assert.True(node.RemoveCollectionKey(new YamlArray("x", "w")));
            Assert.Null(key.Parent);
            Assert.False(node.RemoveCollectionKey(new YamlArray("x", "w")));
            Assert.Throws<ArgumentNullException>(() => node.RemoveCollectionKey(null!));
            Assert.Equal("a: 1\nz: 6\n? m: o\n: null\n", node.ToYamlString(s_block));
        }

        [Fact]
        public void Item4_DeepEqualsCloneAndClear()
        {
            YamlNode left = YamlNode.Parse(Keyed)!;
            Assert.True(YamlNode.DeepEquals(left, YamlNode.Parse("d: 3\n? {x: 1}\n: 4\na: 1\n[b,c]: 2\n")));
            Assert.False(YamlNode.DeepEquals(left, YamlNode.Parse("a: 1\n[b, c]: 2\nd: 3\n")));
            Assert.False(YamlNode.DeepEquals(left, YamlNode.Parse("a: 1\n[b, c]: 9\nd: 3\n? {x: 1}\n: 4\n")));

            YamlObject clone = left.DeepClone().AsObject();
            Assert.True(YamlNode.DeepEquals(left, clone));
            Assert.Same(clone, clone.CollectionKeys[0].Key.Parent);
            Assert.Equal(left.ToYamlString(s_block), clone.ToYamlString(s_block));

            YamlObject cleared = YamlNode.Parse(Keyed)!.AsObject();
            YamlNode clearedKey = cleared.CollectionKeys[0].Key;
            cleared.Clear();
            Assert.Empty(cleared.CollectionKeys);
            Assert.Null(clearedKey.Parent);
            Assert.Equal("{}\n", cleared.ToYamlString(s_block));

            // The JSON-compatible writer has no collection keys.
            Assert.Throws<NotSupportedException>(() => left.ToYamlString(s_json));
        }

        [Fact]
        public void Item4_OrderHolds_AcrossStringKeyEdits()
        {
            // Each collection key stays between the same string entries.
            YamlObject removed = YamlNode.Parse("a: 1\n[b, c]: 2\nd: 3\n")!.AsObject();
            removed.Remove("a");
            Assert.Equal("? [b, c]\n: 2\nd: 3\n", removed.ToYamlString(s_block));
            YamlObject removedAt = YamlNode.Parse("a: 1\nx: 0\n[b, c]: 2\nd: 3\n")!.AsObject();
            removedAt.RemoveAt(0);
            Assert.Equal("x: 0\n? [b, c]\n: 2\nd: 3\n", removedAt.ToYamlString(s_block));
            YamlObject inserted = YamlNode.Parse("a: 1\n[b, c]: 2\nd: 3\n")!.AsObject();
            inserted.Insert(0, "z", 0);
            inserted.Insert(3, "e", 4);
            Assert.Equal("z: 0\na: 1\n? [b, c]\n: 2\nd: 3\ne: 4\n", inserted.ToYamlString(s_block));

            // An added key goes after every entry, and stays before a string key added next, an earlier one removed.
            YamlObject added = YamlNode.Parse("a: 1\nb: 2\n")!.AsObject();
            added.AddCollectionKey(new YamlArray("k"), 0);
            added.Remove("a");
            added["c"] = 3;
            Assert.Equal("b: 2\n? - k\n: 0\nc: 3\n", added.ToYamlString(s_block));

            // Removing the last string key leaves the collection keys in their order.
            YamlObject last = YamlNode.Parse("[a]: 1\nb: 2\n[c]: 3\n")!.AsObject();
            last.Remove("b");
            Assert.Equal("? [a]\n: 1\n? [c]\n: 3\n", last.ToYamlString(s_block));
        }

        [Fact]
        public void Item4_PathsAndReplaceWith()
        {
            YamlObject node = YamlNode.Parse("a: 1\n[b, [c]]: 2\n? {x: 1}\n: [v]\n")!.AsObject();
            YamlNode key = node.CollectionKeys[0].Key;

            // A collection key's value is at [?n] and its key at [?n:key], n its index in CollectionKeys.
            Assert.Same(node, key.Parent);
            Assert.Equal("$[?0:key]", key.GetPath());
            Assert.Equal("$[?0:key][1][0]", key[1]![0]!.GetPath());
            Assert.Equal("$[?0]", node.CollectionKeys[0].Value!.GetPath());
            Assert.Equal("$[?1][0]", node.CollectionKeys[1].Value![0]!.GetPath());
            Assert.Equal("$[?1:key].x", node.CollectionKeys[1].Key["x"]!.GetPath());
            Assert.Equal("$.a", node["a"]!.GetPath());
            Assert.Same(node, key[1]![0]!.Root);

            // ReplaceWith replaces in the entry: a node inside the key, the key, the value.
            key[0]!.ReplaceWith("z");
            Assert.Equal("a: 1\n? [z, [c]]\n: 2\n? {x: 1}\n: [v]\n", node.ToYamlString(s_block));
            key.ReplaceWith(new YamlArray("q"));
            Assert.Null(key.Parent);
            Assert.Same(node, node.CollectionKeys[0].Key.Parent);
            node.CollectionKeys[1].Value!.ReplaceWith(9);
            Assert.Equal("a: 1\n? - q\n: 2\n? {x: 1}\n: 9\n", node.ToYamlString(s_block));
            Assert.Equal(1, node.Count);

            // A key replaced by a scalar or by a deeply equal key is refused, as AddCollectionKey refuses it.
            Assert.Throws<ArgumentException>(() => node.CollectionKeys[0].Key.ReplaceWith("s"));
            Assert.Throws<ArgumentException>(() => node.CollectionKeys[0].Key.ReplaceWith(new YamlObject { ["x"] = 1 }));
            Assert.Equal(2, node.CollectionKeys.Count);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Item 5: a key converter that writes a collection key

        public readonly record struct Pair(int A, int B);

        private sealed class PairConverter : YamlConverter<Pair>
        {
            public override Pair Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                int[] values = YamlSerializer.Deserialize<int[]>(ref reader, options)!;
                return new Pair(values[0], values[1]);
            }

            public override void Write(Utf8YamlWriter writer, Pair value, YamlSerializerOptions options)
            {
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                writer.WriteStartArray();
                writer.WriteNumberValue(value.A);
                writer.WriteNumberValue(value.B);
                writer.WriteEndArray();
            }

            public override void WriteAsPropertyName(Utf8YamlWriter writer, Pair value, YamlSerializerOptions options)
            {
                // The key's node, between WriteStartPropertyKey and WriteEndPropertyKey (M5e); block style only.
                writer.WriteStartPropertyKey();
                Write(writer, value, options);
                writer.WriteEndPropertyKey();
            }
        }

        [Fact]
        public void Item5_KeyConverter_WritesACollectionKey()
        {
            var options = new YamlSerializerOptions(s_block) { Converters = { new PairConverter() } };
            var map = new Dictionary<Pair, string> { [new Pair(1, 2)] = "p", [new Pair(3, 4)] = "q" };
            string yaml = YamlSerializer.Serialize(map, options);
            Assert.Equal("? [1, 2]\n: p\n? [3, 4]\n: q\n", yaml);
            using YamlDocument back = YamlDocument.Parse(yaml);
            YamlProperty[] properties = back.RootElement.EnumerateObject().ToArray();
            Assert.Equal(new[] { YamlKeyKind.Complex, YamlKeyKind.Complex }, properties.Select(p => p.KeyKind));
            Assert.Equal(2, properties[0].GetKeyElement()[1].GetInt32());

            // The JSON-compatible writer refuses a collection key.
            YamlException refused = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(map, new YamlSerializerOptions(s_json) { Converters = { new PairConverter() } }));
            Assert.IsType<InvalidOperationException>(refused.InnerException);
        }
    }

    [YamlSerializable(typeof(ParityFollowUpsTests.Sample))]
    internal partial class ParityFollowUpsContext : YamlSerializerContext;
}
