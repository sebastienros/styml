// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed class NullableAnnotationsTests_Metadata_String : NullableAnnotationsTests_Metadata
    {
        public NullableAnnotationsTests_Metadata_String()
            : base(new StringSerializerWrapper(NullableAnnotationsTestsContext_Metadata.Default)) { }
    }

    public sealed class NullableAnnotationsTests_Metadata_AsyncStream : NullableAnnotationsTests_Metadata
    {
        public NullableAnnotationsTests_Metadata_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(NullableAnnotationsTestsContext_Metadata.Default)) { }
    }

    public abstract partial class NullableAnnotationsTests_Metadata : NullableAnnotationsTests
    {
        protected NullableAnnotationsTests_Metadata(YamlSerializerWrapper serializer)
            : base(serializer) { }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata, RespectNullableAnnotations = false)]
        [YamlSerializable(typeof(NotNullablePropertyClass))]
        [YamlSerializable(typeof(NotNullableReadonlyPropertyClass))]
        [YamlSerializable(typeof(NotNullableFieldClass))]
        [YamlSerializable(typeof(NotNullableSpecialTypePropertiesClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertiesLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullPropertyClass))]
        [YamlSerializable(typeof(MaybeNullPropertyClass))]
        [YamlSerializable(typeof(AllowNullPropertyClass))]
        [YamlSerializable(typeof(DisallowNullPropertyClass))]
        [YamlSerializable(typeof(AllowNullConstructorParameter))]
        [YamlSerializable(typeof(DisallowNullConstructorParameter))]
        [YamlSerializable(typeof(NullStructPropertyClass))]
        [YamlSerializable(typeof(NullStructConstructorParameterClass))]
        [YamlSerializable(typeof(NotNullStructPropertyClass))]
        [YamlSerializable(typeof(DisallowNullStructPropertyClass))]
        [YamlSerializable(typeof(DisallowNullStructConstructorParameter))]
        [YamlSerializable(typeof(NotNullPropertyClass<string>))]
        [YamlSerializable(typeof(MaybeNullPropertyClass<string>))]
        [YamlSerializable(typeof(AllowNullPropertyClass<string>))]
        [YamlSerializable(typeof(DisallowNullPropertyClass<string>))]
        [YamlSerializable(typeof(AllowNullConstructorParameter<string>))]
        [YamlSerializable(typeof(DisallowNullConstructorParameter<string>))]
        [YamlSerializable(typeof(NullablePropertyClass))]
        [YamlSerializable(typeof(NullableFieldClass))]
        [YamlSerializable(typeof(NullableObliviousPropertyClass))]
        [YamlSerializable(typeof(NullableObliviousConstructorParameter))]
        [YamlSerializable(typeof(GenericPropertyClass<string>))]
        [YamlSerializable(typeof(NullableGenericPropertyClass<string>))]
        [YamlSerializable(typeof(NotNullGenericPropertyClass<string>))]
        [YamlSerializable(typeof(GenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NullableGenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NotNullGenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NotNullablePropertyWithIgnoreConditions))]
        [YamlSerializable(typeof(ClassWithNullableInitProperty))]
        [YamlSerializable(typeof(ClassWithNonNullableInitProperty))]
        [YamlSerializable(typeof(ClassWithNullableRequiredProperty))]
        [YamlSerializable(typeof(ClassWithNonNullableRequiredProperty))]
        internal sealed partial class NullableAnnotationsTestsContext_Metadata
            : YamlSerializerContext { }
    }

    public sealed class NullableAnnotationsTests_Default_String : NullableAnnotationsTests_Default
    {
        public NullableAnnotationsTests_Default_String()
            : base(new StringSerializerWrapper(NullableAnnotationsTestsContext_Default.Default)) { }
    }

    public sealed class NullableAnnotationsTests_Default_AsyncStream : NullableAnnotationsTests_Default
    {
        public NullableAnnotationsTests_Default_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(NullableAnnotationsTestsContext_Default.Default)) { }
    }

    public abstract partial class NullableAnnotationsTests_Default : NullableAnnotationsTests
    {
        protected NullableAnnotationsTests_Default(YamlSerializerWrapper serializer)
            : base(serializer) { }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Default, RespectNullableAnnotations = false)]
        [YamlSerializable(typeof(NotNullablePropertyClass))]
        [YamlSerializable(typeof(NotNullableReadonlyPropertyClass))]
        [YamlSerializable(typeof(NotNullableFieldClass))]
        [YamlSerializable(typeof(NotNullableSpecialTypePropertiesClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterClass))]
        [YamlSerializable(typeof(NotNullablePropertyParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertiesLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithHandleNullConverterLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullablePropertyWithAlwaysNullConverterLargeParameterizedCtorClass))]
        [YamlSerializable(typeof(NotNullPropertyClass))]
        [YamlSerializable(typeof(MaybeNullPropertyClass))]
        [YamlSerializable(typeof(AllowNullPropertyClass))]
        [YamlSerializable(typeof(DisallowNullPropertyClass))]
        [YamlSerializable(typeof(AllowNullConstructorParameter))]
        [YamlSerializable(typeof(DisallowNullConstructorParameter))]
        [YamlSerializable(typeof(NullStructPropertyClass))]
        [YamlSerializable(typeof(NullStructConstructorParameterClass))]
        [YamlSerializable(typeof(NotNullStructPropertyClass))]
        [YamlSerializable(typeof(DisallowNullStructPropertyClass))]
        [YamlSerializable(typeof(DisallowNullStructConstructorParameter))]
        [YamlSerializable(typeof(NotNullPropertyClass<string>))]
        [YamlSerializable(typeof(MaybeNullPropertyClass<string>))]
        [YamlSerializable(typeof(AllowNullPropertyClass<string>))]
        [YamlSerializable(typeof(DisallowNullPropertyClass<string>))]
        [YamlSerializable(typeof(AllowNullConstructorParameter<string>))]
        [YamlSerializable(typeof(DisallowNullConstructorParameter<string>))]
        [YamlSerializable(typeof(NullablePropertyClass))]
        [YamlSerializable(typeof(NullableFieldClass))]
        [YamlSerializable(typeof(NullableObliviousPropertyClass))]
        [YamlSerializable(typeof(NullableObliviousConstructorParameter))]
        [YamlSerializable(typeof(GenericPropertyClass<string>))]
        [YamlSerializable(typeof(NullableGenericPropertyClass<string>))]
        [YamlSerializable(typeof(NotNullGenericPropertyClass<string>))]
        [YamlSerializable(typeof(GenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NullableGenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NotNullGenericConstructorParameter<string>))]
        [YamlSerializable(typeof(NotNullablePropertyWithIgnoreConditions))]
        [YamlSerializable(typeof(ClassWithNullableInitProperty))]
        [YamlSerializable(typeof(ClassWithNonNullableInitProperty))]
        [YamlSerializable(typeof(ClassWithNullableRequiredProperty))]
        [YamlSerializable(typeof(ClassWithNonNullableRequiredProperty))]
        internal sealed partial class NullableAnnotationsTestsContext_Default
            : YamlSerializerContext
        { }
    }
}
