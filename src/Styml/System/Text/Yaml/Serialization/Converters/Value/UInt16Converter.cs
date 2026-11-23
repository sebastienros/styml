// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class UInt16Converter : YamlPrimitiveConverter<ushort>
    {
        public UInt16Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override ushort Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetUInt16();
        }

        public override void Write(Utf8YamlWriter writer, ushort value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            // For performance, lift up the writer implementation.
            writer.WriteNumberValue((long)value);
        }

        internal override ushort ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetUInt16WithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, ushort value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override ushort ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String &&
                (YamlNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetUInt16WithQuotes();
            }

            return reader.GetUInt16();
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, ushort value, YamlNumberHandling handling)
        {
            if ((YamlNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else
            {
                // For performance, lift up the writer implementation.
                writer.WriteNumberValue((long)value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
            GetSchemaForNumericType(YamlSchemaType.Integer, numberHandling);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            GetSupportedYamlValueTypesForNumericType(numberHandling);
    }
}
