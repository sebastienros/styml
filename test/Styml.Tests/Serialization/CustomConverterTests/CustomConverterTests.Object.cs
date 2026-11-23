// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// A converter that uses Object as its type.
        /// </summary>
        private class ObjectToCustomerOrIntConverter : YamlConverter<object>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return (typeToConvert == typeof(Customer) ||
                    typeToConvert == typeof(int));
            }

            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (typeToConvert == typeof(Customer))
                {
                    reader.Skip();
                    Customer c = new Customer();
                    c.Name = "HelloWorld";
                    return c;
                }

                if (typeToConvert == typeof(int))
                {
                    return 42;
                }

                throw new NotSupportedException();
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                // Write the name of the type.
                writer.WriteStringValue(value.GetType().ToString());
            }
        }

        [Fact]
        public static void CustomObjectConverter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new ObjectToCustomerOrIntConverter());

            {
                var customer = new Customer();
                string yaml = YamlSerializer.Serialize<object>(customer, options);
                Assert.Contains(typeof(Customer).ToString(), yaml);

                yaml = YamlSerializer.Serialize(customer, options);
                Assert.Contains(typeof(Customer).ToString(), yaml);
            }

            {
                string yaml = YamlSerializer.Serialize(42, options);
                Assert.Contains(typeof(int).ToString(), yaml);
            }

            {
                object obj = YamlSerializer.Deserialize<Customer>("{}", options);
                Assert.IsType<Customer>(obj);
                Assert.Equal("HelloWorld", ((Customer)obj).Name);
            }

            {
                // The converter doesn't handle object.
                object obj = YamlSerializer.Deserialize<object>("{}", options);
                Assert.IsType<YamlElement>(obj);
            }

            {
                int obj = YamlSerializer.Deserialize<int>("0", options);
                Assert.Equal(42, obj);
            }
        }

        /// <summary>
        /// A converter that converts "true" and "false" tokens to a bool.
        /// </summary>
        private class ObjectToBoolConverter : YamlConverter<object>
        {
            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.True)
                {
                    return true;
                }

                if (reader.TokenType == YamlTokenType.False)
                {
                    return false;
                }

                // Use YamlElement as fallback.
                var converter = options.GetConverter(typeof(YamlElement)) as YamlConverter<YamlElement>;
                if (converter != null)
                {
                    return converter.Read(ref reader, typeToConvert, options);
                }

                // Shouldn't get here.
                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                throw new InvalidOperationException("Directly writing object not supported");
            }
        }

        [Fact]
        public static void CustomObjectBoolConverter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new ObjectToBoolConverter());

            {
                object obj = YamlSerializer.Deserialize<object>("true", options);
                Assert.IsType<bool>(obj);
                Assert.True((bool)obj);
            }

            {
                object obj = YamlSerializer.Deserialize<object>("false", options);
                Assert.IsType<bool>(obj);
                Assert.False((bool)obj);
            }

            {
                object obj = YamlSerializer.Deserialize<object>("{}", options);
                Assert.IsType<YamlElement>(obj);
            }
        }

        private class ObjectToCustomerConverter : YamlConverter<Customer>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return (typeToConvert == typeof(Customer) || typeToConvert == typeof(DerivedCustomer));
            }

            public override Customer Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Customer customer = null;
                if (typeToConvert == typeof(Customer))
                {
                    customer = new Customer();
                }

                if (typeToConvert == typeof(DerivedCustomer))
                {
                    customer = new DerivedCustomer();
                }

                if (customer != null)
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == YamlTokenType.EndObject)
                        {
                            return customer;
                        }

                        if (reader.TokenType == YamlTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName)
                            {
                                case "CreditLimit":
                                    decimal creditLimit = reader.GetDecimal();
                                    customer.CreditLimit = creditLimit - 1;
                                    break;
                                case "Address":
                                    string city = reader.GetString();
                                    customer.Address.City = string.IsNullOrEmpty(city) ? "NA" : city;
                                    break;
                                case "Name":
                                    string name = reader.GetString().ToUpper();
                                    customer.Name = name;
                                    break;
                            }
                        }
                    }
                    return customer;
                }

                throw new NotSupportedException();
            }

            public override void Write(Utf8YamlWriter writer, Customer value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("Name", value.Name);
                writer.WriteNumber("CreditLimit", value.CreditLimit);
                writer.WriteString("Address", value.Address.City);
                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithFieldHavingCustomConverterTest()
        {
            TestClassWithFieldsHavingCustomConverter testObject = new TestClassWithFieldsHavingCustomConverter
            {
                IntValue = 32,
                Name = "John Doe",
                Customer = new Customer
                {
                    Name = "Customer Doe",
                    CreditLimit = 1000
                },
                DerivedCustomer = new DerivedCustomer
                {
                    Name = "Derived Doe",
                    CreditLimit = 2000,
                    Address = new Address { City = "UB" }
                }
            };

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ObjectToCustomerConverter());

            string yaml = YamlSerializer.Serialize(testObject, options);

            Assert.Equal("{\"Name\":\"John Doe\"," +
                "\"Customer\":{\"Name\":\"Customer Doe\",\"CreditLimit\":1000,\"Address\":null}," +
                "\"DerivedCustomer\":{\"Name\":\"Derived Doe\",\"CreditLimit\":2000,\"Address\":\"UB\"}," +
                "\"NullDerivedCustomer\":null," +
                "\"IntValue\":32," +
                "\"Message\":null}", yaml);

            TestClassWithFieldsHavingCustomConverter testObj = YamlSerializer.Deserialize<TestClassWithFieldsHavingCustomConverter>(yaml, options);

            Assert.Equal(32, testObj.IntValue);
            Assert.Equal("John Doe", testObj.Name);
            Assert.Equal("CUSTOMER DOE", testObj.Customer.Name);
            Assert.Equal("NA", testObj.Customer.Address.City);
            Assert.Equal("DERIVED DOE", testObj.DerivedCustomer.Name);
            Assert.Equal(1999, testObj.DerivedCustomer.CreditLimit);
            Assert.Equal("UB", testObj.DerivedCustomer.Address.City);
            Assert.Null(testObj.NullDerivedCustomer);
        }

        private class TestClassWithFieldsHavingCustomConverter
        {
            public string Name { get; set; }
            public Customer Customer { get; set; }
            public DerivedCustomer DerivedCustomer { get; set; }
            public DerivedCustomer? NullDerivedCustomer { get; set; }
            public int IntValue { get; set; }
            public string? Message { get; set; }
        }

        /// <summary>
        /// A converter that converts System.Object similar to Newtonsoft's JSON.Net.
        /// Only primitives are the same; arrays and objects do not result in the same types.
        /// </summary>
        private class SystemObjectNewtonsoftCompatibleConverter : YamlConverter<object>
        {
            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.True)
                {
                    return true;
                }

                if (reader.TokenType == YamlTokenType.False)
                {
                    return false;
                }

                if (reader.TokenType == YamlTokenType.Number)
                {
                    if (reader.TryGetInt64(out long l))
                    {
                        return l;
                    }

                    return reader.GetDouble();
                }

                if (reader.TokenType == YamlTokenType.String)
                {
                    if (reader.TryGetDateTime(out DateTime datetime))
                    {
                        return datetime;
                    }

                    return reader.GetString();
                }

                // Use YamlElement as fallback.
                // Newtonsoft uses JArray or JObject.
                using (YamlDocument document = YamlDocument.ParseValue(ref reader))
                {
                    return document.RootElement.Clone();
                }
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                }
                else if (value.GetType() == typeof(object))
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
                else
                {
                    YamlSerializer.Serialize(writer, value, value.GetType(), options);
                }
            }
        }

        private class PrimitiveConverter : YamlConverter<object>
        {
            public int ReadCallCount { get; private set; }
            public int WriteCallCount { get; private set; }

            public override bool CanConvert(Type typeToConvert)
                => typeToConvert != typeof(ClassWithPrimitives)
                && typeToConvert != typeof(ClassWithNullablePrimitives);

            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                ReadCallCount++;

                if (reader.TokenType == YamlTokenType.True)
                {
                    return true;
                }

                if (reader.TokenType == YamlTokenType.False)
                {
                    return false;
                }

                if (reader.TokenType == YamlTokenType.Number)
                {
                    if (reader.TryGetInt32(out int i))
                    {
                        return i;
                    }

                    return reader.GetDouble();
                }

                if (reader.TokenType == YamlTokenType.String)
                {
                    if (reader.TryGetDateTime(out DateTime datetime))
                    {
                        return datetime;
                    }

                    return reader.GetString();
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                WriteCallCount++;

                if (value is int i)
                {
                    writer.WriteNumberValue(i);
                }
                else if (value is bool b)
                {
                    writer.WriteBooleanValue(b);
                }
                else if (value is string s)
                {
                    writer.WriteStringValue(s);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        private class ClassWithPrimitives
        {
            public int MyIntProperty { get; set; }
            public bool MyBoolProperty { get; set; }
            public string MyStringProperty { get; set; }
#pragma warning disable 0649
            public int MyIntField;
            public bool MyBoolField;
            public string MyStringField;
#pragma warning restore
        }

        [Fact]
        public static void ClassWithPrimitivesObjectConverter()
        {
            string expected = """
                {
                "MyIntProperty":123,
                "MyBoolProperty":true,
                "MyStringProperty":"Hello",
                "MyIntField":321,
                "MyBoolField":true,
                "MyStringField":"World"
                }
                """;

            string yaml;
            var converter = new PrimitiveConverter();
            var options = new YamlSerializerOptions
            {
                IncludeFields = true
            };
            options.Converters.Add(converter);

            {
                var obj = new ClassWithPrimitives
                {
                    MyIntProperty = 123,
                    MyBoolProperty = true,
                    MyStringProperty = "Hello",
                    MyIntField = 321,
                    MyBoolField = true,
                    MyStringField = "World",
                };

                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(6, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }
            {
                var obj = YamlSerializer.Deserialize<ClassWithPrimitives>(yaml, options);

                Assert.Equal(6, converter.ReadCallCount);

                Assert.Equal(123, obj.MyIntProperty);
                Assert.True(obj.MyBoolProperty);
                Assert.Equal("Hello", obj.MyStringProperty);
                Assert.Equal(321, obj.MyIntField);
                Assert.True(obj.MyBoolField);
                Assert.Equal("World", obj.MyStringField);
            }
        }

        private class ClassWithNullablePrimitives
        {
            public int? MyIntProperty { get; set; }
            public bool? MyBoolProperty { get; set; }
            public string MyStringProperty { get; set; }
#pragma warning disable 0649
            public int? MyIntField;
            public bool? MyBoolField;
            public string MyStringField;
#pragma warning restore
        }

        [Fact]
        public static void ClassWithNullablePrimitivesObjectConverter()
        {
            string expected = """
                {
                "MyIntProperty":123,
                "MyBoolProperty":true,
                "MyStringProperty":"Hello",
                "MyIntField":321,
                "MyBoolField":true,
                "MyStringField":"World"
                }
                """;

            string yaml;
            var converter = new PrimitiveConverter();
            var options = new YamlSerializerOptions
            {
                IncludeFields = true
            };
            options.Converters.Add(converter);

            {
                var obj = new ClassWithNullablePrimitives
                {
                    MyIntProperty = 123,
                    MyBoolProperty = true,
                    MyStringProperty = "Hello",
                    MyIntField = 321,
                    MyBoolField = true,
                    MyStringField = "World",
                };

                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(6, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }
            {
                var obj = YamlSerializer.Deserialize<ClassWithNullablePrimitives>(yaml, options);

                Assert.Equal(123, obj.MyIntProperty);
                Assert.True(obj.MyBoolProperty);
                Assert.Equal("Hello", obj.MyStringProperty);
                Assert.Equal(321, obj.MyIntField);
                Assert.True(obj.MyBoolField);
                Assert.Equal("World", obj.MyStringField);
            }
        }

        [Fact]
        public static void SystemObjectNewtonsoftCompatibleConverterDeserialize()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new SystemObjectNewtonsoftCompatibleConverter());

            {
                const string Value = @"null";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.Null(obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.Null(newtonsoftObj);
            }

            {
                const string Value = """
                    "mystring"
                    """;

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<string>(obj);
                Assert.Equal("mystring", obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<string>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = "true";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<bool>(obj);
                Assert.True((bool)obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<bool>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = "false";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<bool>(obj);
                Assert.False((bool)obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<bool>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = "123";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<long>(obj);
                Assert.Equal((long)123, obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<long>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = "123.45";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<double>(obj);
                Assert.Equal(123.45d, obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<double>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = """
                    "2019-01-30T12:01:02Z"
                    """;

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<DateTime>(obj);
                Assert.Equal(new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc), obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<DateTime>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = """
                    "2019-01-30T12:01:02+01:00"
                    """;

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<DateTime>(obj);

                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<DateTime>(newtonsoftObj);
                Assert.Equal(newtonsoftObj, obj);
            }

            {
                const string Value = "{}";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<YamlElement>(obj);

                // Types are different.
                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<Newtonsoft.Json.Linq.JObject>(newtonsoftObj);
            }

            {
                const string Value = "[]";

                object obj = YamlSerializer.Deserialize<object>(Value, options);
                Assert.IsType<YamlElement>(obj);

                // Types are different.
                object newtonsoftObj = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(Value);
                Assert.IsType<Newtonsoft.Json.Linq.JArray>(newtonsoftObj);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SystemObjectNewtonsoftCompatibleConverterSerialize()
        {
            static void Verify(YamlSerializerOptions options)
            {
                {
                    string yaml = YamlSerializer.Serialize<object>(null, options);
                    Assert.Equal("null", yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(null);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    const string Value = "mystring";

                    string yaml = YamlSerializer.Serialize<object>(Value, options);
                    Assert.Equal("""
                        "mystring"
                        """, yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(Value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    string yaml = YamlSerializer.Serialize<object>(true, options);
                    Assert.Equal("true", yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(true);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    string yaml = YamlSerializer.Serialize<object>(false, options);
                    Assert.Equal("false", yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(false);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    const long Value = 123;

                    object yaml = YamlSerializer.Serialize<object>(123, options);
                    Assert.Equal("123", yaml);

                    object newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(Value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    const double Value = 123.45;

                    object yaml = YamlSerializer.Serialize<object>(Value, options);
                    Assert.Equal("123.45", yaml);

                    object newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(Value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    var value = new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc);

                    string yaml = YamlSerializer.Serialize<object>(value, options);
                    Assert.Equal("""
                        "2019-01-30T12:01:02Z"
                        """, yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    var value = new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0));

                    string yaml = YamlSerializer.Serialize<object>(value, options);
                    Assert.Equal("""
                        "2019-01-30T12:01:02+01:00"
                        """, yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    var value = new object();

                    string yaml = YamlSerializer.Serialize<object>(new object(), options);
                    Assert.Equal("{}", yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }

                {
                    var value = new int[] { };

                    string yaml = YamlSerializer.Serialize<object>(value, options);
                    Assert.Equal("[]", yaml);

                    string newtonsoftYaml = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                    Assert.Equal(newtonsoftYaml, yaml);
                }
            }

            // Results should be the same with or without the custom converter since the
            // serializer calls value.GetType() for every property value declared as System.Object.
            Verify(new YamlSerializerOptions());

            var options = new YamlSerializerOptions();
            options.Converters.Add(new SystemObjectNewtonsoftCompatibleConverter());
            Verify(options);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CanCustomizeSystemObjectSerialization()
        {
            var options = new YamlSerializerOptions { Converters = { new CustomSystemObjectConverter() } };

            string expectedYaml = "42";
            string actualYaml = YamlSerializer.Serialize(new object(), options);
            Assert.Equal(expectedYaml, actualYaml);
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(false)]
        [InlineData("string")]
        [InlineData(3.1415926)]
        [Trait("category", "jsonoutput")]
        public static void CustomSystemObjectConverter_DoesNotUsePolymorphismInAllContexts(object value)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/72681

            var options = new YamlSerializerOptions { Converters = { new CustomSystemObjectConverter() } };

            TestValue(value, "42");
            TestValue(new { Value = value }, """{"Value":42}""");
            TestValue(new object[] { value }, "[42]");
            TestValue(new Dictionary<string, object> { ["key"] = value }, """{"key":42}""");

            void TestValue<T>(T value, string expectedYaml)
            {
                string yaml = YamlSerializer.Serialize(value, options);
                Assert.Equal(expectedYaml, yaml);

                YamlTypeInfo<T> yamlTypeInfo = options.GetTypeInfo<T>();
                yaml = YamlSerializer.Serialize(value, yamlTypeInfo);
                Assert.Equal(expectedYaml, yaml);
            }
        }

        private class CustomSystemObjectConverter : YamlConverter<object>
        {
            public override object? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
                => writer.WriteNumberValue(42);
        }
    }

}
