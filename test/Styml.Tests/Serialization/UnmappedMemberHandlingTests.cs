// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class UnmappedMemberHandlingTests_String : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed partial class UnmappedMemberHandlingTests_AsyncStream : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed partial class UnmappedMemberHandlingTests_Pipe : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public sealed partial class UnmappedMemberHandlingTests_PipeWithSmallBuffer : UnmappedMemberHandlingTests
    {
        public UnmappedMemberHandlingTests_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}
