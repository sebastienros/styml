// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed class ReferenceHandlerTests_IgnoreCycles_Metadata_String : ReferenceHandlerTests_IgnoreCycles_Metadata
    {
        public ReferenceHandlerTests_IgnoreCycles_Metadata_String()
            : base(new StringSerializerWrapper(ReferenceHandlerTests_IgnoreCyclesContext_Metadata.Default))
        {
        }
    }

    public sealed class ReferenceHandlerTests_IgnoreCycles_Metadata_AsyncStream : ReferenceHandlerTests_IgnoreCycles_Metadata
    {
        public ReferenceHandlerTests_IgnoreCycles_Metadata_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(ReferenceHandlerTests_IgnoreCyclesContext_Metadata.Default))
        {
        }
    }

    public abstract partial class ReferenceHandlerTests_IgnoreCycles_Metadata : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTests_IgnoreCycles_Metadata(YamlSerializerWrapper serializer)
            : base(serializer)
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(GenericIDictionaryWrapper<string, object>))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(IList<object>))]
        [YamlSerializable(typeof(GenericIListWrapper<object>))]
        [YamlSerializable(typeof(GenericISetWrapper<object>))]
        [YamlSerializable(typeof(GenericICollectionWrapper<object>))]
        [YamlSerializable(typeof(NodeWithExtensionData))]
        [YamlSerializable(typeof(RecursiveDictionary))]
        [YamlSerializable(typeof(RecursiveList))]
        [YamlSerializable(typeof(NodeWithObjectProperty))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(ReadOnlyDictionary<string, object>))]
        [YamlSerializable(typeof(WrapperForIDictionary))]
        [YamlSerializable(typeof(IDictionary<string, object>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<int>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<Employee>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<List<int>>))]
        [YamlSerializable(typeof(object[]))]
        [YamlSerializable(typeof(ReferenceHandlerTests.IBoxedStructWithObjectProperty))]
        [YamlSerializable(typeof(ValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(ValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(NodeWithNodeProperty))]
        [YamlSerializable(typeof(NodeWithObjectProperty))]
        [YamlSerializable(typeof(EmptyClassWithExtensionProperty))]
        [YamlSerializable(typeof(Person))]
        [YamlSerializable(typeof(ICollection<object>))]
        [YamlSerializable(typeof(Stack<object>))]
        [YamlSerializable(typeof(Queue<object>))]
        [YamlSerializable(typeof(ConcurrentStack<object>))]
        [YamlSerializable(typeof(ConcurrentQueue<object>))]
        [YamlSerializable(typeof(Stack))]
        [YamlSerializable(typeof(Queue))]
        [YamlSerializable(typeof(IValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(ValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(IValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(ValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(EmptyClass))]
        [YamlSerializable(typeof(EmptyStruct))]
        [YamlSerializable(typeof(List<string>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<object>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<NodeWithNodeProperty>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<NodeWithObjectProperty>))]
        [YamlSerializable(typeof(List<Person>))]
        [YamlSerializable(typeof(RecursiveValue))]
        [YamlSerializable(typeof(List<RecursiveValue>))]
        [YamlSerializable(typeof(PersonHolder))]
        [YamlSerializable(typeof(BoxedPersonHolder))]
        [YamlSerializable(typeof(TreeNode<EmptyClass>))]
        [YamlSerializable(typeof(TreeNode<EmptyStruct>))]
        [YamlSerializable(typeof(TreeNode<object>))]
        [YamlSerializable(typeof(TreeNode<Dictionary<string, object>>))]
        [YamlSerializable(typeof(TreeNode<List<string>>))]
        [YamlSerializable(typeof(TreeNode<object>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(EmployeeLocation))]
        [YamlSerializable(typeof(EmployeeLocation[]))]
        [YamlSerializable(typeof(OfficeWorker))]
        [YamlSerializable(typeof(Office))]
        [YamlSerializable(typeof(RemoteWorker))]
        internal sealed partial class ReferenceHandlerTests_IgnoreCyclesContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed class ReferenceHandlerTests_IgnoreCycles_Default_String : ReferenceHandlerTests_IgnoreCycles_Default
    {
        public ReferenceHandlerTests_IgnoreCycles_Default_String()
            : base(new StringSerializerWrapper(ReferenceHandlerTests_IgnoreCyclesContext_Default.Default))
        {
        }
    }

    public sealed class ReferenceHandlerTests_IgnoreCycles_Default_AsyncStream : ReferenceHandlerTests_IgnoreCycles_Default
    {
        public ReferenceHandlerTests_IgnoreCycles_Default_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(ReferenceHandlerTests_IgnoreCyclesContext_Default.Default))
        {
        }
    }

    public abstract partial class ReferenceHandlerTests_IgnoreCycles_Default : ReferenceHandlerTests_IgnoreCycles
    {
        public ReferenceHandlerTests_IgnoreCycles_Default(YamlSerializerWrapper serializer)
            : base(serializer)
        {
        }

        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(GenericIDictionaryWrapper<string, object>))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(IList<object>))]
        [YamlSerializable(typeof(GenericIListWrapper<object>))]
        [YamlSerializable(typeof(GenericISetWrapper<object>))]
        [YamlSerializable(typeof(GenericICollectionWrapper<object>))]
        [YamlSerializable(typeof(NodeWithExtensionData))]
        [YamlSerializable(typeof(RecursiveDictionary))]
        [YamlSerializable(typeof(RecursiveList))]
        [YamlSerializable(typeof(NodeWithObjectProperty))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(ReadOnlyDictionary<string, object>))]
        [YamlSerializable(typeof(WrapperForIDictionary))]
        [YamlSerializable(typeof(IDictionary<string, object>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<int>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<Employee>))]
        [YamlSerializable(typeof(ReferenceHandlerTests.ClassWithZeroLengthProperty<List<int>>))]
        [YamlSerializable(typeof(object[]))]
        [YamlSerializable(typeof(ReferenceHandlerTests.IBoxedStructWithObjectProperty))]
        [YamlSerializable(typeof(ValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(ValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(NodeWithNodeProperty))]
        [YamlSerializable(typeof(NodeWithObjectProperty))]
        [YamlSerializable(typeof(EmptyClassWithExtensionProperty))]
        [YamlSerializable(typeof(Person))]
        [YamlSerializable(typeof(ICollection<object>))]
        [YamlSerializable(typeof(Stack<object>))]
        [YamlSerializable(typeof(Queue<object>))]
        [YamlSerializable(typeof(ConcurrentStack<object>))]
        [YamlSerializable(typeof(ConcurrentQueue<object>))]
        [YamlSerializable(typeof(Stack))]
        [YamlSerializable(typeof(Queue))]
        [YamlSerializable(typeof(IValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(ValueNodeWithIValueNodeProperty))]
        [YamlSerializable(typeof(IValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(ValueNodeWithObjectProperty))]
        [YamlSerializable(typeof(EmptyClass))]
        [YamlSerializable(typeof(EmptyStruct))]
        [YamlSerializable(typeof(List<string>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<object>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<NodeWithNodeProperty>))]
        [YamlSerializable(typeof(ClassWithGenericProperty<NodeWithObjectProperty>))]
        [YamlSerializable(typeof(List<Person>))]
        [YamlSerializable(typeof(RecursiveValue))]
        [YamlSerializable(typeof(List<RecursiveValue>))]
        [YamlSerializable(typeof(PersonHolder))]
        [YamlSerializable(typeof(BoxedPersonHolder))]
        [YamlSerializable(typeof(TreeNode<EmptyClass>))]
        [YamlSerializable(typeof(TreeNode<EmptyStruct>))]
        [YamlSerializable(typeof(TreeNode<object>))]
        [YamlSerializable(typeof(TreeNode<Dictionary<string, object>>))]
        [YamlSerializable(typeof(TreeNode<List<string>>))]
        [YamlSerializable(typeof(TreeNode<object>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(EmployeeLocation))]
        [YamlSerializable(typeof(EmployeeLocation[]))]
        [YamlSerializable(typeof(OfficeWorker))]
        [YamlSerializable(typeof(Office))]
        [YamlSerializable(typeof(RemoteWorker))]
        internal sealed partial class ReferenceHandlerTests_IgnoreCyclesContext_Default : YamlSerializerContext
        {
        }
    }
}
