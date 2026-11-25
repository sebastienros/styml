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

## YAML versions

A `%YAML` directive names the version a document was written for. Styml reads a document of any `1.x` version as
YAML 1.2, with no warning, and refuses another major version:

<!-- sample:versions -->
```csharp
static string Read(string version) => Describe(() =>
{
    var values = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>($"%YAML {version}\n---\nanswer: yes\n")!;
    return values["answer"].ValueKind;
});

string output = string.Join("\n", new[] { "1.1", "1.2", "1.3", "2.0" }.Select(version => $"{version}: {Read(version)}"));
```

```text
1.1: String
1.2: String
1.3: String
2.0: YamlException: Unsupported YAML version '2.0'. Path: $ | LineNumber: 0 | BytePositionInLine: 0.
```
<!-- /sample:versions -->

The directive does not choose the [schema](schemas.md): under `%YAML 1.1`, `yes` is still a string unless `Schema`
is `Yaml11`.

YAML 1.3 is an unreleased draft, whose stated goal is to make no significant change to the language, so a
`%YAML 1.3` document is read by the same rules. The [yaml-test-suite](https://github.com/yaml/yaml-test-suite) has
22 cases rewritten for 1.3, which Styml accepts, and 30 it marks as invalid in 1.3, which Styml keeps accepting
because YAML 1.2 allows them.

### The YAML 1.3 draft restrictions

To refuse those documents, set `YamlReaderOptions.ApplyYaml13DraftRestrictions`. `Utf8YamlReader` then reports
each of these forms as an error:

- a scalar at the top level of a document with no `---` line before it;
- a plain or quoted scalar that starts on the `---` line (`--- text`), where a block scalar may start (`--- |`);
- the content of a top-level block scalar with no indentation;
- outside a flow collection, an anchor or a tag alone on its line, unless the line before ends with the indicator
  its node follows (`top: &a`, then the mapping);
- an anchor or alias name with an ASCII character that is not a letter, a digit, `_` or `-` (`&a:b`);
- a flow collection as an implicit key of a block mapping (`[a, b]: v`), where `? [a, b]` and `{[a, b]: v}` stay
  valid.

<!-- sample:versions-draft -->
```csharp
static string Read(string yaml) => Describe(() =>
{
    var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { ApplyYaml13DraftRestrictions = true });
    while (reader.Read())
    {
    }
    return "read";
});

string[] documents = { "text\n", "---\ntext\n", "--- text\n", "&a\nkey: v\n", "--- &a\nkey: v\n", "[a, b]: v\n" };
string output = string.Join("\n", documents.Select(yaml => $"{yaml.Replace("\n", "\\n")} => {Read(yaml)}"));
```

```text
text\n => YamlReaderException: With ApplyYaml13DraftRestrictions, a scalar at the top level of a YAML document needs a '---' line before it. LineNumber: 0 | BytePositionInLine: 0.
---\ntext\n => read
--- text\n => YamlReaderException: With ApplyYaml13DraftRestrictions, a plain or quoted scalar may not start on the '---' line of a YAML document; only a block scalar may. LineNumber: 0 | BytePositionInLine: 4.
&a\nkey: v\n => YamlReaderException: With ApplyYaml13DraftRestrictions, a YAML anchor or tag may not be alone on its line; write it after the indicator before its node, or on the first line of the node. LineNumber: 0 | BytePositionInLine: 0.
--- &a\nkey: v\n => read
[a, b]: v\n => YamlReaderException: With ApplyYaml13DraftRestrictions, a YAML flow collection may not be an implicit key of a block mapping; use an explicit key ('? '). LineNumber: 0 | BytePositionInLine: 0.
```
<!-- /sample:versions-draft -->

What to know before relying on it:

- **The rules are drafts.** They come from proposals that were never merged into a specification and from the
  suite's 1.3 cases, limited to what the 30 cases need, and may change.
- **It is a reader option.** `YamlDocument.Parse` and `YamlSerializer` do not have it. To get a document, read
  with a `Utf8YamlReader` that has the option and pass it to `YamlDocument.ParseValue`.
- **The directive plays no part.** A `%YAML 1.2` document is checked by the same rules, and without the option a
  `%YAML 1.3` document is not checked.
- **It refuses more than the 30 cases.** The suite's marks are incomplete: 31 other valid cases hold the same
  forms and are refused too, four of them cases the suite itself rewrote for 1.3.
- **Some of Styml's own output is refused.** The writer writes a top-level scalar with no `---` line unless
  `WriteDocumentStart` is called first, and puts the anchor or tag of a top-level block collection on a line of
  its own.

## Errors

A `YamlException` reports the path of the value (`$.Ports[1]`), its line and its column, from 0. The
[playground](/styml/playground/) shows them on its *An error* sample.
