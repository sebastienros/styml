// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    // Byte scanning and core-schema helpers for the YAML grammar. Scanning lives here (one place, with a NET path and
    // a scalar fallback) so that it can be vectorised later without touching the grammar code.
    internal static partial class YamlReaderHelper
    {
#if NET
        private static readonly SearchValues<byte> s_collectionKeyProbe = SearchValues.Create("[]{}\"'\n\r#"u8);
        private static readonly SearchValues<byte> s_doubleQuotedProbe = SearchValues.Create("\"\\\n\r"u8);

        private static readonly SearchValues<byte> s_closeBracketOrLineBreak = SearchValues.Create("]}\n\r"u8);

        /// <summary>Index of the next ']', '}', LF or CR, or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfCloseBracketOrLineBreak(ReadOnlySpan<byte> span) => span.IndexOfAny(s_closeBracketOrLineBreak);

        /// <summary>Index of the next byte the collection-key probe acts on (brackets, quotes, line breaks, '#'), or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfCollectionKeyProbeByte(ReadOnlySpan<byte> span) => span.IndexOfAny(s_collectionKeyProbe);

        /// <summary>Index of the next '"', '\\' or line break, or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfDoubleQuotedProbeByte(ReadOnlySpan<byte> span) => span.IndexOfAny(s_doubleQuotedProbe);
#else
        internal static int IndexOfCloseBracketOrLineBreak(ReadOnlySpan<byte> span) => span.IndexOfAny("]}\n\r"u8);

        internal static int IndexOfCollectionKeyProbeByte(ReadOnlySpan<byte> span) => span.IndexOfAny("[]{}\"'\n\r#"u8);

        internal static int IndexOfDoubleQuotedProbeByte(ReadOnlySpan<byte> span) => span.IndexOfAny("\"\\\n\r"u8);
#endif

        // M7d (item 1d): the bytes of a simple block key, the common case the key checks are made for. 1: an ASCII letter,
        // digit or '_', which may start one; 2: '-', '.' or '/', which may follow. A key made only of these, followed by
        // ':' and white space, needs none of the other checks (properties, an alias, quotes, an explicit or collection
        // key, a plain scalar's start rules, a space inside it).
        private static ReadOnlySpan<byte> SimpleKeyClass =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
            0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1,
            0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsSimpleKeyStart(byte b) => SimpleKeyClass[b] == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsSimpleKeyByte(byte b) => SimpleKeyClass[b] != 0;

        /// <summary>Index of the next '\'' or line break, or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfSingleQuotedProbeByte(ReadOnlySpan<byte> span) => span.IndexOfAny((byte)'\'', (byte)'\n', (byte)'\r');

        /// <summary>The number of UTF-8 characters (bytes that are not continuation bytes) in <paramref name="utf8"/>.</summary>
        internal static int CountCharacters(ReadOnlySpan<byte> utf8)
        {
#if NET
            if (System.Text.Ascii.IsValid(utf8))
            {
                return utf8.Length;
            }
#endif
            int count = 0;
            foreach (byte b in utf8)
            {
                if ((b & 0xC0) != 0x80)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Index of the first LF or CR, or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfLineBreak(ReadOnlySpan<byte> span) => span.IndexOfAny((byte)'\n', (byte)'\r');

        /// <summary>Index of the first space or tab, or -1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfBlank(ReadOnlySpan<byte> span) => span.IndexOfAny((byte)' ', (byte)'\t');

        /// <summary>Index of a '#' that starts a comment (preceded by a blank), or -1.</summary>
        internal static int IndexOfComment(ReadOnlySpan<byte> span)
        {
            for (int i = 1; i < span.Length; i++)
            {
                if (span[i] == (byte)'#' && span[i - 1] is (byte)' ' or (byte)'\t')
                {
                    return i;
                }
            }
            return -1;
        }

        internal static ReadOnlySpan<byte> TrimBlanks(ReadOnlySpan<byte> span)
        {
            int start = 0;
            while (start < span.Length && span[start] is (byte)' ' or (byte)'\t')
            {
                start++;
            }
            int end = span.Length;
            while (end > start && span[end - 1] is (byte)' ' or (byte)'\t')
            {
                end--;
            }
            return span.Slice(start, end - start);
        }

        internal static bool AllDigits(ReadOnlySpan<byte> span)
        {
            if (span.IsEmpty)
            {
                return false;
            }
            foreach (byte b in span)
            {
                if (b is < (byte)'0' or > (byte)'9')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>RFC 8259 number syntax: <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][-+]?[0-9]+)?</c>.</summary>
        internal static bool IsJsonNumber(ReadOnlySpan<byte> value)
        {
            int i = 0;
            if (i < value.Length && value[i] == (byte)'-')
            {
                i++;
            }
            if (i >= value.Length)
            {
                return false;
            }
            if (value[i] == (byte)'0')
            {
                i++;
            }
            else if (value[i] is >= (byte)'1' and <= (byte)'9')
            {
                while (i < value.Length && value[i] is >= (byte)'0' and <= (byte)'9')
                {
                    i++;
                }
            }
            else
            {
                return false;
            }
            if (i < value.Length && value[i] == (byte)'.')
            {
                i++;
                int start = i;
                while (i < value.Length && value[i] is >= (byte)'0' and <= (byte)'9')
                {
                    i++;
                }
                if (i == start)
                {
                    return false;
                }
            }
            if (i < value.Length && value[i] is (byte)'e' or (byte)'E')
            {
                i++;
                if (i < value.Length && value[i] is (byte)'-' or (byte)'+')
                {
                    i++;
                }
                int start = i;
                while (i < value.Length && value[i] is >= (byte)'0' and <= (byte)'9')
                {
                    i++;
                }
                if (i == start)
                {
                    return false;
                }
            }
            return i == value.Length;
        }

        // ---------------------------------------------------------------------------------------------------------
        // YAML 1.2 core schema

        internal static YamlTokenType ResolveYamlPlainScalar(ReadOnlySpan<byte> value, bool yaml11)
        {
            if (IsYamlNull(value))
            {
                return YamlTokenType.Null;
            }
            if (TryGetYamlBoolean(value, yaml11, out bool b))
            {
                return b ? YamlTokenType.True : YamlTokenType.False;
            }
            if (IsYamlInteger(value, yaml11) || IsYamlFloat(value, yaml11))
            {
                return YamlTokenType.Number;
            }
            return YamlTokenType.String;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetYamlBoolean(ReadOnlySpan<byte> value, bool yaml11, out bool result) =>
            TryGetYamlBoolean(value, out result) || (yaml11 && TryGetYaml11Boolean(value, out result));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsYamlInteger(ReadOnlySpan<byte> value, bool yaml11) =>
            IsYamlInteger(value) || (yaml11 && TryParseYaml11Integer(value, out _, out _, out bool matched) | matched);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsYamlFloat(ReadOnlySpan<byte> value, bool yaml11) =>
            IsYamlFloat(value) || (yaml11 && IsYaml11Float(value));

        // ---------------------------------------------------------------------------------------------------------
        // YAML 1.1 forms of the Yaml11 schema, limited to those YamlDotNet or SharpYaml accept.

        /// <summary><c>y|Y|yes|Yes|YES|on|On|ON</c> and <c>n|N|no|No|NO|off|Off|OFF</c>.</summary>
        internal static bool TryGetYaml11Boolean(ReadOnlySpan<byte> value, out bool result)
        {
            result = value.SequenceEqual("y"u8) || value.SequenceEqual("Y"u8) || value.SequenceEqual("yes"u8) || value.SequenceEqual("Yes"u8)
                || value.SequenceEqual("YES"u8) || value.SequenceEqual("on"u8) || value.SequenceEqual("On"u8) || value.SequenceEqual("ON"u8);
            return result
                || value.SequenceEqual("n"u8) || value.SequenceEqual("N"u8) || value.SequenceEqual("no"u8) || value.SequenceEqual("No"u8)
                || value.SequenceEqual("NO"u8) || value.SequenceEqual("off"u8) || value.SequenceEqual("Off"u8) || value.SequenceEqual("OFF"u8);
        }

        // ---------------------------------------------------------------------------------------------------------
        // YamlReaderOptions.AcceptLenientScalars: forms other libraries resolve
        // although no YAML standard does. Each is reached only where the standard forms have failed.

        /// <summary>
        /// <c>true</c> and <c>false</c> in any case (YamlDotNet, SharpYaml), and with the YAML 1.1 schema <c>y</c>,
        /// <c>yes</c>, <c>n</c>, <c>no</c>, <c>on</c> and <c>off</c> in any case (YamlDotNet). The standard casings match
        /// too.
        /// </summary>
        internal static bool TryGetLenientBoolean(ReadOnlySpan<byte> span, bool yaml11, out bool value)
        {
            value = false;
            if (span.Length == 4 && (span[0] | 0x20) == 't' && (span[1] | 0x20) == 'r' && (span[2] | 0x20) == 'u' && (span[3] | 0x20) == 'e')
            {
                value = true;
                return true;
            }
            if (span.Length == 5 && (span[0] | 0x20) == 'f' && (span[1] | 0x20) == 'a' && (span[2] | 0x20) == 'l' && (span[3] | 0x20) == 's' && (span[4] | 0x20) == 'e')
            {
                return true;
            }
            if (!yaml11 || span.Length is 0 or > 3)
            {
                return false;
            }

            Span<byte> lower = stackalloc byte[3];
            for (int i = 0; i < span.Length; i++)
            {
                lower[i] = (byte)(span[i] | 0x20);
            }
            ReadOnlySpan<byte> word = lower.Slice(0, span.Length);
            if (word.SequenceEqual("y"u8) || word.SequenceEqual("yes"u8) || word.SequenceEqual("on"u8))
            {
                value = true;
                return true;
            }
            return word.SequenceEqual("n"u8) || word.SequenceEqual("no"u8) || word.SequenceEqual("off"u8);
        }

        /// <summary><c>null</c> in any case (SharpYaml).</summary>
        internal static bool IsLenientNull(ReadOnlySpan<byte> span) =>
            span.Length == 4 && (span[0] | 0x20) == 'n' && (span[1] | 0x20) == 'u' && (span[2] | 0x20) == 'l' && (span[3] | 0x20) == 'l';

        /// <summary>
        /// The float names other libraries read: <c>[-+]?\.inf</c> and <c>\.nan</c> in any case (both libraries), a signed
        /// <c>[-+]\.nan</c> in any case (YamlDotNet), and .NET's names <c>nan</c> and <c>[-+]?infinity</c> in any case
        /// (SharpYaml; <see cref="TryParseDotNetFloatName"/>).
        /// </summary>
        internal static bool TryParseLenientFloatName(ReadOnlySpan<byte> span, out double value)
        {
            value = 0;
            bool signed = span.Length > 0 && span[0] is (byte)'-' or (byte)'+';
            ReadOnlySpan<byte> unsigned = signed ? span.Slice(1) : span;
            if (unsigned.Length == 4 && unsigned[0] == (byte)'.')
            {
                if ((unsigned[1] | 0x20) == 'i' && (unsigned[2] | 0x20) == 'n' && (unsigned[3] | 0x20) == 'f')
                {
                    value = span[0] == (byte)'-' ? double.NegativeInfinity : double.PositiveInfinity;
                    return true;
                }
                if ((unsigned[1] | 0x20) == 'n' && (unsigned[2] | 0x20) == 'a' && (unsigned[3] | 0x20) == 'n')
                {
                    value = double.NaN;
                    return true;
                }
                return false;
            }
            return TryParseDotNetFloatName(span, out value);
        }

        /// <summary>
        /// .NET's float names as SharpYaml reads them: <c>nan</c> in any case, unsigned, and <c>infinity</c> in any case,
        /// with or without a sign (not <c>inf</c>, not <c>∞</c>). The only names a <see cref="T:System.Half"/> read takes.
        /// </summary>
        internal static bool TryParseDotNetFloatName(ReadOnlySpan<byte> span, out double value)
        {
            value = 0;
            if (span.Length == 3 && (span[0] | 0x20) == 'n' && (span[1] | 0x20) == 'a' && (span[2] | 0x20) == 'n')
            {
                value = double.NaN;
                return true;
            }
            bool signed = span.Length > 0 && span[0] is (byte)'-' or (byte)'+';
            ReadOnlySpan<byte> unsigned = signed ? span.Slice(1) : span;
            if (unsigned.Length != 8)
            {
                return false;
            }
            ReadOnlySpan<byte> infinity = "infinity"u8;
            for (int i = 0; i < 8; i++)
            {
                if ((unsigned[i] | 0x20) != infinity[i])
                {
                    return false;
                }
            }
            value = span[0] == (byte)'-' ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        /// <summary>A leading-zero octal integer (<c>[-+]?0[0-7_]+</c>), which the YAML number parser would read as decimal.</summary>
        internal static bool IsYaml11LegacyOctal(ReadOnlySpan<byte> value)
        {
            if (value.Length > 0 && value[0] is (byte)'-' or (byte)'+')
            {
                value = value.Slice(1);
            }
            return value.Length > 1 && value[0] == (byte)'0' && value[1] is not ((byte)'x' or (byte)'b' or (byte)'o')
                && TryParseYaml11Integer(value, out _, out _, out bool matched) | matched;
        }

        /// <summary>
        /// Parses a YAML 1.1 integer: <c>[-+]?0b[0-1_]+</c>, <c>[-+]?0[0-7_]+</c>, <c>[-+]?(0|[1-9][0-9_]*)</c>,
        /// <c>[-+]?0x[0-9a-fA-F_]+</c> and <c>[-+]?[1-9][0-9_]*(:[0-5]?[0-9])+</c>.
        /// </summary>
        /// <param name="value">The text.</param>
        /// <param name="magnitude">The absolute value, when it fits.</param>
        /// <param name="negative">The text has a leading <c>-</c>.</param>
        /// <param name="matched">The text has one of these forms (the value may still overflow).</param>
        internal static bool TryParseYaml11Integer(ReadOnlySpan<byte> value, out ulong magnitude, out bool negative, out bool matched)
        {
            magnitude = 0;
            negative = false;
            matched = false;
            if (value.Length > 0 && value[0] is (byte)'-' or (byte)'+')
            {
                negative = value[0] == (byte)'-';
                value = value.Slice(1);
            }
            if (value.IsEmpty)
            {
                return false;
            }

            int radix;
            if (value.Length > 2 && value[0] == (byte)'0' && value[1] is (byte)'b' or (byte)'x')
            {
                radix = value[1] == (byte)'b' ? 2 : 16;
                value = value.Slice(2);
            }
            else if (value[0] == (byte)'0' && value.Length > 1)
            {
                radix = 8;
                value = value.Slice(1);
            }
            else if (value[0] is >= (byte)'0' and <= (byte)'9')
            {
                radix = value.IndexOf((byte)':') > 0 ? 60 : 10;
            }
            else
            {
                return false;
            }

            if (radix == 60)
            {
                return TryParseSexagesimal(value, out magnitude, out matched);
            }

            bool anyDigit = false;
            bool overflow = false;
            foreach (byte b in value)
            {
                if (b == (byte)'_')
                {
                    continue;
                }
                uint digit = b switch
                {
                    >= (byte)'0' and <= (byte)'9' => (uint)(b - '0'),
                    >= (byte)'a' and <= (byte)'f' => (uint)(b - 'a' + 10),
                    >= (byte)'A' and <= (byte)'F' => (uint)(b - 'A' + 10),
                    _ => uint.MaxValue,
                };
                if (digit >= (uint)radix)
                {
                    return false;
                }
                anyDigit = true;
                ulong next = unchecked(magnitude * (ulong)radix + digit);
                if (magnitude > ulong.MaxValue / (ulong)radix || next < magnitude * (ulong)radix)
                {
                    overflow = true;
                }
                magnitude = next;
            }

            matched = anyDigit;
            return anyDigit && !overflow;
        }

        /// <summary><c>[1-9][0-9_]*(:[0-5]?[0-9])+</c>, the value in base 60.</summary>
        private static bool TryParseSexagesimal(ReadOnlySpan<byte> value, out ulong magnitude, out bool matched)
        {
            magnitude = 0;
            matched = false;
            if (value[0] == (byte)'0')
            {
                return false;
            }

            int colon = value.IndexOf((byte)':');
            ulong total = 0;
            foreach (byte b in value.Slice(0, colon))
            {
                if (b == (byte)'_')
                {
                    continue;
                }
                if (b is < (byte)'0' or > (byte)'9')
                {
                    return false;
                }
                total = total * 10 + (ulong)(b - '0');
                if (total > uint.MaxValue)
                {
                    return false;
                }
            }

            value = value.Slice(colon + 1);
            while (true)
            {
                int next = value.IndexOf((byte)':');
                ReadOnlySpan<byte> part = next < 0 ? value : value.Slice(0, next);
                if (part.Length is < 1 or > 2 || !AllDigits(part) || (part.Length == 2 && part[0] > (byte)'5'))
                {
                    return false;
                }
                total = total * 60 + (ulong)(part.Length == 1 ? part[0] - '0' : (part[0] - '0') * 10 + part[1] - '0');
                if (total > long.MaxValue)
                {
                    return false;
                }
                if (next < 0)
                {
                    break;
                }
                value = value.Slice(next + 1);
            }

            magnitude = total;
            matched = true;
            return true;
        }

        /// <summary>A core-schema float written with <c>_</c> digit separators (<c>1_000.5</c>).</summary>
        internal static bool IsYaml11Float(ReadOnlySpan<byte> value)
        {
            if (value.IndexOf((byte)'_') < 0 || value.Length > 128)
            {
                return false;
            }
            Span<byte> stripped = stackalloc byte[value.Length];
            return IsYamlFloat(StripDigitSeparators(value, stripped));
        }

        /// <summary>Removes '_' that follows a digit; returns the input when another '_' makes it invalid.</summary>
        internal static ReadOnlySpan<byte> StripDigitSeparators(ReadOnlySpan<byte> value, Span<byte> destination)
        {
            int w = 0;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b == (byte)'_')
                {
                    if (w == 0 || destination[w - 1] is < (byte)'0' or > (byte)'9')
                    {
                        return value;
                    }
                    continue;
                }
                destination[w++] = b;
            }
            return destination.Slice(0, w);
        }

        internal static bool IsYamlNull(ReadOnlySpan<byte> value) =>
            value.IsEmpty || value.SequenceEqual("~"u8) || value.SequenceEqual("null"u8) || value.SequenceEqual("Null"u8) || value.SequenceEqual("NULL"u8);

        internal static bool TryGetYamlBoolean(ReadOnlySpan<byte> value, out bool result)
        {
            if (value.SequenceEqual("true"u8) || value.SequenceEqual("True"u8) || value.SequenceEqual("TRUE"u8))
            {
                result = true;
                return true;
            }
            if (value.SequenceEqual("false"u8) || value.SequenceEqual("False"u8) || value.SequenceEqual("FALSE"u8))
            {
                result = false;
                return true;
            }
            result = false;
            return false;
        }

        /// <summary>
        /// A number of the YAML 1.2 JSON schema (10.2.2): <c>-?(0|[1-9][0-9]*)(\.[0-9]*)?([eE][-+]?[0-9]+)?</c>.
        /// </summary>
        internal static bool IsJsonSchemaNumber(ReadOnlySpan<byte> value)
        {
            int i = 0;
            if (i < value.Length && value[i] == (byte)'-')
            {
                i++;
            }

            if (i >= value.Length || !IsAsciiDigit(value[i]))
            {
                return false;
            }

            if (value[i++] != (byte)'0')
            {
                while (i < value.Length && IsAsciiDigit(value[i]))
                {
                    i++;
                }
            }

            if (i < value.Length && value[i] == (byte)'.')
            {
                i++;
                while (i < value.Length && IsAsciiDigit(value[i]))
                {
                    i++;
                }
            }

            if (i < value.Length && (value[i] | 0x20) == (byte)'e')
            {
                i++;
                if (i < value.Length && value[i] is (byte)'+' or (byte)'-')
                {
                    i++;
                }

                int digits = i;
                while (i < value.Length && IsAsciiDigit(value[i]))
                {
                    i++;
                }

                if (i == digits)
                {
                    return false;
                }
            }

            return i == value.Length;

            static bool IsAsciiDigit(byte b) => (uint)(b - '0') <= 9;
        }

        /// <summary><c>[-+]?[0-9]+</c>, <c>0o[0-7]+</c>, <c>0x[0-9a-fA-F]+</c>.</summary>
        internal static bool IsYamlInteger(ReadOnlySpan<byte> value)
        {
            if (value.Length > 2 && value[0] == (byte)'0')
            {
                if (value[1] == (byte)'o')
                {
                    foreach (byte b in value.Slice(2))
                    {
                        if (b is < (byte)'0' or > (byte)'7')
                        {
                            return false;
                        }
                    }
                    return true;
                }
                if (value[1] == (byte)'x')
                {
                    foreach (byte b in value.Slice(2))
                    {
                        if (!IsHexDigit(b))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            if (value.Length > 0 && value[0] is (byte)'-' or (byte)'+')
            {
                value = value.Slice(1);
            }
            return AllDigits(value);
        }

        /// <summary>
        /// <c>[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?</c>, <c>[-+]?\.(inf|Inf|INF)</c>, <c>\.(nan|NaN|NAN)</c>.
        /// </summary>
        internal static bool IsYamlFloat(ReadOnlySpan<byte> value)
        {
            if (value.SequenceEqual(".nan"u8) || value.SequenceEqual(".NaN"u8) || value.SequenceEqual(".NAN"u8))
            {
                return true;
            }

            ReadOnlySpan<byte> v = value;
            if (v.Length > 0 && v[0] is (byte)'-' or (byte)'+')
            {
                v = v.Slice(1);
            }

            if (v.SequenceEqual(".inf"u8) || v.SequenceEqual(".Inf"u8) || v.SequenceEqual(".INF"u8))
            {
                return true;
            }

            int i = 0;
            int intDigits = 0;
            while (i < v.Length && v[i] is >= (byte)'0' and <= (byte)'9')
            {
                i++;
                intDigits++;
            }

            int fracDigits = 0;
            bool dot = false;
            if (i < v.Length && v[i] == (byte)'.')
            {
                dot = true;
                i++;
                while (i < v.Length && v[i] is >= (byte)'0' and <= (byte)'9')
                {
                    i++;
                    fracDigits++;
                }
            }

            // '\.[0-9]+' or '[0-9]+(\.[0-9]*)?'
            if (intDigits == 0 && (!dot || fracDigits == 0))
            {
                return false;
            }

            if (i < v.Length && v[i] is (byte)'e' or (byte)'E')
            {
                i++;
                if (i < v.Length && v[i] is (byte)'-' or (byte)'+')
                {
                    i++;
                }
                int expDigits = 0;
                while (i < v.Length && v[i] is >= (byte)'0' and <= (byte)'9')
                {
                    i++;
                    expDigits++;
                }
                if (expDigits == 0)
                {
                    return false;
                }
            }

            return i == v.Length;
        }
    }
}
