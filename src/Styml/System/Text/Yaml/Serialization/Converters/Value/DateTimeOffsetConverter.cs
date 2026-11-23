// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class DateTimeOffsetConverter : YamlPrimitiveConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8YamlWriter writer, DateTimeOffset value, YamlSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        internal override DateTimeOffset ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetDateTimeOffsetNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, DateTimeOffset value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new YamlSchema { Type = YamlSchemaType.String, Format = "date-time" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
