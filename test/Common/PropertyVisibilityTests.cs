// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class PropertyVisibilityTests : SerializerTests
    {
        public PropertyVisibilityTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper) { }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicField()
        {
            // Serialize
            var obj = new ClassWithNewSlotField();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyString":"NewDefaultValue"}""", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotField>(yaml);

            Assert.Equal("NewValue", ((ClassWithNewSlotField)obj).MyString);
            Assert.Equal("DefaultValue", ((ClassWithInternalField)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicProperty()
        {
            // Serialize
            var obj = new ClassWithNewSlotProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyString":"NewDefaultValue"}""", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotProperty>(yaml);

            Assert.Equal("NewValue", ((ClassWithNewSlotProperty)obj).MyString);
            Assert.Equal("DefaultValue", ((ClassWithInternalProperty)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_BasePublicProperty_ConflictWithDerivedPrivate()
        {
            // Serialize
            var obj = new ClassWithNewSlotInternalProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyString":"DefaultValue"}""", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotInternalProperty>(yaml);

            Assert.Equal("NewValue", ((ClassWithPublicProperty)obj).MyString);
            Assert.Equal("NewDefaultValue", ((ClassWithNewSlotInternalProperty)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_PublicProperty_ConflictWithPrivateDueAttributes()
        {
            // Serialize
            var obj = new ClassWithPropertyNamingConflict();

            // Newtonsoft.Json throws JsonSerializationException here because
            // non-public properties are included when [YamlProperty] is placed on them.
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyString":"DefaultValue"}""", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";

            // Newtonsoft.Json throws JsonSerializationException here because
            // non-public properties are included when [YamlProperty] is placed on them.
            obj = await Serializer.DeserializeWrapper<ClassWithPropertyNamingConflict>(yaml);

            Assert.Equal("NewValue", obj.MyString);
            Assert.Equal("ConflictingValue", obj.ConflictingString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_PublicProperty_ConflictWithPrivateDuePolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassWithPropertyPolicyConflict();
            string yaml = await Serializer.SerializeWrapper(obj, options);

            Assert.Equal("""{"myString":"DefaultValue"}""", yaml);

            // Deserialize
            yaml = """{"myString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithPropertyPolicyConflict>(yaml, options);

            Assert.Equal("NewValue", obj.MyString);
            Assert.Equal("ConflictingValue", obj.myString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicProperty_ConflictWithBasePublicProperty()
        {
            // Serialize
            var obj = new ClassWithNewSlotDecimalProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyNumeric":1.5}""", yaml);

            // Deserialize
            yaml = """{"MyNumeric":2.5}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotDecimalProperty>(yaml);

            Assert.Equal(2.5M, obj.MyNumeric);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicField_ConflictWithBasePublicProperty()
        {
            // Serialize
            var obj = new ClassWithNewSlotDecimalField();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyNumeric":1.5}""", yaml);

            // Deserialize
            yaml = """{"MyNumeric":2.5}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotDecimalField>(yaml);

            Assert.Equal(2.5M, obj.MyNumeric);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicField_SpecifiedYamlPropertyName()
        {
            // Serialize
            var obj = new ClassWithNewSlotAttributedDecimalField();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Contains("""
                "MyNewNumeric":1.5
                """, yaml);
            Assert.Contains("""
                "MyNumeric":1
                """, yaml);

            // Deserialize
            yaml = """{"MyNewNumeric":2.5,"MyNumeric":4}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotAttributedDecimalField>(yaml);

            Assert.Equal(4, ((ClassWithHiddenByNewSlotIntProperty)obj).MyNumeric);
            Assert.Equal(2.5M, ((ClassWithNewSlotAttributedDecimalField)obj).MyNumeric);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Serialize_NewSlotPublicProperty_SpecifiedYamlPropertyName()
        {
            // Serialize
            var obj = new ClassWithNewSlotAttributedDecimalProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Contains("""
                "MyNewNumeric":1.5
                """, yaml);
            Assert.Contains("""
                "MyNumeric":1
                """, yaml);

            // Deserialize
            yaml = """{"MyNewNumeric":2.5,"MyNumeric":4}""";
            obj = await Serializer.DeserializeWrapper<ClassWithNewSlotAttributedDecimalProperty>(yaml);

            Assert.Equal(4, ((ClassWithHiddenByNewSlotIntProperty)obj).MyNumeric);
            Assert.Equal(2.5M, ((ClassWithNewSlotAttributedDecimalProperty)obj).MyNumeric);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_NonPublicProperty()
        {
            // Serialize
            var obj = new ClassWithInternalProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithInternalProperty>(yaml);

            Assert.Equal("DefaultValue", obj.MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_NewSlotPublicFieldIgnored()
        {
            // Serialize
            var obj = new ClassWithIgnoredNewSlotField();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredNewSlotField>(yaml);

            Assert.Equal("NewDefaultValue", ((ClassWithIgnoredNewSlotField)obj).MyString);
            Assert.Equal("DefaultValue", ((ClassWithInternalField)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_NewSlotPublicPropertyIgnored()
        {
            // Serialize
            var obj = new ClassWithIgnoredNewSlotProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredNewSlotProperty>(yaml);

            Assert.Equal("NewDefaultValue", ((ClassWithIgnoredNewSlotProperty)obj).MyString);
            Assert.Equal("DefaultValue", ((ClassWithInternalProperty)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_BasePublicPropertyIgnored_ConflictWithDerivedPrivate()
        {
            // Serialize
            var obj = new ClassWithIgnoredPublicPropertyAndNewSlotPrivate();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPublicPropertyAndNewSlotPrivate>(yaml);

            Assert.Equal("DefaultValue", ((ClassWithIgnoredPublicProperty)obj).MyString);
            Assert.Equal("NewDefaultValue", ((ClassWithIgnoredPublicPropertyAndNewSlotPrivate)obj).MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_BasePublicPropertyIgnored_ConflictWithDerivedPublicPropertyIgnored()
        {
            var obj = new ClassWithIgnoredPublicPropertyAndNewSlotPublicAndIgnoredToo();

            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPublicPropertyAndNewSlotPublicAndIgnoredToo>(yaml);

            Assert.Equal("DefaultValue", ((ClassWithIgnoredPublicProperty)obj).MyString);
            Assert.Equal("NewDefaultValue", obj.MyString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_VerifyNoReferenceToGetterAndSetter()
        {
            // Serialize
            var obj = new ClassWithObsoleteAndIgnoredProperty();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"MyString_Obsolete":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithObsoleteAndIgnoredProperty>(yaml);

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Equal("DefaultValue", obj.MyString_Obsolete);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_PublicProperty_ConflictWithPrivateDueAttributes()
        {
            // Serialize
            var obj = new ClassWithIgnoredPropertyNamingConflictPrivate();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("{}", yaml);

            // Newtonsoft.Json has the following output because non-public properties are included when [YamlProperty] is placed on them.
            // {"MyString":"ConflictingValue"}

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPropertyNamingConflictPrivate>(yaml);

            Assert.Equal("DefaultValue", obj.MyString);
            Assert.Equal("ConflictingValue", obj.ConflictingString);

            // The output for Newtonsoft.Json is:
            // obj.ConflictingString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_PublicProperty_ConflictWithPrivateDuePolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassWithIgnoredPropertyPolicyConflictPrivate();
            string yaml = await Serializer.SerializeWrapper(obj, options);

            Assert.Equal("{}", yaml);

            // Deserialize
            yaml = """{"myString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPropertyPolicyConflictPrivate>(yaml, options);

            Assert.Equal("DefaultValue", obj.MyString);
            Assert.Equal("ConflictingValue", obj.myString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_PublicProperty_ConflictWithPublicDueAttributes()
        {
            // Serialize
            var obj = new ClassWithIgnoredPropertyNamingConflictPublic();
            string yaml = await Serializer.SerializeWrapper(obj);

            Assert.Equal("""{"MyString":"ConflictingValue"}""", yaml);

            // Deserialize
            yaml = """{"MyString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPropertyNamingConflictPublic>(yaml);

            Assert.Equal("DefaultValue", obj.MyString);
            Assert.Equal("NewValue", obj.ConflictingString);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Ignore_PublicProperty_ConflictWithPublicDuePolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassWithIgnoredPropertyPolicyConflictPublic();
            string yaml = await Serializer.SerializeWrapper(obj, options);

            Assert.Equal("""{"myString":"ConflictingValue"}""", yaml);

            // Deserialize
            yaml = """{"myString":"NewValue"}""";
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoredPropertyPolicyConflictPublic>(yaml, options);

            Assert.Equal("DefaultValue", obj.MyString);
            Assert.Equal("NewValue", obj.myString);
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDueAttributes()
        {
            // Serialize
            var obj = new ClassWithPropertyNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassWithPropertyNamingConflictWhichThrows>(yaml));
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDueAttributes()
        {
            // Serialize
            var obj = new ClassWithPropertyFieldNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassWithPropertyFieldNamingConflictWhichThrows>(yaml));
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDueAttributes_SingleInheritance()
        {
            // Serialize
            var obj = new ClassInheritedWithPropertyNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // The output for Newtonsoft.Json is:
            // {"MyString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassInheritedWithPropertyNamingConflictWhichThrows>(yaml));

            // The output for Newtonsoft.Json is:
            // obj.ConflictingString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDueAttributes_SingleInheritance()
        {
            // Serialize
            var obj = new ClassInheritedWithPropertyFieldNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // The output for Newtonsoft.Json is:
            // {"MyString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassInheritedWithPropertyFieldNamingConflictWhichThrows>(yaml));

            // The output for Newtonsoft.Json is:
            // obj.ConflictingString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDueAttributes_DoubleInheritance()
        {
            // Serialize
            var obj = new ClassTwiceInheritedWithPropertyNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // The output for Newtonsoft.Json is:
            // {"MyString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassTwiceInheritedWithPropertyNamingConflictWhichThrows>(yaml));

            // The output for Newtonsoft.Json is:
            // obj.ConflictingString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDueAttributes_DoubleInheritance()
        {
            // Serialize
            var obj = new ClassTwiceInheritedWithPropertyFieldNamingConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj));

            // The output for Newtonsoft.Json is:
            // {"MyString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassTwiceInheritedWithPropertyFieldNamingConflictWhichThrows>(yaml));

            // The output for Newtonsoft.Json is:
            // obj.ConflictingString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDuePolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassWithPropertyPolicyConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassWithPropertyPolicyConflictWhichThrows>(yaml, options));
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDuePolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassWithPropertyFieldPolicyConflictWhichThrows();
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassWithPropertyFieldPolicyConflictWhichThrows>(yaml, options));
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDuePolicy_SingleInheritance()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassInheritedWithPropertyPolicyConflictWhichThrows();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // The output for Newtonsoft.Json is:
            // {"myString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassInheritedWithPropertyPolicyConflictWhichThrows>(yaml, options));

            // The output for Newtonsoft.Json is:
            // obj.myString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDuePolicy_SingleInheritance()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassInheritedWithPropertyFieldPolicyConflictWhichThrows();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // The output for Newtonsoft.Json is:
            // {"myString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassInheritedWithPropertyFieldPolicyConflictWhichThrows>(yaml, options));

            // The output for Newtonsoft.Json is:
            // obj.myString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicProperty_ConflictDuePolicy_DobuleInheritance()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassTwiceInheritedWithPropertyPolicyConflictWhichThrows();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // The output for Newtonsoft.Json is:
            // {"myString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassTwiceInheritedWithPropertyPolicyConflictWhichThrows>(yaml, options));

            // The output for Newtonsoft.Json is:
            // obj.myString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        public async Task Throw_PublicPropertyAndField_ConflictDuePolicy_DobuleInheritance()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };

            // Serialize
            var obj = new ClassTwiceInheritedWithPropertyFieldPolicyConflictWhichThrows();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.SerializeWrapper(obj, options));

            // The output for Newtonsoft.Json is:
            // {"myString":"ConflictingValue"}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.

            // Deserialize
            string yaml = """{"MyString":"NewValue"}""";

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await Serializer.DeserializeWrapper<ClassTwiceInheritedWithPropertyFieldPolicyConflictWhichThrows>(yaml, options));

            // The output for Newtonsoft.Json is:
            // obj.myString = "NewValue"
            // obj.MyString still equals "DefaultValue"
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task HiddenPropertiesIgnored_WhenOverridesIgnored()
        {
            string serialized = await Serializer.SerializeWrapper(new DerivedClass_With_IgnoredOverride());
            Assert.Equal("{}", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_WithVisibleProperty_Of_DerivedClass_With_IgnoredOverride());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_IgnoredOverride_And_ConflictingPropertyName());
            Assert.Equal("""{"MyProp":null}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_Ignored_NewProperty());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_WithConflictingNewMember());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_WithConflictingNewMember_Of_DifferentType());
            Assert.Equal("""{"MyProp":0}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_Ignored_ConflictingNewMember());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_Ignored_ConflictingNewMember_Of_DifferentType());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_NewProperty_And_ConflictingPropertyName());
            Assert.Equal("""{"MyProp":null}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_Ignored_NewProperty_Of_DifferentType());
            Assert.Equal("""{"MyProp":false}""", serialized);

            serialized = await Serializer.SerializeWrapper(new DerivedClass_With_Ignored_NewProperty_Of_DifferentType_And_ConflictingPropertyName());
            Assert.Equal("""{"MyProp":null}""", serialized);

            serialized = await Serializer.SerializeWrapper(new FurtherDerivedClass_With_ConflictingPropertyName());
            Assert.Equal("""{"MyProp":null}""", serialized);

            // Here we differ from Newtonsoft.Json, where the output would be
            // {"MyProp":null}
            // Conflicts at different type-hierarchy levels that are not caused by
            // deriving or the new keyword are allowed. Properties on more derived types win.
            // This is invalid in System.Text.Yaml.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new DerivedClass_WithConflictingPropertyName()));

            serialized = await Serializer.SerializeWrapper(new FurtherDerivedClass_With_IgnoredOverride());
            Assert.Equal("""{"MyProp":null}""", serialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CorrectlyIgnorePropertyWithNewModifier()
        {
            Class2 instanceOfSecondClassWithSameName,
                actualDeserializationResult;
            string actualSerializationResult;

            instanceOfSecondClassWithSameName = new Class2();
            instanceOfSecondClassWithSameName.class1.Info = "Some content";
            ((NamespaceBase.Class2)instanceOfSecondClassWithSameName).class1.Info = "Some other content";

            actualSerializationResult = await Serializer.SerializeWrapper(instanceOfSecondClassWithSameName);
            actualDeserializationResult = await Serializer.DeserializeWrapper<Class2>(actualSerializationResult);
            Assert.Equal("{}", actualSerializationResult);
            Assert.Equal(Class1.DefaultValueForInfo, actualDeserializationResult.class1.Info);
            Assert.Equal(NamespaceBase.Class1.DefaultValueForInfo, ((NamespaceBase.Class2)actualDeserializationResult).class1.Info);
        }

        public class Class1
        {
            public const string DefaultValueForInfo = $"Some default content of {nameof(Info)} property of {nameof(Tests)}";

            public string Info { get; set; } = DefaultValueForInfo;
        }

        public class Class2 : NamespaceBase.Class2
        {
            [YamlIgnore]
            public new Class1 class1 => new Class1();
        }

        public class ClassWithInternalField
        {
            internal string MyString = "DefaultValue";
        }

        public class ClassWithNewSlotField : ClassWithInternalField
        {
            [YamlInclude]
            public new string MyString = "NewDefaultValue";
        }

        public class ClassWithInternalProperty
        {
            internal string MyString { get; set; } = "DefaultValue";
        }

        public class ClassWithNewSlotProperty : ClassWithInternalProperty
        {
            public new string MyString { get; set; } = "NewDefaultValue";
        }

        public class ClassWithPublicProperty
        {
            public string MyString { get; set; } = "DefaultValue";
        }

        public class ClassWithNewSlotInternalProperty : ClassWithPublicProperty
        {
            internal new string MyString { get; set; } = "NewDefaultValue";
        }

        public class ClassWithPropertyNamingConflict
        {
            public string MyString { get; set; } = "DefaultValue";

            [YamlPropertyName(nameof(MyString))]
            internal string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassWithPropertyNamingConflictWhichThrows
        {
            public string MyString { get; set; } = "DefaultValue";

            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassWithPropertyFieldNamingConflictWhichThrows
        {
            public string MyString { get; set; } = "DefaultValue";

            [YamlInclude]
            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString = "ConflictingValue";
        }

        public class ClassInheritedWithPropertyNamingConflictWhichThrows : ClassWithPublicProperty
        {
            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassInheritedWithPropertyFieldNamingConflictWhichThrows : ClassWithPublicProperty
        {
            [YamlInclude]
            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString = "ConflictingValue";
        }

        public class ClassTwiceInheritedWithPropertyNamingConflictWhichThrowsDummy : ClassWithPublicProperty
        {
        }

        public class ClassTwiceInheritedWithPropertyNamingConflictWhichThrows : ClassTwiceInheritedWithPropertyNamingConflictWhichThrowsDummy
        {
            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassTwiceInheritedWithPropertyFieldNamingConflictWhichThrows : ClassTwiceInheritedWithPropertyNamingConflictWhichThrowsDummy
        {
            [YamlInclude]
            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString = "ConflictingValue";
        }

        public class ClassWithPropertyPolicyConflict
        {
            public string MyString { get; set; } = "DefaultValue";

            internal string myString { get; set; } = "ConflictingValue";
        }

        public class ClassWithPropertyPolicyConflictWhichThrows
        {
            public string MyString { get; set; } = "DefaultValue";

            public string myString { get; set; } = "ConflictingValue";
        }

        public class ClassWithPropertyFieldPolicyConflictWhichThrows
        {
            public string MyString { get; set; } = "DefaultValue";

            [YamlInclude]
            public string myString = "ConflictingValue";
        }

        public class ClassInheritedWithPropertyPolicyConflictWhichThrows : ClassWithPublicProperty
        {
            public string myString { get; set; } = "ConflictingValue";
        }

        public class ClassInheritedWithPropertyFieldPolicyConflictWhichThrows : ClassWithPublicProperty
        {
            [YamlInclude]
            public string myString = "ConflictingValue";
        }

        public class ClassInheritedWithPropertyPolicyConflictWhichThrowsDummy : ClassWithPublicProperty
        {
        }

        public class ClassTwiceInheritedWithPropertyPolicyConflictWhichThrows : ClassInheritedWithPropertyPolicyConflictWhichThrowsDummy
        {
            public string myString { get; set; } = "ConflictingValue";
        }

        public class ClassTwiceInheritedWithPropertyFieldPolicyConflictWhichThrows : ClassInheritedWithPropertyPolicyConflictWhichThrowsDummy
        {
            [YamlInclude]
            public string myString { get; set; } = "ConflictingValue";
        }

        public class ClassWithIgnoredNewSlotField : ClassWithInternalField
        {
            [YamlIgnore]
            public new string MyString = "NewDefaultValue";
        }

        public class ClassWithIgnoredNewSlotProperty : ClassWithInternalProperty
        {
            [YamlIgnore]
            public new string MyString { get; set; } = "NewDefaultValue";
        }

        public class ClassWithIgnoredPublicProperty
        {
            [YamlIgnore]
            public string MyString { get; set; } = "DefaultValue";
        }

        public class ClassWithObsoleteAndIgnoredProperty
        {
            [Obsolete("Src gen should not generate reference to getter or setter")]
            [YamlIgnore]
            public string MyString_Obsolete { get; set; } = "DefaultValue";
        }

        public class ClassWithIgnoredPublicPropertyAndNewSlotPrivate : ClassWithIgnoredPublicProperty
        {
            internal new string MyString { get; set; } = "NewDefaultValue";
        }

        public class ClassWithIgnoredPublicPropertyAndNewSlotPublicAndIgnoredToo : ClassWithIgnoredPublicProperty
        {
            [YamlIgnore]
            public new string MyString { get; set; } = "NewDefaultValue";
        }

        public class ClassWithIgnoredPropertyNamingConflictPrivate
        {
            [YamlIgnore]
            public string MyString { get; set; } = "DefaultValue";

            [YamlPropertyName(nameof(MyString))]
            internal string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassWithIgnoredPropertyPolicyConflictPrivate
        {
            [YamlIgnore]
            public string MyString { get; set; } = "DefaultValue";

            internal string myString { get; set; } = "ConflictingValue";
        }

        public class ClassWithIgnoredPropertyNamingConflictPublic
        {
            [YamlIgnore]
            public string MyString { get; set; } = "DefaultValue";

            [YamlPropertyName(nameof(MyString))]
            public string ConflictingString { get; set; } = "ConflictingValue";
        }

        public class ClassWithIgnoredPropertyPolicyConflictPublic
        {
            [YamlIgnore]
            public string MyString { get; set; } = "DefaultValue";

            public string myString { get; set; } = "ConflictingValue";
        }

        public class ClassWithHiddenByNewSlotIntProperty
        {
            public int MyNumeric { get; set; } = 1;
        }

        public class ClassWithNewSlotDecimalField : ClassWithHiddenByNewSlotIntProperty
        {
            [YamlInclude]
            public new decimal MyNumeric = 1.5M;
        }

        public class ClassWithNewSlotDecimalProperty : ClassWithHiddenByNewSlotIntProperty
        {
            public new decimal MyNumeric { get; set; } = 1.5M;
        }

        public class ClassWithNewSlotAttributedDecimalField : ClassWithHiddenByNewSlotIntProperty
        {
            [YamlInclude]
            [YamlPropertyName("MyNewNumeric")]
            public new decimal MyNumeric = 1.5M;
        }

        public class ClassWithNewSlotAttributedDecimalProperty : ClassWithHiddenByNewSlotIntProperty
        {
            [YamlPropertyName("MyNewNumeric")]
            public new decimal MyNumeric { get; set; } = 1.5M;
        }

        public class Class_With_VirtualProperty
        {
            public virtual bool MyProp { get; set; }
        }

        public class DerivedClass_With_IgnoredOverride : Class_With_VirtualProperty
        {
            [YamlIgnore]
            public override bool MyProp { get; set; }
        }

        public class DerivedClass_WithVisibleProperty_Of_DerivedClass_With_IgnoredOverride : DerivedClass_With_IgnoredOverride
        {
            public override bool MyProp { get; set; }
        }

        public class DerivedClass_With_IgnoredOverride_And_ConflictingPropertyName : Class_With_VirtualProperty
        {
            [YamlPropertyName("MyProp")]
            public string? MyString { get; set; }

            [YamlIgnore]
            public override bool MyProp { get; set; }
        }

        public class Class_With_Property
        {
            public bool MyProp { get; set; }
        }

        public class DerivedClass_With_Ignored_NewProperty : Class_With_Property
        {
            [YamlIgnore]
            public new bool MyProp { get; set; }
        }

        public class DerivedClass_With_NewProperty_And_ConflictingPropertyName : Class_With_Property
        {
            [YamlPropertyName("MyProp")]
            public string? MyString { get; set; }

            [YamlIgnore]
            public new bool MyProp { get; set; }
        }

        public class DerivedClass_With_Ignored_NewProperty_Of_DifferentType : Class_With_Property
        {
            [YamlIgnore]
            public new int MyProp { get; set; }
        }

        public class DerivedClass_With_Ignored_NewProperty_Of_DifferentType_And_ConflictingPropertyName : Class_With_Property
        {
            [YamlPropertyName("MyProp")]
            public string? MyString { get; set; }

            [YamlIgnore]
            public new int MyProp { get; set; }
        }

        public class DerivedClass_WithIgnoredOverride : Class_With_VirtualProperty
        {
            [YamlIgnore]
            public override bool MyProp { get; set; }
        }

        public class DerivedClass_WithConflictingNewMember : Class_With_VirtualProperty
        {
            public new bool MyProp { get; set; }
        }

        public class DerivedClass_WithConflictingNewMember_Of_DifferentType : Class_With_VirtualProperty
        {
            public new int MyProp { get; set; }
        }

        public class DerivedClass_With_Ignored_ConflictingNewMember : Class_With_VirtualProperty
        {
            [YamlIgnore]
            public new bool MyProp { get; set; }
        }

        public class DerivedClass_With_Ignored_ConflictingNewMember_Of_DifferentType : Class_With_VirtualProperty
        {
            [YamlIgnore]
            public new int MyProp { get; set; }
        }

        public class FurtherDerivedClass_With_ConflictingPropertyName : DerivedClass_WithIgnoredOverride
        {
            [YamlPropertyName("MyProp")]
            public string? MyString { get; set; }
        }

        public class DerivedClass_WithConflictingPropertyName : Class_With_VirtualProperty
        {
            [YamlPropertyName("MyProp")]
            public string? MyString { get; set; }
        }

        public class FurtherDerivedClass_With_IgnoredOverride : DerivedClass_WithConflictingPropertyName
        {
            [YamlIgnore]
            public override bool MyProp { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreReadOnlyProperties()
        {
            var options = new YamlSerializerOptions();
            options.IgnoreReadOnlyProperties = true;

            var obj = new ClassWithNoSetter();

            string yaml = await Serializer.SerializeWrapper(obj, options);

            // Collections are always serialized unless they have [YamlIgnore].
            Assert.Equal("""{"MyInts":[1,2]}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreReadOnlyFields()
        {
            var options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.IgnoreReadOnlyFields = true;

            var obj = new ClassWithReadOnlyFields();

            string yaml = await Serializer.SerializeWrapper(obj, options);

            // Collections are always serialized unless they have [YamlIgnore].
            Assert.Equal("""{"MyInts":[1,2]}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NoSetter()
        {
            var obj = new ClassWithNoSetter();

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "MyString":"DefaultValue"
                """, yaml);
            Assert.Contains("""
                "MyInts":[1,2]
                """, yaml);

            obj = await Serializer.DeserializeWrapper<ClassWithNoSetter>("""{"MyString":"IgnoreMe","MyInts":[0]}""");
            Assert.Equal("DefaultValue", obj.MyString);
            Assert.Equal(2, obj.MyInts.Length);
        }

        [Fact]
        public async Task NoGetter()
        {
            ClassWithNoGetter objWithNoGetter = await Serializer.DeserializeWrapper<ClassWithNoGetter>(
                """{"MyString":"Hello","MyIntArray":[0],"MyIntList":[0]}""");

            Assert.Equal("Hello", objWithNoGetter.GetMyString());

            // Currently we don't support setters without getters.
            Assert.Equal(0, objWithNoGetter.GetMyIntArray().Length);
            Assert.Equal(0, objWithNoGetter.GetMyIntList().Count);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PrivateGetter()
        {
            var obj = new ClassWithPrivateSetterAndGetter();
            obj.SetMyString("Hello");

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        public async Task PrivateSetter()
        {
            string yaml = """{"MyString":"Hello"}""";

            ClassWithPrivateSetterAndGetter objCopy = await Serializer.DeserializeWrapper<ClassWithPrivateSetterAndGetter>(yaml);
            Assert.Null(objCopy.GetMyString());
        }

        [Fact]
        public async Task PrivateSetterPublicGetter()
        {
            // https://github.com/dotnet/runtime/issues/29503
            ClassWithPublicGetterAndPrivateSetter obj
                = await Serializer.DeserializeWrapper<ClassWithPublicGetterAndPrivateSetter>("""{ "Class": {} }""");

            Assert.NotNull(obj);
            Assert.Null(obj.Class);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ProtectedMembers()
        {
            var options = Serializer.CreateOptions(includeFields: true);
            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(ClassWithProtectedMembers));
            Assert.Empty(typeInfo.Properties);

            var value = new ClassWithProtectedMembers();
            value.SetValues(field: 10, property: 20);

            string yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithProtectedMembers>("""{"_field":10,"Property":20}""");
            value.GetValues(out int field, out int property);
            Assert.Equal(0, field);
            Assert.Equal(0, property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ProtectedGetter()
        {
            var value = new ClassWithProtectedGetter { Property = 42 };

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithProtectedGetter>("""{"Property":42}""");
            Assert.Equal(42, value.GetValue());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ProtectedSetter()
        {
            var value = new ClassWithProtectedSetter();
            value.SetValue(42);

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"Property":42}""", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithProtectedSetter>(yaml);
            Assert.Equal(0, value.Property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PrivateProtectedMembers()
        {
            var options = Serializer.CreateOptions(includeFields: true);
            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(ClassWithPrivateProtectedMembers));
            Assert.Empty(typeInfo.Properties);

            var value = new ClassWithPrivateProtectedMembers();
            value.SetValues(field: 10, property: 20);

            string yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithPrivateProtectedMembers>("""{"_field":10,"Property":20}""");
            value.GetValues(out int field, out int property);
            Assert.Equal(0, field);
            Assert.Equal(0, property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PrivateProtectedGetter()
        {
            var value = new ClassWithPrivateProtectedGetter { Property = 42 };

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithPrivateProtectedGetter>("""{"Property":42}""");
            Assert.Equal(42, value.GetValue());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PrivateProtectedSetter()
        {
            var value = new ClassWithPrivateProtectedSetter();
            value.SetValue(42);

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"Property":42}""", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithPrivateProtectedSetter>(yaml);
            Assert.Equal(0, value.Property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task InternalProtectedMembers()
        {
            var options = Serializer.CreateOptions(includeFields: true);
            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(ClassWithInternalProtectedMembers));
            Assert.Empty(typeInfo.Properties);

            var value = new ClassWithInternalProtectedMembers();
            value.SetValues(field: 10, property: 20);

            string yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithInternalProtectedMembers>("""{"_field":10,"Property":20}""");
            value.GetValues(out int field, out int property);
            Assert.Equal(0, field);
            Assert.Equal(0, property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task InternalProtectedGetter()
        {
            var value = new ClassWithInternalProtectedGetter { Property = 42 };

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("{}", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithInternalProtectedGetter>("""{"Property":42}""");
            Assert.Equal(42, value.GetValue());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task InternalProtectedSetter()
        {
            var value = new ClassWithInternalProtectedSetter();
            value.SetValue(42);

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"Property":42}""", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithInternalProtectedSetter>(yaml);
            Assert.Equal(0, value.Property);
        }

        [Fact]
        public async Task MissingObjectProperty()
        {
            ClassWithMissingObjectProperty obj
                = await Serializer.DeserializeWrapper<ClassWithMissingObjectProperty>("""{ "Object": {} }""");

            Assert.Null(obj.Collection);
        }

        [Fact]
        public async Task MissingCollectionProperty()
        {
            ClassWithMissingCollectionProperty obj
                = await Serializer.DeserializeWrapper<ClassWithMissingCollectionProperty>("""{ "Collection": [] }""");

            Assert.Null(obj.Object);
        }

        public class ClassWithPublicGetterAndPrivateSetter
        {
            public NestedClass Class { get; private set; }
        }

        public class NestedClass
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreAttribute()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };

            // Verify default state.
            var obj = new ClassWithIgnoreAttributeProperty();
            Assert.Equal(@"MyString", obj.MyString);
            Assert.Equal(@"MyStringWithIgnore", obj.MyStringWithIgnore);
            Assert.Equal(2, obj.MyStringsWithIgnore.Length);
            Assert.Equal(1, obj.MyDictionaryWithIgnore["Key"]);
            Assert.Equal(3.14M, obj.MyNumeric);

            // Verify serialize.
            string yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("""
                "MyString"
                """, yaml);
            Assert.DoesNotContain(@"MyStringWithIgnore", yaml);
            Assert.DoesNotContain(@"MyStringsWithIgnore", yaml);
            Assert.DoesNotContain(@"MyDictionaryWithIgnore", yaml);
            Assert.DoesNotContain(@"MyNumeric", yaml);

            // Verify deserialize default.
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoreAttributeProperty>("{}", options);
            Assert.Equal(@"MyString", obj.MyString);
            Assert.Equal(@"MyStringWithIgnore", obj.MyStringWithIgnore);
            Assert.Equal(2, obj.MyStringsWithIgnore.Length);
            Assert.Equal(1, obj.MyDictionaryWithIgnore["Key"]);
            Assert.Equal(3.14M, obj.MyNumeric);

            // Verify deserialize ignores the json for MyStringWithIgnore and MyStringsWithIgnore.
            obj = await Serializer.DeserializeWrapper<ClassWithIgnoreAttributeProperty>(
                """{"MyString":"Hello", "MyStringWithIgnore":"IgnoreMe", "MyStringsWithIgnore":["IgnoreMe"], "MyDictionaryWithIgnore":{"Key":9}, "MyNumeric": 2.71828}""", options);
            Assert.Contains(@"Hello", obj.MyString);
            Assert.Equal(@"MyStringWithIgnore", obj.MyStringWithIgnore);
            Assert.Equal(2, obj.MyStringsWithIgnore.Length);
            Assert.Equal(1, obj.MyDictionaryWithIgnore["Key"]);
            Assert.Equal(3.14M, obj.MyNumeric);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreAttribute_UnsupportedBigInteger()
        {
            string yaml = """{"MyBigInteger":1}""";
            string wrapperYaml = """{"MyClass":{"MyBigInteger":1}}""";

            // Unsupported types will throw by default.
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithUnsupportedBigInteger>(yaml));
            // Using new options instance to prevent using previously cached metadata.
            YamlSerializerOptions options = new YamlSerializerOptions();
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<WrapperForClassWithUnsupportedBigInteger>(wrapperYaml, options));

            // When ignored, we can serialize and deserialize without exceptions.
            options = new YamlSerializerOptions();
            ClassWithIgnoredUnsupportedBigInteger obj = await Serializer.DeserializeWrapper<ClassWithIgnoredUnsupportedBigInteger>(yaml, options);
            Assert.Null(obj.MyBigInteger);

            options = new YamlSerializerOptions();
            Assert.Equal("{}", await Serializer.SerializeWrapper(new ClassWithIgnoredUnsupportedBigInteger()));

            options = new YamlSerializerOptions();
            WrapperForClassWithIgnoredUnsupportedBigInteger wrapperObj = await Serializer.DeserializeWrapper<WrapperForClassWithIgnoredUnsupportedBigInteger>(wrapperYaml, options);
            Assert.Null(wrapperObj.MyClass.MyBigInteger);

            options = new YamlSerializerOptions();
            Assert.Equal("""{"MyClass":{}}""", await Serializer.SerializeWrapper(new WrapperForClassWithIgnoredUnsupportedBigInteger()
            {
                MyClass = new ClassWithIgnoredUnsupportedBigInteger(),
            }, options));
        }

        public class ObjectDictWrapper : Dictionary<int, string> { }

        public class ClassWithUnsupportedDictionary
        {
            public ConcurrentDictionary<object, object> MyConcurrentDict { get; set; }
            public IDictionary<object, object> MyIDict { get; set; }
            public ObjectDictWrapper MyDict { get; set; }
        }

        public class WrapperForClassWithUnsupportedDictionary
        {
            public ClassWithUnsupportedDictionary MyClass { get; set; } = new ClassWithUnsupportedDictionary();
        }

        public class ClassWithIgnoredUnsupportedDictionary
        {
            [YamlIgnore]
            public ConcurrentDictionary<object, object> MyConcurrentDict { get; set; }
            [YamlIgnore]
            public IDictionary<object, object> MyIDict { get; set; }
            [YamlIgnore]
            public ObjectDictWrapper MyDict { get; set; }
        }

        public class WrapperForClassWithIgnoredUnsupportedDictionary
        {
            public ClassWithIgnoredUnsupportedDictionary MyClass { get; set; }
        }

        public class ClassWithUnsupportedBigInteger
        {
            public BigInteger? MyBigInteger { get; set; }
        }

        public class WrapperForClassWithUnsupportedBigInteger
        {
            public ClassWithUnsupportedBigInteger MyClass { get; set; } = new();
        }

        public class ClassWithIgnoredUnsupportedBigInteger
        {
            [YamlIgnore]
            public BigInteger? MyBigInteger { get; set; }
        }

        public class WrapperForClassWithIgnoredUnsupportedBigInteger
        {
            public ClassWithIgnoredUnsupportedBigInteger MyClass { get; set; }
        }

        public class ClassWithMissingObjectProperty
        {
            public object[] Collection { get; set; }
        }

        public class ClassWithMissingCollectionProperty
        {
            public object Object { get; set; }
        }

        public class ClassWithPrivateSetterAndGetter
        {
            private string MyString { get; set; }

            public string GetMyString()
            {
                return MyString;
            }

            public void SetMyString(string value)
            {
                MyString = value;
            }
        }

        public class ClassWithProtectedMembers
        {
            protected int _field;
            protected int Property { get; set; }

            public void SetValues(int field, int property) => (_field, Property) = (field, property);
            public void GetValues(out int field, out int property) => (field, property) = (_field, Property);
        }

        public class ClassWithProtectedGetter
        {
            public int Property { protected get; set; }
            public int GetValue() => Property;
        }

        public class ClassWithProtectedSetter
        {
            public int Property { get; protected set; }
            public void SetValue(int value) => Property = value;
        }

        public class ClassWithPrivateProtectedMembers
        {
            private protected int _field;
            private protected int Property { get; set; }

            public void SetValues(int field, int property) => (_field, Property) = (field, property);
            public void GetValues(out int field, out int property) => (field, property) = (_field, Property);
        }

        public class ClassWithPrivateProtectedGetter
        {
            public int Property { private protected get; set; }
            public int GetValue() => Property;
        }

        public class ClassWithPrivateProtectedSetter
        {
            public int Property { get; private protected set; }
            public void SetValue(int value) => Property = value;
        }

        public class ClassWithInternalProtectedMembers
        {
            protected internal int _field;
            protected internal int Property { get; set; }

            public void SetValues(int field, int property) => (_field, Property) = (field, property);
            public void GetValues(out int field, out int property) => (field, property) = (_field, Property);
        }

        public class ClassWithInternalProtectedGetter
        {
            public int Property { internal protected get; set; }
            public int GetValue() => Property;
        }

        public class ClassWithInternalProtectedSetter
        {
            public int Property { get; internal protected set; }
            public void SetValue(int value) => Property = value;
        }

        public class ClassWithReadOnlyFields
        {
            public ClassWithReadOnlyFields()
            {
                MyString = "DefaultValue";
                MyInts = new int[] { 1, 2 };
            }

            public readonly string MyString;
            public readonly int[] MyInts;
        }

        public class ClassWithNoSetter
        {
            public ClassWithNoSetter()
            {
                MyString = "DefaultValue";
                MyInts = new int[] { 1, 2 };
            }

            public string MyString { get; }
            public int[] MyInts { get; }
        }

        public class ClassWithNoGetter
        {
            string _myString = "";
            int[] _myIntArray = new int[] { };
            List<int> _myIntList = new List<int> { };

            public string MyString
            {
                set
                {
                    _myString = value;
                }
            }

            public int[] MyIntArray
            {
                set
                {
                    _myIntArray = value;
                }
            }

            public List<int> MyList
            {
                set
                {
                    _myIntList = value;
                }
            }

            public string GetMyString()
            {
                return _myString;
            }

            public int[] GetMyIntArray()
            {
                return _myIntArray;
            }

            public List<int> GetMyIntList()
            {
                return _myIntList;
            }
        }

        public class ClassWithIgnoreAttributeProperty
        {
            public ClassWithIgnoreAttributeProperty()
            {
                MyDictionaryWithIgnore = new Dictionary<string, int> { { "Key", 1 } };
                MyString = "MyString";
                MyStringWithIgnore = "MyStringWithIgnore";
                MyStringsWithIgnore = new string[] { "1", "2" };
                MyNumeric = 3.14M;
            }

            [YamlIgnore]
            public Dictionary<string, int> MyDictionaryWithIgnore { get; set; }

            [YamlIgnore]
            public string MyStringWithIgnore { get; set; }

            public string MyString { get; set; }

            [YamlIgnore]
            public string[] MyStringsWithIgnore { get; set; }

            [YamlIgnore]
            public decimal MyNumeric;
        }

        public enum MyEnum
        {
            Case1 = 0,
            Case2 = 1,
        }

        public struct StructWithOverride
        {
            [YamlIgnore]
            public MyEnum EnumValue { get; set; }

            [YamlPropertyName("EnumValue")]
            public string EnumString
            {
                get => EnumValue.ToString();
                set
                {
                    if (value == "Case1")
                    {
                        EnumValue = MyEnum.Case1;
                    }
                    else if (value == "Case2")
                    {
                        EnumValue = MyEnum.Case2;
                    }
                    else
                    {
                        throw new Exception("Unknown value!");
                    }
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task OverrideYamlIgnorePropertyUsingYamlPropertyName()
        {
            const string yaml = """{"EnumValue":"Case2"}""";

            StructWithOverride obj = await Serializer.DeserializeWrapper<StructWithOverride>(yaml);

            Assert.Equal(MyEnum.Case2, obj.EnumValue);
            Assert.Equal("Case2", obj.EnumString);

            string yamlSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(yaml, yamlSerialized);
        }

        public struct ClassWithOverrideReversed
        {
            // Same as ClassWithOverride except the order of the properties is different, which should cause different reflection order.
            [YamlPropertyName("EnumValue")]
            public string EnumString
            {
                get => EnumValue.ToString();
                set
                {
                    if (value == "Case1")
                    {
                        EnumValue = MyEnum.Case1;
                    }
                    if (value == "Case2")
                    {
                        EnumValue = MyEnum.Case2;
                    }
                    else
                    {
                        throw new Exception("Unknown value!");
                    }
                }
            }

            [YamlIgnore]
            public MyEnum EnumValue { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task OverrideYamlIgnorePropertyUsingYamlPropertyNameReversed()
        {
            const string yaml = """{"EnumValue":"Case2"}""";

            ClassWithOverrideReversed obj = await Serializer.DeserializeWrapper<ClassWithOverrideReversed>(yaml);

            Assert.Equal(MyEnum.Case2, obj.EnumValue);
            Assert.Equal("Case2", obj.EnumString);

            string yamlSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Theory]
        [InlineData(typeof(ClassWithProperty_IgnoreConditionAlways))]
        [InlineData(typeof(ClassWithProperty_IgnoreConditionAlways_Ctor))]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreConditionSetToAlwaysWorks([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            string yaml = """{"MyString":"Random","MyDateTime":"2020-03-23","MyInt":4}""";

            object obj = await Serializer.DeserializeWrapper(yaml, type);
            Assert.Equal("Random", (string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(default, (DateTime)type.GetProperty("MyDateTime").GetValue(obj));
            Assert.Equal(4, (int)type.GetProperty("MyInt").GetValue(obj));

            string serialized = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "MyString":"Random"
                """, serialized);
            Assert.Contains("""
                "MyInt":4
                """, serialized);
            Assert.DoesNotContain("""
                "MyDateTime":
                """, serialized);
        }

        public class ClassWithProperty_IgnoreConditionAlways
        {
            public string MyString { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public DateTime MyDateTime { get; set; }
            public int MyInt { get; set; }
        }

        public class ClassWithProperty_IgnoreConditionAlways_Ctor
        {
            public string MyString { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public DateTime MyDateTime { get; }
            public int MyInt { get; }

            public ClassWithProperty_IgnoreConditionAlways_Ctor(DateTime myDateTime, int myInt)
            {
                MyDateTime = myDateTime;
                MyInt = myInt;
            }
        }

        [Theory]
        [MemberData(nameof(YamlIgnoreConditionWhenWritingDefault_ClassProperty_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreConditionWhenWritingDefault_ClassProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, YamlSerializerOptions options)
        {
            // Property shouldn't be ignored if it isn't null.
            string yaml = """{"Int1":1,"MyString":"Random","Int2":2}""";

            object obj = await Serializer.DeserializeWrapper(yaml, type, options);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Equal("Random", (string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            string serialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyString":"Random"
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);

            // Property should be ignored when null.
            yaml = """{"Int1":1,"MyString":null,"Int2":2}""";

            obj = await Serializer.DeserializeWrapper(yaml, type, options);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));

            Assert.Null((string)type.GetProperty("MyString").GetValue(obj));

            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            // Set property to be ignored to null.
            type.GetProperty("MyString").SetValue(obj, null);

            serialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);
            Assert.DoesNotContain("""
                "MyString":
                """, serialized);
        }

        public class ClassWithClassProperty_IgnoreConditionWhenWritingDefault
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string? MyString { get; set; } = "DefaultString";
            public int Int2 { get; set; }
        }

        public class ClassWithClassProperty_IgnoreConditionWhenWritingDefault_Ctor
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string MyString { get; set; } = "DefaultString";
            public int Int2 { get; set; }

            public ClassWithClassProperty_IgnoreConditionWhenWritingDefault_Ctor(string? myString)
            {
                if (myString != null)
                {
                    MyString = myString;
                }
            }
        }

        public static IEnumerable<object[]> YamlIgnoreConditionWhenWritingDefault_ClassProperty_TestData()
        {
            yield return new object[] { typeof(ClassWithClassProperty_IgnoreConditionWhenWritingDefault), new YamlSerializerOptions() };
        }

        [Theory]
        [MemberData(nameof(YamlIgnoreConditionWhenWritingDefault_StructProperty_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreConditionWhenWritingDefault_StructProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, YamlSerializerOptions options)
        {
            // Property shouldn't be ignored if it isn't null.
            string yaml = """{"Int1":1,"MyInt":3,"Int2":2}""";

            object obj = await Serializer.DeserializeWrapper(yaml, type, options);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Equal(3, (int)type.GetProperty("MyInt").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            string serialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyInt":3
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);

            // Null being assigned to non-nullable types is invalid.
            yaml = """{"Int1":1,"MyInt":null,"Int2":2}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper(yaml, type, options));
        }

        public class ClassWithStructProperty_IgnoreConditionWhenWritingDefault
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public int MyInt { get; set; }
            public int Int2 { get; set; }
        }

        public struct StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public int MyInt { get; }
            public int Int2 { get; set; }

            [YamlConstructor]
            public StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor(int myInt)
            {
                Int1 = 0;
                MyInt = myInt;
                Int2 = 0;
            }
        }

        public static IEnumerable<object[]> YamlIgnoreConditionWhenWritingDefault_StructProperty_TestData()
        {
            yield return new object[] { typeof(ClassWithStructProperty_IgnoreConditionWhenWritingDefault), new YamlSerializerOptions() };
            yield return new object[] { typeof(StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor), new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull } };
        }

        [Theory]
        [MemberData(nameof(YamlIgnoreConditionNever_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreConditionNever([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // Property should always be (de)serialized, even when null.
            string yaml = """{"Int1":1,"MyString":"Random","Int2":2}""";

            object obj = await Serializer.DeserializeWrapper(yaml, type);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Equal("Random", (string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            string serialized = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyString":"Random"
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);

            // Property should always be (de)serialized, even when null.
            yaml = """{"Int1":1,"MyString":null,"Int2":2}""";

            obj = await Serializer.DeserializeWrapper(yaml, type);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Null((string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            serialized = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyString":null
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);
        }

        [Theory]
        [MemberData(nameof(YamlIgnoreConditionNever_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreConditionNever_IgnoreNullValues_True([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            // Property should always be (de)serialized.
            string yaml = """{"Int1":1,"MyString":"Random","Int2":2}""";
            var options = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull };

            object obj = await Serializer.DeserializeWrapper(yaml, type, options);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Equal("Random", (string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            string serialized = await Serializer.SerializeWrapper(obj, type, options);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyString":"Random"
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);

            // Property should always be (de)serialized, even when null.
            yaml = """{"Int1":1,"MyString":null,"Int2":2}""";

            obj = await Serializer.DeserializeWrapper(yaml, type, options);
            Assert.Equal(1, (int)type.GetProperty("Int1").GetValue(obj));
            Assert.Null((string)type.GetProperty("MyString").GetValue(obj));
            Assert.Equal(2, (int)type.GetProperty("Int2").GetValue(obj));

            serialized = await Serializer.SerializeWrapper(obj, type, options);
            Assert.Contains("""
                "Int1":1
                """, serialized);
            Assert.Contains("""
                "MyString":null
                """, serialized);
            Assert.Contains("""
                "Int2":2
                """, serialized);
        }

        public class ClassWithStructProperty_IgnoreConditionNever
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? MyString { get; set; }
            public int Int2 { get; set; }
        }

        public class ClassWithStructProperty_IgnoreConditionNever_Ctor
        {
            public int Int1 { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? MyString { get; }
            public int Int2 { get; set; }

            public ClassWithStructProperty_IgnoreConditionNever_Ctor(string? myString)
            {
                MyString = myString;
            }
        }

        public static IEnumerable<object[]> YamlIgnoreConditionNever_TestData()
        {
            yield return new object[] { typeof(ClassWithStructProperty_IgnoreConditionNever) };
            yield return new object[] { typeof(ClassWithStructProperty_IgnoreConditionNever_Ctor) };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_WhenWritingNull()
        {
            var obj = new ClassWithTypeLevelIgnore_WhenWritingNull
            {
                MyString = null,
                MyInt = 42,
                MyOtherString = "hello"
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""MyInt"":42", yaml);
            Assert.Contains(@"""MyOtherString"":""hello""", yaml);
            Assert.DoesNotContain(@"""MyString"":", yaml);

            obj.MyString = "value";
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""MyString"":""value""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_WhenWritingDefault()
        {
            var obj = new ClassWithTypeLevelIgnore_WhenWritingDefault
            {
                MyString = null,
                MyInt = 0,
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.DoesNotContain(@"""MyString"":", yaml);
            Assert.DoesNotContain(@"""MyInt"":", yaml);

            obj.MyString = "value";
            obj.MyInt = 1;
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""MyString"":""value""", yaml);
            Assert.Contains(@"""MyInt"":1", yaml);
        }

        [Fact]
        public virtual async Task YamlIgnoreCondition_TypeLevel_Always_ThrowsInvalidOperation()
        {
            var obj = new ClassWithTypeLevelIgnore_Always
            {
                MyString = "value",
                MyInt = 42
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(obj));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithTypeLevelIgnore_Always>(@"{""MyString"":""value"",""MyInt"":42}"));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_PropertyOverridesType()
        {
            var obj = new ClassWithTypeLevelIgnore_PropertyOverride
            {
                MyString = null,
                MyInt = 42,
                AlwaysPresent = "test"
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            // MyString should be ignored (inherited WhenWritingNull, value is null)
            Assert.DoesNotContain(@"""MyString"":", yaml);
            // MyInt should be serialized (inherited WhenWritingNull doesn't apply to value types)
            Assert.Contains(@"""MyInt"":42", yaml);
            // AlwaysPresent has property-level [YamlIgnore(Condition = Never)] which overrides type-level
            Assert.Contains(@"""AlwaysPresent"":""test""", yaml);

            // When AlwaysPresent is null, it should still be present due to Never override
            obj.AlwaysPresent = null;
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""AlwaysPresent"":null", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_Struct()
        {
            var obj = new StructWithTypeLevelIgnore_WhenWritingNull
            {
                MyString = null,
                MyInt = 42,
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.DoesNotContain(@"""MyString"":", yaml);
            Assert.Contains(@"""MyInt"":42", yaml);
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
        public class ClassWithTypeLevelIgnore_WhenWritingNull
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
            public string? MyOtherString { get; set; }
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
        public class ClassWithTypeLevelIgnore_WhenWritingDefault
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
        public class ClassWithTypeLevelIgnore_Always
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
        public class ClassWithTypeLevelIgnore_PropertyOverride
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? AlwaysPresent { get; set; }
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
        public struct StructWithTypeLevelIgnore_WhenWritingNull
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_InheritedProperties()
        {
            var obj = new DerivedClassWithTypeLevelIgnore
            {
                BaseString = null,
                DerivedString = null,
                BaseInt = 42,
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            // Both BaseString and DerivedString are null so should be ignored (WhenWritingNull)
            Assert.DoesNotContain(@"""BaseString"":", yaml);
            Assert.DoesNotContain(@"""DerivedString"":", yaml);
            // BaseInt is a value type, WhenWritingNull doesn't apply
            Assert.Contains(@"""BaseInt"":42", yaml);

            obj.BaseString = "base";
            obj.DerivedString = "derived";
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""BaseString"":""base""", yaml);
            Assert.Contains(@"""DerivedString"":""derived""", yaml);
        }

        public class BaseClassWithProperties
        {
            public string? BaseString { get; set; }
            public int BaseInt { get; set; }
        }

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
        public class DerivedClassWithTypeLevelIgnore : BaseClassWithProperties
        {
            public string? DerivedString { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_TypeLevel_OverridesGlobalJSO()
        {
            // Global JSO says WhenWritingDefault (ignores null strings AND zero ints),
            // but the type-level attribute says WhenWritingNull (only ignores null strings).
            // The type-level attribute should override the global setting.
            var options = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault };
            var obj = new ClassWithTypeLevelIgnore_WhenWritingNull
            {
                MyString = null,
                MyInt = 0,
                MyOtherString = null
            };

            string yaml = await Serializer.SerializeWrapper(obj, options);
            // MyString and MyOtherString are null: both global (WhenWritingDefault) and type-level (WhenWritingNull) would ignore them.
            Assert.DoesNotContain(@"""MyString"":", yaml);
            Assert.DoesNotContain(@"""MyOtherString"":", yaml);
            // MyInt is 0: the global WhenWritingDefault would ignore it, but the type-level WhenWritingNull should override,
            // and WhenWritingNull doesn't apply to non-nullable value types so MyInt should still be serialized.
            Assert.Contains(@"""MyInt"":0", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassWithComplexObjectsUsingIgnoreWhenWritingDefaultAttribute()
        {
            string yaml = """{"Class":{"MyInt16":18}, "Dictionary":null}""";

            ClassUsingIgnoreWhenWritingDefaultAttribute obj = await Serializer.DeserializeWrapper<ClassUsingIgnoreWhenWritingDefaultAttribute>(yaml);

            // Class is deserialized.
            Assert.NotNull(obj.Class);
            Assert.Equal(18, obj.Class.MyInt16);

            // Dictionary is deserialized as YamlIgnoreCondition.WhenWritingDefault only applies to deserialization.
            Assert.Null(obj.Dictionary);

            obj = new ClassUsingIgnoreWhenWritingDefaultAttribute();
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("""{"Dictionary":{"Key":"Value"}}""", yaml);
        }

        public class ClassUsingIgnoreWhenWritingDefaultAttribute
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public SimpleTestClass? Class { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public Dictionary<string, string>? Dictionary { get; set; } = new Dictionary<string, string> { ["Key"] = "Value" };
        }

        public class ClassUsingIgnoreNeverAttribute
        {
            public SimpleTestClass Class { get; set; } = new SimpleTestClass { MyInt16 = 18 };

            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public Dictionary<string, string>? Dictionary { get; set; } = new Dictionary<string, string> { ["Key"] = "Value" };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreConditionNever_WinsOver_IgnoreReadOnlyProperties()
        {
            var options = new YamlSerializerOptions { IgnoreReadOnlyProperties = true };

            // Baseline
            string yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty("Hello"), options);
            Assert.Equal("{}", yaml);

            // With condition to never ignore
            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty_IgnoreNever("Hello"), options);
            Assert.Equal("""{"MyString":"Hello"}""", yaml);

            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty_IgnoreNever(null), options);
            Assert.Equal("""{"MyString":null}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreConditionWhenWritingDefault_WinsOver_IgnoreReadOnlyProperties()
        {
            var options = new YamlSerializerOptions { IgnoreReadOnlyProperties = true };

            // Baseline
            string yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty("Hello"), options);
            Assert.Equal("{}", yaml);

            // With condition to ignore when null
            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault("Hello"), options);
            Assert.Equal("""{"MyString":"Hello"}""", yaml);

            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault(null), options);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreConditionNever_WinsOver_IgnoreReadOnlyFields()
        {
            var options = new YamlSerializerOptions { IgnoreReadOnlyProperties = true };

            // Baseline
            string yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField("Hello"), options);
            Assert.Equal("{}", yaml);

            // With condition to never ignore
            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField_IgnoreNever("Hello"), options);
            Assert.Equal("""{"MyString":"Hello"}""", yaml);

            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField_IgnoreNever(null), options);
            Assert.Equal("""{"MyString":null}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreConditionWhenWritingDefault_WinsOver_IgnoreReadOnlyFields()
        {
            var options = new YamlSerializerOptions { IgnoreReadOnlyProperties = true };

            // Baseline
            string yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField("Hello"), options);
            Assert.Equal("{}", yaml);

            // With condition to ignore when null
            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField_IgnoreWhenWritingDefault("Hello"), options);
            Assert.Equal("""{"MyString":"Hello"}""", yaml);

            yaml = await Serializer.SerializeWrapper(new ClassWithReadOnlyStringField_IgnoreWhenWritingDefault(null), options);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWriting()
        {
            var yaml = await Serializer.SerializeWrapper
                (
                    new YamlIgnoreCondition_WhenReadingWritingTestModel { Age = 10, Name = "Mike" }
                );
            Assert.Equal("""{"Age":10}""", yaml);
        }

        [Fact]
        public async Task YamlIgnoreCondition_WhenReading()
        {
            var yaml = """{"Age":10, "Name":"Mike"}""";
            var model = await Serializer.DeserializeWrapper<YamlIgnoreCondition_WhenReadingWritingTestModel>(yaml);
            Assert.Equal("Mike", model.Name);
            Assert.Equal(0, model.Age);
        }

        public class YamlIgnoreCondition_WhenReadingWritingTestModel
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenReading)]
            public int Age { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWriting)]
            public string? Name { get; set; }
        }

        public class ClassWithReadOnlyStringProperty
        {
            public string MyString { get; }

            public ClassWithReadOnlyStringProperty(string myString) => MyString = myString;
        }

        public class ClassWithReadOnlyStringProperty_IgnoreNever
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? MyString { get; }

            public ClassWithReadOnlyStringProperty_IgnoreNever(string myString) => MyString = myString;
        }

        public class ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string MyString { get; }

            public ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault(string myString) => MyString = myString;
        }

        public class ClassWithReadOnlyStringField
        {
            public string MyString { get; }

            public ClassWithReadOnlyStringField(string myString) => MyString = myString;
        }

        public class ClassWithReadOnlyStringField_IgnoreNever
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? MyString { get; }

            public ClassWithReadOnlyStringField_IgnoreNever(string myString) => MyString = myString;
        }

        public class ClassWithReadOnlyStringField_IgnoreWhenWritingDefault
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string MyString { get; }

            public ClassWithReadOnlyStringField_IgnoreWhenWritingDefault(string myString) => MyString = myString;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NonPublicMembersAreNotIncluded()
        {
            Assert.Equal("{}", await Serializer.SerializeWrapper(new ClassWithNonPublicProperties()));

            string yaml = """{"MyInt":1,"MyString":"Hello","MyFloat":2,"MyDouble":3}""";
            var obj = await Serializer.DeserializeWrapper<ClassWithNonPublicProperties>(yaml);
            Assert.Equal(0, obj.MyInt);
            Assert.Null(obj.MyString);
            Assert.Equal(0, obj.GetMyFloat);
            Assert.Equal(0, obj.GetMyDouble);
        }

        public class ClassWithNonPublicProperties
        {
            internal int MyInt { get; set; }
            internal string MyString { get; private set; }
            internal float MyFloat { private get; set; }
            private double MyDouble { get; set; }

            internal float GetMyFloat => MyFloat;
            internal double GetMyDouble => MyDouble;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCondition_WhenWritingDefault_Globally_Works()
        {
            // Baseline - default values written.
            string expected = """{"MyString":null,"MyInt":0,"MyPoint":{"X":0,"Y":0}}""";
            var obj = new ClassWithProps();
            YamlTestHelper.AssertYamlEqual(expected, await Serializer.SerializeWrapper(obj));

            // Default values ignored when specified.
            Assert.Equal("{}", await Serializer.SerializeWrapper(obj, new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault }));
        }

        public class ClassWithProps
        {
            public string? MyString { get; set; }
            public int MyInt { get; set; }
            public Point_2D_Struct MyPoint { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCondition_WhenWritingDefault_PerProperty_Works()
        {
            // Default values ignored when specified.
            Assert.Equal("""{"MyInt":0}""", await Serializer.SerializeWrapper(new ClassWithPropsAndIgnoreAttributes()));
        }

        public class ClassWithPropsAndIgnoreAttributes
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public string? MyString { get; set; }
            public int MyInt { get; set; }
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public Point_2D_Struct MyPoint { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCondition_WhenWritingDefault_DoesNotApplyToCollections()
        {
            var list = new List<bool> { false, true };

            var options = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault };
            Assert.Equal("[false,true]", await Serializer.SerializeWrapper(list, options));
        }

        [Fact]
        public async Task IgnoreCondition_WhenWritingDefault_DoesNotApplyToDeserialization()
        {
            string yaml = """{"MyString":null,"MyInt":0,"MyPoint":{"X":0,"Y":0}}""";

            // Test - default values (both null and default for value types) are not ignored when using
            // YamlIgnoreCondition.WhenWritingDefault (as the option name implies)
            var options = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault };
            ClassWithInitializedProps obj = await Serializer.DeserializeWrapper<ClassWithInitializedProps>(yaml, options);
            Assert.Null(obj.MyString);
            Assert.Equal(0, obj.MyInt);
            Assert.Equal(0, obj.MyPoint.X);
            Assert.Equal(0, obj.MyPoint.X);
        }

        public class ClassWithInitializedProps
        {
            public string? MyString { get; set; } = "Default";
            public int MyInt { get; set; } = -1;
            public Point_2D_Struct MyPoint { get; set; } = new Point_2D_Struct(-1, -1);
        }

        public class ClassWithValueAndReferenceTypes
        {
            public string MyString { get; set; } = "Default";
            public int MyInt { get; set; } = -1;
            public bool? MyBool { get; set; } = true;
            public PointClass MyPointClass { get; set; } = new PointClass();
            public Point_2D_Struct MyPointStruct { get; set; } = new Point_2D_Struct(1, 2);
        }

        public struct LargeStructWithValueAndReferenceTypes
        {
            public string MyString { get; }
            public int MyInt { get; set; }
            public bool? MyBool { get; set; }
            public PointClass MyPointClass { get; set; }
            public Point_2D_Struct MyPointStruct { get; set; }

            [YamlConstructor]
            public LargeStructWithValueAndReferenceTypes(
                PointClass myPointClass,
                Point_2D_Struct myPointStruct,
                string myString = "Default",
                int myInt = -1,
                bool? myBool = true)
            {
                MyString = myString;
                MyInt = myInt;
                MyBool = myBool;
                MyPointClass = myPointClass;
                MyPointStruct = myPointStruct;
            }
        }

        public struct SmallStructWithValueAndReferenceTypes
        {
            public string MyString { get; }
            public int MyInt { get; set; }
            public bool? MyBool { get; set; }
            public Point_2D_Struct MyPointStruct { get; set; }

            [YamlConstructor]
            public SmallStructWithValueAndReferenceTypes(
                Point_2D_Struct myPointStruct,
                string myString = "Default",
                int myInt = -1,
                bool? myBool = true)
            {
                MyString = myString;
                MyInt = myInt;
                MyBool = myBool;
                MyPointStruct = myPointStruct;
            }
        }

        public class PointClass { }

        [Fact]
        public async Task Ignore_WhenWritingNull_Globally()
        {
            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
                IncludeFields = true
            };

            string yaml = """
                {
                "MyPointClass2_IgnoredWhenWritingNull":{},
                "MyString1_IgnoredWhenWritingNull":"Default",
                "MyNullableBool1_IgnoredWhenWritingNull":null,
                "MyInt2":0,
                "MyPointStruct2":{"X":1,"Y":2},
                "MyInt1":1,
                "MyString2_IgnoredWhenWritingNull":null,
                "MyPointClass1_IgnoredWhenWritingNull":null,
                "MyNullableBool2_IgnoredWhenWritingNull":true,
                "MyPointStruct1":{"X":0,"Y":0}
                }
                """;

            // All members should correspond to JSON contents, as ignore doesn't apply to deserialization.
            ClassWithThingsToIgnore obj = await Serializer.DeserializeWrapper<ClassWithThingsToIgnore>(yaml, options);
            Assert.NotNull(obj.MyPointClass2_IgnoredWhenWritingNull);
            Assert.Equal("Default", obj.MyString1_IgnoredWhenWritingNull);
            Assert.Null(obj.MyNullableBool1_IgnoredWhenWritingNull);
            Assert.Equal(0, obj.MyInt2);
            Assert.Equal(1, obj.MyPointStruct2.X);
            Assert.Equal(2, obj.MyPointStruct2.Y);
            Assert.Equal(1, obj.MyInt1);
            Assert.Null(obj.MyString2_IgnoredWhenWritingNull);
            Assert.Null(obj.MyPointClass1_IgnoredWhenWritingNull);
            Assert.True(obj.MyNullableBool2_IgnoredWhenWritingNull);
            Assert.Equal(0, obj.MyPointStruct1.X);
            Assert.Equal(0, obj.MyPointStruct1.Y);

            // Ignore null as appropriate during serialization.
            string expectedYaml = """
                {
                "MyPointClass2_IgnoredWhenWritingNull":{},
                "MyString1_IgnoredWhenWritingNull":"Default",
                "MyInt2":0,
                "MyPointStruct2":{"X":1,"Y":2},
                "MyInt1":1,
                "MyNullableBool2_IgnoredWhenWritingNull":true,
                "MyPointStruct1":{"X":0,"Y":0}
                }
                """;
            YamlTestHelper.AssertYamlEqual(expectedYaml, await Serializer.SerializeWrapper(obj, options));
        }

        public class ClassWithThingsToIgnore
        {
            public string MyString1_IgnoredWhenWritingNull { get; set; }

            public string? MyString2_IgnoredWhenWritingNull;

            public int MyInt1;

            public int MyInt2 { get; set; }

            public bool? MyNullableBool1_IgnoredWhenWritingNull { get; set; }

            public bool? MyNullableBool2_IgnoredWhenWritingNull;

            public PointClass? MyPointClass1_IgnoredWhenWritingNull;

            public PointClass MyPointClass2_IgnoredWhenWritingNull { get; set; }

            public Point_2D_Struct_WithAttribute MyPointStruct1;

            public Point_2D_Struct_WithAttribute MyPointStruct2 { get; set; }
        }

        [Fact]
        public async Task Ignore_WhenWritingNull_PerProperty()
        {
            string yaml = """
                {
                "MyPointClass2_IgnoredWhenWritingNull":{},
                "MyString1_IgnoredWhenWritingNull":"Default",
                "MyNullableBool1_IgnoredWhenWritingNull":null,
                "MyInt2":0,
                "MyPointStruct2":{"X":1,"Y":2},
                "MyInt1":1,
                "MyString2_IgnoredWhenWritingNull":null,
                "MyPointClass1_IgnoredWhenWritingNull":null,
                "MyNullableBool2_IgnoredWhenWritingNull":true,
                "MyPointStruct1":{"X":0,"Y":0}
                }
                """;

            // All members should correspond to JSON contents, as ignore doesn't apply to deserialization.
            ClassWithThingsToIgnore_PerProperty obj = await Serializer.DeserializeWrapper<ClassWithThingsToIgnore_PerProperty>(yaml);
            Assert.NotNull(obj.MyPointClass2_IgnoredWhenWritingNull);
            Assert.Equal("Default", obj.MyString1_IgnoredWhenWritingNull);
            Assert.Null(obj.MyNullableBool1_IgnoredWhenWritingNull);
            Assert.Equal(0, obj.MyInt2);
            Assert.Equal(1, obj.MyPointStruct2.X);
            Assert.Equal(2, obj.MyPointStruct2.Y);
            Assert.Equal(1, obj.MyInt1);
            Assert.Null(obj.MyString2_IgnoredWhenWritingNull);
            Assert.Null(obj.MyPointClass1_IgnoredWhenWritingNull);
            Assert.True(obj.MyNullableBool2_IgnoredWhenWritingNull);
            Assert.Equal(0, obj.MyPointStruct1.X);
            Assert.Equal(0, obj.MyPointStruct1.Y);

            // Ignore null as appropriate during serialization.
            string expectedYaml = """
                {
                "MyPointClass2_IgnoredWhenWritingNull":{},
                "MyString1_IgnoredWhenWritingNull":"Default",
                "MyInt2":0,
                "MyPointStruct2":{"X":1,"Y":2},
                "MyInt1":1,
                "MyNullableBool2_IgnoredWhenWritingNull":true,
                "MyPointStruct1":{"X":0,"Y":0}
                }
                """;
            YamlTestHelper.AssertYamlEqual(expectedYaml, await Serializer.SerializeWrapper(obj));
        }

        public class ClassWithThingsToIgnore_PerProperty
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public string MyString1_IgnoredWhenWritingNull { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public string? MyString2_IgnoredWhenWritingNull;

            [YamlInclude]
            public int MyInt1;

            public int MyInt2 { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public bool? MyNullableBool1_IgnoredWhenWritingNull { get; set; }

            [YamlInclude, YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public bool? MyNullableBool2_IgnoredWhenWritingNull;

            [YamlInclude, YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public PointClass? MyPointClass1_IgnoredWhenWritingNull;

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public PointClass MyPointClass2_IgnoredWhenWritingNull { get; set; }

            [YamlInclude]
            public Point_2D_Struct_WithAttribute MyPointStruct1;

            public Point_2D_Struct_WithAttribute MyPointStruct2 { get; set; }
        }

        [Theory]
        [InlineData(typeof(ClassWithBadIgnoreAttribute))]
        [InlineData(typeof(StructWithBadIgnoreAttribute))]
        public virtual async Task YamlIgnoreCondition_WhenWritingNull_OnValueType_Fail([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper("{}", type));
            string exAsStr = ex.ToString();
            Assert.Contains("YamlIgnoreCondition.WhenWritingNull", exAsStr);
            Assert.Contains("MyBadMember", exAsStr);
            Assert.Contains(type.ToString(), exAsStr);
            Assert.Contains("YamlIgnoreCondition.WhenWritingDefault", exAsStr);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(Activator.CreateInstance(type), type));
            exAsStr = ex.ToString();
            Assert.Contains("YamlIgnoreCondition.WhenWritingNull", exAsStr);
            Assert.Contains("MyBadMember", exAsStr);
            Assert.Contains(type.ToString(), exAsStr);
            Assert.Contains("YamlIgnoreCondition.WhenWritingDefault", exAsStr);
        }

        [Theory]
        [InlineData(typeof(ClassWithBadIgnoreAttribute))]
        [InlineData(typeof(StructWithBadIgnoreAttribute))]
        public virtual async Task YamlIgnoreCondition_WhenWritingNull_OnValueType_Fail_EmptyYaml([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper("", type));
            string exAsStr = ex.ToString();
            Assert.Contains("YamlIgnoreCondition.WhenWritingNull", exAsStr);
            Assert.Contains("MyBadMember", exAsStr);
            Assert.Contains(type.ToString(), exAsStr);
            Assert.Contains("YamlIgnoreCondition.WhenWritingDefault", exAsStr);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(Activator.CreateInstance(type)));
            exAsStr = ex.ToString();
            Assert.Contains("YamlIgnoreCondition.WhenWritingNull", exAsStr);
            Assert.Contains("MyBadMember", exAsStr);
            Assert.Contains(type.ToString(), exAsStr);
            Assert.Contains("YamlIgnoreCondition.WhenWritingDefault", exAsStr);
        }

        public class ClassWithBadIgnoreAttribute
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public int MyBadMember { get; set; }
        }

        public struct StructWithBadIgnoreAttribute
        {
            [YamlInclude]
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public Point_2D_Struct MyBadMember { get; set; }
        }

        public interface IUseCustomConverter { }

        [YamlConverter(typeof(MyCustomConverter))]
        public struct MyValueTypeWithProperties : IUseCustomConverter
        {
            public int PrimitiveValue { get; set; }
            public object RefValue { get; set; }
        }

        public class MyCustomConverter : YamlConverter<IUseCustomConverter>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(IUseCustomConverter).IsAssignableFrom(typeToConvert);
            }

            public override IUseCustomConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, IUseCustomConverter value, YamlSerializerOptions options)
            {
                MyValueTypeWithProperties obj = (MyValueTypeWithProperties)value;
                writer.WriteNumberValue(obj.PrimitiveValue + 100);
                // Ignore obj.RefValue
            }
        }

        public class MyClassWithValueType
        {
            public MyClassWithValueType() { }

            public MyValueTypeWithProperties Value { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWritingDefault_OnValueTypeWithCustomConverter()
        {
            var obj = new MyClassWithValueType();

            // Baseline without custom options.
            Assert.True(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"Value\":100}", yaml);

            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault
            };

            // Verify ignored.
            Assert.True(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{}", yaml);

            // Change a primitive value so it's no longer a default value.
            obj.Value = new MyValueTypeWithProperties { PrimitiveValue = 1 };
            Assert.False(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"Value\":101}", yaml);

            // Change reference value so it's no longer a default value.
            obj.Value = new MyValueTypeWithProperties { RefValue = 1 };
            Assert.False(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"Value\":100}", yaml);
        }

        [Fact]
        public async Task YamlIgnoreCondition_ConverterCalledOnDeserialize()
        {
            // Verify converter is called.
            await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.DeserializeWrapper<MyValueTypeWithProperties>("{}"));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWritingNull_OnValueTypeWithCustomConverter()
        {
            string yaml;
            var obj = new MyClassWithValueType();

            // Baseline without custom options.
            Assert.True(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"Value\":100}", yaml);

            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull
            };

            // Verify not ignored; MyValueTypeWithProperties is not null.
            Assert.True(EqualityComparer<MyValueTypeWithProperties>.Default.Equals(default, obj.Value));
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"Value\":100}", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWritingDefault_OnRootTypes()
        {
            string yaml;
            int i = 0;
            object obj = null;

            // Baseline without custom options.
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("null", yaml);

            yaml = await Serializer.SerializeWrapper(i);
            Assert.Equal("0", yaml);

            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault
            };

            // We don't ignore when applied to root types; only properties.

            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("null", yaml);

            yaml = await Serializer.SerializeWrapper(i, options);
            Assert.Equal("0", yaml);
        }

        public struct MyValueTypeWithBoxedPrimitive
        {
            public object BoxedPrimitive { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWritingDefault_OnBoxedPrimitive()
        {
            string yaml;

            MyValueTypeWithBoxedPrimitive obj = new MyValueTypeWithBoxedPrimitive { BoxedPrimitive = 0 };

            // Baseline without custom options.
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"BoxedPrimitive\":0}", yaml);

            var options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault
            };

            // No check if the boxed object's value type is a default value (0 in this case).
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"BoxedPrimitive\":0}", yaml);

            obj = new MyValueTypeWithBoxedPrimitive();
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{}", yaml);
        }

        public class MyClassWithValueTypeInterfaceProperty
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public IInterface? MyProp { get; set; }

            public interface IInterface { }
            public struct MyStruct : IInterface { }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_WhenWritingDefault_OnInterface()
        {
            // MyProp should be ignored due to [YamlIgnore].
            var obj = new MyClassWithValueTypeInterfaceProperty();
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{}", yaml);

            // No check if the interface property's value type is a default value.
            obj = new MyClassWithValueTypeInterfaceProperty { MyProp = new MyClassWithValueTypeInterfaceProperty.MyStruct() };
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"MyProp\":{}}", yaml);
        }

        public class ConcreteDerivedClass : AbstractBaseClass
        {
            // Ignored including on base class:
            [YamlIgnore] public override int Abstract_Ignored_Property { get; set; }
            [YamlIgnore] public override int Virtual_Ignored_Property { get; set; }

            // Ignored but not specified on base class:
            [YamlIgnore] public override int Abstract_IgnoredOnConcrete_Property { get; set; }
            [YamlIgnore] public override int Virtual_IgnoredOnConcrete_Property { get; set; }

            // Ignored specified on base class:
            [YamlPropertyOrder(1)] public override int Abstract_IgnoredOnBase_Property { get; set; }
            [YamlPropertyOrder(2)] public override int Virtual_IgnoredOnBase_Property { get; set; }

            // Standard overrides (not ignored):
            [YamlPropertyOrder(3)] public override int Abstract_Property { get; set; }
            [YamlPropertyOrder(4)] public override int Virtual_Property { get; set; }
        }

        public abstract class AbstractBaseClass
        {
            [YamlIgnore] public abstract int Abstract_Ignored_Property { get; set; }
            [YamlIgnore] public virtual int Virtual_Ignored_Property { get; set; }

            public abstract int Abstract_IgnoredOnConcrete_Property { get; set; }
            public virtual int Virtual_IgnoredOnConcrete_Property { get; set; }

            [YamlIgnore] public abstract int Abstract_IgnoredOnBase_Property { get; set; }
            [YamlIgnore] public virtual int Virtual_IgnoredOnBase_Property { get; set; }

            public abstract int Abstract_Property { get; set; }
            public virtual int Virtual_Property { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlIgnoreCondition_Polymorphic()
        {
            ConcreteDerivedClass obj = new()
            {
                Abstract_Ignored_Property = -1,
                Virtual_Ignored_Property = -1,
                Abstract_IgnoredOnConcrete_Property = -1,
                Virtual_IgnoredOnConcrete_Property = -1,
                Abstract_IgnoredOnBase_Property = 1,
                Virtual_IgnoredOnBase_Property = 2,
                Abstract_Property = 3,
                Virtual_Property = 4,
            };

            // Verify properties work as expected.
            Assert.Equal(-1, obj.Abstract_Ignored_Property);
            Assert.Equal(-1, obj.Virtual_Ignored_Property);
            Assert.Equal(-1, obj.Abstract_IgnoredOnConcrete_Property);
            Assert.Equal(-1, obj.Virtual_IgnoredOnConcrete_Property);
            Assert.Equal(1, obj.Abstract_IgnoredOnBase_Property);
            Assert.Equal(2, obj.Virtual_IgnoredOnBase_Property);
            Assert.Equal(3, obj.Abstract_Property);
            Assert.Equal(4, obj.Virtual_Property);

            const string ExpectedYaml = "{" +
                "\"Abstract_IgnoredOnBase_Property\":1," +
                "\"Virtual_IgnoredOnBase_Property\":2," +
                "\"Abstract_Property\":3," +
                "\"Virtual_Property\":4}";

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(ExpectedYaml, yaml);

            const string Yaml = "{" +
                "\"Abstract_Ignored_Property\":-1," +
                "\"Virtual_Ignored_Property\":-1," +
                "\"Abstract_IgnoredOnConcrete_Property\":-1," +
                "\"Virtual_IgnoredOnConcrete_Property\":-1," +
                "\"Abstract_IgnoredOnBase_Property\":1," +
                "\"Virtual_IgnoredOnBase_Property\":2," +
                "\"Abstract_Property\":3," +
                "\"Virtual_Property\":4}";

            obj = await Serializer.DeserializeWrapper<ConcreteDerivedClass>(Yaml);
            Assert.Equal(0, obj.Abstract_Ignored_Property);
            Assert.Equal(0, obj.Virtual_Ignored_Property);
            Assert.Equal(0, obj.Abstract_IgnoredOnConcrete_Property);
            Assert.Equal(0, obj.Virtual_IgnoredOnConcrete_Property);
            Assert.Equal(1, obj.Abstract_IgnoredOnBase_Property);
            Assert.Equal(2, obj.Virtual_IgnoredOnBase_Property);
            Assert.Equal(3, obj.Abstract_Property);
            Assert.Equal(4, obj.Virtual_Property);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializationMetadataNotComputedWhenMemberIgnored()
        {
            string janePayload = """{"Name":"Jane Doe"}""";

#if !BUILDING_SOURCE_GENERATOR_TESTS
            // Without [YamlIgnore], serializer throws exceptions due to runtime-reflection-based property metadata inspection.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new TypeWith_RefStringProp()));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<TypeWith_RefStringProp>("{}"));

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new TypeWith_PropWith_BadConverter()));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<TypeWith_PropWith_BadConverter>("{}"));
#else
            // Ref returns supported in source-gen mode
            string expected = """{"NameRef":"John Doe","Name":"John Doe"}""";
            YamlTestHelper.AssertYamlEqual(expected, await Serializer.SerializeWrapper(new TypeWith_RefStringProp()));

            var obj = await Serializer.DeserializeWrapper<TypeWith_RefStringProp>(janePayload);
            Assert.Equal("Jane Doe", obj.Name);
            Assert.Equal("Jane Doe", obj.NameRef);

            var obj2 = new TypeWith_PropWith_BadConverter();
            obj2.Property = "Hello";

            // Invalid converter specified, fallback to built-in converter. This should be corrected.
            // https://github.com/dotnet/runtime/issues/60020.

            Assert.Equal("""{"Property":"Hello"}""", await Serializer.SerializeWrapper(obj2));

            obj2 = await Serializer.DeserializeWrapper<TypeWith_PropWith_BadConverter>("""{"Property":"World"}""");
            Assert.Equal("World", obj2.Property);
#endif

            // With [YamlIgnore], serializer skips property metadata inspection
            Assert.Equal("""{"Name":"John Doe"}""", await Serializer.SerializeWrapper(new TypeWith_IgnoredRefStringProp()));
            Assert.Equal("Jane Doe", (await Serializer.DeserializeWrapper<TypeWith_IgnoredRefStringProp>(janePayload)).Name);

            Assert.Equal("{}", await Serializer.SerializeWrapper(new TypeWith_IgnoredPropWith_BadConverter()));
            Assert.Null((await Serializer.DeserializeWrapper<TypeWith_IgnoredPropWith_BadConverter>("{}")).Property);
        }

        internal class TypeWith_RefStringProp
        {
            public ref string NameRef => ref Name;

            [YamlInclude] // This is a field.
            public string Name = "John Doe";
        }

        internal class TypeWith_IgnoredRefStringProp
        {
            [YamlIgnore]
            public ref string NameRef => ref Name;

            [YamlInclude] // This is a field.
            public string Name = "John Doe";
        }

        public class TypeWith_PropWith_BadConverter
        {
            [YamlConverter(typeof(BadConverter))]
            public string? Property { get; set; }
        }

        public class TypeWith_IgnoredPropWith_BadConverter
        {
            [YamlIgnore]
            [YamlConverter(typeof(BadConverter))]
            public string? Property { get; set; }
        }

        public class BadConverter
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task TestClassWithIgnoredCallbacks()
        {
            Assert.Equal("{}", await Serializer.SerializeWrapper(new ClassWithIgnoredCallbacks()));
            var obj = await Serializer.DeserializeWrapper<ClassWithIgnoredCallbacks>("""{"Func":"","Action":""}""");
            Assert.False(obj.Func(""));
            Assert.Null(obj.Action);
        }

        [Fact]
        public async Task TestClassWithCallbacks()
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(new ClassWithCallbacks()));
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithCallbacks>("""{"Func":{},"Action":{}"""));
        }

        public class ClassWithIgnoredCallbacks
        {
            [YamlIgnore]
            public Func<string, bool> Func { get; set; } = (val) => false;

            [YamlIgnore]
            public Action<bool> Action { get; set; }
        }

        public class ClassWithCallbacks
        {
            public Func<string, bool>? Func { get; set; }

            public Action<bool> Action { get; set; } = (val) => Console.WriteLine();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SimpleInterfaceHierarchy_Serialization_ShouldIncludeBaseInterfaceProperties()
        {
            var value = new ISimpleInterfaceHierarchy.Implementation
            {
                BaseProperty = 0,
                DerivedProperty = 1,
                DerivedProperty2 = 2,
            };

            string yaml = await Serializer.SerializeWrapper<ISimpleInterfaceHierarchy.IDerivedInterface1>(value);
            Assert.Equal("""{"DerivedProperty":1,"BaseProperty":0}""", yaml);

            yaml = await Serializer.SerializeWrapper<ISimpleInterfaceHierarchy.IDerivedInterface2>(value);
            Assert.Equal("""{"DerivedProperty2":2,"DerivedProperty":1,"BaseProperty":0}""", yaml);
        }

        public interface ISimpleInterfaceHierarchy
        {
            public int BaseProperty { get; set; }

            public interface IDerivedInterface1 : ISimpleInterfaceHierarchy
            {
                public int DerivedProperty { get; set; }
            }

            public interface IDerivedInterface2 : IDerivedInterface1
            {
                public int DerivedProperty2 { get; set; }
            }

            public class Implementation : IDerivedInterface2
            {
                public int BaseProperty { get; set; }
                public int DerivedProperty { get; set; }
                public int DerivedProperty2 { get; set; }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DiamondInterfaceHierarchy_Serialization_ShouldIncludeBaseInterfaceProperties()
        {
            var value = new IDiamondInterfaceHierarchy.Implementation
            {
                BaseProperty = 0,
                DerivedProperty = 1,
                DerivedProperty2 = 2,
                DerivedProperty3 = 3,
            };

            string yaml = await Serializer.SerializeWrapper<IDiamondInterfaceHierarchy.IDerivedInterface1>(value);
            Assert.Equal("""{"DerivedProperty":1,"BaseProperty":0}""", yaml);

            yaml = await Serializer.SerializeWrapper<IDiamondInterfaceHierarchy.IDerivedInterface2>(value);
            Assert.Equal("""{"DerivedProperty2":2,"BaseProperty":0}""", yaml);

            yaml = await Serializer.SerializeWrapper<IDiamondInterfaceHierarchy.IJoinInterface>(value);
            YamlTestHelper.AssertYamlEqual("""{"DerivedProperty3":3,"DerivedProperty2":2,"DerivedProperty":1,"BaseProperty":0}""", yaml);
        }

        public interface IDiamondInterfaceHierarchy
        {
            public int BaseProperty { get; set; }

            public interface IDerivedInterface1 : IDiamondInterfaceHierarchy
            {
                public int DerivedProperty { get; set; }
            }

            public interface IDerivedInterface2 : IDiamondInterfaceHierarchy
            {
                public int DerivedProperty2 { get; set; }
            }

            public interface IJoinInterface : IDerivedInterface1, IDerivedInterface2
            {
                public int DerivedProperty3 { get; set; }
            }

            public class Implementation : IJoinInterface
            {
                public int BaseProperty { get; set; }
                public int DerivedProperty { get; set; }
                public int DerivedProperty2 { get; set; }
                public int DerivedProperty3 { get; set; }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SimpleInterfaceHierarchyWithNamingConflict_ShouldMaskShadowedProperties()
        {
            var value = new ISimpleInterfaceHierarchyWithNamingConflict.Implementation();

            string yaml = await Serializer.SerializeWrapper<ISimpleInterfaceHierarchyWithNamingConflict>(value);
            Assert.Equal("""{"Value":2}""", yaml);

            yaml = await Serializer.SerializeWrapper<ISimpleInterfaceHierarchyWithNamingConflict.IDerivedInterface>(value);
            Assert.Equal("""{"Value":1}""", yaml);
        }

        public interface ISimpleInterfaceHierarchyWithNamingConflict
        {
            int Value { get; set; }

            public interface IDerivedInterface : ISimpleInterfaceHierarchyWithNamingConflict
            {
                new int Value { get; set; }
            }

            public class Implementation : IDerivedInterface
            {
                public int Value { get; set; } = 1;
                int ISimpleInterfaceHierarchyWithNamingConflict.Value { get; set; } = 2;
            }
        }

        [Fact]
        public async Task DiamondInterfaceHierarchyWithNamingConflict_ThrowsYamlException()
        {
            var value = new IDiamondInterfaceHierarchyWithNamingConflict.Implementation
            {
                DerivedProperty = 1,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.SerializeWrapper<IDiamondInterfaceHierarchyWithNamingConflict.IJoinInterface>(value));
        }

        public interface IDiamondInterfaceHierarchyWithNamingConflict
        {
            public interface IDerivedInterface1 : IDiamondInterfaceHierarchyWithNamingConflict
            {
                public int DerivedProperty { get; set; }
            }

            public interface IDerivedInterface2 : IDiamondInterfaceHierarchyWithNamingConflict
            {
                public int DerivedProperty { get; set; }
            }

            public interface IJoinInterface : IDerivedInterface1, IDerivedInterface2
            {
            }

            public class Implementation : IJoinInterface
            {
                public int DerivedProperty { get; set; }
            }
        }

        [Fact]
        public async Task DiamondInterfaceHierarchyWithNamingConflict_UsingYamlPropertyName_WorksAsExpected()
        {
            var value = new IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute.Implementation
            {
                DerivedProperty = 1,
            };

            string yaml = await Serializer.SerializeWrapper<IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute.IJoinInterface>(value);
            YamlTestHelper.AssertYamlEqual("""{"DerivedProperty":1,"DerivedProperty2":1}""", yaml);
        }

        public interface IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute
        {
            public interface IDerivedInterface1 : IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute
            {
                public int DerivedProperty { get; set; }
            }

            public interface IDerivedInterface2 : IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute
            {
                [YamlPropertyName("DerivedProperty2")]
                public int DerivedProperty { get; set; }
            }

            public interface IJoinInterface : IDerivedInterface1, IDerivedInterface2
            {
            }

            public class Implementation : IJoinInterface
            {
                public int DerivedProperty { get; set; }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async virtual Task TestCollectionWithPrivateElementType()
        {
            // The reflection-based serializer supports enumerables whose element type is private.

            CollectionWithPrivateElementType collection = CollectionWithPrivateElementType.CreatePopulatedInstance();

            string yaml = await Serializer.SerializeWrapper(collection);
            Assert.Equal(collection.GetExpectedYaml(), yaml);

            collection = await Serializer.DeserializeWrapper<CollectionWithPrivateElementType>(yaml);
            collection.Validate();
        }

        public class CollectionWithPrivateElementType : ICollection<PrivateEnum>
        {
            private readonly ICollection<PrivateEnum> _values = new List<PrivateEnum>();

            public static CollectionWithPrivateElementType CreatePopulatedInstance()
                => new CollectionWithPrivateElementType { _values = { PrivateEnum.A, PrivateEnum.B, PrivateEnum.C } };

            public void Validate() => Assert.Equal(new[] { PrivateEnum.A, PrivateEnum.B, PrivateEnum.C }, this);
            public string GetExpectedYaml() => "[0,1,2]";

            int ICollection<PrivateEnum>.Count => _values.Count;
            bool ICollection<PrivateEnum>.IsReadOnly => _values.IsReadOnly;
            void ICollection<PrivateEnum>.Add(PrivateEnum item) => _values.Add(item);
            void ICollection<PrivateEnum>.Clear() => _values.Clear();
            bool ICollection<PrivateEnum>.Contains(PrivateEnum item) => _values.Contains(item);
            void ICollection<PrivateEnum>.CopyTo(PrivateEnum[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
            IEnumerator<PrivateEnum> IEnumerable<PrivateEnum>.GetEnumerator() => _values.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
            bool ICollection<PrivateEnum>.Remove(PrivateEnum item) => _values.Remove(item);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async virtual Task TestDictionaryWithPrivateKeyAndValueType()
        {
            // The reflection-based serializer supports dictionaries whose key/value types are private

            DictionaryWithPrivateKeyAndValueType collection = DictionaryWithPrivateKeyAndValueType.CreatePopulatedInstance();

            string yaml = await Serializer.SerializeWrapper(collection);
            Assert.Equal(collection.GetExpectedYaml(), yaml);

            collection = await Serializer.DeserializeWrapper<DictionaryWithPrivateKeyAndValueType>(yaml);
            collection.Validate();
        }

        public class DictionaryWithPrivateKeyAndValueType : IDictionary<PrivateEnum, PrivateEnum>
        {
            private readonly IDictionary<PrivateEnum, PrivateEnum> _values = new Dictionary<PrivateEnum, PrivateEnum>();

            public static DictionaryWithPrivateKeyAndValueType CreatePopulatedInstance()
                => new DictionaryWithPrivateKeyAndValueType { _values = { [PrivateEnum.A] = PrivateEnum.B, [PrivateEnum.B] = PrivateEnum.C } };

            public void Validate() => Assert.Equal(new KeyValuePair<PrivateEnum, PrivateEnum>[] { new(PrivateEnum.A, PrivateEnum.B), new(PrivateEnum.B, PrivateEnum.C) }, this);
            public string GetExpectedYaml() => """{"A":1,"B":2}""";

            PrivateEnum IDictionary<PrivateEnum, PrivateEnum>.this[PrivateEnum key] { get => _values[key]; set => _values[key] = value; }
            ICollection<PrivateEnum> IDictionary<PrivateEnum, PrivateEnum>.Keys => _values.Keys;
            ICollection<PrivateEnum> IDictionary<PrivateEnum, PrivateEnum>.Values => _values.Values;
            int ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.Count => _values.Count;
            bool ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.IsReadOnly => _values.IsReadOnly;
            void IDictionary<PrivateEnum, PrivateEnum>.Add(PrivateEnum key, PrivateEnum value) => _values.Add(key, value);
            void ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.Add(KeyValuePair<PrivateEnum, PrivateEnum> item) => _values.Add(item);
            void ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.Clear() => _values.Clear();
            bool ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.Contains(KeyValuePair<PrivateEnum, PrivateEnum> item) => _values.Contains(item);
            bool IDictionary<PrivateEnum, PrivateEnum>.ContainsKey(PrivateEnum key) => _values.ContainsKey(key);
            void ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.CopyTo(KeyValuePair<PrivateEnum, PrivateEnum>[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
            IEnumerator<KeyValuePair<PrivateEnum, PrivateEnum>> IEnumerable<KeyValuePair<PrivateEnum, PrivateEnum>>.GetEnumerator() => _values.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
            bool IDictionary<PrivateEnum, PrivateEnum>.Remove(PrivateEnum key) => _values.Remove(key);
            bool ICollection<KeyValuePair<PrivateEnum, PrivateEnum>>.Remove(KeyValuePair<PrivateEnum, PrivateEnum> item) => _values.Remove(item);
            bool IDictionary<PrivateEnum, PrivateEnum>.TryGetValue(PrivateEnum key, out PrivateEnum value) => _values.TryGetValue(key, out value);
        }

        private enum PrivateEnum { A = 0, B = 1, C = 2 }
    }
}

namespace NamespaceBase
{
    public class Class1
    {
        public const string DefaultValueForInfo = $"Some default content of {nameof(Info)} property of {nameof(NamespaceBase)}";

        public string Info { get; set; } = DefaultValueForInfo;
    }

    public class Class2
    {
        [YamlIgnore]
        public Class1 class1 => new Class1();
    }
}
