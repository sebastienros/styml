// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace System.Text.Yaml
{
    internal static partial class YamlHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetEscapedPropertyNameSection(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder)
        {
            int idx = YamlWriterHelper.NeedsEscaping(utf8Value, encoder);

            if (idx != -1)
            {
                return GetEscapedPropertyNameSection(utf8Value, idx, encoder);
            }
            else
            {
                return GetPropertyNameSection(utf8Value);
            }
        }

        /// <summary>
        /// The block form of a property name section: <c>name:</c>, or
        /// <c>"na\u0026me":</c> escaped with the YAML set of 5.1 (and the encoder's set, when there is one). Null when the
        /// name needs an explicit key; the writer then writes it from its UTF-8 bytes.
        /// </summary>
        public static byte[]? GetBlockPropertyNameSection(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder, bool lenient)
        {
            bool plain = YamlWriterHelper.IsBlockPlainKey(utf8Value, lenient)
                && (encoder is null || YamlWriterHelper.NeedsEscaping(utf8Value, encoder) < 0);
            if (plain)
            {
                if (YamlWriterHelper.NeedsExplicitKey(utf8Value.Length, utf8Value))
                {
                    return null;
                }

                byte[] section = new byte[utf8Value.Length + 1];
                utf8Value.CopyTo(section);
                section[utf8Value.Length] = YamlConstants.KeyValueSeparator;
                return section;
            }

            byte[] escaped = new byte[YamlWriterHelper.GetMaxBlockEscapedLength(utf8Value.Length) + 3];
            escaped[0] = YamlConstants.Quote;
            int written = YamlWriterHelper.EscapeBlockDoubleQuoted(utf8Value, escaped.AsSpan(1), encoder);
            if (YamlWriterHelper.NeedsExplicitKey(written + 2, escaped.AsSpan(1, written)))
            {
                return null;
            }

            escaped[written + 1] = YamlConstants.Quote;
            escaped[written + 2] = YamlConstants.KeyValueSeparator;
            return escaped.AsSpan(0, written + 3).ToArray();
        }

        public static unsafe byte[] EscapeValue(
            ReadOnlySpan<byte> utf8Value,
            int firstEscapeIndexVal,
            JavaScriptEncoder? encoder)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);

            byte[]? valueArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

            Span<byte> escapedValue = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (valueArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, encoder, out int written);

            byte[] escapedString = escapedValue.Slice(0, written).ToArray();

            if (valueArray is not null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }

            return escapedString;
        }

        private static unsafe byte[] GetEscapedPropertyNameSection(
            ReadOnlySpan<byte> utf8Value,
            int firstEscapeIndexVal,
            JavaScriptEncoder? encoder)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);

            byte[]? valueArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

            Span<byte> escapedValue = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (valueArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, encoder, out int written);

            byte[] propertySection = GetPropertyNameSection(escapedValue.Slice(0, written));

            if (valueArray is not null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }

            return propertySection;
        }

        private static byte[] GetPropertyNameSection(ReadOnlySpan<byte> utf8Value)
        {
            int length = utf8Value.Length;
            byte[] propertySection = new byte[length + 3];

            propertySection[0] = YamlConstants.Quote;
            utf8Value.CopyTo(propertySection.AsSpan(1, length));
            propertySection[++length] = YamlConstants.Quote;
            propertySection[++length] = YamlConstants.KeyValueSeparator;

            return propertySection;
        }
    }
}
