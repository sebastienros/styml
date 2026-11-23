#!/usr/bin/env bash
# Verbatim import of System.Text.Json (and the files its build links in) from a dotnet/runtime git ref.
#
# Usage: eng/import/import.sh <runtime-repo> [ref]
#   runtime-repo  path to a dotnet/runtime clone
#   ref           commit/tag to import          (default: deae2e32bf0e018e09d64597b7c59b62b73bb3ce = v11.0.0)
#
# Reads only the git object database of the runtime repo (git archive); never its working tree,
# and never writes to it. Copies into this repo according to manifest.txt minus exclude.txt.
# The result is a pure, unedited copy; the rename (eng/rename) runs as a separate step.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
runtime="${1:?usage: eng/import/import.sh <runtime-repo> [ref]}"
ref="${2:-deae2e32bf0e018e09d64597b7c59b62b73bb3ce}"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

manifest="$here/manifest.txt"
exclude="$here/exclude.txt"

# Collect upstream paths and extract them all with one git archive call.
paths=()
while read -r kind src dst; do
  [[ -z "${kind:-}" || "$kind" == \#* ]] && continue
  paths+=("$src")
done < "$manifest"

git -C "$runtime" archive --format=tar "$ref" -- "${paths[@]}" | tar -x -C "$stage"

# Drop excluded paths.
while read -r p; do
  [[ -z "${p:-}" || "$p" == \#* ]] && continue
  if [[ ! -e "$stage/$p" ]]; then
    echo "exclude.txt entry not found at $ref: $p" >&2
    exit 1
  fi
  rm -rf "${stage:?}/$p"
done < "$exclude"

# Place everything.
while read -r kind src dst; do
  [[ -z "${kind:-}" || "$kind" == \#* ]] && continue
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

echo "Imported $(git -C "$runtime" rev-parse "$ref") into $repo"
