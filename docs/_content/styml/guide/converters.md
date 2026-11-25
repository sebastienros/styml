---
title: Custom converters
description: Read and write a type your own way with YamlConverter<T>, use YAML's tags and styles in a converter, and convert generic types with a factory.
---

A converter reads and writes one type with the reader and the writer, in place of the serializer's contract. It is
System.Text.Json's `JsonConverter<T>`, with a `Utf8YamlReader` and a `Utf8YamlWriter` that also see YAML's tags,
anchors and styles.

## Write a converter

Derive from `YamlConverter<T>` and override `Read` and `Write`. `Read` starts on the value's first token and must
leave the reader on its last one; for a scalar, that is the same token:

<!-- sample:converter-type -->
```csharp
// A temperature written as a scalar such as "21.5C" rather than as a mapping.
public readonly record struct Temperature(double Celsius);

public sealed class TemperatureConverter : YamlConverter<Temperature>
{
    public override Temperature Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
    {
        string text = reader.GetString() ?? throw new YamlException("A temperature can't be null.");
        if (!text.EndsWith("C", StringComparison.Ordinal)
            || !double.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double celsius))
        {
            throw new YamlException($"'{text}' is not a temperature such as 21.5C.");
        }

        return new Temperature(celsius);
    }

    public override void Write(Utf8YamlWriter writer, Temperature value, YamlSerializerOptions options) =>
        writer.WriteStringValue(value.Celsius.ToString(CultureInfo.InvariantCulture) + "C");
}
```
<!-- /sample:converter-type -->

## Register it

Add the converter to `YamlSerializerOptions.Converters`, where it applies to every value of its type:

<!-- sample:converter-use -->
```csharp
var options = new YamlSerializerOptions { Converters = { new TemperatureConverter() } };
var readings = new Dictionary<string, Temperature> { ["kitchen"] = new(21.5), ["attic"] = new(-3) };
string yaml = YamlSerializer.Serialize(readings, options);
var back = YamlSerializer.Deserialize<Dictionary<string, Temperature>>(yaml, options)!;
string error = Describe(() => YamlSerializer.Deserialize<Temperature>("hot", options));
```

```yaml
kitchen: 21.5C
attic: -3C

# YamlException: 'hot' is not a temperature such as 21.5C.
```
<!-- /sample:converter-use -->

`[YamlConverter]` applies a converter to one member, or to a type when it is on the type's declaration. The member's
converter wins over the options', which win over the type's:

<!-- sample:converter-attribute -->
```csharp
public class Room
{
    public string Name { get; set; } = "";

    [YamlConverter(typeof(TemperatureConverter))]
    public Temperature Target { get; set; }
}
```
<!-- /sample:converter-attribute -->

With a [source-generated context](source-generation.md), add converters with `Converters` on
`[YamlSourceGenerationOptions]`, or on the options passed to the context's constructor.

## Tags and styles in a converter

The reader gives a converter the tag of the current value (`Tag`, `GetExpandedTag()`), its anchor and its scalar style.
The writer takes a tag, an anchor, a scalar or collection style and comments before the next value (`WriteTag`,
`WriteAnchor`, `WriteScalarStyle`, `WriteCollectionStyle`, `WriteCommentValue`). A converter can make the YAML read
as a human would write it:

<!-- sample:converter-yaml-type -->
```csharp
// A script written as a literal block scalar with a tag, whatever the writer's defaults.
public record Script(string Shell, string Body);

public sealed class ScriptConverter : YamlConverter<Script>
{
    public override Script Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
    {
        // The tag is the shell: !bash, !pwsh. An untagged script is a shell script.
        string shell = reader.Tag.IsEmpty ? "sh" : reader.GetExpandedTag()!.TrimStart('!');
        return new Script(shell, reader.GetString() ?? "");
    }

    public override void Write(Utf8YamlWriter writer, Script value, YamlSerializerOptions options)
    {
        writer.WriteTag("!" + value.Shell);
        writer.WriteScalarStyle(YamlScalarStyle.Literal);
        writer.WriteStringValue(value.Body);
    }
}
```
<!-- /sample:converter-yaml-type -->

<!-- sample:converter-yaml -->
```csharp
var options = new YamlSerializerOptions { Converters = { new ScriptConverter() } };
var steps = new List<Script> { new("bash", "set -e\ndotnet test\n"), new("pwsh", "Write-Host 'done'\n") };
string yaml = YamlSerializer.Serialize(steps, options);
List<Script> back = YamlSerializer.Deserialize<List<Script>>(yaml, options)!;
```

```yaml
- !bash |
  set -e
  dotnet test
- !pwsh |
  Write-Host 'done'
```
<!-- /sample:converter-yaml -->

A style is a hint: the writer falls back to a style that can hold the value. The JSON-compatible style ignores scalar
styles, but has no form for a tag, an anchor or an alias and throws an `InvalidOperationException` for them: a
converter used with both styles checks `writer.Options.Style` first.

## Generic types: converter factories

A `YamlConverterFactory` creates converters for a family of types, such as every closed `Range<T>`:

<!-- sample:converter-factory -->
```csharp
// One converter for every Range<T>: "1..5".
public readonly record struct Range<T>(T Start, T End);

public sealed class RangeConverterFactory : YamlConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Range<>);

    public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options) =>
        (YamlConverter)Activator.CreateInstance(typeof(RangeConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;

    private sealed class RangeConverter<T> : YamlConverter<Range<T>> where T : IParsable<T>
    {
        public override Range<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            string[] parts = reader.GetString()!.Split("..");
            return new Range<T>(T.Parse(parts[0], CultureInfo.InvariantCulture), T.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        public override void Write(Utf8YamlWriter writer, Range<T> value, YamlSerializerOptions options) =>
            writer.WriteStringValue(FormattableString.Invariant($"{value.Start}..{value.End}"));
    }
}
```
<!-- /sample:converter-factory -->

<!-- sample:converter-factory-use -->
```csharp
var options = new YamlSerializerOptions { Converters = { new RangeConverterFactory() } };
var limits = new { Ports = new Range<int>(8000, 8099), Ratio = new Range<double>(0.5, 1.5) };
string yaml = YamlSerializer.Serialize(limits, options);
```

```yaml
Ports: 8000..8099
Ratio: 0.5..1.5
```
<!-- /sample:converter-factory-use -->

A factory that calls `Activator.CreateInstance` or `MakeGenericType` is not trim-safe: with source generation, declare
the closed converters instead.

## Before writing a converter

Most customizations don't need one. Names, ignored members, enums as strings, numbers in strings, constructors and
polymorphism have [attributes and options](properties.md), and the [contract model](contracts.md) changes a type's
members without taking over its reading and writing. A converter is for a type whose YAML shape differs from its .NET
shape, as a scalar standing for an object.
