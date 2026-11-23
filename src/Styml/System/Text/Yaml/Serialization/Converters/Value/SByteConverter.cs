// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class SByteConverter : YamlPrimitiveConverter<sbyte>
    {
        public SByteConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override sbyte Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetSByte();
        }

        public override void Write(Utf8YamlWriter writer, sbyte value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override sbyte ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetSByteWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, sbyte value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override sbyte ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String &&
                (YamlNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetSByteWithQuotes();
            }

            return reader.GetSByte();
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, sbyte value, YamlNumberHandling handling)
        {
            if ((YamlNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
            GetSchemaForNumericType(YamlSchemaType.Integer, numberHandling);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            GetSupportedYamlValueTypesForNumericType(numberHandling);
    }
}
