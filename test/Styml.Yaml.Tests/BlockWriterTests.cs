// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Block style of <see cref="Utf8YamlWriter"/> (M4a): the layout of collections and of root values,
    /// and oracle 2, the read-back with token equality and the differential oracle against
    /// the JSON-compatible style for the same calls.
    /// </summary>
    public class BlockWriterTests
    {
        internal static readonly YamlWriterOptions Block = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };

        internal static string Write(Action<Utf8YamlWriter> calls, YamlWriterOptions options)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options))
            {
                calls(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static string Block2(Action<Utf8YamlWriter> calls) => Write(calls, Block);

        // ---------------------------------------------------------------------------------------------------------
        // Layout (decision 7)

        [Fact]
        public void Mapping_KeysAndScalars()
        {
            Assert.Equal("name: app\nreplicas: 3\n\"on\": true\nx: null\n", Block2(w =>
            {
                w.WriteStartObject();
                w.WriteString("name", "app");
                w.WriteNumber("replicas", 3);
                w.WriteBoolean("on", true);
                w.WriteNull("x");
                w.WriteEndObject();
            }));
        }

        [Fact]
        public void Nested_CompactItems_EmptyCollections()
        {
            Assert.Equal(
                "meta:\n  a: b\nitems:\n- x\n- k: v\n  \"n\": 1\n- - 1\n  - 2\n- []\n- {}\nempty: {}\nnone: []\n",
                Block2(w =>
                {
                    w.WriteStartObject();
                    w.WriteStartObject("meta");
                    w.WriteString("a", "b");
                    w.WriteEndObject();
                    w.WriteStartArray("items");
                    w.WriteStringValue("x");
                    w.WriteStartObject();
                    w.WriteString("k", "v");
                    w.WriteNumber("n", 1);
                    w.WriteEndObject();
                    w.WriteStartArray();
                    w.WriteNumberValue(1);
                    w.WriteNumberValue(2);
                    w.WriteEndArray();
                    w.WriteStartArray();
                    w.WriteEndArray();
                    w.WriteStartObject();
                    w.WriteEndObject();
                    w.WriteEndArray();
                    w.WriteStartObject("empty");
                    w.WriteEndObject();
                    w.WriteStartArray("none");
                    w.WriteEndArray();
                    w.WriteEndObject();
                }));
        }

        [Theory]
        [InlineData(false, 2, "k:\n- a: 1\n  b:\n  - 1\n  - 2\n")]
        [InlineData(false, 4, "k:\n-   a: 1\n    b:\n    - 1\n    - 2\n")]
        [InlineData(true, 2, "k:\n  - a: 1\n    b:\n      - 1\n      - 2\n")]
        [InlineData(true, 4, "k:\n    -   a: 1\n        b:\n            - 1\n            - 2\n")]
        public void Sequences_IndentSizeAndIndentSequences(bool indentSequences, int indentSize, string expected)
        {
            // Decision 7: {k: [{a: 1, b: [1, 2]}]}; with IndentSize 4, YamlDotNet's layout.
            var options = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", IndentSize = indentSize, IndentSequences = indentSequences };
            string actual = Write(w =>
            {
                w.WriteStartObject();
                w.WriteStartArray("k");
                w.WriteStartObject();
                w.WriteNumber("a", 1);
                w.WriteStartArray("b");
                w.WriteNumberValue(1);
                w.WriteNumberValue(2);
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            }, options);
            Assert.Equal(expected, actual);
            AssertReadsBack(actual, "{\"k\":[{\"a\":1,\"b\":[1,2]}]}");
        }

        [Fact]
        public void RootValues_EndWithALineBreak_AndDocumentStart()
        {
            Assert.Equal("x\n---\n1\n---\n{}\n---\n- a\n", Block2(w =>
            {
                w.WriteStringValue("x");
                w.WriteDocumentStart();
                w.WriteNumberValue(1);
                w.WriteDocumentStart();
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteDocumentStart();
                w.WriteStartArray();
                w.WriteStringValue("a");
                w.WriteEndArray();
            }));
        }

        [Fact]
        public void DocumentStart_JsonCompatible_WritesTheMarkerOnItsOwnLine()
        {
            Assert.Equal("{}\n---\n1", Write(w =>
            {
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteDocumentStart();
                w.WriteNumberValue(1);
            }, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible, NewLine = "\n" }));
        }

        [Fact]
        public void DocumentStart_InsideACollection_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Block2(w =>
            {
                w.WriteStartArray();
                w.WriteDocumentStart();
            }));
        }

        [Fact]
        public void SecondRootValue_WithoutDocumentStart_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Block2(w =>
            {
                w.WriteNumberValue(1);
                w.WriteNumberValue(2);
            }));
        }

        [Fact]
        public void NewLine_CrLf()
        {
            Assert.Equal("a:\r\n- 1\r\n", Write(w =>
            {
                w.WriteStartObject();
                w.WriteStartArray("a");
                w.WriteNumberValue(1);
                w.WriteEndArray();
                w.WriteEndObject();
            }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\r\n" }));
        }

        [Fact]
        public void NothingIsHeldBack()
        {
            // 4.2: after WriteStartObject() at the root, nothing is pending in block style; a flush mid-document changes
            // nothing.
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(buffer, Block);
            writer.WriteStartObject();
            Assert.Equal(0, writer.BytesPending);
            writer.WritePropertyName("a");
            writer.Flush();
            writer.WriteStartArray();
            writer.Flush();
            writer.WriteNumberValue(1);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            Assert.Equal("a:\n- 1\n", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void IndentSizeBelowTwo_Throws(int indentSize)
        {
            Assert.Throws<ArgumentException>(() => new Utf8YamlWriter(new ArrayBufferWriter<byte>(),
                new YamlWriterOptions { Style = YamlWriterStyle.Block, IndentSize = indentSize }));
        }

        [Fact]
        public void TabIndentation_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Utf8YamlWriter(new ArrayBufferWriter<byte>(),
                new YamlWriterOptions { Style = YamlWriterStyle.Block, IndentCharacter = '\t' }));
        }

        [Fact]
        public void Style_Unset_IsBlock_UnlessTheSwitchIsSet()
        {
            // M4c: an unset style is block; the AppContext switch restores JSON-compatible (test leg YJ sets it).
            bool json = AppContext.TryGetSwitch("System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle", out bool enabled) && enabled;
            Assert.Equal(json ? YamlWriterStyle.JsonCompatible : YamlWriterStyle.Block, default(YamlWriterOptions).Style);
            Assert.Equal(json ? "{\"a\":1}" : "a: 1" + Environment.NewLine, Write(w =>
            {
                w.WriteStartObject();
                w.WriteNumber("a", 1);
                w.WriteEndObject();
            }, default));
        }

        [Fact]
        public void SkipValidation_KeepsTheStructure()
        {
            var options = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", SkipValidation = true };
            Assert.Equal("k:\n- a: b\n  c: []\n", Write(w =>
            {
                w.WriteStartObject();
                w.WriteStartArray("k");
                w.WriteStartObject();
                w.WriteString("a", "b");
                w.WriteStartArray("c");
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            }, options));
        }

        [Fact]
        public void ExplicitKey_AboveTheImplicitKeyLimit()
        {
            string longKey = new string('k', 1025);
            string atLimit = new string('j', 1024);
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WriteString(longKey, "v");
                w.WriteString(atLimit, "w");
                w.WriteStartObject(longKey + "x");
                w.WriteNumber("n", 1);
                w.WriteEndObject();
                w.WriteEndObject();
            });
            Assert.Equal($"? {longKey}\n: v\n{atLimit}: w\n? {longKey}x\n:\n  \"n\": 1\n", actual);
            AssertReadsBack(actual, $"{{\"{longKey}\":\"v\",\"{atLimit}\":\"w\",\"{longKey}x\":{{\"n\":1}}}}");
        }

        [Fact]
        public void NonFiniteFloats_AreYamlFloats()
        {
            string actual = Block2(w =>
            {
                w.WriteStartArray();
                w.WriteNumberValue(double.NaN);
                w.WriteNumberValue(double.PositiveInfinity);
                w.WriteNumberValue(float.NegativeInfinity);
                w.WriteEndArray();
            });
            Assert.Equal("- .nan\n- .inf\n- -.inf\n", actual);
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(actual));
            reader.Read();
            reader.Read();
            Assert.True(double.IsNaN(reader.GetDouble()));
            reader.Read();
            Assert.Equal(double.PositiveInfinity, reader.GetDouble());
            reader.Read();
            Assert.Equal(double.NegativeInfinity, reader.GetDouble());
        }

        [Fact]
        public void NonFiniteFloats_StillThrowInJsonCompatibleStyle()
        {
            Assert.Throws<ArgumentException>(() => Write(w => w.WriteNumberValue(double.NaN), new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }));
        }

        [Fact]
        public void TypedValues_FixedForms()
        {
            // Dates and GUIDs plain, Base64 always quoted.
            Assert.Equal(
                "d: 2001-12-14T21:59:43Z\no: 2001-12-14T21:59:43-05:00\ng: 00000000-0000-0000-0000-000000000000\nb: \"AQID\"\ne: \"\"\n",
                Block2(w =>
                {
                    w.WriteStartObject();
                    w.WriteString("d", new DateTime(2001, 12, 14, 21, 59, 43, DateTimeKind.Utc));
                    w.WriteString("o", new DateTimeOffset(2001, 12, 14, 21, 59, 43, TimeSpan.FromHours(-5)));
                    w.WriteString("g", Guid.Empty);
                    w.WriteBase64String("b", new byte[] { 1, 2, 3 });
                    w.WriteBase64String("e", Array.Empty<byte>());
                    w.WriteEndObject();
                }));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scalars

        [Theory]
        [InlineData("app", "app")]
        [InlineData("true", "\"true\"")]
        [InlineData("tRue", "tRue")]
        [InlineData("yEs", "yEs")]
        [InlineData("NuLL", "NuLL")]
        [InlineData("True", "\"True\"")]
        [InlineData("YES", "\"YES\"")]
        [InlineData("Off", "\"Off\"")]
        [InlineData("12", "\"12\"")]
        [InlineData("0x1F", "\"0x1F\"")]
        [InlineData("1_000", "\"1_000\"")]
        [InlineData("190:20:30.15", "\"190:20:30.15\"")]
        [InlineData("2001-12-14", "\"2001-12-14\"")]
        [InlineData("NaN", "NaN")]
        [InlineData("Infinity", "Infinity")]
        [InlineData(".NaN", "\".NaN\"")]
        [InlineData("-.Inf", "\"-.Inf\"")]
        [InlineData("<<", "\"<<\"")]
        [InlineData("=", "\"=\"")]
        [InlineData("~", "\"~\"")]
        [InlineData("", "\"\"")]
        [InlineData("a: b", "\"a: b\"")]
        [InlineData("a #b", "\"a #b\"")]
        [InlineData("a:b", "a:b")]
        [InlineData("a#b", "a#b")]
        [InlineData("-v", "-v")]
        [InlineData("--port=8080", "--port=8080")]
        [InlineData("- a", "\"- a\"")]
        [InlineData("-", "\"-\"")]
        [InlineData(":x", "\":x\"")]
        [InlineData("?x", "?x")]
        [InlineData("C:\\x", "C:\\x")]
        [InlineData("a\"b", "a\"b")]
        [InlineData("\"a", "\"\\\"a\"")]
        [InlineData("héllo", "héllo")]
        [InlineData("a&b<c>'d'", "a&b<c>'d'")]
        [InlineData(" lead", "\" lead\"")]
        [InlineData("trail ", "\"trail \"")]
        [InlineData("tab\there", "\"tab\\there\"")]
        // M5f (decision 5): the default writes a string with a line break literal.
        [InlineData("line\nbreak", "|-\n  line\n  break")]
        [InlineData("a\u0085b", "\"a\\u0085b\"")]
        [InlineData("a\u2028b", "\"a\\u2028b\"")]
        [InlineData("\ufeffa", "\"\\uFEFFa\"")]
        [InlineData("a\uffffb", "\"a\\uFFFFb\"")]
        [InlineData("---x", "\"---x\"")]
        [InlineData("...", "\"...\"")]
        [InlineData("https://example.com/a?b=c", "https://example.com/a?b=c")]
        public void StringValue_PlainOrQuoted(string value, string expected)
        {
            string actual = Block2(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue(value);
                w.WriteEndArray();
            });
            Assert.Equal($"- {expected}\n", actual);
            AssertStringReadsBack(actual, value);
        }

        // QuoteForLenientReaders: what a reader resolves beyond the YAML 1.2 core schema
        // and the YAML 1.1 types is quoted only with the option; by default it is plain, as the standards read it.
        [Theory]
        [InlineData("tRue")]
        [InlineData("yEs")]
        [InlineData("NuLL")]
        [InlineData("oN")]
        [InlineData("NaN")]
        [InlineData("nan")]
        [InlineData("Infinity")]
        [InlineData("-inf")]
        [InlineData("+Infinity")]
        [InlineData(".iNf")]
        [InlineData("-.nan")]
        [InlineData("_1")]
        [InlineData("nan_")]
        [InlineData("0O17")]
        [InlineData("0X1F")]
        [InlineData("-0o17")]
        [InlineData("0o1_7")]
        [InlineData("0\u00A0")]
        [InlineData("\u3000x")]
        // Ruby's Psych: sexagesimal with a
        // leading zero, digits with commas, dates with a one-digit month or day, no digit before the exponent, and
        // Psych's timestamps.
        [InlineData("09:30")]
        [InlineData("00:15:00")]
        [InlineData("0:30")]
        [InlineData("+0:0")]
        [InlineData("0_:0")]
        [InlineData("-01:2:3")]
        [InlineData("1,000")]
        [InlineData("12,5")]
        [InlineData("0,.")]
        [InlineData("1,0.")]
        [InlineData("-1,0.5e+1")]
        [InlineData("0,x1F")]
        [InlineData("-,1")]
        [InlineData("2024-3-1")]
        [InlineData("5912-2-2")]
        [InlineData("0001-3-1")]
        [InlineData("2024-03-1")]
        [InlineData(".e+0")]
        [InlineData(".E-1")]
        [InlineData(".e+32001")]
        [InlineData("-.e-1")]
        // Psych's TIME beyond the YAML 1.1 timestamp (section 11, open point 1; section 12): a leading '-' and the zones
        // [-+]\d{3,4}. (Its zone [-+]\d{1,2}: ends the string with ':', which is quoted by default.)
        [InlineData("-2001-12-14 21:59:43")]
        [InlineData("-2001-12-14t21:59:43.10Z")]
        [InlineData("-0001-1-1T1:00:00-05:00")]
        [InlineData("2001-12-14 21:59:43+0500")]
        [InlineData("2001-12-14T21:59:43-530")]
        [InlineData("2001-12-14t21:59:43.5+050")]
        public void StringValue_QuotedForLenientReaders_OnlyWithTheOption(string value)
        {
            var lenient = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = true };
            string off = Block2(w => { w.WriteStartArray(); w.WriteStringValue(value); w.WriteEndArray(); });
            string on = Write(w => { w.WriteStartArray(); w.WriteStringValue(value); w.WriteEndArray(); }, lenient);
            Assert.Equal($"- {value}\n", off);
            Assert.Equal($"- \"{value.Replace("\u00A0", "\u00A0")}\"\n", on);
            AssertStringReadsBack(off, value);
            AssertStringReadsBack(on, value);
        }

        [Fact]
        public void QuoteForLenientReaders_KeysLiteralsAndFlow()
        {
            var lenient = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = true };
            void Calls(Utf8YamlWriter w)
            {
                w.WriteStartObject();
                w.WriteString("a *b", "v");
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteString("l", "x\n\ty");
                w.WritePropertyName("f");
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
                w.WriteStartArray();
                w.WriteStringValue("a:b");
                w.WriteStringValue("x?");
                w.WriteEndArray();
                w.WriteEndObject();
            }

            Assert.Equal("a *b: v\nl: |-\n  x\n  \ty\nf: [a:b, x?]\n", Block2(Calls));
            Assert.Equal("\"a *b\": v\nl: \"x\\n\\ty\"\nf: [\"a:b\", \"x?\"]\n", Write(Calls, lenient));
            string json = "{\"a *b\":\"v\",\"l\":\"x\\n\\ty\",\"f\":[\"a:b\",\"x?\"]}";
            AssertReadsBack(Block2(Calls), json);
            AssertReadsBack(Write(Calls, lenient), json);

            YamlWriterOptions options = default;
            Assert.False(options.QuoteForLenientReaders);
            Assert.False(new YamlSerializerOptions().QuoteForLenientReaders);
        }

        [Fact]
        public void Encoder_Explicit_ForcesQuotesOnWhatItEscapes()
        {
            var options = new YamlWriterOptions
            {
                Style = YamlWriterStyle.Block,
                NewLine = "\n",
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
            };
            string actual = Write(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue("héllo");
                w.WriteStringValue("a&b");
                w.WriteStringValue("plain");
                w.WriteStringValue("😀");
                w.WriteEndArray();
            }, options);
            Assert.Equal("- \"h\\u00E9llo\"\n- \"a\\u0026b\"\n- plain\n- \"\\uD83D\\uDE00\"\n", actual);
            AssertReadsBack(actual, "[\"héllo\",\"a&b\",\"plain\",\"😀\"]");
        }

        [Fact]
        public void Keys_FollowTheSameRules()
        {
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WriteString("true", "a");
                w.WriteString("1", "b");
                w.WriteString("a: b", "c");
                w.WriteString("héllo", "d");
                w.WriteString("<<", "e");
                w.WriteString("", "f");
                w.WriteEndObject();
            });
            Assert.Equal("\"true\": a\n\"1\": b\n\"a: b\": c\nhéllo: d\n\"<<\": e\n\"\": f\n", actual);
            AssertReadsBack(actual, "{\"true\":\"a\",\"1\":\"b\",\"a: b\":\"c\",\"héllo\":\"d\",\"<<\":\"e\",\"\":\"f\"}");
        }

        [Theory]
        [InlineData("echo a\necho b\n", "|\n  echo a\n  echo b\n")]
        [InlineData("echo a\necho b", "|-\n  echo a\n  echo b\n")]
        [InlineData("a\n\n", "|+\n  a\n\n")]
        [InlineData(" lead\nx", "|2-\n   lead\n  x\n")]
        [InlineData("\nx\n", "|\n\n  x\n")]
        [InlineData("\n a", "|2-\n\n   a\n")]
        [InlineData("tab\tinside\nx", "|-\n  tab\tinside\n  x\n")]
        // M5f: a first line that starts with a tab is double-quoted, since SharpYaml refuses it.
        [InlineData("\tx\ny", "\"\\tx\\ny\"\n")]
        [InlineData("x\n\ty", "|-\n  x\n  \ty\n")]
        [InlineData("single line", "single line\n")]
        [InlineData("crlf\r\nx", "\"crlf\\r\\nx\"\n")]
        [InlineData("only\n  \nspaces", "\"only\\n  \\nspaces\"\n")]
        [InlineData("\n\n", "\"\\n\\n\"\n")]
        public void Literal_OnRequest(string value, string expectedValue)
        {
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue(value);
                w.WriteEndObject();
            });
            Assert.Equal("k: " + expectedValue, actual);
            AssertReadsBack(actual, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["k"] = value }));
        }

        [Fact]
        public void Literal_InSequences_AndAtTheRoot()
        {
            string actual = Block2(w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue(" a\nb\n");
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteString("s", "x\ny");
                w.WriteEndObject();
                w.WriteEndArray();
            });
            Assert.Equal("- |2\n   a\n  b\n- s: |-\n    x\n    y\n", actual);
            AssertReadsBack(actual, "[\" a\\nb\\n\",{\"s\":\"x\\ny\"}]");

            string root = Block2(w =>
            {
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStringValue(" a\nb");
            });
            // A root literal that needs an indentation indicator is double-quoted (libyaml and the specification
            // disagree on what the indicator counts from at the root).
            Assert.Equal("\" a\\nb\"\n", root);
            AssertReadsBack(root, "\" a\\nb\"");
        }

        [Fact]
        public void Hints_DoubleQuotedAndPlain()
        {
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                w.WritePropertyName("k");
                w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                w.WriteStringValue("v");
                w.WriteScalarStyle(YamlScalarStyle.Plain);
                w.WriteString("p", "a: b");
                w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                w.WriteString("s", "x");
                w.WriteEndObject();
            });

            // Single-quoted from M4b (M4a wrote it double-quoted).
            Assert.Equal("\"k\": \"v\"\np: \"a: b\"\ns: 'x'\n", actual);
        }

        [Fact]
        public void Hints_WithoutAStringNode_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => Block2(w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteNumberValue(1);
            }));
            Assert.Throws<InvalidOperationException>(() => Block2(w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteStartObject();
            }));
            Assert.Throws<InvalidOperationException>(() => Block2(w =>
            {
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteScalarStyle(YamlScalarStyle.Literal);
            }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Block2(w => w.WriteScalarStyle(YamlScalarStyle.None)));

            // With SkipValidation the hint is dropped.
            Assert.Equal("- 1\n", Write(w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteNumberValue(1);
                w.WriteEndArray();
            }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", SkipValidation = true }));

            // In JSON-compatible style the hint is ignored.
            Assert.Equal("[1]", Write(w =>
            {
                w.WriteStartArray();
                w.WriteScalarStyle(YamlScalarStyle.Literal);
                w.WriteNumberValue(1);
                w.WriteEndArray();
            }, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }));
        }

        [Fact]
        public void StringSegments_AreDoubleQuoted()
        {
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("s");
                w.WriteStringValueSegment("ab".AsSpan(), false);
                w.WriteStringValueSegment("c\"d".AsSpan(), true);
                w.WritePropertyName("b");
                w.WriteBase64StringSegment(new byte[] { 1, 2 }, false);
                w.WriteBase64StringSegment(new byte[] { 3 }, true);
                w.WriteEndObject();
            });
            Assert.Equal("s: \"abc\\\"d\"\nb: \"AQID\"\n", actual);
            AssertReadsBack(actual, "{\"s\":\"abc\\\"d\",\"b\":\"AQID\"}");
        }

        [Fact]
        public void InvalidText_IsReplaced_AsInJsonCompatibleStyle()
        {
            // The JSON-compatible style's encoder writes an unpaired surrogate or an invalid UTF-8 sequence as U+FFFD; in
            // block style the shared scan's encoder (YamlBlockEncoder, M4b) and the block body do the same.
            foreach ((Action<Utf8YamlWriter> write, string expected) in new (Action<Utf8YamlWriter>, string)[]
            {
                (w => w.WriteStringValue("a\ud800b"), "a\ufffdb"),
                (w => w.WriteStringValue("a\udc00".AsSpan()), "a\ufffd"),
                (w => w.WriteStringValue(new byte[] { 0x61, 0xC3 }), "a\ufffd"),
                (w => w.WriteStringValue(new byte[] { 0xED, 0xA0, 0x80 }), "\ufffd\ufffd\ufffd"),

                // After non-ASCII text, before and after a quote (the scan's two halves).
                (w => w.WriteStringValue(new byte[] { 0xC3, 0xA9, 0xFF, 0x22 }), "\u00e9\ufffd\""),
                (w => w.WriteStringValue(new byte[] { 0xC3, 0xA9, 0x22, 0xFF }), "\u00e9\"\ufffd"),
                (w => w.WriteStringValue(new byte[] { 0xC3, 0xA9, 0x5C, 0xC3 }), "\u00e9\\\ufffd"),
            })
            {
                string block = Block2(write);
                string json = Write(write, default);
                Assert.Equal(new Token(YamlTokenType.String, expected), ReadTokens(block, YamlSchemaKind.Core).Single());
                Assert.Equal(ReadTokens(json, YamlSchemaKind.Core).Single(), ReadTokens(block, YamlSchemaKind.Core).Single());
            }
        }

        [Fact]
        public void Encoder_Unset_IsReportedUnset_AndNonAsciiIsWrittenAsItIs()
        {
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(buffer, Block);
            Assert.Null(writer.Options.Encoder);

            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WriteString("é", "日本語 \"q\" C:\\x");
                w.WritePropertyName("s");
                w.WriteStringValueSegment("hé".AsSpan(), false);
                w.WriteStringValueSegment("\n☃".AsSpan(), true);
                w.WriteEndObject();
            });
            Assert.Equal("é: 日本語 \"q\" C:\\x\ns: \"hé\\n☃\"\n", actual);

            // A quote or backslash after non-ASCII text, from UTF-8.
            Assert.Equal("- 日本 \"q\"\n- é\\x\n- \"é\\t\"\n", Block2(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue("日本 \"q\""u8);
                w.WriteStringValue("é\\x"u8);
                w.WriteStringValue("é\t"u8);
                w.WriteEndArray();
            }));
            AssertReadsBack(actual, "{\"é\":\"日本語 \\\"q\\\" C:\\\\x\",\"s\":\"hé\\n☃\"}");
        }

        // ---------------------------------------------------------------------------------------------------------
        // Raw values

        [Fact]
        public void RawValues_PlacedByKind()
        {
            string actual = Block2(w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("map");
                w.WriteRawValue("x: 1\ny: [1, 2]\n");
                w.WritePropertyName("flow");
                w.WriteRawValue("[1,\n 2]");
                w.WritePropertyName("scalar");
                w.WriteRawValue("'quoted' # comment");
                w.WriteStartArray("seq");
                w.WriteRawValue("- p\n- q");
                w.WriteRawValue("|\n  lit\n");
                w.WriteRawValue("a: 1\nb: 2");
                w.WriteEndArray();
                w.WriteEndObject();
            });
            Assert.Equal(
                "map:\n  x: 1\n  y: [1, 2]\nflow: [1,\n   2]\nscalar: 'quoted' # comment\nseq:\n- - p\n  - q\n- |\n    lit\n- a: 1\n  b: 2\n",
                actual);
            AssertReadsBack(actual, "{\"map\":{\"x\":1,\"y\":[1,2]},\"flow\":[1,2],\"scalar\":\"quoted\",\"seq\":[[\"p\",\"q\"],\"lit\\n\",{\"a\":1,\"b\":2}]}");
        }

        [Theory]
        [InlineData("# only a comment")]
        [InlineData("# before\n1")]
        [InlineData("--- 1")]
        [InlineData("%YAML 1.2\n--- 1")]
        [InlineData("|2\n  x")]
        public void RawValues_NotOneNode_Throw(string raw)
        {
            Assert.ThrowsAny<Exception>(() => Block2(w =>
            {
                w.WriteStartArray();
                w.WriteRawValue(raw);
            }));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 2 (10.3): read-back with token equality, and the differential oracle

        public static IEnumerable<object[]> Documents()
        {
            foreach (string name in new[] { "workflow-s", "compose-s", "k8s-s", "records-s", "workflow-m", "compose-m", "k8s-m", "openapi-m", "records-m" })
            {
                yield return new object[] { "bench:" + name };
            }

            for (int i = 0; i < JsonCorpus.Items.Count; i++)
            {
                yield return new object[] { "corpus:" + i };
            }
        }

        private static byte[]? Load(string id)
        {
            if (id.StartsWith("bench:", StringComparison.Ordinal))
            {
                return File.ReadAllBytes(Path.Combine(SequenceTests.BenchCorpus, id.Substring(6) + ".json"));
            }

            return JsonCorpus.Items[int.Parse(id.Substring(7))].Utf8;
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void TokenStream_ReadsBack_AndEqualsTheJsonCompatibleOutputsTokens(string id)
        {
            byte[] json = Load(id)!;
            List<Token>? source = JsonTokens.Read(json);
            if (source is null)
            {
                return; // not one valid JSON value (the corpus also holds rejected inputs)
            }

            foreach (YamlWriterOptions options in Variants)
            {
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    string block = Write(w => JsonTokens.Replay(source, w, rotation), options);
                    var jsonOptions = new YamlWriterOptions { Indented = options.Indented, NewLine = options.NewLine };
                    string jsonCompatible = Write(w => JsonTokens.Replay(source, w, rotation), jsonOptions);

                    List<Token> fromBlock = ReadTokens(block, YamlSchemaKind.Core);
                    Assert.True(Same(source, fromBlock), $"{id} (rotation {rotation}): block output reads back differently.\n{block}");
                    Assert.True(Same(source, ReadTokens(block, YamlSchemaKind.Yaml11)), $"{id} (rotation {rotation}): block output reads back differently under Yaml11.\n{block}");
                    Assert.True(Same(ReadTokens(jsonCompatible, YamlSchemaKind.Core), fromBlock), $"{id} (rotation {rotation}): the two styles differ.");
                }
            }
        }

        private static readonly YamlWriterOptions[] Variants =
        [
            new() { Style = YamlWriterStyle.Block, NewLine = "\n" },
            new() { Style = YamlWriterStyle.Block, NewLine = "\r\n", IndentSize = 4 },
            new() { Style = YamlWriterStyle.Block, NewLine = "\n", IndentSize = 9, IndentSequences = true },
            new() { Style = YamlWriterStyle.Block, NewLine = "\n", SkipValidation = true, IndentSequences = true, IndentSize = 3 },
        ];

        [Fact]
        public void EveryOverload_ReadsBack_AndEqualsTheJsonCompatibleOutputsTokens()
        {
            foreach (YamlWriterOptions options in Variants)
            {
                string block = Write(AllOverloads, options);
                string json = Write(AllOverloads, new YamlWriterOptions { NewLine = "\n" });
                Assert.True(Same(ReadTokens(json, YamlSchemaKind.Core), ReadTokens(block, YamlSchemaKind.Core)), block);
                Assert.True(Same(ReadTokens(json, YamlSchemaKind.Core), ReadTokens(block, YamlSchemaKind.Yaml11)), block);
            }
        }

        // One call of every public Write* member that writes a node, in every name form.
        internal static void AllOverloads(Utf8YamlWriter w)
        {
            YamlEncodedText E(string s) => YamlEncodedText.Encode(s);
            byte[] U(string s) => Encoding.UTF8.GetBytes(s);
            var date = new DateTime(2001, 12, 14, 21, 59, 43, DateTimeKind.Utc);
            var offset = new DateTimeOffset(date);
            var guid = new Guid("01234567-89ab-cdef-0123-456789abcdef");

            w.WriteStartObject();
            w.WriteString("s1", "true"); w.WriteString("s2".AsSpan(), "12".AsSpan()); w.WriteString(U("s3"), U("a: b")); w.WriteString(E("s4"), E("x&y"));
            w.WriteString("s5", "héllo".AsSpan()); w.WriteString("s6", U("C:\\x")); w.WriteString("s7", E("plain")); w.WriteString("s8".AsSpan(), "a\nb");
            w.WriteString(U("s9"), "q\"q"); w.WriteString(E("s10"), "~"); w.WriteString(E("s11"), "z".AsSpan()); w.WriteString(E("s12"), U("yes"));
            w.WriteString("s13".AsSpan(), U("-v")); w.WriteString("s14".AsSpan(), E("<<")); w.WriteString(U("s15"), "n".AsSpan()); w.WriteString(U("s16"), E("y"));
            w.WriteString("d1", date); w.WriteString("d2".AsSpan(), offset); w.WriteString(U("d3"), guid); w.WriteString(E("d4"), date);
            w.WriteString("d5", offset); w.WriteString("d6".AsSpan(), guid); w.WriteString(U("d7"), date); w.WriteString(E("d8"), guid);
            w.WriteNumber("n1", 1); w.WriteNumber("n2".AsSpan(), 2L); w.WriteNumber(U("n3"), 3u); w.WriteNumber(E("n4"), 4ul);
            w.WriteNumber("n5", 1.5); w.WriteNumber("n6".AsSpan(), 2.5f); w.WriteNumber(U("n7"), 3.5m); w.WriteNumber(E("n8"), -1.25);
            w.WriteNumber("n9", long.MinValue); w.WriteNumber("n10", ulong.MaxValue); w.WriteNumber(E("n11"), 1e300); w.WriteNumber(U("n12"), decimal.MaxValue);
            w.WriteBoolean("b1", true); w.WriteBoolean("b2".AsSpan(), false); w.WriteBoolean(U("b3"), true); w.WriteBoolean(E("b4"), false);
            w.WriteNull("z1"); w.WriteNull("z2".AsSpan()); w.WriteNull(U("z3")); w.WriteNull(E("z4"));
            w.WriteBase64String("x1", new byte[] { 1, 2, 3 }); w.WriteBase64String("x2".AsSpan(), new byte[] { 4 }); w.WriteBase64String(U("x3"), Array.Empty<byte>()); w.WriteBase64String(E("x4"), new byte[] { 0xFF });
            w.WritePropertyName("p1"); w.WriteStringValue("v");
            w.WritePropertyName("p2".AsSpan()); w.WriteStringValue("on".AsSpan());
            w.WritePropertyName(U("p3")); w.WriteStringValue(U("2001-12-14"));
            w.WritePropertyName(E("p4")); w.WriteStringValue(E("0x1F"));
            w.WritePropertyName("p5"); w.WriteStringValue(date);
            w.WritePropertyName("p6"); w.WriteStringValue(offset);
            w.WritePropertyName("p7"); w.WriteStringValue(guid);
            w.WritePropertyName("p8"); w.WriteNumberValue(1);
            w.WritePropertyName("p9"); w.WriteNumberValue(2L);
            w.WritePropertyName("p10"); w.WriteNumberValue(3u);
            w.WritePropertyName("p11"); w.WriteNumberValue(4ul);
            w.WritePropertyName("p12"); w.WriteNumberValue(1.5);
            w.WritePropertyName("p13"); w.WriteNumberValue(2.5f);
            w.WritePropertyName("p14"); w.WriteNumberValue(3.5m);
            w.WritePropertyName("p15"); w.WriteBooleanValue(true);
            w.WritePropertyName("p16"); w.WriteNullValue();
            w.WritePropertyName("p17"); w.WriteBase64StringValue(new byte[] { 1 });
            w.WritePropertyName("p18"); w.WriteStringValue((string?)null);
            w.WritePropertyName("p19"); w.WriteRawValue("[1, 2]");
            w.WritePropertyName("p20"); w.WriteRawValue("{\"a\": \"b\"}".AsSpan());
            w.WritePropertyName("p21"); w.WriteRawValue(U("3"));
            w.WritePropertyName("p22"); w.WriteRawValue(new ReadOnlySequence<byte>(U("\"x\"")));
            w.WritePropertyName("p23"); w.WriteStringValueSegment("se".AsSpan(), false); w.WriteStringValueSegment("g".AsSpan(), true);
            w.WritePropertyName("p24"); w.WriteStringValueSegment(U("u8"), false); w.WriteStringValueSegment(U("seg"), true);
            w.WriteStartArray("a1"); w.WriteEndArray(); w.WriteStartArray("a2".AsSpan()); w.WriteNumberValue(1); w.WriteEndArray();
            w.WriteStartArray(U("a3")); w.WriteStartArray(); w.WriteEndArray(); w.WriteEndArray(); w.WriteStartArray(E("a4")); w.WriteStartObject(); w.WriteEndObject(); w.WriteEndArray();
            w.WriteStartObject("o1"); w.WriteEndObject(); w.WriteStartObject("o2".AsSpan()); w.WriteNumber("k", 1); w.WriteEndObject();
            w.WriteStartObject(U("o3")); w.WriteStartObject("in"); w.WriteEndObject(); w.WriteEndObject(); w.WriteStartObject(E("o4")); w.WriteStartArray("in"); w.WriteStringValue("x"); w.WriteEndArray(); w.WriteEndObject();
            w.WriteEndObject();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers

        internal readonly record struct Token(YamlTokenType Type, string? Text);

        internal static List<Token> ReadTokens(string yaml, YamlSchemaKind schema)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = schema, MaxDepth = 1000 });
            var tokens = new List<Token>();
            while (reader.Read())
            {
                tokens.Add(reader.TokenType switch
                {
                    YamlTokenType.PropertyName or YamlTokenType.String => new Token(reader.TokenType, reader.GetString()),
                    YamlTokenType.Number => new Token(YamlTokenType.Number, Encoding.UTF8.GetString(reader.ValueSpan.ToArray())),
                    _ => new Token(reader.TokenType, null),
                });
            }

            return tokens;
        }

        // Numbers compare by value: the same value can have several texts (1.0 and 1, 1e2 and 100).
        internal static bool SameNumber(string a, string b)
        {
            const Globalization.NumberStyles Float = Globalization.NumberStyles.Float;
            Globalization.CultureInfo invariant = Globalization.CultureInfo.InvariantCulture;
            if (decimal.TryParse(a, Float, invariant, out decimal da) && decimal.TryParse(b, Float, invariant, out decimal db) && da == db)
            {
                return true;
            }

            // The replay writes some numbers through double, which keeps 17 significant digits. Out of double's range
            // (which .NET Framework's parser rejects), the text is written as it is.
            return double.TryParse(a, Float, invariant, out double xa) && double.TryParse(b, Float, invariant, out double xb)
                ? xa.Equals(xb)
                : a == b;
        }

        internal static bool Same(List<Token> expected, List<Token> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                Token e = expected[i];
                Token a = actual[i];
                if (e.Type == YamlTokenType.Number && a.Type == YamlTokenType.Number ? !SameNumber(e.Text!, a.Text!) : e != a)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void AssertReadsBack(string yaml, string json)
        {
            List<Token> expected = JsonTokens.Read(Encoding.UTF8.GetBytes(json))!;
            Assert.True(Same(expected, ReadTokens(yaml, YamlSchemaKind.Core)), $"Core: {yaml}");
            Assert.True(Same(expected, ReadTokens(yaml, YamlSchemaKind.Yaml11)), $"Yaml11: {yaml}");
        }

        internal static void AssertStringReadsBack(string yaml, string value)
        {
            foreach (YamlSchemaKind schema in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                List<Token> tokens = ReadTokens(yaml, schema);
                Assert.Equal(3, tokens.Count);
                Assert.Equal(new Token(YamlTokenType.String, value), tokens[1]);
            }
        }
    }

    /// <summary>The token stream of a JSON document (System.Text.Json's reader), and its replay through every writer overload family.</summary>
    internal static class JsonTokens
    {
        internal static List<BlockWriterTests.Token>? Read(byte[] json)
        {
            var tokens = new List<BlockWriterTests.Token>();
            try
            {
                var reader = new System.Text.Json.Utf8JsonReader(json, new System.Text.Json.JsonReaderOptions { MaxDepth = 1000 });
                while (reader.Read())
                {
                    tokens.Add(reader.TokenType switch
                    {
                        System.Text.Json.JsonTokenType.StartObject => new(YamlTokenType.StartObject, null),
                        System.Text.Json.JsonTokenType.EndObject => new(YamlTokenType.EndObject, null),
                        System.Text.Json.JsonTokenType.StartArray => new(YamlTokenType.StartArray, null),
                        System.Text.Json.JsonTokenType.EndArray => new(YamlTokenType.EndArray, null),
                        System.Text.Json.JsonTokenType.PropertyName => new(YamlTokenType.PropertyName, reader.GetString()),
                        System.Text.Json.JsonTokenType.String => new(YamlTokenType.String, reader.GetString()),
                        System.Text.Json.JsonTokenType.Number => new(YamlTokenType.Number, Encoding.UTF8.GetString(reader.ValueSpan.ToArray())),
                        System.Text.Json.JsonTokenType.True => new(YamlTokenType.True, null),
                        System.Text.Json.JsonTokenType.False => new(YamlTokenType.False, null),
                        System.Text.Json.JsonTokenType.Null => new(YamlTokenType.Null, null),
                        _ => throw new InvalidOperationException(),
                    });
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }

            return tokens.Count == 0 ? null : tokens;
        }

        // Replays the tokens, rotating through the name and value forms (string, UTF-16 span, UTF-8 span, pre-encoded)
        // and through the separate and combined name/value calls, so that every dispatch point is reached.
        internal static void Replay(List<BlockWriterTests.Token> tokens, Utf8YamlWriter w, int rotation)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                BlockWriterTests.Token t = tokens[i];
                int form = (i + rotation) % 4;
                switch (t.Type)
                {
                    case YamlTokenType.StartObject: w.WriteStartObject(); break;
                    case YamlTokenType.EndObject: w.WriteEndObject(); break;
                    case YamlTokenType.StartArray: w.WriteStartArray(); break;
                    case YamlTokenType.EndArray: w.WriteEndArray(); break;
                    case YamlTokenType.PropertyName:
                        BlockWriterTests.Token next = tokens[i + 1];
                        if ((i + rotation) % 3 == 0 && WriteCombined(w, t.Text!, next, form))
                        {
                            i++;
                            break;
                        }

                        switch (form)
                        {
                            case 0: w.WritePropertyName(t.Text!); break;
                            case 1: w.WritePropertyName(t.Text.AsSpan()); break;
                            case 2: w.WritePropertyName(Encoding.UTF8.GetBytes(t.Text!)); break;
                            default: w.WritePropertyName(YamlEncodedText.Encode(t.Text!)); break;
                        }

                        break;
                    default:
                        WriteValue(w, t, form);
                        break;
                }
            }
        }

        private static void WriteValue(Utf8YamlWriter w, BlockWriterTests.Token t, int form)
        {
            switch (t.Type)
            {
                case YamlTokenType.String:
                    switch (form)
                    {
                        case 0: w.WriteStringValue(t.Text); break;
                        case 1: w.WriteStringValue(t.Text.AsSpan()); break;
                        case 2: w.WriteStringValue(Encoding.UTF8.GetBytes(t.Text!)); break;
                        default: w.WriteStringValue(YamlEncodedText.Encode(t.Text!)); break;
                    }

                    break;
                case YamlTokenType.Number:
                    if (long.TryParse(t.Text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, out long l))
                    {
                        w.WriteNumberValue(l);
                    }
                    else if (decimal.TryParse(t.Text, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, out decimal d) && form % 2 == 0)
                    {
                        w.WriteNumberValue(d);
                    }
                    else if (double.TryParse(t.Text!, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, out double v) && !double.IsInfinity(v))
                    {
                        w.WriteNumberValue(v);
                    }
                    else
                    {
                        // Out of double's range: the text as it is.
                        w.WriteRawValue(t.Text!);
                    }

                    break;
                case YamlTokenType.True: w.WriteBooleanValue(true); break;
                case YamlTokenType.False: w.WriteBooleanValue(false); break;
                case YamlTokenType.Null: w.WriteNullValue(); break;
                default: throw new InvalidOperationException();
            }
        }

        private static bool WriteCombined(Utf8YamlWriter w, string name, BlockWriterTests.Token value, int form)
        {
            switch (value.Type)
            {
                case YamlTokenType.String:
                    switch (form)
                    {
                        case 0: w.WriteString(name, value.Text); break;
                        case 1: w.WriteString(name.AsSpan(), value.Text.AsSpan()); break;
                        case 2: w.WriteString(Encoding.UTF8.GetBytes(name), Encoding.UTF8.GetBytes(value.Text!)); break;
                        default: w.WriteString(YamlEncodedText.Encode(name), value.Text); break;
                    }

                    return true;
                case YamlTokenType.Number when long.TryParse(value.Text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, out long l):
                    switch (form)
                    {
                        case 0: w.WriteNumber(name, l); break;
                        case 1: w.WriteNumber(name.AsSpan(), l); break;
                        case 2: w.WriteNumber(Encoding.UTF8.GetBytes(name), l); break;
                        default: w.WriteNumber(YamlEncodedText.Encode(name), l); break;
                    }

                    return true;
                case YamlTokenType.True or YamlTokenType.False:
                    switch (form)
                    {
                        case 0: w.WriteBoolean(name, value.Type == YamlTokenType.True); break;
                        case 1: w.WriteBoolean(name.AsSpan(), value.Type == YamlTokenType.True); break;
                        case 2: w.WriteBoolean(Encoding.UTF8.GetBytes(name), value.Type == YamlTokenType.True); break;
                        default: w.WriteBoolean(YamlEncodedText.Encode(name), value.Type == YamlTokenType.True); break;
                    }

                    return true;
                case YamlTokenType.Null:
                    switch (form)
                    {
                        case 0: w.WriteNull(name); break;
                        case 1: w.WriteNull(name.AsSpan()); break;
                        case 2: w.WriteNull(Encoding.UTF8.GetBytes(name)); break;
                        default: w.WriteNull(YamlEncodedText.Encode(name)); break;
                    }

                    return true;
                case YamlTokenType.StartObject:
                    switch (form)
                    {
                        case 0: w.WriteStartObject(name); break;
                        case 1: w.WriteStartObject(name.AsSpan()); break;
                        case 2: w.WriteStartObject(Encoding.UTF8.GetBytes(name)); break;
                        default: w.WriteStartObject(YamlEncodedText.Encode(name)); break;
                    }

                    return true;
                case YamlTokenType.StartArray:
                    switch (form)
                    {
                        case 0: w.WriteStartArray(name); break;
                        case 1: w.WriteStartArray(name.AsSpan()); break;
                        case 2: w.WriteStartArray(Encoding.UTF8.GetBytes(name)); break;
                        default: w.WriteStartArray(YamlEncodedText.Encode(name)); break;
                    }

                    return true;
                default:
                    return false;
            }
        }
    }
}
