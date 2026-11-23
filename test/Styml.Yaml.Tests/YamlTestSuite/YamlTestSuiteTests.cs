// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// yaml-test-suite (test/yaml-test-suite, release data-2022-01-17) through the event-trace adapter: accept/reject
    /// agreement and event-trace equality (exit criterion 3).
    /// </summary>
    /// <remarks>
    /// The only exceptions are the cases on the three checked-in lists next to this file. A listed case must still
    /// behave as listed: once it passes, the test fails until the entry is removed.
    /// </remarks>
    public class YamlTestSuiteTests
    {
        public static IEnumerable<object[]> Cases() => YamlTestSuite.AllCaseIds().Select(id => new object[] { id });

        [Theory]
        [MemberData(nameof(Cases))]
        public void Case(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            YamlTestSuite.Outcome outcome = YamlTestSuite.Run(c, out List<string>? actual, out string? error);

            YamlTestSuite.ListKind listed = YamlTestSuite.ListedAs(id, out string? reason);
            switch (listed)
            {
                case YamlTestSuite.ListKind.FalseAccept:
                    Assert.True(c.IsError, $"{id} is on false-accepts.txt but is not an error case.");
                    Assert.True(outcome != YamlTestSuite.Outcome.Rejected, $"{id} is now rejected; remove it from false-accepts.txt ({reason}).");
                    return;

                case YamlTestSuite.ListKind.FalseReject:
                    Assert.False(c.IsError, $"{id} is on false-rejects.txt but is an error case.");
                    Assert.True(outcome == YamlTestSuite.Outcome.Rejected, $"{id} is now accepted; remove it from false-rejects.txt ({reason}).");
                    return;

                case YamlTestSuite.ListKind.TraceDifference:
                    Assert.False(c.IsError, $"{id} is on trace-differences.txt but is an error case.");
                    Assert.True(outcome == YamlTestSuite.Outcome.TraceDiffers, outcome == YamlTestSuite.Outcome.Passed
                        ? $"{id} now produces the expected trace; remove it from trace-differences.txt ({reason})."
                        : $"{id} is listed as a trace difference but is {outcome}: {error}");
                    return;
            }

            if (c.IsError)
            {
                Assert.True(outcome == YamlTestSuite.Outcome.Rejected, $"{id} ({c.Title}) must be rejected. Trace:\n{string.Join("\n", actual ?? new List<string>())}");
            }
            else
            {
                Assert.True(outcome != YamlTestSuite.Outcome.Rejected, $"{id} ({c.Title}) was rejected: {error}");
                Assert.Equal(c.ExpectedEvents, actual);
            }
        }

        /// <summary>
        /// The verdict of a bare read loop, which never opens a Complex key's reader: an error case must fail and a
        /// valid case must not, so a key the main reader accepts wrongly cannot pass as rejected through the adapter.
        /// </summary>
        [Theory]
        [MemberData(nameof(Cases))]
        public void PlainReadLoop(string id)
        {
            YamlTestSuite.Case c = YamlTestSuite.Load(id);
            void ReadAll()
            {
                var reader = new Utf8YamlReader(c.Input, new YamlReaderOptions { AllowMultipleValues = true, MaxDepth = 256 });
                while (reader.Read())
                {
                }
            }
            if (c.IsError)
            {
                Assert.ThrowsAny<YamlException>(ReadAll);
            }
            else
            {
                ReadAll();
            }
        }

        [Fact]
        public void ListsAreWellFormed()
        {
            var ids = new HashSet<string>(YamlTestSuite.AllCaseIds(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string file, string id, string reason) in YamlTestSuite.ListEntries())
            {
                Assert.True(ids.Contains(id), $"{file}: '{id}' is not a yaml-test-suite case.");
                Assert.True(seen.Add(id), $"{file}: '{id}' is listed more than once.");
                Assert.False(string.IsNullOrWhiteSpace(reason), $"{file}: '{id}' has no reason.");
            }
        }

        [Fact]
        public void SuiteIsComplete()
        {
            // The vendored release has 402 cases (THIRD-PARTY-NOTICES.txt).
            Assert.Equal(402, YamlTestSuite.AllCaseIds().Count());
        }
    }

    internal static class YamlTestSuite
    {
        internal enum Outcome { Passed, TraceDiffers, Rejected }

        internal enum ListKind { None, FalseAccept, FalseReject, TraceDifference }

        internal sealed class Case
        {
            public string Id = "";
            public string Title = "";
            public bool IsError;
            public byte[] Input = Array.Empty<byte>();
            public List<string> ExpectedEvents = new();
        }

        internal static string Root => Path.Combine(AppContext.BaseDirectory, "yaml-test-suite");

        private static readonly string ListsDirectory = Path.Combine(AppContext.BaseDirectory, "YamlTestSuite");

        internal static IEnumerable<string> AllCaseIds() =>
            Directory.GetFiles(Root, "in.yaml", SearchOption.AllDirectories)
                .Select(f => Path.GetDirectoryName(f)!.Substring(Root.Length + 1).Replace('\\', '/'))
                .OrderBy(id => id, StringComparer.Ordinal);

        internal static Case Load(string id)
        {
            string dir = Path.Combine(Root, id.Replace('/', Path.DirectorySeparatorChar));
            string titleFile = Path.Combine(dir, "===");
            return new Case
            {
                Id = id,
                Title = File.Exists(titleFile) ? File.ReadAllText(titleFile).Trim() : "",
                IsError = File.Exists(Path.Combine(dir, "error")),
                Input = File.ReadAllBytes(Path.Combine(dir, "in.yaml")),
                ExpectedEvents = YamlEventTrace.NormalizeExpected(File.ReadAllLines(Path.Combine(dir, "test.event"))),
            };
        }

        internal static Outcome Run(Case c, out List<string>? actual, out string? error)
        {
            try
            {
                actual = YamlEventTrace.Read(c.Input);
                error = null;
            }
            catch (YamlException e)
            {
                actual = null;
                error = e.Message;
                return Outcome.Rejected;
            }

            return actual.SequenceEqual(c.ExpectedEvents) ? Outcome.Passed : Outcome.TraceDiffers;
        }

        internal static IEnumerable<(string File, string Id, string Reason)> ListEntries()
        {
            foreach (string file in new[] { "false-accepts.txt", "false-rejects.txt", "trace-differences.txt" })
            {
                foreach (string raw in File.ReadAllLines(Path.Combine(ListsDirectory, file)))
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

        internal static ListKind ListedAs(string id, out string? reason)
        {
            foreach ((string file, string listedId, string r) in ListEntries())
            {
                if (listedId == id)
                {
                    reason = r;
                    return file switch
                    {
                        "false-accepts.txt" => ListKind.FalseAccept,
                        "false-rejects.txt" => ListKind.FalseReject,
                        _ => ListKind.TraceDifference,
                    };
                }
            }
            reason = null;
            return ListKind.None;
        }
    }
}
