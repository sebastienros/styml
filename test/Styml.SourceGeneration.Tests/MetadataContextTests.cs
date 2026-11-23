// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    [YamlSerializable(typeof(Location), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RepeatedTypes.Location), TypeInfoPropertyName = "RepeatedLocation", GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(NumberTypes), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(CampaignSummaryViewModel), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(IndexViewModel), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(EmptyPoco), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(HighLowTemps), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(MyType), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(MyType2), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(MyTypeWithCallbacks), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(MyIntermediateType), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(HighLowTempsImmutable), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(HighLowTempsRecord), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(object[]), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(byte[]), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(string), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof((string Label1, int Label2, bool)), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(YamlDocument), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(YamlElement), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithEnumAndNullable), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithNullableProperties), GenerationMode = YamlSourceGenerationMode.Metadata)]
#if NET
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues), GenerationMode = YamlSourceGenerationMode.Metadata)]
#endif
    [YamlSerializable(typeof(ClassWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(StructWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(StructWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(StructWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(StructWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithCustomConverterNullableProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryNullableProperty), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ClassWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(StructWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(PersonStruct?), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(TypeWithValidationAttributes), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(TypeWithDerivedAttribute), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(PolymorphicClass), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(PocoWithNumberHandlingAttr), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(PocoWithMixedVisibilityMembers), GenerationMode = YamlSourceGenerationMode.Metadata)]
    internal partial class MetadataWithPerTypeAttributeContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Metadata;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    public sealed class MetadataWithPerTypeAttributeContextTests : RealWorldContextTests
    {
        public MetadataWithPerTypeAttributeContextTests() : base(MetadataWithPerTypeAttributeContext.Default, (options) => new MetadataWithPerTypeAttributeContext(options)) { }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.Location.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.RepeatedLocation.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.NumberTypes.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ActiveOrUpcomingEvent.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.IndexViewModel.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.EmptyPoco.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.HighLowTemps.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.MyType.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.MyType2.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.MyIntermediateType.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.MyNestedClass.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ByteArray.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.SourceGenSampleEnum.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.String.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.YamlElement.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithNullableProperties.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithCustomConverter.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.StructWithCustomConverter.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.StructWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.StructWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.ClassWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.StructWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MetadataWithPerTypeAttributeContext.Default.ClassWithBadCustomConverter.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MetadataWithPerTypeAttributeContext.Default.StructWithBadCustomConverter.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.PersonStruct.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(MetadataWithPerTypeAttributeContext.Default.PolymorphicClass.SerializeHandler);
        }
    }

    [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata, IncludeFields = true)]
    [YamlSerializable(typeof(Location))]
    [YamlSerializable(typeof(RepeatedTypes.Location), TypeInfoPropertyName = "RepeatedLocation")]
    [YamlSerializable(typeof(NumberTypes))]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent))]
    [YamlSerializable(typeof(CampaignSummaryViewModel))]
    [YamlSerializable(typeof(IndexViewModel))]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs))]
    [YamlSerializable(typeof(EmptyPoco))]
    [YamlSerializable(typeof(HighLowTemps))]
    [YamlSerializable(typeof(MyType))]
    [YamlSerializable(typeof(MyType2))]
    [YamlSerializable(typeof(MyTypeWithCallbacks))]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering))]
    [YamlSerializable(typeof(MyIntermediateType))]
    [YamlSerializable(typeof(HighLowTempsImmutable))]
    [YamlSerializable(typeof(HighLowTempsRecord))]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass))]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass))]
    [YamlSerializable(typeof(object[]))]
    [YamlSerializable(typeof(byte[]))]
    [YamlSerializable(typeof(string))]
    [YamlSerializable(typeof((string Label1, int Label2, bool)))]
    [YamlSerializable(typeof(YamlDocument))]
    [YamlSerializable(typeof(YamlElement))]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithEnumAndNullable))]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithNullableProperties))]
#if NET
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues))]
#endif
    [YamlSerializable(typeof(ClassWithCustomConverter))]
    [YamlSerializable(typeof(StructWithCustomConverter))]
    [YamlSerializable(typeof(ClassWithCustomConverterFactory))]
    [YamlSerializable(typeof(StructWithCustomConverterFactory))]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty))]
    [YamlSerializable(typeof(StructWithCustomConverterProperty))]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryProperty))]
    [YamlSerializable(typeof(StructWithCustomConverterFactoryProperty))]
    [YamlSerializable(typeof(ClassWithCustomConverterNullableProperty))]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryNullableProperty))]
    [YamlSerializable(typeof(ClassWithBadCustomConverter))]
    [YamlSerializable(typeof(StructWithBadCustomConverter))]
    [YamlSerializable(typeof(PersonStruct?))]
    [YamlSerializable(typeof(TypeWithValidationAttributes))]
    [YamlSerializable(typeof(TypeWithDerivedAttribute))]
    [YamlSerializable(typeof(PolymorphicClass))]
    [YamlSerializable(typeof(PocoWithNumberHandlingAttr))]
    [YamlSerializable(typeof(PocoWithMixedVisibilityMembers))]
    internal partial class MetadataContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Metadata;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    [YamlConverter(typeof(YamlStringEnumConverter<EnumWrittenAsString>))]
    public enum EnumWrittenAsString
    {
        A = 1
    }

    [YamlSerializable(typeof(EnumWrittenAsString))]
    public partial class ContextWithExplicitStringEnum : YamlSerializerContext
    {
    }

    public class PocoWithEnum
    {
        public EnumWrittenAsString MyEnum { get; set; }
    }

    [YamlSerializable(typeof(PocoWithEnum))]
    public partial class ContextWithImplicitStringEnum : YamlSerializerContext
    {
    }

    public sealed class MetadataContextTests : RealWorldContextTests
    {
        public MetadataContextTests() : base(MetadataContext.Default, (options) => new MetadataContext(options)) { }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.Null(MetadataContext.Default.Location.SerializeHandler);
            Assert.Null(MetadataContext.Default.RepeatedLocation.SerializeHandler);
            Assert.Null(MetadataContext.Default.NumberTypes.SerializeHandler);
            Assert.Null(MetadataContext.Default.ActiveOrUpcomingEvent.SerializeHandler);
            Assert.Null(MetadataContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.Null(MetadataContext.Default.IndexViewModel.SerializeHandler);
            Assert.Null(MetadataContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.Null(MetadataContext.Default.EmptyPoco.SerializeHandler);
            Assert.Null(MetadataContext.Default.HighLowTemps.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyType.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyType2.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyTypeWithCallbacks.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyTypeWithPropertyOrdering.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyIntermediateType.SerializeHandler);
            Assert.Null(MetadataContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.Null(MetadataContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyNestedClass.SerializeHandler);
            Assert.Null(MetadataContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(MetadataContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(MetadataContext.Default.ByteArray.SerializeHandler);
            Assert.Null(MetadataContext.Default.SourceGenSampleEnum.SerializeHandler);
            Assert.Null(MetadataContext.Default.String.SerializeHandler);
            Assert.Null(MetadataContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(MetadataContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(MetadataContext.Default.YamlElement.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithNullableProperties.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithCustomConverter.SerializeHandler);
            Assert.Null(MetadataContext.Default.StructWithCustomConverter.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MetadataContext.Default.StructWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MetadataContext.Default.StructWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MetadataContext.Default.ClassWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Null(MetadataContext.Default.StructWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MetadataContext.Default.ClassWithBadCustomConverter.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MetadataContext.Default.StructWithBadCustomConverter.SerializeHandler);
            Assert.Null(MetadataContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.Null(MetadataContext.Default.PersonStruct.SerializeHandler);
            Assert.Null(MetadataContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.Null(MetadataContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(MetadataContext.Default.PolymorphicClass.SerializeHandler);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void EnsureHelperMethodGenerated_TypeFactory()
        {
            // There are 2 helper methods generated for obtaining a converter from a factory:
            // - YamlConverter<T> version that is property-based (that calls the one below)
            // - YamlConverter version that is Type-based
            // and this test verifies the latter one is generated. Other tests also have property-level
            // factories and thus verify both are created.

            const string Yaml = "\"A\"";

            EnumWrittenAsString obj = EnumWrittenAsString.A;

            string yaml = YamlSerializer.Serialize(obj, ContextWithExplicitStringEnum.Default.EnumWrittenAsString);
            Assert.Equal(Yaml, yaml);

            obj = YamlSerializer.Deserialize(Yaml, ContextWithExplicitStringEnum.Default.EnumWrittenAsString);
            Assert.Equal(EnumWrittenAsString.A, obj);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void EnsureHelperMethodGenerated_ImplicitPropertyFactory()
        {
            // ContextWithImplicitStringEnum does not have an entry for EnumWrittenAsString since it is
            // implicitly added by PocoWithEnum. Verify helper methods are still being created properly.

            const string Yaml = "{\"MyEnum\":\"A\"}";

            PocoWithEnum obj = new() { MyEnum = EnumWrittenAsString.A };

            string yaml = YamlSerializer.Serialize(obj, ContextWithImplicitStringEnum.Default.PocoWithEnum);
            Assert.Equal(Yaml, yaml);

            obj = YamlSerializer.Deserialize(Yaml, ContextWithImplicitStringEnum.Default.PocoWithEnum);
            Assert.Equal(EnumWrittenAsString.A, obj.MyEnum);
        }
    }
}
