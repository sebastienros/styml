# bench

The Styml benchmarks (`Styml.Benchmarks`, over the documents in `corpus/`), the identity checks, and the scripts that
measure a change. Each script's header gives its usage.

- **The README's tables and charts:** `run-panel.sh` runs the panel; `report-panel.py` makes the tables of
  [docs/benchmarks](../docs/benchmarks/README.md) and the pivot tables of the README and the package readme, and
  `charts/generate_charts.py` the charts (adapted from Fluid's chart generator), from its results.
- **The identity outputs:** `run-identity.sh` compares the benchmark's `hash`, `hash-block-mixed`, `hash-block`,
  `quote-dump` and `check` outputs with the references in `identity/`; a change that alters any of them changes
  behaviour.
- **`corpus/`:** the documents, generated from a fixed seed by `corpus/generate.cs`, each in YAML and as a JSON twin.

## How BenchmarkDotNet runs here

- **Job:** every BenchmarkDotNet run uses the ShortRun job (`Config.Create`) or something shorter, such as the `dry`
  argument; precision comes from repeating the measurement, not from longer jobs. `STYML_BENCH_LAUNCHES` adds
  launches to the same job where a table needs them (the README panel uses 3).
- **Error columns:** tables are reported with their error column rather than firmed up by a longer run.
- **Documents:** an indicative table may cover a subset of the documents (`STYML_BENCH_DOCS`), if it says which.

## Comparing two builds

A change's effect is measured by a DLL swap: the same benchmark harness with two `Styml.dll`s, run alternately in one
sitting (a run of interleaved rounds: base, change, base), so that drift on the machine shows in the base-against-base
pair.

| Script | What it does |
|---|---|
| `build-base.sh` | Builds a side folder: this tree's harness with the `Styml.dll` of an older commit (`STYML_HISTORY_REPO`). |
| `run-m4-sides.sh`, `report-m4-g1.py` | The DLL swap over side folders, 7 rounds of the `gate3-row` mode, and its per-row summary. |
| `run-m4a-g3.sh`, `report-m4a-g3.py` | Block style against System.Text.Json and the YAML libraries, by interleaved `gate3-row` runs. |
| `run-m4b-reader-deserialize.sh`, `report-m4b-reader.py` | The deserialize rows of two commits in one BenchmarkDotNet sitting. |
| `run-m3c-*.sh`, `report-m3c-*.py` | The single- and multi-segment reader rows of two commits. |
| `run-m6a.sh`, `run-m6b.sh`, `report-m6a.py`, `report-m6b.py` | Every scenario class, one BenchmarkDotNet invocation each, with the machine's load logged. |
| `profile-top.py`, `xctrace-top.py` | The hottest methods of a `dotnet-trace` or Instruments profile of the benchmark's `profile` mode. |

## Results

The scripts write their results to `results/<set>/`, which the repository does not track.
