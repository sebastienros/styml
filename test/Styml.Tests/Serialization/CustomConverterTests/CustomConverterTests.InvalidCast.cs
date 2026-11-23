// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        [Fact]
        public static void InvalidCastRefTypedPropertyFails()
        {
            var obj = new ObjectWrapperWithProperty
            {
                Object = new WrittenObject
                {
                    Int = 123,
                    String = "Hello",
                }
            };

            var yaml = YamlSerializer.Serialize(obj);

            var ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<ObjectWrapperWithProperty>(yaml));
        }

        [Fact]
        public static void InvalidCastRefTypedFieldFails()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            var obj = new ObjectWrapperWithField
            {
                Object = new WrittenObject
                {
                    Int = 123,
                    String = "Hello",
                }
            };

            var yaml = YamlSerializer.Serialize(obj);

            var ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<ObjectWrapperWithField>(yaml, options));
        }

        /// <summary>
        /// A converter that intentionally deserialize a completely unrelated typed object.
        /// </summary>
        public class InvalidCastConverter : YamlConverter<object>
        {
            public override bool CanConvert(Type typeToConvert)
                => true;

            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                YamlSerializer.Deserialize<WrittenObject>(ref reader, options);
                return new ReadObject { Double = Math.PI };
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize<WrittenObject>(writer, (WrittenObject)value, options);
            }
        }

        private class ObjectWrapperWithProperty
        {
            [YamlConverter(typeof(InvalidCastConverter))]
            public WrittenObject Object { get; set; }
        }

        private class ObjectWrapperWithField
        {
            [YamlConverter(typeof(InvalidCastConverter))]
            public WrittenObject Object { get; set; }
        }

        private class WrittenObject
        {
            public string String { get; set; }
            public int Int { get; set; }
        }

        private class ReadObject
        {
            public double Double { get; set; }
        }

        [Fact]
        public static void CastDerivedWorks()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            var obj = YamlSerializer.Deserialize<ObjectWrapperDerived>("""{"DerivedProperty":"","DerivedField":""}""", options);

            Assert.IsType<Derived>(obj.DerivedField);
            Assert.IsType<Derived>(obj.DerivedProperty);
        }

        [Fact]
        public static void CastBaseWorks()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            var obj = YamlSerializer.Deserialize<ObjectWrapperBase>("""{"BaseProperty":"","BaseField":""}""", options);

            Assert.IsType<Derived>(obj.BaseField);
            Assert.IsType<Derived>(obj.BaseProperty);
        }

        [Fact]
        public static void CastBasePropertyFails()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            var ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<ObjectWrapperDerivedWithProperty>("""{"DerivedProperty":""}""", options));
        }

        [Fact]
        public static void CastBaseFieldFails()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };
            var ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<ObjectWrapperDerivedWithField>("""{"DerivedField":""}""", options));
        }

        /// <summary>
        /// A converter that deserializes an object of an derived class.
        /// </summary>
        private class BaseConverter : YamlConverter<Base>
        {
            public override bool CanConvert(Type typeToConvert)
                => true;

            public override Base Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                reader.GetString();
                return new Derived() { String = "Hello", Double = Math.PI };
            }

            public override void Write(Utf8YamlWriter writer, Base value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// A converter that deserializes an object of an derived class.
        /// </summary>
        private class DerivedConverter : YamlConverter<Derived>
        {
            public override bool CanConvert(Type typeToConvert)
                => true;

            public override Derived Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                reader.GetString();
                return new Derived() { String = "Hello", Double = Math.PI };
            }

            public override void Write(Utf8YamlWriter writer, Derived value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// A converter that deserializes an object of the base class where the wrapper expects an derived object.
        /// </summary>
        private class InvalidBaseConverter : YamlConverter<Base>
        {
            public override bool CanConvert(Type typeToConvert)
                => true;

            public override Base Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                reader.GetString();
                return new Base() { String = "Hello" };
            }

            public override void Write(Utf8YamlWriter writer, Base value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        private class Base
        {
            public string String;
        }

        private class Derived : Base
        {
            public double Double;
        }

        private class ObjectWrapperDerived
        {
            [YamlConverter(typeof(BaseConverter))]
            public Derived DerivedProperty { get; set; }
            [YamlConverter(typeof(BaseConverter))]
#pragma warning disable 0649
            public Derived DerivedField;
#pragma warning restore
        }

        private class ObjectWrapperDerivedWithProperty
        {
            [YamlConverter(typeof(InvalidBaseConverter))]
            public Derived DerivedProperty { get; set; }
        }

        private class ObjectWrapperDerivedWithField
        {
            [YamlConverter(typeof(InvalidBaseConverter))]
#pragma warning disable 0649
            public Derived DerivedField;
#pragma warning restore
        }

        private class ObjectWrapperBase
        {
            [YamlConverter(typeof(DerivedConverter))]
            public Base BaseProperty { get; set; }
            [YamlConverter(typeof(DerivedConverter))]
#pragma warning disable 0649
            public Base BaseField;
#pragma warning restore
        }
    }
}
