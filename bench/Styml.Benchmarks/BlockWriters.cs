// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The block-style rows of gate G3, from M4a. They call members that
// do not exist at 9835d4d (YamlWriterOptions.Style), so they live in classes of their own, run on the head only, and
// never in the dll swap of G1.
//
// WriteBlock also splits block style's extra cost between keys and values: the same token stream with every key
// replaced by "k" (ConstKeys) or every string value by "v" (ConstValues), for both writers. The share of the keys is
// (Block - Json) - (Block_ConstKeys - Json_ConstKeys), and likewise for the values.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using SJ = System.Text.Json.JsonSerializer;

namespace Styml.Benchmarks
{
    internal static class BlockLibraries
    {
        public static readonly System.Text.Yaml.YamlWriterOptions BlockWriterOptions = new() { Style = System.Text.Yaml.YamlWriterStyle.Block };

        // With QuoteForLenientReaders (from M4b's fix step): the cost of the option's wider set.
        public static readonly System.Text.Yaml.YamlWriterOptions BlockLenientWriterOptions = new() { Style = System.Text.Yaml.YamlWriterStyle.Block, QuoteForLenientReaders = true };

        public static readonly System.Text.Yaml.YamlSerializerOptions BlockOptions = new()
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.Block,
        };

        // The serializer with QuoteForLenientReaders, for 'hash-block' only (M4p).
        public static readonly System.Text.Yaml.YamlSerializerOptions BlockLenientOptions = new()
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.Block,
            QuoteForLenientReaders = true,
        };

        public static readonly StymlWriteContext BlockGenerated = new(new System.Text.Yaml.YamlSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.Block,
        });

        // The token stream with every key (or every string value) replaced by one short constant.
        public static TokenStream Replace(TokenStream source, TokenStream.Kind kind, string replacement)
        {
            var stream = new TokenStream();
            var text = new ArrayBufferWriter<byte>();
            byte[] constant = Encoding.UTF8.GetBytes(replacement);
            int s = 0;
            foreach (TokenStream.Kind k in source.Kinds)
            {
                stream.Kinds.Add(k);
                if (k is TokenStream.Kind.PropertyName or TokenStream.Kind.String)
                {
                    (int offset, int length) = source.Spans[s++];
                    ReadOnlySpan<byte> bytes = k == kind ? constant : source.Text.AsSpan(offset, length);
                    stream.Spans.Add((text.WrittenCount, bytes.Length));
                    text.Write(bytes);
                }
            }

            stream.Integers.AddRange(source.Integers);
            stream.Doubles.AddRange(source.Doubles);
            stream.Text = text.WrittenSpan.ToArray();
            return stream.Freeze();
        }
    }

    public class WriteBlock : AllDocuments
    {
        private TokenStream _tokens = null!;
        private TokenStream _constKeys = null!;
        private TokenStream _constValues = null!;
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private System.Text.Yaml.Utf8YamlWriter _block = null!;
        private System.Text.Yaml.Utf8YamlWriter _lenient = null!;
        private Utf8JsonWriter _stjIndented = null!;

        public override void Setup()
        {
            WriteLibraries.SetSwitch();
            base.Setup();
            _tokens = TokenStream.Record(_doc.Json).Freeze();
            _constKeys = BlockLibraries.Replace(_tokens, TokenStream.Kind.PropertyName, "k");
            _constValues = BlockLibraries.Replace(_tokens, TokenStream.Kind.String, "v");
            _block = new System.Text.Yaml.Utf8YamlWriter(_buffer, BlockLibraries.BlockWriterOptions);
            _lenient = new System.Text.Yaml.Utf8YamlWriter(_buffer, BlockLibraries.BlockLenientWriterOptions);
            _stjIndented = new Utf8JsonWriter(_buffer, new JsonWriterOptions { Indented = true });
        }

        [Benchmark, Input(Form.Json)] public long Styml_Block() => WriteLibraries.ReplayStyml(_tokens, _block, _buffer);
        [Benchmark, Input(Form.Json)] public long Styml_Block_Lenient() => WriteLibraries.ReplayStyml(_tokens, _lenient, _buffer);
        [Benchmark(Baseline = true), Input(Form.Json)] public long Stj_Indented() => WriteLibraries.ReplayJson(_tokens, _stjIndented, _buffer);
        [Benchmark, Input(Form.Json)] public long Styml_Block_ConstKeys() => WriteLibraries.ReplayStyml(_constKeys, _block, _buffer);
        [Benchmark, Input(Form.Json)] public long Stj_Indented_ConstKeys() => WriteLibraries.ReplayJson(_constKeys, _stjIndented, _buffer);
        [Benchmark, Input(Form.Json)] public long Styml_Block_ConstValues() => WriteLibraries.ReplayStyml(_constValues, _block, _buffer);
        [Benchmark, Input(Form.Json)] public long Stj_Indented_ConstValues() => WriteLibraries.ReplayJson(_constValues, _stjIndented, _buffer);

        internal byte[] Output()
        {
            Styml_Block();
            return _buffer.WrittenSpan.ToArray();
        }

        // The bytes a block row writes, for 'hash-block'.
        internal byte[] Output(string method)
        {
            GetType().GetMethod(method)!.Invoke(this, null);
            return _buffer.WrittenSpan.ToArray();
        }
    }

    public class SerializeReflectionBlock : WriteScenario
    {
        [Benchmark, Input(Form.Json)] public object Styml_Block() => WriteLibraries.Styml(_graph, _doc.Family, BlockLibraries.BlockOptions);
        [Benchmark(Baseline = true), Input(Form.Json)] public object Stj() => WriteLibraries.Json(_graph, _doc.Family);
    }

    public class SerializeSourceGenBlock : WriteScenario
    {
        [Benchmark, Input(Form.Json)] public object Styml_Block() => WriteLibraries.StymlGeneratedWrite(_graph, _doc.Family, BlockLibraries.BlockGenerated);
        [Benchmark(Baseline = true), Input(Form.Json)] public object Stj() => WriteLibraries.JsonGeneratedWrite(_graph, _doc.Family);
    }

    // G3's non-ASCII rows, from M4b (M4a's acceptance, item 4): block style against JSON-compatible indented with the
    // relaxed encoder, the writer's own and Utf8JsonWriter's, on 100 mappings of five properties whose names and
    // values (about 20 characters) are drawn from one alphabet: ASCII, Latin with accents, or CJK. Target: block at
    // most 1.5x Styml_Indented_Relaxed. The ascii set is the control. The rows written from strings are the
    // serializer's path; the _Utf8 rows write the same text as UTF-8.
    public class WriteBlockNonAscii
    {
        public static IEnumerable<string> Documents => new[] { "ascii", "latin", "cjk" };

        [ParamsSource(nameof(Documents))]
        public string Document { get; set; } = null!;

        private const int Items = 100;
        private string[] _names = null!;
        private string[] _values = null!;
        private byte[][] _names8 = null!;
        private byte[][] _values8 = null!;
        private string[] _asciiNames = null!;
        private System.Text.Yaml.YamlEncodedText[] _encodedAsciiNames = null!;
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private System.Text.Yaml.Utf8YamlWriter _block = null!;
        private System.Text.Yaml.Utf8YamlWriter _lenient = null!;
        private System.Text.Yaml.Utf8YamlWriter _indented = null!;
        private Utf8JsonWriter _stj = null!;

        [GlobalSetup]
        public void Setup()
        {
            WriteLibraries.SetSwitch();
            string alphabet = Document switch
            {
                "ascii" => "abcdefghijklmnopqrstuvwxyz     ",
                "latin" => "aàâäeéèêëiîïoôöuùûüçœæ     ",
                "cjk" => "日本語中文漢字한국어の字体字型文本",
                _ => throw new ArgumentException(Document),
            };
            var random = new Random(1);
            string Make(int length)
            {
                var text = new StringBuilder("w");
                for (int i = 0; i < length; i++)
                {
                    text.Append(alphabet[random.Next(alphabet.Length)]);
                }

                return text.ToString().Trim();
            }

            _names = new string[5];
            for (int i = 0; i < _names.Length; i++)
            {
                _names[i] = Make(8) + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _values = new string[Items * _names.Length];
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] = Make(20);
            }

            _names8 = Array.ConvertAll(_names, Encoding.UTF8.GetBytes);
            _values8 = Array.ConvertAll(_values, Encoding.UTF8.GetBytes);

            // Fixed ASCII names for the mixed rows, after the draws, so that the other rows read what they read before
            _asciiNames = new[] { "wdkqhfzea0", "wmrtpxoyb1", "wcnvjlsug2", "wqbzehfir3", "wyoxktdmp4" };
            _encodedAsciiNames = Array.ConvertAll(_asciiNames, n => System.Text.Yaml.YamlEncodedText.Encode(n));
            _block = new System.Text.Yaml.Utf8YamlWriter(_buffer, BlockLibraries.BlockWriterOptions);
            _lenient = new System.Text.Yaml.Utf8YamlWriter(_buffer, BlockLibraries.BlockLenientWriterOptions);
            _indented = new System.Text.Yaml.Utf8YamlWriter(_buffer, new System.Text.Yaml.YamlWriterOptions
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            _stj = new Utf8JsonWriter(_buffer, new JsonWriterOptions
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        // The bytes a block row writes, for 'hash-block'.
        internal byte[] Output(string method)
        {
            GetType().GetMethod(method)!.Invoke(this, null);
            return _buffer.WrittenSpan.ToArray();
        }

        [Benchmark] public long Styml_Block() => Styml(_block, utf8: false);
        [Benchmark] public long Styml_Block_Lenient() => Styml(_lenient, utf8: false);
        [Benchmark] public long Styml_Block_Lenient_Utf8() => Styml(_lenient, utf8: true);
        [Benchmark(Baseline = true)] public long Styml_Indented_Relaxed() => Styml(_indented, utf8: false);
        [Benchmark] public long Stj_Indented_Relaxed() => Stj(utf8: false);
        [Benchmark] public long Styml_Block_Utf8() => Styml(_block, utf8: true);

        // M4p b, the verification rows: ASCII names with this set's values, as strings, through
        // WriteString(string, string); and the serializer's shape, a pre-encoded name with a string value
        // (WriteString(YamlEncodedText, string)). Not in hash-block's list, which is unchanged; 'hash-block-mixed'
        // hashes them.
        [Benchmark] public long Styml_Block_AsciiNames() => StymlMixed(_block, encoded: false);
        [Benchmark] public long Styml_Block_EncodedAsciiNames() => StymlMixed(_block, encoded: true);
        [Benchmark] public long Styml_Indented_Relaxed_Utf8() => Styml(_indented, utf8: true);
        [Benchmark] public long Stj_Indented_Relaxed_Utf8() => Stj(utf8: true);

        private long Styml(System.Text.Yaml.Utf8YamlWriter writer, bool utf8)
        {
            _buffer.ResetWrittenCount();
            writer.Reset(_buffer);
            writer.WriteStartArray();
            int v = 0;
            for (int item = 0; item < Items; item++)
            {
                writer.WriteStartObject();
                for (int n = 0; n < _names.Length; n++, v++)
                {
                    if (utf8)
                    {
                        writer.WriteString(_names8[n], _values8[v]);
                    }
                    else
                    {
                        writer.WriteString(_names[n], _values[v]);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();
            return _buffer.WrittenCount;
        }

        private long StymlMixed(System.Text.Yaml.Utf8YamlWriter writer, bool encoded)
        {
            _buffer.ResetWrittenCount();
            writer.Reset(_buffer);
            writer.WriteStartArray();
            int v = 0;
            for (int item = 0; item < Items; item++)
            {
                writer.WriteStartObject();
                for (int n = 0; n < _asciiNames.Length; n++, v++)
                {
                    if (encoded)
                    {
                        writer.WriteString(_encodedAsciiNames[n], _values[v]);
                    }
                    else
                    {
                        writer.WriteString(_asciiNames[n], _values[v]);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();
            return _buffer.WrittenCount;
        }

        private long Stj(bool utf8)
        {
            _buffer.ResetWrittenCount();
            _stj.Reset(_buffer);
            _stj.WriteStartArray();
            int v = 0;
            for (int item = 0; item < Items; item++)
            {
                _stj.WriteStartObject();
                for (int n = 0; n < _names.Length; n++, v++)
                {
                    if (utf8)
                    {
                        _stj.WriteString(_names8[n], _values8[v]);
                    }
                    else
                    {
                        _stj.WriteString(_names[n], _values[v]);
                    }
                }

                _stj.WriteEndObject();
            }

            _stj.WriteEndArray();
            _stj.Flush();
            return _buffer.WrittenCount;
        }

        internal byte[] Output(bool block)
        {
            Styml(block ? _block : _indented, utf8: false);
            return _buffer.WrittenSpan.ToArray();
        }
    }
}
