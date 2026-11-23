// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class UnmappedMemberHandlingTests : SerializerTests
    {
        public UnmappedMemberHandlingTests(YamlSerializerWrapper serializer) : base(serializer)
        { }

        [Theory]
        [MemberData(nameof(SkipHandling_YamlWithoutUnmappedMembers_MemberData))]
        public async Task SkipHandling_YamlWithoutUnmappedMembers_Succeeds(TypeConfiguration typeConfig, YamlInput yamlInput)
        {
            YamlTypeInfo typeInfo = ResolveTypeInfo(typeConfig);

            object result = await Serializer.DeserializeWrapper(yamlInput.yaml, typeInfo);
            IPoco poco = Assert.IsAssignableFrom<IPoco>(result);
            Assert.Equal(yamlInput.expectedId, poco.Id);
        }

        public static IEnumerable<object[]> SkipHandling_YamlWithoutUnmappedMembers_MemberData()
            => GetAllTestConfigurations()
                .Where(x => x.typeConfig.ExpectedUnmappedMemberHandling is YamlUnmappedMemberHandling.Skip)
                .Where(x => !x.yamlInput.containsUnmappedMember)
                .Select(x => new object[] { x.typeConfig, x.yamlInput });

        [Theory]
        [MemberData(nameof(SkipHandling_YamlWithUnmappedMembers_MemberData))]
        public async Task SkipHandling_YamlWithUnmappedMembers_Succeeds(TypeConfiguration typeConfig, YamlInput yamlInput)
        {
            YamlTypeInfo typeInfo = ResolveTypeInfo(typeConfig);

            object result = await Serializer.DeserializeWrapper(yamlInput.yaml, typeInfo);

            IPoco poco = Assert.IsAssignableFrom<IPoco>(result);
            Assert.Equal(yamlInput.expectedId, poco.Id);
        }

        public static IEnumerable<object[]> SkipHandling_YamlWithUnmappedMembers_MemberData()
            => GetAllTestConfigurations()
                .Where(x => x.typeConfig.ExpectedUnmappedMemberHandling is YamlUnmappedMemberHandling.Skip)
                .Where(x => x.yamlInput.containsUnmappedMember)
                .Select(x => new object[] { x.typeConfig, x.yamlInput });

        [Theory]
        [MemberData(nameof(DisallowHandling_YamlWithoutUnmappedMembers_MemberData))]
        public async Task DisallowHandling_YamlWithoutUnmappedMembers_Succeeds(TypeConfiguration typeConfig, YamlInput yamlInput)
        {
            YamlTypeInfo typeInfo = ResolveTypeInfo(typeConfig);

            object result = await Serializer.DeserializeWrapper(yamlInput.yaml, typeInfo);
            IPoco poco = Assert.IsAssignableFrom<IPoco>(result);
            Assert.Equal(yamlInput.expectedId, poco.Id);
        }

        public static IEnumerable<object[]> DisallowHandling_YamlWithoutUnmappedMembers_MemberData()
            => GetAllTestConfigurations()
                .Where(x => x.typeConfig.ExpectedUnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
                .Where(x => !x.yamlInput.containsUnmappedMember)
                .Select(x => new object[] { x.typeConfig, x.yamlInput });

        [Theory]
        [MemberData(nameof(DisallowHandling_YamlWithUnmappedMembers_MemberData))]
        public async Task DisallowHandling_YamlWithUnmappedMembers_ThrowsYamlException(TypeConfiguration typeConfig, YamlInput yamlInput)
        {
            YamlTypeInfo typeInfo = ResolveTypeInfo(typeConfig);
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper(yamlInput.yaml, typeInfo));
        }

        public static IEnumerable<object[]> DisallowHandling_YamlWithUnmappedMembers_MemberData()
            => GetAllTestConfigurations()
                .Where(x => x.typeConfig.ExpectedUnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
                .Where(x => x.yamlInput.containsUnmappedMember)
                .Select(x => new object[] { x.typeConfig, x.yamlInput });

        [Theory]
        [MemberData(nameof(YamlTypeInfo_ReturnsExpectedUnmappedMemberHandling_MemberData))]
        public void YamlTypeInfo_ReturnsExpectedUnmappedMemberHandling(TypeConfiguration typeConfig)
        {
            YamlTypeInfo typeInfo = ResolveTypeInfo(typeConfig);

            Assert.Equal(typeConfig.contractCustomizationOverride ?? typeConfig.attributeAnnotation, typeInfo.UnmappedMemberHandling);
        }

        public static IEnumerable<object[]> YamlTypeInfo_ReturnsExpectedUnmappedMemberHandling_MemberData()
            => GetTypeConfigurations().Select(x  => new object[] { x });

        private YamlTypeInfo ResolveTypeInfo(TypeConfiguration typeConfig)
        {
            var options = new YamlSerializerOptions(Serializer.DefaultOptions) { UnmappedMemberHandling = typeConfig.globalHandling };
            YamlTypeInfo typeInfo = options.GetTypeInfo(typeConfig.type);

            if (typeConfig.contractCustomizationOverride != null)
            {
                typeInfo.UnmappedMemberHandling = typeConfig.contractCustomizationOverride.Value;
            }

            return typeInfo;
        }

        #region Test Case Generators

        public record struct TypeConfiguration(
            Type type,
            YamlUnmappedMemberHandling globalHandling,
            YamlUnmappedMemberHandling? attributeAnnotation,
            YamlUnmappedMemberHandling? contractCustomizationOverride)
        {
            public YamlUnmappedMemberHandling ExpectedUnmappedMemberHandling => contractCustomizationOverride ?? attributeAnnotation ?? globalHandling;
        }

        public record struct YamlInput(string yaml, bool containsUnmappedMember = false, int expectedId = 0);

        private static IEnumerable<(TypeConfiguration typeConfig, YamlInput yamlInput)> GetAllTestConfigurations()
            => GetTypeConfigurations().CrossJoin(GetYamlInputs());

        private static IEnumerable<TypeConfiguration> GetTypeConfigurations()
            => GetTypesAndAttributeAnnotations().CrossJoin(
                    GetGlobalUnmappedMemberConfigurations(),
                    GetContractCustomizationOverrides(),
                    static (tc, globalConfig, contractOverride) => new TypeConfiguration(tc.type, globalConfig, tc.attributeAnnotation, contractOverride));

        private static IEnumerable<YamlUnmappedMemberHandling> GetGlobalUnmappedMemberConfigurations()
            => new[] { YamlUnmappedMemberHandling.Skip, YamlUnmappedMemberHandling.Disallow };

        private static IEnumerable<YamlUnmappedMemberHandling?> GetContractCustomizationOverrides()
            => new YamlUnmappedMemberHandling?[] { null, YamlUnmappedMemberHandling.Skip, YamlUnmappedMemberHandling.Disallow };

        private static IEnumerable<(Type type, YamlUnmappedMemberHandling? attributeAnnotation)> GetTypesAndAttributeAnnotations()
        {
            yield return (typeof(PocoWithoutAnnotations), null);
            yield return (typeof(PocoWithSkipAnnotation), YamlUnmappedMemberHandling.Skip);
            yield return (typeof(PocoWithDisallowAnnotation), YamlUnmappedMemberHandling.Disallow);
            yield return (typeof(PocoInheritingDisallowAnnotation), null);
        }

        private static IEnumerable<YamlInput> GetYamlInputs()
        {
            yield return new("{}");
            yield return new("""{"Id": 42}""", expectedId: 42);
            yield return new("""{"UnmappedProperty" : null}""", containsUnmappedMember: true);
            yield return new("""{"Id": 42, "UnmappedProperty" : null}""", containsUnmappedMember: true, expectedId: 42);
            yield return new("""{"UnmappedMember" : null, "Id": 42}""", containsUnmappedMember: true, expectedId: 42);
        }

        public interface IPoco
        {
            int Id { get; set; }
        }

        public class PocoWithoutAnnotations : IPoco
        {
            public int Id { get; set; }
        }

        [YamlUnmappedMemberHandling(YamlUnmappedMemberHandling.Skip)]
        public class PocoWithSkipAnnotation : IPoco
        {
            public int Id { get; set; }
        }

        [YamlUnmappedMemberHandling(YamlUnmappedMemberHandling.Disallow)]
        public class PocoWithDisallowAnnotation : IPoco
        {
            public int Id { get; set; }
        }

        public class PocoInheritingDisallowAnnotation : PocoWithDisallowAnnotation
        {
        }
        #endregion

        #region YamlExtensionData Interop

        [Fact]
        public async Task ClassWithExtensionData_GlobalDisallowHandling_ClassConfigurationOverridesGlobalSetting()
        {
            var options = new YamlSerializerOptions { UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow };
            ClassWithExtensionData result = await Serializer.DeserializeWrapper<ClassWithExtensionData>("""{"unmappedMember":null}""", options);

            Assert.NotNull(result.ExtensionData);
            Assert.True(result.ExtensionData.ContainsKey("unmappedMember"));
        }

        [Fact]
        public async Task ClassWithExtensionDataAndDisallowHandling_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.DeserializeWrapper<ClassWithExtensionDataAndDisallowHandling>("{}"));
        }

        [Fact]
        public async Task ClassWithExtensionDataAndDisallowHandling_DisableUnmappedMemberHandling_Succeeds()
        {
            YamlTypeInfo<ClassWithExtensionDataAndDisallowHandling> typeInfo = Serializer.GetTypeInfo<ClassWithExtensionDataAndDisallowHandling>(mutable: true);

            typeInfo.UnmappedMemberHandling = YamlUnmappedMemberHandling.Skip;
            ClassWithExtensionDataAndDisallowHandling result = await Serializer.DeserializeWrapper("""{"ExtensionData":{}}""", typeInfo);

            Assert.NotNull(result.ExtensionData);
        }

        public class ClassWithExtensionData
        {
            [YamlExtensionData]
            public Dictionary<string, object> ExtensionData { get; set; }
        }

        [YamlUnmappedMemberHandling(YamlUnmappedMemberHandling.Disallow)]
        public class ClassWithExtensionDataAndDisallowHandling
        {
            [YamlExtensionData]
            public Dictionary<string, object> ExtensionData { get; set; }
        }
        #endregion
    }
}
