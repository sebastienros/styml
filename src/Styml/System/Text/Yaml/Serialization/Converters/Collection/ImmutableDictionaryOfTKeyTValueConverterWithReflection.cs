// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class ImmutableDictionaryOfTKeyTValueConverterWithReflection<TCollection, TKey, TValue>
        : ImmutableDictionaryOfTKeyTValueConverter<TCollection, TKey, TValue>
        where TCollection : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        [RequiresUnreferencedCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        [RequiresDynamicCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        public ImmutableDictionaryOfTKeyTValueConverterWithReflection()
        {
        }

        [RequiresUnreferencedCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        [RequiresDynamicCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        internal override void ConfigureYamlTypeInfoUsingReflection(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            yamlTypeInfo.CreateObjectWithArgs = DefaultYamlTypeInfoResolver.MemberAccessor.CreateImmutableDictionaryCreateRangeDelegate<TCollection, TKey, TValue>();
        }
    }
}
