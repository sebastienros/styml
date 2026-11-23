// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class ISetOfTConverter<TCollection, TElement>
        : IEnumerableDefaultConverter<TCollection, TElement>
        where TCollection : ISet<TElement>
    {
        internal override bool CanPopulate => true;

        protected override void Add(in TElement value, ref ReadStack state)
        {
            TCollection collection = (TCollection)state.Current.ReturnValue!;
            collection.Add(value);
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            };
        }

        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            base.CreateCollection(ref reader, ref state, options);
            TCollection returnValue = (TCollection)state.Current.ReturnValue!;
            if (returnValue.IsReadOnly)
            {
                state.Current.ReturnValue = null; // clear out for more accurate YamlPath reporting.
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }
        }

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            // Deserialize as HashSet<TElement> for interface types that support it.
            if (yamlTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                Debug.Assert(Type.IsInterface);
                yamlTypeInfo.CreateObject = () => new HashSet<TElement>();
            }
        }
    }
}
