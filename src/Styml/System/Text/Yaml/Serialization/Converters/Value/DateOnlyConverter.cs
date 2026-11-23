// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class DateOnlyConverter : YamlPrimitiveConverter<DateOnly>
    {
        public const int FormatLength = 10; // YYYY-MM-DD
        public const int MaxEscapedFormatLength = FormatLength * YamlConstants.MaxExpansionFactorWhileEscaping;

        public override DateOnly Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (!reader.TokenIsStringLike)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(reader.TokenType);
            }

            // AcceptLenientScalars: a quoted or block text padded with white space or line
            // breaks reads as its trimmed text, as YamlDotNet and SharpYaml read it. The filter runs only once the read has
            // failed, so a read that succeeds pays nothing.
            try
            {
                return ReadCore(ref reader);
            }
            catch (FormatException) when (reader.TryGetLenientTrimmedReader(out Utf8YamlReader inner))
            {
                return ReadCore(ref inner);
            }
        }

        internal override DateOnly ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static unsafe DateOnly ReadCore(ref Utf8YamlReader reader)
        {
            if (!YamlHelpers.IsInRangeInclusive(reader.ValueLength, FormatLength, MaxEscapedFormatLength))
            {
                ThrowHelper.ThrowFormatException(DataType.DateOnly);
            }

            scoped ReadOnlySpan<byte> source;
            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> stackSpan = stackalloc byte[MaxEscapedFormatLength];
                int bytesWritten = reader.CopyString(stackSpan);

                // CopyString can unescape which can change the length, so we need to perform the length check again.
                if (bytesWritten < FormatLength)
                {
                    ThrowHelper.ThrowFormatException(DataType.DateOnly);
                }

                source = stackSpan.Slice(0, bytesWritten);
            }

            if (!YamlHelpers.TryParseAsIso(source, out DateOnly value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateOnly);
            }

            return value;
        }

        public override unsafe void Write(Utf8YamlWriter writer, DateOnly value, YamlSerializerOptions options)
        {
            Span<byte> buffer = stackalloc byte[FormatLength];
            bool formattedSuccessfully = value.TryFormat(buffer, out int charsWritten, "O", CultureInfo.InvariantCulture);
            Debug.Assert(formattedSuccessfully && charsWritten == FormatLength);
            if (writer.IsBlockStyle)
            {
                // Decision 2b (i): typed dates are written plain.
                writer.WriteStringValueBlockPlain(buffer);
                return;
            }

            writer.WriteStringValue(buffer);
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, DateOnly value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> buffer = stackalloc byte[FormatLength];
            bool formattedSuccessfully = value.TryFormat(buffer, out int charsWritten, "O", CultureInfo.InvariantCulture);
            Debug.Assert(formattedSuccessfully && charsWritten == FormatLength);
            writer.WritePropertyName(buffer);
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.String, Format = "date" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
