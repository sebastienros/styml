// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5c: decision 2, empty input reads as a null under every schema
    ///; and <see cref="YamlSerializerOptions.Schema"/> with the JSON and failsafe schemas.
    /// </summary>
    public class EmptyInputAndSchemaTests
    {
        private static readonly YamlSchemaKind[] s_allSchemas = { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe };

        public class C { public string? A { get; set; } }

        public class Typed
        {
            public int I { get; set; }
            public bool B { get; set; }
            public int? N { get; set; }
            public string? S { get; set; } = "unset";
        }

        public static IEnumerable<object[]> Schemas() =>
            s_allSchemas.Select(k => new object[] { k });

        public static IEnumerable<object[]> EmptyInputs() =>
        [
            [""],
            ["  \n\n"],
            ["# only a comment\n"],
            ["﻿"],
        ];

        private static async Task<int> Count(IAsyncEnumerable<int?> items)
        {
            int n = 0;
            await foreach (int? _ in items)
            {
                n++;
            }

            return n;
        }

        [Theory]
        [MemberData(nameof(EmptyInputs))]
        public async Task EmptyInput_IsANullOnEveryPath(string input)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(input);
            foreach (YamlSchemaKind schema in s_allSchemas)
            {
                var options = new YamlSerializerOptions { Schema = schema };
                Assert.Null(YamlSerializer.Deserialize<string>(input, options));
                Assert.Null(YamlSerializer.Deserialize<C>(input, options));
                Assert.Null(YamlSerializer.Deserialize<int?>(input, options));
                Assert.Null(YamlSerializer.Deserialize<object>(input, options));
                Assert.Null(YamlSerializer.Deserialize<List<int>>(input, options));
                Assert.Null(YamlSerializer.Deserialize<C>(utf8.AsSpan(), options));
                Assert.Null(YamlSerializer.Deserialize<C>(new MemoryStream(utf8), options));
                Assert.Null(await YamlSerializer.DeserializeAsync<C>(new MemoryStream(utf8), options));
                Assert.Null(await YamlSerializer.DeserializeAsync<C>(PipeReader.Create(new ReadOnlySequence<byte>(utf8)), options));
                Assert.Null(await YamlSerializer.DeserializeAsync<C>(new MemoryStream(utf8), new YamlSerializerOptions(options) { DefaultBufferSize = 1 }));

                var reader = new Utf8YamlReader(utf8);
                Assert.Null(YamlSerializer.Deserialize<C>(ref reader, options));

                // A non-nullable value type gets the null error, or default under AcceptLenientScalars.
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<int>(input, options));
                Assert.Equal(0, YamlSerializer.Deserialize<int>(input, new YamlSerializerOptions(options) { AcceptLenientScalars = true }));

                // The streaming reads: a root enumerable reads as null (no items); a stream of top-level values has no
                // document, so no item.
                Assert.Equal(0, await Count(YamlSerializer.DeserializeAsyncEnumerable<int?>(new MemoryStream(utf8), options)));
                Assert.Equal(0, await Count(YamlSerializer.DeserializeAsyncEnumerable<int?>(new MemoryStream(utf8), topLevelValues: true, options)));

                Assert.Null(YamlSerializer.Deserialize<YamlNode>(input, options));
                Assert.Null(YamlSerializer.Deserialize<YamlElement?>(input, options));
                Assert.Equal(YamlValueKind.Null, YamlSerializer.Deserialize<YamlElement>(input, options).ValueKind);
                using YamlDocument document = YamlSerializer.Deserialize<YamlDocument>(input, options)!;
                Assert.Equal(YamlValueKind.Null, document.RootElement.ValueKind);
            }

            // The DOM keeps throwing (as ruled); a node is null.
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(input));
            Assert.Null(YamlNode.Parse(input));
            Assert.Null(YamlNode.Parse(utf8.AsSpan()));
            Assert.Null(YamlNode.Parse(new MemoryStream(utf8)));
            Assert.Null(await YamlNode.ParseAsync(new MemoryStream(utf8)));
        }

        [Fact]
        public void TheExplicitEmptyDocument_ResolvesByTheSchema()
        {
            Assert.Null(YamlSerializer.Deserialize<string>("---\n"));
            Assert.Null(YamlSerializer.Deserialize<string>("---\n", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 }));
            Assert.Equal("", YamlSerializer.Deserialize<string>("---\n", new YamlSerializerOptions { Schema = YamlSchemaKind.Failsafe }));
            Assert.Contains("JSON schema", Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<string>("---\n", new YamlSerializerOptions { Schema = YamlSchemaKind.Json })).Message);
        }

        [Fact]
        public void TheReaderUnderEachSchema()
        {
            const string Scalars = "[true, True, yes, 1, 0x1F, 1.5, 1e3, null, ~, '1', foo]";
            Assert.Equal("True True String Number Number Number Number Null Null String String", TokenTypes(Scalars, YamlSchemaKind.Core));
            Assert.Equal("True True True Number Number Number Number Null Null String String", TokenTypes(Scalars, YamlSchemaKind.Yaml11));
            Assert.Equal("String String String String String String String String String String String", TokenTypes(Scalars, YamlSchemaKind.Failsafe));
            Assert.Equal("True Number Number Number Null String", TokenTypes("[true, 1, -0, 1.5e3, null, '1']", YamlSchemaKind.Json));
            Assert.Equal("String", TokenTypes("[\"\"]", YamlSchemaKind.Json));

            // Under Json every other untagged plain scalar is an error (YAML 1.2.2, 10.2.2), the empty one included.
            foreach (string scalar in new[] { "True", "yes", "~", "0x1F", "+1", "01", ".inf", "1_000", "foo", "a b" })
            {
                Assert.Contains("JSON schema", Assert.ThrowsAny<YamlException>(() => TokenTypes($"[{scalar}]", YamlSchemaKind.Json)).Message);
            }

            Assert.ThrowsAny<YamlException>(() => TokenTypes("x:", YamlSchemaKind.Json));
            Assert.Equal("String", TokenTypes("[!!str ]", YamlSchemaKind.Json));

            // Tags keep their meaning, and keys are not resolved, under every schema.
            Assert.Equal("Number Null", TokenTypes("[!!int 1, !!null '']", YamlSchemaKind.Failsafe));
            Assert.Equal("StartObject PropertyName Number EndObject", TokenTypes("foo: 1", YamlSchemaKind.Json).Trim());

            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlReaderOptions { Schema = (YamlSchemaKind)4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { Schema = (YamlSchemaKind)4 });
        }

        private static string TokenTypes(string yaml, YamlSchemaKind schema)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = schema });
            var types = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType is not (YamlTokenType.StartArray or YamlTokenType.EndArray))
                {
                    types.Add(reader.TokenType.ToString());
                }
            }

            return string.Join(" ", types);
        }

        public static IEnumerable<object[]> UnmatchedUnderJson() =>
            new[] { "yes", "True", "~", "0x1F", "foo", "1_000", ".inf" }.Select(v => new object[] { v });

        [Theory]
        [MemberData(nameof(UnmatchedUnderJson))]
        public async Task Json_AcceptLenientScalarsReadsUnmatchedPlainScalarsAsStrings(string scalar)
        {
            // With AcceptLenientScalars, SharpYaml's reading. 1_000 and .inf stay
            // strings (SharpYaml's numbers are not copied).
            var strict = new YamlSerializerOptions { Schema = YamlSchemaKind.Json };
            var lenient = new YamlSerializerOptions { Schema = YamlSchemaKind.Json, AcceptLenientScalars = true };
            var stream = new YamlSerializerOptions(lenient) { DefaultBufferSize = 1 };
            foreach (string yaml in new[] { $"x: {scalar}", $"[{scalar}]", $"- {scalar}\n- 1" })
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>(yaml, strict));
                object read = YamlSerializer.Deserialize<object>(yaml, lenient)!;
                YamlElement element = (YamlElement)read;
                YamlElement value = element.ValueKind == YamlValueKind.Object ? element.GetProperty("x") : element[0];
                Assert.Equal(YamlValueKind.String, value.ValueKind);
                Assert.Equal(scalar, value.GetString());
                YamlElement fromStream = (YamlElement)(await YamlSerializer.DeserializeAsync<object>(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), stream))!;
                Assert.Equal(element.GetRawText(), fromStream.GetRawText());
            }

            Assert.Equal(scalar, YamlSerializer.Deserialize<Typed>($"S: {scalar}", lenient)!.S);
            Assert.Equal(scalar, (await YamlSerializer.DeserializeAsync<Typed>(new MemoryStream(Encoding.UTF8.GetBytes($"S: {scalar}")), stream))!.S);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>($"S: {scalar}", strict));

            // The other lenient rules still apply: a superset of SharpYaml (True into a bool, which its Json schema refuses).
            if (scalar == "True")
            {
                Assert.True(YamlSerializer.Deserialize<Typed>("B: True", lenient)!.B);
            }
            else
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>($"I: {scalar}", lenient));
            }
        }

        [Fact]
        public void Json_AcceptLenientScalars_TheEmptyAndMultiLineScalars_AndTheReader()
        {
            var lenient = new YamlSerializerOptions { Schema = YamlSchemaKind.Json, AcceptLenientScalars = true };
            Assert.Equal("", YamlSerializer.Deserialize<Dictionary<string, string>>("x:", lenient)!["x"]);
            Assert.Equal("", YamlSerializer.Deserialize<string>("---\n", lenient));
            Assert.Equal("a b", YamlSerializer.Deserialize<Dictionary<string, string>>("x: a\n  b", lenient)!["x"]);
            Assert.Null(YamlSerializer.Deserialize<string>("", lenient)); // zero tokens: no document

            var reader = new Utf8YamlReader("[foo, 1, null]"u8, new YamlReaderOptions { Schema = YamlSchemaKind.Json, AcceptLenientScalars = true });
            var types = new List<YamlTokenType>();
            while (reader.Read())
            {
                types.Add(reader.TokenType);
            }

            Assert.Equal(new[] { YamlTokenType.StartArray, YamlTokenType.String, YamlTokenType.Number, YamlTokenType.Null, YamlTokenType.EndArray }, types);

            // YamlDocument.Parse has no lenient option: it keeps the error.
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse("a: foo", new YamlDocumentOptions { Schema = YamlSchemaKind.Json }));
        }

        [Fact]
        public void TheNewClauseLeavesCoreAndYaml11Unchanged()
        {
            const string Scalars = "[true, True, yes, 1, 0x1F, 1.5, null, ~, foo, '1']";
            foreach (YamlSchemaKind schema in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                Assert.Equal(TokenTypes(Scalars, schema), LenientTokenTypes(Scalars, schema));
            }

            static string LenientTokenTypes(string yaml, YamlSchemaKind schema)
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = schema, AcceptLenientScalars = true });
                var types = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType is not (YamlTokenType.StartArray or YamlTokenType.EndArray))
                    {
                        types.Add(reader.TokenType.ToString());
                    }
                }

                return string.Join(" ", types);
            }
        }

        public static IEnumerable<object[]> SchemasAndLeniency() =>
            s_allSchemas.SelectMany(k => new[] { new object[] { k, false }, new object[] { k, true } });

        private static void AssertSameValues(Dictionary<string, object> expected, Dictionary<string, object> actual)
        {
            Assert.Equal(expected.Keys.OrderBy(k => k), actual.Keys.OrderBy(k => k));
            foreach (string key in expected.Keys)
            {
                if (expected[key] is null || actual[key] is null)
                {
                    Assert.True(expected[key] is null && actual[key] is null, key);
                    continue;
                }

                YamlElement e = (YamlElement)expected[key], a = (YamlElement)actual[key];
                Assert.True(YamlElement.DeepEquals(e, a), $"{key}: {e.GetRawText()} / {a.GetRawText()}");
            }
        }

        [Theory]
        [MemberData(nameof(SchemasAndLeniency))]
        public void Rewrites_ReadAsTheHandExpandedDocument(YamlSchemaKind schema, bool lenient)
        {
            // A defect found in review: text the product rewrites and reads again gives, under the
            // operation's schema and options, the kinds and values the source gave. A quoted string stays quoted.
            string body = "{x: 1, s: \"q\", t: !!int 2, n: !!null ~, l: 'y'" + (schema == YamlSchemaKind.Yaml11 ? ", b: yes" : "")
                + (schema != YamlSchemaKind.Json || lenient ? ", p: foo" : "") + "}";
            var options = new YamlSerializerOptions { Schema = schema, AcceptLenientScalars = lenient };
            var merge = new YamlSerializerOptions(options) { ResolveMergeKeys = true };

            // The merge rewrite.
            var merged = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>($"b: &b {body}\nc: {{<<: *b, u: \"w\"}}", merge)!;
            var handMerged = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>($"b: {body}\nc: {body.TrimEnd('}')}, u: \"w\"}}", options)!;
            AssertSameValues(handMerged["c"], merged["c"]);

            // The alias expansion at an object site (no <<, no option).
            var expanded = YamlSerializer.Deserialize<Dictionary<string, object>>($"b: &b {body}\nc: {{k: *b}}", options)!;
            var handExpanded = YamlSerializer.Deserialize<Dictionary<string, object>>($"b: {body}\nc: {{k: {body}}}", options)!;
            Assert.True(YamlElement.DeepEquals((YamlElement)handExpanded["c"], (YamlElement)expanded["c"]), ((YamlElement)expanded["c"]).GetRawText());

            // The DOM's merge too.
            var dom = new YamlDocumentOptions { Schema = schema, ResolveMergeKeys = true };
            if (!lenient && (schema != YamlSchemaKind.Json || !body.Contains("foo")))
            {
                using YamlDocument document = YamlDocument.Parse($"b: &b {body}\nc: {{<<: *b, u: \"w\"}}", dom);
                using YamlDocument hand = YamlDocument.Parse($"b: {body}\nc: {body.TrimEnd('}')}, u: \"w\"}}", new YamlDocumentOptions { Schema = schema });
                foreach (YamlProperty property in hand.RootElement.GetProperty("c").EnumerateObject())
                {
                    Assert.True(YamlElement.DeepEquals(property.Value, document.RootElement.GetProperty("c").GetProperty(property.Name)), property.Name);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Schemas))]
        public void TaggedScalars_AsTheirOwnObject_KeepTheirTagsMeaning(YamlSchemaKind schema)
        {
            // Defect 2 of the verification: each item read as its own object gives the kind the whole array's element
            // gives (recorded choice 3: under Failsafe, explicit core tags keep their meaning).
            const string Yaml = "[!!int 1, !!bool true, !!null ~, !!float 1.5, !!str 2, !!int 0x1F]";
            var options = new YamlSerializerOptions { Schema = schema };
            List<object?> items = YamlSerializer.Deserialize<List<object?>>(Yaml, options)!;
            YamlElement whole = (YamlElement)YamlSerializer.Deserialize<object>(Yaml, options)!;
            YamlValueKind[] expected = { YamlValueKind.Number, YamlValueKind.True, YamlValueKind.Null, YamlValueKind.Number, YamlValueKind.String, YamlValueKind.Number };
            Assert.Equal(expected, whole.EnumerateArray().Select(e => e.ValueKind));
            Assert.Equal(expected, items.Select(i => i is null ? YamlValueKind.Null : ((YamlElement)i).ValueKind));
            Assert.Equal(1, ((YamlElement)items[0]!).GetInt32());
            Assert.Equal(31, ((YamlElement)items[5]!).GetInt32());
            Assert.Equal("2", ((YamlElement)items[4]!).GetString());

            // And re-read as a typed value, with the element's own document.
            Assert.Equal(1, YamlSerializer.Deserialize<int>((YamlElement)items[0]!, options));
            Assert.True(YamlSerializer.Deserialize<bool>((YamlElement)items[1]!, options));
        }

        [Fact]
        public void TypedSitesUnderFailsafe()
        {
            // SharpYaml's Failsafe rows (evidence, "schemas at typed sites"), and Styml's tags and keys.
            var failsafe = new YamlSerializerOptions { Schema = YamlSchemaKind.Failsafe };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("I: 1", failsafe));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("B: true", failsafe));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("N: ~", failsafe));
            Assert.Equal("~", YamlSerializer.Deserialize<Typed>("S: ~", failsafe)!.S);
            Assert.Equal("", YamlSerializer.Deserialize<Typed>("S:", failsafe)!.S);
            Assert.Equal(new object[] { "yes", "1", "~" }, YamlSerializer.Deserialize<List<object>>("[yes, 1, ~]", failsafe)!.Select(e => (object)((YamlElement)e).GetString()!));
            Assert.Equal(1, YamlSerializer.Deserialize<Typed>("I: !!int 1", failsafe)!.I);
            Assert.Equal("a", YamlSerializer.Deserialize<Dictionary<int, string>>("1: a", failsafe)![1]);

            // Under AcceptLenientScalars the lenient boolean rule takes a plain string; the lenient number reader
            // skips plain untagged scalars, so 1 still throws.
            var lenient = new YamlSerializerOptions(failsafe) { AcceptLenientScalars = true };
            Assert.True(YamlSerializer.Deserialize<Typed>("B: true", lenient)!.B);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("I: 1", lenient));
        }

        [Fact]
        public void TypedSitesUnderJson()
        {
            var json = new YamlSerializerOptions { Schema = YamlSchemaKind.Json };
            Typed typed = YamlSerializer.Deserialize<Typed>("{I: 1, B: true, N: null, S: \"s\"}", json)!;
            Assert.Equal((1, true, (int?)null, "s"), (typed.I, typed.B, typed.N, typed.S));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("S: foo", json));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>("B: True", json));

            // JSON-compatible output reads back under the JSON schema; block output's plain strings do not.
            var value = new Typed { I = 2, B = true, N = null, S = "foo" };
            string jsonText = YamlSerializer.Serialize(value, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            Assert.Equal("foo", YamlSerializer.Deserialize<Typed>(jsonText, json)!.S);
            string block = YamlSerializer.Serialize(value, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block });
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>(block, json));

            // The DOM too.
            Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse("a: foo", new YamlDocumentOptions { Schema = YamlSchemaKind.Json }));
        }

        [Theory]
        [MemberData(nameof(Schemas))]
        public void ReReads_TakeCoreOrTheDocumentsSchema(YamlSchemaKind schema)
        {
            // 5.3, C9: a node is the writer's text, read back as Core; an element or a document reads as its document
            // read, whatever the serializer's schema.
            var options = new YamlSerializerOptions { Schema = schema };
            YamlNode node = YamlNode.Parse("{I: 1, B: true, S: foo}")!;
            Typed fromNode = YamlSerializer.Deserialize<Typed>(node, options)!;
            Assert.Equal((1, true, "foo"), (fromNode.I, fromNode.B, fromNode.S));

            using YamlDocument core = YamlDocument.Parse("{I: 1, B: true, S: foo}");
            Typed fromElement = YamlSerializer.Deserialize<Typed>(core.RootElement, options)!;
            Assert.Equal((1, true, "foo"), (fromElement.I, fromElement.B, fromElement.S));
            Assert.Equal(1, YamlSerializer.Deserialize<Typed>(core, options)!.I);

            using YamlDocument failsafe = YamlDocument.Parse("{I: 1, S: 1}", new YamlDocumentOptions { Schema = YamlSchemaKind.Failsafe });
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>(failsafe.RootElement, options));
            Assert.Equal("1", YamlSerializer.Deserialize<string>(failsafe.RootElement.GetProperty("S"), options));
            Assert.Equal("1", YamlSerializer.Deserialize<string>(failsafe.RootElement.Clone().GetProperty("S"), options));

            // The serializer's duplicate handling still applies to the re-read.
            using YamlDocument repeated = YamlDocument.Parse("{S: a, S: b}", new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins });
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Typed>(repeated.RootElement, options));
            Assert.Equal("a", YamlSerializer.Deserialize<Typed>(repeated.RootElement, new YamlSerializerOptions(options) { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins })!.S);
        }
    }
}
