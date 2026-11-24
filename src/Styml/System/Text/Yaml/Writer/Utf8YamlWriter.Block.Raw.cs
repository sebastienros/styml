// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    // WriteRawValue in block style: the raw text is read as a
    // stand-alone, one-document stream that must hold exactly one node, and it is placed by the node's kind, with every
    // line after the first prefixed by the child column. The JSON-compatible style's validation is unchanged in M4a.
    public sealed partial class Utf8YamlWriter
    {
        private enum RawNodeKind : byte
        {
            BlockCollection,
            BlockScalar,
            Inline, // a flow collection, or a quoted or plain scalar
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteRawValueBlock(ReadOnlySpan<byte> utf8Yaml, bool skipInputValidation)
        {
            CheckNoScalarStyleHint();
            if (utf8Yaml.IsEmpty)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedYamlTokens);
            }

            RawNodeKind kind;
            YamlTokenType tokenType;
            if (skipInputValidation)
            {
                // As in the JSON-compatible style, unvalidated text counts as a string for the structural checks. Its
                // kind still decides the placement: a first line that is a mapping entry or a dash is a block collection.
                kind = GuessRawKind(utf8Yaml);
                tokenType = YamlTokenType.String;
            }
            else
            {
                kind = ValidateRawYaml(utf8Yaml, out tokenType);
            }

            // The final line break is the structure's (the next entry's, or the root's).
            if (utf8Yaml[utf8Yaml.Length - 1] == (byte)'\n')
            {
                utf8Yaml = utf8Yaml.Slice(0, utf8Yaml.Length - 1);
                if (!utf8Yaml.IsEmpty && utf8Yaml[utf8Yaml.Length - 1] == (byte)'\r')
                {
                    utf8Yaml = utf8Yaml.Slice(0, utf8Yaml.Length - 1);
                }
            }

            bool root = _enclosingContainer == EnclosingContainerType.None;
            int childColumn = root ? 0 : _blockColumn + (kind == RawNodeKind.BlockCollection
                ? BlockDelta(IsRawSequence(utf8Yaml), _enclosingContainer)
                : _indentLength);

            int lines = 1;
            foreach (byte b in utf8Yaml)
            {
                if (b == (byte)'\n')
                {
                    lines++;
                }
            }

            int extra = utf8Yaml.Length + (lines + 1) * (childColumn + _newLineLength) + 1;
            Span<byte> output;
            if (_blockSpecial != 0)
            {
                if (InFlow && (lines > 1 || kind == RawNodeKind.BlockCollection))
                {
                    ThrowHelper.ThrowInvalidOperationException_RawYamlInFlowCollection();
                }

                if (kind == RawNodeKind.BlockCollection && !InFlow
                    && (_enclosingContainer == EnclosingContainerType.Object || PropertiesPending || (_blockSpecial & SpecialValueOnNewLine) != 0))
                {
                    // A block collection after "key:", after node properties ("- &a") or after a comment line starts on a
                    // new line at the child column.
                    CheckSpecialNode(BlockNode.Scalar);

                    // Read before the bits are cleared: after properties the item's dash is already written.
                    bool propertiesPending = PropertiesPending;
                    _blockSpecial &= ~(SpecialProperties | SpecialValueOnNewLine);
                    if (_memory.Length - BytesPending < extra + BlockPrefixLength)
                    {
                        Grow(extra + BlockPrefixLength);
                    }

                    output = _memory.Span;
                    if (_enclosingContainer == EnclosingContainerType.Array && !propertiesPending && BytesPending > 0
                        && output[BytesPending - 1] != (byte)'-' && output[BytesPending - 1] != YamlConstants.Space)
                    {
                        // A sequence item after a comment line: its dash first.
                        WriteBlockEntryStartSpecial(output);
                        output[BytesPending++] = (byte)'-';
                    }

                    WriteBlockLineStart(output, childColumn);
                }
                else
                {
                    output = BeginBlockValue(scalar: true, extra);
                }
            }
            else if (kind == RawNodeKind.BlockCollection && _enclosingContainer == EnclosingContainerType.Object)
            {
                // A block collection as a mapping value starts on a new line at the child column.
                output = BeginBlockValue(scalar: false, extra);
                WriteBlockLineStart(output, childColumn);
            }
            else
            {
                // On the key's or dash's line: a block collection as a compact sequence item, a block scalar's header,
                // a flow collection, a quoted or plain scalar. At the root, as written.
                output = BeginBlockValue(scalar: true, extra);
            }

            while (true)
            {
                int end = utf8Yaml.IndexOf((byte)'\n');
                ReadOnlySpan<byte> line = end < 0 ? utf8Yaml : utf8Yaml.Slice(0, end + 1);
                line.CopyTo(output.Slice(BytesPending));
                BytesPending += line.Length;
                if (end < 0)
                {
                    break;
                }

                utf8Yaml = utf8Yaml.Slice(end + 1);
                bool emptyLine = utf8Yaml.IsEmpty || utf8Yaml[0] is (byte)'\n' or (byte)'\r';
                if (!emptyLine)
                {
                    output.Slice(BytesPending, childColumn).Fill(YamlConstants.Space);
                    BytesPending += childColumn;
                }
            }

            if (lines > 1)
            {
                // Its last line may be a block scalar's content: no end-of-line comment after it.
                _block!.BlockScalarEnd = BlockPosition;
            }

            EndBlockScalar(tokenType);
        }

        // Reads the raw text as a stand-alone stream and returns the kind of its one node.
        private static RawNodeKind ValidateRawYaml(ReadOnlySpan<byte> utf8Yaml, out YamlTokenType lastTokenType)
        {
            // Document markers and directives at column 0 would end the enclosing document.
            ReadOnlySpan<byte> rest = utf8Yaml;
            while (true)
            {
                if (rest.Length >= 3 && ((rest[0] == '-' && rest[1] == '-' && rest[2] == '-') || (rest[0] == '.' && rest[1] == '.' && rest[2] == '.'))
                    && (rest.Length == 3 || rest[3] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                {
                    ThrowHelper.ThrowArgumentException(SR.Format(SR.RawYamlNotOneNode, "a document marker"));
                }

                if (!rest.IsEmpty && rest[0] == (byte)'%')
                {
                    ThrowHelper.ThrowArgumentException(SR.Format(SR.RawYamlNotOneNode, "a directive"));
                }

                int next = rest.IndexOf((byte)'\n');
                if (next < 0)
                {
                    break;
                }

                rest = rest.Slice(next + 1);
            }

            var reader = new Utf8YamlReader(utf8Yaml, new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, YamlCommentsOnly = true });
            RawNodeKind kind = RawNodeKind.Inline;
            int nodes = 0;
            int depth = 0;
            lastTokenType = YamlTokenType.None;
            while (reader.Read())
            {
                YamlTokenType type = reader.TokenType;
                if (type == YamlTokenType.Comment)
                {
                    if (nodes == 0 && depth == 0)
                    {
                        // Written after "key: ", a comment would take the node into its text.
                        ThrowHelper.ThrowArgumentException(SR.Format(SR.RawYamlNotOneNode, "a comment before the node"));
                    }

                    continue;
                }

                if (depth == 0)
                {
                    if (nodes == 1)
                    {
                        ThrowHelper.ThrowArgumentException(SR.Format(SR.RawYamlNotOneNode, "more than one node"));
                    }

                    nodes = 1;
                    if (type is YamlTokenType.StartObject or YamlTokenType.StartArray)
                    {
                        kind = reader.IsFlow ? RawNodeKind.Inline : RawNodeKind.BlockCollection;
                    }
                    else if (reader.ScalarStyle is YamlScalarStyle.Literal or YamlScalarStyle.Folded)
                    {
                        kind = RawNodeKind.BlockScalar;
                        if (HasIndentationIndicator(utf8Yaml))
                        {
                            // Relative to the parent's column, which changes when the text moves.
                            ThrowHelper.ThrowArgumentException(SR.Format(SR.RawYamlNotOneNode, "a block scalar with an indentation indicator"));
                        }
                    }
                }

                if (type is YamlTokenType.StartObject or YamlTokenType.StartArray)
                {
                    depth++;
                }
                else if (type is YamlTokenType.EndObject or YamlTokenType.EndArray)
                {
                    depth--;
                }

                lastTokenType = type;
            }

            if (nodes == 0)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedYamlTokens);
            }

            return kind;
        }

        // The header of a root block scalar ("|2", "|-2", ">+1"), after any node properties on its line.
        private static bool HasIndentationIndicator(ReadOnlySpan<byte> utf8Yaml)
        {
            int end = utf8Yaml.IndexOf((byte)'\n');
            ReadOnlySpan<byte> header = end < 0 ? utf8Yaml : utf8Yaml.Slice(0, end);
            int i = header.LastIndexOfAny((byte)'|', (byte)'>');
            for (i++; i >= 1 && i < header.Length && header[i] is not ((byte)' ' or (byte)'\t' or (byte)'#'); i++)
            {
                if (header[i] is >= (byte)'1' and <= (byte)'9')
                {
                    return true;
                }
            }

            return false;
        }

        private static RawNodeKind GuessRawKind(ReadOnlySpan<byte> utf8Yaml)
        {
            byte first = utf8Yaml[0];
            if (first is (byte)'|' or (byte)'>')
            {
                return RawNodeKind.BlockScalar;
            }

            if (IsRawSequence(utf8Yaml) || utf8Yaml.IndexOf(": "u8) >= 0 && first is not ((byte)'{' or (byte)'[' or (byte)'"' or (byte)'\''))
            {
                return RawNodeKind.BlockCollection;
            }

            return RawNodeKind.Inline;
        }

        private static bool IsRawSequence(ReadOnlySpan<byte> utf8Yaml) =>
            utf8Yaml.Length >= 2 && utf8Yaml[0] == (byte)'-' && utf8Yaml[1] is (byte)' ' or (byte)'\n' or (byte)'\r'
            || utf8Yaml.Length == 1 && utf8Yaml[0] == (byte)'-';

    }
}
