// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlValueConverter : YamlConverter<YamlValue?>
    {
        internal override bool IsUntypedConverter => true;

        public override void Write(Utf8YamlWriter writer, YamlValue? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override YamlValue? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType is YamlTokenType.Null)
            {
                return null;
            }

            switch (reader.TokenType)
            {
                case YamlTokenType.String:
                case YamlTokenType.False:
                case YamlTokenType.True:
                case YamlTokenType.Number:
                    return ReadNonNullPrimitiveValue(ref reader, options.GetNodeOptions());
                default:
                    YamlElement element = YamlAliasReader.ParseElement(ref reader, options.DuplicateKeyHandling, options);
                    return YamlValue.CreateFromElement(ref element, options.GetNodeOptions());
            }
        }

        internal static YamlValue ReadNonNullPrimitiveValue(ref Utf8YamlReader reader, YamlNodeOptions options)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.String or YamlTokenType.False or YamlTokenType.True or YamlTokenType.Number);
            return YamlValueOfYamlPrimitive.CreatePrimitiveValue(ref reader, options);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
