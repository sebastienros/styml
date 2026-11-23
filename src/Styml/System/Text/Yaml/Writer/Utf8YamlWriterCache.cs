// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// Defines a thread-local cache for YamlSerializer to store reusable Utf8YamlWriter/IBufferWriter instances.
    /// </summary>
    internal static class Utf8YamlWriterCache
    {
        [ThreadStatic]
        private static ThreadLocalState? t_threadLocalState;

        public static Utf8YamlWriter RentWriterAndBuffer(YamlSerializerOptions options, out PooledByteBufferWriter bufferWriter) =>
            RentWriterAndBuffer(options.GetWriterOptions(), options.DefaultBufferSize, out bufferWriter);

        public static Utf8YamlWriter RentWriterAndBuffer(YamlWriterOptions options, int defaultBufferSize, out PooledByteBufferWriter bufferWriter)
        {
            ThreadLocalState state = t_threadLocalState ??= new();
            Utf8YamlWriter writer;

            if (state.RentedWriters++ == 0)
            {
                // First YamlSerializer call in the stack -- initialize & return the cached instances.
                bufferWriter = state.BufferWriter;
                writer = state.Writer;

                bufferWriter.InitializeEmptyInstance(defaultBufferSize);
                writer.ConfigureForCacheReuse(bufferWriter, options);
            }
            else
            {
                // We're in a recursive YamlSerializer call -- return fresh instances.
                bufferWriter = new PooledByteBufferWriter(defaultBufferSize);
                writer = new Utf8YamlWriter(bufferWriter, options);
            }

            return writer;
        }

        /// <summary>
        /// The writer options of the two internal paths that stay JSON-compatible after M5a:
        /// YamlValue's deep equality, whose bytes are internal and compared as values, and
        /// YamlValueOfTCustomized.ComputeValueKind, since a value's kind must not depend on any style. Not a DOM limit.
        /// </summary>
        public static readonly YamlWriterOptions PinnedDocumentOptions = new() { Style = YamlWriterStyle.JsonCompatible };

        public static Utf8YamlWriter RentWriter(YamlSerializerOptions options, IBufferWriter<byte> bufferWriter) =>
            RentWriter(options.GetWriterOptions(), bufferWriter);


        private static Utf8YamlWriter RentWriter(YamlWriterOptions writerOptions, IBufferWriter<byte> bufferWriter)
        {
            ThreadLocalState state = t_threadLocalState ??= new();
            Utf8YamlWriter writer;

            if (state.RentedWriters++ == 0)
            {
                // First YamlSerializer call in the stack -- initialize & return the cached instance.
                writer = state.Writer;
                writer.ConfigureForCacheReuse(bufferWriter, writerOptions);
            }
            else
            {
                // We're in a recursive YamlSerializer call -- return a fresh instance.
                writer = new Utf8YamlWriter(bufferWriter, writerOptions);
            }

            return writer;
        }

        public static void ReturnWriterAndBuffer(Utf8YamlWriter writer, PooledByteBufferWriter bufferWriter)
        {
            Debug.Assert(t_threadLocalState is not null);
            ThreadLocalState state = t_threadLocalState;

            writer.ResetAllStateForCacheReuse();
            bufferWriter.ClearAndReturnBuffers();

            int rentedWriters = --state.RentedWriters;
            Debug.Assert((rentedWriters == 0) == (ReferenceEquals(state.BufferWriter, bufferWriter) && ReferenceEquals(state.Writer, writer)));
        }

        public static void ReturnWriter(Utf8YamlWriter writer)
        {
            Debug.Assert(t_threadLocalState is not null);
            ThreadLocalState state = t_threadLocalState;

            writer.ResetAllStateForCacheReuse();

            int rentedWriters = --state.RentedWriters;
            Debug.Assert((rentedWriters == 0) == ReferenceEquals(state.Writer, writer));
        }

        private sealed class ThreadLocalState
        {
            public readonly PooledByteBufferWriter BufferWriter;
            public readonly Utf8YamlWriter Writer;
            public int RentedWriters;

            public ThreadLocalState()
            {
                BufferWriter = PooledByteBufferWriter.CreateEmptyInstanceForCaching();
                Writer = Utf8YamlWriter.CreateEmptyInstanceForCaching();
            }
        }
    }
}
