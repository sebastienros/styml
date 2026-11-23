// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class VersionConverter : YamlPrimitiveConverter<Version?>
    {
#if NET
        private const int MaximumFormattedVersionLength = 43; // 2147483647.2147483647.2147483647.2147483647
#endif

        public override Version? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType is YamlTokenType.Null || reader.IsLenientNull)
            {
                return null;
            }

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

        private static unsafe Version ReadCore(ref Utf8YamlReader reader)
        {
            Debug.Assert(reader.TokenType is YamlTokenType.String or YamlTokenType.PropertyName || reader.IsYamlScalarText); // M7c: a plain 1234 or true is read as its text (D2)

#if NET
            int bufferLength = reader.ValueLength;
            byte[]? rentedBuffer = null;
            try
            {
                Span<byte> utf8Buffer = bufferLength <= YamlConstants.StackallocByteThreshold
                    ? stackalloc byte[YamlConstants.StackallocByteThreshold]
                    : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferLength));

                int bytesWritten = reader.CopyString(utf8Buffer);
                ReadOnlySpan<byte> utf8Source = utf8Buffer.Slice(0, bytesWritten);

                // Since leading and trailing whitespaces are forbidden throughout System.Text.Yaml converters
                // we need to make sure that our input doesn't have them,
                // and if it has - we need to throw, to match behaviour of other converters
                // since Version.TryParse allows them and silently parses input to Version
                if (utf8Source.IsEmpty || IsWhiteSpaceByte(utf8Source[0]) || IsWhiteSpaceByte(utf8Source[^1]))
                {
                    ThrowHelper.ThrowFormatException(DataType.Version);
                }

                if (Version.TryParse(utf8Source, out Version? result))
                {
                    return result!;
                }
            }
            finally
            {
                if (rentedBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
#else
            string? versionString = reader.GetString();
            if (string.IsNullOrEmpty(versionString) || char.IsWhiteSpace(versionString[0]) || char.IsWhiteSpace(versionString[versionString.Length - 1]))
            {
                // Since leading and trailing whitespaces are forbidden throughout System.Text.Yaml converters
                // we need to make sure that our input doesn't have them,
                // and if it has - we need to throw, to match behaviour of other converters
                // since Version.TryParse allows them and silently parses input to Version
                ThrowHelper.ThrowFormatException(DataType.Version);
            }

            if (Version.TryParse(versionString, out Version? result))
            {
                return result;
            }
#endif
            ThrowHelper.ThrowYamlException();
            return null;
        }

#if NET
        private static bool IsWhiteSpaceByte(byte b)
        {
            // Check for ASCII whitespace characters: space (0x20), tab (0x09), newline (0x0A),
            // carriage return (0x0D), form feed (0x0C), and vertical tab (0x0B)
            // For non-ASCII bytes, convert to char and check with char.IsWhiteSpace
            return b <= 127 ? char.IsWhiteSpace((char)b) : false;
        }
#endif

        public override unsafe void Write(Utf8YamlWriter writer, Version? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

#if NET
            Span<byte> span = stackalloc byte[MaximumFormattedVersionLength];
            bool formattedSuccessfully = value.TryFormat(span, out int charsWritten);
            Debug.Assert(formattedSuccessfully);
            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WriteStringValue(span.Slice(0, charsWritten));
#else
            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WriteStringValue(value.ToString());
#endif
        }

        internal override Version ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return ReadCore(ref reader);
        }

        internal override unsafe void WriteAsPropertyNameCore(Utf8YamlWriter writer, Version value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

#if NET
            Span<byte> span = stackalloc byte[MaximumFormattedVersionLength];
            bool formattedSuccessfully = value.TryFormat(span, out int charsWritten);
            Debug.Assert(formattedSuccessfully);
            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WritePropertyName(span.Slice(0, charsWritten));
#else
            if (writer.IsBlockStyle) writer.WriteTypedTextQuoteStyle(); // always quoted (single quotes under a single-quoted default, M7b)
            writer.WritePropertyName(value.ToString());
#endif
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) =>
            new()
            {
                Type = YamlSchemaType.String,
                Comment = "Represents a version string.",
                Pattern = @"^\d+(\.\d+){1,3}$",
            };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
