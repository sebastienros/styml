// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Tests;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class YamlValueTests
    {
        [Fact]
        public static void CreateFromNull()
        {
            Assert.Null(YamlValue.Create((bool?)null));
            Assert.Null(YamlValue.Create((string)null));
            Assert.Null(YamlValue.Create((YamlElement?)null));
            Assert.Null(YamlValue.Create(YamlElement.Parse("null")));

            Assert.Null(YamlValue.Create((object)null));
            Assert.Null(YamlValue.Create((bool?)null));
            Assert.Null(YamlValue.Create((string)null));
            Assert.Null(YamlValue.Create((YamlElement?)null));
            Assert.Null(YamlValue.Create(YamlElement.Parse("null")));
        }

        [Fact]
        public static void CreateFromNode_Fail()
        {
            Assert.Throws<ArgumentException>(() => YamlValue.Create(new YamlArray()));
            Assert.Throws<ArgumentException>(() => YamlValue.Create(new YamlObject()));
            Assert.Throws<ArgumentException>(() => YamlValue.Create(YamlValue.Create(42)));
        }

        private class Polymorphic_Base { }
        private class Polymorphic_Derived : Polymorphic_Base { }

        [Fact]
        public static void Polymorphic()
        {
            YamlValue value;

            Polymorphic_Base baseClass = new Polymorphic_Derived();
            value = YamlValue.Create(baseClass);
            Assert.Same(baseClass, value.GetValue<Polymorphic_Derived>());

            Polymorphic_Derived derivedClass = new Polymorphic_Derived();
            value = YamlValue.Create(derivedClass);
            Assert.Same(derivedClass, value.GetValue<Polymorphic_Base>());

            Assert.Same(derivedClass, value.GetValue<object>());
        }

        [Fact]
        public static void QuotedNumbers_Deserialize()
        {
            var options = new YamlSerializerOptions();
            options.NumberHandling = YamlNumberHandling.AllowReadingFromString |
                YamlNumberHandling.AllowNamedFloatingPointLiterals;

            YamlNode node = YamlSerializer.Deserialize<YamlNode>("\"42\"", options);
            Assert.IsAssignableFrom<YamlValue>(node);
            Assert.Throws<InvalidOperationException>(() => node.GetValue<int>());

            // A second pass is needed to obtain the quoted number.
            Assert.Equal(42, YamlSerializer.Deserialize<int>(node.ToYamlString(), options));

            node = YamlSerializer.Deserialize<YamlNode>("\"NaN\"", options);
            Assert.IsAssignableFrom<YamlValue>(node);
            Assert.Equal(double.NaN, YamlSerializer.Deserialize<double>(node.ToYamlString(), options));
            Assert.Equal(float.NaN, YamlSerializer.Deserialize<float>(node.ToYamlString(), options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void QuotedNumbers_Serialize()
        {
            var options = new YamlSerializerOptions();
            options.NumberHandling = YamlNumberHandling.WriteAsString;

            YamlValue obj = YamlValue.Create(42);
            string yaml = obj.ToYamlString(options);
            Assert.Equal("\"42\"", yaml);

            obj = YamlValue.Create(double.NaN);
            yaml = obj.ToYamlString(options);
            Assert.Equal("\"NaN\"", yaml);
        }

        [Fact]
        public static void FromElement()
        {
            using (YamlDocument document = YamlDocument.Parse("42"))
            {
                YamlValue jValue = YamlValue.Create(document.RootElement);
                Assert.Equal(42, jValue.GetValue<int>());
            }

            using (YamlDocument document = YamlDocument.Parse("null"))
            {
                YamlValue? jValue = YamlValue.Create(document.RootElement);
                Assert.Null(jValue);
            }
        }

        [Fact]
        public static void TryGetValue_FromString()
        {
            YamlValue jValue = YamlNode.Parse("\"MyString\"").AsValue();

            Assert.True(jValue.TryGetValue(out string _));
            Assert.True(jValue.TryGetValue(out object _));
            Assert.False(jValue.TryGetValue(out char _));
            Assert.False(jValue.TryGetValue(out byte _));
            Assert.False(jValue.TryGetValue(out short _));
            Assert.False(jValue.TryGetValue(out int _));
            Assert.False(jValue.TryGetValue(out long _));
            Assert.False(jValue.TryGetValue(out sbyte _));
            Assert.False(jValue.TryGetValue(out ushort _));
            Assert.False(jValue.TryGetValue(out uint _));
            Assert.False(jValue.TryGetValue(out ulong _));
            Assert.False(jValue.TryGetValue(out bool _));
            Assert.False(jValue.TryGetValue(out float _));
            Assert.False(jValue.TryGetValue(out double _));
            Assert.False(jValue.TryGetValue(out decimal _));
            Assert.False(jValue.TryGetValue(out DateTime _));
            Assert.False(jValue.TryGetValue(out DateTimeOffset _));
            Assert.False(jValue.TryGetValue(out Guid _));
        }

        [Fact]
        public static void TryGetValue_FromNumber()
        {
            YamlValue jValue = YamlNode.Parse("42").AsValue();

            Assert.True(jValue.TryGetValue(out byte _));
            Assert.True(jValue.TryGetValue(out short _));
            Assert.True(jValue.TryGetValue(out int _));
            Assert.True(jValue.TryGetValue(out long _));
            Assert.True(jValue.TryGetValue(out sbyte _));
            Assert.True(jValue.TryGetValue(out ushort _));
            Assert.True(jValue.TryGetValue(out uint _));
            Assert.True(jValue.TryGetValue(out ulong _));
            Assert.True(jValue.TryGetValue(out float _));
            Assert.True(jValue.TryGetValue(out double _));
            Assert.True(jValue.TryGetValue(out decimal _));
            Assert.True(jValue.TryGetValue(out object _));
            Assert.False(jValue.TryGetValue(out bool _));
            Assert.False(jValue.TryGetValue(out string _));
            Assert.False(jValue.TryGetValue(out char _));
            Assert.False(jValue.TryGetValue(out DateTime _));
            Assert.False(jValue.TryGetValue(out DateTimeOffset _));
            Assert.False(jValue.TryGetValue(out Guid _));
        }

        [Fact]
        public static void TryGetValue_FromGuid()
        {
            YamlValue jValue = YamlNode.Parse("\"ed957609-cdfe-412f-88c1-02daca1b4f51\"").AsValue();

            Assert.True(jValue.TryGetValue(out Guid _));
            Assert.True(jValue.TryGetValue(out string _));
            Assert.False(jValue.TryGetValue(out char _));
            Assert.False(jValue.TryGetValue(out byte _));
            Assert.False(jValue.TryGetValue(out short _));
            Assert.False(jValue.TryGetValue(out int _));
            Assert.False(jValue.TryGetValue(out long _));
            Assert.False(jValue.TryGetValue(out sbyte _));
            Assert.False(jValue.TryGetValue(out ushort _));
            Assert.False(jValue.TryGetValue(out uint _));
            Assert.False(jValue.TryGetValue(out ulong _));
            Assert.False(jValue.TryGetValue(out float _));
            Assert.False(jValue.TryGetValue(out double _));
            Assert.False(jValue.TryGetValue(out decimal _));
            Assert.False(jValue.TryGetValue(out bool _));
            Assert.False(jValue.TryGetValue(out DateTime _));
            Assert.False(jValue.TryGetValue(out DateTimeOffset _));
        }

        [Theory]
        [InlineData("\"2020-07-08T00:00:00\"")]
        [InlineData("\"2019-01-30T12:01:02+01:00\"")]
        public static void TryGetValue_FromDateTime(string yaml)
        {
            YamlValue jValue = YamlNode.Parse(yaml).AsValue();

            Assert.True(jValue.TryGetValue(out DateTime _));
            Assert.True(jValue.TryGetValue(out DateTimeOffset _));
            Assert.True(jValue.TryGetValue(out string _));
            Assert.False(jValue.TryGetValue(out char _));
            Assert.False(jValue.TryGetValue(out byte _));
            Assert.False(jValue.TryGetValue(out short _));
            Assert.False(jValue.TryGetValue(out int _));
            Assert.False(jValue.TryGetValue(out long _));
            Assert.False(jValue.TryGetValue(out sbyte _));
            Assert.False(jValue.TryGetValue(out ushort _));
            Assert.False(jValue.TryGetValue(out uint _));
            Assert.False(jValue.TryGetValue(out ulong _));
            Assert.False(jValue.TryGetValue(out float _));
            Assert.False(jValue.TryGetValue(out double _));
            Assert.False(jValue.TryGetValue(out decimal _));
            Assert.False(jValue.TryGetValue(out bool _));
            Assert.False(jValue.TryGetValue(out Guid _));
        }

        [Fact]
        public static void TryGetValue_FromBoolean()
        {
            YamlValue jValue = YamlNode.Parse("true").AsValue();

            Assert.True(jValue.TryGetValue(out bool _));
            Assert.True(jValue.TryGetValue(out object _));
            Assert.False(jValue.TryGetValue(out byte _));
            Assert.False(jValue.TryGetValue(out short _));
            Assert.False(jValue.TryGetValue(out int _));
            Assert.False(jValue.TryGetValue(out long _));
            Assert.False(jValue.TryGetValue(out sbyte _));
            Assert.False(jValue.TryGetValue(out ushort _));
            Assert.False(jValue.TryGetValue(out uint _));
            Assert.False(jValue.TryGetValue(out ulong _));
            Assert.False(jValue.TryGetValue(out float _));
            Assert.False(jValue.TryGetValue(out double _));
            Assert.False(jValue.TryGetValue(out decimal _));
            Assert.False(jValue.TryGetValue(out string _));
            Assert.False(jValue.TryGetValue(out char _));
            Assert.False(jValue.TryGetValue(out DateTime _));
            Assert.False(jValue.TryGetValue(out DateTimeOffset _));
            Assert.False(jValue.TryGetValue(out Guid _));
        }

        [Theory]
        [InlineData("\"Hello World!\"")]
        [InlineData("42")]
        [InlineData("true")]
        [InlineData("false")]
        public static void GetValue_Object(string yaml)
        {
            YamlValue jValue = YamlSerializer.Deserialize<YamlValue>(yaml);

            object result = jValue.GetValue<object>();
            Assert.NotNull(result);
            Assert.IsType<YamlElement>(result);
        }

        [Fact]
        public static void TryGetValue_NullableTypes_Deserialized()
        {
            // String primitive (YamlValueOfYamlString): YamlElement? should work
            YamlValue stringValue = YamlSerializer.Deserialize<YamlValue>("\"Hello\"");
            Assert.True(stringValue.TryGetValue(out YamlElement? _));

            // Bool primitive (YamlValueOfYamlBool): YamlElement? and bool? should work
            YamlValue boolValue = YamlSerializer.Deserialize<YamlValue>("true");
            Assert.True(boolValue.TryGetValue(out YamlElement? _));
            Assert.True(boolValue.TryGetValue(out bool? _));

            // Number primitive (YamlValueOfYamlNumber): YamlElement? and numeric nullable types should work
            YamlValue numberValue = YamlSerializer.Deserialize<YamlValue>("42");
            Assert.True(numberValue.TryGetValue(out YamlElement? _));
            Assert.True(numberValue.TryGetValue(out int? _));
            Assert.True(numberValue.TryGetValue(out long? _));
            Assert.True(numberValue.TryGetValue(out double? _));
            Assert.True(numberValue.TryGetValue(out short? _));
            Assert.True(numberValue.TryGetValue(out decimal? _));
            Assert.True(numberValue.TryGetValue(out byte? _));
            Assert.True(numberValue.TryGetValue(out float? _));
            Assert.True(numberValue.TryGetValue(out uint? _));
            Assert.True(numberValue.TryGetValue(out ushort? _));
            Assert.True(numberValue.TryGetValue(out ulong? _));
            Assert.True(numberValue.TryGetValue(out sbyte? _));
        }

        [Theory]
        [InlineData("\"A\"")]
        [InlineData("\" \"")]
        public static void FromElement_Char(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                YamlValue jValue = YamlValue.Create(document.RootElement);
                Assert.Equal(yaml[1], jValue.GetValue<char>());

                bool success = jValue.TryGetValue(out char ch);
                Assert.True(success);
                Assert.Equal(yaml[1], ch);
            }
        }

        [Theory]
        [InlineData("\"A\"", "A")]
        [InlineData("\"AB\"", "AB")]
        public static void FromElement_ToElement(string yaml, string expected)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                YamlValue jValue = YamlValue.Create(document.RootElement);

                // Obtain the internal element

                YamlElement element = jValue.GetValue<YamlElement>();
                Assert.Equal(expected, element.GetString());

                bool success = jValue.TryGetValue(out element);
                Assert.True(success);
                Assert.Equal(expected, element.GetString());
            }
        }

        [Theory]
        [InlineData("\"A\"", "A")]
        [InlineData("\"AB\"", "AB")]
        [InlineData("\"A\\u0022\"", "A\"")] // ValueEquals compares unescaped values
        public static void DeserializePrimitive_ToElement_String(string yaml, string expected)
        {
            DoTest(yaml, expected);

            // Test long strings
            string padding = new string('P', 256);
            DoTest(yaml[0] + padding + yaml.Substring(1), padding + expected);

            static void DoTest(string yaml, string expected)
            {
                YamlValue value = YamlSerializer.Deserialize<YamlValue>(yaml);
                YamlElement element = value.GetValue<YamlElement>();

                AssertExtensions.TrueExpression(element.ValueEquals(expected));

                bool success = value.TryGetValue(out element);
                Assert.True(success);
                AssertExtensions.TrueExpression(element.ValueEquals(expected));
            }
        }

        [Fact]
        public static void DeserializePrimitive_ToElement_Bool()
        {
            // true
            YamlValue value = YamlSerializer.Deserialize<YamlValue>("true");

            YamlElement element = value.GetValue<YamlElement>();
            Assert.Equal(YamlValueKind.True, element.ValueKind);

            bool success = value.TryGetValue(out element);
            Assert.True(success);
            Assert.Equal(YamlValueKind.True, element.ValueKind);

            // false
            value = YamlSerializer.Deserialize<YamlValue>("false");

            element = value.GetValue<YamlElement>();
            Assert.Equal(YamlValueKind.False, element.ValueKind);

            success = value.TryGetValue(out element);
            Assert.True(success);
            Assert.Equal(YamlValueKind.False, element.ValueKind);
        }

        [Fact]
        public static void DeserializePrimitive_ToElement_Number()
        {
            YamlValue value = YamlSerializer.Deserialize<YamlValue>("42");

            YamlElement element = value.GetValue<YamlElement>();
            Assert.Equal(42, element.GetInt32());

            bool success = value.TryGetValue(out element);
            Assert.True(success);
            Assert.Equal(42, element.GetInt32());
        }

        [Theory]
        [InlineData("42")]
        [InlineData("\"AB\"")]
        [InlineData("\"\"")]
        public static void FromElement_Char_Fail(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                YamlValue jValue = YamlValue.Create(document.RootElement);
                Assert.Throws<InvalidOperationException>(() => jValue.GetValue<char>());

                bool success = jValue.TryGetValue(out char ch);
                Assert.False(success);
            }
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        public static void FromElement_WrongNodeTypeThrows(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
                Assert.Throws<InvalidOperationException>(() => YamlValue.Create(document.RootElement));
        }

        [Fact]
        public static void WriteTo_Validation()
        {
            Assert.Throws<ArgumentNullException>(() => YamlValue.Create(42).WriteTo(null));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteTo()
        {
            const string Yaml = "42";

            YamlValue jObject = YamlNode.Parse(Yaml).AsValue();
            var stream = new MemoryStream();
            var writer = new Utf8YamlWriter(stream);
            jObject.WriteTo(writer);
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(Yaml, yaml);
        }

        [Theory]
        [InlineData("\"A\"")]
        [InlineData("\"AB\"")]
        [InlineData("\"A\\u0022\"")]
        [InlineData("42")]
        [InlineData("true")]
        [InlineData("false")]
        [Trait("category", "jsonoutput")]
        public static void DeserializePrimitive_WriteTo(string yaml)
        {
            byte[] utf8Yaml = Encoding.UTF8.GetBytes(yaml);
            YamlValue value = YamlSerializer.Deserialize<YamlValue>(utf8Yaml);

            var buffer = new ArrayBufferWriter<byte>(yaml.Length);
            using Utf8YamlWriter writer = new Utf8YamlWriter(buffer);
            value.WriteTo(writer);
            writer.Flush();

            AssertExtensions.SequenceEqual(utf8Yaml, buffer.WrittenSpan);
        }

        [Fact]
        public static void DeepCloneNotTrimmable()
        {
            var student = new Student()
            {
                Id = 1,
                Name = "test"
            };
            YamlValue jValue = YamlValue.Create(student);

            YamlNode clone = jValue.DeepClone();

            YamlNodeTests.AssertDeepEqual(jValue, clone);

            string originalYaml = jValue.ToYamlString();
            string clonedYaml = clone.ToYamlString();

            Assert.Equal(originalYaml, clonedYaml);
        }

        [Theory]
        [InlineData("42")]
        [InlineData("\"AB\"")]
        [InlineData("\"\"")]
        public static void DeepCloneTrimmable(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                YamlValue yamlValue = YamlValue.Create(document.RootElement);
                YamlNode clone = yamlValue.DeepClone();

                YamlNodeTests.AssertDeepEqual(yamlValue, clone);
                string originalYaml = yamlValue.ToYamlString();
                string clonedYaml = clone.ToYamlString();

                Assert.Equal(originalYaml, clonedYaml);
            }
        }

        [Fact]
        public static void DeepEqualsComplexType()
        {
            var student = new Student()
            {
                Id = 10,
                Name = "test"
            };
            YamlValue jValue = YamlValue.Create(student);

            var jObject = new YamlObject();
            jObject.Add("Id", 10);
            jObject.Add("Name", "test");

            YamlNodeTests.AssertDeepEqual(jValue, jObject);
        }

        [Fact]
        public static void DeepEqualsPrimitiveType()
        {
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((uint)10));
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((ulong)10));
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((float)10));
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((decimal)10));
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((short)10));
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(10), YamlValue.Create((ushort)10));

            Guid guid = Guid.Empty;
            YamlNodeTests.AssertDeepEqual(YamlValue.Create(guid), YamlValue.Create(guid.ToString()));
            YamlNodeTests.AssertNotDeepEqual(YamlValue.Create(10), YamlValue.Create("10"));
        }

        [Theory]
        [InlineData("-0.0", "0")]
        [InlineData("0", "0.0000e4")]
        [InlineData("0", "0.0000e-4")]
        [InlineData("1", "1.0")]
        [InlineData("1", "1e0")]
        [InlineData("1", "1.0000")]
        [InlineData("1", "1.0000e0")]
        [InlineData("1", "0.10000e1")]
        [InlineData("1", "10.0000e-1")]
        [InlineData("10001", "1.0001e4")]
        [InlineData("10001e-3", "1.0001e1")]
        [InlineData("1", "0.1e1")]
        [InlineData("0.1", "1e-1")]
        [InlineData("0.001", "1e-3")]
        [InlineData("1e9", "1000000000")]
        [InlineData("11", "1.100000000e1")]
        [InlineData("3.141592653589793", "3141592653589793E-15")]
        [InlineData("0.000000000000000000000000000000000000000001", "1e-42")]
        [InlineData("1000000000000000000000000000000000000000000", "1e42")]
        [InlineData("-1.1e3", "-1100")]
        [InlineData("79228162514264337593543950336", "792281625142643375935439503360e-1")] // decimal.MaxValue + 1
        [InlineData("79228162514.264337593543950336", "792281625142643375935439503360e-19")]
        [InlineData("1.75e+300", "1.75E+300")] // Variations in exponent casing
        [InlineData( // > 256 digits
            "1.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001" ,

            "100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001" + "E-512")]
        public static void DeepEqualsNumericType(string leftStr, string rightStr)
        {
            YamlNode left = YamlNode.Parse(leftStr);
            YamlNode right = YamlNode.Parse(rightStr);

            YamlNodeTests.AssertDeepEqual(left, right);
        }

        [Theory]
        [InlineData("0", "1")]
        [InlineData("1", "-1")]
        [InlineData("1.1", "-1.1")]
        [InlineData("1.1e5", "-1.1e5")]
        [InlineData("0", "1e-1024")]
        [InlineData("1", "0.1")]
        [InlineData("1", "1.1")]
        [InlineData("1", "1e1")]
        [InlineData("1", "1.00001")]
        [InlineData("1", "1.0000e1")]
        [InlineData("1", "0.1000e-1")]
        [InlineData("1", "10.0000e-2")]
        [InlineData("10001", "1.0001e3")]
        [InlineData("10001e-3", "1.0001e2")]
        [InlineData("1", "0.1e2")]
        [InlineData("0.1", "1e-2")]
        [InlineData("0.001", "1e-4")]
        [InlineData("1e9", "1000000001")]
        [InlineData("11", "1.100000001e1")]
        [InlineData("0.000000000000000000000000000000000000000001", "1e-43")]
        [InlineData("1000000000000000000000000000000000000000000", "1e43")]
        [InlineData("-1.1e3", "-1100.1")]
        [InlineData("79228162514264337593543950336", "7922816251426433759354395033600e-1")] // decimal.MaxValue + 1
        [InlineData("79228162514.264337593543950336", "7922816251426433759354395033601e-19")]
        [InlineData("1.75e+300", "1.75E+301")] // Variations in exponent casing
        [InlineData("1e2147483647", "1e-2147483648")] // int.MaxValue, int.MinValue exponents
        [InlineData( // > 256 digits
            "1.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
              "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001",

            "100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
             "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000003" + "E-512")]
        public static void NotDeepEqualsNumericType(string leftStr, string rightStr)
        {
            YamlNode left = YamlNode.Parse(leftStr);
            YamlNode right = YamlNode.Parse(rightStr);

            YamlNodeTests.AssertNotDeepEqual(left, right);
        }

        [Theory]
        [InlineData(int.MinValue - 1L)]
        [InlineData(int.MaxValue + 1L)]
        [InlineData(long.MinValue)]
        [InlineData(long.MaxValue)]
        public static void DeepEquals_ExponentExceedsInt32_ThrowsArgumentOutOfRangeException(long exponent)
        {
            YamlNode node = YamlNode.Parse($"1e{exponent}");
            Assert.Throws<ArgumentOutOfRangeException>(() => YamlNode.DeepEquals(node, node));
        }

        [Fact]
        public static void DeepEqualsYamlElement()
        {
            using YamlDocument document1 = YamlDocument.Parse("10");

            YamlValue yamlValue1 = YamlValue.Create(document1.RootElement);

            YamlNodeTests.AssertDeepEqual(yamlValue1, YamlValue.Create(10));

            using YamlDocument document2 = YamlDocument.Parse("\"10\"");

            YamlValue yamlValue2 = YamlValue.Create(document2.RootElement);
            YamlNodeTests.AssertNotDeepEqual(yamlValue1, yamlValue2);
            YamlNodeTests.AssertDeepEqual(yamlValue2, YamlValue.Create("10"));
        }

        [Fact]
        public static void DeepEqualsYamlElement_Boolean()
        {
            YamlValue trueValue = YamlValue.Create(YamlElement.Parse("true"));
            YamlValue falseValue = YamlValue.Create(YamlElement.Parse("false"));

            YamlNodeTests.AssertNotDeepEqual(trueValue, falseValue);
            YamlNodeTests.AssertDeepEqual(trueValue, trueValue.DeepClone());
        }

        [Fact]
        public static void GetValueKind()
        {
            Assert.Equal(YamlValueKind.Object, YamlValue.Create(new Student()).GetValueKind());
            Assert.Equal(YamlValueKind.Array, YamlValue.Create(new Student[] { }).GetValueKind());

            using (YamlDocument document = YamlDocument.Parse("10"))
            {
                YamlValue yamlValue = YamlValue.Create(document.RootElement);
                Assert.Equal(YamlValueKind.Number, yamlValue.GetValueKind());
            }
        }

        [Theory]
        [InlineData(YamlNumberHandling.Strict, YamlValueKind.Number)]
        [InlineData(YamlNumberHandling.AllowReadingFromString, YamlValueKind.Number)]
        [InlineData(YamlNumberHandling.AllowNamedFloatingPointLiterals, YamlValueKind.Number)]
        [InlineData(YamlNumberHandling.WriteAsString, YamlValueKind.String)]
        [InlineData(YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowNamedFloatingPointLiterals, YamlValueKind.String)]
        public static void GetValueKind_NumberHandling(YamlNumberHandling numberHandling, YamlValueKind expectedKind)
        {
            YamlSerializerOptions options = new(YamlSerializerOptions.Default) { NumberHandling = numberHandling };
            YamlTypeInfo<int> typeInfo = options.GetTypeInfo<int>();
            YamlValue value = YamlValue.Create(42, typeInfo);
            Assert.Equal(expectedKind, value.GetValueKind());
        }

        [Fact]
        public static void DeepEquals_EscapedString()
        {
            YamlValue yamlValue = YamlValue.Create(YamlElement.Parse("\"It\'s alright\""));
            YamlValue escapedYamlValue = YamlValue.Create(YamlElement.Parse("\"It\\u0027s alright\""));
            YamlNodeTests.AssertDeepEqual(escapedYamlValue, yamlValue);
        }

        private class Student
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_ReturnExpectedTypeKind<T>(WrappedT<T> wrapped, YamlValueKind expectedKind)
        {
            T value = wrapped.Value;
            YamlNode node = YamlValue.Create(value);
            Assert.Equal(expectedKind, node.GetValueKind());
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_EqualThemselves<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            YamlNode node = YamlValue.Create(value);
            Assert.True(YamlNode.DeepEquals(node, node));
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_EqualClonedValue<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            YamlNode node = YamlValue.Create(value);
            YamlNode clone = node.DeepClone();

            Assert.True(YamlNode.DeepEquals(clone, clone));
            Assert.True(YamlNode.DeepEquals(node, clone));
            Assert.True(YamlNode.DeepEquals(clone, node));
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_EqualDeserializedValue<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            YamlNode node = YamlValue.Create(value);
            YamlNode clone = YamlSerializer.Deserialize<YamlNode>(node.ToYamlString());

            Assert.True(YamlNode.DeepEquals(clone, clone));
            Assert.True(YamlNode.DeepEquals(node, clone));
            Assert.True(YamlNode.DeepEquals(clone, node));
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_DeepEquals_DifferentRepresentations<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            string yaml = YamlSerializer.Serialize(value);
            YamlNode node = YamlSerializer.Deserialize<YamlNode>(yaml);
            YamlNode other = YamlSerializer.Deserialize<YamlArray>($"[{yaml}]")[0]; // YamlValueOfElement

            Assert.True(YamlNode.DeepEquals(other, other));
            Assert.True(YamlNode.DeepEquals(node, other));
            Assert.True(YamlNode.DeepEquals(other, node));
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_EqualClonedValue_DeserializedValue<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            string yaml = YamlSerializer.Serialize(value);
            YamlNode node = YamlSerializer.Deserialize<YamlNode>(yaml);
            YamlNode clone = node.DeepClone();

            Assert.True(YamlNode.DeepEquals(clone, clone));
            Assert.True(YamlNode.DeepEquals(node, clone));
            Assert.True(YamlNode.DeepEquals(clone, node));
        }

        private static readonly HashSet<Type> s_convertibleTypes =
        [
            // True/False
            typeof(bool), typeof(bool?),

            // Number
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),

            typeof(byte?), typeof(sbyte?), typeof(short?), typeof(ushort?), typeof(int?), typeof(uint?),
            typeof(long?), typeof(ulong?), typeof(float?), typeof(double?), typeof(decimal?),

            // String
            typeof(char), typeof(char?),
            typeof(string),
            typeof(DateTimeOffset), typeof(DateTimeOffset?),
            typeof(DateTime), typeof(DateTime?),
            typeof(Guid), typeof(Guid?),
        ];

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_Conversion<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;
            string yaml = YamlSerializer.Serialize(value);
            bool canGetValue = s_convertibleTypes.Contains(typeof(T));

            YamlValue yamlValue = YamlSerializer.Deserialize<YamlValue>(yaml)!;
            AssertExtensions.TrueExpression(yamlValue.TryGetValue(out T unused) == canGetValue);

            if (canGetValue)
            {
                // Assert no throw
                yamlValue.GetValue<T>();
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => yamlValue.GetValue<T>());
            }

            YamlValue yamlNode = (YamlValue)YamlSerializer.Deserialize<YamlNode>(yaml)!;
            AssertExtensions.TrueExpression(yamlNode.TryGetValue(out unused) == canGetValue);

            // Ensure the eager evaluation code path also produces the same result
            yamlNode = (YamlValue)YamlSerializer.Deserialize<YamlNode>(yaml, new YamlSerializerOptions { AllowDuplicateProperties = false })!;
            AssertExtensions.TrueExpression(yamlNode.TryGetValue(out unused) == canGetValue);
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTypes))]
        public static void PrimitiveTypes_ReadOnlySequence<T>(WrappedT<T> wrapped, YamlValueKind _)
        {
            T value = wrapped.Value;

            byte[] yamlBytes = YamlSerializer.SerializeToUtf8Bytes(value);
            ReadOnlySequence<byte> seq = BufferFactory.Create([yamlBytes.AsMemory(0, 1), yamlBytes.AsMemory(1, yamlBytes.Length - 1)]);
            Utf8YamlReader reader = new Utf8YamlReader(seq);
            YamlValue yamlValueFromSequence = YamlSerializer.Deserialize<YamlValue>(ref reader)!;

            string yamlString = YamlSerializer.Serialize(value);
            YamlValue yamlValueFromString = YamlSerializer.Deserialize<YamlValue>(yamlString)!;

            AssertExtensions.TrueExpression(YamlNode.DeepEquals(yamlValueFromString, yamlValueFromSequence));
        }

        public static IEnumerable<object[]> GetPrimitiveTypes()
        {
            yield return Wrap(false, YamlValueKind.False);
            yield return Wrap(true, YamlValueKind.True);
            yield return Wrap((bool?)false, YamlValueKind.False);
            yield return Wrap((bool?)true, YamlValueKind.True);
            yield return Wrap((byte)42, YamlValueKind.Number);
            yield return Wrap((byte?)42, YamlValueKind.Number);
            yield return Wrap((sbyte)42, YamlValueKind.Number);
            yield return Wrap((sbyte?)42, YamlValueKind.Number);
            yield return Wrap((short)42, YamlValueKind.Number);
            yield return Wrap((short?)42, YamlValueKind.Number);
            yield return Wrap((ushort)42, YamlValueKind.Number);
            yield return Wrap((ushort?)42, YamlValueKind.Number);
            yield return Wrap(42, YamlValueKind.Number);
            yield return Wrap((int?)42, YamlValueKind.Number);
            yield return Wrap((uint)42, YamlValueKind.Number);
            yield return Wrap((uint?)42, YamlValueKind.Number);
            yield return Wrap((long)42, YamlValueKind.Number);
            yield return Wrap((long?)42, YamlValueKind.Number);
            yield return Wrap((ulong)42, YamlValueKind.Number);
            yield return Wrap((ulong?)42, YamlValueKind.Number);
            yield return Wrap(42.0f, YamlValueKind.Number);
            yield return Wrap((float?)42.0f, YamlValueKind.Number);
            yield return Wrap(42.0, YamlValueKind.Number);
            yield return Wrap((double?)42.0, YamlValueKind.Number);
            yield return Wrap(42.0m, YamlValueKind.Number);
            yield return Wrap((decimal?)42.0m, YamlValueKind.Number);
            yield return Wrap('A', YamlValueKind.String);
            yield return Wrap((char?)'A', YamlValueKind.String);
            yield return Wrap("A", YamlValueKind.String);
            yield return Wrap("A\u0041", YamlValueKind.String); // \u0041 == A
            yield return Wrap("A\u0022", YamlValueKind.String); // \u0022 == "
            yield return Wrap(new byte[] { 1, 2, 3 }, YamlValueKind.String);
            yield return Wrap(new DateTimeOffset(2024, 06, 20, 10, 29, 0, TimeSpan.Zero), YamlValueKind.String);
            yield return Wrap((DateTimeOffset?)new DateTimeOffset(2024, 06, 20, 10, 29, 0, TimeSpan.Zero), YamlValueKind.String);
            yield return Wrap(new DateTime(2024, 06, 20, 10, 29, 0), YamlValueKind.String);
            yield return Wrap((DateTime?)new DateTime(2024, 06, 20, 10, 29, 0), YamlValueKind.String);
            yield return Wrap(Guid.Empty, YamlValueKind.String);
            yield return Wrap((Guid?)Guid.Empty, YamlValueKind.String);
            yield return Wrap(new Uri("http://example.com"), YamlValueKind.String);
            yield return Wrap(new Version(1, 2, 3, 4), YamlValueKind.String);
            yield return Wrap(BindingFlags.Public, YamlValueKind.Number);
            yield return Wrap((BindingFlags?)BindingFlags.Public, YamlValueKind.Number);
#if NET
            yield return Wrap(Half.MaxValue, YamlValueKind.Number);
            yield return Wrap((Int128)42, YamlValueKind.Number);
            yield return Wrap((Int128)42, YamlValueKind.Number);
            yield return Wrap((Memory<byte>)new byte[] { 1, 2, 3 }, YamlValueKind.String);
            yield return Wrap((ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }, YamlValueKind.String);
            yield return Wrap(new DateOnly(2024, 06, 20), YamlValueKind.String);
            yield return Wrap(new TimeOnly(10, 29), YamlValueKind.String);
#endif
            static object[] Wrap<T>(T value, YamlValueKind expectedKind) => [new WrappedT<T> { Value = value }, expectedKind];
        }

        public class WrappedT<T>
        {
            public T Value;

            public override string ToString() => Value?.ToString();
        }

        [Theory]
        [InlineData("\"string\"")]
        [InlineData("42.0")]
        [InlineData("true")]
        [InlineData("false")]
        public static void PrimitiveTypes_ConverterThrows(string yaml)
        {
            YamlSerializerOptions opts = new YamlSerializerOptions
            {
                Converters = { new ThrowingConverter() }
            };

            YamlValue yamlValue = YamlSerializer.Deserialize<YamlValue>(yaml, opts);

            Assert.False(yamlValue.TryGetValue(out DummyClass unused));
            Assert.Throws<InvalidOperationException>(() => yamlValue.GetValue<DummyClass>());
        }

        private sealed class ThrowingConverter : YamlConverter<DummyClass>
        {
            public override DummyClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new YamlException();

            public override void Write(Utf8YamlWriter writer, DummyClass value, YamlSerializerOptions options) => Assert.Fail();
        }

        [Theory]
        [InlineData("\"string\"")]
        [InlineData("42.0")]
        [InlineData("true")]
        [InlineData("false")]
        public static void PrimitiveTypes_ConverterReturnsNull(string yaml)
        {
            YamlSerializerOptions opts = new YamlSerializerOptions
            {
                Converters = { new NullConverter() }
            };

            YamlValue yamlValue = YamlSerializer.Deserialize<YamlValue>(yaml, opts);

            Assert.False(yamlValue.TryGetValue(out DummyClass unused));
            Assert.Throws<InvalidOperationException>(() => yamlValue.GetValue<DummyClass>());
        }

        private sealed class NullConverter : YamlConverter<DummyClass>
        {
            public override DummyClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => null;

            public override void Write(Utf8YamlWriter writer, DummyClass value, YamlSerializerOptions options) => Assert.Fail();
        }

        [Theory]
        [InlineData("\"string\"")]
        [InlineData("42.0")]
        [InlineData("true")]
        [InlineData("false")]
        public static void PrimitiveTypes_NoTypeInfo(string yaml)
        {
            YamlSerializerOptions opts = new YamlSerializerOptions
            {
                TypeInfoResolver = new ExcludeType_TypeInfoResolver(typeof(DummyClass))
            };

            YamlValue yamlValue = YamlSerializer.Deserialize<YamlValue>(yaml, opts);

            Assert.False(yamlValue.TryGetValue(out DummyClass unused));
            Assert.Throws<InvalidOperationException>(() => yamlValue.GetValue<DummyClass>());
        }

        private class ExcludeType_TypeInfoResolver(Type excludeType) : IYamlTypeInfoResolver
        {
            private static readonly DefaultYamlTypeInfoResolver _defaultResolver = new DefaultYamlTypeInfoResolver();

            public Type ExcludeType { get; } = excludeType;

            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options) =>
                type == ExcludeType ? null : _defaultResolver.GetTypeInfo(type, options);
        }

        private record DummyClass;

        [Fact]
        public static void YamlValue_CreateFromInt()
        {
            YamlValue value = YamlValue.Create(42);
            Assert.Equal(42, value.GetValue<int>());
            Assert.True(value.TryGetValue(out int result));
            Assert.Equal(42, result);
        }

        [Fact]
        public static void YamlValue_CreateFromString()
        {
            YamlValue value = YamlValue.Create("test");
            Assert.Equal("test", value.GetValue<string>());
            Assert.True(value.TryGetValue(out string result));
            Assert.Equal("test", result);
        }

        [Fact]
        public static void YamlValue_CreateFromBool()
        {
            YamlValue value = YamlValue.Create(true);
            Assert.True(value.GetValue<bool>());
            Assert.True(value.TryGetValue(out bool result));
            Assert.True(result);
        }

        [Fact]
        public static void YamlValue_CreateFromDouble()
        {
            YamlValue value = YamlValue.Create(3.14);
            Assert.Equal(3.14, value.GetValue<double>());
            Assert.True(value.TryGetValue(out double result));
            Assert.Equal(3.14, result);
        }

        [Fact]
        public static void YamlValue_CreateWithYamlTypeInfo()
        {
            YamlTypeInfo<int> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<int>();
            
            YamlValue value = YamlValue.Create(42, typeInfo);
            Assert.Equal(42, value.GetValue<int>());
        }

        [Fact]
        public static void YamlValue_CreateWithYamlTypeInfoAndOptions()
        {
            YamlTypeInfo<string> typeInfo = YamlSerializerOptions.Default.GetTypeInfo<string>();
            var nodeOptions = new YamlNodeOptions { PropertyNameCaseInsensitive = true };
            
            YamlValue value = YamlValue.Create("test", typeInfo, nodeOptions);
            Assert.Equal("test", value.GetValue<string>());
        }
    }
}
