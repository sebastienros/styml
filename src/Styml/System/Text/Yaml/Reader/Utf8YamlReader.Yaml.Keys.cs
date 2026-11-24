// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        // Key nodes beyond a single-line scalar: explicit keys of any
        // shape, and collections used as keys (D10: one PropertyName with KeyKind.Complex).

        /// <summary>
        /// Upper bound of an implicit key, in characters (YAML 1.2 sections 7.4.2 and 8.2.2; libyaml's simple keys).
        /// </summary>
        private const int YamlImplicitKeyMaxCharacters = 1024;


        private struct YamlKeyNode
        {
            /// <summary>First byte of the node, properties included.</summary>
            public YamlCursor Start;

            /// <summary>Position after the node, where the copy of the reader committed its last token.</summary>
            public YamlCursor End;

            public bool EndAtLineStart;

            /// <summary>The node ended with a token that consumed nothing (a block collection end, an empty node).</summary>
            public bool EndsWithZeroWidth;

            /// <summary>The node is a collection (the key is <see cref="YamlKeyKind.Complex"/>).</summary>
            public bool Complex;

            /// <summary>End of the node's last content: the end of its last token that consumed bytes, without the
            /// blanks, line breaks and comments a key or an empty node reads past (an empty node ends after its
            /// indicator and properties).</summary>
            public int RawEnd;

            /// <summary>Indentation the key node's first token belongs to (+1): one less for a zero-indented sequence.</summary>
            public int ParentIndentPlusOne;
        }

        /// <summary>
        /// Reads one key node with a copy of this reader, comments skipped: the node is validated with document
        /// positions and MaxDepth, and its end is found before the PropertyName is emitted. For a scalar node the
        /// token fields of this reader (value, style, properties, positions) are set from it; for a collection the
        /// caller emits a Complex key with <see cref="YamlSetComplexKey"/>.
        /// </summary>
        /// <param name="start">The node's first byte (its properties, or the content).</param>
        /// <param name="explicitBlock">The node follows '? ' in a block mapping (read like a value after ': ');
        /// otherwise it is a flow node.</param>
        /// <param name="indent">Indentation of the enclosing block mapping.</param>
        /// <param name="node">The key node read.</param>
        private YamlStep YamlReadKeyNode(in YamlCursor start, bool explicitBlock, int indent, out YamlKeyNode node)
        {
            node = default;
            node.Start = start;

            // A key nested in a key reads its node with one more copy, one level of recursion each. Raising MaxDepth
            // allows deep nesting, so the stack is checked: it must end in an exception the caller can catch.
            YamlEnsureStackForKeyNode(start);

            Utf8YamlReader copy = this;
            copy.YamlProbeShareReadOnly();
            copy._readerOptions.CommentHandling = YamlCommentHandling.Skip;
            copy._consumed = start.Pos;
            copy._lineNumber = start.Line;
            copy._bytePositionInLine = start.Column;
            copy._yaml.Set(YamlParseFlags.ContentAtLineStart, false);
            copy.YamlResetTokenFields();

            YamlCursor c = start;
            YamlStep step = explicitBlock
                ? copy.YamlReadBlockMappingValue(ref c, indent, compactAllowed: true)
                : copy.YamlParseFlowNode(ref c, entryOfSequence: false);
            if (step != YamlStep.Token)
            {
                return YamlStep.NeedMoreData;
            }

            int baseDepth = _bitStack.CurrentDepth;
            if (copy._tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                // The key node starts at the collection's first byte (its properties included), which for an explicit
                // key may be on a line after the '?'.
                node.Start = new YamlCursor { Pos = copy._yamlRawStart, Line = copy._yamlRawLine, Column = copy._yamlRawColumn };
                node.Complex = true;
                node.RawEnd = copy._consumed;
                node.ParentIndentPlusOne = copy._yamlParentIndentPlusOne;
                while (copy._bitStack.CurrentDepth > baseDepth)
                {
                    copy.YamlResetTokenFields();
                    step = copy.YamlReadCore();
                    if (step == YamlStep.NeedMoreData)
                    {
                        return YamlStep.NeedMoreData;
                    }
                    if (step == YamlStep.EndOfInput)
                    {
                        YamlThrow(YamlGrammarError.UnexpectedEndOfFlow, new YamlCursor { Pos = copy._consumed, Line = copy._lineNumber, Column = copy._bytePositionInLine });
                    }
                    if (copy._tokenType == YamlTokenType.PropertyName && copy._yamlStyle is not (YamlScalarStyle.Literal or YamlScalarStyle.Folded))
                    {
                        // A key token reads past its own bytes to its ':', and an explicit key with no ':' on its line
                        // reads the blanks, line breaks and comments after it: the key node's content ends at the key's
                        // bytes (its closing quote included), or at the ':' and properties that follow them.
                        int quote = copy._yamlStyle is YamlScalarStyle.SingleQuoted or YamlScalarStyle.DoubleQuoted ? 1 : 0;
                        int keyEnd = Math.Min(copy._yamlValueWindowStart + copy.YamlValueBytes.Length + quote, copy._consumed);
                        node.RawEnd = Math.Max(node.RawEnd, copy.YamlContentEnd(Math.Max(node.RawEnd, keyEnd), copy._consumed));
                    }
                    else if (copy._consumed > copy.TokenStartIndex)
                    {
                        node.RawEnd = Math.Max(node.RawEnd, copy._consumed);
                    }
                    else if (copy._tokenType is not (YamlTokenType.EndObject or YamlTokenType.EndArray))
                    {
                        // An empty node (an entry '-' or a key '?' with no content) still consumed its indicator and
                        // properties, then read past the blanks, line breaks and comments that follow them: the key
                        // ends after the indicator and properties.
                        node.RawEnd = Math.Max(node.RawEnd, copy.YamlContentEnd(node.RawEnd, copy._consumed));
                    }
                }
            }
            else
            {
                Debug.Assert(copy._tokenType is YamlTokenType.String or YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null or YamlTokenType.Alias);
                TokenStartIndex = copy.TokenStartIndex;
                ValueSpan = copy.ValueSpan;
                _yamlValueWindowStart = copy._yamlValueWindowStart;
                ValueIsEscaped = copy.ValueIsEscaped;
                _yamlNeedsProcessing = copy._yamlNeedsProcessing;
                _yamlStyle = copy._yamlStyle;
                _yamlChomping = copy._yamlChomping;
                _yamlScalarIndent = copy._yamlScalarIndent;
                _yamlAnchorStart = copy._yamlAnchorStart;
                _yamlAnchorLength = copy._yamlAnchorLength;
                _yamlTagStart = copy._yamlTagStart;
                _yamlTagLength = copy._yamlTagLength;
                _yamlDocumentLine = copy._yamlDocumentLine;
                _yamlIsAlias = copy._tokenType == YamlTokenType.Alias;
                _yamlRawStart = copy._yamlRawStart;
                _yamlRawLine = copy._yamlRawLine;
                _yamlRawColumn = copy._yamlRawColumn;
            }

            _yamlProbeBytes = copy._yamlProbeBytes;
            node.End = new YamlCursor { Pos = copy._consumed, Line = copy._lineNumber, Column = copy._bytePositionInLine };
            node.EndAtLineStart = copy._yaml.Has(YamlParseFlags.ContentAtLineStart);
            node.EndsWithZeroWidth = copy._consumed == copy.TokenStartIndex;
            return YamlStep.Token;
        }

        /// <summary>
        /// End of the last byte in [<paramref name="from"/>, <paramref name="to"/>) that is not a blank, a line break
        /// or part of a comment; <paramref name="from"/> when there is none.
        /// </summary>
        private readonly int YamlContentEnd(int from, int to)
        {
            int end = from;
            for (int i = from; i < to; i++)
            {
                byte b = _buffer[i];
                if (YamlIsWhiteSpaceOrBreak(b))
                {
                    continue;
                }

                if (b == (byte)'#' && (i == 0 || YamlIsWhiteSpaceOrBreak(_buffer[i - 1])))
                {
                    while (i + 1 < to && !YamlIsBreak(_buffer[i + 1]))
                    {
                        i++;
                    }
                    continue;
                }

                end = i + 1;
            }
            return end;
        }

        private readonly void YamlEnsureStackForKeyNode(in YamlCursor at)
        {
#if NET
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                YamlThrow(YamlGrammarError.KeysNestedTooDeeply, at);
            }
#else
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                YamlThrow(YamlGrammarError.KeysNestedTooDeeply, at);
            }
#endif
        }

        /// <summary>
        /// Sets the token fields of a Complex key: ValueSpan is the raw key node, up to <see cref="YamlKeyNode.RawEnd"/>.
        /// Nothing more is trimmed: a block scalar's trailing line breaks are its content (keep chomping).
        /// </summary>
        private void YamlSetComplexKey(in YamlKeyNode node)
        {
            int start = node.Start.Pos;
            int end = node.RawEnd;

            TokenStartIndex = start;
            YamlSetRawStart(start, node.Start.Line, node.Start.Column);
            ValueSpan = _buffer.Slice(start, end - start);
            _yamlValueWindowStart = start;
            ValueIsEscaped = true;
            _yamlNeedsProcessing = true;
            _yamlStyle = YamlScalarStyle.None;
            _yamlAnchorLength = 0;
            _yamlTagLength = 0;
            _yamlDocumentLine = 0;
            _yamlIsAlias = false;
            _yamlIsComplexKey = true;
            _yamlParentIndentPlusOne = node.ParentIndentPlusOne;
        }

        /// <summary>Checks the limits of an implicit collection key: one line, at most 1024 characters.</summary>
        private readonly void YamlCheckImplicitCollectionKey(in YamlKeyNode node)
        {
            if (node.End.Line != node.Start.Line)
            {
                YamlThrow(YamlGrammarError.MultilineImplicitKey, node.Start);
            }
            if (YamlCountCharacters(_buffer.Slice(node.Start.Pos, node.RawEnd - node.Start.Pos)) > YamlImplicitKeyMaxCharacters)
            {
                YamlThrow(YamlGrammarError.ImplicitKeyTooLong, node.Start);
            }
        }

        private static int YamlCountCharacters(ReadOnlySpan<byte> utf8)
        {
            int count = 0;
            foreach (byte b in utf8)
            {
                if ((b & 0xC0) != 0x80)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// M5e: the serializer's key reader. When the key holds an anchor, the operation's anchor table exists
        /// before the key is read (it is otherwise made at the first anchor this reader meets), and the source is stored,
        /// so that an anchor in a key resolves for later aliases.
        /// </summary>
        internal Utf8YamlReader CreateKeyReaderWithAnchors()
        {
            if (_readerOptions.RecordAnchors && KeyKind == YamlKeyKind.Complex && YamlValueBytes.IndexOf((byte)'&') >= 0)
            {
                _yaml.AnchorContext ??= new YamlAnchorContext(new YamlAnchorTable(), 0, isSource: true);
                YamlStoreSource();
            }

            return CreateKeyReader();
        }

        /// <summary>
        /// Returns a reader over the current mapping key when the key is a collection
        /// (<see cref="KeyKind"/> is <see cref="YamlKeyKind.Complex"/>): it reads the key node's own tokens, from its
        /// start token to its end token, after which <see cref="Read"/> returns <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The returned reader has this reader's options (schema, <see cref="YamlReaderOptions.AcceptLenientScalars"/>,
        /// comment handling), the document's <c>%TAG</c> handles, and the key's first line and column, so positions and
        /// errors point into the document. Its <see cref="YamlReaderOptions.MaxDepth"/> is this reader's less the
        /// current depth, so a key cannot nest deeper than a value at the same place. <see cref="TokenStartIndex"/>
        /// and <see cref="BytesConsumed"/> count from the key's first byte.
        /// </para>
        /// <para>
        /// The whole key is in this reader's buffer when its <see cref="YamlTokenType.PropertyName"/> token is
        /// returned, also over a multi-segment input, so the returned reader always reads a contiguous span. It is
        /// valid until this reader moves. Anchors and aliases in the key are returned as tokens and are not resolved.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The current token is not a <see cref="YamlTokenType.PropertyName"/> whose key is a collection: a scalar key is
        /// read from the token itself, and an alias key names its anchored node.
        /// </exception>
        public readonly Utf8YamlReader CreateKeyReader()
        {
            if (KeyKind != YamlKeyKind.Complex)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedComplexKey();
            }

            // M5e: the key's depth counts towards this reader's bound.
            YamlReaderOptions options = _readerOptions;
            int maxDepth = options.MaxDepth == 0 ? YamlReaderOptions.DefaultMaxDepth : options.MaxDepth;
            options.MaxDepth = Math.Max(1, maxDepth - CurrentDepth);

            var state = new YamlReaderState(
                lineNumber: _yamlRawLine,
                bytePositionInLine: _yamlRawColumn,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: options,
                bitStack: default,
                yaml: YamlParseState.ForScopedValue(YamlTokenParentIndent, _yaml.TagDirectives, _yaml.AnchorContext?.ForSlice(YamlRawTokenStart)));

            // M5e: under the serializer the key reader shares the operation's anchor table (M5b), rebased on the key's first
            // byte, so an anchor in a key is recorded where it is and an alias in a key resolves; a user's reader has none.
            return new Utf8YamlReader(YamlValueBytes, isFinalBlock: true, state);
        }
    }
}
