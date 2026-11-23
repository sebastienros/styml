// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class AsyncEnumerableTests_AsyncStream : AsyncEnumerableTests
    {
        public AsyncEnumerableTests_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }

        [Fact]
        public void DeserializeAsyncEnumerable_NullArgument_ThrowsArgumentNullException()
        {
            AssertExtensions.Throws<ArgumentNullException>("utf8Yaml", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: (Stream?)null));
            AssertExtensions.Throws<ArgumentNullException>("utf8Yaml", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: (Stream?)null, yamlTypeInfo: ResolveYamlTypeInfo<int>()));
            AssertExtensions.Throws<ArgumentNullException>("yamlTypeInfo", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: new MemoryStream(), yamlTypeInfo: null));
        }
    }

    public class AsyncEnumerableTests_AsyncPipeSerializer : AsyncEnumerableTests
    {
        public AsyncEnumerableTests_AsyncPipeSerializer() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }

        [Fact]
        public void DeserializeAsyncEnumerable_NullArgument_ThrowsArgumentNullException()
        {
            AssertExtensions.Throws<ArgumentNullException>("utf8Yaml", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: (PipeReader?)null));
            AssertExtensions.Throws<ArgumentNullException>("utf8Yaml", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: (PipeReader?)null, yamlTypeInfo: ResolveYamlTypeInfo<int>()));
            AssertExtensions.Throws<ArgumentNullException>("yamlTypeInfo", () => YamlSerializer.DeserializeAsyncEnumerable<int>(utf8Yaml: new Pipe().Reader, yamlTypeInfo: null));
        }
    }
}
