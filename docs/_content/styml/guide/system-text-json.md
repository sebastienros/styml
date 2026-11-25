---
title: Coming from System.Text.Json
description: Styml is System.Text.Json renamed for YAML. What carries over as it is, what YAML adds, and what behaves differently.
---

Styml is a clone of System.Text.Json from [dotnet/runtime](https://github.com/dotnet/runtime) at tag `v11.0.0`,
renamed and adapted to YAML 1.2. Its code, its tests and most of its documentation are System.Text.Json's, so code
written for System.Text.Json reads the same with `Yaml` in place of `Json`:

<!-- sample:stj-side-by-side -->
```csharp
// System.Text.Json
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
string json = JsonSerializer.Serialize(service, jsonOptions);
Service? fromJson = JsonSerializer.Deserialize<Service>(json, jsonOptions);

// Styml
var yamlOptions = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
string yaml = YamlSerializer.Serialize(service, yamlOptions);
Service? fromYaml = YamlSerializer.Deserialize<Service>(yaml, yamlOptions);
```
<!-- /sample:stj-side-by-side -->

## Names

Every public type and member is renamed the same way, and keeps its meaning:

| System.Text.Json | Styml |
| --- | --- |
| `System.Text.Json` namespace | `System.Text.Yaml` |
| `JsonSerializer`, `JsonSerializerOptions` | [`YamlSerializer`](../api/system.text.yaml.yamlserializer.md), [`YamlSerializerOptions`](../api/system.text.yaml.yamlserializeroptions.md) |
| `Utf8JsonReader`, `Utf8JsonWriter` | [`Utf8YamlReader`](../api/system.text.yaml.utf8yamlreader.md), [`Utf8YamlWriter`](../api/system.text.yaml.utf8yamlwriter.md) |
| `JsonDocument`, `JsonElement` | [`YamlDocument`](../api/system.text.yaml.yamldocument.md), [`YamlElement`](../api/system.text.yaml.yamlelement.md) |
| `JsonNode`, `JsonObject`, `JsonArray`, `JsonValue` | [`YamlNode`](../api/system.text.yaml.nodes.yamlnode.md), [`YamlObject`](../api/system.text.yaml.nodes.yamlobject.md), [`YamlArray`](../api/system.text.yaml.nodes.yamlarray.md), [`YamlValue`](../api/system.text.yaml.nodes.yamlvalue.md) |
| `JsonConverter<T>`, `JsonConverterFactory` | [`YamlConverter<T>`](../api/system.text.yaml.serialization.yamlconverter-1.md), [`YamlConverterFactory`](../api/system.text.yaml.serialization.yamlconverterfactory.md) |
| `JsonPropertyName`, `JsonIgnore`, `JsonInclude`, `JsonRequired`... | `YamlPropertyName`, `YamlIgnore`, `YamlInclude`, `YamlRequired`... |
| `JsonDerivedType`, `JsonPolymorphic` | `YamlDerivedType`, `YamlPolymorphic` |
| `JsonTypeInfo`, `DefaultJsonTypeInfoResolver` | [`YamlTypeInfo`](../api/system.text.yaml.serialization.metadata.yamltypeinfo.md), [`DefaultYamlTypeInfoResolver`](../api/system.text.yaml.serialization.metadata.defaultyamltypeinforesolver.md) |
| `JsonSerializerContext`, `[JsonSerializable]`, `[JsonSourceGenerationOptions]` | [`YamlSerializerContext`](../api/system.text.yaml.serialization.yamlserializercontext.md), `[YamlSerializable]`, `[YamlSourceGenerationOptions]` |
| `JsonException` | [`YamlException`](../api/system.text.yaml.yamlexception.md) |

The package is `Styml` and its assembly `Styml.dll`, so an application can use both libraries at once.

## JSON is YAML

YAML 1.2 is a superset of JSON: Styml reads JSON text as it is, and writes JSON with
`WriteStyle = YamlWriterStyle.JsonCompatible`. One serializer can read both formats:

<!-- sample:json-is-yaml -->
```csharp
var service = YamlSerializer.Deserialize<Service>("""
    {"Name": "web", "Image": "nginx:1.27", "Ports": [80, 443], "Environment": {"TIER": "front"}}
    """)!;
string yaml = YamlSerializer.Serialize(service);
string json = YamlSerializer.Serialize(service, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
```

```yaml
Name: web
Image: nginx:1.27
Ports:
- 80
- 443
Environment:
  TIER: front
---
{"Name":"web","Image":"nginx:1.27","Ports":[80,443],"Environment":{"TIER":"front"}}
```
<!-- /sample:json-is-yaml -->

## From System.Text.Json's documents

`YamlSerializer` has no converters for `JsonElement` and `JsonNode`: a `JsonElement` comes out as a mapping with one
`ValueKind` entry. Their JSON text is YAML, though: `YamlNode.Parse(node.ToJsonString())` and
`YamlNode.Parse(element.GetRawText())` read it, and `IgnoreSourceStyles` writes it as block YAML, as in
[Styles](dom.md#styles).

A `Utf8YamlWriter` writes a `JsonElement` as YAML without parsing its text again. Its methods are
`Utf8JsonWriter`'s, so a walk of the element is all it takes:

<!-- sample:json-writer-type -->
```csharp
static void WriteYaml(Utf8YamlWriter writer, JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                WriteYaml(writer, property.Value);
            }
            writer.WriteEndObject();
            break;
        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray())
            {
                WriteYaml(writer, item);
            }
            writer.WriteEndArray();
            break;
        case JsonValueKind.String:
            writer.WriteStringValue(element.GetString());
            break;
        case JsonValueKind.Number:
            // The number as written: 1.50 stays 1.50, and a big integer isn't rounded to a double.
            writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
            break;
        case JsonValueKind.True:
        case JsonValueKind.False:
            writer.WriteBooleanValue(element.GetBoolean());
            break;
        default:
            writer.WriteNullValue();
            break;
    }
}
```
<!-- /sample:json-writer-type -->

The writer quotes the strings that would read back as another type, `yes` and `8080` here, and a string with line
breaks is written as a literal block:

<!-- sample:json-writer -->
```csharp
using JsonDocument json = JsonDocument.Parse("""
    {"name": "web", "replicas": 3, "ratio": 1.50, "tags": ["front", "yes", "8080"], "script": "nginx -t\nnginx -s reload"}
    """);
using var buffer = new MemoryStream();
using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { NewLine = "\n" }))
{
    WriteYaml(writer, json.RootElement);
}

string yaml = Encoding.UTF8.GetString(buffer.ToArray());
```

```yaml
name: web
replicas: 3
ratio: 1.50
tags:
- front
- "yes"
- "8080"
script: |-
  nginx -t
  nginx -s reload
```
<!-- /sample:json-writer -->

`YamlElement.Parse` turns a `JsonElement` into a `YamlElement`, which needs no document to dispose. Strings stay
strings, as JSON's are quoted, and numbers, `true`, `false` and `null` keep their kinds. `JsonDocument` accepts a key
twice and finds its last value, so `LastWins` reads the same document:

<!-- sample:json-element -->
```csharp
using JsonDocument json = JsonDocument.Parse("""
    {"name": "web", "ports": [80, 443], "tls": {"enabled": true}, "port": "8080", "port": "9090"}
    """);
// JsonDocument keeps duplicate keys and finds the last one, where YAML rejects them by default.
var options = new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
YamlElement element = YamlElement.Parse(json.RootElement.GetRawText(), options);

YamlElement ports = element.GetProperty("ports");
string line = $"{element.ValueKind}, {ports.GetArrayLength()} ports, last {ports[1].GetInt32()}, "
    + $"tls {element.GetProperty("tls").GetProperty("enabled").GetBoolean()}, "
    + $"port {element.GetProperty("port").GetString()} ({element.GetProperty("port").ValueKind})";
```

```text
Object, 2 ports, last 443, tls True, port 9090 (String)
```
<!-- /sample:json-element -->

## What YAML adds

- **Anchors and aliases**, read everywhere and written by `ReferenceHandler.Preserve` in place of `$id` and `$ref`,
  with a bound on alias expansion: [References](references.md).
- **Tags**, tag-based polymorphism and tag mappings, which are explicit allow-lists: [Polymorphism and tags](polymorphism.md).
- **Styles**: block and flow collections, five scalar styles, comments written from `[YamlComment]`:
  [Writing YAML](writing-yaml.md) and [Scalar styles](scalar-styles.md).
- **Schemas**: the YAML 1.2 core schema by default, the JSON and failsafe schemas, and an opt-in YAML 1.1 schema:
  [Schemas and typed reads](schemas.md).
- **Merge keys** (`<<`), **collection keys** (`? [a, b]: v`) and **streams of documents** (`---`):
  [Reading YAML](reading-yaml.md).

## What behaves differently

- **The writer writes block YAML by default.** `WriteStyle = YamlWriterStyle.JsonCompatible` writes JSON, which
  `WriteIndented` formats as System.Text.Json does. Block YAML is always on several lines.
- **A plain scalar is text until a typed read asks for something else.** `12` reads into an `int` and into a `string`;
  into `object` it is a `YamlElement` of kind `Number`. A quoted `"12"` is a string and doesn't read into an `int`.
- **Duplicate keys are an error**, as YAML requires, unless `DuplicateKeyHandling` says otherwise.
- **Empty input reads as no value** (`null`) instead of an error, and several top-level values are several documents.
- **Comments are YAML's** (`#`). JSON's `//` and `/* */` are read only when `ReadCommentHandling` asks for them, and
  `AllowTrailingCommas` changes nothing: YAML already accepts a trailing comma in a flow collection.
- **The APIs that were obsolete upstream are removed**: `IgnoreNullValues` (use `DefaultIgnoreCondition`) and
  `AddContext` (use `TypeInfoResolver` or `TypeInfoResolverChain`).

[docs/feature-parity.md](https://github.com/sebastienros/styml/blob/main/docs/feature-parity.md) compares Styml with
YamlDotNet and SharpYaml, feature by feature, for a migration from those libraries.
