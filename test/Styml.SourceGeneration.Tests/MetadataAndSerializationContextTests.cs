// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    [YamlSerializable(typeof(Location))]
    [YamlSerializable(typeof(RepeatedTypes.Location), TypeInfoPropertyName = "RepeatedLocation")]
    [YamlSerializable(typeof(NumberTypes))]
    [YamlSerializable(typeof(ActiveOrUpcomingEvent))]
    [YamlSerializable(typeof(CampaignSummaryViewModel))]
    [YamlSerializable(typeof(IndexViewModel))]
    [YamlSerializable(typeof(WeatherForecastWithPOCOs))]
    [YamlSerializable(typeof(EmptyPoco))]
    // Ensure no errors when type of member in previously specified object graph is passed as input type to generator.
    [YamlSerializable(typeof(HighLowTemps))]
    [YamlSerializable(typeof(MyType))]
    [YamlSerializable(typeof(MyType2))]
    [YamlSerializable(typeof(MyTypeWithCallbacks))]
    [YamlSerializable(typeof(MyTypeWithPropertyOrdering))]
    [YamlSerializable(typeof(MyIntermediateType))]
    [YamlSerializable(typeof(HighLowTempsImmutable))]
    [YamlSerializable(typeof(HighLowTempsRecord))]
    [YamlSerializable(typeof(byte[]))]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass))]
    [YamlSerializable(typeof(RealWorldContextTests.MyNestedClass.MyNestedNestedClass))]
    [YamlSerializable(typeof(object[]))]
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
    internal partial class MetadataAndSerializationContext : YamlSerializerContext, ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode => YamlSourceGenerationMode.Default;
        public bool IsIncludeFieldsEnabled => GetType().GetCustomAttribute<YamlSourceGenerationOptionsAttribute>()?.IncludeFields ?? false;
    }

    public sealed class MetadataAndSerializationContextTests : RealWorldContextTests
    {
        public MetadataAndSerializationContextTests() : base(MetadataAndSerializationContext.Default, (options) => new MetadataAndSerializationContext(options)) { }

        [Fact]
        public override void EnsureFastPathGeneratedAsExpected()
        {
            Assert.NotNull(MetadataAndSerializationContext.Default.Location.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.NumberTypes.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.RepeatedLocation.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.ActiveOrUpcomingEvent.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.CampaignSummaryViewModel.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.IndexViewModel.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.WeatherForecastWithPOCOs.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.EmptyPoco.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.HighLowTemps.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyType.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyType2.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyTypeWithCallbacks.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyTypeWithPropertyOrdering.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyIntermediateType.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.HighLowTempsImmutable.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.HighLowTempsRecord.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyNestedClass.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.MyNestedNestedClass.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.ObjectArray.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.SourceGenSampleEnum.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.String.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.ValueTupleStringInt32Boolean.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.YamlDocument.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.YamlElement.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithEnumAndNullable.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithNullableProperties.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithCustomConverter);
            Assert.NotNull(MetadataAndSerializationContext.Default.StructWithCustomConverter);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithCustomConverterFactory);
            Assert.NotNull(MetadataAndSerializationContext.Default.StructWithCustomConverterFactory);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithCustomConverterProperty);
            Assert.NotNull(MetadataAndSerializationContext.Default.StructWithCustomConverterProperty);
            Assert.NotNull(MetadataAndSerializationContext.Default.ClassWithCustomConverterFactoryProperty);
            Assert.NotNull(MetadataAndSerializationContext.Default.StructWithCustomConverterFactoryProperty);
            Assert.Throws<InvalidOperationException>(() => MetadataAndSerializationContext.Default.ClassWithBadCustomConverter);
            Assert.Throws<InvalidOperationException>(() => MetadataAndSerializationContext.Default.StructWithBadCustomConverter);
            Assert.Null(MetadataAndSerializationContext.Default.NullablePersonStruct.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.PersonStruct.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.TypeWithValidationAttributes.SerializeHandler);
            Assert.NotNull(MetadataAndSerializationContext.Default.TypeWithDerivedAttribute.SerializeHandler);
            Assert.Null(MetadataAndSerializationContext.Default.PolymorphicClass.SerializeHandler);
        }
    }
}
