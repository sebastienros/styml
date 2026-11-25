// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Exit criterion 9: the public API of Styml is the M2b API (commit bf55f5a) plus M3's additions and the M3b
    /// alias-key flag, and nothing else.
    /// </summary>
    /// <remarks>
    /// PublicApi/m2b.&lt;tfm&gt;.txt were produced by <see cref="PublicApiListing"/> over the bf55f5a build of each
    /// library target (eng/test/public-api/README.md). PublicApi/m3a-additions.txt holds M3a's additions and
    /// m3b-additions.txt, m3d-additions.txt, m4-additions.txt, m4b-reader-additions.txt, m5a-additions.txt,
    /// m5b-additions.txt, m5c-additions.txt, m5d-additions.txt, m5e-additions.txt and m5f-additions.txt the M3b, M3d, M4, M4b
    /// reader-step, M5a, M5b, M5c, M5d, M5e and M5f ones; all are the same for every target. m5-additions.txt is their M5
    /// union, for the end-of-M5 check. m7b-additions.txt holds M7b's. Later additions are in files named after what
    /// they add (yaml13-draft-additions.txt).
    /// m7-removals.txt lists what M7 removed (M7c: the inherited obsolete APIs), subtracted from the expected listing.
    /// </remarks>
    public class PublicApiTests
    {
        [Fact]
        public void PublicApiIsM2bPlusSection4Additions()
        {
            Assembly styml = typeof(Utf8YamlReader).Assembly;
            string target = PublicApiListing.TargetMoniker(styml);
            string dir = Path.Combine(AppContext.BaseDirectory, "PublicApi");

            var baseline = new SortedSet<string>(ReadListing(Path.Combine(dir, $"m2b.{target}.txt")), StringComparer.Ordinal);
            var additions = new SortedSet<string>(
                Directory.GetFiles(dir, "*-additions.txt").SelectMany(ReadListing), StringComparer.Ordinal);
            Assert.Empty(baseline.Intersect(additions));

            var expected = new SortedSet<string>(baseline.Concat(additions), StringComparer.Ordinal);

            // M7c: the removals (m*-removals.txt) leave the
            // baseline or an earlier step's additions; each must be a line of one of them.
            // A removal of one target's API only is listed in m*-removals.<target>.txt (M7c: the exception serialization
            // members, obsolete and removed on .NET only).
            var removals = new SortedSet<string>(
                Directory.GetFiles(dir, "m*-removals.txt").Concat(Directory.GetFiles(dir, $"m*-removals.{target}.txt")).SelectMany(ReadListing),
                StringComparer.Ordinal);
            string[] unknown = removals.Except(expected).ToArray();
            Assert.True(unknown.Length == 0, $"Removals that are not in the baseline or the additions:\n  {string.Join("\n  ", unknown)}");
            expected.ExceptWith(removals);

            var actual = new SortedSet<string>(PublicApiListing.List(styml), StringComparer.Ordinal);

            string[] missing = expected.Except(actual).ToArray();
            string[] extra = actual.Except(expected).ToArray();
            Assert.True(missing.Length == 0 && extra.Length == 0,
                $"Public API of {target} differs.\nMissing:\n  {string.Join("\n  ", missing)}\nUnexpected:\n  {string.Join("\n  ", extra)}");
        }

        [Fact]
        public void M5AdditionsAreTheUnionOfTheSteps()
        {
            // m5-additions.txt lists exactly the additions of M5a to M5f.
            string dir = Path.Combine(AppContext.BaseDirectory, "PublicApi");
            var steps = new SortedSet<string>(
                new[] { "m5a", "m5b", "m5c", "m5d", "m5e", "m5f" }.SelectMany(step => ReadListing(Path.Combine(dir, step + "-additions.txt"))),
                StringComparer.Ordinal);
            var m5 = new SortedSet<string>(ReadListing(Path.Combine(dir, "m5-additions.txt")), StringComparer.Ordinal);
            Assert.True(steps.SetEquals(m5), $"m5-additions.txt differs from the steps' union.\nMissing:\n  {string.Join("\n  ", steps.Except(m5))}\nExtra:\n  {string.Join("\n  ", m5.Except(steps))}");
        }

        private static IEnumerable<string> ReadListing(string path) =>
            File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal));
    }
}
