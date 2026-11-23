// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the <see cref="uint"/> value (as a YAML number) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="uint"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumberValue(uint value)
            => WriteNumberValue((ulong)value);

        /// <summary>
        /// Writes the <see cref="ulong"/> value (as a YAML number) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="ulong"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumberValue(ulong value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteNumberValueMinimized(value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteNumberValueIndented(value);
            }
            else
            {
                WriteNumberValueBlock(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        private void WriteNumberValueMinimized(ulong value)
        {
            int maxRequired = YamlConstants.MaximumFormatUInt64Length + 1; // Optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberValueIndented(ulong value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + YamlConstants.MaximumFormatUInt64Length + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        internal unsafe void WriteNumberValueAsString(ulong value)
        {
            Span<byte> utf8Number = stackalloc byte[YamlConstants.MaximumFormatUInt64Length];
            bool result = Utf8Formatter.TryFormat(value, utf8Number, out int bytesWritten);
            Debug.Assert(result);
            WriteNumberValueAsStringUnescaped(utf8Number.Slice(0, bytesWritten));
        }
    }
}
