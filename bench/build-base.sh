#!/usr/bin/env bash
# Builds the benchmark harness and Styml.dll of an old commit, for the DLL swap of bench/run-m4-sides.sh: a side folder
# is a copy of one bench/Styml.Benchmarks build (Release, net11.0) with a different Styml.dll.
#   STYML_HISTORY_REPO=<repository> bench/build-base.sh <commit> <side folder>
# Old commits live in the maintainers' history repository, not in this one: STYML_HISTORY_REPO names it (required,
# no default). The commit is checked out there as a detached worktree outside this repository (STYML_BASE_WORKTREES,
# default $TMPDIR/styml-base-worktrees: BenchmarkDotNet refuses to build when a second Styml.Benchmarks.csproj lies
# under the repository root), built, and the side folder gets this tree's benchmark build with that commit's
# Styml.dll, so both sides run the same harness.
set -euo pipefail
cd "$(dirname "$0")/.."
history="${STYML_HISTORY_REPO:?set STYML_HISTORY_REPO to the repository that holds the commit}"
commit=${1:?usage: bench/build-base.sh <commit> <side folder>}; side=${2:?usage: bench/build-base.sh <commit> <side folder>}
id="$(git -C "$history" rev-parse --verify "$commit^{commit}")"
root="${STYML_BASE_WORKTREES:-${TMPDIR:-/tmp}/styml-base-worktrees}"
mkdir -p "$root"
wt="$root/${id:0:12}"
if [ ! -d "$wt" ]; then
  git -C "$history" worktree add --detach "$wt" "$id" > /dev/null
fi
dotnet build "$wt/src/Styml/Styml.csproj" -c Release -f net11.0 > "$wt.build.log" 2>&1 || { grep -E ' error ' "$wt.build.log" | sort -u | head; exit 1; }
dotnet build bench/Styml.Benchmarks -c Release -f net11.0 > /dev/null
mkdir -p "$side"
cp -R bench/Styml.Benchmarks/bin/Release/net11.0/. "$side/"
cp "$wt/src/Styml/bin/Release/net11.0/Styml.dll" "$side/Styml.dll"
echo "$side: Styml.dll of ${id:0:12} ($(shasum "$side/Styml.dll" | cut -c1-9))"
