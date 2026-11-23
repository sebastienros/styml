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
        /// Writes the <see cref="DateTime"/> value (as a YAML string) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="DateTime"/> using the round-trippable ('O') <see cref="StandardFormat"/> , for example: 2017-06-12T05:30:45.7680000.
        /// </remarks>
        public void WriteStringValue(DateTime value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteStringValueMinimized(value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteStringValueIndented(value);
            }
            else
            {
                WriteStringValueBlock(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        private void WriteStringValueMinimized(DateTime value)
        {
            int maxRequired = YamlConstants.MaximumFormatDateTimeOffsetLength + 3; // 2 quotes, and optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteStringValueIndented(DateTime value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            // 2 quotes, and optionally, 1 list separator and 1-2 bytes for new line
            int maxRequired = indent + YamlConstants.MaximumFormatDateTimeOffsetLength + 3 + _newLineLength;

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

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }
    }
}
