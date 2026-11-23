// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal class StackOrQueueConverter<TCollection>
        : YamlCollectionConverter<TCollection, object?>
        where TCollection : IEnumerable
    {
        internal override bool CanPopulate => true;

        protected sealed override void Add(in object? value, ref ReadStack state)
        {
            var addMethodDelegate = ((Action<TCollection, object?>?)state.Current.YamlTypeInfo.AddMethodDelegate);
            Debug.Assert(addMethodDelegate is not null);
            addMethodDelegate((TCollection)state.Current.ReturnValue!, value);
        }

        protected sealed override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
            {
                return;
            }

            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;
            Func<object>? constructorDelegate = typeInfo.CreateObject;

            if (constructorDelegate is null)
            {
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }

            state.Current.ReturnValue = constructorDelegate();

            Debug.Assert(typeInfo.AddMethodDelegate is not null);
        }

        protected sealed override bool OnWriteResume(Utf8YamlWriter writer, TCollection value, YamlSerializerOptions options, ref WriteStack state)
        {
            IEnumerator enumerator;
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
                enumerator = state.Current.CollectionEnumerator;
            }

            YamlConverter<object?> converter = GetElementConverter(ref state);
            do
            {
                if (ShouldFlush(ref state, writer))
                {
                    return false;
                }

                object? element = enumerator.Current;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    return false;
                }

                state.Current.EndCollectionElement();
            } while (enumerator.MoveNext());

            return true;
        }
    }
}
