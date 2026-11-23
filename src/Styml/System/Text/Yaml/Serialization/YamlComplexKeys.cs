// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The serializer's side of collection keys: a dictionary
    /// key that is a collection (<see cref="YamlKeyKind.Complex"/>) is read through the key converter's ordinary read, on
    /// the key reader, and written by the block writer as an explicit key. Off the hot path: the dictionary converters
    /// test the key's kind where they tested for an alias key, and a scalar key does no new work.
    /// </summary>
    internal static class YamlComplexKeys
    {
        /// <summary>
        /// Whether a key converter reads a collection key: the library's object, collection and dictionary contracts,
        /// <c>object</c>, <see cref="YamlElement"/> and <see cref="YamlNode"/>, and custom converters (through their
        /// <c>Read</c>). The library's scalar converters (strings, numbers, enums…) do not.
        /// </summary>
        internal static bool CanRead(YamlConverter converter) =>
            !converter.IsInternalConverter
            || converter.IsUntypedConverter
            || converter.ConverterStrategy is ConverterStrategy.Object or ConverterStrategy.Enumerable or ConverterStrategy.Dictionary
            || converter.Type == typeof(YamlElement)
            || typeof(YamlNode).IsAssignableFrom(converter.Type);

        /// <summary>The key's raw text: its name in the DOM, and what errors and paths show.</summary>
        internal static string GetRawText(scoped ref Utf8YamlReader reader)
        {
            Debug.Assert(reader.KeyKind == YamlKeyKind.Complex);
            return YamlReaderHelper.TranscodeHelper(reader.YamlValueBytes);
        }

        /// <summary>
        /// A collection key at a class's member site (review 5). It never names a member: the name returned is
        /// not valid UTF-8 (a 0xFF byte, then the raw text), so no member and no metadata name can match it. Without
        /// extension data it is skipped, or an error under <see cref="YamlUnmappedMemberHandling.Disallow"/>; string-keyed
        /// extension data refuses it, as a string-keyed dictionary does; a <see cref="YamlObject"/> extension member holds
        /// it as a collection key (<see cref="ReadStack.PendingComplexKey"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static byte[] MemberSiteName(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlSerializerOptions options)
        {
            string raw = GetRawText(ref reader);
            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;
            if (typeInfo.ExtensionDataProperty is { } extension)
            {
                if (!typeof(YamlObject).IsAssignableFrom(extension.PropertyType))
                {
                    ThrowHelper.ThrowYamlException_ComplexKeyNotReadable(raw, typeof(string));
                }

                Utf8YamlReader keyReader = reader.CreateKeyReaderWithAnchors();
                state.PendingComplexKey = ReadNode<YamlNode?>(ref keyReader, options);
            }
            else if (typeInfo.EffectiveUnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
            {
                ThrowHelper.ThrowYamlException_UnmappedYamlProperty(typeInfo.Type, raw);
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(raw);
            byte[] name = new byte[utf8.Length + 1];
            name[0] = 0xFF;
            utf8.CopyTo(name, 1);
            return name;
        }

        /// <summary>Reads the collection key on the current token into a <typeparamref name="TKey"/> key.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static TKey ReadKey<TKey>(YamlConverter<TKey> keyConverter, ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            Debug.Assert(reader.KeyKind == YamlKeyKind.Complex);
            string raw = GetRawText(ref reader);
            state.Current.YamlPropertyNameAsString = raw;
            if (!CanRead(keyConverter))
            {
                ThrowHelper.ThrowYamlException_ComplexKeyNotReadable(raw, typeof(TKey));
            }

            Utf8YamlReader keyReader = reader.CreateKeyReaderWithAnchors();
            TKey key = ReadNode<TKey>(ref keyReader, options);
            if (typeof(TKey) == typeof(object) && key is not null)
            {
                key = (TKey)InternObjectKey(ref state, key);
            }

            return key;
        }

        /// <summary>
        /// Reads a key node (a collection key, or the node an alias key names) through <typeparamref name="TKey"/>'s
        /// contract, with the operation's anchors (the reader carries them, M5b).
        /// </summary>
        internal static TKey ReadNode<TKey>(ref Utf8YamlReader keyReader, YamlSerializerOptions options)
        {
            var typeInfo = (YamlTypeInfo<TKey>)options.GetTypeInfoInternal(typeof(TKey));
            ReadStack nested = default;
            nested.Initialize(typeInfo);
            return typeInfo.Deserialize(ref keyReader, ref nested)!;
        }

        /// <summary>
        /// An <c>object</c> key that is a collection is a <see cref="YamlElement"/>, which has no value equality. Within
        /// one dictionary, a key whose canonical form was met before is the earlier key, so the dictionary's own
        /// equality and M5c's duplicate handling see a repeated key.
        /// </summary>
        private static object InternObjectKey(scoped ref ReadStack state, object key)
        {
            if (key is not YamlElement element || element.ValueKind is not (YamlValueKind.Object or YamlValueKind.Array))
            {
                return key;
            }

            int expanded = 0;
            string canonical = element.GetCanonicalForm(ref expanded);
            Dictionary<string, object> seen = state.ComplexObjectKeys(state.Current.ReturnValue);
            if (seen.TryGetValue(canonical, out object? earlier))
            {
                return earlier;
            }

            seen[canonical] = key;
            return key;
        }

        /// <summary>
        /// Whether <paramref name="converter"/> writes <paramref name="key"/> as a collection key: the library's object,
        /// collection and dictionary contracts (for an <c>object</c> key, its runtime type's). Value-strategy and custom
        /// converters write a scalar key through <c>WriteAsPropertyName</c>, as before M5e.
        /// </summary>
        internal static bool WritesComplexKey(YamlConverter converter, object? key, YamlSerializerOptions options)
        {
            if (converter.IsUntypedConverter && key is not null)
            {
                Type runtimeType = key.GetType();
                if (runtimeType == typeof(object))
                {
                    return true;
                }

                converter = options.GetConverterInternal(runtimeType);
            }

            // A key node read from YAML (an object key's YamlElement, a YamlNode key) is written as it is.
            if (key is YamlElement element)
            {
                return element.ValueKind is YamlValueKind.Object or YamlValueKind.Array;
            }

            if (key is YamlNode node)
            {
                return node is YamlObject or YamlArray;
            }

            return converter.IsInternalConverter
                && converter.ConverterStrategy is ConverterStrategy.Object or ConverterStrategy.Enumerable or ConverterStrategy.Dictionary;
        }

        /// <summary>Whether a key converter may write a collection key for some value (decided once per converter).</summary>
        internal static bool MayWriteComplexKey(YamlConverter converter, YamlSerializerOptions options) =>
            converter.IsUntypedConverter
            || converter.Type == typeof(YamlElement)
            || typeof(YamlNode).IsAssignableFrom(converter.Type)
            || WritesComplexKey(converter, key: null, options);

        /// <summary>
        /// Writes <paramref name="key"/> as an explicit key, through its converter and the operation's write state (so
        /// <c>Preserve</c> anchors a repeated key instance). A collection key is written whole: it does not suspend.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void WriteKey<TKey>(YamlConverter<TKey> keyConverter, YamlTypeInfo keyTypeInfo, Utf8YamlWriter writer, TKey key, YamlSerializerOptions options, ref WriteStack state)
        {
            if (!writer.IsBlockStyle)
            {
                ThrowHelper.ThrowNotSupportedException_YamlComplexKeyJsonCompatible();
            }

            writer.WriteStartPropertyKey();
            YamlPropertyInfo? propertyInfo = state.Current.YamlPropertyInfo;
            bool supportContinuation = state.SupportContinuation;
            state.Current.YamlPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
            state.SupportContinuation = false;
            try
            {
                bool success = keyConverter.TryWrite(writer, key, options, ref state);
                Debug.Assert(success);
            }
            finally
            {
                state.SupportContinuation = supportContinuation;
                state.Current.YamlPropertyInfo = propertyInfo;
            }

            writer.WriteEndPropertyKey();
        }
    }
}
