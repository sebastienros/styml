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
        /// Writes the pre-encoded property name and <see cref="DateTime"/> value (as a YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="DateTime"/> using the round-trippable ('O') <see cref="StandardFormat"/> , for example: 2017-06-12T05:30:45.7680000.
        /// The property name should already be escaped when the instance of <see cref="YamlEncodedText"/> was created.
        /// </remarks>
        public void WriteString(YamlEncodedText propertyName, DateTime value)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            WriteStringByOptions(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        /// <summary>
        /// Writes the property name and <see cref="DateTime"/> value (as a YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="DateTime"/> using the round-trippable ('O') <see cref="StandardFormat"/> , for example: 2017-06-12T05:30:45.7680000.
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(string propertyName, DateTime value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteString(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="DateTime"/> value (as a YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="DateTime"/> using the round-trippable ('O') <see cref="StandardFormat"/> , for example: 2017-06-12T05:30:45.7680000.
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<char> propertyName, DateTime value)
        {
            YamlWriterHelper.ValidateProperty(propertyName);

            WriteStringEscape(propertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        /// <summary>
        /// Writes the property name and <see cref="DateTime"/> value (as a YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="DateTime"/> using the round-trippable ('O') <see cref="StandardFormat"/> , for example: 2017-06-12T05:30:45.7680000.
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);

            WriteStringEscape(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        private void WriteStringEscape(ReadOnlySpan<char> propertyName, DateTime value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStringEscapeProperty(propertyName, value, propertyIdx);
            }
            else
            {
                WriteStringByOptions(propertyName, value);
            }
        }

        private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStringEscapeProperty(utf8PropertyName, value, propertyIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, value);
            }
        }

        private unsafe void WriteStringEscapeProperty(ReadOnlySpan<char> propertyName, DateTime value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private unsafe void WriteStringEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, DateTime value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<char> propertyName, DateTime value)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteStringMinimized(propertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteStringIndented(propertyName, value);
            }
            else
            {
                WriteStringBlock(propertyName, value);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteStringMinimized(utf8PropertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteStringIndented(utf8PropertyName, value);
            }
            else
            {
                WriteStringBlock(utf8PropertyName, value);
            }
        }

        private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, DateTime value)
        {
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - YamlConstants.MaximumFormatDateTimeOffsetLength - 6);

            // All ASCII, 2 quotes for property name, 2 quotes for date, and 1 colon => escapedPropertyName.Length + YamlConstants.MaximumFormatDateTimeOffsetLength + 5
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatDateTimeOffsetLength + 6;

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

            TranscodeAndWrite(escapedPropertyName, output);

            output[BytesPending++] = YamlConstants.Quote;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, DateTime value)
        {
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - YamlConstants.MaximumFormatDateTimeOffsetLength - 6);

            int minRequired = escapedPropertyName.Length + YamlConstants.MaximumFormatDateTimeOffsetLength + 5; // 2 quotes for property name, 2 quotes for date, and 1 colon
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

            escapedPropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyName.Length;

            output[BytesPending++] = YamlConstants.Quote;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, DateTime value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - indent - YamlConstants.MaximumFormatDateTimeOffsetLength - 7 - _newLineLength);

            // All ASCII, 2 quotes for property name, 2 quotes for date, 1 colon, and 1 space => escapedPropertyName.Length + YamlConstants.MaximumFormatDateTimeOffsetLength + 6
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatDateTimeOffsetLength + 7 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != YamlTokenType.PropertyName);

            if (_tokenType != YamlTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = YamlConstants.Quote;

            TranscodeAndWrite(escapedPropertyName, output);

            output[BytesPending++] = YamlConstants.Quote;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;
            output[BytesPending++] = YamlConstants.Space;

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, DateTime value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - YamlConstants.MaximumFormatDateTimeOffsetLength - 7 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + YamlConstants.MaximumFormatDateTimeOffsetLength + 6; // 2 quotes for property name, 2 quotes for date, 1 colon, and 1 space
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

            Debug.Assert(_options.SkipValidation || _tokenType != YamlTokenType.PropertyName);

            if (_tokenType != YamlTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = YamlConstants.Quote;

            escapedPropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyName.Length;

            output[BytesPending++] = YamlConstants.Quote;
            output[BytesPending++] = YamlConstants.KeyValueSeparator;
            output[BytesPending++] = YamlConstants.Space;

            output[BytesPending++] = YamlConstants.Quote;

            YamlWriterHelper.WriteDateTimeTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
            BytesPending += bytesWritten;

            output[BytesPending++] = YamlConstants.Quote;
        }

        internal unsafe void WritePropertyName(DateTime value)
        {
            Span<byte> buffer = stackalloc byte[YamlConstants.MaximumFormatDateTimeOffsetLength];
            YamlWriterHelper.WriteDateTimeTrimmed(buffer, value, out int bytesWritten);
            WritePropertyNameUnescaped(buffer.Slice(0, bytesWritten));
        }
    }
}
