// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SourceGenerators;
using GenericAccessorEntry = (System.Text.Yaml.SourceGeneration.PropertyGenerationSpec Property, int Index, bool Disambiguate, bool NeedsGetter, bool NeedsSetter);

namespace System.Text.Yaml.SourceGeneration
{
    public sealed partial class YamlSourceGenerator
    {
        private sealed partial class Emitter
        {
            private static readonly AssemblyName s_assemblyName = typeof(Emitter).Assembly.GetName();

            // global::fully.qualified.name for referenced types
            private const string InvalidOperationExceptionTypeRef = "global::System.InvalidOperationException";
            private const string YamlExceptionTypeRef = "global::System.Text.Yaml.YamlException";
            private const string TypeTypeRef = "global::System.Type";
            private const string UnsafeTypeRef = "global::System.Runtime.CompilerServices.Unsafe";
            private const string EqualityComparerTypeRef = "global::System.Collections.Generic.EqualityComparer";
            private const string KeyValuePairTypeRef = "global::System.Collections.Generic.KeyValuePair";
            private const string UnsafeAccessorAttributeTypeRef = "global::System.Runtime.CompilerServices.UnsafeAccessorAttribute";
            private const string UnsafeAccessorKindTypeRef = "global::System.Runtime.CompilerServices.UnsafeAccessorKind";
            private const string YamlEncodedTextTypeRef = "global::System.Text.Yaml.YamlEncodedText";
            private const string YamlNamingPolicyTypeRef = "global::System.Text.Yaml.YamlNamingPolicy";
            private const string YamlSerializerTypeRef = "global::System.Text.Yaml.YamlSerializer";
            private const string YamlSerializerOptionsTypeRef = "global::System.Text.Yaml.YamlSerializerOptions";
            private const string YamlSerializerContextTypeRef = "global::System.Text.Yaml.Serialization.YamlSerializerContext";
            private const string Utf8YamlWriterTypeRef = "global::System.Text.Yaml.Utf8YamlWriter";
            private const string YamlCommentHandlingTypeRef = "global::System.Text.Yaml.YamlCommentHandling";
            private const string YamlConverterTypeRef = "global::System.Text.Yaml.Serialization.YamlConverter";
            private const string YamlConverterFactoryTypeRef = "global::System.Text.Yaml.Serialization.YamlConverterFactory";
            private const string YamlCollectionInfoValuesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlCollectionInfoValues";
            private const string YamlDerivedTypeTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlDerivedType";
            private const string YamlIgnoreConditionTypeRef = "global::System.Text.Yaml.Serialization.YamlIgnoreCondition";
            private const string YamlSerializerDefaultsTypeRef = "global::System.Text.Yaml.YamlSerializerDefaults";
            private const string YamlNumberHandlingTypeRef = "global::System.Text.Yaml.Serialization.YamlNumberHandling";
            private const string YamlObjectCreationHandlingTypeRef = "global::System.Text.Yaml.Serialization.YamlObjectCreationHandling";
            private const string YamlUnmappedMemberHandlingTypeRef = "global::System.Text.Yaml.Serialization.YamlUnmappedMemberHandling";
            private const string YamlUnknownDerivedTypeHandlingTypeRef = "global::System.Text.Yaml.Serialization.YamlUnknownDerivedTypeHandling";
            private const string YamlTypeDiscriminatorStyleTypeRef = "global::System.Text.Yaml.Serialization.YamlTypeDiscriminatorStyle";
            private const string YamlUnknownTypeHandlingTypeRef = "global::System.Text.Yaml.Serialization.YamlUnknownTypeHandling";
            private const string YamlMetadataServicesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlMetadataServices";
            private const string YamlObjectInfoValuesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlObjectInfoValues";
            private const string YamlParameterInfoValuesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlParameterInfoValues";
            private const string YamlPolymorphismOptionsTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlPolymorphismOptions";
            private const string YamlPropertyInfoTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlPropertyInfo";
            private const string YamlPropertyInfoValuesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlPropertyInfoValues";
            private const string YamlTypeInfoTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlTypeInfo";
            private const string YamlTypeInfoResolverTypeRef = "global::System.Text.Yaml.Serialization.Metadata.IYamlTypeInfoResolver";
            private const string YamlUnionCaseInfoTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlUnionCaseInfo";
            private const string YamlUnionInfoValuesTypeRef = "global::System.Text.Yaml.Serialization.Metadata.YamlUnionInfoValues";
            private const string ReferenceHandlerTypeRef = "global::System.Text.Yaml.Serialization.ReferenceHandler";
            private const string EmptyTypeArray = "global::System.Array.Empty<global::System.Type>()";

            private const string ByteArrayValueWriterMethodName = "WriteByteArrayValue";

            /// <summary>
            /// Contains an index from TypeRef to TypeGenerationSpec for the current ContextGenerationSpec.
            /// </summary>
            private readonly Dictionary<TypeRef, TypeGenerationSpec> _typeIndex = new();

            /// <summary>
            /// Cache of property names (statically determined) found across the type graph of the YamlSerializerContext.
            /// The dictionary Key is the JSON property name, and the Value is the variable name which is the same as the property
            /// name except for cases where special characters are used with [YamlPropertyName].
            /// </summary>
            private readonly Dictionary<string, string> _propertyNames = new();

            /// <summary>
            /// Indicates that the type graph contains a nullable property with a design-time custom converter declaration.
            /// </summary>
            private bool _emitGetConverterForNullablePropertyMethod;

            /// <summary>
            /// Indicates that a value type property setter uses the reflection fallback,
            /// requiring the <c>ValueTypeSetter</c> delegate type to be emitted.
            /// </summary>
            private bool _emitValueTypeSetterDelegate;

            /// <summary>
            /// Indicates that the fast-path serializer writes a <see cref="byte"/> array value,
            /// requiring the on-demand byte[] writer helper to be emitted on the context.
            /// </summary>
            private bool _emitByteArrayValueHelper;

            /// <summary>
            /// The SourceText emit implementation filled by the individual Roslyn versions.
            /// </summary>
            private partial void AddSource(string hintName, SourceText sourceText);

            public void Emit(ContextGenerationSpec contextGenerationSpec)
            {
                Debug.Assert(_typeIndex.Count == 0);
                Debug.Assert(_propertyNames.Count == 0);
                Debug.Assert(!_emitGetConverterForNullablePropertyMethod);

                foreach (TypeGenerationSpec spec in contextGenerationSpec.GeneratedTypes)
                {
                    _typeIndex.Add(spec.TypeRef, spec);
                }

                // M5d: a context with [YamlTagMapping] has no serialization fast path, as the simplest rule.
                _contextHasTagMappings = contextGenerationSpec.TagMappings.Count > 0;
                _contextOmitsEmptyCollections = contextGenerationSpec.GeneratedOptionsSpec?.DefaultIgnoreCondition is YamlIgnoreCondition.WhenWritingEmptyCollection;

                foreach (TypeGenerationSpec typeGenerationSpec in contextGenerationSpec.GeneratedTypes)
                {
                    SourceText? sourceText = GenerateTypeInfo(contextGenerationSpec, typeGenerationSpec);
                    if (sourceText != null)
                    {
                        AddSource($"{contextGenerationSpec.ContextType.Name}.{typeGenerationSpec.TypeInfoPropertyName}.g.cs", sourceText);
                    }
                }

                string contextName = contextGenerationSpec.ContextType.Name;

                // Add root context implementation.
                AddSource($"{contextName}.g.cs", GetRootYamlContextImplementation(contextGenerationSpec, _emitGetConverterForNullablePropertyMethod, _emitValueTypeSetterDelegate, _emitByteArrayValueHelper));

                // Add GetYamlTypeInfo override implementation.
                AddSource($"{contextName}.GetYamlTypeInfo.g.cs", GetGetTypeInfoImplementation(contextGenerationSpec));

                // Add property name initialization.
                AddSource($"{contextName}.PropertyNames.g.cs", GetPropertyNameInitialization(contextGenerationSpec));

                _emitGetConverterForNullablePropertyMethod = false;
                _contextHasTagMappings = false;
                _contextOmitsEmptyCollections = false;
                _emitValueTypeSetterDelegate = false;
                _emitByteArrayValueHelper = false;
                _propertyNames.Clear();
                _typeIndex.Clear();
            }

            private static SourceWriter CreateSourceWriterWithContextHeader(ContextGenerationSpec contextSpec, bool isPrimaryContextSourceFile = false, string? interfaceImplementation = null, IReadOnlyCollection<string>? experimentalDiagnosticIds = null)
            {
                var writer = new SourceWriter();

                writer.WriteLine("""
                    // <auto-generated/>

                    #nullable enable annotations
                    #nullable disable warnings

                    // Suppress warnings about [Obsolete] member usage in generated code.
                    #pragma warning disable CS0612, CS0618
                    """);

                if (experimentalDiagnosticIds is { Count: > 0 })
                {
                    // Suppress the specific [Experimental] diagnostic IDs referenced by this file's generated code,
                    // mirroring the unconditional [Obsolete] suppression above but with user-defined, discovered IDs.
                    writer.WriteLine();
                    writer.WriteLine("// Suppress warnings about [Experimental] member usage in generated code.");
                    foreach (string diagnosticId in experimentalDiagnosticIds)
                    {
                        writer.WriteLine($"#pragma warning disable {diagnosticId}");
                    }
                }

                writer.WriteLine();

                if (contextSpec.Namespace != null)
                {
                    writer.WriteLine($"namespace {contextSpec.Namespace}");
                    writer.WriteLine('{');
                    writer.Indentation++;
                }

                ImmutableEquatableArray<string> contextClasses = contextSpec.ContextClassDeclarations;
                Debug.Assert(contextClasses.Count > 0);

                // Emit any containing classes first.
                for (int i = contextClasses.Count - 1; i > 0; i--)
                {
                    writer.WriteLine(contextClasses[i]);
                    writer.WriteLine('{');
                    writer.Indentation++;
                }

                if (isPrimaryContextSourceFile)
                {
                    // Annotate context class with the GeneratedCodeAttribute
                    writer.WriteLine($"""[global::System.CodeDom.Compiler.GeneratedCodeAttribute("{s_assemblyName.Name}", "{s_assemblyName.Version}")]""");
                }

                // Emit the YamlSerializerContext class declaration
                writer.WriteLine($"{contextClasses[0]}{(interfaceImplementation is null ? "" : " : " + interfaceImplementation)}");
                writer.WriteLine('{');
                writer.Indentation++;

                return writer;
            }

            /// <summary>
            /// Reconstitutes the union of every generated type's <see cref="TypeGenerationSpec.ExperimentalDiagnosticIds"/>
            /// plus the options-level IDs, used to suppress <c>[Experimental]</c> diagnostics in the aggregate source
            /// files that reference all registered types (and the options setup) by name. Computed here rather than
            /// stored on the incremental model so the derived data does not inflate model equality comparisons.
            /// </summary>
            private static List<string>? GetContextExperimentalDiagnosticIds(ContextGenerationSpec contextSpec)
            {
                HashSet<string>? ids = null;

                if (contextSpec.GeneratedOptionsSpec?.ExperimentalDiagnosticIds is { Count: > 0 } optionsIds)
                {
                    (ids ??= new(StringComparer.Ordinal)).UnionWith(optionsIds);
                }

                foreach (TypeGenerationSpec generatedType in contextSpec.GeneratedTypes)
                {
                    if (generatedType.ExperimentalDiagnosticIds is { Count: > 0 } typeIds)
                    {
                        (ids ??= new(StringComparer.Ordinal)).UnionWith(typeIds);
                    }
                }

                if (ids is null)
                {
                    return null;
                }

                // Emit in a deterministic order: HashSet<string> enumeration order is process-randomized.
                var sorted = new List<string>(ids);
                sorted.Sort(StringComparer.Ordinal);
                return sorted;
            }

            private static SourceText CompleteSourceFileAndReturnText(SourceWriter writer)
            {
                while (writer.Indentation > 0)
                {
                    writer.Indentation--;
                    writer.WriteLine('}');
                }

                return writer.ToSourceText();
            }

            private SourceText? GenerateTypeInfo(ContextGenerationSpec contextSpec, TypeGenerationSpec typeGenerationSpec)
            {
                switch (typeGenerationSpec.ClassType)
                {
                    case ClassType.BuiltInSupportType:
                        return GenerateForTypeWithBuiltInConverter(contextSpec, typeGenerationSpec);

                    case ClassType.TypeWithDesignTimeProvidedCustomConverter:
                        return GenerateForTypeWithCustomConverter(contextSpec, typeGenerationSpec);

                    case ClassType.Nullable:
                        return GenerateForNullable(contextSpec, typeGenerationSpec);

                    case ClassType.Enum:
                        return GenerateForEnum(contextSpec, typeGenerationSpec);

                    case ClassType.Enumerable:
                    case ClassType.Dictionary:
                        return GenerateForCollection(contextSpec, typeGenerationSpec);

                    case ClassType.Object:
                        return GenerateForObject(contextSpec, typeGenerationSpec);

                    case ClassType.Union:
                        return GenerateForUnion(contextSpec, typeGenerationSpec);

                    case ClassType.UnsupportedType:
                        return GenerateForUnsupportedType(contextSpec, typeGenerationSpec);

                    case ClassType.TypeUnsupportedBySourceGen:
                        return null; // Do not emit a source file for the type.

                    default:
                        Debug.Fail($"Unexpected class type {typeGenerationSpec.ClassType}");
                        return null;
                }
            }

            private static SourceText GenerateForTypeWithBuiltInConverter(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;
                string typeInfoPropertyName = typeMetadata.TypeInfoPropertyName;

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);
                writer.WriteLine($"""
                    yamlTypeInfo = {YamlMetadataServicesTypeRef}.{GetCreateValueInfoMethodRef(typeFQN)}(options, {YamlMetadataServicesTypeRef}.{typeInfoPropertyName}Converter);
                    """);

                GenerateTypeInfoFactoryFooter(writer);

                return CompleteSourceFileAndReturnText(writer);
            }

            private static SourceText GenerateForTypeWithCustomConverter(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                Debug.Assert(typeMetadata.ConverterType != null);

                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;
                string converterFQN = typeMetadata.ConverterType.FullyQualifiedName;

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);

                writer.WriteLine($"""
                    {YamlConverterTypeRef} converter = ExpandConverter(typeof({typeFQN}), new {converterFQN}(), options);
                    yamlTypeInfo = {YamlMetadataServicesTypeRef}.{GetCreateValueInfoMethodRef(typeFQN)} (options, converter);
                    """);

                GenerateTypeInfoFactoryFooter(writer);

                return CompleteSourceFileAndReturnText(writer);
            }

            private static SourceText GenerateForNullable(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                Debug.Assert(typeMetadata.NullableUnderlyingType != null);

                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;
                string underlyingTypeFQN = typeMetadata.NullableUnderlyingType.FullyQualifiedName;

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);

                writer.WriteLine($$"""
                    {{YamlConverterTypeRef}} converter = {{YamlMetadataServicesTypeRef}}.GetNullableConverter<{{underlyingTypeFQN}}>(options);
                    yamlTypeInfo = {{YamlMetadataServicesTypeRef}}.{{GetCreateValueInfoMethodRef(typeFQN)}}(options, converter);
                    """);

                GenerateTypeInfoFactoryFooter(writer);

                return CompleteSourceFileAndReturnText(writer);
            }

            private static SourceText GenerateForUnsupportedType(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);
                writer.WriteLine($"""
                    yamlTypeInfo = {YamlMetadataServicesTypeRef}.{GetCreateValueInfoMethodRef(typeFQN)}(options, {YamlMetadataServicesTypeRef}.GetUnsupportedTypeConverter<{typeFQN}>());
                    """);

                GenerateTypeInfoFactoryFooter(writer);

                return CompleteSourceFileAndReturnText(writer);
            }

            private static SourceText GenerateForEnum(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);
                writer.WriteLine($"""
                    yamlTypeInfo = {YamlMetadataServicesTypeRef}.{GetCreateValueInfoMethodRef(typeFQN)}(options, {YamlMetadataServicesTypeRef}.GetEnumConverter<{typeFQN}>(options));
                    """);

                GenerateTypeInfoFactoryFooter(writer);

                return CompleteSourceFileAndReturnText(writer);
            }

            private SourceText GenerateForCollection(ContextGenerationSpec contextSpec, TypeGenerationSpec typeGenerationSpec)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeGenerationSpec.ExperimentalDiagnosticIds);

                // Key metadata
                TypeRef? collectionKeyType = typeGenerationSpec.CollectionKeyType;
                Debug.Assert(!(typeGenerationSpec.ClassType == ClassType.Dictionary && collectionKeyType == null));
                string? keyTypeFQN = collectionKeyType?.FullyQualifiedName;

                // Value metadata
                TypeRef? collectionValueType = typeGenerationSpec.CollectionValueType;
                Debug.Assert(collectionValueType != null);
                string valueTypeFQN = collectionValueType.FullyQualifiedName;

                CollectionType collectionType = typeGenerationSpec.CollectionType;

                string? serializeMethodName = ShouldGenerateSerializationLogic(typeGenerationSpec)
                    ? $"{typeGenerationSpec.TypeInfoPropertyName}SerializeHandler"
                    : null;

                string typeFQN = typeGenerationSpec.TypeRef.FullyQualifiedName;
                string createCollectionInfoMethodName = GetCollectionInfoMethodName(collectionType);
                string createCollectionMethodExpr;
                PolymorphismOptionsSpec? polymorphismOptions = typeGenerationSpec.PolymorphismOptions;
                string polymorphismOptionsExpr = FormatPolymorphismOptions(polymorphismOptions);
                string typeClassifierFactoryExpr = polymorphismOptions is not null
                    ? FormatTypeClassifierFactory(polymorphismOptions.TypeClassifierFactoryType)
                    : "null";

                switch (collectionType)
                {
                    case CollectionType.Array:
                    case CollectionType.MemoryOfT:
                    case CollectionType.ReadOnlyMemoryOfT:
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{valueTypeFQN}>(options, info)";
                        break;
                    case CollectionType.IEnumerable:
                    case CollectionType.IDictionary:
                    case CollectionType.IList:
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}>(options, info)";
                        break;
                    case CollectionType.Stack:
                    case CollectionType.Queue:
                        string addMethod = collectionType == CollectionType.Stack ? "Push" : "Enqueue";
                        string addFuncNamedArg = $"(collection, value) => collection.{addMethod}(value)";
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}>(options, info, addFunc: {addFuncNamedArg})";
                        break;
                    case CollectionType.ImmutableEnumerable:
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}, {valueTypeFQN}>(options, info, createRangeFunc: {typeGenerationSpec.ImmutableCollectionFactoryMethod})";
                        break;
                    case CollectionType.Dictionary:
                    case CollectionType.IDictionaryOfTKeyTValue:
                    case CollectionType.IReadOnlyDictionary:
                        Debug.Assert(keyTypeFQN != null);
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}, {keyTypeFQN!}, {valueTypeFQN}>(options, info)";
                        break;
                    case CollectionType.ImmutableDictionary:
                        Debug.Assert(keyTypeFQN != null);
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}, {keyTypeFQN!}, {valueTypeFQN}>(options, info, createRangeFunc: {typeGenerationSpec.ImmutableCollectionFactoryMethod})";
                        break;
                    default:
                        createCollectionMethodExpr = $"{createCollectionInfoMethodName}<{typeFQN}, {valueTypeFQN}>(options, info)";
                        break;
                }

                GenerateTypeInfoFactoryHeader(writer, typeGenerationSpec);

                writer.WriteLine($$"""
                    var info = new {{YamlCollectionInfoValuesTypeRef}}<{{typeFQN}}>
                    {
                        ObjectCreator = {{FormatDefaultConstructorExpr(typeGenerationSpec)}},
                        SerializeHandler = {{serializeMethodName ?? "null"}},
                        PolymorphismOptions = {{polymorphismOptionsExpr}},
                        TypeClassifierFactory = {{typeClassifierFactoryExpr}},
                    };

                    yamlTypeInfo = {{YamlMetadataServicesTypeRef}}.{{createCollectionMethodExpr}};
                    yamlTypeInfo.NumberHandling = {{FormatNumberHandling(typeGenerationSpec.NumberHandling)}};
                    """);

                GenerateClosedTypeInferenceGuard(writer, typeGenerationSpec);

                GenerateTypeInfoFactoryFooter(writer);

                if (serializeMethodName != null)
                {
                    writer.WriteLine();

                    if (typeGenerationSpec.ClassType == ClassType.Enumerable)
                    {
                        GenerateFastPathFuncForEnumerable(writer, serializeMethodName, typeGenerationSpec);
                    }
                    else
                    {
                        GenerateFastPathFuncForDictionary(writer, serializeMethodName, typeGenerationSpec);
                    }
                }

                return CompleteSourceFileAndReturnText(writer);
            }

            private void GenerateFastPathFuncForEnumerable(SourceWriter writer, string serializeMethodName, TypeGenerationSpec typeGenerationSpec)
            {
                Debug.Assert(typeGenerationSpec.CollectionValueType != null);
                TypeGenerationSpec valueTypeGenerationSpec = _typeIndex[typeGenerationSpec.CollectionValueType];

                GenerateFastPathFuncHeader(writer, typeGenerationSpec, serializeMethodName);

                writer.WriteLine("writer.WriteStartArray();");
                writer.WriteLine();

                string getCurrentElementExpr;
                switch (typeGenerationSpec.CollectionType)
                {
                    case CollectionType.Array:
                        writer.WriteLine("for (int i = 0; i < value.Length; i++)");
                        getCurrentElementExpr = "value[i]";
                        break;

                    case CollectionType.MemoryOfT:
                    case CollectionType.ReadOnlyMemoryOfT:
                        writer.WriteLine($"foreach ({valueTypeGenerationSpec.TypeRef.FullyQualifiedName} element in value.Span)");
                        getCurrentElementExpr = "element";
                        break;

                    case CollectionType.IListOfT:
                    case CollectionType.List:
                    case CollectionType.IList:
                        writer.WriteLine("for (int i = 0; i < value.Count; i++)");
                        getCurrentElementExpr = "value[i]";
                        break;

                    default:
                        writer.WriteLine($"foreach ({valueTypeGenerationSpec.TypeRef.FullyQualifiedName} element in value)");
                        getCurrentElementExpr = "element";
                        break;
                };

                writer.WriteLine('{');
                writer.Indentation++;

                GenerateSerializeValueStatement(writer, valueTypeGenerationSpec, getCurrentElementExpr);

                writer.Indentation--;
                writer.WriteLine('}');

                writer.WriteLine();
                writer.WriteLine("writer.WriteEndArray();");

                writer.Indentation--;
                writer.WriteLine('}');
            }

            private void GenerateFastPathFuncForDictionary(SourceWriter writer, string serializeMethodName, TypeGenerationSpec typeGenerationSpec)
            {
                Debug.Assert(typeGenerationSpec.CollectionKeyType != null);
                Debug.Assert(typeGenerationSpec.CollectionValueType != null);

                TypeRef keyType = typeGenerationSpec.CollectionKeyType;
                TypeGenerationSpec valueTypeGenerationSpec = _typeIndex[typeGenerationSpec.CollectionValueType];

                GenerateFastPathFuncHeader(writer, typeGenerationSpec, serializeMethodName);

                writer.WriteLine("writer.WriteStartObject();");
                writer.WriteLine();

                writer.WriteLine($"foreach ({KeyValuePairTypeRef}<{keyType.FullyQualifiedName}, {valueTypeGenerationSpec.TypeRef.FullyQualifiedName}> entry in value)");
                writer.WriteLine('{');
                writer.Indentation++;

                GenerateSerializePropertyStatement(writer, valueTypeGenerationSpec, propertyNameExpr: "entry.Key", valueExpr: "entry.Value");

                writer.Indentation--;
                writer.WriteLine('}');

                writer.WriteLine();
                writer.WriteLine("writer.WriteEndObject();");

                writer.Indentation--;
                writer.WriteLine('}');
            }

            private SourceText GenerateForObject(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                string typeFriendlyName = typeMetadata.TypeInfoPropertyName;
                ObjectConstructionStrategy constructionStrategy = typeMetadata.ConstructionStrategy;

                string creatorInvocation = FormatDefaultConstructorExpr(typeMetadata);
                string parameterizedCreatorInvocation = constructionStrategy == ObjectConstructionStrategy.ParameterizedConstructor
                    ? GetParameterizedCtorInvocationFunc(typeMetadata)
                    : "null";

                string? propInitMethodName = null;
                string? propInitAdapterFunc = null;
                string? constructorInfoFactoryFunc = null;
                string? ctorParamMetadataInitMethodName = null;
                string? serializeMethodName = null;

                if (ShouldGenerateMetadata(typeMetadata))
                {
                    propInitMethodName = $"{typeFriendlyName}PropInit";
                    propInitAdapterFunc = $"_ => {propInitMethodName}(options)";

                    if (constructionStrategy is ObjectConstructionStrategy.ParameterizedConstructor)
                    {
                        ctorParamMetadataInitMethodName = $"{typeFriendlyName}CtorParamInit";
                    }

                    if (constructionStrategy is ObjectConstructionStrategy.ParameterlessConstructor
                                             or ObjectConstructionStrategy.ParameterizedConstructor)
                    {
                        string argTypes = typeMetadata.CtorParamGenSpecs.Count == 0
                            ? EmptyTypeArray
                            : $$"""new[] {{{string.Join(", ", typeMetadata.CtorParamGenSpecs.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}}}""";

                        constructorInfoFactoryFunc = $"static () => typeof({typeMetadata.TypeRef.FullyQualifiedName}).GetConstructor(InstanceMemberBindingFlags, binder: null, {argTypes}, modifiers: null)";
                    }
                }

                if (ShouldGenerateSerializationLogic(typeMetadata))
                {
                    serializeMethodName = $"{typeFriendlyName}SerializeHandler";
                }

                string genericArg = typeMetadata.TypeRef.FullyQualifiedName;
                PolymorphismOptionsSpec? polymorphismOptions = typeMetadata.PolymorphismOptions;
                string polymorphismOptionsExpr = FormatPolymorphismOptions(polymorphismOptions);
                string typeClassifierFactoryExpr = polymorphismOptions is not null
                    ? FormatTypeClassifierFactory(polymorphismOptions.TypeClassifierFactoryType)
                    : "null";

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);

                writer.WriteLine($$"""
                    var objectInfo = new {{YamlObjectInfoValuesTypeRef}}<{{genericArg}}>
                    {
                        ObjectCreator = {{creatorInvocation}},
                        ObjectWithParameterizedConstructorCreator = {{parameterizedCreatorInvocation}},
                        PropertyMetadataInitializer = {{propInitAdapterFunc ?? "null"}},
                        ConstructorParameterMetadataInitializer = {{ctorParamMetadataInitMethodName ?? "null"}},
                        ConstructorAttributeProviderFactory = {{constructorInfoFactoryFunc ?? "null"}},
                        SerializeHandler = {{serializeMethodName ?? "null"}},
                        PolymorphismOptions = {{polymorphismOptionsExpr}},
                        TypeClassifierFactory = {{typeClassifierFactoryExpr}},
                    };

                    yamlTypeInfo = {{YamlMetadataServicesTypeRef}}.CreateObjectInfo<{{typeMetadata.TypeRef.FullyQualifiedName}}>(options, objectInfo);
                    yamlTypeInfo.NumberHandling = {{FormatNumberHandling(typeMetadata.NumberHandling)}};
                    """);

                if (typeMetadata is { UnmappedMemberHandling: not null } or { PreferredPropertyObjectCreationHandling: not null })
                {
                    writer.WriteLine();

                    if (typeMetadata.UnmappedMemberHandling != null)
                    {
                        writer.WriteLine($"yamlTypeInfo.UnmappedMemberHandling = {FormatUnmappedMemberHandling(typeMetadata.UnmappedMemberHandling.Value)};");
                    }

                    if (typeMetadata.PreferredPropertyObjectCreationHandling != null)
                    {
                        writer.WriteLine($"yamlTypeInfo.PreferredPropertyObjectCreationHandling = {FormatObjectCreationHandling(typeMetadata.PreferredPropertyObjectCreationHandling.Value)};");
                    }
                }

                GenerateClosedTypeInferenceGuard(writer, typeMetadata);

                GenerateTypeInfoFactoryFooter(writer);

                if (propInitMethodName != null)
                {
                    writer.WriteLine();
                    GeneratePropMetadataInitFunc(writer, contextSpec, propInitMethodName, typeMetadata);
                }

                if (serializeMethodName != null)
                {
                    writer.WriteLine();
                    GenerateFastPathFuncForObject(writer, contextSpec, serializeMethodName, typeMetadata);
                }

                if (ctorParamMetadataInitMethodName != null)
                {
                    writer.WriteLine();
                    GenerateCtorParamMetadataInitFunc(writer, ctorParamMetadataInitMethodName, typeMetadata);
                }

                // Generate UnsafeAccessor methods or reflection cache fields for property accessors.
                _emitValueTypeSetterDelegate |= GeneratePropertyAccessors(writer, contextSpec, typeMetadata);

                // Generate constructor accessor for inaccessible [YamlConstructor] constructors.
                GenerateConstructorAccessor(writer, contextSpec, typeMetadata);

                writer.Indentation--;
                writer.WriteLine('}');

                return CompleteSourceFileAndReturnText(writer);
            }

            private static SourceText GenerateForUnion(ContextGenerationSpec contextSpec, TypeGenerationSpec typeMetadata)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, experimentalDiagnosticIds: typeMetadata.ExperimentalDiagnosticIds);

                GenerateTypeInfoFactoryHeader(writer, typeMetadata);

                string genericArg = typeMetadata.TypeRef.FullyQualifiedName;
                ImmutableEquatableArray<UnionCaseSpec> unionCases = typeMetadata.UnionCaseSpecs;
                UnionCaseSpec? nullCase = unionCases.FirstOrDefault(c => c.IsNullable);

                // C# rejects Nullable<T> in any pattern (CS8116: "It is not legal to use
                // nullable type 'T?' in a pattern; use the underlying type 'T' instead")
                // because at the CLR layer a boxed Nullable<T> with HasValue=true is
                // bit-identical to a boxed T. So when a union declares both Foo(T) and
                // Foo(Nullable<T>) the two ctors must map to the same `T` arm. The parser
                // coalesces such collisions via ITypeSymbol equality on the underlying
                // pattern key and tags exactly one declared case per collision group as
                // the canonical switch arm (preferring the non-Nullable<T> sibling so
                // most-derived dispatch reports typeof(T)). The null payload is handled
                // separately by the `null =>` arm via nullCase.
                string unionCasesExpr = unionCases.Count == 0
                    ? $"global::System.Array.Empty<{YamlUnionCaseInfoTypeRef}>()"
                    : $$"""new {{YamlUnionCaseInfoTypeRef}}[] { {{string.Join(", ", unionCases.Select(c => $"new {YamlUnionCaseInfoTypeRef}(typeof({c.CaseType.FullyQualifiedName})) {{ IsNullable = {(c.IsNullable ? "true" : "false")} }}"))}} }""";

                string typeClassifierFactoryExpr = typeMetadata.UnionClassifierFactoryType is { } classifierFactoryType
                    ? $"new {classifierFactoryType.FullyQualifiedName}()"
                    : "null";

                writer.WriteLine($"var unionInfo = new {YamlUnionInfoValuesTypeRef}<{genericArg}>");
                writer.WriteLine('{');
                writer.Indentation++;

                writer.WriteLine($"UnionCases = {unionCasesExpr},");

                if (unionCases.Count == 0)
                {
                    writer.WriteLine("UnionConstructor = null,");
                    writer.WriteLine("UnionDeconstructor = null,");
                }
                else
                {
                    writer.WriteLine($"UnionConstructor = static ({TypeTypeRef} _, object? value) => value switch");
                    writer.WriteLine('{');
                    writer.Indentation++;

                    int ctorArmIndex = 0;
                    foreach (UnionCaseSpec caseSpec in unionCases)
                    {
                        if (!caseSpec.IsSwitchArm)
                        {
                            continue;
                        }

                        string patternTypeFQN = caseSpec.PatternType.FullyQualifiedName;
                        writer.WriteLine($"{patternTypeFQN} caseValue{ctorArmIndex} => new {genericArg}(caseValue{ctorArmIndex}),");
                        ctorArmIndex++;
                    }

                    if (nullCase is not null)
                    {
                        writer.WriteLine($"null => new {genericArg}({FormatNullCast(nullCase)}null),");
                    }

                    writer.WriteLine($"_ => throw new {YamlExceptionTypeRef}(),");
                    writer.Indentation--;
                    writer.WriteLine("},");

                    // The deconstructor switch has no `_` arm — it relies on the union's
                    // declared case set being exhaustively covered by its arms. Adding a
                    // `_` arm would make it unreachable (CS8510).
                    writer.WriteLine($"UnionDeconstructor = static ({genericArg} value) =>");
                    writer.WriteLine('{');
                    writer.Indentation++;

                    if (!typeMetadata.TypeRef.IsValueType)
                    {
                        writer.WriteLine("""
                            if ((object?)value is null)
                            {
                                return ((global::System.Type?)null, (object?)null);
                            }
                            """);
                        writer.WriteLine();
                    }

                    writer.WriteLine("return value switch");
                    writer.WriteLine('{');
                    writer.Indentation++;

                    if (nullCase is not null)
                    {
                        writer.WriteLine($"null => (typeof({nullCase.CaseType.FullyQualifiedName}), (object?)null),");
                    }
                    else
                    {
                        // Always emit a null arm. C# pattern matching treats default(union)
                        // as the null state (CS8655) and the union's Value property returns
                        // null in that state. Routing to (null, null) signals
                        // YamlUnionConverter to write JSON null instead of throwing.
                        writer.WriteLine("null => ((global::System.Type?)null, (object?)null),");
                    }

                    // unionCases is in topological most-derived-first order, so the first
                    // matching arm always corresponds to the nearest declared case for any
                    // given runtime type.
                    int deconArmIndex = 0;
                    foreach (UnionCaseSpec caseSpec in unionCases)
                    {
                        if (!caseSpec.IsSwitchArm)
                        {
                            continue;
                        }

                        string patternTypeFQN = caseSpec.PatternType.FullyQualifiedName;
                        string caseTypeFQN = caseSpec.CaseType.FullyQualifiedName;

                        writer.WriteLine($"{patternTypeFQN} caseValue{deconArmIndex} => (typeof({caseTypeFQN}), (object?)caseValue{deconArmIndex}),");
                        deconArmIndex++;
                    }

                    writer.Indentation--;
                    writer.WriteLine("};");
                    writer.Indentation--;
                    writer.WriteLine("},");
                }

                writer.WriteLine("TypeClassifier = null,");
                writer.WriteLine($"TypeClassifierFactory = {typeClassifierFactoryExpr},");
                writer.Indentation--;
                writer.WriteLine("};");
                writer.WriteLine();

                writer.WriteLine($"yamlTypeInfo = {YamlMetadataServicesTypeRef}.CreateUnionInfo<{genericArg}>(options, unionInfo);");
                writer.WriteLine($"yamlTypeInfo.NumberHandling = {FormatNumberHandling(typeMetadata.NumberHandling)};");

                GenerateTypeInfoFactoryFooter(writer);

                writer.Indentation--;
                writer.WriteLine('}');

                return CompleteSourceFileAndReturnText(writer);
            }

            /// <summary>
            /// Formats the cast prefix used by the <c>null =&gt;</c> arm of the constructor
            /// switch (e.g. <c>(int?)</c>). For a Nullable&lt;T&gt; case the FQN already ends
            /// in <c>?</c>, so we must not append another <c>?</c>.
            /// </summary>
            private static string FormatNullCast(UnionCaseSpec caseSpec)
            {
                string fqn = caseSpec.CaseType.FullyQualifiedName;
                return caseSpec.CaseType.SpecialType is SpecialType.System_Nullable_T
                    ? $"({fqn})"
                    : $"({fqn}?)";
            }

            private void GeneratePropMetadataInitFunc(SourceWriter writer, ContextGenerationSpec contextSpec, string propInitMethodName, TypeGenerationSpec typeGenerationSpec)
            {
                ImmutableEquatableArray<PropertyGenerationSpec> properties = typeGenerationSpec.PropertyGenSpecs;
                HashSet<string> duplicateMemberNames = GetDuplicateMemberNames(properties);

                writer.WriteLine($"private static {YamlPropertyInfoTypeRef}[] {propInitMethodName}({YamlSerializerOptionsTypeRef} options)");
                writer.WriteLine('{');
                writer.Indentation++;

                writer.WriteLine($"var properties = new {YamlPropertyInfoTypeRef}[{properties.Count}];");
                writer.WriteLine();

                for (int i = 0; i < properties.Count; i++)
                {
                    PropertyGenerationSpec property = properties[i];
                    string propertyName = property.NameSpecifiedInSourceCode;
                    string declaringTypeFQN = property.DeclaringType.FullyQualifiedName;

                    // If the property is ignored and its type is not used anywhere else in the type graph,
                    // emit a YamlPropertyInfo of type 'object' to avoid unnecessarily referencing the type.
                    // STJ requires that all ignored properties be included so that it can perform
                    // necessary run-time validations using configuration not known at compile time
                    // such as the property naming policy and case sensitivity.
                    bool isIgnoredPropertyOfUnusedType =
                        property.DefaultIgnoreCondition is YamlIgnoreCondition.Always &&
                        !_typeIndex.ContainsKey(property.PropertyType);

                    string propertyTypeFQN = isIgnoredPropertyOfUnusedType ? "object" : property.PropertyType.FullyQualifiedName;

                    string getterValue = GetPropertyGetterValue(contextSpec, property, typeGenerationSpec, propertyName, declaringTypeFQN, i, duplicateMemberNames.Contains(property.MemberName));
                    string setterValue = GetPropertySetterValue(contextSpec, property, typeGenerationSpec, propertyName, declaringTypeFQN, i, duplicateMemberNames.Contains(property.MemberName));

                    string ignoreConditionNamedArg = property.DefaultIgnoreCondition.HasValue
                        ? $"{YamlIgnoreConditionTypeRef}.{property.DefaultIgnoreCondition.Value}"
                        : "null";

                    string? converterInstantiationExpr = null;
                    if (property.ConverterType != null)
                    {
                        string converterFQN = property.ConverterType.FullyQualifiedName;
                        TypeRef? nullableUnderlyingType = _typeIndex[property.PropertyType].NullableUnderlyingType;
                        _emitGetConverterForNullablePropertyMethod |= nullableUnderlyingType != null;

                        converterInstantiationExpr = nullableUnderlyingType != null
                            ? $"GetConverterForNullableProperty<{nullableUnderlyingType.FullyQualifiedName}>(new {converterFQN}(), options)"
                            : $"({YamlConverterTypeRef}<{propertyTypeFQN}>)ExpandConverter(typeof({propertyTypeFQN}), new {converterFQN}(), options)";
                    }

                    string attributeProviderFactoryExpr = property switch
                    {
                        _ when isIgnoredPropertyOfUnusedType => "null",
                        { IsProperty: true } => $"typeof({property.DeclaringType.FullyQualifiedName}).GetProperty({FormatStringLiteral(property.MemberName)}, InstanceMemberBindingFlags, null, typeof({propertyTypeFQN}), {EmptyTypeArray}, null)",
                        _ => $"typeof({property.DeclaringType.FullyQualifiedName}).GetField({FormatStringLiteral(property.MemberName)}, InstanceMemberBindingFlags)",
                    };

                    writer.WriteLine($$"""
                        var info{{i}} = new {{YamlPropertyInfoValuesTypeRef}}<{{propertyTypeFQN}}>
                        {
                            IsProperty = {{FormatBoolLiteral(property.IsProperty)}},
                            IsPublic = {{FormatBoolLiteral(property.IsPublic)}},
                            IsVirtual = {{FormatBoolLiteral(property.IsVirtual)}},
                            DeclaringType = typeof({{property.DeclaringType.FullyQualifiedName}}),
                            Converter = {{converterInstantiationExpr ?? "null"}},
                            Getter = {{getterValue}},
                            Setter = {{setterValue}},
                            IgnoreCondition = {{ignoreConditionNamedArg}},
                            HasYamlInclude = {{FormatBoolLiteral(property.HasYamlInclude)}},
                            IsExtensionData = {{FormatBoolLiteral(property.IsExtensionData)}},
                            NumberHandling = {{FormatNumberHandling(property.NumberHandling)}},
                            PropertyName = {{FormatStringLiteral(property.MemberName)}},
                            YamlPropertyName = {{FormatStringLiteral(property.YamlPropertyName)}},
                            AttributeProviderFactory = static () => {{attributeProviderFactoryExpr}},{{FormatM5fPropertyValues(property)}}
                        };

                        properties[{{i}}] = {{YamlMetadataServicesTypeRef}}.CreatePropertyInfo<{{propertyTypeFQN}}>(options, info{{i}});
                        """);

                    if (property.HasYamlRequiredAttribute ||
                        (property.IsRequired && !typeGenerationSpec.ConstructorSetsRequiredParameters))
                    {
                        writer.WriteLine($"properties[{i}].IsRequired = true;");
                    }

                    if (property.ObjectCreationHandling != null)
                    {
                        writer.WriteLine($"properties[{i}].ObjectCreationHandling = {FormatObjectCreationHandling(property.ObjectCreationHandling.Value)};");
                    }

                    if (property.Order != 0)
                    {
                        writer.WriteLine($"properties[{i}].Order = {property.Order};");
                    }

                    if (property.IsGetterNonNullableAnnotation)
                    {
                        writer.WriteLine($"properties[{i}].IsGetNullable = false;");
                    }
                    if (property.IsSetterNonNullableAnnotation)
                    {
                        writer.WriteLine($"properties[{i}].IsSetNullable = false;");
                    }

                    writer.WriteLine();
                }

                writer.WriteLine($"return properties;");
                writer.Indentation--;
                writer.WriteLine('}');
            }

            /// <summary>
            /// Returns true if the property requires an unsafe accessor or reflection fallback
            /// for its getter (i.e. it's inaccessible but has [YamlInclude]).
            /// </summary>
            private static bool NeedsAccessorForGetter(PropertyGenerationSpec property)
                => !property.CanUseGetter && property.HasYamlInclude && property.DefaultIgnoreCondition is not YamlIgnoreCondition.Always;

            /// <summary>
            /// Returns true if the property requires an unsafe accessor or reflection fallback
            /// for its setter (i.e. init-only properties, or inaccessible with [YamlInclude]).
            /// </summary>
            private static bool NeedsAccessorForSetter(PropertyGenerationSpec property)
            {
                if (property.IsReadOnly || property.DefaultIgnoreCondition is YamlIgnoreCondition.Always)
                {
                    return false;
                }

                // All init-only properties need an accessor.
                if (property is { CanUseSetter: true, IsInitOnlySetter: true })
                {
                    return true;
                }

                // Inaccessible [YamlInclude] properties need an accessor.
                if (!property.CanUseSetter && property.HasYamlInclude)
                {
                    return true;
                }

                return false;
            }

            private static string GetUnboxExpression(ContextGenerationSpec contextSpec, string declaringTypeFQN)
            {
                string expression = $"{UnsafeTypeRef}.Unbox<{declaringTypeFQN}>(obj)";
                return contextSpec.UseUpdatedMemorySafetyRules ? $"unsafe({expression})" : expression;
            }

            private static string GetPropertyGetterValue(
                ContextGenerationSpec contextSpec,
                PropertyGenerationSpec property,
                TypeGenerationSpec typeGenerationSpec,
                string propertyName,
                string declaringTypeFQN,
                int propertyIndex,
                bool needsDisambiguation)
            {
                if (property.DefaultIgnoreCondition is YamlIgnoreCondition.Always)
                {
                    return "null";
                }

                if (property.CanUseGetter)
                {
                    return $"static obj => (({declaringTypeFQN})obj).{propertyName}";
                }

                if (NeedsAccessorForGetter(property))
                {
                    string typeFriendlyName = typeGenerationSpec.TypeInfoPropertyName;

                    if (property.CanUseUnsafeAccessors)
                    {
                        // UnsafeAccessor externs for value types take 'ref T'.
                        string castExpr = typeGenerationSpec.TypeRef.IsValueType
                            ? $"ref {GetUnboxExpression(contextSpec, declaringTypeFQN)}"
                            : $"({declaringTypeFQN})obj";

                        string accessorName = property.IsProperty
                            ? GetQualifiedAccessorName(property, typeFriendlyName, "get", property.MemberName, propertyIndex, needsDisambiguation)
                            : GetQualifiedAccessorName(property, typeFriendlyName, "field", property.MemberName, propertyIndex, needsDisambiguation);

                        return $"static obj => {accessorName}({castExpr})";
                    }

                    string getterName = GetAccessorName(typeFriendlyName, "get", property.MemberName, propertyIndex, needsDisambiguation);
                    if (!property.IsProperty)
                    {
                        return $"static obj => {getterName}(obj)";
                    }

                    // Reflection fallback property wrappers are strongly typed; cast in the delegate.
                    return $"static obj => {getterName}(({declaringTypeFQN})obj)";
                }

                return "null";
            }

            private static string GetPropertySetterValue(
                ContextGenerationSpec contextSpec,
                PropertyGenerationSpec property,
                TypeGenerationSpec typeGenerationSpec,
                string propertyName,
                string declaringTypeFQN,
                int propertyIndex,
                bool needsDisambiguation)
            {
                if (property.DefaultIgnoreCondition is YamlIgnoreCondition.Always)
                {
                    return "null";
                }

                if (property is { CanUseSetter: true, IsInitOnlySetter: true })
                {
                    return GetAccessorBasedSetterDelegate(contextSpec, property, typeGenerationSpec, declaringTypeFQN, propertyIndex, needsDisambiguation);
                }

                if (property.CanUseSetter)
                {
                    return typeGenerationSpec.TypeRef.IsValueType
                        ? $"""static (obj, value) => {GetUnboxExpression(contextSpec, declaringTypeFQN)}.{propertyName} = value!"""
                        : $"""static (obj, value) => (({declaringTypeFQN})obj).{propertyName} = value!""";
                }

                if (NeedsAccessorForSetter(property))
                {
                    return GetAccessorBasedSetterDelegate(contextSpec, property, typeGenerationSpec, declaringTypeFQN, propertyIndex, needsDisambiguation);
                }

                return "null";
            }

            /// <summary>
            /// Generates a setter delegate expression that calls the UnsafeAccessor extern directly
            /// or the strongly typed reflection wrapper.
            /// </summary>
            private static string GetAccessorBasedSetterDelegate(
                ContextGenerationSpec contextSpec,
                PropertyGenerationSpec property,
                TypeGenerationSpec typeGenerationSpec,
                string declaringTypeFQN,
                int propertyIndex,
                bool needsDisambiguation)
            {
                string typeFriendlyName = typeGenerationSpec.TypeInfoPropertyName;

                if (property.CanUseUnsafeAccessors)
                {
                    string castExpr = typeGenerationSpec.TypeRef.IsValueType
                        ? $"ref {GetUnboxExpression(contextSpec, declaringTypeFQN)}"
                        : $"({declaringTypeFQN})obj";

                    if (property.IsProperty)
                    {
                        string accessorName = GetQualifiedAccessorName(property, typeFriendlyName, "set", property.MemberName, propertyIndex, needsDisambiguation);
                        return $"static (obj, value) => {accessorName}({castExpr}, value!)";
                    }

                    string fieldName = GetQualifiedAccessorName(property, typeFriendlyName, "field", property.MemberName, propertyIndex, needsDisambiguation);
                    return $"static (obj, value) => {fieldName}({castExpr}) = value!";
                }

                string setterName = GetAccessorName(typeFriendlyName, "set", property.MemberName, propertyIndex, needsDisambiguation);
                if (!property.IsProperty)
                {
                    return $"static (obj, value) => {setterName}(obj, value!)";
                }

                // Reflection fallback property wrappers are strongly typed; cast in the delegate like UnsafeAccessor.
                string setterCastExpr = typeGenerationSpec.TypeRef.IsValueType
                    ? $"ref {GetUnboxExpression(contextSpec, declaringTypeFQN)}"
                    : $"({declaringTypeFQN})obj";

                return $"static (obj, value) => {setterName}({setterCastExpr}, value!)";
            }

            private static bool GeneratePropertyAccessors(SourceWriter writer, ContextGenerationSpec contextSpec, TypeGenerationSpec typeGenerationSpec)
            {
                string safetyModifier = contextSpec.UseUpdatedMemorySafetyRules ? "safe " : "";
                ImmutableEquatableArray<PropertyGenerationSpec> properties = typeGenerationSpec.PropertyGenSpecs;
                HashSet<string> duplicateMemberNames = GetDuplicateMemberNames(properties);
                bool needsAccessors = false;
                bool needsValueTypeSetterDelegate = false;
                Dictionary<string, List<GenericAccessorEntry>>? genericAccessorEntries = null;

                for (int i = 0; i < properties.Count; i++)
                {
                    PropertyGenerationSpec property = properties[i];
                    bool needsGetterAccessor = NeedsAccessorForGetter(property);
                    bool needsSetterAccessor = NeedsAccessorForSetter(property);

                    if (!needsGetterAccessor && !needsSetterAccessor)
                    {
                        continue;
                    }

                    if (!needsAccessors)
                    {
                        writer.WriteLine();
                        needsAccessors = true;
                    }

                    string typeFriendlyName = typeGenerationSpec.TypeInfoPropertyName;
                    string declaringTypeFQN = property.DeclaringType.FullyQualifiedName;
                    string propertyTypeFQN = property.PropertyType.FullyQualifiedName;
                    bool disambiguate = duplicateMemberNames.Contains(property.MemberName);

                    if (property.CanUseUnsafeAccessors)
                    {
                        if (property.DeclaringTypeParameterNames is not null)
                        {
                            // Generic types need a wrapper class for UnsafeAccessor (.NET 9+).
                            // Collect the accessor and emit the wrapper class after the loop.
                            string key = property.DeclaringType.FullyQualifiedName;
                            genericAccessorEntries ??= new();
                            if (!genericAccessorEntries.TryGetValue(key, out List<GenericAccessorEntry>? entries))
                            {
                                entries = new();
                                genericAccessorEntries[key] = entries;
                            }

                            entries.Add((property, i, disambiguate, needsGetterAccessor, needsSetterAccessor));
                        }
                        else
                        {
                            string refPrefix = typeGenerationSpec.TypeRef.IsValueType ? "ref " : "";

                            if (property.IsProperty)
                            {
                                if (needsGetterAccessor)
                                {
                                    string accessorName = GetAccessorName(typeFriendlyName, "get", property.MemberName, i, disambiguate);
                                    writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Method, Name = "get_{property.MemberName}")]""");
                                    writer.WriteLine($"private static {safetyModifier}extern {propertyTypeFQN} {accessorName}({refPrefix}{declaringTypeFQN} obj);");
                                }

                                if (needsSetterAccessor)
                                {
                                    string accessorName = GetAccessorName(typeFriendlyName, "set", property.MemberName, i, disambiguate);
                                    writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Method, Name = "set_{property.MemberName}")]""");
                                    writer.WriteLine($"private static {safetyModifier}extern void {accessorName}({refPrefix}{declaringTypeFQN} obj, {propertyTypeFQN} value);");
                                }
                            }
                            else
                            {
                                // Field: single UnsafeAccessor that returns ref T, used for both get and set.
                                string fieldAccessorName = GetAccessorName(typeFriendlyName, "field", property.MemberName, i, disambiguate);
                                writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Field, Name = "{property.MemberName}")]""");
                                writer.WriteLine($"private static {safetyModifier}extern ref {propertyTypeFQN} {fieldAccessorName}({refPrefix}{declaringTypeFQN} obj);");
                            }
                        }
                    }
                    else if (property.IsProperty)
                    {
                        // Reflection fallback for properties: use Delegate.CreateDelegate on the MethodInfo for efficient invocation.
                        // Wrapper methods are strongly typed to match UnsafeAccessor signatures.
                        string propertyExpr = $"typeof({declaringTypeFQN}).GetProperty({FormatStringLiteral(property.MemberName)}, InstanceMemberBindingFlags, null, typeof({propertyTypeFQN}), {EmptyTypeArray}, null)!";

                        if (needsGetterAccessor)
                        {
                            string cacheName = GetReflectionCacheName(typeFriendlyName, "get", property.MemberName, i, disambiguate);
                            string wrapperName = GetAccessorName(typeFriendlyName, "get", property.MemberName, i, disambiguate);

                            if (typeGenerationSpec.TypeRef.IsValueType)
                            {
                                // For value types, Delegate.CreateDelegate doesn't work with struct instance getters
                                // on .NET Framework (the this parameter is passed by-ref internally).
                                // Cache the MethodInfo and use Invoke instead.
                                string methodCacheType = "global::System.Reflection.MethodInfo";
                                writer.WriteLine($"private static {methodCacheType}? {cacheName};");
                                writer.WriteLine($"private static {propertyTypeFQN} {wrapperName}({declaringTypeFQN} obj) => ({propertyTypeFQN})({cacheName} ??= {propertyExpr}.GetGetMethod(true)!).Invoke(obj, null)!;");
                            }
                            else
                            {
                                string delegateType = $"global::System.Func<{declaringTypeFQN}, {propertyTypeFQN}>";
                                writer.WriteLine($"private static {delegateType}? {cacheName};");
                                writer.WriteLine($"private static {propertyTypeFQN} {wrapperName}({declaringTypeFQN} obj) => ({cacheName} ??= ({delegateType})global::System.Delegate.CreateDelegate(typeof({delegateType}), {propertyExpr}.GetGetMethod(true)!))(obj);");
                            }
                        }

                        if (needsSetterAccessor)
                        {
                            string cacheName = GetReflectionCacheName(typeFriendlyName, "set", property.MemberName, i, disambiguate);
                            string wrapperName = GetAccessorName(typeFriendlyName, "set", property.MemberName, i, disambiguate);

                            if (typeGenerationSpec.TypeRef.IsValueType)
                            {
                                // For value types, use a ref-parameter delegate to mutate the unboxed value in-place.
                                needsValueTypeSetterDelegate = true;
                                string delegateType = $"ValueTypeSetter<{declaringTypeFQN}, {propertyTypeFQN}>";
                                writer.WriteLine($"private static {delegateType}? {cacheName};");
                                writer.WriteLine($"private static void {wrapperName}(ref {declaringTypeFQN} obj, {propertyTypeFQN} value) => ({cacheName} ??= ({delegateType})global::System.Delegate.CreateDelegate(typeof({delegateType}), {propertyExpr}.GetSetMethod(true)!))(ref obj, value);");
                            }
                            else
                            {
                                string delegateType = $"global::System.Action<{declaringTypeFQN}, {propertyTypeFQN}>";
                                writer.WriteLine($"private static {delegateType}? {cacheName};");
                                writer.WriteLine($"private static void {wrapperName}({declaringTypeFQN} obj, {propertyTypeFQN} value) => ({cacheName} ??= ({delegateType})global::System.Delegate.CreateDelegate(typeof({delegateType}), {propertyExpr}.GetSetMethod(true)!))(obj, value);");
                            }
                        }
                    }
                    else
                    {
                        // Reflection fallback for fields: cache the FieldInfo and use GetValue/SetValue.
                        // Fields don't have MethodInfo, so Delegate.CreateDelegate can't be used.
                        string fieldExpr = $"typeof({declaringTypeFQN}).GetField({FormatStringLiteral(property.MemberName)}, InstanceMemberBindingFlags)!";
                        string fieldCacheName = GetReflectionCacheName(typeFriendlyName, "field", property.MemberName, i, disambiguate);
                        writer.WriteLine($"private static global::System.Reflection.FieldInfo? {fieldCacheName};");

                        if (needsGetterAccessor)
                        {
                            string wrapperName = GetAccessorName(typeFriendlyName, "get", property.MemberName, i, disambiguate);
                            writer.WriteLine($"private static {propertyTypeFQN} {wrapperName}(object obj) => ({propertyTypeFQN})({fieldCacheName} ??= {fieldExpr}).GetValue(obj)!;");
                        }

                        if (needsSetterAccessor)
                        {
                            string wrapperName = GetAccessorName(typeFriendlyName, "set", property.MemberName, i, disambiguate);
                            writer.WriteLine($"private static void {wrapperName}(object obj, {propertyTypeFQN} value) => ({fieldCacheName} ??= {fieldExpr}).SetValue(obj, value);");
                        }
                    }
                }

                // Emit generic wrapper classes for UnsafeAccessors on generic types (.NET 9+).
                if (genericAccessorEntries is not null)
                {
                    string typeFriendlyName = typeGenerationSpec.TypeInfoPropertyName;

                    foreach (KeyValuePair<string, List<GenericAccessorEntry>> kvp in genericAccessorEntries)
                    {
                        List<GenericAccessorEntry> entries = kvp.Value;
                        PropertyGenerationSpec firstProperty = entries[0].Property;
                        ImmutableEquatableArray<string> typeParams = firstProperty.DeclaringTypeParameterNames!;
                        string openDeclaringTypeFQN = firstProperty.OpenDeclaringTypeFQN!;
                        string refPrefix = typeGenerationSpec.TypeRef.IsValueType ? "ref " : "";
                        string typeParamList = string.Join(", ", typeParams);
                        string constraintClauses = firstProperty.DeclaringTypeParameterConstraintClauses is { } c ? $" {c}" : "";

                        writer.WriteLine();
                        writer.WriteLine($"private static partial class __GenericAccessors_{typeFriendlyName}_{firstProperty.DeclaringTypeIndex}<{typeParamList}>{constraintClauses}");
                        writer.WriteLine('{');
                        writer.Indentation++;

                        foreach (GenericAccessorEntry entry in entries)
                        {
                            PropertyGenerationSpec property = entry.Property;
                            int index = entry.Index;
                            bool disambiguate = entry.Disambiguate;
                            bool needsGetter = entry.NeedsGetter;
                            bool needsSetter = entry.NeedsSetter;
                            string openPropertyTypeFQN = property.OpenPropertyTypeFQN ?? property.PropertyType.FullyQualifiedName;

                            if (property.IsProperty)
                            {
                                if (needsGetter)
                                {
                                    string accessorName = GetAccessorName(typeFriendlyName, "get", property.MemberName, index, disambiguate);
                                    writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Method, Name = "get_{property.MemberName}")]""");
                                    writer.WriteLine($"public static {safetyModifier}extern {openPropertyTypeFQN} {accessorName}({refPrefix}{openDeclaringTypeFQN} obj);");
                                }

                                if (needsSetter)
                                {
                                    string accessorName = GetAccessorName(typeFriendlyName, "set", property.MemberName, index, disambiguate);
                                    writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Method, Name = "set_{property.MemberName}")]""");
                                    writer.WriteLine($"public static {safetyModifier}extern void {accessorName}({refPrefix}{openDeclaringTypeFQN} obj, {openPropertyTypeFQN} value);");
                                }
                            }
                            else
                            {
                                string fieldAccessorName = GetAccessorName(typeFriendlyName, "field", property.MemberName, index, disambiguate);
                                writer.WriteLine($"""[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Field, Name = "{property.MemberName}")]""");
                                writer.WriteLine($"public static {safetyModifier}extern ref {openPropertyTypeFQN} {fieldAccessorName}({refPrefix}{openDeclaringTypeFQN} obj);");
                            }
                        }

                        writer.Indentation--;
                        writer.WriteLine('}');
                    }
                }

                return needsValueTypeSetterDelegate;
            }

            /// <summary>
            /// Gets the accessor name for a property or field. For UnsafeAccessor this is the extern method name;
            /// for reflection fallback this is the strongly typed wrapper method name.
            /// Use kind "get"/"set" for property getters/setters, or "field" for field UnsafeAccessor externs.
            /// The property index suffix is only appended when needed to disambiguate shadowed members.
            /// </summary>
            private static string GetAccessorName(string typeFriendlyName, string accessorKind, string memberName, int propertyIndex, bool needsDisambiguation)
                => needsDisambiguation
                    ? $"__{accessorKind}_{typeFriendlyName}_{memberName}_{propertyIndex}"
                    : $"__{accessorKind}_{typeFriendlyName}_{memberName}";

            /// <summary>
            /// For properties on generic types using wrapper-class UnsafeAccessors (.NET 9+), returns the
            /// fully qualified accessor reference including the generic wrapper class prefix, e.g.
            /// <c>__GenericAccessors_MyType_0&lt;int&gt;.__get_MyType_Name</c>.
            /// For non-generic types, returns the plain accessor name.
            /// </summary>
            private static string GetQualifiedAccessorName(PropertyGenerationSpec property, string typeFriendlyName, string accessorKind, string memberName, int propertyIndex, bool needsDisambiguation)
            {
                string accessorName = GetAccessorName(typeFriendlyName, accessorKind, memberName, propertyIndex, needsDisambiguation);
                if (property.DeclaringTypeParameterNames is null)
                {
                    return accessorName;
                }

                string closedTypeArgs = property.DeclaringType.FullyQualifiedName;
                int openAngle = closedTypeArgs.IndexOf('<');
                string typeArgsList = closedTypeArgs.Substring(openAngle);
                return $"__GenericAccessors_{typeFriendlyName}_{property.DeclaringTypeIndex}{typeArgsList}.{accessorName}";
            }

            private static string GetReflectionCacheName(string typeFriendlyName, string accessorKind, string memberName, int propertyIndex, bool needsDisambiguation)
                => needsDisambiguation
                    ? $"s_{accessorKind}_{typeFriendlyName}_{memberName}_{propertyIndex}"
                    : $"s_{accessorKind}_{typeFriendlyName}_{memberName}";

            /// <summary>
            /// Returns the set of member names that appear more than once in the property list.
            /// This occurs when derived types shadow base members via the <c>new</c> keyword.
            /// </summary>
            private static HashSet<string> GetDuplicateMemberNames(ImmutableEquatableArray<PropertyGenerationSpec> properties)
            {
                HashSet<string> seen = new();
                HashSet<string> duplicates = new();
                foreach (PropertyGenerationSpec property in properties)
                {
                    if (!seen.Add(property.MemberName))
                    {
                        duplicates.Add(property.MemberName);
                    }
                }

                return duplicates;
            }

            /// <summary>
            /// Gets the unified constructor accessor name. The wrapper has the same
            /// signature for both UnsafeAccessor and reflection fallback:
            /// <c>static TypeName __ctor_TypeName(params)</c>
            /// </summary>
            private static string GetConstructorAccessorName(TypeGenerationSpec typeSpec, bool qualified = true)
            {
                string accessorName = $"__ctor_{typeSpec.TypeInfoPropertyName}";
                if (qualified && typeSpec.CanUseUnsafeAccessorForConstructor && typeSpec.OpenDeclaringTypeFQN is not null)
                {
                    string typeFQN = typeSpec.TypeRef.FullyQualifiedName;
                    string typeArgsList = typeFQN.Substring(typeFQN.IndexOf('<'));
                    return $"__GenericAccessors_{typeSpec.TypeInfoPropertyName}_0{typeArgsList}.{accessorName}";
                }

                return accessorName;
            }

            private static string GetConstructorReflectionCacheName(TypeGenerationSpec typeSpec)
                => $"s_ctor_{typeSpec.TypeInfoPropertyName}";

            /// <summary>
            /// Generates the constructor accessor for inaccessible constructors.
            /// For UnsafeAccessor: emits a [UnsafeAccessor(Constructor)] extern method.
            /// For reflection fallback: emits a cached ConstructorInfo and a wrapper method.
            /// </summary>
            private static void GenerateConstructorAccessor(SourceWriter writer, ContextGenerationSpec contextSpec, TypeGenerationSpec typeSpec)
            {
                if (!typeSpec.ConstructorIsInaccessible)
                {
                    return;
                }

                writer.WriteLine();

                bool useGenericWrapper = typeSpec.CanUseUnsafeAccessorForConstructor && typeSpec.OpenDeclaringTypeFQN is not null;
                string typeFQN = useGenericWrapper ? typeSpec.OpenDeclaringTypeFQN! : typeSpec.TypeRef.FullyQualifiedName;
                string wrapperName = GetConstructorAccessorName(typeSpec, qualified: false);
                ImmutableEquatableArray<ParameterGenerationSpec> parameters = typeSpec.CtorParamGenSpecs;

                if (useGenericWrapper)
                {
                    // The constructor and member accessors share the declaring type's helper.
                    string typeParamList = string.Join(", ", typeSpec.DeclaringTypeParameterNames!);
                    string constraintClauses = typeSpec.DeclaringTypeParameterConstraintClauses is { } c ? $" {c}" : "";
                    writer.WriteLine($"private static partial class __GenericAccessors_{typeSpec.TypeInfoPropertyName}_0<{typeParamList}>{constraintClauses}");
                    writer.WriteLine('{');
                    writer.Indentation++;
                }

                // Build the parameter list for the wrapper method.
                var wrapperParams = new StringBuilder();
                var callArgs = new StringBuilder();

                foreach (ParameterGenerationSpec param in parameters)
                {
                    if (wrapperParams.Length > 0)
                    {
                        wrapperParams.Append(", ");
                        callArgs.Append(", ");
                    }

                    string parameterTypeFQN = (useGenericWrapper ? param.OpenParameterTypeFQN : null) ?? param.ParameterType.FullyQualifiedName;
                    string refModifier = param.RefKind switch
                    {
                        RefKind.Ref => "ref ",
                        RefKind.Out => "out ",
                        // 'in' preserves the readonly by-ref signature without requiring C# 12.
                        RefKind.In or RefKindRefReadOnlyParameter => "in ",
                        _ => "",
                    };
                    wrapperParams.Append($"{refModifier}{parameterTypeFQN} p{param.ParameterIndex}");
                    callArgs.Append(param.RefKind is RefKind.Out ? "null" : $"p{param.ParameterIndex}");
                }

                if (typeSpec.CanUseUnsafeAccessorForConstructor)
                {
                    string safetyModifier = contextSpec.UseUpdatedMemorySafetyRules ? "safe " : "";
                    writer.WriteLine($"[{UnsafeAccessorAttributeTypeRef}({UnsafeAccessorKindTypeRef}.Constructor)]");
                    writer.WriteLine($"{(useGenericWrapper ? "public" : "private")} static {safetyModifier}extern {typeFQN} {wrapperName}({wrapperParams});");
                }
                else
                {
                    // Reflection fallback: cached ConstructorInfo + Invoke.
                    // Note: ConstructorInfo cannot be wrapped in a delegate, so we cache the ConstructorInfo directly.
                    string cacheName = GetConstructorReflectionCacheName(typeSpec);

                    string argTypes = parameters.Count == 0
                        ? EmptyTypeArray
                        : $"new global::System.Type[] {{{string.Join(", ", parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName}){(p.RefKind is RefKind.None ? "" : ".MakeByRefType()")}"))}}}";

                    writer.WriteLine($"private static global::System.Reflection.ConstructorInfo? {cacheName};");

                    string invokeArgs = parameters.Count == 0
                        ? "null"
                        : $"new object?[] {{{callArgs}}}";
                    string constructorInfo = $"{cacheName} ??= typeof({typeFQN}).GetConstructor(InstanceMemberBindingFlags, binder: null, {argTypes}, modifiers: null)!";

                    if (parameters.Any(p => p.RefKind is RefKind.Ref or RefKind.Out))
                    {
                        writer.WriteLine($$"""
                            private static {{typeFQN}} {{wrapperName}}({{wrapperParams}})
                            {
                                object?[] args = {{invokeArgs}};
                                {{typeFQN}} result = ({{typeFQN}})({{constructorInfo}}).Invoke(args);
                            """);
                        writer.Indentation++;

                        foreach (ParameterGenerationSpec param in parameters)
                        {
                            if (param.RefKind is RefKind.Ref or RefKind.Out)
                            {
                                writer.WriteLine($"p{param.ParameterIndex} = ({param.ParameterType.FullyQualifiedName})args[{param.ParameterIndex}]!;");
                            }
                        }

                        writer.WriteLine("return result;");
                        writer.Indentation--;
                        writer.WriteLine('}');
                    }
                    else
                    {
                        writer.WriteLine($"private static {typeFQN} {wrapperName}({wrapperParams}) => ({typeFQN})({constructorInfo}).Invoke({invokeArgs});");
                    }
                }

                if (useGenericWrapper)
                {
                    writer.Indentation--;
                    writer.WriteLine('}');
                }
            }

            /// <summary>
            /// Returns the expression for reading a property value in the fast-path serialization handler.
            /// For accessible properties, this is a direct member access. For inaccessible [YamlInclude]
            /// properties, this uses UnsafeAccessor or reflection.
            /// </summary>
            private static string GetFastPathPropertyValueExpr(
                PropertyGenerationSpec property,
                TypeGenerationSpec typeGenSpec,
                string objectExpr,
                int propertyIndex,
                bool needsDisambiguation)
            {
                if (property.CanUseGetter)
                {
                    return $"{objectExpr}.{property.NameSpecifiedInSourceCode}";
                }

                // Inaccessible [YamlInclude] property/field: call accessor directly.
                string typeFriendlyName = typeGenSpec.TypeInfoPropertyName;

                if (property.CanUseUnsafeAccessors)
                {
                    string accessorName = property.IsProperty
                        ? GetQualifiedAccessorName(property, typeFriendlyName, "get", property.MemberName, propertyIndex, needsDisambiguation)
                        : GetQualifiedAccessorName(property, typeFriendlyName, "field", property.MemberName, propertyIndex, needsDisambiguation);

                    return typeGenSpec.TypeRef.IsValueType
                        ? $"{accessorName}(ref value)"
                        : $"{accessorName}({objectExpr})";
                }

                string getterName = GetAccessorName(typeFriendlyName, "get", property.MemberName, propertyIndex, needsDisambiguation);

                return $"{getterName}({objectExpr})";
            }

            private static void GenerateCtorParamMetadataInitFunc(SourceWriter writer, string ctorParamMetadataInitMethodName, TypeGenerationSpec typeGenerationSpec)
            {
                ImmutableEquatableArray<ParameterGenerationSpec> parameters = typeGenerationSpec.CtorParamGenSpecs;
                ImmutableEquatableArray<PropertyInitializerGenerationSpec> propertyInitializers = typeGenerationSpec.PropertyInitializerSpecs;

                // out parameters don't appear in metadata - they don't receive values from JSON.
                int nonOutParamCount = parameters.Count(p => p.RefKind != RefKind.Out);
                int paramCount = nonOutParamCount + propertyInitializers.Count(propInit => !propInit.MatchesConstructorParameter);
                Debug.Assert(paramCount > 0 || parameters.Any(p => p.RefKind == RefKind.Out));

                writer.WriteLine($"private static {YamlParameterInfoValuesTypeRef}[] {ctorParamMetadataInitMethodName}() => new {YamlParameterInfoValuesTypeRef}[]");
                writer.WriteLine('{');
                writer.Indentation++;

                int i = 0;
                foreach (ParameterGenerationSpec spec in parameters)
                {
                    // Skip out parameters - they don't receive values from JSON deserialization.
                    if (spec.RefKind == RefKind.Out)
                    {
                        continue;
                    }

                    Debug.Assert(spec.ArgsIndex >= 0);
                    writer.WriteLine($$"""
                        new()
                        {
                            Name = {{FormatStringLiteral(spec.Name)}},
                            ParameterType = typeof({{spec.ParameterType.FullyQualifiedName}}),
                            Position = {{spec.ArgsIndex}},
                            HasDefaultValue = {{FormatBoolLiteral(spec.HasDefaultValue)}},
                            DefaultValue = {{(spec.HasDefaultValue ? CSharpSyntaxUtilities.FormatLiteral(spec.DefaultValue, spec.ParameterType) : "null")}},
                            IsNullable = {{FormatBoolLiteral(spec.IsNullable)}},
                        },
                        """);

                    if (++i < paramCount)
                    {
                        writer.WriteLine();
                    }
                }

                foreach (PropertyInitializerGenerationSpec spec in propertyInitializers)
                {
                    if (spec.MatchesConstructorParameter)
                    {
                        continue;
                    }

                    writer.WriteLine($$"""
                        new()
                        {
                            Name = {{FormatStringLiteral(spec.Name)}},
                            ParameterType = typeof({{spec.ParameterType.FullyQualifiedName}}),
                            Position = {{spec.ParameterIndex}},
                            IsNullable = {{FormatBoolLiteral(spec.IsNullable)}},
                            IsMemberInitializer = true,
                        },
                        """);

                    if (++i < paramCount)
                    {
                        writer.WriteLine();
                    }
                }

                writer.Indentation--;
                writer.WriteLine("};");
            }

            private void GenerateFastPathFuncForObject(SourceWriter writer, ContextGenerationSpec contextSpec, string serializeMethodName, TypeGenerationSpec typeGenSpec)
            {
                if (typeGenSpec.FastPathPropertyIndices is null)
                {
                    // Type uses configuration that doesn't support fast-path: emit a stub that just throws.
                    GenerateFastPathFuncHeader(writer, typeGenSpec, serializeMethodName, skipNullCheck: true);

                    string exceptionMessage = string.Format(ExceptionMessages.InvalidSerializablePropertyConfiguration, typeGenSpec.TypeRef.FullyQualifiedName);
                    writer.WriteLine($"""throw new {InvalidOperationExceptionTypeRef}("{exceptionMessage}");""");
                    writer.Indentation--;
                    writer.WriteLine('}');
                    return;
                }

                GenerateFastPathFuncHeader(writer, typeGenSpec, serializeMethodName);

                HashSet<string> duplicateMemberNames = GetDuplicateMemberNames(typeGenSpec.PropertyGenSpecs);

                if (typeGenSpec.ImplementsIYamlOnSerializing)
                {
                    writer.WriteLine($"((global::{YamlConstants.IYamlOnSerializingFullName})value).OnSerializing();");
                    writer.WriteLine();
                }

                writer.WriteLine("writer.WriteStartObject();");
                writer.WriteLine();

                bool generateDisallowNullThrowHelper = false;

                // Provide generation logic for each prop.
                foreach (int i in typeGenSpec.FastPathPropertyIndices)
                {
                    PropertyGenerationSpec propertyGenSpec = typeGenSpec.PropertyGenSpecs[i];

                    if (!propertyGenSpec.ShouldIncludePropertyForFastPath(contextSpec))
                    {
                        continue;
                    }

                    TypeGenerationSpec propertyTypeSpec = _typeIndex[propertyGenSpec.PropertyType];

                    if (propertyTypeSpec.ClassType is ClassType.TypeUnsupportedBySourceGen)
                    {
                        continue;
                    }

                    string effectiveYamlPropertyName = propertyGenSpec.EffectiveYamlPropertyName;
                    string propertyNameFieldName = propertyGenSpec.PropertyNameFieldName;

                    // Add the property names to the context-wide cache; we'll generate the source to initialize them at the end of generation.
                    Debug.Assert(!_propertyNames.TryGetValue(effectiveYamlPropertyName, out string? existingName) || existingName == propertyNameFieldName);
                    _propertyNames.TryAdd(effectiveYamlPropertyName, propertyNameFieldName);

                    SerializedValueCheckType defaultCheckType = GetCheckType(contextSpec, propertyGenSpec);

                    // For properties whose declared type differs from that of the serialized type
                    // perform an explicit cast -- this is to account for hidden properties or diamond ambiguity.
                    string? objectExpr = propertyGenSpec.DeclaringType != typeGenSpec.TypeRef
                        ? $"(({propertyGenSpec.DeclaringType.FullyQualifiedName})value)"
                        : "value";

                    string propValueExpr;
                    // For inaccessible [YamlInclude] properties, use UnsafeAccessor or reflection.
                    string? rawValueExpr = GetFastPathPropertyValueExpr(propertyGenSpec, typeGenSpec, objectExpr, i, duplicateMemberNames.Contains(propertyGenSpec.MemberName));

                    if (defaultCheckType != SerializedValueCheckType.None)
                    {
                        // Use temporary variable to evaluate property value only once
                        string localVariableName =  $"__value_{propertyGenSpec.NameSpecifiedInSourceCode.TrimStart('@')}";
                        writer.WriteLine($"{propertyGenSpec.PropertyType.FullyQualifiedName} {localVariableName} = {rawValueExpr};");
                        propValueExpr = localVariableName;
                    }
                    else
                    {
                        propValueExpr = rawValueExpr;
                    }

                    switch (defaultCheckType)
                    {
                        case SerializedValueCheckType.Ignore:
                            break;

                        case SerializedValueCheckType.IgnoreWhenNull:
                            writer.WriteLine($"if ({propValueExpr} is not null)");
                            writer.WriteLine('{');
                            writer.Indentation++;

                            GenerateSerializePropertyStatement(writer, propertyTypeSpec, propertyNameFieldName, propValueExpr);

                            writer.Indentation--;
                            writer.WriteLine('}');
                            break;

                        case SerializedValueCheckType.IgnoreWhenDefault:
                            writer.WriteLine($"if (!{EqualityComparerTypeRef}<{propertyGenSpec.PropertyType.FullyQualifiedName}>.Default.Equals(default, {propValueExpr}))");
                            writer.WriteLine('{');
                            writer.Indentation++;

                            GenerateSerializePropertyStatement(writer, propertyTypeSpec, propertyNameFieldName, propValueExpr);

                            writer.Indentation--;
                            writer.WriteLine('}');
                            break;

                        case SerializedValueCheckType.DisallowNull:
                            writer.WriteLine($$"""
                                if ({{propValueExpr}} is null)
                                {
                                   ThrowPropertyNullException({{FormatStringLiteral(propertyGenSpec.EffectiveYamlPropertyName)}});
                                }

                                """);

                            GenerateSerializePropertyStatement(writer, propertyTypeSpec, propertyNameFieldName, propValueExpr);
                            generateDisallowNullThrowHelper = true;
                            break;

                        default:
                            GenerateSerializePropertyStatement(writer, propertyTypeSpec, propertyNameFieldName, propValueExpr);
                            break;
                    }
                }

                // End method logic.
                writer.WriteLine();
                writer.WriteLine("writer.WriteEndObject();");

                if (typeGenSpec.ImplementsIYamlOnSerialized)
                {
                    writer.WriteLine();
                    writer.WriteLine($"((global::{YamlConstants.IYamlOnSerializedFullName})value).OnSerialized();");
                }

                if (generateDisallowNullThrowHelper)
                {
                    writer.WriteLine();
                    writer.WriteLine($$"""
                        static void ThrowPropertyNullException(string propertyName)
                        {
                            throw new {{YamlExceptionTypeRef}}(string.Format("{{ExceptionMessages.PropertyGetterDisallowNull}}", propertyName, {{FormatStringLiteral(typeGenSpec.TypeRef.Name)}}));
                        }
                        """);
                }

                writer.Indentation--;
                writer.WriteLine('}');
            }

            // RefKind.RefReadOnlyParameter was added in Roslyn 4.4
            private const RefKind RefKindRefReadOnlyParameter = (RefKind)4;

            private static string GetParameterizedCtorInvocationFunc(TypeGenerationSpec typeGenerationSpec)
            {
                ImmutableEquatableArray<ParameterGenerationSpec> parameters = typeGenerationSpec.CtorParamGenSpecs;
                ImmutableEquatableArray<PropertyInitializerGenerationSpec> propertyInitializers = typeGenerationSpec.PropertyInitializerSpecs;

                bool hasRefOrRefReadonlyParams = parameters.Any(p => p.RefKind == RefKind.Ref || p.RefKind == RefKindRefReadOnlyParameter);

                StringBuilder sb;

                if (hasRefOrRefReadonlyParams)
                {
                    // For ref/ref readonly parameters, we need a block lambda with temp variables
                    sb = new("static args => { ");

                    // Declare temp variables for ref and ref readonly parameters
                    foreach (ParameterGenerationSpec param in parameters)
                    {
                        if (param.RefKind == RefKind.Ref || param.RefKind == RefKindRefReadOnlyParameter)
                        {
                            // Use ArgsIndex to access the args array (out params don't have entries in args)
                            sb.Append($"var __temp{param.ParameterIndex} = ({param.ParameterType.FullyQualifiedName})args[{param.ArgsIndex}]; ");
                        }
                    }

                    if (typeGenerationSpec.ConstructorIsInaccessible)
                    {
                        string accessorName = GetConstructorAccessorName(typeGenerationSpec);
                        sb.Append($"return {accessorName}(");
                    }
                    else
                    {
                        sb.Append($"return new {typeGenerationSpec.TypeRef.FullyQualifiedName}(");
                    }
                }
                else if (typeGenerationSpec.ConstructorIsInaccessible)
                {
                    // Inaccessible constructor: use the unified constructor accessor wrapper.
                    string accessorName = GetConstructorAccessorName(typeGenerationSpec);
                    sb = new($"static args => {accessorName}(");
                }
                else
                {
                    sb = new($"static args => new {typeGenerationSpec.TypeRef.FullyQualifiedName}(");
                }

                if (parameters.Count > 0)
                {
                    foreach (ParameterGenerationSpec param in parameters)
                    {
                        sb.Append($"{GetParamExpression(param)}, ");
                    }

                    sb.Length -= 2; // delete the last ", " token
                }

                sb.Append(')');

                if (propertyInitializers.Count > 0 && !typeGenerationSpec.ConstructorIsInaccessible)
                {
                    sb.Append("{ ");
                    foreach (PropertyInitializerGenerationSpec property in propertyInitializers)
                    {
                        sb.Append($"{property.Name} = ({property.ParameterType.FullyQualifiedName})args[{property.ParameterIndex}], ");
                    }

                    sb.Length -= 2; // delete the last ", " token
                    sb.Append(" }");
                }

                if (hasRefOrRefReadonlyParams)
                {
                    sb.Append("; }");
                }

                return sb.ToString();

                static string GetParamExpression(ParameterGenerationSpec param)
                {
                    return param.RefKind switch
                    {
                        RefKind.Ref => $"ref __temp{param.ParameterIndex}",
                        RefKind.Out => $"out var __discard{param.ParameterIndex}",
                        RefKindRefReadOnlyParameter => $"in __temp{param.ParameterIndex}",
                        // Use ArgsIndex to access the args array (out params don't have entries in args)
                        _ => $"({param.ParameterType.FullyQualifiedName})args[{param.ArgsIndex}]", // None or In (in doesn't require keyword at call site)
                    };
                }
            }

            private static string? GetPrimitiveWriterMethod(TypeGenerationSpec type)
            {
                return type.PrimitiveTypeKind switch
                {
                    YamlPrimitiveTypeKind.Number => "WriteNumber",
                    YamlPrimitiveTypeKind.String or YamlPrimitiveTypeKind.Char => "WriteString",
                    YamlPrimitiveTypeKind.Boolean => "WriteBoolean",
                    YamlPrimitiveTypeKind.ByteArray => "WriteBase64String",
                    _ => null
                };
            }

            private static void GenerateFastPathFuncHeader(SourceWriter writer, TypeGenerationSpec typeGenSpec, string methodName, bool skipNullCheck = false)
            {
                // fast path serializers for reference types always support null inputs.
                string valueTypeRef = typeGenSpec.TypeRef.IsValueType
                    ? typeGenSpec.TypeRef.FullyQualifiedName
                    : typeGenSpec.TypeRef.FullyQualifiedName + "?";

                writer.WriteLine($$"""
                    // Intentionally not a static method because we create a delegate to it. Invoking delegates to instance
                    // methods is almost as fast as virtual calls. Static methods need to go through a shuffle thunk.
                    private void {{methodName}}({{Utf8YamlWriterTypeRef}} writer, {{valueTypeRef}} value)
                    {
                    """);

                writer.Indentation++;

                if (!skipNullCheck && typeGenSpec.TypeRef.CanBeNull)
                {
                    writer.WriteLine($$"""
                        if (value is null)
                        {
                            writer.WriteNullValue();
                            return;
                        }

                        """);
                }
            }

            private void GenerateSerializeValueStatement(SourceWriter writer, TypeGenerationSpec typeSpec, string valueExpr)
            {
                if (GetPrimitiveWriterMethod(typeSpec) is string primitiveWriterMethod)
                {
                    if (typeSpec.PrimitiveTypeKind is YamlPrimitiveTypeKind.Char)
                    {
                        writer.WriteLine($"writer.{primitiveWriterMethod}Value({valueExpr}.ToString());");
                    }
                    else if (typeSpec.PrimitiveTypeKind is YamlPrimitiveTypeKind.ByteArray)
                    {
                        writer.WriteLine($"{ByteArrayValueWriterMethodName}(writer, {valueExpr});");
                        _emitByteArrayValueHelper = true;
                    }
                    else
                    {
                        writer.WriteLine($"writer.{primitiveWriterMethod}Value({valueExpr});");
                    }
                }
                else
                {
                    if (ShouldGenerateSerializationLogic(typeSpec))
                    {
                        writer.WriteLine($"{typeSpec.TypeInfoPropertyName}SerializeHandler(writer, {valueExpr});");
                    }
                    else
                    {
                        writer.WriteLine($"{YamlSerializerTypeRef}.Serialize(writer, {valueExpr}, {typeSpec.TypeInfoPropertyName});");
                    }
                }
            }

            private void GenerateSerializePropertyStatement(SourceWriter writer, TypeGenerationSpec typeSpec, string propertyNameExpr, string valueExpr)
            {
                if (GetPrimitiveWriterMethod(typeSpec) is string primitiveWriterMethod)
                {
                    if (typeSpec.PrimitiveTypeKind is YamlPrimitiveTypeKind.Char)
                    {
                        writer.WriteLine($"writer.{primitiveWriterMethod}({propertyNameExpr}, {valueExpr}.ToString());");
                    }
                    else if (typeSpec.PrimitiveTypeKind is YamlPrimitiveTypeKind.ByteArray)
                    {
                        writer.WriteLine($"writer.WritePropertyName({propertyNameExpr});");
                        writer.WriteLine($"{ByteArrayValueWriterMethodName}(writer, {valueExpr});");
                        _emitByteArrayValueHelper = true;
                    }
                    else
                    {
                        writer.WriteLine($"writer.{primitiveWriterMethod}({propertyNameExpr}, {valueExpr});");
                    }
                }
                else
                {
                    writer.WriteLine($"writer.WritePropertyName({propertyNameExpr});");

                    if (ShouldGenerateSerializationLogic(typeSpec))
                    {
                        writer.WriteLine($"{typeSpec.TypeInfoPropertyName}SerializeHandler(writer, {valueExpr});");
                    }
                    else
                    {
                        writer.WriteLine($"{YamlSerializerTypeRef}.Serialize(writer, {valueExpr}, {typeSpec.TypeInfoPropertyName});");
                    }
                }
            }

            private enum SerializedValueCheckType
            {
                None,
                IgnoreWhenNull,
                IgnoreWhenDefault,
                DisallowNull,
                Ignore
            }

            private static SerializedValueCheckType GetCheckType(ContextGenerationSpec contextSpec, PropertyGenerationSpec propertySpec)
            {
                return (propertySpec.DefaultIgnoreCondition ?? contextSpec.GeneratedOptionsSpec?.DefaultIgnoreCondition) switch
                {
                    YamlIgnoreCondition.WhenWriting => SerializedValueCheckType.Ignore,
                    YamlIgnoreCondition.WhenWritingNull => propertySpec.PropertyType.CanBeNull ? SerializedValueCheckType.IgnoreWhenNull : SerializedValueCheckType.None,
                    YamlIgnoreCondition.WhenWritingDefault => propertySpec.PropertyType.CanBeNull ? SerializedValueCheckType.IgnoreWhenNull : SerializedValueCheckType.IgnoreWhenDefault,
                    _ when propertySpec.IsGetterNonNullableAnnotation && contextSpec.GeneratedOptionsSpec?.RespectNullableAnnotations is true => SerializedValueCheckType.DisallowNull,
                    _ => SerializedValueCheckType.None,
                };
            }

            private static void GenerateTypeInfoFactoryHeader(SourceWriter writer, TypeGenerationSpec typeMetadata)
            {
                string typeFQN = typeMetadata.TypeRef.FullyQualifiedName;
                string typeInfoPropertyName = typeMetadata.TypeInfoPropertyName;
                string typeInfoFQN = $"{YamlTypeInfoTypeRef}<{typeFQN}>";

                writer.WriteLine($$"""
                    private {{typeInfoFQN}}? _{{typeInfoPropertyName}};

                    /// <summary>
                    /// Defines the source generated JSON serialization contract metadata for a given type.
                    /// </summary>
                    #nullable disable annotations // Marking the property type as nullable-oblivious.
                    public {{typeInfoFQN}} {{typeInfoPropertyName}}
                    #nullable enable annotations
                    {
                        get => _{{typeInfoPropertyName}} ??= ({{typeInfoFQN}})Options.GetTypeInfo(typeof({{typeFQN}}));
                    }

                    private {{typeInfoFQN}} {{CreateTypeInfoMethodName(typeMetadata)}}({{YamlSerializerOptionsTypeRef}} options)
                    {
                        if (!TryGetTypeInfoForRuntimeCustomConverter<{{typeFQN}}>(options, out {{typeInfoFQN}} yamlTypeInfo))
                        {
                    """);

                writer.Indentation += 2;
            }

            private static void GenerateTypeInfoFactoryFooter(SourceWriter writer)
            {
                writer.Indentation -= 2;

                // NB OriginatingResolver should be the last property set by the source generator.
                writer.WriteLine($$"""
                        }

                        yamlTypeInfo.OriginatingResolver = this;
                        return yamlTypeInfo;
                    }
                    """);
            }

            /// <summary>
            /// Emits a runtime guard for closed hierarchies whose derived-type polymorphism metadata was not
            /// generated because <c>YamlSourceGenerationOptionsAttribute.InferClosedTypePolymorphism</c> was
            /// disabled at compile time. Enabling the setting only on the runtime <c>YamlSerializerOptions</c>
            /// cannot recover that metadata, so we fail explicitly rather than silently serializing the base
            /// type non-polymorphically.
            /// </summary>
            private static void GenerateClosedTypeInferenceGuard(SourceWriter writer, TypeGenerationSpec typeSpec)
            {
                if (typeSpec.IsClosedTypeWithoutInferredPolymorphism)
                {
                    writer.WriteLine($$"""

                        if (options.InferClosedTypePolymorphism)
                        {
                            throw new {{InvalidOperationExceptionTypeRef}}(string.Format("{{ExceptionMessages.ClosedTypeInferenceRequiresCompileTimeOptIn}}", typeof({{typeSpec.TypeRef.FullyQualifiedName}})));
                        }
                        """);
                }
            }

            private static SourceText GetRootYamlContextImplementation(ContextGenerationSpec contextSpec, bool emitGetConverterForNullablePropertyMethod, bool emitValueTypeSetterDelegate, bool emitByteArrayValueHelper)
            {
                string contextTypeRef = contextSpec.ContextType.FullyQualifiedName;
                string contextTypeName = contextSpec.ContextType.Name;

                int backTickIndex = contextTypeName.IndexOf('`');
                if (backTickIndex != -1)
                {
                    contextTypeName = contextTypeName.Substring(0, backTickIndex);
                }

                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, isPrimaryContextSourceFile: true, experimentalDiagnosticIds: GetContextExperimentalDiagnosticIds(contextSpec));

                GetLogicForDefaultSerializerOptionsInit(contextSpec.GeneratedOptionsSpec, writer, contextSpec.TagMappings);

                writer.WriteLine($"""

                    private const global::System.Reflection.BindingFlags InstanceMemberBindingFlags =
                        global::System.Reflection.BindingFlags.Instance |
                        global::System.Reflection.BindingFlags.Public |
                        global::System.Reflection.BindingFlags.NonPublic;

                    """);

                if (emitValueTypeSetterDelegate)
                {
                    writer.WriteLine("private delegate void ValueTypeSetter<TDeclaringType, TValue>(ref TDeclaringType obj, TValue value);");
                    writer.WriteLine();
                }

                writer.WriteLine($$"""
                    /// <summary>
                    /// The default <see cref="{{YamlSerializerContextTypeRef}}"/> associated with a default <see cref="{{YamlSerializerOptionsTypeRef}}"/> instance.
                    /// </summary>
                    public static {{contextTypeRef}} Default { get; } = new {{contextTypeRef}}(new {{YamlSerializerOptionsTypeRef}}(s_defaultOptions));

                    /// <summary>
                    /// The source-generated options associated with this context.
                    /// </summary>
                    protected override {{YamlSerializerOptionsTypeRef}}? GeneratedSerializerOptions { get; } = s_defaultOptions;

                    /// <inheritdoc/>
                    public {{contextTypeName}}() : base(null)
                    {
                    }

                    /// <inheritdoc/>
                    public {{contextTypeName}}({{YamlSerializerOptionsTypeRef}} options) : base(options)
                    {
                    }
                    """);

                writer.WriteLine();

                if (emitByteArrayValueHelper)
                {
                    writer.WriteLine($$"""
                        private static void {{ByteArrayValueWriterMethodName}}({{Utf8YamlWriterTypeRef}} writer, byte[]? value)
                        {
                            if (value is null)
                            {
                                writer.WriteNullValue();
                            }
                            else
                            {
                                writer.WriteBase64StringValue(value);
                            }
                        }
                        """);
                    writer.WriteLine();
                }

                GenerateConverterHelpers(writer, emitGetConverterForNullablePropertyMethod);

                return CompleteSourceFileAndReturnText(writer);
            }

            private static void GetLogicForDefaultSerializerOptionsInit(SourceGenerationOptionsSpec? optionsSpec, SourceWriter writer, ImmutableEquatableArray<TagMappingSpec> tagMappings)
            {
                if (optionsSpec is null && tagMappings.Count == 0)
                {
                    writer.WriteLine($"private readonly static {YamlSerializerOptionsTypeRef} s_defaultOptions = new();");
                    return;
                }

                if (optionsSpec is null)
                {
                    writer.WriteLine($"private readonly static {YamlSerializerOptionsTypeRef} s_defaultOptions = new()");
                    writer.WriteLine('{');
                    writer.Indentation++;
                    WriteTagMappings(writer, tagMappings);
                    writer.Indentation--;
                    writer.WriteLine("};");
                    return;
                }

                if (optionsSpec.Defaults is YamlSerializerDefaults defaults)
                {
                    writer.WriteLine($"private readonly static {YamlSerializerOptionsTypeRef} s_defaultOptions = new({FormatYamlSerializerDefaults(defaults)})");
                }
                else
                {
                    writer.WriteLine($"private readonly static {YamlSerializerOptionsTypeRef} s_defaultOptions = new()");
                }

                writer.WriteLine('{');
                writer.Indentation++;

                if (optionsSpec.AllowDuplicateProperties is bool allowDuplicateProperties)
                    writer.WriteLine($"AllowDuplicateProperties = {FormatBoolLiteral(allowDuplicateProperties)},");

                if (optionsSpec.AllowOutOfOrderMetadataProperties is bool allowOutOfOrderMetadataProperties)
                    writer.WriteLine($"AllowOutOfOrderMetadataProperties = {FormatBoolLiteral(allowOutOfOrderMetadataProperties)},");

                if (optionsSpec.AllowTrailingCommas is bool allowTrailingCommas)
                    writer.WriteLine($"AllowTrailingCommas = {FormatBoolLiteral(allowTrailingCommas)},");

                if (optionsSpec.Converters is { Count: > 0 } converters)
                {
                    writer.WriteLine("Converters =");
                    writer.WriteLine('{');
                    writer.Indentation++;

                    foreach (TypeRef converter in converters)
                    {
                        writer.WriteLine($"new {converter.FullyQualifiedName}(),");
                    }

                    writer.Indentation--;
                    writer.WriteLine("},");
                }

                if (optionsSpec.TypeClassifiers is { Count: > 0 } TypeClassifiers)
                {
                    writer.WriteLine("TypeClassifiers =");
                    writer.WriteLine('{');
                    writer.Indentation++;

                    foreach (TypeRef classifier in TypeClassifiers)
                    {
                        writer.WriteLine($"new {classifier.FullyQualifiedName}(),");
                    }

                    writer.Indentation--;
                    writer.WriteLine("},");
                }

                if (optionsSpec.DefaultBufferSize is int defaultBufferSize)
                    writer.WriteLine($"DefaultBufferSize = {defaultBufferSize},");

                if (optionsSpec.DefaultIgnoreCondition is YamlIgnoreCondition defaultIgnoreCondition)
                    writer.WriteLine($"DefaultIgnoreCondition = {FormatIgnoreCondition(defaultIgnoreCondition)},");

                if (optionsSpec.DictionaryKeyPolicy is YamlKnownNamingPolicy dictionaryKeyPolicy)
                    writer.WriteLine($"DictionaryKeyPolicy = {FormatNamingPolicy(dictionaryKeyPolicy)},");

                if (optionsSpec.RespectNullableAnnotations is bool respectNullableAnnotations)
                    writer.WriteLine($"RespectNullableAnnotations = {FormatBoolLiteral(respectNullableAnnotations)},");

                if (optionsSpec.RespectRequiredConstructorParameters is bool respectRequiredConstructorParameters)
                    writer.WriteLine($"RespectRequiredConstructorParameters = {FormatBoolLiteral(respectRequiredConstructorParameters)},");

                if (optionsSpec.IgnoreReadOnlyFields is bool ignoreReadOnlyFields)
                    writer.WriteLine($"IgnoreReadOnlyFields = {FormatBoolLiteral(ignoreReadOnlyFields)},");

                if (optionsSpec.IgnoreReadOnlyProperties is bool ignoreReadOnlyProperties)
                    writer.WriteLine($"IgnoreReadOnlyProperties = {FormatBoolLiteral(ignoreReadOnlyProperties)},");

                if (optionsSpec.IncludeFields is bool includeFields)
                    writer.WriteLine($"IncludeFields = {FormatBoolLiteral(includeFields)},");

                if (optionsSpec.InferClosedTypePolymorphism is bool inferClosedTypePolymorphism)
                    writer.WriteLine($"InferClosedTypePolymorphism = {FormatBoolLiteral(inferClosedTypePolymorphism)},");

                if (optionsSpec.MaxDepth is int maxDepth)
                    writer.WriteLine($"MaxDepth = {maxDepth},");

                if (optionsSpec.NewLine is string newLine)
                    writer.WriteLine($"NewLine = {FormatStringLiteral(newLine)},");

                if (optionsSpec.NumberHandling is YamlNumberHandling numberHandling)
                    writer.WriteLine($"NumberHandling = {FormatNumberHandling(numberHandling)},");

                if (optionsSpec.PreferredObjectCreationHandling is YamlObjectCreationHandling preferredObjectCreationHandling)
                    writer.WriteLine($"PreferredObjectCreationHandling = {FormatObjectCreationHandling(preferredObjectCreationHandling)},");

                if (optionsSpec.PropertyNameCaseInsensitive is bool propertyNameCaseInsensitive)
                    writer.WriteLine($"PropertyNameCaseInsensitive = {FormatBoolLiteral(propertyNameCaseInsensitive)},");

                if (optionsSpec.PropertyNamingPolicy is YamlKnownNamingPolicy propertyNamingPolicy)
                    writer.WriteLine($"PropertyNamingPolicy = {FormatNamingPolicy(propertyNamingPolicy)},");

                if (optionsSpec.ReadCommentHandling is YamlCommentHandling readCommentHandling)
                    writer.WriteLine($"ReadCommentHandling = {FormatCommentHandling(readCommentHandling)},");

                if (optionsSpec.ReferenceHandler is YamlKnownReferenceHandler referenceHandler)
                    writer.WriteLine($"ReferenceHandler = {FormatReferenceHandler(referenceHandler)},");

                if (optionsSpec.UnknownTypeHandling is YamlUnknownTypeHandling unknownTypeHandling)
                    writer.WriteLine($"UnknownTypeHandling = {FormatUnknownTypeHandling(unknownTypeHandling)},");

                if (optionsSpec.UnmappedMemberHandling is YamlUnmappedMemberHandling unmappedMemberHandling)
                    writer.WriteLine($"UnmappedMemberHandling = {FormatUnmappedMemberHandling(unmappedMemberHandling)},");

                if (optionsSpec.WriteIndented is bool writeIndented)
                    writer.WriteLine($"WriteIndented = {FormatBoolLiteral(writeIndented)},");

                if (optionsSpec.IndentCharacter is char indentCharacter)
                    writer.WriteLine($"IndentCharacter = {FormatCharLiteral(indentCharacter)},");

                if (optionsSpec.IndentSize is int indentSize)
                    writer.WriteLine($"IndentSize = {indentSize},");

                // M5f: after AllowDuplicateProperties, so that DuplicateKeyHandling wins when both are set.

                if (optionsSpec.WriteStyle is YamlWriterStyle writeStyle)
                    writer.WriteLine($"WriteStyle = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlWriterStyle", writeStyle)},");

                if (optionsSpec.IndentSequences is bool indentSequences)
                    writer.WriteLine($"IndentSequences = {FormatBoolLiteral(indentSequences)},");

                if (optionsSpec.MultiLineStringStyle is YamlScalarStyle multiLineStringStyle)
                    writer.WriteLine($"MultiLineStringStyle = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlScalarStyle", multiLineStringStyle)},");

                if (optionsSpec.DefaultScalarStyle is YamlScalarStyle defaultScalarStyle)
                    writer.WriteLine($"DefaultScalarStyle = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlScalarStyle", defaultScalarStyle)},");

                if (optionsSpec.IgnoreSourceStyles is bool ignoreSourceStyles)
                    writer.WriteLine($"IgnoreSourceStyles = {FormatBoolLiteral(ignoreSourceStyles)},");

                if (optionsSpec.MaxLineWidth is int maxLineWidth)
                    writer.WriteLine($"MaxLineWidth = {maxLineWidth},");

                if (optionsSpec.ExpandSequenceItems is bool expandSequenceItems)
                    writer.WriteLine($"ExpandSequenceItems = {FormatBoolLiteral(expandSequenceItems)},");

                if (optionsSpec.QuoteForLenientReaders is bool quoteForLenientReaders)
                    writer.WriteLine($"QuoteForLenientReaders = {FormatBoolLiteral(quoteForLenientReaders)},");

                if (optionsSpec.Schema is YamlSchemaKind schema)
                    writer.WriteLine($"Schema = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlSchemaKind", schema)},");

                if (optionsSpec.DuplicateKeyHandling is YamlDuplicateKeyHandling duplicateKeyHandling)
                    writer.WriteLine($"DuplicateKeyHandling = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlDuplicateKeyHandling", duplicateKeyHandling)},");

                if (optionsSpec.MappingOrder is YamlMappingOrder mappingOrder)
                    writer.WriteLine($"MappingOrder = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.Serialization.YamlMappingOrder", mappingOrder)},");

                if (optionsSpec.MaxAliasExpansion is int maxAliasExpansion)
                    writer.WriteLine($"MaxAliasExpansion = {maxAliasExpansion},");

                if (optionsSpec.ResolveMergeKeys is bool resolveMergeKeys)
                    writer.WriteLine($"ResolveMergeKeys = {FormatBoolLiteral(resolveMergeKeys)},");

                if (optionsSpec.AcceptLenientScalars is bool acceptLenientScalars)
                    writer.WriteLine($"AcceptLenientScalars = {FormatBoolLiteral(acceptLenientScalars)},");

                if (optionsSpec.DisallowImplicitCollectionKeys is bool disallowImplicitCollectionKeys)
                    writer.WriteLine($"DisallowImplicitCollectionKeys = {FormatBoolLiteral(disallowImplicitCollectionKeys)},");

                if (optionsSpec.InheritTagDirectives is bool inheritTagDirectives)
                    writer.WriteLine($"InheritTagDirectives = {FormatBoolLiteral(inheritTagDirectives)},");

                WriteTagMappings(writer, tagMappings);

                writer.Indentation--;
                writer.WriteLine("};");

                // M5d: the context's [YamlTagMapping] entries, only when there are some.
                static void WriteTagMappings(SourceWriter writer, ImmutableEquatableArray<TagMappingSpec> tagMappings)
                {
                    if (tagMappings.Count == 0)
                    {
                        return;
                    }

                    writer.WriteLine("TagMappings =");
                    writer.WriteLine('{');
                    writer.Indentation++;
                    foreach (TagMappingSpec mapping in tagMappings)
                    {
                        writer.WriteLine($"{{ {FormatStringLiteral(mapping.Tag)}, typeof({mapping.Type.FullyQualifiedName}) }},");
                    }

                    writer.Indentation--;
                    writer.WriteLine("},");
                }

                static string FormatNamingPolicy(YamlKnownNamingPolicy knownNamingPolicy)
                {
                    string? policyName = knownNamingPolicy switch
                    {
                        YamlKnownNamingPolicy.CamelCase => nameof(YamlNamingPolicy.CamelCase),
                        YamlKnownNamingPolicy.SnakeCaseLower => nameof(YamlNamingPolicy.SnakeCaseLower),
                        YamlKnownNamingPolicy.SnakeCaseUpper => nameof(YamlNamingPolicy.SnakeCaseUpper),
                        YamlKnownNamingPolicy.KebabCaseLower => nameof(YamlNamingPolicy.KebabCaseLower),
                        YamlKnownNamingPolicy.KebabCaseUpper => nameof(YamlNamingPolicy.KebabCaseUpper),
                        YamlKnownNamingPolicy.PascalCase => nameof(YamlNamingPolicy.PascalCase),
                        _ => null,
                    };

                    return policyName != null
                    ? $"{YamlNamingPolicyTypeRef}.{policyName}"
                    : "null";
                }

                static string FormatReferenceHandler(YamlKnownReferenceHandler referenceHandler)
                {
                    string? referenceHandlerName = referenceHandler switch
                    {
                        YamlKnownReferenceHandler.Preserve => "Preserve",
                        YamlKnownReferenceHandler.IgnoreCycles => "IgnoreCycles",
                        YamlKnownReferenceHandler.PreserveAll => "PreserveAll",
                        _ => null,
                    };

                    return referenceHandlerName != null
                    ? $"{ReferenceHandlerTypeRef}.{referenceHandlerName}"
                    : "null";
                }
            }

            private static void GenerateConverterHelpers(SourceWriter writer, bool emitGetConverterForNullablePropertyMethod)
            {
                // The generic type parameter could capture type parameters from containing types,
                // so use a name that is unlikely to be used.
                writer.WriteLine($$"""
                    private static bool TryGetTypeInfoForRuntimeCustomConverter<TYamlMetadataType>({{YamlSerializerOptionsTypeRef}} options, out {{YamlTypeInfoTypeRef}}<TYamlMetadataType> yamlTypeInfo)
                    {
                        {{YamlConverterTypeRef}}? converter = GetRuntimeConverterForType(typeof(TYamlMetadataType), options);
                        if (converter != null)
                        {
                            yamlTypeInfo = {{YamlMetadataServicesTypeRef}}.CreateValueInfo<TYamlMetadataType>(options, converter);
                            return true;
                        }

                        yamlTypeInfo = null;
                        return false;
                    }

                    private static {{YamlConverterTypeRef}}? GetRuntimeConverterForType({{TypeTypeRef}} type, {{YamlSerializerOptionsTypeRef}} options)
                    {
                        for (int i = 0; i < options.Converters.Count; i++)
                        {
                            {{YamlConverterTypeRef}}? converter = options.Converters[i];
                            if (converter?.CanConvert(type) == true)
                            {
                                return ExpandConverter(type, converter, options, validateCanConvert: false);
                            }
                        }

                        return null;
                    }

                    private static {{YamlConverterTypeRef}} ExpandConverter({{TypeTypeRef}} type, {{YamlConverterTypeRef}} converter, {{YamlSerializerOptionsTypeRef}} options, bool validateCanConvert = true)
                    {
                        if (validateCanConvert && !converter.CanConvert(type))
                        {
                            throw new {{InvalidOperationExceptionTypeRef}}(string.Format("{{ExceptionMessages.IncompatibleConverterType}}", converter.GetType(), type));
                        }

                        if (converter is {{YamlConverterFactoryTypeRef}} factory)
                        {
                            converter = factory.CreateConverter(type, options);
                            if (converter is null || converter is {{YamlConverterFactoryTypeRef}})
                            {
                                throw new {{InvalidOperationExceptionTypeRef}}(string.Format("{{ExceptionMessages.InvalidYamlConverterFactoryOutput}}", factory.GetType()));
                            }
                        }

                        return converter;
                    }
                    """);

                if (emitGetConverterForNullablePropertyMethod)
                {
                    writer.WriteLine($$"""

                        private static {{YamlConverterTypeRef}}<TYamlMetadataType?> GetConverterForNullableProperty<TYamlMetadataType>({{YamlConverterTypeRef}} converter, {{YamlSerializerOptionsTypeRef}} options)
                            where TYamlMetadataType : struct
                        {
                            if (converter.CanConvert(typeof(TYamlMetadataType?)))
                            {
                                return ({{YamlConverterTypeRef}}<TYamlMetadataType?>)ExpandConverter(typeof(TYamlMetadataType?), converter, options, validateCanConvert: false);
                            }

                            converter = ExpandConverter(typeof(TYamlMetadataType), converter, options);
                            {{YamlTypeInfoTypeRef}}<TYamlMetadataType> typeInfo = {{YamlMetadataServicesTypeRef}}.CreateValueInfo<TYamlMetadataType>(options, converter);
                            return {{YamlMetadataServicesTypeRef}}.GetNullableConverter<TYamlMetadataType>(typeInfo);
                        }
                        """);
                }
            }

            private static SourceText GetGetTypeInfoImplementation(ContextGenerationSpec contextSpec)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec, interfaceImplementation: YamlTypeInfoResolverTypeRef, experimentalDiagnosticIds: GetContextExperimentalDiagnosticIds(contextSpec));

                // YamlSerializerContext.GetTypeInfo override -- returns cached metadata via YamlSerializerOptions
                writer.WriteLine($$"""
                    /// <inheritdoc/>
                    public override {{YamlTypeInfoTypeRef}}? GetTypeInfo({{TypeTypeRef}} type)
                    {
                        Options.TryGetTypeInfo(type, out {{YamlTypeInfoTypeRef}}? typeInfo);
                        return typeInfo;
                    }
                    """);

                writer.WriteLine();

                // Explicit IYamlTypeInfoResolver implementation -- the source of truth for metadata resolution
                writer.WriteLine($"{YamlTypeInfoTypeRef}? {YamlTypeInfoResolverTypeRef}.GetTypeInfo({TypeTypeRef} type, {YamlSerializerOptionsTypeRef} options)");
                writer.WriteLine('{');
                writer.Indentation++;

                foreach (TypeGenerationSpec metadata in contextSpec.GeneratedTypes)
                {
                    if (metadata.ClassType != ClassType.TypeUnsupportedBySourceGen)
                    {
                        writer.WriteLine($$"""
                            if (type == typeof({{metadata.TypeRef.FullyQualifiedName}}))
                            {
                                return {{CreateTypeInfoMethodName(metadata)}}(options);
                            }
                            """);
                    }
                }

                writer.WriteLine("return null;");

                writer.Indentation--;
                writer.WriteLine('}');

                return CompleteSourceFileAndReturnText(writer);
            }

            private SourceText GetPropertyNameInitialization(ContextGenerationSpec contextSpec)
            {
                SourceWriter writer = CreateSourceWriterWithContextHeader(contextSpec);

                foreach (KeyValuePair<string, string> name_varName_pair in _propertyNames)
                {
                    writer.WriteLine($$"""private static readonly {{YamlEncodedTextTypeRef}} {{name_varName_pair.Value}} = {{YamlEncodedTextTypeRef}}.Encode({{FormatStringLiteral(name_varName_pair.Key)}});""");
                }

                return CompleteSourceFileAndReturnText(writer);
            }

            // M5f: only when set, so that the output for other members is unchanged.
            private static string FormatM5fPropertyValues(PropertyGenerationSpec property)
            {
                string text = "";
                if (property.Comment is string comment)
                {
                    text += $"\n    Comment = {FormatStringLiteral(comment)},";
                }

                if (property.ScalarStyle is YamlScalarStyle style)
                {
                    text += $"\n    ScalarStyle = {SourceGeneratorHelpers.FormatEnumLiteral("global::System.Text.Yaml.YamlScalarStyle", style)},";
                }

                return text;
            }

            private static string FormatNumberHandling(YamlNumberHandling? numberHandling)
                => numberHandling.HasValue
                ? SourceGeneratorHelpers.FormatEnumLiteral(YamlNumberHandlingTypeRef, numberHandling.Value)
                : "null";

            private static string FormatObjectCreationHandling(YamlObjectCreationHandling creationHandling)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlObjectCreationHandlingTypeRef, creationHandling);

            private static string FormatUnmappedMemberHandling(YamlUnmappedMemberHandling unmappedMemberHandling)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlUnmappedMemberHandlingTypeRef, unmappedMemberHandling);

            private static string FormatTypeClassifierFactory(TypeRef? classifierFactoryType)
                => classifierFactoryType is not null ? $"new {classifierFactoryType.FullyQualifiedName}()" : "null";

            private static string FormatPolymorphismOptions(PolymorphismOptionsSpec? options)
            {
                if (options is null)
                {
                    // Current source-generated metadata uses an empty options instance to
                    // signal that polymorphism attributes were evaluated at compile time and
                    // found to be absent. The runtime treats null as legacy metadata and runs
                    // the reflection-based compatibility fallback.
                    return $"new {YamlPolymorphismOptionsTypeRef}()";
                }

                var source = new StringBuilder();
                source.AppendLine($"new {YamlPolymorphismOptionsTypeRef}");
                source.AppendLine("{");
                source.AppendLine($"    IgnoreUnrecognizedTypeDiscriminators = {FormatBoolLiteral(options.IgnoreUnrecognizedTypeDiscriminators)},");
                source.AppendLine($"    TypeDiscriminatorPropertyName = {FormatStringLiteral(options.TypeDiscriminatorPropertyName)},");
                source.AppendLine($"    UnknownDerivedTypeHandling = {SourceGeneratorHelpers.FormatEnumLiteral(YamlUnknownDerivedTypeHandlingTypeRef, options.UnknownDerivedTypeHandling)},");
                if (options.DiscriminatorStyle != YamlTypeDiscriminatorStyle.Property)
                {
                    // M5d: written only when set, so that the output for other hierarchies is unchanged.
                    source.AppendLine($"    DiscriminatorStyle = {SourceGeneratorHelpers.FormatEnumLiteral(YamlTypeDiscriminatorStyleTypeRef, options.DiscriminatorStyle)},");
                }

                if (options.DerivedTypes.Count > 0)
                {
                    source.AppendLine("    DerivedTypes =");
                    source.AppendLine("    {");

                    foreach (DerivedTypeSpec derivedType in options.DerivedTypes)
                    {
                        source.Append("        ");
                        source.Append(FormatDerivedType(derivedType));
                        source.AppendLine(",");
                    }

                    source.AppendLine("    },");
                }

                source.Append('}');
                return source.ToString();
            }

            private static string FormatDerivedType(DerivedTypeSpec derivedType)
            {
                string derivedTypeExpr = $"typeof({derivedType.DerivedType.FullyQualifiedName})";

                string created = derivedType.TypeDiscriminator switch
                {
                    null => $"new {YamlDerivedTypeTypeRef}({derivedTypeExpr})",
                    int intDiscriminator => $"new {YamlDerivedTypeTypeRef}({derivedTypeExpr}, {intDiscriminator.ToString(CultureInfo.InvariantCulture)})",
                    string stringDiscriminator => $"new {YamlDerivedTypeTypeRef}({derivedTypeExpr}, {FormatStringLiteral(stringDiscriminator)})",
                    _ => throw new InvalidOperationException(),
                };

                // M5d: the derived type's tag, only when set.
                return derivedType.Tag is string tag ? $"{created} {{ Tag = {FormatStringLiteral(tag)} }}" : created;
            }

            private static string FormatCommentHandling(YamlCommentHandling commentHandling)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlCommentHandlingTypeRef, commentHandling);

            private static string FormatUnknownTypeHandling(YamlUnknownTypeHandling commentHandling)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlUnknownTypeHandlingTypeRef, commentHandling);

            private static string FormatIgnoreCondition(YamlIgnoreCondition ignoreCondition)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlIgnoreConditionTypeRef, ignoreCondition);

            private static string FormatYamlSerializerDefaults(YamlSerializerDefaults defaults)
                => SourceGeneratorHelpers.FormatEnumLiteral(YamlSerializerDefaultsTypeRef, defaults);

            private static string GetCreateValueInfoMethodRef(string typeCompilableName) => $"CreateValueInfo<{typeCompilableName}>";

            private static string FormatBoolLiteral(bool value) => value ? "true" : "false";
            private static string FormatStringLiteral(string? value) => value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);
            private static string FormatCharLiteral(char value) => SymbolDisplay.FormatLiteral(value, quote: true);

            /// <summary>
            /// Method used to generate YamlTypeInfo given options instance
            /// </summary>
            private static string CreateTypeInfoMethodName(TypeGenerationSpec typeSpec)
                => $"Create_{typeSpec.TypeInfoPropertyName}";


            private static string FormatDefaultConstructorExpr(TypeGenerationSpec typeSpec)
            {
                return typeSpec switch
                {
                    { RuntimeTypeRef: TypeRef runtimeType } => $"() => new {runtimeType.FullyQualifiedName}()",
                    { IsValueTuple: true } => $"() => default({typeSpec.TypeRef.FullyQualifiedName})",
                    { ConstructionStrategy: ObjectConstructionStrategy.ParameterlessConstructor, ConstructorIsInaccessible: false } => $"() => new {typeSpec.TypeRef.FullyQualifiedName}()",
                    { ConstructionStrategy: ObjectConstructionStrategy.ParameterlessConstructor, ConstructorIsInaccessible: true } =>
                        $"static () => {GetConstructorAccessorName(typeSpec)}()",
                    _ => "null",
                };
            }

            private static string GetCollectionInfoMethodName(CollectionType collectionType)
            {
                return collectionType switch
                {
                    CollectionType.Array => "CreateArrayInfo",
                    CollectionType.List => "CreateListInfo",
                    CollectionType.IListOfT or CollectionType.IList => "CreateIListInfo",
                    CollectionType.ICollectionOfT => "CreateICollectionInfo",
                    CollectionType.IEnumerableOfT or CollectionType.IEnumerable => "CreateIEnumerableInfo",
                    CollectionType.StackOfT or CollectionType.Stack => "CreateStackInfo",
                    CollectionType.QueueOfT or CollectionType.Queue => "CreateQueueInfo",
                    CollectionType.ConcurrentStack => "CreateConcurrentStackInfo",
                    CollectionType.ConcurrentQueue => "CreateConcurrentQueueInfo",
                    CollectionType.ImmutableEnumerable => "CreateImmutableEnumerableInfo",
                    CollectionType.IAsyncEnumerableOfT => "CreateIAsyncEnumerableInfo",
                    CollectionType.MemoryOfT => "CreateMemoryInfo",
                    CollectionType.ReadOnlyMemoryOfT => "CreateReadOnlyMemoryInfo",
                    CollectionType.ISet => "CreateISetInfo",
                    CollectionType.IReadOnlySetOfT => "CreateIReadOnlySetInfo",
                    CollectionType.Dictionary => "CreateDictionaryInfo",
                    CollectionType.IDictionaryOfTKeyTValue or CollectionType.IDictionary => "CreateIDictionaryInfo",
                    CollectionType.IReadOnlyDictionary => "CreateIReadOnlyDictionaryInfo",
                    CollectionType.ImmutableDictionary => "CreateImmutableDictionaryInfo",

                    _ => throw new Exception(),
                };
            }

            private static bool ShouldGenerateMetadata(TypeGenerationSpec typeSpec)
                => IsGenerationModeSpecified(typeSpec, YamlSourceGenerationMode.Metadata);

            private bool _contextHasTagMappings;

            // M5f: a context whose default ignore condition omits empty collections has no fast path.
            private bool _contextOmitsEmptyCollections;

            private bool ShouldGenerateSerializationLogic(TypeGenerationSpec typeSpec)
                => !_contextHasTagMappings && !_contextOmitsEmptyCollections && IsGenerationModeSpecified(typeSpec, YamlSourceGenerationMode.Serialization) && typeSpec.IsFastPathSupported();

            private static bool IsGenerationModeSpecified(TypeGenerationSpec typeSpec, YamlSourceGenerationMode mode)
                => typeSpec.GenerationMode == YamlSourceGenerationMode.Default || (mode & typeSpec.GenerationMode) != 0;
        }
    }
}
