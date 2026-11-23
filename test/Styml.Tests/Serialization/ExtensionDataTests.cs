// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class ExtensionDataTestsDynamic_String : ExtensionDataTests
    {
        public ExtensionDataTestsDynamic_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public sealed partial class ExtensionDataTestsDynamic_AsyncStream : ExtensionDataTests
    {
        public ExtensionDataTestsDynamic_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }
}
