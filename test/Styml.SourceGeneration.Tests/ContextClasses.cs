// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public interface ITestContext
    {
        public YamlSourceGenerationMode YamlSourceGenerationMode { get; }
        public bool IsIncludeFieldsEnabled { get; }

        public YamlTypeInfo<Location> Location { get; }
        public YamlTypeInfo<NumberTypes> NumberTypes { get; }
        public YamlTypeInfo<RepeatedTypes.Location> RepeatedLocation { get; }
        public YamlTypeInfo<ActiveOrUpcomingEvent> ActiveOrUpcomingEvent { get; }
        public YamlTypeInfo<CampaignSummaryViewModel> CampaignSummaryViewModel { get; }
        public YamlTypeInfo<IndexViewModel> IndexViewModel { get; }
        public YamlTypeInfo<WeatherForecastWithPOCOs> WeatherForecastWithPOCOs { get; }
        public YamlTypeInfo<EmptyPoco> EmptyPoco { get; }
        public YamlTypeInfo<HighLowTemps> HighLowTemps { get; }
        public YamlTypeInfo<MyType> MyType { get; }
        public YamlTypeInfo<MyType2> MyType2 { get; }
        public YamlTypeInfo<MyTypeWithCallbacks> MyTypeWithCallbacks { get; }
        public YamlTypeInfo<MyTypeWithPropertyOrdering> MyTypeWithPropertyOrdering { get; }
        public YamlTypeInfo<MyIntermediateType> MyIntermediateType { get; }
        public YamlTypeInfo<HighLowTempsImmutable> HighLowTempsImmutable { get; }
        public YamlTypeInfo<HighLowTempsRecord> HighLowTempsRecord { get; }
        public YamlTypeInfo<RealWorldContextTests.MyNestedClass> MyNestedClass { get; }
        public YamlTypeInfo<RealWorldContextTests.MyNestedClass.MyNestedNestedClass> MyNestedNestedClass { get; }
        public YamlTypeInfo<object[]> ObjectArray { get; }
        public YamlTypeInfo<byte[]> ByteArray { get; }
        public YamlTypeInfo<string> String { get; }
        public YamlTypeInfo<(string Label1, int Label2, bool)> ValueTupleStringInt32Boolean { get; }
        public YamlTypeInfo<YamlDocument> YamlDocument { get; }
        public YamlTypeInfo<YamlElement> YamlElement { get; }
        public YamlTypeInfo<RealWorldContextTests.ClassWithEnumAndNullable> ClassWithEnumAndNullable { get; }
        public YamlTypeInfo<RealWorldContextTests.ClassWithNullableProperties> ClassWithNullableProperties { get; }
#if NET
        public YamlTypeInfo<RealWorldContextTests.ClassWithDateOnlyAndTimeOnlyValues> ClassWithDateOnlyAndTimeOnlyValues { get; }
#endif
        public YamlTypeInfo<ClassWithCustomConverter> ClassWithCustomConverter { get; }
        public YamlTypeInfo<StructWithCustomConverter> StructWithCustomConverter { get; }
        public YamlTypeInfo<ClassWithCustomConverterFactory> ClassWithCustomConverterFactory { get; }
        public YamlTypeInfo<StructWithCustomConverterFactory> StructWithCustomConverterFactory { get; }
        public YamlTypeInfo<ClassWithCustomConverterProperty> ClassWithCustomConverterProperty { get; }
        public YamlTypeInfo<StructWithCustomConverterProperty> StructWithCustomConverterProperty { get; }
        public YamlTypeInfo<ClassWithCustomConverterFactoryProperty> ClassWithCustomConverterFactoryProperty { get; }
        public YamlTypeInfo<StructWithCustomConverterFactoryProperty> StructWithCustomConverterFactoryProperty { get; }
        public YamlTypeInfo<ClassWithCustomConverterNullableProperty> ClassWithCustomConverterNullableProperty { get; }
        public YamlTypeInfo<ClassWithCustomConverterFactoryNullableProperty> ClassWithCustomConverterFactoryNullableProperty { get; }
        public YamlTypeInfo<ClassWithBadCustomConverter> ClassWithBadCustomConverter { get; }
        public YamlTypeInfo<StructWithBadCustomConverter> StructWithBadCustomConverter { get; }
        public YamlTypeInfo<PersonStruct?> NullablePersonStruct { get; }
        public YamlTypeInfo<TypeWithValidationAttributes> TypeWithValidationAttributes { get; }
        public YamlTypeInfo<TypeWithDerivedAttribute> TypeWithDerivedAttribute { get; }
        public YamlTypeInfo<PolymorphicClass> PolymorphicClass { get; }
        public YamlTypeInfo<PocoWithNumberHandlingAttr> PocoWithNumberHandlingAttr { get; }
        public YamlTypeInfo<PocoWithMixedVisibilityMembers> PocoWithMixedVisibilityMembers { get; }
    }

    internal partial class YamlContext : YamlSerializerContext
    {
        private static YamlSerializerOptions s_defaultOptions { get; } = new YamlSerializerOptions()
        {
            DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = YamlNamingPolicy.CamelCase
        };

        private static YamlContext s_defaultContext;
        public static YamlContext Default => s_defaultContext ??= new YamlContext(new YamlSerializerOptions(s_defaultOptions));

        public YamlContext() : base(null)
        {
        }

        public YamlContext(YamlSerializerOptions options) : base(options)
        {
        }

        protected override YamlSerializerOptions? GeneratedSerializerOptions => s_defaultOptions;

        public override YamlTypeInfo GetTypeInfo(global::System.Type type)
        {
            if (type == typeof(YamlMessage))
            {
                return YamlMessage;
            }

            return null!;
        }

        private YamlTypeInfo<YamlMessage> _YamlMessage;
        public YamlTypeInfo<YamlMessage> YamlMessage
        {
            get
            {
                if (_YamlMessage == null)
                {
                    YamlObjectInfoValues<YamlMessage> objectInfo = new()
                    {
                        ObjectCreator = static () => new YamlMessage(),
                        SerializeHandler = YamlMessageSerialize
                    };

                    _YamlMessage = YamlMetadataServices.CreateObjectInfo<YamlMessage>(Options, objectInfo);
                }

                return _YamlMessage;
            }
        }

        private static void YamlMessageSerialize(Utf8YamlWriter writer, YamlMessage value) => throw new NotImplementedException();
    }

    [YamlSerializable(typeof(Dictionary<string, string>))]
    [YamlSerializable(typeof(Dictionary<int, string>))]
    [YamlSerializable(typeof(Dictionary<string, YamlMessage>))]
    internal partial class DictionaryTypeContext : YamlSerializerContext { }

    [YamlSerializable(typeof(YamlMessage))]
    [YamlSerializable(typeof(PublicClassWithDifferentAccessibilitiesProperties))]
    [YamlSerializable(typeof(YamlConverter))]
    [YamlSerializable(typeof(YamlSerializerOptions))]
    public partial class PublicContext : YamlSerializerContext { }

    [YamlSerializable(typeof(YamlMessage))]
    public partial class GenericContext<T> : YamlSerializerContext { }

    public partial class ContextGenericContainer<T>
    {
        [YamlSerializable(typeof(YamlMessage))]
        public partial class NestedInGenericContainerContext : YamlSerializerContext { }

        [YamlSerializable(typeof(YamlMessage))]
        public partial class NestedGenericInGenericContainerContext<T1> : YamlSerializerContext { }

        public partial class NestedGenericContainer<T1>
        {
            [YamlSerializable(typeof(YamlMessage))]
            public partial class NestedInNestedGenericContainerContext : YamlSerializerContext { }

            [YamlSerializable(typeof(YamlMessage))]
            public partial class NestedGenericInNestedGenericContainerContext<T2> : YamlSerializerContext { }
        }
    }

    [YamlSerializable(typeof(MyContainingClass.MyNestedClass.MyNestedNestedClass))]
    [YamlSerializable(typeof(MyContainingClass.MyNestedClass.MyNestedNestedGenericClass<int>))]
    [YamlSerializable(typeof(MyContainingClass.MyNestedGenericClass<int>.MyNestedGenericNestedClass))]
    [YamlSerializable(typeof(MyContainingClass.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>))]
    [YamlSerializable(typeof(MyContainingGenericClass<int>.MyNestedClass.MyNestedNestedClass))]
    [YamlSerializable(typeof(MyContainingGenericClass<int>.MyNestedClass.MyNestedNestedGenericClass<int>))]
    [YamlSerializable(typeof(MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedClass))]
    [YamlSerializable(typeof(MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>))]
    [YamlSerializable(typeof(MyContainingGenericClass<MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>))]
    internal partial class NestedGenericTypesContext : YamlSerializerContext { }
}
