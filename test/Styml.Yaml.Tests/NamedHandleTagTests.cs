// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A tag with a named handle (<c>%TAG !e! tag:e.com,2000:</c>, then <c>!e!x</c>) reads through every DOM route: a value
    /// read into <c>object</c>, <see cref="YamlElement"/> or <see cref="YamlNode"/> is copied and parsed again, and the copy
    /// keeps the stream's handles.
    /// </summary>
    public class NamedHandleTagTests
    {
        private const string RootTagged = "%TAG !e! tag:e.com,2000:\n--- !e!x &1\na: 1\nb: !e!y [1, 2]\n";
        private const string InnerTagged = "%TAG !e! tag:e.com,2000:\n---\na: !e!x 1\nb: !e!y {c: 2}\nd:\n  - !e!z q\n";

        public static IEnumerable<object[]> Inputs() => new[] { new object[] { RootTagged }, new object[] { InnerTagged } };

        private static ReadOnlySequence<byte> Split(byte[] bytes, int size)
        {
            Segment? first = null, last = null;
            for (int i = 0; i < bytes.Length; i += size)
            {
                var segment = new Segment(bytes.AsMemory(i, Math.Min(size, bytes.Length - i)), last);
                first ??= segment;
                last = segment;
            }

            return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, Segment? previous)
            {
                Memory = memory;
                if (previous is not null)
                {
                    RunningIndex = previous.RunningIndex + previous.Memory.Length;
                    previous.Next = this;
                }
            }
        }

        private static string Expanded(YamlElement element, string name) => element.GetProperty(name).GetExpandedTag()!;

        [Theory]
        [MemberData(nameof(Inputs))]
        public void ObjectElementAndNodeReads_KeepTheHandles(string yaml)
        {
            YamlElement element = YamlSerializer.Deserialize<YamlElement>(yaml);
            Assert.Equal("tag:e.com,2000:y", Expanded(element, "b"));
            Assert.Equal("!e!y", element.GetProperty("b").GetTag());

            var fromObject = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(yaml));
            Assert.Equal("tag:e.com,2000:y", Expanded(fromObject, "b"));

            var inDictionary = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Dictionary<string, object>>(yaml)!["b"]);
            Assert.Equal("tag:e.com,2000:y", inDictionary.GetExpandedTag());

            YamlNode node = YamlSerializer.Deserialize<YamlNode>(yaml)!;
            Assert.Equal(new[] { "a", "b" }, node.AsObject().Select(p => p.Key).Take(2));
            Assert.True(YamlNode.DeepEquals(node, YamlNode.Parse(yaml)));
        }

        [Theory]
        [MemberData(nameof(Inputs))]
        public void ParseValueAndNodeParse_FromAReader_KeepTheHandles(string yaml)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(yaml);
            foreach (ReadOnlySequence<byte> input in new[] { new ReadOnlySequence<byte>(bytes), Split(bytes, 7), Split(bytes, 1) })
            {
                var reader = new Utf8YamlReader(input);
                Assert.True(reader.Read());
                YamlElement element = YamlElement.ParseValue(ref reader);
                Assert.Equal("tag:e.com,2000:y", Expanded(element, "b"));
                if (yaml == RootTagged)
                {
                    Assert.Equal("tag:e.com,2000:x", element.GetExpandedTag());
                    Assert.Equal("1", element.GetAnchor());
                }

                reader = new Utf8YamlReader(input);
                Assert.True(reader.Read());
                YamlNode node = YamlNode.Parse(ref reader)!;
                Assert.True(YamlNode.DeepEquals(node, YamlNode.Parse(yaml)));

                reader = new Utf8YamlReader(input);
                var fromObject = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(ref reader));
                Assert.Equal("tag:e.com,2000:y", Expanded(fromObject, "b"));
            }
        }

        [Fact]
        public void EachDocumentsHandles_InAStream()
        {
            const string yaml = "%TAG !e! tag:e.com,2000:\n--- !e!x\na: 1\n...\n%TAG !e! tag:f.com,2001:\n--- !e!x\nb: 2\n";
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
            var tags = new List<string?>();
            while (reader.Read())
            {
                tags.Add(YamlElement.ParseValue(ref reader).GetExpandedTag());
            }

            Assert.Equal(new[] { "tag:e.com,2000:x", "tag:f.com,2001:x" }, tags);
        }

        [Fact]
        public void AnUndeclaredHandle_IsStillRefused()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<object>("a: !e!x 1\n"));
        }
    }
}
