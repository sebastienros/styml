# yaml-test-suite (vendored)

Data release `data-2022-01-17` (commit `6e6c296ae9c9d2d5c4134b4b64d01b29ac19ff6f`) of
https://github.com/yaml/yaml-test-suite, MIT licensed (see `License` and the repository's `THIRD-PARTY-NOTICES.txt`).
Refresh with `eng/test/vendor-yaml-test-suite.sh <data-tag> <source-tag>`.

Each directory under `data/` is one case (cases with several variants have numbered sub-directories):
`in.yaml` input, `test.event` expected event trace, `error` present when the input must be rejected,
`in.json` the equivalent JSON when there is one, `out.yaml` / `emit.yaml` canonical emitter output, `===` title.
