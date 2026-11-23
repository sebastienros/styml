// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class SpanTests
    {
        [Fact]
        public static void ParseNullTypeFail()
        {
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize(new ReadOnlySpan<byte>(), (Type)null));
        }

        [Theory]
        [MemberData(nameof(ReadSuccessCases))]
        public static void Read(Type classType, byte[] data)
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            object obj = YamlSerializer.Deserialize(data, classType, options);
            Assert.IsAssignableFrom<ITestClass>(obj);
            ((ITestClass)obj).Verify();
        }

        [Theory]
        [MemberData(nameof(ReadSuccessCases))]
        public static void ReadFromStream(Type classType, byte[] data)
        {
            MemoryStream stream = new MemoryStream(data);
            var options = new YamlSerializerOptions { IncludeFields = true };
            object obj = YamlSerializer.DeserializeAsync(
                stream,
                classType,
                options).Result;

            Assert.IsAssignableFrom<ITestClass>(obj);
            ((ITestClass)obj).Verify();

            // Try again with a smaller initial buffer size to ensure we handle incomplete data
            stream = new MemoryStream(data);
            obj = YamlSerializer.DeserializeAsync(
                stream,
                classType,
                new YamlSerializerOptions { DefaultBufferSize = 5, IncludeFields = true }).Result;

            Assert.IsAssignableFrom<ITestClass>(obj);
            ((ITestClass)obj).Verify();
        }

        [Fact]
        public static void ReadGenericApi()
        {
            SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(SimpleTestClass.s_data);
            obj.Verify();
        }

        [Fact]
        public static void ParseUntyped()
        {
            object obj = YamlSerializer.Deserialize("42"u8, typeof(object));
            Assert.IsType<YamlElement>(obj);
            YamlElement element = (YamlElement)obj;
            Assert.Equal(YamlValueKind.Number, element.ValueKind);
            Assert.Equal(42, element.GetInt32());
        }

        [Fact]
        public static void ToStringNullTypeFail()
        {
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(new object(), (Type)null));
        }

        [Fact]
        public static void VerifyTypeFail()
        {
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(1, typeof(string)));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullObjectOutput()
        {
            byte[] encodedNull = Encoding.UTF8.GetBytes(@"null");

            {
                Assert.Throws<ArgumentNullException>(() => YamlSerializer.SerializeToUtf8Bytes(null, null));
            }

            {
                byte[] output = YamlSerializer.SerializeToUtf8Bytes(null, typeof(NullTests));
                Assert.Equal(encodedNull, output);
            }
        }

        public static IEnumerable<object[]> ReadSuccessCases
        {
            get
            {
                return TestData.ReadSuccessCases;
            }
        }
    }
}
