// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Pass additional information to a converter through an attribute on a property.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
        private class PointConverterAttribute : YamlConverterAttribute
        {
            public PointConverterAttribute(int coordinateOffset = 0)
            {
                CoordinateOffset = coordinateOffset;
            }

            public int CoordinateOffset { get; private set; }

            /// <summary>
            /// If overridden, allows a custom attribute to create the converter in order to pass additional state.
            /// </summary>
            /// <returns>The custom converter, or null if the serializer should create the custom converter.</returns>
            public override YamlConverter CreateConverter(Type typeToConvert)
            {
                return new PointConverter(CoordinateOffset);
            }
        }

        private class ClassWithPointConverterAttribute
        {
            [PointConverter(10)]
            public Point Point1 { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeExtraInformation()
        {
            const string yaml = """{"Point1":"1,2"}""";

            ClassWithPointConverterAttribute obj = YamlSerializer.Deserialize<ClassWithPointConverterAttribute>(yaml);
            Assert.Equal(11, obj.Point1.X);
            Assert.Equal(12, obj.Point1.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj);
            Assert.Equal(yaml, yamlSerialized);
        }

        private class ClassWithYamlConverterAttribute
        {
            [YamlConverter(typeof(PointConverter))]
            public Point Point1 { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeOnProperty()
        {
            const string yaml = """{"Point1":"1,2"}""";

            ClassWithYamlConverterAttribute obj = YamlSerializer.Deserialize<ClassWithYamlConverterAttribute>(yaml);
            Assert.Equal(1, obj.Point1.X);
            Assert.Equal(2, obj.Point1.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj);
            Assert.Equal(yaml, yamlSerialized);
        }

        // A custom data type representing a point where JSON is "XValue,Yvalue".
        // A struct is used here, but could be a class.
        [YamlConverter(typeof(AttributedPointConverter))]
        public struct AttributedPoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        /// <summary>
        /// Converter for a custom data type that has additional state (coordinateOffset).
        /// </summary>
        private class AttributedPointConverter : YamlConverter<AttributedPoint>
        {
            private int _offset;

            public AttributedPointConverter() { }

            public AttributedPointConverter(int offset)
            {
                _offset = offset;
            }

            public override AttributedPoint Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.String)
                {
                    throw new YamlException();
                }

                string[] stringValues = reader.GetString().Split(',');
                if (stringValues.Length != 2)
                {
                    throw new YamlException();
                }

                AttributedPoint value = new AttributedPoint();
                if (!int.TryParse(stringValues[0], out int x) || !int.TryParse(stringValues[1], out int y))
                {
                    throw new YamlException();
                }

                value.X = x + _offset;
                value.Y = y + _offset;

                return value;
            }

            public override void Write(Utf8YamlWriter writer, AttributedPoint value, YamlSerializerOptions options)
            {
                string stringValue = $"{value.X - _offset},{value.Y - _offset}";
                writer.WriteStringValue(stringValue);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeOnType()
        {
            const string yaml = """
                "1,2"
                """;

            AttributedPoint point = YamlSerializer.Deserialize<AttributedPoint>(yaml);
            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);

            string yamlSerialized = YamlSerializer.Serialize(point);
            Assert.Equal(yaml, yamlSerialized);
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        private class AttributedPointConverterAttribute : YamlConverterAttribute
        {
            public AttributedPointConverterAttribute(int offset = 0)
            {
                Offset = offset;
            }

            public int Offset { get; private set; }

            /// <summary>
            /// If overridden, allows a custom attribute to create the converter in order to pass additional state.
            /// </summary>
            /// <returns>The custom converter, or null if the serializer should create the custom converter.</returns>
            public override YamlConverter CreateConverter(Type typeToConvert)
            {
                return new AttributedPointConverter(Offset);
            }
        }

        private class ClassWithYamlConverterAttributeOverride
        {
            [AttributedPointConverter(100)] // overrides the type attribute on AttributedPoint
            public AttributedPoint Point1 { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeOnTypeAndProperty()
        {
            const string yaml = """{"Point1":"1,2"}""";

            ClassWithYamlConverterAttributeOverride point = YamlSerializer.Deserialize<ClassWithYamlConverterAttributeOverride>(yaml);

            // The property attribute overrides the type attribute.
            Assert.Equal(101, point.Point1.X);
            Assert.Equal(102, point.Point1.Y);

            string yamlSerialized = YamlSerializer.Serialize(point);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeOnPropertyAndRuntime()
        {
            const string yaml = """{"Point1":"1,2"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new AttributedPointConverter(200));

            ClassWithYamlConverterAttributeOverride point = YamlSerializer.Deserialize<ClassWithYamlConverterAttributeOverride>(yaml);

            // The property attribute overrides the runtime.
            Assert.Equal(101, point.Point1.X);
            Assert.Equal(102, point.Point1.Y);

            string yamlSerialized = YamlSerializer.Serialize(point);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomAttributeOnTypeAndRuntime()
        {
            const string yaml = """
                "1,2"
                """;

            // Baseline
            AttributedPoint point = YamlSerializer.Deserialize<AttributedPoint>(yaml);
            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);
            Assert.Equal(yaml, YamlSerializer.Serialize(point));

            // Now use options.
            var options = new YamlSerializerOptions();
            options.Converters.Add(new AttributedPointConverter(200));

            point = YamlSerializer.Deserialize<AttributedPoint>(yaml, options);

            // The runtime overrides the type attribute.
            Assert.Equal(201, point.X);
            Assert.Equal(202, point.Y);

            string yamlSerialized = YamlSerializer.Serialize(point, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        // Tests for open generic converters on generic types

        /// <summary>
        /// A generic option type that represents an optional value.
        /// The converter type is an open generic, which will be constructed
        /// to match the type arguments of the Option type.
        /// </summary>
        [YamlConverter(typeof(OptionConverter<>))]
        public readonly struct Option<T>
        {
            public bool HasValue { get; }
            public T Value { get; }

            public Option(T value)
            {
                HasValue = true;
                Value = value;
            }

            public static implicit operator Option<T>(T value) => new(value);
        }

        /// <summary>
        /// Generic converter for the Option type. Serializes the value if present,
        /// or null if not.
        /// </summary>
        public sealed class OptionConverter<T> : YamlConverter<Option<T>>
        {
            public override bool HandleNull => true;

            public override Option<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    return default;
                }

                return new(YamlSerializer.Deserialize<T>(ref reader, options)!);
            }

            public override void Write(Utf8YamlWriter writer, Option<T> value, YamlSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNullValue();
                    return;
                }

                YamlSerializer.Serialize(writer, value.Value, options);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnGenericType_Serialize()
        {
            // Test serialization with a value
            Option<int> option = new Option<int>(42);
            string yaml = YamlSerializer.Serialize(option);
            Assert.Equal("42", yaml);

            // Test serialization without a value
            Option<int> emptyOption = default;
            yaml = YamlSerializer.Serialize(emptyOption);
            Assert.Equal("null", yaml);
        }

        [Fact]
        public static void GenericConverterAttributeOnGenericType_Deserialize()
        {
            // Test deserialization with a value
            Option<int> option = YamlSerializer.Deserialize<Option<int>>("42");
            Assert.True(option.HasValue);
            Assert.Equal(42, option.Value);

            // Test deserialization of null
            option = YamlSerializer.Deserialize<Option<int>>("null");
            Assert.False(option.HasValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnGenericType_ComplexType()
        {
            // Test with a complex type
            Option<string> option = new Option<string>("hello");
            string yaml = YamlSerializer.Serialize(option);
            Assert.Equal(@"""hello""", yaml);

            option = YamlSerializer.Deserialize<Option<string>>(yaml);
            Assert.True(option.HasValue);
            Assert.Equal("hello", option.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnGenericType_NestedInClass()
        {
            // Test Option type when used as a property
            var obj = new ClassWithOptionProperty { Name = "Test", OptionalValue = 42 };
            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal(@"{""Name"":""Test"",""OptionalValue"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<ClassWithOptionProperty>(yaml);
            Assert.Equal("Test", deserialized.Name);
            Assert.True(deserialized.OptionalValue.HasValue);
            Assert.Equal(42, deserialized.OptionalValue.Value);
        }

        private class ClassWithOptionProperty
        {
            public string Name { get; set; }
            public Option<int> OptionalValue { get; set; }
        }

        /// <summary>
        /// A generic result type that represents either a success value or an error.
        /// Tests a generic converter with two type parameters.
        /// </summary>
        [YamlConverter(typeof(ResultConverter<,>))]
        public readonly struct Result<TValue, TError>
        {
            public bool IsSuccess { get; }
            public TValue Value { get; }
            public TError Error { get; }

            private Result(TValue value, TError error, bool isSuccess)
            {
                Value = value;
                Error = error;
                IsSuccess = isSuccess;
            }

            public static Result<TValue, TError> Success(TValue value) =>
                new(value, default!, true);

            public static Result<TValue, TError> Failure(TError error) =>
                new(default!, error, false);
        }

        /// <summary>
        /// Generic converter for the Result type with two type parameters.
        /// </summary>
        public sealed class ResultConverter<TValue, TError> : YamlConverter<Result<TValue, TError>>
        {
            public override Result<TValue, TError> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                bool? isSuccess = null;
                TValue value = default!;
                TError error = default!;

                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != YamlTokenType.PropertyName)
                    {
                        throw new YamlException();
                    }

                    string propertyName = reader.GetString()!;
                    reader.Read();

                    switch (propertyName)
                    {
                        case "IsSuccess":
                            isSuccess = reader.GetBoolean();
                            break;
                        case "Value":
                            value = YamlSerializer.Deserialize<TValue>(ref reader, options)!;
                            break;
                        case "Error":
                            error = YamlSerializer.Deserialize<TError>(ref reader, options)!;
                            break;
                    }
                }

                if (isSuccess == true)
                {
                    return Result<TValue, TError>.Success(value);
                }

                return Result<TValue, TError>.Failure(error);
            }

            public override void Write(Utf8YamlWriter writer, Result<TValue, TError> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("IsSuccess", value.IsSuccess);

                if (value.IsSuccess)
                {
                    writer.WritePropertyName("Value");
                    YamlSerializer.Serialize(writer, value.Value, options);
                }
                else
                {
                    writer.WritePropertyName("Error");
                    YamlSerializer.Serialize(writer, value.Error, options);
                }

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnGenericType_TwoTypeParameters_Success()
        {
            var result = Result<int, string>.Success(42);
            string yaml = YamlSerializer.Serialize(result);
            Assert.Equal(@"{""IsSuccess"":true,""Value"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<Result<int, string>>(yaml);
            Assert.True(deserialized.IsSuccess);
            Assert.Equal(42, deserialized.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnGenericType_TwoTypeParameters_Failure()
        {
            var result = Result<int, string>.Failure("error message");
            string yaml = YamlSerializer.Serialize(result);
            Assert.Equal(@"{""IsSuccess"":false,""Error"":""error message""}", yaml);

            var deserialized = YamlSerializer.Deserialize<Result<int, string>>(yaml);
            Assert.False(deserialized.IsSuccess);
            Assert.Equal("error message", deserialized.Error);
        }

        /// <summary>
        /// Test that an open generic converter can be used on a property with [YamlConverter].
        /// </summary>
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttributeOnProperty()
        {
            var obj = new ClassWithGenericConverterOnProperty { Value = new MyGenericWrapper<int>(42) };
            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal(@"{""Value"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<ClassWithGenericConverterOnProperty>(yaml);
            Assert.Equal(42, deserialized.Value.WrappedValue);
        }

        private class ClassWithGenericConverterOnProperty
        {
            [YamlConverter(typeof(MyGenericWrapperConverter<>))]
            public MyGenericWrapper<int> Value { get; set; }
        }

        public class MyGenericWrapper<T>
        {
            public T WrappedValue { get; }

            public MyGenericWrapper(T value)
            {
                WrappedValue = value;
            }
        }

        public sealed class MyGenericWrapperConverter<T> : YamlConverter<MyGenericWrapper<T>>
        {
            public override MyGenericWrapper<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                T value = YamlSerializer.Deserialize<T>(ref reader, options)!;
                return new MyGenericWrapper<T>(value);
            }

            public override void Write(Utf8YamlWriter writer, MyGenericWrapper<T> value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value.WrappedValue, options);
            }
        }

        // Tests for type parameter arity mismatch
        [Fact]
        public static void GenericConverterAttribute_ArityMismatch_ThrowsInvalidOperationException()
        {
            // The converter has two type parameters but the type has one.
            // This throws InvalidOperationException with a contextual error message
            // because the arity doesn't match.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new TypeWithArityMismatch<int>()));
        }

        [YamlConverter(typeof(ConverterWithTwoParams<,>))]
        public class TypeWithArityMismatch<T>
        {
            public T Value { get; set; }
        }

        public sealed class ConverterWithTwoParams<T1, T2> : YamlConverter<TypeWithArityMismatch<T1>>
        {
            public override TypeWithArityMismatch<T1> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, TypeWithArityMismatch<T1> value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        // Tests for type constraint violations
        [Fact]
        public static void GenericConverterAttribute_ConstraintViolation_ThrowsArgumentException()
        {
            // The converter has a class constraint but int is a value type
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(new TypeWithConstraintViolation<int>()));
        }

        [YamlConverter(typeof(ConverterWithClassConstraint<>))]
        public class TypeWithConstraintViolation<T>
        {
            public T Value { get; set; }
        }

        public sealed class ConverterWithClassConstraint<T> : YamlConverter<TypeWithConstraintViolation<T>> where T : class
        {
            public override TypeWithConstraintViolation<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, TypeWithConstraintViolation<T> value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        // Tests for converter type that doesn't match the target type
        [Fact]
        public static void GenericConverterAttribute_ConverterTypeMismatch_ThrowsInvalidOperationException()
        {
            // The converter converts DifferentType<T> but the type is TypeWithConverterMismatch<T>
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new TypeWithConverterMismatch<int>()));
        }

        [YamlConverter(typeof(DifferentTypeConverter<>))]
        public class TypeWithConverterMismatch<T>
        {
            public T Value { get; set; }
        }

        public class DifferentType<T>
        {
            public T Value { get; set; }
        }

        public sealed class DifferentTypeConverter<T> : YamlConverter<DifferentType<T>>
        {
            public override DifferentType<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, DifferentType<T> value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        // Tests for open generic converter on a non-generic type
        [Fact]
        public static void GenericConverterAttribute_OpenGenericOnNonGenericType_ThrowsInvalidOperationException()
        {
            // The converter is an open generic but the target type is not generic,
            // so the converter type cannot be instantiated. We throw InvalidOperationException
            // with a contextual error message.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new NonGenericTypeWithOpenGenericConverter()));
        }

        [YamlConverter(typeof(OpenGenericConverterForNonGenericType<>))]
        public class NonGenericTypeWithOpenGenericConverter
        {
            public string Name { get; set; }
        }

        public sealed class OpenGenericConverterForNonGenericType<T> : YamlConverter<NonGenericTypeWithOpenGenericConverter>
        {
            public override NonGenericTypeWithOpenGenericConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, NonGenericTypeWithOpenGenericConverter value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        // Tests for swapped parameter order in converter
        [Fact]
        public static void GenericConverterAttribute_SwappedParameterOrder_ThrowsInvalidOperationException()
        {
            // The converter converts TypeWithSwappedParams<B, A> but the type is TypeWithSwappedParams<A, B>,
            // so MakeGenericType succeeds but CanConvert check fails.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new TypeWithSwappedParams<int, string>()));
        }

        [YamlConverter(typeof(SwappedParamsConverter<,>))]
        public class TypeWithSwappedParams<T1, T2>
        {
            public T1 First { get; set; }
            public T2 Second { get; set; }
        }

        public sealed class SwappedParamsConverter<A, B> : YamlConverter<TypeWithSwappedParams<B, A>>
        {
            public override TypeWithSwappedParams<B, A> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, TypeWithSwappedParams<B, A> value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        // Tests for nested containing class with type parameters
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttribute_NestedConverter_Works()
        {
            // Converter is nested in a generic container class
            var value = new TypeWithNestedConverter<int, string> { Value1 = 42, Value2 = "hello" };
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(@"{""Value1"":42,""Value2"":""hello""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithNestedConverter<int, string>>(yaml);
            Assert.Equal(42, deserialized.Value1);
            Assert.Equal("hello", deserialized.Value2);
        }

        [YamlConverter(typeof(Container<>.NestedConverter<>))]
        public class TypeWithNestedConverter<T1, T2>
        {
            public T1 Value1 { get; set; }
            public T2 Value2 { get; set; }
        }

        public class Container<T>
        {
            public sealed class NestedConverter<U> : YamlConverter<TypeWithNestedConverter<T, U>>
            {
                public override TypeWithNestedConverter<T, U> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                        throw new YamlException();

                    var result = new TypeWithNestedConverter<T, U>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == YamlTokenType.EndObject)
                            break;

                        if (reader.TokenType != YamlTokenType.PropertyName)
                            throw new YamlException();

                        string propertyName = reader.GetString()!;
                        reader.Read();

                        if (propertyName == "Value1")
                            result.Value1 = YamlSerializer.Deserialize<T>(ref reader, options)!;
                        else if (propertyName == "Value2")
                            result.Value2 = YamlSerializer.Deserialize<U>(ref reader, options)!;
                    }
                    return result;
                }

                public override void Write(Utf8YamlWriter writer, TypeWithNestedConverter<T, U> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Value1");
                    YamlSerializer.Serialize(writer, value.Value1, options);
                    writer.WritePropertyName("Value2");
                    YamlSerializer.Serialize(writer, value.Value2, options);
                    writer.WriteEndObject();
                }
            }
        }

        // Tests for type parameters with constraints that are satisfied
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttribute_ConstraintSatisfied_Works()
        {
            var value = new TypeWithSatisfiedConstraint<string> { Value = "test" };
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(@"{""Value"":""test""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithSatisfiedConstraint<string>>(yaml);
            Assert.Equal("test", deserialized.Value);
        }

        [YamlConverter(typeof(ConverterWithSatisfiedConstraint<>))]
        public class TypeWithSatisfiedConstraint<T>
        {
            public T Value { get; set; }
        }

        public sealed class ConverterWithSatisfiedConstraint<T> : YamlConverter<TypeWithSatisfiedConstraint<T>> where T : class
        {
            public override TypeWithSatisfiedConstraint<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                    throw new YamlException();

                var result = new TypeWithSatisfiedConstraint<T>();
                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                        break;

                    if (reader.TokenType != YamlTokenType.PropertyName)
                        throw new YamlException();

                    string propertyName = reader.GetString()!;
                    reader.Read();

                    if (propertyName == "Value")
                        result.Value = YamlSerializer.Deserialize<T>(ref reader, options)!;
                }
                return result;
            }

            public override void Write(Utf8YamlWriter writer, TypeWithSatisfiedConstraint<T> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Value");
                YamlSerializer.Serialize(writer, value.Value, options);
                writer.WriteEndObject();
            }
        }

        // Tests for generic within non-generic within generic: Outer<>.Middle.Inner<>
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttribute_DeeplyNestedConverter_Works()
        {
            var value = new TypeWithDeeplyNestedConverter<int, string> { Value1 = 99, Value2 = "deep" };
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(@"{""Value1"":99,""Value2"":""deep""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithDeeplyNestedConverter<int, string>>(yaml);
            Assert.Equal(99, deserialized.Value1);
            Assert.Equal("deep", deserialized.Value2);
        }

        [YamlConverter(typeof(OuterGeneric<>.MiddleNonGeneric.InnerConverter<>))]
        public class TypeWithDeeplyNestedConverter<T1, T2>
        {
            public T1 Value1 { get; set; }
            public T2 Value2 { get; set; }
        }

        public class OuterGeneric<T>
        {
            public class MiddleNonGeneric
            {
                public sealed class InnerConverter<U> : YamlConverter<TypeWithDeeplyNestedConverter<T, U>>
                {
                    public override TypeWithDeeplyNestedConverter<T, U> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                    {
                        if (reader.TokenType != YamlTokenType.StartObject)
                            throw new YamlException();

                        var result = new TypeWithDeeplyNestedConverter<T, U>();
                        while (reader.Read())
                        {
                            if (reader.TokenType == YamlTokenType.EndObject)
                                break;

                            if (reader.TokenType != YamlTokenType.PropertyName)
                                throw new YamlException();

                            string propertyName = reader.GetString()!;
                            reader.Read();

                            if (propertyName == "Value1")
                                result.Value1 = YamlSerializer.Deserialize<T>(ref reader, options)!;
                            else if (propertyName == "Value2")
                                result.Value2 = YamlSerializer.Deserialize<U>(ref reader, options)!;
                        }
                        return result;
                    }

                    public override void Write(Utf8YamlWriter writer, TypeWithDeeplyNestedConverter<T, U> value, YamlSerializerOptions options)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("Value1");
                        YamlSerializer.Serialize(writer, value.Value1, options);
                        writer.WritePropertyName("Value2");
                        YamlSerializer.Serialize(writer, value.Value2, options);
                        writer.WriteEndObject();
                    }
                }
            }
        }

        // Tests for converter nested in non-generic class
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttribute_NonGenericOuterConverter_Works()
        {
            var value = new TypeWithNonGenericOuterConverter<int> { Value = 42 };
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(@"{""Value"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithNonGenericOuterConverter<int>>(yaml);
            Assert.Equal(42, deserialized.Value);
        }

        [YamlConverter(typeof(NonGenericOuter.SingleLevelConverter<>))]
        public class TypeWithNonGenericOuterConverter<T>
        {
            public T Value { get; set; }
        }

        public class NonGenericOuter
        {
            public sealed class SingleLevelConverter<T> : YamlConverter<TypeWithNonGenericOuterConverter<T>>
            {
                public override TypeWithNonGenericOuterConverter<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                        throw new YamlException();

                    var result = new TypeWithNonGenericOuterConverter<T>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == YamlTokenType.EndObject)
                            break;

                        if (reader.TokenType != YamlTokenType.PropertyName)
                            throw new YamlException();

                        string propertyName = reader.GetString()!;
                        reader.Read();

                        if (propertyName == "Value")
                            result.Value = YamlSerializer.Deserialize<T>(ref reader, options)!;
                    }
                    return result;
                }

                public override void Write(Utf8YamlWriter writer, TypeWithNonGenericOuterConverter<T> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Value");
                    YamlSerializer.Serialize(writer, value.Value, options);
                    writer.WriteEndObject();
                }
            }
        }

        // Tests for many generic parameters with asymmetric distribution: Level1<,,>.Level2<>.Level3Converter<>
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GenericConverterAttribute_ManyParamsAsymmetricNesting_Works()
        {
            var value = new TypeWithManyParams<int, string, bool, double, long>
            {
                Value1 = 1,
                Value2 = "two",
                Value3 = true,
                Value4 = 4.0,
                Value5 = 5L
            };
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(@"{""Value1"":1,""Value2"":""two"",""Value3"":true,""Value4"":4,""Value5"":5}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithManyParams<int, string, bool, double, long>>(yaml);
            Assert.Equal(1, deserialized.Value1);
            Assert.Equal("two", deserialized.Value2);
            Assert.True(deserialized.Value3);
            Assert.Equal(4.0, deserialized.Value4);
            Assert.Equal(5L, deserialized.Value5);
        }

        [YamlConverter(typeof(Level1<,,>.Level2<>.Level3Converter<>))]
        public class TypeWithManyParams<T1, T2, T3, T4, T5>
        {
            public T1 Value1 { get; set; }
            public T2 Value2 { get; set; }
            public T3 Value3 { get; set; }
            public T4 Value4 { get; set; }
            public T5 Value5 { get; set; }
        }

        public class Level1<A, B, C>
        {
            public class Level2<D>
            {
                public sealed class Level3Converter<E> : YamlConverter<TypeWithManyParams<A, B, C, D, E>>
                {
                    public override TypeWithManyParams<A, B, C, D, E> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                    {
                        if (reader.TokenType != YamlTokenType.StartObject)
                            throw new YamlException();

                        var result = new TypeWithManyParams<A, B, C, D, E>();
                        while (reader.Read())
                        {
                            if (reader.TokenType == YamlTokenType.EndObject)
                                break;

                            if (reader.TokenType != YamlTokenType.PropertyName)
                                throw new YamlException();

                            string propertyName = reader.GetString()!;
                            reader.Read();

                            switch (propertyName)
                            {
                                case "Value1": result.Value1 = YamlSerializer.Deserialize<A>(ref reader, options)!; break;
                                case "Value2": result.Value2 = YamlSerializer.Deserialize<B>(ref reader, options)!; break;
                                case "Value3": result.Value3 = YamlSerializer.Deserialize<C>(ref reader, options)!; break;
                                case "Value4": result.Value4 = YamlSerializer.Deserialize<D>(ref reader, options)!; break;
                                case "Value5": result.Value5 = YamlSerializer.Deserialize<E>(ref reader, options)!; break;
                            }
                        }

                        return result;
                    }

                    public override void Write(Utf8YamlWriter writer, TypeWithManyParams<A, B, C, D, E> value, YamlSerializerOptions options)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("Value1");
                        YamlSerializer.Serialize(writer, value.Value1, options);
                        writer.WritePropertyName("Value2");
                        YamlSerializer.Serialize(writer, value.Value2, options);
                        writer.WritePropertyName("Value3");
                        YamlSerializer.Serialize(writer, value.Value3, options);
                        writer.WritePropertyName("Value4");
                        YamlSerializer.Serialize(writer, value.Value4, options);
                        writer.WritePropertyName("Value5");
                        YamlSerializer.Serialize(writer, value.Value5, options);
                        writer.WriteEndObject();
                    }
                }
            }
        }
    }
}
