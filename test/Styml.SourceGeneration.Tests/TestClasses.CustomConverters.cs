// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    /// <summary>
    /// Custom converter that adds\subtract 100 from MyIntProperty.
    /// </summary>
    public class CustomConverter_ClassWithCustomConverter : YamlConverter<ClassWithCustomConverter>
    {
        public override ClassWithCustomConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException("No StartObject");
            }

            ClassWithCustomConverter obj = new();

            reader.Read();
            if (reader.TokenType != YamlTokenType.PropertyName &&
                reader.GetString() != "MyInt")
            {
                throw new YamlException("Wrong property name");
            }

            reader.Read();
            obj.MyInt = reader.GetInt32() - 100;

            reader.Read();
            if (reader.TokenType != YamlTokenType.EndObject)
            {
                throw new YamlException("No EndObject");
            }

            return obj;
        }

        public override void Write(Utf8YamlWriter writer, ClassWithCustomConverter value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(ClassWithCustomConverter.MyInt), value.MyInt + 100);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Custom converter that adds\subtract 100 from MyIntProperty.
    /// </summary>
    public class CustomConverter_ClassWithCustomConverterFactory : YamlConverter<ClassWithCustomConverterFactory>
    {
        public override ClassWithCustomConverterFactory Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException("No StartObject");
            }

            ClassWithCustomConverterFactory obj = new();

            reader.Read();
            if (reader.TokenType != YamlTokenType.PropertyName &&
                reader.GetString() != "MyInt")
            {
                throw new YamlException("Wrong property name");
            }

            reader.Read();
            obj.MyInt = reader.GetInt32() - 100;

            reader.Read();
            if (reader.TokenType != YamlTokenType.EndObject)
            {
                throw new YamlException("No EndObject");
            }

            return obj;
        }

        public override void Write(Utf8YamlWriter writer, ClassWithCustomConverterFactory value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(ClassWithCustomConverterFactory.MyInt), value.MyInt + 100);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Custom converter that adds\subtract 100 from MyIntProperty.
    /// </summary>
    public class CustomConverter_StructWithCustomConverter : YamlConverter<StructWithCustomConverter>
    {
        public override StructWithCustomConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException("No StartObject");
            }

            StructWithCustomConverter obj = new();

            reader.Read();
            if (reader.TokenType != YamlTokenType.PropertyName &&
                reader.GetString() != "MyInt")
            {
                throw new YamlException("Wrong property name");
            }

            reader.Read();
            obj.MyInt = reader.GetInt32() - 100;

            reader.Read();
            if (reader.TokenType != YamlTokenType.EndObject)
            {
                throw new YamlException("No EndObject");
            }

            return obj;
        }

        public override void Write(Utf8YamlWriter writer, StructWithCustomConverter value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(StructWithCustomConverter.MyInt), value.MyInt + 100);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Custom converter that adds\subtract 100 from MyIntProperty.
    /// </summary>
    public class CustomConverter_StructWithCustomConverterFactory : YamlConverter<StructWithCustomConverterFactory>
    {
        public override StructWithCustomConverterFactory Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.StartObject)
            {
                throw new YamlException("No StartObject");
            }

            StructWithCustomConverterFactory obj = new();

            reader.Read();
            if (reader.TokenType != YamlTokenType.PropertyName &&
                reader.GetString() != "MyInt")
            {
                throw new YamlException("Wrong property name");
            }

            reader.Read();
            obj.MyInt = reader.GetInt32() - 100;

            reader.Read();
            if (reader.TokenType != YamlTokenType.EndObject)
            {
                throw new YamlException("No EndObject");
            }

            return obj;
        }

        public override void Write(Utf8YamlWriter writer, StructWithCustomConverterFactory value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(StructWithCustomConverterFactory.MyInt), value.MyInt + 100);
            writer.WriteEndObject();
        }
    }

    public class CustomConverterFactory : YamlConverterFactory
    {
        public CustomConverterFactory()
        {
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return (
                typeToConvert == typeof(StructWithCustomConverterFactory) ||
                typeToConvert == typeof(ClassWithCustomConverterFactory));
        }

        public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            if (typeToConvert == typeof(StructWithCustomConverterFactory))
            {
                return new CustomConverter_StructWithCustomConverterFactory();
            }

            if (typeToConvert == typeof(ClassWithCustomConverterFactory))
            {
                return new CustomConverter_ClassWithCustomConverterFactory();
            }

            throw new InvalidOperationException("Not expected.");
        }
    }

    [YamlConverter(typeof(CustomConverter_ClassWithCustomConverter))]
    public class ClassWithCustomConverter
    {
        public int MyInt { get; set; }
    }

    [YamlConverter(typeof(CustomConverter_StructWithCustomConverter))]
    public struct StructWithCustomConverter
    {
        public int MyInt { get; set; }
    }

    [YamlConverter(typeof(CustomConverterFactory))]
    public class ClassWithCustomConverterFactory
    {
        public int MyInt { get; set; }
    }

    [YamlConverter(typeof(CustomConverterFactory))]
    public struct StructWithCustomConverterFactory
    {
        public int MyInt { get; set; }
    }

    public class ClassWithCustomConverterProperty
    {
        [YamlConverter(typeof(NestedPocoCustomConverter))]
        public NestedPoco Property { get; set; }

        public class NestedPoco
        {
            public int Value { get; set; }
        }

        public class NestedPocoCustomConverter : YamlConverter<NestedPoco>
        {
            public override NestedPoco? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => new NestedPoco { Value = reader.GetInt32() };
            public override void Write(Utf8YamlWriter writer, NestedPoco value, YamlSerializerOptions options) => writer.WriteNumberValue(value.Value);
        }
    }

    public struct StructWithCustomConverterProperty
    {
        [YamlConverter(typeof(ClassWithCustomConverterProperty.NestedPocoCustomConverter))]
        public ClassWithCustomConverterProperty.NestedPoco Property { get; set; }
    }

    public class ClassWithCustomConverterFactoryProperty
    {
        [YamlConverter(typeof(YamlStringEnumConverter<SourceGenSampleEnum>))] // This converter is a YamlConverterFactory
        public SourceGenSampleEnum MyEnum { get; set; }
    }

    public struct StructWithCustomConverterFactoryProperty
    {
        [YamlConverter(typeof(YamlStringEnumConverter<SourceGenSampleEnum>))] // This converter is a YamlConverterFactory
        public SourceGenSampleEnum MyEnum { get; set; }
    }

    public class ClassWithCustomConverterFactoryNullableProperty
    {
        [YamlConverter(typeof(YamlStringEnumConverter<SourceGenSampleEnum>))] // This converter is a YamlConverterFactory
        public SourceGenSampleEnum? MyEnum { get; set; }
    }

    public class ClassWithCustomConverterNullableProperty
    {
        [YamlConverter(typeof(TimeSpanSecondsConverter))]
        public TimeSpan? TimeSpan { get; set; }
    }

    public class TimeSpanSecondsConverter : YamlConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return TimeSpan.FromSeconds(reader.GetDouble());
        }

        public override void Write(Utf8YamlWriter writer, TimeSpan value, YamlSerializerOptions options)
        {
            writer.WriteNumberValue(value.TotalSeconds);
        }
    }

    [YamlConverter(typeof(CustomConverter_StructWithCustomConverter))] // Invalid
    public class ClassWithBadCustomConverter
    {
        public int MyInt { get; set; }
    }

    [YamlConverter(typeof(CustomConverter_StructWithCustomConverter))] // Invalid
    public struct StructWithBadCustomConverter
    {
        public int MyInt { get; set; }
    }

    public enum SourceGenSampleEnum
    {
        MinZero = 0,
        One = 1,
        Two = 2
    }

    // Generic converter types for testing open generic converter support

    /// <summary>
    /// A generic option type that represents an optional value.
    /// Uses an open generic converter type.
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
    /// Generic converter for the Option type.
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

            return new(YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!);
        }

        public override void Write(Utf8YamlWriter writer, Option<T> value, YamlSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            YamlSerializer.Serialize(writer, value.Value, options.GetTypeInfo<T>());
        }
    }

    /// <summary>
    /// A class that contains an Option property for testing.
    /// </summary>
    public class ClassWithOptionProperty
    {
        public string Name { get; set; }
        public Option<int> OptionalValue { get; set; }
    }

    /// <summary>
    /// A wrapper type that uses an open generic converter on a property.
    /// </summary>
    public class GenericWrapper<T>
    {
        public T WrappedValue { get; }

        public GenericWrapper(T value)
        {
            WrappedValue = value;
        }
    }

    /// <summary>
    /// Generic converter for the GenericWrapper type.
    /// </summary>
    public sealed class GenericWrapperConverter<T> : YamlConverter<GenericWrapper<T>>
    {
        public override GenericWrapper<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            T value = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!;
            return new GenericWrapper<T>(value);
        }

        public override void Write(Utf8YamlWriter writer, GenericWrapper<T> value, YamlSerializerOptions options)
        {
            YamlSerializer.Serialize(writer, value.WrappedValue, options.GetTypeInfo<T>());
        }
    }

    /// <summary>
    /// A class with a property that uses an open generic converter attribute.
    /// </summary>
    public class ClassWithGenericConverterOnProperty
    {
        [YamlConverter(typeof(GenericWrapperConverter<>))]
        public GenericWrapper<int> Value { get; set; }
    }

    // Tests for nested containing class with type parameters
    // The converter is nested in a generic container class.
    [YamlConverter(typeof(NestedConverterContainer<>.NestedConverter<>))]
    public class TypeWithNestedConverter<T1, T2>
    {
        public T1 Value1 { get; set; }
        public T2 Value2 { get; set; }
    }

    public class NestedConverterContainer<T>
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
                        result.Value1 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!;
                    else if (propertyName == "Value2")
                        result.Value2 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<U>())!;
                }

                return result;
            }

            public override void Write(Utf8YamlWriter writer, TypeWithNestedConverter<T, U> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Value1");
                YamlSerializer.Serialize(writer, value.Value1, options.GetTypeInfo<T>());
                writer.WritePropertyName("Value2");
                YamlSerializer.Serialize(writer, value.Value2, options.GetTypeInfo<U>());
                writer.WriteEndObject();
            }
        }
    }

    // Tests for type parameters with constraints that are satisfied
    [YamlConverter(typeof(ConverterWithClassConstraint<>))]
    public class TypeWithSatisfiedConstraint<T>
    {
        public T Value { get; set; }
    }

    public sealed class ConverterWithClassConstraint<T> : YamlConverter<TypeWithSatisfiedConstraint<T>> where T : class
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
                    result.Value = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!;
            }

            return result;
        }

        public override void Write(Utf8YamlWriter writer, TypeWithSatisfiedConstraint<T> value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Value");
            YamlSerializer.Serialize(writer, value.Value, options.GetTypeInfo<T>());
            writer.WriteEndObject();
        }
    }

    // Tests for generic within non-generic within generic: Outer<>.Middle.Inner<>
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
                            result.Value1 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!;
                        else if (propertyName == "Value2")
                            result.Value2 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<U>())!;
                    }

                    return result;
                }

                public override void Write(Utf8YamlWriter writer, TypeWithDeeplyNestedConverter<T, U> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Value1");
                    YamlSerializer.Serialize(writer, value.Value1, options.GetTypeInfo<T>());
                    writer.WritePropertyName("Value2");
                    YamlSerializer.Serialize(writer, value.Value2, options.GetTypeInfo<U>());
                    writer.WriteEndObject();
                }
            }
        }
    }

    // Tests for many generic parameters with asymmetric distribution across nesting levels: Level1<,,>.Level2<>.Level3<>
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
                            case "Value1": result.Value1 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<A>())!; break;
                            case "Value2": result.Value2 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<B>())!; break;
                            case "Value3": result.Value3 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<C>())!; break;
                            case "Value4": result.Value4 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<D>())!; break;
                            case "Value5": result.Value5 = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<E>())!; break;
                        }
                    }

                    return result;
                }

                public override void Write(Utf8YamlWriter writer, TypeWithManyParams<A, B, C, D, E> value, YamlSerializerOptions options)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Value1");
                    YamlSerializer.Serialize(writer, value.Value1, options.GetTypeInfo<A>());
                    writer.WritePropertyName("Value2");
                    YamlSerializer.Serialize(writer, value.Value2, options.GetTypeInfo<B>());
                    writer.WritePropertyName("Value3");
                    YamlSerializer.Serialize(writer, value.Value3, options.GetTypeInfo<C>());
                    writer.WritePropertyName("Value4");
                    YamlSerializer.Serialize(writer, value.Value4, options.GetTypeInfo<D>());
                    writer.WritePropertyName("Value5");
                    YamlSerializer.Serialize(writer, value.Value5, options.GetTypeInfo<E>());
                    writer.WriteEndObject();
                }
            }
        }
    }

    // Tests for a single generic type parameter in a nested converter (non-generic containing generic)
    [YamlConverter(typeof(NonGenericOuter.SingleLevelGenericConverter<>))]
    public class TypeWithSingleLevelNestedConverter<T>
    {
        public T Value { get; set; }
    }

    public class NonGenericOuter
    {
        public sealed class SingleLevelGenericConverter<T> : YamlConverter<TypeWithSingleLevelNestedConverter<T>>
        {
            public override TypeWithSingleLevelNestedConverter<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                    throw new YamlException();

                var result = new TypeWithSingleLevelNestedConverter<T>();
                while (reader.Read())
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                        break;

                    if (reader.TokenType != YamlTokenType.PropertyName)
                        throw new YamlException();

                    string propertyName = reader.GetString()!;
                    reader.Read();

                    if (propertyName == "Value")
                        result.Value = YamlSerializer.Deserialize(ref reader, options.GetTypeInfo<T>())!;
                }

                return result;
            }

            public override void Write(Utf8YamlWriter writer, TypeWithSingleLevelNestedConverter<T> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Value");
                YamlSerializer.Serialize(writer, value.Value, options.GetTypeInfo<T>());
                writer.WriteEndObject();
            }
        }
    }
}
