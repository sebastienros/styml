// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Reflection;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class ParseTests
    {
        [Fact]
        public static void Parse()
        {
            YamlObject jObject = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml).AsObject();

            Assert.Equal("Hello!", jObject["MyString"].GetValue<string>());
            Assert.Null(jObject["MyNull"]);
            Assert.False(jObject["MyBoolean"].GetValue<bool>());
            Assert.Equal("ed957609-cdfe-412f-88c1-02daca1b4f51", jObject["MyGuid"].GetValue<string>());
            Assert.IsType<YamlArray>(jObject["MyArray"]);
            Assert.IsType<YamlObject>(jObject["MyObject"]);

            Assert.Equal(43, jObject["MyInt"].GetValue<int>());
            Assert.Equal<uint>(43, jObject["MyInt"].GetValue<uint>());
            Assert.Equal(43, jObject["MyInt"].GetValue<long>());
            Assert.Equal<ulong>(43, jObject["MyInt"].GetValue<ulong>());
            Assert.Equal(43, jObject["MyInt"].GetValue<short>());
            Assert.Equal<ushort>(43, jObject["MyInt"].GetValue<ushort>());
            Assert.Equal(43, jObject["MyInt"].GetValue<byte>());
            Assert.Equal(43, jObject["MyInt"].GetValue<sbyte>());
            Assert.Equal(43, jObject["MyInt"].GetValue<decimal>());
            Assert.Equal(43, jObject["MyInt"].GetValue<float>());

            DateTime dt = YamlNode.Parse("\"2020-07-08T01:02:03\"").GetValue<DateTime>();
            Assert.Equal(2020, dt.Year);
            Assert.Equal(7, dt.Month);
            Assert.Equal(8, dt.Day);
            Assert.Equal(1, dt.Hour);
            Assert.Equal(2, dt.Minute);
            Assert.Equal(3, dt.Second);

            DateTimeOffset dtOffset = YamlNode.Parse("\"2020-07-08T01:02:03+01:15\"").GetValue<DateTimeOffset>();
            Assert.Equal(2020, dtOffset.Year);
            Assert.Equal(7, dtOffset.Month);
            Assert.Equal(8, dtOffset.Day);
            Assert.Equal(1, dtOffset.Hour);
            Assert.Equal(2, dtOffset.Minute);
            Assert.Equal(3, dtOffset.Second);
            Assert.Equal(new TimeSpan(1,15,0), dtOffset.Offset);
        }

        [Fact]
        public static void Parse_TryGetPropertyValue()
        {
            YamlObject jObject = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml).AsObject();

            YamlNode? node;

            Assert.True(jObject.TryGetPropertyValue("MyString", out node));
            Assert.Equal("Hello!", node.GetValue<string>());

            Assert.True(jObject.TryGetPropertyValue("MyNull", out node));
            Assert.Null(node);

            Assert.True(jObject.TryGetPropertyValue("MyBoolean", out node));
            Assert.False(node.GetValue<bool>());

            Assert.True(jObject.TryGetPropertyValue("MyArray", out node));
            Assert.IsType<YamlArray>(node);

            Assert.True(jObject.TryGetPropertyValue("MyInt", out node));
            Assert.Equal(43, node.GetValue<int>());

            Assert.True(jObject.TryGetPropertyValue("MyDateTime", out node));
            Assert.Equal("2020-07-08T00:00:00", node.GetValue<string>());

            Assert.True(jObject.TryGetPropertyValue("MyGuid", out node));
            Assert.Equal("ed957609-cdfe-412f-88c1-02daca1b4f51", node.AsValue().GetValue<Guid>().ToString());

            Assert.True(jObject.TryGetPropertyValue("MyObject", out node));
            Assert.IsType<YamlObject>(node);
        }

        [Fact]
        public static void Parse_TryGetPropertyValueWithIndex()
        {
            YamlObject jObject = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml).AsObject();

            YamlNode? node;
            int index;

            Assert.True(jObject.TryGetPropertyValue("MyString", out node, out index));
            Assert.Equal("Hello!", node.GetValue<string>());
            Assert.Equal(0, index);

            Assert.True(jObject.TryGetPropertyValue("MyNull", out node, out index));
            Assert.Null(node);
            Assert.Equal(1, index);

            Assert.True(jObject.TryGetPropertyValue("MyBoolean", out node, out index));
            Assert.False(node.GetValue<bool>());
            Assert.Equal(2, index);

            Assert.True(jObject.TryGetPropertyValue("MyArray", out node, out index));
            Assert.IsType<YamlArray>(node);
            Assert.Equal(3, index);

            Assert.True(jObject.TryGetPropertyValue("MyInt", out node, out index));
            Assert.Equal(43, node.GetValue<int>());
            Assert.Equal(4, index);

            Assert.True(jObject.TryGetPropertyValue("MyDateTime", out node, out index));
            Assert.Equal("2020-07-08T00:00:00", node.GetValue<string>());
            Assert.Equal(5, index);

            Assert.True(jObject.TryGetPropertyValue("MyGuid", out node, out index));
            Assert.Equal("ed957609-cdfe-412f-88c1-02daca1b4f51", node.AsValue().GetValue<Guid>().ToString());
            Assert.Equal(6, index);

            Assert.True(jObject.TryGetPropertyValue("MyObject", out node, out index));
            Assert.IsType<YamlObject>(node);
            Assert.Equal(7, index);
        }

        [Fact]
        public static void Parse_TryGetPropertyValueFail()
        {
            YamlObject jObject = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml).AsObject();

            YamlNode? node;
            int index;

            Assert.False(jObject.TryGetPropertyValue("NonExistentKey", out node, out index));
            Assert.Null(node);
            Assert.Equal(-1, index);
        }

        [Fact]
        public static void Parse_TryGetValue()
        {
            Assert.True(YamlNode.Parse("\"Hello\"").AsValue().TryGetValue(out string? _));
            Assert.True(YamlNode.Parse("true").AsValue().TryGetValue(out bool? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out byte? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out sbyte? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out short? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out ushort? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out int? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out uint? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out long? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out ulong? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out decimal? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out float? _));
            Assert.True(YamlNode.Parse("42").AsValue().TryGetValue(out double? _));
            Assert.True(YamlNode.Parse("\"2020-07-08T00:00:00\"").AsValue().TryGetValue(out DateTime? _));
            Assert.True(YamlNode.Parse("\"ed957609-cdfe-412f-88c1-02daca1b4f51\"").AsValue().TryGetValue(out Guid? _));
            Assert.True(YamlNode.Parse("\"2020-07-08T01:02:03+01:15\"").AsValue().TryGetValue(out DateTimeOffset? _));

            YamlValue? jValue = YamlNode.Parse("\"Hello!\"").AsValue();
            Assert.False(jValue.TryGetValue(out int _));
            Assert.False(jValue.TryGetValue(out DateTime _));
            Assert.False(jValue.TryGetValue(out DateTimeOffset _));
            Assert.False(jValue.TryGetValue(out Guid _));
        }

        [Fact]
        public static void Parse_Fail()
        {
            YamlObject jObject = YamlNode.Parse(YamlNodeTests.ExpectedDomYaml).AsObject();

            Assert.Throws<InvalidOperationException>(() => jObject["MyString"].GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyBoolean"].GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyGuid"].GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyInt"].GetValue<string>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyDateTime"].GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyObject"].GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => jObject["MyArray"].GetValue<int>());
        }

        [Fact]
        public static async Task NullReference_Fail()
        {
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize<YamlNode>((string)null));
            Assert.Throws<ArgumentNullException>(() => YamlNode.Parse((string)null));
            Assert.Throws<ArgumentNullException>(() => YamlNode.Parse((Stream)null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => YamlNode.ParseAsync(null));
        }

        [Fact]
        public static async Task NullLiteral()
        {
            Assert.Null(YamlSerializer.Deserialize<YamlNode>("null"));
            Assert.Null(YamlNode.Parse("null"));

            using (MemoryStream stream = new MemoryStream("null"u8.ToArray()))
            {
                Assert.Null(YamlNode.Parse(stream));
                stream.Position = 0;
                Assert.Null(await YamlNode.ParseAsync(stream));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static async Task InternalValueFields()
        {
            // Use reflection to inspect the internal state of the 3 fields that hold values.
            // There is not another way to verify, and using a debug watch causes nodes to be created.
            FieldInfo elementField = typeof(YamlObject).GetField("_yamlElement", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(elementField);

            FieldInfo yamlDictionaryField = typeof(YamlObject).GetField("_dictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(yamlDictionaryField);

            using (MemoryStream stream = new MemoryStream(SimpleTestClass.s_data))
            {
                // Only YamlElement is present.
                YamlNode node = YamlNode.Parse(stream);
                object yamlDictionary = yamlDictionaryField.GetValue(node);
                Assert.Null(yamlDictionary); // Value is null until converted from YamlElement.
                Assert.NotNull(elementField.GetValue(node));
                Test();

                // Cause the single YamlElement to expand into individual YamlElement nodes.
                Assert.Equal(1, node.AsObject()["MyInt16"].GetValue<int>());
                Assert.Null(elementField.GetValue(node));

                yamlDictionary = yamlDictionaryField.GetValue(node);
                Assert.NotNull(yamlDictionary);

                Test();

                void Test()
                {
                    string actual = node.ToYamlString();

                    // Replace the escaped "+" sign used with DateTimeOffset.
                    actual = actual.Replace("\\u002B", "+");

                    Assert.Equal(SimpleTestClass.s_yaml.StripWhitespace(), actual);
                }
            }

            using (MemoryStream stream = new MemoryStream(SimpleTestClass.s_data))
            {
                // Only YamlElement is present.
                YamlNode node = await YamlNode.ParseAsync(stream);
                object yamlDictionary = yamlDictionaryField.GetValue(node);
                Assert.Null(yamlDictionary); // Value is null until converted from YamlElement.
                Assert.NotNull(elementField.GetValue(node));
                Test();

                // Cause the single YamlElement to expand into individual YamlElement nodes.
                Assert.Equal(1, node.AsObject()["MyInt16"].GetValue<int>());
                Assert.Null(elementField.GetValue(node));

                yamlDictionary = yamlDictionaryField.GetValue(node);
                Assert.NotNull(yamlDictionary);

                Test();

                void Test()
                {
                    string actual = node.ToYamlString();

                    // Replace the escaped "+" sign used with DateTimeOffset.
                    actual = actual.Replace("\\u002B", "+");

                    Assert.Equal(SimpleTestClass.s_yaml.StripWhitespace(), actual);
                }
            }
        }

        [Fact]
        public static async Task ReadPrimitives()
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"1")))
            {
                int i = YamlNode.Parse(stream).AsValue().GetValue<int>();
                Assert.Equal(1, i);
            }

            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"1")))
            {
                int i = (await YamlNode.ParseAsync(stream)).AsValue().GetValue<int>();
                Assert.Equal(1, i);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ParseThenEdit()
        {
            const string Expected = "{\"MyString\":null,\"Node\":42,\"Array\":[43],\"Value\":44,\"IntValue\":45,\"Object\":{\"Property\":46}}";

            YamlNode node = YamlNode.Parse(Expected);
            Assert.Equal(Expected, node.ToYamlString());

            // Change a primitive
            node["IntValue"] = 1;
            const string ExpectedAfterEdit1 = "{\"MyString\":null,\"Node\":42,\"Array\":[43],\"Value\":44,\"IntValue\":1,\"Object\":{\"Property\":46}}";
            Assert.Equal(ExpectedAfterEdit1, node.ToYamlString());

            // Change element
            node["Array"][0] = 2;
            const string ExpectedAfterEdit2 = "{\"MyString\":null,\"Node\":42,\"Array\":[2],\"Value\":44,\"IntValue\":1,\"Object\":{\"Property\":46}}";
            Assert.Equal(ExpectedAfterEdit2, node.ToYamlString());

            // Change property
            node["MyString"] = "3";
            const string ExpectedAfterEdit3 = "{\"MyString\":\"3\",\"Node\":42,\"Array\":[2],\"Value\":44,\"IntValue\":1,\"Object\":{\"Property\":46}}";
            Assert.Equal(ExpectedAfterEdit3, node.ToYamlString());
        }
    }
}
