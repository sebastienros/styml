// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        internal const string IdPropertyName = "$id";
        internal const string RefPropertyName = "$ref";
        internal const string TypePropertyName = "$type";
        internal const string ValuesPropertyName = "$values";

        private static readonly byte[] s_idPropertyName = "$id"u8.ToArray();
        private static readonly byte[] s_refPropertyName = "$ref"u8.ToArray();
        private static readonly byte[] s_typePropertyName = "$type"u8.ToArray();
        private static readonly byte[] s_valuesPropertyName = "$values"u8.ToArray();

        internal static bool TryReadMetadata(YamlConverter converter, YamlTypeInfo yamlTypeInfo, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            Debug.Assert(state.Current.ObjectState == StackFrameObjectState.StartToken);
            Debug.Assert(state.Current.CanContainMetadata);

            // Custom classifier branch: runs BEFORE normal metadata scanning.
            // When a TypeClassifier is configured, it replaces discriminator-based type resolution.
            PolymorphicTypeResolver? polymorphicResolver = yamlTypeInfo.PolymorphicTypeResolver;
            if (polymorphicResolver is not null && yamlTypeInfo.TypeClassifier is { } typeClassifier)
            {
                // Ensure the entire object is buffered (for streaming scenarios).
                if (!reader.IsFinalBlock)
                {
                    Utf8YamlReader bufferCheck = reader;
                    if (!bufferCheck.TrySkipPartial())
                    {
                        return false;
                    }
                }

                // If reference handling is configured and the payload is a $ref-only object,
                // defer to the normal metadata loop so the reference resolver can return the
                // previously-deserialized instance instead of asking the classifier to identify
                // a type from a metadata-only payload that does not describe one.
                bool isRefOnlyPayload = false;
                if (state.ReferenceResolver is not null)
                {
                    Utf8YamlReader peek = reader;
                    if (peek.Read() &&
                        peek.TokenType == YamlTokenType.PropertyName &&
                        MetadataCandidate(ref peek).SequenceEqual(s_refPropertyName))
                    {
                        isRefOnlyPayload = true;
                    }
                }

                if (!isRefOnlyPayload)
                {
                    // Classify using a struct copy; the original reader stays at the object start.
                    Utf8YamlReader classifierCopy = reader;
                    Type? resolvedType = typeClassifier(ref classifierCopy);

                    if (resolvedType is null)
                    {
                        ThrowHelper.ThrowYamlException(SR.PolymorphicTypeClassifierReturnedNull);
                    }

                    state.PolymorphicResolvedType = resolvedType;
                }
            }

            Utf8YamlReader checkpoint;
            bool allowOutOfOrderMetadata = yamlTypeInfo.Options.AllowOutOfOrderMetadataProperties;
            bool isReadingAheadOfNonMetadataProperties = false;

            if (allowOutOfOrderMetadata && !reader.IsFinalBlock)
            {
                // When reading ahead for metadata properties, we need to make sure
                // that the entire object has been buffered so that we can roll back
                // the reader state when the read-ahead has been completed.
                checkpoint = reader;
                if (!checkpoint.TrySkipPartial())
                {
                    return false;
                }
            }
            else
            {
                // Checkpointing not used in this mode.
                checkpoint = default;
            }

            while (true)
            {
                if (state.Current.PropertyState == StackFramePropertyState.None)
                {
                    // Read the property name.
                    if (!reader.Read())
                    {
                        Debug.Assert(!allowOutOfOrderMetadata, "Object must have already been buffered in this mode.");
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadName;
                }

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        // Read the entire object while parsing for metadata.
                        goto Done;
                    }

                    // We just read a property. The only valid next tokens are EndObject and PropertyName.
                    Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Ref) != 0)
                    {
                        // No properties whatsoever should follow a $ref property.
                        ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties(MetadataCandidate(ref reader), ref state);
                    }

                    ReadOnlySpan<byte> propertyName = MetadataCandidate(ref reader);
                    switch (state.Current.LatestMetadataPropertyName = GetMetadataPropertyName(propertyName, yamlTypeInfo.PolymorphicTypeResolver))
                    {
                        case MetadataPropertyName.Id:
                            state.Current.YamlPropertyName = s_idPropertyName;

                            if (state.ReferenceResolver is null)
                            {
                                // Found an $id property in a type that doesn't support reference preservation
                                ThrowHelper.ThrowYamlException_MetadataUnexpectedProperty(propertyName, ref state);
                            }
                            if ((state.Current.MetadataPropertyNames & (MetadataPropertyName.Id | MetadataPropertyName.Ref)) != 0)
                            {
                                // No $id or $ref properties should precede $id properties.
                                ThrowHelper.ThrowYamlException_MetadataIdCannotBeCombinedWithRef(propertyName, ref state);
                            }
                            if (!converter.CanHaveMetadata)
                            {
                                // Should not be permitted unless the converter is capable of handling metadata.
                                ThrowHelper.ThrowYamlException_MetadataCannotParsePreservedObjectIntoImmutable(converter.Type!);
                            }

                            break;

                        case MetadataPropertyName.Ref:
                            state.Current.YamlPropertyName = s_refPropertyName;

                            if (state.ReferenceResolver is null)
                            {
                                // Found a $ref property in a type that doesn't support reference preservation
                                ThrowHelper.ThrowYamlException_MetadataUnexpectedProperty(propertyName, ref state);
                            }
                            if (converter.IsValueType)
                            {
                                // Should not be permitted if the converter is a struct.
                                ThrowHelper.ThrowYamlException_MetadataInvalidReferenceToValueType(converter.Type!);
                            }
                            if (state.Current.MetadataPropertyNames != 0 || isReadingAheadOfNonMetadataProperties)
                            {
                                // No properties should precede a $ref property.
                                ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties(MetadataCandidate(ref reader), ref state);
                            }

                            break;

                        case MetadataPropertyName.Type:
                            state.Current.YamlPropertyName = yamlTypeInfo.PolymorphicTypeResolver?.CustomTypeDiscriminatorPropertyNameUtf8 ?? s_typePropertyName;

                            if (yamlTypeInfo.PolymorphicTypeResolver is null)
                            {
                                // Found a $type property in a type that doesn't support polymorphism
                                ThrowHelper.ThrowYamlException_MetadataUnexpectedProperty(propertyName, ref state);
                            }
                            if (state.PolymorphicTypeDiscriminator is not null)
                            {
                                // Found a duplicate $type property.
                                ThrowHelper.ThrowYamlException_DuplicateMetadataProperty(state.Current.YamlPropertyName);
                            }

                            break;

                        case MetadataPropertyName.Values:
                            state.Current.YamlPropertyName = s_valuesPropertyName;

                            if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                            {
                                // Found a duplicate $values property.
                                ThrowHelper.ThrowYamlException_DuplicateMetadataProperty(s_valuesPropertyName);
                            }

                            if (isReadingAheadOfNonMetadataProperties)
                            {
                                // Cannot combine a $values property with other non-metadata properties.
                                ThrowHelper.ThrowYamlException_MetadataInvalidPropertyInArrayMetadata(ref state, yamlTypeInfo.Type, reader);
                            }

                            break;

                        default:
                            Debug.Assert(state.Current.LatestMetadataPropertyName == MetadataPropertyName.None);

                            // Encountered a non-metadata property
                            if (allowOutOfOrderMetadata)
                            {
                                if (!isReadingAheadOfNonMetadataProperties)
                                {
                                    // This is the first non-metadata property, checkpoint the current reader state.
                                    isReadingAheadOfNonMetadataProperties = true;
                                    checkpoint = reader;
                                }

                                if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                                {
                                    // Cannot combine a $values property with other non-metadata properties.
                                    ThrowHelper.ThrowYamlException_MetadataInvalidPropertyInArrayMetadata(ref state, yamlTypeInfo.Type, reader);
                                }

                                if (IsMetadataPropertyName(propertyName, resolver: null))
                                {
                                    // properties starting with '$' are reserved for metadata
                                    ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                                }

                                break;
                            }
                            else
                            {
                                // Exit immediately if no read-ahead is enabled.
                                goto Done;
                            }
                    }

                    state.Current.PropertyState = StackFramePropertyState.Name;
                }

                if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                {
                    // Read the property value.
                    if (!reader.Read())
                    {
                        Debug.Assert(!allowOutOfOrderMetadata, "Object must have already been buffered in this mode.");
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadValue;
                }

                Debug.Assert(state.Current.PropertyState == StackFramePropertyState.ReadValue);

                switch (state.Current.LatestMetadataPropertyName)
                {
                    case MetadataPropertyName.Id:
                        if (!reader.TokenIsStringLike)
                        {
                            ThrowHelper.ThrowYamlException_MetadataValueWasNotString(reader.TokenType);
                        }

                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        state.ReferenceId = reader.GetString();
                        break;

                    case MetadataPropertyName.Ref:
                        if (!reader.TokenIsStringLike)
                        {
                            ThrowHelper.ThrowYamlException_MetadataValueWasNotString(reader.TokenType);
                        }

                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        state.ReferenceId = reader.GetString();
                        break;

                    case MetadataPropertyName.Type:
                        Debug.Assert(state.PolymorphicTypeDiscriminator is null);

                        switch (reader.TokenType)
                        {
                            case YamlTokenType.String:
                                state.PolymorphicTypeDiscriminator = reader.GetString();
                                break;
                            case YamlTokenType.Number:
                                state.PolymorphicTypeDiscriminator = reader.GetInt32();
                                break;
                            default:
                                ThrowHelper.ThrowYamlException_MetadataValueWasNotString(reader.TokenType);
                                break;
                        }

                        break;

                    case MetadataPropertyName.Values:

                        if (reader.TokenType != YamlTokenType.StartArray)
                        {
                            ThrowHelper.ThrowYamlException_MetadataValuesInvalidToken(reader.TokenType);
                        }

                        if (allowOutOfOrderMetadata)
                        {
                            // The $values property contains the collection payload,
                            // checkpoint the current reader and continue the read-ahead.
                            Debug.Assert(!isReadingAheadOfNonMetadataProperties, "must have already verified if non-metadata properties precede $values");
                            isReadingAheadOfNonMetadataProperties = true;
                            checkpoint = reader;
                            reader.SkipWithVerify();
                        }
                        else
                        {
                            state.Current.MetadataPropertyNames |= MetadataPropertyName.Values;
                            state.Current.PropertyState = StackFramePropertyState.None;
                            goto Done; // "$values" property contains the nested payload, exit the metadata reader now.
                        }

                        break;

                    default:
                        Debug.Assert(state.Current.LatestMetadataPropertyName == MetadataPropertyName.None);
                        Debug.Assert(allowOutOfOrderMetadata, "should only be reached if reading ahead is required.");
                        reader.SkipWithVerify();

                        break;
                }

                state.Current.MetadataPropertyNames |= state.Current.LatestMetadataPropertyName;
                state.Current.PropertyState = StackFramePropertyState.None;
                state.Current.YamlPropertyName = null;
            }

        Done:
            if (state.Current.MetadataPropertyNames is MetadataPropertyName.Values)
            {
                // Cannot have a $values property unless there are other metadata properties.
                ThrowHelper.ThrowYamlException_MetadataStandaloneValuesProperty(ref state, s_valuesPropertyName);
            }

            if (isReadingAheadOfNonMetadataProperties)
            {
                // Roll back the reader state to the first non-metadata property.
                reader = checkpoint;
            }

            return true;
        }

        /// <summary>
        /// M5d: under <c>Tag</c> and <c>Both</c> (or a resolver
        /// <c>TagMappings</c> built), the application tag on the start token selects the derived type, so no read-ahead is
        /// needed for it. Free when the site reads no tag or the node has none.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReadTagDiscriminator(YamlTypeInfo yamlTypeInfo, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            if (yamlTypeInfo.PolymorphicTypeResolver is { ReadsTags: true } resolver && !reader.Tag.IsEmpty && yamlTypeInfo.TypeClassifier is null)
            {
                ReadTagDiscriminatorCore(resolver, ref reader, ref state);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadTagDiscriminatorCore(PolymorphicTypeResolver resolver, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            Span<byte> buffer = stackalloc byte[128];
            ReadOnlySpan<byte> tag = YamlTags.ExpandedApplicationTag(in reader, buffer, out _);
            if (tag.IsEmpty || resolver.FindByTag(tag) is not YamlTypeInfo derived)
            {
                return;
            }

            CheckTaggedShape(derived, in reader, tag);
            state.PolymorphicResolvedType = derived.Type;
            state.PolymorphicResolvedByTag = true;
        }

        /// <summary>A tagged node of the wrong shape names the tag; a tagged null is not a null.</summary>
        /// <summary>
        /// A tag to resolve at a site whose resolver <c>TagMappings</c> built. Such a site does not read
        /// metadata, so its converter's fast path would not see the tag.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasMappedTag(YamlTypeInfo yamlTypeInfo, ref Utf8YamlReader reader) =>
            yamlTypeInfo.PolymorphicTypeResolver is { FromTagMappings: true } && !reader.Tag.IsEmpty;

        internal static void CheckTaggedShape(YamlTypeInfo derived, scoped in Utf8YamlReader reader, scoped ReadOnlySpan<byte> tag)
        {
            string? found = reader.TokenType switch
            {
                YamlTokenType.StartObject => derived.Kind == YamlTypeInfoKind.Enumerable && !StartsWithMetadata(reader) ? "a mapping" : null,
                YamlTokenType.StartArray => derived.Kind is YamlTypeInfoKind.Object or YamlTypeInfoKind.Dictionary ? "a sequence" : null,
                YamlTokenType.Null => "a null",
                _ => "a scalar",
            };

            if (found is not null)
            {
                ThrowHelper.ThrowYamlException(SR.Format(SR.YamlTagWrongShape, Encoding.UTF8.GetString(tag.ToArray()), derived.Type, found));
            }

            // A collection's $type/$values wrapper; when the next token is not in the buffer yet, the metadata path judges.
            static bool StartsWithMetadata(Utf8YamlReader copy)
            {
                try
                {
                    return !copy.Read() || copy.TokenType != YamlTokenType.PropertyName || IsMetadataPropertyName(MetadataCandidate(ref copy), resolver: null);
                }
                catch (YamlException)
                {
                    return true;
                }
            }
        }

        /// <summary>A tagged null at a site that reads tags: an error when the tag selects a type.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckTaggedNull(YamlTypeInfo yamlTypeInfo, ref Utf8YamlReader reader)
        {
            if (yamlTypeInfo.PolymorphicTypeResolver is { ReadsTags: true } resolver && yamlTypeInfo.TypeClassifier is null)
            {
                Span<byte> buffer = stackalloc byte[128];
                ReadOnlySpan<byte> tag = YamlTags.ExpandedApplicationTag(in reader, buffer, out _);
                if (!tag.IsEmpty && resolver.FindByTag(tag) is YamlTypeInfo derived)
                {
                    CheckTaggedShape(derived, in reader, tag);
                }
            }
        }

        /// <summary>
        /// The name the metadata reader looks at: a collection key (M5e) is never metadata, so it gives an empty name,
        /// which ends the metadata part as any other member name does.
        /// </summary>
        private static ReadOnlySpan<byte> MetadataCandidate(scoped ref Utf8YamlReader reader) =>
            reader.KeyKind == YamlKeyKind.Complex ? default : reader.GetUnescapedSpan();

        internal static bool IsMetadataPropertyName(ReadOnlySpan<byte> propertyName, PolymorphicTypeResolver? resolver)
        {
            return
                (propertyName.Length > 0 && propertyName[0] == '$') ||
                (resolver?.CustomTypeDiscriminatorPropertyNameUtf8?.AsSpan().SequenceEqual(propertyName) == true);
        }

        internal static MetadataPropertyName GetMetadataPropertyName(ReadOnlySpan<byte> propertyName, PolymorphicTypeResolver? resolver)
        {
            if (propertyName.Length > 0 && propertyName[0] == '$')
            {
                switch (propertyName.Length)
                {
                    case 3 when propertyName.SequenceEqual("$id"u8):
                        return MetadataPropertyName.Id;

                    case 4 when propertyName.SequenceEqual("$ref"u8):
                        return MetadataPropertyName.Ref;

                    // A resolver TagMappings built has no discriminators: $type stays a member name there.
                    case 5 when resolver is not { FromTagMappings: true } && resolver?.CustomTypeDiscriminatorPropertyNameUtf8 is null && propertyName.SequenceEqual("$type"u8):
                        return MetadataPropertyName.Type;

                    case 7 when propertyName.SequenceEqual("$values"u8):
                        return MetadataPropertyName.Values;
                }
            }

            if (resolver?.CustomTypeDiscriminatorPropertyNameUtf8 is byte[] customTypeDiscriminator &&
                propertyName.SequenceEqual(customTypeDiscriminator))
            {
                return MetadataPropertyName.Type;
            }

            return MetadataPropertyName.None;
        }

        internal static bool TryHandleReferenceFromYamlElement(
            ref Utf8YamlReader reader,
            scoped ref ReadStack state,
            YamlElement element,
            [NotNullWhen(true)] out object? referenceValue)
        {
            bool refMetadataFound = false;
            referenceValue = default;

            if (element.ValueKind == YamlValueKind.Object)
            {
                int propertyCount = 0;
                foreach (YamlProperty property in element.EnumerateObject())
                {
                    propertyCount++;
                    if (refMetadataFound)
                    {
                        // There are more properties in an object with $ref.
                        ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties();
                    }
                    else if (property.EscapedNameEquals(s_idPropertyName))
                    {
                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        if (property.Value.ValueKind != YamlValueKind.String)
                        {
                            ThrowHelper.ThrowYamlException_MetadataValueWasNotString(property.Value.ValueKind);
                        }

                        object boxedElement = element;
                        state.ReferenceResolver.AddReference(property.Value.GetString()!, boxedElement);
                        referenceValue = boxedElement;
                        return true;
                    }
                    else if (property.EscapedNameEquals(s_refPropertyName))
                    {
                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        if (propertyCount > 1)
                        {
                            // $ref was found but there were other properties before.
                            ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties();
                        }

                        if (property.Value.ValueKind != YamlValueKind.String)
                        {
                            ThrowHelper.ThrowYamlException_MetadataValueWasNotString(property.Value.ValueKind);
                        }

                        referenceValue = state.ReferenceResolver.ResolveReference(property.Value.GetString()!);
                        refMetadataFound = true;
                    }
                }
            }

            return refMetadataFound;
        }

        internal static bool TryHandleReferenceFromYamlNode(
            ref Utf8YamlReader reader,
            scoped ref ReadStack state,
            YamlNode? yamlNode,
            [NotNullWhen(true)] out object? referenceValue)
        {
            bool refMetadataFound = false;
            referenceValue = default;

            if (yamlNode is YamlObject yamlObject)
            {
                int propertyCount = 0;
                foreach (KeyValuePair<string, YamlNode?> property in yamlObject)
                {
                    propertyCount++;
                    if (refMetadataFound)
                    {
                        // There are more properties in an object with $ref.
                        ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties();
                    }
                    else if (property.Key == "$id")
                    {
                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        string referenceId = ReadAsStringMetadataValue(property.Value);
                        state.ReferenceResolver.AddReference(referenceId, yamlNode);
                        referenceValue = yamlNode;
                        return true;
                    }
                    else if (property.Key == "$ref")
                    {
                        if (state.ReferenceId is not null)
                        {
                            ThrowHelper.ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(s_refPropertyName, ref reader, ref state);
                        }

                        if (propertyCount > 1)
                        {
                            // $ref was found but there were other properties before.
                            ThrowHelper.ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties();
                        }

                        string referenceId = ReadAsStringMetadataValue(property.Value);
                        referenceValue = state.ReferenceResolver.ResolveReference(referenceId);
                        refMetadataFound = true;
                    }

                    static string ReadAsStringMetadataValue(YamlNode? yamlNode)
                    {
                        if (yamlNode is YamlValue yamlValue &&
                            yamlValue.TryGetValue(out string? value) &&
                            value is not null)
                        {
                            return value;
                        }

                        YamlValueKind metadataValueKind = yamlNode?.GetValueKind() ?? YamlValueKind.Null;
                        Debug.Assert(metadataValueKind != YamlValueKind.Undefined);
                        ThrowHelper.ThrowYamlException_MetadataValueWasNotString(metadataValueKind);
                        return null!;
                    }
                }
            }

            return refMetadataFound;
        }

        internal static void ValidateMetadataForObjectConverter(ref ReadStack state)
        {
            if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
            {
                // Object converters do not support $values metadata.
                ThrowHelper.ThrowYamlException_MetadataUnexpectedProperty(s_valuesPropertyName, ref state);
            }
        }

        internal static void ValidateMetadataForArrayConverter(YamlConverter converter, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartArray:
                    Debug.Assert(state.Current.MetadataPropertyNames is MetadataPropertyName.None || state.Current.MetadataPropertyNames.HasFlag(MetadataPropertyName.Values));
                    break;

                case YamlTokenType.EndObject:
                    if (state.Current.MetadataPropertyNames != MetadataPropertyName.Ref)
                    {
                        // Read the entire JSON object while parsing for metadata: for collection converters this is only legal for $ref nodes.
                        ThrowHelper.ThrowYamlException_MetadataPreservedArrayValuesNotFound(ref state, converter.Type!);
                    }
                    break;

                default:
                    Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
                    // Do not tolerate non-metadata properties in collection converters.
                    ThrowHelper.ThrowYamlException_MetadataInvalidPropertyInArrayMetadata(ref state, converter.Type!, reader);
                    break;
            }
        }

        internal static T ResolveReferenceId<T>(ref ReadStack state)
        {
            Debug.Assert(!typeof(T).IsValueType);
            Debug.Assert(state.ReferenceId is not null);

            string referenceId = state.ReferenceId;
            object value = state.ReferenceResolver.ResolveReference(referenceId);
            state.ReferenceId = null;

            try
            {
                return (T)value;
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowInvalidOperationException_MetadataReferenceOfTypeCannotBeAssignedToType(
                    referenceId, value.GetType(), typeof(T));
                return default!;
            }
        }
    }
}
