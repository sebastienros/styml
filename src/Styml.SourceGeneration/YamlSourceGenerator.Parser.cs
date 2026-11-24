// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerators;
using GenericTypeMetadata = (string? OpenDeclaringTypeFQN, SourceGenerators.ImmutableEquatableArray<string>? DeclaringTypeParameterNames, string? DeclaringTypeParameterConstraintClauses);

namespace System.Text.Yaml.SourceGeneration
{
    public sealed partial class YamlSourceGenerator
    {
        // The source generator requires NRT and init-only property support.
        private const LanguageVersion MinimumSupportedLanguageVersion = LanguageVersion.CSharp9;

        private sealed class Parser
        {
            private const string SystemTextYamlNamespace = "System.Text.Yaml";
            // Styml: the library assembly is 'Styml', not its namespace (upstream compares against "System.Text.Json").
            private const string StymlAssemblyName = "Styml";

            /// <summary>
            /// A <see cref="SymbolDisplayFormat"/> that renders fully qualified type names with
            /// generic type parameter constraint clauses appended
            /// (e.g., "global::NS.MyType&lt;T&gt; where T : notnull, global::NS.MyBase").
            /// </summary>
            private static readonly SymbolDisplayFormat s_fullyQualifiedWithConstraints =
                SymbolDisplayFormat.FullyQualifiedFormat.AddGenericsOptions(
                    SymbolDisplayGenericsOptions.IncludeTypeConstraints);
            private const string YamlExtensionDataAttributeFullName = "System.Text.Yaml.Serialization.YamlExtensionDataAttribute";
            private const string YamlIgnoreAttributeFullName = "System.Text.Yaml.Serialization.YamlIgnoreAttribute";
            private const string YamlIgnoreConditionFullName = "System.Text.Yaml.Serialization.YamlIgnoreCondition";
            private const string YamlIncludeAttributeFullName = "System.Text.Yaml.Serialization.YamlIncludeAttribute";
            private const string YamlNumberHandlingAttributeFullName = "System.Text.Yaml.Serialization.YamlNumberHandlingAttribute";
            private const string YamlObjectCreationHandlingAttributeFullName = "System.Text.Yaml.Serialization.YamlObjectCreationHandlingAttribute";
            private const string YamlPropertyNameAttributeFullName = "System.Text.Yaml.Serialization.YamlPropertyNameAttribute";
            private const string YamlPropertyOrderAttributeFullName = "System.Text.Yaml.Serialization.YamlPropertyOrderAttribute";
            private const string YamlRequiredAttributeFullName = "System.Text.Yaml.Serialization.YamlRequiredAttribute";
            private const string YamlCommentAttributeFullName = "System.Text.Yaml.Serialization.YamlCommentAttribute";
            private const string YamlScalarStyleAttributeFullName = "System.Text.Yaml.Serialization.YamlScalarStyleAttribute";
            private const string UnionAttributeName = "UnionAttribute";
            private const string UnionAttributeFullName = "System.Runtime.CompilerServices.UnionAttribute";

            internal const string YamlSerializableAttributeFullName = "System.Text.Yaml.Serialization.YamlSerializableAttribute";

            private readonly KnownTypeSymbols _knownSymbols;
            private readonly bool _compilationContainsCoreYamlTypes;

            // Keeps track of generated context type names
            private readonly HashSet<(string ContextName, string TypeName)> _generatedContextAndTypeNames = new();

            private readonly HashSet<ITypeSymbol> _builtInSupportTypes;
            private readonly Queue<TypeToGenerate> _typesToGenerate = new();
#pragma warning disable RS1024 // Compare symbols correctly https://github.com/dotnet/roslyn-analyzers/issues/5804
            private readonly Dictionary<ITypeSymbol, TypeGenerationSpec> _generatedTypes = new(SymbolEqualityComparer.Default);
            private readonly Dictionary<INamedTypeSymbol, GenericTypeMetadata> _genericTypeDefinitions = new(SymbolEqualityComparer.Default);
#pragma warning restore

            public List<Diagnostic> Diagnostics { get; } = new();
            private Location? _contextClassLocation;
            private YamlNumberHandling _contextNumberHandling;

            public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
            {
                Debug.Assert(_contextClassLocation != null);

                if (location is null || !_knownSymbols.Compilation.ContainsLocation(location))
                {
                    // If location is null or is a location outside of the current compilation, fall back to the location of the context class.
                    location = _contextClassLocation;
                }

                Diagnostics.Add(Diagnostic.Create(descriptor, location, messageArgs));
            }

            public Parser(KnownTypeSymbols knownSymbols)
            {
                _knownSymbols = knownSymbols;
                _compilationContainsCoreYamlTypes =
                    knownSymbols.YamlSerializerContextType != null &&
                    knownSymbols.YamlSerializableAttributeType != null &&
                    knownSymbols.YamlSourceGenerationOptionsAttributeType != null &&
                    knownSymbols.YamlConverterType != null;

                _builtInSupportTypes = (knownSymbols.BuiltInSupportTypes ??= CreateBuiltInSupportTypeSet(knownSymbols));
            }

            public ContextGenerationSpec? ParseContextGenerationSpec(ClassDeclarationSyntax contextClassDeclaration, SemanticModel semanticModel, CancellationToken cancellationToken)
            {
                if (!_compilationContainsCoreYamlTypes)
                {
                    return null;
                }

                Debug.Assert(_knownSymbols.YamlSerializerContextType != null);

                // Ensure context-scoped metadata caches are empty.
                Debug.Assert(_typesToGenerate.Count == 0);
                Debug.Assert(_generatedTypes.Count == 0);
                Debug.Assert(_genericTypeDefinitions.Count == 0);
                Debug.Assert(_contextClassLocation is null);

                INamedTypeSymbol? contextTypeSymbol = semanticModel.GetDeclaredSymbol(contextClassDeclaration, cancellationToken);
                Debug.Assert(contextTypeSymbol != null);

                _contextClassLocation = contextTypeSymbol.GetLocation();
                Debug.Assert(_contextClassLocation is not null);

                if (!_knownSymbols.YamlSerializerContextType.IsAssignableFrom(contextTypeSymbol))
                {
                    ReportDiagnostic(DiagnosticDescriptors.YamlSerializableAttributeOnNonContextType, _contextClassLocation, contextTypeSymbol.ToDisplayString());
                    return null;
                }

                // When a context class is split across multiple partial declarations with
                // [YamlSerializable] attributes on different partials, we only want to
                // generate code once (from the canonical partial) to avoid duplicate hintNames.
                if (!IsCanonicalPartialDeclaration(contextTypeSymbol, contextClassDeclaration))
                {
                    _contextClassLocation = null;
                    return null;
                }

                ParseYamlSerializerContextAttributes(contextTypeSymbol,
                    out List<TypeToGenerate>? rootSerializableTypes,
                    out SourceGenerationOptionsSpec? options,
                    out List<(string Tag, ITypeSymbol Type)>? tagMappings);

                if (rootSerializableTypes is null)
                {
                    // No types were annotated with YamlSerializableAttribute.
                    // Can only be reached if a [YamlSerializable(null)] declaration has been made.
                    // Do not emit a diagnostic since a NRT warning will also be emitted.
                    return null;
                }

                Debug.Assert(rootSerializableTypes.Count > 0);

                LanguageVersion? langVersion = _knownSymbols.Compilation.GetLanguageVersion();
                if (langVersion is null or < MinimumSupportedLanguageVersion)
                {
                    // Unsupported lang version should be the first (and only) diagnostic emitted by the generator.
                    ReportDiagnostic(DiagnosticDescriptors.YamlUnsupportedLanguageVersion, _contextClassLocation, langVersion?.ToDisplayString(), MinimumSupportedLanguageVersion.ToDisplayString());
                    return null;
                }

                if (!TryGetNestedTypeDeclarations(contextClassDeclaration, semanticModel, cancellationToken, out List<string>? classDeclarationList))
                {
                    // Class or one of its containing types is not partial so we can't add to it.
                    ReportDiagnostic(DiagnosticDescriptors.ContextClassesMustBePartial, _contextClassLocation, contextTypeSymbol.Name);
                    return null;
                }

                _contextNumberHandling = options?.GetEffectiveNumberHandling() ?? YamlNumberHandling.Strict;

                // Enqueue attribute data for spec generation
                foreach (TypeToGenerate rootSerializableType in rootSerializableTypes)
                {
                    _typesToGenerate.Enqueue(rootSerializableType);
                }

                // M5d: a mapped type is in the context, as a derived type is.
                List<TagMappingSpec>? tagMappingSpecs = null;
                if (tagMappings is not null)
                {
                    foreach ((string tag, ITypeSymbol type) in tagMappings)
                    {
                        (tagMappingSpecs ??= new()).Add(new TagMappingSpec { Tag = tag, Type = EnqueueType(type, generationMode: null) });
                    }
                }

                // Walk the transitive type graph generating specs for every encountered type.
                while (_typesToGenerate.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TypeToGenerate typeToGenerate = _typesToGenerate.Dequeue();
                    if (!_generatedTypes.ContainsKey(typeToGenerate.Type))
                    {
                        TypeGenerationSpec spec = ParseTypeGenerationSpec(typeToGenerate, contextTypeSymbol, options);
                        _generatedTypes.Add(typeToGenerate.Type, spec);
                    }
                }

                Debug.Assert(_generatedTypes.Count > 0);

                ContextGenerationSpec contextGenSpec = new()
                {
                    ContextType = new(contextTypeSymbol),
                    GeneratedTypes = _generatedTypes.Values.OrderBy(t => t.TypeRef.FullyQualifiedName).ToImmutableEquatableArray(),
                    Namespace = contextTypeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : null,
                    ContextClassDeclarations = classDeclarationList.ToImmutableEquatableArray(),
                    GeneratedOptionsSpec = options,
                    UseUpdatedMemorySafetyRules = semanticModel.UsesUpdatedMemorySafetyRules(),
                    TagMappings = tagMappingSpecs?.ToImmutableEquatableArray() ?? ImmutableEquatableArray<TagMappingSpec>.Empty,
                };

                // Clear the caches of generated metadata between the processing of context classes.
                _generatedTypes.Clear();
                _genericTypeDefinitions.Clear();
                _typesToGenerate.Clear();
                _contextClassLocation = null;
                _contextNumberHandling = default;
                return contextGenSpec;
            }

            private static bool TryGetNestedTypeDeclarations(ClassDeclarationSyntax contextClassSyntax, SemanticModel semanticModel, CancellationToken cancellationToken, [NotNullWhen(true)] out List<string>? typeDeclarations)
            {
                typeDeclarations = null;

                for (TypeDeclarationSyntax? currentType = contextClassSyntax; currentType != null; currentType = currentType.Parent as TypeDeclarationSyntax)
                {
                    StringBuilder stringBuilder = new();
                    bool isPartialType = false;

                    foreach (SyntaxToken modifier in currentType.Modifiers)
                    {
                        stringBuilder.Append(modifier.Text);
                        stringBuilder.Append(' ');
                        isPartialType |= modifier.IsKind(SyntaxKind.PartialKeyword);
                    }

                    if (!isPartialType)
                    {
                        typeDeclarations = null;
                        return false;
                    }

                    stringBuilder.Append(currentType.GetTypeKindKeyword());
                    stringBuilder.Append(' ');

                    INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(currentType, cancellationToken);
                    Debug.Assert(typeSymbol != null);

                    string typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    stringBuilder.Append(typeName);

                    (typeDeclarations ??= new()).Add(stringBuilder.ToString());
                }

                Debug.Assert(typeDeclarations?.Count > 0);
                return true;
            }

            private TypeRef EnqueueType(ITypeSymbol type, YamlSourceGenerationMode? generationMode)
            {
                // Trim compile-time erased metadata such as tuple labels and NRT annotations.
                type = _knownSymbols.Compilation.EraseCompileTimeMetadata(type);

                if (_generatedTypes.TryGetValue(type, out TypeGenerationSpec? spec))
                {
                    return spec.TypeRef;
                }

                _typesToGenerate.Enqueue(new TypeToGenerate
                {
                    Type = type,
                    Mode = generationMode,
                    TypeInfoPropertyName = null,
                    Location = type.GetLocation(),
                    AttributeLocation = null,
                });

                return new TypeRef(type);
            }

            /// <summary>
            /// Adds the <c>[Experimental]</c> diagnostic IDs declared on <paramref name="symbol"/> (if any) to
            /// <paramref name="experimentalIds"/> (allocated on first use), mirroring the generator's unconditional
            /// <c>[Obsolete]</c> suppression. When <paramref name="symbol"/> is a type, also recurses into array
            /// element types and generic type arguments from the type and its containing types, since
            /// <see cref="ISymbol.GetAttributes"/> on a constructed generic returns the definition's attributes
            /// rather than the type arguments'.
            /// </summary>
            private static void AddExperimentalDiagnosticIds(ISymbol? symbol, ref HashSet<string>? experimentalIds)
            {
                if (symbol is null)
                {
                    return;
                }

                // For type references, also gather IDs from array element types and generic type arguments.
                if (symbol is ITypeSymbol type)
                {
                    AddTypeArgumentDiagnosticIds(type, ref experimentalIds);
                }

                foreach (AttributeData attributeData in symbol.GetAttributes())
                {
                    if (IsExperimentalAttribute(attributeData.AttributeClass) &&
                        attributeData.ConstructorArguments.Length > 0 &&
                        attributeData.ConstructorArguments[0].Value is string diagnosticId &&
                        SyntaxFacts.IsValidIdentifier(diagnosticId))
                    {
                        // ExperimentalAttribute.DiagnosticId is the first (required) constructor argument.
                        // Only identifiers are safe to emit into #pragma warning disable directives.
                        (experimentalIds ??= new(StringComparer.Ordinal)).Add(diagnosticId);
                    }
                }

                static void AddTypeArgumentDiagnosticIds(ITypeSymbol type, ref HashSet<string>? experimentalIds)
                {
                    if (type is IArrayTypeSymbol arrayType)
                    {
                        AddExperimentalDiagnosticIds(arrayType.ElementType, ref experimentalIds);
                    }
                    else if (type is INamedTypeSymbol namedType)
                    {
                        for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
                        {
                            foreach (ITypeSymbol typeArgument in current.TypeArguments)
                            {
                                AddExperimentalDiagnosticIds(typeArgument, ref experimentalIds);
                            }
                        }
                    }
                }
            }

            private static bool IsExperimentalAttribute(INamedTypeSymbol? attributeClass)
                => attributeClass is
                {
                    Name: "ExperimentalAttribute",
                    ContainingNamespace:
                    {
                        Name: "CodeAnalysis",
                        ContainingNamespace:
                        {
                            Name: "Diagnostics",
                            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                        }
                    }
                };

            private void ParseYamlSerializerContextAttributes(
                INamedTypeSymbol contextClassSymbol,
                out List<TypeToGenerate>? rootSerializableTypes,
                out SourceGenerationOptionsSpec? options,
                out List<(string Tag, ITypeSymbol Type)>? tagMappings)
            {
                Debug.Assert(_knownSymbols.YamlSerializableAttributeType != null);
                Debug.Assert(_knownSymbols.YamlSourceGenerationOptionsAttributeType != null);

                rootSerializableTypes = null;
                options = null;
                tagMappings = null;

                foreach (AttributeData attributeData in contextClassSymbol.GetAttributes())
                {
                    INamedTypeSymbol? attributeClass = attributeData.AttributeClass;

                    if (SymbolEqualityComparer.Default.Equals(attributeClass, _knownSymbols.YamlSerializableAttributeType))
                    {
                        TypeToGenerate? typeToGenerate = ParseYamlSerializableAttribute(attributeData);
                        if (typeToGenerate is null)
                        {
                            continue;
                        }

                        (rootSerializableTypes ??= new()).Add(typeToGenerate.Value);
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attributeClass, _knownSymbols.YamlSourceGenerationOptionsAttributeType))
                    {
                        options = ParseYamlSourceGenerationOptionsAttribute(contextClassSymbol, attributeData);
                    }
                    else if (_knownSymbols.YamlTagMappingAttributeType is not null &&
                        SymbolEqualityComparer.Default.Equals(attributeClass, _knownSymbols.YamlTagMappingAttributeType))
                    {
                        ParseYamlTagMappingAttribute(contextClassSymbol, attributeData, ref tagMappings);
                    }
                }
            }

            /// <summary>
            /// M5d: one <c>[YamlTagMapping(tag, type)]</c>, checked as
            /// <c>YamlSerializerOptions.TagMappings</c> checks an entry. An entry with a diagnostic is not emitted.
            /// </summary>
            private void ParseYamlTagMappingAttribute(INamedTypeSymbol contextClassSymbol, AttributeData attributeData, ref List<(string Tag, ITypeSymbol Type)>? tagMappings)
            {
                if (attributeData.ConstructorArguments.Length != 2 ||
                    attributeData.ConstructorArguments[0].Value is not string tag ||
                    attributeData.ConstructorArguments[1].Value is not ITypeSymbol type)
                {
                    // A null argument: the compiler's nullable warning is enough.
                    return;
                }

                Location? location = attributeData.GetLocation();
                string context = contextClassSymbol.ToDisplayString();
                if (GetTagError(tag) is string error)
                {
                    ReportDiagnostic(DiagnosticDescriptors.YamlTagNotValid, location, tag, context, error);
                    return;
                }

                if (type is INamedTypeSymbol { IsUnboundGenericType: true } ||
                    type.IsAbstract || type.TypeKind is TypeKind.Interface or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter ||
                    type.SpecialType == SpecialType.System_Void ||
                    type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
                    (type is INamedTypeSymbol named && named.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter)))
                {
                    ReportDiagnostic(DiagnosticDescriptors.YamlTagMappingTypeNotSupported, location, tag, type.ToDisplayString());
                    return;
                }

                byte[] expanded = YamlConfiguredTag.GetExpandedUtf8(tag);
                foreach ((string existingTag, ITypeSymbol existingType) in tagMappings ??= new())
                {
                    if (YamlConfiguredTag.GetExpandedUtf8(existingTag).SequenceEqual(expanded))
                    {
                        ReportDiagnostic(DiagnosticDescriptors.YamlTagDuplicate, location, tag, context);
                        return;
                    }

                    if (SymbolEqualityComparer.Default.Equals(existingType, type))
                    {
                        ReportDiagnostic(DiagnosticDescriptors.YamlTagMappingTypeMappedTwice, location, type.ToDisplayString(), existingTag, tag);
                        return;
                    }
                }

                tagMappings.Add((tag, type));
            }

            /// <summary>Why a configured tag cannot be used, or null (the rules are YamlConfiguredTag's, as at run time).</summary>
            private static string? GetTagError(string tag) =>
                YamlConfiguredTag.IsReserved(tag) ? "it is reserved" :
                !YamlConfiguredTag.IsValid(tag) ? "it is not valid tag syntax" :
                null;

            /// <summary>
            /// Determines if the given class declaration is the canonical partial declaration
            /// for the context type. When a context class is split across multiple partial
            /// declarations with [YamlSerializable] attributes on different partials, we only
            /// want to generate code once (from the canonical partial) to avoid duplicate hintNames.
            /// The canonical partial is determined by picking the first syntax tree alphabetically
            /// by file path among all trees that have at least one [YamlSerializable] attribute.
            /// If file paths are empty or identical, comparison falls back to ordinal string order
            /// which provides deterministic behavior. If no attributes are found (edge case that
            /// shouldn't occur since this method is called from a context triggered by the attribute),
            /// the current partial is treated as canonical.
            /// </summary>
            private bool IsCanonicalPartialDeclaration(INamedTypeSymbol contextTypeSymbol, ClassDeclarationSyntax contextClassDeclaration)
            {
                Debug.Assert(_knownSymbols.YamlSerializableAttributeType != null);

                // Collect all distinct syntax trees that have [YamlSerializable] attributes for this type
                SyntaxTree? canonicalTree = null;

                foreach (AttributeData attributeData in contextTypeSymbol.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _knownSymbols.YamlSerializableAttributeType))
                    {
                        continue;
                    }

                    SyntaxTree? attributeTree = attributeData.ApplicationSyntaxReference?.SyntaxTree;
                    if (attributeTree is null)
                    {
                        continue;
                    }

                    // Pick the first tree alphabetically by file path.
                    // Empty file paths compare as less than non-empty paths with ordinal comparison.
                    if (canonicalTree is null ||
                        string.Compare(attributeTree.FilePath, canonicalTree.FilePath, StringComparison.Ordinal) < 0)
                    {
                        canonicalTree = attributeTree;
                    }
                }

                // This partial is canonical if its syntax tree is the canonical tree.
                // If canonicalTree is null (no attributes found), treat current partial as canonical.
                // This is a fallback that shouldn't normally occur since this method is called
                // from a context triggered by ForAttributeWithMetadataName for YamlSerializableAttribute.
                return canonicalTree is null || canonicalTree == contextClassDeclaration.SyntaxTree;
            }

            private SourceGenerationOptionsSpec ParseYamlSourceGenerationOptionsAttribute(INamedTypeSymbol contextType, AttributeData attributeData)
            {
                // Options-level converters and type classifiers are referenced only by the aggregate source
                // files (not tied to any single generated type), so gather any experimental IDs encountered
                // during this call into an options-scoped set that lands on the returned spec. The shared gather
                // helpers take it by ref and allocate lazily on the first ID, so the common no-experimental case
                // stays allocation-free.
                HashSet<string>? optionsExperimentalIds = null;

                YamlSourceGenerationMode? generationMode = null;
                List<TypeRef>? converters = null;
                List<TypeRef>? typeClassifiers = null;
                YamlSerializerDefaults? defaults = null;
                bool? allowOutOfOrderMetadataProperties = null;
                bool? allowTrailingCommas = null;
                int? defaultBufferSize = null;
                YamlIgnoreCondition? defaultIgnoreCondition = null;
                YamlKnownNamingPolicy? dictionaryKeyPolicy = null;
                bool? respectNullableAnnotations = null;
                bool? ignoreReadOnlyFields = null;
                bool? respectRequiredConstructorParameters = null;
                bool? ignoreReadOnlyProperties = null;
                bool? includeFields = null;
                int? maxDepth = null;
                string? newLine = null;
                YamlNumberHandling? numberHandling = null;
                YamlObjectCreationHandling? preferredObjectCreationHandling = null;
                bool? propertyNameCaseInsensitive = null;
                YamlKnownNamingPolicy? propertyNamingPolicy = null;
                YamlCommentHandling? readCommentHandling = null;
                YamlKnownReferenceHandler? referenceHandler = null;
                YamlUnknownTypeHandling? unknownTypeHandling = null;
                YamlUnmappedMemberHandling? unmappedMemberHandling = null;
                bool? useStringEnumConverter = null;
                bool? writeIndented = null;
                char? indentCharacter = null;
                int? indentSize = null;
                bool? allowDuplicateProperties = null;
                bool? inferClosedTypePolymorphism = null;
                YamlWriterStyle? writeStyle = null;
                bool? indentSequences = null;
                YamlScalarStyle? multiLineStringStyle = null;
                YamlScalarStyle? defaultScalarStyle = null;
                bool? ignoreSourceStyles = null;
                int? maxLineWidth = null;
                bool? expandSequenceItems = null;
                bool? quoteForLenientReaders = null;
                YamlSchemaKind? schema = null;
                YamlDuplicateKeyHandling? duplicateKeyHandling = null;
                YamlMappingOrder? mappingOrder = null;
                int? maxAliasExpansion = null;
                bool? resolveMergeKeys = null;
                bool? acceptLenientScalars = null;
                bool? disallowImplicitCollectionKeys = null;
                bool? inheritTagDirectives = null;

                if (attributeData.ConstructorArguments.Length > 0)
                {
                    Debug.Assert(attributeData.ConstructorArguments.Length == 1 & attributeData.ConstructorArguments[0].Type?.Name is nameof(YamlSerializerDefaults));
                    defaults = (YamlSerializerDefaults)attributeData.ConstructorArguments[0].Value!;
                }

                foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case nameof(YamlSourceGenerationOptionsAttribute.AllowOutOfOrderMetadataProperties):
                            allowOutOfOrderMetadataProperties = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.AllowTrailingCommas):
                            allowTrailingCommas = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.Converters):
                            converters = new List<TypeRef>();
                            foreach (TypedConstant element in namedArg.Value.Values)
                            {
                                var converterType = (ITypeSymbol?)element.Value;
                                TypeRef? typeRef = GetConverterTypeFromAttribute(contextType, converterType, contextType, attributeData, ref optionsExperimentalIds);
                                if (typeRef != null)
                                {
                                    converters.Add(typeRef);
                                }
                            }

                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DefaultBufferSize):
                            defaultBufferSize = (int)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DefaultIgnoreCondition):
                            defaultIgnoreCondition = (YamlIgnoreCondition)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DictionaryKeyPolicy):
                            dictionaryKeyPolicy = (YamlKnownNamingPolicy)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.RespectNullableAnnotations):
                            respectNullableAnnotations = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.RespectRequiredConstructorParameters):
                            respectRequiredConstructorParameters = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IgnoreReadOnlyFields):
                            ignoreReadOnlyFields = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IgnoreReadOnlyProperties):
                            ignoreReadOnlyProperties = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IncludeFields):
                            includeFields = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.MaxDepth):
                            maxDepth = (int)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.NewLine):
                            newLine = (string)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.NumberHandling):
                            numberHandling = (YamlNumberHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.PreferredObjectCreationHandling):
                            preferredObjectCreationHandling = (YamlObjectCreationHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.PropertyNameCaseInsensitive):
                            propertyNameCaseInsensitive = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.PropertyNamingPolicy):
                            propertyNamingPolicy = (YamlKnownNamingPolicy)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.ReadCommentHandling):
                            readCommentHandling = (YamlCommentHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.ReferenceHandler):
                            referenceHandler = (YamlKnownReferenceHandler)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.UnknownTypeHandling):
                            unknownTypeHandling = (YamlUnknownTypeHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.UnmappedMemberHandling):
                            unmappedMemberHandling = (YamlUnmappedMemberHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.UseStringEnumConverter):
                            useStringEnumConverter = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.WriteIndented):
                            writeIndented = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IndentCharacter):
                            indentCharacter = (char)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IndentSize):
                            indentSize = (int)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.GenerationMode):
                            generationMode = (YamlSourceGenerationMode)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.AllowDuplicateProperties):
                            allowDuplicateProperties = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.InferClosedTypePolymorphism):
                            inferClosedTypePolymorphism = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.WriteStyle):
                            writeStyle = (YamlWriterStyle)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IndentSequences):
                            indentSequences = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.MultiLineStringStyle):
                            multiLineStringStyle = (YamlScalarStyle)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DefaultScalarStyle):
                            defaultScalarStyle = (YamlScalarStyle)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.IgnoreSourceStyles):
                            ignoreSourceStyles = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.MaxLineWidth):
                            maxLineWidth = (int)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.ExpandSequenceItems):
                            expandSequenceItems = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.QuoteForLenientReaders):
                            quoteForLenientReaders = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.Schema):
                            schema = (YamlSchemaKind)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DuplicateKeyHandling):
                            duplicateKeyHandling = (YamlDuplicateKeyHandling)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.MappingOrder):
                            mappingOrder = (YamlMappingOrder)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.MaxAliasExpansion):
                            maxAliasExpansion = (int)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.ResolveMergeKeys):
                            resolveMergeKeys = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.AcceptLenientScalars):
                            acceptLenientScalars = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.DisallowImplicitCollectionKeys):
                            disallowImplicitCollectionKeys = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.InheritTagDirectives):
                            inheritTagDirectives = (bool)namedArg.Value.Value!;
                            break;

                        case nameof(YamlSourceGenerationOptionsAttribute.TypeClassifiers):
                            typeClassifiers = new List<TypeRef>();
                            foreach (TypedConstant element in namedArg.Value.Values)
                            {
                                var classifierType = (ITypeSymbol?)element.Value;
                                TypeRef? typeRef = GetTypeClassifierFactoryTypeFromAttribute(contextType, classifierType, contextType, attributeData, ref optionsExperimentalIds);
                                if (typeRef != null)
                                {
                                    typeClassifiers.Add(typeRef);
                                }
                            }

                            break;

                        default:
                            throw new InvalidOperationException();
                    }
                }

                return new SourceGenerationOptionsSpec
                {
                    GenerationMode = generationMode,
                    Defaults = defaults,
                    AllowOutOfOrderMetadataProperties = allowOutOfOrderMetadataProperties,
                    AllowTrailingCommas = allowTrailingCommas,
                    DefaultBufferSize = defaultBufferSize,
                    Converters = converters?.ToImmutableEquatableArray(),
                    TypeClassifiers = typeClassifiers?.ToImmutableEquatableArray(),
                    ExperimentalDiagnosticIds = optionsExperimentalIds is { Count: > 0 }
                        ? optionsExperimentalIds.OrderBy(id => id, StringComparer.Ordinal).ToImmutableEquatableArray()
                        : ImmutableEquatableArray<string>.Empty,
                    DefaultIgnoreCondition = defaultIgnoreCondition,
                    DictionaryKeyPolicy = dictionaryKeyPolicy,
                    RespectNullableAnnotations = respectNullableAnnotations,
                    RespectRequiredConstructorParameters = respectRequiredConstructorParameters,
                    IgnoreReadOnlyFields = ignoreReadOnlyFields,
                    IgnoreReadOnlyProperties = ignoreReadOnlyProperties,
                    IncludeFields = includeFields,
                    MaxDepth = maxDepth,
                    NewLine = newLine,
                    NumberHandling = numberHandling,
                    PreferredObjectCreationHandling = preferredObjectCreationHandling,
                    PropertyNameCaseInsensitive = propertyNameCaseInsensitive,
                    PropertyNamingPolicy = propertyNamingPolicy,
                    ReadCommentHandling = readCommentHandling,
                    ReferenceHandler = referenceHandler,
                    UnknownTypeHandling = unknownTypeHandling,
                    UnmappedMemberHandling = unmappedMemberHandling,
                    UseStringEnumConverter = useStringEnumConverter,
                    WriteIndented = writeIndented,
                    IndentCharacter = indentCharacter,
                    IndentSize = indentSize,
                    AllowDuplicateProperties = allowDuplicateProperties,
                    InferClosedTypePolymorphism = inferClosedTypePolymorphism,
                    WriteStyle = writeStyle,
                    IndentSequences = indentSequences,
                    MultiLineStringStyle = multiLineStringStyle,
                    DefaultScalarStyle = defaultScalarStyle,
                    IgnoreSourceStyles = ignoreSourceStyles,
                    MaxLineWidth = maxLineWidth,
                    ExpandSequenceItems = expandSequenceItems,
                    QuoteForLenientReaders = quoteForLenientReaders,
                    Schema = schema,
                    DuplicateKeyHandling = duplicateKeyHandling,
                    MappingOrder = mappingOrder,
                    MaxAliasExpansion = maxAliasExpansion,
                    ResolveMergeKeys = resolveMergeKeys,
                    AcceptLenientScalars = acceptLenientScalars,
                    DisallowImplicitCollectionKeys = disallowImplicitCollectionKeys,
                    InheritTagDirectives = inheritTagDirectives,
                };
            }

            private TypeToGenerate? ParseYamlSerializableAttribute(AttributeData attributeData)
            {
                Debug.Assert(attributeData.ConstructorArguments.Length == 1);
                var typeSymbol = (ITypeSymbol?)attributeData.ConstructorArguments[0].Value;
                if (typeSymbol is null)
                {
                    return null;
                }

                YamlSourceGenerationMode? generationMode = null;
                string? typeInfoPropertyName = null;

                foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case nameof(YamlSerializableAttribute.TypeInfoPropertyName):
                            typeInfoPropertyName = (string)namedArg.Value.Value!;
                            break;
                        case nameof(YamlSerializableAttribute.GenerationMode):
                            generationMode = (YamlSourceGenerationMode)namedArg.Value.Value!;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                Location? location = typeSymbol.GetLocation();
                Location? attributeLocation = attributeData.GetLocation();
                Debug.Assert(attributeLocation != null);

                if (location is null || !_knownSymbols.Compilation.ContainsLocation(location))
                {
                    // For symbols located outside the compilation, fall back to attribute location instead.
                    location = attributeLocation;
                }

                return new TypeToGenerate
                {
                    Type = _knownSymbols.Compilation.EraseCompileTimeMetadata(typeSymbol),
                    Mode = generationMode,
                    TypeInfoPropertyName = typeInfoPropertyName,
                    Location = location,
                    AttributeLocation = attributeLocation,
                };
            }

            private TypeGenerationSpec ParseTypeGenerationSpec(in TypeToGenerate typeToGenerate, INamedTypeSymbol contextType, SourceGenerationOptionsSpec? options)
            {
                Debug.Assert(IsSymbolAccessibleWithin(typeToGenerate.Type, within: contextType), "should not generate metadata for inaccessible types.");

                ITypeSymbol type = typeToGenerate.Type;

                // Gather this type's [Experimental] IDs into a stack-local set. Referenced types (members, ctor
                // parameters, derived types, converters, etc.) add their IDs via EnqueueType and the funnel helpers
                // as the type is parsed below; the set is allocated lazily and snapshotted into the returned spec.
                HashSet<string>? experimentalIds = null;
                AddExperimentalDiagnosticIds(type, ref experimentalIds);

                ClassType classType;
                YamlPrimitiveTypeKind? primitiveTypeKind = GetPrimitiveTypeKind(type);
                TypeRef? collectionKeyType = null;
                TypeRef? collectionValueType = null;
                TypeRef? nullableUnderlyingType = null;
                bool hasExtensionDataProperty = false;
                TypeRef? runtimeTypeRef = null;
                List<PropertyGenerationSpec>? propertySpecs = null;
                List<int>? fastPathPropertyIndices = null;
                ObjectConstructionStrategy constructionStrategy = default;
                bool constructorSetsRequiredMembers = false;
                bool constructorIsInaccessible = false;
                ParameterGenerationSpec[]? ctorParamSpecs = null;
                List<PropertyInitializerGenerationSpec>? propertyInitializerSpecs = null;
                ImmutableEquatableArray<UnionCaseSpec> unionCaseSpecs = ImmutableEquatableArray<UnionCaseSpec>.Empty;
                CollectionType collectionType = CollectionType.NotApplicable;
                string? immutableCollectionFactoryTypeFullName = null;
                bool implementsIYamlOnSerialized = false;
                bool implementsIYamlOnSerializing = false;

                ProcessTypeCustomAttributes(typeToGenerate, contextType, options,
                    ref experimentalIds,
                    out YamlNumberHandling? numberHandling,
                    out YamlUnmappedMemberHandling? unmappedMemberHandling,
                    out YamlObjectCreationHandling? preferredPropertyObjectCreationHandling,
                    out YamlKnownNamingPolicy? typeNamingPolicy,
                    out YamlIgnoreCondition? typeIgnoreCondition,
                    out bool foundYamlConverterAttribute,
                    out TypeRef? customConverterType,
                    out PolymorphismOptionsSpec? polymorphismOptions,
                    out bool hasClosedDerivedTypes,
                    out TypeRef? unionClassifierFactoryType);

                bool isPolymorphic = polymorphismOptions is not null;

                if (type is { IsRefLikeType: true } or INamedTypeSymbol { IsUnboundGenericType: true } or IErrorTypeSymbol)
                {
                    classType = ClassType.TypeUnsupportedBySourceGen;
                }
                else if (foundYamlConverterAttribute)
                {
                    classType = customConverterType != null
                        ? ClassType.TypeWithDesignTimeProvidedCustomConverter
                        : ClassType.TypeUnsupportedBySourceGen;
                }
                else if (IsBuiltInSupportType(type))
                {
                    classType = ClassType.BuiltInSupportType;
                }
                else if (IsUnsupportedType(type))
                {
                    classType = ClassType.UnsupportedType;
                }
                else if (type.IsNullableValueType(out ITypeSymbol? underlyingType))
                {
                    classType = ClassType.Nullable;
                    nullableUnderlyingType = EnqueueType(underlyingType, typeToGenerate.Mode);

                    // The generated Nullable<T> metadata references the underlying type by name.
                    AddExperimentalDiagnosticIds(underlyingType, ref experimentalIds);
                }
                else if (type.TypeKind is TypeKind.Enum)
                {
                    if (options?.UseStringEnumConverter == true)
                    {
                        Debug.Assert(_knownSymbols.YamlStringEnumConverterOfTType != null);
                        INamedTypeSymbol converterSymbol = _knownSymbols.YamlStringEnumConverterOfTType.Construct(type);

                        customConverterType = new TypeRef(converterSymbol);
                        classType = ClassType.TypeWithDesignTimeProvidedCustomConverter;
                    }
                    else
                    {
                        classType = ClassType.Enum;
                    }
                }
                else if (TryResolveCollectionType(type,
                    out ITypeSymbol? valueType,
                    out ITypeSymbol? keyType,
                    out collectionType,
                    out immutableCollectionFactoryTypeFullName,
                    out bool needsRuntimeType))
                {
                    if (!IsSymbolAccessibleWithin(valueType, within: contextType) ||
                        (keyType != null && !IsSymbolAccessibleWithin(keyType, within: contextType)))
                    {
                        classType = ClassType.UnsupportedType;
                        immutableCollectionFactoryTypeFullName = null;
                        collectionType = default;
                    }
                    else if (valueType.IsRefLikeType || keyType?.IsRefLikeType is true)
                    {
                        classType = ClassType.TypeUnsupportedBySourceGen;
                        immutableCollectionFactoryTypeFullName = null;
                        collectionType = default;
                    }
                    else
                    {
                        if (type.CanUseDefaultConstructorForDeserialization(out IMethodSymbol? defaultCtor))
                        {
                            constructionStrategy = ObjectConstructionStrategy.ParameterlessConstructor;
                            constructorSetsRequiredMembers = defaultCtor?.ContainsAttribute(_knownSymbols.SetsRequiredMembersAttributeType) == true;
                        }

                        classType = keyType != null ? ClassType.Dictionary : ClassType.Enumerable;
                        collectionValueType = EnqueueType(valueType, typeToGenerate.Mode);

                        // The generated collection metadata references the element type by name.
                        AddExperimentalDiagnosticIds(valueType, ref experimentalIds);

                        if (keyType != null)
                        {
                            collectionKeyType = EnqueueType(keyType, typeToGenerate.Mode);

                            // The generated dictionary metadata references the key type by name.
                            AddExperimentalDiagnosticIds(keyType, ref experimentalIds);

                            if (needsRuntimeType)
                            {
                                runtimeTypeRef = GetDictionaryTypeRef(keyType, valueType);
                            }
                        }
                    }
                }
                else
                {
                    bool useDefaultCtorInAnnotatedStructs = type.GetCompatibleGenericBaseType(_knownSymbols.KeyValuePair) is null;

                    if (!TryGetDeserializationConstructor(type, useDefaultCtorInAnnotatedStructs, out IMethodSymbol? constructor))
                    {
                        ReportDiagnostic(DiagnosticDescriptors.MultipleYamlConstructorAttribute, typeToGenerate.Location, type.ToDisplayString());
                    }

                    constructorIsInaccessible = constructor is not null && !IsSymbolAccessibleWithin(constructor, within: contextType);

                    classType = ClassType.Object;

                    implementsIYamlOnSerializing = _knownSymbols.IYamlOnSerializingType.IsAssignableFrom(type);
                    implementsIYamlOnSerialized = _knownSymbols.IYamlOnSerializedType.IsAssignableFrom(type);

                    ctorParamSpecs = ParseConstructorParameters(typeToGenerate, constructor, out constructionStrategy, out constructorSetsRequiredMembers, ref experimentalIds);
                    propertySpecs = ParsePropertyGenerationSpecs(contextType, typeToGenerate, typeIgnoreCondition, options, typeNamingPolicy, out hasExtensionDataProperty, out fastPathPropertyIndices, ref experimentalIds);

                    if (!constructorIsInaccessible)
                    {
                        propertyInitializerSpecs = ParsePropertyInitializers(ctorParamSpecs, propertySpecs, constructorSetsRequiredMembers, ref constructionStrategy);
                    }

                    if (IsUnionType(type) && type is INamedTypeSymbol namedUnionType)
                    {
                        classType = ClassType.Union;
                        List<(ITypeSymbol CaseType, bool IsNullable)> unionCaseTypes = GetUnionCaseTypes(namedUnionType);

                        if (unionCaseTypes.Count > 0 && HasCompatibleUnionValueProperty(namedUnionType))
                        {
                            bool[] switchArmRoles = ComputeUnionSwitchArmRoles(unionCaseTypes);

                            var resolvedUnionCaseSpecs = new List<UnionCaseSpec>(unionCaseTypes.Count);
                            for (int i = 0; i < unionCaseTypes.Count; i++)
                            {
                                (ITypeSymbol caseType, bool acceptsNull) = unionCaseTypes[i];
                                if (!IsSymbolAccessibleWithin(caseType, within: contextType))
                                {
                                    classType = ClassType.UnsupportedType;
                                    resolvedUnionCaseSpecs.Clear();
                                    break;
                                }

                                TypeRef caseTypeRef = EnqueueType(caseType, typeToGenerate.Mode);

                                // The generated union metadata references the case type by name.
                                AddExperimentalDiagnosticIds(caseType, ref experimentalIds);

                                // C# rejects Nullable<T> in `value switch` patterns (CS8116). The CLR layer
                                // boxes a Nullable<T> with HasValue=true bit-identically to a boxed T, so the
                                // generated pattern arm uses the underlying T symbol — never the source
                                // Nullable<T> string spelling. Compute this from the symbol here so the
                                // emitter never has to manipulate FQN strings.
                                ITypeSymbol patternType = caseType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableCaseType
                                    ? nullableCaseType.TypeArguments[0]
                                    : caseType;

                                TypeRef patternTypeRef = SymbolEqualityComparer.Default.Equals(patternType, caseType)
                                    ? caseTypeRef
                                    : new TypeRef(patternType);

                                resolvedUnionCaseSpecs.Add(new UnionCaseSpec
                                {
                                    CaseType = caseTypeRef,
                                    PatternType = patternTypeRef,
                                    IsNullable = acceptsNull,
                                    IsSwitchArm = switchArmRoles[i],
                                });
                            }

                            if (resolvedUnionCaseSpecs.Count > 0)
                            {
                                unionCaseSpecs = resolvedUnionCaseSpecs.ToImmutableEquatableArray();
                            }
                        }
                        else
                        {
                            ReportDiagnostic(
                                DiagnosticDescriptors.UnionTypeShapeNotSupported,
                                typeToGenerate.Location,
                                namedUnionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                        }
                    }
                }

                var typeRef = new TypeRef(type);
                string typeInfoPropertyName = typeToGenerate.TypeInfoPropertyName ?? GetTypeInfoPropertyName(type);

                if (classType is ClassType.TypeUnsupportedBySourceGen)
                {
                    ReportDiagnostic(DiagnosticDescriptors.TypeNotSupported, typeToGenerate.AttributeLocation ?? typeToGenerate.Location, type.ToDisplayString());
                }

                if (!_generatedContextAndTypeNames.Add((contextType.Name, typeInfoPropertyName)))
                {
                    // The context name/property name combination will result in a conflict in generated types.
                    // Workaround for https://github.com/dotnet/roslyn/issues/54185 by keeping track of the file names we've used.
                    ReportDiagnostic(DiagnosticDescriptors.DuplicateTypeName, typeToGenerate.AttributeLocation ?? _contextClassLocation, typeInfoPropertyName);
                    classType = ClassType.TypeUnsupportedBySourceGen;
                }

                bool canUseGenericUnsafeAccessors = type is INamedTypeSymbol { IsGenericType: true } namedConstructorType
                    && _knownSymbols.SupportsGenericUnsafeAccessors
                    && !contextType.IsGenericType
                    && namedConstructorType.ContainingType is not { IsGenericType: true };
                GenericTypeMetadata genericTypeDefinition = GetGenericTypeDefinition(type);

                return new TypeGenerationSpec
                {
                    TypeRef = typeRef,
                    OpenDeclaringTypeFQN = genericTypeDefinition.OpenDeclaringTypeFQN,
                    DeclaringTypeParameterNames = genericTypeDefinition.DeclaringTypeParameterNames,
                    DeclaringTypeParameterConstraintClauses = genericTypeDefinition.DeclaringTypeParameterConstraintClauses,
                    TypeInfoPropertyName = typeInfoPropertyName,
                    GenerationMode = typeToGenerate.Mode ?? options?.GenerationMode ?? YamlSourceGenerationMode.Default,
                    ClassType = classType,
                    PrimitiveTypeKind = primitiveTypeKind,
                    IsPolymorphic = isPolymorphic,
                    PolymorphismOptions = polymorphismOptions,
                    IsClosedTypeWithoutInferredPolymorphism =
                        polymorphismOptions is null &&
                        options?.InferClosedTypePolymorphism is not true &&
                        hasClosedDerivedTypes,
                    NumberHandling = numberHandling,
                    UnmappedMemberHandling = unmappedMemberHandling,
                    PreferredPropertyObjectCreationHandling = preferredPropertyObjectCreationHandling,
                    PropertyGenSpecs = propertySpecs?.ToImmutableEquatableArray() ?? ImmutableEquatableArray<PropertyGenerationSpec>.Empty,
                    FastPathPropertyIndices = fastPathPropertyIndices?.ToImmutableEquatableArray(),
                    UnionCaseSpecs = unionCaseSpecs,
                    UnionClassifierFactoryType = unionClassifierFactoryType,
                    PropertyInitializerSpecs = propertyInitializerSpecs?.ToImmutableEquatableArray() ?? ImmutableEquatableArray<PropertyInitializerGenerationSpec>.Empty,
                    CtorParamGenSpecs = ctorParamSpecs?.ToImmutableEquatableArray() ?? ImmutableEquatableArray<ParameterGenerationSpec>.Empty,
                    CollectionType = collectionType,
                    CollectionKeyType = collectionKeyType,
                    CollectionValueType = collectionValueType,
                    ConstructionStrategy = constructionStrategy,
                    ConstructorSetsRequiredParameters = constructorSetsRequiredMembers,
                    ConstructorIsInaccessible = constructorIsInaccessible,
                    CanUseUnsafeAccessorForConstructor = constructorIsInaccessible
                        && _knownSymbols.UnsafeAccessorAttributeType is not null
                        && (type is not INamedTypeSymbol { IsGenericType: true } || canUseGenericUnsafeAccessors),
                    NullableUnderlyingType = nullableUnderlyingType,
                    RuntimeTypeRef = runtimeTypeRef,
                    IsValueTuple = type.IsTupleType,
                    HasExtensionDataPropertyType = hasExtensionDataProperty,
                    ConverterType = customConverterType,
                    ImplementsIYamlOnSerialized = implementsIYamlOnSerialized,
                    ImplementsIYamlOnSerializing = implementsIYamlOnSerializing,
                    ImmutableCollectionFactoryMethod = DetermineImmutableCollectionFactoryMethod(immutableCollectionFactoryTypeFullName),
                    ExperimentalDiagnosticIds = experimentalIds is not { Count: > 0 }
                        ? ImmutableEquatableArray<string>.Empty
                        : experimentalIds.OrderBy(id => id, StringComparer.Ordinal).ToImmutableEquatableArray(),
                };
            }

            private void ProcessTypeCustomAttributes(
                in TypeToGenerate typeToGenerate,
                INamedTypeSymbol contextType,
                SourceGenerationOptionsSpec? options,
                ref HashSet<string>? experimentalIds,
                out YamlNumberHandling? numberHandling,
                out YamlUnmappedMemberHandling? unmappedMemberHandling,
                out YamlObjectCreationHandling? objectCreationHandling,
                out YamlKnownNamingPolicy? namingPolicy,
                out YamlIgnoreCondition? typeIgnoreCondition,
                out bool foundYamlConverterAttribute,
                out TypeRef? customConverterType,
                out PolymorphismOptionsSpec? polymorphismOptions,
                out bool hasClosedDerivedTypes,
                out TypeRef? unionClassifierFactoryType)
            {
                numberHandling = null;
                unmappedMemberHandling = null;
                objectCreationHandling = null;
                namingPolicy = null;
                typeIgnoreCondition = null;
                customConverterType = null;
                foundYamlConverterAttribute = false;
                polymorphismOptions = null;
                hasClosedDerivedTypes = false;
                unionClassifierFactoryType = null;

                bool hasPolymorphicAttribute = false;
                bool ignoreUnrecognizedTypeDiscriminators = false;
                bool? inferClosedTypePolymorphismOverride = null;
                Location? polymorphicAttributeLocation = null;
                YamlUnknownDerivedTypeHandling unknownDerivedTypeHandling = default;
                YamlTypeDiscriminatorStyle discriminatorStyle = default;
                List<Location?>? derivedTagLocations = null;
                string? typeDiscriminatorPropertyName = null;
                TypeRef? polymorphicClassifierFactoryType = null;
                List<DerivedTypeSpec>? derivedTypes = null;
                HashSet<object>? typeDiscriminators = null;
                bool hasExplicitDerivedTypeAttribute = false;
                bool hasInferredClosedTypePolymorphism = false;
                bool hasUnionTypeClassifierSpecified = options?.TypeClassifiers is { Count: > 0 };
                bool isUnionType = IsUnionType(typeToGenerate.Type);
                INamedTypeSymbol? namedUnionType = typeToGenerate.Type as INamedTypeSymbol;

                foreach (AttributeData attributeData in typeToGenerate.Type.GetAttributes())
                {
                    INamedTypeSymbol? attributeType = attributeData.AttributeClass;

                    if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlNumberHandlingAttributeType))
                    {
                        numberHandling = (YamlNumberHandling)attributeData.ConstructorArguments[0].Value!;
                        continue;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlUnmappedMemberHandlingAttributeType))
                    {
                        unmappedMemberHandling = (YamlUnmappedMemberHandling)attributeData.ConstructorArguments[0].Value!;
                        continue;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlObjectCreationHandlingAttributeType))
                    {
                        objectCreationHandling = (YamlObjectCreationHandling)attributeData.ConstructorArguments[0].Value!;
                        continue;
                    }
                    else if (_knownSymbols.YamlNamingPolicyAttributeType?.IsAssignableFrom(attributeType) == true)
                    {
                        if (attributeData.ConstructorArguments.Length == 1 &&
                            attributeData.ConstructorArguments[0].Value is int knownPolicyValue)
                        {
                            namingPolicy = (YamlKnownNamingPolicy)knownPolicyValue;
                        }
                        else
                        {
                            // The attribute uses a custom naming policy that can't be resolved at compile time.
                            // Use Unspecified to prevent the global naming policy from incorrectly applying.
                            namingPolicy = YamlKnownNamingPolicy.Unspecified;
                        }

                        continue;
                    }
                    else if (!foundYamlConverterAttribute && _knownSymbols.YamlConverterAttributeType.IsAssignableFrom(attributeType))
                    {
                        customConverterType = GetConverterTypeFromYamlConverterAttribute(contextType, typeToGenerate.Type, attributeData, ref experimentalIds);
                        foundYamlConverterAttribute = true;
                    }

                    if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlIgnoreAttributeType))
                    {
                        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs = attributeData.NamedArguments;

                        if (namedArgs.Length == 0)
                        {
                            typeIgnoreCondition = YamlIgnoreCondition.Always;
                        }
                        else if (namedArgs.Length == 1 &&
                            namedArgs[0].Value.Type?.ToDisplayString() == YamlIgnoreConditionFullName)
                        {
                            typeIgnoreCondition = (YamlIgnoreCondition)namedArgs[0].Value.Value!;
                        }

                        if (typeIgnoreCondition == YamlIgnoreCondition.Always)
                        {
                            ReportDiagnostic(DiagnosticDescriptors.YamlIgnoreConditionAlwaysInvalidOnType, typeToGenerate.Location, typeToGenerate.Type.ToDisplayString());
                            typeIgnoreCondition = null; // Reset so it doesn't affect properties
                        }
                    }

                    if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlDerivedTypeAttributeType))
                    {
                        Debug.Assert(attributeData.ConstructorArguments.Length > 0);
                        hasExplicitDerivedTypeAttribute = true;
                        ITypeSymbol derivedType = (ITypeSymbol)attributeData.ConstructorArguments[0].Value!;

                        object? typeDiscriminator = null;
                        if (attributeData.ConstructorArguments.Length == 2)
                        {
                            typeDiscriminator = attributeData.ConstructorArguments[1].Value;
                            Debug.Assert(typeDiscriminator is int or string);
                        }

                        // M5d: the derived type's tag, checked here and with the style below.
                        string? derivedTag = null;
                        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
                        {
                            if (namedArg.Key == "Tag" && namedArg.Value.Value is string tagValue)
                            {
                                derivedTag = tagValue;
                                if (GetTagError(tagValue) is string tagError)
                                {
                                    ReportDiagnostic(DiagnosticDescriptors.YamlTagNotValid, attributeData.GetLocation(), tagValue, derivedType.ToDisplayString(), tagError);
                                }
                            }
                        }

                        (derivedTagLocations ??= new()).Add(attributeData.GetLocation());
                        AddPolymorphicDerivedType(
                            typeToGenerate,
                            derivedType,
                            typeDiscriminator,
                            attributeData.GetLocation(),
                            typeToGenerate.Location,
                            ref typeDiscriminators,
                            ref experimentalIds,
                            ref derivedTypes,
                            isInferredDerivedType: false,
                            tag: derivedTag);
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlPolymorphicAttributeType))
                    {
                        hasPolymorphicAttribute = true;
                        polymorphicAttributeLocation = attributeData.GetLocation();

                        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
                        {
                            switch (namedArg.Key)
                            {
                                case "IgnoreUnrecognizedTypeDiscriminators":
                                    ignoreUnrecognizedTypeDiscriminators = (bool)namedArg.Value.Value!;
                                    break;
                                case "InferClosedTypePolymorphism":
                                    inferClosedTypePolymorphismOverride = (bool)namedArg.Value.Value!;
                                    break;
                                case "TypeDiscriminatorPropertyName":
                                    typeDiscriminatorPropertyName = (string?)namedArg.Value.Value;
                                    break;
                                case "TypeClassifier":
                                    if (namedArg.Value.Value is ITypeSymbol classifierType)
                                    {
                                        polymorphicClassifierFactoryType = GetTypeClassifierFactoryTypeFromAttribute(contextType, classifierType, typeToGenerate.Type, attributeData, ref experimentalIds);
                                    }
                                    break;
                                case "UnknownDerivedTypeHandling":
                                    unknownDerivedTypeHandling = (YamlUnknownDerivedTypeHandling)namedArg.Value.Value!;
                                    break;
                                case "DiscriminatorStyle":
                                    discriminatorStyle = (YamlTypeDiscriminatorStyle)namedArg.Value.Value!;
                                    break;
                            }
                        }
                    }
                    else if (isUnionType &&
                        namedUnionType is not null &&
                        SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.YamlUnionAttributeType))
                    {
                        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
                        {
                            if (namedArg.Key == "TypeClassifier" &&
                                namedArg.Value.Value is ITypeSymbol classifierType)
                            {
                                hasUnionTypeClassifierSpecified = true;
                                unionClassifierFactoryType = GetTypeClassifierFactoryTypeFromAttribute(contextType, classifierType, namedUnionType, attributeData, ref experimentalIds);
                                break;
                            }
                        }
                    }
                }

                // Enumerate a closed hierarchy once when it is needed either for inference or to determine
                // whether generated metadata must reject runtime-only inference. Explicit derived-type
                // registrations suppress inference, while any explicit polymorphism metadata makes the
                // runtime-only inference guard unnecessary.
                //
                // A value specified on the declaration overrides the context-wide
                // YamlSourceGenerationOptionsAttribute setting; the context-wide value applies when unset.
                bool shouldInferClosedTypePolymorphism =
                    (inferClosedTypePolymorphismOverride ?? (options?.InferClosedTypePolymorphism is true)) &&
                    !hasExplicitDerivedTypeAttribute;
                bool needsRuntimeInferenceGuard =
                    options?.InferClosedTypePolymorphism is not true &&
                    !hasPolymorphicAttribute &&
                    derivedTypes is null;

                INamedTypeSymbol? closedBaseType = null;
                if ((shouldInferClosedTypePolymorphism || needsRuntimeInferenceGuard || inferClosedTypePolymorphismOverride is true) &&
                    typeToGenerate.Type is INamedTypeSymbol namedBaseType &&
                    namedBaseType.IsClosedType())
                {
                    closedBaseType = namedBaseType;
                }

                // Enabling inference on a type that is not closed can never infer derived types, so it is
                // always a mistake -- including when the declaration carries explicit YamlDerivedTypeAttribute
                // registrations, which would otherwise mask the error behind a working hierarchy.
                if (inferClosedTypePolymorphismOverride is true && closedBaseType is null)
                {
                    ReportDiagnostic(
                        DiagnosticDescriptors.InferClosedTypePolymorphismOnNonClosedType,
                        polymorphicAttributeLocation ?? typeToGenerate.Location,
                        typeToGenerate.Type.ToDisplayString());
                }
                else if (inferClosedTypePolymorphismOverride is true && hasExplicitDerivedTypeAttribute)
                {
                    // Explicit registrations replace inference rather than adding to it, so a declaration
                    // requesting both silently drops every derived type it did not register. Only the
                    // declaration-level opt-in is reported: a context-wide opt-in is meant to be overridden
                    // by explicit registrations.
                    ReportDiagnostic(
                        DiagnosticDescriptors.InferClosedTypePolymorphismWithExplicitDerivedTypes,
                        polymorphicAttributeLocation ?? typeToGenerate.Location,
                        typeToGenerate.Type.ToDisplayString());
                }

                if ((shouldInferClosedTypePolymorphism || needsRuntimeInferenceGuard) && closedBaseType is not null)
                {
                    List<ITypeSymbol>? closedDerivedTypes = closedBaseType.GetClosedDerivedTypes();
                    // A non-null empty list represents a hierarchy with closed descendants but no terminal types.
                    hasClosedDerivedTypes = closedDerivedTypes is not null;

                    if (shouldInferClosedTypePolymorphism && closedDerivedTypes is not null)
                    {
                        hasInferredClosedTypePolymorphism = true;
                        InferClosedTypeDerivedTypes(typeToGenerate, closedDerivedTypes, ref typeDiscriminators, ref experimentalIds, ref derivedTypes);
                    }
                }

                // A declaration that explicitly opts out of inference, registers no derived types of its own,
                // and specifies no other polymorphism metadata is left non-polymorphic: YamlPolymorphicAttribute
                // is being used to exclude the type from a context-wide opt-in rather than to declare a hierarchy.
                // The generator emits an empty YamlPolymorphismOptions instance for a null spec, which the runtime
                // recognizes as 'no polymorphism metadata'; emitting a configured instance with an empty
                // registration list would instead fail configuration at run time.
                if (discriminatorStyle != YamlTypeDiscriminatorStyle.Property && derivedTypes is { Count: > 0 })
                {
                    CheckDerivedTypeTags(typeToGenerate, derivedTypes, derivedTagLocations);
                }

                bool hasNonDefaultPolymorphismSettings =
                    discriminatorStyle != YamlTypeDiscriminatorStyle.Property ||
                    ignoreUnrecognizedTypeDiscriminators ||
                    polymorphicClassifierFactoryType is not null ||
                    typeDiscriminatorPropertyName is not null ||
                    unknownDerivedTypeHandling != default;
                bool optedOutOfPolymorphism =
                    inferClosedTypePolymorphismOverride is false &&
                    derivedTypes is not { Count: > 0 } &&
                    !hasNonDefaultPolymorphismSettings;

                if (!optedOutOfPolymorphism &&
                    (hasPolymorphicAttribute || hasInferredClosedTypePolymorphism || derivedTypes is { Count: > 0 }))
                {
                    polymorphismOptions = new PolymorphismOptionsSpec
                    {
                        DerivedTypes = derivedTypes?.ToImmutableEquatableArray() ?? ImmutableEquatableArray<DerivedTypeSpec>.Empty,
                        IgnoreUnrecognizedTypeDiscriminators = ignoreUnrecognizedTypeDiscriminators,
                        TypeClassifierFactoryType = polymorphicClassifierFactoryType,
                        TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName,
                        UnknownDerivedTypeHandling = unknownDerivedTypeHandling,
                        DiscriminatorStyle = discriminatorStyle,
                    };
                }

                // Union types handled by the built-in converter need metadata for all case types.
                if (isUnionType && !foundYamlConverterAttribute)
                {
                    EnqueueUnionCaseTypes(typeToGenerate, hasUnionTypeClassifierSpecified, ref experimentalIds);
                }
            }

            /// <summary>
            /// M5d, under <c>DiscriminatorStyle</c> Tag or Both: each derived type's tag (its own, or
            /// <c>!</c> and the discriminator) is valid, needs a discriminator, and appears once in the hierarchy.
            /// </summary>
            private void CheckDerivedTypeTags(in TypeToGenerate typeToGenerate, List<DerivedTypeSpec> derivedTypes, List<Location?>? locations)
            {
                List<byte[]>? seen = null;
                for (int i = 0; i < derivedTypes.Count; i++)
                {
                    DerivedTypeSpec derived = derivedTypes[i];
                    Location? location = locations is not null && i < locations.Count ? locations[i] : typeToGenerate.Location;
                    string? tag = derived.Tag;
                    if (tag is not null && derived.TypeDiscriminator is null)
                    {
                        ReportDiagnostic(DiagnosticDescriptors.YamlTagNotValid, location, tag, derived.DerivedType.FullyQualifiedName,
                            "a tag needs a type discriminator under DiscriminatorStyle Tag or Both");
                        continue;
                    }

                    if (tag is null)
                    {
                        if (derived.TypeDiscriminator is null)
                        {
                            continue;
                        }

                        tag = "!" + (derived.TypeDiscriminator is int number ? number.ToString(CultureInfo.InvariantCulture) : (string)derived.TypeDiscriminator);
                        if (GetTagError(tag) is not null)
                        {
                            ReportDiagnostic(DiagnosticDescriptors.YamlTagNotValid, location, tag, derived.DerivedType.FullyQualifiedName,
                                "the type discriminator is not a valid tag; set YamlDerivedTypeAttribute.Tag");
                            continue;
                        }
                    }
                    else if (GetTagError(tag) is not null)
                    {
                        // Reported where the attribute was read.
                        continue;
                    }

                    byte[] expanded = YamlConfiguredTag.GetExpandedUtf8(tag);
                    if ((seen ??= new()).Any(e => e.SequenceEqual(expanded)))
                    {
                        ReportDiagnostic(DiagnosticDescriptors.YamlTagDuplicate, location, tag, typeToGenerate.Type.ToDisplayString());
                    }

                    seen.Add(expanded);
                }
            }

            private void AddPolymorphicDerivedType(
                in TypeToGenerate typeToGenerate,
                ITypeSymbol derivedType,
                object? typeDiscriminator,
                Location? derivedTypeDiagnosticLocation,
                Location? polymorphismDiagnosticLocation,
                ref HashSet<object>? typeDiscriminators,
                ref HashSet<string>? experimentalIds,
                ref List<DerivedTypeSpec>? derivedTypes,
                bool isInferredDerivedType,
                string? tag = null)
            {
                ITypeSymbol? resolvedDerivedType = derivedType;

                if (derivedType is INamedTypeSymbol { IsUnboundGenericType: true } unboundDerived)
                {
                    if (!TryResolveOpenGenericDerivedType(
                            unboundDerived, typeToGenerate.Type,
                            out resolvedDerivedType, out string? failureReason))
                    {
                        // An open generic derived type that cannot be unified against this base
                        // has no concrete type to emit. Surface it at build time and emit a
                        // guaranteed-invalid placeholder so the shared runtime resolver rejects
                        // the hierarchy, matching reflection without referencing the unresolvable type.
                        ReportDiagnostic(DiagnosticDescriptors.OpenGenericDerivedTypeCouldNotBeResolved, derivedTypeDiagnosticLocation, derivedType.ToDisplayString(), typeToGenerate.Type.ToDisplayString(), failureReason);
                    }
                }

                if (resolvedDerivedType is not null &&
                    !typeToGenerate.Type.IsAssignableFrom(resolvedDerivedType))
                {
                    // The shared runtime resolver throws DerivedTypeNotSupported for the emitted entry;
                    // surface the problem at build time and keep the entry so runtime behavior matches.
                    ReportDiagnostic(DiagnosticDescriptors.DerivedTypeIsNotSupported, derivedTypeDiagnosticLocation, resolvedDerivedType.ToDisplayString(), typeToGenerate.Type.ToDisplayString());
                }

                derivedType = resolvedDerivedType ?? _knownSymbols.ObjectType;

                // An inferred derived type must be at least as visible as the base type it is
                // being registered under; otherwise there are call sites that can see the base but
                // not the derived type, and the generated context could not reference it.
                if (isInferredDerivedType &&
                    !derivedType.IsAtLeastAsVisibleAs(typeToGenerate.Type))
                {
                    // Emit a guaranteed-invalid placeholder so the shared runtime resolver rejects
                    // the hierarchy without referencing the inaccessible type.
                    ReportDiagnostic(DiagnosticDescriptors.InferredDerivedTypeIsNotAccessible, derivedTypeDiagnosticLocation, derivedType.ToDisplayString(), typeToGenerate.Type.ToDisplayString());
                    derivedType = _knownSymbols.ObjectType;
                }

                if (typeDiscriminator is not null &&
                    !(typeDiscriminators ??= new()).Add(typeDiscriminator))
                {
                    // Emit the colliding entry anyway so the shared runtime resolver throws,
                    // matching the reflection path; surface the collision at build time.
                    ReportDiagnostic(DiagnosticDescriptors.DerivedTypeDiscriminatorCollision, derivedTypeDiagnosticLocation, typeDiscriminator, typeToGenerate.Type.ToDisplayString());
                }

                if (derivedTypes is null && typeToGenerate.Mode == YamlSourceGenerationMode.Serialization)
                {
                    ReportDiagnostic(DiagnosticDescriptors.PolymorphismNotSupported, polymorphismDiagnosticLocation, typeToGenerate.Type.ToDisplayString());
                }

                TypeRef derivedTypeRef = EnqueueType(derivedType, typeToGenerate.Mode);

                // The generated polymorphic metadata references the derived type by name.
                AddExperimentalDiagnosticIds(derivedType, ref experimentalIds);

                (derivedTypes ??= new()).Add(new DerivedTypeSpec
                {
                    DerivedType = derivedTypeRef,
                    TypeDiscriminator = typeDiscriminator,
                    Tag = tag,
                });
            }

            /// <summary>
            /// Synthesizes <see cref="DerivedTypeSpec"/> entries from a closed hierarchy's inferred
            /// derived type set, mirroring the reflection-side inference in
            /// <c>DefaultYamlTypeInfoResolver.Helpers.PopulatePolymorphismMetadata</c>. Each inferred
            /// entry uses the derived type's simple name as its string discriminator.
            /// </summary>
            private void InferClosedTypeDerivedTypes(
                in TypeToGenerate typeToGenerate,
                List<ITypeSymbol> closedTypeDerivedTypes,
                ref HashSet<object>? typeDiscriminators,
                ref HashSet<string>? experimentalIds,
                ref List<DerivedTypeSpec>? derivedTypes)
            {
                // Surface inference diagnostics at the [YamlSerializable] registration site when available
                // (mirroring the TypeNotSupported handling), so they appear on the context the author
                // controls rather than on the closed base type's declaration, which may live in another
                // file or referenced assembly.
                Location? diagnosticLocation = typeToGenerate.AttributeLocation ?? typeToGenerate.Location;

                // Order by the simple name used as the discriminator, which must be unique.
                foreach (ITypeSymbol closedDerivedType in closedTypeDerivedTypes.OrderBy(static type => type.Name, StringComparer.Ordinal))
                {
                    string discriminator = closedDerivedType.Name;
                    AddPolymorphicDerivedType(
                        typeToGenerate,
                        closedDerivedType,
                        discriminator,
                        diagnosticLocation,
                        diagnosticLocation,
                        ref typeDiscriminators,
                        ref experimentalIds,
                        ref derivedTypes,
                        isInferredDerivedType: true);
                }
            }

            /// <summary>
            /// Source-gen-side resolver: closes <paramref name="unboundDerived"/> against
            /// <paramref name="baseType"/> via structural unification at compile time.
            /// Returns <c>true</c> when the registration can be closed to a unique concrete
            /// type. Returns <c>false</c> with a localized <paramref name="failureReason"/>
            /// suitable for inclusion in a diagnostic when the derived type cannot be
            /// resolved against this particular base.
            ///
            /// IMPORTANT: This implementation MIRRORS the reflection resolver
            /// <c>DefaultYamlTypeInfoResolver.Helpers.TryResolveOpenGenericDerivedType</c>
            /// in src/System/Text/Yaml/Serialization/Metadata/DefaultYamlTypeInfoResolver.Helpers.cs.
            /// Both implementations -- the structural unbound pre-check, the per-ancestor
            /// unification, and the ambiguity detection -- must be kept in lockstep so that
            /// source-gen and reflection produce the same closed type for the same registration.
            /// Any algorithmic change here must be applied in the reflection mirror as well.
            /// </summary>
            private bool TryResolveOpenGenericDerivedType(
                INamedTypeSymbol unboundDerived,
                ITypeSymbol baseType,
                [NotNullWhen(true)] out ITypeSymbol? resolvedType,
                out string? failureReason)
            {
                resolvedType = null;
                failureReason = null;

                if (baseType is not INamedTypeSymbol { IsGenericType: true } constructedBase)
                {
                    failureReason = SR.Polymorphism_OpenGeneric_Reason_NotAssignable;
                    return false;
                }

                INamedTypeSymbol derivedDefinition = unboundDerived.OriginalDefinition;
                INamedTypeSymbol baseDefinition = constructedBase.OriginalDefinition;

                // Collect every ancestor of the derived type definition whose original
                // definition matches the base type definition. For classes there is at
                // most one ancestor; for interfaces a derived type can implement the same
                // interface definition multiple times with different type arguments.
                List<INamedTypeSymbol> matchingBases = derivedDefinition
                    .GetCompatibleGenericBaseTypes(baseDefinition)
                    .ToList();

                if (matchingBases.Count == 0)
                {
                    failureReason = SR.Polymorphism_OpenGeneric_Reason_NotAssignable;
                    return false;
                }

                // The full set of generic parameters we must bind includes the parameters
                // of the derived type definition AS WELL AS those declared by enclosing
                // generic types (Outer<T>.Derived needs T bound from Outer).
                List<ITypeParameterSymbol> requiredParams = derivedDefinition.GetAllTypeParameters();
                ImmutableArray<ITypeSymbol> constructedBaseArgs = constructedBase.TypeArguments;

                // Structural unbound pre-check: every required parameter must appear at least
                // once somewhere in some matching ancestor's type arguments. If a parameter
                // never appears at all, no closed base could ever bind it -- the derived
                // definition is malformed regardless of which closed base it is registered
                // against.
                HashSet<ITypeParameterSymbol> referencedParams = new(SymbolEqualityComparer.Default);
                foreach (INamedTypeSymbol mb in matchingBases)
                {
                    foreach (ITypeSymbol arg in mb.TypeArguments)
                    {
                        CollectReferencedParameters(arg, referencedParams);
                    }
                }
                foreach (ITypeParameterSymbol required in requiredParams)
                {
                    if (!referencedParams.Contains(required))
                    {
                        failureReason = string.Format(
                            CultureInfo.InvariantCulture,
                            SR.Polymorphism_OpenGeneric_Reason_UnboundParameter,
                            required.Name);
                        return false;
                    }
                }

                Dictionary<ITypeParameterSymbol, ITypeSymbol>? successfulSubstitution = null;
                int successCount = 0;

                foreach (INamedTypeSymbol matchingBase in matchingBases)
                {
                    ImmutableArray<ITypeSymbol> matchingBaseArgs = matchingBase.TypeArguments;
                    Debug.Assert(matchingBaseArgs.Length == constructedBaseArgs.Length,
                        "matchingBase and constructedBase share the same generic definition, so arity must match.");

                    var substitution = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(requiredParams.Count, SymbolEqualityComparer.Default);
                    bool unified = true;
                    for (int i = 0; i < matchingBaseArgs.Length; i++)
                    {
                        if (!matchingBaseArgs[i].TryUnifyWith(constructedBaseArgs[i], substitution))
                        {
                            unified = false;
                            break;
                        }
                    }

                    if (!unified)
                    {
                        continue;
                    }

                    // Unification succeeded for every position. Every required parameter must be
                    // bound; otherwise the resulting closed type would have unbound type arguments
                    // (an unspeakable type). A sibling ancestor may still bind this parameter, so
                    // failure here is not fatal -- just move on to the next matching ancestor.
                    bool allBound = true;
                    foreach (ITypeParameterSymbol p in requiredParams)
                    {
                        if (!substitution.ContainsKey(p))
                        {
                            allBound = false;
                            break;
                        }
                    }

                    if (!allBound)
                    {
                        continue;
                    }

                    successCount++;
                    if (successCount == 1)
                    {
                        successfulSubstitution = substitution;
                    }
                    else
                    {
                        failureReason = SR.Polymorphism_OpenGeneric_Reason_AmbiguousMatch;
                        return false;
                    }
                }

                if (successCount == 0 || successfulSubstitution is null)
                {
                    failureReason = SR.Polymorphism_OpenGeneric_Reason_UnificationFailed;
                    return false;
                }

                // Validate constraints up front so the generated code will compile.
                // Note: HasNotNullConstraint is not enforced because `notnull` is not a
                // runtime-enforced constraint. Reflection MakeGenericType accepts e.g.
                // string? for `where T : notnull`; source-gen must match that behavior.
                if (!_knownSymbols.Compilation.TryValidateGenericConstraints(requiredParams, successfulSubstitution, out ITypeParameterSymbol? failedParam, out ITypeSymbol? failedArg))
                {
                    failureReason = string.Format(
                        CultureInfo.InvariantCulture,
                        SR.Polymorphism_OpenGeneric_Reason_ConstraintViolation,
                        failedParam.Name,
                        failedArg?.ToDisplayString() ?? string.Empty);
                    return false;
                }

                // Build closedArgs in declaration order using the merged substitution.
                ITypeSymbol[] closedArgs = new ITypeSymbol[requiredParams.Count];
                for (int i = 0; i < requiredParams.Count; i++)
                {
                    closedArgs[i] = successfulSubstitution[requiredParams[i]];
                }

                // Note: ConstructWithEnclosingTypeArguments takes the parameters in the order
                // they appear when listed as TypeParameters on the type and on its enclosing types.
                // GetAllTypeParameters preserves that order (outer-to-inner, declaration order).
                resolvedType = derivedDefinition.ConstructWithEnclosingTypeArguments(closedArgs);
                return true;
            }

            private static void CollectReferencedParameters(ITypeSymbol pattern, HashSet<ITypeParameterSymbol> set)
            {
                switch (pattern)
                {
                    case ITypeParameterSymbol tp:
                        set.Add(tp);
                        return;

                    case IArrayTypeSymbol array:
                        CollectReferencedParameters(array.ElementType, set);
                        return;

                    case IPointerTypeSymbol pointer:
                        CollectReferencedParameters(pointer.PointedAtType, set);
                        return;

                    case INamedTypeSymbol { IsGenericType: true } named:
                        // Walk ContainingType to collect type parameters declared on enclosing
                        // generic types (e.g. T in Outer<T>.Box<U>). Roslyn's TypeArguments is
                        // leaf-only, while the reflection mirror uses Type.GetGenericArguments()
                        // which flattens enclosing + leaf. Without this recursion, the unbound
                        // pre-check would spuriously reject patterns whose only reference to a
                        // type parameter lives in the enclosing type.
                        if (named.ContainingType is { IsGenericType: true } containing)
                        {
                            CollectReferencedParameters(containing, set);
                        }

                        foreach (ITypeSymbol arg in named.TypeArguments)
                        {
                            CollectReferencedParameters(arg, set);
                        }
                        return;
                }
            }

            /// <summary>
            /// Checks whether the type is recognized as a union.
            /// </summary>
            private static bool IsUnionType(ITypeSymbol type)
            {
                if (type is not INamedTypeSymbol namedType)
                {
                    return false;
                }

                // Union types declared in the current compilation may not report the [YamlUnion] attribute,
                // so we check for the "union" keyword as well.
                if (namedType.DeclaringSyntaxReferences.Length != 0 && IsUnionSyntaxDeclaration(namedType))
                {
                    return true;
                }

                foreach (AttributeData attr in namedType.GetAttributes())
                {
                    INamedTypeSymbol? attributeType = attr.AttributeClass;
                    if (attributeType?.Name == UnionAttributeName &&
                        attributeType.ToDisplayString() == UnionAttributeFullName)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsUnionSyntaxDeclaration(INamedTypeSymbol type)
            {
                foreach (SyntaxReference syntaxReference in type.DeclaringSyntaxReferences)
                {
                    if (syntaxReference.GetSyntax() is BaseTypeDeclarationSyntax declaration)
                    {
                        foreach (SyntaxToken token in declaration.ChildTokens())
                        {
                            if (token.Text is "union" && !token.Equals(declaration.Identifier))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private bool HasCompatibleUnionValueProperty(INamedTypeSymbol unionType)
            {
                foreach (INamedTypeSymbol type in unionType.GetSortedTypeHierarchy())
                {
                    foreach (ISymbol member in type.GetMembers("Value"))
                    {
                        if (member is IPropertySymbol valueProperty &&
                            valueProperty.DeclaredAccessibility == Accessibility.Public &&
                            !valueProperty.IsStatic &&
                            valueProperty.Parameters.Length == 0 &&
                            valueProperty.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                            SymbolEqualityComparer.Default.Equals(valueProperty.Type, _knownSymbols.ObjectType))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Enqueues all case types from a union's defining members
            /// and emits diagnostics for ambiguous JSON value categories.
            /// </summary>
            private void EnqueueUnionCaseTypes(in TypeToGenerate typeToGenerate, bool hasUnionTypeClassifierSpecified, ref HashSet<string>? experimentalIds)
            {
                if (typeToGenerate.Type is not INamedTypeSymbol namedType)
                {
                    return;
                }

                List<(ITypeSymbol CaseType, bool IsNullable)> caseTypes = GetUnionCaseTypes(namedType);
                foreach ((ITypeSymbol caseType, _) in caseTypes)
                {
                    EnqueueType(caseType, typeToGenerate.Mode);

                    // The generated union metadata references the case type by name.
                    AddExperimentalDiagnosticIds(caseType, ref experimentalIds);
                }

                // Detect ambiguous case types (multiple types mapping to same JSON value category).
                if (caseTypes.Count > 1 && !hasUnionTypeClassifierSpecified)
                {
                    EmitUnionAmbiguityDiagnostics(namedType, caseTypes.Select(c => c.CaseType).ToList(), typeToGenerate.Location);
                }
            }

            private List<(ITypeSymbol CaseType, bool IsNullable)> GetUnionCaseTypes(INamedTypeSymbol namedType)
            {
                IEnumerable<IMethodSymbol> creationMembers = namedType.InstanceConstructors;

                var caseTypes = new List<ITypeSymbol>();
                var acceptsNullByCase = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

                foreach (IMethodSymbol member in creationMembers)
                {
                    if (member.Parameters.Length != 1 ||
                        member.DeclaredAccessibility != Accessibility.Public ||
                        member.Parameters[0].RefKind is not RefKind.None)
                    {
                        continue;
                    }

                    IParameterSymbol parameter = member.Parameters[0];
                    ITypeSymbol caseType = parameter.Type;

                    // Value-type Nullable<T> ctor accepts null by virtue of the type itself.
                    // Reference-type nullability comes from the parameter's nullable annotation.
                    bool acceptsNull = caseType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                        || parameter.IsNullable();

                    // One YamlUnionCaseInfo per discoverable ctor overload. Foo(T) and
                    // Foo(Nullable<T>) coexist as distinct cases (typeof(T) vs typeof(T?));
                    // token-level ambiguity is surfaced via EmitUnionAmbiguityDiagnostics at
                    // compile time and YamlTypeInfo.UnionAmbiguousValueTypes at run time.
                    if (acceptsNullByCase.ContainsKey(caseType))
                    {
                        // C# overload resolution rejects duplicate single-parameter ctors;
                        // defensive skip protects against hand-emitted IL with duplicates.
                        continue;
                    }

                    acceptsNullByCase[caseType] = acceptsNull;
                    caseTypes.Add(caseType);
                }

                List<ITypeSymbol> sorted = SortCaseTypesTopologically(caseTypes);
                var result = new List<(ITypeSymbol, bool)>(sorted.Count);
                foreach (ITypeSymbol caseType in sorted)
                {
                    result.Add((caseType, acceptsNullByCase[caseType]));
                }
                return result;
            }

            /// <summary>
            /// Computes which declared cases should contribute a <c>value switch</c>
            /// arm in the generated union constructor/deconstructor. Two cases
            /// collide when they share the same C# pattern key: the underlying type
            /// after stripping <see cref="Nullable{T}"/>. CS8116 rejects
            /// <c>Nullable&lt;T&gt;</c> in patterns and a boxed <c>Nullable&lt;T&gt;</c>
            /// with HasValue=true is bit-identical to a boxed <c>T</c> at the CLR
            /// layer, so the only valid arm shape covering both is <c>T pat =&gt; …</c>.
            /// Within each collision group only one case can be canonical; we prefer
            /// the non-<c>Nullable&lt;T&gt;</c> sibling so most-derived dispatch reports
            /// <c>typeof(T)</c> rather than <c>typeof(Nullable&lt;T&gt;)</c>.
            /// </summary>
            private static bool[] ComputeUnionSwitchArmRoles(List<(ITypeSymbol CaseType, bool IsNullable)> caseTypes)
            {
                bool[] isSwitchArm = new bool[caseTypes.Count];
                var canonicalIndexByPatternKey = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);

                for (int i = 0; i < caseTypes.Count; i++)
                {
                    ITypeSymbol caseType = caseTypes[i].CaseType;
                    ITypeSymbol patternKey = UnwrapNullable(caseType);

                    if (!canonicalIndexByPatternKey.TryGetValue(patternKey, out int existingIndex))
                    {
                        canonicalIndexByPatternKey[patternKey] = i;
                        isSwitchArm[i] = true;
                        continue;
                    }

                    ITypeSymbol existingCaseType = caseTypes[existingIndex].CaseType;
                    if (IsNullableValueType(existingCaseType) && !IsNullableValueType(caseType))
                    {
                        isSwitchArm[existingIndex] = false;
                        canonicalIndexByPatternKey[patternKey] = i;
                        isSwitchArm[i] = true;
                    }
                }

                return isSwitchArm;

                static ITypeSymbol UnwrapNullable(ITypeSymbol type)
                    => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
                        ? named.TypeArguments[0]
                        : type;

                static bool IsNullableValueType(ITypeSymbol type)
                    => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
            }

            private List<ITypeSymbol> SortCaseTypesTopologically(List<ITypeSymbol> caseTypes)
            {
                if (caseTypes.Count <= 1)
                {
                    return caseTypes;
                }

                // Use System.Void as a synthetic root: it cannot be a case type and is not
                // in a subtype relationship with any valid case type.
                ITypeSymbol root = _knownSymbols.Compilation.GetSpecialType(SpecialType.System_Void);
                ITypeSymbol[] sortedTypesWithRoot = YamlHelpers.TraverseGraphWithTopologicalSort<ITypeSymbol>(root, GetParentTypes, SymbolEqualityComparer.Default);
                Debug.Assert(sortedTypesWithRoot.Length == caseTypes.Count + 1);
                Debug.Assert(SymbolEqualityComparer.Default.Equals(sortedTypesWithRoot[0], root));

                var sortedCaseTypes = new List<ITypeSymbol>(caseTypes.Count);
                for (int i = 1; i < sortedTypesWithRoot.Length; i++)
                {
                    sortedCaseTypes.Add(sortedTypesWithRoot[i]);
                }
                return sortedCaseTypes;

                ICollection<ITypeSymbol> GetParentTypes(ITypeSymbol type)
                {
                    if (SymbolEqualityComparer.Default.Equals(type, root))
                    {
                        ITypeSymbol[] rootChildren = new ITypeSymbol[caseTypes.Count];
                        for (int i = 0; i < rootChildren.Length; i++)
                        {
                            // TraverseGraphWithTopologicalSort writes childless nodes from the
                            // end of the result, so enumerate root children in reverse to
                            // preserve declaration order for unrelated case types.
                            rootChildren[i] = caseTypes[rootChildren.Length - i - 1];
                        }

                        return rootChildren;
                    }

                    List<ITypeSymbol>? parentTypes = null;
                    foreach (ITypeSymbol candidate in caseTypes)
                    {
                        if (!SymbolEqualityComparer.Default.Equals(candidate, type) &&
                            candidate.IsAssignableFrom(type))
                        {
                            (parentTypes ??= new()).Add(candidate);
                        }
                    }

                    return parentTypes ?? (ICollection<ITypeSymbol>)Array.Empty<ITypeSymbol>();
                }
            }

            private void EmitUnionAmbiguityDiagnostics(INamedTypeSymbol unionType, List<ITypeSymbol> caseTypes, Location? location)
            {
                string unionTypeName = unionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                Dictionary<YamlValueType, List<string>> valueTypeToTypes = new();
                YamlNumberHandling? unionNumberHandling = GetNumberHandling(unionType);

                foreach (ITypeSymbol caseType in caseTypes)
                {
                    string caseTypeName = caseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    YamlNumberHandling effectiveNumberHandling = unionNumberHandling ?? GetNumberHandling(caseType) ?? _contextNumberHandling;
                    YamlValueType valueTypes = GetSupportedYamlValueTypes(caseType, effectiveNumberHandling);

                    for (int flag = 1; flag <= (int)YamlValueType.Boolean; flag <<= 1)
                    {
                        YamlValueType valueType = (YamlValueType)flag;
                        if ((valueTypes & valueType) == 0)
                        {
                            continue;
                        }

                        if (!valueTypeToTypes.TryGetValue(valueType, out List<string>? typeNames))
                        {
                            typeNames = new List<string>();
                            valueTypeToTypes[valueType] = typeNames;
                        }

                        typeNames.Add(caseTypeName);
                    }
                }

                foreach (KeyValuePair<YamlValueType, List<string>> kvp in valueTypeToTypes)
                {
                    if (kvp.Value.Count > 1)
                    {
                        ReportDiagnostic(
                            DiagnosticDescriptors.UnionCaseTypesNotClassifiable,
                            location,
                            unionTypeName,
                            $"case types {string.Join(", ", kvp.Value.ConvertAll(n => $"'{n}'"))} all serialize as JSON value type '{kvp.Key}'");
                    }
                }
            }

            // Compile-time approximation of the runtime JSON value shape classifier. The lookup
            // table here MUST stay in sync with:
            //   * src/System/Text/Yaml/Serialization/Metadata/DefaultYamlTypeInfoResolver.Converters.cs (GetDefaultSimpleConverters)
            //   * src/System/Text/Yaml/Serialization/Metadata/YamlMetadataServices.Converters.cs (the *Converter properties)
            //   * src/System/Text/Yaml/Serialization/Converters/ (each built-in converter's
            //     GetSupportedYamlValueTypes override)
            // When a built-in converter is added/removed/retargeted in any of those locations,
            // update this method as well so the union ambiguity diagnostic agrees with the
            // runtime value-shape map (YamlTypeInfo.BuildUnionValueTypeMap).
            //
            // User-defined converters are conservatively classified as potentially representing
            // every JSON value shape, matching the YamlConverter base implementation.
            private YamlValueType GetSupportedYamlValueTypes(ITypeSymbol type, YamlNumberHandling numberHandling)
            {
                if (HasCustomConverterAttribute(type))
                {
                    return YamlValueType.Any;
                }

                if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                {
                    type = nullable.TypeArguments[0];

                    if (HasCustomConverterAttribute(type))
                    {
                        return YamlValueType.Any;
                    }
                }

                if (type is INamedTypeSymbol unionType && IsUnionType(unionType))
                {
                    // The runtime skips nested union cases when building its value-shape map.
                    // Contributing no shapes keeps this compile-time ambiguity check aligned.
                    return YamlValueType.None;
                }

                // Boolean
                if (type.SpecialType is SpecialType.System_Boolean)
                {
                    return YamlValueType.Boolean;
                }

                // Numeric primitives + Half / Int128 / UInt128 / BFloat16 / Decimal32 / Decimal64 / Decimal128
                if (IsBuiltInNumericType(type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.HalfType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.Int128Type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.UInt128Type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.BFloat16Type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.Decimal32Type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.Decimal64Type) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.Decimal128Type))
                {
                    return (numberHandling & YamlNumberHandling.AllowReadingFromString) != 0
                        ? YamlValueType.Number | YamlValueType.String
                        : YamlValueType.Number;
                }

                // String-shaped built-ins:
                //   string, char, DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan,
                //   Guid, Uri, Version, byte[], Memory<byte>, ReadOnlyMemory<byte>
                if (type.SpecialType is SpecialType.System_String or SpecialType.System_Char or SpecialType.System_DateTime ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.DateTimeOffsetType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.DateOnlyType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.TimeOnlyType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.TimeSpanType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.GuidType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.UriType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.VersionType) ||
                    IsByteArray(type) ||
                    IsMemoryOfByte(type) ||
                    IsReadOnlyMemoryOfByte(type))
                {
                    return YamlValueType.String;
                }

                // Enums: default EnumConverter writes a number. A user-applied
                // [YamlConverter] override (e.g. YamlStringEnumConverter) is detected at the
                // top of this method and returns Any.
                if (type.TypeKind is TypeKind.Enum)
                {
                    return YamlValueType.Number;
                }

                // Built-ins that can represent every JSON value shape.
                if (SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlElementType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlDocumentType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlNodeType) ||
                    SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlValueType) ||
                    type.SpecialType is SpecialType.System_Object)
                {
                    return YamlValueType.Any;
                }

                if (SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlObjectType))
                {
                    return YamlValueType.Object;
                }

                if (SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlArrayType))
                {
                    return YamlValueType.Array;
                }

                // Dictionaries serialize as JSON objects. Check before the IEnumerable<T> branch
                // because Dictionary<TKey,TValue> implements IEnumerable<KeyValuePair<TKey,TValue>>.
                if (type.GetCompatibleGenericBaseType(_knownSymbols.IDictionaryOfTKeyTValueType) is not null ||
                    type.GetCompatibleGenericBaseType(_knownSymbols.IReadonlyDictionaryOfTKeyTValueType) is not null ||
                    _knownSymbols.IDictionaryType.IsAssignableFrom(type))
                {
                    return YamlValueType.Object;
                }

                // Arrays and IEnumerable<T>-implementing types serialize as JSON arrays.
                if (type is IArrayTypeSymbol ||
                    type.AllInterfaces.Any(i =>
                        i.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T))
                {
                    return YamlValueType.Array;
                }

                // Anything else (POCOs, dictionaries, etc.) defaults to Object.
                // This matches the runtime ConverterStrategy fallback in YamlTypeInfo.
                return YamlValueType.Object;
            }

            private static bool IsBuiltInNumericType(ITypeSymbol type) =>
                type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
                    SpecialType.System_Int16 or SpecialType.System_UInt16 or
                    SpecialType.System_Int32 or SpecialType.System_UInt32 or
                    SpecialType.System_Int64 or SpecialType.System_UInt64 or
                    SpecialType.System_Single or SpecialType.System_Double or
                    SpecialType.System_Decimal;

            private static bool IsByteArray(ITypeSymbol type) =>
                type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte };

            private bool IsMemoryOfByte(ITypeSymbol type) =>
                type is INamedTypeSymbol named &&
                named.TypeArguments.Length == 1 &&
                named.TypeArguments[0].SpecialType == SpecialType.System_Byte &&
                SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _knownSymbols.MemoryType);

            private bool IsReadOnlyMemoryOfByte(ITypeSymbol type) =>
                type is INamedTypeSymbol named &&
                named.TypeArguments.Length == 1 &&
                named.TypeArguments[0].SpecialType == SpecialType.System_Byte &&
                SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _knownSymbols.ReadOnlyMemoryType);

            private bool HasCustomConverterAttribute(ITypeSymbol type)
            {
                INamedTypeSymbol? converterAttr = _knownSymbols.YamlConverterAttributeType;
                if (converterAttr is null)
                {
                    return false;
                }

                foreach (AttributeData attr in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, converterAttr))
                    {
                        return true;
                    }
                }

                return false;
            }

            private YamlNumberHandling? GetNumberHandling(ITypeSymbol type)
            {
                INamedTypeSymbol? numberHandlingAttr = _knownSymbols.YamlNumberHandlingAttributeType;
                if (numberHandlingAttr is null)
                {
                    return null;
                }

                foreach (AttributeData attr in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, numberHandlingAttr) &&
                        attr.ConstructorArguments.Length > 0 &&
                        attr.ConstructorArguments[0].Value is int handlingValue)
                    {
                        return (YamlNumberHandling)handlingValue;
                    }
                }

                return null;
            }

            private bool TryResolveCollectionType(
                ITypeSymbol type,
                [NotNullWhen(true)] out ITypeSymbol? valueType,
                out ITypeSymbol? keyType,
                out CollectionType collectionType,
                out string? immutableCollectionFactoryTypeFullName,
                out bool needsRuntimeType)
            {
                INamedTypeSymbol? actualTypeToConvert;
                valueType = null;
                keyType = null;
                collectionType = default;
                immutableCollectionFactoryTypeFullName = null;
                needsRuntimeType = false;

                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _knownSymbols.MemoryType))
                {
                    Debug.Assert(!SymbolEqualityComparer.Default.Equals(type, _knownSymbols.MemoryByteType));
                    valueType = ((INamedTypeSymbol)type).TypeArguments[0];
                    collectionType = CollectionType.MemoryOfT;
                    return true;
                }

                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _knownSymbols.ReadOnlyMemoryType))
                {
                    Debug.Assert(!SymbolEqualityComparer.Default.Equals(type, _knownSymbols.ReadOnlyMemoryByteType));
                    valueType = ((INamedTypeSymbol)type).TypeArguments[0];
                    collectionType = CollectionType.ReadOnlyMemoryOfT;
                    return true;
                }

                // IAsyncEnumerable<T> takes precedence over IEnumerable.
                if (type.GetCompatibleGenericBaseType(_knownSymbols.IAsyncEnumerableOfTType) is INamedTypeSymbol iAsyncEnumerableType)
                {
                    valueType = iAsyncEnumerableType.TypeArguments[0];
                    collectionType = CollectionType.IAsyncEnumerableOfT;
                    return true;
                }

                if (!_knownSymbols.IEnumerableType.IsAssignableFrom(type))
                {
                    // Type is not IEnumerable and therefore not a collection type
                    return false;
                }

                if (type is IArrayTypeSymbol arraySymbol)
                {
                    Debug.Assert(arraySymbol.Rank == 1, "multi-dimensional arrays should have been handled earlier.");
                    collectionType = CollectionType.Array;
                    valueType = arraySymbol.ElementType;
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.KeyedCollectionType)) != null)
                {
                    collectionType = CollectionType.ICollectionOfT;
                    valueType = actualTypeToConvert.TypeArguments[1];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.ListOfTType)) != null)
                {
                    collectionType = CollectionType.List;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.DictionaryOfTKeyTValueType)) != null)
                {
                    collectionType = CollectionType.Dictionary;
                    keyType = actualTypeToConvert.TypeArguments[0];
                    valueType = actualTypeToConvert.TypeArguments[1];
                }
                else if (_knownSymbols.IsImmutableDictionaryType(type, out immutableCollectionFactoryTypeFullName))
                {
                    collectionType = CollectionType.ImmutableDictionary;
                    ImmutableArray<ITypeSymbol> genericArgs = ((INamedTypeSymbol)type).TypeArguments;
                    keyType = genericArgs[0];
                    valueType = genericArgs[1];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.IDictionaryOfTKeyTValueType)) != null)
                {
                    collectionType = CollectionType.IDictionaryOfTKeyTValue;
                    keyType = actualTypeToConvert.TypeArguments[0];
                    valueType = actualTypeToConvert.TypeArguments[1];
                    needsRuntimeType = SymbolEqualityComparer.Default.Equals(type, actualTypeToConvert);
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.IReadonlyDictionaryOfTKeyTValueType)) != null)
                {
                    collectionType = CollectionType.IReadOnlyDictionary;
                    keyType = actualTypeToConvert.TypeArguments[0];
                    valueType = actualTypeToConvert.TypeArguments[1];
                    needsRuntimeType = SymbolEqualityComparer.Default.Equals(type, actualTypeToConvert);
                }
                else if (_knownSymbols.IsImmutableEnumerableType(type, out immutableCollectionFactoryTypeFullName))
                {
                    collectionType = CollectionType.ImmutableEnumerable;
                    valueType = ((INamedTypeSymbol)type).TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.IListOfTType)) != null)
                {
                    collectionType = CollectionType.IListOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.ISetOfTType)) != null)
                {
                    collectionType = CollectionType.ISet;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.IReadOnlySetOfTType)) != null)
                {
                    collectionType = CollectionType.IReadOnlySetOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.ICollectionOfTType)) != null)
                {
                    collectionType = CollectionType.ICollectionOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.StackOfTType)) != null)
                {
                    collectionType = CollectionType.StackOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.QueueOfTType)) != null)
                {
                    collectionType = CollectionType.QueueOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.ConcurrentStackType)) != null)
                {
                    collectionType = CollectionType.ConcurrentStack;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.ConcurrentQueueType)) != null)
                {
                    collectionType = CollectionType.ConcurrentQueue;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if ((actualTypeToConvert = type.GetCompatibleGenericBaseType(_knownSymbols.IEnumerableOfTType)) != null)
                {
                    collectionType = CollectionType.IEnumerableOfT;
                    valueType = actualTypeToConvert.TypeArguments[0];
                }
                else if (_knownSymbols.IDictionaryType.IsAssignableFrom(type))
                {
                    collectionType = CollectionType.IDictionary;
                    keyType = _knownSymbols.StringType;
                    valueType = _knownSymbols.ObjectType;
                    needsRuntimeType = SymbolEqualityComparer.Default.Equals(type, actualTypeToConvert);
                }
                else if (_knownSymbols.IListType.IsAssignableFrom(type))
                {
                    collectionType = CollectionType.IList;
                    valueType = _knownSymbols.ObjectType;
                }
                else if (_knownSymbols.StackType.IsAssignableFrom(type))
                {
                    collectionType = CollectionType.Stack;
                    valueType = _knownSymbols.ObjectType;
                }
                else if (_knownSymbols.QueueType.IsAssignableFrom(type))
                {
                    collectionType = CollectionType.Queue;
                    valueType = _knownSymbols.ObjectType;
                }
                else
                {
                    collectionType = CollectionType.IEnumerable;
                    valueType = _knownSymbols.ObjectType;
                }

                return true;
            }

            private TypeRef? GetDictionaryTypeRef(ITypeSymbol keyType, ITypeSymbol valueType)
            {
                INamedTypeSymbol? dictionary = _knownSymbols.DictionaryOfTKeyTValueType?.Construct(keyType, valueType);
                return dictionary is null ? null : new TypeRef(dictionary);
            }

            private List<PropertyGenerationSpec> ParsePropertyGenerationSpecs(
                INamedTypeSymbol contextType,
                in TypeToGenerate typeToGenerate,
                YamlIgnoreCondition? typeIgnoreCondition,
                SourceGenerationOptionsSpec? options,
                YamlKnownNamingPolicy? typeNamingPolicy,
                out bool hasExtensionDataProperty,
                out List<int>? fastPathPropertyIndices,
                ref HashSet<string>? experimentalIds)
            {
                Location? typeLocation = typeToGenerate.Location;
                List<PropertyGenerationSpec> properties = new();
                PropertyHierarchyResolutionState state = new(options);
                hasExtensionDataProperty = false;
                int declaringTypeIndex = -1;

                // Walk the type hierarchy starting from the current type up to the base type(s)
                foreach (INamedTypeSymbol currentType in typeToGenerate.Type.GetSortedTypeHierarchy())
                {
                    declaringTypeIndex++;
                    var declaringTypeRef = new TypeRef(currentType);
                    ImmutableArray<ISymbol> members = currentType.GetMembers();

                    foreach (IPropertySymbol propertyInfo in members.OfType<IPropertySymbol>())
                    {
                        // Skip if:
                        if (
                            // property is static or an indexer
                            propertyInfo.IsStatic || propertyInfo.Parameters.Length > 0 ||
                            // It is overridden by a derived property
                            PropertyIsOverriddenAndIgnored(propertyInfo, state.IgnoredMembers))
                        {
                            continue;
                        }

                        AddMember(
                            declaringTypeRef,
                            memberType: propertyInfo.Type,
                            memberInfo: propertyInfo,
                            typeToGenerate.Mode,
                            ref state,
                            ref hasExtensionDataProperty,
                            ref experimentalIds);
                    }

                    foreach (IFieldSymbol fieldInfo in members.OfType<IFieldSymbol>())
                    {
                        // Skip if :
                        if (
                            // it is a static field, constant
                            fieldInfo.IsStatic || fieldInfo.IsConst ||
                            // it is a compiler-generated backing field
                            fieldInfo.AssociatedSymbol != null ||
                            // symbol represents an explicitly named tuple element
                            fieldInfo.IsExplicitlyNamedTupleElement)
                        {
                            continue;
                        }

                        AddMember(
                            declaringTypeRef,
                            memberType: fieldInfo.Type,
                            memberInfo: fieldInfo,
                            typeToGenerate.Mode,
                            ref state,
                            ref hasExtensionDataProperty,
                            ref experimentalIds);
                    }
                }

                if (state.IsPropertyOrderSpecified)
                {
                    state.Properties.StableSortByKey(i => properties[i].Order);
                }

                if (options?.MappingOrder is YamlMappingOrder.Sorted)
                {
                    // M5f: the fast path's member order, as the library sorts the metadata's when it is configured:
                    // by order, then by the written name, ordinal.
                    state.Properties.Sort((x, y) => properties[x].Order != properties[y].Order
                        ? properties[x].Order.CompareTo(properties[y].Order)
                        : string.CompareOrdinal(properties[x].EffectiveYamlPropertyName, properties[y].EffectiveYamlPropertyName));
                }

                fastPathPropertyIndices = state.HasInvalidConfigurationForFastPath ? null : state.Properties;
                return properties;

                void AddMember(
                    TypeRef declaringTypeRef,
                    ITypeSymbol memberType,
                    ISymbol memberInfo,
                    YamlSourceGenerationMode? generationMode,
                    ref PropertyHierarchyResolutionState state,
                    ref bool hasExtensionDataProperty,
                    ref HashSet<string>? experimentalIds)
                {
                    PropertyGenerationSpec? propertySpec = ParsePropertyGenerationSpec(
                        contextType,
                        declaringTypeRef,
                        declaringTypeIndex,
                        typeLocation,
                        memberType,
                        memberInfo,
                        typeIgnoreCondition,
                        ref hasExtensionDataProperty,
                        generationMode,
                        options,
                        typeNamingPolicy,
                        ref experimentalIds);

                    if (propertySpec is null)
                    {
                        // ignored invalid property
                        return;
                    }

                    AddPropertyWithConflictResolution(propertySpec, memberInfo, propertyIndex: properties.Count, ref state);
                    properties.Add(propertySpec);
                }

                bool PropertyIsOverriddenAndIgnored(IPropertySymbol property, Dictionary<string, ISymbol>? ignoredMembers)
                {
                    return property.IsVirtual() &&
                        ignoredMembers?.TryGetValue(property.Name, out ISymbol? ignoredMember) == true &&
                        ignoredMember.IsVirtual() &&
                        SymbolEqualityComparer.Default.Equals(property.Type, ignoredMember.GetMemberType());
                }
            }

            private ref struct PropertyHierarchyResolutionState(SourceGenerationOptionsSpec? options)
            {
                public readonly List<int> Properties = new();
                public Dictionary<string, (PropertyGenerationSpec, ISymbol, int index)> AddedProperties = new(options?.PropertyNameCaseInsensitive == true ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                public Dictionary<string, ISymbol>? IgnoredMembers;
                public bool IsPropertyOrderSpecified;
                public bool HasInvalidConfigurationForFastPath;
            }

            /// <summary>
            /// Runs the property name conflict resolution algorithm for the fast-path serializer
            /// using the compile-time specified naming policies. Note that the metadata serializer
            /// does not consult these results since its own run-time configuration can be different.
            /// Instead the same algorithm is executed at run-time within the YamlTypeInfo infrastructure.
            /// </summary>
            private static void AddPropertyWithConflictResolution(
                PropertyGenerationSpec propertySpec,
                ISymbol memberInfo,
                int propertyIndex,
                ref PropertyHierarchyResolutionState state)
            {
                // Algorithm should be kept in sync with the runtime equivalent in YamlTypeInfo.cs
                string memberName = propertySpec.MemberName;

                if (state.AddedProperties.TryAdd(propertySpec.EffectiveYamlPropertyName, (propertySpec, memberInfo, state.Properties.Count)))
                {
                    state.Properties.Add(propertyIndex);
                    state.IsPropertyOrderSpecified |= propertySpec.Order != 0;
                }
                else
                {
                    // The YamlPropertyNameAttribute or naming policy resulted in a collision.
                    (PropertyGenerationSpec other, ISymbol otherSymbol, int index) = state.AddedProperties[propertySpec.EffectiveYamlPropertyName];

                    if (other.DefaultIgnoreCondition == YamlIgnoreCondition.Always)
                    {
                        // Overwrite previously cached property since it has [YamlIgnore].
                        state.AddedProperties[propertySpec.EffectiveYamlPropertyName] = (propertySpec, memberInfo, index);
                        state.Properties[index] = propertyIndex;
                        state.IsPropertyOrderSpecified |= propertySpec.Order != 0;
                    }
                    else
                    {
                        bool ignoreCurrentProperty =
                            // Does the current property have `YamlIgnoreAttribute`?
                            propertySpec.DefaultIgnoreCondition == YamlIgnoreCondition.Always ||
                            // Is the current property hidden by the previously cached property
                            // (with `new` keyword, or by overriding)?
                            memberInfo.IsOverriddenOrShadowedBy(otherSymbol) ||
                            // Was a property with the same CLR name ignored? That property hid the current property,
                            // thus, if it was ignored, the current property should be ignored too.
                            (state.IgnoredMembers?.TryGetValue(memberName, out ISymbol? ignored) == true && memberInfo.IsOverriddenOrShadowedBy(ignored));

                        if (!ignoreCurrentProperty)
                        {
                            // Property name conflict cannot be reconciled
                            // Signal the fast-path generator to emit a throwing stub method.
                            state.HasInvalidConfigurationForFastPath = true;
                        }
                    }
                }

                if (propertySpec.DefaultIgnoreCondition == YamlIgnoreCondition.Always)
                {
                    (state.IgnoredMembers ??= new())[memberName] = memberInfo;
                }
            }

            private bool IsValidDataExtensionPropertyType(ITypeSymbol type)
            {
                if (SymbolEqualityComparer.Default.Equals(type, _knownSymbols.YamlObjectType))
                {
                    return true;
                }

                INamedTypeSymbol? actualDictionaryType = type.GetCompatibleGenericBaseType(_knownSymbols.IDictionaryOfTKeyTValueType);
                if (actualDictionaryType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(actualDictionaryType.TypeArguments[0], _knownSymbols.StringType) &&
                        (SymbolEqualityComparer.Default.Equals(actualDictionaryType.TypeArguments[1], _knownSymbols.ObjectType) ||
                         SymbolEqualityComparer.Default.Equals(actualDictionaryType.TypeArguments[1], _knownSymbols.YamlElementType)))
                    {
                        return true;
                    }
                }

                // Also check for IReadOnlyDictionary<string, object> or IReadOnlyDictionary<string, YamlElement>
                // but only if Dictionary can be assigned to it (to exclude ImmutableDictionary and similar types)
                INamedTypeSymbol? actualReadOnlyDictionaryType = type.GetCompatibleGenericBaseType(_knownSymbols.IReadonlyDictionaryOfTKeyTValueType);
                if (actualReadOnlyDictionaryType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(actualReadOnlyDictionaryType.TypeArguments[0], _knownSymbols.StringType) &&
                        (SymbolEqualityComparer.Default.Equals(actualReadOnlyDictionaryType.TypeArguments[1], _knownSymbols.ObjectType) ||
                         SymbolEqualityComparer.Default.Equals(actualReadOnlyDictionaryType.TypeArguments[1], _knownSymbols.YamlElementType)))
                    {
                        // Check if Dictionary can be assigned to this type
                        INamedTypeSymbol? dictionaryType = SymbolEqualityComparer.Default.Equals(actualReadOnlyDictionaryType.TypeArguments[1], _knownSymbols.ObjectType)
                            ? _knownSymbols.StringObjectDictionaryType
                            : _knownSymbols.StringYamlElementDictionaryType;

                        if (dictionaryType != null)
                        {
                            Conversion conversion = _knownSymbols.Compilation.ClassifyConversion(dictionaryType, type);
                            return conversion.IsImplicit || conversion.IsIdentity;
                        }
                    }
                }

                return false;
            }

            private PropertyGenerationSpec? ParsePropertyGenerationSpec(
                INamedTypeSymbol contextType,
                TypeRef declaringType,
                int declaringTypeIndex,
                Location? typeLocation,
                ITypeSymbol memberType,
                ISymbol memberInfo,
                YamlIgnoreCondition? typeIgnoreCondition,
                ref bool typeHasExtensionDataProperty,
                YamlSourceGenerationMode? generationMode,
                SourceGenerationOptionsSpec? options,
                YamlKnownNamingPolicy? typeNamingPolicy,
                ref HashSet<string>? experimentalIds)
            {
                Debug.Assert(memberInfo is IFieldSymbol or IPropertySymbol);

                ProcessMemberCustomAttributes(
                    contextType,
                    memberInfo,
                    memberType,
                    ref experimentalIds,
                    out bool hasYamlInclude,
                    out string? yamlPropertyName,
                    out YamlIgnoreCondition? ignoreCondition,
                    out YamlNumberHandling? numberHandling,
                    out YamlObjectCreationHandling? objectCreationHandling,
                    out YamlKnownNamingPolicy? memberNamingPolicy,
                    out TypeRef? converterType,
                    out int order,
                    out bool isExtensionData,
                    out bool hasYamlRequiredAttribute,
                    out string? comment,
                    out YamlScalarStyle? scalarStyle);

                // Fall back to the type-level [YamlIgnore] if no member-level attribute is specified.
                // WhenWritingNull is invalid for non-nullable value types; treat as Never in that case
                // so that the type-level annotation still overrides the global JSO DefaultIgnoreCondition.
                if (ignoreCondition is null && typeIgnoreCondition is not null)
                {
                    ignoreCondition = typeIgnoreCondition == YamlIgnoreCondition.WhenWritingNull && !memberType.IsNullableType()
                        ? YamlIgnoreCondition.Never
                        : typeIgnoreCondition;
                }

                ProcessMember(
                    contextType,
                    memberInfo,
                    hasYamlInclude,
                    out bool isReadOnly,
                    out bool isAccessible,
                    out bool isRequired,
                    out bool canUseGetter,
                    out bool canUseSetter,
                    out bool setterIsInitOnly,
                    out bool isGetterNonNullable,
                    out bool isSetterNonNullable);

                if (isExtensionData)
                {
                    if (typeHasExtensionDataProperty)
                    {
                        ReportDiagnostic(DiagnosticDescriptors.MultipleYamlExtensionDataAttribute, typeLocation, declaringType.Name);
                    }

                    if (!IsValidDataExtensionPropertyType(memberType))
                    {
                        ReportDiagnostic(DiagnosticDescriptors.DataExtensionPropertyInvalid, memberInfo.GetLocation(), declaringType.Name, memberInfo.Name);
                    }

                    typeHasExtensionDataProperty = true;
                }

                if ((!canUseGetter && !canUseSetter && !hasYamlInclude) ||
                    !IsSymbolAccessibleWithin(memberType, within: contextType))
                {
                    // Skip the member if either of the two conditions hold
                    // 1. Member has no accessible getters or setters and is not annotated with
                    //    YamlIncludeAttribute (inaccessible [YamlInclude] members are read/written
                    //    using UnsafeAccessor or reflection) OR
                    // 2. The member type is not accessible within the generated context.
                    return null;
                }

                if (memberType.IsRefLikeType)
                {
                    // Skip all ref-like members and emit a diagnostic unless the property is being explicitly ignored.
                    if (ignoreCondition is not YamlIgnoreCondition.Always)
                    {
                        ReportDiagnostic(DiagnosticDescriptors.TypeContainsRefLikeMember, memberInfo.GetLocation(), declaringType.Name, memberInfo.Name);
                    }

                    return null;
                }

                string effectiveYamlPropertyName = DetermineEffectiveYamlPropertyName(memberInfo.Name, yamlPropertyName, memberNamingPolicy, typeNamingPolicy, options);
                string propertyNameFieldName = DeterminePropertyNameFieldName(effectiveYamlPropertyName);

                // For metadata-based serialization, embed the effective property name when a naming policy
                // attribute is present so that the runtime DeterminePropertyName uses it instead of
                // falling back to the global PropertyNamingPolicy. YamlPropertyNameAttribute values are
                // already captured in jsonPropertyName and take highest precedence.
                string? metadataYamlPropertyName = yamlPropertyName
                    ?? (memberNamingPolicy is not null || typeNamingPolicy is not null
                        ? effectiveYamlPropertyName
                        : null);

                // Enqueue the property type for generation, unless the member is ignored.
                TypeRef propertyTypeRef = ignoreCondition != YamlIgnoreCondition.Always
                    ? EnqueueType(memberType, generationMode)
                    : new TypeRef(memberType);

                if (ignoreCondition != YamlIgnoreCondition.Always)
                {
                    // The generated code accesses this member and its type, so suppress any [Experimental]
                    // diagnostic declared on the member type as well as on the member itself.
                    AddExperimentalDiagnosticIds(memberType, ref experimentalIds);
                    AddExperimentalDiagnosticIds(memberInfo, ref experimentalIds);

                    // [Experimental] can also be applied directly to the accessor(s) the generated code invokes
                    // (for example [get: Experimental(...)] / [set: Experimental(...)]).
                    if (memberInfo is IPropertySymbol property)
                    {
                        if (canUseGetter)
                        {
                            AddExperimentalDiagnosticIds(property.GetMethod, ref experimentalIds);
                        }

                        if (canUseSetter)
                        {
                            AddExperimentalDiagnosticIds(property.SetMethod, ref experimentalIds);
                        }
                    }
                }

                bool canUseGenericUnsafeAccessors = memberInfo.ContainingType.IsGenericType
                    && _knownSymbols.SupportsGenericUnsafeAccessors
                    && !contextType.IsGenericType
                    && memberInfo.ContainingType.ContainingType is not { IsGenericType: true };
                GenericTypeMetadata genericTypeDefinition = GetGenericTypeDefinition(memberInfo.ContainingType);

                return new PropertyGenerationSpec
                {
                    NameSpecifiedInSourceCode = memberInfo.MemberNameNeedsAtSign() ? "@" + memberInfo.Name : memberInfo.Name,
                    MemberName = memberInfo.Name,
                    IsProperty = memberInfo is IPropertySymbol,
                    IsPublic = isAccessible,
                    IsVirtual = memberInfo.IsVirtual(),
                    YamlPropertyName = metadataYamlPropertyName,
                    EffectiveYamlPropertyName = effectiveYamlPropertyName,
                    PropertyNameFieldName = propertyNameFieldName,
                    IsReadOnly = isReadOnly,
                    IsRequired = isRequired,
                    HasYamlRequiredAttribute = hasYamlRequiredAttribute,
                    IsInitOnlySetter = setterIsInitOnly,
                    CanUseGetter = canUseGetter,
                    CanUseSetter = canUseSetter,
                    DefaultIgnoreCondition = ignoreCondition,
                    NumberHandling = numberHandling,
                    ObjectCreationHandling = objectCreationHandling,
                    Order = order,
                    Comment = comment,
                    ScalarStyle = scalarStyle,
                    HasYamlInclude = hasYamlInclude,
                    CanUseUnsafeAccessors = _knownSymbols.UnsafeAccessorAttributeType is not null
                        && (memberInfo.ContainingType is not INamedTypeSymbol { IsGenericType: true }
                            || canUseGenericUnsafeAccessors),
                    OpenDeclaringTypeFQN = genericTypeDefinition.OpenDeclaringTypeFQN,
                    OpenPropertyTypeFQN = canUseGenericUnsafeAccessors
                        && !SymbolEqualityComparer.Default.Equals(memberType, memberInfo.OriginalDefinition.GetMemberType())
                        ? memberInfo.OriginalDefinition.GetMemberType().GetFullyQualifiedName() : null,
                    DeclaringTypeParameterNames = genericTypeDefinition.DeclaringTypeParameterNames,
                    DeclaringTypeParameterConstraintClauses = genericTypeDefinition.DeclaringTypeParameterConstraintClauses,
                    IsExtensionData = isExtensionData,
                    PropertyType = propertyTypeRef,
                    DeclaringType = declaringType,
                    DeclaringTypeIndex = declaringTypeIndex,
                    ConverterType = converterType,
                    IsGetterNonNullableAnnotation = isGetterNonNullable,
                    IsSetterNonNullableAnnotation = isSetterNonNullable,
                };
            }

            private void ProcessMemberCustomAttributes(
                INamedTypeSymbol contextType,
                ISymbol memberInfo,
                ITypeSymbol memberType,
                ref HashSet<string>? experimentalIds,
                out bool hasYamlInclude,
                out string? yamlPropertyName,
                out YamlIgnoreCondition? ignoreCondition,
                out YamlNumberHandling? numberHandling,
                out YamlObjectCreationHandling? objectCreationHandling,
                out YamlKnownNamingPolicy? memberNamingPolicy,
                out TypeRef? converterType,
                out int order,
                out bool isExtensionData,
                out bool hasYamlRequiredAttribute,
                out string? comment,
                out YamlScalarStyle? scalarStyle)
            {
                Debug.Assert(memberInfo is IFieldSymbol or IPropertySymbol);

                comment = null;
                scalarStyle = null;

                hasYamlInclude = false;
                yamlPropertyName = null;
                ignoreCondition = default;
                numberHandling = default;
                objectCreationHandling = default;
                memberNamingPolicy = default;
                converterType = null;
                order = 0;
                isExtensionData = false;
                hasYamlRequiredAttribute = false;

                foreach (AttributeData attributeData in memberInfo.GetAttributes())
                {
                    INamedTypeSymbol? attributeType = attributeData.AttributeClass;

                    if (attributeType is null)
                    {
                        continue;
                    }

                    if (converterType is null && _knownSymbols.YamlConverterAttributeType.IsAssignableFrom(attributeType))
                    {
                        converterType = GetConverterTypeFromYamlConverterAttribute(contextType, memberInfo, attributeData, ref experimentalIds, memberType);
                    }
                    else if (memberNamingPolicy is null && _knownSymbols.YamlNamingPolicyAttributeType?.IsAssignableFrom(attributeType) == true)
                    {
                        if (attributeData.ConstructorArguments.Length == 1 &&
                            attributeData.ConstructorArguments[0].Value is int knownPolicyValue)
                        {
                            memberNamingPolicy = (YamlKnownNamingPolicy)knownPolicyValue;
                        }
                        else
                        {
                            // The attribute uses a custom naming policy that can't be resolved at compile time.
                            // Use Unspecified to prevent the global naming policy from incorrectly applying.
                            memberNamingPolicy = YamlKnownNamingPolicy.Unspecified;
                        }
                    }
                    else if (attributeType.ContainingAssembly.Name == StymlAssemblyName)
                    {
                        switch (attributeType.ToDisplayString())
                        {
                            case YamlIgnoreAttributeFullName:
                            {
                                ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs = attributeData.NamedArguments;

                                if (namedArgs.Length == 0)
                                {
                                    ignoreCondition = YamlIgnoreCondition.Always;
                                }
                                else if (namedArgs.Length == 1 &&
                                    namedArgs[0].Value.Type?.ToDisplayString() == YamlIgnoreConditionFullName)
                                {
                                    ignoreCondition = (YamlIgnoreCondition)namedArgs[0].Value.Value!;
                                }
                                break;
                            }
                            case YamlIncludeAttributeFullName:
                            {
                                hasYamlInclude = true;
                                break;
                            }
                            case YamlNumberHandlingAttributeFullName:
                            {
                                ImmutableArray<TypedConstant> ctorArgs = attributeData.ConstructorArguments;
                                numberHandling = (YamlNumberHandling)ctorArgs[0].Value!;
                                break;
                            }
                            case YamlObjectCreationHandlingAttributeFullName:
                            {
                                ImmutableArray<TypedConstant> ctorArgs = attributeData.ConstructorArguments;
                                objectCreationHandling = (YamlObjectCreationHandling)ctorArgs[0].Value!;
                                break;
                            }
                            case YamlPropertyNameAttributeFullName:
                            {
                                ImmutableArray<TypedConstant> ctorArgs = attributeData.ConstructorArguments;
                                yamlPropertyName = (string)ctorArgs[0].Value!;
                                // Null check here is done at runtime within YamlSerializer.
                                break;
                            }
                            case YamlPropertyOrderAttributeFullName:
                            {
                                ImmutableArray<TypedConstant> ctorArgs = attributeData.ConstructorArguments;
                                order = (int)ctorArgs[0].Value!;
                                break;
                            }
                            case YamlExtensionDataAttributeFullName:
                            {
                                isExtensionData = true;
                                break;
                            }
                            case YamlRequiredAttributeFullName:
                            {
                                hasYamlRequiredAttribute = true;
                                break;
                            }
                            case YamlCommentAttributeFullName:
                            {
                                // M5f: checked by the library when the contract is configured.
                                comment = (string?)attributeData.ConstructorArguments[0].Value;
                                break;
                            }
                            case YamlScalarStyleAttributeFullName:
                            {
                                // M5f.
                                scalarStyle = (YamlScalarStyle)(byte)attributeData.ConstructorArguments[0].Value!;
                                break;
                            }
                        }
                    }
                }
            }

            private void ProcessMember(
                INamedTypeSymbol contextType,
                ISymbol memberInfo,
                bool hasYamlInclude,
                out bool isReadOnly,
                out bool isAccessible,
                out bool isRequired,
                out bool canUseGetter,
                out bool canUseSetter,
                out bool isSetterInitOnly,
                out bool isGetterNonNullable,
                out bool isSetterNonNullable)
            {
                isAccessible = false;
                isReadOnly = false;
                isRequired = false;
                canUseGetter = false;
                canUseSetter = false;
                isSetterInitOnly = false;
                isGetterNonNullable = false;
                isSetterNonNullable = false;

                switch (memberInfo)
                {
                    case IPropertySymbol propertyInfo:
#if ROSLYN4_4_OR_GREATER
                        isRequired = propertyInfo.IsRequired;
#endif
                        if (propertyInfo.GetMethod is { } getMethod)
                        {
                            if (getMethod.DeclaredAccessibility is Accessibility.Public)
                            {
                                isAccessible = true;
                                canUseGetter = true;
                            }
                            else if (IsSymbolAccessibleWithin(getMethod, within: contextType))
                            {
                                isAccessible = true;
                                canUseGetter = hasYamlInclude;
                            }
                        }

                        if (propertyInfo.SetMethod is { } setMethod)
                        {
                            isSetterInitOnly = setMethod.IsInitOnly;

                            if (setMethod.DeclaredAccessibility is Accessibility.Public)
                            {
                                isAccessible = true;
                                canUseSetter = true;
                            }
                            else if (IsSymbolAccessibleWithin(setMethod, within: contextType))
                            {
                                isAccessible = true;
                                canUseSetter = hasYamlInclude;
                            }
                        }
                        else
                        {
                            isReadOnly = true;
                        }

                        propertyInfo.ResolveNullabilityAnnotations(out isGetterNonNullable, out isSetterNonNullable);
                        break;
                    case IFieldSymbol fieldInfo:
                        isReadOnly = fieldInfo.IsReadOnly;
#if ROSLYN4_4_OR_GREATER
                        isRequired = fieldInfo.IsRequired;
#endif
                        if (fieldInfo.DeclaredAccessibility is Accessibility.Public)
                        {
                            isAccessible = true;
                            canUseGetter = true;
                            canUseSetter = !isReadOnly;
                        }
                        else if (IsSymbolAccessibleWithin(fieldInfo, within: contextType))
                        {
                            isAccessible = true;
                            canUseGetter = hasYamlInclude;
                            canUseSetter = hasYamlInclude && !isReadOnly;
                        }

                        fieldInfo.ResolveNullabilityAnnotations(out isGetterNonNullable, out isSetterNonNullable);
                        break;
                    default:
                        Debug.Fail("Method given an invalid symbol type.");
                        break;
                }
            }

            private ParameterGenerationSpec[]? ParseConstructorParameters(
                in TypeToGenerate typeToGenerate,
                IMethodSymbol? constructor,
                out ObjectConstructionStrategy constructionStrategy,
                out bool constructorSetsRequiredMembers,
                ref HashSet<string>? experimentalIds)
            {
                ITypeSymbol type = typeToGenerate.Type;

                if ((constructor is null && !type.IsValueType) || type.IsAbstract)
                {
                    constructionStrategy = ObjectConstructionStrategy.NotApplicable;
                    constructorSetsRequiredMembers = false;
                    return null;
                }

                // The generated code invokes this constructor, so suppress any [Experimental] diagnostic declared
                // on the constructor itself (parameter types are covered by EnqueueType below).
                AddExperimentalDiagnosticIds(constructor, ref experimentalIds);

                ParameterGenerationSpec[] constructorParameters;
                int paramCount = constructor?.Parameters.Length ?? 0;
                constructorSetsRequiredMembers = constructor?.ContainsAttribute(_knownSymbols.SetsRequiredMembersAttributeType) == true;

                if (paramCount == 0)
                {
                    constructionStrategy = ObjectConstructionStrategy.ParameterlessConstructor;
                    constructorParameters = [];
                }
                else
                {
                    Debug.Assert(constructor != null);

                    constructionStrategy = ObjectConstructionStrategy.ParameterizedConstructor;
                    constructorParameters = new ParameterGenerationSpec[paramCount];

                    // Compute ArgsIndex for each parameter.
                    // out parameters don't have entries in the args array.
                    int argsIndex = 0;
                    for (int i = 0; i < paramCount; i++)
                    {
                        IParameterSymbol parameterInfo = constructor.Parameters[i];

                        if (parameterInfo.Type.IsRefLikeType)
                        {
                            ReportDiagnostic(DiagnosticDescriptors.TypeContainsRefLikeMember, parameterInfo.GetLocation(), type.Name, parameterInfo.Name);
                            constructionStrategy = ObjectConstructionStrategy.NotApplicable;
                            continue;
                        }

                        // Don't enqueue out parameter types for JSON contract generation — they
                        // aren't deserialized and may reference unsupported types (e.g. Task).
                        TypeRef parameterTypeRef;
                        if (parameterInfo.RefKind == RefKind.Out)
                        {
                            parameterTypeRef = new TypeRef(parameterInfo.Type);
                        }
                        else
                        {
                            parameterTypeRef = EnqueueType(parameterInfo.Type, typeToGenerate.Mode);

                            // The generated constructor invocation references the parameter type by name.
                            AddExperimentalDiagnosticIds(parameterInfo.Type, ref experimentalIds);
                        }

                        // out parameters don't receive values from JSON, so they have ArgsIndex = -1.
                        int currentArgsIndex = parameterInfo.RefKind == RefKind.Out ? -1 : argsIndex++;

                        constructorParameters[i] = new ParameterGenerationSpec
                        {
                            ParameterType = parameterTypeRef,
                            OpenParameterTypeFQN = !SymbolEqualityComparer.Default.Equals(parameterInfo.Type, parameterInfo.OriginalDefinition.Type)
                                ? parameterInfo.OriginalDefinition.Type.GetFullyQualifiedName() : null,
                            Name = parameterInfo.Name,
                            HasDefaultValue = parameterInfo.HasExplicitDefaultValue,
                            DefaultValue = parameterInfo.HasExplicitDefaultValue ? parameterInfo.ExplicitDefaultValue : null,
                            ParameterIndex = i,
                            ArgsIndex = currentArgsIndex,
                            IsNullable = parameterInfo.IsNullable(),
                            RefKind = parameterInfo.RefKind,
                        };
                    }
                }

                return constructionStrategy is ObjectConstructionStrategy.NotApplicable ? null : constructorParameters;
            }

            private List<PropertyInitializerGenerationSpec>? ParsePropertyInitializers(
                ParameterGenerationSpec[]? constructorParameters,
                List<PropertyGenerationSpec>? properties,
                bool constructorSetsRequiredMembers,
                ref ObjectConstructionStrategy constructionStrategy)
            {
                if (constructionStrategy is ObjectConstructionStrategy.NotApplicable || properties is null)
                {
                    return null;
                }

                HashSet<string>? requiredMemberNames = null;
                List<PropertyInitializerGenerationSpec>? propertyInitializers = null;

                // Count non-out constructor parameters - out params don't have entries in the args array.
                int paramCount = constructorParameters?.Count(p => p.RefKind != RefKind.Out) ?? 0;

                // Determine required properties that need to be part of the constructor delegate signature.
                // Init-only non-required properties are no longer included here -- they will be set
                // via UnsafeAccessor or reflection post-construction to preserve their default values.
                foreach (PropertyGenerationSpec property in properties)
                {
                    if (!property.CanUseSetter)
                    {
                        continue;
                    }

                    if (property.DefaultIgnoreCondition == YamlIgnoreCondition.Always && !property.IsRequired)
                    {
                        continue;
                    }

                    if (property.IsRequired && !constructorSetsRequiredMembers)
                    {
                        if (!(requiredMemberNames ??= new()).Add(property.MemberName))
                        {
                            // We've already added another required member with the same name to our spec list.
                            // Duplicates can occur here because the provided list of properties includes shadowed members.
                            // This is because we generate metadata for *all* members, including shadowed or ignored ones,
                            // since we need to re-run the deduplication algorithm taking run-time configuration into account.
                            // This is a simple deduplication that keeps the first result for each member name --
                            // this should be fine since the properties are listed from most derived to least derived order,
                            // so the second instance of a member name is always shadowed by the first.
                            continue;
                        }

                        ParameterGenerationSpec? matchingConstructorParameter = GetMatchingConstructorParameter(property, constructorParameters);

                        if (property.IsRequired || matchingConstructorParameter is null)
                        {
                            constructionStrategy = ObjectConstructionStrategy.ParameterizedConstructor;

                            var propertyInitializer = new PropertyInitializerGenerationSpec
                            {
                                Name = property.NameSpecifiedInSourceCode,
                                ParameterType = property.PropertyType,
                                MatchesConstructorParameter = matchingConstructorParameter is not null,
                                // Use ArgsIndex for matching ctor params (excludes out params), or paramCount++ for new ones
                                ParameterIndex = matchingConstructorParameter?.ArgsIndex ?? paramCount++,
                                IsNullable = property.PropertyType.CanBeNull && !property.IsSetterNonNullableAnnotation,
                            };

                            (propertyInitializers ??= new()).Add(propertyInitializer);
                        }

                        static ParameterGenerationSpec? GetMatchingConstructorParameter(PropertyGenerationSpec propSpec, ParameterGenerationSpec[]? paramGenSpecs)
                        {
                            return paramGenSpecs?.FirstOrDefault(MatchesConstructorParameter);

                            bool MatchesConstructorParameter(ParameterGenerationSpec paramSpec)
                                // Don't match out parameters - they don't receive values from JSON.
                                => paramSpec.RefKind != RefKind.Out &&
                                   propSpec.MemberName.Equals(paramSpec.Name, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                return propertyInitializers;
            }

            private TypeRef? GetConverterTypeFromYamlConverterAttribute(INamedTypeSymbol contextType, ISymbol declaringSymbol, AttributeData attributeData, ref HashSet<string>? experimentalIds, ITypeSymbol? typeToConvert = null)
            {
                Debug.Assert(_knownSymbols.YamlConverterAttributeType.IsAssignableFrom(attributeData.AttributeClass));

                if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _knownSymbols.YamlConverterAttributeType))
                {
                    ReportDiagnostic(DiagnosticDescriptors.DerivedYamlConverterAttributesNotSupported, attributeData.GetLocation(), attributeData.AttributeClass!.ToDisplayString());
                    return null;
                }

                Debug.Assert(attributeData.ConstructorArguments.Length == 1 && attributeData.ConstructorArguments[0].Value is null or ITypeSymbol);
                var converterType = (ITypeSymbol?)attributeData.ConstructorArguments[0].Value;

                // If typeToConvert is not provided, try to infer it from declaringSymbol
                typeToConvert ??= declaringSymbol as ITypeSymbol;

                return GetConverterTypeFromAttribute(contextType, converterType, declaringSymbol, attributeData, ref experimentalIds, typeToConvert);
            }

            private TypeRef? GetConverterTypeFromAttribute(INamedTypeSymbol contextType, ITypeSymbol? converterType, ISymbol declaringSymbol, AttributeData attributeData, ref HashSet<string>? experimentalIds, ITypeSymbol? typeToConvert = null)
            {
                INamedTypeSymbol? namedConverterType = converterType as INamedTypeSymbol;

                // Check if this is an unbound generic converter type that needs to be constructed.
                // For open generics, we construct the closed generic type first and then validate.
                if (namedConverterType is { IsUnboundGenericType: true } unboundConverterType &&
                    typeToConvert is INamedTypeSymbol { IsGenericType: true } genericTypeToConvert)
                {
                    // For nested generic types like Container<>.NestedConverter<>, we need to count
                    // all type parameters from the entire type hierarchy, not just the immediate type.
                    int totalTypeParameterCount = GetTotalTypeParameterCount(unboundConverterType);

                    if (totalTypeParameterCount == genericTypeToConvert.TypeArguments.Length)
                    {
                        namedConverterType = ConstructNestedGenericType(unboundConverterType, genericTypeToConvert.TypeArguments);
                    }
                }

                IMethodSymbol? accessibleParameterlessCtor = namedConverterType?.Constructors.FirstOrDefault(c => c.Parameters.Length == 0 && IsSymbolAccessibleWithin(c, within: contextType));

                if (namedConverterType is null ||
                    !_knownSymbols.YamlConverterType.IsAssignableFrom(namedConverterType) ||
                    accessibleParameterlessCtor is null)
                {
                    ReportDiagnostic(DiagnosticDescriptors.YamlConverterAttributeInvalidType, attributeData.GetLocation(), converterType?.ToDisplayString() ?? "null", declaringSymbol.ToDisplayString());
                    return null;
                }

                if (_knownSymbols.YamlStringEnumConverterType.IsAssignableFrom(namedConverterType))
                {
                    ReportDiagnostic(DiagnosticDescriptors.YamlStringEnumConverterNotSupportedInAot, attributeData.GetLocation(), declaringSymbol.ToDisplayString());
                }

                // The generated code instantiates this converter (including any generic type arguments) via
                // its parameterless constructor, so suppress any [Experimental] diagnostic on either symbol.
                AddExperimentalDiagnosticIds(namedConverterType, ref experimentalIds);
                AddExperimentalDiagnosticIds(accessibleParameterlessCtor, ref experimentalIds);
                return new TypeRef(namedConverterType);
            }

            private TypeRef? GetTypeClassifierFactoryTypeFromAttribute(INamedTypeSymbol contextType, ITypeSymbol? classifierType, ISymbol declaringSymbol, AttributeData attributeData, ref HashSet<string>? experimentalIds)
            {
                INamedTypeSymbol? namedClassifierType = classifierType as INamedTypeSymbol;

                IMethodSymbol? accessibleParameterlessCtor = namedClassifierType?.Constructors.FirstOrDefault(c => c.Parameters.Length == 0 && IsSymbolAccessibleWithin(c, within: contextType));

                if (namedClassifierType is null ||
                    namedClassifierType.IsAbstract ||
                    !_knownSymbols.YamlTypeClassifierFactoryType.IsAssignableFrom(namedClassifierType) ||
                    accessibleParameterlessCtor is null)
                {
                    // Reuse the converter-attribute diagnostic for this prototype; the conditions are analogous.
                    ReportDiagnostic(DiagnosticDescriptors.YamlConverterAttributeInvalidType, attributeData.GetLocation(), classifierType?.ToDisplayString() ?? "null", declaringSymbol.ToDisplayString());
                    return null;
                }

                // The generated code instantiates this classifier factory (including any generic type arguments) via
                // its parameterless constructor, so suppress any [Experimental] diagnostic on either symbol.
                AddExperimentalDiagnosticIds(namedClassifierType, ref experimentalIds);
                AddExperimentalDiagnosticIds(accessibleParameterlessCtor, ref experimentalIds);
                return new TypeRef(namedClassifierType);
            }

            /// <summary>
            /// Gets the total number of type parameters from an unbound generic type,
            /// including type parameters from containing types for nested generics.
            /// For example, Container&lt;&gt;.NestedConverter&lt;&gt; has a total of 2 type parameters.
            /// </summary>
            private static int GetTotalTypeParameterCount(INamedTypeSymbol unboundType)
            {
                int count = 0;
                INamedTypeSymbol? current = unboundType;
                while (current != null)
                {
                    count += current.TypeParameters.Length;
                    current = current.ContainingType;
                }
                return count;
            }

            /// <summary>
            /// Constructs a closed generic type from an unbound generic type (potentially nested),
            /// using the provided type arguments in the order they should be applied.
            /// Returns null if the type cannot be constructed.
            /// </summary>
            private static INamedTypeSymbol? ConstructNestedGenericType(INamedTypeSymbol unboundType, ImmutableArray<ITypeSymbol> typeArguments)
            {
                // Build the chain of containing types from outermost to innermost
                var typeChain = new List<INamedTypeSymbol>();
                INamedTypeSymbol? current = unboundType;
                while (current != null)
                {
                    typeChain.Add(current);
                    current = current.ContainingType;
                }

                // Reverse to go from outermost to innermost
                typeChain.Reverse();

                // Track which type arguments have been used
                int typeArgIndex = 0;
                INamedTypeSymbol? constructedContainingType = null;

                foreach (var type in typeChain)
                {
                    int typeParamCount = type.TypeParameters.Length;
                    INamedTypeSymbol originalDef = type.OriginalDefinition;

                    if (typeParamCount > 0)
                    {
                        // Get the type arguments for this level
                        var args = typeArguments.Skip(typeArgIndex).Take(typeParamCount).ToArray();
                        typeArgIndex += typeParamCount;

                        // Construct this level
                        if (constructedContainingType == null)
                        {
                            constructedContainingType = originalDef.Construct(args);
                        }
                        else
                        {
                            // Get the nested type from the constructed containing type
                            var nestedTypeDef = constructedContainingType.GetTypeMembers(originalDef.Name, originalDef.Arity).FirstOrDefault();
                            if (nestedTypeDef != null)
                            {
                                constructedContainingType = nestedTypeDef.Construct(args);
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                    else
                    {
                        // Non-generic type in the chain
                        if (constructedContainingType == null)
                        {
                            constructedContainingType = originalDef;
                        }
                        else
                        {
                            // Use arity 0 to avoid ambiguity with nested types of the same name but different arity
                            var nestedType = constructedContainingType.GetTypeMembers(originalDef.Name, 0).FirstOrDefault();
                            if (nestedType == null)
                            {
                                return null;
                            }
                            constructedContainingType = nestedType;
                        }
                    }
                }

                return constructedContainingType;
            }

            private static string DetermineEffectiveYamlPropertyName(
                string propertyName,
                string? yamlPropertyName,
                YamlKnownNamingPolicy? memberNamingPolicy,
                YamlKnownNamingPolicy? typeNamingPolicy,
                SourceGenerationOptionsSpec? options)
            {
                if (yamlPropertyName != null)
                {
                    return yamlPropertyName;
                }

                YamlKnownNamingPolicy? effectiveKnownPolicy = memberNamingPolicy
                    ?? typeNamingPolicy
                    ?? options?.GetEffectivePropertyNamingPolicy();

                YamlNamingPolicy? instance = effectiveKnownPolicy switch
                {
                    YamlKnownNamingPolicy.CamelCase => YamlNamingPolicy.CamelCase,
                    YamlKnownNamingPolicy.SnakeCaseLower => YamlNamingPolicy.SnakeCaseLower,
                    YamlKnownNamingPolicy.SnakeCaseUpper => YamlNamingPolicy.SnakeCaseUpper,
                    YamlKnownNamingPolicy.KebabCaseLower => YamlNamingPolicy.KebabCaseLower,
                    YamlKnownNamingPolicy.KebabCaseUpper => YamlNamingPolicy.KebabCaseUpper,
                    YamlKnownNamingPolicy.PascalCase => YamlNamingPolicy.PascalCase,
                    _ => null,
                };

                return instance?.ConvertName(propertyName) ?? propertyName;
            }

            private static string? DetermineImmutableCollectionFactoryMethod(string? immutableCollectionFactoryTypeFullName)
            {
                return immutableCollectionFactoryTypeFullName is not null ? $"global::{immutableCollectionFactoryTypeFullName}.CreateRange" : null;
            }

            private static string DeterminePropertyNameFieldName(string effectiveYamlPropertyName)
            {
                const string PropName = "PropName_";

                // Use a different prefix to avoid possible collisions with "PropName_" in
                // the rare case there is a C# property in a hex format.
                const string EncodedPropName = "EncodedPropName_";

                if (SyntaxFacts.IsValidIdentifier(effectiveYamlPropertyName))
                {
                    return PropName + effectiveYamlPropertyName;
                }

                // Encode the string to a byte[] and then convert to hexadecimal.
                // To make the generated code more readable, we could use a different strategy in the future
                // such as including the full class name + the CLR property name when there are duplicates,
                // but that will create unnecessary YamlEncodedText properties.
                byte[] utf8Yaml = Encoding.UTF8.GetBytes(effectiveYamlPropertyName);

                StringBuilder sb = new StringBuilder(
                    EncodedPropName,
                    capacity: EncodedPropName.Length + utf8Yaml.Length * 2);

                for (int i = 0; i < utf8Yaml.Length; i++)
                {
                    sb.Append(utf8Yaml[i].ToString("X2")); // X2 is hex format
                }

                return sb.ToString();
            }

            private YamlPrimitiveTypeKind? GetPrimitiveTypeKind(ITypeSymbol type)
            {
                if (type.IsNumberType())
                {
                    return YamlPrimitiveTypeKind.Number;
                }

                if (type.SpecialType is SpecialType.System_Boolean)
                {
                    return YamlPrimitiveTypeKind.Boolean;
                }

                if (type.SpecialType is SpecialType.System_Char)
                {
                    return YamlPrimitiveTypeKind.Char;
                }

                SymbolEqualityComparer cmp = SymbolEqualityComparer.Default;

                if (type.SpecialType is SpecialType.System_String or SpecialType.System_DateTime ||
                     cmp.Equals(type, _knownSymbols.DateTimeOffsetType) || cmp.Equals(type, _knownSymbols.GuidType))
                {
                    return YamlPrimitiveTypeKind.String;
                }

                if (cmp.Equals(type, _knownSymbols.ByteArrayType))
                {
                    return YamlPrimitiveTypeKind.ByteArray;
                }

                return null;
            }

            private static string GetTypeInfoPropertyName(ITypeSymbol type)
            {
                if (type is IArrayTypeSymbol arrayType)
                {
                    int rank = arrayType.Rank;
                    string suffix = rank == 1 ? "Array" : $"Array{rank}D"; // Array, Array2D, Array3D, ...
                    return GetTypeInfoPropertyName(arrayType.ElementType) + suffix;
                }

                if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
                {
                    return type.Name;
                }

                StringBuilder sb = new();

                string name = namedType.Name;

                sb.Append(name);

                if (namedType.GetAllTypeArgumentsInScope() is List<ITypeSymbol> typeArgsInScope)
                {
                    foreach (ITypeSymbol genericArg in typeArgsInScope)
                    {
                        sb.Append(GetTypeInfoPropertyName(genericArg));
                    }
                }

                return sb.ToString();
            }

            private bool TryGetDeserializationConstructor(
                ITypeSymbol type,
                bool useDefaultCtorInAnnotatedStructs,
                out IMethodSymbol? deserializationCtor)
            {
                IMethodSymbol? ctorWithAttribute = null;
                IMethodSymbol? publicParameterlessCtor = null;
                IMethodSymbol? lonePublicCtor = null;

                if (type is not INamedTypeSymbol namedType)
                {
                    deserializationCtor = null;
                    return false;
                }

                IMethodSymbol[] publicCtors = namedType.GetExplicitlyDeclaredInstanceConstructors().Where(ctor => ctor.DeclaredAccessibility is Accessibility.Public).ToArray();

                if (publicCtors.Length == 1)
                {
                    lonePublicCtor = publicCtors[0];
                }

                foreach (IMethodSymbol constructor in publicCtors)
                {
                    if (constructor.ContainsAttribute(_knownSymbols.YamlConstructorAttributeType))
                    {
                        if (ctorWithAttribute != null)
                        {
                            deserializationCtor = null;
                            return false;
                        }

                        ctorWithAttribute = constructor;
                    }
                    else if (constructor.Parameters.Length == 0)
                    {
                        publicParameterlessCtor = constructor;
                    }
                }

                // Search for non-public ctors with [YamlConstructor].
                foreach (IMethodSymbol constructor in namedType.GetExplicitlyDeclaredInstanceConstructors().Where(ctor => ctor.DeclaredAccessibility is not Accessibility.Public))
                {
                    if (constructor.ContainsAttribute(_knownSymbols.YamlConstructorAttributeType))
                    {
                        if (ctorWithAttribute != null)
                        {
                            deserializationCtor = null;
                            return false;
                        }

                        ctorWithAttribute = constructor;
                    }
                }

                // Structs will use default constructor if attribute isn't used.
                if (useDefaultCtorInAnnotatedStructs && type.IsValueType && ctorWithAttribute == null)
                {
                    deserializationCtor = null;
                    return true;
                }

                deserializationCtor = ctorWithAttribute ?? publicParameterlessCtor ?? lonePublicCtor;
                return true;
            }

            private bool IsSymbolAccessibleWithin(ISymbol symbol, INamedTypeSymbol within)
                => _knownSymbols.Compilation.IsSymbolAccessibleWithin(symbol, within);

            private bool IsUnsupportedType(ITypeSymbol type)
            {
                return
                    SymbolEqualityComparer.Default.Equals(_knownSymbols.SerializationInfoType, type) ||
                    SymbolEqualityComparer.Default.Equals(_knownSymbols.IntPtrType, type) ||
                    SymbolEqualityComparer.Default.Equals(_knownSymbols.UIntPtrType, type) ||
                    _knownSymbols.MemberInfoType.IsAssignableFrom(type) ||
                    _knownSymbols.DelegateType.IsAssignableFrom(type) ||
                    type is IArrayTypeSymbol { Rank: > 1 };
            }

            private bool IsBuiltInSupportType(ITypeSymbol type)
            {
                return type.SpecialType is
                        SpecialType.System_Boolean or
                        SpecialType.System_Char or
                        SpecialType.System_DateTime or
                        SpecialType.System_String or
                        SpecialType.System_Object ||

                    type.IsNumberType() ||
                    _builtInSupportTypes.Contains(type);
            }

            private static HashSet<ITypeSymbol> CreateBuiltInSupportTypeSet(KnownTypeSymbols knownSymbols)
            {
#pragma warning disable RS1024 // Compare symbols correctly https://github.com/dotnet/roslyn-analyzers/issues/5804
                HashSet<ITypeSymbol> builtInSupportTypes = new(SymbolEqualityComparer.Default);
#pragma warning restore

                AddTypeIfNotNull(knownSymbols.ByteArrayType);
                AddTypeIfNotNull(knownSymbols.MemoryByteType);
                AddTypeIfNotNull(knownSymbols.ReadOnlyMemoryByteType);
                AddTypeIfNotNull(knownSymbols.TimeSpanType);
                AddTypeIfNotNull(knownSymbols.DateTimeOffsetType);
                AddTypeIfNotNull(knownSymbols.DateOnlyType);
                AddTypeIfNotNull(knownSymbols.TimeOnlyType);
                AddTypeIfNotNull(knownSymbols.Int128Type);
                AddTypeIfNotNull(knownSymbols.UInt128Type);
                AddTypeIfNotNull(knownSymbols.HalfType);
                AddTypeIfNotNull(knownSymbols.BFloat16Type);
                AddTypeIfNotNull(knownSymbols.Decimal32Type);
                AddTypeIfNotNull(knownSymbols.Decimal64Type);
                AddTypeIfNotNull(knownSymbols.Decimal128Type);
                AddTypeIfNotNull(knownSymbols.GuidType);
                AddTypeIfNotNull(knownSymbols.UriType);
                AddTypeIfNotNull(knownSymbols.VersionType);

                AddTypeIfNotNull(knownSymbols.YamlArrayType);
                AddTypeIfNotNull(knownSymbols.YamlElementType);
                AddTypeIfNotNull(knownSymbols.YamlNodeType);
                AddTypeIfNotNull(knownSymbols.YamlObjectType);
                AddTypeIfNotNull(knownSymbols.YamlValueType);
                AddTypeIfNotNull(knownSymbols.YamlDocumentType);

                return builtInSupportTypes;

                void AddTypeIfNotNull(ITypeSymbol? type)
                {
                    if (type != null)
                    {
                        builtInSupportTypes.Add(type);
                    }
                }
            }

            private GenericTypeMetadata GetGenericTypeDefinition(ITypeSymbol type)
            {
                if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
                {
                    return default;
                }

                namedType = namedType.OriginalDefinition;
                if (!_genericTypeDefinitions.TryGetValue(namedType, out GenericTypeMetadata result))
                {
                    result = (
                        namedType.GetFullyQualifiedName(),
                        namedType.TypeParameters.Select(tp => tp.MemberNameNeedsAtSign() ? "@" + tp.Name : tp.Name).ToImmutableEquatableArray(),
                        GetTypeParameterConstraintClauses(namedType));
                    _genericTypeDefinitions.Add(namedType, result);
                }

                return result;
            }

            /// <summary>
            /// Extracts the type parameter constraint clauses from a generic type using
            /// Roslyn's <see cref="SymbolDisplayGenericsOptions.IncludeTypeConstraints"/>.
            /// Returns the combined <c>where</c> clauses (e.g., "where T : notnull, global::NS.MyBase"),
            /// or null if the type has no constraints.
            /// </summary>
            private static string? GetTypeParameterConstraintClauses(INamedTypeSymbol type)
            {
                Debug.Assert(type.IsGenericType);
                string display = type.ToDisplayString(s_fullyQualifiedWithConstraints);

                // The display string has the form "global::NS.Type<T, U> where T : C1 where U : C2".
                // Extract the constraint clauses after the type name by finding the first " where ".
                const string whereMarker = " where ";
                int whereIndex = display.IndexOf(whereMarker);
                if (whereIndex < 0)
                {
                    return null;
                }

                return display.Substring(whereIndex + 1);
            }

            private readonly struct TypeToGenerate
            {
                public required ITypeSymbol Type { get; init; }
                public required YamlSourceGenerationMode? Mode { get; init; }
                public required string? TypeInfoPropertyName { get; init; }
                public required Location? Location { get; init; }
                public required Location? AttributeLocation { get; init; }
            }
        }
    }
}
