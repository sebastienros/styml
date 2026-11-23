// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class DateTimeConverter : YamlPrimitiveConverter<DateTime>
    {
        public override DateTime Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return reader.GetDateTime();
        }

        public override void Write(Utf8YamlWriter writer, DateTime value, YamlSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        internal override DateTime ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return reader.GetDateTimeNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, DateTime value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new YamlSchema { Type = YamlSchemaType.String, Format = "date-time" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
