// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// The <c>%TAG</c> handles of one YAML document and their prefixes.
    /// Immutable: a directive creates a new table, so copies of the reader state (resumed readers, readers scoped to one
    /// value, the constructor replay path) can share one safely. Allocated only for documents that declare handles.
    /// A table is a list, newest entry first: a directive adds one entry without copying the others, and a lookup finds a
    /// redefined handle's latest prefix. It holds at most <see cref="MaxEntries"/> entries, so neither a directive nor a
    /// lookup costs more than that bound however many directives a hostile stream holds.
    /// </summary>
    internal sealed class YamlTagDirectives
    {
        internal static ReadOnlySpan<byte> SecondaryPrefix => "tag:yaml.org,2002:"u8;

        /// <summary>The most <c>%TAG</c> directives one table holds (a redefinition counts as one).</summary>
        internal const int MaxEntries = 256;

        private readonly byte[] _handle;
        private readonly byte[] _prefix;
        private readonly YamlTagDirectives? _next;
        private readonly int _count;

        private YamlTagDirectives(byte[] handle, byte[] prefix, YamlTagDirectives? next)
        {
            _handle = handle;
            _prefix = prefix;
            _next = next;
            _count = (next?._count ?? 0) + 1;
        }

        /// <summary>
        /// A table with <paramref name="handle"/> added, or redefined when it is already there; false, with no table, when
        /// <paramref name="table"/> already holds <see cref="MaxEntries"/> entries.
        /// </summary>
        internal static bool TryAdd(YamlTagDirectives? table, ReadOnlySpan<byte> handle, ReadOnlySpan<byte> prefix, out YamlTagDirectives? result)
        {
            if (table is not null && table._count >= MaxEntries)
            {
                result = null;
                return false;
            }

            result = new YamlTagDirectives(handle.ToArray(), prefix.ToArray(), table);
            return true;
        }

        internal bool TryGetPrefix(ReadOnlySpan<byte> handle, out ReadOnlySpan<byte> prefix)
        {
            for (YamlTagDirectives? entry = this; entry is not null; entry = entry._next)
            {
                if (handle.SequenceEqual(entry._handle))
                {
                    prefix = entry._prefix;
                    return true;
                }
            }

            prefix = default;
            return false;
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
