#!/usr/bin/env bash
# M4p: the block style's identity checks for one benchmark folder: its output and its quoting decisions.
#   bench/run-block-identity.sh <folder with Styml.Benchmarks.dll> <output prefix>
# writes <prefix>-hash-block.txt and <prefix>-quote-dump.txt; compare two prefixes with diff.
# The string sets are made once by: python3 -I bench/quote-dump-strings.py artifacts/quote-dump
# (and artifacts/crossread/strings.json copied there as crossread.json).
set -euo pipefail
folder=$1; prefix=$2; strings=${STRINGS:-artifacts/quote-dump}
dotnet "$folder/Styml.Benchmarks.dll" hash-block > "$prefix-hash-block.txt"
for s in short keywords timestamps psych extra crossread; do
  dotnet "$folder/Styml.Benchmarks.dll" quote-dump "$strings/$s.json"
done > "$prefix-quote-dump.txt"
echo "$(wc -l < "$prefix-hash-block.txt") hash-block lines, $(grep -vc '^#' "$prefix-quote-dump.txt") quote-dump lines"
