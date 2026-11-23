// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class YamlReaderStateAndOptionsTests
    {
        [Fact]
        public static void DefaultYamlReaderState()
        {
            YamlReaderState state = default;

            var expectedOption = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Disallow,
                MaxDepth = 0
            };
            Assert.Equal(expectedOption, state.Options);
        }

        [Fact]
        public static void YamlReaderStateDefaultCtor()
        {
            var state = new YamlReaderState();

            var expectedOption = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Disallow,
                MaxDepth = 0
            };
            Assert.Equal(expectedOption, state.Options);
        }

        [Fact]
        public static void YamlReaderStateCtor()
        {
            var state = new YamlReaderState(default);

            var expectedOption = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Disallow,
                MaxDepth = 0
            };
            Assert.Equal(expectedOption, state.Options);

            state = new YamlReaderState(new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow, MaxDepth = 0 });
            Assert.Equal(expectedOption, state.Options);

            expectedOption = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Disallow,
                MaxDepth = 32
            };
            state = new YamlReaderState(new YamlReaderOptions { MaxDepth = 32 });
            Assert.Equal(32, state.Options.MaxDepth);
            Assert.Equal(expectedOption, state.Options);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(byte.MaxValue)]
        [InlineData(byte.MaxValue + 4)] // Other values, like byte.MaxValue + 1 overflows to 0 (i.e. YamlCommentHandling.Disallow), which is valid.
        public static void TestCommentHandlingInvalid(int enumValue)
        {
            Assert.Throws<ArgumentOutOfRangeException>("value", () => new YamlReaderOptions { CommentHandling = (YamlCommentHandling)enumValue });
            Assert.Throws<ArgumentOutOfRangeException>("value", () => new YamlReaderState(new YamlReaderOptions { CommentHandling = (YamlCommentHandling)enumValue }));
        }

        [Theory]
        [InlineData(-1)]
        public static void TestDepthInvalid(int depth)
        {
            Assert.Throws<ArgumentOutOfRangeException>("value", () => new YamlReaderOptions { MaxDepth = depth });
            Assert.Throws<ArgumentOutOfRangeException>("value", () => new YamlReaderState(new YamlReaderOptions { MaxDepth = depth }));
        }

        [Fact]
        public static void DefaultYamlReaderOptions()
        {
            YamlReaderOptions options = default;

            var expectedOption = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Disallow,
                MaxDepth = 0
            };
            Assert.Equal(expectedOption, options);

            options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow };
            Assert.Equal(expectedOption, options);

            options = new YamlReaderOptions { MaxDepth = 0 };
            Assert.Equal(expectedOption, options);

            options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow, MaxDepth = 0 };
            Assert.Equal(expectedOption, options);
        }

        [Fact]
        public static void ZeroMaxDepthDefaultsTo64()
        {
            byte[] dataUtf8 = "{}"u8.ToArray();

            ReadOnlyMemory<byte> dataMemory = dataUtf8;
            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 1));
            ReadOnlyMemory<byte> secondMem = dataMemory.Slice(1);
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
            var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

            var state = new YamlReaderState();
            var reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);

            YamlReaderOptions options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow, MaxDepth = 0 };
            state = new YamlReaderState(options);
            reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
            Assert.Equal(0, options.MaxDepth);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);

            state = new YamlReaderState();
            reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);

            state = new YamlReaderState(options);
            reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.Equal(0, options.MaxDepth);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);
        }

        [Fact]
        public static void MaxDepthIsHonored()
        {
            byte[] dataUtf8 = "{}"u8.ToArray();

            ReadOnlyMemory<byte> dataMemory = dataUtf8;
            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 1));
            ReadOnlyMemory<byte> secondMem = dataMemory.Slice(1);
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
            var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

            YamlReaderOptions options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow, MaxDepth = 64 };
            var state = new YamlReaderState(options);
            var reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
            Assert.Equal(64, options.MaxDepth);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);

            state = new YamlReaderState(options);
            reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.Equal(64, options.MaxDepth);
            Assert.Equal(64, reader.CurrentState.Options.MaxDepth);

            options = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Disallow, MaxDepth = 32 };
            state = new YamlReaderState(options);
            reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
            Assert.Equal(32, options.MaxDepth);
            Assert.Equal(32, reader.CurrentState.Options.MaxDepth);

            state = new YamlReaderState(options);
            reader = new Utf8YamlReader(sequence, isFinalBlock: true, state);
            Assert.Equal(32, options.MaxDepth);
            Assert.Equal(32, reader.CurrentState.Options.MaxDepth);
        }
    }
}
