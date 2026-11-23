# Releasing Styml

## What CI runs

| Workflow | When | What |
| --- | --- | --- |
| `build.yml` | push and pull request on `main` (not for `docs/**` or `README.md` alone), or on demand | ubuntu and windows: restore, build every target, leg Y and leg YJ of the five test projects on net11.0, leg Y of `Styml.Yaml.Tests` on net481 (windows), pack, the consumer sample from the packed package (net11.0, net10.0), and a preview push to Feedz from `main` |
| `publish.yml` | a tag `v*.*.*` | `aot.yml`, then tag validation, build, leg Y, pack, provenance attestation, and the push to nuget.org |
| `aot.yml` | called by `publish.yml`, or on demand | `samples/AotSmoke` published trimmed and as Native AOT on linux-x64, warnings as errors, and run |

Leg Y is the writer's block default with upstream's filter (`category!=OuterLoop&category!=failing`) and
`category!=jsonoutput`; leg YJ is the JSON-compatible writer default (`STYML_TEST_WRITER=json`) with every test. Both
run through `eng/test/run-tests.sh`.

Left to the local gates before a release: the net10.0 legs, net481 for the other test projects, the Debug leg (the
product's `Debug.Assert`s), the identity outputs (`bench/run-identity.sh`), the public API check
(`eng/test/public-api`), and the benchmarks (`docs/benchmarks/`).

The three `Microsoft.DotNet.*` test packages are served only by dnceng's public `dotnet-eng` feed. The repository has
no `NuGet.config`: the workflows name nuget.org and that feed on the restore command line, and silence NuGet's
NU1507 there (two sources without package source mapping, which only a `NuGet.config` can declare). Locally, restore the same
way once (`dotnet restore Styml.slnx --source https://api.nuget.org/v3/index.json --source
https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json`).

## Versions

`src/Styml/Styml.csproj` sets `VersionPrefix` (0.1.0). CI builds on `main` add `VersionSuffix=preview.<run number>`;
a release tag sets `Version` from the tag, so `v0.1.0-preview.1` publishes `0.1.0-preview.1` and `v0.1.0` publishes
`0.1.0`. A tag must point to a commit on `main`.

## One-time setup (the repository owner)

- **Feedz:** create the `sebastienros/styml` repository on Feedz and add its API key as the `FEEDZ_IO_API_KEY`
  repository secret. Without it, `build.yml` skips the preview push.
- **nuget.org trusted publishing:** add a policy for the `sebastienros/styml` repository, workflow `publish.yml`,
  environment `release`, owned by the `sebastienros` account (the user `publish.yml` passes to `NuGet/login`).
- **The `release` environment:** create it in the repository settings, with the protection rules wanted for a release
  (required reviewers, tags only).
- **Branch protection on `main`:** require the `build` checks of `build.yml` (ubuntu and windows).

## Making a release

1. Run the local gates above on the commit to release.
2. Tag it: `git tag v0.1.0-preview.1 && git push origin v0.1.0-preview.1`.
3. Approve the `release` environment when `publish.yml` asks.
