---
title: The DOM
description: Read YAML without types with YamlDocument and YamlElement, edit it with YamlNode, and keep its styles, anchors and tags.
---

Styml has System.Text.Json's two document models. `YamlDocument` is read-only and fast: it parses into pooled
buffers, and its `YamlElement` values point into them. `YamlNode` is a tree of objects that you can change and write
back. Both keep what YAML adds: anchors, aliases, tags, and the style of each scalar and collection.

The samples of this page read this document:

```yaml
services:
  web:
    image: &image nginx:1.27
    ports: ["80:80"]
    command: !shell |
      nginx -g 'daemon off;'
  admin:
    image: *image
    replicas: 2
```

## Read with YamlDocument

`YamlDocument.Parse` reads a document, and its `RootElement` navigates it: `GetProperty`, `TryGetProperty`,
`EnumerateObject`, `EnumerateArray`, and the typed `Get` methods. An element also tells its anchor, whether it is an
alias, its tag and its style. Dispose the document to return its buffers to the pool:

<!-- sample:dom-document -->
```csharp
using YamlDocument document = YamlDocument.Parse(Compose);
YamlElement services = document.RootElement.GetProperty("services");
var lines = new List<string>();
foreach (YamlProperty service in services.EnumerateObject())
{
    YamlElement image = service.Value.GetProperty("image");
    string alias = image.IsAlias ? $" (alias of &{image.GetAnchor()})" : "";
    lines.Add($"{service.Name}: {image.GetString()}{alias}");
}

YamlElement command = services.GetProperty("web").GetProperty("command");
lines.Add($"command: tag {command.GetTag()}, {command.ScalarStyle}, {command.GetString()!.TrimEnd()}");
lines.Add($"ports: {services.GetProperty("web").GetProperty("ports").CollectionStyle}");
bool scaled = services.GetProperty("admin").TryGetProperty("replicas", out YamlElement replicas) && replicas.GetInt32() > 1;
lines.Add($"admin scaled: {scaled}");
```

```text
web: nginx:1.27
admin: nginx:1.27 (alias of &image)
command: tag !shell, Literal, nginx -g 'daemon off;'
ports: Flow
admin scaled: True
```
<!-- /sample:dom-document -->

An alias element reads as the node it refers to, and `IsAlias` and `GetAnchor()` tell it apart.

### Keep comments

With `CommentHandling = YamlCommentHandling.Allow`, a document keeps its comments, and writing it in block style puts
them back. Each comment stays with the entry or item it is above or ends the line of, or with the start or the end of
a collection: the comments at the top and the bottom of a file are the root's. The text is kept, not the blank lines,
the columns or the spacing after `#`:

<!-- sample:dom-document-comments -->
```csharp
var options = new YamlDocumentOptions { CommentHandling = YamlCommentHandling.Allow };
using YamlDocument document = YamlDocument.Parse("""
    # Service settings
    name: web # the public name

    # Ports to expose
    ports: [80, 443]
    """, options);

var stream = new MemoryStream();
using (var writer = new Utf8YamlWriter(stream))
{
    document.WriteTo(writer);
}

string yaml = Encoding.UTF8.GetString(stream.ToArray());
```

```yaml
# Service settings
name: web # the public name
# Ports to expose
ports: [80, 443]
```
<!-- /sample:dom-document-comments -->

A JSON-compatible writer writes no comments, and a node written again through an alias or a merge key writes none of
its source's. A comment the writer can't put where it was, such as one on a block scalar's header (`key: | # c`) or
after a `-` whose node starts on the next line, goes on its own line next to it. A few comments are not kept: one on a
directive's line, and those inside a key node read after `?` (after its properties or its header) or inside a
collection key.
`System.Text.Json`'s `JsonDocument` refuses `Allow`.

## Edit with YamlNode

`YamlNode.Parse` reads a mutable tree of `YamlObject`, `YamlArray` and `YamlValue`. The indexers read and replace
values, and `ToYamlString` writes the tree in the styles it was read in, anchors and tags included:

<!-- sample:dom-node -->
```csharp
YamlNode root = YamlNode.Parse(Compose)!;
YamlObject web = root["services"]!["web"]!.AsObject();
web["ports"]!.AsArray().Add("443:443");
web["environment"] = new YamlObject { ["TIER"] = "front" };
root["services"]!.AsObject().Remove("admin");
string yaml = root.ToYamlString();
```

```yaml
services:
  web:
    image: &image nginx:1.27
    ports: ["80:80", 443:443]
    command: !shell |
      nginx -g 'daemon off;'
    environment:
      TIER: front
```
<!-- /sample:dom-node -->

### Comments in a tree

A tree parsed with `CommentHandling = Allow` keeps the document's comments. Each entry or item has a comment above it
and one at the end of its line (`GetComment`, `SetComment` with a `YamlCommentPosition`), and each mapping or sequence
has its `StartComment` and `EndComment`, which hold the comments at the top and the bottom of the file for the root.
An entry keeps its comments when its value is replaced or it is renamed, and loses them when it is removed:

<!-- sample:dom-node-comments -->
```csharp
YamlObject config = YamlNode.Parse("""
    # Service settings
    name: web # the public name
    ports: [80, 443]
    """, documentOptions: new() { CommentHandling = YamlCommentHandling.Allow })!.AsObject();

config["name"] = "api"; // the entry keeps its comment
config.SetComment("ports", YamlCommentPosition.Leading, "Ports to expose");
config["replicas"] = 2;
config.SetComment("replicas", YamlCommentPosition.Trailing, "one per zone");
config.EndComment = "Generated";
string yaml = config.ToYamlString();
```

```yaml
# Service settings
name: api # the public name
# Ports to expose
ports: [80, 443]
replicas: 2 # one per zone
# Generated
```
<!-- /sample:dom-node-comments -->

A copy made through an alias keeps none of the anchored node's comments, and `DeepEquals` ignores comments, as it
ignores styles.

## Create nodes

Nodes are created with collection initializers, and values convert implicitly from .NET values. `CollectionStyle`
chooses block or flow, and `Anchor` and `Tag` set a node's anchor and tag:

<!-- sample:dom-create -->
```csharp
var config = new YamlObject
{
    ["name"] = "web",
    ["ports"] = new YamlArray(80, 443) { CollectionStyle = YamlCollectionStyle.Flow },
    ["limits"] = new YamlObject { ["cpu"] = 0.5, ["memory"] = "512Mi" },
};
config["limits"]!.Anchor = "limits";
config["defaults"] = new YamlObject { ["inherit"] = true };
config["defaults"]!.Tag = "!settings";
string yaml = config.ToYamlString();
int cpu = (int)(config["limits"]!["cpu"]!.GetValue<double>() * 1000);
```

```yaml
name: web
ports: [80, 443]
limits: &limits
  cpu: 0.5
  memory: 512Mi
defaults: !settings
  inherit: true

# 500m
```
<!-- /sample:dom-create -->

## Typed values

`GetValue<T>`, the explicit conversions and `Deserialize<T>` read typed values from nodes. `GetValueKind()` tells what
a scalar resolves to under the [schema](schemas.md): here `yes` is a string, as YAML 1.2 says:

<!-- sample:dom-typed-types -->
```csharp
public record Service(int Replicas, string Name);
```
<!-- /sample:dom-typed-types -->

<!-- sample:dom-typed -->
```csharp
YamlNode node = YamlNode.Parse("replicas: 3\nratio: 0.75\nenabled: yes\nname: '3'\n")!;
int replicas = node["replicas"]!.GetValue<int>();
double ratio = (double)node["ratio"]!;
string enabled = node["enabled"]!.GetValueKind().ToString();
string name = node["name"]!.GetValue<string>();
Service service = node.Deserialize<Service>(new YamlSerializerOptions { PropertyNameCaseInsensitive = true })!;
```

```text
3, 0.75, enabled is String, name '3', Service { Replicas = 3, Name = 3 }
```
<!-- /sample:dom-typed -->

## Styles

A parsed document is written back in its own styles: JSON read in comes out as JSON, a single-quoted string stays
single-quoted. `IgnoreSourceStyles` writes it in the options' styles instead, which turns JSON into block YAML:

<!-- sample:dom-styles -->
```csharp
YamlNode fromJson = YamlNode.Parse("""{"name": "web", "ports": [80, 443], "note": "kept as written"}""")!;
string asIs = fromJson.ToYamlString();
string block = fromJson.ToYamlString(new YamlSerializerOptions { IgnoreSourceStyles = true });
```

```yaml
{"name": "web", "ports": [80, 443], "note": "kept as written"}
---
name: web
ports:
- 80
- 443
note: kept as written
```
<!-- /sample:dom-styles -->

## Options

`YamlDocumentOptions` reads with the [schema](schemas.md), the duplicate-key policy, the alias bound and the merge
keys of your choice, and `YamlNodeOptions` sets case-insensitive property names on `YamlObject`. The
[playground](/styml/playground/) parses its input with `YamlDocument` and shows the tree of each document, with these
options.
