// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Verifies union recognition and the observable contract of
    /// <see cref="YamlTypeInfo{T}.UnionDeconstructor"/> and
    /// <see cref="YamlTypeInfo{T}.UnionConstructor"/> for convention-based unions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same contract applies to both the reflection and source-generated paths, even
    /// though the two implementations differ:
    /// </para>
    /// <list type="bullet">
    ///   <item>The reflection path extracts the case value from a public instance
    ///         <c>object Value</c> property and walks the runtime type's hierarchy to locate
    ///         the nearest declared case (with caching).</item>
    ///   <item>The source generator emits compiler-resolved case patterns ordered most-derived-first.</item>
    /// </list>
    /// <para>
    /// Both implementations must satisfy the same observable contract:
    /// </para>
    /// <list type="bullet">
    ///   <item>The deconstructor returns the *declared* case type — the nearest declared
    ///         ancestor of the case value's runtime type — never the runtime type itself when
    ///         it is a derived non-declared subtype.</item>
    ///   <item>The constructor invokes single-parameter constructors (never conversion operators).</item>
    ///   <item>The constructor dispatches non-declared but assignable case types to the nearest
    ///         declared ancestor's constructor.</item>
    ///   <item>A null value is accepted only when a nullable case exists; otherwise the constructor
    ///         throws.</item>
    /// </list>
    /// </remarks>
    public abstract class UnionTests(YamlSerializerWrapper serializerUnderTest) : SerializerTests(serializerUnderTest)
    {
        public union MixedUnion(int, string, Payload);

        public class Payload
        {
            public string? Name { get; set; }
            public int[]? Values { get; set; }
        }

        [Fact]
        public void CanonicalUnionShape_IsRecognizedWithoutYamlUnionAnnotation()
        {
            YamlTypeInfo<MixedUnion> typeInfo = Serializer.GetTypeInfo<MixedUnion>();

            Assert.Equal(YamlTypeInfoKind.Union, typeInfo.Kind);
            Assert.NotNull(typeInfo.UnionConstructor);
            Assert.NotNull(typeInfo.UnionDeconstructor);
            Assert.Equal(3, typeInfo.UnionCases.Count);
            Assert.Equal(typeof(int), typeInfo.UnionCases[0].CaseType);
            Assert.Equal(typeof(string), typeInfo.UnionCases[1].CaseType);
            Assert.Equal(typeof(Payload), typeInfo.UnionCases[2].CaseType);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CanonicalUnionShape_RoundTripsMixedYamlShapes()
        {
            string intYaml = await Serializer.SerializeWrapper(new MixedUnion(42));
            Assert.Equal("42", intYaml);
            MixedUnion? intResult = await Serializer.DeserializeWrapper<MixedUnion>("42");
            Assert.Equal(42, GetUnionValue(intResult!));

            string stringYaml = await Serializer.SerializeWrapper(new MixedUnion("hello"));
            Assert.Equal("\"hello\"", stringYaml);
            MixedUnion? stringResult = await Serializer.DeserializeWrapper<MixedUnion>("\"hello\"");
            Assert.Equal("hello", GetUnionValue(stringResult!));

            Payload payload = new() { Name = "payload", Values = new[] { 1, 2, 3 } };
            string payloadYaml = await Serializer.SerializeWrapper(new MixedUnion(payload));
            YamlTestHelper.AssertYamlEqual("""{"Name":"payload","Values":[1,2,3]}""", payloadYaml);

            MixedUnion? payloadResult = await Serializer.DeserializeWrapper<MixedUnion>(payloadYaml);
            Payload payloadValue = Assert.IsType<Payload>(GetUnionValue(payloadResult!));
            Assert.Equal("payload", payloadValue.Name);
            Assert.Equal(new[] { 1, 2, 3 }, payloadValue.Values);
        }

        public class Foo
        {
            public int Id { get; set; }
        }

        public class Bar
        {
            public string? Name { get; set; }
        }

        public class Baz
        {
            public bool Flag { get; set; }
        }

#pragma warning disable STYML1227
        public union ObjectUnion(Foo, Bar, Baz);
#pragma warning restore STYML1227

        [Fact]
        public async Task ObjectUnionShape_SerializesButDefaultDeserializationIsAmbiguous()
        {
            ObjectUnion value = new(new Foo { Id = 42 });

            string yaml = await Serializer.SerializeWrapper(value);
            YamlTestHelper.AssertYamlEqual("""{"Id":42}""", yaml);

            YamlException ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<ObjectUnion>(yaml));

            Assert.Contains(nameof(ObjectUnion), ex.Message);
            Assert.Contains("Object", ex.Message);
        }

        [YamlUnion(TypeClassifier = typeof(IntStringClassifierFactory))]
        public union UnionWithCustomClassifier(int, string);

        public sealed class IntStringClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                Assert.Equal(typeof(UnionWithCustomClassifier), context.DeclaringType);
                Assert.Equal(2, context.UnionCases.Count);
                Assert.Empty(context.DerivedTypes);
                return true;
            }

            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                Assert.Equal(typeof(UnionWithCustomClassifier), context.DeclaringType);
                Assert.Equal(2, context.UnionCases.Count);
                return static (ref Utf8YamlReader reader) => reader.TokenType switch
                {
                    YamlTokenType.Number => typeof(int),
                    YamlTokenType.String => typeof(string),
                    _ => null,
                };
            }
        }

        [Fact]
        public async Task PerTypeTypeClassifier_RoundTrips()
        {
            UnionWithCustomClassifier? fromInt = await Serializer.DeserializeWrapper<UnionWithCustomClassifier>("42");
            Assert.NotNull(fromInt);
            Assert.Equal(42, GetUnionValue(fromInt!));

            UnionWithCustomClassifier? fromString = await Serializer.DeserializeWrapper<UnionWithCustomClassifier>("\"hello\"");
            Assert.NotNull(fromString);
            Assert.Equal("hello", GetUnionValue(fromString!));
        }

        // The factory-resolved YamlTypeClassifier delegate must be visible on
        // YamlTypeInfo.TypeClassifier by the time any modifier runs, otherwise modifiers
        // cannot inspect, wrap, or replace the resolved classifier — which is the whole
        // point of contract customization. Validates both reflection and source-gen.
        [Fact]
        public void PerTypeTypeClassifier_IsVisibleToModifier()
        {
            YamlTypeClassifier? observedAtModifierTime = null;

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(UnionWithCustomClassifier))
                {
                    observedAtModifierTime = typeInfo.TypeClassifier;
                }
            });

            // Trigger configuration of the type info via GetTypeInfo (modifiers run during
            // resolver invocation, before Configure).
            options.MakeReadOnly();
            _ = options.GetTypeInfo(typeof(UnionWithCustomClassifier));

            Assert.NotNull(observedAtModifierTime);
        }

        [YamlConverter(typeof(CustomConverter))]
        public class CustomCase
        {
            public string? Value { get; init; }
        }

        public sealed class CustomConverter : YamlConverter<CustomCase>
        {
            public override CustomCase? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                new() { Value = reader.GetString() };

            public override void Write(Utf8YamlWriter writer, CustomCase value, YamlSerializerOptions options) =>
                writer.WriteStringValue(value.Value);
        }

        public class OtherCase
        {
        }

        public sealed class UnionWithOtherCaseOptionsClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                if (context.DeclaringType != typeof(UnionWithCustomConverterCase))
                {
                    return false;
                }

                bool containsOtherCase = false;
                foreach (YamlUnionCaseInfo caseInfo in context.UnionCases)
                {
                    containsOtherCase |= caseInfo.CaseType == typeof(OtherCase);
                }

                Assert.True(containsOtherCase);
                Assert.Empty(context.DerivedTypes);
                return containsOtherCase;
            }

            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                Assert.Equal(typeof(UnionWithCustomConverterCase), context.DeclaringType);

                return static (ref Utf8YamlReader reader) =>
                    reader.TokenType is YamlTokenType.StartObject ? typeof(OtherCase) : null;
            }
        }

        public sealed class SingleObjectUnionClassifierFactory : YamlTypeClassifierFactory<SingleObjectUnion>
        {
            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                throw new InvalidOperationException("This factory should not be selected.");
        }

        public sealed class GenericUnionWithCustomConverterCaseClassifierFactory : YamlTypeClassifierFactory<UnionWithCustomConverterCase>
        {
            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                Assert.Equal(typeof(UnionWithCustomConverterCase), context.DeclaringType);

                return static (ref Utf8YamlReader reader) =>
                    reader.TokenType is YamlTokenType.StartObject ? typeof(OtherCase) : null;
            }
        }

#pragma warning disable STYML1227
        public union UnionWithCustomConverterCase(CustomCase, OtherCase);
#pragma warning restore STYML1227

        [Fact]
        public async Task UnionWithCustomConverterCase_NoClassifier_DeserializeThrows()
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<UnionWithCustomConverterCase>("{}"));

            Assert.Contains(nameof(UnionWithCustomConverterCase), ex.Message);
            Assert.Contains("Object", ex.Message);
        }

        [Fact]
        public async Task UnionWithCustomConverterCase_NoClassifier_UnambiguousShapeDispatches()
        {
            UnionWithCustomConverterCase? result =
                await Serializer.DeserializeWrapper<UnionWithCustomConverterCase>("\"value\"");

            CustomCase customCase = Assert.IsType<CustomCase>(GetUnionValue(result!));
            Assert.Equal("value", customCase.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithCustomConverterCase_NoClassifier_SerializeWorks()
        {
            // Serialization is unaffected by the presence of custom-converter cases —
            // it goes through the deconstructor and the case type's own converter.
            UnionWithCustomConverterCase value = new(new OtherCase());
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        public async Task UnionWithCustomConverterCase_WithClassifier_RoundTrips()
        {
            // With a custom classifier, the runtime bypasses the value-shape map entirely.
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(UnionWithCustomConverterCase))
                {
                    typeInfo.TypeClassifier = static (ref Utf8YamlReader _) => typeof(OtherCase);
                }
            });

            UnionWithCustomConverterCase? value = await Serializer.DeserializeWrapper<UnionWithCustomConverterCase>("{}", options);
            Assert.NotNull(value);
            Assert.IsType<OtherCase>(GetUnionValue(value!));
        }

        [Fact]
        public async Task OptionsTypeClassifier_UsesUnionContextToSelectFactory()
        {
            YamlSerializerOptions options = Serializer.CreateOptions(configure: static options =>
                options.TypeClassifiers.Add(new UnionWithOtherCaseOptionsClassifierFactory()));

            UnionWithCustomConverterCase? value = await Serializer.DeserializeWrapper<UnionWithCustomConverterCase>("{}", options);

            Assert.NotNull(value);
            Assert.IsType<OtherCase>(GetUnionValue(value!));
        }

        [Fact]
        public void OptionsTypeClassifier_IsVisibleToModifier()
        {
            YamlTypeClassifier? observedAtModifierTime = null;

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(UnionWithCustomConverterCase))
                {
                    observedAtModifierTime = typeInfo.TypeClassifier;
                }
            });

            options.TypeClassifiers.Add(new UnionWithOtherCaseOptionsClassifierFactory());
            options.MakeReadOnly();
            _ = options.GetTypeInfo(typeof(UnionWithCustomConverterCase));

            Assert.NotNull(observedAtModifierTime);
        }

        [Fact]
        public async Task GenericOptionsTypeClassifier_AnchorsToDeclaringType()
        {
            YamlSerializerOptions options = Serializer.CreateOptions(configure: static options =>
            {
                options.TypeClassifiers.Add(new SingleObjectUnionClassifierFactory());
                options.TypeClassifiers.Add(new GenericUnionWithCustomConverterCaseClassifierFactory());
            });

            UnionWithCustomConverterCase? value = await Serializer.DeserializeWrapper<UnionWithCustomConverterCase>("{}", options);

            Assert.NotNull(value);
            Assert.IsType<OtherCase>(GetUnionValue(value!));
        }

#pragma warning disable STYML1227
        public union UnionWithIntAndLongCase(int, long);
#pragma warning restore STYML1227

        [Fact]
        public async Task UnionWithOverlappingNumericCases_DeserializeThrows()
        {
            // Configure-time succeeds. Deserialization of a Number token throws because
            // two declared cases share the Number token kind.
            YamlException ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<UnionWithIntAndLongCase>("42"));

            Assert.Contains(nameof(UnionWithIntAndLongCase), ex.Message);
            Assert.Contains("Number", ex.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithOverlappingNumericCases_SerializeWorks()
        {
            // Serialization is unaffected by the ambiguity — the deconstructor dispatches
            // by case type, not token type.
            UnionWithIntAndLongCase value = new(42);
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("42", yaml);
        }

        // Partial ambiguity: Number-token cases overlap (int + long) but String-token case
        // (string) is unique. Deserialization should succeed for unique tokens and only fail
        // for the ambiguous ones — not block the entire union.
#pragma warning disable STYML1227
        public union UnionWithMixedAmbiguity(int, long, string);
#pragma warning restore STYML1227

        [Fact]
        public async Task UnionWithMixedAmbiguity_UniqueTokenDeserializes()
        {
            // String token routes uniquely to the string case despite int/long ambiguity
            // on Number tokens. Configure does not throw.
            UnionWithMixedAmbiguity? value = await Serializer.DeserializeWrapper<UnionWithMixedAmbiguity>("\"hello\"");
            Assert.NotNull(value);
            Assert.Equal("hello", GetUnionValue(value!));
        }

        [Fact]
        public async Task UnionWithMixedAmbiguity_AmbiguousTokenStillThrows()
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<UnionWithMixedAmbiguity>("42"));
            Assert.Contains("Number", ex.Message);
        }

        [Fact]
        public void UnionWithMixedAmbiguity_GetTypeInfoSucceeds()
        {
            // The Configure path must not throw even though the union has an ambiguous token.
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo<UnionWithMixedAmbiguity>();
            Assert.NotNull(typeInfo);
        }

        [YamlNumberHandling(YamlNumberHandling.Strict)]
        public union IntOrString(int, string);

        [Theory]
        [InlineData("42", 42)]
        [InlineData("\"hello\"", "hello")]
        [InlineData("\"42\"", "42")]
        public async Task UnionNumberHandling_StrictTypeAttributeOverridesWebDefaults(string yaml, object expectedValue)
        {
            YamlSerializerOptions options = new(YamlSerializerDefaults.Web)
            {
                TypeInfoResolver = Serializer.DefaultOptions.TypeInfoResolver,
            };

            IntOrString? value = await Serializer.DeserializeWrapper<IntOrString>(yaml, options);

            Assert.Equal(expectedValue, GetUnionValue(value!));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public union NumberHandlingUnion(int, int[], bool);

        [Theory]
        [InlineData("42", "\"42\"")]
        [InlineData("\"42\"", "\"42\"")]
        [InlineData("[1,2,3,4,5,6,7,8]", "[\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\"]")]
        [InlineData("[\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\"]", "[\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\"]")]
        [InlineData("true", "true")]
        [Trait("category", "jsonoutput")]
        public async Task UnionNumberHandling_TypeAttributeAppliesToCaseConverter(string yaml, string expectedYaml)
        {
            YamlSerializerOptions options = Serializer.CreateOptions(configure: options =>
            {
                options.NumberHandling = YamlNumberHandling.Strict;
                options.DefaultBufferSize = 1;
            });

            NumberHandlingUnion value = await Serializer.DeserializeWrapper<NumberHandlingUnion>(yaml, options);
            Assert.Equal(expectedYaml, await Serializer.SerializeWrapper(value, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionNumberHandling_StrictTypeAttributeOverridesGlobalWriteAsString()
        {
            YamlSerializerOptions options = Serializer.CreateOptions(
                configure: options => options.NumberHandling = YamlNumberHandling.WriteAsString);

            Assert.Equal("42", await Serializer.SerializeWrapper(new IntOrString(42), options));
        }

        [Theory]
        [MemberData(nameof(YamlTestHelper.GetUnionCaseNumberHandlingPrecedenceTestData), MemberType = typeof(YamlTestHelper))]
        [Trait("category", "jsonoutput")]
        public async Task UnionNumberHandling_CaseConvertersHonorPrecedence(
            YamlNumberHandling globalHandling, YamlNumberHandling? unionHandling, YamlNumberHandling? caseHandling, YamlNumberHandling expectedHandling)
        {
            foreach ((Type unionType, Type numberType) in new[] { (typeof(IntOrBool), typeof(int)), (typeof(SingleNullableValueTypeUnion), typeof(int?)) })
            {
                YamlSerializerOptions options = Serializer.CreateOptions(
                    configure: options => options.NumberHandling = globalHandling,
                    modifier: typeInfo =>
                    {
                        if (typeInfo.Type == unionType)
                        {
                            typeInfo.NumberHandling = unionHandling;
                        }
                        else if (typeInfo.Type == numberType)
                        {
                            typeInfo.NumberHandling = caseHandling;
                        }
                    });

                object value = await Serializer.DeserializeWrapper("42", unionType, options);
                Assert.Equal(42, ((IUnion)value).Value);

                string expectedYaml = (expectedHandling & YamlNumberHandling.WriteAsString) != 0 ? "\"42\"" : "42";
                Assert.Equal(expectedYaml, await Serializer.SerializeWrapper(value, unionType, options));

                if ((expectedHandling & YamlNumberHandling.AllowReadingFromString) != 0)
                {
                    value = await Serializer.DeserializeWrapper("\"42\"", unionType, options);
                    Assert.Equal(42, ((IUnion)value).Value);
                }
                else
                {
                    await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper("\"42\"", unionType, options));
                }

                if (numberType == typeof(int?))
                {
                    value = await Serializer.DeserializeWrapper("null", unionType, options);
                    Assert.Null(((IUnion)value).Value);
                    Assert.Equal("null", await Serializer.SerializeWrapper(value, unionType, options));
                }
            }
        }

        [Theory]
        [InlineData(YamlNumberHandling.Strict, YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString, """{"Name":"item","Values":["42"]}""")]
        [InlineData(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString, YamlNumberHandling.Strict, """{"Name":"item","Values":[42]}""")]
        public async Task UnionNumberHandling_DoesNotOverridePocoMembers(
            YamlNumberHandling unionHandling, YamlNumberHandling pocoHandling, string expectedYaml)
        {
            YamlSerializerOptions options = Serializer.CreateOptions(
                modifier: typeInfo =>
                {
                    if (typeInfo.Type == typeof(MixedUnion))
                    {
                        typeInfo.NumberHandling = unionHandling;
                    }
                    else if (typeInfo.Type == typeof(Payload))
                    {
                        typeInfo.NumberHandling = pocoHandling;
                    }
                });

            MixedUnion value = new(new Payload { Name = "item", Values = [42] });
            YamlTestHelper.AssertYamlEqual(expectedYaml, await Serializer.SerializeWrapper(value, options));
            MixedUnion? result = await Serializer.DeserializeWrapper<MixedUnion>(expectedYaml, options);
            Assert.Equal(new[] { 42 }, Assert.IsType<Payload>(GetUnionValue(result!)).Values);
        }

        public class Animal { }
        public class Dog : Animal { }
        public class Lab : Dog { }

        // Declared cases: (Animal, Dog). Topologically sorted: Dog before Animal.
#pragma warning disable STYML1227
        public union HierarchyUnion(Animal, Dog);
#pragma warning restore STYML1227

        [Fact]
        public void Deconstructor_DerivedRuntimeType_ReturnsNearestDeclaredCase()
        {
            YamlTypeInfo<HierarchyUnion> typeInfo = Serializer.GetTypeInfo<HierarchyUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            Lab lab = new();
            HierarchyUnion union = new HierarchyUnion((Dog)lab);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);

            // Lab is a derived runtime type that is NOT a declared case. The contract requires
            // dispatching to the nearest declared ancestor — Dog.
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(lab, caseValue);
        }

        [Fact]
        public void Deconstructor_DeclaredCaseRuntimeType_ReturnsCaseType()
        {
            YamlTypeInfo<HierarchyUnion> typeInfo = Serializer.GetTypeInfo<HierarchyUnion>();

            Dog dog = new();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(new HierarchyUnion(dog));

            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(dog, caseValue);
        }

        [Fact]
        public void Deconstructor_BaseDeclaredCase_ReturnsBaseCaseType()
        {
            YamlTypeInfo<HierarchyUnion> typeInfo = Serializer.GetTypeInfo<HierarchyUnion>();

            Animal animal = new();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(new HierarchyUnion(animal));

            Assert.Equal(typeof(Animal), caseType);
            Assert.Same(animal, caseValue);
        }

        [Fact]
        public void Deconstructor_NullUnionInstance_ReturnsNullCaseTypeAndValue()
        {
            YamlTypeInfo<UserDefinedAttributeOnlyUnion> typeInfo = Serializer.GetTypeInfo<UserDefinedAttributeOnlyUnion>();

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(null!);

            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public void Deconstructor_UnionWithNullValue_NoNullableCase_ReturnsNullPair()
        {
            // The deconstructor must succeed when a case payload is null even on a union
            // with no nullable case (e.g. HierarchyUnion(Animal) where Animal is declared
            // non-nullable but the user passed null!). Returning (null, null) signals
            // YamlUnionConverter to write JSON null — matching the class-record behavior
            // described in https://github.com/dotnet/runtime/issues/128834.
            YamlTypeInfo<HierarchyUnion> typeInfo = Serializer.GetTypeInfo<HierarchyUnion>();

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(new HierarchyUnion((Animal)null!));

            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public void Constructor_DerivedDeclaredCase_RoundTripsThroughDeconstructor()
        {
            YamlTypeInfo<HierarchyUnion> typeInfo = Serializer.GetTypeInfo<HierarchyUnion>();

            Lab lab = new();
            HierarchyUnion union = typeInfo.UnionConstructor!(typeof(Dog), lab);

            // Round-trip through deconstructor verifies the formal case type is preserved.
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(lab, caseValue);
        }

        // Two nullable reference-type cases in an inheritance relationship: declaration order
        // is [Animal?, Dog?] but the topological-most-derived-first order used for runtime
        // dispatch of non-null values is [Dog?, Animal?].
#pragma warning disable STYML1227
        public union HierarchyNullableUnion(Animal?, Dog?);
#pragma warning restore STYML1227

        [Fact]
        public async Task NullableCase_MultipleNullableCasesInHierarchy_DispatchAgreesWithUnionCases()
        {
            YamlTypeInfo<HierarchyNullableUnion> typeInfo = Serializer.GetTypeInfo<HierarchyNullableUnion>();

            // The canonical nullable case — the one JSON null deserializes through and the
            // one a null-valued union deconstructs to — must be the first nullable entry in
            // UnionCases. YamlUnionConverter looks up UnionNullableCaseType (cached from that
            // same scan) before invoking the constructor delegate's null fast-path, so the
            // delegate must dispatch through the same case to keep metadata and behavior in
            // agreement.
            Type expectedCaseType = typeInfo.UnionCases.First(c => c.IsNullable).CaseType;

            (Type? deconstructed, object? value) = typeInfo.UnionDeconstructor!(new HierarchyNullableUnion((Animal?)null));
            Assert.Equal(expectedCaseType, deconstructed);
            Assert.Null(value);

            (deconstructed, value) = typeInfo.UnionDeconstructor!(new HierarchyNullableUnion((Dog?)null));
            Assert.Equal(expectedCaseType, deconstructed);
            Assert.Null(value);

            HierarchyNullableUnion fromCtor = typeInfo.UnionConstructor!(typeof(Animal), null);
            (deconstructed, value) = typeInfo.UnionDeconstructor!(fromCtor);
            Assert.Equal(expectedCaseType, deconstructed);
            Assert.Null(value);

            // JSON null deserialization exercises YamlUnionConverter's UnionNullableCaseType
            // lookup followed by the constructor null fast-path.
            HierarchyNullableUnion fromYaml = await Serializer.DeserializeWrapper<HierarchyNullableUnion>("null");
            (deconstructed, value) = typeInfo.UnionDeconstructor!(fromYaml);
            Assert.Equal(expectedCaseType, deconstructed);
            Assert.Null(value);
        }

        public union NullableCaseUnion(string?, int);

        [Fact]
        public void Deconstructor_UnionWithNullValue_NullableCase_ReturnsNullCaseTypeAndValue()
        {
            YamlTypeInfo<NullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NullableCaseUnion>();

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(new NullableCaseUnion((string?)null));

            Assert.Equal(typeof(string), caseType);
            Assert.Null(caseValue);
        }

        [Union]
        public class UserDefinedNullableAttributeOnlyUnion
        {
            public UserDefinedNullableAttributeOnlyUnion(int v) { Value = v; }
            public UserDefinedNullableAttributeOnlyUnion(string? v) { Value = v; }
            public object? Value { get; }
        }

        [Fact]
        public void Deconstructor_NullUnionInstanceAndNullableNullCase_AreDistinct()
        {
            YamlTypeInfo<UserDefinedNullableAttributeOnlyUnion> typeInfo = Serializer.GetTypeInfo<UserDefinedNullableAttributeOnlyUnion>();

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(null!);

            Assert.Null(caseType);
            Assert.Null(caseValue);

            (caseType, caseValue) = typeInfo.UnionDeconstructor!(new UserDefinedNullableAttributeOnlyUnion((string?)null));

            Assert.Equal(typeof(string), caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public void Constructor_NullValue_NullableCaseExists_BuildsViaNullableCtor()
        {
            YamlTypeInfo<NullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NullableCaseUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            NullableCaseUnion union = typeInfo.UnionConstructor!(typeof(string), null);

            Assert.Null(GetUnionValue(union));
        }

        public union NoNullableCaseUnion(int);

        public class NoNullableCaseUnionContainer
        {
            public NoNullableCaseUnion Union { get; set; }
            public int After { get; set; }
        }

        public union IntOrBool(int, bool);

        public class IntOrBoolContainer
        {
            public IntOrBool Value { get; set; }
        }

        [Fact]
        public void Constructor_NullValue_NoNullableCase_Throws()
        {
            YamlTypeInfo<NoNullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NoNullableCaseUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            // Both implementations surface a runtime exception when null is rejected.
            Assert.ThrowsAny<Exception>(() => typeInfo.UnionConstructor!(typeof(int), null));
        }

        [Fact]
        public void Deconstructor_DefaultValueOfStructUnion_NoNullableCase_ReturnsNullPair()
        {
            YamlTypeInfo<NoNullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NoNullableCaseUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(default);

            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RoundTrip_DefaultValueOfStructUnion_NoNullableCase()
        {
            string yaml = await Serializer.SerializeWrapper(default(NoNullableCaseUnion));
            Assert.Equal("null", yaml);

            // Symmetry: JSON null must deserialize back to default(NoNullableCaseUnion),
            // which deconstructs to (null, null) — the same shape that produced the JSON.
            NoNullableCaseUnion roundtripped = await Serializer.DeserializeWrapper<NoNullableCaseUnion>(yaml);
            YamlTypeInfo<NoNullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NoNullableCaseUnion>();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(roundtripped);
            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public async Task RoundTrip_ContainerWithDefaultStructUnionProperty()
        {
            string yaml = await Serializer.SerializeWrapper(new NoNullableCaseUnionContainer { After = 7 });
            YamlTestHelper.AssertYamlEqual("""{"Union":null,"After":7}""", yaml);

            NoNullableCaseUnionContainer? roundtripped =
                await Serializer.DeserializeWrapper<NoNullableCaseUnionContainer>(yaml);
            Assert.NotNull(roundtripped);
            Assert.Equal(7, roundtripped.After);

            YamlTypeInfo<NoNullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NoNullableCaseUnion>();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(roundtripped.Union);
            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public async Task RoundTrip_ContainerWithMissingUnionProperty_DoesNotThrowOnReserialize()
        {
            NoNullableCaseUnionContainer? container =
                await Serializer.DeserializeWrapper<NoNullableCaseUnionContainer>("""{"After":7}""");
            Assert.NotNull(container);
            Assert.Equal(7, container.After);

            // Step 2: serialize the same instance back. The union property must come
            // back as JSON null — the round-trip must not throw.
            string yaml = await Serializer.SerializeWrapper(container);
            YamlTestHelper.AssertYamlEqual("""{"Union":null,"After":7}""", yaml);
        }

        [Fact]
        public void Deconstructor_DefaultValueOfIntOrBool_ReturnsNullPair()
        {
            YamlTypeInfo<IntOrBool> typeInfo = Serializer.GetTypeInfo<IntOrBool>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(default);

            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RoundTrip_DefaultValueOfIntOrBool()
        {
            string yaml = await Serializer.SerializeWrapper(default(IntOrBool));
            Assert.Equal("null", yaml);

            // Symmetry: JSON null must deserialize back to default(IntOrBool).
            IntOrBool roundtripped = await Serializer.DeserializeWrapper<IntOrBool>(yaml);
            YamlTypeInfo<IntOrBool> typeInfo = Serializer.GetTypeInfo<IntOrBool>();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(roundtripped);
            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public async Task RoundTrip_NewContainerOfIntOrBool()
        {
            // Exact translation of `YamlSerializer.Serialize(new Container())` from the
            // issue comment: the container holds a non-nullable IntOrBool whose default
            // value has no case selected. Before the fix this threw
            // UnionDoesNotAcceptNull at $.Value. Now it must succeed and emit
            // {"Value":null} — and the resulting JSON must round-trip back to a
            // semantically identical container.
            string yaml = await Serializer.SerializeWrapper(new IntOrBoolContainer());
            YamlTestHelper.AssertYamlEqual("""{"Value":null}""", yaml);

            IntOrBoolContainer? roundtripped =
                await Serializer.DeserializeWrapper<IntOrBoolContainer>(yaml);
            Assert.NotNull(roundtripped);

            YamlTypeInfo<IntOrBool> typeInfo = Serializer.GetTypeInfo<IntOrBool>();
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(roundtripped.Value);
            Assert.Null(caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        public void Deconstructor_DefaultValueOfIntOrBool_NonDefaultPayload_DispatchesNormally()
        {
            // Regression guard: the always-null arm must not shadow the per-case arms.
            // Construct IntOrBool with a non-null value and verify the deconstructor
            // still reports the right case type.
            YamlTypeInfo<IntOrBool> typeInfo = Serializer.GetTypeInfo<IntOrBool>();

            (Type? intCaseType, object? intCaseValue) = typeInfo.UnionDeconstructor!(new IntOrBool(42));
            Assert.Equal(typeof(int), intCaseType);
            Assert.Equal(42, intCaseValue);

            (Type? boolCaseType, object? boolCaseValue) = typeInfo.UnionDeconstructor!(new IntOrBool(true));
            Assert.Equal(typeof(bool), boolCaseType);
            Assert.Equal(true, boolCaseValue);
        }

        [Fact]
        public void Constructor_NullValueAndNullCaseType_RoutesToNullableCase()
        {
            // The null payload is a value-based fast path: caseType is ignored when value
            // is null (matches the source-generated `null =>` switch arm). Callers that
            // pass null! as the caseType alongside a null payload must not crash.
            YamlTypeInfo<NullableCaseUnion> typeInfo = Serializer.GetTypeInfo<NullableCaseUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            NullableCaseUnion union = typeInfo.UnionConstructor!(null!, null);
            Assert.Null(GetUnionValue(union));
        }

#pragma warning disable STYML1227 // Union 'ValueTypeNullablePairUnion': ambiguous JSON value type 'Number' is intentional for these tests.
        public union ValueTypeNullablePairUnion(int, int?);
#pragma warning restore STYML1227

        [Fact]
        public void NullableAndNonNullableValueTypeCtorOverloads_ProduceDistinctCases()
        {
            // The (T, T?) ctor overloads each yield their own YamlUnionCaseInfo. Metadata
            // does not silently consolidate to a single nullable supertype case — the
            // union shape stays exactly as declared, and the token-level ambiguity that
            // follows from both cases serializing as a JSON Number surfaces at
            // deserialize time (see UnionWithValueTypeNullableCase_DeserializeNumberThrows).
            YamlTypeInfo<ValueTypeNullablePairUnion> typeInfo = Serializer.GetTypeInfo<ValueTypeNullablePairUnion>();

            Assert.Equal(2, typeInfo.UnionCases.Count);

            YamlUnionCaseInfo intCase = typeInfo.UnionCases[0];
            Assert.Equal(typeof(int), intCase.CaseType);
            Assert.False(intCase.IsNullable);

            YamlUnionCaseInfo nullableIntCase = typeInfo.UnionCases[1];
            Assert.Equal(typeof(int?), nullableIntCase.CaseType);
            Assert.True(nullableIntCase.IsNullable);

            // Constructor: typeof(int) routes to Foo(int); typeof(int?) routes to Foo(int?).
            ValueTypeNullablePairUnion intUnion = typeInfo.UnionConstructor!(typeof(int), 42);
            Assert.Equal(42, GetUnionValue(intUnion));

            ValueTypeNullablePairUnion nullUnion = typeInfo.UnionConstructor!(typeof(int?), null);
            Assert.Null(GetUnionValue(nullUnion));
        }

        [Fact]
        public void ValueTypeNullablePairUnion_Deconstructor_NonNullInt_ReturnsNonNullableCase()
        {
            // Most-derived dispatch: a boxed int payload returns the int case, not the
            // int? case (the underlying T is a strict subtype of Nullable<T> for the
            // purposes of metadata dispatch even though boxing collapses the runtime
            // representation).
            YamlTypeInfo<ValueTypeNullablePairUnion> typeInfo = Serializer.GetTypeInfo<ValueTypeNullablePairUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            ValueTypeNullablePairUnion union = new(42);
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);

            Assert.Equal(typeof(int), caseType);
            Assert.Equal(42, caseValue);
        }

        [Fact]
        public void ValueTypeNullablePairUnion_Deconstructor_NullValue_ReturnsNullableCase()
        {
            // Null payload routes to the nullable case (typeof(int?)) so the
            // downstream converter is NullableConverter<int>, which serializes null
            // as JSON null without round-tripping through unbox-on-write.
            YamlTypeInfo<ValueTypeNullablePairUnion> typeInfo = Serializer.GetTypeInfo<ValueTypeNullablePairUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            ValueTypeNullablePairUnion nullUnion = new((int?)null);
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(nullUnion);

            Assert.Equal(typeof(int?), caseType);
            Assert.Null(caseValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithValueTypeNullableCase_SerializesNullAsYamlNull()
        {
            string yaml = await Serializer.SerializeWrapper<ValueTypeNullablePairUnion>(new ValueTypeNullablePairUnion((int?)null));
            Assert.Equal("null", yaml);
        }

        [Fact]
        public async Task UnionWithValueTypeNullableCase_RoundTripsNullValue()
        {
            string yaml = await Serializer.SerializeWrapper<ValueTypeNullablePairUnion>(new ValueTypeNullablePairUnion((int?)null));
            ValueTypeNullablePairUnion roundTripped = await Serializer.DeserializeWrapper<ValueTypeNullablePairUnion>(yaml);
            Assert.Null(roundTripped.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithValueTypeNullableCase_SerializesNonNullAsNumber()
        {
            string yaml = await Serializer.SerializeWrapper<ValueTypeNullablePairUnion>(new ValueTypeNullablePairUnion(42));
            Assert.Equal("42", yaml);
        }

        [Fact]
        public async Task UnionWithValueTypeNullableCase_DeserializeNumberThrows()
        {
            // Both the int and int? cases claim the JSON Number value shape, which is
            // unresolvable without a user-supplied YamlTypeClassifierFactory. The
            // resolver does NOT throw at configure time; the precise failure is
            // surfaced only when a Number token is actually encountered.
            YamlException ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<ValueTypeNullablePairUnion>("42"));
            Assert.Contains(nameof(ValueTypeNullablePairUnion), ex.Message);
            Assert.Contains("Number", ex.Message);
        }

        public union SingleNullableValueTypeUnion(int?);

        [Fact]
        public void SingleNullableValueTypeUnion_ProducesSingleNullableCase()
        {
            // A solo value-type Nullable<T> ctor yields exactly one YamlUnionCaseInfo whose
            // CaseType is Nullable<T> (typeof(int?)) — there is no implicit second case for
            // the underlying T, even though the emitter must internally generate two switch
            // arms (one for the null payload, one for the boxed-T payload).
            YamlTypeInfo<SingleNullableValueTypeUnion> typeInfo = Serializer.GetTypeInfo<SingleNullableValueTypeUnion>();

            YamlUnionCaseInfo singleCase = Assert.Single(typeInfo.UnionCases);
            Assert.Equal(typeof(int?), singleCase.CaseType);
            Assert.True(singleCase.IsNullable);
        }

        [Fact]
        public void SingleNullableValueTypeUnion_Constructor_DispatchesBothPayloads()
        {
            // typeof(int?) + 42 invokes the Nullable<int> ctor with a value payload.
            // typeof(int?) + null invokes the same ctor with HasValue=false.
            YamlTypeInfo<SingleNullableValueTypeUnion> typeInfo = Serializer.GetTypeInfo<SingleNullableValueTypeUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            SingleNullableValueTypeUnion valueUnion = typeInfo.UnionConstructor!(typeof(int?), 42);
            Assert.Equal(42, GetUnionValue(valueUnion));

            SingleNullableValueTypeUnion nullUnion = typeInfo.UnionConstructor!(typeof(int?), null);
            Assert.Null(GetUnionValue(nullUnion));
        }

        [Fact]
        public void SingleNullableValueTypeUnion_Deconstructor_ReportsNullableCaseForBothPayloads()
        {
            // Both branches of the union — boxed-int (HasValue=true) and null (HasValue=false)
            // — report typeof(int?), because the union declares only the Nullable<int> case.
            YamlTypeInfo<SingleNullableValueTypeUnion> typeInfo = Serializer.GetTypeInfo<SingleNullableValueTypeUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            (Type? valueT, object? valueV) = typeInfo.UnionDeconstructor!(new SingleNullableValueTypeUnion(42));
            Assert.Equal(typeof(int?), valueT);
            Assert.Equal(42, valueV);

            (Type? nullT, object? nullV) = typeInfo.UnionDeconstructor!(new SingleNullableValueTypeUnion((int?)null));
            Assert.Equal(typeof(int?), nullT);
            Assert.Null(nullV);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SingleNullableValueTypeUnion_RoundTripsValueAndNull()
        {
            string valueYaml = await Serializer.SerializeWrapper<SingleNullableValueTypeUnion>(new SingleNullableValueTypeUnion(42));
            string nullYaml = await Serializer.SerializeWrapper<SingleNullableValueTypeUnion>(new SingleNullableValueTypeUnion((int?)null));

            Assert.Equal("42", valueYaml);
            Assert.Equal("null", nullYaml);

            SingleNullableValueTypeUnion valueRoundtrip = await Serializer.DeserializeWrapper<SingleNullableValueTypeUnion>(valueYaml);
            SingleNullableValueTypeUnion nullRoundtrip = await Serializer.DeserializeWrapper<SingleNullableValueTypeUnion>(nullYaml);

            Assert.Equal(42, valueRoundtrip.Value);
            Assert.Null(nullRoundtrip.Value);
        }

        public class PayloadCase
        {
            public string? Name { get; set; }
            public List<int>? Values { get; set; }
        }

        public union SingleObjectUnion(PayloadCase);

        public class UnionContainer
        {
            public SingleObjectUnion? Union { get; set; }
            public int After { get; set; }
        }

        public class NullableScalarUnionContainer
        {
            public MixedUnion? Union { get; set; }
        }

        [YamlUnion(TypeClassifier = typeof(PayloadOrStringClassifierFactory))]
        public union ClassifiedPayloadUnion(PayloadCase, string);

        public sealed class PayloadOrStringClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);
                return context.DeclaringType == typeof(ClassifiedPayloadUnion);
            }

            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.Union, context.Kind);

                return static (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType is YamlTokenType.String)
                    {
                        return typeof(string);
                    }

                    if (reader.TokenType is YamlTokenType.StartObject)
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType is YamlTokenType.PropertyName &&
                                reader.ValueTextEquals(nameof(PayloadCase.Name)))
                            {
                                return typeof(PayloadCase);
                            }
                        }
                    }

                    return null;
                };
            }
        }

        public union AsyncEnumerableUnion(IAsyncEnumerable<int>);

        [Fact]
        public async Task UnionWithDefaultTokenClassification_RoundTripsObjectCaseWithSmallBuffer()
        {
            YamlSerializerOptions options = CreateSmallBufferOptions();
            SingleObjectUnion value = new(new PayloadCase
            {
                Name = "payload",
                Values = new List<int> { 1, 2, 3, 4, 5 }
            });

            string yaml = await Serializer.SerializeWrapper(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Name":"payload","Values":[1,2,3,4,5]}""", yaml);

            SingleObjectUnion? result = await Serializer.DeserializeWrapper<SingleObjectUnion>(yaml, options);

            Assert.NotNull(result);
            PayloadCase payload = Assert.IsType<PayloadCase>(GetUnionValue(result!));
            Assert.Equal("payload", payload.Name);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, payload.Values);
        }

        [Fact]
        public async Task UnionWithDefaultTokenClassification_RoundTripsObjectCaseAsPropertyWithSmallBuffer()
        {
            YamlSerializerOptions options = CreateSmallBufferOptions();
            UnionContainer value = new()
            {
                Union = new SingleObjectUnion(new PayloadCase
                {
                    Name = "nested",
                    Values = new List<int> { 6, 7, 8 }
                }),
                After = 42
            };

            string yaml = await Serializer.SerializeWrapper(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Union":{"Name":"nested","Values":[6,7,8]},"After":42}""", yaml);

            UnionContainer? result = await Serializer.DeserializeWrapper<UnionContainer>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(42, result!.After);
            Assert.NotNull(result.Union);
            PayloadCase payload = Assert.IsType<PayloadCase>(GetUnionValue(result.Union!));
            Assert.Equal("nested", payload.Name);
            Assert.Equal(new[] { 6, 7, 8 }, payload.Values);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithDefaultTokenClassification_RoundTripsNullableScalarCaseAsPropertyWithSmallBuffer()
        {
            YamlSerializerOptions options = CreateSmallBufferOptions();
            NullableScalarUnionContainer value = new()
            {
                Union = new MixedUnion(42),
            };

            string yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("""{"Union":42}""", yaml);

            NullableScalarUnionContainer? result = await Serializer.DeserializeWrapper<NullableScalarUnionContainer>(yaml, options);

            Assert.NotNull(result);
            Assert.True(result!.Union.HasValue);
            MixedUnion union = result.Union.GetValueOrDefault();
            Assert.Equal(42, GetUnionValue(union));

            yaml = await Serializer.SerializeWrapper(new NullableScalarUnionContainer(), options);
            Assert.Equal("""{"Union":null}""", yaml);

            result = await Serializer.DeserializeWrapper<NullableScalarUnionContainer>(yaml, options);

            Assert.NotNull(result);
            Assert.False(result!.Union.HasValue);
        }

        [Fact]
        public async Task UnionWithCustomClassifier_RoundTripsObjectCaseWithSmallBuffer()
        {
            YamlSerializerOptions options = CreateSmallBufferOptions();
            ClassifiedPayloadUnion value = new(new PayloadCase
            {
                Name = "classified",
                Values = new List<int> { 9, 10, 11 }
            });

            string yaml = await Serializer.SerializeWrapper(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Name":"classified","Values":[9,10,11]}""", yaml);

            ClassifiedPayloadUnion? result = await Serializer.DeserializeWrapper<ClassifiedPayloadUnion>(yaml, options);

            Assert.NotNull(result);
            PayloadCase payload = Assert.IsType<PayloadCase>(GetUnionValue(result!));
            Assert.Equal("classified", payload.Name);
            Assert.Equal(new[] { 9, 10, 11 }, payload.Values);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionWithAsyncEnumerableCase_SerializesUsingStreamingConverter()
        {
            if (StreamingSerializer?.IsAsyncSerializer != true)
            {
                return;
            }

            YamlSerializerOptions options = CreateSmallBufferOptions();
            string yaml = await Serializer.SerializeWrapper(new AsyncEnumerableUnion(CreateAsyncEnumerable()), options);

            Assert.Equal("[1,2,3]", yaml);
        }

        private YamlSerializerOptions CreateSmallBufferOptions()
            => Serializer.CreateOptions(configure: static options => options.DefaultBufferSize = 1);

        private static object? GetUnionValue(IUnion union) => union.Value;

        private static async IAsyncEnumerable<int> CreateAsyncEnumerable()
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
            yield return 3;
        }

        // YamlTypeInfo.Configure validates the final union shape — but a modifier can patch
        // partial state (e.g., null out a delegate and supply a different one) and the change
        // must survive through Configure for serialization to round-trip. This works against
        // any resolver: reflection or source-gen alike.
        [Fact]
        public async Task Configure_AcceptsModifierOverride_OfUnionConstructor()
        {
            int constructorInvocations = 0;

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type != typeof(NullableCaseUnion))
                {
                    return;
                }

                YamlTypeInfo<NullableCaseUnion> ti = (YamlTypeInfo<NullableCaseUnion>)typeInfo;
                Func<Type, object?, NullableCaseUnion>? original = ti.UnionConstructor;
                Assert.NotNull(original);

                ti.UnionConstructor = (Type caseType, object? value) =>
                {
                    constructorInvocations++;
                    return original(caseType, value);
                };
            });

            // Number tokens route uniquely to the int case.
            NullableCaseUnion? roundTripped = await Serializer.DeserializeWrapper<NullableCaseUnion>("42", options);
            Assert.NotNull(roundTripped);
            Assert.Equal(42, GetUnionValue(roundTripped!));
            Assert.True(constructorInvocations > 0);
        }

        // Nulling out the constructor delegate via a modifier must cause Configure to throw
        // a clear InvalidOperationException naming the union type. Source-gen and reflection
        // share this validation.
        [Fact]
        public void Configure_ThrowsWhenModifierClearsUnionConstructor()
        {
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(NullableCaseUnion))
                {
                    ((YamlTypeInfo<NullableCaseUnion>)typeInfo).UnionConstructor = null;
                }
            });

            options.MakeReadOnly();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => options.GetTypeInfo(typeof(NullableCaseUnion)));

            Assert.Contains(nameof(NullableCaseUnion), ex.Message);
        }

        // Same for the deconstructor delegate.
        [Fact]
        public void Configure_ThrowsWhenModifierClearsUnionDeconstructor()
        {
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(NullableCaseUnion))
                {
                    ((YamlTypeInfo<NullableCaseUnion>)typeInfo).UnionDeconstructor = null;
                }
            });

            options.MakeReadOnly();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => options.GetTypeInfo(typeof(NullableCaseUnion)));

            Assert.Contains(nameof(NullableCaseUnion), ex.Message);
        }

        // [YamlUnion] alone — must be ignored, type behaves as a regular object.
        [YamlUnion]
        public class UserDefinedYamlUnionOnPlainObject
        {
            public int X { get; set; }
            public string? Y { get; set; }
        }

        [Fact]
        public void YamlUnionAttribute_OnNonUnionType_IsIgnored_TypeInfoIsObject()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo<UserDefinedYamlUnionOnPlainObject>();
            Assert.Equal(YamlTypeInfoKind.Object, typeInfo.Kind);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlUnionAttribute_OnNonUnionType_IsIgnored_RoundTripsAsObject()
        {
            UserDefinedYamlUnionOnPlainObject value = new() { X = 42, Y = "hello" };
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"X":42,"Y":"hello"}""", yaml);

            UserDefinedYamlUnionOnPlainObject? roundTripped = await Serializer.DeserializeWrapper<UserDefinedYamlUnionOnPlainObject>(yaml);
            Assert.NotNull(roundTripped);
            Assert.Equal(42, roundTripped!.X);
            Assert.Equal("hello", roundTripped.Y);
        }

        // IUnion alone must not make a type a union.
        public class UserDefinedUnionViaIUnion : IUnion
        {
            public UserDefinedUnionViaIUnion(int v) { Value = v; }
            public UserDefinedUnionViaIUnion(string v) { Value = v; }
            public object? Value { get; }
        }

        [Fact]
        public void IUnion_Alone_DoesNotTriggerUnionBehavior()
        {
            YamlTypeInfo typeInfo = Serializer.GetTypeInfo<UserDefinedUnionViaIUnion>();
            Assert.Equal(YamlTypeInfoKind.Object, typeInfo.Kind);
        }

        [Union]
        public class UserDefinedAttributeOnlyUnion
        {
            public UserDefinedAttributeOnlyUnion(int v) { Value = v; }
            public UserDefinedAttributeOnlyUnion(string v) { Value = v; }
            public object? Value { get; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnionAttribute_WithDuckTypedObjectValue_RoundTrips()
        {
            YamlTypeInfo<UserDefinedAttributeOnlyUnion> typeInfo = Serializer.GetTypeInfo<UserDefinedAttributeOnlyUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);
            Assert.NotNull(typeInfo.UnionDeconstructor);

            UserDefinedAttributeOnlyUnion union = typeInfo.UnionConstructor!(typeof(int), 42);
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);

            Assert.Equal(typeof(int), caseType);
            Assert.Equal(42, caseValue);

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("42", yaml);

            UserDefinedAttributeOnlyUnion? roundTripped = await Serializer.DeserializeWrapper<UserDefinedAttributeOnlyUnion>("\"hello\"");
            Assert.NotNull(roundTripped);
            Assert.Equal("hello", roundTripped!.Value);
        }

        // Both ctor and an implicit conversion operator exist. The convention constructor must
        // invoke the ctor and never the operator. We verify by having the operator throw.
        [Union]
        public class UserDefinedCtorVsImplicitOpUnion
        {
            public UserDefinedCtorVsImplicitOpUnion(int v) { Value = v; CtorWasInvoked = true; }
            public static implicit operator UserDefinedCtorVsImplicitOpUnion(int v) =>
                throw new InvalidOperationException("Implicit conversion operator must not be invoked by the convention constructor.");
            public object? Value { get; }
            public bool CtorWasInvoked { get; }
        }

        [Fact]
        public void Constructor_PrefersSingleParamCtorOverImplicitOperator()
        {
            YamlTypeInfo<UserDefinedCtorVsImplicitOpUnion> typeInfo = Serializer.GetTypeInfo<UserDefinedCtorVsImplicitOpUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            UserDefinedCtorVsImplicitOpUnion union = typeInfo.UnionConstructor!(typeof(int), 42);

            Assert.True(union.CtorWasInvoked);
            Assert.Equal(42, union.Value);
        }

        [Union]
        public readonly struct UserDefinedValueTypeUnion : IUnion
        {
            public UserDefinedValueTypeUnion(int v) { Value = v; }
            public UserDefinedValueTypeUnion(string v) { Value = v; }
            public object? Value { get; }
        }

        [Fact]
        public void UserDefinedValueTypeUnion_ConventionDelegates_RoundTripCaseValue()
        {
            YamlTypeInfo<UserDefinedValueTypeUnion> typeInfo = Serializer.GetTypeInfo<UserDefinedValueTypeUnion>();

            UserDefinedValueTypeUnion union = typeInfo.UnionConstructor!(typeof(int), 42);
            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);

            Assert.Equal(typeof(int), caseType);
            Assert.Equal(42, caseValue);
        }

        // Hand-rolled discriminated union using the "non-boxing access pattern" described in
        // https://github.com/dotnet/csharplang/blob/main/proposals/unions.md#non-boxing-access-members.
        // The struct keeps a discriminator field, exposes the boxed payload through IUnion.Value
        // (which is what STJ deconstructs), and exposes the user-facing payload through typed
        // TryGetValue(out T) overloads (which STJ does not consume). The Tag property lets tests
        // assert which case constructor STJ selected on the way back in.
#pragma warning disable STYML1227
        [Union]
        public readonly struct CustomDiscriminatedAnimalUnion : IUnion
        {
            public CustomDiscriminatedAnimalUnion(Animal animal) { Tag = 0; Value = animal; }
            public CustomDiscriminatedAnimalUnion(Dog dog) { Tag = 1; Value = dog; }

            public int Tag { get; }
            public object? Value { get; }
            public bool HasValue => Value is not null;

            public bool TryGetValue(out Animal? animal)
            {
                if (Tag == 0 && Value is Animal a)
                {
                    animal = a;
                    return true;
                }

                animal = null;
                return false;
            }

            public bool TryGetValue(out Dog? dog)
            {
                if (Tag == 1 && Value is Dog d)
                {
                    dog = d;
                    return true;
                }

                dog = null;
                return false;
            }
        }
#pragma warning restore STYML1227

        // Non-boxing pattern + most-derived-declared-case wins: a Dog runtime value flows
        // through the Dog case constructor, never the Animal one, on the way out and back in.
        [Fact]
        public void CustomDiscriminatedAnimalUnion_DogRuntimeType_DispatchesToDogCtor()
        {
            YamlTypeInfo<CustomDiscriminatedAnimalUnion> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);
            Assert.NotNull(typeInfo.UnionConstructor);

            Dog dog = new();
            CustomDiscriminatedAnimalUnion original = new(dog);
            Assert.Equal(1, original.Tag);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(dog, caseValue);

            CustomDiscriminatedAnimalUnion rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Equal(1, rebuilt.Tag);
            Assert.True(rebuilt.TryGetValue(out Dog? rebuiltDog));
            Assert.Same(dog, rebuiltDog);
        }

        // When a [Union] type defines `bool TryGetValue(out CaseType)` overloads, the
        // reflection-based deconstructor chains those overloads so the deconstructed caseType is
        // selected by the user-controlled tag rather than the runtime type of the boxed value.
        // So a Dog upcast through the (Animal) constructor decomposes as typeof(Animal). The
        // constructor delegate then round-trips on the deconstructed caseType, so feeding
        // (typeof(Animal), dog) back in selects the (Animal) case constructor and preserves the
        // user-controlled Tag=0.
        //
        // The source generator currently emits a plain `value switch` for both delegates, which
        // dispatches on the runtime type of the boxed value and does not honor TryGetValue
        // overloads. Aligning source-gen with the reflection convention is tracked separately,
        // so the source-generated path is skipped here.
        [Fact]
        public void CustomDiscriminatedAnimalUnion_UpcastDog_DispatchesByTagConvention()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                return;
            }

            YamlTypeInfo<CustomDiscriminatedAnimalUnion> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion>();

            Dog dog = new();
            CustomDiscriminatedAnimalUnion original = new(animal: dog);
            Assert.Equal(0, original.Tag);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Animal), caseType);
            Assert.Same(dog, caseValue);

            CustomDiscriminatedAnimalUnion rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Equal(0, rebuilt.Tag);
            Assert.True(rebuilt.TryGetValue(out Animal? rebuiltAnimal));
            Assert.Same(dog, rebuiltAnimal);
        }

        // Same Tag-keyed convention as above: a Lab (not itself a declared case) passed through
        // the (Animal) constructor decomposes as typeof(Animal) because TryGetValue(out Animal)
        // returns true while TryGetValue(out Dog) returns false. The round-trip again funnels
        // through the (Animal) case constructor since dispatch is keyed on the deconstructed
        // caseType, preserving Tag=0.
        //
        // The source-generated `value switch` does not honor TryGetValue and is skipped here for
        // the same reason as above.
        [Fact]
        public void CustomDiscriminatedAnimalUnion_NonDeclaredDerivedRuntimeType_DispatchesByTagConvention()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                return;
            }

            YamlTypeInfo<CustomDiscriminatedAnimalUnion> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion>();

            Lab lab = new();
            CustomDiscriminatedAnimalUnion original = new(animal: lab);
            Assert.Equal(0, original.Tag);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Animal), caseType);
            Assert.Same(lab, caseValue);

            CustomDiscriminatedAnimalUnion rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Equal(0, rebuilt.Tag);
            Assert.True(rebuilt.TryGetValue(out Animal? rebuiltAnimal));
            Assert.Same(lab, rebuiltAnimal);
        }

        // Sibling of CustomDiscriminatedAnimalUnion that intentionally OMITS TryGetValue overloads.
        // Without the convention, the reflection deconstructor and the source generator both fall
        // back to runtime-type dispatch using union.Value, picking the most-derived declared case
        // (the rule reasserted in https://github.com/dotnet/runtime/pull/128162#discussion_r3370238345).
#pragma warning disable STYML1227
        [Union]
        public readonly struct CustomDiscriminatedAnimalUnion_NoConvention : IUnion
        {
            public CustomDiscriminatedAnimalUnion_NoConvention(Animal animal) { Tag = 0; Value = animal; }
            public CustomDiscriminatedAnimalUnion_NoConvention(Dog dog) { Tag = 1; Value = dog; }

            public int Tag { get; }
            public object? Value { get; }
        }
#pragma warning restore STYML1227

        // Without TryGetValue conventions, runtime-type dispatch wins regardless of which
        // constructor was used: an Animal-via-Dog upcast still flows back through the Dog case.
        [Theory]
        [InlineData(0)] // Dog constructed via the (Animal) ctor — upcast scenario.
        [InlineData(1)] // Dog constructed via the (Dog) ctor — direct scenario.
        public void CustomDiscriminatedAnimalUnion_NoConvention_DogRuntimeType_DispatchesByRuntimeType(int tag)
        {
            YamlTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention>();

            Dog dog = new();
            CustomDiscriminatedAnimalUnion_NoConvention original = tag == 1
                ? new(dog: dog)
                : new(animal: dog);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(dog, caseValue);

            CustomDiscriminatedAnimalUnion_NoConvention rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Same(dog, rebuilt.Value);
        }

        // Without TryGetValue conventions, a derived runtime type (Lab) that is not itself a
        // declared case still flows through its nearest declared ancestor (Dog) — most-derived-wins.
        [Fact]
        public void CustomDiscriminatedAnimalUnion_NoConvention_DerivedRuntimeType_DispatchesToMostDerivedDeclaredCase()
        {
            YamlTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention>();

            Lab lab = new();
            CustomDiscriminatedAnimalUnion_NoConvention original = new(animal: lab);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(lab, caseValue);
        }

        // Without TryGetValue conventions, a plain Animal runtime value selects the Animal case.
        [Fact]
        public void CustomDiscriminatedAnimalUnion_NoConvention_AnimalRuntimeType_DispatchesToAnimalCase()
        {
            YamlTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedAnimalUnion_NoConvention>();

            Animal animal = new();
            CustomDiscriminatedAnimalUnion_NoConvention original = new(animal: animal);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(typeof(Animal), caseType);
            Assert.Same(animal, caseValue);
        }

        // Hand-rolled discriminated union with disjoint JSON value shapes (number vs string),
        // exercising the full SerializeWrapper/DeserializeWrapper path. The non-boxing TryGetValue
        // accessors aren't consumed by STJ, but the Tag/Value pair lets the test assert that the
        // round-tripped instance came from the same case constructor that originally produced it.
        [Union]
        public readonly struct CustomDiscriminatedScalarUnion : IUnion
        {
            public CustomDiscriminatedScalarUnion(int v) { Tag = 0; Value = v; }
            public CustomDiscriminatedScalarUnion(string v) { Tag = 1; Value = v; }

            public int Tag { get; }
            public object? Value { get; }

            public bool TryGetValue(out int v)
            {
                if (Tag == 0 && Value is int i)
                {
                    v = i;
                    return true;
                }

                v = default;
                return false;
            }

            public bool TryGetValue(out string? v)
            {
                if (Tag == 1 && Value is string s)
                {
                    v = s;
                    return true;
                }

                v = null;
                return false;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomDiscriminatedScalarUnion_IntCase_RoundTripsViaIntCtor()
        {
            CustomDiscriminatedScalarUnion original = new(42);
            Assert.Equal(0, original.Tag);

            string yaml = await Serializer.SerializeWrapper(original);
            Assert.Equal("42", yaml);

            CustomDiscriminatedScalarUnion rebuilt = await Serializer.DeserializeWrapper<CustomDiscriminatedScalarUnion>("42");
            Assert.Equal(0, rebuilt.Tag);
            Assert.True(rebuilt.TryGetValue(out int rebuiltInt));
            Assert.Equal(42, rebuiltInt);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomDiscriminatedScalarUnion_StringCase_RoundTripsViaStringCtor()
        {
            CustomDiscriminatedScalarUnion original = new("hello");
            Assert.Equal(1, original.Tag);

            string yaml = await Serializer.SerializeWrapper(original);
            Assert.Equal("\"hello\"", yaml);

            CustomDiscriminatedScalarUnion rebuilt = await Serializer.DeserializeWrapper<CustomDiscriminatedScalarUnion>("\"hello\"");
            Assert.Equal(1, rebuilt.Tag);
            Assert.True(rebuilt.TryGetValue(out string? rebuiltString));
            Assert.Equal("hello", rebuiltString);
        }

        // Disjoint case types (int, string) — topological sorting has nothing to disambiguate, so
        // TryGetValue convention and .Value-based dispatch always agree on the case. This test
        // exercises the deconstructor + constructor delegates directly (no JSON), confirming that
        // the round trip is symmetric: Tag is preserved because the boxed value's runtime type is
        // the only thing distinguishing the cases on both the way out and the way back in.
        [Theory]
        [InlineData(0)] // int case, Tag = 0
        [InlineData(1)] // string case, Tag = 1
        public void CustomDiscriminatedScalarUnion_DirectDelegate_DisjointTypesPreserveTag(int tag)
        {
            YamlTypeInfo<CustomDiscriminatedScalarUnion> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedScalarUnion>();

            CustomDiscriminatedScalarUnion original = tag == 0
                ? new(42)
                : new("hello");

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(tag == 0 ? typeof(int) : typeof(string), caseType);
            Assert.Equal(tag == 0 ? (object)42 : "hello", caseValue);

            CustomDiscriminatedScalarUnion rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Equal(tag, rebuilt.Tag);
            Assert.Equal(original.Value, rebuilt.Value);
        }

        // Disjoint reference case types with no shared base — same property as the scalar union
        // above: there is no inheritance for topological sorting to flatten, so TryGetValue and
        // value-runtime-type dispatch produce the same selection. Confirms Tag is preserved on
        // round trip through the deconstructor + constructor delegates.
        public sealed class Tree
        {
            public int Height { get; set; }
        }

        public sealed class Rock
        {
            public string? Kind { get; set; }
        }

#pragma warning disable STYML1227
        [Union]
        public readonly struct CustomDiscriminatedFlora : IUnion
        {
            public CustomDiscriminatedFlora(Tree tree) { Tag = 0; Value = tree; }
            public CustomDiscriminatedFlora(Rock rock) { Tag = 1; Value = rock; }

            public int Tag { get; }
            public object? Value { get; }

            public bool TryGetValue(out Tree? tree)
            {
                if (Tag == 0 && Value is Tree t)
                {
                    tree = t;
                    return true;
                }

                tree = null;
                return false;
            }

            public bool TryGetValue(out Rock? rock)
            {
                if (Tag == 1 && Value is Rock r)
                {
                    rock = r;
                    return true;
                }

                rock = null;
                return false;
            }
        }
#pragma warning restore STYML1227

        [Theory]
        [InlineData(0)] // Tree case, Tag = 0
        [InlineData(1)] // Rock case, Tag = 1
        public void CustomDiscriminatedFlora_DirectDelegate_DisjointReferenceTypesPreserveTag(int tag)
        {
            YamlTypeInfo<CustomDiscriminatedFlora> typeInfo = Serializer.GetTypeInfo<CustomDiscriminatedFlora>();

            Tree tree = new() { Height = 7 };
            Rock rock = new() { Kind = "Granite" };

            CustomDiscriminatedFlora original = tag == 0 ? new(tree) : new(rock);

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(original);
            Assert.Equal(tag == 0 ? typeof(Tree) : typeof(Rock), caseType);
            Assert.Same(tag == 0 ? (object)tree : rock, caseValue);

            CustomDiscriminatedFlora rebuilt = typeInfo.UnionConstructor!(caseType!, caseValue);
            Assert.Equal(tag, rebuilt.Tag);
            Assert.Same(original.Value, rebuilt.Value);
        }

        public enum Color { Red, Green, Blue }

        public union NullableEnumUnion(Color?, string);

        [Fact]
        public void NullableEnumUnionCase_SchemaIncludesNullInTypeArray()
        {
            YamlTypeInfo<NullableEnumUnion> typeInfo = Serializer.GetTypeInfo<NullableEnumUnion>();
            YamlNode schema = typeInfo.GetYamlSchemaAsNode();

            YamlArray anyOf = Assert.IsType<YamlArray>(schema["anyOf"]);
            Assert.Equal(2, anyOf.Count);

            YamlNode? nullableEnumCase = anyOf.FirstOrDefault(node =>
                node?["type"] is YamlArray types &&
                types.Any(t => (string?)t == "integer"));
            Assert.NotNull(nullableEnumCase);

            YamlArray typeArray = Assert.IsType<YamlArray>(nullableEnumCase["type"]);
            Assert.Contains("integer", typeArray.Select(node => (string?)node));
            Assert.Contains("null", typeArray.Select(node => (string?)node));
        }

        [Fact]
        public void NullableEnumUnion_Deconstructor_DispatchesAllBranches()
        {
            // Non-null Color? payload reports typeof(Color?), not typeof(Color) — the union
            // only declares the Nullable<Color> case, so most-derived dispatch picks Color?.
            // Null Color? payload also reports typeof(Color?). The string payload routes to
            // its own arm and reports typeof(string).
            YamlTypeInfo<NullableEnumUnion> typeInfo = Serializer.GetTypeInfo<NullableEnumUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            (Type? colorT, object? colorV) = typeInfo.UnionDeconstructor!(new NullableEnumUnion(Color.Red));
            Assert.Equal(typeof(Color?), colorT);
            Assert.Equal(Color.Red, colorV);

            (Type? nullT, object? nullV) = typeInfo.UnionDeconstructor!(new NullableEnumUnion((Color?)null));
            Assert.Equal(typeof(Color?), nullT);
            Assert.Null(nullV);

            (Type? stringT, object? stringV) = typeInfo.UnionDeconstructor!(new NullableEnumUnion("hello"));
            Assert.Equal(typeof(string), stringT);
            Assert.Equal("hello", stringV);
        }

        [Fact]
        public void NullableEnumUnion_Constructor_DispatchesAllBranches()
        {
            YamlTypeInfo<NullableEnumUnion> typeInfo = Serializer.GetTypeInfo<NullableEnumUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            NullableEnumUnion colorUnion = typeInfo.UnionConstructor!(typeof(Color?), Color.Blue);
            Assert.Equal(Color.Blue, GetUnionValue(colorUnion));

            NullableEnumUnion nullUnion = typeInfo.UnionConstructor!(typeof(Color?), null);
            Assert.Null(GetUnionValue(nullUnion));

            NullableEnumUnion stringUnion = typeInfo.UnionConstructor!(typeof(string), "hello");
            Assert.Equal("hello", GetUnionValue(stringUnion));
        }
        #region Recursive unions

        public union RecursiveNat(bool, RecursiveNat);

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_TripleNested_SerializesAsLeafValue()
        {
            var tripleNested = new RecursiveNat(new RecursiveNat(new RecursiveNat(true)));

            string yaml = await Serializer.SerializeWrapper(tripleNested);
            Assert.Equal("true", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_DoubleNested_SerializesAsLeafValue()
        {
            var doubleNested = new RecursiveNat(new RecursiveNat(false));

            string yaml = await Serializer.SerializeWrapper(doubleNested);
            Assert.Equal("false", yaml);
        }

        [Fact]
        public async Task RecursiveUnion_Deserialize_YieldsSingleLevelWrapper()
        {
            RecursiveNat? result = await Serializer.DeserializeWrapper<RecursiveNat>("true");

            Assert.NotNull(result);
            Assert.Equal(true, GetUnionValue(result!));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_RoundTrip_FlattensNesting()
        {
            var tripleNested = new RecursiveNat(new RecursiveNat(new RecursiveNat(true)));

            string yaml = await Serializer.SerializeWrapper(tripleNested);
            Assert.Equal("true", yaml);

            RecursiveNat? deserialized = await Serializer.DeserializeWrapper<RecursiveNat>(yaml);
            Assert.NotNull(deserialized);
            Assert.IsType<bool>(GetUnionValue(deserialized!));
            Assert.Equal(true, GetUnionValue(deserialized!));
        }

        // Declares the recursive case before the scalar case: the recursive arm must not
        // shadow the arms that follow it.
        public union RecursiveNatReversed(RecursiveNatReversed, bool);

        [Theory]
        [InlineData(0, true, "true")]
        [InlineData(1, false, "false")]
        [InlineData(2, true, "true")]
        [InlineData(5, false, "false")]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_RecursiveCaseDeclaredFirst_RoundTrips(int nestingDepth, bool leaf, string expectedYaml)
        {
            RecursiveNatReversed nested = new RecursiveNatReversed(leaf);
            for (int i = 0; i < nestingDepth; i++)
            {
                nested = new RecursiveNatReversed(nested);
            }

            string yaml = await Serializer.SerializeWrapper(nested);
            Assert.Equal(expectedYaml, yaml);

            RecursiveNatReversed? deserialized = await Serializer.DeserializeWrapper<RecursiveNatReversed>(yaml);
            Assert.NotNull(deserialized);
            Assert.IsType<bool>(GetUnionValue(deserialized!));
            Assert.Equal(leaf, GetUnionValue(deserialized!));
        }

        // The self-referential case is nullable, which makes it the union's null case as well:
        // the generated null arm reports typeof(NullableNat?) while the non-null arm matches
        // the unwrapped NullableNat payload.
        public union NullableNat(bool, NullableNat?);

        [Theory]
        [InlineData(0, true, "true")]
        [InlineData(1, false, "false")]
        [InlineData(3, true, "true")]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_NullableSelfCase_RoundTrip_FlattensNesting(int nestingDepth, bool leaf, string expectedYaml)
        {
            NullableNat nested = new NullableNat(leaf);
            for (int i = 0; i < nestingDepth; i++)
            {
                nested = new NullableNat(nested);
            }

            string yaml = await Serializer.SerializeWrapper(nested);
            Assert.Equal(expectedYaml, yaml);

            NullableNat? deserialized = await Serializer.DeserializeWrapper<NullableNat>(yaml);
            Assert.NotNull(deserialized);
            Assert.IsType<bool>(GetUnionValue(deserialized!));
            Assert.Equal(leaf, GetUnionValue(deserialized!));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RecursiveUnion_NullableSelfCase_NullPayload_RoundTripsAsYamlNull()
        {
            var nullNat = new NullableNat((NullableNat?)null);

            string yaml = await Serializer.SerializeWrapper(nullNat);
            Assert.Equal("null", yaml);

            NullableNat? deserialized = await Serializer.DeserializeWrapper<NullableNat>(yaml);
            Assert.NotNull(deserialized);
            Assert.Null(GetUnionValue(deserialized!));
        }

        #endregion

        #region Class unions with subtype cases

        // A [Union] class is not sealed, so a case type can derive from the union itself.
        // Root-level union type patterns still target the payload, even when an ordinary type
        // pattern could match the union instance through that inheritance.
        //
        // Value is [YamlIgnore]d because CircleShape inherits it, and would otherwise carry it
        // into its own JSON object. That has no bearing on union recognition: ShapeUnion is
        // serialized as a union, not as an object.
        [Union]
        public class ShapeUnion : IUnion
        {
            public ShapeUnion(int i) { Value = i; }
            public ShapeUnion(CircleShape c) { Value = c; }
            protected ShapeUnion() { Value = null; }

            [YamlIgnore]
            public object? Value { get; set; }
        }

        public sealed class CircleShape : ShapeUnion
        {
            public double Radius { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassUnion_SubclassCase_SerializesPayload()
        {
            var union = new ShapeUnion(new CircleShape { Radius = 2.5 });

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("""{"Radius":2.5}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassUnion_SubclassCase_ScalarPayload_SerializesPayload()
        {
            var union = new ShapeUnion(42);

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("42", yaml);
        }

        [Fact]
        public async Task ClassUnion_SubclassCase_Deserialize_YieldsSubclassPayload()
        {
            ShapeUnion? deserialized = await Serializer.DeserializeWrapper<ShapeUnion>("""{"Radius":2.5}""");

            Assert.NotNull(deserialized);
            CircleShape circle = Assert.IsType<CircleShape>(GetUnionValue(deserialized!));
            Assert.Equal(2.5, circle.Radius);
        }

        [Fact]
        public void ClassUnion_SubclassCase_UnionConstructor_RoundTripsBothCases()
        {
            YamlTypeInfo<ShapeUnion> typeInfo = Serializer.GetTypeInfo<ShapeUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            var circle = new CircleShape { Radius = 1.5 };
            ShapeUnion circleUnion = typeInfo.UnionConstructor!(typeof(CircleShape), circle);
            Assert.Same(circle, GetUnionValue(circleUnion));

            ShapeUnion intUnion = typeInfo.UnionConstructor!(typeof(int), 7);
            Assert.Equal(7, GetUnionValue(intUnion));
        }

        // A subclass instance is a legal ShapeUnion, so the deconstructor receives a value whose
        // runtime type matches one of its own case types. It must still report the wrapped value
        // rather than binding the instance it was handed.
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassUnion_SubclassInstanceAsUnion_SerializesWrappedValue()
        {
            ShapeUnion union = new CircleShape { Radius = 3.5, Value = 11 };

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("11", yaml);
        }

        #endregion

        #region Class unions overlapping their own case types

        // ShapeUnion above covers a case type deriving from the union. This region covers the
        // opposite subtyping direction: the union derives from, or implements, one of its own
        // case types.
        //
        // In both directions, an ordinary type pattern could match the union instance. Union type
        // patterns instead target the payload; these tests pin the payload as the observable result.

        [YamlConverter(typeof(YamlWritableConverter))]
        public interface IYamlWritable
        {
            void WriteTo(Utf8YamlWriter writer);
        }

        public sealed class YamlWritableConverter : YamlConverter<IYamlWritable>
        {
            public override IYamlWritable? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                new WritablePoco { Label = reader.GetString() };

            public override void Write(Utf8YamlWriter writer, IYamlWritable value, YamlSerializerOptions options) =>
                value.WriteTo(writer);
        }

        public sealed class WritablePoco : IYamlWritable
        {
            public string? Label { get; set; }

            public void WriteTo(Utf8YamlWriter writer) => writer.WriteStringValue(Label);
        }

        // The union implements the same interface it declares as a case. Because the converter is
        // attached to the interface, a root-level pattern that bound the union instance instead of
        // the payload would still serialize successfully — through the union's own WriteTo — and
        // produce "union" instead of the payload's label. The union itself is unaffected by the
        // converter and continues to use built-in union serialization.
#pragma warning disable STYML1227
        public union WritableUnion(int, IYamlWritable) : IYamlWritable
        {
            public void WriteTo(Utf8YamlWriter writer) => writer.WriteStringValue("union");
        }
#pragma warning restore STYML1227

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Union_ImplementingOwnCaseInterface_SerializesPayloadNotUnionInstance()
        {
            WritableUnion union = new WritablePoco { Label = "payload" };

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("\"payload\"", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Union_ImplementingOwnCaseInterface_ScalarCase_SerializesPayload()
        {
            WritableUnion union = 42;

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("42", yaml);
        }

        [Fact]
        public void Union_ImplementingOwnCaseInterface_Deconstructor_ReportsPayload()
        {
            YamlTypeInfo<WritableUnion> typeInfo = Serializer.GetTypeInfo<WritableUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            var poco = new WritablePoco { Label = "payload" };
            WritableUnion union = poco;

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);
            Assert.Equal(typeof(IYamlWritable), caseType);
            Assert.Same(poco, caseValue);

            (Type? intType, object? intValue) = typeInfo.UnionDeconstructor!(7);
            Assert.Equal(typeof(int), intType);
            Assert.Equal(7, intValue);
        }

        [Fact]
        public void Union_ImplementingOwnCaseInterface_UnionConstructor_RoundTripsBothCases()
        {
            YamlTypeInfo<WritableUnion> typeInfo = Serializer.GetTypeInfo<WritableUnion>();
            Assert.NotNull(typeInfo.UnionConstructor);

            var poco = new WritablePoco { Label = "payload" };
            WritableUnion pocoUnion = typeInfo.UnionConstructor!(typeof(IYamlWritable), poco);
            Assert.Same(poco, GetUnionValue(pocoUnion));

            WritableUnion intUnion = typeInfo.UnionConstructor!(typeof(int), 9);
            Assert.Equal(9, GetUnionValue(intUnion));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Union_ImplementingOwnCaseInterface_WithClassifier_RoundTrips()
        {
            // The custom converter case overlaps the numeric case, so supply a classifier.
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(WritableUnion))
                {
                    typeInfo.TypeClassifier = static (ref Utf8YamlReader reader) =>
                        reader.TokenType is YamlTokenType.String ? typeof(IYamlWritable) : typeof(int);
                }
            });

            WritableUnion union = new WritablePoco { Label = "payload" };
            string yaml = await Serializer.SerializeWrapper(union, options);
            Assert.Equal("\"payload\"", yaml);

            WritableUnion? deserialized = await Serializer.DeserializeWrapper<WritableUnion>(yaml, options);
            Assert.NotNull(deserialized);
            WritablePoco poco = Assert.IsType<WritablePoco>(GetUnionValue(deserialized!));
            Assert.Equal("payload", poco.Label);
        }

        // The [Union] class equivalent: the union derives from one of its own case types, so an
        // ordinary type pattern could match the union instance through an implicit conversion.
        public class NodeCase
        {
            public string? Label { get; set; }
        }

        [Union]
        public class NodeUnion : NodeCase, IUnion
        {
            public NodeUnion(int i) { Value = i; }
            public NodeUnion(NodeCase n) { Value = n; }
            protected NodeUnion() { Value = null; }

            [YamlIgnore]
            public object? Value { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassUnion_DerivingFromOwnCase_SerializesPayloadNotUnionInstance()
        {
            var union = new NodeUnion(new NodeCase { Label = "payload" }) { Label = "union" };

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("""{"Label":"payload"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ClassUnion_DerivingFromOwnCase_ScalarCase_SerializesPayload()
        {
            var union = new NodeUnion(42) { Label = "union" };

            string yaml = await Serializer.SerializeWrapper(union);
            Assert.Equal("42", yaml);
        }

        [Fact]
        public async Task ClassUnion_DerivingFromOwnCase_Deserialize_YieldsPayload()
        {
            NodeUnion? deserialized = await Serializer.DeserializeWrapper<NodeUnion>("""{"Label":"payload"}""");

            Assert.NotNull(deserialized);
            NodeCase node = Assert.IsType<NodeCase>(GetUnionValue(deserialized!));
            Assert.Equal("payload", node.Label);
        }

        [Fact]
        public void ClassUnion_DerivingFromOwnCase_Deconstructor_ReportsPayload()
        {
            YamlTypeInfo<NodeUnion> typeInfo = Serializer.GetTypeInfo<NodeUnion>();
            Assert.NotNull(typeInfo.UnionDeconstructor);

            var payload = new NodeCase { Label = "payload" };
            var union = new NodeUnion(payload) { Label = "union" };

            (Type? caseType, object? caseValue) = typeInfo.UnionDeconstructor!(union);
            Assert.Equal(typeof(NodeCase), caseType);
            Assert.Same(payload, caseValue);
        }

        #endregion

        #region Cyclic object graph

        [Union]
        public class SelfReferentialUnion : IUnion
        {
            public SelfReferentialUnion(string s) { Value = s; }
            public SelfReferentialUnion(SelfReferentialUnion inner) { Value = inner; }
            public object? Value { get; set; }
        }

        [Fact]
        public void CyclicUnion_Serialize_ThrowsYamlExceptionAtMaxDepth()
        {
            var union = new SelfReferentialUnion("seed");
            union.Value = union; // cyclic reference

            var options = new YamlSerializerOptions(Serializer.DefaultOptions)
            {
                MaxDepth = 2,
            };

            Assert.Throws<YamlException>(() =>
                YamlSerializer.Serialize(union, options.GetTypeInfo<SelfReferentialUnion>()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SelfReferentialUnion_NonCyclic_RoundTrips()
        {
            var inner = new SelfReferentialUnion("hello");
            var outer = new SelfReferentialUnion(inner);

            string yaml = await Serializer.SerializeWrapper(outer);
            Assert.Equal("\"hello\"", yaml);

            SelfReferentialUnion? deserialized = await Serializer.DeserializeWrapper<SelfReferentialUnion>(yaml);
            Assert.NotNull(deserialized);
            Assert.Equal("hello", deserialized!.Value);
        }

        #endregion
    }
}
