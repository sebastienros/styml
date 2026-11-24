// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Unity's text files (Unity/*.unity, *.prefab, *.mat, written for these tests in Unity's shapes): each file read
    /// document by document with InheritTagDirectives and AcceptUnityStrippedMarker, and written back through
    /// Utf8YamlWriter, is the same bytes; with one value edited, only that line differs; typed models read the objects
    /// by their tags, with their anchors as file IDs.
    /// </summary>
    public class UnityFileTests
    {
        private static readonly YamlReaderOptions s_read = new() { AllowMultipleValues = true, InheritTagDirectives = true, AcceptUnityStrippedMarker = true };
        private static readonly YamlWriterOptions s_write = new() { Style = YamlWriterStyle.Block, NewLine = "\n", InheritTagDirectives = true };

        public static IEnumerable<object[]> Files() => new[] { "scene.unity", "prefab.prefab", "material.mat" }.Select(f => new object[] { f });

        private static byte[] Load(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Unity", name));

        // The header a tool writes back: the reader does not return a stream's directives, so the file's are copied.
        private static IEnumerable<string> Directives(byte[] file) =>
            Encoding.UTF8.GetString(file).Split('\n').TakeWhile(line => line.StartsWith("%", StringComparison.Ordinal));

        private static string WriteBack(byte[] file, Action<Utf8YamlWriter, YamlElement> writeDocument)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(buffer, s_write))
            {
                foreach (string directive in Directives(file))
                {
                    writer.WriteDirective(directive);
                }

                var reader = new Utf8YamlReader(file, s_read);
                while (reader.Read())
                {
                    YamlElement document = YamlElement.ParseValue(ref reader);
                    writer.WriteDocumentStart();
                    writeDocument(writer, document);
                }
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        }

        private static YamlNode ToNode(YamlElement document) => Serialization.Converters.YamlNodeConverter.Create(document, options: null)!;

        [Theory]
        [MemberData(nameof(Files))]
        public void AFile_RoundTripsByteForByte(string name)
        {
            byte[] file = Load(name);
            string source = Encoding.UTF8.GetString(file);
            Assert.Equal(source, WriteBack(file, (writer, document) => document.WriteTo(writer)));
            Assert.Equal(source, WriteBack(file, (writer, document) => ToNode(document).WriteTo(writer)));
        }

        [Theory]
        [InlineData("scene.unity", "  m_Name: Player", "  m_Name: Hero")]
        [InlineData("material.mat", "    - _Glossiness: 0.5", "    - _Glossiness: 0.75")]
        [InlineData("prefab.prefab", "      value: 3", "      value: 4")]
        public void OneEdit_ChangesOnlyItsLine(string name, string before, string after)
        {
            byte[] file = Load(name);
            string written = WriteBack(file, (writer, document) =>
            {
                YamlNode node = ToNode(document);
                Edit(node, name);
                node.WriteTo(writer);
            });

            string[] source = Encoding.UTF8.GetString(file).Split('\n');
            string[] lines = written.Split('\n');
            Assert.Equal(source.Length, lines.Length);
            int[] changed = Enumerable.Range(0, source.Length).Where(i => source[i] != lines[i]).ToArray();
            Assert.Single(changed);
            Assert.Equal(before, source[changed[0]]);
            Assert.Equal(after, lines[changed[0]]);
        }

        private static void Edit(YamlNode document, string name)
        {
            if (name == "scene.unity" && document["GameObject"] is YamlNode gameObject && gameObject["m_Name"]?.GetValue<string>() == "Player")
            {
                gameObject["m_Name"] = "Hero";
            }
            else if (name == "material.mat" && document["Material"] is YamlNode material)
            {
                material["m_SavedProperties"]!["m_Floats"]![0]!["_Glossiness"] = 0.75;
            }
            else if (name == "prefab.prefab" && document["PrefabInstance"] is YamlNode instance)
            {
                instance["m_Modification"]!["m_Modifications"]![0]!["value"] = 4;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Typed models: the tags as discriminators, the anchors as file IDs.

        public abstract class UnityObject { }

        public sealed class GameObjectDocument : UnityObject { public GameObjectData? GameObject { get; set; } }

        public sealed class TransformDocument : UnityObject { public TransformData? Transform { get; set; } }

        public sealed class MonoBehaviourDocument : UnityObject { public MonoBehaviourData? MonoBehaviour { get; set; } }

        public sealed class GameObjectData
        {
            public string? m_Name { get; set; }
            public int m_IsActive { get; set; }
            public List<ComponentEntry>? m_Component { get; set; }
        }

        public sealed class ComponentEntry { public FileReference? component { get; set; } }

        public sealed class TransformData
        {
            public FileReference? m_GameObject { get; set; }
            public Vector3? m_LocalPosition { get; set; }
            public FileReference? m_Father { get; set; }
            public FileReference? m_PrefabInstance { get; set; }
        }

        public sealed class MonoBehaviourData
        {
            public FileReference? m_Script { get; set; }
            public double speed { get; set; }
            public string? label { get; set; }
        }

        public sealed class FileReference
        {
            public long fileID { get; set; }
            public string? guid { get; set; }
            public int? type { get; set; }
        }

        public sealed class Vector3
        {
            public double x { get; set; }
            public double y { get; set; }
            public double z { get; set; }
        }

        private static YamlSerializerOptions Models()
        {
            var options = new YamlSerializerOptions();
            options.TagMappings.Add("tag:unity3d.com,2011:1", typeof(GameObjectDocument));
            options.TagMappings.Add("tag:unity3d.com,2011:4", typeof(TransformDocument));
            options.TagMappings.Add("tag:unity3d.com,2011:114", typeof(MonoBehaviourDocument));
            return options;
        }

        private static Dictionary<long, (UnityObject? Object, bool Stripped)> ReadObjects(string name)
        {
            YamlSerializerOptions options = Models();
            var objects = new Dictionary<long, (UnityObject?, bool)>();
            var reader = new Utf8YamlReader(Load(name), s_read);
            while (reader.Read())
            {
                long fileID = long.Parse(Encoding.UTF8.GetString(reader.Anchor.ToArray()), Globalization.CultureInfo.InvariantCulture);
                bool stripped = reader.IsUnityStripped;
                string? tag = reader.GetExpandedTag();
                UnityObject? value = tag is not null && options.TagMappings.ContainsKey(tag) ? YamlSerializer.Deserialize<UnityObject>(ref reader, options) : null;
                if (value is null)
                {
                    reader.Skip();
                }

                objects[fileID] = (value, stripped);
            }

            return objects;
        }

        [Fact]
        public void TypedModels_ReadTheScene()
        {
            Dictionary<long, (UnityObject? Object, bool Stripped)> scene = ReadObjects("scene.unity");
            Assert.Equal(new long[] { 1, 1000, 1001, 1002, -4850089497005498858, 8243650411178512303 }, scene.Keys);

            GameObjectData player = Assert.IsType<GameObjectDocument>(scene[1000].Object).GameObject!;
            Assert.Equal("Player", player.m_Name);
            Assert.Equal(1, player.m_IsActive);
            Assert.Equal(new long[] { 1001, 1002 }, player.m_Component!.Select(c => c.component!.fileID));

            TransformData light = Assert.IsType<TransformDocument>(scene[8243650411178512303].Object).Transform!;
            Assert.Equal(-4850089497005498858, light.m_GameObject!.fileID);
            Assert.Equal(3, light.m_LocalPosition!.y);
            Assert.Equal(1001, light.m_Father!.fileID);

            MonoBehaviourData script = Assert.IsType<MonoBehaviourDocument>(scene[1002].Object).MonoBehaviour!;
            Assert.Equal("0123456789abcdef0123456789abcdef", script.m_Script!.guid);
            Assert.Equal(4.5, script.speed);
            Assert.Equal("n", script.label);

            // An object kind the models do not map is skipped.
            Assert.Null(scene[1].Object);
        }

        [Fact]
        public void TypedModels_SeeTheStrippedObject()
        {
            Dictionary<long, (UnityObject? Object, bool Stripped)> prefab = ReadObjects("prefab.prefab");
            Assert.True(prefab[400000].Stripped);
            Assert.Equal(100100000, Assert.IsType<TransformDocument>(prefab[400000].Object).Transform!.m_PrefabInstance!.fileID);
            Assert.False(prefab[500001].Stripped);
            Assert.Equal(400000, Assert.IsType<TransformDocument>(prefab[500001].Object).Transform!.m_Father!.fileID);
        }

        [Fact]
        public void TheBlob_ReadsAsTextIntoAString()
        {
            var reader = new Utf8YamlReader(Load("material.mat"), s_read);
            var texture = new List<YamlElement>();
            while (reader.Read())
            {
                texture.Add(YamlElement.ParseValue(ref reader));
            }

            YamlElement data = texture[1].GetProperty("Texture2D");
            Assert.Equal(20000, data.GetProperty("_typelessdata").GetString()!.Length);
            Assert.Equal("00000000", YamlSerializer.Deserialize<Dictionary<string, string>>("h: " + data.GetProperty("m_DataHash").GetRawText())!["h"]);
        }
    }
}
