// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Pipelines.PipeWriter"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Pipelines.PipeWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        public static Task SerializeAsync<TValue>(
            PipeWriter utf8Yaml,
            TValue value,
            YamlTypeInfo<TValue> yamlTypeInfo,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return yamlTypeInfo.SerializeAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Pipelines.PipeWriter"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Pipelines.PipeWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static Task SerializeAsync<TValue>(
            PipeWriter utf8Yaml,
            TValue value,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return yamlTypeInfo.SerializeAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Pipelines.PipeWriter"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Pipelines.PipeWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static Task SerializeAsync(
            PipeWriter utf8Yaml,
            object? value,
            YamlTypeInfo yamlTypeInfo,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return yamlTypeInfo.SerializeAsObjectAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Pipelines.PipeWriter"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Pipelines.PipeWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/>, <paramref name="inputType"/>, or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        public static Task SerializeAsync(
                PipeWriter utf8Yaml,
                object? value,
                Type inputType,
                YamlSerializerContext context,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);

            return yamlTypeInfo.SerializeAsObjectAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Pipelines.PipeWriter"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Pipelines.PipeWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static Task SerializeAsync(
                PipeWriter utf8Yaml,
                object? value,
                Type inputType,
                YamlSerializerOptions? options = null,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);

            return yamlTypeInfo.SerializeAsObjectAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Serializes each element of an <see cref="IAsyncEnumerable{TValue}"/> to the <see cref="PipeWriter"/>
        /// in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The type of elements in the sequence to serialize.</typeparam>
        /// <param name="utf8Yaml">The <see cref="PipeWriter"/> to write to.</param>
        /// <param name="value">The <see cref="IAsyncEnumerable{T}"/> sequence to serialize.</param>
        /// <param name="topLevelValues"><see langword="true"/> to serialize the elements as a sequence of newline-separated top-level YAML values
        /// (a <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> document); <see langword="false"/> to serialize them as a single root-level YAML array.</param>
        /// <param name="options">Options to control the serialization behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is <see langword="true"/>, the output conforms to the
        /// <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> specification: each element is serialized as a
        /// single-line YAML value followed by a line-feed (<c>\n</c>) terminator. The line terminator is always <c>\n</c>
        /// regardless of <see cref="YamlSerializerOptions.NewLine"/>, and <see cref="YamlSerializerOptions.WriteIndented"/>
        /// is ignored so that each value is emitted on a single line.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static Task SerializeAsyncEnumerable<TValue>(
            PipeWriter utf8Yaml,
            IAsyncEnumerable<TValue> value,
            bool topLevelValues = false,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(value);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            if (topLevelValues)
            {
                return SerializeAsyncEnumerableAsYamlLines(utf8Yaml, value, yamlTypeInfo, cancellationToken);
            }

            YamlTypeInfo<IAsyncEnumerable<TValue>> collectionTypeInfo = GetOrAddIAsyncEnumerableTypeInfoForSerialize(yamlTypeInfo);
            return collectionTypeInfo.SerializeAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Serializes each element of an <see cref="IAsyncEnumerable{TValue}"/> to the <see cref="PipeWriter"/>
        /// in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The type of elements in the sequence to serialize.</typeparam>
        /// <param name="utf8Yaml">The <see cref="PipeWriter"/> to write to.</param>
        /// <param name="value">The <see cref="IAsyncEnumerable{T}"/> sequence to serialize.</param>
        /// <param name="yamlTypeInfo">Metadata about the type of elements to serialize.</param>
        /// <param name="topLevelValues"><see langword="true"/> to serialize the elements as a sequence of newline-separated top-level YAML values
        /// (a <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> document); <see langword="false"/> to serialize them as a single root-level YAML array.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/>, <paramref name="value"/>, or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is <see langword="true"/>, the output conforms to the
        /// <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> specification: each element is serialized as a
        /// single-line YAML value followed by a line-feed (<c>\n</c>) terminator. The line terminator is always <c>\n</c>
        /// regardless of <see cref="YamlSerializerOptions.NewLine"/>, and <see cref="YamlSerializerOptions.WriteIndented"/>
        /// is ignored so that each value is emitted on a single line.
        /// </remarks>
        public static Task SerializeAsyncEnumerable<TValue>(
            PipeWriter utf8Yaml,
            IAsyncEnumerable<TValue> value,
            YamlTypeInfo<TValue> yamlTypeInfo,
            bool topLevelValues = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            if (topLevelValues)
            {
                return SerializeAsyncEnumerableAsYamlLines(utf8Yaml, value, yamlTypeInfo, cancellationToken);
            }

            YamlTypeInfo<IAsyncEnumerable<TValue>> collectionTypeInfo = GetOrAddIAsyncEnumerableTypeInfoForSerialize(yamlTypeInfo);
            return collectionTypeInfo.SerializeAsync(utf8Yaml, value, cancellationToken);
        }

        private static async Task SerializeAsyncEnumerableAsYamlLines<TValue>(
            PipeWriter utf8Yaml,
            IAsyncEnumerable<TValue> value,
            YamlTypeInfo<TValue> yamlTypeInfo,
            CancellationToken cancellationToken)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            YamlWriterOptions writerOptions = yamlTypeInfo.Options.GetWriterOptionsForYamlLines();
            int flushThreshold = (int)(FlushThreshold * (utf8Yaml is PooledByteBufferWriter bufferWriter
                ? bufferWriter.Capacity
                : 4 * PipeOptions.Default.MinimumSegmentSize));
            var writer = new Utf8YamlWriter(utf8Yaml, writerOptions);

            try
            {
                bool first = true;
                await foreach (TValue item in value.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (!first)
                    {
                        writer.Reset();

                        // Decision 1: a YAML stream, "---" between top-level values,
                        // in both styles.
                        if (writer.IsBlockStyle)
                        {
                            writer.WriteDocumentStart();
                            writer.Flush();
                        }
                        else
                        {
                            Span<byte> marker = utf8Yaml.GetSpan(4);
                            "---\n"u8.CopyTo(marker);
                            utf8Yaml.Advance(4);
                        }
                    }

                    first = false;
                    FlushResult result = await yamlTypeInfo.SerializeAsync(utf8Yaml, item, flushThreshold, cancellationToken, yamlLineWriter: writer).ConfigureAwait(false);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Reset the writer in exception cases so writer.Dispose() doesn't flush a partially-written value to the pipe.
                writer.Reset();
                throw;
            }
            finally
            {
                writer.Dispose();
            }
        }
    }
}
