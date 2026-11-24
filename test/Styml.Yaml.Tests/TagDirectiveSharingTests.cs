// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A document's <c>%TAG</c> table is shared, not copied, by what reads under it: later documents under YAML 1.1's
    /// scope, and the values read from the reader into the DOM. No later header changes it, since each header starts
    /// from no table.
    /// </summary>
    public class TagDirectiveSharingTests
    {
        private const int Handles = 50_000;

        private static readonly YamlWriterOptions s_write = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };

        // "%YAML 1.1" and `handles` handles declared once, then `documents` documents that declare none, each with a
        // root tag and a value tag through handles spread over the table.
        private static (List<string> Directives, string Yaml) Stream(int handles, int documents)
        {
            var directives = new List<string> { "%YAML 1.1" };
            for (int i = 0; i < handles; i++)
            {
                directives.Add("%TAG !t" + i + "! tag:x," + i + ":");
            }

            var yaml = new StringBuilder(handles * 28 + documents * 40);
            foreach (string directive in directives)
            {
                yaml.Append(directive).Append('\n');
            }

            for (int d = 0; d < documents; d++)
            {
                int root = (int)((long)d * 7919 % handles);
                int value = handles - 1 - root;
                yaml.Append("--- !t").Append(root).Append("!d").Append(d).Append("\nk: !t").Append(value).Append("!v ").Append(d).Append('\n');
            }

            return (directives, yaml.ToString());
        }

        // Reads every document through YamlNode.Parse(ref reader) and writes it back after the stream's directives.
        private static void ReadAndWriteBack(List<string> directives, byte[] yaml, ArrayBufferWriter<byte> output, List<string?>? rootTags)
        {
            using var writer = new Utf8YamlWriter(output, s_write);
            foreach (string directive in directives)
            {
                writer.WriteDirective(directive);
            }

            var reader = new Utf8YamlReader(yaml, new YamlReaderOptions { AllowMultipleValues = true });
            while (reader.Read())
            {
                rootTags?.Add(reader.GetExpandedTag());
                YamlNode node = YamlNode.Parse(ref reader)!;
                writer.WriteDocumentStart();
                node.WriteTo(writer);
            }
        }

        [Fact]
        public void ManyHandlesDeclaredOnce_ManyDocumentsReadAndWrittenBack()
        {
            const int Documents = 4_000;
            (List<string> directives, string yaml) = Stream(Handles, Documents);
            var output = new ArrayBufferWriter<byte>();
            var rootTags = new List<string?>();
            ReadAndWriteBack(directives, Encoding.UTF8.GetBytes(yaml), output, rootTags);

            Assert.Equal(yaml, Encoding.UTF8.GetString(output.WrittenSpan.ToArray()));
            Assert.Equal(Documents, rootTags.Count);
            for (int d = 0; d < Documents; d += 397)
            {
                Assert.Equal("tag:x," + (long)d * 7919 % Handles + ":d" + d, rootTags[d]);
            }
        }

#if NET
        [Fact]
        public void TheCostOfADocument_DoesNotGrowWithTheInheritedTable()
        {
            // A copy of the table per document would cost each document the size of the table: about a megabyte at
            // 50,000 handles. The cost of the second half of the documents, with the header's cost taken out, is the
            // same under one handle as under 50,000. Allocations, not time, so that a slow runner cannot fail it.
            long Cost(int handles, int documents)
            {
                (List<string> directives, string yaml) = Stream(handles, documents);
                byte[] input = Encoding.UTF8.GetBytes(yaml);
                var output = new ArrayBufferWriter<byte>(input.Length + 1024);
                ReadAndWriteBack(directives, input, new ArrayBufferWriter<byte>(input.Length + 1024), rootTags: null);
                long before = GC.GetAllocatedBytesForCurrentThread();
                ReadAndWriteBack(directives, input, output, rootTags: null);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(input.Length, output.WrittenCount);
                return allocated;
            }

            long small = Cost(1, 4_000) - Cost(1, 2_000);
            long large = Cost(Handles, 4_000) - Cost(Handles, 2_000);
            Assert.True(small > 0);
            Assert.True(large < small * 2 + 64 * 1024, $"2,000 more documents cost {large} bytes under {Handles} handles, {small} under one.");
        }
#endif

        [Fact]
        public void ALaterHeader_DoesNotChangeAnEarlierDocumentsTable()
        {
            const string Yaml = "%YAML 1.1\n%TAG !h! tag:a,1:\n--- !h!x 1\n--- !h!y 2\n...\n"
                + "%TAG !h! tag:b,2:\n%TAG !k! tag:k,3:\n--- !h!z 3\n--- !k!w 4\n";
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Yaml), new YamlReaderOptions { AllowMultipleValues = true });
            var elements = new List<YamlElement>();
            while (reader.Read())
            {
                elements.Add(YamlElement.ParseValue(ref reader));
            }

            // Read after the whole stream: the second document still has the first header's handle, and writes back in
            // its shorthand under that header.
            Assert.Equal(new[] { "tag:a,1:x", "tag:a,1:y", "tag:b,2:z", "tag:k,3:w" }, elements.ConvertAll(e => e.GetExpandedTag()));

            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(output, s_write))
            {
                writer.WriteDirective("%YAML 1.1");
                writer.WriteDirective("%TAG !h! tag:a,1:");
                writer.WriteDocumentStart();
                elements[0].WriteTo(writer);
                writer.WriteDocumentStart();
                elements[1].WriteTo(writer);
            }

            Assert.Equal("%YAML 1.1\n%TAG !h! tag:a,1:\n--- !h!x 1\n--- !h!y 2\n", Encoding.UTF8.GetString(output.WrittenSpan.ToArray()));
        }
    }
}
