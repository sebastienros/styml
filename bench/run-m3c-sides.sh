#!/bin/sh
# M3c gate 3: the dll swap over
# folders that already exist under artifacts/m3c-swap (one benchmark build, a different Styml.dll in each). Unlike
# run-m3c-dllswap.sh it never rebuilds or recopies a folder. SIDES names the folders run in each round, in order; the
# first and last must be 'base', which are printed as 'baseline' and 'baseline2' (the A/A pair); the others are printed
# under their folder names. ROUNDS defaults to 7. The Styml.dll hash of each folder is printed first. Summarise with:
#   python3 bench/report-m3c-rows.py <output> --time
# Usage, from the repository root:
#   SIDES="base head varA base" sh bench/run-m3c-sides.sh Class:Method:Document ... > output.txt
# SWAP names another folder of sides (default artifacts/m3c-swap), e.g. artifacts/m4p-swap (M4p).
set -u
cd "$(dirname "$0")/.."
w=${SWAP:-artifacts/m3c-swap}
sides=${SIDES:-"base head base"}
rounds=${ROUNDS:-7}
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
  r=$((r + 1))
done
echo "# $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
