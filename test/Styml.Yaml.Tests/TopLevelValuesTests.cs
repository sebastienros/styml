// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Top-level values under the YAML grammar are '---'-separated documents (decision D3). Newline-separated values
    /// (JSON Lines) are refused, as YamlDotNet and SharpYaml refuse them (docs/feature-parity.md); these tests
    /// pin the replacement: DeserializeAsyncEnumerable with topLevelValues: true over YAML documents.
    /// </summary>
    public class TopLevelValuesTests
    {
        public class Point
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public static IEnumerable<object[]> BufferSizes() => [[0], [1]];

        private static YamlSerializerOptions Options(int bufferSize) =>
            bufferSize == 0 ? new YamlSerializerOptions() : new YamlSerializerOptions { DefaultBufferSize = bufferSize };

        private static async Task<List<T?>> ReadAll<T>(string yaml, int bufferSize)
        {
            var result = new List<T?>();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            await foreach (T? item in YamlSerializer.DeserializeAsyncEnumerable<T>(stream, topLevelValues: true, Options(bufferSize)))
            {
                result.Add(item);
            }
            return result;
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task Integers(int bufferSize)
        {
            Assert.Equal([1, 22, -3], await ReadAll<int>("1\n---\n22\n--- -3\n", bufferSize));
            Assert.Equal([1, 2], await ReadAll<int>("--- 1\n...\n--- 2\n...\n", bufferSize));
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task BlockMappings(int bufferSize)
        {
            List<Point?> points = await ReadAll<Point>("X: 1\nY: 2\n---\n# second\nX: 3\nY: 4\n---\nX: 5\n", bufferSize);
            Assert.Equal(3, points.Count);
            Assert.Equal((1, 2), (points[0]!.X, points[0]!.Y));
            Assert.Equal((3, 4), (points[1]!.X, points[1]!.Y));
            Assert.Equal((5, 0), (points[2]!.X, points[2]!.Y));
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task FlowSequences(int bufferSize)
        {
            List<List<int>?> lists = await ReadAll<List<int>>("[1, 2]\n---\n[]\n--- [3,\n  4, 5]\n", bufferSize);
            Assert.Equal(3, lists.Count);
            Assert.Equal([1, 2], lists[0]!);
            Assert.Empty(lists[1]!);
            Assert.Equal([3, 4, 5], lists[2]!);
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task BlockSequences(int bufferSize)
        {
            List<List<string>?> lists = await ReadAll<List<string>>("- a\n- b\n---\n- c\n---\n-   d\n-   e\n", bufferSize);
            Assert.Equal(3, lists.Count);
            Assert.Equal(["a", "b"], lists[0]!);
            Assert.Equal(["c"], lists[1]!);
            Assert.Equal(["d", "e"], lists[2]!);
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task JsonShapedDocumentsIntoObject(int bufferSize)
        {
            // object goes through YamlDocument, which reads JSON-shaped YAML only until M5.
            List<object?> values = await ReadAll<object>("1\n---\n{\"X\": 1}\n---\n[1, 2]\n--- \"a\"\n", bufferSize);
            Assert.Equal(["1", "{\"X\":1}", "[1,2]", "a"], values.ConvertAll(v => v!.ToString()!.Replace(" ", "")).ToArray());
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task JsonLines_Rejected(int bufferSize)
        {
            await Assert.ThrowsAnyAsync<YamlException>(() => ReadAll<Point>("{\"X\": 1}\n{\"X\": 2}\n", bufferSize));
            await Assert.ThrowsAnyAsync<YamlException>(() => ReadAll<List<int>>("[1]\n[2]\n", bufferSize));
        }
    }
}
