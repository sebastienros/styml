// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class ToStringTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NodeToString()
        {
            string Expected = $"{{{Environment.NewLine}  \"MyString\": \"Hello!\",{Environment.NewLine}  \"MyNull\": null,{Environment.NewLine}  \"MyBoolean\": false,{Environment.NewLine}  \"MyArray\": [{Environment.NewLine}    2,{Environment.NewLine}    3,{Environment.NewLine}    42{Environment.NewLine}  ],{Environment.NewLine}  \"MyInt\": 43,{Environment.NewLine}  \"MyDateTime\": \"2020-07-08T00:00:00\",{Environment.NewLine}  \"MyGuid\": \"ed957609-cdfe-412f-88c1-02daca1b4f51\",{Environment.NewLine}  \"MyObject\": {{{Environment.NewLine}    \"MyString\": \"Hello!!\"{Environment.NewLine}  }},{Environment.NewLine}  \"Child\": {{{Environment.NewLine}    \"ChildProp\": 1{Environment.NewLine}  }}{Environment.NewLine}}}";
            YamlNode node = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml);
            string yaml = node.ToString();
            Assert.Equal(Expected, yaml);
        }

        [Fact]
        public static void NodeToString_StringValuesNotQuoted()
        {
            YamlNode node = YamlNode.Parse("\"Hello\"");
            string yaml = node.ToString();
            Assert.Equal("Hello", yaml);

            node = YamlValue.Create("Hello");
            yaml = node.ToString();
            Assert.Equal("Hello", yaml);

            node = YamlSerializer.Deserialize<YamlNode>("\"Hello\"");
            yaml = node.ToString();
            Assert.Equal("Hello", yaml);
        }
    }
}
