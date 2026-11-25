---
title: Source generation and Native AOT
description: Generate the contracts of your types at build time with YamlSerializerContext, for trimmed and Native AOT applications.
---

The serializer reads the shape of a type, its contract, either with reflection at run time or from code generated at
build time. Generated contracts start faster, allocate less, and are the only option in trimmed and Native AOT
applications. The source generator is in the `Styml` package: nothing else to install.

## Declare a context

A context is a partial class that derives from `YamlSerializerContext`, with a `[YamlSerializable]` attribute for each
root type. `[YamlSourceGenerationOptions]` sets its options at build time, the YAML ones included (`WriteStyle`,
`IndentSequences`, `MappingOrder`, `Schema`, `DuplicateKeyHandling` and the others of the
[options reference](options.md#yamlsourcegenerationoptionsattribute)):

<!-- sample:sourcegen-context -->
```csharp
public record Deployment(string Name, int Replicas, List<string> Hosts, DeploymentKind Kind);

public enum DeploymentKind { Rolling, BlueGreen }

[YamlSerializable(typeof(Deployment))]
[YamlSourceGenerationOptions(
    PropertyNamingPolicy = YamlKnownNamingPolicy.SnakeCaseLower,
    UseStringEnumConverter = true,
    IndentSequences = true,
    MappingOrder = YamlMappingOrder.Sorted)]
public partial class DeployContext : YamlSerializerContext;
```
<!-- /sample:sourcegen-context -->

The generator writes a property for each type, `DeployContext.Default.Deployment`, that the serializer's overloads
take in place of options:

<!-- sample:sourcegen-use -->
```csharp
var deployment = new Deployment("web", 3, ["a.example", "b.example"], DeploymentKind.BlueGreen);
string yaml = YamlSerializer.Serialize(deployment, DeployContext.Default.Deployment);
Deployment back = YamlSerializer.Deserialize(yaml, DeployContext.Default.Deployment)!;
```

```yaml
hosts:
  - a.example
  - b.example
kind: BlueGreen
name: web
replicas: 3
```
<!-- /sample:sourcegen-use -->

## Combine with options

A context is also an `IYamlTypeInfoResolver`, so `YamlSerializerOptions.TypeInfoResolver` can use it with options
set at run time. The contracts come from the context, and the options from the options instance, not from the
context's attribute:

<!-- sample:sourcegen-options -->
```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = DeployContext.Default,
    WriteStyle = YamlWriterStyle.JsonCompatible,
};
string json = YamlSerializer.Serialize(deployment, options);
```

```yaml
{"Name":"web","Replicas":3,"Hosts":["a.example"],"Kind":"Rolling"}
```
<!-- /sample:sourcegen-options -->

`TypeInfoResolverChain` asks several resolvers in turn, for example a context and then reflection for the types the
context doesn't have:

<!-- sample:sourcegen-chain -->
```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolverChain = { DeployContext.Default, new DefaultYamlTypeInfoResolver() },
};
string kinds = string.Join(", ", new[] { typeof(Deployment), typeof(Uri) }
    .Select(type => $"{type.Name}: {options.GetTypeInfo(type).Kind}"));
```

```text
Deployment: Object, Uri: None
```
<!-- /sample:sourcegen-chain -->

## Turn reflection off

In a trimmed or Native AOT application, set `YamlSerializerIsReflectionEnabledByDefault` to `false` in the project
file, as `JsonSerializerIsReflectionEnabledByDefault` for System.Text.Json. The overloads without a `YamlTypeInfo`
then throw instead of falling back to reflection, and the trimmer removes the reflection-based serializer:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <YamlSerializerIsReflectionEnabledByDefault>false</YamlSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

On .NET 10 and .NET 11 the library is trimmable and AOT-compatible, with the analyzers on: a call that needs
reflection is flagged at build time. The repository publishes `samples/AotSmoke` trimmed and as Native AOT, with
warnings as errors, before every release, and the [playground](/styml/playground/) is a trimmed WebAssembly
application that uses a source-generated context.

## Generation modes

`GenerationMode` on `[YamlSourceGenerationOptions]` or `[YamlSerializable]` chooses what is generated, as in
System.Text.Json: `Metadata` (the contracts, which every feature can use) or `Serialization` (fast-path serialization
code for the writing side only, which supports fewer features). The default generates both where possible.
