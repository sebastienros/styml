// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M6b's serialize scenarios: every library's best output, with Styml's block
// output as the baseline and System.Text.Json on the same graph as the ceiling. The benchmark process keeps the
// JSON-compatible switch on, so every Styml row here sets its style explicitly.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using Styml.Benchmarks.Model;
using SJ = System.Text.Json.JsonSerializer;
using SY = SharpYaml;
using VY = VYaml.Serialization.YamlSerializer;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    internal static class YamlWriteLibraries
    {
        public static readonly System.Text.Yaml.YamlWriterOptions BlockWriterOptions = new() { Style = System.Text.Yaml.YamlWriterStyle.Block };

        public static readonly System.Text.Yaml.YamlSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.JsonCompatible,
        };

        public static readonly StymlWriteContext JsonGenerated = new(new System.Text.Yaml.YamlSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.JsonCompatible,
        });

        // -------------------------------------------------------------------------------------------------------------
        // Styml through a reused Utf8YamlWriter over a reused buffer: the like-for-like row of SharpYaml's and VYaml's
        // IBufferWriter<byte> rows. Each returns the number of bytes written.

        public static long StymlWriter(object graph, Family family, System.Text.Yaml.YamlSerializerOptions options, System.Text.Yaml.Utf8YamlWriter writer, ArrayBufferWriter<byte> buffer)
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            switch (family)
            {
                case Family.K8s: YS.Serialize(writer, (K8sList)graph, options); break;
                case Family.Compose: YS.Serialize(writer, (ComposeFile)graph, options); break;
                case Family.Workflow: YS.Serialize(writer, (Workflow)graph, options); break;
                case Family.OpenApi: YS.Serialize(writer, (OpenApiDocument)graph, options); break;
                default: YS.Serialize(writer, (List<Record>)graph, options); break;
            }
            return buffer.WrittenCount;
        }

        public static long StymlGeneratedWriter(object graph, Family family, StymlWriteContext context, System.Text.Yaml.Utf8YamlWriter writer, ArrayBufferWriter<byte> buffer)
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            switch (family)
            {
                case Family.K8s: YS.Serialize(writer, (K8sList)graph, context.K8sList); break;
                case Family.Compose: YS.Serialize(writer, (ComposeFile)graph, context.ComposeFile); break;
                case Family.Workflow: YS.Serialize(writer, (Workflow)graph, context.Workflow); break;
                case Family.OpenApi: YS.Serialize(writer, (OpenApiDocument)graph, context.OpenApiDocument); break;
                default: YS.Serialize(writer, (List<Record>)graph, context.ListRecord); break;
            }
            return buffer.WrittenCount;
        }

        // SharpYaml's reused-buffer output is an IBufferWriter<char> (UTF-16): its bytes output is a Stream.
        public static long SharpYamlBuffer(object graph, Family family, ArrayBufferWriter<char> buffer)
        {
            buffer.ResetWrittenCount();
            switch (family)
            {
                case Family.K8s: SY.YamlSerializer.Serialize(buffer, (K8sList)graph, WriteLibraries.SharpYamlOptions); break;
                case Family.Compose: SY.YamlSerializer.Serialize(buffer, (ComposeFile)graph, WriteLibraries.SharpYamlOptions); break;
                case Family.Workflow: SY.YamlSerializer.Serialize(buffer, (Workflow)graph, WriteLibraries.SharpYamlOptions); break;
                case Family.OpenApi: SY.YamlSerializer.Serialize(buffer, (OpenApiDocument)graph, WriteLibraries.SharpYamlOptions); break;
                default: SY.YamlSerializer.Serialize(buffer, (List<Record>)graph, WriteLibraries.SharpYamlOptions); break;
            }
            return buffer.WrittenCount;
        }

        public static long SharpYamlGeneratedBuffer(object graph, Family family, SharpYamlWriteContext context, ArrayBufferWriter<char> buffer)
        {
            buffer.ResetWrittenCount();
            switch (family)
            {
                case Family.K8s: SY.YamlSerializer.Serialize(buffer, (K8sList)graph, context); break;
                case Family.Compose: SY.YamlSerializer.Serialize(buffer, (ComposeFile)graph, context); break;
                case Family.Workflow: SY.YamlSerializer.Serialize(buffer, (Workflow)graph, context); break;
                case Family.OpenApi: SY.YamlSerializer.Serialize(buffer, (OpenApiDocument)graph, context); break;
                default: SY.YamlSerializer.Serialize(buffer, (List<Record>)graph, context); break;
            }
            return buffer.WrittenCount;
        }

        public static long VYamlBuffer(object graph, Family family, global::VYaml.Serialization.YamlSerializerOptions options, ArrayBufferWriter<byte> buffer)
        {
            buffer.ResetWrittenCount();
            switch (family)
            {
                case Family.K8s: VY.Serialize(buffer, (K8sList)graph, options); break;
                case Family.Compose: VY.Serialize(buffer, (ComposeFile)graph, options); break;
                case Family.Workflow: VY.Serialize(buffer, (Workflow)graph, options); break;
                case Family.OpenApi: VY.Serialize(buffer, (OpenApiDocument)graph, options); break;
                default: VY.Serialize(buffer, (List<Record>)graph, options); break;
            }
            return buffer.WrittenCount;
        }

        // -------------------------------------------------------------------------------------------------------------
        // String outputs, source-generated.

        public static string StymlString(object graph, Family family, StymlWriteContext context) => family switch
        {
            Family.K8s => YS.Serialize((K8sList)graph, context.K8sList),
            Family.Compose => YS.Serialize((ComposeFile)graph, context.ComposeFile),
            Family.Workflow => YS.Serialize((Workflow)graph, context.Workflow),
            Family.OpenApi => YS.Serialize((OpenApiDocument)graph, context.OpenApiDocument),
            _ => YS.Serialize((List<Record>)graph, context.ListRecord),
        };

        public static string VYamlString(object graph, Family family) => family switch
        {
            Family.K8s => VY.SerializeToString((K8sList)graph, WriteLibraries.VYamlOptions),
            Family.Compose => VY.SerializeToString((ComposeFile)graph, WriteLibraries.VYamlOptions),
            Family.Workflow => VY.SerializeToString((Workflow)graph, WriteLibraries.VYamlOptions),
            Family.OpenApi => VY.SerializeToString((OpenApiDocument)graph, WriteLibraries.VYamlOptions),
            _ => VY.SerializeToString((List<Record>)graph, WriteLibraries.VYamlOptions),
        };

        public static string JsonString(object graph, Family family) => family switch
        {
            Family.K8s => SJ.Serialize((K8sList)graph, WriteLibraries.JsonGenerated.K8sList),
            Family.Compose => SJ.Serialize((ComposeFile)graph, WriteLibraries.JsonGenerated.ComposeFile),
            Family.Workflow => SJ.Serialize((Workflow)graph, WriteLibraries.JsonGenerated.Workflow),
            Family.OpenApi => SJ.Serialize((OpenApiDocument)graph, WriteLibraries.JsonGenerated.OpenApiDocument),
            _ => SJ.Serialize((List<Record>)graph, WriteLibraries.JsonGenerated.ListRecord),
        };
    }

    /// <summary>The serialize scenarios' state: the graph (the JSON twin read by System.Text.Json, as M4's rows) and one
    /// reused buffer and writer for the IBufferWriter rows.</summary>
    public abstract class YamlWriteScenario : WriteScenario
    {
        private protected readonly ArrayBufferWriter<byte> _buffer = new();
        private protected readonly ArrayBufferWriter<char> _chars = new();
        private protected System.Text.Yaml.Utf8YamlWriter _writer = null!;

        public override void Setup()
        {
            base.Setup();
            _writer = new System.Text.Yaml.Utf8YamlWriter(_buffer, YamlWriteLibraries.BlockWriterOptions);
        }

        /// <summary>The bytes the last IBufferWriter row wrote, for the check.</summary>
        internal byte[] Written => _buffer.WrittenSpan.ToArray();

        /// <summary>The text the last IBufferWriter&lt;char&gt; row wrote, as UTF-8, for the check.</summary>
        internal byte[] WrittenChars => Encoding.UTF8.GetBytes(_chars.WrittenSpan.ToArray());
    }

    // VYaml has no reflection serializer (M6a).
    public class SerializeYamlReflection : YamlWriteScenario
    {
        [Benchmark(Baseline = true), Input(Form.Json)] public object Styml_Block() => WriteLibraries.Styml(_graph, _doc.Family, BlockLibraries.BlockOptions);
        [Benchmark, Input(Form.Json)] public long Styml_Block_Writer() => YamlWriteLibraries.StymlWriter(_graph, _doc.Family, BlockLibraries.BlockOptions, _writer, _buffer);
        [Benchmark, Input(Form.Json)] public object Styml_Json() => WriteLibraries.Styml(_graph, _doc.Family, YamlWriteLibraries.JsonOptions);
        [Benchmark, Input(Form.Json)] public object YamlDotNet() => WriteLibraries.YamlDotNet(_graph, _doc.Family, WriteLibraries.YamlDotNetReflection);
        [Benchmark, Input(Form.Json)] public long SharpYaml() => YamlWriteLibraries.SharpYamlBuffer(_graph, _doc.Family, _chars);
        [Benchmark, Input(Form.Json)] public object Stj() => WriteLibraries.Json(_graph, _doc.Family);
    }

    public class SerializeYamlSourceGen : YamlWriteScenario
    {
        [Benchmark(Baseline = true), Input(Form.Json)] public object Styml_Block() => WriteLibraries.StymlGeneratedWrite(_graph, _doc.Family, BlockLibraries.BlockGenerated);
        [Benchmark, Input(Form.Json)] public long Styml_Block_Writer() => YamlWriteLibraries.StymlGeneratedWriter(_graph, _doc.Family, BlockLibraries.BlockGenerated, _writer, _buffer);
        [Benchmark, Input(Form.Json)] public object Styml_Json() => WriteLibraries.StymlGeneratedWrite(_graph, _doc.Family, YamlWriteLibraries.JsonGenerated);
        [Benchmark, Input(Form.Json)] public object YamlDotNet() => WriteLibraries.YamlDotNet(_graph, _doc.Family, WriteLibraries.YamlDotNetStatic);
        [Benchmark, Input(Form.Json)] public long SharpYaml() => YamlWriteLibraries.SharpYamlGeneratedBuffer(_graph, _doc.Family, WriteLibraries.SharpYamlGenerated, _chars);
        [Benchmark, Input(Form.Json)] public long VYaml() => YamlWriteLibraries.VYamlBuffer(_graph, _doc.Family, WriteLibraries.VYamlOptions, _buffer);
        [Benchmark, Input(Form.Json)] public object Stj() => WriteLibraries.JsonGeneratedWrite(_graph, _doc.Family);
    }

    // Every library's string output, source-generated, on M6a's from-string documents.
    public class SerializeToString : Scenario
    {
        private object _graph = null!;

        public static IEnumerable<string> StringDocuments => Corpus.Filter(new[] { "k8s-m", "records-l" });

        [ParamsSource(nameof(StringDocuments))]
        public override string Document { get; set; } = null!;

        public override void Setup()
        {
            base.Setup();
            _graph = Libraries.Json(_doc.Json, _doc.Family)!;
        }

        [Benchmark(Baseline = true), Input(Form.Json)] public string Styml_Block() => YamlWriteLibraries.StymlString(_graph, _doc.Family, BlockLibraries.BlockGenerated);
        [Benchmark, Input(Form.Json)] public string YamlDotNet() => WriteLibraries.YamlDotNet(_graph, _doc.Family, WriteLibraries.YamlDotNetStatic);
        [Benchmark, Input(Form.Json)] public string SharpYaml() => WriteLibraries.SharpYamlGeneratedWrite(_graph, _doc.Family);
        [Benchmark, Input(Form.Json)] public string VYaml() => YamlWriteLibraries.VYamlString(_graph, _doc.Family);
        [Benchmark, Input(Form.Json)] public string Stj() => YamlWriteLibraries.JsonString(_graph, _doc.Family);
    }
}
