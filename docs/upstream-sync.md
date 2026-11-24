# Keeping in step with System.Text.Json

Styml is System.Text.Json from [dotnet/runtime](https://github.com/dotnet/runtime), imported, renamed and then
changed for YAML (`UPSTREAM.md`). This page is the procedure for knowing which upstream commit Styml contains, seeing
what changed upstream since, and bringing those changes in.

## The pieces

| File | What it is |
|---|---|
| `eng/upstream/baseline.json` | **The record**: repository, followed line, ref and commit Styml is in step with, date of the last sync, and pointers to the imported paths (`eng/import/manifest.txt` minus `exclude.txt`). |
| `eng/upstream/baseline-tree.txt` | The renamed upstream tree at that commit: one blob SHA-1 per file, as `eng/import` + `eng/rename` produce it. The base of every three-way merge and of the "edited by Styml" test. Written by the sync, never by hand. |
| `eng/upstream/counterparts.json` | Hand-maintained: renamed files that live elsewhere in Styml (`moved`, e.g. the csprojs), files Styml removed and why (`removed`), and the upstream areas Styml replaced with YAML code (`replaced_areas`). |
| `UPSTREAM.md` | The opening table (Ref, Commit, Synced) and the **Syncs** section, one line per sync. |
| `eng/upstream/check` | Fails if the record, the tree list, `counterparts.json` and `UPSTREAM.md` disagree. Runs in the Build workflow on every pull request. |
| `eng/upstream/verify-replay <runtime-clone>` | Replays import and rename of the recorded commit in a scratch directory and compares every file with `baseline-tree.txt`. |
| `eng/upstream/status <runtime-clone> [ref]` | The report: upstream commits since the baseline that touch the imported paths, and every changed file in one of three groups. |
| `eng/upstream/sync <runtime-clone> <ref>` | The three-way merge by replay. |
| `.github/workflows/upstream.yml` | Weekly (and on demand): runs the report against the followed line and keeps one issue per baseline up to date. |

All of it reads the runtime clone through git's object database only (`rev-parse`, `log`, `diff`, `ls-tree`,
`archive`); nothing is written to the clone, and replays run in a temporary directory. The tools need Python 3, git, and
for replays the .NET SDK of `global.json` (pass B is a file-based C# app).

## Which upstream line to follow

`follow` in `baseline.json` decides what "upstream" means. It is the owner's choice, made once and changed by editing
the record:

- **Servicing tags of the baseline's release** (the default): `{"line": "servicing", "tags": "^v11\\.0\\.[0-9]+$"}`.
  The highest `v11.0.N` tag is the target. Servicing releases carry bug and security fixes only, so each sync is small
  (a few files, usually no API) and can follow every Patch Tuesday. Styml stays on the System.Text.Json of .NET 11
  until the owner moves to the next line.
- **`main`, or the next major release's tag**: `{"line": "main", "ref": "main"}`, or
  `{"line": "major", "tags": "^v12\\.0\\.0$"}`. This brings features (new APIs, new converters, new attributes)
  as they land. Expect a planned merge per release rather than a routine one: in the three months after `v11.0.0`,
  `main` changed about 100 imported files, a dozen of which conflict with Styml's YAML work and several more that
  merge cleanly but need a YAML design (see "No counterpart" below). Following `main` between releases means
  re-merging moving code; syncing to the major tag once it ships is usually less work.

The two can be combined: follow servicing during a release, then sync once to the next major tag and switch the
pattern to its servicing tags.

## The report

```bash
eng/upstream/status ~/github/runtime              # the followed line
eng/upstream/status ~/github/runtime main         # any ref; --output FILE, --summary-json FILE
```

The clone needs the baseline commit and the target (`git fetch` with tags). The report is Markdown:

- **No counterpart in Styml**: a new upstream file (the sync adds it), or a file Styml deleted or folded into its own
  project files (the sync leaves it out; `counterparts.json` says which and why).
- **Edited by Styml**: Styml's file differs from the renamed baseline; the sync merges three-way and may conflict.
- **Not edited by Styml**: Styml's file is the renamed baseline byte for byte; the sync applies the change as is.
- **For a person to judge**: the commits that touch the first group or a `replaced_areas` path (the reader, document
  parsing, the writer), with their subjects.
- Commits whose change to the imported paths is patch-equivalent to one the baseline already has (backports) are
  listed apart and need nothing.

## The sync

```bash
eng/upstream/sync ~/github/runtime v11.0.1        # merge; leaves conflict markers
# resolve, build, test (below)
eng/upstream/sync --continue                       # commits the merge with the record
eng/upstream/sync --abort                          # or: restores every file the sync touched
```

Start from a clean working tree on a new branch. The sync:

1. Replays the **baseline** (import, `verify.sh`, rename passes A and B) with today's tooling into a scratch
   directory and checks it against `baseline-tree.txt`. A difference means the tooling changed since the last sync;
   nothing is merged until `eng/upstream/verify-replay` passes again.
2. Replays the **target** the same way, with `--allow-missing` (manifest entries upstream deleted are reported, not
   fatal) and `--update-counts`: if the rename's guards count differently (`protect.json`'s protected tokens,
   `passb.json`'s emitter and whole-literal names), it prints the change and stops. Check each new occurrence, then
   rerun with `--accept-counts`; the new counts become part of the merge.
3. For each file that differs between the two renamed trees: applies it if Styml has not edited the file, merges it
   with `git merge-file` (Styml / upstream base / upstream target) if Styml has, adds new upstream files, deletes files
   upstream deleted that Styml left alone, and lists the rest (deleted upstream but edited here, binary, no
   counterpart) without touching them.
4. Prints a summary and keeps its state in `.git/styml-upstream-sync`.

`--continue` refuses while a touched file has conflict markers or the manifest still names a file the target does
not have. Then it writes `baseline.json` (ref, commit, today's date in UTC), `baseline-tree.txt` (the renamed target),
the `UPSTREAM.md` table and a new Syncs line, runs `check`, and commits everything, your resolutions included, as one
commit. The record therefore only changes in the commit that brings the code in. `--keep-scratch` keeps both
replayed trees for inspection.

### Usual conflicts

| Where | What happens | Resolution |
|---|---|---|
| `Utf8YamlReader.cs`, `Utf8YamlReader.MultiSegment.cs`, `YamlReaderHelper.cs` | Upstream edits inside the JSON lexer, which Styml deleted: the conflict is the whole deleted region against a small upstream change. | Keep Styml's side. Port the upstream change only where it is in code Styml kept (constructors, `ValueTextEquals`, `TryGet*`), and judge whether the YAML lexer (`Utf8YamlReader.Yaml*.cs`) needs the same fix. |
| `Styml.csproj`, `test/*/Styml.*.csproj` | Upstream project files map onto Styml's own (`counterparts.json` `moved`); their layout differs. | Port the intent by hand: a new linked `$(CommonPath)` file also needs a `manifest.txt` line; runtime-only properties are dropped. |
| A `.targets` upstream shares between projects | No counterpart: Styml folded them into its csprojs. | Port by hand if it matters outside the runtime build. |
| Product code Styml changed for YAML (`YamlHelpers.cs`, converters, `YamlPropertyInfo.cs`, `YamlSerializer.Read.*.cs`, nodes) | Both sides edited the same lines. | Merge both intents; the UPSTREAM.md deviation tables say why Styml's line is the way it is. |
| Deleted upstream, edited here | Listed, the file is kept. | Usually take the deletion (`git rm`) after checking Styml's edit is not needed elsewhere. |
| Tests | Upstream tests that write JSON text. | The test projects run with `category!=jsonoutput` by default; tag a new test that asserts JSON output text with that category (see `test/EXCLUSIONS.md`) rather than editing its expectation. |
| `eng/import/manifest.txt` | A linked upstream file was removed or added. | Remove deleted entries (and the csproj items); add entries for files the target's csprojs newly link. The sync cannot discover new links. |

A clean textual merge is not a correct one: an upstream fix can merge without a conflict over code that behaves
differently in YAML (duplicate keys, metadata names, extension data, number and string forms). Read every commit in
the report's "For a person to judge" list and the edited group's commits before resolving.

### The "no counterpart" group

There is nothing to merge these into: the sync adds new upstream files as they are and leaves changes to files
Styml removed out. Either way a person decides what Styml needs. For each:

- **New upstream file** under an imported directory (a converter, a test file): the sync adds it. Styml's projects
  list every source file (`EnableDefaultItems` is off), so check that the `Compile` item came in with the merged
  csproj change. Then check that it reads and writes YAML correctly and that its tests run in the default legs or
  carry `jsonoutput`.
- **File Styml removed** (`counterparts.json` `removed`): the JSON lexer's files (`ConsumeNumberResult.cs`,
  `ConsumeTokenResult.cs`, the lexer search helpers) and the shared `.targets`. A fix to number parsing, string
  unescaping, depth or size limits, or token validation there may need an equivalent in the YAML reader. Write it in
  `Utf8YamlReader.Yaml*.cs` / `YamlReaderHelper.Yaml*.cs` with a test, or record in the sync commit why YAML does not
  need it.
- **Replaced areas** (`replaced_areas`): the same judgement for files that still exist but whose JSON code paths Styml
  replaced.

Add a row to the relevant UPSTREAM.md deviation table for every hand-made equivalent.

### Gates after a sync

Run before `--continue` (the build and the legs) and after it (the record):

```bash
dotnet build -c Release                                   # every project, every TFM
eng/test/run-tests.sh net11.0                             # leg Y
STYML_TEST_WRITER=json eng/test/run-tests.sh net11.0      # leg YJ
eng/test/run-tests.sh net10.0
eng/test/m5-gate.sh sync-<ref>                            # the five test projects, legs Y and YJ
eng/test/run-net481-remote.sh Styml.Tests                 # net481 on a Windows machine (STYML_REMOTE_HOST)
eng/upstream/check
eng/upstream/verify-replay ~/github/runtime               # the new baseline replays byte for byte
```

- **Public API**: `test/Styml.Yaml.Tests/PublicApiTests.cs` fails if the public surface changed. Each addition
  upstream made is reviewed (does it make sense for YAML?) and listed in a new
  `test/Styml.Yaml.Tests/PublicApi/<sync>-additions.txt`; `eng/test/public-api/README.md` has the listing tool.
- Compare the per-method outcomes with the run before the sync (`eng/test/trx-diff.py`): new upstream tests should
  pass or be excluded with a reason in `test/EXCLUSIONS.md`, and no existing test should change outcome.
- Update `README.md` and `THIRD-PARTY-NOTICES.txt` where they name the upstream version.
