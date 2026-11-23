// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class CollectionTestsDynamic_String : CollectionTests
    {
        public CollectionTestsDynamic_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed partial class CollectionTestsDynamic_AsyncStream : CollectionTests
    {
        public CollectionTestsDynamic_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed partial class CollectionTestsDynamic_AsyncStreamWithSmallBuffer : CollectionTests
    {
        public CollectionTestsDynamic_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public sealed partial class CollectionTestsDynamic_SyncStream : CollectionTests
    {
        public CollectionTestsDynamic_SyncStream() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public sealed partial class CollectionTestsDynamic_Pipe : CollectionTests
    {
        public CollectionTestsDynamic_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public sealed partial class CollectionTestsDynamic_PipeWithSmallBuffer : CollectionTests
    {
        public CollectionTestsDynamic_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}
