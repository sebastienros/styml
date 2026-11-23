// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Nodes;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Alias keys ('*k : v'), accepted since M3b under the feature parity rule (docs/feature-parity.md).
    /// The key is a PropertyName whose ValueSpan is the anchor name and whose KeyKind is Alias; until M5 every
    /// consumer that needs the key's text fails instead of using the anchor name.
    /// </summary>
    public class AliasKeyTests
    {
        private static List<string> Tokens(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
            var tokens = new List<string>();
            while (reader.Read())
            {
                string text = reader.TokenType switch
                {
                    YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    YamlTokenType.Alias => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                    YamlTokenType.PropertyName or YamlTokenType.String or YamlTokenType.Number => reader.GetString()!,
                    _ => "",
                };
                bool aliasKey = reader.KeyKind == YamlKeyKind.Alias;
                Assert.Equal(reader.TokenType != YamlTokenType.PropertyName ? YamlKeyKind.None : aliasKey ? YamlKeyKind.Alias : YamlKeyKind.Scalar, reader.KeyKind);
                tokens.Add($"{reader.TokenType}{(aliasKey ? " alias" : "")} {text}".TrimEnd());
            }
            return tokens;
        }

        public static IEnumerable<object[]> AliasKeyDocuments() => new[]
        {
            // Block mapping, as the second key.
            new object[] { "&a a: 1\n*a : 2\n", new[] { "StartObject", "PropertyName a", "Number 1", "PropertyName alias a", "Number 2", "EndObject" } },
            // As the first key of a mapping: at the root, in a sequence entry, as a nested value.
            new object[] { "*a : 1\n", new[] { "StartObject", "PropertyName alias a", "Number 1", "EndObject" } },
            new object[] { "- *a : 1\n  b: 2\n", new[] { "StartArray", "StartObject", "PropertyName alias a", "Number 1", "PropertyName b", "Number 2", "EndObject", "EndArray" } },
            new object[] { "x:\n  *a : 1\n", new[] { "StartObject", "PropertyName x", "StartObject", "PropertyName alias a", "Number 1", "EndObject", "EndObject" } },
            // Properties on their own line belong to the mapping.
            new object[] { "&m\n*a : 1\n", new[] { "StartObject", "PropertyName alias a", "Number 1", "EndObject" } },
            // Alias value and alias key together (yaml-test-suite E76Z).
            new object[] { "*a : *b\n", new[] { "StartObject", "PropertyName alias a", "Alias b", "EndObject" } },
            // Explicit key.
            new object[] { "? *a\n: 1\n", new[] { "StartObject", "PropertyName alias a", "Number 1", "EndObject" } },
            // Flow mapping, with and without a value, and a flow pair in a sequence.
            new object[] { "{*a : 1, b: 2}", new[] { "StartObject", "PropertyName alias a", "Number 1", "PropertyName b", "Number 2", "EndObject" } },
            new object[] { "{*a , b: 2}", new[] { "StartObject", "PropertyName alias a", "Null", "PropertyName b", "Number 2", "EndObject" } },
            new object[] { "[*a : 1, *b]", new[] { "StartArray", "StartObject", "PropertyName alias a", "Number 1", "EndObject", "Alias b", "EndArray" } },
            // The name runs to white space or a flow indicator, so it may contain ':'.
            new object[] { "*a:b : 1\n", new[] { "StartObject", "PropertyName alias a:b", "Number 1", "EndObject" } },
        };

        [Theory]
        [MemberData(nameof(AliasKeyDocuments))]
        public void ReaderAcceptsAliasKeys(string yaml, string[] expected)
        {
            Assert.Equal(expected, Tokens(yaml));
        }

        [Theory]
        [MemberData(nameof(AliasKeyDocuments))]
        public void ReaderAcceptsAliasKeys_EverySplit(string yaml, string[] expected)
        {
            _ = expected;
            ChunkingTests.AssertEverySplit(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
        }

        [Theory]
        [InlineData("&x *a : 1\n")]
        [InlineData("a: 1\n&x *a : 2\n")]
        [InlineData("{&x *a : 1}")]
        [InlineData("? &x *a\n: 1\n")]
        [InlineData("a: *b : c\n")]
        public void InvalidAliasKeysAreRejected(string yaml)
        {
            Assert.ThrowsAny<YamlException>(() => Tokens(yaml));
        }

        [Fact]
        public void AliasKeyTokenShape()
        {
            var reader = new Utf8YamlReader("{*key : 1}"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
            Assert.Equal(YamlKeyKind.Alias, reader.KeyKind);
            Assert.Equal("key", Encoding.UTF8.GetString(reader.ValueSpan.ToArray()));
            Assert.True(reader.ValueIsEscaped);
            Assert.Equal(YamlScalarStyle.None, reader.ScalarStyle);
            Assert.Equal(1, reader.TokenStartIndex);
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.None, reader.KeyKind);
        }

        [Theory]
        [InlineData("GetString")]
        [InlineData("CopyStringBytes")]
        [InlineData("CopyStringChars")]
        [InlineData("ValueTextEqualsBytes")]
        [InlineData("ValueTextEqualsString")]
        public void AliasKeyHasNoText(string accessor)
        {
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ReadAliasKey(accessor));
            Assert.Contains("KeyKind", e.Message);
        }

        private static void ReadAliasKey(string accessor)
        {
            var reader = new Utf8YamlReader("*key : 1\n"u8);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlKeyKind.Alias, reader.KeyKind);
            switch (accessor)
            {
                case "GetString": reader.GetString(); break;
                case "CopyStringBytes": reader.CopyString(new byte[16]); break;
                case "CopyStringChars": reader.CopyString(new char[16]); break;
                case "ValueTextEqualsBytes": reader.ValueTextEquals("key"u8); break;
                case "ValueTextEqualsString": reader.ValueTextEquals("key"); break;
            }
        }

        public class Target
        {
            public int Key { get; set; }
            public int Other { get; set; }
        }

        public record TargetRecord(int Key, int Other);

        [Fact]
        public void SerializerReadsAliasKeys()
        {
            // M5b: an alias key is its anchored scalar's text. Never the anchor name: 'Key' is not set from '*Key'.
            // The alias key repeats its anchored key, so it is a duplicate (M5c, decision 1): Error by default, and the
            // last value under LastWins.
            const string Yaml = "&Key Other: 1\n*Key : 2\n";
            var lastWins = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Target>(Yaml));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, int>>("&1 1: 1\n*1 : 2\n"));
            Target target = YamlSerializer.Deserialize<Target>(Yaml, lastWins)!;
            Assert.Equal((0, 2), (target.Key, target.Other));
            Assert.Equal(new TargetRecord(0, 2), YamlSerializer.Deserialize<TargetRecord>(Yaml, lastWins));
            Assert.Equal(new Dictionary<string, int> { ["Other"] = 2 }, YamlSerializer.Deserialize<Dictionary<string, int>>(Yaml, lastWins));
            Assert.Equal(new Dictionary<int, int> { [1] = 2 }, YamlSerializer.Deserialize<Dictionary<int, int>>("&1 1: 1\n*1 : 2\n", lastWins));

            // An alias to no anchor is an error of the input, also on a member the type does not have.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("{*k : 1}"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Target>("Other: 1\n*unknown : 2\n"));
        }

        [Fact]
        public void SerializerReadsAliasKeys_ObjectMembers()
        {
            // M5b: an object member is a YamlElement, whose document reads the alias key (M5a). The alias key repeats
            // 'a', a duplicate under the default Error (M5c); LastWins keeps both entries in the element.
            var lastWins = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>("{&k a: 0, *k : 1}"));
            YamlElement element = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("{&k a: 0, *k : 1}", lastWins));
            Assert.Equal(new[] { "a", "a" }, element.EnumerateObject().Select(p => p.Name));
            Assert.Equal(1, element.GetProperty("a").GetInt32());
            var holder = YamlSerializer.Deserialize<Dictionary<string, object>>("{\"a\": {&k a: 0, *k : 1}}", lastWins)!;
            Assert.Equal(new[] { "a", "a" }, Assert.IsType<YamlElement>(holder["a"]).EnumerateObject().Select(p => p.Name));
        }

        [Theory]
        [InlineData("{&k a: 0, *k : 1}")]
        [InlineData("[&x 1, *x]")]
        public void SerializerIntoNodeTypesReadsAliases(string yaml)
        {
            // M5b: the DOM types read aliases as YamlDocument does, on every node path (M5c: the default Error and an
            // explicit handling build nodes from an element too).
            var lastWins = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            if (yaml.StartsWith('{'))
            {
                // The alias key repeats 'a': System.Text.Json's ArgumentException under Error; the last value under LastWins.
                Assert.Throws<ArgumentException>(() => YamlSerializer.Deserialize<YamlNode>(yaml));
                Assert.Equal(1, (int)YamlSerializer.Deserialize<YamlNode>(yaml, lastWins)!["a"]!);
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<YamlElement>(yaml));
            }
            else
            {
                Assert.Equal(1, (int)YamlSerializer.Deserialize<YamlNode>(yaml)![1]!);
                Assert.Equal(1, (int)YamlSerializer.Deserialize<YamlNode>(yaml, lastWins)![1]!);
            }

            Assert.Equal(yaml, YamlSerializer.Deserialize<YamlElement>(yaml, lastWins).GetRawText());
            using YamlDocument document = YamlSerializer.Deserialize<YamlDocument>(yaml, lastWins)!;
            Assert.Equal(yaml, document.RootElement.GetRawText());
        }

        [Fact]
        public void DocumentAndNodeReadAliasKeys()
        {
            // Since M5a an alias key is the anchored scalar's text; an alias to no anchor is an error of the input.
            using (YamlDocument document = YamlDocument.Parse("{x: &k a, *k : 1}"))
            {
                Assert.Equal(new[] { "x", "a" }, document.RootElement.EnumerateObject().Select(p => p.Name));
            }

            Assert.Equal(1, (int)YamlNode.Parse("{x: &k a, *k : 1}")!["a"]!);
            Assert.Throws<YamlException>(() => YamlDocument.Parse("{*k : 1}"));
            Assert.Throws<YamlException>(() => YamlNode.Parse("[{*k : 1}]"));

            // An alias to a collection is a collection key (M5e): its key element is the anchored node.
            using (YamlDocument collectionKey = YamlDocument.Parse("{a: &c [1], *c : 2}"))
            {
                YamlProperty alias = collectionKey.RootElement.EnumerateObject().Last();
                Assert.Equal(YamlKeyKind.Alias, alias.KeyKind);
                Assert.Equal(1, alias.GetKeyElement()[0].GetInt32());
            }
        }
    }
}
