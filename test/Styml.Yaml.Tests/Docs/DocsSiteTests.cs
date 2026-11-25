// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace System.Text.Yaml.Tests.Docs
{
    /// <summary>
    /// The pages of the documentation site against their samples: every sample block of docs/_content/styml/guide/*.md
    /// equals what eng/docs/site.py renders from Docs/*.cs and Docs/expected, and every sample is shown by a page. The
    /// files are copied next to the tests (see the project file).
    /// </summary>
    public class DocsSiteTests
    {
        private static string Folder(string name) => Path.Combine(AppContext.BaseDirectory, "Docs", name);

        [Fact]
        public void SamplesMatchThePages()
        {
            var samples = new Dictionary<string, (string? Language, string Code)>();
            foreach (string file in Directory.GetFiles(Folder("sources"), "*.cs.txt"))
            {
                foreach (KeyValuePair<string, (string?, string)> sample in Regions(File.ReadAllText(file).Replace("\r\n", "\n")))
                {
                    Assert.False(samples.ContainsKey(sample.Key), $"Sample '{sample.Key}' is defined twice.");
                    samples[sample.Key] = sample.Value;
                }
            }

            var shown = new HashSet<string>();
            foreach (string page in Directory.GetFiles(Folder("pages"), "*.md"))
            {
                string text = File.ReadAllText(page).Replace("\r\n", "\n");
                foreach (Match block in Regex.Matches(text, @"<!-- sample:([a-z0-9-]+) -->.*?<!-- /sample:\1 -->", RegexOptions.Singleline))
                {
                    string name = block.Groups[1].Value;
                    Assert.True(samples.ContainsKey(name), $"{Path.GetFileName(page)} shows sample '{name}', which no Docs/*.cs file defines.");
                    (string? language, string code) = samples[name];
                    string expected = language is null ? "" : File.ReadAllText(Path.Combine(Folder("expected"), name + ".txt")).Replace("\r\n", "\n");
                    Assert.True(Render(name, language, code, expected) == block.Value,
                        $"{Path.GetFileName(page)}'s sample '{name}' differs from its source: run python3 eng/docs/site.py.");
                    shown.Add(name);
                }
            }

            Assert.Empty(samples.Keys.Except(shown));
        }

        // The same parsing and rendering as eng/docs/site.py.
        private static Dictionary<string, (string? Language, string Code)> Regions(string source)
        {
            var found = new Dictionary<string, (string?, string)>();
            string? current = null, language = null;
            var lines = new List<string>();
            foreach (string line in source.Split('\n'))
            {
                Match marker = Regex.Match(line, @"^\s*// docs: ([a-z0-9-]+)(?: ([a-z]+))?\s*$");
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
