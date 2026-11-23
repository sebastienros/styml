// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ArrayTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritePrimitiveArray()
        {
            var input = new int[] { 0, 1 };
            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal("[0,1]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteArrayWithEnums()
        {
            var input = new SampleEnum[] { SampleEnum.One, SampleEnum.Two };
            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNullByteArray()
        {
            byte[] input = null;
            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal($"null", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteEmptyByteArray()
        {
            var input = new byte[] {};
            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal("""
                ""
                """, yaml);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(new byte[] { }, "\"\"")]
        [InlineData(new byte[] { 1, 2 }, "\"AQI=\"")]
        [Trait("category", "jsonoutput")]
        public static void WriteByteArray(byte[]? input, string expectedEncoding)
        {
            // root-level serialization
            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal(expectedEncoding, yaml);

            // object property
            yaml = YamlSerializer.Serialize(new { Property = input });
            Assert.Equal(@$"{{""Property"":{expectedEncoding}}}", yaml);

            // array element
            yaml = YamlSerializer.Serialize(new[] { input });
            Assert.Equal($"[{expectedEncoding}]", yaml);

            // dictionary entry
            yaml = YamlSerializer.Serialize(new Dictionary<string, byte[]> { ["key"] = input });
            Assert.Equal(@$"{{""key"":{expectedEncoding}}}", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteTwo2dByteArray()
        {
            var inner = new byte[] { 1, 2 };
            var outer = new byte[2][] { inner, inner };
            string yaml = YamlSerializer.Serialize(outer);
            Assert.Equal($"[\"{Convert.ToBase64String(inner)}\",\"{Convert.ToBase64String(inner)}\"]", yaml);
        }

        [Fact]
        public static void WriteObjectArray()
        {
            string yaml;

            {
                SimpleTestClass[] input = new SimpleTestClass[] { new SimpleTestClass(), new SimpleTestClass() };
                input[0].Initialize();
                input[0].Verify();

                input[1].Initialize();
                input[1].Verify();

                yaml = YamlSerializer.Serialize(input);
            }

            {
                SimpleTestClass[] output = YamlSerializer.Deserialize<SimpleTestClass[]>(yaml);
                Assert.Equal(2, output.Length);
                output[0].Verify();
                output[1].Verify();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteEmptyObjectArray()
        {
            object[] arr = new object[] { new object() };

            string yaml = YamlSerializer.Serialize(arr);
            Assert.Equal("[{}]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritePrimitiveJaggedArray()
        {
            var input = new int[2][];
            input[0] = new int[] { 1, 2 };
            input[1] = new int[] { 3, 4 };

            string yaml = YamlSerializer.Serialize(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteEmpty()
        {
            string yaml = YamlSerializer.Serialize(new SimpleTestClass[] { });
            Assert.Equal("[]", yaml);

            yaml = YamlSerializer.Serialize(new List<SimpleTestClass>());
            Assert.Equal("[]", yaml);
        }

        [Fact]
        public static void WriteClassWithStringArray()
        {
            string yaml;

            {
                TestClassWithStringArray obj = new TestClassWithStringArray();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithStringArray obj = YamlSerializer.Deserialize<TestClassWithStringArray>(yaml);
                obj.Verify();
            }

            {
                TestClassWithStringArray obj = YamlSerializer.Deserialize<TestClassWithStringArray>(TestClassWithStringArray.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectArray()
        {
            string yaml;

            {
                TestClassWithObjectList obj = new TestClassWithObjectList();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectList obj = YamlSerializer.Deserialize<TestClassWithObjectList>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectList obj = YamlSerializer.Deserialize<TestClassWithObjectList>(TestClassWithObjectList.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericList()
        {
            string yaml;

            {
                TestClassWithGenericList obj = new TestClassWithGenericList();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericList obj = YamlSerializer.Deserialize<TestClassWithGenericList>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericList obj = YamlSerializer.Deserialize<TestClassWithGenericList>(TestClassWithGenericList.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericIEnumerableT()
        {
            string yaml;

            {
                TestClassWithGenericIEnumerableT obj = new TestClassWithGenericIEnumerableT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericIEnumerableT obj = YamlSerializer.Deserialize<TestClassWithGenericIEnumerableT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericIEnumerableT obj = YamlSerializer.Deserialize<TestClassWithGenericIEnumerableT>(TestClassWithGenericIEnumerableT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericIListT()
        {
            string yaml;

            {
                TestClassWithGenericIListT obj = new TestClassWithGenericIListT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericIListT obj = YamlSerializer.Deserialize<TestClassWithGenericIListT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericIListT obj = YamlSerializer.Deserialize<TestClassWithGenericIListT>(TestClassWithGenericIListT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericICollectionT()
        {
            string yaml;

            {
                TestClassWithGenericICollectionT obj = new TestClassWithGenericICollectionT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericICollectionT obj = YamlSerializer.Deserialize<TestClassWithGenericICollectionT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericICollectionT obj = YamlSerializer.Deserialize<TestClassWithGenericICollectionT>(TestClassWithGenericICollectionT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericIReadOnlyCollectionT()
        {
            string yaml;

            {
                TestClassWithGenericIReadOnlyCollectionT obj = new TestClassWithGenericIReadOnlyCollectionT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericIReadOnlyCollectionT obj = YamlSerializer.Deserialize<TestClassWithGenericIReadOnlyCollectionT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericIReadOnlyCollectionT obj = YamlSerializer.Deserialize<TestClassWithGenericIReadOnlyCollectionT>(TestClassWithGenericIReadOnlyCollectionT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithGenericIReadOnlyListT()
        {
            string yaml;

            {
                TestClassWithGenericIReadOnlyListT obj = new TestClassWithGenericIReadOnlyListT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithGenericIReadOnlyListT obj = YamlSerializer.Deserialize<TestClassWithGenericIReadOnlyListT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithGenericIReadOnlyListT obj = YamlSerializer.Deserialize<TestClassWithGenericIReadOnlyListT>(TestClassWithGenericIEnumerableT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectIEnumerableT()
        {
            string yaml;

            {
                TestClassWithObjectIEnumerableT obj = new TestClassWithObjectIEnumerableT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectIEnumerableT obj = YamlSerializer.Deserialize<TestClassWithObjectIEnumerableT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectIEnumerableT obj = YamlSerializer.Deserialize<TestClassWithObjectIEnumerableT>(TestClassWithObjectIEnumerableT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectIListT()
        {
            string yaml;

            {
                TestClassWithObjectIListT obj = new TestClassWithObjectIListT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectIListT obj = YamlSerializer.Deserialize<TestClassWithObjectIListT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectIListT obj = YamlSerializer.Deserialize<TestClassWithObjectIListT>(TestClassWithObjectIListT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectICollectionT()
        {
            string yaml;

            {
                TestClassWithObjectICollectionT obj = new TestClassWithObjectICollectionT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectICollectionT obj = YamlSerializer.Deserialize<TestClassWithObjectICollectionT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectICollectionT obj = YamlSerializer.Deserialize<TestClassWithObjectICollectionT>(TestClassWithObjectICollectionT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectIReadOnlyCollectionT()
        {
            string yaml;

            {
                TestClassWithObjectIReadOnlyCollectionT obj = new TestClassWithObjectIReadOnlyCollectionT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectIReadOnlyCollectionT obj = YamlSerializer.Deserialize<TestClassWithObjectIReadOnlyCollectionT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectIReadOnlyCollectionT obj = YamlSerializer.Deserialize<TestClassWithObjectIReadOnlyCollectionT>(TestClassWithObjectIReadOnlyCollectionT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectIReadOnlyListT()
        {
            string yaml;

            {
                TestClassWithObjectIReadOnlyListT obj = new TestClassWithObjectIReadOnlyListT();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectIReadOnlyListT obj = YamlSerializer.Deserialize<TestClassWithObjectIReadOnlyListT>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectIReadOnlyListT obj = YamlSerializer.Deserialize<TestClassWithObjectIReadOnlyListT>(TestClassWithObjectIEnumerableT.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectIEnumerableConstructibleTypes()
        {
            string yaml;

            {
                TestClassWithObjectIEnumerableConstructibleTypes obj = new TestClassWithObjectIEnumerableConstructibleTypes();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectIEnumerableConstructibleTypes obj = YamlSerializer.Deserialize<TestClassWithObjectIEnumerableConstructibleTypes>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectIEnumerableConstructibleTypes obj = YamlSerializer.Deserialize<TestClassWithObjectIEnumerableConstructibleTypes>(TestClassWithObjectIEnumerableConstructibleTypes.s_data);
                obj.Verify();
            }
        }

        [Fact]
        public static void WriteClassWithObjectImmutableTypes()
        {
            string yaml;

            {
                TestClassWithObjectImmutableTypes obj = new TestClassWithObjectImmutableTypes();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj);
            }

            {
                TestClassWithObjectImmutableTypes obj = YamlSerializer.Deserialize<TestClassWithObjectImmutableTypes>(yaml);
                obj.Verify();
            }

            {
                TestClassWithObjectImmutableTypes obj = YamlSerializer.Deserialize<TestClassWithObjectImmutableTypes>(TestClassWithObjectImmutableTypes.s_data);
                obj.Verify();
            }
        }
    }
}
