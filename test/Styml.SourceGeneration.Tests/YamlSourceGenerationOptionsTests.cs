// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Yaml.Serialization;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public static partial class YamlSourceGenerationOptionsTests
    {
        [Fact]
        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void ContextWithGeneralSerializerDefaults_GeneratesExpectedOptions()
        {
            YamlSerializerOptions expected = new(YamlSerializerDefaults.General) { TypeInfoResolver = ContextWithGeneralSerializerDefaults.Default };
            YamlSerializerOptions options = ContextWithGeneralSerializerDefaults.Default.Options;

            YamlTestHelper.AssertOptionsEqual(expected, options);
        }

        [YamlSourceGenerationOptions(YamlSerializerDefaults.General)]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithGeneralSerializerDefaults : YamlSerializerContext
        { }

        [Fact]
        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void ContextWithWebSerializerDefaults_GeneratesExpectedOptions()
        {
            YamlSerializerOptions expected = new(YamlSerializerDefaults.Web) { TypeInfoResolver = ContextWithWebSerializerDefaults.Default };
            YamlSerializerOptions options = ContextWithWebSerializerDefaults.Default.Options;

            YamlTestHelper.AssertOptionsEqual(expected, options);
        }

        [YamlSourceGenerationOptions(YamlSerializerDefaults.Web)]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithWebSerializerDefaults : YamlSerializerContext
        { }

        [Fact]
        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void ContextWithStrictSerializerDefaults_GeneratesExpectedOptions()
        {
            YamlSerializerOptions expected = new(YamlSerializerDefaults.Strict) { TypeInfoResolver = ContextWithStrictSerializerDefaults.Default };
            YamlSerializerOptions options = ContextWithStrictSerializerDefaults.Default.Options;

            YamlTestHelper.AssertOptionsEqual(expected, options);
        }

        [YamlSourceGenerationOptions(YamlSerializerDefaults.Strict)]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithStrictSerializerDefaults : YamlSerializerContext
        { }

        [Fact]
        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void ContextWithWebDefaultsAndOverriddenPropertyNamingPolicy_GeneratesExpectedOptions()
        {
            YamlSerializerOptions expected = new(YamlSerializerDefaults.Web)
            {
                PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower,
                TypeInfoResolver = ContextWithWebDefaultsAndOverriddenPropertyNamingPolicy.Default,
            };

            YamlSerializerOptions options = ContextWithWebDefaultsAndOverriddenPropertyNamingPolicy.Default.Options;

            YamlTestHelper.AssertOptionsEqual(expected, options);
        }

        [YamlSourceGenerationOptions(YamlSerializerDefaults.Web, PropertyNamingPolicy = YamlKnownNamingPolicy.KebabCaseLower)]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithWebDefaultsAndOverriddenPropertyNamingPolicy : YamlSerializerContext
        { }

        [Fact]
        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void ContextWithAllOptionsSet_GeneratesExpectedOptions()
        {
            YamlSerializerOptions expected = new(YamlSerializerDefaults.Web)
            {
                AllowOutOfOrderMetadataProperties = true,
                AllowTrailingCommas = true,
                Converters = { new YamlStringEnumConverter<BindingFlags>(), new YamlStringEnumConverter<YamlIgnoreCondition>() },
                DefaultBufferSize = 128,
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault,
                DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseUpper,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                MaxDepth = 1024,
                NewLine = "\n",
                NumberHandling = YamlNumberHandling.WriteAsString,
                PreferredObjectCreationHandling = YamlObjectCreationHandling.Replace,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = YamlNamingPolicy.KebabCaseUpper,
                ReadCommentHandling = YamlCommentHandling.Skip,
                ReferenceHandler = ReferenceHandler.Preserve,
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true,
                UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode,
                UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow,
                WriteIndented = true,
                IndentCharacter = '\t',
                IndentSize = 1,
                AllowDuplicateProperties = false,

                TypeInfoResolver = ContextWithAllOptionsSet.Default,
            };

            YamlSerializerOptions options = ContextWithAllOptionsSet.Default.Options;

            YamlTestHelper.AssertOptionsEqual(expected, options);
        }

        [YamlSourceGenerationOptions(YamlSerializerDefaults.Web,
            AllowOutOfOrderMetadataProperties = true,
            AllowTrailingCommas = true,
            Converters = [typeof(YamlStringEnumConverter<BindingFlags>), typeof(YamlStringEnumConverter<YamlIgnoreCondition>)],
            DefaultBufferSize = 128,
            DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault,
            DictionaryKeyPolicy = YamlKnownNamingPolicy.SnakeCaseUpper,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            IncludeFields = true,
            MaxDepth = 1024,
            NewLine = "\n",
            NumberHandling = YamlNumberHandling.WriteAsString,
            PreferredObjectCreationHandling = YamlObjectCreationHandling.Replace,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = YamlKnownNamingPolicy.KebabCaseUpper,
            ReadCommentHandling = YamlCommentHandling.Skip,
            ReferenceHandler = YamlKnownReferenceHandler.Preserve,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode,
            UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow,
            WriteIndented = true,
            IndentCharacter = '\t',
            IndentSize = 1,
            AllowDuplicateProperties = false)]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithAllOptionsSet : YamlSerializerContext
        { }

        [Fact]
        public static void ContextWithInvalidSerializerDefaults_ThrowsArgumentOutOfRangeException()
        {
            TypeInitializationException ex = Assert.Throws<TypeInitializationException>(() => ContextWithInvalidSerializerDefaults.Default);
            ArgumentOutOfRangeException inner = Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
            Assert.Contains("defaults", inner.Message);
        }

        [YamlSourceGenerationOptions((YamlSerializerDefaults)(-1))]
        [YamlSerializable(typeof(PersonStruct))]
        public partial class ContextWithInvalidSerializerDefaults : YamlSerializerContext
        { }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void UseStringEnumConverter_EnablesDefaultStringEnumSerialization()
        {
            var value = new ClassWithEnumProperty { StringValue = MyEnum.A, NumberValue = MyEnum.A };
            string expectedYaml = """{"StringValue":"A","NumberValue":0}""";

            string yaml = YamlSerializer.Serialize(value, ContextWithStringEnumConverterEnabled.Default.ClassWithEnumProperty);
            Assert.Equal(expectedYaml, yaml);

            value = YamlSerializer.Deserialize(yaml, ContextWithStringEnumConverterEnabled.Default.ClassWithEnumProperty);
            Assert.Equal(MyEnum.A, value.StringValue);
            Assert.Equal(MyEnum.A, value.NumberValue);
        }

        public class ClassWithEnumProperty
        {
            public MyEnum StringValue { get; set; }

            [YamlConverter(typeof(YamlNumberEnumConverter<MyEnum>))]
            public MyEnum NumberValue { get; set; }
        }

        public enum MyEnum { A = 0, B = 1, C = 2 }

        [YamlSourceGenerationOptions(UseStringEnumConverter = true)]
        [YamlSerializable(typeof(ClassWithEnumProperty))]
        public partial class ContextWithStringEnumConverterEnabled : YamlSerializerContext
        { }

        [Fact]
        public static void AttributeWithGeneralSerializerDefaults_SetsPropertiesCorrectly()
        {
            var attr = new YamlSourceGenerationOptionsAttribute(YamlSerializerDefaults.General);
            
            // General uses default property values
            Assert.False(attr.PropertyNameCaseInsensitive);
            Assert.Equal(YamlKnownNamingPolicy.Unspecified, attr.PropertyNamingPolicy);
            Assert.Equal(YamlNumberHandling.Strict, attr.NumberHandling);
            Assert.Equal(YamlUnmappedMemberHandling.Skip, attr.UnmappedMemberHandling);
            Assert.False(attr.AllowDuplicateProperties);
            Assert.False(attr.RespectNullableAnnotations);
            Assert.False(attr.RespectRequiredConstructorParameters);
        }

        [Fact]
        public static void AttributeWithWebSerializerDefaults_SetsPropertiesCorrectly()
        {
            var attr = new YamlSourceGenerationOptionsAttribute(YamlSerializerDefaults.Web);
            
            // Web sets PropertyNameCaseInsensitive=true, PropertyNamingPolicy=CamelCase, and NumberHandling=AllowReadingFromString
            Assert.True(attr.PropertyNameCaseInsensitive);
            Assert.Equal(YamlKnownNamingPolicy.CamelCase, attr.PropertyNamingPolicy);
            Assert.Equal(YamlNumberHandling.AllowReadingFromString, attr.NumberHandling);
            
            // Other properties should be default
            Assert.Equal(YamlUnmappedMemberHandling.Skip, attr.UnmappedMemberHandling);
            Assert.False(attr.AllowDuplicateProperties);
            Assert.False(attr.RespectNullableAnnotations);
            Assert.False(attr.RespectRequiredConstructorParameters);
        }

        [Fact]
        public static void AttributeWithStrictSerializerDefaults_SetsPropertiesCorrectly()
        {
            var attr = new YamlSourceGenerationOptionsAttribute(YamlSerializerDefaults.Strict);
            
            // Strict sets UnmappedMemberHandling=Disallow, AllowDuplicateProperties=false, RespectNullableAnnotations=true, and RespectRequiredConstructorParameters=true
            Assert.Equal(YamlUnmappedMemberHandling.Disallow, attr.UnmappedMemberHandling);
            Assert.False(attr.AllowDuplicateProperties);
            Assert.True(attr.RespectNullableAnnotations);
            Assert.True(attr.RespectRequiredConstructorParameters);
            
            // Other properties should be default
            Assert.False(attr.PropertyNameCaseInsensitive);
            Assert.Equal(YamlKnownNamingPolicy.Unspecified, attr.PropertyNamingPolicy);
            Assert.Equal(YamlNumberHandling.Strict, attr.NumberHandling);
        }
    }
}
