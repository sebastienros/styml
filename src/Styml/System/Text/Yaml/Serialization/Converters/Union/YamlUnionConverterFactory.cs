// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace System.Text.Yaml.Serialization.Converters
{
    [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
    internal sealed class YamlUnionConverterFactory : YamlConverterFactory
    {
        private const string UnionAttributeName = "UnionAttribute";
        private const string UnionAttributeFullName = "System.Runtime.CompilerServices.UnionAttribute";

        // A type IS a union iff it carries [System.Runtime.CompilerServices.UnionAttribute].
        // The marker is detected by full name (rather than via a compile-time reference)
        // because the C# compiler may polyfill it into the user's assembly when targeting a
        // downlevel runtime. [YamlUnion] alone is a customization attribute that has no meaning
        // on non-union types.
        public override bool CanConvert(Type typeToConvert) => IsUnionType(typeToConvert);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            Type converterType = typeof(YamlUnionConverter<>).MakeGenericType(typeToConvert);
            return (YamlConverter)Activator.CreateInstance(converterType)!;
        }

        private static bool IsUnionType(Type type)
        {
            foreach (CustomAttributeData data in type.GetCustomAttributesData())
            {
                Type attributeType = data.AttributeType;
                if (attributeType.Name == UnionAttributeName &&
                    attributeType.FullName == UnionAttributeFullName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
