// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// The serializer in block style (M4a): <see cref="YamlSerializerOptions.WriteStyle"/>,
    /// the typed rules, decision 8 in the converters, top-level values, and the paths that stay
    /// JSON-compatible.
    /// </summary>
    public class BlockSerializerTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        public enum Color { Red, Yes, Null }

        public sealed class Model
        {
            public string? Name { get; set; }
            public string? Flag { get; set; }
            public string? Number { get; set; }
            public int Count { get; set; }
            public double Ratio { get; set; }
            public double NotANumber { get; set; }
            public float Infinite { get; set; }
            public DateTime Created { get; set; }
            public TimeSpan Elapsed { get; set; }
            public Guid Id { get; set; }
            public Color Plain { get; set; }
            public Color Quoted { get; set; }
            public Color KeywordName { get; set; }
            public byte[]? Bytes { get; set; }
            public Version? Version { get; set; }
            public char Letter { get; set; }
            public Uri? Link { get; set; }
            public List<string>? Items { get; set; }
            public Dictionary<string, int>? Counts { get; set; }
            public Model? Child { get; set; }
            public string? Missing { get; set; }
#if NET
            public DateOnly Day { get; set; }
            public TimeOnly Time { get; set; }
#endif
        }

        private static Model Sample() => new()
        {
            Name = "app",
            Flag = "yes",
            Number = "12",
            Count = 3,
            Ratio = 1.5,
            NotANumber = double.NaN,
            Infinite = float.PositiveInfinity,
            Created = new DateTime(2001, 12, 14, 21, 59, 43, DateTimeKind.Utc),
            Elapsed = new TimeSpan(1, 2, 3),
            Id = Guid.Empty,
            Plain = Color.Red,
            Quoted = Color.Yes,
            KeywordName = Color.Null,
            Bytes = new byte[] { 1, 2, 3 },
            Version = new Version(1, 10),
            Letter = 'y',
            Link = new Uri("https://example.com/a?b=c"),
            Items = new List<string> { "a", "true", "" },
            Counts = new Dictionary<string, int> { ["1"] = 1, ["x"] = 2 },
            Child = new Model { Name = "child", Items = new List<string>() },
#if NET
            Day = new DateOnly(2001, 12, 14),
            Time = new TimeOnly(10, 2, 3),
#endif
        };

        // Decision 8: in block style NaN and infinity are YAML floats, written without
        // AllowNamedFloatingPointLiterals, even where the handling is Strict (the imported NonFiniteValues_WithoutNamedLiterals_Throw
        // and AttributeOnMember_WinsOver_AttributeOnType assert the JSON behaviour, and are tagged jsonoutput).
        [Fact]
        public void Serialize_Block_NonFinite_WithoutNamedLiterals()
        {
            Assert.Equal(".nan\n", YamlSerializer.Serialize(double.NaN, s_block));
            Assert.Equal("-.inf\n", YamlSerializer.Serialize(float.NegativeInfinity, s_block));
            var strict = new YamlSerializerOptions(s_block) { NumberHandling = YamlNumberHandling.Strict };
            Assert.Equal(".inf\n", YamlSerializer.Serialize(double.PositiveInfinity, strict));
            Assert.Equal("Double: .nan\n", YamlSerializer.Serialize(new StrictMember { Double = double.NaN }, s_block));
#if NET11_0_OR_GREATER
            Assert.Equal(".nan\n", YamlSerializer.Serialize(System.Numerics.BFloat16.NaN, s_block));
            Assert.Equal(".inf\n", YamlSerializer.Serialize(System.Numerics.Decimal32.PositiveInfinity, s_block));
            Assert.Equal(".nan\n", YamlSerializer.Serialize(System.Numerics.Decimal64.NaN, s_block));
            Assert.Equal("-.inf\n", YamlSerializer.Serialize(System.Numerics.Decimal128.NegativeInfinity, s_block));
#endif

            // M5f: a non-finite key is written as decision 8 writes the value, as both libraries write it; it threw
            // before M5f. The JSON-compatible style still throws.
            Assert.Equal(".inf: 42\n", YamlSerializer.Serialize(new Dictionary<double, int> { [double.PositiveInfinity] = 42 }, s_block));
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(new Dictionary<double, int> { [double.PositiveInfinity] = 42 }, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));

            // The JSON-compatible style keeps the JSON behaviour.
            var json = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible };
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(double.NaN, json));
            Assert.Throws<ArgumentException>(() => YamlSerializer.Serialize(new StrictMember { Double = double.NaN }, json));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowNamedFloatingPointLiterals)]
        public sealed class StrictMember
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public double Double { get; set; }
        }

        [Fact]
        public void Serialize_Block_TypedRules()
        {
            var options = new YamlSerializerOptions(s_block) { Converters = { new Serialization.YamlStringEnumConverter() } };
            string yaml = YamlSerializer.Serialize(Sample(), options);

            Assert.Contains("\nName: app\n", "\n" + yaml);
            Assert.Contains("\nFlag: \"yes\"\n", yaml);
            Assert.Contains("\nNumber: \"12\"\n", yaml);
            Assert.Contains("\nNotANumber: .nan\n", yaml);   // decision 8
            Assert.Contains("\nInfinite: .inf\n", yaml);
            Assert.Contains("\nCreated: 2001-12-14T21:59:43Z\n", yaml);   // decision 2b (i)
            Assert.Contains("\nElapsed: \"01:02:03\"\n", yaml);   // 5.9: always quoted
            Assert.Contains("\nId: 00000000-0000-0000-0000-000000000000\n", yaml);
            Assert.Contains("\nPlain: Red\n", yaml);   // enum names per member
            Assert.Contains("\nQuoted: \"Yes\"\n", yaml);
            Assert.Contains("\nKeywordName: \"Null\"\n", yaml);
            Assert.Contains("\nBytes: \"AQID\"\n", yaml);
            Assert.Contains("\nVersion: \"1.10\"\n", yaml);
            Assert.Contains("\nLetter: \"y\"\n", yaml);
            Assert.Contains("\nLink: https://example.com/a?b=c\n", yaml);
            Assert.Contains("\nItems:\n- a\n- \"true\"\n- \"\"\n", yaml);
            Assert.Contains("\nCounts:\n  \"1\": 1\n  x: 2\n", yaml);
            Assert.Contains("\nChild:\n  Name: child\n", yaml);
            Assert.Contains("\n  Items: []\n", yaml);
            Assert.Contains("\nMissing: null\n", yaml);
#if NET
            Assert.Contains("\nDay: 2001-12-14\n", yaml);
            Assert.Contains("\nTime: \"10:02:03\"\n", yaml);
#endif
            Assert.EndsWith("\n", yaml);

            Model back = YamlSerializer.Deserialize<Model>(yaml, options)!;
            Assert.Equal(YamlSerializer.Serialize(Sample(), options), YamlSerializer.Serialize(back, options));
        }

        public sealed class Names
        {
            private const string Long = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
            internal const string Long1025 = Long + Long + Long + Long + Long + Long + Long + Long + "x";

            [YamlPropertyName("yes")] public int A { get; set; } = 1;
            [YamlPropertyName("1")] public int B { get; set; } = 2;
            [YamlPropertyName("a: b")] public int C { get; set; } = 3;
            [YamlPropertyName("é")] public int D { get; set; } = 4;
            [YamlPropertyName("")] public int E { get; set; } = 5;
            [YamlPropertyName("a\"b")] public int F { get; set; } = 6;
            [YamlPropertyName("C:\\x")] public int G { get; set; } = 7;
            [YamlPropertyName("x&y<z>")] public int H { get; set; } = 8;
            [YamlPropertyName("tab\there")] public int I { get; set; } = 9;
            [YamlPropertyName("NaN")] public string? K { get; set; } = "NaN";
        }

        // Reflection only: the source generator names a field after the property name, and a name of 1025 characters
        // exceeds the metadata limit (CS7013), as upstream's generator would.
        public sealed class LongName
        {
            [YamlPropertyName(Names.Long1025)] public int J { get; set; } = 10;
        }

        [Fact]
        public void Serialize_Block_PropertyNames_NameSectionsAndPreEncoded()
        {
            // The reflection path's block name sections (YamlPropertyInfo.BlockNameSection) and the source
            // generator's pre-encoded names must quote what the scalar rules quote, and give the same text.
            string reflection = YamlSerializer.Serialize(new Names(), s_block);
            var context = new BlockSerializerTestsContext(new YamlSerializerOptions(s_block));
            string generated = YamlSerializer.Serialize(new Names(), context.Names);
            Assert.Equal(reflection, generated);
            Assert.Equal("\"yes\": 1\n\"1\": 2\n\"a: b\": 3\né: 4\n\"\": 5\na\"b: 6\nC:\\x: 7\nx&y<z>: 8\n\"tab\\there\": 9\nNaN: NaN\n", reflection);

            // A name above the implicit-key limit has no block name section: it is written as an explicit key.
            string explicitKey = YamlSerializer.Serialize(new LongName(), s_block);
            Assert.Equal("? " + Names.Long1025 + "\n: 10\n", explicitKey);
            Assert.Equal(10, YamlSerializer.Deserialize<LongName>(explicitKey)!.J);
            BlockWriterTests.AssertReadsBack(reflection, YamlSerializer.Serialize(new Names(), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));

            // With an explicit encoder, what it escapes is quoted.
            var encoded = new YamlSerializerOptions(s_block) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default };
            string escaped = YamlSerializer.Serialize(new Names(), encoded);
            Assert.Contains("\n\"\\u00E9\": 4\n", escaped);
            Assert.Contains("\n\"x\\u0026y\\u003Cz\\u003E\": 8\n", escaped);
            BlockWriterTests.AssertReadsBack(escaped, YamlSerializer.Serialize(new Names(), new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }));
            Names back = YamlSerializer.Deserialize<Names>(escaped, encoded)!;
            Assert.Equal(8, back.H);
        }

        [Fact]
        public void Serialize_Block_SourceGenerated_EqualsReflection()
        {
            var options = new YamlSerializerOptions(s_block);
            string reflection = YamlSerializer.Serialize(Sample(), options);
            var context = new BlockSerializerTestsContext(new YamlSerializerOptions(s_block));
            string generated = YamlSerializer.Serialize(Sample(), context.Model);
            Assert.Equal(reflection, generated);
            Model back = YamlSerializer.Deserialize(generated, context.Model)!;
            Assert.Equal(generated, YamlSerializer.Serialize(back, context.Model));
        }

        [Fact]
        public void Serialize_Block_Streaming_EqualsTheString()
        {
            var options = new YamlSerializerOptions(s_block) { DefaultBufferSize = 1 };
            Model value = Sample();
            value.Child!.Child = Sample();
            using var stream = new MemoryStream();
            YamlSerializer.Serialize(stream, value, options);
            Assert.Equal(YamlSerializer.Serialize(value, s_block), Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public void Serialize_Block_IndentSequences()
        {
            var options = new YamlSerializerOptions(s_block) { IndentSequences = true };
            Assert.Equal("Items:\n  - a\n", YamlSerializer.Serialize(new Dictionary<string, List<string>> { ["Items"] = new() { "a" } }, options));
        }

        [Fact]
        public void Serialize_WriteStyleUnset_IsBlock_UnlessTheSwitchIsSet()
        {
            // M4c: an unset style is block; the AppContext switch restores JSON-compatible (test leg YJ sets it).
            bool json = AppContext.TryGetSwitch("System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle", out bool enabled) && enabled;
            Assert.Equal(json ? YamlWriterStyle.JsonCompatible : YamlWriterStyle.Block, new YamlSerializerOptions().WriteStyle);
            Assert.Equal(json ? "{\"a\":1}" : "a: 1" + Environment.NewLine, YamlSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1 }));
        }

        [Fact]
        public async Task TopLevelValues_Block_AreDocuments()
        {
            using var stream = new MemoryStream();
            await YamlSerializer.SerializeAsyncEnumerable(stream, Items(), topLevelValues: true, s_block);
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("a: 1\n---\na: 2\n---\na: 3\n", yaml);

            stream.Position = 0;
            var read = new List<int>();
            await foreach (Dictionary<string, int>? item in YamlSerializer.DeserializeAsyncEnumerable<Dictionary<string, int>>(stream, topLevelValues: true))
            {
                read.Add(item!["a"]);
            }

            Assert.Equal(new[] { 1, 2, 3 }, read);

            static async IAsyncEnumerable<Dictionary<string, int>> Items()
            {
                for (int i = 1; i <= 3; i++)
                {
                    await Task.Yield();
                    yield return new Dictionary<string, int> { ["a"] = i };
                }
            }
        }

        // In block style, set or by default, top-level values follow the user's NewLine and indentation. Until
        // the fix of a78369b, an unset style took the JSON Lines options, which dropped both, and the writer then wrote
        // block with Environment.NewLine (CRLF on Windows). The imported
        // SerializeAsyncEnumerable_TopLevelValues_LineTerminatorIsAlwaysLineFeed asserts the JSON-compatible behaviour
        // and is tagged jsonoutput.
        [Theory]
        [InlineData(false, "\n", 2)]
        [InlineData(false, "\r\n", 2)]
        [InlineData(false, "\n", 4)]
        [InlineData(true, "\n", 2)]
        [InlineData(true, "\r\n", 4)]
        public async Task TopLevelValues_Block_FollowNewLineAndIndentation(bool styleSet, string newLine, int indentSize)
        {
            var options = new YamlSerializerOptions { NewLine = newLine, IndentSize = indentSize };
            if (styleSet)
            {
                options.WriteStyle = YamlWriterStyle.Block;
            }

            using var stream = new MemoryStream();
            await YamlSerializer.SerializeAsyncEnumerable(stream, Items(), topLevelValues: true, options);
            string yaml = Encoding.UTF8.GetString(stream.ToArray());

            if (!styleSet && AppContext.TryGetSwitch("System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle", out bool json) && json)
            {
                // Leg YJ: the unset style is JSON-compatible, and writes what that style writes when it is set.
                using var jsonStream = new MemoryStream();
                await YamlSerializer.SerializeAsyncEnumerable(jsonStream, Items(), topLevelValues: true,
                    new YamlSerializerOptions(options) { WriteStyle = YamlWriterStyle.JsonCompatible });
                Assert.Equal(Encoding.UTF8.GetString(jsonStream.ToArray()), yaml);
                return;
            }

            string indent = new string(' ', indentSize);
            Assert.Equal($"a:{newLine}{indent}b: 1{newLine}---{newLine}a:{newLine}{indent}b: 2{newLine}", yaml);

            static async IAsyncEnumerable<Dictionary<string, Dictionary<string, int>>> Items()
            {
                for (int i = 1; i <= 2; i++)
                {
                    await Task.Yield();
                    yield return new() { ["a"] = new() { ["b"] = i } };
                }
            }
        }

        [Fact]
        public async Task TopLevelValues_JsonCompatible_AreDocumentsToo()
        {
            using var stream = new MemoryStream();
            await YamlSerializer.SerializeAsyncEnumerable(stream, Numbers(), topLevelValues: true, new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            Assert.Equal("1\n---\n2\n", Encoding.UTF8.GetString(stream.ToArray()));

            static async IAsyncEnumerable<int> Numbers()
            {
                await Task.Yield();
                yield return 1;
                yield return 2;
            }
        }

        [Fact]
        public void SerializeTo_FollowTheWriteStyle()
        {
            // M5a: the DOM reads YAML, so SerializeToDocument, SerializeToElement,
            // SerializeToNode and Deserialize(YamlNode) write in the options' style; NaN and infinity no longer need zeroing.
            Model value = Sample();
            string yaml = YamlSerializer.Serialize(value, s_block);
            using YamlDocument document = YamlSerializer.SerializeToDocument(value, s_block);
            Assert.Equal("app", document.RootElement.GetProperty("Name").GetString());
            Assert.Equal(yaml.TrimEnd('\n'), document.RootElement.GetRawText().TrimEnd('\n'));
            Assert.Equal(double.NaN, document.RootElement.GetProperty("NotANumber").GetDouble());
            YamlElement element = YamlSerializer.SerializeToElement(value, s_block);
            Assert.StartsWith("Name: app", element.GetRawText());
            Nodes.YamlNode? node = YamlSerializer.SerializeToNode(value, s_block);
            Assert.Equal("child", (string?)node!["Child"]!["Name"]);
            Model back = YamlSerializer.Deserialize<Model>(node, s_block)!;
            Assert.Equal("app", back.Name);
            Assert.True(double.IsNaN(back.NotANumber));

            // In JSON-compatible style the same paths write JSON-compatible text.
            var json = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible };
            value.NotANumber = 0;
            value.Infinite = 0;
            Assert.StartsWith("{", YamlSerializer.SerializeToElement(value, json).GetRawText());
        }

        [Fact]
        public void NodeToYamlString_FollowsTheOptions()
        {
            Nodes.YamlNode node = Nodes.YamlNode.Parse("{\"a\":[1,\"yes\"]}")!;
            // M5f: the JSON source's double-quoted key and string keep their style, as both libraries keep them;
            // M7b (item 1): so do its flow collections.
            Assert.Equal("{\"a\": [1, \"yes\"]}\n", node.ToYamlString(s_block));
        }
    }

    [YamlSerializable(typeof(BlockSerializerTests.Model))]
    [YamlSerializable(typeof(BlockSerializerTests.Names))]
    internal partial class BlockSerializerTestsContext : YamlSerializerContext;
}
