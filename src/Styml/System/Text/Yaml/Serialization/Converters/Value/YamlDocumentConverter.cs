// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Schema;
using System.Text.Yaml.Nodes;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlDocumentConverter : YamlConverter<YamlDocument?>
    {
        internal override bool IsUntypedConverter => true;

        public override bool HandleNull => true;

        public override YamlDocument Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return YamlAliasReader.ParseDocument(ref reader, options.DuplicateKeyHandling, options);
        }

        public override void Write(Utf8YamlWriter writer, YamlDocument? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
