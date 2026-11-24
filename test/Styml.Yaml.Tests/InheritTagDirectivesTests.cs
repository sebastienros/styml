// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// InheritTagDirectives: YAML 1.1's directive scope (section 7.4). A document with no directives keeps the previous
    /// document's %TAG handles; a document with any directive (%YAML included) starts from its own. Off, each document
    /// has only its own handles (YAML 1.2, section 9.1), as before.
    /// </summary>
    public class InheritTagDirectivesTests
    {
        private const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
        private const string Unity = Header + "--- !u!1 &1000\nGameObject:\n  m_Name: Player\n--- !u!4 &1001\nTransform:\n  m_GameObject: {fileID: 1000}\n";

        private static readonly YamlReaderOptions s_on = new() { AllowMultipleValues = true, InheritTagDirectives = true };
        private static readonly YamlReaderOptions s_off = new() { AllowMultipleValues = true };

        private static List<string?> RootTags(string yaml, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tags = new List<string?>();
            while (reader.Read())
            {
                if (reader.CurrentDepth == 0 && reader.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray or YamlTokenType.String or YamlTokenType.Number or YamlTokenType.Null)
                {
                    tags.Add(reader.GetExpandedTag());
                    if (reader.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
                    {
                        reader.Skip();
                    }
                }
            }

            return tags;
        }

        [Fact]
        public void Off_ALaterDocumentHasOnlyTheDefaultHandles()
        {
            YamlException error = Assert.ThrowsAny<YamlException>(() => RootTags(Unity, s_off));
            Assert.Contains("not declared", error.Message);
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:yaml.org,2002:str" }, RootTags(Header + "--- !u!1 a\n--- !!str b\n", s_off));
        }

        [Fact]
        public void On_ALaterDocumentKeepsTheHandles()
        {
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, RootTags(Unity, s_on));

            // After a document end marker, and in a bare document.
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:2", "tag:unity3d.com,2011:3" },
                RootTags(Header + "--- !u!1 a\n...\n--- !u!2 b\n...\n!u!3 c\n", s_on));

            // The tag as written stays the written form.
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Unity), s_on);
            reader.Read();
            reader.Skip();
            reader.Read();
            Assert.Equal("!u!4", Encoding.UTF8.GetString(reader.Tag.ToArray()));
        }

        [Fact]
        public void ADocumentWithDirectives_StartsFromItsOwn()
        {
            // YAML 1.1 section 7.4: "all directives of previous documents, if any, are ignored", as YamlDotNet reads.
            const string twoHandles = "%TAG !a! tag:a,1:\n%TAG !b! tag:b,1:\n--- !a!x 1\n...\n%TAG !a! tag:a2,1:\n--- !a!y 2\n--- !a!z 3\n";
            Assert.Equal(new[] { "tag:a,1:x", "tag:a2,1:y", "tag:a2,1:z" }, RootTags(twoHandles, s_on));
            Assert.Contains("not declared", Assert.ThrowsAny<YamlException>(() => RootTags(twoHandles.Replace("!a!y", "!b!y"), s_on)).Message);

            // %YAML is a directive too.
            Assert.ThrowsAny<YamlException>(() => RootTags(Header + "--- !u!1 a\n...\n%YAML 1.1\n--- !u!2 b\n", s_on));
        }

        [Fact]
        public void YamlOneOne_DoesNotTurnItOn()
        {
            Assert.ThrowsAny<YamlException>(() => RootTags(Unity, new YamlReaderOptions { AllowMultipleValues = true, Schema = YamlSchemaKind.Yaml11 }));
        }

        [Fact]
        public void PartialReadsAndSegments_KeepTheTableAcrossTheBoundary()
        {
            byte[] input = Encoding.UTF8.GetBytes(Unity + "--- !u!114 &1002\nMonoBehaviour: {m_Enabled: 1}\n");
            ChunkingTests.AssertEverySplit(input, s_on);
            ChunkingTests.AssertEverySequence(input, s_on);
        }

        [Fact]
        public void TheDomReadsEachDocument()
        {
            byte[] input = Encoding.UTF8.GetBytes(Unity);

            var reader = new Utf8YamlReader(input, s_on);
            var elements = new List<YamlElement>();
            while (reader.Read())
            {
                elements.Add(YamlElement.ParseValue(ref reader));
            }

            Assert.Equal(new[] { "tag:unity3d.com,2011:4" }, elements.Skip(1).Select(e => e.GetExpandedTag()));
            Assert.Equal(new[] { "1000", "1001" }, elements.Select(e => e.GetAnchor()));
            Assert.Equal(1000, elements[1].GetProperty("Transform").GetProperty("m_GameObject").GetProperty("fileID").GetInt32());

            reader = new Utf8YamlReader(input, s_on);
            var nodes = new List<YamlNode>();
            while (reader.Read())
            {
                nodes.Add(YamlNode.Parse(ref reader)!);
            }

            Assert.Equal("Player", (string)nodes[0]["GameObject"]!["m_Name"]!);
            Assert.Equal(2, nodes.Count);
        }

        [Fact]
        public void TheSerializer_ReadsAStream()
        {
            var options = new YamlSerializerOptions { InheritTagDirectives = true, DefaultBufferSize = 16 };
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Unity));
            List<YamlNode?> nodes = Drain(YamlSerializer.DeserializeAsyncEnumerable<YamlNode>(stream, topLevelValues: true, options));
            Assert.Equal(2, nodes.Count);
            Assert.Equal(1000, (int)nodes[1]!["Transform"]!["m_GameObject"]!["fileID"]!);

            // Off, the second document's tag is refused.
            using var again = new MemoryStream(Encoding.UTF8.GetBytes(Unity));
            Assert.ThrowsAny<YamlException>(() => Drain(YamlSerializer.DeserializeAsyncEnumerable<YamlNode>(again, topLevelValues: true)));
        }

        private static List<T?> Drain<T>(IAsyncEnumerable<T?> source)
        {
            var list = new List<T?>();
            IAsyncEnumerator<T?> e = source.GetAsyncEnumerator();
            try
            {
                while (e.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    list.Add(e.Current);
                }
            }
            finally
            {
                e.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            return list;
        }

        [Fact]
        public void TheDocumentOptions_CarryIt()
        {
            Assert.True(new YamlDocumentOptions { InheritTagDirectives = true }.GetReaderOptions().InheritTagDirectives);
            Assert.True(new YamlSerializerOptions { InheritTagDirectives = true }.GetReaderOptions().InheritTagDirectives);
            Assert.False(new YamlSerializerOptions().GetReaderOptions().InheritTagDirectives);
        }
    }
}
