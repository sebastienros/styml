// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlNodeConverterFactory : YamlConverterFactory
    {
        private static readonly YamlArrayConverter s_arrayConverter = new YamlArrayConverter();
        private static readonly YamlObjectConverter s_objectConverter = new YamlObjectConverter();
        private static readonly YamlValueConverter s_valueConverter = new YamlValueConverter();

        public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            if (typeof(YamlValue).IsAssignableFrom(typeToConvert))
            {
                return s_valueConverter;
            }

            if (typeof(YamlObject) == typeToConvert)
            {
                return s_objectConverter;
            }

            if (typeof(YamlArray) == typeToConvert)
            {
                return s_arrayConverter;
            }

            Debug.Assert(typeof(YamlNode) == typeToConvert);
            return YamlNodeConverter.Instance;
        }

        public override bool CanConvert(Type typeToConvert) => typeof(YamlNode).IsAssignableFrom(typeToConvert);
    }
}
