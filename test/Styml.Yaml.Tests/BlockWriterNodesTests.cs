// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M4b's block-style writer members: node
    /// properties, aliases, explicit keys, documents and directives, comments, flow collections, single-quoted and
    /// folded scalars, MultiLineStringStyle, MaxLineWidth and ExpandSequenceItems. Each output is compared byte for byte
    /// and, where given, its read-back with the yaml-test-suite event trace of Styml's reader.
    /// </summary>
    public class BlockWriterNodesTests
    {
        private static readonly YamlWriterOptions Block = BlockWriterTests.Block;

        private static string Write(Action<Utf8YamlWriter> calls, YamlWriterOptions? options = null) =>
            BlockWriterTests.Write(calls, options ?? Block);

        // The output, and its events (between +STR/+DOC and -DOC/-STR for one document).
        private static void Check(string expected, Action<Utf8YamlWriter> calls, string? events, YamlWriterOptions? options = null)
        {
            string actual = Write(calls, options);
            Assert.Equal(expected, actual);
            if (events is not null)
            {
                Assert.Equal(events, Events(actual));
            }
        }

        private static string Events(string yaml)
        {
            List<string> trace = YamlEventTrace.Read(Encoding.UTF8.GetBytes(yaml));
            return string.Join(" | ", trace.Where(e => e is not ("+STR" or "-STR" or "+DOC" or "-DOC")));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Anchors, tags, aliases

        [Fact]
        public void Anchors_OnValues_AndAliases()
        {
            Check("a: &x v\nb: w\nc: *x\n", w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("a");
                w.WriteAnchor("x");
                w.WriteStringValue("v");
                w.WriteString("b", "w");
                w.WritePropertyName("c");
                w.WriteAliasValue("x");
                w.WriteEndObject();
            }, "+MAP | =VAL :a | =VAL &x :v | =VAL :b | =VAL :w | =VAL :c | =ALI *x | -MAP");
        }

        [Fact]
        public void Properties_BeforeAPropertyName_BelongToTheKey_InEveryForm()
        {
            Check("&k !!str a: v\n*k : w\n&n b: 1\n&m c:\n  d: e\n", w =>
            {
                w.WriteStartObject();
                w.WriteAnchor("k");
                w.WriteTag("!!str");
                w.WriteString("a", "v");
                w.WriteAliasPropertyName("k");
                w.WriteStringValue("w");
                w.WriteAnchor("n");
                w.WriteNumber("b", 1);
                w.WriteAnchor("m");
                w.WriteStartObject("c");
                w.WriteString("d", "e");
                w.WriteEndObject();
                w.WriteEndObject();
            }, "+MAP | =VAL &k <tag:yaml.org,2002:str> :a | =VAL :v | =ALI *k | =VAL :w | =VAL &n :b | =VAL :1 | =VAL &m :c | +MAP | =VAL :d | =VAL :e | -MAP | -MAP");
        }

        [Fact]
        public void Properties_OnCollections_PutTheFirstChildOnANewLine()
        {
            Check("- &m\n  a: \"1\"\n- &e {}\n- !t\n  - 1\n- *m\n- &k a: b\n", w =>
            {
                w.WriteStartArray();
                w.WriteAnchor("m");
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteEndObject();
                w.WriteAnchor("e");
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteTag("!t");
                w.WriteStartArray();
                w.WriteNumberValue(1);
                w.WriteEndArray();
                w.WriteAliasValue("m");
                w.WriteStartObject();
                w.WriteAnchor("k");
                w.WriteString("a", "b");
                w.WriteEndObject();
                w.WriteEndArray();
            }, "+SEQ | +MAP &m | =VAL :a | =VAL \"1 | -MAP | +MAP {} &e | -MAP | +SEQ <!t> | =VAL :1 | -SEQ | =ALI *m | +MAP | =VAL &k :a | =VAL :b | -MAP | -SEQ");
        }

        [Fact]
        public void Properties_AtTheRoot_AndUnderAKey()
        {
            Check("&r\na: \"1\"\n", w =>
            {
                w.WriteAnchor("r");
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteEndObject();
            }, "+MAP &r | =VAL :a | =VAL \"1 | -MAP");
            Check("&r []\n", w =>
            {
                w.WriteAnchor("r");
                w.WriteStartArray();
                w.WriteEndArray();
            }, "+SEQ [] &r | -SEQ");
            Check("!<tag:example.com,2000:x> &a v\n", w =>
            {
                w.WriteTag("!<tag:example.com,2000:x>");
                w.WriteAnchor("a");
                w.WriteStringValue("v");
            }, "=VAL &a <tag:example.com,2000:x> :v");
            Check("k: &m\n  a: \"1\"\ns: !!seq\n- x\ne: &z []\n", w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteAnchor("m");
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteEndObject();
                w.WritePropertyName("s");
                w.WriteTag("!!seq");
                w.WriteStartArray();
                w.WriteStringValue("x");
                w.WriteEndArray();
                w.WritePropertyName("e");
                w.WriteAnchor("z");
                w.WriteStartArray();
                w.WriteEndArray();
                w.WriteEndObject();
            }, "+MAP | =VAL :k | +MAP &m | =VAL :a | =VAL \"1 | -MAP | =VAL :s | +SEQ <tag:yaml.org,2002:seq> | =VAL :x | -SEQ | =VAL :e | +SEQ [] &z | -SEQ | -MAP");
        }

        [Theory]
        [InlineData("!")]
        [InlineData("!foo")]
        [InlineData("!!str")]
        [InlineData("!e!foo")]
        [InlineData("!<tag:example.com,2000:app/foo>")]
        [InlineData("!foo%21bar")]
        [InlineData("!<!local>")]
        public void Tags_ValidForms_AreWrittenAsGiven(string tag)
        {
            string yaml = Write(w =>
            {
                w.WriteStartArray();
                w.WriteTag(tag);
                w.WriteStringValue("v");
                w.WriteEndArray();
            });
            Assert.Equal("- " + tag + " v\n", yaml);
        }

        [Theory]
        [InlineData("")]
        [InlineData("foo")]
        [InlineData("!a b")]
        [InlineData("!a,b")]
        [InlineData("!a[b")]
        [InlineData("!!")]
        [InlineData("!e!")]
        [InlineData("!e x!y")]
        [InlineData("!<>")]
        [InlineData("!<a b>")]
        [InlineData("!foo%2")]
        [InlineData("!é")]
        public void Tags_InvalidForms_Throw(string tag)
        {
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteTag(tag)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("a b")]
        [InlineData("a,b")]
        [InlineData("a[")]
        [InlineData("a}")]
        [InlineData("a\tb")]
        [InlineData("a\u0086")]
        [InlineData("a\u009F")]
        [InlineData("\ufeffa")]
        [InlineData("a\uffff")]
        public void AnchorNames_Invalid_Throw(string name)
        {
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteAnchor(name)));
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteAliasValue(name)));
        }

        [Fact]
        public void AnchorNames_MayHoldAColon_AndAliasKeysAreSpaced()
        {
            Check("&a:b x: *a:b\n*a:b : \"y\"\n", w =>
            {
                w.WriteStartObject();
                w.WriteAnchor("a:b");
                w.WritePropertyName("x");
                w.WriteAliasValue("a:b");
                w.WriteAliasPropertyName("a:b");
                w.WriteStringValue("y");
                w.WriteEndObject();
            }, "+MAP | =VAL &a:b :x | =ALI *a:b | =ALI *a:b | =VAL \"y | -MAP");
        }

        [Fact]
        public void Properties_Misplaced_Throw()
        {
            // Twice the same property, a property with no node after it, an alias with properties.
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteAnchor("a"); w.WriteAnchor("b"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteTag("!a"); w.WriteTag("!b"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteAnchor("a"); w.WriteEndArray(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteAnchor("a"); w.WriteAliasValue("b"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteAnchor("a"); w.WriteAliasPropertyName("b"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStringValue("x"); w.WriteAnchor("a"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteAnchor("a"); w.WriteDocumentStart(); }));
        }

        [Fact]
        public void NodeProperties_InJsonCompatibleStyle_Throw()
        {
            var json = new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible };
            foreach (Action<Utf8YamlWriter> call in new Action<Utf8YamlWriter>[]
            {
                w => w.WriteAnchor("a"),
                w => w.WriteTag("!a"),
                w => w.WriteAliasValue("a"),
                w => { w.WriteStartObject(); w.WriteAliasPropertyName("a"); },
                w => { w.WriteStartObject(); w.WriteStartPropertyKey(); },
                w => w.WriteEndPropertyKey(),
                w => w.WriteDocumentEnd(),
                w => w.WriteDirective("%YAML 1.2"),
                w => w.WriteCollectionStyle(YamlCollectionStyle.Flow),
            })
            {
                Assert.Throws<InvalidOperationException>(() => Write(call, json));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Explicit keys

        [Fact]
        public void ExplicitKeys_ScalarAndCollections()
        {
            Check("? k\n: v\n", w =>
            {
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteStringValue("k");
                w.WriteEndPropertyKey();
                w.WriteStringValue("v");
                w.WriteEndObject();
            }, "+MAP | =VAL :k | =VAL :v | -MAP");
            Check("? a: \"1\"\n  b: \"2\"\n:\n- x\nz: \"y\"\n", w =>
            {
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteString("b", "2");
                w.WriteEndObject();
                w.WriteEndPropertyKey();
                w.WriteStartArray();
                w.WriteStringValue("x");
                w.WriteEndArray();
                w.WriteString("z", "y");
                w.WriteEndObject();
            }, "+MAP | +MAP | =VAL :a | =VAL \"1 | =VAL :b | =VAL \"2 | -MAP | +SEQ | =VAL :x | -SEQ | =VAL :z | =VAL \"y | -MAP");
            Check("- ? &q\n    - 1\n    - 2\n  :\n    m: \"n\"\n", w =>
            {
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteAnchor("q");
                w.WriteStartArray();
                w.WriteNumberValue(1);
                w.WriteNumberValue(2);
                w.WriteEndArray();
                w.WriteEndPropertyKey();
                w.WriteStartObject();
                w.WriteString("m", "n");
                w.WriteEndObject();
                w.WriteEndObject();
                w.WriteEndArray();
            }, "+SEQ | +MAP | +SEQ &q | =VAL :1 | =VAL :2 | -SEQ | +MAP | =VAL :m | =VAL \"n | -MAP | -MAP | -SEQ");
        }

        [Fact]
        public void ExplicitKeys_LiteralNestedAndEmpty()
        {
            Check("? |\n  a\n  b\n: v\n", w =>
            {
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue("a\nb\n");
                w.WriteEndPropertyKey();
                w.WriteStringValue("v");
                w.WriteEndObject();
            }, "+MAP | =VAL |a\\nb\\n | =VAL :v | -MAP");
            Check("? ? a\n  : b\n: c\n? {}\n: null\n", w =>
            {
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteStartObject();
                w.WriteStartPropertyKey();
                w.WriteStringValue("a");
                w.WriteEndPropertyKey();
                w.WriteStringValue("b");
                w.WriteEndObject();
                w.WriteEndPropertyKey();
                w.WriteStringValue("c");
                w.WriteStartPropertyKey();
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteEndPropertyKey();
                w.WriteNullValue();
                w.WriteEndObject();
            }, "+MAP | +MAP | =VAL :a | =VAL :b | -MAP | =VAL :c | +MAP {} | -MAP | =VAL :null | -MAP");
        }

        [Fact]
        public void ExplicitKeys_Misused_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteEndPropertyKey(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteStartPropertyKey(); w.WriteEndPropertyKey(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteStartPropertyKey(); w.WriteStringValue("a"); w.WriteStringValue("b"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteStartPropertyKey(); w.WriteStringValue("a"); w.WriteEndArray(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteStartPropertyKey(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteAnchor("a"); w.WriteStartPropertyKey(); }));

            // The key's level counts toward the maximum depth.
            var shallow = new YamlWriterOptions { Style = YamlWriterStyle.Block, MaxDepth = 1 };
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteStartPropertyKey(); }, shallow));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Flow collections

        [Fact]
        public void Flow_Collections_OnOneLine()
        {
            Check("k: [a, \"b,c\", 1, {x: \"y\"}, []]\n\"n\": m\n", w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteStringValue("a");
                w.WriteStringValue("b,c");
                w.WriteNumberValue(1);
                w.WriteStartObject();
                w.WriteString("x", "y");
                w.WriteEndObject();
                w.WriteStartArray();
                w.WriteEndArray();
                w.WriteEndArray();
                w.WriteString("n", "m");
                w.WriteEndObject();
            }, "+MAP | =VAL :k | +SEQ [] | =VAL :a | =VAL \"b,c | =VAL :1 | +MAP {} | =VAL :x | =VAL \"y | -MAP | +SEQ [] | -SEQ | -SEQ | =VAL \"n | =VAL :m | -MAP");
            Check("{a: b, &z c: d:e, f: *z, ? [p] : null}\n", w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartObject();
                w.WriteString("a", "b");
                w.WriteAnchor("z");
                w.WriteString("c", "d:e");
                w.WritePropertyName("f");
                w.WriteAliasValue("z");
                w.WriteStartPropertyKey();
                w.WriteStartArray();
                w.WriteStringValue("p");
                w.WriteEndArray();
                w.WriteEndPropertyKey();
                w.WriteNullValue();
                w.WriteEndObject();
            }, "+MAP {} | =VAL :a | =VAL :b | =VAL &z :c | =VAL :d:e | =VAL :f | =ALI *z | +SEQ [] | =VAL :p | -SEQ | =VAL :null | -MAP");
            Check("- [a]\n- &f {}\n- {k: v}\n- tail\n", w =>
            {
                w.WriteStartArray();
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteStringValue("a");
                w.WriteEndArray();
                w.WriteAnchor("f");
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartObject();
                w.WriteString("k", "v");
                w.WriteEndObject();
                w.WriteStringValue("tail");
                w.WriteEndArray();
            }, "+SEQ | +SEQ [] | =VAL :a | -SEQ | +MAP {} &f | -MAP | +MAP {} | =VAL :k | =VAL :v | -MAP | =VAL :tail | -SEQ");
        }

        [Fact]
        public void Flow_PlainScalars_AreStricter_AndBlockScalarsAreNotWritten()
        {
            // ':' and '?' are text in a flow plain scalar (YAML 1.2 and 1.1); QuoteForLenientReaders quotes them too.
            // M7c: the width breaks the collection between items, not the scalars.
            Check("[\"a,b\",\n  a:b,\n  ?x, x?,\n  \"[x]\",\n  \"a\\nb\",\n  plain text]\n", w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                foreach (string s in new[] { "a,b", "a:b", "?x", "x?", "[x]" })
                {
                    w.WriteStringValue(s);
                }

                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue("a\nb");
                w.WriteStringValue("plain text");
                w.WriteEndArray();
            }, "+SEQ [] | =VAL \"a,b | =VAL :a:b | =VAL :?x | =VAL :x? | =VAL \"[x] | =VAL \"a\\nb | =VAL :plain text | -SEQ",
            new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 5, MultiLineStringStyle = YamlScalarStyle.Literal });
        }

        [Fact]
        public void Flow_BlockHintInside_IsFlow_AndHintsMisused_Throw()
        {
            Assert.Equal("[[1]]\n", Write(w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteCollectionStyle(YamlCollectionStyle.Block);
                w.WriteStartArray();
                w.WriteNumberValue(1);
                w.WriteEndArray();
                w.WriteEndArray();
            }));
            Assert.Equal("- 1\n", Write(w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Block);
                w.WriteStartArray();
                w.WriteNumberValue(1);
                w.WriteEndArray();
            }));

            Assert.Throws<ArgumentOutOfRangeException>(() => Write(w => w.WriteCollectionStyle((YamlCollectionStyle)2)));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WriteCollectionStyle(YamlCollectionStyle.Flow); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WriteStringValue("a"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WritePropertyName("a"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WriteEndArray(); }));

            // In the forms that write a name and a value, the hint is the value's.
            Assert.Equal("a: [1]\n", Write(w =>
            {
                w.WriteStartObject();
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray("a");
                w.WriteNumberValue(1);
                w.WriteEndArray();
                w.WriteEndObject();
            }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WriteString("a", "b"); }));
        }

        // A raw block collection as a sequence item after a tag, an anchor or a comment line: the same bytes whether
        // the writer still holds what came before or has just flushed it, and the item is the collection (the fix of
        // M4p's final step to e01b0be: the dash after properties depended on the last held byte).
        [Theory]
        [InlineData(0, false, "+SEQ | =VAL :1 | +SEQ <!t> | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        [InlineData(0, true, "+SEQ | =VAL :1 | +SEQ <!t> | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        [InlineData(1, false, "+SEQ | =VAL :1 | +SEQ &x | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        [InlineData(1, true, "+SEQ | =VAL :1 | +SEQ &x | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        [InlineData(2, false, "+SEQ | =VAL :1 | +SEQ | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        [InlineData(2, true, "+SEQ | =VAL :1 | +SEQ | =VAL :1 | =VAL :2 | -SEQ | -SEQ")]
        public void RawBlockCollection_AsASequenceItem_AfterPropertiesOrAComment_HeldOrFlushed(int before, bool flush, string events)
        {
            Action<Utf8YamlWriter> calls(bool flushFirst) => w =>
            {
                w.WriteStartArray();
                w.WriteNumberValue(1);
                switch (before)
                {
                    case 0: w.WriteTag("!t"); break;
                    case 1: w.WriteAnchor("x"); break;
                    default: w.WriteCommentValue("c"); break;
                }

                if (flushFirst)
                {
                    w.Flush();
                }

                w.WriteRawValue("- 1\n- 2");
                w.WriteEndArray();
            };

            string held = Write(calls(false));
            string actual = Write(calls(flush));
            Assert.Equal(held, actual);
            Assert.Equal(events, Events(actual));
        }

        [Fact]
        public void Flow_RawValues_OnOneLine()
        {
            Assert.Equal("[{a: 1}, x]\n", Write(w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteRawValue("{a: 1}");
                w.WriteRawValue("x");
                w.WriteEndArray();
            }));
            Assert.Throws<InvalidOperationException>(() => Write(w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteRawValue("a: 1\nb: 2\n");
            }));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Documents and directives

        [Fact]
        public void Documents_EndAndDirectives()
        {
            string yaml = Write(w =>
            {
                w.WriteDirective("%YAML 1.2");
                w.WriteDirective("%TAG !e! tag:example.com,2000:");
                w.WriteDocumentStart();
                w.WriteTag("!e!foo");
                w.WriteStringValue("a");
                w.WriteDocumentEnd();
                w.WriteStringValue("b");
                w.WriteDirective("%YAML 1.2");
                w.WriteDocumentStart();
                w.WriteStringValue("c");
            });

            // "---" before "b" (a root node after "..."), "..." before the directive after "b".
            Assert.Equal("%YAML 1.2\n%TAG !e! tag:example.com,2000:\n---\n!e!foo a\n...\n---\nb\n...\n%YAML 1.2\n---\nc\n", yaml);
            List<string> trace = YamlEventTrace.Read(Encoding.UTF8.GetBytes(yaml));
            Assert.Equal(new[] { "+STR", "+DOC", "=VAL <tag:example.com,2000:foo> :a", "-DOC", "+DOC", "=VAL :b", "-DOC", "+DOC", "=VAL :c", "-DOC", "-STR" }, trace);
        }

        [Fact]
        public void Directives_Yaml13_IsWrittenAsGivenAndReadsBack()
        {
            string yaml = Write(w =>
            {
                w.WriteDirective("%YAML 1.3");
                w.WriteDocumentStart();
                w.WriteStringValue("a");
            });

            Assert.Equal("%YAML 1.3\n---\na\n", yaml);
            Assert.Equal(new[] { "+STR", "+DOC", "=VAL :a", "-DOC", "-STR" }, YamlEventTrace.Read(Encoding.UTF8.GetBytes(yaml)));
        }

        [Theory]
        [InlineData("%YAML")]
        [InlineData("%YAML 1")]
        [InlineData("%YAML 1.2 x")]
        [InlineData("%TAG !e!")]
        [InlineData("%TAG e! tag:x")]
        [InlineData("%TAG !e! ,x")]
        [InlineData("%FOO bar")]
        [InlineData("YAML 1.2")]
        public void Directives_Invalid_Throw(string directive)
        {
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteDirective(directive)));
        }

        [Fact]
        public void Directives_AndDocumentEnd_Misplaced_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteDirective("%YAML 1.2"); w.WriteStringValue("a"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteDirective("%YAML 1.2"); w.WriteDirective("%YAML 1.1"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteDocumentStart(); w.WriteDirective("%YAML 1.2"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteDirective("%YAML 1.2"); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartArray(); w.WriteDocumentEnd(); }));
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteAnchor("a"); w.WriteDocumentEnd(); }));

            // A comment may come between directives and "---".
            Assert.Equal("%YAML 1.2\n# c\n---\na\n", Write(w => { w.WriteDirective("%YAML 1.2"); w.WriteCommentValue("c"); w.WriteDocumentStart(); w.WriteStringValue("a"); }));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Comments

        [Fact]
        public void Comments_OnTheirOwnLines()
        {
            Check("# top\n# first\na: \"1\"\n# between\n# lines\nb:\n  # after key\n  v\nc:\n# in seq\n- x\ne:\n  # empty\n  {}\n# after root\n", w =>
            {
                w.WriteCommentValue("top");
                w.WriteStartObject();
                w.WriteCommentValue("first");
                w.WriteString("a", "1");
                w.WriteCommentValue("between\nlines");
                w.WritePropertyName("b");
                w.WriteCommentValue("after key");
                w.WriteStringValue("v");
                w.WritePropertyName("c");
                w.WriteStartArray();
                w.WriteCommentValue("in seq");
                w.WriteStringValue("x");
                w.WriteEndArray();
                w.WritePropertyName("e");
                w.WriteStartObject();
                w.WriteCommentValue("empty");
                w.WriteEndObject();
                w.WriteEndObject();
                w.WriteCommentValue("after root");
            }, "+MAP | =VAL :a | =VAL \"1 | =VAL :b | =VAL :v | =VAL :c | +SEQ | =VAL :x | -SEQ | =VAL :e | +MAP {} | -MAP | -MAP");
        }

        [Fact]
        public void Comments_BeforeFirstChildren_AndAfterProperties()
        {
            Check("-\n  # c\n  a: \"1\"\n-\n  # c2\n  []\n", w =>
            {
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteCommentValue("c");
                w.WriteString("a", "1");
                w.WriteEndObject();
                w.WriteStartArray();
                w.WriteCommentValue("c2");
                w.WriteEndArray();
                w.WriteEndArray();
            }, "+SEQ | +MAP | =VAL :a | =VAL \"1 | -MAP | +SEQ [] | -SEQ | -SEQ");
            Check("k: &a\n  # c\n  v\nm: &b\n  # c\n  x: \"y\"\n", w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteAnchor("a");
                w.WriteCommentValue("c");
                w.WriteStringValue("v");
                w.WritePropertyName("m");
                w.WriteAnchor("b");
                w.WriteCommentValue("c");
                w.WriteStartObject();
                w.WriteString("x", "y");
                w.WriteEndObject();
                w.WriteEndObject();
            }, "+MAP | =VAL :k | =VAL &a :v | =VAL :m | +MAP &b | =VAL :x | =VAL \"y | -MAP | -MAP");
            Check("# c\n{}\n", w =>
            {
                w.WriteStartObject();
                w.WriteCommentValue("c");
                w.WriteEndObject();
            }, "+MAP {} | -MAP");
            Assert.Throws<InvalidOperationException>(() => Write(w => { w.WriteStartObject(); w.WriteAnchor("a"); w.WriteCommentValue("c"); }));
        }

        [Fact]
        public void Comments_AtTheEndOfALine_WhereTheLineAllows()
        {
            Check("a: \"1\" # eol a\nb: [] # eol empty\nc:\n- x # eol x\n# after nonempty\nd: |\n  l1\n  l2\n# after literal\ne: *a # alias\n# root\n", w =>
            {
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteCommentValue("eol a", true);
                w.WritePropertyName("b");
                w.WriteStartArray();
                w.WriteEndArray();
                w.WriteCommentValue("eol empty", true);
                w.WritePropertyName("c");
                w.WriteStartArray();
                w.WriteStringValue("x");
                w.WriteCommentValue("eol x", true);
                w.WriteEndArray();
                w.WriteCommentValue("after nonempty", true);
                w.WritePropertyName("d");
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue("l1\nl2\n");
                w.WriteCommentValue("after literal", true);
                w.WritePropertyName("e");
                w.WriteAliasValue("a");
                w.WriteCommentValue("alias", true);
                w.WriteEndObject();
                w.WriteCommentValue("root", true);
            }, "+MAP | =VAL :a | =VAL \"1 | =VAL :b | +SEQ [] | -SEQ | =VAL :c | +SEQ | =VAL :x | -SEQ | =VAL :d | =VAL |l1\\nl2\\n | =VAL :e | =ALI *a | -MAP");
        }

        [Fact]
        public void Comments_InFlowCollections_EndTheLine()
        {
            Check("[ # first\n  a, # c\n  b, # last\n  ]\n", w =>
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteCommentValue("first");
                w.WriteStringValue("a");
                w.WriteCommentValue("c");
                w.WriteStringValue("b");
                w.WriteCommentValue("last", true);
                w.WriteEndArray();
            }, "+SEQ [] | =VAL :a | =VAL :b | -SEQ");
            Check("k: {a: b, # c\n  x: # after key\n  \"y\"}\n", w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartObject();
                w.WriteString("a", "b");
                w.WriteCommentValue("c");
                w.WritePropertyName("x");
                w.WriteCommentValue("after key");
                w.WriteStringValue("y");
                w.WriteEndObject();
                w.WriteEndObject();
            }, "+MAP | =VAL :k | +MAP {} | =VAL :a | =VAL :b | =VAL :x | =VAL \"y | -MAP | -MAP");
        }

        [Fact]
        public void Comments_Text_AndJsonCompatibleStyle()
        {
            // "*/" is text in a YAML comment; CR, LF and CRLF separate lines; other line breaks and controls throw.
            Assert.Equal("# a */ b\n# c\n# d\n#\n# e\n", Write(w => w.WriteCommentValue("a */ b\r\nc\rd\n\ne")));
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteCommentValue("a\u0085b")));
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteCommentValue("a\u0001b")));
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteCommentValue("a\u2028b", true)));

            // JSON-compatible style: the end-of-line form is the existing comment.
            var json = new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible };
            Assert.Equal("[/*c*/]", Write(w => { w.WriteStartArray(); w.WriteCommentValue("c", true); w.WriteEndArray(); }, json));
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteCommentValue("a*/b", true), json));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scalars: single-quoted, folded, MultiLineStringStyle, MaxLineWidth

        [Fact]
        public void SingleQuoted_WhereNothingNeedsAnEscape()
        {
            // M7b (item 2): a line break is written in single quotes over several lines; a tab still needs double quotes.
            Check("'it''s': 'C:\\x \"q\" ''a'''\n\"n\": 'a\n\n  b'\nt: \"a\\tb\"\n", w =>
            {
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WritePropertyName("it's");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteStringValue("C:\\x \"q\" 'a'");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("n", "a\nb");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("t", "a\tb");
                w.WriteEndObject();
            }, "+MAP | =VAL 'it's | =VAL 'C:\\\\x \"q\" 'a' | =VAL \"n | =VAL 'a\\nb | =VAL :t | =VAL \"a\\tb | -MAP");
        }

        [Fact]
        public void Folded_KeepsLineBreaksBetweenTextLines()
        {
            Check("f: >-\n  a\n\n  b\n\n\n  c\n    d\n  e\ng: >\n  one line\n", w =>
            {
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.Folded);
                w.WriteString("f", "a\nb\n\nc\n  d\ne");
                w.WriteScalarStyle(YamlScalarStyle.Folded);
                w.WriteString("g", "one line\n");
                w.WriteEndObject();
            }, "+MAP | =VAL :f | =VAL >a\\nb\\n\\nc\\n  d\\ne | =VAL :g | =VAL >one line\\n | -MAP");
        }

        [Fact]
        public void Folded_LeadingSpaceTrailingLinesAndRoot()
        {
            // M5f: the unhinted third item is literal by default, with its indentation indicator.
            Check("- >2-\n   x\n  y\n- >+\n  a\n\n\n- |2-\n   root\n  x\n", w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Folded);
                w.WriteStringValue(" x\ny");
                w.WriteScalarStyle(YamlScalarStyle.Folded);
                w.WriteStringValue("a\n\n\n");
                w.WriteStringValue(" root\nx");
                w.WriteEndArray();
            }, "+SEQ | =VAL > x\\ny | =VAL >a\\n\\n\\n | =VAL | root\\nx | -SEQ");
            Assert.Equal("\" a\\nb\"\n", Write(w => { w.WriteScalarStyle(YamlScalarStyle.Folded); w.WriteStringValue(" a\nb"); }));
        }

        [Fact]
        public void MultiLineStringStyle_AppliesToValues_NotToKeys()
        {
            var literal = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MultiLineStringStyle = YamlScalarStyle.Literal };
            Check("- |\n  a\n  b\n- x\n- \"a\\nb\": 1\n- \"x\\r\\ny\"\n", w =>
            {
                w.WriteStartArray();
                w.WriteStringValue("a\nb\n");
                w.WriteStringValue("x");
                w.WriteStartObject();
                w.WriteNumber("a\nb", 1);
                w.WriteEndObject();
                w.WriteStringValue("x\r\ny");
                w.WriteEndArray();
            }, "+SEQ | =VAL |a\\nb\\n | =VAL :x | +MAP | =VAL \"a\\nb | =VAL :1 | -MAP | =VAL \"x\\r\\ny | -SEQ", literal);

            var folded = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MultiLineStringStyle = YamlScalarStyle.Folded };
            Assert.Equal("k: >-\n  a\n\n  b\n", Write(w => { w.WriteStartObject(); w.WriteString("k", "a\nb"); w.WriteEndObject(); }, folded));

            // A hint wins over the option.
            Assert.Equal("k: \"a\\nb\"\n", Write(w => { w.WriteStartObject(); w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted); w.WriteString("k", "a\nb"); w.WriteEndObject(); }, folded));

            YamlWriterOptions options = default;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.MultiLineStringStyle = YamlScalarStyle.Plain);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.MultiLineStringStyle = YamlScalarStyle.SingleQuoted);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.MultiLineStringStyle = (YamlScalarStyle)9);
            options.MultiLineStringStyle = YamlScalarStyle.None;
            Assert.Equal(YamlScalarStyle.None, options.MultiLineStringStyle);
        }

        [Fact]
        public void MaxLineWidth_FoldsPlainDoubleQuotedAndFolded()
        {
            var narrow = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 20 };
            Check("plain: the quick\n  brown fox jumps\n  over the lazy dog\ndq: \"the quick: \\\n  brown fox\\tjumps \\\n  over\"\nfold: >-\n  the quick brown\n  fox jumps over the\n  lazy dog\n\n  again\nthe quick brown fox jumps: v\n", w =>
            {
                w.WriteStartObject();
                w.WriteString("plain", "the quick brown fox jumps over the lazy dog");
                w.WriteString("dq", "the quick: brown fox\tjumps over");
                w.WriteScalarStyle(YamlScalarStyle.Folded);
                w.WriteString("fold", "the quick brown fox jumps over the lazy dog\nagain");
                w.WriteString("the quick brown fox jumps", "v");
                w.WriteEndObject();
            }, "+MAP | =VAL :plain | =VAL :the quick brown fox jumps over the lazy dog | =VAL :dq | =VAL \"the quick: brown fox\\tjumps over | =VAL :fold | =VAL >the quick brown fox jumps over the lazy dog\\nagain | =VAL :the quick brown fox jumps | =VAL :v | -MAP", narrow);

            // Not at two spaces, not before an indicator; a word longer than the width stays whole.
            Assert.Equal("- aaaaaa  bbbbbb -c\n- abcdefghijklmnop\n", Write(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue("aaaaaa  bbbbbb -c");
                w.WriteStringValue("abcdefghijklmnop");
                w.WriteEndArray();
            }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 6 }));

            // A root scalar's continuation lines are indented, away from the document markers.
            Assert.Equal("aaa\n  ...\n  b\n", Write(w => w.WriteStringValue("aaa ... b"), new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 3 }));

            YamlWriterOptions options = default;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxLineWidth = -1);
        }

        [Fact]
        public void ExpandSequenceItems_PutsCollectionItemsOnTheNextLine()
        {
            Check("-\n  a: \"1\"\n-\n  - 1\n- []\n", w =>
            {
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteString("a", "1");
                w.WriteEndObject();
                w.WriteStartArray();
                w.WriteNumberValue(1);
                w.WriteEndArray();
                w.WriteStartArray();
                w.WriteEndArray();
                w.WriteEndArray();
            }, "+SEQ | +MAP | =VAL :a | =VAL \"1 | -MAP | +SEQ | =VAL :1 | -SEQ | +SEQ [] | -SEQ | -SEQ",
            new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", ExpandSequenceItems = true });
        }

        // ---------------------------------------------------------------------------------------------------------
        // The serializer

        [Fact]
        public void SerializerOptions_ReachTheWriter()
        {
            var options = new YamlSerializerOptions
            {
                WriteStyle = YamlWriterStyle.Block,
                NewLine = "\n",
                MultiLineStringStyle = YamlScalarStyle.Literal,
                MaxLineWidth = 12,
                ExpandSequenceItems = true,
            };
            string yaml = YamlSerializer.Serialize(new object[] { new Dictionary<string, string> { ["s"] = "a\nb", ["w"] = "one two three four" } }, options);
            Assert.Equal("-\n  s: |-\n    a\n    b\n  w: one two\n    three\n    four\n", yaml);

            var copy = new YamlSerializerOptions(options);
            Assert.Equal(YamlScalarStyle.Literal, copy.MultiLineStringStyle);
            Assert.Equal(12, copy.MaxLineWidth);
            Assert.True(copy.ExpandSequenceItems);
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { MaxLineWidth = -1 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { MultiLineStringStyle = YamlScalarStyle.Plain });
        }
    }
}
