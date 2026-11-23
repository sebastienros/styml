// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:project ../src/Styml/Styml.csproj
#:property PublishAot=false
#:property TieredPGO=true
// M3c gate 3: the in-process measurement for a flagged single-segment
// row. Tokenizes one bench/corpus document with Utf8YamlReader over a span, after a timed warm-up, the best of 40
// timed batches, and prints MB/s. Run from the repository root (a copy in the baseline worktree runs against the baseline's src/):
//   dotnet run -c Release bench/gate3-row.cs <document.form> [nokeys]     e.g. records-l.yaml, compose-s.json
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Yaml;

string input = args[0];
byte[] data = File.ReadAllBytes(Path.Combine("bench", "corpus", input));
YamlReaderOptions options = default;
if (args.Length > 1 && args[1] == "nokeys")
{
    object boxed = options;
    typeof(YamlReaderOptions).GetProperty("DisallowImplicitCollectionKeys")!.SetValue(boxed, true);
    options = (YamlReaderOptions)boxed;
}

long Tokens()
{
    var reader = new Utf8YamlReader(data, options);
    long n = 0;
    while (reader.Read())
    {
        n++;
    }
    return n;
}

// Warm up for a second (tiered compilation promotes the reader only after call counts and a delay), then time
// batches of at least 2 ms; the best of 40 batches, per read.
var clock = Stopwatch.StartNew();
int batch = 1;
while (clock.Elapsed.TotalMilliseconds < 1000)
{
    Tokens();
}
while (true)
{
    clock.Restart();
    for (int i = 0; i < batch; i++)
    {
        Tokens();
    }
    if (clock.Elapsed.TotalMilliseconds >= 2)
    {
        break;
    }
    batch *= 2;
}
double best = double.MaxValue;
for (int round = 0; round < 40; round++)
{
    clock.Restart();
    for (int i = 0; i < batch; i++)
    {
        Tokens();
    }
    best = Math.Min(best, clock.Elapsed.TotalMilliseconds / batch);
}
Console.WriteLine(FormattableString.Invariant($"{input} {(args.Length > 1 ? args[1] : "default")} {data.Length / best / 1000:F1}"));
