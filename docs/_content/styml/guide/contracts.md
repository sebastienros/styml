---
title: Custom contracts
description: Change how types are serialized at run time with the contract model and its modifiers, YAML comments and scalar styles included.
---

The serializer works from a contract for each type, a `YamlTypeInfo`: its kind, its constructor, and for an object its
properties (`YamlPropertyInfo`), each with a name, a getter, a setter and its settings. Attributes and options build
the contracts; the contract model lets code change them, for types you don't own or rules attributes can't express.

## Modifiers

`DefaultYamlTypeInfoResolver` builds the contracts with reflection, and runs its `Modifiers` on each one before it is
used. A modifier is an `Action<YamlTypeInfo>` that can remove, add or change properties.

Besides System.Text.Json's settings, a `YamlPropertyInfo` has YAML's: the `Comment` written above the member and its
`ScalarStyle`, which `[YamlComment]` and `[YamlScalarStyle]` set from attributes:

<!-- sample:contract-types -->
```csharp
public class Database
{
    public string Host { get; set; } = "";

    public int Port { get; set; }

    public string ConnectionPassword { get; set; } = "";

    public string Notes { get; set; } = "";

    public List<string> Replicas { get; set; } = [];
}
```
<!-- /sample:contract-types -->

<!-- sample:contract-modifiers -->
```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = new DefaultYamlTypeInfoResolver
    {
        Modifiers = { HideSecrets, Document, SkipEmptyLists },
    },
};

var database = new Database { Host = "db.internal", Port = 5432, ConnectionPassword = "hunter2", Notes = "Primary.\nBacked up nightly.\n" };
string yaml = YamlSerializer.Serialize(database, options);

// Leaves out every property whose name contains "Password".
static void HideSecrets(YamlTypeInfo typeInfo)
{
    foreach (YamlPropertyInfo property in typeInfo.Properties.Where(p => p.Name.Contains("Password")).ToList())
    {
        typeInfo.Properties.Remove(property);
    }
}

// Writes a comment above the port and the notes as a literal block: YAML's own contract metadata.
static void Document(YamlTypeInfo typeInfo)
{
    if (typeInfo.Type != typeof(Database))
    {
        return;
    }

    foreach (YamlPropertyInfo property in typeInfo.Properties)
    {
        if (property.Name == nameof(Database.Port))
        {
            property.Comment = "The PostgreSQL port.";
        }
        else if (property.Name == nameof(Database.Notes))
        {
            property.ScalarStyle = YamlScalarStyle.Literal;
        }
    }
}

// Writes a list only when it has items.
static void SkipEmptyLists(YamlTypeInfo typeInfo)
{
    foreach (YamlPropertyInfo property in typeInfo.Properties.Where(p => p.PropertyType == typeof(List<string>)))
    {
        property.ShouldSerialize = (_, value) => value is List<string> { Count: > 0 };
    }
}
```

```yaml
Host: db.internal
# The PostgreSQL port.
Port: 5432
Notes: |
  Primary.
  Backed up nightly.
```
<!-- /sample:contract-modifiers -->

## Add members

`CreateYamlPropertyInfo` creates a property with a getter and a setter of your own: here, the private fields of a type:

<!-- sample:contract-private-type -->
```csharp
public class Session
{
    private string _user;
    private int _attempts;

    public Session() : this("", 0) { }

    public Session(string user, int attempts) => (_user, _attempts) = (user, attempts);

    public override string ToString() => $"{_user}, {_attempts} attempts";
}
```
<!-- /sample:contract-private-type -->

<!-- sample:contract-private -->
```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = new DefaultYamlTypeInfoResolver
    {
        Modifiers =
        {
            typeInfo =>
            {
                if (typeInfo.Type != typeof(Session))
                {
                    return;
                }

                // Adds the private fields of Session as properties, under their names without the underscore.
                foreach (FieldInfo field in typeof(Session).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    YamlPropertyInfo property = typeInfo.CreateYamlPropertyInfo(field.FieldType, field.Name.TrimStart('_'));
                    property.Get = field.GetValue;
                    property.Set = field.SetValue;
                    typeInfo.Properties.Add(property);
                }
            },
        },
    },
};

string yaml = YamlSerializer.Serialize(new Session("ada", 3), options);
Session back = YamlSerializer.Deserialize<Session>("user: bob\nattempts: 1\n", options)!;
```

```yaml
user: ada
attempts: 3

# bob, 1 attempts
```
<!-- /sample:contract-private -->

## What a contract can change

| Setting | On | Effect |
| --- | --- | --- |
| `Properties` | `YamlTypeInfo` | The members, in the order they are written |
| `CreateObject` | `YamlTypeInfo` | The factory of the object, in place of its constructor |
| `PolymorphismOptions` | `YamlTypeInfo` | The derived types and their discriminators: see [Polymorphism](polymorphism.md#hierarchies-you-dont-own) |
| `NumberHandling`, `UnmappedMemberHandling`, `PreferredPropertyObjectCreationHandling` | `YamlTypeInfo` | As the options of the same names, for one type |
| `OnSerializing`, `OnDeserialized` and the others | `YamlTypeInfo` | Callbacks around the serialization of an object |
| `Name`, `Get`, `Set` | `YamlPropertyInfo` | The key, and how the value is read and written |
| `ShouldSerialize` | `YamlPropertyInfo` | Whether a value is written |
| `IsRequired`, `IsExtensionData`, `CustomConverter` | `YamlPropertyInfo` | As `[YamlRequired]`, `[YamlExtensionData]` and `[YamlConverter]` |
| `Comment`, `ScalarStyle` | `YamlPropertyInfo` | The comment above the member and the style of its string value |

The contracts of a [source-generated context](source-generation.md) are changed the same way:
`context.WithAddedModifier(modifier)` returns a resolver that runs the modifier on them.

## Resolvers

`TypeInfoResolver` takes any `IYamlTypeInfoResolver`: a source-generated context, a `DefaultYamlTypeInfoResolver`,
or a class of your own whose `GetTypeInfo` returns the contract of the types it knows and `null` for the others.
`TypeInfoResolverChain` asks several in turn, and `YamlTypeInfoResolver.Combine` combines them into one.

`YamlSerializerOptions.GetTypeInfo(type)` returns the contract the serializer uses, which is how a converter or a tool
reads the members of a type.

## Export a JSON schema

`YamlSchemaExporter` writes the JSON schema of a contract, as System.Text.Json's `JsonSchemaExporter` does, for
editors and validators of YAML files: a YAML language server reads JSON schemas. It follows the contract, names,
nullable annotations and required members included, and returns it as a `YamlNode`:

<!-- sample:schema-export -->
```csharp
var options = new YamlSerializerOptions(YamlSerializerOptions.Default) { RespectNullableAnnotations = true };
YamlNode schema = YamlSchemaExporter.GetYamlSchemaAsNode(options, typeof(Volume));
string yaml = schema.ToYamlString();
```

```yaml
type:
- object
- "null"
properties:
  Name:
    type: string
  Path:
    type: string
  Description:
    type:
    - string
    - "null"
required:
- Name
- Path
- Description
```
<!-- /sample:schema-export -->

The sample exports the `Volume` record of [Deserialization scenarios](deserialization.md#nullable-annotations).
`YamlSchemaExporterOptions.TransformSchemaNode` changes each node of the schema as it is written, to add descriptions
or formats.
