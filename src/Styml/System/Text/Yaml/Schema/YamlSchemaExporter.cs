// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Schema
{
    /// <summary>
    /// Functionality for exporting JSON schema from serialization contracts defined in <see cref="YamlTypeInfo"/>.
    /// </summary>
    public static class YamlSchemaExporter
    {
        /// <summary>
        /// Gets the JSON schema for <paramref name="type"/> as a <see cref="YamlNode"/> document.
        /// </summary>
        /// <param name="options">The options declaring the contract for the type.</param>
        /// <param name="type">The type for which to resolve a schema.</param>
        /// <param name="exporterOptions">The options object governing the export operation.</param>
        /// <returns>A YAML object containing the schema for <paramref name="type"/>.</returns>
        public static YamlNode GetYamlSchemaAsNode(this YamlSerializerOptions options, Type type, YamlSchemaExporterOptions? exporterOptions = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(type);

            ValidateOptions(options);
            YamlTypeInfo typeInfo = options.GetTypeInfoInternal(type);
            return typeInfo.GetYamlSchemaAsNode(exporterOptions);
        }

        /// <summary>
        /// Gets the JSON schema for <paramref name="typeInfo"/> as a <see cref="YamlNode"/> document.
        /// </summary>
        /// <param name="typeInfo">The contract from which to resolve the JSON schema.</param>
        /// <param name="exporterOptions">The options object governing the export operation.</param>
        /// <returns>A YAML object containing the schema for <paramref name="typeInfo"/>.</returns>
        public static YamlNode GetYamlSchemaAsNode(this YamlTypeInfo typeInfo, YamlSchemaExporterOptions? exporterOptions = null)
        {
            ArgumentNullException.ThrowIfNull(typeInfo);

            ValidateOptions(typeInfo.Options);
            exporterOptions ??= YamlSchemaExporterOptions.Default;

            typeInfo.EnsureConfigured();
            GenerationState state = new(typeInfo.Options, exporterOptions);
            YamlSchema schema = MapYamlSchemaCore(ref state, typeInfo);
            return schema.ToYamlNode(exporterOptions);
        }

        private static YamlSchema MapYamlSchemaCore(
            ref GenerationState state,
            YamlTypeInfo typeInfo,
            YamlPropertyInfo? propertyInfo = null,
            YamlConverter? customConverter = null,
            YamlNumberHandling? customNumberHandling = null,
            YamlTypeInfo? parentPolymorphicTypeInfo = null,
            bool parentPolymorphicTypeContainsTypesWithoutDiscriminator = false,
            bool parentPolymorphicTypeIsNonNullable = false,
            KeyValuePair<string, YamlSchema>? typeDiscriminator = null,
            bool cacheResult = true)
        {
            Debug.Assert(typeInfo.IsConfigured);

            YamlSchemaExporterContext exporterContext = state.CreateContext(typeInfo, propertyInfo, parentPolymorphicTypeInfo);

            if (cacheResult && typeInfo.Kind is not YamlTypeInfoKind.None &&
                state.TryGetExistingYamlPointer(exporterContext, out string? existingYamlPointer))
            {
                // The schema context has already been generated in the schema document, return a reference to it.
                return CompleteSchema(ref state, new YamlSchema { Ref = existingYamlPointer });
            }

            YamlConverter effectiveConverter = customConverter ?? typeInfo.Converter;
            YamlNumberHandling effectiveNumberHandling = customNumberHandling ?? typeInfo.NumberHandling ?? typeInfo.Options.NumberHandling;
            if (effectiveConverter.GetSchema(effectiveNumberHandling) is { } schema)
            {
                // A schema has been provided by the converter.
                return CompleteSchema(ref state, schema);
            }

            if (parentPolymorphicTypeInfo is null && typeInfo.PolymorphismOptions is { DerivedTypes.Count: > 0 } polyOptions)
            {
                // This is the base type of a polymorphic type hierarchy. The schema for this type
                // will include an "anyOf" property with the schemas for all derived types.
                string typeDiscriminatorKey = polyOptions.TypeDiscriminatorPropertyName;
                List<YamlDerivedType> derivedTypes = new(polyOptions.DerivedTypes);

                if (!typeInfo.Type.IsAbstract && !IsPolymorphicTypeThatSpecifiesItselfAsDerivedType(typeInfo))
                {
                    // For non-abstract base types that haven't been explicitly configured,
                    // add a trivial schema to the derived types since we should support it.
                    derivedTypes.Add(new YamlDerivedType(typeInfo.Type));
                }

                // M5d: under DiscriminatorStyle.Tag the derived types are told apart by a tag, which JSON Schema has
                // no notion of: the anyOf has no discriminator property (and a collection no $type/$values wrapper).
                bool byTag = polyOptions.DiscriminatorStyle == YamlTypeDiscriminatorStyle.Tag;
                bool containsTypesWithoutDiscriminator = byTag || derivedTypes.Exists(static derivedTypes => derivedTypes.TypeDiscriminator is null);
                YamlSchemaType schemaType = YamlSchemaType.Any;
                List<YamlSchema>? anyOf = new(derivedTypes.Count);

                state.PushSchemaNode(YamlSchema.AnyOfPropertyName);

                foreach (YamlDerivedType derivedType in derivedTypes)
                {
                    Debug.Assert(derivedType.TypeDiscriminator is null or int or string);

                    KeyValuePair<string, YamlSchema>? derivedTypeDiscriminator = null;
                    if (!byTag && derivedType.TypeDiscriminator is { } discriminatorValue)
                    {
                        YamlNode discriminatorNode = discriminatorValue switch
                        {
                            string stringId => (YamlNode)stringId,
                            _ => (YamlNode)(int)discriminatorValue,
                        };

                        YamlSchema discriminatorSchema = new() { Constant = discriminatorNode };
                        derivedTypeDiscriminator = new(typeDiscriminatorKey, discriminatorSchema);
                    }

                    YamlTypeInfo derivedTypeInfo = typeInfo.Options.GetTypeInfoInternal(derivedType.DerivedType);

                    state.PushSchemaNode(anyOf.Count.ToString(CultureInfo.InvariantCulture));
                    YamlSchema derivedSchema = MapYamlSchemaCore(
                        ref state,
                        derivedTypeInfo,
                        parentPolymorphicTypeInfo: typeInfo,
                        typeDiscriminator: derivedTypeDiscriminator,
                        parentPolymorphicTypeContainsTypesWithoutDiscriminator: containsTypesWithoutDiscriminator,
                        parentPolymorphicTypeIsNonNullable: propertyInfo is { IsGetNullable: false, IsSetNullable: false },
                        cacheResult: false);

                    state.PopSchemaNode();

                    // Determine if all derived schemas have the same type.
                    if (anyOf.Count == 0)
                    {
                        schemaType = derivedSchema.Type;
                    }
                    else if (schemaType != derivedSchema.Type)
                    {
                        schemaType = YamlSchemaType.Any;
                    }

                    anyOf.Add(derivedSchema);
                }

                state.PopSchemaNode();

                if (schemaType is not YamlSchemaType.Any)
                {
                    // If all derived types have the same schema type, we can simplify the schema
                    // by moving the type keyword to the base schema and removing it from the derived schemas.
                    foreach (YamlSchema derivedSchema in anyOf)
                    {
                        derivedSchema.Type = YamlSchemaType.Any;

                        if (derivedSchema.KeywordCount == 0)
                        {
                            // if removing the type results in an empty schema,
                            // remove the anyOf array entirely since it's always true.
                            anyOf = null;
                            break;
                        }
                    }
                }

                return CompleteSchema(ref state, new()
                {
                    Type = schemaType,
                    AnyOf = anyOf,
                    // If all derived types have a discriminator, we can require it in the base schema.
                    Required = containsTypesWithoutDiscriminator ? null : [typeDiscriminatorKey]
                });
            }

            if (effectiveConverter.NullableElementConverter is { } elementConverter)
            {
                YamlTypeInfo elementTypeInfo = typeInfo.Options.GetTypeInfo(elementConverter.Type!);
                schema = MapYamlSchemaCore(ref state, elementTypeInfo, customConverter: elementConverter, customNumberHandling: customNumberHandling ?? typeInfo.NumberHandling, cacheResult: false);

                if (elementConverter.IsIeeeFloatingPointConverter &&
                    (effectiveNumberHandling & YamlNumberHandling.AllowNamedFloatingPointLiterals) != 0)
                {
                    // IEEE floating-point types with AllowNamedFloatingPointLiterals generate an anyOf schema.
                    // Fold null into the numeric branch to preserve nullability for nullable wrappers.
                    Debug.Assert(schema.AnyOf is not null, "IEEE floating-point types with AllowNamedFloatingPointLiterals should generate an anyOf schema.");

                    List<YamlSchema> anyOf = schema.AnyOf;
                    Debug.Assert(anyOf.Exists(b => (b.Type & YamlSchemaType.Number) != 0),
                        "IEEE floating-point anyOf schema should have a numeric branch.");

                    foreach (YamlSchema branch in anyOf)
                    {
                        if ((branch.Type & YamlSchemaType.Number) != 0)
                        {
                            branch.Type |= YamlSchemaType.Null;
                            break;
                        }
                    }
                }
                else if (schema.Enum is not null)
                {
                    Debug.Assert(elementTypeInfo.Type.IsEnum, "The enum keyword should only be populated by schemas for enum types.");
                    schema.Enum.Add(null); // Append null to the enum array.
                }

                return CompleteSchema(ref state, schema);
            }

            switch (typeInfo.Kind)
            {
                case YamlTypeInfoKind.Object:
                    List<KeyValuePair<string, YamlSchema>>? properties = null;
                    List<string>? required = null;
                    YamlSchema? additionalProperties = null;

                    YamlUnmappedMemberHandling effectiveUnmappedMemberHandling = typeInfo.UnmappedMemberHandling ?? typeInfo.Options.UnmappedMemberHandling;
                    if (effectiveUnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
                    {
                        additionalProperties = YamlSchema.CreateFalseSchema();
                    }

                    if (typeDiscriminator is { } typeDiscriminatorPair)
                    {
                        (properties ??= []).Add(typeDiscriminatorPair);
                        if (parentPolymorphicTypeContainsTypesWithoutDiscriminator)
                        {
                            // Require the discriminator here since it's not common to all derived types.
                            (required ??= []).Add(typeDiscriminatorPair.Key);
                        }
                    }

                    state.PushSchemaNode(YamlSchema.PropertiesPropertyName);
                    foreach (YamlPropertyInfo property in typeInfo.Properties)
                    {
                        if (property is { Get: null, Set: null } or { IsExtensionData: true })
                        {
                            continue; // Skip YamlIgnored properties and extension data
                        }

                        state.PushSchemaNode(property.Name);
                        YamlSchema propertySchema = MapYamlSchemaCore(
                            ref state,
                            property.YamlTypeInfo,
                            propertyInfo: property,
                            customConverter: property.EffectiveConverter,
                            customNumberHandling: property.EffectiveNumberHandling);

                        state.PopSchemaNode();

                        if (property.AssociatedParameter is { HasDefaultValue: true } parameterInfo)
                        {
                            YamlSchema.EnsureMutable(ref propertySchema);
                            propertySchema.DefaultValue = YamlSerializer.SerializeToNode(parameterInfo.DefaultValue, property.YamlTypeInfo);
                            propertySchema.HasDefaultValue = true;
                        }

                        (properties ??= []).Add(new(property.Name, propertySchema));

                        // Mark as required if either the property is required or the associated constructor parameter is non-optional.
                        // While the latter implies the former in cases where the YamlSerializerOptions.RespectRequiredConstructorParameters
                        // setting has been enabled, for the case of the schema exporter we always mark non-optional constructor parameters as required.
                        if (property is { IsRequired: true } or { AssociatedParameter.IsRequiredParameter: true })
                        {
                            (required ??= []).Add(property.Name);
                        }
                    }

                    state.PopSchemaNode();
                    return CompleteSchema(ref state, new()
                    {
                        Type = YamlSchemaType.Object,
                        Properties = properties,
                        Required = required,
                        AdditionalProperties = additionalProperties,
                    });

                case YamlTypeInfoKind.Enumerable:
                    Debug.Assert(typeInfo.ElementTypeInfo is not null);

                    if (typeDiscriminator is null)
                    {
                        state.PushSchemaNode(YamlSchema.ItemsPropertyName);
                        YamlSchema items = MapYamlSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);
                        state.PopSchemaNode();

                        return CompleteSchema(ref state, new()
                        {
                            Type = YamlSchemaType.Array,
                            Items = items.IsTrue ? null : items,
                        });
                    }
                    else
                    {
                        // Polymorphic enumerable types are represented using a wrapping object:
                        // { "$type" : "discriminator", "$values" : [element1, element2, ...] }
                        // Which corresponds to the schema
                        // { "properties" : { "$type" : { "const" : "discriminator" }, "$values" : { "type" : "array", "items" : { ... } } } }
                        const string ValuesKeyword = YamlSerializer.ValuesPropertyName;

                        state.PushSchemaNode(YamlSchema.PropertiesPropertyName);
                        state.PushSchemaNode(ValuesKeyword);
                        state.PushSchemaNode(YamlSchema.ItemsPropertyName);

                        YamlSchema items = MapYamlSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);

                        state.PopSchemaNode();
                        state.PopSchemaNode();
                        state.PopSchemaNode();

                        return CompleteSchema(ref state, new()
                        {
                            Type = YamlSchemaType.Object,
                            Properties =
                            [
                                typeDiscriminator.Value,
                                new(ValuesKeyword,
                                    new YamlSchema()
                                    {
                                        Type = YamlSchemaType.Array,
                                        Items = items.IsTrue ? null : items,
                                    }),
                            ],
                            Required = parentPolymorphicTypeContainsTypesWithoutDiscriminator ? [typeDiscriminator.Value.Key] : null,
                        });
                    }

                case YamlTypeInfoKind.Dictionary:
                    Debug.Assert(typeInfo.ElementTypeInfo is not null);

                    List<KeyValuePair<string, YamlSchema>>? dictProps = null;
                    List<string>? dictRequired = null;

                    if (typeDiscriminator is { } dictDiscriminator)
                    {
                        dictProps = [dictDiscriminator];
                        if (parentPolymorphicTypeContainsTypesWithoutDiscriminator)
                        {
                            // Require the discriminator here since it's not common to all derived types.
                            dictRequired = [dictDiscriminator.Key];
                        }
                    }

                    state.PushSchemaNode(YamlSchema.AdditionalPropertiesPropertyName);
                    YamlSchema valueSchema = MapYamlSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);
                    state.PopSchemaNode();

                    return CompleteSchema(ref state, new()
                    {
                        Type = YamlSchemaType.Object,
                        Properties = dictProps,
                        Required = dictRequired,
                        AdditionalProperties = valueSchema.IsTrue ? null : valueSchema,
                    });

                case YamlTypeInfoKind.Union:
                    if (typeInfo.UnionCases is { Count: > 0 } unionCases)
                    {
                        YamlSchemaType unionSchemaType = YamlSchemaType.Any;
                        List<YamlSchema>? unionAnyOf = new(unionCases.Count);

                        state.PushSchemaNode(YamlSchema.AnyOfPropertyName);

                        foreach (YamlUnionCaseInfo caseInfo in unionCases)
                        {
                            YamlTypeInfo caseTypeInfo = typeInfo.Options.GetTypeInfoInternal(caseInfo.CaseType);

                            state.PushSchemaNode(unionAnyOf.Count.ToString(CultureInfo.InvariantCulture));
                            YamlSchema caseSchema = MapYamlSchemaCore(ref state, caseTypeInfo, customNumberHandling: typeInfo.NumberHandling, cacheResult: false);
                            state.PopSchemaNode();

                            if (caseInfo.IsNullable)
                            {
                                caseSchema.Type |= YamlSchemaType.Null;
                            }

                            if (unionAnyOf.Count == 0)
                            {
                                unionSchemaType = caseSchema.Type;
                            }
                            else if (unionSchemaType != caseSchema.Type)
                            {
                                unionSchemaType = YamlSchemaType.Any;
                            }

                            unionAnyOf.Add(caseSchema);
                        }

                        state.PopSchemaNode();

                        if (unionSchemaType is not YamlSchemaType.Any)
                        {
                            foreach (YamlSchema caseSchema in unionAnyOf)
                            {
                                caseSchema.Type = YamlSchemaType.Any;

                                if (caseSchema.KeywordCount == 0)
                                {
                                    unionAnyOf = null;
                                    break;
                                }
                            }
                        }

                        return CompleteSchema(ref state, new()
                        {
                            Type = unionSchemaType,
                            AnyOf = unionAnyOf,
                        });
                    }

                    return CompleteSchema(ref state, YamlSchema.CreateTrueSchema());

                default:
                    Debug.Assert(typeInfo.Kind is YamlTypeInfoKind.None);
                    // Return a `true` schema for types with user-defined converters.
                    return CompleteSchema(ref state, YamlSchema.CreateTrueSchema());
            }

            YamlSchema CompleteSchema(ref GenerationState state, YamlSchema schema)
            {
                if (HasObsoleteAttribute(typeInfo.Type) ||
                    HasObsoleteAttribute(propertyInfo?.AttributeProvider))
                {
                    YamlSchema.EnsureMutable(ref schema);
                    schema.Deprecated = true;
                }

                if (schema.Ref is null)
                {
                    if (IsNullableSchema(state.ExporterOptions))
                    {
                        schema.MakeNullable();
                    }

                    bool IsNullableSchema(YamlSchemaExporterOptions options)
                    {
                        // A schema is marked as nullable if either:
                        // 1. We have a schema for a property where either the getter or setter are marked as nullable.
                        // 2. We have a schema for a Nullable<T> type.
                        // 3. We have a schema for a reference type, unless we're explicitly treating null-oblivious types as non-nullable.

                        if (propertyInfo is not null)
                        {
                            return propertyInfo.IsGetNullable || propertyInfo.IsSetNullable;
                        }

                        if (typeInfo.IsNullable)
                        {
                            return true;
                        }

                        return !typeInfo.Type.IsValueType && !parentPolymorphicTypeIsNonNullable && !options.TreatNullObliviousAsNonNullable;
                    }
                }

                if (state.ExporterOptions.TransformSchemaNode is not null)
                {
                    // Prime the schema for invocation by the YamlNode transformer.
                    schema.ExporterContext = exporterContext;
                }

                return schema;
            }
        }

        private static void ValidateOptions(YamlSerializerOptions options)
        {
            if (options.ReferenceHandler == ReferenceHandler.Preserve)
            {
                ThrowHelper.ThrowNotSupportedException_YamlSchemaExporterDoesNotSupportReferenceHandlerPreserve();
            }

            options.MakeReadOnly();
        }

        private static bool HasObsoleteAttribute(ICustomAttributeProvider? attributeProvider)
        {
            if (attributeProvider is null)
            {
                return false;
            }

            // Identify ObsoleteAttribute using its full type name rather than typeof(ObsoleteAttribute).
            // On downlevel targets System.Text.Yaml compiles in an internal ObsoleteAttribute polyfill
            // that would otherwise shadow the framework type, causing the typeof comparison to never match
            // the ObsoleteAttribute applied by user code.
            foreach (object attribute in attributeProvider.GetCustomAttributes(inherit: true))
            {
                if (attribute.GetType().FullName == "System.ObsoleteAttribute")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPolymorphicTypeThatSpecifiesItselfAsDerivedType(YamlTypeInfo typeInfo)
        {
            Debug.Assert(typeInfo.PolymorphismOptions is not null);

            foreach (YamlDerivedType derivedType in typeInfo.PolymorphismOptions.DerivedTypes)
            {
                if (derivedType.DerivedType == typeInfo.Type)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly ref struct GenerationState(YamlSerializerOptions options, YamlSchemaExporterOptions exporterOptions)
        {
            private readonly List<string> _currentPath = [];
            private readonly Dictionary<(YamlTypeInfo, YamlPropertyInfo?), string[]> _generated = new();

            public int CurrentDepth => _currentPath.Count;
            public YamlSerializerOptions Options { get; } = options;
            public YamlSchemaExporterOptions ExporterOptions { get; } = exporterOptions;

            public void PushSchemaNode(string nodeId)
            {
                if (CurrentDepth == Options.EffectiveMaxDepth)
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlSchemaExporterDepthTooLarge();
                }

                _currentPath.Add(nodeId);
            }

            public void PopSchemaNode()
            {
                Debug.Assert(CurrentDepth > 0);
                _currentPath.RemoveAt(_currentPath.Count - 1);
            }

            /// <summary>
            /// Registers the current schema node generation context; if it has already been generated return a YAML pointer to its location.
            /// </summary>
            public bool TryGetExistingYamlPointer(in YamlSchemaExporterContext context, [NotNullWhen(true)] out string? existingYamlPointer)
            {
                (YamlTypeInfo TypeInfo, YamlPropertyInfo? PropertyInfo) key = (context.TypeInfo, context.PropertyInfo);
#if NET
                ref string[]? pathToSchema = ref CollectionsMarshal.GetValueRefOrAddDefault(_generated, key, out bool exists);
#else
                bool exists = _generated.TryGetValue(key, out string[]? pathToSchema);
#endif
                if (exists)
                {
                    existingYamlPointer = FormatYamlPointer(pathToSchema);
                    return true;
                }
#if NET
                pathToSchema = context._path;
#else
                _generated[key] = context._path;
#endif
                existingYamlPointer = null;
                return false;
            }

            public YamlSchemaExporterContext CreateContext(YamlTypeInfo typeInfo, YamlPropertyInfo? propertyInfo, YamlTypeInfo? baseTypeInfo)
            {
                return new YamlSchemaExporterContext(typeInfo, propertyInfo, baseTypeInfo, [.. _currentPath]);
            }

            private static string FormatYamlPointer(ReadOnlySpan<string> path)
            {
                if (path.IsEmpty)
                {
                    return "#";
                }

                using ValueStringBuilder sb = new(initialCapacity: path.Length * 10);
                sb.Append('#');

                foreach (string segment in path)
                {
                    sb.Append('/');

                    // Per RFC 6901 the characters '~' and '/' are escaped as '~0' and '~1'.
                    string escapedToken = segment.Replace("~", "~0").Replace("/", "~1");

                    // Per RFC 6901 section 6 the JSON Pointer is embedded in a URI fragment,
                    // so percent-encode any characters that are not valid in a URI fragment.
                    sb.Append(Uri.EscapeDataString(escapedToken));
                }

                return sb.ToString();
            }
        }
    }
}
