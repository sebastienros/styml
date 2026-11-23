// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4p: the block style's byte-identity checks, beside
// G2's 'hash' for the JSON-compatible style. Each prints one line per output: class, row, input, length and the first
// 16 hex digits of its SHA-256, so that two folders of the dll swap compare by diff.
//   dotnet Styml.Benchmarks.dll hash-block
//   dotnet Styml.Benchmarks.dll quote-dump <strings.json>     (a JSON array of strings)
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Styml.Benchmarks
{
    internal static class BlockCheck
    {
        /// <summary>
        /// Every block row's output: `WriteBlock`'s default and `QuoteForLenientReaders` rows, both block serialize
        /// classes with the option off and on, over the twelve documents, and the non-ASCII rows.
        /// </summary>
        public static int HashBlock(TextWriter output)
        {
            WriteLibraries.SetSwitch();
            foreach (string name in Corpus.Names)
            {
                var write = new WriteBlock { Document = name };
                write.Setup();
                Print(output, "WriteBlock", "Styml_Block", name, write.Output(nameof(WriteBlock.Styml_Block)));
                Print(output, "WriteBlock", "Styml_Block_Lenient", name, write.Output(nameof(WriteBlock.Styml_Block_Lenient)));
                Print(output, "WriteBlock", "Styml_Block_ConstKeys", name, write.Output(nameof(WriteBlock.Styml_Block_ConstKeys)));
                Print(output, "WriteBlock", "Styml_Block_ConstValues", name, write.Output(nameof(WriteBlock.Styml_Block_ConstValues)));
                object graph = Libraries.Json(Corpus.Get(name).Json, Corpus.Get(name).Family)!;
                Family family = Corpus.Get(name).Family;
                Print(output, "SerializeReflectionBlock", "Styml_Block", name, WriteLibraries.Styml(graph, family, BlockLibraries.BlockOptions));
                Print(output, "SerializeReflectionBlock", "Styml_Block_Lenient", name, WriteLibraries.Styml(graph, family, BlockLibraries.BlockLenientOptions));
                Print(output, "SerializeSourceGenBlock", "Styml_Block", name, WriteLibraries.StymlGeneratedWrite(graph, family, BlockLibraries.BlockGenerated));
            }

            foreach (string set in WriteBlockNonAscii.Documents)
            {
                var rows = new WriteBlockNonAscii { Document = set };
                rows.Setup();
                foreach (string method in new[] { "Styml_Block", "Styml_Block_Utf8", "Styml_Block_Lenient", "Styml_Block_Lenient_Utf8" })
                {
                    Print(output, "WriteBlockNonAscii", method, set, rows.Output(method));
                }
            }
            return 0;
        }

        // 'hash-block-mixed': the hashes of the two mixed rows of WriteBlockNonAscii (M4p b's
        // verification rows), which hash-block's list does not include.
        public static int HashBlockMixed(TextWriter output)
        {
            foreach (string set in WriteBlockNonAscii.Documents)
            {
                var rows = new WriteBlockNonAscii { Document = set };
                rows.Setup();
                foreach (string method in new[] { "Styml_Block_AsciiNames", "Styml_Block_EncodedAsciiNames" })
                {
                    Print(output, "WriteBlockNonAscii", method, set, rows.Output(method));
                }
            }

            return 0;
        }

        /// <summary>
        /// The quoting decisions over a list of strings: each written in the five positions of
        /// BlockQuotingSpecPatternTests (block item, block value, block key, flow item, flow key) and the serializer's (a
        /// value after a pre-encoded name), through the UTF-8 and
        /// the UTF-16 entry points, with QuoteForLenientReaders off and on, one document per combination.
        /// </summary>
        public static int QuoteDump(string path, TextWriter output)
        {
            string[] strings = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path))!;
            byte[][] utf8 = Array.ConvertAll(strings, System.Text.Encoding.UTF8.GetBytes);
            output.WriteLine($"# {strings.Length} strings from {Path.GetFileName(path)}");
            var buffer = new ArrayBufferWriter<byte>(1 << 20);
            foreach (bool lenient in new[] { false, true })
            {
                var options = new System.Text.Yaml.YamlWriterOptions { Style = System.Text.Yaml.YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = lenient };
                foreach (string position in new[] { "Item", "Value", "Key", "FlowItem", "FlowKey", "EncodedValue", "NameOfPair" })
                {
                    foreach (bool fromUtf8 in new[] { true, false })
                    {
                        buffer.ResetWrittenCount();
                        using (var w = new System.Text.Yaml.Utf8YamlWriter(buffer, options))
                        {
                            Write(w, position, strings, utf8, fromUtf8);
                        }
                        Print(output, lenient ? "lenient" : "default", position, fromUtf8 ? "utf8" : "utf16", buffer.WrittenSpan.ToArray());
                    }
                }
            }
            return 0;
        }

        private static readonly System.Text.Yaml.YamlEncodedText s_encodedName = System.Text.Yaml.YamlEncodedText.Encode("k");

        private static void Write(System.Text.Yaml.Utf8YamlWriter w, string position, string[] strings, byte[][] utf8, bool fromUtf8)
        {
            if (position == "Key")
            {
                w.WriteStartObject();
            }
            else
            {
                w.WriteStartArray();
            }

            for (int i = 0; i < strings.Length; i++)
            {
                switch (position)
                {
                    case "Item":
                        if (fromUtf8) { w.WriteStringValue(utf8[i]); } else { w.WriteStringValue(strings[i]); }
                        break;
                    case "Value":
                        w.WriteStartObject();
                        if (fromUtf8) { w.WriteString("k"u8, utf8[i]); } else { w.WriteString("k", strings[i]); }
                        w.WriteEndObject();
                        break;
                    case "Key":
                        if (fromUtf8) { w.WritePropertyName(utf8[i]); } else { w.WritePropertyName(strings[i]); }
                        w.WriteNumberValue(0);
                        break;
                    case "NameOfPair":
                        // The name of WriteString(name, value), which judges both in one call (M4p, candidate b).
                        w.WriteStartObject();
                        if (fromUtf8) { w.WriteString(utf8[i], "v"u8); } else { w.WriteString(strings[i], "v"); }
                        w.WriteEndObject();
                        break;
                    case "EncodedValue":
                        // The serializer's form: a pre-encoded name and the value (M4p, candidate b).
                        w.WriteStartObject();
                        if (fromUtf8) { w.WriteString(s_encodedName, utf8[i]); } else { w.WriteString(s_encodedName, strings[i]); }
                        w.WriteEndObject();
                        break;
                    case "FlowItem":
                        w.WriteCollectionStyle(System.Text.Yaml.YamlCollectionStyle.Flow);
                        w.WriteStartArray();
                        if (fromUtf8) { w.WriteStringValue(utf8[i]); } else { w.WriteStringValue(strings[i]); }
                        w.WriteEndArray();
                        break;
                    case "FlowKey":
                        w.WriteCollectionStyle(System.Text.Yaml.YamlCollectionStyle.Flow);
                        w.WriteStartObject();
                        if (fromUtf8) { w.WritePropertyName(utf8[i]); } else { w.WritePropertyName(strings[i]); }
                        w.WriteNumberValue(0);
                        w.WriteEndObject();
                        break;
                }
            }

            if (position == "Key")
            {
                w.WriteEndObject();
            }
            else
            {
                w.WriteEndArray();
            }
        }

        private static void Print(TextWriter output, string className, string method, string input, byte[] bytes) =>
            output.WriteLine($"{className} {method} {input} {bytes.Length} {Convert.ToHexString(SHA256.HashData(bytes))[..16]}");
    }
}
