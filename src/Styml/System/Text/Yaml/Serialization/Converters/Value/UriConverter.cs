// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class UriConverter : YamlPrimitiveConverter<Uri?>
    {
        public override Uri? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.TokenType is YamlTokenType.Null || reader.IsLenientNull ? null : ReadCore(ref reader);
        }

        public override void Write(Utf8YamlWriter writer, Uri? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.OriginalString);
        }

        internal override Uri ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static Uri ReadCore(ref Utf8YamlReader reader)
        {
            string? uriString = reader.GetString();

            if (!Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out Uri? value))
            {
                ThrowHelper.ThrowYamlException();
            }

            return value;
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, Uri value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            writer.WritePropertyName(value.OriginalString);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) =>
            new() { Type = YamlSchemaType.String, Format = "uri" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
