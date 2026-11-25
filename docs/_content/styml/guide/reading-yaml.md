---
title: Reading YAML
description: Duplicate keys, empty documents, streams of documents, collection keys, JSON input with comments, and depth.
---

Styml reads YAML 1.2: it accepts all 308 valid cases of the [yaml-test-suite](https://github.com/yaml/yaml-test-suite)
with the expected events, and rejects its 94 error cases. This page covers what a reader of YAML meets beyond
mappings and sequences.

## Duplicate keys

A key that appears twice in a mapping is an error, as YAML 1.2 requires. `DuplicateKeyHandling` keeps the last
(`LastWins`) or the first (`FirstWins`) instead, for documents written by tools that don't check:

<!-- sample:duplicates -->
```csharp
string yaml = "image: nginx:1.26\nimage: nginx:1.27\n";
string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, string>>(yaml));
var last = YamlSerializer.Deserialize<Dictionary<string, string>>(yaml,
    new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins })!;
```

```text
YamlException: Duplicate properties not allowed during deserialization.
LastWins: nginx:1.27
```
<!-- /sample:duplicates -->

## Empty documents

Empty input, input with only comments, and `~` read as `null`, where System.Text.Json would throw on empty input:

<!-- sample:empty -->
```csharp
Step? fromEmpty = YamlSerializer.Deserialize<Step>("");
Step? fromComments = YamlSerializer.Deserialize<Step>("# nothing configured yet\n");
int[]? fromNull = YamlSerializer.Deserialize<int[]>("~");
```

```text
True, True, True
```
<!-- /sample:empty -->

## Several documents in one stream

A YAML stream holds documents separated by `---`, optionally ended by `...`. `DeserializeAsyncEnumerable` with
`topLevelValues: true` reads one value per document as they arrive, and `SerializeAsyncEnumerable` writes `---`
between them:

<!-- sample:documents -->
```csharp
string stream = """
    kind: Service
    name: web
    ---
    kind: Deployment
    name: web
    ...
    ---
    kind: Ingress
    name: web
    """;
var kinds = new List<string>();
using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
await foreach (var manifest in YamlSerializer.DeserializeAsyncEnumerable<Dictionary<string, string>>(input, topLevelValues: true))
{
    kinds.Add(manifest!["kind"]);
}

using var output = new MemoryStream();
await YamlSerializer.SerializeAsyncEnumerable(output, Manifests(), topLevelValues: true);

static async IAsyncEnumerable<object> Manifests()
{
    yield return new { kind = "ConfigMap", name = "settings" };
    await Task.Yield();
    yield return new { kind = "Secret", name = "tokens" };
}
```

```yaml
# read: Service, Deployment, Ingress
kind: ConfigMap
name: settings
---
kind: Secret
name: tokens
```
<!-- /sample:documents -->

`YamlReaderOptions.AllowMultipleValues` lets the reader go past the first document, so `YamlDocument.ParseValue`
reads one document at a time:

<!-- sample:documents-reader -->
```csharp
byte[] utf8 = Encoding.UTF8.GetBytes("a: 1\n---\nb: 2\n");
var reader = new Utf8YamlReader(utf8, new YamlReaderOptions { AllowMultipleValues = true });
var roots = new List<string>();
while (reader.Read())
{
    using YamlDocument document = YamlDocument.ParseValue(ref reader);
    roots.Add(document.RootElement.EnumerateObject().First().Name);
}
```

```text
a, b
```
<!-- /sample:documents-reader -->

## Collection keys

A key can be a sequence or a mapping, explicitly (`? [0, 0]`) or, in flow style, implicitly (`[1, 0]: east`). It
reads into a dictionary whose key type is a collection, and the DOM and the reader show it (`YamlProperty.KeyKind`,
`GetKeyElement()`, `YamlObject.CollectionKeys`, `Utf8YamlReader.KeyKind`):

<!-- sample:collection-keys -->
```csharp
string yaml = """
    ? [0, 0]
    : origin
    [1, 0]: east
    ? {x: 0, y: 1}
    : north
    """;
var grid = YamlSerializer.Deserialize<Dictionary<List<int>, string>>("? [0, 0]\n: origin\n[1, 0]: east\n")!;
string first = $"[{string.Join(", ", grid.Keys.First())}] -> {grid.Values.First()}";
using YamlDocument document = YamlDocument.Parse(yaml);
string kinds = string.Join(", ", document.RootElement.EnumerateObject().Select(p => $"{p.GetKeyElement().ValueKind} {p.KeyKind}"));
string written = YamlSerializer.Serialize(grid);
```

```yaml
# [0, 0] -> origin
# Array Complex, Array Complex, Object Complex
? - 0
  - 0
: origin
? - 1
  - 0
: east
```
<!-- /sample:collection-keys -->

`DisallowImplicitCollectionKeys` turns the implicit form off, so that a stream of flow collections, such as JSON
lines, yields each one as soon as it is complete. The JSON-compatible style can't write collection keys.

## JSON input

JSON is YAML, so `YamlSerializer` reads JSON text as it is, with YAML's rules: duplicate keys are an error, `#`
comments are allowed, and a trailing comma in a flow collection is accepted. JSON's own `//` and `/* */` comments are
not YAML; `ReadCommentHandling = Skip` reads them as System.Text.Json does, inside flow collections and around the
root value:

<!-- sample:json-input -->
```csharp
string json = """
    // The service, from a JSON file with comments
    {
      "name": "web",      /* the public name */
      "ports": [80, 443,],
      "url": "http://example.org", // slashes in a value are text
    }
    """;
var options = new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip };
var service = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>(json, options)!;
string compact = YamlSerializer.Serialize(service, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
```

```json
{"name":"web","ports":[80,443],"url":"http://example.org"}
```
<!-- /sample:json-input -->

In block collections, `//` stays text, so `url: http://x // y` keeps its value. With `Allow`, `Utf8YamlReader` returns
JSON comments as `Comment` tokens.

## Depth

Nesting is limited to 64 levels by default, as in System.Text.Json, which bounds the stack a document can use.
`MaxDepth` raises or lowers it:

<!-- sample:depth -->
```csharp
string deep = string.Concat(Enumerable.Repeat("[", 100)) + string.Concat(Enumerable.Repeat("]", 100));
string error = Describe(() => YamlSerializer.Deserialize<object>(deep));
object? read = YamlSerializer.Deserialize<object>(deep, new YamlSerializerOptions { MaxDepth = 128 });
```

```text
YamlException: The maximum configured depth of 64 has been exceeded. Cannot read next YAML array. Path: $ | LineNumber: 0 | BytePositionInLine: 64.
MaxDepth 128: YamlElement
```
<!-- /sample:depth -->

## Errors

A `YamlException` reports the path of the value (`$.Ports[1]`), its line and its column, from 0. The
[playground](/styml/playground/) shows them on its *An error* sample.
