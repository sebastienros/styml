#!/bin/sh
# M3c gate 3: the single-segment classes measured alternately in one
# sitting, class by class: baseline, head, baseline, head, with the load logged. The baseline is a git worktree of
# the commit given as $1 (default a1d8b01, src/ as at 1685855) in artifacts/m3c-baseline. Results go to
# bench/results/M3c-gate3/{baseline,head}-{1,2}/net11.0. Usage, from the repository root: sh bench/run-m3c-gate3.sh
# Old commits live in the maintainers' history repository, not in this one: STYML_HISTORY_REPO names it (required).
set -u
cd "$(dirname "$0")/.."
head="$(pwd)"
base="$head/artifacts/m3c-baseline"
log="$head/bench/results/M3c-gate3/load.log"
mkdir -p "$head/bench/results/M3c-gate3"
history="${STYML_HISTORY_REPO:?set STYML_HISTORY_REPO to the repository that holds the baseline commit}"
[ -d "$base" ] || git -C "$history" worktree add --detach "$base" "${1:-a1d8b01}"
(cd "$base" && dotnet build bench/Styml.Benchmarks -c Release -f net11.0 > /dev/null) || exit 1
dotnet build bench/Styml.Benchmarks -c Release -f net11.0 > /dev/null || exit 1
for class in Tokenize MinifiedJson DeserializeSourceGen; do
  for round in 1 2; do
    for side in baseline head; do
      dir="$head"; [ "$side" = baseline ] && dir="$base"
      # BenchmarkDotNet finds the benchmark project by name under the repository root, which holds both copies (the
      # baseline worktree is under artifacts/): the other side's project file is hidden while one side runs.
      mine="$dir/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"
      other="$head/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"; [ "$side" = head ] && other="$base/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"
      [ -f "$mine.off" ] && mv "$mine.off" "$mine"
      [ -f "$other" ] && mv "$other" "$other.off"
      echo "$(date '+%F %T') $class $side-$round start: $(uptime | sed 's/.*load averages*: //')" >> "$log"
      (cd "$dir" && STYML_BENCH_RESULTS="M3c-gate3/$side-$round" dotnet run -c Release -f net11.0 --no-build --project bench/Styml.Benchmarks -- --filter "Styml.Benchmarks.$class.*")
      rc=$?
      echo "$(date '+%F %T') $class $side-$round end ($rc): $(uptime | sed 's/.*load averages*: //')" >> "$log"
    done
  done
done
for d in "$base" "$head"; do
  [ -f "$d/bench/Styml.Benchmarks/Styml.Benchmarks.csproj.off" ] && mv "$d/bench/Styml.Benchmarks/Styml.Benchmarks.csproj.off" "$d/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"
done
# The baseline's results were written in its worktree.
mkdir -p "$head/bench/results/M3c-gate3"
for round in 1 2; do
  rm -rf "$head/bench/results/M3c-gate3/baseline-$round"
  cp -R "$base/bench/results/M3c-gate3/baseline-$round" "$head/bench/results/M3c-gate3/baseline-$round"
done
