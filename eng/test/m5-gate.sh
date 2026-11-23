#!/usr/bin/env bash
# The M5 fast-protocol gate.
#   eng/test/m5-gate.sh <run> [legs] [project...]
# STYML_GATE_TFM picks the framework of the legs (default net11.0; M5's end gates also run net10.0).
# Legs default to "Y YJ" (Y: the block writer default; YJ: STYML_TEST_WRITER=json; leg J went with the JSON grammar in
# M7a), projects to
# the five test projects. Results go to artifacts/m5/<run>: legs.log, each leg's output, and the trx files.
#
# Every target is built first: src/Styml's three, and each test project's (net481 included, compile only on a Mac). If
# any build fails, no leg is run (M5c: two compile errors once reached a run that did not build them).
set -u
cd "$(dirname "$0")/../.."
run=$1; legs=${2:-"Y YJ"}; shift; [ $# -gt 0 ] && shift
p=("$@")
[ ${#p[@]} -eq 0 ] && p=(test/Styml.Tests/Styml.Tests.csproj test/Styml.SourceGeneration.Tests/Styml.SourceGeneration.Tests.csproj test/Styml.SourceGeneration.Unit.Tests/Styml.SourceGeneration.Unit.Tests.csproj test/Styml.Yaml.Tests/Styml.Yaml.Tests.csproj test/Styml.Parity.Tests/Styml.Parity.Tests.csproj)
out=artifacts/m5/$run; rm -rf "$out"; mkdir -p "$out"
echo "# $(git log -1 --format=%h), src diff $(git diff -- src | shasum | cut -c1-12), test diff $(git diff -- test | shasum | cut -c1-12); $(date '+%T')" > "$out/legs.log"

for proj in src/Styml/Styml.csproj test/Styml.Tests/Styml.Tests.csproj test/Styml.SourceGeneration.Tests/Styml.SourceGeneration.Tests.csproj test/Styml.SourceGeneration.Unit.Tests/Styml.SourceGeneration.Unit.Tests.csproj test/Styml.Yaml.Tests/Styml.Yaml.Tests.csproj test/Styml.Parity.Tests/Styml.Parity.Tests.csproj; do
  log="$out/build-$(basename "$proj" .csproj).log"
  if ! dotnet build "$proj" -c Release -v q -nologo > "$log" 2>&1; then
    echo "BUILD FAILED: $proj (all targets); no leg is run" >> "$out/legs.log"
    grep -E " error " "$log" | sed 's/\[.*//' | sort -u | head -5 >> "$out/legs.log"
    cat "$out/legs.log"; exit 1
  fi
  echo "built: $proj (all targets)" >> "$out/legs.log"
done

for leg in $legs; do
  unset STYML_TEST_WRITER
  [ "$leg" = YJ ] && export STYML_TEST_WRITER=json
  STYML_RESULTS_DIR="$out/$leg" eng/test/run-tests.sh "${STYML_GATE_TFM:-net11.0}" "${p[@]}" > "$out/$leg.out" 2>&1
  echo "== $leg exit=$?" >> "$out/legs.log"
  grep -h -E "Build FAILED|Passed!|Failed!| error |Aborted" "$out/$leg.out" | sort -u >> "$out/legs.log"
done
cat "$out/legs.log"
