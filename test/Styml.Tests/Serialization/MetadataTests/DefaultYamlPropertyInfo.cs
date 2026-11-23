// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class DefaultYamlPropertyInfoTests_DefaultYamlTypeInfoResolver : DefaultYamlPropertyInfoTests
    {
        protected override IYamlTypeInfoResolver Resolver { get; } = new DefaultYamlTypeInfoResolver();
    }

    public class DefaultYamlPropertyInfoTests_SerializerContextNoWrapping : DefaultYamlPropertyInfoTests
    {
        protected override IYamlTypeInfoResolver Resolver { get; } = Context.Default;
    }

    public class DefaultYamlPropertyInfoTests_SerializerContextWrapped : DefaultYamlPropertyInfoTests
    {
        protected override IYamlTypeInfoResolver Resolver { get; } = Context.Default;
    }

    public abstract partial class DefaultYamlPropertyInfoTests
    {
        protected abstract IYamlTypeInfoResolver Resolver { get; }

        private YamlSerializerOptions CreateOptionsWithModifier(Action<YamlTypeInfo> modifier)
            => new YamlSerializerOptions
            {
                TypeInfoResolver = Resolver.WithAddedModifier(modifier)
            };

        private YamlSerializerOptions CreateOptions() => new YamlSerializerOptions { TypeInfoResolver = Resolver };

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredAttributesGetDetectedAndFailDeserializationWhenValuesNotPresent()
        {
            YamlSerializerOptions options = CreateOptions();

            YamlTypeInfo? typeInfo = options.GetTypeInfo(typeof(ClassWithRequiredCustomAttributes));
            Assert.NotNull(typeInfo);

            Assert.Equal(3, typeInfo.Properties.Count);

            Assert.Equal(nameof(ClassWithRequiredCustomAttributes.NonRequired), typeInfo.Properties[0].Name);
            Assert.False(typeInfo.Properties[0].IsRequired);

            Assert.Equal(nameof(ClassWithRequiredCustomAttributes.RequiredA), typeInfo.Properties[1].Name);
            Assert.True(typeInfo.Properties[1].IsRequired);

            Assert.Equal(nameof(ClassWithRequiredCustomAttributes.RequiredB), typeInfo.Properties[2].Name);
            Assert.True(typeInfo.Properties[2].IsRequired);

            ClassWithRequiredCustomAttributes obj = new();
            string yaml = """{"NonRequired":null,"RequiredA":null,"RequiredB":null}""";
            Assert.Equal(yaml, YamlSerializer.Serialize(obj, options));

            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>(yaml, options);
            Assert.Null(deserialized.NonRequired);
            Assert.Null(deserialized.RequiredA);
            Assert.Null(deserialized.RequiredB);

            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("{}", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo"}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);


            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo", "RequiredB":"bar"}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo", "RequiredA":null}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredMemberCanBeModifiedToNonRequired()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributes))
                {
                    YamlPropertyInfo prop = ti.Properties[1];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributes.RequiredA), prop.Name);
                    Assert.True(prop.IsRequired);
                    prop.IsRequired = false;
                    Assert.False(prop.IsRequired);
                }
            });

            ClassWithRequiredCustomAttributes obj = new();
            string yaml = """{"NonRequired":null,"RequiredA":null,"RequiredB":null}""";
            Assert.Equal(yaml, YamlSerializer.Serialize(obj, options));

            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>(yaml, options);
            Assert.Null(deserialized.NonRequired);
            Assert.Null(deserialized.RequiredA);
            Assert.Null(deserialized.RequiredB);

            deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo", "RequiredB":"bar"}""", options);
            Assert.Equal("foo", deserialized.NonRequired);
            Assert.Null(deserialized.RequiredA);
            Assert.Equal("bar", deserialized.RequiredB);

            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("{}", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo"}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo", "RequiredA":null}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void NonRequiredMemberCanBeModifiedToRequired()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributes))
                {
                    YamlPropertyInfo prop = ti.Properties[0];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributes.NonRequired), prop.Name);
                    Assert.False(prop.IsRequired);
                    prop.IsRequired = true;
                    Assert.True(prop.IsRequired);
                }
            });

            ClassWithRequiredCustomAttributes obj = new();
            string yaml = """{"NonRequired":null,"RequiredA":null,"RequiredB":null}""";
            Assert.Equal(yaml, YamlSerializer.Serialize(obj, options));

            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>(yaml, options);
            Assert.Null(deserialized.NonRequired);
            Assert.Null(deserialized.RequiredA);
            Assert.Null(deserialized.RequiredB);

            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("{}", options));
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":"foo"}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"RequiredA":null}""", options));
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);

            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributes>("""{"NonRequired":null,"RequiredA":null}""", options));
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.NonRequired), exception.Message);
            Assert.DoesNotContain(nameof(ClassWithRequiredCustomAttributes.RequiredA), exception.Message);
            Assert.Contains(nameof(ClassWithRequiredCustomAttributes.RequiredB), exception.Message);
        }

        [Fact]
        public void RequiredExtensionDataPropertyThrows()
        {
            YamlSerializerOptions options = CreateOptions();
            ClassWithRequiredCustomAttributeAndDataExtensionProperty obj = new();
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>("""{"Data":{}}""", options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredExtensionDataPropertyCanBeFixedToNotBeRequiredWithResolver()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributeAndDataExtensionProperty))
                {
                    YamlPropertyInfo prop = ti.Properties[0];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributeAndDataExtensionProperty.Data), prop.Name);
                    Assert.True(prop.IsRequired);
                    prop.IsRequired = false;
                    Assert.False(prop.IsRequired);
                }
            });

            ClassWithRequiredCustomAttributeAndDataExtensionProperty obj = new()
            {
                Data = new Dictionary<string, object>()
                {
                    ["foo"] = "bar"
                }
            };

            string yaml = """{"foo":"bar"}""";
            Assert.Equal(yaml, YamlSerializer.Serialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>(obj, options));
            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("bar", ((YamlElement)deserialized.Data["foo"]).GetString());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredExtensionDataPropertyCanBeFixedToNotBeExtensionDataWithResolver()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributeAndDataExtensionProperty))
                {
                    YamlPropertyInfo prop = ti.Properties[0];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributeAndDataExtensionProperty.Data), prop.Name);
                    Assert.True(prop.IsExtensionData);
                    prop.IsExtensionData = false;
                    Assert.False(prop.IsExtensionData);
                }
            });

            ClassWithRequiredCustomAttributeAndDataExtensionProperty obj = new()
            {
                Data = new Dictionary<string, object>()
                {
                    ["foo"] = "bar"
                }
            };

            string yaml = """{"Data":{"foo":"bar"}}""";
            Assert.Equal(yaml, YamlSerializer.Serialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>(obj, options));
            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("bar", ((YamlElement)deserialized.Data["foo"]).GetString());
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndDataExtensionProperty>("{}", options));
        }

        [Fact]
        public void RequiredReadOnlyPropertyThrows()
        {
            YamlSerializerOptions options = CreateOptions();
            ClassWithRequiredCustomAttributeAndReadOnlyProperty obj = new();
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, options));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>("""{"Data":{}}""", options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredReadOnlyPropertyCanBeFixedToNotBeRequiredWithResolver()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributeAndReadOnlyProperty))
                {
                    YamlPropertyInfo prop = ti.Properties[0];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributeAndReadOnlyProperty.SomeProperty), prop.Name);
                    Assert.True(prop.IsRequired);
                    Assert.Null(prop.Set);
                    prop.IsRequired = false;
                    Assert.False(prop.IsRequired);
                }
            });

            ClassWithRequiredCustomAttributeAndReadOnlyProperty obj = new();

            string yaml = """{"SomeProperty":"SomePropertyInitialValue"}""";
            Assert.Equal(yaml, YamlSerializer.Serialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(obj, options));

            yaml = """{"SomeProperty":"SomeOtherValue"}""";
            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.Equal("SomePropertyInitialValue", deserialized.SomeProperty);

            yaml = "{}";
            deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.Equal("SomePropertyInitialValue", deserialized.SomeProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void RequiredReadOnlyPropertyCanBeFixedToBeWritableWithResolver()
        {
            YamlSerializerOptions options = CreateOptionsWithModifier(ti =>
            {
                if (ti.Type == typeof(ClassWithRequiredCustomAttributeAndReadOnlyProperty))
                {
                    YamlPropertyInfo prop = ti.Properties[0];
                    Assert.Equal(nameof(ClassWithRequiredCustomAttributeAndReadOnlyProperty.SomeProperty), prop.Name);
                    Assert.True(prop.IsRequired);
                    Assert.Null(prop.Set);
                    prop.Set = (obj, value) => ((ClassWithRequiredCustomAttributeAndReadOnlyProperty)obj).SetSomeProperty((string)value);
                    Assert.NotNull(prop.Set);
                }
            });

            ClassWithRequiredCustomAttributeAndReadOnlyProperty obj = new();

            string yaml = """{"SomeProperty":"SomePropertyInitialValue"}""";
            Assert.Equal(yaml, YamlSerializer.Serialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(obj, options));

            yaml = """{"SomeProperty":"SomeOtherValue"}""";
            var deserialized = YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.Equal("SomeOtherValue", deserialized.SomeProperty);

            yaml = "{}";
            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithRequiredCustomAttributeAndReadOnlyProperty>(yaml, options));
            Assert.Contains("SomeProperty", exception.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void Polymorphic_BaseClassWithoutRequiredDerivedClassWithRequiredProperty()
        {
            YamlSerializerOptions options = CreateOptions();

            BaseClassWithoutRequiredProperties obj = new DerivedClassWithRequiredProperty()
            {
                NonRequiredProperty = "non-required",
                RequiredProperty = "required",
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"$type":"derived","RequiredProperty":"required","NonRequiredProperty":"non-required"}""", yaml);

            var deserialized = YamlSerializer.Deserialize<BaseClassWithoutRequiredProperties>(yaml, options);
            Assert.Equal(obj.NonRequiredProperty, deserialized.NonRequiredProperty);
            Assert.Equal(((DerivedClassWithRequiredProperty)obj).RequiredProperty, ((DerivedClassWithRequiredProperty)deserialized).RequiredProperty);

            yaml = """{"$type":"derived","NonRequiredProperty":"non-required"}""";
            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BaseClassWithoutRequiredProperties>(yaml, options));
            Assert.Contains(nameof(DerivedClassWithRequiredProperty.RequiredProperty), exception.Message);
            Assert.DoesNotContain(nameof(BaseClassWithoutRequiredProperties.NonRequiredProperty), exception.Message);

            yaml = """{"NonRequiredProperty":"non-required"}""";
            deserialized = YamlSerializer.Deserialize<BaseClassWithoutRequiredProperties>(yaml, options);
            Assert.Equal(obj.NonRequiredProperty, deserialized.NonRequiredProperty);
            Assert.IsNotType<DerivedClassWithRequiredProperty>(deserialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void Polymorphic_BaseClassWithRequiredDerivedClassWithoutRequiredProperty()
        {
            YamlSerializerOptions options = CreateOptions();

            BaseClassWithRequiredProperties obj = new DerivedClassWithoutRequiredProperty()
            {
                NonRequiredProperty = "non-required",
                RequiredProperty = "required",
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"$type":"derived","NonRequiredProperty":"non-required","RequiredProperty":"required"}""", yaml);

            var deserialized = YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options);
            Assert.Equal(obj.RequiredProperty, deserialized.RequiredProperty);
            Assert.Equal(((DerivedClassWithoutRequiredProperty)obj).NonRequiredProperty, ((DerivedClassWithoutRequiredProperty)deserialized).NonRequiredProperty);

            yaml = """{"$type":"derived","NonRequiredProperty":"non-required"}""";
            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options));
            Assert.Contains(nameof(BaseClassWithRequiredProperties.RequiredProperty), exception.Message);
            Assert.DoesNotContain(nameof(DerivedClassWithoutRequiredProperty.NonRequiredProperty), exception.Message);

            yaml = """{"RequiredProperty":"required"}""";
            deserialized = YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options);
            Assert.Equal(obj.RequiredProperty, deserialized.RequiredProperty);
            Assert.IsNotType<DerivedClassWithoutRequiredProperty>(deserialized);

            yaml = "{}";
            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options));
            Assert.Contains(nameof(BaseClassWithRequiredProperties.RequiredProperty), exception.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void Polymorphic_BaseClassWithRequiredDerivedHidingRequiredPropertyWithNonRequired()
        {
            YamlSerializerOptions options = CreateOptions();

            YamlTypeInfo? typeInfo = options.GetTypeInfo(typeof(DerivedClassHidingRequiredPropertyWithNonRequired));
            Assert.NotNull(typeInfo);
            Assert.Equal(1, typeInfo.Properties.Count);

            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];
            Assert.Equal(nameof(DerivedClassHidingRequiredPropertyWithNonRequired.RequiredProperty), propertyInfo.Name);
            Assert.False(propertyInfo.IsRequired);

            typeInfo = options.GetTypeInfo(typeof(BaseClassWithRequiredProperties));
            Assert.NotNull(typeInfo);
            Assert.Equal(1, typeInfo.Properties.Count);

            propertyInfo = typeInfo.Properties[0];
            Assert.Equal(nameof(BaseClassWithRequiredProperties.RequiredProperty), propertyInfo.Name);
            Assert.True(propertyInfo.IsRequired);

            BaseClassWithRequiredProperties obj = new DerivedClassHidingRequiredPropertyWithNonRequired()
            {
                RequiredProperty = "hidden-with-non-required",
            };

            obj.RequiredProperty = "base-required";

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"$type":"derived-hiding","RequiredProperty":"hidden-with-non-required"}""", yaml);

            var deserialized = YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options);
            Assert.Null(deserialized.RequiredProperty);
            Assert.Equal(((DerivedClassHidingRequiredPropertyWithNonRequired)obj).RequiredProperty, ((DerivedClassHidingRequiredPropertyWithNonRequired)deserialized).RequiredProperty);

            yaml = """{"$type":"derived-hiding"}""";
            deserialized = YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options);
            Assert.Null(deserialized.RequiredProperty);
            Assert.Null(((DerivedClassHidingRequiredPropertyWithNonRequired)deserialized).RequiredProperty);

            yaml = """{"RequiredProperty":"base-required"}""";
            deserialized = YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options);
            Assert.Equal(obj.RequiredProperty, deserialized.RequiredProperty);
            Assert.IsNotType<DerivedClassHidingRequiredPropertyWithNonRequired>(deserialized);

            yaml = "{}";
            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BaseClassWithRequiredProperties>(yaml, options));
            Assert.Contains(nameof(BaseClassWithRequiredProperties.RequiredProperty), exception.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void StructWithRequiredPropertiesDoesWorkCorrectlyWithYamlRequiredCustomAttribute()
        {
            YamlSerializerOptions options = CreateOptions();

            StructWithRequiredProperties obj = new()
            {
                PropertyA = 123,
                PropertyB = 456,
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"PropertyA":123,"PropertyB":456}""", yaml);

            var deserialized = YamlSerializer.Deserialize<StructWithRequiredProperties>(yaml, options);
            Assert.Equal(obj.PropertyA, deserialized.PropertyA);
            Assert.Equal(obj.PropertyB, deserialized.PropertyB);

            yaml = """{"PropertyA":123}""";
            YamlException exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<StructWithRequiredProperties>(yaml, options));
            Assert.DoesNotContain(nameof(StructWithRequiredProperties.PropertyA), exception.Message);
            Assert.Contains(nameof(StructWithRequiredProperties.PropertyB), exception.Message);

            yaml = "{}";
            exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<StructWithRequiredProperties>(yaml, options));
            Assert.Contains(nameof(StructWithRequiredProperties.PropertyA), exception.Message);
            Assert.Contains(nameof(StructWithRequiredProperties.PropertyB), exception.Message);
        }

        public class ClassWithRequiredCustomAttributes
        {
            [YamlPropertyOrder(0)]
            public string? NonRequired { get; set; }

            [YamlPropertyOrder(1)]
            [YamlRequired]
            public string? RequiredA { get; set; }

            [YamlPropertyOrder(2)]
            [YamlRequired]
            public string? RequiredB { get; set; }
        }

        public class ClassWithRequiredCustomAttributeAndDataExtensionProperty
        {
            [YamlRequired]
            [YamlExtensionData]
            public Dictionary<string, object>? Data { get; set; }
        }

        public class ClassWithRequiredCustomAttributeAndReadOnlyProperty
        {
            [YamlRequired]
            public string SomeProperty { get; private set; } = "SomePropertyInitialValue";

            public void SetSomeProperty(string value)
            {
                SomeProperty = value;
            }
        }

        [YamlDerivedType(typeof(DerivedClassWithRequiredProperty), "derived")]
        public class BaseClassWithoutRequiredProperties
        {
            public string NonRequiredProperty { get; set; }
        }

        public class DerivedClassWithRequiredProperty : BaseClassWithoutRequiredProperties
        {
            [YamlRequired]
            public string RequiredProperty { get; set; }
        }

        [YamlDerivedType(typeof(DerivedClassWithoutRequiredProperty), "derived")]
        [YamlDerivedType(typeof(DerivedClassHidingRequiredPropertyWithNonRequired), "derived-hiding")]
        public class BaseClassWithRequiredProperties
        {
            [YamlRequired]
            public string RequiredProperty { get; set; }
        }

        public class DerivedClassWithoutRequiredProperty : BaseClassWithRequiredProperties
        {
            public string NonRequiredProperty { get; set; }
        }

        public class DerivedClassHidingRequiredPropertyWithNonRequired : BaseClassWithRequiredProperties
        {
            public new string RequiredProperty { get; set; }
        }

        public struct StructWithRequiredProperties
        {
            [YamlRequired]
            public int PropertyA { get; set; }

            [YamlRequired]
            public int PropertyB { get; set; }
        }

        [YamlSerializable(typeof(ClassWithRequiredCustomAttributes))]
        [YamlSerializable(typeof(ClassWithRequiredCustomAttributeAndDataExtensionProperty))]
        [YamlSerializable(typeof(ClassWithRequiredCustomAttributeAndReadOnlyProperty))]
        [YamlSerializable(typeof(BaseClassWithoutRequiredProperties))]
        [YamlSerializable(typeof(BaseClassWithRequiredProperties))]
        [YamlSerializable(typeof(StructWithRequiredProperties))]
        internal partial class Context : YamlSerializerContext
        {
        }
    }
}
