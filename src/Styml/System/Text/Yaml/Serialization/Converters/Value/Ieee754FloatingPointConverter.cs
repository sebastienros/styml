// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for IEEE 754 floating-point types that share the same YAML number representation,
    /// such as <see cref="BFloat16"/> and the IEEE 754 decimal types.
    /// </summary>
    internal sealed class Ieee754FloatingPointConverter<T> : YamlPrimitiveConverter<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        // Values that need more room than this are formatted into a pooled buffer.
        // Formatting of the IEEE 754 decimal types only uses scientific notation when it is
        // required or more compact, so large-magnitude values expand to many digits.
        private const int StackBufferLength = 64;
        // This buffer only ever holds one of the named literals "NaN", "Infinity" or "-Infinity",
        // so it is sized from their maximum length rather than from any numeric format length.
        private const int MaxEscapedNamedLiteralLength = YamlConstants.MaximumFloatingPointConstantLength * YamlConstants.MaxExpansionFactorWhileEscaping;

        private readonly NumericType _numericType;

        public Ieee754FloatingPointConverter(NumericType numericType)
        {
            _numericType = numericType;
            IsInternalConverterForNumberType = true;
        }

        internal override bool IsIeeeFloatingPointConverter => true;

        public override T Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader, isStringValue: false);
        }

        public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            WriteCore(writer, value);
        }

        internal override T ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader, isStringValue: true);
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, T value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ValidateFinite(value);

            Span<byte> stackBuffer = stackalloc byte[StackBufferLength];
            byte[]? rented = Format(value, stackBuffer, out ReadOnlySpan<byte> formatted);
            writer.WritePropertyName(formatted);
            Return(rented);
        }

        internal override T ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String)
            {
                if ((YamlNumberHandling.AllowReadingFromString & handling) != 0)
                {
                    if (TryGetFloatingPointConstant(ref reader, out T value))
                    {
                        return value;
                    }

                    return ReadCore(ref reader, isStringValue: true);
                }
                else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
                {
                    if (!TryGetFloatingPointConstant(ref reader, out T value))
                    {
                        ThrowHelper.ThrowFormatException(_numericType);
                    }

                    return value;
                }
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader, isStringValue: false);
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, T value, YamlNumberHandling handling)
        {
            if ((YamlNumberHandling.WriteAsString & handling) != 0)
            {
                Span<byte> stackBuffer = stackalloc byte[StackBufferLength];
                byte[]? rented = Format(value, stackBuffer, out ReadOnlySpan<byte> formatted);
                writer.WriteNumberValueAsStringUnescaped(formatted);
                Return(rented);
            }
            else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
            {
                WriteFloatingPointConstant(writer, value);
            }
            else
            {
                WriteCore(writer, value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
            GetSchemaForNumericType(YamlSchemaType.Number, numberHandling, isIeeeFloatingPoint: true);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            GetSupportedYamlValueTypesForNumericType(numberHandling);

        // Reads the current token as a number. When the token is a string or property name,
        // non-finite results are rejected: the underlying parsers accept named literals such as "naN"
        // that are not valid JSON, and a string that overflows to infinity is rejected for consistency
        // with float and double. Number tokens are already constrained to the JSON number grammar by
        // the reader, so they are permitted to overflow to infinity per IEEE 754.
        private T ReadCore(ref Utf8YamlReader reader, bool isStringValue)
        {
            byte[]? rentedByteBuffer = null;
            int bufferLength = reader.ValueLength;

            Span<byte> byteBuffer = bufferLength <= YamlConstants.StackallocByteThreshold
                ? stackalloc byte[YamlConstants.StackallocByteThreshold]
                : (rentedByteBuffer = ArrayPool<byte>.Shared.Rent(bufferLength));

            int written = reader.CopyValue(byteBuffer);
            byteBuffer = byteBuffer.Slice(0, written);

            bool success = TryParse(byteBuffer, out T result) && (!isStringValue || T.IsFinite(result));
            Return(rentedByteBuffer);

            if (!success)
            {
                ThrowHelper.ThrowFormatException(_numericType);
            }

            return result;
        }

        private static void WriteCore(Utf8YamlWriter writer, T value)
        {
            if (writer.IsBlockStyle && !T.IsFinite(value))
            {
                // Decision 8: in block style, NaN and the infinities are YAML's plain
                // floats (.nan, .inf, -.inf), with or without AllowNamedFloatingPointLiterals.
                writer.WriteNumberValue(T.IsNaN(value) ? double.NaN : T.IsPositiveInfinity(value) ? double.PositiveInfinity : double.NegativeInfinity);
                return;
            }

            ValidateFinite(value);

            Span<byte> stackBuffer = stackalloc byte[StackBufferLength];
            byte[]? rented = Format(value, stackBuffer, out ReadOnlySpan<byte> formatted);
            writer.WriteRawValue(formatted);
            Return(rented);
        }

        // NaN and infinity have no representation in the JSON number grammar. Reject them with the
        // same error Utf8YamlWriter reports for float and double, which points callers at
        // YamlNumberHandling.AllowNamedFloatingPointLiterals.
        private static void ValidateFinite(T value)
        {
            if (!T.IsFinite(value))
            {
                ThrowHelper.ThrowArgumentException_ValueNotSupported();
            }
        }

        private static void WriteFloatingPointConstant(Utf8YamlWriter writer, T value)
        {
            if (writer.IsBlockStyle && !T.IsFinite(value))
            {
                // Decision 8: in block style, NaN and the infinities are YAML's plain
                // floats (.nan, .inf, -.inf), with or without AllowNamedFloatingPointLiterals.
                writer.WriteNumberValue(T.IsNaN(value) ? double.NaN : T.IsPositiveInfinity(value) ? double.PositiveInfinity : double.NegativeInfinity);
                return;
            }

            if (T.IsNaN(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.NaNValue);
            }
            else if (T.IsPositiveInfinity(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.PositiveInfinityValue);
            }
            else if (T.IsNegativeInfinity(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.NegativeInfinityValue);
            }
            else
            {
                WriteCore(writer, value);
            }
        }

        private static bool TryGetFloatingPointConstant(ref Utf8YamlReader reader, out T value)
        {
            scoped Span<byte> buffer;

            if (reader.ValueIsEscaped)
            {
                if (reader.ValueLength > MaxEscapedNamedLiteralLength)
                {
                    value = default;
                    return false;
                }

                buffer = stackalloc byte[MaxEscapedNamedLiteralLength];
            }
            else
            {
                if (reader.ValueLength > YamlConstants.MaximumFloatingPointConstantLength)
                {
                    value = default;
                    return false;
                }

                buffer = stackalloc byte[YamlConstants.MaximumFloatingPointConstantLength];
            }

            int written = reader.CopyValue(buffer);
            return TryGetFloatingPointConstant(buffer.Slice(0, written), out value);
        }

        private static bool TryGetFloatingPointConstant(ReadOnlySpan<byte> span, out T value)
        {
            if (span.SequenceEqual(YamlConstants.NaNValue))
            {
                value = T.NaN;
                return true;
            }

            if (span.SequenceEqual(YamlConstants.PositiveInfinityValue))
            {
                value = T.PositiveInfinity;
                return true;
            }

            if (span.SequenceEqual(YamlConstants.NegativeInfinityValue))
            {
                value = T.NegativeInfinity;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryParse(ReadOnlySpan<byte> buffer, out T result) =>
            T.TryParse(buffer, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);

        // Formats value into initialBuffer, growing into a pooled buffer if necessary.
        // Returns the rented array, if any, which the caller must return.
        private static byte[]? Format(T value, Span<byte> initialBuffer, out ReadOnlySpan<byte> formatted)
        {
            byte[]? rented = null;
            Span<byte> destination = initialBuffer;
            int written;

            while (!value.TryFormat(destination, out written, format: default, provider: CultureInfo.InvariantCulture))
            {
                byte[]? toReturn = rented;
                rented = ArrayPool<byte>.Shared.Rent(destination.Length * 2);
                destination = rented;
                Return(toReturn);
            }

            formatted = destination.Slice(0, written);
            return rented;
        }

        private static void Return(byte[]? rented)
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
