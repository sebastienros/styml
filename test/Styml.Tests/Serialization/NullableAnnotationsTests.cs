// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class NullableAnnotationsTests_String : NullableAnnotationsTests
    {
        public NullableAnnotationsTests_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed partial class NullableAnnotationsTests_AsyncStreamWithSmallBuffer : NullableAnnotationsTests
    {
        public NullableAnnotationsTests_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }
}
