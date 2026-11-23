// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class QueueOfTConverter<TCollection, TElement>
        : IEnumerableDefaultConverter<TCollection, TElement>
        where TCollection : Queue<TElement>
    {
        internal override bool CanPopulate => true;

        protected override void Add(in TElement value, ref ReadStack state)
        {
            ((TCollection)state.Current.ReturnValue!).Enqueue(value);
        }

        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
            {
                return;
            }

            if (state.Current.YamlTypeInfo.CreateObject is null)
            {
                ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(state.Current.YamlTypeInfo.Type);
            }

            state.Current.ReturnValue = state.Current.YamlTypeInfo.CreateObject();
        }
    }
}
