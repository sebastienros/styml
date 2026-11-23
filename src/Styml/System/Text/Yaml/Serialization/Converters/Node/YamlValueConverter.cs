// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
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
                    return ReadNonNullPrimitiveValue(ref reader, options.GetNodeOptions());
                case YamlTokenType.Number when IsJsonNumber(ref reader):
                    return ReadNonNullPrimitiveValue(ref reader, options.GetNodeOptions());
                default:
                    // A YAML number spelling (0x1F, .inf, 012 under YAML 1.1) too: the element reads it in the
                    // reader's schema, as a YamlNode does.
                    YamlElement element = YamlAliasReader.ParseElement(ref reader, options.DuplicateKeyHandling, options);
                    return YamlValue.CreateFromElement(ref element, options.GetNodeOptions());
            }
        }

        private static bool IsJsonNumber(ref Utf8YamlReader reader) =>
            reader.YamlJsonNumberFastPath &&
            YamlReaderHelper.IsJsonNumber(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);

        internal static YamlValue ReadNonNullPrimitiveValue(ref Utf8YamlReader reader, YamlNodeOptions options)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.String or YamlTokenType.False or YamlTokenType.True or YamlTokenType.Number);
            return YamlValueOfYamlPrimitive.CreatePrimitiveValue(ref reader, options);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
