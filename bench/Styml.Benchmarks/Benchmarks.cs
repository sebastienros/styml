// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The scenarios Instances and inputs come from setup; the measured methods
// only read.
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Styml.Benchmarks
{
    public abstract class Scenario
    {
        private protected Doc _doc = null!;

        public abstract string Document { get; set; }

        [GlobalSetup]
        public virtual void Setup()
        {
            // Every class sets the M4 switch: BenchmarkDotNet's generated child process may not carry the runtimeconfig.
            WriteLibraries.SetSwitch();
            _doc = Corpus.Get(Document);
        }
    }

    public abstract class AllDocuments : Scenario
    {
        public static IEnumerable<string> Documents => Corpus.Names;

        [ParamsSource(nameof(Documents))]
        public override string Document { get; set; } = null!;
    }

    public class Tokenize : AllDocuments
    {
        [Benchmark(Baseline = true)] public long Styml_Yaml() => Libraries.StymlTokens(_doc.Yaml, Libraries.StymlReader, text: false);
        [Benchmark] public long Styml_Yaml_NoKeys() => Libraries.StymlTokens(_doc.Yaml, Libraries.StymlReaderNoKeys, text: false);
        [Benchmark, Input(Form.Json)] public long Styml_Json() => Libraries.StymlTokens(_doc.Json, Libraries.StymlReader, text: false);
        [Benchmark, Input(Form.Json)] public long Styml_Json_NoKeys() => Libraries.StymlTokens(_doc.Json, Libraries.StymlReaderNoKeys, text: false);
        [Benchmark] public long YamlDotNet() => Libraries.YamlDotNetEvents(_doc.YamlText);
        [Benchmark] public long SharpYaml() => Libraries.SharpYamlEvents(_doc.YamlText);
        [Benchmark] public long VYaml() => Libraries.VYamlEvents(_doc.Yaml, text: false);
        [Benchmark, Input(Form.Json)] public long Stj() => Libraries.JsonTokens(_doc.Json, text: false);
    }

    // M5a: the DOM parse of each document, YAML and JSON twin, beside
    // System.Text.Json's. The YAML rows are new at M5a (before it the DOM refused block YAML), so only the JSON-twin rows
    // compare with an earlier build by the dll swap.
    public class DomParse : AllDocuments
    {
        private static readonly System.Text.Yaml.YamlDocumentOptions s_yamlOptions = new() { MaxDepth = 256 };
        private static readonly System.Text.Json.JsonDocumentOptions s_jsonOptions = new() { MaxDepth = 256 };

        [Benchmark(Baseline = true)]
        public long Styml_Yaml()
        {
            using System.Text.Yaml.YamlDocument document = System.Text.Yaml.YamlDocument.Parse(_doc.Yaml, s_yamlOptions);
            return document.RootElement.GetPropertyCountOrLength();
        }

        [Benchmark, Input(Form.Json)]
        public long Styml_Json()
        {
            using System.Text.Yaml.YamlDocument document = System.Text.Yaml.YamlDocument.Parse(_doc.Json, s_yamlOptions);
            return document.RootElement.GetPropertyCountOrLength();
        }

        [Benchmark, Input(Form.Json)]
        public long Stj()
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(_doc.Json, s_jsonOptions);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object ? document.RootElement.GetPropertyCount() : document.RootElement.GetArrayLength();
        }

        // M6b: the other DOMs. The methods above are unchanged, for the M5
        // sitting's rows. The text-input libraries read the string decoded in setup; VYaml has no DOM.
        [Benchmark] public long Styml_Node() => Dom.StymlNode(_doc.Yaml);
        [Benchmark] public long YamlDotNet() => Dom.YamlDotNet(_doc.YamlText);
        [Benchmark] public long SharpYaml() => Dom.SharpYaml(_doc.YamlText);
        [Benchmark] public long SharpYaml_Syntax() => Dom.SharpYamlSyntax(_doc.YamlText);
        [Benchmark, Input(Form.Json)] public long Stj_Node() => Dom.StjNode(_doc.Json);
    }

    internal static class DomParseExtensions
    {
        internal static int GetPropertyCountOrLength(this System.Text.Yaml.YamlElement element) =>
            element.ValueKind == System.Text.Yaml.YamlValueKind.Object ? element.GetPropertyCount() : element.GetArrayLength();
    }

    // YamlDotNet and SharpYaml are not repeated here: their events carry every scalar as a string already, so their
    // Tokenize rows are their Tokenize + text rows.
    public class TokenizeText : AllDocuments
    {
        [Benchmark(Baseline = true)] public long Styml_Yaml() => Libraries.StymlTokens(_doc.Yaml, Libraries.StymlReader, text: true);
        [Benchmark] public long Styml_Yaml_NoKeys() => Libraries.StymlTokens(_doc.Yaml, Libraries.StymlReaderNoKeys, text: true);
        [Benchmark, Input(Form.Json)] public long Styml_Json() => Libraries.StymlTokens(_doc.Json, Libraries.StymlReader, text: true);
        [Benchmark, Input(Form.Json)] public long Styml_Json_NoKeys() => Libraries.StymlTokens(_doc.Json, Libraries.StymlReaderNoKeys, text: true);
        [Benchmark] public long VYaml() => Libraries.VYamlEvents(_doc.Yaml, text: true);
        [Benchmark, Input(Form.Json)] public long Stj() => Libraries.JsonTokens(_doc.Json, text: true);
    }

    // VYaml has no reflection serializer.
    public class DeserializeReflection : AllDocuments
    {
        [Benchmark(Baseline = true)] public object? Styml_Yaml() => Libraries.Styml(_doc.Yaml, _doc.Family, Libraries.StymlOptions);
        [Benchmark] public object? Styml_Yaml_NoKeys() => Libraries.Styml(_doc.Yaml, _doc.Family, Libraries.StymlOptionsNoKeys);
        [Benchmark, Input(Form.Json)] public object? Styml_Json() => Libraries.Styml(_doc.Json, _doc.Family, Libraries.StymlOptions);
        [Benchmark, Input(Form.Json)] public object? Styml_Json_NoKeys() => Libraries.Styml(_doc.Json, _doc.Family, Libraries.StymlOptionsNoKeys);
        [Benchmark] public object? YamlDotNet() => Libraries.YamlDotNet(_doc.YamlText, _doc.Family, Libraries.YamlDotNetReflection);
        [Benchmark] public object? SharpYaml() => Libraries.SharpYaml(_doc.YamlText, _doc.Family);
        [Benchmark, Input(Form.Json)] public object? Stj() => Libraries.Json(_doc.Json, _doc.Family);
    }

    public class DeserializeSourceGen : AllDocuments
    {
        [Benchmark(Baseline = true)] public object? Styml_Yaml() => Libraries.StymlGeneratedRead(_doc.Yaml, _doc.Family, Libraries.StymlGenerated);
        [Benchmark] public object? Styml_Yaml_NoKeys() => Libraries.StymlGeneratedRead(_doc.Yaml, _doc.Family, Libraries.StymlGeneratedNoKeys);
        [Benchmark, Input(Form.Json)] public object? Styml_Json() => Libraries.StymlGeneratedRead(_doc.Json, _doc.Family, Libraries.StymlGenerated);
        [Benchmark, Input(Form.Json)] public object? Styml_Json_NoKeys() => Libraries.StymlGeneratedRead(_doc.Json, _doc.Family, Libraries.StymlGeneratedNoKeys);
        [Benchmark] public object? YamlDotNet() => Libraries.YamlDotNet(_doc.YamlText, _doc.Family, Libraries.YamlDotNetStatic);
        [Benchmark] public object? SharpYaml() => Libraries.SharpYamlGeneratedRead(_doc.YamlText, _doc.Family);
        [Benchmark] public object? VYaml() => Libraries.VYaml(_doc.Yaml, _doc.Family);
        [Benchmark, Input(Form.Json)] public object? Stj() => Libraries.JsonGeneratedRead(_doc.Json, _doc.Family);
    }

    // Source-generated deserialization from a string: the natural input of YamlDotNet and SharpYaml, and a transcoding
    // step for the others (VYaml has no text input, so its row includes Encoding.UTF8.GetBytes).
    public class FromString : Scenario
    {
        [Params("k8s-m", "records-l")]
        public override string Document { get; set; } = null!;

        [Benchmark(Baseline = true)] public object? Styml_Yaml() => Libraries.StymlText(_doc.YamlText, _doc.Family, Libraries.StymlGenerated);
        [Benchmark] public object? YamlDotNet() => Libraries.YamlDotNet(_doc.YamlText, _doc.Family, Libraries.YamlDotNetStatic);
        [Benchmark] public object? SharpYaml() => Libraries.SharpYamlGeneratedRead(_doc.YamlText, _doc.Family);
        [Benchmark] public object? VYaml() => Libraries.VYaml(Encoding.UTF8.GetBytes(_doc.YamlText), _doc.Family);
        [Benchmark, Input(Form.Json)] public object? Stj() => Libraries.JsonGeneratedText(_doc.JsonText, _doc.Family);
    }

    // The one-line minified JSON of the large record list: the shape of M3d's gate 5.
    public class MinifiedJson : Scenario
    {
        [Params("records-l")]
        public override string Document { get; set; } = null!;

        [Benchmark(Baseline = true), Input(Form.MinJson)] public long Tokenize_Styml() => Libraries.StymlTokens(_doc.MinJson, Libraries.StymlReader, text: false);
        [Benchmark, Input(Form.MinJson)] public long Tokenize_Styml_NoKeys() => Libraries.StymlTokens(_doc.MinJson, Libraries.StymlReaderNoKeys, text: false);
        [Benchmark, Input(Form.MinJson)] public long Tokenize_Stj() => Libraries.JsonTokens(_doc.MinJson, text: false);
        [Benchmark, Input(Form.MinJson)] public object? Deserialize_Styml() => Libraries.Styml(_doc.MinJson, _doc.Family, Libraries.StymlOptions);
        [Benchmark, Input(Form.MinJson)] public object? Deserialize_Styml_NoKeys() => Libraries.Styml(_doc.MinJson, _doc.Family, Libraries.StymlOptionsNoKeys);
        [Benchmark, Input(Form.MinJson)] public object? Deserialize_Stj() => Libraries.Json(_doc.MinJson, _doc.Family);
    }
}
