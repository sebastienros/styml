// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

using FoundProperty = System.ValueTuple<System.Text.Yaml.Serialization.Metadata.YamlPropertyInfo, System.Text.Yaml.YamlReaderState, long, byte[]?, string?>;
using FoundPropertyAsync = System.ValueTuple<System.Text.Yaml.Serialization.Metadata.YamlPropertyInfo, object?, string?>;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>YamlObjectConverter{T}</cref> that supports the deserialization
    /// of YAML objects using parameterized constructors.
    /// </summary>
    internal abstract partial class ObjectWithParameterizedConstructorConverter<T> : ObjectDefaultConverter<T> where T : notnull
    {
        internal sealed override bool ConstructorIsParameterized => true;

        internal sealed override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, [MaybeNullWhen(false)] out T value)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;

            if (!yamlTypeInfo.UsesParameterizedConstructor || state.Current.IsPopulating)
            {
                // Fall back to default object converter in following cases:
                // - if user configuration has invalidated the parameterized constructor
                // - we're continuing populating an object.
                return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
            }

            object obj;
            ArgumentState argumentState = state.Current.CtorArgumentState!;

            // M5d: a tagged node at a site only TagMappings makes polymorphic takes the slower path, which
            // dispatches on the tag without the metadata rules ($-members stay members there).
            if (!state.SupportContinuation && !state.Current.CanContainMetadata && !YamlSerializer.HasMappedTag(yamlTypeInfo, ref reader))
            {
                // Fast path that avoids maintaining state variables.

                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                }

                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    object populatedObject = state.Current.ReturnValue!;
                    PopulatePropertiesFastPath(populatedObject, yamlTypeInfo, options, ref reader, ref state);
                    value = (T)populatedObject;
                    return true;
                }

                ReadOnlySpan<byte> originalSpan = reader.OriginalSpan;
                ReadOnlySequence<byte> originalSequence = reader.OriginalSequence;

                ReadConstructorArguments(ref state, ref reader, options);

                // We've read all ctor parameters and properties,
                // validate that all required parameters were provided
                // before calling the constructor which may throw.
                state.Current.ValidateAllRequiredPropertiesAreRead(yamlTypeInfo);

                obj = (T)CreateObject(ref state.Current);

                yamlTypeInfo.OnDeserializing?.Invoke(obj);

                if (argumentState.FoundPropertyCount > 0)
                {
                    Utf8YamlReader tempReader;
                    YamlAnchorContext? liveAnchorContext = reader.YamlAnchorContext;

                    FoundProperty[]? properties = argumentState.FoundProperties;
                    Debug.Assert(properties is not null);

                    for (int i = 0; i < argumentState.FoundPropertyCount; i++)
                    {
                        YamlPropertyInfo yamlPropertyInfo = properties[i].Item1;
                        long resumptionByteIndex = properties[i].Item3;
                        byte[]? propertyNameArray = properties[i].Item4;
                        string? dataExtKey = properties[i].Item5;

                        tempReader = originalSequence.IsEmpty
                            ? new Utf8YamlReader(
                                originalSpan.Slice(checked((int)resumptionByteIndex)),
                                isFinalBlock: true,
                                state: properties[i].Item2)
                            : new Utf8YamlReader(
                                originalSequence.Slice(resumptionByteIndex),
                                isFinalBlock: true,
                                state: properties[i].Item2);

                        Debug.Assert(tempReader.TokenType == YamlTokenType.PropertyName);

                        // M5b: the saved state may predate the operation's first anchor; take the live table.
                        if (liveAnchorContext is not null)
                        {
                            tempReader.YamlAnchorContext = liveAnchorContext.ForSlice(resumptionByteIndex);
                        }

                        state.Current.YamlPropertyName = propertyNameArray;
                        state.Current.YamlPropertyInfo = yamlPropertyInfo;
                        state.Current.NumberHandling = yamlPropertyInfo.EffectiveNumberHandling;

                        bool useExtensionProperty = dataExtKey is not null;

                        if (useExtensionProperty)
                        {
                            Debug.Assert(yamlPropertyInfo == state.Current.YamlTypeInfo.ExtensionDataProperty);
                            state.Current.YamlPropertyNameAsString = dataExtKey;
                            YamlSerializer.CreateExtensionDataProperty(obj, yamlPropertyInfo, options);
                        }

                        ReadPropertyValue(obj, ref state, ref tempReader, yamlPropertyInfo, useExtensionProperty);
                    }

                    FoundProperty[] toReturn = argumentState.FoundProperties!;
                    argumentState.FoundProperties = null;
                    ArrayPool<FoundProperty>.Shared.Return(toReturn, clearArray: true);
                }
            }
            else
            {
                // Slower path that supports continuation and metadata reads.

                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    // M5d: a tag on the start token selects the derived type, before the shape is checked.
                    YamlSerializer.ReadTagDiscriminator(yamlTypeInfo, ref reader, ref state);

                    if (reader.TokenType != YamlTokenType.StartObject)
                    {
                        ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                    }

                    state.Current.ObjectState = StackFrameObjectState.StartToken;
                }

                // Read any metadata properties.
                if (state.Current.CanContainMetadata && state.Current.ObjectState < StackFrameObjectState.ReadMetadata)
                {
                    if (!YamlSerializer.TryReadMetadata(this, yamlTypeInfo, ref reader, ref state))
                    {
                        value = default;
                        return false;
                    }

                    if (state.Current.MetadataPropertyNames == MetadataPropertyName.Ref)
                    {
                        value = YamlSerializer.ResolveReferenceId<T>(ref state);
                        return true;
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                }

                // Dispatch to any polymorphic converters: should always be entered regardless of ObjectState progress
                if (((state.Current.MetadataPropertyNames & MetadataPropertyName.Type) != 0 || state.PolymorphicResolvedType is not null
                    || state.Current.PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntrySuspended) &&
                    state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted &&
                    ResolvePolymorphicConverter(yamlTypeInfo, ref state) is YamlConverter polymorphicConverter)
                {
                    Debug.Assert(!IsValueType);
                    bool success = polymorphicConverter.OnTryReadAsObject(ref reader, polymorphicConverter.Type!, options, ref state, out object? objectResult);
                    value = (T)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                // We need to populate before we started reading constructor arguments.
                // Metadata is disallowed with Populate option and therefore ordering here is irrelevant.
                // Since state.Current.IsPopulating is being checked early on in this method the continuation
                // will be handled there.
                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    object populatedObject = state.Current.ReturnValue!;

                    yamlTypeInfo.OnDeserializing?.Invoke(populatedObject);
                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                    state.Current.InitializePropertiesValidationState(yamlTypeInfo);
                    return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
                }

                // Handle metadata post polymorphic dispatch
                if (state.Current.ObjectState < StackFrameObjectState.ConstructorArguments)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        YamlSerializer.ValidateMetadataForObjectConverter(ref state);
                    }

                    if (state.Current.MetadataPropertyNames == MetadataPropertyName.Ref)
                    {
                        value = YamlSerializer.ResolveReferenceId<T>(ref state);
                        return true;
                    }

                    BeginRead(ref state, options);

                    state.Current.ObjectState = StackFrameObjectState.ConstructorArguments;
                }

                if (!ReadConstructorArgumentsWithContinuation(ref state, ref reader, options))
                {
                    value = default;
                    return false;
                }

                // We've read all ctor parameters and properties,
                // validate that all required parameters were provided
                // before calling the constructor which may throw.
                state.Current.ValidateAllRequiredPropertiesAreRead(yamlTypeInfo);

                obj = (T)CreateObject(ref state.Current);

                if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                {
                    Debug.Assert(state.ReferenceId is not null);
                    Debug.Assert(options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve);
                    state.ReferenceResolver.AddReference(state.ReferenceId, obj);
                    state.ReferenceId = null;
                }

                // M5b: members read after the constructor can refer to the object (with Preserve).
                YamlAliasReader.StoreAtCreation(ref reader, ref state, obj);

                yamlTypeInfo.OnDeserializing?.Invoke(obj);

                if (argumentState.FoundPropertyCount > 0)
                {
                    for (int i = 0; i < argumentState.FoundPropertyCount; i++)
                    {
                        YamlPropertyInfo yamlPropertyInfo = argumentState.FoundPropertiesAsync![i].Item1;
                        object? propValue = argumentState.FoundPropertiesAsync![i].Item2;
                        string? dataExtKey = argumentState.FoundPropertiesAsync![i].Item3;

                        if (dataExtKey is null)
                        {
                            Debug.Assert(yamlPropertyInfo.Set is not null);

                            if (propValue is not null || !yamlPropertyInfo.IgnoreNullTokensOnRead || default(T) is not null)
                            {
                                yamlPropertyInfo.Set(obj, propValue);
                            }
                        }
                        else
                        {
                            Debug.Assert(yamlPropertyInfo == state.Current.YamlTypeInfo.ExtensionDataProperty);

                            YamlSerializer.CreateExtensionDataProperty(obj, yamlPropertyInfo, options);
                            object extDictionary = yamlPropertyInfo.GetValueAsObject(obj)!;

                            // M5c: extension data the constructor seeded judges the document's keys.
                            HashSet<string>? documentKeys = YamlPropertyInfo.ExtensionDocumentKeys(extDictionary, ref state, options);
                            if (documentKeys is not null && !documentKeys.Add(dataExtKey))
                            {
                                if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                                {
                                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(dataExtKey);
                                }
                            }
                            else if (documentKeys is not null)
                            {
                                if (extDictionary is IDictionary<string, YamlElement> seededElements)
                                {
                                    seededElements[dataExtKey] = (YamlElement)propValue!;
                                }
                                else
                                {
                                    ((IDictionary<string, object>)extDictionary)[dataExtKey] = propValue!;
                                }
                            }
                            else if (extDictionary is IDictionary<string, YamlElement> dict)
                            {
                                if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
                                {
                                    dict.TryAdd(dataExtKey, (YamlElement)propValue!);
                                }
                                else if (options.AllowDuplicateProperties)
                                {
                                    dict[dataExtKey] = (YamlElement)propValue!;
                                }
                                else if (!dict.TryAdd(dataExtKey, (YamlElement)propValue!))
                                {
                                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(dataExtKey);
                                }
                            }
                            else
                            {
                                IDictionary<string, object> objDict = (IDictionary<string, object>)extDictionary;

                                if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
                                {
                                    objDict.TryAdd(dataExtKey, propValue!);
                                }
                                else if (options.AllowDuplicateProperties)
                                {
                                    objDict[dataExtKey] = propValue!;
                                }
                                else if (!objDict.TryAdd(dataExtKey, propValue!))
                                {
                                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(dataExtKey);
                                }
                            }
                        }
                    }

                    FoundPropertyAsync[] toReturn = argumentState.FoundPropertiesAsync!;
                    argumentState.FoundPropertiesAsync = null;
                    ArrayPool<FoundPropertyAsync>.Shared.Return(toReturn, clearArray: true);
                }
            }

            yamlTypeInfo.OnDeserialized?.Invoke(obj);

            // Unbox
            Debug.Assert(obj is not null);
            value = (T)obj;

            // Check if we are trying to update the UTF-8 property cache.
            if (state.Current.PropertyRefCacheBuilder is not null)
            {
                yamlTypeInfo.UpdateUtf8PropertyCache(ref state.Current);
            }

            return true;
        }

        protected abstract void InitializeConstructorArgumentCaches(ref ReadStack state, YamlSerializerOptions options);

        protected abstract bool ReadAndCacheConstructorArgument(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlParameterInfo yamlParameterInfo);

        protected abstract object CreateObject(ref ReadStackFrame frame);

        /// <summary>
        /// AcceptLenientScalars reads null into a non-nullable value type as its default, but not into a
        /// constructor parameter: SharpYaml builds the object and refuses the null, and YamlDotNet builds no record, so by
        /// the parity rule a parameter keeps refusing it, as without the option.
        /// </summary>
        private protected static void RefuseNullAsDefault(ref Utf8YamlReader reader, YamlParameterInfo yamlParameterInfo)
        {
            // The option first: a read with it off tests one field per argument.
            if (reader.AcceptsLenientScalars && (reader.TokenType == YamlTokenType.Null || reader.IsLenientNull)
                && yamlParameterInfo.EffectiveConverter.ReadsNullAsDefault)
            {
                ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(yamlParameterInfo.ParameterType);
            }
        }

        /// <summary>
        /// Performs a full first pass of the YAML input and deserializes the ctor args.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadConstructorArguments(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlSerializerOptions options)
        {
            BeginRead(ref state, options);

            while (true)
            {
                // Read the next property name or EndObject.
                reader.ReadWithVerify();

                YamlTokenType tokenType = reader.TokenType;

                if (tokenType == YamlTokenType.EndObject)
                {
                    return;
                }

                // Read method would have thrown if otherwise.
                Debug.Assert(tokenType == YamlTokenType.PropertyName);

                ReadOnlySpan<byte> unescapedPropertyName = YamlSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                if (isAlreadyReadMetadataProperty)
                {
                    Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                    reader.SkipWithVerify();
                    state.Current.EndProperty();
                    continue;
                }

                if (TryLookupConstructorParameter(
                    unescapedPropertyName,
                    ref state,
                    options,
                    out YamlPropertyInfo yamlPropertyInfo,
                    out YamlParameterInfo? yamlParameterInfo))
                {
                    // Set the property value.
                    reader.ReadWithVerify();

                    if (!yamlParameterInfo.ShouldDeserialize)
                    {
                        // The Utf8YamlReader.Skip() method will fail fast if it detects that we're reading
                        // from a partially read buffer, regardless of whether the next value is available.
                        // This can result in erroneous failures in cases where a custom converter is calling
                        // into a built-in converter (cf. https://github.com/dotnet/runtime/issues/74108).
                        // For this reason we need to call the TrySkip() method instead -- the serializer
                        // should guarantee sufficient read-ahead has been performed for the current object.
                        bool success = reader.TrySkip();
                        Debug.Assert(success, "Serializer should guarantee sufficient read-ahead has been done.");

                        state.Current.EndConstructorParameter();
                        continue;
                    }

                    Debug.Assert(yamlParameterInfo.MatchingProperty is not null);
                    ReadAndCacheConstructorArgument(ref state, ref reader, yamlParameterInfo);

                    state.Current.EndConstructorParameter();
                }
                else
                {
                    if (yamlPropertyInfo.CanDeserialize)
                    {
                        ArgumentState argumentState = state.Current.CtorArgumentState!;

                        if (argumentState.FoundProperties is null)
                        {
                            argumentState.FoundProperties =
                                ArrayPool<FoundProperty>.Shared.Rent(Math.Max(1, state.Current.YamlTypeInfo.PropertyCache.Length));
                        }
                        else if (argumentState.FoundPropertyCount == argumentState.FoundProperties.Length)
                        {
                            // Rare case where we can't fit all the JSON properties in the rented pool; we have to grow.
                            // This could happen if there are duplicate properties in the JSON.

                            var newCache = ArrayPool<FoundProperty>.Shared.Rent(argumentState.FoundProperties.Length * 2);

                            argumentState.FoundProperties.CopyTo(newCache, 0);

                            FoundProperty[] toReturn = argumentState.FoundProperties;
                            argumentState.FoundProperties = newCache!;

                            ArrayPool<FoundProperty>.Shared.Return(toReturn, clearArray: true);
                        }

                        argumentState.FoundProperties[argumentState.FoundPropertyCount++] = (
                            yamlPropertyInfo,
                            reader.CurrentState,
                            reader.BytesConsumed,
                            state.Current.YamlPropertyName,
                            state.Current.YamlPropertyNameAsString);
                    }

                    reader.SkipWithVerify();
                    state.Current.EndProperty();
                }
            }
        }

        private bool ReadConstructorArgumentsWithContinuation(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlSerializerOptions options)
        {
            // Process all properties.
            while (true)
            {
                // Determine the property.
                if (state.Current.PropertyState == StackFramePropertyState.None)
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadName;
                }

                YamlParameterInfo? yamlParameterInfo;
                YamlPropertyInfo? yamlPropertyInfo;

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    YamlTokenType tokenType = reader.TokenType;

                    if (tokenType == YamlTokenType.EndObject)
                    {
                        return true;
                    }

                    // Read method would have thrown if otherwise.
                    Debug.Assert(tokenType == YamlTokenType.PropertyName);

                    ReadOnlySpan<byte> unescapedPropertyName = YamlSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                    if (isAlreadyReadMetadataProperty)
                    {
                        Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                        reader.SkipWithVerify();
                        state.Current.EndProperty();
                        continue;
                    }

                    if (TryLookupConstructorParameter(
                        unescapedPropertyName,
                        ref state,
                        options,
                        out yamlPropertyInfo,
                        out yamlParameterInfo))
                    {
                        yamlPropertyInfo = null;
                    }

                    state.Current.PropertyState = StackFramePropertyState.Name;
                }
                else
                {
                    yamlParameterInfo = state.Current.CtorArgumentState!.YamlParameterInfo;
                    yamlPropertyInfo = state.Current.YamlPropertyInfo;
                }

                if (yamlParameterInfo is not null)
                {
                    Debug.Assert(yamlPropertyInfo is null);

                    if (!HandleConstructorArgumentWithContinuation(ref state, ref reader, yamlParameterInfo))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!HandlePropertyWithContinuation(ref state, ref reader, yamlPropertyInfo!))
                    {
                        return false;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandleConstructorArgumentWithContinuation(
            scoped ref ReadStack state,
            ref Utf8YamlReader reader,
            YamlParameterInfo yamlParameterInfo)
        {
            if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
            {
                if (!yamlParameterInfo.ShouldDeserialize)
                {
                    if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                    {
                        return false;
                    }

                    state.Current.EndConstructorParameter();
                    return true;
                }

                if (!reader.TryAdvanceWithOptionalReadAhead(yamlParameterInfo.EffectiveConverter.RequiresReadAhead))
                {
                    return false;
                }

                state.Current.PropertyState = StackFramePropertyState.ReadValue;
            }

            if (!ReadAndCacheConstructorArgument(ref state, ref reader, yamlParameterInfo))
            {
                return false;
            }

            state.Current.EndConstructorParameter();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandlePropertyWithContinuation(
            scoped ref ReadStack state,
            ref Utf8YamlReader reader,
            YamlPropertyInfo yamlPropertyInfo)
        {
            if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
            {
                if (!yamlPropertyInfo.CanDeserialize)
                {
                    if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                    {
                        return false;
                    }

                    state.Current.EndProperty();
                    return true;
                }

                if (!ReadAheadPropertyValue(ref state, ref reader, yamlPropertyInfo))
                {
                    return false;
                }

                state.Current.PropertyState = StackFramePropertyState.ReadValue;
            }

            object? propValue;

            if (state.Current.UseExtensionProperty)
            {
                if (!yamlPropertyInfo.ReadYamlExtensionDataValue(ref state, ref reader, out propValue))
                {
                    return false;
                }
            }
            else
            {
                if (!yamlPropertyInfo.ReadYamlAsObject(ref state, ref reader, out propValue))
                {
                    return false;
                }
            }

            Debug.Assert(yamlPropertyInfo.CanDeserialize);

            // Ensure that the cache has enough capacity to add this property.

            ArgumentState argumentState = state.Current.CtorArgumentState!;

            if (argumentState.FoundPropertiesAsync is null)
            {
                argumentState.FoundPropertiesAsync = ArrayPool<FoundPropertyAsync>.Shared.Rent(Math.Max(1, state.Current.YamlTypeInfo.PropertyCache.Length));
            }
            else if (argumentState.FoundPropertyCount == argumentState.FoundPropertiesAsync!.Length)
            {
                // Rare case where we can't fit all the JSON properties in the rented pool; we have to grow.
                // This could happen if there are duplicate properties in the JSON.
                var newCache = ArrayPool<FoundPropertyAsync>.Shared.Rent(argumentState.FoundPropertiesAsync!.Length * 2);

                argumentState.FoundPropertiesAsync!.CopyTo(newCache, 0);

                FoundPropertyAsync[] toReturn = argumentState.FoundPropertiesAsync!;
                argumentState.FoundPropertiesAsync = newCache!;

                ArrayPool<FoundPropertyAsync>.Shared.Return(toReturn, clearArray: true);
            }

            // Cache the property name and value.
            argumentState.FoundPropertiesAsync![argumentState.FoundPropertyCount++] = (
                yamlPropertyInfo,
                propValue,
                state.Current.YamlPropertyNameAsString);

            state.Current.EndProperty();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BeginRead(scoped ref ReadStack state, YamlSerializerOptions options)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;

            yamlTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();

            if (yamlTypeInfo.ParameterCount != yamlTypeInfo.ParameterCache.Length)
            {
                ThrowHelper.ThrowInvalidOperationException_ConstructorParameterIncompleteBinding(Type);
            }

            state.Current.InitializePropertiesValidationState(yamlTypeInfo);

            // Set current YamlPropertyInfo to null to avoid conflicts on push.
            state.Current.YamlPropertyInfo = null;

            Debug.Assert(state.Current.CtorArgumentState is not null);

            InitializeConstructorArgumentCaches(ref state, options);
        }

        /// <summary>
        /// Lookup the constructor parameter given its name in the reader.
        /// </summary>
        protected static bool TryLookupConstructorParameter(
            scoped ReadOnlySpan<byte> unescapedPropertyName,
            scoped ref ReadStack state,
            YamlSerializerOptions options,
            out YamlPropertyInfo yamlPropertyInfo,
            [NotNullWhen(true)] out YamlParameterInfo? yamlParameterInfo)
        {
            Debug.Assert(state.Current.YamlTypeInfo.Kind is YamlTypeInfoKind.Object);
            Debug.Assert(state.Current.CtorArgumentState is not null);

            yamlPropertyInfo = YamlSerializer.LookupProperty(
                obj: null,
                unescapedPropertyName,
                ref state,
                options,
                out bool useExtensionProperty,
                createExtensionProperty: false);

            // Mark the property as read from the payload if it is mapped to a non-extension member.
            if (!useExtensionProperty && yamlPropertyInfo != YamlPropertyInfo.s_missingProperty)
            {
                state.Current.MarkPropertyAsRead(yamlPropertyInfo);
            }

            yamlParameterInfo = yamlPropertyInfo.AssociatedParameter;
            if (yamlParameterInfo is not null)
            {
                state.Current.YamlPropertyInfo = null;
                state.Current.CtorArgumentState!.YamlParameterInfo = yamlParameterInfo;
                state.Current.NumberHandling = yamlParameterInfo.NumberHandling;
                return true;
            }
            else
            {
                state.Current.UseExtensionProperty = useExtensionProperty;
                return false;
            }
        }
    }
}
