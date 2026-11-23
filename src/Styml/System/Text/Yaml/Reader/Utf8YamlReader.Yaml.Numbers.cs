// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers.Text;

namespace System.Text.Yaml
{
    // Slow path of the numeric readers for YAML 1.2 core-schema forms that JSON does not have: a leading '+', 0x / 0o
    // integers, '.5' / '5.' floats, '.inf', '.nan', and with
    // YamlSchemaKind.Yaml11 the YAML 1.1 forms of section 13.4. The JSON fast path runs first, except for a 1.1
    // leading-zero octal; these are only reached for tokens produced by the YAML grammar.
    public ref partial struct Utf8YamlReader
    {
        private readonly bool YamlNumberSlowPathApplies => _yamlStyle != YamlScalarStyle.None;

        /// <summary>
        /// <see cref="YamlReaderOptions.AcceptLenientScalars"/>: a typed boolean read
        /// accepts a plain, untagged scalar whose text is true or false in any case (<c>tRue</c>), as YamlDotNet and
        /// SharpYaml do, and with <see cref="YamlSchemaKind.Yaml11"/> also y, yes, n, no, on or off in any case
        /// (<c>yEs</c>), as YamlDotNet does. The token stays a String (the schema is unchanged), so untyped reads still see
        /// a string. Quoted and block scalars and tagged nodes never qualify. Reached only after the
        /// token was neither True nor False.
        /// </summary>
        private readonly bool TryGetYamlCaseInsensitiveBoolean(out bool value)
        {
            value = false;
            return _readerOptions.AcceptLenientScalars && IsLenientScalarCandidate
                && YamlReaderHelper.TryGetLenientBoolean(YamlValueBytes, _readerOptions.Schema == YamlSchemaKind.Yaml11, out value);
        }

        /// <summary>
        /// A plain, untagged, unprocessed scalar of the YAML grammar that resolved to a String: the only tokens
        /// <see cref="YamlReaderOptions.AcceptLenientScalars"/> reads differently.
        /// </summary>
        private readonly bool IsLenientScalarCandidate =>
            _tokenType == YamlTokenType.String && _yamlStyle == YamlScalarStyle.Plain && _yamlTagLength == 0 && !_yamlNeedsProcessing;

        /// <summary>
        /// <see cref="YamlReaderOptions.AcceptLenientScalars"/>: the current token is a plain, untagged String whose text
        /// is null in a case the core schema does not resolve (<c>NuLL</c>), which typed reads take as null, as SharpYaml
        /// does. The token stays a String. With the option off this is one test of a field.
        /// </summary>
        internal readonly bool IsLenientNull => _readerOptions.AcceptLenientScalars && IsLenientNullCore();

        private readonly bool IsLenientNullCore()
        {
            if (!IsLenientScalarCandidate)
            {
                return false;
            }

            return YamlReaderHelper.IsLenientNull(YamlValueBytes);
        }

        /// <summary>
        /// <see cref="YamlReaderOptions.AcceptLenientScalars"/>: the float names other libraries read
        /// (<see cref="YamlReaderHelper.TryParseLenientFloatName"/>) in a plain, untagged String. The core schema's
        /// casings are Number tokens and never reach this; it is reached only where a String token would throw.
        /// </summary>
        private readonly bool TryGetYamlLenientFloatName(out double value)
        {
            value = 0;
            return _readerOptions.AcceptLenientScalars && IsLenientScalarCandidate
                && YamlReaderHelper.TryParseLenientFloatName(YamlValueBytes, out value);
        }

        /// <summary>Parses a YAML integer (decimal with optional sign, 0x hexadecimal, 0o octal).</summary>
        private readonly bool TryGetYamlInteger(out ulong magnitude, out bool negative)
        {
            magnitude = 0;
            negative = false;
            ReadOnlySpan<byte> span = YamlValueBytes;
            if (_yamlNeedsProcessing)
            {
                return false;
            }
            if (_readerOptions.Schema == YamlSchemaKind.Yaml11)
            {
                // YAML 1.1 forms first: where the schemas disagree (0777), 1.1 wins.
                bool parsed = YamlReaderHelper.TryParseYaml11Integer(span, out magnitude, out negative, out bool matched);
                if (matched)
                {
                    return parsed;
                }
            }
            if (!YamlReaderHelper.IsYamlInteger(span))
            {
                return false;
            }

            int radix = 10;
            if (span.Length > 2 && span[0] == (byte)'0' && span[1] is (byte)'x' or (byte)'o')
            {
                radix = span[1] == (byte)'x' ? 16 : 8;
                span = span.Slice(2);
            }
            else if (span[0] is (byte)'-' or (byte)'+')
            {
                negative = span[0] == (byte)'-';
                span = span.Slice(1);
            }

            foreach (byte b in span)
            {
                uint digit = b <= (byte)'9' ? (uint)(b - '0') : (uint)((b | 0x20) - 'a' + 10);
                ulong next = unchecked(magnitude * (ulong)radix + digit);
                if (magnitude > ulong.MaxValue / (ulong)radix || next < magnitude * (ulong)radix)
                {
                    return false;
                }
                magnitude = next;
            }
            return true;
        }

        private readonly bool TryGetYamlSigned(long min, long max, out long value)
        {
            value = 0;
            if (!YamlNumberSlowPathApplies || !TryGetYamlInteger(out ulong magnitude, out bool negative))
            {
                return false;
            }
            if (negative)
            {
                if (magnitude > (ulong)max + 1 || -(decimal)magnitude < min)
                {
                    return false;
                }
                value = magnitude == (ulong)long.MaxValue + 1 ? long.MinValue : -(long)magnitude;
                return true;
            }
            if (magnitude > (ulong)max)
            {
                return false;
            }
            value = (long)magnitude;
            return true;
        }

        private readonly bool TryGetYamlUnsigned(ulong max, out ulong value)
        {
            value = 0;
            if (!YamlNumberSlowPathApplies || !TryGetYamlInteger(out ulong magnitude, out bool negative) || (negative && magnitude != 0) || magnitude > max)
            {
                return false;
            }
            value = magnitude;
            return true;
        }

        private readonly bool TryGetYamlByte(out byte value) { bool ok = TryGetYamlUnsigned(byte.MaxValue, out ulong v); value = (byte)v; return ok; }

        private readonly bool TryGetYamlSByte(out sbyte value) { bool ok = TryGetYamlSigned(sbyte.MinValue, sbyte.MaxValue, out long v); value = (sbyte)v; return ok; }

        private readonly bool TryGetYamlInt16(out short value) { bool ok = TryGetYamlSigned(short.MinValue, short.MaxValue, out long v); value = (short)v; return ok; }

        private readonly bool TryGetYamlInt32(out int value) { bool ok = TryGetYamlSigned(int.MinValue, int.MaxValue, out long v); value = (int)v; return ok; }

        private readonly bool TryGetYamlInt64(out long value) => TryGetYamlSigned(long.MinValue, long.MaxValue, out value);

        private readonly bool TryGetYamlUInt16(out ushort value) { bool ok = TryGetYamlUnsigned(ushort.MaxValue, out ulong v); value = (ushort)v; return ok; }

        private readonly bool TryGetYamlUInt32(out uint value) { bool ok = TryGetYamlUnsigned(uint.MaxValue, out ulong v); value = (uint)v; return ok; }

        private readonly bool TryGetYamlUInt64(out ulong value) => TryGetYamlUnsigned(ulong.MaxValue, out value);

        /// <summary>Normalises a YAML float (or integer) to text Utf8Parser accepts, or resolves .inf/.nan.</summary>
        private readonly bool TryGetYamlDouble(out double value)
        {
            value = 0;
            if (!YamlNumberSlowPathApplies || _yamlNeedsProcessing)
            {
                return false;
            }

            scoped ReadOnlySpan<byte> span = YamlValueBytes;
            if (TryGetYamlInteger(out ulong magnitude, out bool negative))
            {
                value = negative ? -(double)magnitude : magnitude;
                return true;
            }
            Span<byte> stripped = stackalloc byte[span.Length <= 128 ? span.Length : 0];
            if (_readerOptions.Schema == YamlSchemaKind.Yaml11 && span.Length <= 128 && span.IndexOf((byte)'_') >= 0)
            {
                // '1_000.5'.
                span = YamlReaderHelper.StripDigitSeparators(span, stripped);
            }
            if (!YamlReaderHelper.IsYamlFloat(span))
            {
                // A Number token whose text is a lenient float name comes from !!float .iNf, with the option only.
                return _readerOptions.AcceptLenientScalars && YamlReaderHelper.TryParseLenientFloatName(span, out value);
            }

            bool minus = span[0] == (byte)'-';
            ReadOnlySpan<byte> unsigned = span[0] is (byte)'-' or (byte)'+' ? span.Slice(1) : span;
            if (unsigned.Length == 4 && unsigned[0] == (byte)'.')
            {
                if (unsigned[1] is (byte)'n' or (byte)'N')
                {
                    value = double.NaN;
                    return true;
                }
                value = minus ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }

            Span<byte> buffer = stackalloc byte[unsigned.Length + 3];
            int w = 0;
            if (minus)
            {
                buffer[w++] = (byte)'-';
            }
            if (unsigned[0] == (byte)'.')
            {
                buffer[w++] = (byte)'0';
            }
            for (int i = 0; i < unsigned.Length; i++)
            {
                buffer[w++] = unsigned[i];
                if (unsigned[i] == (byte)'.' && (i + 1 == unsigned.Length || unsigned[i + 1] is (byte)'e' or (byte)'E'))
                {
                    buffer[w++] = (byte)'0';
                }
            }

            ReadOnlySpan<byte> normalized = buffer.Slice(0, w);
            return Utf8Parser.TryParse(normalized, out value, out int consumed) && consumed == normalized.Length;
        }

        private readonly bool TryGetYamlSingle(out float value)
        {
            bool ok = TryGetYamlDouble(out double d);
            value = (float)d;
            return ok && (!float.IsInfinity(value) || double.IsInfinity(d));
        }

        private readonly bool TryGetYamlDecimal(out decimal value)
        {
            value = 0;
            if (!YamlNumberSlowPathApplies || !TryGetYamlDouble(out double d) || double.IsNaN(d) || double.IsInfinity(d))
            {
                return false;
            }

            // Re-parse the normalised decimal text for full precision when it is not a hex/octal integer.
            if (TryGetYamlInteger(out ulong magnitude, out bool negative))
            {
                value = negative ? -(decimal)magnitude : magnitude;
                return true;
            }

            try
            {
                value = (decimal)d;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
