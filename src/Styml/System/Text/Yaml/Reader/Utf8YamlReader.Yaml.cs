// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    // YAML 1.2 grammar for Utf8YamlReader. This file holds the shared machinery;
    // the grammar is split across Utf8YamlReader.Yaml.Documents/.Block/.Flow/.Scalars.cs.
    //
    // Every Read is atomic: the grammar works on a local cursor, and the reader fields only change when a token is
    // emitted. When the buffer ends before the token can be decided (and IsFinalBlock is false), the reader state is
    // restored and Read returns false having consumed nothing.
    public ref partial struct Utf8YamlReader
    {
        private YamlParseState _yaml;

        // Token-local YAML information (valid for the current token only, not part of YamlReaderState).
        private YamlScalarStyle _yamlStyle;
        private int _yamlAnchorStart;
        private int _yamlAnchorLength;
        private int _yamlTagStart;
        private int _yamlTagLength;
        private bool _yamlIsFlow;
        private bool _yamlIsAlias;
        private bool _yamlIsComplexKey;
        private bool _yamlNumberNotJson;
        private bool _yamlNeedsProcessing;
        private byte _yamlChomping;
        private int _yamlScalarIndent;
        private int _yamlRawStart;
        private long _yamlRawLine;
        private long _yamlRawColumn;
        private int _yamlParentIndentPlusOne;

        /// <summary>
        /// Gets the presentation style of the current scalar or mapping key token, or <see cref="YamlScalarStyle.None"/>
        /// for other tokens.
        /// </summary>
        public readonly YamlScalarStyle ScalarStyle => _yamlStyle;

        /// <summary>Gets the anchor name (without '&amp;') of the current node, or an empty span.</summary>
        public readonly ReadOnlySpan<byte> Anchor => _yamlAnchorLength == 0 ? default
            : _yamlWindowIsCopy ? YamlWindowSliceAsInput(_yamlAnchorStart, _yamlAnchorLength) : _buffer.Slice(_yamlAnchorStart, _yamlAnchorLength);

        /// <summary>Gets the tag of the current node as written in the input (for example <c>!!str</c>), or an empty span.</summary>
        public readonly ReadOnlySpan<byte> Tag => _yamlTagLength == 0 ? default
            : _yamlWindowIsCopy ? YamlWindowSliceAsInput(_yamlTagStart, _yamlTagLength) : _buffer.Slice(_yamlTagStart, _yamlTagLength);

        /// <summary>
        /// Gets the tag of the current node with its handle expanded (YAML 1.2 section 6.8.2), or <see langword="null"/>
        /// when the node has no tag: <c>!e!foo</c> after <c>%TAG !e! tag:example.com,2000:</c> gives
        /// <c>tag:example.com,2000:foo</c>, <c>!!str</c> gives <c>tag:yaml.org,2002:str</c>, a percent-encoded suffix is
        /// decoded, a verbatim tag (<c>!&lt;tag:x&gt;</c>) gives its content and the non-specific <c>!</c> stays <c>!</c>.
        /// <see cref="Tag"/> keeps the tag as written.
        /// </summary>
        /// <remarks>This method allocates the returned string; <see cref="TryCopyExpandedTag"/> does not allocate.</remarks>
        public readonly string? GetExpandedTag()
        {
            if (_yamlTagLength == 0)
            {
                return null;
            }

            Span<byte> buffer = stackalloc byte[128];
            int length = YamlTagDirectives.Expand(_yaml.TagDirectives, Tag, buffer);
            if (length > buffer.Length)
            {
                buffer = new byte[length];
                YamlTagDirectives.Expand(_yaml.TagDirectives, Tag, buffer);
            }
            return YamlReaderHelper.TranscodeHelper(buffer.Slice(0, length));
        }

        /// <summary>
        /// Copies the UTF-8 bytes of the current node's expanded tag (see <see cref="GetExpandedTag"/>) to
        /// <paramref name="utf8Destination"/>. Returns <see langword="false"/>, and writes nothing, when the destination
        /// is too short; <paramref name="bytesWritten"/> is 0 when the node has no tag.
        /// </summary>
        public readonly bool TryCopyExpandedTag(Span<byte> utf8Destination, out int bytesWritten)
        {
            if (_yamlTagLength == 0)
            {
                bytesWritten = 0;
                return true;
            }

            Span<byte> scratch = stackalloc byte[128];
            int length = YamlTagDirectives.Expand(_yaml.TagDirectives, Tag, scratch);
            if (length > utf8Destination.Length)
            {
                bytesWritten = 0;
                return false;
            }
            if (length <= scratch.Length)
            {
                scratch.Slice(0, length).CopyTo(utf8Destination);
            }
            else
            {
                YamlTagDirectives.Expand(_yaml.TagDirectives, Tag, utf8Destination);
            }
            bytesWritten = length;
            return true;
        }

        /// <summary>
        /// False when the current number must not go through the JSON number parser first: a YAML 1.1 leading-zero
        /// octal (<c>0777</c>), which that parser would read as decimal.
        /// </summary>
        internal readonly bool YamlJsonNumberFastPath => !_yamlNumberNotJson;

        /// <summary>The %TAG handles of the current document, for readers scoped to one value.</summary>
        internal readonly YamlTagDirectives? YamlTagDirectives => _yaml.TagDirectives;

        /// <summary>Gets whether the current collection token comes from a flow collection (<c>{}</c> or <c>[]</c>).</summary>
        public readonly bool IsFlow => _yamlIsFlow;

        /// <summary>
        /// Gets what the current <see cref="YamlTokenType.PropertyName"/> token stands for:
        /// <see cref="YamlKeyKind.Scalar"/> for an ordinary key, <see cref="YamlKeyKind.Alias"/> for an alias key
        /// (<c>*name : value</c>, whose <see cref="ValueSpan"/> is the anchor name), <see cref="YamlKeyKind.Complex"/> for
        /// a collection used as a key (<c>[a, b]: value</c>, whose <see cref="ValueSpan"/> is the key's raw YAML), or
        /// <see cref="YamlKeyKind.None"/> when the current token is not a key. The string accessors refuse alias and
        /// complex keys.
        /// </summary>
        public readonly YamlKeyKind KeyKind =>
            _tokenType != YamlTokenType.PropertyName ? YamlKeyKind.None
            : _yamlIsAlias ? YamlKeyKind.Alias
            : _yamlIsComplexKey ? YamlKeyKind.Complex
            : YamlKeyKind.Scalar;

        /// <summary>True when the current token value must be processed by the YAML scalar rules (folding, block
        /// indentation, YAML-only escapes) rather than by JSON unescaping.</summary>
        internal readonly bool YamlNeedsProcessing => _yamlNeedsProcessing;

        /// <summary>True when the current token is a scalar read by the YAML grammar whose text can be read as a string (D2).</summary>
        internal readonly bool IsYamlScalarText => _yamlStyle != YamlScalarStyle.None && _tokenType is YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False;

        /// <summary>True when the current token can be read as text: a string, or a YAML scalar of any resolved type (D2).</summary>
        internal readonly bool TokenIsStringLike => _tokenType == YamlTokenType.String || IsYamlScalarText;

        /// <summary>Index (in the current buffer) of the first byte of the current token, properties and quotes included.</summary>
        internal readonly long YamlRawTokenStart => _totalConsumed + _yamlRawStart;

        internal readonly long YamlRawTokenLine => _yamlRawLine;

        internal readonly long YamlRawTokenColumn => _yamlRawColumn;

        /// <summary>Indentation of the block context the current token belongs to (-1 at the top level).</summary>
        internal readonly int YamlTokenParentIndent => _yamlParentIndentPlusOne - 1;

        internal readonly byte YamlBlockChomping => _yamlChomping;

        internal readonly int YamlBlockScalarIndent => _yamlScalarIndent;

        /// <summary>The value of the current YAML scalar after folding, escapes and block processing.</summary>
        internal readonly byte[] GetYamlProcessedValue()
        {
            if (_yamlIsAlias)
            {
                // An alias key's ValueSpan is the anchor name, not text; resolving it is M5 work (decision D5).
                ThrowHelper.ThrowInvalidOperationException_YamlAliasKey();
            }
            if (_yamlIsComplexKey)
            {
                // A collection key has no text; the serializer and the DOM support it in M5 (decision D10).
                ThrowHelper.ThrowInvalidOperationException_YamlComplexKey();
            }

            Debug.Assert(_yamlNeedsProcessing);
            return YamlReaderHelper.ProcessYamlScalar(YamlValueBytes, _yamlStyle, _yamlScalarIndent, _yamlChomping);
        }

        /// <summary>True when the current token is an alias value or an alias key.</summary>
        internal readonly bool YamlIsAliasToken => _yamlIsAlias;

        /// <summary>True when the current token has an anchor or is an alias, so the serializer must handle it (M5b).</summary>
        internal readonly bool YamlHasNodeRef => _yamlAnchorLength != 0 || _tokenType == YamlTokenType.Alias;

        /// <summary>The serializer operation's anchor table and this reader's base position, or null before the first anchor.</summary>
        internal YamlAnchorContext? YamlAnchorContext
        {
            readonly get => _yaml.AnchorContext;
            set => _yaml.AnchorContext = value;
        }

        /// <summary>The absolute source position of the current token's first byte (its properties included).</summary>
        internal readonly long YamlAbsoluteTokenStart => (_yaml.AnchorContext?.Base ?? 0) + YamlRawTokenStart;

        /// <summary>Records the current token's anchor in the operation's table (M5b), creating the table at the first anchor.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void YamlRecordAnchor()
        {
            YamlAnchorContext? context = _yaml.AnchorContext;
            if (context is null)
            {
                // Base 0: an in-memory input starts at 0. A stream's driver rebases a table born in one of its buffers.
                context = new YamlAnchorContext(new YamlAnchorTable(), 0, isSource: true);
                _yaml.AnchorContext = context;
            }

            // A scalar's extent is known now, and a replay must not re-decide it: inside a flow collection, a reader
            // scoped to "1, *a]" in block context would read one plain scalar. An anchored scalar key (&k key: v)
            // replays the key's own bytes, without the ':'. Collections delimit themselves (0: measured on replay).
            int fixedLength = 0;
            if (_tokenType == YamlTokenType.PropertyName)
            {
                if (!_yamlIsAlias && !_yamlIsComplexKey)
                {
                    int quote = _yamlStyle is YamlScalarStyle.SingleQuoted or YamlScalarStyle.DoubleQuoted ? 1 : 0;
                    fixedLength = _yamlValueWindowStart + ValueSpan.Length + quote - _yamlRawStart;
                }
            }
            else if (_tokenType is YamlTokenType.String or YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null)
            {
                fixedLength = checked((int)(BytesConsumed - YamlRawTokenStart));
            }

            context.Table.Record(
                YamlReaderHelper.TranscodeHelper(Anchor),
                context.Base + YamlRawTokenStart,
                _yamlRawLine,
                _yamlRawColumn,
                YamlTokenParentIndent,
                _yaml.TagDirectives,
                fixedLength,
                limit: context.IsSource ? 0 : context.Base + _buffer.Length);
            YamlStoreSource();
        }

        /// <summary>
        /// M5e: a collection key is one token, so its anchors are recorded by reading it once with a key reader,
        /// whatever later reads the key (a typed key, a node copied at an object site, a skip).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void YamlRecordKeyAnchors()
        {
            if (YamlValueBytes.IndexOf((byte)'&') < 0)
            {
                return;
            }

            _yaml.AnchorContext ??= new YamlAnchorContext(new YamlAnchorTable(), 0, isSource: true);
            YamlStoreSource();
            Utf8YamlReader key = CreateKeyReader();
            while (key.Read())
            {
            }
        }

        /// <summary>A new document starts: anchors of earlier documents no longer resolve (YAML 1.2, 3.2.2.2).</summary>
        private readonly void YamlStartAnchorDocument(int pos)
        {
            if (_yaml.AnchorContext is { } context)
            {
                context.Table.StartDocument(context.Base + _totalConsumed + pos);
            }
        }

        /// <summary>
        /// When this reader reads the operation's own input, copies its buffer into the anchor table's store, so that a
        /// replay finds every byte up to the current position (M5b). A no-op for other readers and once stored.
        /// </summary>
        internal readonly void YamlStoreSource()
        {
            if (_yaml.AnchorContext is { IsSource: true } context)
            {
                if (_isMultiSegment)
                {
                    context.Table.Store(_sequence, context.Base);
                }
                else
                {
                    context.Table.Store(_buffer, context.Base);
                }
            }
        }

        private readonly bool YamlAllowComments => _readerOptions.CommentHandling == YamlCommentHandling.Allow;

        /// <summary>
        /// JSON comments ('//' to the end of the line, '/* */') are read: <see cref="YamlCommentHandling.Skip"/> or
        /// <see cref="YamlCommentHandling.Allow"/>. They are looked for in flow collections and at the document level
        /// only, never inside a block collection.
        /// </summary>
        private readonly bool YamlJsonComments => _readerOptions.CommentHandling != YamlCommentHandling.Disallow && !_readerOptions.YamlCommentsOnly;

        /// <summary>JSON comments around the root node: not for a reader over a value sliced out of a document.</summary>
        private readonly bool YamlJsonCommentsAtDocumentLevel => YamlJsonComments && !_yaml.Has(YamlParseFlags.ScopedValue);

        private readonly YamlTokenType YamlEffectiveTokenType => _tokenType == YamlTokenType.Comment ? _previousTokenType : _tokenType;

        private enum YamlStep : byte
        {
            Token,
            NeedMoreData,
            EndOfInput,
        }

        private struct YamlCursor
        {
            public int Pos;
            public long Line;
            public long Column;
        }

        private struct YamlScan
        {
            public bool AtEnd;
            public bool IsComment;
            public bool LineStart;
            public int Indent;
            public bool TabInIndent;
            public bool Moved;
        }

        private struct YamlProperties
        {
            public int AnchorStart;
            public int AnchorLength;
            public int TagStart;
            public int TagLength;
            public readonly bool Any => AnchorLength > 0 || TagLength > 0;
        }

        private bool ReadYaml()
        {
            if (_isMultiSegment)
            {
                // M3c: a multi-segment sequence is read through windows (Utf8YamlReader.Yaml.Segments.cs).
                return YamlReadMultiSegment();
            }

            // The span path keeps the body of M3d as it was, rather than calling YamlReadAttempt: in the dll swap of
            // M3c gate 3, calling it read workflow-m.yaml 2.5 to 4.4% slower, with the same inlining decisions.
            // A change to this body must be made to YamlReadAttempt too.
            int consumed = _consumed;
            long lineNumber = _lineNumber;
            long bytePositionInLine = _bytePositionInLine;
            YamlTokenType tokenType = _tokenType;
            YamlTokenType previousTokenType = _previousTokenType;
            BitStack bitStack = _bitStack;
            YamlParseState yaml = _yaml;
            bool inObject = _inObject;
            long tokenStartIndex = TokenStartIndex;

            YamlResetTokenFields();

            YamlStep step = YamlReadCore();
            if (step == YamlStep.Token)
            {
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

            if (step == YamlStep.NeedMoreData)
            {
                _consumed = consumed;
                _lineNumber = lineNumber;
                _bytePositionInLine = bytePositionInLine;
                _tokenType = tokenType;
                _previousTokenType = previousTokenType;
                _bitStack = bitStack;
                _yaml = yaml;
                _inObject = inObject;
                TokenStartIndex = tokenStartIndex;
                ValueSpan = default;
            }

            return false;
        }

        /// <summary>
        /// Reads one token from the current window of a multi-segment sequence: ReadYaml's span body, returning the
        /// step. On need-more-data every field is restored, so the token is read again from the same state over a
        /// longer window.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private YamlStep YamlReadAttempt()
        {
            int consumed = _consumed;
            long lineNumber = _lineNumber;
            long bytePositionInLine = _bytePositionInLine;
            YamlTokenType tokenType = _tokenType;
            YamlTokenType previousTokenType = _previousTokenType;
            BitStack bitStack = _bitStack;
            YamlParseState yaml = _yaml;
            bool inObject = _inObject;
            long tokenStartIndex = TokenStartIndex;

            YamlResetTokenFields();

            YamlStep step = YamlReadCore();
            if (step == YamlStep.NeedMoreData)
            {
                _consumed = consumed;
                _lineNumber = lineNumber;
                _bytePositionInLine = bytePositionInLine;
                _tokenType = tokenType;
                _previousTokenType = previousTokenType;
                _bitStack = bitStack;
                _yaml = yaml;
                _inObject = inObject;
                TokenStartIndex = tokenStartIndex;
                ValueSpan = default;
            }

            return step;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void YamlResetTokenFields()
        {
            ValueSpan = default;
            ValueIsEscaped = false;
            _yamlStyle = YamlScalarStyle.None;
            _yamlAnchorLength = 0;
            _yamlTagLength = 0;
            _yamlIsFlow = false;
            _yamlIsAlias = false;
            _yamlIsComplexKey = false;
            _yamlNumberNotJson = false;
            _yamlNeedsProcessing = false;
            _yamlParentIndentPlusOne = _yaml.BlockIndentPlusOne;
        }

        private YamlStep YamlReadCore()
        {
            YamlCursor c = new() { Pos = _consumed, Line = _lineNumber, Column = _bytePositionInLine };

            if (_tokenType == YamlTokenType.None && c.Pos == 0 && c.Line == 0 && c.Column == 0)
            {
                // A UTF-8 byte-order mark is allowed at the start of the stream.
                ReadOnlySpan<byte> bom = YamlConstants.Utf8Bom;
                int available = Math.Min(_buffer.Length, bom.Length);
                if (available > 0 && _buffer.Slice(0, available).SequenceEqual(bom.Slice(0, available)))
                {
                    if (available < bom.Length)
                    {
                        if (!IsLastSpan)
                        {
                            return YamlStep.NeedMoreData;
                        }
                    }
                    else
                    {
                        c.Pos = bom.Length;
                    }
                }
            }

            int depth = _bitStack.CurrentDepth;
            if (depth == 0)
            {
                return YamlReadDocument(ref c);
            }

            int level = _yaml.GetLevel(depth);
            return level < 0
                ? YamlReadFlow(ref c, isPair: level == -2)
                : YamlReadBlock(ref c, level);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Committing tokens

        private void YamlCommit(in YamlCursor c, bool contentAtLineStart = false)
        {
            _consumed = c.Pos;
            _lineNumber = c.Line;
            _bytePositionInLine = c.Column;
            _yaml.Set(YamlParseFlags.ContentAtLineStart, contentAtLineStart);
        }

        private void YamlSetTokenType(YamlTokenType type)
        {
            if (type == YamlTokenType.Comment)
            {
                if (_tokenType != YamlTokenType.Comment)
                {
                    _previousTokenType = _tokenType;
                }
            }
            _tokenType = type;
        }

        private void YamlSetRawStart(int pos, long line, long column)
        {
            _yamlRawStart = pos;
            _yamlRawLine = line;
            _yamlRawColumn = column;
        }

        private void YamlApplyProperties(in YamlProperties props)
        {
            _yamlAnchorStart = props.AnchorStart;
            _yamlAnchorLength = props.AnchorLength;
            _yamlTagStart = props.TagStart;
            _yamlTagLength = props.TagLength;
        }

        /// <summary>Emits a token that consumes nothing beyond the whitespace in front of it.</summary>
        private YamlStep YamlEmitZeroWidth(in YamlCursor c, bool lineStart, YamlTokenType type)
        {
            TokenStartIndex = c.Pos;
            ValueSpan = default;
            YamlSetRawStart(c.Pos, c.Line, c.Column);
            YamlCommit(c, lineStart);
            YamlSetTokenType(type);
            return YamlStep.Token;
        }

        private YamlStep YamlEmitNull(in YamlCursor c, bool lineStart, in YamlProperties props, int rawStart, long rawLine, long rawColumn)
        {
            YamlApplyProperties(props);
            YamlSetRawStart(rawStart, rawLine, rawColumn);
            _yamlStyle = YamlScalarStyle.Plain;
            YamlTokenType type = YamlTokenType.Null;
            if (props.TagLength == 0 && _readerOptions.Schema >= YamlSchemaKind.Json)
            {
                // The empty node is a plain scalar: a string under Failsafe, and unresolved under Json: an error,
                // or a string under AcceptLenientScalars.
                if (_readerOptions.Schema == YamlSchemaKind.Json && !AcceptsLenientScalars)
                {
                    YamlThrow(YamlGrammarError.UnresolvedJsonSchemaScalar, c, string.Empty);
                }

                type = YamlTokenType.String;
            }
            else if (props.TagLength > 0)
            {
                // '!!str' (or '!') on an empty node is the empty string.
                YamlCoreTag tag = YamlClassifyTag(props);
                if (tag is YamlCoreTag.Str or YamlCoreTag.NonSpecific)
                {
                    type = YamlTokenType.String;
                }
                else if (tag is YamlCoreTag.Map or YamlCoreTag.Seq or YamlCoreTag.Int or YamlCoreTag.Float or YamlCoreTag.Bool)
                {
                    YamlThrowTagMismatch(props, c);
                }
            }

            TokenStartIndex = c.Pos;
            ValueSpan = default;
            YamlCommit(c, lineStart);
            YamlSetTokenType(type);
            YamlAfterScalarAtRoot();
            return YamlStep.Token;
        }

        private void YamlAfterScalarAtRoot()
        {
            if (_bitStack.CurrentDepth == 0)
            {
                _yaml.Set(YamlParseFlags.RootDone, true);
                _yaml.Set(YamlParseFlags.DocumentOpen, false);
            }
        }

        private void YamlPushCollection(bool isObject, int code)
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
            {
                ThrowHelper.ThrowYamlReaderException(ref this, isObject ? ExceptionResource.ObjectDepthTooLarge : ExceptionResource.ArrayDepthTooLarge);
            }

            if (isObject)
            {
                _bitStack.PushTrue();
            }
            else
            {
                _bitStack.PushFalse();
            }

            _yaml.Push(_bitStack.CurrentDepth, code);
            _inObject = isObject;
            _yaml.Set(YamlParseFlags.DocumentOpen, false);
        }

        private YamlStep YamlEmitEnd(ref YamlCursor c, bool lineStart, bool consumesBracket)
        {
            bool isObject = _inObject;
            TokenStartIndex = c.Pos;
            YamlSetRawStart(c.Pos, c.Line, c.Column);
            if (consumesBracket)
            {
                ValueSpan = _buffer.Slice(c.Pos, 1);
                _yamlValueWindowStart = c.Pos;
                c.Pos++;
                c.Column++;
                YamlCommit(c);
                _yamlIsFlow = true;
            }
            else
            {
                ValueSpan = default;
                YamlCommit(c, lineStart);
            }

            int depth = _bitStack.CurrentDepth;
            _yaml.Pop(depth);
            _inObject = _bitStack.Pop();
            if (_bitStack.CurrentDepth == 0)
            {
                _yaml.Set(YamlParseFlags.RootDone, true);
            }

            _yaml.Set(YamlParseFlags.FlowKeyWithoutValue, false);
            YamlSetTokenType(isObject ? YamlTokenType.EndObject : YamlTokenType.EndArray);
            return YamlStep.Token;
        }

        private YamlStep YamlEmitComment(ref YamlCursor c, bool lineStart = false)
        {
            if (_buffer[c.Pos] == (byte)'/')
            {
                return YamlEmitJsonComment(ref c, lineStart);
            }

            Debug.Assert(_buffer[c.Pos] == (byte)'#');
            ReadOnlySpan<byte> rest = _buffer.Slice(c.Pos + 1);
            int end = YamlReaderHelper.IndexOfLineBreak(rest);
            if (end < 0)
            {
                if (!IsLastSpan)
                {
                    return YamlStep.NeedMoreData;
                }
                end = rest.Length;
            }

            TokenStartIndex = c.Pos;
            YamlSetRawStart(c.Pos, c.Line, c.Column);
            ValueSpan = rest.Slice(0, end);
            _yamlValueWindowStart = c.Pos + 1;
            c.Pos += end + 1;
            c.Column += end + 1;
            YamlCommit(c);
            YamlSetTokenType(YamlTokenType.Comment);
            return YamlStep.Token;
        }

        /// <summary>
        /// A JSON comment as a token, as System.Text.Json returns it: the value is the text after '//' up to the line
        /// break, or the text between '/*' and '*/'; the token starts at the first '/' and, for '//', consumes the
        /// line break.
        /// </summary>
        private YamlStep YamlEmitJsonComment(ref YamlCursor c, bool lineStart)
        {
            YamlCursor start = c;
            bool block = _buffer[c.Pos + 1] == (byte)'*';
            if (!YamlSkipJsonComment(ref c, block))
            {
                return YamlStep.NeedMoreData;
            }

            TokenStartIndex = start.Pos;
            YamlSetRawStart(start.Pos, start.Line, start.Column);
            int valueStart = start.Pos + 2;
            int valueLength = c.Pos - valueStart - (block ? 2 : 0);
            if (!block && c.Pos < _buffer.Length && !YamlAdvanceBreak(ref c))
            {
                // The token of a '//' comment takes its line break, as System.Text.Json's does: a CR at the end of
                // the data so far may be the first half of a CRLF.
                return YamlStep.NeedMoreData;
            }

            ValueSpan = _buffer.Slice(valueStart, valueLength);
            _yamlValueWindowStart = valueStart;

            // What follows a '/* */' on its line is where the comment started, for the indentation of a block node.
            YamlCommit(c, contentAtLineStart: block && lineStart && c.Line == start.Line);
            YamlSetTokenType(YamlTokenType.Comment);
            return YamlStep.Token;
        }

        /// <summary>
        /// Whether a JSON comment starts at the '/' at <paramref name="pos"/>: 1 for '//', 2 for '/*', 0 for neither
        /// (the '/' is content), -2 when the next byte is not in the buffer yet.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly int YamlJsonCommentAt(int pos)
        {
            Debug.Assert(_buffer[pos] == (byte)'/');
            int next = YamlPeek(pos + 1);
            return next switch
            {
                -2 => -2,
                (byte)'/' => 1,
                (byte)'*' => 2,
                _ => 0,
            };
        }

        /// <summary>
        /// Advances over the JSON comment at <paramref name="c"/>: a '//' comment up to its line break, which is left
        /// to the caller, or a '/* */' comment through its '*/'. Returns false when more data is needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly bool YamlSkipJsonComment(ref YamlCursor c, bool block)
        {
            ReadOnlySpan<byte> rest = _buffer.Slice(c.Pos + 2);
            if (!block)
            {
                int end = YamlReaderHelper.IndexOfLineBreak(rest);

                // U+2028 and U+2029 end a line in JavaScript: System.Text.Json refuses them in a '//' comment, and
                // does so as soon as it sees one, before the line break is known.
                ReadOnlySpan<byte> text = end < 0 ? rest : rest.Slice(0, end);
                int separator = text.IndexOf((byte)0xE2);
                while (separator >= 0)
                {
                    if (separator + 2 < text.Length && text[separator + 1] == 0x80 && text[separator + 2] is 0xA8 or 0xA9)
                    {
                        c.Pos += 2 + separator;
                        c.Column += 2 + separator;
                        YamlThrow(YamlGrammarError.LineSeparatorInComment, c);
                    }
                    int more = text.Slice(separator + 1).IndexOf((byte)0xE2);
                    separator = more < 0 ? -1 : separator + 1 + more;
                }

                if (end < 0)
                {
                    if (!IsLastSpan)
                    {
                        return false;
                    }
                    end = rest.Length;
                }

                c.Pos += 2 + end;
                c.Column += 2 + end;
                return true;
            }

            int close = rest.IndexOf("*/"u8);
            if (close < 0)
            {
                if (!IsLastSpan)
                {
                    return false;
                }
                YamlThrow(YamlGrammarError.UnterminatedComment, c);
            }

            // Line breaks inside the comment count as anywhere else: LF, CR, or CRLF as one.
            ReadOnlySpan<byte> body = rest.Slice(0, close);
            int lastBreakEnd = -1;
            int offset = 0;
            while (true)
            {
                int k = YamlReaderHelper.IndexOfLineBreak(body.Slice(offset));
                if (k < 0)
                {
                    break;
                }
                k += offset;
                offset = k + 1;
                if (body[k] == (byte)'\r' && offset < body.Length && body[offset] == (byte)'\n')
                {
                    offset++;
                }
                c.Line++;
                lastBreakEnd = offset;
            }

            c.Pos += 2 + close + 2;
            c.Column = lastBreakEnd < 0 ? c.Column + 2 + close + 2 : close - lastBreakEnd + 2;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Whitespace, line breaks, comments

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool YamlIsBreak(byte b) => b is (byte)'\n' or (byte)'\r';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool YamlIsBlank(byte b) => b is (byte)' ' or (byte)'\t';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool YamlIsWhiteSpaceOrBreak(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool YamlIsFlowIndicator(byte b) => b is (byte)',' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}';

        /// <summary>Advances over one line break (LF, CR or CRLF). Returns false when a CR ends a non-final buffer.</summary>
        private readonly bool YamlAdvanceBreak(ref YamlCursor c)
        {
            byte b = _buffer[c.Pos];
            Debug.Assert(YamlIsBreak(b));
            if (b == (byte)'\r')
            {
                if (c.Pos + 1 < _buffer.Length)
                {
                    c.Pos += _buffer[c.Pos + 1] == (byte)'\n' ? 2 : 1;
                }
                else
                {
                    if (!IsLastSpan)
                    {
                        return false;
                    }
                    c.Pos++;
                }
            }
            else
            {
                c.Pos++;
            }

            c.Line++;
            c.Column = 0;
            return true;
        }

        /// <summary>
        /// Returns the byte after <paramref name="pos"/>, or -1 at the end of the final buffer, or -2 when more data is needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int YamlPeek(int pos)
        {
            if (pos < _buffer.Length)
            {
                return _buffer[pos];
            }
            return IsLastSpan ? -1 : -2;
        }

        /// <summary>True for "followed by whitespace, a line break or the end of input" (−2 means more data is needed).</summary>
        private readonly int YamlFollowedBySpace(int pos)
        {
            int next = YamlPeek(pos);
            if (next == -2)
            {
                return -2;
            }
            return next == -1 || YamlIsWhiteSpaceOrBreak((byte)next) ? 1 : 0;
        }

        /// <summary>
        /// Skips spaces, tabs, line breaks and comments up to the next content byte.
        /// </summary>
        /// <returns>False when the buffer ends and more data could follow.</returns>
        private readonly bool YamlScanToContent(ref YamlCursor c, out YamlScan s, bool stopAtComment, bool jsonComments = false)
        {
            s = default;
            ReadOnlySpan<byte> buffer = _buffer;

            bool lineStart = c.Column == 0 || (c.Pos == _consumed && _yaml.Has(YamlParseFlags.ContentAtLineStart));
            bool inIndent = lineStart;
            int indent = lineStart ? (int)c.Column : 0;
            bool tab = false;
            bool moved = false;

            while (true)
            {
                if (c.Pos >= buffer.Length)
                {
                    if (!IsLastSpan)
                    {
                        return false;
                    }

                    s.AtEnd = true;
                    s.LineStart = lineStart;
                    s.Indent = indent;
                    return true;
                }

                byte b = buffer[c.Pos];
                if (b == (byte)' ')
                {
                    c.Pos++;
                    c.Column++;
                    if (inIndent)
                    {
                        indent++;
                    }
                    moved = true;
                    continue;
                }

                if (b == (byte)'\t')
                {
                    c.Pos++;
                    c.Column++;
                    if (inIndent)
                    {
                        tab = true;
                    }
                    inIndent = false;
                    moved = true;
                    continue;
                }

                if (YamlIsBreak(b))
                {
                    if (!YamlAdvanceBreak(ref c))
                    {
                        return false;
                    }
                    lineStart = true;
                    inIndent = true;
                    indent = 0;
                    tab = false;
                    moved = true;
                    continue;
                }

                if (b == (byte)'#' && (moved || lineStart || (c.Pos > 0 && YamlIsBlank(buffer[c.Pos - 1]))))
                {
                    if (stopAtComment)
                    {
                        s.IsComment = true;
                        s.LineStart = lineStart;
                        s.Indent = indent;
                        return true;
                    }

                    int end = YamlReaderHelper.IndexOfLineBreak(buffer.Slice(c.Pos));
                    if (end < 0)
                    {
                        if (!IsLastSpan)
                        {
                            return false;
                        }
                        end = buffer.Length - c.Pos;
                    }
                    c.Pos += end;
                    c.Column += end;
                    continue;
                }

                if (b == (byte)'/' && jsonComments)
                {
                    // A JSON comment may start wherever a token may: no white space is needed before it.
                    int kind = YamlJsonCommentAt(c.Pos);
                    if (kind == -2)
                    {
                        return false;
                    }

                    if (kind != 0)
                    {
                        if (stopAtComment)
                        {
                            s.IsComment = true;
                            s.LineStart = lineStart;
                            s.Indent = indent;
                            return true;
                        }

                        long line = c.Line;
                        if (!YamlSkipJsonComment(ref c, block: kind == 2))
                        {
                            return false;
                        }

                        if (kind == 2)
                        {
                            // A '/* */' counts as white space: what follows a comment that starts its line is
                            // indented at its own column. After a comment that crossed lines, the content is not at
                            // the start of a line.
                            if (c.Line != line)
                            {
                                lineStart = false;
                                tab = false;
                            }

                            inIndent = lineStart;
                            indent = lineStart ? (int)c.Column : 0;
                        }
                        moved = true;
                        continue;
                    }
                }

                s.LineStart = lineStart;
                s.Indent = indent;
                s.TabInIndent = tab;
                s.Moved = moved;
                return true;
            }
        }

        /// <summary>Skips spaces and tabs on the current line.</summary>
        private readonly void YamlSkipBlanks(ref YamlCursor c)
        {
            ReadOnlySpan<byte> buffer = _buffer;
            while (c.Pos < buffer.Length && YamlIsBlank(buffer[c.Pos]))
            {
                c.Pos++;
                c.Column++;
            }
        }

        /// <summary>
        /// After a node or marker: only blanks and a comment may follow on the line. Returns false when more data is needed.
        /// </summary>
        private readonly bool YamlExpectLineEnd(ref YamlCursor c)
        {
            bool sawBlank = false;
            ReadOnlySpan<byte> buffer = _buffer;
            while (true)
            {
                if (c.Pos >= buffer.Length)
                {
                    return IsLastSpan;
                }

                byte b = buffer[c.Pos];
                if (YamlIsBlank(b))
                {
                    sawBlank = true;
                    c.Pos++;
                    c.Column++;
                    continue;
                }

                if (YamlIsBreak(b))
                {
                    return true;
                }

                if (b == (byte)'#' && (sawBlank || (c.Pos > 0 && YamlIsBlank(buffer[c.Pos - 1]))))
                {
                    int end = YamlReaderHelper.IndexOfLineBreak(buffer.Slice(c.Pos));
                    if (end < 0)
                    {
                        if (!IsLastSpan)
                        {
                            return false;
                        }
                        end = buffer.Length - c.Pos;
                    }
                    c.Pos += end;
                    c.Column += end;
                    return true;
                }

                YamlThrow(YamlGrammarError.UnexpectedContentAfterNode, c);
            }
        }

        /// <summary>Returns 1 for '---', 2 for '...', 0 for neither, -1 when more data is needed.</summary>
        private readonly int YamlDocumentMarker(in YamlCursor c, bool lineStart)
        {
            if (!lineStart || c.Column != 0)
            {
                return 0;
            }

            ReadOnlySpan<byte> buffer = _buffer;
            int pos = c.Pos;
            byte first = buffer[pos];
            if (first != (byte)'-' && first != (byte)'.')
            {
                return 0;
            }

            for (int i = 1; i < 3; i++)
            {
                if (pos + i >= buffer.Length)
                {
                    return IsLastSpan ? 0 : -1;
                }
                if (buffer[pos + i] != first)
                {
                    return 0;
                }
            }

            int after = YamlFollowedBySpace(pos + 3);
            if (after == -2)
            {
                return -1;
            }
            return after == 1 ? (first == (byte)'-' ? 1 : 2) : 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Errors

        [DoesNotReturn]
        private readonly void YamlThrow(YamlGrammarError error, in YamlCursor at, string? argument = null)
        {
            ThrowHelper.ThrowYamlGrammarException(error, at.Line, at.Column, argument);
        }

        [DoesNotReturn]
        private readonly void YamlThrowTagMismatch(in YamlProperties props, in YamlCursor at)
        {
            string tag = YamlReaderHelper.TranscodeHelper(_buffer.Slice(props.TagStart, props.TagLength));
            ThrowHelper.ThrowYamlGrammarException(YamlGrammarError.TagMismatch, at.Line, at.Column, tag);
        }
    }
}
