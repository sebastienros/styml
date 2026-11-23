// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed class ReferenceHandlerTests_Metadata_String : ReferenceHandlerTests_Metadata
    {
        public ReferenceHandlerTests_Metadata_String()
            : base(new StringSerializerWrapper(ReferenceHandlerTestsContext_Metadata.Default))
        {
        }
    }

    public sealed class ReferenceHandlerTests_Metadata_AsyncStream : ReferenceHandlerTests_Metadata
    {
        public ReferenceHandlerTests_Metadata_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(ReferenceHandlerTestsContext_Metadata.Default))
        {
        }
    }

    public abstract partial class ReferenceHandlerTests_Metadata : ReferenceHandlerTests
    {
        public ReferenceHandlerTests_Metadata(YamlSerializerWrapper serializer)
            : base(serializer)
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(Employee))]
        [YamlSerializable(typeof(KeyValuePair<string, string>))]
        [YamlSerializable(typeof(ClassWithUnicodeProperty))]
        [YamlSerializable(typeof(DictionaryWithGenericCycle))]
        [YamlSerializable(typeof(Dictionary<string, Employee>))]
        [YamlSerializable(typeof(DictionaryWithGenericCycleWithinList))]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        [YamlSerializable(typeof(ListWithGenericCycle))]
        [YamlSerializable(typeof(List<Employee>))]
        [YamlSerializable(typeof(ListWithGenericCycleWithinDictionary))]
        [YamlSerializable(typeof(ImmutableArray<PersonReference>))]
        [YamlSerializable(typeof(ClassWithObjectProperty))]
        [YamlSerializable(typeof(ClassWithListOfObjectProperty))]
        [YamlSerializable(typeof(EmployeeWithContacts))]
        [YamlSerializable(typeof(ClassWithSubsequentListProperties))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<int>))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<Employee>))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<List<int>>))]
        [YamlSerializable(typeof(Dictionary<string, string>))]
        [YamlSerializable(typeof(Dictionary<string, EmployeeWithContacts>))]
        [YamlSerializable(typeof(Dictionary<string, List<int>>))]
        [YamlSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
        [YamlSerializable(typeof(List<int>))]
        [YamlSerializable(typeof(Employee[]))]
        [YamlSerializable(typeof(List<List<int>>))]
        [YamlSerializable(typeof(ListWrapper))]
        [YamlSerializable(typeof(List<List<Employee>>))]
        [YamlSerializable(typeof(List<EmployeeStruct>))]
        [YamlSerializable(typeof(ImmutableList<EmployeeWithImmutable>))]
        [YamlSerializable(typeof(EmployeeWithImmutable[]))]
        [YamlSerializable(typeof(ImmutableDictionary<string, EmployeeWithImmutable>))]
        [YamlSerializable(typeof(EmployeeWithImmutable))]
        [YamlSerializable(typeof(ImmutableDictionary<string, Employee>))]
        [YamlSerializable(typeof(Order))]
        [YamlSerializable(typeof(List<Order>))]
        [YamlSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
        [YamlSerializable(typeof(ClassWithTwoListProperties))]
        [YamlSerializable(typeof(EmployeeExtensionData))]
        [YamlSerializable(typeof(List<string>))]
        [YamlSerializable(typeof(BaseAndDerivedWrapper))]
        [YamlSerializable(typeof(List<ClassIncorrectHashCode>))]
        [YamlSerializable(typeof(ClassWithComplexObjects))]
        [YamlSerializable(typeof(object[]))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(SimpleTestStruct))]
        [YamlSerializable(typeof(SimpleTestStructWithFields))]
        [YamlSerializable(typeof(SimpleTestClass))]
        [YamlSerializable(typeof(SimpleTestClassWithFields))]
        [YamlSerializable(typeof(SimpleTestClassWithNullables))]
        [YamlSerializable(typeof(SimpleTestClassWithNulls))]
        [YamlSerializable(typeof(SimpleTestClassWithSimpleObject))]
        [YamlSerializable(typeof(SimpleTestClassWithObjectArrays))]
        [YamlSerializable(typeof(BasicPerson))]
        [YamlSerializable(typeof(BasicCompany))]
        [YamlSerializable(typeof(TestClassWithNestedObjectInner))]
        [YamlSerializable(typeof(TestClassWithNestedObjectOuter))]
        [YamlSerializable(typeof(TestClassWithObjectArray))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerable))]
        [YamlSerializable(typeof(TestClassWithObjectIList))]
        [YamlSerializable(typeof(TestClassWithObjectICollection))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerableT))]
        [YamlSerializable(typeof(TestClassWithObjectIListT))]
        [YamlSerializable(typeof(TestClassWithObjectICollectionT))]
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlyCollectionT))]
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlyListT))]
        [YamlSerializable(typeof(TestClassWithObjectISetT))]

#if NET
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlySetT))]
#endif

        [YamlSerializable(typeof(TestClassWithStringArray))]
        [YamlSerializable(typeof(TestClassWithGenericList))]
        [YamlSerializable(typeof(TestClassWithGenericIEnumerable))]
        [YamlSerializable(typeof(TestClassWithGenericIList))]
        [YamlSerializable(typeof(TestClassWithGenericICollection))]
        [YamlSerializable(typeof(TestClassWithGenericIEnumerableT))]
        [YamlSerializable(typeof(TestClassWithGenericIListT))]
        [YamlSerializable(typeof(TestClassWithGenericICollectionT))]
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlyCollectionT))]
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlyListT))]
        [YamlSerializable(typeof(TestClassWithGenericISetT))]

#if NET
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlySetT))]
#endif

        [YamlSerializable(typeof(TestClassWithStringToPrimitiveDictionary))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerableConstructibleTypes))]
        [YamlSerializable(typeof(TestClassWithObjectImmutableTypes))]
        [YamlSerializable(typeof(YamlElementClass))]
        [YamlSerializable(typeof(YamlElementArrayClass))]
        [YamlSerializable(typeof(ClassWithComplexObjects))]
        [YamlSerializable(typeof(IBoxedStructWithObjectProperty))]
        [YamlSerializable(typeof(StructWithObjectProperty))]
        [YamlSerializable(typeof(ImmutableArray<EmployeeStruct>))]
        [YamlSerializable(typeof(PersonReference))]
        [YamlSerializable(typeof(ClassWithListAndImmutableArray))]
        [YamlSerializable(typeof(ImmutableArray<List<int>>))]
        [YamlSerializable(typeof(List<List<int>>))]
        [YamlSerializable(typeof(List<ImmutableArray<int>>))]
        [YamlSerializable(typeof(List<ImmutableArray<int>>))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(StructCollection))]
        [YamlSerializable(typeof(ImmutableArray<int>))]
        [YamlSerializable(typeof(LinkedList<int>))]
        [YamlSerializable(typeof(LinkedList<int>[]))]
        [YamlSerializable(typeof(LinkedList<object>))]
        [YamlSerializable(typeof(LinkedList<object[]>))]
        [YamlSerializable(typeof(LinkedList<Base[]>))]
        [YamlSerializable(typeof(LinkedList<Base[][]>))]
        [YamlSerializable(typeof(ClassWithConflictingRefProperty))]
        [YamlSerializable(typeof(ClassWithConflictingIdProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredConflictingProperty))]
        [YamlSerializable(typeof(ClassWithExtensionDataConflictingProperty))]
        [YamlSerializable(typeof(EmployeeLocation))]
        [YamlSerializable(typeof(EmployeeLocation[]))]
        [YamlSerializable(typeof(OfficeWorker))]
        [YamlSerializable(typeof(Office))]
        [YamlSerializable(typeof(RemoteWorker))]
        internal sealed partial class ReferenceHandlerTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed class ReferenceHandlerTests_Default_String : ReferenceHandlerTests_Default
    {
        public ReferenceHandlerTests_Default_String()
            : base(new StringSerializerWrapper(ReferenceHandlerTestsContext_Default.Default))
        {
        }

        [Fact]
        public override async Task ThrowByDefaultOnLoop()
        {
            Employee a = new Employee();
            a.Manager = a;

            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.SerializeWrapper(a));
        }
    }

    public sealed class ReferenceHandlerTests_Default_AsyncStream : ReferenceHandlerTests_Default
    {
        public ReferenceHandlerTests_Default_AsyncStream()
            : base(new AsyncStreamSerializerWrapper(ReferenceHandlerTestsContext_Default.Default))
        {
        }
    }

    public abstract partial class ReferenceHandlerTests_Default : ReferenceHandlerTests
    {
        public ReferenceHandlerTests_Default(YamlSerializerWrapper serializer)
            : base(serializer)
        {
        }

        [YamlSerializable(typeof(Employee))]
        [YamlSerializable(typeof(KeyValuePair<string, string>))]
        [YamlSerializable(typeof(ClassWithUnicodeProperty))]
        [YamlSerializable(typeof(DictionaryWithGenericCycle))]
        [YamlSerializable(typeof(Dictionary<string, Employee>))]
        [YamlSerializable(typeof(DictionaryWithGenericCycleWithinList))]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        [YamlSerializable(typeof(ListWithGenericCycle))]
        [YamlSerializable(typeof(List<Employee>))]
        [YamlSerializable(typeof(ListWithGenericCycleWithinDictionary))]
        [YamlSerializable(typeof(ImmutableArray<PersonReference>))]
        [YamlSerializable(typeof(ClassWithObjectProperty))]
        [YamlSerializable(typeof(ClassWithListOfObjectProperty))]
        [YamlSerializable(typeof(EmployeeWithContacts))]
        [YamlSerializable(typeof(ClassWithSubsequentListProperties))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<int>))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<Employee>))]
        [YamlSerializable(typeof(ClassWithZeroLengthProperty<List<int>>))]
        [YamlSerializable(typeof(Dictionary<string, string>))]
        [YamlSerializable(typeof(Dictionary<string, EmployeeWithContacts>))]
        [YamlSerializable(typeof(Dictionary<string, List<int>>))]
        [YamlSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
        [YamlSerializable(typeof(List<int>))]
        [YamlSerializable(typeof(Employee[]))]
        [YamlSerializable(typeof(List<List<int>>))]
        [YamlSerializable(typeof(ListWrapper))]
        [YamlSerializable(typeof(List<List<Employee>>))]
        [YamlSerializable(typeof(List<EmployeeStruct>))]
        [YamlSerializable(typeof(ImmutableList<EmployeeWithImmutable>))]
        [YamlSerializable(typeof(EmployeeWithImmutable[]))]
        [YamlSerializable(typeof(ImmutableDictionary<string, EmployeeWithImmutable>))]
        [YamlSerializable(typeof(EmployeeWithImmutable))]
        [YamlSerializable(typeof(ImmutableDictionary<string, Employee>))]
        [YamlSerializable(typeof(Order))]
        [YamlSerializable(typeof(List<Order>))]
        [YamlSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
        [YamlSerializable(typeof(ClassWithTwoListProperties))]
        [YamlSerializable(typeof(EmployeeExtensionData))]
        [YamlSerializable(typeof(List<string>))]
        [YamlSerializable(typeof(BaseAndDerivedWrapper))]
        [YamlSerializable(typeof(List<ClassIncorrectHashCode>))]
        [YamlSerializable(typeof(ClassWithComplexObjects))]
        [YamlSerializable(typeof(object[]))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(SimpleTestStruct))]
        [YamlSerializable(typeof(SimpleTestStructWithFields))]
        [YamlSerializable(typeof(SimpleTestClass))]
        [YamlSerializable(typeof(SimpleTestClassWithFields))]
        [YamlSerializable(typeof(SimpleTestClassWithNullables))]
        [YamlSerializable(typeof(SimpleTestClassWithNulls))]
        [YamlSerializable(typeof(SimpleTestClassWithSimpleObject))]
        [YamlSerializable(typeof(SimpleTestClassWithObjectArrays))]
        [YamlSerializable(typeof(BasicPerson))]
        [YamlSerializable(typeof(BasicCompany))]
        [YamlSerializable(typeof(TestClassWithNestedObjectInner))]
        [YamlSerializable(typeof(TestClassWithNestedObjectOuter))]
        [YamlSerializable(typeof(TestClassWithObjectArray))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerable))]
        [YamlSerializable(typeof(TestClassWithObjectIList))]
        [YamlSerializable(typeof(TestClassWithObjectICollection))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerableT))]
        [YamlSerializable(typeof(TestClassWithObjectIListT))]
        [YamlSerializable(typeof(TestClassWithObjectICollectionT))]
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlyCollectionT))]
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlyListT))]
        [YamlSerializable(typeof(TestClassWithObjectISetT))]

#if NET
        [YamlSerializable(typeof(TestClassWithObjectIReadOnlySetT))]
#endif

        [YamlSerializable(typeof(TestClassWithStringArray))]
        [YamlSerializable(typeof(TestClassWithGenericList))]
        [YamlSerializable(typeof(TestClassWithGenericIEnumerable))]
        [YamlSerializable(typeof(TestClassWithGenericIList))]
        [YamlSerializable(typeof(TestClassWithGenericICollection))]
        [YamlSerializable(typeof(TestClassWithGenericIEnumerableT))]
        [YamlSerializable(typeof(TestClassWithGenericIListT))]
        [YamlSerializable(typeof(TestClassWithGenericICollectionT))]
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlyCollectionT))]
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlyListT))]
        [YamlSerializable(typeof(TestClassWithGenericISetT))]

#if NET
        [YamlSerializable(typeof(TestClassWithGenericIReadOnlySetT))]
#endif

        [YamlSerializable(typeof(TestClassWithStringToPrimitiveDictionary))]
        [YamlSerializable(typeof(TestClassWithObjectIEnumerableConstructibleTypes))]
        [YamlSerializable(typeof(TestClassWithObjectImmutableTypes))]
        [YamlSerializable(typeof(YamlElementClass))]
        [YamlSerializable(typeof(YamlElementArrayClass))]
        [YamlSerializable(typeof(ClassWithComplexObjects))]
        [YamlSerializable(typeof(IBoxedStructWithObjectProperty))]
        [YamlSerializable(typeof(StructWithObjectProperty))]
        [YamlSerializable(typeof(ImmutableArray<EmployeeStruct>))]
        [YamlSerializable(typeof(PersonReference))]
        [YamlSerializable(typeof(ClassWithListAndImmutableArray))]
        [YamlSerializable(typeof(ImmutableArray<List<int>>))]
        [YamlSerializable(typeof(List<List<int>>))]
        [YamlSerializable(typeof(List<ImmutableArray<int>>))]
        [YamlSerializable(typeof(List<ImmutableArray<int>>))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(StructCollection))]
        [YamlSerializable(typeof(ImmutableArray<int>))]
        [YamlSerializable(typeof(LinkedList<int>))]
        [YamlSerializable(typeof(LinkedList<int>[]))]
        [YamlSerializable(typeof(LinkedList<object>))]
        [YamlSerializable(typeof(LinkedList<object[]>))]
        [YamlSerializable(typeof(LinkedList<Base[]>))]
        [YamlSerializable(typeof(LinkedList<Base[][]>))]
        [YamlSerializable(typeof(ClassWithConflictingRefProperty))]
        [YamlSerializable(typeof(ClassWithConflictingIdProperty))]
        [YamlSerializable(typeof(ClassWithIgnoredConflictingProperty))]
        [YamlSerializable(typeof(ClassWithExtensionDataConflictingProperty))]
        [YamlSerializable(typeof(EmployeeLocation))]
        [YamlSerializable(typeof(EmployeeLocation[]))]
        [YamlSerializable(typeof(OfficeWorker))]
        [YamlSerializable(typeof(Office))]
        [YamlSerializable(typeof(RemoteWorker))]
        internal sealed partial class ReferenceHandlerTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
