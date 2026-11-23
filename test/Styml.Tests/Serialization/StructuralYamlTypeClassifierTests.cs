// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed class StructuralYamlTypeClassifierTests_String : StructuralYamlTypeClassifierTests
    {
        public StructuralYamlTypeClassifierTests_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed class StructuralYamlTypeClassifierTests_AsyncStreamWithSmallBuffer : StructuralYamlTypeClassifierTests
    {
        public StructuralYamlTypeClassifierTests_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public sealed class StructuralYamlTypeClassifierTests_SyncStreamWithSmallBuffer : StructuralYamlTypeClassifierTests
    {
        public StructuralYamlTypeClassifierTests_SyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.SyncStreamSerializerWithSmallBuffer) { }
    }
}
