// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public partial class MetadataTests_SourceGen() : MetadataTests(new StringSerializerWrapper(Context.Default))
    {
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(int?))]
        [YamlSerializable(typeof(string))]
        [YamlSerializable(typeof(string[]))]
        [YamlSerializable(typeof(List<string>))]
        [YamlSerializable(typeof(IList<string>))]
        [YamlSerializable(typeof(ImmutableArray<string>))]
        [YamlSerializable(typeof(DerivedList<int>))]
        [YamlSerializable(typeof(DerivedListWithCustomConverter))]
        [YamlSerializable(typeof(Dictionary<Guid, int>))]
        [YamlSerializable(typeof(IReadOnlyDictionary<Guid, int>))]
        [YamlSerializable(typeof(ImmutableDictionary<Guid, int>))]
        [YamlSerializable(typeof(DerivedDictionary<int>))]
        [YamlSerializable(typeof(DerivedDictionaryWithCustomConverter))]
        [YamlSerializable(typeof(ArrayList))]
        [YamlSerializable(typeof(Hashtable))]
        [YamlSerializable(typeof(ClassWithoutCtor))]
        [YamlSerializable(typeof(IInterfaceWithProperties))]
        [YamlSerializable(typeof(ClassWithDefaultCtor))]
        [YamlSerializable(typeof(StructWithDefaultCtor))]
        [YamlSerializable(typeof(StructWithDefaultCtor?))]
        [YamlSerializable(typeof(ClassWithParameterizedCtor))]
        [YamlSerializable(typeof(StructWithParameterizedCtor))]
        [YamlSerializable(typeof(ClassWithRequiredMember))]
        [YamlSerializable(typeof(ClassWithInitOnlyProperty))]
        [YamlSerializable(typeof(ClassWithInitOnlyAndRequiredMembers))]
        [YamlSerializable(typeof(ClassWithMultipleConstructors))]
        [YamlSerializable(typeof(DerivedClassWithShadowingProperties))]
        [YamlSerializable(typeof(IDerivedInterface))]
        [YamlSerializable(typeof(ClassWithRequiredAndOptionalConstructorParameters))]
        [YamlSerializable(typeof(ClassWithRefStructProperty))]
        [YamlSerializable(typeof(ClassWithRefStructConstructorParameter))]
#if NET
        [YamlSerializable(typeof(CollectionWithRefStructElement))]
#endif
        partial class Context : YamlSerializerContext;
    }
}
