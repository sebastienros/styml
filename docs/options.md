# Options reference

Every System.Text.Json option keeps its meaning in Styml, under the same name with `Yaml` in place of `Json`. This
page lists what Styml adds for YAML, by type, with each default, and the few System.Text.Json options that changed.
The [README's usage section](../README.md#usage) shows each one at work.

<!-- options-reference -->

### `YamlSerializerOptions`

| Member | Default | Effect |
| --- | --- | --- |
| `WriteStyle` | `Block` | `Block` YAML, or `JsonCompatible` JSON. |
| `IndentSequences` | `false` | Indents a sequence under a key (`key:\n  - a`) instead of writing it at the key's column. |
| `ExpandSequenceItems` | `false` | Starts a collection that is a sequence item on the line after its dash. |
| `MaxLineWidth` | `0` (none) | Folds plain and double-quoted scalars at a space past this width, and continues a flow collection on a new line. |
| `MultiLineStringStyle` | `None` (literal) | The style of a string with a line break: `Literal`, `Folded` or `DoubleQuoted`. |
| `DefaultScalarStyle` | `None` | The style of every other string value: `Plain`, `SingleQuoted`, `DoubleQuoted`, `Literal` or `Folded`, where it can hold the text. |
| `QuoteForLenientReaders` | `false` | Also quotes strings that some readers misread although no YAML standard resolves them. |
| `MappingOrder` | `Declaration` | `Sorted` writes members by `[YamlPropertyOrder]`, then by name, ordinal. |
| `IgnoreSourceStyles` | `false` | Writes a `YamlDocument`, a `YamlElement` or a `YamlNode` in the options' styles rather than its source's. |
| `Schema` | `Core` | How plain scalars resolve when read: `Core` (YAML 1.2), `Yaml11`, `Json` or `Failsafe`. |
| `AcceptLenientScalars` | `false` | Typed reads also accept the scalars YamlDotNet or SharpYaml resolve although no standard does (quoted numbers, booleans in any case, padded dates). |
| `DuplicateKeyHandling` | `Error` | What a repeated mapping key does: `Error`, `LastWins` or `FirstWins`. |
| `MaxAliasExpansion` | `0` | The most nodes aliases may copy in one deserialization; 0 allows 10,000 nodes, or as many as the bytes read when that is more. |
| `ResolveMergeKeys` | `false` | Merges a `<<` key's mappings into the mapping that holds it. |
| `DisallowImplicitCollectionKeys` | `false` | Refuses a flow collection as an implicit key, so a stream yields each element as soon as it is complete. |
| `TagMappings` | empty | The tags that select a type when reading, and that the type writes: an explicit allow-list. |

### `YamlWriterOptions`

| Member | Default | Effect |
| --- | --- | --- |
| `Style` | `Block` | `Block` YAML, or `JsonCompatible` JSON. |
| `IndentSequences` | `false` | As on `YamlSerializerOptions`. |
| `ExpandSequenceItems` | `false` | As on `YamlSerializerOptions`. |
| `MaxLineWidth` | `0` | As on `YamlSerializerOptions`. |
| `MultiLineStringStyle` | `None` | As on `YamlSerializerOptions`. |
| `DefaultScalarStyle` | `None` | As on `YamlSerializerOptions`, below a `WriteScalarStyle` hint. |
| `QuoteForLenientReaders` | `false` | As on `YamlSerializerOptions`. |
| `IgnoreSourceStyles` | `false` | As on `YamlSerializerOptions`. |

### `YamlReaderOptions`

| Member | Default | Effect |
| --- | --- | --- |
| `Schema` | `Core` | How plain scalars resolve into token types. |
| `AcceptLenientScalars` | `false` | As on `YamlSerializerOptions`, for the typed `Get` and `TryGet` methods. |
| `DisallowImplicitCollectionKeys` | `false` | As on `YamlSerializerOptions`. |

### `YamlDocumentOptions`

| Member | Default | Effect |
| --- | --- | --- |
| `Schema` | `Core` | How plain scalars resolve in the document. |
| `DuplicateKeyHandling` | `Error` | Under `LastWins` and `FirstWins` the document keeps every entry, and a lookup returns the last or the first match. |
| `MaxAliasExpansion` | `0` | The most nodes copying aliases may add when the document's nodes are copied; 0 allows ten times the document's own node count, or 10,000, whichever is larger. |
| `ResolveMergeKeys` | `false` | As on `YamlSerializerOptions`. |

### `YamlSourceGenerationOptionsAttribute`

The defaults of a source-generated context's options, as on `YamlSerializerOptions`: `WriteStyle`, `IndentSequences`,
`ExpandSequenceItems`, `MaxLineWidth`, `MultiLineStringStyle`, `DefaultScalarStyle`, `QuoteForLenientReaders`,
`MappingOrder`, `IgnoreSourceStyles`, `Schema`, `AcceptLenientScalars`, `DuplicateKeyHandling`, `MaxAliasExpansion`,
`ResolveMergeKeys` and `DisallowImplicitCollectionKeys`. `[YamlTagMapping]` on the context fills `TagMappings`.

### `YamlPolymorphismOptions`

| Member | Default | Effect |
| --- | --- | --- |
| `DiscriminatorStyle` | `Property` | How a hierarchy writes its derived types: `Property` (`$type`), `Tag` or `Both`. |

### `YamlPolymorphicAttribute`

| Member | Default | Effect |
| --- | --- | --- |
| `DiscriminatorStyle` | `Property` | As on `YamlPolymorphismOptions`. |

### `YamlDerivedTypeAttribute`

| Member | Default | Effect |
| --- | --- | --- |
| `Tag` | `!` and the discriminator | The tag the derived type writes and is read from under `Tag` and `Both`. |

### `YamlTagMappingAttribute`

| Member | Effect |
| --- | --- |
| `Tag` | The tag, local (`!dog`) or a global URI. |
| `Type` | The type it reads into and that writes it. |

### `YamlScalarStyleAttribute`

| Member | Effect |
| --- | --- |
| `Style` | The style of the member's string value, as `DefaultScalarStyle` gives it to every other one. |

### `YamlCommentAttribute`

| Member | Effect |
| --- | --- |
| `Comment` | A comment written on the lines above the member, in block style. |

### `YamlIgnoreCondition`

| Value | Effect |
| --- | --- |
| `WhenWritingEmptyCollection` | Leaves out a null value or an empty collection, as YamlDotNet's `OmitNull \| OmitEmptyCollections`. |

### `ReferenceHandler`

| Value | Effect |
| --- | --- |
| `Preserve` | Writes an anchor for each object or collection referenced more than once, and aliases for the references. |
| `PreserveAll` | Writes an anchor for every object and collection. |

<!-- /options-reference -->

## System.Text.Json options that changed

| Option | In Styml |
| --- | --- |
| `WriteIndented` | Formats the JSON-compatible style; block YAML is always written on lines. |
| `IndentSize`, `IndentCharacter` | Block style needs an indent of at least 2 spaces; a tab is refused. |
| `AllowTrailingCommas` | Kept for source compatibility; it changes nothing, since YAML flow collections accept one trailing comma. |
| `ReadCommentHandling` | `#` comments are skipped by default (`Disallow` behaves as `Skip`). |
| `AllowDuplicateProperties` | A view of `DuplicateKeyHandling`: `true` selects `LastWins`, `false` `Error`. |
| `ReferenceHandler` | Writes YAML anchors and aliases in block style; `$id`, `$ref` and `$values` in the JSON-compatible style. |
| `IgnoreNullValues`, `AddContext` | Removed (obsolete in System.Text.Json). |
