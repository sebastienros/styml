// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Yaml.Serialization;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public class ImmutableCollectionsTests
    {
        private static readonly YamlSerializerOptions s_indentedOption = new YamlSerializerOptions { WriteIndented = true };

        #region List
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeList()
        {
            ImmutableList<string> data = ImmutableList.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data);
            Assert.Equal("""["One","II","3"]""", yaml);
        }

        [Fact]
        public void DeserializeList()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            ImmutableList<string> data = YamlSerializer.Deserialize<ImmutableList<string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Equal("One", data[0]);
            Assert.Equal("II", data[1]);
            Assert.Equal("3", data[2]);
        }

        [Fact]
        public void DeserializeListInterface()
        {
            string yaml = """
                [
                        "Volibear",
                        "Teemo",
                        "Katarina"
                      ]
                """;

            IImmutableList<string> champions = YamlSerializer.Deserialize<IImmutableList<string>>(yaml);

            Assert.Equal(3, champions.Count);
            Assert.Equal("Volibear", champions[0]);
            Assert.Equal("Teemo", champions[1]);
            Assert.Equal("Katarina", champions[2]);
        }
        #endregion

        #region Array
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeArray()
        {
            ImmutableArray<string> data = ImmutableArray.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data, s_indentedOption);
            Assert.Equal("""
                [
                  "One",
                  "II",
                  "3"
                ]
                """, yaml, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public void DeserializeArray()
        {
            string yaml = """
                [
                    "One",
                    "II",
                    "3"
                ]
                """;

            ImmutableArray<string> data = YamlSerializer.Deserialize<ImmutableArray<string>>(yaml);

            Assert.Equal(3, data.Length);
            Assert.Equal("One", data[0]);
            Assert.Equal("II", data[1]);
            Assert.Equal("3", data[2]);
        }

        [Fact]
        public void SerializeDefaultArray()
        {
            // The call to .GetEnumerator() throws this exception. Yaml.NET fails in the same way.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(default(ImmutableArray<int>), s_indentedOption));
        }
        #endregion

        #region Queue
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeQueue()
        {
            ImmutableQueue<string> data = ImmutableQueue.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data);
            Assert.Equal("""["One","II","3"]""", yaml);
        }

        [Fact]
        public void DeserializeQueue()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            ImmutableQueue<string> data = YamlSerializer.Deserialize<ImmutableQueue<string>>(yaml);

            Assert.False(data.IsEmpty);
            Assert.Equal("One", data.Peek());
            data = data.Dequeue();
            Assert.Equal("II", data.Peek());
            data = data.Dequeue();
            Assert.Equal("3", data.Peek());
        }

        [Fact]
        public void DeserializeQueueInterface()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            IImmutableQueue<string> data = YamlSerializer.Deserialize<IImmutableQueue<string>>(yaml);

            Assert.False(data.IsEmpty);
            Assert.Equal("One", data.Peek());
            data = data.Dequeue();
            Assert.Equal("II", data.Peek());
            data = data.Dequeue();
            Assert.Equal("3", data.Peek());
        }
        #endregion

        #region Stack
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeStack()
        {
            ImmutableStack<string> data = ImmutableStack.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data);
            Assert.Equal("""["3","II","One"]""", yaml);
        }

        [Fact]
        public void DeserializeStack()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            ImmutableStack<string> data = YamlSerializer.Deserialize<ImmutableStack<string>>(yaml);

            Assert.False(data.IsEmpty);
            Assert.Equal("3", data.Peek());
            data = data.Pop();
            Assert.Equal("II", data.Peek());
            data = data.Pop();
            Assert.Equal("One", data.Peek());
        }

        [Fact]
        public void DeserializeStackInterface()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            IImmutableStack<string> data = YamlSerializer.Deserialize<IImmutableStack<string>>(yaml);

            Assert.False(data.IsEmpty);
            Assert.Equal("3", data.Peek());
            data = data.Pop();
            Assert.Equal("II", data.Peek());
            data = data.Pop();
            Assert.Equal("One", data.Peek());
        }
        #endregion

        #region HashSet
        [Fact]
        public void SerializeHashSet()
        {
            ImmutableHashSet<string> data = ImmutableHashSet.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data, s_indentedOption);

            // Use ISet to disambiguate between ISet and IReadOnlySet overloads below
            ISet<string> a = YamlSerializer.Deserialize<ImmutableHashSet<string>>(yaml);
            Assert.Equal(3, a.Count);
            Assert.Contains("One", a);
            Assert.Contains("II", a);
            Assert.Contains("3", a);
        }

        [Fact]
        public void DeserializeHashSet()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            // Use ISet to disambiguate between ISet and IReadOnlySet overloads below
            ISet<string> data = YamlSerializer.Deserialize<ImmutableHashSet<string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Contains("3", data);
            Assert.Contains("II", data);
            Assert.Contains("One", data);
        }

        [Fact]
        public void DeserializeHashSetInterface()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            IImmutableSet<string> data = YamlSerializer.Deserialize<IImmutableSet<string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.True(data.Contains("3"));
            Assert.True(data.Contains("II"));
            Assert.True(data.Contains("One"));
        }

#if NET
        [Fact]
        public void DeserializeIReadOnlySetInterface()
        {
            string yaml = """
                [
                    "One",
                    "II",
                    "3"
                ]
                """;

            IReadOnlySet<string> data = YamlSerializer.Deserialize<IReadOnlySet<string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.True(data.Contains("3"));
            Assert.True(data.Contains("II"));
            Assert.True(data.Contains("One"));
        }
#endif
        #endregion

        #region SortedSet
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeSortedSet()
        {
            ImmutableSortedSet<string> data = ImmutableSortedSet.CreateRange(new List<string>
            {
                "One",
                "II",
                "3"
            });

            string yaml = YamlSerializer.Serialize(data);
            Assert.Equal("""["3","II","One"]""", yaml);
        }

        [Fact]
        public void DeserializeSortedSet()
        {
            string yaml = """
                [
                  "One",
                  "II",
                  "3"
                ]
                """;

            // Use ISet to disambiguate between ISet and IReadOnlySet overloads below
            ISet<string> data = YamlSerializer.Deserialize<ImmutableSortedSet<string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Contains("3", data);
            Assert.Contains("II", data);
            Assert.Contains("One", data);
        }
        #endregion

        #region Dictionary
        [Fact]
        public void SerializeDictionary()
        {
            ImmutableDictionary<int, string> data = ImmutableDictionary.CreateRange(new Dictionary<int, string>
            {
                { 1, "One" },
                { 2, "II" },
                { 3, "3" }
            });

            string yaml = YamlSerializer.Serialize(data, s_indentedOption);
            ImmutableDictionary<int, string> a = YamlSerializer.Deserialize<ImmutableDictionary<int, string>>(yaml);
            Assert.Equal(3, a.Count);
            Assert.Equal("One", (string)a[1]);
            Assert.Equal("II", (string)a[2]);
            Assert.Equal("3", (string)a[3]);
        }

        [Fact]
        public void DeserializeDictionary()
        {
            string yaml = """
                {
                  "1": "One",
                  "2": "II",
                  "3": "3"
                }
                """;

            ImmutableDictionary<int, string> data = YamlSerializer.Deserialize<ImmutableDictionary<int, string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Equal("One", data[1]);
            Assert.Equal("II", data[2]);
            Assert.Equal("3", data[3]);
        }

        [Fact]
        public void DeserializeDictionaryInterface()
        {
            string yaml = """
                {
                  "1": "One",
                  "2": "II",
                  "3": "3"
                }
                """;

            IImmutableDictionary<int, string> data = YamlSerializer.Deserialize<IImmutableDictionary<int, string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Equal("One", data[1]);
            Assert.Equal("II", data[2]);
            Assert.Equal("3", data[3]);
        }
        #endregion

        #region SortedDictionary
        [Fact]
        public void SerializeSortedDictionary()
        {
            ImmutableSortedDictionary<int, string> data = ImmutableSortedDictionary.CreateRange(new SortedDictionary<int, string>
            {
                { 1, "One" },
                { 2, "II" },
                { 3, "3" }
            });

            string yaml = YamlSerializer.Serialize(data, s_indentedOption);
            YamlTestHelper.AssertYamlEqual("""
                {
                  "1": "One",
                  "2": "II",
                  "3": "3"
                }
                """, yaml);
        }

        [Fact]
        public void DeserializeSortedDictionary()
        {
            string yaml = """
                {
                  "1": "One",
                  "2": "II",
                  "3": "3"
                }
                """;

            ImmutableSortedDictionary<int, string> data = YamlSerializer.Deserialize<ImmutableSortedDictionary<int, string>>(yaml);

            Assert.Equal(3, data.Count);
            Assert.Equal("One", data[1]);
            Assert.Equal("II", data[2]);
            Assert.Equal("3", data[3]);
        }
        #endregion
    }
}
