// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;
using YDCore = YamlDotNet.Core;
using YDEvents = YamlDotNet.Core.Events;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// The rows of docs/feature-parity.md, asserted for YamlDotNet 18.1.0, SharpYaml 3.17.0 and Styml, default
    /// options unless the row says otherwise. When a library is bumped or Styml gains a feature, the failing test names
    /// the row to update. Rows of the second table assert Styml's current "not yet" behaviour, so they fail when M3d
    /// (or M5) implements the feature.
    /// </summary>
    public class FeatureParityTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();
        private static readonly SY.YamlSerializerOptions s_sharpYamlDefault = new();
        private static readonly SY.YamlSerializerOptions s_sharpYamlPreserve = new() { ReferenceHandling = SY.YamlReferenceHandling.Preserve };

        public class Item { public string? Name { get; set; } }
        public class MappingHolder { public Item? A { get; set; } public Item? B { get; set; } }
        public class SequenceHolder { public List<int>? A { get; set; } public List<int>? B { get; set; } }
        public class StringHolder { public string? A { get; set; } public string? B { get; set; } }
        public class ObjectHolder { public object? A { get; set; } public object? B { get; set; } }
        public class BoolHolder { public bool A { get; set; } }

        private static List<(YamlTokenType Type, YamlKeyKind Key, string Raw)> StymlTokens(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
            var tokens = new List<(YamlTokenType, YamlKeyKind, string)>();
            while (reader.Read())
            {
                tokens.Add((reader.TokenType, reader.KeyKind, Encoding.UTF8.GetString(reader.ValueSpan.ToArray())));
            }
            return tokens;
        }

        // ---------------------------------------------------------------------------------------------------------
        // First table: the M3a calls.

        private const string AliasKey = "&k a: 1\n*k : 2\n";

        [Fact]
        public void AliasKey_YamlDotNet_Accepts() =>
            Assert.Equal("2", s_yamlDotNet.Deserialize<Dictionary<string, string>>(AliasKey)["a"]);

        [Fact]
        public void AliasKey_SharpYaml_Rejects() =>
            Assert.Contains("Expected a scalar key token", Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<string, string>>(AliasKey, s_sharpYamlDefault)).Message);

        [Fact]
        public void AliasKey_Styml_Accepts()
        {
            // M5b: the reader reports the alias key (M3b) and the serializer reads it as its anchored scalar, as YamlDotNet.
            Assert.Contains((YamlTokenType.PropertyName, YamlKeyKind.Alias, "k"), StymlTokens(AliasKey));
            // The alias key repeats its anchored key: a duplicate under the default Error (M5c); LastWins reads it as
            // YamlDotNet does.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, string>>(AliasKey));
            Assert.Equal("2", YamlSerializer.Deserialize<Dictionary<string, string>>(AliasKey, new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins })!["a"]);
        }

        private const string AliasToMapping = "A: &x {Name: n}\nB: *x\n";
        private const string AliasToSequence = "A: &x [1, 2]\nB: *x\n";

        [Fact]
        public void AliasToCollection_YamlDotNet_SameInstance()
        {
            MappingHolder m = s_yamlDotNet.Deserialize<MappingHolder>(AliasToMapping);
            Assert.Same(m.A, m.B);
            SequenceHolder s = s_yamlDotNet.Deserialize<SequenceHolder>(AliasToSequence);
            Assert.Same(s.A, s.B);
        }

        [Fact]
        public void AliasToCollection_SharpYaml_OptInPreserve()
        {
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<MappingHolder>(AliasToMapping, s_sharpYamlDefault));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<SequenceHolder>(AliasToSequence, s_sharpYamlDefault));
            MappingHolder m = SY.YamlSerializer.Deserialize<MappingHolder>(AliasToMapping, s_sharpYamlPreserve)!;
            Assert.Same(m.A, m.B);
            SequenceHolder s = SY.YamlSerializer.Deserialize<SequenceHolder>(AliasToSequence, s_sharpYamlPreserve)!;
            Assert.Same(s.A, s.B);
        }

        [Fact]
        public void AliasToCollection_Styml_SameInstance()
        {
            // M5b: by default, as YamlDotNet (SharpYaml needs Preserve).
            MappingHolder m = YamlSerializer.Deserialize<MappingHolder>(AliasToMapping)!;
            Assert.Same(m.A, m.B);
            SequenceHolder s = YamlSerializer.Deserialize<SequenceHolder>(AliasToSequence)!;
            Assert.Same(s.A, s.B);
        }

        private const string AliasToScalar = "A: &x hello\nB: *x\n";

        [Fact]
        public void AliasToScalar_YamlDotNet_Accepts() =>
            Assert.Equal("hello", s_yamlDotNet.Deserialize<StringHolder>(AliasToScalar).B);

        [Fact]
        public void AliasToScalar_SharpYaml_RejectsEvenWithPreserve()
        {
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<StringHolder>(AliasToScalar, s_sharpYamlDefault));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<StringHolder>(AliasToScalar, s_sharpYamlPreserve));
        }

        [Fact]
        public void AliasToScalar_Styml_Accepts() =>
            Assert.Equal("hello", YamlSerializer.Deserialize<StringHolder>(AliasToScalar)!.B);

        private const string AliasInObjectMember = "A: &x {k: v}\nB: *x\n";
        private const string AliasInUntypedDocument = "a: &x {k: v}\nb: *x\n";

        [Fact]
        public void AliasUntyped_YamlDotNet_Accepts()
        {
            Assert.NotNull(s_yamlDotNet.Deserialize<ObjectHolder>(AliasInObjectMember).B);
            Assert.NotNull(s_yamlDotNet.Deserialize<Dictionary<string, object>>(AliasInUntypedDocument)["b"]);
        }

        [Fact]
        public void AliasUntyped_SharpYaml_OptInPreserve()
        {
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<ObjectHolder>(AliasInObjectMember, s_sharpYamlDefault));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<object>(AliasInUntypedDocument, s_sharpYamlDefault));
            Assert.NotNull(SY.YamlSerializer.Deserialize<ObjectHolder>(AliasInObjectMember, s_sharpYamlPreserve)!.B);
            Assert.NotNull(SY.YamlSerializer.Deserialize<object>(AliasInUntypedDocument, s_sharpYamlPreserve));
        }

        [Fact]
        public void AliasUntyped_Styml_Accepts()
        {
            // M5b: an object member is a YamlElement (System.Text.Json's model); an alias gives the anchored node's.
            Assert.Equal("v", Assert.IsType<YamlElement>(YamlSerializer.Deserialize<ObjectHolder>(AliasInObjectMember)!.B).GetProperty("k").GetString());
            YamlElement document = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(AliasInUntypedDocument));
            Assert.Equal("v", document.GetProperty("b").GetProperty("k").GetString());
        }

        [Theory]
        [InlineData("A: 12\n", "12")]
        [InlineData("{\"A\": 12}", "12")]
        [InlineData("A: true\n", "true")]
        public void PlainScalarIntoString_AllThree(string yaml, string expected)
        {
            Assert.Equal(expected, s_yamlDotNet.Deserialize<StringHolder>(yaml).A);
            Assert.Equal(expected, SY.YamlSerializer.Deserialize<StringHolder>(yaml, s_sharpYamlDefault)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<StringHolder>(yaml)!.A);
        }

        [Fact]
        public void EmptyInput_YamlDotNetAndSharpYaml_ReturnNull()
        {
            Assert.Null(s_yamlDotNet.Deserialize<StringHolder>(""));
            Assert.Null(SY.YamlSerializer.Deserialize<StringHolder>("", s_sharpYamlDefault));
        }

        [Fact]
        public void EmptyInput_Styml_NoTokensAndSerializerReturnsNull()
        {
            // D4: the reader yields no token; decision 2 (M5c): the serializer reads zero tokens as a null, as both
            // libraries do.
            Assert.Empty(StymlTokens(""));
            Assert.Null(YamlSerializer.Deserialize<StringHolder>(""));
        }

        [Fact]
        public void DuplicateKeys_LibrariesAndStyml()
        {
            // M5c, decision 1: YamlDotNet keeps the last value; SharpYaml and Styml throw by default, and both have
            // LastWins and FirstWins.
            Assert.Equal("y", s_yamlDotNet.Deserialize<StringHolder>("A: x\nA: y").A);
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<StringHolder>("A: x\nA: y", s_sharpYamlDefault));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<StringHolder>("A: x\nA: y"));
            foreach ((string mode, string expected) in new[] { ("LastWins", "y"), ("FirstWins", "x") })
            {
                var sharpYaml = new SY.YamlSerializerOptions();
                System.Reflection.PropertyInfo handling = typeof(SY.YamlSerializerOptions).GetProperty("DuplicateKeyHandling")!;
                handling.SetValue(sharpYaml, Enum.Parse(handling.PropertyType, mode));
                Assert.Equal(expected, SY.YamlSerializer.Deserialize<StringHolder>("A: x\nA: y", sharpYaml)!.A);
                var styml = new YamlSerializerOptions { DuplicateKeyHandling = Enum.Parse<YamlDuplicateKeyHandling>(mode) };
                Assert.Equal(expected, YamlSerializer.Deserialize<StringHolder>("A: x\nA: y", styml)!.A);
            }
        }

        [Fact]
        public void Schemas_JsonAndFailsafe_SharpYamlAndStyml()
        {
            // M5c: both read Json's forms and Failsafe's strings alike; under Json an unmatched plain scalar is an error
            // in Styml (YAML 1.2.2, 10.2.2) and a string in SharpYaml.
            var syJson = new SY.YamlSerializerOptions { Schema = SY.YamlSchemaKind.Json, UseSchema = true };
            var syFailsafe = new SY.YamlSerializerOptions { Schema = SY.YamlSchemaKind.Failsafe, UseSchema = true };
            var json = new YamlSerializerOptions { Schema = YamlSchemaKind.Json };
            var failsafe = new YamlSerializerOptions { Schema = YamlSchemaKind.Failsafe };
            Assert.Equal(1, SY.YamlSerializer.Deserialize<IntHolder>("A: 1", syJson)!.A);
            Assert.Equal(1, YamlSerializer.Deserialize<IntHolder>("A: 1", json)!.A);
            Assert.Equal("foo", SY.YamlSerializer.Deserialize<StringHolder>("A: foo", syJson)!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<StringHolder>("A: foo", json));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<IntHolder>("A: 1", syFailsafe));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<IntHolder>("A: 1", failsafe));
            Assert.Equal("~", SY.YamlSerializer.Deserialize<StringHolder>("A: ~", syFailsafe)!.A);
            Assert.Equal("~", YamlSerializer.Deserialize<StringHolder>("A: ~", failsafe)!.A);

            // With AcceptLenientScalars the JSON schema reads an unmatched plain scalar as a string, as SharpYaml does, and more
            // (True into a bool, which SharpYaml's Json schema refuses).
            var lenientJson = new YamlSerializerOptions { Schema = YamlSchemaKind.Json, AcceptLenientScalars = true };
            foreach (string scalar in new[] { "foo", "~", "yes", "0x1F" })
            {
                Assert.Equal(scalar, SY.YamlSerializer.Deserialize<StringHolder>($"A: {scalar}", syJson)!.A);
                Assert.Equal(scalar, YamlSerializer.Deserialize<StringHolder>($"A: {scalar}", lenientJson)!.A);
            }

            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<BoolHolder>("A: True", syJson));
            Assert.True(YamlSerializer.Deserialize<BoolHolder>("A: True", lenientJson)!.A);
        }

        public class IntHolder { public int A { get; set; } }

        // Newline-separated top-level values (JSON Lines). Neither library reads them as several values: scalars fold
        // into one multi-line plain scalar, and a second collection after the first is an error. Several values are
        // '---'-separated documents in all three (decision D3).
        private const string JsonLinesScalars = "1\n2\n3\n";
        public static IEnumerable<object[]> JsonLinesCollections() =>
        [
            ["{\"a\": 1}\n{\"a\": 2}\n"],
            ["[1]\n[2]\n"],
            ["\"x\"\n\"y\"\n"],
        ];

        [Fact]
        public void JsonLinesScalars_AllThree_FoldIntoOneScalar()
        {
            Assert.Equal("1 2 3", s_yamlDotNet.Deserialize<string>(JsonLinesScalars));
            Assert.Equal("1 2 3", SY.YamlSerializer.Deserialize<string>(JsonLinesScalars, s_sharpYamlDefault));
            Assert.Equal("1 2 3", YamlSerializer.Deserialize<string>(JsonLinesScalars));
        }

        [Theory]
        [MemberData(nameof(JsonLinesCollections))]
        public void JsonLinesCollections_YamlDotNetAndSharpYaml_Reject(string yaml)
        {
            Assert.ThrowsAny<Exception>(() => s_yamlDotNet.Deserialize<object>(yaml));
            Assert.ThrowsAny<Exception>(() => YamlDotNetDocumentCount(yaml));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<object>(yaml, s_sharpYamlDefault));
            Assert.ThrowsAny<Exception>(() => SharpYamlDocumentCount(yaml));
        }

        [Theory]
        [MemberData(nameof(JsonLinesCollections))]
        public void JsonLinesCollections_Styml_Rejects(string yaml)
        {
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<object>(yaml));
            Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
                while (reader.Read()) { }
            });
            Assert.ThrowsAny<YamlException>(() => DrainTopLevelValues(yaml));
        }

        [Fact]
        public void DocumentSeparatedValues_AllThree_ReadSeveralValues()
        {
            const string yaml = "{\"a\": 1}\n---\n{\"a\": 2}\n";
            Assert.Equal(2, YamlDotNetDocumentCount(yaml));
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
            int starts = 0;
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.StartObject) starts++;
            }
            Assert.Equal(2, starts);
            Assert.Equal(2, DrainTopLevelValues(yaml));
            Assert.Equal(2, SharpYamlDocumentCount(yaml));
        }

        private static int YamlDotNetDocumentCount(string yaml)
        {
            var parser = new YDCore.Parser(new StringReader(yaml));
            int documents = 0;
            while (parser.MoveNext())
            {
                if (parser.Current is YDEvents.DocumentStart) documents++;
            }
            return documents;
        }

        private static int SharpYamlDocumentCount(string yaml)
        {
            using var reader = new StringReader(yaml);
            return SY.Model.YamlStream.Load(reader, null!).Count;
        }

        private static int DrainTopLevelValues(string yaml)
        {
            int count = 0;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            var e = YamlSerializer.DeserializeAsyncEnumerable<object>(stream, topLevelValues: true).GetAsyncEnumerator();
            try
            {
                while (e.MoveNextAsync().AsTask().GetAwaiter().GetResult()) count++;
            }
            finally
            {
                e.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            return count;
        }

        private const string TabIndentation = "a:\n\tb: 1\n";

        [Fact]
        public void TabIndentation_AllThreeReject()
        {
            Assert.ThrowsAny<Exception>(() => s_yamlDotNet.Deserialize<Dictionary<string, object>>(TabIndentation));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<string, object>>(TabIndentation, s_sharpYamlDefault));
            Assert.ThrowsAny<YamlException>(() => StymlTokens(TabIndentation));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Second table: the D6 cuts, superseded by the rule (scheduled for M3d and M5).

        private const string MultiLineExplicitKey = "? first\n  second\n: v\n";

        [Fact]
        public void MultiLineExplicitKey_YamlDotNetAndSharpYaml_Accept()
        {
            Assert.Equal("v", s_yamlDotNet.Deserialize<Dictionary<string, string>>(MultiLineExplicitKey)["first second"]);
            Assert.Equal("v", SY.YamlSerializer.Deserialize<Dictionary<string, string>>(MultiLineExplicitKey, s_sharpYamlDefault)!["first second"]);
        }

        [Fact]
        public void MultiLineExplicitKey_Styml_Accepts()
        {
            Assert.Contains((YamlTokenType.PropertyName, YamlKeyKind.Scalar, "first\n  second"), StymlTokens(MultiLineExplicitKey));
            Assert.Equal("v", YamlSerializer.Deserialize<Dictionary<string, string>>(MultiLineExplicitKey)!["first second"]);
        }

        private const string CollectionKey = "? [a, b]\n: v\n";

        [Fact]
        public void CollectionKey_YamlDotNet_Accepts()
        {
            Dictionary<List<string>, string> d = s_yamlDotNet.Deserialize<Dictionary<List<string>, string>>(CollectionKey);
            Assert.Equal(new[] { "a", "b" }, d.Keys.Single());
            Assert.NotNull(s_yamlDotNet.Deserialize<object>(CollectionKey));
        }

        [Fact]
        public void CollectionKey_SharpYaml_Rejects()
        {
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<List<string>, string>>(CollectionKey, s_sharpYamlDefault));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<object>(CollectionKey, s_sharpYamlDefault));
        }

        [Fact]
        public void CollectionKey_Styml_ReaderAcceptsSerializerFailsUntilM5()
        {
            Assert.Contains((YamlTokenType.PropertyName, YamlKeyKind.Complex, "[a, b]"), StymlTokens(CollectionKey));
            Assert.Contains((YamlTokenType.PropertyName, YamlKeyKind.Complex, "[a, b]"), StymlTokens("[a, b]: v\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, string>>(CollectionKey));
        }

        // Implicit collection keys: one line, at most 1024 characters (libyaml's simple keys); explicit keys have no
        // limit. Measured at the parser level, which is where both libraries accept collection keys.
        public static IEnumerable<object[]> ImplicitKeyLimits() =>
        [
            ["[" + new string('a', 1022) + "]: v\n", true],
            ["[" + new string('a', 1023) + "]: v\n", false],
            ["[a,\n b]: v\n", false],
            ["[[a,\n b]: c]\n", false],
            ["? [" + new string('a', 5000) + "]\n: v\n", true],
            ["- [a]:b\n", false],
        ];

        [Theory]
        [MemberData(nameof(ImplicitKeyLimits))]
        public void ImplicitCollectionKeyLimits_AllThree(string yaml, bool accepted)
        {
            if (accepted)
            {
                Assert.True(YamlDotNetEventCount(yaml) > 0);
                Assert.True(SharpYamlEventCount(yaml) > 0);
                Assert.Contains(StymlTokens(yaml), t => t.Key == YamlKeyKind.Complex);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => YamlDotNetEventCount(yaml));
                Assert.ThrowsAny<Exception>(() => SharpYamlEventCount(yaml));
                Assert.ThrowsAny<YamlException>(() => StymlTokens(yaml));
            }
        }

        // Quotes inside plain scalars in collection keys (M3d review round 4): a quote after a blank is part of the plain
        // scalar, not the start of a quoted one. Both parsers accept these; Styml reads one collection key each.
        [Theory]
        [InlineData("[a \"b]: c\n")]
        [InlineData("[a 'b]: c\n")]
        [InlineData("[it's, a 'b]: c\n")]
        [InlineData("{a \"b: c}: d\n")]
        [InlineData("[[a \"b], [c \"d]: e]\n")]
        [InlineData("[[a \"b], [\"c\"]: e]\n")]
        public void QuotesInPlainScalarsOfCollectionKeys_AllThreeAccept(string yaml)
        {
            Assert.True(YamlDotNetEventCount(yaml) > 0);
            Assert.True(SharpYamlEventCount(yaml) > 0);
            Assert.Single(StymlTokens(yaml), t => t.Key == YamlKeyKind.Complex);
        }

        [Fact]
        public void QuoteInPlainScalar_NeighbourStaysASequence()
        {
            // '[[a "b], ["c"]: e]': the first entry is the sequence [a "b], the second a pair whose key is ["c"].
            var tokens = StymlTokens("[[a \"b], [\"c\"]: e]");
            Assert.Equal(YamlTokenType.StartArray, tokens[1].Type);
            Assert.Equal((YamlTokenType.String, YamlKeyKind.None, "a \"b"), tokens[2]);
            Assert.Equal((YamlTokenType.PropertyName, YamlKeyKind.Complex, "[\"c\"]"), tokens[5]);
        }

        [Fact]
        public void RealMultiLineKey_ReportsTheSingleLineRule()
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() => StymlTokens("[a,\n b]: v\n"));
            Assert.Contains("single line", e.Message);
            Assert.ThrowsAny<Exception>(() => YamlDotNetEventCount("[a,\n b]: v\n"));
        }

        [Fact]
        public void MultiLineCollectionKeyInFlowMapping_StymlAcceptsAsTheSpecAllows()
        {
            // YAML 1.2 reads flow mapping keys in flow-in context, so they may span lines; both libraries' parsers
            // refuse it (a stale simple key). Accepting more than both is outside the rule; recorded in the table.
            const string Yaml = "{[a,\n b]: c}\n";
            Assert.ThrowsAny<Exception>(() => YamlDotNetEventCount(Yaml));
            Assert.ThrowsAny<Exception>(() => SharpYamlEventCount(Yaml));
            Assert.Contains(StymlTokens(Yaml), t => t.Key == YamlKeyKind.Complex);
        }

        private static int YamlDotNetEventCount(string yaml)
        {
            var parser = new YDCore.Parser(new StringReader(yaml));
            int events = 0;
            while (parser.MoveNext())
            {
                events++;
            }
            return events;
        }

        private static int SharpYamlEventCount(string yaml)
        {
            SY.IParser parser = SY.Parser.CreateParser(new StringReader(yaml));
            int events = 0;
            while (parser.MoveNext())
            {
                events++;
            }
            return events;
        }

        [Theory]
        [InlineData("A: yes\n", true)]
        [InlineData("A: no\n", false)]
        public void Yaml11Boolean_YamlDotNet_Accepts(string yaml, bool expected) =>
            Assert.Equal(expected, s_yamlDotNet.Deserialize<BoolHolder>(yaml).A);

        [Theory]
        [InlineData("A: yes\n", true)]
        [InlineData("A: no\n", false)]
        public void Yaml11Boolean_SharpYaml_OptInExtendedSchema(string yaml, bool expected)
        {
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<BoolHolder>(yaml, s_sharpYamlDefault));
            // Schema = Extended alone is not enough: UseSchema must be set too.
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<BoolHolder>(yaml, new SY.YamlSerializerOptions { Schema = SY.YamlSchemaKind.Extended }));
            Assert.Equal(expected, SY.YamlSerializer.Deserialize<BoolHolder>(yaml, new SY.YamlSerializerOptions { Schema = SY.YamlSchemaKind.Extended, UseSchema = true })!.A);
        }

        [Theory]
        [InlineData("A: yes\n", true)]
        [InlineData("A: no\n", false)]
        public void Yaml11Boolean_Styml_OptInSchema(string yaml, bool expected)
        {
            Assert.Equal(YamlTokenType.String, StymlTokens(yaml)[2].Type);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<BoolHolder>(yaml));
            Assert.Equal(expected, StymlYaml11<BoolHolder>(yaml).A);
        }

        // YAML 1.1 forms, one row each (feature-parity.md, "YAML 1.1 forms"): the value each library reads into a
        // typed member, or null when it refuses. YamlDotNet with default options; SharpYaml with default options and
        // with Schema = Extended + UseSchema = true; Styml with YamlSchemaKind.Yaml11 on the reader.
        private static readonly SY.YamlSerializerOptions s_sharpYamlExtended = new() { Schema = SY.YamlSchemaKind.Extended, UseSchema = true };

        public class LongHolder { public long A { get; set; } }
        public class DoubleHolder { public double A { get; set; } }
        public class NullableIntHolder { public int? A { get; set; } }

        private static T StymlYaml11<T>(string yaml, bool lenient = false)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = lenient });
            return YamlSerializer.Deserialize<T>(ref reader)!;
        }

        private static object? Try(Func<object?> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return null;
            }
        }

        [Theory]
        // AcceptLenientScalars is the last column: yEs and tRue only with it.
        //           value          YamlDotNet  SharpYaml   SharpYaml Extended  Styml Yaml11  Styml Yaml11 + AcceptLenientScalars
        [InlineData("y", true, null, true, true, true)]
        [InlineData("Y", true, null, true, true, true)]
        [InlineData("YES", true, null, true, true, true)]
        [InlineData("On", true, null, true, true, true)]
        [InlineData("OFF", false, null, false, false, false)]
        [InlineData("n", false, null, false, false, false)]
        [InlineData("yEs", true, null, null, null, true)]
        [InlineData("tRue", true, true, null, null, true)]
        public void Yaml11Forms_Boolean(string value, bool? yamlDotNet, bool? sharpYaml, bool? sharpYamlExtended, bool? styml, bool? stymlLenient)
        {
            string yaml = "A: " + value + "\n";
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<BoolHolder>(yaml).A));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<BoolHolder>(yaml, s_sharpYamlDefault)!.A));
            Assert.Equal(sharpYamlExtended, Try(() => SY.YamlSerializer.Deserialize<BoolHolder>(yaml, s_sharpYamlExtended)!.A));
            Assert.Equal(styml, Try(() => StymlYaml11<BoolHolder>(yaml).A));
            Assert.Equal(stymlLenient, Try(() => StymlYaml11<BoolHolder>(yaml, lenient: true).A));
        }

        [Theory]
        [InlineData("0777", 511L, 777L, 511L, 511L)]
        [InlineData("010", 8L, 10L, 8L, 8L)]
        [InlineData("0o17", null, 15L, 15L, 15L)]
        [InlineData("0b1010", 10L, 10L, 10L, 10L)]
        [InlineData("-0b11", -3L, -3L, -3L, -3L)]
        [InlineData("0b1_0", 2L, 2L, 2L, 2L)]
        [InlineData("0x_1F", 31L, 31L, 31L, 31L)]
        [InlineData("1_000", 1000L, 1000L, 1000L, 1000L)]
        [InlineData("190:20:30", 685230L, null, null, 685230L)]
        [InlineData("-1:30", -90L, null, null, -90L)]
        [InlineData("1_", 1L, 1L, 1L, 1L)]
        [InlineData("1__0", 10L, 10L, 10L, 10L)]
        [InlineData("08", null, 8L, null, 8L)]
        public void Yaml11Forms_Integer(string value, long? yamlDotNet, long? sharpYaml, long? sharpYamlExtended, long? styml)
        {
            string yaml = "A: " + value + "\n";
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<LongHolder>(yaml).A));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<LongHolder>(yaml, s_sharpYamlDefault)!.A));
            Assert.Equal(sharpYamlExtended, Try(() => SY.YamlSerializer.Deserialize<LongHolder>(yaml, s_sharpYamlExtended)!.A));
            Assert.Equal(styml, Try(() => StymlYaml11<LongHolder>(yaml).A));
        }

        [Theory]
        // A leading zero: octal in YAML 1.1 (YamlDotNet, SharpYaml's Extended schema), decimal in the core schema
        // (SharpYaml's default). 08 and 09 are not octal: YamlDotNet and SharpYaml Extended refuse them as integers,
        // Styml reads them as decimal under both schemas. Styml's element, node and object reads give its typed value.
        //           value    YamlDotNet  SharpYaml  SharpYaml Extended  Styml Core  Styml Yaml11
        [InlineData("012", 10L, 12L, 10L, 12L, 10L)]
        [InlineData("-012", -10L, -12L, -10L, -12L, -10L)]
        [InlineData("+012", 10L, 12L, 10L, 12L, 10L)]
        [InlineData("0_12", 10L, 12L, 10L, null, 10L)]
        [InlineData("00", 0L, 0L, 0L, 0L, 0L)]
        [InlineData("08", null, 8L, null, 8L, 8L)]
        [InlineData("09", null, 9L, null, 9L, 9L)]
        [InlineData("0o14", null, 12L, 12L, 12L, 12L)]
        public void LeadingZeroIntegers(string value, long? yamlDotNet, long? sharpYaml, long? sharpYamlExtended, long? stymlCore, long? stymlYaml11)
        {
            string yaml = "A: " + value + "\n";
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<LongHolder>(yaml).A));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<LongHolder>(yaml, s_sharpYamlDefault)!.A));
            Assert.Equal(sharpYamlExtended, Try(() => SY.YamlSerializer.Deserialize<LongHolder>(yaml, s_sharpYamlExtended)!.A));
            foreach ((YamlSchemaKind schema, long? expected) in new[] { (YamlSchemaKind.Core, stymlCore), (YamlSchemaKind.Yaml11, stymlYaml11) })
            {
                var options = new YamlSerializerOptions { Schema = schema };
                Assert.Equal(expected, Try(() => YamlSerializer.Deserialize<LongHolder>(yaml, options)!.A));
                using YamlDocument document = YamlDocument.Parse(yaml, new YamlDocumentOptions { Schema = schema });
                Assert.Equal(expected, Try(() => document.RootElement.GetProperty("A").GetInt64()));
                Assert.Equal(expected, Try(() => YamlNode.Parse(yaml, documentOptions: new YamlDocumentOptions { Schema = schema })!["A"]!.GetValue<long>()));
                Assert.Equal(expected, Try(() => ((YamlElement)YamlSerializer.Deserialize<Dictionary<string, object>>(yaml, options)!["A"]).GetInt64()));
            }
        }

        [Theory]
        [InlineData("1_000.5", 1000.5, 1000.5, 1000.5, 1000.5)]
        [InlineData("0777.5", 777.5, 777.5, null, 777.5)]
        [InlineData("190:20:30.15", null, null, null, null)]
        [InlineData("1:30.5", null, null, null, null)]
        public void Yaml11Forms_Float(string value, double? yamlDotNet, double? sharpYaml, double? sharpYamlExtended, double? styml)
        {
            string yaml = "A: " + value + "\n";
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<DoubleHolder>(yaml).A));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<DoubleHolder>(yaml, s_sharpYamlDefault)!.A));
            Assert.Equal(sharpYamlExtended, Try(() => SY.YamlSerializer.Deserialize<DoubleHolder>(yaml, s_sharpYamlExtended)!.A));
            Assert.Equal(styml, Try(() => StymlYaml11<DoubleHolder>(yaml).A));
        }

        [Fact]
        public void Yaml11Forms_Null()
        {
            // '~' is already core; 'NuLL' is null only in SharpYaml's default schema (case-insensitive).
            Assert.Null(s_yamlDotNet.Deserialize<NullableIntHolder>("A: ~\n").A);
            Assert.Null(SY.YamlSerializer.Deserialize<NullableIntHolder>("A: ~\n", s_sharpYamlDefault)!.A);
            Assert.Null(StymlYaml11<NullableIntHolder>("A: ~\n").A);
            Assert.Null(YamlSerializer.Deserialize<NullableIntHolder>("A: ~\n")!.A);
            Assert.Equal("NuLL", s_yamlDotNet.Deserialize<StringHolder>("A: NuLL\n").A);
            Assert.Null(SY.YamlSerializer.Deserialize<StringHolder>("A: NuLL\n", s_sharpYamlDefault)!.A);
            Assert.Equal("NuLL", StymlYaml11<StringHolder>("A: NuLL\n").A);
        }

        [Theory]
        [InlineData("tRue", true)]
        [InlineData("FaLsE", false)]
        public void CaseInsensitiveBoolean_AllThreeInTypedReads(string value, bool expected)
        {
            // Case-insensitive booleans in typed reads, behind AcceptLenientScalars (non-standard
            // behaviour is opt-in): typed boolean reads accept true/false in any case,
            // as both libraries do with default options; the token stays a String under the core schema.
            string yaml = "A: " + value + "\n";
            Assert.Equal(expected, s_yamlDotNet.Deserialize<BoolHolder>(yaml).A);
            Assert.Equal(expected, SY.YamlSerializer.Deserialize<BoolHolder>(yaml, s_sharpYamlDefault)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<BoolHolder>(yaml, new YamlSerializerOptions { AcceptLenientScalars = true })!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<BoolHolder>(yaml));
            Assert.Equal(YamlTokenType.String, StymlTokens(yaml)[2].Type);
        }

        [Fact]
        public void OneLibraryOnly_BehindAcceptLenientScalars()
        {
            // 'yEs' is a boolean in YamlDotNet only, 'NuLL' a null in SharpYaml only. By the parity rule (the feature
            // exists if either library has it) Styml reads both with AcceptLenientScalars ('yEs' with the YAML 1.1 schema
            // too) and refuses both by default (the full matrix is LenientScalarsParityTests).
            Assert.True(s_yamlDotNet.Deserialize<BoolHolder>("A: yEs\n").A);
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<BoolHolder>("A: yEs\n", s_sharpYamlDefault));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<BoolHolder>("A: yEs\n"));
            Assert.True(StymlYaml11<BoolHolder>("A: yEs\n", lenient: true).A);
            Assert.Equal("NuLL", s_yamlDotNet.Deserialize<StringHolder>("A: NuLL\n").A);
            Assert.Null(SY.YamlSerializer.Deserialize<StringHolder>("A: NuLL\n", s_sharpYamlDefault)!.A);
            Assert.Equal("NuLL", YamlSerializer.Deserialize<StringHolder>("A: NuLL\n")!.A);
            Assert.Null(YamlSerializer.Deserialize<StringHolder>("A: NuLL\n", new YamlSerializerOptions { AcceptLenientScalars = true })!.A);
        }

        [Theory]
        [InlineData("A: 0b1010\n")]
        [InlineData("A: 1_000\n")]
        public void DefaultSchemaGap_BothAcceptStymlCoreRefuses(string yaml)
        {
            // Found while measuring (M3d): both libraries accept these with default
            // options; Styml's default is the exact 1.2 core schema and accepts them only with Yaml11.
            Assert.NotNull(Try(() => s_yamlDotNet.Deserialize<LongHolder>(yaml).A));
            Assert.NotNull(Try(() => SY.YamlSerializer.Deserialize<LongHolder>(yaml, s_sharpYamlDefault)!.A));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<LongHolder>(yaml));
            Assert.NotNull(Try(() => StymlYaml11<LongHolder>(yaml).A));
        }

        private const string MergeAliased = "base: &b {x: 1}\nderived:\n  <<: *b\n  y: 2\n";
        private const string MergeInline = "derived:\n  <<: {x: 1}\n  y: 2\n";

        [Fact]
        public void MergeKey_YamlDotNet_OptInMergingParser()
        {
            Assert.ThrowsAny<Exception>(() => s_yamlDotNet.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeAliased));
            var merged = s_yamlDotNet.Deserialize<Dictionary<string, Dictionary<string, int>>>(
                new YDCore.MergingParser(new YDCore.Parser(new StringReader(MergeAliased))));
            Assert.Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 }, merged["derived"]);
        }

        [Fact]
        public void MergeKey_SharpYaml_ByDefault_AliasNeedsPreserve()
        {
            Assert.Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 },
                SY.YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeInline, s_sharpYamlDefault)!["derived"]);
            // '<<: *b' is an alias, which SharpYaml only accepts with ReferenceHandling.Preserve.
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeAliased, s_sharpYamlDefault));
            Assert.Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 },
                SY.YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeAliased, s_sharpYamlPreserve)!["derived"]);
        }

        [Fact]
        public void MergeKey_Styml_OptIn()
        {
            // M5b: resolved with ResolveMergeKeys, as YamlDotNet's MergingParser.
            var on = new YamlSerializerOptions { ResolveMergeKeys = true };
            Assert.Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 }, YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeAliased, on)!["derived"]);
            Assert.Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 }, YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeInline, on)!["derived"]);

            // Off by default: '<<' is an ordinary key, so the mapping does not fit an int.
            Assert.Contains((YamlTokenType.PropertyName, YamlKeyKind.Scalar, "<<"), StymlTokens(MergeInline));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeInline));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(MergeAliased));
        }

        private const string TagDirective = "%TAG !e! tag:example.com,2000:\n--- !e!foo x\n";

        [Fact]
        public void TagHandle_YamlDotNet_ParserExpands_DeserializerRejectsUnknownTag()
        {
            Assert.Equal("tag:example.com,2000:foo", FirstScalarTag(new YDCore.Parser(new StringReader(TagDirective))));
            Assert.Contains("unresolved tag", Assert.ThrowsAny<Exception>(() => s_yamlDotNet.Deserialize<string>(TagDirective)).Message);
        }

        [Fact]
        public void TagHandle_SharpYaml_ParserExpands_SerializerIgnoresTag()
        {
            SY.IParser parser = SY.Parser.CreateParser(new StringReader(TagDirective));
            string? tag = null;
            while (parser.MoveNext())
            {
                if (parser.Current is SY.Events.Scalar scalar)
                {
                    tag = scalar.Tag;
                    break;
                }
            }
            Assert.Equal("tag:example.com,2000:foo", tag);
            Assert.Equal("x", SY.YamlSerializer.Deserialize<string>(TagDirective, s_sharpYamlDefault));
        }

        [Fact]
        public void TagHandle_Styml_Expands()
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(TagDirective));
            Assert.True(reader.Read());
            Assert.Equal("!e!foo", Encoding.UTF8.GetString(reader.Tag.ToArray()));
            Assert.Equal("tag:example.com,2000:foo", reader.GetExpandedTag());
            Assert.Equal("x", YamlSerializer.Deserialize<string>(TagDirective));
        }

        private static string? FirstScalarTag(YDCore.IParser parser)
        {
            while (parser.MoveNext())
            {
                if (parser.Current is YDEvents.Scalar scalar)
                {
                    return scalar.Tag.Value;
                }
            }
            return null;
        }
    }
}
