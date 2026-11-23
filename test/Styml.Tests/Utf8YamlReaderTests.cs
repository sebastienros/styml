// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XUnitExtensions;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class Utf8YamlReaderTests
    {
        [Fact]
        public static void DefaultUtf8YamlReader()
        {
            Utf8YamlReader yaml = default;

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);
            Assert.False(yaml.ValueIsEscaped);

            Assert.Equal(0, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.False(yaml.Read());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.ValueTextEquals(""));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.ValueTextEquals("".AsSpan()));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.ValueTextEquals(default(ReadOnlySpan<char>)));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.ValueTextEquals(default(ReadOnlySpan<byte>)));

            TestGetMethodsOnDefault();
        }

        private static void TestGetMethodsOnDefault()
        {
            Utf8YamlReader yaml = default;

            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetByte(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetByte());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetComment());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetDateTime(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetDateTime());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetDateTimeOffset(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetDateTimeOffset());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetDecimal(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetDecimal());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetDouble(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetDouble());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetInt16(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetInt16());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetInt32(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetInt32());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetInt64(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetInt64());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetSByte(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetSByte());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetSingle(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetSingle());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetUInt16(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetUInt16());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetUInt32(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetUInt32());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.TryGetUInt64(out _));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetUInt64());

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetString());
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.CopyString(new byte[16]));
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.CopyString(new char[16]));

            yaml = default;
            YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetBoolean());
        }

        [Fact]
        public static void InitialState()
        {
            var yaml = new Utf8YamlReader("1"u8, isFinalBlock: true, state: default);

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.False(yaml.Read());
        }

        [Fact]
        public static void InitialStateSimpleCtor()
        {
            var yaml = new Utf8YamlReader("1"u8);

            Assert.Equal(0, yaml.BytesConsumed);
            Assert.Equal(0, yaml.TokenStartIndex);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(default, yaml.Position);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.Equal(64, yaml.CurrentState.Options.MaxDepth);
            Assert.False(yaml.CurrentState.Options.AllowTrailingCommas);
            Assert.False(yaml.CurrentState.Options.AllowMultipleValues);
            Assert.Equal(YamlCommentHandling.Disallow, yaml.CurrentState.Options.CommentHandling);

            Assert.True(yaml.Read());
            Assert.False(yaml.Read());
        }

        // TestCaseType is only used to give the json strings a descriptive name.
        [Theory]
        [MemberData(nameof(TestCases))]
        public static void TestYamlReaderUtf8(bool compactData, TestCaseType type, string yamlString)
        {
            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            SpanSequenceStatesAreEqual(dataUtf8);

            byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int length);
            string actualStr = Encoding.UTF8.GetString(result, 0, length);

            byte[] resultSequence = YamlTestHelper.SequenceReturnBytesHelper(dataUtf8, out length);
            string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            Assert.Equal(expectedStr, actualStr);
            Assert.Equal(expectedStr, actualStrSequence);

            // Yaml payload contains numbers that are too large for .NET (need BigInteger+)
            if (type != TestCaseType.FullSchema1 && type != TestCaseType.BasicLargeNum)
            {
                object yamlValues = YamlTestHelper.ReturnObjectHelper(dataUtf8);
                string str = YamlTestHelper.ObjectToString(yamlValues);
                ReadOnlySpan<char> expectedSpan = expectedStr.AsSpan(0, expectedStr.Length - 2);
                ReadOnlySpan<char> actualSpan = str.AsSpan(0, str.Length - 2);
                Assert.True(expectedSpan.SequenceEqual(actualSpan));
            }

            result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out length, YamlCommentHandling.Skip);
            actualStr = Encoding.UTF8.GetString(result, 0, length);
            resultSequence = YamlTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, YamlCommentHandling.Skip);
            actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            Assert.Equal(expectedStr, actualStr);
            Assert.Equal(expectedStr, actualStrSequence);

            result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out length, YamlCommentHandling.Allow);
            actualStr = Encoding.UTF8.GetString(result, 0, length);
            resultSequence = YamlTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, YamlCommentHandling.Allow);
            actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            Assert.Equal(expectedStr, actualStr);
            Assert.Equal(expectedStr, actualStrSequence);
        }

        [Theory]
        [MemberData(nameof(LargeTestCases))]
        public static void TestPartialLargeYamlReader(bool compactData, TestCaseType type, string yamlString)
        {
            // Skipping really large JSON since slicing them (O(n^2)) is too slow.
            if (type == TestCaseType.Yaml40KB || type == TestCaseType.Yaml400KB || type == TestCaseType.ProjectLockYaml)
            {
                return;
            }

            // Remove all formatting/indentation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
            var outputArray = new byte[outputLength];
            Span<byte> outputSpan = outputArray;

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                byte[] output = YamlTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref yaml);
                output.AsSpan(0, firstLength).CopyTo(outputSpan);
                int written = firstLength;

                long consumed = yaml.BytesConsumed;
                Assert.Equal(default, yaml.Position);

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                output = YamlTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref yaml);
                output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                written += length;
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
                Assert.Equal(default, yaml.Position);

                Assert.Equal(outputSpan.Length, written);
                string actualStr = Encoding.UTF8.GetString(outputArray);
                Assert.Equal(expectedStr, actualStr);
            }
        }

        [Theory]
        // Skipping large JSON since slicing them (O(n^2)) is too slow.
        [MemberData(nameof(SmallTestCases))]
        public static void TestPartialYamlReader(bool compactData, TestCaseType type, string yamlString)
        {
            _ = type;

            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
            var outputArray = new byte[outputLength];
            Span<byte> outputSpan = outputArray;

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                byte[] output = YamlTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref yaml);
                output.AsSpan(0, firstLength).CopyTo(outputSpan);
                int written = firstLength;

                long consumed = yaml.BytesConsumed;
                Assert.Equal(default, yaml.Position);

                for (long j = consumed; j < dataUtf8.Length - consumed; j++)
                {
                    // Need to re-initialize the state and reader to avoid using the previous state stack.
                    state = default;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                    while (yaml.Read())
                        ;

                    YamlReaderState yamlState = yaml.CurrentState;

                    written = firstLength;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, yamlState);
                    output = YamlTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref yaml);
                    output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                    written += length;

                    long consumedInner = yaml.BytesConsumed;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, yaml.CurrentState);
                    output = YamlTestHelper.ReaderLoop(outputSpan.Length - written, out length, ref yaml);
                    output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                    written += length;
                    Assert.Equal(dataUtf8.Length - consumedInner - consumed, yaml.BytesConsumed);
                    Assert.Equal(default, yaml.Position);

                    Assert.Equal(outputSpan.Length, written);
                    string actualStr = Encoding.UTF8.GetString(outputArray);
                    Assert.Equal(expectedStr, actualStr);
                }
            }
        }

        [Fact]
        public static void TestSingleStrings()
        {
            string yamlString = "\"Hello, \\u0041hson!\"";
            string expectedString = "Hello, \\u0041hson!, ";

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
            var outputArray = new byte[outputLength];
            Span<byte> outputSpan = outputArray;

            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                byte[] output = YamlTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref yaml);
                output.AsSpan(0, firstLength).CopyTo(outputSpan);
                int written = firstLength;

                long consumed = yaml.BytesConsumed;
                Assert.Equal(default, yaml.Position);
                Assert.Equal(0, yaml.TokenStartIndex);

                for (long j = consumed; j < dataUtf8.Length - consumed; j++)
                {
                    // Need to re-initialize the state and reader to avoid using the previous state stack.
                    state = default;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                    while (yaml.Read())
                        ;

                    YamlReaderState yamlState = yaml.CurrentState;

                    written = firstLength;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, yamlState);
                    output = YamlTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref yaml);
                    output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                    written += length;

                    long consumedInner = yaml.BytesConsumed;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, yaml.CurrentState);
                    output = YamlTestHelper.ReaderLoop(outputSpan.Length - written, out length, ref yaml);
                    output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                    written += length;
                    Assert.Equal(dataUtf8.Length - consumedInner - consumed, yaml.BytesConsumed);
                    Assert.Equal(default, yaml.Position);
                    Assert.Equal(0, yaml.TokenStartIndex);

                    Assert.Equal(outputSpan.Length, written);
                    string actualStr = Encoding.UTF8.GetString(outputArray);
                    Assert.Equal(expectedString, actualStr);
                }
            }
        }

        [Fact]
        public static void CurrentDepthArrayTest()
        {
            string yamlString =
"""
    [
        [
            1,
            2,
            3
        ]
    ]
    """;

            {
                byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);

                Assert.Equal(0, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.False(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
            }

            {
                byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
                var yaml = new Utf8YamlReader(dataUtf8);

                Assert.Equal(0, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.True(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.False(yaml.Read());
                Assert.Equal(0, yaml.CurrentDepth);
            }
        }

        [Fact]
        public static void CurrentDepthObjectTest()
        {
            string yamlString =
"""
    {
        "array": [
            1,
            2,
            3,
            {}
        ]
    }
    """;

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);

            Assert.Equal(0, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(1, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(1, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(2, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(2, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(2, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(2, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(2, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(1, yaml.CurrentDepth);
            Assert.True(yaml.Read());
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.False(yaml.Read());
            Assert.Equal(0, yaml.CurrentDepth);
        }

        [Theory]
        [MemberData(nameof(TrySkipValues))]
        public static void TestTrySkip(string yamlString, YamlTokenType lastToken)
        {
            List<YamlTokenType> expectedTokenTypes = YamlTestHelper.GetTokenTypes(yamlString);
            TrySkipHelper(yamlString, lastToken, expectedTokenTypes, YamlCommentHandling.Disallow);
            TrySkipHelper(yamlString, lastToken, expectedTokenTypes, YamlCommentHandling.Skip);
            TrySkipHelper(yamlString, lastToken, expectedTokenTypes, YamlCommentHandling.Allow);
        }

        private static void TrySkipHelper(string yamlString, YamlTokenType lastToken, List<YamlTokenType> expectedTokenTypes, YamlCommentHandling commentHandling)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
            var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling });
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

            YamlReaderState previous = yaml.CurrentState;
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(0, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            Assert.True(yaml.TrySkip());

            YamlReaderState current = yaml.CurrentState;
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(previous, current);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(0, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            yaml.Skip();

            current = yaml.CurrentState;
            Assert.Equal(YamlTokenType.None, yaml.TokenType);
            Assert.Equal(previous, current);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(0, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.True(yaml.IsFinalBlock);
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
            Assert.True(yaml.IsFinalBlock);
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
            Assert.True(yaml.IsFinalBlock);
            Assert.True(yaml.ValueSpan.SequenceEqual(default));
            Assert.True(yaml.ValueSequence.IsEmpty);

            yaml.Skip();

            current = yaml.CurrentState;
            Assert.Equal(previous, current);
            Assert.Equal(lastToken, yaml.TokenType);
            Assert.Equal(0, yaml.CurrentDepth);
            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.IsFinalBlock);
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
                Assert.True(expectedTokenTypes[i] == yaml.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {yaml.TokenType}, Index: {i}, BytesConsumed: {yaml.BytesConsumed}");
            }

            for (int i = 0; i < totalReads; i++)
            {
                state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling });
                yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
                for (int j = 0; j < i; j++)
                {
                    Assert.True(yaml.Read());
                }
                yaml.Skip();
                Assert.True(expectedTokenTypes[i] == yaml.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {yaml.TokenType}, Index: {i}, BytesConsumed: {yaml.BytesConsumed}");
            }
        }

        [Theory]
        [MemberData(nameof(TrySkipValues))]
        public static void TestTrySkipPartial(string yamlString, YamlTokenType lastToken)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            // Skip, then skip some more
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);

                long bytesConsumed = yaml.BytesConsumed;
                while (yaml.TrySkip())
                {
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }

                ValidateNextTrySkip(ref yaml);

                long consumed = yaml.BytesConsumed;

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                while (yaml.TrySkip())
                {
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
            }

            // Read, then skip
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (yaml.Read())
                    ;

                ValidateNextTrySkip(ref yaml);

                long consumed = yaml.BytesConsumed;

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                long bytesConsumed = yaml.BytesConsumed;
                while (yaml.TrySkip())
                {
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
            }

            // Skip, then read
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                long bytesConsumed = yaml.BytesConsumed;
                while (yaml.TrySkip())
                {
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }

                ValidateNextTrySkip(ref yaml);

                long consumed = yaml.BytesConsumed;

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                while (yaml.Read())
                    ;
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
                Assert.Equal(lastToken, yaml.TokenType);
            }
        }

        private static void ValidateNextTrySkip(ref Utf8YamlReader yaml)
        {
            YamlReaderState previous = yaml.CurrentState;
            YamlTokenType prevTokenType = yaml.TokenType;
            int prevDepth = yaml.CurrentDepth;
            long prevConsumed = yaml.BytesConsumed;
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSequence.IsEmpty);
            ReadOnlySpan<byte> prevValue = yaml.ValueSpan;

            if (yaml.TokenType == YamlTokenType.PropertyName || yaml.TokenType == YamlTokenType.StartObject || yaml.TokenType == YamlTokenType.StartArray)
            {
                Assert.False(yaml.TrySkip());
            }
            else
            {
                Assert.True(yaml.TrySkip());
            }

            YamlReaderState current = yaml.CurrentState;
            Assert.Equal(previous, current);
            Assert.Equal(prevTokenType, yaml.TokenType);
            Assert.Equal(prevDepth, yaml.CurrentDepth);
            Assert.Equal(prevConsumed, yaml.BytesConsumed);
            Assert.False(yaml.HasValueSequence);
            Assert.False(yaml.ValueIsEscaped);
            Assert.True(yaml.ValueSequence.IsEmpty);
            Assert.True(yaml.ValueSpan.SequenceEqual(prevValue));
        }

        [Theory]
        [MemberData(nameof(TrySkipValues))]
        public static void TestSkipPartial(string yamlString, YamlTokenType lastToken)
        {
            _ = lastToken;
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: false, default);
            try
            {
                Assert.False(yaml.IsFinalBlock);
                yaml.Skip();
                Assert.Fail("Expected InvalidOperationException was not thrown when calling Skip with isFinalBlock = false, even if whole payload is available.");
            }
            catch (InvalidOperationException) { }

            // Skip, then skip some more
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);

                try
                {
                    Assert.False(yaml.IsFinalBlock);
                    yaml.Skip();
                    Assert.Fail("Expected InvalidOperationException was not thrown when calling Skip with isFinalBlock = false");
                }
                catch (InvalidOperationException) { }

                long bytesConsumed = yaml.BytesConsumed;
                while (yaml.TrySkip())
                {
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }

                long consumed = yaml.BytesConsumed;

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                while (true)
                {
                    Assert.True(yaml.IsFinalBlock);
                    yaml.Skip();
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
            }

            // Read, then skip
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                YamlReaderState state = default;
                yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (yaml.Read())
                    ;

                long consumed = yaml.BytesConsumed;

                yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, yaml.CurrentState);
                long bytesConsumed = yaml.BytesConsumed;
                while (true)
                {
                    Assert.True(yaml.IsFinalBlock);
                    yaml.Skip();
                    Assert.True(yaml.TokenType != YamlTokenType.PropertyName && yaml.TokenType != YamlTokenType.StartObject && yaml.TokenType != YamlTokenType.StartArray);
                    if (bytesConsumed == yaml.BytesConsumed)
                    {
                        if (!yaml.Read())
                        {
                            break;
                        }
                    }
                    else
                    {
                        bytesConsumed = yaml.BytesConsumed;
                    }
                }
                Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
            }
        }

        [Fact]
        public static void SkipTest()
        {
            string yamlString = """{"propertyName": {"foo": "bar"},"nestedArray": {"numbers": [1,2,3]}}""";

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                yaml.Skip();
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.None, yaml.TokenType);
                Assert.Equal(0, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                // start object
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartObject, yaml.TokenType);
                Assert.Equal(0, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(0, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndObject, yaml.TokenType);
                Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                // start object, property
                Assert.True(yaml.Read());
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.PropertyName, yaml.TokenType);
                Assert.Equal(1, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndObject, yaml.TokenType);
                Assert.Equal(31, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                // start object, property, start object
                Assert.True(yaml.Read());
                Assert.True(yaml.Read());
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartObject, yaml.TokenType);
                Assert.Equal(1, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndObject, yaml.TokenType);
                Assert.Equal(31, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 4; i++)
                {
                    // start object, property, start object, property
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.PropertyName, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.String, yaml.TokenType);
                Assert.Equal(30, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 5; i++)
                {
                    // start object, property, start object, property, string value
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.String, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.String, yaml.TokenType);
                Assert.Equal(30, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 6; i++)
                {
                    // start object, property, start object, property, string value, end object
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.EndObject, yaml.TokenType);
                Assert.Equal(1, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(1, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndObject, yaml.TokenType);
                Assert.Equal(31, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 9; i++)
                {
                    // start object, property, start object, property, string value, property, start object, property
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.PropertyName, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);
                Assert.Equal(66, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 10; i++)
                {
                    // start object, property, start object, property, string value, property, start object, property, start array
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);
                Assert.Equal(66, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 11; i++)
                {
                    // start object, property, start object, property, string value, property, start object, property, start array, number value
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.Number, yaml.TokenType);
                Assert.Equal(3, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(3, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.Number, yaml.TokenType);
                Assert.Equal(61, yaml.BytesConsumed);
            }
        }

        [Fact]
        public static void SkipTestEmpty()
        {
            string yamlString = """{"nestedArray": {"empty": [],"empty": [{}]}}""";

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 4; i++)
                {
                    // start object, property, start object, property
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.PropertyName, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);
                Assert.Equal(28, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 5; i++)
                {
                    // start object, property, start object, property, start array
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);
                Assert.Equal(28, yaml.BytesConsumed);
            }

            {
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
                for (int i = 0; i < 7; i++)
                {
                    // start object, property, start object, property, start array, end array, property name
                    Assert.True(yaml.Read());
                }
                Assert.Equal(YamlTokenType.PropertyName, yaml.TokenType);
                Assert.Equal(2, yaml.CurrentDepth);
                yaml.Skip();
                Assert.Equal(2, yaml.CurrentDepth);
                Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);
                Assert.Equal(42, yaml.BytesConsumed);
            }
        }

        [Theory]
        // Pad depth by nested objects, but minimize the text
        [InlineData(1, true, true)]
        [InlineData(2, true, true)]
        [InlineData(3, true, true)]
        [InlineData(16, true, true)]
        [InlineData(32, true, true)]
        [InlineData(60, true, true)]
        [InlineData(61, true, true)]
        [InlineData(62, true, true)]
        [InlineData(63, true, true)]
        [InlineData(64, true, true)]
        [InlineData(65, true, true)]
        [InlineData(66, true, true)]
        [InlineData(67, true, true)]
        [InlineData(68, true, true)]
        [InlineData(123, true, true)]
        [InlineData(124, true, true)]
        [InlineData(125, true, true)]

        // Pad depth by nested arrays, but minimize the text
        [InlineData(1, false, true)]
        [InlineData(2, false, true)]
        [InlineData(3, false, true)]
        [InlineData(16, false, true)]
        [InlineData(32, false, true)]
        [InlineData(60, false, true)]
        [InlineData(61, false, true)]
        [InlineData(62, false, true)]
        [InlineData(63, false, true)]
        [InlineData(64, false, true)]
        [InlineData(65, false, true)]
        [InlineData(66, false, true)]
        [InlineData(67, false, true)]
        [InlineData(68, false, true)]
        [InlineData(123, false, true)]
        [InlineData(124, false, true)]
        [InlineData(125, false, true)]

        // Pad depth by nested arrays, but keep the text formatted
        [InlineData(1, false, false)]
        [InlineData(2, false, false)]
        [InlineData(3, false, false)]
        [InlineData(16, false, false)]
        [InlineData(32, false, false)]
        [InlineData(60, false, false)]
        [InlineData(61, false, false)]
        [InlineData(62, false, false)]
        [InlineData(63, false, false)]
        [InlineData(64, false, false)]
        [InlineData(65, false, false)]
        [InlineData(66, false, false)]
        [InlineData(67, false, false)]
        [InlineData(68, false, false)]
        [InlineData(123, false, false)]
        [InlineData(124, false, false)]
        [InlineData(125, false, false)]

        // Pad depth by nested objects, but keep the text formatted
        [InlineData(1, true, false)]
        [InlineData(2, true, false)]
        [InlineData(3, true, false)]
        [InlineData(16, true, false)]
        [InlineData(32, true, false)]
        [InlineData(60, true, false)]
        [InlineData(61, true, false)]
        [InlineData(62, true, false)]
        [InlineData(63, true, false)]
        [InlineData(64, true, false)]
        [InlineData(65, true, false)]
        [InlineData(66, true, false)]
        [InlineData(67, true, false)]
        [InlineData(68, true, false)]
        [InlineData(123, true, false)]
        [InlineData(124, true, false)]
        [InlineData(125, true, false)]
        public static void TestPartialYamlReaderWithDepthPadding(int depthPadding, bool padByObject, bool compactData)
        {
            var builderPrefix = new StringBuilder();
            var builderSuffix = new StringBuilder();

            if (padByObject)
            {
                for (int i = 0; i < depthPadding; i++)
                {
                    builderPrefix.Append($"{{\n \"property{i}\": ");
                    builderSuffix.Append("\n}");
                }
            }
            else
            {
                for (int i = 0; i < depthPadding; i++)
                {
                    builderPrefix.Append("[\n");
                    builderSuffix.Append("\n]");
                }
            }

            string yamlString = builderPrefix.ToString() + SR.FullYamlSchema1 + builderSuffix.ToString();

            // Remove all formatting/indendation
            if (compactData)
            {
                yamlString = YamlTestHelper.GetCompactString(yamlString);
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            // Set the max depth sufficiently large to account for the depth padding.
            byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength, maxDepth: 256);
            Span<byte> outputSpan = new byte[outputLength];
            string actualStr = Encoding.UTF8.GetString(result, 0, outputLength);

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string expectedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);

            Assert.Equal(expectedStr, actualStr);
        }

        [Theory]
        [MemberData(nameof(SpecialNumTestCases))]
        public static void TestPartialYamlReaderSpecialNumbers(TestCaseType type, string yamlString)
        {
            _ = type;
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < dataUtf8.Length; i++)
                {
                    var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                    var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                    while (yaml.Read())
                        ;
                }
            }
        }

        [Theory]
        [OuterLoop]
        [MemberData(nameof(SpecialNumTestCases))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        public static void TestPartialYamlReaderSlicesSpecialNumbers(TestCaseType type, string yamlString)
        {
            _ = type;
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < dataUtf8.Length; i++)
                {
                    var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                    var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                    while (yaml.Read())
                        ;

                    long consumed = yaml.BytesConsumed;

                    for (long j = consumed; j < dataUtf8.Length - consumed; j++)
                    {
                        // Need to re-initialize the state and reader to avoid using the previous state stack.
                        state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                        yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                        while (yaml.Read())
                            ;

                        YamlReaderState yamlState = yaml.CurrentState;

                        yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, yamlState);
                        while (yaml.Read())
                            ;

                        long consumedInner = yaml.BytesConsumed;
                        yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, yaml.CurrentState);
                        while (yaml.Read())
                            ;
                        Assert.Equal(dataUtf8.Length - consumedInner - consumed, yaml.BytesConsumed);
                    }
                }
            }
        }

        [ConditionalTheory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(62)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(66)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        public static void TestDepth(int depth)
        {
            if (PlatformDetection.IsInterpreter && depth >= 256)
            {
                throw new SkipTestException("Takes very long to run on interpreter.");
            }

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < depth; i++)
                {
                    string yamlStr = YamlTestHelper.WriteDepthObject(i, commentHandling == YamlCommentHandling.Allow);
                    Span<byte> data = Encoding.UTF8.GetBytes(yamlStr);

                    var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = depth });
                    var yaml = new Utf8YamlReader(data, isFinalBlock: true, state);

                    int actualDepth = 0;
                    while (yaml.Read())
                    {
                        if (yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null)
                        {
                            actualDepth = yaml.CurrentDepth;
                        }
                    }

                    int expectedDepth = 0;
                    var newtonYaml = new JsonTextReader(new StringReader(yamlStr))
                    {
                        MaxDepth = depth
                    };
                    while (newtonYaml.Read())
                    {
                        if (newtonYaml.TokenType == JsonToken.String)
                        {
                            expectedDepth = newtonYaml.Depth;
                        }
                    }

                    Assert.Equal(expectedDepth, actualDepth);
                    Assert.Equal(i + 1, actualDepth);
                }
            }

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < depth; i++)
                {
                    string yamlStr = YamlTestHelper.WriteDepthObject(i, commentHandling == YamlCommentHandling.Allow);
                    Span<byte> data = Encoding.UTF8.GetBytes(yamlStr);

                    var yaml = new Utf8YamlReader(data, new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = depth });

                    int actualDepth = 0;
                    while (yaml.Read())
                    {
                        if (yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null)
                        {
                            actualDepth = yaml.CurrentDepth;
                        }
                    }

                    int expectedDepth = 0;
                    var newtonYaml = new JsonTextReader(new StringReader(yamlStr))
                    {
                        MaxDepth = depth
                    };
                    while (newtonYaml.Read())
                    {
                        if (newtonYaml.TokenType == JsonToken.String)
                        {
                            expectedDepth = newtonYaml.Depth;
                        }
                    }

                    Assert.Equal(expectedDepth, actualDepth);
                    Assert.Equal(i + 1, actualDepth);
                }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(62)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(66)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        public static void TestDepthWithObjectArrayMismatch(int depth)
        {
            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < depth; i++)
                {
                    string yamlStr = YamlTestHelper.WriteDepthObjectWithArray(i, commentHandling == YamlCommentHandling.Allow);
                    Span<byte> data = Encoding.UTF8.GetBytes(yamlStr);

                    var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = depth + 1 });
                    var yaml = new Utf8YamlReader(data, isFinalBlock: true, state);

                    int actualDepth = 0;
                    while (yaml.Read())
                    {
                        if (yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null)
                            actualDepth = yaml.CurrentDepth;
                    }

                    int expectedDepth = 0;
                    var newtonYaml = new JsonTextReader(new StringReader(yamlStr))
                    {
                        MaxDepth = depth + 1
                    };
                    while (newtonYaml.Read())
                    {
                        if (newtonYaml.TokenType == JsonToken.String)
                        {
                            expectedDepth = newtonYaml.Depth;
                        }
                    }

                    Assert.Equal(expectedDepth, actualDepth);
                    Assert.Equal(i + 2, actualDepth);
                }
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(62)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(66)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        public static void TestDepthBeyondLimit(int depth)
        {
            string yamlStr = YamlTestHelper.WriteDepthObject(depth - 1);
            Span<byte> data = Encoding.UTF8.GetBytes(yamlStr);

            var state = new YamlReaderState(new YamlReaderOptions { MaxDepth = depth - 1 });
            var yaml = new Utf8YamlReader(data, isFinalBlock: true, state);

            try
            {
                int maxDepth = 0;
                while (yaml.Read())
                {
                    if (maxDepth < yaml.CurrentDepth)
                        maxDepth = yaml.CurrentDepth;
                }
                Assert.Fail($"Expected YamlException was not thrown. Max depth allowed = {yaml.CurrentState.Options.MaxDepth} | Max depth reached = {maxDepth}");
            }
            catch (YamlException)
            { }

            yamlStr = YamlTestHelper.WriteDepthArray(depth - 1);
            data = Encoding.UTF8.GetBytes(yamlStr);

            state = new YamlReaderState(new YamlReaderOptions { MaxDepth = depth - 1 });
            yaml = new Utf8YamlReader(data, isFinalBlock: true, state);

            try
            {
                int maxDepth = 0;
                while (yaml.Read())
                {
                    if (maxDepth < yaml.CurrentDepth)
                        maxDepth = yaml.CurrentDepth;
                }
                Assert.Fail($"Expected YamlException was not thrown. Max depth allowed = {yaml.CurrentState.Options.MaxDepth} | Max depth reached = {maxDepth}");
            }
            catch (YamlException)
            { }
        }

        [Theory]
        [InlineData("{\"nam\\\"e\":\"ah\\\"son\"}", "nam\\\"e, ah\\\"son, ", "nam\"e, ah\"son, ")]
        [InlineData("{\"Here is a string: \\\"\\\"\":\"Here is a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str\":\"\\\"\\\"\"}",
            "Here is a string: \\\"\\\", Here is a, Here is a back slash\\\\, Multiline\\r\\n String\\r\\n, \\tMul\\r\\ntiline String, \\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\, str, \\\"\\\", ",
            "Here is a string: \"\", Here is a, Here is a back slash\\, Multiline\r\n String\r\n, \tMul\r\ntiline String, \"somequote\"\tMu\"\"l\r\ntiline\"another\" String\\, str, \"\", ")]
        public static void TestYamlReaderUtf8SpecialString(string yamlString, string expectedStr, string expectedEscapedStr)
        {
            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);
                byte[] result = YamlTestHelper.ReturnBytesHelper(dataUtf8, out int length, commentHandling);
                string actualStr = Encoding.UTF8.GetString(result, 0, length);

                Assert.Equal(expectedStr, actualStr);

                result = YamlTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, commentHandling);
                actualStr = Encoding.UTF8.GetString(result, 0, length);

                Assert.Equal(expectedStr, actualStr);

                object yamlValues = YamlTestHelper.ReturnObjectHelper(dataUtf8, commentHandling);
                string str = YamlTestHelper.ObjectToString(yamlValues);
                Assert.Equal(expectedEscapedStr, str);

                Stream stream = new MemoryStream(dataUtf8);
                TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                expectedEscapedStr = YamlTestHelper.NewtonsoftReturnStringHelper(reader);
                Assert.Equal(expectedEscapedStr, str);
            }
        }

        [Theory]
        [MemberData(nameof(SingleValueYaml))]
        public static void SingleYamlValue(string yamlString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                for (int i = 0; i < dataUtf8.Length; i++)
                {
                    var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                    var yaml = new Utf8YamlReader(dataUtf8.AsSpan(0, i), false, state);
                    while (yaml.Read())
                    {
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                        Assert.True(yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null);
                        Assert.Equal(expectedString, Encoding.UTF8.GetString(yaml.ValueSpan.ToArray()));
                        Assert.Equal(2, yaml.TokenStartIndex);
                    }

                    long consumed = yaml.BytesConsumed;
                    yaml = new Utf8YamlReader(dataUtf8.AsSpan((int)consumed), true, yaml.CurrentState);
                    while (yaml.Read())
                    {
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                        Assert.True(yaml.TokenType >= YamlTokenType.String && yaml.TokenType <= YamlTokenType.Null);
                        Assert.Equal(expectedString, Encoding.UTF8.GetString(yaml.ValueSpan.ToArray()));
                        if (consumed <= 2)
                        {
                            Assert.Equal(2 - consumed, yaml.TokenStartIndex);
                        }
                    }
                    Assert.Equal(dataUtf8.Length - consumed, yaml.BytesConsumed);
                }
            }
        }

        [Fact]
        public static void ReadLongWhitespaceAndDigitRuns()
        {
            // Long whitespace runs exercise the vectorized whitespace-skipping path in
            // Utf8YamlReader, alongside long integer/fraction digit runs, to lock in correct
            // tokenization and number parsing for large runs.
            string yamlString =
                "[" + new string(' ', 60) + "1234567890123456789," +
                "\n\n" + new string(' ', 40) + "-1.25e3," +
                new string('\t', 33) + "42," +
                new string(' ', 48) + "12345678901234567890123456789012345678" +
                "\n]";
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(1234567890123456789L, yaml.GetInt64());

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(-1250.0, yaml.GetDouble());

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(42, yaml.GetInt32());

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.Number, yaml.TokenType);
            Assert.Equal(38, yaml.ValueSpan.Length);

            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.EndArray, yaml.TokenType);

            Assert.False(yaml.Read());
            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        public static IEnumerable<object[]> WhitespaceBeforeInvalidTokenData()
        {
            yield return new object[] { "\n\n\n   ", 3, 3 };
            yield return new object[] { "\r\n\t\t", 1, 2 };
            yield return new object[] { new string(' ', 100), 0, 101 };
            yield return new object[] { new string('\n', 50) + new string(' ', 30), 50, 30 };
            yield return new object[] { " \t \t \r\n", 1, 0 };
        }

        [Theory]
        [MemberData(nameof(WhitespaceBeforeInvalidTokenData))]
        public static void WhitespaceRunBeforeInvalidToken_ReportsLineAndBytePosition(string whitespace, int expectedLineNumber, int expectedBytePosition)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes("[" + whitespace + "@]");

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.StartArray, yaml.TokenType);

                try
                {
                    yaml.Read();
                    Assert.Fail("Expected YamlException was not thrown.");
                }
                catch (YamlException ex)
                {
                    Assert.Equal(expectedLineNumber, ex.LineNumber);
                    Assert.Equal(expectedBytePosition, ex.BytePositionInLine);
                }
            }
        }

        private static void VerifyReadLoop(ref Utf8YamlReader yaml, string expected)
        {
            while (yaml.Read())
            {
                switch (yaml.TokenType)
                {
                    case YamlTokenType.StartObject:
                    case YamlTokenType.EndObject:
                        break;
                    case YamlTokenType.Comment:
                        if (expected != null)
                        {
                            string actualComment = yaml.GetComment();
                            Assert.Equal(expected, actualComment);
                        }
                        else
                        {
                            Assert.Fail();
                        }
                        break;
                    default:
                        Assert.Fail();
                        break;
                }
            }
        }

        private static void TestReadTokenWithExtra(byte[] utf8, YamlCommentHandling commentHandling, bool isFinalBlock, bool commentsAppended = false)
        {
            YamlReaderState state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
            Utf8YamlReader reader = new Utf8YamlReader(utf8, isFinalBlock, state);

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

        private static void TrailingCommasHelper(byte[] utf8, YamlReaderState state, bool allow, bool expectThrow)
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

        private static void TrailingCommasHelperPartial(byte[] utf8, YamlReaderState state, bool expectThrow)
        {
            if (expectThrow)
            {
                Assert.ThrowsAny<YamlException>(() => PartialReaderLoop(utf8, state));
            }
            else
            {
                PartialReaderLoop(utf8, state);
            }
        }

        private static void PartialReaderLoop(byte[] utf8, YamlReaderState state)
        {
            for (int i = 0; i < utf8.Length; i++)
            {
                YamlReaderState stateCopy = state;
                PartialReaderLoop(utf8, stateCopy, i);
            }
        }

        private static void PartialReaderLoop(byte[] utf8, YamlReaderState state, int splitLocation)
        {
            var reader = new Utf8YamlReader(utf8.AsSpan(0, splitLocation), isFinalBlock: false, state);
            while (reader.Read())
                ;

            long consumed = reader.BytesConsumed;
            reader = new Utf8YamlReader(utf8.AsSpan((int)consumed), isFinalBlock: true, reader.CurrentState);
            while (reader.Read())
                ;
        }

        [Theory]
        [MemberData(nameof(SingleYamlTokenStartIndex))]
        public static void TestTokenStartIndex_SingleValue(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(SingleYamlWithCommentsAllowTokenStartIndex))]
        public static void TestTokenStartIndex_SingleValueCommentsAllow(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, AllowTrailingCommas = false });
            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
            Assert.True(reader.Read());
            Assert.Equal(expectedIndex, reader.TokenStartIndex);

            state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow, AllowTrailingCommas = true });
            reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
            Assert.True(reader.Read());
            Assert.Equal(expectedIndex, reader.TokenStartIndex);
        }

        [Theory]
        [MemberData(nameof(ComplexArrayYamlTokenStartIndex))]
        public static void TestTokenStartIndex_ComplexArrayValue(string yamlString, int expectedIndex)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndex, reader.TokenStartIndex);
            }
        }

        [Theory]
        [MemberData(nameof(ComplexObjectYamlTokenStartIndex))]
        public static void TestTokenStartIndex_ComplexObjectValue(string yamlString, int expectedIndexProperty, int expectedIndexValue)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
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
        public static void TestTokenStartIndex_ComplexObjectManyValues(string yamlString, int expectedIndexProperty, int expectedIndexValue)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexProperty, reader.TokenStartIndex);
                Assert.True(reader.Read());
                Assert.Equal(expectedIndexValue, reader.TokenStartIndex);

                state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
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
        public static void TestTokenStartIndex_WithTrailingCommas(string yamlString)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                while (reader.Read())
                { }

                Assert.Equal(utf8.Length - 1, reader.TokenStartIndex);
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("42")]
        [InlineData("\"stringWithoutEscaping\"")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("/* comment */ null", YamlCommentHandling.Allow)]
        public static void ValueIsEscaped_IsFalseOnTokensWithoutEscaping(string yaml, YamlCommentHandling commentHandling = default)
        {
            var options = new YamlReaderOptions { CommentHandling = commentHandling };
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test, options);

            static void Test(ref Utf8YamlReader reader)
            {
                while (reader.Read())
                {
                    Assert.False(reader.ValueIsEscaped);
                }
            }
        }

        [Theory]
        [InlineData("""
            \"
            """)]
        [InlineData(@"\n")]
        [InlineData(@"\r")]
        [InlineData(@"\\")]
        [InlineData(@"\/")]
        [InlineData(@"\t")]
        [InlineData(@"\b")]
        [InlineData(@"\f")]
        [InlineData(@"\u6F22\u5B57")]
        public static void ValueIsEscaped_IsTrueOnEscapedStrings(string yamlString)
        {
            string yaml = $@"""{yamlString}""";
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            static void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.String, reader.TokenType);
                Assert.True(reader.ValueIsEscaped);
                Assert.Contains((byte)'\\', reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray());
            }
        }

        [Theory]
        [InlineData("""
            \"
            """)]
        [InlineData(@"\n")]
        [InlineData(@"\r")]
        [InlineData(@"\\")]
        [InlineData(@"\/")]
        [InlineData(@"\t")]
        [InlineData(@"\b")]
        [InlineData(@"\f")]
        [InlineData(@"\u6F22\u5B57")]
        public static void ValueIsEscaped_IsTrueOnEscapedPropertyNames(string yamlString)
        {
            string yaml = $@"{{ ""{yamlString}"" : false }}";
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            static void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
                Assert.True(reader.ValueIsEscaped);
                Assert.Contains((byte)'\\', reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray());
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("42")]
        [InlineData("\"stringWithoutEscaping\"")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("/* comment */ null", YamlCommentHandling.Allow)]
        public static void ValueIsEscaped_AfterEscapedStrings_IsFalseOnNonEscapedTokens(string yamlValue, YamlCommentHandling commentHandling = default)
        {
            string yaml = $@"[""hello\n"", {yamlValue}]";
            var options = new YamlReaderOptions { CommentHandling = commentHandling };
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test, options);

            static void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.String, reader.TokenType);
                Assert.True(reader.ValueIsEscaped);

                while (reader.Read())
                {
                    Assert.False(reader.ValueIsEscaped);
                }
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("42")]
        [InlineData("\"stringWithoutEscaping\"")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("/* comment */ null", YamlCommentHandling.Allow)]
        public static void ValueIsEscaped_AfterEscapedPropertyNames_IsFalseOnNonEscapedTokens(string yamlValue, YamlCommentHandling commentHandling = default)
        {
            string yaml = $@"{{ ""hello\n"" : {yamlValue} }}";
            var options = new YamlReaderOptions { CommentHandling = commentHandling };
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test, options);

            static void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Assert.True(reader.Read());
                Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
                Assert.True(reader.ValueIsEscaped);

                while (reader.Read())
                {
                    Assert.False(reader.ValueIsEscaped);
                }
            }
        }

        [Fact]
        public static void AllowMultipleValues_NonYamlTrailingData_ThrowsYamlException()
        {
            YamlReaderOptions options = new() { AllowMultipleValues = true };
            Utf8YamlReader reader = new("{ }      not JSON"u8, options);
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
        public static void AllowMultipleValues_NoYamlContent_ReturnsFalse(string yaml)
        {
            YamlReaderOptions options = new() { AllowMultipleValues = true };
            Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), options);

            Assert.True(reader.IsFinalBlock);
            Assert.False(reader.Read());
            Assert.Equal(YamlTokenType.None, reader.TokenType);
        }

        public static IEnumerable<object[]> TestCases
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { true, TestCaseType.Basic, SR.BasicYaml},
                    new object[] { true, TestCaseType.BasicLargeNum, SR.BasicYamlWithLargeNum}, // Yaml.NET treats numbers starting with 0 as octal (0425 becomes 277)
                    new object[] { true, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Yaml.NET and System.Text.Yaml
                    new object[] { true, TestCaseType.DeepTree, SR.DeepTree},
                    new object[] { true, TestCaseType.FullSchema1, SR.FullYamlSchema1},
                    new object[] { true, TestCaseType.HelloWorld, SR.HelloWorld},
                    new object[] { true, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                    new object[] { true, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                    new object[] { true, TestCaseType.ProjectLockYaml, SR.ProjectLockYaml},
                    new object[] { true, TestCaseType.Yaml400B, SR.Yaml400B},
                    new object[] { true, TestCaseType.Yaml4KB, SR.Yaml4KB},
                    new object[] { true, TestCaseType.Yaml40KB, SR.Yaml40KB},
                    new object[] { true, TestCaseType.Yaml400KB, SR.Yaml400KB},

                    new object[] { false, TestCaseType.Basic, SR.BasicYaml},
                    new object[] { false, TestCaseType.BasicLargeNum, SR.BasicYamlWithLargeNum}, // Yaml.NET treats numbers starting with 0 as octal (0425 becomes 277)
                    new object[] { false, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Yaml.NET and System.Text.Yaml
                    new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
                    new object[] { false, TestCaseType.FullSchema1, SR.FullYamlSchema1},
                    new object[] { false, TestCaseType.HelloWorld, SR.HelloWorld},
                    new object[] { false, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                    new object[] { false, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                    new object[] { false, TestCaseType.ProjectLockYaml, SR.ProjectLockYaml},
                    new object[] { false, TestCaseType.Yaml400B, SR.Yaml400B},
                    new object[] { false, TestCaseType.Yaml4KB, SR.Yaml4KB},
                    new object[] { false, TestCaseType.Yaml40KB, SR.Yaml40KB},
                    new object[] { false, TestCaseType.Yaml400KB, SR.Yaml400KB}
                };
            }
        }

        public static IEnumerable<object[]> SmallTestCases
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { true, TestCaseType.Basic, SR.BasicYaml},
                    new object[] { true, TestCaseType.BasicLargeNum, SR.BasicYamlWithLargeNum}, // Yaml.NET treats numbers starting with 0 as octal (0425 becomes 277)
                    new object[] { true, TestCaseType.FullSchema1, SR.FullYamlSchema1},
                    new object[] { true, TestCaseType.HelloWorld, SR.HelloWorld},
                    new object[] { true, TestCaseType.Yaml400B, SR.Yaml400B},

                    new object[] { false, TestCaseType.Basic, SR.BasicYaml},
                    new object[] { false, TestCaseType.BasicLargeNum, SR.BasicYamlWithLargeNum}, // Yaml.NET treats numbers starting with 0 as octal (0425 becomes 277)
                    new object[] { false, TestCaseType.FullSchema1, SR.FullYamlSchema1},
                    new object[] { false, TestCaseType.HelloWorld, SR.HelloWorld},
                    new object[] { false, TestCaseType.Yaml400B, SR.Yaml400B},
                };
            }
        }

        public static IEnumerable<object[]> LargeTestCases
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { true, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Yaml.NET and System.Text.Yaml
                    new object[] { true, TestCaseType.DeepTree, SR.DeepTree},
                    new object[] { true, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                    new object[] { true, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                    new object[] { true, TestCaseType.ProjectLockYaml, SR.ProjectLockYaml},
                    new object[] { true, TestCaseType.Yaml400B, SR.Yaml400B},
                    new object[] { true, TestCaseType.Yaml40KB, SR.Yaml40KB},
                    new object[] { true, TestCaseType.Yaml400KB, SR.Yaml400KB},

                    new object[] { false, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Yaml.NET and System.Text.Yaml
                    new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
                    new object[] { false, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                    new object[] { false, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                    new object[] { false, TestCaseType.ProjectLockYaml, SR.ProjectLockYaml},
                    new object[] { false, TestCaseType.Yaml400B, SR.Yaml400B},
                    new object[] { false, TestCaseType.Yaml40KB, SR.Yaml40KB},
                    new object[] { false, TestCaseType.Yaml400KB, SR.Yaml400KB}
                };
            }
        }

        public static IEnumerable<object[]> SpecialNumTestCases
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { TestCaseType.FullSchema2, SR.FullYamlSchema2},
                    new object[] { TestCaseType.SpecialNumForm, SR.YamlWithSpecialNumFormat},
                };
            }
        }

        public static IEnumerable<object[]> LotsOfCommentsTests
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"   12345   ", true, "12345"},
                    new object[] {"   12345.67890e-12   ", true, "1.23456789E-08"},
                    new object[] {"   true  ", true, "True"},
                    new object[] {"   false   ", true, "False"},
                    new object[] {"   null   ", true, "null"},
                    new object[] {"   \" Test string with \\\"nested quotes \\\" and hex: \\uABCD values! \"   ", true, " Test string with \"nested quotes \" and hex: \uABCD values! "},

                    new object[] {"   12345   ", false, "12345"},
                    new object[] {"   12345.67890e-12   ", false, "1.23456789E-08"},
                    new object[] {"   true  ", false, "True"},
                    new object[] {"   false   ", false, "False"},
                    new object[] {"   null   ", false, "null"},
                    new object[] {"   \" Test string with \\\"nested quotes \\\" and hex: \\uABCD values! \"   ", false, " Test string with \"nested quotes \" and hex: \uABCD values! "},
                };
            }
        }

        public static IEnumerable<object[]> SingleValueYaml
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"  \"h\u6F22\u5B57ello\"  ", "h\u6F22\u5B57ello"},    // "\u6F22\u5B57" is Chinese for "Chinese character" (from the Han script)
                    new object[] {"  \"he\\r\\n\\\"l\\\\\\\"lo\\\\\"  ", "he\\r\\n\\\"l\\\\\\\"lo\\\\"},
                    new object[] {"  12345  ", "12345"},
                    new object[] {"  null  ", "null"},
                    new object[] {"  true  ", "true"},
                    new object[] {"  false  ", "false"},
                };
            }
        }

        public static IEnumerable<object[]> YamlTokenWithExtraValue
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"  true  5 "},
                    new object[] {"  false  5 "},
                    new object[] {"  null  5 "},
                    new object[] {"  5  5 "},
                    new object[] {"  5.1234e-4  5 "},
                    new object[] {"  \"hello\"  5 "},
                    new object[] {"  \"hello\"  \"hello\" "},
                    new object[] {"  [  ]  5 "},
                    new object[] {"  [  ]  [] "},
                    new object[] {"  [  ]  {} "},
                    new object[] {"  { }  5 "},
                    new object[] {"  { }  [] "},
                    new object[] {"  { }  {} "},
                    new object[] {"  [  ]5 "},
                    new object[] {"  [  ][] "},
                    new object[] {"  [  ]{} "},
                    new object[] {"  { }5 "},
                    new object[] {"  { }[] "},
                    new object[] {"  { }{} "},
                    new object[] {"  { }  5.1234e-4"},
                    new object[] {"  { }  null "},
                    new object[] {"  { }  false "},
                    new object[] {"  { }  true "},
                    new object[] {"  { }  \"hello\" " },
                    new object[] {"  { },  5 "},
                    new object[] {"  { },  [] "},
                    new object[] {"  { },  {} "},
                    new object[] {"  { },  5.1234e-4"},
                    new object[] {"  { },  null "},
                    new object[] {"  { },  false "},
                    new object[] {"  { },  true "},
                    new object[] {"  { },  \"hello\" " },
                };
            }
        }

        public static IEnumerable<object[]> YamlTokenWithExtraValueAndComments
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"  true  /* comment */ 5 "},
                    new object[] {"  false  /* comment */ 5 "},
                    new object[] {"  null  /* comment */ 5 "},
                    new object[] {"  5  /* comment */ 5 "},
                    new object[] {"  5.1234e-4  /* comment */ 5 "},
                    new object[] {"  \"hello\"  /* comment */ 5 "},
                    new object[] {"  \"hello\"  /* comment */ \"hello\" "},
                    new object[] {"  \"hello\"  // comment \n \"hello\" "},
                    new object[] {"  [  ]  /* comment */ 5 "},
                    new object[] {"  [  ]  /* comment */ [ ]"},
                    new object[] {"  [  ]  /* comment */ { }"},
                    new object[] {"  [  ]  // comment \n 5 "},
                    new object[] {"  { }  /* comment */ 5 "},
                    new object[] {"  { }  /* comment */ [] "},
                    new object[] {"  { }  /* comment */ {} "},
                    new object[] {"  [  ]/* comment */5 "},
                    new object[] {"  [  ]/* comment */[ ]"},
                    new object[] {"  [  ]/* comment */{ }"},
                    new object[] {"  [  ]// comment \n5 "},
                    new object[] {"  { }/* comment */5 "},
                    new object[] {"  { }/* comment */[] "},
                    new object[] {"  { }/* comment */{} "},
                    new object[] {"  { }  /* comment */ 5.1234e-4"},
                    new object[] {"  { }  /* comment */ null "},
                    new object[] {"  { }  /* comment */ false "},
                    new object[] {"  { }  /* comment */ true "},
                    new object[] {"  { }  /* comment */ \"hello\" "},
                    new object[] {"  { }  // comment \n \"hello\" "},
                    new object[] {"  { },  /* comment */ 5 "},
                    new object[] {"  { },  /* comment */ [] "},
                    new object[] {"  { },  /* comment */ {} "},
                    new object[] {"  { },  /* comment */ 5.1234e-4"},
                    new object[] {"  { },  /* comment */ null "},
                    new object[] {"  { },  /* comment */ false "},
                    new object[] {"  { },  /* comment */ true "},
                    new object[] {"  { },  /* comment */ \"hello\" "},
                    new object[] {"  { },  // comment \n \"hello\" "},
                };
            }
        }

        public static IEnumerable<object[]> YamlWithValidTrailingCommas
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"{\"name\": \"value\",}"},
                    new object[] {"{\"name\": [],}"},
                    new object[] {"{\"name\": 1,}"},
                    new object[] {"{\"name\": true,}"},
                    new object[] {"{\"name\": false,}"},
                    new object[] {"{\"name\": null,}"},
                    new object[] {"{\"name\": [{},],}"},
                    new object[] {"{\"first\" : \"value\", \"name\": [{},], \"last\":2 ,}"},
                    new object[] {"{\"prop\":{\"name\": 1,\"last\":2,},}"},
                    new object[] {"{\"prop\":[1,2,],}"},
                    new object[] {"[\"value\",]"},
                    new object[] {"[1,]"},
                    new object[] {"[true,]"},
                    new object[] {"[false,]"},
                    new object[] {"[null,]"},
                    new object[] {"[{},]"},
                    new object[] {"[{\"name\": [],},]"},
                    new object[] {"[1, {\"name\": [],},2 , ]"},
                    new object[] {"[[1,2,],]"},
                    new object[] {"[{\"name\": 1,\"last\":2,},]"},
                };
            }
        }

        public static IEnumerable<object[]> YamlWithValidTrailingCommasAndComments
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"{\"name\": \"value\"/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": []/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": 1/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": true/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": false/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": null/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": [{},]/*comment*/,/*comment*/}"},
                    new object[] {"{\"first\" : \"value\", \"name\": [{},], \"last\":2 /*comment*/,/*comment*/}"},
                    new object[] {"{\"prop\":{\"name\": 1,\"last\":2,}/*comment*/,}"},
                    new object[] {"{\"prop\":[1,2,]/*comment*/,}"},
                    new object[] {"{\"prop\":1,/*comment*/}"},
                    new object[] {"[\"value\"/*comment*/,/*comment*/]"},
                    new object[] {"[1/*comment*/,/*comment*/]"},
                    new object[] {"[true/*comment*/,/*comment*/]"},
                    new object[] {"[false/*comment*/,/*comment*/]"},
                    new object[] {"[null/*comment*/,/*comment*/]"},
                    new object[] {"[{}/*comment*/,/*comment*/]"},
                    new object[] {"[{\"name\": [],}/*comment*/,/*comment*/]"},
                    new object[] {"[1, {\"name\": [],},2 /*comment*/,/*comment*/ ]"},
                    new object[] {"[[1,2,]/*comment*/,]"},
                    new object[] {"[{\"name\": 1,\"last\":2,}/*comment*/,]"},
                    new object[] {"[1,/*comment*/]"},
                };
            }
        }

        public static IEnumerable<object[]> YamlWithInvalidTrailingCommas
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {","},
                    new object[] {"   ,   "},
                    new object[] {"{},"},
                    new object[] {"[],"},
                    new object[] {"1,"},
                    new object[] {"true,"},
                    new object[] {"false,"},
                    new object[] {"null,"},
                    new object[] {"{,}"},
                    new object[] {"{\"name\": 1,,}"},
                    new object[] {"{\"name\": 1,,\"last\":2,}"},
                    new object[] {"[,]"},
                    new object[] {"[1,,]"},
                    new object[] {"[1,,2,]"},
                };
            }
        }

        public static IEnumerable<object[]> YamlWithInvalidTrailingCommasAndComments
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"/*comment*/ ,/*comment*/"},
                    new object[] {"   /*comment*/ ,  /*comment*/ "},
                    new object[] {"{}/*comment*/,/*comment*/"},
                    new object[] {"[]/*comment*/,/*comment*/"},
                    new object[] {"1/*comment*/,/*comment*/"},
                    new object[] {"true/*comment*/,/*comment*/"},
                    new object[] {"false/*comment*/,/*comment*/"},
                    new object[] {"null/*comment*/,/*comment*/"},
                    new object[] {"{/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": 1/*comment*/,/*comment*/,/*comment*/}"},
                    new object[] {"{\"name\": 1,/*comment*/,\"last\":2,}"},
                    new object[] {"[/*comment*/,/*comment*/]"},
                    new object[] {"[1/*comment*/,/*comment*/,/*comment*/]"},
                    new object[] {"[1,/*comment*/,2,]"},
                };
            }
        }

        public static IEnumerable<object[]> SingleYamlTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"[]", 0},
                    new object[] {"{}", 0},
                    new object[] {"12345", 0},
                    new object[] {"1", 0},
                    new object[] {"-0", 0},
                    new object[] {"0.0e-0", 0},
                    new object[] {"0.0e+0", 0},
                    new object[] {"true", 0},
                    new object[] {"false", 0},
                    new object[] {"null", 0},
                    new object[] {"\"hello\"", 0},
                    new object[] {"\"\"", 0},

                    new object[] {"  []", 2},
                    new object[] {"  {}", 2},
                    new object[] {"  12345", 2},
                    new object[] {"  1", 2},
                    new object[] {"  -0", 2},
                    new object[] {"  0.0e-0", 2},
                    new object[] {"  0.0e+0", 2},
                    new object[] {"  true", 2},
                    new object[] {"  false", 2},
                    new object[] {"  null", 2},
                    new object[] {"  \"hello\"", 2},
                    new object[] {"  \"\"", 2},

                    new object[] {"  []  ", 2},
                    new object[] {"  {}  ", 2},
                    new object[] {"  12345  ", 2},
                    new object[] {"  1  ", 2},
                    new object[] {"  -0  ", 2},
                    new object[] {"  0.0e-0  ", 2},
                    new object[] {"  0.0e+0  ", 2},
                    new object[] {"  true  ", 2},
                    new object[] {"  false  ", 2},
                    new object[] {"  null  ", 2},
                    new object[] {"  \"hello\"  ", 2},
                    new object[] {"  \"\"  ", 2},
                };
            }
        }

        public static IEnumerable<object[]> SingleYamlWithCommentsAllowTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"/*comment*/", 0},
                    new object[] {"//comment\n", 0},
                    new object[] {"/*comment*//*comment*/", 0},
                    new object[] {"/*comment*///comment\n", 0},
                    new object[] {"//comment\n/*comment*/", 0},
                    new object[] {"//comment\n//comment\n", 0},

                    new object[] {"  /*comment*/", 2},
                    new object[] {"  //comment\n", 2},
                    new object[] {"  /*comment*//*comment*/", 2},
                    new object[] {"  /*comment*///comment\n", 2},
                    new object[] {"  //comment\n/*comment*/", 2},
                    new object[] {"  //comment\n//comment\n", 2},

                    new object[] {"  /*comment*/  ", 2},
                    new object[] {"  //comment\n  ", 2},
                    new object[] {"  /*comment*//*comment*/  ", 2},
                    new object[] {"  /*comment*///comment\n  ", 2},
                    new object[] {"  //comment\n/*comment*/  ", 2},
                    new object[] {"  //comment\n//comment\n  ", 2},
                };
            }
        }

        public static IEnumerable<object[]> SingleYamlWithCommentsTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"/*comment*/[]", 11},
                    new object[] {"/*comment*/{}", 11},
                    new object[] {"/*comment*/12345", 11},
                    new object[] {"/*comment*/12345  ", 11},
                    new object[] {"/*comment*/12345/*comment*/", 11},
                    new object[] {"/*comment*/12345  /*comment*/", 11},
                    new object[] {"/*comment*/12345  /*comment*/  ", 11},
                    new object[] {"/*comment*/1", 11},
                    new object[] {"/*comment*/true", 11},
                    new object[] {"/*comment*/false", 11},
                    new object[] {"/*comment*/null", 11},
                    new object[] {"/*comment*/\"hello\"", 11},
                    new object[] {"/*comment*/\"\"", 11},

                    new object[] {"  /*comment*/  []", 15},
                    new object[] {"  /*comment*/  {}", 15},
                    new object[] {"  /*comment*/  12345", 15},
                    new object[] { "  /*comment*/  12345  ", 15},
                    new object[] { "  /*comment*/  12345/*comment*/", 15},
                    new object[] { "  /*comment*/  12345  /*comment*/", 15},
                    new object[] { "  /*comment*/  12345  /*comment*/  ", 15},
                    new object[] {"  /*comment*/  1", 15},
                    new object[] {"  /*comment*/  true", 15},
                    new object[] {"  /*comment*/  false", 15},
                    new object[] {"  /*comment*/  null", 15},
                    new object[] {"  /*comment*/  \"hello\"", 15},
                    new object[] {"  /*comment*/  \"\"", 15},
                };
            }
        }

        public static IEnumerable<object[]> ComplexArrayYamlTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"[1,2]", 3},
                    new object[] {"[1,  2]", 5},
                    new object[] {"[1  ,2]", 5},
                    new object[] {"[1  ,  2]", 7},
                    new object[] {"[1  ,  2  ]", 7},

                    new object[] {"[1,\"string\"]", 3},
                    new object[] {"[1,  \"string\"]", 5},
                    new object[] {"[1  ,\"string\"]", 5},
                    new object[] {"[1  ,  \"string\"]", 7},
                    new object[] {"[1  ,  \"string\"  ]", 7},

                    new object[] {"[{}]", 2},
                    new object[] {"[[]]", 2},
                    new object[] {"[123,{}]", 5},
                    new object[] {"[123,[]]", 5},
                    new object[] {"[  {}]", 4},
                    new object[] {"[  []]", 4},
                    new object[] {"[123,  {}]", 7},
                    new object[] {"[123,  []]", 7},
                };
            }
        }

        public static IEnumerable<object[]> ComplexObjectYamlTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"{\"propertyName\":\"value\"}", 1, 16},
                    new object[] {"{  \"propertyName\":\"value\"}", 3, 18},
                    new object[] {"{\"propertyName\"  :\"value\"}", 1, 18},
                    new object[] {"{\"propertyName\":  \"value\"}", 1, 18},
                    new object[] {"{\"propertyName\":\"value\"  }", 1, 16},
                    new object[] {"  {\"propertyName\":\"value\"}", 3, 18},
                    new object[] {"{\"propertyName\":\"value\"}  ", 1, 16},

                    new object[] {"{  \"propertyName\"  :\"value\"}", 3, 20},
                    new object[] {"{  \"propertyName\":  \"value\"}", 3, 20},
                    new object[] {"{  \"propertyName\":\"value\"  }", 3, 18},
                    new object[] {"  {  \"propertyName\":\"value\"}", 5, 20},
                    new object[] {"{  \"propertyName\":\"value\"}   ", 3, 18},

                    new object[] {"{\"propertyName\"  :  \"value\"}", 1, 20},
                    new object[] {"{\"propertyName\":  \"value\"  }", 1, 18},
                    new object[] {"  {\"propertyName\":  \"value\"}", 3, 20},
                    new object[] {"{\"propertyName\":  \"value\"}  ", 1, 18},

                    new object[] {"{\"propertyName\"  :\"value\"  }", 1, 18},
                    new object[] {"  {\"propertyName\"  :\"value\"}", 3, 20},
                    new object[] {"{\"propertyName\"  :\"value\"}  ", 1, 18},

                    new object[] {"  {\"propertyName\":\"value\"  }", 3, 18},
                    new object[] {"{\"propertyName\":\"value\"  }  ", 1, 16},

                    new object[] {"{\"propertyName\":123}", 1, 16},
                    new object[] {"{  \"propertyName\":123}", 3, 18},
                    new object[] {"{\"propertyName\"  :123}", 1, 18},
                    new object[] {"{\"propertyName\":  123}", 1, 18},
                    new object[] {"{\"propertyName\":123  }", 1, 16},
                    new object[] {"  {\"propertyName\":123}", 3, 18},
                    new object[] {"{\"propertyName\":123}   ", 1, 16},

                    new object[] {"{  \"propertyName\"  :123}", 3, 20},
                    new object[] {"{  \"propertyName\":  123}", 3, 20},
                    new object[] {"{  \"propertyName\":123  }", 3, 18},
                    new object[] {"  {  \"propertyName\":123}", 5, 20},
                    new object[] {"{  \"propertyName\":123}  ", 3, 18},

                    new object[] {"{\"propertyName\"  :  123}", 1, 20},
                    new object[] {"{\"propertyName\":  123  }", 1, 18},
                    new object[] {"  {\"propertyName\":  123}", 3, 20},
                    new object[] {"{\"propertyName\":  123}  ", 1, 18},

                    new object[] {"{\"propertyName\"  :123  }", 1, 18},
                    new object[] {"  {\"propertyName\"  :123}", 3, 20},
                    new object[] {"{\"propertyName\"  :123}  ", 1, 18},

                    new object[] {"  {\"propertyName\":123  }", 3, 18},
                    new object[] {"{\"propertyName\":123  }  ", 1, 16},

                    new object[] {"{\"propertyName\":[]}", 1, 16},
                    new object[] {"{  \"propertyName\":[]}", 3, 18},
                    new object[] {"{\"propertyName\"  :[]}", 1, 18},
                    new object[] {"{\"propertyName\":  []}", 1, 18},
                    new object[] {"{\"propertyName\":[]  }", 1, 16},
                    new object[] {"  {\"propertyName\":[]}", 3, 18},
                    new object[] {"{\"propertyName\":[]}  ", 1, 16},

                    new object[] {"{\"propertyName\":{}}", 1, 16},
                    new object[] {"{  \"propertyName\":{}}", 3, 18},
                    new object[] {"{\"propertyName\"  :{}}", 1, 18},
                    new object[] {"{\"propertyName\":  {}}", 1, 18},
                    new object[] {"{\"propertyName\":{}  }", 1, 16},
                    new object[] {"  {\"propertyName\":{}}", 3, 18},
                    new object[] {"{\"propertyName\":{}}  ", 1, 16},
                };
            }
        }

        public static IEnumerable<object[]> ComplexObjectSeveralYamlTokenStartIndex
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"{\"\":\"\", \"propertyName\":[]}", 8, 23},
                    new object[] {"{\"\":\"\",   \"propertyName\":[]}", 10, 25},
                    new object[] {"{\"\":\"\", \"propertyName\"  :[]}", 8, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":  []}", 8, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":[]  }", 8, 23},
                    new object[] {"  {\"\":\"\", \"propertyName\":[]}", 10, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":[]}  ", 8, 23},

                    new object[] {"{\"\":\"\", \"propertyName\":{}}", 8, 23},
                    new object[] {"{\"\":\"\",   \"propertyName\":{}}", 10, 25},
                    new object[] {"{\"\":\"\", \"propertyName\"  :{}}", 8, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":  {}}", 8, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":{}  }", 8, 23},
                    new object[] {"{  \"\":\"\", \"propertyName\":{}}", 10, 25},
                    new object[] {"{\"\":\"\", \"propertyName\":{}}  ", 8, 23},
                };
            }
        }

        public static IEnumerable<object[]> InvalidYamlStrings
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"\"", 0, 1},
                    new object[] {"{]", 0, 1},
                    new object[] {"[}", 0, 1},
                    new object[] {"nul", 0, 3},
                    new object[] {"tru", 0, 3},
                    new object[] {"fals", 0, 4},
                    new object[] {"falseinvalid", 0, 5},
                    new object[] {"\"a\u6F22\u5B57ge\":", 0, 11},
                    new object[] {"{\"a\u6F22\u5B57ge\":", 0, 13},
                    new object[] {"{\"name\":\"A\u6F22\u5B57hso", 0, 19},
                    new object[] {"{\"name\":\"A\u6F22\u5B57h\\nso", 0, 21},
                    new object[] {"12345.1.", 0, 7},
                    new object[] {"-", 0, 1},
                    new object[] {"-f", 0, 1},
                    new object[] {"1.f", 0, 2},
                    new object[] {"0.", 0, 2},
                    new object[] {"0.1f", 0, 3},
                    new object[] {"0.1e1f", 0, 5},
                    new object[] {"123,", 0, 3},
                    new object[] {"false,", 0, 5},
                    new object[] {"true,", 0, 4},
                    new object[] {"null,", 0, 4},
                    new object[] {"trUe,", 0, 2},
                    new object[] {"\"h\u6F22\u5B57ello\",", 0, 13},
                    new object[] {"\"\\u12z3\"", 0, 5},
                    new object[] {"\"\\u12]3\"", 0, 5},
                    new object[] {"\"\\u12=3\"", 0, 5},
                    new object[] {"\"\\u12$3\"", 0, 5},
                    new object[] {"\"\\u12\"", 0, 5},
                    new object[] {"\"\\u120\"", 0, 6},
                    new object[] {"+0", 0, 0},
                    new object[] {"+1", 0, 0},
                    new object[] {"0e", 0, 2},
                    new object[] {"0.1e", 0, 4},
                    new object[] {"01", 0, 1},
                    new object[] {"1a", 0, 1},
                    new object[] {"-01", 0, 2},
                    new object[] {"10.5e", 0, 5},
                    new object[] {"10.5e-", 0, 6},
                    new object[] {"10.5e+", 0, 6},
                    new object[] {"10.5e-0.2", 0, 7},
                    new object[] {"{\"age\":30, \"ints\":[1, 2, 3, 4, 5.1e7.3]}", 0, 36},
                    new object[] {"{\"age\":30, \r\n \"num\":-0.e, \r\n \"ints\":[1, 2, 3, 4, 5]}", 1, 10},
                    new object[] {"{ \"number\": 00", 0, 13},
                    new object[] {"{{}}", 0, 1},
                    new object[] {"[[]", 0, 3},
                    new object[] {"[[{{}}]]", 0, 3},
                    new object[] {"[1, 2, 3, ]", 0, 10},
                    new object[] {"{\"ints\":[1, 2, 3, 4, 5", 0, 22},
                    new object[] {"{\"s\u6F22\u5B57trings\":[\"a\u6F22\u5B57bc\", \"def\"", 0, 36},
                    new object[] {"{\"age\":30, \"ints\":[1, 2, 3, 4, 5}}", 0, 32},
                    new object[] {"{\"age\":30, \"name\":\"test}", 0, 24},
                    new object[] {"{\r\n\"isActive\": false \"\r\n}", 1, 18},
                    new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[}]]]]}]]]]", 1, 28},
                    new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2:[]}]]]]}]]]]", 1, 38},
                    new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[]},[}]]]]}]]]]", 1, 32},
                    new object[] {"{\r\n\t\"isActive\": false,\r\n\t\"array\": [\r\n\t\t[{\r\n\t\t\t\"id\": 1\r\n\t\t}]\r\n\t]\r\n}", 3, 3, 3},
                    new object[] {"{\"Here is a \u6F22\u5B57string: \\\"\\\"\":\"Here is \u6F22\u5B57a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str:\"\\\"\\\"\"}", 0, 190},
                    new object[] {"\"hel\rlo\"", 0, 4},
                    new object[] {"\"hel\nlo\"", 0, 4},
                    new object[] {"\"hel\\uABCXlo\"", 0, 9},
                    new object[] {"\"hel\\\tlo\"", 0, 5},
                    new object[] {"\"hel\rlo\\\"\"", 0, 4},
                    new object[] {"\"hel\nlo\\\"\"", 0, 4},
                    new object[] {"\"hel\\uABCXlo\\\"\"", 0, 9},
                    new object[] {"\"hel\\\tlo\\\"\"", 0, 5},
                    new object[] {"\"he\\nl\rlo\\\"\"", 0, 6},
                    new object[] {"\"he\\nl\nlo\\\"\"", 0, 6},
                    new object[] {"\"he\\nl\\uABCXlo\\\"\"", 0, 11},
                    new object[] {"\"he\\nl\\\tlo\\\"\"", 0, 7},
                    new object[] {"\"he\\nl\rlo", 0, 6},
                    new object[] {"\"he\\nl\nlo", 0, 6},
                    new object[] {"\"he\\nl\\uABCXlo", 0, 11},
                    new object[] {"\"he\\nl\\\tlo", 0, 7},
                };
            }
        }

        public static IEnumerable<object[]> InvalidUTF8Strings
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { new byte[] { 34, 97, 0xc3, 0x28, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xa0, 0xa1, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xe2, 0x28, 0xa1, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xe2, 0x82, 0x28, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0xbc, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xf0, 0x90, 0x28, 0xbc, 98, 34 } },
                    new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0x28, 98, 34 } },
                };
            }
        }

        public static IEnumerable<object[]> CommentTestLineSeparators
        {
            get
            {
                return new List<object[]>
                {
                    new object [] {"\r" },
                    new object [] {"\r\n" },
                    new object [] {"\n" },
                };
            }
        }

        public static IEnumerable<object[]> TrySkipValues
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"[[[]], {\"a\":1, \"b\": 2}, 3, {\"a\":{}, \"b\":{\"c\":[]} }, [{\"a\":1, \"b\": 2}, null]]", YamlTokenType.EndArray},
                    new object[] {"[]", YamlTokenType.EndArray},
                    new object[] {"[[],[],[],[]]", YamlTokenType.EndArray},
                    new object[] {"[[[],[],[]]]", YamlTokenType.EndArray},
                    new object[] {"[{},{},{},{}]", YamlTokenType.EndArray},
                    new object[] {"[{\"a\":[], \"b\":[], \"c\":[]}]", YamlTokenType.EndArray},
                    new object[] {"{\"a\":{\"b\":{}}, \"c\":[1, 2], \"d\": 3, \"e\":[[], [{}] ], \"f\":{\"g\":[1, 2], \"e\":null}}", YamlTokenType.EndObject},
                    new object[] {"{}", YamlTokenType.EndObject},
                    new object[] {"{\"a\":{}, \"b\":{}, \"c\":{}, \"d\":{}}", YamlTokenType.EndObject},
                    new object[] {"{\"a\":{\"b\":{}, \"c\":{}, \"d\":{}}}", YamlTokenType.EndObject},
                    new object[] {"{\"a\":[], \"b\":[], \"c\":[], \"d\":[]}", YamlTokenType.EndObject},
                    new object[] {"{\"a\":[{}, {}, {}]}", YamlTokenType.EndObject},
                };
            }
        }

        public static IEnumerable<object[]> GetCommentTestData
        {
            get
            {
                var dataList = new List<object[]>();
                foreach (string delim in new[] { "\r", "\r\n", "\n" })
                {
                    // NOTE: Leading and trailing spaces in the comments are significant.
                    var singleLineComment = " Single Line Comment ";
                    dataList.Add(new object[] { $"{{//{singleLineComment}{delim}}}", singleLineComment });

                    var multilineComment = $" Multiline {delim} Comment ";
                    dataList.Add(new object[] { $"{{/*{multilineComment}*/{delim}}}", multilineComment });
                }
                return dataList;
            }
        }

        public static IEnumerable<object[]> GetCommentUnescapeData
        {
            get
            {
                var dataList = new List<object[]>();

                var rawComments = new string[]
                {
                    "A string with {0}valid UTF8 \\t tab",
                    "A string with {0}invalid UTF8 \\xc3\\x28",
                    "A string with {0}valid UTF16 \\u002e \\u0009 \u092E",
                    "A string with {0}invalid UTF16 \\uDD1E"
                };

                // single line comments
                foreach (string raw in rawComments)
                {
                    string str = string.Format(raw, "");
                    string cmt = "//" + str;
                    dataList.Add(new object[] { cmt, str });
                }

                // multiline comments
                foreach (string raw in rawComments)
                {
                    string str = string.Format(raw, "\n");
                    string cmt = "/*" + str + "*/";
                    dataList.Add(new object[] { cmt, str });
                }

                return dataList;
            }
        }

    }
}
