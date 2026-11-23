// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class ByteArrayConverter : YamlConverter<byte[]?>
    {
        public override byte[]? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.Null || reader.IsLenientNull)
            {
                return null;
            }

            return reader.GetBytesFromBase64();
        }

        public override void Write(Utf8YamlWriter writer, byte[]? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteBase64StringValue(value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.String, ContentEncoding = "base64" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
