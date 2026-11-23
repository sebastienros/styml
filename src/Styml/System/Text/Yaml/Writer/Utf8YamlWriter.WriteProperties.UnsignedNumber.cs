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
        /// Writes the pre-encoded property name and <see cref="ulong"/> value (as a YAML number) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="ulong"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(YamlEncodedText propertyName, ulong value)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            WriteNumberByOptions(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the property name and <see cref="ulong"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="ulong"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(string propertyName, ulong value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteNumber(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="ulong"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="ulong"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(ReadOnlySpan<char> propertyName, ulong value)
        {
            YamlWriterHelper.ValidateProperty(propertyName);

            WriteNumberEscape(propertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the property name and <see cref="ulong"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="ulong"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, ulong value)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);

            WriteNumberEscape(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the pre-encoded property name and <see cref="uint"/> value (as a YAML number) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="uint"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(YamlEncodedText propertyName, uint value)
            => WriteNumber(propertyName, (ulong)value);

        /// <summary>
        /// Writes the property name and <see cref="uint"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="uint"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(string propertyName, uint value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteNumber(propertyName.AsSpan(), (ulong)value);
        }

        /// <summary>
        /// Writes the property name and <see cref="uint"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="uint"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(ReadOnlySpan<char> propertyName, uint value)
            => WriteNumber(propertyName, (ulong)value);

        /// <summary>
        /// Writes the property name and <see cref="uint"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="uint"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        [CLSCompliant(false)]
        public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, uint value)
            => WriteNumber(utf8PropertyName, (ulong)value);

        private void WriteNumberEscape(ReadOnlySpan<char> propertyName, ulong value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteNumberEscapeProperty(propertyName, value, propertyIdx);
            }
            else
            {
                WriteNumberByOptions(propertyName, value);
            }
        }

        private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, ulong value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
            }
            else
            {
                WriteNumberByOptions(utf8PropertyName, value);
            }
        }

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, ulong value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ulong value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, ulong value)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteNumberMinimized(propertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteNumberIndented(propertyName, value);
            }
            else
            {
                WriteNumberBlock(propertyName, value);
            }
        }

        private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, ulong value)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteNumberMinimized(utf8PropertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteNumberIndented(utf8PropertyName, value);
            }
            else
            {
                WriteNumberBlock(utf8PropertyName, value);
            }
        }

        private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, ulong value)
        {
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - YamlConstants.MaximumFormatUInt64Length - 4);

            // All ASCII, 2 quotes for property name, and 1 colon => escapedPropertyName.Length + YamlConstants.MaximumFormatUInt64Length + 3
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatUInt64Length + 4;

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

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, ulong value)
        {
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - YamlConstants.MaximumFormatUInt64Length - 4);

            int minRequired = escapedPropertyName.Length + YamlConstants.MaximumFormatUInt64Length + 3; // 2 quotes for property name, and 1 colon
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

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, ulong value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - indent - YamlConstants.MaximumFormatUInt64Length - 5 - _newLineLength);

            // All ASCII, 2 quotes for property name, 1 colon, and 1 space => escapedPropertyName.Length + YamlConstants.MaximumFormatUInt64Length + 4
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatUInt64Length + 5 + _newLineLength;

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

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, ulong value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - YamlConstants.MaximumFormatUInt64Length - 5 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + YamlConstants.MaximumFormatUInt64Length + 4; // 2 quotes for property name, 1 colon, and 1 space
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

            bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        internal void WritePropertyName(uint value)
            => WritePropertyName((ulong)value);

        internal unsafe void WritePropertyName(ulong value)
        {
            Span<byte> utf8PropertyName = stackalloc byte[YamlConstants.MaximumFormatUInt64Length];

            bool result = Utf8Formatter.TryFormat(value, utf8PropertyName, out int bytesWritten);
            Debug.Assert(result);

            WritePlainPropertyName(utf8PropertyName.Slice(0, bytesWritten));
        }
    }
}
