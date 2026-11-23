// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Tests.Schemas.OrderPayload;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class NodeInteropTests : SerializerTests
    {
        public NodeInteropTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper) { }

        [Fact]
        public async Task CompareResultsAgainstSerializer()
        {
            List<Order> obj = Order.PopulateLargeObject(2);
            string expected = await Serializer.SerializeWrapper(obj);

            YamlArray jArray = await Serializer.DeserializeWrapper<YamlArray>(expected);
            string actual = jArray.ToYamlString();
            Assert.Equal(expected, actual);

            jArray = YamlNode.Parse(expected).AsArray();
            actual = jArray.ToYamlString();
            Assert.Equal(expected, actual);
        }

        public class Poco
        {
            public string? MyString { get; set; }
            public YamlNode Node { get; set; }
            public YamlArray Array { get; set; }
            public YamlValue Value { get; set; }
            public YamlValue IntValue { get; set; }
            public YamlObject Object { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NodesAsPocoProperties()
        {
            const string Expected = "{\"MyString\":null,\"Node\":42,\"Array\":[43],\"Value\":44,\"IntValue\":45,\"Object\":{\"Property\":46}}";

            var poco = new Poco
            {
                Node = 42,
                Array = new YamlArray(43),
                Value = (YamlValue)44,
                IntValue = (YamlValue)45,
                Object = new YamlObject
                {
                    ["Property"] = 46
                }
            };

            string yaml = await Serializer.SerializeWrapper(poco);
            Assert.Equal(Expected, yaml);

            poco = await Serializer.DeserializeWrapper<Poco>(yaml);
            Assert.Equal(42, (int)poco.Node);
            Assert.Equal(43, (int)poco.Array[0]);
            Assert.Equal(44, (int)poco.Value);
            Assert.Equal(45, (int)poco.IntValue);
            Assert.Equal(46, (int)poco.Object["Property"]);
        }
    }
}
