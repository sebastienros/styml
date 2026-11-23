// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M3c: tokenizing a multi-segment sequence with the real
// reader against the span read, interleaved in one process (best of 40 after 30 warm-up calls each), with the
// allocation of one read and the bytes stitched into windows per input byte.
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Yaml;

namespace Styml.Benchmarks
{
    internal static class SequenceCheck
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_yamlStitchedBytes")]
        private static extern ref long StitchedBytes(ref Utf8YamlReader reader);

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory, RunningIndex + Memory.Length);
                Next = next;
                return next;
            }
        }

        internal static ReadOnlySequence<byte> Segments(byte[] data, int size)
        {
            var head = new Segment(data.AsSpan(0, Math.Min(size, data.Length)).ToArray(), 0);
            Segment tail = head;
            for (int offset = head.Memory.Length; offset < data.Length; offset += size)
            {
                tail = tail.Append(data.AsSpan(offset, Math.Min(size, data.Length - offset)).ToArray());
            }
            return new ReadOnlySequence<byte>(head, 0, tail, tail.Memory.Length);
        }

        private static long Read(ReadOnlySequence<byte> sequence, YamlReaderOptions options, out long stitched)
        {
            var reader = new Utf8YamlReader(sequence, options);
            long n = 0;
            while (reader.Read())
            {
                n++;
            }
            stitched = StitchedBytes(ref reader);
            return n;
        }

        /// <summary>'sequence-check [inputs] [sizes]': one Markdown row per input, option setting and segment size.</summary>
        public static int Run(string[] args)
        {
            string[] inputs = args.Length > 1 ? args[1].Split(',') : new[] { "records-l.min.json" };
            int[] sizes = args.Length > 2 ? Array.ConvertAll(args[2].Split(','), int.Parse) : new[] { 4096, 65536 };
            Console.WriteLine("| Input | Options | Segment | Span MB/s | Sequence MB/s | Sequence ÷ span (time) | Allocated per read | Stitched ÷ input |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|");
            foreach (string input in inputs)
            {
                byte[] data = Config.InputData(input);
                foreach ((string name, YamlReaderOptions options) in new[] { ("default", Libraries.StymlReader), ("nokeys", Libraries.StymlReaderNoKeys) })
                {
                    foreach (int size in sizes)
                    {
                        ReadOnlySequence<byte> sequence = Segments(data, size);
                        long tokens = Libraries.StymlTokens(data, options, text: false);
                        if (Read(sequence, options, out _) != tokens)
                        {
                            throw new InvalidOperationException($"{input} {name} {size}: token counts differ");
                        }
                        for (int i = 0; i < 30; i++)
                        {
                            Libraries.StymlTokens(data, options, text: false);
                            Read(sequence, options, out _);
                        }
                        double bestSpan = double.MaxValue, bestSequence = double.MaxValue;
                        var clock = new System.Diagnostics.Stopwatch();
                        for (int i = 0; i < 40; i++)
                        {
                            clock.Restart();
                            Libraries.StymlTokens(data, options, text: false);
                            bestSpan = Math.Min(bestSpan, clock.Elapsed.TotalMilliseconds);
                            clock.Restart();
                            Read(sequence, options, out _);
                            bestSequence = Math.Min(bestSequence, clock.Elapsed.TotalMilliseconds);
                        }
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        Read(sequence, options, out long stitched);
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        Console.WriteLine(FormattableString.Invariant(
                            $"| {input} | {name} | {size} | {data.Length / bestSpan / 1000:F0} | {data.Length / bestSequence / 1000:F0} | {bestSequence / bestSpan:F3} | {allocated:N0} B | {(double)stitched / data.Length:F3} |"));
                    }
                }
            }
            return 0;
        }
    }
}
