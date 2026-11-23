// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class ReferenceHandlerTests_IgnoreCycles : SerializerTests
    {
        public ReferenceHandlerTests_IgnoreCycles(YamlSerializerWrapper stringSerializer)
            : base(stringSerializer)
        {
        }

        private static readonly YamlSerializerOptions s_optionsIgnoreCycles =
            new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles, DefaultBufferSize = 1 };

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnObject()
        {
            await Verify<NodeWithNodeProperty>();
            await Verify<NodeWithObjectProperty>();

            async Task Verify<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : class, new()
            {
                T root = new T();
                SetNextProperty(typeof(T), root, root);

                await Test_Serialize_And_SerializeAsync(root, """{"Next":null}""", s_optionsIgnoreCycles);

                // Verify that object property is not mutated on serialization.
                object rootNext = GetNextProperty(typeof(T), root);
                Assert.NotNull(rootNext);
                Assert.Same(rootNext, root);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnObject_AsProperty()
        {
            await Verify<NodeWithNodeProperty>();
            await Verify<NodeWithObjectProperty>();

            async Task Verify<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : class, new()
            {
                var node = new T();
                SetNextProperty(typeof(T), node, node);

                var root = new ClassWithGenericProperty<T>();
                root.Foo = node;
                await Test_Serialize_And_SerializeAsync(root, expected: """{"Foo":{"Next":null}}""", s_optionsIgnoreCycles);

                object nodeNext = GetNextProperty(typeof(T), node);
                Assert.NotNull(nodeNext);
                Assert.Same(nodeNext, node);

                var rootWithObjProperty = new ClassWithGenericProperty<object>();
                rootWithObjProperty.Foo = node;
                await Test_Serialize_And_SerializeAsync(rootWithObjProperty, expected: """{"Foo":{"Next":null}}""", s_optionsIgnoreCycles);

                nodeNext = GetNextProperty(typeof(T), node);
                Assert.NotNull(nodeNext);
                Assert.Same(nodeNext, node);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnBoxedValueType()
        {
            await Verify<ValueNodeWithIValueNodeProperty>();
            await Verify<ValueNodeWithObjectProperty>();

            async Task Verify<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : new()
            {
                object root = new T();
                SetNextProperty(typeof(T), root, root);
                await Test_Serialize_And_SerializeAsync(root, expected: """{"Next":null}""", s_optionsIgnoreCycles);

                object rootNext = GetNextProperty(typeof(T), root);
                Assert.NotNull(rootNext);
                Assert.Same(rootNext, root);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnBoxedValueType_Interface()
        {
            IValueNodeWithIValueNodeProperty root = new ValueNodeWithIValueNodeProperty();
            root.Next = root;
            await Test_Serialize_And_SerializeAsync(root, expected: """{"Next":null}""", s_optionsIgnoreCycles);

            IValueNodeWithObjectProperty root2 = new ValueNodeWithObjectProperty();
            root2.Next = root2;
            await Test_Serialize_And_SerializeAsync(root2, expected: """{"Next":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnBoxedValueType_AsProperty()
        {
            await Verify<ValueNodeWithIValueNodeProperty>();
            await Verify<ValueNodeWithObjectProperty>();

            async Task Verify<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : new()
            {
                object node = new T();
                SetNextProperty(typeof(T), node, node);

                var rootWithObjProperty = new ClassWithGenericProperty<object>();
                rootWithObjProperty.Foo = node;
                await Test_Serialize_And_SerializeAsync(rootWithObjProperty, expected: """{"Foo":{"Next":null}}""", s_optionsIgnoreCycles);

                object nodeNext = GetNextProperty(typeof(T), node);
                Assert.NotNull(nodeNext);
                Assert.Same(nodeNext, node);
            }
        }

        [Theory]
        [InlineData(typeof(Dictionary<string, object>))]
        [InlineData(typeof(GenericIDictionaryWrapper<string, object>))]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnDictionary([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type typeToSerialize)
        {
            var root = (IDictionary<string, object>)Activator.CreateInstance(typeToSerialize);
            root.Add("self", root);

            await Test_Serialize_And_SerializeAsync(root, """{"self":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnRecursiveDictionary()
        {
            var root = new RecursiveDictionary();
            root.Add("self", root);

            await Test_Serialize_And_SerializeAsync(root, """{"self":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnReadOnlyDictionary()
        {
            var innerDictionary = new Dictionary<string, object>();
            var root = new ReadOnlyDictionary<string, object>(innerDictionary);
            innerDictionary.Add("self", root);

            await Test_Serialize_And_SerializeAsync(root, """{"self":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnIDictionary()
        {
            var root = new WrapperForIDictionary();
            root.Add("self", root);

            await Test_Serialize_And_SerializeAsync(root, """{"self":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnArray()
        {
            var root = new object[1];
            root[0] = root;
            await Test_Serialize_And_SerializeAsync(root, "[null]", s_optionsIgnoreCycles);
        }

        [Theory]
        [InlineData(typeof(List<object>))]
        [InlineData(typeof(GenericIListWrapper<object>))]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnList([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type typeToSerialize)
        {
            var root = (IList<object>)Activator.CreateInstance(typeToSerialize);
            root.Add(root);
            await Test_Serialize_And_SerializeAsync(root, "[null]", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnRecursiveList()
        {
            var root = new RecursiveList();
            root.Add(root);
            await Test_Serialize_And_SerializeAsync(root, "[null]", s_optionsIgnoreCycles);
        }

        [Theory]
        [InlineData(typeof(GenericISetWrapper<object>))]
        [InlineData(typeof(GenericICollectionWrapper<object>))]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnCollections([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type typeToSerialize)
        {
            var root = (ICollection<object>)Activator.CreateInstance(typeToSerialize);
            root.Add(root);
            await Test_Serialize_And_SerializeAsync(root, "[null]", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnCollections_WithoutAddMethod()
        {
            var root = new Stack<object>();
            root.Push(root);
            await Test_Serialize_And_SerializeAsync(root, "[null]", s_optionsIgnoreCycles);

            var root2 = new Queue<object>();
            root2.Enqueue(root2);
            await Test_Serialize_And_SerializeAsync(root2, "[null]", s_optionsIgnoreCycles);

            var root3 = new ConcurrentStack<object>();
            root3.Push(root3);
            await Test_Serialize_And_SerializeAsync(root3, "[null]", s_optionsIgnoreCycles);

            var root4 = new ConcurrentQueue<object>();
            root4.Enqueue(root4);
            await Test_Serialize_And_SerializeAsync(root4, "[null]", s_optionsIgnoreCycles);

            var root5 = new Stack();
            root5.Push(root5);
            await Test_Serialize_And_SerializeAsync(root5, "[null]", s_optionsIgnoreCycles);

            var root6 = new Queue();
            root6.Enqueue(root6);
            await Test_Serialize_And_SerializeAsync(root6, "[null]", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_OnExtensionData()
        {
            var root = new EmptyClassWithExtensionProperty();
            root.MyOverflow.Add("root", root);
            await Test_Serialize_And_SerializeAsync(root, """{"root":null}""", s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_DoesNotSupportPreserveSemantics()
        {
            if (StreamingSerializer is null)
            {
                return;
            }

            // Object
            var node = new NodeWithExtensionData();
            node.Next = node;
            string yaml = await SerializeWithPreserve(node);

            node = await Serializer.DeserializeWrapper<NodeWithExtensionData>(yaml, s_optionsIgnoreCycles);
            Assert.True(node.MyOverflow.ContainsKey("$id"));
            Assert.True(node.Next.MyOverflow.ContainsKey("$ref"));

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            node = await StreamingSerializer.DeserializeWrapper<NodeWithExtensionData>(ms, s_optionsIgnoreCycles);
            Assert.True(node.MyOverflow.ContainsKey("$id"));
            Assert.True(node.Next.MyOverflow.ContainsKey("$ref"));

            // Dictionary
            var dictionary = new RecursiveDictionary();
            dictionary.Add("self", dictionary);
            yaml = await SerializeWithPreserve(dictionary);

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<RecursiveDictionary>(yaml, s_optionsIgnoreCycles));
            using var ms2 = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            await Assert.ThrowsAsync<YamlException>(() => StreamingSerializer.DeserializeWrapper<RecursiveDictionary>(ms2, s_optionsIgnoreCycles));

            // List
            var list = new RecursiveList();
            list.Add(list);
            yaml = await SerializeWithPreserve(list);

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<RecursiveList>(yaml, s_optionsIgnoreCycles));
            using var ms3 = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            await Assert.ThrowsAsync<YamlException>(() => StreamingSerializer.DeserializeWrapper<RecursiveList>(ms3, s_optionsIgnoreCycles));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_DoesNotSupportPreserveSemantics_Polymorphic()
        {
            if (StreamingSerializer is null)
            {
                return;
            }

            // Object
            var node = new NodeWithObjectProperty();
            node.Next = node;
            string yaml = await SerializeWithPreserve(node);

            node = await Serializer.DeserializeWrapper<NodeWithObjectProperty>(yaml, s_optionsIgnoreCycles);
            YamlElement nodeAsYamlElement = Assert.IsType<YamlElement>(node.Next);
            Assert.True(nodeAsYamlElement.GetProperty("$ref").GetString() == "1");

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            node = await StreamingSerializer.DeserializeWrapper<NodeWithObjectProperty>(ms, s_optionsIgnoreCycles);
            nodeAsYamlElement = Assert.IsType<YamlElement>(node.Next);
            Assert.True(nodeAsYamlElement.GetProperty("$ref").GetString() == "1");

            // Dictionary
            var dictionary = new Dictionary<string, object>();
            dictionary.Add("self", dictionary);
            yaml = await SerializeWithPreserve(dictionary);

            dictionary = await Serializer.DeserializeWrapper<Dictionary<string, object>>(yaml, s_optionsIgnoreCycles);
        }

        private async Task<string> SerializeWithPreserve<T>(T value)
        {
            var opts = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            return await Serializer.SerializeWrapper(value, opts);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AlreadySeenInstance_ShouldNotBeIgnoredOnSiblingBranch()
        {
            await Verify<EmptyClass>(expectedPayload: "{}");
            await Verify<EmptyStruct>(expectedPayload: "{}");
            await Verify<object>(expectedPayload: "{}");
            await Verify<Dictionary<string, object>>(expectedPayload: "{}");
            await Verify<List<string>>(expectedPayload: "[]");

            async Task Verify<T>(string expectedPayload) where T : new()
            {
                T value = new();
                var root = new TreeNode<T> { Left = value, Right = value };
                await Test_Serialize_And_SerializeAsync(root, $@"{{""Left"":{expectedPayload},""Right"":{expectedPayload}}}", s_optionsIgnoreCycles);

                var rootWithObjectProperties = new TreeNode<object> { Left = value, Right = value };
                await Test_Serialize_And_SerializeAsync(rootWithObjectProperties, $@"{{""Left"":{expectedPayload},""Right"":{expectedPayload}}}", s_optionsIgnoreCycles);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AlreadySeenInstance_ShouldNotBeIgnoredOnSiblingBranch_Converter()
        {
            var opts = new YamlSerializerOptions(s_optionsIgnoreCycles);
            // This converter turns the object into a string.
            opts.Converters.Add(new PersonConverter());

            Person person = new() { Name = "John" };

            await Test_Serialize_And_SerializeAsync(new PersonHolder { Person1 = person, Person2 = person },
                expected: """{"Person1":"John","Person2":"John"}""", opts);

            await Test_Serialize_And_SerializeAsync(new BoxedPersonHolder { Person1 = person, Person2 = person },
                expected: """{"Person1":"John","Person2":"John"}""", opts);

            await Test_Serialize_And_SerializeAsync(new List<Person> { person, person },
                expected: """["John","John"]""", opts);

            await Test_Serialize_And_SerializeAsync(new List<object> { person, person },
                expected: """["John","John"]""", opts);
        }

        public class PersonHolder
        {
            public Person Person1 { get; set; }
            public Person Person2 { get; set; }
        }

        public class BoxedPersonHolder
        {
            public object Person1 { get; set; }
            public object Person2 { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_WhenWritingNull()
        {
            var opts = new YamlSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull
            };

            // Reference cycles are treated as null, hence the YamlIgnoreCondition can be used to actually ignore the property.
            var rootObj = new NodeWithObjectProperty();
            rootObj.Next = rootObj;

            await Test_Serialize_And_SerializeAsync(rootObj, "{}", opts);

            // YamlIgnoreCondition does not ignore nulls in collections, hence a reference loop should not omit writing it.
            // This also helps us to avoid changing the length of a collection when the loop is detected in one of the elements.
            var rootList = new List<object>();
            rootList.Add(rootList);

            await Test_Serialize_And_SerializeAsync(rootList, "[null]", opts);

            var rootDictionary = new Dictionary<string, object>();
            rootDictionary.Add("self", rootDictionary);

            await Test_Serialize_And_SerializeAsync(rootDictionary, """{"self":null}""", opts);
        }

        [Fact] // https://github.com/dotnet/runtime/issues/51837
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_StringShouldNotBeIgnored()
        {
            var stringReference = "John";
            
            var root = new Person
            {
                Name = stringReference,
                Parent = new Person
                {
                    Name = stringReference,
                }
            };

            await Test_Serialize_And_SerializeAsync_Contains(root, expectedSubstring: """
                "Name":"John"
                """, expectedTimes: 2, s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_BoxedValueShouldNotBeIgnored()
        {
            object dayOfBirthAsObject = 15;


            var root = new Person
            {
                Name = "Anna",
                DayOfBirth = dayOfBirthAsObject,
                Parent = new Person
                {
                    Name = "John",
                    DayOfBirth = dayOfBirthAsObject
                }
            };

            await Test_Serialize_And_SerializeAsync_Contains(root, expectedSubstring: """
                "DayOfBirth":15
                """, expectedTimes: 2, s_optionsIgnoreCycles);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IgnoreCycles_DerivedType_InArray()
        {
            var worker = new OfficeWorker
            {
                Office = new Office
                {
                    Dummy = new()
                }
            };

            worker.Office.Staff = [worker, new RemoteWorker()];

            await Test_Serialize_And_SerializeAsync(worker, """{"Office":{"Staff":[null,{"$type":"remote"}],"Dummy":{}}}""", s_optionsIgnoreCycles);

            worker.Office.Staff = [worker];

            await Test_Serialize_And_SerializeAsync(worker, """{"Office":{"Staff":[null],"Dummy":{}}}""", s_optionsIgnoreCycles);
        }

        [YamlDerivedType(typeof(OfficeWorker), "office")]
        [YamlDerivedType(typeof(RemoteWorker), "remote")]
        public abstract class EmployeeLocation
        {
        }

        public class OfficeWorker : EmployeeLocation
        {
            public Office Office { get; set; }
        }

        public class RemoteWorker : EmployeeLocation
        {
        }

        public class Office
        {
            public EmployeeLocation[] Staff { get; set; }

            public EmptyClass Dummy { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CycleDetectionStatePersistsAcrossContinuations()
        {
            string expectedValueYaml = """{"LargePropertyName":"A large-ish string to force continuations","Nested":null}""";
            var recVal = new RecursiveValue { LargePropertyName = "A large-ish string to force continuations" };
            recVal.Nested = recVal;

            var value = new List<RecursiveValue> { recVal, recVal };
            string expectedYaml = $"[{expectedValueYaml},{expectedValueYaml}]";

            await Test_Serialize_And_SerializeAsync(value, expectedYaml, s_optionsIgnoreCycles);
        }

        public class RecursiveValue
        {
            public string LargePropertyName { get; set; }
            public RecursiveValue? Nested { get; set; }
        }

        private async Task Test_Serialize_And_SerializeAsync<T>(T obj, string expected, YamlSerializerOptions options)
        {
            string yaml;
            Type objType = typeof(T);

            if (objType != typeof(object))
            {
                yaml = await Serializer.SerializeWrapper(obj, options);
                Assert.Equal(expected, yaml);
            }

            yaml = await Serializer.SerializeWrapper(obj, objType, options);
            Assert.Equal(expected, yaml);
        }

        private async Task Test_Serialize_And_SerializeAsync_Contains<T>(T obj, string expectedSubstring, int expectedTimes, YamlSerializerOptions options)
        {
            string yaml;
            Type objType = typeof(T);

            if (objType != typeof(object))
            {
                yaml = await Serializer.SerializeWrapper(obj, options);
                VerifySubstringExistsNTimes(yaml, expectedSubstring, expectedTimes);
            }

            yaml = await Serializer.SerializeWrapper(obj, objType, options);
            VerifySubstringExistsNTimes(yaml, expectedSubstring, expectedTimes);

            static void VerifySubstringExistsNTimes(string actualString, string expectedSubstring, int expectedTimes)
            {
                int actualTimes = actualString.Split(new[] { expectedSubstring }, StringSplitOptions.None).Length - 1;
                Assert.Equal(expectedTimes, actualTimes);
            }
        }

        private const string Next = nameof(Next);
        private void SetNextProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, object obj, object value)
        {
            type.GetProperty(Next).SetValue(obj, value);
        }

        private object GetNextProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, object obj)
        {
            return type.GetProperty(Next).GetValue(obj);
        }

        public class NodeWithObjectProperty
        {
            public object? Next { get; set; }
        }

        public class NodeWithNodeProperty
        {
            public NodeWithNodeProperty? Next { get; set; }
        }

        public class ClassWithGenericProperty<T>
        {
            public T Foo { get; set; }
        }

        public class TreeNode<T>
        {
            public T Left { get; set; }
            public T Right { get; set; }
        }

        public interface IValueNodeWithObjectProperty
        {
            public object? Next { get; set; }
        }

        public struct ValueNodeWithObjectProperty : IValueNodeWithObjectProperty
        {
            public object? Next { get; set; }
        }

        public interface IValueNodeWithIValueNodeProperty
        {
            public IValueNodeWithIValueNodeProperty? Next { get; set; }
        }

        public struct ValueNodeWithIValueNodeProperty : IValueNodeWithIValueNodeProperty
        {
            public IValueNodeWithIValueNodeProperty? Next { get; set; }
        }

        public class EmptyClass { }
        public struct EmptyStruct { }

        public class EmptyClassWithExtensionProperty
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; } = new Dictionary<string, object>();
        }

        public class NodeWithExtensionData
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; } = new Dictionary<string, object>();
            public NodeWithExtensionData Next { get; set; }
        }

        public class RecursiveDictionary : Dictionary<string, RecursiveDictionary> { }

        public class RecursiveList : List<RecursiveList> { }

        public class Person
        {
            public string Name { get; set; }
            public object? DayOfBirth { get; set; }
            public Person? Parent { get; set; }
        }

        class PersonConverter : YamlConverter<Person>
        {
            public override Person? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, Person value, YamlSerializerOptions options)
                => writer.WriteStringValue(value.Name);
        }
    }
}
