#!/usr/bin/env bash
# Runs the net481 leg of test projects on a Windows machine reachable over ssh.
# The test outputs are built here (the reference-assemblies package makes net481 build on any OS), copied over,
# run with that machine's 'dotnet test', and the .trx/.log files are copied back to artifacts/testresults.
# Usage: eng/test/run-net481-remote.sh <Project> [<Project> ...]     e.g. Styml.Tests Styml.Yaml.Tests
# Environment: STYML_REMOTE_HOST (required: the ssh host alias of the Windows machine), STYML_REMOTE_DIR (default styml-net481, relative to the
# remote home), STYML_TEST_FILTER (default upstream's "category!=OuterLoop&category!=failing", plus
# "&category!=jsonoutput" unless STYML_TEST_WRITER=json), STYML_TEST_WRITER (block|json, passed to the remote test
# process: json is leg YJ, with results in artifacts/testresults-yj), STYML_REMOTE_PROBE_SECONDS (default 60, the hard limit on the
# first connection).
# A dead tunnel fails in about a minute: every ssh and scp call has BatchMode, ConnectTimeout and keep-alives, and the
# first probe ('whoami') runs under a hard time limit, since a ProxyCommand (a tunnel, for instance) can hang past ConnectTimeout.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"
host="${STYML_REMOTE_HOST:?set STYML_REMOTE_HOST to the ssh host alias of a Windows machine}"
rdir="${STYML_REMOTE_DIR:-styml-net481}"
writer="${STYML_TEST_WRITER:-block}"
default_filter="category!=OuterLoop&category!=failing"
results=artifacts/testresults
if [ "$writer" = "json" ]; then
  results=artifacts/testresults-yj
else
  default_filter="$default_filter&category!=jsonoutput"
fi
filter="${STYML_TEST_FILTER:-$default_filter}"
ssh_opts=(-o BatchMode=yes -o ConnectTimeout=30 -o ServerAliveInterval=15 -o ServerAliveCountMax=4)
# perl's alarm is the time limit (macOS has no timeout(1)); it ends the ssh process. Against a stale tunnel client
# the probe has failed on ConnectTimeout, after 30 seconds, before the alarm.
probe_seconds="${STYML_REMOTE_PROBE_SECONDS:-60}"
if ! who="$(perl -e 'alarm shift; exec @ARGV' "$probe_seconds" ssh "${ssh_opts[@]}" "$host" 'whoami' 2>&1)"; then
  echo "probe failed: ssh $host 'whoami' (time limit ${probe_seconds}s): $who" >&2
  exit 2
fi
echo "probe: $host answers as $(echo "$who" | tr -d '\r' | tail -1)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$results"
ssh "${ssh_opts[@]}" "$host" "New-Item -ItemType Directory -Force $rdir\\results | Out-Null"
for p in "$@"; do
  if ! dotnet build "test/$p/$p.csproj" -c Release -f net481 > "$work/$p.build.log" 2>&1; then
    grep -E ' error ' "$work/$p.build.log" | sort -u | head -20
    echo "build failed: $p" >&2
    exit 1
  fi
  echo "Build succeeded: $p"
  # COPYFILE_DISABLE stops macOS tar from adding AppleDouble ._* files.
  COPYFILE_DISABLE=1 tar -czf "$work/$p.tgz" "test/$p/bin/Release/net481"
  ssh "${ssh_opts[@]}" "$host" "cd $rdir; if (Test-Path test\\$p) { Remove-Item -Recurse -Force test\\$p }; Remove-Item -Force results\\$p.* -ErrorAction SilentlyContinue"
  scp "${ssh_opts[@]}" -q "$work/$p.tgz" "$host:$rdir/$p.tgz"
  # NO_COLOR keeps the console log readable; the full log is copied back next to the trx.
  ssh "${ssh_opts[@]}" "$host" "\$env:NO_COLOR='1'; \$env:STYML_TEST_WRITER='$writer'; cd $rdir; tar -xzf $p.tgz; dotnet test test\\$p\\bin\\Release\\net481\\$p.dll --filter '$filter' --logger 'trx;LogFileName=$p.net481.trx' --results-directory results --blame-hang-timeout ${STYML_TEST_HANG_TIMEOUT:-15m} --blame-hang-dump-type none *> results\\$p.log; Get-Content results\\$p.log | Select-String -Pattern '^(Passed|Failed)!|Test Run Aborted' | Select-Object -Last 3" \
    | sed 's/\x1b\[[0-9;]*m//g'
  scp "${ssh_opts[@]}" -q "$host:$rdir/results/$p.net481.trx" "$results/" 2>/dev/null || echo "no trx for $p"
  scp "${ssh_opts[@]}" -q "$host:$rdir/results/$p.log" "$results/$p.net481.log" 2>/dev/null || true
done
