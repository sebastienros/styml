// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides a way to transform UTF-8 or UTF-16 encoded text into a form that is suitable for YAML.
    /// </summary>
    /// <remarks>
    /// This can be used to cache and store known strings used for writing YAML ahead of time by pre-encoding them up front.
    /// </remarks>
    public readonly struct YamlEncodedText : IEquatable<YamlEncodedText>
    {
        internal readonly byte[] _utf8Value;
        internal readonly string _value;

        // M4p candidate a: the block key decision, made once here (YamlWriterHelper.IsPlainBlockKeyInAnyWriter).
        internal readonly bool _plainBlockKey;

        /// <summary>
        /// Returns the UTF-8 encoded representation of the pre-encoded YAML text.
        /// </summary>
        public ReadOnlySpan<byte> EncodedUtf8Bytes => _utf8Value;

        /// <summary>
        /// Returns the UTF-16 encoded representation of the pre-encoded YAML text as a <see cref="string"/>.
        /// </summary>
        public string Value => _value ?? string.Empty;

        private YamlEncodedText(byte[] utf8Value)
        {
            Debug.Assert(utf8Value is not null);

            _value = YamlReaderHelper.GetTextFromUtf8(utf8Value);
            _utf8Value = utf8Value;
            _plainBlockKey = YamlWriterHelper.IsPlainBlockKeyInAnyWriter(utf8Value);
        }

        internal YamlWriterHelper.BlockScan BlockKeyScan =>
            _plainBlockKey ? YamlWriterHelper.BlockScan.PlainKey : YamlWriterHelper.BlockScan.Unknown;

        /// <summary>
        /// Encodes the string text value as a YAML string.
        /// </summary>
        /// <param name="value">The value to be transformed as YAML encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if value is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-16 characters.
        /// </exception>
        public static YamlEncodedText Encode(string value, JavaScriptEncoder? encoder = null)
        {
            ArgumentNullException.ThrowIfNull(value);

            return Encode(value.AsSpan(), encoder);
        }

        /// <summary>
        /// Encodes the text value as a YAML string.
        /// </summary>
        /// <param name="value">The value to be transformed as YAML encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-16 characters.
        /// </exception>
        public static YamlEncodedText Encode(ReadOnlySpan<char> value, JavaScriptEncoder? encoder = null)
        {
            if (value.Length == 0)
            {
                return new YamlEncodedText(Array.Empty<byte>());
            }

            return TranscodeAndEncode(value, encoder);
        }

        private static unsafe YamlEncodedText TranscodeAndEncode(ReadOnlySpan<char> value, JavaScriptEncoder? encoder)
        {
            YamlWriterHelper.ValidateValue(value);

            int expectedByteCount = YamlReaderHelper.GetUtf8ByteCount(value);

            byte[]? array = null;
            Span<byte> utf8Bytes = expectedByteCount <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (array = ArrayPool<byte>.Shared.Rent(expectedByteCount));

            // Since GetUtf8ByteCount above already throws on invalid input, the transcoding
            // to UTF-8 is guaranteed to succeed here. Therefore, there's no need for a try-catch-finally block.
            int actualByteCount = YamlReaderHelper.GetUtf8FromText(value, utf8Bytes);
            utf8Bytes = utf8Bytes.Slice(0, actualByteCount);
            Debug.Assert(expectedByteCount == utf8Bytes.Length);

            YamlEncodedText encodedText = EncodeHelper(utf8Bytes, encoder);

            if (array is not null)
            {
                // On the basis that this is user data, go ahead and clear it.
                utf8Bytes.Clear();
                ArrayPool<byte>.Shared.Return(array);
            }

            return encodedText;
        }

        /// <summary>
        /// Encodes the UTF-8 text value as a YAML string.
        /// </summary>
        /// <param name="utf8Value">The UTF-8 encoded value to be transformed as YAML encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-8 bytes.
        /// </exception>
        public static YamlEncodedText Encode(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder = null)
        {
            if (utf8Value.Length == 0)
            {
                return new YamlEncodedText(Array.Empty<byte>());
            }

            YamlWriterHelper.ValidateValue(utf8Value);
            return EncodeHelper(utf8Value, encoder);
        }

        private static YamlEncodedText EncodeHelper(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder)
        {
            int idx = YamlWriterHelper.NeedsEscaping(utf8Value, encoder);

            if (idx != -1)
            {
                return new YamlEncodedText(YamlHelpers.EscapeValue(utf8Value, idx, encoder));
            }
            else
            {
                return new YamlEncodedText(utf8Value.ToArray());
            }
        }

        /// <summary>
        /// Determines whether this instance and another specified <see cref="YamlEncodedText"/> instance have the same value.
        /// </summary>
        /// <remarks>
        /// Default instances of <see cref="YamlEncodedText"/> are treated as equal.
        /// </remarks>
        public bool Equals(YamlEncodedText other)
        {
            if (_value is null)
            {
                return other._value is null;
            }
            else
            {
                return _value.Equals(other._value);
            }
        }

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="YamlEncodedText"/> instance, have the same value.
        /// </summary>
        /// <remarks>
        /// If <paramref name="obj"/> is null, the method returns false.
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is YamlEncodedText encodedText)
            {
                return Equals(encodedText);
            }
            return false;
        }

        /// <summary>
        /// Converts the value of this instance to a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// Returns an empty string on a default instance of <see cref="YamlEncodedText"/>.
        /// </remarks>
        /// <returns>
        /// Returns the underlying UTF-16 encoded string.
        /// </returns>
        public override string ToString()
            => _value ?? string.Empty;

        /// <summary>
        /// Returns the hash code for this <see cref="YamlEncodedText"/>.
        /// </summary>
        /// <remarks>
        /// Returns 0 on a default instance of <see cref="YamlEncodedText"/>.
        /// </remarks>
        public override int GetHashCode()
            => _value?.GetHashCode() ?? 0;
    }
}
