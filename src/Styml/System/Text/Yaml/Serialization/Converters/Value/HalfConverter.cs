// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class HalfConverter : YamlPrimitiveConverter<Half>
    {
        private const int MaxFormatLength = 20;
        private const int MaxUnescapedFormatLength = YamlConstants.MaximumFloatingPointConstantLength * YamlConstants.MaxExpansionFactorWhileEscaping;

        public HalfConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        internal override bool IsIeeeFloatingPointConverter => true;

        public override Half Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                if (TryReadLenient(ref reader, out Half lenient))
                {
                    return lenient;
                }
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        public override void Write(Utf8YamlWriter writer, Half value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            WriteCore(writer, value);
        }

        // YamlReaderOptions.AcceptLenientScalars: .NET's names (nan, infinity; not the
        // dotted .inf and .nan, which both libraries refuse into a Half), and a quoted, block or tagged number. Reached only
        // where the read would throw.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryReadLenient(ref Utf8YamlReader reader, out Half value)
        {
            if (reader.TryGetLenientDotNetFloatName(out double name))
            {
                value = (Half)name;
                return true;
            }
            if (reader.TryGetLenientScalarReader(out Utf8YamlReader inner))
            {
                if (inner.TokenType == YamlTokenType.Number)
                {
                    value = ReadCore(ref inner);
                    return true;
                }
                if (inner.TryGetLenientDotNetFloatName(out name))
                {
                    value = (Half)name;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static unsafe Half ReadCore(ref Utf8YamlReader reader)
        {
            Half result;

            byte[]? rentedByteBuffer = null;
            int bufferLength = reader.ValueLength;

            Span<byte> byteBuffer = bufferLength <= YamlConstants.StackallocByteThreshold
                ? stackalloc byte[YamlConstants.StackallocByteThreshold]
                : (rentedByteBuffer = ArrayPool<byte>.Shared.Rent(bufferLength));

            int written = reader.CopyValue(byteBuffer);
            byteBuffer = byteBuffer.Slice(0, written);

            bool success = TryParse(byteBuffer, out result);
            if (rentedByteBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedByteBuffer);
            }

            if (!success)
            {
                ThrowHelper.ThrowFormatException(NumericType.Half);
            }

            Debug.Assert(!Half.IsNaN(result) && !Half.IsInfinity(result));
            return result;
        }

        private static unsafe void WriteCore(Utf8YamlWriter writer, Half value)
        {
            if (writer.IsBlockStyle && !Half.IsFinite(value))
            {
                // Decision 8: in block style, NaN and the infinities are YAML's plain
                // floats (.nan, .inf, -.inf), with or without AllowNamedFloatingPointLiterals.
                writer.WriteNumberValue(Half.IsNaN(value) ? double.NaN : Half.IsPositiveInfinity(value) ? double.PositiveInfinity : double.NegativeInfinity);
                return;
            }

            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            writer.WriteRawValue(buffer.Slice(0, written));
        }

        internal override Half ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, Half value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            if (Half.IsFinite(value))
            {
                // M5f: plain in block style.
                writer.WritePlainPropertyName(buffer.Slice(0, written));
            }
            else
            {
                // A Half does not read .nan or .inf (as in both libraries), so its non-finite keys keep today's "NaN" text.
                writer.WritePropertyName(buffer.Slice(0, written));
            }
        }

        internal override Half ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String)
            {
                if ((YamlNumberHandling.AllowReadingFromString & handling) != 0)
                {
                    if (TryGetFloatingPointConstant(ref reader, out Half value) || TryReadLenient(ref reader, out value))
                    {
                        return value;
                    }

                    return ReadCore(ref reader);
                }
                else if ((YamlNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
                {
                    if (!TryGetFloatingPointConstant(ref reader, out Half value) && !TryReadLenient(ref reader, out value))
                    {
                        ThrowHelper.ThrowFormatException(NumericType.Half);
                    }

                    return value;
                }
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                if (TryReadLenient(ref reader, out Half lenient))
                {
                    return lenient;
                }
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        internal override unsafe void WriteNumberWithCustomHandling(Utf8YamlWriter writer, Half value, YamlNumberHandling handling)
        {
            if ((YamlNumberHandling.WriteAsString & handling) != 0)
            {
                const byte Quote = YamlConstants.Quote;
                Span<byte> buffer = stackalloc byte[MaxFormatLength + 2];
                buffer[0] = Quote;
                Format(buffer.Slice(1), value, out int written);

                int length = written + 2;
                buffer[length - 1] = Quote;
                writer.WriteRawValue(buffer.Slice(0, length));
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

        private static unsafe bool TryGetFloatingPointConstant(ref Utf8YamlReader reader, out Half value)
        {
            scoped Span<byte> buffer;

            // Only checking for length 10 or less for constants
            if (reader.ValueIsEscaped)
            {
                if (reader.ValueLength > MaxUnescapedFormatLength)
                {
                    value = default;
                    return false;
                }

                buffer = stackalloc byte[MaxUnescapedFormatLength];
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

            if (written > YamlConstants.MaximumFloatingPointConstantLength)
            {
                value = default;
                return false;
            }

            return YamlReaderHelper.TryGetFloatingPointConstant(buffer.Slice(0, written), out value);
        }

        private static void WriteFloatingPointConstant(Utf8YamlWriter writer, Half value)
        {
            if (writer.IsBlockStyle && !Half.IsFinite(value))
            {
                // Decision 8: in block style, NaN and the infinities are YAML's plain
                // floats (.nan, .inf, -.inf), with or without AllowNamedFloatingPointLiterals.
                writer.WriteNumberValue(Half.IsNaN(value) ? double.NaN : Half.IsPositiveInfinity(value) ? double.PositiveInfinity : double.NegativeInfinity);
                return;
            }

            if (Half.IsNaN(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.NaNValue);
            }
            else if (Half.IsPositiveInfinity(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.PositiveInfinityValue);
            }
            else if (Half.IsNegativeInfinity(value))
            {
                writer.WriteNumberValueAsStringUnescaped(YamlConstants.NegativeInfinityValue);
            }
            else
            {
                WriteCore(writer, value);
            }
        }

        private static bool TryParse(ReadOnlySpan<byte> buffer, out Half result)
        {
            bool success = Half.TryParse(buffer, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);

            // Half.TryParse is more lax with floating-point literals than other S.T.Yaml floating-point types
            // e.g: it parses "naN" successfully. Only succeed with the exact match.
            return success &&
                (!Half.IsNaN(result) || buffer.SequenceEqual(YamlConstants.NaNValue)) &&
                (!Half.IsPositiveInfinity(result) || buffer.SequenceEqual(YamlConstants.PositiveInfinityValue)) &&
                (!Half.IsNegativeInfinity(result) || buffer.SequenceEqual(YamlConstants.NegativeInfinityValue));
        }

        private static void Format(
            Span<byte> destination,
            Half value, out int written)
        {
            bool formattedSuccessfully = value.TryFormat(destination, out written, provider: CultureInfo.InvariantCulture);
            Debug.Assert(formattedSuccessfully);
        }
    }
}
