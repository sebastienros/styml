// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public partial class MetadataTests(YamlSerializerWrapper serializerUnderTest) : SerializerTests(serializerUnderTest)
    {
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(int?))]
        [InlineData(typeof(string))]
        [InlineData(typeof(List<string>))]
        [InlineData(typeof(Dictionary<Guid, int>))]
        [InlineData(typeof(ClassWithoutCtor))]
        [InlineData(typeof(StructWithDefaultCtor?))]
        [InlineData(typeof(IInterfaceWithProperties))]
        [InlineData(typeof(IDerivedInterface))]
        public void TypeWithoutConstructor_TypeInfoReportsNullCtorProvider(Type type)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);

            Assert.Null(typeInfo.ConstructorAttributeProvider);
        }

        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        public void TypeWithConstructor_TypeInfoReportsExpectedCtorProvider([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type typeWithCtor)
        {
            ConstructorInfo? expectedCtor = typeWithCtor.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(ctor => ctor.GetCustomAttribute<YamlConstructorAttribute>() is not null)
                .FirstOrDefault();

            Assert.NotNull(expectedCtor);

            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithCtor);

            Assert.Same(expectedCtor, typeInfo.ConstructorAttributeProvider);
        }

        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        public void TypeWithConstructor_SettingCtorDelegate_ResetsCtorAttributeProvider([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type typeWithCtor)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithCtor, mutable: true);
            Assert.NotNull(typeInfo.ConstructorAttributeProvider);

            typeInfo.CreateObject = () => Activator.CreateInstance(typeWithCtor);

            Assert.Null(typeInfo.ConstructorAttributeProvider);
        }

        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(ClassWithoutCtor))]
        [InlineData(typeof(IInterfaceWithProperties))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        [InlineData(typeof(IDerivedInterface))]
        public void YamlPropertyInfo_DeclaringType_HasExpectedValue(Type typeWithProperties)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithProperties);
            Assert.NotEmpty(typeInfo.Properties);

            Assert.All(typeInfo.Properties, propertyInfo =>
            {
                Assert.True(propertyInfo.DeclaringType.IsAssignableFrom(typeInfo.Type));
            });
        }

        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(ClassWithoutCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(IInterfaceWithProperties))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        [InlineData(typeof(IDerivedInterface))]
        [RequiresUnreferencedCode("Uses reflection to resolve the member backing each YamlPropertyInfo.")]
        public void YamlPropertyInfo_AttributeProvider_HasExpectedValue(Type typeWithProperties)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithProperties);
            Assert.NotEmpty(typeInfo.Properties);

            Assert.All(typeInfo.Properties, propertyInfo =>
            {
                MemberInfo? memberInfo = ResolveMember(typeWithProperties, propertyInfo.Name);
                Assert.Same(memberInfo, propertyInfo.AttributeProvider);
            });
        }

        [Theory]
        [InlineData(typeof(ClassWithoutCtor))]
        [InlineData(typeof(IInterfaceWithProperties))]
        [InlineData(typeof(IDerivedInterface))]
        public void TypeWithoutConstructor_YamlPropertyInfo_AssociatedParameter_IsNull(Type type)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);
            Assert.NotEmpty(typeInfo.Properties);

            Assert.All(typeInfo.Properties, propertyInfo => Assert.Null(propertyInfo.AssociatedParameter));
        }


        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        public void TypeWithConstructor_YamlPropertyInfo_AssociatedParameter_MatchesCtorParams([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type typeWithCtor)
        {
            ConstructorInfo? expectedCtor = typeWithCtor.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(ctor => ctor.GetCustomAttribute<YamlConstructorAttribute>() is not null)
                .FirstOrDefault();

            Assert.NotNull(expectedCtor);

            Dictionary<string, ParameterInfo> parameters = expectedCtor.GetParameters().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithCtor);

            Assert.All(typeInfo.Properties, yamlProperty =>
            {
                YamlParameterInfo? yamlParameter = yamlProperty.AssociatedParameter;

                if (!parameters.TryGetValue(yamlProperty.Name, out ParameterInfo? parameter))
                {
                    Assert.Null(yamlParameter);
                    return;
                }

                Assert.NotNull(yamlParameter);

                Assert.Equal(typeWithCtor, yamlParameter.DeclaringType);
                Assert.Equal(parameter.Position, yamlParameter.Position);
                Assert.Equal(parameter.ParameterType, yamlParameter.ParameterType);
                Assert.Equal(parameter.Name, yamlParameter.Name);
                Assert.Equal(parameter.Name, yamlProperty.Name, StringComparer.OrdinalIgnoreCase);

                Assert.Equal(parameter.HasDefaultValue, yamlParameter.HasDefaultValue);
                Assert.Equal(GetDefaultValue(parameter), yamlParameter.DefaultValue);
                Assert.Same(parameter, yamlParameter.AttributeProvider);
                Assert.Equal(yamlProperty.IsSetNullable, yamlParameter.IsNullable);
                Assert.False(yamlParameter.IsMemberInitializer);

                parameters.Remove(yamlProperty.Name);
            });

            Assert.Empty(parameters);
        }

        [Fact]
        public void TypeWithRequiredMember_SourceGen_HasAssociatedParameterInfo()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithRequiredMember));
            YamlPropertyInfo propertyInfo = typeInfo.Properties.Single();

            YamlParameterInfo? yamlParameter = propertyInfo.AssociatedParameter;

            if (Serializer.IsSourceGeneratedSerializer)
            {
                Assert.NotNull(yamlParameter);

                Assert.Equal(typeof(ClassWithRequiredMember), yamlParameter.DeclaringType);
                Assert.Equal(0, yamlParameter.Position);
                Assert.Equal(propertyInfo.PropertyType, yamlParameter.ParameterType);
                Assert.Equal(propertyInfo.Name, yamlParameter.Name);

                Assert.False(yamlParameter.HasDefaultValue);
                Assert.Null(yamlParameter.DefaultValue);
                Assert.Null(yamlParameter.AttributeProvider);
                Assert.Equal(propertyInfo.IsSetNullable, yamlParameter.IsNullable);
                Assert.True(yamlParameter.IsMemberInitializer);
            }
            else
            {
                Assert.Null(yamlParameter);
            }
        }

        [Fact]
        public void TypeWithInitOnlyMember_SourceGen_HasNoAssociatedParameterInfo()
        {
            // Non-required init-only properties are no longer part of the constructor delegate
            // in source gen. They are set post-construction via UnsafeAccessor or reflection.
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithInitOnlyProperty));
            YamlPropertyInfo propertyInfo = typeInfo.Properties.Single();

            YamlParameterInfo? yamlParameter = propertyInfo.AssociatedParameter;
            Assert.Null(yamlParameter);
        }

        [Fact]
        public void TypeWithInitOnlyAndRequiredMembers_OnlyRequiredHasAssociatedParameterInfo()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithInitOnlyAndRequiredMembers));
            Assert.Equal(2, typeInfo.Properties.Count);

            YamlPropertyInfo initOnlyProp = typeInfo.Properties.Single(p => p.Name == nameof(ClassWithInitOnlyAndRequiredMembers.InitOnlyValue));
            YamlPropertyInfo requiredProp = typeInfo.Properties.Single(p => p.Name == nameof(ClassWithInitOnlyAndRequiredMembers.RequiredValue));

            Assert.Null(initOnlyProp.AssociatedParameter);

            if (Serializer.IsSourceGeneratedSerializer)
            {
                Assert.NotNull(requiredProp.AssociatedParameter);
                Assert.True(requiredProp.AssociatedParameter.IsMemberInitializer);
                Assert.Equal(typeof(ClassWithInitOnlyAndRequiredMembers), requiredProp.AssociatedParameter.DeclaringType);
            }
            else
            {
                Assert.Null(requiredProp.AssociatedParameter);
            }
        }

        [Theory]
        [InlineData(typeof(ClassWithDefaultCtor))]
        [InlineData(typeof(StructWithDefaultCtor))]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(ClassWithRequiredMember))]
        [InlineData(typeof(ClassWithInitOnlyProperty))]
        [InlineData(typeof(ClassWithMultipleConstructors))]
        [InlineData(typeof(DerivedClassWithShadowingProperties))]
        public void TypeWithConstructor_SettingCtorDelegate_ResetsAssociatedParameters([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type typeWithCtor)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeWithCtor, mutable: true);
            Assert.NotEmpty(typeInfo.Properties);

            typeInfo.CreateObject = () => Activator.CreateInstance(typeWithCtor);

            Assert.All(typeInfo.Properties, propertyInfo => Assert.Null(propertyInfo.AssociatedParameter));
        }

        [Theory]
        [InlineData(typeof(int), null)]
        [InlineData(typeof(int?), null)]
        [InlineData(typeof(string), null)]
        [InlineData(typeof(ClassWithDefaultCtor), null)]
        [InlineData(typeof(StructWithDefaultCtor), null)]
        [InlineData(typeof(StructWithDefaultCtor?), null)]
        [InlineData(typeof(string[]), null)]
        [InlineData(typeof(List<string>), null)]
        [InlineData(typeof(IList<string>), null)]
        [InlineData(typeof(ImmutableArray<string>), null)]
        [InlineData(typeof(DerivedList<int>), null)]
        [InlineData(typeof(DerivedListWithCustomConverter), null)]
        [InlineData(typeof(Dictionary<Guid, int>), typeof(Guid))]
        [InlineData(typeof(IReadOnlyDictionary<Guid, int>), typeof(Guid))]
        [InlineData(typeof(ImmutableDictionary<Guid, int>), typeof(Guid))]
        [InlineData(typeof(DerivedDictionary<int>), typeof(Guid))]
        [InlineData(typeof(DerivedDictionaryWithCustomConverter), null)]
        [InlineData(typeof(ArrayList), null)]
        [InlineData(typeof(Hashtable), typeof(string))]
        public void YamlTypeInfo_KeyType_ReturnsExpectedValue(Type type, Type? expectedKeyType)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);
            Assert.Equal(expectedKeyType, typeInfo.KeyType);
        }

        [Theory]
        [InlineData(typeof(int), null)]
        [InlineData(typeof(int?), typeof(int))]
        [InlineData(typeof(string), null)]
        [InlineData(typeof(ClassWithDefaultCtor), null)]
        [InlineData(typeof(StructWithDefaultCtor), null)]
        [InlineData(typeof(StructWithDefaultCtor?), typeof(StructWithDefaultCtor))]
        [InlineData(typeof(string[]), typeof(string))]
        [InlineData(typeof(List<string>), typeof(string))]
        [InlineData(typeof(IList<string>), typeof(string))]
        [InlineData(typeof(ImmutableArray<string>), typeof(string))]
        [InlineData(typeof(DerivedList<int>), typeof((int, Guid)))]
        [InlineData(typeof(DerivedListWithCustomConverter), null)]
        [InlineData(typeof(Dictionary<Guid, int>), typeof(int))]
        [InlineData(typeof(IReadOnlyDictionary<Guid, int>), typeof(int))]
        [InlineData(typeof(ImmutableDictionary<Guid, int>), typeof(int))]
        [InlineData(typeof(DerivedDictionary<int>), typeof(int))]
        [InlineData(typeof(DerivedDictionaryWithCustomConverter), null)]
        [InlineData(typeof(ArrayList), typeof(object))]
        [InlineData(typeof(Hashtable), typeof(object))]
        public void YamlTypeInfo_ElementType_ReturnsExpectedValue(Type type, Type? expectedKeyType)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);
            Assert.Equal(expectedKeyType, typeInfo.ElementType);
        }

        [Theory]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(ClassWithRequiredAndOptionalConstructorParameters))]
        public void RespectRequiredConstructorParameters_false_ReportsCorrespondingPropertiesAsNotRequired(Type type)
        {
            var options = new YamlSerializerOptions { RespectRequiredConstructorParameters = false };
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, options);

            Assert.NotEmpty(typeInfo.Properties);
            Assert.All(typeInfo.Properties, property =>
            {
                Assert.False(property.IsRequired);
            });
        }

        [Theory]
        [InlineData(typeof(ClassWithParameterizedCtor))]
        [InlineData(typeof(StructWithParameterizedCtor))]
        [InlineData(typeof(ClassWithRequiredAndOptionalConstructorParameters))]
        public void RespectRequiredConstructorParameters_true_ReportsCorrespondingPropertiesAsRequired(Type type)
        {
            var options = new YamlSerializerOptions { RespectRequiredConstructorParameters = true };
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type, options);

            Assert.NotEmpty(typeInfo.Properties);
            Assert.All(typeInfo.Properties, property =>
            {
                bool isRequiredParam = property.AssociatedParameter is { HasDefaultValue: false, IsMemberInitializer: false };
                Assert.Equal(isRequiredParam, property.IsRequired);
            });
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassWithRefStructProperty_Serialization()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // The source generator warns but otherwise skips ref struct properties.
                ClassWithRefStructProperty value = new();
                string yaml = await Serializer.SerializeWrapper(value);
                Assert.Equal("{}", yaml);

                ClassWithRefStructProperty deserialized = await Serializer.DeserializeWrapper<ClassWithRefStructProperty>("""{"Value":"abc"}""");
                Assert.True(deserialized.Value.IsEmpty);
            }
            else
            {
                // The reflection-based serializer throws.
                Assert.Throws<InvalidOperationException>(() => Serializer.GetTypeInfo<ClassWithRefStructProperty>());
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassWithRefStructConstructorParameter_Serialization()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // The source generator warns but otherwise skips constructors with ref struct parameters.
                ClassWithRefStructConstructorParameter value = new();
                string yaml = await Serializer.SerializeWrapper(value);
                Assert.Equal("""{"Value":"default"}""", yaml);

                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<ClassWithRefStructConstructorParameter>("{}"));
            }
            else
            {
                // The reflection-based serializer throws.
                Assert.Throws<InvalidOperationException>(() => Serializer.GetTypeInfo<ClassWithRefStructConstructorParameter>());
            }
        }

#if NET
        [Fact]
        public void CollectionWithRefStructElement_Serialization()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                Assert.Throws<NotSupportedException>(() => Serializer.GetTypeInfo<CollectionWithRefStructElement>());
            }
            else
            {
                // The reflection-based serializer throws.
                Assert.Throws<InvalidOperationException>(() => Serializer.GetTypeInfo<CollectionWithRefStructElement>());
            }
        }
#endif

        private static object? GetDefaultValue(ParameterInfo parameterInfo)
        {
            Type parameterType = parameterInfo.ParameterType;
            object? defaultValue = parameterInfo.DefaultValue;

            if (defaultValue is null)
            {
                return null;
            }

            // DBNull.Value is sometimes used as the default value (returned by reflection) of nullable params in place of null.
            if (defaultValue == DBNull.Value && parameterType != typeof(DBNull))
            {
                return null;
            }

            // Default values of enums or nullable enums are represented using the underlying type and need to be cast explicitly
            // cf. https://github.com/dotnet/runtime/issues/68647
            if (parameterType.IsEnum)
            {
                return Enum.ToObject(parameterType, defaultValue);
            }

            if (Nullable.GetUnderlyingType(parameterType) is Type underlyingType && underlyingType.IsEnum)
            {
                return Enum.ToObject(underlyingType, defaultValue);
            }

            return defaultValue;
        }

        [RequiresUnreferencedCode("Uses Type.GetMember/Type.GetInterfaces reflection to resolve the member backing a YamlPropertyInfo.")]
        private static MemberInfo? ResolveMember(Type type, string name)
        {
            MemberInfo? result = type.GetMember(name, BindingFlags.Instance | BindingFlags.Public).FirstOrDefault();
            if (result is null && type.IsInterface)
            {
                return type.GetInterfaces().Select(i => ResolveMember(i, name)).FirstOrDefault(m => m is not null);
            }

            return result;
        }

        internal class ClassWithoutCtor
        {
            private ClassWithoutCtor() { }

            public int Value { get; set; }
            public string Value2 { get; set; }

            [YamlInclude]
            public bool Value3 = true;
        }

        internal interface IInterfaceWithProperties
        {
            public int Value { get; set; }
            public string Value2 { get; set; }
        }

        internal struct StructWithDefaultCtor
        {
            [YamlConstructor]
            public StructWithDefaultCtor()
            {
            }

            public int Value { get; set; }
            public string Value2 { get; set; }

            [YamlInclude]
            public bool Value3 = true;
        }

        internal class ClassWithDefaultCtor
        {
            [YamlConstructor]
            public ClassWithDefaultCtor()
            {
            }

            public int Value { get; set; }
            public string Value2 { get; set; }

            [YamlInclude]
            public bool Value3 = true;
        }

        internal class ClassWithParameterizedCtor
        {
            [YamlConstructor]
            public ClassWithParameterizedCtor(
                string x1, int x2, bool x3, BindingFlags x4,
                string x5 = "str", int x6 = 42, bool x7 = true, BindingFlags? x8 = BindingFlags.Instance)
            {
                X1 = x1;
                X2 = x2;
                X3 = x3;
                X4 = x4;
                X5 = x5;
                X6 = x6;
                X7 = x7;
                X8 = x8;
            }

            public string X1 { get; }
            public int X2 { get; }
            public bool X3 { get; }
            public BindingFlags X4 { get; }
            public string X5 { get; }
            public int X6 { get; }
            public bool X7 { get; }

            [YamlInclude]
            public BindingFlags? X8;

            public string ExtraProperty { get; set; }
        }

        internal struct StructWithParameterizedCtor
        {
            [YamlConstructor]
            public StructWithParameterizedCtor(
                string x1, int x2, bool x3, BindingFlags x4,
                string x5 = "str", int x6 = 42, bool x7 = true, BindingFlags? x8 = BindingFlags.Instance)
            {
                X1 = x1;
                X2 = x2;
                X3 = x3;
                X4 = x4;
                X5 = x5;
                X6 = x6;
                X7 = x7;
                X8 = x8;
            }

            public string X1 { get; }
            public int X2 { get; }
            public bool X3 { get; }
            public BindingFlags X4 { get; }
            public string X5 { get; }
            public int X6 { get; }
            public bool X7 { get; }

            [YamlInclude]
            public BindingFlags? X8;

            public string ExtraProperty { get; set; }
        }

        internal class ClassWithRequiredMember
        {
            public required int Value { get; set; }
        }

        internal class ClassWithInitOnlyProperty
        {
            public int Value { get; init; }
        }

        internal class ClassWithInitOnlyAndRequiredMembers
        {
            public int InitOnlyValue { get; init; }
            public required string RequiredValue { get; set; }
        }

        internal class ClassWithMultipleConstructors
        {
            public ClassWithMultipleConstructors() { }

            public ClassWithMultipleConstructors(int x) { }

            [YamlConstructor]
            public ClassWithMultipleConstructors(string value)
            {
                Value = value;
            }

            public string Value { get; set; }
        }

        internal abstract class BaseClassWithProperties
        {
            public int Value1 { get; }
            public virtual int Value2 { get; set; }
            public abstract int Value3 { get; set; }
        }

        internal class DerivedClassWithShadowingProperties : BaseClassWithProperties
        {
            [YamlConstructor]
            public DerivedClassWithShadowingProperties(string value1, int value2, int value3)
            {
                Value1 = value1;
                Value2 = value2;
                Value3 = value3;
            }

            public new string Value1 { get; set; }
            public override int Value2 { get; set; }
            public override int Value3 { get; set; }
        }

        internal interface IBaseInterface1
        {
            int Value1 { get; }
        }

        internal interface IBaseInterface2
        {
            int Value2 { get; set; }
        }

        internal interface IDerivedInterface : IBaseInterface1, IBaseInterface2
        {
            new string Value2 { get; set; }
            int Value3 { get; set; }
        }

        internal class DerivedList<T> : List<(T, Guid)>;

        [YamlConverter(typeof(CustomConverter))]
        internal class DerivedListWithCustomConverter : List<Guid>
        {
            public sealed class CustomConverter : YamlConverter<DerivedListWithCustomConverter>
            {
                public override DerivedListWithCustomConverter? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
                public override void Write(Utf8YamlWriter writer, DerivedListWithCustomConverter value, YamlSerializerOptions options) => throw new NotImplementedException();
            }
        }

        internal class DerivedDictionary<T> : Dictionary<Guid, T>;

        [YamlConverter(typeof(CustomConverter))]
        internal class DerivedDictionaryWithCustomConverter : Dictionary<Guid, string>
        {
            public sealed class CustomConverter : YamlConverter<DerivedDictionaryWithCustomConverter>
            {
                public override DerivedDictionaryWithCustomConverter? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
                public override void Write(Utf8YamlWriter writer, DerivedDictionaryWithCustomConverter value, YamlSerializerOptions options) => throw new NotImplementedException();
            }
        }

        internal class ClassWithRequiredAndOptionalConstructorParameters
        {
            [YamlConstructor]
            public ClassWithRequiredAndOptionalConstructorParameters(string? x, string? y = null)
            {
                X = x;
                Y = y;
            }
            public string? X { get; }
            public string? Y { get; }
        }

        public class ClassWithRefStructProperty
        {
            public ReadOnlySpan<char> Value
            {
                get => _value.AsSpan();
                set => _value = value.ToString();
            }

            private string? _value;
        }

        public class ClassWithRefStructConstructorParameter
        {
            public ClassWithRefStructConstructorParameter()
            {
                Value = "default";
            }

            [YamlConstructor]
            public ClassWithRefStructConstructorParameter(ReadOnlySpan<char> value)
            {
                Value = value.ToString();
            }

            public string Value { get; }
        }

#if NET
        public class CollectionWithRefStructElement : IEnumerable<ReadOnlySpan<char>>
        {
            private List<string> _values = new();
            public void Add(ReadOnlySpan<char> value) => _values.Add(value.ToString());
            IEnumerator<ReadOnlySpan<char>> IEnumerable<ReadOnlySpan<char>>.GetEnumerator() => new SpanEnumerator(_values.GetEnumerator());
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

            private sealed class SpanEnumerator(IEnumerator<string> inner) : IEnumerator<ReadOnlySpan<char>>
            {
                public ReadOnlySpan<char> Current => inner.Current.AsSpan();
                object IEnumerator.Current => throw new NotSupportedException();
                public void Dispose() => inner.Dispose();
                public bool MoveNext() => inner.MoveNext();
                public void Reset() => inner.Reset();
            }
        }
#endif
    }

    internal class WeatherForecastWithPOCOs
    {
        public DateTimeOffset Date { get; set; }
        public int TemperatureCelsius { get; set; }
        public string? Summary { get; set; }
        public string? SummaryField;
        public List<DateTimeOffset>? DatesAvailable { get; set; }
        public Dictionary<string, HighLowTemps>? TemperatureRanges { get; set; }
        public string[]? SummaryWords { get; set; }
    }

    public class HighLowTemps
    {
        public int High { get; set; }
        public int Low { get; set; }
    }

    [YamlSerializable(typeof(WeatherForecastWithPOCOs))]
    internal sealed partial class YamlContext : YamlSerializerContext;
}
