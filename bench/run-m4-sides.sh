#!/bin/sh
# M4 gate G1: the dll swap of M3c (bench/run-m3c-sides.sh) over folders
# under artifacts/m4-swap, each a copy of one bench/Styml.Benchmarks build (Release, net11.0) with a different
# Styml.dll. 'base' holds Styml.dll built from 9835d4d. SIDES names the folders run in each round, in order; the first
# and last must be 'base' (printed 'baseline' and 'baseline2', the A/A pair). ROUNDS defaults to 7. Each run is one
# process of the benchmark's 'gate3-row' mode (one second of warm-up, best of 40 batches of at least 2 ms, microseconds
# per call). Without arguments, the rows are the 48 Styml rows of G1. Summarise with:
#   python3 bench/report-m4-g1.py <output>
# SWAP names another folder of sides (M5's end sitting: artifacts/m5-swap).
# Usage, from the repository root: SIDES="base head base" sh bench/run-m4-sides.sh [Class:Method:Document ...] > out.txt
set -u
cd "$(dirname "$0")/.."
w=${SWAP:-artifacts/m4-swap}
sides=${SIDES:-"base head base"}
rounds=${ROUNDS:-7}
if [ $# -eq 0 ]; then
  for d in workflow-s compose-s k8s-s records-s workflow-m compose-m k8s-m openapi-m records-m openapi-l k8s-l records-l; do
    set -- "$@" "Write:Styml:$d" "Write:Styml_Indented:$d" "SerializeReflection:Styml:$d" "SerializeSourceGen:Styml:$d"
  done
fi
for s in $sides; do echo "# $s $(shasum "$w/$s/Styml.dll" | cut -c1-8)"; done | sort -u
run() { dotnet "$w/$1/Styml.Benchmarks.dll" gate3-row "$2" "$3" "$4" 2>&1 | tail -1; }
echo "# $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
r=1
while [ "$r" -le "$rounds" ]; do
  for row in "$@"; do
    c=$(echo "$row" | cut -d: -f1); m=$(echo "$row" | cut -d: -f2); d=$(echo "$row" | cut -d: -f3)
    seen=0
    for s in $sides; do
      label=$s
      if [ "$s" = base ]; then
        if [ "$seen" -eq 0 ]; then label=baseline; else label=baseline2; fi
        seen=1
      fi
      echo "$r $label $(run "$s" "$c" "$m" "$d")"
    done
  done
  echo "# round $r done $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
  r=$((r + 1))
done
