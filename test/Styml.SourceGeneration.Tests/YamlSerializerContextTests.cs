// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Text.Yaml.SourceGeneration.Tests.NETStandard;
using System.Threading.Tasks;
using Microsoft.DotNet.RemoteExecutor;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public static partial class YamlSerializerContextTests
    {
        [Fact]
        public static void VariousNestingAndVisibilityLevelsAreSupported()
        {
            Assert.NotNull(PublicContext.Default);
            Assert.NotNull(NestedContext.Default);
            Assert.NotNull(NestedPublicContext.Default);
            Assert.NotNull(NestedPublicContext.NestedProtectedInternalClass.Default);
        }

        [Fact]
        public static void PropertyMetadataIsImmutable()
        {
            YamlTypeInfo<Person> typeInfo = PersonYamlContext.Default.Person;

            Assert.True(typeInfo.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => typeInfo.CreateObject = null);
            Assert.Throws<InvalidOperationException>(() => typeInfo.OnDeserializing = obj => { });
            Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Clear());

            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];
            Assert.Throws<InvalidOperationException>(() => propertyInfo.Name = "differentName");
            Assert.Throws<InvalidOperationException>(() => propertyInfo.NumberHandling = YamlNumberHandling.AllowReadingFromString);
            Assert.Throws<InvalidOperationException>(() => propertyInfo.IsRequired = true);
            Assert.Throws<InvalidOperationException>(() => propertyInfo.Order = -1);
        }

        [Fact]
        public static void YamlSerializerContext_GetTypeInfo_MetadataIsImmutable()
        {
            YamlTypeInfo<Person> typeInfo = (YamlTypeInfo<Person>)PersonYamlContext.Default.GetTypeInfo(typeof(Person));

            Assert.True(typeInfo.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => typeInfo.CreateObject = null);
            Assert.Throws<InvalidOperationException>(() => typeInfo.OnDeserializing = obj => { });
            Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Clear());

            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];
            Assert.Throws<InvalidOperationException>(() => propertyInfo.Name = "differentName");
            Assert.Throws<InvalidOperationException>(() => propertyInfo.NumberHandling = YamlNumberHandling.AllowReadingFromString);
            Assert.Throws<InvalidOperationException>(() => propertyInfo.IsRequired = true);
            Assert.Throws<InvalidOperationException>(() => propertyInfo.Order = -1);
        }

        [Fact]
        public static void IYamlTypeInfoResolver_GetTypeInfo_MetadataIsMutable()
        {
            IYamlTypeInfoResolver resolver = PersonYamlContext.Default;
            YamlTypeInfo<Person> typeInfo = (YamlTypeInfo<Person>)resolver.GetTypeInfo(typeof(Person), PersonYamlContext.Default.Options);

            Assert.NotSame(typeInfo, PersonYamlContext.Default.Person);
            Assert.False(typeInfo.IsReadOnly);

            YamlTypeInfo<Person> typeInfo2 = (YamlTypeInfo<Person>)resolver.GetTypeInfo(typeof(Person), PersonYamlContext.Default.Options);
            Assert.NotSame(typeInfo, typeInfo2);
            Assert.False(typeInfo.IsReadOnly);

            typeInfo.CreateObject = null;
            typeInfo.OnDeserializing = obj => { };

            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];
            propertyInfo.Name = "differentName";
            propertyInfo.NumberHandling = YamlNumberHandling.AllowReadingFromString;
            propertyInfo.IsRequired = true;
            propertyInfo.Order = -1;

            typeInfo.Properties.Clear();
            Assert.Equal(0, typeInfo.Properties.Count);

            // Changes should not impact other metadata instances
            Assert.Equal(2, typeInfo2.Properties.Count);
            Assert.Equal(2, PersonYamlContext.Default.Person.Properties.Count);
        }

        [Fact]
        public static void VariousGenericsAreSupported()
        {
            AssertGenericContext(GenericContext<int>.Default);
            AssertGenericContext(ContextGenericContainer<int>.NestedInGenericContainerContext.Default);
            AssertGenericContext(ContextGenericContainer<int>.NestedGenericInGenericContainerContext<int>.Default);
            AssertGenericContext(ContextGenericContainer<int>.NestedGenericContainer<int>.NestedInNestedGenericContainerContext.Default);
            AssertGenericContext(ContextGenericContainer<int>.NestedGenericContainer<int>.NestedGenericInNestedGenericContainerContext<int>.Default);

            Assert.NotNull(NestedGenericTypesContext.Default);
            var original = new MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>()
            {
                DataT = 1,
                DataT1 = 10,
                DataT2 = 100
            };
            Type type = typeof(MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>);
            string yaml = YamlSerializer.Serialize(original, type, NestedGenericTypesContext.Default);
            var deserialized = (MyContainingGenericClass<int>.MyNestedGenericClass<int>.MyNestedGenericNestedGenericClass<int>)YamlSerializer.Deserialize(yaml, type, NestedGenericTypesContext.Default);
            Assert.Equal(1, deserialized.DataT);
            Assert.Equal(10, deserialized.DataT1);
            Assert.Equal(100, deserialized.DataT2);

            static void AssertGenericContext(YamlSerializerContext context)
            {
                Assert.NotNull(context);
                string yaml = YamlSerializer.Serialize(new YamlMessage { Message = "Hi" }, typeof(YamlMessage), context);
                YamlMessage deserialized = (YamlMessage)YamlSerializer.Deserialize(yaml, typeof(YamlMessage), context);
                Assert.Equal("Hi", deserialized.Message);
            }
        }

        [Fact]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static async Task SupportsBoxedRootLevelValues()
        {
            PersonYamlContext context = PersonYamlContext.Default;
            object person = new Person("John", "Smith");
            string expectedYaml = """{"firstName":"John","lastName":"Smith"}""";
            // Sanity check -- context resolver does not specify object metadata
            Assert.Null(((IYamlTypeInfoResolver)context).GetTypeInfo(typeof(object), new()));

            string yaml = YamlSerializer.Serialize(person, context.Options);
            Assert.Equal(expectedYaml, yaml);

            yaml = YamlSerializer.Serialize(person, typeof(object), context);
            Assert.Equal(expectedYaml, yaml);

            yaml = YamlSerializer.Serialize(person, context.GetTypeInfo(typeof(object)));
            Assert.NotNull(context.GetTypeInfo(typeof(object)));

            var stream = new Utf8MemoryStream();
            await YamlSerializer.SerializeAsync(stream, person, context.Options);
            Assert.Equal(expectedYaml, stream.AsString());
        }

        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Converters_AndTypeInfoCreator_NotRooted_WhenMetadataNotPresent()
        {
            RemoteExecutor.Invoke(
                static () =>
                {
                    object[] objArr = new object[] { new MyStruct() };

                    // Metadata not generated for MyStruct without YamlSerializableAttribute.
                    NotSupportedException ex = Assert.Throws<NotSupportedException>(
                        () => YamlSerializer.Serialize(objArr, MetadataContext.Default.ObjectArray));
                    string exAsStr = ex.ToString();
                    Assert.Contains(typeof(MyStruct).ToString(), exAsStr);
                    Assert.Contains("YamlSerializerOptions", exAsStr);

                    // This test uses reflection to:
                    // - Access DefaultYamlTypeInfoResolver.s_defaultSimpleConverters
                    // - Access DefaultYamlTypeInfoResolver.s_defaultFactoryConverters
                    //
                    // If any of them changes, this test will need to be kept in sync.

                    // Confirm built-in converters not set.
                    AssertFieldNull("s_defaultSimpleConverters");
                    AssertFieldNull("s_defaultFactoryConverters");

                    static void AssertFieldNull(string fieldName)
                    {
                        FieldInfo fieldInfo = typeof(DefaultYamlTypeInfoResolver).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                        Assert.NotNull(fieldInfo);
                        Assert.Null(fieldInfo.GetValue(null));
                    }
                }).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void YamlSerializerContext_GeneratedDefault_IsSingleton()
        {
            RemoteExecutor.Invoke(
                static () =>
                {
                    const int Count = 30;
                    var contexts = new MetadataContext[Count];
                    Parallel.For(0, Count, i => contexts[i] = MetadataContext.Default);

                    Assert.All(contexts, ctx => Assert.Same(MetadataContext.Default, ctx));

                }).Dispose();
        }

        [Fact]
        public static void SupportsReservedLanguageKeywordsAsProperties()
        {
            GreetingCard card = new()
            {
                @event = "Birthday",
                message = @"Happy Birthday!"
            };

            byte[] utf8Yaml = YamlSerializer.SerializeToUtf8Bytes(card, GreetingCardYamlContext.Default.GreetingCard);

            card = YamlSerializer.Deserialize<GreetingCard>(utf8Yaml, GreetingCardYamlContext.Default.GreetingCard);
            Assert.Equal("Birthday", card.@event);
            Assert.Equal("Happy Birthday!", card.message);
        }

        [Fact]
        public static void SupportsReservedLanguageKeywordsAsFields()
        {
            var options = new YamlSerializerOptions { IncludeFields = true };

            GreetingCardWithFields card = new() {@event = "Birthday", message = @"Happy Birthday!"};
        
            byte[] utf8Yaml = YamlSerializer.SerializeToUtf8Bytes(card, GreetingCardWithFieldsYamlContext.Default.GreetingCardWithFields);
        
            card = YamlSerializer.Deserialize<GreetingCardWithFields>(utf8Yaml, GreetingCardWithFieldsYamlContext.Default.GreetingCardWithFields);
            Assert.Equal("Happy Birthday!", card.message);
            Assert.Equal("Birthday", card.@event);
        }

        [Fact]
        public static void SupportsPositionalRecords()
        {
            Person person = new(FirstName: "Jane", LastName: "Doe");

            byte[] utf8Yaml = YamlSerializer.SerializeToUtf8Bytes(person, PersonYamlContext.Default.Person);

            person = YamlSerializer.Deserialize<Person>(utf8Yaml, PersonYamlContext.Default.Person);
            Assert.Equal("Jane", person.FirstName);
            Assert.Equal("Doe", person.LastName);
        }

        [Fact]
        public static void CombiningContexts_ResolveYamlTypeInfo()
        {
            IYamlTypeInfoResolver combined = YamlTypeInfoResolver.Combine(NestedContext.Default, PersonYamlContext.Default);
            var options = new YamlSerializerOptions { TypeInfoResolver = combined };

            YamlTypeInfo messageInfo = combined.GetTypeInfo(typeof(YamlMessage), options);
            Assert.IsAssignableFrom<YamlTypeInfo<YamlMessage>>(messageInfo);
            Assert.Same(options, messageInfo.Options);

            YamlTypeInfo personInfo = combined.GetTypeInfo(typeof(Person), options);
            Assert.IsAssignableFrom<YamlTypeInfo<Person>>(personInfo);
            Assert.Same(options, personInfo.Options);
        }

        [Fact]
        public static void ChainedContexts_ResolveYamlTypeInfo()
        {
            var options = new YamlSerializerOptions { TypeInfoResolverChain = { NestedContext.Default, PersonYamlContext.Default } };

            YamlTypeInfo messageInfo = options.GetTypeInfo(typeof(YamlMessage));
            Assert.IsAssignableFrom<YamlTypeInfo<YamlMessage>>(messageInfo);
            Assert.Same(options, messageInfo.Options);

            YamlTypeInfo personInfo = options.GetTypeInfo(typeof(Person));
            Assert.IsAssignableFrom<YamlTypeInfo<Person>>(personInfo);
            Assert.Same(options, personInfo.Options);

            NotSupportedException exn = Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(MyStruct)));
            Assert.Contains(typeof(NestedContext).FullName, exn.Message);
            Assert.Contains(typeof(PersonYamlContext).FullName, exn.Message);
        }

        [Fact]
        public static void CombiningContexts_ResolveYamlTypeInfo_DifferentCasing()
        {
            IYamlTypeInfoResolver combined = YamlTypeInfoResolver.Combine(NestedContext.Default, PersonYamlContext.Default);
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = combined,
                PropertyNamingPolicy = YamlNamingPolicy.CamelCase,
            };

            Assert.NotSame(YamlNamingPolicy.CamelCase, NestedContext.Default.Options.PropertyNamingPolicy);
            Assert.Same(YamlNamingPolicy.CamelCase, PersonYamlContext.Default.Options.PropertyNamingPolicy);

            YamlTypeInfo messageInfo = combined.GetTypeInfo(typeof(YamlMessage), options);
            Assert.Equal(2, messageInfo.Properties.Count);
            Assert.Equal("message", messageInfo.Properties[0].Name);
            Assert.Equal("length", messageInfo.Properties[1].Name);

            YamlTypeInfo personInfo = combined.GetTypeInfo(typeof(Person), options);
            Assert.Equal(2, personInfo.Properties.Count);
            Assert.Equal("firstName", personInfo.Properties[0].Name);
            Assert.Equal("lastName", personInfo.Properties[1].Name);
        }

        [Fact]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void FastPathSerialization_ResolvingYamlTypeInfo()
        {
            YamlSerializerOptions options = FastPathSerializationContext.Default.Options;

            YamlTypeInfo<YamlMessage> yamlMessageInfo = options.GetTypeInfo<YamlMessage>();
            Assert.NotNull(yamlMessageInfo.SerializeHandler);

            var value = new YamlMessage { Message = "Hi" };
            string expectedYaml = """{"Message":"Hi","Length":2}""";

            Assert.Equal(expectedYaml, YamlSerializer.Serialize(value, yamlMessageInfo));
            Assert.Equal(expectedYaml, YamlSerializer.Serialize(value, options));

            // Throws since deserialization without metadata is not supported
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<YamlMessage>(expectedYaml, yamlMessageInfo));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<YamlMessage>(expectedYaml, options));
        }

        [Theory]
        [MemberData(nameof(GetFastPathCompatibleResolvers))]
        [MemberData(nameof(GetFastPathIncompatibleResolvers))]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void FastPathSerialization_AppendedResolver_WorksAsExpected(IYamlTypeInfoResolver appendedResolver)
        {
            // Resolvers appended after ours will never introduce metadata to the type graph,
            // therefore the fast path should always be used regardless of what they are doing.

            var fastPathContext = new ContextWithInstrumentedFastPath();
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = YamlTypeInfoResolver.Combine(fastPathContext, appendedResolver, new DefaultYamlTypeInfoResolver())
            };

            YamlTypeInfo<PocoWithInteger> yamlMessageInfo = options.GetTypeInfo<PocoWithInteger>();
            Assert.NotNull(yamlMessageInfo.SerializeHandler);

            var value = new PocoWithInteger { Value = 42 };
            string expectedYaml = """{"Value":42}""";

            string yaml = YamlSerializer.Serialize(value, yamlMessageInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(1, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(2, fastPathContext.FastPathInvocationCount);

            YamlTypeInfo<ContainingClass> classInfo = options.GetTypeInfo<ContainingClass>();
            Assert.Null(classInfo.SerializeHandler);

            var largerValue = new ContainingClass { Message = value };
            expectedYaml = $$"""{"Message":{{expectedYaml}}}""";

            yaml = YamlSerializer.Serialize(largerValue, classInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(3, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(largerValue, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(4, fastPathContext.FastPathInvocationCount);
        }

        [Theory]
        [MemberData(nameof(GetFastPathCompatibleResolvers))]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void FastPathSerialization_PrependedResolver_CompatibleResolvers_WorksAsExpected(IYamlTypeInfoResolver prependedResolver)
        {
            // We're prepending a resolver that generates metadata for the property of our type,
            // but because the two sources use compatible configuration the fast path should still be used.

            var fastPathContext = new ContextWithInstrumentedFastPath();
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = YamlTypeInfoResolver.Combine(prependedResolver, fastPathContext, new DefaultYamlTypeInfoResolver())
            };

            YamlTypeInfo<PocoWithInteger> yamlMessageInfo = options.GetTypeInfo<PocoWithInteger>();
            Assert.NotNull(yamlMessageInfo.SerializeHandler);

            var value = new PocoWithInteger { Value = 42 };
            string expectedYaml = """{"Value":42}""";

            string yaml = YamlSerializer.Serialize(value, yamlMessageInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(1, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(2, fastPathContext.FastPathInvocationCount);

            YamlTypeInfo<ContainingClass> classInfo = options.GetTypeInfo<ContainingClass>();
            Assert.Null(classInfo.SerializeHandler);

            var largerValue = new ContainingClass { Message = value };
            expectedYaml = $$"""{"Message":{{expectedYaml}}}""";

            yaml = YamlSerializer.Serialize(largerValue, classInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(3, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(largerValue, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(4, fastPathContext.FastPathInvocationCount);
        }

        [Theory]
        [MemberData(nameof(GetFastPathIncompatibleResolvers))]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void FastPathSerialization_PrependedResolver_IncompatibleResolvers_FallsBackToMetadata(IYamlTypeInfoResolver prependedResolver)
        {
            // We're prepending a resolver that generates metadata for the property of our type,
            // because the two sources use incompatible configuration the fast path should not be used.

            var fastPathContext = new ContextWithInstrumentedFastPath();
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = YamlTypeInfoResolver.Combine(prependedResolver, fastPathContext, new DefaultYamlTypeInfoResolver())
            };

            YamlTypeInfo<PocoWithInteger> yamlMessageInfo = options.GetTypeInfo<PocoWithInteger>();
            Assert.NotNull(yamlMessageInfo.SerializeHandler);

            var value = new PocoWithInteger { Value = 42 };
            string expectedYaml = """{"Value":42}""";

            string yaml = YamlSerializer.Serialize(value, yamlMessageInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(0, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(0, fastPathContext.FastPathInvocationCount);

            YamlTypeInfo<ContainingClass> classInfo = options.GetTypeInfo<ContainingClass>();
            Assert.Null(classInfo.SerializeHandler);

            var largerValue = new ContainingClass { Message = value };
            expectedYaml = $$"""{"Message":{{expectedYaml}}}""";

            yaml = YamlSerializer.Serialize(largerValue, classInfo);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(0, fastPathContext.FastPathInvocationCount);

            yaml = YamlSerializer.Serialize(largerValue, options);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(0, fastPathContext.FastPathInvocationCount);
        }

        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        public static IEnumerable<object[]> GetFastPathCompatibleResolvers()
        {
            yield return new object[] { CompatibleWithInstrumentedFastPathContext.Default };
            yield return new object[] { new CustomWrappingResolver<int> { Resolver = new DefaultYamlTypeInfoResolver() } };
            yield return new object[] { new CustomWrappingResolver<int> { Resolver = CompatibleWithInstrumentedFastPathContext.Default } };
            yield return new object[] { new CustomWrappingResolver<int> { Resolver = new ContextWithInstrumentedFastPath() } };
        }

        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        public static IEnumerable<object[]> GetFastPathIncompatibleResolvers()
        {
            yield return new object[] { NotCompatibleWithInstrumentedFastPathContext.Default };
            yield return new object[] { new CustomWrappingResolver<int> { Resolver = new DefaultYamlTypeInfoResolver { Modifiers = { static jti => jti.PolymorphismOptions = null } } } };
            yield return new object[] { new CustomWrappingResolver<int> { Resolver = NotCompatibleWithInstrumentedFastPathContext.Default } };
        }

        public class PocoWithInteger
        {
            public int Value { get; set; }
        }

        public class ContainingClass
        {
            public PocoWithInteger Message { get; set; }
        }

        public class ContextWithInstrumentedFastPath : YamlSerializerContext, IYamlTypeInfoResolver
        {
            public int FastPathInvocationCount { get; private set; }

            public ContextWithInstrumentedFastPath() : base(null)
            { }

            protected override YamlSerializerOptions? GeneratedSerializerOptions => Options;
            public override YamlTypeInfo? GetTypeInfo(Type type) => GetTypeInfo(type, Options);
            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                YamlTypeInfo? typeInfo = null;

                if (type == typeof(int))
                {
                    typeInfo = YamlMetadataServices.CreateValueInfo<int>(options, YamlMetadataServices.Int32Converter);
                }

                if (type == typeof(PocoWithInteger))
                {
                    typeInfo = YamlMetadataServices.CreateObjectInfo<PocoWithInteger>(options,
                        new YamlObjectInfoValues<PocoWithInteger>
                        {
                            PropertyMetadataInitializer = _ => new YamlPropertyInfo[1]
                            {
                                YamlMetadataServices.CreatePropertyInfo(options,
                                    new YamlPropertyInfoValues<int>
                                    {
                                        IsProperty = true,
                                        IsPublic = true,
                                        DeclaringType = typeof(PocoWithInteger),
                                        PropertyName = "Value",
                                        Getter = obj => ((PocoWithInteger)obj).Value,
                                        Setter = (obj, value) => ((PocoWithInteger)obj).Value = value,
                                    })
                            },

                            SerializeHandler = (writer, value) =>
                            {
                                writer.WriteStartObject();
                                writer.WriteNumber("Value", value.Value);
                                writer.WriteEndObject();
                                FastPathInvocationCount++;
                            }
                        });
                }

                if (typeInfo != null)
                    typeInfo.OriginatingResolver = this;

                return typeInfo;
            }
        }

        [YamlSerializable(typeof(int))]
        public partial class CompatibleWithInstrumentedFastPathContext : YamlSerializerContext
        { }

        [YamlSourceGenerationOptions(IncludeFields = true)]
        [YamlSerializable(typeof(int))]
        public partial class NotCompatibleWithInstrumentedFastPathContext : YamlSerializerContext
        { }

        public class CustomWrappingResolver<T> : IYamlTypeInfoResolver
        {
            public required IYamlTypeInfoResolver Resolver { get; init; }
            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
                => type == typeof(T) ? Resolver.GetTypeInfo(type, options) : null;
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization)]
        [YamlSerializable(typeof(YamlMessage))]
        [YamlSerializable(typeof(AllocatingOnPropertyAccess))]
        public partial class FastPathSerializationContext : YamlSerializerContext
        { }

        [Theory]
        [MemberData(nameof(GetCombiningContextsData))]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        public static void CombiningContexts_Serialization<T>(T value, string expectedYaml)
        {
            IYamlTypeInfoResolver combined = YamlTypeInfoResolver.Combine(NestedContext.Default, PersonYamlContext.Default);
            var options = new YamlSerializerOptions { TypeInfoResolver = combined };

            YamlTypeInfo<T> typeInfo = (YamlTypeInfo<T>)combined.GetTypeInfo(typeof(T), options)!;

            string yaml = YamlSerializer.Serialize(value, typeInfo);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            YamlSerializer.Deserialize<T>(yaml, typeInfo);
            YamlSerializer.Deserialize<T>(yaml, options);
        }

        [Theory]
        [MemberData(nameof(GetCombiningContextsData))]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        public static void ChainedContexts_Serialization<T>(T value, string expectedYaml)
        {
            var options = new YamlSerializerOptions { TypeInfoResolverChain = { NestedContext.Default, PersonYamlContext.Default } };

            YamlTypeInfo<T> typeInfo = options.GetTypeInfo<T>();

            string yaml = YamlSerializer.Serialize(value, typeInfo);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            YamlSerializer.Deserialize<T>(yaml, typeInfo);
            YamlSerializer.Deserialize<T>(yaml, options);
        }

        [Fact]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void CombiningContextWithCustomResolver_ReplacePoco()
        {
            TestResolver customResolver = new((type, options) =>
            {
                if (type != typeof(TestPoco))
                    return null;

                YamlTypeInfo<TestPoco> typeInfo = YamlTypeInfo.CreateYamlTypeInfo<TestPoco>(options);
                typeInfo.CreateObject = () => new TestPoco();
                YamlPropertyInfo property = typeInfo.CreateYamlPropertyInfo(typeof(string), "test");
                property.Get = (o) => unsafe(System.Runtime.CompilerServices.Unsafe.Unbox<TestPoco>(o)).IntProperty.ToString();
                property.Set = (o, val) =>
                {
                    unsafe
                    {
                        System.Runtime.CompilerServices.Unsafe.Unbox<TestPoco>(o).StringProperty = (string)val;
                        System.Runtime.CompilerServices.Unsafe.Unbox<TestPoco>(o).IntProperty = int.Parse((string)val);
                    }
                };

                typeInfo.Properties.Add(property);
                return typeInfo;
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = YamlTypeInfoResolver.Combine(customResolver, ClassWithPocoListDictionaryAndNullablePropertyContext.Default);

            // ensure we're not falling back to reflection serialization
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(new Person("a", "b"), o));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize((byte)1, o));

            ClassWithPocoListDictionaryAndNullable obj = new()
            {
                UIntProperty = 13,
                ListOfPocoProperty = new List<TestPoco>() { new TestPoco() { IntProperty = 4 }, new TestPoco() { IntProperty = 5 } },
                DictionaryPocoValueProperty = new Dictionary<char, TestPoco>() { ['c'] = new TestPoco() { IntProperty = 6 }, ['d'] = new TestPoco() { IntProperty = 7 } },
                NullablePocoProperty = new TestPoco() { IntProperty = 8 },
                PocoProperty = new TestPoco() { IntProperty = 9 },
            };

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"UIntProperty":13,"ListOfPocoProperty":[{"test":"4"},{"test":"5"}],"DictionaryPocoValueProperty":{"c":{"test":"6"},"d":{"test":"7"}},"NullablePocoProperty":{"test":"8"},"PocoProperty":{"test":"9"}}""", yaml);

            ClassWithPocoListDictionaryAndNullable deserialized = YamlSerializer.Deserialize<ClassWithPocoListDictionaryAndNullable>(yaml, o);
            Assert.Equal(obj.UIntProperty, deserialized.UIntProperty);
            Assert.Equal(obj.ListOfPocoProperty.Count, deserialized.ListOfPocoProperty.Count);
            Assert.Equal(2, obj.ListOfPocoProperty.Count);
            Assert.Equal(obj.ListOfPocoProperty[0].IntProperty.ToString(), deserialized.ListOfPocoProperty[0].StringProperty);
            Assert.Equal(obj.ListOfPocoProperty[0].IntProperty, deserialized.ListOfPocoProperty[0].IntProperty);
            Assert.Equal(obj.ListOfPocoProperty[1].IntProperty.ToString(), deserialized.ListOfPocoProperty[1].StringProperty);
            Assert.Equal(obj.ListOfPocoProperty[1].IntProperty, deserialized.ListOfPocoProperty[1].IntProperty);
            Assert.Equal(obj.DictionaryPocoValueProperty.Count, deserialized.DictionaryPocoValueProperty.Count);
            Assert.Equal(2, obj.DictionaryPocoValueProperty.Count);
            Assert.Equal(obj.DictionaryPocoValueProperty['c'].IntProperty.ToString(), deserialized.DictionaryPocoValueProperty['c'].StringProperty);
            Assert.Equal(obj.DictionaryPocoValueProperty['c'].IntProperty, deserialized.DictionaryPocoValueProperty['c'].IntProperty);
            Assert.Equal(obj.DictionaryPocoValueProperty['d'].IntProperty.ToString(), deserialized.DictionaryPocoValueProperty['d'].StringProperty);
            Assert.Equal(obj.DictionaryPocoValueProperty['d'].IntProperty, deserialized.DictionaryPocoValueProperty['d'].IntProperty);
            Assert.Equal(obj.NullablePocoProperty.Value.IntProperty.ToString(), deserialized.NullablePocoProperty.Value.StringProperty);
            Assert.Equal(obj.NullablePocoProperty.Value.IntProperty, deserialized.NullablePocoProperty.Value.IntProperty);
            Assert.Equal(obj.PocoProperty.IntProperty.ToString(), deserialized.PocoProperty.StringProperty);
            Assert.Equal(obj.PocoProperty.IntProperty, deserialized.PocoProperty.IntProperty);
        }

        public static IEnumerable<object[]> GetCombiningContextsData()
        {
            yield return WrapArgs(new YamlMessage { Message = "Hi" }, """{ "Message" : "Hi", "Length" : 2 }""");
            yield return WrapArgs(new Person("John", "Doe"), """{ "FirstName" : "John", "LastName" : "Doe" }""");
            static object[] WrapArgs<T>(T value, string expectedYaml) => new object[] { value, expectedYaml };
        }

        [YamlSerializable(typeof(YamlMessage))]
        internal partial class NestedContext : YamlSerializerContext { }

        [YamlSerializable(typeof(YamlMessage))]
        public partial class NestedPublicContext : YamlSerializerContext
        {
            [YamlSerializable(typeof(YamlMessage))]
            protected internal partial class NestedProtectedInternalClass : YamlSerializerContext { }
        }

        internal record Person(string FirstName, string LastName);

        [YamlSourceGenerationOptions(
            PropertyNamingPolicy = YamlKnownNamingPolicy.CamelCase)]
        [YamlSerializable(typeof(Person))]
        internal partial class PersonYamlContext : YamlSerializerContext
        {
        }

        internal class GreetingCard
        {
            public string @event { get;set; }
            public string message { get;set; }
        }

        internal class GreetingCardWithFields
        {
            public string @event;
            public string message;
        }

        [YamlSerializable(typeof(GreetingCard))]
        internal partial class GreetingCardYamlContext : YamlSerializerContext
        {
        }

        [YamlSourceGenerationOptions(IncludeFields = true)]
        [YamlSerializable(typeof(GreetingCardWithFields))]
        internal partial class GreetingCardWithFieldsYamlContext : YamlSerializerContext
        {
        }

        // Regression test for https://github.com/dotnet/runtime/issues/62079
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsPropertiesWithCustomConverterFactory()
        {
            var value = new ClassWithCustomConverterFactoryProperty { MyEnum = SourceGenSampleEnum.MinZero };
            string yaml = YamlSerializer.Serialize(value, SingleClassWithCustomConverterFactoryPropertyContext.Default.ClassWithCustomConverterFactoryProperty);
            Assert.Equal("""{"MyEnum":"MinZero"}""", yaml);
        }

        public class ParentClass
        {
            public ClassWithCustomConverterFactoryProperty? Child { get; set; }
        }

        [YamlSerializable(typeof(ParentClass))]
        internal partial class SingleClassWithCustomConverterFactoryPropertyContext : YamlSerializerContext
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsGenericParameterWithCustomConverterFactory()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/61860
            var value = new List<TestEnum> { TestEnum.Cee };
            string yaml = YamlSerializer.Serialize(value, GenericParameterWithCustomConverterFactoryContext.Default.ListTestEnum);
            Assert.Equal("""["Cee"]""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithStringValuesRoundtrips()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/74652
            YamlSerializerOptions options = ClassWithStringValuesContext.Default.Options;

            ClassWithStringValues obj = new()
            {
                StringValuesProperty = new(new[] { "abc", "def" })
            };

            string yaml = YamlSerializer.Serialize(obj, options.GetTypeInfo<ClassWithStringValues>());
            Assert.Equal("""{"StringValuesProperty":["abc","def"]}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithDictionaryPropertyRoundtrips()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/61734
            YamlSerializerOptions options = ClassWithDictionaryPropertyContext.Default.Options;

            ClassWithDictionaryProperty obj = new(new Dictionary<string, object?>()
            {
                ["foo"] = "bar",
                ["test"] = "baz",
            });

            string yaml = YamlSerializer.Serialize(obj, options.GetTypeInfo<ClassWithDictionaryProperty>());
            Assert.Equal("""{"DictionaryProperty":{"foo":"bar","test":"baz"}}""", yaml);
        }

        [YamlConverter(typeof(YamlStringEnumConverter<TestEnum>))]
        public enum TestEnum
        {
            Aye, Bee, Cee
        }

        [YamlSerializable(typeof(List<TestEnum>))]
        internal partial class GenericParameterWithCustomConverterFactoryContext : YamlSerializerContext
        {
        }

        [YamlSerializable(typeof(ClassWithPocoListDictionaryAndNullable))]
        internal partial class ClassWithPocoListDictionaryAndNullablePropertyContext : YamlSerializerContext
        {
        }

        [YamlSerializable(typeof(ClassWithStringValues))]
        internal partial class ClassWithStringValuesContext : YamlSerializerContext
        {
        }

        [YamlSerializable(typeof(ClassWithDictionaryProperty))]
        internal partial class ClassWithDictionaryPropertyContext : YamlSerializerContext
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DoesNotReferenceInternalMembersFromOtherAssemblies()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/66679

            Assert.Equal(1, ContextForClassesFromAnotherAssembly.Default.ClassFromOtherAssemblyWithNonPublicMembers.Properties.Count);
            Assert.Equal("PublicValue", ContextForClassesFromAnotherAssembly.Default.ClassFromOtherAssemblyWithNonPublicMembers.Properties[0].Name);

            var value = new ClassFromOtherAssemblyWithNonPublicMembers();
            string yaml = YamlSerializer.Serialize(value, ContextForClassesFromAnotherAssembly.Default.ClassFromOtherAssemblyWithNonPublicMembers);
            Assert.Equal("""{"PublicValue":1}""", yaml);

            YamlSerializer.Deserialize(yaml, ContextForClassesFromAnotherAssembly.Default.ClassFromOtherAssemblyWithNonPublicMembers);
        }

        [YamlSerializable(typeof(ClassFromOtherAssemblyWithNonPublicMembers))]
        internal partial class ContextForClassesFromAnotherAssembly : YamlSerializerContext
        {
        }

        internal class ClassWithPocoListDictionaryAndNullable
        {
            public uint UIntProperty { get; set; }
            public List<TestPoco> ListOfPocoProperty { get; set; }
            public Dictionary<char, TestPoco> DictionaryPocoValueProperty { get; set; }
            public TestPoco? NullablePocoProperty { get; set; }
            public TestPoco PocoProperty { get; set; }
        }

        internal struct TestPoco
        {
            public string StringProperty { get; set; }
            public int IntProperty { get; set; }
        }

        internal class TestResolver : IYamlTypeInfoResolver
        {
            private Func<Type, YamlSerializerOptions, YamlTypeInfo?> _getTypeInfo;

            public TestResolver(Func<Type, YamlSerializerOptions, YamlTypeInfo?> getTypeInfo)
            {
                _getTypeInfo = getTypeInfo;
            }

            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options) => _getTypeInfo(type, options);
        }

        [Fact]
        [RequiresUnreferencedCode("Tests reflection-based YamlSerializer APIs.")]
        [RequiresDynamicCode("Tests reflection-based YamlSerializer APIs.")]
        [Trait("category", "jsonoutput")]
        public static void FastPathSerialization_EvaluatePropertyOnlyOnceWhenIgnoreNullOrDefaultIsSpecified()
        {
            YamlSerializerOptions options = FastPathSerializationContext.Default.Options;
            YamlTypeInfo<AllocatingOnPropertyAccess> allocatingOnPropertyAccessInfo = options.GetTypeInfo<AllocatingOnPropertyAccess>();
            Assert.NotNull(allocatingOnPropertyAccessInfo.SerializeHandler);

            var value = new AllocatingOnPropertyAccess();
            Assert.Equal(0, value.WhenWritingNullAccessCounter);
            Assert.Equal(0, value.WhenWritingDefaultAccessCounter);

            string expectedYaml = """{"SomeAllocatingProperty":"Current Value: 1","SomeAllocatingProperty2":"Current Value: 1"}""";
            Assert.Equal(expectedYaml, YamlSerializer.Serialize(value, options));
            Assert.Equal(1, value.WhenWritingNullAccessCounter);
            Assert.Equal(1, value.WhenWritingDefaultAccessCounter);
        }

        [Fact]
        public static void ContextWithInterpolatedAnnotations_WorksAsExpected()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/82997 and https://github.com/dotnet/runtime/issues/69207
            Assert.IsAssignableFrom<YamlTypeInfo<TestPoco>>(ContextWithInterpolatedAnnotations.Default.TestPocoSomeUniqueSuffixSuffix2);
        }

        [YamlSerializable(type: typeof(TestPoco), TypeInfoPropertyName = $"{nameof(TestPoco)}SomeUniqueSuffix" + "Suffix2")]
        internal partial class ContextWithInterpolatedAnnotations : YamlSerializerContext
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static async Task SupportsPreserveReferenceHandling()
        {
            ContextWithPreserveReference context = ContextWithPreserveReference.Default;
            var selfRef = new SelfReference();
            selfRef.Me = selfRef;

            string expectedYaml = """{"$id":"1","Me":{"$ref":"1"}}""";

            string yaml = YamlSerializer.Serialize(selfRef, context.SelfReference);
            Assert.Equal(expectedYaml, yaml);

            var stream = new Utf8MemoryStream();
            await YamlSerializer.SerializeAsync(stream, selfRef, typeof(SelfReference), context);
            Assert.Equal(expectedYaml, stream.AsString());

            SelfReference? deserialized = YamlSerializer.Deserialize(expectedYaml, context.SelfReference);
            Assert.NotNull(deserialized);
            Assert.Same(deserialized, deserialized.Me);
        }

        [YamlSourceGenerationOptions(
            ReferenceHandler = YamlKnownReferenceHandler.Preserve)]
        [YamlSerializable(typeof(SelfReference))]
        internal partial class ContextWithPreserveReference : YamlSerializerContext
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static async Task SupportsIgnoreCyclesReferenceHandling()
        {
            ContextWithIgnoreCyclesReferenceHandling context = ContextWithIgnoreCyclesReferenceHandling.Default;
            var selfRef = new SelfReference();
            selfRef.Me = selfRef;

            string expectedYaml = """{"Me":null}""";

            string yaml = YamlSerializer.Serialize(selfRef, context.SelfReference);
            Assert.Equal(expectedYaml, yaml);

            var stream = new Utf8MemoryStream();
            await YamlSerializer.SerializeAsync(stream, selfRef, typeof(SelfReference), context);
            Assert.Equal(expectedYaml, stream.AsString());

            string circularYaml = """{"$id":"1","Me":{"$ref":"1"}}""";
            SelfReference? deserialized = YamlSerializer.Deserialize(circularYaml, context.SelfReference);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Me);
            Assert.NotSame(deserialized, deserialized.Me);
            Assert.Null(deserialized.Me.Me);
        }

        [YamlSourceGenerationOptions(
            ReferenceHandler = YamlKnownReferenceHandler.IgnoreCycles)]
        [YamlSerializable(typeof(SelfReference))]
        internal partial class ContextWithIgnoreCyclesReferenceHandling : YamlSerializerContext
        {
        }

        [Fact]
        public static async Task SupportsUnspecifiedReferenceHandling()
        {
            ContextWithUnspecifiedReferenceHandling context = ContextWithUnspecifiedReferenceHandling.Default;
            var selfRef = new SelfReference();
            selfRef.Me = selfRef;

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(selfRef, context.SelfReference));

            var stream = new Utf8MemoryStream();
            await Assert.ThrowsAsync<YamlException>(async () => await YamlSerializer.SerializeAsync(stream, selfRef, typeof(SelfReference), context));

            string circularYaml = """{"$id":"1","Me":{"$ref":"1"}}""";
            SelfReference deserialized = YamlSerializer.Deserialize(circularYaml, context.SelfReference);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Me);
            Assert.NotSame(deserialized, deserialized.Me);
            Assert.Null(deserialized.Me.Me);
        }

        [YamlSourceGenerationOptions(
            ReferenceHandler = YamlKnownReferenceHandler.Unspecified)]
        [YamlSerializable(typeof(SelfReference))]
        internal partial class ContextWithUnspecifiedReferenceHandling : YamlSerializerContext
        {
        }

        internal class SelfReference
        {
            public SelfReference Me { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeIgnoreWhenWritingTest()
        {
            var p = new IgnoreWhenReadingWritingPerson
            {
                Id = 1,
                Name = "Alice",
                Description = "test"
            };
            var serializedYaml = YamlSerializer.Serialize(p, SerializeIgnoreReadingWritingYamlSerializerContext.Default.IgnoreWhenReadingWritingPerson);
            Assert.Equal("""{"Name":"Alice","Description":"test"}""", serializedYaml);
        }

        [Fact]
        public static void SerializeIgnoreWhenReadingTest()
        {
            var yaml = """{"Id":1,"Name":"Alice","Description":"test"}""";
            var deserialized = YamlSerializer.Deserialize<IgnoreWhenReadingWritingPerson>(yaml, SerializeIgnoreReadingWritingYamlSerializerContext.Default.IgnoreWhenReadingWritingPerson);
            Assert.NotNull(deserialized);
            Assert.Equal(1, deserialized.Id);
            Assert.Equal("Alice", deserialized.Name);
            Assert.Null(deserialized.Description);
        }

        internal sealed class IgnoreWhenReadingWritingPerson
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWriting)]
            public int Id { get; set; }

            public string Name { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenReading)]
            public string? Description { get; set; }
        }

        [YamlSerializable(typeof(IgnoreWhenReadingWritingPerson))]
        internal partial class SerializeIgnoreReadingWritingYamlSerializerContext : YamlSerializerContext
        {
        }

        [Fact]
        public static void SupportsDisallowDuplicateProperty()
        {
            YamlTypeInfo typeInfo =
                ContextWithAllowDuplicateProperties.Default.GetTypeInfo(typeof(Dictionary<string, int>));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""{"a":1,"a":2}""", typeInfo));
        }

        [YamlSourceGenerationOptions(AllowDuplicateProperties = false)]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        internal partial class ContextWithAllowDuplicateProperties : YamlSerializerContext
        {
        }

        // Test for https://github.com/dotnet/runtime/issues/99669
        // Verifies that partial contexts with [YamlSerializable] attributes on multiple declarations
        // work correctly at runtime - all types from all partial declarations should be available.
        [Fact]
        public static void PartialContextWithAttributesOnMultipleDeclarations_RuntimeBehavior()
        {
            // Verify both types from both partial declarations are available
            Assert.NotNull(MultiplePartialDeclarationsContext.Default.TypeFromPartial1);
            Assert.NotNull(MultiplePartialDeclarationsContext.Default.TypeFromPartial2);

            // Test serialization of type from first partial
            var obj1 = new TypeFromPartial1 { Id = 42, Name = "Test" };
            string yaml1 = YamlSerializer.Serialize(obj1, MultiplePartialDeclarationsContext.Default.TypeFromPartial1);
            Assert.Contains("42", yaml1);
            Assert.Contains("Test", yaml1);

            // Test deserialization of type from first partial
            var deserialized1 = YamlSerializer.Deserialize<TypeFromPartial1>(yaml1, MultiplePartialDeclarationsContext.Default.TypeFromPartial1);
            Assert.Equal(42, deserialized1.Id);
            Assert.Equal("Test", deserialized1.Name);

            // Test serialization of type from second partial
            var obj2 = new TypeFromPartial2 { Value = 3.14, IsActive = true };
            string yaml2 = YamlSerializer.Serialize(obj2, MultiplePartialDeclarationsContext.Default.TypeFromPartial2);
            Assert.Contains("3.14", yaml2);
            Assert.Contains("true", yaml2);

            // Test deserialization of type from second partial
            var deserialized2 = YamlSerializer.Deserialize<TypeFromPartial2>(yaml2, MultiplePartialDeclarationsContext.Default.TypeFromPartial2);
            Assert.Equal(3.14, deserialized2.Value);
            Assert.True(deserialized2.IsActive);
        }

        [Theory]
        [InlineData(42, "42")]
        [InlineData(0, "0")]
        [Trait("category", "jsonoutput")]
        public static void SupportsOpenGenericConverterOnGenericType_Int(int value, string expectedYaml)
        {
            Option<int> option = new Option<int>(value);
            string yaml = YamlSerializer.Serialize(option, OpenGenericConverterContext.Default.OptionInt32);
            Assert.Equal(expectedYaml, yaml);

            Option<int> deserialized = YamlSerializer.Deserialize<Option<int>>(yaml, OpenGenericConverterContext.Default.OptionInt32);
            Assert.True(deserialized.HasValue);
            Assert.Equal(value, deserialized.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsOpenGenericConverterOnGenericType_NullValue()
        {
            Option<int> option = default;
            string yaml = YamlSerializer.Serialize(option, OpenGenericConverterContext.Default.OptionInt32);
            Assert.Equal("null", yaml);

            Option<int> deserialized = YamlSerializer.Deserialize<Option<int>>("null", OpenGenericConverterContext.Default.OptionInt32);
            Assert.False(deserialized.HasValue);
        }

        [Theory]
        [InlineData("hello", @"""hello""")]
        [InlineData("", @"""""")]
        [Trait("category", "jsonoutput")]
        public static void SupportsOpenGenericConverterOnGenericType_String(string value, string expectedYaml)
        {
            Option<string> option = new Option<string>(value);
            string yaml = YamlSerializer.Serialize(option, OpenGenericConverterContext.Default.OptionString);
            Assert.Equal(expectedYaml, yaml);

            Option<string> deserialized = YamlSerializer.Deserialize<Option<string>>(yaml, OpenGenericConverterContext.Default.OptionString);
            Assert.True(deserialized.HasValue);
            Assert.Equal(value, deserialized.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsOpenGenericConverterOnProperty()
        {
            var obj = new ClassWithGenericConverterOnProperty { Value = new GenericWrapper<int>(42) };
            string yaml = YamlSerializer.Serialize(obj, OpenGenericConverterContext.Default.ClassWithGenericConverterOnProperty);
            Assert.Equal(@"{""Value"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<ClassWithGenericConverterOnProperty>(yaml, OpenGenericConverterContext.Default.ClassWithGenericConverterOnProperty);
            Assert.Equal(42, deserialized.Value.WrappedValue);
        }

        [YamlSerializable(typeof(Option<int>))]
        [YamlSerializable(typeof(Option<string>))]
        [YamlSerializable(typeof(ClassWithOptionProperty))]
        [YamlSerializable(typeof(ClassWithGenericConverterOnProperty))]
        internal partial class OpenGenericConverterContext : YamlSerializerContext
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsNestedGenericConverterOnGenericType()
        {
            var value = new TypeWithNestedConverter<int, string> { Value1 = 42, Value2 = "hello" };
            string yaml = YamlSerializer.Serialize(value, NestedGenericConverterContext.Default.TypeWithNestedConverterInt32String);
            Assert.Equal(@"{""Value1"":42,""Value2"":""hello""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithNestedConverter<int, string>>(yaml, NestedGenericConverterContext.Default.TypeWithNestedConverterInt32String);
            Assert.Equal(42, deserialized.Value1);
            Assert.Equal("hello", deserialized.Value2);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsConstrainedGenericConverterOnGenericType()
        {
            var value = new TypeWithSatisfiedConstraint<string> { Value = "test" };
            string yaml = YamlSerializer.Serialize(value, NestedGenericConverterContext.Default.TypeWithSatisfiedConstraintString);
            Assert.Equal(@"{""Value"":""test""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithSatisfiedConstraint<string>>(yaml, NestedGenericConverterContext.Default.TypeWithSatisfiedConstraintString);
            Assert.Equal("test", deserialized.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsGenericWithinNonGenericWithinGenericConverter()
        {
            var value = new TypeWithDeeplyNestedConverter<int, string> { Value1 = 99, Value2 = "deep" };
            string yaml = YamlSerializer.Serialize(value, NestedGenericConverterContext.Default.TypeWithDeeplyNestedConverterInt32String);
            Assert.Equal(@"{""Value1"":99,""Value2"":""deep""}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithDeeplyNestedConverter<int, string>>(yaml, NestedGenericConverterContext.Default.TypeWithDeeplyNestedConverterInt32String);
            Assert.Equal(99, deserialized.Value1);
            Assert.Equal("deep", deserialized.Value2);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsSingleGenericLevelNestedConverter()
        {
            var value = new TypeWithSingleLevelNestedConverter<int> { Value = 42 };
            string yaml = YamlSerializer.Serialize(value, NestedGenericConverterContext.Default.TypeWithSingleLevelNestedConverterInt32);
            Assert.Equal(@"{""Value"":42}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithSingleLevelNestedConverter<int>>(yaml, NestedGenericConverterContext.Default.TypeWithSingleLevelNestedConverterInt32);
            Assert.Equal(42, deserialized.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsAsymmetricNestedConverterWithManyParams()
        {
            var value = new TypeWithManyParams<int, string, bool, double, long>
            {
                Value1 = 1,
                Value2 = "two",
                Value3 = true,
                Value4 = 4.0,
                Value5 = 5L
            };
            string yaml = YamlSerializer.Serialize(value, NestedGenericConverterContext.Default.TypeWithManyParamsInt32StringBooleanDoubleInt64);
            Assert.Equal(@"{""Value1"":1,""Value2"":""two"",""Value3"":true,""Value4"":4,""Value5"":5}", yaml);

            var deserialized = YamlSerializer.Deserialize<TypeWithManyParams<int, string, bool, double, long>>(yaml, NestedGenericConverterContext.Default.TypeWithManyParamsInt32StringBooleanDoubleInt64);
            Assert.Equal(1, deserialized.Value1);
            Assert.Equal("two", deserialized.Value2);
            Assert.True(deserialized.Value3);
            Assert.Equal(4.0, deserialized.Value4);
            Assert.Equal(5L, deserialized.Value5);
        }

        [YamlSerializable(typeof(TypeWithNestedConverter<int, string>))]
        [YamlSerializable(typeof(TypeWithSatisfiedConstraint<string>))]
        [YamlSerializable(typeof(TypeWithDeeplyNestedConverter<int, string>))]
        [YamlSerializable(typeof(TypeWithSingleLevelNestedConverter<int>))]
        [YamlSerializable(typeof(TypeWithManyParams<int, string, bool, double, long>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(string))]
        [YamlSerializable(typeof(bool))]
        [YamlSerializable(typeof(double))]
        [YamlSerializable(typeof(long))]
        internal partial class NestedGenericConverterContext : YamlSerializerContext
        {
        }

        public class ClassWithMultipleByteArrayProperties
        {
            public byte[]? Data1 { get; set; }
            public byte[]? Data2 { get; set; }
            public byte[]? Data3 { get; set; }
        }

        [YamlSerializable(typeof(ClassWithMultipleByteArrayProperties))]
        internal partial class MultipleByteArrayContext : YamlSerializerContext
        {
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 }, null)]
        [InlineData(null, new byte[] { 4, 5, 6 }, new byte[] { 7, 8, 9 })]
        [InlineData(null, null, null)]
        [Trait("category", "jsonoutput")]
        public static void ClassWithMultipleByteArrayProperties_Roundtrip(byte[]? data1, byte[]? data2, byte[]? data3)
        {
            var value = new ClassWithMultipleByteArrayProperties
            {
                Data1 = data1,
                Data2 = data2,
                Data3 = data3,
            };

            string yaml = YamlSerializer.Serialize(value, MultipleByteArrayContext.Default.ClassWithMultipleByteArrayProperties);

            if (data1 is null) Assert.Contains("\"Data1\":null", yaml);
            if (data2 is null) Assert.Contains("\"Data2\":null", yaml);
            if (data3 is null) Assert.Contains("\"Data3\":null", yaml);

            ClassWithMultipleByteArrayProperties deserialized = YamlSerializer.Deserialize(yaml, MultipleByteArrayContext.Default.ClassWithMultipleByteArrayProperties);
            Assert.Equal(value.Data1, deserialized.Data1);
            Assert.Equal(value.Data2, deserialized.Data2);
            Assert.Equal(value.Data3, deserialized.Data3);
        }
    }
}
