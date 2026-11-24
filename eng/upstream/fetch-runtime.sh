#!/usr/bin/env bash
# Copyright (c) Sébastien Ros. Licensed under the MIT license.
# A small, shallow, blob-less dotnet/runtime clone that is enough for eng/upstream/status: the recorded baseline
# commit, the target ref and the history between them (back to their merge base, for backport detection).
# Used by .github/workflows/upstream.yml. History is fetched since the record's sync date minus 30, 120, then 400 days,
# until the merge base of baseline and target is in the clone. Blobs are fetched on demand (partial clone).
# dest.git is deleted and recreated.
#
# Usage: eng/upstream/fetch-runtime.sh <dest.git> [target-ref]
#   dest.git    a new bare repository to create
#   target-ref  a tag or branch name (default: eng/upstream/target --ls-remote, the newest ref of the followed line)
# Environment: STYML_RUNTIME_URL (default https://github.com/dotnet/runtime.git); STYML_UPLOAD_PACK, an
#   --upload-pack command for testing against a local clone over file:// (it needs uploadpack.allowFilter and
#   uploadpack.allowAnySHA1InWant).
# Prints the target ref name, which exists in dest.git as refs/tags/<name> or refs/heads/<name>.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dest="${1:?usage: eng/upstream/fetch-runtime.sh <dest.git> [target-ref]}"
url="${STYML_RUNTIME_URL:-https://github.com/dotnet/runtime.git}"
upload=()
[[ -n "${STYML_UPLOAD_PACK:-}" ]] && upload=(--upload-pack="$STYML_UPLOAD_PACK")

field() { python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))[sys.argv[2]])' "$here/baseline.json" "$1"; }
base="$(field commit)"
synced="$(field synced)"

target="${2:-}"
if [[ -z "$target" ]]; then
  tags="$(mktemp)"
  git ls-remote ${upload[@]+"${upload[@]}"} --tags --refs "$url" > "$tags"
  target="$(python3 -I "$here/upstream.py" target --ls-remote "$tags")"
  rm -f "$tags"
fi
if git ls-remote ${upload[@]+"${upload[@]}"} --exit-code --tags "$url" "refs/tags/$target" > /dev/null; then
  refspec="+refs/tags/$target:refs/tags/$target"
else
  refspec="+refs/heads/$target:refs/heads/$target"
fi

since_date() {  # synced minus N days
  python3 -I -c 'import datetime,sys; print(datetime.date.fromisoformat(sys.argv[1]) - datetime.timedelta(days=int(sys.argv[2])))' "$synced" "$1"
}

# Each attempt starts from a new repository: a second --shallow-since fetch into a shallow partial clone does not
# reliably deepen refs it already has.
for days in 30 120 400; do
  since="$(since_date "$days")"
  rm -rf "$dest"
  git init -q --bare "$dest"
  git -C "$dest" remote add origin "$url"
  git -C "$dest" config remote.origin.promisor true
  git -C "$dest" config remote.origin.partialclonefilter blob:none
  [[ -n "${STYML_UPLOAD_PACK:-}" ]] && git -C "$dest" config remote.origin.uploadpack "$STYML_UPLOAD_PACK"
  err="$(git -C "$dest" fetch -q --no-tags --filter=blob:none --shallow-since="$since" origin "$refspec" "$base" 2>&1)" || true
  if git -C "$dest" cat-file -e "$base^{commit}" 2> /dev/null &&
     git -C "$dest" rev-parse -q --verify "${refspec##*:}" > /dev/null &&
     git -C "$dest" merge-base "$base" "${refspec##*:}" > /dev/null 2>&1; then
    echo "fetched $url $target and $base, history since $since" >&2
    echo "$target"
    exit 0
  fi
done
echo "could not fetch $base and $target with their merge base (history since $since)${err:+: $err}" >&2
exit 1
