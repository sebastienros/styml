// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace System.Text.Yaml.Tests.Readme
{
    /// <summary>
    /// The README against its sources: every sample block equals what eng/docs/readme.py renders from ReadmeSamples.cs
    /// and Readme/expected, and the options reference (docs/options.md) names every member Styml added to an options
    /// type or an attribute (from the PublicApi addition lists). The files are copied next to the tests (see the project file).
    /// </summary>
    public class ReadmeDocTests
    {
        private static string Read(params string[] path) =>
            File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory }.Concat(path).ToArray())).Replace("\r\n", "\n");

        [Fact]
        public void SamplesMatchTheReadme()
        {
            string readme = Read("Readme", "README.md");
            Dictionary<string, (string? Language, string Code)> samples = Regions(Read("Readme", "ReadmeSamples.cs.txt"));
            MatchCollection blocks = Regex.Matches(readme, @"<!-- sample:([a-z0-9-]+) -->.*?<!-- /sample:\1 -->", RegexOptions.Singleline);

            var shown = new HashSet<string>();
            foreach (Match block in blocks)
            {
                string name = block.Groups[1].Value;
                Assert.True(samples.ContainsKey(name), $"README.md shows sample '{name}', which ReadmeSamples.cs does not define.");
                (string? language, string code) = samples[name];
                string expected = language is null ? "" : Read("Readme", "expected", name + ".txt");
                Assert.True(Render(name, language, code, expected) == block.Value,
                    $"README.md's sample '{name}' differs from ReadmeSamples.cs: run python3 eng/docs/readme.py.");
                shown.Add(name);
            }

            Assert.Empty(samples.Keys.Except(shown));
        }

        [Fact]
        public void OptionsReferenceNamesEveryAddedMember()
        {
            string reference = Read("Readme", "options.md");
            int start = reference.IndexOf("<!-- options-reference -->", StringComparison.Ordinal);
            int end = reference.IndexOf("<!-- /options-reference -->", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "docs/options.md has no options reference block.");
            reference = reference.Substring(start, end - start);

            // "### `TypeName`" sections of the reference, each with its text.
            var sections = new Dictionary<string, string>();
            foreach (Match section in Regex.Matches(reference, @"^### `(\w+)`(.*?)(?=^### |\z)", RegexOptions.Singleline | RegexOptions.Multiline))
            {
                sections[section.Groups[1].Value] = section.Groups[2].Value;
            }

            var missing = new List<string>();
            foreach ((string type, string member) in AddedOptionsMembers())
            {
                if (!sections.TryGetValue(type, out string? text) || text.IndexOf("`" + member + "`", StringComparison.Ordinal) < 0)
                {
                    missing.Add(type + "." + member);
                }
            }

            Assert.True(missing.Count == 0, "The options reference does not name: " + string.Join(", ", missing));
        }

        // The properties Styml added to the options types and attributes, and the added values that work as options.
        private static IEnumerable<(string Type, string Member)> AddedOptionsMembers()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "PublicApi");
            HashSet<string> removed = new(Directory.GetFiles(dir, "m7-removals*.txt").SelectMany(File.ReadAllLines));
            var seen = new HashSet<(string, string)>();
            foreach (string line in Directory.GetFiles(dir, "*-additions.txt").SelectMany(File.ReadAllLines))
            {
                if (line.Length == 0 || line[0] == '#' || removed.Contains(line))
                {
                    continue;
                }

                Match property = Regex.Match(line, @"^[\w.]+\.(\w+(?:Options|Attribute))\.(\w+) property ");
                Match value = Regex.Match(line, @"^[\w.]+\.(YamlIgnoreCondition|ReferenceHandler)\.(WhenWritingEmptyCollection|PreserveAll) ");
                Match match = property.Success ? property : value;
                if (match.Success && seen.Add((match.Groups[1].Value, match.Groups[2].Value)))
                {
                    yield return (match.Groups[1].Value, match.Groups[2].Value);
                }
            }
        }

        // The same parsing and rendering as eng/docs/readme.py.
        private static Dictionary<string, (string? Language, string Code)> Regions(string source)
        {
            var found = new Dictionary<string, (string?, string)>();
            string? current = null, language = null;
            var lines = new List<string>();
            foreach (string line in source.Split('\n'))
            {
                Match marker = Regex.Match(line, @"^\s*// readme: ([a-z0-9-]+)(?: ([a-z]+))?\s*$");
                if (marker.Success && marker.Groups[1].Value != "end")
                {
                    current = marker.Groups[1].Value;
                    language = marker.Groups[2].Success ? marker.Groups[2].Value : null;
                    lines.Clear();
                }
                else if (marker.Success && current is not null)
                {
                    int cut = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart(' ').Length).DefaultIfEmpty(0).Min();
                    found[current] = (language, string.Join("\n", lines.Select(l => l.Trim().Length > 0 ? l.Substring(cut) : "")));
                    current = null;
                }
                else if (current is not null)
                {
                    lines.Add(line);
                }
            }

            return found;
        }

        private static string Render(string name, string? language, string code, string expected)
        {
            string text = $"<!-- sample:{name} -->\n```csharp\n{code}\n```\n";
            if (language is not null)
            {
                text += $"\n```{language}\n{expected.TrimEnd('\n')}\n```\n";
            }

            return text + $"<!-- /sample:{name} -->";
        }
    }
}
