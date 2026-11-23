// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;
using SY = SharpYaml;
using YC = YamlDotNet.Core;
using YD = YamlDotNet.Serialization;
using YR = YamlDotNet.RepresentationModel;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M7c, against YamlDotNet 18.1.0 and SharpYaml 3.17.0, run live: a flow collection at a line width (both libraries'
    /// emitters break it after a comma, libyaml's rule, and so does Styml with MaxLineWidth); the column across flushes
    /// (kept across flushes); a copy made through an alias (C); and IgnoreSourceStyles, which neither library has
    /// (both keep a source's styles on write-back, which is Styml's default).
    /// </summary>
    public class WriteBackFollowUpsParityTests
    {
        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        private static string Items(int count) => string.Join(", ", Enumerable.Range(1, count).Select(i => "item" + i));

        private static string YamlDotNetAtWidth(string yaml, int width)
        {
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(yaml));
            var writer = new StringWriter();
            stream.Save(new YC.Emitter(writer, new YC.EmitterSettings(2, width, false, 1024)), assignAnchors: false);
            return Lf(writer.ToString()).Replace("...\n", "");
        }

        private static string SharpYamlAtWidth(string yaml, int width)
        {
            SY.Model.YamlStream stream = SY.Model.YamlStream.Load(new StringReader(yaml));
            var writer = new StringWriter();
            stream.WriteTo(new SY.Emitter(writer, 2, width, false), true);
            return Lf(writer.ToString());
        }

        // ------------------------------------------------------------------------------------------------------------
        // B: a flow collection at a line width

        [Theory]
        [InlineData("a: [ITEMS]")]
        [InlineData("x:\n  a: [ITEMS]")]
        [InlineData("- [ITEMS]")]
        [InlineData("[ITEMS]")]
        [InlineData("{a: [ITEMS]}")]
        [InlineData("a: [[ITEMS], b]")]
        [InlineData("a: {k: [ITEMS], m: [ITEMS]}")]
        public void B_FlowBreaks_AsYamlDotNet(string source)
        {
            string yaml = source.Replace("ITEMS", Items(14));
            string styml = YamlNode.Parse(yaml)!.ToYamlString(new YamlSerializerOptions(s_block) { MaxLineWidth = 40 });
            Assert.Equal(YamlDotNetAtWidth(yaml, 40), styml);

            // Both libraries read Styml's lines back to the source's values.
            var source1 = new YR.YamlStream();
            source1.Load(new StringReader(yaml));
            var back = new YR.YamlStream();
            back.Load(new StringReader(styml));
            Assert.True(source1.Documents[0].RootNode.Equals(back.Documents[0].RootNode));
            Assert.Equal(Lf(SY.Model.YamlStream.Load(new StringReader(yaml)).ToString()), Lf(SY.Model.YamlStream.Load(new StringReader(styml)).ToString()));
        }

        [Theory]
        [InlineData("[ITEMS]")]
        [InlineData("{a: [ITEMS]}")]
        [InlineData("{a: {k: [ITEMS], m: [ITEMS]}}")]
        public void B_FlowBreaks_AsSharpYaml(string source)
        {
            // SharpYaml's emitter breaks at the same places for a flow root; in block context its sequences are indented
            // differently (its block layout, not the break).
            string yaml = source.Replace("ITEMS", Items(14));
            string styml = YamlNode.Parse(yaml)!.ToYamlString(new YamlSerializerOptions(s_block) { MaxLineWidth = 40 });
            Assert.Equal(SharpYamlAtWidth(yaml, 40), styml);
        }

        [Fact]
        public void B_NoWidth_OneLine_InAllThree()
        {
            string yaml = "a: [" + Items(30) + "]";
            Assert.Equal(yaml + "\n", YamlNode.Parse(yaml)!.ToYamlString(s_block));
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(yaml));
            var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            Assert.Equal(yaml + "\n...\n", Lf(writer.ToString()));
            var sy = new StringWriter();
            SY.Model.YamlStream.Load(new StringReader(yaml)).WriteTo(sy, true);
            Assert.Equal(yaml + "\n", Lf(sy.ToString()));
        }

        // ------------------------------------------------------------------------------------------------------------
        // The column across flushes

        [Fact]
        public void Column_AcrossFlushes_ReadsTheSameInBothLibraries()
        {
            var value = new Dictionary<string, string> { ["key"] = string.Join(" ", Enumerable.Range(1, 60).Select(i => "word" + i)) };
            var options = new YamlSerializerOptions(s_block) { MaxLineWidth = 40 };
            string whole = YamlSerializer.Serialize(value, options);
            using var stream = new MemoryStream();
            YamlSerializer.Serialize(stream, value, new YamlSerializerOptions(options) { DefaultBufferSize = 1 });
            Assert.Equal(whole, Encoding.UTF8.GetString(stream.ToArray()));

            // Every folded line is within the width, as YamlDotNet's and SharpYaml's emitters keep theirs.
            Assert.All(whole.Split('\n'), line => Assert.True(line.Length <= 40 + "word60".Length, line));
            Assert.Equal(value["key"], new YD.DeserializerBuilder().Build().Deserialize<Dictionary<string, string>>(whole)["key"]);
            Assert.Equal(value["key"], SY.YamlSerializer.Deserialize<Dictionary<string, string>>(whole)!["key"]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // C: a copy made through an alias

        [Fact]
        public void C_AliasCopy_BesideTheLibraries()
        {
            const string Yaml = "a: &x [1, 2]\nb: *x\n";

            // YamlDotNet's model shares the anchored node, so an edit of the root keeps the alias.
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(Yaml));
            ((YR.YamlMappingNode)stream.Documents[0].RootNode).Add("c", "3");
            var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            Assert.Equal("a: &x [1, 2]\nb: *x\nc: 3\n...\n", Lf(writer.ToString()));

            // Styml's nodes copy what an alias names (M5a), so the copy is written without the anchor, not a second time.
            YamlObject node = YamlNode.Parse(Yaml)!.AsObject();
            node["c"] = 3;
            string styml = node.ToYamlString(s_block);
            Assert.Equal("a: &x [1, 2]\nb: [1, 2]\nc: 3\n", styml);
            Assert.Single(styml.Split('&').Skip(1));

            // Both libraries read the two forms as the same values.
            var a = new YR.YamlStream();
            a.Load(new StringReader(Lf(writer.ToString())));
            var b = new YR.YamlStream();
            b.Load(new StringReader(styml));
            Assert.True(a.Documents[0].RootNode.Equals(b.Documents[0].RootNode));
            Assert.Equal(new[] { 1, 2 }, SY.YamlSerializer.Deserialize<Dictionary<string, object>>(styml)!["b"] is List<object> list ? list.Select(Convert.ToInt32).ToArray() : null);
        }

        // ------------------------------------------------------------------------------------------------------------
        // IgnoreSourceStyles

        [Fact]
        public void IgnoreSourceStyles_NoLibraryEquivalent_OutputReadsTheSame()
        {
            const string Json = """{"name": "x", "items": [1, 2], "nested": {"flag": true, "s": "12"}}""";

            // Both libraries keep a JSON source's styles on write-back, as Styml does by default.
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(Json));
            var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            Assert.StartsWith("{\"name\": \"x\"", Lf(writer.ToString()));
            var sy = new StringWriter();
            SY.Model.YamlStream.Load(new StringReader(Json)).WriteTo(sy, true);
            Assert.StartsWith("{\"name\": \"x\"", Lf(sy.ToString()));
            Assert.StartsWith("{\"name\": \"x\"", YamlNode.Parse(Json)!.ToYamlString(s_block));

            // With the option, block YAML that both libraries read to the same values.
            string block = YamlNode.Parse(Json)!.ToYamlString(new YamlSerializerOptions(s_block) { IgnoreSourceStyles = true });
            Assert.Equal("name: x\nitems:\n- 1\n- 2\nnested:\n  flag: true\n  s: \"12\"\n", block);
            var back = new YR.YamlStream();
            back.Load(new StringReader(block));
            Assert.True(stream.Documents[0].RootNode.Equals(back.Documents[0].RootNode));
            var fromSy = SY.YamlSerializer.Deserialize<Dictionary<string, object>>(block)!;
            Assert.Equal("x", fromSy["name"]);
            Assert.Equal("12", ((Dictionary<string, object>)fromSy["nested"])["s"]);
        }
    }
}
