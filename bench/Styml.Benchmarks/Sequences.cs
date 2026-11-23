// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M3c gate 4: multi-segment rows. Styml on a sequence of 4 KB or 64 KB
// segments, Styml on the span as reference, and System.Text.Json on the same JSON sequences. Deserialization goes
// through a reader in every row (YamlSerializer.Deserialize(ref reader, ...), JsonSerializer.Deserialize(ref reader,
// ...)), since neither library has a ReadOnlySequence<byte> overload.
using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Yaml;
using BenchmarkDotNet.Attributes;
using Styml.Benchmarks.Model;

namespace Styml.Benchmarks
{
    public abstract class SequenceScenario
    {
        private protected byte[] _input = null!;
        private protected ReadOnlySequence<byte> _sequence;
        private protected Family _family;
        private protected bool _isJson;

        [Params("k8s-l.yaml", "k8s-l.json", "records-l.yaml", "records-l.json", "openapi-l.yaml", "openapi-l.json", "records-l.min.json")]
        public string Input { get; set; } = null!;

        [Params(4096, 65536)]
        public int SegmentSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _input = Config.InputData(Input);
            _sequence = SequenceCheck.Segments(_input, SegmentSize);
            _family = Corpus.Get(Input[..Input.IndexOf('.')]).Family;
            _isJson = Input.EndsWith(".json", StringComparison.Ordinal);
        }
    }

    public class TokenizeSequence : SequenceScenario
    {
        [Benchmark(Baseline = true)]
        public long Styml_Span() => Libraries.StymlTokens(_input, Libraries.StymlReader, text: false);

        [Benchmark]
        public long Styml_Sequence()
        {
            var reader = new Utf8YamlReader(_sequence, Libraries.StymlReader);
            long n = 0;
            while (reader.Read())
            {
                n++;
            }
            return n;
        }

        [Benchmark]
        public long Stj_Sequence()
        {
            if (!_isJson)
            {
                return 0;
            }
            var reader = new Utf8JsonReader(_sequence);
            long n = 0;
            while (reader.Read())
            {
                n++;
            }
            return n;
        }
    }

    public class DeserializeSequence : SequenceScenario
    {
        [Benchmark(Baseline = true)]
        public object? Styml_Span()
        {
            var reader = new Utf8YamlReader(_input, Libraries.StymlReader);
            return StymlRead(ref reader, _family);
        }

        [Benchmark]
        public object? Styml_Sequence()
        {
            var reader = new Utf8YamlReader(_sequence, Libraries.StymlReader);
            return StymlRead(ref reader, _family);
        }

        [Benchmark]
        public object? Stj_Sequence()
        {
            if (!_isJson)
            {
                return null;
            }
            var reader = new Utf8JsonReader(_sequence);
            return _family switch
            {
                Family.K8s => JsonSerializer.Deserialize(ref reader, Libraries.JsonGenerated.K8sList),
                Family.OpenApi => JsonSerializer.Deserialize(ref reader, Libraries.JsonGenerated.OpenApiDocument),
                _ => JsonSerializer.Deserialize(ref reader, Libraries.JsonGenerated.ListRecord),
            };
        }

        private static object? StymlRead(ref Utf8YamlReader reader, Family family) => family switch
        {
            Family.K8s => YamlSerializer.Deserialize(ref reader, Libraries.StymlGenerated.K8sList),
            Family.OpenApi => YamlSerializer.Deserialize(ref reader, Libraries.StymlGenerated.OpenApiDocument),
            _ => YamlSerializer.Deserialize(ref reader, Libraries.StymlGenerated.ListRecord),
        };
    }
}
