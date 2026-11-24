// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Integers with a leading zero. YAML 1.1 reads <c>012</c> as octal (10), the core schema as decimal (12), and
    /// <c>0o14</c> is octal in both. Every read of a document, an element or a node gives the typed read's value:
    /// the JSON number parser, which reads <c>012</c> as decimal, is not used for such a number under YAML 1.1.
    /// An element writes its text back where the writer's schema reads it to the same number, and the value elsewhere.
    /// </summary>
    public class LeadingZeroIntegerTests
    {
        public static IEnumerable<object[]> Forms() => new[]
        {
            new object[] { YamlSchemaKind.Core, "012", 12L },
            new object[] { YamlSchemaKind.Core, "-012", -12L },
            new object[] { YamlSchemaKind.Core, "+012", 12L },
            new object[] { YamlSchemaKind.Core, "00", 0L },
            new object[] { YamlSchemaKind.Core, "08", 8L },
            new object[] { YamlSchemaKind.Core, "09", 9L },
            new object[] { YamlSchemaKind.Core, "0o14", 12L },
            new object[] { YamlSchemaKind.Core, "0x1F", 31L },
            new object[] { YamlSchemaKind.Yaml11, "012", 10L },
            new object[] { YamlSchemaKind.Yaml11, "-012", -10L },
            new object[] { YamlSchemaKind.Yaml11, "+012", 10L },
            new object[] { YamlSchemaKind.Yaml11, "0_12", 10L },
            new object[] { YamlSchemaKind.Yaml11, "00", 0L },
            // 08 and 09 are not octal: YAML 1.1 reads them as decimal, as SharpYaml's default schema does
            // (FeatureParityTests.LeadingZeroIntegers).
            new object[] { YamlSchemaKind.Yaml11, "08", 8L },
            new object[] { YamlSchemaKind.Yaml11, "09", 9L },
            new object[] { YamlSchemaKind.Yaml11, "0o14", 12L },
            new object[] { YamlSchemaKind.Yaml11, "0x1F", 31L },
        };

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

        [Theory]
        [MemberData(nameof(Forms))]
        public void TheElementReadsTheTypedValue(YamlSchemaKind schema, string scalar, long expected)
        {
            var options = new YamlSerializerOptions { Schema = schema };
            string yaml = "a: " + scalar + "\n";
            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, long>>(yaml, options)!["a"]);

            using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { Schema = schema });
            YamlElement element = document.RootElement.GetProperty("a");
            Assert.Equal(YamlValueKind.Number, element.ValueKind);
            Assert.Equal(scalar, element.GetRawText());
            Assert.Equal(expected, element.GetInt64());
            Assert.Equal((int)expected, element.GetInt32());
            Assert.Equal((short)expected, element.GetInt16());
            Assert.Equal(expected, element.GetDouble());
            Assert.Equal(expected, element.GetSingle());
            Assert.Equal(expected, element.GetDecimal());
            Assert.Equal(expected >= 0, element.TryGetUInt64(out ulong unsigned));
            Assert.Equal(expected >= 0, element.TryGetByte(out byte b));
            if (expected >= 0)
            {
                Assert.Equal((ulong)expected, unsigned);
                Assert.Equal(expected, b);
            }

            Assert.Equal(expected, element.Deserialize<long>(options));
            Assert.Equal(expected, element.Clone().GetInt64());

            // The element keeps the document's schema whatever the options say.
            var other = new YamlSerializerOptions { Schema = schema == YamlSchemaKind.Core ? YamlSchemaKind.Yaml11 : YamlSchemaKind.Core };
            Assert.Equal(expected, element.Deserialize<long>(other));
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void NodesReadTheTypedValue(YamlSchemaKind schema, string scalar, long expected)
        {
            var options = new YamlSerializerOptions { Schema = schema };
            var documentOptions = new YamlDocumentOptions { Schema = schema };
            string yaml = "a: " + scalar + "\n";

            YamlNode parsed = YamlNode.Parse(yaml, documentOptions: documentOptions)!["a"]!;
            Assert.Equal(expected, parsed.GetValue<long>());
            Assert.Equal(expected, parsed.Deserialize<long>(options));
            Assert.Equal(expected, YamlNode.Parse(scalar, documentOptions: documentOptions)!.GetValue<long>());

            using YamlDocument document = YamlDocument.Parse(yaml, documentOptions);
            Assert.Equal(expected, YamlValue.Create(document.RootElement.GetProperty("a"))!.GetValue<long>());

            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, YamlNode>>(yaml, options)!["a"]!.GetValue<long>());
            Assert.Equal(expected, YamlSerializer.Deserialize<YamlNode>(scalar, options)!.GetValue<long>());
            Assert.Equal(expected, YamlSerializer.Deserialize<YamlValue>(scalar, options)!.GetValue<long>());
            Assert.Equal(expected, YamlSerializer.Deserialize<YamlValue>(scalar, options)!.GetValue<double>());
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void AnObjectReadsTheTypedValue(YamlSchemaKind schema, string scalar, long expected)
        {
            var options = new YamlSerializerOptions { Schema = schema };
            object value = YamlSerializer.Deserialize<Dictionary<string, object>>("a: " + scalar + "\n", options)!["a"];
            Assert.Equal(expected, Assert.IsType<YamlElement>(value).GetInt64());
            Assert.Equal(expected, ((YamlElement)YamlSerializer.Deserialize<object>(scalar, options)!).GetInt64());

            // As a key.
            Assert.Equal(expected, Assert.Single(YamlSerializer.Deserialize<Dictionary<long, string>>(scalar + ": x\n", options)!).Key);
        }

        // The number the text reads to under `schema`, or null when it is not a number there.
        private static long? ReadAs(string scalar, YamlSchemaKind schema)
        {
            try
            {
                return YamlSerializer.Deserialize<long>(scalar, new YamlSerializerOptions { Schema = schema });
            }
            catch (YamlException)
            {
                return null;
            }
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void WriteBack_KeepsTheTextWhereItReadsTheSame(YamlSchemaKind schema, string scalar, long expected)
        {
            var documentOptions = new YamlDocumentOptions { Schema = schema };
            using YamlDocument document = YamlDocument.Parse("a: " + scalar + "\n", documentOptions);
            YamlElement element = document.RootElement.GetProperty("a");
            YamlNode node = YamlNode.Parse("a: " + scalar + "\n", documentOptions: documentOptions)!["a"]!;
            object value = YamlSerializer.Deserialize<Dictionary<string, object>>("a: " + scalar + "\n", new YamlSerializerOptions { Schema = schema })!["a"];

            // In block style the text is kept where the writer's schema reads it to the same number (always under the
            // document's own); otherwise, and in JSON, the value is written.
            foreach (YamlSchemaKind written in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                foreach (YamlWriterStyle style in new[] { YamlWriterStyle.Block, YamlWriterStyle.JsonCompatible })
                {
                    bool keepsText = style == YamlWriterStyle.Block && (written == schema || ReadAs(scalar, written) == expected);
                    string text = keepsText ? scalar : Text(expected);
                    var options = new YamlSerializerOptions { Schema = written, WriteStyle = style };
                    Assert.Equal(text, YamlSerializer.Serialize(element, options).TrimEnd());
                    Assert.Equal(text, YamlSerializer.Serialize(node, options).TrimEnd());
                    Assert.Equal(text, YamlSerializer.Serialize(value, options).TrimEnd());
                    Assert.Equal(expected, YamlSerializer.Deserialize<long>(YamlSerializer.Serialize(element, options), options));
                }
            }

            // Under the document's own schema the text is written as it was read, and reads back to the number.
            var same = new YamlSerializerOptions { Schema = schema, WriteStyle = YamlWriterStyle.Block };
            string written1 = YamlSerializer.Serialize(document, same);
            Assert.Equal("a: " + scalar, written1.TrimEnd());
            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, long>>(written1, same)!["a"]);
        }

        [Fact]
        public void WriteBack_AnOctalUnderACoreWriter_IsTheValue()
        {
            // 012 is 10 under YAML 1.1 and 12 under the core schema: a core writer gets the value, a YAML 1.1 one the text.
            using YamlDocument document = YamlDocument.Parse("a: 012\n", new YamlDocumentOptions { Schema = YamlSchemaKind.Yaml11 });
            Assert.Equal("a: 10", YamlSerializer.Serialize(document).TrimEnd());
            Assert.Equal("a: 012", YamlSerializer.Serialize(document, new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 }).TrimEnd());
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void DeepEquals_ComparesTheValue(YamlSchemaKind schema, string scalar, long expected)
        {
            var documentOptions = new YamlDocumentOptions { Schema = schema };
            string same = Text(expected), other = Text(expected + 1);
            foreach (string shape in new[] { "{0}", "[{0}]", "k: {0}\n" })
            {
                string left = string.Format(CultureInfo.InvariantCulture, shape, scalar);
                using YamlDocument a = YamlDocument.Parse(left, documentOptions);
                using YamlDocument b = YamlDocument.Parse(string.Format(CultureInfo.InvariantCulture, shape, same), documentOptions);
                using YamlDocument c = YamlDocument.Parse(string.Format(CultureInfo.InvariantCulture, shape, other), documentOptions);
                Assert.True(YamlElement.DeepEquals(a.RootElement, b.RootElement), left);
                Assert.False(YamlElement.DeepEquals(a.RootElement, c.RootElement), left);
                Assert.True(YamlNode.DeepEquals(YamlNode.Parse(left, documentOptions: documentOptions), b.RootElement.Deserialize<YamlNode>()), left);
            }
        }

        [Fact]
        public void YamlValue_ReadsAYamlSpellingInTheReadersSchema()
        {
            // A YamlValue read from a YAML number spelling holds the value the element reads.
            Assert.Equal(double.PositiveInfinity, YamlSerializer.Deserialize<YamlValue>(".inf")!.GetValue<double>());
            Assert.Equal(31, YamlSerializer.Deserialize<YamlValue>("0x1F")!.GetValue<int>());
            Assert.Equal(YamlValueKind.Number, YamlSerializer.Deserialize<YamlValue>("0x1F")!.GetValueKind());
            Assert.Equal("0x1F", YamlSerializer.Serialize(YamlSerializer.Deserialize<YamlValue>("0x1F"), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block }).TrimEnd());
            Assert.Equal("31", YamlSerializer.Serialize(YamlSerializer.Deserialize<YamlValue>("0x1F"), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }).TrimEnd());
            Assert.Equal(5, YamlSerializer.Deserialize<YamlValue>("0b101", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.GetValue<int>());
            Assert.Equal("012", YamlSerializer.Deserialize<YamlValue>("012", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.GetValue<YamlElement>().GetRawText());
        }
    }
}
