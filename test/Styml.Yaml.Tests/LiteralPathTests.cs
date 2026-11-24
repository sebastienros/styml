// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// A string with a line break, written from UTF-16, goes to the literal writer from its UTF-8 form; written from UTF-8
    /// it takes the general path (escape, transcode, unescape). Both give the same bytes, in every position and under
    /// every option that changes the decision.
    /// </summary>
    public class LiteralPathTests
    {
        private static readonly string[] s_texts =
        {
            "a\nb", "a\nb\n", "a\nb\n\n", "\na", "\n\na\n", "a\n", "\n", "\n\n", " a\nb", "a\n b\n", "a\n\n\nb",
            "run: |\n  echo hi\n", "a \nb", "a\n \nb", "a\n  \n", "\ta\nb", "a\n\tb", "a\r\nb", "a\nb\u0001", "a\nb\u007F",
            "a\n\u0085b", "a\n\u2028b", "a\n\uFEFFb", "\u00E9\n\u00F1", "\u65E5\u672C\n\u8A9E", "a\n\"q\"\n\\", "- a\n- b", "key: v\nk2: v2",
            "#c\nd", "a\n" + new string('x', 300) + "\nb", new string('y', 200) + "\n" + new string(' ', 3) + "z",
            "true\nfalse", "~\n", "a\n\u1F600b",
        };

        public static IEnumerable<object[]> Cases() =>
            from text in s_texts
            from shape in new[] { "root", "map", "seq", "nested", "mapInSeq" }
            from options in new[] { "default", "indented", "lenient", "folded", "width", "skip" }
            select new object[] { text, shape, options };

        private static YamlWriterOptions Options(string name) => name switch
        {
            "indented" => new YamlWriterOptions { Style = YamlWriterStyle.Block, IndentSequences = true, IndentSize = 4 },
            "lenient" => new YamlWriterOptions { Style = YamlWriterStyle.Block, QuoteForLenientReaders = true },
            "folded" => new YamlWriterOptions { Style = YamlWriterStyle.Block, MultiLineStringStyle = YamlScalarStyle.Folded },
            "width" => new YamlWriterOptions { Style = YamlWriterStyle.Block, MaxLineWidth = 20 },
            "skip" => new YamlWriterOptions { Style = YamlWriterStyle.Block, SkipValidation = true },
            _ => new YamlWriterOptions { Style = YamlWriterStyle.Block },
        };

        private static string Write(string shape, YamlWriterOptions options, Action<Utf8YamlWriter> value)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options))
            {
                switch (shape)
                {
                    case "root":
                        value(writer);
                        break;
                    case "map":
                        writer.WriteStartObject();
                        writer.WritePropertyName("k");
                        value(writer);
                        writer.WriteString("after", "x");
                        writer.WriteEndObject();
                        break;
                    case "seq":
                        writer.WriteStartArray();
                        value(writer);
                        value(writer);
                        writer.WriteEndArray();
                        break;
                    case "nested":
                        writer.WriteStartObject();
                        writer.WritePropertyName("outer");
                        writer.WriteStartObject();
                        writer.WritePropertyName("inner");
                        value(writer);
                        writer.WriteEndObject();
                        writer.WriteEndObject();
                        break;
                    default:
                        writer.WriteStartArray();
                        writer.WriteStartObject();
                        writer.WritePropertyName("k");
                        value(writer);
                        writer.WriteEndObject();
                        writer.WriteEndArray();
                        break;
                }
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Utf16AndUtf8_WriteTheSameBytes(string text, string shape, string optionsName)
        {
            YamlWriterOptions options = Options(optionsName);
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            if (text.Any(char.IsSurrogate) && !text.Zip(text.Skip(1), (a, b) => char.IsHighSurrogate(a) && char.IsLowSurrogate(b)).Any())
            {
                return;
            }

            string fromUtf16 = Write(shape, options, w => w.WriteStringValue(text));
            string fromUtf8 = Write(shape, options, w => w.WriteStringValue(utf8));
            Assert.Equal(fromUtf8, fromUtf16);

            // And it reads back as the text.
            if (shape == "map")
            {
                Assert.Equal(text, YamlSerializer.Deserialize<Dictionary<string, string>>(fromUtf16)!["k"]);
            }
        }

        [Fact]
        public void AHintOrAnUnpairedSurrogate_TakesTheGeneralPath()
        {
            var options = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" };
            Assert.Equal("k: \"a\\nb\"\n", Write("map", options, w => { w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted); w.WriteStringValue("a\nb"); }).Replace("after: x\n", ""));
            Assert.Equal("k: |-\n  a\n  \uFFFD\nafter: x\n", Write("map", options, w => w.WriteStringValue("a\n\ud800")));
        }
    }
}
