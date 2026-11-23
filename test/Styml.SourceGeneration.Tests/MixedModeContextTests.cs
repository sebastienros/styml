// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    [YamlSourceGenerationOptions(IncludeFields = true)]
    [YamlSerializable(typeof(Location), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RepeatedTypes.Location), TypeInfoPropertyName = "RepeatedLocation", GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(NumberTypes), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(CampaignSummaryViewModel), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(IndexViewModel), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(EmptyPoco), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTemps), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyType), GenerationMode = YamlSourceGenerationMode.Default)]
    [YamlSerializable(typeof(MyType2), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithCallbacks), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyIntermediateType), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTempsImmutable), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(HighLowTempsRecord), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(object[]), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(byte[]), GenerationMode = YamlSourceGenerationMode.Metadata)]
    [YamlSerializable(typeof(string), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof((string Label1, int Label2, bool)), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlDocument), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlElement), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithEnumAndNullable), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithNullableProperties), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
#if NET
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
#endif
    [YamlSerializable(typeof(ClassWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterNullableProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryNullableProperty), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PersonStruct?), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithValidationAttributes), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithDerivedAttribute), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PolymorphicClass), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithNumberHandlingAttr), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithMixedVisibilityMembers), GenerationMode = YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization)]
    internal partial class MixedModeContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Metadata | YamlSourceGenerationMode.Serialization;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    public sealed class MixedModeContextTests : RealWorldContextTests
    {
        public MixedModeContextTests() : base(MixedModeContext.Default, (options) => new MixedModeContext(options)) { }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.Null(MixedModeContext.Default.Location.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.RepeatedLocation.SerializeHandler);
            Assert.Null(MixedModeContext.Default.NumberTypes.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.Null(MixedModeContext.Default.IndexViewModel.SerializeHandler);
            Assert.Null(MixedModeContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.EmptyPoco.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.HighLowTemps.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyType.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyType2.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyTypeWithCallbacks.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyTypeWithPropertyOrdering.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyIntermediateType.SerializeHandler);
            Assert.Null(MixedModeContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.Null(MixedModeContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyNestedClass.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ByteArray.SerializeHandler);
            Assert.Null(MixedModeContext.Default.SourceGenSampleEnum.SerializeHandler);
            Assert.Null(MixedModeContext.Default.String.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(MixedModeContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(MixedModeContext.Default.YamlElement.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.ClassWithNullableProperties.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ClassWithCustomConverter.SerializeHandler);
            Assert.Null(MixedModeContext.Default.StructWithCustomConverter.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ClassWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MixedModeContext.Default.StructWithCustomConverterFactory.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ClassWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MixedModeContext.Default.StructWithCustomConverterProperty.SerializeHandler);
            Assert.Null(MixedModeContext.Default.ClassWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Null(MixedModeContext.Default.StructWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MixedModeContext.Default.ClassWithBadCustomConverter.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => MixedModeContext.Default.StructWithBadCustomConverter.SerializeHandler);
            Assert.Null(MixedModeContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.PersonStruct.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.NotNull(MixedModeContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(MixedModeContext.Default.PolymorphicClass.SerializeHandler);
        }

        [Fact]
        public override void RoundTripIndexViewModel()
        {
            IndexViewModel expected = CreateIndexViewModel();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.IndexViewModel);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.IndexViewModel), typeof(CampaignSummaryViewModel));

            IndexViewModel obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).IndexViewModel);
            VerifyIndexViewModel(expected, obj);
        }

        [Fact]
        public override void RoundTripCampaignSummaryViewModel()
        {
            CampaignSummaryViewModel expected = CreateCampaignSummaryViewModel();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.CampaignSummaryViewModel);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.CampaignSummaryViewModel), typeof(CampaignSummaryViewModel));

            CampaignSummaryViewModel obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).CampaignSummaryViewModel);
            VerifyCampaignSummaryViewModel(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.CampaignSummaryViewModel);
        }

        [Fact]
        public override void RoundTripCollectionsDictionary()
        {
            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.WeatherForecastWithPOCOs);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.WeatherForecastWithPOCOs), typeof(HighLowTemps));

            WeatherForecastWithPOCOs obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).WeatherForecastWithPOCOs);
            VerifyWeatherForecastWithPOCOs(expected, obj);
        }

        [Fact]
        public override void RoundTripEmptyPoco()
        {
            EmptyPoco expected = CreateEmptyPoco();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.EmptyPoco);
            // This would have thrown if we tried to lookup any properties but since there are no properties this is able to complete.
            EmptyPoco obj = YamlSerializer.Deserialize(yaml, DefaultContext.EmptyPoco);
            VerifyEmptyPoco(expected, obj);

            obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).EmptyPoco);
            VerifyEmptyPoco(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.EmptyPoco);
        }

        [Fact]
        public override void RoundTripTypeNameClash()
        {
            RepeatedTypes.Location expected = CreateRepeatedLocation();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.RepeatedLocation);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.RepeatedLocation), typeof(RepeatedTypes.Location));

            RepeatedTypes.Location obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).RepeatedLocation);
            VerifyRepeatedLocation(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.RepeatedLocation);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public override void HandlesNestedTypes()
        {
            string yaml = """{"MyInt":5}""";
            MyNestedClass obj = YamlSerializer.Deserialize<MyNestedClass>(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).MyNestedClass);
            Assert.Equal(5, obj.MyInt);
            Assert.Equal(yaml, YamlSerializer.Serialize(obj, DefaultContext.MyNestedClass));

            MyNestedClass.MyNestedNestedClass obj2 = YamlSerializer.Deserialize<MyNestedClass.MyNestedNestedClass>(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).MyNestedNestedClass);
            Assert.Equal(5, obj2.MyInt);
            Assert.Equal(yaml, YamlSerializer.Serialize(obj2, DefaultContext.MyNestedNestedClass));
        }

        [Fact]
        public override void SerializeObjectArray()
        {
            IndexViewModel index = CreateIndexViewModel();
            CampaignSummaryViewModel campaignSummary = CreateCampaignSummaryViewModel();

            string yaml = YamlSerializer.Serialize(new object[] { index, campaignSummary }, DefaultContext.ObjectArray);
            object[] arr = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ObjectArray);

            YamlElement indexAsYamlElement = (YamlElement)arr[0];
            YamlElement campaignSummeryAsYamlElement = (YamlElement)arr[1];
            VerifyIndexViewModel(index, YamlSerializer.Deserialize(indexAsYamlElement.GetRawText(), ((ITestContext)MetadataWithPerTypeAttributeContext.Default).IndexViewModel));
            VerifyCampaignSummaryViewModel(campaignSummary, YamlSerializer.Deserialize(campaignSummeryAsYamlElement.GetRawText(), ((ITestContext)MetadataWithPerTypeAttributeContext.Default).CampaignSummaryViewModel));
        }

        [Fact]
        public override void SerializeObjectArray_WithCustomOptions()
        {
            IndexViewModel index = CreateIndexViewModel();
            CampaignSummaryViewModel campaignSummary = CreateCampaignSummaryViewModel();

            ITestContext context = SerializationContextWithCamelCase.Default;
            Assert.Same(YamlNamingPolicy.CamelCase, ((YamlSerializerContext)context).Options.PropertyNamingPolicy);

            string yaml = YamlSerializer.Serialize(new object[] { index, campaignSummary }, context.ObjectArray);
            object[] arr = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ObjectArray);

            YamlElement indexAsYamlElement = (YamlElement)arr[0];
            YamlElement campaignSummeryAsYamlElement = (YamlElement)arr[1];

            ITestContext metadataContext = new MetadataContext(new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase });
            VerifyIndexViewModel(index, YamlSerializer.Deserialize(indexAsYamlElement.GetRawText(), metadataContext.IndexViewModel));
            VerifyCampaignSummaryViewModel(campaignSummary, YamlSerializer.Deserialize(campaignSummeryAsYamlElement.GetRawText(), metadataContext.CampaignSummaryViewModel));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void OnSerializeCallbacks_WithCustomOptions()
        {
            MyTypeWithCallbacks obj = new();
            Assert.Null(obj.MyProperty);

            ITestContext context = SerializationContextWithCamelCase.Default;
            Assert.Same(YamlNamingPolicy.CamelCase, ((YamlSerializerContext)context).Options.PropertyNamingPolicy);

            string yaml = YamlSerializer.Serialize(obj, context.MyTypeWithCallbacks);
            Assert.Equal("{\"myProperty\":\"Before\"}", yaml);
            Assert.Equal("After", obj.MyProperty);

            context = new MetadataContext(new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase });
            yaml = YamlSerializer.Serialize(obj, context.MyTypeWithCallbacks);
            Assert.Equal("{\"myProperty\":\"Before\"}", yaml);
            Assert.Equal("After", obj.MyProperty);
        }
    }
}
