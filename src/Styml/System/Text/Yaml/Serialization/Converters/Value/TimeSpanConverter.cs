// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class TimeSpanConverter : YamlPrimitiveConverter<TimeSpan>
    {
        private const int MinimumTimeSpanFormatLength = 1; // d
        private const int MaximumTimeSpanFormatLength = 26; // -dddddddd.hh:mm:ss.fffffff
        private const int MaximumEscapedTimeSpanFormatLength = YamlConstants.MaxExpansionFactorWhileEscaping * MaximumTimeSpanFormatLength;

        public override TimeSpan Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
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

        internal override TimeSpan ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static unsafe TimeSpan ReadCore(ref Utf8YamlReader reader)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.String or YamlTokenType.PropertyName);

            if (!YamlHelpers.IsInRangeInclusive(reader.ValueLength, MinimumTimeSpanFormatLength, MaximumEscapedTimeSpanFormatLength))
            {
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            scoped ReadOnlySpan<byte> source;
            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> stackSpan = stackalloc byte[MaximumEscapedTimeSpanFormatLength];
                int bytesWritten = reader.CopyString(stackSpan);
                source = stackSpan.Slice(0, bytesWritten);
            }

            byte firstChar = source[0];
            if (!YamlHelpers.IsDigit(firstChar) && firstChar != '-')
            {
                // Note: Utf8Parser.TryParse allows for leading whitespace so we
                // need to exclude that case here.
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            bool result = Utf8Parser.TryParse(source, out TimeSpan tmpValue, out int bytesConsumed, 'c');

            // Note: Utf8Parser.TryParse will return true for invalid input so
            // long as it starts with an integer. Example: "2021-06-18" or
            // "1$$$$$$$$$$". We need to check bytesConsumed to know if the
            // entire source was actually valid.

            if (!result || source.Length != bytesConsumed)
            {
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            return tmpValue;
        }

        public override unsafe void Write(Utf8YamlWriter writer, TimeSpan value, YamlSerializerOptions options)
        {
            Span<byte> output = stackalloc byte[MaximumTimeSpanFormatLength];

            bool result = Utf8Formatter.TryFormat(value, output, out int bytesWritten, 'c');
            Debug.Assert(result);

            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WriteStringValue(output.Slice(0, bytesWritten));
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, TimeSpan value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> output = stackalloc byte[MaximumTimeSpanFormatLength];

            bool result = Utf8Formatter.TryFormat(value, output, out int bytesWritten, 'c');
            Debug.Assert(result);

            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WritePropertyName(output.Slice(0, bytesWritten));
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new()
        {
            Type = YamlSchemaType.String,
            Comment = "Represents a System.TimeSpan value.",
            Pattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"
        };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
