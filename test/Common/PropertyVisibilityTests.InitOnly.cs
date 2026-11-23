// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class PropertyVisibilityTests
    {
        [Theory]
        [InlineData(typeof(ClassWithInitOnlyProperty))]
        [InlineData(typeof(StructWithInitOnlyProperty))]
        [Trait("category", "jsonoutput")]
        public virtual async Task InitOnlyProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // Init-only property included by default.
            object obj = await Serializer.DeserializeWrapper("""{"MyInt":1}""", type);
            Assert.Equal(1, (int)type.GetProperty("MyInt").GetValue(obj));

            // Init-only properties can be serialized.
            Assert.Equal("""{"MyInt":1}""", await Serializer.SerializeWrapper(obj));
        }

        [Theory]
        [InlineData(typeof(ClassWithCustomNamedInitOnlyProperty))]
        [InlineData(typeof(StructWithCustomNamedInitOnlyProperty))]
        [Trait("category", "jsonoutput")]
        public virtual async Task CustomNamedInitOnlyProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/82730

            // Init-only property included by default.
            object obj = await Serializer.DeserializeWrapper("""{"CustomMyInt":1}""", type);
            Assert.Equal(1, (int)type.GetProperty("MyInt").GetValue(obj));

            // Init-only properties can be serialized.
            Assert.Equal("""{"CustomMyInt":1}""", await Serializer.SerializeWrapper(obj));
        }

        [Theory]
        [InlineData(typeof(Class_PropertyWith_PrivateInitOnlySetter))]
        [InlineData(typeof(Class_PropertyWith_InternalInitOnlySetter))]
        [InlineData(typeof(Class_PropertyWith_ProtectedInitOnlySetter))]
        [Trait("category", "jsonoutput")]
        public async Task NonPublicInitOnlySetter_Without_YamlInclude_Fails([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // Non-public init-only property setter ignored.
            object obj = await Serializer.DeserializeWrapper("""{"MyInt":1}""", type);
            Assert.Equal(0, (int)type.GetProperty("MyInt").GetValue(obj));

            // Public getter can be used for serialization.
            Assert.Equal("""{"MyInt":0}""", await Serializer.SerializeWrapper(obj, type));
        }

        [Theory]
        [InlineData(typeof(Class_PropertyWith_PrivateInitOnlySetter_WithAttribute))]
        [InlineData(typeof(Class_PropertyWith_InternalInitOnlySetter_WithAttribute))]
        [InlineData(typeof(Class_PropertyWith_ProtectedInitOnlySetter_WithAttribute))]
        [Trait("category", "jsonoutput")]
        public virtual async Task NonPublicInitOnlySetter_With_YamlInclude([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            // Non-public init-only property setter included with [YamlInclude].
            object obj = await Serializer.DeserializeWrapper("""{"MyInt":1}""", type);
            Assert.Equal(1, (int)type.GetProperty("MyInt").GetValue(obj));

            // Init-only properties can be serialized.
            Assert.Equal("""{"MyInt":1}""", await Serializer.SerializeWrapper(obj));
        }

        [Theory]
        [InlineData(typeof(Class_WithIgnoredInitOnlyProperty))]
        [InlineData(typeof(Record_WithIgnoredPropertyInCtor))]
        public async Task InitOnlySetter_With_YamlIgnoreAlways([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            object obj = await Serializer.DeserializeWrapper("""{"MyInt":42}""", type);
            Assert.Equal(0, (int)type.GetProperty("MyInt").GetValue(obj));
        }

        [Theory]
        [InlineData(typeof(Class_WithIgnoredRequiredProperty))]
        public async Task RequiredSetter_With_YamlIgnoreAlways_ThrowsInvalidOperationException(Type type)
        {
            YamlSerializerOptions options = Serializer.CreateOptions(opts => opts.RespectRequiredConstructorParameters = true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.DeserializeWrapper("""{"MyInt":42}""", type, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreOnInitOnlyProperty()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/101877

            RecordWithIgnoredNestedInitOnlyProperty.InnerRecord inner = new(42, "Baz");
            RecordWithIgnoredNestedInitOnlyProperty value = new RecordWithIgnoredNestedInitOnlyProperty(inner);
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"foo":42}""", yaml);

            RecordWithIgnoredNestedInitOnlyProperty? deserializedValue = await Serializer.DeserializeWrapper<RecordWithIgnoredNestedInitOnlyProperty>(yaml);
            Assert.Equal(value, deserializedValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NullableStructWithInitOnlyProperty()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/86483

            StructWithInitOnlyProperty? value = new StructWithInitOnlyProperty { MyInt = 42 };
            string yaml = await Serializer.SerializeWrapper(value);

            Assert.Equal("""{"MyInt":42}""", yaml);

            StructWithInitOnlyProperty? deserializedValue = await Serializer.DeserializeWrapper<StructWithInitOnlyProperty?>(yaml);
            Assert.Equal(deserializedValue, value);
        }

#if NET11_0_OR_GREATER // Styml: System.Numerics.Colors is new in .NET 11 (net10.0 test leg)
        [Fact]
        public async Task GenericColor_InitOnlyProperties_CanRoundtrip()
        {
            const string Yaml = """{"A":1,"R":0.25,"G":0.5,"B":0.75}""";
            var color = await Serializer.DeserializeWrapper<System.Numerics.Colors.Argb<float>>(Yaml);

            Assert.Equal(1f, color.A);
            Assert.Equal(0.25f, color.R);
            Assert.Equal(0.5f, color.G);
            Assert.Equal(0.75f, color.B);
            YamlTestHelper.AssertYamlEqual(Yaml, await Serializer.SerializeWrapper(color));
        }
#endif

        public class ClassWithInitOnlyProperty
        {
            public int MyInt { get; init; }
        }

        public struct StructWithInitOnlyProperty
        {
            public int MyInt { get; init; }
        }

        public class ClassWithCustomNamedInitOnlyProperty
        {
            [YamlPropertyName("CustomMyInt")]
            public int MyInt { get; init; }
        }

        public struct StructWithCustomNamedInitOnlyProperty
        {
            [YamlPropertyName("CustomMyInt")]
            public int MyInt { get; init; }
        }

        public class Class_PropertyWith_PrivateInitOnlySetter
        {
            public int MyInt { get; private init; }
        }

        public class Class_PropertyWith_InternalInitOnlySetter
        {
            public int MyInt { get; internal init; }
        }

        public class Class_PropertyWith_ProtectedInitOnlySetter
        {
            public int MyInt { get; protected init; }
        }

        public class Class_PropertyWith_PrivateInitOnlySetter_WithAttribute
        {
            [YamlInclude]
            public int MyInt { get; private init; }
        }

        public class Class_PropertyWith_InternalInitOnlySetter_WithAttribute
        {
            [YamlInclude]
            public int MyInt { get; internal init; }
        }

        public class Class_PropertyWith_ProtectedInitOnlySetter_WithAttribute
        {
            [YamlInclude]
            public int MyInt { get; protected init; }
        }

        public class Class_WithIgnoredInitOnlyProperty
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public int MyInt { get; init; }
        }
        
        public record Record_WithIgnoredPropertyInCtor(
            [property: YamlIgnore] int MyInt = 0);

        public class Class_WithIgnoredRequiredProperty
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public required int MyInt { get; set; }
        }

        public record RecordWithIgnoredNestedInitOnlyProperty(
            [property: YamlIgnore] RecordWithIgnoredNestedInitOnlyProperty.InnerRecord Other)
        {
            [YamlConstructor]
            public RecordWithIgnoredNestedInitOnlyProperty(int foo)
                : this(new InnerRecord(foo, "Baz"))
            {
            }

            [YamlPropertyName("foo")] public int Foo => Other.Foo;

            public record InnerRecord(int Foo, string Bar);
        }

        [Theory]
        [InlineData(typeof(ClassWithInitOnlyPropertyDefaults))]
        [InlineData(typeof(StructWithInitOnlyPropertyDefaults))]
        public async Task InitOnlyPropertyDefaultValues_PreservedWhenMissingFromYaml([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // When no properties are present in JSON, C# default values should be preserved.
            object obj = await Serializer.DeserializeWrapper("{}", type);
            Assert.Equal("DefaultName", (string)type.GetProperty("Name").GetValue(obj));
            Assert.Equal(42, (int)type.GetProperty("Number").GetValue(obj));
        }

        [Theory]
        [InlineData(typeof(ClassWithInitOnlyPropertyDefaults))]
        [InlineData(typeof(StructWithInitOnlyPropertyDefaults))]
        public async Task InitOnlyPropertyDefaultValues_OverriddenWhenPresentInYaml([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // When properties are present in JSON, specified values should be used.
            object obj = await Serializer.DeserializeWrapper("""{"Name":"Override","Number":99}""", type);
            Assert.Equal("Override", (string)type.GetProperty("Name").GetValue(obj));
            Assert.Equal(99, (int)type.GetProperty("Number").GetValue(obj));
        }

        [Theory]
        [InlineData(typeof(ClassWithInitOnlyPropertyDefaults))]
        [InlineData(typeof(StructWithInitOnlyPropertyDefaults))]
        public async Task InitOnlyPropertyDefaultValues_PartialOverride([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // When only some properties are present in JSON, the rest should keep defaults.
            object obj = await Serializer.DeserializeWrapper("""{"Number":99}""", type);
            Assert.Equal("DefaultName", (string)type.GetProperty("Name").GetValue(obj));
            Assert.Equal(99, (int)type.GetProperty("Number").GetValue(obj));
        }

        public class ClassWithInitOnlyPropertyDefaults
        {
            public string Name { get; init; } = "DefaultName";
            public int Number { get; init; } = 42;
        }

        public struct StructWithInitOnlyPropertyDefaults
        {
            public StructWithInitOnlyPropertyDefaults() { }
            public string Name { get; init; } = "DefaultName";
            public int Number { get; init; } = 42;
        }
    }
}
