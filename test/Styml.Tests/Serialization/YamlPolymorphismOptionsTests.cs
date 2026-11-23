// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;
using static System.Text.Yaml.Serialization.Tests.PolymorphicTests;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class YamlPolymorphismOptionsTests
    {
        [Fact]
        public static void YamlPolymorphismOptions_DefaultInstance()
        {
            var options = new YamlPolymorphismOptions();

            Assert.False(options.IgnoreUnrecognizedTypeDiscriminators);
            Assert.Equal(YamlUnknownDerivedTypeHandling.FailSerialization, options.UnknownDerivedTypeHandling);
            Assert.Equal("$type", options.TypeDiscriminatorPropertyName);
            Assert.Empty(options.DerivedTypes);
        }

        [Theory]
        [MemberData(nameof(GetDerivedTypes))]
        public static void YamlPolymorphismOptions_AddDerivedTypes(YamlDerivedType[] derivedTypes)
        {
            var options = new YamlPolymorphismOptions();
            foreach (YamlDerivedType derivedType in derivedTypes)
            {
                options.DerivedTypes.Add(derivedType);
            }

            Assert.Equal(derivedTypes, options.DerivedTypes);
        }

        public static IEnumerable<object[]> GetDerivedTypes()
        {
            yield return WrapArgs(default(YamlDerivedType));
            yield return WrapArgs(new YamlDerivedType(typeof(int)));
            yield return WrapArgs(new YamlDerivedType(typeof(void), "void"));
            yield return WrapArgs(new YamlDerivedType(typeof(object), 42));
            yield return WrapArgs(new YamlDerivedType(typeof(string)));
            yield return WrapArgs(
                new YamlDerivedType(typeof(YamlSerializerOptions)),
                new YamlDerivedType(typeof(int), 42),
                new YamlDerivedType(typeof(void), "void"));

            static object[] WrapArgs(params YamlDerivedType[] derivedTypes) => new object[] { derivedTypes };
        }

        [Fact]
        public static void YamlPolymorphismOptions_AssigningOptionsToYamlTypeInfoKindNone_ThrowsInvalidOperationException()
        {
            var options = new YamlPolymorphismOptions();
            YamlTypeInfo jti = YamlTypeInfo.CreateYamlTypeInfo(typeof(int), new());
            Assert.Equal(YamlTypeInfoKind.None, jti.Kind);

            Assert.Throws<InvalidOperationException>(() => jti.PolymorphismOptions = options);
        }

        [Fact]
        public static void YamlPolymorphismOptions_AssigningOptionsToSecondYamlTypeInfo_ThrowsInvalidOperationException()
        {
            var options = new YamlPolymorphismOptions();

            YamlTypeInfo jti1 = YamlTypeInfo.CreateYamlTypeInfo(typeof(PolymorphicClass), new());
            jti1.PolymorphismOptions = options;

            YamlTypeInfo jti2 = YamlTypeInfo.CreateYamlTypeInfo(typeof(PolymorphicClass), new());
            Assert.Throws<ArgumentException>(() => jti2.PolymorphismOptions = options);
        }

        [Fact]
        public static void YamlPolymorphismOptions_CreateBlankYamlTypeInfo_ContainsNoPolymorphismMetadata()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;

            // Sanity check: type returns polymorphism options using the default resolver
            YamlTypeInfo jti = options.TypeInfoResolver.GetTypeInfo(typeof(PolymorphicClass), options);
            Assert.NotNull(jti.PolymorphismOptions);

            // Blank instance should not contain polymorphism options
            jti = YamlTypeInfo.CreateYamlTypeInfo(typeof(PolymorphicClass), options);
            Assert.Null(jti.PolymorphismOptions);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(IEnumerable<int>))]
        [InlineData(typeof(PolymorphicClass))]
        [InlineData(typeof(PolymorphicClass.DerivedClass1_NoTypeDiscriminator))]
        [InlineData(typeof(PolymorphicClass.DerivedClass1_TypeDiscriminator))]
        [InlineData(typeof(PolymorphicClassWithConstructor))]
        [InlineData(typeof(PolymorphicList))]
        [InlineData(typeof(PolymorphicDictionary))]
        [InlineData(typeof(PolymorphicClassWithCustomTypeDiscriminator))]
        [InlineData(typeof(PolymorphicClassWithoutDerivedTypeAttribute))]
        [InlineData(typeof(PolymorphicClass_InvalidCustomTypeDiscriminatorPropertyName))]
        [InlineData(typeof(PolymorphicClassWithNullDerivedTypeAttribute))]
        [InlineData(typeof(PolymorphicClassWithStructDerivedTypeAttribute))]
        [InlineData(typeof(PolymorphicClassWithObjectDerivedTypeAttribute))]
        [InlineData(typeof(PolymorphicClassWithNonAssignableDerivedTypeAttribute))]
        [InlineData(typeof(PolymorphicAbstractClassWithAbstractClassDerivedType))]
        [InlineData(typeof(PolymorphicClassWithDuplicateDerivedTypeRegistrations))]
        [InlineData(typeof(PolymorphicClasWithDuplicateTypeDiscriminators))]
        [InlineData(typeof(PolymorphicGenericClass<int>))]
        [InlineData(typeof(PolymorphicDerivedGenericClass.DerivedClass<int>))]
        [InlineData(typeof(PolymorphicClass_CustomConverter_TypeDiscriminator))]
        [InlineData(typeof(PolymorphicClass_CustomConverter_NoTypeDiscriminator))]
        public static void DefaultResolver_ReportsCorrectPolymorphismMetadata(Type polymorphicType)
        {
            YamlPolymorphicAttribute? polymorphicAttribute = polymorphicType.GetCustomAttribute<YamlPolymorphicAttribute>(inherit: false);
            YamlDerivedTypeAttribute[] derivedTypeAttributes = polymorphicType.GetCustomAttributes<YamlDerivedTypeAttribute>(inherit: false).ToArray();

            YamlSerializer.Serialize(42); // Ensure default converters have been rooted
            var options = YamlSerializerOptions.Default;
            YamlTypeInfo yamlTypeInfo = options.TypeInfoResolver.GetTypeInfo(polymorphicType, options);

            Assert.Equal(polymorphicType, yamlTypeInfo.Type);

            YamlPolymorphismOptions? polyOptions = yamlTypeInfo.PolymorphismOptions;
            if (polymorphicAttribute == null && derivedTypeAttributes.Length == 0)
            {
                Assert.Null(polyOptions);
            }
            else
            {
                Assert.NotNull(polyOptions);

                Assert.Equal(polymorphicAttribute?.IgnoreUnrecognizedTypeDiscriminators ?? false, polyOptions.IgnoreUnrecognizedTypeDiscriminators);
                Assert.Equal(polymorphicAttribute?.UnknownDerivedTypeHandling ?? default, polyOptions.UnknownDerivedTypeHandling);
                Assert.Equal(polymorphicAttribute?.TypeDiscriminatorPropertyName ?? "$type", polyOptions.TypeDiscriminatorPropertyName);
                Assert.Equal(derivedTypeAttributes.Length, polyOptions.DerivedTypes.Count);

                for (int i = 0; i < derivedTypeAttributes.Length; i++)
                {
                    YamlDerivedTypeAttribute attr = derivedTypeAttributes[i];
                    YamlDerivedType derivedType = polyOptions.DerivedTypes[i];
                    Assert.Equal(attr.TypeDiscriminator, derivedType.TypeDiscriminator);

                    if (attr.DerivedType is { IsGenericTypeDefinition: true })
                    {
                        // Open generic derived types are resolved by the reflection resolver
                        // to closed types: verify the resolved type is closed and is a
                        // constructed-form of the original open generic definition.
                        Assert.False(derivedType.DerivedType.IsGenericTypeDefinition);
                        Assert.True(derivedType.DerivedType.IsGenericType);
                        Assert.Equal(attr.DerivedType, derivedType.DerivedType.GetGenericTypeDefinition());
                    }
                    else
                    {
                        Assert.Equal(attr.DerivedType, derivedType.DerivedType);
                    }
                }
            }
        }
    }
}
