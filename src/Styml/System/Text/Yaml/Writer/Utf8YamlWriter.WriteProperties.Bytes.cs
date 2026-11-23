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
        /// Writes the pre-encoded property name and raw bytes value (as a Base64 encoded YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <param name="bytes">The binary data to write as Base64 encoded text.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteBase64String(YamlEncodedText propertyName, ReadOnlySpan<byte> bytes)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            WriteBase64ByOptions(utf8PropertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        /// <summary>
        /// Writes the property name and raw bytes value (as a Base64 encoded YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="bytes">The binary data to write as Base64 encoded text.</param>
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
        public void WriteBase64String(string propertyName, ReadOnlySpan<byte> bytes)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteBase64String(propertyName.AsSpan(), bytes);
        }

        /// <summary>
        /// Writes the property name and raw bytes value (as a Base64 encoded YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="bytes">The binary data to write as Base64 encoded text.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteBase64String(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            YamlWriterHelper.ValidatePropertyNameLength(propertyName);

            WriteBase64Escape(propertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        /// <summary>
        /// Writes the property name and raw bytes value (as a Base64 encoded YAML string) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="bytes">The binary data to write as Base64 encoded text.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteBase64String(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            YamlWriterHelper.ValidatePropertyNameLength(utf8PropertyName);

            WriteBase64Escape(utf8PropertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.String;
        }

        private void WriteBase64Escape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteBase64EscapeProperty(propertyName, bytes, propertyIdx);
            }
            else
            {
                WriteBase64ByOptions(propertyName, bytes);
            }
        }

        private void WriteBase64Escape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteBase64EscapeProperty(utf8PropertyName, bytes, propertyIdx);
            }
            else
            {
                WriteBase64ByOptions(utf8PropertyName, bytes);
            }
        }

        private unsafe void WriteBase64EscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteBase64ByOptions(escapedPropertyName.Slice(0, written), bytes);

            if (propertyArray is not null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private unsafe void WriteBase64EscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteBase64ByOptions(escapedPropertyName.Slice(0, written), bytes);

            if (propertyArray is not null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteBase64ByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteBase64Minimized(propertyName, bytes);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteBase64Indented(propertyName, bytes);
            }
            else
            {
                WriteBase64Block(propertyName, bytes);
            }
        }

        private void WriteBase64ByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            ValidateWritingProperty();
            if (_style == WriterStyleJsonMinimized)
            {
                WriteBase64Minimized(utf8PropertyName, bytes);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteBase64Indented(utf8PropertyName, bytes);
            }
            else
            {
                WriteBase64Block(utf8PropertyName, bytes);
            }
        }

        private void WriteBase64Minimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - encodedLength - 6);

            // All ASCII, 2 quotes for property name, 2 quotes to surround the base-64 encoded string value, and 1 colon => escapedPropertyName.Length + encodedLength + 5
            // Optionally, 1 list separator, and up to 3x growth when transcoding.
            int maxRequired = (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + encodedLength + 6;

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

            Base64EncodeAndWrite(bytes, output);

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteBase64Minimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - encodedLength - 6);

            // 2 quotes for property name, 2 quotes to surround the base-64 encoded string value, and 1 colon => escapedPropertyName.Length + encodedLength + 5
            // Optionally, 1 list separator.
            int maxRequired = escapedPropertyName.Length + encodedLength + 6;

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

            Base64EncodeAndWrite(bytes, output);

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteBase64Indented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - indent - encodedLength - 7 - _newLineLength);

            // All ASCII, 2 quotes for property name, 2 quotes to surround the base-64 encoded string value, 1 colon, and 1 space => indent + escapedPropertyName.Length + encodedLength + 6
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding.
            int maxRequired = indent + (escapedPropertyName.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) + encodedLength + 7 + _newLineLength;

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

            Base64EncodeAndWrite(bytes, output);

            output[BytesPending++] = YamlConstants.Quote;
        }

        private void WriteBase64Indented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - encodedLength - 7 - _newLineLength);

            // 2 quotes for property name, 2 quotes to surround the base-64 encoded string value, 1 colon, and 1 space => indent + escapedPropertyName.Length + encodedLength + 6
            // Optionally, 1 list separator, and 1-2 bytes for new line.
            int maxRequired = indent + escapedPropertyName.Length + encodedLength + 7 + _newLineLength;

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

            Base64EncodeAndWrite(bytes, output);

            output[BytesPending++] = YamlConstants.Quote;
        }
    }
}
