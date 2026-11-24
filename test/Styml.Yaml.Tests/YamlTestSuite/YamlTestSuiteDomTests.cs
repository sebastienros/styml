// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5a's DOM read oracle: for every yaml-test-suite case the reader
    /// reads with the expected trace, a walk of <see cref="YamlDocument"/> gives the same trace as the reader. The trace
    /// holds what both can say: structure, keys as text, each node's kind, anchor, expanded tag and scalar style, scalar
    /// text, and aliases by name. Flow or block layout and the properties and style of keys are not in the DOM's public
    /// surface, so neither trace has them. Since M5e a collection key is compared too: the reader's trace
    /// lists the key's own events (through its key reader) between KEY+ and KEY-, and the walk the key element's;
    /// several documents are one document each for the DOM, so a stream is compared document by document.
    /// </summary>
    public class YamlTestSuiteDomTests
    {
        public static IEnumerable<object[]> Cases() => YamlTestSuiteWriterTests.Cases();

        private static readonly YamlReaderOptions s_readerOptions = new() { AllowMultipleValues = true, MaxDepth = 256 };
        // LastWins: the oracle compares the parse, not composition, and 2JQS repeats its empty key (M5c, 5.3, A3; its
        // default-options row is DefaultOptionsRefuse2JQS).
        private static readonly YamlDocumentOptions s_documentOptions = new() { MaxDepth = 256, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        [Fact]
        public void DefaultOptionsRefuse2JQS()
        {
            // The only suite case with a repeated key (suite-repeated-keys.py).
            Assert.Contains("Duplicate", Assert.Throws<YamlException>(() => YamlDocument.Parse(YamlTestSuite.Load("2JQS").Input)).Message);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void DomWalk_EqualsTheReadersTrace(string id)
        {
            byte[] input = YamlTestSuite.Load(id).Input;
            foreach ((byte[] document, List<string> expected, _) in Documents(input))
            {
                using YamlDocument dom = YamlDocument.Parse(document, s_documentOptions);
                var actual = new List<string>();
                Walk(dom.RootElement, actual);
                Assert.True(expected.SequenceEqual(actual), $"{id}:\nreader: {string.Join(" | ", expected)}\ndom:    {string.Join(" | ", actual)}");
            }
        }

        [Fact]
        public void Report_EveryCaseIsCompared()
        {
            int documents = 0;
            int complexKeys = 0;
            foreach (object[] row in Cases())
            {
                foreach ((_, _, bool complexKey) in Documents(YamlTestSuite.Load((string)row[0]).Input))
                {
                    documents++;
                    complexKeys += complexKey ? 1 : 0;
                }
            }

            // Every document is compared; since M5e those with a collection key (D10) too.
            Assert.True(documents > 300, $"{documents} documents");
            Assert.True(complexKeys > 0, "the suite has collection keys");
            Console.WriteLine($"DOM oracle: {documents} documents, {complexKeys} with a collection key (compared since M5e)");
        }

        /// <summary>
        /// GetRawText's contract (M5a): for every node of every document compared above, YamlDocument.Parse reads the
        /// node's raw text back to an equal node. Excepted, and counted: a node with an alias (as a value or a key) whose
        /// anchor is outside it, a node whose tags need a %TAG directive of the stream, and an empty node, whose raw text is
        /// empty, which a document cannot be (decision D4, until M5c).
        /// </summary>
        [Fact]
        public void GetRawText_ReadsBackForEveryNode()
        {
            int nodes = 0;
            int outsideAlias = 0;
            int tagHandle = 0;
            int empty = 0;
            var failures = new List<string>();
            foreach (object[] row in Cases())
            {
                string id = (string)row[0];
                foreach ((byte[] document, _, _) in Documents(YamlTestSuite.Load(id).Input))
                {
                    using YamlDocument dom = YamlDocument.Parse(document, s_documentOptions);
                    foreach (YamlElement node in Nodes(dom.RootElement))
                    {
                        nodes++;
                        string text = node.GetRawText();
                        if (text.Trim().Length == 0)
                        {
                            empty++;
                            continue;
                        }

                        try
                        {
                            using YamlDocument back = YamlDocument.Parse(text, s_documentOptions);
                            if (!YamlElement.DeepEquals(node, back.RootElement))
                            {
                                failures.Add($"{id}: [{text}]");
                            }
                        }
                        catch (YamlException e) when (e.Message.Contains("does not refer to an anchor"))
                        {
                            outsideAlias++;
                        }
                        catch (YamlException) when (NeedsTagDirective(node))
                        {
                            tagHandle++;
                        }
                        catch (Exception e)
                        {
                            failures.Add($"{id}: [{text}] {e.GetType().Name}: {e.Message}");
                        }
                    }
                }
            }

            Console.WriteLine($"GetRawText oracle: {nodes} nodes, {outsideAlias} with an alias to an outside anchor, {tagHandle} needing a %TAG directive, {empty} empty");
            Assert.True(failures.Count == 0, $"{failures.Count} of {nodes} nodes:\n" + string.Join("\n", failures.Take(40)));
        }

        private static IEnumerable<YamlElement> Nodes(YamlElement element)
        {
            yield return element;
            if (element.IsAlias)
            {
                yield break;
            }

            if (element.ValueKind == YamlValueKind.Object)
            {
                foreach (YamlProperty property in element.EnumerateObject())
                {
                    foreach (YamlElement child in Nodes(property.Value))
                    {
                        yield return child;
                    }
                }
            }
            else if (element.ValueKind == YamlValueKind.Array)
            {
                foreach (YamlElement item in element.EnumerateArray())
                {
                    foreach (YamlElement child in Nodes(item))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static bool HasAlias(YamlElement element) => Nodes(element).Any(e => e.IsAlias);

        // A tag whose expansion is not the standard one ('!!x' as tag:yaml.org,2002:x, '!x' as itself, '!<...>' verbatim)
        // comes from a %TAG directive of the stream, which the node's text alone does not have.
        private static bool NeedsTagDirective(YamlElement element) => Nodes(element).Any(e =>
            e.GetTag() is string tag && e.GetExpandedTag() is string expanded
            && !(tag.StartsWith("!!", StringComparison.Ordinal) && expanded == "tag:yaml.org,2002:" + tag.Substring(2))
            && !(tag.StartsWith("!<", StringComparison.Ordinal))
            && !(tag.IndexOf('!', 1) < 0 && expanded == tag));

        // The stream's documents, each with the reader's trace and whether it has a collection key.
        private static IEnumerable<(byte[] Document, List<string> Trace, bool ComplexKey)> Documents(byte[] input)
        {
            var result = new List<(byte[], List<string>, bool)>();
            var reader = new Utf8YamlReader(input, s_readerOptions);
            List<string>? trace = null;
            Dictionary<string, string>? anchoredText = null;
            bool complexKey = false;
            long start = 0;
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.Comment)
                {
                    continue;
                }

                if (trace is null)
                {
                    trace = new List<string>();
                    anchoredText = new Dictionary<string, string>(StringComparer.Ordinal);
                    complexKey = false;
                }

                AddReaderEvent(ref reader, trace, anchoredText!);
                complexKey |= reader.TokenType == YamlTokenType.PropertyName && reader.KeyKind == YamlKeyKind.Complex;

                if (reader.CurrentDepth == 0 && reader.TokenType is not (YamlTokenType.StartObject or YamlTokenType.StartArray))
                {
                    // The document ends here: its bytes run from the previous document's end (its '---' and directives
                    // included) to here, with a leading '...' end marker of the previous document left out.
                    int end = (int)reader.BytesConsumed;
                    byte[] document = WithoutLeadingEndMarker(input.AsSpan((int)start, end - (int)start));
                    result.Add((document, trace, complexKey));
                    trace = null;
                    start = end;
                }
            }

            if (result.Count == 1)
            {
                // One document: the whole stream, directives included (a %TAG handle needs its directive).
                result[0] = (input, result[0].Item2, result[0].Item3);
            }

            return result;
        }

        private static byte[] WithoutLeadingEndMarker(ReadOnlySpan<byte> text)
        {
            while (true)
            {
                int i = 0;
                while (i < text.Length && text[i] is (byte)'\n' or (byte)'\r' or (byte)' ')
                {
                    i++;
                }

                if (text.Length - i >= 3 && text[i] == (byte)'.' && text[i + 1] == (byte)'.' && text[i + 2] == (byte)'.')
                {
                    int lineEnd = text.Slice(i).IndexOf((byte)'\n');
                    text = lineEnd < 0 ? default : text.Slice(i + lineEnd + 1);
                    continue;
                }

                return text.ToArray();
            }
        }

        private static int LineOffset(byte[] input, int position)
        {
            int i = position;
            while (i > 0 && input[i - 1] != (byte)'\n')
            {
                i--;
            }

            // Only white space before the node on its line (properties are part of the node).
            for (int j = i; j < position; j++)
            {
                if (input[j] != (byte)' ')
                {
                    return 0;
                }
            }

            return position - i;
        }

        private static void AddReaderEvent(ref Utf8YamlReader reader, List<string> trace, Dictionary<string, string> anchoredText)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                    trace.Add("+MAP" + ReaderProperties(ref reader));
                    break;
                case YamlTokenType.StartArray:
                    trace.Add("+SEQ" + ReaderProperties(ref reader));
                    break;
                case YamlTokenType.EndObject:
                    trace.Add("-MAP");
                    break;
                case YamlTokenType.EndArray:
                    trace.Add("-SEQ");
                    break;
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias:
                    {
                        // An alias to a collection is a collection key (M5e): the alias, by name.
                        string name = Encoding.UTF8.GetString(reader.ValueSpan.ToArray());
                        trace.Add(anchoredText.TryGetValue(name, out string? keyText) ? "KEY " + keyText : "KEYALI *" + name);
                        break;
                    }

                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex:
                    {
                        // M5e: the key's own events, through its key reader.
                        trace.Add("KEY+");
                        Utf8YamlReader key = reader.CreateKeyReader();
                        while (key.Read())
                        {
                            if (key.TokenType != YamlTokenType.Comment)
                            {
                                AddReaderEvent(ref key, trace, anchoredText);
                            }
                        }

                        trace.Add("KEY-");
                        break;
                    }
                case YamlTokenType.PropertyName:
                    {
                        string text = YamlEventTrace.ScalarText(ref reader);
                        Remember(ref reader, anchoredText, text);
                        trace.Add("KEY " + text);
                        break;
                    }

                case YamlTokenType.Alias:
                    trace.Add("ALI *" + Encoding.UTF8.GetString(reader.ValueSpan.ToArray()));
                    break;
                default:
                    {
                        string text = YamlEventTrace.ScalarText(ref reader);
                        Remember(ref reader, anchoredText, text);
                        trace.Add($"VAL {reader.TokenType}{ReaderProperties(ref reader)} {reader.ScalarStyle} {text}");
                        break;
                    }
            }
        }

        private static void Remember(ref Utf8YamlReader reader, Dictionary<string, string> anchoredText, string text)
        {
            if (!reader.Anchor.IsEmpty)
            {
                anchoredText[Encoding.UTF8.GetString(reader.Anchor.ToArray())] = text;
            }
        }

        private static string ReaderProperties(ref Utf8YamlReader reader) =>
            (reader.Anchor.IsEmpty ? "" : " &" + Encoding.UTF8.GetString(reader.Anchor.ToArray()))
            + (reader.Tag.IsEmpty ? "" : " <" + reader.GetExpandedTag() + ">");

        private static string DomProperties(YamlElement element) =>
            (element.GetAnchor() is string anchor ? " &" + anchor : "")
            + (element.GetExpandedTag() is string tag ? " <" + tag + ">" : "");

        // The write-back's shape also has what the DOM keeps of a node's text: the order of its properties (tag first),
        // a number's spelling, and a null that was empty in the source.
        // (A document's root that is empty is written null: written empty, the document would not be read again.)
        private static string KeptText(YamlElement element, bool root = false) =>
            (element.TagBeforeAnchor ? " tag-first" : "")
            + (element.ValueKind == YamlValueKind.Number ? " " + element.GetRawText() : "")
            + (!root && element.ValueKind == YamlValueKind.Null && element.GetRawText().Length == 0 ? " empty" : "");

        // ------------------------------------------------------------------------------------------------------------
        // M5f: the DOM keeps each scalar's style on
        // write-back. For every document the walk above compares, Parse, WriteTo in block style and a parse give the same
        // shape (structure, anchors, tags and their order, kinds, string values, numbers' spellings, empty nulls), each string keeps its source style or falls in one of
        // the named classes (each counted), and writing the result again gives the same bytes (a fixed point).

        private static readonly YamlWriterOptions s_writeBack = new() { Style = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = 256 };

        [Fact]
        public void WriteBack_KeepsScalarStyles()
        {
            var kept = new Dictionary<YamlScalarStyle, int>();
            var classes = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var examples = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var failures = new List<string>();
            int documents = 0;
            foreach (object[] row in Cases())
            {
                string id = (string)row[0];
                foreach ((byte[] document, _, _) in Documents(YamlTestSuite.Load(id).Input))
                {
                    using YamlDocument source = YamlDocument.Parse(document, s_documentOptions);
                    byte[] written = WriteBack(source);
                    using YamlDocument back = YamlDocument.Parse(written, s_documentOptions);
                    documents++;

                    var sourceShape = new List<string>();
                    var sourceStrings = new List<(YamlScalarStyle Style, string Text)>();
                    Shape(source.RootElement, sourceShape, sourceStrings, root: true);
                    var backShape = new List<string>();
                    var backStrings = new List<(YamlScalarStyle Style, string Text)>();
                    Shape(back.RootElement, backShape, backStrings, root: true);
                    if (!sourceShape.SequenceEqual(backShape))
                    {
                        failures.Add($"{id}: shape\n  {string.Join(" | ", sourceShape)}\n  {string.Join(" | ", backShape)}\n{Encoding.UTF8.GetString(written)}");
                        continue;
                    }

                    for (int i = 0; i < sourceStrings.Count; i++)
                    {
                        (YamlScalarStyle style, string text) = sourceStrings[i];
                        YamlScalarStyle backStyle = backStrings[i].Style;
                        if (style == backStyle)
                        {
                            kept[style] = kept.TryGetValue(style, out int n) ? n + 1 : 1;
                            continue;
                        }

                        string? name = FallbackClass(style, backStyle, text);
                        if (name is null)
                        {
                            failures.Add($"{id}: {style} {Escape(text)} written {backStyle}\n{Encoding.UTF8.GetString(written)}");
                        }
                        else
                        {
                            classes[name] = classes.TryGetValue(name, out int n) ? n + 1 : 1;
                            if (!examples.ContainsKey(name))
                            {
                                examples[name] = $"{id} {Escape(text)}";
                            }
                        }
                    }

                    // The fixed point (review): what the write-back writes, written back again, is the same bytes.
                    byte[] again = WriteBack(back);
                    if (!again.AsSpan().SequenceEqual(written))
                    {
                        failures.Add($"{id}: not a fixed point\n{Encoding.UTF8.GetString(written)}\n---\n{Encoding.UTF8.GetString(again)}");
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n\n", failures.Take(5)) + $"\n({failures.Count} in all)");
            Console.WriteLine($"Write-back: {documents} documents; kept {string.Join(", ", kept.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}"))}; fallbacks {string.Join(", ", classes.Select(p => $"{p.Key}: {p.Value} (e.g. {examples[p.Key]})"))}");

            // Each style of the source is kept for some scalars. These counts are of string values only (oracle 4's include
            // keys and every scalar kind), pinned below the M5f counts (Plain 389, SingleQuoted 13, DoubleQuoted 67,
            // Literal 32, Folded 27).
            foreach (YamlScalarStyle style in new[] { YamlScalarStyle.Plain, YamlScalarStyle.SingleQuoted, YamlScalarStyle.DoubleQuoted, YamlScalarStyle.Literal, YamlScalarStyle.Folded })
            {
                int minimum = style switch { YamlScalarStyle.Plain => 350, YamlScalarStyle.DoubleQuoted => 60, YamlScalarStyle.SingleQuoted => 10, _ => 25 };
                Assert.True(kept.TryGetValue(style, out int n) && n >= minimum, $"{style}: {(kept.TryGetValue(style, out int m) ? m : 0)} kept");
            }
        }

        private static byte[] WriteBack(YamlDocument document)
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, s_writeBack))
            {
                document.WriteTo(writer);
            }

            return buffer.WrittenSpan.ToArray();
        }

        // A style the writer cannot keep, by class. Anything else fails the test.
        private static string? FallbackClass(YamlScalarStyle source, YamlScalarStyle written, string text)
        {
            bool lineBreak = text.IndexOf('\n') >= 0;
            return (source, written) switch
            {
                // M7b (item 2): single quotes over several lines hold a line break, but not a tab or a space next to one.
                (YamlScalarStyle.SingleQuoted, YamlScalarStyle.DoubleQuoted) when lineBreak && (text.IndexOf('\t') >= 0 || text.IndexOf(" \n", StringComparison.Ordinal) >= 0 || text.IndexOf("\n ", StringComparison.Ordinal) >= 0) =>
                    "single-quoted with a line break and a tab, or a space next to a break: double-quoted",
                (YamlScalarStyle.SingleQuoted, YamlScalarStyle.DoubleQuoted) => "single-quoted that single quotes cannot hold (a tab or a control): double-quoted",
                (YamlScalarStyle.Literal or YamlScalarStyle.Folded, YamlScalarStyle.Plain or YamlScalarStyle.DoubleQuoted) when !lineBreak => "a block scalar on one line: the writer's choice",
                (YamlScalarStyle.Literal or YamlScalarStyle.Folded, YamlScalarStyle.DoubleQuoted) => "a block scalar the writer cannot hold (a line of spaces, a first line with a tab, a leading space at the root): double-quoted",
                (YamlScalarStyle.Plain, YamlScalarStyle.Literal) when lineBreak => "a plain scalar with a line break: literal (A2)",
                (YamlScalarStyle.Plain, YamlScalarStyle.DoubleQuoted) => "a plain scalar the writer's syntax check refuses: double-quoted",
                _ => null,
            };
        }

        private static string Escape(string text) => text.Replace("\n", "\\n").Replace("\t", "\\t");

        // M7b: a collection's flow style is part of the
        // shape, so a flow collection is written back flow and a block one block.
        private static string Flow(YamlElement element) => element.CollectionStyle == YamlCollectionStyle.Flow ? " flow" : "";

        // The shape: structure, anchors, tags, kinds and string values; strings' styles apart.
        private static void Shape(YamlElement element, List<string> shape, List<(YamlScalarStyle Style, string Text)> strings, bool root = false)
        {
            if (element.IsAlias)
            {
                shape.Add("ALI *" + element.GetAnchor());
                return;
            }

            switch (element.ValueKind)
            {
                case YamlValueKind.Object:
                    shape.Add("+MAP" + DomProperties(element) + KeptText(element) + Flow(element));
                    foreach (YamlProperty property in element.EnumerateObject())
                    {
                        if (property.KeyKind == YamlKeyKind.Complex)
                        {
                            shape.Add("KEY+");
                            Shape(property.GetKeyElement(), shape, strings);
                            shape.Add("KEY-");
                        }
                        else if (property.KeyKind == YamlKeyKind.Alias && property.GetKeyElement().ValueKind is YamlValueKind.Object or YamlValueKind.Array)
                        {
                            // Its name is the raw text of the anchored node, which follows the layout.
                            shape.Add("KEYALI *" + property.GetKeyElement().GetAnchor());
                        }
                        else
                        {
                            shape.Add("KEY " + property.Name);
                        }

                        Shape(property.Value, shape, strings);
                    }

                    shape.Add("-MAP");
                    break;
                case YamlValueKind.Array:
                    shape.Add("+SEQ" + DomProperties(element) + KeptText(element) + Flow(element));
                    foreach (YamlElement item in element.EnumerateArray())
                    {
                        Shape(item, shape, strings);
                    }

                    shape.Add("-SEQ");
                    break;
                case YamlValueKind.String:
                    shape.Add("STR" + DomProperties(element) + KeptText(element) + " " + element.GetString());
                    strings.Add((element.ScalarStyle, element.GetString()!));
                    break;
                default:
                    shape.Add(element.ValueKind + DomProperties(element) + KeptText(element, root));
                    break;
            }
        }

        private static void Walk(YamlElement element, List<string> trace)
        {
            if (element.IsAlias)
            {
                // The element is the anchored node; the alias names the most recent anchor of that name, which is it.
                trace.Add("ALI *" + element.GetAnchor());
                return;
            }

            switch (element.ValueKind)
            {
                case YamlValueKind.Object:
                    trace.Add("+MAP" + DomProperties(element));
                    foreach (YamlProperty property in element.EnumerateObject())
                    {
                        YamlElement key = property.GetKeyElement();
                        if (property.KeyKind == YamlKeyKind.Complex)
                        {
                            trace.Add("KEY+");
                            Walk(key, trace);
                            trace.Add("KEY-");
                        }
                        else if (property.KeyKind == YamlKeyKind.Alias && key.ValueKind is YamlValueKind.Object or YamlValueKind.Array)
                        {
                            trace.Add("KEYALI *" + key.GetAnchor());
                        }
                        else
                        {
                            trace.Add("KEY " + property.Name);
                        }

                        Walk(property.Value, trace);
                    }

                    trace.Add("-MAP");
                    break;
                case YamlValueKind.Array:
                    trace.Add("+SEQ" + DomProperties(element));
                    foreach (YamlElement item in element.EnumerateArray())
                    {
                        Walk(item, trace);
                    }

                    trace.Add("-SEQ");
                    break;
                default:
                    {
                        YamlTokenType type = element.ValueKind switch
                        {
                            YamlValueKind.String => YamlTokenType.String,
                            YamlValueKind.Number => YamlTokenType.Number,
                            YamlValueKind.True => YamlTokenType.True,
                            YamlValueKind.False => YamlTokenType.False,
                            _ => YamlTokenType.Null,
                        };
                        string text = element.ValueKind == YamlValueKind.String
                            ? element.GetString()!
                            : Encoding.UTF8.GetString(Runtime.InteropServices.YamlMarshal.GetRawUtf8Value(element).ToArray());
                        trace.Add($"VAL {type}{DomProperties(element)} {element.ScalarStyle} {text}");
                        break;
                    }
            }
        }
    }
}
