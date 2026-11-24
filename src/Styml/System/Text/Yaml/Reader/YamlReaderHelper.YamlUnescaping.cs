// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    // Turns the raw bytes of a YAML scalar (Utf8YamlReader.ValueSpan) into its value: line folding, '' and
    // backslash escapes, block scalar indentation, chomping and folding.
    internal static partial class YamlReaderHelper
    {
        /// <summary>
        /// An upper bound of the processed length: only the \L and \P escapes grow (2 bytes to 3).
        /// </summary>
        internal static int GetMaxYamlProcessedLength(int rawLength) => rawLength + (rawLength / 2) + 1;

        internal static byte[] ProcessYamlScalar(ReadOnlySpan<byte> raw, YamlScalarStyle style, int blockIndent, byte chomping)
        {
            byte[]? rented = null;
            Span<byte> buffer = raw.Length <= 128
                ? stackalloc byte[GetMaxYamlProcessedLength(128)]
                : (rented = ArrayPool<byte>.Shared.Rent(GetMaxYamlProcessedLength(raw.Length)));

            int written = ProcessYamlScalar(raw, buffer, style, blockIndent, chomping);
            byte[] result = buffer.Slice(0, written).ToArray();

            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
            return result;
        }

        /// <returns>The number of bytes written to <paramref name="destination"/>, which must hold
        /// <see cref="GetMaxYamlProcessedLength"/> bytes.</returns>
        internal static int ProcessYamlScalar(ReadOnlySpan<byte> raw, Span<byte> destination, YamlScalarStyle style, int blockIndent, byte chomping)
        {
            switch (style)
            {
                case YamlScalarStyle.Literal:
                case YamlScalarStyle.Folded:
                    return ProcessBlockScalar(raw, destination, style == YamlScalarStyle.Folded, blockIndent, chomping);
                case YamlScalarStyle.Plain:
                    return ProcessFlowScalar(raw, destination, singleQuoted: false, doubleQuoted: false);
                case YamlScalarStyle.SingleQuoted:
                    return ProcessFlowScalar(raw, destination, singleQuoted: true, doubleQuoted: false);
                default:
                    Debug.Assert(style == YamlScalarStyle.DoubleQuoted);
                    return ProcessFlowScalar(raw, destination, singleQuoted: false, doubleQuoted: true);
            }
        }

        private static bool IsYamlBreak(byte b) => b is (byte)'\n' or (byte)'\r';

        private static bool IsYamlBlank(byte b) => b is (byte)' ' or (byte)'\t';

        private static int SkipYamlBreak(ReadOnlySpan<byte> raw, int i)
        {
            Debug.Assert(IsYamlBreak(raw[i]));
            return raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n' ? i + 2 : i + 1;
        }

        /// <summary>Plain, single-quoted and double-quoted scalars: line folding plus escapes.</summary>
        private static int ProcessFlowScalar(ReadOnlySpan<byte> raw, Span<byte> dest, bool singleQuoted, bool doubleQuoted)
        {
            int w = 0;
            int i = 0;
            int lineContentEnd = 0; // w just after the last non-blank byte written on the current line

            while (i < raw.Length)
            {
                byte b = raw[i];

                if (IsYamlBreak(b))
                {
                    // Fold: drop trailing blanks of this line and leading blanks of the next ones.
                    w = lineContentEnd;
                    i = SkipYamlBreak(raw, i);
                    int empties = 0;
                    while (true)
                    {
                        while (i < raw.Length && IsYamlBlank(raw[i]))
                        {
                            i++;
                        }
                        if (i < raw.Length && IsYamlBreak(raw[i]))
                        {
                            empties++;
                            i = SkipYamlBreak(raw, i);
                            continue;
                        }
                        break;
                    }

                    if (empties == 0)
                    {
                        dest[w++] = (byte)' ';
                    }
                    else
                    {
                        for (int k = 0; k < empties; k++)
                        {
                            dest[w++] = (byte)'\n';
                        }
                    }
                    lineContentEnd = w;
                    continue;
                }

                if (singleQuoted && b == (byte)'\'' && i + 1 < raw.Length && raw[i + 1] == (byte)'\'')
                {
                    dest[w++] = (byte)'\'';
                    i += 2;
                    lineContentEnd = w;
                    continue;
                }

                if (doubleQuoted && b == (byte)'\\' && i + 1 < raw.Length)
                {
                    byte e = raw[i + 1];
                    if (IsYamlBreak(e))
                    {
                        // An escaped line break: no folding space, blanks before the backslash are kept, the next
                        // line's leading blanks are dropped, and each following empty line is a line feed.
                        i = SkipYamlBreak(raw, i + 1);
                        while (true)
                        {
                            while (i < raw.Length && IsYamlBlank(raw[i]))
                            {
                                i++;
                            }
                            if (i < raw.Length && IsYamlBreak(raw[i]))
                            {
                                dest[w++] = (byte)'\n';
                                i = SkipYamlBreak(raw, i);
                                continue;
                            }
                            break;
                        }
                        lineContentEnd = w;
                        continue;
                    }

                    i += 2;
                    switch (e)
                    {
                        case (byte)'0': dest[w++] = 0x00; break;
                        case (byte)'a': dest[w++] = 0x07; break;
                        case (byte)'b': dest[w++] = 0x08; break;
                        case (byte)'t':
                        case (byte)'\t': dest[w++] = 0x09; break;
                        case (byte)'n': dest[w++] = 0x0A; break;
                        case (byte)'v': dest[w++] = 0x0B; break;
                        case (byte)'f': dest[w++] = 0x0C; break;
                        case (byte)'r': dest[w++] = 0x0D; break;
                        case (byte)'e': dest[w++] = 0x1B; break;
                        case (byte)' ': dest[w++] = (byte)' '; break;
                        case (byte)'"': dest[w++] = (byte)'"'; break;
                        case (byte)'/': dest[w++] = (byte)'/'; break;
                        case (byte)'\\': dest[w++] = (byte)'\\'; break;
                        case (byte)'N': w += WriteUtf8(0x85, dest.Slice(w)); break;
                        case (byte)'_': w += WriteUtf8(0xA0, dest.Slice(w)); break;
                        case (byte)'L': w += WriteUtf8(0x2028, dest.Slice(w)); break;
                        case (byte)'P': w += WriteUtf8(0x2029, dest.Slice(w)); break;
                        case (byte)'x': w += WriteUtf8(ParseHex(raw.Slice(i, 2)), dest.Slice(w)); i += 2; break;
                        case (byte)'U': w += WriteUtf8(ParseHex(raw.Slice(i, 8)), dest.Slice(w)); i += 8; break;
                        case (byte)'u':
                            {
                                int codePoint = ParseHex(raw.Slice(i, 4));
                                i += 4;
                                if (codePoint is >= 0xD800 and <= 0xDBFF
                                    && i + 6 <= raw.Length && raw[i] == (byte)'\\' && raw[i + 1] == (byte)'u')
                                {
                                    int low = ParseHex(raw.Slice(i + 2, 4));
                                    if (low is >= 0xDC00 and <= 0xDFFF)
                                    {
                                        codePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (low - 0xDC00);
                                        i += 6;
                                    }
                                }
                                // WriteUtf8 rejects a lone surrogate.
                                w += WriteUtf8(codePoint, dest.Slice(w));
                                break;
                            }
                        default:
                            Debug.Fail("Escapes are validated by the reader.");
                            break;
                    }
                    lineContentEnd = w;
                    continue;
                }

                dest[w++] = b;
                i++;
                if (!IsYamlBlank(b))
                {
                    lineContentEnd = w;
                }
            }

            return w;
        }

        /// <summary>Literal and folded block scalars (the algorithm of libyaml's scan_block_scalar).</summary>
        private static int ProcessBlockScalar(ReadOnlySpan<byte> raw, Span<byte> dest, bool folded, int indent, byte chomping)
        {
            int w = 0;
            int i = 0;

            // Leading empty lines.
            int trailingBreaks = CountEmptyLines(raw, ref i, indent);
            bool hasLeadingBreak = false;
            bool leadingBlank = false;

            while (i < raw.Length)
            {
                // A content line: strip the indentation.
                int skipped = 0;
                while (skipped < indent && i < raw.Length && raw[i] == (byte)' ')
                {
                    i++;
                    skipped++;
                }

                bool trailingBlank = i < raw.Length && IsYamlBlank(raw[i]);
                if (folded && hasLeadingBreak && !leadingBlank && !trailingBlank)
                {
                    if (trailingBreaks == 0)
                    {
                        dest[w++] = (byte)' ';
                    }
                }
                else if (hasLeadingBreak)
                {
                    dest[w++] = (byte)'\n';
                }

                for (int k = 0; k < trailingBreaks; k++)
                {
                    dest[w++] = (byte)'\n';
                }
                trailingBreaks = 0;
                hasLeadingBreak = false;
                leadingBlank = trailingBlank;

                while (i < raw.Length && !IsYamlBreak(raw[i]))
                {
                    dest[w++] = raw[i++];
                }

                if (i >= raw.Length)
                {
                    // The end of the input ends the last line like a line break.
                    hasLeadingBreak = true;
                    break;
                }

                i = SkipYamlBreak(raw, i);
                hasLeadingBreak = true;
                trailingBreaks = CountEmptyLines(raw, ref i, indent);
            }

            // Chomping: clip and keep add the final line break, keep also the trailing empty lines.
            if (chomping != 1 && hasLeadingBreak)
            {
                dest[w++] = (byte)'\n';
            }
            if (chomping == 2)
            {
                for (int k = 0; k < trailingBreaks; k++)
                {
                    dest[w++] = (byte)'\n';
                }
            }

            return w;
        }

        /// <summary>Counts lines that hold at most <paramref name="indent"/> spaces and a line break.</summary>
        private static int CountEmptyLines(ReadOnlySpan<byte> raw, ref int i, int indent)
        {
            int count = 0;
            while (i < raw.Length)
            {
                int j = i;
                int spaces = 0;
                while (j < raw.Length && raw[j] == (byte)' ' && spaces < indent)
                {
                    j++;
                    spaces++;
                }

                if (j < raw.Length && IsYamlBreak(raw[j]))
                {
                    count++;
                    i = SkipYamlBreak(raw, j);
                    continue;
                }

                if (j >= raw.Length)
                {
                    // Spaces at the very end: an empty last line (the end of the input ends it like a line break).
                    if (j > i)
                    {
                        count++;
                    }
                    i = j;
                }
                break;
            }
            return count;
        }

        internal static int ParseHex(ReadOnlySpan<byte> hex)
        {
            int value = 0;
            foreach (byte b in hex)
            {
                value <<= 4;
                value |= b <= '9' ? b - '0' : (b | 0x20) - 'a' + 10;
            }
            return value;
        }

        private static int WriteUtf8(int codePoint, Span<byte> dest)
        {
            if ((uint)codePoint > 0x10FFFF || (codePoint is >= 0xD800 and <= 0xDFFF))
            {
                ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(codePoint);
            }
            if (codePoint < 0x80)
            {
                dest[0] = (byte)codePoint;
                return 1;
            }
            if (codePoint < 0x800)
            {
                dest[0] = (byte)(0xC0 | (codePoint >> 6));
                dest[1] = (byte)(0x80 | (codePoint & 0x3F));
                return 2;
            }
            if (codePoint < 0x10000)
            {
                dest[0] = (byte)(0xE0 | (codePoint >> 12));
                dest[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                dest[2] = (byte)(0x80 | (codePoint & 0x3F));
                return 3;
            }
            dest[0] = (byte)(0xF0 | (codePoint >> 18));
            dest[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
            dest[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            dest[3] = (byte)(0x80 | (codePoint & 0x3F));
            return 4;
        }
    }
}
