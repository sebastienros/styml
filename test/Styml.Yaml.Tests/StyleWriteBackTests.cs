// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5f: the DOM keeps scalar styles on write-back,
    /// and what the library writes from a DOM and reads again (review 6: GetDeserializationValue, the merge rewrite,
    /// alias expansion at an object site) gives the same kinds and values under each schema. The yaml-test-suite check
    /// is YamlTestSuiteDomTests.WriteBack_KeepsScalarStyles.
    /// </summary>
    public class StyleWriteBackTests
    {
        private static readonly YamlWriterOptions s_writer = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private const string Styles =
            "plain: a b\nsingle: 'it''s'\ndouble: \"x\\ty\"\nliteral: |\n  l1\n  l2\nstrip: |-\n  s1\n  s2\nkeep: |+\n  k1\n\n" +
            "folded: >\n  f1\n  f2\n\n  f3\n'quoted key': 1\n\"dq key\": 2\nindent: |2\n   lead\n  next\nemptysingle: ''\nnum: '12'\n" +
            "list: ['x', \"y\", z]\n";

        private static string Write(YamlDocument document)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, s_writer))
            {
                document.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        [Fact]
        public void Document_KeepsStyles()
        {
            using YamlDocument document = YamlDocument.Parse(Styles);
            string written = Write(document);
            Assert.Equal(
                "plain: a b\nsingle: 'it''s'\ndouble: \"x\\ty\"\nliteral: |\n  l1\n  l2\nstrip: |-\n  s1\n  s2\nkeep: |+\n  k1\n\n" +
                "folded: >\n  f1 f2\n\n  f3\n'quoted key': 1\n\"dq key\": 2\nindent: |2\n   lead\n  next\nemptysingle: ''\nnum: '12'\n" +
                "list: ['x', \"y\", z]\n",
                written);

            // The fixed point, and the same values.
            using YamlDocument back = YamlDocument.Parse(written);
            Assert.Equal(written, Write(back));
            Assert.True(YamlElement.DeepEquals(document.RootElement, back.RootElement));

            // Styles that cannot be kept fall back to the writer's choice.
            // (A line of one space past the indentation is content: a line made only of spaces, which a literal cannot hold.)
            // M7b (item 2): a single-quoted scalar over several lines is kept; with a tab it falls back to double quotes.
            using YamlDocument fallbacks = YamlDocument.Parse("ml: 'a\n\n  b'\none: |-\n  x\nspaces: |\n  a\n   \n  b\ntab: 'a\tb\n\n  c'\n");
            Assert.Equal("ml: 'a\n\n  b'\none: x\nspaces: \"a\\n \\nb\\n\"\ntab: \"a\\tb\\nc\"\n", Write(fallbacks));

            // The JSON-compatible writer ignores the styles.
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }))
            {
                document.WriteTo(writer);
            }

            Assert.StartsWith("{\"plain\":\"a b\",\"single\":\"it\\u0027s\"", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
        }

        [Fact]
        public void ClonesAndNodes_KeepStyles()
        {
            using YamlDocument document = YamlDocument.Parse(Styles);
            string written = Write(document);

            // A clone keeps every style (the rows it copies hold them).
            YamlElement clone = document.RootElement.Clone();
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, s_writer))
            {
                clone.WriteTo(writer);
            }

            Assert.Equal(written, Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));

            // Nodes over an element, and their deep clones.
            YamlNode node = YamlNode.Parse(Styles)!;
            Assert.Equal(written, node.ToYamlString(s_block));
            Assert.Equal(written, node.DeepClone().ToYamlString(s_block));

            // A value made from a .NET string has no style: the writer chooses.
            Assert.Equal("a: x\n", new YamlObject { ["a"] = "x" }.ToYamlString(s_block));
        }

        [Fact]
        public void ObjectValues_KeepStyles()
        {
            // An object value read from a quoted or block scalar keeps its style; a plain one keeps the writer's choice.
            Dictionary<string, object> read = YamlSerializer.Deserialize<Dictionary<string, object>>("a: 'x'\nb: |\n  l\nc: \"1\"\nd: plain\n", s_block)!;
            Assert.Equal("a: 'x'\nb: |\n  l\nc: \"1\"\nd: plain\n", YamlSerializer.Serialize(read, s_block));
        }

        public static IEnumerable<object[]> Schemas() => new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe }.Select(s => new object[] { s });

        // Valid under every schema: quoted and block strings, and numbers and booleans in their JSON spelling.
        private const string ReRead = "m:\n  single: 'it''s'\n  double: \"x\"\n  literal: |\n    l1\n    l2\n  folded: >\n    f1\n    f2\n  quotedNumber: '12'\n  quotedTrue: \"true\"\n  number: 1\n  flag: true\n";

        [Theory]
        [MemberData(nameof(Schemas))]
        public void ReRead_GivesTheSourceKindsAndValues(YamlSchemaKind schema)
        {
            // Review 6: an element that does not stand alone is written (with its kept styles) and read again.
            var options = new YamlSerializerOptions(s_block) { Schema = schema };
            using YamlDocument document = YamlDocument.Parse(ReRead, new YamlDocumentOptions { Schema = schema });
            YamlElement inner = document.RootElement.GetProperty("m");
            Dictionary<string, object> fromElement = inner.Deserialize<Dictionary<string, object>>(options)!;
            Dictionary<string, object> fromText = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(ReRead, options)!["m"];
            AssertSameKindsAndValues(fromText, fromElement);

            // A typed read.
            Typed typed = inner.Deserialize<Typed>(options)!;
            Assert.Equal("it's", typed.single);
            Assert.Equal("l1\nl2\n", typed.literal);
            Assert.Equal("f1 f2\n", typed.folded);
            Assert.Equal("12", typed.quotedNumber);
            Assert.Equal("true", typed.quotedTrue);
        }

        public class Typed
        {
            public string? single { get; set; }
            public string? literal { get; set; }
            public string? folded { get; set; }
            public string? quotedNumber { get; set; }
            public string? quotedTrue { get; set; }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReRead_MergeRewrite(bool resolveMergeKeys)
        {
            const string Yaml = "base: &b\n  s: 'q'\n  l: |\n    x\n  n: 1\nitem:\n  <<: *b\n  own: 'o'\n";
            var options = new YamlSerializerOptions(s_block) { ResolveMergeKeys = resolveMergeKeys };
            using YamlDocument document = YamlDocument.Parse(Yaml, new YamlDocumentOptions { ResolveMergeKeys = resolveMergeKeys });
            Dictionary<string, object> fromElement = document.RootElement.GetProperty("item").Deserialize<Dictionary<string, object>>(options)!;
            Dictionary<string, object> fromText = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(Yaml, options)!["item"];
            AssertSameKindsAndValues(fromText, fromElement);
        }

        [Fact]
        public void MergeRewrite_KeepsStyles()
        {
            // Resolving merge keys rewrites the whole document: scalars outside the merged mappings keep their styles,
            // and so do the merged copies, with their anchors and tags.
            using YamlDocument off = YamlDocument.Parse(Styles);
            using YamlDocument on = YamlDocument.Parse(Styles + "m:\n  <<: {k: v}\n", new YamlDocumentOptions { ResolveMergeKeys = true });
            foreach (YamlProperty property in off.RootElement.EnumerateObject())
            {
                YamlElement merged = on.RootElement.GetProperty(property.Name);
                Assert.Equal(property.Value.ScalarStyle, merged.ScalarStyle);
                Assert.True(YamlElement.DeepEquals(property.Value, merged));
            }

            Assert.Equal(Write(off) + "m:\n  k: v\n", Write(on));

            const string Copies =
                "base: &b\n  l: &la |\n    x\n  s: 'q'\n  f: !t >\n    a\n    b\n  one: |-\n    y\nitem:\n  <<: *b\n  own: |\n    z\nref: *la\n";
            using YamlDocument copies = YamlDocument.Parse(Copies, new YamlDocumentOptions { ResolveMergeKeys = true });
            YamlElement source = copies.RootElement.GetProperty("base");
            YamlElement item = copies.RootElement.GetProperty("item");
            foreach (YamlElement mapping in new[] { source, item })
            {
                Assert.Equal(YamlScalarStyle.Literal, mapping.GetProperty("l").ScalarStyle);
                Assert.Equal("x\n", mapping.GetProperty("l").GetString());
                Assert.Equal(YamlScalarStyle.SingleQuoted, mapping.GetProperty("s").ScalarStyle);
                Assert.Equal(YamlScalarStyle.Folded, mapping.GetProperty("f").ScalarStyle);
                Assert.Equal("a b\n", mapping.GetProperty("f").GetString());
                Assert.Equal("!t", mapping.GetProperty("f").GetTag());

                // A block scalar the writer cannot hold as one stays quoted, as before.
                Assert.Equal(YamlScalarStyle.DoubleQuoted, mapping.GetProperty("one").ScalarStyle);
                Assert.Equal("y", mapping.GetProperty("one").GetString());
            }

            Assert.Equal("la", source.GetProperty("l").GetAnchor());
            Assert.Null(item.GetProperty("l").GetAnchor());
            Assert.Equal(YamlScalarStyle.Literal, item.GetProperty("own").ScalarStyle);
            Assert.Equal(YamlScalarStyle.Literal, copies.RootElement.GetProperty("ref").ScalarStyle);

            // The serializer's rewrite reads the same styles.
            var options = new YamlSerializerOptions(s_block) { ResolveMergeKeys = true };
            var read = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>(Copies, options)!;
            Assert.Equal(YamlScalarStyle.Literal, read["item"].GetProperty("l").ScalarStyle);
            Assert.Equal(YamlScalarStyle.Folded, read["item"].GetProperty("f").ScalarStyle);
            Assert.Equal(YamlScalarStyle.SingleQuoted, read["item"].GetProperty("s").ScalarStyle);
        }

        [Theory]
        [MemberData(nameof(Schemas))]
        public void MergeRewrite_BlockScalarsItCannotKeepStayStrings(YamlSchemaKind schema)
        {
            // A block scalar without a line break is not written as one; the rewrite double-quotes it, never plain, so
            // that it reads as the same string under every schema.
            const string Yaml = "n: |-\n  12\nt: >-\n  true\ny: |-\n  yes\nnul: |-\n  ~\ne: |-\nf:\n  - |\n    a\n  - 'x'\nm:\n  <<: {k: 'v'}\n";
            using YamlDocument off = YamlDocument.Parse(Yaml, new YamlDocumentOptions { Schema = schema });
            using YamlDocument on = YamlDocument.Parse(Yaml, new YamlDocumentOptions { Schema = schema, ResolveMergeKeys = true });
            foreach (string name in new[] { "n", "t", "y", "nul", "e" })
            {
                YamlElement merged = on.RootElement.GetProperty(name);
                Assert.Equal(YamlValueKind.String, merged.ValueKind);
                Assert.Equal(off.RootElement.GetProperty(name).GetString(), merged.GetString());
                Assert.Equal(YamlScalarStyle.DoubleQuoted, merged.ScalarStyle);
            }

            Assert.True(YamlElement.DeepEquals(off.RootElement.GetProperty("f"), on.RootElement.GetProperty("f")));
            Assert.Equal(YamlScalarStyle.Literal, on.RootElement.GetProperty("f")[0].ScalarStyle);
            Assert.Equal(YamlScalarStyle.SingleQuoted, on.RootElement.GetProperty("f")[1].ScalarStyle);
            Assert.Equal("v", on.RootElement.GetProperty("m").GetProperty("k").GetString());
        }

        [Fact]
        public void ReRead_AliasAtAnObjectSite()
        {
            const string Flow = "base: &b {s: 'q', d: \"w\", f: x}\ncopy: *b\n";
            const string Block = "base: &b\n  s: 'q'\n  d: \"w\"\n  l: |\n    x\ncopy: *b\n";
            foreach (string yaml in new[] { Flow, Block })
            {
                Dictionary<string, object> read = YamlSerializer.Deserialize<Dictionary<string, object>>(yaml, s_block)!;
                var copy = (YamlElement)read["copy"];
                Assert.Equal(YamlScalarStyle.SingleQuoted, copy.GetProperty("s").ScalarStyle);
                Assert.Equal("q", copy.GetProperty("s").GetString());
                if (yaml == Block)
                {
                    Assert.Equal(YamlScalarStyle.Literal, copy.GetProperty("l").ScalarStyle);
                    Assert.Equal("x\n", copy.GetProperty("l").GetString());
                }
                string written = YamlSerializer.Serialize(read, s_block);
                Dictionary<string, object> again = YamlSerializer.Deserialize<Dictionary<string, object>>(written, s_block)!;
                AssertSameKindsAndValues((YamlElement)read["copy"], (YamlElement)again["copy"]);
            }
        }

        [Theory]
        [MemberData(nameof(Schemas))]
        public void ReRead_AliasToAnOutsideAnchorInsideTheValue_KeepsStyles(YamlSchemaKind schema)
        {
            // An object site whose value has an alias to an anchor outside it reads the value written again with the
            // alias expanded: its scalars, and the expanded node's, keep their styles. A block scalar the writer cannot
            // hold as one is double-quoted, never plain, so it reads as the same string under every schema.
            const string Yaml =
                "base: &b\n  l: |\n    x\n  s: 'q'\n  f: >\n    a\n    b\nwrap:\n  own: |\n    y\n  'sk': 'a'\n  \"dk\": \"b\"\n" +
                "  one: |-\n    12\n  t: >-\n    true\n  ref: *b\n";
            var options = new YamlSerializerOptions(s_block) { Schema = schema };
            YamlElement wrap = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>(Yaml, options)!["wrap"];
            YamlElement expanded = wrap.GetProperty("ref");
            Assert.Equal(YamlScalarStyle.Literal, wrap.GetProperty("own").ScalarStyle);
            Assert.Equal(YamlScalarStyle.Literal, expanded.GetProperty("l").ScalarStyle);
            Assert.Equal("x\n", expanded.GetProperty("l").GetString());
            Assert.Equal(YamlScalarStyle.SingleQuoted, expanded.GetProperty("s").ScalarStyle);
            Assert.Equal(YamlScalarStyle.Folded, expanded.GetProperty("f").ScalarStyle);
            Assert.Equal("a b\n", expanded.GetProperty("f").GetString());
            foreach ((string name, string value) in new[] { ("one", "12"), ("t", "true") })
            {
                Assert.Equal(YamlValueKind.String, wrap.GetProperty(name).ValueKind);
                Assert.Equal(value, wrap.GetProperty(name).GetString());
                Assert.Equal(YamlScalarStyle.DoubleQuoted, wrap.GetProperty(name).ScalarStyle);
            }

            // Quoted keys are written quoted again.
            string written = YamlSerializer.Serialize(wrap, s_block);
            Assert.Contains("'sk': 'a'\n", written);
            Assert.Contains("\"dk\": \"b\"\n", written);
        }

        private static void AssertSameKindsAndValues(Dictionary<string, object> expected, Dictionary<string, object> actual)
        {
            Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.Ordinal), actual.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (string key in expected.Keys)
            {
                AssertSameKindsAndValues((YamlElement)expected[key], (YamlElement)actual[key]);
            }
        }

        private static void AssertSameKindsAndValues(YamlElement expected, YamlElement actual)
        {
            Assert.Equal(expected.ValueKind, actual.ValueKind);
            if (expected.ValueKind == YamlValueKind.String)
            {
                Assert.Equal(expected.GetString(), actual.GetString());
            }
            else
            {
                Assert.True(YamlElement.DeepEquals(expected, actual), $"{expected.GetRawText()} / {actual.GetRawText()}");
            }
        }
    }
}
