// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;

namespace System.Text.Yaml.SourceGeneration
{
    public sealed partial class YamlSourceGenerator
    {
        internal static class DiagnosticDescriptors
        {
            // Must be kept in sync with https://github.com/dotnet/runtime/blob/main/docs/project/list-of-diagnostics.md

            public static DiagnosticDescriptor TypeNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1030",
                title: new LocalizableResourceString(nameof(SR.TypeNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.TypeNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor DuplicateTypeName { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1031",
                title: new LocalizableResourceString(nameof(SR.DuplicateTypeNameTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.DuplicateTypeNameMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor ContextClassesMustBePartial { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1032",
                title: new LocalizableResourceString(nameof(SR.ContextClassesMustBePartialTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.ContextClassesMustBePartialMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor MultipleYamlConstructorAttribute { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1033",
                title: new LocalizableResourceString(nameof(SR.MultipleYamlConstructorAttributeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.MultipleYamlConstructorAttributeFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlStringEnumConverterNotSupportedInAot { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1034",
                title: new LocalizableResourceString(nameof(SR.YamlStringEnumConverterNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlStringEnumConverterNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor MultipleYamlExtensionDataAttribute { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1035",
                title: new LocalizableResourceString(nameof(SR.MultipleYamlExtensionDataAttributeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.MultipleYamlExtensionDataAttributeFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor DataExtensionPropertyInvalid { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1036",
                title: new LocalizableResourceString(nameof(SR.DataExtensionPropertyInvalidTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.DataExtensionPropertyInvalidFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor PolymorphismNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1039",
                title: new LocalizableResourceString(nameof(SR.FastPathPolymorphismNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.FastPathPolymorphismNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlConverterAttributeInvalidType { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1220",
                title: new LocalizableResourceString(nameof(SR.YamlConverterAttributeInvalidTypeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlConverterAttributeInvalidTypeMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlUnsupportedLanguageVersion { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1221",
                title: new LocalizableResourceString(nameof(SR.YamlUnsupportedLanguageVersionTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlUnsupportedLanguageVersionMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor DerivedYamlConverterAttributesNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1223",
                title: new LocalizableResourceString(nameof(SR.DerivedYamlConverterAttributesNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.DerivedYamlConverterAttributesNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlSerializableAttributeOnNonContextType { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1224",
                title: new LocalizableResourceString(nameof(SR.YamlSerializableAttributeOnNonContextTypeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlSerializableAttributeOnNonContextTypeMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor TypeContainsRefLikeMember { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1225",
                title: new LocalizableResourceString(nameof(SR.TypeContainsRefLikeMemberTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.TypeContainsRefLikeMemberFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlIgnoreConditionAlwaysInvalidOnType { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1226",
                title: new LocalizableResourceString(nameof(SR.YamlIgnoreConditionAlwaysInvalidOnTypeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlIgnoreConditionAlwaysInvalidOnTypeFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor UnionCaseTypesNotClassifiable { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1227",
                title: new LocalizableResourceString(nameof(SR.UnionCaseTypesNotClassifiableTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.UnionCaseTypesNotClassifiableMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor UnionTypeShapeNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1228",
                title: new LocalizableResourceString(nameof(SR.UnionTypeShapeNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.UnionTypeShapeNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor OpenGenericDerivedTypeCouldNotBeResolved { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1229",
                title: new LocalizableResourceString(nameof(SR.OpenGenericDerivedTypeCouldNotBeResolvedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.OpenGenericDerivedTypeCouldNotBeResolvedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor DerivedTypeIsNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1240",
                title: new LocalizableResourceString(nameof(SR.DerivedTypeIsNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.DerivedTypeIsNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor InferredDerivedTypeIsNotAccessible { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1241",
                title: new LocalizableResourceString(nameof(SR.InferredDerivedTypeIsNotAccessibleTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.InferredDerivedTypeIsNotAccessibleMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor DerivedTypeDiscriminatorCollision { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1242",
                title: new LocalizableResourceString(nameof(SR.DerivedTypeDiscriminatorCollisionTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.DerivedTypeDiscriminatorCollisionMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor InferClosedTypePolymorphismOnNonClosedType { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1243",
                title: new LocalizableResourceString(nameof(SR.InferClosedTypePolymorphismOnNonClosedTypeTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.InferClosedTypePolymorphismOnNonClosedTypeMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor InferClosedTypePolymorphismWithExplicitDerivedTypes { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1244",
                title: new LocalizableResourceString(nameof(SR.InferClosedTypePolymorphismWithExplicitDerivedTypesTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.InferClosedTypePolymorphismWithExplicitDerivedTypesMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            // M5d: tags as type discriminators and [YamlTagMapping].
            public static DiagnosticDescriptor YamlTagNotValid { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1250",
                title: new LocalizableResourceString(nameof(SR.YamlTagNotValidTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlTagNotValidMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlTagDuplicate { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1251",
                title: new LocalizableResourceString(nameof(SR.YamlTagDuplicateTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlTagDuplicateMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlTagMappingTypeNotSupported { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1252",
                title: new LocalizableResourceString(nameof(SR.YamlTagMappingTypeNotSupportedTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlTagMappingTypeNotSupportedMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

            public static DiagnosticDescriptor YamlTagMappingTypeMappedTwice { get; } = DiagnosticDescriptorHelper.Create(
                id: "STYML1253",
                title: new LocalizableResourceString(nameof(SR.YamlTagMappingTypeMappedTwiceTitle), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                messageFormat: new LocalizableResourceString(nameof(SR.YamlTagMappingTypeMappedTwiceMessageFormat), SR.ResourceManager, typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)),
                category: YamlConstants.SystemTextYamlSourceGenerationName,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        }
    }
}
