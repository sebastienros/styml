// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// M5d: the tags a hierarchy or the options'
    /// <c>TagMappings</c> configure. A configured tag is a local tag (<c>!dog</c>) or a global tag URI
    /// (<c>tag:example.com,2000:dog</c>). It is compared with a node's expanded tag, which is the configured text in
    /// both forms, and written as is (local) or verbatim (<c>!&lt;tag:…&gt;</c>, global). Core tags, <c>!</c> and
    /// <c>?</c> are reserved. A tag never names a CLR type: it selects one only through these explicit lists.
    /// </summary>
    internal static class YamlTags
    {
        internal const string CorePrefix = YamlConfiguredTag.CorePrefix;

        /// <summary>
        /// Checks a configured tag and returns the text it is written as, or null with the reason in
        /// <paramref name="error"/> (the rules are YamlConfiguredTag's, which the source generator shares).
        /// </summary>
        internal static string? GetWrittenForm(string tag, out string? error)
        {
            error = null;
            if (YamlConfiguredTag.IsReserved(tag))
            {
                error = SR.Format(SR.YamlTagReserved, tag);
                return null;
            }

            if (!YamlConfiguredTag.IsValid(tag))
            {
                error = SR.Format(SR.YamlTagInvalid, tag);
                return null;
            }

            string written = YamlConfiguredTag.GetWrittenForm(tag);
            Debug.Assert(YamlWriterHelper.IsValidTag(Encoding.UTF8.GetBytes(written)));
            return written;
        }

        /// <summary>
        /// The expanded form the reader gives for a configured tag: a local tag's suffix percent-decoded, a global tag
        /// as is.
        /// </summary>
        internal static byte[] GetExpandedUtf8(string tag) => YamlConfiguredTag.GetExpandedUtf8(tag);

        /// <summary>
        /// Copies the current node's expanded tag into <paramref name="buffer"/>: the span, empty when the node has no tag
        /// or a core tag (never a discriminator), and true when a tag that is longer than the buffer was skipped.
        /// </summary>
        internal static ReadOnlySpan<byte> ExpandedApplicationTag(scoped in Utf8YamlReader reader, Span<byte> buffer, out byte[]? rented)
        {
            rented = null;
            if (reader.Tag.IsEmpty)
            {
                return default;
            }

            if (!reader.TryCopyExpandedTag(buffer, out int written))
            {
                string expanded = reader.GetExpandedTag()!;
                rented = Encoding.UTF8.GetBytes(expanded);
                buffer = rented;
                written = rented.Length;
            }

            ReadOnlySpan<byte> tag = buffer.Slice(0, written);
            return tag.StartsWith("tag:yaml.org,2002:"u8) || tag.SequenceEqual("!"u8) ? default : tag;
        }
    }

    /// <summary>The options' <c>TagMappings</c>: checked on every change, frozen with the options.</summary>
    internal sealed class YamlTagMappingDictionary : IDictionary<string, Type>
    {
        private readonly Dictionary<string, Type> _entries = new(StringComparer.Ordinal);
        private readonly YamlSerializerOptions _owner;

        internal YamlTagMappingDictionary(YamlSerializerOptions owner) => _owner = owner;

        internal YamlTagMappingDictionary(YamlSerializerOptions owner, YamlTagMappingDictionary source)
            : this(owner)
        {
            foreach (KeyValuePair<string, Type> entry in source._entries)
            {
                _entries.Add(entry.Key, entry.Value);
            }
        }

        internal IReadOnlyDictionary<string, Type> Entries => _entries;

        public Type this[string key]
        {
            get => _entries[key];
            set
            {
                _owner.VerifyMutableForTagMappings();
                Validate(key, value, replacing: _entries.ContainsKey(key));
                _entries[key] = value;
            }
        }

        public ICollection<string> Keys => _entries.Keys;

        public ICollection<Type> Values => _entries.Values;

        public int Count => _entries.Count;

        public bool IsReadOnly => _owner.IsReadOnly;

        public void Add(string key, Type value)
        {
            _owner.VerifyMutableForTagMappings();
            Validate(key, value, replacing: false);
            if (_entries.ContainsKey(key))
            {
                ThrowHelper.ThrowArgumentException(SR.Format(SR.YamlTagMappedTwice, key));
            }

            _entries.Add(key, value);
        }

        public void Add(KeyValuePair<string, Type> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            _owner.VerifyMutableForTagMappings();
            _entries.Clear();
        }

        public bool Contains(KeyValuePair<string, Type> item) => _entries.TryGetValue(item.Key, out Type? type) && type == item.Value;

        public bool ContainsKey(string key) => _entries.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, Type>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, Type>>)_entries).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, Type>> GetEnumerator() => _entries.GetEnumerator();

        public bool Remove(string key)
        {
            _owner.VerifyMutableForTagMappings();
            return _entries.Remove(key);
        }

        public bool Remove(KeyValuePair<string, Type> item) => Contains(item) && Remove(item.Key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out Type value) => _entries.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void Validate(string tag, Type type, bool replacing)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(type);
            if (YamlTags.GetWrittenForm(tag, out string? error) is null)
            {
                ThrowHelper.ThrowArgumentException(error!);
            }

            if (type.ContainsGenericParameters || type.IsAbstract || type.IsInterface || Nullable.GetUnderlyingType(type) is not null
                || type.IsPointer || type.IsByRef || type == typeof(void))
            {
                ThrowHelper.ThrowArgumentException(SR.Format(SR.YamlTagMappingTypeNotSupported, tag, type));
            }

            foreach (KeyValuePair<string, Type> entry in _entries)
            {
                if (entry.Value == type && !(replacing && entry.Key == tag))
                {
                    // The write direction needs one tag per type.
                    ThrowHelper.ThrowArgumentException(SR.Format(SR.YamlTagMappingTypeMappedTwice, type, entry.Key, tag));
                }
            }
        }

        internal bool ContentEquals(YamlTagMappingDictionary? other)
        {
            int count = other?.Count ?? 0;
            if (count != Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, Type> entry in _entries)
            {
                if (!other!._entries.TryGetValue(entry.Key, out Type? type) || type != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }

        internal int ContentHashCode()
        {
            int hash = 0;
            foreach (KeyValuePair<string, Type> entry in _entries)
            {
                // Order-insensitive.
                hash += StringComparer.Ordinal.GetHashCode(entry.Key) ^ entry.Value.GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>The frozen lookup of <c>TagMappings</c>: expanded tags as UTF-8, and each type's written tag.</summary>
    internal sealed class YamlTagMappingTable
    {
        private readonly (byte[] Expanded, Type Type)[] _byTag;
        private readonly Dictionary<Type, string> _writtenByType = new();

        internal YamlTagMappingTable(IReadOnlyDictionary<string, Type> entries)
        {
            _byTag = new (byte[], Type)[entries.Count];
            int i = 0;
            foreach (KeyValuePair<string, Type> entry in entries)
            {
                _byTag[i++] = (YamlTags.GetExpandedUtf8(entry.Key), entry.Value);
                _writtenByType[entry.Value] = YamlTags.GetWrittenForm(entry.Key, out _)!;
            }
        }

        internal IEnumerable<(string Written, byte[] Expanded, Type Type)> Entries
        {
            get
            {
                foreach ((byte[] expanded, Type type) in _byTag)
                {
                    yield return (_writtenByType[type], expanded, type);
                }
            }
        }

        /// <summary>The type an expanded tag maps, or null; allocates nothing.</summary>
        internal Type? Find(ReadOnlySpan<byte> expandedTag)
        {
            foreach ((byte[] expanded, Type type) in _byTag)
            {
                if (expandedTag.SequenceEqual(expanded))
                {
                    return type;
                }
            }

            return null;
        }

        internal string? WrittenTagOf(Type type) => _writtenByType.TryGetValue(type, out string? tag) ? tag : null;
    }
}
