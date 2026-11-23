// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the property name and value (as a YAML number) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="utf8FormattedNumber">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="utf8FormattedNumber"/> does not represent a valid YAML number.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="long"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        internal void WriteNumber(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8FormattedNumber)
        {
            YamlWriterHelper.ValidateProperty(propertyName);
            YamlWriterHelper.ValidateValue(utf8FormattedNumber);
            YamlWriterHelper.ValidateNumber(utf8FormattedNumber);

            WriteNumberEscape(propertyName, utf8FormattedNumber);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        /// <summary>
        /// Writes the property name and value (as a YAML number) as part of a name/value pair of a YAML object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write..</param>
        /// <param name="utf8FormattedNumber">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="utf8FormattedNumber"/> does not represent a valid YAML number.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="long"/> using the default <see cref="StandardFormat"/> (that is, 'G'), for example: 32767.
        /// The property name is escaped before writing.
        /// </remarks>
        internal void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8FormattedNumber)
        {
            YamlWriterHelper.ValidateProperty(utf8PropertyName);
            YamlWriterHelper.ValidateValue(utf8FormattedNumber);
            YamlWriterHelper.ValidateNumber(utf8FormattedNumber);

            WriteNumberEscape(utf8PropertyName, utf8FormattedNumber);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        internal void WriteNumber(YamlEncodedText propertyName, ReadOnlySpan<byte> utf8FormattedNumber)
        {
            YamlWriterHelper.ValidateValue(utf8FormattedNumber);
            YamlWriterHelper.ValidateNumber(utf8FormattedNumber);

            WriteNumberByOptions(propertyName.EncodedUtf8Bytes, utf8FormattedNumber);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        private void WriteNumberEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
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

        private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
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

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
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

        private unsafe void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
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

        private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
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
                WriteNumberBlock(propertyName, value);
            }
        }

        private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
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
                WriteNumberBlock(utf8PropertyName, value);
            }
        }
    }
}
