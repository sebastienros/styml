// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5c, decision 1 (Error by default): the three
    /// settings per consumer, the key equality rule, the default under each grammar, and what a skipped value must
    /// still satisfy. Under the JSON grammar the default is LastWins; this project runs the YAML grammar in every leg.
    /// </summary>
    public class DuplicateKeyTests
    {
        private static YamlSerializerOptions With(YamlDuplicateKeyHandling handling) => new() { DuplicateKeyHandling = handling };

        public class C { public string? A { get; set; } }

        public record R(string A);

        public class Required { public required string A { get; set; } }

        public class WithInt { public int N { get; set; } }

        public class WithList { public List<int> L { get; set; } = new(); }

        public class WithExtension
        {
            public string? A { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object>? Extra { get; set; }
        }

        public record WithExtensionRecord(string A)
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement>? Extra { get; set; }
        }

        public class Many
        {
            public int P0 { get; set; } public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
            public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; } public int P7 { get; set; }
            public int P8 { get; set; } public int P9 { get; set; } public int P10 { get; set; } public int P11 { get; set; }
            public int P12 { get; set; } public int P13 { get; set; } public int P14 { get; set; } public int P15 { get; set; }
            public int P16 { get; set; } public int P17 { get; set; } public int P18 { get; set; } public int P19 { get; set; }
            public int P20 { get; set; } public int P21 { get; set; } public int P22 { get; set; } public int P23 { get; set; }
            public int P24 { get; set; } public int P25 { get; set; } public int P26 { get; set; } public int P27 { get; set; }
            public int P28 { get; set; } public int P29 { get; set; } public int P30 { get; set; } public int P31 { get; set; }
            public int P32 { get; set; } public int P33 { get; set; } public int P34 { get; set; } public int P35 { get; set; }
            public int P36 { get; set; } public int P37 { get; set; } public int P38 { get; set; } public int P39 { get; set; }
            public int P40 { get; set; } public int P41 { get; set; } public int P42 { get; set; } public int P43 { get; set; }
            public int P44 { get; set; } public int P45 { get; set; } public int P46 { get; set; } public int P47 { get; set; }
            public int P48 { get; set; } public int P49 { get; set; } public int P50 { get; set; } public int P51 { get; set; }
            public int P52 { get; set; } public int P53 { get; set; } public int P54 { get; set; } public int P55 { get; set; }
            public int P56 { get; set; } public int P57 { get; set; } public int P58 { get; set; } public int P59 { get; set; }
            public int P60 { get; set; } public int P61 { get; set; } public int P62 { get; set; } public int P63 { get; set; }
            public int P64 { get; set; } public int P65 { get; set; }
        }

        [Fact]
        public void TheDefaultIsError_AndAllowDuplicatePropertiesIsItsView()
        {
            Assert.Equal(YamlDuplicateKeyHandling.Error, new YamlSerializerOptions().DuplicateKeyHandling);
            Assert.False(new YamlSerializerOptions().AllowDuplicateProperties);
            Assert.Equal(YamlDuplicateKeyHandling.Error, default(YamlDocumentOptions).DuplicateKeyHandling);
            Assert.Equal(YamlDuplicateKeyHandling.Error, new YamlSerializerOptions(YamlSerializerDefaults.Strict).DuplicateKeyHandling);

            var options = new YamlSerializerOptions { AllowDuplicateProperties = true };
            Assert.Equal(YamlDuplicateKeyHandling.LastWins, options.DuplicateKeyHandling);
            options.DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins;
            Assert.True(options.AllowDuplicateProperties);
            options.AllowDuplicateProperties = false;
            Assert.Equal(YamlDuplicateKeyHandling.Error, options.DuplicateKeyHandling);

            var document = new YamlDocumentOptions { AllowDuplicateProperties = true };
            Assert.Equal(YamlDuplicateKeyHandling.LastWins, document.DuplicateKeyHandling);

            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { DuplicateKeyHandling = (YamlDuplicateKeyHandling)3 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlDocumentOptions { DuplicateKeyHandling = (YamlDuplicateKeyHandling)3 });

            // The copy constructor keeps it.
            Assert.Equal(YamlDuplicateKeyHandling.FirstWins, new YamlSerializerOptions(With(YamlDuplicateKeyHandling.FirstWins)).DuplicateKeyHandling);
        }

        public static IEnumerable<object[]> Settings() =>
        [
            [YamlDuplicateKeyHandling.Error],
            [YamlDuplicateKeyHandling.LastWins],
            [YamlDuplicateKeyHandling.FirstWins],
        ];

        private static void Expect<T>(YamlDuplicateKeyHandling handling, Func<T> read, T last, T first)
        {
            switch (handling)
            {
                case YamlDuplicateKeyHandling.Error:
                    Assert.Contains("Duplicate", Assert.ThrowsAny<YamlException>(() => read()).Message);
                    break;
                case YamlDuplicateKeyHandling.LastWins:
                    Assert.Equal(last, read());
                    break;
                default:
                    Assert.Equal(first, read());
                    break;
            }
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void PerConsumer(YamlDuplicateKeyHandling handling)
        {
            YamlSerializerOptions options = With(handling);
            Expect(handling, () => YamlSerializer.Deserialize<C>("A: x\nA: y", options)!.A, "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<R>("A: x\nA: y", options)!.A, "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<Required>("A: x\nA: y", options)!.A, "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<Dictionary<string, string>>("a: x\na: y", options)!["a"], "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<IDictionary<string, string>>("a: x\na: y", options)!["a"], "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<IReadOnlyDictionary<string, string>>("a: x\na: y", options)!["a"], "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<ImmutableDictionary<string, string>>("a: x\na: y", options)!["a"], "y", "x");
            Expect(handling, () => (string?)YamlSerializer.Deserialize<System.Collections.Hashtable>("a: x\na: y", options)!["a"]?.ToString(), "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<Dictionary<int, string>>("1: x\n\"1\": y", options)![1], "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<WithExtension>("A: a\nk: x\nk: y", options)!.Extra!["k"].ToString(), "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<WithExtensionRecord>("A: a\nk: x\nk: y", options)!.Extra!["k"].GetString(), "y", "x");
            Expect(handling, () => YamlSerializer.Deserialize<Many>("P1: 1\nP64: 2\nP64: 3", options)!.P64, 3, 2);

            // Populate: under LastWins each occurrence populates (System.Text.Json's behaviour), so a list accumulates.
            var populate = new YamlSerializerOptions(options) { PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate };
            Expect(handling, () => string.Join(",", YamlSerializer.Deserialize<WithList>("L: [1]\nL: [2]", populate)!.L), "1,2", "1");
        }

        [Fact]
        public void FirstWins_ALaterValueIsReadForSyntaxAndNotConverted()
        {
            YamlSerializerOptions options = With(YamlDuplicateKeyHandling.FirstWins);
            Assert.Equal(1, YamlSerializer.Deserialize<WithInt>("N: 1\nN: abc", options)!.N);
            Assert.Equal("x", YamlSerializer.Deserialize<Dictionary<string, string>>("a: x\na: [1, {b: 2}]", options)!["a"]);
            Assert.Equal(1, YamlSerializer.Deserialize<Dictionary<string, int>>("a: 1\na: abc", options)!["a"]);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<WithInt>("N: 1\nN: [abc", options));

            // An anchor in the skipped value is still recorded for later aliases.
            Assert.Equal("v", YamlSerializer.Deserialize<Dictionary<string, string>>("a: x\na: &k v\nb: *k", options)!["b"]);

            // Depth still applies to the skipped value.
            var shallow = new YamlSerializerOptions(options) { MaxDepth = 2 };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>("a: 1\na: [[[1]]]", shallow));
        }

        [Fact]
        public async Task FirstWins_ThroughStreamsInSmallBuffers()
        {
            var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins, DefaultBufferSize = 1 };
            const string Yaml = "a: x\nb: {c: [1, 2, {d: e}]}\nb: {c: 3}\nm: {k: 1}\nm: {k: [2, 3]}\n";
            var read = await YamlSerializer.DeserializeAsync<Dictionary<string, YamlElement>>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)), options);
            Assert.Equal(YamlValueKind.Object, read!["b"].ValueKind);
            Assert.Equal(YamlValueKind.Array, read["b"].GetProperty("c").ValueKind);

            var typed = await YamlSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, object>>>(new MemoryStream(Encoding.UTF8.GetBytes("a: {k: 1}\na: {k: [2]}\n")), options);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)typed!["a"]["k"]).ValueKind);
        }

        [Fact]
        public void NotJudged_ARepeatedUnmappedKeyAndARepeatInsideASkippedValue()
        {
            // As SharpYaml under its three settings (evidence: "repeated keys no member maps"); YamlDotNet's check
            // judges the unmapped key but not the skipped value.
            foreach (object[] row in Settings())
            {
                YamlSerializerOptions options = With((YamlDuplicateKeyHandling)row[0]);
                Assert.Equal("a", YamlSerializer.Deserialize<C>("A: a\nU: 1\nU: 2", options)!.A);
                Assert.Equal("a", YamlSerializer.Deserialize<C>("A: a\nU: {k: 1, k: 2}", options)!.A);
            }
        }

        [Fact]
        public void KeyEquality_IsTheConsumersRule()
        {
            YamlSerializerOptions error = new();

            // Untyped sites compare decoded text, as both libraries: a, "a" and 'a' are one key, and so are 1 and "1"
            // (an int and a str, distinct in YAML 1.2: the recorded choice of 5.3, A2); 1 and 0x1 are two.
            foreach (string yaml in new[] { "a: 1\n\"a\": 2", "a: 1\n'a': 2", "1: x\n\"1\": y", "'a''b': 1\n\"a'b\": 2", "\"\\x61\": 1\na: 2" })
            {
                Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(yaml));
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>(yaml, error));
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>(yaml, error));
            }

            foreach (string yaml in new[] { "1: x\n0x1: y", "~: x\nnull: y", "true: x\nTrue: y", "A: x\na: y" })
            {
                using YamlDocument document = YamlDocument.Parse(yaml);
                Assert.Equal(2, document.RootElement.EnumerateObject().Count());
                Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, error)!.Count);
            }

            // Typed keys compare the converted key: 1 and 01 are one int.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, string>>("1: x\n01: y", error));

            // Members compare the member they match: A and a under PropertyNameCaseInsensitive.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<C>("A: x\na: y", new YamlSerializerOptions { PropertyNameCaseInsensitive = true }));

            // An alias key is its anchored scalar's text.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("&k a: 1\n*k : 2", error));
        }

        // Seeded collections: a duplicate is a property of the
        // document. A key that appears once overrides what the target held, under every setting; a key repeated in the
        // input is judged over the seed.

        public class PopulatedMember
        {
            [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
            public Dictionary<string, int> D { get; set; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        public class ReadOnlyMember
        {
            [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
            public Dictionary<string, int> D { get; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        public class PopulatedHashtable
        {
            [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
            public System.Collections.Hashtable D { get; set; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        public class SeededExtension
        {
            public int A { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> Extra { get; set; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        public class SeededElementExtension
        {
            public int A { get; set; }

            [YamlExtensionData]
            public Dictionary<string, YamlElement> Extra { get; set; } = new() { ["seed"] = default, ["k"] = default };
        }

        public class SeededNodeExtension
        {
            public int A { get; set; }

            [YamlExtensionData]
            public YamlObject Extra { get; set; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        public class SeededExtensionCtor
        {
            public SeededExtensionCtor(int a) => A = a;

            public int A { get; }

            [YamlExtensionData]
            public Dictionary<string, object> Extra { get; set; } = new() { ["seed"] = 0, ["k"] = 0 };
        }

        private static int Int(object? value) => value switch
        {
            YamlElement e => e.GetInt32(),
            YamlNode n => (int)n,
            _ => Convert.ToInt32(value),
        };

        [Theory]
        [MemberData(nameof(Settings))]
        public void SeededCollections_TheDocumentsKeysAreJudged(YamlDuplicateKeyHandling handling)
        {
            YamlSerializerOptions options = With(handling);
            var shapes = new (string Name, Func<string, (int Seed, int K)> Read)[]
            {
                ("populated member", y => { var v = YamlSerializer.Deserialize<PopulatedMember>("D: " + y, options)!; return (v.D["seed"], v.D["k"]); }),
                ("read-only member", y => { var v = YamlSerializer.Deserialize<ReadOnlyMember>("D: " + y, options)!; return (v.D["seed"], v.D["k"]); }),
                ("non-generic dictionary", y => { var v = YamlSerializer.Deserialize<PopulatedHashtable>("D: " + y, options)!; return (Int(v.D["seed"]), Int(v.D["k"])); }),
                ("extension data", y => { var v = YamlSerializer.Deserialize<SeededExtension>(y, options)!; return (Int(v.Extra["seed"]), Int(v.Extra["k"])); }),
                ("element extension data", y => { var v = YamlSerializer.Deserialize<SeededElementExtension>(y, options)!; return (v.Extra["seed"].ValueKind == YamlValueKind.Undefined ? 0 : v.Extra["seed"].GetInt32(), v.Extra["k"].GetInt32()); }),
                ("node extension data", y => { var v = YamlSerializer.Deserialize<SeededNodeExtension>(y, options)!; return (Int(v.Extra["seed"]), Int(v.Extra["k"])); }),
                ("extension data, constructor", y => { var v = YamlSerializer.Deserialize<SeededExtensionCtor>("{A: 1, " + y.TrimStart('{'), options)!; return (Int(v.Extra["seed"]), Int(v.Extra["k"])); }),
            };

            foreach ((string name, Func<string, (int Seed, int K)> read) in shapes)
            {
                // Once: overrides the seed, under every setting.
                Assert.True((0, 1) == read("{k: 1}"), name);

                // Repeated in the input: judged over the seed.
                if (handling == YamlDuplicateKeyHandling.Error)
                {
                    Assert.ThrowsAny<YamlException>(() => read("{k: 1, k: 2}"));
                }
                else
                {
                    Assert.True((0, handling == YamlDuplicateKeyHandling.LastWins ? 2 : 1) == read("{k: 1, k: 2}"), name);
                }
            }
        }

        [Fact]
        public void SeededCollections_InStreams()
        {
            var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins, DefaultBufferSize = 1 };
            PopulatedMember member = YamlSerializer.Deserialize<PopulatedMember>(new MemoryStream(Encoding.UTF8.GetBytes("D: {k: 1, k: 2, n: 3}")), options)!;
            Assert.Equal((0, 1, 3), (member.D["seed"], member.D["k"], member.D["n"]));
            var error = new YamlSerializerOptions { DefaultBufferSize = 1 };
            Assert.Equal(1, YamlSerializer.Deserialize<PopulatedMember>(new MemoryStream(Encoding.UTF8.GetBytes("D: {k: 1}")), error)!.D["k"]);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<PopulatedMember>(new MemoryStream(Encoding.UTF8.GetBytes("D: {k: 1, k: 2}")), error));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void TheDom(YamlDuplicateKeyHandling handling)
        {
            var options = new YamlDocumentOptions { DuplicateKeyHandling = handling };
            Expect(handling, () => { using YamlDocument d = YamlDocument.Parse("a: 1\nb: 2\na: 3", options); return d.RootElement.GetProperty("a").GetInt32(); }, 3, 1);
            Expect(handling, () => { using YamlDocument d = YamlDocument.Parse("a: 1\nb: 2\na: 3", options); return d.RootElement.EnumerateObject().Count(); }, 3, 3);
            Expect(handling, () => { using YamlDocument d = YamlDocument.Parse("a: 1\nb: 2\na: 3", options); return d.RootElement.Clone().GetProperty("a").GetInt32(); }, 3, 1);

            // Past 16 names the check uses its rented table; nested objects are checked too.
            string many = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"k{i}: {i}"));
            Expect(handling, () => { using YamlDocument d = YamlDocument.Parse(many + "\nk7: x", options); return d.RootElement.GetProperty("k7").GetRawText(); }, "x", "7");
            Expect(handling, () => { using YamlDocument d = YamlDocument.Parse("o: [{p: {q: 1, q: 2}}]", options); return d.RootElement.GetProperty("o")[0].GetProperty("p").GetProperty("q").GetInt32(); }, 2, 1);

            // No repeat, no error, at any size.
            using (YamlDocument d = YamlDocument.Parse(many, options))
            {
                Assert.Equal(40, d.RootElement.EnumerateObject().Count());
            }

            // The serializer's object sites take its handling.
            Expect(handling, () => ((YamlElement)YamlSerializer.Deserialize<object>("a: 1\na: 2", With(handling))!).GetProperty("a").GetInt32(), 2, 1);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void Nodes(YamlDuplicateKeyHandling handling)
        {
            YamlSerializerOptions options = With(handling);
            if (handling == YamlDuplicateKeyHandling.Error)
            {
                // System.Text.Json's exception on the eager path, which the imported YamlObject_DuplicatePropertyThrows
                // tests assert in every leg.
                Assert.Throws<ArgumentException>(() => YamlSerializer.Deserialize<YamlObject>("a: 1\na: 2", options));
                Assert.Throws<ArgumentException>(() => YamlSerializer.Deserialize<YamlNode>("[{a: 1, a: 2}]", options));
                Assert.ThrowsAny<YamlException>(() => YamlNode.Parse("a: 1\na: 2"));
                return;
            }

            int expected = handling == YamlDuplicateKeyHandling.LastWins ? 2 : 1;
            Assert.Equal(expected, (int)YamlSerializer.Deserialize<YamlObject>("a: 1\nb: 0\na: 2", options)!["a"]!);
            Assert.Equal(expected, (int)YamlSerializer.Deserialize<YamlNode>("[{a: 1, a: 2}]", options)![0]!["a"]!);
            Assert.Equal(expected, (int)YamlNode.Parse("a: 1\nb: 0\na: 2", documentOptions: new YamlDocumentOptions { DuplicateKeyHandling = handling })!["a"]!);
            Assert.Equal(2, YamlSerializer.Deserialize<YamlObject>("a: 1\nb: 0\na: 2", options)!.Count);
        }
    }
}
