// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class PropertyVisibilityTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NonPublic_AccessorsNotSupported_WithoutAttribute()
        {
            string yaml = """
                    {
                                    "MyInt":1,
                                    "MyString":"Hello",
                                    "MyFloat":2,
                                    "MyUri":"https://microsoft.com"
                                }
                """;

            var obj = await Serializer.DeserializeWrapper<MyClass_WithNonPublicAccessors>(yaml);
            Assert.Equal(0, obj.MyInt);
            Assert.Null(obj.MyString);
            Assert.Equal(2f, obj.GetMyFloat);
            Assert.Equal(new Uri("https://microsoft.com"), obj.MyUri);

            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "MyInt":0
                """, yaml);
            Assert.Contains("""
                "MyString":null
                """, yaml);
            Assert.DoesNotContain("""
                "MyFloat":
                """, yaml);
            Assert.DoesNotContain("""
                "MyUri":
                """, yaml);
        }

        public class MyClass_WithNonPublicAccessors
        {
            public int MyInt { get; private set; }
            public string? MyString { get; internal set; }
            public float MyFloat { private get; set; }
            public Uri MyUri { internal get; set; }

            // For test validation.
            internal float GetMyFloat => MyFloat;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task Honor_YamlSerializablePropertyAttribute_OnProperties()
        {
            string yaml = """
                    {
                                    "MyInt":1,
                                    "MyString":"Hello",
                                    "MyFloat":2,
                                    "MyUri":"https://microsoft.com"
                                }
                """;

            var obj = await Serializer.DeserializeWrapper<MyClass_WithNonPublicAccessors_WithPropertyAttributes>(yaml);
            Assert.Equal(1, obj.MyInt);
            Assert.Equal("Hello", obj.MyString);
            Assert.Equal(2f, obj.GetMyFloat);
            Assert.Equal(new Uri("https://microsoft.com"), obj.MyUri);

            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "MyInt":1
                """, yaml);
            Assert.Contains("""
                "MyString":"Hello"
                """, yaml);
            Assert.Contains("""
                "MyFloat":2
                """, yaml);
            Assert.Contains("""
                "MyUri":"https://microsoft.com"
                """, yaml);
        }

        public class MyClass_WithNonPublicAccessors_WithPropertyAttributes
        {
            [YamlInclude]
            public int MyInt { get; private set; }
            [YamlInclude]
            public string? MyString { get; internal set; }
            [YamlInclude]
            public float MyFloat { private get; set; }
            [YamlInclude]
            public Uri? MyUri { internal get; set; }

            // For test validation.
            internal float GetMyFloat => MyFloat;
        }

        private class MyClass_WithNonPublicAccessors_WithPropertyAttributes_And_PropertyIgnore
        {
            [YamlInclude]
            [YamlIgnore]
            public int MyInt { get; private set; }

            [YamlInclude]
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string MyString { get; internal set; } = "DefaultString";

            [YamlInclude]
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public float MyFloat { private get; set; }

            [YamlInclude]
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public Uri MyUri { internal get; set; }

            // For test validation.
            internal float GetMyFloat => MyFloat;
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        // Need support for extension data.
        [ActiveIssue("https://github.com/dotnet/runtime/issues/45448")]
#endif
        [Trait("category", "jsonoutput")]
        public async Task ExtensionDataCanHaveNonPublicSetter()
        {
            string yaml = """{"Key":"Value"}""";

            // Baseline
            var obj1 = await Serializer.DeserializeWrapper<ClassWithExtensionData_NonPublicSetter>(yaml);
            Assert.Null(obj1.ExtensionData);
            Assert.Equal("{}", await Serializer.SerializeWrapper(obj1));

            // With attribute
            var obj2 = await Serializer.DeserializeWrapper<ClassWithExtensionData_NonPublicSetter_WithAttribute>(yaml);
            Assert.Equal("Value", obj2.ExtensionData["Key"].GetString());
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj2));
        }

        private class ClassWithExtensionData_NonPublicSetter
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { get; private set; }
        }

        private class ClassWithExtensionData_NonPublicSetter_WithAttribute
        {
            [YamlExtensionData]
            [YamlInclude]
            public Dictionary<string, YamlElement> ExtensionData { get; private set; }
        }

        private class ClassWithExtensionData_NonPublicGetter
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { internal get; set; }
        }

        [Fact]
        public virtual async Task HonorCustomConverter_UsingPrivateSetter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlStringEnumConverter<MySmallEnum>());

            string yaml = """{"MyEnum":"AnotherValue","MyInt":2}""";

            // Deserialization baseline, without enum converter, we get YamlException.
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithPropertiesWithConverter>(yaml));

            var obj = await Serializer.DeserializeWrapper<StructWithPropertiesWithConverter>(yaml, options);
            Assert.Equal(MySmallEnum.AnotherValue, obj.GetMyEnum);
            Assert.Equal(25, obj.MyInt);

            // ConverterForInt32 throws this exception.
            await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(obj, options));
        }

        public struct StructWithPropertiesWithConverter
        {
            [YamlInclude]
            public MySmallEnum MyEnum { private get; set; }

            [YamlInclude]
            [YamlConverter(typeof(ConverterForInt32))]
            public int MyInt { get; private set; }

            // For test validation.
            internal MySmallEnum GetMyEnum => MyEnum;
        }

        public enum MySmallEnum
        {
            DefaultValue = 0,
            AnotherValue = 1
        }

        [Fact]
        public async Task HonorCaseInsensitivity()
        {
            var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true };

            string yaml = """{"MYSTRING":"Hello"}""";
            Assert.Null((await Serializer.DeserializeWrapper<MyStruct_WithNonPublicAccessors_WithTypeAttribute>(yaml)).MyString);
            Assert.Equal("Hello", (await Serializer.DeserializeWrapper<MyStruct_WithNonPublicAccessors_WithTypeAttribute>(yaml, options)).MyString);
        }

        public struct MyStruct_WithNonPublicAccessors_WithTypeAttribute
        {
            [YamlInclude]
            public int MyInt { get; private set; }
            [YamlInclude]
            public string MyString { get; internal set; }
            [YamlInclude]
            public float MyFloat { private get; set; }
            [YamlInclude]
            public Uri MyUri { internal get; set; }

            // For test validation.
            internal float GetMyFloat => MyFloat;
        }

        [Fact]
        public async Task HonorNamingPolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };

            string yaml = """{"my_string":"Hello"}""";
            Assert.Null((await Serializer.DeserializeWrapper<MyStruct_WithNonPublicAccessors_WithTypeAttribute>(yaml)).MyString);
            Assert.Equal("Hello", (await Serializer.DeserializeWrapper<MyStruct_WithNonPublicAccessors_WithTypeAttribute>(yaml, options)).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task HonorYamlPropertyName_PrivateGetter()
        {
            string yaml = """{"prop1":1}""";

            var obj = await Serializer.DeserializeWrapper<StructWithPropertiesWithYamlPropertyName_PrivateGetter>(yaml);
            Assert.Equal(MySmallEnum.AnotherValue, obj.GetProxy());

            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "prop1":1
                """, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task HonorYamlPropertyName_PrivateSetter()
        {
            string yaml = """{"prop2":2}""";

            var obj = await Serializer.DeserializeWrapper<StructWithPropertiesWithYamlPropertyName_PrivateSetter>(yaml);
            Assert.Equal(2, obj.MyInt);

            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "prop2":2
                """, yaml);
        }

        public struct StructWithPropertiesWithYamlPropertyName_PrivateGetter
        {
            [YamlInclude]
            [YamlPropertyName("prop1")]
            public MySmallEnum MyEnum { private get; set; }

            // For test validation.
            internal MySmallEnum GetProxy() => MyEnum;
        }

        public struct StructWithPropertiesWithYamlPropertyName_PrivateSetter
        {
            [YamlInclude]
            [YamlPropertyName("prop2")]
            public int MyInt { get; private set; }

            internal void SetProxy(int myInt) => MyInt = myInt;
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        // Needs support for parameterized ctors.
        [ActiveIssue("https://github.com/dotnet/runtime/issues/45448")]
#endif
        public async Task Map_YamlSerializableProperties_ToCtorArgs()
        {
            var obj = await Serializer.DeserializeWrapper<PointWith_YamlSerializableProperties>("""{"X":1,"Y":2}""");
            Assert.Equal(1, obj.X);
            Assert.Equal(2, obj.GetY);
        }

        private struct PointWith_YamlSerializableProperties
        {
            [YamlInclude]
            public int X { get; internal set; }
            [YamlInclude]
            public int Y { internal get; set;  }

            internal int GetY => Y;

            [YamlConstructor]
            public PointWith_YamlSerializableProperties(int x, int y) => (X, Y) = (x, y);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task Public_And_NonPublicPropertyAccessors_PropertyAttributes()
        {
            string yaml = """{"W":1,"X":2,"Y":3,"Z":4}""";

            var obj = await Serializer.DeserializeWrapper<ClassWithMixedPropertyAccessors_PropertyAttributes>(yaml);
            Assert.Equal(1, obj.W);
            Assert.Equal(2, obj.X);
            Assert.Equal(3, obj.Y);
            Assert.Equal(4, obj.GetZ);

            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "W":1
                """, yaml);
            Assert.Contains("""
                "X":2
                """, yaml);
            Assert.Contains("""
                "Y":3
                """, yaml);
            Assert.Contains("""
                "Z":4
                """, yaml);
        }

        public class ClassWithMixedPropertyAccessors_PropertyAttributes
        {
            [YamlInclude]
            public int W { get; set; }
            [YamlInclude]
            public int X { get; internal set; }
            [YamlInclude]
            public int Y { get; set; }
            [YamlInclude]
            public int Z { private get; set; }

            internal int GetZ => Z;
        }

        [Theory]
        [InlineData(typeof(ClassWithPrivateProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternalProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtectedProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithPrivateField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternalField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtectedField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty))]
        [Trait("category", "jsonoutput")]
        public virtual async Task NonPublicProperty_YamlInclude_WorksAsExpected([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            string yaml = """{"MyString":"value"}""";
            MemberInfo memberInfo = type.GetMember("MyString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];

            object result = await Serializer.DeserializeWrapper(yaml, type);
            Assert.IsType(type, result);
            Assert.Equal("value", memberInfo is PropertyInfo p ? p.GetValue(result) : ((FieldInfo)memberInfo).GetValue(result));

            string actualYaml = await Serializer.SerializeWrapper(result, type);
            Assert.Equal(yaml, actualYaml);
        }

        public class ClassWithPrivateProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            private string MyString { get; set; }
        }

        public class ClassWithInternalProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            internal string MyString { get; set; }
        }

        public class ClassWithProtectedProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            protected string MyString { get; private set; }
        }

        public class ClassWithPrivateField_WithYamlIncludeProperty
        {
            [YamlInclude]
            private string MyString = null;

            public override string ToString() => MyString;
        }

        public class ClassWithInternalField_WithYamlIncludeProperty
        {
            [YamlInclude]
            internal string MyString = null;
        }

        public class ClassWithProtectedField_WithYamlIncludeProperty
        {
            [YamlInclude]
            protected string MyString = null;
        }

        public class ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            private string MyString { get; init; }
        }

        public class ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            internal string MyString { get; init; }
        }

        public class ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty
        {
            [YamlInclude]
            protected string MyString { get; init; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CanAlwaysRoundtripInternalYamlIncludeProperties()
        {
            // Internal YamlInclude properties should be honored
            // by both reflection and the source generator.

            var value = new ClassWithInternalYamlIncludeProperties { X = 1, Y = 2 };
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"X":1,"Y":2}""", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithInternalYamlIncludeProperties>(yaml);
            Assert.Equal(1, value.X);
            Assert.Equal(2, value.Y);
        }

        [Fact]
        public virtual void InitOnlyProperties_ExposesSetterDelegate()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithYamlIncludePrivateInitOnlyProperties));

            YamlPropertyInfo nameProp = typeInfo.Properties.Single(p => p.Name == "Name");
            Assert.NotNull(nameProp.Get);
            Assert.NotNull(nameProp.Set);
            Assert.Null(nameProp.AssociatedParameter);

            YamlPropertyInfo numberProp = typeInfo.Properties.Single(p => p.Name == "Number");
            Assert.NotNull(numberProp.Get);
            Assert.NotNull(numberProp.Set);
            Assert.Null(numberProp.AssociatedParameter);
        }

        [Theory]
        [InlineData(typeof(ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty))]
        public virtual void NonPublicInitOnlyYamlIncludeProperties_HaveNoAssociatedParameterInfo(Type type)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);

            YamlPropertyInfo prop = typeInfo.Properties.Single(p => p.Name == "MyString");
            Assert.NotNull(prop.Get);
            Assert.NotNull(prop.Set);
            Assert.Null(prop.AssociatedParameter);
        }

        [Fact]
        public virtual void PrivateYamlIncludeProperties_ExposesGetterAndSetterDelegates()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithPrivateYamlIncludeProperties_Roundtrip));

            YamlPropertyInfo nameProp = typeInfo.Properties.Single(p => p.Name == "Name");
            Assert.NotNull(nameProp.Get);
            Assert.NotNull(nameProp.Set);

            YamlPropertyInfo ageProp = typeInfo.Properties.Single(p => p.Name == "Age");
            Assert.NotNull(ageProp.Get);
            Assert.NotNull(ageProp.Set);
        }

        [Fact]
        public virtual void PrivateYamlIncludeGetterOnly_ExposesGetterDelegate()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithYamlIncludePrivateGetterProperties));

            YamlPropertyInfo nameProp = typeInfo.Properties.Single(p => p.Name == "Name");
            Assert.NotNull(nameProp.Get);
            Assert.NotNull(nameProp.Set);

            YamlPropertyInfo numberProp = typeInfo.Properties.Single(p => p.Name == "Number");
            Assert.NotNull(numberProp.Get);
            Assert.NotNull(numberProp.Set);
        }

        [Theory]
        [InlineData(typeof(ClassWithPrivateProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternalProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtectedProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithPrivateField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternalField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtectedField_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty))]
        [InlineData(typeof(ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty))]
        public virtual void NonPublicYamlIncludeMembers_ExposeGetterAndSetterDelegates(Type type)
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(type);

            YamlPropertyInfo prop = typeInfo.Properties.Single(p => p.Name == "MyString");
            Assert.NotNull(prop.Get);
            Assert.NotNull(prop.Set);
        }

        [Fact]
        public virtual void MixedAccessibilityYamlIncludeProperties_AllExposeGetterAndSetterDelegates()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithMixedAccessibilityYamlIncludeProperties));
            Assert.Equal(4, typeInfo.Properties.Count);

            foreach (YamlPropertyInfo prop in typeInfo.Properties)
            {
                Assert.NotNull(prop.Get);
                Assert.NotNull(prop.Set);
            }
        }

        [Fact]
        public virtual void StructWithPrivateYamlIncludeProperties_ExposesGetterAndSetterDelegates()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(StructWithYamlIncludePrivateProperties));

            YamlPropertyInfo nameProp = typeInfo.Properties.Single(p => p.Name == "Name");
            Assert.NotNull(nameProp.Get);
            Assert.NotNull(nameProp.Set);

            YamlPropertyInfo numberProp = typeInfo.Properties.Single(p => p.Name == "Number");
            Assert.NotNull(numberProp.Get);
            Assert.NotNull(numberProp.Set);
        }

        [Fact]
        public void PublicPropertyWithNonPublicAccessors_WithoutYamlInclude_ExposesOnlyPublicAccessors()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(ClassWithInternalYamlIncludeProperties));

            // X has public get, internal set (with [YamlInclude]) - both should be exposed
            YamlPropertyInfo xProp = typeInfo.Properties.Single(p => p.Name == "X");
            Assert.NotNull(xProp.Get);
            Assert.NotNull(xProp.Set);

            // Y has internal get (with [YamlInclude]), public set - both should be exposed
            YamlPropertyInfo yProp = typeInfo.Properties.Single(p => p.Name == "Y");
            Assert.NotNull(yProp.Get);
            Assert.NotNull(yProp.Set);
        }

        public class ClassWithInternalYamlIncludeProperties
        {
            [YamlInclude]
            public int X { get; internal set; }
            [YamlInclude]
            public int Y { internal get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task ClassWithIgnoredAndPrivateMembers_DoesNotIncludeIgnoredMetadata()
        {
            YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);

            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(ClassWithIgnoredAndPrivateMembers));

            // The contract surfaces the ignored properties but not the private ones
            Assert.Equal(2, typeInfo.Properties.Count);
            Assert.Contains(typeInfo.Properties, prop => prop.Name == "PublicIgnoredField");
            Assert.Contains(typeInfo.Properties, prop => prop.Name == "PublicIgnoredProperty");

            // The ignored properties included in the contract do not specify any accessor delegates.
            Assert.All(typeInfo.Properties, prop => Assert.True(prop.Get is null));
            Assert.All(typeInfo.Properties, prop => Assert.True(prop.Set is null));

            string yaml = await Serializer.SerializeWrapper(new ClassWithIgnoredAndPrivateMembers(), options);
            Assert.Equal("{}", yaml);
        }

        public class ClassWithIgnoredAndPrivateMembers
        {
            [YamlIgnore]
            public TypeThatShouldNotBeGenerated PublicIgnoredField = new();

            private TypeThatShouldNotBeGenerated PrivateField = new();

            [YamlIgnore]
            public TypeThatShouldNotBeGenerated PublicIgnoredProperty { get; set; } = new();

            private TypeThatShouldNotBeGenerated PrivateProperty { get; set; } = new();
        }

        public class TypeThatShouldNotBeGenerated
        {
            private protected object _thisLock = new object();
        }

        // ---- Extensive test types for [YamlInclude] with inaccessible members ----

        public class ClassWithPrivateYamlIncludeProperties_Roundtrip
        {
            [YamlInclude]
            private string Name { get; set; } = "default";
            [YamlInclude]
            private int Age { get; set; }

            public static ClassWithPrivateYamlIncludeProperties_Roundtrip Create(string name, int age)
            {
                var obj = new ClassWithPrivateYamlIncludeProperties_Roundtrip();
                obj.Name = name;
                obj.Age = age;
                return obj;
            }

            // For test validation.
            internal string GetName() => Name;
            internal int GetAge() => Age;
        }

        public class ClassWithProtectedYamlIncludeProperties_Roundtrip
        {
            [YamlInclude]
            protected string Name { get; set; } = "default";
            [YamlInclude]
            protected int Age { get; set; }

            public static ClassWithProtectedYamlIncludeProperties_Roundtrip Create(string name, int age)
            {
                var obj = new ClassWithProtectedYamlIncludeProperties_Roundtrip();
                obj.Name = name;
                obj.Age = age;
                return obj;
            }

            // For test validation.
            internal string GetName() => Name;
            internal int GetAge() => Age;
        }

        public class ClassWithMixedAccessibilityYamlIncludeProperties
        {
            [YamlInclude]
            public int PublicProp { get; set; }
            [YamlInclude]
            internal int InternalProp { get; set; }
            [YamlInclude]
            private int PrivateProp { get; set; }
            [YamlInclude]
            protected int ProtectedProp { get; set; }

            internal int GetPrivateProp() => PrivateProp;
            internal int GetProtectedProp() => ProtectedProp;
        }

        public class ClassWithYamlIncludePrivateInitOnlyProperties
        {
            [YamlInclude]
            public string Name { get; private init; } = "DefaultName";
            [YamlInclude]
            public int Number { get; private init; } = 42;
        }

        public class ClassWithYamlIncludePrivateGetterProperties
        {
            [YamlInclude]
            public string Name { private get; set; } = "DefaultName";
            [YamlInclude]
            public int Number { private get; set; } = 42;

            internal string GetName() => Name;
            internal int GetNumber() => Number;
        }

        public struct StructWithYamlIncludePrivateProperties
        {
            [YamlInclude]
            private string Name { get; set; }
            [YamlInclude]
            private int Number { get; set; }

            public static StructWithYamlIncludePrivateProperties Create(string name, int number) =>
                new StructWithYamlIncludePrivateProperties { Name = name, Number = number };

            internal readonly string GetName() => Name;
            internal readonly int GetNumber() => Number;
        }

        public class GenericClassWithPrivateYamlIncludeProperties<T>
        {
            [YamlInclude]
            private T Value { get; set; }

            [YamlInclude]
            private string Label { get; set; } = "default";

            public static GenericClassWithPrivateYamlIncludeProperties<T> Create(T value, string label)
            {
                var obj = new GenericClassWithPrivateYamlIncludeProperties<T>();
                obj.Value = value;
                obj.Label = label;
                return obj;
            }

            public T GetValue() => Value;
            public string GetLabel() => Label;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_PrivateProperties_CanRoundtrip()
        {
            var obj = ClassWithPrivateYamlIncludeProperties_Roundtrip.Create("Test", 25);
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""Name"":""Test""", yaml);
            Assert.Contains(@"""Age"":25", yaml);

            var deserialized = await Serializer.DeserializeWrapper<ClassWithPrivateYamlIncludeProperties_Roundtrip>(yaml);
            Assert.Equal("Test", deserialized.GetName());
            Assert.Equal(25, deserialized.GetAge());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_ProtectedProperties_CanRoundtrip()
        {
            var obj = ClassWithProtectedYamlIncludeProperties_Roundtrip.Create("Test", 25);
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""Name"":""Test""", yaml);
            Assert.Contains(@"""Age"":25", yaml);

            var deserialized = await Serializer.DeserializeWrapper<ClassWithProtectedYamlIncludeProperties_Roundtrip>(yaml);
            Assert.Equal("Test", deserialized.GetName());
            Assert.Equal(25, deserialized.GetAge());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_MixedAccessibility_AllPropertiesRoundtrip()
        {
            string yaml = """{"PublicProp":1,"InternalProp":2,"PrivateProp":3,"ProtectedProp":4}""";
            var deserialized = await Serializer.DeserializeWrapper<ClassWithMixedAccessibilityYamlIncludeProperties>(yaml);
            Assert.Equal(1, deserialized.PublicProp);
            Assert.Equal(2, deserialized.InternalProp);
            Assert.Equal(3, deserialized.GetPrivateProp());
            Assert.Equal(4, deserialized.GetProtectedProp());

            string actualYaml = await Serializer.SerializeWrapper(deserialized);
            Assert.Contains(@"""PublicProp"":1", actualYaml);
            Assert.Contains(@"""InternalProp"":2", actualYaml);
            Assert.Contains(@"""PrivateProp"":3", actualYaml);
            Assert.Contains(@"""ProtectedProp"":4", actualYaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_PrivateInitOnlyProperties_PreservesDefaults()
        {
            // Deserializing empty JSON should preserve default values.
            var deserialized = await Serializer.DeserializeWrapper<ClassWithYamlIncludePrivateInitOnlyProperties>("{}");
            Assert.Equal("DefaultName", deserialized.Name);
            Assert.Equal(42, deserialized.Number);

            // Deserializing with values should override defaults.
            deserialized = await Serializer.DeserializeWrapper<ClassWithYamlIncludePrivateInitOnlyProperties>("""{"Name":"Override","Number":100}""");
            Assert.Equal("Override", deserialized.Name);
            Assert.Equal(100, deserialized.Number);

            // Serialization should work.
            string yaml = await Serializer.SerializeWrapper(deserialized);
            Assert.Contains(@"""Name"":""Override""", yaml);
            Assert.Contains(@"""Number"":100", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_PrivateGetterProperties_CanSerialize()
        {
            var obj = new ClassWithYamlIncludePrivateGetterProperties { Name = "Test", Number = 99 };
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""Name"":""Test""", yaml);
            Assert.Contains(@"""Number"":99", yaml);

            var deserialized = await Serializer.DeserializeWrapper<ClassWithYamlIncludePrivateGetterProperties>(yaml);
            Assert.Equal("Test", deserialized.GetName());
            Assert.Equal(99, deserialized.GetNumber());
        }

        [Fact]
        public virtual async Task YamlInclude_PrivateProperties_EmptyYaml_DeserializesToDefault()
        {
            var deserialized = await Serializer.DeserializeWrapper<ClassWithPrivateYamlIncludeProperties_Roundtrip>("{}");
            Assert.Equal("default", deserialized.GetName());
            Assert.Equal(0, deserialized.GetAge());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_StructWithPrivateProperties_CanRoundtrip()
        {
            var obj = StructWithYamlIncludePrivateProperties.Create("Hello", 42);
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""Name"":""Hello""", yaml);
            Assert.Contains(@"""Number"":42", yaml);

            var deserialized = await Serializer.DeserializeWrapper<StructWithYamlIncludePrivateProperties>(yaml);
            Assert.Equal("Hello", deserialized.GetName());
            Assert.Equal(42, deserialized.GetNumber());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task YamlInclude_GenericType_PrivateProperties_CanRoundtrip()
        {
            var obj = GenericClassWithPrivateYamlIncludeProperties<int>.Create(42, "test");
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""Value"":42", yaml);
            Assert.Contains(@"""Label"":""test""", yaml);

            var deserialized = await Serializer.DeserializeWrapper<GenericClassWithPrivateYamlIncludeProperties<int>>(yaml);
            Assert.Equal(42, deserialized.GetValue());
            Assert.Equal("test", deserialized.GetLabel());
        }

        public struct GenericStructWithPrivateYamlIncludeProperties<T>
        {
            [YamlInclude]
            private T Value { get; set; }

            [YamlInclude]
            private string Label { get; set; }
        }

        [Theory]
        [InlineData(typeof(GenericStructWithPrivateYamlIncludeProperties<int>), """{"Value":42,"Label":"test"}""")]
        [InlineData(typeof(GenericStructWithPrivateYamlIncludeProperties<string>), """{"Value":"hello","Label":"test"}""")]
        public async Task YamlInclude_GenericStruct_PrivateProperties_CanRoundtrip(Type type, string yaml)
        {
            object result = await Serializer.DeserializeWrapper(yaml, type);
            YamlTestHelper.AssertYamlEqual(yaml, await Serializer.SerializeWrapper(result, type));
        }

        public class GenericClassWithReadOnlyYamlIncludeMembers<T>
        {
            [YamlInclude]
            [YamlPropertyName("Value")]
            private readonly T _value;

            [YamlInclude]
            private string Label { get; } = "initial";

            public GenericClassWithReadOnlyYamlIncludeMembers() : this(default!) { }

            public GenericClassWithReadOnlyYamlIncludeMembers(T value) => _value = value;

            public T GetValue() => _value;
        }

        [Theory]
        [InlineData("""{"Value":42}""")]
        [InlineData("""{"Label":"changed"}""")]
        public async Task YamlInclude_GenericReadOnlyMembers_AreNotDeserialized(string yaml)
        {
            var result = await Serializer.DeserializeWrapper<GenericClassWithReadOnlyYamlIncludeMembers<int>>(yaml);
            Assert.Equal(0, result.GetValue());
            YamlTestHelper.AssertYamlEqual("""{"Value":0,"Label":"initial"}""", await Serializer.SerializeWrapper(result));
        }

        public class GenericClassWithPrivateYamlIncludeFields<T>
        {
            [YamlInclude]
            [YamlPropertyName("Value")]
            private T _value;

            [YamlInclude]
            [YamlPropertyName("Label")]
            private string _label;

            public GenericClassWithPrivateYamlIncludeFields() : this(default!, "initial") { }

            public GenericClassWithPrivateYamlIncludeFields(T value, string label)
            {
                _value = value;
                _label = label;
            }

            public T GetValue() => _value;
            public string GetLabel() => _label;
        }

        public struct GenericStructWithPrivateYamlIncludeFields<T>
        {
            [YamlInclude]
            [YamlPropertyName("Value")]
            private T _value;

            [YamlInclude]
            [YamlPropertyName("Label")]
            private string _label;

            public GenericStructWithPrivateYamlIncludeFields(T value, string label)
            {
                _value = value;
                _label = label;
            }

            public readonly T GetValue() => _value;
            public readonly string GetLabel() => _label;
        }

        [Theory]
        [InlineData(typeof(GenericClassWithPrivateYamlIncludeFields<int>), """{"Value":42,"Label":"test"}""")]
        [InlineData(typeof(GenericClassWithPrivateYamlIncludeFields<string>), """{"Value":"hello","Label":"test"}""")]
        [InlineData(typeof(GenericStructWithPrivateYamlIncludeFields<int>), """{"Value":42,"Label":"test"}""")]
        [InlineData(typeof(GenericStructWithPrivateYamlIncludeFields<string>), """{"Value":"hello","Label":"test"}""")]
        public async Task YamlInclude_GenericFields_CanRoundtrip(Type type, string yaml)
        {
            object result = await Serializer.DeserializeWrapper(yaml, type);
            YamlTestHelper.AssertYamlEqual(yaml, await Serializer.SerializeWrapper(result, type));
        }

        public struct StructWithPrivateConstructorAndMembers
        {
            public int Value { get; }

            [YamlInclude, YamlPropertyName("Field")]
            private int _field;

            [YamlInclude, YamlPropertyName("ReadOnly")]
            private readonly int _readOnly;

            [YamlInclude]
            private string Property { get; set; }

            [YamlInclude]
            private string InitOnly { get; init; }

            [YamlConstructor]
            private StructWithPrivateConstructorAndMembers(int value)
            {
                Value = value;
                _field = -1;
                _readOnly = 23;
                Property = "initial";
                InitOnly = "initial";
            }

            public readonly int GetField() => _field;
            public readonly int GetReadOnly() => _readOnly;
        }

        public struct GenericStructWithPrivateInitOnlyMembers<T>
        {
            public T Value { get; }

            [YamlInclude, YamlPropertyName("ReadOnly")]
            private readonly T _readOnly;

            [YamlInclude]
            private T InitOnly { get; init; }

            [YamlConstructor]
            private GenericStructWithPrivateInitOnlyMembers(T value)
            {
                Value = value;
                _readOnly = value;
                InitOnly = default!;
            }
        }

        [Theory]
        [InlineData(typeof(StructWithPrivateConstructorAndMembers),
            """{"Value":42}""",
            """{"Value":42,"Field":-1,"ReadOnly":23,"Property":"initial","InitOnly":"initial"}""")]
        [InlineData(typeof(StructWithPrivateConstructorAndMembers),
            """{"Value":42,"Field":17,"ReadOnly":99,"Property":"updated","InitOnly":"initialized"}""",
            """{"Value":42,"Field":17,"ReadOnly":23,"Property":"updated","InitOnly":"initialized"}""")]
        [InlineData(typeof(GenericStructWithPrivateInitOnlyMembers<int>),
            """{"Value":42,"ReadOnly":99,"InitOnly":17}""",
            """{"Value":42,"ReadOnly":42,"InitOnly":17}""")]
        [InlineData(typeof(GenericStructWithPrivateInitOnlyMembers<string>),
            """{"Value":"constructor","ReadOnly":"ignored","InitOnly":"initialized"}""",
            """{"Value":"constructor","ReadOnly":"constructor","InitOnly":"initialized"}""")]
        public async Task YamlInclude_Struct_PrivateConstructorAndMembers_CanRoundtrip(Type type, string yaml, string expectedYaml)
        {
            object result = await Serializer.DeserializeWrapper(yaml, type);
            Assert.IsType(type, result);
            YamlTestHelper.AssertYamlEqual(expectedYaml, await Serializer.SerializeWrapper(result, type));
        }

        public class GenericMemberOuter<TOuter>
        {
            public class Nested<TInner>
            {
                [YamlInclude]
                private TOuter OuterValue { get; set; }

                public TInner InnerValue { get; init; }
            }

            public class Nested
            {
                [YamlInclude]
                private TOuter Value { get; set; }
            }
        }

        public class GenericAccessorBase<T>
        {
            [YamlInclude]
            [YamlPropertyName("BaseValue")]
            private T Value { get; set; }
        }

        public class GenericAccessorDerived<T> : GenericAccessorBase<T>
        {
            [YamlInclude]
            [YamlPropertyName("DerivedValue")]
            private T Value { get; set; }
        }

        public class NonGenericAccessorDerived : GenericAccessorBase<int> { }

        public class GenericAccessorGrandBase<TGrand> where TGrand : class
        {
            [YamlInclude]
            private TGrand GrandSecret { get; set; } = default!;
        }

        public class GenericAccessorIntermediate<TBase> : GenericAccessorGrandBase<string> where TBase : struct
        {
            [YamlInclude]
            private TBase BaseSecret { get; set; }
        }

        public class NonGenericAccessorLeaf : GenericAccessorIntermediate<int> { }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task YamlInclude_MultipleGenericBaseTypes_CanRoundtrip(bool deserialize)
        {
            const string PopulatedYaml = """{"BaseSecret":42,"GrandSecret":"secret"}""";
            const string DefaultYaml = """{"BaseSecret":0,"GrandSecret":null}""";

            NonGenericAccessorLeaf value = deserialize
                ? await Serializer.DeserializeWrapper<NonGenericAccessorLeaf>(PopulatedYaml)
                : new NonGenericAccessorLeaf();

            YamlTestHelper.AssertYamlEqual(
                deserialize ? PopulatedYaml : DefaultYaml,
                await Serializer.SerializeWrapper(value));
        }

        public class GenericAccessorDerivedWithPublicProperty<T> : GenericAccessorBase<T>
        {
            public T DerivedValue { get; set; }
        }

        public class GenericAccessorDerivedWithIgnoredProperty<T> : GenericAccessorBase<T>
        {
            [YamlIgnore]
            public T IgnoredValue { get; init; }
        }

        public class GenericAccessorDerived<TFirst, TSecond> : GenericAccessorBase<TFirst>
        {
            [YamlInclude]
            [YamlPropertyName("DerivedValue")]
            private TSecond Value { get; set; }
        }

        public class GenericMembersWithKeywordParameter<@class> where @class : struct
        {
            [YamlInclude]
            private @class Value { get; set; }

            public @class Other { get; init; }
        }

        public class ConstrainedGenericAccessor<TValue, TCollection>
            where TValue : struct
            where TCollection : ICollection<TValue>, new()
        {
            [YamlInclude]
            private TValue Value { get; set; }

            public TCollection Values { get; init; } = new();
        }

        [Theory]
        [InlineData(typeof(GenericMemberOuter<int>.Nested<string>), """{"OuterValue":42,"InnerValue":"test"}""")]
        [InlineData(typeof(GenericMemberOuter<int>.Nested), """{"Value":42}""")]
        [InlineData(typeof(GenericAccessorDerived<int>), """{"BaseValue":1,"DerivedValue":2}""")]
        [InlineData(typeof(NonGenericAccessorDerived), """{"BaseValue":42}""")]
        [InlineData(typeof(GenericAccessorDerivedWithPublicProperty<int>), """{"BaseValue":1,"DerivedValue":2}""")]
        [InlineData(typeof(GenericAccessorDerivedWithIgnoredProperty<int>), """{"BaseValue":42}""")]
        [InlineData(typeof(GenericAccessorDerived<int, string>), """{"BaseValue":42,"DerivedValue":"test"}""")]
        [InlineData(typeof(GenericMembersWithKeywordParameter<int>), """{"Value":42,"Other":7}""")]
        [InlineData(typeof(ConstrainedGenericAccessor<int, List<int>>), """{"Value":42,"Values":[1,2]}""")]
        public async Task YamlInclude_GenericNestedAndInheritedMembers_CanRoundtrip(Type type, string yaml)
        {
            object result = await Serializer.DeserializeWrapper(yaml, type);
            YamlTestHelper.AssertYamlEqual(yaml, await Serializer.SerializeWrapper(result, type));
        }

        public class ConstraintBase
        {
            public int Id { get; set; }
        }

        public class ConstraintDerived : ConstraintBase
        {
            public string Extra { get; set; } = "";
        }

        public class ConstrainedGenericClassWithInitOnlyProperties<T> where T : notnull, ConstraintBase
        {
            public required T Response { get; init; }
            public required string Name { get; init; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual async Task InitOnlyProperties_GenericTypeWithConstraints_CanRoundtrip()
        {
            var derived = new ConstraintDerived { Id = 1, Extra = "extra" };
            string yaml = await Serializer.SerializeWrapper(
                new ConstrainedGenericClassWithInitOnlyProperties<ConstraintDerived> { Response = derived, Name = "test" });
            Assert.Contains(@"""Name"":""test""", yaml);
            Assert.Contains(@"""Id"":1", yaml);

            var deserialized = await Serializer.DeserializeWrapper<ConstrainedGenericClassWithInitOnlyProperties<ConstraintDerived>>(yaml);
            Assert.Equal("test", deserialized.Name);
            Assert.Equal(1, deserialized.Response.Id);
            Assert.Equal("extra", deserialized.Response.Extra);
        }

        public class ConstrainedGenericClassWithOptionalInitOnlyProperties<T> where T : notnull, ConstraintBase
        {
            public T? Response { get; init; }
            public string Name { get; init; } = "default";
        }

        [Fact]
        public async Task InitOnlyProperties_ConstrainedGenericType_PreservesDefaults()
        {
            var value = new ConstrainedGenericClassWithOptionalInitOnlyProperties<ConstraintDerived>
            {
                Response = new ConstraintDerived { Id = 2, Extra = "optional" },
                Name = "updated"
            };

            string yaml = await Serializer.SerializeWrapper(value);
            var deserialized = await Serializer.DeserializeWrapper<ConstrainedGenericClassWithOptionalInitOnlyProperties<ConstraintDerived>>(yaml);
            Assert.Equal(2, deserialized.Response.Id);
            Assert.Equal("optional", deserialized.Response.Extra);
            Assert.Equal("updated", deserialized.Name);

            deserialized = await Serializer.DeserializeWrapper<ConstrainedGenericClassWithOptionalInitOnlyProperties<ConstraintDerived>>("{}");
            Assert.Null(deserialized.Response);
            Assert.Equal("default", deserialized.Name);
        }
    }
}
