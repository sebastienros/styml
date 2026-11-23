#!/usr/bin/env bash
# Vendors the yaml-test-suite data release into test/yaml-test-suite (see THIRD-PARTY-NOTICES.txt).
# Usage: eng/test/vendor-yaml-test-suite.sh [data-tag] [source-tag]
#   data-tag    release of the generated 'data' branch (default data-2022-01-17)
#   source-tag  matching source release, used only for the License file (default v2022-01-17)
# Copies every test-case directory byte for byte. The 'name/' and 'tags/' index directories (symlinks into the
# case directories) are not vendored.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
data_tag="${1:-data-2022-01-17}"
source_tag="${2:-v2022-01-17}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

git clone -q --depth 1 --branch "$data_tag" https://github.com/yaml/yaml-test-suite "$work/yts"
git -C "$work/yts" fetch -q --depth 1 origin tag "$source_tag"

dest="$repo/test/yaml-test-suite"
rm -rf "$dest/data"
mkdir -p "$dest/data"
for d in "$work/yts"/*/; do
  name="$(basename "$d")"
  [[ "$name" == name || "$name" == tags ]] && continue
  cp -R "$d" "$dest/data/$name"
done
git -C "$work/yts" show "$source_tag:License" > "$dest/License"
echo "data commit:   $(git -C "$work/yts" rev-parse HEAD)"
echo "source commit: $(git -C "$work/yts" rev-parse "$source_tag^{commit}")"
echo "cases: $(find "$dest/data" -name in.yaml | wc -l)"
