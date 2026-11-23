// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal class ImmutableDictionaryOfTKeyTValueConverter<TDictionary, TKey, TValue>
        : DictionaryDefaultConverter<TDictionary, TKey, TValue>
        where TDictionary : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        protected sealed override bool ContainsKey(TKey key, scoped ref ReadStack state) => ((Dictionary<TKey, TValue>)state.Current.ReturnValue!).ContainsKey(key);

        protected sealed override void Add(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state)
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

        internal sealed override bool CanHaveMetadata => false;

        internal override bool SupportsCreateObjectDelegate => false;
        protected sealed override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            state.Current.ReturnValue = new Dictionary<TKey, TValue>();
        }

        internal sealed override bool IsConvertibleCollection => true;
        protected sealed override void ConvertCollection(ref ReadStack state, YamlSerializerOptions options)
        {
            Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? creator =
                (Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>?)state.Current.YamlTypeInfo.CreateObjectWithArgs;
            Debug.Assert(creator is not null);
            state.Current.ReturnValue = creator((Dictionary<TKey, TValue>)state.Current.ReturnValue!);
        }
    }
}
