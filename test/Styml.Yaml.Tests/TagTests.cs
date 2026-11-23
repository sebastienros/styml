// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// %TAG handle expansion (M3d): <see cref="Utf8YamlReader.Tag"/>
    /// stays the tag as written; <see cref="Utf8YamlReader.GetExpandedTag"/> and
    /// <see cref="Utf8YamlReader.TryCopyExpandedTag"/> give the expanded form, as YamlDotNet's and SharpYaml's parsers do.
    /// </summary>
    public class TagTests
    {
        private static readonly YamlReaderOptions s_multiple = new() { AllowMultipleValues = true };

        /// <summary>The (as written, expanded) tags of the document's tagged nodes, in order.</summary>
        private static List<(string Written, string Expanded)> Tags(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), s_multiple);
            var tags = new List<(string, string)>();
            while (reader.Read())
            {
                if (!reader.Tag.IsEmpty)
                {
                    tags.Add((Encoding.UTF8.GetString(reader.Tag.ToArray()), reader.GetExpandedTag()!));
                }
            }
            return tags;
        }

        [Theory]
        [InlineData("%TAG !e! tag:example.com,2000:\n--- !e!foo x\n", "!e!foo", "tag:example.com,2000:foo")]
        [InlineData("%TAG !e! tag:example.com,2000:app/\n--- !e!tag%21 x\n", "!e!tag%21", "tag:example.com,2000:app/tag!")]
        [InlineData("--- !!str x\n", "!!str", "tag:yaml.org,2002:str")]
        [InlineData("--- !local x\n", "!local", "!local")]
        [InlineData("--- !<tag:verbatim.org,1:x> x\n", "!<tag:verbatim.org,1:x>", "tag:verbatim.org,1:x")]
        [InlineData("--- ! x\n", "!", "!")]
        [InlineData("%TAG !! tag:example.com,2000:\n--- !!int x\n", "!!int", "tag:example.com,2000:int")]
        [InlineData("%TAG ! tag:prim.org,1:\n--- !foo x\n", "!foo", "tag:prim.org,1:foo")]
        [InlineData("%TAG !e! tag:e,1:\n%TAG !e! tag:f,2:\n--- !e!x y\n", "!e!x", "tag:f,2:x")]
        [InlineData("--- !%C3%A9t%C3%A9 x\n", "!%C3%A9t%C3%A9", "!été")]
        public void TagsAreExpanded(string yaml, string written, string expanded)
        {
            Assert.Equal(new[] { (written, expanded) }, Tags(yaml));
        }

        [Fact]
        public void TagsOnEveryNodeKind()
        {
            const string Yaml = "%TAG !e! tag:e,1:\n--- !e!map\n!e!k key: !e!seq [!e!s a, !e!m {!e!x k: v}]\n? !e!key [b]\n: !e!val c\n";
            Assert.Equal(
                new[] { "tag:e,1:map", "tag:e,1:k", "tag:e,1:seq", "tag:e,1:s", "tag:e,1:m", "tag:e,1:x", "tag:e,1:val" },
                Tags(Yaml).ConvertAll(t => t.Expanded));
        }

        [Fact]
        public void HandlesBelongToOneDocument()
        {
            const string Yaml = "%TAG !e! tag:e,1:\n--- !e!a 1\n...\n%TAG !e! tag:f,2:\n--- !e!a 2\n...\n--- !!str 3\n";
            Assert.Equal(new[] { "tag:e,1:a", "tag:f,2:a", "tag:yaml.org,2002:str" }, Tags(Yaml).ConvertAll(t => t.Expanded));

            // A handle declared for an earlier document is undeclared in the next one.
            Assert.ThrowsAny<YamlException>(() => Tags("%TAG !e! tag:e,1:\n--- !e!a 1\n...\n--- !e!a 2\n"));
            Assert.ThrowsAny<YamlException>(() => Tags("--- !e!a 1\n"));
        }

        [Fact]
        public void CoreTagsUseTheExpandedForm()
        {
            // '!!' redefined: '!!int' is not the core int, so a non-integer is fine.
            Assert.Single(Tags("%TAG !! tag:example.com,2000:\n--- !!int x\n"));

            // Another handle with the core prefix: '!y!int' is the core int, so a non-integer is an error.
            Assert.ThrowsAny<YamlException>(() => Tags("%TAG !y! tag:yaml.org,2002:\n--- !y!int x\n"));
            Assert.ThrowsAny<YamlException>(() => Tags("--- !!int x\n"));
            var reader = new Utf8YamlReader("%TAG !y! tag:yaml.org,2002:\n--- !y!str 12\n"u8);
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.String, reader.TokenType);
        }

        [Fact]
        public void NoTag()
        {
            var reader = new Utf8YamlReader("a: b"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Null(reader.GetExpandedTag());
            Assert.True(reader.TryCopyExpandedTag(new byte[4], out int written));
            Assert.Equal(0, written);
        }

        [Fact]
        public void TryCopyExpandedTag()
        {
            var reader = new Utf8YamlReader("%TAG !e! tag:example.com,2000:\n--- !e!foo x\n"u8);
            Assert.True(reader.Read());
            const string Expected = "tag:example.com,2000:foo";

            Assert.False(reader.TryCopyExpandedTag(new byte[Expected.Length - 1], out int written));
            Assert.Equal(0, written);

            byte[] exact = new byte[Expected.Length];
            Assert.True(reader.TryCopyExpandedTag(exact, out written));
            Assert.Equal(Expected.Length, written);
            Assert.Equal(Expected, Encoding.UTF8.GetString(exact));

            // A long tag goes past the internal scratch buffer.
            string longSuffix = new string('s', 300);
            var longReader = new Utf8YamlReader(Encoding.UTF8.GetBytes("%TAG !e! tag:example.com,2000:\n--- !e!" + longSuffix + " x\n"));
            Assert.True(longReader.Read());
            byte[] big = new byte[400];
            Assert.True(longReader.TryCopyExpandedTag(big, out written));
            Assert.Equal("tag:example.com,2000:" + longSuffix, Encoding.UTF8.GetString(big, 0, written));
            Assert.Equal("tag:example.com,2000:" + longSuffix, longReader.GetExpandedTag());
        }

#if NET
        [Fact]
        public void TryCopyExpandedTagDoesNotAllocate()
        {
            byte[] yaml = "%TAG !e! tag:example.com,2000:\n--- !e!foo x\n"u8.ToArray();
            byte[] destination = new byte[64];
            var reader = new Utf8YamlReader(yaml);
            Assert.True(reader.Read());
            Assert.True(reader.TryCopyExpandedTag(destination, out _));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                reader.TryCopyExpandedTag(destination, out _);
            }
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        }
#endif

        [Fact]
        public void ComplexKeyReaderSeesTheHandles()
        {
            var reader = new Utf8YamlReader("%TAG !e! tag:e,1:\n---\n? [!e!x a]\n: v\n"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.Complex, reader.KeyKind);
            Utf8YamlReader key = reader.CreateKeyReader();
            Assert.True(key.Read());
            Assert.True(key.Read());
            Assert.Equal("tag:e,1:x", key.GetExpandedTag());
        }

        // Readers scoped to one value carry the document's handles: a custom converter and a record whose
        // members come before its constructor arguments both read values sliced out of the document.

        public class Inner
        {
            public string? Name { get; set; }
        }

        public class Holder
        {
            public Inner? Value { get; set; }
        }

        private sealed class HolderConverter : YamlConverter<Holder>
        {
            public override Holder Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);
                Assert.True(reader.Read());
                Assert.Equal("Value", reader.GetString());
                Assert.True(reader.Read());
                var holder = new Holder { Value = YamlSerializer.Deserialize<Inner>(ref reader, options) };
                Assert.True(reader.Read());
                return holder;
            }

            public override void Write(Utf8YamlWriter writer, Holder value, YamlSerializerOptions options) => throw new NotSupportedException();
        }

        public record Mixed(string First)
        {
            public string? Before { get; set; }
        }

        [Fact]
        public void ScopedReadersSeeTheHandles()
        {
            const string Yaml = "%TAG !e! tag:e,1:\n---\nValue: !e!inner\n  Name: !e!name n\n";
            var options = new YamlSerializerOptions { Converters = { new HolderConverter() } };
            Assert.Equal("n", YamlSerializer.Deserialize<Holder>(Yaml, options)!.Value!.Name);

            Mixed m = YamlSerializer.Deserialize<Mixed>("%TAG !e! tag:e,1:\n---\nBefore: !e!b b\nFirst: !e!f f\n")!;
            Assert.Equal(("f", "b"), (m.First, m.Before));
        }
    }
}
