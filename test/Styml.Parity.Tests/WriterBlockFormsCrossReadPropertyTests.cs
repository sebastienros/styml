// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// G5 for M4b's output forms: the strings of <see cref="WriterBlockCrossReadPropertyTests"/> written inside flow
    /// collections, with the single-quoted and folded hints, with <see cref="YamlWriterOptions.MaxLineWidth"/>, and
    /// anchored with an alias to each, as sequence items, mapping values and keys. YamlDotNet and SharpYaml read each
    /// output into <c>object</c>, and every string must come back as a string with the same text (an alias as the
    /// anchored string). Like the block test, its failures define the quoting set: here, the plain strings of flow
    /// context. It runs in two configurations, as the block test does: with
    /// QuoteForLenientReaders a hard oracle, without it the misread classes pinned per form.
    /// </summary>
    public class WriterBlockFormsCrossReadPropertyTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();

        // SharpYaml resolves no alias to a scalar into object, even when it preserves references ("Unknown YAML alias"):
        // it reads the anchored items without their aliases, and YamlDotNet checks the aliases.
        private static readonly SY.YamlSerializerOptions s_sharpYaml = new() { ReferenceHandling = SY.YamlReferenceHandling.Preserve };

        public enum Form
        {
            Flow,
            SingleQuoted,
            Folded,
            Width,
            Anchored,
        }

        public static IEnumerable<object[]> Forms() => Enum.GetValues(typeof(Form)).Cast<Form>().Select(f => new object[] { f });

        [Theory]
        [MemberData(nameof(Forms))]
        public void GeneratedStrings_ReadBackAsStrings(Form form) => Run(form, 8_000, 20261122 + (int)form, lenient: true);

        [Theory]
        [MemberData(nameof(Forms))]
        public void GeneratedStrings_Default_MisreadClassesArePinned(Form form)
        {
            var random = new Random(20261122 + (int)form);
            var classes = new SortedSet<string>(StringComparer.Ordinal);
            const int Batch = 200;
            for (int done = 0; done < 8_000; done += Batch)
            {
                int width = 8 + random.Next(40);
                string[] strings = Strings(random, form, Math.Min(Batch, 8_000 - done));
                if (!BatchReadsBack(strings, form, width, lenient: false))
                {
                    foreach (string s in strings)
                    {
                        foreach (string failure in Check(s, form, width, lenient: false))
                        {
                            string library = failure.Substring(0, failure.IndexOf(','));
                            string position = failure.Split(", ")[2].Split(':')[0];
                            classes.Add($"{library}, {position}: " + WriterBlockCrossReadPropertyTests.MisreadClass(
                                s, position == "key", form == Form.Folded && position != "key", form == Form.Flow));
                        }
                    }
                }
            }

            string[] pinned = DefaultMisreads.TryGetValue(form, out string[]? p) ? p : Array.Empty<string>();
            Assert.True(pinned.SequenceEqual(classes), $"The misread classes of {form} are now:\n" + string.Join("\n", classes.Select(c => "                \"" + c + "\",")));
        }

        // Pinned on 2026-11-19 (8,000 strings per form, the seeds of GeneratedStrings_ReadBackAsStrings).
        private static readonly Dictionary<Form, string[]> DefaultMisreads = new()
        {
            [Form.Flow] =
            [
                "SharpYaml, item without aliases: '?' or ':' in a flow plain scalar",
                "SharpYaml, item without aliases: Unicode white space at an edge",
                "SharpYaml, item without aliases: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item without aliases: a keyword in a case no standard lists",
                "SharpYaml, item without aliases: a number with '_' where no standard has it",
                "SharpYaml, item without aliases: an IEEE name in a form no standard lists",
                "SharpYaml, item: '?' or ':' in a flow plain scalar",
                "SharpYaml, item: Unicode white space at an edge",
                "SharpYaml, item: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item: a keyword in a case no standard lists",
                "SharpYaml, item: a number with '_' where no standard has it",
                "SharpYaml, item: an IEEE name in a form no standard lists",
                "SharpYaml, key: '?' or ':' in a flow plain scalar",
                "SharpYaml, value: '?' or ':' in a flow plain scalar",
                "SharpYaml, value: Unicode white space at an edge",
                "SharpYaml, value: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, value: a keyword in a case no standard lists",
                "SharpYaml, value: a number with '_' where no standard has it",
                "SharpYaml, value: an IEEE name in a form no standard lists",
                "YamlDotNet, key: a key with ' *'",
            ],
            [Form.Folded] =
            // M5f: a first line that starts with a tab is no longer a block scalar, so SharpYaml's refusal of it
            // (a block scalar line that starts with a tab) no longer occurs.
            [
                "SharpYaml, item without aliases: Unicode white space at an edge",
                "SharpYaml, item without aliases: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item without aliases: a keyword in a case no standard lists",
                "SharpYaml, item without aliases: a number with '_' where no standard has it",
                "SharpYaml, item without aliases: an IEEE name in a form no standard lists",
                "SharpYaml, item: Unicode white space at an edge",
                "SharpYaml, item: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item: a keyword in a case no standard lists",
                "SharpYaml, item: a number with '_' where no standard has it",
                "SharpYaml, item: an IEEE name in a form no standard lists",
                "SharpYaml, value: Unicode white space at an edge",
                "SharpYaml, value: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, value: a keyword in a case no standard lists",
                "SharpYaml, value: a number with '_' where no standard has it",
                "SharpYaml, value: an IEEE name in a form no standard lists",
                "YamlDotNet, key: a key with ' *'",
            ],
            [Form.Width] =
            [
                "SharpYaml, item without aliases: Unicode white space at an edge",
                "SharpYaml, item without aliases: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item without aliases: a keyword in a case no standard lists",
                "SharpYaml, item without aliases: a number with '_' where no standard has it",
                "SharpYaml, item without aliases: an IEEE name in a form no standard lists",
                "SharpYaml, item: Unicode white space at an edge",
                "SharpYaml, item: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item: a keyword in a case no standard lists",
                "SharpYaml, item: a number with '_' where no standard has it",
                "SharpYaml, item: an IEEE name in a form no standard lists",
                "SharpYaml, value: Unicode white space at an edge",
                "SharpYaml, value: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, value: a keyword in a case no standard lists",
                "SharpYaml, value: a number with '_' where no standard has it",
                "SharpYaml, value: an IEEE name in a form no standard lists",
                "YamlDotNet, key: a key with ' *'",
            ],
            [Form.Anchored] =
            [
                "SharpYaml, item without aliases: Unicode white space at an edge",
                "SharpYaml, item without aliases: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, item without aliases: a keyword in a case no standard lists",
                "SharpYaml, item without aliases: a number with '_' where no standard has it",
                "SharpYaml, item without aliases: an IEEE name in a form no standard lists",
                "SharpYaml, value: Unicode white space at an edge",
                "SharpYaml, value: a 0o, 0x or 0b number with a sign, an upper-case prefix or '_'",
                "SharpYaml, value: a keyword in a case no standard lists",
                "SharpYaml, value: a number with '_' where no standard has it",
                "SharpYaml, value: an IEEE name in a form no standard lists",
                "YamlDotNet, key: a key with ' *'",
            ],
        };

        [Theory]
        [MemberData(nameof(Forms))]
        [Trait("category", "OuterLoop")]
        public void GeneratedStrings_ReadBackAsStrings_Large(Form form) => Run(form, 200_000, 20261132 + (int)form, lenient: true);

        private static string[] Strings(Random random, Form form, int count) =>
            Enumerable.Range(0, count)
                .Select(_ => form == Form.Width ? Sentence(random) : WriterBlockCrossReadPropertyTests.Generate(random))
                .Where(s => s != "<<")
                .Distinct()
                .ToArray();

        private static void Run(Form form, int count, int seed, bool lenient)
        {
            var random = new Random(seed);
            var failures = new SortedSet<string>(StringComparer.Ordinal);
            const int Batch = 200;
            for (int done = 0; done < count && failures.Count < 40; done += Batch)
            {
                int width = 8 + random.Next(40);
                string[] strings = Strings(random, form, Math.Min(Batch, count - done));
                if (!BatchReadsBack(strings, form, width, lenient))
                {
                    foreach (string s in strings)
                    {
                        foreach (string failure in Check(s, form, width, lenient))
                        {
                            failures.Add(failure);
                        }
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // Several generated strings with single spaces between them, for folding.
        private static string Sentence(Random random) =>
            string.Join(" ", Enumerable.Range(0, random.Next(1, 8)).Select(_ => WriterBlockCrossReadPropertyTests.Generate(random)));

        private static YamlWriterOptions Options(Form form, int width, bool lenient) => new()
        {
            Style = YamlWriterStyle.Block,
            NewLine = "\n",
            MaxLineWidth = form == Form.Width ? width : 0,
            QuoteForLenientReaders = lenient,
        };

        private static void Prefix(Utf8YamlWriter w, Form form, int index)
        {
            switch (form)
            {
                case Form.SingleQuoted:
                    w.WriteScalarStyle(YamlScalarStyle.SingleQuoted);
                    break;
                case Form.Folded:
                    w.WriteScalarStyle(YamlScalarStyle.Folded);
                    break;
                case Form.Anchored:
                    w.WriteAnchor("a" + index.ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void Start(Utf8YamlWriter w, Form form, bool mapping)
        {
            if (form == Form.Flow)
            {
                w.WriteCollectionStyle(YamlCollectionStyle.Flow);
            }

            if (mapping)
            {
                w.WriteStartObject();
            }
            else
            {
                w.WriteStartArray();
            }
        }

        // The strings as items; anchored, each is followed by an alias to it (unless `aliases` is false).
        private static string Items(string[] strings, Form form, int width, bool lenient, bool aliases = true) => Write(w =>
        {
            Start(w, form, mapping: false);
            for (int i = 0; i < strings.Length; i++)
            {
                Prefix(w, form, i);
                w.WriteStringValue(strings[i]);
                if (form == Form.Anchored && aliases)
                {
                    w.WriteAliasValue("a" + i.ToString(CultureInfo.InvariantCulture));
                }
            }

            w.WriteEndArray();
        }, Options(form, width, lenient));

        private static string Values(string[] strings, Form form, int width, bool lenient) => Write(w =>
        {
            Start(w, form, mapping: true);
            for (int i = 0; i < strings.Length; i++)
            {
                w.WritePropertyName("k" + i.ToString(CultureInfo.InvariantCulture));
                Prefix(w, form, i);
                w.WriteStringValue(strings[i]);
            }

            w.WriteEndObject();
        }, Options(form, width, lenient));

        // Keys, in the forms a key has: flow, single-quoted, anchored (the hint and the anchor apply to the key node).
        private static string Keys(string[] strings, Form form, int width, bool lenient) => Write(w =>
        {
            Start(w, form, mapping: true);
            for (int i = 0; i < strings.Length; i++)
            {
                if (form is Form.SingleQuoted or Form.Anchored)
                {
                    Prefix(w, form, i);
                }

                w.WritePropertyName(strings[i]);
                w.WriteNumberValue(i);
            }

            w.WriteEndObject();
        }, Options(form, width, lenient));

        private static string Write(Action<Utf8YamlWriter> calls, YamlWriterOptions options)
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, options))
            {
                calls(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static bool BatchReadsBack(string[] strings, Form form, int width, bool lenient)
        {
            try
            {
                string items = Items(strings, form, width, lenient);
                string itemsWithoutAliases = Items(strings, form, width, lenient, aliases: false);
                string values = Values(strings, form, width, lenient);
                string keys = Keys(strings, form, width, lenient);
                return ItemsMatch(strings, form, s_yamlDotNet.Deserialize<object>(items))
                    && ItemsMatch(strings, form, SY.YamlSerializer.Deserialize<object>(itemsWithoutAliases, s_sharpYaml), aliases: false)
                    && ValuesMatch(strings, s_yamlDotNet.Deserialize<object>(values)) && ValuesMatch(strings, SY.YamlSerializer.Deserialize<object>(values, s_sharpYaml))
                    && KeysMatch(strings, s_yamlDotNet.Deserialize<object>(keys)) && KeysMatch(strings, SY.YamlSerializer.Deserialize<object>(keys, s_sharpYaml));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static IEnumerable<string> Check(string s, Form form, int width, bool lenient)
        {
            string[] one = { s };
            foreach ((string position, Func<string> write, Func<object?, bool> match) in new (string, Func<string>, Func<object?, bool>)[]
            {
                ("item", () => Items(one, form, width, lenient), o => ItemsMatch(one, form, o)),
                ("item without aliases", () => Items(one, form, width, lenient, aliases: false), o => ItemsMatch(one, form, o, aliases: false)),
                ("value", () => Values(one, form, width, lenient), o => ValuesMatch(one, o)),
                ("key", () => Keys(one, form, width, lenient), o => KeysMatch(one, o)),
            })
            {
                string yaml = write();
                foreach ((string library, Func<string, object?> read) in new (string, Func<string, object?>)[]
                {
                    ("YamlDotNet", y => s_yamlDotNet.Deserialize<object>(y)),
                    ("SharpYaml", y => SY.YamlSerializer.Deserialize<object>(y, s_sharpYaml)),
                })
                {
                    if (form == Form.Anchored && library == "SharpYaml" && position == "item")
                    {
                        continue;
                    }

                    string? failure = null;
                    try
                    {
                        object? read1 = read(yaml);
                        if (!match(read1))
                        {
                            failure = $"read back as {Describe(read1)}";
                        }
                    }
                    catch (Exception e)
                    {
                        failure = $"threw {e.GetType().Name}: {e.Message}";
                    }

                    if (failure is not null)
                    {
                        yield return $"{library}, {form}{(form == Form.Width ? " " + width.ToString(CultureInfo.InvariantCulture) : "")}, {position}: {Escape(s)} written {Escape(yaml)} {failure}";
                    }
                }
            }
        }

        private static bool ItemsMatch(string[] strings, Form form, object? read, bool aliases = true)
        {
            int per = form == Form.Anchored && aliases ? 2 : 1;
            return read is IList list && list.Count == strings.Length * per
                && Enumerable.Range(0, list.Count).All(i => list[i] is string t && t == strings[i / per]);
        }

        private static bool ValuesMatch(string[] strings, object? read)
        {
            if (read is not IDictionary map || map.Count != strings.Length)
            {
                return false;
            }

            for (int i = 0; i < strings.Length; i++)
            {
                if (map["k" + i.ToString(CultureInfo.InvariantCulture)] is not string t || t != strings[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool KeysMatch(string[] strings, object? read)
        {
            if (read is not IDictionary map || map.Count != strings.Length)
            {
                return false;
            }

            var keys = new List<object>();
            foreach (DictionaryEntry e in map)
            {
                keys.Add(e.Key);
            }

            return Enumerable.Range(0, strings.Length).All(i => keys[i] is string t && t == strings[i]);
        }

        private static string Describe(object? o) => o switch
        {
            null => "null",
            IList l => "list[" + string.Join(", ", l.Cast<object?>().Select(Describe)) + "]",
            IDictionary d => "map{" + string.Join(", ", d.Cast<DictionaryEntry>().Select(e => Describe(e.Key) + ": " + Describe(e.Value))) + "}",
            string s => "string " + Escape(s),
            _ => o.GetType().Name + " " + Convert.ToString(o, CultureInfo.InvariantCulture),
        };

        private static string Escape(string s) => System.Text.Json.JsonSerializer.Serialize(s);
    }
}
