// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5a: <see cref="YamlDocument"/> reads YAML, and the traversals that
    /// follow aliases are bounded.
    /// </summary>
    public class DomYamlTests
    {
        private const string Sample =
            "name: app\nplain: hello world\nquoted: 'it''s'\nfold: >\n  a\n  b\nlit: |\n  x\n  y\nhex: 0x1F\ninf: .inf\n"
            + "nul: ~\nempty:\nbase: &b\n  k: 1\n  list: [1, two]\nref: *b\n&ka key: v\n*ka : w\ntagged: !t 5\nseq:\n- a\n- b: 2\n  c: 3\n";

        // The alias key repeats 'key' by construction, a duplicate under the default Error (M5c): the sample keeps both.
        private static readonly YamlDocumentOptions s_sampleOptions = new() { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        [Fact]
        public void Scalars_EveryStyle()
        {
            using YamlDocument document = YamlDocument.Parse(Sample, s_sampleOptions);
            YamlElement root = document.RootElement;
            Assert.Equal("hello world", root.GetProperty("plain").GetString());
            Assert.Equal(YamlScalarStyle.Plain, root.GetProperty("plain").ScalarStyle);
            Assert.Equal("it's", root.GetProperty("quoted").GetString());
            Assert.Equal(YamlScalarStyle.SingleQuoted, root.GetProperty("quoted").ScalarStyle);
            Assert.Equal("a b\n", root.GetProperty("fold").GetString());
            Assert.Equal(YamlScalarStyle.Folded, root.GetProperty("fold").ScalarStyle);
            Assert.Equal("x\ny\n", root.GetProperty("lit").GetString());
            Assert.Equal("|\n  x\n  y\n", root.GetProperty("lit").GetRawText());
            Assert.Equal(31, root.GetProperty("hex").GetInt32());
            Assert.Equal(double.PositiveInfinity, root.GetProperty("inf").GetDouble());
            Assert.Equal(YamlValueKind.Null, root.GetProperty("nul").ValueKind);
            Assert.Equal(YamlValueKind.Null, root.GetProperty("empty").ValueKind);
            Assert.Equal(YamlScalarStyle.None, root.GetProperty("base").ScalarStyle);
            Assert.True(root.GetProperty("plain").ValueEquals("hello world"));
        }

        [Fact]
        public void AnchorsTagsAndAliases()
        {
            using YamlDocument document = YamlDocument.Parse(Sample, s_sampleOptions);
            YamlElement root = document.RootElement;
            YamlElement @base = root.GetProperty("base");
            Assert.Equal("b", @base.GetAnchor());
            Assert.False(@base.IsAlias);
            YamlElement reference = root.GetProperty("ref");
            Assert.True(reference.IsAlias);
            Assert.Equal(YamlValueKind.Object, reference.ValueKind);
            Assert.Equal(1, reference.GetProperty("k").GetInt32());
            Assert.Equal("two", reference.GetProperty("list")[1].GetString());
            Assert.True(YamlElement.DeepEquals(@base, reference));
            Assert.Equal("!t", root.GetProperty("tagged").GetTag());
            Assert.Equal("!t", root.GetProperty("tagged").GetExpandedTag());

            // The alias key reads as the anchored scalar's text.
            string[] names = root.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.Equal(2, names.Count(n => n == "key"));
        }

        [Fact]
        public void GetRawText_OfANestedBlockNode_IsStandAloneYaml()
        {
            using YamlDocument document = YamlDocument.Parse(Sample, s_sampleOptions);
            YamlElement item = document.RootElement.GetProperty("seq")[1];
            Assert.Equal("b: 2\nc: 3", item.GetRawText());
            using YamlDocument back = YamlDocument.Parse(item.GetRawText());
            Assert.True(YamlElement.DeepEquals(item, back.RootElement));
        }

        [Fact]
        public void DeepEquals_ComparesValues_NotAnchorsTagsOrStyles()
        {
            using YamlDocument left = YamlDocument.Parse("a: &x [1, 'two']\nb: *x\n");
            using YamlDocument right = YamlDocument.Parse("a: !seq [1, \"two\"]\nb: [0x1, two]\n");
            Assert.True(YamlElement.DeepEquals(left.RootElement, right.RootElement));
            using YamlDocument other = YamlDocument.Parse("a: [1, two]\nb: [1, three]\n");
            Assert.False(YamlElement.DeepEquals(left.RootElement, other.RootElement));
        }

        // 6 levels of 9 aliases over a 9-item list: 9^7 leaves if expanded.
        private static string Bomb()
        {
            var text = new StringBuilder("a0: &a0 [x, x, x, x, x, x, x, x, x]\n");
            for (int i = 1; i <= 6; i++)
            {
                text.Append($"a{i}: &a{i} [" + string.Join(", ", Enumerable.Repeat($"*a{i - 1}", 9)) + "]\n");
            }

            return text.ToString();
        }

        [Fact]
        public void Bomb_NavigatesAndComparesWithoutCopying_ExpandingIsBounded()
        {
            using YamlDocument document = YamlDocument.Parse(Bomb());
            YamlElement top = document.RootElement.GetProperty("a6");
            Assert.Equal(9, top.GetArrayLength());
            Assert.True(YamlElement.DeepEquals(top, top));

            // The whole document writes its aliases; a part apart from its anchors would expand them, which is bounded.
            Assert.True(WriteBlock(document.RootElement).Length < 1000);
            Assert.Throws<YamlException>(() => WriteBlock(top));
            Assert.Throws<YamlException>(() => Materialize(YamlNode.Parse(Bomb())));
            Assert.Throws<YamlException>(() => top.Clone());
        }

        [Fact]
        public void RecursiveAlias_NavigatesAndCompares_ExpansionIsRefused()
        {
            using YamlDocument document = YamlDocument.Parse("a: &a [1, *a]\n");
            YamlElement a = document.RootElement.GetProperty("a");
            Assert.True(a[1].IsAlias);
            Assert.Equal(1, a[1][1][1][0].GetInt32());
            Assert.True(YamlElement.DeepEquals(a, a[1]));
            Assert.Equal("a: &a [1, *a]\n", WriteBlock(document.RootElement)); // M7b: the flow style is kept

            // Written alone, the anchored node holds its alias, so it writes as it is; a node copy cannot hold it.
            Assert.Equal("&a [1, *a]\n", WriteBlock(a[1]));
            Assert.Throws<YamlException>(() => Materialize(YamlNode.Parse("a: &a [1, *a]\n")));
        }

        // A chain of anchors deeper than MaxDepth (64): no alias is recursive, but following them is.
        private static string Chain(int length)
        {
            var text = new StringBuilder("a0: &a0 [x]\n");
            for (int i = 1; i <= length; i++)
            {
                text.Append($"a{i}: &a{i} [*a{i - 1}]\n");
            }

            return text.ToString();
        }

        [Fact]
        public void ChainDeeperThanMaxDepth_EveryTraversalThrows()
        {
            using YamlDocument document = YamlDocument.Parse(Chain(100));
            YamlElement top = document.RootElement.GetProperty("a100");
            using YamlDocument copy = YamlDocument.Parse(Chain(100));
            Assert.Throws<YamlException>(() => YamlElement.DeepEquals(top, copy.RootElement.GetProperty("a100")));
            Assert.Throws<YamlException>(() => WriteBlock(top));
            Assert.Throws<YamlException>(() => top.Clone());
            Assert.Throws<YamlException>(() => YamlNode.Parse(Chain(100))!["a100"]!.ToYamlString());
            Assert.Throws<YamlException>(() => top.GetDeserializationValue()); // Deserialize(YamlElement) writes it first
            Assert.Throws<YamlException>(() =>
            {
                YamlNode? node = YamlArray.Create(top);
                for (int i = 0; i < 100; i++)
                {
                    node = node![0];
                }
            });

            // Within the bound, the same traversals work.
            using YamlDocument shallow = YamlDocument.Parse(Chain(20));
            YamlElement a20 = shallow.RootElement.GetProperty("a20");
            Assert.True(YamlElement.DeepEquals(a20, a20.Clone()));
        }

        [Fact]
        public void Clone_KeepsDecodedScalarsAnchorsTagsAndInsideAliases()
        {
            YamlElement clone;
            using (YamlDocument document = YamlDocument.Parse("top:\n  inner: &n !t\n    text: |\n      one\n      two\n    again: *n\n  other: 1\n"))
            {
                clone = document.RootElement.GetProperty("top").Clone();
            }

            YamlElement inner = clone.GetProperty("inner");
            Assert.Equal("n", inner.GetAnchor());
            Assert.Equal("!t", inner.GetTag());
            Assert.Equal("one\ntwo\n", inner.GetProperty("text").GetString());
            Assert.Equal(YamlScalarStyle.Literal, inner.GetProperty("text").ScalarStyle);
            YamlElement again = inner.GetProperty("again");
            Assert.True(again.IsAlias);
            Assert.Equal("one\ntwo\n", again.GetProperty("text").GetString());
            Assert.Equal(1, clone.GetProperty("other").GetInt32());
        }

        [Fact]
        public void MaxAliasExpansion_IsTheLimit()
        {
            string yaml = "a: &a [1, 2, 3]\nb: [*a, *a]\n";
            Assert.Equal("[[1,2,3],[1,2,3]]", YamlNode.Parse(yaml)!["b"]!.ToYamlString(new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));
            using YamlDocument small = YamlDocument.Parse(yaml, new YamlDocumentOptions { MaxAliasExpansion = 4 });
            Assert.Throws<YamlException>(() => WriteJson(small.RootElement.GetProperty("b")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlDocumentOptions { MaxAliasExpansion = -1 });
        }

        /// <summary>
        /// The expansion budget is one operation's, not the document's: a long-lived document whose sub-element with an
        /// outside alias is cloned, written and deserialized 50,000 times (50,000 expansions, above the default limit of
        /// 10,000) never runs out, while one operation that expands more than the limit still throws.
        /// </summary>
        [Fact]
        public void AliasExpansionBudget_IsPerOperation()
        {
            using YamlDocument document = YamlDocument.Parse("x: &x 7\nsub: [*x]\n");
            YamlElement sub = document.RootElement.GetProperty("sub");
            for (int i = 0; i < 50_000; i++)
            {
                Assert.Equal(7, sub.Clone()[0].GetInt32());
                Assert.Equal("[&x 7]\n", WriteBlock(sub)); // the expansion keeps the anchor
                Assert.Equal(new[] { 7 }, YamlSerializer.Deserialize<int[]>(sub));
            }

            using YamlDocument small = YamlDocument.Parse("x: &x 7\nfour: [*x, *x, *x, *x]\nsub: [*x]\n", new YamlDocumentOptions { MaxAliasExpansion = 3 });
            YamlElement four = small.RootElement.GetProperty("four");
            Assert.Throws<YamlException>(() => WriteBlock(four));
            Assert.Throws<YamlException>(() => four.Clone());
            Assert.Throws<YamlException>(() => four.GetDeserializationValue());
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal("[&x 7]\n", WriteBlock(small.RootElement.GetProperty("sub"))); // the failures used no budget
            }
        }

        /// <summary>Each node tree made from a document has its own budget; one tree is still bounded.</summary>
        [Fact]
        public void AliasExpansionBudget_IsPerNodeTree()
        {
            using YamlDocument document = YamlDocument.Parse("x: &x 7\npair: [*x, *x]\nfour: [*x, *x, *x, *x]\n", new YamlDocumentOptions { MaxAliasExpansion = 3 });
            for (int tree = 0; tree < 5; tree++)
            {
                YamlObject root = YamlObject.Create(document.RootElement)!;
                Assert.Equal(7, (int)root["pair"]![1]!); // two expansions per tree: five trees use ten, each within 3
            }

            YamlObject one = YamlObject.Create(document.RootElement)!;
            Assert.Equal(7, (int)one["pair"]![0]!);
            Assert.Throws<YamlException>(() => one["four"]![0]); // the same tree: 2 + 4 above 3
            Assert.Throws<YamlException>(() => YamlObject.Create(document.RootElement)!["four"]![0]); // a fresh tree: 4 above 3

            // A tree whose root is itself an alias starts with that expansion.
            using YamlDocument aliased = YamlDocument.Parse("a: &a [1]\nb: *a\n", new YamlDocumentOptions { MaxAliasExpansion = 2 });
            Assert.Equal(1, (int)YamlArray.Create(aliased.RootElement.GetProperty("b"))![0]!);
        }

        [Fact]
        public void YamlNode_KeepsAnchorsAndTags()
        {
            YamlNode node = YamlNode.Parse("a: &x !t [1]\nb: *x\n")!;
            Assert.Equal("x", node["a"]!.Anchor);
            Assert.Equal("!t", node["a"]!.Tag);
            Assert.Null(node["b"]!.Anchor);
            Assert.Equal("!t", node["b"]!.Tag);
            node["a"]!.Tag = "!u";
            Assert.Contains("!u", node.ToYamlString(new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" }));
        }

        // M5a: a plain source scalar is written back plain where the syntax allows it, since
        // it was read as a string under the document's schema; a string written fresh with the same text is still quoted.
        [Fact]
        public void PlainStrings_RoundTripPlain_FreshStringsStayQuoted()
        {
            const string Yaml = "a: yes\nb: 2001-01-01\nc: 1_000\nd: 0001-01-01T00:00:00\n";
            using YamlDocument document = YamlDocument.Parse(Yaml);
            Assert.Equal(YamlValueKind.String, document.RootElement.GetProperty("b").ValueKind);
            Assert.Equal(Yaml, WriteBlock(document.RootElement));

            var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                writer.WriteStartObject();
                writer.WriteString("a", "yes");
                writer.WriteString("b", "2001-01-01");
                writer.WriteString("c", "1_000");
                writer.WriteEndObject();
            }

            Assert.Equal("a: \"yes\"\nb: \"2001-01-01\"\nc: \"1_000\"\n", Encoding.UTF8.GetString(buffer.ToArray()));
            Assert.Equal("{\"a\":\"yes\",\"b\":\"2001-01-01\",\"c\":\"1_000\",\"d\":\"0001-01-01T00:00:00\"}", WriteJson(document.RootElement));
        }

        // Serializer output written back through the DOM is the same text, for every M6a document (block style).
        [Theory]
        [MemberData(nameof(CorpusDocuments))]
        public void SerializerOutput_WrittenBackThroughTheDom_IsTheSameText(string name)
        {
            string json = File.ReadAllText(Path.Combine(SequenceTests.BenchCorpus, name + ".json"));
            var options = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = 256 };
            string serialized = YamlSerializer.Serialize(YamlNode.Parse(json, documentOptions: new YamlDocumentOptions { MaxDepth = 256 }), options);
            using YamlDocument document = YamlDocument.Parse(serialized, new YamlDocumentOptions { MaxDepth = 256 });
            Assert.Equal(serialized, WriteBlock(document.RootElement));
        }

        public static IEnumerable<object[]> CorpusDocuments() =>
            Directory.EnumerateFiles(SequenceTests.BenchCorpus, "*.yaml").Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

        // The twin oracle: each corpus document's YAML and its JSON twin give equal documents, with LF and with CRLF.
        // M6b's merge documents (merge-*) have the merged typed graph as their twin: their YAML is read with merge keys
        // resolved, and each of the twin's members equals the YAML's member of that name (the YAML's x-common extension
        // block is what the twin leaves out). Every other document is compared whole.
        [Theory]
        [MemberData(nameof(CorpusDocuments))]
        public void CorpusYaml_EqualsItsJsonTwin(string name)
        {
            string yaml = File.ReadAllText(Path.Combine(SequenceTests.BenchCorpus, name + ".yaml"));
            string json = File.ReadAllText(Path.Combine(SequenceTests.BenchCorpus, name + ".json"));
            bool merge = name.StartsWith("merge-", StringComparison.Ordinal);
            var options = new YamlDocumentOptions { MaxDepth = 256, ResolveMergeKeys = merge };
            using YamlDocument fromJson = YamlDocument.Parse(json, options);
            foreach (string text in new[] { yaml.Replace("\r\n", "\n"), yaml.Replace("\r\n", "\n").Replace("\n", "\r\n") })
            {
                using YamlDocument fromYaml = YamlDocument.Parse(text, options);
                if (merge)
                {
                    foreach (YamlProperty member in fromJson.RootElement.EnumerateObject())
                    {
                        Assert.True(YamlElement.DeepEquals(member.Value, fromYaml.RootElement.GetProperty(member.Name)), name + " " + member.Name);
                    }
                }
                else
                {
                    Assert.True(YamlElement.DeepEquals(fromJson.RootElement, fromYaml.RootElement), name);
                }

                // Round trip: the document written in block style reads back equal.
                foreach (string newLine in new[] { "\n", "\r\n" })
                {
                    using YamlDocument back = YamlDocument.Parse(WriteBlock(fromYaml.RootElement, newLine), options);
                    Assert.True(YamlElement.DeepEquals(fromYaml.RootElement, back.RootElement), name + " written with " + (newLine.Length == 1 ? "LF" : "CRLF"));
                }
            }
        }

        // Makes every node of the tree (a node over a document is made lazily, on first access).
        private static int Materialize(YamlNode? node) => node switch
        {
            YamlObject o => o.Sum(p => 1 + Materialize(p.Value)),
            YamlArray a => a.Sum(n => 1 + Materialize(n)),
            _ => 1,
        };

        private static string WriteBlock(YamlElement element, string newLine = "\n")
        {
            var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = newLine, MaxDepth = 1000 }))
            {
                element.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        /// <summary>
        /// Deserialize(YamlElement) reads the source bytes of a stand-alone node
        /// (the root, a flow collection, a scalar without an indentation indicator) and writes, then reads, a nested
        /// block collection, a block scalar with an indentation indicator, and a node with an alias to a node outside it.
        /// </summary>
        [Fact]
        public void Deserialize_FromElement_TakesTheSourceOrWritesTheNode()
        {
            const string yaml = "x: &x 7\nlist: [1, two]\nplain: hello\nquoted: 'it''s'\nlit: |\n  a\n  b\n"
                + "indented: |2\n   a\n  b\nnested:\n  k: 1\n  v: [p, q]\npair: [*x, 2]\n";
            using YamlDocument document = YamlDocument.Parse(yaml);
            YamlElement root = document.RootElement;

            string Value(YamlElement element) => Encoding.UTF8.GetString(element.GetDeserializationValue().ToArray());

            // From the source: the exact source text.
            Assert.Equal(yaml, Value(root));
            Assert.Equal("[1, two]", Value(root.GetProperty("list")));
            Assert.Equal("hello", Value(root.GetProperty("plain")));
            Assert.Equal("'it''s'", Value(root.GetProperty("quoted")));
            Assert.Equal("|\n  a\n  b\n", Value(root.GetProperty("lit")));

            // Written and read: not a substring of the source.
            foreach (string name in new[] { "indented", "nested", "pair" })
            {
                Assert.DoesNotContain(Value(root.GetProperty(name)), yaml);
            }

            Assert.Equal(new[] { "1", "two" }, YamlSerializer.Deserialize<string[]>(root.GetProperty("list")));
            Assert.Equal("hello", YamlSerializer.Deserialize<string>(root.GetProperty("plain")));
            Assert.Equal("it's", YamlSerializer.Deserialize<string>(root.GetProperty("quoted")));
            Assert.Equal("a\nb\n", YamlSerializer.Deserialize<string>(root.GetProperty("lit")));
            Assert.Equal(" a\nb\n", YamlSerializer.Deserialize<string>(root.GetProperty("indented")));
            Dictionary<string, YamlNode?> nested = YamlSerializer.Deserialize<Dictionary<string, YamlNode?>>(root.GetProperty("nested"))!;
            Assert.Equal(1, (int)nested["k"]!);
            Assert.Equal(new[] { "p", "q" }, nested["v"]!.AsArray().Select(n => (string)n!));
            Assert.Equal(new[] { 7, 2 }, YamlSerializer.Deserialize<int[]>(root.GetProperty("pair")));
        }

        private static string WriteJson(YamlElement element)
        {
            var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }))
            {
                element.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        [Theory]
        [InlineData("'true'", "true")]
        [InlineData("'12'", "12")]
        [InlineData("'~'", "~")]
        [InlineData("''", "")]
        [InlineData("'say \"hi\" \\'", "say \"hi\" \\")]
        [InlineData("|\n  true\n", "true\n")]
        [InlineData(">-\n  a\n  b\n", "a b")]
        [InlineData("a\n  b", "a b")]
        public void SerializerMadeStringElements_RawTextReadsBackAsTheSameString(string scalar, string value)
        {
            // M5a's CreateForYamlString gave the decoded text as raw text ('true' read back as true); since M5b it is
            // the double-quoted form, so the raw text reads back as the same kind and value, as YamlDocument.Parse's.
            string block = scalar.Contains('\n') ? "\n  " + scalar.Replace("\n", "\n  ").TrimEnd(' ') : " " + scalar;
            var member = (YamlElement)YamlSerializer.Deserialize<Dictionary<string, object>>("v:" + block + "\n")!["v"];
            YamlElement dictionaryValue = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>("v:" + block + "\n")!["v"];
            var item = (YamlElement)YamlSerializer.Deserialize<List<object>>("-" + block + "\n")![0];
            foreach (YamlElement element in new[] { member, dictionaryValue, item, member.Clone() })
            {
                Assert.Equal(YamlValueKind.String, element.ValueKind);
                Assert.Equal(value, element.GetString());
                using YamlDocument again = YamlDocument.Parse(element.GetRawText());
                Assert.Equal(YamlValueKind.String, again.RootElement.ValueKind);
                Assert.Equal(value, again.RootElement.GetString());
                Assert.Equal(value, YamlSerializer.Deserialize<string>(element));
            }
        }
    }
}
