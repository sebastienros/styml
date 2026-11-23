// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// The anchors one serializer operation has read, with a copy of the
    /// source from the first anchor on, so that an alias can replay its anchored node after the reader has moved past
    /// it, or after a stream has dropped those bytes. Created by the reader at the first anchor of an operation whose
    /// reader options have the internal RecordAnchors flag; it lives as long as the operation's readers.
    /// </summary>
    internal sealed class YamlAnchorTable
    {
        private YamlAnchorEntry[] _entries = new YamlAnchorEntry[4];
        private int _count;
        private Dictionary<string, int>? _latestByName;

        // The copy of the source: _store[0] is the byte at absolute position _storeStart.
        private byte[] _store = Array.Empty<byte>();
        private long _storeStart = -1;
        private int _storeLength;

        // Entries before this absolute position belong to an earlier document and never resolve.
        private long _documentStart;

        /// <summary>The nodes that aliases have copied in this operation.</summary>
        internal int Expanded;

        /// <summary>The furthest source position the operation has resolved an alias or recorded an anchor at.</summary>
        internal long Furthest;

        /// <summary>
        /// True while the positions are relative to the buffer the reader created the table in: always for in-memory input
        /// (whose buffer starts at 0), and for a stream until its driver rebases the table at the end of that buffer.
        /// </summary>
        internal bool Unrebased = true;

        internal long StoreEnd => _storeStart + _storeLength;

        // The anchored nodes the serializer is reading, by the depth of the frame that reads each (innermost last).
        private (int Depth, YamlAnchorEntry Entry)[] _open = Array.Empty<(int, YamlAnchorEntry)>();
        private int _openCount;

        internal void PushOpen(int depth, YamlAnchorEntry entry)
        {
            if (_openCount == _open.Length)
            {
                Array.Resize(ref _open, Math.Max(4, _openCount * 2));
            }

            _open[_openCount++] = (depth, entry);
        }

        internal YamlAnchorEntry? PeekOpen(int depth) =>
            _openCount > 0 && _open[_openCount - 1].Depth == depth ? _open[_openCount - 1].Entry : null;

        internal YamlAnchorEntry? PopOpen(int depth)
        {
            YamlAnchorEntry? entry = PeekOpen(depth);
            if (entry is not null)
            {
                _open[--_openCount] = default;
            }

            return entry;
        }

        /// <summary>Records an anchor met by a reader. Recording a position already in the table does nothing.</summary>
        internal void Record(string name, long position, long line, long column, int parentIndent, YamlTagDirectives? tagDirectives, int fixedLength, long limit = 0)
        {
            if (position < _documentStart)
            {
                return;
            }

            if (_count > 0 && position <= _entries[_count - 1].Position)
            {
                // A rollback (partial data, read-ahead, a checkpoint) or a replay meets the same anchor again.
                if (IndexOf(position) >= 0)
                {
                    return;
                }

                Debug.Fail("Anchors are met in source order.");
            }

            if (_count == _entries.Length)
            {
                Array.Resize(ref _entries, _count * 2);
            }

            _latestByName ??= new Dictionary<string, int>(StringComparer.Ordinal);
            int previous = _latestByName.TryGetValue(name, out int p) ? p : -1;
            _entries[_count] = new YamlAnchorEntry(name, position, line, column, parentIndent, tagDirectives, previous) { FixedLength = fixedLength, Limit = limit };
            _latestByName[name] = _count;
            _count++;

            if (position > Furthest)
            {
                Furthest = position;
            }
        }

        private int IndexOf(long position)
        {
            int lo = 0, hi = _count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                long p = _entries[mid].Position;
                if (p == position)
                {
                    return mid;
                }

                if (p < position)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return -1;
        }

        /// <summary>The entry recorded for the anchor at <paramref name="position"/>, or null.</summary>
        internal YamlAnchorEntry? FindAt(long position)
        {
            int index = IndexOf(position);
            return index < 0 ? null : _entries[index];
        }

        /// <summary>The latest anchor named <paramref name="name"/> before <paramref name="position"/>, or null (YAML 1.2, 3.2.2.2).</summary>
        internal YamlAnchorEntry? Find(ReadOnlySpan<byte> name, long position)
        {
            if (_latestByName is null || !_latestByName.TryGetValue(YamlReaderHelper.TranscodeHelper(name), out int index))
            {
                return null;
            }

            while (index >= 0)
            {
                YamlAnchorEntry entry = _entries[index];
                if (entry.Position < position)
                {
                    return entry.Position >= _documentStart ? entry : null;
                }

                index = entry.PreviousSameName;
            }

            return null;
        }

        /// <summary>A new YAML document starts at <paramref name="position"/>: earlier anchors no longer resolve, and their bytes are released.</summary>
        internal void StartDocument(long position)
        {
            if (position <= _documentStart)
            {
                return;
            }

            _documentStart = position;
            _count = 0;
            _latestByName?.Clear();
            _storeStart = -1;
            _storeLength = 0;
            _store = Array.Empty<byte>();
        }

        /// <summary>Copies <paramref name="bytes"/>, which start at absolute position <paramref name="start"/>, into the store, from the first anchor on.</summary>
        internal void Store(ReadOnlySpan<byte> bytes, long start)
        {
            if (_count == 0)
            {
                return;
            }

            if (_storeStart < 0)
            {
                _storeStart = _entries[0].Position;
            }

            long from = Math.Max(StoreEnd, start);
            long end = start + bytes.Length;
            if (end <= from)
            {
                return;
            }

            ReadOnlySpan<byte> add = bytes.Slice(checked((int)(from - start)));
            EnsureCapacity(_storeLength + add.Length);
            add.CopyTo(_store.AsSpan(_storeLength));
            _storeLength += add.Length;
        }

        /// <inheritdoc cref="Store(ReadOnlySpan{byte}, long)"/>
        internal void Store(ReadOnlySequence<byte> bytes, long start)
        {
            if (bytes.IsSingleSegment)
            {
                Store(bytes.First.Span, start);
                return;
            }

            foreach (ReadOnlyMemory<byte> segment in bytes)
            {
                Store(segment.Span, start);
                start += segment.Length;
            }
        }

        private void EnsureCapacity(int length)
        {
            if (length > _store.Length)
            {
                // A plain array, not a pooled one: the operation may hand out values that point into it.
                Array.Resize(ref _store, Math.Max(length, Math.Max(256, _store.Length * 2)));
            }
        }

        /// <summary>The stored bytes from <paramref name="position"/> to the end of the store.</summary>
        internal ReadOnlySpan<byte> GetStored(long position)
        {
            Debug.Assert(_storeStart >= 0 && position >= _storeStart && position <= StoreEnd);
            return _store.AsSpan(checked((int)(position - _storeStart)), checked((int)(StoreEnd - position)));
        }

        /// <summary>
        /// Moves every position by <paramref name="delta"/>: a stream's driver rebases a table that a reader created in
        /// one of its buffers, whose positions were relative to that buffer.
        /// </summary>
        internal void Shift(long delta)
        {
            for (int i = 0; i < _count; i++)
            {
                _entries[i].Shift(delta);
            }

            if (_storeStart >= 0)
            {
                _storeStart += delta;
            }

            Furthest += delta;
            _documentStart += delta;
        }

        internal bool IsStored(long position) => _storeStart >= 0 && position >= _storeStart && position < StoreEnd;
    }

    /// <summary>One anchor of a <see cref="YamlAnchorTable"/>.</summary>
    internal sealed class YamlAnchorEntry
    {
        internal YamlAnchorEntry(string name, long position, long line, long column, int parentIndent, YamlTagDirectives? tagDirectives, int previousSameName)
        {
            Name = name;
            Position = position;
            Line = line;
            Column = column;
            ParentIndent = parentIndent;
            TagDirectives = tagDirectives;
            PreviousSameName = previousSameName;
        }

        internal string Name { get; }

        /// <summary>The absolute source position of the anchored node's first byte, its properties included.</summary>
        internal long Position { get; private set; }

        internal void Shift(long delta) => Position += delta;

        internal long Line { get; }

        internal long Column { get; }

        internal int ParentIndent { get; }

        internal YamlTagDirectives? TagDirectives { get; }

        internal int PreviousSameName { get; }

        /// <summary>
        /// The instance read at the anchored site, when a built-in object or collection converter made it.
        /// Never a scalar or a value type.
        /// </summary>
        internal object? Instance;

        /// <summary>How many reads of the anchored node (the original read or a replay) are in progress.</summary>
        internal int OpenCount;

        /// <summary>The length of the anchored node in bytes, and its node count, once a replay has measured them; -1 before.</summary>
        internal int Length = -1;

        /// <summary>
        /// M5e: for an anchor met by a reader over a slice (a collection key's reader, a replay), the absolute end of that
        /// slice, which a replay does not read past: a key's node read alone from the source would read on into its
        /// mapping (<c>&amp;k [a, b]: 1</c> is a mapping); 0 for the operation's own reader.
        /// </summary>
        internal long Limit;

        internal int NodeCount;

        /// <summary>
        /// For an anchored scalar, or an anchored scalar key, the length of its bytes (properties and quotes included) as
        /// the reader read them; 0 for a collection, whose end a replay measures.
        /// </summary>
        internal int FixedLength;

        /// <summary>The boxed <see cref="YamlElement"/> of the anchored node, once an object site has read it.</summary>
        internal object? Element;
    }

    /// <summary>
    /// What a reader needs to record anchors and resolve aliases: the operation's table, and the absolute source position
    /// of the reader's first byte. Readers over a slice of the input get their own context with the slice's base.
    /// </summary>
    internal sealed class YamlAnchorContext
    {
        internal YamlAnchorContext(YamlAnchorTable table, long @base, bool isSource)
        {
            Table = table;
            Base = @base;
            IsSource = isSource;
        }

        internal YamlAnchorTable Table { get; }

        /// <summary>The absolute source position of the reader's first byte. A stream's readers move it at each buffer.</summary>
        internal long Base;

        /// <summary>
        /// True for the reader over the operation's own input (a span, a sequence, a stream's buffers), whose bytes go into
        /// the store; false for readers over a slice (scoped values, replays, re-read constructor arguments).
        /// </summary>
        internal bool IsSource { get; }

        internal YamlAnchorContext ForSlice(long offset) => new(Table, Base + offset, isSource: false);
    }
}
