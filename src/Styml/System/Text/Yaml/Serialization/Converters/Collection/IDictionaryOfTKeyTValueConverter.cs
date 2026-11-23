// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for <cref>System.Collections.Generic.IDictionary{TKey, TValue}</cref> that
    /// (de)serializes as a YAML object with properties representing the dictionary element key and value.
    /// </summary>
    internal sealed class IDictionaryOfTKeyTValueConverter<TDictionary, TKey, TValue>
        : DictionaryDefaultConverter<TDictionary, TKey, TValue>
        where TDictionary : IDictionary<TKey, TValue>
        where TKey : notnull
    {
        internal override bool CanPopulate => true;

        protected override bool ContainsKey(TKey key, scoped ref ReadStack state) => ((TDictionary)state.Current.ReturnValue!).ContainsKey(key);

        protected override int SeededCount(scoped ref ReadStack state) => ((TDictionary)state.Current.ReturnValue!).Count;

        protected override IEqualityComparer<TKey>? SeededComparer(scoped ref ReadStack state) =>
            state.Current.ReturnValue is Dictionary<TKey, TValue> dictionary ? dictionary.Comparer : null;

        protected override void Set(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;
            collection[key] = value;
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
        }

        protected override void Add(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;

            if (!options.AllowDuplicateProperties && collection.ContainsKey(key))
            {
                ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed();
            }

            // M5c: the indexer on the local, not the TryAdd extension, which boxes a struct dictionary and adds to the
            // copy (System.Text.Json's refusal path; with Error by default it is the default path).
            collection[key] = value;

            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            };
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

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            // Deserialize as Dictionary<TKey,TValue> for interface types that support it.
            if (yamlTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            {
                Debug.Assert(Type.IsInterface);
                yamlTypeInfo.CreateObject = () => new Dictionary<TKey, TValue>();
            }
        }
    }
}
