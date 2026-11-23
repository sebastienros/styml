// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// The implicit collection-key probe examines O(input) bytes in total, for final and non-final data, whatever the
    /// content. Deterministic: the reader counts the bytes the probe
    /// examines (Utf8YamlReader.YamlProbeBytesExamined), and the tests bound that count by a small multiple of the
    /// input length.
    /// </summary>
    public class KeyProbeCostTests
    {
        /// <summary>Bytes examined by the probe per input byte, at most.</summary>
        private const double MaxRatio = 4;

        private static string Repeat(string item, int targetLength, string open = "[", string close = "]", string separator = ", ")
        {
            var parts = new List<string>();
            int length = open.Length + close.Length;
            while (length < targetLength)
            {
                parts.Add(item);
                length += item.Length + separator.Length;
            }
            return open + string.Join(separator, parts) + close;
        }

        private static string Object2K(int i, string tail) =>
            "{\"id\": " + i + ", \"name\": \"item " + i + "\", \"text\": \"" + new string('x', 1900) + tail + "\", \"tags\": [\"a\", \"b\"]}";

        /// <summary>The costly shapes, each as one line, and a variant without the trigger where there is one.</summary>
        public static IEnumerable<object[]> Shapes()
        {
            foreach (int size in new[] { 64 * 1024, 512 * 1024 })
            {
                foreach (bool final in new[] { true, false })
                {
                    // a. A flow sequence of small keys.
                    yield return new object[] { "a: [[a]: b, ...]", size, final };
                    yield return new object[] { "a: [{a: 1}: b, ...]", size, final };
                    // b. JSON: 2 KB objects, the last element is the string "]:".
                    yield return new object[] { "b: JSON objects, last \"]:\"", size, final };
                    // c. JSON: 2 KB objects whose text ends in a markdown reference "[1]: https://".
                    yield return new object[] { "c: JSON objects with \"[1]: https\" inside strings", size, final };
                    // d. JSON: 2 KB objects, no bracket-colon anywhere.
                    yield return new object[] { "d: JSON objects", size, final };
                    // Small objects, minified (the throughput shape).
                    yield return new object[] { "e: JSON small objects", size, final };
                    // Deep nesting, with and without keys inside keys.
                    yield return new object[] { "f: [[[a]: 1]: 1]: 1 nested", size, final };
                    yield return new object[] { "g: [[[[...]]]] nested", size, final };
                    // A bracket-colon candidate less than 4 KB ahead of many single-child collections (review round 3).
                    yield return new object[] { "h: {\"k\":[[1]], ... \"s\":\"]:\"}", size, final };
                    yield return new object[] { "i: {\"k\":[{\"x\":1}], ... \"s\":\"]:\"}", size, final };
                    yield return new object[] { "j: {\"k\":[[1]], ... [a]: 1}", size, final };
                    // Quotes inside plain scalars next to probed entries (review round 4).
                    yield return new object[] { "l: [x \"y, [k], ... \"]:\"]", size, final };
                    yield return new object[] { "m: [x 'y, [k], ... \"]:\"]", size, final };
                }
            }
        }

        internal static string Build(string shape, int size)
        {
            switch (shape[0])
            {
                case 'a':
                    return shape.Contains("{") ? Repeat("{a: 1}: b", size) : Repeat("[a]: b", size);
                case 'b':
                    {
                        int count = (size - 8) / 2100 + 1;
                        return "[" + string.Join(", ", Enumerable.Range(0, count).Select(i => Object2K(i, ""))) + ", \"]:\"]";
                    }
                case 'c':
                    {
                        int count = size / 2100 + 1;
                        return "[" + string.Join(", ", Enumerable.Range(0, count).Select(i => Object2K(i, " [1]: https://example.com/" + i))) + "]";
                    }
                case 'd':
                    {
                        int count = size / 2100 + 1;
                        return "[" + string.Join(", ", Enumerable.Range(0, count).Select(i => Object2K(i, ""))) + "]";
                    }
                case 'e':
                    return Repeat("{\"a\":1,\"b\":\"two\",\"c\":[3,4]}", size, separator: ",");
                case 'h':
                case 'i':
                case 'j':
                case 'k':
                    {
                        // A one-line root object; every ~3,900 bytes a member whose text has ']:' ('k': the control).
                        string member = shape[0] == 'i' ? "\"k\":[{\"x\":1}]" : "\"k\":[[1]]";
                        string trigger = shape[0] switch { 'j' => "[a]: 1", 'k' => "\"s\":\"xx\"", _ => "\"s\":\"]:\"" };
                        var sb = new StringBuilder("{");
                        int sinceTrigger = 0;
                        while (sb.Length < size)
                        {
                            if (sinceTrigger >= 3900)
                            {
                                sb.Append(trigger).Append(',');
                                sinceTrigger = 0;
                            }
                            sb.Append(member).Append(',');
                            sinceTrigger += member.Length + 1;
                        }
                        sb.Append(member).Append('}');
                        return sb.ToString();
                    }
                case 'l':
                case 'm':
                    {
                        string entry = shape[0] == 'l' ? "x \"y, [k]" : "x 'y, [k]";
                        var sb = new StringBuilder("[");
                        int since = 0;
                        while (sb.Length < size)
                        {
                            if (since >= 3900)
                            {
                                sb.Append("\"]:\", ");
                                since = 0;
                            }
                            sb.Append(entry).Append(", ");
                            since += entry.Length + 2;
                        }
                        return sb.Append("z]").ToString();
                    }
                case 'f':
                    {
                        int depth = Math.Min(size / 8, 3000);
                        return new string('[', depth) + "a" + string.Concat(Enumerable.Repeat("]: 1", depth));
                    }
                default:
                    {
                        int depth = Math.Min(size / 2, 50_000);
                        return new string('[', depth) + "a" + new string(']', depth);
                    }
            }
        }

        private static long ProbeBytes(byte[] utf8, bool final, out bool threw)
        {
            threw = false;
            var reader = new Utf8YamlReader(utf8, isFinalBlock: final, new YamlReaderState(new YamlReaderOptions { MaxDepth = 100_000 }));
            try
            {
                while (reader.Read())
                {
                }
            }
            catch (YamlException)
            {
                // Some shapes are not valid YAML at depth (implicit keys longer than 1024 characters); the cost bound
                // holds up to the error too.
                threw = true;
            }
            return reader.YamlProbeBytesExamined;
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void ProbeWorkIsLinear(string shape, int size, bool final)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(Build(shape, size));
            long examined = ProbeBytes(utf8, final, out _);
            double ratio = (double)examined / utf8.Length;
            Assert.True(ratio <= MaxRatio, $"{shape} ({utf8.Length:N0} bytes, final: {final}): the probe examined {examined:N0} bytes, {ratio:F2} per input byte.");
        }

        /// <summary>
        /// M3c (oracle 5): the same bound through multi-segment sequences, and
        /// a bound on the bytes copied into stitched windows.
        /// </summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void ProbeWorkIsLinearThroughSequences(string shape, int size, bool final)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(Build(shape, size));
            foreach (int segment in new[] { 7, 4096, 65536 })
            {
                ReadOnlySequence<byte> sequence = SequenceFactory.Uniform(utf8, segment).Value;
                var reader = new Utf8YamlReader(sequence, isFinalBlock: final, new YamlReaderState(new YamlReaderOptions { MaxDepth = 100_000 }));
                try
                {
                    while (reader.Read())
                    {
                    }
                }
                catch (YamlException)
                {
                }
                double ratio = (double)reader.YamlProbeBytesExamined / utf8.Length;
                Assert.True(ratio <= MaxRatio, $"{shape} ({utf8.Length:N0} bytes, {segment}-byte segments, final: {final}): the probe examined {reader.YamlProbeBytesExamined:N0} bytes, {ratio:F2} per input byte.");
                double copied = (double)reader.YamlStitchedBytes / utf8.Length;
                double copyBound = segment >= 4096 ? 2 : 3;
                Assert.True(copied <= copyBound, $"{shape} ({utf8.Length:N0} bytes, {segment}-byte segments, final: {final}): {reader.YamlStitchedBytes:N0} bytes stitched, {copied:F2} per input byte.");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CandidatesAheadOfSingleChildCollections(bool final)
        {
            // Shape h against its control k (the same document with "xx" for "]:").
            const int Size = 512 * 1024;
            long control = ProbeBytes(Encoding.UTF8.GetBytes(Build("k", Size)), final, out _);
            long withTrigger = ProbeBytes(Encoding.UTF8.GetBytes(Build("h", Size)), final, out _);
            Assert.True(withTrigger <= 4 * control + 64 * 1024, $"{withTrigger:N0} bytes examined, against {control:N0} for the control.");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TriggersCostNoMoreThanTheSameDocumentWithout(bool final)
        {
            // Shapes b and c against d: a bracket-colon inside a string must not make the probe scan more than a small
            // factor of what the same document without it costs.
            const int Size = 512 * 1024;
            long withoutTrigger = ProbeBytes(Encoding.UTF8.GetBytes(Build("d", Size)), final, out _);
            foreach (string shape in new[] { "b", "c" })
            {
                long withTrigger = ProbeBytes(Encoding.UTF8.GetBytes(Build(shape, Size)), final, out _);
                Assert.True(withTrigger <= 4 * withoutTrigger + 64 * 1024, $"{shape}: {withTrigger:N0} bytes examined, against {withoutTrigger:N0} without the trigger.");
            }
        }
    }
}
