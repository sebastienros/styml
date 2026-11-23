// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class Int32Converter : YamlPrimitiveConverter<int>
    {
        public Int32Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetInt32();
        }

        public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            // For performance, lift up the writer implementation.
            writer.WriteNumberValue((long)value);
        }

        internal override int ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetInt32WithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, int value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override int ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String &&
                (YamlNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetInt32WithQuotes();
            }

            return reader.GetInt32();
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, int value, YamlNumberHandling handling)
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
