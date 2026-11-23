#!/bin/sh
# M3c gate 3: the interleaved
# measurement of flagged rows with one benchmark build and two Styml.dll files. artifacts/m3c-swap/head is the head's
# bench/Styml.Benchmarks output; artifacts/m3c-swap/base is the same output with the baseline worktree's Styml.dll
# (artifacts/m3c-baseline, see run-m3c-gate3.sh). Both must be built first (Release, net11.0). Each row runs seven
# rounds of baseline, head, baseline through the 'gate3-row' mode. Summarise with:
#   python3 bench/report-m3c-rows.py <output> --time
# Usage, from the repository root: sh bench/run-m3c-dllswap.sh Class:Method:Document ... > output.txt
set -u
cd "$(dirname "$0")/.."
w=artifacts/m3c-swap
rm -rf "$w"; mkdir -p "$w"
cp -R bench/Styml.Benchmarks/bin/Release/net11.0 "$w/head"
cp -R bench/Styml.Benchmarks/bin/Release/net11.0 "$w/base"
cp artifacts/m3c-baseline/bench/Styml.Benchmarks/bin/Release/net11.0/Styml.dll artifacts/m3c-baseline/bench/Styml.Benchmarks/bin/Release/net11.0/Styml.pdb "$w/base/"
run() { dotnet "$w/$1/Styml.Benchmarks.dll" gate3-row "$2" "$3" "$4" 2>&1 | tail -1; }
echo "# $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
for round in 1 2 3 4 5 6 7; do
  for row in "$@"; do
    c=$(echo "$row" | cut -d: -f1); m=$(echo "$row" | cut -d: -f2); d=$(echo "$row" | cut -d: -f3)
    echo "$round baseline $(run base "$c" "$m" "$d")"
    echo "$round head $(run head "$c" "$m" "$d")"
    echo "$round baseline2 $(run base "$c" "$m" "$d")"
  done
done
echo "# $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
