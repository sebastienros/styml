// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.IO;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// A number read into a document model is written back with its source text: YamlDotNet's YamlStream, SharpYaml's
    /// Model.YamlStream and Styml's YamlDocument and YamlNode, under the schema that reads the spelling as a number.
    /// </summary>
    public class NumberTextParityTests
    {
        [Theory]
        [InlineData("0x1F", YamlSchemaKind.Core)]
        [InlineData("0o14", YamlSchemaKind.Core)]
        [InlineData("012", YamlSchemaKind.Core)]
        [InlineData("00000000", YamlSchemaKind.Core)]
        [InlineData("+1", YamlSchemaKind.Core)]
        [InlineData(".5", YamlSchemaKind.Core)]
        [InlineData("1.", YamlSchemaKind.Core)]
        [InlineData("1e5", YamlSchemaKind.Core)]
        [InlineData("012", YamlSchemaKind.Yaml11)]
        [InlineData("1_000", YamlSchemaKind.Yaml11)]
        [InlineData("0b101", YamlSchemaKind.Yaml11)]
        [InlineData("1:30", YamlSchemaKind.Yaml11)]
        public void ModelsKeepANumbersText(string text, YamlSchemaKind schema)
        {
            string yaml = "a: " + text + "\n";

            var ydn = new YamlDotNet.RepresentationModel.YamlStream();
            ydn.Load(new StringReader(yaml));
            var ydnText = new StringWriter();
            ydn.Save(ydnText, assignAnchors: false);
            Assert.StartsWith(yaml, ydnText.ToString().Replace("\r\n", "\n"));

            var syText = new StringWriter();
            SharpYaml.Model.YamlStream.Load(new StringReader(yaml)).WriteTo(syText);
            Assert.EndsWith(yaml, syText.ToString().Replace("\r\n", "\n"));

            var options = new YamlSerializerOptions { Schema = schema, WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
            using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { Schema = schema });
            Assert.Equal(YamlValueKind.Number, document.RootElement.GetProperty("a").ValueKind);
            Assert.Equal(yaml, YamlSerializer.Serialize(document, options));
            Assert.Equal(yaml, YamlNode.Parse(yaml, documentOptions: new YamlDocumentOptions { Schema = schema })!.ToYamlString(options));
        }

        // What each model writes back of a node's text beyond numbers: the order of its properties, an empty value, and
        // plain text that YAML 1.1 reads as a boolean. Both libraries write the anchor first and an empty value with its
        // blank; Styml keeps the source's order and blank.
        [Theory]
        [InlineData("a: !t &x 1\n", "a: &x !t 1\n", "a: &x !t 1\n", "a: !t &x 1\n")]
        [InlineData("a: &x !t 1\n", "a: &x !t 1\n", "a: &x !t 1\n", "a: &x !t 1\n")]
        [InlineData("a: \nb:\n", "a: \nb: \n", "a: \nb: \n", "a: \nb:\n")]
        [InlineData("y: on\nv: {x: 0, y: 1}\n", "y: on\nv: {x: 0, y: 1}\n", "y: on\nv: {x: 0, y: 1}\n", "y: on\nv: {x: 0, y: 1}\n")]
        public void ModelsWriteBackNodeText(string yaml, string yamlDotNet, string sharpYaml, string styml)
        {
            var ydn = new YamlDotNet.RepresentationModel.YamlStream();
            ydn.Load(new StringReader(yaml));
            var ydnText = new StringWriter();
            ydn.Save(ydnText, assignAnchors: false);
            Assert.StartsWith(yamlDotNet, ydnText.ToString().Replace("\r\n", "\n"));

            var syText = new StringWriter();
            SharpYaml.Model.YamlStream.Load(new StringReader(yaml)).WriteTo(syText);
            Assert.EndsWith(sharpYaml, syText.ToString().Replace("\r\n", "\n"));

            using YamlDocument document = YamlDocument.Parse(yaml);
            Assert.Equal(styml, YamlSerializer.Serialize(document, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" }));
        }
    }
}
