// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the input as YAML content. It is expected that the input content is a single complete YAML value.
        /// </summary>
        /// <param name="yaml">The raw YAML content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is a well-formed YAML payload.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="yaml"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than 715,827,882 (<see cref="int.MaxValue"/> / 3).</exception>
        /// <exception cref="YamlException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single YAML value according to the YAML 1.2 specification (https://yaml.org/spec/1.2.2/)
        /// or the input YAML exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused YAML values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid YAML
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="YamlWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="YamlWriterOptions.Indented"/> and <see cref="YamlWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue([StringSyntax(StringSyntaxAttribute.Json)] string yaml, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            ArgumentNullException.ThrowIfNull(yaml);

            TranscodeAndWriteRawValue(yaml.AsSpan(), skipInputValidation);
        }

        /// <summary>
        /// Writes the input as YAML content. It is expected that the input content is a single complete YAML value.
        /// </summary>
        /// <param name="yaml">The raw YAML content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is a well-formed YAML payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than 715,827,882 (<see cref="int.MaxValue"/> / 3).</exception>
        /// <exception cref="YamlException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single YAML value according to the YAML 1.2 specification (https://yaml.org/spec/1.2.2/)
        /// or the input YAML exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused YAML values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid YAML
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="YamlWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="YamlWriterOptions.Indented"/> and <see cref="YamlWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            TranscodeAndWriteRawValue(yaml, skipInputValidation);
        }

        /// <summary>
        /// Writes the input as YAML content. It is expected that the input content is a single complete YAML value.
        /// </summary>
        /// <param name="utf8Yaml">The raw YAML content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is a well-formed YAML payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than or equal to <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="YamlException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single YAML value according to the YAML 1.2 specification (https://yaml.org/spec/1.2.2/)
        /// or the input YAML exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused YAML values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid YAML
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="YamlWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="YamlWriterOptions.Indented"/> and <see cref="YamlWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue(ReadOnlySpan<byte> utf8Yaml, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (utf8Yaml.Length == int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(int.MaxValue);
            }

            WriteRawValueCore(utf8Yaml, skipInputValidation);
        }

        /// <summary>
        /// Writes the input as YAML content. It is expected that the input content is a single complete YAML value.
        /// </summary>
        /// <param name="utf8Yaml">The raw YAML content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is a well-formed YAML payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or equal to <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="YamlException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single YAML value according to the YAML 1.2 specification (https://yaml.org/spec/1.2.2/)
        /// or the input YAML exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused YAML values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid YAML
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="YamlWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="YamlWriterOptions.Indented"/> and <see cref="YamlWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue(ReadOnlySequence<byte> utf8Yaml, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            long utf8YamlLen = utf8Yaml.Length;

            if (utf8YamlLen == 0)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedYamlTokens);
            }
            if (utf8YamlLen >= int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(utf8YamlLen);
            }

            if (_style == WriterStyleBlock)
            {
                byte[] contiguous = ArrayPool<byte>.Shared.Rent((int)utf8YamlLen);
                try
                {
                    utf8Yaml.CopyTo(contiguous);
                    WriteRawValueBlock(contiguous.AsSpan(0, (int)utf8YamlLen), skipInputValidation);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(contiguous);
                }

                SetFlagToAddListSeparatorBeforeNextItem();
                return;
            }

            if (skipInputValidation)
            {
                // Treat all unvalidated raw JSON value writes as string. If the payload is valid, this approach does
                // not affect structural validation since a string token is equivalent to a complete object, array,
                // or other complete JSON tokens when considering structural validation on subsequent writer calls.
                // If the payload is not valid, then we make no guarantees about the structural validation of the final payload.
                _tokenType = YamlTokenType.String;
            }
            else
            {
                // Utilize reader validation.
                Utf8YamlReader reader = new(utf8Yaml);
                while (reader.Read());
                _tokenType = reader.TokenType;
            }

            Debug.Assert(utf8YamlLen < int.MaxValue);
            int len = (int)utf8YamlLen;

            // TODO (https://github.com/dotnet/runtime/issues/29293):
            // investigate writing this in chunks, rather than requesting one potentially long, contiguous buffer.
            int maxRequired = len + 1; // Optionally, 1 list separator. We've guarded against integer overflow earlier in the call stack.

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            utf8Yaml.CopyTo(output.Slice(BytesPending));
            BytesPending += len;

            SetFlagToAddListSeparatorBeforeNextItem();
        }

        private unsafe void TranscodeAndWriteRawValue(ReadOnlySpan<char> yaml, bool skipInputValidation)
        {
            if (yaml.Length > YamlConstants.MaxUtf16RawValueLength)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(yaml.Length);
            }

            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8Yaml =
                // Use stack memory
                yaml.Length <= (YamlConstants.StackallocByteThreshold / YamlConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[YamlConstants.StackallocByteThreshold] :
                // Use a pooled array
                yaml.Length <= (YamlConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / YamlConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(yaml.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[YamlReaderHelper.GetUtf8ByteCount(yaml)];

            try
            {
                int actualByteCount = YamlReaderHelper.GetUtf8FromText(yaml, utf8Yaml);
                utf8Yaml = utf8Yaml.Slice(0, actualByteCount);
                WriteRawValueCore(utf8Yaml, skipInputValidation);
            }
            finally
            {
                if (tempArray is not null)
                {
                    utf8Yaml.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }

        private void WriteRawValueCore(ReadOnlySpan<byte> utf8Yaml, bool skipInputValidation)
        {
            int len = utf8Yaml.Length;

            if (len == 0)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedYamlTokens);
            }

            // In the UTF-16-based entry point methods above, we validate that the payload length <= int.MaxValue /3.
            // The result of this division will be rounded down, so even if every input character needs to be transcoded
            // (with expansion factor of 3), the resulting payload would be less than int.MaxValue,
            // as (int.MaxValue/3) * 3 is less than int.MaxValue.
            Debug.Assert(len < int.MaxValue);

            if (_style == WriterStyleBlock)
            {
                WriteRawValueBlock(utf8Yaml, skipInputValidation);
                SetFlagToAddListSeparatorBeforeNextItem();
                return;
            }

            if (skipInputValidation)
            {
                // Treat all unvalidated raw JSON value writes as string. If the payload is valid, this approach does
                // not affect structural validation since a string token is equivalent to a complete object, array,
                // or other complete JSON tokens when considering structural validation on subsequent writer calls.
                // If the payload is not valid, then we make no guarantees about the structural validation of the final payload.
                _tokenType = YamlTokenType.String;
            }
            else
            {
                // Utilize reader validation.
                Utf8YamlReader reader = new(utf8Yaml);
                while (reader.Read());
                _tokenType = reader.TokenType;
            }

            // TODO (https://github.com/dotnet/runtime/issues/29293):
            // investigate writing this in chunks, rather than requesting one potentially long, contiguous buffer.
            int maxRequired = len + 1; // Optionally, 1 list separator. We've guarded against integer overflow earlier in the call stack.

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            utf8Yaml.CopyTo(output.Slice(BytesPending));
            BytesPending += len;

            SetFlagToAddListSeparatorBeforeNextItem();
        }
    }
}
