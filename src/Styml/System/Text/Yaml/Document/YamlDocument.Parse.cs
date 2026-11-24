// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // Cached unrented documents for literal values.
        private static YamlDocument? s_nullLiteral;
        private static YamlDocument? s_trueLiteral;
        private static YamlDocument? s_falseLiteral;

        private const int UnseekableStreamInitialRentSize = 4096;

        /// <summary>
        ///   Parse memory as UTF-8 encoded text representing a single YAML value into a YamlDocument.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The <see cref="ReadOnlyMemory{T}"/> value will be used for the entire lifetime of the
        ///     YamlDocument object, and the caller must ensure that the data therein does not change during
        ///     the object lifetime.
        ///   </para>
        ///
        ///   <para>
        ///     Because the input is considered to be text, a UTF-8 Byte-Order-Mark (BOM) must not be present.
        ///   </para>
        /// </remarks>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>
        ///   A YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static YamlDocument Parse(ReadOnlyMemory<byte> utf8Yaml, YamlDocumentOptions options = default)
        {
            return ApplyMergeKeys(Parse(utf8Yaml, options.GetReaderOptions(), duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
        }

        /// <summary>
        ///   Parse a sequence as UTF-8 encoded text representing a single YAML value into a YamlDocument.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The <see cref="ReadOnlySequence{T}"/> may be used for the entire lifetime of the
        ///     YamlDocument object, and the caller must ensure that the data therein does not change during
        ///     the object lifetime.
        ///   </para>
        ///
        ///   <para>
        ///     Because the input is considered to be text, a UTF-8 Byte-Order-Mark (BOM) must not be present.
        ///   </para>
        /// </remarks>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>
        ///   A YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static YamlDocument Parse(ReadOnlySequence<byte> utf8Yaml, YamlDocumentOptions options = default)
        {
            YamlReaderOptions readerOptions = options.GetReaderOptions();

            if (utf8Yaml.IsSingleSegment)
            {
                return ApplyMergeKeys(Parse(utf8Yaml.First, readerOptions, duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
            }

            int length = checked((int)utf8Yaml.Length);
            byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(length);

            try
            {
                utf8Yaml.CopyTo(utf8Bytes.AsSpan());
                return ApplyMergeKeys(Parse(
                    utf8Bytes.AsMemory(0, length),
                    readerOptions,
                    extraRentedArrayPoolBytes: utf8Bytes,
                    duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
            }
            catch
            {
                // Holds document content, clear it before returning it.
                utf8Bytes.AsSpan(0, length).Clear();
                ArrayPool<byte>.Shared.Return(utf8Bytes);
                throw;
            }
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single YAML value into a
        ///   YamlDocument.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>
        ///   A YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static YamlDocument Parse(Stream utf8Yaml, YamlDocumentOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            ArraySegment<byte> drained = ReadToEnd(utf8Yaml);
            Debug.Assert(drained.Array is not null);
            try
            {
                return ApplyMergeKeys(Parse(
                    drained.AsMemory(),
                    options.GetReaderOptions(),
                    extraRentedArrayPoolBytes: drained.Array,
                    duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
            }
            catch
            {
                // Holds document content, clear it before returning it.
                drained.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(drained.Array);
                throw;
            }
        }

        internal static YamlDocument ParseRented(PooledByteBufferWriter utf8Yaml, YamlDocumentOptions options = default)
        {
            return Parse(
                utf8Yaml.WrittenMemory,
                options.GetReaderOptions(),
                extraRentedArrayPoolBytes: null,
                extraPooledByteBufferWriter: utf8Yaml,
                duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion);
        }

        internal static YamlDocument ParseValue(Stream utf8Yaml, YamlDocumentOptions options)
        {
            Debug.Assert(utf8Yaml is not null);

            ArraySegment<byte> drained = ReadToEnd(utf8Yaml);
            Debug.Assert(drained.Array is not null);

            byte[] owned = new byte[drained.Count];
            Buffer.BlockCopy(drained.Array, 0, owned, 0, drained.Count);

            // Holds document content, clear it before returning it.
            drained.AsSpan().Clear();
            ArrayPool<byte>.Shared.Return(drained.Array);

            return ApplyMergeKeys(ParseUnrented(
                owned.AsMemory(),
                options.GetReaderOptions(),
                duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
        }

        internal static YamlDocument ParseValue(ReadOnlySpan<byte> utf8Yaml, YamlDocumentOptions options)
        {
            byte[] owned = new byte[utf8Yaml.Length];
            utf8Yaml.CopyTo(owned);

            return ApplyMergeKeys(ParseUnrented(
                owned.AsMemory(),
                options.GetReaderOptions(),
                duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
        }

        internal static YamlDocument ParseValue(string yaml, YamlDocumentOptions options)
        {
            Debug.Assert(yaml is not null);
            return ParseValue(yaml.AsSpan(), options);
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single YAML value into a
        ///   YamlDocument.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        ///   A Task to produce a YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static Task<YamlDocument> ParseAsync(
            Stream utf8Yaml,
            YamlDocumentOptions options = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            return ParseAsyncCore(utf8Yaml, options, cancellationToken);
        }

        private static async Task<YamlDocument> ParseAsyncCore(
            Stream utf8Yaml,
            YamlDocumentOptions options = default,
            CancellationToken cancellationToken = default)
        {
            ArraySegment<byte> drained = await ReadToEndAsync(utf8Yaml, cancellationToken).ConfigureAwait(false);
            Debug.Assert(drained.Array is not null);
            try
            {
                return ApplyMergeKeys(Parse(
                    drained.AsMemory(),
                    options.GetReaderOptions(),
                    extraRentedArrayPoolBytes: drained.Array,
                    duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
            }
            catch
            {
                // Holds document content, clear it before returning it.
                drained.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(drained.Array);
                throw;
            }
        }

        internal static async Task<YamlDocument> ParseAsyncCoreUnrented(
            Stream utf8Yaml,
            YamlDocumentOptions options = default,
            CancellationToken cancellationToken = default)
        {
            ArraySegment<byte> drained = await ReadToEndAsync(utf8Yaml, cancellationToken).ConfigureAwait(false);
            Debug.Assert(drained.Array is not null);

            byte[] owned = new byte[drained.Count];
            Buffer.BlockCopy(drained.Array, 0, owned, 0, drained.Count);

            // Holds document content, clear it before returning it.
            drained.AsSpan().Clear();
            ArrayPool<byte>.Shared.Return(drained.Array);

            return ParseUnrented(
                owned.AsMemory(),
                options.GetReaderOptions(),
                duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion);
        }

        /// <summary>
        ///   Parses text representing a single YAML value into a YamlDocument.
        /// </summary>
        /// <remarks>
        ///   The <see cref="ReadOnlyMemory{T}"/> value may be used for the entire lifetime of the
        ///   YamlDocument object, and the caller must ensure that the data therein does not change during
        ///   the object lifetime.
        /// </remarks>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>
        ///   A YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static YamlDocument Parse([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlyMemory<char> yaml, YamlDocumentOptions options = default)
        {
            ReadOnlySpan<char> yamlChars = yaml.Span;
            int expectedByteCount = YamlReaderHelper.GetUtf8ByteCount(yamlChars);
            byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(expectedByteCount);

            try
            {
                int actualByteCount = YamlReaderHelper.GetUtf8FromText(yamlChars, utf8Bytes);
                Debug.Assert(expectedByteCount == actualByteCount);

                return ApplyMergeKeys(Parse(
                    utf8Bytes.AsMemory(0, actualByteCount),
                    options.GetReaderOptions(),
                    extraRentedArrayPoolBytes: utf8Bytes,
                    duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
            }
            catch
            {
                // Holds document content, clear it before returning it.
                utf8Bytes.AsSpan(0, expectedByteCount).Clear();
                ArrayPool<byte>.Shared.Return(utf8Bytes);
                throw;
            }
        }

        internal static YamlDocument ParseValue(ReadOnlySpan<char> yaml, YamlDocumentOptions options)
        {
            int expectedByteCount = YamlReaderHelper.GetUtf8ByteCount(yaml);
            byte[] owned;
            byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(expectedByteCount);

            try
            {
                int actualByteCount = YamlReaderHelper.GetUtf8FromText(yaml, utf8Bytes);
                Debug.Assert(expectedByteCount == actualByteCount);

                owned = new byte[actualByteCount];
                Buffer.BlockCopy(utf8Bytes, 0, owned, 0, actualByteCount);
            }
            finally
            {
                // Holds document content, clear it before returning it.
                utf8Bytes.AsSpan(0, expectedByteCount).Clear();
                ArrayPool<byte>.Shared.Return(utf8Bytes);
            }

            return ApplyMergeKeys(ParseUnrented(
                owned.AsMemory(),
                options.GetReaderOptions(),
                duplicateKeyHandling: options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion), options);
        }

        /// <summary>
        ///   Parses text representing a single YAML value into a YamlDocument.
        /// </summary>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>
        ///   A YamlDocument representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="yaml"/> does not represent a valid single YAML value.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="options"/> contains unsupported options.
        /// </exception>
        public static YamlDocument Parse([StringSyntax(StringSyntaxAttribute.Json)] string yaml, YamlDocumentOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(yaml);

            return Parse(yaml.AsMemory(), options);
        }

        /// <summary>
        ///   Attempts to parse one YAML value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <param name="document">Receives the parsed document.</param>
        /// <returns>
        ///   <see langword="true"/> if a value was read and parsed into a YamlDocument,
        ///   <see langword="false"/> if the reader ran out of data while parsing.
        ///   All other situations result in an exception being thrown.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value.  If an exception is thrown, or <see langword="false"/>
        ///     is returned, the reader is reset to the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="YamlException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static bool TryParseValue(ref Utf8YamlReader reader, [NotNullWhen(true)] out YamlDocument? document)
        {
            return TryParseValue(ref reader, out document, shouldThrow: false, useArrayPools: true);
        }

        /// <summary>
        ///   Parses one YAML value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <returns>
        ///   A YamlDocument representing the value (and nested values) read from the reader.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="YamlException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static YamlDocument ParseValue(ref Utf8YamlReader reader) =>
            ParseValue(ref reader, YamlDuplicateKeyHandling.LastWins);

        internal static YamlDocument ParseValue(ref Utf8YamlReader reader, YamlDuplicateKeyHandling duplicateKeyHandling)
        {
            bool ret = TryParseValue(ref reader, out YamlDocument? document, shouldThrow: true, useArrayPools: true, duplicateKeyHandling);

            Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
            Debug.Assert(document is not null, "null document returned with shouldThrow: true.");
            return document;
        }

        internal static bool TryParseValue(
            ref Utf8YamlReader reader,
            [NotNullWhen(true)] out YamlDocument? document,
            bool shouldThrow,
            bool useArrayPools,
            YamlDuplicateKeyHandling duplicateKeyHandling = YamlDuplicateKeyHandling.LastWins)
        {
            YamlReaderState state = reader.CurrentState;
            CheckSupportedOptions(state.Options, nameof(reader));

            // Value copy to overwrite the ref on an exception and undo the destructive reads.
            Utf8YamlReader restore = reader;

            ReadOnlySpan<byte> valueSpan = default;
            ReadOnlySequence<byte> valueSequence = default;
            int padPrefix = 0;
            string? rootTag = null;
            byte rootDocumentLine = reader.YamlDocumentLineFlags;
            string? rootExpandedTag = null;

            // M5b: a reader scoped to one value (a replay, a nested Deserialize) starts at the value's first byte, so the
            // line's prefix before a block collection is not in its buffer: the copy then starts with that many spaces.
            int prepend = 0;

            try
            {
                switch (reader.TokenType)
                {
                    // A new reader was created and has never been read,
                    // so we need to move to the first token.
                    // (or a reader has terminated and we're about to throw)
                    case YamlTokenType.None:
                    // Using a reader loop the caller has identified a property they wish to
                    // hydrate into a YamlDocument. Move to the value first.
                    case YamlTokenType.PropertyName:
                    {
                        if (!reader.Read())
                        {
                            if (shouldThrow)
                            {
                                ThrowHelper.ThrowYamlReaderException(
                                    ref reader,
                                    ExceptionResource.ExpectedYamlTokens);
                            }

                            reader = restore;
                            document = null;
                            return false;
                        }

                        break;
                    }
                }

                // M5d: a tagged collection or non-string scalar keeps its tag; the copy below starts after it.
                if (!reader.Tag.IsEmpty)
                {
                    rootTag = YamlReaderHelper.TranscodeHelper(reader.Tag);
                    rootExpandedTag = reader.GetExpandedTag();
                }

                // M5a: a YAML value. A block collection is copied from the
                // start of its first line, with what precedes it on that line ("- ", "key: ") turned into spaces, so its
                // lines keep their columns and the copy reads as an indented root node. A scalar that is not a JSON
                // string is built from the reader's token, not re-read, since its indentation context would change.
                switch (reader.TokenType)
                {
                    case YamlTokenType.Alias:
                        // The serializer resolves aliases in M5b (decision D5); until then it refuses them.
                        ThrowHelper.ThrowNotSupportedException_YamlAlias();
                        break;
                    case YamlTokenType.String when reader.ScalarStyle != YamlScalarStyle.DoubleQuoted || reader.YamlNeedsProcessing:
                        document = CreateForYamlString(ref reader);
                        document.AttachRootDocumentLine(rootDocumentLine);
                        return true;
                    case YamlTokenType.Null when reader.ValueSpan.IsEmpty && !reader.HasValueSequence:
                        document = CreateForLiteral(YamlTokenType.Null);
                        return true;
                    case YamlTokenType.StartObject or YamlTokenType.StartArray when !reader.IsFlow:
                        padPrefix = checked((int)reader.YamlRawTokenColumn);
                        break;
                }

                switch (reader.TokenType)
                {
                    // Any of the "value start" states are acceptable.
                    case YamlTokenType.StartObject:
                    case YamlTokenType.StartArray:
                    {
                        if (padPrefix > reader.YamlRawTokenStart)
                        {
                            prepend = padPrefix;
                        }

                        long startingOffset = padPrefix > 0 ? reader.YamlRawTokenStart - padPrefix + prepend : reader.TokenStartIndex;

                        if (!reader.TrySkip())
                        {
                            if (shouldThrow)
                            {
                                ThrowHelper.ThrowYamlReaderException(
                                    ref reader,
                                    ExceptionResource.ExpectedYamlTokens);
                            }

                            reader = restore;
                            document = null;
                            return false;
                        }

                        long totalLength = reader.BytesConsumed - startingOffset;
                        ReadOnlySequence<byte> sequence = reader.OriginalSequence;

                        if (sequence.IsEmpty)
                        {
                            valueSpan = reader.OriginalSpan.Slice(
                                checked((int)startingOffset),
                                checked((int)totalLength));
                        }
                        else
                        {
                            valueSequence = sequence.Slice(startingOffset, totalLength);
                        }

                        Debug.Assert(
                            reader.TokenType == YamlTokenType.EndObject ||
                            reader.TokenType == YamlTokenType.EndArray);

                        break;
                    }

                    case YamlTokenType.False:
                    case YamlTokenType.True:
                    case YamlTokenType.Null:
                        if (useArrayPools)
                        {
                            if (reader.HasValueSequence)
                            {
                                valueSequence = reader.ValueSequence;
                            }
                            else
                            {
                                valueSpan = reader.ValueSpan;
                            }

                            break;
                        }

                        document = CreateForLiteral(reader.TokenType, cached: rootTag is null && rootDocumentLine == 0);
                        if (rootTag is not null)
                        {
                            document.AttachRootTag(rootTag, rootExpandedTag);
                        }

                        document.AttachRootDocumentLine(rootDocumentLine);
                        return true;

                    case YamlTokenType.Number:
                    {
                        if (reader.HasValueSequence)
                        {
                            valueSequence = reader.ValueSequence;
                        }
                        else
                        {
                            valueSpan = reader.ValueSpan;
                        }

                        break;
                    }

                    // String's ValueSequence/ValueSpan omits the quotes, we need them back.
                    case YamlTokenType.String:
                    {
                        ReadOnlySequence<byte> sequence = reader.OriginalSequence;

                        if (sequence.IsEmpty)
                        {
                            // Since the quoted string fit in a ReadOnlySpan originally
                            // the contents length plus the two quotes can't overflow.
                            int payloadLength = reader.ValueSpan.Length + 2;
                            Debug.Assert(payloadLength > 1);

                            ReadOnlySpan<byte> readerSpan = reader.OriginalSpan;

                            Debug.Assert(
                                readerSpan[(int)reader.TokenStartIndex] == (byte)'"',
                                $"Calculated span starts with {readerSpan[(int)reader.TokenStartIndex]}");

                            Debug.Assert(
                                readerSpan[(int)reader.TokenStartIndex + payloadLength - 1] == (byte)'"',
                                $"Calculated span ends with {readerSpan[(int)reader.TokenStartIndex + payloadLength - 1]}");

                            valueSpan = readerSpan.Slice((int)reader.TokenStartIndex, payloadLength);
                        }
                        else
                        {
                            long payloadLength = 2;

                            if (reader.HasValueSequence)
                            {
                                payloadLength += reader.ValueSequence.Length;
                            }
                            else
                            {
                                payloadLength += reader.ValueSpan.Length;
                            }

                            valueSequence = sequence.Slice(reader.TokenStartIndex, payloadLength);
                            Debug.Assert(
                                valueSequence.First.Span[0] == (byte)'"',
                                $"Calculated sequence starts with {valueSequence.First.Span[0]}");

                            Debug.Assert(
                                valueSequence.ToArray()[payloadLength - 1] == (byte)'"',
                                $"Calculated sequence ends with {valueSequence.ToArray()[payloadLength - 1]}");
                        }

                        break;
                    }
                    default:
                    {
                        if (shouldThrow)
                        {
                            // Default case would only hit if TokenType equals YamlTokenType.EndObject or YamlTokenType.EndArray in which case it would never be sequence
                            Debug.Assert(!reader.HasValueSequence);
                            byte displayByte = reader.ValueSpan[0];

                            ThrowHelper.ThrowYamlReaderException(
                                ref reader,
                                ExceptionResource.ExpectedStartOfValueNotFound,
                                displayByte);
                        }

                        reader = restore;
                        document = null;
                        return false;
                    }
                }
            }
            catch
            {
                reader = restore;
                throw;
            }

            int length = (valueSpan.IsEmpty ? checked((int)valueSequence.Length) : valueSpan.Length) + prepend;

            // M5c: a lone number, boolean or null is copied without its tag, and the reader has already resolved it; under
            // Json or Failsafe the bare text would not read as that kind (Failsafe's 1 is a string, Json's 0x1F an error),
            // so the copy is read as Core, which gives the same kind and value.
            YamlReaderOptions copyOptions = state.Options;
            if (reader.TokenType is YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null
                && copyOptions.Schema is YamlSchemaKind.Json or YamlSchemaKind.Failsafe)
            {
                copyOptions.Schema = YamlSchemaKind.Core;
            }

            if (useArrayPools)
            {
                byte[] rented = ArrayPool<byte>.Shared.Rent(length);
                Span<byte> rentedSpan = rented.AsSpan(0, length);

                try
                {
                    if (valueSpan.IsEmpty)
                    {
                        valueSequence.CopyTo(rentedSpan.Slice(prepend));
                    }
                    else
                    {
                        valueSpan.CopyTo(rentedSpan.Slice(prepend));
                    }

                    rentedSpan.Slice(0, padPrefix).Fill((byte)' ');
                    BlankUnityStrippedMarker(rentedSpan, rootDocumentLine);

                    document = Parse(rented.AsMemory(0, length), copyOptions, duplicateKeyHandling, rented, tagDirectives: reader.YamlTagDirectives);
                }
                catch
                {
                    // This really shouldn't happen since the document was already checked
                    // for consistency by Skip.  But if data mutations happened just after
                    // the calls to Read then the copy may not be valid.
                    rentedSpan.Clear();
                    ArrayPool<byte>.Shared.Return(rented);
                    throw;
                }
            }
            else
            {
                byte[] owned = new byte[length];
                if (valueSpan.IsEmpty)
                {
                    valueSequence.CopyTo(owned.AsSpan(prepend));
                }
                else
                {
                    valueSpan.CopyTo(owned.AsSpan(prepend));
                }

                owned.AsSpan(0, padPrefix).Fill((byte)' ');
                BlankUnityStrippedMarker(owned, rootDocumentLine);

                document = ParseUnrented(owned, copyOptions, duplicateKeyHandling, reader.TokenType, tagDirectives: reader.YamlTagDirectives);
            }

            if (rootTag is not null)
            {
                document.AttachRootTag(rootTag, rootExpandedTag);
            }

            document.AttachRootDocumentLine(rootDocumentLine);
            return true;
        }

        // The copy of a root read with Unity's stripped marker starts on its properties' line, which holds the marker; the
        // copy is not on a '---' line, where the marker is read, so it is turned into spaces (the flag is attached).
        private static void BlankUnityStrippedMarker(Span<byte> copy, byte rootDocumentLine)
        {
            if ((rootDocumentLine & Utf8YamlReader.UnityStripped) == 0)
            {
                return;
            }

            int lineEnd = copy.IndexOfAny((byte)'\n', (byte)'\r');
            Span<byte> line = lineEnd < 0 ? copy : copy.Slice(0, lineEnd);
            for (int i = line.Length - 8; i > 0; i--)
            {
                if (line[i - 1] is (byte)' ' or (byte)'\t' && line.Slice(i, 8).SequenceEqual("stripped"u8)
                    && (i + 8 == line.Length || line[i + 8] is (byte)' ' or (byte)'\t' or (byte)'#'))
                {
                    line.Slice(i, 8).Fill((byte)' ');
                    return;
                }
            }
        }

        /// <summary>
        /// A document for the reader's current YAML string that is not a JSON string (plain, single-quoted, block, or
        /// processed): one row over the string's value. Its raw text is the value.
        /// </summary>
        private static YamlDocument CreateForYamlString(ref Utf8YamlReader reader)
        {
            byte[] value = reader.YamlNeedsProcessing
                ? reader.GetYamlProcessedValue()
                : reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray();

            // The payload is the value double-quoted, with JSON's escapes, so that the element's raw text reads back as
            // the same string ('true', '12', '~', '' and block scalars included), as YamlDocument.Parse's does.
            byte[] payload = DoubleQuote(value, out bool escaped);
            string? anchor = reader.Anchor.IsEmpty ? null : YamlReaderHelper.TranscodeHelper(reader.Anchor);
            string? tag = reader.Tag.IsEmpty ? null : YamlReaderHelper.TranscodeHelper(reader.Tag);
            YamlTables? tables = null;
            AddProperties(ref tables, 0, anchor, tag, tag is null ? null : reader.GetExpandedTag());

            // M5f: the row keeps the source's style (single-quoted, literal or folded; the payload's own form does
            // not depend on it), so that the element is written back in it. A plain source keeps no style: the writer's
            // choice stays, since a plain hint skips the resolution check and the element may be read again under
            // another schema (review 6).
            YamlScalarStyle style = reader.ScalarStyle is YamlScalarStyle.Plain ? YamlScalarStyle.None : reader.ScalarStyle;
            MetadataDb database = MetadataDb.CreateLocked(payload.Length);
            database.Append(YamlTokenType.String, startLocation: 1, payload.Length - 2, DbRow.StyleBits(style), hasNodeInfo: tables is not null);
            if (escaped)
            {
                database.SetHasComplexChildren(0);
            }

            return new YamlDocument(payload, database, isDisposable: false, yaml: tables);
        }

        /// <summary><paramref name="value"/> as a double-quoted scalar with JSON's escapes, which YAML reads alike.</summary>
        private static byte[] DoubleQuote(byte[] value, out bool escaped)
        {
            int extra = 0;
            foreach (byte b in value)
            {
                extra += b is (byte)'"' or (byte)'\\' ? 1 : b < 0x20 || b == 0x7F ? 5 : 0;
            }

            escaped = extra > 0;
            byte[] payload = new byte[value.Length + extra + 2];
            int i = 0;
            payload[i++] = (byte)'"';
            foreach (byte b in value)
            {
                if (b is (byte)'"' or (byte)'\\')
                {
                    payload[i++] = (byte)'\\';
                    payload[i++] = b;
                }
                else if (b < 0x20 || b == 0x7F)
                {
                    const string Hex = "0123456789ABCDEF";
                    payload[i++] = (byte)'\\';
                    payload[i++] = (byte)'u';
                    payload[i++] = (byte)'0';
                    payload[i++] = (byte)'0';
                    payload[i++] = (byte)Hex[b >> 4];
                    payload[i++] = (byte)Hex[b & 0xF];
                }
                else
                {
                    payload[i++] = b;
                }
            }

            payload[i] = (byte)'"';
            return payload;
        }

        private static void SetYamlOptions(YamlTables? tables, YamlReaderOptions readerOptions, int maxAliasExpansion)
        {
            if (tables is not null)
            {
                tables.Schema = readerOptions.Schema;
                tables.MaxAliasExpansion = maxAliasExpansion;
                tables.TraversalMaxDepth = readerOptions.MaxDepth == 0 ? YamlDocumentOptions.DefaultMaxDepth : readerOptions.MaxDepth;
            }
        }

        private static YamlDocument CreateForLiteral(YamlTokenType tokenType, bool cached = true)
        {
            if (!cached)
            {
                // M5d: a tagged literal gets its own document, since the tag is attached to it.
                return Create((tokenType == YamlTokenType.False ? YamlConstants.FalseValue : tokenType == YamlTokenType.True ? YamlConstants.TrueValue : YamlConstants.NullValue).ToArray());
            }

            switch (tokenType)
            {
                case YamlTokenType.False:
                    s_falseLiteral ??= Create(YamlConstants.FalseValue.ToArray());
                    return s_falseLiteral;
                case YamlTokenType.True:
                    s_trueLiteral ??= Create(YamlConstants.TrueValue.ToArray());
                    return s_trueLiteral;
                default:
                    Debug.Assert(tokenType == YamlTokenType.Null);
                    s_nullLiteral ??= Create(YamlConstants.NullValue.ToArray());
                    return s_nullLiteral;
            }

            YamlDocument Create(byte[] utf8Yaml)
            {
                MetadataDb database = MetadataDb.CreateLocked(utf8Yaml.Length);
                database.Append(tokenType, startLocation: 0, utf8Yaml.Length);
                return new YamlDocument(utf8Yaml, database, isDisposable: false);
            }
        }

        private static YamlDocument Parse(
            ReadOnlyMemory<byte> utf8Yaml,
            YamlReaderOptions readerOptions,
            YamlDuplicateKeyHandling duplicateKeyHandling,
            byte[]? extraRentedArrayPoolBytes = null,
            PooledByteBufferWriter? extraPooledByteBufferWriter = null,
            int maxAliasExpansion = 0,
            YamlTagDirectives? tagDirectives = null)
        {
            ReadOnlySpan<byte> utf8YamlSpan = utf8Yaml.Span;
            var database = MetadataDb.CreateRented(utf8Yaml.Length, convertToAlloc: false);
            var stack = new StackRowStack(YamlDocumentOptions.DefaultMaxDepth * StackRow.Size);
            YamlDocument document;
            byte[]? combinedRented = null;

            try
            {
                Parse(utf8YamlSpan, readerOptions, ref database, ref stack, out YamlTables? tables, tagDirectives);
                ReadOnlyMemory<byte> payload = AttachDecoded(utf8Yaml, tables, rent: true, out combinedRented);
                SetYamlOptions(tables, readerOptions, maxAliasExpansion);
                document = new YamlDocument(payload, database, extraRentedArrayPoolBytes, extraPooledByteBufferWriter, isDisposable: true, tables);

                document.ParsedDuplicateKeyHandling = duplicateKeyHandling;
                document.ParsedSchema = readerOptions.Schema;
                if (duplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                {
                    ValidateNoDuplicateProperties(document);
                }
            }
            catch
            {
                // The caller returns any resources they rented, so all we need to do is dispose the database (and the
                // combined copy of a YAML document, which is ours). Specifically: don't dispose the document as that
                // will result in double return of the rented array.
                database.Dispose();
                if (combinedRented is not null)
                {
                    ArrayPool<byte>.Shared.Return(combinedRented);
                }

                throw;
            }
            finally
            {
                stack.Dispose();
            }

            return document;
        }

        private static YamlDocument ParseUnrented(
            ReadOnlyMemory<byte> utf8Yaml,
            YamlReaderOptions readerOptions,
            YamlDuplicateKeyHandling duplicateKeyHandling,
            YamlTokenType tokenType = YamlTokenType.None,
            int maxAliasExpansion = 0,
            YamlTagDirectives? tagDirectives = null)
        {
            // These tokens should already have been processed.
            Debug.Assert(
                tokenType is not (YamlTokenType.Null or YamlTokenType.False or YamlTokenType.True));

            ReadOnlySpan<byte> utf8YamlSpan = utf8Yaml.Span;
            MetadataDb database;
            YamlTables? tables;

            if (tokenType is YamlTokenType.String or YamlTokenType.Number)
            {
                // For primitive types, we can avoid renting MetadataDb and creating StackRowStack.
                database = MetadataDb.CreateLocked(utf8Yaml.Length);
                StackRowStack stack = default;
                Parse(utf8YamlSpan, readerOptions, ref database, ref stack, out tables, tagDirectives);
            }
            else
            {
                database = MetadataDb.CreateRented(utf8Yaml.Length, convertToAlloc: true);
                var stack = new StackRowStack(YamlDocumentOptions.DefaultMaxDepth * StackRow.Size);
                try
                {
                    Parse(utf8YamlSpan, readerOptions, ref database, ref stack, out tables, tagDirectives);
                }
                finally
                {
                    stack.Dispose();
                }
            }

            ReadOnlyMemory<byte> payload = AttachDecoded(utf8Yaml, tables, rent: false, out _);
            SetYamlOptions(tables, readerOptions, maxAliasExpansion);
            YamlDocument document = new YamlDocument(payload, database, isDisposable: false, yaml: tables);

            document.ParsedDuplicateKeyHandling = duplicateKeyHandling;
            document.ParsedSchema = readerOptions.Schema;
            if (duplicateKeyHandling == YamlDuplicateKeyHandling.Error)
            {
                ValidateNoDuplicateProperties(document);
            }

            return document;
        }

        private static ArraySegment<byte> ReadToEnd(Stream stream)
        {
            int written = 0;
            byte[]? rented = null;

            ReadOnlySpan<byte> utf8Bom = YamlConstants.Utf8Bom;

            try
            {
                if (stream.CanSeek)
                {
                    // Ask for 1 more than the length to avoid resizing later,
                    // which is unnecessary in the common case where the stream length doesn't change.
                    long expectedLength = Math.Max(utf8Bom.Length, stream.Length - stream.Position) + 1;
                    rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
                }
                else
                {
                    rented = ArrayPool<byte>.Shared.Rent(UnseekableStreamInitialRentSize);
                }

                int lastRead;

                // Read up to 3 bytes to see if it's the UTF-8 BOM
                do
                {
                    // No need for checking for growth, the minimal rent sizes both guarantee it'll fit.
                    Debug.Assert(rented.Length >= utf8Bom.Length);

                    lastRead = stream.Read(
                        rented,
                        written,
                        utf8Bom.Length - written);

                    written += lastRead;
                } while (lastRead > 0 && written < utf8Bom.Length);

                // If we have 3 bytes, and they're the BOM, reset the write position to 0.
                if (written == utf8Bom.Length &&
                    utf8Bom.SequenceEqual(rented.AsSpan(0, utf8Bom.Length)))
                {
                    written = 0;
                }

                do
                {
                    if (rented.Length == written)
                    {
                        byte[] toReturn = rented;
                        rented = ArrayPool<byte>.Shared.Rent(checked(toReturn.Length * 2));
                        Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);
                        // Holds document content, clear it.
                        ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
                    }

                    lastRead = stream.Read(rented, written, rented.Length - written);
                    written += lastRead;
                } while (lastRead > 0);

                return new ArraySegment<byte>(rented, 0, written);
            }
            catch
            {
                if (rented is not null)
                {
                    // Holds document content, clear it before returning it.
                    rented.AsSpan(0, written).Clear();
                    ArrayPool<byte>.Shared.Return(rented);
                }

                throw;
            }
        }

        private static async
#if NET
            ValueTask<ArraySegment<byte>>
#else
            Task<ArraySegment<byte>>
#endif
            ReadToEndAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            int written = 0;
            byte[]? rented = null;

            try
            {
                // Save the length to a local to be reused across awaits.
                int utf8BomLength = YamlConstants.Utf8Bom.Length;

                if (stream.CanSeek)
                {
                    // Ask for 1 more than the length to avoid resizing later,
                    // which is unnecessary in the common case where the stream length doesn't change.
                    long expectedLength = Math.Max(utf8BomLength, stream.Length - stream.Position) + 1;
                    rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
                }
                else
                {
                    rented = ArrayPool<byte>.Shared.Rent(UnseekableStreamInitialRentSize);
                }

                int lastRead;

                // Read up to 3 bytes to see if it's the UTF-8 BOM
                do
                {
                    // No need for checking for growth, the minimal rent sizes both guarantee it'll fit.
                    Debug.Assert(rented.Length >= YamlConstants.Utf8Bom.Length);

                    lastRead = await stream.ReadAsync(rented.AsMemory(written, utf8BomLength - written), cancellationToken).ConfigureAwait(false);

                    written += lastRead;
                } while (lastRead > 0 && written < utf8BomLength);

                // If we have 3 bytes, and they're the BOM, reset the write position to 0.
                if (written == utf8BomLength &&
                    YamlConstants.Utf8Bom.SequenceEqual(rented.AsSpan(0, utf8BomLength)))
                {
                    written = 0;
                }

                do
                {
                    if (rented.Length == written)
                    {
                        byte[] toReturn = rented;
                        rented = ArrayPool<byte>.Shared.Rent(toReturn.Length * 2);
                        Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);
                        // Holds document content, clear it.
                        ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
                    }

                    lastRead = await stream.ReadAsync(rented.AsMemory(written), cancellationToken).ConfigureAwait(false);

                    written += lastRead;

                } while (lastRead > 0);

                return new ArraySegment<byte>(rented, 0, written);
            }
            catch
            {
                if (rented is not null)
                {
                    // Holds document content, clear it before returning it.
                    rented.AsSpan(0, written).Clear();
                    ArrayPool<byte>.Shared.Return(rented);
                }

                throw;
            }
        }
    }
}
