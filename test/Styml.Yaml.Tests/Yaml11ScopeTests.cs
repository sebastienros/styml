// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A stream that declares %YAML 1.1 (or 1.0) has YAML 1.1's directive scope without InheritTagDirectives: a later
    /// document with no directives keeps the previous document's %TAG handles, until a document declares %YAML 1.2 or
    /// later. A document with other directives starts its own table and keeps the version, as YamlDotNet's parser does.
    /// The schema does not change.
    /// </summary>
    public class Yaml11ScopeTests
    {
        private static readonly YamlReaderOptions s_defaults = new() { AllowMultipleValues = true };

        private static List<string?> Tags(string yaml, YamlReaderOptions? options = null)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options ?? s_defaults);
            var tags = new List<string?>();
            while (reader.Read())
            {
                if (!reader.Tag.IsEmpty)
                {
                    tags.Add(reader.GetExpandedTag());
                }
            }

            return tags;
        }

        [Fact]
        public void Yaml11_KeepsTheHandles()
        {
            Assert.Equal(new[] { "tag:a,1:x", "tag:a,1:y", "tag:a,1:z" }, Tags("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n...\n!a!z 3\n"));
            Assert.Equal(new[] { "tag:a,1:x", "tag:a,1:y" }, Tags("%YAML 1.0\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n"));
            Assert.Equal(new[] { "tag:a,1:x", "tag:a,1:y" }, Tags("%YAML 1.01\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n"));
        }

        [Fact]
        public void WhatEndsIt()
        {
            // A later %YAML 1.2, and any later document that declares directives starts its own table.
            Assert.ThrowsAny<YamlException>(() => Tags("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.2\n--- !a!y 2\n"));
            Assert.ThrowsAny<YamlException>(() => Tags("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%TAG !b! tag:b,1:\n--- !a!y 2\n"));

            // The version stays with a document that declares only %TAG, so the next document inherits that table.
            Assert.Equal(new[] { "tag:a,1:x", "tag:b,1:y", "tag:b,1:z" }, Tags("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%TAG !b! tag:b,1:\n--- !b!y 2\n--- !b!z 3\n"));

            // Nothing turns it on but a %YAML 1.0 or 1.1: 1.2, 1.3, or no directive, and the YAML 1.1 schema.
            foreach (string version in new[] { "%YAML 1.2\n", "%YAML 1.3\n", "%YAML 1.10\n", "" })
            {
                Assert.ThrowsAny<YamlException>(() => Tags(version + "%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n"));
            }

            Assert.ThrowsAny<YamlException>(() => Tags("%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n", new YamlReaderOptions { AllowMultipleValues = true, Schema = YamlSchemaKind.Yaml11 }));
        }

        [Fact]
        public void ALaterDocumentMayDeclareIt()
        {
            Assert.Equal(new[] { "tag:a,1:x", "tag:a2,1:y", "tag:a2,1:z" }, Tags("%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.1\n%TAG !a! tag:a2,1:\n--- !a!y 2\n--- !a!z 3\n"));
            Assert.Equal(new[] { "tag:a,1:x", "tag:a,1:y" }, Tags("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!y 2\n"));
        }

        [Fact]
        public void TheSchemaDoesNotChange()
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("%YAML 1.1\n---\na: yes\nb: 010\n"));
            var tokens = new List<YamlTokenType>();
            while (reader.Read())
            {
                tokens.Add(reader.TokenType);
            }

            Assert.Equal(new[] { YamlTokenType.StartObject, YamlTokenType.PropertyName, YamlTokenType.String, YamlTokenType.PropertyName, YamlTokenType.Number, YamlTokenType.EndObject }, tokens);
            Assert.Equal(10, YamlSerializer.Deserialize<Dictionary<string, int>>("%YAML 1.1\n---\nb: 010\n")!["b"]);
        }

        private const string Stream = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &1000\nGameObject:\n  m_Name: P\n--- !u!4 &1001\nTransform:\n  m: {fileID: 1000}\n";

        [Fact]
        public void PartialReadsSegmentsAndTheDom()
        {
            byte[] input = Encoding.UTF8.GetBytes(Stream);
            ChunkingTests.AssertEverySplit(input, s_defaults);
            ChunkingTests.AssertEverySequence(input, s_defaults);

            var reader = new Utf8YamlReader(input, s_defaults);
            var expanded = new List<string?>();
            while (reader.Read())
            {
                expanded.Add(YamlElement.ParseValue(ref reader).GetExpandedTag());
            }

            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, expanded);

            reader = new Utf8YamlReader(input, s_defaults);
            reader.Read();
            reader.Skip();
            reader.Read();
            Assert.Equal(1000, YamlNode.Parse(ref reader)!["Transform"]!["m"]!["fileID"]!.GetValue<int>());

            reader = new Utf8YamlReader(input, s_defaults);
            reader.Read();
            reader.Skip();
            reader.Read();
            Assert.Equal("tag:unity3d.com,2011:4", Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(ref reader)).GetExpandedTag());

            using var stream = new MemoryStream(input);
            IAsyncEnumerator<YamlNode?> items = YamlSerializer.DeserializeAsyncEnumerable<YamlNode>(stream, topLevelValues: true, new YamlSerializerOptions { DefaultBufferSize = 16 }).GetAsyncEnumerator();
            int count = 0;
            while (items.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                count++;
            }

            Assert.Equal(2, count);
        }

        [Fact]
        public void TheWriter_FollowsTheSameRule()
        {
            var readerOptions = new YamlReaderOptions { AllowMultipleValues = true };
            var elements = new List<YamlElement>();
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Stream), readerOptions);
            while (reader.Read())
            {
                elements.Add(YamlElement.ParseValue(ref reader));
            }

            string Write(bool yaml11)
            {
                var buffer = new ArrayBufferWriter<byte>();
                using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
                {
                    if (yaml11)
                    {
                        writer.WriteDirective("%YAML 1.1");
                    }

                    writer.WriteDirective("%TAG !u! tag:unity3d.com,2011:");
                    foreach (YamlElement element in elements)
                    {
                        writer.WriteDocumentStart();
                        element.WriteTo(writer);
                    }
                }

                return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
            }

            // After %YAML 1.1 the second document's tag is written in its shorthand, without the writer option, and the
            // output reads back with default options to the same expanded tags; without it, the stand-alone form.
            string withVersion = Write(yaml11: true);
            Assert.Equal(Stream, withVersion);
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, Tags(withVersion));

            string withoutVersion = Write(yaml11: false);
            Assert.Contains("--- !<tag:unity3d.com,2011:4> &1001\n", withoutVersion);
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, Tags(withoutVersion));
        }
    }
}
