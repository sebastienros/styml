// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// <see cref="YamlReaderOptions.ApplyYaml13DraftRestrictions"/>: the six forms it refuses, what stays valid next
    /// to each, and that the default reads all of them. The yaml-test-suite's verdicts are in YamlTestSuiteDraft13Tests.
    /// </summary>
    public class Yaml13DraftRestrictionsTests
    {
        private static readonly YamlReaderOptions s_strict = new() { AllowMultipleValues = true, ApplyYaml13DraftRestrictions = true };
        private static readonly YamlReaderOptions s_default = new() { AllowMultipleValues = true };

        private static List<string> Events(string yaml, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            return YamlEventTrace.Read(ref reader);
        }

        private static string Error(string yaml)
        {
            Events(yaml, s_default);
            return Assert.ThrowsAny<YamlException>(() => Events(yaml, s_strict)).Message;
        }

        private static void Accepted(string yaml) => Assert.Equal(Events(yaml, s_default), Events(yaml, s_strict));

        [Fact]
        public void OffByDefault()
        {
            Assert.False(default(YamlReaderOptions).ApplyYaml13DraftRestrictions);
        }

        [Theory]
        [InlineData("text\n")]
        [InlineData("42\n")]
        [InlineData("'quoted'\n")]
        [InlineData("\"quoted\"\n")]
        [InlineData("|\n text\n")]
        [InlineData(">\n text\n")]
        [InlineData("! a\n")]
        [InlineData("&a text\n")]
        [InlineData("a\nb\n")]
        [InlineData("--- [a]\n...\nbare\n")]
        [InlineData("# comment\ntext\n")]
        public void RootScalar_NeedsADocumentStart(string yaml)
        {
            Assert.Contains("needs a '---' line", Error(yaml));
        }

        [Theory]
        [InlineData("---\ntext\n")]
        [InlineData("---\n'quoted'\n")]
        [InlineData("--- |\n text\n")]
        [InlineData("---\n|\n text\n")]
        [InlineData("--- !!str\ntext\n")]
        [InlineData("--- &a\ntext\n")]
        [InlineData("---\na\nb\n")]
        [InlineData("---\n")]
        [InlineData("--- # nothing\n...\n")]
        [InlineData("key: text\n")]
        [InlineData("- text\n- 'quoted'\n- |\n  block\n")]
        [InlineData("[text]\n")]
        [InlineData("{key: text}\n")]
        [InlineData("")]
        public void RootScalar_AfterADocumentStart_AndScalarsElsewhere_AreRead(string yaml)
        {
            Accepted(yaml);
        }

        [Theory]
        [InlineData("--- text\n")]
        [InlineData("--- 'quoted'\n")]
        [InlineData("--- \"quoted\nstring\"\n")]
        [InlineData("--- &node text\n")]
        [InlineData("--- !!str text\n")]
        [InlineData("---\ttext\n")]
        [InlineData("%YAML 1.2\n--- text\n")]
        public void RootScalar_MayNotStartOnTheMarkerLine(string yaml)
        {
            Assert.Contains("may not start on the '---' line", Error(yaml));
        }

        [Theory]
        [InlineData("--- |\ntext\n")]
        [InlineData("--- >\nline1\nline2\n")]
        [InlineData("---\n|\ntext\n")]
        [InlineData("--- |-\n\ntext\n")]
        public void RootBlockScalar_ContentMustBeIndented(string yaml)
        {
            Assert.Contains("must be indented", Error(yaml));
        }

        [Theory]
        [InlineData("--- |\n text\n")]
        [InlineData("--- |1\n  text\n")]
        [InlineData("--- |\n")]
        [InlineData("--- >\n\n text\n")]
        [InlineData("key: |\n text\n")]
        [InlineData("- |\n text\n")]
        public void BlockScalar_IndentedOrNested_IsRead(string yaml)
        {
            Accepted(yaml);
        }

        [Theory]
        [InlineData("&a\nkey: v\n")]
        [InlineData("---\n&a\nkey: v\n")]
        [InlineData("---\n!!map\nkey: v\n")]
        [InlineData("---\n&a\n- x\n")]
        [InlineData("---\n&a\n!!str\ntext\n")]
        [InlineData("top:\n  &a\n  key: v\n")]
        [InlineData("top:\n  &a # comment\n  - x\n")]
        [InlineData("top: &a\n  !!map\n  key: v\n")]
        [InlineData("- \n  !!str\n  text\n")]
        [InlineData("---\n&a\n")]
        public void Properties_MayNotBeAloneOnTheirLine(string yaml)
        {
            Assert.Contains("may not be alone on its line", Error(yaml));
        }

        [Theory]
        [InlineData("--- &a\nkey: v\n")]
        [InlineData("--- !!map\nkey: v\n")]
        [InlineData("--- &a !!map\n&k key: v\n")]
        [InlineData("top: &a\n  key: v\n")]
        [InlineData("top: &a # comment\n  - x\n")]
        [InlineData("top: &a\n  &k key: v\n")]
        [InlineData("top: &a !!map\n  key: v\n")]
        [InlineData("top:\n  &a text\n")]
        [InlineData("top: !!str\n  text\n")]
        [InlineData("- &a\n  key: v\n")]
        [InlineData("- &a\n- text\n")]
        [InlineData("? &a\n: v\n")]
        [InlineData("? &a key\n: v\n")]
        [InlineData("&k key: &v value\n")]
        public void Properties_AfterAnIndicatorOrOnTheNodesLine_AreRead(string yaml)
        {
            Accepted(yaml);
        }

        [Theory]
        [InlineData("key: &a:b v\n")]
        [InlineData("key: &a. v\n")]
        [InlineData("key: &a/b v\n")]
        [InlineData("key: &a* v\n")]
        [InlineData("- &a v\n- *a:\n")]
        [InlineData("&a: key: v\n")]
        [InlineData("? &a.b [x]\n: v\n")]
        public void AnchorNames_AsciiPunctuationIsRefused(string yaml)
        {
            Assert.Contains("anchor or alias name may only", Error(yaml));
        }

        [Theory]
        [InlineData("key: &a v\n")]
        [InlineData("key: &A_b-9 v\n")]
        [InlineData("key: &42 v\n")]
        [InlineData("key: &kääk v\n")]
        [InlineData("- &😁 v\n- *😁\n")]
        public void AnchorNames_WordCharactersAndNonAscii_AreRead(string yaml)
        {
            Accepted(yaml);
        }

        [Theory]
        [InlineData("[a, b]: v\n")]
        [InlineData("{a: b}: v\n")]
        [InlineData("first: 1\n[a, b]: v\n")]
        [InlineData("top:\n  [a]: v\n")]
        [InlineData("- [a]: v\n")]
        [InlineData("--- &m\n&k [a]: v\n")]
        [InlineData("? []: x\n")]
        public void FlowCollection_AsAnImplicitBlockKey_IsRefused(string yaml)
        {
            Assert.Contains("may not be an implicit key of a block mapping", Error(yaml));
        }

        [Theory]
        [InlineData("? [a, b]\n: v\n")]
        [InlineData("? {a: b}\n: v\n")]
        [InlineData("{[a, b]: v}\n")]
        [InlineData("[[a]: b]\n")]
        [InlineData("key: [a, b]\n")]
        [InlineData("[a, b]\n")]
        public void FlowCollection_AsAnExplicitKeyOrInFlow_IsRead(string yaml)
        {
            Accepted(yaml);
        }

        [Fact]
        public void TheDirectiveDoesNotTurnTheRulesOnOrOff()
        {
            // A 1.3 document is read by the 1.2 rules without the option, and a 1.2 document by the draft's with it.
            Events("%YAML 1.3\n--- text\n", s_default);
            Assert.Contains("may not start on the '---' line", Error("%YAML 1.2\n--- text\n"));
            Assert.Contains("may not start on the '---' line", Error("%YAML 1.3\n--- text\n"));
        }

        [Fact]
        public void EveryDocumentOfAStreamIsChecked()
        {
            Accepted("---\na\n---\nb\n...\n--- |\n c\n");
            Assert.Contains("needs a '---' line", Error("---\na\n...\nb\n"));
            Assert.Contains("may not start on the '---' line", Error("---\na\n--- b\n"));
        }

        [Fact]
        public void TheErrorPointsAtTheNode()
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() => Events("# one\n\n  'text'\n", s_strict));
            Assert.Equal(2, e.LineNumber);
            Assert.Equal(2, e.BytePositionInLine);
        }

        [Fact]
        public void AKeyNodesReaderDoesNotTakeItsRootForADocuments()
        {
            // The scalars of a collection key are read by a reader over the key's bytes, whose depth 0 is not a document.
            const string Yaml = "? [plain, 'quoted', \"double\"]\n: v\n? - |\n    block\n  - text\n: w\n? 'scalar key'\n: x\n";
            Accepted(Yaml);

            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("? - text\n  - |\n    block\n: v\n"), s_strict);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            Utf8YamlReader key = reader.CreateKeyReader();
            var values = new List<string>();
            while (key.Read())
            {
                if (key.TokenType == YamlTokenType.String)
                {
                    values.Add(key.GetString()!);
                }
            }
            Assert.Equal(new[] { "text", "block\n" }, values);
        }

        [Fact]
        public void ADocumentBuiltFromAStrictReaderReadsItsValuesAgain()
        {
            // The DOM re-reads slices of the source for typed values; they are not documents and need no '---'.
            var reader = new Utf8YamlReader("---\n- 'quoted'\n- 12\n- &x plain\n- *x\n- |\n  block\n"u8, s_strict);
            using YamlDocument document = YamlDocument.ParseValue(ref reader);
            YamlElement root = document.RootElement;
            Assert.Equal("quoted", root[0].GetString());
            Assert.Equal(12, root[1].GetInt32());
            Assert.Equal("plain", root[2].GetString());
            Assert.Equal("block\n", root[4].GetString());
            Assert.Equal(new[] { "quoted", "12", "plain", "plain", "block\n" }, YamlSerializer.Deserialize<string[]>(root));
        }

        [Theory]
        [InlineData("---\n'text'\n")]
        [InlineData("---\ntext\n")]
        [InlineData("--- |\n text\n")]
        public void ARootScalarReadsThroughADocumentAndANode(string yaml)
        {
            // A document and a node copy the scalar's bytes and read them again; the copy has no '---' and is not a
            // document, so it must not be checked as one.
            string expected = yaml.Contains('|') ? "text\n" : "text";

            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), s_strict);
            using (YamlDocument document = YamlDocument.ParseValue(ref reader))
            {
                Assert.Equal(expected, document.RootElement.GetString());
                Assert.Equal(expected, document.RootElement.Clone().GetString());
                Assert.Equal(expected, document.RootElement.Deserialize<string>());
            }

            reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), s_strict);
            Assert.Equal(expected, Nodes.YamlNode.Parse(ref reader)!.GetValue<string>());
        }

        [Fact]
        public void ANestedScalarReadsThroughADocumentAndANode()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes("k: 'v'\nn: 12\nb: |\n  block\n");

            var reader = new Utf8YamlReader(utf8, s_strict);
            using (YamlDocument document = YamlDocument.ParseValue(ref reader))
            {
                Assert.Equal("v", document.RootElement.GetProperty("k").GetString());
                Assert.Equal(12, document.RootElement.GetProperty("n").GetInt32());
                Assert.Equal("block\n", document.RootElement.GetProperty("b").Clone().GetString());
                Assert.Equal("v", document.RootElement.GetProperty("k").Deserialize<string>());
            }

            reader = new Utf8YamlReader(utf8, s_strict);
            Nodes.YamlNode node = Nodes.YamlNode.Parse(ref reader)!;
            Assert.Equal("v", node["k"]!.GetValue<string>());
            Assert.Equal(12, node["n"]!.GetValue<int>());
            Assert.Equal("block\n", node["b"]!.GetValue<string>());
        }

        [Fact]
        public void TheSerializerReadsFromAStrictReader()
        {
            // The serializer reads an aliased value again from text of its own; that text is not checked.
            var reader = new Utf8YamlReader("--- &root\na: &x [p, 'q']\nb: *x\nc: |\n  block\n"u8, s_strict);
            var values = YamlSerializer.Deserialize<Dictionary<string, object>>(ref reader)!;
            Assert.Equal(new[] { "a", "b", "c" }, values.Keys);
            Assert.Equal(new[] { "p", "q" }, ((YamlElement)values["b"]).Deserialize<string[]>());

            var scalar = new Utf8YamlReader("---\n'text'\n"u8, s_strict);
            Assert.Equal("text", YamlSerializer.Deserialize<string>(ref scalar));
        }

        [Fact]
        public void WriterOutput_WhatReadsBack()
        {
            // Block style, whatever the test leg's default style is.
            static string Write(bool documentStart, Action<Utf8YamlWriter> value) => BlockWriterTests.Write(
                writer =>
                {
                    if (documentStart)
                    {
                        writer.WriteDocumentStart();
                    }
                    value(writer);
                },
                BlockWriterTests.Block);

            static void Anchored(Utf8YamlWriter w)
            {
                w.WriteAnchor("a");
                w.WriteStartArray();
                w.WriteStringValue("x");
                w.WriteEndArray();
            }

            // The writer does not write '---' by itself, so its root scalars are refused, and read once the document
            // is started.
            Assert.Contains("needs a '---' line", Error(Write(false, w => w.WriteStringValue("text"))));
            Assert.Contains("needs a '---' line", Error(Write(false, w => w.WriteStringValue("two\nlines\n"))));
            Accepted(Write(true, w => w.WriteStringValue("text")));
            Accepted(Write(true, w => w.WriteStringValue("two\nlines\n")));
            Accepted(Write(true, w => w.WriteNumberValue(12)));
            Accepted(Write(true, w => { w.WriteAnchor("a"); w.WriteStringValue("x"); }));

            // It writes the anchor or tag of a root block collection on a line of its own, with or without '---':
            // that is refused either way.
            Assert.Equal("&a\n- x\n", Write(false, Anchored));
            Assert.Equal("---\n&a\n- x\n", Write(true, Anchored));
            Assert.Contains("may not be alone on its line", Error(Write(false, Anchored)));
            Assert.Contains("may not be alone on its line", Error(Write(true, Anchored)));
            Assert.Contains("may not be alone on its line", Error(Write(true, w => { w.WriteTag("!t"); w.WriteStartObject(); w.WriteString("k", "v"); w.WriteEndObject(); })));

            // Collections with no properties, and properties below the root, read back.
            Accepted(Write(false, w => { w.WriteStartObject(); w.WriteString("key", "two\nlines\n"); w.WriteEndObject(); }));
            Accepted(Write(false, w => { w.WriteStartObject(); w.WritePropertyName("k"); Anchored(w); w.WriteEndObject(); }));
            Accepted(Write(false, w => { w.WriteStartArray(); Anchored(w); w.WriteEndArray(); }));
        }
    }
}
