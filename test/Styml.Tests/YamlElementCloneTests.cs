// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static class YamlElementCloneTests
    {
        [Fact]
        public static void CloneTwiceFromSameDocument()
        {
            string yaml = "[[]]";
            YamlElement root;
            YamlElement clone;
            YamlElement clone2;

            using (YamlDocument doc = YamlDocument.Parse(yaml))
            {
                root = doc.RootElement;
                clone = root.Clone();
                clone2 = root.Clone();

                Assert.Equal(yaml, clone.GetRawText());
                Assert.NotSame(doc, clone.SniffDocument());
                Assert.NotSame(doc, clone2.SniffDocument());

                Assert.False(clone.SniffDocument().IsDisposable());
                Assert.False(clone2.SniffDocument().IsDisposable());
            }

            // After document Dispose
            Assert.Equal(yaml, clone.GetRawText());
            Assert.Equal(yaml, clone2.GetRawText());
            Assert.NotSame(clone.SniffDocument(), clone2.SniffDocument());

            Assert.Throws<ObjectDisposedException>(() => root.GetRawText());
        }

        [Fact]
        public static void CloneInnerElementFromClonedElement()
        {
            YamlElement clone;

            using (YamlDocument doc = YamlDocument.Parse("[[[]]]"))
            {
                YamlElement middle = doc.RootElement[0].Clone();
                YamlElement inner = middle[0];
                clone = inner.Clone();

                Assert.Equal(inner.GetRawText(), clone.GetRawText());
                Assert.NotSame(doc, clone.SniffDocument());
                Assert.Same(middle.SniffDocument(), clone.SniffDocument());
                Assert.Same(inner.SniffDocument(), clone.SniffDocument());
                Assert.False(clone.SniffDocument().IsDisposable());
            }

            // After document Dispose
            Assert.Equal("[]", clone.GetRawText());
        }

        [Fact]
        public static void CloneAtInnerNumber()
        {
            CloneAtInner("1.21e9", YamlValueKind.Number);
        }

        [Fact]
        public static void CloneAtInnerString()
        {
            CloneAtInner("\"  this  string  has  \\u0039 spaces\"", YamlValueKind.String);
        }

        [Fact]
        public static void CloneAtInnerTrue()
        {
            CloneAtInner("true", YamlValueKind.True);
        }

        [Fact]
        public static void CloneAtInnerFalse()
        {
            CloneAtInner("false", YamlValueKind.False);
        }

        [Fact]
        public static void CloneAtInnerNull()
        {
            CloneAtInner("null", YamlValueKind.Null);
        }

        [Fact]
        public static void CloneAtInnerObject()
        {
            // Very weird whitespace is used here just to ensure that the
            // clone API isn't making any whitespace assumptions.
            CloneAtInner(
                """
                    {
                      "this":
                      [
                        {
                          "object": 0,




                          "has": [ "whitespace" ]
                        }
                      ]
                    }
                    """,
                YamlValueKind.Object);
        }

        [Fact]
        public static void CloneAtInnerArray()
        {
            // Very weird whitespace is used here just to ensure that the
            // clone API isn't making any whitespace assumptions.
            CloneAtInner(
                """
                    [
                    {
                      "this":
                      [
                        {
                          "object": 0,




                          "has": [ "whitespace" ]
                        }
                      ]
                    },

                    5

                    ,



                    false,



                    null
                    ]
                    """,
                YamlValueKind.Array);
        }

        private static void CloneAtInner(string innerYaml, YamlValueKind valueType)
        {
            string yaml = $"{{ \"obj\": [ {{ \"not target\": true, \"target\": {innerYaml} }}, 5 ] }}";

            YamlElement clone;

            using (YamlDocument doc = YamlDocument.Parse(yaml))
            {
                YamlElement target = doc.RootElement.GetProperty("obj")[0].GetProperty("target");
                Assert.Equal(valueType, target.ValueKind);
                clone = target.Clone();
            }

            Assert.Equal(innerYaml, clone.GetRawText());
        }

        internal static YamlDocument SniffDocument(this YamlElement element)
        {
            return (YamlDocument)typeof(YamlElement)
                .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(element);
        }

        internal static bool IsDisposable(this YamlDocument document)
        {
            return (bool)typeof(YamlDocument)
                .GetProperty("IsDisposable", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(document);
        }
    }
}
