// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed class MetadataTests_String() : MetadataTests(YamlSerializerWrapper.StringSerializer);
    public sealed class MetadataTests_AsyncStream() : MetadataTests(YamlSerializerWrapper.AsyncStreamSerializer);
}
