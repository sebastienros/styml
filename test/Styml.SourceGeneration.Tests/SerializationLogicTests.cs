// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public static class SerializationLogicTests
    {
        private static YamlSerializerOptions s_compatibleOptions = new YamlSerializerOptions
        {
            DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = YamlNamingPolicy.CamelCase
        };

        [Theory]
        [MemberData(nameof(GetOptionsUsingDeserializationOnlyFeatures))]
        [MemberData(nameof(GetCompatibleOptions))]
        public static void SerializationFuncInvokedWhenSupported(YamlSerializerOptions options)
        {
            YamlMessage message = new();

            // Per context implementation, NotImplementedException thrown because the options are compatible, hence the serialization func is invoked.
            YamlContext context = new(options);
            Assert.Throws<NotImplementedException>(() => YamlSerializer.Serialize(message, context.YamlMessage));
            Assert.Throws<NotImplementedException>(() => YamlSerializer.Serialize(message, typeof(YamlMessage), context));
        }

        [Theory]
        [MemberData(nameof(GetOptionsUsingUnsupportedFeatures))]
        [MemberData(nameof(GetIncompatibleOptions))]
        public static void SerializationFuncNotInvokedWhenNotSupported(YamlSerializerOptions options)
        {
            YamlMessage message = new();

            // Per context implementation, NotImplementedException thrown because the options are compatible, hence the serialization func is invoked.
            Assert.Throws<NotImplementedException>(() => YamlSerializer.Serialize(message, YamlContext.Default.YamlMessage));
            Assert.Throws<NotImplementedException>(() => YamlSerializer.Serialize(message, typeof(YamlMessage), YamlContext.Default));

            // NotSupportedException thrown because
            // - the options are not compatible, hence the serialization func is not invoked.
            // - the serializer correctly tries to serialize based on property metadata, but we have not provided it in our implementation.
            YamlContext context = new(options);
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Serialize(message, context.YamlMessage), typeof(YamlMessage));
            YamlTestHelper.AssertThrows_PropMetadataInit(() => YamlSerializer.Serialize(message, typeof(YamlMessage), context), typeof(YamlMessage));
        }

        [Fact]
        public static void DictionaryFastPathPrimitiveValueSupported()
        {
            Assert.NotNull(DictionaryTypeContext.Default.DictionaryStringString.SerializeHandler);
            Assert.NotNull(DictionaryTypeContext.Default.DictionaryStringYamlMessage.SerializeHandler);
            Assert.NotNull(DictionaryTypeContext.Default.YamlMessage.SerializeHandler);
            Assert.Null(DictionaryTypeContext.Default.String.SerializeHandler);
            Assert.Null(DictionaryTypeContext.Default.Int32.SerializeHandler);
        }

        // Options with features that apply only to deserialization.
        public static IEnumerable<object[]> GetOptionsUsingDeserializationOnlyFeatures()
        {
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { AllowTrailingCommas = true } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { DefaultBufferSize = 8192 } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { PropertyNameCaseInsensitive = true } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { ReadCommentHandling = YamlCommentHandling.Skip } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { NumberHandling = YamlNumberHandling.AllowReadingFromString } };
        }

        /// <summary>
        /// Options compatible with <see cref="YamlContext.s_defaultOptions"/>.
        /// </summary>
        public static IEnumerable<object[]> GetCompatibleOptions()
        {
            yield return new object[] { s_compatibleOptions };
            yield return new object[] { new YamlSerializerOptions(YamlSerializerDefaults.Web) { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { NumberHandling = YamlNumberHandling.Strict } };
        }

        // Options with features that aren't supported in the generated serialization funcs.
        public static IEnumerable<object[]> GetOptionsUsingUnsupportedFeatures()
        {
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { Converters = { new YamlStringEnumConverter<YamlIgnoreCondition>() } } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { NumberHandling = YamlNumberHandling.WriteAsString } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowNamedFloatingPointLiterals } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { ReferenceHandler = ReferenceHandler.Preserve } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { ReferenceHandler = ReferenceHandler.IgnoreCycles } };
        }

        /// <summary>
        /// Options incompatible with <see cref="YamlContext.s_defaultOptions"/>.
        /// </summary>
        public static IEnumerable<object[]> GetIncompatibleOptions()
        {
            yield return new object[] { new YamlSerializerOptions() };
            yield return new object[] { new YamlSerializerOptions { Converters = { new YamlStringEnumConverter<YamlIgnoreCondition>() } } };
            yield return new object[] { new YamlSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping } };
            yield return new object[] { new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString } };
            yield return new object[] { new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve } };
            yield return new object[] { new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles } };
            yield return new object[] { new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.Never } };
            yield return new object[] { new YamlSerializerOptions { IgnoreReadOnlyFields = true } };
            yield return new object[] { new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault } };
            yield return new object[] { new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { DefaultIgnoreCondition = YamlIgnoreCondition.Never } };
            yield return new object[] { new YamlSerializerOptions(s_compatibleOptions) { IgnoreReadOnlyFields = true } };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriterIsFlushedAtRootCall()
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);

            YamlSerializer.Serialize(writer, new HighLowTemps(), SerializationContext.Default.HighLowTemps);
            Assert.Equal(18, writer.BytesCommitted);
            Assert.Equal(0, writer.BytesPending);
        }

        [Fact]
        public static void FastPathInvokedForNullableUnderlyingType()
        {
            PersonStruct? person = new()
            {
                FirstName = "Jane",
                LastName = "Doe"
            };

            NullablePersonContext context = new();
            Assert.False(context.FastPathCalled);
            string yaml = YamlSerializer.Serialize(person, context.NullablePersonStruct);
            Assert.True(context.FastPathCalled);
            YamlTestHelper.AssertYamlEqual("""{"FirstName":"Jane","LastName":"Doe"}""", yaml);
        }

        internal partial class NullablePersonContext : YamlSerializerContext
        {
            private static YamlSerializerOptions s_options = new YamlSerializerOptions();

            public bool FastPathCalled { get; private set; }

            public NullablePersonContext() : base(s_options)
            {
            }

            protected override YamlSerializerOptions? GeneratedSerializerOptions => s_options;

            public YamlTypeInfo<PersonStruct?> NullablePersonStruct =>
                YamlMetadataServices.CreateValueInfo<PersonStruct?>(s_options, YamlMetadataServices.GetNullableConverter(underlyingTypeInfo: PersonStruct));

            public YamlTypeInfo<PersonStruct> PersonStruct
            {
                get
                {
                    var objectInfo = new YamlObjectInfoValues<PersonStruct>()
                    {
                        ObjectCreator = static () => new PersonStruct(),
                        SerializeHandler = PersonStructSerializeHandler
                    };

                    return YamlMetadataServices.CreateObjectInfo(s_options, objectInfo);
                }
            }

            private void PersonStructSerializeHandler(Utf8YamlWriter writer, PersonStruct value)
            {
                FastPathCalled = true;
                writer.WriteStartObject();
                writer.WriteString("FirstName", value.FirstName);
                writer.WriteString("LastName", value.LastName);
                writer.WriteEndObject();
            }

            public override YamlTypeInfo? GetTypeInfo(Type type)
            {
                if (type == typeof(PersonStruct))
                {
                    return PersonStruct;
                }

                return null;
            }
        }
    }
}
