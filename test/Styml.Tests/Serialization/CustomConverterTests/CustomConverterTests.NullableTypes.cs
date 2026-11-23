// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        private class YamlTestStructConverter : YamlConverter<TestStruct>
        {
            public override TestStruct Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new TestStruct
                {
                    InnerValue = reader.GetInt32()
                };
            }

            public override void Write(Utf8YamlWriter writer, TestStruct value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(value.InnerValue);
            }
        }

        private class YamlTestStructThrowingConverter : YamlConverter<TestStruct>
        {
            public override TestStruct Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8YamlWriter writer, TestStruct value, YamlSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private struct TestStruct
        {
            public int InnerValue { get; set; }
        }

        private class TestStructClass
        {
            [YamlConverter(typeof(YamlTestStructConverter))]
            public TestStruct? MyStruct { get; set; }
        }

        private class TestStructInvalidClass
        {
            // Note: YamlTestStructConverter does not convert int, this is for negative testing.
            [YamlConverter(typeof(YamlTestStructConverter))]
            public int? MyInt { get; set; }
        }

        [Fact]
        public static void NullableCustomValueTypeUsingOptions()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlTestStructConverter());

            {
                TestStruct myStruct = YamlSerializer.Deserialize<TestStruct>("1", options);
                Assert.Equal(1, myStruct.InnerValue);
            }

            {
                TestStruct? myStruct = YamlSerializer.Deserialize<TestStruct?>("null", options);
                Assert.False(myStruct.HasValue);
            }

            {
                TestStruct? myStruct = YamlSerializer.Deserialize<TestStruct?>("1", options);
                Assert.Equal(1, myStruct.Value.InnerValue);
            }
        }

        [Fact]
        public static void NullableCustomValueTypeUsingAttributes()
        {
            {
                TestStructClass myStructClass = YamlSerializer.Deserialize<TestStructClass>("""{"MyStruct":null}""");
                Assert.False(myStructClass.MyStruct.HasValue);
            }

            {
                TestStructClass myStructClass = YamlSerializer.Deserialize<TestStructClass>("""{"MyStruct":1}""");
                Assert.True(myStructClass.MyStruct.HasValue);
                Assert.Equal(1, myStructClass.MyStruct.Value.InnerValue);
            }
        }

        [Fact]
        public static void NullableCustomValueTypeChoosesAttributeOverOptions()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlTestStructThrowingConverter());

            // Chooses YamlTestStructThrowingConverter on options, which will throw.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<TestStruct?>("1", options));

            // Chooses YamlTestStructConverter on attribute, which will not throw.
            TestStructClass myStructClass = YamlSerializer.Deserialize<TestStructClass>("""{"MyStruct":null}""", options);
            Assert.False(myStructClass.MyStruct.HasValue);
        }

        [Fact]
        public static void NullableCustomValueTypeNegativeTest()
        {
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestStructInvalidClass>("""{"MyInt":null}"""));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestStructInvalidClass>("""{"MyInt":1}"""));
        }

        [Fact]
        public static void NullableStandardValueTypeTest()
        {
            {
                int? myInt = YamlSerializer.Deserialize<int?>("null");
                Assert.False(myInt.HasValue);
            }

            {
                int? myInt = YamlSerializer.Deserialize<int?>("1");
                Assert.Equal(1, myInt.Value);
            }
        }

        /// <summary>
        /// Used to verify a converter for Nullable is called for null values. Converters are passed
        /// null when {T} is a value type including when {T}=Nullable{int?}.
        /// </summary>
        private class NullIntTo42Converter : YamlConverter<int?>
        {
            public override int? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    // Use literal value to differ from the default converter's behavior.
                    return 42;
                }

                Debug.Assert(false);
                throw new Exception("not expected");
            }

            public override void Write(Utf8YamlWriter writer, int? value, YamlSerializerOptions options)
            {
                if (value == null)
                {
                    // Use literal value to differ from the default converter's behavior.
                    writer.WriteNumberValue(42);
                }
                else
                {
                    Debug.Assert(false);
                    throw new Exception("not expected");
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NullableConverterIsNotPassedNull()
        {
            // For compat, deserialize does not call converter for null token unless the type doesn't support
            // null or HandleNull is overridden and returns 'true'.
            // For compat, serialize does not call converter for null unless null is a valid value and HandleNull is true.

            var options = new YamlSerializerOptions();
            options.Converters.Add(new NullIntTo42Converter());

            {
                int? myInt = YamlSerializer.Deserialize<int?>("null", options);
                Assert.Null(myInt);
            }

            {
                string yaml = YamlSerializer.Serialize<int?>(null, options);
                Assert.Equal("null", yaml);
            }

            {
                int?[] ints = YamlSerializer.Deserialize<int?[]>("[null, null]", options);
                Assert.Equal(2, ints.Length);
                Assert.Null(ints[0]);
                Assert.Null(ints[1]);
            }

            {
                string yaml = YamlSerializer.Serialize<int?[]>(new int?[] { null, null }, options);
                Assert.Equal("[null,null]", yaml);
            }
        }

        private class PocoSingleInt
        {
            public int MyInt;
        }

        /// <summary>
        /// Used to verify a converter for a reference type is not called for null values.
        /// </summary>
        private class PocoFailOnNullConverter : YamlConverter<PocoSingleInt>
        {
            public override PocoSingleInt Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    Debug.Assert(false);
                    throw new Exception();
                }

                reader.Skip();

                return new PocoSingleInt()
                {
                    // Use literal value to differ from the default converter's behavior.
                    MyInt = 42
                };
            }

            public override void Write(Utf8YamlWriter writer, PocoSingleInt value, YamlSerializerOptions options)
            {
                if (value == null)
                {
                    Debug.Assert(false);
                    throw new Exception();
                }

                writer.WriteStartObject();

                // Use literal value to differ from the default converter's behavior.
                writer.WriteNumber("MyInt", 42);

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReferenceTypeConverterDoesntGetPassedNull()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new PocoFailOnNullConverter());

            {
                PocoSingleInt poco = YamlSerializer.Deserialize<PocoSingleInt>("null", options);
                Assert.Null(poco);

                poco = YamlSerializer.Deserialize<PocoSingleInt>("{}", options);
                Assert.Equal(42, poco.MyInt);
            }

            {
                PocoSingleInt[] pocos = YamlSerializer.Deserialize<PocoSingleInt[]>("[null, null]", options);
                Assert.Equal(2, pocos.Length);
                Assert.Null(pocos[0]);
                Assert.Null(pocos[1]);

                pocos = YamlSerializer.Deserialize<PocoSingleInt[]>("[{}, {}]", options);
                Assert.Equal(2, pocos.Length);
                Assert.Equal(42, pocos[0].MyInt);
                Assert.Equal(42, pocos[1].MyInt);
            }

            {
                string yaml = YamlSerializer.Serialize<PocoSingleInt>(null, options);
                Assert.Equal(@"null", yaml);

                PocoSingleInt poco = new PocoSingleInt();
                yaml = YamlSerializer.Serialize<PocoSingleInt>(poco, options);
                Assert.Equal("""{"MyInt":42}""", yaml);
            }

            {
                string yaml = YamlSerializer.Serialize<PocoSingleInt[]>(new PocoSingleInt[] { null, null }, options);
                Assert.Equal("[null,null]", yaml);

                PocoSingleInt poco = new PocoSingleInt();
                yaml = YamlSerializer.Serialize<PocoSingleInt[]>(new PocoSingleInt[] { poco, poco }, options);
                Assert.Equal("""[{"MyInt":42},{"MyInt":42}]""", yaml);
            }
        }

        [Fact]
        public static void StructConverter_SaysCanConvertNullableStruct_ConverterOnProperty()
        {
            string converterTypeAsStr = typeof(YamlTestStructValueChangingConverter).ToString();
            string structTypeAsStr = typeof(TestStruct).ToString();
            string nullableStructTypeAsStr = typeof(TestStruct?).ToString();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => YamlSerializer.Serialize(new ClassWithNullableStruct_ConverterOnProperty { MyStruct = new TestStruct() }));
            string exAsStr = ex.ToString();
            Assert.Contains(converterTypeAsStr, exAsStr);
            Assert.Contains(structTypeAsStr, exAsStr);
            Assert.Contains(nullableStructTypeAsStr, exAsStr);

            ex = Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Deserialize<ClassWithNullableStruct_ConverterOnProperty>(""));
            exAsStr = ex.ToString();
            Assert.Contains(converterTypeAsStr, exAsStr);
            Assert.Contains(structTypeAsStr, exAsStr);
            Assert.Contains(nullableStructTypeAsStr, exAsStr);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void StructConverter_SaysCanConvertNullableStruct_ConverterOnType()
        {
            // Converter cannot be applied directly to nullable type, so the serializer wraps the converter it with NullableConverter<T> as expected.

            string serialized = YamlSerializer.Serialize(new ClassWithNullableStruct_ConverterOnType { MyStruct = new TestStructWithConverter { InnerValue = 5 } });
            Assert.Equal("""{"MyStruct":{"InnerValue":10}}""", serialized);

            ClassWithNullableStruct_ConverterOnType obj = YamlSerializer.Deserialize<ClassWithNullableStruct_ConverterOnType>(serialized);
            Assert.Equal(15, obj.MyStruct?.InnerValue);
        }

        [Fact]
        public static void StructConverter_SaysCanConvertNullableStruct_ConverterOnOptions()
        {
            var options = new YamlSerializerOptions { Converters = { new YamlTestStructValueChangingConverter() } };

            Assert.Throws<InvalidOperationException>(
                () => YamlSerializer.Serialize(new ClassWithNullableStruct { MyStruct = new TestStruct() }, options));

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Deserialize<ClassWithNullableStruct>("", options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void StructConverter_SaysCanConvertNullableStruct_StructAsRootType_ConverterOnType()
        {
            // Converter cannot be applied directly to nullable type, so the serializer wraps the converter it with NullableConverter<T> as expected.

            TestStructWithConverter? obj = new TestStructWithConverter { InnerValue = 5 };
            string serialized = YamlSerializer.Serialize(obj);
            Assert.Equal("""{"InnerValue":10}""", serialized);

            obj = YamlSerializer.Deserialize<TestStructWithConverter?>(serialized);
            Assert.Equal(15, obj?.InnerValue);
        }

        [Fact]
        public static void StructConverter_SaysCanConvertNullableStruct_StructAsRootType_ConverterOnOptions()
        {
            var options = new YamlSerializerOptions { Converters = { new YamlTestStructValueChangingConverter() } };

            TestStruct? obj = new TestStruct { InnerValue = 5 };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, options));

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Deserialize<TestStruct?>("", options));
        }

        private class ClassWithNullableStruct_ConverterOnProperty
        {
            [YamlConverter(typeof(YamlTestStructValueChangingConverter))]
            public TestStruct? MyStruct { get; set; }
        }

        private class ClassWithNullableStruct_ConverterOnType
        {
            public TestStructWithConverter? MyStruct { get; set; }
        }

        private class ClassWithNullableStruct
        {
            public TestStruct? MyStruct { get; set; }
        }

        [YamlConverter(typeof(YamlTestStructWithConverterValueChangingConverter))]
        public struct TestStructWithConverter
        {
            public int InnerValue { get; set; }
        }

        private class YamlTestStructValueChangingConverter : YamlConverter<TestStruct>
        {
            public override bool CanConvert(Type typeToConvert) =>
                typeToConvert == typeof(TestStruct) || typeToConvert == typeof(TestStruct?);

            public override TestStruct Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, TestStruct value, YamlSerializerOptions options) =>
                throw new NotImplementedException();
        }

        private class YamlTestStructWithConverterValueChangingConverter : YamlConverter<TestStructWithConverter>
        {
            public override bool CanConvert(Type typeToConvert) =>
                typeToConvert == typeof(TestStructWithConverter) || typeToConvert == typeof(TestStructWithConverter?);

            public override TestStructWithConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                reader.Read();

                if (reader.TokenType != YamlTokenType.PropertyName || reader.GetString() != "InnerValue")
                {
                    throw new YamlException();
                }

                reader.Read();

                var obj = new TestStructWithConverter
                {
                    InnerValue = reader.GetInt32() + 5
                };

                reader.Read();

                if (reader.TokenType != YamlTokenType.EndObject)
                {
                    throw new YamlException();
                }

                return obj;
            }

            public override void Write(Utf8YamlWriter writer, TestStructWithConverter value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("InnerValue", value.InnerValue + 5);
                writer.WriteEndObject();
            }
        }

        [Fact]
        public static void NonNullableConverter_ReturnedByYamlConverterFactory_CanBeUsedAsFallback_ForNullableProperty()
        {
            string yaml = """
                {
                "Property": {
                "Item1":1,
                "Item2":2
                }}
                """;
            // Verify that below converters will be called -
            // serializer doesn't support ValueTuple unless field support is active.
            ClassWithValueTuple obj0 = YamlSerializer.Deserialize<ClassWithValueTuple>(yaml);
            Assert.Equal(0, obj0.Property.Item1);
            Assert.Equal(0, obj0.Property.Item2);

            obj0 = YamlSerializer.Deserialize<ClassWithValueTuple>(yaml, new YamlSerializerOptions { IncludeFields = true });
            Assert.Equal(1, obj0.Property.Item1);
            Assert.Equal(2, obj0.Property.Item2);

            // Baseline: converter returned from factory can be used for non-nullable property.
            ClassWithFactoryOn_NonNullableProperty obj1 = YamlSerializer.Deserialize<ClassWithFactoryOn_NonNullableProperty>(yaml);
            Assert.Equal(1, obj1.Property.Item1);
            Assert.Equal(2, obj1.Property.Item2);

            YamlTestHelper.AssertYamlEqual(yaml, YamlSerializer.Serialize(obj1));

            // Test: converter returned from factory can be used for nullable property.
            ClassWithFactoryOn_NullableProperty obj2 = YamlSerializer.Deserialize<ClassWithFactoryOn_NullableProperty>(yaml);
            Assert.Equal(1, obj2.Property?.Item1);
            Assert.Equal(2, obj2.Property?.Item2);

            YamlTestHelper.AssertYamlEqual(yaml, YamlSerializer.Serialize(obj2));
        }

        private class ClassWithValueTuple
        {
            public (int, int) Property { get; set; }
        }

        private class ClassWithFactoryOn_NonNullableProperty
        {
            [YamlConverter(typeof(MyValueTupleConverterFactory))]
            public (int, int) Property { get; set; }
        }

        private class ClassWithFactoryOn_NullableProperty
        {
            [YamlConverter(typeof(MyValueTupleConverterFactory))]
            public (int, int)? Property { get; set; }
        }

        public class MyValueTupleConverterFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) => IsValueTupleType(typeToConvert);

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                Debug.Assert(CanConvert(typeToConvert));

                Type[] genericArgs = typeToConvert.GetGenericArguments();
                Type item1Type = genericArgs[0];
                Type item2Type = genericArgs[1];

                YamlConverter converter = (YamlConverter)Activator.CreateInstance(
                    typeof(ValueTupleConverter<,>).MakeGenericType(new Type[] { item1Type, item2Type }),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: new object[] { },
                    culture: null);

                return converter;
            }

            protected static bool IsValueTupleType(Type type) =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTuple<,>);

            protected class ValueTupleConverter<TItem1, TItem2> : YamlConverter<ValueTuple<TItem1, TItem2>>
            {
                public override (TItem1, TItem2) Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    Debug.Assert(reader.TokenType == YamlTokenType.StartObject);

                    reader.Read();
                    YamlTokenType tokenType = reader.TokenType;

                    (TItem1, TItem2) value = default;

                    while (true)
                    {
                        if (tokenType == YamlTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            if (propertyName == "Item1")
                            {
                                value.Item1 = YamlSerializer.Deserialize<TItem1>(ref reader);
                            }
                            else if (propertyName == "Item2")
                            {
                                value.Item2 = YamlSerializer.Deserialize<TItem2>(ref reader);
                            }
                            else
                            {
                                throw new YamlException();
                            }

                            reader.Read();
                            tokenType = reader.TokenType;
                            continue;
                        }
                        else if (tokenType == YamlTokenType.EndObject)
                        {
                            break;
                        }

                        throw new YamlException();
                    }

                    return value;
                }

                public override void Write(Utf8YamlWriter writer, (TItem1, TItem2) value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("Item1");
                    YamlSerializer.Serialize(writer, value.Item1);

                    writer.WritePropertyName("Item2");
                    YamlSerializer.Serialize(writer, value.Item2);

                    writer.WriteEndObject();
                }
            }
        }
    }
}
