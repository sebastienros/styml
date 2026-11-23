# public-api

Lists the public API of a `Styml.dll`, one line per public or protected type and member, in the format that
`test/Styml.Yaml.Tests/PublicApiTests.cs` compares against (M3's exit criterion 9). The listing code is `test/Styml.Yaml.Tests/PublicApiListing.cs`, shared
with the test.

The baselines in `test/Styml.Yaml.Tests/PublicApi/m2b.<tfm>.txt` were produced from the M2b commit `bf55f5a`, which
lives in the maintainers' history repository (`STYML_HISTORY_REPO`), not in this one:

```bash
mkdir -p /tmp/m2b && git -C "$STYML_HISTORY_REPO" archive bf55f5a | tar -x -C /tmp/m2b
dotnet build /tmp/m2b/src/Styml/Styml.csproj -c Release
dotnet build eng/test/public-api -c Release
for f in net11.0 net10.0 netstandard2.0; do
  eng/test/public-api/bin/Release/net11.0/list-public-api /tmp/m2b/src/Styml/bin/Release/$f/Styml.dll \
    test/Styml.Yaml.Tests/PublicApi/m2b.$f.txt
done
```

The header line of each file was then edited to name the commit. The test reads `m2b.<tfm>.txt` for the target of
the `Styml.dll` it runs against (the net481 leg uses `netstandard2.0`) and `m3a-additions.txt` for every target.
Dependencies of the netstandard2.0 build that are not in the shared framework are loaded from the NuGet cache.
