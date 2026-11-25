// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        // The implicit collection-key probe. At a position where a key
        // may start, a '[' or '{' is a key when its matching bracket is on the same line, within 1024 characters, and
        // is followed by ':'. The probe is a bracket and quote scan, not a parse.
        //
        // Cost. An implicit key is one line of at most 1024 characters, so no probe looks further than a bounded window
        // past its own start, and what a probe learns is kept so that later probes do not scan the same bytes again:
        // - a key-free range [_yamlNoKeyFrom, _yamlNoKeyTo): every collection that starts in it is not a key;
        // - the decisions of every collection a full scan saw open, in order of position (the probe cache).
        // A first pass over at most YamlProbeWindowBytes answers JSON and any line without a ']' or '}' followed by ':',
        // and always records a key-free range: up to the line end, or up to YamlKeyMaxBytes before the first close
        // bracket followed by ':', or before the end of the window. The full scan covers 2048 characters, so every
        // collection it leaves undecided starts at least 1024 characters after the probe. Together the bytes all
        // probes examine are O(input), for final and non-final data (KeyProbeCostTests counts them).
        //
        // This is instance state of the reader, not YamlReaderState: a reader over a new buffer starts empty and probes
        // again. A copy of the reader that reads a key node gets its own cache (YamlReadKeyNode).

        /// <summary>The most bytes an implicit key of 1024 characters can take in UTF-8.</summary>
        private const int YamlKeyMaxBytes = YamlImplicitKeyMaxCharacters * 4;

        /// <summary>
        /// The window of the first pass, in bytes. A pass that finds nothing records a key-free range of the window less
        /// YamlKeyMaxBytes, so a wider window means fewer passes over the same bytes.
        /// </summary>
        private const int YamlProbeWindowBytes = 8 * YamlKeyMaxBytes;

        /// <summary>The window of the full scan, in characters.</summary>
        private const int YamlProbeScanCharacters = 2 * YamlImplicitKeyMaxCharacters;

        private const sbyte YamlProbeUndecided = -1;
        private const sbyte YamlProbeNotKey = 0;
        private const sbyte YamlProbeKey = 1;
        private const sbyte YamlProbeKeyInFlowOnly = 2;
        private const sbyte YamlProbeTooLong = 3;

        private int _yamlNoKeyFrom;
        private int _yamlNoKeyTo;

        // The stretch the last first pass searched up to its candidate (a close bracket followed by ':'), see below.
        private int _yamlCandidateFrom;
        private int _yamlCandidateAt;

        // Absolute offsets (_totalConsumed + local), so that an entry means the same byte in every copy of the reader that
        // shares the array, whichever window each copy reads.
        private long[]? _yamlProbeCachePos;
        private sbyte[]? _yamlProbeCacheState;
        private int[]? _yamlProbeCacheChars;
        private int[]? _yamlProbeStack;
        private int _yamlProbeCacheCount;
        private int _yamlProbeCacheCursor;
        private long _yamlProbeBytes;
        private bool _yamlProbeCacheShared;

        /// <summary>The number of bytes the collection-key probe has examined in this reader's buffer (for tests).</summary>
        internal readonly long YamlProbeBytesExamined => _yamlProbeBytes;

        /// <summary>
        /// Decides whether the flow collection starting at <paramref name="pos"/> is an implicit key. Returns 1 for a
        /// key, 0 for not a key, -2 when more data is needed. <paramref name="tooLong"/> is set when it is not a key only
        /// because it is longer than 1024 characters (for the error message).
        /// </summary>
        private int YamlProbeCollectionKey(int pos, bool flow, out bool tooLong)
        {
            tooLong = false;
            if (pos >= _yamlNoKeyFrom && pos < _yamlNoKeyTo)
            {
                return 0;
            }
            if (YamlProbeCacheLookup(pos, flow, out int cached, out tooLong))
            {
                return cached;
            }

            int safeEnd;
            if (pos >= _yamlCandidateFrom && pos < _yamlCandidateAt)
            {
                // An earlier first pass cleared [_yamlCandidateFrom, _yamlCandidateAt): no close bracket followed by ':'
                // there, and a candidate at _yamlCandidateAt. A probe that starts in that stretch reuses it rather than
                // searching the same bytes again, so the first passes search each byte once.
                safeEnd = _yamlCandidateAt - YamlKeyMaxBytes;
            }
            else
            {
                // First pass: a vectorised search in a bounded window, which answers JSON.
                ReadOnlySpan<byte> buffer = _buffer;
                int windowEnd = buffer.Length - pos > YamlProbeWindowBytes ? pos + YamlProbeWindowBytes : buffer.Length;
                ReadOnlySpan<byte> window = buffer.Slice(pos, windowEnd - pos);
                bool windowIsRest = windowEnd == buffer.Length && IsLastSpan;
                bool beyondBuffer = windowEnd == buffer.Length && _isMultiSegment && !YamlWindowReachesSequenceEnd;

                // One vectorised pass to the nearer of the line end and the first close bracket followed by ':'. It stops
                // only at line breaks and close brackets, which are sparse in JSON text; the follower of a close bracket is
                // checked there. A close bracket whose follower lies beyond the window counts as a candidate, which the
                // full scan below decides exactly.
                int lineEnd = -1;
                int close = -1;
                int offset = 0;
                while (true)
                {
                    int k = YamlReaderHelper.IndexOfCloseBracketOrLineBreak(window.Slice(offset));
                    if (k < 0)
                    {
                        offset = window.Length;
                        break;
                    }
                    k += offset;
                    if (window[k] is (byte)'\n' or (byte)'\r')
                    {
                        lineEnd = k;
                        break;
                    }
                    int follower = k + 1;
                    while (follower < window.Length && window[follower] is (byte)' ' or (byte)'\t')
                    {
                        follower++;
                    }
                    if (follower < window.Length ? window[follower] == (byte)':' : !windowIsRest)
                    {
                        close = k;
                        break;
                    }
                    offset = k + 1;
                }
                _yamlProbeBytes += (lineEnd >= 0 ? lineEnd : close >= 0 ? close : window.Length) + 1;
                int searched = window.Length;
                if (lineEnd < 0 && close < 0 && beyondBuffer)
                {
                    // A multi-segment sequence whose window ends here: the pass goes on over the following segments,
                    // without copying, to the same bound as on a span (M3c).
                    YamlProbeFirstPassBeyondWindow(window.Length, ref lineEnd, ref close, ref searched, ref windowIsRest);
                }
                int lineLength = lineEnd >= 0 ? lineEnd : searched;
                bool lineComplete = lineEnd >= 0 || windowIsRest;

                // Collections starting before safeEnd are not keys: a key ends within YamlKeyMaxBytes of its start, on its
                // line, at a close bracket followed by ':', and there is none before 'close' (or in the line or window).
                safeEnd = close >= 0 ? pos + close - YamlKeyMaxBytes
                    : lineComplete ? pos + lineLength
                    : pos + searched - YamlKeyMaxBytes;
                if (close >= 0)
                {
                    _yamlCandidateFrom = pos;
                    _yamlCandidateAt = pos + close;
                }
            }

            if (safeEnd > pos)
            {
                _yamlNoKeyFrom = pos;
                _yamlNoKeyTo = safeEnd;
                return 0;
            }

            // Full scan of up to 2048 characters, which decides this collection and those it sees open.
            YamlProbeScan(pos, flow);
            if (YamlProbeCacheLookup(pos, flow, out cached, out tooLong))
            {
                return cached;
            }
            return IsLastSpan ? 0 : -2;
        }

        private bool YamlProbeCacheLookup(int pos, bool flow, out int result, out bool tooLong)
        {
            result = 0;
            tooLong = false;
            long[]? positions = _yamlProbeCachePos;
            if (positions is null)
            {
                return false;
            }

            // Probes come in increasing position order within one buffer, so the cursor only moves forward.
            long absolute = _totalConsumed + pos;
            while (_yamlProbeCacheCursor < _yamlProbeCacheCount && positions[_yamlProbeCacheCursor] < absolute)
            {
                _yamlProbeCacheCursor++;
            }
            if (_yamlProbeCacheCursor >= _yamlProbeCacheCount || positions[_yamlProbeCacheCursor] != absolute)
            {
                return false;
            }

            switch (_yamlProbeCacheState![_yamlProbeCacheCursor])
            {
                case YamlProbeKey:
                    result = 1;
                    return true;
                case YamlProbeKeyInFlowOnly:
                    result = flow ? 1 : 0;
                    return true;
                case YamlProbeNotKey:
                    return true;
                case YamlProbeTooLong:
                    tooLong = true;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Scans from the '[' or '{' at <paramref name="pos"/> to the end of its line, a comment, the end of the data,
        /// the close of its parent, or 2048 characters, and records a decision for every collection that opens on the
        /// way: key (its matching bracket is followed by ':'), not a key, too long, or undecided.
        /// </summary>
        private void YamlProbeScan(int pos, bool flow)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            Debug.Assert(buffer[pos] is (byte)'[' or (byte)'{');
            _yamlProbeCacheCount = 0;
            _yamlProbeCacheCursor = 0;
            if (_yamlProbeCacheShared || _yamlProbeCachePos is null)
            {
                // A copy of the reader writes to its own arrays (copy on write), never to its parent's.
                _yamlProbeCachePos = new long[16];
                _yamlProbeCacheState = new sbyte[16];
                _yamlProbeCacheChars = new int[16];
                _yamlProbeStack = new int[16];
                _yamlProbeCacheShared = false;
            }

            // The scan lexes flow content as the reader does (YamlParseFlowNode, YamlScanPlain, YamlScanProperties),
            // so that a quote opens a quoted scalar exactly where the reader would start one: where a node starts,
            // never inside a plain scalar ('a "b' is one plain scalar). States:
            const byte BetweenNodes = 0;    // a node may start here (after '[', '{', ',' or an indicator)
            const byte InPlain = 1;         // inside a plain scalar
            const byte AfterNode = 2;       // after a quoted scalar, an alias or a collection
            const byte InProperty = 3;      // inside an anchor or a tag
            int depth = 0;
            int characters = 0;
            byte state = BetweenNodes;
            bool afterBlank = false;
            byte quote = 0;
            bool lineEnded = false;
            int plainStart = pos;
            int i = pos;

            for (; i < buffer.Length; i++)
            {
                byte b = buffer[i];
                if ((b & 0xC0) != 0x80 && ++characters > YamlProbeScanCharacters)
                {
                    break;
                }
                if (b is (byte)'\n' or (byte)'\r')
                {
                    lineEnded = true;
                    break;
                }

                if (quote != 0)
                {
                    if (quote == (byte)'"')
                    {
                        if (b == (byte)'\\')
                        {
                            i++;
                            if (i < buffer.Length && (buffer[i] & 0xC0) != 0x80)
                            {
                                characters++;
                            }
                        }
                        else if (b == (byte)'"')
                        {
                            quote = 0;
                            state = AfterNode;
                        }
                    }
                    else if (b == (byte)'\'')
                    {
                        if (i + 1 >= buffer.Length)
                        {
                            // '' (an escaped quote) or the end of the scalar: undecided until the next byte is known.
                            break;
                        }
                        if (buffer[i + 1] == (byte)'\'')
                        {
                            i++;
                            characters++;
                        }
                        else
                        {
                            quote = 0;
                            state = AfterNode;
                        }
                    }

                    if (quote != 0 && i + 1 < buffer.Length)
                    {
                        // Skip the rest of the quoted text to the next byte that matters, counting its characters.
                        ReadOnlySpan<byte> rest = buffer.Slice(i + 1);
                        int k = quote == (byte)'"' ? YamlReaderHelper.IndexOfDoubleQuotedProbeByte(rest) : YamlReaderHelper.IndexOfSingleQuotedProbeByte(rest);
                        int skip = k < 0 ? rest.Length : k;
                        characters += YamlReaderHelper.CountCharacters(rest.Slice(0, skip));
                        i += skip;
                        if (characters > YamlProbeScanCharacters)
                        {
                            i++;
                            break;
                        }
                    }
                    continue;
                }

                bool blank = b is (byte)' ' or (byte)'\t';
                if (blank)
                {
                    afterBlank = true;
                    if (state == InProperty)
                    {
                        state = BetweenNodes;
                    }
                    continue;
                }

                // A comment: '#' after a blank, in any state (YamlScanPlain ends a plain scalar the same way).
                if (b == (byte)'#' && afterBlank)
                {
                    lineEnded = true;
                    goto Done;
                }

                if (b == (byte)'/' && i + 1 < buffer.Length && YamlJsonComments
                    && (afterBlank || state is BetweenNodes or AfterNode || (state == InPlain && YamlIsJsonLiteral(buffer.Slice(plainStart, i - plainStart)))))
                {
                    // A JSON comment, where the reader starts one (YamlScanToContent, YamlScanPlain): after a blank,
                    // where a node may start, or directly after a JSON literal. '//' ends the line; a '/* */' closed
                    // on the line is white space, and one that is not ends the line too.
                    if (buffer[i + 1] == (byte)'/')
                    {
                        lineEnded = true;
                        goto Done;
                    }
                    if (buffer[i + 1] == (byte)'*')
                    {
                        ReadOnlySpan<byte> rest = buffer.Slice(i + 2);
                        int close = rest.IndexOf("*/"u8);
                        if (close < 0 ? YamlReaderHelper.IndexOfLineBreak(rest) >= 0 || IsLastSpan : YamlReaderHelper.IndexOfLineBreak(rest.Slice(0, close)) >= 0)
                        {
                            lineEnded = true;
                            goto Done;
                        }
                        if (close < 0)
                        {
                            // Not closed in the data so far: undecided.
                            i = buffer.Length;
                            break;
                        }
                        characters += YamlReaderHelper.CountCharacters(rest.Slice(0, close + 2)) + 1;
                        i += close + 3;
                        afterBlank = true;
                        continue;
                    }
                }
                afterBlank = false;

                // Flow indicators end a plain scalar or a property, and are structural in every state.
                switch (b)
                {
                    case (byte)'[':
                    case (byte)'{':
                        YamlProbeOpen(i, characters, ref depth);
                        state = BetweenNodes;
                        continue;

                    case (byte)']':
                    case (byte)'}':
                        if (depth == 0)
                        {
                            // The close of the parent collection: the entries after the probed one end here.
                            goto Done;
                        }
                        int entry = _yamlProbeStack![--depth];
                        int length = characters - _yamlProbeCacheChars![entry] + 1;
                        _yamlProbeCacheState![entry] = length > YamlImplicitKeyMaxCharacters ? YamlProbeTooLong : YamlProbeFollower(i + 1);
                        state = AfterNode;
                        if (depth == 0 && !flow)
                        {
                            // In block context what follows the probed collection is not flow content.
                            goto Done;
                        }
                        continue;

                    case (byte)',':
                        state = BetweenNodes;
                        continue;
                }

                switch (state)
                {
                    case InPlain:
                        if (b == (byte)':' && YamlProbeEndsPlainAt(i + 1))
                        {
                            // The value indicator ends the plain scalar.
                            state = BetweenNodes;
                        }
                        continue;

                    case InProperty:
                        continue;

                    case AfterNode:
                        // ':' may be adjacent to a JSON-like node; anything else here is not valid YAML, and is read on
                        // as a plain scalar would be.
                        state = b == (byte)':' ? BetweenNodes : InPlain;
                        plainStart = i;
                        continue;
                }

                // BetweenNodes: what starts here (YamlParseFlowNode, YamlCanStartPlain).
                switch (b)
                {
                    case (byte)'"':
                    case (byte)'\'':
                        quote = b;
                        continue;

                    case (byte)'&':
                    case (byte)'!':
                        state = InProperty;
                        continue;

                    case (byte)'*':
                        // An alias runs to a blank or a flow indicator, like a plain scalar for this purpose.
                        state = InPlain;
                        plainStart = i;
                        continue;

                    case (byte)'?':
                    case (byte)':':
                    case (byte)'-':
                        // An indicator when followed by a blank, a flow indicator or the end; otherwise the first
                        // character of a plain scalar.
                        state = YamlProbeEndsPlainAt(i + 1) ? BetweenNodes : InPlain;
                        plainStart = i;
                        continue;

                    default:
                        state = InPlain;
                        plainStart = i;
                        continue;
                }
            }

        Done:
            _yamlProbeBytes += Math.Min(i + 1, buffer.Length) - pos;

            // The collections still open: on a line that ended (or at the end of the input) they are not keys; those
            // opened at least 1024 characters back are too long; the others stay undecided.
            bool endOfInput = i >= buffer.Length && IsLastSpan;
            for (int d = 0; d < depth; d++)
            {
                int entry = _yamlProbeStack![d];
                if (lineEnded || endOfInput)
                {
                    _yamlProbeCacheState![entry] = YamlProbeNotKey;
                }
                else if (characters - _yamlProbeCacheChars![entry] >= YamlImplicitKeyMaxCharacters)
                {
                    _yamlProbeCacheState![entry] = YamlProbeTooLong;
                }
            }
        }

        /// <summary>Whether the byte at <paramref name="i"/> ends a plain scalar after ':' (or makes '?', '-' an
        /// indicator): a blank, a line break, a flow indicator, or the end of the data.</summary>
        private readonly bool YamlProbeEndsPlainAt(int i)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            return i >= buffer.Length || YamlIsWhiteSpaceOrBreak(buffer[i]) || YamlIsFlowIndicator(buffer[i]);
        }

        private void YamlProbeOpen(int position, int characters, ref int depth)
        {
            if (_yamlProbeCacheCount == _yamlProbeCachePos!.Length)
            {
                int size = _yamlProbeCacheCount * 2;
                Array.Resize(ref _yamlProbeCachePos, size);
                Array.Resize(ref _yamlProbeCacheState, size);
                Array.Resize(ref _yamlProbeCacheChars, size);
            }
            if (depth == _yamlProbeStack!.Length)
            {
                Array.Resize(ref _yamlProbeStack, depth * 2);
            }

            int entry = _yamlProbeCacheCount++;
            _yamlProbeCachePos[entry] = _totalConsumed + position;
            _yamlProbeCacheState![entry] = YamlProbeUndecided;
            _yamlProbeCacheChars![entry] = characters;
            _yamlProbeStack[depth++] = entry;
        }

        /// <summary>What follows a close bracket at <paramref name="i"/>: ':' and white space (a key in any context),
        /// ':' adjacent (a key in flow context only), something else, or not yet known.</summary>
        private sbyte YamlProbeFollower(int i)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = i;
            while (i < buffer.Length && YamlIsBlank(buffer[i]))
            {
                i++;
            }
            _yamlProbeBytes += i - start + 2;
            if (i >= buffer.Length)
            {
                return IsLastSpan ? YamlProbeNotKey : YamlProbeUndecided;
            }
            if (buffer[i] != (byte)':')
            {
                return YamlProbeNotKey;
            }
            if (i + 1 >= buffer.Length)
            {
                return IsLastSpan ? YamlProbeKey : YamlProbeUndecided;
            }
            return YamlIsWhiteSpaceOrBreak(buffer[i + 1]) ? YamlProbeKey : YamlProbeKeyInFlowOnly;
        }

        private void YamlNoteTooLongKey(bool tooLong, long line)
        {
            if (tooLong)
            {
                _yaml.TooLongKeyLinePlusOne = line + 1;
            }
        }

        /// <summary>
        /// The error for a ':' after a node where no key was recognised: a collection on this line that was too long to
        /// be an implicit key, or otherwise a key that spans lines.
        /// </summary>
        private readonly YamlGrammarError YamlKeyAfterNodeError(in YamlCursor c) =>
            _readerOptions.DisallowImplicitCollectionKeys ? YamlGrammarError.ImplicitCollectionKeyDisallowed
            : _yaml.TooLongKeyLinePlusOne == c.Line + 1 ? YamlGrammarError.ImplicitKeyTooLong
            : YamlGrammarError.MultilineImplicitKey;

        /// <summary>
        /// For a copy of the reader that reads a key node: it keeps reading the parent's decisions (a key nested in a key
        /// is decided by the parent's scan), and allocates its own arrays the first time it scans.
        /// </summary>
        private void YamlProbeShareReadOnly() => _yamlProbeCacheShared = true;
    }
}
