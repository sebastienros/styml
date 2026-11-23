// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M4p, candidate b: <see cref="YamlWriterHelper.ScanUnescapedAscii"/>
    /// stands for the block encoder's escaping scan and <see cref="YamlWriterHelper.ScanEscapedText"/> together. For every
    /// text it judges, the encoder finds nothing to escape and the block scan gives the same answer; it declines
    /// (<c>Unknown</c>) exactly the texts with a <c>"</c>, a <c>\</c> or a byte above ASCII. The UTF-16 form gives the
    /// same answer on every text, and on a text it declines the escaping scan resumed at its stop finds what it finds from
    /// the start.
    /// </summary>
    public class BlockOneScanTests
    {
        // Each byte the scan decides on: the indicators that start a text, ':' and '#' with and without a space or tab
        // beside them, '-', '?' and '.' for the edges and the document markers, C0 controls, DEL, the two escapes, a
        // non-ASCII letter (two bytes), and a letter.
        private const string Alphabet = " :#-?.\t\n\x7F\"\\é!&a";

        // 15^1 + ... + 15^5 = 813,615 texts.
        [Fact]
        public void AgreesWithBothScans_UpToFiveCharacters()
        {
            var failures = new List<string>();
            int judged = 0;
            var current = new List<string> { "" };
            Check("", failures, ref judged);
            for (int length = 1; length <= 5; length++)
            {
                var next = new List<string>(current.Count * Alphabet.Length);
                foreach (string prefix in current)
                {
                    foreach (char c in Alphabet)
                    {
                        string s = prefix + c;
                        next.Add(s);
                        Check(s, failures, ref judged);
                    }
                }

                current = next;
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)) + $"\n({failures.Count} in all)");
            Assert.True(judged > 100_000, $"{judged} texts judged");
        }

        [Theory]
        [InlineData("--- a")]
        [InlineData("...")]
        [InlineData("a: b")]
        [InlineData("a:")]
        [InlineData("a:b")]
        [InlineData("a #b")]
        [InlineData("a#b")]
        [InlineData("- a")]
        [InlineData("-a")]
        [InlineData("? a")]
        [InlineData("?a")]
        [InlineData("-\ta")]
        [InlineData("a ")]
        [InlineData(" a")]
        [InlineData("http://example.com/a#b")]
        [InlineData("line one\nline two")]
        [InlineData("tab\there")]
        [InlineData("a\x7F")]
        [InlineData("a: b \"c\"")]
        [InlineData("a\\b")]
        [InlineData("Reykjavík")]
        [InlineData("a: Reykjavík")]
        [InlineData("")]
        [InlineData("true")]
        [InlineData("1e3")]
        [InlineData("~")]
        public void AgreesWithBothScans_Examples(string text)
        {
            var failures = new List<string>();
            int judged = 0;
            Check(text, failures, ref judged);
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // Unpaired and paired surrogates (in a fact: xunit's data serialisation does not keep a lone surrogate).
        [Fact]
        public void AgreesWithBothScans_Surrogates()
        {
            var failures = new List<string>();
            int judged = 0;
            foreach (string text in new[] { "a\uD83D", "\uDE00a", "a\uD83D\uDE00", "a: \uD83D", "\uD83D\uDE00: b" })
            {
                Check(text, failures, ref judged);
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // Invalid UTF-8 (which strings cannot hold): declined, and the resumed escaping scan reports the invalid byte as the
        // scan from the start does.
        [Fact]
        public void InvalidUtf8_ResumedScanAgrees()
        {
            byte[][] texts =
            [
                [0x61, 0xFF], [0xFF], [0x61, 0xC3], [0xC3, 0x28, 0x22], [0x61, 0x3A, 0x20, 0xE2, 0x82], [0x61, 0x22, 0xFF],
                [0x61, 0xED, 0xA0, 0x80], [0x61, 0xC3, 0xA9, 0x5C], [0x61, 0xF4, 0x90, 0x80, 0x80], [0x23, 0xC0, 0xAF],
            ];
            foreach (byte[] bytes in texts)
            {
                Assert.Equal(YamlWriterHelper.BlockScan.Unknown, YamlWriterHelper.ScanUnescapedAscii(bytes, out int stop));
                Assert.Equal(YamlWriterHelper.NeedsEscaping(bytes, YamlBlockEncoder.Instance), YamlWriterHelper.BlockEncoderEscapeIndex(bytes, stop));
            }
        }

        // Longer texts, so that the vectorised searches reach their wide paths and their tails.
        [Fact]
        public void AgreesWithBothScans_LongTexts()
        {
            var failures = new List<string>();
            int judged = 0;
            string[] inserts = ["", ":", ": ", " #", "#", "\t", "\n", "\x7F", "\"", "\\", "é"];
            foreach (int length in new[] { 15, 16, 17, 31, 32, 33, 63, 64, 65, 200 })
            {
                foreach (string insert in inserts)
                {
                    foreach (int at in new[] { 0, 1, length / 2, length - 1, length })
                    {
                        string filler = new string('a', length);
                        Check(filler.Insert(Math.Min(at, filler.Length), insert), failures, ref judged);
                        Check(filler.Insert(Math.Min(at, filler.Length), insert) + " ", failures, ref judged);
                        Check(": " + filler.Insert(Math.Min(at, filler.Length), insert), failures, ref judged);
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)) + $"\n({failures.Count} in all)");
        }

        private static void Check(string text, List<string> failures, ref int judged)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            YamlWriterHelper.BlockScan one = YamlWriterHelper.ScanUnescapedAscii(bytes, out int stop);

            // The UTF-16 form judges the same texts, the same way (the writer transcodes before its block scan).
            YamlWriterHelper.BlockScan chars = YamlWriterHelper.ScanUnescapedAscii(text.AsSpan(), out int charStop);
            if (chars != one)
            {
                failures.Add($"{Escape(text)}: UTF-8 scan {one}, UTF-16 scan {chars}");
            }

            bool declines = text.Any(c => c is '"' or '\\' || c >= 0x80);
            if (declines)
            {
                if (one != YamlWriterHelper.BlockScan.Unknown || chars != YamlWriterHelper.BlockScan.Unknown)
                {
                    failures.Add($"{Escape(text)}: judged {one} and {chars}, but it holds a quote, a backslash or non-ASCII");
                    return;
                }

                // The escaping scan, resumed at the stop, finds what it finds from the start.
                int expected = YamlWriterHelper.NeedsEscaping(bytes, YamlBlockEncoder.Instance);
                int resumed = YamlWriterHelper.BlockEncoderEscapeIndex(bytes, stop);
                int expectedChars = YamlWriterHelper.NeedsEscaping(text.AsSpan(), YamlBlockEncoder.Instance);
                int resumedChars = YamlWriterHelper.BlockEncoderEscapeIndex(text.AsSpan(), charStop);
                if (resumed != expected || resumedChars != expectedChars)
                {
                    failures.Add($"{Escape(text)}: escaping scan {expected} (UTF-8) and {expectedChars} (UTF-16); resumed at {stop} and {charStop}: {resumed} and {resumedChars}");
                }

                return;
            }

            judged++;
            int escape = YamlWriterHelper.NeedsEscaping(bytes, YamlBlockEncoder.Instance);
            YamlWriterHelper.BlockScan two = YamlWriterHelper.ScanEscapedText(bytes);
            if (one == YamlWriterHelper.BlockScan.Unknown || escape != -1 || one != two)
            {
                failures.Add($"{Escape(text)}: one scan {one}, escaping scan {escape}, block scan {two}");
            }
        }

        private static string Escape(string s) =>
            "\"" + string.Concat(s.Select(c => c < 0x20 || c == 0x7F ? $"\\x{(int)c:X2}" : c.ToString())) + "\"";
    }
}
