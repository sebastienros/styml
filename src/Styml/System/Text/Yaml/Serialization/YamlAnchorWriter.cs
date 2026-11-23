// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// <see cref="ReferenceHandler.Preserve"/> and <see cref="ReferenceHandler.PreserveAll"/> in block style:
    /// an object or collection written again is an alias to the anchor
    /// of its first write. <see cref="ReferenceHandler.PreserveAll"/> anchors every object; <see cref="ReferenceHandler.Preserve"/>
    /// anchors only the objects a first pass over the graph counted twice or more. Only reached behind the existing
    /// <c>ReferenceHandlingStrategy</c> test, so the default handler runs none of this.
    /// </summary>
    internal sealed class YamlAnchorWriter
    {
        // The instances written so far, with their anchor name, or null when written without one.
        private readonly Dictionary<object, string?> _written = new(ReferenceEqualityComparer.Instance);

        // Preserve: how many times the first pass met each instance; null when every object is anchored.
        private readonly Dictionary<object, int>? _counts;

        private YamlAnchorWriter(Dictionary<object, int>? counts) => _counts = counts;

        /// <summary>
        /// Writes an alias for an instance written before, and returns true; or writes the anchor the instance gets, if
        /// any, and returns false for the converter to write it.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryWriteAliasOrAnchor(object value, YamlTypeInfo typeInfo, Utf8YamlWriter writer, YamlSerializerOptions options, ref WriteStack state)
        {
            YamlAnchorWriter anchors = state.YamlAnchors ??= Create(value, typeInfo, options, ref state);
            if (anchors._written.TryGetValue(value, out string? name))
            {
                if (name is null)
                {
                    // Written once without an anchor (the pass did not count it twice: a getter that returns another
                    // instance): written in full again, as under the default handler.
                    return false;
                }

                writer.WriteAliasValue(name);
                state.PolymorphicTypeDiscriminator = null;
                state.PolymorphicTypeResolver = null;
                return true;
            }

            if (anchors._counts is null || (anchors._counts.TryGetValue(value, out int count) && count > 1))
            {
                name = writer.NextAnchorName();
                writer.WriteAnchor(name);
            }

            anchors._written[value] = name;
            return false;
        }

        private static YamlAnchorWriter Create(object value, YamlTypeInfo typeInfo, YamlSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(options.ReferenceHandler is { AnchorMode: not YamlAnchorMode.None });
            if (options.ReferenceHandler.AnchorMode == YamlAnchorMode.All || state.CurrentDepth > 0)
            {
                // PreserveAll; or the first object is not the root (a value-type or custom-converter root), so a pass from
                // it would not see the whole graph: every object is anchored.
                return new YamlAnchorWriter(counts: null);
            }

            var pass = new FirstPass(options);
            pass.Visit(value, typeInfo, depth: 0);
            return new YamlAnchorWriter(pass.Complete ? pass.Counts : null);
        }

        /// <summary>
        /// Counts each reference-type instance the serializer's own contracts reach from the root. It does not enter a
        /// custom converter (its writes are a separate reference scope), and it stops at what it cannot enumerate twice:
        /// an <see cref="IAsyncEnumerable{T}"/>, or an enumerable that is not a collection. Then it is incomplete.
        /// </summary>
        private sealed class FirstPass
        {
            private readonly YamlSerializerOptions _options;

            internal FirstPass(YamlSerializerOptions options) => _options = options;

            internal Dictionary<object, int> Counts { get; } = new(ReferenceEqualityComparer.Instance);

            internal bool Complete { get; private set; } = true;

            internal void Visit(object? value, YamlTypeInfo declared, int depth)
            {
                if (value is null || !Complete)
                {
                    return;
                }

                Type runtimeType = value.GetType();
                YamlTypeInfo typeInfo = runtimeType == declared.Type || !(declared.Type == typeof(object) || declared.PolymorphicTypeResolver is not null)
                    ? declared
                    : _options.GetTypeInfoInternal(runtimeType);

                if (!typeInfo.Converter.IsInternalConverter || typeInfo.Kind == YamlTypeInfoKind.None)
                {
                    // A custom converter, or a value: nothing here can be an alias.
                    return;
                }

                if (!runtimeType.IsValueType)
                {
                    Counts.TryGetValue(value, out int count);
                    Counts[value] = count + 1;
                    if (count > 0)
                    {
                        return;
                    }
                }

                if (depth > _options.EffectiveMaxDepth)
                {
                    // The write throws its depth error; the pass need not go further.
                    return;
                }

                switch (typeInfo.Kind)
                {
                    case YamlTypeInfoKind.Object:
                        foreach (YamlPropertyInfo property in typeInfo.Properties)
                        {
                            if (!property.CanSerialize || property.Get is null)
                            {
                                continue;
                            }

                            object? member = property.GetValueAsObject(value);
                            if (property.ShouldSerialize is { } shouldSerialize && !shouldSerialize(value, member))
                            {
                                continue;
                            }

                            if (property.IgnoreEmptyCollectionsOnWrite && property.IsNullOrEmptyCollection(member))
                            {
                                // M5f: omitted by the write, so neither counted nor anchored.
                                continue;
                            }

                            if (property.IsExtensionData && member is IEnumerable extension)
                            {
                                VisitDictionaryValues(extension, property.YamlTypeInfo, depth + 1);
                            }
                            else
                            {
                                Visit(member, property.YamlTypeInfo, depth + 1);
                            }
                        }

                        break;

                    case YamlTypeInfoKind.Enumerable:
                        if (value is not (Array or ICollection))
                        {
                            // An IAsyncEnumerable, a lazy enumerable, or a collection without the non-generic
                            // ICollection (HashSet<T>): enumerating it again may differ or fail, so it is not entered.
                            Complete = false;
                            return;
                        }

                        YamlTypeInfo elementTypeInfo = typeInfo.ElementTypeInfo!;
                        foreach (object? item in (IEnumerable)value)
                        {
                            Visit(item, elementTypeInfo, depth + 1);
                        }

                        break;

                    case YamlTypeInfoKind.Dictionary:
                        VisitDictionaryValues((IEnumerable)value, typeInfo, depth + 1);
                        break;
                }
            }

            private void VisitDictionaryValues(IEnumerable dictionary, YamlTypeInfo typeInfo, int depth)
            {
                YamlTypeInfo valueTypeInfo = typeInfo.ElementTypeInfo ?? _options.GetTypeInfoInternal(typeof(object));

                // M5e: a key written as a collection key is written through its contract, so it is visited too
                // (a key instance that repeats, or that is also a value, is anchored once).
                YamlTypeInfo? keyTypeInfo = typeInfo.KeyTypeInfo;
                bool keysMayBeCollections = keyTypeInfo is not null && YamlComplexKeys.MayWriteComplexKey(keyTypeInfo.Converter, _options);
                if (dictionary is IDictionary nonGeneric)
                {
                    foreach (DictionaryEntry entry in nonGeneric)
                    {
                        if (keysMayBeCollections && YamlComplexKeys.WritesComplexKey(keyTypeInfo!.Converter, entry.Key, _options))
                        {
                            Visit(entry.Key, keyTypeInfo, depth);
                        }

                        Visit(entry.Value, valueTypeInfo, depth);
                    }

                    return;
                }

                // A dictionary that is not an IDictionary: the pass cannot read its values without reflection.
                Complete = false;
            }
        }
    }
}
