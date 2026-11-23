// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class StackOrQueueConverterWithReflection<TCollection>
        : StackOrQueueConverter<TCollection>
        where TCollection : IEnumerable
    {
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        public StackOrQueueConverterWithReflection() { }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal override void ConfigureYamlTypeInfoUsingReflection(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            yamlTypeInfo.AddMethodDelegate = DefaultYamlTypeInfoResolver.MemberAccessor.CreateAddMethodDelegate<TCollection>();
        }
    }
}
