---
title: References, anchors and merge keys
description: Write shared objects and cycles as anchors and aliases, read aliases safely, and merge mappings with <<.
---

YAML has its own syntax for references: an anchor (`&name`) names a node, and an alias (`*name`) refers to it. Styml
uses it where System.Text.Json writes `$id` and `$ref`.

## Write shared objects as anchors

`ReferenceHandler.Preserve` writes an anchor on each object or collection that is referenced more than once, and an
alias for its other occurrences. Reading the document back restores the shared instances:

<!-- sample:refs-types -->
```csharp
public class Person
{
    public string Name { get; set; } = "";

    public Address? Home { get; set; }

    public Person? Manager { get; set; }
}

public class Address
{
    public string City { get; set; } = "";
}
```
<!-- /sample:refs-types -->

<!-- sample:refs-preserve -->
```csharp
var paris = new Address { City = "Paris" };
var boss = new Person { Name = "Grace", Home = paris };
var team = new List<Person> { boss, new() { Name = "Ada", Home = paris, Manager = boss } };
var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
string yaml = YamlSerializer.Serialize(team, options);
List<Person> back = YamlSerializer.Deserialize<List<Person>>(yaml)!;
bool shared = ReferenceEquals(back[1].Manager, back[0]) && ReferenceEquals(back[1].Home, back[0].Home);
```

```yaml
- &1
  Name: Grace
  Home: &2
    City: Paris
  Manager: null
- Name: Ada
  Home: *2
  Manager: *1

# shared after reading: True
```
<!-- /sample:refs-preserve -->

`ReferenceHandler.PreserveAll`, a Styml addition, writes an anchor on every object and collection, referenced again or
not, as `Preserve` does in System.Text.Json.

## Cycles

A cycle is an error by default. `IgnoreCycles` writes `null` where a cycle would start, and `Preserve` writes it as an
alias:

<!-- sample:refs-cycles -->
```csharp
var ada = new Person { Name = "Ada" };
ada.Manager = ada;
string error = Describe(() => YamlSerializer.Serialize(ada));
string ignored = YamlSerializer.Serialize(ada, new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles });
string preserved = YamlSerializer.Serialize(ada, new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve });
```

```text
# YamlException: A possible object cycle was detected.
Name: Ada
Home: null
Manager: null
---
&1
Name: Ada
Home: null
Manager: *1
```
<!-- /sample:refs-cycles -->

## Read aliases

Aliases are always read, without any option: an alias to an object or a collection shares the instance read at its
anchor.

<!-- sample:refs-team -->
```csharp
public record Team(Address Defaults, List<Person> People);
```
<!-- /sample:refs-team -->

<!-- sample:refs-aliases -->
```csharp
string yaml = """
    Defaults: &office {City: Lyon}
    People:
    - {Name: Ada, Home: *office}
    - {Name: Alan, Home: *office}
    """;
Team team = YamlSerializer.Deserialize<Team>(yaml)!;
bool same = ReferenceEquals(team.People[0].Home, team.People[1].Home) && ReferenceEquals(team.Defaults, team.People[0].Home);
```

```text
Lyon, Lyon, same instance: True
```
<!-- /sample:refs-aliases -->

An alias read into a scalar, a value type or `object` copies the anchored node.

## Bounded alias expansion

Copying nodes is how a few lines of nested aliases expand into billions of nodes (the "billion laughs" attack).
`MaxAliasExpansion` bounds the nodes that aliases may copy in one deserialization: by default 10,000, or the size of
the input in bytes when that is more.

<!-- sample:refs-bomb -->
```csharp
string bomb = """
    a: &a [lol, lol, lol, lol, lol, lol, lol, lol, lol]
    b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a]
    c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b]
    d: &d [*c, *c, *c, *c, *c, *c, *c, *c, *c]
    e: [*d, *d, *d, *d, *d, *d, *d, *d, *d]
    """;
string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, object>>(bomb));
```

```text
YamlException: Reading YAML aliases would copy more than 10000 nodes (YamlSerializerOptions.MaxAliasExpansion).
```
<!-- /sample:refs-bomb -->

`YamlDocumentOptions.MaxAliasExpansion` bounds the copies made from a `YamlDocument` the same way. See
[Security](security.md) for the other bounds.

## Merge keys

`ResolveMergeKeys` merges the mappings of a `<<` key into the mapping that holds it, the merge type of YAML 1.1 that
Compose files and CI configurations use. The mapping's own keys win, and a sequence of mappings merges in order. It is
off by default, since YAML 1.2 has no merge type, and `<<` is then an ordinary key:

<!-- sample:merge-keys -->
```csharp
string yaml = """
    base: &base
      image: nginx:1.27
      replicas: 2
    services:
      web:
        <<: *base
        replicas: 4
      admin:
        <<: [*base, {port: 8081}]
    """;
var options = new YamlSerializerOptions { ResolveMergeKeys = true };
YamlElement root = YamlSerializer.Deserialize<YamlElement>(yaml, options);
string merged = YamlSerializer.Serialize(root.GetProperty("services"));
```

```yaml
web:
  image: nginx:1.27
  replicas: 4
admin:
  image: nginx:1.27
  replicas: 2
  port: 8081
```
<!-- /sample:merge-keys -->

The [playground](/styml/playground/) shows merge keys in the document tree, with the *Merge keys* option.
