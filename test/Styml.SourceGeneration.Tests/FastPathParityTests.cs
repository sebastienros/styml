// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// Styml M5f: not imported. The generator's fast path (the generated
// SerializeHandler, called here directly so that the test cannot pass with the fast path off) writes the same bytes as the
// reflection path with the same options, in block and JSON-compatible styles: for the imported fast-path contexts' types
// with RealWorldContextTests' samples, for names with non-ASCII and escaped characters, and for a sorted context.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public class FastPathParityTests
    {
        public static IEnumerable<object[]> Styles() => new[] { new object[] { YamlWriterStyle.Block }, new object[] { YamlWriterStyle.JsonCompatible } };

        private static T Sample<T>(string factory) =>
            (T)typeof(RealWorldContextTests).GetMethod(factory, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;

        private static void AssertSameBytes<T>(YamlSerializerContext context, T value)
        {
            var typeInfo = (YamlTypeInfo<T>)context.GetTypeInfo(typeof(T))!;
            Assert.NotNull(typeInfo.SerializeHandler);

            YamlSerializerOptions options = context.Options;
            var buffer = new MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = options.WriteStyle, NewLine = options.NewLine, Indented = options.WriteIndented }))
            {
                typeInfo.SerializeHandler!(writer, value);
            }

            var reflection = new YamlSerializerOptions(options) { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            string expected = YamlSerializer.Serialize(value, reflection);
            Assert.Equal(expected, Encoding.UTF8.GetString(buffer.ToArray()));

            // And through the serializer, which takes the fast path for this context.
            Assert.Equal(expected, YamlSerializer.Serialize(value, typeInfo));
        }

        private static YamlSerializerOptions Options(YamlWriterStyle style) => new() { WriteStyle = style, NewLine = "\n" };

        [Theory]
        [MemberData(nameof(Styles))]
        public void ImportedContexts_FastPathEqualsReflection(YamlWriterStyle style)
        {
            foreach (YamlSerializerContext context in new YamlSerializerContext[] { new SerializationContext(Options(style)) })
            {
                AssertSameBytes(context, Sample<Location>("CreateLocation"));
                AssertSameBytes(context, Sample<NumberTypes>("CreateNumberTypes"));
                AssertSameBytes(context, Sample<ActiveOrUpcomingEvent>("CreateActiveOrUpcomingEvent"));
                AssertSameBytes(context, Sample<CampaignSummaryViewModel>("CreateCampaignSummaryViewModel"));
                AssertSameBytes(context, Sample<IndexViewModel>("CreateIndexViewModel"));
                AssertSameBytes(context, Sample<WeatherForecastWithPOCOs>("CreateWeatherForecastWithPOCOs"));
                AssertSameBytes(context, Sample<EmptyPoco>("CreateEmptyPoco"));
            }
        }

        [Theory]
        [MemberData(nameof(Styles))]
        public void Names_FastPathEqualsReflection(YamlWriterStyle style)
        {
            var context = new FastPathNamesContext(Options(style));
            AssertSameBytes(context, new FastPathNames());
            if (style == YamlWriterStyle.Block)
            {
                Assert.Equal("é: 1\nnaïve: 2\n日本: 3\na&b: 4\nit's: 5\n<x>: 6\na+b: 7\n\"yes\": 8\nmulti: |-\n  a\n  b\nnumbers:\n  2: x\n", YamlSerializer.Serialize(new FastPathNames(), context.FastPathNames));
            }
        }

        [Theory]
        [MemberData(nameof(Styles))]
        public void Sorted_FastPathEqualsReflection(YamlWriterStyle style)
        {
            // The generated options carry the attribute's order; run-time options keep the fast path when they agree.
            Assert.Equal(YamlMappingOrder.Sorted, FastPathSortedContext.Default.Options.MappingOrder);
            var context = new FastPathSortedContext(new YamlSerializerOptions(Options(style)) { MappingOrder = YamlMappingOrder.Sorted });
            AssertSameBytes(context, new FastPathUnsorted());
            if (style == YamlWriterStyle.Block)
            {
                Assert.Equal("z: 6\nA10: 4\nA9: 5\nB: 2\na: 3\nb: 1\n", YamlSerializer.Serialize(new FastPathUnsorted(), context.FastPathUnsorted));
            }
        }

        // M7a, and its audit: every serializer option
        // Styml added that can change what is written is either honoured through the writer (the generated handler writes
        // through a Utf8YamlWriter built from the options, so the fast path stays and writes what reflection writes), or
        // a term of IsCompatibleWithOptions (the handler's own code would differ, so the context takes the metadata path).
        // The read-only options change nothing written and keep the fast path. A context generated with default options,
        // serialized with run-time options that set one property, must write reflection's bytes, on the path its group
        // gives. M7a's report has the table.
        public static IEnumerable<object[]> RunTimeOptions() => s_runTimeOptions.Keys.Select(name => new object[] { name });

        private static readonly Dictionary<string, (Action<YamlSerializerOptions> Set, bool FastPath)> s_runTimeOptions = new()
        {
            // Honoured through the writer (YamlSerializerOptions.GetWriterOptions).
            ["WriteStyle"] = (o => o.WriteStyle = YamlWriterStyle.JsonCompatible, true),
            ["IndentSequences"] = (o => o.IndentSequences = true, true),
            ["ExpandSequenceItems"] = (o => o.ExpandSequenceItems = true, true),
            ["MaxLineWidth"] = (o => o.MaxLineWidth = 24, true),
            ["MultiLineStringStyle"] = (o => o.MultiLineStringStyle = YamlScalarStyle.DoubleQuoted, true),
            ["QuoteForLenientReaders"] = (o => o.QuoteForLenientReaders = true, true),
            // M7c: the JSON schema quotes string values (M5f's follow-up).
            ["Schema.Json"] = (o => o.Schema = YamlSchemaKind.Json, true),
            // M7c: a DOM's styles only, so the typed sample is unchanged and FastPathNodeSample is exercised below.
            ["IgnoreSourceStyles"] = (o => o.IgnoreSourceStyles = true, true),
            // Read only: nothing written depends on them.
            ["AcceptLenientScalars"] = (o => o.AcceptLenientScalars = true, true),
            ["DisallowImplicitCollectionKeys"] = (o => o.DisallowImplicitCollectionKeys = true, true),
            ["DuplicateKeyHandling"] = (o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, true),
            ["MaxAliasExpansion"] = (o => o.MaxAliasExpansion = 5, true),
            ["ResolveMergeKeys"] = (o => o.ResolveMergeKeys = true, true),
            ["Schema"] = (o => o.Schema = YamlSchemaKind.Yaml11, true),
            // Terms of IsCompatibleWithOptions.
            ["MappingOrder"] = (o => o.MappingOrder = YamlMappingOrder.Sorted, false),
            ["TagMappings"] = (o => o.TagMappings.Add("!dog", typeof(FastPathDog)), false),
            ["DefaultIgnoreCondition.WhenWritingEmptyCollection"] = (o => o.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection, false),
            ["ReferenceHandler.PreserveAll"] = (o => o.ReferenceHandler = ReferenceHandler.PreserveAll, false),
            // M7b (fix A): it styles a dictionary's keys, which the generated dictionary handler cannot reach.
            ["DefaultScalarStyle"] = (o => o.DefaultScalarStyle = YamlScalarStyle.SingleQuoted, false),
        };

        private static readonly PropertyInfo s_canUseSerializeHandler =
            typeof(YamlTypeInfo).GetProperty("CanUseSerializeHandler", BindingFlags.NonPublic | BindingFlags.Instance)!;

        [Theory]
        [MemberData(nameof(RunTimeOptions))]
        public void RunTimeOptions_AgainstADefaultContext_WriteReflectionsBytes(string option)
        {
            (Action<YamlSerializerOptions> set, bool fastPath) = s_runTimeOptions[option];
            var options = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
            set(options);

            var context = new FastPathOptionsContext(options);
            YamlTypeInfo<FastPathOptionsSample> typeInfo = context.FastPathOptionsSample;
            Assert.NotNull(typeInfo.SerializeHandler);
            var value = new FastPathOptionsSample();

            string actual = YamlSerializer.Serialize(value, typeInfo);
            string expected = YamlSerializer.Serialize(value, new YamlSerializerOptions(options) { TypeInfoResolver = new DefaultYamlTypeInfoResolver() });
            Assert.Equal(expected, actual);
            Assert.Equal(fastPath, (bool)s_canUseSerializeHandler.GetValue(typeInfo)!);

            // The sample exercises the option: a writer option or a term changes the output, a read-only one does not.
            string plain = YamlSerializer.Serialize(value, new FastPathOptionsContext(new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" }).FastPathOptionsSample);
            bool readOnly = option is "AcceptLenientScalars" or "DisallowImplicitCollectionKeys" or "DuplicateKeyHandling" or "MaxAliasExpansion" or "ResolveMergeKeys" or "Schema"
                or "IgnoreSourceStyles";
            if (readOnly)
            {
                Assert.Equal(plain, actual);
            }
            else
            {
                Assert.NotEqual(plain, actual);
            }

            if (option == "Schema.Json")
            {
                // The output reads back under the same schema.
                Assert.Contains("Lenient: \"yes\"\n", actual);
                Assert.Contains("When: \"2020-01-02T00:00:00\"\n", actual);
                Assert.NotNull(YamlSerializer.Deserialize<FastPathOptionsSample>(actual, options));
            }

            if (option == "IgnoreSourceStyles")
            {
                // M7c: a node member, written through the writer the options build (the metadata path, as any node
                // member), loses its source's flow style and quotes, and equals reflection's.
                var node = new FastPathNodeSample { Node = Nodes.YamlNode.Parse("{\"a\": [1, \"b\"]}") };
                string nodeYaml = YamlSerializer.Serialize(node, context.FastPathNodeSample);
                Assert.Equal(YamlSerializer.Serialize(node, new YamlSerializerOptions(options) { TypeInfoResolver = new DefaultYamlTypeInfoResolver() }), nodeYaml);
                Assert.Equal("Node:\n  a:\n  - 1\n  - b\n", nodeYaml);
                var keep = new FastPathOptionsContext(new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" });
                Assert.Equal("Node: {\"a\": [1, \"b\"]}\n", YamlSerializer.Serialize(node, keep.FastPathNodeSample));
            }

            if (option == "DefaultScalarStyle")
            {
                // M7b (fix A): the string values, a typed text value and a dictionary's keys take it; member names do not.
                Assert.Contains("Lenient: 'yes'\n", actual);
                Assert.Contains("When: '2020-01-02T00:00:00'\n", actual);
                Assert.Contains("  'zeta': 1\n", actual);
                Assert.Contains("\nZeta: 26\n", actual);
            }
        }

        // The options whose effect is per type or per member cannot reach a fast-path type: a polymorphic type (any
        // discriminator style) and a member with [YamlComment], [YamlScalarStyle] or WhenWritingEmptyCollection get no
        // generated handler (TypeGenerationSpec.IsFastPathSupported; M5fMemberAttributes_HaveNoFastPath for the members).
        [Fact]
        public void PolymorphicType_HasNoFastPath()
        {
            Assert.Null(FastPathOptionsContext.Default.FastPathPolymorphic.SerializeHandler);
            Assert.NotNull(FastPathOptionsContext.Default.FastPathOptionsSample.SerializeHandler);
        }

        [Fact]
        public void M5fMemberAttributes_HaveNoFastPath()
        {
            // 5.6, C9: a type with [YamlComment], [YamlScalarStyle] or WhenWritingEmptyCollection is written by the metadata.
            // In a context that generates metadata (the default mode); under GenerationMode.Serialization such a type has
            // neither, as any type the fast path does not support (a custom converter, an object member, polymorphism).
            var context = new FastPathMetadataContext(Options(YamlWriterStyle.Block));
            Assert.Null(((YamlTypeInfo<FastPathCommented>)context.GetTypeInfo(typeof(FastPathCommented))!).SerializeHandler);
            var reflection = new YamlSerializerOptions(context.Options) { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            Assert.Equal(YamlSerializer.Serialize(new FastPathCommented(), reflection), YamlSerializer.Serialize(new FastPathCommented(), context.FastPathCommented));
            Assert.StartsWith("# the value\n", YamlSerializer.Serialize(new FastPathCommented(), context.FastPathCommented));
        }
    }

    public class FastPathNames
    {
        [YamlPropertyName("é")] public int E { get; set; } = 1;
        [YamlPropertyName("naïve")] public int Naive { get; set; } = 2;
        [YamlPropertyName("日本")] public int Japan { get; set; } = 3;
        [YamlPropertyName("a&b")] public int Amp { get; set; } = 4;
        [YamlPropertyName("it's")] public int Quote { get; set; } = 5;
        [YamlPropertyName("<x>")] public int Angle { get; set; } = 6;
        [YamlPropertyName("a+b")] public int Plus { get; set; } = 7;
        [YamlPropertyName("yes")] public int Yes { get; set; } = 8;
        [YamlPropertyName("multi")] public string Multi { get; set; } = "a\nb";
        [YamlPropertyName("numbers")] public Dictionary<int, string> Numbers { get; set; } = new() { [2] = "x" };
    }

    public class FastPathCommented
    {
        [YamlComment("the value")]
        [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
        public string Value { get; set; } = "v";

        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
        public List<int> Empty { get; set; } = new();
    }

    public class FastPathUnsorted
    {
        public int b { get; set; } = 1;
        public int B { get; set; } = 2;
        public int a { get; set; } = 3;
        public int A10 { get; set; } = 4;
        public int A9 { get; set; } = 5;
        [YamlPropertyOrder(-1)] public int z { get; set; } = 6;
    }

    [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization)]
    [YamlSerializable(typeof(FastPathNames))]
    internal partial class FastPathNamesContext : YamlSerializerContext;

    [YamlSerializable(typeof(FastPathCommented))]
    internal partial class FastPathMetadataContext : YamlSerializerContext;

    [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization, MappingOrder = YamlMappingOrder.Sorted)]
    [YamlSerializable(typeof(FastPathUnsorted))]
    internal partial class FastPathSortedContext : YamlSerializerContext;

    // M7a's options audit: one sample that each option above can change.
    public class FastPathOptionsSample
    {
        public string Text { get; set; } = "line one\nline two\n";
        public string Lenient { get; set; } = "yes";
        public string Psych { get; set; } = "1,000"; // quoted only under QuoteForLenientReaders (Psych reads it as a number)
        public string Long { get; set; } = "a long line of words that a narrow width folds into several lines";
        public List<int> Numbers { get; set; } = new() { 1, 2, 3 };
        public List<FastPathOptionsItem> Items { get; set; } = new() { new() { Name = "a", Size = 1 }, new() { Name = "b", Size = 2 } };
        public Dictionary<string, int> Unsorted { get; set; } = new() { ["zeta"] = 1, ["alpha"] = 2 };
        public FastPathAnimal? Pet { get; set; } = new FastPathDog { Name = "d", Bark = 1 };
        public List<int> Empty { get; set; } = new();
        public string? Missing { get; set; }
        public int Zeta { get; set; } = 26;
        public int Alpha { get; set; } = 1;
        public DateTime When { get; set; } = new DateTime(2020, 1, 2); // M7b (fix A): a typed text value, for DefaultScalarStyle
    }

    public class FastPathNodeSample
    {
        public Nodes.YamlNode? Node { get; set; }
    }

    public class FastPathOptionsItem
    {
        public string? Name { get; set; }
        public int Size { get; set; }
    }

    public class FastPathAnimal
    {
        public string? Name { get; set; }
    }

    public class FastPathDog : FastPathAnimal
    {
        public int Bark { get; set; }
    }

    [YamlPolymorphic]
    [YamlDerivedType(typeof(FastPathPolymorphicDerived), "derived")]
    public class FastPathPolymorphic
    {
        public int Value { get; set; }
    }

    public class FastPathPolymorphicDerived : FastPathPolymorphic
    {
        public int More { get; set; }
    }

    [YamlSerializable(typeof(FastPathOptionsSample))]
    [YamlSerializable(typeof(FastPathNodeSample))]
    [YamlSerializable(typeof(FastPathDog))]
    [YamlSerializable(typeof(FastPathPolymorphic))]
    internal partial class FastPathOptionsContext : YamlSerializerContext;
}
