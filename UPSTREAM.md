# Upstream

Styml is derived from `System.Text.Json` in [dotnet/runtime](https://github.com/dotnet/runtime).

| | |
|---|---|
| Repository | https://github.com/dotnet/runtime |
| Tag | `v11.0.0` |
| Commit | `deae2e32bf0e018e09d64597b7c59b62b73bb3ce` |
| License | MIT, © .NET Foundation and Contributors; Styml's own code MIT, © Sébastien Ros (see `LICENSE`, `THIRD-PARTY-NOTICES.txt`) |

## How the import is produced

The import is scripted and replayable. It reads only the runtime repo's object database (`git archive`), never
its working tree, and never writes to it.

```bash
eng/import/import.sh <runtime-clone> deae2e32bf0e018e09d64597b7c59b62b73bb3ce   # copy per eng/import/manifest.txt
eng/import/verify.sh <runtime-clone> deae2e32bf0e018e09d64597b7c59b62b73bb3ce   # blob-SHA comparison, expect 0 problems
```

- `eng/import/manifest.txt`: every upstream directory/file and where it lands here. The vendored file list
  (`src/Vendored`, `test/Vendored`) was derived from the tag's csprojs/targets (`$(CommonPath)`,
  `$(CoreLibSharedDir)`, `$(LibrariesProjectRoot)`, `$(CommonTestPath)` items) plus the files injected by
  runtime build infrastructure (`eng/resources.targets`, `src/libraries/Directory.Build.targets`).
- `eng/import/exclude.txt`: upstream paths dropped from directory imports.

At the import commit `eng/import/verify.sh` reports `verified 1040 files, 0 problems`.

The rename to `System.Text.Yaml` / `Yaml*` is a separate, scripted step: see `eng/rename/`.

## Layout mapping

| Upstream (`src/libraries/…`) | Here |
|---|---|
| `System.Text.Json/src` | `src/Styml` |
| `System.Text.Json/gen` | `src/Styml.SourceGeneration` |
| `System.Text.Json/Common` | `src/Common` |
| `Common/src/…` (linked files) | `src/Vendored/Common/…` |
| `System.Private.CoreLib/src/…` (linked files) | `src/Vendored/CoreLib/…` |
| `System.Collections/src/…` (OrderedDictionary) | `src/Vendored/System.Collections/…` |
| `System.Text.Json/tests/Common` | `test/Common` |
| `System.Text.Json/tests/System.Text.Json.Tests` | `test/Styml.Tests` |
| `System.Text.Json/tests/System.Text.Json.SourceGeneration.Tests` | `test/Styml.SourceGeneration.Tests` |
| `System.Text.Json/tests/System.Text.Json.SourceGeneration.Unit.Tests` | `test/Styml.SourceGeneration.Unit.Tests` |
| `System.Text.Json/tests/System.Text.Json.SourceGeneration.TestLibrary` | `test/Styml.SourceGeneration.TestLibrary` |
| `Common/tests/…` (linked files, TestUtilities subset) | `test/Vendored/…` |
| `System.Text.Json/docs`, `README.md`, root `THIRD-PARTY-NOTICES.TXT` | `docs/upstream/` |

## Not imported

| Upstream | Reason |
|---|---|
| `System.Text.Json/ref` | No reference assembly project; the library is a plain package. |
| `System.Text.Json/roadmap`, `source_package`, `System.Text.Json.slnx` | Not needed outside the runtime repo. |
| `src/CompatibilitySuppressions.xml` | API-compat baseline for the shipping STJ package; does not apply. |
| `src/ILLink/ILLink.Descriptors.LibraryBuild.xml` | Runtime library-build trimming descriptor (only consumed by the runtime's own ILLink pass). At the tag there is no `ILLink.Substitutions.xml`; the reflection feature switch is a `[FeatureSwitchDefinition]` attribute. |
| `gen/Resources/xlf` | Localization (Xliff) is not used. |
| `gen/*.Roslyn3.11.csproj`, `gen/*.Roslyn4.0.csproj`, `gen/JsonSourceGenerator.Roslyn3.11.cs` | Single Roslyn 4.4 generator. `JsonSourceGenerator.Roslyn4.0.cs` **is** imported (the 4.4 project compiles it). |
| `tests/System.Text.Json.FSharp.Tests` | F# tests; see `test/EXCLUSIONS.md`. |
| `tests/*.Roslyn3.11*.csproj`, `tests/…SourceGeneration.Tests/roslyn3.11.rd.xml` | Roslyn 3.11 test variants; see `test/EXCLUSIONS.md`. |
| `tests/System.Text.Json.Tests/TrimmingTests` | ILLink/AOT trimming test apps driven by runtime infrastructure; see `test/EXCLUSIONS.md`. |
| `Common/tests/TestUtilities` (most of it) | Replaced by a small shim in `test/TestShims`; only `AssertExtensions.cs`, `ThreadCultureChange.cs`, `LineEndingsHelper.cs` are vendored. |

## Rename (eng/rename)

- **Pass A** (`eng/rename/rename.py` + `protect.json`, commit "Rename System.Text.Json to System.Text.Yaml"):
  textual `System.Text.Json` → `System.Text.Yaml`, then `Json` → `Yaml`, with protected tokens; generator
  diagnostic ids `SYSLIB1030-1039/1220-1229/1240-1249` → `STYML…`; file/directory names (vendored files keep
  their upstream paths). The vendored `StringSyntaxAttribute.cs` is not renamed (its `Json` constant is the BCL's).
  `protect.json` records the expected count of every protected token, so a replay on a newer tag that silently
  loses a protection fails. Replay: check out the import commit, copy `eng/rename`, run
  `python3 -I eng/rename/rename.py`, commit the output unedited.
- Rename-level fix-up (scripted, in `protect.json`): source-generator baselines hard-code the generator identity
  `GeneratedCodeAttribute("System.Text.Json.SourceGeneration", "42.42.42.42")` (42.42.42.42 is Arcade's dev-build
  version). They now say `("Styml.SourceGeneration", "%VERSION%")`; the test already substitutes `%VERSION%`
  with the generator's real version.
- **Pass B** (`eng/rename/passb/passb.cs`, M2b, `dotnet run eng/rename/passb/passb.cs`): Roslyn rewrite of
  identifier tokens only: lowercase-leading `json…` (optionally after `_`, `s_`, `t_`) → `yaml…`, e.g. the public
  parameter names `json` → `yaml` and `jsonTypeInfo` → `yamlTypeInfo` (`utf8Json` was already `utf8Yaml` after Pass A); plus
  `<param>`/`<paramref>` names in doc comments. Each file is parsed under several preprocessor symbol sets (one per
  TFM / project flavour) and the edits are unioned; the tool asserts that no non-identifier token and no comment
  changed. String literals are untouched except one scripted rule: the generator emits a local named
  `jsonTypeInfo` from string literals in `YamlSourceGenerator.Emitter.cs`, which becomes `yamlTypeInfo`, and the
  same identifier rule is applied to the generator baselines (`Baselines/**/*.txt`, C#) rather than regenerating
  them, so the baseline tests check emitter and baselines independently. Vendored files and `test/TestShims` are
  excluded. Result: 14,256 tokens in 454 files (2 found only in code inactive under every symbol set).
  The pass B commit also hand-fixed 13 test string literals that name a renamed parameter or member: `"json"` /
  `"jsonTypeInfo"` expected `ArgumentNullException.ParamName`s, `GetProperty("s_json…")` and
  `GetField("_jsonElement")` reflection lookups, one `Assert.Contains("json", ex.Message)`. Those are now pass B's
  rule 4: a regular string literal whose whole text is an identifier the identifier rule renames is renamed the same
  way (exactly those 13 at the baseline; exceptions would go in `passb.json`'s `keep_literals`). Without it a replay on
  newer upstream code turned them back into `json…` with no merge conflict. `passb.json` records the expected number
  of emitter literals (19) and whole-literal names (13); a run that renames a different number fails, and
  `--update-counts` records the new numbers (the sync uses it on the target tree and shows the change). Not renamed:
  message text such as `Debug.Assert(..., "jsonTypeInfo.Configure() ...")`, lowercase `json` inside longer literals,
  comments and payloads. Result with rule 4: 14,269 edits.

## File headers

A file that came from the import keeps dotnet/runtime's header ("Licensed to the .NET Foundation under one or more
agreements. The .NET Foundation licenses this file to you under the MIT license."), modified or not. A file is
counted as imported when it exists at the rename commit under its renamed path, following later renames. A file
Styml created carries `// Copyright (c) Sébastien Ros. Licensed under the MIT license.` At publication, 763 C# files
kept the upstream header (14 imported files have none, as upstream), 137 Styml files had carried the upstream header
by mistake and took Styml's, and 9 Styml files without a header gained it. No Styml C# file was created as a copy of
an imported one (git's copy detection on each file's creating commit).

## Local deviations

Changes to imported files beyond the scripted rename, and build differences from upstream that affect
behaviour. Pure infrastructure written for this repo (csprojs, `Directory.*`, `eng/Resources.targets`,
`test/TestShims`) is described in the next section.

### Product code

| File | Change | Why |
|---|---|---|
| `src/Styml.SourceGeneration/YamlSourceGenerator.Parser.cs` | Member attributes (`[YamlIgnore]`, `[YamlInclude]`, `[YamlRequired]`, `[YamlPropertyName]`, `[YamlPropertyOrder]`, `[YamlExtensionData]`, `[YamlNumberHandling]`, `[YamlObjectCreationHandling]`) are recognised when their containing assembly is `Styml` (new `StymlAssemblyName` constant) instead of the namespace string. | Upstream compares `ContainingAssembly.Name` with `"System.Text.Json"`, which is both its namespace and its assembly name. Here the assembly is `Styml`, so without this every such attribute was ignored by the generator (51 failures in the first net11.0 run of `Styml.Tests`). |
| `src/Styml/System/Text/Yaml/Serialization/Metadata/ReflectionMemberAccessor.cs` | In the non-.NET branch of `CreateParameterizedConstructor<T>(ConstructorInfo)` (constructors with more than 4 parameters), constructor arguments are mapped to constructor positions skipping `out` parameters, as `ReflectionEmitMemberAccessor` does. Unchanged when there is no `out` parameter. | **Latent upstream bug** exposed by running the netstandard2.0 build: argument arrays exclude `out` parameters (`DefaultYamlTypeInfoResolver.PopulateParameterInfoValues`), but this branch copied them positionally, shifting every later argument (`NonPublicCtors_WithByRefParameters` failed on net481). Upstream only reaches it on netstandard2.0, which it never executes (its .NET Framework build uses the emit accessor). The `#if NET` branch (used when dynamic code is unsupported, e.g. NativeAOT) appears, from reading the code, to do the same positional copy; it was left unchanged and not exercised here. Worth confirming and reporting upstream. |

### Product code: the YAML grammar (M3a)

The grammar itself is in new partial files (`Utf8YamlReader.Yaml*.cs`, `YamlParseState.cs`, `YamlGrammar.cs`,
`YamlScalarStyle.cs`, `YamlReaderHelper.Yaml*.cs`, `ThrowHelper.Yaml.cs`,
`YamlSerializer.Read.Utf8YamlReader.Yaml.cs`, `YamlDocument.Yaml.cs`).
These are the edits to imported files, all under `src/Styml/System/Text/Yaml/` unless noted. Under the JSON
grammar (the D1 switch on) each one was a no-op: the new branches tested `YamlGrammar.UseJsonGrammar`, or
`_yamlNeedsProcessing` / `IsYamlScalarText`, which only the YAML grammar sets.

**M7a (decision D8) deleted the JSON grammar:** `YamlGrammar.cs`
and the switch, upstream's JSON lexers (about 1,850 lines of `Utf8YamlReader.cs` and 2,510 of
`Utf8YamlReader.MultiSegment.cs`), `ConsumeNumberResult.cs`, `ConsumeTokenResult.cs`, `YamlReaderHelper.net8.cs`,
`YamlReaderHelper.netstandard.cs`, the 28 `ExceptionResource` members and 25 strings only the lexers used, the reader
state's `_isNotPrimitive` and `_trailingCommaBeforeComment`, and every branch on the switch. The rows below that
describe a JSON-grammar branch are the record of what M3 to M5 did; the code keeps only their YAML side.

| File | Change | Why |
|---|---|---|
| `YamlTokenType.cs` | `Alias = 12` appended. | Token model, D5. |
| `Reader/YamlReaderState.cs` | New field `_yaml` (`YamlParseState`), cleared by the public constructor and passed by the internal one (new optional parameter). | Resumability: the indentation stack and document flags travel in `CurrentState`. |
| `Reader/Utf8YamlReader.cs` | `CurrentState` and the span constructor carry `_yaml`; `Read()` calls `ReadYaml()` unless `UseJsonGrammar`; `ValueTextEquals` and the internal `GetUnescapedSpan` accept plain scalars (`IsTokenTypeStringOrYamlScalar`) and compare/return the processed value when the scalar needs YAML processing. M3c: `GetUnescapedSpan` reads `ValueBytesOrSequenceCopy` instead of `HasValueSequence ? ValueSequence.ToArray() : ValueSpan`. | D1, D2; folding and YAML escapes are applied on access, like JSON unescaping. M3c: under YAML a token that spans segments has its bytes in the reader's window, so no copy is made; under JSON the property is the old expression. |
| `Reader/Utf8YamlReader.MultiSegment.cs` | The sequence constructor copies `_yaml` from the state. | Keeps the state complete. Multi-segment input threw `NotSupportedException` under YAML until M3c (D7); since M3c the YAML grammar reads it through windows (`Utf8YamlReader.Yaml.Segments.cs`), and this file is unchanged. |
| `Reader/Utf8YamlReader.TryGet.cs` | `GetString`, both `CopyString` overloads, `TryGetBytesFromBase64` and the unescaping step shared by `TryGetGuid`/`TryGetDateTime`/`TryGetDateTimeOffset` accept plain scalars and use the processed value; each `TryGet<integer>`/`Decimal` falls back to a `TryGetYaml…` (hex, octal, `+`, `.inf`/`.nan`, core-schema forms) after the JSON parse fails; `TryGetSingle`/`TryGetDouble` likewise. M3d: the JSON fast path of those 11 methods is skipped for a YAML 1.1 leading-zero octal (`YamlJsonNumberFastPath`). `GetBoolean` accepts a plain untagged `true`/`false` in any case , and its two `Debug.Assert`s on the token length apply to the JSON grammar only (YAML booleans have other lengths). M3c: the 14 sites that read `HasValueSequence ? ValueSequence.ToArray() : ValueSpan` read `ValueBytesOrSequenceCopy`. | D2 and the YAML typed reads. The JSON fast path runs first, so valid JSON numbers are parsed exactly as before. |
| `Reader/YamlReaderOptions.cs` (M3d) | New `Schema` property (`YamlSchemaKind`, with its backing field and range check), and `DisallowImplicitCollectionKeys` (review round 2). | The opt-in YAML 1.1 schema (M3 note section 13.4) and the opt-out of implicit collection keys (13.1); they live in the reader options so that `YamlReaderState` and scoped readers carry them. |
| `Serialization/YamlSerializerOptions.cs`, `Serialization/YamlSerializerOptions.Caching.cs` (M3d) | `DisallowImplicitCollectionKeys`: field, property, copy constructor, `GetReaderOptions`, and the options equality comparer (`Equals`, `GetHashCode`). | The option must reach `DeserializeAsyncEnumerable` and the stream overloads. |
| `Serialization/Converters/Value/{Char,DateOnly,TimeOnly,TimeSpan,Version}Converter.cs` | `TokenType != String` → `!reader.TokenIsStringLike`. | D2: these converters test the token type themselves; `TokenIsStringLike` is the shared "is scalar text" helper (String, or a plain Number/True/False under YAML; identical to `== String` under JSON). |
| `Serialization/Converters/FSharp/FSharpUnionConverter.cs` (3 sites) | Same. | Same. |
| `Serialization/YamlSerializer.Read.HandleMetadata.cs` (2 sites) | Same, for `$id`/`$ref` values. | Same. |
| `Serialization/YamlSerializer.Read.Utf8YamlReader.cs` | `GetReaderScopedToNextValue` returns `GetYamlReaderScopedToNextValue` under YAML. | The JSON version slices `TokenStartIndex..BytesConsumed` with `+ 2` quote arithmetic; the YAML version uses the raw token extent and seeds the scoped reader's indentation. |
| `Serialization/YamlConverterOfT.ReadCore.cs` | When the final block yields no token and `AllowMultipleValues` is off, throw `ExpectedYamlTokens`. | D4: the reader now reads empty input as zero tokens; this keeps the serializer's "no value" error. |
| `YamlHelpers.cs` | `GetUnescapedSpan` (the extension) returns the processed value when needed. M3c: reads `ValueBytesOrSequenceCopy`. | The YAML scalar forms; M3c as for `Utf8YamlReader.cs`. |
| `Document/YamlDocument.cs`, `Document/YamlDocument.Parse.cs` | Under YAML, `ThrowIfNotJsonShaped(ref reader)` before building the database. | D5 and M5: `YamlDocument` keeps its JSON-backed model until M5; block collections, non-double-quoted scalars, anchors, tags and aliases throw `NotSupportedException` instead of producing a wrong document. |
| `Document/YamlDocument.cs` (M3b) | Under YAML, `ThrowIfNoYamlValue(ref reader, database.Length)` after the parse loop. | D4: the YAML grammar reads empty, whitespace-only or comment-only input as zero tokens; without this the document came back with an empty buffer, which reads as disposed (`ObjectDisposedException` on first use). It now throws `ExpectedYamlTokens`, as the JSON grammar does. |
| `Resources/Strings.resx` | New `YamlGrammar_*` messages and `YamlNotSupported_*` texts. M3d: `YamlGrammar_ImplicitKeyTooLong`, `YamlComplexKeyHasNoText`, `YamlExpectedComplexKey`, `YamlComplexKeyNotSupported` added; `YamlGrammar_ComplexKeyNotSupported` removed. M3c: `YamlMultiSegmentNotSupported` removed. | New errors say "YAML". |

### Product code: the block writer style (M4a)

Block style is in new partial files (`Writer/Utf8YamlWriter.Block.cs`, `Writer/Utf8YamlWriter.Block.Raw.cs`,
`Writer/YamlWriterHelper.Block.cs`, `Writer/YamlWriterStyle.cs`). These are
the edits to imported files, all under `src/Styml/System/Text/Yaml/`. Unless the style is set to `Block`, which no
imported test does, the JSON-compatible output is byte for byte what it was at `9835d4d`, except decision 1's `---`
between top-level values under the YAML grammar.

| File | Change | Why |
|---|---|---|
| `Writer/Utf8YamlWriter*.cs` (the 48 `*ByOptions` / `*Slow` / prologue dispatch points) | `if (_options.Indented) { A } else { B }` becomes `if (_style == Minimized) { B } else if (_style == Indented) { A } else { block }`. | The style dispatch, in the order M4a measured (minimized first, then indented, then block). |
| `Writer/Utf8YamlWriter.WriteProperties.String.cs`, `...WriteProperties.Literal.cs` | `WritePropertyNameSection(span)` and `WriteNullSection(span)` replaced by overloads taking the `YamlPropertyInfo` (in `Utf8YamlWriter.Block.cs`), which pick the JSON or the block name section. | The block name section, with no extra argument on the JSON path. |
| `Writer/Utf8YamlWriter.cs` | `SetOptions` computes the style byte and checks the block options (`IndentSize` at least 2, no tab); `ResetHelper` clears the block state; two `Debug.Assert`s allow block style. | The block style's options and layout. |
| `Writer/Utf8YamlWriter.WriteValues.{Double,Float}.cs`, `...WriteProperties.{Double,Float}.cs` | The finite check before the dispatch point lets NaN and infinity through in block style (`ValidateFiniteUnlessBlock`), and `WriteFloatingPointConstant` writes them as numbers in block style. | Decision 8. The style is tested only when the value is not finite. |
| `Writer/Utf8YamlWriter.WriteValues.Raw.cs`, `...WriteValues.StringSegment.cs` | A block branch in `WriteRawValueCore` and the sequence overload; the segment epilogue writes the root's line break in block style. | Strings written in segments and raw values in block style. |
| `Writer/YamlWriterOptions.cs` | `Style` and `IndentSequences` in `_optionsMask` (bits 16 to 18); `IndentedOrNotSkipValidation` includes the block bits. | The block style's options. |
| `BitStack.cs` | `PeekParent()`. | The parent's kind, for the compact form. |
| `YamlConstants.cs` | `MaxImplicitKeyLength` (1024). | YAML 1.2's limit on implicit keys. |
| `Reader/Utf8YamlReader.TryGet.cs` | Two `Debug.Assert`s that a value without escapes has no backslash now hold for JSON and double-quoted values only. | A plain scalar may hold a backslash (`C:\x`); found by M4a's tests in Debug builds. No release-build change. |
| `Serialization/YamlSerializerOptions.cs`, `...Caching.cs` | `WriteStyle` and `IndentSequences` (field, property, copy constructor, writer options, equality); `GetWriterOptionsForDocument`; block style in `GetWriterOptionsForYamlLines`. | What the serializer reaches of the block style. |
| `Serialization/YamlSerializer.Write.{Document,Element,Node}.cs`, `YamlSerializer.Read.Node.cs`, `Nodes/YamlValue.cs`, `Nodes/YamlValueOfTCustomized.cs`, `Writer/Utf8YamlWriterCache.cs` | The write-then-parse paths ask for JSON-compatible style (`RentWriterForDocument`, `PinnedDocumentOptions`). | The DOM reads JSON-shaped YAML only until M5. |
| `Serialization/YamlSerializer.Write.Pipe.cs`, `Serialization/Metadata/YamlTypeInfoOfT.WriteHelpers.cs` | `---` between top-level values (a line feed only under the JSON grammar switch); no extra line feed after a block value. | Decision 1: a YAML stream. |
| `Serialization/Metadata/YamlPropertyInfo.cs`, `...YamlPropertyInfoOfT.cs` | `BlockNameSection`, computed with `EscapedNameSection`; the writer gets the property. | The block name section. |
| `Serialization/Converters/Value/{Ieee754FloatingPoint,Half}Converter.cs` | NaN and infinity go to `WriteNumberValue(double)` in block style. | Decision 8. |
| `Serialization/Converters/Value/{TimeSpan,TimeOnly,Version}Converter.cs`, `DateOnlyConverter.cs` | In block style, a double-quoted hint before the value or key; `DateOnly` written plain. | The typed forms and decision 2b. |
| `Resources/Strings.resx`, `ThrowHelper.cs` | The block-style messages. | |

### Tests

| File | Change | Why |
|---|---|---|
| `test/Common/PropertyVisibilityTests.InitOnly.cs`, `test/Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` (2 sites) | `#if NET` → `#if NET11_0_OR_GREATER` around the `System.Numerics.Colors.Argb<float>` test/registrations. | `System.Numerics.Colors` is new in .NET 11; upstream's only .NET test leg is net11.0, so `#if NET` meant net11 there. Needed for the added net10.0 leg; net11.0 and net481 behaviour unchanged. Listed in `test/EXCLUSIONS.md`. |
| `test/Styml.SourceGeneration.Unit.Tests/CompilationHelper.cs` | On net10.0 only (`#elif !NET11_0_OR_GREATER`), the in-memory test compilations get the `IUnion` / `UnionAttribute` declarations, as upstream already does for .NET Framework. | The compilations reference the running framework; .NET 10 lacks the union types, so 24 union generator tests could not see union types on the added net10.0 leg. |
| `test/Styml.SourceGeneration.Unit.Tests/CompilationHelper.cs` | On .NET Framework only (`#if !NET`): (1) the test compilations also reference the `netstandard.dll` facade; (2) warning CS1702 is suppressed in their compilation options. | First-time combination "net481 tests → netstandard2.0 library" (upstream binds net481 tests to its net462 asset). (1) Without the facade every compilation fails with CS0012 (169 failures). (2) The netstandard2.0 assets of System.Memory / System.Buffers / System.Threading.Tasks.Extensions carry lower assembly versions than the net462 assets loaded in the test process (e.g. 4.0.2.0 vs 4.0.5.0); Roslyn reports the unification as CS1702, which the tests' "no warnings" checks rejected (67 failures). Generator diagnostics are still validated. |
| `test/TestShims/JsonSchemaNetBridge.cs` (new) | Extension `Evaluate(this JsonSchema, YamlNode?, EvaluationOptions?)` that hands instances to JsonSchema.Net as JSON text. Test sources unchanged (overload resolution falls back to the extension). | JsonSchema.Net is built on the real `System.Text.Json` (`JsonNode`); after the rename the schema-exporter tests hold `YamlNode`s. The library still writes JSON grammar at this stage. |

### Tests: the YAML default (M3b)

The imported projects run the YAML grammar by default since M3b. Tests that only hold for the JSON grammar get
`[Trait("category", "jsongrammar")]` (decision D1); `test/EXCLUSIONS.md` lists each one with
its reason category. The only source change in these files is the added trait, plus four theory splits that move
JSON-comment rows to a tagged twin calling the original method. No assertion changed. M3d added 7 traits (partial
data at a collection that may be a key). Besides traits, M3d added one line to `Styml.Tests/Serialization/CacheTests.cs`:
`DisallowImplicitCollectionKeys` in `GetPropertiesWithSettersAndNonDefaultValues`, the list upstream's guard requires
for every settable `YamlSerializerOptions` property (its failure message says to update it), so the options
equality comparer is tested on the new property too. M4a added two more lines there, `WriteStyle` and `IndentSequences`, for the same reason (each later step that added a settable option added its line too; M7b's is `DefaultScalarStyle`, M7c's `IgnoreSourceStyles`, and M7c removed `IgnoreNullValues`' with the property), and changed the traits of
`Common/AsyncEnumerableTests.cs` (`test/EXCLUSIONS.md`, "JSON Lines output"). The three csprojs lost
`StymlTestJsonGrammar` (ours).

M3c removed 64 traits and changed nothing else in these files (`git diff 1685855 HEAD` over the imported test
directories is those 64 lines): 55 methods that pass every row under YAML once multi-segment input reads, and the
class-level traits of the nine Pipe wrappers, whose remaining failures all belong to methods with their own trait.
The column below gives the count at M3c's end; `test/EXCLUSIONS.md` has the detail.

| File | Traits (at the end of M3c) | Other change |
|---|---:|---|
| `Common/AsyncEnumerableTests.cs` | 13 | M3d: 1 more (partial data at a collection that may be a key); M4a: 15 more (JSON Lines output, one of them the twin of a theory split: its `topLevelValues: true` row) and 5 removed (round trips that read `---`), 23 in all |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | 1 |  |
| `Common/CollectionTests/CollectionTests.Specialized.Read.cs` | 1 |  |
| `Common/ConstructorTests/ConstructorTests.Exceptions.cs` | 1 |  |
| `Common/ConstructorTests/ConstructorTests.Stream.cs` | 1 |  |
| `Common/ExtensionDataTests.cs` | 1 |  |
| `Common/NumberHandlingTests.cs` | 2 |  |
| `Common/PolymorphicTests.cs` | 1 |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | 6 |  |
| `Common/StructuralYamlTypeClassifierTests.cs` | 0 | M3c: 1 removed (multi-segment input) |
| `Common/YamlSchemaExporterTests.cs` | 1 | 1 theory split: MemberData filtered into `GetTestDataWithoutJsonComments` / `GetTestDataWithJsonComments`, tagged twin |
| `Styml.Tests/NewtonsoftTests/YamlSerializerTests.cs` | 2 |  |
| `Styml.Tests/Serialization/Array.ReadTests.cs` | 1 |  |
| `Styml.Tests/Serialization/AsyncEnumerableTests.cs` | 0 | M3c: the class-level trait on the Pipe wrapper removed |
| `Styml.Tests/Serialization/CollectionTests.cs` | 0 | M3c: the class-level traits on the two Pipe wrappers removed |
| `Styml.Tests/Serialization/ConstructorTests.cs` | 0 | M3c: the class-level `[Xunit.Trait]` on the Pipe wrapper removed |
| `Styml.Tests/Serialization/ContinuationTests.cs` | 2 | M3c: 1 removed (multi-segment input) |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.List.cs` | 1 |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | 1 |  |
| `Styml.Tests/Serialization/ExceptionTests.cs` | 4 |  |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | 4 | 2 theories split (comment rows to `_JsonComments` twins) |
| `Styml.Tests/Serialization/OptionsTests.cs` | 3 |  |
| `Styml.Tests/Serialization/PipeTests.cs` | 0 | M3c: the two class-level `[Xunit.Trait]`s removed |
| `Styml.Tests/Serialization/ReadValueTests.cs` | 15 | M3d: 1 more (partial data at a collection that may be a key); M3c: 1 removed (multi-segment input) |
| `Styml.Tests/Serialization/ReferenceHandlerTests.cs` | 0 | M3c: the class-level traits on the two Pipe wrappers removed |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | 4 |  |
| `Styml.Tests/Serialization/StreamTests.cs` | 0 | M3c: the class-level `[Xunit.Trait]` on the Pipe wrapper removed |
| `Styml.Tests/Serialization/Value.ReadTests.cs` | 3 |  |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | 39 | M3c: 30 removed (multi-segment input) |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.Date.cs` | 2 |  |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | 6 | M3c: 4 removed (multi-segment input) |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | 1 | M3c: 10 removed (multi-segment input) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | 56 | M3d: 4 more (partial data at a collection that may be a key); M3c: 7 removed (multi-segment input) |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | 1 |  |
| `Styml.Tests/YamlDocumentTests.cs` | 20 | M3d: 1 more (partial data at a collection that may be a key) |
| `Styml.Tests/YamlElementParseTests.cs` | 7 | 1 theory split (comment row to a `_JsonComments` twin) |
| `Styml.Tests/YamlElementWriteTests.cs` | 11 |  |
| `Styml.Tests/YamlNode/ParseTests.cs` | 1 |  |
| `Styml.Tests/YamlNode/YamlNodeTests.cs` | 1 |  |
| `Styml.Tests/YamlNode/YamlValueTests.cs` | 0 | M3c: 1 removed (multi-segment input) |

### Tests: the JSON grammar retired (M7a)

M7a (decision D8) deleted the 237 methods that held only for the JSON
grammar (category `jsongrammar`) and the trait itself; `test/EXCLUSIONS.md` lists each one with its reason. 106
duplicate-key methods were retargeted instead: each read that used the default (System.Text.Json's last-wins, the
JSON grammar's) now passes `DuplicateKeyHandling = LastWins`, at the method's own options construction, through
`Serializer.CreateOptions(configure: …)`, or as `YamlTestSerializerOptions.LastWins` / `LastWinsDocument` (new in
`test/Common/YamlTestSerializerOptions.cs`, used by these methods only). Two private helpers whose every caller is a
retarget (`AssertDuplicateProperty<T>` in `Styml.Tests/Serialization/YamlDocumentTests.cs` and `YamlElementTests.cs`)
pass it in their no-throw read; the one in `Styml.Tests/YamlDocumentTests.cs`, which two other methods call too, gained
an optional `noThrowOptions` parameter that only the retargets pass. No assertion changed. The retargeted methods:

| File | Methods |
|---|---|
| `test/Common/CollectionTests/CollectionTests.Dictionary.cs` | `DeserializeBuiltInDictionaryWithDuplicatePropertiesThrow`, `DeserializeDictionaryWithDuplicateKeys`, `DeserializeDictionaryWithDuplicateProperties`, `DeserializeNestedDictionaryWithDuplicatePropertiesThrow` |
| `test/Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `LastParameterWins`, `LastParameterWins_DoesNotGoToExtensionData`, `NumerousPropertiesWork`, `RespectAllowDuplicateProp_DuplicateCtorParam`, `RespectAllowDuplicateProp_Large`, `RespectAllowDuplicateProp_Small` |
| `test/Common/ConstructorTests/ConstructorTests.Stream.cs` | `ExerciseStreamCodePaths`, `ReadSimpleObjectAsync` |
| `test/Common/ExtensionDataTests.cs` | `EmptyPropertyNameInExtensionData`, `ExtensionPropertyDuplicateNames`, `ExtensionProperty_DuplicatesThrow` |
| `test/Common/PropertyNameTests.cs` | `DuplicatesByKebabCaseNamingPolicy`, `DuplicatesByKebabCaseNamingPolicyIgnoreCase`, `DuplicatesIgnoreCase` |
| `test/Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_LastOneWins` |
| `test/Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `DuplicateNonReferenceProperties_Preserve` |
| `test/Common/RequiredKeywordTests.cs` | `RequiredPropertyOccuringTwiceInThePayloadWorksAsExpected` |
| `test/Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes` |
| `test/Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_StructListOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_ListOfYamlElement_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_StructListOfYamlElement_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_IList_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes_NullInBetween`, `CreationHandlingSetWithAttribute_CanPopulate_StructCollectionOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_StructListOfInt_PropertyOccurringMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulate_StructSetOfInt_PropertyOccurringMultipleTimes` |
| `test/Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateAndSerialize_ClassWithEnabledPolymorphismOnSerialization`, `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_Callbacks`, `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithFieldAndIgnoreReadOnlyFields`, `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyFieldAndIgnoreReadOnlyProperties`, `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyPropertyAndIgnoreReadOnlyFields`, `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithWritablePropertyAndIgnoreReadOnlyProperties`, `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithLargeParametrizedCtor_PropertyOccurredMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithSmallParametrizedCtor_PropertyOccurredMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClass_PropertyOccurredMultipleTimes`, `CreationHandlingSetWithAttribute_CanPopulateWritableProperty_SimpleStruct_PropertyOccurredMultipleTimes`, `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithLargeParametrizedCtor_DeserializingNull`, `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithSmallParametrizedCtor_DeserializingNull`, `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClass_DeserializingNull`, `CreationHandlingSetWithOptions_CanPopulateOrSerialize_RequiredPropertiesRecursive` |
| `test/Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadNestedObjectWithDuplicateProperties`, `ReadSimpleObjectWithDuplicateProperties` |
| `test/Styml.Tests/Serialization/YamlDocumentTests.cs` | `DserializeYamlDocumentArrayWithDuplicateProperties`, `DserializeYamlDocumentClassWithDuplicateProperties`, `DserializeYamlDocumentDeeplyNestedWithDuplicateProperties`, `DserializeYamlDocumentWithDuplicateProperties` |
| `test/Styml.Tests/Serialization/YamlElementTests.cs` | `DeepEquals_EqualValuesReturnTrue`, `DeepEquals_NotEqualValuesReturnFalse`, `DserializeYamlElementArrayWithDuplicateProperties`, `DserializeYamlElementClassWithDuplicateProperties`, `DserializeYamlElementDeeplyNestedWithDuplicateProperties`, `DserializeYamlElementWithDuplicateProperties` |
| `test/Styml.Tests/YamlDocumentTests.cs` | `GetPropertyFindsLast`, `GetPropertyFindsLast_WithEscaping`, `ParseYamlDocumentArrayWithDuplicateProperties`, `ParseYamlDocumentClassWithDuplicateProperties`, `ParseYamlDocumentDeeplyNestedWithDuplicateProperties`, `ParseYamlDocumentEscapeWithDuplicateProperties`, `ParseYamlDocumentObjectWithDuplicateProperties`, `ParseYamlDocumentWithDuplicateProperties` |
| `test/Styml.Tests/YamlNode/YamlObjectTests.cs` | `YamlObject_ExtensionData_ManyDuplicatePayloads` |

`test/Directory.Build.targets`, `test/TestShims/GrammarModeInitializer.cs` and the two YAML-only csprojs lost the
grammar switch; `Styml.Yaml.Tests/GrammarModeTests.cs` checks that setting the deleted switch does nothing.

### Tests: the obsolete APIs removed (M7c)

M7c removes `YamlSerializerOptions.IgnoreNullValues`,
`YamlSerializerOptions.AddContext<TContext>()` and the exception serialization members. `test/EXCLUSIONS.md` ("M7c: the
obsolete APIs removed") lists every change with its reason. Counted by source method: **23 retargeted, 14 methods
retired, 7 row sets retired, 8 parts retired**. No assertion changed outside the retired code.
- **Retargets:** a test that used `IgnoreNullValues` only to leave nulls out on write now sets
  `DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull`, its option line alone changed (13). One that bound a
  context with `AddContext<T>()` now calls `new T(options);` (10).
- **Retirements:** a test of either API itself, or of the option's read-side skip of null tokens, which
  `WhenWritingNull` does not have, is retired. That is a method, a theory's `true` rows (`ContinuationTests`' theory
  dimension and the `bool` parameter of three `RequiredKeywordTests` theories, now `[Fact]`s), or a block inside a
  larger test.
- **No test used the exception serialization members.** The `SerializationInfo` fixtures in `ExceptionTests` and
  `UnsupportedTypesTests` test the serializer's refusal of that .NET type and stay.
- **The two guard lists:** the `IgnoreNullValues` line in `CacheTests.GetPropertiesWithSettersAndNonDefaultValues`
  and the clause in `OptionsTests.GetFullyPopulatedOptionsInstance` are gone.
- **M7a's list above** still names `YamlIgnoreCondition_LastOneWins` among its retargets, as the record; M7c retired
  it.

| File | Retargeted | Retired methods | Retired row sets | Retired parts |
|---|---:|---:|---:|---:|
| `test/Styml.SourceGeneration.Tests/SerializationLogicTests.cs` | | | 1 (with its `#pragma` pair) | |
| `test/Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | 1 | | | |
| `test/Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | 1 | | | |
| `test/Styml.Tests/Serialization/CacheTests.cs` | | | | (guard line) |
| `test/Styml.Tests/Serialization/ContinuationTests.cs` | | | 2 | |
| `test/Styml.Tests/Serialization/ContinuationTests.NullToken.cs` | | 1 | | |
| `test/Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | | 1 | | |
| `test/Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | 1 | | | |
| `test/Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | 1 | 1 | | |
| `test/Styml.Tests/Serialization/Null.ReadTests.cs` | | 1 | | 1 |
| `test/Styml.Tests/Serialization/Null.WriteTests.cs` | 2 | | | |
| `test/Styml.Tests/Serialization/OptionsTests.cs` | 6 | 2 | | 2 (and the guard clause) |
| `test/Styml.Tests/Serialization/Stream.WriteTests.cs` | 3 | | | |
| `test/Styml.Tests/Serialization/YamlSerializerApiValidation.cs` | 1 | | | |
| `test/Common/CollectionTests/CollectionTests.Dictionary.cs` | 2 | | | |
| `test/Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | | 1 | | 1 |
| `test/Common/ExtensionDataTests.cs` | | | | 1 |
| `test/Common/MetadataTests.Options.cs` | 1 | 2 | | 1 |
| `test/Common/PolymorphicTests.cs` | 1 | | | |
| `test/Common/PropertyVisibilityTests.cs` | 2 | 5 | 1 | 2 |
| `test/Common/RequiredKeywordTests.cs` | | | 3 | |
| `test/Common/UnsupportedTypesTests.cs` | 1 | | | |
| **Total** | **23** | **14** | **7** | **8** |

`test/Styml.Tests/Styml.Tests.csproj` and `test/Styml.SourceGeneration.Tests/Styml.SourceGeneration.Tests.csproj` still
carry their `NoWarn` for SYSLIB0020 and SYSLIB0049, which now suppress nothing in the tests.

### Product docs and messages: "JSON" where it meant the input format (M7c)

M7c changed the inherited wording by a script, `eng/text/json-wording.py`, not by hand:
- **The rule:** "JSON" stays where it names JSON itself (the JSON-compatible style, `Schema = Json` and the JSON schema,
  JSON schema export, JSON Lines, JSONPath, a JSON source, JSON's own escapes and numbers, the deleted JSON grammar), and
  otherwise becomes "YAML". A line that already says "YAML" or carries a step tag (M2 to M7) is Styml's own wording and
  is left alone. The references to RFC 8259 are reworded to the YAML 1.2 specification first.
- **What changed:**
  - 1,167 `///` lines in 128 files of `src/Styml` and `src/Common`; 71 occurrences in 30 lines kept;
  - 48 of the 56 `Strings.resx` values that said JSON (the 7 kept name the JSON schema, the JSON-compatible style or the
    schema exporter; `SpecialNumberValuesNotSupported` is thrown by the JSON-compatible writer only); the dead
    `YamlDocumentNotSupportedForYamlGrammar` and its throw helper are removed;
  - the three trimming-message constants (`YAML serialization and deserialization might require ...`).
- **Tests with a changed message's text,** edited by the same script: `Common/PolymorphicTests.CustomTypeHierarchies.cs`
  (one assertion), `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` (a comment),
  `Styml.Tests/Serialization/WriteValueTests.cs` (a comment), `Styml.Tests/YamlDocumentTests.cs` (one constant) and
  `Common/StructuralYamlTypeClassifierTests.cs` (two assertions that hold only the part from "JSON" on, found by M7c's
  gate and edited by the script's `--base` pass). No other assertion holds message text; the generator's own diagnostics (its resources) are unchanged, so its tests are
  too. The gate is M7c's per-method diff: no outcome changed by the rename.
- **Not changed:** `//` comments, and `PACKAGE.md`, which M7e rewrites.

### Build / packaging differences from upstream

- **Package sources**: the repo has no `NuGet.config` and uses the environment's default sources (CI names its
  sources on the restore command line, `docs/release.md`). Earlier it had one (since M2, `72237c4`) that cleared the sources and mapped them to nuget.org and dnceng `dotnet-eng`,
  which conflicted with the environment's. The environment's proxy feed serves nuget.org's packages but not
  `Microsoft.DotNet.XUnitExtensions`, `Microsoft.DotNet.RemoteExecutor` or `Microsoft.DotNet.XUnitAssert` (404);
  they restore here from the global packages cache, and a clean machine needs the environment to provide them.

- **Assembly identity**: `Styml` / `Styml.SourceGeneration` (upstream `System.Text.Json`,
  `System.Text.Json.SourceGeneration.Roslyn4.4`); no strong-name signing; Arcade's `CLSCompliant(true)` added
  explicitly.
- **Target frameworks**: library `net11.0;net10.0;netstandard2.0` (no `net462`, no `ref/` project); tests
  `net11.0;net10.0;net481` (upstream tests: `net11.0;net481`). `net481` tests run the **netstandard2.0** library
  (upstream they bind to the `net462` asset).
- **Dependencies**: netstandard2.0 uses packages instead of upstream's live-built ProjectReferences
  (`System.Text.Encodings.Web`, `System.IO.Pipelines`, `Microsoft.Bcl.AsyncInterfaces` 11.0.0; `System.Buffers`,
  `System.Memory`, `System.Threading.Tasks.Extensions`, `System.Runtime.CompilerServices.Unsafe` at the tag's
  versions). **net10.0 also references `System.Text.Encodings.Web` and `System.IO.Pipelines` 11.0.0**, as
  upstream does on every TFM ("Application tfms need to use the same or higher version of .NETStandard's
  dependencies"): without it, a netstandard2.0 library compiled against Styml fails to load
  `System.Text.Encodings.Web 11.0.0.0` on a net10.0 app (seen in `NETStandardContextTests`). `System.ValueTuple`
  (upstream: .NET Framework asset only) is not needed.
- **Source generator**: one netstandard2.0 project, Roslyn 4.4 code path (`ROSLYN4_0_OR_GREATER;ROSLYN4_4_OR_GREATER`),
  compiled against `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.4.0 (upstream's API floor for this variant; a
  newer reference would stop the generator loading in the 4.8-based generator unit tests and in older
  compilers). Upstream also builds it for `$(NetCoreAppToolCurrent)` only to get nullable analysis; not done.
- **String resources**: upstream's Arcade `GenerateResxSource` is replaced by `eng/Resources.targets`, an inline
  MSBuild task that emits the same shape (`internal static partial class System.SR` + the `FxResources.<name>.SR`
  marker type, no `GetResourceString`), paired with the vendored `SR.cs`. `Microsoft.CodeAnalysis.ResxSourceGenerator`
  (suggested by the plan) was tried first but cannot emit the marker type that the generator references via
  `typeof(FxResources.System.Text.Yaml.SourceGeneration.SR)`. Manifest names: `FxResources.Styml.SR`,
  `FxResources.System.Text.Yaml.SourceGeneration.SR` (generator keeps upstream's `FxResources.$(RootNamespace).SR`).
- **Feature switch**: `buildTransitive/Styml.targets` maps `YamlSerializerIsReflectionEnabledByDefault` to the
  `System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault` runtime option (the SDK does this only for the
  real STJ). There is no `ILLink.Substitutions.xml` at the tag to embed.
- **Not wired**: ILLink descriptors, trimming/AOT test infrastructure, Xliff, Arcade analyzers, API compat, the runtime-async compiler feature (upstream enables it only for non-packable
  shared-framework libraries, so STJ never had it).
- **Warnings**: `Styml.csproj` also suppresses CS0282 (fields of the partial `Utf8YamlReader` struct declared in
  more than one file), since M3 puts the reader's YAML fields in `Utf8YamlReader.Yaml.cs`.
- **Compiler**: the stock SDK 11.0.100 compiler; no pinned `Microsoft.Net.Compilers.Toolset` was needed.
- **Superseded upstream project files**: the renamed upstream `*.csproj` / `*.targets` are removed in the M2
  commit and replaced by `Styml*.csproj`; their `Compile` item groups are copied verbatim (paths repointed at
  `src/Vendored` / `test/Vendored`), checked item-for-item.

## Infrastructure written for this repo

- `global.json` (SDK 11.0.100, `latestFeature`), `Directory.Packages.props` (versions from the tag's `eng/Versions.props`,
  `eng/Version.Details.props` and Arcade 11.0.0-beta.26465.108 `DefaultVersions.props`, all confirmed by restore).
- `Directory.Build.props` / `.targets`: upstream build settings and the files runtime infrastructure injects
  (nullable attributes, span polyfills, internal `ObsoleteAttribute`, `SkipLocalsInit`, `SR`).
- `test/TestShims/TestUtilities.csproj`: stand-in for upstream `TestUtilities` (separate assembly, as upstream):
  vendored `AssertExtensions` / `ThreadCultureChange` / `LineEndingsHelper` + a `PlatformDetection` subset.
- Test mode initializer (M3a; since M7a the writer's only): `test/TestShims/GrammarModeInitializer.cs`, compiled into
  every test project by `test/Directory.Build.targets`, sets the writer's default style for the process
  (`STYML_TEST_WRITER=json` is leg YJ) in a module initializer and checks it, and checks that the reader reads YAML.
  Until M7a it also set the D1 reader switch (`STYML_TEST_GRAMMAR`, leg J), deleted with the JSON grammar.
- `test/Styml.Parity.Tests` (net11.0, net10.0): the rows of `docs/feature-parity.md` asserted for YamlDotNet
  18.1.0, SharpYaml 3.17.0 and Styml (both packages pinned in `Directory.Packages.props`).
- `test/Styml.Yaml.Tests`: the M3 tests (yaml-test-suite adapter and lists, JSON equivalence against the real
  `System.Text.Json`, chunking, typed reads, serializer, positions, behaviour, public API). `System.Text.Json`
  11.0.0, the JSON oracle, is the in-box assembly on net11.0 and the package on net10.0 and net481.
- `samples/RoundTrip`: console smoke test (reflection + source-generated round trip next to in-box STJ).

## Building and testing

```bash
dotnet build -c Release                                                          # all projects, all TFMs
dotnet test -c Release -f net11.0                                                # the default filter below applies
dotnet test -c Release -f net10.0
# The JSON-compatible writer default (leg YJ), category=jsonoutput included:
STYML_TEST_WRITER=json dotnet test -c Release -f net11.0
```

A bare `dotnet test` applies the filter through `VSTestTestCaseFilter` (`test/Directory.Build.targets`):
`category!=OuterLoop&category!=failing&category!=jsonoutput`, without the last part when `STYML_TEST_WRITER=json`.
A `--filter` on the command line replaces it. (Until M7a, `category!=jsongrammar` was excluded too, and
`STYML_TEST_GRAMMAR=json` ran the JSON grammar.)

Test scripts (`eng/test/`):

| Script | Purpose |
|---|---|
| `run-tests.sh <tfm> [csproj...]` | Build and run test projects for one TFM with upstream's filter; trx files to `artifacts/testresults` |
| `run-net481-remote.sh <Project>...` | Build the net481 leg here, copy it to a Windows machine over ssh (`STYML_REMOTE_HOST`, required), run it there with `dotnet test`, copy the trx and log back |
| `trx-summary.py <dir>` | Discovered / passed / failed / skipped table per TFM per project |
| `trx-compare.py <old.trx> <new.trx>` | Same tests? Compares per test method (names without arguments) and outcome counts |
| `trx-failures.py <trx>...` | Failures grouped by test method, with the first message |
| `reconcile-tests.py <csproj> <trx>` | Every test method in the compiled sources vs. what ran, with the attribute or `#if` explaining each one that did not |
| `vendor-yaml-test-suite.sh [data-tag] [source-tag]` | Refresh `test/yaml-test-suite` |
| `json-corpus/extract-json-corpus.cs` | Regenerate `test/Styml.Yaml.Tests/JsonCorpus/corpus.json` (the JSON-equivalence corpus) from the imported tests: `dotnet run eng/test/json-corpus/extract-json-corpus.cs` |
| `run-tests.sh` / `run-net481-remote.sh` with `STYML_TEST_WRITER=json` | Leg YJ: the JSON-compatible writer default, `category=jsonoutput` included, results in `artifacts/testresults-yj` |
| `m5-gate.sh <run> [legs]` | Every target built, then legs Y and YJ (the default) of the five test projects, results in `artifacts/m5/<run>` |
| `trx-diff.py <before run> <after run>` | The per-method gate (M7a): each method's outcome in two `m5-gate.sh` runs, with the expected new and gone methods |
| `public-api/` | List the public API of a `Styml.dll`; produced the M2b baselines of `test/Styml.Yaml.Tests/PublicApi` (see its README) |

The filter is upstream's default (`eng/testing/tests.targets`) plus `category!=jsonoutput` outside leg YJ (M4c; from
M3b to M7a also `category!=jsongrammar`, the tests retired with the JSON grammar, `test/EXCLUSIONS.md`). Without the
upstream part, `[OuterLoop]` and `[ActiveIssue]` (category `failing`) tests also run, and some of those fail as they
do upstream. The `net481` leg builds on any OS
(`dotnet build -c Release -f net481` per test project) and runs on Windows: copy each
`test/<Project>/bin/Release/net481` folder and run `dotnet test <Project>.dll --filter ...` there, with the same filter (and `STYML_TEST_WRITER` set in that
environment for leg YJ); `eng/test/run-net481-remote.sh` does all of this.
