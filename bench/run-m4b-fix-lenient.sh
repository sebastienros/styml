#!/bin/sh
# M4b's fix step: the cost of QuoteForLenientReaders's wider set. The block rows with the option off and on, run with
# two Styml.dll builds in one bench build (the one of ff2eeb4), interleaved per row: artifacts/m4-swap/lenient-m4b (c13df21's Styml.dll) and
# artifacts/m4-swap/lenient-fix (ff2eeb4's). ROUNDS defaults to 5. Each line: round, side, class, method, document, microseconds.
# Usage, from the repository root: sh bench/run-m4b-fix-lenient.sh > out.txt
set -u
cd "$(dirname "$0")/.."
w=artifacts/m4-swap
rounds=${ROUNDS:-5}
for s in lenient-m4b lenient-fix; do echo "# $s $(shasum "$w/$s/Styml.dll" | cut -c1-8)"; done
echo "# $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
r=1
while [ "$r" -le "$rounds" ]; do
  for row in WriteBlock:Styml_Block:compose-m WriteBlock:Styml_Block_Lenient:compose-m WriteBlock:Styml_Block:k8s-m WriteBlock:Styml_Block_Lenient:k8s-m \
             WriteBlock:Styml_Block:openapi-m WriteBlock:Styml_Block_Lenient:openapi-m WriteBlock:Styml_Block:records-m WriteBlock:Styml_Block_Lenient:records-m \
             WriteBlockNonAscii:Styml_Block:ascii WriteBlockNonAscii:Styml_Block_Lenient:ascii WriteBlockNonAscii:Styml_Block:latin WriteBlockNonAscii:Styml_Block_Lenient:latin \
             WriteBlockNonAscii:Styml_Block:cjk WriteBlockNonAscii:Styml_Block_Lenient:cjk; do
    c=$(echo "$row" | cut -d: -f1); m=$(echo "$row" | cut -d: -f2); d=$(echo "$row" | cut -d: -f3)
    for s in lenient-m4b lenient-fix; do
      echo "$r $s $(dotnet "$w/$s/Styml.Benchmarks.dll" gate3-row "$c" "$m" "$d" 2>&1 | tail -1)"
    done
  done
  echo "# round $r done $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
  r=$((r + 1))
done
