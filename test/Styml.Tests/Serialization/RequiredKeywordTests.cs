// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class RequiredKeywordTests_Span : RequiredKeywordTests
    {
        public RequiredKeywordTests_Span() : base(YamlSerializerWrapper.SpanSerializer) { }
    }

    public class RequiredKeywordTests_String : RequiredKeywordTests
    {
        public RequiredKeywordTests_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public class RequiredKeywordTests_AsyncStream : RequiredKeywordTests
    {
        public RequiredKeywordTests_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public class RequiredKeywordTests_AsyncStreamWithSmallBuffer : RequiredKeywordTests
    {
        public RequiredKeywordTests_AsyncStreamWithSmallBuffer() : base(YamlSerializerWrapper.AsyncStreamSerializerWithSmallBuffer) { }
    }

    public class RequiredKeywordTests_SyncStream : RequiredKeywordTests
    {
        public RequiredKeywordTests_SyncStream() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public class RequiredKeywordTests_Writer : RequiredKeywordTests
    {
        public RequiredKeywordTests_Writer() : base(YamlSerializerWrapper.ReaderWriterSerializer) { }
    }

    public class RequiredKeywordTests_Document : RequiredKeywordTests
    {
        public RequiredKeywordTests_Document() : base(YamlSerializerWrapper.DocumentSerializer) { }
    }

    public class RequiredKeywordTests_Element : RequiredKeywordTests
    {
        public RequiredKeywordTests_Element() : base(YamlSerializerWrapper.ElementSerializer) { }
    }

    public class RequiredKeywordTests_Node : RequiredKeywordTests
    {
        public RequiredKeywordTests_Node() : base(YamlSerializerWrapper.NodeSerializer) { }
    }

    public class RequiredKeywordTests_Pipe : RequiredKeywordTests
    {
        public RequiredKeywordTests_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public class RequiredKeywordTests_PipeWithSmallBuffer : RequiredKeywordTests
    {
        public RequiredKeywordTests_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}
