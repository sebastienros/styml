// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class NullTests
    {
        [Fact]
        public static void ClassWithNullProperty()
        {
            TestClassWithNull obj = YamlSerializer.Deserialize<TestClassWithNull>(TestClassWithNull.s_yaml);
            obj.Verify();
        }

        [Fact]
        public static void RootObjectIsNull()
        {
            {
                TestClassWithNull obj = YamlSerializer.Deserialize<TestClassWithNull>("null");
                Assert.Null(obj);
            }

            {
                object obj = YamlSerializer.Deserialize<object>("null");
                Assert.Null(obj);
            }

            {
                string obj = YamlSerializer.Deserialize<string>("null");
                Assert.Null(obj);
            }

            {
                IEnumerable<int> obj = YamlSerializer.Deserialize<IEnumerable<int>>("null");
                Assert.Null(obj);
            }

            {
                Dictionary<string, object> obj = YamlSerializer.Deserialize<Dictionary<string, object>>("null");
                Assert.Null(obj);
            }

        }

        [Fact]
        public static void RootArrayIsNull()
        {
            {
                int[] obj = YamlSerializer.Deserialize<int[]>("null");
                Assert.Null(obj);
            }

            {
                object[] obj = YamlSerializer.Deserialize<object[]>("null");
                Assert.Null(obj);
            }

            {
                TestClassWithNull[] obj = YamlSerializer.Deserialize<TestClassWithNull[]>("null");
                Assert.Null(obj);
            }
        }

        [Fact]
        public static void DefaultIgnoreNullValuesOnRead()
        {
            TestClassWithInitializedProperties obj = YamlSerializer.Deserialize<TestClassWithInitializedProperties>(TestClassWithInitializedProperties.s_null_json);
            Assert.Null(obj.MyString);
            Assert.Null(obj.MyInt);
            Assert.Null(obj.MyDateTime);
            Assert.Null(obj.MyIntArray);
            Assert.Null(obj.MyIntList);
            Assert.Null(obj.MyNullableIntList);
            Assert.Null(obj.MyObjectList[0]);
            Assert.Null(obj.MyListList[0][0]);
            Assert.Null(obj.MyDictionaryList[0]["key"]);
            Assert.Null(obj.MyStringDictionary["key"]);
            Assert.Null(obj.MyNullableDateTimeDictionary["key"]);
            Assert.Null(obj.MyObjectDictionary["key"]);
            Assert.Null(obj.MyStringDictionaryDictionary["key"]["key"]);
            Assert.Null(obj.MyListDictionary["key"][0]);
            Assert.Null(obj.MyObjectDictionaryDictionary["key"]["key"]);
        }

        [Fact]
        public static void ParseNullArgumentFail()
        {
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize<string>((string)null));
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize("1", (Type)null));
        }

        [Fact]
        public static void NullLiteralObjectInput()
        {
            {
                string obj = YamlSerializer.Deserialize<string>("null");
                Assert.Null(obj);
            }

            {
                string obj = YamlSerializer.Deserialize<string>("""
                    "null"
                    """);
                Assert.Equal("null", obj);
            }
        }

        [Fact]
        public static void NullAcceptsLeadingAndTrailingTrivia()
        {
            {
                TestClassWithNull obj = YamlSerializer.Deserialize<TestClassWithNull>(" null");
                Assert.Null(obj);
            }

            {
                object obj = YamlSerializer.Deserialize<object>("null ");
                Assert.Null(obj);
            }

            {
                object obj = YamlSerializer.Deserialize<object>(" null\t");
                Assert.Null(obj);
            }
        }

        [Fact]
        public static void NullReadTestChar()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("null"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("\"\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>(""));   // Empty JSON is invalid
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("1234")); // Can't convert a JSON number to JSON string/char
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("\"stringTooLong\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("\"\u0059B\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<char>("\"\uD800\uDC00\""));
            Assert.Equal('a', YamlSerializer.Deserialize<char>("\"a\""));
            Assert.Equal('Y', YamlSerializer.Deserialize<char>("\"\u0059\""));
        }

        [Fact]
        public static void ParseNullStringToStructShouldThrowYamlException()
        {
            string nullString = "null";
            byte[] nullStringAsBytes = Encoding.UTF8.GetBytes(nullString);

            Utf8YamlReader reader = new Utf8YamlReader(nullStringAsBytes);

            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader reader) => YamlSerializer.Deserialize<SimpleStruct>(ref reader));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<SimpleStruct>(nullStringAsBytes));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<SimpleStruct>(nullString));

            // null can be assigned to nullable structs.
            Assert.Null(YamlSerializer.Deserialize<SimpleStruct?>(nullStringAsBytes));
            Assert.Null(YamlSerializer.Deserialize<SimpleStruct?>(nullString));
        }

        [Fact]
        public static async Task ParseNullStringShouldThrowYamlExceptionAsync()
        {
            using (var stream = new MemoryStream("null"u8.ToArray()))
            { 
                await Assert.ThrowsAsync<YamlException>(async () => await YamlSerializer.DeserializeAsync<SimpleStruct>(stream));

                // null can be assigned to nullable structs.
                stream.Position = 0;
                Assert.Null(await YamlSerializer.DeserializeAsync<SimpleStruct?>(stream));
            }
        }

        [Fact]
        public static void DeserializeDictionaryWithNullValues()
        {
            {
                Dictionary<string, string> dict = YamlSerializer.Deserialize<Dictionary<string, string>>("""{"key":null}""");
                Assert.Null(dict["key"]);
            }

            {
                Dictionary<string, object> dict = YamlSerializer.Deserialize<Dictionary<string, object>>("""{"key":null}""");
                Assert.Null(dict["key"]);
            }

            {
                Dictionary<string, Dictionary<string, string>> dict = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>("""{"key":null}""");
                Assert.Null(dict["key"]);
            }

            {
                Dictionary<string, Dictionary<string, object>> dict = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>("""{"key":null}""");
                Assert.Null(dict["key"]);
            }
        }

        [Theory]
        [InlineData(typeof(UIntPtr))]
        [InlineData(typeof(IntPtr))]
        [InlineData(typeof(Type))]
        [InlineData(typeof(int[,]))]
        [InlineData(typeof(System.Delegate))]
        public static void InvalidRootOnRead(Type type)
        {
            if (type.IsValueType)
            {
                // Unsupported struct types throw
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize("null", type));
            }
            else
            {
                // Unsupported reference types handle null values.
                Assert.Null(YamlSerializer.Deserialize("null", type));
            }
        }

        [Theory]
        [MemberData(nameof(GetBuiltInConvertersForNullableTypes))]
        public static void ReadNullValue_BuiltInConverter<T>(YamlConverter<T> converter)
        {
            var reader = new Utf8YamlReader("null"u8);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.Null, reader.TokenType);

            T? result = converter.Read(ref reader, typeof(T), YamlSerializerOptions.Default);
            Assert.True(result is null or YamlDocument { RootElement.ValueKind: YamlValueKind.Null } or YamlElement { ValueKind: YamlValueKind.Null });
        }

        [Theory]
        [MemberData(nameof(GetBuiltInConvertersForNullableTypes))]
        public static void DeserializeNullValue_BuiltInConverter<T>(YamlConverter<T> converter)
        {
            _ = converter; // Not needed here.

            T? value = YamlSerializer.Deserialize<T>("null");
            AssertNull(value);

            T[]? array = YamlSerializer.Deserialize<T[]>("[null]");
            AssertNull(array[0]);

            List<T>? list = YamlSerializer.Deserialize<List<T>>("[null]");
            AssertNull(list[0]);

            GenericRecord<T>? record = YamlSerializer.Deserialize<GenericRecord<T>>("""{"Value":null}""");
            AssertNull(record.Value);

            Dictionary<string, T>? dictionary = YamlSerializer.Deserialize<Dictionary<string, T>>("""{"Key":null}""");
            AssertNull(dictionary["Key"]);

            static void AssertNull(T? result) => Assert.True(
                result is null
                       or YamlDocument { RootElement.ValueKind: YamlValueKind.Null }
                       or YamlElement { ValueKind: YamlValueKind.Null });
        }
    }
}
