---
title: Schemas and typed reads
description: How plain scalars resolve under the core, YAML 1.1, JSON and failsafe schemas, how typed members read them, and lenient reads.
---

In YAML, `12`, `true` and `null` are plain scalars: text that a schema resolves to a number, a boolean or null. Styml
resolves them with the YAML 1.2 core schema by default, and a typed member decides what it accepts.

## Schemas

`Schema` decides what a plain scalar is when nothing else does: when it is read into `object`, a `YamlElement`, a
`YamlNode`, or by the reader's token types.

| Schema | Resolves |
| --- | --- |
| `Core` (default) | YAML 1.2: `null`, `~` and empty as null; `true`, `false`; decimal, `0o` octal, `0x` hexadecimal, floats, `.inf`, `.nan` |
| `Yaml11` | The core schema and YAML 1.1's booleans (`yes`, `no`, `on`, `off`), octal with a leading zero (`012`), binary (`0b101`), sexagesimal (`1:30`) and `_` separators |
| `Json` | JSON's values only (`null`, `true`, `false`, numbers); any other plain scalar is an error, so quote strings |
| `Failsafe` | Nothing: every scalar is a string |

<!-- sample:schemas -->
```csharp
string yaml = "a: 12\nb: 0o14\nc: 012\nd: yes\ne: ~\nf: 1_000\ng: .inf\nh: 2026-11-24\n";
string Read(YamlSchemaKind schema) => Describe(() =>
{
    var values = YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml, new YamlSerializerOptions { Schema = schema })!;
    return string.Join(", ", values.Select(p => $"{p.Key}={Kind(p.Value)}"));
});
static string Kind(object? value) => value is YamlElement e ? e.ValueKind.ToString() : value?.GetType().Name ?? "null";

string output = string.Join("\n", new[] { YamlSchemaKind.Core, YamlSchemaKind.Yaml11, YamlSchemaKind.Json, YamlSchemaKind.Failsafe }
    .Select(schema => $"{schema}: {Read(schema)}"));
```

```text
Core: a=Number, b=Number, c=Number, d=String, e=null, f=String, g=Number, h=String
Yaml11: a=Number, b=Number, c=Number, d=True, e=null, f=Number, g=Number, h=String
Json: YamlException: The plain scalar '0o14' is not null, a boolean or a number of the JSON schema (YamlSchemaKind.Json); quote it to read a string. Path: $.b | LineNumber: 1 | BytePositionInLine: 7.
Failsafe: a=String, b=String, c=String, d=String, e=String, f=String, g=String, h=String
```
<!-- /sample:schemas -->

`Yaml11` reads the documents of tools that still follow YAML 1.1, for typed members too:

<!-- sample:schemas-yaml11 -->
```csharp
var options = new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 };
var flags = YamlSerializer.Deserialize<Dictionary<string, bool>>("a: yes\nb: Off\nc: on\n", options)!;
var numbers = YamlSerializer.Deserialize<Dictionary<string, int>>("octal: 012\nbinary: 0b101\nsexagesimal: 1:30\n", options)!;
string output = string.Join(", ", flags.Select(p => $"{p.Key}={p.Value}")) + "\n"
    + string.Join(", ", numbers.Select(p => $"{p.Key}={p.Value}"));
```

```text
a=True, b=False, c=True
octal=10, binary=5, sexagesimal=90
```
<!-- /sample:schemas-yaml11 -->

## Typed reads

A typed member reads its own type, whatever the schema says: a plain `0x10` reads into an `int`, a plain `512` into a
`string`. A quoted scalar is a string: `'4'` doesn't read into an `int`.

`AcceptLenientScalars` accepts the forms that YamlDotNet or SharpYaml resolve although no standard does: quoted numbers
and booleans, booleans and `null` in any case, padded dates. It helps with documents written for those libraries:

<!-- sample:typed-types -->
```csharp
public record Limits(int Cpu, string Memory, bool Burst);
```
<!-- /sample:typed-types -->

<!-- sample:typed -->
```csharp
Limits plain = YamlSerializer.Deserialize<Limits>("Cpu: 0x10\nMemory: 512\nBurst: true\n")!;
string quoted = Describe(() => YamlSerializer.Deserialize<Limits>("Cpu: '4'\nMemory: 512\nBurst: 'TRUE'\n"));
Limits lenient = YamlSerializer.Deserialize<Limits>("Cpu: '4'\nMemory: 512\nBurst: 'TRUE'\n",
    new YamlSerializerOptions { AcceptLenientScalars = true })!;
```

```text
plain: Limits { Cpu = 16, Memory = 512, Burst = True }
quoted: YamlException: The YAML value could not be converted to System.Int32. Path: $.Cpu | LineNumber: 0 | BytePositionInLine: 8.
lenient: Limits { Cpu = 4, Memory = 512, Burst = True }
```
<!-- /sample:typed -->

`NumberHandling.AllowReadingFromString` also reads quoted numbers, as in System.Text.Json: see
[Customize properties](properties.md#numbers).

## Writing

The writer quotes every string that would read back as another type under the core schema or YAML 1.1, so what it
writes reads the same under both: see [Scalar styles and quoting](scalar-styles.md#what-gets-quoted).
