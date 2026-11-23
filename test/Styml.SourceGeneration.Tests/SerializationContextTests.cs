// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization)]
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
    [YamlSerializable(typeof(ClassWithConflictingIgnoredProperties))]
    internal partial class SerializationContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Serialization;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    [YamlSerializable(typeof(Location), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RepeatedTypes.Location), GenerationMode = YamlSourceGenerationMode.Serialization, TypeInfoPropertyName = "RepeatedLocation")]
    [YamlSerializable(typeof(NumberTypes), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(CampaignSummaryViewModel), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(IndexViewModel), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(EmptyPoco), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTemps), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyType), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyType2), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithCallbacks), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyIntermediateType), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTempsImmutable), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTempsRecord), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(object[]), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(byte[]), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(string), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof((string Label1, int Label2, bool)), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlDocument), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlElement), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithEnumAndNullable), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithNullableProperties), GenerationMode = YamlSourceGenerationMode.Serialization)]
#if NET
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues), GenerationMode = YamlSourceGenerationMode.Serialization)]
#endif
    [YamlSerializable(typeof(ClassWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterNullableProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryNullableProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PersonStruct?), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithValidationAttributes), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithDerivedAttribute), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PolymorphicClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithNumberHandlingAttr), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithMixedVisibilityMembers), GenerationMode = YamlSourceGenerationMode.Serialization)]
    internal partial class SerializationWithPerTypeAttributeContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Serialization;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    [YamlSourceGenerationOptions(PropertyNamingPolicy = YamlKnownNamingPolicy.CamelCase, IncludeFields = true)]
    [YamlSerializable(typeof(Location), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RepeatedTypes.Location), GenerationMode = YamlSourceGenerationMode.Serialization, TypeInfoPropertyName = "RepeatedLocation")]
    [YamlSerializable(typeof(NumberTypes), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(CampaignSummaryViewModel), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(IndexViewModel), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(EmptyPoco), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTemps), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyType), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyType2), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithCallbacks), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(MyIntermediateType), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTempsImmutable), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(HighLowTempsRecord), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(object[]), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(byte[]), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(string), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof((string Label1, int Label2, bool)), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlDocument), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(YamlElement), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithEnumAndNullable), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithNullableProperties), GenerationMode = YamlSourceGenerationMode.Serialization)]
#if NET
    [YamlSerializable(typeof(RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues), GenerationMode = YamlSourceGenerationMode.Serialization)]
#endif
    [YamlSerializable(typeof(ClassWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactory), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithCustomConverterFactoryProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterNullableProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithCustomConverterFactoryNullableProperty), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(ClassWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(StructWithBadCustomConverter), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PersonStruct?), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithValidationAttributes), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(TypeWithDerivedAttribute), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PolymorphicClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithNumberHandlingAttr), GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(PocoWithMixedVisibilityMembers), GenerationMode = YamlSourceGenerationMode.Serialization)]
    internal partial class SerializationContextWithCamelCase : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Serialization;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    public class SerializationContextTests : RealWorldContextTests
    {
        public SerializationContextTests() : this(SerializationContext.Default, (options) => new SerializationContext(options)) { }

        internal SerializationContextTests(ITestContext defaultContext, Func<YamlSerializerOptions, ITestContext> contextCreator)
            : base(defaultContext, contextCreator)
        {
        }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.NotNull(SerializationContext.Default.Location.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.RepeatedLocation.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.NumberTypes.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.ActiveOrUpcomingEvent.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.IndexViewModel.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.HighLowTemps.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyType.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyType2.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyTypeWithCallbacks.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyTypeWithPropertyOrdering.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyIntermediateType.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyNestedClass.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(SerializationContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(SerializationContext.Default.ByteArray.SerializeHandler);
            Assert.Null(SerializationContext.Default.String.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(SerializationContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(SerializationContext.Default.YamlElement.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.Null(SerializationContext.Default.ClassWithCustomConverter.SerializeHandler);
            Assert.Null(SerializationContext.Default.StructWithCustomConverter.SerializeHandler);
            Assert.Null(SerializationContext.Default.ClassWithCustomConverterFactory.SerializeHandler);
            Assert.Null(SerializationContext.Default.StructWithCustomConverterFactory.SerializeHandler);
            Assert.Null(SerializationContext.Default.ClassWithCustomConverterProperty.SerializeHandler);
            Assert.Null(SerializationContext.Default.StructWithCustomConverterProperty.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => SerializationContext.Default.ClassWithBadCustomConverter.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => SerializationContext.Default.StructWithBadCustomConverter.SerializeHandler);
            Assert.Null(SerializationContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.PersonStruct.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.NotNull(SerializationContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(SerializationContext.Default.PolymorphicClass.SerializeHandler);
        }

        [Fact]
        public void TypeWithoutMetadataOrFastPathHandler_SerializationThrowsInvalidOperationException()
        {
            YamlTypeInfo<ClassWithCustomConverterProperty> typeInfo = SerializationContext.Default.ClassWithCustomConverterProperty;
            Assert.Null(typeInfo.SerializeHandler);
            Assert.Empty(typeInfo.Properties);

            var value = new ClassWithCustomConverterProperty();
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, typeInfo));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("""{"Property":42}""", typeInfo));
        }

        [Fact]
        public override void RoundTripLocation()
        {
            Location expected = CreateLocation();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.Location);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.Location), typeof(Location));

            Location obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).Location);
            VerifyLocation(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.Location);
        }

        [Fact]
        public override void RoundTripNumberTypes()
        {
            NumberTypes expected = CreateNumberTypes();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.NumberTypes);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.NumberTypes), typeof(NumberTypes));

            NumberTypes obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).NumberTypes);
            VerifyNumberTypes(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.NumberTypes);
        }

        [Fact]
        public override void RoundTripIndexViewModel()
        {
            IndexViewModel expected = CreateIndexViewModel();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.IndexViewModel);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.IndexViewModel), typeof(IndexViewModel));

            IndexViewModel obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).IndexViewModel);
            VerifyIndexViewModel(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.IndexViewModel);
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
        public override void RoundTripActiveOrUpcomingEvent()
        {
            ActiveOrUpcomingEvent expected = CreateActiveOrUpcomingEvent();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.ActiveOrUpcomingEvent);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.ActiveOrUpcomingEvent), typeof(ActiveOrUpcomingEvent));

            ActiveOrUpcomingEvent obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ActiveOrUpcomingEvent);
            VerifyActiveOrUpcomingEvent(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.ActiveOrUpcomingEvent);
        }

        [Fact]
        public override void RoundTripCollectionsDictionary()
        {
            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.WeatherForecastWithPOCOs);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.WeatherForecastWithPOCOs), typeof(WeatherForecastWithPOCOs));

            WeatherForecastWithPOCOs obj = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).WeatherForecastWithPOCOs);
            VerifyWeatherForecastWithPOCOs(expected, obj);

            AssertFastPathLogicCorrect(yaml, obj, DefaultContext.WeatherForecastWithPOCOs);
        }

        [Fact]
        public override void RoundTripEmptyPoco()
        {
            EmptyPoco expected = CreateEmptyPoco();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.EmptyPoco);
            // This would have thrown on the first property lookup but JSON is empty here
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
        public override void NestedSameTypeWorks()
        {
            MyType myType = new() { Type = new() };
            string yaml = YamlSerializer.Serialize(myType, DefaultContext.MyType);
            myType = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).MyType);
            AssertFastPathLogicCorrect(yaml, myType, DefaultContext.MyType);

            MyType2 myType2 = new() { Type = new MyIntermediateType() { Type = myType } };
            yaml = YamlSerializer.Serialize(myType2, DefaultContext.MyType2);
            myType2 = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).MyType2);
            AssertFastPathLogicCorrect(yaml, myType2, DefaultContext.MyType2);
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
            // Verify JSON was written with camel casing.
            Assert.Contains("activeOrUpcomingEvents", yaml);
            Assert.Contains("featuredCampaign", yaml);
            Assert.Contains("description", yaml);
            Assert.Contains("organizationName", yaml);

            object[] arr = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ObjectArray);

            YamlElement indexAsYamlElement = (YamlElement)arr[0];
            YamlElement campaignSummeryAsYamlElement = (YamlElement)arr[1];

            ITestContext metadataContext = new MetadataContext(new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase });
            VerifyIndexViewModel(index, YamlSerializer.Deserialize(indexAsYamlElement.GetRawText(), metadataContext.IndexViewModel));
            VerifyCampaignSummaryViewModel(campaignSummary, YamlSerializer.Deserialize(campaignSummeryAsYamlElement.GetRawText(), metadataContext.CampaignSummaryViewModel));
        }

        [Fact]
        public override void SerializeObjectArray_SimpleTypes_WithCustomOptions()
        {
            YamlSerializerOptions options = new() { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
            ITestContext context = new SerializationContext(options);

            string yaml = YamlSerializer.Serialize(new object[] { "Hello", "World" }, typeof(object[]), (YamlSerializerContext)context);
            object[] arr = (object[])YamlSerializer.Deserialize(yaml, typeof(object[]), (YamlSerializerContext)((ITestContext)MetadataWithPerTypeAttributeContext.Default));

            YamlElement hello = (YamlElement)arr[0];
            YamlElement world = (YamlElement)arr[1];
            Assert.Equal("\"Hello\"", hello.GetRawText());
            Assert.Equal("\"World\"", world.GetRawText());
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
        public override void EnumAndNullable()
        {
            RunTest(new ClassWithEnumAndNullable() { Day = DayOfWeek.Monday, NullableDay = DayOfWeek.Tuesday });
            RunTest(new ClassWithEnumAndNullable());

            void RunTest(ClassWithEnumAndNullable expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithEnumAndNullable);
                ClassWithEnumAndNullable actual = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ClassWithEnumAndNullable);
                Assert.Equal(expected.Day, actual.Day);
                Assert.Equal(expected.NullableDay, actual.NullableDay);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public override void ClassWithNullableProperties_Roundtrip()
        {
            RunTest(new ClassWithNullableProperties
            {
                Uri = new Uri("http://contoso.com"),
                Array = new int[] { 42 },
                ByteArray = new byte[] { 1, 2, 3 },
                Poco = new ClassWithNullableProperties.MyPoco(),

                NullableUri = new Uri("http://contoso.com"),
                NullableArray = new int[] { 42 },
                NullablePoco = new ClassWithNullableProperties.MyPoco()
            });

            RunTest(new ClassWithNullableProperties());

            void RunTest(ClassWithNullableProperties expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithNullableProperties);

                if (expected.ByteArray is null)
                {
                    Assert.Contains("\"ByteArray\":null", yaml);
                }

                ClassWithNullableProperties actual = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ClassWithNullableProperties);

                Assert.Equal(expected.Uri, actual.Uri);
                Assert.Equal(expected.Array, actual.Array);
                Assert.Equal(expected.ByteArray, actual.ByteArray);
                Assert.Equal(expected.Poco, actual.Poco);

                Assert.Equal(expected.NullableUri, actual.NullableUri);
                Assert.Equal(expected.NullableArray, actual.NullableArray);
                Assert.Equal(expected.NullablePoco, actual.NullablePoco);

                Assert.Equal(expected.NullableUriParameter, actual.NullableUriParameter);
                Assert.Equal(expected.NullableArrayParameter, actual.NullableArrayParameter);
                Assert.Equal(expected.NullablePocoParameter, actual.NullablePocoParameter);
            }
        }

#if NET
        [Fact]
        public override void ClassWithDateOnlyAndTimeOnlyValues_Roundtrip()
        {
            RunTest(new ClassWithDateOnlyAndTimeOnlyValues
            {
                DateOnly = DateOnly.Parse("2022-05-10"),
                NullableDateOnly = DateOnly.Parse("2022-05-10"),

                TimeOnly = TimeOnly.Parse("21:51:51"),
                NullableTimeOnly = TimeOnly.Parse("21:51:51"),
            });

            RunTest(new ClassWithDateOnlyAndTimeOnlyValues());

            void RunTest(ClassWithDateOnlyAndTimeOnlyValues expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithDateOnlyAndTimeOnlyValues);
                ClassWithDateOnlyAndTimeOnlyValues actual = YamlSerializer.Deserialize(yaml, ((ITestContext)MetadataWithPerTypeAttributeContext.Default).ClassWithDateOnlyAndTimeOnlyValues);

                Assert.Equal(expected.DateOnly, actual.DateOnly);
                Assert.Equal(expected.NullableDateOnly, actual.NullableDateOnly);

                Assert.Equal(expected.TimeOnly, actual.TimeOnly);
                Assert.Equal(expected.NullableTimeOnly, actual.NullableTimeOnly);
            }
        }
#endif

        [Fact]
        [Trait("category", "jsonoutput")]
        public override void ParameterizedConstructor()
        {
            string yaml = YamlSerializer.Serialize(new HighLowTempsImmutable(1, 2), DefaultContext.HighLowTempsImmutable);
            Assert.Contains("""
                "High":1
                """, yaml);
            Assert.Contains("""
                "Low":2
                """, yaml);

            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.HighLowTempsImmutable), typeof(HighLowTempsImmutable));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public override void PositionalRecord()
        {
            string yaml = YamlSerializer.Serialize(new HighLowTempsRecord(1, 2), DefaultContext.HighLowTempsRecord);
            Assert.Contains("""
                "High":1
                """, yaml);
            Assert.Contains("""
                "Low":2
                """, yaml);

            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Deserialize(yaml, DefaultContext.HighLowTempsRecord), typeof(HighLowTempsRecord));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void OnSerializeCallbacks()
        {
            MyTypeWithCallbacks obj = new();
            Assert.Null(obj.MyProperty);

            string yaml = YamlSerializer.Serialize(obj, DefaultContext.MyTypeWithCallbacks);
            Assert.Equal("{\"MyProperty\":\"Before\"}", yaml);
            Assert.Equal("After", obj.MyProperty);
        }

        [Fact]
        public override void NullableStruct()
        {
            PersonStruct? person = new()
            {
                FirstName = "Jane",
                LastName = "Doe"
            };

            string yaml = YamlSerializer.Serialize(person, DefaultContext.NullablePersonStruct);
            YamlTestHelper.AssertYamlEqual("""{"FirstName":"Jane","LastName":"Doe"}""", yaml);

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize(yaml, DefaultContext.NullablePersonStruct));
        }

        [Fact]
        public void ClassWithConflictingIgnoredProperties_FastPathSerialization()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/99988

            ClassWithConflictingIgnoredProperties value = new()
            {
                SystemTextYamlUserList = ["root"],
                SystemTextYamlUserGroupsList = ["wheel"],
                SystemTextYamlIPAddresses = ["127.0.0.1"],
                SystemTextYamlQueryParams = ["key=value"],
            };

            string yaml = YamlSerializer.Serialize(value, SerializationContext.Default.ClassWithConflictingIgnoredProperties);

            YamlTestHelper.AssertYamlEqual("""
                {
                    "userlist" : ["root"],
                    "usergroupslist" : ["wheel"],
                    "SystemTextYamlIPAddresses" : ["127.0.0.1"],
                    "queryparams" : ["key=value"]
                }
                """, yaml);
        }
    }

    public sealed class SerializationWithPerTypeAttributeContextTests : SerializationContextTests
    {
        public SerializationWithPerTypeAttributeContextTests() : base(SerializationWithPerTypeAttributeContext.Default, (options) => new SerializationContext(options)) { }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.Location.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.RepeatedLocation.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.ActiveOrUpcomingEvent.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.IndexViewModel.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.HighLowTemps.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.MyType.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.MyType2.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.MyIntermediateType.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.MyNestedClass.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ByteArray.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.SourceGenSampleEnum.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.String.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.YamlElement.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ClassWithCustomConverter.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.StructWithCustomConverter.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ClassWithCustomConverterFactory.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.StructWithCustomConverterFactory.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ClassWithCustomConverterProperty.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.StructWithCustomConverterProperty.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.ClassWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.StructWithCustomConverterFactoryProperty.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => SerializationWithPerTypeAttributeContext.Default.ClassWithBadCustomConverter.SerializeHandler);
            Assert.Throws<InvalidOperationException>(() => SerializationWithPerTypeAttributeContext.Default.StructWithBadCustomConverter.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.PersonStruct.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.NotNull(SerializationWithPerTypeAttributeContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(SerializationWithPerTypeAttributeContext.Default.PolymorphicClass.SerializeHandler);
        }
    }
}
