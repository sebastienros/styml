// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// yaml-test-suite read with <see cref="YamlReaderOptions.ApplyYaml13DraftRestrictions"/>: the cases the suite
    /// marks as invalid in YAML 1.3 are rejected, each by the rule listed for it, the cases it rewrote for 1.3 are
    /// accepted, and every error case is still rejected.
    /// </summary>
    /// <remarks>
    /// The suite's 1.3 marks are incomplete: other valid cases hold the same forms. The rules reject those too, and
    /// draft13-also-rejected.txt lists each of them with its rule. Every other valid case must give its expected
    /// events, as without the option.
    /// </remarks>
    public class YamlTestSuiteDraft13Tests
    {
        private static readonly YamlReaderOptions s_options = new() { AllowMultipleValues = true, MaxDepth = 256, ApplyYaml13DraftRestrictions = true };

        /// <summary>The rules, by the name the lists use, with a part of the message that tells each one apart.</summary>
        internal static readonly Dictionary<string, string> Rules = new(StringComparer.Ordinal)
        {
            ["root-scalar-without-marker"] = "needs a '---' line",
            ["scalar-on-marker-line"] = "may not start on the '---' line",
            ["root-block-scalar-indentation"] = "must be indented",
            ["properties-on-own-line"] = "may not be alone on its line",
            ["anchor-name"] = "anchor or alias name may only",
            ["implicit-collection-key"] = "may not be an implicit key of a block mapping",
        };

        private const string InvalidList = "draft13-invalid.txt";
        private const string RewrittenList = "draft13-rewritten.txt";
        private const string AlsoRejectedList = "draft13-also-rejected.txt";

        private static readonly Dictionary<string, (string File, string Rule)> s_rejected =
            Entries(InvalidList).Concat(Entries(AlsoRejectedList)).ToDictionary(e => e.Id, e => (e.File, e.Note), StringComparer.Ordinal);

        public static IEnumerable<object[]> Cases() => YamlTestSuite.AllCaseIds().Select(id => new object[] { id });

        [Theory]
        [MemberData(nameof(Cases))]
        public void Case(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            List<string>? actual = null;
            string? error = null;
            try
            {
                var reader = new Utf8YamlReader(c.Input, s_options);
                actual = YamlEventTrace.Read(ref reader);
            }
            catch (YamlException e)
            {
                error = e.Message;
            }

            if (c.IsError)
            {
                Assert.True(error is not null, $"{id} ({c.Title}) must be rejected.");
                return;
            }

            if (s_rejected.TryGetValue(id, out (string File, string Rule) listed))
            {
                Assert.True(error is not null, $"{id} ({c.Title}) is accepted; remove it from {listed.File}.");
                Assert.Contains(Rules[listed.Rule], error);
                return;
            }

            Assert.True(error is null, $"{id} ({c.Title}) was rejected: {error}");
            Assert.Equal(c.ExpectedEvents, actual);
        }

        /// <summary>
        /// The verdict of a bare read loop, which never opens a collection key's reader: the same cases are rejected
        /// and accepted as through the event trace, which does open them.
        /// </summary>
        [Theory]
        [MemberData(nameof(Cases))]
        public void PlainReadLoop(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            string? error = null;
            try
            {
                var reader = new Utf8YamlReader(c.Input, s_options);
                while (reader.Read())
                {
                }
            }
            catch (YamlException e)
            {
                error = e.Message;
            }

            if (c.IsError)
            {
                Assert.True(error is not null, $"{id} ({c.Title}) must be rejected.");
            }
            else if (s_rejected.TryGetValue(id, out (string File, string Rule) listed))
            {
                Assert.True(error is not null, $"{id} ({c.Title}) is accepted by a bare read loop.");
                Assert.Contains(Rules[listed.Rule], error);
            }
            else
            {
                Assert.True(error is null, $"{id} ({c.Title}) was rejected: {error}");
            }
        }

        /// <summary>
        /// Comments returned as tokens (CommentHandling.Allow) do not change the verdict: a case is rejected at the
        /// same place, by the same rule, as with comments skipped.
        /// </summary>
        [Theory]
        [MemberData(nameof(Cases))]
        public void CommentTokens_LeaveTheVerdict(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            string? Verdict(YamlCommentHandling comments)
            {
                YamlReaderOptions options = s_options;
                options.CommentHandling = comments;
                try
                {
                    var reader = new Utf8YamlReader(c.Input, options);
                    while (reader.Read())
                    {
                    }
                    return null;
                }
                catch (YamlException e)
                {
                    return e.Message;
                }
            }

            Assert.Equal(Verdict(YamlCommentHandling.Skip), Verdict(YamlCommentHandling.Allow));
        }

        [Fact]
        public void ListsAreWellFormed()
        {
            var valid = new HashSet<string>(YamlTestSuite.AllCaseIds().Where(id => !YamlTestSuite.Load(id).IsError), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string file, string id, string note) in Entries(InvalidList).Concat(Entries(AlsoRejectedList)).Concat(Entries(RewrittenList)))
            {
                Assert.True(valid.Contains(id), $"{file}: '{id}' is not a valid yaml-test-suite case.");
                Assert.True(seen.Add(id), $"'{id}' is listed more than once.");
                if (file == RewrittenList)
                {
                    Assert.True(valid.Contains(note), $"{file}: '{id}' rewrites '{note}', which is not a valid case.");
                }
                else
                {
                    Assert.True(Rules.ContainsKey(note), $"{file}: '{id}' names the unknown rule '{note}'.");
                }
            }

            // The counts of tags/1.3-err and tags/1.3-mod in the vendored release.
            Assert.Equal(30, Entries(InvalidList).Count());
            Assert.Equal(22, Entries(RewrittenList).Count());

            // Every rule is needed by a case the suite marks.
            Assert.Equal(Rules.Keys.OrderBy(r => r, StringComparer.Ordinal), Entries(InvalidList).Select(e => e.Note).Distinct().OrderBy(r => r, StringComparer.Ordinal));
        }

        private static IEnumerable<(string File, string Id, string Note)> Entries(string file)
        {
            foreach (string raw in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "YamlTestSuite", file)))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                int space = line.IndexOf(' ');
                yield return space < 0 ? (file, line, "") : (file, line.Substring(0, space), line.Substring(space + 1).Trim());
            }
        }
    }
}
