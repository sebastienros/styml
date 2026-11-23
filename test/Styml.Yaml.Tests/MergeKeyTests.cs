// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5b, merge keys (decision 3, opt-in). The three inputs are the
    /// evidence rows: merged entries sit at the position of '&lt;&lt;' (both
    /// libraries), an own key after the merge overrides in place (both), and an own key before it wins (SharpYaml, and
    /// the merge type's text; YamlDotNet's MergingParser lets the merge override it).
    /// </summary>
    public class MergeKeyTests
    {
        private static readonly YamlSerializerOptions s_on = new() { ResolveMergeKeys = true };
        private static readonly YamlDocumentOptions s_domOn = new() { ResolveMergeKeys = true };

        public class XY { public int X { get; set; } public int Y { get; set; } }

        public record XYRecord(int X, int Y);

        public class Listed { public List<int> L { get; } = new(); }

        public static IEnumerable<object[]> Evidence() =>
        [
            ["b: &b {x: 1}\nc:\n  <<: *b\n  y: 2", new[] { ("x", 1), ("y", 2) }],
            ["b: &b {x: 1, y: 1}\nc:\n  <<: *b\n  x: 2", new[] { ("x", 2), ("y", 1) }],
            ["b: &b {x: 1, y: 1}\nc:\n  x: 2\n  <<: *b", new[] { ("x", 2), ("y", 1) }],
        ];

        [Theory]
        [MemberData(nameof(Evidence))]
        public void DictionaryAndDomGiveTheLibrariesOrder(string yaml, (string Key, int Value)[] expected)
        {
            var dictionary = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(yaml, s_on)!;
            Assert.Equal(expected, dictionary["c"].Select(p => (p.Key, p.Value)));

            using YamlDocument document = YamlDocument.Parse(yaml, s_domOn);
            Assert.Equal(expected, document.RootElement.GetProperty("c").EnumerateObject().Select(p => (p.Name, p.Value.GetInt32())));

            // A class and a constructor read the same values.
            XY xy = YamlSerializer.Deserialize<Dictionary<string, XY>>(yaml.Replace("x", "X").Replace("y", "Y"), s_on)!["c"];
            Assert.Equal(expected.Select(e => e.Value), new[] { xy.X, xy.Y }.Take(expected.Length));
            XYRecord record = YamlSerializer.Deserialize<Dictionary<string, XYRecord>>(yaml.Replace("x", "X").Replace("y", "Y"), s_on)!["c"];
            Assert.Equal(xy.X, record.X);
            Assert.Equal(xy.Y, record.Y);
        }

        [Fact]
        public void SequencesOfSourcesAndInlineSources()
        {
            // An earlier source wins over a later one; inline mappings merge too.
            const string Yaml = "a: &a {x: 1}\nb: &b {x: 2, z: 2}\nc: {<<: [*a, *b], w: 0}\nd: {<<: {i: 1}, j: 2}";
            var read = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(Yaml, s_on)!;
            Assert.Equal(new[] { ("x", 1), ("z", 2), ("w", 0) }, read["c"].Select(p => (p.Key, p.Value)));
            Assert.Equal(new[] { ("i", 1), ("j", 2) }, read["d"].Select(p => (p.Key, p.Value)));

            // A source's own merges apply first.
            var nested = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>("a: &a {x: 1}\nb: &b {<<: *a, y: 2}\nc: {<<: *b}", s_on)!;
            Assert.Equal(new[] { ("x", 1), ("y", 2) }, nested["c"].Select(p => (p.Key, p.Value)));

            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>("a: &a [1]\nc: {<<: *a}", s_on));
        }

        [Fact]
        public void AnOwnKeyAfterTheMergeIsReadOnce()
        {
            // Under Populate, the own value replaces the merged one rather than being appended to it.
            var options = new YamlSerializerOptions { ResolveMergeKeys = true, PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate };
            Listed listed = YamlSerializer.Deserialize<Dictionary<string, Listed>>("b: &b {L: [1]}\nc:\n  <<: *b\n  L: [2]", options)!["c"];
            Assert.Equal(new[] { 2 }, listed.L);

            // And it is not a duplicate key.
            var strict = new YamlSerializerOptions { ResolveMergeKeys = true, AllowDuplicateProperties = false };
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<string, XY>>("b: &b {X: 1}\nc:\n  <<: *b\n  X: 2", strict)!["c"].X);
        }

        [Fact]
        public void OffByDefault_AndOnlyThePlainKeyMerges()
        {
            // Off: '<<' is an ordinary key.
            var off = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, YamlElement>>>("b: &b {x: 1}\nc: {<<: *b}")!;
            Assert.Equal(new[] { "<<" }, off["c"].Keys);
            using (YamlDocument document = YamlDocument.Parse("b: &b {x: 1}\nc: {<<: *b}"))
            {
                Assert.Equal(new[] { "<<" }, document.RootElement.GetProperty("c").EnumerateObject().Select(p => p.Name));
            }

            // On: a quoted '<<' is a string key.
            var quoted = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, YamlElement>>>("b: &b {x: 1}\nc: {\"<<\": *b}", s_on)!;
            Assert.Equal(new[] { "<<" }, quoted["c"].Keys);
        }

        [Fact]
        public async Task StreamsAndReaders()
        {
            const string Yaml = "b: &b {x: 1}\nc:\n  <<: *b\n  y: 2";
            var expected = new[] { ("x", 1), ("y", 2) };
            var options = new YamlSerializerOptions { ResolveMergeKeys = true, DefaultBufferSize = 1 };
            var fromStream = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)), options)!;
            Assert.Equal(expected, fromStream["c"].Select(p => (p.Key, p.Value)));
            var fromAsync = (await YamlSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, int>>>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)), options))!;
            Assert.Equal(expected, fromAsync["c"].Select(p => (p.Key, p.Value)));

            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(Yaml));
            var fromReader = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(ref reader, s_on)!;
            Assert.Equal(expected, fromReader["c"].Select(p => (p.Key, p.Value)));

            // DeserializeAsyncEnumerable streams documents; merge keys need a whole one.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.DeserializeAsyncEnumerable<int>(new MemoryStream(), topLevelValues: true, s_on));
        }

        public static IEnumerable<object[]> Settings() =>
        [
            [YamlDuplicateKeyHandling.Error],
            [YamlDuplicateKeyHandling.LastWins],
            [YamlDuplicateKeyHandling.FirstWins],
        ];

        [Theory]
        [MemberData(nameof(Settings))]
        public void AKeyRepeatedInsideOneSourceIsJudgedAtTheMerge(YamlDuplicateKeyHandling handling)
        {
            // M5c: the source is often an unmapped x- member, so nothing else would judge it.
            var options = new YamlSerializerOptions { ResolveMergeKeys = true, DuplicateKeyHandling = handling };
            var dom = new YamlDocumentOptions { ResolveMergeKeys = true, DuplicateKeyHandling = handling };
            foreach (string yaml in new[] { "x-b: &b {a: 1, a: 2}\nc: {<<: *b}", "c: {<<: {a: 1, a: 2}}" })
            {
                if (handling == YamlDuplicateKeyHandling.Error)
                {
                    Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(yaml.Replace("x-b", "c0"), options));
                    Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, XY>>(yaml.Replace("x-b", "z"), options));
                    Assert.ThrowsAny<YamlException>(() => YamlDocument.Parse(yaml, dom));
                    continue;
                }

                int expected = handling == YamlDuplicateKeyHandling.LastWins ? 2 : 1;
                Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(yaml.Replace("x-b", "c0"), options)!["c"]["a"]);
                using YamlDocument document = YamlDocument.Parse(yaml, dom);
                Assert.Equal(expected, document.RootElement.GetProperty("c").GetProperty("a").GetInt32());
            }

            // Across sources and against own keys it is merge order, never a duplicate.
            Assert.Equal(new[] { ("x", 1), ("y", 9) }, YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(
                "a: &a {x: 1}\nb: &b {x: 2, y: 2}\nc: {<<: [*a, *b], y: 9}", options)!["c"].Select(p => (p.Key, p.Value)));
        }

        public class Unmapped { public string? X { get; set; } public Dictionary<string, object>? M { get; set; } }

        [Theory]
        [MemberData(nameof(Settings))]
        public void OutsideMergedMappingsOnAndOffGiveTheSameResult(YamlDuplicateKeyHandling handling)
        {
            // The invariant of 5.3, A6: the rewrite does not turn the serializer's per-consumer Error into a
            // whole-document check (a repeated unmapped key and a repeat inside a skipped value are not judged).
            const string Yaml = "X: a\nU: 1\nU: 2\nS: {k: 1, k: 2}\nb: &b {z: 1}\nM: {<<: *b}";
            var off = new YamlSerializerOptions { DuplicateKeyHandling = handling };
            var on = new YamlSerializerOptions { DuplicateKeyHandling = handling, ResolveMergeKeys = true };
            Assert.Equal("a", YamlSerializer.Deserialize<Unmapped>(Yaml, off)!.X);
            Unmapped merged = YamlSerializer.Deserialize<Unmapped>(Yaml, on)!;
            Assert.Equal("a", merged.X);
            Assert.Equal(1, ((YamlElement)merged.M!["z"]).GetInt32());

            // A repeat the consumer judges is judged the same either way.
            const string Judged = "X: a\nX: b\nb: &b {z: 1}\nM: {<<: *b}";
            if (handling == YamlDuplicateKeyHandling.Error)
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Unmapped>(Judged, off));
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Unmapped>(Judged, on));
            }
            else
            {
                Assert.Equal(YamlSerializer.Deserialize<Unmapped>(Judged, off)!.X, YamlSerializer.Deserialize<Unmapped>(Judged, on)!.X);
            }
        }

        [Fact]
        public void UnderJsonWithAcceptLenientScalars_TheRewriteReadsAsTheOptionOff()
        {
            // 5.3, C11: the rewrite's parse carries AcceptLenientScalars, so a plain string under Json reads the same with
            // ResolveMergeKeys on and off.
            const string Yaml = "name: foo\nb: &b {x: 1}\nc: {y: 2}\n\"<<\": 1";
            var off = new YamlSerializerOptions { Schema = YamlSchemaKind.Json, AcceptLenientScalars = true };
            var on = new YamlSerializerOptions(off) { ResolveMergeKeys = true };
            var expected = YamlSerializer.Deserialize<Dictionary<string, object>>(Yaml, off)!;
            var merged = YamlSerializer.Deserialize<Dictionary<string, object>>(Yaml, on)!;
            Assert.Equal("foo", ((YamlElement)merged["name"]).GetString());
            Assert.Equal(expected.Keys, merged.Keys);

            // And a real merge under Json with the option.
            var c = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>("b: &b {x: foo}\nc: {<<: *b, y: bar}", on)!["c"];
            Assert.Equal(new[] { ("x", "foo"), ("y", "bar") }, c.Select(p => (p.Key, ((YamlElement)p.Value).GetString())));
        }

        [Fact]
        public void MergedScalarsKeepTheirSpelling()
        {
            // The rewrite writes scalars as the source spells them, so a member reads what it would have read there.
            const string Values = "{s: True, n: ~, m: NULL, e: , f: false, q: 'True', num: 0x1F}";
            var merged = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string?>>>($"b: &b {Values}\nc:\n  <<: *b\n", s_on)!;
            var direct = YamlSerializer.Deserialize<Dictionary<string, string?>>(Values)!;
            Assert.Equal(direct, merged["c"]);
        }

        [Fact]
        public void MergedCopiesCountAgainstTheBound()
        {
            var tight = new YamlSerializerOptions { ResolveMergeKeys = true, MaxAliasExpansion = 3 };
            Assert.Equal(1, YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>("b: &b {x: 1}\nc: {<<: *b}", tight)!["c"]["x"]);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>("b: &b {x: 1, y: 2}\nc: {<<: *b}\nd: {<<: *b}", tight));
        }
    }
}
