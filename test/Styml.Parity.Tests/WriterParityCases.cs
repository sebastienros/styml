// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using SY = SharpYaml;
using SYE = SharpYaml.Events;
using YD = YamlDotNet.Serialization;
using YDCore = YamlDotNet.Core;
using YDEvents = YamlDotNet.Core.Events;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// The writer rows (tables 1 and 2), as calls: each case writes one
    /// value with one library and returns the text, or "THREW Type: message" when the library throws. The text each case
    /// wrote with YamlDotNet 18.1.0 and SharpYaml 3.17.0 on 2026-11-19 is in WriterParityExpected.cs; "Styml" cases record
    /// what Styml writes at 9835d4d (JSON-compatible output), so that M4a and M4b, which change it, update them. The
    /// calls are those, reduced to one or two per row and library. VYaml is measured
    /// there and not asserted here (the parity rule covers YamlDotNet and SharpYaml).
    /// </summary>
    internal static class WriterParityCases
    {
        public sealed record Case(string Id, string Row, string Library, Func<string> Write);

        // -------------------------------------------------------------------------------------------------------------
        // Models (the evidence program's, without VYaml's attributes).

        public class Root
        {
            public string Name { get; set; } = "a";
            public int Count { get; set; } = 1;
            public List<string> Tags { get; set; } = new() { "x", "y" };
            public Dictionary<string, int> Map { get; set; } = new() { ["k"] = 1 };
            public Child Child { get; set; } = new Child { V = 2 };
            public List<Child> Items { get; set; } = new() { new Child { V = 3 } };
            public string? Nothing { get; set; }
            public List<string> Empty { get; set; } = new();
            public Dictionary<string, int> EmptyMap { get; set; } = new();
        }

        public class Child
        {
            public int V { get; set; }

            public override bool Equals(object? obj) => obj is Child c && c.V == V;
            public override int GetHashCode() => V;
            public override string ToString() => "Child(" + V + ")";
        }

        public class Pair
        {
            public Child? A { get; set; }
            public Child? B { get; set; }
        }

        public class Node
        {
            public string Name { get; set; } = "n";
            public Node? Next { get; set; }
        }

        public class Holder
        {
            public object? Value { get; set; }
        }

        public class Commented
        {
            [YD.YamlMember(Description = "the name")]
            public string Name { get; set; } = "a";
            public int Count { get; set; } = 1;
        }

        public class Ordered
        {
            [YD.YamlMember(Order = 2)]
            [SY.Serialization.YamlPropertyOrder(2)]
            [YamlPropertyOrder(2)]
            public int B { get; set; } = 2;

            [YD.YamlMember(Order = 1)]
            [SY.Serialization.YamlPropertyOrder(1)]
            [YamlPropertyOrder(1)]
            public int A { get; set; } = 1;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Calls.

        private static readonly YD.ISerializer s_yamlDotNet = new YD.SerializerBuilder().Build();

        public static string Ydn<T>(T value) => s_yamlDotNet.Serialize(value!, typeof(T));

        public static string Ydn<T>(T value, Func<YD.SerializerBuilder, YD.SerializerBuilder> configure) =>
            configure(new YD.SerializerBuilder()).Build().Serialize(value!, typeof(T));

        public static string YdnSettings<T>(T value, YDCore.EmitterSettings settings, Func<YD.SerializerBuilder, YD.SerializerBuilder>? configure = null)
        {
            YD.SerializerBuilder builder = configure?.Invoke(new YD.SerializerBuilder()) ?? new YD.SerializerBuilder();
            return YD.Serializer.FromValueSerializer(builder.BuildValueSerializer(), settings).Serialize(value!, typeof(T));
        }

        public static string YdnEvents(Action<YDCore.IEmitter> emit)
        {
            var text = new StringWriter();
            emit(new YDCore.Emitter(text));
            return text.ToString();
        }

        public static string Sy<T>(T value, SY.YamlSerializerOptions? options = null) => SY.YamlSerializer.Serialize(value, options);

        public static string SyEvents(Action<SY.IEmitter> emit, int bestIndent = 2, int bestWidth = int.MaxValue, bool forceIndentLess = false)
        {
            var text = new StringWriter();
            emit(new SY.Emitter(text, bestIndent, bestWidth, false, forceIndentLess));
            return text.ToString();
        }

        // The Styml rows record the JSON-compatible style (WriterParityTests' summary), so the style is pinned: since M4c an
        // unset style is block.
        public static string Styml<T>(T value, YamlSerializerOptions? options = null) =>
            YamlSerializer.Serialize(value, new YamlSerializerOptions(options ?? YamlSerializerOptions.Default) { WriteStyle = YamlWriterStyle.JsonCompatible });

        public static string Run(Func<string> write)
        {
            try
            {
                return write();
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException { InnerException: { } i } ? i : e;
                string message = inner.Message.Split('\n')[0];
                return "THREW " + inner.GetType().Name + ": " + (message.Length > 160 ? message[..160] + "…" : message);
            }
        }

        private static Dictionary<string, T> V<T>(T value) => new() { ["v"] = value };

        public static readonly string Words = MakeWords();

        private static string MakeWords()
        {
            var words = new StringBuilder();
            while (words.Length < 200)
            {
                words.Append("lorem ipsum dolor sit amet ");
            }
            string s = words.ToString()[..200].TrimEnd();
            return s.PadRight(200, 'x');
        }

        public static readonly Dictionary<string, List<Dictionary<string, object>>> Nested = new()
        {
            ["k"] = new() { new Dictionary<string, object> { ["a"] = 1, ["b"] = new List<int> { 1, 2 } } },
        };

        // -------------------------------------------------------------------------------------------------------------
        // The writer table, by its row numbers.

        public static IEnumerable<Case> Table1()
        {
            const string Y = "YamlDotNet", S = "SharpYaml", T = "Styml";
            var root = new Root();
            var child = new Child { V = 2 };
            var pair = new Pair { A = child, B = child };
            var flags = new List<string> { "--port=8080", "-v", "-a b", "?x" };
            var colons = new List<string> { ":x", "-1", "-.inf" };

            // 1, 1b: indentation width.
            yield return new("01.default", "1", Y, () => Ydn(root));
            yield return new("01.indent4", "1", Y, () => YdnSettings(root, YDCore.EmitterSettings.Default.WithBestIndent(4)));
            yield return new("01.default", "1", S, () => Sy(root));
            yield return new("01.indent4", "1", S, () => Sy(root, new SY.YamlSerializerOptions { IndentSize = 4 }));
            yield return new("01.default", "1", T, () => Styml(root));
            yield return new("01.indented", "1", T, () => Styml(root, new YamlSerializerOptions { WriteIndented = true }));
            yield return new("01b.default", "1b", Y, () => Ydn(Nested));
            yield return new("01b.indent4", "1b", Y, () => YdnSettings(Nested, YDCore.EmitterSettings.Default.WithBestIndent(4)));
            yield return new("01b.default", "1b", S, () => Sy(Nested));
            yield return new("01b.indent4", "1b", S, () => Sy(Nested, new SY.YamlSerializerOptions { IndentSize = 4 }));
            yield return new("01b.default", "1b", T, () => Styml(Nested));

            // 2: a sequence under a key.
            yield return new("02.indented", "2", Y, () => Ydn(root, b => b.WithIndentedSequences()));
            yield return new("02.forceIndentLess", "2", S, () => SyEvents(e => SY.Model.YamlStream.Load(new StringReader(Sy(root))).WriteTo(e), forceIndentLess: true));

            // 3, 4: collections in a sequence.
            var mappings = new List<Dictionary<string, int>> { new() { ["a"] = 1, ["b"] = 2 } };
            var sequences = new List<List<int>> { new() { 1, 2 } };
            yield return new("03.default", "3", Y, () => Ydn(mappings));
            yield return new("03.default", "3", S, () => Sy(mappings));
            yield return new("03.expanded", "3", S, () => Sy(mappings, new SY.YamlSerializerOptions { BlockSequenceMappingStyle = SY.YamlSequenceItemStyle.Expanded }));
            yield return new("04.default", "4", Y, () => Ydn(sequences));
            yield return new("04.default", "4", S, () => Sy(sequences));
            yield return new("04.compact", "4", S, () => Sy(sequences, new SY.YamlSerializerOptions { BlockSequenceSequenceStyle = SY.YamlSequenceItemStyle.Compact }));
            yield return new("03.default", "3", T, () => Styml(mappings));
            yield return new("04.default", "4", T, () => Styml(sequences));

            // 5: block, JSON-compatible, unindented.
            yield return new("05.json", "5", Y, () => Ydn(root, b => b.JsonCompatible()));
            yield return new("05.unindented", "5", S, () => Sy(root, new SY.YamlSerializerOptions { WriteIndented = false }));
            yield return new("05.unindentedReadBack", "5", S, () => Sy(SY.YamlSerializer.Deserialize<Root>(Sy(root, new SY.YamlSerializerOptions { WriteIndented = false }))));

            // 6: empty collections at the root.
            yield return new("06.roots", "6", Y, () => Ydn(new List<int>()) + " / " + Ydn(new Dictionary<string, int>()));
            yield return new("06.omitEmpty", "6", Y, () => Ydn(root, b => b.ConfigureDefaultValuesHandling(YD.DefaultValuesHandling.OmitEmptyCollections)));
            yield return new("06.roots", "6", S, () => Sy(new List<int>()) + " / " + Sy(new Dictionary<string, int>()));
            yield return new("06.roots", "6", T, () => Styml(new List<int>()) + " / " + Styml(new Dictionary<string, int>()));

            // 7: null.
            var nulls = new List<string?> { "a", null };
            yield return new("07.list", "7", Y, () => Ydn(nulls));
            yield return new("07.omitNull", "7", Y, () => Ydn(root, b => b.ConfigureDefaultValuesHandling(YD.DefaultValuesHandling.OmitNull)));
            yield return new("07.list", "7", S, () => Sy(nulls));
            yield return new("07.whenWritingNull", "7", S, () => Sy(root, new SY.YamlSerializerOptions { DefaultIgnoreCondition = SY.YamlIgnoreCondition.WhenWritingNull }));
            yield return new("07.list", "7", T, () => Styml(nulls));

            // 8: document markers (emitters only).
            yield return new("08.explicit", "8", Y, () => YdnEvents(e =>
            {
                e.Emit(new YDEvents.StreamStart());
                e.Emit(new YDEvents.DocumentStart(null, null, false));
                e.Emit(new YDEvents.Scalar("a"));
                e.Emit(new YDEvents.DocumentEnd(false));
                e.Emit(new YDEvents.StreamEnd());
            }));
            yield return new("08.explicit", "8", S, () => SyEvents(e =>
            {
                e.Emit(new SYE.StreamStart());
                e.Emit(new SYE.DocumentStart(null, null, false));
                e.Emit(new SYE.Scalar("a"));
                e.Emit(new SYE.DocumentEnd(false));
                e.Emit(new SYE.StreamEnd());
            }));

            // 9: directives (emitters only).
            yield return new("09.directives", "9", Y, () => YdnEvents(e =>
            {
                e.Emit(new YDEvents.StreamStart());
                e.Emit(new YDEvents.DocumentStart(new YDCore.Tokens.VersionDirective(new YDCore.Version(1, 2)), new YDCore.TagDirectiveCollection { new YDCore.Tokens.TagDirective("!e!", "tag:example.com,2000:") }, false));
                e.Emit(new YDEvents.Scalar(YDCore.AnchorName.Empty, new YDCore.TagName("tag:example.com,2000:x"), "a", YDCore.ScalarStyle.Any, false, false));
                e.Emit(new YDEvents.DocumentEnd(true));
                e.Emit(new YDEvents.StreamEnd());
            }));
            yield return new("09.directives", "9", S, () => SyEvents(e =>
            {
                e.Emit(new SYE.StreamStart());
                e.Emit(new SYE.DocumentStart(new SY.Tokens.VersionDirective(new SY.Version(1, 2)), new SY.TagDirectiveCollection { new SY.Tokens.TagDirective("!e!", "tag:example.com,2000:") }, false));
                e.Emit(new SYE.Scalar(null, "tag:example.com,2000:x", "a", SY.ScalarStyle.Any, false, false));
                e.Emit(new SYE.DocumentEnd(true));
                e.Emit(new SYE.StreamEnd());
            }));

            // 10: line breaks.
            yield return new("10.crlf", "10", Y, () => Ydn(new List<string> { "x", "y" }, b => b.WithNewLine("\r\n")));
            yield return new("10.textWriter", "10", S, () =>
            {
                var text = new StringWriter { NewLine = "\r\n" };
                SY.YamlSerializer.Serialize(text, new List<string> { "x", "y" });
                return text.ToString();
            });

            // 11: the end of the output.
            yield return new("11.roots", "11", Y, () => Ydn("a") + " / " + Ydn(1) + " / " + Ydn(new List<int> { 1 }));
            yield return new("11.roots", "11", S, () => Sy("a") + " / " + Sy(1) + " / " + Sy(new List<int> { 1 }));
            yield return new("11.roots", "11", T, () => Styml("a") + " / " + Styml(1) + " / " + Styml(new List<int> { 1 }));

            // 12: key order.
            yield return new("12.ordered", "12", Y, () => Ydn(new Ordered()));
            yield return new("12.ordered", "12", S, () => Sy(new Ordered()));
            yield return new("12.sorted", "12", S, () => Sy(new Dictionary<string, int> { ["b"] = 1, ["a"] = 2 }, new SY.YamlSerializerOptions { MappingOrder = SY.YamlMappingOrderPolicy.Sorted }));
            yield return new("12.ordered", "12", T, () => Styml(new Ordered()));

            // 13: implicit keys and their length.
            var k1024 = new Dictionary<string, int> { [new string('a', 1024)] = 1 };
            var k1025 = new Dictionary<string, int> { [new string('a', 1025)] = 1 };
            yield return new("13.long", "13", Y, () => Ydn(k1024).Replace("\r\n", "\n").Length + " / " + Ydn(k1025)[..3]);
            yield return new("13.maxSimpleKeyLength", "13", Y, () => YdnSettings(k1025, YDCore.EmitterSettings.Default.WithMaxSimpleKeyLength(2000))[..3]);
            yield return new("13.long", "13", S, () => Sy(k1024).Replace("\r\n", "\n").Length + " / " + Sy(k1025)[..3]);
            yield return new("13.readBack", "13", S, () => Run(() => SY.YamlSerializer.Deserialize<Dictionary<string, int>>(Sy(k1025))!.Keys.Single().Length.ToString()));

            // 14: a key with a line break.
            var lineBreakKey = new Dictionary<string, int> { ["a\nb"] = 1 };
            yield return new("14.lineBreak", "14", Y, () => Ydn(lineBreakKey));
            yield return new("14.lineBreak", "14", S, () => Sy(lineBreakKey));
            yield return new("14.lineBreak", "14", T, () => Styml(lineBreakKey));

            // 15: a collection as a key.
            var collectionKey = new Dictionary<Child, int> { [new Child { V = 2 }] = 1 };
            yield return new("15.collectionKey", "15", Y, () => Ydn(collectionKey));
            yield return new("15.collectionKey", "15", S, () => Sy(collectionKey));
            yield return new("15.collectionKey", "15", T, () => Run(() => Styml(collectionKey)));

            // 16: shared references.
            yield return new("16.default", "16", Y, () => Ydn(pair));
            yield return new("16.disableAliases", "16", Y, () => Ydn(pair, b => b.DisableAliases()));
            yield return new("16.default", "16", S, () => Sy(pair));
            yield return new("16.preserve", "16", S, () => Sy(pair, new SY.YamlSerializerOptions { ReferenceHandling = SY.YamlReferenceHandling.Preserve }));
            yield return new("16.preserveMinimal", "16", S, () => Sy(pair, new SY.YamlSerializerOptions { ReferenceHandling = SY.YamlReferenceHandling.PreserveMinimal }));
            yield return new("16.default", "16", T, () => Styml(pair));

            // 17: cycles. Each library throws or writes an alias; none overflows the stack.
            var cycle = new Node();
            cycle.Next = cycle;
            yield return new("17.default", "17", Y, () => Ydn(cycle));
            yield return new("17.default", "17", S, () => Run(() => Sy(cycle)));
            yield return new("17.preserve", "17", S, () => Sy(cycle, new SY.YamlSerializerOptions { ReferenceHandling = SY.YamlReferenceHandling.Preserve }));
            yield return new("17.default", "17", T, () => Run(() => Styml(cycle)));

            // 18: tags.
            var holder = new Holder { Value = new Child { V = 2 } };
            yield return new("18.default", "18", Y, () => Ydn(holder));
            yield return new("18.tagMapping", "18", Y, () => Ydn(holder, b => b.WithTagMapping("!child", typeof(Child))));
            yield return new("18.default", "18", S, () => Sy(holder));
            yield return new("18.default", "18", T, () => Styml(holder));

            // 19: comments.
            yield return new("19.description", "19", Y, () => Ydn(new Commented()));
            yield return new("19.emitter", "19", Y, () => YdnEvents(e =>
            {
                e.Emit(new YDEvents.StreamStart());
                e.Emit(new YDEvents.DocumentStart());
                e.Emit(new YDEvents.Comment("top", false));
                e.Emit(new YDEvents.MappingStart());
                e.Emit(new YDEvents.Scalar("k"));
                e.Emit(new YDEvents.Scalar("v"));
                e.Emit(new YDEvents.Comment("after v", true));
                e.Emit(new YDEvents.Scalar("k2"));
                e.Emit(new YDEvents.Scalar("v2"));
                e.Emit(new YDEvents.MappingEnd());
                e.Emit(new YDEvents.DocumentEnd(true));
                e.Emit(new YDEvents.StreamEnd());
            }));
            yield return new("19.api", "19", S, () => string.Join(", ", typeof(SY.Emitter).GetMethods().Concat(typeof(SY.Serialization.YamlWriter).GetMethods()).Select(m => m.Name).Where(n => n.Contains("Comment", StringComparison.OrdinalIgnoreCase)).Distinct()));

            // 20: line width.
            var words = new Dictionary<string, string> { ["k"] = Words };
            yield return new("20.default", "20", Y, () => Ydn(words));
            yield return new("20.width40", "20", Y, () => YdnSettings(words, YDCore.EmitterSettings.Default.WithBestWidth(40)));
            yield return new("20.default", "20", S, () => Sy(words));
            yield return new("20.width40", "20", S, () => SyEvents(e => SY.Model.YamlStream.Load(new StringReader(Sy(words))).WriteTo(e), 2, 40));

            // 21, 22, 23: scalar styles, quoting, multi-line strings.
            var multiLine = new Dictionary<string, string> { ["k"] = "line1\nline2\n" };
            var quoted = new Dictionary<string, string> { ["k"] = "a: b" };
            yield return new("21.styles", "21", Y, () => string.Join(", ", Enum.GetNames(typeof(YDCore.ScalarStyle))));
            yield return new("21.styles", "21", S, () => string.Join(", ", Enum.GetNames(typeof(SY.ScalarStyle))));
            yield return new("22.colonSpace", "22", Y, () => Ydn(quoted));
            yield return new("22.colonSpace", "22", S, () => Sy(quoted));
            yield return new("22.colonSpace", "22", T, () => Styml(quoted));
            yield return new("23.default", "23", Y, () => Ydn(multiLine));
            yield return new("23.default", "23", S, () => Sy(multiLine));
            yield return new("23.literal", "23", S, () => Sy(multiLine, new SY.YamlSerializerOptions { ScalarStylePreferences = new SY.YamlScalarStylePreferences { MultilineStringStyle = SY.ScalarStyle.Literal } }));
            yield return new("23.default", "23", T, () => Styml(multiLine));

            // 24: non-ASCII and HTML-sensitive characters.
            var text = new Dictionary<string, string> { ["k"] = "héllo ☃ 😀 <&'" };
            yield return new("24.default", "24", Y, () => Ydn(text));
            yield return new("24.default", "24", S, () => Sy(text));
            yield return new("24.default", "24", T, () => Styml(text));

            // 25, 26: NaN, infinities and other numbers.
            var specials = new Dictionary<string, double> { ["nan"] = double.NaN, ["inf"] = double.PositiveInfinity, ["ninf"] = double.NegativeInfinity };
            var numbers = new Dictionary<string, object> { ["a"] = 1.0, ["b"] = 0.1, ["c"] = 1e20, ["d"] = 1e-7, ["e"] = -0.0, ["f"] = float.MaxValue, ["g"] = 1.10m };
            yield return new("25.specials", "25", Y, () => Ydn(specials));
            yield return new("25.specials", "25", S, () => Sy(specials));
            yield return new("25.specials", "25", T, () => Run(() => Styml(specials)));
            yield return new("25.namedLiterals", "25", T, () => Styml(specials, new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals }));
            yield return new("26.numbers", "26", Y, () => Ydn(numbers));
            yield return new("26.numbers", "26", S, () => Sy(numbers));
            yield return new("26.numbers", "26", T, () => Styml(numbers));

            // 27, 28, 28b, 29: typed values.
            var date = new DateTime(2001, 12, 14, 21, 59, 43, 100, DateTimeKind.Utc);
            yield return new("27.dateTime", "27", Y, () => Ydn(V(date)));
            yield return new("27.dateTime", "27", S, () => Sy(V(date)));
            yield return new("27.dateTime", "27", T, () => Styml(V(date)));
            yield return new("27.dateTimeOffset", "27", T, () => Styml(V(new DateTimeOffset(date))));
            yield return new("27.dateOnly", "27", S, () => Sy(V(new DateOnly(2001, 12, 14))));
            yield return new("27.dateOnly", "27", T, () => Styml(V(new DateOnly(2001, 12, 14))));
            yield return new("28.timeSpan", "28", Y, () => Ydn(V(new TimeSpan(10, 2, 3))));
            yield return new("28.timeSpan", "28", S, () => Sy(V(new TimeSpan(10, 2, 3))));
            yield return new("28.timeSpan", "28", T, () => Styml(V(new TimeSpan(10, 2, 3))));
            yield return new("28.timeOnly", "28", S, () => Sy(V(new TimeOnly(21, 59, 43))));
            yield return new("28.timeOnly", "28", T, () => Styml(V(new TimeOnly(21, 59, 43))));
            yield return new("28b.uri", "28b", Y, () => Ydn(V(new Uri("https://example.com/a?b=c"))));
            yield return new("28b.version", "28b", Y, () => Ydn(V(new Version(1, 10))));
            yield return new("28b.uri", "28b", S, () => Run(() => Sy(V(new Uri("https://example.com/a?b=c")))));
            yield return new("28b.version", "28b", S, () => Sy(V(new Version(1, 10))));
            yield return new("28b.uri", "28b", T, () => Styml(V(new Uri("https://example.com/a?b=c"))));
            yield return new("28b.version", "28b", T, () => Styml(V(new Version(1, 10))));
            yield return new("28c.flags", "28c", Y, () => Ydn(flags));
            yield return new("28c.flags", "28c", S, () => Sy(flags));
            yield return new("28d.colons", "28d", Y, () => Ydn(colons));
            yield return new("28d.colons", "28d", S, () => Sy(colons));
            yield return new("29.bytes", "29", Y, () => Ydn(V(new byte[] { 1, 2, 3 })));
            yield return new("29.bytes", "29", S, () => Sy(V(new byte[] { 1, 2, 3 })));
            yield return new("29.bytes", "29", T, () => Styml(V(new byte[] { 1, 2, 3 })));

            // 30, 31: several documents, and an IAsyncEnumerable at the root.
            yield return new("30.twoCalls", "30", Y, () =>
            {
                var output = new StringWriter();
                s_yamlDotNet.Serialize(output, new Child { V = 1 });
                s_yamlDotNet.Serialize(output, new Child { V = 2 });
                return output.ToString();
            });
            yield return new("30.twoCalls", "30", S, () =>
            {
                var output = new StringWriter();
                SY.YamlSerializer.Serialize(output, new Child { V = 1 });
                SY.YamlSerializer.Serialize(output, new Child { V = 2 });
                return output.ToString();
            });
            yield return new("30.topLevelValues", "30", T, () =>
            {
                var output = new MemoryStream();
                YamlSerializer.SerializeAsyncEnumerable(output, Values(1, 2), topLevelValues: true).GetAwaiter().GetResult();
                return Encoding.UTF8.GetString(output.ToArray());
            });
            yield return new("31.asyncEnumerable", "31", Y, () => Ydn(Values(1, 2)));
            yield return new("31.asyncEnumerable", "31", S, () => Sy(Values(1, 2)));
            yield return new("31.asyncEnumerable", "31", T, () =>
            {
                var output = new MemoryStream();
                YamlSerializer.SerializeAsync(output, Values(1, 2), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }).GetAwaiter().GetResult();
                return Encoding.UTF8.GetString(output.ToArray());
            });

            // 32: raw text.
            yield return new("32.raw", "32", T, () => Write(w =>
            {
                w.WriteStartObject();
                w.WritePropertyName("k");
                w.WriteRawValue("[1, 2]");
                w.WriteEndObject();
            }));
            yield return new("32.rawInvalid", "32", T, () => Run(() => Write(w => w.WriteRawValue("[1,"))));

            // 33, 34: the two refused YamlDotNet settings.
            yield return new("33.canonical", "33", Y, () => YdnSettings(new Child { V = 2 }, YDCore.EmitterSettings.Default.Canonical()));
            yield return new("34.skipAnchorName", "34", Y, () => YdnSettings(pair, YDCore.EmitterSettings.Default.WithoutAnchorName()));
            yield return new("34.skipAnchorNameReadBack", "34", Y, () =>
            {
                Pair read = new YD.DeserializerBuilder().Build().Deserialize<Pair>(YdnSettings(pair, YDCore.EmitterSettings.Default.WithoutAnchorName()));
                return (read.A?.ToString() ?? "null") + " / " + (read.B?.ToString() ?? "null");
            });

            // 35: keys always quoted.
            yield return new("35.quotedKeys", "35", S, () => SyEvents(e =>
            {
                e.Emit(new SYE.StreamStart());
                e.Emit(new SYE.DocumentStart());
                e.Emit(new SYE.MappingStart());
                e.Emit(new SYE.Scalar(null, null, "k", SY.ScalarStyle.DoubleQuoted, false, true));
                e.Emit(new SYE.Scalar("v"));
                e.Emit(new SYE.MappingEnd());
                e.Emit(new SYE.DocumentEnd(true));
                e.Emit(new SYE.StreamEnd());
            }));

            // Whether the libraries resolve plain keys read into object keys. Styml's serializer reads keys
            // into Dictionary<object, …> through YamlElement, which is not JSON-shaped here, so its row is the throw.
            const string ResolvableKeys = "true: a\n1: b\n1.5: c\n";
            yield return new("05.4.objectKeys", "5.4", Y, () => DescribeKeys(new YD.DeserializerBuilder().Build().Deserialize<Dictionary<object, string>>(ResolvableKeys)));
            yield return new("05.4.objectKeys", "5.4", S, () => Run(() => DescribeKeys(SY.YamlSerializer.Deserialize<Dictionary<object, string>>(ResolvableKeys)!)));
            yield return new("05.4.objectKeys", "5.4", T, () => Run(() => DescribeKeys(YamlSerializer.Deserialize<Dictionary<object, string>>(ResolvableKeys)!)));
        }

        private static string DescribeKeys(Dictionary<object, string> read) =>
            string.Join(", ", read.Select(p => p.Key.GetType().Name + " " + p.Key + " = " + p.Value));

        private static async IAsyncEnumerable<int> Values(params int[] values)
        {
            foreach (int v in values)
            {
                await Threading.Tasks.Task.Yield();
                yield return v;
            }
        }

        private static string Write(Action<Utf8YamlWriter> write)
        {
            var buffer = new Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.JsonCompatible }))
            {
                write(writer);
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Table 2: quoting. Value position is Dictionary<string, string> { ["k"] = s }, key position { [s] = "v" }, and
        // sequence-item position (table 2c of the evidence) List<string> { s }.

        public static readonly string[] Strings =
        {
            "", " ", "a", "hello world", " lead", "trail ", "true", "True", "TRUE", "tRue", "false", "yes", "Yes", "YES", "yEs", "no", "No", "on", "On", "off", "OFF", "y", "Y", "n", "N", "null", "Null", "NULL", "NuLL", "~", "0", "1", "-1", "+1", "1.0", "1e3", "1E3", ".5", "1.", "0x1F", "0o17", "0b101", "0777", "010", "1_000", "1_000.5", "190:20:30", "1:30", "12:30:45.5", ".inf", ".Inf", ".INF", "-.inf", "+.inf", ".nan", ".NaN", ".NAN", "inf", "nan", "Infinity", "NaN", "2001-12-14", "2001-12-14t21:59:43.10-05:00", "=", "<<", "-", "--", "---", "--- x", "...", "... x", "?", "? x", ":", ": x", "a: b", "a:b", "a #b", "a#b", "#a", "@a", "`a", "!a", "&a", "*a", "|a", ">a", "'a", "\"a", "%a", "[a", "]a", "{a", "}a", ",a", "a,b", "a[b", "a{b", "-a", "- a", "a\nb", "a\n", "\na", "a\tb", "\ta", "a\u0001b", "a\u0085b", "a\u2028b", "é", "\ufeffa", "1.10", "0.0", "-0", "1,000", "100%", "12e", "0x", "+", ".", "a\\b", "a'b", "a\"b", "C:\\x", "--port=8080", "-v", "-a b", "?x", ":x", "-x:y", "-.5", "-1:30", "+190:20:30.15", "?x y", "-- a", "-1.5", "-0x1F",
        };

        public static readonly string[] SequenceStrings = { "--port=8080", "-v", "-a b", "--", "?x", ":x", "-1", "-", "- a", "---", "--- a", "-.inf", "-x:y", "?x y", "-- a", "-0x1F" };

        public static IEnumerable<Case> Table2()
        {
            for (int i = 0; i < Strings.Length; i++)
            {
                string s = Strings[i];
                string n = "Q" + (i + 1).ToString("000");
                var value = new Dictionary<string, string> { ["k"] = s };
                var key = new Dictionary<string, string> { [s] = "v" };
                yield return new(n + ".value", n, "YamlDotNet", () => Run(() => Ydn(value)));
                yield return new(n + ".key", n, "YamlDotNet", () => Run(() => Ydn(key)));
                yield return new(n + ".value", n, "SharpYaml", () => Run(() => Sy(value)));
                yield return new(n + ".key", n, "SharpYaml", () => Run(() => Sy(key)));
            }
            for (int i = 0; i < SequenceStrings.Length; i++)
            {
                var item = new List<string> { SequenceStrings[i] };
                string n = "Q2c" + (i + 1).ToString("00");
                yield return new(n + ".item", n, "YamlDotNet", () => Run(() => Ydn(item)));
                yield return new(n + ".item", n, "SharpYaml", () => Run(() => Sy(item)));
            }
        }

        public static IEnumerable<Case> All => Table1().Concat(Table2());

        public static string Key(Case c) => c.Library + "." + c.Id;
    }
}
