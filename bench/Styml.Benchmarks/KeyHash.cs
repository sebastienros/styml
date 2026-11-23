// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4p candidate g: the hash candidates, measured before the variants, over
// the dictionary keys of one deserialize (the census's keys, as UTF-8). Copies of the product's code, so that the same
// row runs on any Styml.dll.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Styml.Benchmarks
{
    public class KeyHash
    {
        [Params("openapi-l", "k8s-l", "compose-m")]
        public string Document { get; set; } = null!;

        private byte[][] _keys = null!;
        private readonly ulong _seed = 0x243F6A8885A308D3UL;
        private const ulong Seed1 = 0x13198A2E03707344UL, Seed2 = 0xA4093822299F31D0UL, Seed3 = 0x082EFA98EC4E6C89UL;

        private static ulong Mum(ulong a, ulong b)
        {
            ulong high = Math.BigMul(a, b, out ulong low);
            return high ^ low;
        }

        [GlobalSetup]
        public void Setup() => _keys = KeyCensus.DictionaryKeys(Corpus.Get(Document)).Select(Encoding.UTF8.GetBytes).ToArray();

        // Variant A's: every byte, 8 at a time, a chain of multiplications, each folded.
        [Benchmark(Baseline = true)]
        public int Chain()
        {
            int sum = 0;
            foreach (byte[] key in _keys)
            {
                sum += HashChain(key);
            }

            return sum;
        }

        // Variant B's: the first and the last 16 bytes, two independent 128-bit products.
        [Benchmark]
        public long FourWords()
        {
            long sum = 0;
            foreach (byte[] key in _keys)
            {
                Load(key, out ulong h0, out ulong h1, out ulong t0, out ulong t1);
                sum += (long)HashWords(h0, h1, t0, t1, key.Length);
            }

            return sum;
        }

        // HashCode.AddBytes, seeded per process (net only).
        [Benchmark]
        public int AddBytes()
        {
            int sum = 0;
            foreach (byte[] key in _keys)
            {
                var hash = new HashCode();
                hash.AddBytes(key);
                sum += hash.ToHashCode();
            }

            return sum;
        }

        private int HashChain(ReadOnlySpan<byte> utf8)
        {
            int length = utf8.Length;
            ulong hash = _seed ^ ((ulong)length * 0x9E3779B97F4A7C15UL);
            if (length >= 8)
            {
                int i = 0;
                for (; length - i > 8; i += 8)
                {
                    hash = (hash ^ MemoryMarshal.Read<ulong>(utf8.Slice(i))) * 0xC2B2AE3D27D4EB4FUL;
                    hash ^= hash >> 32;
                }

                hash = (hash ^ MemoryMarshal.Read<ulong>(utf8.Slice(length - 8))) * 0x165667B19E3779F9UL;
            }
            else
            {
                ulong word = 0;
                for (int i = 0; i < length; i++)
                {
                    word |= (ulong)utf8[i] << (i * 8);
                }

                hash = (hash ^ word) * 0x165667B19E3779F9UL;
            }

            hash = (hash ^ (hash >> 33)) * 0xFF51AFD7ED558CCDUL;
            hash = (hash ^ (hash >> 33)) * 0xC4CEB9FE1A85EC53UL;
            return (int)(hash ^ (hash >> 33));
        }

        private static void Load(ReadOnlySpan<byte> utf8, out ulong head0, out ulong head1, out ulong tail0, out ulong tail1)
        {
            if (utf8.Length >= 16)
            {
                ReadOnlySpan<byte> tail = utf8.Slice(utf8.Length - 16);
                head0 = MemoryMarshal.Read<ulong>(utf8);
                head1 = MemoryMarshal.Read<ulong>(utf8.Slice(8));
                tail0 = MemoryMarshal.Read<ulong>(tail);
                tail1 = MemoryMarshal.Read<ulong>(tail.Slice(8));
            }
            else
            {
                head0 = 0;
                head1 = 0;
                for (int i = 0; i < utf8.Length; i++)
                {
                    if (i < 8)
                    {
                        head0 |= (ulong)utf8[i] << (i * 8);
                    }
                    else
                    {
                        head1 |= (ulong)utf8[i] << ((i - 8) * 8);
                    }
                }

                tail0 = head0;
                tail1 = head1;
            }
        }

        private ulong HashWords(ulong head0, ulong head1, ulong tail0, ulong tail1, int length)
        {
            ulong hash = Mum(head0 ^ _seed, head1 ^ Seed1) ^ Mum(tail0 ^ Seed2, tail1 ^ Seed3) ^ (ulong)length;
            hash = (hash ^ (hash >> 33)) * 0xFF51AFD7ED558CCDUL;
            hash = (hash ^ (hash >> 33)) * 0xC4CEB9FE1A85EC53UL;
            return hash ^ (hash >> 33);
        }
    }
}
