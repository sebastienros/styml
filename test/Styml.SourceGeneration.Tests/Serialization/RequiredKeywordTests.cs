// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public partial class RequiredKeywordTests_SourceGen : RequiredKeywordTests
    {
        public RequiredKeywordTests_SourceGen()
            : base(new StringSerializerWrapper(RequiredKeywordTestsContext.Default))
        {
        }

        [YamlSerializable(typeof(InheritedPersonWithRequiredMembers))]
        [YamlSerializable(typeof(InheritedPersonWithRequiredMembersWithAdditionalRequiredMembers))]
        [YamlSerializable(typeof(InheritedPersonWithRequiredMembersSetsRequiredMembers))]
        [YamlSerializable(typeof(PersonWithRequiredMembers))]
        [YamlSerializable(typeof(PersonWithRequiredMembersAndSmallParametrizedCtor))]
        [YamlSerializable(typeof(PersonWithRequiredMembersAndLargeParametrizedCtor))]
        [YamlSerializable(typeof(PersonWithRequiredMembersAndSetsRequiredMembers))]
        [YamlSerializable(typeof(PersonWithRequiredMembersAndSmallParametrizedCtorAndSetsRequiredMembers))]
        [YamlSerializable(typeof(PersonWithRequiredMembersAndLargeParametrizedCtorAndSetsRequiredMembers))]
        [YamlSerializable(typeof(ClassWithInitOnlyRequiredProperty))]
        [YamlSerializable(typeof(ClassWithRequiredField))]
        [YamlSerializable(typeof(ClassWithRequiredExtensionDataProperty))]
        [YamlSerializable(typeof(ClassWithRequiredKeywordAndYamlRequiredCustomAttribute))]
        [YamlSerializable(typeof(ClassWithCustomRequiredPropertyName))]
        [YamlSerializable(typeof(DerivedClassWithRequiredInitOnlyProperty))]
        [YamlSerializable(typeof(ClassWithNullValidatingConstructor))]
        internal sealed partial class RequiredKeywordTestsContext : YamlSerializerContext
        {
        }
    }
}
