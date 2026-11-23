// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Base class for all collections. Collections are assumed to implement <see cref="IEnumerable{T}"/>
    /// or a variant thereof e.g. <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    internal abstract class YamlCollectionConverter<TCollection, TElement> : YamlResumableConverter<TCollection>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Enumerable;
        internal sealed override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Array;
        internal override Type ElementType => typeof(TElement);

        internal override bool? IsEmptyCollection(object value)
        {
            try
            {
                return value switch
                {
                    ICollection<TElement> collection => collection.Count == 0,
                    IReadOnlyCollection<TElement> collection => collection.Count == 0,
                    System.Collections.ICollection collection => collection.Count == 0,
                    _ => null,
                };
            }
            catch (InvalidOperationException) when (value.GetType().IsValueType)
            {
                // A default ImmutableArray<T> has no count; it is empty.
                return true;
            }
        }

        protected abstract void Add(in TElement value, ref ReadStack state);

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
            {
                return;
            }

            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;

            if (typeInfo.CreateObject is null)
            {
                ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(typeInfo, ref reader, ref state);
            }

            state.Current.ReturnValue = typeInfo.CreateObject();
            Debug.Assert(state.Current.ReturnValue is TCollection);
        }

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// The <see cref="YamlConverter.IsConvertibleCollection"/> property must also be set to <see langword="true"/>.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, YamlSerializerOptions options) { }

        protected static YamlConverter<TElement> GetElementConverter(YamlTypeInfo elementTypeInfo)
        {
            return ((YamlTypeInfo<TElement>)elementTypeInfo).EffectiveConverter;
        }

        protected static YamlConverter<TElement> GetElementConverter(ref WriteStack state)
        {
            Debug.Assert(state.Current.YamlPropertyInfo is not null);
            return (YamlConverter<TElement>)state.Current.YamlPropertyInfo.EffectiveConverter;
        }

        // The fast path's loop through TryRead, for AcceptLenientScalars: a copy of the loop OnTryRead runs for
        // an element converter without the direct read, kept out of line (M4p, candidate f).

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static TElement? ReadElementWithNodeRef(ref Utf8YamlReader reader, YamlConverter<TElement> elementConverter, YamlSerializerOptions options, scoped ref ReadStack state)
        {
            elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _);
            return element;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReadElementsThroughTryRead(ref Utf8YamlReader reader, YamlConverter<TElement> elementConverter, YamlSerializerOptions options, scoped ref ReadStack state)
        {
            while (true)
            {
                reader.ReadWithVerify();
                if (reader.TokenType == YamlTokenType.EndArray)
                {
                    break;
                }

                elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _);
                Add(element!, ref state);
            }
        }

        internal override bool OnTryRead(
            ref Utf8YamlReader reader,
            Type typeToConvert,
            YamlSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TCollection value)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;
            YamlTypeInfo elementTypeInfo = yamlTypeInfo.ElementTypeInfo!;

            // M5d: a tagged node at a site only TagMappings makes polymorphic takes the slower path, which
            // dispatches on the tag without the metadata rules ($-members stay members there).
            if (!state.SupportContinuation && !state.Current.CanContainMetadata && !YamlSerializer.HasMappedTag(yamlTypeInfo, ref reader))
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != YamlTokenType.StartArray)
                {
                    ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state, options);

                yamlTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                state.Current.YamlPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                YamlConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                if (elementConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling is null)
                {
                    // AcceptLenientScalars reads null elements as their default through TryRead: one test per
                    // collection, inside this branch and with its loop out of line, so that the branches' weights, and
                    // the JIT's inlining of the TryRead loop below, are as without the option (M4p, candidate f). The option
                    // acts only where default(TElement) is not null, as in TryRead, so for reference types (the shared
                    // code) the JIT removes the test and the call.
                    if (default(TElement) is not null && reader.AcceptsLenientScalars)
                    {
                        ReadElementsThroughTryRead(ref reader, elementConverter, options, ref state);
                    }
                    else
                    {
                        // Fast path that avoids validation and extra indirection.
                        while (true)
                        {
                            reader.ReadWithVerify();
                            if (reader.TokenType == YamlTokenType.EndArray)
                            {
                                break;
                            }

                            // Obtain the CLR value from the JSON and apply to the object. An anchor or an alias (M5b)
                            // goes through TryRead.
                            TElement? element = reader.YamlHasNodeRef
                                ? ReadElementWithNodeRef(ref reader, elementConverter, options, ref state)
                                : elementConverter.Read(ref reader, elementConverter.Type, options);
                            Add(element!, ref state);
                        }
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        reader.ReadWithVerify();
                        if (reader.TokenType == YamlTokenType.EndArray)
                        {
                            break;
                        }

                        // Get the value from the converter and add it.
                        elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _);
                        Add(element!, ref state);
                    }
                }
            }
            else
            {
                // Slower path that supports continuation and reading metadata.
                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    // M5d: a tag on the start token selects the derived type, before the shape is checked.
                    YamlSerializer.ReadTagDiscriminator(yamlTypeInfo, ref reader, ref state);

                    if (reader.TokenType == YamlTokenType.StartArray)
                    {
                        state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                    }
                    else if (state.Current.CanContainMetadata)
                    {
                        if (reader.TokenType != YamlTokenType.StartObject)
                        {
                            ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                        }

                        state.Current.ObjectState = StackFrameObjectState.StartToken;
                    }
                    else
                    {
                        ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                    }
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
                        value = YamlSerializer.ResolveReferenceId<TCollection>(ref state);
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
                    value = (TCollection)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        YamlSerializer.ValidateMetadataForArrayConverter(this, ref reader, ref state);
                    }

                    CreateCollection(ref reader, ref state, options);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId is not null);
                        Debug.Assert(options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TCollection);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    // M5b: a collection whose created instance is its result can be referred to from inside it.
                    if (state.Current.ReturnValue is TCollection created)
                    {
                        YamlAliasReader.StoreAtCreation(ref reader, ref state, created);
                    }

                    yamlTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                if (state.Current.ObjectState < StackFrameObjectState.ReadElements)
                {
                    YamlConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                    state.Current.YamlPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;

                    // Process all elements.
                    while (true)
                    {
                        if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                        {
                            if (!reader.TryAdvanceWithOptionalReadAhead(elementConverter.RequiresReadAhead))
                            {
                                value = default;
                                return false;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValue;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.ReadValueIsEnd)
                        {
                            if (reader.TokenType == YamlTokenType.EndArray)
                            {
                                break;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValueIsEnd;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                        {
                            // Get the value from the converter and add it.
                            if (!elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
                            {
                                value = default;
                                return false;
                            }

                            Add(element!, ref state);

                            // No need to set PropertyState to TryRead since we're done with this element now.
                            state.Current.EndElement();
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadElements;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndToken)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.EndToken;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndTokenValidation)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (reader.TokenType != YamlTokenType.EndObject)
                        {
                            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
                            if (options.AllowOutOfOrderMetadataProperties)
                            {
                                Debug.Assert(YamlSerializer.IsMetadataPropertyName(reader.GetUnescapedSpan(), (state.Current.BaseYamlTypeInfo ?? yamlTypeInfo).PolymorphicTypeResolver), "should only be hit if metadata property.");
                                bool result = reader.TrySkipPartial(reader.CurrentDepth - 1); // skip to the end of the object
                                Debug.Assert(result, "Metadata reader must have buffered all contents.");
                                Debug.Assert(reader.TokenType is YamlTokenType.EndObject);
                            }
                            else
                            {
                                ThrowHelper.ThrowYamlException_MetadataInvalidPropertyInArrayMetadata(ref state, typeToConvert, reader);
                            }
                        }
                    }
                }
            }

            ConvertCollection(ref state, options);
            object returnValue = state.Current.ReturnValue!;
            yamlTypeInfo.OnDeserialized?.Invoke(returnValue);
            value = (TCollection)returnValue;

            return true;
        }

        internal override bool OnTryWrite(
            Utf8YamlWriter writer,
            TCollection value,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            bool success;

            if (value is null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;

                if (!state.Current.ProcessedStartToken)
                {
                    state.Current.ProcessedStartToken = true;

                    yamlTypeInfo.OnSerializing?.Invoke(value);

                    if (state.CurrentContainsMetadata && CanHaveMetadata)
                    {
                        state.Current.MetadataPropertyName = YamlSerializer.WriteMetadataForCollection(this, ref state, writer);
                    }

                    // Writing the start of the array must happen after any metadata
                    writer.WriteStartArray();
                    state.Current.YamlPropertyInfo = yamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
                }

                success = OnWriteResume(writer, value, options, ref state);
                if (success)
                {
                    if (!state.Current.ProcessedEndToken)
                    {
                        state.Current.ProcessedEndToken = true;
                        writer.WriteEndArray();

                        if (state.Current.MetadataPropertyName != 0)
                        {
                            // Write the EndObject for $values.
                            writer.WriteEndObject();
                        }
                    }

                    yamlTypeInfo.OnSerialized?.Invoke(value);
                }
            }

            return success;
        }

        protected abstract bool OnWriteResume(Utf8YamlWriter writer, TCollection value, YamlSerializerOptions options, ref WriteStack state);
    }
}
