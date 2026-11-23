// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class PropertyNameTestsDynamic : PropertyNameTests
    {
        public PropertyNameTestsDynamic() : base(YamlSerializerWrapper.StringSerializer) { }

        [Fact]
        public async Task YamlNullNameAttribute()
        {
            var options = new YamlSerializerOptions();
            options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;

            // A null name in YamlPropertyNameAttribute is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new NullPropertyName_TestClass(), options));
        }

        [Fact]
        public async Task YamlNameConflictOnCaseInsensitiveFail()
        {
            string yaml = """{"myInt":1,"MyInt":2}""";

            {
                var options = new YamlSerializerOptions();
                options.PropertyNameCaseInsensitive = true;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>(yaml, options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new IntPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }
        }

        [Fact]
        public void CamelCasePolicyToleratesNullOrEmpty()
        {
            Assert.Null(YamlNamingPolicy.CamelCase.ConvertName(null));
            Assert.Equal(string.Empty, YamlNamingPolicy.CamelCase.ConvertName(string.Empty));
        }

        [Theory]
        [MemberData(nameof(YamlSeparatorNamingPolicyInstances))]
        public void InboxSeparatorNamingPolicies_ThrowsOnNullInput(YamlNamingPolicy policy)
        {
            Assert.Throws<ArgumentNullException>(() => policy.ConvertName(null));
        }

        [Theory]
        [MemberData(nameof(YamlSeparatorNamingPolicyInstances))]
        public void InboxSeparatorNamingPolicies_EmptyInput(YamlNamingPolicy policy)
        {
            Assert.Equal(string.Empty, policy.ConvertName(string.Empty));
        }

        public static IEnumerable<object[]> YamlSeparatorNamingPolicyInstances()
        {
            yield return new object[] { YamlNamingPolicy.SnakeCaseLower };
            yield return new object[] { YamlNamingPolicy.SnakeCaseUpper };
            yield return new object[] { YamlNamingPolicy.KebabCaseLower };
            yield return new object[] { YamlNamingPolicy.KebabCaseUpper };
            yield return new object[] { YamlNamingPolicy.PascalCase };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_AppliesCustomPolicy()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MYVALUE":"test"}""", yaml);
        }

        [Fact]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_Deserialize()
        {
            var obj = await Serializer.DeserializeWrapper<ClassWithCustomDerivedNamingPolicyAttribute>("""{"MYVALUE":"test"}""");
            Assert.Equal("test", obj.MyValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_AppliesCustomPolicy()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedMemberNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"MYVALUE":"test"}""", yaml);
        }

        [Fact]
        public async Task YamlNamingPolicyAttribute_CustomDerived_MemberLevel_Deserialize()
        {
            var obj = await Serializer.DeserializeWrapper<ClassWithCustomDerivedMemberNamingPolicyAttribute>("""{"MYVALUE":"test"}""");
            Assert.Equal("test", obj.MyValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_CustomDerived_TypeLevel_OverridesGlobalPolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCustomDerivedNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"MYVALUE":"test"}""", yaml);
        }
    }
}
