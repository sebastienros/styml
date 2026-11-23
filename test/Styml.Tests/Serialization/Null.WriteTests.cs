// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class NullTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DefaultIgnoreNullValuesOnWrite()
        {
            var obj = new TestClassWithInitializedProperties
            {
                MyString = null,
                MyInt = null,
                MyDateTime = null,
                MyIntArray = null,
                MyIntList = null,
                MyNullableIntList = null,
                MyObjectList = new List<object> { null },
                MyListList = new List<List<object>> { new List<object> { null } },
                MyDictionaryList = new List<Dictionary<string, string>> { new Dictionary<string, string>() { ["key"] = null } },
                MyStringDictionary = new Dictionary<string, string>() { ["key"] = null },
                MyNullableDateTimeDictionary = new Dictionary<string, DateTime?>() { ["key"] = null },
                MyObjectDictionary = new Dictionary<string, object>() { ["key"] = null },
                MyStringDictionaryDictionary = new Dictionary<string, Dictionary<string, string>>() { ["key"] = null },
                MyListDictionary = new Dictionary<string, List<object>>() { ["key"] = null },
                MyObjectDictionaryDictionary = new Dictionary<string, Dictionary<string, object>>() { ["key"] = null }
            };

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "MyString":null
                """, yaml);
            Assert.Contains("""
                "MyInt":null
                """, yaml);
            Assert.Contains("""
                "MyDateTime":null
                """, yaml);
            Assert.Contains("""
                "MyIntArray":null
                """, yaml);
            Assert.Contains("""
                "MyIntList":null
                """, yaml);
            Assert.Contains("""
                "MyNullableIntList":null
                """, yaml);
            Assert.Contains("""
                "MyObjectList":[null],
                """, yaml);
            Assert.Contains("""
                "MyListList":[[null]],
                """, yaml);
            Assert.Contains("""
                "MyDictionaryList":[{"key":null}],
                """, yaml);
            Assert.Contains("""
                "MyStringDictionary":{"key":null},
                """, yaml);
            Assert.Contains("""
                "MyNullableDateTimeDictionary":{"key":null},
                """, yaml);
            Assert.Contains("""
                "MyObjectDictionary":{"key":null},
                """, yaml);
            Assert.Contains("""
                "MyStringDictionaryDictionary":{"key":null},
                """, yaml);
            Assert.Contains("""
                "MyListDictionary":{"key":null},
                """, yaml);
            Assert.Contains("""
                "MyObjectDictionaryDictionary":{"key":null}
                """, yaml);
        }

        [Fact]
        public static void EnableIgnoreNullValuesOnWrite()
        {
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull;

            var obj = new TestClassWithInitializedProperties
            {
                MyString = null,
                MyInt = null,
                MyDateTime = null,
                MyIntArray = null,
                MyIntList = null,
                MyNullableIntList = null,
                MyObjectList = new List<object> { null },
                MyListList = new List<List<object>> { new List<object> { null } },
                MyDictionaryList = new List<Dictionary<string, string>> { new Dictionary<string, string>() { ["key"] = null } },
                MyStringDictionary = new Dictionary<string, string>() { ["key"] = null },
                MyNullableDateTimeDictionary = new Dictionary<string, DateTime?>() { ["key"] = null },
                MyObjectDictionary = new Dictionary<string, object>() { ["key"] = null },
                MyStringDictionaryDictionary = new Dictionary<string, Dictionary<string, string>>() { ["key"] = new Dictionary<string, string>() { ["key"] = null } },
                MyListDictionary = new Dictionary<string, List<object>>() { ["key"] = new List<object> { null } },
                MyObjectDictionaryDictionary = new Dictionary<string, Dictionary<string, object>>() { ["key"] = new Dictionary<string, object>() { ["key"] = null } }
            };

            string yaml = YamlSerializer.Serialize(obj, options);

            // Roundtrip to verify serialize is accurate.
            TestClassWithInitializedProperties newObj = YamlSerializer.Deserialize<TestClassWithInitializedProperties>(yaml);
            Assert.Equal("Hello", newObj.MyString);
            Assert.Equal(1, newObj.MyInt);
            Assert.Equal(new DateTime(1995, 4, 16), newObj.MyDateTime);
            Assert.Equal(1, newObj.MyIntArray[0]);
            Assert.Equal(1, newObj.MyIntList[0]);
            Assert.Equal(1, newObj.MyNullableIntList[0]);

            Assert.Null(newObj.MyObjectList[0]);
            Assert.Null(newObj.MyObjectList[0]);
            Assert.Null(newObj.MyListList[0][0]);
            Assert.Null(newObj.MyDictionaryList[0]["key"]);
            Assert.Null(newObj.MyStringDictionary["key"]);
            Assert.Null(newObj.MyNullableDateTimeDictionary["key"]);
            Assert.Null(newObj.MyObjectDictionary["key"]);
            Assert.Null(newObj.MyStringDictionaryDictionary["key"]["key"]);
            Assert.Null(newObj.MyListDictionary["key"][0]);
            Assert.Null(newObj.MyObjectDictionaryDictionary["key"]["key"]);

            var parentObj = new WrapperForTestClassWithInitializedProperties
            {
                MyClass = obj
            };
            yaml = YamlSerializer.Serialize(parentObj, options);

            // Roundtrip to ensure serialize is accurate.
            WrapperForTestClassWithInitializedProperties newParentObj = YamlSerializer.Deserialize<WrapperForTestClassWithInitializedProperties>(yaml);
            TestClassWithInitializedProperties nestedObj = newParentObj.MyClass;
            Assert.Equal("Hello", nestedObj.MyString);
            Assert.Equal(1, nestedObj.MyInt);
            Assert.Equal(new DateTime(1995, 4, 16), nestedObj.MyDateTime);
            Assert.Equal(1, nestedObj.MyIntArray[0]);
            Assert.Equal(1, nestedObj.MyIntList[0]);
            Assert.Equal(1, nestedObj.MyNullableIntList[0]);

            Assert.Null(nestedObj.MyObjectList[0]);
            Assert.Null(nestedObj.MyObjectList[0]);
            Assert.Null(nestedObj.MyListList[0][0]);
            Assert.Null(nestedObj.MyDictionaryList[0]["key"]);
            Assert.Null(nestedObj.MyStringDictionary["key"]);
            Assert.Null(nestedObj.MyNullableDateTimeDictionary["key"]);
            Assert.Null(nestedObj.MyObjectDictionary["key"]);
            Assert.Null(nestedObj.MyStringDictionaryDictionary["key"]["key"]);
            Assert.Null(nestedObj.MyListDictionary["key"][0]);
            Assert.Null(nestedObj.MyObjectDictionaryDictionary["key"]["key"]);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullReferences()
        {
            var obj = new ObjectWithObjectProperties();
            obj.Address = null;
            obj.Array = null;
            obj.List = null;
            obj.IEnumerableT = null;
            obj.IListT = null;
            obj.ICollectionT = null;
            obj.IReadOnlyCollectionT = null;
            obj.IReadOnlyListT = null;
            obj.StackT = null;
            obj.QueueT = null;
            obj.HashSetT = null;
            obj.IReadOnlySetT = null;
            obj.LinkedListT = null;
            obj.SortedSetT = null;
            obj.NullableInt = null;
            obj.NullableIntArray = null;
            obj.Object = null;

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("""
                "Address":null
                """, yaml);
            Assert.Contains("""
                "List":null
                """, yaml);
            Assert.Contains("""
                "Array":null
                """, yaml);
            Assert.Contains("""
                "IEnumerableT":null
                """, yaml);
            Assert.Contains("""
                "IListT":null
                """, yaml);
            Assert.Contains("""
                "ICollectionT":null
                """, yaml);
            Assert.Contains("""
                "IReadOnlyCollectionT":null
                """, yaml);
            Assert.Contains("""
                "IReadOnlyListT":null
                """, yaml);
            Assert.Contains("""
                "StackT":null
                """, yaml);
            Assert.Contains("""
                "QueueT":null
                """, yaml);
            Assert.Contains("""
                "HashSetT":null
                """, yaml);
            Assert.Contains("""
                "IReadOnlySetT":null
                """, yaml);
            Assert.Contains("""
                "LinkedListT":null
                """, yaml);
            Assert.Contains("""
                "SortedSetT":null
                """, yaml);
            Assert.Contains("""
                "NullableInt":null
                """, yaml);
            Assert.Contains("""
                "Object":null
                """, yaml);
            Assert.Contains("""
                "NullableIntArray":null
                """, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullArrayElement()
        {
            string yaml = YamlSerializer.Serialize(new ObjectWithObjectProperties[]{ null });
            Assert.Equal("[null]", yaml);
        }

        [Fact]
        public static void NullArgumentFail()
        {
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize("", (Type)null));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullObjectOutput()
        {
            {
                string output = YamlSerializer.Serialize<string>(null);
                Assert.Equal("null", output);
            }

            {
                string output = YamlSerializer.Serialize<string>(null, options: null);
                Assert.Equal("null", output);
            }
        }

        class WrapperForTestClassWithInitializedProperties
        {
            public TestClassWithInitializedProperties MyClass { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeDictionaryWithNullValues()
        {
            Dictionary<string, string> StringVals = new Dictionary<string, string>()
            {
                ["key"] = null,
            };
            Assert.Equal("""{"key":null}""", YamlSerializer.Serialize(StringVals));

            Dictionary<string, object> ObjVals = new Dictionary<string, object>()
            {
                ["key"] = null,
            };
            Assert.Equal("""{"key":null}""", YamlSerializer.Serialize(ObjVals));

            Dictionary<string, Dictionary<string, string>> StringDictVals = new Dictionary<string, Dictionary<string, string>>()
            {
                ["key"] = null,
            };
            Assert.Equal("""{"key":null}""", YamlSerializer.Serialize(StringDictVals));

            Dictionary<string, Dictionary<string, object>> ObjectDictVals = new Dictionary<string, Dictionary<string, object>>()
            {
                ["key"] = null,
            };
            Assert.Equal("""{"key":null}""", YamlSerializer.Serialize(ObjectDictVals));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritePocoArray()
        {
            var input = new MyPoco[] { null, new MyPoco { Foo = "foo" } };

            string yaml = YamlSerializer.Serialize(input, new YamlSerializerOptions { Converters = { new MyPocoConverter() } });
            Assert.Equal("[null,{\"Foo\":\"foo\"}]", yaml);
        }

        private class MyPoco
        {
            public string Foo { get; set; }
        }

        private class MyPocoConverter : YamlConverter<MyPoco>
        {
            public override MyPoco Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, MyPoco value, YamlSerializerOptions options)
            {
                if (value == null)
                {
                    throw new InvalidOperationException("The custom converter should never get called with null value.");
                }

                writer.WriteStartObject();
                writer.WriteString(nameof(value.Foo), value.Foo);
                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void InvalidRootOnWrite()
        {
            int[,] arr = null;
            string yaml = YamlSerializer.Serialize<int[,]>(arr);
            Assert.Equal("null", yaml);

            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull
            };

            // We still serialize when we have an unsupported root.
            yaml = YamlSerializer.Serialize<int[,]>(arr, options);
            Assert.Equal("null", yaml);
        }

        [Theory]
        [MemberData(nameof(GetBuiltInConvertersForNullableTypes))]
        [Trait("category", "jsonoutput")]
        public static void WriteNullValue_BuiltInConverter<T>(YamlConverter<T> converter)
        {
            T @null = default;
            Assert.Null(@null);

            using var stream = new Utf8MemoryStream();
            using var writer = new Utf8YamlWriter(stream);

            converter.Write(writer, @null, YamlSerializerOptions.Default);
            writer.Flush();

            Assert.Equal("null", stream.AsString());
        }

        [Theory]
        [MemberData(nameof(GetBuiltInConvertersForNullableTypes))]
        [Trait("category", "jsonoutput")]
        public static void SerializeNullValue_BuiltInConverter<T>(YamlConverter<T> converter)
        {
            _ = converter; // Not needed here.

            T @null = default;
            Assert.Null(@null);

            string yaml = YamlSerializer.Serialize(@null);
            Assert.Equal("null", yaml);

            yaml = YamlSerializer.Serialize(new T[] { @null });
            Assert.Equal("[null]", yaml);

            yaml = YamlSerializer.Serialize(new List<T> { @null });
            Assert.Equal("[null]", yaml);

            yaml = YamlSerializer.Serialize(new GenericRecord<T>(@null));
            Assert.Equal("""{"Value":null}""", yaml);

            yaml = YamlSerializer.Serialize(new Dictionary<string, T> { ["Key"] = @null });
            Assert.Equal("""{"Key":null}""", yaml);
        }

        public static IEnumerable<object?[]> GetBuiltInConvertersForNullableTypes()
        {
            return typeof(YamlMetadataServices)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(prop =>
                    prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(YamlConverter<>) &&
                    !prop.PropertyType.GetGenericArguments()[0].IsValueType)
                .Select(prop => new object?[] { prop.GetValue(null) });
        }

        public record GenericRecord<T>(T Value);
    }
}
