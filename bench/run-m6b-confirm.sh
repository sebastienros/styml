#!/bin/sh
# M6b's confirmation run: the rows whose spread or difference the report could not
# attribute, with 3 launches (STYML_BENCH_LAUNCHES), on net11.0 and net10.0, into bench/results/M6b-confirm. Each
# invocation is logged with its load at start and end in bench/results/M6b-confirm/load.log; one above 8 marks it loaded.
# Usage, from the repository root: sh bench/run-m6b-confirm.sh
set -u
cd "$(dirname "$0")/.."
log=bench/results/M6b-confirm/load.log
mkdir -p bench/results/M6b-confirm artifacts/m6b-console
load1() { sysctl -n vm.loadavg | awk '{print $2}'; }
for tfm in net11.0 net10.0; do
  for spec in "TokenizeText:compose-m:Styml_Yaml*" "DomParse:k8s-m,records-m:Styml_Json Styml.Benchmarks.DomParse.Styml_Yaml Styml.Benchmarks.DomParse.Stj" "SerializeYamlReflection:k8s-m:Styml_Block"; do
    class=${spec%%:*}; rest=${spec#*:}; docs=${rest%%:*}; methods=${rest#*:}
    filters=""
    for m in $methods; do
      case $m in Styml.Benchmarks.*) filters="$filters $m" ;; *) filters="$filters Styml.Benchmarks.$class.$m" ;; esac
    done
    start=$(load1)
    STYML_BENCH_DOCS=$docs STYML_BENCH_LAUNCHES=3 STYML_BENCH_RESULTS=M6b-confirm \
      dotnet run -c Release -f "$tfm" --no-build --project bench/Styml.Benchmarks -- --filter $filters > "artifacts/m6b-console/confirm.$tfm.$class.txt" 2>&1
    rc=$?
    end=$(load1)
    echo "$(date '+%F %T') $tfm $class ($docs) end ($rc): load start $start, end $end" >> "$log"
  done
done
