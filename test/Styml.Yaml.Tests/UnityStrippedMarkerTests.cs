// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Unity's <c>stripped</c> marker (<c>--- !u!4 &amp;400000 stripped</c>) under AcceptUnityStrippedMarker, and a document
    /// root's properties on its <c>---</c> line, which the DOM writes back there.
    /// </summary>
    public class UnityStrippedMarkerTests
    {
        private const string Prefab =
            "%TAG !u! tag:unity3d.com,2011:\n--- !u!1001 &100100000\nPrefabInstance:\n  m_Modification: {}\n--- !u!4 &400000 stripped\nTransform:\n  m_PrefabInstance: {fileID: 100100000}\n";

        private static readonly YamlReaderOptions s_on = new() { AllowMultipleValues = true, InheritTagDirectives = true, AcceptUnityStrippedMarker = true };
        private static readonly YamlReaderOptions s_off = new() { AllowMultipleValues = true, InheritTagDirectives = true };

        private static string Tokens(string yaml, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tokens = new List<string>();
            while (reader.Read())
            {
                tokens.Add(reader.TokenType + (reader.IsUnityStripped ? "*" : "") + (reader.TokenType == YamlTokenType.String ? "(" + reader.GetString() + ")" : ""));
            }

            return string.Join(" ", tokens);
        }

        private static string Error(string yaml, YamlReaderOptions options)
        {
            try
            {
                return Tokens(yaml, options);
            }
            catch (YamlException e)
            {
                return "error: " + e.Message;
            }
        }

        [Fact]
        public void Off_TheMarkerIsTheRootsContent()
        {
            Assert.StartsWith("error: An implicit YAML mapping key must be on a single line", Error(Prefab, s_off));
            Assert.Equal("Null String(stripped)", Tokens("--- !t &1\n--- !t &2 stripped\n", s_off));
        }

        [Fact]
        public void On_TheRootTokenReportsIt()
        {
            Assert.Equal("StartObject PropertyName StartObject PropertyName StartObject EndObject EndObject EndObject "
                + "StartObject* PropertyName StartObject PropertyName StartObject PropertyName Number EndObject EndObject EndObject",
                Tokens(Prefab, s_on));

            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Prefab), s_on);
            reader.Read();
            reader.Skip();
            Assert.True(reader.Read());
            Assert.True(reader.IsUnityStripped);
            Assert.Equal("tag:unity3d.com,2011:4", reader.GetExpandedTag());
            Assert.Equal("400000", Encoding.UTF8.GetString(reader.Anchor.ToArray()));
        }

        [Theory]
        // Accepted: after properties with a tag, at the end of the '---' line (blanks and a comment may follow).
        [InlineData("--- !t &1 stripped\nA: 1\n", "StartObject* PropertyName Number EndObject")]
        [InlineData("--- !t stripped\nA: 1\n", "StartObject* PropertyName Number EndObject")]
        [InlineData("--- !t &1 stripped   # c\nA: 1\n", "StartObject* PropertyName Number EndObject")]
        [InlineData("--- !t &1 stripped", "Null*")]
        [InlineData("--- !t &1 stripped\n--- !t &2\nB: 1\n", "Null* StartObject PropertyName Number EndObject")]
        [InlineData("--- !t &1 stripped\n...\n", "Null*")]
        // Not the marker: read as without the option.
        [InlineData("--- !t &1 stripped x\n", "String(stripped x)")]
        [InlineData("a: !t stripped\n", "StartObject PropertyName String(stripped) EndObject")]
        [InlineData("- !t stripped\n", "StartArray String(stripped) EndArray")]
        [InlineData("--- !t &1 strippedx\n", "String(strippedx)")]
        public void TheExactRule(string yaml, string expected)
        {
            Assert.Equal(expected, Tokens(yaml, s_on));
        }

        [Theory]
        [InlineData("--- &1 stripped\nA: 1\n")]
        [InlineData("--- !t &1 Stripped\nA: 1\n")]
        [InlineData("--- !t &1 stripped#c\nA: 1\n")]
        [InlineData("---\n!t &1 stripped\nA: 1\n")]
        public void NotTheMarker_ReadsAsWithoutTheOption(string yaml)
        {
            Assert.Equal(Error(yaml, s_off), Error(yaml, s_on));
        }

        [Fact]
        public void EverySplitAndSegment_KeepsTheFlag()
        {
            byte[] input = Encoding.UTF8.GetBytes(Prefab);
            string expected = Tokens(Prefab, s_on);
            for (int split = 0; split <= input.Length; split++)
            {
                foreach (SequenceFactory.Sequence sequence in new[] { SequenceFactory.Split(input, split), SequenceFactory.Uniform(input, 1) })
                {
                    var reader = new Utf8YamlReader(sequence.Value, s_on);
                    var tokens = new List<string>();
                    while (reader.Read())
                    {
                        tokens.Add(reader.TokenType + (reader.IsUnityStripped ? "*" : "") + (reader.TokenType == YamlTokenType.String ? "(" + reader.GetString() + ")" : ""));
                    }

                    Assert.Equal(expected, string.Join(" ", tokens));
                }

                // A partial first block, resumed with the rest.
                var partial = new Utf8YamlReader(input.AsSpan(0, split), isFinalBlock: false, new YamlReaderState(s_on));
                var seen = new List<string>();
                while (partial.Read())
                {
                    seen.Add(partial.TokenType + (partial.IsUnityStripped ? "*" : ""));
                }

                var rest = new Utf8YamlReader(input.AsSpan((int)partial.BytesConsumed), isFinalBlock: true, partial.CurrentState);
                while (rest.Read())
                {
                    seen.Add(rest.TokenType + (rest.IsUnityStripped ? "*" : ""));
                }

                Assert.Equal(expected.Split(' ').Select(t => t.Split('(')[0]), seen);
            }
        }

        [Fact]
        public void TheElementAndTheNode_ReportIt()
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Prefab), s_on);
            var elements = new List<YamlElement>();
            while (reader.Read())
            {
                elements.Add(YamlElement.ParseValue(ref reader));
            }

            Assert.Equal(new[] { false, true }, elements.Select(e => e.IsUnityStripped));
            Assert.False(elements[1].GetProperty("Transform").IsUnityStripped);
            Assert.Equal("400000", elements[1].GetAnchor());

            using YamlDocument document = YamlDocument.Parse("%TAG !u! tag:unity3d.com,2011:\n--- !u!4 &4 stripped\nTransform: {}\n",
                new YamlDocumentOptions { AcceptUnityStrippedMarker = true });
            Assert.True(document.RootElement.IsUnityStripped);

            reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Prefab), s_on);
            var nodes = new List<YamlNode>();
            while (reader.Read())
            {
                nodes.Add(YamlNode.Parse(ref reader)!);
            }

            Assert.Equal(new[] { false, true }, nodes.Select(n => n.IsUnityStripped));
        }

        private static string Write(Action<Utf8YamlWriter> write, YamlWriterOptions? options = null)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options ?? new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                write(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static void Transform(Utf8YamlWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Transform");
            writer.WriteStartObject();
            writer.WriteNumber("m", 1);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        [Fact]
        public void TheWriter_PutsTheRootsPropertiesOnTheMarkerLine()
        {
            Assert.Equal("--- !u!4 &400000 stripped\nTransform:\n  m: 1\n", Write(w =>
            {
                w.WriteDocumentStart(rootPropertiesOnMarkerLine: true);
                w.WriteTag("!u!4");
                w.WriteAnchor("400000");
                w.WriteUnityStrippedMarker();
                Transform(w);
            }));

            Assert.Equal("--- &1 !t\nTransform:\n  m: 1\n", Write(w =>
            {
                w.WriteDocumentStart(rootPropertiesOnMarkerLine: true);
                w.WriteAnchor("1");
                w.WriteTag("!t");
                Transform(w);
            }));

            // Without it, as before; and after a flush the line break is out of reach, so the properties go on the next line.
            Assert.Equal("---\n!t &1\nTransform:\n  m: 1\n", Write(w => { w.WriteDocumentStart(); w.WriteTag("!t"); w.WriteAnchor("1"); Transform(w); }));
            Assert.Equal("---\n!t\nTransform:\n  m: 1\n", Write(w => { w.WriteDocumentStart(rootPropertiesOnMarkerLine: true); w.Flush(); w.WriteTag("!t"); Transform(w); }));

            // The marker needs a tag on the marker line.
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteDocumentStart(); w.WriteTag("!t"); w.WriteUnityStrippedMarker(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteDocumentStart(rootPropertiesOnMarkerLine: true); w.WriteAnchor("1"); w.WriteUnityStrippedMarker(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteUnityStrippedMarker(), new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }));
        }

        [Fact]
        public void TheDom_WritesThemBackOnTheMarkerLine()
        {
            const string source = "%TAG !u! tag:unity3d.com,2011:\n--- !u!4 &400000 stripped\nTransform:\n  m: 1\n";
            using YamlDocument document = YamlDocument.Parse(source, new YamlDocumentOptions { AcceptUnityStrippedMarker = true });
            YamlNode node = YamlNode.Parse(source, documentOptions: new YamlDocumentOptions { AcceptUnityStrippedMarker = true })!;
            const string expected = "--- &400000 !<tag:unity3d.com,2011:4> stripped\nTransform:\n  m: 1\n";
            Assert.Equal(expected, Write(w => { w.WriteDocumentStart(); document.RootElement.WriteTo(w); }));
            Assert.Equal(expected, Write(w => { w.WriteDocumentStart(); node.WriteTo(w); }));

            // An edited node writes its own properties, its tag as written.
            node["Transform"]!["m"] = 2;
            node.Anchor = "400000";
            Assert.Equal("--- &400000 !u!4 stripped\nTransform:\n  m: 2\n", Write(w => { w.WriteDocumentStart(); node.WriteTo(w); }));

            // Without a document start, the marker has no line to go on.
            Assert.Equal("&400000 !<tag:unity3d.com,2011:4>\nTransform:\n  m: 1\n", Write(w => document.RootElement.WriteTo(w)));

            // IgnoreSourceStyles writes the properties on the next line.
            Assert.Equal("---\n&400000 !<tag:unity3d.com,2011:4>\nTransform:\n  m: 1\n",
                Write(w => { w.WriteDocumentStart(); document.RootElement.WriteTo(w); }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", IgnoreSourceStyles = true }));

            // A node marked by the caller.
            var created = new YamlObject { ["Transform"] = new YamlObject() };
            created.Tag = "!u!4";
            created.IsUnityStripped = true;
            Assert.Equal("--- !u!4 stripped\nTransform: {}\n", Write(w => { w.WriteDocumentStart(); created.WriteTo(w); }));
        }

        [Fact]
        public void PropertiesOnTheMarkerLine_WithoutTheOption_AreWrittenBackThere()
        {
            const string source = "--- !t &1\nA: 1\n";
            using YamlDocument document = YamlDocument.Parse(source);
            // The element writes its anchor before its tag.
            Assert.Equal("--- &1 !t\nA: 1\n", Write(w => { w.WriteDocumentStart(); document.RootElement.WriteTo(w); }));
            using YamlDocument ownLine = YamlDocument.Parse("---\n!t &1\nA: 1\n");
            Assert.Equal("---\n&1 !t\nA: 1\n", Write(w => { w.WriteDocumentStart(); ownLine.RootElement.WriteTo(w); }));
        }
    }
}
