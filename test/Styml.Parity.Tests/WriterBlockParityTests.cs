// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;
using YDCore = YamlDotNet.Core;
using YDEvents = YamlDotNet.Core.Events;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M4a: the block-style targets of WriterParityTests become assertions on what
    /// Styml writes, and oracle 3, the cross-read: block output read by YamlDotNet and SharpYaml must
    /// equal what they read from the JSON twin.
    /// </summary>
    public class WriterBlockParityTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private static readonly YamlSerializerOptions s_lenient = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = true };

        private static string Write(Action<Utf8YamlWriter> calls, YamlWriterOptions? options = null)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options ?? new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                calls(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static string Ser<T>(T value, YamlSerializerOptions? options = null) => YamlSerializer.Serialize(value, options ?? s_block);

        // The Table 1 rows with a block target: M4a's, and since M4b the expanded items, the directive, the tag and the
        // comment. Anchors, collection keys and flow collections have no target there (their JSON twin cannot hold them);
        // BlockWriterNodesTests and oracle 4 cover them.
        private static readonly Dictionary<string, Func<string>> s_writes = new()
        {
            ["01.default"] = () => Ser(new WriterParityCases.Root()),
            ["01b.default"] = () => Ser(WriterParityCases.Nested),
            ["01b.indent4"] = () => Ser(WriterParityCases.Nested, new YamlSerializerOptions(s_block) { IndentSize = 4 }),
            ["02.indentSequences"] = () => Ser(new WriterParityCases.Root(), new YamlSerializerOptions(s_block) { IndentSequences = true }),
            ["03.compact"] = () => Ser(new[] { new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 } }),
            ["04.compact"] = () => Ser(new[] { new[] { 1, 2 } }),
            ["03.expanded"] = () => Ser(new[] { new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 } }, new YamlSerializerOptions(s_block) { ExpandSequenceItems = true }),
            ["04.expanded"] = () => Ser(new[] { new[] { 1, 2 } }, new YamlSerializerOptions(s_block) { ExpandSequenceItems = true }),
            ["09.directive"] = () => Write(w => { w.WriteDirective("%YAML 1.2"); w.WriteDocumentStart(); w.WriteStringValue("a"); }),
            ["18.tag"] = () => Write(w => { w.WriteStartObject(); w.WritePropertyName("Value"); w.WriteTag("!child"); w.WriteStartObject(); w.WriteNumber("V", 2); w.WriteEndObject(); w.WriteEndObject(); }),
            ["19.comment"] = () => Write(w => { w.WriteStartObject(); w.WriteCommentValue("the name"); w.WriteString("Name", "a"); w.WriteNumber("Count", 1); w.WriteEndObject(); }),
            ["06.emptySequence"] = () => Ser(Array.Empty<int>()),
            ["06.emptyMapping"] = () => Ser(new Dictionary<string, int>()),
            ["07.null"] = () => Ser(new[] { "a", null }),
            ["08.documentStart"] = () => Write(w => { w.WriteStringValue("a"); w.WriteDocumentStart(); w.WriteStringValue("b"); }),
            ["10.crlf"] = () => Ser(new[] { "x", "y" }, new YamlSerializerOptions(s_block) { NewLine = "\r\n" }),
            ["11.rootString"] = () => Ser("a"),
            ["11.rootNumber"] = () => Ser(1),
            ["11.rootSequence"] = () => Ser(new[] { 1 }),
            ["12.ordered"] = () => Ser(new WriterParityCases.Ordered()),
            ["13.explicitKey"] = () => Ser(new Dictionary<string, int> { [new string('a', 1025)] = 1 }),
            ["13.implicitKey"] = () => Ser(new Dictionary<string, int> { [new string('a', 1024)] = 1 }),
            ["14.lineBreakKey"] = () => Ser(new Dictionary<string, int> { ["a\nb"] = 1 }),
            ["20.noWidth"] = () => Ser(new Dictionary<string, string> { ["k"] = WriterParityCases.Words }),
            ["21.literal"] = () => Write(w => { w.WriteStartObject(); w.WriteScalarStyle(YamlScalarStyle.Literal); w.WriteString("k", "line1\nline2\n"); w.WriteEndObject(); }),
            ["22.doubleQuoted"] = () => Ser(new Dictionary<string, string> { ["k"] = "a: b" }),
            ["23.multiLine"] = () => Ser(new Dictionary<string, string> { ["k"] = "line1\nline2\n" }),
            ["24.raw"] = () => Ser(new Dictionary<string, string> { ["k"] = "héllo ☃ 😀 <&'" }),
            ["26.numbers"] = () => Ser(new Dictionary<string, object> { ["a"] = 1.0, ["b"] = 0.1, ["c"] = 1e20, ["d"] = 1e-7, ["e"] = -0.0, ["f"] = float.MaxValue, ["g"] = 1.10m }),
            ["27.dateTime"] = () => Ser(new Dictionary<string, DateTime> { ["v"] = new DateTime(2001, 12, 14, 21, 59, 43, 100, DateTimeKind.Utc) }),
            ["27.dateTimeOffset"] = () => Ser(new Dictionary<string, DateTimeOffset> { ["v"] = new DateTimeOffset(new DateTime(2001, 12, 14, 21, 59, 43, 100, DateTimeKind.Utc)) }),
            ["27.dateOnly"] = () => Ser(new Dictionary<string, DateOnly> { ["v"] = new DateOnly(2001, 12, 14) }),
            ["28.timeSpan"] = () => Ser(new Dictionary<string, TimeSpan> { ["v"] = new TimeSpan(10, 2, 3) }),
            ["28.timeOnly"] = () => Ser(new Dictionary<string, TimeOnly> { ["v"] = new TimeOnly(21, 59, 43) }),
            ["28b.version"] = () => Ser(new Dictionary<string, Version> { ["v"] = new Version(1, 10) }),
            ["28b.uri"] = () => Ser(new Dictionary<string, Uri> { ["v"] = new Uri("https://example.com/a?b=c") }),
            ["28c.flags"] = () => Ser(new[] { "--port=8080", "-v", "-a b", "?x" }),
            ["28d.quoted"] = () => Ser(new[] { ":x", "-1", "-.inf" }),
            ["29.bytes"] = () => Ser(new Dictionary<string, byte[]> { ["v"] = new byte[] { 1, 2, 3 } }),
            ["30.topLevelValues"] = () => TopLevel(),
            ["31.asyncEnumerable"] = () => Ser(new List<int> { 1, 2 }),
            ["32.raw"] = () => Write(w => { w.WriteStartObject(); w.WritePropertyName("k"); w.WriteRawValue("[1, 2]"); w.WriteEndObject(); }),
            ["35.quotedKey"] = () => Write(w => { w.WriteStartObject(); w.WriteScalarStyle(YamlScalarStyle.DoubleQuoted); w.WritePropertyName("k"); w.WriteStringValue("v"); w.WriteEndObject(); }),
        };

        private static string TopLevel()
        {
            using var stream = new MemoryStream();
            YamlSerializer.SerializeAsyncEnumerable(stream, Numbers(), topLevelValues: true, s_block).GetAwaiter().GetResult();
            return Encoding.UTF8.GetString(stream.ToArray());

            static async IAsyncEnumerable<int> Numbers()
            {
                await Task.Yield();
                yield return 1;
                yield return 2;
            }
        }

        public static IEnumerable<object[]> M4aTargetIds => s_writes.Keys.Select(k => new object[] { k });

        [Theory]
        [MemberData(nameof(M4aTargetIds))]
        public void Table1_StymlBlockWritesTheTarget(string id)
        {
            WriterParityTests.Target target = WriterParityTests.Table1Targets.Single(t => t.Id == id);
            Assert.Equal(target.Yaml, s_writes[id]());
        }

        [Fact]
        public void Table1_Specials_KeysFollowSection54()
        {
            // The M4.0 data row "25.specials" had the keys plain; keys follow the same rules as values. No standard
            // resolves "nan" or "inf", so they are plain by default and quoted with
            // QuoteForLenientReaders, as M4a quoted them.
            var specials = new Dictionary<string, double> { ["nan"] = double.NaN, ["inf"] = double.PositiveInfinity, ["ninf"] = double.NegativeInfinity };
            Assert.Equal("nan: .nan\ninf: .inf\nninf: -.inf\n", Ser(specials));
            Assert.Equal("\"nan\": .nan\n\"inf\": .inf\nninf: -.inf\n", Ser(specials, s_lenient));
        }

        public static IEnumerable<object[]> QuotingRows => Enumerable.Range(1, WriterParityCases.Strings.Length).Select(i => new object[] { i });

        public static IEnumerable<object[]> SequenceRows => Enumerable.Range(1, WriterParityCases.SequenceStrings.Length).Select(i => new object[] { i });

        [Theory]
        [MemberData(nameof(QuotingRows))]
        public void Quoting_StymlBlockWritesTheTarget(int row)
        {
            string s = WriterParityCases.Strings[row - 1];
            string target = WriterParityExpected.QuotingTargets[row - 1];
            Assert.Equal("k: " + WriterParityExpected.QuotingValueTarget(s, target) + "\n", Ser(new Dictionary<string, string> { ["k"] = s }));
            Assert.Equal(target + ": v\n", Ser(new Dictionary<string, string> { [s] = "v" }));
        }

        [Theory]
        [MemberData(nameof(SequenceRows))]
        public void SequenceItem_StymlBlockWritesTheTarget(int row)
        {
            string s = WriterParityCases.SequenceStrings[row - 1];
            Assert.Equal("- " + WriterParityExpected.SequenceTargets[row - 1] + "\n", Ser(new[] { s }));
            Assert.Equal("- " + WriterParityExpected.LenientSequenceTargets[row - 1] + "\n", Ser(new[] { s }, s_lenient));
        }

        [Theory]
        [MemberData(nameof(QuotingRows))]
        public void Quoting_StymlBlockWritesTheLenientTarget(int row)
        {
            string s = WriterParityCases.Strings[row - 1];
            Assert.Equal("k: " + WriterParityExpected.QuotingValueTarget(s, WriterParityExpected.LenientQuotingTargets[row - 1]) + "\n", Ser(new Dictionary<string, string> { ["k"] = s }, s_lenient));
            Assert.Equal(WriterParityExpected.LenientKeyTargets[row - 1] + ": v\n", Ser(new Dictionary<string, string> { [s] = "v" }, s_lenient));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 3: the cross-read

        public static IEnumerable<object[]> Documents => Directory.EnumerateFiles(BenchCorpus, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !n!.EndsWith(".min", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => new object[] { n! });

        private static string BenchCorpus => Path.Combine(AppContext.BaseDirectory, "BenchCorpus");

        [Theory]
        [MemberData(nameof(Documents))]
        public void CrossRead_Documents_ReadAsTheJsonTwin(string name)
        {
            string json = File.ReadAllText(Path.Combine(BenchCorpus, name + ".json"));
            Nodes.YamlNode node = YamlSerializer.Deserialize<Nodes.YamlNode>(json)!;
            string block = Ser(node);

            // YamlDotNet does not resolve plain scalars in an object read, so its read is checked at the parser level
            // (scalar values) and as a graph; SharpYaml resolves them, so its graph also checks the quoting.
            Assert.Equal(YdnScalars(json), YdnScalars(block));
            Assert.Equal(Canon(new YD.DeserializerBuilder().Build().Deserialize<object>(json)), Canon(new YD.DeserializerBuilder().Build().Deserialize<object>(block)));
            Assert.Equal(Canon(SY.YamlSerializer.Deserialize<object>(json)), Canon(SY.YamlSerializer.Deserialize<object>(block)));
        }

        private static string[] CrossReadStrings => WriterParityCases.Strings.Concat(WriterParityCases.SequenceStrings)
            .Concat(new[] { "1", "-1", "1.5", "1e3", "0x1F", "0o17", "0777", "1_000", "true", "False", "NULL", "~", "yes", "No", "on", "OFF", "y", "N", ".inf", "-.Inf", ".NaN", "nan", "2001-12-14", "12:30", "1:30:00", "=", "<<" })
            .Distinct()
            .ToArray();

        /// <summary>
        /// By default the strings no standard resolves are plain, and SharpYaml reads
        /// these of them as non-strings, into <c>object</c>; the list is pinned, so a change in the default set shows.
        /// </summary>
        [Fact]
        public void CrossRead_QuotingStrings_Default_SharpYamlMisreadsArePinned()
        {
            string[] strings = CrossReadStrings;
            List<object?> sharp = SY.YamlSerializer.Deserialize<List<object?>>(Ser(strings))!;
            string[] misread = strings.Where((s, i) => !(sharp[i] is string text && text == s)).ToArray();
            Assert.Equal(new[] { "tRue", "NuLL", "nan", "Infinity", "NaN" }, misread);

            // YamlDotNet reads every one back as written, as the standards do.
            Assert.Equal(strings, YdnScalars(Ser(strings)));
        }

        [Fact]
        public void CrossRead_QuotingStrings_AreStringsForBothLibraries()
        {
            // With QuoteForLenientReaders, a hard oracle: every string reads back as itself in both libraries.
            string[] strings = CrossReadStrings;
            string values = Ser(strings, s_lenient);
            List<object?> sharp = SY.YamlSerializer.Deserialize<List<object?>>(values)!;
            Assert.Equal(strings.Length, sharp.Count);
            for (int i = 0; i < strings.Length; i++)
            {
                Assert.True(sharp[i] is string text && text == strings[i], $"SharpYaml read {Escape(strings[i])} as {sharp[i]?.GetType().Name} {Escape(sharp[i]?.ToString())}");
            }

            Assert.Equal(strings, YdnScalars(values));
            Assert.Equal(strings, new YD.DeserializerBuilder().Build().Deserialize<List<string>>(values));

            // As keys, into object-keyed dictionaries, which SharpYaml resolves (M4.0's parity row).
            foreach (string s in strings.Where(s => s != "<<"))
            {
                string keyed = Ser(new Dictionary<string, int> { [s] = 1 }, s_lenient);
                object key = SY.YamlSerializer.Deserialize<Dictionary<object, object>>(keyed)!.Keys.Single();
                Assert.True(key is string k && k == s, $"SharpYaml read the key {Escape(s)} as {key.GetType().Name} {Escape(key.ToString())}");
                Assert.Equal(s, YdnScalars(keyed)[0]);
            }
        }

        [Fact]
        public void CrossRead_TypedModel_ReadsTheSame()
        {
            var root = new WriterParityCases.Root { Name = "yes", Tags = new() { "1", "a: b", "" } };
            string block = Ser(root);
            string json = YamlSerializer.Serialize(root);
            Assert.Equal(
                System.Text.Json.JsonSerializer.Serialize(new YD.DeserializerBuilder().Build().Deserialize<WriterParityCases.Root>(json)),
                System.Text.Json.JsonSerializer.Serialize(new YD.DeserializerBuilder().Build().Deserialize<WriterParityCases.Root>(block)));
            Assert.Equal(
                System.Text.Json.JsonSerializer.Serialize(SY.YamlSerializer.Deserialize<WriterParityCases.Root>(json)),
                System.Text.Json.JsonSerializer.Serialize(SY.YamlSerializer.Deserialize<WriterParityCases.Root>(block)));
        }

        private static List<string> YdnScalars(string yaml)
        {
            var parser = new YDCore.Parser(new StringReader(yaml));
            var scalars = new List<string>();
            while (parser.MoveNext())
            {
                if (parser.Current is YDEvents.Scalar scalar)
                {
                    scalars.Add(scalar.Value);
                }
            }

            return scalars;
        }

        // A canonical text of an object graph: mappings in order, sequences, and scalars with their .NET type.
        private static string Canon(object? value)
        {
            var builder = new StringBuilder();
            Append(builder, value);
            return builder.ToString();

            static void Append(StringBuilder b, object? v)
            {
                switch (v)
                {
                    case null:
                        b.Append("null");
                        break;
                    case string s:
                        b.Append("s:").Append(System.Text.Json.JsonSerializer.Serialize(s));
                        break;
                    case IDictionary d:
                        b.Append('{');
                        foreach (DictionaryEntry e in d)
                        {
                            Append(b, e.Key);
                            b.Append(':');
                            Append(b, e.Value);
                            b.Append(',');
                        }

                        b.Append('}');
                        break;
                    case IEnumerable list:
                        b.Append('[');
                        foreach (object? item in list)
                        {
                            Append(b, item);
                            b.Append(',');
                        }

                        b.Append(']');
                        break;
                    case IFormattable f:
                        b.Append(v.GetType().Name).Append(':').Append(f.ToString(null, CultureInfo.InvariantCulture));
                        break;
                    default:
                        b.Append(v.GetType().Name).Append(':').Append(v);
                        break;
                }
            }
        }

        private static string Escape(string? s) => s is null ? "null" : System.Text.Json.JsonSerializer.Serialize(s);
    }
}
