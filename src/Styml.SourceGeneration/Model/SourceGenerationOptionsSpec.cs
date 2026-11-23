// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using SourceGenerators;

namespace System.Text.Yaml.SourceGeneration
{
    /// <summary>
    /// Models compile-time configuration of <see cref="YamlSourceGenerationOptionsAttribute"/>.
    /// Properties are made nullable to model the presence or absence of a given configuration.
    /// </summary>
    public sealed record SourceGenerationOptionsSpec
    {
        public required YamlSourceGenerationMode? GenerationMode { get; init; }

        public required YamlSerializerDefaults? Defaults { get; init; }

        public required bool? AllowOutOfOrderMetadataProperties { get; init; }

        public required bool? AllowTrailingCommas { get; init; }

        public required ImmutableEquatableArray<TypeRef>? Converters { get; init; }

        public required ImmutableEquatableArray<TypeRef>? TypeClassifiers { get; init; }

        /// <summary>
        /// The <c>[Experimental]</c> diagnostic IDs referenced by the options-level converters and type
        /// classifiers above. These are suppressed in the aggregate source files that emit the options setup.
        /// </summary>
        public required ImmutableEquatableArray<string> ExperimentalDiagnosticIds { get; init; }

        public required int? DefaultBufferSize { get; init; }

        public required YamlIgnoreCondition? DefaultIgnoreCondition { get; init; }

        public required YamlKnownNamingPolicy? DictionaryKeyPolicy { get; init; }

        public required bool? RespectNullableAnnotations { get; init; }

        public required bool? RespectRequiredConstructorParameters { get; init; }

        public required bool? IgnoreReadOnlyFields { get; init; }

        public required bool? IgnoreReadOnlyProperties { get; init; }

        public required bool? IncludeFields { get; init; }

        public required int? MaxDepth { get; init; }

        public required string? NewLine { get; init; }

        public required YamlNumberHandling? NumberHandling { get; init; }

        public required YamlObjectCreationHandling? PreferredObjectCreationHandling { get; init; }

        public required bool? PropertyNameCaseInsensitive { get; init; }

        public required YamlKnownNamingPolicy? PropertyNamingPolicy { get; init; }

        public required YamlCommentHandling? ReadCommentHandling { get; init; }

        public required YamlKnownReferenceHandler? ReferenceHandler { get; init; }

        public required YamlUnknownTypeHandling? UnknownTypeHandling { get; init; }

        public required YamlUnmappedMemberHandling? UnmappedMemberHandling { get; init; }

        public required bool? UseStringEnumConverter { get; init; }

        public required bool? WriteIndented { get; init; }

        public required char? IndentCharacter { get; init; }

        public required int? IndentSize { get; init; }

        public required bool? AllowDuplicateProperties { get; init; }

        public required bool? InferClosedTypePolymorphism { get; init; }

        // M5f.

        public required YamlWriterStyle? WriteStyle { get; init; }

        public required bool? IndentSequences { get; init; }

        public required YamlScalarStyle? MultiLineStringStyle { get; init; }

        public required YamlScalarStyle? DefaultScalarStyle { get; init; }

        public required bool? IgnoreSourceStyles { get; init; }

        public required int? MaxLineWidth { get; init; }

        public required bool? ExpandSequenceItems { get; init; }

        public required bool? QuoteForLenientReaders { get; init; }

        public required YamlSchemaKind? Schema { get; init; }

        public required YamlDuplicateKeyHandling? DuplicateKeyHandling { get; init; }

        public required YamlMappingOrder? MappingOrder { get; init; }

        public required int? MaxAliasExpansion { get; init; }

        public required bool? ResolveMergeKeys { get; init; }

        public required bool? AcceptLenientScalars { get; init; }

        public required bool? DisallowImplicitCollectionKeys { get; init; }

        public YamlNumberHandling GetEffectiveNumberHandling()
            => NumberHandling ?? (Defaults is YamlSerializerDefaults.Web ? YamlNumberHandling.AllowReadingFromString : YamlNumberHandling.Strict);

        public YamlKnownNamingPolicy? GetEffectivePropertyNamingPolicy()
            => PropertyNamingPolicy ?? (Defaults is YamlSerializerDefaults.Web ? YamlKnownNamingPolicy.CamelCase : null);
    }
}
