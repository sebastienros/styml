// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.IO;
using Xunit;

namespace System.Text.Yaml.Tests.Docs
{
    /// <summary>
    /// The samples of the documentation site (docs/_content/styml/guide). Each region of a Docs/*.cs file between
    /// "// docs: name [language]" and "// docs: end" is the code a page shows, and Docs/expected/name.txt, when the
    /// marker names its language, is the output it shows; eng/docs/site.py copies both into the pages and
    /// DocsSiteTests fails when they drift. Outputs are compared with LF line breaks. With STYML_DOCS_RECORD set to the
    /// Docs folder, a sample writes its output there instead of comparing it.
    /// </summary>
    internal static class DocSample
    {
        /// <summary>Compares the output of sample <paramref name="name"/> with the one the page shows.</summary>
        /// <param name="defaultStyle">Whether the output depends on the writer's default style, which the
        /// JSON-compatible test leg (STYML_TEST_WRITER=json) changes: it is then not compared.</param>
        public static void Check(string name, string output, bool defaultStyle = true)
        {
            output = output.Replace("\r\n", "\n");
            string? record = Environment.GetEnvironmentVariable("STYML_DOCS_RECORD");
            if (!string.IsNullOrEmpty(record))
            {
                Directory.CreateDirectory(Path.Combine(record, "expected"));
                File.WriteAllText(Path.Combine(record, "expected", name + ".txt"), output);
                return;
            }

            if (defaultStyle && GrammarModeInitializer.ExpectJsonCompatibleWriter)
            {
                return;
            }

            string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Docs", "expected", name + ".txt")).Replace("\r\n", "\n");
            Assert.Equal(expected, output);
        }

        /// <summary>
        /// Whether a sample that only works with the block writer (anchors, tags, collection keys) is skipped: the
        /// JSON-compatible test leg (STYML_TEST_WRITER=json) has no form for them.
        /// </summary>
        public static bool BlockDefaultOnly() => GrammarModeInitializer.ExpectJsonCompatibleWriter;

        /// <summary>The value <paramref name="read"/> returns, or the type and first line of the exception it throws.</summary>
        public static string Describe(Func<object?> read)
        {
            try
            {
                return read()?.ToString() ?? "null";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message.Split('\n')[0];
            }
        }
    }
}
