// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class PropertyNameTests_Metadata : PropertyNameTests
    {
        public PropertyNameTests_Metadata()
            : base(new StringSerializerWrapper(PropertyNameTestsContext_Metadata.Default))
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_FallsBackToClrName()
        {
            // Source gen can't resolve custom derived naming policies at compile time,
            // so the property name falls back to the original CLR name.
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_FallsBackToClrName()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedMemberNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_PreventsGlobalPolicyFromApplying()
        {
            // Even when a global naming policy is configured, the custom derived attribute
            // should prevent it from applying — the CLR name should be used instead.
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_PreventsGlobalPolicyFromApplying()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedMemberNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(Dictionary<string, OverridePropertyNameDesignTime_TestClass>))]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithSpecialCharacters))]
        [YamlSerializable(typeof(ClassWithPropertyNamePermutations))]
        [YamlSerializable(typeof(ClassWithUnicodeProperty))]
        [YamlSerializable(typeof(DuplicatePropertyNameDesignTime_TestClass))]
        [YamlSerializable(typeof(EmptyPropertyName_TestClass))]
        [YamlSerializable(typeof(IntPropertyNamesDifferentByCaseOnly_TestClass))]
        [YamlSerializable(typeof(NullPropertyName_TestClass))]
        [YamlSerializable(typeof(ObjectPropertyNamesDifferentByCaseOnly_TestClass))]
        [YamlSerializable(typeof(OverridePropertyNameDesignTime_TestClass))]
        [YamlSerializable(typeof(SimpleTestClass))]
        [YamlSerializable(typeof(ClassWithIgnoredCaseInsensitiveConflict))]
        [YamlSerializable(typeof(ClassWithCamelCaseNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithMemberNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithMixedNamingPolicies))]
        [YamlSerializable(typeof(ClassWithNamingPolicyAndPropertyName))]
        [YamlSerializable(typeof(ClassWithCustomDerivedNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithCustomDerivedMemberNamingPolicyAttribute))]
        internal sealed partial class PropertyNameTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed partial class PropertyNameTests_Default : PropertyNameTests
    {
        public PropertyNameTests_Default()
            : base(new StringSerializerWrapper(PropertyNameTestsContext_Default.Default))
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_FallsBackToClrName()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_FallsBackToClrName()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedMemberNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_PreventsGlobalPolicyFromApplying()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_PreventsGlobalPolicyFromApplying()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedMemberNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"MyValue":"test"}""", yaml);
        }

        [YamlSerializable(typeof(Dictionary<string, OverridePropertyNameDesignTime_TestClass>))]
        [YamlSerializable(typeof(Dictionary<string, int>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(ClassWithSpecialCharacters))]
        [YamlSerializable(typeof(ClassWithPropertyNamePermutations))]
        [YamlSerializable(typeof(ClassWithUnicodeProperty))]
        [YamlSerializable(typeof(DuplicatePropertyNameDesignTime_TestClass))]
        [YamlSerializable(typeof(EmptyPropertyName_TestClass))]
        [YamlSerializable(typeof(IntPropertyNamesDifferentByCaseOnly_TestClass))]
        [YamlSerializable(typeof(NullPropertyName_TestClass))]
        [YamlSerializable(typeof(ObjectPropertyNamesDifferentByCaseOnly_TestClass))]
        [YamlSerializable(typeof(OverridePropertyNameDesignTime_TestClass))]
        [YamlSerializable(typeof(SimpleTestClass))]
        [YamlSerializable(typeof(ClassWithIgnoredCaseInsensitiveConflict))]
        [YamlSerializable(typeof(ClassWithCamelCaseNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithMemberNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithMixedNamingPolicies))]
        [YamlSerializable(typeof(ClassWithNamingPolicyAndPropertyName))]
        [YamlSerializable(typeof(ClassWithCustomDerivedNamingPolicyAttribute))]
        [YamlSerializable(typeof(ClassWithCustomDerivedMemberNamingPolicyAttribute))]
        internal sealed partial class PropertyNameTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
