// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class BooleanConverter : YamlPrimitiveConverter<bool>
    {
        public override bool Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.GetBoolean();
        }

        public override void Write(Utf8YamlWriter writer, bool value, YamlSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }

        internal override bool ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();

            // A quoted key is text: System.Text.Json's rule, true or false in any case, so a JSON document reads as
            // System.Text.Json reads it. A plain key reads as a
            // value would: the core schema's
            // casings, the YAML 1.1 words under that schema, and other cases only with AcceptLenientScalars.
            bool plain = reader.ScalarStyle == YamlScalarStyle.Plain;
            if (Utf8Parser.TryParse(propertyName, out bool value, out int bytesConsumed) && propertyName.Length == bytesConsumed)
            {
                if (!plain || YamlReaderHelper.TryGetYamlBoolean(propertyName, out _) || reader.AcceptsLenientScalars)
                {
                    return value;
                }
            }
            else if (plain && reader.UsesYaml11Schema
                && (YamlReaderHelper.TryGetYaml11Boolean(propertyName, out value)
                    || (reader.AcceptsLenientScalars && YamlReaderHelper.TryGetLenientBoolean(propertyName, yaml11: true, out value))))
            {
                return value;
            }

            ThrowHelper.ThrowFormatException(DataType.Boolean);
            return false;
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, bool value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.Boolean };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Boolean;
    }
}
