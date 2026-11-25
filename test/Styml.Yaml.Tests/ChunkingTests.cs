// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Exit criterion 4: every suite input and every JSON corpus item, split at every byte offset with
    /// isFinalBlock: false and resumed through CurrentState, yields the one-shot token trace.
    /// </summary>
    public class ChunkingTests
    {
        /// <summary>Items up to this size are split independently at every offset (n reads of O(n) each).</summary>
        internal const int IndependentSplitLimit = 16 * 1024;

        private static readonly YamlReaderOptions s_options = new() { AllowMultipleValues = true, MaxDepth = 256 };

        public static IEnumerable<object[]> SuiteCases() => YamlTestSuite.AllCaseIds().Select(id => new object[] { id });

        public static IEnumerable<object[]> CorpusItems() => JsonCorpus.Items.Select((_, i) => new object[] { i });

        [Theory]
        [MemberData(nameof(SuiteCases))]
        public void SuiteInput_EverySplit(string id)
        {
            byte[] input = YamlTestSuite.Load(id).Input;
            AssertEverySplit(input, s_options);
        }

        [Theory]
        [MemberData(nameof(CorpusItems))]
        public void CorpusItem_EverySplit(int index)
        {
            byte[] input = JsonCorpus.Items[index].Utf8;
            AssertEverySplit(input, s_options);
        }

        // The same oracle with implicit collection keys turned off, where a
        // '[' or '{' is never probed.
        private static readonly YamlReaderOptions s_noCollectionKeys = new() { AllowMultipleValues = true, MaxDepth = 256, DisallowImplicitCollectionKeys = true };

        [Theory]
        [MemberData(nameof(SuiteCases))]
        public void SuiteInput_EverySplit_NoImplicitCollectionKeys(string id)
        {
            AssertEverySplit(YamlTestSuite.Load(id).Input, s_noCollectionKeys);
        }

        [Theory]
        [MemberData(nameof(CorpusItems))]
        public void CorpusItem_EverySplit_NoImplicitCollectionKeys(int index)
        {
            AssertEverySplit(JsonCorpus.Items[index].Utf8, s_noCollectionKeys);
        }

        // The same oracle with the YAML 1.3 draft restrictions: a refusal must come at the same place however the
        // input is cut.
        private static readonly YamlReaderOptions s_yaml13Draft = new() { AllowMultipleValues = true, MaxDepth = 256, ApplyYaml13DraftRestrictions = true };

        [Theory]
        [MemberData(nameof(SuiteCases))]
        public void SuiteInput_EverySplit_Yaml13DraftRestrictions(string id)
        {
            AssertEverySplit(YamlTestSuite.Load(id).Input, s_yaml13Draft);
        }

        internal static void AssertEverySplit(byte[] input, YamlReaderOptions options)
        {
            Trace oneShot = ReadOneShot(input, options);

            if (input.Length <= IndependentSplitLimit)
            {
                for (int split = 0; split <= input.Length; split++)
                {
                    Trace chunked = ReadSplit(input, split, options);
                    AssertSame(oneShot, chunked, $"split at {split}");
                }
            }

            // Byte-by-byte feeding: the reader is resumed through CurrentState at every offset.
            Trace incremental = ReadIncrementally(input, options);
            AssertSame(oneShot, incremental, "byte-by-byte");

            AssertEverySequence(input, options, oneShot);
        }

        /// <summary>
        /// M3c (oracle 1): the same input as a multi-segment sequence gives
        /// the one-shot trace: two segments split at every offset, 1-byte segments with and without empty segments
        /// between, 7-byte segments, and a non-final prefix resumed over the rest. Larger inputs get 1-byte (up to
        /// 256 KB) and 4 KB segments only.
        /// </summary>
        internal static void AssertEverySequence(byte[] input, YamlReaderOptions options, Trace? oneShot = null)
        {
            oneShot ??= ReadOneShot(input, options);

            if (input.Length <= IndependentSplitLimit)
            {
                for (int split = 0; split <= input.Length; split++)
                {
                    AssertSame(oneShot, ReadSequence(SequenceFactory.Split(input, split), options), $"sequence split at {split}");
                }
                for (int split = 0; split <= input.Length; split += Math.Max(1, input.Length / 16))
                {
                    AssertSame(oneShot, ReadSequenceResumed(input, split, options), $"non-final sequence prefix of {split}, resumed");
                }
                AssertSame(oneShot, ReadSequence(SequenceFactory.Uniform(input, 7), options), "7-byte segments");
            }
            else
            {
                AssertSame(oneShot, ReadSequence(SequenceFactory.Uniform(input, 4096), options), "4 KB segments");
            }
            if (input.Length <= 256 * 1024)
            {
                AssertSame(oneShot, ReadSequence(SequenceFactory.Uniform(input, 1), options), "1-byte segments");
                AssertSame(oneShot, ReadSequence(SequenceFactory.Uniform(input, 1, emptyBetween: true), options), "1-byte segments, empty segments between");
            }
        }

        internal static Trace ReadSequence(SequenceFactory.Sequence sequence, YamlReaderOptions options)
        {
            var trace = new Trace();
            try
            {
                var reader = new Utf8YamlReader(sequence.Value, isFinalBlock: true, new YamlReaderState(options));
                DrainSequence(ref reader, sequence, 0, trace.Tokens);
            }
            catch (YamlException e)
            {
                trace.Error = e.Message;
            }
            return trace;
        }

        /// <summary>A non-final multi-segment prefix (7-byte segments), then the rest as another sequence from CurrentState.</summary>
        private static Trace ReadSequenceResumed(byte[] input, int prefixLength, YamlReaderOptions options)
        {
            var trace = new Trace();
            try
            {
                SequenceFactory.Sequence prefix = SequenceFactory.Uniform(input.AsSpan(0, prefixLength).ToArray(), 7);
                var first = new Utf8YamlReader(prefix.Value, isFinalBlock: false, new YamlReaderState(options));
                DrainSequence(ref first, prefix, 0, trace.Tokens);
                int consumed = checked((int)first.BytesConsumed);
                SequenceFactory.Sequence rest = SequenceFactory.Uniform(input.AsSpan(consumed).ToArray(), 7);
                var second = new Utf8YamlReader(rest.Value, isFinalBlock: true, first.CurrentState);
                DrainSequence(ref second, rest, consumed, trace.Tokens);
            }
            catch (YamlException e)
            {
                trace.Error = e.Message;
            }
            return trace;
        }

        /// <summary>Reads the tokens and checks the multi-segment contract of every token.</summary>
        private static void DrainSequence(ref Utf8YamlReader reader, SequenceFactory.Sequence sequence, long offset, List<string> tokens)
        {
            while (reader.Read())
            {
                string token = Describe(ref reader, offset);
                // ReadOnlySequence.GetOffset is not in System.Memory for net481; the length of the slice up to Position is.
                long positionOffset = sequence.Value.Slice(sequence.Value.Start, reader.Position).Length;
                if (positionOffset != reader.BytesConsumed)
                {
                    Assert.Fail($"Position is at {positionOffset}, BytesConsumed is {reader.BytesConsumed}: {token}");
                }
                if (reader.HasValueSequence)
                {
                    Assert.True(reader.ValueSpan.IsEmpty, $"HasValueSequence with a non-empty ValueSpan: {token}");
                }
                else if (!sequence.IsSliceOfOneSegment(reader.ValueSpan))
                {
                    Assert.Fail($"ValueSpan is not a slice of one input segment: {token}");
                }
                // Anchor and Tag are a slice of one segment, or (when their bytes cross a boundary) of a stitched copy
                //; never a span of the input that crosses a segment boundary.
                if (sequence.StraddlesSegments(reader.Anchor) || sequence.StraddlesSegments(reader.Tag))
                {
                    Assert.Fail($"Anchor or Tag crosses a segment boundary of the input: {token}");
                }
                tokens.Add(token);
            }
        }
        private static void AssertSame(Trace expected, Trace actual, string how)
        {
            if (expected.Error is not null || actual.Error is not null)
            {
                Assert.True(expected.Error is not null && actual.Error is not null,
                    $"{how}: one-shot {(expected.Error is null ? "succeeded" : "failed")}, chunked {(actual.Error is null ? "succeeded" : "failed: " + actual.Error)}");
            }

            int count = Math.Min(expected.Tokens.Count, actual.Tokens.Count);
            for (int i = 0; i < count; i++)
            {
                if (expected.Tokens[i] != actual.Tokens[i])
                {
                    Assert.Fail($"{how}: token {i} differs.\n  one-shot: {expected.Tokens[i]}\n  chunked:  {actual.Tokens[i]}");
                }
            }

            if (expected.Error is null)
            {
                Assert.Equal(expected.Tokens.Count, actual.Tokens.Count);
            }
        }

        internal sealed class Trace
        {
            public List<string> Tokens = new();
            public string? Error;
        }

        internal static Trace ReadOneShot(byte[] input, YamlReaderOptions options)
        {
            var trace = new Trace();
            try
            {
                var reader = new Utf8YamlReader(input, isFinalBlock: true, new YamlReaderState(options));
                Drain(ref reader, 0, trace.Tokens);
            }
            catch (YamlException e)
            {
                trace.Error = e.Message;
            }
            return trace;
        }

        private static Trace ReadSplit(byte[] input, int split, YamlReaderOptions options)
        {
            var trace = new Trace();
            try
            {
                var first = new Utf8YamlReader(input.AsSpan(0, split), isFinalBlock: false, new YamlReaderState(options));
                Drain(ref first, 0, trace.Tokens);
                int consumed = checked((int)first.BytesConsumed);
                var second = new Utf8YamlReader(input.AsSpan(consumed), isFinalBlock: true, first.CurrentState);
                Drain(ref second, consumed, trace.Tokens);
            }
            catch (YamlException e)
            {
                trace.Error = e.Message;
            }
            return trace;
        }

        private static Trace ReadIncrementally(byte[] input, YamlReaderOptions options)
        {
            var trace = new Trace();
            try
            {
                YamlReaderState state = new(options);
                int offset = 0;
                for (int end = 0; ; end++)
                {
                    bool final = end == input.Length;
                    var reader = new Utf8YamlReader(input.AsSpan(offset, end - offset), final, state);
                    Drain(ref reader, offset, trace.Tokens);
                    offset += checked((int)reader.BytesConsumed);
                    state = reader.CurrentState;
                    if (final)
                    {
                        break;
                    }
                }
            }
            catch (YamlException e)
            {
                trace.Error = e.Message;
            }
            return trace;
        }

        private static void Drain(ref Utf8YamlReader reader, long offset, List<string> tokens)
        {
            while (reader.Read())
            {
                tokens.Add(Describe(ref reader, offset));
            }
        }

        /// <summary>The events of a Complex key, read through the reader scoped to the key node.</summary>
        private static string ComplexKeyEvents(ref Utf8YamlReader reader)
        {
            var events = new List<string>();
            YamlEventTrace.AddEvent(ref reader, events);
            return "{" + string.Join(";", events) + "}";
        }

        /// <summary>Everything observable about a token, with indices made absolute.</summary>
        internal static string Describe(ref Utf8YamlReader reader, long offset)
        {
            string value = reader.KeyKind == YamlKeyKind.Complex ? ComplexKeyEvents(ref reader)
                : reader.TokenType == YamlTokenType.Alias || reader.KeyKind == YamlKeyKind.Alias ? "*" + Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray()) : reader.TokenType switch
            {
                YamlTokenType.String or YamlTokenType.PropertyName or YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False => reader.GetString() ?? "",
                _ => "",
            };

            return string.Join("|",
                reader.TokenType,
                Convert.ToBase64String(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray()),
                reader.ValueIsEscaped,
                reader.TokenStartIndex + offset,
                reader.BytesConsumed + offset,
                reader.CurrentDepth,
                reader.ScalarStyle,
                Convert.ToBase64String(reader.Anchor.ToArray()),
                Convert.ToBase64String(reader.Tag.ToArray()),
                reader.GetExpandedTag() ?? "",
                reader.IsFlow,
                reader.KeyKind,
                value);
        }
    }
}
