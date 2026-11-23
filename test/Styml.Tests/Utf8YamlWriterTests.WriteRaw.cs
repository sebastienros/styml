// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public partial class Utf8YamlWriterTests
    {
        private const string TestGuidAsStr = "eb97fadd-3ebf-4781-8722-f4773989160e";
        private readonly static Guid s_guid = Guid.Parse(TestGuidAsStr);

        private static byte[] s_oneAsYaml = new byte[] { (byte)'1' };

        [Theory]
        [MemberData(nameof(GetRootLevelPrimitives))]
        [MemberData(nameof(GetArrays))]
        public static void WriteRawValidYaml(byte[] rawYaml, Action<byte[]> verifyWithDeserialize)
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);

            string rawYamlAsStr = Encoding.UTF8.GetString(rawYaml);

            RunTests(skipInputValidation: true);
            RunTests(skipInputValidation: false);

            void RunTests(bool skipInputValidation)
            {
                // ROS<byte>
                writer.Reset();
                ms.SetLength(0);
                WriteRawValueWithSetting(writer, rawYamlAsStr, OverloadParamType.ByteArray, skipInputValidation);
                writer.Flush();
                verifyWithDeserialize(ms.ToArray());

                // string
                
                writer.Reset();
                ms.SetLength(0);
                WriteRawValueWithSetting(writer, rawYamlAsStr, OverloadParamType.String, skipInputValidation);
                writer.Flush();
                verifyWithDeserialize(ms.ToArray());

                // ROS<char>
                writer.Reset();
                ms.SetLength(0);
                WriteRawValueWithSetting(writer, rawYamlAsStr, OverloadParamType.ROSChar, skipInputValidation);
                writer.Flush();
                verifyWithDeserialize(ms.ToArray());

                // ROS<char>
                writer.Reset();
                ms.SetLength(0);
                WriteRawValueWithSetting(writer, rawYamlAsStr, OverloadParamType.ROSeqByte, skipInputValidation);
                writer.Flush();
                verifyWithDeserialize(ms.ToArray());
            }
        }

        public static IEnumerable<object[]> GetRootLevelPrimitives()
        {
            Action<byte[]> validate;

            validate = (data) => Assert.Equal(123456789, YamlSerializer.Deserialize<long>(data));
            yield return new object[] { "123456789"u8.ToArray(), validate };

            validate = (data) => Assert.Equal(1234.56789, YamlSerializer.Deserialize<double>(data));
            yield return new object[] { "1234.56789"u8.ToArray(), validate };

            validate = (data) => Assert.Equal(1234.56789, YamlSerializer.Deserialize<double>(data));
            yield return new object[] { " 1234.56789 "u8.ToArray(), validate };

            validate = (data) => Assert.Equal(@"Hello", YamlSerializer.Deserialize<string>(data));
            yield return new object[] { Encoding.UTF8.GetBytes("""
                "Hello"
                """), validate };

            validate = (data) => Assert.Equal(@"Hello", YamlSerializer.Deserialize<string>(data));
            yield return new object[] { Encoding.UTF8.GetBytes("""  "Hello"  """), validate };

            validate = (data) => Assert.Equal(s_guid, YamlSerializer.Deserialize<Guid>(data));
            byte[] guidAsYaml = WrapInQuotes(Encoding.UTF8.GetBytes(TestGuidAsStr));
            yield return new object[] { guidAsYaml, validate };
        }

        public static IEnumerable<object[]> GetArrays()
        {
            Action<byte[]> validate;

            byte[] yaml = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Repeat(1234.56789, 4));
            validate = (data) =>
            {
                foreach (double d in YamlSerializer.Deserialize<double[]>(data))
                {
                    Assert.Equal(1234.56789, d);
                }
            };
            yield return new object[] { yaml, validate };

            yaml = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Repeat("Hello", 4));
            validate = (data) =>
            {
                foreach (string str in YamlSerializer.Deserialize<string[]>(data))
                {
                    Assert.Equal("Hello", str);
                }
            };
            yield return new object[] { yaml, validate };

            yaml = YamlSerializer.SerializeToUtf8Bytes(Enumerable.Repeat("Hello", 4));
            validate = (data) =>
            {
                foreach (string str in YamlSerializer.Deserialize<string[]>(data))
                {
                    Assert.Equal("Hello", str);
                }
            };
            yield return new object[] { yaml, validate };

            yaml = "[ 1, 1,1,1,1 ] "u8.ToArray();
            validate = (data) =>
            {
                foreach (int val in YamlSerializer.Deserialize<int[]>(data))
                {
                    Assert.Equal(1, val);
                }
            };
            yield return new object[] { yaml, validate };
        }

        public static IEnumerable<object[]> GetObjects()
        {
            Action<byte[]> validate;

            byte[] yaml = Encoding.UTF8.GetBytes("""{"Hello":"World"}"""); ;
            validate = (data) =>
            {
                KeyValuePair<string, string> kvp = YamlSerializer.Deserialize<Dictionary<string, string>>(data).Single();
                Assert.Equal("Hello", kvp.Key);
                Assert.Equal("World", kvp.Value);
            };
            yield return new object[] { yaml, validate };

            yaml = Encoding.UTF8.GetBytes(""" {  "Hello"    :"World"  }   """); ;
            validate = (data) =>
            {
                KeyValuePair<string, string> kvp = YamlSerializer.Deserialize<Dictionary<string, string>>(data).Single();
                Assert.Equal("Hello", kvp.Key);
                Assert.Equal("World", kvp.Value);
            };
            yield return new object[] { yaml, validate };
        }

        private static byte[] WrapInQuotes(ReadOnlySpan<byte> buffer)
        {
            byte[] quotedBuffer = new byte[buffer.Length + 2];
            quotedBuffer[0] = (byte)'"';
            buffer.CopyTo(quotedBuffer.AsSpan(1));
            quotedBuffer[buffer.Length + 1] = (byte)'"';
            return quotedBuffer;
        }

        [Theory]
        [InlineData(true, 0, "[]")]
        [InlineData(false, 0, "[]")]
        [InlineData(true, 1, "[1]")]
        [InlineData(false, 1, "[1]")]
        [InlineData(true, 5, "[1,1,1,1,1]")]
        [InlineData(false, 5, "[1,1,1,1,1]")]
        [Trait("category", "jsonoutput")]
        public static void WriteRawArrayElements(bool skipInputValidation, int numElements, string expectedYaml)
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);
            writer.WriteStartArray();

            for (int i = 0; i < numElements; i++)
            {
                writer.WriteRawValue(s_oneAsYaml, skipInputValidation);
            }

            writer.WriteEndArray();

            writer.Flush();
            Assert.Equal(expectedYaml, Encoding.UTF8.GetString(ms.ToArray()));
        }

        [Theory]
        [InlineData(true, 0, "{}")]
        [InlineData(false, 0, "{}")]
        [InlineData(true, 1, """{"int":1}""")]
        [InlineData(false, 1, """{"int":1}""")]
        [InlineData(true, 3, """{"int":1,"int":1,"int":1}""")]
        [InlineData(false, 3, """{"int":1,"int":1,"int":1}""")]
        [Trait("category", "jsonoutput")]
        public static void WriteRawObjectProperty(bool skipInputValidation, int numElements, string expectedYaml)
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);
            writer.WriteStartObject();

            for (int i = 0; i < numElements; i++)
            {
                writer.WritePropertyName("int");
                writer.WriteRawValue(s_oneAsYaml, skipInputValidation);
            }

            writer.WriteEndObject();

            writer.Flush();
            Assert.Equal(expectedYaml, Encoding.UTF8.GetString(ms.ToArray()));
        }

        [Fact]
        public static void WriteRawNullOrEmptyTokenInvalid()
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);
            Assert.Throws<ArgumentNullException>(() => writer.WriteRawValue(yaml: default(string)));
            Assert.Throws<ArgumentException>(() => writer.WriteRawValue(yaml: ""));
            Assert.Throws<ArgumentException>(() => writer.WriteRawValue(yaml: default(ReadOnlySpan<char>)));
            Assert.Throws<ArgumentException>(() => writer.WriteRawValue(utf8Yaml: default(ReadOnlySpan<byte>)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public static void WriteRawHonorSkipValidation(bool skipValidation)
        {
            RunTest(true);
            RunTest(false);

            void RunTest(bool skipInputValidation)
            {
                using MemoryStream ms = new();
                using Utf8YamlWriter writer = new(ms, new YamlWriterOptions { SkipValidation = skipValidation });

                writer.WriteStartObject();

                if (skipValidation)
                {
                    writer.WriteRawValue("{}", skipInputValidation);
                    writer.Flush();
                    Assert.True(ms.ToArray().SequenceEqual(new byte[] { (byte)'{',  (byte)'{', (byte)'}' }));
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => writer.WriteRawValue("{}", skipInputValidation));
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteRawDepthExceedsMaxOf64Fail()
        {

            RunTest(GenerateYamlUsingDepth(1), false);
            RunTest(GenerateYamlUsingDepth(64), false);
            RunTest(GenerateYamlUsingDepth(65), true);
            RunTest(GenerateYamlUsingDepth(65), false, true);

            void RunTest(string yaml, bool expectFail, bool skipInputValidation = false)
            {
                using MemoryStream ms = new();
                using Utf8YamlWriter writer = new(ms);

                if (expectFail)
                {
                    Assert.ThrowsAny<YamlException>(() => writer.WriteRawValue(yaml, skipInputValidation));
                }
                else
                {
                    writer.WriteRawValue(yaml, skipInputValidation);
                    writer.Flush();

                    Assert.Equal(yaml, Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
        }

        private static string GenerateYamlUsingDepth(int depth)
        {
            Assert.True(depth > 0 && depth <= 65, "Test depth out of range");

            StringBuilder sb = new();
            sb.Append("{");

            for (int i = 0; i < depth - 1; i++)
            {
                sb.Append("""
                    "prop":{
                    """);
            }

            for (int i = 0; i < depth - 1; i++)
            {
                sb.Append("}");
            }

            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// This test is constrained to run on Windows and MacOSX because it causes
        /// problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        /// succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        /// time the memory is accessed which triggers the full memory allocation.
        /// Also see <see cref="WriteLargeYamlToStreamWithoutFlushing"/>
        /// </summary>
        [ActiveIssue("https://github.com/dotnet/runtime/issues/88272")]
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        public void WriteRawLargeYamlToStreamWithoutFlushing()
        {
            try
            {
                var largeArray = new char[150_000_000];
                largeArray.AsSpan().Fill('a');

                // Text size chosen so that after several doublings of the underlying buffer we reach ~2 GB (but don't go over)
                YamlEncodedText text1 = YamlEncodedText.Encode(largeArray.AsSpan(0, 7_500));
                YamlEncodedText text2 = YamlEncodedText.Encode(largeArray.AsSpan(0, 5_000));
                YamlEncodedText text3 = YamlEncodedText.Encode(largeArray.AsSpan(0, 150_000_000));

                using (var output = new MemoryStream())
                using (var writer = new Utf8YamlWriter(output))
                {
                    writer.WriteStartArray();
                    writer.WriteRawValue(WrapInQuotes(text1.EncodedUtf8Bytes));
                    Assert.Equal(7_503, writer.BytesPending);

                    for (int i = 0; i < 30_000; i++)
                    {
                        writer.WriteRawValue(WrapInQuotes(text2.EncodedUtf8Bytes));
                    }
                    Assert.Equal(150_097_503, writer.BytesPending);

                    for (int i = 0; i < 13; i++)
                    {
                        writer.WriteRawValue(WrapInQuotes(text3.EncodedUtf8Bytes));
                    }
                    Assert.Equal(2_100_097_542, writer.BytesPending);

                    // Next write forces a grow beyond max array length

                    Assert.Throws<OutOfMemoryException>(() => writer.WriteRawValue(WrapInQuotes(text3.EncodedUtf8Bytes)));

                    Assert.Equal(2_100_097_542, writer.BytesPending);

                    var text4 = YamlEncodedText.Encode(largeArray.AsSpan(0, 1));
                    for (int i = 0; i < 10_000_000; i++)
                    {
                        writer.WriteRawValue(WrapInQuotes(text4.EncodedUtf8Bytes));
                    }

                    Assert.Equal(2_100_097_542 + (4 * 10_000_000), writer.BytesPending);
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [InlineData(YamlTokenType.String)]
        [InlineData(YamlTokenType.StartArray)]
        [InlineData(YamlTokenType.StartObject)]
        public static void WriteRawMaxUtf16InputLength(YamlTokenType tokenType)
        {
            try
            {
                // Max raw payload length supported by the writer.
                int maxLength = int.MaxValue / 3;

                StringBuilder sb = new(maxLength + 2);
                sb.Append('"');

                for (int i = 1; i < maxLength - 1; i++)
                {
                    sb.Append('a');
                }

                sb.Append('"');

                string payload = sb.ToString();

                RunTest(OverloadParamType.ROSChar);
                RunTest(OverloadParamType.String);
                RunTest(OverloadParamType.ByteArray);
                RunTest(OverloadParamType.ROSeqByte);

                void RunTest(OverloadParamType paramType)
                {
                    using MemoryStream ms = new();
                    using Utf8YamlWriter writer = new(ms);

                    switch (tokenType)
                    {
                        case YamlTokenType.String:
                            WriteRawValueWithSetting(writer, payload, paramType);
                            writer.Flush();
                            Assert.Equal(payload.Length, writer.BytesCommitted);
                            break;
                        case YamlTokenType.StartArray:
                            writer.WriteStartArray();
                            WriteRawValueWithSetting(writer, payload, paramType);
                            WriteRawValueWithSetting(writer, payload, paramType);
                            writer.WriteEndArray();
                            writer.Flush();
                            // Start/EndArray + comma, 2 array elements
                            Assert.Equal(3 + (payload.Length * 2), writer.BytesCommitted);
                            break;
                        case YamlTokenType.StartObject:
                            writer.WriteStartObject();
                            writer.WritePropertyName("1");
                            WriteRawValueWithSetting(writer, payload, paramType);
                            writer.WritePropertyName("2");
                            WriteRawValueWithSetting(writer, payload, paramType);
                            writer.WriteEndObject();
                            writer.Flush();
                            // Start/EndToken + comma, 2 property names, 2 property values
                            Assert.Equal(3 + (4 * 2) + (payload.Length * 2), writer.BytesCommitted);
                            break;
                        default:
                            Assert.Fail("Unexpected test configuration");
                            break;
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        private enum OverloadParamType
        {
            ROSChar,
            String,
            ByteArray,
            ROSeqByte
        }

        private static void WriteRawValueWithSetting(Utf8YamlWriter writer, string payload, OverloadParamType param, bool skipInputValidation = false)
        {
            switch (param)
            {
                case OverloadParamType.ROSChar:
                    writer.WriteRawValue(payload.AsSpan(), skipInputValidation);
                    break;
                case OverloadParamType.String:
                    writer.WriteRawValue(payload, skipInputValidation);
                    break;
                case OverloadParamType.ByteArray:
                    byte[] payloadAsBytes = Encoding.UTF8.GetBytes(payload);
                    writer.WriteRawValue(payloadAsBytes, skipInputValidation);
                    break;
                case OverloadParamType.ROSeqByte:
                    ReadOnlySequence<byte> payloadAsSequence = new(Encoding.UTF8.GetBytes(payload));
                    writer.WriteRawValue(payloadAsSequence, skipInputValidation);
                    break;
            }
        }

        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [InlineData((int.MaxValue / 3) + 1)]
        [InlineData(int.MaxValue / 3 + 2)]
        [OuterLoop]
        public static void WriteRawUtf16LengthGreaterThanMax(int len)
        {
            try
            {
                StringBuilder sb = new(len + 2);
                sb.Append('"');

                for (int i = 1; i < len - 1; i++)
                {
                    sb.Append('a');
                }

                sb.Append('"');

                string payload = sb.ToString();

                using MemoryStream ms = new();
                using Utf8YamlWriter writer = new(ms);

                // UTF-16 overloads not compatible with this length.
                Assert.Throws<ArgumentException>(() => WriteRawValueWithSetting(writer, payload, OverloadParamType.ROSChar));
                Assert.Throws<ArgumentException>(() => WriteRawValueWithSetting(writer, payload, OverloadParamType.String));

                // UTF-8 overload is okay.
                WriteRawValueWithSetting(writer, payload, OverloadParamType.ByteArray);
                writer.Flush();
                Assert.Equal(payload.Length, Encoding.UTF8.GetString(ms.ToArray()).Length);

                writer.Reset();
                ms.SetLength(0);
                WriteRawValueWithSetting(writer, payload, OverloadParamType.ROSeqByte);
                writer.Flush();
                Assert.Equal(payload.Length, Encoding.UTF8.GetString(ms.ToArray()).Length);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [InlineData(int.MaxValue)]
        [InlineData((long)int.MaxValue + 1)]
        [OuterLoop]
        public void WriteRawUtf8LengthGreaterThanOrEqualToIntMax(long len)
        {
            try
            {
                using MemoryStream ms = new();
                using Utf8YamlWriter writer = new(ms);
                ReadOnlySequence<byte> readonlySeq = CreateLargeReadOnlySequence(len);
                Assert.Throws<ArgumentException>(() => writer.WriteRawValue(readonlySeq));
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        public static void WriteRawTranscodeFromUtf16ToUtf8TooLong()
        {
            try
            {
                // Max raw payload length supported by the writer.
                int maxLength = int.MaxValue / 3;

                StringBuilder sb = new(maxLength + 2);
                sb.Append('"');

                for (int i = 1; i < maxLength - 1; i++)
                {
                    sb.Append('\u7684'); // Non-UTF-8 character than will expand during transcoding
                }

                sb.Append('"');

                string payload = sb.ToString();

                RunTest(OverloadParamType.ROSChar);
                RunTest(OverloadParamType.String);
                RunTest(OverloadParamType.ByteArray);
                RunTest(OverloadParamType.ROSeqByte);

                void RunTest(OverloadParamType paramType)
                {
                    using MemoryStream ms = new();
                    using Utf8YamlWriter writer = new(ms);

                    try
                    {
                        WriteRawValueWithSetting(writer, payload, paramType);
                        writer.Flush();

                        // All characters in the payload will be expanded during transcoding, except for the quotes.
                        int expectedLength = ((payload.Length - 2) * 3) + 2;
                        Assert.Equal(expectedLength, writer.BytesCommitted);
                    }
                    catch (OutOfMemoryException) { } // OutOfMemoryException is okay since the transcoding output is probably too large.
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        private static ReadOnlySequence<byte> CreateLargeReadOnlySequence(long len)
        {
            Assert.InRange(len, int.MaxValue, long.MaxValue);

            const int ArrayMaxLength = 0X7FFFFFC7; // Array.MaxLength

            long totalLen = len;
            TestSequenceSegment? startSegment = null;
            TestSequenceSegment? endSegment = null;

            // Construct ReadOnlySequence<byte> with length as 'len' which is greater than or equal to int.MaxValue
            do
            {
                if (startSegment is null) // First segment
                {
                    var bytes = new byte[ArrayMaxLength];
                    bytes[0] = (byte)'"';
                    FillArray(bytes, (byte)'a', 1, bytes.Length);
                    endSegment = startSegment = new(bytes);
                }
                else
                {
                    byte[] bytes;
                    if (totalLen <= ArrayMaxLength) // The last segment
                    {
                        bytes = new byte[totalLen];
                        bytes[totalLen - 1] = (byte)'"';
                    }
                    else
                    {
                        bytes = new byte[ArrayMaxLength];
                        bytes[ArrayMaxLength - 1] = (byte)'a';
                    }

                    FillArray(bytes, (byte)'a', 0, bytes.Length - 1);
                    endSegment = endSegment!.Append(bytes);
                }

                totalLen -= endSegment.Memory.Length;
            } while (totalLen > 0);

            var readonlySeq = new ReadOnlySequence<byte>(startSegment, 0, endSegment, endSegment.Memory.Length);

            Assert.Equal(len, readonlySeq.Length); // Make sure constructed ReadOnlySequence's length is as expected

            return readonlySeq;

            static void FillArray(byte[] bytes, byte value, int start, int end)
            {
                for (int i = start; i < end; i++)
                {
                    bytes[i] = value;
                }
            }
        }

        private class TestSequenceSegment : ReadOnlySequenceSegment<byte>
        {
            public TestSequenceSegment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public TestSequenceSegment Append(ReadOnlyMemory<byte> memory)
            {
                var newSegment = new TestSequenceSegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };

                Next = newSegment;

                return newSegment;
            }
        }
    }
}
