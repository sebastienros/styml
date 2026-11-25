---
title: Serialize and deserialize
description: The methods of YamlSerializer, its options, the supported types, and the errors it reports.
---

`YamlSerializer` has the methods of `JsonSerializer`. They take an optional `YamlSerializerOptions`, or a
`YamlTypeInfo` from a [source-generated context](source-generation.md) for trimmed and Native AOT applications.

## Strings, bytes and documents

`Serialize` returns a string, `SerializeToUtf8Bytes` the UTF-8 bytes, which are what Styml reads and writes
internally. `SerializeToDocument`, `SerializeToElement` and `SerializeToNode` return the [DOM](dom.md) of the value,
and `Deserialize` has overloads for each of these inputs:

<!-- sample:utf8 -->
```csharp
byte[] utf8 = YamlSerializer.SerializeToUtf8Bytes(service);
Service back = YamlSerializer.Deserialize<Service>(utf8)!;

YamlDocument document = YamlSerializer.SerializeToDocument(service);
YamlElement element = YamlSerializer.SerializeToElement(service);
YamlNode node = YamlSerializer.SerializeToNode(service)!;
Service fromNode = node.Deserialize<Service>()!;
```

```text
web, nginx:1.27, 80, front
```
<!-- /sample:utf8 -->

## Streams and async

`SerializeAsync` and `DeserializeAsync` write to and read from a `Stream` without buffering the whole document, and
`PipeReader` and `PipeWriter` overloads work with System.IO.Pipelines:

<!-- sample:streams -->
```csharp
using (FileStream file = File.Create(path))
{
    await YamlSerializer.SerializeAsync(file, service);
}

using FileStream read = File.OpenRead(path);
Service? back = await YamlSerializer.DeserializeAsync<Service>(read);
```

```text
nginx:1.27
```
<!-- /sample:streams -->

A stream of several YAML documents is read one document at a time by `DeserializeAsyncEnumerable` with
`topLevelValues: true`: see [Reading YAML](reading-yaml.md#several-documents-in-one-stream).

## Options

`YamlSerializerOptions` has the options of `JsonSerializerOptions` (naming policies, ignore conditions, number
handling, converters, reference handling...) and the YAML ones, listed in the [options reference](options.md).

The options cache the contracts of the types they serialize: create them once and reuse them. Options are frozen at
their first use, and changing them afterwards throws an `InvalidOperationException`.

<!-- sample:reuse-options -->
```csharp
// Create the options once and reuse them: they cache the contracts of the types they serialize.
private static readonly YamlSerializerOptions Options = new()
{
    PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower,
    IndentSequences = true,
};
```
<!-- /sample:reuse-options -->

`YamlSerializerDefaults.Web` gives the defaults of ASP.NET Core, as in System.Text.Json: camel case names,
case-insensitive reads, and numbers read from strings:

<!-- sample:web-defaults -->
```csharp
var options = new YamlSerializerOptions(YamlSerializerDefaults.Web);
var line = $"{options.PropertyNamingPolicy == YamlNamingPolicy.CamelCase}, "
    + $"{options.PropertyNameCaseInsensitive}, {options.NumberHandling}";
```

```text
True, True, AllowReadingFromString
```
<!-- /sample:web-defaults -->

## Supported types

The serializer supports the types System.Text.Json supports: primitives, strings, enums, dates and times, `Guid`,
`Uri`, `Version`, nullable value types, arrays and every collection and dictionary of the base class library,
records, tuples (with `IncludeFields`, since tuples are fields), immutable collections, `IAsyncEnumerable<T>`, and
the DOM types. A `byte[]` is written in base64:

<!-- sample:supported-types -->
```csharp
var value = new
{
    Date = new DateTimeOffset(2026, 11, 24, 9, 30, 0, TimeSpan.FromHours(1)),
    Time = new TimeSpan(1, 30, 0),
    Id = new Guid("8f2e5a3c-7b1d-4c6e-9a0f-1d2e3f4a5b6c"),
    Uri = new Uri("https://example.org"),
    Bytes = new byte[] { 1, 2, 3 },
    Nullable = (int?)null,
    Tuple = (Count: 3, Name: "three"),
    Set = new HashSet<string> { "a", "b" },
};
string yaml = YamlSerializer.Serialize(value, new YamlSerializerOptions { IncludeFields = true });
```

```yaml
Date: 2026-11-24T09:30:00+01:00
Time: "01:30:00"
Id: 8f2e5a3c-7b1d-4c6e-9a0f-1d2e3f4a5b6c
Uri: https://example.org
Bytes: "AQID"
Nullable: null
Tuple:
  Item1: 3
  Item2: three
Set:
- a
- b
```
<!-- /sample:supported-types -->

A date is written plain, in its ISO 8601 form.

## Errors

Invalid YAML, and YAML that does not fit the type, throw a `YamlException` with the path of the value in the document
(`Path`) and its position (`LineNumber` and `BytePositionInLine`, both from 0):

<!-- sample:errors -->
```csharp
string yaml = """
    Name: web
    Ports: [80, http]
    """;
try
{
    YamlSerializer.Deserialize<Service>(yaml);
}
catch (YamlException e)
{
    Console.WriteLine($"{e.Path}, line {e.LineNumber + 1}: {e.Message}");
}
```

```text
$.Ports[1], line 2: The YAML value could not be converted to System.Int32. Path: $.Ports[1] | LineNumber: 1 | BytePositionInLine: 16.
```
<!-- /sample:errors -->

The serializer also throws `NotSupportedException` for a type it can't serialize, and `InvalidOperationException` for
invalid options or contracts.
