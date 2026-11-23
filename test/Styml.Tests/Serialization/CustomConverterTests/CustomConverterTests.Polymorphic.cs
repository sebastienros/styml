// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        // A polymorphic POCO converter using a type discriminator.
        private class PersonConverterWithTypeDiscriminator : YamlConverter<Person>
        {
            enum TypeDiscriminator
            {
                Customer = 1,
                Employee = 2
            }

            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Person).IsAssignableFrom(typeToConvert);
            }

            public override Person Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                reader.Read();
                if (reader.TokenType != YamlTokenType.PropertyName)
                {
                    throw new YamlException();
                }

                string propertyName = reader.GetString();
                if (propertyName != "TypeDiscriminator")
                {
                    throw new YamlException();
                }

                reader.Read();
                if (reader.TokenType != YamlTokenType.Number)
                {
                    throw new YamlException();
                }

                Person value;
                TypeDiscriminator typeDiscriminator = (TypeDiscriminator)reader.GetInt32();
                switch (typeDiscriminator)
                {
                    case TypeDiscriminator.Customer:
                        value = new Customer();
                        break;

                    case TypeDiscriminator.Employee:
                        value = new Employee();
                        break;

                    default:
                        throw new YamlException();
                }

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        return value;
                    }

                    if (reader.TokenType == YamlTokenType.PropertyName)
                    {
                        propertyName = reader.GetString();
                        reader.Read();
                        switch (propertyName)
                        {
                            case "CreditLimit":
                                decimal creditLimit = reader.GetDecimal();
                                ((Customer)value).CreditLimit = creditLimit;
                                break;
                            case "OfficeNumber":
                                string officeNumber = reader.GetString();
                                ((Employee)value).OfficeNumber = officeNumber;
                                break;
                            case "Name":
                                string name = reader.GetString();
                                value.Name = name;
                                break;
                        }
                    }
                }

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, Person value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                if (value is Customer)
                {
                    writer.WriteNumber("TypeDiscriminator", (int)TypeDiscriminator.Customer);
                    writer.WriteNumber("CreditLimit", ((Customer)value).CreditLimit);
                }
                else if (value is Employee)
                {
                    writer.WriteNumber("TypeDiscriminator", (int)TypeDiscriminator.Employee);
                    writer.WriteString("OfficeNumber", ((Employee)value).OfficeNumber);
                }

                writer.WriteString("Name", value.Name);

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void PersonConverterPolymorphicTypeDiscriminator()
        {
            const string customerYaml = """{"TypeDiscriminator":1,"CreditLimit":100.00,"Name":"C"}""";
            const string employeeYaml = """{"TypeDiscriminator":2,"OfficeNumber":"77a","Name":"E"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PersonConverterWithTypeDiscriminator());

            {
                Person person = YamlSerializer.Deserialize<Person>(customerYaml, options);
                Assert.IsType<Customer>(person);
                Assert.Equal(100, ((Customer)person).CreditLimit);
                Assert.Equal("C", person.Name);

                string yaml = YamlSerializer.Serialize(person, options);
                Assert.Equal(customerYaml, yaml);
            }

            {
                Person person = YamlSerializer.Deserialize<Person>(employeeYaml, options);
                Assert.IsType<Employee>(person);
                Assert.Equal("77a", ((Employee)person).OfficeNumber);
                Assert.Equal("E", person.Name);

                string yaml = YamlSerializer.Serialize(person, options);
                Assert.Equal(employeeYaml, yaml);
            }
        }

        [Fact]
        public static void NullPersonConverterPolymorphicTypeDiscriminator()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new PersonConverterWithTypeDiscriminator());

            Person person = YamlSerializer.Deserialize<Person>("null");
            Assert.Null(person);
        }

        // A converter that can serialize an abstract Person type.
        private class PersonPolymorphicSerializerConverter : YamlConverter<Person>
        {
            public override Person Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotSupportedException($"Deserializing not supported. Type={typeToConvert}.");
            }

            public override void Write(Utf8YamlWriter writer, Person value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void PersonConverterSerializerPolymorphic()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new PersonPolymorphicSerializerConverter());

            Customer customer = new Customer
            {
                Name = "C",
                CreditLimit = 100
            };

            {
                // Verify the polymorphic case.
                Person person = customer;

                string yaml = YamlSerializer.Serialize(person, options);
                Assert.Contains("""
                    "CreditLimit":100
                    """, yaml);
                Assert.Contains("""
                    "Name":"C"
                    """, yaml);
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<Person>(yaml, options));

                string arrayYaml = YamlSerializer.Serialize(new Person[] { person }, options);
                Assert.Contains("""
                    "CreditLimit":100
                    """, arrayYaml);
                Assert.Contains("""
                    "Name":"C"
                    """, arrayYaml);
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<Person[]>(arrayYaml, options));
            }

            {
                // Ensure (de)serialization still works when using a Person-derived type. This does not call the custom converter.
                string yaml = YamlSerializer.Serialize(customer, options);
                Assert.Contains("""
                    "CreditLimit":100
                    """, yaml);
                Assert.Contains("""
                    "Name":"C"
                    """, yaml);

                customer = YamlSerializer.Deserialize<Customer>(yaml, options);
                Assert.Equal(100, customer.CreditLimit);
                Assert.Equal("C", customer.Name);

                string arrayYaml = YamlSerializer.Serialize(new Customer[] { customer }, options);
                Assert.Contains("""
                    "CreditLimit":100
                    """, arrayYaml);
                Assert.Contains("""
                    "Name":"C"
                    """, arrayYaml);

                Customer[] customers = YamlSerializer.Deserialize<Customer[]>(arrayYaml, options);
                Assert.Equal(100, customers[0].CreditLimit);
                Assert.Equal("C", customers[0].Name);
            }
        }

        [Theory]
        [MemberData(nameof(PolymorphicConverter_ShouldRoundtripInAllContexts_GetTestData))]
        [Trait("category", "jsonoutput")]
        public static void PolymorphicConverter_ShouldRoundtripInAllContexts<T>(T value, string expectedYaml)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/46522
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(expectedYaml, yaml);

            T deserializedValue = YamlSerializer.Deserialize<T>(yaml);
            yaml = YamlSerializer.Serialize(deserializedValue);
            Assert.Equal(expectedYaml, yaml);
        }

        public static IEnumerable<object[]> PolymorphicConverter_ShouldRoundtripInAllContexts_GetTestData()
        {
            var value = new SampleRepro { Value = "string" };
            string expectedYaml = "\"string\"";

            yield return WrapArgs(value, expectedYaml);
            yield return WrapArgs(new { Value = value }, $@"{{""Value"":{expectedYaml}}}");
            yield return WrapArgs(new[] { value }, $"[{expectedYaml}]");
            yield return WrapArgs(new Dictionary<string, SampleRepro> { ["key"] = value }, $@"{{""key"":{expectedYaml}}}");

            static object[] WrapArgs<T>(T value, string expectedYaml) => new object[] { value, expectedYaml };
        }

        public interface IRepro<T>
        {
            T Value { get; }
        }

        [YamlConverter(typeof(ReproYamlConverter))]
        public class SampleRepro : IRepro<object>
        {
            public object Value { get; set; }
        }

        public sealed class ReproYamlConverter : YamlConverter<IRepro<object>>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(IRepro<object>).IsAssignableFrom(typeToConvert);
            }

            public override IRepro<object> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new SampleRepro { Value = YamlSerializer.Deserialize<object>(ref reader, options) };
            }

            public override void Write(Utf8YamlWriter writer, IRepro<object> value, YamlSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    YamlSerializer.Serialize(writer, value.Value, options);
                }
            }
        }

        [Fact]
        public static void PolymorphicBaseClassConverter_IsPassedCorrectTypeToConvertParameter()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/77173
            var options = new YamlSerializerOptions { Converters = { new PolymorphicBaseClassConverter() } };

            // Sanity check -- returns converter for the base class.
            YamlConverter converter = options.GetConverter(typeof(DerivedClass));
            Assert.IsAssignableFrom<PolymorphicBaseClassConverter>(converter);

            // Validate that the correct typeToConvert parameter is passed in all serialization contexts:
            // 1. Typed root value.
            // 2. Untyped root value (where the reported regression occurred).
            // 3. Nested values in POCOs, collections & dictionaries.

            DerivedClass result = YamlSerializer.Deserialize<DerivedClass>("{}", options);
            Assert.IsType<DerivedClass>(result);

            object objResult = YamlSerializer.Deserialize("{}", typeof(DerivedClass), options);
            Assert.IsType<DerivedClass>(objResult);

            PocoWithDerivedClassProperty pocoResult = YamlSerializer.Deserialize<PocoWithDerivedClassProperty>("""{"Value":{}}""", options);
            Assert.IsType<DerivedClass>(pocoResult.Value);

            DerivedClass[] arrayResult = YamlSerializer.Deserialize<DerivedClass[]>("[{}]", options);
            Assert.IsType<DerivedClass>(arrayResult[0]);

            Dictionary<string, DerivedClass> dictResult = YamlSerializer.Deserialize<Dictionary<string, DerivedClass>>("""{"Value":{}}""", options);
            Assert.IsType<DerivedClass>(dictResult["Value"]);
        }

        public class BaseClass
        { }

        public class DerivedClass : BaseClass
        { }

        public class PocoWithDerivedClassProperty
        {
            public DerivedClass Value { get; set; }
        }

        public class PolymorphicBaseClassConverter : YamlConverter<BaseClass>
        {
            public override bool CanConvert(Type typeToConvert) => typeof(BaseClass).IsAssignableFrom(typeToConvert);

            public override BaseClass? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(typeof(DerivedClass), typeToConvert);
                reader.Skip();
                return (BaseClass)Activator.CreateInstance(typeToConvert);
            }

            public override void Write(Utf8YamlWriter writer, BaseClass value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }
}
