// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

#nullable enable annotations

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class NullableAnnotationsTests : SerializerTests
    {
        private static readonly YamlSerializerOptions s_optionsWithIgnoredNullability = new YamlSerializerOptions { RespectNullableAnnotations = false };
        private static readonly YamlSerializerOptions s_optionsWithEnforcedNullability = new YamlSerializerOptions { RespectNullableAnnotations = true };

        protected NullableAnnotationsTests(YamlSerializerWrapper serializerUnderTest)
            : base(serializerUnderTest) { }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertyGetter))]
        public async Task WriteNullFromNotNullablePropertyGetter_EnforcedNullability_ThrowsYamlException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string propertyName)
        {
            object value = Activator.CreateInstance(type)!;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.SerializeWrapper(value, type, s_optionsWithEnforcedNullability));

            Assert.Contains(propertyName, ex.Message);
            Assert.Contains(type.Name.Split('`')[0], ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertyGetter))]
        public async Task WriteNullFromNotNullablePropertyGetter_IgnoredNullability_Succeeds([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string _)
        {
            object value = Activator.CreateInstance(type)!;
            string yaml = await Serializer.SerializeWrapper(value, type, s_optionsWithIgnoredNullability);
            Assert.NotNull(yaml);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertyGetter))]
        public async Task WriteNullFromNotNullablePropertyGetter_EnforcedNullability_DisabledFlag_Succeeds([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string propertyName)
        {
            object value = Activator.CreateInstance(type)!;
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, s_optionsWithEnforcedNullability, mutable: true);
            YamlPropertyInfo propertyInfo = typeInfo.Properties.FirstOrDefault(p => p.Name == propertyName);

            Assert.NotNull(propertyInfo);
            Assert.False(propertyInfo.IsGetNullable);

            propertyInfo.IsGetNullable = true;
            Assert.True(propertyInfo.IsGetNullable);

            string yaml = await Serializer.SerializeWrapper(value, typeInfo);
            Assert.NotNull(yaml);
        }

        public static IEnumerable<object[]> GetTypesWithNonNullablePropertyGetter()
        {
            yield return Wrap(typeof(NotNullablePropertyClass), nameof(NotNullablePropertyClass.Property));
            yield return Wrap(typeof(NotNullableFieldClass), nameof(NotNullableFieldClass.Field));
            yield return Wrap(typeof(NotNullablePropertyWithConverterClass), nameof(NotNullablePropertyWithConverterClass.PropertyWithConverter));
            yield return Wrap(typeof(NotNullPropertyClass), nameof(NotNullPropertyClass.Property));
            yield return Wrap(typeof(NotNullStructPropertyClass), nameof(NotNullStructPropertyClass.Property));
            yield return Wrap(typeof(NotNullPropertyClass<string>), nameof(NotNullPropertyClass<string>.Property));
            yield return Wrap(typeof(NotNullablePropertyParameterizedCtorClass), nameof(NotNullablePropertyParameterizedCtorClass.CtorProperty));
            yield return Wrap(typeof(NotNullablePropertiesLargeParameterizedCtorClass), nameof(NotNullablePropertiesLargeParameterizedCtorClass.CtorProperty2));
            yield return Wrap(typeof(NotNullableSpecialTypePropertiesClass), nameof(NotNullableSpecialTypePropertiesClass.YamlDocument));
            yield return Wrap(typeof(NullableObliviousConstructorParameter), nameof(NullableObliviousConstructorParameter.Property));
            yield return Wrap(typeof(NotNullGenericPropertyClass<string>), nameof(NotNullGenericPropertyClass<string>.Property));
            yield return Wrap(typeof(ClassWithNonNullableInitProperty), nameof(ClassWithNonNullableInitProperty.Property));
            yield return Wrap(typeof(ClassWithNonNullableInitProperty), nameof(ClassWithNonNullableRequiredProperty.Property));

            static object[] Wrap(Type type, string propertyName) => [type, propertyName];
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertyGetter))]
        public async Task WriteNullFromNullablePropertyGetter_EnforcedNullability_Succeeds([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string _)
        {
            object value = Activator.CreateInstance(type)!;
            string yaml = await Serializer.SerializeWrapper(value, type, s_optionsWithEnforcedNullability);
            Assert.NotNull(yaml);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertyGetter))]
        public async Task WriteNullFromNullablePropertyGetter_IgnoredNullability_Succeeds([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string _)
        {
            object value = Activator.CreateInstance(type)!;
            string yaml = await Serializer.SerializeWrapper(value, type, s_optionsWithIgnoredNullability);
            Assert.NotNull(yaml);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertyGetter))]
        public async Task WriteNullFromNullablePropertyGetter_EnforcedNullability_EnabledFlag_ThrowsYamlException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, string propertyName)
        {
            object value = Activator.CreateInstance(type)!;
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, s_optionsWithEnforcedNullability, mutable: true);
            YamlPropertyInfo propertyInfo = typeInfo.Properties.FirstOrDefault(p => p.Name == propertyName);

            Assert.NotNull(propertyInfo);
            Assert.True(propertyInfo.IsGetNullable);

            propertyInfo.IsGetNullable = false;
            Assert.False(propertyInfo.IsGetNullable);

            YamlException ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.SerializeWrapper(value, typeInfo));

            Assert.Contains(propertyName, ex.Message);
            Assert.Contains(type.Name.Split('`')[0], ex.Message);
        }

        public static IEnumerable<object[]> GetTypesWithNullablePropertyGetter()
        {
            yield return Wrap(typeof(NullablePropertyClass), nameof(NullablePropertyClass.Property));
            yield return Wrap(typeof(NullableFieldClass), nameof(NullableFieldClass.Field));
            yield return Wrap(typeof(NullStructPropertyClass), nameof(NullStructPropertyClass.Property));
            yield return Wrap(typeof(NullStructConstructorParameterClass), nameof(NullStructConstructorParameterClass.Property));
            yield return Wrap(typeof(MaybeNullPropertyClass), nameof(MaybeNullPropertyClass.Property));
            yield return Wrap(typeof(MaybeNullPropertyClass<string>), nameof(MaybeNullPropertyClass<string>.Property));
            yield return Wrap(typeof(NullableObliviousPropertyClass), nameof(NullableObliviousPropertyClass.Property));
            yield return Wrap(typeof(GenericPropertyClass<string>), nameof(GenericPropertyClass<string>.Property));
            yield return Wrap(typeof(NullableGenericPropertyClass<string>), nameof(NullableGenericPropertyClass<string>.Property));
            yield return Wrap(typeof(ClassWithNullableInitProperty), nameof(ClassWithNullableInitProperty.Property));
            yield return Wrap(typeof(ClassWithNullableInitProperty), nameof(ClassWithNullableRequiredProperty.Property));

            static object[] Wrap(Type type, string propertyName) => [type, propertyName];
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertySetter))]
        public async Task ReadNullIntoNotNullablePropertySetter_EnforcedNullability_ThrowsYamlException(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";

            YamlException ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper(yaml, type, s_optionsWithEnforcedNullability));

            Assert.Contains(propertyName, ex.Message);
            Assert.Contains(type.Name.Split('`')[0], ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertySetter))]
        public async Task ReadNullIntoNotNullablePropertySetter_IgnoredNullability_Succeeds(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";
            object? result = await Serializer.DeserializeWrapper(yaml, type, s_optionsWithIgnoredNullability);
            Assert.IsType(type, result);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertySetter))]
        public async Task ReadEmptyObjectIntoNotNullablePropertySetter_Succeeds(Type type, string _)
        {
            object result = await Serializer.DeserializeWrapper("{}", type, s_optionsWithEnforcedNullability);
            Assert.IsType(type, result);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNonNullablePropertySetter))]
        public async Task ReadNullIntoNotNullablePropertySetter_EnforcedNullability_DisabledFlag_Succeeds(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, s_optionsWithEnforcedNullability, mutable: true);
            YamlPropertyInfo propertyInfo = typeInfo.Properties.FirstOrDefault(p => p.Name == propertyName);

            Assert.NotNull(propertyInfo);
            Assert.False(propertyInfo.IsSetNullable);

            propertyInfo.IsSetNullable = true;
            Assert.True(propertyInfo.IsSetNullable);

            object? result = await Serializer.DeserializeWrapper(yaml, typeInfo);
            Assert.IsType(type, result);
        }

        public static IEnumerable<object[]> GetTypesWithNonNullablePropertySetter()
        {
            yield return Wrap(typeof(NotNullablePropertyClass), nameof(NotNullablePropertyClass.Property));
            yield return Wrap(typeof(NotNullableFieldClass), nameof(NotNullableFieldClass.Field));
            yield return Wrap(typeof(NotNullablePropertyWithConverterClass), nameof(NotNullablePropertyWithConverterClass.PropertyWithConverter));
            yield return Wrap(typeof(DisallowNullPropertyClass), nameof(DisallowNullPropertyClass.Property));
            yield return Wrap(typeof(DisallowNullStructPropertyClass), nameof(DisallowNullStructPropertyClass.Property));
            yield return Wrap(typeof(DisallowNullStructConstructorParameter), nameof(DisallowNullStructConstructorParameter.Property));
            yield return Wrap(typeof(DisallowNullPropertyClass<string>), nameof(DisallowNullPropertyClass<string>.Property));
            yield return Wrap(typeof(NotNullablePropertyParameterizedCtorClass), nameof(NotNullablePropertyParameterizedCtorClass.CtorProperty));
            yield return Wrap(typeof(NotNullablePropertiesLargeParameterizedCtorClass), nameof(NotNullablePropertiesLargeParameterizedCtorClass.CtorProperty2));
            yield return Wrap(typeof(NotNullGenericPropertyClass<string>), nameof(NotNullGenericPropertyClass<string>.Property));
            yield return Wrap(typeof(DisallowNullConstructorParameter), nameof(DisallowNullConstructorParameter.Property));
            yield return Wrap(typeof(DisallowNullConstructorParameter<string>), nameof(DisallowNullConstructorParameter<string>.Property));
            yield return Wrap(typeof(NotNullGenericConstructorParameter<string>), nameof(NotNullGenericConstructorParameter<string>.Property));
            yield return Wrap(typeof(ClassWithNonNullableInitProperty), nameof(ClassWithNonNullableInitProperty.Property));
            yield return Wrap(typeof(ClassWithNonNullableInitProperty), nameof(ClassWithNonNullableRequiredProperty.Property));

            static object[] Wrap(Type type, string propertyName) => [type, propertyName];
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertySetter))]
        public async Task ReadNullIntoNullablePropertySetter_EnforcedNullability_Succeeds(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";
            object? result = await Serializer.DeserializeWrapper(yaml, type, s_optionsWithEnforcedNullability);
            Assert.IsType(type, result);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertySetter))]
        public async Task ReadNullIntoNullablePropertySetter_IgnoredNullability_Succeeds(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";
            object? result = await Serializer.DeserializeWrapper(yaml, type, s_optionsWithIgnoredNullability);
            Assert.IsType(type, result);
        }

        [Theory]
        [MemberData(nameof(GetTypesWithNullablePropertySetter))]
        public async Task ReadNullIntoNullablePropertySetter_EnforcedNullability_EnabledFlag_ThrowsYamlException(Type type, string propertyName)
        {
            string yaml = $$"""{"{{propertyName}}":null}""";
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, s_optionsWithEnforcedNullability, mutable: true);
            YamlPropertyInfo propertyInfo = typeInfo.Properties.FirstOrDefault(p => p.Name == propertyName);

            Assert.NotNull(propertyInfo);
            Assert.True(propertyInfo.IsSetNullable);

            propertyInfo.IsSetNullable = false;
            Assert.False(propertyInfo.IsSetNullable);

            YamlException ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper(yaml, typeInfo));

            Assert.Contains(propertyName, ex.Message);
            Assert.Contains(type.Name.Split('`')[0], ex.Message);
        }

        public static IEnumerable<object[]> GetTypesWithNullablePropertySetter()
        {
            yield return Wrap(typeof(NullablePropertyClass), nameof(NullablePropertyClass.Property));
            yield return Wrap(typeof(NullableFieldClass), nameof(NullableFieldClass.Field));
            yield return Wrap(typeof(NullStructPropertyClass), nameof(NullStructPropertyClass.Property));
            yield return Wrap(typeof(NullStructConstructorParameterClass), nameof(NullStructConstructorParameterClass.Property));
            yield return Wrap(typeof(AllowNullPropertyClass), nameof(AllowNullPropertyClass.Property));
            yield return Wrap(typeof(AllowNullPropertyClass<string>), nameof(AllowNullPropertyClass<string>.Property));
            yield return Wrap(typeof(NullableObliviousPropertyClass), nameof(NullableObliviousPropertyClass.Property));
            yield return Wrap(typeof(NullableObliviousConstructorParameter), nameof(NullableObliviousConstructorParameter.Property));
            yield return Wrap(typeof(GenericPropertyClass<string>), nameof(GenericPropertyClass<string>.Property));
            yield return Wrap(typeof(NullableGenericPropertyClass<string>), nameof(NullableGenericPropertyClass<string>.Property));
            yield return Wrap(typeof(AllowNullConstructorParameter), nameof(AllowNullConstructorParameter.Property));
            yield return Wrap(typeof(AllowNullConstructorParameter<string>), nameof(AllowNullConstructorParameter<string>.Property));
            yield return Wrap(typeof(GenericConstructorParameter<string>), nameof(GenericConstructorParameter<string>.Property));
            yield return Wrap(typeof(NullableGenericConstructorParameter<string>), nameof(NullableGenericConstructorParameter<string>.Property));
            yield return Wrap(typeof(ClassWithNullableInitProperty), nameof(ClassWithNullableInitProperty.Property));
            yield return Wrap(typeof(ClassWithNullableInitProperty), nameof(ClassWithNullableRequiredProperty.Property));

            static object[] Wrap(Type type, string propertyName) => [type, propertyName];
        }

        [Fact]
        public async Task ReadNullIntoReadonlyProperty_Succeeds()
        {
            string yaml = """{"ReadonlyProperty":null}""";
            NotNullableReadonlyPropertyClass result = await Serializer.DeserializeWrapper<NotNullableReadonlyPropertyClass>(yaml, s_optionsWithEnforcedNullability);
            Assert.Null(result.ReadonlyProperty);
        }

        [Fact]
        public async Task ReadNullIntoNotNullableSpecialTypeProperties()
        {
            string yaml = """
                {
                  "YamlDocument":null,
                  "MemoryByte":null,
                  "ReadOnlyMemoryByte":null,
                  "MemoryOfT":null,
                  "ReadOnlyMemoryOfT":null
                }
                """;

            NotNullableSpecialTypePropertiesClass result = await Serializer.DeserializeWrapper<NotNullableSpecialTypePropertiesClass>(yaml, s_optionsWithEnforcedNullability);
            Assert.Equal(YamlValueKind.Null, result.YamlDocument?.RootElement.ValueKind);
            Assert.Equal(default, result.MemoryByte);
            Assert.Equal(default, result.ReadOnlyMemoryByte);
            Assert.Equal(default, result.MemoryOfT);
            Assert.Equal(default, result.ReadOnlyMemoryOfT);
        }

        [Fact]
        public async Task ReadNullIntoNotNullablePropertyWithHandleNullConverter()
        {
            string yaml = """{"PropertyWithHandleNullConverter":null}""";

            NotNullablePropertyWithHandleNullConverterClass result = await Serializer.DeserializeWrapper<NotNullablePropertyWithHandleNullConverterClass>(yaml, s_optionsWithEnforcedNullability);
            Assert.NotNull(result.PropertyWithHandleNullConverter);
        }

        [Fact]
        public async Task ReadNotNullValueIntoNotNullablePropertyWithAlwaysNullConverterThrows()
        {
            string yaml = """{"PropertyWithAlwaysNullConverter":"42"}""";

            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<NotNullablePropertyWithAlwaysNullConverterClass>(yaml, s_optionsWithEnforcedNullability));
            Assert.Contains("PropertyWithAlwaysNullConverter", ex.Message);
            Assert.Contains(nameof(NotNullablePropertyWithAlwaysNullConverterClass), ex.Message);
        }

        [Fact]
        public async Task ReadNullIntoNotNullablePropertyWithHandleNullConverterWithParameterizedCtor()
        {
            string yaml = """{"CtorPropertyWithHandleNullConverter":null}""";

            var result = await Serializer.DeserializeWrapper<NotNullablePropertyWithHandleNullConverterParameterizedCtorClass>(yaml, s_optionsWithEnforcedNullability);
            Assert.NotNull(result.CtorPropertyWithHandleNullConverter);
        }

        [Fact]
        public async Task ReadNotNullValueIntoNotNullablePropertyWithAlwaysNullConverterWithParameterizedCtorThrows()
        {
            string yaml = """{"CtorPropertyWithAlwaysNullConverter":"42"}""";

            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass>(yaml, s_optionsWithEnforcedNullability));
            Assert.Contains("CtorPropertyWithAlwaysNullConverter", ex.Message);
            Assert.Contains(nameof(NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass), ex.Message);
        }

        [Fact]
        public async Task ReadNullIntoNotNullablePropertyWithHandleNullConverterWithLargeParameterizedCtor()
        {
            string yaml = """{"LargeCtorPropertyWithHandleNullConverter":null}""";

            var result = await Serializer.DeserializeWrapper<NotNullablePropertyWithHandleNullConverterLargeParameterizedCtorClass>(yaml, s_optionsWithEnforcedNullability);
            Assert.NotNull(result.LargeCtorPropertyWithHandleNullConverter);
        }

        [Fact]
        public async Task ReadNotNullValueIntoNotNullablePropertyWithAlwaysNullConverterWithLargeParameterizedCtorThrows()
        {
            string yaml = """{"LargeCtorPropertyWithAlwaysNullConverter":"42"}""";

            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass>(yaml, s_optionsWithEnforcedNullability));
            Assert.Contains("LargeCtorPropertyWithAlwaysNullConverter", ex.Message);
            Assert.Contains(nameof(NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass), ex.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteNotNullPropertiesWithNullIgnoreConditions_Succeeds()
        {
            // YamlIgnoreCondition.WhenWritingNull/Default takes precedence over nullability enforcement.
            var value = new NotNullablePropertyWithIgnoreConditions { WhenWritingNull = null!, WhenWritingDefault = null! };
            string yaml = await Serializer.SerializeWrapper(value, s_optionsWithEnforcedNullability);
            Assert.Equal("{}", yaml);
        }

        public class NotNullablePropertyClass
        {
            public string Property { get; set; }
        }

        public class NullableObliviousPropertyClass
        {
#nullable disable annotations
            public string Property { get; set; }
#nullable restore annotations
        }

        public class NullableObliviousConstructorParameter
        {
            public string Property { get; set; }

            public NullableObliviousConstructorParameter() { }

            [YamlConstructor]
#nullable disable annotations
            public NullableObliviousConstructorParameter(string property)
#nullable restore annotations
            {
                Property = property;
            }
        }

        public class NotNullableReadonlyPropertyClass
        {
            public string ReadonlyProperty { get; }
        }

        public class NotNullableFieldClass
        {
            [YamlInclude]
            public string Field;
        }

        public class NotNullableSpecialTypePropertiesClass
        {
            // types with internal converter that handles null.
            public YamlDocument YamlDocument { get; set; }
            public Memory<byte> MemoryByte { get; set; }
            public ReadOnlyMemory<byte> ReadOnlyMemoryByte { get; set; }
            public Memory<int> MemoryOfT { get; set; }
            public ReadOnlyMemory<int> ReadOnlyMemoryOfT { get; set; }
        }

        public class NotNullablePropertyWithHandleNullConverterClass
        {
            [YamlConverter(typeof(MyHandleNullConverter))]
            public MyClass PropertyWithHandleNullConverter { get; set; }
        }

        public class NotNullablePropertyWithAlwaysNullConverterClass
        {
            [YamlConverter(typeof(MyAlwaysNullConverter))]
            public MyClass PropertyWithAlwaysNullConverter { get; set; }
        }

        public class NotNullablePropertyWithConverterClass
        {
            [YamlConverter(typeof(MyConverter))]
            public MyClass PropertyWithConverter { get; set; }
        }

        public class MyClass { }

        public class MyHandleNullConverter : MyConverter
        {
            public override bool HandleNull => true;
        }

        public class MyConverter : YamlConverter<MyClass>
        {
            public override MyClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new MyClass();
            }

            public override void Write(Utf8YamlWriter writer, MyClass value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        public class MyAlwaysNullConverter : YamlConverter<MyClass>
        {
            public override bool HandleNull => true;

            public override MyClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return null!;
            }

            public override void Write(Utf8YamlWriter writer, MyClass value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        public class NotNullablePropertyParameterizedCtorClass
        {
            public string CtorProperty { get; }

            public NotNullablePropertyParameterizedCtorClass() { }

            [YamlConstructor]
            public NotNullablePropertyParameterizedCtorClass(string ctorProperty) => CtorProperty = ctorProperty;
        }

        public class NotNullablePropertyWithHandleNullConverterParameterizedCtorClass
        {
            [YamlConverter(typeof(MyHandleNullConverter))]
            public MyClass CtorPropertyWithHandleNullConverter { get; }

            public NotNullablePropertyWithHandleNullConverterParameterizedCtorClass() { }

            [YamlConstructor]
            public NotNullablePropertyWithHandleNullConverterParameterizedCtorClass(MyClass ctorPropertyWithHandleNullConverter) => CtorPropertyWithHandleNullConverter = ctorPropertyWithHandleNullConverter;
        }

        public class NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass
        {
            [YamlConverter(typeof(MyAlwaysNullConverter))]
            public MyClass CtorPropertyWithAlwaysNullConverter { get; }

            [YamlConstructor]
            public NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass(MyClass ctorPropertyWithAlwaysNullConverter) => CtorPropertyWithAlwaysNullConverter = ctorPropertyWithAlwaysNullConverter;
        }

        public class NotNullablePropertiesLargeParameterizedCtorClass
        {
            public string CtorProperty0 { get; }
            public string CtorProperty1 { get; }
            public string CtorProperty2 { get; }
            public string CtorProperty3 { get; }
            public string CtorProperty4 { get; }

            public NotNullablePropertiesLargeParameterizedCtorClass()
            {
                CtorProperty0 = "str";
                CtorProperty1 = "str";
                // CtorProperty2 intentionally left uninitialized.
                CtorProperty3 = "str";
                CtorProperty4 = "str";
            }

            [YamlConstructor]
            public NotNullablePropertiesLargeParameterizedCtorClass(string ctorProperty0, string ctorProperty1, string ctorProperty2, string ctorProperty3, string ctorProperty4)
            {
                CtorProperty0 = ctorProperty0;
                CtorProperty1 = ctorProperty1;
                CtorProperty2 = ctorProperty2;
                CtorProperty3 = ctorProperty3;
                CtorProperty4 = ctorProperty4;
            }
        }

        public class NotNullablePropertyWithHandleNullConverterLargeParameterizedCtorClass
        {
            public string CtorProperty0 { get; }
            public string CtorProperty1 { get; }
            [YamlConverter(typeof(MyHandleNullConverter))]
            public MyClass LargeCtorPropertyWithHandleNullConverter { get; }
            public string CtorProperty3 { get; }
            public string CtorProperty4 { get; }

            [YamlConstructor]
            public NotNullablePropertyWithHandleNullConverterLargeParameterizedCtorClass(
                string ctorProperty0, string ctorProperty1, MyClass largeCtorPropertyWithHandleNullConverter, string ctorProperty3, string ctorProperty4)
            {
                CtorProperty0 = ctorProperty0;
                CtorProperty1 = ctorProperty1;
                LargeCtorPropertyWithHandleNullConverter = largeCtorPropertyWithHandleNullConverter;
                CtorProperty3 = ctorProperty3;
                CtorProperty4 = ctorProperty4;
            }
        }

        public class NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass
        {
            public string CtorProperty0 { get; }
            public string CtorProperty1 { get; }
            [YamlConverter(typeof(MyAlwaysNullConverter))]
            public MyClass LargeCtorPropertyWithAlwaysNullConverter { get; }
            public string CtorProperty3 { get; }
            public string CtorProperty4 { get; }

            [YamlConstructor]
            public NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass(
                string ctorProperty0, string ctorProperty1, MyClass largeCtorPropertyWithAlwaysNullConverter, string ctorProperty3, string ctorProperty4)
            {
                CtorProperty0 = ctorProperty0;
                CtorProperty1 = ctorProperty1;
                LargeCtorPropertyWithAlwaysNullConverter = largeCtorPropertyWithAlwaysNullConverter;
                CtorProperty3 = ctorProperty3;
                CtorProperty4 = ctorProperty4;
            }
        }

        public class NotNullPropertyClass
        {
            [NotNull]
            public string? Property { get; set; }
        }

        public class MaybeNullPropertyClass
        {
            [MaybeNull]
            public string Property { get; set; }
        }

        public class AllowNullPropertyClass
        {
            [AllowNull]
            public string Property { get; set; }
        }

        public class DisallowNullPropertyClass
        {
            [DisallowNull]
            public string? Property { get; set; }
        }

        public class AllowNullConstructorParameter
        {
            public string? Property { get; set; }

            public AllowNullConstructorParameter() { }

            [YamlConstructor]
            public AllowNullConstructorParameter([AllowNull] string property)
            {
                Property = property;
            }
        }

        public class DisallowNullConstructorParameter
        {
            public string Property { get; set; }

            public DisallowNullConstructorParameter() { }

            [YamlConstructor]
            public DisallowNullConstructorParameter([DisallowNull] string? property)
            {
                Property = property;
            }
        }

        public class NullStructPropertyClass
        {
            public int? Property { get; set; }
        }

        public class NullStructConstructorParameterClass
        {
            public int? Property { get; set; }

            public NullStructConstructorParameterClass() { }

            [YamlConstructor]
            public NullStructConstructorParameterClass(int? property)
            {
                Property = property;
            }
        }

        public class NotNullStructPropertyClass
        {
            [NotNull]
            public int? Property { get; set; }
        }

        public class DisallowNullStructPropertyClass
        {
            [DisallowNull]
            public int? Property { get; set; }
        }

        public class DisallowNullStructConstructorParameter
        {
            public int? Property { get; set; }

            public DisallowNullStructConstructorParameter() { }

            [YamlConstructor]
            public DisallowNullStructConstructorParameter([DisallowNull] int? property)
            {
                Property = property;
            }
        }

        public class NotNullPropertyClass<T>
        {
            [NotNull]
            public T? Property { get; set; }
        }

        public class MaybeNullPropertyClass<T>
        {
            [MaybeNull]
            public T Property { get; set; }
        }

        public class AllowNullPropertyClass<T>
        {
            [AllowNull]
            public T Property { get; set; }
        }

        public class DisallowNullPropertyClass<T>
        {
            [DisallowNull]
            public T? Property { get; set; }
        }

        public class AllowNullConstructorParameter<T>
        {
            public T? Property { get; set; }

            public AllowNullConstructorParameter() { }

            [YamlConstructor]
            public AllowNullConstructorParameter([AllowNull] T property)
            {
                Property = property;
            }
        }

        public class DisallowNullConstructorParameter<T>
        {
            public T Property { get; set; }

            public DisallowNullConstructorParameter() { }

            [YamlConstructor]
            public DisallowNullConstructorParameter([DisallowNull] T? property)
            {
                Property = property;
            }
        }

        public class GenericPropertyClass<T>
        {
            public T Property { get; set; }
        }

        public class NullableGenericPropertyClass<T>
        {
            public T? Property { get; set; }
        }

        public class NotNullGenericPropertyClass<T> where T : notnull
        {
            public T Property { get; set; }
        }

        public class GenericConstructorParameter<T>
        {
            public T Property { get; set; }

            public GenericConstructorParameter() { }

            [YamlConstructor]
            public GenericConstructorParameter(T property)
            {
                Property = property;
            }
        }

        public class NullableGenericConstructorParameter<T>
        {
            public T? Property { get; set; }

            public NullableGenericConstructorParameter() { }

            [YamlConstructor]
            public NullableGenericConstructorParameter(T? property)
            {
                Property = property;
            }
        }

        public class NotNullGenericConstructorParameter<T> where T : notnull
        {
            public T Property { get; set; }

            public NotNullGenericConstructorParameter() { }

            [YamlConstructor]
            public NotNullGenericConstructorParameter(T property)
            {
                Property = property;
            }
        }

        public class NotNullablePropertyWithIgnoreConditions
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public string WhenWritingNull { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string WhenWritingDefault { get; set; }
        }

        public class NullablePropertyClass
        {
            public string? Property { get; set; }
        }

        public class NullableFieldClass
        {
            [YamlInclude]
            public string? Field;
        }

        public class ClassWithNullableInitProperty
        {
            public string? Property { get; init; }
        }

        public class ClassWithNonNullableInitProperty
        {
            public string Property { get; init; }
        }

        public class ClassWithNullableRequiredProperty
        {
            public required string? Property { get; set; }
        }

        public class ClassWithNonNullableRequiredProperty
        {
            public required string Property { get; set; }
        }
    }
}
