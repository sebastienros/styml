// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Reads the UTF-8 encoded text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static ValueTask<TValue?> DeserializeAsync<TValue>(
            PipeReader utf8Yaml,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return yamlTypeInfo.DeserializeAsync(utf8Yaml, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        public static ValueTask<TValue?> DeserializeAsync<TValue>(
                PipeReader utf8Yaml,
                YamlTypeInfo<TValue> yamlTypeInfo,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));
            ArgumentNullException.ThrowIfNull(yamlTypeInfo, nameof(yamlTypeInfo));

            yamlTypeInfo.EnsureConfigured();
            return yamlTypeInfo.DeserializeAsync(utf8Yaml, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        public static ValueTask<object?> DeserializeAsync(
                PipeReader utf8Yaml,
                YamlTypeInfo yamlTypeInfo,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));
            ArgumentNullException.ThrowIfNull(yamlTypeInfo, nameof(yamlTypeInfo));

            yamlTypeInfo.EnsureConfigured();
            return yamlTypeInfo.DeserializeAsObjectAsync(utf8Yaml, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single YAML value into a <paramref name="returnType"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/>, <paramref name="returnType"/>, or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// the <paramref name="returnType"/> is not compatible with the YAML,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method on the provided <paramref name="context"/>
        /// did not return a compatible <see cref="YamlTypeInfo"/> for <paramref name="returnType"/>.
        /// </exception>
        public static ValueTask<object?> DeserializeAsync(
                PipeReader utf8Yaml,
                Type returnType,
                YamlSerializerContext context,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));
            ArgumentNullException.ThrowIfNull(returnType, nameof(returnType));
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, returnType);
            return yamlTypeInfo.DeserializeAsObjectAsync(utf8Yaml, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single YAML value into a <paramref name="returnType"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// the <paramref name="returnType"/> is not compatible with the YAML,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static ValueTask<object?> DeserializeAsync(
               PipeReader utf8Yaml,
               Type returnType,
               YamlSerializerOptions? options = null,
               CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));
            ArgumentNullException.ThrowIfNull(returnType, nameof(returnType));

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            return yamlTypeInfo.DeserializeAsObjectAsync(utf8Yaml, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize root-level YAML arrays in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided YAML array.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
                PipeReader utf8Yaml,
                YamlSerializerOptions? options = null,
                CancellationToken cancellationToken = default)
        {
            return DeserializeAsyncEnumerable<TValue>(utf8Yaml, topLevelValues: false, options, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize root-level YAML arrays in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided YAML array.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the element type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
                PipeReader utf8Yaml,
                YamlTypeInfo<TValue> yamlTypeInfo,
                CancellationToken cancellationToken = default)
        {
            return DeserializeAsyncEnumerable(utf8Yaml, yamlTypeInfo, topLevelValues: false, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize sequences of YAML values in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided YAML sequence.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the element type to convert.</param>
        /// <param name="topLevelValues"><see langword="true"/> to deserialize from a sequence of top-level YAML values
        /// (for example, a <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> document); <see langword="false"/> to deserialize from a single top-level array.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is set to <see langword="true" />, treats the PipeReader as a sequence of
        /// whitespace separated top-level YAML values and attempts to deserialize each value into <typeparamref name="TValue"/>.
        /// This is a superset of the <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> format and can be used to consume valid JSONL documents.
        /// When <paramref name="topLevelValues"/> is set to <see langword="false" />, treats the PipeReader as a YAML array and
        /// attempts to deserialize each element into <typeparamref name="TValue"/>.
        /// </remarks>
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
            PipeReader utf8Yaml,
            YamlTypeInfo<TValue> yamlTypeInfo,
            bool topLevelValues,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));
            ArgumentNullException.ThrowIfNull(yamlTypeInfo, nameof(yamlTypeInfo));

            yamlTypeInfo.EnsureConfigured();
            return DeserializeAsyncEnumerableCore(utf8Yaml, yamlTypeInfo, topLevelValues, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize sequences of YAML values in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided YAML sequence.</returns>
        /// <param name="utf8Yaml">YAML data to parse.</param>
        /// <param name="topLevelValues"><see langword="true"/> to deserialize from a sequence of top-level YAML values
        /// (for example, a <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> document); <see langword="false"/> to deserialize from a single top-level array.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is set to <see langword="true" />, treats the PipeReader as a sequence of
        /// whitespace separated top-level YAML values and attempts to deserialize each value into <typeparamref name="TValue"/>.
        /// This is a superset of the <see href="https://jsonlines.org/">JSON Lines (JSONL)</see> format and can be used to consume valid JSONL documents.
        /// When <paramref name="topLevelValues"/> is set to <see langword="false" />, treats the PipeReader as a YAML array and
        /// attempts to deserialize each element into <typeparamref name="TValue"/>.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
            PipeReader utf8Yaml,
            bool topLevelValues,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml, nameof(utf8Yaml));

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return DeserializeAsyncEnumerableCore(utf8Yaml, yamlTypeInfo, topLevelValues, cancellationToken);
        }

        private static IAsyncEnumerable<T?> DeserializeAsyncEnumerableCore<T>(
            PipeReader utf8Yaml,
            YamlTypeInfo<T> yamlTypeInfo,
            bool topLevelValues,
            CancellationToken cancellationToken)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            if (yamlTypeInfo.Options.ResolveMergeKeys)
            {
                // M5b: merge keys are resolved on one whole document; this API streams them.
                ThrowHelper.ThrowNotSupportedException_MergeKeysInAsyncEnumerable();
            }


            YamlTypeInfo<List<T?>> listTypeInfo;
            YamlReaderOptions readerOptions = yamlTypeInfo.Options.GetReaderOptions();
            if (topLevelValues)
            {
                listTypeInfo = GetOrAddListTypeInfoForRootLevelValueMode(yamlTypeInfo);
                readerOptions.AllowMultipleValues = true;
            }
            else
            {
                listTypeInfo = GetOrAddListTypeInfoForArrayMode(yamlTypeInfo);
            }

            return CreateAsyncEnumerableFromArray(utf8Yaml, listTypeInfo, readerOptions, cancellationToken);

            static async IAsyncEnumerable<T?> CreateAsyncEnumerableFromArray(
                PipeReader utf8Yaml,
                YamlTypeInfo<List<T?>> listTypeInfo,
                YamlReaderOptions readerOptions,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                Debug.Assert(listTypeInfo.IsConfigured);

                ReadStack readStack = default;
                readStack.Initialize(listTypeInfo, supportContinuation: true);
                YamlReaderState yamlReaderState = new(readerOptions);
                PipeReadBufferState bufferState = new(utf8Yaml);

                try
                {
                    bool success;
                    do
                    {
                        bufferState = await bufferState.ReadAsync(utf8Yaml, cancellationToken, fillBuffer: false).ConfigureAwait(false);
                        success = listTypeInfo.ContinueDeserialize<PipeReadBufferState, PipeReader>(
                            ref bufferState,
                            ref yamlReaderState,
                            ref readStack,
                            out List<T?>? _);

                        if (readStack.Current.ReturnValue is { } returnValue)
                        {
                            var list = (List<T?>)returnValue;
                            foreach (T? item in list)
                            {
                                yield return item;
                            }

                            list.Clear();
                        }
                    } while (!success);
                }
                finally
                {
                    bufferState.Dispose();
                }
            }
        }
    }
}
