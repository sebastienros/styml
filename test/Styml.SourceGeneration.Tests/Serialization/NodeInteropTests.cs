// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;
using System.Text.Yaml.Serialization.Tests.Schemas.OrderPayload;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class NodeInteropTests_Metadata : NodeInteropTests
    {
        public NodeInteropTests_Metadata()
            : base(new StringSerializerWrapper(NodeInteropTestsContext_Metadata.Default))
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(List<Order>))]
        [YamlSerializable(typeof(YamlArray))]
        [YamlSerializable(typeof(Poco))]
        [YamlSerializable(typeof(string))]
        internal sealed partial class NodeInteropTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed partial class NodeInteropTests_Default : NodeInteropTests
    {
        public NodeInteropTests_Default()
            : base(new StringSerializerWrapper(NodeInteropTestsContext_Default.Default))
        {
        }

        [YamlSerializable(typeof(List<Order>))]
        [YamlSerializable(typeof(YamlArray))]
        [YamlSerializable(typeof(Poco))]
        [YamlSerializable(typeof(string))]
        internal sealed partial class NodeInteropTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
