// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:project ../../../src/Styml/Styml.csproj
#:property PublishAot=false
#:property TieredPGO=true
// Rough reader throughput (M3d exit gate 5): Utf8YamlReader over the JSON corpus (test/Styml.Yaml.Tests/JsonCorpus,
// one reader per document), over a generated block-YAML sample, and over the one-line shapes of the M3d review. Prints MB/s, the best of 7 rounds of about
// 1 s each, after a warm-up. Run from the repo root:
//   dotnet run -c Release eng/test/throughput/throughput.cs [nokeys]
// (Its 'json' mode ran the JSON grammar, deleted in M7a, decision D8.)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Yaml;

// 'nokeys': every reader runs with YamlReaderOptions.DisallowImplicitCollectionKeys (M3d review round 2). Set through
// reflection so the same script also runs against commits that predate the option.
YamlReaderOptions readerOptions = default;
if (args.Length > 0 && args[0] == "nokeys")
{
    object boxed = readerOptions;
    typeof(YamlReaderOptions).GetProperty("DisallowImplicitCollectionKeys")!.SetValue(boxed, true);
    readerOptions = (YamlReaderOptions)boxed;
}
var readerState = new YamlReaderState(readerOptions);

string root = Directory.GetCurrentDirectory();
using JsonDocument corpus = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "test/Styml.Yaml.Tests/JsonCorpus/corpus.json")));
byte[][] jsonDocs = corpus.RootElement.EnumerateArray().Select(e => Encoding.UTF8.GetBytes(e.GetProperty("json").GetString()!)).ToArray();
byte[] block = Encoding.UTF8.GetBytes(BlockSample(4000));

Console.WriteLine($"grammar: YAML{(args.Length > 0 && args[0] == "nokeys" ? ", DisallowImplicitCollectionKeys" : "")}");
Report("JSON corpus", jsonDocs.Sum(d => (long)d.Length), () =>
{
    long tokens = 0;
    foreach (byte[] d in jsonDocs)
    {
        var reader = new Utf8YamlReader(d, readerOptions);
        while (reader.Read()) tokens++;
    }
    return tokens;
});
if (args.Length == 0 || args[0] != "json")
{
    Report("block YAML", block.Length, () =>
    {
        long tokens = 0;
        var reader = new Utf8YamlReader(block, readerOptions);
        while (reader.Read()) tokens++;
        return tokens;
    });
}

// One-line shapes of the M3d review (each one document, read with a single loop). Shape a needs collection keys, which
// M3b rejects; the harness reports n/a for a document the reader rejects.
string Obj2K(int i, string tail) => "{\"id\": " + i + ", \"name\": \"item " + i + "\", \"text\": \"" + new string('x', 1900) + tail + "\", \"tags\": [\"a\", \"b\"]}";
string Array2K(int bytes, string tail, string last = "", string separator = ", ") =>
    "[" + string.Join(separator, Enumerable.Range(0, bytes / 2100).Select(i => Obj2K(i, tail))) + last + "]";
string KeysLine(int bytes) => "[" + string.Join(", ", Enumerable.Repeat("[a]: b", bytes / 8)) + "]";
string SmallObjects(int bytes) => "[" + string.Join(",", Enumerable.Range(0, bytes / 30).Select(i => "{\"a\":" + i + ",\"b\":\"two\",\"c\":[3,4]}")) + "]";
string CandidateAhead(int bytes, string trigger)
{
    // A one-line root object of "k":[[1]] members with a trigger member every ~3,900 bytes (review round 3).
    var sb = new StringBuilder("{");
    int since = 0;
    while (sb.Length < bytes)
    {
        if (since >= 3900) { sb.Append(trigger).Append(','); since = 0; }
        sb.Append("\"k\":[[1]],");
        since += 11;
    }
    return sb.Append("\"k\":[[1]]}").ToString();
}
string QuotedPlain(int bytes, string entry, string trigger)
{
    // A one-line flow sequence of entries with a quote inside a plain scalar, a trigger every ~3,900 bytes (round 4).
    var sb = new StringBuilder("[");
    int since = 0;
    while (sb.Length < bytes)
    {
        if (since >= 3900) { sb.Append(trigger).Append(", "); since = 0; }
        sb.Append(entry).Append(", ");
        since += entry.Length + 2;
    }
    return sb.Append("z]").ToString();
}
var shapes = new (string Name, byte[] Utf8, bool Final)[]
{
    ("h. {\"k\":[[1]],...\"s\":\"]:\"} 4 MB", Encoding.UTF8.GetBytes(CandidateAhead(4 << 20, "\"s\":\"]:\"")), true),
    ("h. same, non-final", Encoding.UTF8.GetBytes(CandidateAhead(4 << 20, "\"s\":\"]:\"")), false),
    ("h. control (\"xx\") 4 MB", Encoding.UTF8.GetBytes(CandidateAhead(4 << 20, "\"s\":\"xx\"")), true),
    ("l. [x \"y, [k], ...\"]:\"] 4 MB", Encoding.UTF8.GetBytes(QuotedPlain(4 << 20, "x \"y, [k]", "\"]:\"")), true),
    ("l. control (no \"]:\") 4 MB", Encoding.UTF8.GetBytes(QuotedPlain(4 << 20, "x \"y, [k]", "\"zz\"")), true),
    ("a. [[a]: b, ...] 4 MB", Encoding.UTF8.GetBytes(KeysLine(4 << 20)), true),
    ("b. 2 KB objects + \"]:\" 4 MB", Encoding.UTF8.GetBytes(Array2K(4 << 20, "", ", \"]:\"")), true),
    ("c. 2 KB objects, [1]: in text 4 MB", Encoding.UTF8.GetBytes(Array2K(4 << 20, " [1]: https://example.com/x")), true),
    ("d. 2 KB objects 4 MB", Encoding.UTF8.GetBytes(Array2K(4 << 20, "")), true),
    ("d. 2 KB objects 4 MB, non-final", Encoding.UTF8.GetBytes(Array2K(4 << 20, "")), false),
    ("e. small objects 3.2 MB", Encoding.UTF8.GetBytes(SmallObjects(3200 * 1024)), true),
    ("d'. 2 KB objects, one per line", Encoding.UTF8.GetBytes(Array2K(4 << 20, "", separator: ",\n")), true),
};
foreach (var (name, utf8, final) in shapes)
{
    bool ok;
    try
    {
        var probe = new Utf8YamlReader(utf8, final, readerState);
        while (probe.Read()) { }
        ok = true;
    }
    catch (YamlException)
    {
        ok = false;
    }
    if (!ok)
    {
        Console.WriteLine($"{name,-36} {utf8.Length,10:N0} bytes/pass       n/a (rejected)");
        continue;
    }
    Report(name, utf8.Length, () =>
    {
        long tokens = 0;
        var reader = new Utf8YamlReader(utf8, final, readerState);
        while (reader.Read()) tokens++;
        return tokens;
    });
}

static void Report(string name, long bytesPerPass, Func<long> pass)
{
    for (int i = 0; i < 20; i++) pass();
    double best = 0;
    for (int round = 0; round < 7; round++)
    {
        var sw = Stopwatch.StartNew();
        long bytes = 0;
        while (sw.ElapsedMilliseconds < 1000)
        {
            pass();
            bytes += bytesPerPass;
        }
        best = Math.Max(best, bytes / sw.Elapsed.TotalSeconds / 1e6);
    }
    Console.WriteLine($"{name,-36} {bytesPerPass,10:N0} bytes/pass  {best,8:F0} MB/s");
}

static string BlockSample(int records)
{
    var sb = new StringBuilder();
    sb.Append("# generated block-YAML sample\nservices:\n");
    for (int i = 0; i < records; i++)
    {
        sb.Append($"  - name: service-{i}\n");
        sb.Append($"    image: \"registry.example.com/app:{i % 17}.{i % 5}\"\n");
        sb.Append($"    replicas: {i % 9}\n");
        sb.Append($"    enabled: {(i % 2 == 0 ? "true" : "false")}\n");
        sb.Append($"    ratio: {i % 100}.25\n");
        sb.Append("    ports: [80, 443]\n");
        sb.Append("    env:\n");
        sb.Append($"      LOG_LEVEL: 'info'   # level\n");
        sb.Append($"      PATH_PREFIX: /api/v{i % 3}\n");
        sb.Append("    tags:\n      - web\n      - tier-" + (i % 4) + "\n");
        sb.Append("    description: >\n      A folded description\n      over two lines.\n");
    }
    return sb.ToString();
}
