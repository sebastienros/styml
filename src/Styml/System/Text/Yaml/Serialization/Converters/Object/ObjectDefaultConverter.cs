// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Default base class implementation of <cref>YamlObjectConverter{T}</cref>.
    /// </summary>
    /// <remarks>
    /// Aspects of the property reading and deserialization logic in this converter are
    /// duplicated in <see cref="FSharpUnionConverter{T}"/>. If any behavior is changed here
    /// (e.g. unmapped member handling, required properties, metadata processing),
    /// the union converter should also be updated for parity.
    /// </remarks>
    internal class ObjectDefaultConverter<T> : YamlObjectConverter<T> where T : notnull
    {
        internal override bool CanHaveMetadata => true;
        internal override bool SupportsCreateObjectDelegate => true;

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, [MaybeNullWhen(false)] out T value)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;

            object obj;

            // M5d: a tagged node at a site only TagMappings makes polymorphic takes the slower path, which
            // dispatches on the tag without the metadata rules ($-members stay members there).
            if (!state.SupportContinuation && !state.Current.CanContainMetadata && !YamlSerializer.HasMappedTag(yamlTypeInfo, ref reader))
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                }

                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    obj = state.Current.ReturnValue!;
                }
                else
                {
                    if (yamlTypeInfo.CreateObject is null)
                    {
                        ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(yamlTypeInfo, ref reader, ref state);
                    }

                    obj = yamlTypeInfo.CreateObject();
                }

                PopulatePropertiesFastPath(obj, yamlTypeInfo, options, ref reader, ref state);
                Debug.Assert(obj is not null);
                value = (T)obj;
                return true;
            }
            else
            {
                // Slower path that supports continuation and reading metadata.

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

                // Handle the metadata properties.
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

                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
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

                    if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                    {
                        obj = state.Current.ReturnValue!;
                    }
                    else
                    {
                        if (yamlTypeInfo.CreateObject is null)
                        {
                            ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(yamlTypeInfo, ref reader, ref state);
                        }

                        obj = yamlTypeInfo.CreateObject();
                    }

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId is not null);
                        Debug.Assert(options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve);
                        state.ReferenceResolver.AddReference(state.ReferenceId, obj);
                        state.ReferenceId = null;
                    }

                    // M5b: an alias inside the anchored object can refer to it (with Preserve).
                    YamlAliasReader.StoreAtCreation(ref reader, ref state, obj);
                    yamlTypeInfo.OnDeserializing?.Invoke(obj);

                    state.Current.ReturnValue = obj;
                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                    state.Current.InitializePropertiesValidationState(yamlTypeInfo);
                }
                else
                {
                    obj = state.Current.ReturnValue!;
                    Debug.Assert(obj is not null);
                }

                // Process all properties.
                while (true)
                {
                    // Determine the property.
                    if (state.Current.PropertyState == StackFramePropertyState.None)
                    {
                        if (!reader.Read())
                        {
                            state.Current.ReturnValue = obj;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadName;
                    }

                    YamlPropertyInfo yamlPropertyInfo;

                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        YamlTokenType tokenType = reader.TokenType;
                        if (tokenType == YamlTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(tokenType == YamlTokenType.PropertyName);

                        yamlTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();
                        ReadOnlySpan<byte> unescapedPropertyName = YamlSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                        if (isAlreadyReadMetadataProperty)
                        {
                            Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                            reader.SkipWithVerify();
                            state.Current.EndProperty();
                            continue;
                        }

                        yamlPropertyInfo = YamlSerializer.LookupProperty(
                            obj,
                            unescapedPropertyName,
                            ref state,
                            options,
                            out bool useExtensionProperty);

                        state.Current.UseExtensionProperty = useExtensionProperty;
                        state.Current.PropertyState = StackFramePropertyState.Name;
                    }
                    else
                    {
                        Debug.Assert(state.Current.YamlPropertyInfo is not null);
                        yamlPropertyInfo = state.Current.YamlPropertyInfo!;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                    {
                        if (!yamlPropertyInfo.CanDeserializeOrPopulate)
                        {
                            if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                            {
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }

                            state.Current.EndProperty();
                            continue;
                        }

                        if (!ReadAheadPropertyValue(ref state, ref reader, yamlPropertyInfo))
                        {
                            state.Current.ReturnValue = obj;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadValue;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                    {
                        // Obtain the CLR value from the JSON and set the member.
                        if (!state.Current.UseExtensionProperty)
                        {
                            if (!yamlPropertyInfo.ReadYamlAndSetMember(obj, ref state, ref reader))
                            {
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }
                        }
                        else
                        {
                            if (!yamlPropertyInfo.ReadYamlAndAddExtensionProperty(obj, ref state, ref reader))
                            {
                                // No need to set 'value' here since YamlElement must be read in full.
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }
                        }

                        state.Current.EndProperty();
                    }
                }
            }

            yamlTypeInfo.OnDeserialized?.Invoke(obj);
            state.Current.ValidateAllRequiredPropertiesAreRead(yamlTypeInfo);

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

        // This method is using aggressive inlining to avoid extra stack frame for deep object graphs.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void PopulatePropertiesFastPath(object obj, YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            yamlTypeInfo.OnDeserializing?.Invoke(obj);
            state.Current.InitializePropertiesValidationState(yamlTypeInfo);

            // Process all properties.
            while (true)
            {
                // Read the property name or EndObject.
                reader.ReadWithVerify();

                YamlTokenType tokenType = reader.TokenType;

                if (tokenType == YamlTokenType.EndObject)
                {
                    break;
                }

                // Read method would have thrown if otherwise.
                Debug.Assert(tokenType == YamlTokenType.PropertyName);

                ReadOnlySpan<byte> unescapedPropertyName = YamlSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                Debug.Assert(!isAlreadyReadMetadataProperty, "Only possible for types that can read metadata, which do not call into the fast-path method.");

                yamlTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();
                YamlPropertyInfo yamlPropertyInfo = YamlSerializer.LookupProperty(
                    obj,
                    unescapedPropertyName,
                    ref state,
                    options,
                    out bool useExtensionProperty);

                ReadPropertyValue(obj, ref state, ref reader, yamlPropertyInfo, useExtensionProperty);
            }

            yamlTypeInfo.OnDeserialized?.Invoke(obj);
            state.Current.ValidateAllRequiredPropertiesAreRead(yamlTypeInfo);

            // Check if we are trying to update the UTF-8 property cache.
            if (state.Current.PropertyRefCacheBuilder is not null)
            {
                yamlTypeInfo.UpdateUtf8PropertyCache(ref state.Current);
            }
        }

        internal sealed override bool OnTryWrite(
            Utf8YamlWriter writer,
            T value,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;
            yamlTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();

            object obj = value; // box once

            if (!state.SupportContinuation)
            {
                yamlTypeInfo.OnSerializing?.Invoke(obj);

                writer.WriteStartObject();

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    YamlSerializer.WriteMetadataForObject(this, ref state, writer);
                }

                foreach (YamlPropertyInfo yamlPropertyInfo in yamlTypeInfo.PropertyCache)
                {
                    if (yamlPropertyInfo.CanSerialize)
                    {
                        // Remember the current property for YamlPath support if an exception is thrown.
                        state.Current.YamlPropertyInfo = yamlPropertyInfo;
                        state.Current.NumberHandling = yamlPropertyInfo.EffectiveNumberHandling;

                        bool success = yamlPropertyInfo.GetMemberAndWriteYaml(obj, ref state, writer);
                        // Converters only return 'false' when out of data which is not possible in fast path.
                        Debug.Assert(success);

                        state.Current.EndProperty();
                    }
                }

                // Write extension data after the normal properties.
                YamlPropertyInfo? extensionDataProperty = yamlTypeInfo.ExtensionDataProperty;
                if (extensionDataProperty?.CanSerialize == true)
                {
                    // Remember the current property for YamlPath support if an exception is thrown.
                    state.Current.YamlPropertyInfo = extensionDataProperty;
                    state.Current.NumberHandling = extensionDataProperty.EffectiveNumberHandling;

                    bool success = extensionDataProperty.GetMemberAndWriteYamlExtensionData(obj, ref state, writer);
                    Debug.Assert(success);

                    state.Current.EndProperty();
                }

                writer.WriteEndObject();
            }
            else
            {
                if (!state.Current.ProcessedStartToken)
                {
                    writer.WriteStartObject();

                    if (state.CurrentContainsMetadata && CanHaveMetadata)
                    {
                        YamlSerializer.WriteMetadataForObject(this, ref state, writer);
                    }

                    yamlTypeInfo.OnSerializing?.Invoke(obj);

                    state.Current.ProcessedStartToken = true;
                }

                ReadOnlySpan<YamlPropertyInfo> propertyCache = yamlTypeInfo.PropertyCache;
                while (state.Current.EnumeratorIndex < propertyCache.Length)
                {
                    YamlPropertyInfo yamlPropertyInfo = propertyCache[state.Current.EnumeratorIndex];
                    if (yamlPropertyInfo.CanSerialize)
                    {
                        state.Current.YamlPropertyInfo = yamlPropertyInfo;
                        state.Current.NumberHandling = yamlPropertyInfo.EffectiveNumberHandling;

                        if (!yamlPropertyInfo.GetMemberAndWriteYaml(obj!, ref state, writer))
                        {
                            Debug.Assert(yamlPropertyInfo.EffectiveConverter.ConverterStrategy != ConverterStrategy.Value);
                            return false;
                        }

                        state.Current.EndProperty();
                        state.Current.EnumeratorIndex++;

                        if (ShouldFlush(ref state, writer))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        state.Current.EnumeratorIndex++;
                    }
                }

                // Write extension data after the normal properties.
                if (state.Current.EnumeratorIndex == propertyCache.Length)
                {
                    YamlPropertyInfo? extensionDataProperty = yamlTypeInfo.ExtensionDataProperty;
                    if (extensionDataProperty?.CanSerialize == true)
                    {
                        // Remember the current property for YamlPath support if an exception is thrown.
                        state.Current.YamlPropertyInfo = extensionDataProperty;
                        state.Current.NumberHandling = extensionDataProperty.EffectiveNumberHandling;

                        if (!extensionDataProperty.GetMemberAndWriteYamlExtensionData(obj, ref state, writer))
                        {
                            return false;
                        }

                        state.Current.EndProperty();
                        state.Current.EnumeratorIndex++;

                        if (ShouldFlush(ref state, writer))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        state.Current.EnumeratorIndex++;
                    }
                }

                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndObject();
                }
            }

            yamlTypeInfo.OnSerialized?.Invoke(obj);

            return true;
        }

        // AggressiveInlining since this method is only called from two locations and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ReadPropertyValue(
            object obj,
            scoped ref ReadStack state,
            ref Utf8YamlReader reader,
            YamlPropertyInfo yamlPropertyInfo,
            bool useExtensionProperty)
        {
            // Skip the property if not found.
            if (!yamlPropertyInfo.CanDeserializeOrPopulate)
            {
                // The Utf8YamlReader.Skip() method will fail fast if it detects that we're reading
                // from a partially read buffer, regardless of whether the next value is available.
                // This can result in erroneous failures in cases where a custom converter is calling
                // into a built-in converter (cf. https://github.com/dotnet/runtime/issues/74108).
                // For this reason we need to call the TrySkip() method instead -- the serializer
                // should guarantee sufficient read-ahead has been performed for the current object.
                bool success = reader.TrySkip();
                Debug.Assert(success, "Serializer should guarantee sufficient read-ahead has been done.");
            }
            else
            {
                // Set the property value.
                reader.ReadWithVerify();

                if (!useExtensionProperty)
                {
                    yamlPropertyInfo.ReadYamlAndSetMember(obj, ref state, ref reader);
                }
                else
                {
                    yamlPropertyInfo.ReadYamlAndAddExtensionProperty(obj, ref state, ref reader);
                }
            }

            // Ensure any exception thrown in the next read does not have a property in its YamlPath.
            state.Current.EndProperty();
        }

        protected static bool ReadAheadPropertyValue(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlPropertyInfo yamlPropertyInfo)
        {
            // Extension properties can use the YamlElement converter and thus require read-ahead.
            bool requiresReadAhead = yamlPropertyInfo.EffectiveConverter.RequiresReadAhead || state.Current.UseExtensionProperty;
            return reader.TryAdvanceWithOptionalReadAhead(requiresReadAhead);
        }
    }
}
