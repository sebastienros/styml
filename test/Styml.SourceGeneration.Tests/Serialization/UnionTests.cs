// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class UnionTests_Metadata : UnionTests
    {
        public UnionTests_Metadata()
            : base(new StringSerializerWrapper(UnionTestsContext_Metadata.Default))
        {
        }

#pragma warning disable STYML1227
        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(MixedUnion))]
        [YamlSerializable(typeof(ObjectUnion))]
        [YamlSerializable(typeof(Payload))]
        [YamlSerializable(typeof(HierarchyUnion))]
        [YamlSerializable(typeof(HierarchyNullableUnion))]
        [YamlSerializable(typeof(NullableCaseUnion))]
        [YamlSerializable(typeof(NoNullableCaseUnion))]
        [YamlSerializable(typeof(NoNullableCaseUnionContainer))]
        [YamlSerializable(typeof(IntOrBool))]
        [YamlSerializable(typeof(IntOrBoolContainer))]
        [YamlSerializable(typeof(ValueTypeNullablePairUnion))]
        [YamlSerializable(typeof(SingleNullableValueTypeUnion))]
        [YamlSerializable(typeof(SingleObjectUnion))]
        [YamlSerializable(typeof(ClassifiedPayloadUnion))]
        [YamlSerializable(typeof(UnionWithCustomClassifier))]
        [YamlSerializable(typeof(UnionWithCustomConverterCase))]
        [YamlSerializable(typeof(UnionWithIntAndLongCase))]
        [YamlSerializable(typeof(UnionWithMixedAmbiguity))]
        [YamlSerializable(typeof(IntOrString))]
        [YamlSerializable(typeof(NumberHandlingUnion))]
        [YamlSerializable(typeof(AsyncEnumerableUnion))]
        [YamlSerializable(typeof(CustomCase))]
        [YamlSerializable(typeof(OtherCase))]
        [YamlSerializable(typeof(PayloadCase))]
        [YamlSerializable(typeof(UnionContainer))]
        [YamlSerializable(typeof(NullableScalarUnionContainer))]
        [YamlSerializable(typeof(UserDefinedAttributeOnlyUnion))]
        [YamlSerializable(typeof(UserDefinedNullableAttributeOnlyUnion))]
        [YamlSerializable(typeof(UserDefinedCtorVsImplicitOpUnion))]
        [YamlSerializable(typeof(UserDefinedYamlUnionOnPlainObject))]
        [YamlSerializable(typeof(UserDefinedUnionViaIUnion))]
        [YamlSerializable(typeof(UserDefinedValueTypeUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedAnimalUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedAnimalUnion_NoConvention))]
        [YamlSerializable(typeof(CustomDiscriminatedScalarUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedFlora))]
        [YamlSerializable(typeof(NullableEnumUnion))]
        [YamlSerializable(typeof(RecursiveNat))]
        [YamlSerializable(typeof(RecursiveNatReversed))]
        [YamlSerializable(typeof(NullableNat))]
        [YamlSerializable(typeof(SelfReferentialUnion))]
        [YamlSerializable(typeof(ShapeUnion))]
        [YamlSerializable(typeof(WritableUnion))]
        [YamlSerializable(typeof(NodeUnion))]
        internal sealed partial class UnionTestsContext_Metadata : YamlSerializerContext
        {
        }
#pragma warning restore STYML1227
    }

    public sealed partial class UnionTests_Metadata_AsyncStream : UnionTests
    {
        public UnionTests_Metadata_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(UnionTests_Metadata.UnionTestsContext_Metadata.Default))
        {
        }
    }

    public sealed partial class UnionTests_Default : UnionTests
    {
        public UnionTests_Default()
            : base(new StringSerializerWrapper(UnionTestsContext_Default.Default))
        {
        }

#pragma warning disable STYML1227
        [YamlSerializable(typeof(MixedUnion))]
        [YamlSerializable(typeof(ObjectUnion))]
        [YamlSerializable(typeof(Payload))]
        [YamlSerializable(typeof(HierarchyUnion))]
        [YamlSerializable(typeof(HierarchyNullableUnion))]
        [YamlSerializable(typeof(NullableCaseUnion))]
        [YamlSerializable(typeof(NoNullableCaseUnion))]
        [YamlSerializable(typeof(NoNullableCaseUnionContainer))]
        [YamlSerializable(typeof(IntOrBool))]
        [YamlSerializable(typeof(IntOrBoolContainer))]
        [YamlSerializable(typeof(ValueTypeNullablePairUnion))]
        [YamlSerializable(typeof(SingleNullableValueTypeUnion))]
        [YamlSerializable(typeof(SingleObjectUnion))]
        [YamlSerializable(typeof(ClassifiedPayloadUnion))]
        [YamlSerializable(typeof(UnionWithCustomClassifier))]
        [YamlSerializable(typeof(UnionWithCustomConverterCase))]
        [YamlSerializable(typeof(UnionWithIntAndLongCase))]
        [YamlSerializable(typeof(UnionWithMixedAmbiguity))]
        [YamlSerializable(typeof(IntOrString))]
        [YamlSerializable(typeof(NumberHandlingUnion))]
        [YamlSerializable(typeof(AsyncEnumerableUnion))]
        [YamlSerializable(typeof(CustomCase))]
        [YamlSerializable(typeof(OtherCase))]
        [YamlSerializable(typeof(PayloadCase))]
        [YamlSerializable(typeof(UnionContainer))]
        [YamlSerializable(typeof(NullableScalarUnionContainer))]
        [YamlSerializable(typeof(UserDefinedAttributeOnlyUnion))]
        [YamlSerializable(typeof(UserDefinedNullableAttributeOnlyUnion))]
        [YamlSerializable(typeof(UserDefinedCtorVsImplicitOpUnion))]
        [YamlSerializable(typeof(UserDefinedYamlUnionOnPlainObject))]
        [YamlSerializable(typeof(UserDefinedUnionViaIUnion))]
        [YamlSerializable(typeof(UserDefinedValueTypeUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedAnimalUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedAnimalUnion_NoConvention))]
        [YamlSerializable(typeof(CustomDiscriminatedScalarUnion))]
        [YamlSerializable(typeof(CustomDiscriminatedFlora))]
        [YamlSerializable(typeof(NullableEnumUnion))]
        [YamlSerializable(typeof(RecursiveNat))]
        [YamlSerializable(typeof(RecursiveNatReversed))]
        [YamlSerializable(typeof(NullableNat))]
        [YamlSerializable(typeof(SelfReferentialUnion))]
        [YamlSerializable(typeof(ShapeUnion))]
        [YamlSerializable(typeof(WritableUnion))]
        [YamlSerializable(typeof(NodeUnion))]
        internal sealed partial class UnionTestsContext_Default : YamlSerializerContext
        {
        }
#pragma warning restore STYML1227
    }

    public sealed partial class UnionTests_Default_AsyncStream : UnionTests
    {
        public UnionTests_Default_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(UnionTests_Default.UnionTestsContext_Default.Default))
        {
        }
    }
}
