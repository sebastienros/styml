// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Serialization.Metadata
{
    public partial class YamlTypeInfo<T>
    {
        // This section provides helper methods guiding root-level deserialization
        // of values corresponding according to the current YamlTypeInfo configuration.

        internal T? Deserialize(ref Utf8YamlReader reader, ref ReadStack state)
        {
            Debug.Assert(IsConfigured);
            bool success = EffectiveConverter.ReadCore(ref reader, out T? result, Options, ref state);
            Debug.Assert(success, "Should only return false for async deserialization");
            return result;
        }

        internal async ValueTask<T?> DeserializeAsync<TReadBufferState, TStream>(TStream utf8Yaml, TReadBufferState bufferState, CancellationToken cancellationToken)
            where TReadBufferState : struct, IReadBufferState<TReadBufferState, TStream>
        {
            Debug.Assert(IsConfigured);
            YamlSerializerOptions options = Options;
            if (options.ResolveMergeKeys)
            {
                // M5b: merge keys are resolved on the whole input, read first.
                var all = new MemoryStream();
                try
                {
                    while (true)
                    {
                        bufferState = await bufferState.ReadAsync(utf8Yaml, cancellationToken).ConfigureAwait(false);
                        AppendAndAdvance<TReadBufferState, TStream>(ref bufferState, all);
                        if (bufferState.IsFinalBlock)
                        {
                            return YamlSerializer.ReadFromSpan(all.GetBuffer().AsSpan(0, checked((int)all.Length)), this);
                        }
                    }
                }
                finally
                {
                    bufferState.Dispose();
                }
            }

            ReadStack readStack = default;
            readStack.Initialize(this, supportContinuation: true);
            var yamlReaderState = new YamlReaderState(options.GetReaderOptions());

            try
            {
                while (true)
                {
                    bufferState = await bufferState.ReadAsync(utf8Yaml, cancellationToken).ConfigureAwait(false);
                    bool success = ContinueDeserialize<TReadBufferState, TStream>(
                        ref bufferState,
                        ref yamlReaderState,
                        ref readStack,
                        out T? value);

                    if (success)
                    {
                        return value;
                    }
                }
            }
            finally
            {
                bufferState.Dispose();
            }
        }

        internal ValueTask<T?> DeserializeAsync(Stream utf8Yaml, CancellationToken cancellationToken)
        {
            // Note: The ReadBufferState ctor rents pooled buffers.
            StreamReadBufferState bufferState = new StreamReadBufferState(Options.DefaultBufferSize);
            return DeserializeAsync(utf8Yaml, bufferState, cancellationToken);
        }

        internal ValueTask<T?> DeserializeAsync(PipeReader utf8Yaml, CancellationToken cancellationToken)
        {
            PipeReadBufferState bufferState = new(utf8Yaml);
            return DeserializeAsync(utf8Yaml, bufferState, cancellationToken);
        }

        internal T? Deserialize(Stream utf8Yaml)
        {
            Debug.Assert(IsConfigured);
            YamlSerializerOptions options = Options;
            if (options.ResolveMergeKeys)
            {
                // M5b: merge keys are resolved on the whole input, read first.
                StreamReadBufferState allState = new StreamReadBufferState(options.DefaultBufferSize);
                var all = new MemoryStream();
                try
                {
                    while (true)
                    {
                        allState.Read(utf8Yaml);
                        AppendAndAdvance<StreamReadBufferState, Stream>(ref allState, all);
                        if (allState.IsFinalBlock)
                        {
                            return YamlSerializer.ReadFromSpan(all.GetBuffer().AsSpan(0, checked((int)all.Length)), this);
                        }
                    }
                }
                finally
                {
                    allState.Dispose();
                }
            }

            ReadStack readStack = default;
            readStack.Initialize(this, supportContinuation: true);
            var yamlReaderState = new YamlReaderState(options.GetReaderOptions());
            // Note: The ReadBufferState ctor rents pooled buffers.
            StreamReadBufferState bufferState = new StreamReadBufferState(options.DefaultBufferSize);

            try
            {
                while (true)
                {
                    bufferState.Read(utf8Yaml);
                    bool success = ContinueDeserialize<StreamReadBufferState, Stream>(
                        ref bufferState,
                        ref yamlReaderState,
                        ref readStack,
                        out T? value);

                    if (success)
                    {
                        return value;
                    }
                }
            }
            finally
            {
                bufferState.Dispose();
            }
        }

        /// <summary>
        /// Caches YamlTypeInfo instances used by the Serialize/DeserializeAsyncEnumerable methods.
        /// Store as a non-generic type to avoid triggering generic recursion in the AOT compiler.
        /// cf. https://github.com/dotnet/runtime/issues/85184
        /// </summary>
        internal YamlTypeInfo? _asyncEnumerableArrayTypeInfo;
        internal YamlTypeInfo? _asyncEnumerableRootLevelValueTypeInfo;
        internal YamlTypeInfo? _asyncEnumerableRootLevelSerializer;

        internal sealed override object? DeserializeAsObject(ref Utf8YamlReader reader, ref ReadStack state)
            => Deserialize(ref reader, ref state);

        internal sealed override async ValueTask<object?> DeserializeAsObjectAsync(Stream utf8Yaml, CancellationToken cancellationToken)
        {
            // Note: The ReadBufferState ctor rents pooled buffers.
            StreamReadBufferState bufferState = new StreamReadBufferState(Options.DefaultBufferSize);
            T? result = await DeserializeAsync(utf8Yaml, bufferState, cancellationToken).ConfigureAwait(false);
            return result;
        }

        internal sealed override async ValueTask<object?> DeserializeAsObjectAsync(PipeReader utf8Yaml, CancellationToken cancellationToken)
        {
            T? result = await DeserializeAsync<PipeReadBufferState, PipeReader>(utf8Yaml, bufferState: new PipeReadBufferState(utf8Yaml), cancellationToken).ConfigureAwait(false);
            return result;
        }

        internal sealed override object? DeserializeAsObject(Stream utf8Yaml)
            => Deserialize(utf8Yaml);

        private static void AppendAndAdvance<TReadBufferState, TStream>(ref TReadBufferState bufferState, MemoryStream all)
            where TReadBufferState : struct, IReadBufferState<TReadBufferState, TStream>
        {
            ReadOnlySequence<byte> bytes = bufferState.Bytes;
            foreach (ReadOnlyMemory<byte> segment in bytes)
            {
#if NET
                all.Write(segment.Span);
#else
                byte[] copy = segment.ToArray();
                all.Write(copy, 0, copy.Length);
#endif
            }

            bufferState.Advance(bytes.Length);
        }

        internal bool ContinueDeserialize<TReadBufferState, TStream>(
            ref TReadBufferState bufferState,
            ref YamlReaderState yamlReaderState,
            ref ReadStack readStack,
            out T? value)
            where TReadBufferState : struct, IReadBufferState<TReadBufferState, TStream>
        {
            // M5b: a stream's readers give absolute positions to the operation's anchor table.
            if (yamlReaderState._yaml.AnchorContext is { } anchorContext)
            {
                anchorContext.Base = readStack.StreamPosition;
            }

            bufferState.GetReader(yamlReaderState, out Utf8YamlReader reader);

            try
            {
                bool success = EffectiveConverter.ReadCore(ref reader, out value, Options, ref readStack);

#if DEBUG
                Debug.Assert(reader.BytesConsumed <= bufferState.Bytes.Length);
                Debug.Assert(!bufferState.IsFinalBlock || reader.AllowMultipleValues || reader.BytesConsumed == bufferState.Bytes.Length,
                    "The reader should have thrown if we have remaining bytes.");
#endif

                yamlReaderState = reader.CurrentState;
                return success;
            }
            finally
            {
                if (reader.YamlAnchorContext is { } context)
                {
                    if (context.Table.Unrebased)
                    {
                        // The reader created the table in this buffer, with positions relative to it.
                        context.Table.Unrebased = false;
                        context.Table.Shift(readStack.StreamPosition);
                        context.Base = readStack.StreamPosition;
                    }

                    // The buffer's bytes go into the store before the stream drops them.
                    context.Table.Store(bufferState.Bytes, context.Base);
                }

                readStack.StreamPosition += reader.BytesConsumed;
                bufferState.Advance(reader.BytesConsumed);
            }
        }
    }
}
