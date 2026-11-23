// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    // Multi-segment input under the YAML grammar (M3c). The grammar reads one
    // contiguous window: the memory of one segment, or a stitched copy when a token or its lookahead crosses a segment
    // boundary. When the grammar needs bytes beyond the window and the sequence has them, the read loop forms a longer
    // window starting at the restored token start and reads the token again (the in-place resumption of ReadYaml).
    //
    // The window is carried by upstream's multi-segment fields: _totalConsumed is the absolute offset of _buffer[0] in
    // the sequence, _currentPosition its position, and _isLastSegment whether the window reaches the end of the
    // sequence (so IsLastSpan keeps its meaning). A stitched array is never written after its window is formed, since
    // copies of the reader may still read it.
    public ref partial struct Utf8YamlReader
    {
        /// <summary>The minimum growth of a stitched window beyond the bytes the failed attempt saw.</summary>
        private const int YamlWindowMinGrowth = 256;

        private bool _yamlWindowIsCopy;

        // The window offset of the current token's value bytes (ValueSpan, or the bytes ValueSequence holds).
        private int _yamlValueWindowStart;

        private long _yamlStitchedBytes;

        /// <summary>The bytes copied into stitched windows by this reader (for tests).</summary>
        internal readonly long YamlStitchedBytes => _yamlStitchedBytes;

        private readonly bool YamlWindowReachesSequenceEnd => _totalConsumed + _buffer.Length >= _sequence.Length;

        /// <summary>
        /// The current value's bytes: <see cref="ValueSpan"/>, or the window's copy of what <see cref="ValueSequence"/>
        /// holds. The value is whole in the window by construction, so no copy is made. YAML grammar only.
        /// </summary>
        internal readonly ReadOnlySpan<byte> YamlValueBytes =>
            HasValueSequence ? _buffer.Slice(_yamlValueWindowStart, checked((int)ValueSequence.Length)) : ValueSpan;

        /// <summary>The value bytes for parsing: the value span, or the window's bytes for a value across segments.</summary>
        internal readonly ReadOnlySpan<byte> ValueBytesOrSequenceCopy =>
            !HasValueSequence ? ValueSpan : YamlValueBytes;

        private bool YamlReadMultiSegment()
        {
            while (true)
            {
                if (HasValueSequence)
                {
                    // Only after a token that set it: ValueSequence holds object references, and writing it on every
                    // token costs two write barriers.
                    HasValueSequence = false;
                    ValueSequence = ReadOnlySequence<byte>.Empty;
                }

                YamlStep step = YamlReadAttempt();
                if (step == YamlStep.Token)
                {
                    if (_yamlWindowIsCopy)
                    {
                        YamlFinishCopyToken();
                    }
                    else
                    {
                        // A window that is one segment's memory: every token lies in that segment.
                        TokenStartIndex += _totalConsumed;
                    }

                    if (_yamlAnchorLength != 0 && _readerOptions.RecordAnchors)
                    {
                        YamlRecordAnchor();
                    }

                    if (_yamlIsComplexKey && _readerOptions.RecordAnchors)
                    {
                        YamlRecordKeyAnchors();
                    }

                    return true;
                }
                if (step != YamlStep.NeedMoreData || !YamlMoveWindow())
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Forms the next window, from the first unconsumed byte, at least twice as long as what the failed attempt saw
        /// (so re-reading a long token is linear in total). Returns false when the window already reaches the end of
        /// the sequence.
        /// </summary>
        private bool YamlMoveWindow()
        {
            long sequenceLength = _sequence.Length;
            if (_totalConsumed + _buffer.Length >= sequenceLength)
            {
                // The window already reaches the end. The constructor leaves _isLastSegment false when only empty
                // segments follow; with final data the window is then the last span, and the token is read again.
                if (_isFinalBlock && !_isLastSegment)
                {
                    _isLastSegment = true;
                    return true;
                }
                return false;
            }

            int consumed = _consumed;
            long start = _totalConsumed + consumed;
            long remaining = sequenceLength - start;
            long leftover = _buffer.Length - consumed;
            long needed = leftover == 0 ? 1 : Math.Max(2 * leftover, leftover + YamlWindowMinGrowth);
            if (needed > remaining)
            {
                needed = remaining;
            }

            // The first non-empty memory at or after the start.
            SequencePosition position = _sequence.GetPosition(consumed, _currentPosition);
            SequencePosition cursor = position;
            ReadOnlyMemory<byte> memory = default;
            while (true)
            {
                SequencePosition at = cursor;
                bool found = _sequence.TryGet(ref cursor, out ReadOnlyMemory<byte> next);
                Debug.Assert(found, "remaining > 0, so a segment follows");
                if (!next.IsEmpty)
                {
                    memory = next;
                    position = at;
                    break;
                }
            }

            if (memory.Length >= needed)
            {
                _buffer = memory.Span;
                _yamlWindowIsCopy = false;
            }
            else
            {
                int length = checked((int)Math.Min(remaining, Math.Max(needed, YamlWindowMinGrowth)));
                byte[] copy = new byte[length];
                _sequence.Slice(position, length).CopyTo(copy);
                _buffer = copy;
                _yamlWindowIsCopy = true;
                _yamlStitchedBytes += length;
            }

            YamlRebaseProbe(consumed);
            _totalConsumed = start;
            _currentPosition = position;
            _consumed = 0;
            _isLastSegment = _isFinalBlock && start + _buffer.Length >= sequenceLength;
            return true;
        }

        /// <summary>
        /// Moves the per-copy probe state (window offsets) by the window's shift. The shared cache holds absolute
        /// offsets and is not touched.
        /// </summary>
        private void YamlRebaseProbe(int shift)
        {
            if (_yamlNoKeyTo - shift <= 0)
            {
                _yamlNoKeyFrom = _yamlNoKeyTo = 0;
            }
            else
            {
                _yamlNoKeyFrom = Math.Max(0, _yamlNoKeyFrom - shift);
                _yamlNoKeyTo -= shift;
            }

            if (_yamlCandidateAt - shift <= 0)
            {
                _yamlCandidateFrom = _yamlCandidateAt = 0;
            }
            else
            {
                _yamlCandidateFrom = Math.Max(0, _yamlCandidateFrom - shift);
                _yamlCandidateAt -= shift;
            }
        }

        /// <summary>
        /// After a token from a stitched window: makes <see cref="TokenStartIndex"/> absolute and applies the
        /// multi-segment contract: <see cref="HasValueSequence"/> by upstream's lexing rule, and
        /// otherwise <see cref="ValueSpan"/> as a slice of the input segment.
        /// </summary>
        private void YamlFinishCopyToken()
        {
            int tokenStart = checked((int)TokenStartIndex);
            TokenStartIndex += _totalConsumed;
            if (!YamlTokenHasValue && ValueSpan.IsEmpty)
            {
                // Zero-width tokens; flow collection tokens carry their one byte in ValueSpan and are mapped below.
                return;
            }

            int valueStart = _yamlValueWindowStart;
            int valueEnd = valueStart + ValueSpan.Length;
            int extentStart = valueStart;
            int extentEnd = valueEnd;
            bool terminator;
            switch (_yamlStyle)
            {
                case YamlScalarStyle.SingleQuoted:
                case YamlScalarStyle.DoubleQuoted:
                    // From the opening quote through the closing quote, as upstream's strings.
                    extentStart = valueStart - 1;
                    extentEnd = valueEnd + 1;
                    terminator = true;
                    break;
                case YamlScalarStyle.Plain:
                    // Through the byte that ends the scalar, as upstream's numbers; true, false and null are their own
                    // bytes, as upstream's literals.
                    terminator = _tokenType is not (YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null) && valueEnd < _buffer.Length;
                    if (terminator)
                    {
                        extentEnd = valueEnd + 1;
                    }
                    break;
                case YamlScalarStyle.Literal:
                case YamlScalarStyle.Folded:
                    // From the header through the last content byte.
                    extentStart = Math.Min(tokenStart, valueStart);
                    terminator = false;
                    break;
                default:
                    if (_yamlIsAlias || _tokenType == YamlTokenType.Alias)
                    {
                        // The '*' through the byte after the name.
                        extentStart = valueStart - 1;
                        terminator = valueEnd < _buffer.Length;
                        if (terminator)
                        {
                            extentEnd = valueEnd + 1;
                        }
                    }
                    else
                    {
                        // A Complex key (its raw node), a comment: the value bytes.
                        terminator = false;
                    }
                    break;
            }

            if (extentEnd <= extentStart)
            {
                return;
            }

            YamlFindSegment(extentStart, out SequencePosition segment, out int segmentStart, out ReadOnlyMemory<byte> memory);
            if (extentEnd <= segmentStart + memory.Length)
            {
                // The token lies in one segment: ValueSpan is a slice of that segment, as on any input.
                ValueSpan = memory.Span.Slice(valueStart - segmentStart, valueEnd - valueStart);
                return;
            }

            // The token crosses a boundary. ValueSequence runs from the value's first byte to the position where the
            // lexer found its end: the terminator's own segment for a scalar that has one (which may be offset 0 of the
            // next segment), the end of the last byte otherwise.
            SequencePosition start = YamlPositionOf(valueStart, segment, segmentStart, memory);
            SequencePosition end = terminator || valueEnd == valueStart
                ? YamlPositionOf(valueEnd, segment, segmentStart, memory)
                : YamlPositionAfter(valueEnd - 1, segment, segmentStart, memory);
            ValueSequence = _sequence.Slice(start, end);
            HasValueSequence = true;
            ValueSpan = default;
        }

        /// <summary>Whether the current token has value bytes that ValueSpan or ValueSequence report.</summary>
        private readonly bool YamlTokenHasValue => _tokenType is YamlTokenType.String or YamlTokenType.Number or YamlTokenType.True
            or YamlTokenType.False or YamlTokenType.Null or YamlTokenType.PropertyName or YamlTokenType.Alias or YamlTokenType.Comment;

        /// <summary>The segment of the window that holds window offset <paramref name="local"/>.</summary>
        private readonly void YamlFindSegment(int local, out SequencePosition segment, out int segmentStart, out ReadOnlyMemory<byte> memory)
        {
            SequencePosition cursor = _currentPosition;
            int start = 0;
            while (true)
            {
                SequencePosition at = cursor;
                if (!_sequence.TryGet(ref cursor, out ReadOnlyMemory<byte> next))
                {
                    Debug.Fail("the window lies in the sequence");
                    segment = at;
                    segmentStart = start;
                    memory = default;
                    return;
                }
                if (next.IsEmpty)
                {
                    continue;
                }
                if (local < start + next.Length)
                {
                    segment = at;
                    segmentStart = start;
                    memory = next;
                    return;
                }
                start += next.Length;
            }
        }

        /// <summary>The position of the byte at window offset <paramref name="local"/>, in the segment that holds it.</summary>
        private readonly SequencePosition YamlPositionOf(int local, SequencePosition segment, int segmentStart, ReadOnlyMemory<byte> memory)
        {
            if (local < segmentStart || local >= segmentStart + memory.Length)
            {
                if (local - _buffer.Length >= 0 && _totalConsumed + local >= _sequence.Length)
                {
                    return _sequence.End;
                }
                YamlFindSegment(local, out segment, out segmentStart, out memory);
            }
            return new SequencePosition(segment.GetObject(), segment.GetInteger() + (local - segmentStart));
        }

        /// <summary>The position just after the byte at window offset <paramref name="local"/>, in that byte's segment.</summary>
        private readonly SequencePosition YamlPositionAfter(int local, SequencePosition segment, int segmentStart, ReadOnlyMemory<byte> memory)
        {
            if (local < segmentStart || local >= segmentStart + memory.Length)
            {
                YamlFindSegment(local, out segment, out segmentStart, out memory);
            }
            return new SequencePosition(segment.GetObject(), segment.GetInteger() + (local - segmentStart) + 1);
        }

        /// <summary>
        /// A slice of a stitched window, as a slice of the input segment when it lies in one (Anchor and Tag, decision
        /// 5): only bytes that cross a boundary stay in the copy.
        /// </summary>
        private readonly ReadOnlySpan<byte> YamlWindowSliceAsInput(int start, int length)
        {
            YamlFindSegment(start, out _, out int segmentStart, out ReadOnlyMemory<byte> memory);
            if (start + length <= segmentStart + memory.Length)
            {
                return memory.Span.Slice(start - segmentStart, length);
            }
            return _buffer.Slice(start, length);
        }

        /// <summary>
        /// The collection-key probe's first pass beyond the end of the window, over the following segments without
        /// copying, up to the same bound as on a span. <paramref name="searched"/> is the number of bytes
        /// searched from the probe's position so far; the results are offsets from that position. A close bracket
        /// whose follower lies in a later segment counts as a candidate, which the full scan decides.
        /// </summary>
        private void YamlProbeFirstPassBeyondWindow(int searchedInWindow, ref int lineEnd, ref int close, ref int searched, ref bool windowIsRest)
        {
            int at = searchedInWindow;
            int budget = YamlProbeWindowBytes - searchedInWindow;
            SequencePosition cursor = _sequence.GetPosition(_buffer.Length, _currentPosition);
            bool sequenceEnded = false;
            while (budget > 0)
            {
                if (!_sequence.TryGet(ref cursor, out ReadOnlyMemory<byte> memory))
                {
                    sequenceEnded = true;
                    break;
                }
                ReadOnlySpan<byte> span = memory.Span;
                if (span.Length > budget)
                {
                    span = span.Slice(0, budget);
                }

                int i = 0;
                while (i < span.Length)
                {
                    int k = YamlReaderHelper.IndexOfCloseBracketOrLineBreak(span.Slice(i));
                    if (k < 0)
                    {
                        break;
                    }
                    k += i;
                    if (span[k] is (byte)'\n' or (byte)'\r')
                    {
                        lineEnd = at + k;
                        searched = at + k;
                        _yamlProbeBytes += k + 1;
                        return;
                    }
                    int follower = k + 1;
                    while (follower < span.Length && span[follower] is (byte)' ' or (byte)'\t')
                    {
                        follower++;
                    }
                    if (follower >= span.Length || span[follower] == (byte)':')
                    {
                        close = at + k;
                        searched = at + k;
                        _yamlProbeBytes += k + 1;
                        return;
                    }
                    i = k + 1;
                }

                _yamlProbeBytes += span.Length;
                at += span.Length;
                budget -= span.Length;
            }

            searched = at;
            windowIsRest = sequenceEnded && _isFinalBlock;
        }
    }
}
