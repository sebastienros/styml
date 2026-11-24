// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Text.Yaml.Serialization;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// YAML-grammar counterpart of GetReaderScopedToNextValue: the
        /// slice runs over the raw extent of the value (properties, quotes and block-scalar header included), and the
        /// scoped reader starts at the column of the value's first byte, inside the parent's indentation.
        /// </summary>
        /// <summary>
        /// Decision 2 (M5c): zero tokens is no document, and reads as a null
        /// under every schema. A reader over <c>~</c> with the Core schema gives every converter what <c>~</c> gives it,
        /// <see cref="YamlReaderOptions.AcceptLenientScalars"/> included.
        /// </summary>
        internal static Utf8YamlReader EmptyInputReader(YamlReaderOptions options) =>
            new Utf8YamlReader("~"u8, new YamlReaderOptions { AcceptLenientScalars = options.AcceptLenientScalars });

        private static Utf8YamlReader GetYamlReaderScopedToNextValue(ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            ReadOnlySpan<byte> valueSpan = default;
            ReadOnlySequence<byte> valueSequence = default;
            long lineNumber = 0;
            long bytePositionInLine = 0;
            int parentIndent = -1;
            long valueStart = 0;
            bool documentLine = false;

            try
            {
                switch (reader.TokenType)
                {
                    case YamlTokenType.None:
                    case YamlTokenType.PropertyName:
                        bool fresh = reader.TokenType == YamlTokenType.None;
                        if (!reader.Read())
                        {
                            if (fresh && reader.IsFinalBlock && reader.TokenType == YamlTokenType.None)
                            {
                                // Decision 2 (M5c): a reader with no token at all is empty input, read as a null.
                                return EmptyInputReader(reader.CurrentState.Options);
                            }

                            ThrowHelper.ThrowYamlReaderException(ref reader, ExceptionResource.ExpectedOneCompleteToken);
                        }
                        break;
                }

                long start = reader.YamlRawTokenStart;
                valueStart = start;
                lineNumber = reader.YamlRawTokenLine;
                bytePositionInLine = reader.YamlRawTokenColumn;
                parentIndent = reader.YamlTokenParentIndent;
                documentLine = reader.YamlPropertiesOnDocumentLine;

                switch (reader.TokenType)
                {
                    case YamlTokenType.StartObject:
                    case YamlTokenType.StartArray:
                        if (!reader.TrySkip())
                        {
                            ThrowHelper.ThrowYamlReaderException(ref reader, ExceptionResource.NotEnoughData);
                        }
                        break;

                    case YamlTokenType.String:
                    case YamlTokenType.Number:
                    case YamlTokenType.True:
                    case YamlTokenType.False:
                    case YamlTokenType.Null:
                    case YamlTokenType.Alias:
                        // M5b: an alias is read as one token; the scoped reader resolves it through the operation's table.
                        break;

                    default:
                        ThrowHelper.ThrowYamlReaderException(ref reader, ExceptionResource.ExpectedStartOfValueNotFound, reader.YamlValueBytes.IsEmpty ? default : reader.YamlValueBytes[0]);
                        break;
                }

                long length = reader.BytesConsumed - start;
                ReadOnlySequence<byte> sequence = reader.OriginalSequence;
                if (length == 0)
                {
                    // A zero-width empty node: read it as an explicit null.
                    Debug.Assert(reader.TokenType == YamlTokenType.Null);
                    valueSpan = "~"u8;
                }
                else if (sequence.IsEmpty)
                {
                    valueSpan = reader.OriginalSpan.Slice(checked((int)start), checked((int)length));
                }
                else
                {
                    valueSequence = sequence.Slice(start, length);
                }
            }
            catch (YamlReaderException ex)
            {
                ThrowHelper.ReThrowWithPath(ref state, ex);
            }

            // M5b: the scoped reader records anchors and resolves aliases in the operation's table, with its own base.
            YamlReaderOptions scopedOptions = reader.CurrentState.Options;
            scopedOptions.RecordAnchors = true;
            YamlAnchorContext? anchorContext = null;
            if (reader.YamlAnchorContext is { } outerContext)
            {
                reader.YamlStoreSource();
                anchorContext = outerContext.ForSlice(valueStart);
            }

            var scopedState = new YamlReaderState(
                lineNumber: lineNumber,
                bytePositionInLine: bytePositionInLine,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: scopedOptions,
                bitStack: default,
                yaml: YamlParseState.ForScopedValue(parentIndent, reader.YamlTagDirectives, anchorContext, documentLine));

            return valueSpan.IsEmpty
                ? new Utf8YamlReader(valueSequence, isFinalBlock: true, state: scopedState)
                : new Utf8YamlReader(valueSpan, isFinalBlock: true, state: scopedState);
        }
    }
}
