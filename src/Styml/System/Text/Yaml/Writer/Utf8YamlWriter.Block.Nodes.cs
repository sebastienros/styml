// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    // M4b: node properties, aliases, explicit keys,
    // document ends, directives, comments and flow collections in block style.
    //
    // The special states. M4a's block paths test one int, _blockSpecial, once per node: while it is 0 they run as they
    // did; any bit sends the node through the paths here, which handle every state. The bits are set by the members of
    // this file and cleared by the node they wait for, so a document that uses none of them never leaves the M4a paths.
    public sealed partial class Utf8YamlWriter
    {
        private const int SpecialAnchor = 1;              // an anchor is written, pending its node
        private const int SpecialTag = 2;                 // a tag is written, pending its node
        private const int SpecialProperties = SpecialAnchor | SpecialTag;
        private const int SpecialNoCompact = 4;           // the collection just started has properties: its first child goes on a new line
        private const int SpecialCommentSinceStart = 8;   // a comment line follows the start of the current collection
        private const int SpecialValueOnNewLine = 16;     // a comment line follows "key:", node properties or "?": the node starts on a new line
        private const int SpecialFlow = 32;               // inside a flow collection
        private const int SpecialExplicitKey = 64;        // an explicit key's node is open (WriteStartPropertyKey)
        private const int SpecialDirectives = 128;        // directives are written: WriteDocumentStart comes next
        private const int SpecialCollectionHint = 256;    // WriteCollectionStyle is pending
        private const int SpecialLineWidth = 512;         // MaxLineWidth is set: every scalar is measured
        private const int SpecialFlowFreshLine = 1024;    // a comment in a flow collection ended the line (and wrote the separator)
        private const int SpecialMultiLineStyle = 2048;   // MultiLineStringStyle is literal or folded: every string is judged from its text
        private const int SpecialAfterDocumentEnd = 4096; // "..." is written: a root node needs "---" first (libyaml's readers want it)
        private const int SpecialDefaultStyle = 8192;     // DefaultScalarStyle is set: every string value is judged from its text (M7b)
        private const int SpecialJsonSchema = 16384;      // the serializer's Schema is Json: no string value is plain (M7c)
        private const int SpecialPermanent = SpecialLineWidth | SpecialMultiLineStyle | SpecialDefaultStyle | SpecialJsonSchema;

        private int _blockSpecial;

        // The rest of M4b's block state, in an object of its own that the writer allocates once, when its options are
        // block style (and keeps across Reset), so that the JSON-compatible style's writer object does not grow: G1's
        // `Write` indented group read 1.0125 with these fields in the writer.
        private BlockState? _block;

        private sealed class BlockState
        {
            public YamlCollectionStyle CollectionStyleHint;

            // The depth of the outermost flow collection, while one is open.
            public int FlowDepth;

            // The depths of the open explicit keys' levels (WriteStartPropertyKey pushes a level of its own).
            public int[]? ExplicitKeyDepths;
            public int ExplicitKeyCount;

            // Where an end-of-line comment cannot go (after a block scalar, whose last line is content) and where it
            // can (after a collection end: an empty block collection, or a flow collection).
            public long BlockScalarEnd = -1;
            public long EndOfLineCommentAfterEnd = -1;

            // WriteDocumentStart wrote "---" and no root value followed yet; a %YAML directive is written for the next
            // document.
            public bool AfterDocumentStartMarker;
            public bool YamlDirectivePending;

            // WriteDocumentStart(rootPropertiesOnMarkerLine: true): the root's first property goes on the "---" line
            // (PropertiesOnMarkerLine), and once it did (OnMarkerLine), Unity's stripped marker may follow.
            public bool PropertiesOnMarkerLine;
            public bool OnMarkerLine;

            // The %TAG handles WriteDirective declared for the current document (YAML 1.2's scope, or YAML 1.1's under
            // InheritTagDirectives), for the DOM's tag shorthand.
            public YamlTagDirectives? TagDirectives;

            // The writer wrote %YAML 1.1 (or 1.0): later documents keep the table, as a reader reads such a stream.
            public bool Yaml11Scope;
        }

        private enum BlockNode : byte
        {
            Scalar,          // a scalar value, or an alias
            BlockCollection, // a block collection's start
            FlowCollection,  // a flow collection's opening bracket
        }

        private long BlockPosition => BytesCommitted + BytesPending;

        private bool InFlow => (_blockSpecial & SpecialFlow) != 0;

        private bool PropertiesPending => (_blockSpecial & SpecialProperties) != 0;

        // The current level is an explicit key's (its one node is written into it).
        private bool InExplicitKeyNode => _block!.ExplicitKeyCount != 0 && _block!.ExplicitKeyDepths![_block!.ExplicitKeyCount - 1] == _bitStack.CurrentDepth;

        // The column of a node written on a line of its own after "key:", node properties or "?".
        private int ValueColumn => _enclosingContainer == EnclosingContainerType.None ? _indentLength : _blockColumn + _indentLength;

        private void ResetBlockState()
        {
            _blockSpecial = 0;
            if (_style != WriterStyleBlock)
            {
                return;
            }

            if (_options.MaxLineWidth > 0)
            {
                _blockSpecial |= SpecialLineWidth;
            }

            if (_options.MultiLineStringStyle is YamlScalarStyle.Literal or YamlScalarStyle.Folded)
            {
                _blockSpecial |= SpecialMultiLineStyle;
            }

            if (_options.DefaultScalarStyle != YamlScalarStyle.None)
            {
                _blockSpecial |= SpecialDefaultStyle;
            }

            if (_options.QuoteStringsForJsonSchema)
            {
                _blockSpecial |= SpecialJsonSchema;
            }

            BlockState block = _block ??= new BlockState();
            block.CollectionStyleHint = YamlCollectionStyle.Block;
            block.FlowDepth = 0;
            block.ExplicitKeyCount = 0;
            block.BlockScalarEnd = -1;
            block.EndOfLineCommentAfterEnd = -1;
            block.AfterDocumentStartMarker = false;
            block.YamlDirectivePending = false;
        }

        private void ThrowIfJsonCompatible(string member)
        {
            if (_style != WriterStyleBlock)
            {
                ThrowHelper.ThrowInvalidOperationException_NotSupportedInJsonCompatibleStyle(member);
            }
        }

        // An argument given as UTF-16, as UTF-8 in `stackBuffer` or a rented array. Invalid UTF-16 throws.
        private static ReadOnlySpan<byte> TranscodeArgument(ReadOnlySpan<char> value, ref byte[]? rented, Span<byte> stackBuffer)
        {
            int maxLength = value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding;
            Span<byte> destination = maxLength <= stackBuffer.Length ? stackBuffer : (rented = ArrayPool<byte>.Shared.Rent(maxLength));
            if (YamlWriterHelper.ToUtf8(value, destination, out int written) != OperationStatus.Done)
            {
                ReturnRented(rented);
                rented = null;
                ThrowHelper.ThrowArgumentException(SR.CannotTranscodeInvalidUtf16);
            }

            return destination.Slice(0, written);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Node properties and aliases

        /// <summary>
        /// Writes an anchor (<c>&amp;name</c>) for the next node: a value, a collection start, or a property name (the
        /// key node).
        /// </summary>
        /// <param name="anchorName">The anchor's name, without <c>&amp;</c>.</param>
        /// <remarks>
        /// <para>
        /// The anchor is written at once, before the node. Before a property name, in any of the forms that write one
        /// (<see cref="WritePropertyName(string)"/>, <c>WriteString(name, value)</c>, <c>WriteStartObject(name)</c>),
        /// it belongs to the key; to anchor the value of such a pair, write the name first.
        /// </para>
        /// <para>Supported in block style only. Whether the name is defined before an alias uses it is not checked.</para>
        /// <para>
        /// YAML allows any non-space character other than the flow indicators in a name, but some readers reject names
        /// with characters other than ASCII letters, digits, <c>-</c> and <c>_</c> (SharpYaml, VYaml, and YamlDotNet
        /// inside a flow collection); keep to those for output such readers read.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">The name is not a valid anchor name.</exception>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, no node may come next, or the next
        /// node already has an anchor.
        /// </exception>
        public void WriteAnchor(string anchorName)
        {
            ArgumentNullException.ThrowIfNull(anchorName);
            WriteAnchor(anchorName.AsSpan());
        }

        /// <inheritdoc cref="WriteAnchor(string)"/>
        public void WriteAnchor(ReadOnlySpan<char> anchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAnchor));
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(anchorName, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteAnchor(utf8);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteAnchor(string)"/>
        public void WriteAnchor(ReadOnlySpan<byte> utf8AnchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAnchor));
            if (!YamlWriterHelper.IsValidAnchorName(utf8AnchorName))
            {
                ThrowHelper.ThrowArgumentException_InvalidAnchorName();
            }

            WriteNodeProperty(utf8AnchorName, SpecialAnchor);
        }

        /// <summary>
        /// Writes a tag for the next node: a value, a collection start, or a property name (the key node), as
        /// <see cref="WriteAnchor(string)"/> does.
        /// </summary>
        /// <param name="tag">
        /// The tag as written: <c>!</c>, <c>!foo</c>, <c>!!str</c>, <c>!e!foo</c> (after a <c>%TAG</c> directive for
        /// <c>!e!</c>) or <c>!&lt;tag:example.com,2000:foo&gt;</c>. It is checked, not expanded.
        /// </param>
        /// <remarks>Supported in block style only.</remarks>
        /// <exception cref="ArgumentException">The tag is not valid tag syntax.</exception>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, no node may come next, or the next
        /// node already has a tag.
        /// </exception>
        public void WriteTag(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);
            WriteTag(tag.AsSpan());
        }

        /// <inheritdoc cref="WriteTag(string)"/>
        public void WriteTag(ReadOnlySpan<char> tag)
        {
            ThrowIfJsonCompatible(nameof(WriteTag));
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(tag, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteTag(utf8);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteTag(string)"/>
        public void WriteTag(ReadOnlySpan<byte> utf8Tag)
        {
            ThrowIfJsonCompatible(nameof(WriteTag));
            if (!YamlWriterHelper.IsValidTag(utf8Tag))
            {
                ThrowHelper.ThrowArgumentException_InvalidTag();
            }

            WriteNodeProperty(utf8Tag, SpecialTag);
        }

        /// <summary>Writes an alias (<c>*name</c>) as a value.</summary>
        /// <param name="anchorName">The anchor's name, without <c>*</c>.</param>
        /// <remarks>
        /// Supported in block style only. Whether an earlier node defined the anchor is not checked; a reader reports an
        /// undefined alias.
        /// </remarks>
        /// <exception cref="ArgumentException">The name is not a valid anchor name.</exception>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, a value cannot come next, or an
        /// anchor or a tag is pending (an alias has none).
        /// </exception>
        public void WriteAliasValue(string anchorName)
        {
            ArgumentNullException.ThrowIfNull(anchorName);
            WriteAliasValue(anchorName.AsSpan());
        }

        /// <inheritdoc cref="WriteAliasValue(string)"/>
        public void WriteAliasValue(ReadOnlySpan<char> anchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAliasValue));
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(anchorName, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteAliasValue(utf8);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteAliasValue(string)"/>
        public void WriteAliasValue(ReadOnlySpan<byte> utf8AnchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAliasValue));
            if (!YamlWriterHelper.IsValidAnchorName(utf8AnchorName))
            {
                ThrowHelper.ThrowArgumentException_InvalidAnchorName();
            }

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
                if (PropertiesPending)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodePropertiesNotApplicable();
                }
            }

            CheckNoScalarStyleHint();
            Span<byte> output = BeginBlockValue(scalar: true, utf8AnchorName.Length + 1);
            output[BytesPending++] = (byte)'*';
            utf8AnchorName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8AnchorName.Length;
            EndBlockScalar(YamlTokenType.Alias);
            SetFlagToAddListSeparatorBeforeNextItem();
        }

        /// <summary>Writes an alias (<c>*name :</c>) as a mapping key.</summary>
        /// <inheritdoc cref="WriteAliasValue(string)"/>
        public void WriteAliasPropertyName(string anchorName)
        {
            ArgumentNullException.ThrowIfNull(anchorName);
            WriteAliasPropertyName(anchorName.AsSpan());
        }

        /// <inheritdoc cref="WriteAliasPropertyName(string)"/>
        public void WriteAliasPropertyName(ReadOnlySpan<char> anchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAliasPropertyName));
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(anchorName, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteAliasPropertyName(utf8);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteAliasPropertyName(string)"/>
        public void WriteAliasPropertyName(ReadOnlySpan<byte> utf8AnchorName)
        {
            ThrowIfJsonCompatible(nameof(WriteAliasPropertyName));
            if (!YamlWriterHelper.IsValidAnchorName(utf8AnchorName))
            {
                ThrowHelper.ThrowArgumentException_InvalidAnchorName();
            }

            ValidateWritingProperty();
            if (!_options.SkipValidation && PropertiesPending)
            {
                ThrowHelper.ThrowInvalidOperationException_NodePropertiesNotApplicable();
            }

            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();

            // An anchor name may hold ':', so a space separates it from the indicator.
            Span<byte> output = BeginBlockKey(utf8AnchorName.Length + 3, explicitKey: false);
            output[BytesPending++] = (byte)'*';
            utf8AnchorName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8AnchorName.Length;
            output[BytesPending++] = YamlConstants.Space;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _tokenType = YamlTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        // Writes an anchor ('&' and the name) or a tag, after what comes before a node when it is the node's first
        // property, after a space otherwise.
        private void WriteNodeProperty(ReadOnlySpan<byte> text, int bit)
        {
            if (!_options.SkipValidation)
            {
                if ((_blockSpecial & bit) != 0)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodePropertyPending();
                }

                ValidateNodePropertyPosition();
            }

            if ((_blockSpecial & SpecialAfterDocumentEnd) != 0)
            {
                WriteDocumentStartAfterEnd();
            }

            int required = text.Length + 2 + BlockPrefixLength + ValueColumn + 2 * _newLineLength + FlowBreakRoom;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (!PropertiesPending)
            {
                if (_block is { PropertiesOnMarkerLine: true } && TryReopenMarkerLine(output))
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
                else
                {
                    WriteNodePrefix(output);
                }
            }
            else
            {
                output[BytesPending++] = YamlConstants.Space;
            }

            if (bit == SpecialAnchor)
            {
                output[BytesPending++] = (byte)'&';
            }

            text.CopyTo(output.Slice(BytesPending));
            BytesPending += text.Length;
            _blockSpecial |= bit;
        }

        // The root's first property on the "---" line: the marker's line break is taken back while it is the last thing
        // in the buffer (not yet flushed, nothing written after it); otherwise the property goes on the next line.
        private bool TryReopenMarkerLine(Span<byte> output)
        {
            _block!.PropertiesOnMarkerLine = false;
            int markerEnd = BytesPending - _newLineLength;
            if (_tokenType != YamlTokenType.None || _currentDepth != 0 || markerEnd < 3
                || output[markerEnd - 1] != (byte)'-' || output[markerEnd - 2] != (byte)'-' || output[markerEnd - 3] != (byte)'-'
                || (markerEnd > 3 && output[markerEnd - 4] != (byte)'\n') || output[BytesPending - 1] != (byte)'\n')
            {
                return false;
            }

            BytesPending = markerEnd;
            _block.OnMarkerLine = true;
            return true;
        }

        /// <summary>
        /// Writes Unity's <c>stripped</c> marker after the document root's properties on its <c>---</c> line
        /// (<c>--- !u!4 &amp;400000 stripped</c>), for a placeholder of a source prefab's object. Call it after
        /// <see cref="WriteDocumentStart(bool)"/> with <c>rootPropertiesOnMarkerLine</c> and the root's
        /// <see cref="WriteTag(string)"/> (and <see cref="WriteAnchor(string)"/>), before the root node.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, no root tag is pending on a document's
        /// <c>---</c> line.
        /// </exception>
        public void WriteUnityStrippedMarker()
        {
            ThrowIfJsonCompatible(nameof(WriteUnityStrippedMarker));
            if (!_options.SkipValidation
                && (_block is not { OnMarkerLine: true } || (_blockSpecial & SpecialTag) == 0 || _tokenType != YamlTokenType.None || _currentDepth != 0))
            {
                ThrowHelper.ThrowInvalidOperationException_UnityStrippedMarkerPosition();
            }

            int required = 9;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            output[BytesPending++] = YamlConstants.Space;
            "stripped"u8.CopyTo(output.Slice(BytesPending));
            BytesPending += 8;
            _block!.OnMarkerLine = false;
        }

        // The DOM: a root read with its properties on its '---' line writes them there when the marker was just written.
        internal void KeepRootPropertiesOnMarkerLine()
        {
            if (_block is { AfterDocumentStartMarker: true } && _tokenType == YamlTokenType.None && _currentDepth == 0 && !PropertiesPending)
            {
                _block.PropertiesOnMarkerLine = true;
            }
        }

        // The DOM: the stripped marker where it can go (after the root's tag on its '---' line); nothing elsewhere.
        internal void TryWriteUnityStrippedMarker()
        {
            if (_block is { OnMarkerLine: true } && (_blockSpecial & SpecialTag) != 0 && _tokenType == YamlTokenType.None && _currentDepth == 0)
            {
                WriteUnityStrippedMarker();
            }
        }

        private void ValidateNodePropertyPosition()
        {
            if (IsWritingPartialString)
            {
                ThrowInvalidOperationException(ExceptionResource.CannotWriteWithinString);
            }

            if ((_blockSpecial & SpecialDirectives) != 0)
            {
                ThrowHelper.ThrowInvalidOperationException_DocumentStartExpected();
            }

            // In a mapping a property may precede a key as well as a value; elsewhere, only where a value may come.
            if (_enclosingContainer != EnclosingContainerType.Object && !CanWriteValue)
            {
                OnValidateWritingValueFailed();
            }

            if (InExplicitKeyNode && _tokenType != YamlTokenType.StartArray)
            {
                ThrowHelper.ThrowInvalidOperationException_ExplicitKeyOneNode();
            }
        }

        // What comes before a node or a key that has no property written yet: the separator of a flow collection, the
        // space after "key:", "?" or "-", the entry's start before a key, or the line of a node after a comment.
        private void WriteNodePrefix(Span<byte> output)
        {
            if ((_blockSpecial & SpecialValueOnNewLine) != 0)
            {
                _blockSpecial &= ~SpecialValueOnNewLine;
                WriteBlockLineStart(output, ValueColumn);
                return;
            }

            if (InFlow)
            {
                WriteFlowItemPrefix(output);
                return;
            }

            if (InExplicitKeyNode)
            {
                output[BytesPending++] = YamlConstants.Space;
                return;
            }

            switch (_enclosingContainer)
            {
                case EnclosingContainerType.Object:
                    if (_tokenType == YamlTokenType.PropertyName)
                    {
                        output[BytesPending++] = YamlConstants.Space;
                    }
                    else
                    {
                        WriteBlockEntryStartSpecial(output);
                    }

                    break;
                case EnclosingContainerType.Array:
                    WriteBlockEntryStartSpecial(output);
                    output[BytesPending++] = (byte)'-';
                    output[BytesPending++] = YamlConstants.Space;
                    break;
            }
        }

        private void WriteBlockIndentation(Span<byte> output, int column)
        {
            output.Slice(BytesPending, column).Fill(YamlConstants.Space);
            BytesPending += column;
        }

        // An entry's start (a key, or a sequence item's dash) when special states may apply: a first child that cannot
        // be compact (the collection has properties, or a comment line follows its start) goes on a new line.
        private void WriteBlockEntryStartSpecial(Span<byte> output)
        {
            if (_tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray
                && (_blockSpecial & (SpecialNoCompact | SpecialCommentSinceStart)) != 0)
            {
                _blockSpecial &= ~(SpecialNoCompact | SpecialCommentSinceStart);
                WriteBlockLineStart(output, _blockColumn);
                return;
            }

            WriteBlockEntryStart(output);
        }

        // Before an item of a flow sequence, a key or a value of a flow mapping, or an explicit key's node.
        private void WriteFlowItemPrefix(Span<byte> output)
        {
            bool freshLine = (_blockSpecial & SpecialFlowFreshLine) != 0;
            _blockSpecial &= ~SpecialFlowFreshLine;
            if ((_enclosingContainer == EnclosingContainerType.Object && _tokenType == YamlTokenType.PropertyName) || InExplicitKeyNode)
            {
                if (!freshLine)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                return;
            }

            if (_tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray || freshLine)
            {
                if (!freshLine)
                {
                    TryWriteFlowBreak(output);
                }

                return;
            }

            output[BytesPending++] = YamlConstants.ListSeparator;
            if (!TryWriteFlowBreak(output))
            {
                output[BytesPending++] = YamlConstants.Space;
            }
        }

        // M7c: with MaxLineWidth, a flow collection's item or key starts a new line once the
        // line is past the width, as libyaml's emitter (YamlDotNet's and SharpYaml's) breaks it: after the comma, or
        // after the opening bracket, never inside a key or between a key and its value. The continuation line is
        // indented past the enclosing block level by one indent per open flow collection, so it stays inside the flow
        // collection for every reader; a flow collection that is an implicit key is never written (the writer makes
        // collection keys explicit), so no key spans lines.
        private bool TryWriteFlowBreak(Span<byte> output)
        {
            if ((_blockSpecial & SpecialLineWidth) == 0 || CurrentBlockColumn() <= _options.MaxLineWidth)
            {
                return false;
            }

            WriteNewLine(output);
            WriteBlockIndentation(output, FlowContinuationColumn);
            return true;
        }

        // The column of a flow collection's continuation lines, and the room a break before an item needs.
        private int FlowContinuationColumn => _blockColumn + _indentLength * (_bitStack.CurrentDepth - _block!.FlowDepth + 1);

        private int FlowBreakRoom => (_blockSpecial & (SpecialFlow | SpecialLineWidth)) == (SpecialFlow | SpecialLineWidth)
            ? _newLineLength + FlowContinuationColumn
            : 0;

        // The checks every node makes while a special state is set.
        private void CheckSpecialNode(BlockNode node)
        {
            if ((_blockSpecial & SpecialAfterDocumentEnd) != 0)
            {
                WriteDocumentStartAfterEnd();
            }

            if (node != BlockNode.BlockCollection && node != BlockNode.FlowCollection && (_blockSpecial & SpecialCollectionHint) != 0)
            {
                OnCollectionStyleHintWithoutCollection();
            }

            if ((_blockSpecial & SpecialDirectives) != 0)
            {
                _blockSpecial &= ~SpecialDirectives;
                if (!_options.SkipValidation)
                {
                    ThrowHelper.ThrowInvalidOperationException_DocumentStartExpected();
                }
            }

            if (!_options.SkipValidation && InExplicitKeyNode && _tokenType != YamlTokenType.StartArray)
            {
                ThrowHelper.ThrowInvalidOperationException_ExplicitKeyOneNode();
            }
        }

        // BeginBlockValue while a special state is set: what comes before a value node.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Span<byte> BeginBlockValueSpecial(BlockNode node, int extra)
        {
            CheckSpecialNode(node);
            int required = BlockPrefixLength + extra + 2 * _newLineLength + ValueColumn + 2 + FlowBreakRoom;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (PropertiesPending)
            {
                _blockSpecial &= ~SpecialProperties;
                if ((_blockSpecial & SpecialValueOnNewLine) != 0)
                {
                    // Properties, then a comment line: a scalar or a flow collection goes on a line of its own, and a block
                    // collection's first child does.
                    _blockSpecial &= ~SpecialValueOnNewLine;
                    if (node == BlockNode.BlockCollection)
                    {
                        _blockSpecial |= SpecialCommentSinceStart;
                    }
                    else
                    {
                        WriteBlockLineStart(output, ValueColumn);
                    }
                }
                else if (node == BlockNode.BlockCollection)
                {
                    // "- &a" or "key: &a": the collection's first child goes on the next line.
                    _blockSpecial |= SpecialNoCompact;
                }
                else
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                return output;
            }

            if ((_blockSpecial & SpecialValueOnNewLine) != 0)
            {
                _blockSpecial &= ~SpecialValueOnNewLine;
                if (node == BlockNode.BlockCollection)
                {
                    _blockSpecial |= SpecialCommentSinceStart;
                }
                else
                {
                    WriteBlockLineStart(output, ValueColumn);
                }

                return output;
            }

            if (InFlow)
            {
                WriteFlowItemPrefix(output);
                return output;
            }

            if (InExplicitKeyNode)
            {
                // After "?": a block collection's first child is compact ("? a: 1"), anything else follows a space.
                if (node != BlockNode.BlockCollection)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                return output;
            }

            if (_enclosingContainer == EnclosingContainerType.Array)
            {
                WriteBlockEntryStartSpecial(output);
                output[BytesPending++] = (byte)'-';
                if (node != BlockNode.BlockCollection)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
            }
            else if (_enclosingContainer == EnclosingContainerType.Object)
            {
                if (node != BlockNode.BlockCollection)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }
            }

            return output;
        }

        // BeginBlockKey while a special state is set: what comes before a key.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Span<byte> BeginBlockKeySpecial(int keyLength, bool explicitKey)
        {
            if ((_blockSpecial & SpecialDirectives) != 0)
            {
                _blockSpecial &= ~SpecialDirectives;
                if (!_options.SkipValidation)
                {
                    ThrowHelper.ThrowInvalidOperationException_DocumentStartExpected();
                }
            }

            int required = 2 * BlockPrefixLength + keyLength + 6 + FlowBreakRoom;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (PropertiesPending)
            {
                // The key's properties are written, with its entry's start before them.
                _blockSpecial &= ~SpecialProperties;
                output[BytesPending++] = YamlConstants.Space;
            }
            else if (InFlow)
            {
                WriteFlowItemPrefix(output);
            }
            else
            {
                WriteBlockEntryStartSpecial(output);
            }

            if (explicitKey)
            {
                output[BytesPending++] = (byte)'?';
                output[BytesPending++] = YamlConstants.Space;
            }

            return output;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Collections

        /// <summary>
        /// Requests a style for the next collection started (<see cref="WriteStartArray()"/>,
        /// <see cref="WriteStartObject()"/> and their forms with a property name): <see cref="YamlCollectionStyle.Flow"/>
        /// writes it on one line, <c>[a, b]</c> or <c>{key: value}</c>, with everything inside it.
        /// </summary>
        /// <remarks>
        /// Inside a flow collection every collection is flow, whatever is requested. Supported in block style only.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="style"/> is not a collection style.</exception>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, a style is already pending. A style
        /// followed by anything but a collection start also throws, at that call.
        /// </exception>
        public void WriteCollectionStyle(YamlCollectionStyle style)
        {
            if (style is not (YamlCollectionStyle.Block or YamlCollectionStyle.Flow))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_CollectionStyle(nameof(style));
            }

            ThrowIfJsonCompatible(nameof(WriteCollectionStyle));
            if ((_blockSpecial & SpecialCollectionHint) != 0 && !_options.SkipValidation)
            {
                ThrowHelper.ThrowInvalidOperationException_CollectionStyleHintPending();
            }

            _block!.CollectionStyleHint = style;
            _blockSpecial |= SpecialCollectionHint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckNoCollectionStyleHint()
        {
            if ((_blockSpecial & SpecialCollectionHint) != 0)
            {
                OnCollectionStyleHintWithoutCollection();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnCollectionStyleHintWithoutCollection()
        {
            _blockSpecial &= ~SpecialCollectionHint;
            if (!_options.SkipValidation)
            {
                ThrowHelper.ThrowInvalidOperationException_CollectionStyleHintNotApplicable();
            }
        }

        // A collection start while a special state is set, after the validation and the scalar style check; the caller
        // increments the depth.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StartBlockCollectionSpecial(byte token)
        {
            bool flow = InFlow;
            if ((_blockSpecial & SpecialCollectionHint) != 0)
            {
                flow |= _block!.CollectionStyleHint == YamlCollectionStyle.Flow;
                _blockSpecial &= ~SpecialCollectionHint;
            }

            EnclosingContainerType parent = _enclosingContainer;
            if (flow)
            {
                Span<byte> output = BeginBlockValueSpecial(BlockNode.FlowCollection, 1);
                output[BytesPending++] = token;
                UpdateBitStackOnStart(token);
                if (!InFlow)
                {
                    _block!.FlowDepth = _bitStack.CurrentDepth;
                    _blockSpecial |= SpecialFlow;
                }

                return;
            }

            BeginBlockValueSpecial(BlockNode.BlockCollection, 0);
            UpdateBitStackOnStart(token);
            _blockColumn += BlockDelta(token == YamlConstants.OpenBracket, parent);
        }

        // A collection end while a special state is set; the caller decrements the depth and sets the token type.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteEndBlockSpecial(byte token)
        {
            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();
            if (PropertiesPending)
            {
                _blockSpecial &= ~SpecialProperties;
                if (!_options.SkipValidation)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodePropertiesWithoutNode();
                }
            }

            if (InExplicitKeyNode)
            {
                ThrowHelper.ThrowInvalidOperationException_ExplicitKeyNotClosed();
            }

            bool empty = _tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray;
            bool flow = InFlow;
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
            int required = 3 + 2 * _newLineLength + _blockColumn + _indentLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (flow)
            {
                _blockSpecial &= ~SpecialFlowFreshLine;
                output[BytesPending++] = token;
                if (_bitStack.CurrentDepth < _block!.FlowDepth)
                {
                    _blockSpecial &= ~SpecialFlow;
                    _block!.FlowDepth = 0;
                    _block!.EndOfLineCommentAfterEnd = BlockPosition;
                }

                if (root)
                {
                    WriteNewLine(output);
                }

                return;
            }

            int delta = BlockDelta(token == YamlConstants.CloseBracket, _enclosingContainer);
            if (empty)
            {
                if ((_blockSpecial & SpecialCommentSinceStart) != 0)
                {
                    // A comment line follows the start: "{}" on a line of its own, at the child column.
                    WriteBlockLineStart(output, root ? 0 : _blockColumn - delta + _indentLength);
                }
                else if (!root || (_blockSpecial & SpecialNoCompact) != 0)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                output[BytesPending++] = token == YamlConstants.CloseBracket ? YamlConstants.OpenBracket : YamlConstants.OpenBrace;
                output[BytesPending++] = token;
                _block!.EndOfLineCommentAfterEnd = BlockPosition;
            }

            _blockSpecial &= ~(SpecialNoCompact | SpecialCommentSinceStart);
            _blockColumn -= delta;
            if (root)
            {
                WriteNewLine(output);
                _blockColumn = 0;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Explicit keys

        /// <summary>
        /// Starts an explicit key (<c>? </c>): exactly one node follows, a scalar or a collection, and becomes the key of
        /// the next entry; <see cref="WriteEndPropertyKey"/> ends it, and the entry's value follows as after a property
        /// name.
        /// </summary>
        /// <remarks>
        /// The key's node may have an anchor or a tag, written after this call. Its level counts toward
        /// <see cref="YamlWriterOptions.MaxDepth"/>. Supported in block style only.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style, or the depth would exceed the maximum; or, while validation is
        /// enabled, a property name cannot come next, or an anchor or a tag is pending.
        /// </exception>
        public void WriteStartPropertyKey()
        {
            ThrowIfJsonCompatible(nameof(WriteStartPropertyKey));
            ValidateWritingProperty();
            if (!_options.SkipValidation && PropertiesPending)
            {
                ThrowHelper.ThrowInvalidOperationException_NodePropertiesNotApplicable();
            }

            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();
            _defaultStyleKey = false; // M7b (fix A): a collection key is not styled.
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            int required = BlockPrefixLength + 4 + FlowBreakRoom;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (InFlow)
            {
                WriteFlowItemPrefix(output);
            }
            else
            {
                WriteBlockEntryStartSpecial(output);
            }

            output[BytesPending++] = (byte)'?';

            // The key's node is written into a level of its own, of a sequence's kind, at the mapping's column: "?" plays
            // the part of a sequence item's dash.
            UpdateBitStackOnStart(YamlConstants.OpenBracket);
            _block!.ExplicitKeyDepths ??= new int[4];
            if (_block!.ExplicitKeyCount == _block!.ExplicitKeyDepths.Length)
            {
                Array.Resize(ref _block!.ExplicitKeyDepths, _block!.ExplicitKeyCount * 2);
            }

            _block!.ExplicitKeyDepths[_block!.ExplicitKeyCount++] = _bitStack.CurrentDepth;
            _blockSpecial |= SpecialExplicitKey;
            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = YamlTokenType.StartArray;
        }

        /// <summary>
        /// Ends the explicit key that <see cref="WriteStartPropertyKey"/> started, and writes <c>:</c> on the next line;
        /// the entry's value follows.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style, or no explicit key is open at this level; or, while validation is
        /// enabled, its node is not written.
        /// </exception>
        public void WriteEndPropertyKey()
        {
            ThrowIfJsonCompatible(nameof(WriteEndPropertyKey));
            if (!InExplicitKeyNode)
            {
                ThrowHelper.ThrowInvalidOperationException_ExplicitKeyNotOpen();
            }

            if (!_options.SkipValidation && (_tokenType == YamlTokenType.StartArray || PropertiesPending))
            {
                ThrowHelper.ThrowInvalidOperationException_ExplicitKeyOneNode();
            }

            _blockSpecial &= ~(SpecialProperties | SpecialValueOnNewLine);
            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();

            _bitStack.Pop();
            _enclosingContainer = EnclosingContainerType.Object;
            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth--;
            if (--_block!.ExplicitKeyCount == 0)
            {
                _blockSpecial &= ~SpecialExplicitKey;
            }

            int required = _newLineLength + _blockColumn + 3;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (InFlow)
            {
                output[BytesPending++] = YamlConstants.Space;
            }
            else
            {
                WriteBlockLineStart(output, _blockColumn);
            }

            output[BytesPending++] = YamlConstants.KeyValueSeparator;
            _tokenType = YamlTokenType.PropertyName;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Documents and directives

        /// <summary>Writes <c>...</c>, the end of a document.</summary>
        /// <remarks>
        /// A new document may follow, and so may directives. A root node written without
        /// <see cref="WriteDocumentStart()"/> first gets its <c>---</c> anyway, which YAML 1.1 readers need there.
        /// Supported in block style only.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, the writer is inside a collection
        /// or a string, or after directives, or an anchor or a tag is pending.
        /// </exception>
        public void WriteDocumentEnd()
        {
            ThrowIfJsonCompatible(nameof(WriteDocumentEnd));
            if (!_options.SkipValidation
                && (CurrentDepth != 0 || IsWritingPartialString || PropertiesPending || (_blockSpecial & SpecialDirectives) != 0))
            {
                ThrowHelper.ThrowInvalidOperationException_DocumentEndNotAtRoot();
            }

            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();
            int required = 3 + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            output[BytesPending++] = (byte)'.';
            output[BytesPending++] = (byte)'.';
            output[BytesPending++] = (byte)'.';
            WriteNewLine(output);
            ResetRootForNewDocument();
            _block!.AfterDocumentStartMarker = false;
            _blockSpecial |= SpecialAfterDocumentEnd;
        }

        // "%TAG !h! prefix": the handle and the prefix, in the writer's table.
        private void RecordTagDirective(ReadOnlySpan<byte> directive)
        {
            ReadOnlySpan<byte> args = YamlReaderHelper.TrimBlanks(directive.Slice(4));
            int split = YamlReaderHelper.IndexOfBlank(args);
            if (split <= 0)
            {
                return;
            }

            ReadOnlySpan<byte> prefix = YamlReaderHelper.TrimBlanks(args.Slice(split));
            int end = YamlReaderHelper.IndexOfBlank(prefix);
            prefix = end < 0 ? prefix : prefix.Slice(0, end);
            _block!.TagDirectives = YamlTagDirectives.With(_block.TagDirectives, args.Slice(0, split), prefix);
        }

        // A document that starts without directives of its own: under YAML 1.2's scope it has only the default handles.
        private void ScopeTagDirectivesToNewDocument()
        {
            if (_block is not null && (_blockSpecial & SpecialDirectives) == 0 && !_options.InheritTagDirectives && !_block.Yaml11Scope)
            {
                _block.TagDirectives = null;
            }
        }

        /// <summary>The writer's %TAG handles for the current document, or null.</summary>
        internal YamlTagDirectives? TagDirectives => _block?.TagDirectives;

        // A root node after "...": "---" first.
        private void WriteDocumentStartAfterEnd()
        {
            ScopeTagDirectivesToNewDocument();
            _blockSpecial &= ~SpecialAfterDocumentEnd;
            int required = 3 + _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            output[BytesPending++] = (byte)'-';
            output[BytesPending++] = (byte)'-';
            output[BytesPending++] = (byte)'-';
            WriteNewLine(output);
        }

        // A new document's root: nothing written in it yet.
        private void ResetRootForNewDocument()
        {
            _tokenType = YamlTokenType.None;
            _enclosingContainer = EnclosingContainerType.None;
            _commentAfterNoneOrPropertyName = false;
            _currentDepth = 0;
            _bitStack = default;
            _blockColumn = 0;
            _blockSpecial &= SpecialPermanent | SpecialDirectives;
            if (_block is not null)
            {
                _block.FlowDepth = 0;
                _block.ExplicitKeyCount = 0;
            }
        }

        /// <summary>
        /// Writes a directive, <c>%YAML 1.2</c> or <c>%TAG !e! tag:example.com,2000:</c>, for the next document.
        /// </summary>
        /// <param name="directive">The directive's line, with its <c>%</c>.</param>
        /// <remarks>
        /// Directives come at the start of the output, after the end of a document (<c>...</c> is written first when the
        /// previous document is complete and not ended), or after another directive; <see cref="WriteDocumentStart()"/>
        /// must follow them. Supported in block style only.
        /// </remarks>
        /// <exception cref="ArgumentException">The text is not a <c>%YAML</c> or <c>%TAG</c> directive.</exception>
        /// <exception cref="InvalidOperationException">
        /// The writer is in JSON-compatible style; or, while validation is enabled, a directive cannot come here, or a
        /// second <c>%YAML</c> directive is written for the same document.
        /// </exception>
        public void WriteDirective(string directive)
        {
            ArgumentNullException.ThrowIfNull(directive);
            WriteDirective(directive.AsSpan());
        }

        /// <inheritdoc cref="WriteDirective(string)"/>
        public void WriteDirective(ReadOnlySpan<char> directive)
        {
            ThrowIfJsonCompatible(nameof(WriteDirective));
            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(directive, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteDirective(utf8);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteDirective(string)"/>
        public void WriteDirective(ReadOnlySpan<byte> utf8Directive)
        {
            ThrowIfJsonCompatible(nameof(WriteDirective));
            YamlWriterHelper.DirectiveKind kind = YamlWriterHelper.GetDirectiveKind(utf8Directive);
            if (kind == YamlWriterHelper.DirectiveKind.None)
            {
                ThrowHelper.ThrowArgumentException_InvalidDirective();
            }

            if (!_options.SkipValidation)
            {
                if (CurrentDepth != 0 || IsWritingPartialString || PropertiesPending
                    || (_tokenType == YamlTokenType.None && _block!.AfterDocumentStartMarker))
                {
                    ThrowHelper.ThrowInvalidOperationException_DirectiveNotAllowed();
                }

                if (kind == YamlWriterHelper.DirectiveKind.Yaml && _block!.YamlDirectivePending)
                {
                    ThrowHelper.ThrowInvalidOperationException_DuplicateYamlDirective();
                }
            }

            CheckNoScalarStyleHint();
            CheckNoCollectionStyleHint();
            int required = utf8Directive.Length + 3 + 2 * _newLineLength;
            if (_memory.Length - BytesPending < required)
            {
                Grow(required);
            }

            Span<byte> output = _memory.Span;
            if (_tokenType != YamlTokenType.None)
            {
                // A complete document that was not ended: directives need its "..." first.
                output[BytesPending++] = (byte)'.';
                output[BytesPending++] = (byte)'.';
                output[BytesPending++] = (byte)'.';
                WriteNewLine(output);
                ResetRootForNewDocument();
            }

            if ((_blockSpecial & SpecialDirectives) == 0)
            {
                // A document's first directive starts its own table (YAML 1.1 section 7.4; YAML 1.2 has no other).
                _block!.TagDirectives = null;
            }

            if (kind == YamlWriterHelper.DirectiveKind.Tag)
            {
                RecordTagDirective(utf8Directive);
            }
            else if (kind == YamlWriterHelper.DirectiveKind.Yaml)
            {
                // %YAML 1.0 or 1.1: YAML 1.1's directive scope, as the reader applies it; 1.2 or later ends it.
                ReadOnlySpan<byte> version = YamlReaderHelper.TrimBlanks(utf8Directive.Slice(5));
                int comment = YamlReaderHelper.IndexOfComment(version);
                version = YamlReaderHelper.TrimBlanks(comment >= 0 ? version.Slice(0, comment) : version);
                int dot = version.IndexOf((byte)'.');
                _block!.Yaml11Scope = dot > 0 && version.Slice(0, dot).SequenceEqual("1"u8) && YamlReaderHelper.IsYaml11Version(version.Slice(dot + 1));
            }

            utf8Directive.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Directive.Length;
            WriteNewLine(output);
            _blockSpecial = (_blockSpecial & ~SpecialAfterDocumentEnd) | SpecialDirectives;
            _block!.AfterDocumentStartMarker = false;
            if (kind == YamlWriterHelper.DirectiveKind.Yaml)
            {
                _block!.YamlDirectivePending = true;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Comments

        /// <summary>
        /// Writes a comment. In block style, <paramref name="endOfLine"/> asks for it at the end of the current line
        /// (<c>key: value # text</c>), which is where it goes after a scalar value, an alias or an empty collection that
        /// is a mapping value or a sequence item; anywhere else, and for a comment of several lines, it is written on
        /// lines of its own, as <see cref="WriteCommentValue(string)"/> writes it. In JSON-compatible style
        /// <paramref name="endOfLine"/> is ignored.
        /// </summary>
        /// <param name="value">The comment's text.</param>
        /// <param name="endOfLine">Whether to put the comment at the end of the current line where it can go.</param>
        /// <exception cref="ArgumentException">
        /// The text is too large; in JSON-compatible style it contains <c>*/</c>; in block style it holds a character a
        /// comment cannot hold.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        public void WriteCommentValue(string value, bool endOfLine)
        {
            ArgumentNullException.ThrowIfNull(value);
            WriteCommentValue(value.AsSpan(), endOfLine);
        }

        /// <inheritdoc cref="WriteCommentValue(string, bool)"/>
        public void WriteCommentValue(ReadOnlySpan<char> value, bool endOfLine)
        {
            if (_style != WriterStyleBlock)
            {
                WriteCommentValue(value);
                return;
            }

            YamlWriterHelper.ValidateValue(value);
            if (!_options.SkipValidation)
            {
                ValidateNotWithinUnfinalizedString();
            }

            byte[]? rented = null;
            ReadOnlySpan<byte> utf8 = TranscodeArgument(value, ref rented, stackalloc byte[YamlConstants.StackallocByteThreshold]);
            WriteCommentBlock(utf8, endOfLine);
            ReturnRented(rented);
        }

        /// <inheritdoc cref="WriteCommentValue(string, bool)"/>
        public void WriteCommentValue(ReadOnlySpan<byte> utf8Value, bool endOfLine)
        {
            if (_style != WriterStyleBlock)
            {
                WriteCommentValue(utf8Value);
                return;
            }

            YamlWriterHelper.ValidateValue(utf8Value);
            if (!YamlWriterHelper.IsValidUtf8String(utf8Value))
            {
                ThrowHelper.ThrowArgumentException_InvalidUTF8(utf8Value);
            }

            if (!_options.SkipValidation)
            {
                ValidateNotWithinUnfinalizedString();
            }

            WriteCommentBlock(utf8Value, endOfLine);
        }

        // A comment in block style, as text (valid UTF-8).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteCommentBlock(ReadOnlySpan<byte> comment, bool endOfLine)
        {
            if (!YamlWriterHelper.IsValidBlockComment(comment))
            {
                ThrowHelper.ThrowArgumentException_InvalidBlockComment();
            }

            if (PropertiesPending && _enclosingContainer == EnclosingContainerType.Object && _tokenType != YamlTokenType.PropertyName
                && !InFlow)
            {
                // "&a" on a line before the key would belong to the mapping, not to the key.
                ThrowHelper.ThrowInvalidOperationException_CommentBetweenKeyPropertiesAndKey();
            }

            bool multiLine = comment.IndexOfAny((byte)'\n', (byte)'\r') >= 0;
            if (InFlow)
            {
                WriteFlowComment(comment);
            }
            else if (endOfLine && !multiLine && CanWriteEndOfLineComment())
            {
                int required = comment.Length + 3;
                if (_memory.Length - BytesPending < required)
                {
                    Grow(required);
                }

                Span<byte> output = _memory.Span;
                output[BytesPending++] = YamlConstants.Space;
                WriteCommentLine(output, comment);
            }
            else
            {
                WriteOwnLineComment(comment);
            }

            if (_tokenType is YamlTokenType.PropertyName or YamlTokenType.None)
            {
                _commentAfterNoneOrPropertyName = true;
            }
        }

        private void WriteCommentLine(Span<byte> output, ReadOnlySpan<byte> line)
        {
            output[BytesPending++] = (byte)'#';
            if (!line.IsEmpty)
            {
                output[BytesPending++] = YamlConstants.Space;
                line.CopyTo(output.Slice(BytesPending));
                BytesPending += line.Length;
            }
        }

        // After a scalar value, an alias or an empty or flow collection, inside a block collection, on the line it ended.
        private bool CanWriteEndOfLineComment()
        {
            if (_bitStack.CurrentDepth == 0 || (_blockSpecial & (SpecialProperties | SpecialValueOnNewLine | SpecialCommentSinceStart)) != 0)
            {
                return false;
            }

            long position = BlockPosition;
            return _tokenType switch
            {
                YamlTokenType.String or YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null
                    or YamlTokenType.Alias => position != _block!.BlockScalarEnd,
                YamlTokenType.EndObject or YamlTokenType.EndArray => position == _block!.EndOfLineCommentAfterEnd,
                _ => false,
            };
        }

        // Each line of the comment on a line of its own, "# text", at the column of what comes next.
        private void WriteOwnLineComment(ReadOnlySpan<byte> comment)
        {
            bool valuePosition = PropertiesPending
                || (_enclosingContainer == EnclosingContainerType.Object && _tokenType == YamlTokenType.PropertyName)
                || (InExplicitKeyNode && _tokenType == YamlTokenType.StartArray);
            bool firstChild = !valuePosition && _bitStack.CurrentDepth > 0 && _tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray;
            bool rootLevel = !valuePosition && _bitStack.CurrentDepth == 0;

            // The writer is at the start of a line at the root (at the start, after "---", "..." or a directive, or after
            // a complete root value), and in a root collection with nothing written for it yet.
            bool lineStart = !PropertiesPending
                && (_bitStack.CurrentDepth == 0
                    || (_bitStack.CurrentDepth == 1 && firstChild && (_blockSpecial & (SpecialNoCompact | SpecialCommentSinceStart)) == 0));
            int column = valuePosition ? ValueColumn : rootLevel ? 0 : _blockColumn;

            while (true)
            {
                int end = comment.IndexOfAny((byte)'\n', (byte)'\r');
                ReadOnlySpan<byte> line = end < 0 ? comment : comment.Slice(0, end);
                int required = line.Length + column + 2 + 2 * _newLineLength;
                if (_memory.Length - BytesPending < required)
                {
                    Grow(required);
                }

                Span<byte> output = _memory.Span;
                if (!lineStart)
                {
                    WriteNewLine(output);
                }

                WriteBlockIndentation(output, column);
                WriteCommentLine(output, line);
                lineStart = false;
                if (end < 0)
                {
                    break;
                }

                int next = end + (comment[end] == (byte)'\r' && end + 1 < comment.Length && comment[end + 1] == (byte)'\n' ? 2 : 1);
                comment = comment.Slice(next);
            }

            if (rootLevel)
            {
                // At the root, what follows starts on a line of its own.
                WriteNewLine(_memory.Span);
            }
            else if (valuePosition)
            {
                _blockSpecial |= SpecialValueOnNewLine;
            }
            else if (firstChild)
            {
                _blockSpecial |= SpecialCommentSinceStart;
            }
        }

        // A comment inside a flow collection ends the line: the separator before it, " # text", then a new line at a
        // column past the enclosing block's.
        private void WriteFlowComment(ReadOnlySpan<byte> comment)
        {
            bool freshLine = (_blockSpecial & SpecialFlowFreshLine) != 0;
            bool separator = !freshLine
                && _tokenType is not (YamlTokenType.StartObject or YamlTokenType.StartArray or YamlTokenType.PropertyName)
                && !InExplicitKeyNode;
            int column = _blockColumn + _indentLength;
            while (true)
            {
                int end = comment.IndexOfAny((byte)'\n', (byte)'\r');
                ReadOnlySpan<byte> line = end < 0 ? comment : comment.Slice(0, end);
                int required = line.Length + column + 4 + _newLineLength;
                if (_memory.Length - BytesPending < required)
                {
                    Grow(required);
                }

                Span<byte> output = _memory.Span;
                if (separator)
                {
                    output[BytesPending++] = YamlConstants.ListSeparator;
                    separator = false;
                }

                if (!freshLine)
                {
                    output[BytesPending++] = YamlConstants.Space;
                }

                WriteCommentLine(output, line);
                WriteBlockLineStart(output, column);
                freshLine = true;
                if (end < 0)
                {
                    break;
                }

                int next = end + (comment[end] == (byte)'\r' && end + 1 < comment.Length && comment[end + 1] == (byte)'\n' ? 2 : 1);
                comment = comment.Slice(next);
            }

            _blockSpecial |= SpecialFlowFreshLine;
        }
    }
}
