// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class YamlDocumentTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeYamlDocument()
        {
            using YamlDocumentClass obj = new YamlDocumentClass();
            obj.Document = YamlSerializer.Deserialize<YamlDocument>(YamlDocumentClass.s_yaml);
            obj.Verify();
            string reserialized = YamlSerializer.Serialize(obj.Document);

            // Properties in the exported json will be in the order that they were reflected, doing a quick check to see that
            // we end up with the same length (i.e. same amount of data) to start.
            Assert.Equal(YamlDocumentClass.s_yaml.StripWhitespace().Length, reserialized.Length);

            // Shoving it back through the parser should validate round tripping.
            obj.Document = YamlSerializer.Deserialize<YamlDocument>(reserialized);
            obj.Verify();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeYamlElementArray()
        {
            using YamlDocumentArrayClass obj = new YamlDocumentArrayClass();
            obj.Document = YamlSerializer.Deserialize<YamlDocument>(YamlDocumentArrayClass.s_yaml);
            obj.Verify();
            string reserialized = YamlSerializer.Serialize(obj.Document);

            // Properties in the exported json will be in the order that they were reflected, doing a quick check to see that
            // we end up with the same length (i.e. same amount of data) to start.
            Assert.Equal(YamlDocumentArrayClass.s_yaml.StripWhitespace().Length, reserialized.Length);

            // Shoving it back through the parser should validate round tripping.
            obj.Document = YamlSerializer.Deserialize<YamlDocument>(reserialized);
            obj.Verify();
        }

        [Theory,
            InlineData(5),
            InlineData(10),
            InlineData(20),
            InlineData(1024)]
        public void ReadYamlDocumentFromStream(int defaultBufferSize)
        {
            // Streams need to read ahead when they hit objects or arrays that are assigned to YamlElement or object.

            byte[] data = Encoding.UTF8.GetBytes("""{"Data":[1,true,{"City":"MyCity"},null,"foo"]}""");
            MemoryStream stream = new MemoryStream(data);
            YamlDocument obj = YamlSerializer.DeserializeAsync<YamlDocument>(stream, new YamlSerializerOptions { DefaultBufferSize = defaultBufferSize }).Result;

            data = Encoding.UTF8.GetBytes("""[1,true,{"City":"MyCity"},null,"foo"]""");
            stream = new MemoryStream(data);
            obj = YamlSerializer.DeserializeAsync<YamlDocument>(stream, new YamlSerializerOptions { DefaultBufferSize = defaultBufferSize }).Result;

            // Ensure we fail with incomplete data
            data = Encoding.UTF8.GetBytes("""{"Data":[1,true,{"City":"MyCity"},null,"foo"]""");
            stream = new MemoryStream(data);
            Assert.Throws<YamlException>(() => YamlSerializer.DeserializeAsync<YamlDocument>(stream, new YamlSerializerOptions { DefaultBufferSize = defaultBufferSize }).Result);

            data = Encoding.UTF8.GetBytes("""
                [1,true,{"City":"MyCity"},null,"foo"
                """);
            stream = new MemoryStream(data);
            Assert.Throws<YamlException>(() => YamlSerializer.DeserializeAsync<YamlDocument>(stream, new YamlSerializerOptions { DefaultBufferSize = defaultBufferSize }).Result);
        }

        [Theory]
        [MemberData(nameof(TestData.DuplicatePropertyYamlPayloads), MemberType = typeof(TestData))]
        public void DserializeYamlDocumentWithDuplicateProperties(string yamlPayload, bool isValidYaml = false)
        {
            AssertDuplicateProperty<YamlDocument>(yamlPayload, isValidYaml);
        }

        [Theory]
        [MemberData(nameof(TestData.DuplicatePropertyYamlPayloads), MemberType = typeof(TestData))]
        public void DserializeYamlDocumentArrayWithDuplicateProperties(string yamlPayload, bool isValidYaml = false)
        {
            yamlPayload = $"[{yamlPayload}]";
            AssertDuplicateProperty<YamlDocument>(yamlPayload, isValidYaml);
        }

        [Theory]
        [MemberData(nameof(TestData.DuplicatePropertyYamlPayloads), MemberType = typeof(TestData))]
        public static void DserializeYamlDocumentDeeplyNestedWithDuplicateProperties(string yamlPayload, bool isValidYaml = false)
        {
            yamlPayload = $$"""{"p0":{"p1":{"p2":{"p3":{"p4":{"p5":{"p6":{"p7":{"p8":{"p9":{{yamlPayload}}} } } } } } } } } }""";
            AssertDuplicateProperty<YamlDocument>(yamlPayload, isValidYaml);
        }

        [Theory]
        [MemberData(nameof(TestData.DuplicatePropertyYamlPayloads), MemberType = typeof(TestData))]
        public void DserializeYamlDocumentClassWithDuplicateProperties(string yamlPayload, bool isValidYaml = false)
        {
            yamlPayload = $$"""{"Object":{{yamlPayload}}}""";
            AssertDuplicateProperty<YamlDocument>(yamlPayload, isValidYaml);
        }

        private static void AssertDuplicateProperty<T>(string yamlPayload, bool isValidYaml)
        {
            if (isValidYaml)
            {
                _ = YamlSerializer.Deserialize<T>(yamlPayload, YamlTestSerializerOptions.DisallowDuplicateProperties); // Assert no throw
            }
            else
            {
                Exception ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<T>(yamlPayload, YamlTestSerializerOptions.DisallowDuplicateProperties));
                Assert.Contains("Duplicate", ex.Message);
            }

            _ = YamlSerializer.Deserialize<T>(yamlPayload, YamlTestSerializerOptions.LastWins); // Assert no throw (M7a: LastWins, the JSON grammar's default)
        }

        [Fact]
        public void DserializeYamlDocument_CaseInsensitiveWithDuplicatePropertiesNoThrow()
        {
            string yamlPayload = """{"a": 1, "A": 2}""";

            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;
            YamlDocument doc = YamlSerializer.Deserialize<YamlDocument>(yamlPayload, options);

            // YamlDocument is always case-sensitive
            Assert.Equal(1, doc.RootElement.GetProperty("a").GetInt32());
            Assert.Equal(2, doc.RootElement.GetProperty("A").GetInt32());
        }

        [Fact]
        public void DserializeYamlDocumentWrapper_CaseInsensitiveWithDuplicatePropertiesNoThrow()
        {
            string yamlPayload = """{"document": {"a": 1, "A": 2}}""";

            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;
            YamlDocumentClass obj = YamlSerializer.Deserialize<YamlDocumentClass>(yamlPayload, options);

            // YamlDocument is always case-sensitive
            Assert.Equal(1, obj.Document.RootElement.GetProperty("a").GetInt32());
            Assert.Equal(2, obj.Document.RootElement.GetProperty("A").GetInt32());
        }
    }
}
