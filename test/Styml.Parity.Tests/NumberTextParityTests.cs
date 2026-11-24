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
    }
}
