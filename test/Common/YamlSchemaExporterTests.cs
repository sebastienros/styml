// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Xml.Linq;
using Json.Schema;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;
using Xunit.Sdk;

namespace System.Text.Yaml.Schema.Tests
{
    public abstract partial class YamlSchemaExporterTests : SerializerTests
    {
        private readonly YamlSerializerOptions _indentedOptions;

        protected YamlSchemaExporterTests(YamlSerializerWrapper serializer) : base(serializer)
        {
            _indentedOptions = new(serializer.DefaultOptions) { WriteIndented = true };
        }

        [Theory]
        [MemberData(nameof(GetTestDataWithoutJsonComments))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/103694", TestRuntimes.Mono)]
        public void TestTypes_GeneratesExpectedYamlSchema(ITestData testData)
        {
            YamlSerializerOptions options = testData.SerializerOptions is { } opts
                ? new(opts) { TypeInfoResolver = Serializer.DefaultOptions.TypeInfoResolver }
                : Serializer.DefaultOptions;

            YamlNode schema = options.GetYamlSchemaAsNode(testData.Type, testData.Options);
            AssertValidYamlSchema(testData.Type, testData.ExpectedYamlSchema, schema);
        }

        public static IEnumerable<object[]> GetTestDataWithoutJsonComments() => GetTestData().Where(row => !ExpectedSchemaHasJsonComments(row));

        public static IEnumerable<object[]> GetTestDataWithJsonComments() => GetTestData().Where(ExpectedSchemaHasJsonComments);

        private static bool ExpectedSchemaHasJsonComments(object[] row) =>
            row[0] is ITestData { ExpectedYamlSchema: string schema } && schema.Contains("/*");

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        [MemberData(nameof(GetTestDataUsingAllValues))]
        [RequiresUnreferencedCode("Uses reflection-based YamlSerializer.SerializeToNode(object, Type, options).")]
        [RequiresDynamicCode("Uses reflection-based YamlSerializer.SerializeToNode(object, Type, options).")]
        [Trait("category", "jsonoutput")]
        public void TestTypes_SerializedValueMatchesGeneratedSchema(ITestData testData)
        {
            YamlSerializerOptions options = testData.SerializerOptions is { } opts
                ? new(opts) { TypeInfoResolver = Serializer.DefaultOptions.TypeInfoResolver }
                : Serializer.DefaultOptions;

            YamlNode schema = options.GetYamlSchemaAsNode(testData.Type, testData.Options);
            YamlNode? instance = YamlSerializer.SerializeToNode(testData.Value, testData.Type, options);
            AssertDocumentMatchesSchema(schema, instance);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnionNumberHandling_StrictTypeAttributeOverridesWebDefaults(bool asCollectionElement)
        {
            YamlSerializerOptions options = new(YamlSerializerDefaults.Web)
            {
                TypeInfoResolver = Serializer.DefaultOptions.TypeInfoResolver,
            };

            Type type = asCollectionElement ? typeof(List<StrictIntOrStringUnion>) : typeof(StrictIntOrStringUnion);
            YamlNode schema = Serializer.GetTypeInfo(type, options).GetYamlSchemaAsNode();
            YamlNode unionSchema = asCollectionElement ? schema["items"]! : schema;

            YamlTestHelper.AssertYamlEqual("""{"type":"integer"}""", unionSchema["anyOf"]![0]!.ToYamlString());
        }

        [Theory]
        [MemberData(nameof(YamlTestHelper.GetUnionCaseNumberHandlingPrecedenceTestData), MemberType = typeof(YamlTestHelper))]
        public void UnionNumberHandling_MetadataOverrides(
            YamlNumberHandling globalHandling, YamlNumberHandling? unionHandling, YamlNumberHandling? caseHandling, YamlNumberHandling expectedHandling)
        {
            foreach ((Type unionType, Type numberType) in new[] { (typeof(IntOrBoolUnion), typeof(int)), (typeof(NullableIntUnion), typeof(int?)) })
            {
                YamlSerializerOptions options = Serializer.CreateOptions(
                    configure: options => options.NumberHandling = globalHandling,
                    modifier: typeInfo =>
                    {
                        if (typeInfo.Type == numberType)
                        {
                            typeInfo.NumberHandling = caseHandling;
                        }
                    });

                YamlTypeInfo typeInfo = Serializer.GetTypeInfo(unionType, options, mutable: true);
                typeInfo.NumberHandling = unionHandling;
                YamlNode schema = typeInfo.GetYamlSchemaAsNode();
                YamlNode numberSchema = numberType == typeof(int) ? schema["anyOf"]![0]! : schema;
                YamlNode schemaType = numberSchema["type"]!;
                IEnumerable<string?> actualTypes = schemaType is YamlArray types
                    ? types.Select(type => (string?)type)
                    : [(string?)schemaType];

                List<string> expectedTypes = ["integer"];
                if ((expectedHandling & (YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)) != 0)
                {
                    expectedTypes.Add("string");
                }
                if (numberType == typeof(int?))
                {
                    expectedTypes.Add("null");
                }

                Assert.Equal(expectedTypes.OrderBy(type => type, StringComparer.Ordinal), actualTypes.OrderBy(type => type, StringComparer.Ordinal));
            }
        }

        [Theory]
        [InlineData(YamlNumberHandling.AllowReadingFromString, YamlNumberHandling.Strict)]
        [InlineData(YamlNumberHandling.Strict, YamlNumberHandling.AllowReadingFromString)]
        public void UnionNumberHandling_NullableCasePreservesElementOverride(YamlNumberHandling globalHandling, YamlNumberHandling elementHandling)
        {
            YamlSerializerOptions options = Serializer.CreateOptions(
                configure: options => options.NumberHandling = globalHandling,
                modifier: typeInfo =>
                {
                    if (typeInfo.Type == typeof(int))
                    {
                        typeInfo.NumberHandling = elementHandling;
                    }
                });

            bool allowsStrings = (elementHandling & YamlNumberHandling.AllowReadingFromString) != 0;
            YamlNode schema = Serializer.GetTypeInfo<NullableIntUnion>(options).GetYamlSchemaAsNode();
            YamlArray types = Assert.IsType<YamlArray>(schema["type"]);
            Assert.Equal(allowsStrings, types.Any(type => (string?)type == "string"));
        }

        [Theory]
        [InlineData(typeof(string), "string")]
        [InlineData(typeof(int[]), "array")]
        [InlineData(typeof(Dictionary<string, int>), "object")]
        [InlineData(typeof(SimplePoco), "object")]
        public void TreatNullObliviousAsNonNullable_False_MarksReferenceTypesAsNullable(Type referenceType, string expectedType)
        {
            Assert.True(!referenceType.IsValueType);
            var config = new YamlSchemaExporterOptions { TreatNullObliviousAsNonNullable = false };
            YamlNode schema = Serializer.DefaultOptions.GetYamlSchemaAsNode(referenceType, config);
            YamlArray arr = Assert.IsType<YamlArray>(schema["type"]);
            Assert.Equal([expectedType, "null"], arr.Select(e => (string)e!));
        }

        [Theory]
        [InlineData(typeof(string), "string")]
        [InlineData(typeof(int[]), "array")]
        [InlineData(typeof(Dictionary<string, int>), "object")]
        [InlineData(typeof(SimplePoco), "object")]
        public void TreatNullObliviousAsNonNullable_True_MarksReferenceTypesAsNonNullable(Type referenceType, string expectedType)
        {
            Assert.True(!referenceType.IsValueType);
            var config = new YamlSchemaExporterOptions { TreatNullObliviousAsNonNullable = true };
            YamlNode schema = Serializer.DefaultOptions.GetYamlSchemaAsNode(referenceType, config);
            Assert.Equal(expectedType, (string)schema["type"]!);
        }

        [Theory]
        [InlineData(typeof(Type))]
        [InlineData(typeof(MethodInfo))]
        [InlineData(typeof(UIntPtr))]
        [InlineData(typeof(MemberInfo))]
        [InlineData(typeof(SerializationInfo))]
        [InlineData(typeof(Func<int, int>))]
        [Trait("category", "jsonoutput")]
        public void UnsupportedType_ReturnsExpectedSchema(Type type)
        {
            YamlNode schema = Serializer.DefaultOptions.GetYamlSchemaAsNode(type);
            Assert.Equal(""""{"$comment":"Unsupported .NET type","not":true}"""", schema.ToYamlString());
        }

        [Fact]
        public void CanGenerateXElementSchema()
        {
            YamlNode schema = Serializer.DefaultOptions.GetYamlSchemaAsNode(typeof(XElement));
            Assert.True(schema.ToYamlString().Length < 100_000);
        }

        [Fact]
        public void TransformSchemaNode_PropertiesWithCustomConverters()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/109868
            List<(Type? ParentType, string? PropertyName, Type type)> visitedNodes = new();
            YamlSchemaExporterOptions exporterOptions = new()
            {
                TransformSchemaNode = (ctx, schema) =>
                {
                    visitedNodes.Add((ctx.PropertyInfo?.DeclaringType, ctx.PropertyInfo?.Name, ctx.TypeInfo.Type));
                    return schema;
                }
            };

            List<(Type? ParentType, string? PropertyName, Type type)> expectedNodes =
            [
                (typeof(ClassWithPropertiesUsingCustomConverters), "Prop1", typeof(ClassWithPropertiesUsingCustomConverters.ClassWithCustomConverter1)),
                (typeof(ClassWithPropertiesUsingCustomConverters), "Prop2", typeof(ClassWithPropertiesUsingCustomConverters.ClassWithCustomConverter2)),
                (null, null, typeof(ClassWithPropertiesUsingCustomConverters)),
            ];

            Serializer.DefaultOptions.GetYamlSchemaAsNode(typeof(ClassWithPropertiesUsingCustomConverters), exporterOptions);

            Assert.Equal(expectedNodes, visitedNodes);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void TypeWithDisallowUnmappedMembers_AdditionalPropertiesFailValidation()
        {
            YamlNode schema = Serializer.DefaultOptions.GetYamlSchemaAsNode(typeof(PocoDisallowingUnmappedMembers));
            YamlNode? yamlWithUnmappedProperties = YamlNode.Parse("""{ "UnmappedProperty" : {} }""");
            AssertDoesNotMatchSchema(schema, yamlWithUnmappedProperties);
        }

        [Fact]
        public void GetYamlSchemaAsNode_NullInputs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ((YamlSerializerOptions)null!).GetYamlSchemaAsNode(typeof(int)));
            Assert.Throws<ArgumentNullException>(() => Serializer.DefaultOptions.GetYamlSchemaAsNode((Type)null!));
            Assert.Throws<ArgumentNullException>(() => ((YamlTypeInfo)null!).GetYamlSchemaAsNode());
        }

        [Fact]
        public void GetYamlSchemaAsNode_NoResolver_ThrowInvalidOperationException()
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<InvalidOperationException>(() => options.GetYamlSchemaAsNode(typeof(int)));
        }

        [Fact]
        public void YamlSerializerOptions_SmallMaxDepth_ThrowsInvalidOperationException()
        {
            var options = new YamlSerializerOptions(Serializer.DefaultOptions) { MaxDepth = 1 };
            var ex = Assert.Throws<InvalidOperationException>(() => options.GetYamlSchemaAsNode(typeof(PocoWithRecursiveMembers)));
            Assert.Contains("depth", ex.Message);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(SimplePoco))]
        [InlineData(typeof(DiscriminatedUnion))]
        public void YamlSchemaExporterContext_BaseTypeInfo_ReturnsExpectedValue(Type type)
        {
            bool isCallbackInvoked = false;
            YamlSerializerOptions options = Serializer.DefaultOptions;
            YamlSchemaExporterOptions exporterOptions = new()
            {
                TransformSchemaNode = (ctx, node) =>
                {
                    if (typeof(DiscriminatedUnion).IsAssignableFrom(ctx.TypeInfo.Type) &&
                       typeof(DiscriminatedUnion) != ctx.TypeInfo.Type)
                    {
                        Assert.NotNull(ctx.BaseTypeInfo);
                        Assert.Equal(typeof(DiscriminatedUnion), ctx.BaseTypeInfo.Type);
                    }
                    else
                    {
                        Assert.Null(ctx.BaseTypeInfo);
                    }

                    isCallbackInvoked = true;
                    return node;
                }
            };

            options.GetYamlSchemaAsNode(type, exporterOptions);
            Assert.True(isCallbackInvoked);
        }

        [Fact]
        public void ReferenceHandlePreserve_Enabled_ThrowsNotSupportedException()
        {
            var options = new YamlSerializerOptions(Serializer.DefaultOptions) { ReferenceHandler = ReferenceHandler.Preserve };
            options.MakeReadOnly();

            var ex = Assert.Throws<NotSupportedException>(() => options.GetYamlSchemaAsNode(typeof(SimplePoco)));
            Assert.Contains("ReferenceHandler.Preserve", ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void YamlSchemaExporterOptions_DefaultSettings(bool useSingleton)
        {
            YamlSchemaExporterOptions options = useSingleton ? YamlSchemaExporterOptions.Default : new();

            Assert.False(options.TreatNullObliviousAsNonNullable);
            Assert.Null(options.TransformSchemaNode);
        }

        [Fact]
        public void YamlSchemaExporterOptions_Default_IsSame()
        {
            Assert.Same(YamlSchemaExporterOptions.Default, YamlSchemaExporterOptions.Default);
        }

        [ConditionalFact(typeof(YamlSerializer), nameof(YamlSerializer.IsReflectionEnabledByDefault))]
        [RequiresUnreferencedCode("Uses private reflection to access System.Text.Yaml converter internals.")]
        [RequiresDynamicCode("Uses private reflection to access System.Text.Yaml converter internals.")]
        public void LegacySchemaExporter_CanAccessReflectedMembers()
        {
            // A number of libraries such as Microsoft.Extensions.AI and Semantic Kernel
            // rely on a polyfilled version of YamlSchemaExporter for System.Text.Yaml v8
            // that uses private reflection to access necessary metadata. This test validates
            // that the necessary members are still present in newer implementations of STJ.

            YamlStringEnumConverter converter = new(namingPolicy: YamlNamingPolicy.CamelCase, allowIntegerValues: false);
            YamlSerializerOptions options = new(YamlSerializerOptions.Default) { Converters = { converter } };
            YamlConverter nullableConverter = options.GetConverter(typeof(BindingFlags?));

            Type nullableConverterType = nullableConverter.GetType();
            Assert.True(nullableConverterType.IsGenericType);
            Assert.StartsWith("System.Text.Yaml.Serialization.Converters.NullableConverter`1", nullableConverterType.FullName);

            FieldInfo elementConverterField = nullableConverterType.GetField("_elementConverter", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(elementConverterField);
            var enumConverter = (YamlConverter)elementConverterField.GetValue(nullableConverter);
            Assert.NotNull(enumConverter);

            Type enumConverterType = enumConverter.GetType();
            Assert.True(enumConverterType.IsGenericType);
            Assert.StartsWith("System.Text.Yaml.Serialization.Converters.EnumConverter`1", enumConverterType.FullName);

            FieldInfo namingPolicyField = enumConverterType.GetField("_namingPolicy", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(namingPolicyField);
            Assert.Same(YamlNamingPolicy.CamelCase, namingPolicyField.GetValue(enumConverter));

            FieldInfo converterOptionsField = enumConverterType.GetField("_converterOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(converterOptionsField);
            Assert.Equal(1, (int)converterOptionsField.GetValue(enumConverter));

            YamlPropertyInfo propertyInfo = PocoWithPropertyContext.Default.PocoWithProperty.Properties.Single();
            PropertyInfo memberNameProperty = typeof(YamlPropertyInfo).GetProperty("MemberName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(memberNameProperty);
            Assert.Equal("Value", memberNameProperty.GetValue(propertyInfo));
        }

        record PocoWithProperty(int Value);

        [YamlSerializable(typeof(PocoWithProperty))]
        partial class PocoWithPropertyContext : YamlSerializerContext;

        protected void AssertValidYamlSchema(Type type, string expectedYamlSchema, YamlNode actualYamlSchema)
        {
            YamlNode? expectedYamlSchemaNode = YamlNode.Parse(expectedYamlSchema, documentOptions: new() { CommentHandling = YamlCommentHandling.Skip, AllowTrailingCommas = true });

            if (!YamlNode.DeepEquals(expectedYamlSchemaNode, actualYamlSchema))
            {
                throw new XunitException($"""
                Generated schema does not match the expected specification.
                Expected:
                {FormatYaml(expectedYamlSchemaNode)}
                Actual:
                {FormatYaml(actualYamlSchema)}
                """);
            }
        }

        protected void AssertDocumentMatchesSchema(YamlNode schema, YamlNode? instance)
        {
            EvaluationResults results = EvaluateSchemaCore(schema, instance);
            if (!results.IsValid)
            {
                IEnumerable<string> errors = results.Details
                    .Where(d => d.HasErrors)
                    .SelectMany(d => d.Errors!.Select(error => $"Path:${d.InstanceLocation} {error.Key}:{error.Value}"));

                throw new XunitException($"""
                Instance JSON document does not match the specified schema.
                Schema:
                {FormatYaml(schema)}
                Instance:
                {FormatYaml(instance)}
                Errors:
                {string.Join(Environment.NewLine, errors)}
                """);
            }
        }

        protected void AssertDoesNotMatchSchema(YamlNode schema, YamlNode? instance)
        {
            EvaluationResults results = EvaluateSchemaCore(schema, instance);
            if (results.IsValid)
            {
                throw new XunitException($"""
                Instance JSON document matches the specified schema.
                Schema:
                {FormatYaml(schema)}
                Instance:
                {FormatYaml(instance)}
                """);
            }
        }

        private EvaluationResults EvaluateSchemaCore(YamlNode schema, YamlNode? instance)
        {
            JsonSchema yamlSchema = JsonSchema.FromText(schema.ToYamlString());
            return yamlSchema.Evaluate(instance, s_evaluationOptions);
        }

        private static readonly EvaluationOptions s_evaluationOptions = new()
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true,
        };

        private string FormatYaml(YamlNode? node) =>
            YamlSerializer.Serialize(node, _indentedOptions.GetTypeInfo<YamlNode>());
    }
}
