// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        private class PocoWithNoBaseClass { }
        private class DerivedCustomer : Customer { }
        private class SuccessException : Exception { }

        private class BadCustomerConverter : YamlConverter<Customer>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                // Say this converter supports all types even though we specify "Customer".
                return true;
            }

            public override Customer Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new SuccessException();
            }

            public override void Write(Utf8YamlWriter writer, Customer value, YamlSerializerOptions options)
            {
                throw new SuccessException();
            }
        }

        [Fact]
        public static void ContraVariantConverterFail()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new BadCustomerConverter());

            // Incompatible types.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<int>("0", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(0, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithNoBaseClass>("{}", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithNoBaseClass(), options));

            // Contravariant to Customer.
            Assert.Throws<SuccessException>(() => YamlSerializer.Deserialize<DerivedCustomer>("{}", options));
            Assert.Throws<SuccessException>(() => YamlSerializer.Serialize(new DerivedCustomer(), options));

            // Covariant to Customer.
            Assert.Throws<SuccessException>(() => YamlSerializer.Deserialize<Customer>("{}", options));
            Assert.Throws<SuccessException>(() => YamlSerializer.Serialize(new Customer(), options));
            Assert.Throws<SuccessException>(() => YamlSerializer.Serialize<Customer>(new DerivedCustomer(), options));

            Assert.Throws<SuccessException>(() => YamlSerializer.Deserialize<Person>("{}", options));
            Assert.Throws<SuccessException>(() => YamlSerializer.Serialize<Person>(new Customer(), options));
            Assert.Throws<SuccessException>(() => YamlSerializer.Serialize<Person>(new DerivedCustomer(), options));
        }

        private class InvalidConverterAttribute : YamlConverterAttribute
        {
            // converterType is not valid since typeof(int) is not a type that derives from YamlConverter.
            public InvalidConverterAttribute() : base(converterType: typeof(int)) { }
        }

        private class PocoWithInvalidConverter
        {
            [InvalidConverter]
            public int MyInt { get; set; }
        }

        private class NullConverterAttribute : YamlConverterAttribute
        {
            public NullConverterAttribute() : base(null) { }

            public override YamlConverter CreateConverter(Type typeToConvert)
            {
                return null;
            }
        }

        private class PocoWithNullConverter
        {
            [NullConverter]
            public int MyInt { get; set; }
        }

        [Fact]
        public static void AttributeCreateConverterFail()
        {
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithInvalidConverter()));
            // Message should be in the form "The converter specified on 'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt' does not derive from YamlConverter or have a public parameterless constructor."
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithInvalidConverter>("{}"));
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithNullConverter()));
            // Message should be in the form "The converter specified on 'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'  is not compatible with the type 'System.Int32'."
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'", ex.Message);
            Assert.Contains("'System.Int32'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithNullConverter>("{}"));
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'", ex.Message);
            Assert.Contains("'System.Int32'", ex.Message);
        }

        private class InvalidTypeConverterClass
        {
            [YamlConverter(typeof(YamlStringEnumConverter))]
            public ICollection<InvalidTypeConverterEnum> MyEnumValues { get; set; }
        }

        private class InvalidEnumTypeConverterClass
        {
            [YamlConverter(typeof(YamlStringEnumConverter<BindingFlags>))]
            public InvalidTypeConverterEnum MyEnumValues { get; set; }
        }

        private enum InvalidTypeConverterEnum
        {
            Value1,
            Value2,
        }

        [Theory]
        [InlineData(typeof(InvalidTypeConverterClass),
            "System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClass.MyEnumValues",
            "System.Collections.Generic.ICollection`1[System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnum]")]
        [InlineData(typeof(InvalidEnumTypeConverterClass),
            "System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidEnumTypeConverterClass.MyEnumValues",
            "System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnum")]
        public static void AttributeOnPropertyFail(Type type, string propertyName, string propertyTypeName)
        {
            object value = Activator.CreateInstance(type);
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, type));
            // Message should be in the form "The converter specified on 'System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClass.MyEnumValues' is not compatible with the type 'System.Collections.Generic.ICollection`1[System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnum]'."
            Assert.Contains($"'{propertyName}'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("{}", type));
            Assert.Contains($"'{propertyName}'", ex.Message);
            Assert.Contains($"'{propertyTypeName}'", ex.Message);
        }

        [YamlConverter(typeof(YamlStringEnumConverter))]
        private class InvalidTypeConverterClassWithAttribute { }

        [YamlConverter(typeof(YamlStringEnumConverter<BindingFlags>))]
        private enum InvalidTypeConverterEnumWithAttribute { }

        [Theory]
        [InlineData(typeof(InvalidTypeConverterClassWithAttribute), "System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClassWithAttribute")]
        [InlineData(typeof(InvalidTypeConverterEnumWithAttribute), "System.Text.Yaml.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnumWithAttribute")]
        public static void AttributeOnClassFail(Type type, string expectedSubStr)
        {
            InvalidOperationException ex;
            object value = Activator.CreateInstance(type);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, type));

            int pos = ex.Message.IndexOf(expectedSubStr);
            Assert.True(pos > 0);
            Assert.Contains(expectedSubStr, ex.Message.Substring(pos + expectedSubStr.Length)); // The same string is repeated again.

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("{}", type));
            pos = ex.Message.IndexOf(expectedSubStr);
            Assert.True(pos > 0);
            Assert.Contains(expectedSubStr, ex.Message.Substring(pos + expectedSubStr.Length));
        }

        private class ConverterFactoryThatReturnsNull : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                // To verify the nullable converter, don't convert Nullable.
                return Nullable.GetUnderlyingType(typeToConvert) == null;
            }

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                return null;
            }
        }

        [Fact]
        public static void ConverterThatReturnsNullFail()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new ConverterFactoryThatReturnsNull());

            // A null return value from CreateConverter() will generate a InvalidOperationException with the type name.
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(0, options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<int>("0", options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);

            // This will invoke the Nullable converter which should detect a null converter.
            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<int?>("0", options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);
        }

        private class ConverterFactoryThatReturnsYamlConverterFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type type) => true;

            public override YamlConverter CreateConverter(Type type, YamlSerializerOptions options) => new ConverterFactoryThatReturnsYamlConverterFactory();
        }

        [Fact]
        public static void CustomYamlConverterFactoryThatReturnsYamlConverterFactoryFail()
        {
            YamlSerializerOptions options = new()
            {
                Converters = { new ConverterFactoryThatReturnsYamlConverterFactory() }
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(1, options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsYamlConverterFactory).ToString(), ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<int>("1", options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsYamlConverterFactory).ToString(), ex.Message);
        }

        private class Level1
        {
            public Level1()
            {
                Level2 = new Level2();
                Level2.Level3s = new Level3[] { new Level3() };
            }

            public Level2 Level2 { get; set; }
        }

        private class Level2
        {
            public Level3[] Level3s {get; set; }
        }

        private class Level3
        {
            // If true, read\write too much instead of too little.
            public bool ReadWriteTooMuch { get; set; }
        }

        private class Level3ConverterThatsBad: YamlConverter<Level3>
        {
            public override Level3 Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);

                reader.Read();
                Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);

                reader.Read();
                Assert.True(reader.TokenType == YamlTokenType.True || reader.TokenType == YamlTokenType.False);

                // Determine if we should read too much.
                if (reader.TokenType == YamlTokenType.True)
                {
                    // Do the correct read.
                    reader.Read();
                    Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

                    // Do an extra read.
                    reader.Read();
                    Assert.Equal(YamlTokenType.EndArray, reader.TokenType);

                    // End on EndObject token so it looks good, but wrong depth.
                    reader.Read();
                    Assert.Equal(YamlTokenType.EndObject, reader.TokenType);
                }

                return new Level3();
            }

            public override void Write(Utf8YamlWriter writer, Level3 value, YamlSerializerOptions options)
            {
                if (value.ReadWriteTooMuch)
                {
                    writer.WriteStartObject();
                }
            }
        }

        [Fact]
        public static void ConverterReadTooLittle()
        {
            const string yaml = """{"Level2":{"Level3s":[{"ReadWriteTooMuch":false}]}}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            try
            {
                YamlSerializer.Deserialize<Level1>(yaml, options);
                Assert.Fail("Expected exception");
            }
            catch (YamlException ex)
            {
                Assert.Contains("$.Level2.Level3s[0]", ex.Message);
                Assert.Equal("$.Level2.Level3s[0]", ex.Path);
            }
        }

        [Fact]
        public static void ConverterReadTooMuch()
        {
            const string yaml = """{"Level2":{"Level3s":[{"ReadWriteTooMuch":true}]}}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad ());

            try
            {
                YamlSerializer.Deserialize<Level1>(yaml, options);
                Assert.Fail("Expected exception");
            }
            catch (YamlException ex)
            {
                Assert.Contains("$.Level2.Level3s[0]", ex.Message);
                Assert.Equal("$.Level2.Level3s[0]", ex.Path);
            }
        }

        [Fact]
        public static void ConverterWroteNothing()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            // Not writing is allowed.
            string str = YamlSerializer.Serialize(new Level1(), options);
            Assert.False(string.IsNullOrEmpty(str));
        }

        [Fact]
        public static void ConverterWroteTooMuch()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            try
            {
                var l1 = new Level1();
                l1.Level2.Level3s[0].ReadWriteTooMuch = true;

                YamlSerializer.Serialize(l1, options);
                Assert.Fail("Expected exception");
            }
            catch (YamlException ex)
            {
                Assert.Contains("$.Level2.Level3s", ex.Message);
                Assert.Equal("$.Level2.Level3s", ex.Path);
            }
        }

        private class PocoWithTwoConvertersOnProperty
        {
            [InvalidConverter]
            [PointConverter]
            public int MyInt { get; set; }
        }

        [Fact]
        public static void PropertyHasMoreThanOneConverter()
        {
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithTwoConvertersOnProperty()));
            // Message should be in the form "The attribute 'System.Text.Yaml.Serialization.YamlConverterAttribute' cannot exist more than once on 'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'."
            Assert.Contains("'System.Text.Yaml.Serialization.YamlConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithTwoConvertersOnProperty>("{}"));
            Assert.Contains("'System.Text.Yaml.Serialization.YamlConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'", ex.Message);
        }

        [InvalidConverter]
        [PointConverter]
        private class PocoWithTwoConverters
        {
            public int MyInt { get; set; }
        }

        [Fact]
        public static void TypeHasMoreThanOneConverter()
        {
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithTwoConverters()));
            // Message should be in the form "The attribute 'System.Text.Yaml.Serialization.YamlConverterAttribute' cannot exist more than once on 'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'."
            Assert.Contains("'System.Text.Yaml.Serialization.YamlConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithTwoConverters>("{}"));
            Assert.Contains("'System.Text.Yaml.Serialization.YamlConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'", ex.Message);
        }

        [Fact]
        public static void ConverterWithoutDefaultCtor()
        {
            string yaml = """{"MyType":"ABC"}""";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ClassWithConverterWithoutPublicEmptyCtor>(yaml));
            Assert.Contains("'System.Text.Yaml.Serialization.Tests.CustomConverterTests+ClassWithConverterWithoutPublicEmptyCtor'", ex.Message);
        }

        [YamlConverter(typeof(ConverterWithoutPublicEmptyCtor))]
        public class ClassWithConverterWithoutPublicEmptyCtor
        {
            public string MyType { get; set; }
        }

        internal class ConverterWithoutPublicEmptyCtor : YamlConverter<ClassWithConverterWithoutPublicEmptyCtor>
        {
            public ConverterWithoutPublicEmptyCtor(int x)
            {
            }

            public override ClassWithConverterWithoutPublicEmptyCtor Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, ClassWithConverterWithoutPublicEmptyCtor value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }


        [Fact]
        public static void BadDoubleConverter_Serialization()
        {
            var options = new YamlSerializerOptions
            {
                Converters = { new BadDoubleConverter() }
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize<double?>(3.14, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new { value = (double?)3.14 }, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new double?[] { 3.14 }, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new Dictionary<string, double?> { ["key"] = 3.14 }, options));
        }

        [Fact]
        public static void BadDoubleConverter_Deserialization()
        {
            var options = new YamlSerializerOptions
            {
                Converters = { new BadDoubleConverter() }
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<double?>("3.14", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithGenericProperty<double?>>("""{"Property":3.14}""", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<double?[]>("[3.14]", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<Dictionary<string, double?>>("""{"key":3.14}""", options));
        }


        [Fact]
        public static void BadNullableDoubleConverter_Serialization()
        {
            var options = new YamlSerializerOptions
            {
                Converters = { new BadNullableDoubleConverter() }
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(3.14, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new { value = 3.14 }, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new[] { 3.14 }, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new Dictionary<string, double> { ["key"] = 3.14 }, options));
        }

        [Fact]
        public static void BadNullableDoubleConverter_Deserialization()
        {
            var options = new YamlSerializerOptions
            {
                Converters = { new BadNullableDoubleConverter() }
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<double>("3.14", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PocoWithGenericProperty<double>>("""{"Property":3.14}""", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<double[]>("[3.14]", options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<Dictionary<string, double>>("""{"key":3.14}""", options));
        }

        private class PocoWithGenericProperty<T>
        {
            public T Property { get; set; }
        }

        /// <summary>
        /// A double converter that incorrectly claims to support double? types.
        /// </summary>
        private class BadDoubleConverter : YamlConverter<double>
        {
            public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(double) || typeToConvert == typeof(double?);
            public override double Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetDouble();
            public override void Write(Utf8YamlWriter writer, double value, YamlSerializerOptions options) => writer.WriteNumberValue(value);
        }

        /// <summary>
        /// A double? converter that incorrectly claims to support double types.
        /// </summary>
        private class BadNullableDoubleConverter : YamlConverter<double?>
        {
            public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(double) || typeToConvert == typeof(double?);
            public override double? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetDouble();
            public override void Write(Utf8YamlWriter writer, double? value, YamlSerializerOptions options) => writer.WriteNumberValue(value.Value);
        }
    }
}
