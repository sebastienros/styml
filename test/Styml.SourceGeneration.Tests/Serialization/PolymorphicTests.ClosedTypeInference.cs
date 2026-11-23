// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Text.Yaml.SourceGeneration.Tests.NETStandard;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class PolymorphicTests_Metadata
    {
        protected override YamlSerializerOptions ClosedTypeInferenceOptions =>
            ClosedInferenceContext_Metadata.Default.Options;

        [YamlSerializable(typeof(ClosedAttributeExplicitBase))]
        [YamlSerializable(typeof(ClosedAttributeOptInBase))]
        [YamlSerializable(typeof(ClosedAttributeOptOutBase))]
        [YamlSerializable(typeof(ClosedEmptyAttributeOptInBase))]
        [YamlSerializable(typeof(ClosedEmptyBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithCustomDiscriminatorBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithIgnoreUnrecognizedDiscriminatorsBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithTypeClassifierBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithUnknownDerivedTypeHandlingBase))]
        [YamlSerializable(typeof(ClosedEmptyPolymorphicBase))]
        [YamlSerializable(typeof(ClosedExplicitBase))]
        [YamlSerializable(typeof(ClosedExplicitOptOutBase))]
        [YamlSerializable(typeof(ClosedNumberList))]
        [YamlSerializable(typeof(ClosedShape))]
        [YamlSerializable(typeof(NonClosedAttributeBase))]
        internal sealed partial class PolymorphicTestsContext_Metadata;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeInferClosedTypePolymorphism_EmptyHierarchy_IsInert(bool defaultMode)
        {
            var options = new YamlSerializerOptions
            {
                InferClosedTypePolymorphism = true,
            };

            YamlSerializerContext context = defaultMode
                ? new PolymorphicTests_Default.PolymorphicTestsContext_Default(options)
                : new PolymorphicTestsContext_Metadata(options);

            Assert.Same(options, context.Options);
            Assert.Null(options.GetTypeInfo(typeof(ClosedEmptyBase)).PolymorphismOptions);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClosedTypeInference_FromReferencedAssembly(bool defaultMode)
        {
            YamlSerializerContext context = defaultMode
                ? ClosedInferenceContext_Default.Default
                : ClosedInferenceContext_Metadata.Default;

            YamlTypeInfo? typeInfo = context.GetTypeInfo(typeof(ReferencedClosedShape));
            Assert.NotNull(typeInfo);
            YamlPolymorphismOptions? polymorphismOptions = typeInfo.PolymorphismOptions;
            Assert.NotNull(polymorphismOptions);

            Assert.Collection(
                polymorphismOptions.DerivedTypes,
                derivedType =>
                {
                    Assert.Equal(typeof(ReferencedClosedCircle), derivedType.DerivedType);
                    Assert.Equal(nameof(ReferencedClosedCircle), derivedType.TypeDiscriminator);
                },
                derivedType =>
                {
                    Assert.Equal(typeof(ReferencedClosedSquare), derivedType.DerivedType);
                    Assert.Equal(nameof(ReferencedClosedSquare), derivedType.TypeDiscriminator);
                });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClosedTypeInference_NestedHierarchyFromReferencedAssembly(bool defaultMode)
        {
            YamlSerializerContext context = defaultMode
                ? ClosedInferenceContext_Default.Default
                : ClosedInferenceContext_Metadata.Default;

            YamlTypeInfo? typeInfo = context.GetTypeInfo(typeof(ReferencedClosedPet));
            Assert.NotNull(typeInfo);
            YamlPolymorphismOptions? polymorphismOptions = typeInfo.PolymorphismOptions;
            Assert.NotNull(polymorphismOptions);

            Assert.Collection(
                polymorphismOptions.DerivedTypes,
                derivedType =>
                {
                    Assert.Equal(typeof(ReferencedClosedCat), derivedType.DerivedType);
                    Assert.Equal(nameof(ReferencedClosedCat), derivedType.TypeDiscriminator);
                },
                derivedType =>
                {
                    Assert.Equal(typeof(ReferencedClosedLabrador), derivedType.DerivedType);
                    Assert.Equal(nameof(ReferencedClosedLabrador), derivedType.TypeDiscriminator);
                });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void RuntimeInferClosedTypePolymorphism_WithoutGeneratedMetadata_Throws(
            bool defaultMode,
            bool closedCollection)
        {
            var options = new YamlSerializerOptions
            {
                InferClosedTypePolymorphism = true,
            };

            YamlSerializerContext context = defaultMode
                ? new PolymorphicTests_Default.PolymorphicTestsContext_Default(options)
                : new PolymorphicTestsContext_Metadata(options);

            Assert.Same(options, context.Options);

            Type baseType = closedCollection ? typeof(ClosedNumberList) : typeof(ClosedShape);
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => options.GetTypeInfo(baseType));

            Assert.Contains(baseType.ToString(), exception.Message);
            Assert.Contains(
                $"{nameof(YamlSerializerOptions)}.{nameof(YamlSerializerOptions.InferClosedTypePolymorphism)}",
                exception.Message);
            Assert.Contains(
                $"{nameof(YamlSourceGenerationOptionsAttribute)}.{nameof(YamlSourceGenerationOptionsAttribute.InferClosedTypePolymorphism)}",
                exception.Message);
            Assert.Contains(
                $"{nameof(YamlPolymorphicAttribute)}.{nameof(YamlPolymorphicAttribute.InferClosedTypePolymorphism)}",
                exception.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeInferClosedTypePolymorphism_NestedHierarchyWithoutGeneratedMetadata_Throws(bool defaultMode)
        {
            var options = new YamlSerializerOptions
            {
                InferClosedTypePolymorphism = true,
            };

            YamlSerializerContext context = defaultMode
                ? new PolymorphicTests_Default.NestedPolymorphismTestsContext_Default(options)
                : new NestedPolymorphismTestsContext_Metadata(options);

            Assert.Same(options, context.Options);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => options.GetTypeInfo(typeof(ClosedPet)));

            Assert.Contains(typeof(ClosedPet).ToString(), exception.Message);
            Assert.Contains(
                $"{nameof(YamlSerializerOptions)}.{nameof(YamlSerializerOptions.InferClosedTypePolymorphism)}",
                exception.Message);
            Assert.Contains(
                $"{nameof(YamlSourceGenerationOptionsAttribute)}.{nameof(YamlSourceGenerationOptionsAttribute.InferClosedTypePolymorphism)}",
                exception.Message);
            Assert.Contains(
                $"{nameof(YamlPolymorphicAttribute)}.{nameof(YamlPolymorphicAttribute.InferClosedTypePolymorphism)}",
                exception.Message);
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(ClosedPet))]
        internal sealed partial class NestedPolymorphismTestsContext_Metadata : YamlSerializerContext;
    }

    public sealed partial class PolymorphicTests_Metadata_AsyncStream
    {
        protected override YamlSerializerOptions ClosedTypeInferenceOptions =>
            ClosedInferenceContext_Metadata.Default.Options;
    }

    public sealed partial class PolymorphicTests_Default
    {
        protected override YamlSerializerOptions ClosedTypeInferenceOptions =>
            ClosedInferenceContext_Default.Default.Options;

        [YamlSerializable(typeof(ClosedAttributeExplicitBase))]
        [YamlSerializable(typeof(ClosedAttributeOptInBase))]
        [YamlSerializable(typeof(ClosedAttributeOptOutBase))]
        [YamlSerializable(typeof(ClosedEmptyAttributeOptInBase))]
        [YamlSerializable(typeof(ClosedEmptyBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithCustomDiscriminatorBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithIgnoreUnrecognizedDiscriminatorsBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithTypeClassifierBase))]
        [YamlSerializable(typeof(ClosedEmptyOptOutWithUnknownDerivedTypeHandlingBase))]
        [YamlSerializable(typeof(ClosedEmptyPolymorphicBase))]
        [YamlSerializable(typeof(ClosedExplicitBase))]
        [YamlSerializable(typeof(ClosedExplicitOptOutBase))]
        [YamlSerializable(typeof(ClosedNumberList))]
        [YamlSerializable(typeof(ClosedShape))]
        [YamlSerializable(typeof(NonClosedAttributeBase))]
        internal sealed partial class PolymorphicTestsContext_Default;

        [YamlSerializable(typeof(ClosedPet))]
        internal sealed partial class NestedPolymorphismTestsContext_Default : YamlSerializerContext;
    }

    public sealed partial class PolymorphicTests_Default_AsyncStream
    {
        protected override YamlSerializerOptions ClosedTypeInferenceOptions =>
            ClosedInferenceContext_Default.Default.Options;
    }

    public closed class ClosedNumberList : List<int>;
    public sealed class AscendingNumberList : ClosedNumberList;
    public sealed class DescendingNumberList : ClosedNumberList;

    // These contexts intentionally include invalid inferred registrations whose diagnostics are
    // validated by the corresponding runtime-behavior tests.
#pragma warning disable STYML1229, STYML1240, STYML1241, STYML1242
    [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata, InferClosedTypePolymorphism = true)]
    [YamlSerializable(typeof(ClosedAccessBase))]
    [YamlSerializable(typeof(ClosedArrayBase<int[]>))]
    [YamlSerializable(typeof(ClosedAttributeOptInBase))]
    [YamlSerializable(typeof(ClosedCollisionBase))]
    // Both inferred types are named Node, so give their generated metadata properties unique names.
    [YamlSerializable(typeof(ClosedCollisionHolderA.Node), TypeInfoPropertyName = "ClosedCollisionNodeA")]
    [YamlSerializable(typeof(ClosedCollisionHolderB.Node), TypeInfoPropertyName = "ClosedCollisionNodeB")]
    [YamlSerializable(typeof(ClosedConcreteMismatchBase<string>))]
    [YamlSerializable(typeof(ClosedConstrainedBase<List<string>>))]
    [YamlSerializable(typeof(ClosedConstraintViolationBase<string>))]
    [YamlSerializable(typeof(ClosedContainer<int>))]
    [YamlSerializable(typeof(ClosedContainer<List<int>>))]
    [YamlSerializable(typeof(ClosedContainer<string>))]
    [YamlSerializable(typeof(ClosedCustomDiscriminatorBase))]
    [YamlSerializable(typeof(ClosedDeepJaggedBase<List<int[][][]>>))]
    [YamlSerializable(typeof(ClosedDeepJaggedMismatchBase<List<int[][]>>))]
    [YamlSerializable(typeof(ClosedDog))]
    [YamlSerializable(typeof(ClosedDuplicateArityBase<int, int>))]
    [YamlSerializable(typeof(ClosedDuplicateArityBase<int, string>))]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedOne")]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int, int>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedTwoIntInt")]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int, string>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedTwoIntString")]
    [YamlSerializable(typeof(ClosedEmptyAttributeOptInBase))]
    [YamlSerializable(typeof(ClosedEmptyBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithCustomDiscriminatorBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithIgnoreUnrecognizedDiscriminatorsBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithTypeClassifierBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithUnknownDerivedTypeHandlingBase))]
    [YamlSerializable(typeof(ClosedEmptyPolymorphicBase))]
    [YamlSerializable(typeof(ClosedExplicitBase))]
    [YamlSerializable(typeof(ClosedExplicitOptOutBase))]
    [YamlSerializable(typeof(ClosedGroundMismatchBase<int, string>))]
    [YamlSerializable(typeof(ClosedKvpBase<KeyValuePair<string, int>>))]
    [YamlSerializable(typeof(ClosedMixedBase<int>))]
    [YamlSerializable(typeof(ClosedNestedAccessContainer.Base))]
    [YamlSerializable(typeof(ClosedNestedAccessBase))]
    [YamlSerializable(typeof(ClosedNestedArgBase<ClosedNestedOuter<string>.NestedBox<int>>))]
    [YamlSerializable(typeof(ClosedNestedCollisionBase))]
    [YamlSerializable(typeof(ClosedNestedCollisionHolderA.Node), TypeInfoPropertyName = "ClosedNestedCollisionNodeA")]
    [YamlSerializable(typeof(ClosedNestedCollisionHolderB.Node), TypeInfoPropertyName = "ClosedNestedCollisionNodeB")]
    [YamlSerializable(typeof(ClosedNestedConverterRoot))]
    [YamlSerializable(typeof(ClosedNestedDerivedBase<int>))]
    [YamlSerializable(typeof(ClosedNestedEmptyRoot))]
    [YamlSerializable(typeof(ClosedNestedGenericRoot<List<int[]>>))]
    [YamlSerializable(typeof(ClosedNestedMismatchBase<ClosedNestedOuter<string>.NestedBox<int>>))]
    [YamlSerializable(typeof(ClosedOpenRoot))]
    [YamlSerializable(typeof(ClosedPair<string, int>))]
    [YamlSerializable(typeof(ClosedPartialBase<string, int>))]
    [YamlSerializable(typeof(ClosedPayload))]
    [YamlSerializable(typeof(ClosedPet))]
    [YamlSerializable(typeof(ClosedProtectedAccessBase))]
    [YamlSerializable(typeof(ClosedRepeatedBase<int, int>))]
    [YamlSerializable(typeof(ClosedRepeatedMismatchBase<int, string>))]
    [YamlSerializable(typeof(ClosedReorderedBase<int, string>))]
    [YamlSerializable(typeof(ClosedShape))]
    [YamlSerializable(typeof(ClosedShapeHolder))]
    [YamlSerializable(typeof(ClosedTupleBase<(int, string)>))]
    [YamlSerializable(typeof(ClosedUnspeakableBase<int[]>))]
    [YamlSerializable(typeof(ClosedUnspeakableBase<string>))]
    [YamlSerializable(typeof(ClosedWrappedBase<List<string>>))]
    [YamlSerializable(typeof(List<ClosedShape>))]
    [YamlSerializable(typeof(PlainAbstractBase))]
    [YamlSerializable(typeof(ReferencedClosedPet))]
    [YamlSerializable(typeof(ReferencedClosedShape))]
    internal sealed partial class ClosedInferenceContext_Metadata : YamlSerializerContext;

    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
    [YamlSerializable(typeof(ClosedAccessBase))]
    [YamlSerializable(typeof(ClosedArrayBase<int[]>))]
    [YamlSerializable(typeof(ClosedAttributeOptInBase))]
    [YamlSerializable(typeof(ClosedCollisionBase))]
    [YamlSerializable(typeof(ClosedCollisionHolderA.Node), TypeInfoPropertyName = "ClosedCollisionNodeA")]
    [YamlSerializable(typeof(ClosedCollisionHolderB.Node), TypeInfoPropertyName = "ClosedCollisionNodeB")]
    [YamlSerializable(typeof(ClosedConcreteMismatchBase<string>))]
    [YamlSerializable(typeof(ClosedConstrainedBase<List<string>>))]
    [YamlSerializable(typeof(ClosedConstraintViolationBase<string>))]
    [YamlSerializable(typeof(ClosedContainer<int>))]
    [YamlSerializable(typeof(ClosedContainer<List<int>>))]
    [YamlSerializable(typeof(ClosedContainer<string>))]
    [YamlSerializable(typeof(ClosedCustomDiscriminatorBase))]
    [YamlSerializable(typeof(ClosedDeepJaggedBase<List<int[][][]>>))]
    [YamlSerializable(typeof(ClosedDeepJaggedMismatchBase<List<int[][]>>))]
    [YamlSerializable(typeof(ClosedDog))]
    [YamlSerializable(typeof(ClosedDuplicateArityBase<int, int>))]
    [YamlSerializable(typeof(ClosedDuplicateArityBase<int, string>))]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedOne")]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int, int>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedTwoIntInt")]
    [YamlSerializable(typeof(ClosedDuplicateArityDerived<int, string>), TypeInfoPropertyName = "ClosedDuplicateArityDerivedTwoIntString")]
    [YamlSerializable(typeof(ClosedEmptyAttributeOptInBase))]
    [YamlSerializable(typeof(ClosedEmptyBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithCustomDiscriminatorBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithIgnoreUnrecognizedDiscriminatorsBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithTypeClassifierBase))]
    [YamlSerializable(typeof(ClosedEmptyOptOutWithUnknownDerivedTypeHandlingBase))]
    [YamlSerializable(typeof(ClosedEmptyPolymorphicBase))]
    [YamlSerializable(typeof(ClosedExplicitBase))]
    [YamlSerializable(typeof(ClosedExplicitOptOutBase))]
    [YamlSerializable(typeof(ClosedGroundMismatchBase<int, string>))]
    [YamlSerializable(typeof(ClosedKvpBase<KeyValuePair<string, int>>))]
    [YamlSerializable(typeof(ClosedMixedBase<int>))]
    [YamlSerializable(typeof(ClosedNestedAccessContainer.Base))]
    [YamlSerializable(typeof(ClosedNestedAccessBase))]
    [YamlSerializable(typeof(ClosedNestedArgBase<ClosedNestedOuter<string>.NestedBox<int>>))]
    [YamlSerializable(typeof(ClosedNestedCollisionBase))]
    [YamlSerializable(typeof(ClosedNestedCollisionHolderA.Node), TypeInfoPropertyName = "ClosedNestedCollisionNodeA")]
    [YamlSerializable(typeof(ClosedNestedCollisionHolderB.Node), TypeInfoPropertyName = "ClosedNestedCollisionNodeB")]
    [YamlSerializable(typeof(ClosedNestedConverterRoot))]
    [YamlSerializable(typeof(ClosedNestedDerivedBase<int>))]
    [YamlSerializable(typeof(ClosedNestedEmptyRoot))]
    [YamlSerializable(typeof(ClosedNestedGenericRoot<List<int[]>>))]
    [YamlSerializable(typeof(ClosedNestedMismatchBase<ClosedNestedOuter<string>.NestedBox<int>>))]
    [YamlSerializable(typeof(ClosedOpenRoot))]
    [YamlSerializable(typeof(ClosedPair<string, int>))]
    [YamlSerializable(typeof(ClosedPartialBase<string, int>))]
    [YamlSerializable(typeof(ClosedPayload))]
    [YamlSerializable(typeof(ClosedPet))]
    [YamlSerializable(typeof(ClosedProtectedAccessBase))]
    [YamlSerializable(typeof(ClosedRepeatedBase<int, int>))]
    [YamlSerializable(typeof(ClosedRepeatedMismatchBase<int, string>))]
    [YamlSerializable(typeof(ClosedReorderedBase<int, string>))]
    [YamlSerializable(typeof(ClosedShape))]
    [YamlSerializable(typeof(ClosedShapeHolder))]
    [YamlSerializable(typeof(ClosedTupleBase<(int, string)>))]
    [YamlSerializable(typeof(ClosedUnspeakableBase<int[]>))]
    [YamlSerializable(typeof(ClosedUnspeakableBase<string>))]
    [YamlSerializable(typeof(ClosedWrappedBase<List<string>>))]
    [YamlSerializable(typeof(List<ClosedShape>))]
    [YamlSerializable(typeof(PlainAbstractBase))]
    [YamlSerializable(typeof(ReferencedClosedPet))]
    [YamlSerializable(typeof(ReferencedClosedShape))]
    internal sealed partial class ClosedInferenceContext_Default : YamlSerializerContext;
#pragma warning restore STYML1229, STYML1240, STYML1241, STYML1242

}
