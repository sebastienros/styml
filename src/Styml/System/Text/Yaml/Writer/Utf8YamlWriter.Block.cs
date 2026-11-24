// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    // Block style.
    //
    // Dispatch. Each *ByOptions / *Slow method that chose between the indented and the minimized body now tests the
    // style byte, minimized first, then indented, then block (the order M4a measured),
    // and the block branch calls one of the *Block bodies here. They are not inlined, so the JSON-compatible callers'
    // inlining budgets do not pay for them. Everything before a dispatch point is shared and unchanged, the escaping
    // scan included: a block body receives property names and string values JSON-escaped by the writer's encoder, as
    // the JSON-compatible bodies do, or pre-encoded (YamlEncodedText), which is the same form. It unescapes them when
    // they hold an escape and then applies the scalar rules to the text.
    //
    // Structure is written as it goes, with nothing held back: a collection start writes what does not depend
    // on whether the collection is empty, and the first child or the end decides the rest, from _tokenType (StartObject
    // or StartArray exactly while the collection has no child) and _bitStack (the parent's kind). In block style the
    // writer maintains _bitStack and _enclosingContainer whether or not validation is skipped.
    public sealed partial class Utf8YamlWriter
    {
        private const byte WriterStyleJsonMinimized = 0;
        private const byte WriterStyleJsonIndented = 1;
        private const byte WriterStyleBlock = 2;

        // Computed once in SetOptions: JSON-compatible minimized or indented, or block.
        private byte _style;

        // Computed once in SetOptions: Indented || Block || !SkipValidation, the test of WriteStart and WriteEnd. It was
        // a mask test on the options until M4c, when a style that is not set can mean block.
        private bool _startEndSlow;

        // The column of the current collection's entries (keys or dashes), a running sum of the deltas of 4.2.
        private int _blockColumn;

        // QuoteForLenientReaders, in block style: the quoting for readers that resolve beyond the standards.
        private bool _lenient;

        // The style hint of WriteScalarStyle, for the next string value or property name; None when unset.
        private YamlScalarStyle _scalarStyleHint;

        // With a Plain hint: the next string value was a plain scalar in a YamlDocument (M5a), so it was read as a string
        // under the document's schema. It stays plain where the syntactic check allows it; the resolution check
        // is skipped, so a plain 2001-01-01 written back is not quoted.
        private bool _plainFromSource;

        // M5f: the key being written is a converter's number, boolean or non-finite text, written plain.
        private bool _plainNumberKey;

        // M5f: the pending hint is the serializer's, for a member's string value: dropped, not an error, when the
        // next node is not a string.
        private bool _softScalarStyleHint;

        // M7b (fix A): the next key is a dictionary's or a YamlObject's string key, which DefaultScalarStyle styles, as
        // YamlDotNet styles them. Member names never set it (YamlDotNet leaves them alone), and a number or boolean key
        // or a collection key drops it.
        private bool _defaultStyleKey;

        /// <summary>
        /// M7b (fix A): the next key, if it is written as text, is a dictionary's or a <see cref="Nodes.YamlObject"/>'s
        /// string key: <see cref="YamlWriterOptions.DefaultScalarStyle"/> applies to it where no hint does. Block style
        /// only; nothing when the option is not set.
        /// </summary>
        internal void HintDefaultScalarStyleForNextKey()
        {
            if ((_blockSpecial & SpecialDefaultStyle) != 0)
            {
                _defaultStyleKey = true;
            }
        }

        /// <summary>
        /// M5f: a member's <see cref="Serialization.YamlScalarStyleAttribute"/> style, for the next node if it is a string
        /// value. Unlike <see cref="WriteScalarStyle"/>, the next node may be anything else, which drops it. Block style
        /// only.
        /// </summary>
        internal void HintScalarStyleForNextString(YamlScalarStyle style)
        {
            if (_style == WriterStyleBlock && _scalarStyleHint == YamlScalarStyle.None)
            {
                _scalarStyleHint = style;
                _softScalarStyleHint = true;
            }
        }

        /// <summary>
        /// M5f: a <see cref="YamlDocument"/> scalar's source style, for the string value or key the DOM writes
        /// next: applied where the writer can hold the text in it (the rules of <see cref="WriteScalarStyle"/>), and only
        /// when no hint is pending, so that the merge rewrite's own quoting goes first. Block style only.
        /// </summary>
        internal void HintSourceScalarStyle(YamlScalarStyle style)
        {
            if (_style == WriterStyleBlock && _scalarStyleHint == YamlScalarStyle.None && !_options.IgnoreSourceStyles)
            {
                _scalarStyleHint = style;
            }
        }

        /// <summary>
        /// The always-quoted typed forms (a <c>TimeSpan</c>, a <c>TimeOnly</c>, a <c>Version</c>): their converter's
        /// hint, double quotes, or single quotes under a single-quoted <see cref="YamlWriterOptions.DefaultScalarStyle"/>
        /// (M7b, fix A). Either keeps the text a string for every reader.
        /// </summary>
        internal void WriteTypedTextQuoteStyle() =>
            WriteScalarStyle(_options.DefaultScalarStyle == YamlScalarStyle.SingleQuoted ? YamlScalarStyle.SingleQuoted : YamlScalarStyle.DoubleQuoted);

        /// <summary>M5f: drops a soft hint that the member's value did not consume.</summary>
        internal void ClearSoftScalarStyleHint()
        {
            if (_softScalarStyleHint)
            {
                _softScalarStyleHint = false;
                _scalarStyleHint = YamlScalarStyle.None;
                _plainFromSource = false;
            }
        }

        // The encoder the user set, if any: in block style an unset encoder is replaced by the YAML set's (SetOptions).
        private System.Text.Encodings.Web.JavaScriptEncoder? UserEncoder =>
            ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance) ? null : _options.Encoder;

        /// <summary>The writer writes block style.</summary>
        internal bool IsBlockStyle => _style == WriterStyleBlock;

        /// <summary>M7c: the DOM's source styles are not asked for (<see cref="YamlWriterOptions.IgnoreSourceStyles"/>).</summary>
        internal bool KeepsSourceStyles => _style == WriterStyleBlock && !_options.IgnoreSourceStyles;

        /// <summary>
        /// Requests a scalar style for the next string value or property name. In block style the request is honoured
        /// where the string allows it; in JSON-compatible style it is ignored.
        /// </summary>
        /// <param name="style">
        /// <see cref="YamlScalarStyle.Plain"/>, <see cref="YamlScalarStyle.SingleQuoted"/>,
        /// <see cref="YamlScalarStyle.DoubleQuoted"/>, <see cref="YamlScalarStyle.Literal"/> or
        /// <see cref="YamlScalarStyle.Folded"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// The hint applies to the next node. A property name is a key node, so a hint before
        /// <see cref="WritePropertyName(string)"/> styles the key. In the forms that write a property name and a value in
        /// one call, the hint applies to the value.
        /// </para>
        /// <para>
        /// The hint is a request: <see cref="YamlScalarStyle.Plain"/> on a string that cannot be plain is written
        /// double-quoted, and <see cref="YamlScalarStyle.Literal"/> or <see cref="YamlScalarStyle.Folded"/> on a string
        /// without a line break, or with a character a block scalar cannot hold, is written as it would be without the
        /// hint. <see cref="YamlScalarStyle.SingleQuoted"/> on a string with a line break is written over several lines
        /// (a line break as an empty line, M7b), except inside a flow collection or when the string has a tab, a carriage
        /// return or a space next to a line break, where it is double-quoted.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="style"/> is not a scalar style.</exception>
        /// <exception cref="InvalidOperationException">
        /// A hint is already pending, while validation is enabled. A hint followed by a node that is not a string value
        /// or a property name also throws, at that node.
        /// </exception>
        public void WriteScalarStyle(YamlScalarStyle style)
        {
            if (style is <= YamlScalarStyle.None or > YamlScalarStyle.Folded)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(style));
            }

            if (_style != WriterStyleBlock)
            {
                return;
            }

            if (_softScalarStyleHint)
            {
                // A converter's own hint replaces the member's.
                _softScalarStyleHint = false;
                _scalarStyleHint = YamlScalarStyle.None;
            }

            if (_scalarStyleHint != YamlScalarStyle.None && !_options.SkipValidation)
            {
                ThrowHelper.ThrowInvalidOperationException_ScalarStyleHintPending();
            }

            _scalarStyleHint = style;
        }

        /// <summary>
        /// Writes <c>---</c>, the start of a new document, and allows a new root value after a completed one.
        /// </summary>
        /// <remarks>
        /// The marker is written in both styles. Without it, a second root value is an error, as before.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The writer is inside a collection or a string, or after a property name, while validation is enabled.
        /// </exception>
        public void WriteDocumentStart() => WriteDocumentStart(rootPropertiesOnMarkerLine: false);

        /// <summary>
        /// Writes <c>---</c>, the start of a new document; with <paramref name="rootPropertiesOnMarkerLine"/>, the root
        /// node's properties (<see cref="WriteTag(string)"/>, <see cref="WriteAnchor(string)"/>) go on that line
        /// (<c>--- !u!1 &amp;1000</c>, as Unity writes its objects) and the root starts on the next line.
        /// </summary>
        /// <param name="rootPropertiesOnMarkerLine">Whether the root's properties go on the <c>---</c> line.</param>
        /// <exception cref="InvalidOperationException">
        /// The writer is inside a collection or a string, or after a property name, while validation is enabled.
        /// </exception>
        public void WriteDocumentStart(bool rootPropertiesOnMarkerLine)
        {
            if (!_options.SkipValidation && (CurrentDepth != 0 || IsWritingPartialString || _tokenType == YamlTokenType.PropertyName))
            {
                ThrowHelper.ThrowInvalidOperationException_DocumentStartNotAtRoot();
            }

            if (_scalarStyleHint != YamlScalarStyle.None)
            {
                OnScalarStyleHintWithoutScalar();
            }

            if (_blockSpecial != 0)
            {
                CheckNoCollectionStyleHint();
                if (PropertiesPending && !_options.SkipValidation)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodePropertiesWithoutNode();
                }
            }

            ScopeTagDirectivesToNewDocument();

            // In block style a completed root value has written its line break; in JSON-compatible style, the
            // marker goes on a line of its own.
            bool lineBreakFirst = _style != WriterStyleBlock && _tokenType != YamlTokenType.None;
            int required = 3 + 2 * _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (lineBreakFirst)
            {
                WriteNewLine(output);
            }

            output[BytesPending++] = (byte)'-';
            output[BytesPending++] = (byte)'-';
            output[BytesPending++] = (byte)'-';
            WriteNewLine(output);

            ResetRootForNewDocument();
            _blockSpecial &= ~(SpecialDirectives | SpecialAfterDocumentEnd);
            if (_block is not null)
            {
                _block.AfterDocumentStartMarker = true;
                _block.YamlDirectivePending = false;
                _block.PropertiesOnMarkerLine = rootPropertiesOnMarkerLine;
                _block.OnMarkerLine = false;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Structure

        // The column delta of 4.2 for a collection of this kind whose parent is `parent` (None at the root).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BlockDelta(bool isArray, EnclosingContainerType parent)
        {
            if (parent == EnclosingContainerType.None)
            {
                return 0;
            }

            if (parent == EnclosingContainerType.Object && isArray && !_options.IndentSequences)
            {
                return 0;
            }

            return _indentLength;
        }

        // Writes what comes before an entry (a key, or a sequence item's dash) of the current collection: a line
        // break and the indentation, or, for the first entry of a collection that is a sequence item, the spaces
        // after the parent's dash (the compact form), or nothing for the first entry of a root collection.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBlockEntryStart(Span<byte> output)
        {
            if (_tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                int depth = _bitStack.CurrentDepth;
                if (depth <= 1)
                {
                    return;
                }

                if (!_bitStack.PeekParent() && !_options.ExpandSequenceItems)
                {
                    // Compact: "- a: 1", "- - 1". The dash is followed by IndentSize - 1 spaces. With
                    // ExpandSequenceItems, the first child goes on the next line instead.
                    int spaces = _indentLength - 1;
                    output.Slice(BytesPending, spaces).Fill(YamlConstants.Space);
                    BytesPending += spaces;
                    return;
                }
            }

            WriteBlockLineStart(output, _blockColumn);
        }

        // A line break and up to BlockLineStartColumns columns of block indentation, as one copy (M4p's candidate
        // c): "\r\n" and the spaces, read from offset 1 for "\n". Wider columns are
        // written as before. Block style only: the JSON-compatible style keeps WriteNewLine and WriteIndentation.
        private const int BlockLineStartColumns = 128;

        private static ReadOnlySpan<byte> BlockLineStart => "\r\n                                                                                                                                "u8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBlockLineStart(Span<byte> output, int column)
        {
            if ((uint)column <= BlockLineStartColumns)
            {
                int length = _newLineLength + column;
                BlockLineStart.Slice(2 - _newLineLength, length).CopyTo(output.Slice(BytesPending));
                BytesPending += length;
            }
            else
            {
                WriteBlockLineStartWide(output, column);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBlockLineStartWide(Span<byte> output, int column)
        {
            WriteNewLine(output);
            output.Slice(BytesPending, column).Fill(YamlConstants.Space);
            BytesPending += column;
        }

        // The bytes WriteBlockEntryStart and a dash with its space can write.
        private int BlockPrefixLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _blockColumn + _blockPrefixFixed;
        }

        // Writes what comes before a value node: the dash of a sequence item, or the space after "key:" for a scalar.
        // `extra` is the room the caller needs after it; the line break that completes a root scalar is included.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> BeginBlockValue(bool scalar, int extra)
        {
            if (_blockSpecial != 0)
            {
                return BeginBlockValueSpecial(scalar ? BlockNode.Scalar : BlockNode.BlockCollection, extra);
            }

            int required = BlockPrefixLength + extra + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (_enclosingContainer == EnclosingContainerType.Array)
            {
                WriteBlockEntryStart(output);
                output[BytesPending++] = (byte)'-';
                if (scalar)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
            }
            else if (_enclosingContainer == EnclosingContainerType.Object)
            {
                if (scalar)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
            }

            return output;
        }

        // A root scalar is complete as soon as it is written: one line break follows it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EndBlockScalar(YamlTokenType tokenType)
        {
            if (_bitStack.CurrentDepth == 0)
            {
                WriteNewLine(_memory.Span);
            }

            _tokenType = tokenType;
        }

        // NaN and the infinities have no JSON form; in block style they are YAML's .nan, .inf and -.inf. The
        // style is tested only when the value is not finite.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateFiniteUnlessBlock(double value)
        {
            if (!double.IsFinite(value) && _style != WriterStyleBlock)
            {
                ThrowHelper.ThrowArgumentException_ValueNotSupported();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateFiniteUnlessBlock(float value)
        {
            if (!float.IsFinite(value) && _style != WriterStyleBlock)
            {
                ThrowHelper.ThrowArgumentException_ValueNotSupported();
            }
        }

        // The hint check of every value node that is not a string.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckNoScalarStyleHint()
        {
            if (_scalarStyleHint != YamlScalarStyle.None)
            {
                OnScalarStyleHintWithoutScalar();
            }
        }

        // M7b (fix A): a typed value the writer writes as a YAML string from its own text (a date, a time, a GUID,
        // Base64) takes a quoted DefaultScalarStyle: the quote byte, or 0. Plain, literal and folded leave it in its typed
        // form (a date given to the string path would be quoted by the resolution check, and a block scalar needs a line
        // break), and so does a pending hint, a member's or a converter's, which goes first. The hint is checked here.
        private byte TypedTextQuote()
        {
            byte quote = 0;
            if ((_blockSpecial & SpecialDefaultStyle) != 0 && _scalarStyleHint == YamlScalarStyle.None)
            {
                quote = _options.DefaultScalarStyle switch
                {
                    YamlScalarStyle.DoubleQuoted => YamlConstants.Quote,
                    YamlScalarStyle.SingleQuoted => (byte)'\'',
                    _ => 0,
                };
            }

            if (quote == 0 && (_blockSpecial & SpecialJsonSchema) != 0)
            {
                // M7c: a date, a time or a GUID read back as a string under the JSON schema only when quoted.
                quote = YamlConstants.Quote;
            }

            CheckNoScalarStyleHint();
            return quote;
        }

        /// <summary>
        /// M5f: a dictionary key that a number or boolean converter formatted (<c>1</c>, <c>-0</c>,
        /// <c>1E+20</c>, <c>true</c>, <c>.nan</c>), written plain in block style, as both libraries write it: the text is
        /// the converter's, so it reads back as the same typed key under every schema, and the resolution check that
        /// quotes a string key such as <c>"1"</c> does not apply. In JSON-compatible style it is a quoted name, as before.
        /// </summary>
        internal void WritePlainPropertyName(ReadOnlySpan<byte> formatted)
        {
            if (_style != WriterStyleBlock)
            {
                WritePropertyNameUnescaped(formatted);
                return;
            }

            YamlWriterHelper.ValidateProperty(formatted);
            ValidateWritingProperty();
            CheckNoCollectionStyleHint();
            _plainNumberKey = true;
            try
            {
                WriteBlockKey(formatted, consumeHint: true, YamlWriterHelper.BlockScan.PlainKey);
            }
            finally
            {
                _plainNumberKey = false;
            }

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _tokenType = YamlTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        /// <summary>
        /// The next string value or key was a plain scalar of a <see cref="YamlDocument"/> that the output's reading schema
        /// reads as the same string: plain where the syntactic check allows it (see <see cref="_plainFromSource"/>). Ignored
        /// in JSON-compatible style.
        /// </summary>
        internal void HintPlainFromSource()
        {
            if (_style == WriterStyleBlock && _scalarStyleHint == YamlScalarStyle.None && !_options.IgnoreSourceStyles)
            {
                _scalarStyleHint = YamlScalarStyle.Plain;
                _plainFromSource = true;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnScalarStyleHintWithoutScalar()
        {
            _scalarStyleHint = YamlScalarStyle.None;
            _plainFromSource = false;
            if (_softScalarStyleHint)
            {
                // The serializer's hint for a member whose value is not a string.
                _softScalarStyleHint = false;
                return;
            }

            if (!_options.SkipValidation)
            {
                ThrowHelper.ThrowInvalidOperationException_ScalarStyleHintNotApplicable();
            }
        }

        // WriteStartArray() and WriteStartObject(), after the depth check; the caller increments the depth.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStartBlock(byte token)
        {
            if (!_options.SkipValidation)
            {
                ValidateStart();
            }

            CheckNoScalarStyleHint();
            if (_blockSpecial != 0)
            {
                StartBlockCollectionSpecial(token);
                return;
            }

            EnclosingContainerType parent = _enclosingContainer;
            BeginBlockValue(scalar: false, extra: 0);
            UpdateBitStackOnStart(token);
            _blockColumn += BlockDelta(token == YamlConstants.OpenBracket, parent);
        }

        // WriteStartArray(name) and WriteStartObject(name), after ValidateWritingProperty(token), which pushed the new
        // collection when validation is enabled; the caller increments the depth.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStartBlock(ReadOnlySpan<byte> escapedPropertyName, byte token)
        {
            UndoStartPush();
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            StartBlockCollectionAfterKey(token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStartBlock(ReadOnlySpan<char> escapedPropertyName, byte token)
        {
            UndoStartPush();
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            StartBlockCollectionAfterKey(token);
        }

        private void UndoStartPush()
        {
            if (!_options.SkipValidation)
            {
                // The key is written in the enclosing mapping; the collection is pushed again after it.
                _bitStack.Pop();
                _enclosingContainer = EnclosingContainerType.Object;
            }
        }

        private void StartBlockCollectionAfterKey(byte token)
        {
            CheckNoScalarStyleHint();
            _tokenType = YamlTokenType.PropertyName;
            if (_blockSpecial != 0)
            {
                StartBlockCollectionSpecial(token);
                return;
            }

            UpdateBitStackOnStart(token);
            _blockColumn += BlockDelta(token == YamlConstants.OpenBracket, EnclosingContainerType.Object);
        }

        // WriteEndArray() and WriteEndObject(); the caller decrements the depth and sets the token type.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEndBlock(byte token)
        {
            if (_blockSpecial != 0)
            {
                WriteEndBlockSpecial(token);
                return;
            }

            CheckNoScalarStyleHint();

            bool empty = _tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray;
            if (!_options.SkipValidation)
            {
                ValidateEnd(token);
            }
            else if (_bitStack.CurrentDepth > 0)
            {
                EnclosingContainerType container = _bitStack.Pop() ? EnclosingContainerType.Object : EnclosingContainerType.Array;
                _enclosingContainer = _bitStack.CurrentDepth == 0 ? EnclosingContainerType.None : container;
            }

            bool root = _bitStack.CurrentDepth == 0;
            int required = 3 + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (empty)
            {
                // "{}" at the root, " {}" after "key:" or "-".
                if (!root)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                output[BytesPending++] = token == YamlConstants.CloseBracket ? YamlConstants.OpenBracket : YamlConstants.OpenBrace;
                output[BytesPending++] = token;
                _block!.EndOfLineCommentAfterEnd = BlockPosition;
            }

            _blockColumn -= BlockDelta(token == YamlConstants.CloseBracket, _enclosingContainer);
            if (root)
            {
                WriteNewLine(output);
                _blockColumn = 0;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Escaped input

        // A property name or string value as the dispatch point receives it (JSON-escaped, see the top of this file),
        // as the text it stands for: valid UTF-8, unescaped. The result is in `stackBuffer` or a rented array when it
        // differs from the input.
        private static ReadOnlySpan<byte> UnescapeForBlock(ReadOnlySpan<byte> escaped, ref byte[]? rented, Span<byte> stackBuffer)
        {
            int backslash = escaped.IndexOf(YamlConstants.BackSlash);
            if (backslash < 0)
            {
                return escaped;
            }

            Span<byte> destination = escaped.Length <= stackBuffer.Length
                ? stackBuffer
                : (rented = ArrayPool<byte>.Shared.Rent(escaped.Length));
            YamlReaderHelper.Unescape(escaped, destination, backslash, out int written);
            return destination.Slice(0, written);
        }

        private static ReadOnlySpan<byte> TranscodeForBlock(ReadOnlySpan<char> escaped, ref byte[]? rented, Span<byte> stackBuffer)
        {
            int maxLength = escaped.Length * YamlConstants.MaxExpansionFactorWhileTranscoding;
            Span<byte> destination = maxLength <= stackBuffer.Length
                ? stackBuffer
                : (rented = ArrayPool<byte>.Shared.Rent(maxLength));
            if (YamlWriterHelper.ToUtf8(escaped, destination, out int written) != OperationStatus.Done)
            {
                // An unpaired surrogate becomes U+FFFD, as the JSON-compatible style's encoder makes it.
                written = YamlWriterHelper.ToUtf8Replacing(escaped, destination);
            }

            // The text is unescaped in place: unescaping never makes it longer.
            int backslash = destination.Slice(0, written).IndexOf(YamlConstants.BackSlash);
            if (backslash >= 0)
            {
                YamlReaderHelper.Unescape(destination.Slice(0, written), destination, backslash, out written);
            }

            return destination.Slice(0, written);
        }

        private static void ReturnRented(byte[]? rented)
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // The one-scan entries (M4p, candidate b)
        //
        // A string, a name, or both, as the public entry points receive them, in block style with the block encoder in
        // place. The shared escaping scan and the block body's scan are one pass (ScanUnescapedAscii). For a text that
        // pass does not judge, the escaping scan resumes where it stopped (BlockEncoderEscapeIndex), and the text takes
        // the shared path's tail with that index, as it would have without the one scan. Each returns false, having done
        // nothing, only when another encoder is set.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringValueBlockOneScan(ReadOnlySpan<byte> utf8Value)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan scan = YamlWriterHelper.ScanUnescapedAscii(utf8Value, out int stop);
            if (scan == YamlWriterHelper.BlockScan.Unknown)
            {
                int valueIdx = YamlWriterHelper.BlockEncoderEscapeIndex(utf8Value, stop);
                if (valueIdx != -1)
                {
                    WriteStringEscapeValue(utf8Value, valueIdx);
                }
                else
                {
                    WriteStringByOptions(utf8Value);
                }

                return true;
            }

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            WriteStringValueBlock(utf8Value, scan);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringValueBlockOneScan(ReadOnlySpan<char> value)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan scan = YamlWriterHelper.ScanUnescapedAscii(value, out int stop);
            if (scan == YamlWriterHelper.BlockScan.Unknown)
            {
                int valueIdx = YamlWriterHelper.BlockEncoderEscapeIndex(value, stop);
                if (valueIdx != -1)
                {
                    // The text before `stop` is ASCII the scan judged, with no line break.
                    if (value.Slice(stop).IndexOf('\n') < 0 || !TryWriteLiteralStringValue(value))
                    {
                        WriteStringEscapeValue(value, valueIdx);
                    }
                }
                else
                {
                    WriteStringByOptions(value, value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding);
                }

                return true;
            }

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            WriteStringValueBlock(value, scan);
            return true;
        }

        // M7d item 4: a string with a line break, under the default multi-line style
        // (literal), is written as a literal block scalar from its UTF-8 form. The general path escapes it, transcodes
        // the escaped text into the output, copies it back out and unescapes it before it reaches the same literal
        // writer. False, with nothing written, when that path must decide: a hint, a flow collection, a user encoder,
        // an unpaired surrogate, or text a literal cannot hold.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteLiteralStringValue(ReadOnlySpan<char> value)
        {
            if (_blockSpecial != 0 || _scalarStyleHint != YamlScalarStyle.None || UserEncoder is not null
                || _options.MultiLineStringStyle != YamlScalarStyle.None)
            {
                return false;
            }

            int maxBytes = value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding;
            byte[]? rented = null;
            Span<byte> buffer = maxBytes <= YamlConstants.StackallocByteThreshold
                ? stackalloc byte[YamlConstants.StackallocByteThreshold]
                : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));
            bool written = false;
            if (YamlWriterHelper.ToUtf8(value, buffer, out int length) == OperationStatus.Done)
            {
                ReadOnlySpan<byte> text = buffer.Slice(0, length);
                if (YamlWriterHelper.CanWriteLiteral(text, _lenient))
                {
                    if (!_options.SkipValidation)
                    {
                        ValidateWritingValue();
                    }

                    if (TryWriteBlockScalar(text, folded: false))
                    {
                        EndBlockScalar(YamlTokenType.String);
                        written = true;
                    }
                }
            }

            ReturnRented(rented);
            return written;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWritePropertyNameBlockOneScan(ReadOnlySpan<byte> utf8PropertyName)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan scan = YamlWriterHelper.ScanUnescapedAscii(utf8PropertyName, out int stop);
            if (scan == YamlWriterHelper.BlockScan.Unknown)
            {
                int propertyIdx = YamlWriterHelper.BlockEncoderEscapeIndex(utf8PropertyName, stop);
                if (propertyIdx != -1)
                {
                    WriteStringEscapeProperty(utf8PropertyName, propertyIdx);
                }
                else
                {
                    WriteStringByOptionsPropertyName(utf8PropertyName);
                }

                return true;
            }

            ValidateWritingProperty();
            CheckNoCollectionStyleHint();
            WriteBlockKey(utf8PropertyName, consumeHint: true, scan);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWritePropertyNameBlockOneScan(ReadOnlySpan<char> propertyName)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan scan = YamlWriterHelper.ScanUnescapedAscii(propertyName, out int stop);
            if (scan == YamlWriterHelper.BlockScan.Unknown)
            {
                int propertyIdx = YamlWriterHelper.BlockEncoderEscapeIndex(propertyName, stop);
                if (propertyIdx != -1)
                {
                    WriteStringEscapeProperty(propertyName, propertyIdx);
                }
                else
                {
                    WriteStringByOptionsPropertyName(propertyName);
                }

                return true;
            }

            ValidateWritingProperty();
            CheckNoCollectionStyleHint();
            WriteBlockKey(propertyName, consumeHint: true, scan);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringBlockOneScan(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan keyScan = YamlWriterHelper.ScanUnescapedAscii(utf8PropertyName, out int keyStop);
            YamlWriterHelper.BlockScan valueScan = YamlWriterHelper.ScanUnescapedAscii(utf8Value, out int valueStop);
            if (keyScan == YamlWriterHelper.BlockScan.Unknown || valueScan == YamlWriterHelper.BlockScan.Unknown)
            {
                int propertyIdx = keyScan == YamlWriterHelper.BlockScan.Unknown ? YamlWriterHelper.BlockEncoderEscapeIndex(utf8PropertyName, keyStop) : -1;
                int valueIdx = valueScan == YamlWriterHelper.BlockScan.Unknown ? YamlWriterHelper.BlockEncoderEscapeIndex(utf8Value, valueStop) : -1;
                if (valueIdx + propertyIdx != -2)
                {
                    WriteStringEscapePropertyOrValue(utf8PropertyName, utf8Value, propertyIdx, valueIdx);
                }
                else
                {
                    WriteStringByOptions(utf8PropertyName, utf8Value);
                }

                return true;
            }

            ValidateWritingProperty();
            WriteBlockKey(utf8PropertyName, consumeHint: false, keyScan);
            WriteStringValueBlock(utf8Value, valueScan);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringBlockOneScan(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan keyScan = YamlWriterHelper.ScanUnescapedAscii(propertyName, out int keyStop);
            YamlWriterHelper.BlockScan valueScan = YamlWriterHelper.ScanUnescapedAscii(value, out int valueStop);
            if (keyScan == YamlWriterHelper.BlockScan.Unknown || valueScan == YamlWriterHelper.BlockScan.Unknown)
            {
                int propertyIdx = keyScan == YamlWriterHelper.BlockScan.Unknown ? YamlWriterHelper.BlockEncoderEscapeIndex(propertyName, keyStop) : -1;
                int valueIdx = valueScan == YamlWriterHelper.BlockScan.Unknown ? YamlWriterHelper.BlockEncoderEscapeIndex(value, valueStop) : -1;
                if (valueIdx + propertyIdx != -2)
                {
                    WriteStringEscapePropertyOrValue(propertyName, value, propertyIdx, valueIdx);
                }
                else
                {
                    WriteStringByOptions(propertyName, value);
                }

                return true;
            }

            ValidateWritingProperty();
            if (keyScan == YamlWriterHelper.BlockScan.Plain && valueScan == YamlWriterHelper.BlockScan.Plain
                && TryWritePlainEntryBlock(default, propertyName, value))
            {
                return true;
            }

            WriteBlockKey(propertyName, consumeHint: false, keyScan);
            WriteStringValueBlock(value, valueScan);
            return true;
        }

        // A pre-encoded name (YamlEncodedText, escaped when it was made) and a UTF-16 value: only the value is scanned
        // here; the name takes the key body's scan, as before.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteEncodedNameStringBlockOneScan(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> value, YamlWriterHelper.BlockScan keyScan)
        {
            if (!ReferenceEquals(_options.Encoder, YamlBlockEncoder.Instance))
            {
                return false;
            }

            YamlWriterHelper.BlockScan valueScan = YamlWriterHelper.ScanUnescapedAscii(value, out int valueStop);
            if (valueScan == YamlWriterHelper.BlockScan.Unknown)
            {
                int valueIdx = YamlWriterHelper.BlockEncoderEscapeIndex(value, valueStop);
                if (valueIdx != -1)
                {
                    WriteStringEscapeValueOnly(escapedPropertyName, value, valueIdx);
                }
                else
                {
                    WriteStringByOptions(escapedPropertyName, value);
                }

                return true;
            }

            ValidateWritingProperty();
            if (keyScan == YamlWriterHelper.BlockScan.PlainKey && valueScan == YamlWriterHelper.BlockScan.Plain
                && TryWritePlainEntryBlock(escapedPropertyName, default, value))
            {
                return true;
            }

            WriteBlockKey(escapedPropertyName, consumeHint: false, keyScan);
            WriteStringValueBlock(value, valueScan);
            return true;
        }

        // M7d item 3: the commonest mapping entry, a plain key and a plain string value (block style, no hint, no flow, no
        // anchor or tag pending), in one pass: one room check and one span for the entry's start, the key, ": " and the
        // value, which WriteBlockKey and WriteStringValueBlock otherwise take apart. The key is either a pre-encoded name
        // judged plain when it was encoded (`plainKeyBytes`, BlockScan.PlainKey) or UTF-16 text that ScanUnescapedAscii
        // judged Plain (`asciiKey`); the value is UTF-16 text it judged Plain, so both are ASCII with no '"'. The
        // written bytes are what the two calls write: the value quoted when it resolves to a non-string. A UTF-16
        // key that resolves to a non-string, or is lenient-quoted, returns false having written nothing, and the entry
        // takes the two calls.
        private bool TryWritePlainEntryBlock(ReadOnlySpan<byte> plainKeyBytes, ReadOnlySpan<char> asciiKey, ReadOnlySpan<char> value)
        {
            int keyLength = plainKeyBytes.Length + asciiKey.Length;
            if (_blockSpecial != 0 || _scalarStyleHint != YamlScalarStyle.None || _enclosingContainer != EnclosingContainerType.Object
                || keyLength > YamlConstants.MaxImplicitKeyLength - 2)
            {
                return false;
            }

            int required = 2 * BlockPrefixLength + keyLength + value.Length + 6 + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            int mark = BytesPending;
            WriteBlockEntryStart(output);
            if (asciiKey.IsEmpty)
            {
                plainKeyBytes.CopyTo(output.Slice(BytesPending));
            }
            else
            {
                Span<byte> key = output.Slice(BytesPending, keyLength);
                YamlWriterHelper.NarrowAscii(asciiKey, key);
                if (YamlWriterHelper.ResolvesToNonString(key, _lenient) || (_lenient && YamlWriterHelper.HasSpaceBeforeAsterisk(key)))
                {
                    BytesPending = mark;
                    return false;
                }
            }

            BytesPending += keyLength;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;
            output[BytesPending++] = YamlConstants.Space;
            int start = BytesPending;
            Span<byte> text = output.Slice(start, value.Length);
            YamlWriterHelper.NarrowAscii(value, text);
            if (!YamlWriterHelper.ResolvesToNonString(text, _lenient))
            {
                BytesPending += value.Length;
            }
            else
            {
                // Quoted with nothing to escape: the text moves one byte right, between the quotes.
                text.CopyTo(output.Slice(start + 1));
                output[start] = YamlConstants.Quote;
                output[start + 1 + value.Length] = YamlConstants.Quote;
                BytesPending += value.Length + 2;
            }

            _tokenType = YamlTokenType.String;
            return true;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Property names

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WritePropertyNameBlock(ReadOnlySpan<byte> escapedPropertyName, YamlWriterHelper.BlockScan scan)
        {
            CheckNoCollectionStyleHint();
            WriteBlockKey(escapedPropertyName, consumeHint: true, scan);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WritePropertyNameBlock(ReadOnlySpan<char> escapedPropertyName)
        {
            CheckNoCollectionStyleHint();
            WriteBlockKey(escapedPropertyName, consumeHint: true);
        }

        // A key: `consumeHint` is false in the forms that write a name and a value in one call, where the hint is
        // the value's. The common case (no hint, no explicit encoder, no escape, short) is judged on the bytes as
        // they are, in one pass; everything else goes through the text.
        private void WriteBlockKey(ReadOnlySpan<byte> escapedPropertyName, bool consumeHint) =>
            WriteBlockKey(escapedPropertyName, consumeHint, YamlWriterHelper.BlockScan.Unknown);

        // With `scan` already known (the one-scan entries, M4p candidate b), the key is not scanned again.
        private void WriteBlockKey(ReadOnlySpan<byte> escapedPropertyName, bool consumeHint, YamlWriterHelper.BlockScan scan)
        {
            // Short enough that even with quotes it cannot need an explicit key.
            if (_blockSpecial == 0 && (!consumeHint || _scalarStyleHint == YamlScalarStyle.None) && UserEncoder is null
                && escapedPropertyName.Length <= YamlConstants.MaxImplicitKeyLength - 2)
            {
                if (scan == YamlWriterHelper.BlockScan.PlainKey)
                {
                    // A pre-encoded name judged when it was encoded (M4p candidate a).
                    WriteBlockKeyBytes(escapedPropertyName, quoted: false, explicitKey: false);
                    _tokenType = YamlTokenType.PropertyName;
                    return;
                }

                if (scan == YamlWriterHelper.BlockScan.Unknown)
                {
                    scan = YamlWriterHelper.ScanEscapedText(escapedPropertyName);
                }

                if (scan != YamlWriterHelper.BlockScan.Escaped)
                {
                    bool plain = scan == YamlWriterHelper.BlockScan.Plain && !YamlWriterHelper.ResolvesToNonString(escapedPropertyName, _lenient)
                        && !(_lenient && YamlWriterHelper.HasSpaceBeforeAsterisk(escapedPropertyName));
                    if (plain || (scan == YamlWriterHelper.BlockScan.Plain && escapedPropertyName.IndexOf(YamlConstants.Quote) < 0))
                    {
                        // Plain, or quoted with nothing to escape (a plain-safe text that resolves to a non-string).
                        WriteBlockKeyBytes(escapedPropertyName, quoted: !plain, explicitKey: false);
                        _tokenType = YamlTokenType.PropertyName;
                        return;
                    }
                }
            }

            WriteBlockKeySlow(escapedPropertyName, consumeHint);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBlockKeySlow(ReadOnlySpan<byte> escapedPropertyName, bool consumeHint)
        {
            byte[]? rented = null;
            ReadOnlySpan<byte> key = UnescapeForBlock(escapedPropertyName, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteBlockKeyText(key, consumeHint);
            ReturnRented(rented);
        }

        private void WriteBlockKey(ReadOnlySpan<char> escapedPropertyName, bool consumeHint) =>
            WriteBlockKey(escapedPropertyName, consumeHint, YamlWriterHelper.BlockScan.Unknown);

        // With `scan` already known (the one-scan entries, M4p candidate b), the transcoded key is not scanned again.
        private void WriteBlockKey(ReadOnlySpan<char> escapedPropertyName, bool consumeHint, YamlWriterHelper.BlockScan scan)
        {
            int maxBytes = escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding;
            if (_blockSpecial == 0 && (!consumeHint || _scalarStyleHint == YamlScalarStyle.None) && UserEncoder is null
                && maxBytes <= YamlConstants.MaxImplicitKeyLength)
            {
                // Transcoded straight into the output, after the entry's start; judged there.
                Span<byte> output = BeginBlockKey(maxBytes + 2, explicitKey: false);
                int start = BytesPending;
                if (YamlWriterHelper.ToUtf8(escapedPropertyName, output.Slice(start), out int length) != OperationStatus.Done)
                {
                    // An unpaired surrogate becomes U+FFFD, as the JSON-compatible style's encoder makes it.
                    length = YamlWriterHelper.ToUtf8Replacing(escapedPropertyName, output.Slice(start));
                }
                ReadOnlySpan<byte> key = output.Slice(start, length);
                if (scan == YamlWriterHelper.BlockScan.Unknown)
                {
                    scan = YamlWriterHelper.ScanEscapedText(key);
                }

                if (scan == YamlWriterHelper.BlockScan.Plain && !YamlWriterHelper.ResolvesToNonString(key, _lenient)
                    && !(_lenient && YamlWriterHelper.HasSpaceBeforeAsterisk(key)))
                {
                    BytesPending += length;
                    output[BytesPending++] = YamlConstants.KeyValueSeparator;
                    _tokenType = YamlTokenType.PropertyName;
                    return;
                }

                // Not plain: the text moves to a buffer of its own, and the key is written from there, after the
                // entry's start already written.
                WriteBlockKeyAfterStart(key, consumeHint);
                return;
            }

            byte[]? rented = null;
            ReadOnlySpan<byte> text = TranscodeForBlock(escapedPropertyName, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteBlockKeyText(text, consumeHint);
            ReturnRented(rented);
        }

        // A short key (no explicit form possible) whose entry start is written, from its transcoded bytes in the output.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBlockKeyAfterStart(ReadOnlySpan<byte> transcodedInOutput, bool consumeHint)
        {
            Span<byte> copy = stackalloc byte[YamlConstants.StackallocByteThreshold];
            byte[]? rented = transcodedInOutput.Length > copy.Length ? ArrayPool<byte>.Shared.Rent(transcodedInOutput.Length) : null;
            Span<byte> buffer = rented ?? copy;
            transcodedInOutput.CopyTo(buffer);
            ReadOnlySpan<byte> key = buffer.Slice(0, transcodedInOutput.Length);
            byte[]? unescaped = null;
            key = UnescapeForBlock(key, ref unescaped, stackalloc byte[YamlConstants.StackallocByteThreshold]);

            YamlScalarStyle hint = YamlScalarStyle.None;
            bool fromSource = false;
            if (consumeHint)
            {
                hint = _scalarStyleHint;
                fromSource = _plainFromSource && hint == YamlScalarStyle.Plain;
                _scalarStyleHint = YamlScalarStyle.None;
                _plainFromSource = false;
            }

            // A plain source key (the DOM's hint) needs only the syntactic check, as a plain source value does.
            bool plain = hint is not (YamlScalarStyle.DoubleQuoted or YamlScalarStyle.SingleQuoted)
                && (fromSource ? YamlWriterHelper.IsBlockPlainSyntax(key) && !(_lenient && YamlWriterHelper.HasSpaceBeforeAsterisk(key))
                    : YamlWriterHelper.IsBlockPlainKey(key, _lenient));

            // The written form is known before anything follows the entry's start, so "? " can still go first.
            byte[] escaped = ArrayPool<byte>.Shared.Rent(YamlWriterHelper.GetMaxBlockEscapedLength(key.Length) + 2);
            int written;
            if (plain)
            {
                key.CopyTo(escaped);
                written = key.Length;
            }
            else
            {
                escaped[0] = YamlConstants.Quote;
                written = 1 + YamlWriterHelper.EscapeBlockDoubleQuoted(key, escaped.AsSpan(1), UserEncoder);
                escaped[written++] = YamlConstants.Quote;
            }

            ReadOnlySpan<byte> form = escaped.AsSpan(0, written);
            bool explicitKey = YamlWriterHelper.NeedsExplicitKey(written, plain ? form : form.Slice(1, written - 2));
            int required = written + 2 * BlockPrefixLength + 3;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (explicitKey)
            {
                output[BytesPending++] = (byte)'?';
                output[BytesPending++] = YamlConstants.Space;
            }

            form.CopyTo(output.Slice(BytesPending));
            BytesPending += written;
            ArrayPool<byte>.Shared.Return(escaped);
            EndBlockKey(output, explicitKey);
            _tokenType = YamlTokenType.PropertyName;
            ReturnRented(unescaped);
            ReturnRented(rented);
        }

        // A key from its text: plain when the scalar rules allow it, double-quoted otherwise, and explicit ("? key") when
        // its written form is longer than 1024 characters.
        private void WriteBlockKeyText(ReadOnlySpan<byte> key, bool consumeHint)
        {
            YamlScalarStyle hint = YamlScalarStyle.None;
            bool fromSource = false;
            if (consumeHint)
            {
                hint = _scalarStyleHint;
                fromSource = _plainFromSource && hint == YamlScalarStyle.Plain;
                _scalarStyleHint = YamlScalarStyle.None;
                _plainFromSource = false;
            }

            // M7b (fix A): a dictionary's string key takes DefaultScalarStyle where no hint does; a key cannot be a block
            // scalar, so a literal or folded default leaves it to the writer's choice, as a plain one does.
            if (_defaultStyleKey)
            {
                _defaultStyleKey = false;
                if (hint == YamlScalarStyle.None && !_plainNumberKey)
                {
                    hint = _options.DefaultScalarStyle;
                }
            }

            bool flow = _blockSpecial != 0 && InFlow;
            if (hint == YamlScalarStyle.SingleQuoted && YamlWriterHelper.CanWriteSingleQuoted(key) && !EncoderEscapes(key))
            {
                WriteBlockSingleQuotedKey(key);
                _tokenType = YamlTokenType.PropertyName;
                return;
            }

            bool plain = hint is not (YamlScalarStyle.DoubleQuoted or YamlScalarStyle.SingleQuoted)
                && (_plainNumberKey || (fromSource
                    ? YamlWriterHelper.IsBlockPlainSyntax(key) && !(_lenient && YamlWriterHelper.HasSpaceBeforeAsterisk(key))
                    : YamlWriterHelper.IsBlockPlainKey(key, _lenient)))
                && (!flow || YamlWriterHelper.IsFlowPlain(key, _lenient))
                && !EncoderEscapes(key);

            if (plain)
            {
                WriteBlockKeyBytes(key, quoted: false, YamlWriterHelper.NeedsExplicitKey(key.Length, key));
            }
            else
            {
                WriteBlockQuotedKey(key);
            }

            _tokenType = YamlTokenType.PropertyName;
        }

        private void WriteBlockSingleQuotedKey(ReadOnlySpan<byte> key)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(2 * key.Length + 2);
            int written = WriteSingleQuoted(key, rented);
            ReadOnlySpan<byte> quoted = rented.AsSpan(0, written);
            WriteBlockKeyBytes(quoted, quoted: false, YamlWriterHelper.NeedsExplicitKey(written, quoted.Slice(1, written - 2)));
            ArrayPool<byte>.Shared.Return(rented);
        }

        private void WriteBlockQuotedKey(ReadOnlySpan<byte> key)
        {
            int maxEscaped = YamlWriterHelper.GetMaxBlockEscapedLength(key.Length);
            if (maxEscaped + 2 <= YamlConstants.MaxImplicitKeyLength)
            {
                // Short enough that it cannot need an explicit key: escaped straight into the output.
                Span<byte> output = BeginBlockKey(maxEscaped + 2, explicitKey: false);
                output[BytesPending++] = YamlConstants.Quote;
                BytesPending += YamlWriterHelper.EscapeBlockDoubleQuoted(key, output.Slice(BytesPending), UserEncoder);
                output[BytesPending++] = YamlConstants.Quote;
                EndBlockKey(output, explicitKey: false);
                return;
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(maxEscaped);
            try
            {
                int written = YamlWriterHelper.EscapeBlockDoubleQuoted(key, rented, UserEncoder);
                ReadOnlySpan<byte> escaped = rented.AsSpan(0, written);
                WriteBlockKeyBytes(escaped, quoted: true, YamlWriterHelper.NeedsExplicitKey(written + 2, escaped));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // Writes a key whose bytes are ready: plain bytes, or the escaped content of a double-quoted key.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBlockKeyBytes(ReadOnlySpan<byte> bytes, bool quoted, bool explicitKey)
        {
            Span<byte> output = BeginBlockKey(bytes.Length + 2, explicitKey);
            if (quoted)
            {
                output[BytesPending++] = YamlConstants.Quote;
            }

            bytes.CopyTo(output.Slice(BytesPending));
            BytesPending += bytes.Length;
            if (quoted)
            {
                output[BytesPending++] = YamlConstants.Quote;
            }

            EndBlockKey(output, explicitKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> BeginBlockKey(int keyLength, bool explicitKey)
        {
            if (_blockSpecial != 0)
            {
                return BeginBlockKeySpecial(keyLength, explicitKey);
            }

            int required = 2 * BlockPrefixLength + keyLength + 3;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            WriteBlockEntryStart(output);
            if (explicitKey)
            {
                output[BytesPending++] = (byte)'?';
                output[BytesPending++] = YamlConstants.Space;
            }

            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EndBlockKey(Span<byte> output, bool explicitKey)
        {
            if (explicitKey)
            {
                // "? key", then ":" on the next line at the key's column; the value follows as after "key:". In a flow
                // mapping, " :" on the same line.
                if (InFlow)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
                else
                {
                    WriteBlockLineStart(output, _blockColumn);
                }
            }

            output[BytesPending++] = YamlConstants.KeyValueSeparator;
        }

        /// <summary>
        /// The serializer's reflection path: the property's name section, <c>"name":</c> in JSON-compatible style, or its
        /// block form, computed when the property is configured.
        /// </summary>
        internal void WritePropertyNameSection(YamlPropertyInfo property)
        {
            ReadOnlySpan<byte> escapedPropertyNameSection = property.EscapedNameSection;
            if (_style == WriterStyleJsonMinimized)
            {
                Debug.Assert(escapedPropertyNameSection.Length <= YamlConstants.MaxUnescapedTokenSize - 3);

                WriteStringPropertyNameSection(escapedPropertyNameSection);

                _currentDepth &= YamlConstants.RemoveFlagsBitMask;
                _tokenType = YamlTokenType.PropertyName;
                _commentAfterNoneOrPropertyName = false;
            }
            else if (_style == WriterStyleJsonIndented)
            {
                ReadOnlySpan<byte> escapedPropertyName =
                    escapedPropertyNameSection.Slice(1, escapedPropertyNameSection.Length - 3);

                WritePropertyNameHelper(escapedPropertyName);
            }
            else
            {
                WritePropertyNameSectionBlock(property);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WritePropertyNameSectionBlock(YamlPropertyInfo property)
        {
            ValidateWritingProperty();
            CheckNoCollectionStyleHint();
            byte[]? section = property.BlockNameSection;
            if (section is null || _scalarStyleHint != YamlScalarStyle.None || _blockSpecial != 0)
            {
                WriteBlockKeyText(property.NameAsUtf8Bytes, consumeHint: true);
            }
            else
            {
                Span<byte> output = BeginBlockKey(section.Length, explicitKey: false);
                section.CopyTo(output.Slice(BytesPending));
                BytesPending += section.Length;
                _tokenType = YamlTokenType.PropertyName;
            }

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _commentAfterNoneOrPropertyName = false;
        }

        /// <summary>The reflection path's null-valued property, in either style.</summary>
        internal void WriteNullSection(YamlPropertyInfo property)
        {
            ReadOnlySpan<byte> escapedPropertyNameSection = property.EscapedNameSection;
            if (_style == WriterStyleJsonMinimized)
            {
                Debug.Assert(escapedPropertyNameSection.Length <= YamlConstants.MaxUnescapedTokenSize - 3);

                ReadOnlySpan<byte> span = YamlConstants.NullValue;

                WriteLiteralSection(escapedPropertyNameSection, span);

                SetFlagToAddListSeparatorBeforeNextItem();
                _tokenType = YamlTokenType.Null;
            }
            else if (_style == WriterStyleJsonIndented)
            {
                ReadOnlySpan<byte> escapedName =
                    escapedPropertyNameSection.Slice(1, escapedPropertyNameSection.Length - 3);

                WriteLiteralHelper(escapedName, YamlConstants.NullValue);
                _tokenType = YamlTokenType.Null;
            }
            else
            {
                WritePropertyNameSectionBlock(property);
                WriteLiteralValueBlock(YamlConstants.NullValue);
                SetFlagToAddListSeparatorBeforeNextItem();
                _tokenType = YamlTokenType.Null;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // String values

        // A string value. The common case (no hint, no explicit encoder, no escape) is judged on the bytes as they are,
        // in one pass; everything else goes through the text.
        private void WriteStringValueBlock(ReadOnlySpan<byte> escapedValue) =>
            WriteStringValueBlock(escapedValue, YamlWriterHelper.BlockScan.Unknown);

        // With `scan` already known (the one-scan entries, M4p candidate b), the value is not scanned again.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlock(ReadOnlySpan<byte> escapedValue, YamlWriterHelper.BlockScan scan)
        {
            if (_blockSpecial == 0 && _scalarStyleHint == YamlScalarStyle.None && UserEncoder is null)
            {
                if (scan == YamlWriterHelper.BlockScan.Unknown)
                {
                    scan = YamlWriterHelper.ScanEscapedText(escapedValue);
                }

                if (scan != YamlWriterHelper.BlockScan.Escaped)
                {
                    bool plain = scan == YamlWriterHelper.BlockScan.Plain && !YamlWriterHelper.ResolvesToNonString(escapedValue, _lenient);
                    if (plain || (scan == YamlWriterHelper.BlockScan.Plain && escapedValue.IndexOf(YamlConstants.Quote) < 0))
                    {
                        // Plain, or quoted with nothing to escape.
                        Span<byte> output = BeginBlockValue(scalar: true, escapedValue.Length + 2);
                        if (!plain)
                        {
                            output[BytesPending++] = YamlConstants.Quote;
                        }

                        escapedValue.CopyTo(output.Slice(BytesPending));
                        BytesPending += escapedValue.Length;
                        if (!plain)
                        {
                            output[BytesPending++] = YamlConstants.Quote;
                        }

                        EndBlockScalar(YamlTokenType.String);
                        return;
                    }

                    // Not plain, with no escape: the bytes are the text.
                    WriteStringValueBlockText(escapedValue);
                    return;
                }
            }

            WriteStringValueBlockSlow(escapedValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlockSlow(ReadOnlySpan<byte> escapedValue)
        {
            byte[]? rented = null;
            ReadOnlySpan<byte> text = UnescapeForBlock(escapedValue, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteStringValueBlockText(text);
            ReturnRented(rented);
        }

        private void WriteStringValueBlock(ReadOnlySpan<char> escapedValue) =>
            WriteStringValueBlock(escapedValue, YamlWriterHelper.BlockScan.Unknown);

        // With `scan` already known (the one-scan entries, M4p candidate b), the transcoded value is not scanned again.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlock(ReadOnlySpan<char> escapedValue, YamlWriterHelper.BlockScan scan)
        {
            if (_blockSpecial == 0 && _scalarStyleHint == YamlScalarStyle.None && UserEncoder is null)
            {
                // Transcoded straight into the output, after the value's prefix; judged there. The room covers the
                // quoted form of a plain-safe text too (two quotes).
                int maxBytes = escapedValue.Length * YamlConstants.MaxExpansionFactorWhileTranscoding;
                Span<byte> output = BeginBlockValue(scalar: true, maxBytes + 2);
                int start = BytesPending;
                if (YamlWriterHelper.ToUtf8(escapedValue, output.Slice(start), out int length) != OperationStatus.Done)
                {
                    // An unpaired surrogate becomes U+FFFD, as the JSON-compatible style's encoder makes it.
                    length = YamlWriterHelper.ToUtf8Replacing(escapedValue, output.Slice(start));
                }
                Span<byte> text = output.Slice(start, length);
                if (scan == YamlWriterHelper.BlockScan.Unknown)
                {
                    scan = YamlWriterHelper.ScanEscapedText(text);
                }

                if (scan == YamlWriterHelper.BlockScan.Plain)
                {
                    if (!YamlWriterHelper.ResolvesToNonString(text, _lenient))
                    {
                        BytesPending += length;
                        EndBlockScalar(YamlTokenType.String);
                        return;
                    }

                    if (text.IndexOf(YamlConstants.Quote) < 0)
                    {
                        // Quoted with nothing to escape: the text moves one byte right, between the quotes.
                        text.CopyTo(output.Slice(start + 1));
                        output[start] = YamlConstants.Quote;
                        output[start + 1 + length] = YamlConstants.Quote;
                        BytesPending += length + 2;
                        EndBlockScalar(YamlTokenType.String);
                        return;
                    }
                }

                // The text moves to a buffer of its own and is written from there, after the prefix already written.
                WriteStringValueBlockFromOutput(text);
                return;
            }

            byte[]? rented = null;
            ReadOnlySpan<byte> converted = TranscodeForBlock(escapedValue, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteStringValueBlockText(converted);
            ReturnRented(rented);
        }

        // The value's prefix is written; its transcoded bytes are in the output after it, and are written again from a
        // copy, unescaped, in the style the scalar rules give them.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlockFromOutput(ReadOnlySpan<byte> transcodedInOutput)
        {
            Span<byte> copy = stackalloc byte[YamlConstants.StackallocByteThreshold];
            byte[]? rented = transcodedInOutput.Length > copy.Length ? ArrayPool<byte>.Shared.Rent(transcodedInOutput.Length) : null;
            Span<byte> buffer = rented ?? copy;
            transcodedInOutput.CopyTo(buffer);
            ReadOnlySpan<byte> text = buffer.Slice(0, transcodedInOutput.Length);
            byte[]? unescaped = null;
            text = UnescapeForBlock(text, ref unescaped, stackalloc byte[YamlConstants.StackallocByteThreshold]);

            bool plain = YamlWriterHelper.IsBlockPlain(text, _lenient);

            // M5f: the default writes a string with a line break as a literal block scalar where it can. Only
            // this path's non-plain branch tests for it, so the default keeps _blockSpecial at 0. The fast path
            // that reaches here has no hint and no flow collection; the value's prefix is already written.
            if (!plain && _options.MultiLineStringStyle == YamlScalarStyle.None && text.IndexOf((byte)'\n') >= 0
                && YamlWriterHelper.CanWriteLiteral(text, _lenient) && TryWriteBlockScalar(text, folded: false, prefixWritten: true))
            {
                EndBlockScalar(YamlTokenType.String);
                ReturnRented(unescaped);
                ReturnRented(rented);
                return;
            }

            int required = (plain ? text.Length : YamlWriterHelper.GetMaxBlockEscapedLength(text.Length) + 2) + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (plain)
            {
                text.CopyTo(output.Slice(BytesPending));
                BytesPending += text.Length;
            }
            else
            {
                output[BytesPending++] = YamlConstants.Quote;
                BytesPending += YamlWriterHelper.EscapeBlockDoubleQuoted(text, output.Slice(BytesPending), UserEncoder);
                output[BytesPending++] = YamlConstants.Quote;
            }

            EndBlockScalar(YamlTokenType.String);
            ReturnRented(unescaped);
            ReturnRented(rented);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            WriteStringValueBlock(escapedValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            WriteStringValueBlock(escapedValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            WriteStringValueBlock(escapedValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            WriteStringValueBlock(escapedValue);
        }

        // A string value from its text: literal or folded when asked for (a hint, or MultiLineStringStyle for a
        // string with a line break) and possible, single-quoted when asked for and possible, plain when the scalar rules
        // allows it, double-quoted otherwise. Inside a flow collection, plain is stricter and block scalars are not
        // written; with MaxLineWidth, plain and double-quoted scalars are folded.
        private void WriteStringValueBlockText(ReadOnlySpan<byte> text)
        {
            YamlScalarStyle hint = _scalarStyleHint;
            _scalarStyleHint = YamlScalarStyle.None;
            bool fromSource = _plainFromSource;
            _plainFromSource = false;
            bool flow = _blockSpecial != 0 && InFlow;

            // M7b: DefaultScalarStyle, below every hint and below a MultiLineStringStyle that is set
            // for a string with a line break. Where it cannot write the text, the writer's default applies: a plain or
            // single-quoted default that cannot hold a line break leaves the block style to the default below.
            bool fromDefault = false;
            if (hint == YamlScalarStyle.None && (_blockSpecial & SpecialDefaultStyle) != 0
                && (_options.MultiLineStringStyle == YamlScalarStyle.None || text.IndexOf((byte)'\n') < 0))
            {
                hint = _options.DefaultScalarStyle;
                fromDefault = true;
            }

            if (!flow)
            {
                YamlScalarStyle blockStyle = hint;
                if (hint == YamlScalarStyle.None || ((fromSource || fromDefault) && hint == YamlScalarStyle.Plain)
                    || (fromDefault && hint == YamlScalarStyle.SingleQuoted && !YamlWriterHelper.CanWriteMultiLineSingleQuoted(text)))
                {
                    // M5f: the unset MultiLineStringStyle means literal. A plain source scalar (the DOM's hint)
                    // cannot hold a line break, so it takes the default too.
                    blockStyle = _options.MultiLineStringStyle is YamlScalarStyle.None ? YamlScalarStyle.Literal : _options.MultiLineStringStyle;
                }

                if (blockStyle is YamlScalarStyle.Literal or YamlScalarStyle.Folded && YamlWriterHelper.CanWriteLiteral(text, _lenient)
                    && !EncoderEscapes(text) && TryWriteBlockScalar(text, folded: blockStyle == YamlScalarStyle.Folded))
                {
                    EndBlockScalar(YamlTokenType.String);
                    return;
                }
            }

            if (hint == YamlScalarStyle.SingleQuoted && YamlWriterHelper.CanWriteSingleQuoted(text) && !EncoderEscapes(text))
            {
                Span<byte> output = BeginBlockValue(scalar: true, 2 * text.Length + 2);
                BytesPending += WriteSingleQuoted(text, output.Slice(BytesPending));
                EndBlockScalar(YamlTokenType.String);
                return;
            }

            if (hint == YamlScalarStyle.SingleQuoted && !flow && YamlWriterHelper.CanWriteMultiLineSingleQuoted(text) && !EncoderEscapes(text))
            {
                WriteMultiLineSingleQuoted(text);
                EndBlockScalar(YamlTokenType.String);
                return;
            }

            bool fold = _blockSpecial != 0 && (_blockSpecial & SpecialLineWidth) != 0 && !flow;
            if (hint is not (YamlScalarStyle.DoubleQuoted or YamlScalarStyle.SingleQuoted)
                && (_blockSpecial & SpecialJsonSchema) == 0 // M7c: under the JSON schema no string value is plain
                && (fromSource ? YamlWriterHelper.IsBlockPlainSyntax(text) : YamlWriterHelper.IsBlockPlain(text, _lenient))
                && (!flow || YamlWriterHelper.IsFlowPlain(text, _lenient))
                && !EncoderEscapes(text))
            {
                if (fold)
                {
                    WritePlainFolded(text);
                }
                else
                {
                    Span<byte> output = BeginBlockValue(scalar: true, text.Length);
                    text.CopyTo(output.Slice(BytesPending));
                    BytesPending += text.Length;
                }
            }
            else if (fold)
            {
                WriteDoubleQuotedFolded(text);
            }
            else
            {
                Span<byte> output = BeginBlockValue(scalar: true, YamlWriterHelper.GetMaxBlockEscapedLength(text.Length) + 2);
                output[BytesPending++] = YamlConstants.Quote;
                BytesPending += YamlWriterHelper.EscapeBlockDoubleQuoted(text, output.Slice(BytesPending), UserEncoder);
                output[BytesPending++] = YamlConstants.Quote;
            }

            EndBlockScalar(YamlTokenType.String);
        }

        // M7b: a single-quoted scalar over several lines, as YamlDotNet writes one. Line folding turns
        // one line break into a space, so a run of n line breaks in the text is written as n + 1; each line after the
        // first, and the closing quote after a final line break, is indented at the block scalar's column.
        private void WriteMultiLineSingleQuoted(ReadOnlySpan<byte> text)
        {
            bool root = _enclosingContainer == EnclosingContainerType.None;
            int contentColumn = root ? _indentLength : _blockColumn + _indentLength;
            int breaks = 0;
            foreach (byte b in text)
            {
                if (b == (byte)'\n')
                {
                    breaks++;
                }
            }

            int maxRequired = 2 * text.Length + 2 + 2 * (breaks + 1) * (_newLineLength + contentColumn);
            Span<byte> output = BeginBlockValue(scalar: true, maxRequired);
            output[BytesPending++] = (byte)'\'';
            int i = 0;
            while (true)
            {
                int end = text.Slice(i).IndexOf((byte)'\n');
                ReadOnlySpan<byte> line = end < 0 ? text.Slice(i) : text.Slice(i, end);
                foreach (byte b in line)
                {
                    output[BytesPending++] = b;
                    if (b == (byte)'\'')
                    {
                        output[BytesPending++] = (byte)'\'';
                    }
                }

                if (end < 0)
                {
                    break;
                }

                i += end;
                int run = 0;
                while (i < text.Length && text[i] == (byte)'\n')
                {
                    run++;
                    i++;
                }

                for (int n = 0; n <= run; n++)
                {
                    WriteNewLine(output);
                }

                WriteBlockIndentation(output, contentColumn);
                if (i == text.Length)
                {
                    break;
                }
            }

            output[BytesPending++] = (byte)'\'';
        }

        // 'text', with every ' doubled; the caller checked that nothing in it needs an escape. Returns the bytes written.
        private static int WriteSingleQuoted(ReadOnlySpan<byte> text, Span<byte> destination)
        {
            int written = 0;
            destination[written++] = (byte)'\'';
            while (true)
            {
                int quote = text.IndexOf((byte)'\'');
                ReadOnlySpan<byte> part = quote < 0 ? text : text.Slice(0, quote + 1);
                part.CopyTo(destination.Slice(written));
                written += part.Length;
                if (quote < 0)
                {
                    break;
                }

                destination[written++] = (byte)'\'';
                text = text.Slice(quote + 1);
            }

            destination[written++] = (byte)'\'';
            return written;
        }

        // The column the next byte goes to, from the bytes since the last line break. M7c:
        // when the line began before the last flush, the flushed part's column is kept (_flushedColumn), so folding and
        // flow breaks fall where the width asks whatever the buffer size.
        private int CurrentBlockColumn()
        {
            ReadOnlySpan<byte> pending = _memory.Span.Slice(0, BytesPending);
            int lineBreak = pending.LastIndexOf((byte)'\n');
            int column = YamlWriterHelper.CountCharacters(pending.Slice(lineBreak + 1));
            return lineBreak < 0 ? _flushedColumn + column : column;
        }

        // M7c: the column the pending bytes start at, kept up to date before they leave the buffer; only with MaxLineWidth.
        private int _flushedColumn;

        private void KeepColumnAcrossFlush()
        {
            if ((_blockSpecial & SpecialLineWidth) != 0 && BytesPending != 0)
            {
                ReadOnlySpan<byte> pending = _memory.Span.Slice(0, BytesPending);
                int lineBreak = pending.LastIndexOf((byte)'\n');
                int column = YamlWriterHelper.CountCharacters(pending.Slice(lineBreak + 1));
                _flushedColumn = lineBreak < 0 ? _flushedColumn + column : column;
            }
        }

        // The column of a scalar's continuation lines: past the parent collection's, and never 0, where "---" and "..."
        // would be document markers.
        private int ContinuationColumn => _blockColumn + _indentLength;

        // A plain value folded at single spaces between two characters that are not spaces, so that it reads back the
        // same (a line break in a plain scalar reads as one space, and the white space around it is trimmed); a
        // continuation line never starts with an indicator (MaxLineWidth).
        private void WritePlainFolded(ReadOnlySpan<byte> text)
        {
            int spaces = 0;
            foreach (byte b in text)
            {
                if (b == YamlConstants.Space)
                {
                    spaces++;
                }
            }

            int indent = ContinuationColumn;
            Span<byte> output = BeginBlockValue(scalar: true, text.Length + spaces * (_newLineLength + indent));
            int column = CurrentBlockColumn();
            int width = _options.MaxLineWidth;
            int lineStart = 0;
            int lastBreak = -1;
            for (int i = 0; i < text.Length; i++)
            {
                byte b = text[i];
                if (b == YamlConstants.Space && i > lineStart && text[i - 1] != YamlConstants.Space && i + 1 < text.Length
                    && YamlWriterHelper.CanStartPlainContinuation(text[i + 1]))
                {
                    lastBreak = i;
                }

                if ((b & 0xC0) != 0x80)
                {
                    column++;
                }

                if (column > width && lastBreak > lineStart)
                {
                    ReadOnlySpan<byte> line = text.Slice(lineStart, lastBreak - lineStart);
                    line.CopyTo(output.Slice(BytesPending));
                    BytesPending += line.Length;
                    WriteBlockLineStart(output, indent);
                    lineStart = lastBreak + 1;
                    lastBreak = -1;
                    column = indent + YamlWriterHelper.CountCharacters(text.Slice(lineStart, i + 1 - lineStart));
                }
            }

            ReadOnlySpan<byte> rest = text.Slice(lineStart);
            rest.CopyTo(output.Slice(BytesPending));
            BytesPending += rest.Length;
        }

        // A double-quoted value folded after a space that a character other than a space follows: the space stays
        // content, "\" escapes the line break, and the next line's indentation is trimmed (MaxLineWidth).
        private void WriteDoubleQuotedFolded(ReadOnlySpan<byte> text)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(YamlWriterHelper.GetMaxBlockEscapedLength(text.Length));
            int length = YamlWriterHelper.EscapeBlockDoubleQuoted(text, rented, UserEncoder);
            ReadOnlySpan<byte> escaped = rented.AsSpan(0, length);
            int spaces = 0;
            foreach (byte b in escaped)
            {
                if (b == YamlConstants.Space)
                {
                    spaces++;
                }
            }

            int indent = ContinuationColumn;
            Span<byte> output = BeginBlockValue(scalar: true, length + 2 + spaces * (_newLineLength + indent + 1));
            int column = CurrentBlockColumn() + 1;
            output[BytesPending++] = YamlConstants.Quote;
            int width = _options.MaxLineWidth;
            int lineStart = 0;
            int lastBreak = -1;
            for (int i = 0; i < escaped.Length; i++)
            {
                byte b = escaped[i];
                if (b == YamlConstants.Space && i + 1 < escaped.Length && escaped[i + 1] != YamlConstants.Space)
                {
                    lastBreak = i;
                }

                if ((b & 0xC0) != 0x80)
                {
                    column++;
                }

                if (column + 1 > width && lastBreak >= lineStart && lastBreak < i)
                {
                    ReadOnlySpan<byte> line = escaped.Slice(lineStart, lastBreak + 1 - lineStart);
                    line.CopyTo(output.Slice(BytesPending));
                    BytesPending += line.Length;
                    output[BytesPending++] = YamlConstants.BackSlash;
                    WriteBlockLineStart(output, indent);
                    lineStart = lastBreak + 1;
                    lastBreak = -1;
                    column = indent + YamlWriterHelper.CountCharacters(escaped.Slice(lineStart, i + 1 - lineStart));
                }
            }

            ReadOnlySpan<byte> rest = escaped.Slice(lineStart);
            rest.CopyTo(output.Slice(BytesPending));
            BytesPending += rest.Length;
            output[BytesPending++] = YamlConstants.Quote;
            ArrayPool<byte>.Shared.Return(rented);
        }

        // A block scalar: "|" or ">", with "-" or "+" for the chomping and the indentation indicator when the first line
        // that is not empty starts with a space, then each line at the content column. The line break after the last
        // line is the next entry's, or the root's.
        //
        // Folded (">"): a single line break between two lines that start with a character other than a space reads as a
        // space, so between two such lines one empty line more is written (it reads as the line break); around a line
        // that starts with a space, line breaks are kept as they are. With MaxLineWidth, a line that does not start
        // with a space is also folded at single spaces between two characters that are not spaces.
        // `prefixWritten`: the value's prefix (its key's ": ", or "- ") is already written (WriteStringValueBlockFromOutput),
        // so only room for the body is made. The method returns false only before it writes anything.
        private bool TryWriteBlockScalar(ReadOnlySpan<byte> text, bool folded, bool prefixWritten = false)
        {
            bool root = _enclosingContainer == EnclosingContainerType.None;
            int contentColumn = root ? _indentLength : _blockColumn + _indentLength;

            // Indentation is detected from the first line that is not empty (leading empty lines do not count), so it is
            // that line's leading space that needs the indicator.
            int firstContent = 0;
            while (text[firstContent] == (byte)'\n')
            {
                firstContent++;
            }

            int indicator = 0;
            if (text[firstContent] == YamlConstants.Space)
            {
                // The indicator is relative to the parent node's indentation, the collection's column. At the root, the
                // specification's parent indentation is -1, while libyaml and its ports (YamlDotNet, SharpYaml) count
                // from 0, so the same "|2" reads differently: a root block scalar that needs the indicator is
                // double-quoted.
                if (root || _indentLength > 9)
                {
                    return false;
                }

                indicator = _indentLength;
            }

            int trailing = 0;
            while (trailing < text.Length && text[text.Length - 1 - trailing] == (byte)'\n')
            {
                trailing++;
            }

            // Each line break can add an empty line (folded) and each space a fold (MaxLineWidth).
            int lines = 1;
            int spaces = 0;
            if (folded)
            {
                foreach (byte b in text)
                {
                    if (b == (byte)'\n')
                    {
                        lines++;
                    }
                    else if (b == YamlConstants.Space)
                    {
                        spaces++;
                    }
                }
            }
            else
            {
                lines += YamlWriterHelper.CountLineBreaks(text);
            }

            int breaks = folded ? 2 * lines + spaces : lines;
            int maxRequired = 3 + text.Length + breaks * (_newLineLength + contentColumn);
            Span<byte> output;
            if (prefixWritten)
            {
                if (_memory.Length - BytesPending < maxRequired)
                {
                    Grow(maxRequired);
                }

                output = _memory.Span;
            }
            else
            {
                output = BeginBlockValue(scalar: true, maxRequired);
            }

            output[BytesPending++] = folded ? (byte)'>' : (byte)'|';
            if (indicator != 0)
            {
                output[BytesPending++] = (byte)('0' + indicator);
            }

            if (trailing == 0)
            {
                output[BytesPending++] = (byte)'-';
            }
            else if (trailing > 1)
            {
                output[BytesPending++] = (byte)'+';
            }

            int width = folded && (_blockSpecial & SpecialLineWidth) != 0 ? _options.MaxLineWidth : 0;

            // The content without its final line break, which the next line break supplies.
            ReadOnlySpan<byte> content = trailing == 0 ? text : text.Slice(0, text.Length - 1);
            int contentEnd = text.Length - trailing;
            int offset = 0;
            bool previousIsText = false;
            while (true)
            {
                int end = content.IndexOf((byte)'\n');
                ReadOnlySpan<byte> line = end < 0 ? content : content.Slice(0, end);
                bool isContent = offset < contentEnd && !line.IsEmpty;
                if (folded && isContent)
                {
                    // A line that starts with white space (a space, or a tab) is a "spaced" line, which does not fold.
                    bool isText = line[0] is not (YamlConstants.Space or (byte)'\t');
                    if (isText && previousIsText)
                    {
                        // The empty line that the line break between two text lines needs.
                        WriteNewLine(output);
                    }

                    previousIsText = isText;
                }

                WriteNewLine(output);
                if (!line.IsEmpty)
                {
                    WriteBlockIndentation(output, contentColumn);
                    if (width != 0 && line[0] is not (YamlConstants.Space or (byte)'\t'))
                    {
                        WriteFoldedLine(output, line, contentColumn, width);
                    }
                    else
                    {
                        line.CopyTo(output.Slice(BytesPending));
                        BytesPending += line.Length;
                    }
                }

                if (end < 0)
                {
                    break;
                }

                offset += end + 1;
                content = content.Slice(end + 1);
            }

            _block!.BlockScalarEnd = BlockPosition;
            return true;
        }

        // A text line of a folded scalar, folded at single spaces between two characters that are not white space (a
        // line that starts with white space would not fold).
        private void WriteFoldedLine(Span<byte> output, ReadOnlySpan<byte> line, int contentColumn, int width)
        {
            int column = contentColumn;
            int lineStart = 0;
            int lastBreak = -1;
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == YamlConstants.Space && i > lineStart && line[i - 1] is not (YamlConstants.Space or (byte)'\t') && i + 1 < line.Length
                    && line[i + 1] is not (YamlConstants.Space or (byte)'\t'))
                {
                    lastBreak = i;
                }

                if ((b & 0xC0) != 0x80)
                {
                    column++;
                }

                if (column > width && lastBreak > lineStart)
                {
                    ReadOnlySpan<byte> part = line.Slice(lineStart, lastBreak - lineStart);
                    part.CopyTo(output.Slice(BytesPending));
                    BytesPending += part.Length;
                    WriteBlockLineStart(output, contentColumn);
                    lineStart = lastBreak + 1;
                    lastBreak = -1;
                    column = contentColumn + YamlWriterHelper.CountCharacters(line.Slice(lineStart, i + 1 - lineStart));
                }
            }

            ReadOnlySpan<byte> rest = line.Slice(lineStart);
            rest.CopyTo(output.Slice(BytesPending));
            BytesPending += rest.Length;
        }

        // Whether an explicit encoder escapes any character of the text: it then forces double quotes.
        private bool EncoderEscapes(ReadOnlySpan<byte> text) =>
            UserEncoder is not null && YamlWriterHelper.NeedsEscaping(text, UserEncoder) >= 0;

        /// <summary>
        /// A value whose type has a fixed form that never needs quotes (<c>DateOnly</c>, decision 2b), in
        /// block style; the caller checks the style.
        /// </summary>
        internal void WriteStringValueBlockPlain(ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(IsBlockStyle);
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            byte quote = TypedTextQuote();
            Span<byte> output = BeginBlockValue(scalar: true, utf8Value.Length + 2);
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            EndBlockScalar(YamlTokenType.String);
            SetFlagToAddListSeparatorBeforeNextItem();
        }

        // -----------------------------------------------------------------------------------------------------------
        // Other values: numbers, booleans, null, dates, GUIDs, Base64. The callers validate before the
        // dispatch point; the bodies check the hint. Each has a form after a property name.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteLiteralValueBlock(ReadOnlySpan<byte> literal)
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, literal.Length);
            literal.CopyTo(output.Slice(BytesPending));
            BytesPending += literal.Length;
            EndBlockScalar(_tokenType);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteLiteralBlock(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> literal, YamlWriterHelper.BlockScan keyScan)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false, keyScan);
            WriteLiteralValueBlock(literal);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteLiteralBlock(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> literal)
        {
            WriteBlockKey(escapedPropertyName, consumeHint: false);
            WriteLiteralValueBlock(literal);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(long value)
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatInt64Length);
            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            EndBlockScalar(YamlTokenType.Number);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(ulong value)
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatUInt64Length);
            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            EndBlockScalar(YamlTokenType.Number);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(decimal value)
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatDecimalLength);
            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            EndBlockScalar(YamlTokenType.Number);
        }

        // Decision 8: NaN and the infinities are YAML's plain floats in block style.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(double value)
        {
            if (!double.IsFinite(value))
            {
                WriteNumberValueBlock(double.IsNaN(value) ? ".nan"u8 : value > 0 ? ".inf"u8 : "-.inf"u8);
                return;
            }

            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatDoubleLength);
            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            EndBlockScalar(YamlTokenType.Number);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(float value)
        {
            if (!float.IsFinite(value))
            {
                WriteNumberValueBlock(float.IsNaN(value) ? ".nan"u8 : value > 0 ? ".inf"u8 : "-.inf"u8);
                return;
            }

            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatSingleLength);
            bool result = TryFormatSingle(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            EndBlockScalar(YamlTokenType.Number);
        }

        /// <summary>
        /// A document's number written with its source text (<c>0x1F</c>, <c>012</c>, <c>1_000</c>), in block style: the
        /// document has checked that the text reads to its number under <see cref="YamlWriterOptions.ReadSchema"/>.
        /// </summary>
        internal void WriteYamlNumberText(ReadOnlySpan<byte> text)
        {
            Debug.Assert(_style == WriterStyleBlock && text.Length > 0);
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            WriteNumberValueBlock(text);
            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        internal bool WritesYamlText => _style == WriterStyleBlock;

        // A formatted number (the raw-number path, and the names of 5.7's non-finite floats): copied as it is.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberValueBlock(ReadOnlySpan<byte> utf8FormattedNumber)
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, utf8FormattedNumber.Length);
            utf8FormattedNumber.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8FormattedNumber.Length;
            EndBlockScalar(YamlTokenType.Number);
        }

        // Decision 2b (i): typed dates are written plain, in the JSON-compatible style's ISO 8601 form.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlock(DateTime value)
        {
            byte quote = TypedTextQuote();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatDateTimeOffsetLength + 2);
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            EndBlockScalar(YamlTokenType.String);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlock(DateTimeOffset value)
        {
            byte quote = TypedTextQuote();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatDateTimeOffsetLength + 2);
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            YamlWriterHelper.WriteDateTimeOffsetTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            EndBlockScalar(YamlTokenType.String);
        }

        // A GUID in the 'D' format never needs quotes.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringValueBlock(Guid value)
        {
            byte quote = TypedTextQuote();
            Span<byte> output = BeginBlockValue(scalar: true, YamlConstants.MaximumFormatGuidLength + 2);
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
            if (quote != 0)
            {
                output[BytesPending++] = quote;
            }

            EndBlockScalar(YamlTokenType.String);
        }

        // Base64 is always quoted (1111 is valid Base64 and an integer): double-quoted, or single-quoted
        // under a single-quoted DefaultScalarStyle (M7b, fix A).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBase64Block(ReadOnlySpan<byte> bytes)
        {
            byte quote = TypedTextQuote() is (byte)'\'' ? (byte)'\'' : YamlConstants.Quote;
            if (bytes.Length > int.MaxValue / 4 * 3)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(bytes.Length);
            }

            int encodingLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
            Span<byte> output = BeginBlockValue(scalar: true, encodingLength + 2);
            output[BytesPending++] = quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = quote;
            EndBlockScalar(YamlTokenType.String);
        }

        // The forms after a property name. The key never takes the hint: in these forms it is the value's.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, long value, YamlWriterHelper.BlockScan keyScan) { WriteBlockKey(escapedPropertyName, consumeHint: false, keyScan); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, long value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, ulong value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, ulong value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, decimal value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, decimal value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, double value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, double value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, float value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, float value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> utf8FormattedNumber) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(utf8FormattedNumber); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteNumberBlock(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> utf8FormattedNumber) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteNumberValueBlock(utf8FormattedNumber); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<byte> escapedPropertyName, DateTime value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<char> escapedPropertyName, DateTime value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<byte> escapedPropertyName, DateTimeOffset value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<char> escapedPropertyName, DateTimeOffset value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<byte> escapedPropertyName, Guid value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBlock(ReadOnlySpan<char> escapedPropertyName, Guid value) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteStringValueBlock(value); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBase64Block(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteBase64Block(bytes); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBase64Block(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes) { WriteBlockKey(escapedPropertyName, consumeHint: false); WriteBase64Block(bytes); }

        // -----------------------------------------------------------------------------------------------------------
        // Comments, string segments

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteCommentBlock(ReadOnlySpan<char> comment)
        {
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(comment, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteCommentBlock(utf8, endOfLine: false);
            ReturnRented(rented);
        }

        // A string written in segments is double-quoted, escaped as the JSON-compatible style escapes it
        // (a valid YAML double-quoted scalar).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringSegmentBlockPrologue()
        {
            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, 1);
            output[BytesPending++] = YamlConstants.Quote;
        }

        // The line break that completes a root string written in segments.
        private void WriteStringSegmentBlockEpilogue()
        {
            if (_bitStack.CurrentDepth == 0)
            {
                if (_memory.Length - BytesPending < _newLineLength)
                {
                    Grow(_newLineLength);
                }

                WriteNewLine(_memory.Span);
            }
        }
    }
}
