// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed class ReferenceHandlerTestsDynamic_String : ReferenceHandlerTests
    {
        public ReferenceHandlerTestsDynamic_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_AsyncStream : ReferenceHandlerTests
    {
        public ReferenceHandlerTestsDynamic_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_AsyncStreamWithSmallBuffer : ReferenceHandlerTests
    {
        public ReferenceHandlerTestsDynamic_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_IgnoreCycles_String : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTestsDynamic_IgnoreCycles_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_IgnoreCycles_AsyncStream : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTestsDynamic_IgnoreCycles_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_IgnoreCycles_Pipe : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTestsDynamic_IgnoreCycles_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public sealed class ReferenceHandlerTestsDynamic_IgnoreCycles_PipeWithSmallBuffer : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTestsDynamic_IgnoreCycles_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}
