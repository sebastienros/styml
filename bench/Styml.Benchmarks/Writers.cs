// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M4.0 writer benchmarks: Write, SerializeReflection and
// SerializeSourceGen over every corpus document. The measured methods call only members that exist at 9835d4d, so
// the dll swap of gate G1 can run them against a Styml.dll built from that commit. Every library leaves null members
// out, so that all of them write the same graph (the corpus's JSON twin read by System.Text.Json).
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using Styml.Benchmarks.Model;
using SJ = System.Text.Json.JsonSerializer;
using SY = SharpYaml;
using VY = VYaml.Serialization.YamlSerializer;
using YD = YamlDotNet.Serialization;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    /// <summary>The token stream of a corpus document's JSON twin, recorded once, for the Write rows.</summary>
    internal sealed class TokenStream
    {
        public enum Kind : byte
        {
            StartObject,
            EndObject,
            StartArray,
            EndArray,
            PropertyName,
            String,
            Integer,
            Double,
            True,
            False,
            Null,
        }

        public readonly List<Kind> Kinds = new();

        // For PropertyName and String: the unescaped UTF-8 text, as (offset, length) into Text.
        public readonly List<(int Offset, int Length)> Spans = new();

        // For Integer and Double.
        public readonly List<long> Integers = new();
        public readonly List<double> Doubles = new();

        public byte[] Text = Array.Empty<byte>();

        public static TokenStream Record(byte[] json)
        {
            var stream = new TokenStream();
            var text = new ArrayBufferWriter<byte>();
            var reader = new Utf8JsonReader(json);
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject: stream.Add(Kind.StartObject); break;
                    case JsonTokenType.EndObject: stream.Add(Kind.EndObject); break;
                    case JsonTokenType.StartArray: stream.Add(Kind.StartArray); break;
                    case JsonTokenType.EndArray: stream.Add(Kind.EndArray); break;
                    case JsonTokenType.True: stream.Add(Kind.True); break;
                    case JsonTokenType.False: stream.Add(Kind.False); break;
                    case JsonTokenType.Null: stream.Add(Kind.Null); break;
                    case JsonTokenType.PropertyName:
                    case JsonTokenType.String:
                        byte[] utf8 = Encoding.UTF8.GetBytes(reader.GetString()!);
                        stream.Spans.Add((text.WrittenCount, utf8.Length));
                        text.Write(utf8);
                        stream.Add(reader.TokenType == JsonTokenType.PropertyName ? Kind.PropertyName : Kind.String);
                        break;
                    case JsonTokenType.Number:
                        if (reader.TryGetInt64(out long l))
                        {
                            stream.Integers.Add(l);
                            stream.Add(Kind.Integer);
                        }
                        else
                        {
                            stream.Doubles.Add(reader.GetDouble());
                            stream.Add(Kind.Double);
                        }
                        break;
                }
            }
            stream.Text = text.WrittenSpan.ToArray();
            return stream;
        }

        private void Add(Kind kind) => Kinds.Add(kind);

        public Kind[] KindArray = Array.Empty<Kind>();
        public (int Offset, int Length)[] SpanArray = Array.Empty<(int, int)>();
        public long[] IntegerArray = Array.Empty<long>();
        public double[] DoubleArray = Array.Empty<double>();

        public TokenStream Freeze()
        {
            KindArray = Kinds.ToArray();
            SpanArray = Spans.ToArray();
            IntegerArray = Integers.ToArray();
            DoubleArray = Doubles.ToArray();
            return this;
        }
    }

    /// <summary>The write calls of every library, with instances built once.</summary>
    internal static class WriteLibraries
    {
        // Set in the benchmark's runtimeconfig too (Styml.Benchmarks.csproj). At 9835d4d no Styml.dll reads it; from
        // M4a on it keeps the head's default style JSON-compatible, so that the dll swap compares like with like.
        public const string JsonCompatibleSwitch = "System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle";

        // M4c: from the flip on, a style that is not set means block unless this switch is on. Every caller then checks
        // that the default writer and serializer really write JSON-compatible output, so that no JSON-compatible row or
        // identity command can switch to block silently. The check calls only members that exist at 9835d4d.
        public static void SetSwitch()
        {
            AppContext.SetSwitch(JsonCompatibleSwitch, true);
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new System.Text.Yaml.Utf8YamlWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("a", 1);
                writer.WriteEndObject();
            }

            string written = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
            string serialized = YS.Serialize(new Dictionary<string, int> { ["a"] = 1 });
            if (written != "{\"a\":1}" || serialized != "{\"a\":1}")
            {
                throw new InvalidOperationException($"The default style is not JSON-compatible: the writer wrote '{written}', the serializer '{serialized}'.");
            }
        }

        public static bool SwitchValue => AppContext.TryGetSwitch(JsonCompatibleSwitch, out bool on) && on;

        public static readonly System.Text.Yaml.YamlSerializerOptions StymlOptions = new()
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
        };

        public static readonly StymlWriteContext StymlGenerated = StymlWriteContext.Default;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static readonly JsonWriteContext JsonGenerated = JsonWriteContext.Default;

        public static readonly YD.ISerializer YamlDotNetReflection = new YD.SerializerBuilder()
            .WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(YD.DefaultValuesHandling.OmitNull)
            .Build();

        public static readonly YD.ISerializer YamlDotNetStatic = new YD.StaticSerializerBuilder(new YamlDotNetContext())
            .WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(YD.DefaultValuesHandling.OmitNull)
            .Build();

        public static readonly SY.YamlSerializerOptions SharpYamlOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = SY.YamlIgnoreCondition.WhenWritingNull,
        };

        public static readonly SharpYamlWriteContext SharpYamlGenerated = SharpYamlWriteContext.Default;

        public static readonly global::VYaml.Serialization.YamlSerializerOptions VYamlOptions = new()
        {
            DefaultIgnoreCondition = global::VYaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
        };

        // -------------------------------------------------------------------------------------------------------------
        // Serialization, by document family. Styml and System.Text.Json return UTF-8 bytes; YamlDotNet and SharpYaml
        // return a string, their natural output; VYaml returns UTF-8 bytes.

        public static byte[] Styml(object graph, Family family, System.Text.Yaml.YamlSerializerOptions options) => family switch
        {
            Family.K8s => YS.SerializeToUtf8Bytes((K8sList)graph, options),
            Family.Compose => YS.SerializeToUtf8Bytes((ComposeFile)graph, options),
            Family.Workflow => YS.SerializeToUtf8Bytes((Workflow)graph, options),
            Family.OpenApi => YS.SerializeToUtf8Bytes((OpenApiDocument)graph, options),
            _ => YS.SerializeToUtf8Bytes((List<Record>)graph, options),
        };

        public static byte[] StymlGeneratedWrite(object graph, Family family, StymlWriteContext context) => family switch
        {
            Family.K8s => YS.SerializeToUtf8Bytes((K8sList)graph, context.K8sList),
            Family.Compose => YS.SerializeToUtf8Bytes((ComposeFile)graph, context.ComposeFile),
            Family.Workflow => YS.SerializeToUtf8Bytes((Workflow)graph, context.Workflow),
            Family.OpenApi => YS.SerializeToUtf8Bytes((OpenApiDocument)graph, context.OpenApiDocument),
            _ => YS.SerializeToUtf8Bytes((List<Record>)graph, context.ListRecord),
        };

        public static byte[] Json(object graph, Family family) => family switch
        {
            Family.K8s => SJ.SerializeToUtf8Bytes((K8sList)graph, JsonOptions),
            Family.Compose => SJ.SerializeToUtf8Bytes((ComposeFile)graph, JsonOptions),
            Family.Workflow => SJ.SerializeToUtf8Bytes((Workflow)graph, JsonOptions),
            Family.OpenApi => SJ.SerializeToUtf8Bytes((OpenApiDocument)graph, JsonOptions),
            _ => SJ.SerializeToUtf8Bytes((List<Record>)graph, JsonOptions),
        };

        public static byte[] JsonGeneratedWrite(object graph, Family family) => family switch
        {
            Family.K8s => SJ.SerializeToUtf8Bytes((K8sList)graph, JsonGenerated.K8sList),
            Family.Compose => SJ.SerializeToUtf8Bytes((ComposeFile)graph, JsonGenerated.ComposeFile),
            Family.Workflow => SJ.SerializeToUtf8Bytes((Workflow)graph, JsonGenerated.Workflow),
            Family.OpenApi => SJ.SerializeToUtf8Bytes((OpenApiDocument)graph, JsonGenerated.OpenApiDocument),
            _ => SJ.SerializeToUtf8Bytes((List<Record>)graph, JsonGenerated.ListRecord),
        };

        public static string YamlDotNet(object graph, Family family, YD.ISerializer serializer) => family switch
        {
            Family.K8s => serializer.Serialize((K8sList)graph),
            Family.Compose => serializer.Serialize((ComposeFile)graph),
            Family.Workflow => serializer.Serialize((Workflow)graph),
            Family.OpenApi => serializer.Serialize((OpenApiDocument)graph),
            _ => serializer.Serialize((List<Record>)graph),
        };

        public static string SharpYaml(object graph, Family family) => family switch
        {
            Family.K8s => SY.YamlSerializer.Serialize((K8sList)graph, SharpYamlOptions),
            Family.Compose => SY.YamlSerializer.Serialize((ComposeFile)graph, SharpYamlOptions),
            Family.Workflow => SY.YamlSerializer.Serialize((Workflow)graph, SharpYamlOptions),
            Family.OpenApi => SY.YamlSerializer.Serialize((OpenApiDocument)graph, SharpYamlOptions),
            _ => SY.YamlSerializer.Serialize((List<Record>)graph, SharpYamlOptions),
        };

        public static string SharpYamlGeneratedWrite(object graph, Family family) => family switch
        {
            Family.K8s => SY.YamlSerializer.Serialize((K8sList)graph, SharpYamlGenerated),
            Family.Compose => SY.YamlSerializer.Serialize((ComposeFile)graph, SharpYamlGenerated),
            Family.Workflow => SY.YamlSerializer.Serialize((Workflow)graph, SharpYamlGenerated),
            Family.OpenApi => SY.YamlSerializer.Serialize((OpenApiDocument)graph, SharpYamlGenerated),
            _ => SY.YamlSerializer.Serialize((List<Record>)graph, SharpYamlGenerated),
        };

        public static ReadOnlyMemory<byte> VYaml(object graph, Family family) => family switch
        {
            Family.K8s => VY.Serialize((K8sList)graph, VYamlOptions),
            Family.Compose => VY.Serialize((ComposeFile)graph, VYamlOptions),
            Family.Workflow => VY.Serialize((Workflow)graph, VYamlOptions),
            Family.OpenApi => VY.Serialize((OpenApiDocument)graph, VYamlOptions),
            _ => VY.Serialize((List<Record>)graph, VYamlOptions),
        };

        // -------------------------------------------------------------------------------------------------------------
        // Token replay. The writer and its buffer are reused, so nothing is allocated per operation once the buffer has
        // grown to the document's size. Each returns the number of bytes written.

        public static long ReplayStyml(TokenStream tokens, System.Text.Yaml.Utf8YamlWriter writer, ArrayBufferWriter<byte> buffer)
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            TokenStream.Kind[] kinds = tokens.KindArray;
            (int Offset, int Length)[] spans = tokens.SpanArray;
            long[] integers = tokens.IntegerArray;
            double[] doubles = tokens.DoubleArray;
            ReadOnlySpan<byte> text = tokens.Text;
            int s = 0, i = 0, d = 0;
            for (int k = 0; k < kinds.Length; k++)
            {
                switch (kinds[k])
                {
                    case TokenStream.Kind.StartObject: writer.WriteStartObject(); break;
                    case TokenStream.Kind.EndObject: writer.WriteEndObject(); break;
                    case TokenStream.Kind.StartArray: writer.WriteStartArray(); break;
                    case TokenStream.Kind.EndArray: writer.WriteEndArray(); break;
                    case TokenStream.Kind.PropertyName: writer.WritePropertyName(text.Slice(spans[s].Offset, spans[s].Length)); s++; break;
                    case TokenStream.Kind.String: writer.WriteStringValue(text.Slice(spans[s].Offset, spans[s].Length)); s++; break;
                    case TokenStream.Kind.Integer: writer.WriteNumberValue(integers[i++]); break;
                    case TokenStream.Kind.Double: writer.WriteNumberValue(doubles[d++]); break;
                    case TokenStream.Kind.True: writer.WriteBooleanValue(true); break;
                    case TokenStream.Kind.False: writer.WriteBooleanValue(false); break;
                    default: writer.WriteNullValue(); break;
                }
            }
            writer.Flush();
            return buffer.WrittenCount;
        }

        public static long ReplayJson(TokenStream tokens, Utf8JsonWriter writer, ArrayBufferWriter<byte> buffer)
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            TokenStream.Kind[] kinds = tokens.KindArray;
            (int Offset, int Length)[] spans = tokens.SpanArray;
            long[] integers = tokens.IntegerArray;
            double[] doubles = tokens.DoubleArray;
            ReadOnlySpan<byte> text = tokens.Text;
            int s = 0, i = 0, d = 0;
            for (int k = 0; k < kinds.Length; k++)
            {
                switch (kinds[k])
                {
                    case TokenStream.Kind.StartObject: writer.WriteStartObject(); break;
                    case TokenStream.Kind.EndObject: writer.WriteEndObject(); break;
                    case TokenStream.Kind.StartArray: writer.WriteStartArray(); break;
                    case TokenStream.Kind.EndArray: writer.WriteEndArray(); break;
                    case TokenStream.Kind.PropertyName: writer.WritePropertyName(text.Slice(spans[s].Offset, spans[s].Length)); s++; break;
                    case TokenStream.Kind.String: writer.WriteStringValue(text.Slice(spans[s].Offset, spans[s].Length)); s++; break;
                    case TokenStream.Kind.Integer: writer.WriteNumberValue(integers[i++]); break;
                    case TokenStream.Kind.Double: writer.WriteNumberValue(doubles[d++]); break;
                    case TokenStream.Kind.True: writer.WriteBooleanValue(true); break;
                    case TokenStream.Kind.False: writer.WriteBooleanValue(false); break;
                    default: writer.WriteNullValue(); break;
                }
            }
            writer.Flush();
            return buffer.WrittenCount;
        }
    }

    /// <summary>The graph every Serialize row writes: the JSON twin, read once by System.Text.Json.</summary>
    public abstract class WriteScenario : AllDocuments
    {
        private protected object _graph = null!;

        public override void Setup()
        {
            WriteLibraries.SetSwitch();
            base.Setup();
            _graph = Libraries.Json(_doc.Json, _doc.Family)!;
        }
    }

    // The Write rows replay the JSON twin's tokens (the same calls for both writers). Default options: minimized.
    // Indented: JSON-compatible indented, the other layout the style dispatch serves.
    public class Write : AllDocuments
    {
        private TokenStream _tokens = null!;
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private System.Text.Yaml.Utf8YamlWriter _styml = null!;
        private System.Text.Yaml.Utf8YamlWriter _stymlIndented = null!;
        private Utf8JsonWriter _stj = null!;
        private Utf8JsonWriter _stjIndented = null!;

        public override void Setup()
        {
            WriteLibraries.SetSwitch();
            base.Setup();
            _tokens = TokenStream.Record(_doc.Json).Freeze();
            _styml = new System.Text.Yaml.Utf8YamlWriter(_buffer);
            _stymlIndented = new System.Text.Yaml.Utf8YamlWriter(_buffer, new System.Text.Yaml.YamlWriterOptions { Indented = true });
            _stj = new Utf8JsonWriter(_buffer);
            _stjIndented = new Utf8JsonWriter(_buffer, new JsonWriterOptions { Indented = true });
        }

        [Benchmark(Baseline = true), Input(Form.Json)] public long Styml() => WriteLibraries.ReplayStyml(_tokens, _styml, _buffer);
        [Benchmark, Input(Form.Json)] public long Styml_Indented() => WriteLibraries.ReplayStyml(_tokens, _stymlIndented, _buffer);
        [Benchmark, Input(Form.Json)] public long Stj() => WriteLibraries.ReplayJson(_tokens, _stj, _buffer);
        [Benchmark, Input(Form.Json)] public long Stj_Indented() => WriteLibraries.ReplayJson(_tokens, _stjIndented, _buffer);

        /// <summary>The bytes a row writes, for the output-hash check (gate G2).</summary>
        internal byte[] Output(string method)
        {
            switch (method)
            {
                case nameof(Styml): Styml(); break;
                case nameof(Styml_Indented): Styml_Indented(); break;
                case nameof(Stj): Stj(); break;
                default: Stj_Indented(); break;
            }
            return _buffer.WrittenSpan.ToArray();
        }
    }

    // VYaml has no reflection serializer.
    public class SerializeReflection : WriteScenario
    {
        [Benchmark(Baseline = true), Input(Form.Json)] public object Styml() => WriteLibraries.Styml(_graph, _doc.Family, WriteLibraries.StymlOptions);
        [Benchmark, Input(Form.Json)] public object YamlDotNet() => WriteLibraries.YamlDotNet(_graph, _doc.Family, WriteLibraries.YamlDotNetReflection);
        [Benchmark, Input(Form.Json)] public object SharpYaml() => WriteLibraries.SharpYaml(_graph, _doc.Family);
        [Benchmark, Input(Form.Json)] public object Stj() => WriteLibraries.Json(_graph, _doc.Family);
    }

    public class SerializeSourceGen : WriteScenario
    {
        [Benchmark(Baseline = true), Input(Form.Json)] public object Styml() => WriteLibraries.StymlGeneratedWrite(_graph, _doc.Family, WriteLibraries.StymlGenerated);
        [Benchmark, Input(Form.Json)] public object YamlDotNet() => WriteLibraries.YamlDotNet(_graph, _doc.Family, WriteLibraries.YamlDotNetStatic);
        [Benchmark, Input(Form.Json)] public object SharpYaml() => WriteLibraries.SharpYamlGeneratedWrite(_graph, _doc.Family);
        [Benchmark, Input(Form.Json)] public long VYaml() => WriteLibraries.VYaml(_graph, _doc.Family).Length;
        [Benchmark, Input(Form.Json)] public object Stj() => WriteLibraries.JsonGeneratedWrite(_graph, _doc.Family);
    }
}
