---
title: Writing YAML
description: Block or JSON output, indentation, sequence layout, line width, line breaks, comments and member order.
---

The serializer, `YamlNode.ToYamlString` and `Utf8YamlWriter` share one writer and its options. This page uses
`YamlSerializerOptions`; the same options are on `YamlWriterOptions` for the writer, and on
`[YamlSourceGenerationOptions]` for a [source-generated context](source-generation.md).

<!-- sample:write-types -->
```csharp
public class Pipeline
{
    public string Name { get; set; } = "ci";

    public string[] Branches { get; set; } = ["main", "release/*"];

    public List<Step> Steps { get; set; } = [new("restore", "dotnet restore"), new("test", "dotnet test --no-build")];
}

public record Step(string Name, string Run);
```
<!-- /sample:write-types -->

## Block YAML or JSON

`WriteStyle` is `Block` by default: mappings and sequences on their own lines, the style people write by hand.
`JsonCompatible` writes JSON, which any YAML reader also reads, on one line unless `WriteIndented` is set:

<!-- sample:write-styles -->
```csharp
var pipeline = new Pipeline();
string block = YamlSerializer.Serialize(pipeline);
string json = YamlSerializer.Serialize(pipeline, new YamlSerializerOptions
{
    WriteStyle = YamlWriterStyle.JsonCompatible,
});
```

```yaml
Name: ci
Branches:
- main
- release/*
Steps:
- Name: restore
  Run: dotnet restore
- Name: test
  Run: dotnet test --no-build
---
{"Name":"ci","Branches":["main","release/*"],"Steps":[{"Name":"restore","Run":"dotnet restore"},{"Name":"test","Run":"dotnet test --no-build"}]}
```
<!-- /sample:write-styles -->

## Indentation and sequences

`IndentSize` sets the indentation of block YAML (2 by default, at least 2). A sequence under a key is written at the
key's column by default, as Kubernetes and most tools write it; `IndentSequences` indents it under the key.
`ExpandSequenceItems` starts a mapping that is a sequence item on the line after its dash:

<!-- sample:write-layout -->
```csharp
var options = new YamlSerializerOptions
{
    IndentSize = 4,
    IndentSequences = true,
    ExpandSequenceItems = true,
};
string yaml = YamlSerializer.Serialize(new Pipeline(), options);
```

```yaml
Name: ci
Branches:
    - main
    - release/*
Steps:
    -
        Name: restore
        Run: dotnet restore
    -
        Name: test
        Run: dotnet test --no-build
```
<!-- /sample:write-layout -->

## Line width

`MaxLineWidth` folds plain and double-quoted scalars at a space past the width, and continues a flow collection on a new
line. Folding doesn't change the value: the lines of a plain scalar join with spaces when read. 0, the default, folds
nothing:

<!-- sample:write-width -->
```csharp
var options = new YamlSerializerOptions { MaxLineWidth = 50 };
string yaml = YamlSerializer.Serialize(new
{
    Summary = "Restores the packages, builds the solution in Release, and runs every test project in parallel.",
    Tags = new[] { "dotnet", "yaml", "serializer", "source-generator", "aot", "trimming" },
}, options);
YamlNode flow = YamlNode.Parse("tags: [dotnet, yaml, serializer, source-generator, aot, trimming]")!;
string flowYaml = flow.ToYamlString(options);
```

```yaml
Summary: Restores the packages, builds the
  solution in Release, and runs every test project
  in parallel.
Tags:
- dotnet
- yaml
- serializer
- source-generator
- aot
- trimming
---
tags: [dotnet, yaml, serializer, source-generator, aot,
  trimming]
```
<!-- /sample:write-width -->

## Line breaks

The writer ends lines with `Environment.NewLine` unless `NewLine` is set to `"\n"` or `"\r\n"`. Set it to get the same
output on every operating system, in tests or in generated files.

## Comments and order

`[YamlComment]` writes a comment above a member, one `#` line per line of text, in block style (JSON has no comments).
Members are written in declaration order, moved by `[YamlPropertyOrder]`; `MappingOrder = Sorted` writes them by
order, then by name in ordinal order. Dictionaries keep their own order:

<!-- sample:comments-types -->
```csharp
public class ServerSettings
{
    public bool Tls { get; set; } = true;

    [YamlComment("Requests per second.\nOver this limit, clients get a 429.")]
    public int RateLimit { get; set; } = 100;

    [YamlComment("The address the server listens on.")]
    public string Host { get; set; } = "0.0.0.0";

    [YamlPropertyOrder(-1)]
    public string Name { get; set; } = "api";
}
```
<!-- /sample:comments-types -->

<!-- sample:comments -->
```csharp
string declared = YamlSerializer.Serialize(new ServerSettings());
string sorted = YamlSerializer.Serialize(new ServerSettings(), new YamlSerializerOptions
{
    MappingOrder = YamlMappingOrder.Sorted,
});
```

```yaml
Name: api
Tls: true
# Requests per second.
# Over this limit, clients get a 429.
RateLimit: 100
# The address the server listens on.
Host: 0.0.0.0
---
Name: api
# The address the server listens on.
Host: 0.0.0.0
# Requests per second.
# Over this limit, clients get a 429.
RateLimit: 100
Tls: true
```
<!-- /sample:comments -->

Comments of a parsed document are not kept by the [DOM](dom.md). The [contract model](contracts.md) sets comments at run
time, and `Utf8YamlWriter.WriteCommentValue` writes them anywhere.

## Styles of scalars

Which strings are quoted, and how multi-line strings are written, is the subject of
[Scalar styles and quoting](scalar-styles.md).
