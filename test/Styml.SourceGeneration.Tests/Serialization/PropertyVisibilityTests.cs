// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public partial class PropertyVisibilityTests_Metadata : PropertyVisibilityTests
    {
        public PropertyVisibilityTests_Metadata()
            : this(new StringSerializerWrapper(PropertyVisibilityTestsContext_Metadata.Default))
        {
        }

        protected PropertyVisibilityTests_Metadata(Serialization.Tests.YamlSerializerWrapper serializerWrapper)
            : base(serializerWrapper)
        {
        }

        [Theory]
        [InlineData(typeof(ClassWithBadIgnoreAttribute))]
        [InlineData(typeof(StructWithBadIgnoreAttribute))]
        public override async Task YamlIgnoreCondition_WhenWritingNull_OnValueType_Fail_EmptyYaml([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            InvalidOperationException ioe = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper("", type));
            ValidateInvalidOperationException();

            ioe = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(Activator.CreateInstance(type), type));
            ValidateInvalidOperationException();

            void ValidateInvalidOperationException()
            {
                string exAsStr = ioe.ToString();
                Assert.Contains("YamlIgnoreCondition.WhenWritingNull", exAsStr);
                Assert.Contains("MyBadMember", exAsStr);
                Assert.Contains(type.ToString(), exAsStr);
                Assert.Contains("YamlIgnoreCondition.WhenWritingDefault", exAsStr);
            }
        }

        [Fact]
        public override async Task TestCollectionWithPrivateElementType()
        {
            // The source generator cannot support enumerables whose element type is private.
            CollectionWithPrivateElementType collection = CollectionWithPrivateElementType.CreatePopulatedInstance();
            string yaml = collection.GetExpectedYaml();

            Assert.True(Serializer.DefaultOptions.TryGetTypeInfo(typeof(CollectionWithPrivateElementType), out _));

            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(collection));
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<CollectionWithPrivateElementType>(yaml));
        }

        [Fact]
        public override async Task TestDictionaryWithPrivateKeyAndValueType()
        {
            // The source generator cannot support dictionaries whose key/value types are private.
            DictionaryWithPrivateKeyAndValueType dictionary = DictionaryWithPrivateKeyAndValueType.CreatePopulatedInstance();
            string yaml = dictionary.GetExpectedYaml();

            Assert.True(Serializer.DefaultOptions.TryGetTypeInfo(typeof(DictionaryWithPrivateKeyAndValueType), out _));

            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(dictionary));
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<DictionaryWithPrivateKeyAndValueType>(yaml));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public override async Task ClassWithIgnoredAndPrivateMembers_DoesNotIncludeIgnoredMetadata()
        {
            // The type referenced by the ignored/private properties should
            // not be included in the supported types by the generator.
            YamlSerializerOptions options = Serializer.DefaultOptions;
            Assert.Null(options.TypeInfoResolver.GetTypeInfo(typeof(TypeThatShouldNotBeGenerated), options));

            await base.ClassWithIgnoredAndPrivateMembers_DoesNotIncludeIgnoredMetadata();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public override async Task YamlIgnoreCondition_TypeLevel_Always_ThrowsInvalidOperation()
        {
            // In the source generator path, 'YamlIgnoreCondition.Always' on a type emits a diagnostic warning
            // and the attribute is ignored, so all properties are serialized normally.
            var obj = new ClassWithTypeLevelIgnore_Always
            {
                MyString = "value",
                MyInt = 42
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""MyString"":""value""", yaml);
            Assert.Contains(@"""MyInt"":42", yaml);
        }

        [Theory]
        [InlineData(YamlSourceGenerationMode.Metadata, false)]
        [InlineData(YamlSourceGenerationMode.Metadata, true)]
        [InlineData(YamlSourceGenerationMode.Default, false)]
        [InlineData(YamlSourceGenerationMode.Default, true)]
        public static void GenericContexts_InaccessibleGenericMembers_CanRoundtrip(YamlSourceGenerationMode mode, bool nested)
        {
            YamlSerializerContext context = (mode, nested) switch
            {
                (YamlSourceGenerationMode.Metadata, false) => GenericAccessorContext_Metadata<string>.Default,
                (YamlSourceGenerationMode.Metadata, true) => GenericAccessorContextContainer<string>.NestedGenericAccessorContext_Metadata.Default,
                (YamlSourceGenerationMode.Default, false) => GenericAccessorContext_Default<string>.Default,
                _ => GenericAccessorContextContainer<string>.NestedGenericAccessorContext_Default.Default,
            };

            const string Yaml = """{"Value":42,"Label":"test"}""";
            var result = (GenericClassWithPrivateYamlIncludeProperties<int>)YamlSerializer.Deserialize(
                Yaml, typeof(GenericClassWithPrivateYamlIncludeProperties<int>), context);
            Assert.Equal(42, result.GetValue());
            Assert.Equal("test", result.GetLabel());
            YamlTestHelper.AssertYamlEqual(Yaml, YamlSerializer.Serialize(
                result, typeof(GenericClassWithPrivateYamlIncludeProperties<int>), context));
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
        private partial class GenericAccessorContext_Metadata<TContext> : YamlSerializerContext { }

        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
        private partial class GenericAccessorContext_Default<TContext> : YamlSerializerContext { }

        private partial class GenericAccessorContextContainer<TContext>
        {
            [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
            [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
            public partial class NestedGenericAccessorContext_Metadata : YamlSerializerContext { }

            [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
            public partial class NestedGenericAccessorContext_Default : YamlSerializerContext { }
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(ClassWithNewSlotField))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(object))]
        [YamlSerializable(typeof(ClassWithInternalField))]
        [YamlSerializable(typeof(ClassWithNewSlotDecimalField))]
        [YamlSerializable(typeof(ClassWithNewSlotAttributedDecimalField))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyPolicyConflictPrivate))]
        [YamlSerializable(typeof(ClassWithMissingCollectionProperty))]
        [YamlSerializable(typeof(ClassWithClassProperty_IgnoreConditionWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithNoSetter))]
        [YamlSerializable(typeof(ClassWithInternalProperty))]
        [YamlSerializable(typeof(ClassWithPropertyNamingConflict))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithMissingObjectProperty))]
        [YamlSerializable(typeof(ClassWithInitOnlyProperty))]
        [YamlSerializable(typeof(Class_WithIgnoredInitOnlyProperty))]
        [YamlSerializable(typeof(Record_WithIgnoredPropertyInCtor))]
        [YamlSerializable(typeof(Class_WithIgnoredRequiredProperty))]
        [YamlSerializable(typeof(RecordWithIgnoredNestedInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithInitOnlyProperty?))]
        [YamlSerializable(typeof(ClassWithCustomNamedInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithCustomNamedInitOnlyProperty))]
        [YamlSerializable(typeof(MyClassWithValueTypeInterfaceProperty))]
        [YamlSerializable(typeof(ClassWithNonPublicProperties))]
        [YamlSerializable(typeof(ClassWithProperty_IgnoreConditionAlways))]
        [YamlSerializable(typeof(ClassWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(StructWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicPropertyAndNewSlotPrivate))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicPropertyAndNewSlotPublicAndIgnoredToo))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyPolicyConflictPublic))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyNamingConflictPrivate))]
        [YamlSerializable(typeof(ClassWithIgnoredNewSlotProperty))]
        [YamlSerializable(typeof(ClassWithObsoleteAndIgnoredProperty))]
        [YamlSerializable(typeof(ClassWithPublicGetterAndPrivateSetter))]
        [YamlSerializable(typeof(ClassWithInitializedProps))]
        [YamlSerializable(typeof(ClassWithNewSlotInternalProperty))]
        [YamlSerializable(typeof(ClassWithPropertyPolicyConflict))]
        [YamlSerializable(typeof(ClassWithPrivateSetterAndGetter))]
        [YamlSerializable(typeof(ClassWithProtectedMembers))]
        [YamlSerializable(typeof(ClassWithProtectedGetter))]
        [YamlSerializable(typeof(ClassWithProtectedSetter))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedMembers))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedGetter))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedSetter))]
        [YamlSerializable(typeof(ClassWithInternalProtectedMembers))]
        [YamlSerializable(typeof(ClassWithInternalProtectedGetter))]
        [YamlSerializable(typeof(ClassWithInternalProtectedSetter))]
        [YamlSerializable(typeof(ClassWithIgnoreAttributeProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredNewSlotField))]
        [YamlSerializable(typeof(MyStruct_WithNonPublicAccessors_WithTypeAttribute))]
        [YamlSerializable(typeof(ClassWithReadOnlyFields))]
        [YamlSerializable(typeof(MyValueTypeWithBoxedPrimitive))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithNoGetter))]
        [YamlSerializable(typeof(ClassWithPropsAndIgnoreAttributes))]
        [YamlSerializable(typeof(List<bool>))]
        [YamlSerializable(typeof(MyValueTypeWithProperties))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithOverrideReversed))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty_IgnoreNever))]
        [YamlSerializable(typeof(ClassWithProps))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionNever))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionNever_Ctor))]
        [YamlSerializable(typeof(ClassWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(StructWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(ClassWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField_IgnoreWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField_IgnoreNever))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithPrivateProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternalProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtectedProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPrivateField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternalField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtectedField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPublicProperty))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(StructWithOverride))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(MyClass_WithNonPublicAccessors_WithPropertyAttributes))]
        [YamlSerializable(typeof(Class_PropertyWith_PrivateInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_PrivateInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(DerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(DerivedClass_WithVisibleProperty_Of_DerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(DerivedClass_With_IgnoredOverride_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingNewMember))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingNewMember_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_ConflictingNewMember))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_ConflictingNewMember_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_NewProperty_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty_Of_DifferentType_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(FurtherDerivedClass_With_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingPropertyName))]
        [YamlSerializable(typeof(FurtherDerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyNamingConflictPublic))]
        [YamlSerializable(typeof(MyClassWithValueType))]
        [YamlSerializable(typeof(StructWithPropertiesWithConverter))]
        [YamlSerializable(typeof(ClassWithNewSlotProperty))]
        [YamlSerializable(typeof(ClassWithNewSlotAttributedDecimalProperty))]
        [YamlSerializable(typeof(ClassWithNewSlotDecimalProperty))]
        [YamlSerializable(typeof(LargeStructWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(ClassWithUnsupportedBigInteger))]
        [YamlSerializable(typeof(WrapperForClassWithUnsupportedBigInteger))]
        [YamlSerializable(typeof(ClassWithIgnoredUnsupportedBigInteger))]
        [YamlSerializable(typeof(WrapperForClassWithIgnoredUnsupportedBigInteger))]
        [YamlSerializable(typeof(ClassWithThingsToIgnore))]
        [YamlSerializable(typeof(ClassWithMixedPropertyAccessors_PropertyAttributes))]
        [YamlSerializable(typeof(ClassWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(MyClass_WithNonPublicAccessors))]
        [YamlSerializable(typeof(ClassWithThingsToIgnore_PerProperty))]
        [YamlSerializable(typeof(StructWithPropertiesWithYamlPropertyName_PrivateGetter))]
        [YamlSerializable(typeof(StructWithPropertiesWithYamlPropertyName_PrivateSetter))]
        [YamlSerializable(typeof(ClassWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault))]
        [YamlSerializable(typeof(ConcreteDerivedClass))]
        [YamlSerializable(typeof(TypeWith_RefStringProp))]
        [YamlSerializable(typeof(TypeWith_IgnoredRefStringProp))]
        [YamlSerializable(typeof(TypeWith_PropWith_BadConverter))]
        [YamlSerializable(typeof(TypeWith_IgnoredPropWith_BadConverter))]
        [YamlSerializable(typeof(ClassWithIgnoredCallbacks))]
        [YamlSerializable(typeof(ClassWithCallbacks))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchy.IDerivedInterface1))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchy.IDerivedInterface2))]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IDerivedInterface1), TypeInfoPropertyName = "IDiamondInterfaceHierarchyIDerivedInterface1")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IDerivedInterface2), TypeInfoPropertyName = "IDiamondInterfaceHierarchyIDerivedInterface2")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IJoinInterface))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchyWithNamingConflict))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchyWithNamingConflict.IDerivedInterface))]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchyWithNamingConflict.IJoinInterface), TypeInfoPropertyName = "IDiamondInterfaceHierarchyWithNamingConflictIJoinInterface")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute.IJoinInterface), TypeInfoPropertyName = "IDiamondInterfaceHierarchyWithNamingConflictUsingAttributeIJoinInterface")]
        [YamlSerializable(typeof(CollectionWithPrivateElementType))]
        [YamlSerializable(typeof(DictionaryWithPrivateKeyAndValueType))]
        [YamlSerializable(typeof(ClassWithIgnoredAndPrivateMembers))]
        [YamlSerializable(typeof(ClassWithInternalYamlIncludeProperties))]
        [YamlSerializable(typeof(ClassWithIgnoredAndPrivateMembers))]
        [YamlSerializable(typeof(ClassWithPrivateYamlIncludeProperties_Roundtrip))]
        [YamlSerializable(typeof(ClassWithProtectedYamlIncludeProperties_Roundtrip))]
        [YamlSerializable(typeof(ClassWithMixedAccessibilityYamlIncludeProperties))]
        [YamlSerializable(typeof(ClassWithYamlIncludePrivateInitOnlyProperties))]
        [YamlSerializable(typeof(ClassWithYamlIncludePrivateGetterProperties))]
        [YamlSerializable(typeof(StructWithYamlIncludePrivateProperties))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeProperties<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeProperties<string>))]
        [YamlSerializable(typeof(GenericClassWithReadOnlyYamlIncludeMembers<int>))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeFields<int>))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeFields<string>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeFields<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeFields<string>))]
        [YamlSerializable(typeof(StructWithPrivateConstructorAndMembers))]
        [YamlSerializable(typeof(GenericStructWithPrivateInitOnlyMembers<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateInitOnlyMembers<string>))]
        [YamlSerializable(typeof(GenericMemberOuter<int>.Nested<string>))]
        [YamlSerializable(typeof(GenericMemberOuter<int>.Nested))]
        [YamlSerializable(typeof(GenericAccessorDerived<int>))]
        [YamlSerializable(typeof(NonGenericAccessorDerived))]
        [YamlSerializable(typeof(NonGenericAccessorLeaf))]
        [YamlSerializable(typeof(GenericAccessorDerivedWithPublicProperty<int>))]
        [YamlSerializable(typeof(GenericAccessorDerivedWithIgnoredProperty<int>))]
        [YamlSerializable(typeof(GenericAccessorDerived<int, string>))]
        [YamlSerializable(typeof(GenericMembersWithKeywordParameter<int>))]
        [YamlSerializable(typeof(ConstrainedGenericAccessor<int, List<int>>))]
        [YamlSerializable(typeof(ConstrainedGenericClassWithInitOnlyProperties<ConstraintDerived>))]
        [YamlSerializable(typeof(ConstrainedGenericClassWithOptionalInitOnlyProperties<ConstraintDerived>))]
        [YamlSerializable(typeof(ClassWithInitOnlyPropertyDefaults))]
        [YamlSerializable(typeof(StructWithInitOnlyPropertyDefaults))]
        [YamlSerializable(typeof(ClassUsingIgnoreWhenWritingDefaultAttribute))]
        [YamlSerializable(typeof(ClassUsingIgnoreNeverAttribute))]
        [YamlSerializable(typeof(ClassWithIgnoredUnsupportedDictionary))]
        [YamlSerializable(typeof(ClassWithProperty_IgnoreConditionAlways_Ctor))]
        [YamlSerializable(typeof(ClassWithClassProperty_IgnoreConditionWhenWritingDefault_Ctor))]
        [YamlSerializable(typeof(StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor))]
        [YamlSerializable(typeof(YamlIgnoreCondition_WhenReadingWritingTestModel))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_WhenWritingNull))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_WhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_Always))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_PropertyOverride))]
        [YamlSerializable(typeof(StructWithTypeLevelIgnore_WhenWritingNull))]
        [YamlSerializable(typeof(BaseClassWithProperties))]
        [YamlSerializable(typeof(DerivedClassWithTypeLevelIgnore))]
        [YamlSerializable(typeof(SmallStructWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(WrapperForClassWithIgnoredUnsupportedDictionary))]
        [YamlSerializable(typeof(Class1))]
        [YamlSerializable(typeof(Class2))]
        [YamlSerializable(typeof(NamespaceBase.Class1), TypeInfoPropertyName = "Class1FromNamespaceBase")]
        [YamlSerializable(typeof(NamespaceBase.Class2), TypeInfoPropertyName = "Class2FromNamespaceBase")]
#if NET11_0_OR_GREATER // Styml: System.Numerics.Colors is new in .NET 11 (net10.0 test leg)
        [YamlSerializable(typeof(System.Numerics.Colors.Argb<float>))]
#endif
        internal sealed partial class PropertyVisibilityTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public partial class PropertyVisibilityTests_Default : PropertyVisibilityTests_Metadata
    {
        public PropertyVisibilityTests_Default()
            : base(new StringSerializerWrapper(PropertyVisibilityTestsContext_Default.Default))
        {
        }

        [Theory]
        [InlineData(typeof(ClassWithBadIgnoreAttribute))]
        [InlineData(typeof(StructWithBadIgnoreAttribute))]
        public override async Task YamlIgnoreCondition_WhenWritingNull_OnValueType_Fail([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper("{}", type));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(Activator.CreateInstance(type), type));
        }

        [Theory]
        [InlineData(typeof(ClassWithBadIgnoreAttribute))]
        [InlineData(typeof(StructWithBadIgnoreAttribute))]
        public override async Task YamlIgnoreCondition_WhenWritingNull_OnValueType_Fail_EmptyYaml([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            // Since this code goes down fast-path, there's no warm up and we hit the reader exception about having no tokens.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper("", type));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(Activator.CreateInstance(type), type));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void PublicContextAndTestClassWithPropertiesWithDifferentAccessibilities()
        {
            YamlSerializerOptions options = new()
            {
                IncludeFields = true,
            };

            new PublicContext(options);

            PublicClassWithDifferentAccessibilitiesProperties obj = new()
            {
                PublicProperty = new(),
                PublicField = new(),
            };

            string yaml = YamlSerializer.Serialize(obj, options.GetTypeInfo<PublicClassWithDifferentAccessibilitiesProperties>());
            Assert.Equal("""{"PublicProperty":{},"PublicField":{}}""", yaml);

            var deserialized = YamlSerializer.Deserialize(yaml, options.GetTypeInfo<PublicClassWithDifferentAccessibilitiesProperties>());
            Assert.NotNull(deserialized.PublicProperty);
            Assert.NotNull(deserialized.PublicField);

            yaml = "{}";
            deserialized = YamlSerializer.Deserialize(yaml, options.GetTypeInfo<PublicClassWithDifferentAccessibilitiesProperties>());
            Assert.Null(deserialized.PublicProperty);
            Assert.Null(deserialized.PublicField);
        }

        [Fact]
        public void PublicContextAndYamlConverter()
        {
            YamlConverter obj = YamlMetadataServices.BooleanConverter;

            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(obj, PublicContext.Default.Options.GetTypeInfo<YamlConverter>()));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize("{}", PublicContext.Default.Options.GetTypeInfo<YamlConverter>()));
        }

        [Fact]
        public void PublicContextAndYamlSerializerOptions()
        {
            YamlSerializerOptions obj = new()
            {
                DefaultBufferSize = 123,
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
                IncludeFields = true,
            };

            string yaml = YamlSerializer.Serialize(obj, PublicContext.Default.Options.GetTypeInfo<YamlSerializerOptions>());

            YamlSerializerOptions deserialized = YamlSerializer.Deserialize(yaml, PublicContext.Default.Options.GetTypeInfo<YamlSerializerOptions>());
            Assert.Equal(obj.DefaultBufferSize, deserialized.DefaultBufferSize);
            Assert.Equal(obj.DefaultIgnoreCondition, deserialized.DefaultIgnoreCondition);
            Assert.Equal(obj.IncludeFields, deserialized.IncludeFields);
            Assert.Equal(obj.IgnoreReadOnlyFields, deserialized.IgnoreReadOnlyFields);
            Assert.Equal(obj.MaxDepth, deserialized.MaxDepth);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void PocoWithNullableProperties_IgnoresNullValuesWithGlobalSetting()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/96404
            var value = new PocoWithNullableProperties();
            string yaml = YamlSerializer.Serialize(value, DefaultContextWithGlobalIgnoreSetting.Default.PocoWithNullableProperties);
            Assert.Equal("{}", yaml);
        }

        class PocoWithNullableProperties
        {
            public string? NullableRefType { get; set; }
            public int? NullableValueType { get; set; }
        }

        [YamlSourceGenerationOptions(
            GenerationMode = YamlSourceGenerationMode.Default,
            DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull)]
        [YamlSerializable(typeof(PocoWithNullableProperties))]
        partial class DefaultContextWithGlobalIgnoreSetting : YamlSerializerContext;

        [YamlSerializable(typeof(ClassWithNewSlotField))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(object))]
        [YamlSerializable(typeof(ClassWithInternalField))]
        [YamlSerializable(typeof(ClassWithNewSlotDecimalField))]
        [YamlSerializable(typeof(ClassWithNewSlotAttributedDecimalField))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyPolicyConflictPrivate))]
        [YamlSerializable(typeof(ClassWithMissingCollectionProperty))]
        [YamlSerializable(typeof(ClassWithClassProperty_IgnoreConditionWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithNoSetter))]
        [YamlSerializable(typeof(ClassWithInternalProperty))]
        [YamlSerializable(typeof(ClassWithPropertyNamingConflict))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithMissingObjectProperty))]
        [YamlSerializable(typeof(ClassWithInitOnlyProperty))]
        [YamlSerializable(typeof(Class_WithIgnoredInitOnlyProperty))]
        [YamlSerializable(typeof(Record_WithIgnoredPropertyInCtor))]
        [YamlSerializable(typeof(Class_WithIgnoredRequiredProperty))]
        [YamlSerializable(typeof(RecordWithIgnoredNestedInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithInitOnlyProperty?))]
        [YamlSerializable(typeof(ClassWithCustomNamedInitOnlyProperty))]
        [YamlSerializable(typeof(StructWithCustomNamedInitOnlyProperty))]
        [YamlSerializable(typeof(MyClassWithValueTypeInterfaceProperty))]
        [YamlSerializable(typeof(ClassWithNonPublicProperties))]
        [YamlSerializable(typeof(ClassWithProperty_IgnoreConditionAlways))]
        [YamlSerializable(typeof(ClassWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(StructWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicPropertyAndNewSlotPrivate))]
        [YamlSerializable(typeof(ClassWithIgnoredPublicPropertyAndNewSlotPublicAndIgnoredToo))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyPolicyConflictPublic))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyNamingConflictPrivate))]
        [YamlSerializable(typeof(ClassWithIgnoredNewSlotProperty))]
        [YamlSerializable(typeof(ClassWithObsoleteAndIgnoredProperty))]
        [YamlSerializable(typeof(ClassWithPublicGetterAndPrivateSetter))]
        [YamlSerializable(typeof(ClassWithInitializedProps))]
        [YamlSerializable(typeof(ClassWithNewSlotInternalProperty))]
        [YamlSerializable(typeof(ClassWithPropertyPolicyConflict))]
        [YamlSerializable(typeof(ClassWithPrivateSetterAndGetter))]
        [YamlSerializable(typeof(ClassWithProtectedMembers))]
        [YamlSerializable(typeof(ClassWithProtectedGetter))]
        [YamlSerializable(typeof(ClassWithProtectedSetter))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedMembers))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedGetter))]
        [YamlSerializable(typeof(ClassWithPrivateProtectedSetter))]
        [YamlSerializable(typeof(ClassWithInternalProtectedMembers))]
        [YamlSerializable(typeof(ClassWithInternalProtectedGetter))]
        [YamlSerializable(typeof(ClassWithInternalProtectedSetter))]
        [YamlSerializable(typeof(ClassWithIgnoreAttributeProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredNewSlotField))]
        [YamlSerializable(typeof(MyStruct_WithNonPublicAccessors_WithTypeAttribute))]
        [YamlSerializable(typeof(ClassWithReadOnlyFields))]
        [YamlSerializable(typeof(MyValueTypeWithBoxedPrimitive))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithNoGetter))]
        [YamlSerializable(typeof(ClassWithPropsAndIgnoreAttributes))]
        [YamlSerializable(typeof(List<bool>))]
        [YamlSerializable(typeof(MyValueTypeWithProperties))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithOverrideReversed))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty_IgnoreNever))]
        [YamlSerializable(typeof(ClassWithProps))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionNever))]
        [YamlSerializable(typeof(ClassWithStructProperty_IgnoreConditionNever_Ctor))]
        [YamlSerializable(typeof(ClassWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(StructWithBadIgnoreAttribute))]
        [YamlSerializable(typeof(ClassWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField_IgnoreWhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringField_IgnoreNever))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyFieldNamingConflictWhichThrows))]
        [YamlSerializable(typeof(ClassWithPrivateProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternalProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtectedProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPrivateField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternalField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtectedField_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPrivate_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithInternal_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithProtected_InitOnlyProperty_WithYamlIncludeProperty))]
        [YamlSerializable(typeof(ClassWithPublicProperty))]
        [YamlSerializable(typeof(ClassInheritedWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(StructWithOverride))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyFieldPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyNamingConflictWhichThrows))]
        [YamlSerializable(typeof(MyClass_WithNonPublicAccessors_WithPropertyAttributes))]
        [YamlSerializable(typeof(Class_PropertyWith_PrivateInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter))]
        [YamlSerializable(typeof(Class_PropertyWith_PrivateInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_InternalInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(Class_PropertyWith_ProtectedInitOnlySetter_WithAttribute))]
        [YamlSerializable(typeof(DerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(DerivedClass_WithVisibleProperty_Of_DerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(DerivedClass_With_IgnoredOverride_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingNewMember))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingNewMember_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_ConflictingNewMember))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_ConflictingNewMember_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_NewProperty_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty_Of_DifferentType))]
        [YamlSerializable(typeof(DerivedClass_With_Ignored_NewProperty_Of_DifferentType_And_ConflictingPropertyName))]
        [YamlSerializable(typeof(FurtherDerivedClass_With_ConflictingPropertyName))]
        [YamlSerializable(typeof(DerivedClass_WithConflictingPropertyName))]
        [YamlSerializable(typeof(FurtherDerivedClass_With_IgnoredOverride))]
        [YamlSerializable(typeof(ClassWithIgnoredPropertyNamingConflictPublic))]
        [YamlSerializable(typeof(MyClassWithValueType))]
        [YamlSerializable(typeof(StructWithPropertiesWithConverter))]
        [YamlSerializable(typeof(ClassWithNewSlotProperty))]
        [YamlSerializable(typeof(ClassWithNewSlotAttributedDecimalProperty))]
        [YamlSerializable(typeof(ClassWithNewSlotDecimalProperty))]
        [YamlSerializable(typeof(LargeStructWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(ClassWithUnsupportedBigInteger))]
        [YamlSerializable(typeof(WrapperForClassWithUnsupportedBigInteger))]
        [YamlSerializable(typeof(ClassWithIgnoredUnsupportedBigInteger))]
        [YamlSerializable(typeof(WrapperForClassWithIgnoredUnsupportedBigInteger))]
        [YamlSerializable(typeof(ClassWithThingsToIgnore))]
        [YamlSerializable(typeof(ClassWithMixedPropertyAccessors_PropertyAttributes))]
        [YamlSerializable(typeof(ClassWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(ClassTwiceInheritedWithPropertyPolicyConflictWhichThrows))]
        [YamlSerializable(typeof(MyClass_WithNonPublicAccessors))]
        [YamlSerializable(typeof(ClassWithThingsToIgnore_PerProperty))]
        [YamlSerializable(typeof(StructWithPropertiesWithYamlPropertyName_PrivateGetter))]
        [YamlSerializable(typeof(StructWithPropertiesWithYamlPropertyName_PrivateSetter))]
        [YamlSerializable(typeof(ClassWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(ClassWithReadOnlyStringProperty_IgnoreWhenWritingDefault))]
        [YamlSerializable(typeof(ConcreteDerivedClass))]
        [YamlSerializable(typeof(TypeWith_RefStringProp))]
        [YamlSerializable(typeof(TypeWith_IgnoredRefStringProp))]
        [YamlSerializable(typeof(TypeWith_PropWith_BadConverter))]
        [YamlSerializable(typeof(TypeWith_IgnoredPropWith_BadConverter))]
        [YamlSerializable(typeof(ClassWithIgnoredCallbacks))]
        [YamlSerializable(typeof(ClassWithCallbacks))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchy.IDerivedInterface1))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchy.IDerivedInterface2))]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IDerivedInterface1), TypeInfoPropertyName = "IDiamondInterfaceHierarchyIDerivedInterface1")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IDerivedInterface2), TypeInfoPropertyName = "IDiamondInterfaceHierarchyIDerivedInterface2")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchy.IJoinInterface))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchyWithNamingConflict))]
        [YamlSerializable(typeof(ISimpleInterfaceHierarchyWithNamingConflict.IDerivedInterface))]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchyWithNamingConflict.IJoinInterface), TypeInfoPropertyName = "IDiamondInterfaceHierarchyWithNamingConflictIJoinInterface")]
        [YamlSerializable(typeof(IDiamondInterfaceHierarchyWithNamingConflictUsingAttribute.IJoinInterface), TypeInfoPropertyName = "IDiamondInterfaceHierarchyWithNamingConflictUsingAttributeIJoinInterface")]
        [YamlSerializable(typeof(CollectionWithPrivateElementType))]
        [YamlSerializable(typeof(DictionaryWithPrivateKeyAndValueType))]
        [YamlSerializable(typeof(ClassWithInternalYamlIncludeProperties))]
        [YamlSerializable(typeof(ClassWithIgnoredAndPrivateMembers))]
        [YamlSerializable(typeof(ClassWithPrivateYamlIncludeProperties_Roundtrip))]
        [YamlSerializable(typeof(ClassWithProtectedYamlIncludeProperties_Roundtrip))]
        [YamlSerializable(typeof(ClassWithMixedAccessibilityYamlIncludeProperties))]
        [YamlSerializable(typeof(ClassWithYamlIncludePrivateInitOnlyProperties))]
        [YamlSerializable(typeof(ClassWithYamlIncludePrivateGetterProperties))]
        [YamlSerializable(typeof(StructWithYamlIncludePrivateProperties))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeProperties<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeProperties<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeProperties<string>))]
        [YamlSerializable(typeof(GenericClassWithReadOnlyYamlIncludeMembers<int>))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeFields<int>))]
        [YamlSerializable(typeof(GenericClassWithPrivateYamlIncludeFields<string>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeFields<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateYamlIncludeFields<string>))]
        [YamlSerializable(typeof(StructWithPrivateConstructorAndMembers))]
        [YamlSerializable(typeof(GenericStructWithPrivateInitOnlyMembers<int>))]
        [YamlSerializable(typeof(GenericStructWithPrivateInitOnlyMembers<string>))]
        [YamlSerializable(typeof(GenericMemberOuter<int>.Nested<string>))]
        [YamlSerializable(typeof(GenericMemberOuter<int>.Nested))]
        [YamlSerializable(typeof(GenericAccessorDerived<int>))]
        [YamlSerializable(typeof(NonGenericAccessorDerived))]
        [YamlSerializable(typeof(NonGenericAccessorLeaf))]
        [YamlSerializable(typeof(GenericAccessorDerivedWithPublicProperty<int>))]
        [YamlSerializable(typeof(GenericAccessorDerivedWithIgnoredProperty<int>))]
        [YamlSerializable(typeof(GenericAccessorDerived<int, string>))]
        [YamlSerializable(typeof(GenericMembersWithKeywordParameter<int>))]
        [YamlSerializable(typeof(ConstrainedGenericAccessor<int, List<int>>))]
        [YamlSerializable(typeof(ConstrainedGenericClassWithInitOnlyProperties<ConstraintDerived>))]
        [YamlSerializable(typeof(ConstrainedGenericClassWithOptionalInitOnlyProperties<ConstraintDerived>))]
        [YamlSerializable(typeof(ClassWithInitOnlyPropertyDefaults))]
        [YamlSerializable(typeof(StructWithInitOnlyPropertyDefaults))]
        [YamlSerializable(typeof(ClassUsingIgnoreWhenWritingDefaultAttribute))]
        [YamlSerializable(typeof(ClassUsingIgnoreNeverAttribute))]
        [YamlSerializable(typeof(ClassWithIgnoredUnsupportedDictionary))]
        [YamlSerializable(typeof(ClassWithProperty_IgnoreConditionAlways_Ctor))]
        [YamlSerializable(typeof(ClassWithClassProperty_IgnoreConditionWhenWritingDefault_Ctor))]
        [YamlSerializable(typeof(StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor))]
        [YamlSerializable(typeof(YamlIgnoreCondition_WhenReadingWritingTestModel))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_WhenWritingNull))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_WhenWritingDefault))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_Always))]
        [YamlSerializable(typeof(ClassWithTypeLevelIgnore_PropertyOverride))]
        [YamlSerializable(typeof(StructWithTypeLevelIgnore_WhenWritingNull))]
        [YamlSerializable(typeof(BaseClassWithProperties))]
        [YamlSerializable(typeof(DerivedClassWithTypeLevelIgnore))]
        [YamlSerializable(typeof(SmallStructWithValueAndReferenceTypes))]
        [YamlSerializable(typeof(WrapperForClassWithIgnoredUnsupportedDictionary))]
        [YamlSerializable(typeof(Class1))]
        [YamlSerializable(typeof(Class2))]
        [YamlSerializable(typeof(NamespaceBase.Class1), TypeInfoPropertyName = "Class1FromNamespaceBase")]
        [YamlSerializable(typeof(NamespaceBase.Class2), TypeInfoPropertyName = "Class2FromNamespaceBase")]
#if NET11_0_OR_GREATER // Styml: System.Numerics.Colors is new in .NET 11 (net10.0 test leg)
        [YamlSerializable(typeof(System.Numerics.Colors.Argb<float>))]
#endif
        internal sealed partial class PropertyVisibilityTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
