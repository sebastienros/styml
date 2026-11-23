// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class PolymorphicTests : SerializerTests
    {
        public PolymorphicTests(YamlSerializerWrapper serializer) : base(serializer)
        {
        }

        [Theory]
        [InlineData(1, "1")]
        [InlineData("stringValue", """
            "stringValue"
            """)]
        [InlineData(true, "true")]
        [InlineData(null, "null")]
        [InlineData(new int[] { 1, 2, 3}, "[1,2,3]")]
        [Trait("category", "jsonoutput")]
        public async Task PrimitivesAsRootObject(object? value, string expectedYaml)
        {
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal(expectedYaml, yaml);
            yaml = await Serializer.SerializeWrapper(value, typeof(object));
            Assert.Equal(expectedYaml, yaml);

            YamlTypeInfo<object> objectTypeInfo = Serializer.DefaultOptions.GetTypeInfo<object>();
            yaml = await Serializer.SerializeWrapper(value, objectTypeInfo);
            Assert.Equal(expectedYaml, yaml);
        }

        [Fact]
        public async Task ParseUntyped()
        {
            object obj = await Serializer.DeserializeWrapper<object>("""
                "hello"
                """);
            Assert.IsType<YamlElement>(obj);
            YamlElement element = (YamlElement)obj;
            Assert.Equal(YamlValueKind.String, element.ValueKind);
            Assert.Equal("hello", element.GetString());

            obj = await Serializer.DeserializeWrapper<object>(@"true");
            element = (YamlElement)obj;
            Assert.Equal(YamlValueKind.True, element.ValueKind);
            Assert.True(element.GetBoolean());

            obj = await Serializer.DeserializeWrapper<object>(@"null");
            Assert.Null(obj);

            obj = await Serializer.DeserializeWrapper<object>("[]");
            element = (YamlElement)obj;
            Assert.Equal(YamlValueKind.Array, element.ValueKind);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ArrayAsRootObject()
        {
            const string ExpectedYaml = """[1,true,{"City":"MyCity"},null,"foo"]""";
            const string ReversedExpectedYaml = """["foo",null,{"City":"MyCity"},true,1]""";

            string[] expectedObjects = { """
                "foo"
                """, @"null", """{"City":"MyCity"}""", @"true", @"1" };

            var address = new Address();
            address.Initialize();

            var array = new object[] { 1, true, address, null, "foo" };
            string yaml = await Serializer.SerializeWrapper(array);
            Assert.Equal(ExpectedYaml, yaml);

            var dictionary = new Dictionary<string, string> { { "City", "MyCity" } };
            var arrayWithDictionary = new object[] { 1, true, dictionary, null, "foo" };
            yaml = await Serializer.SerializeWrapper(arrayWithDictionary);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(array);
            Assert.Equal(ExpectedYaml, yaml);

            List<object> list = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(list);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(list);
            Assert.Equal(ExpectedYaml, yaml);

            IEnumerable ienumerable = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(ienumerable);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(ienumerable);
            Assert.Equal(ExpectedYaml, yaml);

            IList ilist = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(ilist);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(ilist);
            Assert.Equal(ExpectedYaml, yaml);

            ICollection icollection = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(icollection);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(icollection);
            Assert.Equal(ExpectedYaml, yaml);

            IEnumerable<object> genericIEnumerable = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(genericIEnumerable);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(genericIEnumerable);
            Assert.Equal(ExpectedYaml, yaml);

            IList<object> genericIList = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(genericIList);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(genericIList);
            Assert.Equal(ExpectedYaml, yaml);

            ICollection<object> genericICollection = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(genericICollection);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(genericICollection);
            Assert.Equal(ExpectedYaml, yaml);

            IReadOnlyCollection<object> genericIReadOnlyCollection = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(genericIReadOnlyCollection);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(genericIReadOnlyCollection);
            Assert.Equal(ExpectedYaml, yaml);

            IReadOnlyList<object> genericIReadonlyList = new List<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(genericIReadonlyList);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(genericIReadonlyList);
            Assert.Equal(ExpectedYaml, yaml);

            ISet<object> iset = new HashSet<object> { 1, true, address, null, "foo" };
            yaml = await Serializer.SerializeWrapper(iset);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(iset);
            Assert.Equal(ExpectedYaml, yaml);

            Stack<object> stack = new Stack<object>(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(stack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(stack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            Queue<object> queue = new Queue<object>(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(queue);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(queue);
            Assert.Equal(ExpectedYaml, yaml);

            HashSet<object> hashset = new HashSet<object>(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(hashset);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(hashset);
            Assert.Equal(ExpectedYaml, yaml);

            LinkedList<object> linkedlist = new LinkedList<object>(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(linkedlist);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(linkedlist);
            Assert.Equal(ExpectedYaml, yaml);

            ImmutableArray<object> immutablearray = ImmutableArray.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(immutablearray);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(immutablearray);
            Assert.Equal(ExpectedYaml, yaml);

            IImmutableList<object> iimmutablelist = ImmutableList.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(iimmutablelist);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(iimmutablelist);
            Assert.Equal(ExpectedYaml, yaml);

            IImmutableStack<object> iimmutablestack = ImmutableStack.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(iimmutablestack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(iimmutablestack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            IImmutableQueue<object> iimmutablequeue = ImmutableQueue.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(iimmutablequeue);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(iimmutablequeue);
            Assert.Equal(ExpectedYaml, yaml);

            IImmutableSet<object> iimmutableset = ImmutableHashSet.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(iimmutableset);
            foreach (string obj in expectedObjects)
            {
                Assert.Contains(obj, yaml);
            }

            yaml = await Serializer.SerializeWrapper<object>(iimmutableset);
            foreach (string obj in expectedObjects)
            {
                Assert.Contains(obj, yaml);
            }

            ImmutableHashSet<object> immutablehashset = ImmutableHashSet.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(immutablehashset);
            foreach (string obj in expectedObjects)
            {
                Assert.Contains(obj, yaml);
            }

            yaml = await Serializer.SerializeWrapper<object>(immutablehashset);
            foreach (string obj in expectedObjects)
            {
                Assert.Contains(obj, yaml);
            }

            ImmutableList<object> immutablelist = ImmutableList.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(immutablelist);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(immutablelist);
            Assert.Equal(ExpectedYaml, yaml);

            ImmutableStack<object> immutablestack = ImmutableStack.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(immutablestack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(immutablestack);
            Assert.Equal(ReversedExpectedYaml, yaml);

            ImmutableQueue<object> immutablequeue = ImmutableQueue.CreateRange(new List<object> { 1, true, address, null, "foo" });
            yaml = await Serializer.SerializeWrapper(immutablequeue);
            Assert.Equal(ExpectedYaml, yaml);

            yaml = await Serializer.SerializeWrapper<object>(immutablequeue);
            Assert.Equal(ExpectedYaml, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SimpleTestClassAsRootObject()
        {
            // Sanity checks on test type.
            Assert.Equal(typeof(object), typeof(SimpleTestClassWithObject).GetProperty("MyInt16").PropertyType);
            Assert.Equal(typeof(object), typeof(SimpleTestClassWithObject).GetProperty("MyBooleanTrue").PropertyType);
            Assert.Equal(typeof(object), typeof(SimpleTestClassWithObject).GetProperty("MyInt16Array").PropertyType);

            var obj = new SimpleTestClassWithObject();
            obj.Initialize();

            // Verify with actual type.
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "MyInt16":1
                """, yaml);
            Assert.Contains("""
                "MyBooleanTrue":true
                """, yaml);
            Assert.Contains("""
                "MyInt16Array":[1]
                """, yaml);

            // Verify with object type.
            yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Contains("""
                "MyInt16":1
                """, yaml);
            Assert.Contains("""
                "MyBooleanTrue":true
                """, yaml);
            Assert.Contains("""
                "MyInt16Array":[1]
                """, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NestedObjectAsRootObject()
        {
            void Verify(string yaml)
            {
                Assert.Contains("""
                    "Address":{"City":"MyCity"}
                    """, yaml);
                Assert.Contains("""
                    "List":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "Array":["Hello","Again"]
                    """, yaml);
                Assert.Contains("""
                    "IEnumerable":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IList":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ICollection":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IEnumerableT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IListT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ICollectionT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IReadOnlyCollectionT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IReadOnlyListT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ISetT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IReadOnlySetT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "StackT":["World","Hello"]
                    """, yaml);
                Assert.Contains("""
                    "QueueT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "HashSetT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "LinkedListT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "SortedSetT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ImmutableArrayT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IImmutableListT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "IImmutableStackT":["World","Hello"]
                    """, yaml);
                Assert.Contains("""
                    "IImmutableQueueT":["Hello","World"]
                    """, yaml);
                Assert.True(yaml.Contains("""
                    "IImmutableSetT":["Hello","World"]
                    """) || yaml.Contains("""
                    "IImmutableSetT":["World","Hello"]
                    """));
                Assert.True(yaml.Contains("""
                    "ImmutableHashSetT":["Hello","World"]
                    """) || yaml.Contains("""
                    "ImmutableHashSetT":["World","Hello"]
                    """));
                Assert.Contains("""
                    "ImmutableListT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ImmutableStackT":["World","Hello"]
                    """, yaml);
                Assert.Contains("""
                    "ImmutableQueueT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "ImmutableSortedSetT":["Hello","World"]
                    """, yaml);
                Assert.Contains("""
                    "NullableInt":42
                    """, yaml);
                Assert.Contains("""
                    "Object":{}
                    """, yaml);
                Assert.Contains("""
                    "NullableIntArray":[null,42,null]
                    """, yaml);
            }

            // Sanity checks on test type.
            Assert.Equal(typeof(object), typeof(ObjectWithObjectProperties).GetProperty("Address").PropertyType);
            Assert.Equal(typeof(object), typeof(ObjectWithObjectProperties).GetProperty("List").PropertyType);
            Assert.Equal(typeof(object), typeof(ObjectWithObjectProperties).GetProperty("Array").PropertyType);
            Assert.Equal(typeof(object), typeof(ObjectWithObjectProperties).GetProperty("NullableInt").PropertyType);
            Assert.Equal(typeof(object), typeof(ObjectWithObjectProperties).GetProperty("NullableIntArray").PropertyType);

            var obj = new ObjectWithObjectProperties();

            string yaml = await Serializer.SerializeWrapper(obj);
            Verify(yaml);

            yaml = await Serializer.SerializeWrapper<object>(obj);
            Verify(yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NestedObjectAsRootObjectIgnoreNullable()
        {
            // Ensure that null properties are properly written and support ignore.
            var obj = new ObjectWithObjectProperties();
            obj.NullableInt = null;

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "NullableInt":null
                """, yaml);

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull;
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.DoesNotContain("""
                "NullableInt":null
                """, yaml);
        }

        [Fact]
        public async Task StaticAnalysisBaseline()
        {
            Customer customer = new Customer();
            customer.Initialize();
            customer.Verify();

            string yaml = await Serializer.SerializeWrapper(customer);
            Customer deserializedCustomer = await Serializer.DeserializeWrapper<Customer>(yaml);
            deserializedCustomer.Verify();
        }

        [Fact]
        public async Task StaticAnalysis()
        {
            Customer customer = new Customer();
            customer.Initialize();
            customer.Verify();

            Person person = customer;

            // Generic inference used <TValue> = <Person>
            string yaml = await Serializer.SerializeWrapper(person);

            Customer deserializedCustomer = await Serializer.DeserializeWrapper<Customer>(yaml);

            // We only serialized the Person base class, so the Customer fields should be default.
            Assert.Equal(typeof(Customer), deserializedCustomer.GetType());
            Assert.Equal(0, deserializedCustomer.CreditLimit);
            ((Person)deserializedCustomer).VerifyNonVirtual();
        }

        [Fact]
        public async Task WriteStringWithRuntimeType()
        {
            Customer customer = new Customer();
            customer.Initialize();
            customer.Verify();

            Person person = customer;

            string yaml = await Serializer.SerializeWrapper(person, person.GetType());

            Customer deserializedCustomer = await Serializer.DeserializeWrapper<Customer>(yaml);

            // We serialized the Customer
            Assert.Equal(typeof(Customer), deserializedCustomer.GetType());
            deserializedCustomer.Verify();
        }

        [Fact]
        public async Task StaticAnalysisWithRelationship()
        {
            UsaCustomer usaCustomer = new UsaCustomer();
            usaCustomer.Initialize();
            usaCustomer.Verify();

            // Note: this could be typeof(UsaAddress) if we preserve objects created in the ctor. Currently we only preserve IEnumerables.
            Assert.Equal(typeof(Address), usaCustomer.Address.GetType());

            Customer customer = usaCustomer;

            // Generic inference used <TValue> = <Customer>
            string yaml = await Serializer.SerializeWrapper(customer);

            UsaCustomer deserializedCustomer = await Serializer.DeserializeWrapper<UsaCustomer>(yaml);

            // We only serialized the Customer base class
            Assert.Equal(typeof(UsaCustomer), deserializedCustomer.GetType());
            Assert.Equal(typeof(Address), deserializedCustomer.Address.GetType());
            ((Customer)deserializedCustomer).VerifyNonVirtual();
        }

        [Fact]
        public async Task PolymorphicInterface_NotSupported()
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<MyClass>("""{ "Value": "A value", "Thing": { "Number": 123 } }"""));
        }

        [Fact]
        public async Task GenericListOfInterface_WithInvalidYaml_ThrowsYamlException()
        {
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<MyThingCollection>("false"));
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<MyThingCollection>("{}"));
        }

        [Fact]
        public async Task GenericListOfInterface_WithValidYaml_ThrowsNotSupportedException()
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<MyThingCollection>("[{}]"));
        }

        [Fact]
        public async Task GenericDictionaryOfInterface_WithInvalidYaml_ThrowsYamlException()
        {
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<MyThingDictionary>("""{"":1}"""));
        }

        [Fact]
        public async Task GenericDictionaryOfInterface_WithValidYaml_ThrowsNotSupportedException()
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<MyThingDictionary>("""{"":{}}"""));
        }

        [ConditionalFact]
        [Trait("category", "jsonoutput")]
        public async Task AnonymousType()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                throw new SkipTestException("Anonymous types are unspeakable and cannot be registered with the source generator.");
            }

            const string Expected = """{"x":1,"y":true}""";
            var value = new { x = 1, y = true };

            // Strongly-typed.
            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal(Expected, yaml);

            // Boxed.
            object objValue = value;
            yaml = await Serializer.SerializeWrapper(objValue);
            Assert.Equal(Expected, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomResolverWithFailingAncestorType_DoesNotSurfaceException()
        {
            YamlSerializerOptions options = Serializer.GetDefaultOptionsWithMetadataModifier(static typeInfo =>
            {
                if (typeInfo.Type == typeof(MyThing) ||
                    typeInfo.Type == typeof(IList))
                {
                    throw new InvalidOperationException("some latent custom resolution bug");
                }
            });

            object value = new MyDerivedThing { Number = 42 };
            string yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("""{"Number":42}""", yaml);

            value = new int[] { 1, 2, 3 };
            yaml = await Serializer.SerializeWrapper(value, options);
            Assert.Equal("[1,2,3]", yaml);
        }

        internal class MyClass
        {
            public string Value { get; set; }
            public IThing Thing { get; set; }
        }

        internal interface IThing
        {
            int Number { get; set; }
        }

        internal class MyThing : IThing
        {
            public int Number { get; set; }
        }

        internal class MyDerivedThing : MyThing
        {
        }

        internal class MyThingCollection : List<IThing> { }

        internal class MyThingDictionary : Dictionary<string, IThing> { }

        [Theory]
        [InlineData(typeof(PolymorphicTypeWithConflictingPropertyNameAtBase), typeof(PolymorphicTypeWithConflictingPropertyNameAtBase.Derived))]
        [InlineData(typeof(PolymorphicTypeWithConflictingPropertyNameAtDerived), typeof(PolymorphicTypeWithConflictingPropertyNameAtDerived.Derived))]
        public async Task PolymorphicTypesWithConflictingPropertyNames_ThrowsInvalidOperationException(Type baseType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type derivedType)
        {
            InvalidOperationException ex;
            object value = Activator.CreateInstance(derivedType);

            ex = Assert.Throws<InvalidOperationException>(() => Serializer.GetTypeInfo(baseType));
            ValidateException(ex);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.SerializeWrapper(value, baseType));
            ValidateException(ex);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.DeserializeWrapper("{}", baseType));
            ValidateException(ex);

            void ValidateException(InvalidOperationException ex)
            {
                Assert.Contains($"The type '{derivedType}' contains property 'Type' that conflicts with an existing metadata property name.", ex.Message);
            }
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "Type")]
        [YamlDerivedType(typeof(Derived), nameof(Derived))]
        public abstract class PolymorphicTypeWithConflictingPropertyNameAtBase
        {
            public string Type { get; set; }

            public class Derived : PolymorphicTypeWithConflictingPropertyNameAtBase
            {
                public string Name { get; set; }
            }
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "Type")]
        [YamlDerivedType(typeof(Derived), nameof(Derived))]
        public abstract class PolymorphicTypeWithConflictingPropertyNameAtDerived
        {
            public class Derived : PolymorphicTypeWithConflictingPropertyNameAtDerived
            {
                public string Type { get; set; }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PolymorphicTypeWithIgnoredConflictingPropertyName_Supported()
        {
            PolymorphicTypeWithIgnoredConflictingPropertyName value = new PolymorphicTypeWithIgnoredConflictingPropertyName.Derived();

            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(PolymorphicTypeWithIgnoredConflictingPropertyName));
            Assert.NotNull(typeInfo);

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"Type":"Derived"}""", yaml);

            value = await Serializer.DeserializeWrapper<PolymorphicTypeWithIgnoredConflictingPropertyName>(yaml);
            Assert.IsType<PolymorphicTypeWithIgnoredConflictingPropertyName.Derived>(value);
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "Type")]
        [YamlDerivedType(typeof(Derived), nameof(Derived))]
        public abstract class PolymorphicTypeWithIgnoredConflictingPropertyName
        {
            [YamlIgnore]
            public string Type { get; set; }

            public class Derived : PolymorphicTypeWithIgnoredConflictingPropertyName;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task PolymorphicTypeWithExtensionDataConflictingPropertyName_Supported()
        {
            PolymorphicTypeWithExtensionDataConflictingPropertyName value = new PolymorphicTypeWithExtensionDataConflictingPropertyName.Derived();

            YamlTypeInfo typeInfo = Serializer.GetTypeInfo(typeof(PolymorphicTypeWithExtensionDataConflictingPropertyName));
            Assert.NotNull(typeInfo);

            string yaml = await Serializer.SerializeWrapper(value);
            Assert.Equal("""{"Type":"Derived"}""", yaml);

            value = await Serializer.DeserializeWrapper<PolymorphicTypeWithExtensionDataConflictingPropertyName>("""{"Type":"Derived","extraProp":null}""");
            Assert.IsType<PolymorphicTypeWithExtensionDataConflictingPropertyName.Derived>(value);
            Assert.Equal(1, value.Type.Count);
            Assert.Contains("extraProp", value.Type);
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "Type")]
        [YamlDerivedType(typeof(Derived), nameof(Derived))]
        public abstract class PolymorphicTypeWithExtensionDataConflictingPropertyName
        {
            [YamlExtensionData]
            public YamlObject Type { get; set; }

            public class Derived : PolymorphicTypeWithExtensionDataConflictingPropertyName;
        }
    }
}
