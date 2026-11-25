---
title: Getting started
description: Install Styml, write an object as YAML, read it back, and edit a YAML document.
---

Styml is a YAML 1.2 library for .NET: a reader, a writer, a document model and a serializer with reflection and source
generation. It is System.Text.Json's code and API carried over to YAML, so if you know `JsonSerializer`, you know
`YamlSerializer`.

> [!TIP]
> The [playground](/styml/playground/) runs Styml in your browser: paste YAML, format it, convert it to JSON, and look
> at its document tree and its tokens.

## Install the package

Styml is published on [NuGet](https://www.nuget.org/packages/Styml) as `Styml`:

```shell
dotnet add package Styml
```

The package targets .NET 11, .NET 10 and .NET Standard 2.0, which covers .NET Framework 4.6.2 and later. Its namespace
is `System.Text.Yaml`, next to `System.Text.Json`, without conflict:

```csharp
using System.Text.Yaml;                 // YamlSerializer, YamlSerializerOptions, the reader, the writer, YamlDocument
using System.Text.Yaml.Nodes;           // YamlNode, YamlObject, YamlArray, YamlValue
using System.Text.Yaml.Serialization;   // attributes, converters, the source generator's types
```

Previews of the next version are published to a [Feedz feed](https://feedz.io/org/sebastienros/repository/styml/packages/Styml)
after every push to `main`:

```shell
dotnet nuget add source https://f.feedz.io/sebastienros/styml/nuget/index.json --name styml-preview
dotnet add package Styml --prerelease
```

## Write an object as YAML

`YamlSerializer.Serialize` writes any .NET object, with the public properties of its type. The samples of this page use
this record:

<!-- sample:first-types -->
```csharp
public record Service(string Name, string Image, int[] Ports, Dictionary<string, string> Environment);
```
<!-- /sample:first-types -->

<!-- sample:first-serialize -->
```csharp
var service = new Service("web", "nginx:1.27", [80, 443], new() { ["TIER"] = "front", ["REPLICAS"] = "3" });
string yaml = YamlSerializer.Serialize(service);
```

```yaml
Name: web
Image: nginx:1.27
Ports:
- 80
- 443
Environment:
  TIER: front
  REPLICAS: "3"
```
<!-- /sample:first-serialize -->

The writer quotes `"3"` because a plain `3` would read back as a number. It quotes exactly what YAML would otherwise
resolve to another type, and nothing else: see [Scalar styles and quoting](scalar-styles.md).

## Read YAML into an object

`YamlSerializer.Deserialize` reads YAML into a type. Members are matched by name, case-sensitively by default, and a
type with a constructor is created through it:

<!-- sample:first-deserialize -->
```csharp
string yaml = """
    Name: api
    Image: example/api:2.4
    Ports: [8080]
    Environment:
      LOG_LEVEL: debug
    """;
Service service = YamlSerializer.Deserialize<Service>(yaml)!;
Console.WriteLine($"{service.Name} on {service.Ports[0]}, {service.Environment.Count} variable");
```

```text
api on 8080, 1 variable
```
<!-- /sample:first-deserialize -->

The flow sequence `[8080]` reads like a block one: the style of the source doesn't matter to the serializer.

## Edit a YAML document

`YamlNode` is a mutable document, like `JsonNode`. It keeps the styles of the source, so an edited document is written
back the way it was written:

<!-- sample:first-node -->
```csharp
YamlNode config = YamlNode.Parse("""
    server:
      port: 8080
      hosts: [a.example, b.example]
    """)!;
config["server"]!["port"] = 9090;
config["server"]!["hosts"]!.AsArray().Add("c.example");
string yaml = config.ToYamlString();
```

```yaml
server:
  port: 9090
  hosts: [a.example, b.example, c.example]
```
<!-- /sample:first-node -->

## About the samples

Every sample of this guide is compiled and run by Styml's tests (`test/Styml.Yaml.Tests/Docs`), and the output shown
under it is the one the tests check, so the samples can't drift from what the library does. Where a sample shows an
exception, it calls a small helper that returns the value, or the type and message of the exception:

```csharp
static string Describe(Func<object?> read)
{
    try
    {
        return read()?.ToString() ?? "null";
    }
    catch (Exception e)
    {
        return e.GetType().Name + ": " + e.Message.Split('\n')[0];
    }
}
```

## Next steps

- [Coming from System.Text.Json](system-text-json.md): what is the same, and what YAML changes.
- [Serialize and deserialize](serialization.md): the serializer's methods, options and errors.
- [Source generation and Native AOT](source-generation.md): serialize without reflection.
- [The DOM](dom.md) and [the reader and the writer](reader-writer.md), to work with YAML without types.
- The [API reference](../api/index.md).
