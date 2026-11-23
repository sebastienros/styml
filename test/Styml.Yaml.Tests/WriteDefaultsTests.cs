// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5f: the literal default for strings with a line break (decision 5,
    /// A2's eight rules) and plain number keys. Every output is read back by Styml; the libraries' reads are in
    /// Styml.Parity.Tests (WriteDefaultsParityTests).
    /// </summary>
    public class WriteDefaultsTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlWriterOptions s_writer = new() { Style = YamlWriterStyle.Block, NewLine = "\n" };

        private static string Write(Action<Utf8YamlWriter> write, YamlWriterOptions? options = null)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options ?? s_writer))
            {
                write(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        // ------------------------------------------------------------------------------------------------------------
        // A2: the literal default

        [Theory]
        // Rule 1 and chomping.
        [InlineData("a\nb", "k: |-\n  a\n  b\n")]
        [InlineData("a\nb\n", "k: |\n  a\n  b\n")]
        [InlineData("a\nb\n\n", "k: |+\n  a\n  b\n\n")]
        [InlineData("a\nb\n\n\n", "k: |+\n  a\n  b\n\n\n")]
        [InlineData("\n", "k: \"\\n\"\n")]
        [InlineData("\n\n", "k: \"\\n\\n\"\n")]
        // Rule 2.
        [InlineData("a\r\nb", "k: \"a\\r\\nb\"\n")]
        [InlineData("a\rb\nc", "k: \"a\\rb\\nc\"\n")]
        [InlineData("a\u0001\nb", "k: \"a\\u0001\\nb\"\n")]
        [InlineData("a\u007F\nb", "k: \"a\\u007F\\nb\"\n")]
        [InlineData("a\u0085b\nc", "k: \"a\\u0085b\\nc\"\n")]
        [InlineData("a\u2028b\nc", "k: \"a\\u2028b\\nc\"\n")]
        [InlineData("a\uFEFFb\nc", "k: \"a\\uFEFFb\\nc\"\n")]
        // Rule 3.
        [InlineData("a\n  \nb", "k: \"a\\n  \\nb\"\n")]
        [InlineData("a\n ", "k: \"a\\n \"\n")]
        [InlineData("a \nb", "k: |-\n  a \n  b\n")]
        [InlineData("a\nb ", "k: |-\n  a\n  b \n")]
        // Rule 4: a first line that starts with a tab; a later one is content.
        [InlineData("\ta\nb", "k: \"\\ta\\nb\"\n")]
        [InlineData("\n\ta", "k: \"\\n\\ta\"\n")]
        [InlineData("a\n\tb", "k: |-\n  a\n  \tb\n")]
        [InlineData("a\tb\nc", "k: |-\n  a\tb\n  c\n")]
        // Rule 5: a leading space needs the indicator.
        [InlineData(" a\nb", "k: |2-\n   a\n  b\n")]
        [InlineData("\n a", "k: |2-\n\n   a\n")]
        [InlineData("\n\na", "k: |-\n\n\n  a\n")]
        // Content that looks like markers or comments.
        [InlineData("a\n---\nb", "k: |-\n  a\n  ---\n  b\n")]
        [InlineData("---\na", "k: |-\n  ---\n  a\n")]
        [InlineData("a\n...\nb", "k: |-\n  a\n  ...\n  b\n")]
        [InlineData("a\n# b", "k: |-\n  a\n  # b\n")]
        [InlineData("a:\nb", "k: |-\n  a:\n  b\n")]
        [InlineData("\"a\\\"\n'b'", "k: |-\n  \"a\\\"\n  'b'\n")]
        [InlineData("é\n😀", "k: |-\n  é\n  😀\n")]
        public void Literal_Rules(string value, string expected)
        {
            var map = new Dictionary<string, string> { ["k"] = value };
            string yaml = YamlSerializer.Serialize(map, s_block);
            Assert.Equal(expected, yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<Dictionary<string, string>>(yaml)!["k"]);

            // Every entry point writes the same bytes: UTF-16 and UTF-8 values, a pre-encoded name, a sequence item.
            Assert.Equal(expected, Write(w => { w.WriteStartObject(); w.WriteString("k", value); w.WriteEndObject(); }));
            Assert.Equal(expected, Write(w => { w.WriteStartObject(); w.WriteString("k"u8, Encoding.UTF8.GetBytes(value)); w.WriteEndObject(); }));
            Assert.Equal(expected, Write(w => { w.WriteStartObject(); w.WriteString(YamlEncodedText.Encode("k"), value); w.WriteEndObject(); }));
            Assert.Equal(expected, Write(w => { w.WriteStartObject(); w.WritePropertyName("k"); w.WriteStringValue(value.AsSpan()); w.WriteEndObject(); }));
            string item = Write(w => { w.WriteStartArray(); w.WriteStringValue(value); w.WriteEndArray(); });
            Assert.Equal(item, Write(w => { w.WriteStartArray(); w.WriteStringValue(Encoding.UTF8.GetBytes(value)); w.WriteEndArray(); }));
            Assert.Equal(value, YamlSerializer.Deserialize<List<string>>(item)!.Single());
        }

        [Fact]
        public void Literal_Positions()
        {
            // The root, unless the first line starts with a space (libyaml counts the indicator from 0 there).
            Assert.Equal("|-\n  a\n  b\n", Write(w => w.WriteStringValue("a\nb")));
            Assert.Equal("\" a\\nb\"\n", Write(w => w.WriteStringValue(" a\nb")));

            // A nested sequence item with an indicator and keep chomping.
            string nested = Write(w => { w.WriteStartObject(); w.WritePropertyName("k"); w.WriteStartArray(); w.WriteStringValue(" a\nb\n\n"); w.WriteEndArray(); w.WriteEndObject(); });
            Assert.Equal("k:\n- |2+\n   a\n  b\n\n", nested);
            Assert.Equal(" a\nb\n\n", YamlSerializer.Deserialize<Dictionary<string, List<string>>>(nested)!["k"].Single());

            // Inside an explicit key: a value in block context.
            string inKey = Write(w => { w.WriteStartObject(); w.WriteStartPropertyKey(); w.WriteStartArray(); w.WriteStringValue("a\nb"); w.WriteEndArray(); w.WriteEndPropertyKey(); w.WriteNumberValue(1); w.WriteEndObject(); });
            Assert.Equal("? - |-\n    a\n    b\n: 1\n", inKey);

            // Rule 7: a key and a string in a flow collection stay double-quoted.
            Assert.Equal("\"a\\nb\": 1\n", Write(w => { w.WriteStartObject(); w.WritePropertyName("a\nb"); w.WriteNumberValue(1); w.WriteEndObject(); }));
            Assert.Equal("\"a\\nb\": 1\n", YamlSerializer.Serialize(new Dictionary<string, int> { ["a\nb"] = 1 }, s_block));
            Assert.Equal("[\"a\\nb\"]\n", Write(w => { w.WriteCollectionStyle(YamlCollectionStyle.Flow); w.WriteStartArray(); w.WriteStringValue("a\nb"); w.WriteEndArray(); }));

            // Rule 8: segments stay double-quoted.
            string segments = Write(w => { w.WriteStartObject(); w.WritePropertyName("s"); w.WriteStringValueSegment("a\n".AsSpan(), isFinalSegment: false); w.WriteStringValueSegment("b".AsSpan(), isFinalSegment: true); w.WriteEndObject(); });
            Assert.Equal("s: \"a\\nb\"\n", segments);
        }

        [Fact]
        public void Literal_Options()
        {
            // IndentSize: the indicator is the size; above 9 a leading space is double-quoted (rule 5).
            Assert.Equal("k: |4-\n     a\n    b\n", Write(w => { w.WriteStartObject(); w.WriteString("k", " a\nb"); w.WriteEndObject(); }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", IndentSize = 4 }));
            Assert.Equal("k: \" a\\nb\"\n", Write(w => { w.WriteStartObject(); w.WriteString("k", " a\nb"); w.WriteEndObject(); }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", IndentSize = 10 }));

            // MaxLineWidth never folds a literal's lines.
            const string Long = "one two three four five six seven\nb";
            Assert.Equal("k: |-\n  one two three four five six seven\n  b\n", Write(w => { w.WriteStartObject(); w.WriteString("k", Long); w.WriteEndObject(); }, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxLineWidth = 20 }));

            // NewLine: CRLF between the lines; the content reads back with \n.
            string crlf = YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a\nb" }, new YamlSerializerOptions(s_block) { NewLine = "\r\n" });
            Assert.Equal("k: |-\r\n  a\r\n  b\r\n", crlf);
            Assert.Equal("a\nb", YamlSerializer.Deserialize<Dictionary<string, string>>(crlf)!["k"]);

            // QuoteForLenientReaders: any line that starts with a tab is double-quoted.
            Assert.Equal("k: \"a\\n\\tb\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a\n\tb" }, new YamlSerializerOptions(s_block) { QuoteForLenientReaders = true }));

            // An explicit encoder that escapes a character (rule 6).
            var encoded = new YamlSerializerOptions(s_block) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default };
            Assert.Equal("k: \"\\u00E9\\nx\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "é\nx" }, encoded));

            // The old form, and the folded one.
            Assert.Equal("k: \"a\\nb\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a\nb" }, new YamlSerializerOptions(s_block) { MultiLineStringStyle = YamlScalarStyle.DoubleQuoted }));
            Assert.Equal("k: >-\n  a\n\n  b\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a\nb" }, new YamlSerializerOptions(s_block) { MultiLineStringStyle = YamlScalarStyle.Folded }));

            // The stored default is still None, which now means literal.
            Assert.Equal(YamlScalarStyle.None, new YamlSerializerOptions().MultiLineStringStyle);
            Assert.Equal(YamlScalarStyle.None, new YamlWriterOptions().MultiLineStringStyle);

            // The JSON-compatible style is unchanged.
            Assert.Equal("{\"k\":\"a\\nb\"}", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "a\nb" }, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));
        }

        [Fact]
        public void Literal_SerializeTo()
        {
            YamlElement element = YamlSerializer.SerializeToElement(new Dictionary<string, string> { ["k"] = "a\nb" }, s_block).GetProperty("k");
            Assert.Equal(YamlScalarStyle.Literal, element.ScalarStyle);
            Assert.Equal("a\nb", element.GetString());
            using YamlDocument document = YamlSerializer.SerializeToDocument(new Dictionary<string, string> { ["k"] = "a\nb\n" }, s_block);
            Assert.Equal(YamlScalarStyle.Literal, document.RootElement.GetProperty("k").ScalarStyle);
            Assert.Equal("a\nb\n", YamlSerializer.Deserialize<string>(YamlSerializer.SerializeToNode("a\nb\n", s_block)!));
        }

        // ------------------------------------------------------------------------------------------------------------
        // A3: plain number keys

        public enum Day
        {
            Monday = 1,
        }

        [Fact]
        public void NumberKeys_Written()
        {
            Assert.Equal("1: a\n", Ser(new Dictionary<int, string> { [1] = "a" }));
            Assert.Equal("-1: a\n", Ser(new Dictionary<long, string> { [-1] = "a" }));
            Assert.Equal("-9223372036854775808: a\n", Ser(new Dictionary<long, string> { [long.MinValue] = "a" }));
            Assert.Equal("18446744073709551615: a\n", Ser(new Dictionary<ulong, string> { [ulong.MaxValue] = "a" }));
            Assert.Equal("7: a\n", Ser(new Dictionary<byte, string> { [7] = "a" }));
            Assert.Equal("-3: a\n", Ser(new Dictionary<short, string> { [-3] = "a" }));
            Assert.Equal("1.5: a\n", Ser(new Dictionary<double, string> { [1.5] = "a" }));
            Assert.Equal("1E+20: a\n", Ser(new Dictionary<double, string> { [1e20] = "a" }));
#if NET
            Assert.Equal("-0: a\n", Ser(new Dictionary<double, string> { [-0.0] = "a" }));
#else
            // .NET Framework formats -0.0 as "0", for values as for keys (the netstandard2.0 build's double formatting).
            Assert.Equal("0: a\n", Ser(new Dictionary<double, string> { [-0.0] = "a" }));
#endif
#if NET
            Assert.Equal("0.1: a\n", Ser(new Dictionary<float, string> { [0.1f] = "a" }));
#else
            // .NET Framework's shortest float form is longer ("0.100000001"); it is plain and reads back the same.
            Assert.Equal(0.1f, YamlSerializer.Deserialize<Dictionary<float, string>>(Ser(new Dictionary<float, string> { [0.1f] = "a" }))!.Keys.Single());
#endif
            Assert.Equal("1.10: a\n", Ser(new Dictionary<decimal, string> { [1.10m] = "a" }));
            Assert.Equal("true: a\nfalse: b\n", Ser(new Dictionary<bool, string> { [true] = "a", [false] = "b" }));
            Assert.Equal("Monday: a\n9: b\n", Ser(new Dictionary<Day, string> { [Day.Monday] = "a", [(Day)9] = "b" }));
            Assert.Equal(".nan: a\n.inf: b\n-.inf: c\n", Ser(new Dictionary<double, string> { [double.NaN] = "a", [double.PositiveInfinity] = "b", [double.NegativeInfinity] = "c" }));
            Assert.Equal(".inf: a\n", Ser(new Dictionary<float, string> { [float.PositiveInfinity] = "a" }));
#if NET
            Assert.Equal("5: a\n", Ser(new Dictionary<Int128, string> { [5] = "a" }));
            Assert.Equal("6: a\n", Ser(new Dictionary<UInt128, string> { [6] = "a" }));
            Assert.Equal("1.5: a\n", Ser(new Dictionary<Half, string> { [(Half)1.5] = "a" }));
            Assert.Equal("NaN: a\n", Ser(new Dictionary<Half, string> { [Half.NaN] = "a" }));
#endif

            // Unchanged: string and char keys that look like numbers, and the JSON-compatible style.
            Assert.Equal("\"1\": a\n\"true\": b\n", Ser(new Dictionary<string, string> { ["1"] = "a", ["true"] = "b" }));
            Assert.Equal("\"1\": a\n", Ser(new Dictionary<char, string> { ['1'] = "a" }));
            var json = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible };
            Assert.Equal("{\"1\":\"a\"}", YamlSerializer.Serialize(new Dictionary<int, string> { [1] = "a" }, json));
            Assert.Equal("{\"True\":\"a\"}", YamlSerializer.Serialize(new Dictionary<bool, string> { [true] = "a" }, json));
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(new Dictionary<double, string> { [double.NaN] = "a" }, json));

            // An object key by its runtime type; a key written under MaxLineWidth (the writer's special path).
            Assert.Equal("3: 1\n\"3\": 2\n", Ser(new Dictionary<object, int> { [3] = 1, ["3"] = 2 }));
            Assert.Equal("3: 1\n", YamlSerializer.Serialize(new Dictionary<int, int> { [3] = 1 }, new YamlSerializerOptions(s_block) { MaxLineWidth = 20 }));
            Assert.Equal("m:\n  3: 1\n", YamlSerializer.Serialize(new Dictionary<string, Dictionary<int, int>> { ["m"] = new() { [3] = 1 } }, s_block));

            static string Ser<T>(T value) => YamlSerializer.Serialize(value, s_block);
        }

        public static IEnumerable<object[]> Schemas() => new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe }.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(Schemas))]
        public void NumberKeys_RoundTrip(YamlSchemaKind schema)
        {
            // Review 5: written plain, read back as the same typed key, through the text and through SerializeToElement. The
            // values are strings that look like numbers, which the writer quotes, so that they read the same under every schema
            // (a plain string value does not read under Json, and a plain number is a string under Failsafe).
            var options = new YamlSerializerOptions(s_block) { Schema = schema };
            RoundTrip(new Dictionary<int, string> { [1] = "1", [-1] = "2", [int.MaxValue] = "3" }, options);
            RoundTrip(new Dictionary<long, string> { [long.MinValue] = "1" }, options);
            RoundTrip(new Dictionary<double, string> { [1.5] = "1", [1e20] = "2", [-0.0] = "3" }, options);
            RoundTrip(new Dictionary<decimal, string> { [1.10m] = "1" }, options);
            RoundTrip(new Dictionary<bool, string> { [true] = "1", [false] = "2" }, options);
            RoundTrip(new Dictionary<Day, string> { [Day.Monday] = "1", [(Day)9] = "2" }, options);

            // -0 keeps its sign (on .NET Framework, -0.0 is formatted "0", for values as for keys).
            Dictionary<double, string> zero = YamlSerializer.Deserialize<Dictionary<double, string>>(YamlSerializer.Serialize(new Dictionary<double, string> { [-0.0] = "1" }, options), options)!;
#if NET
            Assert.True(1 / zero.Keys.Single() < 0);
#else
            Assert.Equal(0.0, zero.Keys.Single());
#endif

            // A Dictionary<string, T> and an object key read the text.
            Assert.Equal("1", YamlSerializer.Deserialize<Dictionary<string, string>>(YamlSerializer.Serialize(new Dictionary<int, string> { [1] = "1" }, options), options)!.Keys.Single());

            static void RoundTrip<TKey>(Dictionary<TKey, string> value, YamlSerializerOptions options) where TKey : notnull
            {
                string yaml = YamlSerializer.Serialize(value, options);
                Assert.Equal(value, YamlSerializer.Deserialize<Dictionary<TKey, string>>(yaml, options));
                YamlElement element = YamlSerializer.SerializeToElement(value, options);
                Assert.Equal(value, element.Deserialize<Dictionary<TKey, string>>(options));
            }
        }

        [Fact]
        public void NumberKeys_NonFinite_ReadBack()
        {
            // Core and Yaml11 read .nan and .inf into a typed key; Json and Failsafe do not, as a .nan value.
            string yaml = YamlSerializer.Serialize(new Dictionary<double, string> { [double.NaN] = "a", [double.PositiveInfinity] = "b" }, s_block);
            foreach (YamlSchemaKind schema in new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11 })
            {
                Dictionary<double, string> read = YamlSerializer.Deserialize<Dictionary<double, string>>(yaml, new YamlSerializerOptions { Schema = schema })!;
                Assert.Contains(read.Keys, double.IsNaN);
                Assert.Contains(double.PositiveInfinity, read.Keys);
            }

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<double, string>>(yaml, new YamlSerializerOptions { Schema = YamlSchemaKind.Failsafe }));
        }

        [Fact]
        public void NumberKeys_DuplicatesStayDuplicates()
        {
            // Review 5: 1 and 1.0 into Dictionary<double, T> are one key: an error under Error, as SharpYaml.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<double, string>>("1: a\n1.0: b\n"));
            Assert.Equal("b", YamlSerializer.Deserialize<Dictionary<double, string>>("1: a\n1.0: b\n", new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins })![1]);
        }
    }
}
