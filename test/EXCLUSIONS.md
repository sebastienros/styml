# Test exclusions

Every upstream test, file or project that is not imported, not built, disabled or skipped **beyond what
upstream already skips**, with a one-line reason. Upstream's own `[ActiveIssue]`, `[SkipOnTargetFramework]`,
`[OuterLoop]`, `ConditionalFact` conditions etc. are kept as-is and are not listed.

## Projects / directories not imported

| Upstream | Reason |
|---|---|
| `tests/System.Text.Json.FSharp.Tests` (7 `.fs` files, 1 `.fsproj`) | F# test project; out of scope for a C#-only repo. FSharp.Core support code in the library is still compiled and is exercised only indirectly. |
| `tests/System.Text.Json.SourceGeneration.TestLibrary/System.Text.Json.TestLibrary.Roslyn3.11.csproj` | Single Roslyn 4.4 generator; the Roslyn 3.11 generator variant is not built. |
| `tests/System.Text.Json.SourceGeneration.Tests/System.Text.Json.SourceGeneration.Roslyn3.11.Tests.csproj` (+ `roslyn3.11.rd.xml`) | Same sources as the Roslyn 4.4 project, against the Roslyn 3.11 generator that is not built. |
| `tests/System.Text.Json.SourceGeneration.Unit.Tests/System.Text.Json.SourceGeneration.Roslyn3.11.Unit.Tests.csproj` | Same sources as the Roslyn 4.4 unit tests, against the Roslyn 3.11 generator that is not built. |
| `tests/System.Text.Json.Tests/TrimmingTests` | ILLink/NativeAOT trimming test apps driven by runtime-repo infrastructure (`TrimmingTests` targets); trimming/AOT validation is a later milestone (M7). |

## Files imported but not compiled

| File | Reason |
|---|---|
| `test/Styml.Tests/YamlNamingPolicyTests.cs` (upstream `JsonNamingPolicyTests.cs`) | Not in upstream's `System.Text.Json.Tests.csproj` either (dead file at the tag); kept verbatim, not added. |
| `test/Styml.Tests/ILLink.Descriptors.xml`, `test/Styml.SourceGeneration.Tests/roslyn4.4.rd.xml` | Trimming / NativeAOT descriptors used only by runtime-repo AOT test infrastructure. |

## Tests compiled out on a test leg upstream does not have

Upstream runs these tests on `net11.0` and `net481` only; `net10.0` is an added leg.

| Test | Leg | Reason |
|---|---|---|
| `PropertyVisibilityTests.GenericColor_InitOnlyProperties_CanRoundtrip` (`test/Common/PropertyVisibilityTests.InitOnly.cs`, all serializer wrappers in both `Styml.Tests` and `Styml.SourceGeneration.Tests`), plus the `System.Numerics.Colors.Argb<float>` `[YamlSerializable]` registrations in `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | net10.0 | Uses `System.Numerics.Colors`, new in .NET 11. Upstream's `#if NET` became `#if NET11_0_OR_GREATER`; still runs on net11.0. |

Files upstream already restricts to net11.0 (`IsTargetFrameworkCompatible(net11.0)` item groups:
`Ieee754NumericConverterTests.cs` in both serializer test projects) are not compiled on net10.0 or net481,
exactly as upstream does not compile them on net481. That is why net10.0 discovers fewer tests than net11.0.

## Disabled or skipped tests

None. No test is skipped, disabled or weakened beyond upstream's own conditions and the default
`category!=OuterLoop&category!=failing` filter. The tests that held only for the JSON grammar were retired with it in
M7a (next section), each listed with its reason; the 106 duplicate-key methods that an option makes valid under the
YAML grammar were retargeted and run. The tests of the inherited obsolete APIs were retired with them in M7c
("M7c: the obsolete APIs removed", below), each listed with its reason.

Reconciliation (net11.0, done at M2 with the JSON grammar; the JSON-grammar run of M3b executes the same tests
plus the four `_JsonComments` twins of the split theories): every `[Fact]`/`[Theory]`/`[ConditionalFact]`/
`[ConditionalTheory]` method in the compiled sources was matched by name against the executed tests in the trx files. All methods that did not run
are explained by upstream's own attributes or `#if`s:

| Project | Methods not executed | Explained by |
|---|---:|---|
| Styml.Tests | 45 | `[OuterLoop]` (40, some also `[ActiveIssue]`/`[PlatformSpecific]`/`[SkipOnCoreClr]`), unconditional upstream `[ActiveIssue]` (4: dotnet/runtime#34779, #36605, #1780, #50205), `[SkipOnTargetFramework(Netcoreapp)]` (1) |
| Styml.SourceGeneration.Tests | 67 | `#if !BUILDING_SOURCE_GENERATOR_TESTS` (46), upstream `[ActiveIssue]` (18: unconditional, or conditioned on Browser / AppleMobile / MonoInterpreter / Interpreter, all false here), `[OuterLoop]`+`[ActiveIssue]` (2), `[SkipOnTargetFramework]` (1) |
| Styml.SourceGeneration.Unit.Tests | 0 | |

Conditional `[ActiveIssue]`/`ConditionalFact` conditions evaluate through `test/TestShims/PlatformDetection.cs`,
whose members are copied from upstream. On macOS arm64, `[ActiveIssue(..., IsOSX, IsX64Process)]`
(`Stream.Collections.cs`) is inactive, so that test runs.

## JSON comment tests restored

`//` and `/* */` are read again, as comments, when `CommentHandling` (`ReadCommentHandling` on the serializer's options)
is `Skip` or `Allow`: inside flow collections and at the document level, never inside a block collection, and never
with `Disallow`, the default, which stays standard YAML. Of the 74 methods retired as "JSON comment syntax", 63 run
again. They were taken from System.Text.Json's test sources at the import tag, renamed as the import is, and put back
where upstream has them. 41 are unchanged. The others, each named in the Note column of the table "JSON comment syntax"
below:

- 6 theories lost rows that hold for another retired category: `InvalidYamlWithComments` and its single-segment twin
  (6 rows each, invalid JSON that is valid YAML), and the four `ReadYamlTokenWithExtraValueAndComments*` theories
  (5 of 37 rows, partial data: YAML token boundaries), which take their rows from
  `YamlTokenWithExtraValueAndCommentsAfterAQuotedScalarOrACollection`.
- 4 methods lost what they assert about `Disallow`: `ReadCommentHandling`, `Parse_RespectsOptions`, and the two
  `YamlWithSingleLineCommentEndingWithNonStandardLineEnding*` theories, whose loop over the three values leaves it out.
- `ReadSimpleObjectWithTrailingTriviaAsync` (constructor tests) sets `DuplicateKeyHandling = LastWins`, as the
  duplicate-key methods retargeted in M7a.
- The 11 `YamlElementWriteTests` methods (also run by `YamlDocumentWriteTests`) are in category `jsonoutput`, as their
  siblings: they assert JSON output, and run in leg YJ.

The comment rows of three split theories are back in their original methods (`ReadSimpleClassIgnoresLeadingOrTrailingTrivia`
and `ReadComplexClassIgnoresLeadingOrTrailingTrivia`, 6 rows each; `YamlMarshal_GetRawUtf8Value_RootValue_ReturnsFullValue`,
1 row).

11 methods stay retired: 3 expect `Disallow` to reject a comment or to end a value at one
(`CommentsAreInvalidByDefault` and its single-segment twin, `TooMuchYamlWithComments`); 1 expects comment-only input to
be an error (`YamlContainingOnlyCommentsIsInvalid`, D4); 4 expect a strict token-type exception (D2); 2 read several values on one
line (D3); 1 has mostly rows that are valid YAML. The comment tests retired under other categories (the trailing-comma
option, multiple values with partial data) stay retired for those categories' reasons.

One difference from System.Text.Json is by design and has no retired test: inside a plain scalar that is not a JSON
number, `true`, `false` or `null`, a comment needs white space before it (`[a//b]` is the scalar `a//b`, where
System.Text.Json reports invalid JSON). `Styml.Yaml.Tests/JsonCommentTests.cs` covers that rule and the YAML edges.

## Tests retired with the JSON grammar (M7a, decision D8; category `jsongrammar` from M3b to M7a)

From M3b to M7a the imported projects ran the YAML grammar by default (decision D1),
and a test that held only for the JSON grammar carried `[Trait("category", "jsongrammar")]`, run only by leg J
(`STYML_TEST_GRAMMAR=json`). M7 deletes the JSON grammar:
M7a deleted it with leg J and the category:

| Category | Methods | M7a |
|---|---:|---|
| Duplicate keys | 112 | **106 retargeted**: they failed only on the YAML grammar's duplicate-key error, and now set `DuplicateKeyHandling = LastWins` (System.Text.Json's default) and run in legs Y and YJ. **6 retired**: the 3 with no success expected, and the 3 `YamlObject_DuplicatePropertyThrows*` methods, which assert System.Text.Json's deferred throw on a node read lazily, a path that only the JSON grammar's default reached. |
| JSON comment syntax | 74 | Retired in M7a: `//` and `/* */` are not YAML. **63 restored** since, when the reader learned JSON comments under `CommentHandling` `Skip` and `Allow` ("JSON comment tests restored", below); 11 still retired. |
| Invalid JSON that is valid YAML | 33 | Retired: the error they expect does not exist in YAML. |
| Error text, type or position on invalid JSON | 20 | Retired: the JSON lexer's messages and positions; the YAML reader's are covered by the yaml-test-suite's error cases. |
| Trailing-comma option | 19 | Retired: YAML flow collections always allow one (D3); the option is a documented no-op (owner question 5). |
| Multiple top-level JSON values (D3) | 19 | Retired: under YAML `AllowMultipleValues` means documents, tested since M5b. |
| Partial data: YAML token boundaries | 17 | Retired: the YAML reader waits for a token's end (M3c). |
| Strict token-type exceptions (D2) | 16 | Retired: D2, a plain scalar reads as any text type. |
| JSON Lines output (M4 decision 1) | 15 | Retired: the JSON Lines separator went with the grammar; top-level values are YAML documents. |
| Empty input (D4) | 8 | Retired: D4, empty input is zero tokens. |
| Object keys read as text (M5e) | 3 | Retired: M5e reads a scalar key into an `object` key as its text. |
| **Total** | **336** | **106 retargeted, 230 retired** (63 of them restored since) |

Also retired: the four `_JsonComments` twins of the split theories (below), and the 3 methods of the Styml-authored
`Styml.Tests/Serialization/LenientScalarsJsonGrammarTests.cs`, deleted whole (they checked that the lenient-scalar
option did nothing under the JSON grammar). 237 methods were deleted in all; the tables below keep every one, as the
record, and the duplicate-key table says which ran on. The per-method gate (`eng/test/trx-diff.py`, M7a's report)
compared legs Y and YJ before and after: every other method kept its outcome.

The rest of this section is the M3b to M5e record, as it stood.

Reasons, by category (one category per method):

| Category | What the test assumes |
|---|---|
| Multi-segment input | None since M3c: the YAML grammar reads multi-segment sequences. The category held 61 methods and 9 classes at M3d; the measurement that removed them is below. |
| JSON comment syntax | Its input uses `//` or `/* */`. Under YAML only `#` starts a comment, so these are content (or errors). |
| Trailing-comma option | It expects `AllowTrailingCommas = false` to reject a trailing comma; YAML flow collections always allow one (D3). |
| Invalid JSON that is valid YAML | It expects an error for input that is not JSON but is YAML (an unquoted word, a key without quotes, `01`, two scalars on a line). |
| Error text, type or position on invalid JSON | The input is invalid in both grammars, but the YAML grammar reports a different message, exception type or position, or fails at a different token. |
| Strict token-type exceptions (D2) | It expects a token-type exception, such as a number read as a string; under D2 a plain scalar reads as text. |
| Multiple top-level JSON values (D3) | It reads several top-level JSON values with `AllowMultipleValues`. Under YAML that option means several documents (`---`); values on one line or separated only by line breaks form one document or one multi-line scalar. |
| JSON Lines output (M4 decision 1) | It asserts the bytes of `SerializeAsyncEnumerable(..., topLevelValues: true)` as JSON Lines, a line feed after each value. Since M4a the serializer writes a YAML stream, `---` between top-level values, in both writer styles; the line feed alone remains only under the JSON grammar switch. |
| Empty input (D4) | It expects empty, whitespace-only or comment-only input to be an error; under D4 the reader yields no tokens, and since M5c the serializer reads zero tokens as a null. |
| Duplicate keys | Its input repeats a key inside one mapping and it expects System.Text.Json's default, which accepts it (last wins), or its exception for a refused repeat at another point. Since M5c the YAML grammar's default is `DuplicateKeyHandling.Error`; the JSON grammar's stays `LastWins`. |
| Object keys read as text (M5e) | It expects a scalar key into an `object` dictionary key to be refused (`NotSupportedException`). Since M5e the YAML grammar reads it as its `string`, as both libraries do (decided under the parity rule); the JSON grammar keeps the refusal. |
| Partial data: YAML token boundaries | With `isFinalBlock: false`, a plain or quoted scalar at the end of the data may continue (`true  x`) or turn out to be a key (`"a" :`), and since M3d a `[` or `{` at the root or in a flow sequence may turn out to be a collection key (`[a]: b`) until the rest of its line, or 1024 characters, is known; so the reader waits for more data where the JSON grammar returns the token. |

**M3c (2026-11-18).** The tags of the multi-segment category were removed by measurement, not by list.
- **The run:** at the M3c head, every test tagged `jsongrammar` in `Styml.Tests` and `Styml.SourceGeneration.Tests`
  ran under the YAML grammar (net11.0, `--filter category=jsongrammar`).
- **The 61 multi-segment methods:**
  - 55 passed every row and lost their tag;
  - 6 still failed, each only in its `isFinalBlock: false` rows: a lone quoted scalar or a root `[` at the end of
    non-final data waits under YAML, exactly as the same input does on a span. They moved to "Partial data: YAML
    token boundaries", with the note "moved from multi-segment in M3c". Their final rows were checked separately: they read,
    with `HasValueSequence` as upstream.
- **The 9 Pipe classes:** every failing test belongs to a method with its own permanent tag (comments, trailing
  commas, invalid JSON, error text, D3, token boundaries), or is upstream's `[ActiveIssue]`
  (`BindingBetweenRefProps`, category `failing`, excluded from every run). The class tags came off. The method tags
  keep those methods out of the default run.
- **The M3d pipe rows** (a root `[`/`{` waiting on partial data turned a one-segment pipe read into a multi-segment
  one) pass with default options: they were among the class-tagged tests.
- **The 55 "also multi-segment input" entries** still fail for their own reason; none passed. The note was removed
  from them, since multi-segment input is no longer a reason.
- **Effect:** the default run of `Styml.Tests` on net11.0 went from 40,849 to 46,556 tests, all passing.

(At M3b, the same kind of measurement, with a temporary change that copied a multi-segment sequence into one array,
had moved 27 multi-segment methods to permanent categories; two more moved in M3d. That left the 61.)

"Valid JSON, success expected" marks the methods whose input is valid JSON and whose expected outcome is success
(criterion 4 of M3b); for each of them the category, and the note where there is one, is its individual reason.
The marking follows the category and the method name; an error-category method is never one, nor is a method
whose input has comments, trailing commas or other non-JSON text. 37 methods are marked since M3c: 23 multiple-value
and 14 token-boundary (4 added in M3d, 2 moved from multi-segment in M3d review round 3, when they also started
waiting at a root `[`/`{` on partial data, and 6 moved from multi-segment in M3c). At M3d it was 86, 55 of them
multi-segment; 49 of those 55 lost their tag in M3c.

### Whole classes

None since M3c. Nine serializer wrapper classes that deserialize through a `PipeReader` were tagged as a whole until
M3c, since the YAML grammar refused the multi-segment sequences a pipe produces (D7). M3c removed the nine class
tags (measurement above):

| File | Class | Tests at M3d |
|---|---|---:|
| `Styml.Tests/Serialization/StreamTests.cs` | `PipeTests_Async` | 513 |
| `Styml.Tests/Serialization/ConstructorTests.cs` | `ConstructorTests_Pipe` | 372 |
| `Styml.Tests/Serialization/AsyncEnumerableTests.cs` | `AsyncEnumerableTests_AsyncPipeSerializer` | 147 |
| `Styml.Tests/Serialization/CollectionTests.cs` | `CollectionTestsDynamic_Pipe` | 682 |
| `Styml.Tests/Serialization/CollectionTests.cs` | `CollectionTestsDynamic_PipeWithSmallBuffer` | 682 |
| `Styml.Tests/Serialization/PipeTests.cs` | `PipeTests.PipeTests_Async` | 28 |
| `Styml.Tests/Serialization/PipeTests.cs` | `PipeTests.PipeTests_AsyncWithSmallBuffer` | 28 |
| `Styml.Tests/Serialization/ReferenceHandlerTests.cs` | `ReferenceHandlerTestsDynamic_IgnoreCycles_Pipe` | 27 |
| `Styml.Tests/Serialization/ReferenceHandlerTests.cs` | `ReferenceHandlerTestsDynamic_IgnoreCycles_PipeWithSmallBuffer` | 27 |

### Split theories

Rows with JSON comment syntax (and, since M4a, one JSON Lines row) moved to a tagged twin that calls the original method; the original keeps the other
rows, so they still run under YAML. Under the JSON grammar the two methods together run exactly the original rows.

| File | Original method | Tagged twin | Rows moved |
|---|---|---|---:|
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadSimpleClassIgnoresLeadingOrTrailingTrivia` | `..._JsonComments` (rows back in the original since JSON comments are read) | 6 of 11 |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadComplexClassIgnoresLeadingOrTrailingTrivia` | `..._JsonComments` (rows back in the original since JSON comments are read) | 6 of 11 |
| `Styml.Tests/YamlElementParseTests.cs` | `YamlMarshal_GetRawUtf8Value_RootValue_ReturnsFullValue` | `..._JsonComments` (row back in the original since JSON comments are read) | 1 of 11 |
| `Common/YamlSchemaExporterTests.cs` | `TestTypes_GeneratesExpectedYamlSchema` | `..._JsonComments` (rows whose expected schema contains `/*`) | 2 of 236 |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced` | `..._JsonLines` (M4a: the JSON Lines row) | 1 of 2 |

### Tagged methods

### Multi-segment input (0 methods since M3c)

No method is tagged for multi-segment input since M3c. The 61 methods of M3d: 6 moved to "Partial data: YAML token
boundaries" (below), and these 55 lost their tag and run in the default run:

| File | Test method | Valid JSON, success expected |
|---|---|---|
| `Common/StructuralYamlTypeClassifierTests.cs` | `StructuralClassifier_SegmentedEscapedPropertyNameIsClassified` | yes |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `ShouldWorkAtAnyPosition_Sequence` | yes |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `PrimitivesMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_NoYamlContent_ReturnsFalse_MultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_NonYamlTrailingData_ThrowsYamlException_MultiSegment` |  |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `CheckOnlyOneOfValueSpanOrSequenceIsSetSingleValue` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InitialStateMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InitialStateSimpleCtorMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadComplexNestedYamlMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadDeeplyNestedArraysMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadEscapedStringAcrossSegmentBoundary` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadLiteralsAcrossSegmentBoundaries` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadLongStringAcrossSegments` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadNumberWithExponentAcrossSegments` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadPropertyNameAcrossSegmentWithEscaping` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlWithNumberVariousSegmentSizes` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestMultiSegmentStringConversionToDateTime` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestMultiSegmentStringConversionToDateTimeOffset` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestPartialYamlReaderMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestSingleStringsMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestSingleStringsMultiSegmentByOne` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_ComplexArrayValue` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_ComplexObjectManyValues` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_ComplexObjectValue` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_SingleValue` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_SingleValueCommentsAllow` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_WithTrailingCommas` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTrySkipMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestYamlReaderLargeUtf8SegmentSizeOne` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestYamlReaderUtf8SegmentSizeOne` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithSingleLineCommentInTheMiddleOfThePayloadEndingWithNonStandardLineEndingMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithSingleLineCommentWithNoLineEndingsNonFinalBlockMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithUnfinishedMultiLineCommentNonFinalBlockMultiSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `CopyString_Utf16_DestinationTooShort_ThrowsArgumentException` |  |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `CopyString_Utf16_SuccessPath` | yes |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `CopyString_Utf8_DestinationTooShort_ThrowsArgumentException` |  |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `CopyString_Utf8_SuccessPath` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEquals` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsLargeMatch` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsLargeMismatch` |  |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsMismatchMultiSegment` |  |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsTooLargeToMatch` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsTooSmallToMatch` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsValue` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TextEqualDefault` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TextEqualsEscapedCharAtTheLastSegment` | yes |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TextMismatchSameLength` |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestYamlReaderUtf8` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestYamlReaderUtf8SpecialString` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ValueIsEscaped_AfterEscapedPropertyNames_IsFalseOnNonEscapedTokens` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ValueIsEscaped_AfterEscapedStrings_IsFalseOnNonEscapedTokens` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ValueIsEscaped_IsFalseOnTokensWithoutEscaping` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ValueIsEscaped_IsTrueOnEscapedPropertyNames` | yes |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ValueIsEscaped_IsTrueOnEscapedStrings` | yes |
| `Styml.Tests/YamlNode/YamlValueTests.cs` | `PrimitiveTypes_ReadOnlySequence` | yes |

### JSON comment syntax (74 methods: 63 restored, 11 still retired)

The Note column says what became of each method when JSON comments came back under `CommentHandling`
(`Skip` and `Allow`); "JSON comment tests restored", above, has the rules.

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/ConstructorTests/ConstructorTests.Stream.cs` | `ReadSimpleObjectWithTrailingTriviaAsync` |  | Restored, with `DuplicateKeyHandling = LastWins` as its sibling `ReadSimpleObjectAsync` |
| `Styml.Tests/NewtonsoftTests/YamlSerializerTests.cs` | `DeserializeCommentTestObjectWithComments` |  | Restored |
| `Styml.Tests/Serialization/Array.ReadTests.cs` | `ReadArrayWithInterleavedComments` |  | Restored |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | `VerifyConverterWithTrailingComments` |  | Restored |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadClassWithNestedComments` |  | Restored |
| `Styml.Tests/Serialization/OptionsTests.cs` | `ReadCommentHandling` |  | Restored without its two assertions that `Disallow` rejects `/* comment */` (it is text in YAML) |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderOptionsWinComments` |  | Restored |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoPropertyNameWithSkippedComments` |  | Still retired. Strict token-type exceptions (D2): it expects a number read into a string, or compared as text, to throw. |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoWithCommentBeforeNoneToken` |  | Still retired. Strict token-type exceptions (D2): it expects a number read into a string, or compared as text, to throw. |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoWithSkippedComments` |  | Still retired. Strict token-type exceptions (D2): it expects a number read into a string, or compared as text, to throw. |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `TooMuchYamlWithComments` |  | Still retired. It expects `Disallow`, the default, to reject a comment. In Styml `Disallow` is standard YAML, where `//` and `/* */` are text. It reads `0/**/` with a default reader and expects the number 0. |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | `ReadPrimitivesWithTrailingTriviaAsync` |  | Restored |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | `ReadSimpleObjectWithTrailingTriviaAsync` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowCommentStackMismatchMultiSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_CommentSeparated_MultiSegment` |  | Still retired. Multiple top-level JSON values (D3): values separated by a comment on one line; under YAML the option means documents. ; sibling `AllowMultipleValues_CommentSeparated` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `CheckOnlyOneOfValueSpanOrSequenceIsSet` |  | Restored ; row 2 has `//` comments, rows 0 and 1 need only M3c |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ConsumeSingleLineCommentMultiSpanTest` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadCommentsAcrossSegments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadInvalidYamlStringsWithComments` |  | Still retired. Invalid JSON that is valid YAML: 17 of its 29 rows expect an error for text that YAML reads (a plain scalar after a comment). |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlStringsWithComments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlTokenWithExtraValueAndCommentsAppendedMultiSegment` |  | Restored, 32 of 37 rows: in the 5 rows that start with a plain scalar the YAML reader, given partial data, waits for the end of the line ; sibling `ReadYamlTokenWithExtraValueAndCommentsAppended` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlTokenWithExtraValueAndCommentsMultiSegment` |  | Restored, 32 of 37 rows: in the 5 rows that start with a plain scalar the YAML reader, given partial data, waits for the end of the line ; sibling `ReadYamlTokenWithExtraValueAndComments` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `SkipSingleLineCommentMultiSpanTest` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_SingleValueWithComments` |  | Restored ; sibling `TestTokenStartIndex_SingleValueWithComments` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTrySkipWithCommentsMultiSegment` |  | Restored ; sibling `TestTrySkipWithComments` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithMultiLineCommentWithNoLineEndingsFinalBlockMultiSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithSingleLineCommentEndingWithNonStandardLineEndingMultiSegment` |  | Restored for `Skip` and `Allow`; the loop leaves `Disallow` out |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithSingleLineCommentWithNoLineEndingsFinalBlockMultiSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithSingleLineCommentWithRegularLineEndingMultiSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `TestGetCommentUnescape` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `TestingGetComment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.ValueTextEquals.cs` | `TestTextEqualsInvalid` |  | Still retired. Strict token-type exceptions (D2): it expects a number read into a string, or compared as text, to throw. |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `Allow` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowCommentStackMismatch` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowComment_ReadsCommentToken` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowMultipleValues_CommentSeparated` |  | Still retired. Multiple top-level JSON values (D3): values separated by a comment on one line; under YAML the option means documents. |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowSingleSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `CommentsAreInvalidByDefault` |  | Still retired. It expects `Disallow`, the default, to reject a comment. In Styml `Disallow` is standard YAML, where `//` and `/* */` are text. |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `CommentsAreInvalidByDefaultSingleSegment` |  | Still retired. It expects `Disallow`, the default, to reject a comment. In Styml `Disallow` is standard YAML, where `//` and `/* */` are text. |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ConsumeLotsOfComments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ConsumeSingleLineCommentSingleSpanTest` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYamlWithComments` |  | Restored, 48 of 54 rows: the 6 rows whose text after the comment is `漢字}` are valid YAML (a plain scalar) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYamlWithCommentsSingleSegment` |  | Restored, 44 of 50 rows: the 6 rows whose text after the comment is `漢字}` are valid YAML (a plain scalar) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ReadYamlTokenWithExtraValueAndComments` |  | Restored, 32 of 37 rows: in the 5 rows that start with a plain scalar the YAML reader, given partial data, waits for the end of the line |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ReadYamlTokenWithExtraValueAndCommentsAppended` |  | Restored, 32 of 37 rows: in the 5 rows that start with a plain scalar the YAML reader, given partial data, waits for the end of the line |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `Skip` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipAtCommentDoesNothing` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipComment_MultiLineComment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipComment_SingleLineComment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipLotsOfComments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipSingleLineCommentSingleSpanTest` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipSingleSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestTokenStartIndex_SingleValueWithComments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestTrySkipWithComments` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlContainingOnlyCommentsIsInvalid` |  | Still retired. Empty input (D4): comment-only input is zero tokens, not an error. |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithASingleCommentEndingWithLineEndingSingleSegment` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithSingleCorrectLineCommentWithPartialNonStandardLineEnding` |  | Restored |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithSingleLineCommentEndingWithNonStandardLineEnding` |  | Restored for `Skip` and `Allow`; the loop leaves `Disallow` out |
| `Styml.Tests/YamlDocumentTests.cs` | `ReadArrayWithComments` |  | Restored |
| `Styml.Tests/YamlDocumentTests.cs` | `ReadCommentHandlingWorksForNegativeOverflow` |  | Restored |
| `Styml.Tests/YamlElementParseTests.cs` | `Parse_RespectsOptions` |  | Restored without its three assertions that the default rejects a comment (it is text in YAML) |
| `Styml.Tests/YamlElementParseTests.cs` | `YamlMarshal_GetRawUtf8Value_NestedValues_ReturnsExpectedValue` |  | Restored |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyCommentedArray` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyCommentedArrayAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyCommentedObject` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyCommentedObjectAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEverythingArray` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEverythingArrayAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEverythingObject` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEverythingObjectAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteSimpleArrayAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteSimpleObject` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteSimpleObjectAsProperty` |  | Restored, in category `jsonoutput` as its siblings: it asserts JSON output |
| `Styml.Tests/YamlNode/ParseTests.cs` | `ReadSimpleObjectWithTrailingTrivia` |  | Restored |

### Trailing-comma option (19 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Styml.Tests/Serialization/OptionsTests.cs` | `AllowTrailingCommas` |  |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `ExtensionDataUsesReaderOptions` |  |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderOptionsWinTrailingCommas` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlStringsWithCommentsAndTrailingCommas` |  | reads 1-byte and larger segments; fails for its trailing commas and `//` comments under YAML, not for the segments |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestTokenStartIndexMultiSegment_WithTrailingCommasAndComments` |  | ; sibling `TestTokenStartIndex_WithTrailingCommasAndComments` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithTrailingCommasAndCommentsMultiSegment_Invalid` |  | ; sibling `YamlWithTrailingCommasAndComments_Invalid` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithTrailingCommasAndCommentsMultiSegment_Valid` |  | ; sibling `YamlWithTrailingCommasAndComments_Valid` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithTrailingCommasMultiSegment_Invalid` |  | ; sibling `YamlWithTrailingCommas_Invalid` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `YamlWithTrailingCommasMultiSegment_Valid` |  | ; sibling `YamlWithTrailingCommas_Valid` |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PartialYamlWithTrailingCommasAndComments_Invalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PartialYamlWithTrailingCommasAndComments_Valid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PartialYamlWithTrailingCommas_Invalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PartialYamlWithTrailingCommas_Valid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestTokenStartIndex_WithTrailingCommasAndComments` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithTrailingCommasAndComments_Invalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithTrailingCommasAndComments_Valid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithTrailingCommas_Invalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlWithTrailingCommas_Valid` |  |  |
| `Styml.Tests/YamlDocumentTests.cs` | `TrailingCommas` |  |  |

### Invalid JSON that is valid YAML (33 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `InvalidYamlFail` |  |  |
| `Common/CollectionTests/CollectionTests.Specialized.Read.cs` | `Read_SpecializedCollection_Throws` |  | `{1:"value"}` (unquoted key) is valid YAML |
| `Common/ConstructorTests/ConstructorTests.Exceptions.cs` | `ExtensionPropertyRoundTripFails` |  | `bad` is a plain YAML string |
| `Common/NumberHandlingTests.cs` | `Unquoted_FloatingPointConstants_Read_Fail` |  | unquoted `NaN`/`Infinity` are YAML plain strings, read by AllowNamedFloatingPointLiterals |
| `Common/NumberHandlingTests.cs` | `UnsupportedFormats` |  | unquoted `01` (leading zero) is a YAML integer; the other formats still throw |
| `Common/PolymorphicTests.cs` | `ReadPrimitivesFail` |  | `a` is a plain YAML string, which the YamlDocument guard rejects with NotSupportedException |
| `Styml.Tests/Serialization/Value.ReadTests.cs` | `RangeFail` |  | `00` (ulong) is a YAML integer; the other rows still throw |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InvalidYamlMultiSegmentByOne` |  | ; same rows as `InvalidYaml`, some rows also report a different position |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InvalidYamlMultiSegmentWithEmptyFirst` |  | ; same rows as `InvalidYaml`, some rows also report a different position |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InvalidYamlNumberVariousSegmentSizes` |  | reads 1-byte segments; fails because its invalid JSON numbers are valid YAML plain scalars, not for the segments |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `InvalidYamlStringVariousSegmentSizes` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `MultiSegmentInvalidLiteral` |  | ; `truabcdef` is a plain scalar |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `MultiSegmentInvalidLiteralInObject` |  | ; `nul234` is a plain scalar |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ReadYamlTokenWithExtraValueMultiSegment` |  | ; sibling `ReadYamlTokenWithExtraValue` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `TestBOMWithSingleYamlValueMultiSegment` |  | ; sibling `TestBOMWithSingleYamlValue`; the BOM-only rows are also D4 |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `ValidStringWithinInvalidYamlVariousSegmentSizes` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYaml` |  | M3d: the `[[{{}}]]` row is valid YAML since M3d (`{{}}` is a mapping whose key is `{}`) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYamlSingleSegment` |  | M3d: the `[[{{}}]]` row is valid YAML since M3d (`{{}}` is a mapping whose key is `{}`) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYamlSplitRemainsInvalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `InvalidYamlWhenPartial` |  | M3d: the `[[{{}}]]` row is valid YAML since M3d (`{{}}` is a mapping whose key is `{}`) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `ReadYamlTokenWithExtraValue` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TestBOMWithSingleYamlValue` |  | a UTF-8 BOM at the start is accepted under YAML |
| `Styml.Tests/YamlDocumentTests.cs` | `CheckUnparsable` |  | `1 2` is one plain scalar; `""` is D4 |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtArray` |  | `{value}   5` (two values) is one YAML scalar; the nested scenario ends in a non-final `9 `, which may continue (PB); M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtFalse` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtNull` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtNumber` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtObject` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtString` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtTrue` |  | as ParseReaderAtArray; M3d: the `StartAtPropertyName` rows with segmentCount 0 also wait at a root `{` on partial data (PB) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderThrowsWhileReading` |  | `[ invalid ]` is a valid YAML sequence |
| `Styml.Tests/YamlElementParseTests.cs` | `ParseValueInvalidDataFail` |  |  |
| `Styml.Tests/YamlElementParseTests.cs` | `TryParseValueInvalidDataFail` |  |  |

### Error text, type or position on invalid JSON (20 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyInvalidYamlFail` |  |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `YamlPath` |  |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `YamlPathPreservedArray` |  |  |
| `Styml.Tests/NewtonsoftTests/YamlSerializerTests.cs` | `IncompleteContainers` |  |  |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `InvalidYamlShouldFailAtAnyPosition_Sequence` |  | ; sibling `InvalidYamlShouldFailAtAnyPosition_Stream` |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `InvalidYamlShouldFailAtAnyPosition_Stream` |  | the YAML error is reported at a different byte |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `PathForArrayFails` |  |  |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `RootThrownFromReaderFails` |  |  |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `ThrownFromReaderFails` |  |  |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadObjectFail` |  |  |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | `ReadObjectWithWhitespaceAndThenInvalid` |  |  |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | `ReadPrimitiveWithWhitespaceAndThenInvalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `BasicTrySkipIncomplete` |  | M3d: some rows also wait at a root `[`/`{` on partial data (PB) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PositionInCodeUnits` |  | invalid escape reported at a different byte |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipIncomplete` |  | M3d: some rows also wait at a root `[`/`{` on partial data (PB) |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TrySkipOnValuePartialWithWhiteSpace` |  |  |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYaml_SeekableStream_Async_BadBOM` |  |  |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYaml_SeekableStream_BadBOM` |  |  |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYaml_UnseekableStream_Async_BadBOM` |  |  |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYaml_UnseekableStream_BadBOM` |  |  |

### Strict token-type exceptions (D2) (16 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `MetadataId_StartsWithInvalidToken` |  |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `MetadataRef_StartsWithInvalidToken` |  |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `OnlyStringTypeIsAllowed` |  |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `TestYamlPathDoesNotFailOnMultiThreads` |  | `{"$id":1}`: the number reads as the string "1" (D2), so the error and its path differ |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.List.cs` | `CustomListWithYamlConverterAttribute` |  | the baseline failure (numbers into List<string>) succeeds under D2 |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `TypeMismatchIDictionaryExceptionThrown` |  | `{"Key":1}` into IDictionary<string,string> succeeds (D2) |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfo` |  |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoMultiSegment` |  | ; sibling `ReaderPreservesPositionInfo` |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoNoneTokenType` |  |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoPropertyNameMultiLine` |  |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoPropertyNameTokenType` |  |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReaderPreservesPositionInfoSingleLineTokens` |  |  |
| `Styml.Tests/Serialization/Value.ReadTests.cs` | `TimeSpan_Read_Failure` |  | unquoted `1234` reads as text |
| `Styml.Tests/Serialization/Value.ReadTests.cs` | `ValueFail` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `CopyString_InvalidToken_ThrowsInvalidOperationException` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `InvalidConversion` |  |  |

### Multiple top-level JSON values (D3: multiple values are documents) (19 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_Array_TopLevelValues` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_Object_TopLevelValues` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_TopLevelValues_AcceptsAllYamlLinesShapes` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_TopLevelValues_HandlesArbitraryBufferBoundaries` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_TopLevelValues_HeterogeneousYamlValueTypes` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_NullElementsAreWrittenAsNull` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_StringsContainingNewlinesAreEscaped` | yes |  |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `TooMuchYaml` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_Comments_PartialData_MultiSegment` | yes | ; sibling `AllowMultipleValues_Comments_PartialData` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_MultiSegment` | yes | ; sibling `AllowMultipleValues` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_PartialData_MultiSegment` | yes | ; sibling `AllowMultipleValues_PartialData` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `AllowMultipleValues_SkipMultipleRepeatingValues_MultiSegment` | yes | ; sibling `AllowMultipleValues_SkipMultipleRepeatingValues` |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowMultipleValues` | yes |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowMultipleValues_Comments_PartialData` | yes |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowMultipleValues_PartialData` | yes |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `AllowMultipleValues_SkipMultipleRepeatingValues` | yes |  |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseValue_AllowMultipleValues_TrailingYaml` | yes |  |
| `Styml.Tests/YamlElementParseTests.cs` | `ParseValue_AllowMultipleValues_TrailingYaml` | yes |  |
| `Styml.Tests/YamlNode/YamlNodeTests.cs` | `Parse_AllowMultipleValues_TrailingYaml` | yes |  |

**M4a (2026-11-19).** Five round trips lost their tag: since M4's decision 1, the serializer writes `---`
between top-level values, which the YAML reader reads as documents, so they pass every row under the YAML grammar
(`--filter category=jsongrammar`, net11.0): `SerializeAsyncEnumerable_PipeWriter_TopLevelValues_RoundTripsWithDeserializeAsyncEnumerable`,
`SerializeAsyncEnumerable_TopLevelValues_HandlesItemsLargerThanDefaultBufferSize`,
`SerializeAsyncEnumerable_TopLevelValues_PipeReader_RoundTrip`, `SerializeAsyncEnumerable_TopLevelValues_PolymorphicItems` and
`SerializeAsyncEnumerable_TopLevelValues_RoundTripsWithDeserializeAsyncEnumerable`. The other 19 still fail under YAML
for the reason above (or assert the JSON Lines text as well).

**M4c (2026-11-21).** The writer-tied tags of M3 were measured again after the block default: every
`jsongrammar` test of `Styml.Tests` and `Styml.SourceGeneration.Tests` ran under the YAML grammar on net11.0
(`--filter category=jsongrammar`), once with the block default and once with `STYML_TEST_WRITER=json`. None of them lost its tag:
- the 19 methods above, and the 15 JSON Lines methods below, still fail under both defaults;
- `WriteRawInvalidYaml` (Empty input) fails all 10 rows under block and 5 of 10 under JSON-compatible, since the raw
  text is still validated by the YAML reader;
- `DeserializeAsyncEnumerable_ShouldStreamPartialData` (Partial data) passes under block, since a block sequence is not
  one long root line, but it still fails under JSON-compatible (leg YJ), so it keeps its tag.

### JSON Lines output (M4 decision 1) (15 methods)

Added in M4a. Each asserts JSON Lines text, which the serializer writes only under the JSON grammar switch since M4a.
`SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced` was split: its `topLevelValues: true` row moved to a tagged
twin, and the other row still runs under YAML.

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced_JsonLines` | yes | split from `SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced` (1 of 2 rows) |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_LargeRecord_Failure_PreservesCompletedLines` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_LargeRecord_PipeBackpressure` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_LargeRecord_PreservesOptionsAndReferences` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_LargeRecord_StreamBackpressure` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_Options_TopLevelValues_WorksWithoutAsyncEnumerableMetadata` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_PipeWriter_TopLevelValues_PartialItemFailure_PriorItemsAreFullyWritten` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_PipeWriter_TopLevelValues_ProducesYamlLines` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_ElementsCanBeAsyncEnumerables` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_IgnoresWriteIndented` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_PartialItemFailure_PriorItemsAreFullyWritten` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_PrimitiveValues_ProducesYamlLines` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_ProducesYamlLines` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_SourceGeneratedFailure_PreservesCompletedLines` | yes |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_SourceGeneratedSmallRecords` | yes |  |

### Empty input (D4) (8 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `EmptyYamlMultiSegmentIsInvalid` |  | ; sibling `EmptyYamlIsInvalid` |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `EmptyYamlWithinSequenceIsInvalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `EmptyYamlIsInvalid` |  |  |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `YamlContainingOnlyWhitespaceIsInvalid` |  |  |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | `WriteRawInvalidYaml` |  |  |
| `Styml.Tests/YamlElementParseTests.cs` | `TryParseValuePartialDataFail` |  |  |
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_NotARootLevelYamlArray_ThrowsYamlException` |  | M5c: decision 2; Tests |
| `Styml.Tests/Serialization/Stream.Collections.cs` | `DeserializeDictionaryStartsWithInvalidYaml` |  | M5c: decision 2; Tests |

### Duplicate keys (112 methods)

M5c. Every method here repeats a key inside one mapping of its input (checked by count); a method whose input repeats nothing was fixed instead. Most are theories, tagged whole. "Valid JSON, success expected" marks the methods that failed with the duplicate error itself (System.Text.Json accepts the repeat).

| File | Test method | Valid JSON, success expected | Projects | Note |
|---|---|---|---|---|
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DeserializeBuiltInDictionaryWithDuplicatePropertiesThrow` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DeserializeDictionaryWithDuplicateKeys` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DeserializeDictionaryWithDuplicateProperties` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DeserializeNestedDictionaryWithDuplicatePropertiesThrow` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `LastParameterWins` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `LastParameterWins_DoesNotGoToExtensionData` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `NumerousPropertiesWork` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `RespectAllowDuplicateProp_DuplicateCtorParam` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `RespectAllowDuplicateProp_Large` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `RespectAllowDuplicateProp_Small` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `TupleDeserialization_MoreThanSevenItems` |  | Tests | M7a: retired |
| `Common/ConstructorTests/ConstructorTests.Stream.cs` | `ExerciseStreamCodePaths` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ConstructorTests/ConstructorTests.Stream.cs` | `ReadSimpleObjectAsync` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ExtensionDataTests.cs` | `EmptyPropertyNameInExtensionData` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyDuplicateNames` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/ExtensionDataTests.cs` | `ExtensionProperty_DuplicatesThrow` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/PropertyNameTests.cs` | `DuplicatesByKebabCaseNamingPolicy` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/PropertyNameTests.cs` | `DuplicatesByKebabCaseNamingPolicyIgnoreCase` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/PropertyNameTests.cs` | `DuplicatesIgnoreCase` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_LastOneWins` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Deserialize.cs` | `DuplicateNonReferenceProperties_Preserve` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/RequiredKeywordTests.cs` | `RequiredPropertyOccuringTwiceInThePayloadWorksAsExpected` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IDictionary_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Dictionary.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_StructListOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_ListOfYamlElement_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_StructListOfYamlElement_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_IList_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes_NullInBetween` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StructCollectionOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StructListOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Enumerable.cs` | `CreationHandlingSetWithAttribute_CanPopulate_StructSetOfInt_PropertyOccurringMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateAndSerialize_ClassWithEnabledPolymorphismOnSerialization` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_Callbacks` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithFieldAndIgnoreReadOnlyFields` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyFieldAndIgnoreReadOnlyProperties` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyPropertyAndIgnoreReadOnlyFields` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithWritablePropertyAndIgnoreReadOnlyProperties` | yes | SourceGeneration.Tests, Tests | YJ only; M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_RequiredProperties` |  | SourceGeneration.Tests, Tests | M7a: retired |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithLargeParametrizedCtor_PropertyOccurredMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithSmallParametrizedCtor_PropertyOccurredMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClass_PropertyOccurredMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateWritableProperty_SimpleStruct_PropertyOccurredMultipleTimes` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithLargeParametrizedCtor_DeserializingNull` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithSmallParametrizedCtor_DeserializingNull` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClass_DeserializingNull` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithOptions_CanPopulateOrSerialize_RequiredPropertiesRecursive` | yes | SourceGeneration.Tests, Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadNestedObjectWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadSimpleObjectWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/OptionsTests.cs` | `SetOptionsFail` |  | Tests | M7a: retired |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `DserializeYamlDocumentArrayWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `DserializeYamlDocumentClassWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `DserializeYamlDocumentDeeplyNestedWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `DserializeYamlDocumentWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DeepEquals_EqualValuesReturnTrue` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DeepEquals_NotEqualValuesReturnFalse` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DserializeYamlElementArrayWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DserializeYamlElementClassWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DserializeYamlElementDeeplyNestedWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `DserializeYamlElementWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `GetPropertyFindsLast` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `GetPropertyFindsLast_WithEscaping` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentArrayWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentClassWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentDeeplyNestedWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentEscapeWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentObjectWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseYamlDocumentWithDuplicateProperties` | yes | Tests | M7a: retargeted (LastWins) |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `YamlObject_DuplicatePropertyThrows` | yes | Tests | M7a: retired |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `YamlObject_DuplicatePropertyThrows_NestedDeeply` | yes | Tests | M7a: retired |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `YamlObject_DuplicatePropertyThrows_NestedInArray` | yes | Tests | M7a: retired |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `YamlObject_ExtensionData_ManyDuplicatePayloads` | yes | Tests | M7a: retargeted (LastWins) |

### Object keys read as text (M5e) (3 methods)

M5e. Each asserts that reading an `object`-keyed dictionary throws; under the YAML grammar it now reads string keys, so each fails in leg YJ, where it ran as a `jsonoutput` method, and passes in leg J. Moved from "Exact output" (`jsonoutput`) to this category; their write halves still pass in YJ and J.

| File | Test method | Projects | Note |
|---|---|---|---|
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `GenericDictionary_WithObjectKeys_Throw_WhenDeserializing` | Tests | M5e: was `jsonoutput` |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestGenericDictionaryKeyObject` | Tests | M5e: was `jsonoutput` |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreAttribute_UnsupportedCollection` | SourceGeneration.Tests, Tests | M5e: was `jsonoutput` |

### Partial data: YAML token boundaries (17 methods)

| File | Test method | Valid JSON, success expected | Note |
|---|---|---|---|
| `Common/AsyncEnumerableTests.cs` | `DeserializeAsyncEnumerable_ShouldStreamPartialData` | yes | M3d: the root `[0,1,...]` is one 290-byte line, so the reader needs the whole line (or 1024 characters) before it can rule out `[...]: v`; the stream is read past half before the first item |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `NotEnoughData` |  | M3d: the row with `isFinalBlock: false` expects `{` to be returned as `StartObject` with the line unknown |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `SingleYamlValueMultiSegment` | yes | non-final `  true  ` may continue as a longer plain scalar |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `BasicSkipTest` | yes | M3d: the `isFinalBlock: false` block reads a root `[`/`{` whose line has no end in the data |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `PartialYaml` | yes | valid JSON split in two: a number or scalar at the split may continue, so a different byte count is consumed; the truncated prefix (`10.5e`) is a valid plain scalar; M3d: some rows also wait at a root `[`/`{` whose line has no end in the data |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `SkipInvalid` |  | M3d: the `isFinalBlock: false` blocks read a root `[` whose line has no end in the data |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `StateRecovery` | yes | M3d: `[1]` with `isFinalBlock: false` and no line end: the root `[` may still be a key |
| `Styml.Tests/Utf8YamlReaderTests.cs` | `TrySkipPartialAndContinueWithWhiteSpace` | yes | M3d: `[ 1, ` with `isFinalBlock: false`: the root `[` may still be a key |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderAtInvalidStart` |  | M3d: `[ { } ]` with `isFinalBlock: false` and no line end: the root `[` may still be a key |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderTerminatesAfterPropertyName` | yes | M3d: also a root `[`/`{` waiting on partial data (single-segment rows) |
| `Styml.Tests/YamlDocumentTests.cs` | `ParseReaderTerminatesWhileReading` | yes | M3d: also a root `[`/`{` waiting on partial data (single-segment rows) |
| `Styml.Tests/Serialization/ReadValueTests.cs` | `ReadObjectMultiSegment` | yes | M3c: the `isFinalBlock: false` row waits at the root `[`, whose line has no end in the data, as the span form does; moved from multi-segment in M3c |
| `Styml.Tests/Utf8YamlReaderTests.MultiSegment.cs` | `StateRecoveryMultiSegment` | yes | M3c: `[1]` with `isFinalBlock: false` and no line end: the root `[` may still be a key, as in `StateRecovery`; moved from multi-segment in M3c |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.Date.cs` | `TryGetDateTimeOffset_HasValueSequence_False` | yes | M3c: the `isFinalBlock: false` half reads a lone quoted scalar, which may still be a key (`"a" :`); the final half reads, with `HasValueSequence` as upstream; moved from multi-segment in M3c |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.Date.cs` | `TryGetDateTime_HasValueSequence_False` | yes | M3c: as `TryGetDateTimeOffset_HasValueSequence_False`; moved from multi-segment in M3c |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `TryGetGuid_HasValueSequence_False` | yes | M3c: as `TryGetDateTimeOffset_HasValueSequence_False`; moved from multi-segment in M3c |
| `Styml.Tests/Utf8YamlReaderTests.TryGet.cs` | `TryGetGuid_HasValueSequence_RetrievesGuid` | yes | M3c: as `TryGetDateTimeOffset_HasValueSequence_False`; moved from multi-segment in M3c |

## M7c: the obsolete APIs removed

M7c removes three inherited obsolete public APIs:
`YamlSerializerOptions.IgnoreNullValues` (SYSLIB0020), `YamlSerializerOptions.AddContext<TContext>()` (SYSLIB0049),
and the `SerializationInfo` constructors and `GetObjectData` of `YamlException` and `YamlReaderException` (SYSLIB0051).

- **A test of `IgnoreNullValues` itself is retired.** `IgnoreNullValues = true` also skips a null token on read (the
  member keeps its value); `DefaultIgnoreCondition = WhenWritingNull` does nothing on read. So a test whose assertions
  depend on that read-side skip is retired, not retargeted.
- **A test that only uses it to leave nulls out on write is retargeted** to
  `DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull` (a `bool` parameter: `ignoreNull ? WhenWritingNull :
  Never`): only the option line changes. A test that also reads is retargeted only when the payload it reads has no
  null token the old option would have skipped.
- **`AddContext<T>()` binding** is retargeted to `new TContext(options);`, which is what `AddContext` did (it binds
  the context and makes the options read-only). A test of `AddContext` itself is retired.
- **The exception serialization members:** no imported test uses them. `ExceptionTests`' `SerializationInfo` hits
  (`TypeWithBadCtorNoProps`, `TypeWithBadCtorWithPropsInvalid`) and `UnsupportedTypesTests`' "SerializationInfo is
  unsupported" rows test the serializer's refusal of the .NET type `SerializationInfo`, which stays; nothing changed.
- **The two guard lists:** `Styml.Tests/Serialization/CacheTests.cs` lost its `IgnoreNullValues` line in
  `GetPropertiesWithSettersAndNonDefaultValues`, and `OptionsTests.GetFullyPopulatedOptionsInstance` lost its
  `IgnoreNullValues` clause. Both checks fail until the product drops the property, and are expected to pass once
  it is gone.

Counted by source method (a `test/Common` method once, however many wrappers run it): **23 retargeted, 14 methods
retired, 7 row sets retired, 8 parts retired.** No assertion changed outside the retired code. The per-method diff
of the touched classes (legs Y and YJ, net11.0) shows the 14 retired methods gone (33 wrapper methods in leg Y, 36 in
leg YJ, which also runs the `jsonoutput` ones), and no other method changed outcome except the two guard checks above.
The earlier tables of this file keep their entries for the retired methods, as the record (M7a's rule):
`ClassWithComplexObjectUsingIgnoreNeverAttribute` under "Exact output", `YamlIgnoreCondition_LastOneWins` under
"Duplicate keys".

### Retired methods (14)

| File | Method | Reason |
|---|---|---|
| `Styml.Tests/Serialization/Null.ReadTests.cs` | `EnableIgnoreNullValuesOnRead` | the option's read side: null tokens skipped, initialized values kept |
| `Styml.Tests/Serialization/ContinuationTests.NullToken.cs` | `ContinuationAtNullToken` (and its member data `ContinuationAtNullTokenTestData`) | read only, with the option: the skip of a null token at a buffer boundary (dotnet/runtime#42070) |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `SetterCalledWhenConverterReturnsNull` | read only, with the option: its baseline is the skip; its second half (a converter's null result is still set) holds trivially without it |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `IgnoreNullValues_Deserialization_CanBeInvalidatedByCustomContracts` | the option's read side, overridden by a custom setter |
| `Styml.Tests/Serialization/OptionsTests.cs` | `CopyConstructor_IgnoreNullValuesCopied` | copies the removed option |
| `Styml.Tests/Serialization/OptionsTests.cs` | `CannotSetBoth_IgnoreNullValues_And_DefaultIgnoreCondition` | the removed option's conflict with `DefaultIgnoreCondition` |
| `Common/PropertyVisibilityTests.cs` | `ClassWithComplexObjectUsingIgnoreNeverAttribute` | the option's read side (a null `Class` is skipped and keeps its value); also listed under "Exact output" (category `jsonoutput`), which keeps it as the record |
| `Common/PropertyVisibilityTests.cs` | `ValueType_Properties_NotIgnoredWhen_IgnoreNullValues_Active_ClassTest` | the option itself, read side first (null skipped for reference and nullable members) |
| `Common/PropertyVisibilityTests.cs` | `ValueType_Properties_NotIgnoredWhen_IgnoreNullValues_Active_LargeStructTest` | as `..._ClassTest` |
| `Common/PropertyVisibilityTests.cs` | `ValueType_Properties_NotIgnoredWhen_IgnoreNullValues_Active_SmallStructTest` | as `..._ClassTest` |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_LastOneWins` | read only, with the option: a `Never` member's last null wins over the option's skip; without the option it holds trivially. M7a had retargeted it (`DuplicateKeyHandling = LastWins`; "Duplicate keys" above) |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `IgnoreNullValues_SetDefaultConstructorParameter_ToConstructorArguments_ThatCanBeNull` | the option's read side for a constructor parameter |
| `Common/MetadataTests.Options.cs` | `AddContext` | `AddContext` itself: binds once, then throws |
| `Common/MetadataTests.Options.cs` | `AddContextOverwritesOptionsForFreshContext` | `AddContext` itself: it replaces the options a parameterless constructor bound |

### Retired row sets (7)

| File | Method | Rows retired | Reason |
|---|---|---|---|
| `Styml.SourceGeneration.Tests/SerializationLogicTests.cs` | `SerializationFuncNotInvokedWhenNotSupported` | the `IgnoreNullValues = true` row of `GetOptionsUsingUnsupportedFeatures` (with its `#pragma warning disable/restore SYSLIB0020`) | that the removed option turns off the fast path |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `ShouldWorkAtAnyPosition_Stream` | the `ignoreNullValues: true` half of `TestData`: the `IgnoreNullValues` dimension, its loop, its row element and the method's parameter are gone | read only, with the option, and the payload holds a null (`NestedClassWithParamCtor(null)`) |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `ShouldWorkAtAnyPosition_Sequence` | as `ShouldWorkAtAnyPosition_Stream` (same member data) | as `ShouldWorkAtAnyPosition_Stream` |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordDeserialization` | `InlineData(true)`; the method is a `[Fact]` without the parameter, its `false` row | reads `{"LastName":null}` with the option: a skipped null still counting as present |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordAndSmallParametrizedCtorFailsDeserialization` | as `ClassWithRequiredKeywordDeserialization` | reads nulls with the option |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordAndLargeParametrizedCtorFailsDeserialization` | as `ClassWithRequiredKeywordDeserialization` | reads nulls into constructor parameters with the option |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionWhenWritingDefault_ClassProperty` | the `ClassWithClassProperty_IgnoreConditionWhenWritingDefault_Ctor` row (`IgnoreNullValues = true`); the method's `if (options.IgnoreNullValues)` branch went with it, its `else` assertion kept | the row asserted the option's read-side skip; it was that type's only row |

### Retired parts (8)

| File | Method | Part retired | Reason |
|---|---|---|---|
| `Styml.Tests/Serialization/Null.ReadTests.cs` | `InvalidRootOnRead` | the read with `IgnoreNullValues = true` in both branches | the option's read side at an unsupported root |
| `Styml.Tests/Serialization/OptionsTests.cs` | `NewDefaultOptions_IsNotReadOnly` | `Assert.Throws(() => options.AddContext<YamlContext>())` | `AddContext` itself on read-only options |
| `Styml.Tests/Serialization/OptionsTests.cs` | `YamlSerializerOptions_Default_IsReadOnly` | `Assert.Throws(() => optionsSingleton.AddContext<YamlContext>())` | as above; the `new YamlContext(optionsSingleton)` line beside it stays |
| `Common/ExtensionDataTests.cs` | `NullValuesIgnored` | the block that reads with `IgnoreNullValues = true` | the option's read side, which does not reach extension data |
| `Common/PropertyVisibilityTests.cs` | `IgnoreCondition_WhenWritingDefault_DoesNotApplyToDeserialization` | the "Baseline" read with `IgnoreNullValues = true` (the kept half declares its own `options` and `obj`) | the option's read side |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_ConverterCalledOnDeserialize` | the read with `IgnoreNullValues = true` | the option on read |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `IgnoreNullValues_DontSetNull_ToConstructorArguments_ThatCantBeNull` | the second half, the same reads with `IgnoreNullValues = true` | the option's read side for constructor parameters |
| `Common/MetadataTests.Options.cs` | `OptionsImmutableAfterBinding` | the "Bind via AddContext" block (the kept "Bind via context ctor" block declares `options`) | `AddContext` itself; the ctor block checks the same binding |

### Retargeted (23)

| File | Methods | Change |
|---|---|---|
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `EnableIgnoreNullValuesOnWrite`, `InvalidRootOnWrite` | `WhenWritingNull` (the first reads back with default options) |
| `Styml.Tests/Serialization/Stream.WriteTests.cs` | `VeryLargeYamlFileTest`, `DeepNestedYamlFileTest`, `NestedYamlFileCircularDependencyTest` | `WhenWritingNull`; the first two by their `bool ignoreNull` (they read only what they wrote, which has no null member) |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `IgnoreNullValues_Serialization_CanBeInvalidatedByCustomContracts` | `WhenWritingNull` (write only; its comment names both options) |
| `Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | `BlogCodeSample` | `WhenWritingNull` (qualified `Serialization.YamlIgnoreCondition`: the file has no `using` for it) |
| `Common/UnsupportedTypesTests.cs` | `SerializeUnsupportedType` | `WhenWritingNull` (write only) |
| `Common/PolymorphicTests.cs` | `NestedObjectAsRootObjectIgnoreNullable` | `WhenWritingNull` (write only) |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ShouldHandleNullInDictionaries_Serialize_IgnoreNullValues`, `DictionaryWithNullShouldPreserveOrder_Serialize_IgnoreNullValues` | `WhenWritingNull` (write only) |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionNever_IgnoreNullValues_True` | `WhenWritingNull`: its null is on a `Never` member, which neither option skips on read or write |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionWhenWritingDefault_StructProperty` (its `StructWithStructProperty_IgnoreConditionWhenWritingDefault_Ctor` row) | `WhenWritingNull`: it writes no null, and its null read into an `int` throws under either option |
| `Styml.Tests/Serialization/OptionsTests.cs` | `TypeInfoResolverCannotBeSetAfterAddingContext`, `WhenAddingContextTypeInfoResolverAsContextOptionsAreSameAsOptions`, `WhenAddingContext_SettingResolverToNullThrowsInvalidOperationException`, `CopyConstructor_CopiesYamlSerializerContext`, `GetTypeInfo_ResolverWithoutMetadata_ThrowsNotSupportedException`, `GetTypeInfoGeneric_ResolverWithoutMetadata_ThrowsNotSupportedException` | `new YamlContext(options);` |
| `Styml.Tests/Serialization/YamlSerializerApiValidation.cs` | the `GetTypeInfo` helper (the `myDummyTypeInfo` field of the validation methods) | `new MyDummyContext(options);` |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | `GetConverter_Poco_WriteThrowsNotSupportedException` | `new YamlContext(options2);` |
| `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | `PublicContextAndTestClassWithPropertiesWithDifferentAccessibilities` | `new PublicContext(options);` |
| `Common/MetadataTests.Options.cs` | `AlreadyBindedOptions` | `new MyYamlContext(options);` |

Fixtures left in place, now unused by any test: the classes of `Styml.Tests/Serialization/ContinuationTests.NullToken.cs`
and the `CustomerSearchApi107KB`/`CustomerSearchApi108KB` strings of `Styml.Tests/Resources/Strings.resx`;
`ClassWithInitializedUri` and `UriToNullConverter` (`CustomConverterTests.HandleNull.cs`);
`MyYamlContextThatSetsOptionsInParameterlessCtor` (`MetadataTests.Options.cs`). The `test/Common` fixtures of the
retired methods stay too, since the source-generation contexts register them.

## Local changes to imported test code (M5c, M5d, M5f)

Under the `Error` default the serializer's duplicate check reads custom dictionary types (`ContainsKey`, `Contains`,
`Count`), which System.Text.Json's default never did, and the DOM-backed wrappers parse before the serializer runs.
These imported files are changed so that the methods keep running in every leg; no assertion changes.

| File | Change | Why |
|---|---|---|
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `DerivedDictionary_TypeDiscriminator`: `Count` (1) and `ContainsKey` over its one `dictionaryKey` entry; `PolymorphicClassWithConstructor.DerivedDictionary`: `Count` and `ContainsKey` over its storage | they threw `NotImplementedException`; 18 polymorphic `*_Deserialization` methods use them |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryThatHasIncompatibleEnumerator.Contains` over its `Hashtable` | it threw `NotImplementedException`; the fixture's point, its enumerator, is unchanged (`VerifyDictionaryThatHasIncomatibleEnumerator*`) |
| `Styml.Tests/Serialization/YamlSerializerWrapper.Reflection.cs` | `OptionsHelpers.GetDocumentOptions` sets `DuplicateKeyHandling = LastWins` | the Document, Element and Node wrappers parse the input into a DOM first; with the default `Error` the DOM refused a repeated `$type` before the serializer under test could judge it (5 polymorphic methods) |
| `Styml.Tests/Serialization/OptionsTests.cs` (M5d) | `GetFullyPopulatedOptionsInstance` fills `TagMappings` (`IDictionary<string, Type>`, get-only) with one entry | the method creates an instance of each property type it does not know, and says an exception there means the test should be updated for a new property; `TagMappings` is the first reference-type property since the import. `CopyConstructor_CopiesAllPublicProperties` then also checks that the copy has the entry |
| `Styml.Tests/Serialization/OptionsTests.cs` (M5f) | `GetFullyPopulatedOptionsInstance`'s value-type branch also sets `MappingOrder = Sorted` | M5f adds `YamlSerializerOptions.MappingOrder` (review 2); the branch sets the value-type options it knows, so `CopyConstructor_CopiesAllPublicProperties` checks that the copy has it |
| `Styml.Tests/Serialization/CacheTests.cs` (M5f) | `GetPropertiesWithSettersAndNonDefaultValues` yields `MappingOrder = Sorted` | `YamlSerializerOptions_EqualityComparer_ChangingAnySettingShouldReturnFalse` fails with "missing property declaration for MappingOrder, please update the method" for a new settable property; the options' equality and hash code include it |

## Tests that need JSON-compatible output (category `jsonoutput`, M4c)

Since M4c the writer's unset style is block. A test
that only holds when the default output is JSON-compatible carries `[Trait("category", "jsonoutput")]`, which a bare
`dotnet test` (through the default `VSTestTestCaseFilter` in `test/Directory.Build.targets`), `eng/test/run-tests.sh`
and `run-net481-remote.sh` exclude from the default run (leg Y). With `STYML_TEST_WRITER=json` (leg YJ) the
`AppContext` switch `System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle` restores the JSON-compatible default, and
these tests run with the rest of the YAML-grammar run, which then equals the pre-M4c default run. The JSON grammar
(`STYML_TEST_GRAMMAR=json`, leg J) sets the same switch and runs everything.

The list was made by script from the real block writer, not from 10.1's proxy. On net11.0, a method is tagged when it fails in
leg Y, passes in leg YJ, and its failure is one of 10.2's classes, or is one of the by-design differences below.
Each method is in one section; 1,112 methods in all since M5e (1,022 exact output, 0 DOM, 90 by design; 1,114 at M5d,
1,112 at M5b, 1,104 at M5a, 1,350 at M4c). M5e moved three methods to `jsongrammar` ("Object keys read as text") and
added one. M5d's two pass in leg Y on net11.0 by coincidence and fail there on net481; they are
tagged from that run. A method
runs in several serializer wrapper classes, and its trait covers all of them, including wrappers that pass in leg Y.

| Category | What the test assumes |
|---|---|
| Exact output | It asserts the writer's or the serializer's text, bytes, lengths or counts (`BytesPending`, `BytesCommitted`, flush counts, substring counts), or parses the output with System.Text.Json, or builds JSON text from it. |
| DOM reads writer output | It read `Serialize` output with `YamlDocument`, `YamlElement` or `YamlNode`, which accepted only JSON-shaped YAML until M5a (`NotSupportedException`, "block collection"). The 256 methods lost their tag in M5a, when the DOM read block YAML; the section is empty since. |
| Block style behaves differently by design | It expects the JSON-compatible behaviour where block style behaves otherwise by an M4 decision. Each difference is asserted by a Styml.Yaml.Tests test. |

The by-design differences:

| Difference | Decision | Asserted by |
|---|---|---|
| `IndentSize` below 2, or `IndentCharacter` `'\t'`, is refused (`ArgumentException` when the writer is created) | The `IndentSize` row of the writer's layout: a dash needs a space after it; YAML indentation is spaces | `BlockWriterTests.IndentSizeBelowTwo_Throws`, `BlockWriterTests.TabIndentation_Throws` |
| NaN and infinity are written as `.nan`, `.inf`, `-.inf` without `AllowNamedFloatingPointLiterals`, also under `Strict` | Decision 8 | `BlockWriterTests.NonFiniteFloats_AreYamlFloats`, `BlockSerializerTests.Serialize_Block_NonFinite_WithoutNamedLiterals` (added in M4c) |
| `*/` is text in a `#` comment, not an error | The `WriteCommentValue` row of the writer's layout | `BlockWriterNodesTests.Comments_Text_AndJsonCompatibleStyle` |
| Top-level values follow the user's `NewLine` (and indentation); the JSON Lines terminator is `\n` only in JSON-compatible style | In block style the user's layout options apply | `BlockSerializerTests.TopLevelValues_Block_FollowNewLineAndIndentation` (added in M4c) |
| NaN and infinity are `.nan`, `.inf` with `AllowNamedFloatingPointLiterals` too, not the strings `"NaN"`, `"Infinity"` | Decision 8 | `BlockSerializerTests.Serialize_Block_NonFinite_WithoutNamedLiterals`, `BlockSerializerTests.Serialize_Block_TypedRules` |
| `ReferenceHandler.Preserve` writes YAML anchors and aliases, not `$id`/`$ref`/`$values`: output compared with a `$id` structure differs, and a cycle read back with `IgnoreCycles` (or no handler) is a recursive alias, refused, not `$id`/`$ref` extension data (M5b) | M5 decision 6 | `SerializerPreserveTests.PreserveAnchorsRepeatedObjects_PreserveAllAnchorsEveryObject`, `SerializerPreserveTests.PolymorphicValuesKeepTheirDiscriminator`, `SerializerPreserveTests.Cycles`, `SerializerPreserveTests.IgnoreCyclesReadsAnchoredCyclesAsRecursiveAliases`, `SerializerAliasTests.RecursiveAliases` |

### Exact output (1,022 methods)

| File | Test method | Projects | Note |
|---|---|---|---|
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_FlushesStreamAsValuesAreProduced` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_LargeRecord_PipeInterruption_DisposesEnumerators` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_Options_ArrayMode_WorksWithoutAsyncEnumerableMetadata` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_PropagatesCustomEncoder` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_YamlTypeInfo_ArrayMode_WorksWithoutAsyncEnumerableMetadata` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsync_NestedAsyncEnumerable_FlushesStreamWhenAwaitingProducer` | Tests |  |
| `Common/AsyncEnumerableTests.cs` | `SerializeAsync_ValueWithoutAsyncEnumerables_FlushesStreamOnCompletion` | Tests |  |
| `Common/CollectionTests/CollectionTests.AsyncEnumerable.cs` | `WriteAsyncEnumerable_CancellationToken_IsPassedToAsyncEnumerator` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Concurrent.cs` | `Write_ConcurrentCollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_ApplyDictionaryKeyPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_ForClassWithDictionaryProperty_ApplyDictionaryKeyPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_ForKeyValuePairWithDictionaryValue_ApplyDictionaryKeyPolicy` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_ForNestedTypedDictionary_ApplyDictionaryKeyPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_ForTypedDictionary_ApplyDictionaryKeyPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_Null_Nullable_Values` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize_Null_Values` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CamelCaseSerialize` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CustomNameSerialize_NullableValue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `CustomNameSerialize` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `EnumSerializationAsDictKey_NoDictionaryKeyPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `EnumSerialization_DictionaryPolicy_Honored_CamelCase` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `EnumSerialization_DictionaryPolicy_NotApplied_WhenEnumsAreSerialized` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `IgnoreKeyPolicyForExtensionData` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `KeyConflict_Serialize_WriteAll` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.KeyPolicy.cs` | `SerializationWithYamlExtensionDataAttribute_IgoneDictionaryKeyPolicy` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `EnsureNonStringKeysDontGetEscapedOnSerialize` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `IDictionary_Keys_ShouldBe_String_WhenDeserializing` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `KeyWithCustomPrimitiveConverter_FallbackToDefaultConverter` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `RoundtripAllDictionaryConverters` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestDictionaryKey_CustomConverter_ComposingWithDefaultConverter` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestDictionaryKey` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestEnumKeyWithNotValidIdentifier` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestNonGenericDictionaryKeyObject` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `TestPolicyOnlyAppliesToString` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ArrayOfDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `CustomEscapingOnPropertyNameAndValue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfArrayOfDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfDictionaryOfDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfObject_NonPrimitiveTypes` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfObject` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfString` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfTOnlyWithStringPoco` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfTOnlyWithStringTValueAsInt` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryOfTOnlyWithStringTValueAsPoco` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryWithNullShouldPreserveOrder_Serialize_IgnoreNullValues` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryWithNullShouldPreserveOrder_Serialize` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `DictionaryWith_ObjectWithNoParameterlessCtor_Serialize_Works` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `Hashtable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ImplementsDictionary_DictionaryOfString` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ImplementsIDictionaryOfObject` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ImplementsIDictionaryOfString` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ListOfDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `NestedDictionariesRoundtrip` | Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ShouldHandleNullInDictionaries_Serialize_IgnoreNullValues` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `ShouldHandleNullInDictionaries_Serialize` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Dictionary.cs` | `UnicodePropertyNames` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Read.cs` | `CollectionWith_BackingField_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Read.cs` | `DoesNotCall_CollectionPropertyGetter_EveryTimeElementIsAdded` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `ConvertIEnumerableValueTypesThenSerialize` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `GenericStructIReadOnlySetWrapperT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `GenericStructISetWrapperT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfICollectionT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfIEnumerableT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfIListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfIReadOnlyCollectionT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfIReadOnlyListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfLinkedListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfSortedSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteArrayOfStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericICollectionOfGenericICollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericIEnumerableOfGenericIEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericIListOfGenericIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericIReadOnlyCollectionOfGenericIReadOnlyCollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericStructICollectionWrapperT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteGenericStructIListWrapperT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteHashSetTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteICollectionTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIEnumerableTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIListTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIReadOnlyCollectionTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIReadOnlyListTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIReadOnlyListTOfIReadOnlyListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteIReadOnlySetTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteISetTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteLinkedListTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteLinkedListTOfLinkedListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteListOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteListOfList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveHashSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveICollectionT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveIEnumerableT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveIListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveIReadOnlyCollectionT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveIReadOnlyListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveIReadOnlySet` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveISetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveLinkedListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveSortedSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WritePrimitiveStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteQueueTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteQueueTOfQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteSimpleTestClassWithGenericStructCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteSimpleTestStructWithNullableGenericStructCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteStackTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Generic.Write.cs` | `WriteStackTOfStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfIImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfIImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfIImmutableSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfIImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableHashSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableSortedSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteArrayOfImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableListTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableListTOfIImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableQueueTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableQueueTOfIImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableSetTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableSetTOfIImmutableSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableStackTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteIImmutableStackTOfIImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableArrayOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableArrayOfImmutableArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableDictionaryStringKey` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableHashSetTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableHashSetTOfImmutableHashSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableListTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableListTOfImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableQueueTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableQueueTOfImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableQueue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableStackTOfArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableStackTOfImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteImmutableStack` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveIImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveIImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveIImmutableSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveIImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveImmutableHashSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveImmutableListT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveImmutableQueueT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveImmutableSortedSetT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WritePrimitiveImmutableStackT` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteSimpleClassWithImmutableArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteSimpleClassWithObjectImmutableArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Immutable.Write.cs` | `WriteSimpleImmutableArray` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `HonorCustomEncoder` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `HonorNamingPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `WriteKeyValuePairOfKeyValuePair` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `WriteKeyValuePairOfList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `WriteKeyValuePairWithNullValues` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `WriteListOfKeyValuePair` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.KeyValuePair.cs` | `WritePrimitiveKeyValuePair` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeMemoryByteAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeMemoryByteClassAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeMemoryOfTAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeMemoryOfTClassAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeReadOnlyMemoryOfTAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Memory.cs` | `SerializeReadOnlyMemoryOfTClassAsync` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayListOfArrayList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfArrayList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfICollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfIEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfQueue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteArrayOfStack` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteGenericICollectionOfICollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteGenericIEnumerableOfIEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteGenericQueueOfQueue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteGenericStackOfStack` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteICollectionOfICollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteIEnumerableOfIEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteIListGenericOfIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteIListOfIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteNonGenericCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveArrayList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveICollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveIEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveQueue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WritePrimitiveStack` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteQueueOfQueue` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteSimpleTestClassWithStructCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteSimpleTestStructWithNullableStructCollectionWrappers` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteStackOfStack` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.NonGeneric.Write.cs` | `WriteStructWrapperForIList` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.ObjectModel.Write.cs` | `Write_ObjectModelCollection` | SourceGeneration.Tests, Tests |  |
| `Common/CollectionTests/CollectionTests.Specialized.Write.cs` | `Write_SpecializedCollection` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `MultiplePublicParameterizedCtors_SingleParameterlessCtor_NoAttribute_Supported_UseParameterlessCtor` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `NoPublicParameterlessCtor_MultiplePublicParameterizedCtors_WithAttribute_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `PublicParameterlessCtor_MultiplePublicParameterizedCtors_WithAttribute_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `SinglePublicParameterizedCtor_NoPublicParameterlessCtor_NoAttribute_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `SinglePublicParameterizedCtor_NoPublicParameterlessCtor_WithAttribute_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `SinglePublicParameterizedCtor_SingleParameterlessCtor_NoAttribute_Supported_UseParameterlessCtor` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.AttributePresence.cs` | `Struct_MultiplePublicParameterizedCtors_NoPublicParameterlessCtor_NoAttribute_Supported_UseParameterlessCtor` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `ArgumentDeserialization_Honors_YamlInclude` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `ArgumentDeserialization_Honors_YamlNumberHandling` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `ArgumentDeserialization_Honors_YamlPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `DefaultForReferenceTypeCtorParam` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `DefaultForValueTypeCtorParam` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `TestTypeWithEnumParameters` | SourceGeneration.Tests, Tests |  |
| `Common/ConstructorTests/ConstructorTests.ParameterMatching.cs` | `TupleDeserialization_DefaultValuesUsed_WhenYamlMissing` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `CustomObjectConverterInExtensionProperty` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `CustomYamlElementConverterInExtensionProperty` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyCamelCasing` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyDuplicateNames` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyIgnoredWhenWritingDefault` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyObjectValue_Empty` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyObjectValue_SameAsExtensionPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionPropertyObjectValue` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionProperty_SupportsWritingToCustomSerializerWithExplicitConverter` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `ExtensionProperty_SupportsWritingToCustomSerializerWithOptions` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `IReadOnlyDictionary_ExtensionPropertyIgnoredWhenWritingDefault` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `IReadOnlyDictionary_ObjectExtensionPropertyRoundTrip` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `IReadOnlyDictionary_YamlElementExtensionPropertyRoundTrip` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `LongPropertyNames` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `MultipleExtensionPropertyIgnoredWhenWritingDefault` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `NullValuesIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `SerializeIntoImmutableDictionaryProperty` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `SerializeYamlObjectExtensionData_EmptyYamlObject` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `SerializeYamlObjectExtensionData_NullYamlObject` | SourceGeneration.Tests, Tests |  |
| `Common/ExtensionDataTests.cs` | `SerializeYamlObjectExtensionData_ProducesValidYaml` | Tests, SourceGeneration.Tests | M5a |
| `Common/Ieee754NumericConverterTests.cs` | `Decimal128_ExtremeValues_Roundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/Ieee754NumericConverterTests.cs` | `DictionaryKeys_Roundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/Ieee754NumericConverterTests.cs` | `FullPrecisionValues_RoundtripAsKeysAndStrings` | SourceGeneration.Tests, Tests |  |
| `Common/Ieee754NumericConverterTests.cs` | `NamedFloatingPointLiterals_Roundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/Ieee754NumericConverterTests.cs` | `WriteAsString` | SourceGeneration.Tests, Tests |  |
| `Common/MetadataTests.cs` | `ClassWithRefStructConstructorParameter_Serialization` | SourceGeneration.Tests |  |
| `Common/MetadataTests.cs` | `ClassWithRefStructProperty_Serialization` | SourceGeneration.Tests |  |
| `Common/NodeInteropTests.cs` | `NodesAsPocoProperties` | SourceGeneration.Tests, Tests |  |
| `Common/NullableAnnotationsTests.cs` | `WriteNotNullPropertiesWithNullIgnoreConditions_Succeeds` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `AdaptableCustomConverter` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `AllowFloatingPointConstants_WriteAsNumber_IfNotConstant` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `AttributeOnType_WinsOver_GlobalOption` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Attribute_Allowed_On_NonNumber_NonCollection_Property` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Attribute_OnType_NotRecursive` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `CustomConverterOverridesBuiltInLogic` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `EscapingTest` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_Byte_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_Decimal_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_Int32_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_Int64_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_Single_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `GetConverter_UInt64_RespectsNumberHandling_WriteAsString` | Tests |  |
| `Common/NumberHandlingTests.cs` | `HandlingOnMemberOverridesHandlingOnType_Dictionary` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `HandlingOnMemberOverridesHandlingOnType_Enumerable` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `MemberAttributeAppliesToCollection_SimpleElements` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `NestedCollectionElementTypeHandling_Overrides_GlobalOption` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `NestedDictionaryElementTypeHandling_Overrides_GlobalOption` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `NonNumber_AsBoxed_CollectionRootType_Element` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `NullableNumber_RoundtripNull` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_AsBoxed_CollectionRootType_Element` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_AsBoxed_RootType` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_AsCollectionElement_RoundTrip` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_AsObjectWithParameterizedCtor_PropHasAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_AsRootType_RoundTrip` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `Number_RoundtripNull` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `TypeAttributeAppliesTo_CustomCollectionElements_HonoredWhenProperty` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `TypeAttributeAppliesTo_CustomCollectionElements` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `TypeAttributeAppliesTo_CustomDictionaryElements_HonoredWhenProperty` | SourceGeneration.Tests, Tests |  |
| `Common/NumberHandlingTests.cs` | `TypeAttributeAppliesTo_CustomDictionaryElements` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `PolymorphicClassWithDerivedCollection_Collection_RoundtripsAsExpected` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `PolymorphicClassWithDerivedCollection_Dictionary_RoundtripsAsExpected` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `PolymorphicClassWithEscapedTypeDiscriminator_RoundtripsCorrectly` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.TypeClassifier.cs` | `Classifier_SerializeThenDeserialize_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.TypeClassifier.cs` | `OpenGenericDerivedType_PartiallyConcrete_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `AnonymousType` | Tests |  |
| `Common/PolymorphicTests.cs` | `ArrayAsRootObject` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `CustomResolverWithFailingAncestorType_DoesNotSurfaceException` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `NestedObjectAsRootObjectIgnoreNullable` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `NestedObjectAsRootObject` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `PolymorphicTypeWithExtensionDataConflictingPropertyName_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `PolymorphicTypeWithIgnoredConflictingPropertyName_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `PrimitivesAsRootObject` | SourceGeneration.Tests, Tests |  |
| `Common/PolymorphicTests.cs` | `SimpleTestClassAsRootObject` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `BuiltInPolicySerialize` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `CachingKeys` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `ClassWithIgnoredCaseInsensitiveConflict_RespectsIgnoredMember` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `EmptyPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `SpecialCharacters` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `UnicodePropertyNamesWithPooledAlloc` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `UnicodePropertyNames` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_MemberLevel_OverridesTypeLevelAndGlobal` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_MemberLevel_OverridesTypeLevel` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_MemberLevel_Serialize` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_TypeLevel_OverridesGlobalPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_TypeLevel_Serialize` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_YamlPropertyNameTakesPrecedence` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlOutputNotAffectedByCasingPolicy` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyNameTests.cs` | `YamlPropertyNameAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `CustomNamedInitOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `InitOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `NonPublicInitOnlySetter_With_YamlInclude` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `NonPublicInitOnlySetter_Without_YamlInclude_Fails` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `NullableStructWithInitOnlyProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.InitOnly.cs` | `YamlIgnoreOnInitOnlyProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `CanAlwaysRoundtripInternalYamlIncludeProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `ClassWithIgnoredAndPrivateMembers_DoesNotIncludeIgnoredMetadata` | Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `ExtensionDataCanHaveNonPublicSetter` | Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `HonorYamlPropertyName_PrivateGetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `HonorYamlPropertyName_PrivateSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `Honor_YamlSerializablePropertyAttribute_OnProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `InitOnlyProperties_GenericTypeWithConstraints_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `NonPublicProperty_YamlInclude_WorksAsExpected` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `NonPublic_AccessorsNotSupported_WithoutAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `Public_And_NonPublicPropertyAccessors_PropertyAttributes` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_GenericType_PrivateProperties_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_MixedAccessibility_AllPropertiesRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_PrivateGetterProperties_CanSerialize` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_PrivateInitOnlyProperties_PreservesDefaults` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_PrivateProperties_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_ProtectedProperties_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.NonPublicAccessors.cs` | `YamlInclude_StructWithPrivateProperties_CanRoundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `ClassWithComplexObjectUsingIgnoreNeverAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `ClassWithComplexObjectsUsingIgnoreWhenWritingDefaultAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `CorrectlyIgnorePropertyWithNewModifier` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `DiamondInterfaceHierarchy_Serialization_ShouldIncludeBaseInterfaceProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `HiddenPropertiesIgnored_WhenOverridesIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreConditionNever_WinsOver_IgnoreReadOnlyFields` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreConditionNever_WinsOver_IgnoreReadOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreConditionWhenWritingDefault_WinsOver_IgnoreReadOnlyFields` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreConditionWhenWritingDefault_WinsOver_IgnoreReadOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreCondition_WhenWritingDefault_DoesNotApplyToCollections` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreCondition_WhenWritingDefault_Globally_Works` | Tests, SourceGeneration.Tests | M5a: `{}` against `{}\n` |
| `Common/PropertyVisibilityTests.cs` | `IgnoreCondition_WhenWritingDefault_PerProperty_Works` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreReadOnlyFields` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `IgnoreReadOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_BasePublicPropertyIgnored_ConflictWithDerivedPrivate` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_BasePublicPropertyIgnored_ConflictWithDerivedPublicPropertyIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_NewSlotPublicFieldIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_NewSlotPublicPropertyIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_NonPublicProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_PublicProperty_ConflictWithPrivateDueAttributes` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_PublicProperty_ConflictWithPrivateDuePolicy` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_PublicProperty_ConflictWithPublicDueAttributes` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_PublicProperty_ConflictWithPublicDuePolicy` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Ignore_VerifyNoReferenceToGetterAndSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `InternalProtectedGetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `InternalProtectedMembers` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `InternalProtectedSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `NoSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `NonPublicMembersAreNotIncluded` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `OverrideYamlIgnorePropertyUsingYamlPropertyNameReversed` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `OverrideYamlIgnorePropertyUsingYamlPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `PrivateGetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `PrivateProtectedGetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `PrivateProtectedMembers` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `PrivateProtectedSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `ProtectedGetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `ProtectedMembers` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `ProtectedSetter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `SerializationMetadataNotComputedWhenMemberIgnored` | SourceGeneration.Tests, Tests | some rows also read the output with the DOM |
| `Common/PropertyVisibilityTests.cs` | `Serialize_BasePublicProperty_ConflictWithDerivedPrivate` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicField_ConflictWithBasePublicProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicField_SpecifiedYamlPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicField` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicProperty_ConflictWithBasePublicProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicProperty_SpecifiedYamlPropertyName` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_NewSlotPublicProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_PublicProperty_ConflictWithPrivateDueAttributes` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `Serialize_PublicProperty_ConflictWithPrivateDuePolicy` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `SimpleInterfaceHierarchyWithNamingConflict_ShouldMaskShadowedProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `SimpleInterfaceHierarchy_Serialization_ShouldIncludeBaseInterfaceProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `TestClassWithIgnoredCallbacks` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `TestCollectionWithPrivateElementType` | Tests |  |
| `Common/PropertyVisibilityTests.cs` | `TestDictionaryWithPrivateKeyAndValueType` | Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreAttribute_UnsupportedBigInteger` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreAttribute` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionNever_IgnoreNullValues_True` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionNever` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionSetToAlwaysWorks` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionWhenWritingDefault_ClassProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreConditionWhenWritingDefault_StructProperty` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_Polymorphic` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_InheritedProperties` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_OverridesGlobalJSO` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_PropertyOverridesType` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_Struct` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_WhenWritingDefault` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_WhenWritingNull` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWritingDefault_OnBoxedPrimitive` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWritingDefault_OnInterface` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWritingDefault_OnRootTypes` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWritingDefault_OnValueTypeWithCustomConverter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWritingNull_OnValueTypeWithCustomConverter` | SourceGeneration.Tests, Tests |  |
| `Common/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_WhenWriting` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `AlreadySeenInstance_ShouldNotBeIgnoredOnSiblingBranch_Converter` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `AlreadySeenInstance_ShouldNotBeIgnoredOnSiblingBranch` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `CycleDetectionStatePersistsAcrossContinuations` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_BoxedValueShouldNotBeIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_DerivedType_InArray` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnArray` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnBoxedValueType_AsProperty` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnBoxedValueType_Interface` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnBoxedValueType` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnCollections_WithoutAddMethod` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnCollections` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnExtensionData` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnIDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnList` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnObject_AsProperty` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnObject` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnReadOnlyDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnRecursiveDictionary` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_OnRecursiveList` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_StringShouldNotBeIgnored` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_WhenWritingNull` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Serialize.cs` | `CustomHashCode` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Serialize.cs` | `ExtensionDataDictionaryHandlesPreserveReferences` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.Serialize.cs` | `Preserve_DerivedType_InArray` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ArrayDictionaryLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ArrayLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ArrayObjectLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ArrayPreserveDuplicateDictionaries` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ArrayPreserveDuplicateObjects` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `BoxedStructReferencePreservation_NestedStructCollection` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `BoxedStructReferencePreservation_NestedStructObject` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `BoxedStructReferencePreservation_SiblingPrimitiveValues` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `BoxedStructReferencePreservation_SiblingStructCollections` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `BoxedStructReferencePreservation_SiblingStructObjects` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ClassWithExtensionDataConflictingProperty_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ClassWithIgnoredConflictingProperty_Supported` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `CustomReferenceResolverPersistent` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `CustomReferenceResolver` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryArrayLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryObjectLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryPreserveDuplicateArrays` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryPreserveDuplicateDictionaries` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `DictionaryPreserveDuplicateObjects` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectArrayLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectDictionaryLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectLoop` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectPreserveDuplicateArrays` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectPreserveDuplicateDictionaries` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `ObjectPreserveDuplicateObjects` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `UnicodeDictionaryKeys` | SourceGeneration.Tests, Tests |  |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.cs` | `UnicodePropertyNames` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ChangingPropertiesWithRequiredKeywordToNotBeRequiredAllowsDeserialization` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithCustomRequiredPropertyName_Roundtrip` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordAndLargeParametrizedCtorFailsDeserialization` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordAndSetsRequiredMembersOnCtorWorks` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordAndSmallParametrizedCtorFailsDeserialization` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordDeserialization` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordLargeParametrizedCtorAndSetsRequiredMembersOnCtorWorks` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `ClassWithRequiredKeywordSmallParametrizedCtorAndSetsRequiredMembersOnCtorWorks` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `DerivedClassWithRequiredProperty` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `RemovingPropertiesWithRequiredKeywordAllowsDeserialization` | SourceGeneration.Tests, Tests |  |
| `Common/RequiredKeywordTests.cs` | `RequiredKeywordAndYamlRequiredCustomAttributeWorkCorrectlyTogether` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `CanonicalUnionShape_RoundTripsMixedYamlShapes` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `ClassUnion_DerivingFromOwnCase_ScalarCase_SerializesPayload` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `ClassUnion_DerivingFromOwnCase_SerializesPayloadNotUnionInstance` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `ClassUnion_SubclassCase_ScalarPayload_SerializesPayload` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `ClassUnion_SubclassCase_SerializesPayload` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `ClassUnion_SubclassInstanceAsUnion_SerializesWrappedValue` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `CustomDiscriminatedScalarUnion_IntCase_RoundTripsViaIntCtor` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `CustomDiscriminatedScalarUnion_StringCase_RoundTripsViaStringCtor` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_DoubleNested_SerializesAsLeafValue` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_NullableSelfCase_NullPayload_RoundTripsAsYamlNull` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_NullableSelfCase_RoundTrip_FlattensNesting` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_RecursiveCaseDeclaredFirst_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_RoundTrip_FlattensNesting` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RecursiveUnion_TripleNested_SerializesAsLeafValue` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RoundTrip_DefaultValueOfIntOrBool` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `RoundTrip_DefaultValueOfStructUnion_NoNullableCase` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `SelfReferentialUnion_NonCyclic_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `SingleNullableValueTypeUnion_RoundTripsValueAndNull` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionAttribute_WithDuckTypedObjectValue_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionNumberHandling_CaseConvertersHonorPrecedence` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionNumberHandling_StrictTypeAttributeOverridesGlobalWriteAsString` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionNumberHandling_TypeAttributeAppliesToCaseConverter` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithAsyncEnumerableCase_SerializesUsingStreamingConverter` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithCustomConverterCase_NoClassifier_SerializeWorks` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithDefaultTokenClassification_RoundTripsNullableScalarCaseAsPropertyWithSmallBuffer` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithOverlappingNumericCases_SerializeWorks` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithValueTypeNullableCase_SerializesNonNullAsNumber` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `UnionWithValueTypeNullableCase_SerializesNullAsYamlNull` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `Union_ImplementingOwnCaseInterface_ScalarCase_SerializesPayload` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `Union_ImplementingOwnCaseInterface_SerializesPayloadNotUnionInstance` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `Union_ImplementingOwnCaseInterface_WithClassifier_RoundTrips` | SourceGeneration.Tests, Tests |  |
| `Common/UnionTests.cs` | `YamlUnionAttribute_OnNonUnionType_IsIgnored_RoundTripsAsObject` | SourceGeneration.Tests, Tests |  |
| `Common/UnsupportedTypesTests.cs` | `CompileTimeConverterIsSupported_AsyncEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/UnsupportedTypesTests.cs` | `CompileTimeConverterIsSupported_IntPtr` | SourceGeneration.Tests, Tests |  |
| `Common/UnsupportedTypesTests.cs` | `RuntimeConverterIsSupported_AsyncEnumerable` | SourceGeneration.Tests, Tests |  |
| `Common/UnsupportedTypesTests.cs` | `RuntimeConverterIsSupported_IntPtr` | SourceGeneration.Tests, Tests |  |
| `Common/UnsupportedTypesTests.cs` | `SerializeUnsupportedType` | SourceGeneration.Tests, Tests |  |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateAndSerialize_ClassWithEnabledPolymorphismOnSerialization` | SourceGeneration.Tests, Tests |  |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithFieldAndIgnoreReadOnlyFields` | SourceGeneration.Tests, Tests |  |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyFieldAndIgnoreReadOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyPropertyAndIgnoreReadOnlyFields` | SourceGeneration.Tests, Tests |  |
| `Common/YamlCreationHandlingTests.Object.cs` | `CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithWritablePropertyAndIgnoreReadOnlyProperties` | SourceGeneration.Tests, Tests |  |
| `Common/YamlSchemaExporterTests.cs` | `TestTypes_SerializedValueMatchesGeneratedSchema` | SourceGeneration.Tests, Tests |  |
| `Common/YamlSchemaExporterTests.cs` | `TypeWithDisallowUnmappedMembers_AdditionalPropertiesFailValidation` | SourceGeneration.Tests, Tests |  |
| `Common/YamlSchemaExporterTests.cs` | `UnsupportedType_ReturnsExpectedSchema` | SourceGeneration.Tests, Tests |  |
| `Styml.SourceGeneration.Tests/MetadataContextTests.cs` | `EnsureHelperMethodGenerated_ImplicitPropertyFactory` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/MetadataContextTests.cs` | `EnsureHelperMethodGenerated_TypeFactory` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/MixedModeContextTests.cs` | `HandlesNestedTypes` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/MixedModeContextTests.cs` | `OnSerializeCallbacks_WithCustomOptions` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `ClassWithNullableProperties_Roundtrip` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `GetTypeInfoCalledDuringPolymorphicSerialization` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `HandlesNestedTypes` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `ParameterizedConstructor` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `PositionalRecord` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `PropertyOrdering` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripValueTuple` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomConverterFactoryNullableProperty` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomConverterFactory_Class` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomConverterNullableProperty` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomConverter_Class` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomConverter_Struct` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomPropertyConverterFactory_Class` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundTripWithCustomPropertyConverterFactory_Struct` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundtripWithCustomConverterProperty_Class` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `RoundtripWithCustomConverterProperty_Struct` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `SerializeByteArray` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/RealWorldContextTests.cs` | `SerializeObjectArray_SimpleTypes_WithCustomOptions` | SourceGeneration.Tests | M5a: an element's raw text, `"Hello"` against `Hello` |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_MemberLevel_FallsBackToClrName` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_MemberLevel_FallsBackToClrName` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_MemberLevel_PreventsGlobalPolicyFromApplying` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_MemberLevel_PreventsGlobalPolicyFromApplying` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_FallsBackToClrName` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_FallsBackToClrName` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_PreventsGlobalPolicyFromApplying` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_PreventsGlobalPolicyFromApplying` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | `ClassWithIgnoredAndPrivateMembers_DoesNotIncludeIgnoredMetadata` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | `PocoWithNullableProperties_IgnoresNullValuesWithGlobalSetting` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | `PublicContextAndTestClassWithPropertiesWithDifferentAccessibilities` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/Serialization/PropertyVisibilityTests.cs` | `YamlIgnoreCondition_TypeLevel_Always_ThrowsInvalidOperation` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationContextTests.cs` | `ClassWithNullableProperties_Roundtrip` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationContextTests.cs` | `HandlesNestedTypes` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationContextTests.cs` | `OnSerializeCallbacks` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationContextTests.cs` | `ParameterizedConstructor` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationContextTests.cs` | `PositionalRecord` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/SerializationLogicTests.cs` | `WriterIsFlushedAtRootCall` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/UnspeakableTypeTests.cs` | `CanSerializeUnspeakableRootTypes` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/UnspeakableTypeTests.cs` | `CanSerializeUnspeakableTypesAsBoxedCollectionElements` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/UnspeakableTypeTests.cs` | `CanSerializeUnspeakableTypesAsBoxedDictionaryValues` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/UnspeakableTypeTests.cs` | `CanSerializeUnspeakableTypesAsBoxedProperties` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `ClassWithDictionaryPropertyRoundtrips` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `ClassWithMultipleByteArrayProperties_Roundtrip` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `ClassWithStringValuesRoundtrips` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `CombiningContextWithCustomResolver_ReplacePoco` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `DoesNotReferenceInternalMembersFromOtherAssemblies` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `FastPathSerialization_AppendedResolver_WorksAsExpected` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `FastPathSerialization_EvaluatePropertyOnlyOnceWhenIgnoreNullOrDefaultIsSpecified` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `FastPathSerialization_PrependedResolver_CompatibleResolvers_WorksAsExpected` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `FastPathSerialization_PrependedResolver_IncompatibleResolvers_FallsBackToMetadata` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `FastPathSerialization_ResolvingYamlTypeInfo` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SerializeIgnoreWhenWritingTest` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsAsymmetricNestedConverterWithManyParams` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsBoxedRootLevelValues` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsConstrainedGenericConverterOnGenericType` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsGenericParameterWithCustomConverterFactory` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsGenericWithinNonGenericWithinGenericConverter` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsIgnoreCyclesReferenceHandling` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsNestedGenericConverterOnGenericType` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsOpenGenericConverterOnGenericType_Int` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsOpenGenericConverterOnGenericType_NullValue` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsOpenGenericConverterOnGenericType_String` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsOpenGenericConverterOnProperty` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsPreserveReferenceHandling` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsPropertiesWithCustomConverterFactory` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSerializerContextTests.cs` | `SupportsSingleGenericLevelNestedConverter` | SourceGeneration.Tests |  |
| `Styml.SourceGeneration.Tests/YamlSourceGenerationOptionsTests.cs` | `UseStringEnumConverter_EnablesDefaultStringEnumSerialization` | SourceGeneration.Tests |  |
| `Styml.Tests/NewtonsoftTests/CamelCaseTests.cs` | `BlogPostExample` | Tests |  |
| `Styml.Tests/NewtonsoftTests/CamelCaseTests.cs` | `YamlSerializerCamelCaseSettings` | Tests |  |
| `Styml.Tests/NewtonsoftTests/CustomObjectConverterTests.cs` | `AssertShouldSerializeTest` | Tests |  |
| `Styml.Tests/NewtonsoftTests/CustomObjectConverterTests.cs` | `SerializeAndConvertNullValue` | Tests |  |
| `Styml.Tests/NewtonsoftTests/CustomObjectConverterTests.cs` | `SerializeByteArrayClass` | Tests |  |
| `Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | `BlogCodeSample` | Tests |  |
| `Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | `NullableSerializeUTC` | Tests |  |
| `Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | `SerializeDateTimeOffset` | Tests |  |
| `Styml.Tests/NewtonsoftTests/DateTimeConverterTests.cs` | `SerializeDateTime` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `EnumMemberPlusFlags` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeEnumClassUndefined` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeEnumClass` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeEnumMemberWithDifferentCases` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeEnumWithDifferentCases` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeFlagEnum` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeNegativeEnum` | Tests |  |
| `Styml.Tests/NewtonsoftTests/EnumConverterTests.cs` | `SerializeNegativeFlagsEnum` | Tests |  |
| `Styml.Tests/NewtonsoftTests/ImmutableCollectionsTests.cs` | `SerializeArray` | Tests |  |
| `Styml.Tests/NewtonsoftTests/ImmutableCollectionsTests.cs` | `SerializeList` | Tests |  |
| `Styml.Tests/NewtonsoftTests/ImmutableCollectionsTests.cs` | `SerializeQueue` | Tests |  |
| `Styml.Tests/NewtonsoftTests/ImmutableCollectionsTests.cs` | `SerializeSortedSet` | Tests |  |
| `Styml.Tests/NewtonsoftTests/ImmutableCollectionsTests.cs` | `SerializeStack` | Tests |  |
| `Styml.Tests/NewtonsoftTests/YamlSerializerTests.cs` | `NewPropertyVirtual` | Tests |  |
| `Styml.Tests/NewtonsoftTests/YamlSerializerTests.cs` | `NewProperty` | Tests |  |
| `Styml.Tests/Serialization/Array.ReadTests.cs` | `DoNotDependOnPropertyGetterWhenDeserializingCollections` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteArrayWithEnums` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteByteArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteEmptyByteArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteEmptyObjectArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteEmpty` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteNullByteArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WritePrimitiveArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WritePrimitiveJaggedArray` | Tests |  |
| `Styml.Tests/Serialization/Array.WriteTests.cs` | `WriteTwo2dByteArray` | Tests |  |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `ShouldWorkAtAnyPosition_Sequence` | Tests | as `ShouldWorkAtAnyPosition_Stream` |
| `Styml.Tests/Serialization/ContinuationTests.cs` | `ShouldWorkAtAnyPosition_Stream` | Tests | splices the output into `[ ]` as JSON text; a block mapping in a flow sequence is not YAML, and the reader refuses it. Its rows are built from the output's length (1,994 in leg YJ, 1,924 in leg Y) |
| `Styml.Tests/Serialization/ConvertersForUnsupportedTypesFunctionalTests.cs` | `RoundtripValues` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Array.cs` | `CustomArrayConverterAsRoot` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Array.cs` | `CustomArrayConverterInProperty` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeExtraInformation` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeOnPropertyAndRuntime` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeOnProperty` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeOnTypeAndProperty` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeOnTypeAndRuntime` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `CustomAttributeOnType` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnGenericType_ComplexType` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnGenericType_NestedInClass` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnGenericType_Serialize` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnGenericType_TwoTypeParameters_Failure` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnGenericType_TwoTypeParameters_Success` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttributeOnProperty` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttribute_ConstraintSatisfied_Works` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttribute_DeeplyNestedConverter_Works` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttribute_ManyParamsAsymmetricNesting_Works` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttribute_NestedConverter_Works` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Attribute.cs` | `GenericConverterAttribute_NonGenericOuterConverter_Works` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Callback.cs` | `ConverterWithCallback` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Callback.cs` | `UnsupportedPropertyWithCustomConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.ContravariantDictionaryConverter.cs` | `ClassHavingDictionaryFieldWhichUsingCustomConverterTest` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.ContravariantDictionaryConverter.cs` | `CustomDictionaryConverterContravariant` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DerivedTypes.cs` | `CustomDerivedTypeConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DerivedTypes.cs` | `CustomUnsupportedDictionaryConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DerivedTypes.cs` | `CustomUnsupportedIEnumerableConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryEnumConverter.cs` | `VerifyDictionaryEnumToIntConverterCaseInsensitive` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryEnumConverter.cs` | `VerifyDictionaryEnumToIntConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryEnumConverter.cs` | `VerifyDictionaryEnumToObjectConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryGuidConverter.cs` | `GuidToStringConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryInt32StringConverter.cs` | `IntToStringDictionaryObjectConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryInt32StringKeyValueConverter.cs` | `VerifyDictionaryInt32StringKeyValueConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryKeyValueConverter.cs` | `IntStringKeyValuePairConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.DictionaryKeyValueConverter.cs` | `NestedDictionaryConversion` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Dynamic.Sample.Tests.cs` | `QuotedNumbers_Serialize` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Dynamic.Sample.Tests.cs` | `YamlDynamicTypes_Serialize` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Enum.cs` | `CustomEnumConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Enum.cs` | `NullableCustomEnumConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ComplexReferenceTypeConverter_NoOverride` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ConverterNotCalled_IgnoreNullValues` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `NullableValueTypeConverter_NoOverride` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `NullableValueTypeConverter_OptOut` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ObjectAsCollectionElement` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ObjectAsRootValue` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ReferenceTypeConverter_NoOverride` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ReferenceTypeConverter_NullOptIn` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.HandleNull.cs` | `ReferenceTypeConverter_OptOut` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Interface.cs` | `CustomInterfaceConverter_Serialization` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.KeyConverters.cs` | `CustomKeyConverter_Serialization` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.KeyConverters.cs` | `ExtensionDataProperty_Serialization_IgnoreCustomStringKeyConverter` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.KeyConverters.cs` | `ObjectConverterHandlingStrings_DictionaryWithStringKey_DoesNotCauseStackOverflow` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.List.cs` | `ListConverterClosedGeneric` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.List.cs` | `ListConverterOpenGeneric` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.List.cs` | `ListConverterPolymorphic` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullValueType.cs` | `ConverterForClassThatCanBeNullDependingOnContent` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullValueType.cs` | `ValueConverterForNullableWithYamlConverterAttribute` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullValueType.cs` | `ValueConverterForNullableWithoutYamlConverterAttribute` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullableTypes.cs` | `NullableConverterIsNotPassedNull` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullableTypes.cs` | `ReferenceTypeConverterDoesntGetPassedNull` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullableTypes.cs` | `StructConverter_SaysCanConvertNullableStruct_ConverterOnType` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.NullableTypes.cs` | `StructConverter_SaysCanConvertNullableStruct_StructAsRootType_ConverterOnType` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Object.cs` | `CanCustomizeSystemObjectSerialization` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Object.cs` | `ClassWithFieldHavingCustomConverterTest` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Object.cs` | `CustomSystemObjectConverter_DoesNotUsePolymorphismInAllContexts` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Object.cs` | `SystemObjectNewtonsoftCompatibleConverterSerialize` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomObjectConverterFromRoot` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomObjectConverterInArray` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomValueConverterFromArray` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomValueConverterFromRootAndOptions` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomValueConverterFromRoot` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Point.cs` | `CustomValueConverterStructFromRoot` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Polymorphic.cs` | `PersonConverterPolymorphicTypeDiscriminator` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Polymorphic.cs` | `PersonConverterSerializerPolymorphic` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.Polymorphic.cs` | `PolymorphicConverter_ShouldRoundtripInAllContexts` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.ReadAhead.cs` | `ReadAheadFromArray` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.ReadAhead.cs` | `ReadAheadFromProperties` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.ReadAhead.cs` | `ReadAheadFromRoot` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | `MultipleConvertersInObjectArray` | Tests |  |
| `Styml.Tests/Serialization/CustomConverterTests/CustomConverterTests.cs` | `OptionsArePassedToCreateConverter` | Tests |  |
| `Styml.Tests/Serialization/CyclicTests.cs` | `SimpleTypeCycle` | Tests |  |
| `Styml.Tests/Serialization/DomTests.cs` | `SerializeToDocument_WithEscaping` | Tests | M5a: caused by SerializeToDocument writing in the options' style |
| `Styml.Tests/Serialization/DomTests.cs` | `SerializeToElement_WithEscaping` | Tests | M5a: caused by SerializeToElement writing in the options' style |
| `Styml.Tests/Serialization/DomTests.cs` | `SerializeToNode_WithEscaping` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `ConvertDayOfWeek` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `ConvertEnumUsingAttributes` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `ConvertFileAttributes` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumConverterComposition` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumFlagsWithMemberAttributes_SerializesAsExpected` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumFlagsWithValidMemberNameOverrides` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithConflictingCaseNames_SerializesAsExpected` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithConflictingMemberAttributes_IsTolerated` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithConverterAttribute` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithEmptyStringMemberName_NonFlags_RoundtripsAsExpected` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithMemberAttributes_NoStringEnumConverter_SerializesAsNumber` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithMemberAttributes_StringEnumConverterWithNamingPolicy_NotAppliedToCustomNames` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithMemberAttributes_StringEnumConverter_SerializesAsExpected` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithNoValues` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithOverlappingBitsTests` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `EnumWithValidMemberNameOverrides` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `MoreThan64EnumValuesToSerializeWithNamingPolicy` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `MoreThan64EnumValuesToSerialize` | Tests |  |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `NegativeEnumValue_CultureInvariance` | Tests | RemoteExecutor: the child's `Assert.Equal` on the output |
| `Styml.Tests/Serialization/EnumConverterTests.cs` | `SerilizeDictionaryWhenCacheIsFull` | Tests |  |
| `Styml.Tests/Serialization/EnumTests.cs` | `ToString_ExceedMaxCapacity_ResetsBackToMinimum` | Tests |  |
| `Styml.Tests/Serialization/EnumTests.cs` | `ToString_WithinRange_ReturnsSameValue` | Tests |  |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `ClassWithUnsupportedDictionary` | Tests |  |
| `Styml.Tests/Serialization/ExceptionTests.cs` | `TypeWithBadCtorNoProps` | Tests |  |
| `Styml.Tests/Serialization/InvalidTypeTests.cs` | `ArraySegmentTest` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `NonRequiredMemberCanBeModifiedToRequired` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `Polymorphic_BaseClassWithRequiredDerivedClassWithoutRequiredProperty` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `Polymorphic_BaseClassWithRequiredDerivedHidingRequiredPropertyWithNonRequired` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `Polymorphic_BaseClassWithoutRequiredDerivedClassWithRequiredProperty` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredAttributesGetDetectedAndFailDeserializationWhenValuesNotPresent` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredExtensionDataPropertyCanBeFixedToNotBeExtensionDataWithResolver` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredExtensionDataPropertyCanBeFixedToNotBeRequiredWithResolver` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredMemberCanBeModifiedToNonRequired` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredReadOnlyPropertyCanBeFixedToBeWritableWithResolver` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `RequiredReadOnlyPropertyCanBeFixedToNotBeRequiredWithResolver` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlPropertyInfo.cs` | `StructWithRequiredPropertiesDoesWorkCorrectlyWithYamlRequiredCustomAttribute` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverMultiContextTests.cs` | `SerializationWithYamlTypeInfoWhenTypeInfoResolverSetIsPossible` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverMultiContextTests.cs` | `YamlSerializerSerializeWithTypeInfoOfT` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `AddingNumberHandlingToPropertyIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `AddingShouldSerializeToPropertyIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `ClassWithReadOnlyMembers_SupportedWhenSetManually` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `DefaultIgnoreConditionFromOptionsDoesntFlowToShouldSerializePropertyAndOverrideIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `DefaultIgnoreConditionFromOptionsIsRespectedWhenShouldSerializePropertyIsAssignedAndCleared` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `IgnoreNullValues_Serialization_CanBeInvalidatedByCustomContracts` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `NumberHandlingFromOptionsDoesntFlowToPropertyAndOverrideIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `NumberHandlingFromTypeDoesntFlowToPropertyAndOverrideIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `RemovingOrChangingNumberHandlingFromPropertyIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `RemovingOrChangingShouldSerializeFromPropertyWithIgnoreConditionIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlIgnoreConditionIsCorrectlyTranslatedToShouldSerializeDelegateAndChangingShouldSerializeIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoCustomConverterFactoryIsNotExpandedWhenSetInResolver` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoCustomConverterFactoryIsNotExpanded` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoCustomConverterIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoCustomConverterSetToNullIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoGetIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoGetPropertyNotSerializableButDeserializableWhenNull` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoSetIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlPropertyInfo.cs` | `YamlPropertyInfoSetPropertyDeserializableButNotSerializableWhenNull` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `ClassWithCallBacks_CanCustomizeCallbacks` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `CollectionWithCallBacks_CanCustomizeCallbacks` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `DictionaryWithCallBacks_CanCustomizeCallbacks` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `PropertyOrderIsRespected` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `RecursiveTypeNumberHandling` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `RecursiveTypeWithResolverResolvingOnlyThatType` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `RecursiveTypeWithResolverResolvingOnlyUsedTypes` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `SerializingTypeWithCustomNonSerializablePropertyAndYamlConstructorWorksCorrectly` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `SerializingTypeWithCustomSerializablePropertyAndYamlConstructorWorksCorrectly` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `TypeInfoKindNoneNumberHandlingDirectThroughObject` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `TypeInfoKindNoneNumberHandlingDirect` | Tests |  |
| `Styml.Tests/Serialization/MetadataTests/DefaultYamlTypeInfoResolverTests.YamlTypeInfo.cs` | `TypeInfoKindNoneNumberHandling` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `DefaultIgnoreNullValuesOnWrite` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `InvalidRootOnWrite` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `NullArrayElement` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `NullObjectOutput` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `NullReferences` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `SerializeDictionaryWithNullValues` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `SerializeNullValue_BuiltInConverter` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `WriteNullValue_BuiltInConverter` | Tests |  |
| `Styml.Tests/Serialization/Null.WriteTests.cs` | `WritePocoArray` | Tests |  |
| `Styml.Tests/Serialization/NullableTests.cs` | `ClassWithDictionariesWithNullableValues` | Tests |  |
| `Styml.Tests/Serialization/NullableTests.cs` | `DictionaryWithNullableValue` | Tests |  |
| `Styml.Tests/Serialization/NullableTests.cs` | `EnumerableWithNullableValue` | Tests |  |
| `Styml.Tests/Serialization/NullableTests.cs` | `NullableCustomStructRoundtrip` | Tests |  |
| `Styml.Tests/Serialization/NullableTests.cs` | `RecursiveNullableStruct_Roundtrip` | Tests |  |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadClassWithComplexObjects` | Tests |  |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadSimpleClassWithObjectArray` | Tests |  |
| `Styml.Tests/Serialization/Object.ReadTests.cs` | `ReadSimpleClassWithObject` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `EscapingShouldntStackOverflow` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `WriteObjectAsObject` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `WriteObjectWorks_ReferenceTypeMissingPublicParameterlessConstructor` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `WriteObject_PublicIndexer` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `WritePolymorphicDifferentAttributes` | Tests |  |
| `Styml.Tests/Serialization/Object.WriteTests.cs` | `WritePolymorphicSimple` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyClassWithLargeConstructor` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyClassWithSmallConstructor` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyClass` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyCollection` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyCyclicClass` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyDictionary` | Tests |  |
| `Styml.Tests/Serialization/OnSerializeTests.cs` | `Test_MyStruct` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `CustomEncoderAllowLatin1Supplement` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `ExtensionDataUsesWriterOptions` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `GetTypeInfoGeneric_MutableOptions_CanModifyMetadata` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `GetTypeInfo_MutableOptions_CanModifyMetadata` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `GetTypeInfo_ResultsAreGeneric` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `IndentCharacter` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `NewLine` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `Options_GetConverterForObjectYamlElement_GivesCorrectConverter` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `Options_GetConverter_GivesCorrectCustomConverterAndReadWriteSuccess` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `Options_GetConverter_GivesCorrectDefaultConverterAndReadWriteSuccess` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `Options_RespectNullableAnnotationsDefault_FeatureSwitch` | Tests | RemoteExecutor: the child's `Assert.Equal` on the output |
| `Styml.Tests/Serialization/OptionsTests.cs` | `WriteIndented` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `YamlEncodedTextStringsCustomAllowAll` | Tests |  |
| `Styml.Tests/Serialization/OptionsTests.cs` | `YamlSerializerOptions_Web_SerializesWithExpectedSettings` | Tests |  |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `BackpressureIsObservedWhenWritingYaml` | Tests | pipe thresholds sized for `1`; block writes `1\n`, so the write never resumes and the test hangs |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `BuffersBehaveAsExpected` | Tests |  |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `CanCancelBackpressuredFastPath` | Tests | payload sized from its JSON length against the pipe's thresholds |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `CanCancelBackpressuredYamlWrite` | Tests |  |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `FastPathObservesBackpressure` | Tests | payload sized from its JSON length against the pipe's thresholds |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `FlushesPeriodicallyWhenWritingLargeYaml` | Tests |  |
| `Styml.Tests/Serialization/Pipe.WriteTests.cs` | `ShouldUseFastPathOnSmallPayloads` | Tests |  |
| `Styml.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_MemberLevel_AppliesCustomPolicy` | Tests |  |
| `Styml.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_AppliesCustomPolicy` | Tests |  |
| `Styml.Tests/Serialization/PropertyNameTests.cs` | `YamlNamingPolicyAttribute_CustomDerived_TypeLevel_OverridesGlobalPolicy` | Tests |  |
| `Styml.Tests/Serialization/PropertyOrderTests.cs` | `AfterDefaultOrder` | Tests |  |
| `Styml.Tests/Serialization/PropertyOrderTests.cs` | `BeforeAndAfterDefaultOrder` | Tests |  |
| `Styml.Tests/Serialization/PropertyOrderTests.cs` | `BeforeDefaultOrder` | Tests |  |
| `Styml.Tests/Serialization/SpanTests.cs` | `NullObjectOutput` | Tests |  |
| `Styml.Tests/Serialization/Stream.Collections.cs` | `HandleCollectionsAsync` | Tests | M5a: text surgery on the output (`GetPayloadWithWhiteSpace` widens every double space), which breaks block indentation |
| `Styml.Tests/Serialization/Stream.Collections.cs` | `SerializeEmptyCollection` | Tests |  |
| `Styml.Tests/Serialization/Stream.ReadTests.cs` | `TestBOMWithShortAndLongBuffers` | Tests |  |
| `Styml.Tests/Serialization/Stream.WriteTests.cs` | `RoundTripAsync` | Tests |  |
| `Styml.Tests/Serialization/Stream.WriteTests.cs` | `ShouldUseFastPathOnSmallPayloads` | Tests |  |
| `Styml.Tests/Serialization/Stream.WriteTests.cs` | `WritePrimitivesAsync` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `AddPrefixToEveryPropertyOfClass` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `AppendCharacterWhenSerializingField` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `CombineCustomResolverWithDefault` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `CreateObjectForDictionaryWithDefaults` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `CreateObjectForListWithDefaults` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `CreateObjectWithDefaults` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `DataContractResolverScenario` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `DoNotDeserializePropertyWithNameButSerializeIt` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `DoNotSerializePropertyWithNameButDeserializeIt` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `DoNotSerializeValue42` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `FieldContractResolverScenario` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SetCustomConverterForDictionaryProperty` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SetCustomConverterForIntProperty` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SetCustomConverterForListProperty` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SetCustomNumberHandlingForAProperty` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SetCustomNumberHandlingForAType` | Tests |  |
| `Styml.Tests/Serialization/TypeInfoResolverFunctionalTests.cs` | `SpecifiedContractResolverScenario` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `DateOnly_WriteDictionaryKey_Success` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `DateOnly_Write_Success` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `Int128_AsDictionaryKey_SerializesCorrectly` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `TimeOnly_Write_Success` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `TimeSpan_Write_Success` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `UInt128_AsDictionaryKey_SerializesCorrectly` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `WritePrimitives` | Tests |  |
| `Styml.Tests/Serialization/Value.WriteTests.cs` | `WriteStringWithRelaxedEscaper` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `CanWriteValueToYamlArray` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `NullValueWithNullableSuccess` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `OptionsFollowToConverter` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `SerializeToWriterRoundTripEscaping` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `WriterOptionsSkipValidation` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `WriterOptionsWinEncoder` | Tests |  |
| `Styml.Tests/Serialization/WriteValueTests.cs` | `WriterOptionsWinIndented` | Tests |  |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `SerializeYamlElementArray` | Tests |  |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `SerializeYamlElementArray` | Tests |  |
| `Styml.Tests/SourceGenRegressionTests/Net60RegressionTests.cs` | `SupportsRecursiveTypeSerialization` | Tests |  |
| `Styml.Tests/SourceGenRegressionTests/Net70RegressionTests.cs` | `SupportsRecursiveTypeSerialization` | Tests |  |
| `Styml.Tests/SourceGenRegressionTests/Net80RegressionTests.cs` | `SupportsRecursiveTypeSerialization` | Tests |  |
| `Styml.Tests/SourceGenRegressionTests/Net90RegressionTests.cs` | `SupportsRecursiveTypeSerialization` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Base64_Basic` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Base64_ClearedPartial` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Base64_Reset` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Base64_SplitDataBasic` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Empty` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Flush` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_BadSurrogatePairs` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_BasicSplit` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_Basic` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_ClearedPartial` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_Reset` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_SplitCodePointsReplacement` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf16_SplitInSurrogatePair` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_BasicSplit` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_Basic` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_ClearedPartial` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_Reset` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_Split8CodePointsBasic` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.Values.StringSegment.cs` | `WriteStringValueSegment_Utf8_SplitCodePointsReplacement` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | `WriteRawArrayElements` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | `WriteRawDepthExceedsMaxOf64Fail` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | `WriteRawHonorSkipValidation` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.WriteRaw.cs` | `WriteRawObjectProperty` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `CustomEscaper` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `EscapeSurrogatePairs` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `EscapingTestWhileWritingSurrogate` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `EscapingTestWhileWriting` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FlushToStreamThrows_WriterRemainsInConsistentState` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlContinueShouldSucceed` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `OutputConsistentWithYamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `ResetWithNewOptions_ProducesExpectedOutput` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf16SurrogatePairReplacement_InvalidPropertyName_InvalidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf16SurrogatePairReplacement_InvalidPropertyName_ValidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf16SurrogatePairReplacement_ValidPropertyName_InvalidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf16SurrogatePairReplacement_ValidPropertyName_ValidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf8SurrogatePairReplacement_InvalidPropertyName_InvalidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf8SurrogatePairReplacement_InvalidPropertyName_ValidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf8SurrogatePairReplacement_ValidPropertyName_InvalidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Utf8SurrogatePairReplacement_ValidPropertyName_ValidValue` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBase64DoesNotEscapeLarge` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBase64DoesNotEscape` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBase64String_IndentedFormat` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBase64String_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBoolean_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentValue_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentsInArray_ComparedWithStringLiteral` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentsInObject_ComparedWithStringLiteral` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteDateTimeOffset_TrimsFractionCorrectly` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteDateTime_TrimsFractionCorrectly` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteLiteral_YamlEncodedText_Default` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteLiteral_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNull_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_Decimal` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_Double` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_Int32` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_Int64` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_Single` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_UInt32` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_NullPropertyName_UInt64` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_YamlEncodedText_Default` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumber_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritePropertyName_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteRawValue_WithValidation` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartArrayObject_YamlEncodedText_Default` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartArrayObject_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartArray_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartArray_WithPropertyName_Indented` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEndInvalid` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartObject_NullPropertyName` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartObject_WithPropertyName_Indented` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringDateAndGuid_YamlEncodedText_Default` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringDateAndGuid_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringInvalidCharacter` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_ReadOnlySpanBytesProperty_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_ReadOnlySpanCharProperty_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_StringProperty_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_YamlEncodedTextProperty_NullString` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_YamlEncodedText_Default` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_YamlEncodedText_Large` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringValue_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_DateTimeOffset` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_DateTime` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_Guid` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_ReadOnlySpan_Byte` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_ReadOnlySpan_Char` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_String` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteString_NullPropertyName_YamlEncodedText` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStringsWithRelaxedEscaping` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteWithMinimizedFormatting` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteYamlOnlyWritesToStreamOnDemand_DisposeAsync` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteYamlOnlyWritesToStreamOnDemand_Dispose` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteYamlOnlyWritesToStreamOnDemand_FlushAsync` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteYamlOnlyWritesToStreamOnDemand_Flush` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteYamlWritesToIBWOnDemand_Dispose` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritingNullStringsWithBuggyJavascriptEncoder` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritingNullStringsWithCustomEscaping` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritingStringsWithCustomEscaping` | Tests |  |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `YamlSerializerCacheNotPoisonedByDisposedWriter` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `ReadWriteEscapedPropertyNames` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteAsciiStringAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteAsciiString` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyArrayAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyArray` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyObjectAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEmptyObject` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEscapedNonAsciiStringAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEscapedNonAsciiString` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEscapedStringAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteEscapedString` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteFalseAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteFalse` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteIncredibleDepth` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNonAsciiStringAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNonAsciiString` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNullAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNull` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberAsPropertyWithLargeName` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberDecimalScientific` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberOverpreciseAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberOverprecise` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberScientificAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberScientific` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberTooLargeAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumberTooLargeScientific` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteNumber` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WritePropertyOutsideObject` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteSimpleArray` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteSimpleObjectNeedsEscaping` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteTrueAsProperty` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteTrue` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteValueInsideObject` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteValueSurrogatesEscapeString` | Tests |  |
| `Styml.Tests/YamlElementWriteTests.cs` | `WriteWithRelaxedEscaper` | Tests |  |
| `Styml.Tests/YamlNode/ParseTests.cs` | `InternalValueFields` | Tests |  |
| `Styml.Tests/YamlNode/ParseTests.cs` | `ParseThenEdit` | Tests |  |
| `Styml.Tests/YamlNode/ToStringTests.cs` | `NodeToString` | Tests |  |
| `Styml.Tests/YamlNode/YamlArrayTests.cs` | `AddYamlElement` | Tests |  |
| `Styml.Tests/YamlNode/YamlArrayTests.cs` | `ReplaceWithYamlElement` | Tests |  |
| `Styml.Tests/YamlNode/YamlArrayTests.cs` | `ReplaceWith` | Tests |  |
| `Styml.Tests/YamlNode/YamlArrayTests.cs` | `WriteTo_Options` | Tests |  |
| `Styml.Tests/YamlNode/YamlArrayTests.cs` | `WriteTo` | Tests |  |
| `Styml.Tests/YamlNode/YamlNodeOperatorTests.cs` | `ImplicitOperators_FromProperties` | Tests |  |
| `Styml.Tests/YamlNode/YamlNodeTests.cs` | `Parse_AllowMultipleValues_TrailingContent` | Tests |  |
| `Styml.Tests/YamlNode/YamlNodeTests.cs` | `ReplaceWith` | Tests |  |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `DynamicObject_LINQ_Convert` | Tests |  |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `ListToDictionaryConversions` | Tests | M5a: a substring of the output |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `ReplaceWith` | Tests |  |
| `Styml.Tests/YamlNode/YamlObjectTests.cs` | `WriteTo` | Tests |  |
| `Styml.Tests/YamlNode/YamlValueTests.cs` | `DeserializePrimitive_WriteTo` | Tests |  |
| `Styml.Tests/YamlNode/YamlValueTests.cs` | `QuotedNumbers_Serialize` | Tests |  |
| `Styml.Tests/YamlNode/YamlValueTests.cs` | `WriteTo` | Tests |  |
| `Styml.Tests/YamlPropertyTests.cs` | `WriteObjectValidations` | Tests |  |
| `Styml.Tests/YamlPropertyTests.cs` | `WriteSimpleObject` | Tests |  |

### DOM reads writer output (0 methods since M5a)

**M5a (2026-11-21).** The 256 methods tagged here at M4c lost their tag when `YamlDocument` read block YAML
. Leg Y grew by exactly their rows on net11.0: 5,077 in Styml.Tests and
1,477 in Styml.SourceGeneration.Tests, with no per-method difference. Nine of them still failed, with the DOM no longer
hiding a later assertion. After M4c's three-condition gate, five are under "Exact output"
and three under "Block style behaves differently by design" (each noted "M5a"). `CompareResultsAgainstSerializer` was fixed
instead: a plain source scalar is written back plain (M5a). Two tests that were never tagged here,
`SerializeToDocument_WithEscaping` and `SerializeToElement_WithEscaping`, are under "Exact output" since M5a's unpinning.

### Block style behaves differently by design (90 methods)

| File | Test method | Projects | Note |
|---|---|---|---|
| `Common/AsyncEnumerableTests.cs` | `SerializeAsyncEnumerable_TopLevelValues_LineTerminatorIsAlwaysLineFeed` | Tests | top-level values follow `NewLine`; found on net481, where the unset style's defect wrote CRLF |
| `Common/Ieee754NumericConverterTests.cs` | `NonFiniteValues_WithoutNamedLiterals_Throw` | SourceGeneration.Tests, Tests | NaN and infinity |
| `Common/NumberHandlingTests.cs` | `AttributeOnMember_WinsOver_AttributeOnType` | SourceGeneration.Tests, Tests | NaN and infinity |
| `Common/NumberHandlingTests.cs` | `Attribute_OnRootType_Works` | Tests, SourceGeneration.Tests | M5a: decision 8 |
| `Common/NumberHandlingTests.cs` | `FloatingPointConstants_Pass` | Tests, SourceGeneration.Tests | M5a: decision 8, `.nan` rather than `"NaN"` |
| `Common/NumberHandlingTests.cs` | `ReadFromString_AllowFloatingPoint` | Tests, SourceGeneration.Tests | M5a: decision 8 |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_CustomTypeDiscriminator_MultipleRepeatingValues_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_CustomTypeDiscriminator_RepeatingValue_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_CustomTypeDiscriminator_SingleValue_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_MultipleRepeatingValues_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_RepeatingValue_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/PolymorphicTests.CustomTypeHierarchies.cs` | `ReferencePreservation_SingleValue_Serialization` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`/`$values` |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_DoesNotSupportPreserveSemantics` | SourceGeneration.Tests, Tests | M5b: decision 6, anchors in place of `$id`/`$ref`; the streaming wrappers only (the others return early) |
| `Common/ReferenceHandlerTests/ReferenceHandlerTests.IgnoreCycles.cs` | `IgnoreCycles_DoesNotSupportPreserveSemantics_Polymorphic` | SourceGeneration.Tests, Tests | M5b: decision 6, as above |
| `Common/CollectionTests/CollectionTests.Dictionary.NonStringKey.cs` | `ThrowUnsupported_Serialize` | Tests | M5e: the block writer writes a class, struct or `object` key as a collection key; the JSON-compatible writer keeps the refusal, so it runs in legs YJ and J |
| `Styml.Tests/Serialization/OptionsTests.cs` | `IndentSize` | Tests | indentation |
| `Styml.Tests/Serialization/YamlDocumentTests.cs` | `SerializeYamlDocument` | Tests | M5d, found on net481: it asserts the output's length equals the whitespace-stripped JSON input's; block output (since M5a) matches it only where a line break is one character (11 lines), so it is not a round-trip property |
| `Styml.Tests/Serialization/YamlElementTests.cs` | `SerializeYamlElement` | Tests | M5d, found on net481: it asserts the output's length equals the whitespace-stripped JSON input's; block output (since M5a) matches it only where a line break is one character (11 lines), so it is not a round-trip property |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `DisposeAutoFlushesAsync` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `DisposeAutoFlushes` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `EscapeAsciiCharacters` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `EscapeCharacters` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FixedSizeBufferWriter_DateTimeOffset` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FixedSizeBufferWriter_DateTime` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FixedSizeBufferWriter_Decimal` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FixedSizeBufferWriter_Guid` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FlushEmptyAsync` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FlushEmpty` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FlushMultipleTimesAsync` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `FlushMultipleTimes` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `GrowBeyondBufferSize` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `HighSurrogateMissingGetsReplaced` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InitialState` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidBufferWriter` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidNumbersYaml` | Tests | indentation, NaN and infinity |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidReset` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlDueToWritingMultipleValuesWithComments` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlDueToWritingMultipleValues` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlIncomplete` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlMismatch` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `InvalidYamlPrimitive` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `ResetChangeOutputModeWithNewOptions` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `ResetChangeOutputMode` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `ResetWithNewOptions` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `ResetWithSameOutput` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `Reset` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `UseAfterDisposeInvalidAsync` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `UseAfterDisposeInvalid` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteArrayWithProperty` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBase64String` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteBooleanValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentsInArray` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentsInObject` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCommentsInvalidTextAllowed` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteCustomStrings` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteDateTimeOffsetsValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteDateTimesValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteDoubleValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteFloatValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteGuidsValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteHelloWorldEscaped_AdditionalCases` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteHelloWorldEscaped` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteHelloWorld` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteIntegerValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteInvalidBase64` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteInvalidComment` | Tests | indentation, `*/` in a comment |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteInvalidDepthPartial` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteInvalidPartialYaml` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteLargeToStream` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNullValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueDecimal` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueDouble` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueInt32` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueInt64` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueSingle` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueUInt32` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumberValueUInt64` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteNumbers` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritePartialBase64String` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritePartialHelloWorld` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteSeparateProperties` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteSingleValue` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEndWithPropertyNameArrayDifferentKeyLengths` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEndWithPropertyNameArray` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEndWithPropertyNameObjectDifferentKeyLengths` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEndWithPropertyNameObject` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStartEnd` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WriteStrings` | Tests | indentation; its other rows assert exact output |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritingTooDeepProperty` | Tests | indentation |
| `Styml.Tests/Utf8YamlWriterTests.cs` | `WritingTooDeep` | Tests | indentation |
