// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class Utf8YamlReaderTests
    {
        [Fact]
        public static void InitialStateMultiSegment()
        {
            byte[] utf8 = "1"u8.ToArray();
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);
            var yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.NotEqual(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.False(yaml.Read());
        }

        [Theory]
        [InlineData("2e2", 200, 3)]
        [InlineData("2e+2", 200, 4)]
        [InlineData("123e-01", 12.3, 7)]
        [InlineData("0", 0, 1)]
        [InlineData("0.1", 0.1, 3)]
        [InlineData("-0", 0, 2)]
        [InlineData("-0.1", -0.1, 4)]
        [InlineData("   2e2   ", 200, 3)]
        [InlineData("   2e+2   ", 200, 4)]
        [InlineData("   123e-01   ", 12.3, 7)]
        [InlineData("   0   ", 0, 1)]
        [InlineData("   0.1   ", 0.1, 3)]
        [InlineData("   -0   ", 0, 2)]
        [InlineData("   -0.1   ", -0.1, 4)]
        [InlineData("[2e2]", 200, 3)]
        [InlineData("[2e+2]", 200, 4)]
        [InlineData("[123e-01]", 12.3, 7)]
        [InlineData("[0]", 0, 1)]
        [InlineData("[0.1]", 0.1, 3)]
        [InlineData("[-0]", 0, 2)]
        [InlineData("[-0.1]", -0.1, 4)]
        [InlineData("{\"foo\": 2e2}", 200, 3)]
        [InlineData("{\"foo\": 2e+2}", 200, 4)]
        [InlineData("{\"foo\": 123e-01}", 12.3, 7)]
        [InlineData("{\"foo\": 0}", 0, 1)]
        [InlineData("{\"foo\": 0.1}", 0.1, 3)]
        [InlineData("{\"foo\": -0}", 0, 2)]
        [InlineData("{\"foo\": -0.1}", -0.1, 4)]
        public static void ReadYamlWithNumberVariousSegmentSizes(string input, double expectedValue, long expectedTokenLength)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(input);

            var yamlReader = new Utf8YamlReader(utf8);
            ReadDoubleHelper(ref yamlReader, utf8.Length, expectedValue, expectedTokenLength);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);
            yamlReader = new Utf8YamlReader(sequence);
            ReadDoubleHelper(ref yamlReader, utf8.Length, expectedValue, expectedTokenLength);

            for (int splitLocation = 0; splitLocation < utf8.Length; splitLocation++)
            {
                sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                yamlReader = new Utf8YamlReader(sequence);
                ReadDoubleHelper(ref yamlReader, utf8.Length, expectedValue, expectedTokenLength);
            }

            for (int firstSplit = 0; firstSplit < utf8.Length; firstSplit++)
            {
                for (int secondSplit = firstSplit; secondSplit < utf8.Length; secondSplit++)
                {
                    sequence = YamlTestHelper.CreateSegments(utf8, firstSplit, secondSplit);
                    yamlReader = new Utf8YamlReader(sequence);
                    ReadDoubleHelper(ref yamlReader, utf8.Length, expectedValue, expectedTokenLength);
                }
            }
        }

        private static void ReadDoubleHelper(ref Utf8YamlReader yamlReader, int expectedBytesConsumed, double expectedValue, long expectedTokenLength)
        {
            while (yamlReader.Read())
            {
                if (yamlReader.TokenType == YamlTokenType.Number)
                {
                    long tokenLength = yamlReader.HasValueSequence ? yamlReader.ValueSequence.Length : yamlReader.ValueSpan.Length;
                    Assert.Equal(expectedTokenLength, tokenLength);
                    Assert.Equal(tokenLength, yamlReader.BytesConsumed - yamlReader.TokenStartIndex);
                    Assert.Equal(expectedValue, yamlReader.GetDouble());
                }
            }
            Assert.Equal(expectedBytesConsumed, yamlReader.BytesConsumed);
        }

        private static void InvalidReadNumberHelper(ref Utf8YamlReader yamlReader, int expectedBytePositionInLine, bool firstReadSuccessful, int expectedConsumed, char invalidChar)
        {
            if (firstReadSuccessful)
            {
                Assert.True(yamlReader.Read());

                long tokenLength = yamlReader.HasValueSequence ? yamlReader.ValueSequence.Length : yamlReader.ValueSpan.Length;
                Assert.Equal(expectedConsumed, tokenLength);
                Assert.Equal(0, yamlReader.TokenStartIndex);
                Assert.Equal(expectedConsumed, yamlReader.BytesConsumed - yamlReader.TokenStartIndex);
            }

            try
            {
                yamlReader.Read();
                Assert.Fail("Expected YamlException was not thrown for incomplete/invalid JSON payload reading.");
            }
            catch (YamlException ex)
            {
                Assert.Equal(0, ex.LineNumber);
                Assert.Equal(expectedBytePositionInLine, ex.BytePositionInLine);
                Assert.Equal(expectedConsumed, yamlReader.BytesConsumed);
                Assert.Contains($"'{invalidChar}'", ex.Message);
            }
        }

        [Fact]
        public static void ReadComplexNestedYamlMultiSegment()
        {
            string yamlString = """{"array":[1,2,{"nested":"value","number":42.5e10}],"bool":true,"null":null}""";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.PropertyName, yamlReader.TokenType);
                Assert.Equal("array", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartArray, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(1, yamlReader.GetInt32());
                Assert.True(yamlReader.Read());
                Assert.Equal(2, yamlReader.GetInt32());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal("nested", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal("value", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal("number", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal(42.5e10, yamlReader.GetDouble());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndArray, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal("bool", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.True(yamlReader.GetBoolean());
                Assert.True(yamlReader.Read());
                Assert.Equal("null", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.Null, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndObject, yamlReader.TokenType);
                Assert.False(yamlReader.Read());
            }
        }

        [Fact]
        public static void ReadLongStringAcrossSegments()
        {
            string longString = new string('a', 1000);
            string yamlString = $$"""{"key":"{{longString}}"}""";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int segmentSize = 10; segmentSize < 100; segmentSize += 10)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, segmentSize);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.PropertyName, yamlReader.TokenType);
                Assert.Equal("key", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.String, yamlReader.TokenType);
                Assert.Equal(longString, yamlReader.GetString());
                Assert.True(yamlReader.HasValueSequence);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndObject, yamlReader.TokenType);
                Assert.False(yamlReader.Read());
            }
        }

        [Fact]
        public static void ReadEscapedStringAcrossSegmentBoundary()
        {
            string yamlString = """{"key":"value\twith\nescape\u0041sequences"}""";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.True(yamlReader.Read());
                Assert.Equal("key", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal("value\twith\nescape\u0041sequences", yamlReader.GetString());
                Assert.True(yamlReader.ValueIsEscaped);
            }
        }

        [Fact]
        public static void ReadNumberWithExponentAcrossSegments()
        {
            string yamlString = "[1.23456789e+100,-9.87654321e-50,1e308]";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartArray, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(1.23456789e+100, yamlReader.GetDouble());
                Assert.True(yamlReader.Read());
                Assert.Equal(-9.87654321e-50, yamlReader.GetDouble());
                Assert.True(yamlReader.Read());
                Assert.Equal(1e308, yamlReader.GetDouble());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndArray, yamlReader.TokenType);
                Assert.False(yamlReader.Read());
            }
        }

        [Fact]
        public static void ReadLiteralsAcrossSegmentBoundaries()
        {
            string yamlString = """{"t":true,"f":false,"n":null}""";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal("t", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.True(yamlReader.GetBoolean());
                Assert.True(yamlReader.Read());
                Assert.Equal("f", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.False(yamlReader.GetBoolean());
                Assert.True(yamlReader.Read());
                Assert.Equal("n", yamlReader.GetString());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.Null, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndObject, yamlReader.TokenType);
                Assert.False(yamlReader.Read());
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public static void ReadDeeplyNestedArraysMultiSegment(int depth)
        {
            string yaml = new string('[', depth) + "1" + new string(']', depth);
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 3);
            var yamlReader = new Utf8YamlReader(sequence);
            
            for (int i = 0; i < depth; i++)
            {
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartArray, yamlReader.TokenType);
                Assert.Equal(i, yamlReader.CurrentDepth);
            }
            
            Assert.True(yamlReader.Read());
            Assert.Equal(1, yamlReader.GetInt32());
            Assert.Equal(depth, yamlReader.CurrentDepth);
            
            for (int i = depth - 1; i >= 0; i--)
            {
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndArray, yamlReader.TokenType);
                Assert.Equal(i, yamlReader.CurrentDepth);
            }
            
            Assert.False(yamlReader.Read());
        }

        [Fact]
        public static void ReadPropertyNameAcrossSegmentWithEscaping()
        {
            string yamlString = """{"prop\u0065rty":123}""";
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
            {
                ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8, splitLocation);
                var yamlReader = new Utf8YamlReader(sequence);
                
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.StartObject, yamlReader.TokenType);
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.PropertyName, yamlReader.TokenType);
                Assert.Equal("property", yamlReader.GetString());
                Assert.True(yamlReader.ValueIsEscaped);
                Assert.True(yamlReader.Read());
                Assert.Equal(123, yamlReader.GetInt32());
                Assert.True(yamlReader.Read());
                Assert.Equal(YamlTokenType.EndObject, yamlReader.TokenType);
                Assert.False(yamlReader.Read());
            }
        }

        private static void ValidReadStringHelper(ref Utf8YamlReader yamlReader, int expectedBytePositionInLine, int expectedConsumed, string expectedStr, int expectedTokenLength)
        {
            Assert.True(yamlReader.Read());

            Assert.Equal(YamlTokenType.String, yamlReader.TokenType);
            long tokenLength = yamlReader.HasValueSequence ? yamlReader.ValueSequence.Length : yamlReader.ValueSpan.Length;
            Assert.Equal(expectedTokenLength, tokenLength);
            Assert.Equal(expectedTokenLength + 2, yamlReader.BytesConsumed - yamlReader.TokenStartIndex);
            Assert.Equal(expectedStr, yamlReader.GetString());

            try
            {
                yamlReader.Read();
                Assert.Fail("Expected YamlException was not thrown for incomplete/invalid JSON payload reading.");
            }
            catch (YamlException ex)
            {
                Assert.Equal(0, ex.LineNumber);
                Assert.Equal(expectedBytePositionInLine, ex.BytePositionInLine);
                Assert.Equal(expectedConsumed, yamlReader.BytesConsumed);
            }
        }

        private static void InvalidReadStringHelper(ref Utf8YamlReader yamlReader, int expectedBytePositionInLine, int numberOfSuccessfulReads, int expectedConsumed)
        {
            for (int i = 0; i < numberOfSuccessfulReads; i++)
            {
                Assert.True(yamlReader.Read());
            }

            try
            {
                yamlReader.Read();
                Assert.Fail("Expected YamlException was not thrown for incomplete/invalid JSON payload reading.");
            }
            catch (YamlException ex)
            {
                Assert.Equal(0, ex.LineNumber);
                Assert.Equal(expectedBytePositionInLine, ex.BytePositionInLine);
                Assert.Equal(expectedConsumed, yamlReader.BytesConsumed);
            }
        }

        [Fact]
        public static void InitialStateSimpleCtorMultiSegment()
        {
            byte[] utf8 = "1"u8.ToArray();
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);
            var yaml = new Utf8YamlReader(sequence);

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.NotEqual(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.False(yaml.Read());
        }

        // TestCaseType is only used to give the json strings a descriptive name.
        [Theory]
        // Skipping large JSON since slicing them (O(n^2)) is too slow.
        [MemberData(nameof(SmallTestCases))]
        public static void TestYamlReaderUtf8SegmentSizeOne(bool compactData, TestCaseType type, string yamlString)
        {
            ReadPartialSegmentSizeOne(compactData, type, yamlString);
        }

        // TestCaseType is only used to give the json strings a descriptive name.
        [Theory]
        [MemberData(nameof(LargeTestCases))]
        public static void TestYamlReaderLargeUtf8SegmentSizeOne(bool compactData, TestCaseType type, string yamlString)
        {
            // Skipping really large JSON on Browser to prevent OOM
            if (PlatformDetection.IsBrowser && (type == TestCaseType.Yaml40KB || type == TestCaseType.Yaml400KB || type == TestCaseType.ProjectLockYaml))
            {
                return;
            }

            ReadFullySegmentSizeOne(compactData, type, yamlString);
        }

        // TestCaseType is only used to give the json strings a descriptive name.
        [Theory]
        [OuterLoop]
        [MemberData(nameof(LargeTestCases))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        public static void TestYamlReaderLargestUtf8SegmentSizeOne(bool compactData, TestCaseType type, string yamlString)
        {
            // Skipping really large JSON since slicing them (O(n^2)) is too slow.
            if (type == TestCaseType.Yaml40KB || type == TestCaseType.Yaml400KB || type == TestCaseType.ProjectLockYaml)
            {
                return;
            }

            ReadPartialSegmentSizeOne(compactData, type, yamlString);
        }

        private static void ReadPartialSegmentSizeOne(bool compactData, TestCaseType type, string yamlString)
        {
            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, 1);

            for (int j = 0; j < dataUtf8.Length; j++)
            {
                var utf8YamlReader = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                byte[] resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8YamlReader);
                string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

                long consumed = utf8YamlReader.BytesConsumed;
                utf8YamlReader = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, utf8YamlReader.CurrentState);
                resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8YamlReader);
                actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
                string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8YamlReader.BytesConsumed}, Index: {j}";
                Assert.True(dataUtf8.Length - consumed == utf8YamlReader.BytesConsumed, message);
                Assert.Equal(expectedStr, actualStrSequence);
            }
        }

        private static void ReadFullySegmentSizeOne(bool compactData, TestCaseType type, string yamlString)
        {
            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, 1);

            var utf8YamlReader = new Utf8YamlReader(sequence, isFinalBlock: true, default);
            byte[] resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8YamlReader);
            string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);
            Assert.Equal(expectedStr, actualStrSequence);
        }

        [Theory]
        [MemberData(nameof(SmallTestCases))]
        public static void TestPartialYamlReaderMultiSegment(bool compactData, TestCaseType type, string yamlString)
        {
            _ = type;

            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlyMemory<byte> dataMemory = dataUtf8;

            List<ReadOnlySequence<byte>> sequences = YamlTestHelper.GetSequences(dataMemory);

            for (int i = 0; i < sequences.Count; i++)
            {
                ReadOnlySequence<byte> sequence = sequences[i];
                var yaml = new Utf8YamlReader(sequence, isFinalBlock: true, default);
                while (yaml.Read())
                    ;
                Assert.Equal(sequence.Length, yaml.BytesConsumed);

                Assert.True(sequence.Slice(yaml.Position).IsEmpty);
            }

            for (int i = 0; i < sequences.Count; i++)
            {
                ReadOnlySequence<byte> sequence = sequences[i];
                var yaml = new Utf8YamlReader(sequence);
                while (yaml.Read())
                    ;
                Assert.Equal(sequence.Length, yaml.BytesConsumed);

                Assert.True(sequence.Slice(yaml.Position).IsEmpty);
            }
        }

        [Theory]
        [OuterLoop]
        [MemberData(nameof(SmallTestCases))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        public static void TestPartialYamlReaderSlicesMultiSegment(bool compactData, TestCaseType type, string yamlString)
        {
            _ = type;

            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlyMemory<byte> dataMemory = dataUtf8;

            List<ReadOnlySequence<byte>> sequences = YamlTestHelper.GetSequences(dataMemory);

            for (int i = 0; i < sequences.Count; i++)
            {
                ReadOnlySequence<byte> sequence = sequences[i];
                for (int j = 0; j < dataUtf8.Length; j++)
                {
                    var yaml = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                    while (yaml.Read())
                        ;

                    long consumed = yaml.BytesConsumed;
                    YamlReaderState yamlState = yaml.CurrentState;
                    byte[] consumedArray = sequence.Slice(0, consumed).ToArray();
                    Assert.Equal(consumedArray, sequence.Slice(0, yaml.Position).ToArray());
                    yaml = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, yamlState);
                    while (yaml.Read())
                        ;
                    Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
                }
            }
        }

        [Theory]
        [MemberData(nameof(TrySkipValues))]
        public static void TestTrySkipMultiSegment(string yamlString, YamlTokenType lastToken)
        {
            List<YamlTokenType> expectedTokenTypes = YamlTestHelper.GetTokenTypes(yamlString);
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(dataUtf8);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Disallow);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Skip);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Allow);

            sequence = YamlTestHelper.GetSequence(dataUtf8, 1);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Disallow);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Skip);
            TrySkipHelper(sequence, lastToken, expectedTokenTypes, YamlCommentHandling.Allow);
        }

        private static void TrySkipHelper(ReadOnlySequence<byte> dataUtf8, YamlTokenType lastToken, List<YamlTokenType> expectedTokenTypes, YamlCommentHandling commentHandling)
        {
            var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling });
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

            YamlReaderState previous = yaml.CurrentState;
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(0, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.True(yaml.TrySkip());

            YamlReaderState current = yaml.CurrentState;
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(previous, current);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(0, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            int totalReads = 0;
            while (yaml.Read())
            {
                totalReads++;
            }

            Assert.Equal(expectedTokenTypes.Count, totalReads);

            previous = yaml.CurrentState;
            Assert.Equal(lastToken, yaml.TokenType);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.True(yaml.TrySkip());

            current = yaml.CurrentState;
            Assert.Equal(previous, current);
            Assert.Equal(lastToken, yaml.TokenType);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            for (int i = 0; i < totalReads; i++)
            {
                state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling });
                yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
                for (int j = 0; j < i; j++)
                {
                    Assert.True(yaml.Read());
                }
                Assert.True(yaml.TrySkip());
                Assert.True(expectedTokenTypes[i] == yaml.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {yaml.TokenType}, , Index: {i}, BytesConsumed: {yaml.BytesConsumed}");
            }
        }

        [Fact]
        public static void TestSingleStringsMultiSegmentByOne()
        {
            string yamlString = "\"Hello, \\u0041hson!\"";
            string expectedString = "Hello, \\u0041hson!, ";

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, 1);

            for (int j = 0; j < dataUtf8.Length; j++)
            {
                var utf8YamlReader = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                byte[] resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8YamlReader);
                string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

                long consumed = utf8YamlReader.BytesConsumed;
                utf8YamlReader = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, utf8YamlReader.CurrentState);
                resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8YamlReader);
                actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
                string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8YamlReader.BytesConsumed}, Index: {j}";
                Assert.True(dataUtf8.Length - consumed == utf8YamlReader.BytesConsumed, message);
                Assert.Equal(expectedString, actualStrSequence);
            }
        }

        [Fact]
        public static void TestSingleStringsMultiSegment()
        {
            string yamlString = "\"Hello, \\u0041hson!\"";
            string expectedString = "Hello, \\u0041hson!, ";

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(dataUtf8);

            for (int j = 0; j < dataUtf8.Length; j++)
            {
                var utf8YamlReader = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                byte[] resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8YamlReader);
                string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

                Assert.Equal(0, utf8YamlReader.TokenStartIndex);

                long consumed = utf8YamlReader.BytesConsumed;
                utf8YamlReader = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, utf8YamlReader.CurrentState);
                resultSequence = YamlTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8YamlReader);
                actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
                string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8YamlReader.BytesConsumed}, Index: {j}";
                Assert.True(dataUtf8.Length - consumed == utf8YamlReader.BytesConsumed, message);
                Assert.Equal(expectedString, actualStrSequence);

                Assert.Equal(0, utf8YamlReader.TokenStartIndex);
            }
        }

        [Fact]
        public static void TestMultiSegmentStringConversionToDateTime()
        {
            string yamlString = "\"1997-07-16\"";
            string expectedString = "1997-07-16";
            int expectedTokenLength = 10;
            DateTime expectedDateTime = DateTime.Parse(expectedString);

            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8);

            for (int j = 0; j < utf8.Length; j++)
            {
                var utf8YamlReader = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                ReadDateTimeHelper(ref utf8YamlReader, expectedDateTime, expectedTokenLength);

                Assert.Equal(0, utf8YamlReader.TokenStartIndex);

                long consumed = utf8YamlReader.BytesConsumed;
                utf8YamlReader = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, utf8YamlReader.CurrentState);
                ReadDateTimeHelper(ref utf8YamlReader, expectedDateTime, expectedTokenLength);
            }
        }

        private static void ReadDateTimeHelper(ref Utf8YamlReader yamlReader, DateTime expectedValue, long expectedTokenLength)
        {
            while (yamlReader.Read())
            {
                if (yamlReader.TokenType == YamlTokenType.String)
                {
                    long tokenLength = yamlReader.HasValueSequence ? yamlReader.ValueSequence.Length : yamlReader.ValueSpan.Length;
                    Assert.Equal(expectedTokenLength, tokenLength);
                    Assert.Equal(expectedValue, yamlReader.GetDateTime());
                }
            }
        }

        [Fact]
        public static void TestMultiSegmentStringConversionToDateTimeOffset()
        {
            string yamlString = "\"1997-07-16\"";
            string expectedString = "1997-07-16";
            int expectedTokenLength = 10;
            DateTimeOffset expectedDateTimeOffset = DateTimeOffset.Parse(expectedString);

            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8);

            for (int j = 0; j < utf8.Length; j++)
            {
                var utf8YamlReader = new Utf8YamlReader(sequence.Slice(0, j), isFinalBlock: false, default);
                ReadDateTimeOffsetHelper(ref utf8YamlReader, expectedDateTimeOffset, expectedTokenLength);

                Assert.Equal(0, utf8YamlReader.TokenStartIndex);

                long consumed = utf8YamlReader.BytesConsumed;
                utf8YamlReader = new Utf8YamlReader(sequence.Slice(consumed), isFinalBlock: true, utf8YamlReader.CurrentState);
                ReadDateTimeOffsetHelper(ref utf8YamlReader, expectedDateTimeOffset, expectedTokenLength);
            }
        }

        private static void ReadDateTimeOffsetHelper(ref Utf8YamlReader yamlReader, DateTimeOffset expectedValue, long expectedTokenLength)
        {
            while (yamlReader.Read())
            {
                if (yamlReader.TokenType == YamlTokenType.String)
                {
                    long tokenLength = yamlReader.HasValueSequence ? yamlReader.ValueSequence.Length : yamlReader.ValueSpan.Length;
                    Assert.Equal(expectedTokenLength, tokenLength);
                    Assert.Equal(expectedValue, yamlReader.GetDateTimeOffset());
                }
            }
        }

        private static void SpanSequenceStatesAreEqualInvalidYaml(byte[] dataUtf8, ReadOnlySequence<byte> sequence, int maxDepth, YamlCommentHandling commentHandling)
        {
            var stateSpan = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var yamlSpan = new Utf8YamlReader(dataUtf8, isFinalBlock: true, stateSpan);

            var stateSequence = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var yamlSequence = new Utf8YamlReader(sequence, isFinalBlock: true, stateSequence);

            try
            {
                while (true)
                {
                    bool spanResult = yamlSpan.Read();
                    bool sequenceResult = yamlSequence.Read();

                    Assert.Equal(spanResult, sequenceResult);
                    Assert.Equal(yamlSpan.CurrentDepth, yamlSequence.CurrentDepth);
                    Assert.Equal(yamlSpan.BytesConsumed, yamlSequence.BytesConsumed);
                    Assert.Equal(yamlSpan.TokenType, yamlSequence.TokenType);

                    if (!spanResult)
                    {
                        break;
                    }
                }
                Assert.Fail("Expected YamlException due to invalid JSON.");
            }
            catch (YamlException)
            { }
        }

        private static void SpanSequenceStatesAreEqual(byte[] dataUtf8)
        {
            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(dataUtf8);

            var yamlSpan = new Utf8YamlReader(dataUtf8, isFinalBlock: true, default);
            var yamlSequence = new Utf8YamlReader(sequence, isFinalBlock: true, default);

            while (true)
            {
                bool spanResult = yamlSpan.Read();
                bool sequenceResult = yamlSequence.Read();

                Assert.Equal(spanResult, sequenceResult);
                Assert.Equal(yamlSpan.CurrentDepth, yamlSequence.CurrentDepth);
                Assert.Equal(yamlSpan.BytesConsumed, yamlSequence.BytesConsumed);
                Assert.Equal(yamlSpan.TokenType, yamlSequence.TokenType);

                if (!spanResult)
                {
                    break;
                }
            }
        }

        private static void TestReadingYamlWithComments(byte[] inputData, ReadOnlySequence<byte> sequence, string expectedWithoutComments, string expectedWithComments)
        {
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            var yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state);

            var builder = new StringBuilder();
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number || yaml.TokenType == YamlTokenType.Comment || yaml.TokenType == YamlTokenType.PropertyName)
                {
                    builder.Append(Encoding.UTF8.GetString(yaml.HasValueSequence ? yaml.ValueSequence.ToArray() : yaml.ValueSpan.ToArray()));
                    if (yaml.HasValueSequence)
                    {
                        Assert.True(yaml.ValueSpan == default);
                    }
                    else
                    {
                        Assert.True(yaml.ValueSequence.IsEmpty);
                    }
                }
            }

            Assert.Equal(expectedWithComments, builder.ToString());
            Assert.Equal(inputData, sequence.Slice(0, yaml.Position).ToArray());

            state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip });
            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state);

            builder = new StringBuilder();
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number || yaml.TokenType == YamlTokenType.Comment || yaml.TokenType == YamlTokenType.PropertyName)
                {
                    builder.Append(Encoding.UTF8.GetString(yaml.HasValueSequence ? yaml.ValueSequence.ToArray() : yaml.ValueSpan.ToArray()));
                    if (yaml.HasValueSequence)
                    {
                        Assert.True(yaml.ValueSpan == default);
                    }
                    else
                    {
                        Assert.True(yaml.ValueSequence.IsEmpty);
                    }
                }
            }

            Assert.Equal(expectedWithoutComments, builder.ToString());
            Assert.Equal(inputData, sequence.Slice(0, yaml.Position).ToArray());
        }

        private static void TestReadingSingleValueYaml(byte[] inputData, ReadOnlySequence<byte> sequence, string expectedString)
        {
            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(sequence, false, state);

                while (yaml.Read())
                {
                    // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                    Assert.True(yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null);
                    Assert.Equal(expectedString, Encoding.UTF8.GetString(yaml.HasValueSequence ? yaml.ValueSequence.ToArray() : yaml.ValueSpan.ToArray()));

                    if (yaml.HasValueSequence)
                    {
                        Assert.True(yaml.ValueSpan == default);
                    }
                    else
                    {
                        Assert.True(yaml.ValueSequence.IsEmpty);
                    }
                    Assert.Equal(2, yaml.TokenStartIndex);
                }

                Assert.Equal(inputData, sequence.Slice(0, yaml.Position).ToArray());
            }
        }

        [Theory]
        [InlineData("\"abcdefg\"")]
        [InlineData("12345")]
        [InlineData("12345.0e-3")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("null")]
        public static void CheckOnlyOneOfValueSpanOrSequenceIsSetSingleValue(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state);

                Assert.False(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.True(yaml.ValueSequence.IsEmpty);

                Assert.True(yaml.Read());
                Assert.True(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.False(yaml.ValueSequence.IsEmpty);

                // Subsequent calls to Read clears the value properties since Read returned false.
                Assert.False(yaml.Read());
                Assert.False(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.True(yaml.ValueSequence.IsEmpty);
            }

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var yaml = new Utf8YamlReader(sequence, new YamlReaderOptions { CommentHandling = commentHandling });

                Assert.False(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.True(yaml.ValueSequence.IsEmpty);

                Assert.True(yaml.Read());
                Assert.True(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.False(yaml.ValueSequence.IsEmpty);

                // Subsequent calls to Read clears the value properties since Read returned false.
                Assert.False(yaml.Read());
                Assert.False(yaml.HasValueSequence);
                Assert.True(yaml.ValueSpan == default);
                Assert.True(yaml.ValueSequence.IsEmpty);
            }
        }

        [Theory]
        [InlineData("{ \"foo\" : \"bar\" //\u2028\n}", 1)]
        [InlineData("{ \"foo\" : \"bar\" //\u2028\n}", 2)]
        [InlineData("{ \"foo\" : \"bar\" //\u2028\n}", 3)]
        [InlineData("{ \"foo\" : \"bar\" //\u2028\n}", 100)]
        public static void YamlWithSingleLineCommentInTheMiddleOfThePayloadEndingWithNonStandardLineEndingMultiSegment(string yamlString, int segmentSize)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, segmentSize);

            foreach (YamlCommentHandling yamlCommentHandling in typeof(YamlCommentHandling).GetEnumValues())
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = yamlCommentHandling });

                foreach (bool isFinalBlock in new bool[] { false, true })
                {
                    var yaml = new Utf8YamlReader(sequence, isFinalBlock, state);

                    try
                    {
                        Assert.True(yaml.Read()); // {
                        Assert.True(yaml.Read()); // "foo"
                        Assert.True(yaml.Read()); // "bar"
                        yaml.Read(); // bad comment
                        Assert.Fail($"Expected YamlException was not thrown. CommentHandling = {yamlCommentHandling}");
                    }
                    catch (YamlException) { }
                }
            }
        }

        [Theory]
        [InlineData("//", 1)]
        [InlineData("//", 2)]
        [InlineData("//", 3)]
        [InlineData("//", 100)]
        [InlineData("//a", 1)]
        [InlineData("//a", 2)]
        [InlineData("//a", 3)]
        [InlineData("//a", 100)]
        [InlineData("//abc", 1)]
        [InlineData("//abc", 2)]
        [InlineData("//abc", 3)]
        [InlineData("//abc", 100)]
        public static void YamlWithSingleLineCommentWithNoLineEndingsNonFinalBlockMultiSegment(string yamlString, int segmentSize)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, segmentSize);
            var yaml = new Utf8YamlReader(sequence, isFinalBlock: false, state);

            Assert.False(yaml.Read());
            Assert.Equal(0, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("/*", 1)]
        [InlineData("/*", 2)]
        [InlineData("/*", 100)]
        [InlineData("/*  ", 1)]
        [InlineData("/*  ", 2)]
        [InlineData("/*  ", 3)]
        [InlineData("/*  ", 100)]
        [InlineData("/**", 1)]
        [InlineData("/**", 2)]
        [InlineData("/**", 3)]
        [InlineData("/**", 100)]
        [InlineData("/*  *", 1)]
        [InlineData("/*  *", 2)]
        [InlineData("/*  *", 3)]
        [InlineData("/*  *", 100)]
        public static void YamlWithUnfinishedMultiLineCommentNonFinalBlockMultiSegment(string yamlString, int segmentSize)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(dataUtf8, segmentSize);
            var yaml = new Utf8YamlReader(sequence, isFinalBlock: false, state);

            Assert.False(yaml.Read());
            Assert.Equal(0, yaml.BytesConsumed);
        }

        private static void TestReadTokenWithExtra(ReadOnlySequence<byte> sequence, YamlCommentHandling commentHandling, bool isFinalBlock, bool commentsAppended = false)
        {
            YamlReaderState state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
            Utf8YamlReader reader = new Utf8YamlReader(sequence, isFinalBlock, state);

            if (commentsAppended && commentHandling == YamlCommentHandling.Allow)
            {
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.Comment, reader.TokenType);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.Comment, reader.TokenType);
            }

            Assert.True(reader.Read());
            if (reader.TokenType == YamlTokenType.StartArray || reader.TokenType == YamlTokenType.StartObject)
            {
                Assert.True(reader.Read());
                Assert.Contains(reader.TokenType, new[] { YamlTokenType.EndArray, YamlTokenType.EndObject });
            }

            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader yamlReader) =>
            {
                yamlReader.Read();
                if (commentHandling == YamlCommentHandling.Allow && yamlReader.TokenType == YamlTokenType.Comment)
                {
                    yamlReader.Read();
                }
            });
        }

        private static void TrailingCommasHelper(ReadOnlySequence<byte> utf8, YamlReaderState state, bool allow, bool expectThrow)
        {
            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);

            Assert.Equal(allow, state.Options.AllowTrailingCommas);
            Assert.Equal(allow, reader.CurrentState.Options.AllowTrailingCommas);

            if (expectThrow)
            {
                YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader yamlReader) =>
                {
                    while (yamlReader.Read())
                        ;
                });
            }
            else
            {
                while (reader.Read())
                    ;
            }
        }

        private static void ValidateThrows(ref Utf8YamlReader reader)
        {
            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader yamlReader) =>
            {
                while (yamlReader.Read())
                    ;
            });
        }

        private static void ReadWithCommentsHelper(ref Utf8YamlReader reader, int expectedConsumed, bool validateThrows = false)
        {
            if (validateThrows)
            {
                try
                {
                    while (reader.Read())
                        ;
                    Assert.Fail("Expected YamlException was not thrown when reading JSON with trailing commas.");
                }
                catch (YamlException ex)
                {
                    Assert.Contains("trailing", ex.Message);
                }
            }
            else
            {
                while (reader.Read())
                    ;
                Assert.Equal(expectedConsumed, reader.BytesConsumed);
            }
        }

        [Theory]
        [MemberData(nameof(SingleYamlTokenStartIndex))]
        public static void TestTokenStartIndexMultiSegment_SingleValue(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);
            }

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var reader = new Utf8YamlReader(sequence, new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);

                reader = new Utf8YamlReader(sequence, new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(SingleYamlWithCommentsAllowTokenStartIndex))]
        public static void TestTokenStartIndexMultiSegment_SingleValueCommentsAllow(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, AllowTrailingCommas = false });
            var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.True(reader.Read());
            Assert.Equal(expectedIndex, reader.TokenStartIndex);

            state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, AllowTrailingCommas = true });
            reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.True(reader.Read());
            Assert.Equal(expectedIndex, reader.TokenStartIndex);
        }

        [Theory]
        [MemberData(nameof(ComplexArrayYamlTokenStartIndex))]
        public static void TestTokenStartIndexMultiSegment_ComplexArrayValue(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(ComplexObjectYamlTokenStartIndex))]
        public static void TestTokenStartIndexMultiSegment_ComplexObjectValue(string yamlString, int expectedIndexProperty, int expectedIndexValue)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(ComplexObjectSeveralYamlTokenStartIndex))]
        public static void TestTokenStartIndexMultiSegment_ComplexObjectManyValues(string yamlString, int expectedIndexProperty, int expectedIndexValue)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(YamlWithValidTrailingCommas))]
        public static void TestTokenStartIndexMultiSegment_WithTrailingCommas(string yamlString)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8, 1);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
                while (reader.Read())
                { }

                Assert.Equal(utf8.Length - 1, reader.TokenStartIndex);
            }
        }

        [Fact]
        public static void AllowMultipleValues_NonYamlTrailingData_ThrowsYamlException_MultiSegment()
        {
            YamlReaderOptions options = new() { AllowMultipleValues = true };
            Utf8YamlReader reader = new(YamlTestHelper.GetSequence("{ }      not JSON", 1), options);
            Assert.Equal(YamlTokenType.None, reader.TokenType);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader reader) => reader.Read());
        }

        [Theory]
        [InlineData("")]
        [InlineData("\t\r\n")]
        [InlineData("    \t\t                        ")]
        public static void AllowMultipleValues_NoYamlContent_ReturnsFalse_MultiSegment(string yaml)
        {
            YamlReaderOptions options = new() { AllowMultipleValues = true };
            Utf8YamlReader reader = new(YamlTestHelper.GetSequence(yaml, 1), options);

            Assert.True(reader.IsFinalBlock);
            Assert.False(reader.Read());
            Assert.Equal(YamlTokenType.None, reader.TokenType);
        }
    }
}
