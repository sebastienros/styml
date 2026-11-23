#!/usr/bin/env bash
# Verifies that the imported tree is byte-identical to the upstream ref (blob SHA comparison).
# Run on the import commit (before the rename), e.g.: git stash/checkout <import commit> && eng/import/verify.sh
# Usage: eng/import/verify.sh <runtime-repo> [ref]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
runtime="${1:?usage: eng/import/verify.sh <runtime-repo> [ref]}"
ref="${2:-deae2e32bf0e018e09d64597b7c59b62b73bb3ce}"

excluded() {
  local p="$1"
  while read -r e; do
    [[ -z "${e:-}" || "$e" == \#* ]] && continue
    [[ "$p" == "$e" || "$p" == "$e"/* ]] && return 0
  done < "$here/exclude.txt"
  return 1
}

checked=0; bad=0
while read -r kind src dst; do
  [[ -z "${kind:-}" || "$kind" == \#* ]] && continue
  while read -r mode type sha path; do
    [[ "$type" == blob ]] || continue
    excluded "$path" && continue
    rel="${path#"$src"}"
    target="$repo/$dst$rel"
    if [[ ! -f "$target" ]]; then
      echo "MISSING $target"; bad=$((bad+1)); continue
    fi
    actual="$(git hash-object "$target")"
    if [[ "$actual" != "$sha" ]]; then
      echo "DIFFERS $target"; bad=$((bad+1))
    fi
    checked=$((checked+1))
  done < <(git -C "$runtime" ls-tree -r "$ref" -- "$src" | tr '\t' ' ')
done < "$here/manifest.txt"

echo "verified $checked files, $bad problems"
[[ $bad -eq 0 ]]
