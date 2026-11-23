// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class UnsupportedTypesTests_Metadata : UnsupportedTypesTests
    {
        public UnsupportedTypesTests_Metadata() : base(
            new StringSerializerWrapper(UnsupportedTypesTestsContext_Metadata.Default),
            supportsYamlPathOnSerialize: true)
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        // Supported types:
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithIntPtrConverter))]
        // Unsupported types:
        [YamlSerializable(typeof(Type))]
        [YamlSerializable(typeof(ClassWithType<object>))]
        [YamlSerializable(typeof(ClassWithType<Type>))]
        [YamlSerializable(typeof(ConstructorInfo))]
        [YamlSerializable(typeof(ClassWithType<ConstructorInfo>))]
        [YamlSerializable(typeof(PropertyInfo))]
        [YamlSerializable(typeof(ClassWithType<PropertyInfo>))]
        [YamlSerializable(typeof(SerializationInfo))]
        [YamlSerializable(typeof(ClassWithType<SerializationInfo>))]
        [YamlSerializable(typeof(int[,]))]
        [YamlSerializable(typeof(ClassWithType<int[,]>))]
        [YamlSerializable(typeof(bool[,,,]))]
        [YamlSerializable(typeof(ClassWithType<bool[,,,]>))]
        [YamlSerializable(typeof(IntPtr))]
        [YamlSerializable(typeof(ClassWithType<IntPtr>))]
        [YamlSerializable(typeof(ClassWithIntPtr))]
        [YamlSerializable(typeof(IntPtr?))]
        [YamlSerializable(typeof(ClassWithType<IntPtr?>))]
        [YamlSerializable(typeof(UIntPtr))]
        [YamlSerializable(typeof(ClassWithType<UIntPtr>))]
        [YamlSerializable(typeof(IAsyncEnumerable<int>))]
        [YamlSerializable(typeof(ClassWithType<IAsyncEnumerable<int>>))]
        [YamlSerializable(typeof(ClassThatImplementsIAsyncEnumerable))]
        [YamlSerializable(typeof(ClassWithType<ClassThatImplementsIAsyncEnumerable>))]
        [YamlSerializable(typeof(ClassWithAsyncEnumerableConverter))]
        internal sealed partial class UnsupportedTypesTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed partial class UnsupportedTypesTests_Default : UnsupportedTypesTests
    {
        public UnsupportedTypesTests_Default() : base(
            new StringSerializerWrapper(UnsupportedTypesTestsContext_Default.Default),
            supportsYamlPathOnSerialize: false)
        {
        }

        // Supported types:
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithIntPtrConverter))]
        // Unsupported types:
        [YamlSerializable(typeof(Type))]
        [YamlSerializable(typeof(ClassWithType<object>))]
        [YamlSerializable(typeof(ClassWithType<Type>))]
        [YamlSerializable(typeof(ConstructorInfo))]
        [YamlSerializable(typeof(ClassWithType<ConstructorInfo>))]
        [YamlSerializable(typeof(PropertyInfo))]
        [YamlSerializable(typeof(ClassWithType<PropertyInfo>))]
        [YamlSerializable(typeof(SerializationInfo))]
        [YamlSerializable(typeof(ClassWithType<SerializationInfo>))]
        [YamlSerializable(typeof(int[,]))]
        [YamlSerializable(typeof(ClassWithType<int[,]>))]
        [YamlSerializable(typeof(bool[,,,]))]
        [YamlSerializable(typeof(ClassWithType<bool[,,,]>))]
        [YamlSerializable(typeof(IntPtr))]
        [YamlSerializable(typeof(ClassWithType<IntPtr>))]
        [YamlSerializable(typeof(ClassWithIntPtr))]
        [YamlSerializable(typeof(IntPtr?))]
        [YamlSerializable(typeof(ClassWithType<IntPtr?>))]
        [YamlSerializable(typeof(UIntPtr))]
        [YamlSerializable(typeof(ClassWithType<UIntPtr>))]
        [YamlSerializable(typeof(IAsyncEnumerable<int>))]
        [YamlSerializable(typeof(ClassWithType<IAsyncEnumerable<int>>))]
        [YamlSerializable(typeof(ClassThatImplementsIAsyncEnumerable))]
        [YamlSerializable(typeof(ClassWithType<ClassThatImplementsIAsyncEnumerable>))]
        [YamlSerializable(typeof(ClassWithAsyncEnumerableConverter))]
        internal sealed partial class UnsupportedTypesTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
