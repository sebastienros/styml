// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;
#if NET
using System.Text.Unicode;
#endif

namespace System.Text.Yaml
{
    /// <summary>
    /// The encoders a block-style writer escapes with when no encoder is set (decision 5;
    /// M4b). The escaping scan before each dispatch point is shared with the JSON-compatible style and takes the
    /// options' encoder; with this one in place of the default, non-ASCII text reaches the block body as it is, with no
    /// escape to undo (M4a's acceptance, item 4). It escapes the YAML set (C0 controls, DEL, C1 controls,
    /// LS, PS, U+FEFF, U+FFFE, U+FFFF) and the two characters an escape needs, <c>"</c> and <c>\</c>, so that a
    /// backslash in what the block body receives is always an escape. Escapes are JSON's (<c>\n</c>, <c>\"</c>,
    /// <c>\u0085</c>), which the reader's unescaper reads. Invalid UTF-8 and UTF-16 are reported as the default encoder
    /// reports them.
    /// </summary>
    internal sealed class YamlBlockEncoder : JavaScriptEncoder
    {
        /// <summary>
        /// The encoder of the shared escaping scan: it escapes only <c>"</c> and <c>\</c> (and reports invalid UTF-8),
        /// since the block body judges and escapes everything else itself. A backslash in what the body receives is
        /// then always an escape, and nothing else is escaped on the way.
        /// </summary>
        public static readonly YamlBlockEncoder Instance = new(full: false);

        /// <summary>The YAML set of 5.1 as well, for strings written in segments, which go straight between quotes.</summary>
        public static readonly YamlBlockEncoder Full = new(full: true);

        private readonly bool _full;

        private YamlBlockEncoder(bool full)
        {
            _full = full;
        }

        public override int MaxOutputCharactersPerInputCharacter => 12; // a surrogate pair as two \uXXXX

        public override bool WillEncode(int unicodeScalar) =>
            unicodeScalar is (int)'"' or (int)'\\'
            || (_full && (unicodeScalar < 0x20 || unicodeScalar == 0x7F || (unicodeScalar >= 0x80 && unicodeScalar <= 0x9F)
                || unicodeScalar is 0x2028 or 0x2029 or 0xFEFF or 0xFFFE or 0xFFFF));

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            Debug.Assert(unicodeScalar <= 0xFFFF || !WillEncode(unicodeScalar));
            char shortForm = unicodeScalar switch
            {
                (int)'"' => '"',
                (int)'\\' => '\\',
                (int)'\n' => 'n',
                (int)'\r' => 'r',
                (int)'\t' => 't',
                (int)'\b' => 'b',
                (int)'\f' => 'f',
                _ => '\0',
            };

            if (!WillEncode(unicodeScalar))
            {
                // A scalar that needs no escape is written as it is.
                if (unicodeScalar > 0xFFFF)
                {
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }

                    int v = unicodeScalar - 0x10000;
                    buffer[0] = (char)(0xD800 + (v >> 10));
                    buffer[1] = (char)(0xDC00 + (v & 0x3FF));
                    numberOfCharactersWritten = 2;
                    return true;
                }

                if (bufferLength < 1)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                buffer[0] = (char)unicodeScalar;
                numberOfCharactersWritten = 1;
                return true;
            }

            if (shortForm != '\0')
            {
                if (bufferLength < 2)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                buffer[0] = '\\';
                buffer[1] = shortForm;
                numberOfCharactersWritten = 2;
                return true;
            }

            if (bufferLength < 6)
            {
                numberOfCharactersWritten = 0;
                return false;
            }

            buffer[0] = '\\';
            buffer[1] = 'u';
            buffer[2] = Hex(unicodeScalar >> 12);
            buffer[3] = Hex(unicodeScalar >> 8);
            buffer[4] = Hex(unicodeScalar >> 4);
            buffer[5] = Hex(unicodeScalar);
            numberOfCharactersWritten = 6;
            return true;
        }

        private static char Hex(int value)
        {
            value &= 0xF;
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }

#if NET
        // The characters to look closer at: those WillEncode names, and the surrogates (to find unpaired ones).
        private static readonly SearchValues<char> s_charStop = SearchValues.Create(CharStop());

        private static string CharStop()
        {
            var chars = new StringBuilder();
            for (int c = 0; c < 0x20; c++)
            {
                chars.Append((char)c);
            }

            chars.Append('"').Append('\\').Append('\x7F');
            for (int c = 0x80; c <= 0x9F; c++)
            {
                chars.Append((char)c);
            }

            for (int c = 0xD800; c <= 0xDFFF; c++)
            {
                chars.Append((char)c);
            }

            chars.Append('\u2028').Append('\u2029').Append('\uFEFF').Append('\uFFFE').Append('\uFFFF');
            return chars.ToString();
        }

        // ASCII without '"' and '\\': what Instance never escapes, for its first pass.
        private static readonly SearchValues<byte> s_asciiBytesExceptEscapes = SearchValues.Create(AsciiExceptEscapes());
        private static readonly SearchValues<char> s_asciiCharsExceptEscapes = SearchValues.Create(Encoding.ASCII.GetString(AsciiExceptEscapes()));

        private static byte[] AsciiExceptEscapes()
        {
            var bytes = new List<byte>();
            for (int b = 0; b < 0x80; b++)
            {
                if (b is not ((int)'"' or (int)'\\'))
                {
                    bytes.Add((byte)b);
                }
            }

            return bytes.ToArray();
        }

        private static readonly SearchValues<byte> s_byteStop = SearchValues.Create(
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            (byte)'"', (byte)'\\', 0x7F, 0xC2, 0xE2, 0xEF,
        ]);
#endif

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            var span = new ReadOnlySpan<char>(text, textLength);
            if (!_full)
            {
                // Unpaired surrogates are left to the block body, which transcodes and replaces them. ASCII text other
                // than the two takes one vectorized pass.
#if NET
                int first = span.IndexOfAnyExcept(s_asciiCharsExceptEscapes);
                if (first < 0 || span[first] < 0x80)
                {
                    return first;
                }

                int k = span.Slice(first).IndexOfAny('"', '\\');
                return k < 0 ? -1 : first + k;
#else
                return span.IndexOfAny('"', '\\');
#endif
            }

            int i = 0;
            while (i < span.Length)
            {
#if NET
                int k = span.Slice(i).IndexOfAny(s_charStop);
                if (k < 0)
                {
                    return -1;
                }

                i += k;
#endif
                char c = span[i];
                if (char.IsHighSurrogate(c) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
                {
                    i += 2;
                    continue;
                }

                if (char.IsSurrogate(c) || WillEncode(c))
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// The scan of <see cref="Instance"/> on UTF-8 from its first non-ASCII byte: the index of the first <c>"</c>,
        /// <c>\</c> or byte of invalid UTF-8, or -1. (The shared escaping scan reaches it through the virtual call, which
        /// keeps that scan's body as M4a had it, inlined in the JSON-compatible callers.)
        /// </summary>
        public static int FindFirstToEscapeUtf8(ReadOnlySpan<byte> utf8Text)
        {
            // The text before the first quote is checked for validity, which is one vectorized pass on valid text.
            int quote = utf8Text.IndexOfAny((byte)'"', (byte)'\\');
            int invalid = FirstInvalidUtf8(quote < 0 ? utf8Text : utf8Text.Slice(0, quote));
            return invalid >= 0 ? invalid : quote;
        }

        public override int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
        {
            if (!_full)
            {
#if NET
                // ASCII text other than the two takes one vectorized pass.
                int first = utf8Text.IndexOfAnyExcept(s_asciiBytesExceptEscapes);
                if (first < 0 || utf8Text[first] < 0x80)
                {
                    return first;
                }

                int k = FindFirstToEscapeUtf8(utf8Text.Slice(first));
                return k < 0 ? -1 : first + k;
#else
                return FindFirstToEscapeUtf8(utf8Text);
#endif
            }

            int invalid = FirstInvalidUtf8(utf8Text);

            ReadOnlySpan<byte> valid = invalid < 0 ? utf8Text : utf8Text.Slice(0, invalid);
            int i = 0;
            while (i < valid.Length)
            {
#if NET
                int k = valid.Slice(i).IndexOfAny(s_byteStop);
                if (k < 0)
                {
                    break;
                }

                i += k;
#endif
                byte b = valid[i];
                if (b < 0x80)
                {
                    if (b < 0x20 || b is (byte)'"' or (byte)'\\' or 0x7F)
                    {
                        return i;
                    }

                    i++;
                    continue;
                }

                int length = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : b >= 0xC0 ? 2 : 1;
                if (length > 1 && i + length <= valid.Length && WillEncode(Decode(valid.Slice(i, length))))
                {
                    return i;
                }

                i += length;
            }

            return invalid;
        }

        private static int Decode(ReadOnlySpan<byte> s) => s.Length switch
        {
            2 => ((s[0] & 0x1F) << 6) | (s[1] & 0x3F),
            3 => ((s[0] & 0x0F) << 12) | ((s[1] & 0x3F) << 6) | (s[2] & 0x3F),
            _ => ((s[0] & 0x07) << 18) | ((s[1] & 0x3F) << 12) | ((s[2] & 0x3F) << 6) | (s[3] & 0x3F),
        };

        // The index of the first byte that does not begin a valid UTF-8 sequence, or -1.
        private static int FirstInvalidUtf8(ReadOnlySpan<byte> s)
        {
#if NET
            if (Utf8.IsValid(s))
            {
                return -1;
            }
#endif
            int i = 0;
            while (i < s.Length)
            {
                OperationStatus status = Rune.DecodeFromUtf8(s.Slice(i), out _, out int consumed);
                if (status != OperationStatus.Done)
                {
                    return i;
                }

                i += consumed;
            }

            return -1;
        }
    }
}
