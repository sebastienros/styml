// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        private static readonly char[] s_singleLineCommentDelimiter = new char[2] { '*', '/' };
        private static ReadOnlySpan<byte> SingleLineCommentDelimiterUtf8 => "*/"u8;

        /// <summary>
        /// Writes the string text value (as a YAML comment).
        /// </summary>
        /// <param name="value">The value to write as a YAML comment within /*..*/.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given string text value contains a comment delimiter (that is, */).
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="value"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        public void WriteCommentValue(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            WriteCommentValue(value.AsSpan());
        }

        /// <summary>
        /// Writes the text value (as a YAML comment).
        /// </summary>
        /// <param name="value">The value to write as a YAML comment within /*..*/.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given text value contains a comment delimiter (that is, */).
        /// </exception>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        public void WriteCommentValue(ReadOnlySpan<char> value)
        {
            YamlWriterHelper.ValidateValue(value);

            if (_style != WriterStyleBlock && value.Contains(s_singleLineCommentDelimiter, StringComparison.Ordinal))
            {
                ThrowHelper.ThrowArgumentException_InvalidCommentValue();
            }

            WriteCommentByOptions(value);
            if (_tokenType is YamlTokenType.PropertyName or YamlTokenType.None)
            {
                _commentAfterNoneOrPropertyName = true;
            }
        }

        private void WriteCommentByOptions(ReadOnlySpan<char> value)
        {
            if (!_options.SkipValidation)
            {
                // Comments generally can be placed anywhere in JSON, but not after a non-final
                // string segment.
                ValidateNotWithinUnfinalizedString();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteCommentMinimized(value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteCommentIndented(value);
            }
            else
            {
                WriteCommentBlock(value);
            }
        }

        private void WriteCommentMinimized(ReadOnlySpan<char> value)
        {
            Debug.Assert(value.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - 4);

            // All ASCII, /*...*/ => escapedValue.Length + 4
            // Optionally, up to 3x growth when transcoding
            int maxRequired = (value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + 4;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            output[BytesPending++] = YamlConstants.Slash;
            output[BytesPending++] = YamlConstants.Asterisk;

            OperationStatus status = YamlWriterHelper.ToUtf8(value, output.Slice(BytesPending), out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            if (status == OperationStatus.InvalidData)
            {
                ThrowHelper.ThrowArgumentException_InvalidUTF16(value[written]);
            }

            BytesPending += written;

            output[BytesPending++] = YamlConstants.Asterisk;
            output[BytesPending++] = YamlConstants.Slash;
        }

        private void WriteCommentIndented(ReadOnlySpan<char> value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(value.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - indent - 4 - _newLineLength);

            // All ASCII, /*...*/ => escapedValue.Length + 4
            // Optionally, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + 4 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_tokenType != YamlTokenType.None || _commentAfterNoneOrPropertyName)
            {
                WriteNewLine(output);
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = YamlConstants.Slash;
            output[BytesPending++] = YamlConstants.Asterisk;

            OperationStatus status = YamlWriterHelper.ToUtf8(value, output.Slice(BytesPending), out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            if (status == OperationStatus.InvalidData)
            {
                ThrowHelper.ThrowArgumentException_InvalidUTF16(value[written]);
            }

            BytesPending += written;

            output[BytesPending++] = YamlConstants.Asterisk;
            output[BytesPending++] = YamlConstants.Slash;
        }

        /// <summary>
        /// Writes the UTF-8 text value (as a YAML comment).
        /// </summary>
        /// <param name="utf8Value">The UTF-8 encoded value to be written as a YAML comment within /*..*/.</param>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given UTF-8 text value contains a comment delimiter (that is, */).
        /// </exception>
        public void WriteCommentValue(ReadOnlySpan<byte> utf8Value)
        {
            YamlWriterHelper.ValidateValue(utf8Value);

            if (_style != WriterStyleBlock && utf8Value.IndexOf(SingleLineCommentDelimiterUtf8) >= 0)
            {
                ThrowHelper.ThrowArgumentException_InvalidCommentValue();
            }

            if (!YamlWriterHelper.IsValidUtf8String(utf8Value))
            {
                ThrowHelper.ThrowArgumentException_InvalidUTF8(utf8Value);
            }

            WriteCommentByOptions(utf8Value);
            if (_tokenType is YamlTokenType.PropertyName or YamlTokenType.None)
            {
                _commentAfterNoneOrPropertyName = true;
            }
        }

        private void WriteCommentByOptions(ReadOnlySpan<byte> utf8Value)
        {
            if (_style == WriterStyleJsonMinimized)
            {
                WriteCommentMinimized(utf8Value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteCommentIndented(utf8Value);
            }
            else
            {
                WriteCommentBlock(utf8Value, endOfLine: false);
            }
        }

        private void WriteCommentMinimized(ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8Value.Length < int.MaxValue - 4);

            int maxRequired = utf8Value.Length + 4; // /*...*/

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            output[BytesPending++] = YamlConstants.Slash;
            output[BytesPending++] = YamlConstants.Asterisk;

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;

            output[BytesPending++] = YamlConstants.Asterisk;
            output[BytesPending++] = YamlConstants.Slash;
        }

        private void WriteCommentIndented(ReadOnlySpan<byte> utf8Value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(utf8Value.Length < int.MaxValue - indent - 4 - _newLineLength);

            int minRequired = indent + utf8Value.Length + 4; // /*...*/
            int maxRequired = minRequired + _newLineLength; // Optionally, 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_tokenType != YamlTokenType.None || _commentAfterNoneOrPropertyName)
            {
                WriteNewLine(output);

                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = YamlConstants.Slash;
            output[BytesPending++] = YamlConstants.Asterisk;

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;

            output[BytesPending++] = YamlConstants.Asterisk;
            output[BytesPending++] = YamlConstants.Slash;
        }
    }
}
