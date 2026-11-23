// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for <cref>System.Collections.IDictionary</cref> that (de)serializes as a YAML object with properties
    /// representing the dictionary element key and value.
    /// </summary>
    internal sealed class IDictionaryConverter<TDictionary>
        : YamlDictionaryConverter<TDictionary, string, object?>
        where TDictionary : IDictionary
    {
        internal override bool CanPopulate => true;

        protected override bool ContainsKey(string key, scoped ref ReadStack state) => ((TDictionary)state.Current.ReturnValue!).Contains(key);

        protected override int SeededCount(scoped ref ReadStack state) => ((TDictionary)state.Current.ReturnValue!).Count;

        protected override void Set(string key, in object? value, YamlSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;
            collection[key] = value;
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
        }

        protected override void Add(string key, in object? value, YamlSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;

            if (!options.AllowDuplicateProperties && collection.Contains(key))
            {
                ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(key);
            }

            collection[key] = value;

            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
        }

        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            base.CreateCollection(ref reader, ref state);
            TDictionary returnValue = (TDictionary)state.Current.ReturnValue!;
            if (returnValue.IsReadOnly)
            {
                state.Current.ReturnValue = null; // clear out for more accurate YamlPath reporting.
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }
        }

        protected internal override bool OnWriteResume(Utf8YamlWriter writer, TDictionary value, YamlSerializerOptions options, ref WriteStack state)
        {
            IDictionaryEnumerator enumerator;
            if (state.Current.CollectionEnumerator is null)
            {
                enumerator = value.GetEnumerator();
                state.Current.CollectionEnumerator = enumerator;
                if (!enumerator.MoveNext())
                {
                    return true;
                }
            }
            else
            {
                enumerator = (IDictionaryEnumerator)state.Current.CollectionEnumerator;
            }

            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;
            _valueConverter ??= GetConverter<object?>(typeInfo.ElementTypeInfo!);

            do
            {
                if (ShouldFlush(ref state, writer))
                {
                    return false;
                }

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;
                    object key = enumerator.Key;
                    writer.HintDefaultScalarStyleForNextKey(); // M7b (fix A)
                    // Optimize for string since that's the hot path.
                    if (key is string keyString)
                    {
                        _keyConverter ??= GetConverter<string>(typeInfo.KeyTypeInfo!);
                        _keyConverter.WriteAsPropertyNameCore(writer, keyString, options, state.Current.IsWritingExtensionDataProperty);
                    }
                    else
                    {
                        // IDictionary is a special case since it has polymorphic object semantics on serialization
                        // but needs to use YamlConverter<string> on deserialization.
                        _valueConverter.WriteAsPropertyNameCore(writer, key, options, state.Current.IsWritingExtensionDataProperty);
                    }
                }

                object? element = enumerator.Value;
                if (!_valueConverter.TryWrite(writer, element, options, ref state))
                {
                    return false;
                }

                state.Current.EndDictionaryEntry();
            } while (enumerator.MoveNext());

            return true;
        }

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            // Deserialize as Dictionary<TKey,TValue> for interface types that support it.
            if (yamlTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(Dictionary<string, object?>)))
            {
                Debug.Assert(Type.IsInterface);
                yamlTypeInfo.CreateObject = () => new Dictionary<string, object?>();
            }
        }
    }
}
