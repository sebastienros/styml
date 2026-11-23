// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Default base class implementation of <cref>YamlDictionaryConverter{TCollection}</cref> .
    /// </summary>
    internal abstract class DictionaryDefaultConverter<TDictionary, TKey, TValue>
        : YamlDictionaryConverter<TDictionary, TKey, TValue>
        where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        internal override bool CanHaveMetadata => true;

        protected internal override bool OnWriteResume(
            Utf8YamlWriter writer,
            TDictionary value,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            IEnumerator<KeyValuePair<TKey, TValue>> enumerator;
            if (state.Current.CollectionEnumerator is null)
            {
                enumerator = value.GetEnumerator();
                state.Current.CollectionEnumerator = enumerator;
                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
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
                    TKey key = enumerator.Current.Key;
                    WriteKey(writer, key, options, ref state);
                }

                TValue element = enumerator.Current.Value;
                if (!_valueConverter.TryWrite(writer, element, options, ref state))
                {
                    return false;
                }

                state.Current.EndDictionaryEntry();
            } while (enumerator.MoveNext());

            enumerator.Dispose();
            return true;
        }
    }
}
