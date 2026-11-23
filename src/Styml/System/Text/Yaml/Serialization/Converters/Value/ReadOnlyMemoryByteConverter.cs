// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class ReadOnlyMemoryByteConverter : YamlConverter<ReadOnlyMemory<byte>>
    {
        public override bool HandleNull => true;

        public override ReadOnlyMemory<byte> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.TokenType is YamlTokenType.Null || reader.IsLenientNull ? default : reader.GetBytesFromBase64();
        }

        public override void Write(Utf8YamlWriter writer, ReadOnlyMemory<byte> value, YamlSerializerOptions options)
        {
            writer.WriteBase64StringValue(value.Span);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.String, ContentEncoding = "base64" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
