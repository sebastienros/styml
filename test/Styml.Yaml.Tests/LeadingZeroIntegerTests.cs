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
    /// An element writes the value (<c>10</c>), as it writes every YAML number spelling, so the text reads back the
    /// same under any schema.
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

        [Theory]
        [MemberData(nameof(Forms))]
        public void WriteBack_WritesTheValue(YamlSchemaKind schema, string scalar, long expected)
        {
            var documentOptions = new YamlDocumentOptions { Schema = schema };
            using YamlDocument document = YamlDocument.Parse("a: " + scalar + "\n", documentOptions);
            YamlElement element = document.RootElement.GetProperty("a");
            YamlNode node = YamlNode.Parse("a: " + scalar + "\n", documentOptions: documentOptions)!["a"]!;
            object value = YamlSerializer.Deserialize<Dictionary<string, object>>("a: " + scalar + "\n", new YamlSerializerOptions { Schema = schema })!["a"];

            // Under the document's schema or another one, in block style and in JSON.
            foreach (YamlSchemaKind written in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                foreach (YamlWriterStyle style in new[] { YamlWriterStyle.Block, YamlWriterStyle.JsonCompatible })
                {
                    var options = new YamlSerializerOptions { Schema = written, WriteStyle = style };
                    Assert.Equal(Text(expected), YamlSerializer.Serialize(element, options).TrimEnd());
                    Assert.Equal(Text(expected), YamlSerializer.Serialize(node, options).TrimEnd());
                    Assert.Equal(Text(expected), YamlSerializer.Serialize(value, options).TrimEnd());
                }
            }

            // The document's text reads back as the value under both schemas.
            string text = YamlSerializer.Serialize(document, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block });
            Assert.Equal("a: " + Text(expected), text.TrimEnd());
            foreach (YamlSchemaKind reread in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, long>>(text, new YamlSerializerOptions { Schema = reread })!["a"]);
            }
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
            Assert.Equal("31", YamlSerializer.Serialize(YamlSerializer.Deserialize<YamlValue>("0x1F"), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block }).TrimEnd());
            Assert.Equal(5, YamlSerializer.Deserialize<YamlValue>("0b101", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.GetValue<int>());
            Assert.Equal("012", YamlSerializer.Deserialize<YamlValue>("012", new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.GetValue<YamlElement>().GetRawText());
        }
    }
}
