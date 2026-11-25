---
title: Polymorphism and tags
description: Serialize derived types with a $type member or a YAML tag, configure hierarchies you don't own, and map tags to types with an explicit allow-list.
---

A value declared as a base type can hold a derived type. As in System.Text.Json, the serializer writes and reads the
derived type only for the types the hierarchy lists: a document never chooses a .NET type that the code didn't allow.
YAML adds a second way to name the type, the tag.

## Derived types

`[YamlDerivedType]` on the base type lists each derived type with its discriminator, and the serializer writes it as a
`$type` member, the way System.Text.Json does:

<!-- sample:poly-types -->
```csharp
[YamlDerivedType(typeof(Email), "email")]
[YamlDerivedType(typeof(Sms), "sms")]
public abstract class Notification
{
    public string To { get; set; } = "";
}

public class Email : Notification
{
    public string Subject { get; set; } = "";
}

public class Sms : Notification
{
    public bool Flash { get; set; }
}
```
<!-- /sample:poly-types -->

<!-- sample:poly-property -->
```csharp
Notification[] notifications =
[
    new Email { To = "ada@example.org", Subject = "Build passed" },
    new Sms { To = "+15550100", Flash = true },
];
string yaml = YamlSerializer.Serialize(notifications);
Notification[] back = YamlSerializer.Deserialize<Notification[]>(yaml)!;
string types = string.Join(", ", back.Select(n => n.GetType().Name));
```

```yaml
- $type: email
  Subject: Build passed
  To: ada@example.org
- $type: sms
  Flash: true
  To: "+15550100"

# Email, Sms
```
<!-- /sample:poly-property -->

The discriminator is a string or an integer. On reading, `$type` must be the first key of the mapping unless
`AllowOutOfOrderMetadataProperties` is set.

## Tags as discriminators

`[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]` writes the discriminator as a YAML tag
instead: `!` followed by the discriminator, unless `Tag` sets another. `Both` writes the two. Under every style,
reading accepts a `$type` member, and the JSON-compatible style, which has no tags, writes `$type`:

<!-- sample:poly-tag-types -->
```csharp
[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
[YamlDerivedType(typeof(Http), "http")]
[YamlDerivedType(typeof(Exec), "exec", Tag = "!command")]
public abstract record Check;

public record Http(string Path, int Port) : Check;

public record Exec(string[] Command) : Check;
```
<!-- /sample:poly-tag-types -->

<!-- sample:poly-tag -->
```csharp
var checks = new Dictionary<string, Check>
{
    ["liveness"] = new Http("/health", 8080),
    ["readiness"] = new Exec(["cat", "/tmp/ready"]),
};
string yaml = YamlSerializer.Serialize(checks);
var back = YamlSerializer.Deserialize<Dictionary<string, Check>>(yaml)!;
```

```yaml
liveness: !http
  Path: /health
  Port: 8080
readiness: !command
  Command:
  - cat
  - /tmp/ready
```
<!-- /sample:poly-tag -->

## Discriminator name and unknown types

`TypeDiscriminatorPropertyName` renames `$type`. `UnknownDerivedTypeHandling` decides what happens to a derived type the
hierarchy doesn't list: an error by default, or `FallBackToBaseType` and `FallBackToNearestAncestor`, which write it
as the listed type, without its own members:

<!-- sample:poly-name-types -->
```csharp
[YamlPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBaseType)]
[YamlDerivedType(typeof(Circle), "circle")]
public class Shape
{
    public string Color { get; set; } = "black";
}

public class Circle : Shape
{
    public double Radius { get; set; }
}

public class Square : Shape
{
    public double Side { get; set; }
}
```
<!-- /sample:poly-name-types -->

<!-- sample:poly-name -->
```csharp
Shape[] shapes = [new Circle { Radius = 2 }, new Square { Side = 3, Color = "red" }];
string yaml = YamlSerializer.Serialize(shapes);
```

```yaml
- kind: circle
  Radius: 2
  Color: black
- Color: red
```
<!-- /sample:poly-name -->

`IgnoreUnrecognizedTypeDiscriminators` reads a discriminator the hierarchy doesn't know as the base type rather than
failing.

`InferClosedTypePolymorphism` (on the options, `[YamlPolymorphic]` or `[YamlSourceGenerationOptions]`), new in .NET 11's
System.Text.Json, treats a type that the compiler marks as a closed hierarchy as polymorphic without a
`[YamlDerivedType]` list: each derived type is registered with its name as the discriminator.

## Hierarchies you don't own

The [contract model](contracts.md) configures polymorphism without attributes, for types of another library:
`YamlTypeInfo.PolymorphismOptions` takes the derived types, the discriminator style and the other settings of the
attributes:

<!-- sample:poly-contract-types -->
```csharp
// Types of a library you don't own: no attributes on them.
public class Vehicle { public int Wheels { get; set; } }

public class Bike : Vehicle { public bool Electric { get; set; } }

public class Truck : Vehicle { public double Load { get; set; } }
```
<!-- /sample:poly-contract-types -->

<!-- sample:poly-contract -->
```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = new DefaultYamlTypeInfoResolver
    {
        Modifiers =
        {
            typeInfo =>
            {
                if (typeInfo.Type == typeof(Vehicle))
                {
                    typeInfo.PolymorphismOptions = new YamlPolymorphismOptions
                    {
                        DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                        DerivedTypes =
                        {
                            new YamlDerivedType(typeof(Bike), "bike"),
                            new YamlDerivedType(typeof(Truck), "truck"),
                        },
                    };
                }
            },
        },
    },
};
string yaml = YamlSerializer.Serialize<Vehicle[]>([new Bike { Wheels = 2, Electric = true }, new Truck { Wheels = 6, Load = 7.5 }], options);
```

```yaml
- !bike
  Electric: true
  Wheels: 2
- !truck
  Load: 7.5
  Wheels: 6
```
<!-- /sample:poly-contract -->

## Tag mappings: an allow-list of types

Outside a hierarchy, `TagMappings` maps a tag to a type. A value read into `object` (or a collection of `object`)
with a mapped tag is read as that type, and a value of that type is written with its tag. Tags are matched after
`%TAG` handles are expanded, so `!<tag:example.org,2026:color>` and a `!e!color` handle declared for the same prefix
are the same tag:

<!-- sample:tag-mappings-types -->
```csharp
public record Coordinate(double Lat, double Lon);

public record Color(string Hex);
```
<!-- /sample:tag-mappings-types -->

<!-- sample:tag-mappings -->
```csharp
var options = new YamlSerializerOptions();
options.TagMappings.Add("!geo", typeof(Coordinate));
options.TagMappings.Add("tag:example.org,2026:color", typeof(Color));

var values = YamlSerializer.Deserialize<List<object>>("""
    - !geo {Lat: 48.85, Lon: 2.35}
    - !<tag:example.org,2026:color> {Hex: '#7c3aed'}
    - !System.IO.FileInfo {FileName: /etc/passwd}
    - plain text
    """, options)!;
string types = string.Join(", ", values.Select(v => v.GetType().Name));
string yaml = YamlSerializer.Serialize<object[]>([new Coordinate(51.5, -0.12), new Color("#2563eb")], options);
```

```yaml
# Coordinate, Color, YamlElement, YamlElement
- !geo
  Lat: 51.5
  Lon: -0.12
- !<tag:example.org,2026:color>
  Hex: "#2563eb"
```
<!-- /sample:tag-mappings -->

> [!IMPORTANT]
> **A tag never names a .NET type.** Some YAML libraries create the type a tag names, such as
> `!System.IO.FileInfo`, which lets a document instantiate classes the application never meant to deserialize. In
> Styml, a tag selects a type only when the code mapped it: an unmapped tag is data (a `YamlElement` in `object`), and
> no type is ever loaded by name.

`[YamlTagMapping]` declares the same mappings on a source-generated context:

<!-- sample:tag-context -->
```csharp
[YamlSerializable(typeof(List<object>))]
[YamlSerializable(typeof(Coordinate))]
[YamlTagMapping("!geo", typeof(Coordinate))]
public partial class GeoContext : YamlSerializerContext;
```
<!-- /sample:tag-context -->

The tags of the source are also visible without mapping them: `YamlElement.GetTag()` and `GetExpandedTag()`, the
reader's `Tag` property, and the `YamlNode` of a tagged value. See [the DOM](dom.md) and
[the reader and the writer](reader-writer.md).
