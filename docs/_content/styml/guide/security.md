---
title: Security
description: Reading YAML from untrusted sources with Styml - what is bounded by default, what a document can't do, and the options to review.
---

YAML has features that have made other libraries unsafe for untrusted input: tags that name types, aliases that
expand without bound, and deep nesting. Styml's defaults are safe for documents from users and the network.

## A document never chooses a type

Some YAML libraries create the .NET type a tag names (`!System.Diagnostics.Process`), so a document can instantiate
classes the application never meant to deserialize. In Styml:

- a tag selects a type only through an explicit allow-list: the derived types of a
  [polymorphic hierarchy](polymorphism.md), or [`TagMappings`](polymorphism.md#tag-mappings-an-allow-list-of-types);
- an unknown tag is data: it reads into `object` as a `YamlElement`, and no type is loaded by name;
- the `$type` discriminator, as in System.Text.Json, only selects among the derived types the hierarchy declares.

## Bounded alias expansion

An alias read into a scalar, a value type or `object` copies its node, which is how a few lines of nested aliases can
expand into billions of nodes. `MaxAliasExpansion` bounds the copies of one deserialization, by default to 10,000
nodes or the input's size in bytes, whichever is larger, and `YamlDocumentOptions.MaxAliasExpansion` bounds the
copies made from a document: see [References](references.md#bounded-alias-expansion).

## Bounded depth

Nesting is limited to 64 levels (`MaxDepth`), on the reader, the document and the serializer, so a document can't
exhaust the stack: see [Reading YAML](reading-yaml.md#depth).

## Strict by default

- Duplicate keys are an error (`DuplicateKeyHandling`), as YAML 1.2 requires, so two readers of one document can't
  see different values for a key.
- Merge keys are off (`ResolveMergeKeys`), so `<<` can't inject keys unless the application asks for merging.
- Unknown keys are skipped. `UnmappedMemberHandling = Disallow` refuses them, which catches typos in configuration:
  see [Deserialization scenarios](deserialization.md#unknown-keys).

## Options to review

| Option | Default | Review when |
| --- | --- | --- |
| `MaxDepth` | 64 | Raising it for deeply nested documents |
| `MaxAliasExpansion` | 0 (10,000 nodes or the input's size) | Reading documents with many aliases |
| `DuplicateKeyHandling` | `Error` | Accepting documents written by tools that don't check |
| `ResolveMergeKeys` | `false` | Reading Compose files or CI configurations |
| `TagMappings`, `[YamlDerivedType]` | none | Every type you add can be created by a document |
| `UnmappedMemberHandling` | `Skip` | Validating configuration |

## Conformance and testing

Styml is tested with the 402 cases of the [yaml-test-suite](https://github.com/yaml/yaml-test-suite) and with
System.Text.Json's own test suite, renamed. Please report a vulnerability privately to the maintainer rather than in a
public issue.
