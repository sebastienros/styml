// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Multi-segment sequences for the M3c oracles. Every segment is a
    /// slice of one backing array, with a poison byte between segments, so a test can tell whether a span the reader
    /// returns is a slice of exactly one segment of the input (<see cref="Sequence.IsSliceOfOneSegment"/>).
    /// </summary>
    internal static class SequenceFactory
    {
        internal sealed class Sequence
        {
            private readonly byte[] _backing;
            private readonly int[] _starts;    // start of each segment in the backing array
            private readonly int[] _lengths;

            public ReadOnlySequence<byte> Value { get; }

            public int SegmentCount => _starts.Length;

            internal Sequence(byte[] backing, int[] starts, int[] lengths, ReadOnlySequence<byte> value)
            {
                _backing = backing;
                _starts = starts;
                _lengths = lengths;
                Value = value;
            }

            /// <summary>Whether <paramref name="span"/> lies inside the input's memory but crosses a segment boundary.</summary>
            public bool StraddlesSegments(ReadOnlySpan<byte> span)
            {
                if (span.IsEmpty)
                {
                    return false;
                }
                long offset = (long)Unsafe.ByteOffset(ref _backing[0], ref MemoryMarshal.GetReference(span));
                return offset >= 0 && offset < _backing.Length && !IsSliceOfOneSegment(span);
            }

            /// <summary>Whether <paramref name="span"/> lies inside one segment's bytes (an empty span always does).</summary>
            public bool IsSliceOfOneSegment(ReadOnlySpan<byte> span)
            {
                if (span.IsEmpty)
                {
                    return true;
                }
                long offset = (long)Unsafe.ByteOffset(ref _backing[0], ref MemoryMarshal.GetReference(span));
                if (offset < 0 || offset + span.Length > _backing.Length)
                {
                    return false;
                }
                int index = Array.BinarySearch(_starts, (int)offset);
                if (index < 0)
                {
                    index = ~index - 1;
                }
                return index >= 0 && offset + span.Length <= _starts[index] + _lengths[index];
            }
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory, RunningIndex + Memory.Length);
                Next = next;
                return next;
            }
        }

        /// <summary>Segments of the given lengths (zero-length segments allowed), covering <paramref name="data"/>.</summary>
        public static Sequence Create(byte[] data, IReadOnlyList<int> lengths)
        {
            int total = 0;
            foreach (int length in lengths)
            {
                total += length;
            }
            if (total != data.Length)
            {
                throw new ArgumentException("lengths do not cover the data");
            }

            byte[] backing = new byte[data.Length + lengths.Count + 1];
            backing.AsSpan().Fill((byte)'ÿ'); // never valid UTF-8, never read by a correct reader
            int[] starts = new int[lengths.Count];
            int[] sizes = new int[lengths.Count];
            int at = 1, from = 0;
            Segment? head = null, tail = null;
            for (int i = 0; i < lengths.Count; i++)
            {
                int length = lengths[i];
                data.AsSpan(from, length).CopyTo(backing.AsSpan(at));
                starts[i] = at;
                sizes[i] = length;
                ReadOnlyMemory<byte> memory = backing.AsMemory(at, length);
                if (head is null)
                {
                    head = tail = new Segment(memory, 0);
                }
                else
                {
                    tail = tail!.Append(memory);
                }
                at += length + 1;
                from += length;
            }
            if (head is null)
            {
                head = tail = new Segment(ReadOnlyMemory<byte>.Empty, 0);
                starts = new[] { 1 };
                sizes = new[] { 0 };
            }
            return new Sequence(backing, starts, sizes, new ReadOnlySequence<byte>(head, 0, tail!, tail!.Memory.Length));
        }

        /// <summary>Segments of <paramref name="size"/> bytes (the last one shorter), optionally with an empty segment after each.</summary>
        public static Sequence Uniform(byte[] data, int size, bool emptyBetween = false)
        {
            var lengths = new List<int>();
            for (int offset = 0; offset < data.Length; offset += size)
            {
                lengths.Add(Math.Min(size, data.Length - offset));
                if (emptyBetween)
                {
                    lengths.Add(0);
                }
            }
            return Create(data, lengths);
        }

        /// <summary>Two segments, split at <paramref name="split"/> (either may be empty).</summary>
        public static Sequence Split(byte[] data, int split) => Create(data, new[] { split, data.Length - split });

        /// <summary>Segments of seeded random lengths from 1 to <paramref name="maxSize"/>.</summary>
        public static Sequence Random(byte[] data, int seed, int maxSize = 64)
        {
            var random = new Random(seed);
            var lengths = new List<int>();
            for (int offset = 0; offset < data.Length;)
            {
                int length = Math.Min(random.Next(1, maxSize + 1), data.Length - offset);
                lengths.Add(length);
                offset += length;
            }
            return Create(data, lengths);
        }
    }
}
