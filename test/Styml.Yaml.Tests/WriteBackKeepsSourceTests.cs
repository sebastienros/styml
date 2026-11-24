// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A document written back keeps what it read: the order of a node's properties, a tag's shorthand where the writer's
    /// own %TAG directives resolve it, plain text that YAML 1.1 reads as a boolean (where the output's schema reads it as
    /// the same string), and an empty value. IgnoreSourceStyles gives the canonical form; a created or edited node is
    /// written as before.
    /// </summary>
    public class WriteBackKeepsSourceTests
    {
        private static readonly YamlWriterOptions s_block = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlWriterOptions s_canonical = new() { Style = YamlWriterStyle.Block, NewLine = "\n", IgnoreSourceStyles = true };

        private static string Write(Action<Utf8YamlWriter> write, YamlWriterOptions? options = null)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options ?? s_block))
            {
                write(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static string Write(YamlElement element, YamlWriterOptions? options = null) => Write(element.WriteTo, options);

        private static string Write(YamlNode node, YamlWriterOptions? options = null) => Write(w => node.WriteTo(w), options);

        // Written, read and written again: the same bytes.
        private static void AssertFixedPoint(string written, YamlDocumentOptions options = default)
        {
            using YamlDocument again = YamlDocument.Parse(written, options);
            Assert.Equal(written, Write(again.RootElement));
        }

        // ------------------------------------------------------------------------------------------------------------
        // 1. The order of a node's properties

        private const string Ordered = "a: !t &x 1\nb: &y !t 2\nc: !t &z\n  d: !t &k e\nf: [!t &w 3]\n!t &key g: 4\n";

        [Fact]
        public void PropertyOrder_IsTheSources()
        {
            using YamlDocument document = YamlDocument.Parse(Ordered);
            Assert.Equal(Ordered, Write(document.RootElement));
            Assert.Equal(Ordered, Write(YamlNode.Parse(Ordered)!));
            Assert.Equal(Ordered, Write(document.RootElement.Clone()));
            AssertFixedPoint(Write(document.RootElement));

            // IgnoreSourceStyles: anchor first, as a created node.
            string canonical = "a: &x !t 1\nb: &y !t 2\nc: &z !t\n  d: &k !t e\nf:\n- &w !t 3\n&key !t g: 4\n";
            Assert.Equal(canonical, Write(document.RootElement, s_canonical));
            YamlNode created = YamlValue.Create(1)!;
            created.Tag = "!t";
            created.Anchor = "n";
            Assert.Equal("&n !t 1\n", Write(created));
        }

        [Fact]
        public void PropertyOrder_AnEditedSiblingKeepsTheOthers()
        {
            // (A YamlObject's keys are strings: an edited node does not keep a key's properties, as before.)
            const string values = "a: !t &x 1\nb: &y !t 2\nc: !t &z\n  d: !t &k e\nf: [!t &w 3]\n";
            YamlNode node = YamlNode.Parse(values)!;
            node["b"] = 20;
            Assert.Equal("a: !t &x 1\nb: 20\nc: !t &z\n  d: !t &k e\nf: [!t &w 3]\n", Write(node));

            // The node whose properties are set again is written anchor first.
            YamlNode edited = YamlNode.Parse(Ordered)!;
            edited["a"]!.Anchor = "x2";
            Assert.StartsWith("a: &x2 !t 1\n", Write(edited));
        }

        // ------------------------------------------------------------------------------------------------------------
        // 2. A tag's shorthand

        private const string Header = "%TAG !e! tag:e.com,2000:\n";

        [Fact]
        public void TagShorthand_WhereTheWritersDirectivesResolveIt()
        {
            using YamlDocument document = YamlDocument.Parse(Header + "--- !e!x\na: !e!y 1\n");
            Assert.Equal("%TAG !e! tag:e.com,2000:\n--- !e!x\na: !e!y 1\n", Write(w =>
            {
                w.WriteDirective("%TAG !e! tag:e.com,2000:");
                w.WriteDocumentStart();
                document.RootElement.WriteTo(w);
            }));

            // Another prefix for the handle, or no directive: the stand-alone form.
            Assert.Equal("%TAG !e! tag:other,1:\n--- !<tag:e.com,2000:x>\na: !<tag:e.com,2000:y> 1\n", Write(w =>
            {
                w.WriteDirective("%TAG !e! tag:other,1:");
                w.WriteDocumentStart();
                document.RootElement.WriteTo(w);
            }));
            Assert.Equal("!<tag:e.com,2000:x>\na: !<tag:e.com,2000:y> 1\n", Write(document.RootElement));

            // IgnoreSourceStyles: the stand-alone form, on the line after the marker.
            Assert.Equal("%TAG !e! tag:e.com,2000:\n---\n!<tag:e.com,2000:x>\na: !<tag:e.com,2000:y> 1\n", Write(w =>
            {
                w.WriteDirective("%TAG !e! tag:e.com,2000:");
                w.WriteDocumentStart();
                document.RootElement.WriteTo(w);
            }, s_canonical));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TagShorthand_FollowsTheReadersScope(bool inherit)
        {
            const string source = Header + "--- !e!x 1\n--- !e!y 2\n";
            var readerOptions = new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = true };
            var elements = new List<YamlElement>();
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(source), readerOptions);
            while (reader.Read())
            {
                elements.Add(YamlElement.ParseValue(ref reader));
            }

            string written = Write(w =>
            {
                w.WriteDirective("%TAG !e! tag:e.com,2000:");
                foreach (YamlElement element in elements)
                {
                    w.WriteDocumentStart();
                    element.WriteTo(w);
                }
            }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", InheritTagDirectives = inherit });

            // A later document has the handle only under the option; without it, the stand-alone form.
            Assert.Equal(inherit ? source : Header + "--- !e!x 1\n--- !<tag:e.com,2000:y> 2\n", written);

            // The output reads back to the same expanded tags, under the option it was written with.
            var back = new Utf8YamlReader(Encoding.UTF8.GetBytes(written), new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = inherit });
            var tags = new List<string?>();
            while (back.Read())
            {
                tags.Add(back.GetExpandedTag());
            }

            Assert.Equal(new[] { "tag:e.com,2000:x", "tag:e.com,2000:y" }, tags);
        }

        [Fact]
        public void TheWritersTable_IsBoundedAndFedByTheSerializer()
        {
            Assert.Throws<InvalidOperationException>(() => Write(w =>
            {
                for (int i = 0; i < 257; i++)
                {
                    w.WriteDirective("%TAG !h" + i + "! tag:h" + i + ",1:");
                }
            }));

            Assert.True(new YamlSerializerOptions { InheritTagDirectives = true }.GetWriterOptions().InheritTagDirectives);
            Assert.False(new YamlSerializerOptions().GetWriterOptions().InheritTagDirectives);
        }

        // ------------------------------------------------------------------------------------------------------------
        // 3. Plain text that YAML 1.1 reads as a boolean

        private const string Booleanish = "y: on\nn: off\nv: {x: 0, y: 1, n: [y, n, yes, no]}\nlist:\n- y\n- On\n";

        [Fact]
        public void PlainBooleanText_StaysPlainWhereTheOutputReadsItTheSame()
        {
            using YamlDocument document = YamlDocument.Parse(Booleanish);
            Assert.Equal(Booleanish, Write(document.RootElement));
            Assert.Equal(Booleanish, Write(YamlNode.Parse(Booleanish)!));
            AssertFixedPoint(Write(document.RootElement));

            // Under YAML 1.1 these are booleans: quoted.
            string yaml11 = YamlSerializer.Serialize(document, new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 });
            Assert.Equal("\"y\": \"on\"\n\"n\": \"off\"\nv: {x: 0, \"y\": 1, \"n\": [\"y\", \"n\", \"yes\", \"no\"]}\nlist:\n- \"y\"\n- \"On\"\n", yaml11);

            // IgnoreSourceStyles, created values and typed graphs: quoted, as before.
            Assert.Equal("\"y\": \"on\"\n", Write(YamlDocument.Parse("y: on\n").RootElement, s_canonical));
            Assert.Equal("\"y\": \"on\"\n", Write(new YamlObject { ["y"] = "on" }));
            Assert.Equal("\"y\": \"on\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["y"] = "on" }));
        }

        // ------------------------------------------------------------------------------------------------------------
        // 4. An empty value

        private const string Empties = "a: \nb:\nc:\n- \n-\n- x\nd: &x\ne: {f: , g: }\nh:\n  i: \n";

        [Fact]
        public void AnEmptyValue_IsWrittenBackEmpty()
        {
            using YamlDocument document = YamlDocument.Parse(Empties);
            Assert.Equal(Empties, Write(document.RootElement));
            Assert.Equal(Empties, Write(YamlNode.Parse(Empties)!));
            Assert.Equal(Empties, Write(document.RootElement.Clone()));
            AssertFixedPoint(Write(document.RootElement));
            Assert.Equal(YamlValueKind.Null, document.RootElement.GetProperty("b").ValueKind);

            // IgnoreSourceStyles, a created null and a typed null: null.
            Assert.Equal("a: null\nb: null\nc:\n- null\n- null\n- x\nd: &x null\ne:\n  f: null\n  g: null\nh:\n  i: null\n", Write(document.RootElement, s_canonical));
            Assert.Equal("a: null\n", Write(new YamlObject { ["a"] = null }));
            Assert.Equal("a: null\n", YamlSerializer.Serialize(new Dictionary<string, string?> { ["a"] = null }));

            // An empty root is written null: written empty, there would be no document to read.
            Assert.Equal("null\n", Write(YamlDocument.Parse("--- \n").RootElement));

            // An edited sibling: the members that stay elements keep their empty values. A null member of an edited
            // YamlObject is a null reference, as in JsonNode, so it is written null and its anchor is not kept (as before).
            YamlNode node = YamlNode.Parse(Empties)!;
            node["a"] = "set";
            Assert.Equal("a: set\nb: null\nc:\n- \n-\n- x\nd: null\ne: {f: , g: }\nh:\n  i: \n", Write(node));
        }

        [Fact]
        public void AnEmptyValueWithoutABlank_InAFlowMapping()
        {
            using YamlDocument document = YamlDocument.Parse("e: {f:, g:}\n");
            string written = Write(document.RootElement);
            Assert.Equal("e: {f:, g:}\n", written);
            AssertFixedPoint(written);
        }
    }
}
