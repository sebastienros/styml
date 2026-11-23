#!/bin/sh
# The README's benchmark panel (docs/benchmarks): Styml against YamlDotNet, SharpYaml and VYaml, with System.Text.Json on
# the equivalent JSON as the ceiling, on the five medium documents. BenchmarkDotNet's ShortRun job with three launches
# (STYML_BENCH_LAUNCHES), the memory diagnoser on, one invocation per class, only the rows the tables show. Before each
# class the runner waits until the 1-minute load average is below 6 and the 5-minute below 8, and logs the loads.
# Results: bench/results/<set>/<tfm> (set: STYML_BENCH_RESULTS, default Panel), and <set>/load.log.
# Usage, from the repository root, after building bench/Styml.Benchmarks (Release):  sh bench/run-panel.sh [tfm]
set -u
cd "$(dirname "$0")/.."
tfm=${1:-net11.0}
set=${STYML_BENCH_RESULTS:-Panel}
export STYML_BENCH_RESULTS=$set STYML_BENCH_LAUNCHES=${STYML_BENCH_LAUNCHES:-3}
export STYML_BENCH_DOCS=${STYML_BENCH_DOCS:-workflow-m,compose-m,k8s-m,openapi-m,records-m}
mkdir -p "bench/results/$set" artifacts/panel-console
log="bench/results/$set/load.log"
loads() { sysctl -n vm.loadavg 2>/dev/null | awk '{print $2, $3}' || cut -d' ' -f1,2 /proc/loadavg; }
quiet() { loads | awk '{exit !($1 < 6 && $2 < 8)}'; }
# class: the rows it runs.
rows() {
  case $1 in
    Tokenize|DeserializeSourceGen) echo "Styml_Yaml YamlDotNet SharpYaml VYaml Stj" ;;
    DeserializeReflection) echo "Styml_Yaml YamlDotNet SharpYaml Stj" ;;
    SerializeYamlSourceGen) echo "Styml_Block Styml_Block_Writer YamlDotNet SharpYaml VYaml Stj" ;;
    DomParse) echo "Styml_Yaml Styml_Node YamlDotNet SharpYaml Stj Stj_Node" ;;
  esac
}
status=0
for class in DeserializeSourceGen DeserializeReflection SerializeYamlSourceGen DomParse Tokenize; do
  until quiet; do sleep 20; done
  filters=""
  for r in $(rows "$class"); do filters="$filters Styml.Benchmarks.$class.$r"; done
  echo "$(date '+%F %T') $tfm $class start, load $(loads), docs $STYML_BENCH_DOCS, launches $STYML_BENCH_LAUNCHES" >> "$log"
  # shellcheck disable=SC2086
  dotnet run -c Release -f "$tfm" --no-build --project bench/Styml.Benchmarks -- --filter $filters > "artifacts/panel-console/$tfm.$class.txt" 2>&1
  rc=$?
  [ $rc -eq 0 ] || status=1
  echo "$(date '+%F %T') $tfm $class end ($rc), load $(loads)" >> "$log"
done
exit $status
