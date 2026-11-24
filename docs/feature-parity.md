# Feature parity rule

The rule Styml follows for YAML features.

**Rule.** If YamlDotNet or SharpYaml implements a YAML feature, Styml implements it too. A feature may be refused
only when **both** libraries refuse it. "Implements" includes opt-in support: a feature one of them offers behind an
option may be opt-in here as well.

The rule was stated for the four implementation calls of M3a.
The rule also overrides the earlier cuts of decision D6.

## Evidence

Measured on 2026-11-17 with YamlDotNet 18.1.0 and SharpYaml 3.17.0 on net10.0, default options unless stated, by
deserializing small documents into classes, dictionaries and `object`. The JSON Lines row was measured on
2026-11-18 (M3d, part 1).

| Feature | YamlDotNet | SharpYaml | Styml after M3b | Consequence |
|---|---|---|---|---|
| Alias as a mapping key (`*k : v`) | Yes | No ("expected a scalar key token") | Yes: the anchored scalar's text | **Implemented** (reader M3b, DOM M5a, serializer M5b). Reverses M3a's first implementation call. |
| Alias to a mapping or sequence, typed member | Yes; same instance | Opt-in (`ReferenceHandling.Preserve`); same instance | Yes, by default; same instance | **Implemented in M5b.** |
| Alias to a scalar, typed member | Yes | No, even with `Preserve` | Yes; read again through the member's converter | **Implemented in M5b.** |
| Alias in an `object` member or untyped document | Yes | Opt-in (`Preserve`) | Yes; a `YamlElement` of the anchored node | **Implemented in M5b.** |
| Plain number or boolean into a `string` member (`A: 12`, `{"A": 12}`) | Yes | Yes | Yes (decision D2) | Keep. |
| Empty input into a class | Returns `null` | Returns `null` | Reader yields no tokens (D4); the serializer reads zero tokens as a null, under every schema | **Implemented in M5c** (decision 2). `YamlDocument.Parse("")` keeps throwing, as ruled. |
| Duplicate mapping keys | Last wins; `WithDuplicateKeyChecking()` throws | Throws (`DuplicateKeyHandling.Error`); `LastWins` and `FirstWins` opt-in | `Error` by default, `LastWins` and `FirstWins` opt-in (`DuplicateKeyHandling` on both option types; `AllowDuplicateProperties` is its view) | **Implemented in M5c** (decision 1). Keys compare by decoded text at untyped sites, as both libraries. |
| JSON and failsafe schemas | No | `Schema = Json` / `Failsafe` with `UseSchema = true` | `YamlSchemaKind.Json` and `Failsafe`, on the reader, the DOM and the serializer (`YamlSerializerOptions.Schema`) | **Implemented in M5c**. Under Json an unmatched plain scalar is an error, as YAML 1.2.2 (10.2.2) says; SharpYaml reads it as a string (not offered as an opt-in). |
| Tab used as indentation | Rejected | Rejected | Rejected | Keep. |
| Newline-separated top-level values (JSON Lines: `1\n2\n3`, `{"a": 1}\n{"a": 2}`) | No: scalars fold into one plain scalar (`"1 2 3"`); a second object, array or quoted string throws, in the deserializer and in the parser | No: same | No: same, with `AllowMultipleValues` and with `DeserializeAsyncEnumerable(..., topLevelValues: true)`. Several values are `---`-separated documents (D3), which all three read | **Refused**, as the rule allows. M4's decision 1: the writer emits `---` between top-level values (until M7a, a line feed only under the test-only JSON grammar switch). |

Earlier cuts (decision D6), same measurement:

| Feature | YamlDotNet | SharpYaml | Styml after M3d | Decision |
|---|---|---|---|---|
| Explicit key spanning lines (`? first` / `  second`) | Yes | Yes | Accepted: `KeyKind.Scalar`, text `first second` | **Implemented in M3d** (block scalars as keys too). |
| Collection as a mapping key (`? [a, b]`, `[a, b]: v`) | Yes, into `Dictionary<List<string>, string>` and `object` | Parser yes (all 17 suite cases but M2N8/00); serializer no | Reader accepts every form (`KeyKind.Complex`, raw key as `ValueSpan`, `CreateKeyReader()`); DOM (`YamlProperty.KeyKind`, `GetKeyElement()`), nodes and serializer read and write them (block style) | **Implemented in M3d** (reader), as D10; **DOM, nodes and serializer in M5e** (decision 4): typed keys (`Dictionary<List<string>, V>`, class, record and struct keys), `object` keys as `YamlElement` (scalar keys as `string`), duplicates compared structurally in the DOM. Implicit collection keys can be turned off with `DisallowImplicitCollectionKeys` , for streams of JSON; the default keeps them. |
| YAML 1.1 `yes`/`no` into `bool` | Yes | Opt-in (`Schema = Extended` **and** `UseSchema = true`; `Extended` alone still rejects) | `String` token by default; `True`/`False` with `YamlReaderOptions.Schema = YamlSchemaKind.Yaml11` | **Implemented in M3d**, opt-in; the forms are in the table below. |
| Implicit collection key longer than 1024 characters, or spanning lines in block context or in a flow sequence pair | Rejected (parser) | Rejected (parser) | Rejected | Keep (YAML 1.2 sections 7.4.2 and 8.2.2; libyaml's simple keys). Explicit keys have no limit in all three. |
| Collection key spanning lines inside a flow mapping (`{[a,\n b]: c}`) | Rejected (parser) | Rejected (parser) | Accepted | Keep: YAML 1.2 allows it (flow mapping keys are in flow-in context); accepting more than both is outside the rule. |
| Merge key (`<<: *base`) | Opt-in (`MergingParser`) | Yes by default for an inline mapping (`<<: {x: 1}`); `<<: *base` needs `ReferenceHandling.Preserve`, like every alias | Opt-in (`ResolveMergeKeys`, serializer and DOM) | **Implemented in M5b**, opt-in (decision 3). |
| `%TAG` handle expansion | Parser resolves the full tag; the deserializer then rejects the document ("unresolved tag") | Parser resolves the full tag; the serializer accepts the document and ignores the tag | `Tag` as written, `GetExpandedTag()` / `TryCopyExpandedTag()` expanded (percent-decoded, verbatim and redefined handles as both parsers); the serializer ignores the tag | **Implemented in M3d.** |
| A named-handle tag in a value read as `object`, `YamlElement` or `YamlNode` (`%TAG !e! ...`, then `!e!x`) | Refused untyped ("unresolved tag": an untyped read needs a tag mapping) | Reads | Reads, with the tag expanded through the stream's handles; before, every such route refused it ("The YAML tag handle is not declared") | A standard-YAML defect, fixed. |
| A number written back from a document model (`0x1F`, `012`, `1_000`, `00000000`) | Its source text (`YamlStream`) | Its source text (`Model.YamlStream`) | Its source text in block style, where the writer's schema reads it to the same number; by value in JSON, under `IgnoreSourceStyles`, or for an edited value (`NumberTextParityTests`) | Before, every YAML spelling was written by value. |
| `%TAG` in a later document without directives (Unity's files: `%TAG !u!` once, then `--- !u!4`) | Kept when the stream declares `%YAML 1.1` (YAML 1.1's rule, section 7.4: a document with any directive starts from its own); reset without it or under `%YAML 1.2` | Refused ("undefined tag handle"): YAML 1.2, each document independent | Refused by default (YAML 1.2, section 9.1); kept under `InheritTagDirectives`, with YamlDotNet's rule. `%YAML 1.1` does not turn it on (YamlDotNet's parser does; neither library changes the schema on it) | Opt-in. |
| Unity's `stripped` after a document's properties (`--- !u!4 &400000 stripped`) | Refused ("found invalid mapping") | Refused | Refused by default (YAML reads `stripped` as the root's content); accepted under `AcceptUnityStrippedMarker`, after a tag and at the end of the line (VYaml: after a `!u!` tag only); `IsUnityStripped` on the reader, `YamlElement` and `YamlNode`, and `WriteUnityStrippedMarker` | Opt-in; VYaml always accepts it (`UnityStrippedMark`). |

### YAML 1.1 forms (M3d)

Measured 2026-11-18, one parity test per row (`Yaml11Forms_*` in `test/Styml.Parity.Tests`), reading into a typed
member. "Ext" is SharpYaml with `Schema = Extended` and `UseSchema = true`; Styml is the reader with
`YamlSchemaKind.Yaml11`. A form is in the option when at least one library accepts it. The leading-zero rows are
`LeadingZeroIntegers`, which also reads Styml's `YamlElement`, `YamlNode` and `object` against the typed value.

| Form | YamlDotNet | SharpYaml | SharpYaml Ext | Styml `Yaml11` | Styml default |
|---|---|---|---|---|---|
| `y Y yes Yes YES on On ON` / `n N no No NO off Off OFF` | bool | refused | bool | bool | string |
| `true`/`false` in any case (`tRue`, `FaLsE`) | bool | bool | refused | bool in typed reads with `AcceptLenientScalars` (token: string) | the same |
| Mixed-case 1.1 boolean `yEs` | bool | refused | refused | bool in typed reads with `AcceptLenientScalars` (token: string) | refused, with the option too |
| Leading-zero octal `0777`, `010` | 511, 8 | 777, 10 (decimal) | 511, 8 | 511, 8 | 777, 10 |
| `0o17` (1.2 octal) | refused | 15 | 15 | 15 | 15 |
| Signed leading-zero `-012`, `+012` | -10, 10 | -12, 12 (decimal) | -10, 10 | -10, 10 | -12, 12 |
| `08`, `09` (not octal) | refused | 8, 9 | refused | 8, 9 | 8, 9 |
| Binary `0b1010`, `-0b11`, `0b1_0` | 10, -3, 2 | same | same | same | string |
| `_` separators `1_000`, `0x_1F`, `1_000.5` | yes | yes | yes | yes | string |
| Sexagesimal integer `190:20:30`, `-1:30` | 685230, -90 | refused | refused | 685230, -90 | string |
| Sexagesimal float `190:20:30.15` | refused | refused | refused | string (refused) | string |
| `0777.5` | 777.5 | 777.5 | refused | 777.5 | 777.5 |
| `~` | null | null | null | null | null (core) |
| `NuLL` | string | null | string | string; null in typed reads with `AcceptLenientScalars` | the same |

**Found while measuring.** Both libraries accept binary integers and `_` separators with default options, and
both read `true`/`false` in any case as a boolean; SharpYaml also reads `null` in any case, and YamlDotNet `yEs`.

- **Case-insensitive booleans:** `true`/`false` in any case is accepted in typed reads only; the token stays a
  string, so untyped reads keep the exact 1.2 core schema (`CaseInsensitiveBoolean_AllThreeInTypedReads`). Since
  M4b it needs `AcceptLenientScalars`, since non-standard behaviour is opt-in; see the next section.
- Binary integers and `_` separators stay behind `Yaml11` (`DefaultSchemaGap_BothAcceptStymlCoreRefuses`); the rule
  allows opt-in support.
- **One library only** (`OneLibraryOnly_BehindAcceptLenientScalars`, formerly `OpenRows_OneLibraryOnly`): `yEs`
  as a boolean (YamlDotNet only) and `null` in any case (SharpYaml only) are read with `AcceptLenientScalars`, by the
  rule (a feature exists if either library has it), opt-in since it is non-standard.
  See the next section.

### Lenient scalars: `AcceptLenientScalars` (M4b)

`YamlReaderOptions.AcceptLenientScalars` and `YamlSerializerOptions.AcceptLenientScalars`, off by default, cell by
cell in `LenientScalarsParityTests.Matrix` (150 rows) and `Yaml11Keys` (9), against YamlDotNet 18.1.0 and SharpYaml
3.17.0 with default options. When a `Utf8YamlReader` is passed in, its own option
applies.

| Form | YamlDotNet | SharpYaml | Styml default | Styml with the option |
|---|---|---|---|---|
| `tRue`, `FaLsE` into `bool` | bool | bool | refused | bool |
| `yEs`, `oN`, `nO`, `oFF` into `bool` | bool | refused | refused | bool with `YamlSchemaKind.Yaml11` only |
| `'true'`, `"12"`, `!!str 12`, `\|` and `>` blocks, `' 12'`, `'0x1F'` into `bool` and the number types | read | read | refused | read, when the trimmed text is the plain scalar of a boolean, number or float name under the active schema (`'1_000'` and `'yes'` with `Yaml11` only) |
| `''`, `'null'`, `'~'`, `'NuLL'` into a typed target | refused | refused | refused | refused: a quoted null is a string |
| `!!bool tRuE`, `!!float .iNf`, `!!null NuLL` | read (not `!!null`) | read (not `!!bool yEs`) | tag mismatch | read: the tag's type, so the token changes |
| A mismatching tag (`!!int true`, `!!bool 1`) | refused | read (it ignores tags) | refused | refused |
| `NuLL` into `string`, `int?`, a class, an array, an element or value, `Uri` | string, or refused | null | string, or refused | null |
| `null`, `~`, empty, `NuLL` into a non-nullable value type: member, element, dictionary value, root | default (not `Guid`, `TimeSpan` for `null`) | default for members; refused in collections and at the root | refused | default, every built-in value type and structs |
| The same into a constructor parameter | builds no record | refused | refused | refused |
| `NuLL` into `object`, `YamlElement` | string | null | refused (the DOM reads JSON-shaped YAML only until M5) | the same |
| `.iNf`, `-.iNF`, `.nAn` into `double`, `float` | ∞, NaN | ∞, NaN | refused | ∞, NaN |
| Signed `-.nan`, `+.nAn` | NaN | refused | refused | NaN (one library is enough) |
| `nan`, `NaN`, `[-+]?infinity` in any case | refused | read (also into `Half`) | refused | read (also into `Half`) |
| `.inf`, `.nan` into `Half` | refused | refused | refused | refused |
| `∞` into `Half` | ∞ | refused | refused | refused (an accident, below) |
| Plain `tRuE:` as a `Dictionary<bool, …>` key (a plain key reads as a value would) | bool | bool | refused (until this step: read) | bool |
| Quoted `'tRuE':` as a key | bool | bool | bool (System.Text.Json's key parse) | the same |
| `yes:`, `On:`, `NO:` as keys under `Yaml11` (E) | bool | refused | bool (a gap of M3d's schema, fixed without the option) | bool; `yEs:` too |

| A quoted, block or `!!str` number into an enum (`'1'` into `DayOfWeek`) | read | read (also `'0x1'`) | refused | read, as the plain number; `'0x1'` too |
| An enum name (`Monday`, plain or quoted) | read | read | refused (enums read numbers, as System.Text.Json's do; `YamlStringEnumConverter` reads names) | the same (numbers only, below) |
| Padded text (`' 2001-01-01 '`, `"…\n"`, `\|` blocks) into `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid` | read | read | refused | read, as the trimmed text |
| Padded text into `Version` | read | refused (no `Version` support) | refused | read |
| `'x '`, `"x\n"` into `char` | `x` | refused | refused | `x` |
| `' x '`, `'\tx'`, `'xy'` into `char` | the first character (a space, a tab, `x`) | refused | refused | refused (one character only, below) |
| Padded text into `Uri` | read | refused | read (`Uri` trims) | the same |

**Refused departures.** Four library behaviours that look like accidents rather than features, refused by Styml in
every setting:
- **YamlDotNet's `'- 1'` read as -1:** its number parser takes a sign, a space and digits.
- **YamlDotNet's `∞` into `Half`:** it parses `Half` with the current culture, whose infinity symbol is `∞` under en-US
  and `Infinity` under the invariant culture; the matrix measures the other libraries under en-US.
- **SharpYaml ignoring a mismatching tag:** `!!int true` into `bool`, `!!bool 1` into `int`.
- **Both libraries parsing dates with the machine's culture:** `1.5` into `DateTime` gives January 5 of the current year.

One character only: YamlDotNet reads the first character of any longer text into a `char` (`' x '` gives
a space, `'xy'` gives `x`), and SharpYaml refuses those. Styml with the option reads only one character followed by
white space (`'x '` gives `x`), and refuses the rest.

Enums read numbers only, **a departure from the parity rule:** both libraries read an enum by name (`Monday`
into `DayOfWeek`) with default options. Styml's enums read numbers only by default, as System.Text.Json's do, and names
need `YamlStringEnumConverter`.

**Also settled:** `NuLL` into a `string` reads null with the option (SharpYaml): "NuLL as null" is an option, off
by default. `'1_000'` reads under `Yaml11` only, as plain `1_000`
does: the option does not bring YAML 1.1 in, as for `yEs:`. `KeyValuePair`,
`DateOnly`, `TimeOnly`, `Half` and the 128-bit integers read null as their default with the option, like every other
value type (one library is enough).

Not a feature, so the rule does not apply: how thoroughly the chunking tests cover the seven largest corpus items
(M3a call 2).

Corrections after `test/Styml.Parity.Tests` measured every row (2026-11-17, M3b): SharpYaml's YAML 1.1 booleans
need `UseSchema = true` as well as `Schema = Extended`; SharpYaml applies merge keys by default (the earlier
"no" came from the aliased form, which needs `ReferenceHandling.Preserve`); both libraries' parsers expand `%TAG`
handles, and it is YamlDotNet's deserializer, not SharpYaml's, that refuses the document afterwards. None of these
changes a decision.

## Writer (M4)

Measured on 2026-11-19 with YamlDotNet 18.1.0 and SharpYaml 3.17.0 (VYaml 1.6.1 for comparison) by a program
that calls each library's emitter. The rows are asserted in
`test/Styml.Parity.Tests/WriterParityTests.cs`, with the same row numbers.

| Rows | Feature | YamlDotNet | SharpYaml | Styml | Decision |
|---|---|---|---|---|---|
| 1, 1b, 2 | Indentation width; a sequence under a key | 2, `WithBestIndent(n)`; `key:\n- x`, `WithIndentedSequences()` | 2, `IndentSize`; `key:\n  - x` | 2, `IndentSize`; `key:\n- x`, `IndentSequences`; YamlDotNet's layout for other widths | **Implement in M4a.** |
| 3, 4 | Collections in a sequence | compact `- a: 1`, `- - 1` | compact mappings, expanded sequences; options for both | compact; `ExpandSequenceItems` | **M4a** (compact), **M4b** (option). |
| 5 | Block, JSON, per-collection flow | block; `.JsonCompatible()`; flow in the emitter | block; flow in the emitter | block by default (M4c); `Style = JsonCompatible`; `WriteCollectionStyle(Flow)` | **M4a** / **M4b**. |
| 6, 7, 11 | Empty collections, null, the final line break | `[]` `{}`; `Nothing: ` (empty); yes | `[]` `{}`; `null`; yes | `[]` `{}`; `null`; yes | **M4a.** |
| 8, 9, 30 | `---`, `...`, `%YAML` / `%TAG`, several documents | emitter only; two serializer calls give `V: 1\nV: 2` | same | `WriteDocumentStart` / `WriteDocumentEnd`, `WriteDirective`; `---` from `SerializeAsyncEnumerable(..., topLevelValues: true)` | **M4a** / **M4b.** |
| 13, 14 | Keys above 1024 characters; a key with a line break | `? key` above 1024, `MaxSimpleKeyLength`; `? >-` | implicit (its reader then throws); `"a\nb"` | `? key` above 1024; `"a\nb"` | **M4a.** |
| 15–18 | Collection keys, anchors and aliases, cycles, tags | yes, anchors by default | `ToString()` keys; anchors with `Preserve`; tags for polymorphism | `WriteStartPropertyKey`, `WriteAnchor`, `WriteAliasValue`, `WriteTag` | **Writer in M4b, serializer in M5.** |
| 19, 20, 21 | Comments, line width, scalar style per call | own line and end of line; `BestWidth`; every style | none; emitter `bestWidth`; every style but `ForcePlain` | `WriteCommentValue`, `MaxLineWidth`, `WriteScalarStyle` | **M4a** / **M4b.** |
| 22, 23, 24 | Quoting, multi-line strings, non-ASCII | single quotes; folded; raw, astral `\U…` | double quotes; `"a\nb"`, `MultilineStringStyle`; raw | double quotes; `"a\nb"`, literal per value, `MultiLineStringStyle`; raw | **M4a** / **M4b.** |
| 25–29 | NaN and infinities, numbers, dates, times, versions, URIs, bytes | `.nan`; plain dates; `TimeSpan` plain | `.nan`; quoted dates and times | `.nan`; plain dates; `TimeSpan`, `TimeOnly`, `Version`, Base64 always quoted | **M4a.** |
| 28c, 28d, Q1–Q136 | Which strings are quoted | core non-strings written plain | quotes the core forms, more YAML 1.1 forms, some syntax | quotes every form the YAML 1.2 core schema or the YAML 1.1 types resolve, `-`/`?` before a non-space plain (as both); with `QuoteForLenientReaders` (off by default), also what only a lenient reader resolves | **M4a**; **M4b** (the option: non-standard behaviour is opt-in). M4's decision 2 sets the default set. |
| keys | Plain keys read into `Dictionary<object, …>` | strings | resolved: `true` a `Boolean`, `1` an `Int64`, `1.5` a `Double` | quoted like values, so no reader resolves them | **M4a.** SharpYaml's resolution is why keys are quoted. |
| 31, 32, 35 | `IAsyncEnumerable` at the root, raw text, quoted keys | `{}`; no; no | `{}`; no; emitter only | a sequence; `WriteRawValue`, validated; `WriteScalarStyle(DoubleQuoted)` | **Exists** / **M4a.** |
| 13, 33, 34 | Canonical output, `SkipAnchorName`, `MaxSimpleKeyLength` above 1024, writing ambiguous strings plain | yes (each) | no; no; no; `PreferQuotedForAmbiguousScalars = false` | no | **Refused, deliberate departures from the rule (M4, decision 9).** `WriteRawValue` is the escape hatch for a string written plain. |

`test/Styml.Parity.Tests` asserts each row's measured output for YamlDotNet and SharpYaml, and Styml's JSON-compatible
output (as at `9835d4d`, except decision 1's `---` between top-level values since M4a), in `WriterParityTests` (cases in
`WriterParityCases.cs`, texts in `WriterParityExpected.cs`, regenerated by running the class with
`STYML_WRITER_PARITY_EXPECTED` set to that file). Styml's block-style targets are data there; each is checked by reading
it back with Styml's reader, and each quoting target also with YamlDotNet's and SharpYaml's parsers and typed readers.
One target does not read back in SharpYaml: a quoted `"<<"` key, which SharpYaml's typed reader still takes as a merge
key, its own refusal. VYaml is not asserted.

**Since M4a** (`WriterBlockParityTests`), what Styml writes in block style is asserted against those targets: every
Table 1 row that M4a's members can write, and all 136 quoting strings as values and keys and the 16 sequence items.
M4a corrected three M4.0 targets that wrote the item `y` plain, which decision 2 quotes, and found that the `25.specials`
data row's keys (`nan`, `inf`) are quoted as values are. The same class holds M4's cross-library oracle: the block
output of the M6a documents read by YamlDotNet (parser and object graph) and SharpYaml (object graph, which resolves
plain scalars) equals what each reads from the JSON twin, and every quoting string reads back as a string in both,
as a value and, for SharpYaml, as a key of `Dictionary<object, object>`.

**Since M4b** the rows that M4b's members write are asserted too: the expanded items (3, 4), the directive (9), the tag
(18) and the own-line comment (19) write their targets exactly. Anchors, aliases, collection keys and flow
collections (5, 15, 16) have no target in that data, since their JSON twin cannot hold them; `BlockWriterNodesTests`
and oracle 4 (the yaml-test-suite read, written and read again) cover them. The cross-read property test
(`WriterBlockFormsCrossReadPropertyTests`) reads the new output forms with YamlDotNet and SharpYaml: flow collections,
single-quoted and folded scalars, `MaxLineWidth` and anchored nodes with their aliases. SharpYaml does not resolve an
alias to a scalar into `object`, even with `ReferenceHandling.Preserve` ("Unknown YAML alias"), so it reads the
anchored nodes without their aliases and YamlDotNet checks the aliases.

**Found while verifying M4b**, behaviour of other readers, not Styml defects:
- **YamlDotNet 18.1.0's parser rejects an anchored scalar value in a flow mapping when another entry follows:**
  `{a: &x b, c: d}` gives "While parsing a flow mapping, did not find expected ',' or '}'". It reads `{a: &x b}`,
  `{a: &x [], c: d}`, `{&x a: b, c: d}` and the block form; Psych, PyYAML and VYaml read all of them.
- **Anchor names:** YAML 1.2 allows any non-space character other than the flow indicators. SharpYaml and VYaml reject
  names with characters outside letters, digits, `-` and `_` (`&k:1`, `&é2`), and YamlDotNet rejects `&k:1` inside a
  flow collection. `WriteAnchor`'s remarks say so.
- **Harmless over-quoting by default:** digit strings with an underscore where no YAML 1.1 pattern has one (`09_`,
  `0_9`, `1_e0`, `1e0_`, `+.0_`) are quoted, since Styml's reader resolves them under the YAML 1.1 schema (116 of
  718,000 short strings in that run). `BlockQuotingSpecPatternTests` pins the full list against the patterns as
  printed.

## The parity follow-ups (M7b)

Measured on 2026-11-23 with YamlDotNet 18.1.0 and SharpYaml 3.17.0, and asserted live in
`test/Styml.Parity.Tests/ParityFollowUpsParityTests.cs`: the five parity follow-ups of M5, which M7b implemented. Each is a feature at least one library has, so each is implemented.

| # | Feature | YamlDotNet | SharpYaml | Styml after M7b | Decision |
|---|---|---|---|---|---|
| 1 | A collection's flow style on DOM write-back | Kept (and `...` after the document) | Kept | Kept: `YamlElement.CollectionStyle` (read; `null` for a scalar), `YamlObject.CollectionStyle` and `YamlArray.CollectionStyle` (settable, kept across edits, `Clear` and `DeepClone`; not part of `DeepEquals`). JSON's collections are flow; a single-pair mapping in a flow sequence is flow. | **Implemented in M7b**, with the existing `YamlCollectionStyle`. |
| 2 | A single-quoted string with a line break | `'a\n\n  b'` (the closing quote after a final break at column 0) | Written double-quoted | `'a\n\n  b'` (the closing quote at the content's column), from a member style, a writer hint, a kept source style or `DefaultScalarStyle`; double-quoted with a tab, a `\r` or a space next to a break, and inside a flow collection | **Implemented in M7b.** No new API. |
| 3 | A serializer-wide default scalar style | `WithDefaultScalarStyle`: strings, chars, enum names and a dictionary's string keys; member names, numbers, booleans and nulls left alone; dates and GUIDs plain; a member's style wins; on a key a literal or folded default writes double quotes | None (`ScalarStylePreferences` only) | `DefaultScalarStyle` on `YamlSerializerOptions`, `YamlWriterOptions` and the generation attribute: string values, the typed values written as YAML strings (dates, times, GUIDs, URIs, versions, chars, enum names, Base64) under a quoted default, and a dictionary's and a `YamlObject`'s string keys; member names, numbers, booleans and nulls left alone. Precedence: a converter's `WriteScalarStyle`, a member's style, a kept source style, `MultiLineStringStyle` (set, for a string with a line break), `DefaultScalarStyle`, the writer's default; a style that cannot hold the text gives way to the writer's own choice (YamlDotNet's `Plain` writes a multi-line string single-quoted, its `Literal` writes one line as `\|-` and a key double-quoted; Styml writes them as its default does). A source-generated context takes its metadata path while it is set. | **Implemented in M7b** (keys and typed values in its fix A). |
| 4 | Collection keys in the DOM | `YamlMappingNode.Children` keyed by node: its count includes them, document order, an added entry last, order-insensitive equality | The model writes them back | `YamlObject.CollectionKeys`, `AddCollectionKey`, `RemoveCollectionKey`: source order kept on write-back, an added key after every entry; `DeepEquals` and `DeepClone` include them. `Count`, the indexer and enumeration stay string-keyed (`IDictionary<string, YamlNode?>`). | **Implemented in M7b.** `Count` departs from YamlDotNet's, for the dictionary contract. |
| 5 | A key converter that writes a collection key | Through its type converters: `? [1, 2]\n: p` | The key's `ToString()` | `? [1, 2]\n: p`, from `YamlConverter<T>.WriteAsPropertyName` with `WriteStartPropertyKey` and `WriteEndPropertyKey` (M4b), which already existed | **Exists**; M7b adds its tests. No `WriteStartComplexKey`. |

## M7c: the write-back follow-ups and `IgnoreSourceStyles`

Measured on 2026-11-23 with YamlDotNet 18.1.0 and SharpYaml 3.17.0, and asserted live in
`test/Styml.Parity.Tests/WriteBackFollowUpsParityTests.cs`.

| Feature | YamlDotNet | SharpYaml | Styml after M7c | Decision |
|---|---|---|---|---|
| A long flow collection at a line width | Broken after a comma once past the width (libyaml's rule), continuation lines indented one level per flow collection | The same breaks | The same, byte for byte, with `MaxLineWidth`; no width, one line, as both | **Implemented in M7c** (the B). |
| The column when output is flushed mid-line | Tracked by the emitter | Tracked by the emitter | Kept across flushes: folding and flow breaks do not depend on the buffer size | **Implemented in M7c** . |
| A copy made through an alias, after an edit | The model shares the node: written as `*x` | — | Styml's nodes copy what an alias names (M5a). A top-level copy is written without the anchor. An alias inside an unedited child (`c: [*s]`) or inside a copy's content is written as the anchored node with its anchor, which redefines the name at that use (legal YAML; it re-reads equal) | **Implemented in M7c** (the C); the nested case is described, not changed. |
| Writing a parsed document in the options' styles | No option (the model keeps the source's styles) | No option | `IgnoreSourceStyles` on the writer options, the serializer options and the generation attribute; off by default, as both libraries keep a source's styles | **Implemented in M7c**. |
| The writer under the JSON schema | — | `Schema = Json` reads only | Block output double-quotes string values, dates and GUIDs, so it reads back under `Schema = Json`; keys unchanged | **Implemented in M7c** (M5f's follow-up). |

## Keeping the table honest

A small test project (`test/Styml.Parity.Tests`, net11.0 and net10.0 only) pins the two libraries and asserts each
row above for all three libraries: what YamlDotNet does, what SharpYaml does, and what Styml does. When a library
version is bumped or Styml gains a feature, the failing assertion says which row to update.
