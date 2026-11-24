// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:package Microsoft.CodeAnalysis.CSharp@4.8.0
// Styml rename tool, Pass B (M2b): Roslyn token rewrite of lowercase-leading identifiers.
//
//   json -> yaml, jsonTypeInfo -> yamlTypeInfo, _jsonElement -> _yamlElement, s_jsonX -> s_yamlX, ...
//
// Rules
// 1. In every .cs file under src/ and test/ (except vendored files and test/TestShims), only IdentifierToken tokens
//    are rewritten: never string literals, interpolated-string text or comment text. Identifiers inside XML doc
//    comments (<param name="json"/>, <paramref name="json"/>, cref) are identifier tokens and are rewritten so the
//    docs keep matching the parameter names.
//    Each file is parsed under several preprocessor symbol sets (one per TFM / project flavour) and the edits are
//    unioned, so code in every #if branch is seen as real syntax. Code that is inactive under all of them is
//    re-lexed from its disabled-text trivia as a fallback (reported separately).
// 2. Generator-emitted code: the generator's emitter writes a local named 'jsonTypeInfo' inside string literals.
//    That name is rewritten in string-literal text of YamlSourceGenerator.Emitter.cs only, and the same identifier
//    rule (1) is applied to the generator baselines (test/Styml.SourceGeneration.Unit.Tests/Baselines/**/*.txt,
//    which are C#). Emitter and baselines are changed independently; the baseline tests check they still agree.
// 3. Invariance check: for every symbol set, the old and new token streams must have the same kinds, every
//    non-identifier token (except rule-2 and rule-4 literals) the same text, and every comment trivia the same text.
// 4. Whole-literal names: a regular string literal whose entire text is an identifier that rule 1 renames ("json",
//    "jsonTypeInfo", "s_json_minimal", "_jsonElement") names that parameter or member (an expected
//    ArgumentException.ParamName, a reflection lookup, a message that quotes the parameter) and is renamed the same way.
//    Literals that must keep their text are listed in passb.json ("keep_literals").
// 5. Counts: passb.json records how many rule-2 and rule-4 literals the run is expected to rename. A run whose counts
//    differ fails, so that a replay on newer upstream code cannot change them unnoticed; --update-counts writes the
//    new counts to passb.json instead, and prints each change (eng/upstream/sync uses it on the target tree).
//
// Usage (from the repo root):  dotnet run eng/rename/passb/passb.cs -- [--check] [--update-counts]
// The tree to rewrite is the current directory; passb.json is read from <current directory>/eng/rename/passb.
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

bool check = args.Contains("--check");
bool updateCounts = args.Contains("--update-counts");
string repo = Directory.GetCurrentDirectory();
string configPath = Path.Combine(repo, "eng", "rename", "passb", "passb.json");
JsonObject config = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
var keepLiterals = config["keep_literals"]!.AsArray()
    .Select(k => (File: (string)k!["file"]!, Literal: (string)k!["literal"]!))
    .ToHashSet();
string[] excludedDirs = ["/bin/", "/obj/", "/src/Vendored/", "/test/Vendored/", "/test/TestShims/"];
const string EmitterFile = "src/Styml.SourceGeneration/YamlSourceGenerator.Emitter.cs";
const string BaselinesDir = "test/Styml.SourceGeneration.Unit.Tests/Baselines";

// lowercase 'json' at the start of an identifier (after an optional _, s_ or t_ field prefix), followed by an
// uppercase letter, a digit, an underscore or the end of the identifier.
var identifierPattern = new Regex(@"^(?<prefix>@?(?:_|s_|t_)?)json(?=[A-Z0-9_]|$)", RegexOptions.CultureInvariant);
var emitterLiteralPattern = new Regex(@"\bjsonTypeInfo\b", RegexOptions.CultureInvariant);
// Rule 4: the whole literal is one identifier that rule 1 renames.
var wholeLiteralPattern = new Regex(@"^""(?<prefix>(?:_|s_|t_)?)json(?<rest>(?:[A-Z0-9_][A-Za-z0-9_]*)?)""$", RegexOptions.CultureInvariant);

string[] common = ["ROSLYN4_0_OR_GREATER", "ROSLYN4_4_OR_GREATER"];
string[] netOrGreater(int max) => Enumerable.Range(5, max - 4).Select(v => $"NET{v}_0_OR_GREATER").ToArray();
string[][] symbolSets =
[
    [],
    ["NET", "NETCOREAPP", .. netOrGreater(11), .. common, "BUILDING_SOURCE_GENERATOR_TESTS", "DEBUG"],
    ["NET", "NETCOREAPP", .. netOrGreater(10), .. common],
    ["NETFRAMEWORK", "NET481", "NET462_OR_GREATER", "NET48_OR_GREATER", .. common, "BUILDING_SOURCE_GENERATOR_TESTS"],
    ["NETSTANDARD", "NETSTANDARD2_0", "BUILDING_SOURCE_GENERATOR", .. common],
    ["UPDATE_BASELINES", "LAUNCH_DEBUGGER", "NET", .. netOrGreater(11)],
];
CSharpParseOptions Options(string[] symbols) =>
    new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose, preprocessorSymbols: symbols);

int filesChanged = 0, tokensChanged = 0, fallbackOnly = 0, literalEdits = 0, wholeLiteralEdits = 0;
var renamedIdentifiers = new SortedDictionary<string, int>(StringComparer.Ordinal);

IEnumerable<string> Files(string root, string pattern) =>
    Directory.EnumerateFiles(Path.Combine(repo, root), pattern, SearchOption.AllDirectories)
        .Where(p => !excludedDirs.Any(p.Replace('\\', '/').Contains))
        .Order(StringComparer.Ordinal);

var files = Files("src", "*.cs").Concat(Files("test", "*.cs")).Concat(Files(BaselinesDir, "*.txt"));

foreach (string path in files)
{
    string rel = Path.GetRelativePath(repo, path).Replace('\\', '/');
    byte[] bytes = File.ReadAllBytes(path);
    bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    string text = new System.Text.UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
    var edits = new Dictionary<TextSpan, string>();
    var fallbackSpans = new HashSet<TextSpan>();
    var literalSpans = new HashSet<TextSpan>();

    void Consider(SyntaxToken token, bool fromFallback)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken))
            return;
        Match m = identifierPattern.Match(token.Text);
        if (!m.Success)
            return;
        if (fromFallback)
        {
            if (!edits.ContainsKey(token.Span))
                fallbackSpans.Add(token.Span);
        }
        else
        {
            fallbackSpans.Remove(token.Span);
        }
        edits[token.Span] = m.Groups["prefix"].Value + "yaml" + token.Text.Substring(m.Length);
    }

    var trees = symbolSets.Select(s => CSharpSyntaxTree.ParseText(text, Options(s), path)).ToArray();
    foreach (SyntaxTree tree in trees)
    {
        foreach (SyntaxToken token in tree.GetRoot().DescendantTokens(descendIntoTrivia: true))
            Consider(token, fromFallback: false);
    }
    foreach (SyntaxTree tree in trees)
    {
        foreach (SyntaxTrivia trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia))
                continue;
            foreach (SyntaxToken token in SyntaxFactory.ParseTokens(trivia.ToFullString(), initialTokenPosition: trivia.FullSpan.Start, options: trees[0].Options as CSharpParseOptions))
                Consider(token, fromFallback: true);
        }
    }

    if (rel == EmitterFile)
    {
        foreach (SyntaxTree tree in trees)
        {
            foreach (SyntaxToken token in tree.GetRoot().DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.InterpolatedStringTextToken) &&
                    !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) &&
                    !token.IsKind(SyntaxKind.InterpolatedRawStringEndToken))
                    continue;
                foreach (Match m in emitterLiteralPattern.Matches(token.Text))
                {
                    var span = new TextSpan(token.SpanStart + m.Index, m.Length);
                    if (edits.TryAdd(span, "yamlTypeInfo"))
                        literalSpans.Add(token.Span);
                }
            }
        }
    }

    var wholeLiteralSpans = new HashSet<TextSpan>();
    if (rel != EmitterFile)
    {
        foreach (SyntaxTree tree in trees)
        {
            foreach (SyntaxToken token in tree.GetRoot().DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.StringLiteralToken))
                    continue;
                Match m = wholeLiteralPattern.Match(token.Text);
                if (!m.Success || keepLiterals.Contains((rel, token.Text)))
                    continue;
                string renamed = "\"" + m.Groups["prefix"].Value + "yaml" + m.Groups["rest"].Value + "\"";
                if (edits.TryAdd(token.Span, renamed))
                {
                    literalSpans.Add(token.Span);
                    wholeLiteralSpans.Add(token.Span);
                }
            }
        }
    }

    if (edits.Count == 0)
        continue;

    string newText = SourceText.From(text).WithChanges(edits.Select(e => new TextChange(e.Key, e.Value))).ToString();

    // Invariance check.
    foreach (string[] symbols in symbolSets)
    {
        var oldTokens = CSharpSyntaxTree.ParseText(text, Options(symbols)).GetRoot().DescendantTokens(descendIntoTrivia: true).ToList();
        var newTokens = CSharpSyntaxTree.ParseText(newText, Options(symbols)).GetRoot().DescendantTokens(descendIntoTrivia: true).ToList();
        if (oldTokens.Count != newTokens.Count)
            throw new Exception($"{rel}: token count changed under [{string.Join(",", symbols)}]");
        for (int i = 0; i < oldTokens.Count; i++)
        {
            SyntaxToken o = oldTokens[i], n = newTokens[i];
            if (!o.IsKind(n.Kind()))
                throw new Exception($"{rel}: token kind changed at {o.GetLocation().GetLineSpan()}: {o.Kind()} -> {n.Kind()}");
            bool mayDiffer = o.IsKind(SyntaxKind.IdentifierToken) || literalSpans.Contains(o.Span);
            if (!mayDiffer && o.Text != n.Text)
                throw new Exception($"{rel}: non-identifier token changed at {o.GetLocation().GetLineSpan()}: '{o.Text}' -> '{n.Text}'");
            var oc = o.LeadingTrivia.Concat(o.TrailingTrivia).Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)).Select(t => t.ToString());
            var nc = n.LeadingTrivia.Concat(n.TrailingTrivia).Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)).Select(t => t.ToString());
            if (!oc.SequenceEqual(nc))
                throw new Exception($"{rel}: comment changed near {o.GetLocation().GetLineSpan()}");
        }
    }

    foreach (var (span, replacement) in edits)
    {
        string old = text.Substring(span.Start, span.Length);
        string key = wholeLiteralSpans.Contains(span) ? $"{old} (whole literal)"
            : literalSpans.Any(s => s.Contains(span)) ? $"\"{old}\" (emitter string)" : old;
        renamedIdentifiers[key] = renamedIdentifiers.GetValueOrDefault(key) + 1;
    }

    literalEdits += edits.Keys.Count(s => !wholeLiteralSpans.Contains(s) && literalSpans.Any(l => l.Contains(s)));
    wholeLiteralEdits += wholeLiteralSpans.Count;
    fallbackOnly += fallbackSpans.Count;
    tokensChanged += edits.Count;
    filesChanged++;
    if (!check)
        File.WriteAllText(path, newText, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom));
}

Console.WriteLine($"{(check ? "would change" : "changed")} {tokensChanged} tokens in {filesChanged} files " +
                  $"({fallbackOnly} only via inactive-region re-lexing, {literalEdits} in emitter string literals, " +
                  $"{wholeLiteralEdits} whole-literal names)");
foreach (var (name, count) in renamedIdentifiers.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
    Console.WriteLine($"  {count,6}  {name}");

// Rule 5.
JsonObject expected = config["expected_counts"]!.AsObject();
var actual = new Dictionary<string, int> { ["emitter_literals"] = literalEdits, ["whole_literals"] = wholeLiteralEdits };
var mismatches = actual.Where(kv => (int?)expected[kv.Key] != kv.Value).ToList();
foreach (var (name, count) in mismatches)
    Console.WriteLine($"expected_counts.{name}: passb.json says {(int?)expected[name]}, the run renamed {count}");
if (mismatches.Count > 0)
{
    if (!updateCounts)
    {
        Console.Error.WriteLine($"{configPath}: expected counts differ (rerun with --update-counts to record the new counts)");
        return 1;
    }
    foreach (var (name, count) in mismatches)
        expected[name] = count;
    File.WriteAllText(configPath, config.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    Console.WriteLine($"updated {configPath}");
}
return 0;
