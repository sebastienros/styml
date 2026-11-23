#!/usr/bin/env bash
# Opt-in cross-read of block style's quoting by Ruby's Psych and PyYAML (not a test dependency): writes every string up
# to MAX_LENGTH characters of the patterns' alphabet, the keywords in every case and generated timestamps, with
# QuoteForLenientReaders off and on, then reads them back with each reader and reports what does not come back as the
# same string. Exits non-zero if a reader misreads anything with the option on.
#
#   eng/crossread/run.sh [output directory] [max length]
#
# Needs ruby (Psych) and python3 with PyYAML (pip install pyyaml); RUBY and PYTHON override the interpreters.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/crossread}"
max="${2:-4}"
dotnet run --file "$root/eng/crossread/write-documents.cs" -c Release -- "$out" "$max"
status=0
"${RUBY:-ruby}" "$root/eng/crossread/read-psych.rb" "$out" || status=1
"${PYTHON:-python3}" -I "$root/eng/crossread/read-pyyaml.py" "$out" || status=1
exit $status
