// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Schema;
using System.Text.Yaml.Nodes;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlElementConverter : YamlConverter<YamlElement>
    {
        internal override bool IsUntypedConverter => true;

        public override YamlElement Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return YamlAliasReader.ParseElement(ref reader, options.DuplicateKeyHandling, options);
        }

        public override void Write(Utf8YamlWriter writer, YamlElement value, YamlSerializerOptions options)
        {
            value.WriteTo(writer);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
