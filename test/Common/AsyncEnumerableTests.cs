// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class AsyncEnumerableTests : SerializerTests
    {
        public AsyncEnumerableTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper)
        {
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(10, 1)]
        [InlineData(100, 1)]
        [InlineData(1000, 1)]
        [InlineData(1000, 1000)]
        [InlineData(1000, 32000)]
        public async Task DeserializeAsyncEnumerable_ReadSimpleObjectAsync(int count, int bufferSize)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = bufferSize
            };

            using var stream = new MemoryStream(GenerateYamlArray(count));

            int callbackCount = 0;
            await foreach (SimpleTestClass item in Serializer.DeserializeAsyncEnumerable<SimpleTestClass>(stream, options))
            {
                Assert.Equal(callbackCount, item.MyInt32);

                item.MyInt32 = 2; // Put correct value back for Verify()
                item.Verify();

                callbackCount++;
            }

            Assert.Equal(count, callbackCount);

            static byte[] GenerateYamlArray(int count)
            {
                SimpleTestClass[] collection = new SimpleTestClass[count];
                for (int i = 0; i < collection.Length; i++)
                {
                    var obj = new SimpleTestClass();
                    obj.Initialize();
                    obj.MyInt32 = i; // verify order correctness
                    collection[i] = obj;
                }

                return YamlSerializer.SerializeToUtf8Bytes(collection);
            }
        }

        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public async Task DeserializeAsyncEnumerable_ReadSourceAsync<TElement>(IEnumerable<TElement> source, int bufferSize, DeserializeAsyncEnumerableOverload overload)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = bufferSize,
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
            };

            byte[] data = YamlSerializer.SerializeToUtf8Bytes(source);

            using var stream = new MemoryStream(data);
            List<TElement> results = await DeserializeAsyncEnumerableWrapper<TElement>(stream, options, overload: overload).ToListAsync();
            Assert.Equal(source, results);
        }

        [Fact]
        public async Task DeserializeAsyncEnumerable_TopLevelValues_TrailingData_ThrowsYamlException()
        {
            YamlSerializerOptions options = new() { DefaultBufferSize = 1 };
            using var stream = new Utf8MemoryStream("[] [1] [1,2,3] <NotYaml/>");

            IAsyncEnumerable<List<int>> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<List<int>>(stream, topLevelValues:true, options);
            await using var asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();

            await Assert.ThrowsAnyAsync<YamlException>(async () =>
            {
                while (await asyncEnumerator.MoveNextAsync());
            });
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlTypeInfo)]
        public async Task DeserializeAsyncEnumerable_ShouldTolerateCustomQueueConverters(DeserializeAsyncEnumerableOverload overload)
        {
            const int expectedCount = 20;

            YamlSerializerOptions options = new YamlSerializerOptions
            {
                Converters = { new DegenerateQueueConverterFactory() }
            };

            byte[] data = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Repeat(Enumerable.Repeat(1,3), expectedCount));

            using var stream = new MemoryStream(data);

            int callbackCount = 0;
            await foreach (Queue<int> nestedQueue in DeserializeAsyncEnumerableWrapper<Queue<int>>(stream, options, overload: overload))
            {
                Assert.Equal(1, nestedQueue.Count);
                Assert.Equal(0, nestedQueue.Peek());
                callbackCount++;
            }

            Assert.Equal(expectedCount, callbackCount);
        }

        private class DegenerateQueueConverterFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeof(Queue<>) == typeToConvert.GetGenericTypeDefinition();
            public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                Type queueElement = typeToConvert.GetGenericArguments()[0];
                Type converterType = typeof(DegenerateQueueConverter<>).MakeGenericType(queueElement);
                return (YamlConverter)Activator.CreateInstance(converterType, nonPublic: true);
            }

            private class DegenerateQueueConverter<T> : YamlConverter<Queue<T>>
            {
                public override bool CanConvert(Type typeToConvert) => typeof(Queue<T>).IsAssignableFrom(typeToConvert);
                public override Queue<T>? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                {
                    while (reader.Read() && reader.TokenType != YamlTokenType.EndArray);
                    var queue = new Queue<T>();
                    queue.Enqueue(default);
                    return queue;
                }

                public override void Write(Utf8YamlWriter writer, Queue<T> value, YamlSerializerOptions options) => throw new NotImplementedException();
            }
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlTypeInfo)]
        public async Task DeserializeAsyncEnumerable_CancellationToken_ThrowsOnCancellation(DeserializeAsyncEnumerableOverload overload)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = 1,
            };

            byte[] data = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Range(1, 100));

            var token = new CancellationToken(canceled: true);
            using var stream = new MemoryStream(data);
            var cancellableAsyncEnumerable = DeserializeAsyncEnumerableWrapper<int>(stream, options, token, overload);

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (int element in cancellableAsyncEnumerable)
                {
                }
            });
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.YamlTypeInfo)]
        public async Task DeserializeAsyncEnumerable_EnumeratorWithCancellationToken_ThrowsOnCancellation(DeserializeAsyncEnumerableOverload overload)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = 1
            };

            byte[] data = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Range(1, 100));

            var token = new CancellationToken(canceled: true);
            using var stream = new MemoryStream(data);
            var cancellableAsyncEnumerable = DeserializeAsyncEnumerableWrapper<int>(stream, options, overload: overload).WithCancellation(token);

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (int element in cancellableAsyncEnumerable)
                {
                }
            });
        }

        [Theory]
        [InlineData(5, 1024)]
        [InlineData(5, 1024 * 1024)]
        public async Task DeserializeAsyncEnumerable_SlowStreamWithLargeStrings(int totalStrings, int stringLength)
        {
            var options = new YamlSerializerOptions
            {
                Converters = { new StringLengthConverter() }
            };

            using var stream = new SlowStream(GenerateYamlCharacters());
            string expectedElement = stringLength.ToString(CultureInfo.InvariantCulture);
            IAsyncEnumerable<string?> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<string>(stream, options);

            await foreach (string? value in asyncEnumerable)
            {
                Assert.Equal(expectedElement, value);
            }

            IEnumerable<byte> GenerateYamlCharacters()
            {
                // ["xxx...x","xxx...x",...,"xxx...x"]
                yield return (byte)'[';
                for (int i = 0; i < totalStrings; i++)
                {
                    yield return (byte)'"';
                    for (int j = 0; j < stringLength; j++)
                    {
                        yield return (byte)'x';
                    }
                    yield return (byte)'"';

                    if (i < totalStrings - 1)
                    {
                        yield return (byte)',';
                    }
                }
                yield return (byte)']';
            }
        }

        public static IEnumerable<object[]> GetAsyncEnumerableSources()
        {
            yield return WrapArgs(Enumerable.Empty<int>(), 1, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);
            yield return WrapArgs(Enumerable.Empty<int>(), 1, DeserializeAsyncEnumerableOverload.YamlTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 20), 1, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100), 20, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => $"lorem ipsum dolor: {i}"), 500, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => $"lorem ipsum dolor: {i}"), 500, DeserializeAsyncEnumerableOverload.YamlTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 10).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 100, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 10).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 100, DeserializeAsyncEnumerableOverload.YamlTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 500, DeserializeAsyncEnumerableOverload.YamlSerializerOptions);

            static object[] WrapArgs<TSource>(IEnumerable<TSource> source, int bufferSize, DeserializeAsyncEnumerableOverload overload) => new object[] { source, bufferSize, overload };
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(43)]
        public async Task SerializeAsyncEnumerable_Cancellation_DisposesEnumerators(int depth)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/120010

            using SelfCancellingAsyncEnumerable enumerable = new();
            using MemoryStream stream = new MemoryStream();

            object wrappingValue = enumerable;
            while (depth-- > 0)
            {
                // Use a LINQ enumerable instead of array/list
                // to force use of enumerators in every layer.
                wrappingValue = Enumerable.Repeat(wrappingValue, 1);
            }

            await Assert.ThrowsAsync<TaskCanceledException>(() => StreamingSerializer.SerializeWrapper(stream, wrappingValue, cancellationToken: enumerable.CancellationToken));
            Assert.True(enumerable.IsEnumeratorDisposed);
        }

        public enum DeserializeAsyncEnumerableOverload { YamlSerializerOptions, YamlTypeInfo };

        private IAsyncEnumerable<T> DeserializeAsyncEnumerableWrapper<T>(Stream stream, YamlSerializerOptions options = null, CancellationToken cancellationToken = default, DeserializeAsyncEnumerableOverload overload = DeserializeAsyncEnumerableOverload.YamlSerializerOptions)
        {
            return overload switch
            {
                DeserializeAsyncEnumerableOverload.YamlTypeInfo => Serializer.DeserializeAsyncEnumerable<T>(stream, ResolveYamlTypeInfo<T>(options), cancellationToken),
                DeserializeAsyncEnumerableOverload.YamlSerializerOptions or _ => Serializer.DeserializeAsyncEnumerable<T>(stream, options, cancellationToken),
            };
        }

        internal static YamlTypeInfo<T> ResolveYamlTypeInfo<T>(YamlSerializerOptions? options = null)
        {
            return (YamlTypeInfo<T>)ResolveYamlTypeInfo(typeof(T), options);
        }

        private static YamlTypeInfo ResolveYamlTypeInfo(Type type, YamlSerializerOptions? options = null)
        {
            options ??= YamlSerializerOptions.Default;
            options.TypeInfoResolver ??= new DefaultYamlTypeInfoResolver();
            options.MakeReadOnly(); // Lock the options instance before initializing metadata
            return options.TypeInfoResolver.GetTypeInfo(type, options);
        }

        private sealed class SlowStream(IEnumerable<byte> byteSource) : Stream, IDisposable
        {
            private readonly IEnumerator<byte> _enumerator = byteSource.GetEnumerator();
            private long _position;

            public override bool CanRead => true;
            public override int Read(byte[] buffer, int offset, int count)
            {
                Debug.Assert(buffer != null);
                Debug.Assert(offset >= 0 && count <= buffer.Length - offset);

                if (count == 0 || !_enumerator.MoveNext())
                {
                    return 0;
                }

                _position++;
                buffer[offset] = _enumerator.Current;
                return 1;
            }

            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override long Length => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            void IDisposable.Dispose() => _enumerator.Dispose();
        }

        private sealed class StringLengthConverter : YamlConverter<string>
        {
            public override string Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Debug.Assert(!reader.ValueIsEscaped);
                if (reader.HasValueSequence)
                {
                    return reader.ValueSequence.Length.ToString(CultureInfo.InvariantCulture);
                }
                return reader.ValueSpan.Length.ToString(CultureInfo.InvariantCulture);
            }

            public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options) => throw new NotImplementedException();
        }

        sealed class SelfCancellingAsyncEnumerable : IAsyncEnumerable<int>, IDisposable
        {
            private readonly CancellationTokenSource _cts = new();
            public CancellationToken CancellationToken => _cts.Token;
            public bool IsEnumeratorDisposed { get; private set; }
            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken _) => new Enumerator(this);
            private sealed class Enumerator(SelfCancellingAsyncEnumerable parent) : IAsyncEnumerator<int>
            {
                public int Current { get; private set; }
                public async ValueTask<bool> MoveNextAsync()
                {
                    await Task.Yield();
                    if (++Current == 10)
                    {
                        parent._cts.Cancel();
                    }

                    return true;
                }

                public ValueTask DisposeAsync()
                {
                    parent.IsEnumeratorDisposed = true;
                    return default;
                }
            }

            public void Dispose() => _cts.Dispose();
        }

        private sealed class ThrowingValue
        {
            private readonly int _v;
            private readonly bool _throwOnSerialize;

            public ThrowingValue() { }

            public ThrowingValue(int value, bool throwOnSerialize = false)
            {
                _v = value;
                _throwOnSerialize = throwOnSerialize;
            }

            public int V => _throwOnSerialize
                ? throw new InvalidOperationException("Simulated serialization failure.")
                : _v;
        }

        private sealed class DisposableAsyncEnumerable : IAsyncEnumerable<int>
        {
            public bool Disposed { get; private set; }

            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new Enumerator(this, cancellationToken);
            }

            private sealed class Enumerator : IAsyncEnumerator<int>
            {
                private readonly DisposableAsyncEnumerable _parent;
                private readonly CancellationToken _ct;
                private int _index;

                public Enumerator(DisposableAsyncEnumerable parent, CancellationToken ct)
                {
                    _parent = parent;
                    _ct = ct;
                }

                public int Current { get; private set; }

                public async ValueTask<bool> MoveNextAsync()
                {
                    _ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                    if (_index < 3)
                    {
                        Current = _index++;
                        return true;
                    }
                    return false;
                }

                public ValueTask DisposeAsync()
                {
                    _parent.Disposed = true;
                    return default;
                }
            }
        }

        private sealed class EmptyResolver : IYamlTypeInfoResolver
        {
            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options) => null;
        }

        private sealed class ElementOnlyInt32Resolver : IYamlTypeInfoResolver
        {
            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                // Simulates a source-gen context that only knows the element type.
                if (type == typeof(int))
                {
                    return YamlMetadataServices.CreateValueInfo<int>(options, YamlMetadataServices.Int32Converter);
                }

                return null;
            }
        }

        // -----------------------------
        // SerializeAsyncEnumerable tests
        // -----------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(100)]
        public async Task SerializeAsyncEnumerable_DefaultMode_ProducesYamlArray(int count)
        {
            using MemoryStream stream = new();

            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                GenerateItems(count),
                ResolveYamlTypeInfo<SimpleTestClass>());

            stream.Position = 0;
            SimpleTestClass[] roundTripped = YamlSerializer.Deserialize<SimpleTestClass[]>(stream);
            Assert.Equal(count, roundTripped.Length);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(i, roundTripped[i].MyInt32);
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_RoundTripsWithDeserializeAsyncEnumerable()
        {
            const int count = 50;
            using MemoryStream stream = new();

            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                GenerateItems(count),
                ResolveYamlTypeInfo<SimpleTestClass>(),
                topLevelValues: true);

            stream.Position = 0;

            int i = 0;
            await foreach (SimpleTestClass item in YamlSerializer.DeserializeAsyncEnumerable<SimpleTestClass>(stream, topLevelValues: true))
            {
                Assert.Equal(i++, item.MyInt32);
            }

            Assert.Equal(count, i);
        }

        public static IEnumerable<object[]> FlushPointsData()
        {
            // topLevelValues, expected stream contents at each flush, expected final output.
            yield return new object[] { false, new[] { "[0", "[0,1", "[0,1,2", "[0,1,2]" }, "[0,1,2]" };
        }

        // Styml (M4a): the JSON Lines row, which holds only under the JSON grammar switch (M4 decision 1).
        public static IEnumerable<object[]> FlushPointsData_JsonLines()
        {
            yield return new object[] { true, new[] { "0\n", "0\n1\n", "0\n1\n2\n" }, "0\n1\n2\n" };
        }

        [Theory]
        [MemberData(nameof(FlushPointsData))]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced(bool topLevelValues, string[] expectedFlushSnapshots, string expectedOutput)
        {
            // Values produced so far must be flushed to the underlying stream before awaiting
            // the producer, so that consumers can observe them in real time. The producer is only
            // released once the flush has been observed, making the expected flush points deterministic.
            using FlushTrackingStream stream = new();
            GatedAsyncEnumerable source = new(count: 3);
            stream.OnFlush = source.ReleaseNext;
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                source,
                ResolveYamlTypeInfo<int>(),
                topLevelValues,
                cts.Token);

            Assert.Equal(expectedFlushSnapshots, stream.FlushSnapshots);
            Assert.Equal(expectedOutput, Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsync_NestedAsyncEnumerable_FlushesStreamWhenAwaitingProducer()
        {
            // The same guarantee applies to IAsyncEnumerable values nested in a larger payload.
            using FlushTrackingStream stream = new();
            GatedAsyncEnumerable source = new(count: 2);
            stream.OnFlush = source.ReleaseNext;
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

            await YamlSerializer.SerializeAsync(
                stream,
                new AsyncEnumerableDto { Values = source },
                ResolveYamlTypeInfo<AsyncEnumerableDto>(),
                cts.Token);

            Assert.Equal(new[] { "{\"Values\":[0", "{\"Values\":[0,1", "{\"Values\":[0,1]}" }, stream.FlushSnapshots);
            Assert.Equal("{\"Values\":[0,1]}", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsync_ValueWithoutAsyncEnumerables_FlushesStreamOnCompletion()
        {
            // Non-streaming payloads are written in a single flush at the end of serialization.
            using FlushTrackingStream stream = new();

            await YamlSerializer.SerializeAsync(stream, new int[] { 1, 2, 3 }, ResolveYamlTypeInfo<int[]>());

            Assert.Equal(new[] { "[1,2,3]" }, stream.FlushSnapshots);
            Assert.Equal("[1,2,3]", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_DestinationFlushFailure_PropagatesAndDisposesEnumerator()
        {
            // A failure to flush the destination must surface to the caller and must not prevent
            // the enumerator from being disposed.
            using FlushTrackingStream stream = new();
            stream.OnFlush = () => throw new IOException("flush failed");
            SlowAsyncEnumerable source = new(count: 3);

            IOException exception = await Assert.ThrowsAsync<IOException>(() =>
                YamlSerializer.SerializeAsyncEnumerable(
                    stream,
                    source,
                    ResolveYamlTypeInfo<int>(),
                    topLevelValues: true));

            Assert.Equal("flush failed", exception.Message);
            Assert.True(source.Disposed);
        }

        public class AsyncEnumerableDto
        {
            public IAsyncEnumerable<int> Values { get; set; }
        }

        /// <summary>
        /// An enumerable of increasing integers whose producer suspends after every element
        /// until it is explicitly released.
        /// </summary>
        private sealed class GatedAsyncEnumerable : IAsyncEnumerable<int>
        {
            private readonly TaskCompletionSource<bool>[] _gates;
            private int _released;

            public GatedAsyncEnumerable(int count)
            {
                _gates = new TaskCompletionSource<bool>[count];
                for (int i = 0; i < count; i++)
                {
                    _gates[i] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            /// <summary>Lets the producer advance past the element it last yielded.</summary>
            public void ReleaseNext()
            {
                int index = _released++;
                if (index < _gates.Length)
                {
                    _gates[index].TrySetResult(true);
                }
            }

            public async IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                for (int i = 0; i < _gates.Length; i++)
                {
                    yield return i;

                    // Cancellation ensures the test fails instead of hanging if the value is never flushed.
                    TaskCompletionSource<bool> gate = _gates[i];
                    using CancellationTokenRegistration registration = cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken));
                    await gate.Task;
                }
            }
        }

        /// <summary>An enumerable whose producer always suspends between elements.</summary>
        private sealed class SlowAsyncEnumerable : IAsyncEnumerable<int>
        {
            private readonly int _count;

            public SlowAsyncEnumerable(int count) => _count = count;

            public bool Disposed { get; private set; }

            public async IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                try
                {
                    for (int i = 0; i < _count; i++)
                    {
                        yield return i;
                        await Task.Delay(10, CancellationToken.None);
                    }
                }
                finally
                {
                    Disposed = true;
                }
            }
        }

        private sealed class FlushTrackingStream : MemoryStream
        {
            /// <summary>Gets the contents of the stream as observed at the time of every flush.</summary>
            public List<string> FlushSnapshots { get; } = new();

            /// <summary>Gets or sets a callback invoked after every recorded flush.</summary>
            public Action OnFlush { get; set; }

            public override void Flush()
            {
                FlushSnapshots.Add(Encoding.UTF8.GetString(ToArray()));
                OnFlush?.Invoke();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Flush();
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_PipeWriter_TopLevelValues_RoundTripsWithDeserializeAsyncEnumerable()
        {
            const int count = 50;
            Pipe pipe = new();

            Task writeTask = YamlSerializer.SerializeAsyncEnumerable(
                pipe.Writer,
                GenerateItems(count),
                ResolveYamlTypeInfo<SimpleTestClass>(),
                topLevelValues: true);

            byte[] bytes = await ReadAllAsync(pipe, writeTask);
            using MemoryStream stream = new(bytes);

            int i = 0;
            await foreach (SimpleTestClass item in YamlSerializer.DeserializeAsyncEnumerable<SimpleTestClass>(stream, topLevelValues: true))
            {
                Assert.Equal(i++, item.MyInt32);
            }

            Assert.Equal(count, i);
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_TopLevelValues_LineTerminatorIsAlwaysLineFeed(string optionsNewLine)
        {
            // Per API review feedback (#126395): the JSONL line terminator is canonical \n
            // regardless of YamlSerializerOptions.NewLine.
            YamlSerializerOptions options = new() { NewLine = optionsNewLine };

            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                GenerateItems(3),
                ResolveYamlTypeInfo<SimpleTestClass>(options),
                topLevelValues: true);

            byte[] bytes = stream.ToArray();
            Assert.DoesNotContain((byte)'\r', bytes);
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_EmptySequence_ProducesEmptyOutput()
        {
            using MemoryStream stream = new();

            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                EmptyAsyncEnumerable(),
                ResolveYamlTypeInfo<int>(),
                topLevelValues: true);

            Assert.Equal(0, stream.Length);

            static async IAsyncEnumerable<int> EmptyAsyncEnumerable()
            {
                await Task.CompletedTask;
                yield break;
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_DisposesAsyncEnumerator()
        {
            DisposableAsyncEnumerable enumerable = new();
            using MemoryStream stream = new();

            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                enumerable,
                ResolveYamlTypeInfo<int>(),
                topLevelValues: true);

            Assert.True(enumerable.Disposed);
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_CancellationToken_DisposesAsyncEnumerator()
        {
            DisposableAsyncEnumerable enumerable = new();
            using MemoryStream stream = new();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                YamlSerializer.SerializeAsyncEnumerable(
                    stream,
                    enumerable,
                    ResolveYamlTypeInfo<int>(),
                    topLevelValues: true,
                    cts.Token));

            Assert.True(enumerable.Disposed);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_YamlTypeInfo_ArrayMode_WorksWithoutAsyncEnumerableMetadata()
        {
            // The YamlTypeInfo<TValue> overload should not require the resolver to know
            // IAsyncEnumerable<TValue>; the wrapper type info is synthesized from the supplied element metadata.
            YamlSerializerOptions options = new() { TypeInfoResolver = new EmptyResolver() };

            YamlTypeInfo<int> elementInfo = YamlMetadataServices.CreateValueInfo<int>(options, YamlMetadataServices.Int32Converter);
            options.MakeReadOnly();

            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(stream, GenerateInts(), elementInfo);

            string actual = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("[1,2,3]", actual);

            static async IAsyncEnumerable<int> GenerateInts()
            {
                yield return 1;
                yield return 2;
                yield return 3;
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_Options_ArrayMode_WorksWithoutAsyncEnumerableMetadata()
        {
            // The YamlSerializerOptions-based overload must also synthesize IAsyncEnumerable<TValue>
            // metadata from the element type info: a source-gen resolver may register only the element type.
            YamlSerializerOptions options = new() { TypeInfoResolver = new ElementOnlyInt32Resolver() };

            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(stream, GenerateInts(), options: options);

            string actual = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("[1,2,3]", actual);

            static async IAsyncEnumerable<int> GenerateInts()
            {
                yield return 1;
                yield return 2;
                yield return 3;
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_TopLevelValues_PropagatesCustomEncoder()
        {
            // YamlSerializerOptions.Encoder must propagate to the JSONL writer. UnsafeRelaxedJsonEscaping
            // leaves '+' unescaped; the default encoder escapes it as \u002B.
            YamlSerializerOptions strict = new();
            YamlSerializerOptions relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            using MemoryStream strictStream = new();
            using MemoryStream relaxedStream = new();

            await YamlSerializer.SerializeAsyncEnumerable(strictStream, Strings(), ResolveYamlTypeInfo<string>(strict), topLevelValues: true);
            await YamlSerializer.SerializeAsyncEnumerable(relaxedStream, Strings(), ResolveYamlTypeInfo<string>(relaxed), topLevelValues: true);

            Assert.Equal("\"a\\u002Bb\"\n", Encoding.UTF8.GetString(strictStream.ToArray()));
            Assert.Equal("\"a+b\"\n", Encoding.UTF8.GetString(relaxedStream.ToArray()));

            static async IAsyncEnumerable<string> Strings()
            {
                yield return "a+b";
                await Task.CompletedTask;
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_HandlesItemsLargerThanDefaultBufferSize()
        {
            // The Utf8YamlWriter calls _output.Advance + GetMemory mid-write (Grow) when an item is
            // larger than its current span. Verify large items round-trip cleanly.
            const int LargeStringLength = 16 * 1024;
            string payload = new('x', LargeStringLength);

            YamlSerializerOptions options = new() { DefaultBufferSize = 64 };

            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                Items(),
                ResolveYamlTypeInfo<string>(options),
                topLevelValues: true);

            stream.Position = 0;
            int i = 0;
            await foreach (string? item in YamlSerializer.DeserializeAsyncEnumerable<string>(stream, topLevelValues: true, options))
            {
                Assert.Equal(payload, item);
                i++;
            }
            Assert.Equal(3, i);

            async IAsyncEnumerable<string> Items()
            {
                yield return payload;
                yield return payload;
                yield return payload;
                await Task.CompletedTask;
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_PolymorphicItems()
        {
            // Polymorphic root-level values: each derived type must serialize with its discriminator
            // and round-trip back to the correct derived instance.
            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                Items(),
                ResolveYamlTypeInfo<PolymorphicBase>(),
                topLevelValues: true);

            stream.Position = 0;
            List<PolymorphicBase?> roundTripped = new();
            await foreach (PolymorphicBase? item in YamlSerializer.DeserializeAsyncEnumerable<PolymorphicBase>(stream, topLevelValues: true))
            {
                roundTripped.Add(item);
            }

            Assert.Equal(2, roundTripped.Count);
            Assert.IsType<PolymorphicDerivedA>(roundTripped[0]);
            Assert.Equal(7, ((PolymorphicDerivedA)roundTripped[0]!).A);
            Assert.IsType<PolymorphicDerivedB>(roundTripped[1]);
            Assert.Equal("hi", ((PolymorphicDerivedB)roundTripped[1]!).B);

            static async IAsyncEnumerable<PolymorphicBase> Items()
            {
                yield return new PolymorphicDerivedA { A = 7 };
                yield return new PolymorphicDerivedB { B = "hi" };
                await Task.CompletedTask;
            }
        }

        public static IEnumerable<object[]> LargeRecordSerializationOptions()
        {
            foreach (bool topLevelValues in new[] { false, true })
            foreach (bool useTypeInfo in new[] { false, true })
            foreach (bool useSourceGeneration in new[] { false, true })
            {
                yield return new object[] { topLevelValues, useTypeInfo, useSourceGeneration };
            }
        }

        private static StreamingRecord CreateStreamingRecord(int cellCount, Action onSerialize)
        {
            string value = new('a', 1000);
            return new StreamingRecord
            {
                Cells = Enumerable.Range(0, cellCount)
                    .Select(_ => new StreamingCell { Value = value, OnSerialize = onSerialize })
                    .ToList()
            };
        }

        private static YamlTypeInfo<T> CreateStreamingTypeInfo<T>(bool useSourceGeneration, YamlSerializerOptions? options = null)
        {
            options ??= new();
            options.TypeInfoResolver = useSourceGeneration ? new StreamingRecordContext() : new DefaultYamlTypeInfoResolver();
            return ResolveYamlTypeInfo<T>(options);
        }

        private static string ExpectedStreamingRecords(int cellCount, bool topLevelValues)
        {
            string cell = "{\"Value\":\"" + new string('a', 1000) + "\"}";
            string record = "{\"Cells\":[" + string.Join(",", Enumerable.Repeat(cell, cellCount)) + "]}";
            return topLevelValues ? record + "\n" + record + "\n" : "[" + record + "," + record + "]";
        }

        private static async IAsyncEnumerable<T> RepeatAsync<T>(T value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return value;
            }

            await Task.CompletedTask;
        }

        public class StreamingRecord
        {
            public List<StreamingCell> Cells { get; set; } = new();
        }

        public class StreamingEnumerableRecord
        {
            public IEnumerable<StreamingCell> Cells { get; set; }
        }

        public class StreamingCell
        {
            private string _value = "";

            public string Value
            {
                get
                {
                    OnSerialize?.Invoke();
                    return _value;
                }
                set => _value = value;
            }

            [YamlIgnore]
            public Action? OnSerialize { get; set; }
        }

        [YamlSerializable(typeof(StreamingRecord))]
        [YamlSerializable(typeof(StreamingEnumerableRecord))]
        [YamlSerializable(typeof(object))]
        private partial class StreamingRecordContext : YamlSerializerContext
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization)]
        [YamlSerializable(typeof(StreamingRecord))]
        [YamlSerializable(typeof(List<StreamingRecord>))]
        private partial class StreamingRecordSerializationContext : YamlSerializerContext
        {
        }

        private sealed class GatedWriteStream : MemoryStream
        {
            public TaskCompletionSource<bool> FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int MaxWriteSize { get; private set; }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await BeforeWriteAsync(count, cancellationToken);
                await base.WriteAsync(buffer, offset, count, cancellationToken);
            }

#if NET
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await BeforeWriteAsync(buffer.Length, cancellationToken);
                await base.WriteAsync(buffer, cancellationToken);
            }
#endif

            private async Task BeforeWriteAsync(int count, CancellationToken cancellationToken)
            {
                MaxWriteSize = Math.Max(MaxWriteSize, count);
                if (FirstWrite.TrySetResult(true))
                {
                    using CancellationTokenRegistration registration = cancellationToken.Register(() => Release.TrySetCanceled(cancellationToken));
                    await Release.Task;
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsyncEnumerable_LargeRecord_StreamCancellation_DisposesEnumerators(bool useSourceGeneration)
        {
            int visitedCells = 0;
            int disposedCells = 0;
            int disposedRecords = 0;
            StreamingRecord record = CreateStreamingRecord(1024, () => visitedCells++);
            StreamingEnumerableRecord value = new() { Cells = Cells() };
            using GatedWriteStream stream = new();
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            Task writeTask = YamlSerializer.SerializeAsyncEnumerable(
                stream, Records(), CreateStreamingTypeInfo<StreamingEnumerableRecord>(useSourceGeneration),
                topLevelValues: true, cts.Token);

            try
            {
                await Task.WhenAny(stream.FirstWrite.Task, writeTask);
                Assert.False(writeTask.IsCompleted);
                Assert.InRange(visitedCells, 1, 1023);
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writeTask);
                Assert.Equal(1, disposedCells);
                Assert.Equal(1, disposedRecords);
                Assert.Equal(0, stream.Length);
                Assert.True(stream.CanWrite);
            }
            finally
            {
                cts.Cancel();
                stream.Release.TrySetResult(true);
                try
                {
                    await writeTask;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
            }

            IEnumerable<StreamingCell> Cells()
            {
                try
                {
                    foreach (StreamingCell cell in record.Cells)
                    {
                        yield return cell;
                    }
                }
                finally
                {
                    disposedCells++;
                }
            }

            async IAsyncEnumerable<StreamingEnumerableRecord> Records()
            {
                try
                {
                    yield return value;
                    Assert.Fail("The next record should not be requested.");
                    await Task.CompletedTask;
                }
                finally
                {
                    disposedRecords++;
                }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [Trait("category", "jsonoutput")]
        public async Task SerializeAsyncEnumerable_LargeRecord_PipeInterruption_DisposesEnumerators(int interruption)
        {
            int visitedCells = 0;
            int disposedCells = 0;
            int disposedRecords = 0;
            StreamingRecord record = CreateStreamingRecord(1024, () => visitedCells++);
            StreamingEnumerableRecord value = new() { Cells = Cells() };
            Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 32 * 1024, resumeWriterThreshold: 16 * 1024));
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            Task writeTask = YamlSerializer.SerializeAsyncEnumerable(
                pipe.Writer, Records(), ResolveYamlTypeInfo<StreamingEnumerableRecord>(), topLevelValues: true, cts.Token);

            try
            {
                ReadResult result = await pipe.Reader.ReadAsync(cts.Token);
                Assert.False(writeTask.IsCompleted);
                Assert.InRange(visitedCells, 1, 1023);
                Assert.InRange(result.Buffer.Length, 32 * 1024, 128 * 1024 - 1);
                foreach (ReadOnlyMemory<byte> segment in result.Buffer)
                {
                    Assert.DoesNotContain((byte)'\n', segment.ToArray());
                }

                switch (interruption)
                {
                    case 1:
                        cts.Cancel();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writeTask);
                        break;
                    case 2:
                        pipe.Writer.CancelPendingFlush();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writeTask);
                        break;
                }

                Assert.Equal(1, disposedCells);
                Assert.Equal(1, disposedRecords);
            }
            finally
            {
                await pipe.Reader.CompleteAsync();
                try
                {
                    await writeTask;
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    await pipe.Writer.CompleteAsync();
                }
            }

            IEnumerable<StreamingCell> Cells()
            {
                try
                {
                    foreach (StreamingCell cell in record.Cells)
                    {
                        yield return cell;
                    }
                }
                finally
                {
                    disposedCells++;
                }
            }

            async IAsyncEnumerable<StreamingEnumerableRecord> Records()
            {
                try
                {
                    yield return value;
                    Assert.Fail("The next record should not be requested.");
                    await Task.CompletedTask;
                }
                finally
                {
                    disposedRecords++;
                }
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task SerializeAsyncEnumerable_TopLevelValues_SerializationOnlyMetadata_MatchesSerializeAsync(bool usePipe, bool collection)
        {
            StreamingRecord record = CreateStreamingRecord(2, () => { });
            if (collection)
            {
                List<StreamingRecord> value = new() { record };
                await Test(value, StreamingRecordSerializationContext.Default.ListStreamingRecord);
            }
            else
            {
                await Test(record, StreamingRecordSerializationContext.Default.StreamingRecord);
            }

            async Task Test<T>(T value, YamlTypeInfo<T> typeInfo)
            {
                using MemoryStream control = new();
                InvalidOperationException expected;
                if (usePipe)
                {
                    PipeWriter writer = PipeWriter.Create(control, new StreamPipeWriterOptions(leaveOpen: true));
                    try
                    {
                        expected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                            YamlSerializer.SerializeAsync(writer, value, typeInfo));
                    }
                    finally
                    {
                        await writer.CompleteAsync();
                    }
                }
                else
                {
                    expected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        YamlSerializer.SerializeAsync(control, value, typeInfo));
                }

                using MemoryStream stream = new();
                InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    SerializeYamlLines(stream, RepeatAsync(value, 2), typeInfo, usePipe));

                Assert.Equal(expected.Message, actual.Message);
                Assert.Equal(0, stream.Length);
                Assert.True(stream.CanWrite);
            }
        }

        private static async Task SerializeYamlLines<T>(
            Stream stream, IAsyncEnumerable<T> values, YamlTypeInfo<T> typeInfo, bool usePipe)
        {
            if (usePipe)
            {
                PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsyncEnumerable(writer, values, typeInfo, topLevelValues: true);
                }
                finally
                {
                    await writer.CompleteAsync();
                }
            }
            else
            {
                await YamlSerializer.SerializeAsyncEnumerable(stream, values, typeInfo, topLevelValues: true);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsyncEnumerable_PipeWriterWithoutUnflushedBytes_MatchesSerializeAsync(bool useTypeInfo)
        {
            bool disposed = false;
            StreamingRecord record = new();
            YamlTypeInfo<StreamingRecord> typeInfo = CreateStreamingTypeInfo<StreamingRecord>(false);
            using MemoryStream stream = new();
            PipeWriter innerWriter = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
            NonReportingPipeWriter writer = new(innerWriter);

            try
            {
                InvalidOperationException expected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    YamlSerializer.SerializeAsync(writer, record, typeInfo));
                InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    useTypeInfo
                        ? YamlSerializer.SerializeAsyncEnumerable(writer, Values(), typeInfo, topLevelValues: true)
                        : YamlSerializer.SerializeAsyncEnumerable(writer, Values(), topLevelValues: true, typeInfo.Options));

                Assert.Equal(expected.Message, actual.Message);
                Assert.True(disposed);
                Assert.False(writer.Completed);
                Assert.Equal(0, stream.Length);
            }
            finally
            {
                await innerWriter.CompleteAsync();
            }

            async IAsyncEnumerable<StreamingRecord> Values()
            {
                try
                {
                    yield return record;
                    Assert.Fail("The next record should not be requested.");
                    await Task.CompletedTask;
                }
                finally
                {
                    disposed = true;
                }
            }
        }

        private sealed class NonReportingPipeWriter(PipeWriter writer) : PipeWriter
        {
            public bool Completed { get; private set; }
            public override void Advance(int bytes) => writer.Advance(bytes);
            public override Memory<byte> GetMemory(int sizeHint = 0) => writer.GetMemory(sizeHint);
            public override Span<byte> GetSpan(int sizeHint = 0) => writer.GetSpan(sizeHint);
            public override void CancelPendingFlush() => writer.CancelPendingFlush();
            public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => writer.FlushAsync(cancellationToken);
            public override void Complete(Exception? exception = null)
            {
                Completed = true;
                writer.Complete(exception);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsyncEnumerable_TopLevelValues_SourceGeneratedRecords_StopsWhenReaderCompletes(bool asObject)
        {
            StreamingRecord record = new();
            if (asObject)
            {
                await Test<object>(record, CreateStreamingTypeInfo<object>(true));
            }
            else
            {
                await Test(record, CreateStreamingTypeInfo<StreamingRecord>(true));
            }

            static async Task Test<T>(T value, YamlTypeInfo<T> typeInfo)
            {
                for (int i = 0; i < 10; i++)
                {
                    await YamlSerializer.SerializeAsync(Stream.Null, value, typeInfo);
                }

                int yielded = 0;
                CompletingPipeWriter writer = new(completeAfterFlushes: 2);
                await YamlSerializer.SerializeAsyncEnumerable(writer, Values(), typeInfo, topLevelValues: true);
                Assert.Equal(2, yielded);
                Assert.Equal(2, writer.FlushCount);

                async IAsyncEnumerable<T> Values()
                {
                    for (int i = 0; i < 20; i++)
                    {
                        yielded++;
                        yield return value;
                    }

                    await Task.CompletedTask;
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsyncEnumerable_TopLevelValues_MaxDepth_IsRespected(bool usePipe)
        {
            StreamingRecord record = CreateStreamingRecord(1, () => { });
            YamlTypeInfo<StreamingRecord> typeInfo = CreateStreamingTypeInfo<StreamingRecord>(
                useSourceGeneration: true, new YamlSerializerOptions { MaxDepth = 2 });
            using MemoryStream stream = new();

            await Assert.ThrowsAsync<YamlException>(() => SerializeYamlLines(stream, RepeatAsync(record, 1), typeInfo, usePipe));
            Assert.DoesNotContain("\n", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_PipeWriter_TopLevelValues_StopsWhenReaderCompletes()
        {
            // Verify that when FlushAsync returns IsCompleted=true (the reader has completed), the
            // writer breaks out of the loop and does not consume the rest of the IAsyncEnumerable.
            int yielded = 0;
            CompletingPipeWriter pipeWriter = new(completeAfterFlushes: 2);

            await YamlSerializer.SerializeAsyncEnumerable(
                pipeWriter,
                Items(),
                ResolveYamlTypeInfo<int>(),
                topLevelValues: true);

            // Stops after producing the items that triggered the first two flushes; the rest of the
            // 100-item enumerable is never consumed.
            Assert.InRange(yielded, 1, 10);
            Assert.Equal(2, pipeWriter.FlushCount);

            async IAsyncEnumerable<int> Items()
            {
                for (int i = 0; i < 100; i++)
                {
                    yielded++;
                    yield return i;
                    await Task.Yield();
                }
            }
        }

        private sealed class CompletingPipeWriter : PipeWriter
        {
            private readonly int _completeAfterFlushes;
            private byte[] _buffer = new byte[1024];
            private int _written;

            public CompletingPipeWriter(int completeAfterFlushes)
            {
                _completeAfterFlushes = completeAfterFlushes;
            }

            public int FlushCount { get; private set; }
            public override bool CanGetUnflushedBytes => true;
            public override long UnflushedBytes => _written;

            public override void Advance(int bytes) => _written += bytes;

            public override Memory<byte> GetMemory(int sizeHint = 0)
            {
                if (sizeHint <= 0) sizeHint = 256;
                if (_written + sizeHint > _buffer.Length)
                {
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + sizeHint));
                }
                return _buffer.AsMemory(_written);
            }

            public override Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

            public override void CancelPendingFlush() { }
            public override void Complete(Exception? exception = null) { }

            public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                FlushCount++;
                bool isCompleted = FlushCount >= _completeAfterFlushes;
                return new ValueTask<FlushResult>(new FlushResult(isCanceled: false, isCompleted: isCompleted));
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_PipeReader_RoundTrip()
        {
            // Round-trip serialize-via-Stream → deserialize-via-PipeReader.
            using MemoryStream stream = new();
            await YamlSerializer.SerializeAsyncEnumerable(
                stream,
                GenerateItems(20),
                ResolveYamlTypeInfo<SimpleTestClass>(),
                topLevelValues: true);

            stream.Position = 0;
            PipeReader reader = PipeReader.Create(stream);

            try
            {
                int i = 0;
                await foreach (SimpleTestClass? item in YamlSerializer.DeserializeAsyncEnumerable<SimpleTestClass>(reader, topLevelValues: true))
                {
                    Assert.NotNull(item);
                    Assert.Equal(i++, item.MyInt32);
                }
                Assert.Equal(20, i);
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_TopLevelValues_CancellationMidIteration()
        {
            // Cancel after a few items have been emitted; verify the remaining items are not consumed
            // and an OperationCanceledException is raised.
            using CancellationTokenSource cts = new();
            using MemoryStream stream = new();
            int yielded = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                YamlSerializer.SerializeAsyncEnumerable(
                    stream,
                    Items(),
                    ResolveYamlTypeInfo<int>(),
                    topLevelValues: true,
                    cts.Token));

            // We expected to produce a few items before cancellation took effect.
            Assert.InRange(yielded, 1, 10);

            async IAsyncEnumerable<int> Items()
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (i == 3)
                    {
                        cts.Cancel();
                    }
                    yielded++;
                    yield return i;
                    await Task.Yield();
                }
            }
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
        [YamlDerivedType(typeof(PolymorphicDerivedA), typeDiscriminator: "a")]
        [YamlDerivedType(typeof(PolymorphicDerivedB), typeDiscriminator: "b")]
        public abstract class PolymorphicBase { }

        public sealed class PolymorphicDerivedA : PolymorphicBase
        {
            public int A { get; set; }
        }

        public sealed class PolymorphicDerivedB : PolymorphicBase
        {
            public string? B { get; set; }
        }

        [Fact]
        public async Task SerializeAsyncEnumerable_NullArguments_ThrowsArgumentNullException()
        {
            YamlTypeInfo<int> typeInfo = ResolveYamlTypeInfo<int>();
            IAsyncEnumerable<int> source = AsyncEnumerable();

            await AssertExtensions.ThrowsAsync<ArgumentNullException>("utf8Yaml", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(utf8Yaml: (Stream)null!, source));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("value", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new MemoryStream(), value: null!));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("utf8Yaml", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(utf8Yaml: (Stream)null!, source, typeInfo));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("value", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new MemoryStream(), value: null!, typeInfo));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("yamlTypeInfo", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new MemoryStream(), source, yamlTypeInfo: null!));

            await AssertExtensions.ThrowsAsync<ArgumentNullException>("utf8Yaml", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(utf8Yaml: (PipeWriter)null!, source));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("value", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new Pipe().Writer, value: null!));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("utf8Yaml", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(utf8Yaml: (PipeWriter)null!, source, typeInfo));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("value", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new Pipe().Writer, value: null!, typeInfo));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("yamlTypeInfo", () =>
                YamlSerializer.SerializeAsyncEnumerable<int>(new Pipe().Writer, source, yamlTypeInfo: null!));

            static async IAsyncEnumerable<int> AsyncEnumerable()
            {
                await Task.CompletedTask;
                yield break;
            }
        }

        // ----------------------------------------------------------
        // DeserializeAsyncEnumerable JSONL spec coverage (issue 126395)
        // ----------------------------------------------------------

        public static IEnumerable<object[]> YamlLinesValidShapes()
        {
            // Per https://jsonlines.org/, every line is a valid JSON value separated by \n.
            // The reader is intentionally lenient (Postel's law) so it accepts a superset of strict JSONL.

            // Strict JSONL with trailing line feed.
            yield return new object[] { "1\n2\n3\n", new[] { 1, 2, 3 } };
            // Strict JSONL without trailing line feed.
            yield return new object[] { "1\n2\n3", new[] { 1, 2, 3 } };
            // Single value, no trailing newline.
            yield return new object[] { "42", new[] { 42 } };
            // Single value, with trailing newline.
            yield return new object[] { "42\n", new[] { 42 } };
            // Empty document.
            yield return new object[] { "", Array.Empty<int>() };
            // Document containing only whitespace.
            yield return new object[] { "   \n\t\n", Array.Empty<int>() };
            // Document containing only line terminators.
            yield return new object[] { "\n\n\n", Array.Empty<int>() };
            // Lenient: \r\n separator (still valid JSONL since values are self-delimiting).
            yield return new object[] { "1\r\n2\r\n3\r\n", new[] { 1, 2, 3 } };
            // Lenient: extra whitespace (tabs, spaces) between/around values.
            yield return new object[] { "  1  \n\t2\t\n 3 \n", new[] { 1, 2, 3 } };
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private static async IAsyncEnumerable<SimpleTestClass> GenerateItems(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = new SimpleTestClass();
                obj.Initialize();
                obj.MyInt32 = i;
                yield return obj;
                await Task.Yield();
            }
        }

        private static async Task<byte[]> ReadAllAsync(Pipe pipe, Task writerTask)
        {
            // Run the writer to completion in parallel with the reader.
            // When the writer finishes, signal end-of-stream by completing the PipeWriter.
            Task completer = writerTask.ContinueWith(
                t => pipe.Writer.CompleteAsync(t.Exception?.InnerException).AsTask(),
                TaskScheduler.Default).Unwrap();

            using MemoryStream output = new();
            try
            {
                while (true)
                {
                    ReadResult result = await pipe.Reader.ReadAsync();
                    foreach (ReadOnlyMemory<byte> segment in result.Buffer)
                    {
                        byte[] tmp = segment.ToArray();
                        output.Write(tmp, 0, tmp.Length);
                    }

                    pipe.Reader.AdvanceTo(result.Buffer.End);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await pipe.Reader.CompleteAsync();
            }

            await completer;
            await writerTask;
            return output.ToArray();
        }
    }
}
