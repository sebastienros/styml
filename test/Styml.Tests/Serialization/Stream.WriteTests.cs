// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests.Schemas.OrderPayload;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public partial class StreamTests
    {
        [Fact]
        public async Task WriteNullArgumentFail()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await YamlSerializer.SerializeAsync((Stream)null, 1));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await YamlSerializer.SerializeAsync((Stream)null, 1, typeof(int)));
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize((Stream)null, 1));
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize((Stream)null, 1, typeof(int)));
        }

        [Fact]
        public async Task VerifyValueFail()
        {
            MemoryStream stream = new MemoryStream();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await YamlSerializer.SerializeAsync(stream, "", (Type)null));
            Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(stream, "", (Type)null));
        }

        [Fact]
        public async Task VerifyTypeFail()
        {
            MemoryStream stream = new MemoryStream();
            await Assert.ThrowsAsync<ArgumentException>(async () => await YamlSerializer.SerializeAsync(stream, 1, typeof(string)));
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(stream, 1, typeof(string)));
        }

        [Fact]
        public async Task NullObjectValue()
        {
            MemoryStream stream = new MemoryStream();

            await Serializer.SerializeWrapper(stream, (object)null);

            stream.Seek(0, SeekOrigin.Begin);

            byte[] readBuffer = new byte[4];
            int bytesRead = stream.Read(readBuffer, 0, 4);

            Assert.Equal(4, bytesRead);
            string value = Encoding.UTF8.GetString(readBuffer);
            Assert.Equal("null", value);
        }

        [Fact]
        [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
        [Trait("category", "jsonoutput")]
        public async Task RoundTripAsync()
        {
            byte[] buffer;

            using (TestStream stream = new TestStream(1))
            {
                await WriteAsync(stream);

                // Make a copy
                buffer = stream.ToArray();
            }

            using (TestStream stream = new TestStream(buffer))
            {
                await ReadAsync(stream);
            }
        }

        [Fact]

        public async Task RoundTripLargeYamlViaYamlElementAsync()
        {
            // Generating tailored json
            int i = 0;
            StringBuilder yaml = new StringBuilder();
            yaml.Append("{");
            while (true)
            {
                if (yaml.Length >= 14757)
                {
                    break;
                }
                yaml.AppendFormat("""
                    "Key_{0}":"{0}",
                    """, i);
                i++;
            }
            yaml.Remove(yaml.Length - 1, 1).Append("}");

            YamlElement root = YamlSerializer.Deserialize<YamlElement>(yaml.ToString());
            var ms = new MemoryStream();

            await Serializer.SerializeWrapper(ms, root, root.GetType());
        }

        [Fact]
        public async Task RoundTripLargeYamlViaPocoAsync()
        {
            byte[] array = YamlSerializer.Deserialize<byte[]>(YamlSerializer.Serialize(new byte[11056]));
            var ms = new MemoryStream();

            await Serializer.SerializeWrapper(ms, array, array.GetType());
        }

        private async Task WriteAsync(TestStream stream)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                // Will likely default to 4K due to buffer pooling.
                DefaultBufferSize = 1
            };

            {
                LargeDataTestClass obj = new LargeDataTestClass();
                obj.Initialize();
                obj.Verify();

                await Serializer.SerializeWrapper(stream, obj, options: options);
            }

            // Must be changed if the test classes change:
            Assert.Equal(551_368, stream.TestWriteBytesCount);

            // We should have more than one write called due to the large byte count.
            if (Serializer.IsAsyncSerializer)
            {
                Assert.InRange(stream.TestAsyncWriteCount, 2, int.MaxValue);
            }
            else
            {
                Assert.InRange(stream.TestWriteCount, 2, int.MaxValue);
            }
        }

        private async Task ReadAsync(TestStream stream)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                // Will likely default to 4K due to buffer pooling.
                DefaultBufferSize = 1
            };

            LargeDataTestClass obj = await Serializer.DeserializeWrapper<LargeDataTestClass>(stream, options);
            // Must be changed if the test classes change; may be > since last read may not have filled buffer.
            Assert.InRange(stream.TestRequestedReadBytesCount, 551368, int.MaxValue);

            // We should have more than one read called due to the large byte count.
            Assert.InRange(stream.TestReadCount, 2, int.MaxValue);

            // We don't auto-flush.
            Assert.Equal(0, stream.TestFlushCount);

            obj.Verify();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitivesAsync()
        {
            MemoryStream stream = new MemoryStream();
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultBufferSize = 1
            };

            await Serializer.SerializeWrapper(stream, 1, options);
            string yamlSerialized = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("1", yamlSerialized);
        }

        private class Session
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public virtual string Abstract { get; set; }
            public virtual DateTimeOffset? StartTime { get; set; }
            public virtual DateTimeOffset? EndTime { get; set; }
            public TimeSpan Duration => EndTime?.Subtract(StartTime ?? EndTime ?? DateTimeOffset.MinValue) ?? TimeSpan.Zero;
            public int? TrackId { get; set; }
        }

        private class SessionResponse : Session
        {
            public Track Track { get; set; }
            public List<Speaker> Speakers { get; set; } = new List<Speaker>();
        }

        private class Track
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class Speaker
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Bio { get; set; }
            public virtual string WebSite { get; set; }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(4000)]
        [InlineData(8000)]
        [InlineData(16000)]
        [InlineData(32000)]
        [InlineData(64000)]
        public async Task LargeYamlFile(int bufferSize)
        {
            const int SessionResponseCount = 100;

            // Build up a large list to serialize.
            var list = new List<SessionResponse>();
            for (int i = 0; i < SessionResponseCount; i++)
            {
                SessionResponse response = new SessionResponse
                {
                    Id = i,
                    Abstract = new string('A', i * 2),
                    Title = new string('T', i),
                    StartTime = new DateTime(i, DateTimeKind.Utc),
                    EndTime = new DateTime(i * 10000, DateTimeKind.Utc),
                    TrackId = i,
                    Track = new Track()
                    {
                        Id = i,
                        Name = new string('N', i),
                    },
                };

                for (int j = 0; j < 5; j++)
                {
                    response.Speakers.Add(new Speaker()
                    {
                        Bio = new string('B', 50),
                        Id = j,
                        Name = new string('N', i),
                        WebSite = new string('W', 20),
                    });
                }

                list.Add(response);
            }

            // Adjust buffer length to encourage buffer flusing at several levels.
            YamlSerializerOptions options = new YamlSerializerOptions();
            if (bufferSize != 0)
            {
                options.DefaultBufferSize = bufferSize;
            }

            string yaml = YamlSerializer.Serialize(list, options);
            Assert.True(yaml.Length > 100_000); // Verify data is large and will cause buffer flushing.
            Assert.True(yaml.Length < 200_000); // But not too large for memory considerations.

            // Sync case.
            {
                List<SessionResponse> deserializedList = YamlSerializer.Deserialize<List<SessionResponse>>(yaml, options);
                Assert.Equal(SessionResponseCount, deserializedList.Count);

                string yamlSerialized = YamlSerializer.Serialize(deserializedList, options);
                Assert.Equal(yaml, yamlSerialized);
            }

            // Async case.
            using (var memoryStream = new MemoryStream())
            {
                await Serializer.SerializeWrapper(memoryStream, list, options);
                string yamlSerialized = Encoding.UTF8.GetString(memoryStream.ToArray());
                Assert.Equal(yaml, yamlSerialized);

                memoryStream.Position = 0;
                List<SessionResponse> deserializedList = await Serializer.DeserializeWrapper<List<SessionResponse>>(memoryStream, options);
                Assert.Equal(SessionResponseCount, deserializedList.Count);
            }
        }

        [Theory]
        [InlineData(1, true, true)]
        [InlineData(1, true, false)]
        [InlineData(1, false, true)]
        [InlineData(1, false, false)]
        [InlineData(10, true, false)]
        [InlineData(10, false, false)]
        [InlineData(100, false, false)]
        [InlineData(1000, false, false)]
        public async Task VeryLargeYamlFileTest(int payloadSize, bool ignoreNull, bool writeIndented)
        {
            List<Order> list = Order.PopulateLargeObject(payloadSize);

            YamlSerializerOptions options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = ignoreNull ? YamlIgnoreCondition.WhenWritingNull : YamlIgnoreCondition.Never,
                WriteIndented = writeIndented
            };

            string yaml = YamlSerializer.Serialize(list, options);

            // Sync case.
            {
                List<Order> deserializedList = YamlSerializer.Deserialize<List<Order>>(yaml, options);
                Assert.Equal(payloadSize, deserializedList.Count);

                string yamlSerialized = YamlSerializer.Serialize(deserializedList, options);
                Assert.Equal(yaml, yamlSerialized);
            }

            // Async case.
            using (var memoryStream = new MemoryStream())
            {
                await Serializer.SerializeWrapper(memoryStream, list, options);
                string yamlSerialized = Encoding.UTF8.GetString(memoryStream.ToArray());
                Assert.Equal(yaml, yamlSerialized);

                memoryStream.Position = 0;
                List<Order> deserializedList = await Serializer.DeserializeWrapper<List<Order>>(memoryStream, options);
                Assert.Equal(payloadSize, deserializedList.Count);
            }
        }

        [Theory]
        [InlineData(1, true, true)]
        [InlineData(1, true, false)]
        [InlineData(1, false, true)]
        [InlineData(1, false, false)]
        [InlineData(2, true, false)]
        [InlineData(2, false, false)]
        [InlineData(4, false, false)]
        [InlineData(8, false, false)] // Greater depths have caused failures on some test machine configurations due to memory constraints
        public async Task DeepNestedYamlFileTest(int depthFactor, bool ignoreNull, bool writeIndented)
        {
            const int ListLength = 10;

            int length = ListLength * depthFactor;
            List<Order>[] orders = new List<Order>[length];
            orders[0] = Order.PopulateLargeObject(1);
            for (int i = 1; i < length; i++ )
            {
                orders[i] = Order.PopulateLargeObject(1);
                orders[i - 1][0].RelatedOrder = orders[i];
            }

            YamlSerializerOptions options = new YamlSerializerOptions()
            {
                MaxDepth = (ListLength * depthFactor * 2) + 4, // Order-to-RelatedOrder has a depth of 2.
                DefaultIgnoreCondition = ignoreNull ? YamlIgnoreCondition.WhenWritingNull : YamlIgnoreCondition.Never,
                WriteIndented = writeIndented
            };
            string yaml = YamlSerializer.Serialize(orders[0], options);

            // Sync case.
            {
                List<Order> deserializedList = YamlSerializer.Deserialize<List<Order>>(yaml, options);

                string yamlSerialized = YamlSerializer.Serialize(deserializedList, options);
                Assert.Equal(yaml, yamlSerialized);
            }

            // Async case.
            using (var memoryStream = new MemoryStream())
            {
                await Serializer.SerializeWrapper(memoryStream, orders[0], options);
                string yamlSerialized = Encoding.UTF8.GetString(memoryStream.ToArray());
                Assert.Equal(yaml, yamlSerialized);

                memoryStream.Position = 0;
                List<Order> deserializedList = await Serializer.DeserializeWrapper<List<Order>>(memoryStream, options);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public async Task NestedYamlFileCircularDependencyTest(int depthFactor)
        {
            const int ListLength = 2;

            int length = ListLength * depthFactor;
            List<Order>[] orders = new List<Order>[length];
            orders[0] = Order.PopulateLargeObject(1000);
            for (int i = 1; i < length; i++)
            {
                orders[i] = Order.PopulateLargeObject(1);
                orders[i - 1][0].RelatedOrder = orders[i];
            }

            YamlSerializerOptions options = new YamlSerializerOptions()
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull
            };

            // Ensure no exception for default settings (MaxDepth=64) and no cycle.
            YamlSerializer.Serialize(orders[0], options);

            // Create a cycle.
            orders[length - 1][0].RelatedOrder = orders[0];

            Assert.Throws<YamlException> (() => YamlSerializer.Serialize(orders[0], options));

            using (var memoryStream = new MemoryStream())
            {
                await Assert.ThrowsAsync<YamlException>(async () => await Serializer.SerializeWrapper(memoryStream, orders[0], options));
            }
        }

        [Fact]
        public void NestedSerializeCallsFlushAtThreshold()
        {
            var data = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var options = new YamlSerializerOptions { DefaultBufferSize = 512 };
            options.Converters.Add(new MyStringConverter());

            using var stream = new TestStream(1);
            YamlSerializer.Serialize(stream, CreateManyTestObjects(), options);

            // Flush should happen every ~460 bytes (+36 for writing data when just below threshold)
            // Assuming a "perfect" array pool implementation, the number of write calls should be closer to (data.Length * 10_000 / (512 * .9))
            // But because the array pool may give a larger buffer than 512, we need to be a little more permissive when checking how many writes occur
            Assert.InRange(stream.TestWriteCount, data.Length * 10_000 / 5000, data.Length * 10_000 / 200);

            IEnumerable<string> CreateManyTestObjects()
            {
                int i = 0;
                while (++i < 10_000)
                {
                    yield return data;
                }
            }
        }

        [Fact]
        public void SerializeCallsFlushAtThreshold()
        {
            var data = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var options = new YamlSerializerOptions { DefaultBufferSize = 512 };

            using var stream = new TestStream(1);
            YamlSerializer.Serialize(stream, CreateManyTestObjects(), options);

            // Flush should happen every ~460 bytes (+36 for writing data when just below threshold)
            // Assuming a "perfect" array pool implementation, the number of write calls should be closer to (data.Length * 10_000 / (512 * .9))
            // But because the array pool may give a larger buffer than 512, we need to be a little more permissive when checking how many writes occur
            Assert.InRange(stream.TestWriteCount, data.Length * 10_000 / 5000, data.Length * 10_000 / 200);

            IEnumerable<string> CreateManyTestObjects()
            {
                int i = 0;
                while (++i < 10_000)
                {
                    yield return data;
                }
            }
        }

        [Fact]
        public async Task NestedSerializeAsyncCallsFlushAtThreshold()
        {
            var data = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var options = new YamlSerializerOptions { DefaultBufferSize = 512 };
            options.Converters.Add(new MyStringConverter());

            using var stream = new TestStream(1);
            await YamlSerializer.SerializeAsync(stream, CreateManyTestObjects(), options);

            // Flush should happen every ~460 bytes (+36 for writing data when just below threshold)
            // Assuming a "perfect" array pool implementation, the number of write calls should be closer to (data.Length * 10_000 / (512 * .9))
            // But because the array pool may give a larger buffer than 512, we need to be a little more permissive when checking how many writes occur
            Assert.InRange(stream.TestAsyncWriteCount, data.Length * 10_000 / 5000, data.Length * 10_000 / 200);

            IEnumerable<string> CreateManyTestObjects()
            {
                int i = 0;
                while (++i < 10_000)
                {
                    yield return data;
                }
            }
        }

        [Fact]
        public async Task SerializeAsyncCallsFlushAtThreshold()
        {
            var data = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var options = new YamlSerializerOptions { DefaultBufferSize = 512 };

            using var stream = new TestStream(1);
            await YamlSerializer.SerializeAsync(stream, CreateManyTestObjects(), options);

            // Flush should happen every ~460 bytes (+36 for writing data when just below threshold)
            // Assuming a "perfect" array pool implementation, the number of write calls should be closer to (data.Length * 10_000 / (512 * .9))
            // But because the array pool may give a larger buffer than 512, we need to be a little more permissive when checking how many writes occur
            Assert.InRange(stream.TestAsyncWriteCount, data.Length * 10_000 / 5000, data.Length * 10_000 / 200);

            IEnumerable<string> CreateManyTestObjects()
            {
                int i = 0;
                while (++i < 10_000)
                {
                    yield return data;
                }
            }
        }

        class MyStringConverter : YamlConverter<string>
        {
            public override string Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                throw new NotImplementedException();

            public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options)
            {
                YamlSerializer.Serialize(writer, value);
            }
        }

        [Theory]
        [InlineData(32)]
        [InlineData(128)]
        [InlineData(1024)]
        [InlineData(1024 * 16)] // the default YamlSerializerOptions.DefaultBufferSize value
        [InlineData(1024 * 1024)]
        [Trait("category", "jsonoutput")]
        public async Task ShouldUseFastPathOnSmallPayloads(int defaultBufferSize)
        {
            if (Serializer.ForceSmallBufferInOptions)
            {
                return;
            }

            var instrumentedResolver = new PocoWithInstrumentedFastPath.Context(
                new YamlSerializerOptions
                {
                    DefaultBufferSize = defaultBufferSize,
                });

            // The current implementation uses a heuristic
            int smallValueThreshold = defaultBufferSize / 2;
            PocoWithInstrumentedFastPath smallValue = CreateValueWithSerializationSize(smallValueThreshold);

            var stream = new MemoryStream();

            // The first 10 serializations should not call into the fast path
            for (int i = 0; i < 10; i++)
            {
                await Serializer.SerializeWrapper(stream, smallValue, instrumentedResolver.Options);
                stream.Position = 0;
                Assert.Equal(0, instrumentedResolver.FastPathInvocationCount);
            }

            // Subsequent iterations do call into the fast path
            for (int i = 0; i < 10; i++)
            {
                await Serializer.SerializeWrapper(stream, smallValue, instrumentedResolver.Options);
                stream.Position = 0;
                Assert.Equal(i + 1, instrumentedResolver.FastPathInvocationCount);
            }

            // Polymorphic serialization should use the fast path
            await Serializer.SerializeWrapper(stream, (object)smallValue, instrumentedResolver.Options);
            stream.Position = 0;
            Assert.Equal(11, instrumentedResolver.FastPathInvocationCount);

            // Attempt to serialize a value that is deemed large
            var largeValue = CreateValueWithSerializationSize(smallValueThreshold + 1);
            await Serializer.SerializeWrapper(stream, largeValue, instrumentedResolver.Options);
            stream.Position = 0;
            Assert.Equal(12, instrumentedResolver.FastPathInvocationCount);

            // Any subsequent attempts no longer call into the fast path
            for (int i = 0; i < 10; i++)
            {
                await Serializer.SerializeWrapper(stream, smallValue, instrumentedResolver.Options);
                stream.Position = 0;
                Assert.Equal(12, instrumentedResolver.FastPathInvocationCount);
            }

            static PocoWithInstrumentedFastPath CreateValueWithSerializationSize(int targetSerializationSize)
            {
                int objectSerializationPaddingSize = """{"Value":""}""".Length; // 12
                return new PocoWithInstrumentedFastPath { Value = new string('a', targetSerializationSize - objectSerializationPaddingSize) };
            }
        }

        public class PocoWithInstrumentedFastPath
        {
            public string? Value { get; set; }

            public class Context : YamlSerializerContext, IYamlTypeInfoResolver
            {
                public int FastPathInvocationCount { get; private set; }

                public Context(YamlSerializerOptions options) : base(options)
                { }

                protected override YamlSerializerOptions? GeneratedSerializerOptions => Options;
                public override YamlTypeInfo? GetTypeInfo(Type type) => GetTypeInfo(type, Options);

                public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
                {
                    if (type == typeof(string))
                    {
                        return YamlMetadataServices.CreateValueInfo<string>(options, YamlMetadataServices.StringConverter);
                    }

                    if (type == typeof(object))
                    {
                        return YamlMetadataServices.CreateValueInfo<object>(options, YamlMetadataServices.ObjectConverter);
                    }

                    if (type == typeof(PocoWithInstrumentedFastPath))
                    {
                        return YamlMetadataServices.CreateObjectInfo<PocoWithInstrumentedFastPath>(options,
                            new YamlObjectInfoValues<PocoWithInstrumentedFastPath>
                            {
                                PropertyMetadataInitializer = _ => new YamlPropertyInfo[1]
                                {
                                    YamlMetadataServices.CreatePropertyInfo<string>(options,
                                        new YamlPropertyInfoValues<string>
                                        {
                                            DeclaringType = typeof(PocoWithInstrumentedFastPath),
                                            PropertyName = "Value",
                                            Getter = obj => ((PocoWithInstrumentedFastPath)obj).Value,
                                        })
                                },

                                SerializeHandler = (writer, value) =>
                                {
                                    writer.WriteStartObject();
                                    writer.WriteString("Value", value.Value);
                                    writer.WriteEndObject();
                                    FastPathInvocationCount++;
                                }
                            });
                    }

                    return null;
                }
            }
        }

        [Theory]
        [InlineData(128)]
        [InlineData(1024)]
        [InlineData(4096)]
        [InlineData(8192)]
        [InlineData(16384)]
        [InlineData(65536)]
        public async Task FlushThresholdTest(int bufferSize)
        {
            // bufferSize * 0.9 is the threshold size from codebase, subtract 2 for [" characters, then create a 
            // string containing (threshold - 2) amount of char 'a' which when written into output buffer produces buffer 
            // which size equal to or very close to threshold size, then adding the string to the list, then adding a big 
            // object to the list which changes depth of written json and should cause buffer flush
            int thresholdSize = (int)(bufferSize * 0.9 - 2);
            FlushThresholdTestClass serializeObject = new FlushThresholdTestClass(GenerateListOfSize(bufferSize));
            List<object> list = new List<object>();
            string stringOfThresholdSize = new string('a', thresholdSize);
            list.Add(stringOfThresholdSize);
            serializeObject.StringProperty = stringOfThresholdSize;
            list.Add(serializeObject);
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.DefaultBufferSize = bufferSize;

            string yaml = YamlSerializer.Serialize(list);

            using (var memoryStream = new MemoryStream())
            {
                await Serializer.SerializeWrapper(memoryStream, list, options);
                string yamlSerialized = Encoding.UTF8.GetString(memoryStream.ToArray());
                Assert.Equal(yaml, yamlSerialized);

                List<object> deserializedList = YamlSerializer.Deserialize<List<object>>(yaml, options);
                Assert.Equal(stringOfThresholdSize, ((YamlElement)deserializedList[0]).GetString());
                YamlElement obj = (YamlElement)deserializedList[1];             
                Assert.Equal(stringOfThresholdSize, obj.GetProperty("StringProperty").GetString());
            }
        }

        private class FlushThresholdTestClass 
        {
            public string StringProperty { get; set; }
            public List<int> ListOfInts { get; set; }
            public FlushThresholdTestClass(List<int> list)
            {
                ListOfInts = list;
            }
        }

        private static List<int> GenerateListOfSize(int size)
        {
            List<int> list = new List<int>();
            for (int i = 0; i < size; i++)
            {
                list.Add(1);
            }
            return list;
        }
    }

    public sealed class TestStream : Stream
    {
        private readonly MemoryStream _stream;

        public TestStream(int capacity) { _stream = new MemoryStream(capacity); }

        public TestStream(byte[] buffer) { _stream = new MemoryStream(buffer); }

        public int TestFlushCount { get; private set; }

        public int TestWriteCount { get; private set; }
        public int TestAsyncWriteCount { get; private set; }
        public int TestWriteBytesCount { get; private set; }
        public int TestReadCount { get; private set; }
        public int TestRequestedReadBytesCount { get; private set; }

        public byte[] ToArray() => _stream.ToArray();

        public override void Flush()
        {
            TestFlushCount++;
            _stream.Flush();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            TestWriteCount++;
            TestWriteBytesCount += (count - offset);
            _stream.Write(buffer, offset, count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            TestReadCount++;
            TestRequestedReadBytesCount += count;
            return _stream.Read(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            TestAsyncWriteCount++;
            TestWriteBytesCount += (count - offset);
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => _stream.SetLength(value);
        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
    }
}
