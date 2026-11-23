## About

Styml is a YAML 1.2 library for .NET with the API of System.Text.Json: `YamlSerializer`, `Utf8YamlReader`,
`Utf8YamlWriter`, `YamlDocument`, `YamlNode`, converters and a source generator, in the `System.Text.Yaml` namespace.
It is derived from System.Text.Json (dotnet/runtime v11.0.0) and adapted to YAML. It is an independent project, not a
Microsoft or .NET Foundation product.

## Key features

* Serialize and deserialize .NET types with reflection or with source-generated contracts (`[YamlSerializable]`).
* A forward-only `Utf8YamlReader` and a `Utf8YamlWriter` that writes block style by default.
* A read-only `YamlDocument` and a mutable `YamlNode` DOM.
* YAML's own features: anchors and aliases, tags, block and flow styles, the core, JSON and failsafe schemas, merge
  keys, and collection keys.
* Trimming and Native AOT with the source generator; `net11.0`, `net10.0` and `netstandard2.0`.

## How to use

```csharp
using System.Text.Yaml;
using System.Text.Yaml.Serialization;

var service = new Service("web", "nginx:1.27", [80, 443], new() { ["TIER"] = "front", ["REPLICAS"] = "3" });

string yaml = YamlSerializer.Serialize(service);
// Name: web
// Image: nginx:1.27
// Ports:
// - 80
// - 443
// Environment:
//   TIER: front
//   REPLICAS: "3"

Service? back = YamlSerializer.Deserialize<Service>(yaml);

// The same with a source-generated context (trimming and Native AOT).
string generated = YamlSerializer.Serialize(service, SampleContext.Default.Service);

public record Service(string Name, string Image, int[] Ports, Dictionary<string, string> Environment);

[YamlSerializable(typeof(Service))]
internal partial class SampleContext : YamlSerializerContext;
```

The DOM, with an anchor, an alias and a merge key:

```csharp
using System.Text.Yaml;
using System.Text.Yaml.Nodes;

YamlNode root = YamlNode.Parse(
    """
    defaults: &defaults
      image: nginx:1.27
      restart: always
    web:
      <<: *defaults
      ports: [80, 443]
    """,
    documentOptions: new YamlDocumentOptions { ResolveMergeKeys = true })!;

Console.WriteLine(root["web"]!["restart"]); // always
```

## Benchmarks

Styml against YamlDotNet 18.1.0, SharpYaml 3.17.0 and VYaml 1.6.1, with System.Text.Json on the same data as JSON as
the reference; .NET 11.0.0, Apple M4 Pro, BenchmarkDotNet ShortRun. The first two tables are the Kubernetes manifests
of the repository's benchmark corpus (76 KB); the charts, every document and the method are in the repository's
docs/benchmarks.

<!-- panel:pivot -->
Mean time on k8s-m (microseconds; smaller is better):

| Scenario | Styml | VYaml | SharpYaml | YamlDotNet | System.Text.Json |
| --- | ---: | ---: | ---: | ---: | ---: |
| Deserialize, source-generated | **290.0** | 370.1 | 1,030.6 | 2,090.7 | 177.7 |
| Deserialize, reflection | **324.0** | n/a | 1,563.8 | 2,733.2 | 198.0 |
| Serialize, source-generated | **53.7** | 100.2 | 182.4 | 1,832.3 | 39.5 |
| Serialize, reused buffer | **50.8** | 100.2 | 182.4 | — | — |
| DOM parse (`YamlDocument`, `JsonDocument`) | **259.4** | n/a | 1,248.0 | 1,760.4 | 158.3 |
| DOM parse, mutable (`YamlNode`, `JsonNode`) | **409.4** | n/a | 1,248.0 | 1,760.4 | 344.5 |
| Tokenize | **189.7** | 309.0 | 891.0 | 1,360.2 | 119.1 |

Allocated on k8s-m (smaller is better):

| Scenario | Styml | VYaml | SharpYaml | YamlDotNet | System.Text.Json |
| --- | ---: | ---: | ---: | ---: | ---: |
| Deserialize, source-generated | **188.8 KB** | 185.5 KB | 1.60 MB | 3.39 MB | 188.6 KB |
| Deserialize, reflection | **188.8 KB** | n/a | 1.84 MB | 3.60 MB | 188.6 KB |
| Serialize, source-generated | **73.1 KB** | 6.2 KB | 25.3 KB | 2.06 MB | 55.0 KB |
| Serialize, reused buffer | **0 B** | 6.2 KB | 25.3 KB | — | — |
| DOM parse (`YamlDocument`, `JsonDocument`) | **72 B** | n/a | 1.83 MB | 2.75 MB | 72 B |
| DOM parse, mutable (`YamlNode`, `JsonNode`) | **815.9 KB** | n/a | 1.83 MB | 2.75 MB | 809.5 KB |
| Tokenize | **0 B** | 1.1 KB | 1.26 MB | 1.78 MB | 0 B |

Time against Styml's, the geometric mean over the five documents (below 1 is faster than Styml):

| Scenario | Styml | VYaml | SharpYaml | YamlDotNet | System.Text.Json |
| --- | ---: | ---: | ---: | ---: | ---: |
| Deserialize, source-generated | **1.00×** | 1.42× | 3.66× | 7.26× | 0.65× |
| Deserialize, reflection | **1.00×** | n/a | 4.57× | 8.62× | 0.62× |
| Serialize, source-generated | **1.00×** | 1.76× | 2.92× | 28.06× | 0.66× |
| Serialize, reused buffer | **1.00×** | 1.89× | 3.15× | — | — |
| DOM parse (`YamlDocument`, `JsonDocument`) | **1.00×** | n/a | 4.86× | 6.99× | 0.59× |
| DOM parse, mutable (`YamlNode`, `JsonNode`) | **1.00×** | n/a | 2.76× | 3.97× | 0.78× |
| Tokenize | **1.00×** | 1.79× | 4.95× | 7.43× | 0.65× |
<!-- /panel:pivot -->

## Main types

* `System.Text.Yaml.YamlSerializer`
* `System.Text.Yaml.Utf8YamlReader` and `System.Text.Yaml.Utf8YamlWriter`
* `System.Text.Yaml.YamlDocument` and `System.Text.Yaml.Nodes.YamlNode`
* `System.Text.Yaml.Serialization.YamlConverter<T>` and `YamlSerializerContext`

## More

The repository's README covers the relationship with System.Text.Json, YAML conformance and benchmarks:
https://github.com/sebastienros/styml. Styml is released under the MIT license; the package carries
THIRD-PARTY-NOTICES.txt, which credits dotnet/runtime.
