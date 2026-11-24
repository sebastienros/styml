// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        private enum YamlPlainEnd : byte
        {
            LineEnd,
            KeyColon,
            Comment,
            FlowIndicator,
            EndOfInput,
            Dedent,
        }

        private enum YamlCoreTag : byte
        {
            Other,
            NonSpecific,
            Str,
            Int,
            Float,
            Bool,
            Null,
            Map,
            Seq,
        }

        // ---------------------------------------------------------------------------------------------------------
        // Plain scalars

        /// <summary>Whether a plain scalar may start at <paramref name="c"/> (ns-plain-first).</summary>
        private readonly bool YamlCanStartPlain(in YamlCursor c, bool flow, out bool needMore)
        {
            needMore = false;
            byte b = _buffer[c.Pos];
            switch (b)
            {
                case (byte)'-':
                case (byte)'?':
                case (byte)':':
                    {
                        int next = YamlPeek(c.Pos + 1);
                        if (next == -2)
                        {
                            needMore = true;
                            return false;
                        }
                        return next != -1 && !YamlIsWhiteSpaceOrBreak((byte)next) && !(flow && YamlIsFlowIndicator((byte)next));
                    }

                case (byte)',':
                case (byte)'[':
                case (byte)']':
                case (byte)'{':
                case (byte)'}':
                case (byte)'#':
                case (byte)'&':
                case (byte)'*':
                case (byte)'!':
                case (byte)'|':
                case (byte)'>':
                case (byte)'\'':
                case (byte)'"':
                case (byte)'%':
                case (byte)'@':
                case (byte)'`':
                    return false;

                default:
                    return !YamlIsWhiteSpaceOrBreak(b);
            }
        }

        /// <summary>
        /// Scans a plain scalar starting at <paramref name="c"/>. On success <paramref name="c"/> is right after the
        /// scalar's last non-blank byte, and <paramref name="end"/> says what stopped it.
        /// </summary>
        /// <returns>False when the buffer ends before the end of the scalar is known.</returns>
        private readonly bool YamlScanPlain(ref YamlCursor c, int parentIndent, bool flow, bool singleLine, out int start, out int length, out bool multiLine, out YamlPlainEnd end)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            start = c.Pos;
            multiLine = false;
            YamlCursor last = c;
            YamlCursor p = c;

            while (true)
            {
                // Within one line.
                while (true)
                {
                    if (p.Pos >= buffer.Length)
                    {
                        if (!IsLastSpan)
                        {
                            length = 0;
                            end = default;
                            return false;
                        }
                        end = YamlPlainEnd.EndOfInput;
                        goto Done;
                    }

                    byte b = buffer[p.Pos];
                    if (b == (byte)':')
                    {
                        int next = YamlPeek(p.Pos + 1);
                        if (next == -2)
                        {
                            length = 0;
                            end = default;
                            return false;
                        }
                        if (next == -1 || YamlIsWhiteSpaceOrBreak((byte)next) || (flow && YamlIsFlowIndicator((byte)next)))
                        {
                            end = YamlPlainEnd.KeyColon;
                            goto Done;
                        }
                        p.Pos++;
                        p.Column++;
                        last = p;
                        continue;
                    }

                    if (YamlIsBlank(b))
                    {
                        p.Pos++;
                        p.Column++;
                        if (p.Pos < buffer.Length && buffer[p.Pos] == (byte)'#')
                        {
                            end = YamlPlainEnd.Comment;
                            goto Done;
                        }
                        continue;
                    }

                    if (YamlIsBreak(b))
                    {
                        break;
                    }

                    if (flow && YamlIsFlowIndicator(b))
                    {
                        end = YamlPlainEnd.FlowIndicator;
                        goto Done;
                    }

                    p.Pos++;
                    p.Column++;
                    last = p;
                }

                if (singleLine)
                {
                    end = YamlPlainEnd.LineEnd;
                    goto Done;
                }

                // A line break: does the scalar continue on a following line?
                YamlCursor q = p;
                int indent = 0;
                bool inIndent = true;
                while (true)
                {
                    if (q.Pos >= buffer.Length)
                    {
                        if (!IsLastSpan)
                        {
                            length = 0;
                            end = default;
                            return false;
                        }
                        end = YamlPlainEnd.EndOfInput;
                        goto Done;
                    }

                    byte b = buffer[q.Pos];
                    if (YamlIsBreak(b))
                    {
                        if (!YamlAdvanceBreak(ref q))
                        {
                            length = 0;
                            end = default;
                            return false;
                        }
                        indent = 0;
                        inIndent = true;
                        continue;
                    }
                    if (b == (byte)' ')
                    {
                        q.Pos++;
                        q.Column++;
                        if (inIndent)
                        {
                            indent++;
                        }
                        continue;
                    }
                    if (b == (byte)'\t')
                    {
                        q.Pos++;
                        q.Column++;
                        inIndent = false;
                        continue;
                    }
                    break;
                }

                byte first = buffer[q.Pos];
                if (first == (byte)'#')
                {
                    end = YamlPlainEnd.Comment;
                    goto Done;
                }

                int marker = q.Column == 0 ? YamlDocumentMarker(q, lineStart: true) : 0;
                if (marker < 0)
                {
                    length = 0;
                    end = default;
                    return false;
                }

                if (flow)
                {
                    if (marker > 0)
                    {
                        YamlThrow(YamlGrammarError.DocumentMarkerInFlow, q);
                    }
                    if (indent <= parentIndent)
                    {
                        YamlThrow(YamlGrammarError.FlowIndentation, q);
                    }
                    if (YamlIsFlowIndicator(first))
                    {
                        end = YamlPlainEnd.FlowIndicator;
                        goto Done;
                    }
                }
                else if (marker > 0 || indent <= parentIndent)
                {
                    end = YamlPlainEnd.Dedent;
                    goto Done;
                }

                if (first == (byte)':')
                {
                    int next = YamlPeek(q.Pos + 1);
                    if (next == -2)
                    {
                        length = 0;
                        end = default;
                        return false;
                    }
                    if (next == -1 || YamlIsWhiteSpaceOrBreak((byte)next) || (flow && YamlIsFlowIndicator((byte)next)))
                    {
                        end = YamlPlainEnd.KeyColon;
                        goto Done;
                    }
                }

                multiLine = true;
                p = q;
            }

        Done:
            length = last.Pos - start;
            c = last;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Quoted scalars

        /// <summary>
        /// Scans a single- or double-quoted scalar starting at the opening quote. On success <paramref name="c"/> is
        /// after the closing quote; <paramref name="start"/>/<paramref name="length"/> exclude the quotes.
        /// </summary>
        private readonly bool YamlScanQuoted(ref YamlCursor c, int parentIndent, bool flow, out int start, out int length, out bool multiLine, out bool escaped, out bool yamlOnlyEscape)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            byte quote = buffer[c.Pos];
            bool doubleQuoted = quote == (byte)'"';

            // M7d (item 1b, attempt 2): the common token, a quoted scalar that closes on its line with no escape, in
            // one pass with a local index and the cursor written once. Anything else (an escape, a doubled single
            // quote, a tab, a line break, a control, or the end of a non-final buffer) goes through the general loop
            // below, from the start.
            int close = c.Pos + 1;
            while (close < buffer.Length)
            {
                byte b = buffer[close];
                if (b == quote || b == (byte)'\\' || b < 0x20)
                {
                    break;
                }

                close++;
            }

            if (close < buffer.Length && buffer[close] == quote
                && (doubleQuoted || (close + 1 < buffer.Length ? buffer[close + 1] != (byte)'\'' : IsLastSpan)))
            {
                start = c.Pos + 1;
                length = close - start;
                multiLine = false;
                escaped = false;
                yamlOnlyEscape = false;
                c.Column += close + 1 - c.Pos;
                c.Pos = close + 1;
                return true;
            }
            YamlCursor p = c;
            p.Pos++;
            p.Column++;
            start = p.Pos;
            length = 0;
            multiLine = false;
            escaped = false;
            yamlOnlyEscape = false;

            while (true)
            {
                if (p.Pos >= buffer.Length)
                {
                    if (!IsLastSpan)
                    {
                        return false;
                    }
                    YamlThrow(YamlGrammarError.UnterminatedQuotedScalar, c);
                }

                byte b = buffer[p.Pos];
                if (b == quote)
                {
                    if (!doubleQuoted)
                    {
                        int next = YamlPeek(p.Pos + 1);
                        if (next == -2)
                        {
                            return false;
                        }
                        if (next == (byte)'\'')
                        {
                            escaped = true;
                            p.Pos += 2;
                            p.Column += 2;
                            continue;
                        }
                    }

                    length = p.Pos - start;
                    p.Pos++;
                    p.Column++;
                    c = p;
                    return true;
                }

                if (doubleQuoted && b == (byte)'\\')
                {
                    escaped = true;
                    int next = YamlPeek(p.Pos + 1);
                    if (next == -2)
                    {
                        return false;
                    }
                    if (next == -1)
                    {
                        YamlThrow(YamlGrammarError.UnterminatedQuotedScalar, c);
                    }

                    int hexDigits = 0;
                    switch ((byte)next)
                    {
                        case (byte)'"':
                        case (byte)'\\':
                        case (byte)'/':
                        case (byte)'b':
                        case (byte)'f':
                        case (byte)'n':
                        case (byte)'r':
                        case (byte)'t':
                            break;
                        case (byte)'u':
                            hexDigits = 4;
                            break;
                        case (byte)'0':
                        case (byte)'a':
                        case (byte)'v':
                        case (byte)'e':
                        case (byte)' ':
                        case (byte)'\t':
                        case (byte)'N':
                        case (byte)'_':
                        case (byte)'L':
                        case (byte)'P':
                            yamlOnlyEscape = true;
                            break;
                        case (byte)'x':
                            hexDigits = 2;
                            yamlOnlyEscape = true;
                            break;
                        case (byte)'U':
                            hexDigits = 8;
                            yamlOnlyEscape = true;
                            break;
                        case (byte)'\n':
                        case (byte)'\r':
                            // An escaped line break: the break is handled below.
                            yamlOnlyEscape = true;
                            p.Pos++;
                            p.Column++;
                            continue;
                        default:
                            YamlThrow(YamlGrammarError.InvalidEscape, p);
                            break;
                    }

                    if (p.Pos + 2 + hexDigits > buffer.Length)
                    {
                        if (!IsLastSpan)
                        {
                            return false;
                        }
                        YamlThrow(YamlGrammarError.InvalidEscape, p);
                    }
                    for (int i = 0; i < hexDigits; i++)
                    {
                        if (!YamlReaderHelper.IsHexDigit(buffer[p.Pos + 2 + i]))
                        {
                            YamlThrow(YamlGrammarError.InvalidEscape, p);
                        }
                    }
                    if (hexDigits == 8)
                    {
                        // \U must name a Unicode scalar value: at most U+10FFFF and not a surrogate.
                        uint codePoint = (uint)YamlReaderHelper.ParseHex(buffer.Slice(p.Pos + 2, 8));
                        if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
                        {
                            YamlThrow(YamlGrammarError.InvalidEscape, p);
                        }
                    }

                    p.Pos += 2 + hexDigits;
                    p.Column += 2 + hexDigits;
                    continue;
                }

                if (YamlIsBreak(b))
                {
                    multiLine = true;
                    escaped = true;
                    if (!YamlAdvanceBreak(ref p))
                    {
                        return false;
                    }

                    // Continuation line: indentation and document markers.
                    int spaces = 0;
                    int i = p.Pos;
                    while (i < buffer.Length && buffer[i] == (byte)' ')
                    {
                        spaces++;
                        i++;
                    }
                    while (i < buffer.Length && buffer[i] == (byte)'\t')
                    {
                        i++;
                    }

                    if (i >= buffer.Length)
                    {
                        if (!IsLastSpan)
                        {
                            return false;
                        }
                        YamlThrow(YamlGrammarError.UnterminatedQuotedScalar, c);
                    }

                    if (!YamlIsBreak(buffer[i]))
                    {
                        if (spaces == 0)
                        {
                            int marker = YamlDocumentMarker(p, lineStart: true);
                            if (marker < 0)
                            {
                                return false;
                            }
                            if (marker > 0)
                            {
                                YamlThrow(YamlGrammarError.DocumentMarkerInScalar, p);
                            }
                        }
                        if (spaces <= parentIndent)
                        {
                            YamlThrow(YamlGrammarError.QuotedScalarIndentation, p);
                        }
                    }

                    continue;
                }

                if (b < 0x20 && b != (byte)'\t')
                {
                    YamlThrow(YamlGrammarError.InvalidCharacterInScalar, p);
                }

                p.Pos++;
                p.Column++;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Block scalars

        private YamlStep YamlParseBlockScalar(ref YamlCursor c, int parentIndent, in YamlProperties props, int rawStart, long rawLine, long rawColumn)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            YamlCursor indicator = c;
            bool folded = buffer[c.Pos] == (byte)'>';
            c.Pos++;
            c.Column++;

            int explicitIndent = 0;
            byte chomping = 0;
            bool sawChomping = false;
            for (int i = 0; i < 2; i++)
            {
                if (c.Pos >= buffer.Length)
                {
                    if (!IsLastSpan)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    break;
                }

                byte h = buffer[c.Pos];
                if (h is >= (byte)'1' and <= (byte)'9' && explicitIndent == 0)
                {
                    explicitIndent = h - '0';
                }
                else if (h is (byte)'+' or (byte)'-' && !sawChomping)
                {
                    chomping = h == (byte)'-' ? (byte)1 : (byte)2;
                    sawChomping = true;
                }
                else
                {
                    break;
                }
                c.Pos++;
                c.Column++;
            }

            int afterHeader = YamlPeek(c.Pos);
            if (afterHeader == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (afterHeader != -1 && !YamlIsWhiteSpaceOrBreak((byte)afterHeader))
            {
                YamlThrow(YamlGrammarError.InvalidBlockScalarHeader, c);
            }

            if (!YamlExpectLineEnd(ref c))
            {
                return YamlStep.NeedMoreData;
            }

            int contentStart;
            int contentEnd;
            int contentIndent;
            YamlCursor end;

            if (c.Pos >= buffer.Length)
            {
                // Header at the very end of the input: an empty scalar.
                contentStart = contentEnd = c.Pos;
                contentIndent = Math.Max(parentIndent + 1, 1);
                end = c;
            }
            else
            {
                if (!YamlAdvanceBreak(ref c))
                {
                    return YamlStep.NeedMoreData;
                }

                contentStart = c.Pos;
                bool indentKnown = explicitIndent > 0;
                contentIndent = indentKnown ? Math.Max(parentIndent, 0) + explicitIndent : 0;
                int maxLeadingBlank = 0;
                YamlCursor line = c;

                while (true)
                {
                    // 'line' is at the start of a line. Count its leading spaces.
                    int pos = line.Pos;
                    int spaces = 0;
                    while (pos < buffer.Length && buffer[pos] == (byte)' ')
                    {
                        pos++;
                        spaces++;
                    }

                    if (pos >= buffer.Length)
                    {
                        if (!IsLastSpan)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        if (!indentKnown)
                        {
                            maxLeadingBlank = Math.Max(maxLeadingBlank, spaces);
                        }
                        end = new YamlCursor { Pos = buffer.Length, Line = line.Line, Column = spaces };
                        break;
                    }

                    byte b = buffer[pos];
                    if (YamlIsBreak(b))
                    {
                        // An empty line belongs to the scalar (it matters for chomping).
                        if (!indentKnown)
                        {
                            maxLeadingBlank = Math.Max(maxLeadingBlank, spaces);
                        }
                        YamlCursor next = new() { Pos = pos, Line = line.Line, Column = spaces };
                        if (!YamlAdvanceBreak(ref next))
                        {
                            return YamlStep.NeedMoreData;
                        }
                        line = next;
                        continue;
                    }

                    if (spaces == 0)
                    {
                        int marker = YamlDocumentMarker(line, lineStart: true);
                        if (marker < 0)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        if (marker > 0)
                        {
                            end = line;
                            break;
                        }
                    }

                    if (b == (byte)'\t' && !indentKnown && spaces <= parentIndent)
                    {
                        // A tab where an indentation space is expected.
                        YamlThrow(YamlGrammarError.BlockScalarIndentation, new YamlCursor { Pos = pos, Line = line.Line, Column = spaces });
                    }

                    if (!indentKnown)
                    {
                        if (spaces <= parentIndent)
                        {
                            end = line;
                            break;
                        }
                        contentIndent = spaces;
                        indentKnown = true;
                        if (maxLeadingBlank > contentIndent)
                        {
                            YamlThrow(YamlGrammarError.BlockScalarIndentation, line);
                        }
                    }

                    if (spaces < contentIndent)
                    {
                        // A less-indented non-empty line ends the scalar; the parent reads it.
                        end = line;
                        break;
                    }

                    int lineEnd = YamlReaderHelper.IndexOfLineBreak(buffer.Slice(pos));
                    if (lineEnd < 0)
                    {
                        if (!IsLastSpan)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        end = new YamlCursor { Pos = buffer.Length, Line = line.Line, Column = spaces + (buffer.Length - pos) };
                        break;
                    }

                    YamlCursor afterLine = new() { Pos = pos + lineEnd, Line = line.Line, Column = spaces + lineEnd };
                    if (!YamlAdvanceBreak(ref afterLine))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    line = afterLine;
                }

                if (!indentKnown)
                {
                    contentIndent = Math.Max(Math.Max(maxLeadingBlank, parentIndent + 1), 1);
                }
            }

            contentEnd = end.Pos;

            YamlTokenType type = YamlTokenType.String;
            if (props.TagLength > 0)
            {
                YamlCoreTag tag = YamlClassifyTag(props);
                if (tag is not (YamlCoreTag.Other or YamlCoreTag.NonSpecific or YamlCoreTag.Str))
                {
                    YamlThrowTagMismatch(props, indicator);
                }
            }

            TokenStartIndex = indicator.Pos;
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            YamlApplyProperties(props);
            ValueSpan = buffer.Slice(contentStart, contentEnd - contentStart);
            _yamlValueWindowStart = contentStart;
            ValueIsEscaped = true;
            _yamlNeedsProcessing = true;
            _yamlStyle = folded ? YamlScalarStyle.Folded : YamlScalarStyle.Literal;
            _yamlChomping = chomping;
            _yamlScalarIndent = contentIndent;
            c = end;
            YamlCommit(c);
            YamlSetTokenType(type);
            YamlAfterScalarAtRoot();
            return YamlStep.Token;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Properties, aliases, tags

        /// <summary>Reads node properties (<c>&amp;anchor</c>, <c>!tag</c>) and the blanks after them.</summary>
        private readonly bool YamlScanProperties(ref YamlCursor c, ref YamlProperties props, bool flow)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            while (c.Pos < buffer.Length)
            {
                byte b = buffer[c.Pos];
                if (b == (byte)'&')
                {
                    if (props.AnchorLength > 0)
                    {
                        YamlThrow(YamlGrammarError.DuplicateProperty, c);
                    }
                    int nameStart = c.Pos + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < buffer.Length && !YamlIsWhiteSpaceOrBreak(buffer[nameEnd]) && !YamlIsFlowIndicator(buffer[nameEnd]))
                    {
                        nameEnd++;
                    }
                    if (nameEnd >= buffer.Length && !IsLastSpan)
                    {
                        return false;
                    }
                    if (nameEnd == nameStart || !YamlWriterHelper.IsValidAnchorName(buffer.Slice(nameStart, nameEnd - nameStart)))
                    {
                        YamlThrow(YamlGrammarError.InvalidAnchor, c);
                    }
                    props.AnchorStart = nameStart;
                    props.AnchorLength = nameEnd - nameStart;
                    c.Column += nameEnd - c.Pos;
                    c.Pos = nameEnd;
                }
                else if (b == (byte)'!')
                {
                    if (props.TagLength > 0)
                    {
                        YamlThrow(YamlGrammarError.DuplicateProperty, c);
                    }
                    int tagEnd = c.Pos + 1;
                    if (tagEnd < buffer.Length && buffer[tagEnd] == (byte)'<')
                    {
                        int close = buffer.Slice(tagEnd).IndexOf((byte)'>');
                        int lineBreak = YamlReaderHelper.IndexOfLineBreak(buffer.Slice(tagEnd));
                        if (close < 0 || (lineBreak >= 0 && lineBreak < close))
                        {
                            if (close < 0 && lineBreak < 0 && !IsLastSpan)
                            {
                                return false;
                            }
                            YamlThrow(YamlGrammarError.InvalidTag, c);
                        }
                        tagEnd += close + 1;
                        if (!YamlWriterHelper.IsValidTag(buffer.Slice(c.Pos, tagEnd - c.Pos)))
                        {
                            YamlThrow(YamlGrammarError.InvalidTag, c);
                        }
                    }
                    else
                    {
                        while (tagEnd < buffer.Length && !YamlIsWhiteSpaceOrBreak(buffer[tagEnd]) && !YamlIsFlowIndicator(buffer[tagEnd]))
                        {
                            tagEnd++;
                        }
                        if (tagEnd >= buffer.Length && !IsLastSpan)
                        {
                            return false;
                        }
                        // '!handle!suffix': a named handle needs a suffix and a %TAG declaration.
                        ReadOnlySpan<byte> written = buffer.Slice(c.Pos, tagEnd - c.Pos);
                        if (!YamlWriterHelper.IsValidTag(written))
                        {
                            YamlThrow(YamlGrammarError.InvalidTag, c);
                        }
                        int second = written.Slice(1).IndexOf((byte)'!');
                        if (second > 0 && !YamlTagDirectives.TryGetPrefix(_yaml.TagDirectives, written.Slice(0, second + 2), out _))
                        {
                            YamlThrow(YamlGrammarError.UndefinedTagHandle, c);
                        }
                    }
                    props.TagStart = c.Pos;
                    props.TagLength = tagEnd - c.Pos;
                    c.Column += tagEnd - c.Pos;
                    c.Pos = tagEnd;
                }
                else
                {
                    return true;
                }

                // A property must be separated from what follows.
                int next = YamlPeek(c.Pos);
                if (next == -2)
                {
                    return false;
                }
                if (next == -1)
                {
                    return true;
                }
                if (YamlIsBlank((byte)next))
                {
                    YamlSkipBlanks(ref c);
                    continue;
                }
                if (YamlIsBreak((byte)next) || (flow && YamlIsFlowIndicator((byte)next)))
                {
                    return true;
                }
                YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ThrowHelper.GetPrintableString((byte)next));
            }

            return IsLastSpan;
        }

        /// <summary>Reads an alias value ('*name' at <paramref name="c"/>); the caller has ruled out an alias key.</summary>
        private YamlStep YamlParseAlias(ref YamlCursor c)
        {
            YamlCursor start = c;
            if (!YamlScanAliasName(ref c, out int nameStart, out int nameLength))
            {
                return YamlStep.NeedMoreData;
            }

            TokenStartIndex = start.Pos;
            YamlSetRawStart(start.Pos, start.Line, start.Column);
            ValueSpan = _buffer.Slice(nameStart, nameLength);
            _yamlValueWindowStart = nameStart;
            _yamlIsAlias = true;
            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.Alias);
            YamlAfterScalarAtRoot();
            return YamlStep.Token;
        }

        /// <summary>
        /// Scans '*name' at <paramref name="c"/> and moves past it. The name runs to the next white space or flow
        /// indicator (ns-anchor-char, so it may contain ':'). Returns false when more data is needed.
        /// </summary>
        private bool YamlScanAliasName(ref YamlCursor c, out int nameStart, out int nameLength)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            Debug.Assert(buffer[c.Pos] == (byte)'*');
            nameStart = c.Pos + 1;
            int nameEnd = nameStart;
            while (nameEnd < buffer.Length && !YamlIsWhiteSpaceOrBreak(buffer[nameEnd]) && !YamlIsFlowIndicator(buffer[nameEnd]))
            {
                nameEnd++;
            }
            if (nameEnd >= buffer.Length && !IsLastSpan)
            {
                nameLength = 0;
                return false;
            }
            if (nameEnd == nameStart || !YamlWriterHelper.IsValidAnchorName(buffer.Slice(nameStart, nameEnd - nameStart)))
            {
                YamlThrow(YamlGrammarError.InvalidAnchor, c);
            }

            nameLength = nameEnd - nameStart;
            c.Column += nameEnd - c.Pos;
            c.Pos = nameEnd;
            return true;
        }

        /// <summary>
        /// Whether the alias at <paramref name="c"/> is a mapping key: ':' follows on the same line, then separation
        /// (or, in flow context, a flow indicator). Returns -2 when more data is needed, 1 for a key, 0 otherwise.
        /// </summary>
        private int YamlProbeAliasKey(in YamlCursor c, bool flow)
        {
            YamlCursor probe = c;
            if (!YamlScanAliasName(ref probe, out _, out _))
            {
                return -2;
            }
            YamlSkipBlanks(ref probe);
            int next = YamlPeek(probe.Pos);
            if (next == -2)
            {
                return -2;
            }
            if (next != (byte)':')
            {
                return 0;
            }
            return flow ? YamlFollowedBySpaceOrFlowIndicator(probe.Pos + 1) : YamlFollowedBySpace(probe.Pos + 1);
        }

        private readonly YamlCoreTag YamlClassifyTag(in YamlProperties props)
        {
            ReadOnlySpan<byte> tag = _buffer.Slice(props.TagStart, props.TagLength);
            if (tag.Length == 1)
            {
                return YamlCoreTag.NonSpecific;
            }

            // Core tags are recognised by their expanded form, so '%TAG !! ...' and handles declared with the
            // tag:yaml.org,2002: prefix behave as the specification says.
            Span<byte> expanded = stackalloc byte[64];
            int length = YamlTagDirectives.Expand(_yaml.TagDirectives, tag, expanded);
            if (length > expanded.Length || !expanded.Slice(0, length).StartsWith(YamlTagDirectives.SecondaryPrefix))
            {
                return YamlCoreTag.Other;
            }
            ReadOnlySpan<byte> suffix = expanded.Slice(YamlTagDirectives.SecondaryPrefix.Length, length - YamlTagDirectives.SecondaryPrefix.Length);

            if (suffix.SequenceEqual("str"u8))
            {
                return YamlCoreTag.Str;
            }
            if (suffix.SequenceEqual("int"u8))
            {
                return YamlCoreTag.Int;
            }
            if (suffix.SequenceEqual("float"u8))
            {
                return YamlCoreTag.Float;
            }
            if (suffix.SequenceEqual("bool"u8))
            {
                return YamlCoreTag.Bool;
            }
            if (suffix.SequenceEqual("null"u8))
            {
                return YamlCoreTag.Null;
            }
            if (suffix.SequenceEqual("map"u8))
            {
                return YamlCoreTag.Map;
            }
            if (suffix.SequenceEqual("seq"u8))
            {
                return YamlCoreTag.Seq;
            }
            return YamlCoreTag.Other;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Emitting scalars

        /// <summary>
        /// The JSON schema (YAML 1.2.2, 10.2.2): <c>null</c>, <c>true</c>, <c>false</c> and JSON numbers resolve; any
        /// other untagged plain scalar is an error, as the specification says,
        /// or a string under <see cref="YamlReaderOptions.AcceptLenientScalars"/>.
        /// </summary>
        private readonly YamlTokenType YamlResolveJsonSchemaScalar(ReadOnlySpan<byte> value, bool multiLine, in YamlCursor c)
        {
            if (!multiLine)
            {
                if (value.SequenceEqual("null"u8))
                {
                    return YamlTokenType.Null;
                }
                if (value.SequenceEqual("true"u8))
                {
                    return YamlTokenType.True;
                }
                if (value.SequenceEqual("false"u8))
                {
                    return YamlTokenType.False;
                }
                if (YamlReaderHelper.IsJsonSchemaNumber(value))
                {
                    return YamlTokenType.Number;
                }
            }

            if (AcceptsLenientScalars)
            {
                // With AcceptLenientScalars, SharpYaml's reading: a string.
                return YamlTokenType.String;
            }

            YamlThrow(YamlGrammarError.UnresolvedJsonSchemaScalar, c, YamlReaderHelper.TranscodeHelper(value));
            return default;
        }

        private YamlStep YamlEmitScalar(
            ref YamlCursor c,
            int tokenStart,
            int start,
            int length,
            YamlScalarStyle style,
            bool escaped,
            bool needsProcessing,
            bool multiLine,
            in YamlProperties props,
            int rawStart,
            long rawLine,
            long rawColumn)
        {
            ReadOnlySpan<byte> value = _buffer.Slice(start, length);
            YamlTokenType type;
            YamlCoreTag tag = props.TagLength > 0 ? YamlClassifyTag(props) : YamlCoreTag.Other;
            bool yaml11 = _readerOptions.Schema == YamlSchemaKind.Yaml11;

            switch (tag)
            {
                case YamlCoreTag.Str:
                case YamlCoreTag.NonSpecific:
                    type = YamlTokenType.String;
                    break;

                case YamlCoreTag.Int:
                    if (escaped || !YamlReaderHelper.IsYamlInteger(value, yaml11))
                    {
                        YamlThrowTagMismatch(props, c);
                    }
                    type = YamlTokenType.Number;
                    break;

                case YamlCoreTag.Float:
                    if (escaped || !(YamlReaderHelper.IsYamlInteger(value, yaml11) || YamlReaderHelper.IsYamlFloat(value, yaml11)))
                    {
                        // AcceptLenientScalars: !!float .iNf, !!float NaN.
                        if (escaped || !_readerOptions.AcceptLenientScalars || !YamlReaderHelper.TryParseLenientFloatName(value, out _))
                        {
                            YamlThrowTagMismatch(props, c);
                        }
                    }
                    type = YamlTokenType.Number;
                    break;

                case YamlCoreTag.Bool:
                    if (escaped || !YamlReaderHelper.TryGetYamlBoolean(value, yaml11, out bool b))
                    {
                        // AcceptLenientScalars: !!bool tRuE, and with Yaml11 !!bool yEs.
                        if (escaped || !_readerOptions.AcceptLenientScalars || !YamlReaderHelper.TryGetLenientBoolean(value, yaml11, out b))
                        {
                            YamlThrowTagMismatch(props, c);
                            b = false;
                        }
                    }
                    type = b ? YamlTokenType.True : YamlTokenType.False;
                    break;

                case YamlCoreTag.Null:
                    if (escaped || !YamlReaderHelper.IsYamlNull(value))
                    {
                        // AcceptLenientScalars: !!null NuLL (SharpYaml).
                        if (escaped || !_readerOptions.AcceptLenientScalars || !YamlReaderHelper.IsLenientNull(value))
                        {
                            YamlThrowTagMismatch(props, c);
                        }
                    }
                    type = YamlTokenType.Null;
                    break;

                case YamlCoreTag.Map:
                case YamlCoreTag.Seq:
                    YamlThrowTagMismatch(props, c);
                    type = default;
                    break;

                default:
                    type = style != YamlScalarStyle.Plain ? YamlTokenType.String
                        : _readerOptions.Schema switch
                        {
                            YamlSchemaKind.Failsafe => YamlTokenType.String,
                            YamlSchemaKind.Json => YamlResolveJsonSchemaScalar(value, multiLine, c),
                            _ => multiLine ? YamlTokenType.String : YamlReaderHelper.ResolveYamlPlainScalar(value, yaml11),
                        };
                    break;
            }

            TokenStartIndex = tokenStart;
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            YamlApplyProperties(props);
            ValueSpan = value;
            _yamlValueWindowStart = start;
            ValueIsEscaped = escaped;
            _yamlNeedsProcessing = needsProcessing;
            _yamlStyle = style;
            _yamlNumberNotJson = yaml11 && type == YamlTokenType.Number && YamlReaderHelper.IsYaml11LegacyOctal(value);
            YamlCommit(c);
            YamlSetTokenType(type);
            YamlAfterScalarAtRoot();
            return YamlStep.Token;
        }
    }
}
