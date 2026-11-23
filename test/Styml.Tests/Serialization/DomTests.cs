// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Provides basic tests for serializing To\From DOM types including YamlDocument, YamlElement and YamlNode.
    /// </summary>
    /// The test class <see cref="System.Text.Yaml.Tests.Serialization.MetadataTests"/> provides tests for the YamlTypeInfo and YamlContext permutations.
    /// The test class <see cref="YamlSerializerApiValidation"/> provides tests for input validation.
    public static partial class DomTests
    {
        private const string Escaped_PlusSign = "\"\\u002B\""; // A '+' sign is escaped as hex.

        [YamlSerializable(typeof(MyPoco))]
        private partial class MyPocoContext : YamlSerializerContext
        {
        }

        private class MyPoco
        {
            public static MyPoco Create()
            {
                return new MyPoco { StringProp = "Hello", IntArrayProp = new int[] { 1, 2 } };
            }

            [YamlPropertyOrder(0)]
            public string StringProp { get; set; }

            [YamlPropertyOrder(1)]
            public int[] IntArrayProp { get; set; }

            public void Verify()
            {
                Assert.Equal("Hello", StringProp);
                Assert.Equal(1, IntArrayProp[0]);
                Assert.Equal(2, IntArrayProp[1]);
            }
        }

        public const string Yaml =
            "{\"StringProp\":\"Hello\",\"IntArrayProp\":[1,2]}";

        [Fact]
        public static void YamlDocumentDeserialize_Generic()
        {
            using YamlDocument dom = YamlDocument.Parse(Yaml);
            MyPoco obj = dom.Deserialize<MyPoco>();
            obj.Verify();
        }

        [Fact]
        public static void YamlDocumentDeserialize_NonGeneric()
        {
            using YamlDocument dom = YamlDocument.Parse(Yaml);
            MyPoco obj = (MyPoco)dom.Deserialize(typeof(MyPoco));
            obj.Verify();
        }

        [Fact]
        public static void YamlDocumentDeserialize_Null()
        {
            using YamlDocument dom = YamlDocument.Parse("null");
            MyPoco obj = dom.Deserialize<MyPoco>();
            Assert.Null(obj);
        }

        [Fact]
        public static void YamlElementDeserialize_Generic()
        {
            using YamlDocument document = YamlDocument.Parse(Yaml);
            YamlElement dom = document.RootElement;
            MyPoco obj = YamlSerializer.Deserialize<MyPoco>(dom);
            obj.Verify();
        }

        [Fact]
        public static void YamlElementDeserialize_NonGeneric()
        {
            using YamlDocument document = YamlDocument.Parse(Yaml);
            YamlElement dom = document.RootElement;
            MyPoco obj = (MyPoco)YamlSerializer.Deserialize(dom, typeof(MyPoco));
            obj.Verify();
        }

        [Fact]
        public static void YamlElementDeserialize_Null()
        {
            using YamlDocument document = YamlDocument.Parse("null");
            YamlElement dom = document.RootElement;
            MyPoco obj = dom.Deserialize<MyPoco>();
            Assert.Null(obj);
        }

        [Fact]
        public static void YamlElementDeserialize_FromChildElement()
        {
            using YamlDocument document = YamlDocument.Parse(Yaml);
            YamlElement dom = document.RootElement.GetProperty("IntArrayProp");
            int[] arr = YamlSerializer.Deserialize<int[]>(dom);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
        }

        [Fact]
        public static void YamlNodeDeserialize_Generic()
        {
            YamlNode dom = YamlNode.Parse(Yaml);
            MyPoco obj = dom.Deserialize<MyPoco>();
            obj.Verify();
        }

        [Fact]
        public static void YamlNodeDeserialize_NonGeneric()
        {
            YamlNode dom = YamlNode.Parse(Yaml);
            MyPoco obj = (MyPoco)dom.Deserialize(typeof(MyPoco));
            obj.Verify();
        }

        [Fact]
        public static void YamlNodeDeserialize_Null()
        {
            YamlNode node = null;
            MyPoco obj = YamlSerializer.Deserialize<MyPoco>(node);
            Assert.Null(obj);
        }

        [Fact]
        public static void YamlElementDeserialize_FromChildNode()
        {
            YamlNode dom = YamlNode.Parse(Yaml)["IntArrayProp"];
            int[] arr = YamlSerializer.Deserialize<int[]>(dom);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
        }

        [Fact]
        public static void SerializeToDocument()
        {
            MyPoco obj = MyPoco.Create();
            using YamlDocument dom = YamlSerializer.SerializeToDocument(obj);

            YamlElement stringProp = dom.RootElement.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());

            YamlElement[] elements = dom.RootElement.GetProperty("IntArrayProp").EnumerateArray().ToArray();
            Assert.Equal(YamlValueKind.Number, elements[0].ValueKind);
            Assert.Equal(1, elements[0].GetInt32());
            Assert.Equal(YamlValueKind.Number, elements[1].ValueKind);
            Assert.Equal(2, elements[1].GetInt32());
        }

        [Fact]
        public static void SerializeToElement()
        {
            MyPoco obj = MyPoco.Create();
            YamlElement dom = YamlSerializer.SerializeToElement(obj);

            YamlElement stringProp = dom.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());

            YamlElement[] elements = dom.GetProperty("IntArrayProp").EnumerateArray().ToArray();
            Assert.Equal(YamlValueKind.Number, elements[0].ValueKind);
            Assert.Equal(1, elements[0].GetInt32());
            Assert.Equal(YamlValueKind.Number, elements[1].ValueKind);
            Assert.Equal(2, elements[1].GetInt32());
        }

        [Fact]
        public static void SerializeToNode()
        {
            MyPoco obj = MyPoco.Create();
            YamlNode dom = YamlSerializer.SerializeToNode(obj);

            YamlNode stringProp = dom["StringProp"];
            Assert.True(stringProp is YamlValue);
            Assert.Equal("Hello", stringProp.AsValue().GetValue<string>());

            YamlNode arrayProp = dom["IntArrayProp"];
            Assert.IsType<YamlArray>(arrayProp);
            Assert.Equal(1, arrayProp[0].AsValue().GetValue<int>());
            Assert.Equal(2, arrayProp[1].AsValue().GetValue<int>());
        }

        [Theory]
        [InlineData(5)]
        [InlineData(32)]
        [InlineData(70)] // default max depth is 64
        public static void SerializeToNode_RespectsMaxDepth(int maxDepth)
        {
            var options = new YamlSerializerOptions { MaxDepth = maxDepth };

            RecursiveClass value = RecursiveClass.FromInt(maxDepth);
            YamlNode dom = YamlSerializer.SerializeToNode(value, options);

            value = RecursiveClass.FromInt(maxDepth + 1);
            Assert.Throws<YamlException>(() => YamlSerializer.SerializeToNode(value, options));
        }

        public class RecursiveClass
        {
            public RecursiveClass? Next { get; set; }
            public static RecursiveClass FromInt(int depth) => depth == 0 ? null : new RecursiveClass { Next = FromInt(depth - 1) };
            public static int ToInt(RecursiveClass value) => value is null ? 0 : 1 + ToInt(value.Next);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeToDocument_WithEscaping()
        {
            using YamlDocument document = YamlSerializer.SerializeToDocument("+");
            YamlElement dom = document.RootElement;
            Assert.Equal(YamlValueKind.String, dom.ValueKind);
            Assert.Equal(Escaped_PlusSign, dom.GetRawText());

            string yaml = dom.Deserialize<string>();
            Assert.Equal("+", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeToElement_WithEscaping()
        {
            YamlElement dom = YamlSerializer.SerializeToElement("+");
            Assert.Equal(YamlValueKind.String, dom.ValueKind);
            Assert.Equal(Escaped_PlusSign, dom.GetRawText());

            string yaml = dom.Deserialize<string>();
            Assert.Equal("+", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeToNode_WithEscaping()
        {
            YamlNode dom = YamlSerializer.SerializeToNode("+");
            Assert.Equal(Escaped_PlusSign, dom.ToYamlString());

            string yaml = dom.Deserialize<string>();
            Assert.Equal("+", yaml);
        }

        [Fact]
        public static void SerializeToDocument_WithYamlTypeInfo()
        {
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = MyPoco.Create();
            using YamlDocument dom = YamlSerializer.SerializeToDocument(obj, typeInfo);

            YamlElement stringProp = dom.RootElement.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToDocument_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            MyPoco obj = MyPoco.Create();
            using YamlDocument dom = YamlSerializer.SerializeToDocument(obj, typeInfo);

            YamlElement stringProp = dom.RootElement.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToElement_WithYamlTypeInfo()
        {
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = MyPoco.Create();
            YamlElement element = YamlSerializer.SerializeToElement(obj, typeInfo);

            YamlElement stringProp = element.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToElement_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            MyPoco obj = MyPoco.Create();
            YamlElement element = YamlSerializer.SerializeToElement(obj, typeInfo);

            YamlElement stringProp = element.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToNode_WithYamlTypeInfo()
        {
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = MyPoco.Create();
            YamlNode node = YamlSerializer.SerializeToNode(obj, typeInfo);

            Assert.NotNull(node);
            Assert.Equal("Hello", node["StringProp"]?.GetValue<string>());
        }

        [Fact]
        public static void SerializeToNode_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            MyPoco obj = MyPoco.Create();
            YamlNode node = YamlSerializer.SerializeToNode(obj, typeInfo);

            Assert.NotNull(node);
            Assert.Equal("Hello", node["StringProp"]?.GetValue<string>());
        }

        [Fact]
        public static void DeserializeFromSpan_WithYamlTypeInfo()
        {
            ReadOnlySpan<byte> utf8Yaml = """{"StringProp":"Hello","IntArrayProp":[1,2]}"""u8;
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = YamlSerializer.Deserialize(utf8Yaml, typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromSpan_WithYamlTypeInfo_NonGeneric()
        {
            ReadOnlySpan<byte> utf8Yaml = """{"StringProp":"Hello","IntArrayProp":[1,2]}"""u8;
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = YamlSerializer.Deserialize(utf8Yaml, typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void SerializeToDocument_NullValue_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            using YamlDocument dom = YamlSerializer.SerializeToDocument((object)null, typeInfo);
            Assert.Equal(YamlValueKind.Null, dom.RootElement.ValueKind);
        }

        [Fact]
        public static void SerializeToElement_NullValue_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            YamlElement element = YamlSerializer.SerializeToElement((object)null, typeInfo);
            Assert.Equal(YamlValueKind.Null, element.ValueKind);
        }

        [Fact]
        public static void SerializeToNode_NullValue_WithYamlTypeInfo_NonGeneric()
        {
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            YamlNode node = YamlSerializer.SerializeToNode((object)null, typeInfo);
            Assert.Null(node);
        }

        [Fact]
        public static void DeserializeFromSpan_NullValue()
        {
            ReadOnlySpan<byte> utf8Yaml = "null"u8;
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = YamlSerializer.Deserialize(utf8Yaml, typeInfo);
            Assert.Null(obj);
        }

        [Fact]
        public static void DeserializeFromYamlDocument_WithYamlTypeInfo()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = doc.Deserialize(typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromYamlDocument_WithYamlTypeInfo_NonGeneric()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = doc.Deserialize(typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromYamlElement_WithYamlTypeInfo()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            YamlElement element = doc.RootElement;
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = element.Deserialize(typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromYamlElement_WithYamlTypeInfo_NonGeneric()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            YamlElement element = doc.RootElement;
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = element.Deserialize(typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromYamlNode_WithYamlTypeInfo()
        {
            YamlNode node = YamlNode.Parse(Yaml);
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = node.Deserialize(typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromYamlNode_WithYamlTypeInfo_NonGeneric()
        {
            YamlNode node = YamlNode.Parse(Yaml);
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = node.Deserialize(typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromCharSpan_WithYamlTypeInfo()
        {
            ReadOnlySpan<char> yamlChars = Yaml.AsSpan();
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = YamlSerializer.Deserialize(yamlChars, typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromCharSpan_WithYamlTypeInfo_NonGeneric()
        {
            ReadOnlySpan<char> yamlChars = Yaml.AsSpan();
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = YamlSerializer.Deserialize(yamlChars, typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void SerializeToUtf8Bytes_WithYamlTypeInfo_NonGeneric()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            byte[] bytes = YamlSerializer.SerializeToUtf8Bytes(obj, typeInfo);
            string yaml = Encoding.UTF8.GetString(bytes);
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static void SerializeToStream_WithYamlTypeInfo_NonGeneric()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            using MemoryStream stream = new();
            YamlSerializer.Serialize(stream, obj, typeInfo);
            
            stream.Position = 0;
            string yaml = new StreamReader(stream).ReadToEnd();
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static void SerializeToUtf8YamlWriter_WithYamlTypeInfo_NonGeneric()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            using MemoryStream stream = new();
            using (Utf8YamlWriter writer = new(stream))
            {
                YamlSerializer.Serialize(writer, obj, typeInfo);
            }
            
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static void DeserializeFromUtf8YamlReader_WithYamlTypeInfo()
        {
            byte[] utf8Yaml = Encoding.UTF8.GetBytes(Yaml);
            Utf8YamlReader reader = new(utf8Yaml);
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = YamlSerializer.Deserialize(ref reader, typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromUtf8YamlReader_WithYamlTypeInfo_NonGeneric()
        {
            byte[] utf8Yaml = Encoding.UTF8.GetBytes(Yaml);
            Utf8YamlReader reader = new(utf8Yaml);
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = YamlSerializer.Deserialize(ref reader, typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromStream_WithYamlTypeInfo()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(Yaml));
            
            YamlTypeInfo<MyPoco> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<MyPoco>();
            
            MyPoco obj = YamlSerializer.Deserialize(stream, typeInfo);
            obj.Verify();
        }

        [Fact]
        public static void DeserializeFromStream_WithYamlTypeInfo_NonGeneric()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(Yaml));
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            object obj = YamlSerializer.Deserialize(stream, typeInfo);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void SerializeToDocument_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            using YamlDocument dom = YamlSerializer.SerializeToDocument(obj, typeof(MyPoco), MyPocoContext.Default);

            YamlElement stringProp = dom.RootElement.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToElement_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlElement element = YamlSerializer.SerializeToElement(obj, typeof(MyPoco), MyPocoContext.Default);

            YamlElement stringProp = element.GetProperty("StringProp");
            Assert.Equal(YamlValueKind.String, stringProp.ValueKind);
            Assert.Equal("Hello", stringProp.ToString());
        }

        [Fact]
        public static void SerializeToNode_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlNode node = YamlSerializer.SerializeToNode(obj, typeof(MyPoco), MyPocoContext.Default);

            Assert.NotNull(node);
            Assert.Equal("Hello", node["StringProp"]?.GetValue<string>());
        }

        [Fact]
        public static void DeserializeFromSpan_WithYamlSerializerContext()
        {
            ReadOnlySpan<byte> utf8Yaml = """{"StringProp":"Hello","IntArrayProp":[1,2]}"""u8;
            
            object obj = YamlSerializer.Deserialize(utf8Yaml, typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromCharSpan_WithYamlSerializerContext()
        {
            ReadOnlySpan<char> yamlChars = Yaml.AsSpan();
            
            object obj = YamlSerializer.Deserialize(yamlChars, typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromYamlDocument_WithYamlSerializerContext()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            
            object obj = doc.Deserialize(typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromYamlElement_WithYamlSerializerContext()
        {
            using YamlDocument doc = YamlDocument.Parse(Yaml);
            YamlElement element = doc.RootElement;
            
            object obj = element.Deserialize(typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromYamlNode_WithYamlSerializerContext()
        {
            YamlNode node = YamlNode.Parse(Yaml);
            
            object obj = node.Deserialize(typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromUtf8YamlReader_WithYamlSerializerContext()
        {
            byte[] utf8Yaml = Encoding.UTF8.GetBytes(Yaml);
            Utf8YamlReader reader = new(utf8Yaml);
            
            object obj = YamlSerializer.Deserialize(ref reader, typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void DeserializeFromStream_WithYamlSerializerContext()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(Yaml));
            
            object obj = YamlSerializer.Deserialize(stream, typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }

        [Fact]
        public static void SerializeToUtf8Bytes_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            byte[] bytes = YamlSerializer.SerializeToUtf8Bytes(obj, typeof(MyPoco), MyPocoContext.Default);
            string yaml = Encoding.UTF8.GetString(bytes);
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static void SerializeToStream_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            using MemoryStream stream = new();
            YamlSerializer.Serialize(stream, obj, typeof(MyPoco), MyPocoContext.Default);
            
            stream.Position = 0;
            string yaml = new StreamReader(stream).ReadToEnd();
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static void SerializeToUtf8YamlWriter_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            using MemoryStream stream = new();
            using (Utf8YamlWriter writer = new(stream))
            {
                YamlSerializer.Serialize(writer, obj, typeof(MyPoco), MyPocoContext.Default);
            }
            
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static async Task SerializeAsyncToPipeWriter_WithYamlTypeInfo()
        {
            MyPoco obj = MyPoco.Create();
            
            YamlTypeInfo typeInfo = YamlSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
            
            Pipe pipe = new();
            await YamlSerializer.SerializeAsync(pipe.Writer, obj, typeInfo);
            await pipe.Writer.CompleteAsync();
            
            ReadResult result = await pipe.Reader.ReadAsync();
            string yaml = Encoding.UTF8.GetString(result.Buffer.First.Span.ToArray());
            pipe.Reader.Complete();
            
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static async Task SerializeAsyncToPipeWriter_WithYamlSerializerContext()
        {
            MyPoco obj = MyPoco.Create();
            
            Pipe pipe = new();
            await YamlSerializer.SerializeAsync(pipe.Writer, obj, typeof(MyPoco), MyPocoContext.Default);
            await pipe.Writer.CompleteAsync();
            
            ReadResult result = await pipe.Reader.ReadAsync();
            string yaml = Encoding.UTF8.GetString(result.Buffer.First.Span.ToArray());
            pipe.Reader.Complete();
            
            Assert.Contains("Hello", yaml);
        }

        [Fact]
        public static async Task DeserializeAsyncFromPipeReader_WithYamlSerializerContext()
        {
            Pipe pipe = new();
            byte[] utf8Yaml = Encoding.UTF8.GetBytes(Yaml);
            await pipe.Writer.WriteAsync(utf8Yaml);
            await pipe.Writer.CompleteAsync();
            
            object obj = await YamlSerializer.DeserializeAsync(pipe.Reader, typeof(MyPoco), MyPocoContext.Default);
            Assert.IsType<MyPoco>(obj);
            ((MyPoco)obj).Verify();
        }
    }
}
