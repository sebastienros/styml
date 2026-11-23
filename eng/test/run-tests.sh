#!/usr/bin/env bash
# Builds and runs test projects for one TFM with upstream's default filter; writes
# artifacts/testresults/<Project>.<tfm>.trx.
# The legs (from M4c; leg J and the JSON grammar were deleted in
# M7a, decision D8):
# - Y (default): the writer's block default; excludes category=jsonoutput (M4c). Results in artifacts/testresults.
# - YJ (STYML_TEST_WRITER=json): the JSON-compatible writer default (the writer's AppContext switch); every test.
#   Results in artifacts/testresults-yj.
# Usage: eng/test/run-tests.sh <tfm> [test/<Project>/<Project>.csproj ...]   (default: the imported test projects)
# Extra arguments for 'dotnet test' can be passed in STYML_TEST_ARGS, and for 'dotnet build' in STYML_BUILD_ARGS (CI
# passes its build step's properties, so that the leg's build is incremental and the packed assembly keeps them). STYML_TEST_CONFIGURATION picks the build
# configuration (default Release; M7c's Debug leg is Y with Debug, so that the product's Debug.Asserts run).
# A test that runs longer than STYML_TEST_HANG_TIMEOUT (default 5m) fails the leg, which names it, instead of stalling
# it (M4c: a byte-count test deadlocked on a pipe once the default flipped).
set -u
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"
tfm=$1; shift
config="${STYML_TEST_CONFIGURATION:-Release}"
projects=("$@")
if [ ${#projects[@]} -eq 0 ]; then
  projects=(test/Styml.Tests/Styml.Tests.csproj
            test/Styml.SourceGeneration.Tests/Styml.SourceGeneration.Tests.csproj
            test/Styml.SourceGeneration.Unit.Tests/Styml.SourceGeneration.Unit.Tests.csproj)
fi
filter="category!=OuterLoop&category!=failing"
results=artifacts/testresults
if [ "${STYML_TEST_WRITER:-}" = "json" ]; then
  results=artifacts/testresults-yj
else
  filter="$filter&category!=jsonoutput"
fi
results="${STYML_RESULTS_DIR:-$results}"
mkdir -p "$results"
status=0
for p in "${projects[@]}"; do
  n=$(basename "$p" .csproj)
  rm -f "$results/$n.$tfm.trx"
  # A leg whose build fails runs nothing: with --no-build, 'dotnet test' would run the assembly a previous build left
  # behind (M4b: a net10.0 leg once ran a stale test assembly).
  log="$(mktemp)"
  if ! dotnet build "$p" -c "$config" -f "$tfm" ${STYML_BUILD_ARGS:-} > "$log" 2>&1; then
    grep -E ' error ' "$log" | sort -u | head -20
    echo "Build FAILED: $n ($tfm); the leg is not run."
    rm -f "$log"
    status=1
    continue
  fi
  grep -E 'Build succeeded' "$log" | sort -u | head -1
  rm -f "$log"
  dotnet test "$p" -c "$config" -f "$tfm" --no-build --filter "$filter" \
    --logger "trx;LogFileName=$n.$tfm.trx" --results-directory "$results" \
    --blame-hang-timeout "${STYML_TEST_HANG_TIMEOUT:-5m}" --blame-hang-dump-type none ${STYML_TEST_ARGS:-} > "$results/$n.$tfm.console.log" 2>&1 || status=1
  grep -E '^(Passed|Failed)!|Total tests' "$results/$n.$tfm.console.log" | head -3
done
exit $status
