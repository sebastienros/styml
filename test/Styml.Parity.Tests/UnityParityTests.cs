// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// Unity's dialect against YamlDotNet's parser, SharpYaml's and VYaml's (which targets Unity): a %TAG declared once for
    /// a stream of documents, YAML 1.1's directive rule, and the stripped marker.
    /// </summary>
    public class UnityParityTests
    {
        private const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
        private const string TwoObjects = Header + "--- !u!1 &1000\nGameObject:\n  m_Name: Player\n--- !u!4 &1001\nTransform:\n  m_GameObject: {fileID: 1000}\n";
        private const string Stripped = Header + "--- !u!1001 &100100000\nPrefabInstance:\n  m: {}\n--- !u!4 &400000 stripped\nTransform:\n  m_PrefabInstance: {fileID: 100100000}\n";

        private static List<string> YamlDotNetTags(string yaml)
        {
            var parser = new YamlDotNet.Core.Parser(new StringReader(yaml));
            var tags = new List<string>();
            while (parser.MoveNext())
            {
                if (parser.Current is YamlDotNet.Core.Events.MappingStart { Tag.IsEmpty: false } start)
                {
                    tags.Add(start.Tag.Value);
                }
            }

            return tags;
        }

        private static List<string> SharpYamlTags(string yaml)
        {
            var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml));
            var tags = new List<string>();
            while (parser.MoveNext())
            {
                if (parser.Current is SharpYaml.Events.MappingStart { Tag: not null } start)
                {
                    tags.Add(start.Tag);
                }
            }

            return tags;
        }

        private static List<string> VYamlTags(string yaml, List<bool>? stripped = null)
        {
            var parser = VYaml.Parser.YamlParser.FromBytes(Encoding.UTF8.GetBytes(yaml));
            var tags = new List<string>();
            while (parser.Read())
            {
                if (parser.CurrentEventType == VYaml.Parser.ParseEventType.MappingStart && parser.TryGetCurrentTag(out VYaml.Parser.Tag tag))
                {
                    tags.Add(tag.ToString());
                    stripped?.Add(parser.UnityStrippedMark);
                }
            }

            return tags;
        }

        private static List<string?> StymlTags(string yaml, YamlReaderOptions options, List<bool>? stripped = null)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tags = new List<string?>();
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.StartObject && !reader.Tag.IsEmpty)
                {
                    tags.Add(reader.GetExpandedTag());
                    stripped?.Add(reader.IsUnityStripped);
                }
            }

            return tags;
        }

        [Fact]
        public void ATagDeclaredOnce_InALaterDocument()
        {
            // YamlDotNet keeps the handles when the stream declares %YAML 1.1 (YAML 1.1's scope), and resets them without
            // it or under %YAML 1.2; SharpYaml resets them (YAML 1.2); VYaml does not expand tags.
            Assert.ThrowsAny<Exception>(() => YamlDotNetTags(TwoObjects.Replace("%YAML 1.1\n", "")));
            Assert.ThrowsAny<Exception>(() => YamlDotNetTags(TwoObjects.Replace("%YAML 1.1", "%YAML 1.2")));
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, YamlDotNetTags(TwoObjects));
            Assert.ThrowsAny<Exception>(() => SharpYamlTags(TwoObjects));
            Assert.Equal(new[] { "!u!1", "!u!4" }, VYamlTags(TwoObjects));

            // Styml: as YamlDotNet by default (the handles kept under %YAML 1.1, reset without it or under %YAML 1.2), and
            // whatever the version under InheritTagDirectives.
            var defaults = new YamlReaderOptions { AllowMultipleValues = true };
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" }, StymlTags(TwoObjects, defaults));
            Assert.ThrowsAny<YamlException>(() => StymlTags(TwoObjects.Replace("%YAML 1.1\n", ""), defaults));
            Assert.ThrowsAny<YamlException>(() => StymlTags(TwoObjects.Replace("%YAML 1.1", "%YAML 1.2"), defaults));
            Assert.Equal(new[] { "tag:unity3d.com,2011:1", "tag:unity3d.com,2011:4" },
                StymlTags(TwoObjects.Replace("%YAML 1.1\n", ""), new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = true }));
        }

        [Fact]
        public void ADocumentWithDirectives_StartsFromItsOwn()
        {
            // YAML 1.1 section 7.4: the second document declares !a! again and loses !b!; the third inherits the second's.
            // (YamlDotNet applies it under %YAML 1.1 only.)
            const string yaml = "%YAML 1.1\n%TAG !a! tag:a,1:\n%TAG !b! tag:b,1:\n--- !a!x\nA: 1\n...\n%TAG !a! tag:a2,1:\n--- !a!y\nB: 2\n--- !a!z\nC: 3\n";
            var on = new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = true };
            Assert.Equal(new[] { "tag:a,1:x", "tag:a2,1:y", "tag:a2,1:z" }, YamlDotNetTags(yaml));
            Assert.Equal(new[] { "tag:a,1:x", "tag:a2,1:y", "tag:a2,1:z" }, StymlTags(yaml, on));

            string usesB = yaml.Replace("!a!y", "!b!y");
            Assert.ThrowsAny<Exception>(() => YamlDotNetTags(usesB));
            Assert.ThrowsAny<YamlException>(() => StymlTags(usesB, on));
        }

        [Theory]
        // YamlDotNet's parser and Styml's reader, with default options. Where YamlDotNet refuses a second %YAML directive
        // (it treats the version as the stream's: "Found duplicate %YAML directive"), Styml reads each document's, as the
        // specification allows; a later %YAML 1.2 ends YAML 1.1's scope.
        //           stream                                                                                       YamlDotNet                                  Styml
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n...\n--- !a!z 3\n", "tag:a,1:x tag:a,1:y tag:a,1:z", "tag:a,1:x tag:a,1:y tag:a,1:z")]
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%TAG !b! tag:b,1:\n--- !b!y 2\n--- !b!z 3\n", "tag:a,1:x tag:b,1:y tag:b,1:z", "tag:a,1:x tag:b,1:y tag:b,1:z")]
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%TAG !b! tag:b,1:\n--- !a!y 2\n", null, null)]
        [InlineData("%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.1\n%TAG !a! tag:a2,1:\n--- !a!y 2\n--- !a!z 3\n", "tag:a,1:x tag:a2,1:y tag:a2,1:z", "tag:a,1:x tag:a2,1:y tag:a2,1:z")]
        [InlineData("%YAML 1.0\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n", "tag:a,1:x tag:a,1:y", "tag:a,1:x tag:a,1:y")]
        [InlineData("%YAML 1.3\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n", null, null)]
        [InlineData("%YAML 1.2\n%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n", null, null)]
        [InlineData("%TAG !a! tag:a,1:\n--- !a!x 1\n--- !a!y 2\n", null, null)]
        // A later %YAML 1.1 alone is a directive: that document starts its own table (rule (a)), so !a! is undeclared there.
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.1\n--- !a!y 2\n", null, null)]
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!y 2\n--- !a!z 3\n", null, "tag:a,1:x tag:a,1:y tag:a,1:z")]
        [InlineData("%YAML 1.1\n%TAG !a! tag:a,1:\n--- !a!x 1\n...\n%YAML 1.2\n--- !a!y 2\n", null, null)]
        public void TheVersionsDirectiveScope(string yaml, string? yamlDotNet, string? styml)
        {
            Assert.Equal(yamlDotNet, Scalars(() => YamlDotNetScalarTags(yaml)));
            Assert.Equal(styml, Scalars(() => StymlScalarTags(yaml)));
        }

        private static string? Scalars(Func<List<string?>> read)
        {
            try
            {
                return string.Join(" ", read());
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<string?> YamlDotNetScalarTags(string yaml)
        {
            var parser = new YamlDotNet.Core.Parser(new StringReader(yaml));
            var tags = new List<string?>();
            while (parser.MoveNext())
            {
                if (parser.Current is YamlDotNet.Core.Events.Scalar { Tag.IsEmpty: false } scalar)
                {
                    tags.Add(scalar.Tag.Value);
                }
            }

            return tags;
        }

        private static List<string?> StymlScalarTags(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { AllowMultipleValues = true });
            var tags = new List<string?>();
            while (reader.Read())
            {
                if (!reader.Tag.IsEmpty)
                {
                    tags.Add(reader.GetExpandedTag());
                }
            }

            return tags;
        }

        [Fact]
        public void TheStrippedMarker()
        {
            // YamlDotNet and SharpYaml refuse it; VYaml accepts it, always, and marks the root (UnityStrippedMark).
            Assert.ThrowsAny<Exception>(() => YamlDotNetTags(Stripped));
            Assert.ThrowsAny<Exception>(() => SharpYamlTags(Stripped));
            var vyaml = new List<bool>();
            Assert.Equal(new[] { "!u!1001", "!u!4" }, VYamlTags(Stripped, vyaml));
            Assert.Equal(new[] { false, true }, vyaml);

            // Styml: refused by default; under AcceptUnityStrippedMarker, IsUnityStripped on the same root.
            Assert.ThrowsAny<YamlException>(() => StymlTags(Stripped, new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = true }));
            var styml = new List<bool>();
            StymlTags(Stripped, new YamlReaderOptions { AllowMultipleValues = true, InheritTagDirectives = true, AcceptUnityStrippedMarker = true }, styml);
            Assert.Equal(vyaml, styml);
        }

        [Theory]
        // Both take the marker in lower case, at the end of the line, after a tag: VYaml after a !u! tag only, Styml after any.
        [InlineData("--- !u!4 &1 stripped\nA: 1\n", true, true)]
        [InlineData("--- !t &1 stripped\nA: 1\n", false, true)]
        [InlineData("--- &1 stripped\nA: 1\n", false, false)]
        [InlineData("--- !u!4 &1 Stripped\nA: 1\n", false, false)]
        [InlineData("--- stripped\nA: 1\n", false, false)]
        public void TheStrippedMarkersPlace(string yaml, bool vyamlAccepts, bool stymlAccepts)
        {
            Assert.Equal(vyamlAccepts, Accepts(() => VYamlTags(yaml)));
            Assert.Equal(stymlAccepts, Accepts(() => StymlTags(Header + yaml, new YamlReaderOptions { AllowMultipleValues = true, AcceptUnityStrippedMarker = true })));
        }

        private static bool Accepts(Action read)
        {
            try
            {
                read();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
