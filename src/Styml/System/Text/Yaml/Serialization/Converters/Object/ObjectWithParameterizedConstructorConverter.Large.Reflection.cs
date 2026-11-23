// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>YamlObjectConverter{T}</cref> that supports the deserialization
    /// of YAML objects using parameterized constructors.
    /// </summary>
    internal sealed class LargeObjectWithParameterizedConstructorConverterWithReflection<T>
        : LargeObjectWithParameterizedConstructorConverter<T> where T : notnull
    {
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        public LargeObjectWithParameterizedConstructorConverterWithReflection()
        {
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal override void ConfigureYamlTypeInfoUsingReflection(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            yamlTypeInfo.CreateObjectWithArgs = DefaultYamlTypeInfoResolver.MemberAccessor.CreateParameterizedConstructor<T>(ConstructorInfo!);
        }
    }
}
