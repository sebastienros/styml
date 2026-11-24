// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        // Positions follow the JSON reader for JSON input: whitespace and ',' are consumed by the Read that produces
        // the next token, a PropertyName is consumed through its ':', and brackets are one-byte tokens.

        private YamlStep YamlReadFlow(ref YamlCursor c, bool isPair)
        {
            if (!YamlScanToFlowContent(ref c, out YamlScan s))
            {
                return YamlStep.NeedMoreData;
            }

            if (s.IsComment)
            {
                return YamlEmitComment(ref c);
            }

            if (s.AtEnd)
            {
                YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
            }

            byte b = _buffer[c.Pos];
            YamlTokenType effective = YamlEffectiveTokenType;

            if (isPair)
            {
                // An implicit single-pair mapping inside a flow sequence: key, value, then a zero-width end.
                if (effective == YamlTokenType.StartObject)
                {
                    return YamlParseFlowKey(ref c, inPair: true);
                }
                if (effective == YamlTokenType.PropertyName)
                {
                    if (b is (byte)',' or (byte)']')
                    {
                        return YamlEmitFlowImplicitNull(c);
                    }
                    YamlRequireValueAfterKey(c);
                    return YamlParseFlowNode(ref c, entryOfSequence: false);
                }
                if (b is (byte)',' or (byte)']')
                {
                    return YamlEmitEnd(ref c, lineStart: false, consumesBracket: false);
                }
                YamlThrow(YamlGrammarError.ExpectedFlowSeparator, c);
            }

            if (_inObject)
            {
                if (effective == YamlTokenType.PropertyName && _yaml.Has(YamlParseFlags.FlowColonPending))
                {
                    // The key's ':' follows the comment tokens just read.
                    Debug.Assert(b == (byte)':');
                    _yaml.Set(YamlParseFlags.FlowColonPending, false);
                    c.Pos++;
                    c.Column++;
                    if (!YamlScanToFlowContent(ref c, out s))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (s.IsComment)
                    {
                        return YamlEmitComment(ref c);
                    }
                    if (s.AtEnd)
                    {
                        YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                    }
                    b = _buffer[c.Pos];
                }

                if (effective == YamlTokenType.PropertyName)
                {
                    if (b is (byte)',' or (byte)'}')
                    {
                        return YamlEmitFlowImplicitNull(c);
                    }
                    YamlRequireValueAfterKey(c);
                    return YamlParseFlowNode(ref c, entryOfSequence: false);
                }

                if (effective != YamlTokenType.StartObject && _yaml.Has(YamlParseFlags.FlowSeparatorConsumed))
                {
                    // The ',' was read before the comment tokens just emitted.
                    _yaml.Set(YamlParseFlags.FlowSeparatorConsumed, false);
                    if (b == (byte)',')
                    {
                        YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
                    }
                }
                else if (effective != YamlTokenType.StartObject)
                {
                    // After a value: ',' or '}'.
                    if (b == (byte)'}')
                    {
                        return YamlEmitEnd(ref c, lineStart: false, consumesBracket: true);
                    }
                    if (b != (byte)',')
                    {
                        YamlThrow(YamlGrammarError.ExpectedFlowSeparator, c);
                    }

                    c.Pos++;
                    c.Column++;
                    if (!YamlScanToFlowContent(ref c, out s))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (s.IsComment)
                    {
                        _yaml.Set(YamlParseFlags.FlowSeparatorConsumed, true);
                        return YamlEmitComment(ref c);
                    }
                    if (s.AtEnd)
                    {
                        YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                    }
                    b = _buffer[c.Pos];
                    if (b == (byte)',')
                    {
                        YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
                    }
                }

                if (b == (byte)'}')
                {
                    return YamlEmitEnd(ref c, lineStart: false, consumesBracket: true);
                }
                if (b == (byte)',')
                {
                    YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
                }
                return YamlParseFlowKey(ref c, inPair: false);
            }

            // Flow sequence.
            if (effective != YamlTokenType.StartArray && _yaml.Has(YamlParseFlags.FlowSeparatorConsumed))
            {
                // The ',' was read before the comment tokens just emitted.
                _yaml.Set(YamlParseFlags.FlowSeparatorConsumed, false);
                if (b == (byte)',')
                {
                    YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
                }
            }
            else if (effective != YamlTokenType.StartArray)
            {
                if (b == (byte)']')
                {
                    return YamlEmitEnd(ref c, lineStart: false, consumesBracket: true);
                }
                if (b != (byte)',')
                {
                    YamlThrow(b == (byte)':' ? YamlKeyAfterNodeError(c) : YamlGrammarError.ExpectedFlowSeparator, c);
                }

                c.Pos++;
                c.Column++;
                if (!YamlScanToFlowContent(ref c, out s))
                {
                    return YamlStep.NeedMoreData;
                }
                if (s.IsComment)
                {
                    _yaml.Set(YamlParseFlags.FlowSeparatorConsumed, true);
                    return YamlEmitComment(ref c);
                }
                if (s.AtEnd)
                {
                    YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                }
                b = _buffer[c.Pos];
                if (b == (byte)',')
                {
                    YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
                }
            }

            if (b == (byte)']')
            {
                return YamlEmitEnd(ref c, lineStart: false, consumesBracket: true);
            }
            if (b == (byte)',')
            {
                YamlThrow(YamlGrammarError.UnexpectedCharacter, c, ",");
            }

            return YamlParseFlowNode(ref c, entryOfSequence: true);
        }

        private readonly void YamlRequireValueAfterKey(in YamlCursor c)
        {
            if (_yaml.Has(YamlParseFlags.FlowKeyWithoutValue))
            {
                YamlThrow(YamlGrammarError.ExpectedColon, c);
            }
        }

        private YamlStep YamlEmitFlowImplicitNull(in YamlCursor c)
        {
            _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
            return YamlEmitNull(c, lineStart: false, default, c.Pos, c.Line, c.Column);
        }

        /// <summary>
        /// Skips whitespace and comments inside a flow collection; continuation lines must be indented more than the
        /// enclosing block collection, and document markers are not allowed.
        /// </summary>
        private readonly bool YamlScanToFlowContent(ref YamlCursor c, out YamlScan s) =>
            YamlScanToFlowContent(ref c, out s, stopAtComment: YamlAllowComments);

        private readonly bool YamlScanToFlowContent(ref YamlCursor c, out YamlScan s, bool stopAtComment)
        {
            int startLine = (int)c.Line;
            if (!YamlScanToContent(ref c, out s, stopAtComment, jsonComments: YamlJsonComments))
            {
                return false;
            }

            // A comment token is not content: what follows it is checked by the next read. After a '/* */' that
            // crossed lines the content is not at the start of its line, and is not checked.
            if (!s.AtEnd && !s.IsComment && s.LineStart && c.Line != startLine)
            {
                int marker = YamlDocumentMarker(c, s.LineStart);
                if (marker < 0)
                {
                    return false;
                }
                if (marker > 0)
                {
                    YamlThrow(YamlGrammarError.DocumentMarkerInFlow, c);
                }
                if (s.Indent <= _yaml.BlockIndent)
                {
                    YamlThrow(YamlGrammarError.FlowIndentation, c);
                }
            }

            return true;
        }

        /// <summary>A flow mapping key (or the key of a pair inside a flow sequence) and its ':' when present.</summary>
        private YamlStep YamlParseFlowKey(ref YamlCursor c, bool inPair)
        {
            int rawStart = c.Pos;
            long rawLine = c.Line;
            long rawColumn = c.Column;
            YamlProperties props = default;
            byte b = _buffer[c.Pos];
            bool explicitKey = false;

            if (b == (byte)'?')
            {
                int follow = YamlFollowedBySpaceOrFlowIndicator(c.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (follow == 1)
                {
                    explicitKey = true;
                    c.Pos++;
                    c.Column++;
                    if (!YamlScanToFlowContent(ref c, out YamlScan s, stopAtComment: false))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (s.AtEnd)
                    {
                        YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                    }
                    rawStart = c.Pos;
                    rawLine = c.Line;
                    rawColumn = c.Column;
                    b = _buffer[c.Pos];
                }
            }

            if (b is (byte)'&' or (byte)'!')
            {
                if (!YamlScanProperties(ref c, ref props, flow: true))
                {
                    return YamlStep.NeedMoreData;
                }
                if (!YamlScanToFlowContent(ref c, out YamlScan s, stopAtComment: false))
                {
                    return YamlStep.NeedMoreData;
                }
                if (s.AtEnd)
                {
                    YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                }
                b = _buffer[c.Pos];
            }

            YamlCursor keyStart = c;
            int start;
            int length;
            YamlScalarStyle style;
            bool escaped;
            bool needsProcessing;
            bool jsonLike;
            bool multiLine;
            bool aliasKey = false;

            int followKey = b == (byte)':' ? YamlFollowedBySpaceOrFlowIndicator(c.Pos + 1) : 0;
            if (followKey == -2)
            {
                return YamlStep.NeedMoreData;
            }

            if (b is (byte)'[' or (byte)'{')
            {
                if (!explicitKey && _readerOptions.DisallowImplicitCollectionKeys)
                {
                    YamlThrow(YamlGrammarError.ImplicitCollectionKeyDisallowed, c);
                }
                return YamlParseFlowCollectionKey(ref c, inPair, explicitKey, rawStart, rawLine, rawColumn);
            }

            if (b == (byte)':' && followKey == 1 || YamlIsFlowIndicator(b))
            {
                // An empty key ('{: v}', '{? }', '[? : v]').
                start = c.Pos;
                length = 0;
                style = YamlScalarStyle.Plain;
                escaped = false;
                needsProcessing = false;
                jsonLike = false;
                multiLine = false;
            }
            else if (b == (byte)'*')
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
                jsonLike = false;
                multiLine = false;
                aliasKey = true;
            }
            else if (b is (byte)'|' or (byte)'>')
            {
                YamlThrow(YamlGrammarError.BlockInFlow, c);
                return default;
            }
            else if (b is (byte)'\'' or (byte)'"')
            {
                if (!YamlScanQuoted(ref c, _yaml.BlockIndent, flow: true, out start, out length, out multiLine, out escaped, out bool yamlOnlyEscape))
                {
                    return YamlStep.NeedMoreData;
                }
                style = b == (byte)'"' ? YamlScalarStyle.DoubleQuoted : YamlScalarStyle.SingleQuoted;
                needsProcessing = escaped && (style == YamlScalarStyle.SingleQuoted || multiLine || yamlOnlyEscape);
                jsonLike = true;
            }
            else
            {
                if (!YamlCanStartPlain(c, flow: true, out bool needMore))
                {
                    if (needMore)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    YamlThrow(b is (byte)'@' or (byte)'`' or (byte)'%' ? YamlGrammarError.ReservedIndicator : YamlGrammarError.UnexpectedCharacter, c, ThrowHelper.GetPrintableString(b));
                }
                if (!YamlScanPlain(ref c, _yaml.BlockIndent, flow: true, singleLine: inPair && !explicitKey, YamlJsonComments, out start, out length, out multiLine, out _))
                {
                    return YamlStep.NeedMoreData;
                }
                style = YamlScalarStyle.Plain;
                escaped = multiLine;
                needsProcessing = multiLine;
                jsonLike = false;
            }

            if (inPair && multiLine && !explicitKey)
            {
                YamlThrow(YamlGrammarError.MultilineImplicitKey, keyStart);
            }

            // The ':' may follow after separation (flow mappings allow line breaks; pairs do not).
            YamlCursor afterKey = c;
            YamlCursor probe = c;
            bool afterComment = false;
            if (inPair && !explicitKey)
            {
                YamlSkipBlanks(ref probe);
                if (probe.Pos >= _buffer.Length && !IsLastSpan)
                {
                    return YamlStep.NeedMoreData;
                }
            }
            else
            {
                if (!YamlScanToFlowContent(ref probe, out YamlScan ps))
                {
                    return YamlStep.NeedMoreData;
                }
                if (ps.IsComment)
                {
                    // CommentHandling.Allow: the comment tokens come after the key, so look past them for ':'.
                    if (!YamlScanToFlowContent(ref probe, out ps, stopAtComment: false))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    afterComment = true;
                }
                if (ps.AtEnd)
                {
                    YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, probe);
                }
            }

            bool hasColon = false;
            if (probe.Pos < _buffer.Length && _buffer[probe.Pos] == (byte)':')
            {
                int follow = YamlFollowedBySpaceOrFlowIndicator(probe.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                hasColon = follow == 1 || jsonLike;
            }

            if (!hasColon)
            {
                if (!explicitKey && !inPair && probe.Pos < _buffer.Length && _buffer[probe.Pos] is not ((byte)',' or (byte)'}'))
                {
                    YamlThrow(YamlGrammarError.ExpectedColon, probe);
                }
            }

            TokenStartIndex = keyStart.Pos;
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            YamlApplyProperties(props);
            ValueSpan = _buffer.Slice(start, length);
            _yamlValueWindowStart = start;
            ValueIsEscaped = escaped;
            _yamlNeedsProcessing = needsProcessing;
            _yamlStyle = style;
            _yamlIsAlias = aliasKey;

            if (hasColon && afterComment)
            {
                // The ':' is read after the comment tokens, by the value's Read.
                c = afterKey;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
                _yaml.Set(YamlParseFlags.FlowColonPending, true);
            }
            else if (hasColon)
            {
                c = probe;
                c.Pos++;
                c.Column++;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
            }
            else
            {
                c = afterKey;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, true);
            }

            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.PropertyName);
            return YamlStep.Token;
        }

        /// <summary>
        /// A collection used as a flow mapping key or as the key of a pair in a flow sequence: the node,
        /// properties included, is read with a copy of the reader, then ':' is looked for as after a YAML-like key.
        /// </summary>
        private YamlStep YamlParseFlowCollectionKey(ref YamlCursor c, bool inPair, bool explicitKey, int rawStart, long rawLine, long rawColumn)
        {
            YamlCursor nodeStart = new() { Pos = rawStart, Line = rawLine, Column = rawColumn };
            if (YamlReadKeyNode(nodeStart, explicitBlock: false, _yaml.BlockIndent, out YamlKeyNode node) != YamlStep.Token)
            {
                return YamlStep.NeedMoreData;
            }
            if (inPair && !explicitKey && node.End.Line != nodeStart.Line)
            {
                YamlThrow(YamlGrammarError.MultilineImplicitKey, nodeStart);
            }

            // The ':' may follow after separation (flow mappings allow line breaks; pairs do not).
            YamlCursor afterKey = node.End;
            YamlCursor probe = node.End;
            bool afterComment = false;
            if (inPair && !explicitKey)
            {
                YamlSkipBlanks(ref probe);
                if (probe.Pos >= _buffer.Length && !IsLastSpan)
                {
                    return YamlStep.NeedMoreData;
                }
            }
            else
            {
                if (!YamlScanToFlowContent(ref probe, out YamlScan ps))
                {
                    return YamlStep.NeedMoreData;
                }
                if (ps.IsComment)
                {
                    // CommentHandling.Allow: the comment tokens come after the key, so look past them for ':'.
                    if (!YamlScanToFlowContent(ref probe, out ps, stopAtComment: false))
                    {
                        return YamlStep.NeedMoreData;
                    }
                    afterComment = true;
                }
                if (ps.AtEnd)
                {
                    YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, probe);
                }
            }

            // After a collection (JSON-like), ':' may be adjacent to the value.
            bool hasColon = probe.Pos < _buffer.Length && _buffer[probe.Pos] == (byte)':';
            if (!hasColon && !explicitKey && !inPair && probe.Pos < _buffer.Length && _buffer[probe.Pos] is not ((byte)',' or (byte)'}'))
            {
                YamlThrow(YamlGrammarError.ExpectedColon, probe);
            }

            YamlSetComplexKey(node);
            if (hasColon && afterComment)
            {
                // The ':' is read after the comment tokens, by the value's Read.
                c = afterKey;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
                _yaml.Set(YamlParseFlags.FlowColonPending, true);
            }
            else if (hasColon)
            {
                c = probe;
                c.Pos++;
                c.Column++;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
            }
            else
            {
                c = afterKey;
                _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, true);
            }

            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.PropertyName);
            return YamlStep.Token;
        }

        /// <summary>A flow node: a mapping value, or an entry of a flow sequence (which may turn out to be a pair).</summary>
        private YamlStep YamlParseFlowNode(ref YamlCursor c, bool entryOfSequence)
        {
            int rawStart = c.Pos;
            long rawLine = c.Line;
            long rawColumn = c.Column;
            byte b = _buffer[c.Pos];

            if (entryOfSequence)
            {
                int follow = YamlFollowedBySpaceOrFlowIndicator(c.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (b == (byte)'?' && follow == 1 || b == (byte)':' && follow == 1)
                {
                    return YamlEmitFlowPairStart(c);
                }
            }

            YamlProperties props = default;
            if (b is (byte)'&' or (byte)'!')
            {
                if (!YamlScanProperties(ref c, ref props, flow: true))
                {
                    return YamlStep.NeedMoreData;
                }
                if (!YamlScanToFlowContent(ref c, out YamlScan s, stopAtComment: false))
                {
                    return YamlStep.NeedMoreData;
                }
                if (s.AtEnd)
                {
                    YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, c);
                }
                b = _buffer[c.Pos];

                if (b is (byte)',' or (byte)']' or (byte)'}')
                {
                    // An empty node with properties.
                    return YamlEmitNull(c, lineStart: false, props, rawStart, rawLine, rawColumn);
                }
                if (entryOfSequence && b == (byte)':')
                {
                    int follow = YamlFollowedBySpaceOrFlowIndicator(c.Pos + 1);
                    if (follow == -2)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (follow == 1)
                    {
                        // '[!!str : v]': an empty key with properties.
                        return YamlEmitFlowPairStart(new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn });
                    }
                }
            }

            YamlCursor scalarStart = c;
            switch (b)
            {
                case (byte)'*':
                    if (props.Any)
                    {
                        YamlThrow(YamlGrammarError.PropertiesOnAlias, c);
                    }
                    if (entryOfSequence)
                    {
                        // '[*a : v]': a single-pair mapping whose key is the alias.
                        int aliasKey = YamlProbeAliasKey(c, flow: true);
                        if (aliasKey == -2)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        if (aliasKey == 1)
                        {
                            return YamlEmitFlowPairStart(c);
                        }
                    }
                    return YamlParseAlias(ref c);

                case (byte)'[':
                case (byte)'{':
                    if (entryOfSequence && !_readerOptions.DisallowImplicitCollectionKeys)
                    {
                        // '[[a]: b]': a single-pair mapping whose key is the collection.
                        int collectionKey = YamlProbeCollectionKey(c.Pos, flow: true, out bool tooLong);
                        if (collectionKey == -2)
                        {
                            return YamlStep.NeedMoreData;
                        }
                        YamlNoteTooLongKey(tooLong, rawLine);
                        if (collectionKey == 1)
                        {
                            return YamlEmitFlowPairStart(new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn });
                        }
                    }
                    return YamlEmitFlowStart(ref c, props, rawStart, rawLine, rawColumn, b == (byte)'{');

                case (byte)'|':
                case (byte)'>':
                    YamlThrow(YamlGrammarError.BlockInFlow, c);
                    break;

                case (byte)'\'':
                case (byte)'"':
                    {
                        if (!YamlScanQuoted(ref c, _yaml.BlockIndent, flow: true, out int qStart, out int qLength, out bool qMultiLine, out bool qEscaped, out bool yamlOnlyEscape))
                        {
                            return YamlStep.NeedMoreData;
                        }

                        if (entryOfSequence && !qMultiLine)
                        {
                            YamlCursor probe = c;
                            YamlSkipBlanks(ref probe);
                            if (probe.Pos >= _buffer.Length && !IsLastSpan)
                            {
                                return YamlStep.NeedMoreData;
                            }
                            if (probe.Pos < _buffer.Length && _buffer[probe.Pos] == (byte)':')
                            {
                                // A JSON-like key: ':' may be adjacent.
                                return YamlEmitFlowPairStart(new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn });
                            }
                        }

                        YamlScalarStyle style = b == (byte)'"' ? YamlScalarStyle.DoubleQuoted : YamlScalarStyle.SingleQuoted;
                        bool needsProcessing = qEscaped && (style == YamlScalarStyle.SingleQuoted || qMultiLine || yamlOnlyEscape);
                        return YamlEmitScalar(ref c, scalarStart.Pos, qStart, qLength, style, qEscaped, needsProcessing, qMultiLine, props, rawStart, rawLine, rawColumn);
                    }
            }

            {
                int follow = YamlFollowedBySpace(c.Pos + 1);
                if (follow == -2)
                {
                    return YamlStep.NeedMoreData;
                }
                if (b == (byte)'-' && follow == 1)
                {
                    YamlThrow(YamlGrammarError.BlockInFlow, c);
                }
            }

            if (!YamlCanStartPlain(c, flow: true, out bool needMore))
            {
                if (needMore)
                {
                    return YamlStep.NeedMoreData;
                }
                YamlThrow(b is (byte)'@' or (byte)'`' or (byte)'%' ? YamlGrammarError.ReservedIndicator : YamlGrammarError.UnexpectedCharacter, c, ThrowHelper.GetPrintableString(b));
            }

            if (entryOfSequence)
            {
                YamlCursor probe = c;
                if (!YamlScanPlain(ref probe, _yaml.BlockIndent, flow: true, singleLine: true, YamlJsonComments, out _, out _, out _, out YamlPlainEnd lineEnd))
                {
                    return YamlStep.NeedMoreData;
                }
                if (lineEnd == YamlPlainEnd.KeyColon)
                {
                    return YamlEmitFlowPairStart(new YamlCursor { Pos = rawStart, Line = rawLine, Column = rawColumn });
                }
            }

            if (!YamlScanPlain(ref c, _yaml.BlockIndent, flow: true, singleLine: false, YamlJsonComments, out int start, out int length, out bool multiLine, out YamlPlainEnd end))
            {
                return YamlStep.NeedMoreData;
            }

            if (end == YamlPlainEnd.KeyColon && !entryOfSequence)
            {
                // '{a: b: c}'
                YamlThrow(YamlGrammarError.MappingValueNotAllowed, c);
            }
            if (end == YamlPlainEnd.KeyColon && multiLine)
            {
                YamlThrow(YamlGrammarError.MultilineImplicitKey, scalarStart);
            }

            return YamlEmitScalar(ref c, scalarStart.Pos, start, length, YamlScalarStyle.Plain, escaped: multiLine, needsProcessing: multiLine, multiLine, props, rawStart, rawLine, rawColumn);
        }

        private YamlStep YamlEmitFlowPairStart(in YamlCursor at)
        {
            YamlPushCollection(isObject: true, -2);
            _yamlIsFlow = true;
            return YamlEmitZeroWidth(at, lineStart: false, YamlTokenType.StartObject);
        }

        /// <summary>Like <see cref="YamlFollowedBySpace"/>, but a flow indicator also counts as a separator.</summary>
        private readonly int YamlFollowedBySpaceOrFlowIndicator(int pos)
        {
            int next = YamlPeek(pos);
            if (next == -2)
            {
                return -2;
            }
            return next == -1 || YamlIsWhiteSpaceOrBreak((byte)next) || YamlIsFlowIndicator((byte)next) ? 1 : 0;
        }
    }
}
