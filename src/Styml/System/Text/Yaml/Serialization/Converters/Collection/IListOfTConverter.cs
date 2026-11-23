// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for <cref>System.Collections.Generic.IList{TElement}</cref>.
    /// </summary>
    internal sealed class IListOfTConverter<TCollection, TElement>
        : IEnumerableDefaultConverter<TCollection, TElement>
        where TCollection : IList<TElement>
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
            // Deserialize as List<T> for interface types that support it.
            if (yamlTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(List<TElement>)))
            {
                Debug.Assert(Type.IsInterface);
                yamlTypeInfo.CreateObject = () => new List<TElement>();
            }
        }
    }
}
