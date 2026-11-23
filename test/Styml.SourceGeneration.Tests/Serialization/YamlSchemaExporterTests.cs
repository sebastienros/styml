// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema.Tests;
using System.Text.Yaml.Serialization;
using System.Xml.Linq;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class YamlSchemaExporterTests_SourceGen()
        : YamlSchemaExporterTests(new StringSerializerWrapper(TestTypesContext.Default))
    {
        [YamlSerializable(typeof(object))]
        [YamlSerializable(typeof(bool))]
        [YamlSerializable(typeof(byte))]
        [YamlSerializable(typeof(ushort))]
        [YamlSerializable(typeof(uint))]
        [YamlSerializable(typeof(ulong))]
        [YamlSerializable(typeof(sbyte))]
        [YamlSerializable(typeof(short))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(long))]
        [YamlSerializable(typeof(float))]
        [YamlSerializable(typeof(double))]
        [YamlSerializable(typeof(decimal))]
#if NET
        [YamlSerializable(typeof(UInt128))]
        [YamlSerializable(typeof(Int128))]
        [YamlSerializable(typeof(Half))]
#endif
#if NET11_0_OR_GREATER
        [YamlSerializable(typeof(System.Numerics.BFloat16))]
        [YamlSerializable(typeof(System.Numerics.Decimal32))]
        [YamlSerializable(typeof(System.Numerics.Decimal64))]
        [YamlSerializable(typeof(System.Numerics.Decimal128))]
#endif
        [YamlSerializable(typeof(string))]
        [YamlSerializable(typeof(char))]
        [YamlSerializable(typeof(byte[]))]
        [YamlSerializable(typeof(Memory<byte>))]
        [YamlSerializable(typeof(ReadOnlyMemory<byte>))]
        [YamlSerializable(typeof(DateTime))]
        [YamlSerializable(typeof(DateTimeOffset))]
        [YamlSerializable(typeof(DateTimeOffset?))]
        [YamlSerializable(typeof(TimeSpan))]
#if NET
        [YamlSerializable(typeof(DateOnly))]
        [YamlSerializable(typeof(TimeOnly))]
#endif
        [YamlSerializable(typeof(Guid))]
        [YamlSerializable(typeof(Uri))]
        [YamlSerializable(typeof(Version))]
        [YamlSerializable(typeof(YamlDocument))]
        [YamlSerializable(typeof(YamlElement))]
        [YamlSerializable(typeof(YamlNode))]
        [YamlSerializable(typeof(YamlValue))]
        [YamlSerializable(typeof(YamlObject))]
        [YamlSerializable(typeof(YamlArray))]
        // Unsupported types
        [YamlSerializable(typeof(Type))]
        [YamlSerializable(typeof(MethodInfo))]
        [YamlSerializable(typeof(UIntPtr))]
        [YamlSerializable(typeof(MemberInfo))]
        [YamlSerializable(typeof(SerializationInfo))]
        [YamlSerializable(typeof(Func<int, int>))]
        // Enum types
        [YamlSerializable(typeof(IntEnum))]
        [YamlSerializable(typeof(StringEnum))]
        [YamlSerializable(typeof(FlagsStringEnum))]
        [YamlSerializable(typeof(EnumWithNameAttributes))]
        // Nullable<T> types
        [YamlSerializable(typeof(bool?))]
        [YamlSerializable(typeof(int?))]
        [YamlSerializable(typeof(double?))]
        [YamlSerializable(typeof(float?))]
#if NET
        [YamlSerializable(typeof(Half?))]
#endif
#if NET11_0_OR_GREATER
        [YamlSerializable(typeof(System.Numerics.BFloat16?))]
        [YamlSerializable(typeof(System.Numerics.Decimal64?))]
#endif
        [YamlSerializable(typeof(Guid?))]
        [YamlSerializable(typeof(YamlElement?))]
        [YamlSerializable(typeof(IntEnum?))]
        [YamlSerializable(typeof(StringEnum?))]
        [YamlSerializable(typeof(SimpleRecordStruct?))]
        // Union types
        [YamlSerializable(typeof(StrictIntOrStringUnion))]
        [YamlSerializable(typeof(List<StrictIntOrStringUnion>))]
        [YamlSerializable(typeof(IntOrBoolUnion))]
        [YamlSerializable(typeof(NullableIntUnion))]
        // User-defined POCOs
        [YamlSerializable(typeof(SimplePoco))]
        [YamlSerializable(typeof(SimpleRecord))]
        [YamlSerializable(typeof(SimpleRecordStruct))]
        [YamlSerializable(typeof(RecordWithOptionalParameters))]
        [YamlSerializable(typeof(PocoWithRequiredMembers))]
        [YamlSerializable(typeof(PocoWithIgnoredMembers))]
        [YamlSerializable(typeof(PocoWithCustomNaming))]
        [YamlSerializable(typeof(PocoWithCustomNumberHandling))]
        [YamlSerializable(typeof(PocoWithCustomNumberHandlingOnProperties))]
        [YamlSerializable(typeof(PocoWithNullableFloatingPoint))]
        [YamlSerializable(typeof(PocoWithRecursiveMembers))]
        [YamlSerializable(typeof(PocoWithRecursiveCollectionElement))]
        [YamlSerializable(typeof(PocoWithRecursiveDictionaryValue))]
        [YamlSerializable(typeof(PocoWithNonRecursiveDuplicateOccurrences))]
        [YamlSerializable(typeof(PocoWithDescription))]
        [YamlSerializable(typeof(PocoWithCustomConverter))]
        [YamlSerializable(typeof(PocoWithCustomPropertyConverter))]
        [YamlSerializable(typeof(PocoWithEnums))]
        [YamlSerializable(typeof(PocoWithStructFollowedByNullableStruct))]
        [YamlSerializable(typeof(PocoWithNullableStructFollowedByStruct))]
        [YamlSerializable(typeof(PocoWithExtensionDataProperty))]
        [YamlSerializable(typeof(PocoDisallowingUnmappedMembers))]
        [YamlSerializable(typeof(PocoWithNullableAnnotationAttributes))]
        [YamlSerializable(typeof(PocoWithNullableAnnotationAttributesOnConstructorParams))]
        [YamlSerializable(typeof(PocoWithNullableConstructorParameter))]
        [YamlSerializable(typeof(PocoWithOptionalConstructorParams))]
        [YamlSerializable(typeof(GenericPocoWithNullableConstructorParameter<string>))]
        [YamlSerializable(typeof(PocoWithPolymorphism))]
        [YamlSerializable(typeof(DiscriminatedUnion))]
        [YamlSerializable(typeof(NonAbstractClassWithSingleDerivedType))]
        [YamlSerializable(typeof(PocoCombiningPolymorphicTypeAndDerivedTypes))]
        [YamlSerializable(typeof(ClassWithComponentModelAttributes))]
        [YamlSerializable(typeof(ClassWithYamlPointerEscapablePropertyNames))]
        [YamlSerializable(typeof(ClassWithPropertyNameRequiringFragmentEncoding))]
        [YamlSerializable(typeof(ClassWithOptionalObjectParameter))]
        [YamlSerializable(typeof(ClassWithPropertiesUsingCustomConverters))]
#pragma warning disable CS0612 // Type or member is obsolete
        [YamlSerializable(typeof(MyObsoleteType))]
#pragma warning restore CS0612 // Type or member is obsolete
        // Collection types
        [YamlSerializable(typeof(int[]))]
        [YamlSerializable(typeof(List<bool>))]
        [YamlSerializable(typeof(HashSet<string>))]
        [YamlSerializable(typeof(Queue<double>))]
        [YamlSerializable(typeof(Stack<char>))]
        [YamlSerializable(typeof(ImmutableArray<int>))]
        [YamlSerializable(typeof(ImmutableList<string>))]
        [YamlSerializable(typeof(ImmutableQueue<bool>))]
        [YamlSerializable(typeof(object[]))]
        [YamlSerializable(typeof(System.Collections.ArrayList))]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        [YamlSerializable(typeof(SortedDictionary<int, string>))]
        [YamlSerializable(typeof(Dictionary<string, SimplePoco>))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(Hashtable))]
        [YamlSerializable(typeof(StructDictionary<string, int>))]
        [YamlSerializable(typeof(XElement))]
        public partial class TestTypesContext : YamlSerializerContext;
    }
}
