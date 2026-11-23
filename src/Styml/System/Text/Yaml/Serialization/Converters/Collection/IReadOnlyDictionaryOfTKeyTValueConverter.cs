// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class IReadOnlyDictionaryOfTKeyTValueConverter<TDictionary, TKey, TValue>
        : DictionaryDefaultConverter<TDictionary, TKey, TValue>
        where TDictionary : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly bool _isDeserializable = typeof(TDictionary).IsAssignableFrom(typeof(Dictionary<TKey, TValue>));

        protected override bool ContainsKey(TKey key, scoped ref ReadStack state) => ((Dictionary<TKey, TValue>)state.Current.ReturnValue!).ContainsKey(key);

        protected override void Add(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state)
        {
            Dictionary<TKey, TValue> dictionary = (Dictionary<TKey, TValue>)state.Current.ReturnValue!;

            if (options.AllowDuplicateProperties)
            {
                dictionary[key] = value;
            }
            else
            {
                if (!dictionary.TryAdd(key, value))
                {
                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed();
                }
            }
        }

        internal override bool SupportsCreateObjectDelegate => false;
        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            if (!_isDeserializable)
            {
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }

            state.Current.ReturnValue = new Dictionary<TKey, TValue>();
        }
    }
}
