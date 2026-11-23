#!/bin/sh
# Runs the M6b benchmarks. Usage, from the repository root:
#   sh bench/run-m6b.sh <tfm> new     M6b's classes: the twelve documents on net11.0; on net10.0 the medium documents
#                                     and anchors-m, merge-m (STYML_BENCH_DOCS)
#   sh bench/run-m6b.sh <tfm> reads   M6a's read classes on the medium documents
#   sh bench/run-m6b.sh <tfm> dry     every class, once (BenchmarkDotNet's Dry job), into bench/results/M6b-dry
# Timed runs: one BenchmarkDotNet invocation per class. Load is a precondition (review B): before each class, while the
# 1-minute load average is above 8 the runner waits, up to 10 minutes, then runs the class anyway and marks it loaded.
# A noisy class (loaded, or above 8 at its end) is repeated once into bench/results/M6b-repeat; the quieter run (the
# lower of the two runs' highest load) is kept in bench/results/M6b. Every class is logged in the set's load.log with
# its loads at start and end and the highest load sampled every 15 seconds during the run.
set -u
cd "$(dirname "$0")/.."
tfm=$1
what=$2
medium="workflow-m,compose-m,k8s-m,openapi-m,records-m"

load1() { sysctl -n vm.loadavg | awk '{print $2}'; }
above() { awk -v a="$1" -v b="$2" 'BEGIN { exit !(a > b) }'; }

if [ "$what" = dry ]; then
  mkdir -p bench/results/M6b-dry
  log=bench/results/M6b-dry/load.log
  echo "$(date '+%F %T') $tfm dry start: $(load1)" >> "$log"
  STYML_BENCH_RESULTS=M6b-dry dotnet run -c Release -f "$tfm" --no-build --project bench/Styml.Benchmarks -- dry --filter 'Styml.Benchmarks.*'
  rc=$?
  echo "$(date '+%F %T') $tfm dry end ($rc): $(load1)" >> "$log"
  exit $rc
fi

case "$what" in
  new)
    classes="SerializeYamlReflection SerializeYamlSourceGen SerializeToString DomParse AnchorsTokenize AnchorsDomParse AnchorsDeserializeReflection AnchorsDeserializeSourceGen AnchorsSerialize"
    if [ "$tfm" = net10.0 ]; then export STYML_BENCH_DOCS="$medium,anchors-m,merge-m"; fi ;;
  reads)
    classes="Tokenize TokenizeText DeserializeReflection DeserializeSourceGen"
    export STYML_BENCH_DOCS="$medium" ;;
  *) echo "usage: $0 <tfm> new|reads|dry" >&2; exit 2 ;;
esac

mkdir -p bench/results/M6b artifacts/m6b-console
log=bench/results/M6b/load.log

# One timed run of a class into results set $2; prints "<start> <end> <highest>" and returns BenchmarkDotNet's code.
run_class() {
  class=$1; set=$2
  samples=$(mktemp)
  ( while :; do load1 >> "$samples"; sleep 15; done ) > /dev/null 2>&1 &
  sampler=$!
  start=$(load1)
  STYML_BENCH_RESULTS=$set dotnet run -c Release -f "$tfm" --no-build --project bench/Styml.Benchmarks -- --filter "Styml.Benchmarks.$class.*" > "artifacts/m6b-console/$set.$tfm.$class.txt" 2>&1
  rc=$?
  end=$(load1)
  kill "$sampler" 2> /dev/null
  high=$(sort -n "$samples" | tail -1)
  rm -f "$samples"
  echo "$start $end ${high:-$end}"
  return $rc
}

for class in $classes; do
  waited=0
  while above "$(load1)" 8 && [ $waited -lt 600 ]; do sleep 30; waited=$((waited + 30)); done
  loaded=""
  if above "$(load1)" 8; then loaded=" loaded"; fi
  echo "$(date '+%F %T') $tfm $class start (waited ${waited}s)$loaded, docs ${STYML_BENCH_DOCS:-all}" >> "$log"
  loads=$(run_class "$class" M6b); rc=$?
  set -- $loads
  echo "$(date '+%F %T') $tfm $class end ($rc): load start $1, end $2, highest $3" >> "$log"
  if [ -n "$loaded" ] || above "$2" 8; then
    echo "$(date '+%F %T') $tfm $class noisy: repeated once" >> "$log"
    first_high=$3
    loads=$(run_class "$class" M6b-repeat); rc2=$?
    set -- $loads
    echo "$(date '+%F %T') $tfm $class repeat end ($rc2): load start $1, end $2, highest $3" >> "$log"
    if [ $rc2 -eq 0 ] && above "$first_high" "$3"; then
      cp bench/results/M6b-repeat/"$tfm"/results/Styml.Benchmarks."$class"-report* bench/results/M6b/"$tfm"/results/
      echo "$(date '+%F %T') $tfm $class kept the repeat (highest $3 against $first_high)" >> "$log"
    else
      echo "$(date '+%F %T') $tfm $class kept the first run (highest $first_high against $3)" >> "$log"
    fi
  fi
done
