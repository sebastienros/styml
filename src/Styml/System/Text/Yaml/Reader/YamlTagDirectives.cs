// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// The <c>%TAG</c> handles of one YAML document and their prefixes, in a hash table so that a header with many
    /// directives reads in linear time. Mutable: each directive header starts from no table (the reader drops the
    /// previous document's before its first directive) and adds to it in place, and the table is read-only once the
    /// document starts. Copies of the reader state (resumed readers, readers scoped to one value, the constructor
    /// replay path) can share one: a state copied or restored inside the header can see handles from directive lines it
    /// has not consumed yet, but it reads those same lines again and adds the same handles in the same order.
    /// Allocated only for documents that declare handles.
    /// </summary>
    internal sealed class YamlTagDirectives
    {
        internal static ReadOnlySpan<byte> SecondaryPrefix => "tag:yaml.org,2002:"u8;

        private struct Entry
        {
            public int HashCode;
            public int Next;
            public byte[] Handle;
            public byte[] Prefix;
        }

        private int[] _buckets;
        private Entry[] _entries;
        private int _count;

        private YamlTagDirectives()
        {
            _buckets = new int[4];
            _entries = new Entry[4];
        }

        /// <summary>
        /// Adds <paramref name="handle"/> to <paramref name="table"/> in place, or redefines it when it is already there;
        /// a new table when <paramref name="table"/> is null. Called only while reading a directive header.
        /// </summary>
        internal static YamlTagDirectives With(YamlTagDirectives? table, ReadOnlySpan<byte> handle, ReadOnlySpan<byte> prefix)
        {
            table ??= new YamlTagDirectives();
            table.AddOrUpdate(handle, prefix);
            return table;
        }

        private void AddOrUpdate(ReadOnlySpan<byte> handle, ReadOnlySpan<byte> prefix)
        {
            int hashCode = Marvin.ComputeHash32(handle, Marvin.DefaultSeed);
            int existing = FindEntry(handle, hashCode);
            if (existing >= 0)
            {
                _entries[existing].Prefix = prefix.ToArray();
                return;
            }

            if (_count == _entries.Length)
            {
                Resize();
            }

            int bucketIndex = hashCode & (_buckets.Length - 1);
            int index = _count++;
            ref Entry entry = ref _entries[index];
            entry.HashCode = hashCode;
            entry.Next = _buckets[bucketIndex] - 1;
            entry.Handle = handle.ToArray();
            entry.Prefix = prefix.ToArray();
            _buckets[bucketIndex] = index + 1;
        }

        /// <summary>The index of the entry for <paramref name="handle"/>, or -1.</summary>
        private int FindEntry(ReadOnlySpan<byte> handle, int hashCode)
        {
            for (int i = _buckets[hashCode & (_buckets.Length - 1)] - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hashCode && handle.SequenceEqual(_entries[i].Handle))
                {
                    return i;
                }
            }
            return -1;
        }

        private void Resize()
        {
            int newSize = _entries.Length * 2;
            int[] newBuckets = new int[newSize];
            Entry[] newEntries = new Entry[newSize];
            Array.Copy(_entries, newEntries, _count);

            int mask = newSize - 1;
            for (int i = 0; i < _count; i++)
            {
                int bucketIndex = newEntries[i].HashCode & mask;
                newEntries[i].Next = newBuckets[bucketIndex] - 1;
                newBuckets[bucketIndex] = i + 1;
            }

            _buckets = newBuckets;
            _entries = newEntries;
        }

        internal bool TryGetPrefix(ReadOnlySpan<byte> handle, out ReadOnlySpan<byte> prefix)
        {
            int index = FindEntry(handle, Marvin.ComputeHash32(handle, Marvin.DefaultSeed));
            prefix = index >= 0 ? _entries[index].Prefix : default;
            return index >= 0;
        }

        /// <summary>The prefix of a handle: declared, or the default of '!' and '!!'; false for an undeclared named handle.</summary>
        internal static bool TryGetPrefix(YamlTagDirectives? table, ReadOnlySpan<byte> handle, out ReadOnlySpan<byte> prefix)
        {
            if (table is not null && table.TryGetPrefix(handle, out prefix))
            {
                return true;
            }
            if (handle.Length == 1)
            {
                prefix = "!"u8;
                return true;
            }
            if (handle.Length == 2)
            {
                prefix = SecondaryPrefix;
                return true;
            }
            prefix = default;
            return false;
        }

        /// <summary>
        /// Splits a tag as written into its handle and suffix: <c>!!str</c> is <c>!!</c> + <c>str</c>, <c>!e!foo</c> is
        /// <c>!e!</c> + <c>foo</c>, <c>!local</c> is <c>!</c> + <c>local</c>. False for a verbatim (<c>!&lt;...&gt;</c>) or
        /// non-specific (<c>!</c>) tag.
        /// </summary>
        internal static bool TrySplitShorthand(ReadOnlySpan<byte> written, out ReadOnlySpan<byte> handle, out ReadOnlySpan<byte> suffix)
        {
            Debug.Assert(written.Length > 0 && written[0] == (byte)'!');
            handle = default;
            suffix = default;
            if (written.Length == 1 || written[1] == (byte)'<')
            {
                return false;
            }

            int second = written.Slice(1).IndexOf((byte)'!');
            int handleLength = second < 0 ? 1 : second + 2;
            handle = written.Slice(0, handleLength);
            suffix = written.Slice(handleLength);
            return true;
        }

        /// <summary>
        /// Writes the expanded form of a tag as written (YAML 1.2 section 6.8.2): the handle replaced by its prefix and
        /// the suffix percent-decoded; a verbatim tag gives its content, the non-specific '!' stays '!'.
        /// </summary>
        /// <returns>The number of bytes the expanded tag has; it was written only if it fits in <paramref name="destination"/>.</returns>
        internal static int Expand(YamlTagDirectives? table, ReadOnlySpan<byte> written, Span<byte> destination)
        {
            if (written.Length == 1)
            {
                return Write(written, destination, 0);
            }
            if (written[1] == (byte)'<')
            {
                return Write(written.Slice(2, written.Length - 3), destination, 0);
            }

            bool split = TrySplitShorthand(written, out ReadOnlySpan<byte> handle, out ReadOnlySpan<byte> suffix);
            Debug.Assert(split);
            if (!TryGetPrefix(table, handle, out ReadOnlySpan<byte> prefix))
            {
                // Undeclared handles are rejected when the tag is read; keep the tag as written otherwise.
                return Write(written, destination, 0);
            }

            int length = Write(prefix, destination, 0);
            for (int i = 0; i < suffix.Length; i++)
            {
                byte b = suffix[i];
                if (b == (byte)'%' && i + 2 < suffix.Length && TryHex(suffix[i + 1], out int high) && TryHex(suffix[i + 2], out int low))
                {
                    b = (byte)((high << 4) | low);
                    i += 2;
                }
                if (length < destination.Length)
                {
                    destination[length] = b;
                }
                length++;
            }
            return length;
        }

        private static int Write(ReadOnlySpan<byte> source, Span<byte> destination, int offset)
        {
            if (offset + source.Length <= destination.Length)
            {
                source.CopyTo(destination.Slice(offset));
            }
            return offset + source.Length;
        }

        private static bool TryHex(byte b, out int value)
        {
            value = b switch
            {
                >= (byte)'0' and <= (byte)'9' => b - '0',
                >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
                _ => -1,
            };
            return value >= 0;
        }
    }
}
