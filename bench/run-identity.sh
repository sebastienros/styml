#!/usr/bin/env bash
# The identity outputs: the benchmark's hash, hash-block-mixed, hash-block, quote-dump and check modes, compared byte
# for byte with the references in bench/identity (the reader's events and trees, the block writer's output and its
# quoting decisions, and the cross-library check). A change that alters any output is a change of behaviour.
#   bench/run-identity.sh [folder with Styml.Benchmarks.dll] [output folder]
# quote-dump reads the string sets made by bench/quote-dump-strings.py (see bench/run-block-identity.sh).
set -euo pipefail
folder=${1:-bench/Styml.Benchmarks/bin/Release/net11.0}; out=${2:-artifacts/identity}
mkdir -p "$out"
dotnet "$folder/Styml.Benchmarks.dll" hash > "$out/hash.txt"
dotnet "$folder/Styml.Benchmarks.dll" hash-block-mixed > "$out/hash-block-mixed.txt"
sh bench/run-block-identity.sh "$folder" "$out/run" > /dev/null
mv "$out/run-hash-block.txt" "$out/hash-block.txt"; mv "$out/run-quote-dump.txt" "$out/quote-dump.txt"
dotnet "$folder/Styml.Benchmarks.dll" check > "$out/check.txt" 2>&1
status=0
for f in hash hash-block-mixed hash-block quote-dump check; do
  if cmp -s "$out/$f.txt" "bench/identity/$f.txt"; then echo "$f identical"; else echo "$f DIFFERS"; status=1; fi
done
exit $status
