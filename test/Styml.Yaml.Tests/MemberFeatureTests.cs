// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5f: comments from attributes, per-member scalar styles,
    /// sorted mapping order and omitting empty collections, through reflection, contract customization and a
    /// generated context. The libraries' reads of the same outputs are in Styml.Parity.Tests (MemberFeatureParityTests).
    /// </summary>
    public class MemberFeatureTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_json = new() { WriteStyle = YamlWriterStyle.JsonCompatible };
        private static readonly YamlSerializerOptions s_sorted = new(s_block) { MappingOrder = YamlMappingOrder.Sorted };
        private static readonly YamlSerializerOptions s_omit = new(s_block) { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection };

        // ------------------------------------------------------------------------------------------------------------
        // B4: comments

        public class Commented
        {
            [YamlComment("The name.\nSecond # line")]
            public string? Name { get; set; }

            [YamlComment("A child")]
            public Commented? Child { get; set; }

            [YamlComment("Items")]
            public List<Commented>? Items { get; set; }

            [YamlComment("tab\tinside\r\nCR\rand CRLF\n\nempty above")]
            public int Count { get; set; } = 1;
        }

        public class BadComment
        {
            [YamlComment("a\u0085b: injected")]
            public int X { get; set; }
        }

        public class OmittedCommented
        {
            [YamlComment("never written")]
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public string? Gone { get; set; }

            public int N { get; set; } = 1;
        }

        [Fact]
        public void Comments_Written()
        {
            var value = new Commented { Name = "name", Child = new Commented { Name = "c" }, Items = new() { new Commented() } };
            string yaml = YamlSerializer.Serialize(value, s_block);
            Assert.Equal(
                "# The name.\n# Second # line\nName: name\n# A child\nChild:\n  # The name.\n  # Second # line\n  Name: c\n  # A child\n  Child: null\n  # Items\n  Items: null\n" +
                "  # tab\tinside\n  # CR\n  # and CRLF\n  #\n  # empty above\n  Count: 1\n" +
                "# Items\nItems:\n-\n  # The name.\n  # Second # line\n  Name: null\n  # A child\n  Child: null\n  # Items\n  Items: null\n" +
                "  # tab\tinside\n  # CR\n  # and CRLF\n  #\n  # empty above\n  Count: 1\n" +
                "# tab\tinside\n# CR\n# and CRLF\n#\n# empty above\nCount: 1\n",
                yaml);

            // Reading skips them.
            Commented back = YamlSerializer.Deserialize<Commented>(yaml)!;
            Assert.Equal("name", back.Name);
            Assert.Equal("c", back.Child!.Name);
            Assert.Single(back.Items!);

            // A member left out keeps no comment; the JSON-compatible style drops them.
            Assert.Equal("N: 1\n", YamlSerializer.Serialize(new OmittedCommented(), s_block).Replace("\"N\"", "N"));
            Assert.Equal("{\"Name\":\"name\",\"Child\":null,\"Items\":null,\"Count\":1}", YamlSerializer.Serialize(new Commented { Name = "name" }, s_json));
        }

        [Fact]
        public void Comments_CheckedWhenConfigured()
        {
            // Review 4: NEL, LS, PS and controls are refused before any output (YamlDotNet writes the NEL raw).
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new BadComment(), s_block));
            Assert.Contains("'X'", error.Message);
            foreach (string text in new[] { "a\u2028b", "a\u2029b", "a\u0000b", "a\u0001b", "a\u007Fb", "a\uFEFFb" })
            {
                var options = new YamlSerializerOptions(s_block) { TypeInfoResolver = new DefaultYamlTypeInfoResolver { Modifiers = { Comment(text) } } };
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new OmittedCommented { Gone = "g" }, options));
            }
        }

        [Fact]
        public void Comments_ContractCustomization()
        {
            var options = new YamlSerializerOptions(s_block) { TypeInfoResolver = new DefaultYamlTypeInfoResolver { Modifiers = { Comment("set by a modifier") } } };
            Assert.Equal("# set by a modifier\nGone: g\n\"N\": 1\n", YamlSerializer.Serialize(new OmittedCommented { Gone = "g" }, options));

            YamlTypeInfo info = s_block.GetTypeInfo(typeof(Commented));
            Assert.Equal("A child", info.Properties.Single(p => p.Name == "Child").Comment);
            Assert.Throws<InvalidOperationException>(() => info.Properties[0].Comment = "locked");
        }

        private static Action<YamlTypeInfo> Comment(string text) => info =>
        {
            if (info.Type == typeof(OmittedCommented))
            {
                info.Properties.Single(p => p.Name == "Gone").Comment = text;
            }
        };

        // ------------------------------------------------------------------------------------------------------------
        // B5: member scalar styles

        public class Plain
        {
            public string? Name { get; set; }
        }

        public class Styled
        {
            [YamlScalarStyle(YamlScalarStyle.Literal)] public string LiteralOneLine { get; set; } = "one line";
            [YamlScalarStyle(YamlScalarStyle.Literal)] public string LiteralTwoLines { get; set; } = "a\nb";
            [YamlScalarStyle(YamlScalarStyle.Folded)] public string FoldedTwoLines { get; set; } = "a\nb";
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public string SingleQuote { get; set; } = "it's";
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public string SingleQuotedLineBreak { get; set; } = "a\nb";
            [YamlScalarStyle(YamlScalarStyle.DoubleQuoted)] public string DoubleQuoted { get; set; } = "x";
            [YamlScalarStyle(YamlScalarStyle.Plain)] public string PlainTrue { get; set; } = "true";
            [YamlScalarStyle(YamlScalarStyle.Plain)] public string PlainColon { get; set; } = "a: b";
            [YamlScalarStyle(YamlScalarStyle.DoubleQuoted)] public int DoubleQuotedInt { get; set; } = 1;
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public string? SingleQuotedNull { get; set; }
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public List<string> List { get; set; } = new() { "x" };
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public Plain Child { get; set; } = new() { Name = "c" };
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public char Char { get; set; } = 'c';
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)] public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
            public string After { get; set; } = "plain";
        }

        public class NoneStyle
        {
            [YamlScalarStyle(YamlScalarStyle.None)] public string? X { get; set; }
        }

        [Fact]
        public void Styles_Written()
        {
            var options = new YamlSerializerOptions(s_block) { Converters = { new YamlStringEnumConverter() } };
            string yaml = YamlSerializer.Serialize(new Styled(), options);
            Assert.Equal(
                "LiteralOneLine: one line\nLiteralTwoLines: |-\n  a\n  b\nFoldedTwoLines: >-\n  a\n\n  b\nSingleQuote: 'it''s'\nSingleQuotedLineBreak: 'a\n\n  b'\n" +
                "DoubleQuoted: \"x\"\nPlainTrue: \"true\"\nPlainColon: \"a: b\"\nDoubleQuotedInt: 1\nSingleQuotedNull: null\nList:\n- x\nChild:\n  Name: c\n" +
                "Char: 'c'\nDay: 'Monday'\nAfter: plain\n",
                yaml);

            // Every value reads back as it was (the fallbacks keep the reading).
            Styled back = YamlSerializer.Deserialize<Styled>(yaml, options)!;
            Assert.Equal("true", back.PlainTrue);
            Assert.Equal("a: b", back.PlainColon);
            Assert.Equal(1, back.DoubleQuotedInt);
            Assert.Null(back.SingleQuotedNull);
            Assert.Equal("a\nb", back.SingleQuotedLineBreak);
            Assert.Equal('c', back.Char);
            Assert.Equal(DayOfWeek.Monday, back.Day);

            // Ignored in JSON-compatible style.
            Assert.StartsWith("{\"LiteralOneLine\":\"one line\",\"LiteralTwoLines\":\"a\\nb\"", YamlSerializer.Serialize(new Styled(), s_json));

            // None on the attribute is refused; on a contract it means no style.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new NoneStyle(), s_block));
            YamlPropertyInfo property = new YamlSerializerOptions(s_block).GetTypeInfo(typeof(Plain)).Properties[0];
            Assert.Equal(YamlScalarStyle.None, property.ScalarStyle);
        }

        public sealed class OwnHintConverter : YamlConverter<string>
        {
            public override string Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => reader.GetString()!;

            public override void Write(Utf8YamlWriter writer, string value, YamlSerializerOptions options)
            {
                writer.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                writer.WriteStringValue(value);
            }
        }

        public class WithOwnHint
        {
            [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
            [YamlConverter(typeof(OwnHintConverter))]
            public string Value { get; set; } = "v";

            public string Next { get; set; } = "next";
        }

        [Fact]
        public void Styles_ConverterHintWins_AndNothingLeaks()
        {
            // A converter's own hint replaces the member's; the next key is not affected.
            Assert.Equal("Value: \"v\"\nNext: next\n", YamlSerializer.Serialize(new WithOwnHint(), s_block));

            // Contract customization, and a style on a member holding an object or a number, then a plain member.
            var options = new YamlSerializerOptions(s_block)
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers = { info => { if (info.Type == typeof(Plain)) { info.Properties[0].ScalarStyle = YamlScalarStyle.SingleQuoted; } } },
                },
            };
            Assert.Equal("Name: 'name'\n", YamlSerializer.Serialize(new Plain { Name = "name" }, options));
        }

        // ------------------------------------------------------------------------------------------------------------
        // B6: sorted order

        public class Unsorted
        {
            public int b { get; set; } = 1;
            public int B { get; set; } = 2;
            public int a { get; set; } = 3;
            public int A10 { get; set; } = 4;
            public int A9 { get; set; } = 5;
            public int é { get; set; } = 6;
            public int _x { get; set; } = 7;
            [YamlPropertyOrder(-1)] public int z { get; set; } = 8;
        }

        public class WithExtension
        {
            public int Z { get; set; } = 1;
            public int A { get; set; } = 2;

            [YamlExtensionData]
            public Dictionary<string, object>? Extra { get; set; }
        }

        [YamlPolymorphic(TypeDiscriminatorPropertyName = "zkind")]
        [YamlDerivedType(typeof(Dog), "dog")]
        public class Animal
        {
            public string? Name { get; set; }
        }

        public class Dog : Animal
        {
            public int Bark { get; set; }
        }

        [Fact]
        public void Sorted_Members()
        {
            Assert.Equal("z: 8\nA10: 4\nA9: 5\nB: 2\n_x: 7\na: 3\nb: 1\né: 6\n", YamlSerializer.Serialize(new Unsorted(), s_sorted));
            Assert.Equal("b: 1\nB: 2\na: 3\nA10: 4\nA9: 5\né: 6\n_x: 7\nz: 8\n".Split('\n').Length, YamlSerializer.Serialize(new Unsorted(), s_block).Split('\n').Length);

            // By the written name: the naming policy first.
            var upper = new YamlSerializerOptions(s_sorted) { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseUpper };
            Assert.Equal("NAME: d\nBARK: 1\n".Length, YamlSerializer.Serialize(new Dog { Name = "d", Bark = 1 }, upper).Length);
            Assert.Equal("BARK: 1\nNAME: d\n", YamlSerializer.Serialize(new Dog { Name = "d", Bark = 1 }, upper));

            // The discriminator stays first, even one that sorts late.
            Assert.Equal("zkind: dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<Animal>(new Dog { Name = "d", Bark = 1 }, s_sorted));

            // Reading is unaffected; dictionaries keep their order.
            Assert.Equal(3, YamlSerializer.Deserialize<Unsorted>("b: 9\na: 3\n", s_sorted)!.a);
            Assert.Equal("m: 1\nk: 2\n", YamlSerializer.Serialize(new Dictionary<string, int> { ["m"] = 1, ["k"] = 2 }, s_sorted));
        }

        [Fact]
        public async Task Sorted_ExtensionData()
        {
            var value = new WithExtension { Extra = new() { ["M"] = 3, ["B"] = 4 } };
            Assert.Equal("A: 2\nZ: 1\nB: 4\nM: 3\n", YamlSerializer.Serialize(value, s_sorted));

            // A 1-byte buffer: the write suspends and resumes over the same order.
            var stream = new MemoryStream();
            await YamlSerializer.SerializeAsync(stream, value, new YamlSerializerOptions(s_sorted) { DefaultBufferSize = 1 });
            Assert.Equal("A: 2\nZ: 1\nB: 4\nM: 3\n", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public void Sorted_Preserve()
        {
            // The first occurrence written carries the anchor.
            var shared = new Plain { Name = "s" };
            var options = new YamlSerializerOptions(s_sorted) { ReferenceHandler = ReferenceHandler.Preserve };
            string yaml = YamlSerializer.Serialize(new Pair { Second = shared, First = shared }, options);
            Assert.Matches("^First: &(\\w+)\n  Name: s\nSecond: \\*\\1\n$", yaml);
            Pair back = YamlSerializer.Deserialize<Pair>(yaml, options)!;
            Assert.Same(back.First, back.Second);
        }

        public class Pair
        {
            public Plain? Second { get; set; }
            public Plain? First { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // B7: empty collections

        public class Empties
        {
            public List<int> List { get; set; } = new();
            public int[] Array { get; set; } = System.Array.Empty<int>();
            public Dictionary<string, int> Dict { get; set; } = new();
            public HashSet<int> Set { get; set; } = new();
            public string S { get; set; } = "";
            public List<int>? NullList { get; set; }
            public string? NullString { get; set; }
            public IEnumerable<int> Lazy { get; set; } = Yield();
            public byte[] Bytes { get; set; } = System.Array.Empty<byte>();
            public Plain Empty { get; set; } = new();
            public object ObjEmpty { get; set; } = new List<int>();
            public object ObjBytes { get; set; } = System.Array.Empty<byte>();
            public int N { get; set; }
            public List<int> NonEmpty { get; set; } = new() { 1 };
#if NET
            public System.Collections.Immutable.ImmutableArray<int> DefaultArray { get; set; }
            public System.Collections.Immutable.ImmutableArray<int> EmptyArray { get; set; } = System.Collections.Immutable.ImmutableArray<int>.Empty;
#endif

            private static IEnumerable<int> Yield()
            {
                yield break;
            }
        }

        public class PerMember
        {
            [YamlComment("dropped with the member")]
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
            public List<int> Omitted { get; set; } = new();

            public List<int> Kept { get; set; } = new();

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
            public string? NullString { get; set; }
        }

        public class OnValueType
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
            public int X { get; set; }
        }

        public class ValueOnly
        {
            public int Count { get; set; }
        }

        public class WithInitializer
        {
            public List<int> Items { get; set; } = new() { 7 };
        }

        [Fact]
        public void Empty_Omitted()
        {
            string yaml = YamlSerializer.Serialize(new Empties(), s_omit);
            string expected = "S: \"\"\nLazy: []\nBytes: \"\"\nEmpty:\n  Name: null\nObjBytes: \"\"\n\"N\": 0\nNonEmpty:\n- 1\n";
            Assert.Equal(expected, yaml.Replace("Empty: {}\n", "Empty:\n  Name: null\n"));

            Assert.Equal("Kept: []\n", YamlSerializer.Serialize(new PerMember(), s_block));

            // Review 3: a value type that is not a collection: refused on the member, written under the default.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new OnValueType(), s_block));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new OnValueType(), s_omit));
            Assert.Equal("Count: 0\n", YamlSerializer.Serialize(new ValueOnly(), s_omit));

            // An omitted member keeps its initializer's value on read.
            Assert.Equal("{}\n", YamlSerializer.Serialize(new WithInitializer { Items = new() }, s_omit));
            Assert.Equal(7, YamlSerializer.Deserialize<WithInitializer>("{}\n", s_omit)!.Items.Single());

            // Items and dictionary values are never omitted.
            Assert.Equal("- []\n", YamlSerializer.Serialize(new List<List<int>> { new() }, s_omit));
            Assert.Equal("e: []\n", YamlSerializer.Serialize(new Dictionary<string, List<int>> { ["e"] = new() }, s_omit));
        }

        [Fact]
        public void Empty_Preserve()
        {
            // The anchor pass skips what the write omits: no alias names an anchor that was never written.
            var shared = new List<int>();
            var options = new YamlSerializerOptions(s_omit) { ReferenceHandler = ReferenceHandler.Preserve };
            var value = new SharedEmpty { First = shared, Second = shared, InDictionary = new() { ["d"] = shared } };
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.DoesNotContain("*", yaml);
            Assert.Equal("InDictionary:\n  d: []\n", yaml);
            Assert.NotNull(YamlSerializer.Deserialize<SharedEmpty>(yaml, options));
        }

        public class SharedEmpty
        {
            public List<int>? First { get; set; }
            public List<int>? Second { get; set; }
            public Dictionary<string, List<int>>? InDictionary { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Source generation: the generated metadata carries B4 to B7, and its output equals reflection's.

        [Fact]
        public void Generated_EqualsReflection()
        {
            var context = new MemberFeatureContext(new YamlSerializerOptions(s_block));
            Assert.Equal(YamlSerializer.Serialize(new Commented { Name = "n" }, s_block), YamlSerializer.Serialize(new Commented { Name = "n" }, context.Commented));
            Assert.Equal(YamlSerializer.Serialize(new Styled(), s_block), YamlSerializer.Serialize(new Styled(), context.Styled));
            Assert.Equal(YamlSerializer.Serialize(new PerMember(), s_block), YamlSerializer.Serialize(new PerMember(), context.PerMember));
            Assert.Equal("A child", context.Commented.Properties.Single(p => p.Name == "Child").Comment);
            Assert.Equal(YamlScalarStyle.Literal, context.Styled.Properties.Single(p => p.Name == "LiteralOneLine").ScalarStyle);

            // The attribute's options: sorted, empty collections omitted, and the YAML writer options.
            var sorted = SortedContext.Default;
            Assert.Equal(YamlMappingOrder.Sorted, sorted.Options.MappingOrder);
            Assert.Equal(YamlIgnoreCondition.WhenWritingEmptyCollection, sorted.Options.DefaultIgnoreCondition);
            Assert.Equal(YamlScalarStyle.DoubleQuoted, sorted.Options.MultiLineStringStyle);
            Assert.Equal(YamlScalarStyle.SingleQuoted, sorted.Options.DefaultScalarStyle);
            Assert.True(sorted.Options.IgnoreSourceStyles);
            Assert.Equal(YamlSchemaKind.Yaml11, sorted.Options.Schema);
            Assert.Equal(YamlDuplicateKeyHandling.FirstWins, sorted.Options.DuplicateKeyHandling);
            Assert.True(sorted.Options.IndentSequences && sorted.Options.ExpandSequenceItems && sorted.Options.QuoteForLenientReaders);
            Assert.True(sorted.Options.ResolveMergeKeys && sorted.Options.AcceptLenientScalars && sorted.Options.DisallowImplicitCollectionKeys);
            Assert.Equal(40, sorted.Options.MaxLineWidth);
            Assert.Equal(7, sorted.Options.MaxAliasExpansion);
            Assert.Equal(YamlWriterStyle.Block, sorted.Options.WriteStyle);
            var reflection = new YamlSerializerOptions(sorted.Options) { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            Assert.Equal(YamlSerializer.Serialize(new Unsorted(), reflection), YamlSerializer.Serialize(new Unsorted(), sorted.Unsorted));
            Assert.StartsWith("z: 8\nA10: 4\n", YamlSerializer.Serialize(new Unsorted(), sorted.Unsorted));
            Assert.Equal(YamlSerializer.Serialize(new Empties(), reflection), YamlSerializer.Serialize(new Empties(), sorted.Empties));
        }
    }

    [YamlSerializable(typeof(MemberFeatureTests.Commented))]
    [YamlSerializable(typeof(MemberFeatureTests.Styled))]
    [YamlSerializable(typeof(MemberFeatureTests.PerMember))]
    internal partial class MemberFeatureContext : YamlSerializerContext;

    [YamlSourceGenerationOptions(
        MappingOrder = YamlMappingOrder.Sorted,
        DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection,
        WriteStyle = YamlWriterStyle.Block,
        NewLine = "\n",
        IndentSequences = true,
        ExpandSequenceItems = true,
        QuoteForLenientReaders = true,
        MultiLineStringStyle = YamlScalarStyle.DoubleQuoted,
        DefaultScalarStyle = YamlScalarStyle.SingleQuoted,
        IgnoreSourceStyles = true,
        MaxLineWidth = 40,
        Schema = YamlSchemaKind.Yaml11,
        AllowDuplicateProperties = true,
        DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins,
        MaxAliasExpansion = 7,
        ResolveMergeKeys = true,
        AcceptLenientScalars = true,
        DisallowImplicitCollectionKeys = true)]
    [YamlSerializable(typeof(MemberFeatureTests.Unsorted))]
    [YamlSerializable(typeof(MemberFeatureTests.Empties))]
    internal partial class SortedContext : YamlSerializerContext;
}
