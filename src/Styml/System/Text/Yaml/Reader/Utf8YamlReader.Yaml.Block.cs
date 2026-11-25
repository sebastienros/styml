// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        private YamlStep YamlReadBlock(ref YamlCursor c, int level)
        {
            int indent = level >> 1;
            if (_inObject)
            {
                return YamlReadBlockMapping(ref c, indent);
            }
            return YamlReadBlockSequence(ref c, indent, indentless: (level & 1) != 0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Block mappings

        private YamlStep YamlReadBlockMapping(ref YamlCursor c, int indent)
        {
            if (_yaml.Has(YamlParseFlags.ExplicitKeyPending))
            {
                return YamlReadExplicitValue(ref c, indent);
            }

            YamlTokenType effective = YamlEffectiveTokenType;
            if (effective == YamlTokenType.PropertyName)
            {
                return YamlReadBlockMappingValue(ref c, indent, compactAllowed: false);
            }

            if (!YamlScanToContent(ref c, out YamlScan s, stopAtComment: YamlAllowComments))
            {
                return YamlStep.NeedMoreData;
            }

            if (s.IsComment)
            {
                return YamlEmitComment(ref c);
            }

            if (s.AtEnd)
            {
                return YamlEmitEnd(ref c, s.LineStart, consumesBracket: false);
            }

            if (s.LineStart)
            {
                int marker = YamlDocumentMarker(c, s.LineStart);
                if (marker < 0)
                {
                    return YamlStep.NeedMoreData;
                }
                if (marker > 0 || s.Indent < indent)
                {
                    return YamlEmitEnd(ref c, s.LineStart, consumesBracket: false);
                }
                if (s.Indent > indent)
                {
                    YamlThrow(YamlGrammarError.BadIndentation, c);
                }
                if (s.TabInIndent)
                {
                    YamlThrow(YamlGrammarError.TabIndentation, c);
                }
            }
            else if (effective != YamlTokenType.StartObject)
            {
                YamlThrow(_buffer[c.Pos] == (byte)':' ? YamlGrammarError.MappingValueNotAllowed : YamlGrammarError.UnexpectedContentAfterNode, c);
            }

            return YamlParseBlockKey(ref c, indent);
        }

        // Not inlined: with dynamic PGO, a caller that inlines Read could inline this method once its budget allowed,
        // and then had no budget left for the key path it calls (YamlParseImplicitKey, YamlScanPlain,
        // YamlEmitPropertyName), which its own compilation inlines. That cost single-segment block mappings up to 4.7%
        // when M3c's smaller GetString left the benchmark's read loop that budget (M3c gate 3).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private YamlStep YamlParseBlockKey(ref YamlCursor c, int indent)
        {
            int rawStart = c.Pos;
            long rawLine = c.Line;
            long rawColumn = c.Column;
            YamlProperties props = default;
            byte b = _buffer[c.Pos];

            // M7d (item 1d): the common key, letters, digits and _-./ that ':' and white space end on the same line, is
            // emitted at once; everything else takes the checks below.
            if (YamlReaderHelper.IsSimpleKeyStart(b))
            {
                ReadOnlySpan<byte> buffer = _buffer;
                int end = c.Pos + 1;
                int limit = Math.Min(buffer.Length, c.Pos + YamlImplicitKeyMaxCharacters);
                while (end < limit && YamlReaderHelper.IsSimpleKeyByte(buffer[end]))
                {
                    end++;
                }

                if (end + 1 < buffer.Length && buffer[end] == (byte)':' && YamlIsWhiteSpaceOrBreak(buffer[end + 1]))
                {
                    int keyStart = c.Pos;
                    c.Column += end - keyStart;
                    c.Pos = end;
                    TokenStartIndex = keyStart;
                    YamlStep step = YamlEmitPropertyName(ref c, keyStart, end - keyStart, YamlScalarStyle.Plain, escaped: false, needsProcessing: false, props, rawStart, rawLine, rawColumn, consumeColon: true, keyStartOverride: keyStart);
                    _yamlIsAlias = false;
                    return step;
                }
            }

            if (b is (byte)'&' or (byte)'!')
            {
                if (!YamlScanProperties(ref c, ref props, flow: false))
                {
                    return YamlStep.NeedMoreData;
                }
                YamlSkipBlanks(ref c);
                if (c.Pos >= _buffer.Length)
                {
                    if (!IsLastSpan)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlThrow(YamlGrammarError.ExpectedColon, c);
                }
                b = _buffer[c.Pos];
                if (YamlIsBreak(b) || b == (byte)'#')
                {
                    // Properties on their own line in key position would make the implicit key span lines.
                    YamlThrow(YamlGrammarError.MultilineImplicitKey, c);
                }
            }

            int follow = b is (byte)'-' or (byte)'?' or (byte)':' ? YamlFollowedBySpace(c.Pos + 1) : 0;
            if (follow == -2)
            {
                return YamlStep.NeedMoreData;
            }

            switch (b)
            {
                case (byte)'?' when follow == 1:
                    if (props.Any)
                    {
                        YamlThrow(YamlGrammarError.UnexpectedCharacter, c, "?");
                    }
                    return YamlParseExplicitBlockKey(ref c, indent, rawStart, rawLine, rawColumn);

                case (byte)':' when follow == 1:
                    // An empty (null) key.
                    return YamlEmitPropertyName(ref c, c.Pos, 0, YamlScalarStyle.Plain, escaped: false, needsProcessing: false, props, rawStart, rawLine, rawColumn, consumeColon: true);

                case (byte)'*' when props.Any:
                    YamlThrow(YamlGrammarError.PropertiesOnAlias, c);
                    break;

                case (byte)'[':
                case (byte)'{':
                    return YamlParseImplicitCollectionKey(ref c, indent, rawStart, rawLine, rawColumn);

                case (byte)'|':
                case (byte)'>':
                    // A block scalar can only be a key after '? '.
                    YamlThrow(YamlGrammarError.ExpectedColon, c);
                    break;

                case (byte)'-' when follow == 1:
                    YamlThrow(YamlGrammarError.SequenceEntryNotAllowed, c);
                    break;
            }

            return YamlParseImplicitKey(ref c, indent, flow: false, props, rawStart, rawLine, rawColumn, out _);
        }

        /// <summary>
        /// '[a, b]: v' or '{a: b}: v' in a block mapping: the flow collection is read as the key node (its properties
        /// included), on one line and within 1024 characters, then ':' and whitespace must follow.
        /// </summary>
        private YamlStep YamlParseImplicitCollectionKey(ref YamlCursor c, int indent, int rawStart, long rawLine, long rawColumn)
        {
            if (_readerOptions.DisallowImplicitCollectionKeys)
            {
                YamlThrow(YamlGrammarError.ImplicitCollectionKeyDisallowed, c);
            }
            if (YamlDraft13)
            {
                YamlThrow(YamlGrammarError.Draft13ImplicitCollectionKey, c);
            }
            YamlCursor nodeStart = new() { Pos = rawStart, Line = rawLine, Column = rawColumn };
            if (YamlReadKeyNode(nodeStart, explicitBlock: false, indent, out YamlKeyNode node) != YamlStep.Token)
            {
                return YamlStep.NeedMoreData;
            }
            YamlCheckImplicitCollectionKey(node);

            c = node.End;
            YamlSkipBlanks(ref c);
            int colon = YamlPeek(c.Pos);
            if (colon == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (colon != (byte)':')
            {
                YamlThrow(YamlGrammarError.ExpectedColon, c);
            }
            int follow = YamlFollowedBySpace(c.Pos + 1);
            if (follow == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (follow == 0)
            {
                YamlThrow(YamlGrammarError.ExpectedColon, c);
            }

            YamlSetComplexKey(node);
            c.Pos++;
            c.Column++;
            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.PropertyName);
            return YamlStep.Token;
        }

        /// <summary>
        /// Reads an implicit key (a single-line scalar followed by ':') and emits it as a PropertyName.
        /// </summary>
        private YamlStep YamlParseImplicitKey(ref YamlCursor c, int indent, bool flow, in YamlProperties props, int rawStart, long rawLine, long rawColumn, out bool hadColon)
        {
            hadColon = false;
            byte b = _buffer[c.Pos];
            int start;
            int length;
            YamlScalarStyle style;
            bool escaped;
            bool needsProcessing;
            bool alias = false;
            YamlCursor keyStart = c;

            if (b == (byte)'*')
            {
                // An alias key: ValueSpan is the anchor name; the string accessors refuse it (KeyKind.Alias).
                if (props.Any)
                {
                    YamlThrow(YamlGrammarError.PropertiesOnAlias, c);
                }
                if (!YamlScanAliasName(ref c, out start, out length))
                {
                    return YamlStep.NeedMoreData;
                }
                style = YamlScalarStyle.None;
                escaped = true;
                needsProcessing = true;
                alias = true;
            }
            else if (b is (byte)'\'' or (byte)'"')
            {
                if (!YamlScanQuoted(ref c, indent, flow, out start, out length, out bool multiLine, out escaped, out bool yamlOnlyEscape))
                {
                    return YamlStep.NeedMoreData;
                }
                if (multiLine && !flow)
                {
                    YamlThrow(YamlGrammarError.MultilineImplicitKey, keyStart);
                }
                style = b == (byte)'"' ? YamlScalarStyle.DoubleQuoted : YamlScalarStyle.SingleQuoted;
                needsProcessing = escaped && (style == YamlScalarStyle.SingleQuoted || multiLine || yamlOnlyEscape);
            }
            else
            {
                if (!YamlCanStartPlain(c, flow, out bool needMore))
                {
                    if (needMore)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ThrowHelper.GetPrintableString(b));
                }

                if (!YamlScanPlain(ref c, indent, flow, singleLine: true, flow && YamlJsonComments, out start, out length, out _, out YamlPlainEnd end))
                {
                    return YamlStep.NeedMoreData;
                }

                if (!flow && end != YamlPlainEnd.KeyColon)
                {
                    YamlThrow(YamlGrammarError.ExpectedColon, c);
                }

                style = YamlScalarStyle.Plain;
                escaped = false;
                needsProcessing = false;
            }

            if (flow)
            {
                // The caller decides about ':' (it may be absent in flow mappings).
                TokenStartIndex = keyStart.Pos;
                YamlSetRawStart(rawStart, rawLine, rawColumn);
                YamlApplyProperties(props);
                ValueSpan = _buffer.Slice(start, length);
                _yamlValueWindowStart = start;
                ValueIsEscaped = escaped;
                _yamlNeedsProcessing = needsProcessing;
                _yamlStyle = style;
                _yamlIsAlias = alias;
                return YamlStep.Token;
            }

            // Block: ':' must follow on the same line, then whitespace or the end of the line.
            YamlSkipBlanks(ref c);
            int colon = YamlPeek(c.Pos);
            if (colon == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (colon != (byte)':')
            {
                YamlThrow(YamlGrammarError.ExpectedColon, c);
            }
            int follow = YamlFollowedBySpace(c.Pos + 1);
            if (follow == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (follow == 0)
            {
                YamlThrow(YamlGrammarError.ExpectedColon, c);
            }

            hadColon = true;
            TokenStartIndex = keyStart.Pos;
            YamlStep step = YamlEmitPropertyName(ref c, start, length, style, escaped, needsProcessing, props, rawStart, rawLine, rawColumn, consumeColon: true, keyStartOverride: keyStart.Pos);
            _yamlIsAlias = alias;
            return step;
        }

        private YamlStep YamlEmitPropertyName(
            ref YamlCursor c,
            int start,
            int length,
            YamlScalarStyle style,
            bool escaped,
            bool needsProcessing,
            in YamlProperties props,
            int rawStart,
            long rawLine,
            long rawColumn,
            bool consumeColon,
            int keyStartOverride = -1)
        {
            TokenStartIndex = keyStartOverride >= 0 ? keyStartOverride : c.Pos;
            if (consumeColon)
            {
                Debug.Assert(_buffer[c.Pos] == (byte)':');
                c.Pos++;
                c.Column++;
            }

            YamlSetRawStart(rawStart, rawLine, rawColumn);
            YamlApplyProperties(props);
            ValueSpan = _buffer.Slice(start, length);
            _yamlValueWindowStart = start;
            ValueIsEscaped = escaped;
            _yamlNeedsProcessing = needsProcessing;
            _yamlStyle = style;
            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.PropertyName);
            return YamlStep.Token;
        }

        /// <summary>
        /// '? key': the key node is read like a value after ': ' (any scalar, a block or flow collection, a compact
        /// collection, or nothing), with a copy of the reader, before the PropertyName is emitted.
        /// </summary>
        private YamlStep YamlParseExplicitBlockKey(ref YamlCursor c, int indent, int rawStart, long rawLine, long rawColumn)
        {
            YamlCursor question = c;
            c.Pos++;
            c.Column++;
            YamlCursor afterIndicator = c;
            YamlSkipBlanks(ref afterIndicator);

            if (YamlReadKeyNode(c, explicitBlock: true, indent, out YamlKeyNode node) != YamlStep.Token)
            {
                return YamlStep.NeedMoreData;
            }

            if (node.Complex)
            {
                YamlSetComplexKey(node);
            }
            else
            {
                YamlSetRawStart(question.Pos, question.Line, question.Column);
                if (node.EndsWithZeroWidth && ValueSpan.IsEmpty && _yamlAnchorLength == 0 && _yamlTagLength == 0)
                {
                    // An empty key ('?' alone): positioned after the indicator, as before M3d.
                    TokenStartIndex = afterIndicator.Pos;
                }
            }

            if (!node.EndsWithZeroWidth && !node.EndAtLineStart && node.End.Column != 0)
            {
                // Only a comment may follow the key node on its last line.
                YamlCursor rest = node.End;
                if (!YamlExpectLineEnd(ref rest))
                {
                    return YamlStep.NeedMoreData;
                }
            }

            // The value indicator is looked for by the next Read.
            _yaml.Set(YamlParseFlags.ExplicitKeyPending, true);
            YamlCommit(node.End, node.EndAtLineStart);
            YamlSetTokenType(YamlTokenType.PropertyName);
            return YamlStep.Token;
        }

        /// <summary>After '? key': the ':' value indicator at the mapping's indentation, or an implicit null value.</summary>
        private YamlStep YamlReadExplicitValue(ref YamlCursor c, int indent)
        {
            if (!YamlScanToContent(ref c, out YamlScan s, stopAtComment: YamlAllowComments))
            {
                return YamlStep.NeedMoreData;
            }

            if (s.IsComment)
            {
                return YamlEmitComment(ref c);
            }

            _yaml.Set(YamlParseFlags.ExplicitKeyPending, false);
            if (!s.AtEnd && s.LineStart && s.Indent == indent && _buffer[c.Pos] == (byte)':')
            {
                int follow = YamlFollowedBySpace(c.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (follow == 1)
                {
                    c.Pos++;
                    c.Column++;
                    return YamlReadBlockMappingValue(ref c, indent, compactAllowed: true);
                }
            }

            return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
        }

        /// <summary>After 'key:': the value on the same line, a nested node on the following lines, or null.</summary>
        private YamlStep YamlReadBlockMappingValue(ref YamlCursor c, int indent, bool compactAllowed)
        {
            int separationStart = c.Pos;
            if (!YamlScanToContent(ref c, out YamlScan s, stopAtComment: YamlAllowComments && !compactAllowed))
            {
                return YamlStep.NeedMoreData;
            }

            if (s.IsComment)
            {
                return YamlEmitComment(ref c);
            }

            if (s.AtEnd)
            {
                return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
            }

            if (!s.LineStart)
            {
                // After '? ' or an explicit key's ': ', a compact collection after a tab separator would use the tab
                // as indentation (as after '- ').
                s.TabInIndent = compactAllowed && _buffer.Slice(separationStart, c.Pos - separationStart).IndexOf((byte)'\t') >= 0;
                return YamlParseBlockNode(ref c, s, parentIndent: indent, allowBlockCollection: compactAllowed, allowIndentlessSequence: true);
            }

            int marker = YamlDocumentMarker(c, s.LineStart);
            if (marker < 0)
            {
                return YamlStep.NeedMoreData;
            }
            if (marker > 0)
            {
                return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
            }

            if (s.Indent > indent)
            {
                // A tab after the indentation is separation; it only matters if a block collection starts here.
                return YamlParseBlockNode(ref c, s, parentIndent: indent, allowBlockCollection: true, allowIndentlessSequence: true);
            }

            if (s.Indent == indent && _buffer[c.Pos] == (byte)'-')
            {
                int follow = YamlFollowedBySpace(c.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (follow == 1)
                {
                    if (s.TabInIndent)
                    {
                        YamlThrow(YamlGrammarError.TabIndentation, c);
                    }
                    return YamlParseBlockNode(ref c, s, parentIndent: indent, allowBlockCollection: true, allowIndentlessSequence: true);
                }
            }

            return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Block sequences

        private YamlStep YamlReadBlockSequence(ref YamlCursor c, int indent, bool indentless)
        {
            if (!YamlScanToContent(ref c, out YamlScan s, stopAtComment: YamlAllowComments))
            {
                return YamlStep.NeedMoreData;
            }

            if (s.IsComment)
            {
                return YamlEmitComment(ref c);
            }

            if (s.AtEnd)
            {
                return YamlEmitEnd(ref c, s.LineStart, consumesBracket: false);
            }

            int follow = YamlFollowedBySpace(c.Pos + 1);
            if (follow == -2)
            {
                return YamlStep.NeedMoreData;
            }
            bool isDash = _buffer[c.Pos] == (byte)'-' && follow == 1;

            if (s.LineStart)
            {
                int marker = YamlDocumentMarker(c, s.LineStart);
                if (marker < 0)
                {
                    return YamlStep.NeedMoreData;
                }
                if (marker > 0 || s.Indent < indent || (indentless && s.Indent == indent && !isDash))
                {
                    return YamlEmitEnd(ref c, s.LineStart, consumesBracket: false);
                }
                if (s.Indent > indent)
                {
                    YamlThrow(YamlGrammarError.BadIndentation, c);
                }
                if (s.TabInIndent)
                {
                    YamlThrow(YamlGrammarError.TabIndentation, c);
                }
            }
            else if (YamlEffectiveTokenType != YamlTokenType.StartArray)
            {
                YamlThrow(YamlGrammarError.UnexpectedContentAfterNode, c);
            }

            if (!isDash)
            {
                YamlThrow(YamlGrammarError.ExpectedSequenceEntry, c);
            }

            // Consume '-' and the separation after it; the entry node is part of this Read.
            c.Pos++;
            c.Column++;
            int separationStart = c.Pos;
            YamlSkipBlanks(ref c);
            bool tabSeparation = _buffer.Slice(separationStart, c.Pos - separationStart).IndexOf((byte)'\t') >= 0;

            int next = YamlPeek(c.Pos);
            if (next == -2)
            {
                return YamlStep.NeedMoreData;
            }

            if (next == -1 || YamlIsBreak((byte)next) || next == (byte)'#')
            {
                YamlCursor entry = c;
                if (!YamlScanToContent(ref entry, out YamlScan es, stopAtComment: false))
                {
                    return YamlStep.NeedMoreData;
                }

                if (es.AtEnd)
                {
                    return YamlEmitNull(entry, es.LineStart, default, entry.Pos, entry.Line, entry.Column);
                }

                int marker = YamlDocumentMarker(entry, es.LineStart);
                if (marker < 0)
                {
                    return YamlStep.NeedMoreData;
                }
                if (marker > 0 || es.Indent <= indent)
                {
                    return YamlEmitNull(entry, es.LineStart, default, entry.Pos, entry.Line, entry.Column);
                }
                c = entry;
                return YamlParseBlockNode(ref c, es, parentIndent: indent, allowBlockCollection: true, allowIndentlessSequence: false);
            }

            // A compact collection after a tab separator would use the tab as indentation.
            YamlScan same = new() { LineStart = false, TabInIndent = tabSeparation };
            return YamlParseBlockNode(ref c, same, parentIndent: indent, allowBlockCollection: true, allowIndentlessSequence: false);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Block nodes

        /// <summary>
        /// Reads the node that starts at <paramref name="c"/> in block context and emits its first token.
        /// </summary>
        /// <param name="c">Position of the node's first byte; advanced past what is consumed.</param>
        /// <param name="s">What the scan before the node found: whether it is at a line start, its indentation, tabs.</param>
        /// <param name="parentIndent">Indentation of the enclosing block collection (or of the context).</param>
        /// <param name="allowBlockCollection">Whether a block mapping or sequence may start here (fresh line or compact form).</param>
        /// <param name="allowIndentlessSequence">Whether a sequence at <paramref name="parentIndent"/> is allowed (mapping values).</param>
        private YamlStep YamlParseBlockNode(ref YamlCursor c, YamlScan s, int parentIndent, bool allowBlockCollection, bool allowIndentlessSequence)
        {
            int rawStart = c.Pos;
            long rawLine = c.Line;
            long rawColumn = c.Column;
            YamlProperties props = default;
            bool propsOnOwnLine = false;
            byte b = _buffer[c.Pos];

            if (b is (byte)'&' or (byte)'!')
            {
                if (!YamlScanProperties(ref c, ref props, flow: false))
                {
                    return YamlStep.NeedMoreData;
                }

                YamlSkipBlanks(ref c);
                int next = YamlPeek(c.Pos);
                if (next == -2)
                {
                    return YamlStep.NeedMoreData;
                }

                if (next == -1 || YamlIsBreak((byte)next) || next == (byte)'#')
                {
                    if (s.LineStart && YamlDraft13 && !(_bitStack.CurrentDepth == 0 && _yaml.Has(YamlParseFlags.ScopedValue)))
                    {
                        // YAML 1.3 drafts: properties alone on their line, after no indicator. The root of a reader over
                        // a sliced value is left alone: the slice does not say what came before it on its line.
                        YamlThrow(YamlGrammarError.Draft13PropertiesOnOwnLine, new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn });
                    }

                    // The node continues on the following lines, or is empty.
                    YamlCursor after = c;
                    if (!YamlScanToContent(ref after, out YamlScan s2, stopAtComment: false))
                    {
                        return YamlStep.NeedMoreData;
                    }

                    bool empty = s2.AtEnd;
                    if (!empty)
                    {
                        int marker = YamlDocumentMarker(after, s2.LineStart);
                        if (marker < 0)
                        {
                            return YamlStep.NeedMoreData;
                        }

                        bool dash = _buffer[after.Pos] == (byte)'-' && YamlFollowedBySpace(after.Pos + 1) == 1;
                        empty = marker > 0
                            || (s2.LineStart && s2.Indent < parentIndent + 1 && !(allowIndentlessSequence && dash && s2.Indent == parentIndent));
                    }

                    if (empty)
                    {
                        return YamlEmitNull(c, lineStart: false, props, rawStart, rawLine, rawColumn);
                    }

                    propsOnOwnLine = true;
                    c = after;
                    s = s2;
                    allowBlockCollection = true;
                }

                b = _buffer[c.Pos];

                while (propsOnOwnLine && b is (byte)'&' or (byte)'!' or (byte)'*')
                {
                    // Properties on their own line followed by more properties: either the first key of a block
                    // mapping (the first properties belong to the mapping), or more properties of the same node.
                    YamlStep keyStep = YamlParseMappingAfterOwnLineProperties(ref c, s, parentIndent, props, rawStart, rawLine, rawColumn, out bool isMapping);
                    if (isMapping || keyStep != YamlStep.Token)
                    {
                        return keyStep;
                    }

                    if (!YamlScanProperties(ref c, ref props, flow: false))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlSkipBlanks(ref c);
                    int more = YamlPeek(c.Pos);
                    if (more == -2)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (more == -1 || YamlIsBreak((byte)more) || more == (byte)'#')
                    {
                        if (YamlDraft13)
                        {
                            // A second line of properties: this one starts its line.
                            YamlThrow(YamlGrammarError.Draft13PropertiesOnOwnLine, c);
                        }

                        YamlCursor after = c;
                        if (!YamlScanToContent(ref after, out YamlScan s3, stopAtComment: false))
                        {
                            return YamlStep.NeedMoreData;
                        }
                        if (s3.AtEnd || YamlDocumentMarker(after, s3.LineStart) != 0 || (s3.LineStart && s3.Indent <= parentIndent))
                        {
                            return YamlEmitNull(c, lineStart: false, props, rawStart, rawLine, rawColumn);
                        }
                        c = after;
                        s = s3;
                    }
                    b = _buffer[c.Pos];
                }
            }

            int follow = b is (byte)'-' or (byte)'?' or (byte)':' ? YamlFollowedBySpace(c.Pos + 1) : 0;
            if (follow == -2)
            {
                return YamlStep.NeedMoreData;
            }

            switch (b)
            {
                case (byte)'*':
                {
                    if (props.Any)
                    {
                        YamlThrow(YamlGrammarError.PropertiesOnAlias, c);
                    }
                    int aliasKey = YamlProbeAliasKey(c, flow: false);
                    if (aliasKey == -2)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (aliasKey == 1)
                    {
                        // The first key of an implicit block mapping; the key itself is read by the next Read.
                        if (!allowBlockCollection)
                        {
                            YamlThrow(YamlGrammarError.MappingValueNotAllowed, c);
                        }
                        return YamlEmitImplicitMappingStart(c, s.LineStart, parentIndent, default, rawStart, rawLine, rawColumn, s.TabInIndent);
                    }
                    return YamlParseAlias(ref c);
                }

                case (byte)'[':
                case (byte)'{':
                    if (allowBlockCollection && !_readerOptions.DisallowImplicitCollectionKeys)
                    {
                        // A flow collection followed by ':' on its line is the first key of an implicit block
                        // mapping; the key itself is read by the next Read.
                        int collectionKey = YamlProbeCollectionKey(c.Pos, flow: false, out bool tooLong);
                        if (collectionKey == -2)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        YamlNoteTooLongKey(tooLong, rawLine);
                        if (collectionKey == 1)
                        {
                            if (YamlDraft13)
                            {
                                YamlThrow(YamlGrammarError.Draft13ImplicitCollectionKey, c);
                            }
                            return YamlEmitImplicitMappingStart(propsOnOwnLine ? c : new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn }, s.LineStart || propsOnOwnLine, parentIndent, propsOnOwnLine ? props : default, rawStart, rawLine, rawColumn, s.TabInIndent);
                        }
                    }
                    return YamlEmitFlowStart(ref c, props, rawStart, rawLine, rawColumn, b == (byte)'{');

                case (byte)'|':
                case (byte)'>':
                    if (YamlDraft13)
                    {
                        YamlCheckDraft13RootScalar(c, startsOnMarkerLine: false);
                    }
                    return YamlParseBlockScalar(ref c, parentIndent, props, rawStart, rawLine, rawColumn);

                case (byte)'\'':
                case (byte)'"':
                    return YamlParseBlockQuoted(ref c, s, parentIndent, allowBlockCollection, props, propsOnOwnLine, rawStart, rawLine, rawColumn);

                case (byte)'-' when follow == 1:
                    if (!allowBlockCollection || (props.Any && !propsOnOwnLine))
                    {
                        YamlThrow(YamlGrammarError.SequenceEntryNotAllowed, c);
                    }
                    if (c.Column <= parentIndent && !(allowIndentlessSequence && c.Column == parentIndent))
                    {
                        YamlThrow(YamlGrammarError.BadIndentation, c);
                    }
                    return YamlEmitBlockCollectionStart(c, s.LineStart || propsOnOwnLine, isObject: false, indentless: c.Column == parentIndent, props, rawStart, rawLine, rawColumn, s.TabInIndent);

                case (byte)':' when follow == 1 && props.Any && !propsOnOwnLine:
                    // '!!null : a': an empty key carrying the properties.
                    if (!allowBlockCollection)
                    {
                        YamlThrow(YamlGrammarError.MappingValueNotAllowed, c);
                    }
                    return YamlEmitImplicitMappingStart(new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn }, s.LineStart, parentIndent, default, rawStart, rawLine, rawColumn, s.TabInIndent);

                case (byte)'?' when follow == 1:
                case (byte)':' when follow == 1:
                    if (!allowBlockCollection || (props.Any && !propsOnOwnLine))
                    {
                        YamlThrow(YamlGrammarError.MappingValueNotAllowed, c);
                    }
                    if (c.Column <= parentIndent)
                    {
                        YamlThrow(YamlGrammarError.BadIndentation, c);
                    }
                    return YamlEmitBlockCollectionStart(c, s.LineStart || propsOnOwnLine, isObject: true, indentless: false, props, rawStart, rawLine, rawColumn, s.TabInIndent);
            }

            if (!YamlCanStartPlain(c, flow: false, out bool needMore))
            {
                if (needMore)
                {
                    return YamlStep.NeedMoreData;
                }
                YamlThrow(b is (byte)'@' or (byte)'`' or (byte)'%' ? YamlGrammarError.ReservedIndicator : YamlGrammarError.UnexpectedCharacter, c, ThrowHelper.GetPrintableString(b));
            }

            // A plain scalar, or the first key of an implicit block mapping.
            YamlCursor scalarStart = c;
            YamlCursor probe = c;
            if (!YamlScanPlain(ref probe, parentIndent, flow: false, singleLine: true, jsonComments: false, out _, out _, out _, out YamlPlainEnd firstLineEnd))
            {
                return YamlStep.NeedMoreData;
            }

            if (firstLineEnd == YamlPlainEnd.KeyColon)
            {
                if (!allowBlockCollection)
                {
                    YamlThrow(YamlGrammarError.MappingValueNotAllowed, probe);
                }

                return YamlEmitImplicitMappingStart(propsOnOwnLine ? scalarStart : new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn }, s.LineStart || propsOnOwnLine, parentIndent, propsOnOwnLine ? props : default, rawStart, rawLine, rawColumn, s.TabInIndent);
            }

            // The root node of a document is not inside a block collection: a JSON comment ends its plain scalar.
            bool jsonComments = _bitStack.CurrentDepth == 0 && YamlJsonCommentsAtDocumentLevel;
            if (!YamlScanPlain(ref c, parentIndent, flow: false, singleLine: false, jsonComments, out int start, out int length, out bool multiLine, out YamlPlainEnd end))
            {
                return YamlStep.NeedMoreData;
            }

            if (end == YamlPlainEnd.KeyColon)
            {
                YamlThrow(multiLine ? YamlGrammarError.MultilineImplicitKey : YamlGrammarError.MappingValueNotAllowed, c);
            }

            if (YamlDraft13)
            {
                YamlCheckDraft13RootScalar(scalarStart, startsOnMarkerLine: !s.LineStart);
            }
            return YamlEmitScalar(ref c, scalarStart.Pos, start, length, YamlScalarStyle.Plain, escaped: multiLine, needsProcessing: multiLine, multiLine, props, rawStart, rawLine, rawColumn);
        }

        private YamlStep YamlParseMappingAfterOwnLineProperties(ref YamlCursor c, in YamlScan s, int parentIndent, in YamlProperties mappingProps, int rawStart, long rawLine, long rawColumn, out bool isMapping)
        {
            isMapping = false;
            YamlCursor keyStart = c;
            YamlCursor probe = c;
            if (_buffer[probe.Pos] == (byte)'*')
            {
                // '&m' on its own line, then '*a : v': the first key of a mapping that carries the properties.
                // Otherwise the properties would belong to the alias, which cannot have any.
                int aliasKey = YamlProbeAliasKey(probe, flow: false);
                if (aliasKey == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (aliasKey == 0)
                {
                    YamlThrow(YamlGrammarError.PropertiesOnAlias, probe);
                }
                isMapping = true;
                return YamlEmitImplicitMappingStart(keyStart, lineStart: true, parentIndent, mappingProps, rawStart, rawLine, rawColumn, s.TabInIndent);
            }

            YamlProperties keyProps = default;
            if (!YamlScanProperties(ref probe, ref keyProps, flow: false))
            {
                return YamlStep.NeedMoreData;
            }

            int next = YamlPeek(probe.Pos);
            if (next == -2)
            {
                return YamlStep.NeedMoreData;
            }

            bool isKey = false;
            if (next >= 0 && !YamlIsBreak((byte)next) && next != (byte)'#')
            {
                byte b = (byte)next;
                int follow = YamlFollowedBySpace(probe.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }

                if (b == (byte)':' && follow == 1)
                {
                    isKey = true;
                }
                else if (b is (byte)'\'' or (byte)'"')
                {
                    if (!YamlScanQuoted(ref probe, parentIndent, flow: false, out _, out _, out bool multiLine, out _, out _))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlSkipBlanks(ref probe);
                    int colon = YamlPeek(probe.Pos);
                    if (colon == -2)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    isKey = !multiLine && colon == (byte)':' && YamlFollowedBySpace(probe.Pos + 1) == 1;
                }
                else if (b == (byte)'*')
                {
                    YamlThrow(YamlGrammarError.PropertiesOnAlias, probe);
                }
                else if (b is (byte)'[' or (byte)'{' && !_readerOptions.DisallowImplicitCollectionKeys)
                {
                    int collectionKey = YamlProbeCollectionKey(probe.Pos, flow: false, out bool tooLong);
                    if (collectionKey == -2)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlNoteTooLongKey(tooLong, probe.Line);
                    isKey = collectionKey == 1;
                    if (isKey && YamlDraft13)
                    {
                        YamlThrow(YamlGrammarError.Draft13ImplicitCollectionKey, probe);
                    }
                }
                else if (YamlCanStartPlain(probe, flow: false, out bool needMore))
                {
                    if (!YamlScanPlain(ref probe, parentIndent, flow: false, singleLine: true, jsonComments: false, out _, out _, out _, out YamlPlainEnd end))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    isKey = end == YamlPlainEnd.KeyColon;
                }
                else if (needMore)
                {
                    return YamlStep.NeedMoreData;
                }
            }

            if (!isKey)
            {
                // More properties of the same node (the caller merges them).
                return YamlStep.Token;
            }

            isMapping = true;
            return YamlEmitImplicitMappingStart(keyStart, lineStart: true, parentIndent, mappingProps, rawStart, rawLine, rawColumn, s.TabInIndent);
        }

        private YamlStep YamlParseBlockQuoted(ref YamlCursor c, in YamlScan s, int parentIndent, bool allowBlockCollection, in YamlProperties props, bool propsOnOwnLine, int rawStart, long rawLine, long rawColumn)
        {
            YamlCursor scalarStart = c;
            byte quote = _buffer[c.Pos];
            if (!YamlScanQuoted(ref c, parentIndent, flow: false, out int start, out int length, out bool multiLine, out bool escaped, out bool yamlOnlyEscape))
            {
                return YamlStep.NeedMoreData;
            }

            // Is this the key of an implicit block mapping?
            YamlCursor probe = c;
            YamlSkipBlanks(ref probe);
            int next = YamlPeek(probe.Pos);
            if (next == -2)
            {
                return YamlStep.NeedMoreData;
            }
            if (next == (byte)':')
            {
                int follow = YamlFollowedBySpace(probe.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (follow == 1)
                {
                    if (multiLine)
                    {
                        YamlThrow(YamlGrammarError.MultilineImplicitKey, scalarStart);
                    }
                    if (!allowBlockCollection)
                    {
                        YamlThrow(YamlGrammarError.MappingValueNotAllowed, probe);
                    }
                    return YamlEmitImplicitMappingStart(propsOnOwnLine ? scalarStart : new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn }, s.LineStart || propsOnOwnLine, parentIndent, propsOnOwnLine ? props : default, rawStart, rawLine, rawColumn, s.TabInIndent);
                }
            }

            if (YamlDraft13)
            {
                YamlCheckDraft13RootScalar(scalarStart, startsOnMarkerLine: !s.LineStart);
            }
            YamlScalarStyle style = quote == (byte)'"' ? YamlScalarStyle.DoubleQuoted : YamlScalarStyle.SingleQuoted;
            bool needsProcessing = escaped && (style == YamlScalarStyle.SingleQuoted || multiLine || yamlOnlyEscape);
            return YamlEmitScalar(ref c, scalarStart.Pos, start, length, style, escaped, needsProcessing, multiLine, props, rawStart, rawLine, rawColumn);
        }

        private YamlStep YamlEmitImplicitMappingStart(in YamlCursor at, bool lineStart, int parentIndent, in YamlProperties mappingProps, int rawStart, long rawLine, long rawColumn, bool tabInIndent)
        {
            if (at.Column <= parentIndent)
            {
                YamlThrow(YamlGrammarError.BadIndentation, at);
            }

            return YamlEmitBlockCollectionStart(at, lineStart, isObject: true, indentless: false, mappingProps, rawStart, rawLine, rawColumn, tabInIndent);
        }

        private YamlStep YamlEmitBlockCollectionStart(in YamlCursor at, bool lineStart, bool isObject, bool indentless, in YamlProperties props, int rawStart, long rawLine, long rawColumn, bool tabInIndent)
        {
            if (tabInIndent)
            {
                YamlThrow(YamlGrammarError.TabIndentation, at);
            }

            if (props.TagLength > 0)
            {
                YamlCoreTag tag = YamlClassifyTag(props);
                if (tag != YamlCoreTag.Other && tag != YamlCoreTag.NonSpecific && tag != (isObject ? YamlCoreTag.Map : YamlCoreTag.Seq))
                {
                    YamlThrowTagMismatch(props, at);
                }
            }

            YamlApplyProperties(props);
            if (indentless)
            {
                // An indentless sequence sits at its parent's indentation; a reader scoped to it must start just outside.
                _yamlParentIndentPlusOne--;
            }
            YamlPushCollection(isObject, ((int)at.Column << 1) | (indentless ? 1 : 0));
            YamlEmitZeroWidth(at, lineStart, isObject ? YamlTokenType.StartObject : YamlTokenType.StartArray);
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            return YamlStep.Token;
        }

        private YamlStep YamlEmitFlowStart(ref YamlCursor c, in YamlProperties props, int rawStart, long rawLine, long rawColumn, bool isObject)
        {
            if (props.TagLength > 0)
            {
                YamlCoreTag tag = YamlClassifyTag(props);
                if (tag != YamlCoreTag.Other && tag != YamlCoreTag.NonSpecific && tag != (isObject ? YamlCoreTag.Map : YamlCoreTag.Seq))
                {
                    YamlThrowTagMismatch(props, c);
                }
            }

            TokenStartIndex = c.Pos;
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            YamlApplyProperties(props);
            ValueSpan = _buffer.Slice(c.Pos, 1);
            _yamlValueWindowStart = c.Pos;
            c.Pos++;
            c.Column++;
            YamlPushCollection(isObject, -1);
            _yamlIsFlow = true;
            YamlCommit(c);
            YamlSetTokenType(isObject ? YamlTokenType.StartObject : YamlTokenType.StartArray);
            return YamlStep.Token;
        }
    }
}
