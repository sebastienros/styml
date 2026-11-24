// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A number read from a document is written back with its source text in block style, where the writer's schema reads
    /// that text to the same number (as JsonElement.WriteTo keeps a number's text), so a hash or a blob keeps its digits.
    /// The JSON-compatible style, IgnoreSourceStyles, a schema that would read the text differently, and an edited or
    /// created value write the value.
    /// </summary>
    public class NumberTextWriteBackTests
    {
        private static readonly string s_longRun = "0" + new string('7', 20_000);

        public static IEnumerable<object[]> Spellings() =>
            new[] { "0x1F", "0o14", "+1", "+12", "-0", ".5", "-.5", "+.5", "1.", "1.0", "1e3", "1E-3", "+1.5e+3", "012", "00",
                    "0777", ".inf", "-.Inf", "+.INF", ".nan", ".NaN", "18446744073709551616", "-9223372036854775809", s_longRun }
                .Select(s => new object[] { YamlSchemaKind.Core, s })
            .Concat(new[] { "0b101", "-0b11", "0b1_0", "1_000", "0x_1F", "1:30", "190:20:30", "-1:30", "012", "-012", "0o14", "+12",
                            ".5", "1_0.5", "0777.5", "1__0", "1_", s_longRun }
                .Select(s => new object[] { YamlSchemaKind.Yaml11, s }));

        private static string Block(object? value, YamlSchemaKind schema, bool ignoreSourceStyles = false) =>
            YamlSerializer.Serialize(value, new YamlSerializerOptions { Schema = schema, WriteStyle = YamlWriterStyle.Block, IgnoreSourceStyles = ignoreSourceStyles });

        [Theory]
        [MemberData(nameof(Spellings))]
        public void UnderTheDocumentsSchema_TheTextIsKept(YamlSchemaKind schema, string text)
        {
            string yaml = "a: " + text + "\nb: [" + text + "]\nc: {d: " + text + "}\n";
            using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { Schema = schema });
            Assert.Equal(YamlValueKind.Number, document.RootElement.GetProperty("a").ValueKind);

            // The document, an element, a node made from it, and an object read.
            Assert.Equal(yaml, Block(document, schema));
            Assert.Equal(text, Block(document.RootElement.GetProperty("a"), schema).TrimEnd());
            Assert.Equal(yaml, Block(YamlNode.Parse(yaml, documentOptions: new YamlDocumentOptions { Schema = schema }), schema));
            Assert.Equal(yaml, Block(YamlSerializer.Deserialize<object>(yaml, new YamlSerializerOptions { Schema = schema }), schema));

            // The fixed point: the written text reads to the same document and writes the same text.
            using YamlDocument again = YamlDocument.Parse(Block(document, schema), new YamlDocumentOptions { Schema = schema });
            Assert.True(YamlElement.DeepEquals(document.RootElement, again.RootElement));
            Assert.Equal(yaml, Block(again, schema));
        }

        [Theory]
        [MemberData(nameof(Spellings))]
        public void InJsonOrIgnoringSourceStyles_TheValueIsWritten(YamlSchemaKind schema, string text)
        {
            if (text == s_longRun)
            {
                // Its value is past double's range: written by value it would be infinity, which JSON cannot hold. Only
                // the text keeps it (UnderTheDocumentsSchema_TheTextIsKept).
                return;
            }

            string yaml = "a: " + text + "\n";
            using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { Schema = schema });
            YamlElement a = document.RootElement.GetProperty("a");
            bool isJsonNumber = Text.RegularExpressions.Regex.IsMatch(text, @"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][-+]?[0-9]+)?$");
            bool notFinite = text.TrimStart('+', '-').StartsWith(".", StringComparison.Ordinal) && char.IsLetter(text.TrimStart('+', '-')[1]);

            string ignoring = Block(a, schema, ignoreSourceStyles: true).TrimEnd();
            using (YamlDocument back = YamlDocument.Parse("a: " + ignoring + "\n", new YamlDocumentOptions { Schema = schema }))
            {
                Assert.True(YamlElement.DeepEquals(a, back.RootElement.GetProperty("a")), ignoring);
            }

            if (notFinite)
            {
                // .inf and .nan are their own value's text; JSON has no spelling for them.
                Assert.Equal(text.ToLowerInvariant().TrimStart('+'), ignoring);
                return;
            }

            string json = YamlSerializer.Serialize(a, new YamlSerializerOptions { Schema = schema, WriteStyle = YamlWriterStyle.JsonCompatible });
            Assert.True(isJsonNumber == (json == text), json);
            Assert.True(isJsonNumber == (ignoring == text), ignoring);
            using YamlDocument fromJson = YamlDocument.Parse("a: " + json + "\n", new YamlDocumentOptions { Schema = schema });
            Assert.True(YamlElement.DeepEquals(a, fromJson.RootElement.GetProperty("a")), json);
        }

        [Fact]
        public void UnderAnotherSchema_TheTextIsKeptOnlyWhereItReadsTheSame()
        {
            using YamlDocument yaml11 = YamlDocument.Parse("a: 0x1F\nb: 012\nc: 0b101\nd: 1_000\ne: 08\n", new YamlDocumentOptions { Schema = YamlSchemaKind.Yaml11 });

            // The core schema reads 0x1F and 08 to the same numbers, 012 to 12 (not 10), and 0b101 and 1_000 as strings.
            Assert.Equal("a: 0x1F\nb: 10\nc: 5\nd: 1000\ne: 08\n", Block(yaml11, YamlSchemaKind.Core));

            // The JSON schema reads only JSON's numbers, and failsafe none.
            Assert.Equal("a: 31\nb: 10\nc: 5\nd: 1000\ne: 8\n", Block(yaml11, YamlSchemaKind.Json));
            Assert.Equal("a: 31\nb: 10\nc: 5\nd: 1000\ne: 8\n", Block(yaml11, YamlSchemaKind.Failsafe));

            // A Utf8YamlWriter made by the caller writes for the core schema.
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                yaml11.WriteTo(writer);
            }

            Assert.Equal("a: 0x1F\nb: 10\nc: 5\nd: 1000\ne: 08\n", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
        }

        [Fact]
        public void AnEditedOrCreatedValue_IsWrittenByValue()
        {
            YamlNode node = YamlNode.Parse("a: 0x1F\nb: 0o14\n")!;
            node["b"] = 12;
            node["c"] = YamlValue.Create(0x1F);
            Assert.Equal("a: 0x1F\nb: 12\nc: 31\n", Block(node, YamlSchemaKind.Core));

            // A typed read writes the type's value.
            Assert.Equal("a: 31\n", Block(YamlSerializer.Deserialize<Dictionary<string, int>>("a: 0x1F\n"), YamlSchemaKind.Core));
        }

        [Fact]
        public void GettersAndEquality_StayByValue()
        {
            using YamlDocument document = YamlDocument.Parse("a: 0x1F\nb: 31\n");
            Assert.Equal(31, document.RootElement.GetProperty("a").GetInt32());
            Assert.True(YamlElement.DeepEquals(document.RootElement.GetProperty("a"), document.RootElement.GetProperty("b")));
            Assert.Equal("0x1F", document.RootElement.GetProperty("a").GetRawText());
        }
    }
}
