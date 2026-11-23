// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class TimeOnlyConverter : YamlPrimitiveConverter<TimeOnly>
    {
        private const int MinimumTimeOnlyFormatLength = 3; // h:m
        private const int MaximumTimeOnlyFormatLength = 16; // hh:mm:ss.fffffff
        private const int MaximumEscapedTimeOnlyFormatLength = YamlConstants.MaxExpansionFactorWhileEscaping * MaximumTimeOnlyFormatLength;

        public override TimeOnly Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
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

        internal override TimeOnly ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static unsafe TimeOnly ReadCore(ref Utf8YamlReader reader)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.String or YamlTokenType.PropertyName || reader.IsYamlScalarText); // M7c: a plain 1234 or true is read as its text (D2)

            if (!YamlHelpers.IsInRangeInclusive(reader.ValueLength, MinimumTimeOnlyFormatLength, MaximumEscapedTimeOnlyFormatLength))
            {
                ThrowHelper.ThrowFormatException(DataType.TimeOnly);
            }

            scoped ReadOnlySpan<byte> source;
            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> stackSpan = stackalloc byte[MaximumEscapedTimeOnlyFormatLength];
                int bytesWritten = reader.CopyString(stackSpan);
                source = stackSpan.Slice(0, bytesWritten);
            }

            byte firstChar = source[0];
            int firstSeparator = source.IndexOfAny((byte)'.', (byte)':');
            if (!YamlHelpers.IsDigit(firstChar) || firstSeparator < 0 || source[firstSeparator] == (byte)'.')
            {
                // Note: Utf8Parser.TryParse permits leading whitespace, negative values
                // and numbers of days so we need to exclude these cases here.
                ThrowHelper.ThrowFormatException(DataType.TimeOnly);
            }

            bool result = Utf8Parser.TryParse(source, out TimeSpan timespan, out int bytesConsumed, 'c');

            // Note: Utf8Parser.TryParse will return true for invalid input so
            // long as it starts with an integer. Example: "2021-06-18" or
            // "1$$$$$$$$$$". We need to check bytesConsumed to know if the
            // entire source was actually valid.

            if (!result || source.Length != bytesConsumed)
            {
                ThrowHelper.ThrowFormatException(DataType.TimeOnly);
            }

            Debug.Assert(TimeOnly.MinValue.ToTimeSpan() <= timespan && timespan <= TimeOnly.MaxValue.ToTimeSpan());
            return TimeOnly.FromTimeSpan(timespan);
        }

        public override unsafe void Write(Utf8YamlWriter writer, TimeOnly value, YamlSerializerOptions options)
        {
            Span<byte> output = stackalloc byte[MaximumTimeOnlyFormatLength];

            bool result = Utf8Formatter.TryFormat(value.ToTimeSpan(), output, out int bytesWritten, 'c');
            Debug.Assert(result);

            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WriteStringValue(output.Slice(0, bytesWritten));
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, TimeOnly value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> output = stackalloc byte[MaximumTimeOnlyFormatLength];

            bool result = Utf8Formatter.TryFormat(value.ToTimeSpan(), output, out int bytesWritten, 'c');
            Debug.Assert(result);

            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WritePropertyName(output.Slice(0, bytesWritten));
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.String, Format = "time" };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
