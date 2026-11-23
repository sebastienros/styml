// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    // Converter for F# maps: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2.html
    internal sealed class FSharpMapConverter<TMap, TKey, TValue> : DictionaryDefaultConverter<TMap, TKey, TValue>
        where TMap : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        private readonly Func<IEnumerable<Tuple<TKey, TValue>>, TMap> _mapConstructor;

        [RequiresUnreferencedCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        [RequiresDynamicCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        public FSharpMapConverter()
        {
            _mapConstructor = FSharpCoreReflectionProxy.Instance.CreateFSharpMapConstructor<TMap, TKey, TValue>();
        }

        protected override void Add(TKey key, in TValue value, YamlSerializerOptions options, ref ReadStack state)
        {
            ((List<Tuple<TKey, TValue>>)state.Current.ReturnValue!).Add(new Tuple<TKey, TValue>(key, value));
        }

        internal override bool CanHaveMetadata => false;

        internal override bool SupportsCreateObjectDelegate => false;
        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            state.Current.ReturnValue = new List<Tuple<TKey, TValue>>();
        }

        internal sealed override bool IsConvertibleCollection => true;
        protected override void ConvertCollection(ref ReadStack state, YamlSerializerOptions options)
        {
            List<Tuple<TKey, TValue>> listToConvert = (List<Tuple<TKey, TValue>>)state.Current.ReturnValue!;
            if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                // M5c: the map's constructor keeps the last of a repeated key, so the first is kept here.
                var seen = new HashSet<TKey>();
                listToConvert = listToConvert.FindAll(entry => seen.Add(entry.Item1));
            }
            TMap map = _mapConstructor(listToConvert);
            state.Current.ReturnValue = map;

            if (!options.AllowDuplicateProperties)
            {
                int totalItemsAdded = listToConvert.Count;
                int mapCount = ((ICollection<KeyValuePair<TKey, TValue>>)map).Count;

                if (mapCount != totalItemsAdded)
                {
                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed();
                }
            }
        }
    }
}
