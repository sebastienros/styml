// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed class YamlCreationHandlingTests_String : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed class YamlCreationHandlingTests_AsyncStream : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed class YamlCreationHandlingTests_AsyncStreamWithSmallBuffer : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public sealed class YamlCreationHandlingTests_SyncStream : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_SyncStream() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public sealed class YamlCreationHandlingTests_Pipe : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public sealed class YamlCreationHandlingTests_PipeWithSmallBuffer : YamlCreationHandlingTests
    {
        public YamlCreationHandlingTests_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}
