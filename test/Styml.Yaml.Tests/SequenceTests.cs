// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M3c oracles: the yaml-test-suite through sequences (oracle 2), JSON
// equivalence with Utf8JsonReader on the same sequences (oracle 4), copies of a reader across windows (oracle 7),
// the serializer through a PipeReader, and the allocations of single-segment reads (oracle 8). The chunking oracle's
// sequence forms (oracle 1) are in ChunkingTests.AssertEverySequence.
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;
using StjReader = System.Text.Json.Utf8JsonReader;

namespace System.Text.Yaml.Tests
{
    public class SequenceTests
    {
        private static readonly YamlReaderOptions s_suiteOptions = new() { AllowMultipleValues = true, MaxDepth = 256 };

        public static IEnumerable<object[]> SuiteCases() => YamlTestSuite.AllCaseIds().Select(id => new object[] { id });

        public static IEnumerable<object[]> CorpusItems() => JsonCorpus.Items.Select((_, i) => new object[] { i });

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 2: the yaml-test-suite's event traces through sequences.

        [Theory]
        [MemberData(nameof(SuiteCases))]
        public void SuiteTrace_ThroughSequences(string id)
        {
            byte[] input = YamlTestSuite.Load(id).Input;
            string expected = SuiteTrace(SequenceFactory.Create(input, new[] { input.Length }).Value);
            Assert.Equal(expected, SuiteTrace(SequenceFactory.Uniform(input, 1).Value));
            for (int split = 0; split <= input.Length; split++)
            {
                Assert.Equal(expected, SuiteTrace(SequenceFactory.Split(input, split).Value));
            }
        }

        private static string SuiteTrace(ReadOnlySequence<byte> sequence)
        {
            try
            {
                var reader = new Utf8YamlReader(sequence, s_suiteOptions);
                return string.Join("\n", YamlEventTrace.Read(ref reader));
            }
            catch (YamlException)
            {
                return "error";
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 3: CR LF line breaks, multi-byte characters and the BOM, split at every offset.

        [Theory]
        [MemberData(nameof(SuiteCases))]
        public void SuiteInputWithCrLf_EverySequence(string id)
        {
            byte[] input = YamlTestSuite.Load(id).Input;
            if (Array.IndexOf(input, (byte)'\n') < 0)
            {
                return;
            }
            byte[] crlf = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(input).Replace("\r\n", "\n").Replace("\n", "\r\n"));
            ChunkingTests.AssertEverySequence(crlf, s_suiteOptions);
        }

        [Theory]
        [InlineData("ключ: значение\n\"é\": 'ü'\n- &锚 x\n")]
        [InlineData("a: |\n  多行\n  文本 😀\n# комментарий 😀\nb: !標籤 c\n")]
        [InlineData("[é, \"😀\", {ü: ä}]")]
        [InlineData("﻿a: 1\n")]
        [InlineData("﻿[1, 2]")]
        public void MultiByteCharacters_EverySequence(string yaml)
        {
            byte[] input = Encoding.UTF8.GetBytes(yaml);
            if (yaml[0] == '﻿')
            {
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, input.Take(3).ToArray());
            }
            ChunkingTests.AssertEverySequence(input, s_suiteOptions);
            ChunkingTests.AssertEverySequence(input, new YamlReaderOptions { AllowMultipleValues = true, MaxDepth = 256, DisallowImplicitCollectionKeys = true });
        }

        // ---------------------------------------------------------------------------------------------------------
        // The M6a documents (bench/corpus), the inputs of the M3c spike: 7-byte, 4 KB and 64 KB segments, and 1-byte
        // segments with and without empty segments between up to 256 KB, in both option settings.

        public static IEnumerable<object[]> AllBenchDocuments() =>
            Directory.EnumerateFiles(BenchCorpus, "*.*")
                .Where(f => f.EndsWith(".yaml", StringComparison.Ordinal) || f.EndsWith(".json", StringComparison.Ordinal))
                .Select(f => new object[] { Path.GetFileName(f) })
                .OrderBy(a => (string)a[0], StringComparer.Ordinal);

        [Theory]
        [MemberData(nameof(AllBenchDocuments))]
        public void BenchDocument_ThroughSequences(string name)
        {
            byte[] input = File.ReadAllBytes(Path.Combine(BenchCorpus, name));
            foreach (YamlReaderOptions options in new[] { default(YamlReaderOptions), new YamlReaderOptions { DisallowImplicitCollectionKeys = true } })
            {
                ChunkingTests.Trace oneShot = ChunkingTests.ReadOneShot(input, options);
                Assert.Null(oneShot.Error);
                var sequences = new List<(string, SequenceFactory.Sequence)>
                {
                    ("7-byte", SequenceFactory.Uniform(input, 7)),
                    ("4 KB", SequenceFactory.Uniform(input, 4096)),
                    ("64 KB", SequenceFactory.Uniform(input, 65536)),
                };
                if (input.Length <= 256 * 1024)
                {
                    sequences.Add(("1-byte", SequenceFactory.Uniform(input, 1)));
                    sequences.Add(("1-byte with empty segments", SequenceFactory.Uniform(input, 1, emptyBetween: true)));
                }
                foreach ((string how, SequenceFactory.Sequence sequence) in sequences)
                {
                    ChunkingTests.Trace actual = ChunkingTests.ReadSequence(sequence, options);
                    Assert.Null(actual.Error);
                    Assert.Equal(oneShot.Tokens.Count, actual.Tokens.Count);
                    for (int i = 0; i < oneShot.Tokens.Count; i++)
                    {
                        if (oneShot.Tokens[i] != actual.Tokens[i])
                        {
                            Assert.Fail($"{name}, {how} segments: token {i} differs.\n  one-shot: {oneShot.Tokens[i]}\n  sequence: {actual.Tokens[i]}");
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 4: JSON equivalence on sequences, against System.Text.Json on the same segments.

        [Theory]
        [MemberData(nameof(CorpusItems))]
        public void MatchesUtf8JsonReaderOnSequences(int index)
        {
            JsonCorpus.Item item = JsonCorpus.Items[index];
            foreach (SequenceFactory.Sequence sequence in new[] { SequenceFactory.Uniform(item.Utf8, 1), SequenceFactory.Random(item.Utf8, index), SequenceFactory.Random(item.Utf8, index + 1, maxSize: 7) })
            {
                string? difference = CompareWithUtf8JsonReader(sequence.Value);
                Assert.True(difference is null, $"{item.Source} ({sequence.SegmentCount} segments): {difference}");
            }
        }

        [Theory]
        [InlineData("[123", ",4]", true)]
        [InlineData("[\"abc", "\",1]", true)]
        [InlineData("{\"a", "\":1}", true)]
        [InlineData("[true", ",1]", false)]
        [InlineData("[tr", "ue,1]", true)]
        [InlineData("[1,123", "]", true)]
        public void HasValueSequenceFollowsUpstream(string first, string second, bool expected)
        {
            byte[] a = Encoding.UTF8.GetBytes(first), b = Encoding.UTF8.GetBytes(second);
            ReadOnlySequence<byte> sequence = SequenceFactory.Create(a.Concat(b).ToArray(), new[] { a.Length, b.Length }).Value;
            Assert.Null(CompareWithUtf8JsonReader(sequence));

            // The token that ends next to the boundary: the last scalar of the first segment.
            var reader = new Utf8YamlReader(sequence);
            bool? seen = null;
            while (reader.Read())
            {
                if (reader.TokenStartIndex < a.Length && reader.TokenType is YamlTokenType.Number or YamlTokenType.String or YamlTokenType.PropertyName or YamlTokenType.True)
                {
                    seen = reader.HasValueSequence;
                }
            }
            Assert.Equal(expected, seen);
        }

        private static string? CompareWithUtf8JsonReader(ReadOnlySequence<byte> json)
        {
            var expected = new StjReader(json);
            var actual = new Utf8YamlReader(json);
            for (int i = 0; ; i++)
            {
                bool expectedRead = expected.Read();
                bool actualRead = actual.Read();
                if (expectedRead != actualRead)
                {
                    return $"token {i}: Utf8JsonReader.Read() = {expectedRead}, Utf8YamlReader.Read() = {actualRead}";
                }
                if (!expectedRead)
                {
                    return null;
                }

                string at = $"token {i} ({expected.TokenType} at {expected.TokenStartIndex})";
                if ((int)expected.TokenType != (int)actual.TokenType)
                {
                    return $"{at}: TokenType {actual.TokenType}";
                }
                if (expected.HasValueSequence != actual.HasValueSequence)
                {
                    return $"{at}: HasValueSequence {actual.HasValueSequence}, expected {expected.HasValueSequence}";
                }
                byte[] expectedValue = expected.HasValueSequence ? expected.ValueSequence.ToArray() : expected.ValueSpan.ToArray();
                byte[] actualValue = actual.HasValueSequence ? actual.ValueSequence.ToArray() : actual.ValueSpan.ToArray();
                if (!expectedValue.AsSpan().SequenceEqual(actualValue))
                {
                    return $"{at}: value bytes differ";
                }
                if (expected.ValueIsEscaped != actual.ValueIsEscaped)
                {
                    return $"{at}: ValueIsEscaped {actual.ValueIsEscaped}";
                }
                if (expected.TokenStartIndex != actual.TokenStartIndex || expected.BytesConsumed != actual.BytesConsumed)
                {
                    return $"{at}: TokenStartIndex/BytesConsumed {actual.TokenStartIndex}/{actual.BytesConsumed}, expected {expected.TokenStartIndex}/{expected.BytesConsumed}";
                }
                if (!ReferenceEquals(expected.Position.GetObject(), actual.Position.GetObject()) || expected.Position.GetInteger() != actual.Position.GetInteger())
                {
                    return $"{at}: Position ({actual.Position.GetInteger()}) is not upstream's ({expected.Position.GetInteger()}, same segment: {ReferenceEquals(expected.Position.GetObject(), actual.Position.GetObject())})";
                }
                if (expected.CurrentDepth != actual.CurrentDepth)
                {
                    return $"{at}: CurrentDepth {actual.CurrentDepth}";
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 7: copies of a reader across windows. The input is one line of equal-length records, so the same
        // offsets within a 4 KB segment hold different records in successive windows; every third record holds an
        // implicit collection key, so the probe's full scan caches decisions (keys and not keys) in every window.

        internal static byte[] RecordsWithSomeCollectionKeys(int count)
        {
            var records = new List<string>();
            for (int i = 0; i < count; i++)
            {
                // 40 bytes each, so 4096 / 40 is not a multiple of 3 and the key records move within the windows.
                string record = i % 3 == 0
                    ? $"{{\"r\": {i:D6}, \"m\": {{[a, b]: v}}}}"
                    : $"{{\"r\": {i:D6}, \"k\": [1, 2, 3], \"z\": 0}}";
                records.Add(record.PadRight(40));
            }
            return Encoding.UTF8.GetBytes("[" + string.Join(",", records) + "]");
        }

        // 41 bytes per record with its comma: segments of 25 or 7 records shift every window by whole records (so a
        // collection recurs at the same window offset) and by a number of records not divisible by 3 (so that offset
        // holds a key in one window and not in the next). 4096 and 61 do not align, for contrast.
        [Theory]
        [InlineData(41 * 25)]
        [InlineData(41 * 7)]
        [InlineData(4096)]
        [InlineData(61)]
        public void CopyReadsTheSameTokensAfterTheOriginalCrossedWindows(int segment)
        {
            byte[] input = RecordsWithSomeCollectionKeys(600);
            ChunkingTests.Trace oneShot = ChunkingTests.ReadOneShot(input, default);
            Assert.Null(oneShot.Error);
            Assert.Contains(oneShot.Tokens, t => t.Contains("|Complex|"));
            SequenceFactory.Sequence sequence = SequenceFactory.Uniform(input, segment);

            // Many copy points. After each, the original reads on far enough to move its window and scan in the new
            // one (writing to the cache the copy shares), then the copy reads to the end. With cache entries relative
            // to the window, the copy can hit an entry about a different byte at the same window offset and take the
            // wrong key decision.
            var reader = new Utf8YamlReader(sequence.Value);
            int at = 0;
            int checks = 0;
            while (true)
            {
                Utf8YamlReader copy = reader;
                Utf8YamlReader ahead = reader;
                for (int i = 0; i < 150 && ahead.Read(); i++)
                {
                }

                var tokens = new List<string>();
                while (copy.Read())
                {
                    tokens.Add(ChunkingTests.Describe(ref copy, 0));
                }
                Assert.Equal(oneShot.Tokens.Skip(at), tokens);
                checks++;

                // The next copy point: 37 tokens further.
                int step = 0;
                while (step < 37 && reader.Read())
                {
                    step++;
                }
                at += step;
                if (step < 37)
                {
                    break;
                }
            }
            Assert.True(checks > 100);
        }

#if NET
        [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_totalConsumed")]
        private static extern ref long WindowBase(ref Utf8YamlReader reader);

        [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_yamlProbeCachePos")]
        private static extern ref long[]? CachePositions(ref Utf8YamlReader reader);

        [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_yamlProbeCacheCount")]
        private static extern ref int CacheCount(ref Utf8YamlReader reader);

        [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_yamlProbeCacheCursor")]
        private static extern ref int CacheCursor(ref Utf8YamlReader reader);
#endif

        /// <summary>
        /// The probe cache across a window move, with a copy that holds pending entries. One line of 600 8-byte entries in a root flow sequence, every third a key (<c>[a]: v</c>), the
        /// others not (<c>[a]</c>); entry e starts at offset 8(e + 1).
        /// - The root <c>[</c> needs 1024 characters of its line before it is decided, so the first window grows by
        ///   doubling from the first segment to about 2 KB or more, and its first scan caches the entries in reach. A copy
        ///   taken just after it has pending entries, in the arrays it shares with the original.
        /// - Sweeping the segment size moves the end of that first window; where it falls inside an entry's <c>[a]</c>,
        ///   the entry's probe cannot decide, and the next window starts at its <c>[</c>, a whole number of entries
        ///   further. The original's scan there records entries at the same window offsets as the copy's pending ones,
        ///   from index 0, for entries whose decision differs.
        /// - With window-relative cache offsets the copy then takes the original's decisions (the negative
        ///   control); with absolute offsets it reads the one-shot trace.
        /// </summary>
        [Fact]
        public void CopyWithPendingCacheEntriesAcrossAWindowMove()
        {
            var text = new StringBuilder("[       ");
            for (int e = 0; e < 600; e++)
            {
                text.Append(e % 3 == 0 ? "[a]: v, " : "[a]   , ");
            }
            text.Append("x]");
            byte[] input = Encoding.UTF8.GetBytes(text.ToString());
            ChunkingTests.Trace oneShot = ChunkingTests.ReadOneShot(input, default);
            Assert.Null(oneShot.Error);

            int cases = 0;
            var failures = new List<string>();
            for (int segment = 1; segment <= 64; segment++)
            {
                SequenceFactory.Sequence sequence = SequenceFactory.Uniform(input, segment);
                var reader = new Utf8YamlReader(sequence.Value);
                int at = 0;
                while (true)
                {
                    // Copy points along the whole line: a copy and the original collide only when both are past the
                    // first window (whose base is 0) and their window bases differ by a multiple of the entry length.
                    Utf8YamlReader copy = reader;
                    Utf8YamlReader original = reader;
                    try
                    {
                        for (int i = 0; i < 400 && original.Read(); i++)
                        {
                        }
                    }
                    catch (YamlException e)
                    {
                        failures.Add($"segment {segment}, the original after {at} tokens: {e.Message}");
                        break;
                    }

                    string state = "";
#if NET
                    long[]? positions = CachePositions(ref copy);
                    state = $"copy base {WindowBase(ref copy)}, original base {WindowBase(ref original)}, copy cursor {CacheCursor(ref copy)} of {CacheCount(ref copy)}, shared entries from the copy's cursor [{(positions is null ? "" : string.Join(", ", positions.Skip(CacheCursor(ref copy)).Take(6)))}]";
#endif
                    var tokens = new List<string>();
                    try
                    {
                        while (copy.Read())
                        {
                            tokens.Add(ChunkingTests.Describe(ref copy, 0));
                        }
                    }
                    catch (YamlException e)
                    {
                        tokens.Add("error: " + e.Message);
                    }
                    cases++;

                    List<string> expected = oneShot.Tokens.Skip(at).ToList();
                    if (!expected.SequenceEqual(tokens))
                    {
                        int k = 0;
                        while (k < Math.Min(expected.Count, tokens.Count) && expected[k] == tokens[k])
                        {
                            k++;
                        }
                        failures.Add($"segment {segment}, copy after {at} tokens ({state}): token {at + k} is {(k < tokens.Count ? tokens[k] : "missing")}, one-shot {(k < expected.Count ? expected[k] : "missing")}");
                    }

                    int step = 0;
                    try
                    {
                        while (step < 11 && reader.Read())
                        {
                            step++;
                        }
                    }
                    catch (YamlException e)
                    {
                        failures.Add($"segment {segment}, the reader itself after {at + step} tokens: {e.Message}");
                        break;
                    }
                    at += step;
                    if (step < 11)
                    {
                        break;
                    }
                }
            }
            Assert.True(failures.Count == 0, $"{failures.Count} failures in {cases} copies:\n" + string.Join("\n", failures.Take(8)));
            Assert.True(cases > 5000, $"{cases} cases");
        }

        [Theory]
        [InlineData(4096)]
        [InlineData(997)]
        public void TrySkipRestoresAcrossWindows(int segment)
        {
            byte[] input = RecordsWithSomeCollectionKeys(500);
            ChunkingTests.Trace oneShot = ChunkingTests.ReadOneShot(input, default);
            Assert.Null(oneShot.Error);

            // The data ends before the root array closes, and is not final: TrySkip on the root array reads through
            // every window (scanning in each), fails for want of data, and restores the reader.
            SequenceFactory.Sequence sequence = SequenceFactory.Uniform(input.AsSpan(0, input.Length - 1).ToArray(), segment);
            var reader = new Utf8YamlReader(sequence.Value, isFinalBlock: false, new YamlReaderState());
            Assert.True(reader.Read(), "the first Read");
            Assert.False(reader.TrySkip(), "TrySkip on partial data");
            var tokens = new List<string> { ChunkingTests.Describe(ref reader, 0) };
            while (reader.Read())
            {
                tokens.Add(ChunkingTests.Describe(ref reader, 0));
            }
            // The same as a span reader over the same non-final bytes, which waits on the last record as well.
            byte[] partial = input.AsSpan(0, input.Length - 1).ToArray();
            var span = new Utf8YamlReader(partial, isFinalBlock: false, new YamlReaderState());
            var expected = new List<string>();
            while (span.Read())
            {
                expected.Add(ChunkingTests.Describe(ref span, 0));
            }
            Assert.Equal(expected, tokens);
            Assert.Equal(oneShot.Tokens.Take(tokens.Count), tokens);
        }

        // ---------------------------------------------------------------------------------------------------------
        // The serializer: polymorphic records (metadata is read with checkpoint and restore) through a PipeReader over
        // small segments, with bracket-colons in strings so that the probe's full scan runs in many windows.

        [YamlPolymorphic]
        [YamlDerivedType(typeof(Circle), "circle")]
        [YamlDerivedType(typeof(Square), "square")]
        public class Shape
        {
            public string? Name { get; set; }

            public List<int>? Points { get; set; }
        }

        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        public class Square : Shape
        {
            public double Side { get; set; }
        }

        internal static byte[] ShapesOnOneLine(int count)
        {
            var parts = new List<string>();
            for (int i = 0; i < count; i++)
            {
                parts.Add(i % 2 == 0
                    ? $"{{\"$type\":\"circle\",\"Name\":\"c{i}]: x\",\"Points\":[{i},{i + 1}],\"Radius\":{i}.5}}"
                    : $"{{\"$type\":\"square\",\"Name\":\"s{i}\",\"Points\":[{i}],\"Side\":{i}}}");
            }
            return Encoding.UTF8.GetBytes("[" + string.Join(",", parts) + "]");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(4096)]
        public async Task PolymorphicRecordsThroughAPipe(int segment)
        {
            byte[] input = ShapesOnOneLine(3000);
            string expected = Canonical(YamlSerializer.Deserialize<List<Shape>>(input)!);
            PipeReader pipe = PipeReader.Create(SequenceFactory.Uniform(input, segment).Value);
            List<Shape>? actual = await YamlSerializer.DeserializeAsync<List<Shape>>(pipe);
            Assert.Equal(expected, Canonical(actual!));
        }

        private static string Canonical(List<Shape> shapes) => string.Join(";", shapes.Select(s => s switch
        {
            Circle c => $"circle:{c.Name}:{string.Join(",", c.Points!)}:{c.Radius}",
            Square q => $"square:{q.Name}:{string.Join(",", q.Points!)}:{q.Side}",
            _ => "?",
        }));

        // ---------------------------------------------------------------------------------------------------------
        // YamlDocument's JSON-shape guard on tokens that span segments (found on the net481 leg: it checked numbers,
        // booleans and null through ValueSpan, which is empty when HasValueSequence is set).

        // A single-token value is checked on the reader itself (YamlDocument.TryParseValue), not on a copy of its bytes,
        // so a number, boolean or null that spans segments reaches the guard as a multi-segment token.
        [Theory]
        [InlineData("1234567")]
        [InlineData("-0.5e3")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("null")]
        public void YamlDocumentParsesJsonShapedScalarsThatSpanSegments(string json)
        {
            byte[] input = Encoding.UTF8.GetBytes(json);
            using YamlDocument expected = YamlDocument.Parse(input);
            foreach (SequenceFactory.Sequence sequence in new[] { SequenceFactory.Uniform(input, 1), SequenceFactory.Uniform(input, 2) })
            {
                var reader = new Utf8YamlReader(sequence.Value);
                Assert.True(reader.Read());
                Assert.True(reader.HasValueSequence);
                using YamlDocument actual = YamlDocument.ParseValue(ref reader);
                Assert.Equal(expected.RootElement.GetRawText(), actual.RootElement.GetRawText());
            }
        }

        public class WithObjectMember
        {
            public object? Value { get; set; }

            public List<double>? Numbers { get; set; }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(4096)]
        public async Task ObjectMembersThroughAPipe(int segment)
        {
            var parts = new List<string>();
            for (int i = 0; i < 400; i++)
            {
                string value = (i % 4) switch { 0 => $"{i * 1001}", 1 => "true", 2 => "null", _ => "false" };
                parts.Add($"{{\"Value\": {value}, \"Numbers\": [{i}.25, {i * 7}]}}");
            }
            byte[] input = Encoding.UTF8.GetBytes("[" + string.Join(",", parts) + "]");
            List<WithObjectMember> expected = YamlSerializer.Deserialize<List<WithObjectMember>>(input)!;
            List<WithObjectMember>? actual = await YamlSerializer.DeserializeAsync<List<WithObjectMember>>(PipeReader.Create(SequenceFactory.Uniform(input, segment).Value));
            Assert.Equal(YamlSerializer.Serialize(expected), YamlSerializer.Serialize(actual));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle 8: single-segment reads allocate nothing new.

        public static IEnumerable<object[]> BenchDocuments() =>
            new[] { "k8s-m.yaml", "k8s-m.json", "records-m.yaml", "openapi-m.json", "workflow-m.yaml", "compose-m.yaml" }.Select(n => new object[] { n });

        [Theory]
        [MemberData(nameof(BenchDocuments))]
        public void SingleSegmentReadsDoNotAllocate(string name)
        {
            byte[] input = File.ReadAllBytes(Path.Combine(BenchCorpus, name));
            Read(new ReadOnlySequence<byte>(input));
            ReadSpan(input);

            // The reader takes the same path on every pass over the same input, so a read that allocates allocates on
            // every pass, and one pass that allocates nothing proves the claim. A single window can count bytes nobody
            // allocated: when a background GC, started by other tests' allocations, pauses this thread inside it,
            // GetAllocatedBytesForCurrentThread can grow by up to the thread's allocation quantum (24, 312 and 4800
            // bytes seen in the full suite; a stress run reproduces it, and DOTNET_gcConcurrent=0 makes it go away).
            // The pause does not repeat on the next pass, so a few passes absorb it; a real allocation fails all of
            // them, and the message lists each pass.
            long[] allocated = new long[5];
            for (int pass = 0; pass < allocated.Length; pass++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                Read(new ReadOnlySequence<byte>(input));
                ReadSpan(input);
                allocated[pass] = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated[pass] == 0)
                {
                    return;
                }
            }

            Assert.Fail($"Every pass allocated: {string.Join(", ", allocated)} bytes.");

            static void Read(ReadOnlySequence<byte> sequence)
            {
                var reader = new Utf8YamlReader(sequence);
                while (reader.Read())
                {
                }
            }

            static void ReadSpan(byte[] input)
            {
                var reader = new Utf8YamlReader(input);
                while (reader.Read())
                {
                }
            }
        }

        // The M6a documents, copied from bench/corpus into the test output (Styml.Yaml.Tests.csproj).
        internal static string BenchCorpus { get; } = Path.Combine(AppContext.BaseDirectory, "BenchCorpus");
    }
}
