// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class DoubleConverter : YamlPrimitiveConverter<double>
    {
        public DoubleConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        internal override bool IsIeeeFloatingPointConverter => true;

        public override double Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetDouble();
        }

        public override void Write(Utf8YamlWriter writer, double value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override double ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetDoubleWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, double value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override double ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String)
            {
                if ((YamlNumberHandling.AllowReadingFromString & handling) != 0)
                {
                    return reader.GetDoubleWithQuotes();
                }
                else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
                {
                    return reader.GetDoubleFloatingPointConstant();
                }
            }

            return reader.GetDouble();
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, double value, YamlNumberHandling handling)
        {
            if ((YamlNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
            {
                writer.WriteFloatingPointConstant(value);
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
                GetSchemaForNumericType(YamlSchemaType.Number, numberHandling, isIeeeFloatingPoint: true);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            GetSupportedYamlValueTypesForNumericType(numberHandling);
    }
}
