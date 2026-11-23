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
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class YamlDictionaryConverter<TDictionary> : YamlResumableConverter<TDictionary>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Dictionary;
        internal sealed override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Object;

        protected internal abstract bool OnWriteResume(Utf8YamlWriter writer, TDictionary dictionary, YamlSerializerOptions options, ref WriteStack state);

        /// <summary>
        /// Writes extension data's entries sorted by key, ordinal, under <see cref="YamlMappingOrder.Sorted"/>.
        /// The default writes them in the dictionary's order.
        /// </summary>
        internal virtual bool OnWriteExtensionDataSorted(Utf8YamlWriter writer, TDictionary dictionary, YamlSerializerOptions options, ref WriteStack state) =>
            OnWriteResume(writer, dictionary, options, ref state);
    }

    /// <summary>
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class YamlDictionaryConverter<TDictionary, TKey, TValue> : YamlDictionaryConverter<TDictionary>
        where TKey : notnull
    {
        /// <summary>
        /// When overridden, adds the value to the collection.
        /// </summary>
        protected abstract void Add(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state);

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// This is used with immutable collections.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, YamlSerializerOptions options) { }

        /// <summary>
        /// Whether the collection being read already holds <paramref name="key"/>: under
        /// <see cref="YamlDuplicateKeyHandling.FirstWins"/> a later occurrence is skipped before its value is converted.
        /// False when the collection cannot tell.
        /// </summary>
        protected virtual bool ContainsKey(TKey key, scoped ref ReadStack state) => false;

        /// <summary>
        /// How many entries the collection being read held before the read (a populated or pre-instantiated instance);
        /// 0 when it is new. Under Error and FirstWins the document's keys of a seeded collection are then tracked apart
        /// from it, so that a key that appears once overrides the seed.
        /// </summary>
        protected virtual int SeededCount(scoped ref ReadStack state) => 0;

        /// <summary>The comparer of the collection being read, for its document's keys, when it has one.</summary>
        protected virtual IEqualityComparer<TKey>? SeededComparer(scoped ref ReadStack state) => null;

        /// <summary>Sets an entry of a seeded collection, overriding what it held.</summary>
        protected virtual void Set(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state) => Add(key, value, options, ref state);

        // The document's keys of the collection at this depth (a stable target: the converter), or null for a new one.
        private HashSet<TKey>? DocumentKeys(scoped ref ReadStack state, YamlSerializerOptions options, bool start) =>
            options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null
            // A populated instance, or a new one a constructor may have seeded (a reference type), is asked what it holds;
            // a new struct is not (its default value holds nothing, and a struct wrapper's Count can throw on it).
            : state.SeededDocumentKeys<TKey>(this, start && MaySeed(ref state) ? SeededCount(ref state) : 0, reset: start,
                start && MaySeed(ref state) ? SeededComparer(ref state) : null);

        private bool MaySeed(scoped ref ReadStack state) => state.Current.IsPopulating || !IsValueType;

        private bool IsRepeatedKey(TKey key, HashSet<TKey>? documentKeys, YamlSerializerOptions options, scoped ref ReadStack state) =>
            options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins
            && (documentKeys is not null ? documentKeys.Contains(key) : ContainsKey(key, ref state));

        private void AddEntry(TKey key, in TValue value, HashSet<TKey>? documentKeys, YamlSerializerOptions options, ref ReadStack state)
        {
            if (documentKeys is null)
            {
                Add(key, value, options, ref state);
                return;
            }

            if (!documentKeys.Add(key) && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
            {
                ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed();
            }

            Set(key, value, options, ref state);
        }

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state)
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
            Debug.Assert(state.Current.ReturnValue is TDictionary);
        }

        internal override Type ElementType => typeof(TValue);

        internal override Type KeyType => typeof(TKey);

        internal override bool? IsEmptyCollection(object value) => value switch
        {
            ICollection<KeyValuePair<TKey, TValue>> collection => collection.Count == 0,
            IReadOnlyCollection<KeyValuePair<TKey, TValue>> collection => collection.Count == 0,
            System.Collections.ICollection collection => collection.Count == 0,
            _ => null,
        };


        protected YamlConverter<TKey>? _keyConverter;

        // M5f: the entries are copied and sorted once, and the frame keeps the enumerator over the copy, so a
        // suspended write resumes in the same order.
        internal override bool OnWriteExtensionDataSorted(Utf8YamlWriter writer, TDictionary dictionary, YamlSerializerOptions options, ref WriteStack state)
        {
            if (dictionary is not IEnumerable<KeyValuePair<TKey, TValue>> source)
            {
                return OnWriteResume(writer, dictionary, options, ref state);
            }

            IEnumerator<KeyValuePair<TKey, TValue>> enumerator;
            if (state.Current.CollectionEnumerator is null)
            {
                var entries = new List<KeyValuePair<TKey, TValue>>(source);
                entries.Sort(static (x, y) => string.CompareOrdinal(x.Key as string ?? x.Key.ToString(), y.Key as string ?? y.Key.ToString()));
                enumerator = entries.GetEnumerator();
                state.Current.CollectionEnumerator = enumerator;
                if (!enumerator.MoveNext())
                {
                    return true;
                }
            }
            else
            {
                enumerator = (IEnumerator<KeyValuePair<TKey, TValue>>)state.Current.CollectionEnumerator;
            }

            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;
            _keyConverter ??= GetConverter<TKey>(typeInfo.KeyTypeInfo!);
            _valueConverter ??= GetConverter<TValue>(typeInfo.ElementTypeInfo!);
            do
            {
                if (ShouldFlush(ref state, writer))
                {
                    return false;
                }

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;
                    WriteKey(writer, enumerator.Current.Key, options, ref state);
                }

                if (!_valueConverter.TryWrite(writer, enumerator.Current.Value, options, ref state))
                {
                    return false;
                }

                state.Current.EndDictionaryEntry();
            } while (enumerator.MoveNext());

            return true;
        }

        // M5e: 0 unknown, 1 the key converter writes scalar keys only, 2 it may write a collection key.
        private int _complexKeyWrites;

        /// <summary>
        /// Writes a dictionary key: as a property name, as before M5e, unless the key's contract is the library's object,
        /// collection or dictionary contract (for an <c>object</c> key, its runtime type's), which the block writer writes
        /// as an explicit key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteKey(Utf8YamlWriter writer, TKey key, YamlSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(_keyConverter is not null);
            if (_complexKeyWrites == 0)
            {
                _complexKeyWrites = YamlComplexKeys.MayWriteComplexKey(_keyConverter, options) ? 2 : 1;
            }

            // A null key keeps System.Text.Json's ArgumentNullException (WriteAsPropertyNameCore below).
            if (_complexKeyWrites == 2 && key is not null && !state.Current.IsWritingExtensionDataProperty && YamlComplexKeys.WritesComplexKey(_keyConverter, key, options))
            {
                YamlComplexKeys.WriteKey(_keyConverter, state.Current.YamlTypeInfo.KeyTypeInfo!, writer, key, options, ref state);
                return;
            }

            // M7b (fix A): a dictionary's key takes DefaultScalarStyle when it is written as text.
            writer.HintDefaultScalarStyleForNextKey();
            _keyConverter.WriteAsPropertyNameCore(writer, key!, options, state.Current.IsWritingExtensionDataProperty); // a null key throws there (above)
        }
        protected YamlConverter<TValue>? _valueConverter;

        protected static YamlConverter<T> GetConverter<T>(YamlTypeInfo typeInfo)
        {
            return ((YamlTypeInfo<T>)typeInfo).EffectiveConverter;
        }

        internal sealed override bool OnTryRead(
            ref Utf8YamlReader reader,
            Type typeToConvert,
            YamlSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TDictionary value)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;
            YamlTypeInfo keyTypeInfo = yamlTypeInfo.KeyTypeInfo!;
            YamlTypeInfo elementTypeInfo = yamlTypeInfo.ElementTypeInfo!;

            // M5d: a tagged node at a site only TagMappings makes polymorphic takes the slower path, which
            // dispatches on the tag without the metadata rules ($-members stay members there).
            if (!state.SupportContinuation && !state.Current.CanContainMetadata && !YamlSerializer.HasMappedTag(yamlTypeInfo, ref reader))
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state);
                HashSet<TKey>? documentKeys = DocumentKeys(ref state, options, start: true);

                yamlTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);

                // AcceptLenientScalars reads null values as their default through TryRead: one test per dictionary.
                if (_valueConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling is null && !reader.AcceptsLenientScalars)
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == YamlTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);

                        state.Current.YamlPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                        if (IsRepeatedKey(key, documentKeys, options, ref state))
                        {
                            reader.SkipWithVerify();
                            continue;
                        }

                        // Read the value and add.
                        reader.ReadWithVerify();
                        state.Current.YamlPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        TValue? element = reader.YamlHasNodeRef
                            ? ReadValueWithNodeRef(ref reader, _valueConverter, options, ref state)
                            : _valueConverter.Read(ref reader, ElementType, options);
                        AddEntry(key, element!, documentKeys, options, ref state);
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == YamlTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
                        state.Current.YamlPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                        if (IsRepeatedKey(key, documentKeys, options, ref state))
                        {
                            reader.SkipWithVerify();
                            continue;
                        }

                        reader.ReadWithVerify();

                        // Get the value from the converter and add it.
                        state.Current.YamlPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        _valueConverter.TryRead(ref reader, ElementType, options, ref state, out TValue? element, out _);
                        AddEntry(key, element!, documentKeys, options, ref state);
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
                        value = YamlSerializer.ResolveReferenceId<TDictionary>(ref state);
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
                    value = (TDictionary)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                // Create the dictionary.
                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        YamlSerializer.ValidateMetadataForObjectConverter(ref state);
                    }

                    CreateCollection(ref reader, ref state);
                    DocumentKeys(ref state, options, start: true);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId is not null);
                        Debug.Assert(options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TDictionary);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    // M5b: a dictionary whose created instance is its result can be referred to from inside it.
                    if (state.Current.ReturnValue is TDictionary created)
                    {
                        YamlAliasReader.StoreAtCreation(ref reader, ref state, created);
                    }

                    yamlTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                // Process all elements.
                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);
                while (true)
                {
                    if (state.Current.PropertyState == StackFramePropertyState.None)
                    {
                        // Read the key name.
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadName;
                    }

                    // Determine the property.
                    TKey key;
                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        if (reader.TokenType == YamlTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);

                        state.Current.PropertyState = StackFramePropertyState.Name;

                        // M5e: a collection key is never metadata.
                        if (state.Current.CanContainMetadata && reader.KeyKind != YamlKeyKind.Complex)
                        {
                            ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();
                            if (YamlSerializer.IsMetadataPropertyName(propertyName, state.Current.BaseYamlTypeInfo.PolymorphicTypeResolver))
                            {
                                if (options.AllowOutOfOrderMetadataProperties)
                                {
                                    reader.SkipWithVerify();
                                    state.Current.EndElement();
                                    continue;
                                }
                                else
                                {
                                    ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                                }
                            }
                        }

                        state.Current.YamlPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                    }
                    else
                    {
                        // DictionaryKey is assigned before all return false cases, null value is unreachable
                        key = (TKey)state.Current.DictionaryKey!;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                    {
                        if (IsRepeatedKey(key, DocumentKeys(ref state, options, start: false), options, ref state))
                        {
                            // Resumable: on re-entry the key is tested again and the skip continues.
                            if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                            {
                                state.Current.DictionaryKey = key;
                                value = default;
                                return false;
                            }

                            state.Current.EndElement();
                            continue;
                        }

                        if (!reader.TryAdvanceWithOptionalReadAhead(_valueConverter.RequiresReadAhead))
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadValue;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                    {
                        // Get the value from the converter and add it.
                        state.Current.YamlPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        bool success = _valueConverter.TryRead(ref reader, typeof(TValue), options, ref state, out TValue? element, out _);
                        if (!success)
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        AddEntry(key, element!, DocumentKeys(ref state, options, start: false), options, ref state);
                        state.Current.EndElement();
                    }
                }
            }

            ConvertCollection(ref state, options);
            object result = state.Current.ReturnValue!;
            yamlTypeInfo.OnDeserialized?.Invoke(result);
            value = (TDictionary)result;

            return true;

            static TKey ReadDictionaryKey(YamlConverter<TKey> keyConverter, ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
            {
                if (reader.KeyKind != YamlKeyKind.Scalar)
                {
                    // M5b: an alias key reads as its anchored node; M5e: a collection key through the key's contract.
                    return reader.KeyKind == YamlKeyKind.Alias
                        ? YamlAliasReader.ReadAliasKey(keyConverter, ref reader, ref state, options)
                        : YamlComplexKeys.ReadKey(keyConverter, ref reader, ref state, options);
                }

                TKey key;
                string unescapedPropertyNameAsString = reader.GetString()!;
                state.Current.YamlPropertyNameAsString = unescapedPropertyNameAsString; // Copy key name for JSON Path support in case of error.

                // Special case string to avoid calling GetString twice and save one allocation.
                if (keyConverter.IsInternalConverter && keyConverter.Type == typeof(string))
                {
                    key = (TKey)(object)unescapedPropertyNameAsString;
                }
                else
                {
                    key = keyConverter.ReadAsPropertyNameCore(ref reader, keyConverter.Type, options);
                }

                return key;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static TValue? ReadValueWithNodeRef(ref Utf8YamlReader reader, YamlConverter<TValue> valueConverter, YamlSerializerOptions options, scoped ref ReadStack state)
        {
            valueConverter.TryRead(ref reader, typeof(TValue), options, ref state, out TValue? value, out _);
            return value;
        }


        internal sealed override bool OnTryWrite(
            Utf8YamlWriter writer,
            TDictionary dictionary,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            if (dictionary is null)
            {
                writer.WriteNullValue();
                return true;
            }

            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;

            if (!state.Current.ProcessedStartToken)
            {
                state.Current.ProcessedStartToken = true;

                yamlTypeInfo.OnSerializing?.Invoke(dictionary);

                writer.WriteStartObject();

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    YamlSerializer.WriteMetadataForObject(this, ref state, writer);
                }

                state.Current.YamlPropertyInfo = yamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            }

            bool success = OnWriteResume(writer, dictionary, options, ref state);
            if (success)
            {
                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndObject();
                }

                yamlTypeInfo.OnSerialized?.Invoke(dictionary);
            }

            return success;
        }
    }
}
