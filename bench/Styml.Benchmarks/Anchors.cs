// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M6b's anchors documents: anchors-s and anchors-m (aliases to blocks
// anchored under a mapped member), merge-s and merge-m (an x-common block merged with <<). Their own corpus list and
// classes, so that no existing class or identity output changes. SharpYaml and VYaml do not run on the merge files
// ([NotOnMerge], Config's filter): SharpYaml refuses a merge into a typed mapping and VYaml ignores <<.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Styml.Benchmarks.Model;
using SJ = System.Text.Json.JsonSerializer;
using SY = SharpYaml;
using VY = VYaml.Serialization.YamlSerializer;
using YD = YamlDotNet.Serialization;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    /// <summary>A row that has no merge-key reading in its library, so that it does not run on the merge files.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NotOnMergeAttribute : Attribute
    {
    }

    internal static class AnchorLibraries
    {
        // Styml: aliases are read by default; the merge files add ResolveMergeKeys.
        public static readonly System.Text.Yaml.YamlSerializerOptions StymlMergeOptions = new() { PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase, ResolveMergeKeys = true };
        public static readonly StymlContext StymlMergeGenerated = new(new System.Text.Yaml.YamlSerializerOptions { PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase, ResolveMergeKeys = true });

        // YamlDotNet: unmatched members skipped (the merge files' x-common), merges through MergingParser.
        public static readonly YD.IDeserializer YamlDotNetReflection = new YD.DeserializerBuilder().WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        public static readonly YD.IDeserializer YamlDotNetStatic = new YD.StaticDeserializerBuilder(new YamlDotNetContext()).WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

        // SharpYaml reads aliases only with ReferenceHandling = Preserve.
        public static readonly SY.YamlSerializerOptions SharpYamlOptions = new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, ReferenceHandling = SY.YamlReferenceHandling.Preserve };
        public static readonly SharpYamlContext SharpYamlGenerated = new(new SY.YamlSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, ReferenceHandling = SY.YamlReferenceHandling.Preserve });

        // AnchorsSerialize: references kept where the library can keep them.
        public static readonly StymlWriteContext StymlPreserveGenerated = new(new System.Text.Yaml.YamlSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull,
            WriteStyle = System.Text.Yaml.YamlWriterStyle.Block,
            ReferenceHandler = System.Text.Yaml.Serialization.ReferenceHandler.Preserve,
        });

        public static readonly SharpYamlWriteContext SharpYamlPreserveGenerated = new(new SY.YamlSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = SY.YamlIgnoreCondition.WhenWritingNull,
            ReferenceHandling = SY.YamlReferenceHandling.PreserveMinimal,
        });

        public static ComposeFile? Styml(byte[] utf8, bool merge) => YS.Deserialize<ComposeFile>(utf8, merge ? StymlMergeOptions : Libraries.StymlOptions);

        public static ComposeFile? StymlGeneratedRead(byte[] utf8, bool merge) => YS.Deserialize(utf8, (merge ? StymlMergeGenerated : Libraries.StymlGenerated).ComposeFile);

        public static ComposeFile? YamlDotNet(string text, bool merge, YD.IDeserializer deserializer) => merge
            ? deserializer.Deserialize<ComposeFile>(new global::YamlDotNet.Core.MergingParser(new global::YamlDotNet.Core.Parser(new StringReader(text))))
            : deserializer.Deserialize<ComposeFile>(text);

        public static ComposeFile? SharpYaml(string text) => SY.YamlSerializer.Deserialize<ComposeFile>(text, SharpYamlOptions);

        public static ComposeFile? SharpYamlGeneratedRead(string text) => SY.YamlSerializer.Deserialize<ComposeFile>(text, SharpYamlGenerated);

        public static ComposeFile? VYaml(ReadOnlyMemory<byte> utf8) => VY.Deserialize<ComposeFile>(utf8, Libraries.VYamlOptions);

        public static ComposeFile? Json(ReadOnlySpan<byte> utf8) => SJ.Deserialize<ComposeFile>(utf8, Libraries.JsonOptions);

        public static ComposeFile? JsonGeneratedRead(ReadOnlySpan<byte> utf8) => SJ.Deserialize(utf8, Libraries.JsonGenerated.ComposeFile);
    }

    public abstract class AnchorDocuments : Scenario
    {
        private protected bool _merge;

        public static IEnumerable<string> Documents => Corpus.AnchorNames;

        [ParamsSource(nameof(Documents))]
        public override string Document { get; set; } = null!;

        public override void Setup()
        {
            base.Setup();
            _merge = Corpus.IsMerge(Document);
        }
    }

    // Every parser yields one alias event per alias and does not expand it. System.Text.Json reads the expanded twin.
    public class AnchorsTokenize : AnchorDocuments
    {
        [Benchmark(Baseline = true)] public long Styml_Yaml() => Libraries.StymlTokens(_doc.Yaml, Libraries.StymlReader, text: false);
        [Benchmark] public long YamlDotNet() => Libraries.YamlDotNetEvents(_doc.YamlText);
        [Benchmark] public long SharpYaml() => Libraries.SharpYamlEvents(_doc.YamlText);
        [Benchmark] public long VYaml() => Libraries.VYamlEvents(_doc.Yaml, text: false);
        [Benchmark, Input(Form.Json)] public long Stj() => Libraries.JsonTokens(_doc.Json, text: false);
    }

    // Parse only: no DOM resolves merge keys. Styml keeps an alias row, YamlDotNet shares the anchored node, SharpYaml's
    // model expands the alias into a copy.
    public class AnchorsDomParse : AnchorDocuments
    {
        [Benchmark(Baseline = true)] public long Styml_Yaml() => Dom.StymlDocument(_doc.Yaml);
        [Benchmark] public long Styml_Node() => Dom.StymlNode(_doc.Yaml);
        [Benchmark] public long YamlDotNet() => Dom.YamlDotNet(_doc.YamlText);
        [Benchmark] public long SharpYaml() => Dom.SharpYaml(_doc.YamlText);
        [Benchmark] public long SharpYaml_Syntax() => Dom.SharpYamlSyntax(_doc.YamlText);
        [Benchmark, Input(Form.Json)] public long Stj() => Dom.Stj(_doc.Json);
        [Benchmark, Input(Form.Json)] public long Stj_Node() => Dom.StjNode(_doc.Json);
    }

    // VYaml has no reflection serializer.
    public class AnchorsDeserializeReflection : AnchorDocuments
    {
        [Benchmark(Baseline = true)] public object? Styml_Yaml() => AnchorLibraries.Styml(_doc.Yaml, _merge);
        [Benchmark] public object? YamlDotNet() => AnchorLibraries.YamlDotNet(_doc.YamlText, _merge, AnchorLibraries.YamlDotNetReflection);
        [Benchmark, NotOnMerge] public object? SharpYaml() => AnchorLibraries.SharpYaml(_doc.YamlText);
        [Benchmark, Input(Form.Json)] public object? Stj() => AnchorLibraries.Json(_doc.Json);
    }

    public class AnchorsDeserializeSourceGen : AnchorDocuments
    {
        [Benchmark(Baseline = true)] public object? Styml_Yaml() => AnchorLibraries.StymlGeneratedRead(_doc.Yaml, _merge);
        [Benchmark] public object? YamlDotNet() => AnchorLibraries.YamlDotNet(_doc.YamlText, _merge, AnchorLibraries.YamlDotNetStatic);
        [Benchmark, NotOnMerge] public object? SharpYaml() => AnchorLibraries.SharpYamlGeneratedRead(_doc.YamlText);
        [Benchmark, NotOnMerge] public object? VYaml() => AnchorLibraries.VYaml(_doc.Yaml);
        [Benchmark, Input(Form.Json)] public object? Stj() => AnchorLibraries.JsonGeneratedRead(_doc.Json);
    }

    // Source-generated only (VYaml has no reflection serializer). The graph is Styml's read of the anchors YAML, so the
    // services that share a block share its instance. VYaml and System.Text.Json write the shared blocks out in full.
    public class AnchorsSerialize : Scenario
    {
        private ComposeFile _graph = null!;
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private readonly ArrayBufferWriter<char> _chars = new();

        public static IEnumerable<string> Documents => Corpus.SharedNames;

        [ParamsSource(nameof(Documents))]
        public override string Document { get; set; } = null!;

        public override void Setup()
        {
            base.Setup();
            _graph = AnchorLibraries.Styml(_doc.Yaml, merge: false)!;
        }

        internal ComposeFile Graph => _graph;

        internal byte[] Written => _buffer.WrittenSpan.ToArray();

        internal byte[] WrittenChars => System.Text.Encoding.UTF8.GetBytes(_chars.WrittenSpan.ToArray());

        [Benchmark(Baseline = true), Input(Form.Yaml)] public byte[] Styml_Block() => YS.SerializeToUtf8Bytes(_graph, AnchorLibraries.StymlPreserveGenerated.ComposeFile);
        [Benchmark, Input(Form.Yaml)] public string YamlDotNet() => WriteLibraries.YamlDotNetStatic.Serialize(_graph);
        [Benchmark, Input(Form.Yaml)] public long SharpYaml() => YamlWriteLibraries.SharpYamlGeneratedBuffer(_graph, Family.Compose, AnchorLibraries.SharpYamlPreserveGenerated, _chars);
        [Benchmark, Input(Form.Yaml)] public long VYaml_Expanded() => YamlWriteLibraries.VYamlBuffer(_graph, Family.Compose, WriteLibraries.VYamlOptions, _buffer);
        [Benchmark, Input(Form.Json)] public byte[] Stj_Expanded() => SJ.SerializeToUtf8Bytes(_graph, WriteLibraries.JsonGenerated.ComposeFile);
    }

    // The generated SharpYaml contexts bound to the anchors rows' options (the base class's constructor).
    internal partial class SharpYamlContext
    {
        public SharpYamlContext(SY.YamlSerializerOptions options)
            : base(options)
        {
        }
    }

    internal partial class SharpYamlWriteContext
    {
        public SharpYamlWriteContext(SY.YamlSerializerOptions options)
            : base(options)
        {
        }
    }
}
