#!/bin/sh
# M4b's reader step: the before/after of the deserialize rows with AcceptLenientScalars
# off, because M3c's gate 3 rows (bench/run-m3c-sides.sh) are Tokenize rows only. BenchmarkDotNet in ShortRun (see
# bench/README.md), out of process, alternately in one sitting, class by class: baseline, head, baseline, head,
# with the load logged. Only the Styml rows run (the other libraries' code is the same on both sides). The baseline is a
# git worktree of the commit given as $1 (default ff2eeb4, the last product commit before the step) in
# artifacts/m4b-reader-base. Results go to bench/results/M4b/reader/deserialize/{baseline,head}-{1,2}/net11.0.
# Summarise with: python3 bench/report-m4b-reader.py. Usage, from the repository root:
#   sh bench/run-m4b-reader-deserialize.sh [baseline commit]
# Old commits live in the maintainers' history repository, not in this one: STYML_HISTORY_REPO names it (required).
set -u
cd "$(dirname "$0")/.."
head="$(pwd)"
base="$head/artifacts/m4b-reader-base"
out=M4b/reader/deserialize
log="$head/bench/results/$out/load.log"
mkdir -p "$head/bench/results/$out"
history="${STYML_HISTORY_REPO:?set STYML_HISTORY_REPO to the repository that holds the baseline commit}"
[ -d "$base" ] || git -C "$history" worktree add --detach "$base" "${1:-ff2eeb4}"
(cd "$base" && dotnet build bench/Styml.Benchmarks -c Release -f net11.0 > /dev/null) || exit 1
dotnet build bench/Styml.Benchmarks -c Release -f net11.0 > /dev/null || exit 1
for class in DeserializeSourceGen DeserializeReflection FromString MinifiedJson; do
  case $class in
    MinifiedJson) filter="Styml.Benchmarks.MinifiedJson.Deserialize_Styml*" ;;
    *) filter="Styml.Benchmarks.$class.Styml_*" ;;
  esac
  for round in 1 2; do
    for side in baseline head; do
      dir="$head"; [ "$side" = baseline ] && dir="$base"
      # As in run-m3c-gate3.sh: BenchmarkDotNet finds the project by name under the repository root, which holds both
      # copies, so the other side's project file is hidden while one side runs.
      mine="$dir/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"
      other="$head/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"; [ "$side" = head ] && other="$base/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"
      [ -f "$mine.off" ] && mv "$mine.off" "$mine"
      [ -f "$other" ] && mv "$other" "$other.off"
      echo "$(date '+%F %T') $class $side-$round start: $(uptime | sed 's/.*load averages*: //')" >> "$log"
      (cd "$dir" && STYML_BENCH_RESULTS="$out/$side-$round" dotnet run -c Release -f net11.0 --no-build --project bench/Styml.Benchmarks -- --filter "$filter")
      rc=$?
      echo "$(date '+%F %T') $class $side-$round end ($rc): $(uptime | sed 's/.*load averages*: //')" >> "$log"
    done
  done
done
for f in "$head/bench/Styml.Benchmarks/Styml.Benchmarks.csproj" "$base/bench/Styml.Benchmarks/Styml.Benchmarks.csproj"; do
  [ -f "$f.off" ] && mv "$f.off" "$f"
done
# The baseline's results are written under its own tree; copy them next to the head's.
for round in 1 2; do
  mkdir -p "$head/bench/results/$out/baseline-$round"
  cp -R "$base/bench/results/$out/baseline-$round/." "$head/bench/results/$out/baseline-$round/"
done
