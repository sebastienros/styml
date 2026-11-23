// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed class StreamTests_Async : StreamTests
    {
        public StreamTests_Async() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public sealed class StreamTests_AsyncWithSmallBuffer : StreamTests
    {
        public StreamTests_AsyncWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public sealed class StreamTests_Sync : StreamTests
    {
        public StreamTests_Sync() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public sealed class PipeTests_Async : StreamTests
    {
        public PipeTests_Async() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public abstract partial class StreamTests
    {
        /// <summary>
        /// The System Under Test for the test suite.
        /// </summary>
        private StreamingYamlSerializerWrapper Serializer { get; }

        public StreamTests(StreamingYamlSerializerWrapper serializer)
        {
            Serializer = serializer;
        }
    }
}
