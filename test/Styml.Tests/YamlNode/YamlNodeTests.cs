// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static partial class YamlNodeTests
    {
        [Fact]
        public static void YamlTypes_Deserialize()
        {
            Assert.IsType<YamlObject>(YamlSerializer.Deserialize<YamlNode>("{}"));
            Assert.IsType<YamlObject>(YamlNode.Parse("{}"));
            Assert.IsType<YamlObject>(YamlNode.Parse(ToUtf8("{}")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("{}"));

            Assert.IsType<YamlArray>(YamlSerializer.Deserialize<YamlNode>("[]"));
            Assert.IsType<YamlArray>(YamlNode.Parse("[]"));
            Assert.IsType<YamlArray>(YamlNode.Parse(ToUtf8("[]")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("[]"));

            Assert.IsAssignableFrom<YamlValue>(YamlSerializer.Deserialize<YamlNode>("true"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse("true"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse(ToUtf8("true")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("true"));

            Assert.IsAssignableFrom<YamlValue>(YamlSerializer.Deserialize<YamlNode>("0"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse("0"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse(ToUtf8("0")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("0"));

            Assert.IsAssignableFrom<YamlValue>(YamlSerializer.Deserialize<YamlNode>("1.2"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse("1.2"));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse(ToUtf8("1.2")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("1.2"));

            Assert.IsAssignableFrom<YamlValue>(YamlSerializer.Deserialize<YamlNode>("\"str\""));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse("\"str\""));
            Assert.IsAssignableFrom<YamlValue>(YamlNode.Parse(ToUtf8("\"str\"")));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("\"str\""));

            YamlType_Deserializes_Null<YamlNode>();
            YamlType_Deserializes_Null<YamlArray>();
            YamlType_Deserializes_Null<YamlObject>();
        }


        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Parse_AllowMultipleValues_TrailingContent()
        {
            var options = new YamlReaderOptions { AllowMultipleValues = true };
            var reader = new Utf8YamlReader("[null,false,42,{},[1]]             <NotYaml/>"u8, options);

            YamlNode node = YamlNode.Parse(ref reader);
            Assert.Equal("[null,false,42,{},[1]]", node.ToYamlString());
            Assert.Equal(YamlTokenType.EndArray, reader.TokenType);

            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader reader) => reader.Read());
        }

        private static void YamlType_Deserializes_Null<TNode>() where TNode : YamlNode
        {
            Assert.Null(YamlSerializer.Deserialize<TNode>("null"));
            Assert.Collection(YamlSerializer.Deserialize<TNode[]>("[null]"), Assert.Null);
            Assert.Collection(YamlSerializer.Deserialize<IReadOnlyDictionary<string, TNode>>("{ \"Value\": null }"), kv => Assert.Null(kv.Value));
            Assert.Null(YamlSerializer.Deserialize<ObjectWithNodeProperty<TNode>>("{ \"Value\": null }").Value);
        }
        private record ObjectWithNodeProperty<TNode>(TNode Value) where TNode : YamlNode;

        [Fact]
        public static void AsMethods_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("{}").AsArray());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("{}").AsValue());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("[]").AsObject());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("[]").AsValue());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("1").AsArray());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("1").AsObject());
        }

        [Fact]
        public static void NullHandling()
        {
            var options = new YamlSerializerOptions();
            YamlNode obj = YamlSerializer.Deserialize<YamlNode>("null", options);
            Assert.Null(obj);
        }

        private static byte[] ToUtf8(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        [Fact]
        public static void GetValue_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("{}").GetValue<object>());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("[]").GetValue<object>());
        }

        [Fact]
        public static void GetValueKind()
        {
            Assert.Equal(YamlValueKind.Object, YamlNode.Parse("{}").GetValueKind());
            Assert.Equal(YamlValueKind.Array, YamlNode.Parse("[]").GetValueKind());
            Assert.Equal(YamlValueKind.Number, YamlNode.Parse("12").GetValueKind());
            Assert.Equal(YamlValueKind.String, YamlNode.Parse("\"12\"").GetValueKind());
            Assert.Equal(YamlValueKind.True, YamlNode.Parse("true").GetValueKind());
            Assert.Equal(YamlValueKind.False, YamlNode.Parse("false").GetValueKind());
        }

        [Fact]
        public static void GetPropertyName()
        {
            YamlNode yamlNode = YamlNode.Parse("{\"a\" : \"b\"}");
            Assert.Equal("a", yamlNode["a"].GetPropertyName());

            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("[]").GetPropertyName());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("5").GetPropertyName());
        }

        [Fact]
        public static void GetElementIndex()
        {
            YamlNode yamlNode = YamlNode.Parse("[90, \"str\", true, false]");
            Assert.Equal(0, yamlNode[0].GetElementIndex());
            Assert.Equal(1, yamlNode[1].GetElementIndex());
            Assert.Equal(2, yamlNode[2].GetElementIndex());
            Assert.Equal(3, yamlNode[3].GetElementIndex());

            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("{}").GetElementIndex());
            Assert.Throws<InvalidOperationException>(() => YamlNode.Parse("5").GetElementIndex());
        }


        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReplaceWith()
        {
            YamlNode yamlNode = YamlNode.Parse("[90, 2, 3]");
            yamlNode[1].ReplaceWith(12);
            yamlNode[2].ReplaceWith("str");

            Assert.Equal(12, yamlNode[1].GetValue<int>());
            Assert.Equal("str", yamlNode[2].GetValue<string>());

            Assert.Equal("[90,12,\"str\"]", yamlNode.ToYamlString());

            yamlNode = YamlNode.Parse("{\"a\": \"b\"}");
            yamlNode["a"].ReplaceWith("c");
            Assert.Equal("{\"a\":\"c\"}", yamlNode.ToYamlString());
        }

        [Theory]
        [InlineData("null", "  null       ")]
        [InlineData("false", "   false  ")]
        [InlineData("true", "   true  ")]
        [InlineData("-0.0", "0")]
        [InlineData("0", "0.0000e4")]
        [InlineData("0", "0.0000e-4")]
        [InlineData("1", "1.0")]
        [InlineData("1", "1e0")]
        [InlineData("1", "1.0000")]
        [InlineData("1", "1.0000e0")]
        [InlineData("1", "0.10000e1")]
        [InlineData("1", "10.0000e-1")]
        [InlineData("10001", "1.0001e4")]
        [InlineData("10001e-3", "1.0001e1")]
        [InlineData("1", "0.1e1")]
        [InlineData("0.1", "1e-1")]
        [InlineData("0.001", "1e-3")]
        [InlineData("1e9", "1000000000")]
        [InlineData("11", "1.100000000e1")]
        [InlineData("3.141592653589793", "3141592653589793E-15")]
        [InlineData("0.000000000000000000000000000000000000000001", "1e-42")]
        [InlineData("1000000000000000000000000000000000000000000", "1e42")]
        [InlineData("-1.1e3", "-1100")]
        [InlineData("79228162514264337593543950336", "792281625142643375935439503360e-1")] // decimal.MaxValue + 1
        [InlineData("79228162514.264337593543950336", "792281625142643375935439503360e-19")]
        [InlineData("1.75e+300", "1.75E+300")] // Variations in exponent casing
        [InlineData( // > 256 digits
            "1.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001",

            "100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001" + "E-512")]
        [InlineData("\"\"", "   \"\"")]
        [InlineData("\"ABC\"", "\"\\u0041\\u0042\\u0043\"")]
        [InlineData("{ }", " {    }")]
        [InlineData("""{ "x" : 1, "y" : 2 }""", """{ "x" : 1, "y" : 2 }""")]
        [InlineData("""{ "x" : 1, "y" : 2 }""", """{ "y" : 2, "x" : 1 }""")]
        [InlineData("[]", " [ ]")]
        [InlineData("[1, 2, 3]", " [1,  2,  3  ]")]
        [InlineData("""[null, false, 3.14, "ABC", { "x" : 1, "y" : 2 }, []]""",
            """[null, false, 314e-2, "\u0041\u0042\u0043", { "y" : 2, "x" : 1 }, [ ] ]""")]
        public static void DeepEquals_EqualValuesReturnTrue(string value1, string value2)
        {
            YamlNode node1 = YamlNode.Parse(value1);
            YamlNode node2 = YamlNode.Parse(value2);

            AssertDeepEqual(node1, node2);
        }

        [Theory]
        // Kind mismatch
        [InlineData("null", "false")]
        [InlineData("null", "42")]
        [InlineData("null", "\"str\"")]
        [InlineData("null", "{}")]
        [InlineData("null", "[]")]
        [InlineData("false", "42")]
        [InlineData("false", "\"str\"")]
        [InlineData("false", "{}")]
        [InlineData("false", "[]")]
        [InlineData("42", "\"str\"")]
        [InlineData("42", "{}")]
        [InlineData("42", "[]")]
        [InlineData("\"str\"", "{}")]
        [InlineData("\"str\"", "[]")]
        [InlineData("{}", "[]")]
        // Value mismatch
        [InlineData("false", "true")]
        [InlineData("0", "1")]
        [InlineData("1", "-1")]
        [InlineData("1.1", "-1.1")]
        [InlineData("1.1e5", "-1.1e5")]
        [InlineData("0", "1e-1024")]
        [InlineData("1", "0.1")]
        [InlineData("1", "1.1")]
        [InlineData("1", "1e1")]
        [InlineData("1", "1.00001")]
        [InlineData("1", "1.0000e1")]
        [InlineData("1", "0.1000e-1")]
        [InlineData("1", "10.0000e-2")]
        [InlineData("10001", "1.0001e3")]
        [InlineData("10001e-3", "1.0001e2")]
        [InlineData("1", "0.1e2")]
        [InlineData("0.1", "1e-2")]
        [InlineData("0.001", "1e-4")]
        [InlineData("1e9", "1000000001")]
        [InlineData("11", "1.100000001e1")]
        [InlineData("0.000000000000000000000000000000000000000001", "1e-43")]
        [InlineData("1000000000000000000000000000000000000000000", "1e43")]
        [InlineData("-1.1e3", "-1100.1")]
        [InlineData("79228162514264337593543950336", "7922816251426433759354395033600e-1")] // decimal.MaxValue + 1
        [InlineData("79228162514.264337593543950336", "7922816251426433759354395033601e-19")]
        [InlineData("1.75e+300", "1.75E+301")] // Variations in exponent casing
        [InlineData("1e2147483647", "1e-2147483648")] // int.MaxValue, int.MinValue exponents
        [InlineData( // > 256 digits
            "1.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001",

            "100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000003" + "E-512")]
        [InlineData("\"\"", "   \" \"")]
        [InlineData("\"ABC\"", "   \"ABc\"")]
        [InlineData("[1]", "[]")]
        [InlineData("[1]", "[2]")]
        [InlineData("[1,2,3]", "[1,2,3,4]")]
        [InlineData("[1,2,3]", "[1,3,2]")]
        [InlineData("{}", """{ "Prop" : null }""")]
        [InlineData("""{ "Prop" : 1 }""", """{ "Prop" : null }""")]
        [InlineData("""{ "Prop1" : 1 }""", """{ "Prop1" : 1, "Prop2" : 2 }""")]
        [InlineData("""{ "Prop1" : 1, "Prop2": {} }""", """{ "Prop1" : 1, "Prop2" : 2 }""")]
        [InlineData("""{ "Prop1" : 1, "Prop2": {}, "Prop3": false }""", """{ "Prop1" : 1, "Prop2" : { "c" : null }, "Prop3" : false }""")]
        [InlineData("""{ "Prop1" : 1, "Prop2": {}, "Prop3": false }""", """{ "Prop1" : 1, "Prop3" : true, "Prop2" : {} }""")]
        // Regression tests for https://github.com/dotnet/runtime/issues/112769
        [InlineData("""{"test1":null}""", """{"test2":null}""")]
        [InlineData("""{"test1":null, "test2":null}""", """{"test3":null, "test4":null}""")]
        [InlineData("""{"test1":null, "test2":null}""", """{"test3":null}""")]
        [InlineData("""{"test1":null}""", """{"test2":[null]}""")]
        public static void DeepEquals_NotEqualValuesReturnFalse(string value1, string value2)
        {
            YamlNode obj1 = YamlNode.Parse(value1);
            YamlNode obj2 = YamlNode.Parse(value2);

            AssertNotDeepEqual(obj1, obj2);
        }

        [Fact]
        public static void YamlNode_ReplaceWith()
        {
            YamlObject obj = new YamlObject();
            obj["name"] = "test";
            YamlNode node = obj["name"];
            
            node.ReplaceWith(YamlValue.Create("replaced"));
            Assert.Equal("replaced", obj["name"].GetValue<string>());
        }

        [Fact]
        public static void YamlArray_Replace()
        {
            YamlArray arr = new YamlArray(1, 2, 3);
            arr[1] = YamlValue.Create(99);
            Assert.Equal(99, arr[1].GetValue<int>());
        }

        [Fact]
        public static void YamlObject_SetProperty()
        {
            YamlObject obj = new YamlObject();
            obj["key"] = "value";
            obj["key"] = "newValue";
            Assert.Equal("newValue", obj["key"].GetValue<string>());
        }

        [Fact]
        public static void YamlValue_AsValue()
        {
            YamlNode node = YamlValue.Create(42);
            YamlValue value = node.AsValue();
            Assert.Equal(42, value.GetValue<int>());
        }
    }
}
