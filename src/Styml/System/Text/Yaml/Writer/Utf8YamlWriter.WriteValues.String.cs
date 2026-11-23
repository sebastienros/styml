// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the pre-encoded text value (as a YAML string) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The YAML-encoded value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStringValue(YamlEncodedText value)
        {
            ReadOnlySpan<byte> utf8Value = value.EncodedUtf8Bytes;
            Debug.Assert(utf8Value.Length <= YamlConstants.MaxUnescapedTokenSize);

            WriteStringByOptions(utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        /// <summary>
        /// Writes the string text value (as a YAML string) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// <para>
        /// The value is escaped before writing.</para>
        /// <para>
        /// If <paramref name="value"/> is <see langword="null"/> the YAML null value is written,
        /// as if <see cref="WriteNullValue"/> was called.
        /// </para>
        /// </remarks>
        public void WriteStringValue(string? value)
        {
            if (value is null)
            {
                WriteNullValue();
            }
            else
            {
                WriteStringValue(value.AsSpan());
            }
        }

        /// <summary>
        /// Writes the text value (as a YAML string) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The value is escaped before writing.
        /// </remarks>
        public void WriteStringValue(ReadOnlySpan<char> value)
        {
            YamlWriterHelper.ValidateValue(value);

            WriteStringEscape(value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        private void WriteStringEscape(ReadOnlySpan<char> value)
        {
            // Block style judges most strings in one scan (M4p, candidate b).
            if (_style == WriterStyleBlock && TryWriteStringValueBlockOneScan(value))
            {
                return;
            }

            int valueIdx = YamlWriterHelper.NeedsEscaping(value, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < value.Length);

            if (valueIdx != -1)
            {
                WriteStringEscapeValue(value, valueIdx);
            }
            else
            {
                // Each input char may transcode to up to 3 bytes.
                WriteStringByOptions(value, value.Length * YamlConstants.MaxExpansionFactorWhileTranscoding);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<char> value, int maxRequiredBytes)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteStringMinimized(value, maxRequiredBytes);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteStringIndented(value, maxRequiredBytes);
            }
            else
            {
                WriteStringValueBlock(value);
            }
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<char> escapedValue, int maxRequiredBytes)
        {
            Debug.Assert(maxRequiredBytes is >= 0 and < int.MaxValue - 3);

            // 2 quotes + optional 1 list separator, plus precomputed max bytes for the payload.
            int maxRequired = maxRequiredBytes + 3;

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

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = YamlConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<char> escapedValue, int maxRequiredBytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);
            Debug.Assert(maxRequiredBytes >= 0 && maxRequiredBytes < int.MaxValue - indent - 3 - _newLineLength);

            // indent + 2 quotes + optional 1 list separator + 1-2 bytes for new line, plus precomputed max bytes for the payload.
            int maxRequired = indent + maxRequiredBytes + 3 + _newLineLength;

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

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = YamlConstants.Quote;
        }

        private unsafe void WriteStringEscapeValue(ReadOnlySpan<char> value, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= value.Length);
            Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < value.Length);

            char[]? valueArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);

            Span<char> escapedValue = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (valueArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);

            // Each original input char expands to at most MaxExpansionFactorWhileEscaping bytes to the output.
            // Escaped sequences are all ASCII (1 byte each), so × 6 ≥ transcoded bytes.
            int requiredBytes = value.Length * YamlConstants.MaxExpansionFactorWhileEscaping;

            WriteStringByOptions(escapedValue.Slice(0, written), requiredBytes);

            if (valueArray is not null)
            {
                ArrayPool<char>.Shared.Return(valueArray);
            }
        }

        /// <summary>
        /// Writes the UTF-8 text value (as a YAML string) as an element of a YAML array.
        /// </summary>
        /// <param name="utf8Value">The UTF-8 encoded value to be written as a YAML string element of a YAML array.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The value is escaped before writing.
        /// </remarks>
        public void WriteStringValue(ReadOnlySpan<byte> utf8Value)
        {
            YamlWriterHelper.ValidateValue(utf8Value);

            WriteStringEscape(utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        private void WriteStringEscape(ReadOnlySpan<byte> utf8Value)
        {
            // Block style judges most strings in one scan (M4p, candidate b).
            if (_style == WriterStyleBlock && TryWriteStringValueBlockOneScan(utf8Value))
            {
                return;
            }

            int valueIdx = YamlWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length);

            if (valueIdx != -1)
            {
                WriteStringEscapeValue(utf8Value, valueIdx);
            }
            else
            {
                WriteStringByOptions(utf8Value);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<byte> utf8Value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteStringMinimized(utf8Value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteStringIndented(utf8Value);
            }
            else
            {
                WriteStringValueBlock(utf8Value);
            }
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<byte> escapedValue)
        {
            Debug.Assert(escapedValue.Length < int.MaxValue - 3);

            int minRequired = escapedValue.Length + 2; // 2 quotes
            int maxRequired = minRequired + 1; // Optionally, 1 list separator

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

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = YamlConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<byte> escapedValue)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedValue.Length < int.MaxValue - indent - 3 - _newLineLength);

            int minRequired = indent + escapedValue.Length + 2; // 2 quotes
            int maxRequired = minRequired + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = YamlConstants.Quote;
        }

        private unsafe void WriteStringEscapeValue(ReadOnlySpan<byte> utf8Value, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);

            byte[]? valueArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

            Span<byte> escapedValue = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (valueArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);

            WriteStringByOptions(escapedValue.Slice(0, written));

            if (valueArray is not null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }
        }

        /// <summary>
        /// Writes a number as a YAML string. The string value is not escaped.
        /// </summary>
        /// <param name="utf8Value"></param>
        internal void WriteNumberValueAsStringUnescaped(ReadOnlySpan<byte> utf8Value)
        {
            // The value has been validated prior to calling this method.

            WriteStringByOptions(utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }
    }
}
