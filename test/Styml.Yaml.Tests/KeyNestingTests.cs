// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Linq;
using System.Threading;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A key nested in a key is read with one more copy of the reader, one level of recursion each.
    /// With MaxDepth raised, every nesting shape must end in an exception the caller can catch or be read; it must never
    /// overflow the stack. The tests run on a thread with a small stack, so they are fast and cannot pass by luck.
    /// </summary>
    public class KeyNestingTests
    {
        private const int SmallStack = 1024 * 1024;

        public static TheoryData<string, int> Shapes() => new()
        {
            { "explicit", 5_000 },
            { "explicit-in-sequence", 5_000 },
            { "flow-mapping", 5_000 },
            { "flow-sequence-pair", 400 },
            { "explicit", 100_000 },
            { "flow-mapping", 100_000 },
        };

        internal static string Build(string shape, int depth) => shape switch
        {
            // '? ? ? a' (compact: each explicit key holds the next one on the same line)
            "explicit" => string.Concat(Enumerable.Repeat("? ", depth)) + "a\n",
            // '? - ? - a'
            "explicit-in-sequence" => string.Concat(Enumerable.Repeat("? - ", depth)) + "a\n",
            // '{{{a: 1}: 1}: 1}'
            "flow-mapping" => new string('{', depth) + "a: 1" + string.Concat(Enumerable.Repeat("}: 1", depth - 1)) + "}",
            // '[[[a]: 1]: 1]: 1' (implicit keys: at most 1024 characters, so this nesting is bounded by length)
            _ => new string('[', depth) + "a" + string.Concat(Enumerable.Repeat("]: 1", depth - 1)) + "]",
        };

        private static Exception? ReadOnSmallStack(string yaml)
        {
            Exception? failure = null;
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
            var thread = new Thread(() =>
            {
                try
                {
                    var reader = new Utf8YamlReader(utf8, new YamlReaderOptions { MaxDepth = 1_000_000 });
                    while (reader.Read())
                    {
                    }
                }
                catch (Exception e)
                {
                    failure = e;
                }
            }, SmallStack);
            thread.Start();
            thread.Join();
            return failure;
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void DeepKeyNestingEndsInACatchableException(string shape, int depth)
        {
            Exception? failure = ReadOnSmallStack(Build(shape, depth));
            if (failure is not null)
            {
                // The stack guard, or for the implicit pairs the 1024-character limit of their outer keys.
                YamlException e = Assert.IsAssignableFrom<YamlException>(failure);
                Assert.True(e.Message.Contains("nested inside other keys too deeply") || e.Message.Contains("1024"), e.Message);
            }
            if (shape != "flow-sequence-pair" && depth >= 5_000)
            {
                // At this depth the 1 MB stack runs out before the input does.
                Assert.Contains("nested inside other keys too deeply", failure?.Message);
            }
        }

        [Fact]
        public void ModerateKeyNestingReads()
        {
            Assert.Null(ReadOnSmallStack(Build("explicit", 50)));
            Assert.Null(ReadOnSmallStack(Build("flow-mapping", 50)));
            Assert.Null(ReadOnSmallStack(Build("flow-sequence-pair", 50)));
        }

        [Theory]
        [InlineData("- - - a")]
        [InlineData("[[[a]]]")]
        public void DeepNestingWithoutKeysReads(string pattern)
        {
            string yaml = pattern[0] == '-'
                ? string.Concat(Enumerable.Repeat("- ", 100_000)) + "a\n"
                : new string('[', 100_000) + "a" + new string(']', 100_000);
            Assert.Null(ReadOnSmallStack(yaml));
        }

        [Fact]
        public void DefaultMaxDepthStillApplies()
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() =>
            {
                var r = new Utf8YamlReader(Encoding.UTF8.GetBytes(Build("flow-mapping", 5_000)));
                while (r.Read())
                {
                }
            });
            Assert.Contains("depth", e.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TooLongAndMultiLineKeysReportTheirOwnError()
        {
            // Nested pairs whose outer keys pass 1024 characters: the ':' after one of them reports the length limit.
            YamlException tooLong = Assert.ThrowsAny<YamlException>(() => Read(Build("flow-sequence-pair", 2_000), maxDepth: 100_000));
            Assert.Contains("1024", tooLong.Message);

            YamlException rootTooLong = Assert.ThrowsAny<YamlException>(() => Read("[" + new string('a', 1100) + "]: v\n"));
            Assert.Contains("1024", rootTooLong.Message);

            YamlException multiLine = Assert.ThrowsAny<YamlException>(() => Read("[a,\n b]: v\n"));
            Assert.Contains("single line", multiLine.Message);

            YamlException pairMultiLine = Assert.ThrowsAny<YamlException>(() => Read("[[a,\n b]: v]\n"));
            Assert.Contains("single line", pairMultiLine.Message);
        }

        private static void Read(string yaml, int maxDepth = 64)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { MaxDepth = maxDepth });
            while (reader.Read())
            {
            }
        }
    }
}
