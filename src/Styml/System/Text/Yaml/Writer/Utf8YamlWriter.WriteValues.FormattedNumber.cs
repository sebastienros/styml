// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the value (as a YAML number) as an element of a YAML array.
        /// </summary>
        /// <param name="utf8FormattedNumber">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="utf8FormattedNumber"/> does not represent a valid YAML number.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="int"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// </remarks>
        internal void WriteNumberValue(ReadOnlySpan<byte> utf8FormattedNumber)
        {
            YamlWriterHelper.ValidateValue(utf8FormattedNumber);
            YamlWriterHelper.ValidateNumber(utf8FormattedNumber);

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteNumberValueMinimized(utf8FormattedNumber);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteNumberValueIndented(utf8FormattedNumber);
            }
            else
            {
                WriteNumberValueBlock(utf8FormattedNumber);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        private void WriteNumberValueMinimized(ReadOnlySpan<byte> utf8Value)
        {
            int maxRequired = utf8Value.Length + 1; // Optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;
        }

        private void WriteNumberValueIndented(ReadOnlySpan<byte> utf8Value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(utf8Value.Length < int.MaxValue - indent - 1 - _newLineLength);

            int maxRequired = indent + utf8Value.Length + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            if (_tokenType != YamlTokenType.PropertyName)
            {
                if (_tokenType != YamlTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;
        }
    }
}
