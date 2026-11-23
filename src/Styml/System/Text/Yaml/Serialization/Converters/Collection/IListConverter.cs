// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// Converter for <cref>System.Collections.IList</cref>.
    internal sealed class IListConverter<TCollection>
        : YamlCollectionConverter<TCollection, object?>
        where TCollection : IList
    {
        internal override bool CanPopulate => true;

        protected override void Add(in object? value, ref ReadStack state)
        {
            TCollection collection = (TCollection)state.Current.ReturnValue!;
            collection.Add(value);
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
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

        protected override bool OnWriteResume(Utf8YamlWriter writer, TCollection value, YamlSerializerOptions options, ref WriteStack state)
        {
            IList list = value;

            // Using an index is 2x faster than using an enumerator.
            int index = state.Current.EnumeratorIndex;
            YamlConverter<object?> elementConverter = GetElementConverter(ref state);

            if (elementConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling is null)
            {
                // Fast path that avoids validation and extra indirection.
                for (; index < list.Count; index++)
                {
                    elementConverter.Write(writer, list[index], options);
                }
            }
            else
            {
                for (; index < list.Count; index++)
                {
                    object? element = list[index];
                    if (!elementConverter.TryWrite(writer, element, options, ref state))
                    {
                        state.Current.EnumeratorIndex = index;
                        return false;
                    }

                    state.Current.EndCollectionElement();

                    if (ShouldFlush(ref state, writer))
                    {
                        state.Current.EnumeratorIndex = ++index;
                        return false;
                    }
                }
            }

            return true;
        }

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            // Deserialize as List<object?> for interface types that support it.
            if (yamlTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(List<object?>)))
            {
                Debug.Assert(Type.IsInterface);
                yamlTypeInfo.CreateObject = () => new List<object?>();
            }
        }
    }
}
