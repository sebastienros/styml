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
    internal sealed class UInt128Converter : YamlPrimitiveConverter<UInt128>
    {
        private const int MaxFormatLength = 39;

        public UInt128Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override UInt128 Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                if (TryReadLenient(ref reader, out UInt128 lenient))
                {
                    return lenient;
                }
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        public override void Write(Utf8YamlWriter writer, UInt128 value, YamlSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not YamlNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            WriteCore(writer, value);
        }

        // YamlReaderOptions.AcceptLenientScalars: a quoted, block or tagged number.
        // Reached only where the read would throw.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryReadLenient(ref Utf8YamlReader reader, out UInt128 value)
        {
            if (reader.TryGetLenientScalarReader(out Utf8YamlReader inner) && inner.TokenType == YamlTokenType.Number)
            {
                value = ReadCore(ref inner);
                return true;
            }
            value = default;
            return false;
        }

        private static unsafe UInt128 ReadCore(ref Utf8YamlReader reader)
        {
            int bufferLength = reader.ValueLength;

            byte[]? rentedBuffer = null;
            Span<byte> buffer = bufferLength <= YamlConstants.StackallocByteThreshold
                ? stackalloc byte[YamlConstants.StackallocByteThreshold]
                : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferLength));

            int written = reader.CopyValue(buffer);
            if (!UInt128.TryParse(buffer.Slice(0, written), CultureInfo.InvariantCulture, out UInt128 result))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt128);
            }

            if (rentedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            return result;
        }

        private static unsafe void WriteCore(Utf8YamlWriter writer, UInt128 value)
        {
            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            writer.WriteRawValue(buffer.Slice(0, written));
        }

        internal override UInt128 ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, UInt128 value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            writer.WritePlainPropertyName(buffer.Slice(0, written));
        }

        internal override UInt128 ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.String &&
                (YamlNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return ReadCore(ref reader);
            }

            if (reader.TokenType != YamlTokenType.Number)
            {
                if (TryReadLenient(ref reader, out UInt128 lenient))
                {
                    return lenient;
                }
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        internal override unsafe void WriteNumberWithCustomHandling(Utf8YamlWriter writer, UInt128 value, YamlNumberHandling handling)
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
            else
            {
                WriteCore(writer, value);
            }
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling) =>
            GetSchemaForNumericType(YamlSchemaType.Integer, numberHandling);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            GetSupportedYamlValueTypesForNumericType(numberHandling);

        private static void Format(
            Span<byte> destination,
            UInt128 value, out int written)
        {
            bool formattedSuccessfully = value.TryFormat(destination, out written, provider: CultureInfo.InvariantCulture);
            Debug.Assert(formattedSuccessfully);
        }
    }
}
