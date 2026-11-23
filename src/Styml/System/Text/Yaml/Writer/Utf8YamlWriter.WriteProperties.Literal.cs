// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the pre-encoded property name and the YAML literal "null" as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteNull(YamlEncodedText propertyName)
        {
            WriteLiteralHelper(propertyName.EncodedUtf8Bytes, YamlConstants.NullValue, propertyName.BlockKeyScan);
            _tokenType = YamlTokenType.Null;
        }

        private void WriteLiteralHelper(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, YamlWriterHelper.BlockScan blockKeyScan = YamlWriterHelper.BlockScan.Unknown)
        {
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            WriteLiteralByOptions(utf8PropertyName, value, blockKeyScan);

            SetFlagToAddListSeparatorBeforeNextItem();
        }

        /// <summary>
        /// Writes the property name and the YAML literal "null" as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
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
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNull(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteNull(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the property name and the YAML literal "null" as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNull(ReadOnlySpan<char> propertyName)
        {
            YamlWriterHelper.ValidateProperty(propertyName);

            ReadOnlySpan<byte> span = YamlConstants.NullValue;

            WriteLiteralEscape(propertyName, span);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Null;
        }

        /// <summary>
        /// Writes the property name and the YAML literal "null" as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNull(ReadOnlySpan<byte> utf8PropertyName)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);

            ReadOnlySpan<byte> span = YamlConstants.NullValue;

            WriteLiteralEscape(utf8PropertyName, span);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Null;
        }

        /// <summary>
        /// Writes the pre-encoded property name and <see cref="bool"/> value (as a YAML literal "true" or "false") as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteBoolean(YamlEncodedText propertyName, bool value)
        {
            if (value)
            {
                WriteLiteralHelper(propertyName.EncodedUtf8Bytes, YamlConstants.TrueValue, propertyName.BlockKeyScan);
                _tokenType = YamlTokenType.True;
            }
            else
            {
                WriteLiteralHelper(propertyName.EncodedUtf8Bytes, YamlConstants.FalseValue, propertyName.BlockKeyScan);
                _tokenType = YamlTokenType.False;
            }
        }

        /// <summary>
        /// Writes the property name and <see cref="bool"/> value (as a YAML literal "true" or "false") as part of a name/value pair of a YAML object.
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
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteBoolean(string propertyName, bool value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteBoolean(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="bool"/> value (as a YAML literal "true" or "false") as part of a name/value pair of a YAML object.
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
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteBoolean(ReadOnlySpan<char> propertyName, bool value)
        {
            YamlWriterHelper.ValidateProperty(propertyName);

            ReadOnlySpan<byte> span = value ? YamlConstants.TrueValue : YamlConstants.FalseValue;

            WriteLiteralEscape(propertyName, span);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = value ? YamlTokenType.True : YamlTokenType.False;
        }

        /// <summary>
        /// Writes the property name and <see cref="bool"/> value (as a YAML literal "true" or "false") as part of a name/value pair of a YAML object.
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
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteBoolean(ReadOnlySpan<byte> utf8PropertyName, bool value)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);

            ReadOnlySpan<byte> span = value ? YamlConstants.TrueValue : YamlConstants.FalseValue;

            WriteLiteralEscape(utf8PropertyName, span);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = value ? YamlTokenType.True : YamlTokenType.False;
        }

        private void WriteLiteralEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteLiteralEscapeProperty(propertyName, value, propertyIdx);
            }
            else
            {
                WriteLiteralByOptions(propertyName, value);
            }
        }

        private void WriteLiteralEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteLiteralEscapeProperty(utf8PropertyName, value, propertyIdx);
            }
            else
            {
                WriteLiteralByOptions(utf8PropertyName, value);
            }
        }

        private unsafe void WriteLiteralEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteLiteralByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private unsafe void WriteLiteralEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteLiteralByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray is not null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteLiteralByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteLiteralMinimized(propertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteLiteralIndented(propertyName, value);
            }
            else
            {
                WriteLiteralBlock(propertyName, value);
            }
        }

        private void WriteLiteralByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, YamlWriterHelper.BlockScan blockKeyScan = YamlWriterHelper.BlockScan.Unknown)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteLiteralMinimized(utf8PropertyName, value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteLiteralIndented(utf8PropertyName, value);
            }
            else
            {
                WriteLiteralBlock(utf8PropertyName, value, blockKeyScan);
            }
        }

        private void WriteLiteralMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> value)
        {
            Debug.Assert(value.Length <= YamlConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - value.Length - 4);

            // All ASCII, 2 quotes for property name, and 1 colon => escapedPropertyName.Length + value.Length + 3
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + value.Length + 4;

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

            value.CopyTo(output.Slice(BytesPending));
            BytesPending += value.Length;
        }

        private void WriteLiteralMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> value)
        {
            Debug.Assert(value.Length <= YamlConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - value.Length - 4);

            int minRequired = escapedPropertyName.Length + value.Length + 3; // 2 quotes for property name, and 1 colon
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

            value.CopyTo(output.Slice(BytesPending));
            BytesPending += value.Length;
        }

        // AggressiveInlining used since this is only called from one location.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteLiteralSection(ReadOnlySpan<byte> escapedPropertyNameSection, ReadOnlySpan<byte> value)
        {
            Debug.Assert(value.Length <= YamlConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyNameSection.Length < int.MaxValue - value.Length - 1);

            int minRequired = escapedPropertyNameSection.Length + value.Length;
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

            escapedPropertyNameSection.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyNameSection.Length;

            value.CopyTo(output.Slice(BytesPending));
            BytesPending += value.Length;
        }

        private void WriteLiteralIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(value.Length <= YamlConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / YamlConstants.MaxExpansionFactorWhileTranscoding) - indent - value.Length - 5 - _newLineLength);

            // All ASCII, 2 quotes for property name, 1 colon, and 1 space => escapedPropertyName.Length + value.Length + 4
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + value.Length + 5 + _newLineLength;

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

            value.CopyTo(output.Slice(BytesPending));
            BytesPending += value.Length;
        }

        private void WriteLiteralIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(value.Length <= YamlConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - value.Length - 5 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + value.Length + 4; // 2 quotes for property name, 1 colon, and 1 space
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

            value.CopyTo(output.Slice(BytesPending));
            BytesPending += value.Length;
        }

        internal unsafe void WritePropertyName(bool value)
        {
            if (_style == WriterStyleBlock)
            {
                // M5f: the core schema's spelling, plain, as both libraries write it (Utf8Formatter gives "True").
                WritePlainPropertyName(value ? YamlConstants.TrueValue : YamlConstants.FalseValue);
                return;
            }

            Span<byte> utf8PropertyName = stackalloc byte[YamlConstants.MaximumFormatBooleanLength];

            bool result = Utf8Formatter.TryFormat(value, utf8PropertyName, out int bytesWritten);
            Debug.Assert(result);

            WritePropertyNameUnescaped(utf8PropertyName.Slice(0, bytesWritten));
        }
    }
}
