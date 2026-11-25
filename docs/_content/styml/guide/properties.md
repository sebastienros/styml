---
title: Customize properties
description: Names and naming policies, enums as strings, ignored members, fields and non-public members, order, and numbers.
---

The serializer writes the public properties of a type with their .NET names. Attributes and options change which
members are written, under which names, and how, exactly as in System.Text.Json.

## Names and naming policies

`[YamlPropertyName]` names one member. `PropertyNamingPolicy` converts every other name: `CamelCase`, `PascalCase`,
`SnakeCaseLower`, `SnakeCaseUpper`, `KebabCaseLower` and `KebabCaseUpper`, or a `YamlNamingPolicy` of your own.
`DictionaryKeyPolicy` converts dictionary keys, which are written as they are by default.

`YamlStringEnumConverter` writes enums by name instead of by number, and `[YamlStringEnumMemberName]` names one value:

<!-- sample:naming-types -->
```csharp
public class Endpoint
{
    public string HostName { get; set; } = "";

    [YamlPropertyName("tcp-port")]
    public int Port { get; set; }

    public Protocol Protocol { get; set; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();
}

public enum Protocol { Http, [YamlStringEnumMemberName("h2")] Http2 }
```
<!-- /sample:naming-types -->

<!-- sample:naming -->
```csharp
var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower,
    DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseUpper,
    Converters = { new YamlStringEnumConverter() },
};
string yaml = YamlSerializer.Serialize(endpoint, options);
```

```yaml
host-name: api.example
tcp-port: 443
protocol: h2
request-headers:
  X-TRACE: "on"
```
<!-- /sample:naming -->

`"on"` is quoted because YAML 1.1 reads a plain `on` as `true`.

## Ignore members

`[YamlIgnore]` leaves a member out, always or under a condition. `DefaultIgnoreCondition` applies a condition to every
member: `WhenWritingNull`, `WhenWritingDefault`, or Styml's `WhenWritingEmptyCollection`, which also leaves out empty
collections, as YamlDotNet's `OmitNull | OmitEmptyCollections` does:

<!-- sample:ignore-types -->
```csharp
public class Account
{
    public string User { get; set; } = "";

    [YamlIgnore]
    public string Password { get; set; } = "";

    [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
    public int FailedLogins { get; set; }

    public string? Email { get; set; }

    public List<string> Roles { get; set; } = [];
}
```
<!-- /sample:ignore-types -->

<!-- sample:ignore -->
```csharp
var account = new Account { User = "ada", Password = "secret" };
string all = YamlSerializer.Serialize(account);
string compact = YamlSerializer.Serialize(account, new YamlSerializerOptions
{
    DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection,
});
```

```yaml
User: ada
Email: null
Roles: []
---
User: ada
```
<!-- /sample:ignore -->

`IgnoreReadOnlyProperties` and `IgnoreReadOnlyFields` leave out members without a setter when writing.

## Fields, non-public members and order

`IncludeFields` adds public fields, and `[YamlInclude]` adds one member, public or not. `[YamlPropertyOrder]` moves a
member before (negative) or after (positive) the others, which keep their declaration order. `MappingOrder = Sorted`
sorts the members by order, then by name: see [Writing YAML](writing-yaml.md#comments-and-order).

<!-- sample:fields-types -->
```csharp
public class Counter
{
    public int Count;

    [YamlInclude]
    internal string Label { get; set; } = "hits";

    [YamlPropertyOrder(-1)]
    public string Id { get; set; } = "c1";
}
```
<!-- /sample:fields-types -->

<!-- sample:fields -->
```csharp
string yaml = YamlSerializer.Serialize(new Counter { Count = 7 }, new YamlSerializerOptions { IncludeFields = true });
```

```yaml
Id: c1
Label: hits
Count: 7
```
<!-- /sample:fields -->

## Numbers

`NumberHandling` writes numbers as strings (`WriteAsString`), reads them from strings (`AllowReadingFromString`), and
reads and writes `NaN` and the infinities (`AllowNamedFloatingPointLiterals`). `[YamlNumberHandling]` sets it on a
type or a member. YAML's own `.inf`, `-.inf` and `.nan` always read into a floating-point number:

<!-- sample:numbers -->
```csharp
var options = new YamlSerializerOptions
{
    NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowNamedFloatingPointLiterals,
};
string yaml = YamlSerializer.Serialize(new { Port = 8080, Ratio = double.PositiveInfinity }, options);
var back = YamlSerializer.Deserialize<Dictionary<string, double>>("Port: '8080'\nRatio: .inf\n",
    new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString })!;
string read = $"read: {back["Port"]}, {double.IsPositiveInfinity(back["Ratio"])}";
```

```yaml
Port: "8080"
Ratio: Infinity
# read: 8080, True
```
<!-- /sample:numbers -->

## Character encoding

Block YAML is written with every Unicode character as it is: `café <b>` stays `café <b>`. The JSON-compatible style
escapes as System.Text.Json does by default, non-ASCII and HTML-sensitive characters included (`café
<b>`). `Encoder` takes a `JavaScriptEncoder` that changes this, such as
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to keep the characters in JSON; in block style, a string that the
encoder escapes is written double-quoted with its escapes.
