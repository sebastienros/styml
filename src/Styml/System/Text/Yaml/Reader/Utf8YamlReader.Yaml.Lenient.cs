// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    // YamlReaderOptions.AcceptLenientScalars: with
    // the option, a bool or number read also takes a quoted, block or tagged String whose text, trimmed, is the plain
    // scalar of a number, a boolean or a float name, as YamlDotNet and SharpYaml do. Every method here is reached only
    // where a typed read of a String token would throw, and none is inlined into the typed readers, so their bodies
    // (and their inlining decisions) do not change for a read with the option off.
    public ref partial struct Utf8YamlReader
    {
        /// <summary>
        /// With the option, a null (the Null token, or <c>NuLL</c>) read into a
        /// non-nullable value type gives its default, as YamlDotNet and SharpYaml do. The serializer tests it only once it
        /// knows the token is null, and for a collection once before its loop.
        /// </summary>
        internal readonly bool AcceptsLenientScalars => _readerOptions.AcceptLenientScalars;

        /// <summary>The YAML 1.1 schema is in use (for the bool key converter).</summary>
        internal readonly bool UsesYaml11Schema => _readerOptions.Schema == YamlSchemaKind.Yaml11;

        // Longer than any number or boolean text; a longer scalar is not tried.
        private const int LenientTextMaxLength = 256;

        /// <summary>
        /// A second reader over the current String's text, trimmed of white space and line breaks, positioned on that
        /// text read as one plain scalar under the same schema, with the option: a Number, True or False token, or a
        /// String (for the lenient forms of a plain scalar). Fails, so that the caller throws as without the option, for a
        /// plain untagged String (handled by the plain paths), a text that is not exactly one plain, untagged, unanchored
        /// scalar (<c>'1 # c'</c>, <c>'- 1'</c>, <c>'a: 1'</c>), a text that does not parse (<c>'[1'</c>), and a text
        /// that resolves to null (<c>''</c>, <c>'~'</c>, <c>'null'</c>, <c>'NuLL'</c>): a quoted null is a string.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryGetLenientScalarReader(out Utf8YamlReader inner)
        {
            inner = default;
            if (!_readerOptions.AcceptLenientScalars || _tokenType != YamlTokenType.String || _yamlStyle == YamlScalarStyle.None
                || (_yamlStyle == YamlScalarStyle.Plain && _yamlTagLength == 0))
            {
                return false;
            }

            long rawLength = HasValueSequence ? ValueSequence.Length : ValueSpan.Length;
            if (rawLength > LenientTextMaxLength)
            {
                return false;
            }

            ReadOnlySpan<byte> text = GetUnescapedSpan();
            int start = 0;
            int end = text.Length;
            while (start < end && text[start] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                start++;
            }
            while (end > start && text[end - 1] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                end--;
            }
            if (start == end)
            {
                return false;
            }

            text = text.Slice(start, end - start);
            var reader = new Utf8YamlReader(text, new YamlReaderOptions { Schema = _readerOptions.Schema, AcceptLenientScalars = true });
            try
            {
                if (!reader.Read())
                {
                    return false;
                }
            }
            catch (YamlException)
            {
                return false;
            }

            if (reader._tokenType is not (YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.String)
                || reader._yamlStyle != YamlScalarStyle.Plain || reader._yamlTagLength != 0 || reader._yamlAnchorLength != 0
                || reader._yamlNeedsProcessing || reader.HasValueSequence || !reader.ValueSpan.SequenceEqual(text)
                || reader.IsLenientNull)
            {
                return false;
            }

            inner = reader;
            return true;
        }

        /// <summary>.NET's float names in a plain, untagged String (SharpYaml): the only names a Half read takes.</summary>
        internal readonly bool TryGetLenientDotNetFloatName(out double value)
        {
            value = 0;
            return _readerOptions.AcceptLenientScalars && IsLenientScalarCandidate
                && YamlReaderHelper.TryParseDotNetFloatName(YamlValueBytes, out value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetBooleanLenient(out bool value)
        {
            if (TryGetYamlCaseInsensitiveBoolean(out value))
            {
                return true;
            }
            if (TryGetLenientScalarReader(out Utf8YamlReader inner))
            {
                switch (inner._tokenType)
                {
                    case YamlTokenType.True:
                        value = true;
                        return true;
                    case YamlTokenType.False:
                        value = false;
                        return true;
                    case YamlTokenType.String:
                        return inner.TryGetYamlCaseInsensitiveBoolean(out value);
                }
            }
            value = false;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetDoubleLenient(out double value)
        {
            if (TryGetYamlLenientFloatName(out value))
            {
                return true;
            }
            if (TryGetLenientScalarReader(out Utf8YamlReader inner) && inner._tokenType is YamlTokenType.Number or YamlTokenType.String)
            {
                return inner._tokenType == YamlTokenType.Number ? inner.TryGetDouble(out value) : inner.TryGetYamlLenientFloatName(out value);
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetSingleLenient(out float value)
        {
            if (TryGetYamlLenientFloatName(out double name))
            {
                value = (float)name;
                return true;
            }
            if (TryGetLenientScalarReader(out Utf8YamlReader inner) && inner._tokenType is YamlTokenType.Number or YamlTokenType.String)
            {
                if (inner._tokenType == YamlTokenType.Number)
                {
                    return inner.TryGetSingle(out value);
                }
                bool ok = inner.TryGetYamlLenientFloatName(out name);
                value = (float)name;
                return ok;
            }
            value = 0;
            return false;
        }

        // The throwing branches of the typed readers call one of these: the lenient read, or the throw it replaced, so the
        // readers' bodies (and their inlining) stay as they were before the option.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetByteLenientOrThrow(out byte value)
        {
            if (TryGetByteLenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetSByteLenientOrThrow(out sbyte value)
        {
            if (TryGetSByteLenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetInt16LenientOrThrow(out short value)
        {
            if (TryGetInt16Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetInt32LenientOrThrow(out int value)
        {
            if (TryGetInt32Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetInt64LenientOrThrow(out long value)
        {
            if (TryGetInt64Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetUInt16LenientOrThrow(out ushort value)
        {
            if (TryGetUInt16Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetUInt32LenientOrThrow(out uint value)
        {
            if (TryGetUInt32Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetUInt64LenientOrThrow(out ulong value)
        {
            if (TryGetUInt64Lenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetDecimalLenientOrThrow(out decimal value)
        {
            if (TryGetDecimalLenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetSingleLenientOrThrow(out float value)
        {
            if (TryGetSingleLenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetDoubleLenientOrThrow(out double value)
        {
            if (TryGetDoubleLenient(out value))
            {
                return true;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool GetBooleanLenientOrThrow()
        {
            if (TryGetBooleanLenient(out bool value))
            {
                return value;
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedBoolean(TokenType);
            return false;
        }

        /// <summary>
        /// With the option, a quoted or block text padded with white space or line
        /// breaks (<c>' 2001-01-01 '</c>, <c>"01:02:03\n"</c>, a <c>|</c> block) reads into a type that parses a string as
        /// its trimmed text would, as YamlDotNet and SharpYaml read it: a second reader over the trimmed text.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryGetLenientTrimmedReader(out Utf8YamlReader inner) =>
            TryGetLenientScalarReader(out inner) && inner.TokenIsStringLike;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetDateTimeLenient(out DateTime value)
        {
            value = default;
            return TryGetLenientTrimmedReader(out Utf8YamlReader inner) && inner.TryGetDateTimeCore(out value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetDateTimeOffsetLenient(out DateTimeOffset value)
        {
            value = default;
            return TryGetLenientTrimmedReader(out Utf8YamlReader inner) && inner.TryGetDateTimeOffsetCore(out value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetGuidLenient(out Guid value)
        {
            value = default;
            return TryGetLenientTrimmedReader(out Utf8YamlReader inner) && inner.TryGetGuidCore(out value);
        }

        /// <summary>
        /// M5e: a typed dictionary key reads like a value of the same schema. A
        /// second reader over the key's decoded text (quoted or not: a key's quotes say nothing about its type, as in YAML
        /// and both libraries) resolves it as a plain scalar of the Core or YAML 1.1 schema; under Json and Failsafe a key
        /// keeps its text forms only. Called after the .NET parse fails, or first for YAML 1.1's leading-zero forms.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly bool TryGetYamlKeySchemaReader(ReadOnlySpan<byte> text, out Utf8YamlReader inner)
        {
            inner = default;
            if (_readerOptions.Schema is not (YamlSchemaKind.Core or YamlSchemaKind.Yaml11) || text.IsEmpty || text.Length > LenientTextMaxLength
                || text[0] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r'
                || text[text.Length - 1] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                // Only a text that is one plain scalar as written: a padded key keeps today's refusal.
                return false;
            }

            var reader = new Utf8YamlReader(text, new YamlReaderOptions { Schema = _readerOptions.Schema });
            try
            {
                if (!reader.Read() || reader._tokenType is not (YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False))
                {
                    return false;
                }
            }
            catch (YamlException)
            {
                return false;
            }

            inner = reader;
            return true;
        }

        /// <summary>
        /// M5e: an enum key spelled as an integer form that .NET does not read the schema's way (<c>0x1</c>,
        /// <c>0o7</c>, and under YAML 1.1 <c>017</c>). False for a plain decimal integer, which reads as before.
        /// </summary>
        internal bool TryGetYamlKeySchemaInteger(out long value)
        {
            value = 0;
            if (_tokenType != YamlTokenType.PropertyName)
            {
                return false;
            }

            ReadOnlySpan<byte> text = GetUnescapedSpan();
            int i = text.Length > 0 && text[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            bool decimalDigits = text.Length > i;
            for (int j = i; j < text.Length && decimalDigits; j++)
            {
                decimalDigits = text[j] is >= (byte)'0' and <= (byte)'9';
            }

            return (!decimalDigits || YamlKeyPrefersSchema(text))
                && TryGetYamlKeySchemaReader(text, out Utf8YamlReader key) && key.TryGetInt64(out value);
        }

        /// <summary>
        /// M5e (review 6): the one form where .NET and a schema read a key differently: under YAML 1.1 an integer
        /// with a leading zero (<c>017</c>, <c>-0777</c>) is octal, so the schema's reading goes first.
        /// </summary>
        private readonly bool YamlKeyPrefersSchema(ReadOnlySpan<byte> text)
        {
            if (_readerOptions.Schema != YamlSchemaKind.Yaml11)
            {
                return false;
            }

            int i = text.Length > 0 && text[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            return text.Length > i + 1 && text[i] == (byte)'0' && text[i + 1] is >= (byte)'0' and <= (byte)'9';
        }

        // The integer and decimal readers take a Number only: a float name or a boolean is not a number of these types.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetLenientNumberReader(out Utf8YamlReader inner) =>
            TryGetLenientScalarReader(out inner) && inner._tokenType == YamlTokenType.Number;

        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetByteLenient(out byte value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetByte(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetSByteLenient(out sbyte value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetSByte(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetInt16Lenient(out short value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetInt16(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetInt32Lenient(out int value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetInt32(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetInt64Lenient(out long value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetInt64(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetUInt16Lenient(out ushort value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetUInt16(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetUInt32Lenient(out uint value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetUInt32(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetUInt64Lenient(out ulong value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetUInt64(out value); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool TryGetDecimalLenient(out decimal value) { value = 0; return TryGetLenientNumberReader(out Utf8YamlReader inner) && inner.TryGetDecimal(out value); }
    }
}
