// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class SingleConverter : YamlPrimitiveConverter<float>
    {

        public SingleConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        internal override bool IsIeeeFloatingPointConverter => true;

        public override float Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetSingle();
        }

        public override void Write(Utf8YamlWriter writer, float value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override float ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetSingleWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, float value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override float ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String)
            {
                if ((YamlNumberHandling.AllowReadingFromString & handling) != 0)
                {
                    return reader.GetSingleWithQuotes();
                }
                else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
                {
                    return reader.GetSingleFloatingPointConstant();
                }
            }

            return reader.GetSingle();
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, float value, YamlNumberHandling handling)
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
