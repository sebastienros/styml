// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M5e, row by row against YamlDotNet 18.1.0 and SharpYaml
    /// 3.17.0, run live: collection keys read and written, duplicates, alias keys, and the schema's number forms in typed
    /// keys. Each row gives what each library yields and what Styml yields; a departure is one of M5e's recorded choices
    /// and is named.
    /// </summary>
    public class CollectionKeyParityTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();
        private static readonly SY.YamlSerializerOptions s_sharpYaml = new();
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        public class Point
        {
            public int X { get; set; }

            public int Y { get; set; }

            public override bool Equals(object? obj) => obj is Point p && p.X == X && p.Y == Y;

            public override int GetHashCode() => (X * 397) ^ Y;
        }

        private static string Try(Func<object?> read)
        {
            try
            {
                return read() switch
                {
                    null => "null",
                    IEnumerable<KeyValuePair<List<string>, string>> lists => string.Join(";", lists.Select(p => "[" + string.Join(",", p.Key) + "]=" + p.Value)),
                    IEnumerable<KeyValuePair<Point, string>> points => string.Join(";", points.Select(p => $"({p.Key.X},{p.Key.Y})={p.Value}")),
                    System.Collections.ICollection collection => "count " + collection.Count,
                    object other => other.ToString()!,
                };
            }
            catch (Exception)
            {
                return "throws";
            }
        }

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        // ------------------------------------------------------------------------------------------------------------
        // Reading

        [Theory]
        // input, YamlDotNet, SharpYaml, Styml
        [InlineData("[a, b]: x\n", "[a,b]=x", "throws", "[a,b]=x")]
        [InlineData("? - a\n  - b\n: x\n", "[a,b]=x", "throws", "[a,b]=x")]
        [InlineData("a: x\n", "throws", "throws", "throws")]
        public void Read_ListKeys(string yaml, string yamlDotNet, string sharpYaml, string styml)
        {
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<Dictionary<List<string>, string>>(yaml)));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<Dictionary<List<string>, string>>(yaml, s_sharpYaml)));
            Assert.Equal(styml, Try(() => YamlSerializer.Deserialize<Dictionary<List<string>, string>>(yaml, s_block)));
        }

        [Fact]
        public void Read_ClassKeys()
        {
            const string Yaml = "{X: 1, Y: 2}: x\n";
            Assert.Equal("(1,2)=x", Try(() => s_yamlDotNet.Deserialize<Dictionary<Point, string>>(Yaml)));
            Assert.Equal("(1,2)=x", Try(() => YamlSerializer.Deserialize<Dictionary<Point, string>>(Yaml, s_block)));
        }

        [Fact]
        public void Read_ObjectKeys()
        {
            // A scalar key is a string in both libraries and in Styml (the parity rule).
            Assert.Equal("String", s_yamlDotNet.Deserialize<Dictionary<object, object>>("a: x\n").Keys.Single().GetType().Name);
            Assert.Equal("String", SY.YamlSerializer.Deserialize<Dictionary<object, object>>("a: x\n", s_sharpYaml)!.Keys.Single().GetType().Name);
            Assert.Equal("String", YamlSerializer.Deserialize<Dictionary<object, object>>("a: x\n", s_block)!.Keys.Single().GetType().Name);

            // A collection key: YamlDotNet gives a List<object>; Styml a YamlElement; SharpYaml refuses.
            Assert.Equal("List`1", s_yamlDotNet.Deserialize<Dictionary<object, object>>("[a, b]: x\n").Keys.Single().GetType().Name);
            Assert.Equal("throws", Try(() => SY.YamlSerializer.Deserialize<Dictionary<object, object>>("[a, b]: x\n", s_sharpYaml)));
            Assert.Equal("YamlElement", YamlSerializer.Deserialize<Dictionary<object, object>>("[a, b]: x\n", s_block)!.Keys.Single().GetType().Name);
        }

        [Fact]
        public void Read_IntoAStringKey()
        {
            // Both refuse a collection key into a string key (recorded line 3).
            Assert.Equal("throws", Try(() => s_yamlDotNet.Deserialize<Dictionary<string, string>>("[a, b]: x\n")));
            Assert.Equal("throws", Try(() => YamlSerializer.Deserialize<Dictionary<string, string>>("[a, b]: x\n", s_block)));
        }

        [Fact]
        public void Duplicates()
        {
            const string Yaml = "[a, b]: 1\n[a,b]: 2\n";

            // A typed dictionary with List keys keeps both (recorded line 1), as YamlDotNet's serializer.
            Assert.Equal(2, s_yamlDotNet.Deserialize<Dictionary<List<string>, int>>(Yaml).Count);
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<List<string>, int>>(Yaml, s_block)!.Count);

            // The DOM calls them one key, as YamlDotNet's representation model.
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            Assert.ThrowsAny<Exception>(() => stream.Load(new IO.StringReader(Yaml)));
            Assert.Throws<YamlException>(() => YamlDocument.Parse(Yaml));
        }

        [Fact]
        public void AliasKeys()
        {
            // The same node twice as a key: one entry, the last value (YamlDotNet; Styml under LastWins; Error refuses).
            const string Yaml = "&k [a, b]: 1\n*k : 2\n";
            Dictionary<object, object> ydn = s_yamlDotNet.Deserialize<Dictionary<object, object>>(Yaml);
            Assert.Equal("2", ydn.Values.Single());
            var lastWins = new YamlSerializerOptions(s_block) { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            Assert.Equal(2, YamlSerializer.Deserialize<Dictionary<List<string>, int>>(Yaml, lastWins)!.Values.Single());
        }

        // ------------------------------------------------------------------------------------------------------------
        // Writing

        [Fact]
        public void Write()
        {
            var lists = new Dictionary<List<string>, string> { [new List<string> { "a", "b" }] = "x" };
            Assert.Equal("? - a\n  - b\n: x\n", Lf(new YD.SerializerBuilder().Build().Serialize(lists)));
            Assert.Equal("? - a\n  - b\n: x\n", YamlSerializer.Serialize(lists, s_block));

            // SharpYaml writes the key's ToString(), which reads back as a string; Styml does not.
            Assert.Contains("List`1", SY.YamlSerializer.Serialize(lists, s_sharpYaml));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Number forms in typed keys

        [Theory]
        // key, YamlDotNet, SharpYaml, Styml (Core), Styml (YAML 1.1)
        [InlineData("0x1", "1", "1", "1", "1")]
        [InlineData("1_000", "1000", "1000", "throws", "1000")]
        [InlineData("+1", "1", "1", "1", "1")]
        [InlineData("\"0x1\"", "1", "1", "1", "1")]
        // Review 6: YAML 1.1 reads a leading zero as octal, key and value; YamlDotNet agrees, SharpYaml reads decimal.
        [InlineData("017", "15", "17", "17", "15")]
        [InlineData("0777", "511", "777", "777", "511")]
        [InlineData("1e3", "throws", "throws", "throws", "throws")]
        public void IntKeys(string key, string yamlDotNet, string sharpYaml, string core, string yaml11)
        {
            string yaml = key + ": v\n";
            Assert.Equal(yamlDotNet, Try(() => s_yamlDotNet.Deserialize<Dictionary<int, string>>(yaml).Keys.Single()));
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, s_sharpYaml)!.Keys.Single()));
            Assert.Equal(core, Try(() => YamlSerializer.Deserialize<Dictionary<int, string>>(yaml)!.Keys.Single()));
            Assert.Equal(yaml11, Try(() => YamlSerializer.Deserialize<Dictionary<int, string>>(yaml, new YamlSerializerOptions { Schema = YamlSchemaKind.Yaml11 })!.Keys.Single()));
        }

        [Fact]
        public void IntKeys_Collisions()
        {
            // 1 and 0x1 are one int key: YamlDotNet keeps the last silently, SharpYaml and Styml's default refuse.
            const string Yaml = "1: a\n0x1: b\n";
            Assert.Equal("b", s_yamlDotNet.Deserialize<Dictionary<int, string>>(Yaml)[1]);
            Assert.Equal("throws", Try(() => SY.YamlSerializer.Deserialize<Dictionary<int, string>>(Yaml, s_sharpYaml)));
            Assert.Equal("throws", Try(() => YamlSerializer.Deserialize<Dictionary<int, string>>(Yaml)));
        }
    }
}
