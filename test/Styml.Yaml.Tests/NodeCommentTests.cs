// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A node tree parsed with <see cref="YamlCommentHandling.Allow"/> keeps its comments: through its document while a
    /// collection is untouched, then on its entries, items, start and end, which edits keep in step and code can set.
    /// </summary>
    public class NodeCommentTests
    {
        private static readonly YamlDocumentOptions s_allow = new() { CommentHandling = YamlCommentHandling.Allow };

        private const string Sample =
            "# header\nname: web # the name\n# ports\nports:\n- 80 # http\n# tls\n- 443\nlimits: # caps\n  cpu: 1\n# footer\n";

        public static IEnumerable<object[]> Sources() => new[]
        {
            new object[] { Sample },
            new object[] { "- a\n# between\n- b # after b\n" },
            new object[] { "key:\n  # c\n  x: 1\n" },
            new object[] { "key:\n  # c\n  value\n" },
            new object[] { "outer:\n  inner: 1\n  # inner tail\n# outer tail\n" },
            new object[] { "a: [1, # in flow\n  2]\n" },
            new object[] { "a: &x 1 # anchored\nb: !t 2 # tagged\n" },
            new object[] { "? [k] # complex\n: v\n# end\n" },
        };

        [Fact]
        public void UneditedTree_WritesAliasComments()
        {
            // A materialized tree writes an alias as a copy, so only the unedited one writes it as the source did.
            YamlNode node = YamlNode.Parse("a: &x 1 # anchored\nc: *x # alias\n", documentOptions: s_allow)!;
            Assert.Equal("a: &x 1 # anchored\nc: *x # alias\n", Write(node));
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public void UneditedTree_WritesTheComments(string yaml)
        {
            using YamlDocument document = YamlDocument.Parse(yaml, s_allow);
            YamlNode node = YamlNode.Parse(yaml, documentOptions: s_allow)!;
            Assert.Equal(Write(document.RootElement), Write(node));
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public void MaterializedTree_WritesTheSameText(string yaml)
        {
            YamlNode unedited = YamlNode.Parse(yaml, documentOptions: s_allow)!;
            YamlNode touched = YamlNode.Parse(yaml, documentOptions: s_allow)!;
            Materialize(touched);
            Assert.Equal(Write(unedited), Write(touched));
        }

        [Fact]
        public void Comments_AreRead()
        {
            YamlObject root = YamlNode.Parse(Sample, documentOptions: s_allow)!.AsObject();
            Assert.Equal("header", root.StartComment);
            Assert.Equal("footer", root.EndComment);
            Assert.Equal("the name", root.GetComment("name", YamlCommentPosition.Trailing));
            Assert.Null(root.GetComment("name", YamlCommentPosition.Leading));
            Assert.Equal("ports", root.GetComment("ports", YamlCommentPosition.Leading));
            Assert.Equal("caps", root.GetComment("limits", YamlCommentPosition.Trailing));
            Assert.Null(root.GetComment("missing", YamlCommentPosition.Leading));

            YamlArray ports = root["ports"]!.AsArray();
            Assert.Equal("http", ports.GetComment(0, YamlCommentPosition.Trailing));
            Assert.Equal("tls", ports.GetComment(1, YamlCommentPosition.Leading));
            Assert.Throws<ArgumentOutOfRangeException>(() => ports.GetComment(2, YamlCommentPosition.Leading));
        }

        [Fact]
        public void ReadingAComment_DoesNotMaterializeTheNode()
        {
            // An untouched mapping writes its element, with the source's key styles.
            YamlObject root = YamlNode.Parse("'quoted': 1 # one\n", documentOptions: s_allow)!.AsObject();
            Assert.Equal("one", root.GetComment("quoted", YamlCommentPosition.Trailing));
            Assert.Equal("'quoted': 1 # one\n", Write(root));
        }

        [Fact]
        public void Edits_KeepCommentsWithTheirEntries()
        {
            YamlObject root = YamlNode.Parse(Sample, documentOptions: s_allow)!.AsObject();
            root["name"] = "api";                                     // replaced value: the entry keeps its comments
            root.Insert(0, "kind", "service");                        // the header stays the mapping's
            root.Remove("limits");                                    // its comment goes with it
            root.SetAt(root.IndexOf("name"), "title", "api");         // renamed in place
            YamlArray ports = root["ports"]!.AsArray();
            ports.Insert(0, 22);
            ports.RemoveAt(2);                                         // 443 and its leading comment

            Assert.Equal("# header\nkind: service\ntitle: api # the name\n# ports\nports:\n- 22\n- 80 # http\n# footer\n", Write(root));
            Assert.Null(root.GetComment("limits", YamlCommentPosition.Trailing));
        }

        [Fact]
        public void ArrayRemovals_KeepCommentsInStep()
        {
            YamlArray items = YamlNode.Parse("- a # a\n- b # b\n- c # c\n- d # d\n", documentOptions: s_allow)!.AsArray();
            items.RemoveAll(n => n!.GetValue<string>() == "b");
            Assert.True(items.Remove(items[0]));
            Assert.Equal("- c # c\n- d # d\n", Write(items));
            items.RemoveRange(0, 1);
            items.Add("e");
            Assert.Equal("- d # d\n- e\n", Write(items));
            items.Clear();
            items.Add("f");
            Assert.Equal("- f\n", Write(items));
        }

        [Fact]
        public void Clear_KeepsTheCollectionComments()
        {
            YamlObject root = YamlNode.Parse(Sample, documentOptions: s_allow)!.AsObject();
            root.Clear();
            root["only"] = 1;
            Assert.Equal("# header\nonly: 1\n# footer\n", Write(root));
        }

        [Fact]
        public void Comments_AreSetFromCode()
        {
            var config = new YamlObject { ["name"] = "web", ["ports"] = new YamlArray(80, 443), ["empty"] = null };
            config.StartComment = "Service settings";
            config.EndComment = "end";
            config.SetComment("name", YamlCommentPosition.Trailing, "the public name");
            config.SetComment("ports", YamlCommentPosition.Leading, "Ports\nto expose");
            config.SetComment("empty", YamlCommentPosition.Trailing, "nothing yet");
            config["ports"]!.AsArray().SetComment(1, YamlCommentPosition.Trailing, "tls");

            Assert.Equal(
                "# Service settings\nname: web # the public name\n# Ports\n# to expose\nports:\n- 80\n- 443 # tls\nempty: null # nothing yet\n# end\n",
                Write(config));

            config.SetComment("name", YamlCommentPosition.Trailing, null);
            Assert.Null(config.GetComment("name", YamlCommentPosition.Trailing));
            Assert.Throws<KeyNotFoundException>(() => config.SetComment("missing", YamlCommentPosition.Leading, "x"));
            Assert.Throws<ArgumentException>(() => config.SetComment("name", YamlCommentPosition.Trailing, "two\nlines"));
            Assert.Throws<ArgumentException>(() => config.StartComment = "bell\a");
            Assert.Throws<ArgumentOutOfRangeException>(() => config.SetComment("name", (YamlCommentPosition)5, "x"));
        }

        [Fact]
        public void TrailingComment_OfABlockCollection_FollowsTheKey()
        {
            var config = new YamlObject { ["limits"] = new YamlObject { ["cpu"] = 1 } };
            config.SetComment("limits", YamlCommentPosition.Trailing, "caps");
            Assert.Equal("limits:\n  # caps\n  cpu: 1\n", Write(config));
        }

        [Fact]
        public void JsonCompatibleWriter_WritesNoComments()
        {
            YamlObject root = YamlNode.Parse(Sample, documentOptions: s_allow)!.AsObject();
            string unedited = Write(root, YamlWriterStyle.JsonCompatible);
            Materialize(root);
            Assert.Equal(unedited, Write(root, YamlWriterStyle.JsonCompatible));
            Assert.DoesNotContain("/*", unedited);
        }

        [Fact]
        public void DeepClone_CopiesComments_DeepEquals_IgnoresThem()
        {
            YamlObject root = YamlNode.Parse(Sample, documentOptions: s_allow)!.AsObject();
            YamlNode untouchedClone = root.DeepClone();
            Materialize(root);
            YamlNode clone = root.DeepClone();
            Assert.Equal(Write(root), Write(clone));
            Assert.Equal(Write(root), Write(untouchedClone));

            YamlNode plain = YamlNode.Parse(Sample)!;
            Assert.True(YamlNode.DeepEquals(root, plain));
        }

        [Fact]
        public void AliasCopy_KeepsNoCommentsOfTheAnchoredNode()
        {
            YamlObject root = YamlNode.Parse("base: &b\n  x: 1 # x\n  inner:\n    # in\n    y: 2\ncopy: *b\n", documentOptions: s_allow)!.AsObject();
            YamlObject copy = root["copy"]!.AsObject();
            Assert.Null(copy.GetComment("x", YamlCommentPosition.Trailing));
            Assert.Null(copy["inner"]!.AsObject().StartComment);
            copy["z"] = 3;
            Assert.Equal("base: &b\n  x: 1 # x\n  inner:\n    # in\n    \"y\": 2\ncopy:\n  x: 1\n  inner:\n    \"y\": 2\n  z: 3\n", Write(root));
        }

        [Theory]
        [InlineData(YamlDuplicateKeyHandling.FirstWins, "a: 1 # first\n")]
        [InlineData(YamlDuplicateKeyHandling.LastWins, "a: 2 # second\n")]
        public void RepeatedKeys_KeepTheCommentsOfTheEntryKept(YamlDuplicateKeyHandling handling, string expected)
        {
            YamlObject root = YamlNode.Parse("a: 1 # first\na: 2 # second\n", documentOptions: new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Allow, DuplicateKeyHandling = handling })!.AsObject();
            Materialize(root);
            Assert.Equal(expected, Write(root));
        }

        [Fact]
        public void CaseInsensitiveNames_FindTheEntrysComments()
        {
            YamlObject root = YamlNode.Parse("Name: web # n\n", new YamlNodeOptions { PropertyNameCaseInsensitive = true }, s_allow)!.AsObject();
            Assert.Equal("n", root.GetComment("name", YamlCommentPosition.Trailing));
            root.SetComment("NAME", YamlCommentPosition.Leading, "above");
            Assert.Equal("# above\nName: web # n\n", Write(root));
        }

        [Fact]
        public void ParseWithoutAllow_KeepsNoComments()
        {
            YamlObject root = YamlNode.Parse(Sample)!.AsObject();
            Assert.Null(root.StartComment);
            Assert.Null(root.GetComment("name", YamlCommentPosition.Trailing));
            Assert.Equal("name: web\nports:\n- 80\n- 443\nlimits:\n  cpu: 1\n", Write(root));
        }

        // Every collection of the tree materialized, nothing changed.
        private static void Materialize(YamlNode? node)
        {
            switch (node)
            {
                case YamlObject o:
                    foreach (KeyValuePair<string, YamlNode?> entry in o)
                    {
                        Materialize(entry.Value);
                    }

                    break;
                case YamlArray a:
                    foreach (YamlNode? item in a)
                    {
                        Materialize(item);
                    }

                    break;
            }
        }

        private static string Write(YamlNode node, YamlWriterStyle style = YamlWriterStyle.Block)
        {
            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Style = style, NewLine = "\n" }))
            {
                node.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string Write(YamlElement element)
        {
            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                element.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
