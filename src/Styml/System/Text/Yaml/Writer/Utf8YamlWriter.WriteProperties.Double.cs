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
        /// Writes the pre-encoded property name and <see cref="double"/> value (as a YAML number) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> (that is, 'G').
        /// </remarks>
        public void WriteNumber(YamlEncodedText propertyName, double value)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            ValidateFiniteUnlessBlock(value);

            WriteNumberByOptions(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the property name and <see cref="double"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> (that is, 'G').
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(string propertyName, double value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteNumber(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="double"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> (that is, 'G').
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(ReadOnlySpan<char> propertyName, double value)
        {
            YamlWriterHelper.ValidateProperty(propertyName);
            ValidateFiniteUnlessBlock(value);

            WriteNumberEscape(propertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the property name and <see cref="double"/> value (as a YAML number) as part of a name/value pair of a YAML object.
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
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> (that is, 'G').
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, double value)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);
            ValidateFiniteUnlessBlock(value);

            WriteNumberEscape(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        private void WriteNumberEscape(ReadOnlySpan<char> propertyName, double value)
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

        private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, double value)
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

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, double value, int firstEscapeIndexProp)
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

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, double value, int firstEscapeIndexProp)
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

        private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, double value)
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

        private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, double value)
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

        private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, double value)
        {
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - YamlConstants.MaximumFormatDoubleLength - 4);

            // All ASCII, 2 quotes for property name, and 1 colon => escapedPropertyName.Length + YamlConstants.MaximumFormatDoubleLength + 3
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatDoubleLength + 4;

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

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, double value)
        {
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - YamlConstants.MaximumFormatDoubleLength - 4);

            int minRequired = escapedPropertyName.Length + YamlConstants.MaximumFormatDoubleLength + 3; // 2 quotes for property name, and 1 colon
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

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, double value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - indent - YamlConstants.MaximumFormatDoubleLength - 5 - _newLineLength);

            // All ASCII, 2 quotes for property name, 1 colon, and 1 space => escapedPropertyName.Length + YamlConstants.MaximumFormatDoubleLength + 4
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + YamlConstants.MaximumFormatDoubleLength + 5 + _newLineLength;

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

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, double value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - YamlConstants.MaximumFormatDoubleLength - 5 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + YamlConstants.MaximumFormatDoubleLength + 4; // 2 quotes for property name, 1 colon, and 1 space
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

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        internal unsafe void WritePropertyName(double value)
        {
            if (_style == WriterStyleBlock && !double.IsFinite(value))
            {
                // M5f: .nan, .inf and -.inf, as both libraries write them and as decision 8 writes the values.
                WritePlainPropertyName(double.IsNaN(value) ? ".nan"u8 : value > 0 ? ".inf"u8 : "-.inf"u8);
                return;
            }

            YamlWriterHelper.ValidateDouble(value);
            Span<byte> utf8PropertyName = stackalloc byte[YamlConstants.MaximumFormatDoubleLength];
            bool result = TryFormatDouble(value, utf8PropertyName, out int bytesWritten);
            Debug.Assert(result);
            WritePlainPropertyName(utf8PropertyName.Slice(0, bytesWritten));
        }
    }
}
