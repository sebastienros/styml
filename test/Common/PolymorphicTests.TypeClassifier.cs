// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Reflection helper for constructing an instance of <see cref="YamlTypeClassifierContext"/>
    /// in tests. The runtime constructor is internal because the context is normally produced by
    /// STJ's plumbing; tests reach through reflection to exercise classifier factories directly.
    /// </summary>
    internal static class YamlTypeClassifierContextTestExtensions
    {
        private static readonly ConstructorInfo s_ctor = typeof(YamlTypeClassifierContext)
            .GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(YamlTypeClassifierKind), typeof(YamlTypeInfo), typeof(IReadOnlyList<YamlUnionCaseInfo>), typeof(IReadOnlyList<YamlDerivedType>), typeof(string)],
                modifiers: null)!;

        public static YamlTypeClassifierContext Create(
            YamlTypeInfo declaringTypeInfo,
            IReadOnlyList<YamlUnionCaseInfo> unionCases,
            IReadOnlyList<YamlDerivedType> derivedTypes,
            string? typeDiscriminatorPropertyName,
            YamlTypeClassifierKind kind = YamlTypeClassifierKind.PolymorphicType)
            => (YamlTypeClassifierContext)s_ctor.Invoke([kind, declaringTypeInfo, unionCases, derivedTypes, typeDiscriminatorPropertyName]);
    }

    /// <summary>
    /// Verifies polymorphic type resolution via <see cref="YamlTypeClassifier"/> and the
    /// generation of <see cref="YamlPolymorphismOptions"/>. The same contract applies to both
    /// the reflection and source-generated paths, so the suite is exercised by both wrappers.
    /// </summary>
    public abstract partial class PolymorphicTests
    {
        #region Test Models

        [YamlPolymorphic]
        [YamlDerivedType(typeof(ClassifiedDog), "dog")]
        [YamlDerivedType(typeof(ClassifiedCat), "cat")]
        [YamlDerivedType(typeof(ClassifiedParrot), "parrot")]
        public class ClassifiedAnimalBase
        {
            public string? Name { get; set; }
        }

        public class ClassifiedDog : ClassifiedAnimalBase
        {
            public string? Breed { get; set; }
        }

        public class ClassifiedCat : ClassifiedAnimalBase
        {
            public int Lives { get; set; }
        }

        public class ClassifiedParrot : ClassifiedAnimalBase
        {
            public bool CanTalk { get; set; }
        }

        [YamlPolymorphic]
        [YamlDerivedType(typeof(ClassifiedCircle), "circle")]
        [YamlDerivedType(typeof(ClassifiedRectangle), "rect")]
        public abstract class ClassifiedShape
        {
            public string? Color { get; set; }
        }

        public class ClassifiedCircle : ClassifiedShape
        {
            public double Radius { get; set; }
        }

        public class ClassifiedRectangle : ClassifiedShape
        {
            public double Width { get; set; }
            public double Height { get; set; }
        }

        [YamlPolymorphic]
        [YamlDerivedType(typeof(ClassifiedDogL2), "dog")]
        [YamlDerivedType(typeof(ClassifiedLabrador), "lab")]
        [YamlDerivedType(typeof(ClassifiedPoodle), "poodle")]
        public class ClassifiedAnimalDeepHierarchy
        {
            public string? Name { get; set; }
        }

        public class ClassifiedDogL2 : ClassifiedAnimalDeepHierarchy
        {
            public string? Breed { get; set; }
        }

        public class ClassifiedLabrador : ClassifiedDogL2
        {
            public bool IsGuide { get; set; }
        }

        public class ClassifiedPoodle : ClassifiedDogL2
        {
            public string? Size { get; set; }
        }

        [YamlPolymorphic(TypeClassifier = typeof(AnimalStructuralClassifierFactory))]
        [YamlDerivedType(typeof(AttrClassifiedDog), "dog")]
        [YamlDerivedType(typeof(AttrClassifiedCat), "cat")]
        public class AttrClassifiedAnimal
        {
            public string? Name { get; set; }
        }

        public class AttrClassifiedDog : AttrClassifiedAnimal
        {
            public string? Breed { get; set; }
        }

        public class AttrClassifiedCat : AttrClassifiedAnimal
        {
            public int Lives { get; set; }
        }

        [YamlPolymorphic(TypeClassifier = typeof(UnionOnlyPolymorphicClassifierFactory))]
        [YamlDerivedType(typeof(UnsupportedAttrClassifiedDog), "dog")]
        public class UnsupportedAttrClassifiedAnimal
        {
            public string? Name { get; set; }
        }

        public class UnsupportedAttrClassifiedDog : UnsupportedAttrClassifiedAnimal
        {
            public string? Breed { get; set; }
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "$metadata")]
        [YamlDerivedType(typeof(MetadataServicesAttributedDerived), "attr-derived")]
        public class MetadataServicesAttributedBase
        {
            public string? Value { get; set; }
        }

        public class MetadataServicesAttributedDerived : MetadataServicesAttributedBase
        {
            public string? DerivedValue { get; set; }
        }

        public class MetadataServicesExplicitDerived : MetadataServicesAttributedBase
        {
            public string? ExplicitValue { get; set; }
        }

        public class MetadataServicesAttributedBaseConverter : YamlConverter<MetadataServicesAttributedBase>
        {
            public override MetadataServicesAttributedBase? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                throw new NotSupportedException();

            public override void Write(Utf8YamlWriter writer, MetadataServicesAttributedBase value, YamlSerializerOptions options) =>
                throw new NotSupportedException();
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "$metadata")]
        [YamlDerivedType(typeof(MetadataServicesAttributedListDerived), "attr-list")]
        public class MetadataServicesAttributedList : List<int>
        {
        }

        public class MetadataServicesAttributedListDerived : MetadataServicesAttributedList
        {
        }

        public class AnimalStructuralClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context) => context.Kind is YamlTypeClassifierKind.PolymorphicType;

            public override YamlTypeClassifier CreateYamlClassifier(
                YamlTypeClassifierContext context,
                YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.PolymorphicType, context.Kind);

                return (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                        return null;

                    while (reader.Read() && reader.TokenType != YamlTokenType.EndObject)
                    {
                        if (reader.TokenType == YamlTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Breed"u8)) return typeof(AttrClassifiedDog);
                            if (reader.ValueTextEquals("Lives"u8)) return typeof(AttrClassifiedCat);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        public class TestStructuralClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context) => context.Kind is YamlTypeClassifierKind.PolymorphicType;

            public override YamlTypeClassifier CreateYamlClassifier(
                YamlTypeClassifierContext context,
                YamlSerializerOptions options)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                Assert.Equal(YamlTypeClassifierKind.PolymorphicType, context.Kind);
                Assert.Empty(context.UnionCases);
                Assert.NotEmpty(context.DerivedTypes);

                return context.DeclaringType switch
                {
                    _ when context.DeclaringType == typeof(ClassifiedAnimalBase) => CreateAnimalClassifier(),
                    _ when context.DeclaringType == typeof(ClassifiedShape) => CreateShapeClassifier(),
                    _ when context.DeclaringType == typeof(ClassifiedAnimalDeepHierarchy) => CreateDeepHierarchyClassifier(),
                    _ => (ref Utf8YamlReader _) => null,
                };
            }

            private static YamlTypeClassifier CreateAnimalClassifier() =>
                static (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType is not YamlTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not YamlTokenType.EndObject)
                    {
                        if (reader.TokenType is YamlTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Breed"u8)) return typeof(ClassifiedDog);
                            if (reader.ValueTextEquals("Lives"u8)) return typeof(ClassifiedCat);
                            if (reader.ValueTextEquals("CanTalk"u8)) return typeof(ClassifiedParrot);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedDog);
                };

            private static YamlTypeClassifier CreateShapeClassifier() =>
                static (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType is not YamlTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not YamlTokenType.EndObject)
                    {
                        if (reader.TokenType is YamlTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Radius"u8)) return typeof(ClassifiedCircle);
                            if (reader.ValueTextEquals("Width"u8) || reader.ValueTextEquals("Height"u8)) return typeof(ClassifiedRectangle);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedCircle);
                };

            private static YamlTypeClassifier CreateDeepHierarchyClassifier() =>
                static (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType is not YamlTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not YamlTokenType.EndObject)
                    {
                        if (reader.TokenType is YamlTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("IsGuide"u8)) return typeof(ClassifiedLabrador);
                            if (reader.ValueTextEquals("Size"u8)) return typeof(ClassifiedPoodle);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedDogL2);
                };
        }

        public class AnimalKindClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context) => context.Kind is YamlTypeClassifierKind.PolymorphicType;

            public override YamlTypeClassifier CreateYamlClassifier(
                YamlTypeClassifierContext context,
                YamlSerializerOptions options)
            {
                var innerFactory = new TestDiscriminatorClassifierFactory();
                var innerContext = YamlTypeClassifierContextTestExtensions.Create(
                                    options.GetTypeInfo(context.DeclaringType),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                        new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                        new YamlDerivedType(typeof(ClassifiedCat), "cat"),
                        new YamlDerivedType(typeof(ClassifiedParrot), "parrot"),},
                                    "kind");

                return innerFactory.CreateYamlClassifier(innerContext, options);
            }
        }

        public class TestDiscriminatorClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context) => context.Kind is YamlTypeClassifierKind.PolymorphicType;

            public override YamlTypeClassifier CreateYamlClassifier(
                YamlTypeClassifierContext context,
                YamlSerializerOptions options)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                if (options is null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                Assert.Equal(YamlTypeClassifierKind.PolymorphicType, context.Kind);

                string propertyName = context.TypeDiscriminatorPropertyName ?? "$type";
                byte[] propertyNameUtf8 = System.Text.Encoding.UTF8.GetBytes(propertyName);

                Dictionary<string, Type>? stringMap = null;
                Dictionary<int, Type>? intMap = null;

                foreach (YamlDerivedType derivedType in context.DerivedTypes)
                {
                    if (derivedType.TypeDiscriminator is string s)
                    {
                        stringMap ??= new Dictionary<string, Type>(StringComparer.Ordinal);
                        stringMap[s] = derivedType.DerivedType;
                    }
                    else if (derivedType.TypeDiscriminator is int i)
                    {
                        intMap ??= new Dictionary<int, Type>();
                        intMap[i] = derivedType.DerivedType;
                    }
                }

                return (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType is not YamlTokenType.StartObject)
                    {
                        return null;
                    }

                    Utf8YamlReader copy = reader;

                    while (copy.Read())
                    {
                        if (copy.TokenType is YamlTokenType.EndObject)
                        {
                            break;
                        }

                        if (copy.TokenType is YamlTokenType.PropertyName &&
                            copy.ValueTextEquals(propertyNameUtf8))
                        {
                            if (!copy.Read())
                            {
                                break;
                            }

                            if (stringMap is not null && copy.TokenType is YamlTokenType.String)
                            {
                                string? value = copy.GetString();
                                if (value is not null && stringMap.TryGetValue(value, out Type? result))
                                {
                                    return result;
                                }
                            }
                            else if (intMap is not null && copy.TokenType is YamlTokenType.Number)
                            {
                                if (copy.TryGetInt32(out int value) && intMap.TryGetValue(value, out Type? result))
                                {
                                    return result;
                                }
                            }

                            return null;
                        }

                        if (copy.TokenType is YamlTokenType.PropertyName)
                        {
                            copy.Read();
                            copy.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        public class UnionOnlyPolymorphicClassifierFactory : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context)
            {
                Assert.Equal(YamlTypeClassifierKind.PolymorphicType, context.Kind);
                return false;
            }

            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                throw new InvalidOperationException("This factory should not be selected.");
        }

        public class DrawingCanvas
        {
            public List<ClassifiedShape>? Shapes { get; set; }
        }

        public class PetOwner
        {
            public string? OwnerName { get; set; }
            public ClassifiedAnimalBase? Pet { get; set; }
        }

        // Hierarchy with a custom type-discriminator configuration, used to validate that
        // PolymorphismOptions are surfaced identically under reflection and source generation.
        [YamlPolymorphic(
            TypeDiscriminatorPropertyName = "$kind",
            IgnoreUnrecognizedTypeDiscriminators = true,
            UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBaseType)]
        [YamlDerivedType(typeof(StringDiscriminatorDerived), "string-derived")]
        [YamlDerivedType(typeof(IntDiscriminatorDerived), 42)]
        public class PolymorphicBaseWithCustomDiscriminator
        {
            public string? Value { get; set; }
        }

        public sealed class StringDiscriminatorDerived : PolymorphicBaseWithCustomDiscriminator
        {
            public string? StringValue { get; set; }
        }

        public sealed class IntDiscriminatorDerived : PolymorphicBaseWithCustomDiscriminator
        {
            public int IntValue { get; set; }
        }

        // Hierarchy whose classifier is supplied through a strongly typed
        // YamlTypeClassifierFactory<T> referenced from the [YamlPolymorphic] attribute.
        [YamlPolymorphic(TypeClassifier = typeof(FactoryClassifiedAnimalClassifierFactory))]
        [YamlDerivedType(typeof(FactoryClassifiedDog), "dog")]
        [YamlDerivedType(typeof(FactoryClassifiedCat), "cat")]
        public class FactoryClassifiedAnimal
        {
            public string? Name { get; set; }
        }

        public sealed class FactoryClassifiedDog : FactoryClassifiedAnimal
        {
            public string? Breed { get; set; }
        }

        public sealed class FactoryClassifiedCat : FactoryClassifiedAnimal
        {
            public int Lives { get; set; }
        }

        public sealed class FactoryClassifiedAnimalClassifierFactory : YamlTypeClassifierFactory<FactoryClassifiedAnimal>
        {
            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTypeClassifierKind.PolymorphicType, context.Kind);
                Assert.Equal(typeof(FactoryClassifiedAnimal), context.DeclaringType);
                Assert.Equal("$type", context.TypeDiscriminatorPropertyName);
                Assert.Collection(
                    context.DerivedTypes,
                    derivedType =>
                    {
                        Assert.Equal(typeof(FactoryClassifiedDog), derivedType.DerivedType);
                        Assert.Equal("dog", derivedType.TypeDiscriminator);
                    },
                    derivedType =>
                    {
                        Assert.Equal(typeof(FactoryClassifiedCat), derivedType.DerivedType);
                        Assert.Equal("cat", derivedType.TypeDiscriminator);
                    });

                return static (ref Utf8YamlReader reader) =>
                {
                    if (reader.TokenType != YamlTokenType.StartObject)
                        return null;

                    while (reader.Read() && reader.TokenType != YamlTokenType.EndObject)
                    {
                        if (reader.TokenType == YamlTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Breed"u8)) return typeof(FactoryClassifiedDog);
                            if (reader.ValueTextEquals("Lives"u8)) return typeof(FactoryClassifiedCat);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        // Collection type carrying polymorphism options.
        [YamlPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
        [YamlDerivedType(typeof(PolymorphicIntListDerived), "derived-list")]
        public class PolymorphicIntList : List<int>
        {
        }

        public sealed class PolymorphicIntListDerived : PolymorphicIntList
        {
        }

        // Open generic hierarchy where the derived type only partially unifies with the base.
        [YamlDerivedType(typeof(OpenGenericDerived<>), "derived")]
        public class OpenGenericBase<T1, T2>
        {
            public T1? Value1 { get; set; }
            public T2? Value2 { get; set; }
        }

        public sealed class OpenGenericDerived<T> : OpenGenericBase<T, int>
        {
            public T? Extra { get; set; }
        }

        #endregion

        #region Classifier via contract customization — structural matching

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesDogByBreedProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"Name":"Rex","Breed":"Labrador"}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);

            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
            Assert.Equal("Labrador", ((ClassifiedDog)result).Breed);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesCatByLivesProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"Name":"Whiskers","Lives":9}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);

            Assert.IsType<ClassifiedCat>(result);
            Assert.Equal(9, ((ClassifiedCat)result).Lives);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesParrotByCanTalkProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"Name":"Polly","CanTalk":true}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);

            Assert.IsType<ClassifiedParrot>(result);
            Assert.True(((ClassifiedParrot)result).CanTalk);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_BestMatchWins_EvenWithUnknownProperties()
        {
            // When all derived types score equally (e.g., all share only "Name" from base),
            // the structural classifier returns the first declared derived type by declaration order.
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"Name":"Unknown","UnknownProp":42}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal("Unknown", result.Name);
            Assert.IsType<ClassifiedDog>(result);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_AbstractBaseType()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string yaml = """{"Color":"red","Radius":5.0}""";

            ClassifiedShape? result = await Serializer.DeserializeWrapper<ClassifiedShape>(yaml, options);

            Assert.IsType<ClassifiedCircle>(result);
            Assert.Equal("red", result.Color);
            Assert.Equal(5.0, ((ClassifiedCircle)result).Radius);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_AbstractBaseType_Rectangle()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string yaml = """{"Color":"blue","Width":10,"Height":20}""";

            ClassifiedShape? result = await Serializer.DeserializeWrapper<ClassifiedShape>(yaml, options);

            Assert.IsType<ClassifiedRectangle>(result);
            Assert.Equal(10, ((ClassifiedRectangle)result).Width);
            Assert.Equal(20, ((ClassifiedRectangle)result).Height);
        }

        #endregion

        #region Classifier via contract customization — discriminator-based

        [Theory]
        [InlineData("""{"kind":"dog","Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDog))]
        [InlineData("""{"kind":"cat","Name":"Whiskers","Lives":9}""", typeof(ClassifiedCat))]
        [InlineData("""{"kind":"parrot","Name":"Polly","CanTalk":true}""", typeof(ClassifiedParrot))]
        public async Task Classifier_DiscriminatorBased_ResolvesCorrectType(string yaml, Type expectedType)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),
                    new YamlDerivedType(typeof(ClassifiedParrot), "parrot"),},
                                    "kind");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType(expectedType, result);
        }

        [Theory]
        [InlineData("""{"Name":"Rex","Breed":"Lab","kind":"dog"}""")]
        [InlineData("""{"Name":"Rex","kind":"dog","Breed":"Lab"}""")]
        public async Task Classifier_DiscriminatorBased_AnyPropertyPosition(string yaml)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        [Theory]
        [InlineData("""{"type_id":1,"Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDog))]
        [InlineData("""{"Name":"Whiskers","type_id":2,"Lives":9}""", typeof(ClassifiedCat))]
        public async Task Classifier_IntDiscriminator_ResolvesCorrectType(string yaml, Type expectedType)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), 1),
                    new YamlDerivedType(typeof(ClassifiedCat), 2),},
                                    "type_id");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType(expectedType, result);
        }

        #endregion

        #region Classifier via YamlSerializerOptions.TypeClassifiers

        [Fact]
        public async Task OptionsTypeClassifier_UsesPolymorphicContextToSelectFactory()
        {
            YamlSerializerOptions options = Serializer.CreateOptions(
                configure: o => o.TypeClassifiers.Add(new TestStructuralClassifierFactory()));

            string yaml = """{"Name":"Rex","Breed":"Labrador"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);

            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Labrador", ((ClassifiedDog)result).Breed);
        }

        [Fact]
        public void OptionsTypeClassifier_IsVisibleToPolymorphicModifier()
        {
            YamlTypeClassifier? classifierFromModifier = null;
            YamlSerializerOptions options = Serializer.CreateOptions(
                configure: o => o.TypeClassifiers.Add(new TestStructuralClassifierFactory()),
                modifier: typeInfo =>
                {
                    if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                    {
                        classifierFromModifier = typeInfo.TypeClassifier;
                    }
                });

            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(ClassifiedAnimalBase));

            Assert.NotNull(classifierFromModifier);
            Assert.Same(classifierFromModifier, typeInfo.TypeClassifier);
        }

        #endregion

        #region Classifier via YamlPolymorphicAttribute.TypeClassifier

        [Fact]
        public async Task Classifier_ViaAttribute_DeserializesDogByStructure()
        {
            string yaml = """{"Name":"Rex","Breed":"Labrador"}""";

            AttrClassifiedAnimal? result = await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(yaml);

            Assert.IsType<AttrClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
            Assert.Equal("Labrador", ((AttrClassifiedDog)result).Breed);
        }

        [Fact]
        public async Task Classifier_ViaAttribute_DeserializesCatByStructure()
        {
            string yaml = """{"Name":"Whiskers","Lives":7}""";

            AttrClassifiedAnimal? result = await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(yaml);

            Assert.IsType<AttrClassifiedCat>(result);
            Assert.Equal(7, ((AttrClassifiedCat)result).Lives);
        }

        [Fact]
        public async Task Classifier_ViaAttribute_ThrowsWhenNoMatch()
        {
            string yaml = """{"Name":"Unknown"}""";

            await Assert.ThrowsAsync<YamlException>(async () =>
                await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(yaml));
        }

        [Fact]
        public async Task Classifier_ViaAttribute_NoFallbackToDiscriminator_WhenClassifierReturnsNull()
        {
            // JSON with $type but no structural distinguishing properties.
            // The classifier returns null — this is now a classification failure,
            // not a fallback to $type discriminator.
            string yaml = """{"$type":"dog","Name":"Rex"}""";

            await Assert.ThrowsAsync<YamlException>(async () =>
                await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(yaml));
        }

        [Fact]
        public void Classifier_ViaAttribute_RejectsFactoryWhenCanClassifyReturnsFalse()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Serializer.GetTypeInfo<UnsupportedAttrClassifiedAnimal>());

            Assert.Contains(nameof(UnionOnlyPolymorphicClassifierFactory), ex.Message);
            Assert.Contains(nameof(UnsupportedAttrClassifiedAnimal), ex.Message);
        }

        [Fact]
        public void Classifier_ViaAttribute_IsVisibleToModifier()
        {
            YamlTypeClassifier? classifierFromModifier = null;

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(AttrClassifiedAnimal))
                {
                    classifierFromModifier = typeInfo.TypeClassifier;
                }
            });

            YamlTypeInfo typeInfo = options.GetTypeInfo(typeof(AttrClassifiedAnimal));

            Assert.NotNull(classifierFromModifier);
            Assert.Same(classifierFromModifier, typeInfo.TypeClassifier);
        }

        [Fact]
        public void MetadataServices_NullPolymorphismOptions_ReadsAttributes()
        {
            YamlTypeInfo<MetadataServicesAttributedBase> typeInfo = YamlMetadataServices.CreateObjectInfo(
                new YamlSerializerOptions(),
                new YamlObjectInfoValues<MetadataServicesAttributedBase>
                {
                    ObjectCreator = static () => new MetadataServicesAttributedBase(),
                });

            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            Assert.Equal("$metadata", options.TypeDiscriminatorPropertyName);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(MetadataServicesAttributedDerived), derivedType.DerivedType);
            Assert.Equal("attr-derived", derivedType.TypeDiscriminator);
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/128765", typeof(PlatformDetection), nameof(PlatformDetection.IsAppleMobile), nameof(PlatformDetection.IsMonoRuntime))]
        public void MetadataServices_NullPolymorphismOptions_DoesNotActivateAttributeClassifier()
        {
            YamlTypeInfo<AttrClassifiedAnimal> typeInfo = YamlMetadataServices.CreateObjectInfo(
                new YamlSerializerOptions(),
                new YamlObjectInfoValues<AttrClassifiedAnimal>
                {
                    ObjectCreator = static () => new AttrClassifiedAnimal(),
                });

            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            Assert.Collection(
                options.DerivedTypes,
                derivedType =>
                {
                    Assert.Equal(typeof(AttrClassifiedDog), derivedType.DerivedType);
                    Assert.Equal("dog", derivedType.TypeDiscriminator);
                },
                derivedType =>
                {
                    Assert.Equal(typeof(AttrClassifiedCat), derivedType.DerivedType);
                    Assert.Equal("cat", derivedType.TypeDiscriminator);
                });

            Assert.Null(typeInfo.TypeClassifier);
        }

        [Fact]
        public void MetadataServices_NullPolymorphismOptions_IgnoresTypeClassifierFactory()
        {
            YamlTypeInfo<AttrClassifiedAnimal> typeInfo = YamlMetadataServices.CreateObjectInfo(
                new YamlSerializerOptions(),
                new YamlObjectInfoValues<AttrClassifiedAnimal>
                {
                    ObjectCreator = static () => new AttrClassifiedAnimal(),
                    TypeClassifierFactory = new AnimalStructuralClassifierFactory(),
                });

            Assert.NotNull(typeInfo.PolymorphismOptions);
            Assert.Null(typeInfo.TypeClassifier);
        }

        [Fact]
        public void MetadataServices_EmptyPolymorphismOptions_IgnoresAttributes()
        {
            YamlTypeInfo<MetadataServicesAttributedBase> typeInfo = YamlMetadataServices.CreateObjectInfo(
                new YamlSerializerOptions(),
                new YamlObjectInfoValues<MetadataServicesAttributedBase>
                {
                    ObjectCreator = static () => new MetadataServicesAttributedBase(),
                    PolymorphismOptions = new YamlPolymorphismOptions(),
                });

            Assert.Null(typeInfo.PolymorphismOptions);
            Assert.Null(typeInfo.TypeClassifier);
        }

        [Fact]
        public void MetadataServices_ExplicitPolymorphismOptions_DoNotMergeAttributes()
        {
            YamlTypeInfo<MetadataServicesAttributedBase> typeInfo = YamlMetadataServices.CreateObjectInfo(
                new YamlSerializerOptions(),
                new YamlObjectInfoValues<MetadataServicesAttributedBase>
                {
                    ObjectCreator = static () => new MetadataServicesAttributedBase(),
                    PolymorphismOptions = new YamlPolymorphismOptions
                    {
                        TypeDiscriminatorPropertyName = "$explicit",
                        DerivedTypes =
                        {
                            new YamlDerivedType(typeof(MetadataServicesExplicitDerived), "explicit-derived"),
                        },
                    },
                });

            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            Assert.Equal("$explicit", options.TypeDiscriminatorPropertyName);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(MetadataServicesExplicitDerived), derivedType.DerivedType);
            Assert.Equal("explicit-derived", derivedType.TypeDiscriminator);
        }

        [Fact]
        public void MetadataServices_ValueInfoReadsAttributesWhenNoSentinelIsAvailable()
        {
            YamlTypeInfo<MetadataServicesAttributedBase> typeInfo = YamlMetadataServices.CreateValueInfo<MetadataServicesAttributedBase>(
                new YamlSerializerOptions(),
                new MetadataServicesAttributedBaseConverter());

            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            Assert.Equal("$metadata", options.TypeDiscriminatorPropertyName);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(MetadataServicesAttributedDerived), derivedType.DerivedType);
            Assert.Equal("attr-derived", derivedType.TypeDiscriminator);
        }

        [Fact]
        public void MetadataServices_CollectionPolymorphismOptions_ReadAttributesWhenNull()
        {
            var yamlOptions = new YamlSerializerOptions();
            YamlTypeInfo<int> elementInfo = YamlMetadataServices.CreateValueInfo<int>(yamlOptions, YamlMetadataServices.Int32Converter);
            YamlTypeInfo<MetadataServicesAttributedList> typeInfo = YamlMetadataServices.CreateListInfo<MetadataServicesAttributedList, int>(
                yamlOptions,
                new YamlCollectionInfoValues<MetadataServicesAttributedList>
                {
                    ObjectCreator = static () => new MetadataServicesAttributedList(),
                    ElementInfo = elementInfo,
                });

            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            Assert.Equal("$metadata", options.TypeDiscriminatorPropertyName);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(MetadataServicesAttributedListDerived), derivedType.DerivedType);
            Assert.Equal("attr-list", derivedType.TypeDiscriminator);
        }

        [Fact]
        public void MetadataServices_CollectionEmptyPolymorphismOptions_IgnoresAttributes()
        {
            var yamlOptions = new YamlSerializerOptions();
            YamlTypeInfo<int> elementInfo = YamlMetadataServices.CreateValueInfo<int>(yamlOptions, YamlMetadataServices.Int32Converter);
            YamlTypeInfo<MetadataServicesAttributedList> typeInfo = YamlMetadataServices.CreateListInfo<MetadataServicesAttributedList, int>(
                yamlOptions,
                new YamlCollectionInfoValues<MetadataServicesAttributedList>
                {
                    ObjectCreator = static () => new MetadataServicesAttributedList(),
                    ElementInfo = elementInfo,
                    PolymorphismOptions = new YamlPolymorphismOptions(),
                });

            Assert.Null(typeInfo.PolymorphismOptions);
            Assert.Null(typeInfo.TypeClassifier);
        }

        #endregion

        #region Collections of polymorphic types with classifier

        [Fact]
        public async Task Classifier_CollectionOfPolymorphicTypes()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """[{"Name":"Rex","Breed":"Lab"},{"Name":"Whiskers","Lives":9},{"Name":"Polly","CanTalk":true}]""";

            List<ClassifiedAnimalBase>? result = await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.IsType<ClassifiedDog>(result[0]);
            Assert.IsType<ClassifiedCat>(result[1]);
            Assert.IsType<ClassifiedParrot>(result[2]);
        }

        [Fact]
        public async Task Classifier_CollectionWithMixedDiscriminators()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);
            string yaml = """[{"kind":"dog","Name":"Rex","Breed":"Lab"},{"kind":"cat","Name":"Whiskers","Lives":9}]""";

            List<ClassifiedAnimalBase>? result = await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.IsType<ClassifiedDog>(result[0]);
            Assert.IsType<ClassifiedCat>(result[1]);
        }

        [Fact]
        public async Task Classifier_DictionaryWithPolymorphicValues()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"pet1":{"Name":"Rex","Breed":"Lab"},"pet2":{"Name":"Whiskers","Lives":9}}""";

            Dictionary<string, ClassifiedAnimalBase>? result =
                await Serializer.DeserializeWrapper<Dictionary<string, ClassifiedAnimalBase>>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.IsType<ClassifiedDog>(result["pet1"]);
            Assert.IsType<ClassifiedCat>(result["pet2"]);
        }

        #endregion

        #region Nested polymorphic types with classifier

        [Fact]
        public async Task Classifier_NestedPolymorphicProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = """{"OwnerName":"Alice","Pet":{"Name":"Rex","Breed":"Lab"}}""";

            PetOwner? result = await Serializer.DeserializeWrapper<PetOwner>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal("Alice", result.OwnerName);
            Assert.IsType<ClassifiedDog>(result.Pet);
            Assert.Equal("Lab", ((ClassifiedDog)result.Pet!).Breed);
        }

        [Fact]
        public async Task Classifier_NestedCollectionOfPolymorphicShapes()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string yaml = """{"Shapes":[{"Color":"red","Radius":5},{"Color":"blue","Width":10,"Height":20}]}""";

            DrawingCanvas? result = await Serializer.DeserializeWrapper<DrawingCanvas>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Shapes!.Count);
            Assert.IsType<ClassifiedCircle>(result.Shapes[0]);
            Assert.IsType<ClassifiedRectangle>(result.Shapes[1]);
        }

        #endregion

        #region Deep hierarchy with classifier

        [Theory]
        [InlineData("""{"Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDogL2))]
        [InlineData("""{"Name":"Buddy","Breed":"Lab","IsGuide":true}""", typeof(ClassifiedLabrador))]
        [InlineData("""{"Name":"Fifi","Breed":"Toy","Size":"Miniature"}""", typeof(ClassifiedPoodle))]
        public async Task Classifier_DeepHierarchy_ResolvesLeafType(string yaml, Type expectedType)
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalDeepHierarchy>();

            ClassifiedAnimalDeepHierarchy? result =
                await Serializer.DeserializeWrapper<ClassifiedAnimalDeepHierarchy>(yaml, options);

            Assert.IsType(expectedType, result);
        }

        #endregion

        #region Classifier interaction with standard $type discriminator

        [Fact]
        public async Task Classifier_OverridesDiscriminator_WhenSet()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            // No $type in JSON — classifier resolves by structure
            string yaml = """{"Name":"Rex","Breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType<ClassifiedDog>(result);
        }

        [Fact]
        public async Task Classifier_StandardDiscriminatorStillWorks_WhenNoClassifierSet()
        {
            string yaml = """{"$type":"dog","Name":"Rex","Breed":"Lab"}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml);

            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        #endregion

        #region Classifier + AllowOutOfOrderMetadataProperties

        [Fact]
        public async Task Classifier_PlusAllowOutOfOrder_ClassifierWins()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                {
                    typeInfo.TypeClassifier = classify;
                }
            });
            options.AllowOutOfOrderMetadataProperties = true;

            // "kind" property is not standard $type — classifier handles it
            string yaml = """{"Name":"Rex","kind":"dog","Breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType<ClassifiedDog>(result);
        }

        #endregion

        #region Classifier returning null

        [Fact]
        public async Task Classifier_ReturnsNull_ThrowsYamlException()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>((ref Utf8YamlReader _) => null);

            string yaml = """{"Name":"Unknown"}""";
            await Assert.ThrowsAsync<YamlException>(async () =>
                await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options));
        }

        [Fact]
        public async Task Classifier_ReturnsBaseType_ThrowsNotSupported()
        {
            // The base type itself is not in the derived types list,
            // so the resolver throws NotSupportedException.
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8YamlReader _) => typeof(ClassifiedAnimalBase));

            string yaml = """{"Name":"Generic"}""";
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options));
        }

        #endregion

        #region Classifier error cases

        [Fact]
        public async Task Classifier_ReturnsUnregisteredType_Throws()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8YamlReader _) => typeof(string));

            string yaml = """{"Name":"Rex"}""";
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options));
        }

        [Fact]
        public async Task Classifier_ThrowsException_Propagates()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8YamlReader _) => throw new InvalidOperationException("Test error"));

            string yaml = """{"Name":"Rex"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options));
        }

        #endregion

        #region Classifier with PropertyNamingPolicy

        [Fact]
        public async Task Classifier_WithCamelCaseNamingPolicy_ReadsRawYamlPropertyNames()
        {
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                {
                    // Classifier reads raw JSON property names, not C# names
                    typeInfo.TypeClassifier = (ref Utf8YamlReader reader) =>
                    {
                        if (reader.TokenType != YamlTokenType.StartObject) return null;
                        while (reader.Read() && reader.TokenType != YamlTokenType.EndObject)
                        {
                            if (reader.TokenType == YamlTokenType.PropertyName)
                            {
                                // JSON uses camelCase "breed", not PascalCase "Breed"
                                if (reader.ValueTextEquals("breed"u8)) return typeof(ClassifiedDog);
                                if (reader.ValueTextEquals("lives"u8)) return typeof(ClassifiedCat);
                                reader.Read();
                                reader.TrySkip();
                            }
                        }

                        return null;
                    };
                }
            });
            options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;

            string yaml = """{"name":"Rex","breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        #endregion

        #region Multiple polymorphic base types with different TypeClassifiers

        [Fact]
        public async Task Classifier_DifferentClassifiersForDifferentBaseTypes()
        {
            var animalFactory = new TestDiscriminatorClassifierFactory();
            var animalContext = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            YamlTypeClassifier animalClassify = animalFactory.CreateYamlClassifier(animalContext, Serializer.DefaultOptions);

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                {
                    typeInfo.TypeClassifier = animalClassify;
                }
                else if (typeInfo.Type == typeof(ClassifiedShape))
                {
                    typeInfo.TypeClassifier = (ref Utf8YamlReader reader) =>
                    {
                        if (reader.TokenType != YamlTokenType.StartObject) return null;
                        while (reader.Read() && reader.TokenType != YamlTokenType.EndObject)
                        {
                            if (reader.TokenType == YamlTokenType.PropertyName)
                            {
                                if (reader.ValueTextEquals("Radius"u8)) return typeof(ClassifiedCircle);
                                if (reader.ValueTextEquals("Width"u8)) return typeof(ClassifiedRectangle);
                                reader.Read();
                                reader.TrySkip();
                            }
                        }

                        return null;
                    };
                }
            });

            // Animal
            string animalYaml = """{"kind":"dog","Name":"Rex","Breed":"Lab"}""";
            ClassifiedAnimalBase? animal = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(animalYaml, options);
            Assert.IsType<ClassifiedDog>(animal);

            // Shape
            string shapeYaml = """{"Color":"red","Radius":5.0}""";
            ClassifiedShape? shape = await Serializer.DeserializeWrapper<ClassifiedShape>(shapeYaml, options);
            Assert.IsType<ClassifiedCircle>(shape);
        }

        #endregion

        #region Serialization round-trip with classifier

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Classifier_SerializeThenDeserialize_RoundTrips()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            ClassifiedAnimalBase original = new ClassifiedDog { Name = "Rex", Breed = "Lab" };
            string yaml = await Serializer.SerializeWrapper(original, options);

            // Serialization writes discriminator
            Assert.Contains("\"$type\"", yaml);

            // Deserialization with classifier can use either classifier or discriminator
            ClassifiedAnimalBase? roundtripped = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.IsType<ClassifiedDog>(roundtripped);
            Assert.Equal("Rex", roundtripped.Name);
            Assert.Equal("Lab", ((ClassifiedDog)roundtripped).Breed);
        }

        [Fact]
        public async Task Classifier_SerializeThenDeserialize_Collection()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            var original = new List<ClassifiedAnimalBase>
            {
                new ClassifiedDog { Name = "Rex", Breed = "Lab" },
                new ClassifiedCat { Name = "Whiskers", Lives = 9 },
            };

            string yaml = await Serializer.SerializeWrapper(original, options);
            List<ClassifiedAnimalBase>? roundtripped =
                await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(yaml, options);

            Assert.NotNull(roundtripped);
            Assert.Equal(2, roundtripped.Count);
            Assert.IsType<ClassifiedDog>(roundtripped[0]);
            Assert.IsType<ClassifiedCat>(roundtripped[1]);
        }

        #endregion

        #region Null and empty JSON values

        [Fact]
        public async Task Classifier_NullYamlValue_ReturnsNull()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = "null";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.Null(result);
        }

        [Fact]
        public async Task Classifier_EmptyObject_MatchesFirstDeclaredCase()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string yaml = "{}";

            // Empty object matches all types equally → first-declared wins.
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(yaml, options);
            Assert.NotNull(result);
        }

        #endregion

        [Fact]
        public void PolymorphismOptions_AreGenerated()
        {
            YamlTypeInfo<PolymorphicBaseWithCustomDiscriminator> typeInfo = Serializer.GetTypeInfo<PolymorphicBaseWithCustomDiscriminator>();
            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);

            Assert.True(options.IgnoreUnrecognizedTypeDiscriminators);
            Assert.Equal(YamlUnknownDerivedTypeHandling.FallBackToBaseType, options.UnknownDerivedTypeHandling);
            Assert.Equal("$kind", options.TypeDiscriminatorPropertyName);
            Assert.Collection(
                options.DerivedTypes,
                derivedType =>
                {
                    Assert.Equal(typeof(StringDiscriminatorDerived), derivedType.DerivedType);
                    Assert.Equal("string-derived", derivedType.TypeDiscriminator);
                },
                derivedType =>
                {
                    Assert.Equal(typeof(IntDiscriminatorDerived), derivedType.DerivedType);
                    Assert.Equal(42, derivedType.TypeDiscriminator);
                });
        }

        [Fact]
        public void CollectionPolymorphismOptions_AreGenerated()
        {
            YamlTypeInfo<PolymorphicIntList> typeInfo = Serializer.GetTypeInfo<PolymorphicIntList>();
            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);

            Assert.Equal("$kind", options.TypeDiscriminatorPropertyName);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(PolymorphicIntListDerived), derivedType.DerivedType);
            Assert.Equal("derived-list", derivedType.TypeDiscriminator);
        }

        [Fact]
        public async Task PolymorphicTypeClassifier_IsGeneratedAndVisibleToModifier()
        {
            bool modifierObservedClassifier = false;
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(FactoryClassifiedAnimal))
                {
                    Assert.NotNull(typeInfo.PolymorphismOptions);
                    Assert.NotNull(typeInfo.TypeClassifier);
                    modifierObservedClassifier = true;
                }
            });

            FactoryClassifiedAnimal? result = await Serializer.DeserializeWrapper<FactoryClassifiedAnimal>(
                """{"Name":"Rex","Breed":"Labrador"}""",
                options);

            FactoryClassifiedDog dog = Assert.IsType<FactoryClassifiedDog>(result);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
            Assert.True(modifierObservedClassifier);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task OpenGenericDerivedType_PartiallyConcrete_RoundTrips()
        {
            // OpenGenericDerived<T> : OpenGenericBase<T, int> registered on OpenGenericBase<string, int>.
            // Position 0 (T) unifies to string; position 1 (concrete int) matches. The resolved derived
            // type for the closed base must be OpenGenericDerived<string>.
            YamlTypeInfo<OpenGenericBase<string, int>> typeInfo = Serializer.GetTypeInfo<OpenGenericBase<string, int>>();
            YamlPolymorphismOptions options = Assert.IsType<YamlPolymorphismOptions>(typeInfo.PolymorphismOptions);
            YamlDerivedType derivedType = Assert.Single(options.DerivedTypes);
            Assert.Equal(typeof(OpenGenericDerived<string>), derivedType.DerivedType);
            Assert.Equal("derived", derivedType.TypeDiscriminator);

            OpenGenericBase<string, int> value = new OpenGenericDerived<string> { Extra = "hello" };
            string yaml = await Serializer.SerializeWrapper(value, typeInfo);
            Assert.Contains("\"$type\":\"derived\"", yaml);

            OpenGenericBase<string, int>? result = await Serializer.DeserializeWrapper(yaml, typeInfo);
            OpenGenericDerived<string> d = Assert.IsType<OpenGenericDerived<string>>(result);
            Assert.Equal("hello", d.Extra);
        }

        #region Helpers

        private YamlSerializerOptions CreateOptionsWithStructuralClassifier<TBase>()
            where TBase : class
        {
            return Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(TBase) && typeInfo.PolymorphismOptions is not null)
                {
                    var factory = new TestStructuralClassifierFactory();
                    var context = YamlTypeClassifierContextTestExtensions.Create(
                        typeInfo,
                        Array.Empty<YamlUnionCaseInfo>(),
                        typeInfo.PolymorphismOptions.DerivedTypes.ToList(),
                        typeInfo.PolymorphismOptions.TypeDiscriminatorPropertyName);

                    typeInfo.TypeClassifier =
                        factory.CreateYamlClassifier(context, typeInfo.Options);
                }
            });
        }

        [Fact]
        public async Task Classifier_WithReferenceHandlerPreserve_PreservesReferences()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = YamlTypeClassifierContextTestExtensions.Create(
                                    Serializer.GetTypeInfo<ClassifiedAnimalBase>(),
                                    Array.Empty<YamlUnionCaseInfo>(),
                                    new YamlDerivedType[] {
                    new YamlDerivedType(typeof(ClassifiedDog), "dog"),
                    new YamlDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            YamlTypeClassifier classify = factory.CreateYamlClassifier(context, Serializer.DefaultOptions);

            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(ClassifiedAnimalBase) && typeInfo.PolymorphismOptions is not null)
                {
                    typeInfo.PolymorphismOptions.TypeDiscriminatorPropertyName = "kind";
                    typeInfo.TypeClassifier = classify;
                }
            });
            options.ReferenceHandler = ReferenceHandler.Preserve;

            var dog = new ClassifiedDog { Name = "Rex", Breed = "Lab" };
            ClassifiedAnimalBase[] payload = new ClassifiedAnimalBase[] { dog, dog };

            string yaml = await Serializer.SerializeWrapper(payload, options);

            ClassifiedAnimalBase[]? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase[]>(yaml, options);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Length);
            Assert.IsType<ClassifiedDog>(result[0]);
            Assert.Same(result[0], result[1]);
            Assert.Equal("Rex", result[0].Name);
            Assert.Equal("Lab", ((ClassifiedDog)result[0]).Breed);
        }

        private YamlSerializerOptions CreateOptionsWithClassifier<TBase>(YamlTypeClassifier classifier)
            where TBase : class
        {
            return Serializer.GetDefaultOptionsWithMetadataModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(TBase) && typeInfo.PolymorphismOptions is not null)
                {
                    typeInfo.TypeClassifier = classifier;
                }
            });
        }

        #endregion
    }
}
