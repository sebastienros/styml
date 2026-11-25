---
title: Deserialization scenarios
description: Required members, nullable annotations, unknown keys, extension data, immutable types, and populating existing objects.
---

These are the deserialization options and attributes of System.Text.Json, renamed for YAML. They behave the same,
with YAML's mappings in place of JSON's objects.

## Case-insensitive names

Names are matched case-sensitively by default. `PropertyNameCaseInsensitive` matches them in any case:

<!-- sample:case-insensitive -->
```csharp
var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true };
Probe probe = YamlSerializer.Deserialize<Probe>("path: /ready\nINTERVAL: 10\n", options)!;
```

```text
/ready every 10s
```
<!-- /sample:case-insensitive -->

## Required members

A member marked `[YamlRequired]`, or declared `required` in C#, must be in the YAML. A missing one is an error:

<!-- sample:required-types -->
```csharp
public class Job
{
    [YamlRequired]
    public string Name { get; set; } = "";

    public required string Image { get; set; }

    public string? Schedule { get; set; }
}
```
<!-- /sample:required-types -->

<!-- sample:required -->
```csharp
string missing = Describe(() => YamlSerializer.Deserialize<Job>("Name: backup\n"));
```

```text
YamlException: YAML deserialization for type 'System.Text.Yaml.Tests.Docs.ContractSamples+Job' was missing required properties including: 'Image'.
```
<!-- /sample:required -->

## Nullable annotations

By default, `null` reads into any reference type, whatever its annotation. `RespectNullableAnnotations` refuses a
`null` for a member or a constructor parameter that is not nullable (`string` rather than `string?`), and
`RespectRequiredConstructorParameters` requires the constructor parameters without a default value:

<!-- sample:nullable-types -->
```csharp
public record Volume(string Name, string Path, string? Description);
```
<!-- /sample:nullable-types -->

<!-- sample:nullable -->
```csharp
string yaml = "Name: data\nPath: null\n";
string lax = Describe(() => YamlSerializer.Deserialize<Volume>(yaml));
string strict = Describe(() => YamlSerializer.Deserialize<Volume>(yaml, new YamlSerializerOptions
{
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
}));
```

```text
lax: Volume { Name = data, Path = , Description =  }
strict: YamlException: The constructor parameter 'Path' on type 'System.Text.Yaml.Tests.Docs.ContractSamples+Volume' doesn't allow null values. Consider updating its nullability annotation. Path: $.Path | LineNumber: 1 | BytePositionInLine: 10.
```
<!-- /sample:nullable -->

## Unknown keys

Keys that match no member are skipped. `UnmappedMemberHandling = Disallow`, on the options or as an attribute on the
type, makes them an error, which catches typos in configuration files:

<!-- sample:unmapped-types -->
```csharp
[YamlUnmappedMemberHandling(YamlUnmappedMemberHandling.Disallow)]
public class Probe
{
    public string Path { get; set; } = "/";

    public int Interval { get; set; }
}
```
<!-- /sample:unmapped-types -->

<!-- sample:unmapped -->
```csharp
string typo = Describe(() => YamlSerializer.Deserialize<Probe>("Path: /health\nIntervall: 30\n"));
```

```text
YamlException: The YAML property 'Intervall' could not be mapped to any .NET member contained in type 'System.Text.Yaml.Tests.Docs.ContractSamples+Probe'.
```
<!-- /sample:unmapped -->

## Keep unknown keys

A dictionary marked `[YamlExtensionData]` receives the keys that match no member, and writes them back. Its values can
be `YamlElement`, `object`, or the whole member can be a `YamlObject`. The source's styles are kept, the flow
mapping included:

<!-- sample:overflow-types -->
```csharp
public class Manifest
{
    public string Kind { get; set; } = "";

    [YamlExtensionData]
    public Dictionary<string, YamlElement> Extra { get; set; } = new();
}
```
<!-- /sample:overflow-types -->

<!-- sample:overflow -->
```csharp
var manifest = YamlSerializer.Deserialize<Manifest>("""
    Kind: Service
    metadata:
      name: web
    spec: {ports: [80]}
    """)!;
string keys = string.Join(", ", manifest.Extra.Keys);
manifest.Kind = "Deployment";
string yaml = YamlSerializer.Serialize(manifest);
```

```yaml
# extra: metadata, spec
Kind: Deployment
metadata:
  name: web
spec: {ports: [80]}
```
<!-- /sample:overflow -->

## Immutable types and constructors

A type without a parameterless constructor is created through its public constructor; `[YamlConstructor]` chooses one
when there are several. Each parameter binds to the property of the same name, ignoring case, and is read from that
property's key, so keys match as for any member (case-sensitively unless `PropertyNameCaseInsensitive` is set). A
parameter missing from the YAML takes its default value. Records, `init` properties and read-only properties work
this way:

<!-- sample:immutable-types -->
```csharp
public class Point
{
    [YamlConstructor]
    public Point(double x, double y) => (X, Y) = (x, y);

    public Point() : this(0, 0) { }

    public double X { get; }

    public double Y { get; }
}

public record Size(double Width, double Height = 1);
```
<!-- /sample:immutable-types -->

<!-- sample:immutable -->
```csharp
Point point = YamlSerializer.Deserialize<Point>("X: 3\nY: 4\n")!;
Size size = YamlSerializer.Deserialize<Size>("Width: 2\n")!;
```

```text
(3, 4), Size { Width = 2, Height = 1 }
```
<!-- /sample:immutable -->

## Populate existing objects

By default, a collection or an object read into a member replaces the member's value, and a member without a setter is
skipped. `[YamlObjectCreationHandling(Populate)]`, or `PreferredObjectCreationHandling` for every member, fills the
existing value instead: a collection keeps its items and gets the new ones, a dictionary merges the keys:

<!-- sample:populate-types -->
```csharp
public class Settings
{
    public List<string> Hosts { get; } = ["localhost"];

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    public Dictionary<string, int> Limits { get; set; } = new() { ["cpu"] = 1 };
}
```
<!-- /sample:populate-types -->

<!-- sample:populate -->
```csharp
string yaml = "Hosts: [a.example]\nLimits: {memory: 512}\n";
Settings replaced = YamlSerializer.Deserialize<Settings>(yaml)!;
Settings populated = YamlSerializer.Deserialize<Settings>(yaml, new YamlSerializerOptions
{
    PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate,
})!;
```

```text
default: [localhost] {cpu: 1, memory: 512}
populate: [localhost, a.example] {cpu: 1, memory: 512}
```
<!-- /sample:populate -->
