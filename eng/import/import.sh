#!/usr/bin/env bash
# Verbatim import of System.Text.Json (and the files its build links in) from a dotnet/runtime git ref.
#
# Usage: eng/import/import.sh <runtime-repo> [ref] [--allow-missing]
#   runtime-repo     path to a dotnet/runtime clone
#   ref              commit/tag to import (default: the commit recorded in eng/upstream/baseline.json)
#   --allow-missing  skip manifest.txt and exclude.txt entries that do not exist at ref, listing them, instead of
#                    failing (eng/upstream/sync uses it for a newer upstream commit, where such entries are deletions)
#
# Reads only the git object database of the runtime repo (git archive); never its working tree,
# and never writes to it. Copies into this repo according to manifest.txt minus exclude.txt.
# The result is a pure, unedited copy; the rename (eng/rename) runs as a separate step.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
allow_missing=0
args=()
for a in "$@"; do
  if [[ "$a" == --allow-missing ]]; then allow_missing=1; else args+=("$a"); fi
done
runtime="${args[0]:?usage: eng/import/import.sh <runtime-repo> [ref] [--allow-missing]}"
ref="${args[1]:-$(sed -n 's/^ *"commit": *"\([0-9a-f]\{40\}\)".*/\1/p' "$repo/eng/upstream/baseline.json" 2>/dev/null)}"
[[ -n "$ref" ]] || { echo "no ref given and none recorded in eng/upstream/baseline.json" >&2; exit 1; }

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

manifest="$here/manifest.txt"
exclude="$here/exclude.txt"

# Collect upstream paths and extract them all with one git archive call.
paths=()
missing=()
while read -r kind src dst; do
  [[ -z "${kind:-}" || "$kind" == \#* ]] && continue
  if [[ $allow_missing -eq 1 ]] && ! git -C "$runtime" cat-file -e "$ref:$src" 2>/dev/null; then
    missing+=("$src"); continue
  fi
  paths+=("$src")
done < "$manifest"

git -C "$runtime" archive --format=tar "$ref" -- "${paths[@]}" | tar -x -C "$stage"

# Drop excluded paths.
while read -r p; do
  [[ -z "${p:-}" || "$p" == \#* ]] && continue
  if [[ ! -e "$stage/$p" ]]; then
    if [[ $allow_missing -eq 1 ]]; then missing+=("exclude.txt: $p"); continue; fi
    echo "exclude.txt entry not found at $ref: $p" >&2
    exit 1
  fi
  rm -rf "${stage:?}/$p"
done < "$exclude"

# Place everything.
while read -r kind src dst; do
  [[ -z "${kind:-}" || "$kind" == \#* ]] && continue
  [[ -e "$stage/$src" ]] || continue   # only when --allow-missing skipped it
  case "$kind" in
    dir)
      mkdir -p "$repo/$dst"
      cp -R "$stage/$src/." "$repo/$dst/"
      ;;
    file)
      mkdir -p "$(dirname "$repo/$dst")"
      cp "$stage/$src" "$repo/$dst"
      ;;
    *)
      echo "unknown manifest kind '$kind'" >&2
      exit 1
      ;;
  esac
done < "$manifest"

for m in ${missing[@]+"${missing[@]}"}; do echo "not at $ref, skipped: $m"; done
echo "Imported $(git -C "$runtime" rev-parse "$ref") into $repo"
