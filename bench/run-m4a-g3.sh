#!/bin/sh
# M4 gate G3, from M4a: block style against System.Text.Json and the
# YAML libraries, by
# interleaved in-process runs on one build (the 'gate3-row' mode: one second of warm-up, best of 40 batches of at least
# 2 ms, microseconds per call; one process per run). Per round, every document's rows run back to back, so that each
# ratio compares rows measured seconds apart. ROUNDS defaults to 5; DIR is the build folder (default: the benchmark's
# Release net11.0 output). Summarise with:
#   python3 bench/report-m4a-g3.py <output>
# From M4b, the non-ASCII rows (WriteBlockNonAscii on the ascii, latin and cjk string sets) run once per round as well.
# ROWS and NONASCII_ROWS override the two row lists (M4b's fix step: the _Lenient rows, QuoteForLenientReaders on).
# Usage, from the repository root: sh bench/run-m4a-g3.sh > out.txt
set -u
cd "$(dirname "$0")/.."
dir=${DIR:-bench/Styml.Benchmarks/bin/Release/net11.0}
rounds=${ROUNDS:-5}
docs=${DOCS:-"workflow-s compose-s k8s-s records-s workflow-m compose-m k8s-m openapi-m records-m openapi-l k8s-l records-l"}
rows=${ROWS:-"WriteBlock:Styml_Block WriteBlock:Stj_Indented WriteBlock:Styml_Block_ConstKeys WriteBlock:Stj_Indented_ConstKeys WriteBlock:Styml_Block_ConstValues WriteBlock:Stj_Indented_ConstValues SerializeReflectionBlock:Styml_Block SerializeReflectionBlock:Stj SerializeSourceGenBlock:Styml_Block SerializeSourceGenBlock:Stj SerializeReflection:YamlDotNet SerializeReflection:SharpYaml SerializeSourceGen:YamlDotNet SerializeSourceGen:SharpYaml SerializeSourceGen:VYaml"}
echo "# $(shasum "$dir/Styml.dll" | cut -c1-8) $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
r=1
while [ "$r" -le "$rounds" ]; do
  for d in $docs; do
    for row in $rows; do
      c=$(echo "$row" | cut -d: -f1); m=$(echo "$row" | cut -d: -f2)
      echo "$r $(dotnet "$dir/Styml.Benchmarks.dll" gate3-row "$c" "$m" "$d" 2>&1 | tail -1)"
    done
  done
  for d in ${NONASCII:-ascii latin cjk}; do
    for m in ${NONASCII_ROWS:-Styml_Block Styml_Indented_Relaxed Stj_Indented_Relaxed Styml_Block_Utf8 Styml_Indented_Relaxed_Utf8 Stj_Indented_Relaxed_Utf8}; do
      echo "$r $(dotnet "$dir/Styml.Benchmarks.dll" gate3-row WriteBlockNonAscii "$m" "$d" 2>&1 | tail -1)"
    done
  done
  echo "# round $r done $(date '+%F %T') load $(uptime | sed 's/.*load averages*: //')"
  r=$((r + 1))
done
