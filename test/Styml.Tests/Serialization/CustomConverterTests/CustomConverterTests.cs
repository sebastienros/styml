// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.RemoteExecutor;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void MultipleConvertersInObjectArray()
        {
            const string expectedYaml = """["?",{"TypeDiscriminator":1,"CreditLimit":100,"Name":"C"},null]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new MyBoolEnumConverter());
            options.Converters.Add(new PersonConverterWithTypeDiscriminator());

            Customer customer = new Customer();
            customer.CreditLimit = 100;
            customer.Name = "C";

            MyBoolEnum myBoolEnum = MyBoolEnum.Unknown;
            MyBoolEnum? myNullBoolEnum = null;

            string yaml = YamlSerializer.Serialize(new object[] { myBoolEnum, customer, myNullBoolEnum }, options);
            Assert.Equal(expectedYaml, yaml);

            YamlElement yamlElement = YamlSerializer.Deserialize<YamlElement>(yaml, options);
            string yamlElementString = yamlElement.ToString();
            Assert.Equal(expectedYaml, yamlElementString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void OptionsArePassedToCreateConverter()
        {
            TestFactory factory = new TestFactory();
            YamlSerializerOptions options = new YamlSerializerOptions { Converters = { factory } };
            string yaml = YamlSerializer.Serialize("Test", options);
            Assert.Equal("""
                "Test"
                """, yaml);
            Assert.Same(options, factory.Options);
        }

        public class TestFactory : YamlConverterFactory
        {
            public YamlSerializerOptions Options { get; private set; }

            public override bool CanConvert(Type typeToConvert) => true;

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                Options = options;
                return new SimpleConverter();
            }

            public class SimpleConverter : YamlConverter<string>
            {
                public override string Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    throw new NotImplementedException();
                }

                public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options)
                    => writer.WriteStringValue(value);
            }
        }

        private class ConverterReturningNull : YamlConverter<Customer>
        {
            public override Customer Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);

                bool rc = reader.Read();
                Assert.True(rc);

                Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

                return null;
            }

            public override void Write(Utf8YamlWriter writer, Customer value, YamlSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        public static void VerifyConverterWithTrailingWhitespace()
        {
            string yaml = "{}   ";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ConverterReturningNull());

            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            // The serializer will finish reading the whitespace and no exception will be thrown.
            Customer c = YamlSerializer.Deserialize<Customer>(utf8, options);

            Assert.Null(c);
        }

        [Fact]
        public static void VerifyConverterWithTrailingComments()
        {
            string yaml = "{}  //";
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            // Disallow comments
            var options = new YamlSerializerOptions();
            options.Converters.Add(new ConverterReturningNull());
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Customer>(utf8, options));

            // Skip comments
            options = new YamlSerializerOptions();
            options.Converters.Add(new ConverterReturningNull());
            options.ReadCommentHandling = YamlCommentHandling.Skip;
            Customer c = YamlSerializer.Deserialize<Customer>(utf8, options);
            Assert.Null(c);
        }

        public class ObjectBoolConverter : YamlConverter<object>
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

                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public static void VerifyObjectConverterWithPreservedReferences()
        {
            var yaml = "true";
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            var options = new YamlSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve,
            };
            options.Converters.Add(new ObjectBoolConverter());

            object obj = (YamlSerializer.Deserialize<object>(utf8, options));

            Assert.IsType<bool>(obj);
            Assert.Equal(true, obj);
        }

        [Fact]
        public static void GetConverterRootsBuiltInConverters()
        {
            YamlSerializerOptions options = new();
            RunTest<DateTime>();
            RunTest<Point_2D>();

            void RunTest<TConverterReturn>()
            {
                YamlConverter converter = options.GetConverter(typeof(TConverterReturn));
                Assert.NotNull(converter);
                Assert.True(converter is YamlConverter<TConverterReturn>);
            }
        }

        [ActiveIssue("https://github.com/dotnet/runtime/issues/66232", TargetFrameworkMonikers.NetFramework)]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/66371", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoInterpreter))]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void GetConverter_Poco_WriteThrowsNotSupportedException()
        {
            RemoteExecutor.Invoke(static () =>
            {
                YamlSerializerOptions options = new();
                YamlConverter<Point_2D> converter = (YamlConverter<Point_2D>)options.GetConverter(typeof(Point_2D));

                using var writer = new Utf8YamlWriter(new MemoryStream());
                var value = new Point_2D(0, 0);

                // Running the converter without priming the options instance
                // for reflection-based serialization should throw NotSupportedException
                // since it can't resolve reflection-based metadata.
                Assert.Throws<NotSupportedException>(() => converter.Write(writer, value, options));
                Assert.Equal(0, writer.BytesCommitted + writer.BytesPending);
                options.IncludeFields = false; // options should still be mutable

                YamlSerializer.Serialize(42, options);

                // Same operation should succeed when instance has been primed.
                converter.Write(writer, value, options);
                Assert.NotEqual(0, writer.BytesCommitted + writer.BytesPending);
                writer.Reset();

                Assert.Throws<InvalidOperationException>(() => options.IncludeFields = false);

                // State change should not leak into unrelated options instances.
                var options2 = new YamlSerializerOptions();
                new YamlContext(options2);
                Assert.Throws<NotSupportedException>(() => converter.Write(writer, value, options2));
                Assert.Equal(0, writer.BytesCommitted + writer.BytesPending);
            }).Dispose();
        }

        [Fact]
        public static void GetConverterTypeToConvertNull()
        {
            Assert.Throws<ArgumentNullException>(() => (new YamlSerializerOptions()).GetConverter(typeToConvert: null!));
        }

        [Fact]
        public static void ErrorMessageContainsExpectedType()
        {
            YamlSerializerOptions options = new();
            options.Converters.Add(new InvalidYamlConverterFactory());
            var ex = Assert.Throws<InvalidOperationException>(() => 
                YamlSerializer.Serialize(new InvalidTestInfo("Hello"), options));
            Assert.Contains(typeof(InvalidTestInfo).Name, ex.Message);
        }

        private sealed record InvalidTestInfo(string Name);

        private sealed class InvalidYamlConverterFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) => true;

            public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
                => new MyBoolEnumConverter();
        }

        [Theory]
        [InlineData(typeof(ConverterReturningNull), typeof(Customer))]
        [InlineData(typeof(TestFactory.SimpleConverter), typeof(string))]
        [InlineData(typeof(ObjectBoolConverter), typeof(object))]
        [InlineData(typeof(Int32NullConverter), typeof(int))]
        [InlineData(typeof(YamlStringEnumConverter), null)]
        [InlineData(typeof(YamlStringEnumConverter<MyBoolEnum>), null)]
        [InlineData(typeof(InvalidYamlConverterFactory), null)]
        [InlineData(typeof(TestFactory), null)]
        public static void YamlConverter_TypeProperty_ReturnsExpectedResult(Type converterType, Type? expectedType)
        {
            var converter = (YamlConverter)Activator.CreateInstance(converterType)!;
            Assert.Equal(expectedType, converter.Type);
        }

        [Theory]
        [InlineData(typeof(bool))]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(MyBoolEnum))]
        [InlineData(typeof(Customer))]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(Dictionary<string, int>))]
        public static void YamlSerializerOptions_GetConverter_TypeProperty_ReturnsExpectedResult(Type type)
        {
            YamlConverter converter = YamlSerializerOptions.Default.GetConverter(type);
            Assert.Equal(type, converter.Type);
        }
    }
}
