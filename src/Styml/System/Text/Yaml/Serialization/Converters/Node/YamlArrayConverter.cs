// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlArrayConverter : YamlConverter<YamlArray?>
    {
        internal override bool IsUntypedConverter => true;

        public override void Write(Utf8YamlWriter writer, YamlArray? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override YamlArray? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartArray:
                    return (YamlArray)YamlNodeConverter.ReadAsBuiltNode(ref reader, options)!;
                case YamlTokenType.Null:
                    return null;
                default:
                    throw ThrowHelper.GetInvalidOperationException_ExpectedArray(reader.TokenType);
            }
        }

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Array;

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.Array };
    }
}
