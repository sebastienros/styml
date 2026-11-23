// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Oracle 4: every yaml-test-suite case Styml reads is read, its
    /// tokens are written in block style with <see cref="Utf8YamlWriter"/> (anchors, tags, aliases, collection keys,
    /// flow collections and scalar styles through M4b's members), and the output is read again. The two reads' events
    /// must be equal (structure, anchors, tags, token types and text), under the writer's own rules only:
    /// <list type="bullet">
    /// <item>
    /// a string may come back in another style: the writer quotes what the scalar rules require, and a style hint falls back
    /// where the string does not allow it;
    /// </item>
    /// <item>a null reads as one event whatever its spelling (the writer writes <c>null</c>, decision 7).</item>
    /// </list>
    /// Tags are written verbatim (the reader does not expose the %TAG directives they were written with). Cases that
    /// differ for a reason of design are on writer-differences.txt, each with its reason; a listed case must still
    /// differ, as with the reader's lists. The test also counts the scalars written in their source's style, so that a
    /// style that is never written cannot pass unseen.
    /// </summary>
    public class YamlTestSuiteWriterTests
    {
        public static IEnumerable<object[]> Cases() =>
            YamlTestSuite.AllCaseIds().Where(ReadToday).Select(id => new object[] { id });

        // The cases the reader reads with the expected trace.
        private static bool ReadToday(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            return !c.IsError && YamlTestSuite.ListedAs(id, out _) == YamlTestSuite.ListKind.None
                && YamlTestSuite.Run(c, out _, out _) == YamlTestSuite.Outcome.Passed;
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ReadWriteRead(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            string? difference = Compare(c.Input, comments: false, out string written, out _);

            // With the comments as tokens too: every case reads with them since M5.0 (6HB6 and K3WX did not before:
            // comment tokens in flow collections).
            string? withComments = difference is null ? Compare(c.Input, comments: true, out written, out _) : null;

            // And with the other layout: four columns, indented sequences, expanded items, folding at 20.
            string? otherLayout = difference is null && withComments is null ? Compare(c.Input, comments: false, out written, out _, s_otherLayout) : null;
            string? listed = ListedReason(id);
            if (listed is not null)
            {
                Assert.True(difference is not null || withComments is not null || otherLayout is not null,
                    $"{id} now writes back the same events; remove it from writer-differences.txt ({listed}).");
                return;
            }

            Assert.True(difference is null, $"{id} ({c.Title}): {difference}\nWritten:\n{written}");
            Assert.True(withComments is null, $"{id} ({c.Title}), with comments: {withComments}\nWritten:\n{written}");
            Assert.True(otherLayout is null, $"{id} ({c.Title}), other layout: {otherLayout}\nWritten:\n{written}");
        }

        /// <summary>
        /// The styles of the scalars written in their source's style, over every case: each style of the source must be
        /// written as itself for at least some scalars (the counts are reported).
        /// </summary>
        [Fact]
        public void EveryStyle_IsWrittenAsItself()
        {
            var kept = new Dictionary<YamlScalarStyle, int>();
            foreach (object[] row in Cases())
            {
                string id = (string)row[0];
                if (ListedReason(id) is not null)
                {
                    continue;
                }

                Compare(YamlTestSuite.Load(id).Input, comments: false, out _, out Dictionary<YamlScalarStyle, int> counts);
                foreach (KeyValuePair<YamlScalarStyle, int> pair in counts)
                {
                    kept[pair.Key] = kept.TryGetValue(pair.Key, out int n) ? n + pair.Value : pair.Value;
                }
            }

            foreach (YamlScalarStyle style in new[] { YamlScalarStyle.Plain, YamlScalarStyle.SingleQuoted, YamlScalarStyle.DoubleQuoted, YamlScalarStyle.Literal, YamlScalarStyle.Folded })
            {
                // Pinned below the counts of M4b (Plain 860, SingleQuoted 20, DoubleQuoted 89, Literal 29, Folded 26).
                int minimum = style switch { YamlScalarStyle.Plain => 800, YamlScalarStyle.DoubleQuoted => 80, _ => 15 };
                Assert.True(kept.TryGetValue(style, out int n) && n >= minimum, $"{style}: {(kept.TryGetValue(style, out int m) ? m : 0)} scalars written as in the source.");
            }
        }

        [Fact]
        public void ListIsWellFormed()
        {
            var ids = new HashSet<string>(YamlTestSuite.AllCaseIds(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string id, string reason) in ListEntries())
            {
                Assert.True(ids.Contains(id), $"writer-differences.txt: '{id}' is not a yaml-test-suite case.");
                Assert.True(seen.Add(id), $"writer-differences.txt: '{id}' is listed more than once.");
                Assert.False(string.IsNullOrWhiteSpace(reason), $"writer-differences.txt: '{id}' has no reason.");
                Assert.True(ReadToday(id), $"writer-differences.txt: '{id}' is not a case the reader reads.");
            }
        }

        // -------------------------------------------------------------------------------------------------------------

        private static string? ListedReason(string id)
        {
            foreach ((string listed, string reason) in ListEntries())
            {
                if (listed == id)
                {
                    return reason;
                }
            }

            return null;
        }

        private static IEnumerable<(string Id, string Reason)> ListEntries()
        {
            string file = Path.Combine(AppContext.BaseDirectory, "YamlTestSuite", "writer-differences.txt");
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int space = line.IndexOf(' ');
                yield return space < 0 ? (line, "") : (line.Substring(0, space), line.Substring(space + 1).Trim());
            }
        }

        private static bool ReadsWithComments(byte[] input)
        {
            try
            {
                var reader = new Utf8YamlReader(input, ReaderOptions(comments: true));
                while (reader.Read())
                {
                }

                return true;
            }
            catch (YamlException)
            {
                return false;
            }
        }

        /// <summary>Every case reads with comments as tokens (none since M5.0), and the counts of kept styles.</summary>
        [Fact]
        public void Report()
        {
            var noComments = new List<string>();
            var kept = new Dictionary<YamlScalarStyle, int>();
            int cases = 0;
            foreach (object[] row in Cases())
            {
                string id = (string)row[0];
                cases++;
                byte[] input = YamlTestSuite.Load(id).Input;
                if (!ReadsWithComments(input))
                {
                    noComments.Add(id);
                }

                Compare(input, comments: false, out _, out Dictionary<YamlScalarStyle, int> counts);
                foreach (KeyValuePair<YamlScalarStyle, int> pair in counts)
                {
                    kept[pair.Key] = kept.TryGetValue(pair.Key, out int n) ? n + pair.Value : pair.Value;
                }
            }

            Assert.Empty(noComments);
            Console.WriteLine($"oracle 4: {cases} cases; kept styles: " + string.Join(", ", kept.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
        }

        private static YamlReaderOptions ReaderOptions(bool comments) => new()
        {
            AllowMultipleValues = true,
            MaxDepth = 256,
            CommentHandling = comments ? YamlCommentHandling.Allow : YamlCommentHandling.Skip,
        };

        // Null when the second read's events equal the first's under the rules above, else the first difference.
        private static readonly YamlWriterOptions s_layout = new() { Style = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = 256 };

        private static readonly YamlWriterOptions s_otherLayout = new()
        {
            Style = YamlWriterStyle.Block,
            NewLine = "\n",
            MaxDepth = 256,
            IndentSize = 4,
            IndentSequences = true,
            ExpandSequenceItems = true,
            MaxLineWidth = 20,
        };

        private static string? Compare(byte[] input, bool comments, out string written, out Dictionary<YamlScalarStyle, int> kept, YamlWriterOptions? options = null)
        {
            kept = new Dictionary<YamlScalarStyle, int>();
            List<Event> source = Events(input);
            var buffer = new ArrayBufferWriter<byte>();
            try
            {
                using (var writer = new Utf8YamlWriter(buffer, options ?? s_layout))
                {
                    var reader = new Utf8YamlReader(input, ReaderOptions(comments));
                    bool documentWritten = false;
                    while (reader.Read())
                    {
                        if (reader.CurrentDepth == 0 && reader.TokenType is not (YamlTokenType.EndObject or YamlTokenType.EndArray or YamlTokenType.Comment))
                        {
                            if (documentWritten)
                            {
                                writer.WriteDocumentStart();
                            }

                            documentWritten = true;
                        }

                        Replay(ref reader, writer);
                    }
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or YamlException)
            {
                written = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
                return $"the writer threw {e.GetType().Name}: {e.Message}";
            }

            written = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
            List<Event> back;
            try
            {
                back = Events(buffer.WrittenSpan.ToArray());
            }
            catch (YamlException e)
            {
                return $"the output does not read: {e.Message}";
            }

            for (int i = 0; i < Math.Max(source.Count, back.Count); i++)
            {
                if (i >= source.Count || i >= back.Count)
                {
                    return $"event {i}: {(i < source.Count ? source[i].ToString() : "(none)")} read back as {(i < back.Count ? back[i].ToString() : "(none)")}";
                }

                Event a = source[i];
                Event b = back[i];
                if (!Same(a, b))
                {
                    return $"event {i}: {a} read back as {b}";
                }

                if (a.Style != YamlScalarStyle.None && a.Style == b.Style)
                {
                    kept[a.Style] = kept.TryGetValue(a.Style, out int n) ? n + 1 : 1;
                }
            }

            return null;
        }

        private static bool Same(Event a, Event b)
        {
            if (a.Text != b.Text || a.Type != b.Type)
            {
                return false;
            }

            if (a.Style == b.Style)
            {
                return true;
            }

            // A string's style is its presentation: the writer quotes what it must, and a hint falls back where
            // the string does not allow it (6.2: a literal hint on one line is written as the default would be).
            return a.Token == YamlTokenType.String && b.Token == YamlTokenType.String;
        }

        // -------------------------------------------------------------------------------------------------------------
        // The replay

        private static void Replay(ref Utf8YamlReader reader, Utf8YamlWriter writer)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                case YamlTokenType.StartArray:
                    WriteProperties(ref reader, writer);
                    if (reader.IsFlow)
                    {
                        writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
                    }

                    if (reader.TokenType == YamlTokenType.StartObject)
                    {
                        writer.WriteStartObject();
                    }
                    else
                    {
                        writer.WriteStartArray();
                    }

                    break;
                case YamlTokenType.EndObject:
                    writer.WriteEndObject();
                    break;
                case YamlTokenType.EndArray:
                    writer.WriteEndArray();
                    break;
                case YamlTokenType.Comment:
                    writer.WriteCommentValue(reader.GetComment());
                    break;
                case YamlTokenType.Alias:
                    writer.WriteAliasValue(reader.ValueSpan);
                    break;
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias:
                    writer.WriteAliasPropertyName(reader.ValueSpan);
                    break;
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex:
                    writer.WriteStartPropertyKey();
                    Utf8YamlReader keyReader = reader.CreateKeyReader();
                    while (keyReader.Read())
                    {
                        Replay(ref keyReader, writer);
                    }

                    writer.WriteEndPropertyKey();
                    break;
                case YamlTokenType.PropertyName:
                    WriteProperties(ref reader, writer);
                    if (reader.ScalarStyle != YamlScalarStyle.None)
                    {
                        writer.WriteScalarStyle(reader.ScalarStyle);
                    }

                    writer.WritePropertyName(reader.GetString()!);
                    break;
                default:
                    WriteProperties(ref reader, writer);
                    WriteScalar(ref reader, writer);
                    break;
            }
        }

        // A string in its source's style (a hint); a number, boolean or null written plain in the source keeps its text
        // (raw); one written quoted is a string with its tag.
        private static void WriteScalar(ref Utf8YamlReader reader, Utf8YamlWriter writer)
        {
            YamlScalarStyle style = reader.ScalarStyle;
            if (reader.TokenType != YamlTokenType.String && style is YamlScalarStyle.Plain or YamlScalarStyle.None)
            {
                string raw = YamlEventTrace.ScalarText(ref reader);
                if (reader.TokenType == YamlTokenType.Null && raw.Length == 0)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteRawValue(raw);
                }

                return;
            }

            if (style != YamlScalarStyle.None)
            {
                writer.WriteScalarStyle(style);
            }

            writer.WriteStringValue(YamlEventTrace.ScalarText(ref reader));
        }

        private static void WriteProperties(ref Utf8YamlReader reader, Utf8YamlWriter writer)
        {
            if (!reader.Anchor.IsEmpty)
            {
                writer.WriteAnchor(reader.Anchor);
            }

            string? tag = reader.GetExpandedTag();
            if (tag is not null)
            {
                writer.WriteTag(tag == "!" ? "!" : "!<" + PercentEncode(tag) + ">");
            }
        }

        // The tag's characters that are not URI characters, percent-encoded (UTF-8).
        private static string PercentEncode(string tag)
        {
            var builder = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(tag))
            {
                char c = (char)b;
                if (b < 0x80 && (char.IsLetterOrDigit(c) || "-#;/?:@&=+$,_.!~*'()[]".IndexOf(c) >= 0))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('%').Append(b.ToString("X2", Globalization.CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        // -------------------------------------------------------------------------------------------------------------
        // The events of a read: YamlEventTrace's, with the token type of each scalar and its style apart, and nulls as
        // one event.

        private readonly record struct Event(string Type, string Text, YamlTokenType Token, YamlScalarStyle Style)
        {
            public override string ToString() => $"{Type} {Text} ({Token}, {Style})";
        }

        private static List<Event> Events(byte[] yaml)
        {
            var reader = new Utf8YamlReader(yaml, ReaderOptions(comments: false));
            var events = new List<Event>();
            while (reader.Read())
            {
                Add(ref reader, events);
            }

            return events;
        }

        private static void Add(ref Utf8YamlReader reader, List<Event> events)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                case YamlTokenType.StartArray:
                    events.Add(new Event((reader.TokenType == YamlTokenType.StartObject ? "+MAP" : "+SEQ") + (reader.IsFlow ? " flow" : ""),
                        Properties(ref reader), reader.TokenType, YamlScalarStyle.None));
                    break;
                case YamlTokenType.EndObject:
                    events.Add(new Event("-MAP", "", reader.TokenType, YamlScalarStyle.None));
                    break;
                case YamlTokenType.EndArray:
                    events.Add(new Event("-SEQ", "", reader.TokenType, YamlScalarStyle.None));
                    break;
                case YamlTokenType.Alias:
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias:
                    events.Add(new Event("=ALI", Encoding.UTF8.GetString(reader.ValueSpan.ToArray()), YamlTokenType.Alias, YamlScalarStyle.None));
                    break;
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex:
                    Utf8YamlReader keyReader = reader.CreateKeyReader();
                    while (keyReader.Read())
                    {
                        Add(ref keyReader, events);
                    }

                    break;
                case YamlTokenType.Comment:
                    break;
                case YamlTokenType.PropertyName:
                    events.Add(new Event("=KEY", Properties(ref reader) + " " + reader.GetString(), YamlTokenType.String, reader.ScalarStyle));
                    break;
                default:
                    string text = reader.TokenType == YamlTokenType.Null ? "~null" : YamlEventTrace.ScalarText(ref reader);
                    events.Add(new Event("=VAL", Properties(ref reader) + " " + text, reader.TokenType,
                        reader.TokenType == YamlTokenType.Null ? YamlScalarStyle.None : reader.ScalarStyle));
                    break;
            }
        }

        private static string Properties(ref Utf8YamlReader reader)
        {
            string anchor = reader.Anchor.IsEmpty ? "" : "&" + Encoding.UTF8.GetString(reader.Anchor.ToArray());
            string? tag = reader.GetExpandedTag();
            return tag is null ? anchor : anchor + " <" + tag + ">";
        }
    }
}
