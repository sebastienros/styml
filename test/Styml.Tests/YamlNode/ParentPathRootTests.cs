// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class ParentPathRootTests
    {
        [Fact]
        public static void GetPathAndRoot()
        {
            YamlNode node;

            node = YamlValue.Create(1);
            Assert.Equal("$", node.GetPath());
            Assert.Same(node, node.Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$", node.GetPath());

            node = new YamlObject();
            Assert.Equal("$", node.GetPath());
            Assert.Same(node, node.Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$", node.GetPath());

            node = new YamlArray();
            Assert.Equal("$", node.GetPath());
            Assert.Same(node, node.Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$", node.GetPath());

            node = new YamlObject
            {
                ["Child"] = 1
            };
            Assert.Equal("$.Child", node["Child"].GetPath());

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$.Child", node["Child"].GetPath());

            node = new YamlObject
            {
                ["Child"] = new YamlArray { 1, 2, 3 }
            };
            Assert.Equal("$.Child[1]", node["Child"][1].GetPath());
            Assert.Same(node, node["Child"][1].Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$.Child[1]", node["Child"][1].GetPath());

            node = new YamlObject
            {
                ["Child"] = new YamlArray { 1, 2, 3 }
            };
            Assert.Equal("$.Child[2]", node["Child"][2].GetPath());
            Assert.Same(node, node["Child"][2].Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$.Child[2]", node["Child"][2].GetPath());

            node = new YamlArray
            {
                new YamlObject
                {
                    ["Child"] = 42
                }
            };
            Assert.Equal("$[0].Child", node[0]["Child"].GetPath());
            Assert.Same(node, node[0]["Child"].Root);

            node = YamlValue.Parse(node.ToYamlString());
            Assert.Equal("$[0].Child", node[0]["Child"].GetPath());
        }

        [Fact]
        public static void GetPath_SpecialCharacters()
        {
            YamlNode node = new YamlObject
            {
                ["[Child"] = 1
            };

            Assert.Equal("$['[Child']", node["[Child"].GetPath());
        }

        [Theory]
        [InlineData("$defs", "$['$defs']")]
        [InlineData("foo['bar", "$['foo[\\'bar']")]
        [InlineData("a'b", "$['a\\'b']")]
        [InlineData("a\\b", "$['a\\\\b']")]
        [InlineData("a\\'b", "$['a\\\\\\'b']")]
        [InlineData("prop with space", "$['prop with space']")]
        [InlineData("dotted.name", "$['dotted.name']")]
        public static void GetPath_EscapedPropertyNames(string propertyName, string expectedPath)
        {
            YamlNode node = new YamlObject
            {
                [propertyName] = 1
            };

            Assert.Equal(expectedPath, node[propertyName]!.GetPath());
        }

        [Fact]
        public static void GetPath_NestedEscapedPropertyNames()
        {
            // Test case from the issue: {"$defs":{"foo['bar":"baz"}}
            YamlNode node = YamlNode.Parse("""{"$defs":{"foo['bar":"baz"}}""")!;
            Assert.Equal("$['$defs']['foo[\\'bar']", node["$defs"]!["foo['bar"]!.GetPath());
        }

        [Fact]
        public static void DetectCycles_Object()
        {
            var jObject = new YamlObject();
            Assert.Throws<InvalidOperationException>(() => jObject.Add("a", jObject));

            var jObject2 = new YamlObject();
            jObject.Add("b", jObject2);
            Assert.Throws<InvalidOperationException>(() => jObject2.Add("b", jObject));
        }

        [Fact]
        public static void DetectCycles_Array()
        {
            var jArray = new YamlArray { };
            Assert.Throws<InvalidOperationException>(() => jArray.Add(jArray));

            var jArray2 = new YamlArray { };
            jArray.Add(jArray2);
            Assert.Throws<InvalidOperationException>(() => jArray2.Add(jArray));
        }

        [Fact]
        public static void DetectCycles_ArrayWithObject()
        {
            var jObject = new YamlObject { };
            var jArray = new YamlArray { };
            jArray.Add(jObject);

            Assert.Throws<InvalidOperationException>(() => jObject.Add("Prop", jArray));
        }

        [Fact]
        public static void Parent_Array()
        {
            var child = YamlValue.Create(42);
            Assert.Null(child.Options);

            var parent = new YamlArray();
            Assert.Null(parent.Options);
            parent.Add(child);
            Assert.Null(child.Options);
            parent.Clear();

            parent = new YamlArray(new YamlNodeOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(parent.Options);
            parent.Add(child);
            Assert.NotNull(child.Options);
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);

            // The indexer unparents existing node.
            parent[0] = YamlValue.Create(43);
            Assert.Null(child.Parent);

            parent.Clear();
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);

            // Adding to a new parent does not affect options previously obtained from a different parent.
            parent = new YamlArray(new YamlNodeOptions { PropertyNameCaseInsensitive = false });
            parent.Add(child);
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);
        }

        [Fact]
        public static void Parent_Object()
        {
            var child = YamlValue.Create(42);
            Assert.Null(child.Options);

            var parent = new YamlObject();
            Assert.Null(parent.Options);
            parent.Add("MyProp", child);
            Assert.Null(child.Options);
            parent.Clear();

            parent = new YamlObject(new YamlNodeOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(parent.Options);
            parent.Add("MyProp", child);
            Assert.NotNull(child.Options);
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);

            // The indexer unparents existing node.
            parent["MyProp"] = YamlValue.Create(43);
            Assert.Null(child.Parent);

            parent.Clear();
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);

            // Adding to a new parent does not affect options previously obtained from a different parent.
            parent = new YamlObject(new YamlNodeOptions { PropertyNameCaseInsensitive = false });
            parent.Add("MyProp", child);
            Assert.True(child.Options.Value.PropertyNameCaseInsensitive);
        }
    }
}
