// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using StjReader = System.Text.Json.Utf8JsonReader;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Exit criterion 2: for valid JSON taken from the imported test data, the YAML grammar and the real
    /// System.Text.Json Utf8JsonReader agree on token type, ValueSpan bytes, ValueIsEscaped, TokenStartIndex,
    /// BytesConsumed and CurrentDepth at every token.
    /// </summary>
    public class JsonEquivalenceTests
    {
        public static IEnumerable<object[]> CorpusItems() => JsonCorpus.Items.Select((_, i) => new object[] { i });

        [Theory]
        [MemberData(nameof(CorpusItems))]
        public void MatchesUtf8JsonReader(int index)
        {
            JsonCorpus.Item item = JsonCorpus.Items[index];
            string? difference = Compare(item.Utf8);
            Assert.True(difference is null, $"{item.Source}: {difference}");
        }

        [Fact]
        public void CorpusIsExtractedFromTheImportedTests()
        {
            // eng/test/json-corpus/extract-json-corpus.cs; regenerate it after changing the imported tests.
            Assert.True(JsonCorpus.Items.Count > 1000);
            Assert.All(JsonCorpus.Items, item => Assert.StartsWith("test/", item.Source));
        }

        internal static string? Compare(byte[] json)
        {
            var expected = new StjReader(json);
            var actual = new Utf8YamlReader(json);
            for (int i = 0; ; i++)
            {
                bool expectedRead = expected.Read();
                bool actualRead = actual.Read();
                if (expectedRead != actualRead)
                {
                    return $"token {i}: Utf8JsonReader.Read() = {expectedRead}, Utf8YamlReader.Read() = {actualRead}";
                }
                if (!expectedRead)
                {
                    return null;
                }

                string at = $"token {i} ({expected.TokenType} at {expected.TokenStartIndex})";
                if ((int)expected.TokenType != (int)actual.TokenType)
                {
                    return $"{at}: TokenType {actual.TokenType}";
                }
                if (!expected.ValueSpan.SequenceEqual(actual.ValueSpan))
                {
                    return $"{at}: ValueSpan differs";
                }
                if (expected.ValueIsEscaped != actual.ValueIsEscaped)
                {
                    return $"{at}: ValueIsEscaped {actual.ValueIsEscaped}";
                }
                if (expected.TokenStartIndex != actual.TokenStartIndex)
                {
                    return $"{at}: TokenStartIndex {actual.TokenStartIndex}";
                }
                if (expected.BytesConsumed != actual.BytesConsumed)
                {
                    return $"{at}: BytesConsumed {actual.BytesConsumed} instead of {expected.BytesConsumed}";
                }
                if (expected.CurrentDepth != actual.CurrentDepth)
                {
                    return $"{at}: CurrentDepth {actual.CurrentDepth}";
                }
            }
        }
    }

    internal static class JsonCorpus
    {
        internal sealed class Item
        {
            public string Source = "";
            public byte[] Utf8 = Array.Empty<byte>();
        }

        private static List<Item>? s_items;

        internal static List<Item> Items => s_items ??= Load();

        private static List<Item> Load()
        {
            byte[] file = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "JsonCorpus", "corpus.json"));
            using var document = System.Text.Json.JsonDocument.Parse(file);
            var items = new List<Item>();
            foreach (System.Text.Json.JsonElement element in document.RootElement.EnumerateArray())
            {
                items.Add(new Item
                {
                    Source = element.GetProperty("source").GetString()!,
                    Utf8 = Encoding.UTF8.GetBytes(element.GetProperty("json").GetString()!),
                });
            }
            return items;
        }
    }
}
