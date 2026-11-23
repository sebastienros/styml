// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Serialization;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;
using YR = YamlDotNet.RepresentationModel;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M5f, against YamlDotNet 18.1.0 and SharpYaml 3.17.0, run live: what
    /// Styml writes by default (the literal default, plain number keys) and with M5f's features (comments, sorted
    /// order, omitted empty collections, kept styles) reads the same in both libraries.
    /// </summary>
    public class WriteFeatureParityTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();
        private static readonly SY.YamlSerializerOptions s_sharpYaml = new();
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        // ------------------------------------------------------------------------------------------------------------
        // A2: the literal default reads the same in both libraries

        public static IEnumerable<object[]> MultiLineStrings() => new[]
        {
            "a\nb", "a\nb\n", "a\nb\n\n", "a \nb", "a\nb ", " a\nb", "   a\nb", "\n a", "\n\na", "a\tb\nc", "a\n\tb",
            "a\n---\nb", "---\na", "a\n...\nb", "a\n# b", "a:\nb", "\"a\\\"\n'b'", "é\n😀",
            "one two three four five six seven eight nine ten\nb",
            // Double-quoted by A2's rules: each still reads the same.
            "\ta\nb", "\n\ta", "a\n  \nb", "a\r\nb", "\n", "\n\n",
        }.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(MultiLineStrings))]
        public void Literal_ReadsTheSameInBothLibraries(string value)
        {
            var map = new Dictionary<string, string> { ["k"] = value };
            string yaml = YamlSerializer.Serialize(map, s_block);
            Assert.Equal(value, s_yamlDotNet.Deserialize<Dictionary<string, string>>(yaml)["k"]);
            Assert.Equal(value, SY.YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, s_sharpYaml)!["k"]);

            // As a sequence item, and nested with an indicator.
            string list = YamlSerializer.Serialize(new List<string> { value }, s_block);
            Assert.Equal(value, s_yamlDotNet.Deserialize<List<string>>(list).Single());
            Assert.Equal(value, SY.YamlSerializer.Deserialize<List<string>>(list, s_sharpYaml)!.Single());
            string nested = YamlSerializer.Serialize(new Dictionary<string, List<string>> { ["k"] = new() { value } }, s_block);
            Assert.Equal(value, s_yamlDotNet.Deserialize<Dictionary<string, List<string>>>(nested)["k"].Single());
            Assert.Equal(value, SY.YamlSerializer.Deserialize<Dictionary<string, List<string>>>(nested, s_sharpYaml)!["k"].Single());
        }

        [Fact]
        public void Literal_RootAndRule4()
        {
            // The root: literal, read by both.
            string root = YamlSerializer.Serialize("a\nb", s_block);
            Assert.Equal("|-\n  a\n  b\n", root);
            Assert.Equal("a\nb", s_yamlDotNet.Deserialize<string>(root));
            Assert.Equal("a\nb", SY.YamlSerializer.Deserialize<string>(root, s_sharpYaml));

            // Rule 4: SharpYaml refuses a block scalar whose first line starts with a tab, which is why Styml quotes it.
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<string, string>>("k: |-\n  \ta\n  b\n", s_sharpYaml));
            Assert.Equal("\ta\nb", s_yamlDotNet.Deserialize<Dictionary<string, string>>("k: |-\n  \ta\n  b\n")["k"]);
            Assert.Equal("k: \"\\ta\\nb\"\n", YamlSerializer.Serialize(new Dictionary<string, string> { ["k"] = "\ta\nb" }, s_block));
        }

        // ------------------------------------------------------------------------------------------------------------
        // A3: plain number keys, as both libraries write them

        [Fact]
        public void NumberKeys_AsBothLibraries()
        {
            var ints = new Dictionary<int, string> { [1] = "a", [-1] = "b" };
            Assert.Equal(Lf(new YD.SerializerBuilder().Build().Serialize(ints)), YamlSerializer.Serialize(ints, s_block));
            Assert.Equal(Lf(SY.YamlSerializer.Serialize(ints, s_sharpYaml)), YamlSerializer.Serialize(ints, s_block));

            var bools = new Dictionary<bool, string> { [true] = "a" };
            Assert.Equal(Lf(new YD.SerializerBuilder().Build().Serialize(bools)), YamlSerializer.Serialize(bools, s_block));
            var doubles = new Dictionary<double, string> { [1.5] = "a", [1e20] = "b", [double.NaN] = "c" };
            Assert.Equal(Lf(SY.YamlSerializer.Serialize(doubles, s_sharpYaml)), YamlSerializer.Serialize(doubles, s_block));
        }

        [Fact]
        public void NumberKeys_ReadBackByBothLibraries()
        {
            // Review 5: -0, 1.10 as a decimal and 1E+20 as a double read back to the same typed key in both libraries.
            string zero = YamlSerializer.Serialize(new Dictionary<double, string> { [-0.0] = "a" }, s_block);
            Assert.True(1 / s_yamlDotNet.Deserialize<Dictionary<double, string>>(zero).Keys.Single() < 0);
            Assert.True(1 / SY.YamlSerializer.Deserialize<Dictionary<double, string>>(zero, s_sharpYaml)!.Keys.Single() < 0);

            string dec = YamlSerializer.Serialize(new Dictionary<decimal, string> { [1.10m] = "a" }, s_block);
            Assert.Equal("1.10", s_yamlDotNet.Deserialize<Dictionary<decimal, string>>(dec).Keys.Single().ToString(Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("1.10", SY.YamlSerializer.Deserialize<Dictionary<decimal, string>>(dec, s_sharpYaml)!.Keys.Single().ToString(Globalization.CultureInfo.InvariantCulture));

            string big = YamlSerializer.Serialize(new Dictionary<double, string> { [1e20] = "a" }, s_block);
            Assert.Equal(1e20, s_yamlDotNet.Deserialize<Dictionary<double, string>>(big).Keys.Single());
            Assert.Equal(1e20, SY.YamlSerializer.Deserialize<Dictionary<double, string>>(big, s_sharpYaml)!.Keys.Single());

            // A Dictionary<string, T> reads the text in both.
            string ints = YamlSerializer.Serialize(new Dictionary<int, string> { [1] = "a" }, s_block);
            Assert.Equal("1", s_yamlDotNet.Deserialize<Dictionary<string, string>>(ints).Keys.Single());
            Assert.Equal("1", SY.YamlSerializer.Deserialize<Dictionary<string, string>>(ints, s_sharpYaml)!.Keys.Single());

            // 1 and 1.0 are one double key: Styml and SharpYaml refuse under Error; YamlDotNet keeps the last.
            const string Twice = "1: a\n1.0: b\n";
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<double, string>>(Twice));
            Assert.ThrowsAny<Exception>(() => SY.YamlSerializer.Deserialize<Dictionary<double, string>>(Twice, s_sharpYaml));
            Assert.Equal("b", s_yamlDotNet.Deserialize<Dictionary<double, string>>(Twice)[1]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // B4, B6, B7

        public class Commented
        {
            [YamlComment("The name.\nSecond # line")]
            [YD.YamlMember(Description = "The name.\nSecond # line")]
            public string? Name { get; set; } = "name";

            [YamlComment("tab\there")]
            public int Count { get; set; } = 1;
        }

        [Fact]
        public void Comments_ReadAsCommentsByBothLibraries()
        {
            string yaml = YamlSerializer.Serialize(new Commented(), s_block);
            Assert.Equal("# The name.\n# Second # line\nName: name\n# tab\there\nCount: 1\n", yaml);
            Assert.Equal(new Dictionary<string, string> { ["Name"] = "name", ["Count"] = "1" }, s_yamlDotNet.Deserialize<Dictionary<string, string>>(yaml));
            Assert.Equal(new Dictionary<string, string> { ["Name"] = "name", ["Count"] = "1" }, SY.YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, s_sharpYaml));

            // YamlDotNet writes the same lines for the name.
            Assert.StartsWith("# The name.\n# Second # line\nName: name\n", Lf(new YD.SerializerBuilder().Build().Serialize(new Commented())));
        }

        public class Unsorted
        {
            public int b { get; set; } = 1;
            public int B { get; set; } = 2;
            public int a { get; set; } = 3;
            public int A10 { get; set; } = 4;
            public int A9 { get; set; } = 5;
            public int é { get; set; } = 6;
            public int _x { get; set; } = 7;
            public int z { get; set; } = 8;
        }

        [Fact]
        public void Sorted_AsSharpYaml()
        {
            string sharpYaml = Lf(SY.YamlSerializer.Serialize(new Unsorted(), new SY.YamlSerializerOptions { MappingOrder = SY.YamlMappingOrderPolicy.Sorted }));
            Assert.Equal(sharpYaml, YamlSerializer.Serialize(new Unsorted(), new YamlSerializerOptions(s_block) { MappingOrder = YamlMappingOrder.Sorted }));
        }

        public class Empties
        {
            public List<int> List { get; set; } = new();
            public int[] Array { get; set; } = System.Array.Empty<int>();
            public Dictionary<string, int> Dictionary { get; set; } = new();
            public List<int>? NullList { get; set; }
            public List<int> NonEmpty { get; set; } = new() { 1 };
        }

        [Fact]
        public void EmptyCollections_AsYamlDotNet()
        {
            // WhenWritingEmptyCollection is YamlDotNet's OmitNull | OmitEmptyCollections on these members.
            YD.ISerializer omit = new YD.SerializerBuilder().ConfigureDefaultValuesHandling(YD.DefaultValuesHandling.OmitNull | YD.DefaultValuesHandling.OmitEmptyCollections).Build();
            string styml = YamlSerializer.Serialize(new Empties(), new YamlSerializerOptions(s_block) { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection });
            Assert.Equal(Lf(omit.Serialize(new Empties())), styml);
        }

        // ------------------------------------------------------------------------------------------------------------
        // C10: styles kept on write-back, as both libraries' models keep them

        [Fact]
        public void WriteBack_AsBothModels()
        {
            const string Source = "plain: a b\nsingle: 'it''s'\ndouble: \"x y\"\nliteral: |\n  l1\n  l2\nkeep: |+\n  k1\n\nfolded: >\n  f1 f2\n\n  f3\n'quoted key': 1\n\"dq key\": 2\nnum: '12'\n";
            using YamlDocument document = YamlDocument.Parse(Source);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n" }))
            {
                document.WriteTo(writer);
            }

            string styml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
            Assert.Equal(Source, styml);

            var stream = new YR.YamlStream();
            stream.Load(new StringReader(Source));
            var yamlDotNet = new StringWriter();
            stream.Save(yamlDotNet, assignAnchors: false);
            Assert.Equal(Source + "...\n", Lf(yamlDotNet.ToString()));

            var sharpYaml = new StringWriter();
            SY.Model.YamlStream.Load(new StringReader(Source)).WriteTo(sharpYaml, true);
            Assert.Equal(Source, Lf(sharpYaml.ToString()));
        }
    }
}
