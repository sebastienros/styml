// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:package Microsoft.CodeAnalysis.CSharp@4.8.0
// Extracts the JSON-equivalence corpus (exit criterion 2) from the imported test
// data: every C# string literal in the imported test projects, and every value of the main test project's
// Strings.resx, that System.Text.Json (default options) accepts as a JSON object or array. Duplicates are removed.
// Output: test/Styml.Yaml.Tests/JsonCorpus/corpus.json, a JSON array of { "source", "json" } records.
//
// Usage (from the repo root):  dotnet run eng/test/json-corpus/extract-json-corpus.cs
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string repo = Directory.GetCurrentDirectory();
string[] roots = ["test/Common", "test/Styml.Tests", "test/Styml.SourceGeneration.Tests", "test/Styml.SourceGeneration.Unit.Tests"];
var seen = new HashSet<string>(StringComparer.Ordinal);
var records = new List<(string Source, string Json)>();

void Consider(string text, string source)
{
    string trimmed = text.Trim();
    if (trimmed.Length < 2 || (trimmed[0] != '{' && trimmed[0] != '['))
        return;
    try
    {
        using var doc = JsonDocument.Parse(text);
    }
    catch (Exception e) when (e is JsonException or ArgumentException)
    {
        // Not JSON, or not valid UTF-16 (some tests use lone surrogates on purpose).
        return;
    }
    if (seen.Add(text))
        records.Add((source, text));
}

foreach (string root in roots)
{
    foreach (string path in Directory.EnumerateFiles(Path.Combine(repo, root), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        if (path.Contains("/obj/") || path.Contains("/bin/"))
            continue;
        string rel = Path.GetRelativePath(repo, path).Replace('\\', '/');
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview));
        foreach (SyntaxToken token in tree.GetRoot().DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken))
            {
                Consider(token.ValueText, $"{rel}:{token.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
            }
        }
    }
}

string resx = Path.Combine(repo, "test/Styml.Tests/Resources/Strings.resx");
foreach (XElement data in XDocument.Load(resx).Root!.Elements("data"))
{
    Consider((string?)data.Element("value") ?? "", $"test/Styml.Tests/Resources/Strings.resx:{(string?)data.Attribute("name")}");
}

string outDir = Path.Combine(repo, "test/Styml.Yaml.Tests/JsonCorpus");
Directory.CreateDirectory(outDir);
using (var stream = File.Create(Path.Combine(outDir, "corpus.json")))
using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartArray();
    foreach (var (source, json) in records)
    {
        writer.WriteStartObject();
        writer.WriteString("source", source);
        writer.WriteString("json", json);
        writer.WriteEndObject();
    }
    writer.WriteEndArray();
}

var sizes = records.Select(r => System.Text.Encoding.UTF8.GetByteCount(r.Json)).OrderBy(x => x).ToList();
Console.WriteLine($"{records.Count} JSON documents, {sizes.Sum()} bytes; median {sizes[sizes.Count / 2]}, max {sizes[^1]}, > 4096: {sizes.Count(s => s > 4096)}");
