// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class GuidConverter : YamlPrimitiveConverter<Guid>
    {
        public override Guid Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.GetGuid();
        }

        public override void Write(Utf8YamlWriter writer, Guid value, YamlSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        internal override Guid ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetGuidNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, Guid value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
            new() { Type = YamlSchemaType.String, Format = "uuid" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
