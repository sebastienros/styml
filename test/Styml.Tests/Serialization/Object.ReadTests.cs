// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ObjectTests
    {
        [Fact]
        public static void ReadSimpleStruct()
        {
            SimpleTestStruct obj = YamlSerializer.Deserialize<SimpleTestStruct>(SimpleTestStruct.s_yaml);
            obj.Verify();
        }

        [Fact]
        public static void ReadSimpleClass()
        {
            SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(SimpleTestClass.s_yaml);
            obj.Verify();
        }

        [Theory]
        [InlineData("", " ")]
        [InlineData("", "\t ")]
        [InlineData(" ", "")]
        [InlineData("\t ", "")]
        [InlineData(" \t", " \n")]
        public static void ReadSimpleClassIgnoresLeadingOrTrailingTrivia(string leadingTrivia, string trailingTrivia)
        {
            var options = new YamlSerializerOptions();
            options.ReadCommentHandling = YamlCommentHandling.Skip;

            SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(leadingTrivia + SimpleTestClass.s_yaml + trailingTrivia, options);
            obj.Verify();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReadSimpleClassWithObject()
        {
            SimpleTestClassWithSimpleObject obj = YamlSerializer.Deserialize<SimpleTestClassWithSimpleObject>(SimpleTestClassWithSimpleObject.s_yaml);
            obj.Verify();
            string reserialized = YamlSerializer.Serialize(obj);

            // Properties in the exported json will be in the order that they were reflected, doing a quick check to see that
            // we end up with the same length (i.e. same amount of data) to start.
            Assert.Equal(SimpleTestClassWithSimpleObject.s_yaml.StripWhitespace().Length, reserialized.Length);

            // Shoving it back through the parser should validate round tripping.
            obj = YamlSerializer.Deserialize<SimpleTestClassWithSimpleObject>(reserialized);
            obj.Verify();
        }

        [Theory]
        [InlineData("", "//Single Line Comment\r\n")]
        [InlineData("", "/* Multi\nLine Comment */")]
        [InlineData("", "\t\t\t\n// Both\n/* Comments */")]
        [InlineData("// Leading Comment\n", "")]
        [InlineData("/* Multi\nLine\nComment */ ", "")]
        [InlineData("/* Multi\nLine\nComment */ ", "\t// trailing comment\n ")]
        public static void ReadClassWithCommentsThrowsIfDisallowed(string leadingTrivia, string trailingTrivia)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithComplexObjects>(leadingTrivia + ClassWithComplexObjects.s_yaml + trailingTrivia));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReadSimpleClassWithObjectArray()
        {
            SimpleTestClassWithObjectArrays obj = YamlSerializer.Deserialize<SimpleTestClassWithObjectArrays>(SimpleTestClassWithObjectArrays.s_yaml);
            obj.Verify();
            string reserialized = YamlSerializer.Serialize(obj);

            // Properties in the exported json will be in the order that they were reflected, doing a quick check to see that
            // we end up with the same length (i.e. same amount of data) to start.
            string yaml = SimpleTestClassWithObjectArrays.s_yaml.StripWhitespace();
            Assert.Equal(yaml.Length, reserialized.Length);

            // Shoving it back through the parser should validate round tripping.
            obj = YamlSerializer.Deserialize<SimpleTestClassWithObjectArrays>(reserialized);
            obj.Verify();
        }

        [Fact]
        public static void ReadArrayInObjectArray()
        {
            object[] array = YamlSerializer.Deserialize<object[]>("[[]]");
            Assert.Equal(1, array.Length);
            Assert.IsType<YamlElement>(array[0]);
            Assert.Equal(YamlValueKind.Array, ((YamlElement)array[0]).ValueKind);
        }

        [Fact]
        public static void ReadObjectInObjectArray()
        {
            object[] array = YamlSerializer.Deserialize<object[]>("[{}]");
            Assert.Equal(1, array.Length);
            Assert.IsType<YamlElement>(array[0]);
            Assert.Equal(YamlValueKind.Object, ((YamlElement)array[0]).ValueKind);

            // Scenario described in https://github.com/dotnet/runtime/issues/29021
            array = YamlSerializer.Deserialize<object[]>("[1, false]");
            Assert.Equal(2, array.Length);
            Assert.IsType<YamlElement>(array[0]);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)array[0]).ValueKind);
            Assert.Equal(1, ((YamlElement)array[0]).GetInt32());
            Assert.IsType<YamlElement>(array[1]);
            Assert.Equal(YamlValueKind.False, ((YamlElement)array[1]).ValueKind);

            array = YamlSerializer.Deserialize<object[]>("""[1, false, { "name" : "Person" }]""");
            Assert.Equal(3, array.Length);
            Assert.IsType<YamlElement>(array[0]);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)array[0]).ValueKind);
            Assert.Equal(1, ((YamlElement)array[0]).GetInt32());
            Assert.IsType<YamlElement>(array[1]);
            Assert.Equal(YamlValueKind.False, ((YamlElement)array[1]).ValueKind);
            Assert.IsType<YamlElement>(array[2]);
            Assert.Equal(YamlValueKind.Object, ((YamlElement)array[2]).ValueKind);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReadClassWithComplexObjects()
        {
            ClassWithComplexObjects obj = YamlSerializer.Deserialize<ClassWithComplexObjects>(ClassWithComplexObjects.s_yaml);
            obj.Verify();
            string reserialized = YamlSerializer.Serialize(obj);

            // Properties in the exported json will be in the order that they were reflected, doing a quick check to see that
            // we end up with the same length (i.e. same amount of data) to start.
            Assert.Equal(ClassWithComplexObjects.s_yaml.StripWhitespace().Length, reserialized.Length);

            // Shoving it back through the parser should validate round tripping.
            obj = YamlSerializer.Deserialize<ClassWithComplexObjects>(reserialized);
            obj.Verify();
        }

        [Theory]
        [InlineData("", " ")]
        [InlineData("", "\t ")]
        [InlineData(" ", "")]
        [InlineData("\t ", "")]
        [InlineData(" \t", " \n")]
        public static void ReadComplexClassIgnoresLeadingOrTrailingTrivia(string leadingTrivia, string trailingTrivia)
        {
            var options = new YamlSerializerOptions();
            options.ReadCommentHandling = YamlCommentHandling.Skip;

            ClassWithComplexObjects obj = YamlSerializer.Deserialize<ClassWithComplexObjects>(leadingTrivia + ClassWithComplexObjects.s_yaml + trailingTrivia, options);
            obj.Verify();
        }

        [Fact]
        public static void ReadEmpty()
        {
            SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>("{}");
            Assert.NotNull(obj);
        }

        [Fact]
        public static void EmptyClassWithRandomData()
        {
            YamlSerializer.Deserialize<EmptyClass>(SimpleTestClass.s_yaml);
            YamlSerializer.Deserialize<EmptyClass>(SimpleTestClassWithNulls.s_yaml);
        }

        [Fact]
        public static void ReadObjectFail_ReferenceTypeMissingPublicParameterlessConstructor()
        {
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<ClassWithInternalParameterlessCtor>("""{"Name":"Name!"}"""));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<ClassWithPrivateParameterlessCtor>("""{"Name":"Name!"}"""));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<CollectionWithoutPublicParameterlessCtor>("""["foo", 1, false]"""));

            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<GenericClassWithProtectedInternalCtor<string>>("{\"Result\":null}"));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<ConcreteDerivedClassWithNoPublicDefaultCtor>("{\"ErrorString\":\"oops\"}"));
        }

        private class PublicParameterizedConstructorTestClass
        {
            public PublicParameterizedConstructorTestClass(string name) { }

            private PublicParameterizedConstructorTestClass(int internalId)
            {
                Name = internalId.ToString();
            }

            public string Name { get; }

            public static PublicParameterizedConstructorTestClass Instance { get; } = new PublicParameterizedConstructorTestClass(42);
        }

        private class ClassWithInternalParameterlessCtor
        {
            internal ClassWithInternalParameterlessCtor()
            {
                Debug.Fail("The YamlSerializer should not be callin non-public ctors, by default.");
            }

            private ClassWithInternalParameterlessCtor(string name)
            {
                Name = name;
            }

            public string Name { get; set; }

            public static ClassWithInternalParameterlessCtor Instance { get; } = new ClassWithInternalParameterlessCtor("InstancePropertyInternal");
        }

        private class ClassWithPrivateParameterlessCtor
        {
            private ClassWithPrivateParameterlessCtor()
            {
                Debug.Fail("The YamlSerializer should not be callin non-public ctors, by default.");
            }

            private ClassWithPrivateParameterlessCtor(string name)
            {
                Name = name;
            }

            public string Name { get; set; }

            public static ClassWithPrivateParameterlessCtor Instance { get; } = new ClassWithPrivateParameterlessCtor("InstancePropertyPrivate");
        }

        private class CollectionWithoutPublicParameterlessCtor : IList
        {
            private readonly List<object> _list;

            internal CollectionWithoutPublicParameterlessCtor()
            {
                Debug.Fail("The YamlSerializer should not be calling non-public ctors, by default.");
            }

            public CollectionWithoutPublicParameterlessCtor(List<object> list)
            {
                _list = list;
            }

            public object this[int index] { get => _list[index]; set => _list[index] = value; }

            public bool IsFixedSize => throw new NotImplementedException();

            public bool IsReadOnly => false;

            public int Count => _list.Count;

            public bool IsSynchronized => throw new NotImplementedException();

            public object SyncRoot => throw new NotImplementedException();

            public int Add(object value)
            {
                int pos = _list.Count;
                _list.Add(value);
                return pos;
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(object value)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public IEnumerator GetEnumerator() => _list.GetEnumerator();

            public int IndexOf(object value)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, object value)
            {
                throw new NotImplementedException();
            }

            public void Remove(object value)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }
        }

        private class GenericClassWithProtectedInternalCtor<T>
        {
            public T Result { get; set; }

            protected internal GenericClassWithProtectedInternalCtor()
            {
                Result = default;
            }
        }

        private sealed class ConcreteDerivedClassWithNoPublicDefaultCtor : GenericClassWithProtectedInternalCtor<string>
        {
            public string ErrorString { get; set; }

            private ConcreteDerivedClassWithNoPublicDefaultCtor(string error)
            {
                ErrorString = error;
            }

            public static ConcreteDerivedClassWithNoPublicDefaultCtor Ok() => new ConcreteDerivedClassWithNoPublicDefaultCtor("ok");
            public static GenericClassWithProtectedInternalCtor<T> Ok<T>() => new GenericClassWithProtectedInternalCtor<T>();
            public static ConcreteDerivedClassWithNoPublicDefaultCtor Error(string error) => new ConcreteDerivedClassWithNoPublicDefaultCtor(error);
        }

        [Fact]
        public static void ReadClassWithStringToPrimitiveDictionary()
        {
            TestClassWithStringToPrimitiveDictionary obj = YamlSerializer.Deserialize<TestClassWithStringToPrimitiveDictionary>(TestClassWithStringToPrimitiveDictionary.s_data);
            obj.Verify();
        }

        public class TestClassWithBadData
        {
            public TestChildClassWithBadData[] Children { get; set; }
        }

        public class TestChildClassWithBadData
        {
            public int MyProperty { get; set; }
        }

        [Fact]
        public static void ReadConversionFails()
        {
            byte[] data = Encoding.UTF8.GetBytes(
                @"{" +
                    @"""Children"":[" +
                        @"{""MyProperty"":""StringButShouldBeInt""}" +
                    @"]" +
                @"}");

            bool exceptionThrown = false;

            try
            {
                YamlSerializer.Deserialize<TestClassWithBadData>(data);
            }
            catch (YamlException exception)
            {
                exceptionThrown = true;

                // Exception should contain path.
                Assert.Contains("Path: $.Children[0].MyProperty", exception.ToString());
            }

            Assert.True(exceptionThrown);
        }

        [Fact]
        public static void ReadObject_PublicIndexer()
        {
            Indexer indexer = YamlSerializer.Deserialize<Indexer>("""{"NonIndexerProp":"Value"}""");
            Assert.Equal("Value", indexer.NonIndexerProp);
            Assert.Equal(-1, indexer[0]);
        }

        [Fact]
        public static void ReadSimpleStructWithSimpleClass()
        {
            SimpleStructWithSimpleClass testObject = new SimpleStructWithSimpleClass();
            testObject.Initialize();

            string yaml = YamlSerializer.Serialize(testObject, testObject.GetType());
            SimpleStructWithSimpleClass obj = YamlSerializer.Deserialize<SimpleStructWithSimpleClass>(yaml);
            obj.Verify();
        }

        [Fact]
        public static void ReadSimpleTestStructWithSimpleTestClass()
        {
            SimpleTestStruct testObject = new SimpleTestStruct();
            testObject.Initialize();
            testObject.MySimpleTestClass = new SimpleTestClass { MyString = "Hello", MyDouble = 3.14 };

            string yaml = YamlSerializer.Serialize(testObject);
            SimpleTestStruct parsedObject = YamlSerializer.Deserialize<SimpleTestStruct>(yaml);
            parsedObject.Verify();
            Assert.Equal(3.14, parsedObject.MySimpleTestClass.MyDouble);
            Assert.Equal("Hello", parsedObject.MySimpleTestClass.MyString);
        }

        [Fact]
        public static void ReadSimpleTestClassWithSimpleTestStruct()
        {
            SimpleTestClass testObject = new SimpleTestClass();
            testObject.Initialize();
            testObject.MySimpleTestStruct = new SimpleTestStruct { MyInt64 = 64, MyString = "Hello", MyInt32Array = new int[] { 32 } };

            string yaml = YamlSerializer.Serialize(testObject);
            SimpleTestClass parsedObject = YamlSerializer.Deserialize<SimpleTestClass>(yaml);
            parsedObject.Verify();
            Assert.Equal(64, parsedObject.MySimpleTestStruct.MyInt64);
            Assert.Equal("Hello", parsedObject.MySimpleTestStruct.MyString);
            Assert.Equal(32, parsedObject.MySimpleTestStruct.MyInt32Array[0]);
        }

        [Fact]
        public static void OuterClassHavingPropertiesDefinedAfterClassWithDictionaryTest()
        {
            OuterClassHavingPropertiesDefinedAfterClassWithDictionary testObject = new OuterClassHavingPropertiesDefinedAfterClassWithDictionary
            {
                MyInt = 10,
                MyIntArray = new int[] { 3 },
                MyDouble = 3.14,
                MyList = new List<string> { "Hello" },
                MyString = "World",
                MyInnerTestClass = new SimpleClassWithDictionary { MyInt = 2 }
            };
            string yaml = YamlSerializer.Serialize(testObject);

            OuterClassHavingPropertiesDefinedAfterClassWithDictionary parsedObject = YamlSerializer.Deserialize<OuterClassHavingPropertiesDefinedAfterClassWithDictionary>(yaml);

            Assert.Equal(3.14, parsedObject.MyDouble);
            Assert.Equal(10, parsedObject.MyInt);
            Assert.Equal("World", parsedObject.MyString);
            Assert.Equal("Hello", parsedObject.MyList[0]);
            Assert.Equal(3, parsedObject.MyIntArray[0]);
            Assert.Equal(2, parsedObject.MyInnerTestClass.MyInt);
        }

        [Fact]
        public static void ReadStructWithSimpleClassArrayValueTest()
        {
            string yaml = "{\"MySimpleTestClass\":{\"MyInt32Array\":[1],\"MyStringToStringDict\":null,\"MyStringToStringIDict\":null},\"MyInt32Array\":[2]}";
            SimpleTestStruct parsedObject = YamlSerializer.Deserialize<SimpleTestStruct>(yaml);

            Assert.Equal(1, parsedObject.MySimpleTestClass.MyInt32Array[0]);
            Assert.Equal(2, parsedObject.MyInt32Array[0]);
        }

        public class ClassWithNoSetter
        {
            public SimpleTestClass SkippedChild { get; }
            public SimpleTestClass ParsedChild { get; set; }
            public SimpleTestClass AnotherSkippedChild { get; }
            public SimpleTestClass AnotherParsedChild { get; set; }
        }

        [Theory]
        [InlineData("""
                {
                                "SkippedChild": {},
                                "ParsedChild": {"MyInt16":18},
                                "UnmatchedProp": null,
                                "AnotherSkippedChild": {"DrainProp1":{}, "DrainProp2":{"SubProp":0}},
                                "AnotherSkippedChild": {},
                                "AnotherParsedChild": {"MyInt16":20}}
            """)]
        [InlineData("""
                {
                                "SkippedChild": null,
                                "ParsedChild": {"MyInt16":18},
                                "UnmatchedProp": null,
                                "AnotherSkippedChild": {"DrainProp1":{}, "DrainProp2":{"SubProp":0}},
                                "AnotherSkippedChild": null,
                                "AnotherParsedChild": {"MyInt16":20}}
            """)]
        public static void ClassWithNoSetterAndValidProperty(string yaml)
        {
            ClassWithNoSetter parsedObject = YamlSerializer.Deserialize<ClassWithNoSetter>(yaml);

            Assert.Null(parsedObject.SkippedChild);

            Assert.NotNull(parsedObject.ParsedChild);
            Assert.Equal(18, parsedObject.ParsedChild.MyInt16);

            Assert.Null(parsedObject.AnotherSkippedChild);

            Assert.NotNull(parsedObject.AnotherParsedChild);
            Assert.Equal(20, parsedObject.AnotherParsedChild.MyInt16);
        }

        public class ClassMixingSkippedTypes
        {
            public IDictionary<string, string> SkippedDictionary { get; }

            public SimpleTestClass ParsedClass { get; set; }

            public IList<int> SkippedList { get; }
            public IDictionary<string, string> AnotherSkippedDictionary { get; }
            public SimpleTestClass SkippedClass { get; }

            public Dictionary<string, int> ParsedDictionary { get; set; }

            public IList<int> AnotherSkippedList { get; }
            public IDictionary<string, string> AnotherSkippedDictionary2 { get; }
            public IDictionary<string, string> SkippedDictionaryNotInYaml { get; }
            public SimpleTestClass AnotherSkippedClass { get; }

            public int[] ParsedList { get; set; }

            public ClassMixingSkippedTypes ParsedSubMixedTypeParsedClass { get; set; }
        }

        [Fact]
        public static void ClassWithMixingSkippedTypes()
        {
            // Tests that the parser picks back up after skipping/draining ignored elements. Complex version.
            string yaml = """
                    {
                                    "SkippedDictionary": {},
                                    "ParsedClass": {"MyInt16":18},
                                    "SkippedList": [18,20],
                                    "UnmatchedList": [{},{}],
                                    "AnotherSkippedDictionary": {"Key":"Value"},
                                    "SkippedClass": {"MyInt16":99},
                                    "ParsedDictionary": {"Key1":18},
                                    "UnmatchedProp": null,
                                    "AnotherSkippedList": null,
                                    "AnotherSkippedDictionary2": {"Key":"Value"},
                                    "AnotherSkippedDictionary2": {"Key":"Dupe"},
                                    "AnotherSkippedClass": {},
                                    "ParsedList": [18,20],
                                    "ParsedSubMixedTypeParsedClass": {"ParsedDictionary": {"Key1":18}},
                                    "UnmatchedDictionary": {"DrainProp1":{}, "DrainProp2":{"SubProp":0}}                
                                }
                """;

            ClassMixingSkippedTypes parsedObject = YamlSerializer.Deserialize<ClassMixingSkippedTypes>(yaml);

            Assert.Null(parsedObject.SkippedDictionary);

            Assert.NotNull(parsedObject.ParsedClass);
            Assert.Equal(18, parsedObject.ParsedClass.MyInt16);

            Assert.Null(parsedObject.SkippedList);
            Assert.Null(parsedObject.AnotherSkippedDictionary);
            Assert.Null(parsedObject.SkippedClass);

            Assert.NotNull(parsedObject.ParsedDictionary);
            Assert.Equal(1, parsedObject.ParsedDictionary.Count);
            Assert.Equal(18, parsedObject.ParsedDictionary["Key1"]);

            Assert.Null(parsedObject.AnotherSkippedList);
            Assert.Null(parsedObject.AnotherSkippedDictionary2);
            Assert.Null(parsedObject.SkippedDictionaryNotInYaml);
            Assert.Null(parsedObject.AnotherSkippedClass);

            Assert.NotNull(parsedObject.ParsedList);
            Assert.Equal(2, parsedObject.ParsedList.Length);
            Assert.Equal(18, parsedObject.ParsedList[0]);
            Assert.Equal(20, parsedObject.ParsedList[1]);

            Assert.NotNull(parsedObject.ParsedSubMixedTypeParsedClass);
            Assert.NotNull(parsedObject.ParsedSubMixedTypeParsedClass.ParsedDictionary);
            Assert.Equal(1, parsedObject.ParsedSubMixedTypeParsedClass.ParsedDictionary.Count);
            Assert.Equal(18, parsedObject.ParsedSubMixedTypeParsedClass.ParsedDictionary["Key1"]);
        }

        private class POCO { }

        [Theory]
        [InlineData("{}{}")]
        [InlineData("{}1")]
        [InlineData("{}/**/")]
        [InlineData("{} /**/")]
        public static void TooMuchYamlFails(string yaml)
        {
            byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<POCO>(yaml));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<POCO>(yamlBytes));
            Assert.Throws<YamlException>(() => YamlSerializer.DeserializeAsync<POCO>(new MemoryStream(yamlBytes)).Result);

            // Using a reader directly doesn't throw since it stops once POCO is read.
            Utf8YamlReader reader = new Utf8YamlReader(yamlBytes);
            YamlSerializer.Deserialize<POCO>(ref reader);

            Assert.True(reader.BytesConsumed > 0);
            Assert.NotEqual(yamlBytes.Length, reader.BytesConsumed);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("""
            {"
            """)]
        [InlineData("""
            {"a"
            """)]
        [InlineData("""{"a":""")]
        [InlineData("""{"a":1""")]
        [InlineData("""{"a":1,""")]
        public static void TooLittleYamlFails(string yaml)
        {
            byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<POCO>(yaml));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<POCO>(yamlBytes));
            Assert.Throws<YamlException>(() => YamlSerializer.DeserializeAsync<POCO>(new MemoryStream(yamlBytes)).Result);

            // Using a reader directly throws since it can't read full object.
            Utf8YamlReader reader = new Utf8YamlReader(yamlBytes);
            try
            {
                YamlSerializer.Deserialize<POCO>(ref reader);
                Assert.Fail("Expected exception.");
            }
            catch (YamlException) { }

            Assert.Equal(0, reader.BytesConsumed);
        }

        // Regression test for https://github.com/dotnet/runtime/issues/61995
        [Theory]
        [InlineData(YamlUnknownTypeHandling.YamlElement, typeof(YamlElement))]
        [InlineData(YamlUnknownTypeHandling.YamlNode, typeof(YamlNode))]
        public static void ReadObjectWithNumberHandling(YamlUnknownTypeHandling unknownTypeHandling, Type expectedType)
        {
            var options = new YamlSerializerOptions
            {
                NumberHandling = YamlNumberHandling.AllowReadingFromString,
                UnknownTypeHandling = unknownTypeHandling
            };

            object result = YamlSerializer.Deserialize<object>("""{ "key" : "42" }""", options);
            Assert.IsAssignableFrom(expectedType, result);
        }

        [Theory]
        [InlineData("""{ "MyInt32" : 42, "MyInt32" : 42 }""")]
        [InlineData("""{ "MyInt32Array" : null, "MyInt32Array" : null }""")]
        public static void ReadSimpleObjectWithDuplicateProperties(string payload)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;

            Exception ex =  Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<SimpleTestClass>(payload, options));
            Assert.Contains("Duplicate", ex.Message);

            _ = YamlSerializer.Deserialize<SimpleTestClass>(payload, YamlTestSerializerOptions.LastWins); // Assert no throw
        }

        [Theory]
        [InlineData("""{ "MyData" : null, "MyData" : null }""")]
        [InlineData("""{ "MyData" : {}, "MyData" : {} }""")]
        public static void ReadNestedObjectWithDuplicateProperties(string payload)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;

            Exception ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TestClassWithNestedObjectInner>(payload, options));
            Assert.Contains("Duplicate", ex.Message);

            _ = YamlSerializer.Deserialize<TestClassWithNestedObjectInner>(payload, YamlTestSerializerOptions.LastWins); // Assert no throw
        }

        [Theory]
        [InlineData("""{ "MyInt32" : 42, "myInt32" : 42 }""")]
        [InlineData("""{ "MyInt32Array" : null, "myInt32Array" : null }""")]
        public static void ReadSimpleObjectWithDuplicatePropertiesCaseInsensitive(string payload)
        {
            var options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;

            Exception ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<SimpleTestClass>(payload, options));
            Assert.Contains("Duplicate", ex.Message);
        }
    }
}
