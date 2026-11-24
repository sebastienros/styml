// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Specifies compile-time source generator configuration when applied to <see cref="YamlSerializerContext"/> class declarations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class YamlSourceGenerationOptionsAttribute : YamlAttribute
    {
        /// <summary>
        /// Constructs a new <see cref="YamlSourceGenerationOptionsAttribute"/> instance.
        /// </summary>
        public YamlSourceGenerationOptionsAttribute() { }

        /// <summary>
        /// Constructs a new <see cref="YamlSourceGenerationOptionsAttribute"/> instance with a predefined set of options determined by the specified <see cref="YamlSerializerDefaults"/>.
        /// </summary>
        /// <param name="defaults">The <see cref="YamlSerializerDefaults"/> to reason about.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid <paramref name="defaults"/> parameter.</exception>
        public YamlSourceGenerationOptionsAttribute(YamlSerializerDefaults defaults)
        {
            // Constructor kept in sync with equivalent overload in YamlSerializerOptions

            if (defaults is YamlSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true;
                PropertyNamingPolicy = YamlKnownNamingPolicy.CamelCase;
                NumberHandling = YamlNumberHandling.AllowReadingFromString;
            }
            else if (defaults is YamlSerializerDefaults.Strict)
            {
                UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow;
                AllowDuplicateProperties = false;
                RespectNullableAnnotations = true;
                RespectRequiredConstructorParameters = true;
            }
            else if (defaults is not YamlSerializerDefaults.General)
            {
                throw new ArgumentOutOfRangeException(nameof(defaults));
            }
        }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.AllowOutOfOrderMetadataProperties"/> when set.
        /// </summary>
        public bool AllowOutOfOrderMetadataProperties { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.AllowTrailingCommas"/> when set.
        /// </summary>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.Converters"/> when set.
        /// </summary>
        public Type[]? Converters { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DefaultBufferSize"/> when set.
        /// </summary>
        public int DefaultBufferSize { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DefaultIgnoreCondition"/> when set.
        /// </summary>
        public YamlIgnoreCondition DefaultIgnoreCondition { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DictionaryKeyPolicy"/> when set.
        /// </summary>
        public YamlKnownNamingPolicy DictionaryKeyPolicy { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IgnoreReadOnlyFields"/> when set.
        /// </summary>
        public bool IgnoreReadOnlyFields { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IgnoreReadOnlyProperties"/> when set.
        /// </summary>
        public bool IgnoreReadOnlyProperties { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IncludeFields"/> when set.
        /// </summary>
        public bool IncludeFields { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.MaxDepth"/> when set.
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.NumberHandling"/> when set.
        /// </summary>
        public YamlNumberHandling NumberHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.PreferredObjectCreationHandling"/> when set.
        /// </summary>
        public YamlObjectCreationHandling PreferredObjectCreationHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.PropertyNameCaseInsensitive"/> when set.
        /// </summary>
        public bool PropertyNameCaseInsensitive { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.PropertyNamingPolicy"/> when set.
        /// </summary>
        public YamlKnownNamingPolicy PropertyNamingPolicy { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.ReadCommentHandling"/> when set.
        /// </summary>
        public YamlCommentHandling ReadCommentHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.ReferenceHandler"/> when set.
        /// </summary>
        public YamlKnownReferenceHandler ReferenceHandler { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.RespectNullableAnnotations"/> when set.
        /// </summary>
        public bool RespectNullableAnnotations { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.RespectRequiredConstructorParameters"/> when set.
        /// </summary>
        public bool RespectRequiredConstructorParameters { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.TypeClassifiers"/> when set.
        /// </summary>
        public Type[]? TypeClassifiers { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.UnknownTypeHandling"/> when set.
        /// </summary>
        public YamlUnknownTypeHandling UnknownTypeHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.UnmappedMemberHandling"/> when set.
        /// </summary>
        public YamlUnmappedMemberHandling UnmappedMemberHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.WriteIndented"/> when set.
        /// </summary>
        public bool WriteIndented { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IndentCharacter"/> when set.
        /// </summary>
        public char IndentCharacter { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IndentCharacter"/> when set.
        /// </summary>
        public int IndentSize { get; set; }

        /// <summary>
        /// Specifies the default source generation mode for type declarations that don't set a <see cref="YamlSerializableAttribute.GenerationMode"/>.
        /// </summary>
        public YamlSourceGenerationMode GenerationMode { get; set; }

        /// <summary>
        /// Instructs the source generator to default to <see cref="YamlStringEnumConverter"/>
        /// instead of numeric serialization for all enum types encountered in its type graph.
        /// </summary>
        public bool UseStringEnumConverter { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.NewLine"/> when set.
        /// </summary>
        public string? NewLine { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.AllowDuplicateProperties"/> when set.
        /// </summary>
        public bool AllowDuplicateProperties { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.InferClosedTypePolymorphism"/> when set.
        /// </summary>
        /// <remarks>
        /// If a type declares one or more <see cref="YamlDerivedTypeAttribute"/> registrations, inference is
        /// skipped for that type and only the explicitly registered derived types are used. Polymorphism
        /// configuration declared on derived types applies to their respective contracts and does not affect
        /// inference for the base type. Closed derived types are expanded recursively and only terminal derived
        /// types are registered.
        /// </remarks>
        public bool InferClosedTypePolymorphism { get; set; }

        // M5f: the YAML options.

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.WriteStyle"/> when set.
        /// </summary>
        public YamlWriterStyle WriteStyle { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IndentSequences"/> when set.
        /// </summary>
        public bool IndentSequences { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.MultiLineStringStyle"/> when set.
        /// </summary>
        public YamlScalarStyle MultiLineStringStyle { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DefaultScalarStyle"/> when set.
        /// </summary>
        public YamlScalarStyle DefaultScalarStyle { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.IgnoreSourceStyles"/> when set.
        /// </summary>
        public bool IgnoreSourceStyles { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.MaxLineWidth"/> when set.
        /// </summary>
        public int MaxLineWidth { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.ExpandSequenceItems"/> when set.
        /// </summary>
        public bool ExpandSequenceItems { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.QuoteForLenientReaders"/> when set.
        /// </summary>
        public bool QuoteForLenientReaders { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.Schema"/> when set.
        /// </summary>
        public YamlSchemaKind Schema { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DuplicateKeyHandling"/> when set. When <see cref="AllowDuplicateProperties"/> is set too, this property wins.
        /// </summary>
        public YamlDuplicateKeyHandling DuplicateKeyHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.MappingOrder"/> when set. <see cref="YamlMappingOrder.Sorted"/> also sorts the generated members of the fast path.
        /// </summary>
        public YamlMappingOrder MappingOrder { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.MaxAliasExpansion"/> when set.
        /// </summary>
        public int MaxAliasExpansion { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.ResolveMergeKeys"/> when set.
        /// </summary>
        public bool ResolveMergeKeys { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.AcceptLenientScalars"/> when set.
        /// </summary>
        public bool AcceptLenientScalars { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.DisallowImplicitCollectionKeys"/> when set.
        /// </summary>
        public bool DisallowImplicitCollectionKeys { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.InheritTagDirectives"/> when set.
        /// </summary>
        public bool InheritTagDirectives { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="YamlSerializerOptions.AcceptUnityStrippedMarker"/> when set.
        /// </summary>
        public bool AcceptUnityStrippedMarker { get; set; }
    }
}
