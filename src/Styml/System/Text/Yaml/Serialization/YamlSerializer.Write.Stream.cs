// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        // We flush the Stream when the buffer is >=90% of capacity.
        // This threshold is a compromise between buffer utilization and minimizing cases where the buffer
        // needs to be expanded\doubled because it is not large enough to write the current property or element.
        // We check for flush after each JSON property and element is written to the buffer.
        // Once the buffer is expanded to contain the largest single element\property, a 90% threshold
        // means the buffer may be expanded a maximum of 4 times: 1-(1/(2^4))==.9375.
        internal const float FlushThreshold = .90f;

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
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
            Stream utf8Yaml,
            TValue value,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return yamlTypeInfo.SerializeAsync(utf8Yaml, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static void Serialize<TValue>(
            Stream utf8Yaml,
            TValue value,
            YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            yamlTypeInfo.Serialize(utf8Yaml, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
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
            Stream utf8Yaml,
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
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
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
        public static void Serialize(
            Stream utf8Yaml,
            object? value,
            Type inputType,
            YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);
            yamlTypeInfo.SerializeAsObject(utf8Yaml, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        public static Task SerializeAsync<TValue>(
            Stream utf8Yaml,
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
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        public static void Serialize<TValue>(
            Stream utf8Yaml,
            TValue value,
            YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            yamlTypeInfo.Serialize(utf8Yaml, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static Task SerializeAsync(
            Stream utf8Yaml,
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
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static void Serialize(
            Stream utf8Yaml,
            object? value,
            YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            yamlTypeInfo.SerializeAsObject(utf8Yaml, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
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
            Stream utf8Yaml,
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
        /// Converts the provided value to UTF-8 encoded YAML text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
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
        public static void Serialize(
            Stream utf8Yaml,
            object? value,
            Type inputType,
            YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);
            yamlTypeInfo.SerializeAsObject(utf8Yaml, value);
        }

        /// <summary>
        /// Serializes each element of an <see cref="IAsyncEnumerable{TValue}"/> to the <see cref="System.IO.Stream"/>
        /// in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The type of elements in the sequence to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
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
            Stream utf8Yaml,
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
        /// Serializes each element of an <see cref="IAsyncEnumerable{TValue}"/> to the <see cref="System.IO.Stream"/>
        /// in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The type of elements in the sequence to serialize.</typeparam>
        /// <param name="utf8Yaml">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
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
            Stream utf8Yaml,
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
            Stream utf8Yaml,
            IAsyncEnumerable<TValue> value,
            YamlTypeInfo<TValue> yamlTypeInfo,
            CancellationToken cancellationToken)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            using var bufferWriter = new PooledByteBufferWriter(yamlTypeInfo.Options.DefaultBufferSize, utf8Yaml);
            await SerializeAsyncEnumerableAsYamlLines(bufferWriter, value, yamlTypeInfo, cancellationToken).ConfigureAwait(false);
        }
    }
}
