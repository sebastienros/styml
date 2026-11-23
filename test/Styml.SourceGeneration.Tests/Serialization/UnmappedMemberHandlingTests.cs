// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class UnmappedMemberHandlingTests_Metadata_String : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_Metadata_String()
            : base(new StringSerializerWrapper(UnmappedMemberHandlingTestsContext.Default))
        {
        }

        [YamlSerializable(typeof(PocoWithoutAnnotations))]
        [YamlSerializable(typeof(PocoWithSkipAnnotation))]
        [YamlSerializable(typeof(PocoWithDisallowAnnotation))]
        [YamlSerializable(typeof(PocoInheritingDisallowAnnotation))]
        [YamlSerializable(typeof(ClassWithExtensionData))]
        [YamlSerializable(typeof(ClassWithExtensionDataAndDisallowHandling))]
        public partial class UnmappedMemberHandlingTestsContext : YamlSerializerContext
        { }
    }

    public sealed class UnmappedMemberHandlingTests_Metadata_Async : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_Metadata_Async()
            : base(new AsyncStreamSerializerWrapper(UnmappedMemberHandlingTests_Metadata_String.UnmappedMemberHandlingTestsContext.Default))
        {
        }
    }
}
