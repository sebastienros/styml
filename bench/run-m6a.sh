#!/bin/sh
# Runs the M6a benchmarks: one BenchmarkDotNet invocation per scenario class, so that a
# crash or a noisy stretch costs one class, on net11.0 (net10.0 on request). The load average is logged before and after each
# class to bench/results/M6a/load.log. Usage, from the repository root: sh bench/run-m6a.sh [tfm ...]
set -u
cd "$(dirname "$0")/.."
tfms="${*:-net11.0}"
log=bench/results/M6a/load.log
mkdir -p bench/results/M6a
dotnet build bench/Styml.Benchmarks -c Release > /dev/null || exit 1
for tfm in $tfms; do
  for class in MinifiedJson Tokenize TokenizeText DeserializeReflection DeserializeSourceGen FromString; do
    echo "$(date '+%F %T') $tfm $class start: $(uptime | sed 's/.*load averages*: //')" >> "$log"
    dotnet run -c Release -f "$tfm" --no-build --project bench/Styml.Benchmarks -- --filter "Styml.Benchmarks.$class.*"
    rc=$?
    echo "$(date '+%F %T') $tfm $class end ($rc): $(uptime | sed 's/.*load averages*: //')" >> "$log"
  done
done
