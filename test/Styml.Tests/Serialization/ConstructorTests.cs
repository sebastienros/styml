// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public class ConstructorTests_String : ConstructorTests
    {
        public ConstructorTests_String()
            : base(YamlSerializerWrapper.StringSerializer)
        { }
    }

    public class ConstructorTests_AsyncStream : ConstructorTests
    {
        public ConstructorTests_AsyncStream()
            : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public class ConstructorTests_SyncStream : ConstructorTests
    {
        public ConstructorTests_SyncStream()
            : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public class ConstructorTests_Span : ConstructorTests
    {
        public ConstructorTests_Span()
            : base(YamlSerializerWrapper.SpanSerializer) { }
    }

    public class ConstructorTests_Pipe : ConstructorTests
    {
        public ConstructorTests_Pipe()
            : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }
}
