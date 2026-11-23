// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Tests
{
    internal class TestResolver : IYamlTypeInfoResolver
    {
        private Func<Type, YamlSerializerOptions, YamlTypeInfo?> _getTypeInfo;

        public TestResolver(Func<Type, YamlSerializerOptions, YamlTypeInfo> getTypeInfo)
        {
            _getTypeInfo = getTypeInfo;
        }

        public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            return _getTypeInfo(type, options);
        }
    }
}
