// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Validates and indexes polymorphic type configuration,
    /// providing derived YamlTypeInfo resolution methods
    /// in both serialization and deserialization scenaria.
    /// </summary>
    internal sealed class PolymorphicTypeResolver
    {
        private readonly ConcurrentDictionary<Type, DerivedYamlTypeInfo?> _typeToDiscriminatorId = new();
        private readonly Dictionary<object, DerivedYamlTypeInfo>? _discriminatorIdtoType;
        private readonly YamlSerializerOptions _options;

        // M5d: the derived types' expanded tags, compared with a node's expanded tag; null when no tag is read.
        private readonly (byte[] Expanded, DerivedYamlTypeInfo Derived)[]? _tagToType;

        public PolymorphicTypeResolver(YamlSerializerOptions options, YamlPolymorphismOptions polymorphismOptions, Type baseType, bool converterCanHaveMetadata)
            : this(options, polymorphismOptions, baseType, converterCanHaveMetadata, fromTagMappings: false)
        {
        }

        private PolymorphicTypeResolver(YamlSerializerOptions options, YamlPolymorphismOptions polymorphismOptions, Type baseType, bool converterCanHaveMetadata, bool fromTagMappings)
        {
            Style = polymorphismOptions.DiscriminatorStyle;
            FromTagMappings = fromTagMappings;
            UnknownDerivedTypeHandling = polymorphismOptions.UnknownDerivedTypeHandling;
            IgnoreUnrecognizedTypeDiscriminators = polymorphismOptions.IgnoreUnrecognizedTypeDiscriminators;
            BaseType = baseType;
            _options = options;

            if (!IsSupportedPolymorphicBaseType(BaseType))
            {
                ThrowHelper.ThrowInvalidOperationException_TypeDoesNotSupportPolymorphism(BaseType);
            }

            bool containsDerivedTypes = false;
            List<(byte[], DerivedYamlTypeInfo)>? tags = null;
            foreach (YamlDerivedType derived in polymorphismOptions.DerivedTypes)
            {
                (Type derivedType, object? typeDiscriminator) = derived;
                Debug.Assert(typeDiscriminator is null or int or string);

                if (!IsSupportedDerivedType(BaseType, derivedType) ||
                    (derivedType.IsAbstract && UnknownDerivedTypeHandling != YamlUnknownDerivedTypeHandling.FallBackToNearestAncestor))
                {
                    ThrowHelper.ThrowInvalidOperationException_DerivedTypeNotSupported(BaseType, derivedType);
                }

                YamlTypeInfo derivedTypeInfo = options.GetTypeInfoInternal(derivedType);
                string? writtenTag = ResolveTag(derived, baseType, Style, fromTagMappings);
                DerivedYamlTypeInfo derivedTypeInfoHolder = new(typeDiscriminator, derivedTypeInfo, writtenTag);
                if (writtenTag is not null)
                {
                    // The configured form (a verbatim written tag's content) expanded as the reader expands it.
                    byte[] expanded = YamlTags.GetExpandedUtf8(writtenTag.StartsWith("!<", StringComparison.Ordinal)
                        ? writtenTag.Substring(2, writtenTag.Length - 3)
                        : writtenTag);
                    foreach ((byte[] existing, _) in tags ??= new())
                    {
                        if (existing.AsSpan().SequenceEqual(expanded))
                        {
                            ThrowHelper.ThrowInvalidOperationException(SR.Format(SR.YamlTagAlreadySpecified, writtenTag, BaseType));
                        }
                    }

                    tags.Add((expanded, derivedTypeInfoHolder));
                }

                if (!_typeToDiscriminatorId.TryAdd(derivedType, derivedTypeInfoHolder))
                {
                    ThrowHelper.ThrowInvalidOperationException_DerivedTypeIsAlreadySpecified(BaseType, derivedType);
                }

                if (typeDiscriminator is not null)
                {
                    if (!(_discriminatorIdtoType ??= new()).TryAdd(typeDiscriminator, derivedTypeInfoHolder))
                    {
                        ThrowHelper.ThrowInvalidOperationException_TypeDicriminatorIdIsAlreadySpecified(BaseType, typeDiscriminator);
                    }

                    UsesTypeDiscriminators = true;
                }

                containsDerivedTypes = true;
            }

            if (!containsDerivedTypes && !fromTagMappings)
            {
                ThrowHelper.ThrowInvalidOperationException_PolymorphicTypeConfigurationDoesNotSpecifyDerivedTypes(BaseType);
            }

            _tagToType = Style != YamlTypeDiscriminatorStyle.Property && tags is not null ? tags.ToArray() : null;
            ReadsTags = Style != YamlTypeDiscriminatorStyle.Property;
            if (ReadsTags && !converterCanHaveMetadata)
            {
                ThrowHelper.ThrowNotSupportedException_BaseConverterDoesNotSupportMetadata(BaseType);
            }

            if (UsesTypeDiscriminators)
            {
                Debug.Assert(_discriminatorIdtoType is not null, "Discriminator index must have been populated.");

                if (!converterCanHaveMetadata)
                {
                    ThrowHelper.ThrowNotSupportedException_BaseConverterDoesNotSupportMetadata(BaseType);
                }

                string propertyName = polymorphismOptions.TypeDiscriminatorPropertyName;
                if (!propertyName.Equals(YamlSerializer.TypePropertyName, StringComparison.Ordinal))
                {
                    byte[] utf8EncodedName = Encoding.UTF8.GetBytes(propertyName);

                    // Check if the property name conflicts with other metadata property names
                    if ((YamlSerializer.GetMetadataPropertyName(utf8EncodedName, resolver: null) & ~MetadataPropertyName.Type) != 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidCustomTypeDiscriminatorPropertyName();
                    }

                    CustomTypeDiscriminatorPropertyNameUtf8 = utf8EncodedName;
                    CustomTypeDiscriminatorPropertyNameYamlEncoded = YamlEncodedText.Encode(propertyName, options.Encoder);
                }

                // Check if the discriminator property name conflicts with any derived property names.
                foreach (DerivedYamlTypeInfo derivedTypeInfo in _discriminatorIdtoType.Values)
                {
                    if (derivedTypeInfo.YamlTypeInfo.Kind is YamlTypeInfoKind.Object)
                    {
                        foreach (YamlPropertyInfo property in derivedTypeInfo.YamlTypeInfo.Properties)
                        {
                            if (property is { IsIgnored: false, IsExtensionData: false } && property.Name == propertyName)
                            {
                                ThrowHelper.ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(derivedTypeInfo.YamlTypeInfo.Type, propertyName);
                            }
                        }
                    }
                }
            }
        }

        public Type BaseType { get; }

        /// <summary>M5d: how the hierarchy writes its derived types.</summary>
        public YamlTypeDiscriminatorStyle Style { get; }

        /// <summary>Whether a tag at this site can select a type: <c>Tag</c> and <c>Both</c>, decided once.</summary>
        public bool ReadsTags { get; }

        /// <summary>Built from the options' <c>TagMappings</c> for a site with no declared hierarchy.</summary>
        public bool FromTagMappings { get; }

        /// <summary>
        /// The resolver <c>TagMappings</c> gives a reference-type site with no declared hierarchy: the mapped
        /// types it can hold, with their tags and no discriminators; an unmapped runtime type writes as without it.
        /// </summary>
        internal static PolymorphicTypeResolver? CreateForTagMappings(YamlSerializerOptions options, Type baseType, bool converterCanHaveMetadata)
        {
            if (!converterCanHaveMetadata || !IsSupportedPolymorphicBaseType(baseType) || baseType == typeof(object) || options.TagMappingTable is not { } table)
            {
                return null;
            }

            var polymorphism = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBaseType,
            };

            foreach ((string written, _, Type type) in table.Entries)
            {
                // The site's own type is not tagged: a member typed Dog writes no !dog.
                if (type != baseType && IsSupportedDerivedType(baseType, type))
                {
                    polymorphism.DerivedTypes.Add(new YamlDerivedType(type) { Tag = written.StartsWith("!<", StringComparison.Ordinal) ? written.Substring(2, written.Length - 3) : written });
                }
            }

            if (polymorphism.DerivedTypes.Count == 0)
            {
                // No mapped type fits the site: it stays as it is, tags ignored.
                return null;
            }

            return new PolymorphicTypeResolver(options, polymorphism, baseType, converterCanHaveMetadata, fromTagMappings: true);
        }

        // The tag a derived type writes under Tag and Both, checked; under Property an explicit Tag is checked and unused.
        private static string? ResolveTag(YamlDerivedType derived, Type baseType, YamlTypeDiscriminatorStyle style, bool fromTagMappings)
        {
            string? tag = derived.Tag;
            if (tag is null)
            {
                if (style == YamlTypeDiscriminatorStyle.Property || derived.TypeDiscriminator is null)
                {
                    return null;
                }

                tag = "!" + (derived.TypeDiscriminator is int number ? number.ToString(Globalization.CultureInfo.InvariantCulture) : (string)derived.TypeDiscriminator);
                if (YamlTags.GetWrittenForm(tag, out _) is not string fromDiscriminator)
                {
                    ThrowHelper.ThrowInvalidOperationException(SR.Format(SR.YamlTagDiscriminatorNotATag, derived.TypeDiscriminator, derived.DerivedType));
                    return null;
                }

                return fromDiscriminator;
            }

            if (YamlTags.GetWrittenForm(tag, out string? error) is not string written)
            {
                ThrowHelper.ThrowInvalidOperationException(error!);
                return null;
            }

            if (style == YamlTypeDiscriminatorStyle.Property)
            {
                return null;
            }

            if (derived.TypeDiscriminator is null && !fromTagMappings)
            {
                ThrowHelper.ThrowInvalidOperationException(SR.Format(SR.YamlTagWithoutDiscriminator, derived.DerivedType, baseType));
            }

            return written;
        }

        /// <summary>
        /// The derived type a node's expanded application tag selects, or null for an unrecognized tag under
        /// <c>IgnoreUnrecognizedTypeDiscriminators</c> or a resolver <c>TagMappings</c> built. A tag mapped to a type
        /// this site cannot hold is an error; another unrecognized tag throws unless ignored.
        /// </summary>
        public YamlTypeInfo? FindByTag(ReadOnlySpan<byte> expandedTag)
        {
            Debug.Assert(ReadsTags && !expandedTag.IsEmpty);
            if (_tagToType is not null)
            {
                foreach ((byte[] expanded, DerivedYamlTypeInfo derived) in _tagToType)
                {
                    if (expandedTag.SequenceEqual(expanded))
                    {
                        return derived.YamlTypeInfo;
                    }
                }
            }

            if (FromTagMappings)
            {
                if (_options.TagMappingTable?.Find(expandedTag) is Type mapped && mapped != BaseType)
                {
                    ThrowHelper.ThrowYamlException(SR.Format(SR.YamlTagMappedTypeNotAssignable, Encoding.UTF8.GetString(expandedTag.ToArray()), mapped, BaseType));
                }

                return null;
            }

            if (!IgnoreUnrecognizedTypeDiscriminators)
            {
                ThrowHelper.ThrowYamlException(SR.Format(SR.YamlTagUnrecognized, Encoding.UTF8.GetString(expandedTag.ToArray()), BaseType));
            }

            return null;
        }

        /// <summary>The tag a derived type writes under Tag and Both, or null.</summary>
        public string? WrittenTagOf(Type derivedType) =>
            _typeToDiscriminatorId.TryGetValue(derivedType, out DerivedYamlTypeInfo? derived) ? derived?.WrittenTag : null;
        public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; }
        public bool UsesTypeDiscriminators { get; }
        public bool IgnoreUnrecognizedTypeDiscriminators { get; }
        public byte[]? CustomTypeDiscriminatorPropertyNameUtf8 { get; }
        public YamlEncodedText? CustomTypeDiscriminatorPropertyNameYamlEncoded { get; }

        /// <summary>
        /// Resolves a classifier-returned <see cref="Type"/> to its <see cref="YamlTypeInfo"/> using the registered derived types.
        /// </summary>
        public bool TryResolveDerivedYamlTypeInfo(Type resolvedType, [NotNullWhen(true)] out YamlTypeInfo? yamlTypeInfo)
        {
            if (_typeToDiscriminatorId.TryGetValue(resolvedType, out DerivedYamlTypeInfo? result) && result is not null)
            {
                yamlTypeInfo = result.YamlTypeInfo;
                return true;
            }

            if (IgnoreUnrecognizedTypeDiscriminators)
            {
                yamlTypeInfo = null;
                return false;
            }

            ThrowHelper.ThrowNotSupportedException_RuntimeTypeNotSupported(BaseType, resolvedType);
            yamlTypeInfo = null;
            return false;
        }

        public bool TryGetDerivedYamlTypeInfo(Type runtimeType, [NotNullWhen(true)] out YamlTypeInfo? yamlTypeInfo, out object? typeDiscriminator)
        {
            Debug.Assert(BaseType.IsAssignableFrom(runtimeType));

            if (!_typeToDiscriminatorId.TryGetValue(runtimeType, out DerivedYamlTypeInfo? result))
            {
                switch (UnknownDerivedTypeHandling)
                {
                    case YamlUnknownDerivedTypeHandling.FallBackToNearestAncestor:
                        // Calculate (and cache the result) of the nearest ancestor for given runtime type.
                        // A `null` result denotes no matching ancestor type, we also cache that.
                        result = CalculateNearestAncestor(runtimeType);
                        _typeToDiscriminatorId[runtimeType] = result;
                        break;
                    case YamlUnknownDerivedTypeHandling.FallBackToBaseType:
                        // Recover the polymorphic contract (i.e. any type discriminators) for the base type, if it exists.
                        _typeToDiscriminatorId.TryGetValue(BaseType, out result);
                        _typeToDiscriminatorId[runtimeType] = result;
                        break;

                    case YamlUnknownDerivedTypeHandling.FailSerialization:
                    default:
                        if (runtimeType != BaseType)
                        {
                            ThrowHelper.ThrowNotSupportedException_RuntimeTypeNotSupported(BaseType, runtimeType);
                        }
                        break;
                }
            }

            if (result is null)
            {
                yamlTypeInfo = null;
                typeDiscriminator = null;
                return false;
            }
            else
            {
                yamlTypeInfo = result.YamlTypeInfo;
                typeDiscriminator = result.TypeDiscriminator;
                return true;
            }
        }

        public bool TryGetDerivedYamlTypeInfo(object typeDiscriminator, [NotNullWhen(true)] out YamlTypeInfo? yamlTypeInfo)
        {
            Debug.Assert(typeDiscriminator is int or string);
            Debug.Assert(UsesTypeDiscriminators);
            Debug.Assert(_discriminatorIdtoType is not null);

            if (_discriminatorIdtoType.TryGetValue(typeDiscriminator, out DerivedYamlTypeInfo? result))
            {
                Debug.Assert(typeDiscriminator.Equals(result.TypeDiscriminator));
                yamlTypeInfo = result.YamlTypeInfo;
                return true;
            }

            if (!IgnoreUnrecognizedTypeDiscriminators)
            {
                ThrowHelper.ThrowYamlException_UnrecognizedTypeDiscriminator(typeDiscriminator);
            }

            yamlTypeInfo = null;
            return false;
        }

        public static bool IsSupportedPolymorphicBaseType(Type? type) =>
            type != null &&
            (type.IsClass || type.IsInterface) &&
            !type.IsSealed &&
            !type.IsGenericTypeDefinition &&
            !type.IsPointer &&
            type != YamlTypeInfo.ObjectType;

        public static bool IsSupportedDerivedType(Type baseType, Type? derivedType) =>
            baseType.IsAssignableFrom(derivedType) && !derivedType.IsGenericTypeDefinition;

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
            Justification = "The call to GetInterfaces will cross-reference results with interface types " +
                            "already declared as derived types of the polymorphic base type.")]
        private DerivedYamlTypeInfo? CalculateNearestAncestor(Type type)
        {
            Debug.Assert(!type.IsAbstract);
            Debug.Assert(BaseType.IsAssignableFrom(type));
            Debug.Assert(UnknownDerivedTypeHandling == YamlUnknownDerivedTypeHandling.FallBackToNearestAncestor);

            if (type == BaseType)
            {
                return null;
            }

            DerivedYamlTypeInfo? result = null;

            // First, walk up the class hierarchy for any supported types.
            for (Type? candidate = type.BaseType; BaseType.IsAssignableFrom(candidate); candidate = candidate.BaseType)
            {
                Debug.Assert(candidate != null);

                if (_typeToDiscriminatorId.TryGetValue(candidate, out result))
                {
                    break;
                }
            }

            // Interface hierarchies admit the possibility of diamond ambiguities in type discriminators.
            // Examine all interface implementations and identify potential conflicts.
            if (BaseType.IsInterface)
            {
                foreach (Type interfaceTy in type.GetInterfaces())
                {
                    if (interfaceTy != BaseType && BaseType.IsAssignableFrom(interfaceTy) &&
                        _typeToDiscriminatorId.TryGetValue(interfaceTy, out DerivedYamlTypeInfo? interfaceResult) &&
                        interfaceResult is not null)
                    {
                        if (result is null)
                        {
                            result = interfaceResult;
                        }
                        else
                        {
                            ThrowHelper.ThrowNotSupportedException_RuntimeTypeDiamondAmbiguity(BaseType, type, result.YamlTypeInfo.Type, interfaceResult.YamlTypeInfo.Type);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Walks the type hierarchy above the current type for any types that use polymorphic configuration.
        /// </summary>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:UnrecognizedReflectionPattern",
            Justification = "The call to GetInterfaces will cross-reference results with interface types " +
                            "already declared as derived types of the polymorphic base type.")]
        internal static YamlTypeInfo? FindNearestPolymorphicBaseType(YamlTypeInfo typeInfo)
        {
            Debug.Assert(typeInfo.IsConfigured);

            if (typeInfo.PolymorphismOptions is not null)
            {
                // Type defines its own polymorphic configuration.
                return null;
            }

            YamlTypeInfo? matchingResult = null;

            // First, walk up the class hierarchy for any supported types.
            for (Type? candidate = typeInfo.Type.BaseType; candidate != null; candidate = candidate.BaseType)
            {
                YamlTypeInfo? candidateInfo = ResolveAncestorTypeInfo(candidate, typeInfo.Options);
                if (candidateInfo?.PolymorphismOptions is not null)
                {
                    // stop on the first ancestor that has a match
                    matchingResult = candidateInfo;
                    break;
                }
            }

            // Now, walk the interface hierarchy for any polymorphic interface declarations.
            foreach (Type interfaceType in typeInfo.Type.GetInterfaces())
            {
                YamlTypeInfo? candidateInfo = ResolveAncestorTypeInfo(interfaceType, typeInfo.Options);
                if (candidateInfo?.PolymorphismOptions is not null)
                {
                    if (matchingResult is not null)
                    {
                        // Resolve any conflicting matches.
                        if (matchingResult.Type.IsAssignableFrom(interfaceType))
                        {
                            // interface is more derived than previous match, replace it.
                            matchingResult = candidateInfo;
                        }
                        else if (interfaceType.IsAssignableFrom(matchingResult.Type))
                        {
                            // interface is less derived than previous match, keep the previous one.
                            continue;
                        }
                        else
                        {
                            // Diamond ambiguity, do not report any ancestors.
                            return null;
                        }
                    }
                    else
                    {
                        matchingResult = candidateInfo;
                    }
                }
            }

            return matchingResult;

            static YamlTypeInfo? ResolveAncestorTypeInfo(Type type, YamlSerializerOptions options)
            {
                try
                {
                    return options.GetTypeInfoInternal(type, ensureNotNull: null);
                }
                catch
                {
                    // The resolver produced an exception when resolving the ancestor type.
                    // Eat the exception and report no result instead.
                    return null;
                }
            }
        }

        /// <summary>
        /// YamlTypeInfo result holder for a derived type.
        /// </summary>
        private sealed class DerivedYamlTypeInfo
        {
            public DerivedYamlTypeInfo(object? typeDiscriminator, YamlTypeInfo derivedTypeInfo, string? writtenTag = null)
            {
                Debug.Assert(typeDiscriminator is null or int or string);

                TypeDiscriminator = typeDiscriminator;
                YamlTypeInfo = derivedTypeInfo;
                WrittenTag = writtenTag;
            }

            public object? TypeDiscriminator { get; }
            public string? WrittenTag { get; }
            public YamlTypeInfo YamlTypeInfo { get; }
        }
    }
}
