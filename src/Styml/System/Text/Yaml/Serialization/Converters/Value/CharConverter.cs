// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class CharConverter : YamlPrimitiveConverter<char>
    {
        private const int MaxEscapedCharacterLength = YamlConstants.MaxExpansionFactorWhileEscaping;

        public override char Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            // AcceptLenientScalars: one character followed by white space or line breaks
            // ('x ', "x\n", a | block) reads as that character, as YamlDotNet reads it. A leading space is not trimmed:
            // YamlDotNet reads ' x ' as the space, by taking the first character, so neither library reads it as 'x'. The
            // filter runs only once the read has failed.
            try
            {
                return ReadCore(ref reader);
            }
            catch (InvalidOperationException) when (reader.TryGetLenientTrimmedReader(out Utf8YamlReader inner) && FirstCharacterIs(ref reader, ref inner))
            {
                return ReadCore(ref inner);
            }
        }

        // The trimmed text starts where the text does: only trailing white space was dropped.
        private static bool FirstCharacterIs(ref Utf8YamlReader reader, ref Utf8YamlReader inner)
        {
            string? text = reader.GetString();
            string? trimmed = inner.GetString();
            return text is { Length: > 0 } && trimmed is { Length: > 0 } && text[0] == trimmed[0];
        }

        private static unsafe char ReadCore(ref Utf8YamlReader reader)
        {
            if (reader.TokenType is not YamlTokenType.PropertyName && !reader.TokenIsStringLike)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(reader.TokenType);
            }

            if (!YamlHelpers.IsInRangeInclusive(reader.ValueLength, 1, MaxEscapedCharacterLength))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedChar(reader.TokenType);
            }

            Span<char> buffer = stackalloc char[MaxEscapedCharacterLength];
            int charsWritten = reader.CopyString(buffer);

            if (charsWritten != 1)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedChar(reader.TokenType);
            }

            return buffer[0];
        }

        public override void Write(Utf8YamlWriter writer, char value, YamlSerializerOptions options)
        {
            writer.WriteStringValue(
#if NET
                new ReadOnlySpan<char>(in value)
#else
                value.ToString()
#endif
                );
        }

        internal override char ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
            return Read(ref reader, typeToConvert, options);
        }

        internal override void WriteAsPropertyNameCore(Utf8YamlWriter writer, char value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(
#if NET
                new ReadOnlySpan<char>(in value)
#else
                value.ToString()
#endif
                );
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) =>
            new() { Type = YamlSchemaType.String, MinLength = 1, MaxLength = 1 };

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.String;
    }
}
