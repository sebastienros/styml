// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M4p candidate a: a pre-encoded name
    /// carries its block key decision, made when it was encoded. The cached decision must not depend on the writer. So
    /// each name here is written through writers with <c>QuoteForLenientReaders</c> on and off, in block and in flow,
    /// after an anchor and a tag, and with a scalar hint. It goes through every entry point that takes the cached
    /// decision, as a <see cref="YamlEncodedText"/> and as a string (the uncached path), and the bytes must be equal.
    /// </summary>
    public class EncodedNameKeyCacheTests
    {
        private static readonly string[] s_names =
        {
            // Plain under both settings.
            "k", "name", "apiVersion", "x-y", "a_b", "a.b", "a b", "key with a long name",

            // Resolved by the standards (quoted under both).
            "true", "No", "null", "~", "123", "0x1F", "1e3", "12:30", "2026-11-21", "y", "N", "on", "OFF",

            // Quoted only for lenient readers.
            "Yes", "YES", "tRUE", "Off", "1_000", "+1", ".5", "0o17", "1:20", "=", "<<", "a *b", " lead", "trail ",

            // Never plain.
            "", "with:colon", "- x", "#c", "a #b", "multi\nline", "\"dq\"", "'q'", "[x]", "{x}", ",", "&a", "*a", "!t",
            "%p", "@a", "`t", "|p", ">g", "café", "日本", "tab\there", "\u0001ctl",
        };

        public static IEnumerable<object[]> Settings()
        {
            foreach (bool lenient in new[] { false, true })
            {
                foreach (bool flow in new[] { false, true })
                {
                    foreach (int prefix in new[] { 0, 1, 2, 3 })
                    {
                        yield return new object[] { lenient, flow, prefix };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void EncodedName_WritesTheSameBytesAsTheString(bool lenient, bool flow, int prefix)
        {
            var options = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = lenient };
            foreach (string name in s_names)
            {
                YamlEncodedText encoded = YamlEncodedText.Encode(name);
                for (int form = 0; form < 6; form++)
                {
                    string expected = Capture(() => BlockWriterTests.Write(w => WriteOne(w, flow, prefix, form, name, default, useEncoded: false), options));
                    string actual = Capture(() => BlockWriterTests.Write(w => WriteOne(w, flow, prefix, form, name, encoded, useEncoded: true), options));
                    Assert.True(expected == actual, $"name {Show(name)}, form {form}, lenient {lenient}, flow {flow}, prefix {prefix}:\n{expected}\n---\n{actual}");
                }
            }
        }

        // `prefix`: 0 nothing, 1 an anchor, 2 a tag, 3 a scalar hint, all before the key.
        private static void WriteOne(Utf8YamlWriter w, bool flow, int prefix, int form, string name, YamlEncodedText encoded, bool useEncoded)
        {
            if (flow)
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
            }

            w.WriteStartObject();
            w.WritePropertyName("first");
            w.WriteNumberValue(0);
            switch (prefix)
            {
                case 1: w.WriteAnchor("a1"); break;
                case 2: w.WriteTag("!t"); break;
                case 3: w.WriteScalarStyle(YamlScalarStyle.SingleQuoted); break;
            }

            switch (form)
            {
                case 0:
                    if (useEncoded) w.WritePropertyName(encoded); else w.WritePropertyName(name);
                    w.WriteStringValue("v");
                    break;
                case 1:
                    if (useEncoded) w.WriteString(encoded, "v"); else w.WriteString(name, "v");
                    break;
                case 2:
                    if (useEncoded) w.WriteNumber(encoded, 42L); else w.WriteNumber(name, 42L);
                    break;
                case 3:
                    if (useEncoded) w.WriteBoolean(encoded, true); else w.WriteBoolean(name, true);
                    break;
                case 4:
                    if (useEncoded) w.WriteNull(encoded); else w.WriteNull(name);
                    break;
                default:
                    if (useEncoded) w.WriteNumber(encoded, 7); else w.WriteNumber(name, 7);
                    break;
            }

            w.WriteEndObject();
        }

        // A combination the writer refuses must be refused the same way on both paths.
        private static string Capture(Func<string> write)
        {
            try
            {
                return write();
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                return "! " + e.GetType().Name;
            }
        }

        private static string Show(string s) => "\"" + s.Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }
}
