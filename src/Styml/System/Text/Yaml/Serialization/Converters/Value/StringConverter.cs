// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class StringConverter : YamlPrimitiveConverter<string?>
    {
        public override string? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            // AcceptLenientScalars: NuLL is null here, as the null token is (GetString returns null for it).
            if (reader.IsLenientNull)
            {
                return null;
            }

            return reader.GetString();
        }

        public override void Write(Utf8YamlWriter writer, string? value, YamlSerializerOptions options)
        {
            // For performance, lift up the writer implementation.
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.AsSpan());
            }
        }

        internal override string ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetString()!;
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, string value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (options.DictionaryKeyPolicy is not null && !isWritingExtensionDataProperty)
            {
                value = options.DictionaryKeyPolicy.ConvertName(value);

                if (value is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_NamingPolicyReturnNull(options.DictionaryKeyPolicy);
                }
            }

            writer.WritePropertyName(value);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.String };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
