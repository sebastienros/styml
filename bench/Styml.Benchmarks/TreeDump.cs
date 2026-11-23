// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4p: the old-against-new byte comparison for
// candidate c. It writes seeded random trees in block style through the public writer API only, so that it runs
// against any Styml.dll by the dll swap. The trees hold what the twelve documents never exercise: explicit keys, flow
// inside block, anchors, tags, aliases, both comment forms, directives and documents, raw values, multi-line and
// hinted scalars, M4b's options, IndentSize up to 127, both line breaks, deep nesting, and nodes written by
// YamlNode.WriteTo.
//
// Each tree is written three times, through three writers that are reused (Reset) from tree to tree:
//   A  an ArrayBufferWriter<byte>;
//   B  a buffer writer that hands out exactly the size the writer asks for, never more, with Flush calls at random
//      points between the calls, so that every span ends where the writer's reservation ends;
//   C  a MemoryStream.
// It prints one line per tree: index, the tree's options, and per writer the length and the first 16 hex digits of its
// SHA-256 ("=A" when equal to A's), or the exception's type and call index. Two builds compare writer by writer, by
// diff. Within one build B and C equal A, except that B may differ where MaxLineWidth is set: the writer measures a
// line's column from the bytes it still holds, so a line begun before a flush, or before a buffer writer's span ends,
// folds later (CurrentBlockColumn). B may also differ in a tree with a multi-line raw block collection as a sequence
// item: that path decides on its dash from the last byte the writer still holds.
// Lines starting with '#' are the census of what was reached.
//   dotnet Styml.Benchmarks.dll tree-dump <count> <seed>
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Y = System.Text.Yaml;
using YN = System.Text.Yaml.Nodes;

namespace Styml.Benchmarks
{
    internal static class TreeDump
    {
        // The indentation widths the census counts around: c's precomputed line start holds this many spaces.
        private const int SpanSpaces = 128;

        public static int Run(string[] args, TextWriter output)
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 20_000;
            int seed = args.Length > 2 ? int.Parse(args[2]) : 20261121;
            var census = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var arrayWriter = new ArrayBufferWriter<byte>();
            var exact = new ExactBufferWriter();
            var stream = new MemoryStream();
            Y.Utf8YamlWriter? wa = null, wb = null, wc = null;
            Y.YamlWriterOptions previous = default;
            int failures = 0, mismatches = 0, widthFlushDiffers = 0, rawFlushDiffers = 0;
            var indentHistogram = new long[4]; // SpanSpaces - 1, SpanSpaces, SpanSpaces + 1, above SpanSpaces + 1
            int maxIndent = 0;

            for (int i = 0; i < count; i++)
            {
                int treeSeed = unchecked(seed * 1_000_003 + i);
                var shape = new Random(treeSeed);
                bool keepOptions = wa is not null && shape.Next(4) == 0;
                Y.YamlWriterOptions options = keepOptions ? previous : PickOptions(shape);
                previous = options;
                int genSeed = shape.Next();
                int flushSeed = shape.Next();

                arrayWriter.Clear();
                exact.Clear();
                stream.SetLength(0);

                string? exA = WriteTree(ref wa, options, keepOptions, w => w.Reset(arrayWriter), w => w.Reset(arrayWriter, options), () => new Y.Utf8YamlWriter(arrayWriter, options), genSeed, null, census, out bool rawItem, out bool rawAfterProperties);
                string? exB = WriteTree(ref wb, options, keepOptions, w => w.Reset(exact), w => w.Reset(exact, options), () => new Y.Utf8YamlWriter(exact, options), genSeed, new Random(flushSeed), null, out _, out _);
                string? exC = WriteTree(ref wc, options, keepOptions, w => w.Reset(stream), w => w.Reset(stream, options), () => new Y.Utf8YamlWriter(stream, options), genSeed, null, null, out _, out _);

                byte[] a = arrayWriter.WrittenSpan.ToArray();
                byte[] b = exact.Written.ToArray();
                byte[] c = stream.ToArray();
                bool bDiffers = exB is null && !b.AsSpan().SequenceEqual(a);
                bool cDiffers = exC is null && !c.AsSpan().SequenceEqual(a);
                if (exA is not null || exB is not null || exC is not null)
                {
                    failures++;
                }
                else if (cDiffers || (bDiffers && options.MaxLineWidth == 0 && !rawItem))
                {
                    mismatches++;
                }
                else if (bDiffers && options.MaxLineWidth != 0)
                {
                    widthFlushDiffers++;
                }
                else if (bDiffers)
                {
                    rawFlushDiffers++;
                }

                output.WriteLine($"{i} {Describe(options)} A:{exA ?? Hash(a)} B:{exB ?? (bDiffers ? Hash(b) : "=A")} C:{exC ?? (cDiffers ? Hash(c) : "=A")}");
                if (rawAfterProperties && Environment.GetEnvironmentVariable("TREE_DUMP_RAWPROPS") == "1")
                {
                    // For the WriteRawValue fix's check (M4p): the trees it may change.
                    output.WriteLine($"#P {i}");
                }

                if (Environment.GetEnvironmentVariable("TREE_DUMP_SHOW") == i.ToString())
                {
                    File.WriteAllBytes("tree-a.yaml", a);
                    File.WriteAllBytes("tree-b.yaml", b);
                    File.WriteAllBytes("tree-c.yaml", c);
                }
                CountIndentation(a, indentHistogram, ref maxIndent);
            }

            output.WriteLine($"# trees {count}, seed {seed}; trees with an exception {failures}; trees whose C differs from A, or B without MaxLineWidth {mismatches}; trees with MaxLineWidth whose B differs from A {widthFlushDiffers}; trees with a raw block collection item, without MaxLineWidth, whose B differs from A {rawFlushDiffers}");
            output.WriteLine($"# lines indented {SpanSpaces - 1}: {indentHistogram[0]}, {SpanSpaces}: {indentHistogram[1]}, {SpanSpaces + 1}: {indentHistogram[2]}, more: {indentHistogram[3]}; widest {maxIndent}");
            foreach (KeyValuePair<string, long> entry in census)
            {
                output.WriteLine($"# {entry.Key} {entry.Value}");
            }

            return failures == 0 && mismatches == 0 ? 0 : 1;
        }

        // Writes one tree through `writer`, reusing it when it exists: Reset with the sink alone when the options are the
        // previous tree's, Reset with the options otherwise. An exception ends the tree and drops the writer.
        private static string? WriteTree(ref Y.Utf8YamlWriter? writer, Y.YamlWriterOptions options, bool keepOptions,
            Action<Y.Utf8YamlWriter> reset, Action<Y.Utf8YamlWriter> resetWithOptions, Func<Y.Utf8YamlWriter> create,
            int genSeed, Random? flushes, SortedDictionary<string, long>? census, out bool rawItem, out bool rawAfterProperties)
        {
            rawItem = false;
            rawAfterProperties = false;
            if (writer is null)
            {
                writer = create();
            }
            else if (keepOptions)
            {
                reset(writer);
            }
            else
            {
                resetWithOptions(writer);
            }

            var gen = new Generator(writer, options, new Random(genSeed), flushes, census);
            try
            {
                gen.Tree();
                writer.Flush();
                rawItem = gen.RawBlockCollectionItem;
                rawAfterProperties = gen.RawBlockCollectionAfterProperties;
                return null;
            }
            catch (Exception e)
            {
                writer = null;
                return $"{e.GetType().Name}@{gen.Calls}";
            }
        }

        private static string Hash(byte[] bytes) => bytes.Length + ":" + Convert.ToHexString(SHA256.HashData(bytes), 0, 8).ToLowerInvariant();

        private static string Describe(Y.YamlWriterOptions o) =>
            $"i{o.IndentSize}{(o.NewLine.Length == 2 ? "crlf" : "lf")}{(o.IndentSequences ? "S" : "")}{(o.ExpandSequenceItems ? "E" : "")}" +
            $"m{(int)o.MultiLineStringStyle}w{o.MaxLineWidth}{(o.QuoteForLenientReaders ? "Q" : "")}d{o.MaxDepth}";

        private static Y.YamlWriterOptions PickOptions(Random r)
        {
            int[] indents = { 2, 2, 2, 3, 3, 4, 4, 5, 8, 2 + r.Next(11), 31, 43, 64, 127 };
            Y.YamlScalarStyle[] multiLine = { Y.YamlScalarStyle.None, Y.YamlScalarStyle.DoubleQuoted, Y.YamlScalarStyle.Literal, Y.YamlScalarStyle.Folded };
            var options = new Y.YamlWriterOptions
            {
                Style = Y.YamlWriterStyle.Block,
                IndentSize = indents[r.Next(indents.Length)],
                NewLine = r.Next(3) == 0 ? "\r\n" : "\n",
                IndentSequences = r.Next(2) == 0,
                ExpandSequenceItems = r.Next(4) == 0,
                MultiLineStringStyle = multiLine[r.Next(multiLine.Length)],
                MaxLineWidth = r.Next(3) == 0 ? 0 : 8 + r.Next(113),
                QuoteForLenientReaders = r.Next(3) == 0,
            };
            if (r.Next(3) == 0)
            {
                options.MaxDepth = 200;
            }

            return options;
        }

        private static void CountIndentation(byte[] yaml, long[] histogram, ref int widest)
        {
            int i = 0;
            while (i < yaml.Length)
            {
                int spaces = 0;
                while (i < yaml.Length && yaml[i] == (byte)' ')
                {
                    spaces++;
                    i++;
                }

                if (spaces == SpanSpaces - 1) histogram[0]++;
                else if (spaces == SpanSpaces) histogram[1]++;
                else if (spaces == SpanSpaces + 1) histogram[2]++;
                else if (spaces > SpanSpaces + 1) histogram[3]++;
                widest = Math.Max(widest, spaces);

                int next = Array.IndexOf(yaml, (byte)'\n', i);
                i = next < 0 ? yaml.Length : next + 1;
            }
        }

        // Hands out exactly the size asked for (at least one byte) and keeps what is advanced.
        private sealed class ExactBufferWriter : IBufferWriter<byte>
        {
            private readonly ArrayBufferWriter<byte> _written = new();
            private byte[] _buffer = new byte[1 << 16];
            private int _handedOut;

            public ReadOnlySpan<byte> Written => _written.WrittenSpan;

            public void Clear()
            {
                _written.Clear();
                _handedOut = 0;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                _handedOut = Math.Max(sizeHint, 1);
                if (_buffer.Length < _handedOut)
                {
                    _buffer = new byte[_handedOut];
                }

                return _buffer.AsMemory(0, _handedOut);
            }

            public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

            public void Advance(int count)
            {
                if ((uint)count > (uint)_handedOut)
                {
                    throw new InvalidOperationException($"Advance({count}) past the {_handedOut} bytes handed out.");
                }

                _written.Write(_buffer.AsSpan(0, count));
                _handedOut = 0;
            }
        }

        private enum Place
        {
            Root,
            SequenceItem,
            MappingValue,
            ExplicitKey,
        }

        private sealed class Generator
        {
            private static readonly string[] s_fragments =
            {
                "a", "plain text", "with: colon", "#hash", "a #b", "- dash", "? q", "[x]", "{y}", "*star", "&amp", "!bang",
                "multi\nline", "trailing\n", "two\n\nbreaks\n", " lead", "trail ", "true", "No", "123", "0x1F", "1e3",
                "12:30", "2026-11-21", "null", "~", "'q'", "\"dq\"", "tab\there", "\u0001ctl", "café", "日本",
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt",
                "word word word word word word word word word word word word word word word word",
                "", ",", "a,b", "%pct", "@at", "`tick", "|pipe", ">gt", "= eq", "<<",
            };

            private static readonly string[] s_names = { "k", "name", "a b", "with:colon", "123", "true", "", "multi\nline", "café", "- x", "key with a long name for the width" };
            private static readonly string[] s_tags = { "!t", "!!str", "!!map", "!<tag:example.com,2000:x>", "!e!foo" };
            private static readonly string[] s_anchors = { "a1", "a2", "a3", "x", "a:b", "anchor_long_name" };
            private static readonly string[] s_comments = { "c", "two words", "a */ b", "l1\nl2", "" };
            private static readonly string[] s_rawBlock = { "1", "\"q\"", "[1, 2]", "{a: 1}", "a: 1\nb: [x]", "- 1\n- 2", "x" };
            private static readonly string[] s_rawFlow = { "1", "\"q\"", "[1, 2]", "{a: 1}", "x" };
            private static readonly Y.YamlEncodedText[] s_encoded = s_names.Select(n => Y.YamlEncodedText.Encode(n)).ToArray();

            private readonly Y.Utf8YamlWriter _w;
            private readonly Y.YamlWriterOptions _options;
            private readonly Random _r;
            private readonly Random? _flushes;
            private readonly SortedDictionary<string, long>? _census;
            private readonly int _maxDepth;
            private int _depth;
            private int _flowDepth;
            private bool _deep;

            public Generator(Y.Utf8YamlWriter w, Y.YamlWriterOptions options, Random r, Random? flushes, SortedDictionary<string, long>? census)
            {
                _w = w;
                _options = options;
                _r = r;
                _flushes = flushes;
                _census = census;
                _maxDepth = options.MaxDepth == 0 ? 1000 : options.MaxDepth;
            }

            public int Calls { get; private set; }

            // The tree writes a multi-line raw block collection as a sequence item.
            public bool RawBlockCollectionItem { get; private set; }

            // The same, after an anchor or a tag (the case the WriteRawValue fix of M4p changes).
            public bool RawBlockCollectionAfterProperties { get; private set; }

            // Set while Value writes the scalar that follows the properties it wrote.
            private bool _valueHasProperties;

            private void Tick(string kind)
            {
                Calls++;
                if (_census is not null)
                {
                    _census.TryGetValue(kind, out long n);
                    _census[kind] = n + 1;
                }

                if (_flushes is not null && _flushes.Next(6) == 0)
                {
                    _w.Flush();
                }
            }

            private bool Chance(int inN) => _r.Next(inN) == 0;

            private string Text()
            {
                int parts = 1 + _r.Next(3);
                var sb = new StringBuilder();
                for (int i = 0; i < parts; i++)
                {
                    sb.Append(s_fragments[_r.Next(s_fragments.Length)]);
                }

                return sb.ToString();
            }

            public void Tree()
            {
                _deep = Chance(8);
                int documents = 1 + (Chance(3) ? _r.Next(3) : 0);
                if (Chance(10))
                {
                    Tick("node.root");
                    (Node() ?? YN.YamlValue.Create("x")).WriteTo(_w);
                    return;
                }

                for (int d = 0; d < documents; d++)
                {
                    if (d > 0)
                    {
                        if (Chance(2))
                        {
                            Tick("document.end");
                            _w.WriteDocumentEnd();
                        }
                        else if (Chance(3))
                        {
                            Tick("comment.between-documents");
                            _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)]);
                        }
                    }

                    if (Chance(5))
                    {
                        Tick("directive");
                        _w.WriteDirective(Chance(2) ? "%YAML 1.2" : "%TAG !e! tag:example.com,2000:");
                        if (Chance(3))
                        {
                            Tick("comment.after-directive");
                            _w.WriteCommentValue("d");
                        }

                        Tick("document.start");
                        _w.WriteDocumentStart();
                    }
                    else if (d > 0 || Chance(5))
                    {
                        Tick("document.start");
                        _w.WriteDocumentStart();
                    }

                    if (Chance(4))
                    {
                        Tick("comment.before-root");
                        _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)]);
                    }

                    Value(Place.Root);
                    if (Chance(5))
                    {
                        Tick("comment.after-root");
                        _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)], Chance(2));
                    }
                }

                if (Chance(4))
                {
                    Tick("document.end.last");
                    _w.WriteDocumentEnd();
                }
            }

            // How many children a collection gets: a deep tree is a chain with few siblings.
            private int Children()
            {
                if (_deep)
                {
                    return _depth + 2 < Math.Min(_maxDepth, 130) ? 1 + (Chance(4) ? 1 : 0) : 0;
                }

                if (_depth >= 9 || _depth + 2 >= _maxDepth)
                {
                    return 0;
                }

                return Chance(6) ? 0 : 1 + _r.Next(_depth < 3 ? 6 : 3);
            }

            private bool CollectionHere() => _deep ? _depth + 2 < Math.Min(_maxDepth, 130) : (_depth < 9 && _depth + 2 < _maxDepth && Chance(_depth < 2 ? 2 : 4));

            // Optional properties before a node.
            private bool Properties(bool allowAnchor = true)
            {
                bool any = false;
                if (allowAnchor && Chance(6))
                {
                    Tick("anchor");
                    _w.WriteAnchor(s_anchors[_r.Next(s_anchors.Length)]);
                    any = true;
                }

                if (Chance(8))
                {
                    Tick("tag");
                    _w.WriteTag(s_tags[_r.Next(s_tags.Length)]);
                    any = true;
                }

                return any;
            }

            private void Value(Place place)
            {
                if (place != Place.Root && place != Place.ExplicitKey && Chance(14))
                {
                    Tick("alias.value");
                    _w.WriteAliasValue(s_anchors[_r.Next(s_anchors.Length)]);
                    EndOfLineComment();
                    return;
                }

                bool properties = Properties();
                if (CollectionHere())
                {
                    Collection();
                    if (Chance(10))
                    {
                        EndOfLineComment(force: true);
                    }

                    return;
                }

                _valueHasProperties = properties;
                Scalar(place);
                _valueHasProperties = false;
                EndOfLineComment();
            }

            private void EndOfLineComment(bool force = false)
            {
                if (force || Chance(12))
                {
                    Tick("comment.end-of-line");
                    _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length - 1)], true);
                }
            }

            private void Scalar(Place place)
            {
                switch (_r.Next(10))
                {
                    case 0:
                        Tick("value.int");
                        _w.WriteNumberValue(_r.Next(-1000, 100000));
                        return;
                    case 1:
                        Tick("value.double");
                        _w.WriteNumberValue(_r.NextDouble() * 1000);
                        return;
                    case 2:
                        Tick("value.bool");
                        _w.WriteBooleanValue(Chance(2));
                        return;
                    case 3:
                        Tick("value.null");
                        _w.WriteNullValue();
                        return;
                    case 4 when place != Place.ExplicitKey:
                        string raw = _flowDepth > 0 ? s_rawFlow[_r.Next(s_rawFlow.Length)] : s_rawBlock[_r.Next(s_rawBlock.Length)];
                        if (place == Place.SequenceItem && raw.Contains('\n'))
                        {
                            RawBlockCollectionItem = true;
                            RawBlockCollectionAfterProperties |= _valueHasProperties;
                        }

                        Tick(_flowDepth > 0 ? "value.raw.flow" : "value.raw.block");
                        _w.WriteRawValue(raw);
                        return;
                }

                string text = Text();
                if (place != Place.ExplicitKey && Chance(4))
                {
                    Y.YamlScalarStyle[] styles = { Y.YamlScalarStyle.Plain, Y.YamlScalarStyle.SingleQuoted, Y.YamlScalarStyle.DoubleQuoted, Y.YamlScalarStyle.Literal, Y.YamlScalarStyle.Folded };
                    Y.YamlScalarStyle style = styles[_r.Next(styles.Length)];
                    Tick("hint.scalar." + style);
                    _w.WriteScalarStyle(style);
                }

                switch (_r.Next(3))
                {
                    case 0:
                        Tick("value.string");
                        _w.WriteStringValue(text);
                        break;
                    case 1:
                        Tick("value.string.utf8");
                        _w.WriteStringValue(Encoding.UTF8.GetBytes(text));
                        break;
                    default:
                        Tick("value.string.chars");
                        _w.WriteStringValue(text.AsSpan());
                        break;
                }
            }

            private void CollectionHint()
            {
                if (Chance(3))
                {
                    Y.YamlCollectionStyle style = Chance(4) ? Y.YamlCollectionStyle.Block : Y.YamlCollectionStyle.Flow;
                    Tick("hint.collection." + style);
                    _w.WriteCollectionStyle(style);
                    LastHintWasFlow = style == Y.YamlCollectionStyle.Flow;
                }
            }

            // A collection whose properties, if any, are already written.
            private void Collection()
            {
                bool sequence = Chance(2);
                CollectionHint();
                Start(sequence);
                Children(sequence);
            }

            private void Start(bool sequence)
            {
                Tick(sequence ? "start.sequence" : "start.mapping");
                if (sequence)
                {
                    _w.WriteStartArray();
                }
                else
                {
                    _w.WriteStartObject();
                }
            }

            private void Children(bool sequence)
            {
                _depth++;
                bool flow = _flowDepth > 0 || LastHintWasFlow;
                if (flow)
                {
                    _flowDepth++;
                }

                LastHintWasFlow = false;
                int n = Children();
                if (n > 0 && Chance(6))
                {
                    Tick("comment.first-child");
                    _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)]);
                }

                for (int i = 0; i < n; i++)
                {
                    if (i > 0 && Chance(10))
                    {
                        Tick("comment.between");
                        _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)]);
                    }

                    if (sequence)
                    {
                        Value(Place.SequenceItem);
                    }
                    else
                    {
                        Entry();
                    }
                }

                if (flow)
                {
                    _flowDepth--;
                }

                _depth--;
                Tick(sequence ? "end.sequence" : "end.mapping");
                if (sequence)
                {
                    _w.WriteEndArray();
                }
                else
                {
                    _w.WriteEndObject();
                }
            }

            private bool LastHintWasFlow { get; set; }

            private void Entry()
            {
                int form = _r.Next(12);
                if (form == 0 && _depth + 2 < _maxDepth)
                {
                    Tick("key.explicit");
                    _w.WriteStartPropertyKey();
                    _depth++;
                    Properties();
                    if (CollectionHere() || Chance(3))
                    {
                        Collection();
                    }
                    else
                    {
                        Scalar(Place.ExplicitKey);
                    }

                    _depth--;
                    Tick("key.explicit.end");
                    _w.WriteEndPropertyKey();
                    Value(Place.MappingValue);
                    return;
                }

                if (form == 1)
                {
                    Tick("key.alias");
                    _w.WriteAliasPropertyName(s_anchors[_r.Next(s_anchors.Length)]);
                    Value(Place.MappingValue);
                    return;
                }

                Properties();
                int nameIndex = _r.Next(s_names.Length);
                int nameForm = _r.Next(4);
                if (form <= 5)
                {
                    Name(nameIndex, nameForm);
                    if (Chance(10))
                    {
                        Tick("comment.after-key");
                        _w.WriteCommentValue(s_comments[_r.Next(s_comments.Length)]);
                    }

                    Value(Place.MappingValue);
                    return;
                }

                if (form <= 7 && CollectionHere())
                {
                    bool sequence = Chance(2);
                    CollectionHint();
                    Tick(sequence ? "start.sequence.named" : "start.mapping.named");
                    switch (nameForm)
                    {
                        case 0:
                            if (sequence) _w.WriteStartArray(s_names[nameIndex]); else _w.WriteStartObject(s_names[nameIndex]);
                            break;
                        case 1:
                            if (sequence) _w.WriteStartArray(Encoding.UTF8.GetBytes(s_names[nameIndex])); else _w.WriteStartObject(Encoding.UTF8.GetBytes(s_names[nameIndex]));
                            break;
                        case 2:
                            if (sequence) _w.WriteStartArray(s_names[nameIndex].AsSpan()); else _w.WriteStartObject(s_names[nameIndex].AsSpan());
                            break;
                        default:
                            if (sequence) _w.WriteStartArray(s_encoded[nameIndex]); else _w.WriteStartObject(s_encoded[nameIndex]);
                            break;
                    }

                    Children(sequence);
                    return;
                }

                string text = Text();
                string name = s_names[nameIndex];
                switch (_r.Next(5))
                {
                    case 0:
                        Tick("pair.number");
                        if (nameForm == 3) _w.WriteNumber(s_encoded[nameIndex], _r.Next(-50, 5000)); else _w.WriteNumber(name, _r.Next(-50, 5000));
                        break;
                    case 1:
                        Tick("pair.bool");
                        if (nameForm == 3) _w.WriteBoolean(s_encoded[nameIndex], Chance(2)); else _w.WriteBoolean(name, Chance(2));
                        break;
                    case 2:
                        Tick("pair.null");
                        if (nameForm == 3) _w.WriteNull(s_encoded[nameIndex]); else _w.WriteNull(name);
                        break;
                    default:
                        Tick("pair.string." + nameForm);
                        switch (nameForm)
                        {
                            case 0: _w.WriteString(name, text); break;
                            case 1: _w.WriteString(Encoding.UTF8.GetBytes(name), Encoding.UTF8.GetBytes(text)); break;
                            case 2: _w.WriteString(name.AsSpan(), text.AsSpan()); break;
                            default: _w.WriteString(s_encoded[nameIndex], text); break;
                        }

                        break;
                }

                EndOfLineComment();
            }

            private void Name(int index, int form)
            {
                Tick("key.name." + form);
                switch (form)
                {
                    case 0: _w.WritePropertyName(s_names[index]); break;
                    case 1: _w.WritePropertyName(Encoding.UTF8.GetBytes(s_names[index])); break;
                    case 2: _w.WritePropertyName(s_names[index].AsSpan()); break;
                    default: _w.WritePropertyName(s_encoded[index]); break;
                }
            }

            // A YamlNode tree, written by WriteTo.
            private YN.YamlNode? Node(int depth = 0)
            {
                int kind = depth >= 6 ? 2 + _r.Next(4) : _r.Next(6);
                switch (kind)
                {
                    case 0:
                    {
                        var o = new YN.YamlObject();
                        int n = _r.Next(5);
                        for (int i = 0; i < n; i++)
                        {
                            o.Add(s_names[_r.Next(s_names.Length)] + i, Node(depth + 1));
                        }

                        return o;
                    }
                    case 1:
                    {
                        var a = new YN.YamlArray();
                        int n = _r.Next(5);
                        for (int i = 0; i < n; i++)
                        {
                            a.Add(Node(depth + 1));
                        }

                        return a;
                    }
                    case 2: return YN.YamlValue.Create(Text());
                    case 3: return YN.YamlValue.Create(_r.Next(-1000, 100000));
                    case 4: return YN.YamlValue.Create(Chance(2));
                    default: return null;
                }
            }
        }
    }
}
