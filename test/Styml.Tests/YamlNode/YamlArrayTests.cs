// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class YamlArrayTests
    {
        [Fact]
        public static void ParamsContructors()
        {
            YamlArray expectedArray = [41, 42, 43];
            YamlNodeOptions options = new YamlNodeOptions { PropertyNameCaseInsensitive = true };

            Verify(new YamlArray(new YamlNode[] { 41, 42, 43 }), null);
            Verify(new YamlArray((ReadOnlySpan<YamlNode>)new YamlNode[] { 41, 42, 43 }), null);
            Verify(new YamlArray(options, new YamlNode[] { 41, 42, 43 }), options);
            Verify(new YamlArray(options, (ReadOnlySpan<YamlNode>)new YamlNode[] { 41, 42, 43 }), options);

            void Verify(YamlArray yamlArray, YamlNodeOptions? expectedOptions)
            {
                Assert.Equal(expectedArray.Count, yamlArray.Count);
                YamlNodeTests.AssertDeepEqual(expectedArray, yamlArray);
                Assert.Equal(expectedOptions, yamlArray.Options);
            }
        }

        [Fact]
        public static void FromElement()
        {
            using (YamlDocument document = YamlDocument.Parse("[42]"))
            {
                YamlArray jArray = YamlArray.Create(document.RootElement);
                Assert.Equal(42, jArray[0].GetValue<int>());
            }

            using (YamlDocument document = YamlDocument.Parse("null"))
            {
                YamlArray jArray = YamlArray.Create(document.RootElement);
                Assert.Null(jArray);
            }
        }

        [Theory]
        [InlineData("42")]
        [InlineData("{}")]
        public static void FromElement_WrongNodeTypeThrows(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
                Assert.Throws<InvalidOperationException>(() => YamlArray.Create(document.RootElement));
        }

        [Fact]
        public static void WriteTo_Validation()
        {
            Assert.Throws<ArgumentNullException>(() => new YamlArray().WriteTo(null));
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("[42]")]
        [InlineData("[42,43]")]
        [Trait("category", "jsonoutput")]
        public static void WriteTo(string yaml)
        {
            YamlArray jArray = YamlNode.Parse(yaml).AsArray();
            var stream = new MemoryStream();
            var writer = new Utf8YamlWriter(stream);
            jArray.WriteTo(writer);
            writer.Flush();

            string result = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(yaml, result);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteTo_Options()
        {
            YamlArray jArray = new YamlArray(42);

            // Baseline.
            var stream = new MemoryStream();
            var writer = new Utf8YamlWriter(stream);
            jArray.WriteTo(writer);
            writer.Flush();
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("[42]", yaml);

            var options = new YamlSerializerOptions
            {
                NumberHandling = Serialization.YamlNumberHandling.WriteAsString
            };

            // With options.
            stream = new MemoryStream();
            writer = new Utf8YamlWriter(stream);
            jArray.WriteTo(writer, options);
            writer.Flush();
            yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("[\"42\"]", yaml);
        }

        [Fact]
        public static void Clear()
        {
            var jArray = new YamlArray(42, 43);
            Assert.Equal(2, jArray.Count);

            YamlNode node = jArray[0];
            jArray.Clear();
            Assert.Equal(0, jArray.Count);
            jArray.Add(node);
            Assert.Equal(1, jArray.Count);
        }

        [Fact]
        public static void AddOverloads()
        {
            var jArray = new YamlArray();
            jArray.Add((object)null);
            jArray.Add((int?)null);
            jArray.Add(1);
            Assert.Equal(3, jArray.Count);
        }

        [Fact]
        public static void IsReadOnly()
        {
            Assert.False(((IList<YamlNode>)new YamlArray()).IsReadOnly);
        }

        [Fact]
        public static void IEnumerable()
        {
            IEnumerable jArray = new YamlArray(1, 2);

            int count = 0;
            foreach (YamlNode? node in jArray)
            {
                count++;
            }

            Assert.Equal(2, count);
        }

        [Fact]
        public static void Contains_IndexOf_Remove_Insert()
        {
            YamlNode node1 = 1;
            YamlNode node2 = 2;
            YamlNode node3 = 3;

            var jArray = new YamlArray(node1, node2, node3);
            Assert.Equal(3, jArray.Count);

            Assert.Equal(0, jArray.IndexOf(node1));
            Assert.Equal(1, jArray.IndexOf(node2));
            Assert.Equal(2, jArray.IndexOf(node3));

            // Remove
            bool success = jArray.Remove(node2);
            Assert.True(success);
            Assert.Equal(2, jArray.Count);

            Assert.Equal(0, jArray.IndexOf(node1));
            Assert.Equal(-1, jArray.IndexOf(node2));
            Assert.Equal(1, jArray.IndexOf(node3));

            Assert.False(jArray.Remove(node2)); // remove an already removed node.
            Assert.Equal(2, jArray.Count);

            // Contains
            Assert.True(jArray.Contains(node1));
            Assert.False(jArray.Contains(node2));
            Assert.True(jArray.Contains(node3));

            // Insert
            jArray.Insert(1, node2);
            Assert.Equal(3, jArray.Count);

            Assert.Equal(0, jArray.IndexOf(node1));
            Assert.Equal(1, jArray.IndexOf(node2));
            Assert.Equal(2, jArray.IndexOf(node3));
        }

        [Fact]
        public static void CopyTo()
        {
            YamlNode node1 = 1;
            YamlNode node2 = 2;

            IList<YamlNode?> jArray = new YamlArray(node1, node2, null);
            var arr = new YamlNode[4];
            jArray.CopyTo(arr, 0);

            Assert.Same(node1, arr[0]);
            Assert.Same(node2, arr[1]);
            Assert.Null(arr[2]);

            arr = new YamlNode[5];
            jArray.CopyTo(arr, 1);
            Assert.Null(arr[0]);
            Assert.Same(node1, arr[1]);
            Assert.Same(node2, arr[2]);
            Assert.Null(arr[3]);
            Assert.Null(arr[4]);

            arr = new YamlNode[3];
            Assert.Throws<ArgumentException>(() => jArray.CopyTo(arr, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => jArray.CopyTo(arr, -1));
        }

        [Fact]
        public static void ConvertJSONArrayToIListOfYamlNode()
        {
            YamlArray obj = YamlSerializer.Deserialize<YamlArray>("[42]");
            Assert.Equal(42, (int)obj[0]);

            IList<YamlNode> ilist = obj;
            Assert.NotNull(ilist);
            Assert.Equal(42, (int)ilist[0]);
        }

        [Fact]
        public static void ConvertJSONArrayToYamlArray()
        {
            YamlArray nodes = YamlSerializer.Deserialize<YamlArray>("[1,1.1,\"Hello\"]");
            Assert.Equal(1, (long)nodes[0]);
            Assert.Equal(1.1, (double)nodes[1]);
            Assert.Equal("Hello", (string)nodes[2]);
        }

        [Fact]
        public static void ConvertJSONArrayToYamlNodeArray()
        {
            // Instead of YamlArray, use array of YamlNodes
            YamlNode[] nodes = YamlSerializer.Deserialize<YamlNode[]>("[1,1.1,\"Hello\"]");
            Assert.Equal(1, (long)nodes[0]);
            Assert.Equal(1.1, (double)nodes[1]);
            Assert.Equal("Hello", (string)nodes[2]);
        }

        [Fact]
        public static void ConvertJSONArrayToObjectArray()
        {
            // Instead of YamlArray, use array of objects
            YamlSerializerOptions options = new();
            options.UnknownTypeHandling = Serialization.YamlUnknownTypeHandling.YamlNode;
            object[] nodes = YamlSerializer.Deserialize<object[]>("[1,1.1,\"Hello\"]", options);
            Assert.Equal(1, (long)(YamlNode)nodes[0]);
            Assert.Equal(1.1, (double)(YamlNode)nodes[1]);
            Assert.Equal("Hello", (string)(YamlNode)nodes[2]);
        }

        [Fact]
        public static void ReAddSameNode_Throws()
        {
            var jValue = YamlValue.Create(1);

            var jArray = new YamlArray(jValue);
            Assert.Throws<InvalidOperationException>(() => jArray.Add(jValue));
        }

        [Fact]
        public static void ReAddRemovedNode()
        {
            var jValue = YamlValue.Create(1);

            var jArray = new YamlArray(jValue);
            Assert.Equal(1, jArray.Count);
            jArray.Remove(jValue);
            Assert.Equal(0, jArray.Count);
            jArray.Add(jValue);
            Assert.Equal(1, jArray.Count);
        }

        [Fact]
        public static void CreatingYamlArrayFromNodeArray()
        {
            YamlNode[] expected = { "sushi", "pasta", "cucumber soup" };

            var dishesYamlArray = new YamlArray(expected);
            Assert.Equal(3, dishesYamlArray.Count);

            for (int i = 0; i < dishesYamlArray.Count; i++)
            {
                Assert.Equal(expected[i], dishesYamlArray[i]);
            }
        }

        [Fact]
        public static void CreatingYamlArrayFromArrayOfStrings()
        {
            var strangeWords = new string[]
            {
                "supercalifragilisticexpialidocious",
                "gladiolus",
                "albumen",
                "smaragdine",
            };

            var strangeWordsYamlArray = new YamlArray();
            strangeWords.Where(word => word.Length < 10).
                ToList().ForEach(str => strangeWordsYamlArray.Add(YamlValue.Create(str)));

            Assert.Equal(2, strangeWordsYamlArray.Count);

            string[] expected = { "gladiolus", "albumen" };

            for (int i = 0; i < strangeWordsYamlArray.Count; i++)
            {
                Assert.Equal(expected[i], strangeWordsYamlArray[i].GetValue<string>());
            }
        }

        [Fact]
        public static void CreatingNestedYamlArray()
        {
            var vertices = new YamlArray()
            {
                new YamlArray
                {
                    new YamlArray
                    {
                        new YamlArray { 0, 0, 0 },
                        new YamlArray { 0, 0, 1 }
                    },
                    new YamlArray
                    {
                        new YamlArray { 0, 1, 0 },
                        new YamlArray { 0, 1, 1 }
                    }
                },
                new YamlArray
                {
                    new YamlArray
                    {
                        new YamlArray { 1, 0, 0 },
                        new YamlArray { 1, 0, 1 }
                    },
                    new YamlArray
                    {
                        new YamlArray { 1, 1, 0 },
                        new YamlArray { 1, 1, 1 }
                    }
                },
            };

            var jArray = (YamlArray)vertices[0];
            Assert.Equal(2, jArray.Count());
            jArray = (YamlArray)jArray[1];
            Assert.Equal(2, jArray.Count());
            jArray = (YamlArray)jArray[0];
            Assert.Equal(3, jArray.Count());

            Assert.Equal(0, (int)jArray[0]);
            Assert.Equal(1, (int)jArray[1]);
            Assert.Equal(0, (int)jArray[2]);
        }

        [Fact]
        public static void NullHandling()
        {
            var jArray = new YamlArray() { "to be replaced" };

            jArray[0] = null;
            Assert.Equal(1, jArray.Count);
            Assert.Null(jArray[0]);

            jArray.Add(null);
            Assert.Equal(2, jArray.Count);
            Assert.Null(jArray[1]);

            jArray.Add(null);
            Assert.Equal(3, jArray.Count);
            Assert.Null(jArray[2]);

            jArray.Insert(3, null);
            Assert.Equal(4, jArray.Count);
            Assert.Null(jArray[3]);

            jArray.Insert(4, null);
            Assert.Equal(5, jArray.Count);
            Assert.Null(jArray[4]);

            Assert.True(jArray.Contains(null));
            Assert.Equal(0, jArray.IndexOf(null));

            jArray.Remove(null);
            Assert.Equal(4, jArray.Count);
        }

        [Fact]
        public static void AccessingNestedYamlArray()
        {
            var issues = new YamlObject
            {
                { "features", new YamlArray { "new functionality 1", "new functionality 2" } },
                { "bugs", new YamlArray { "bug 123", "bug 4566", "bug 821" } },
                { "tests", new YamlArray { "code coverage" } },
            };

            issues["bugs"].AsArray().Add("bug 12356");
            issues["features"].AsArray()[0] = "feature 1569";
            issues["features"].AsArray()[1] = "feature 56134";

            Assert.Equal("bug 12356", (string)issues["bugs"][3]);
            Assert.Equal("feature 1569", (string)issues["features"][0]);
            Assert.Equal("feature 56134", (string)issues["features"][1]);
        }

        [Fact]
        public static void Insert()
        {
            var jArray = new YamlArray() { 1 };
            Assert.Equal(1, jArray.Count);

            jArray.Insert(0, 0);

            Assert.Equal(2, jArray.Count);
            Assert.Equal(0, (int)jArray[0]);
            Assert.Equal(1, (int)jArray[1]);

            jArray.Insert(2, 3);

            Assert.Equal(3, jArray.Count);
            Assert.Equal(0, (int)jArray[0]);
            Assert.Equal(1, (int)jArray[1]);
            Assert.Equal(3, (int)jArray[2]);

            jArray.Insert(2, 2);

            Assert.Equal(4, jArray.Count);
            Assert.Equal(0, (int)jArray[0]);
            Assert.Equal(1, (int)jArray[1]);
            Assert.Equal(2, (int)jArray[2]);
            Assert.Equal(3, (int)jArray[3]);
        }

        [Fact]
        public static void HeterogeneousArray()
        {
            var mixedTypesArray = new YamlArray { 1, "value", true, null, 2.3, new YamlObject() };

            Assert.Equal(1, mixedTypesArray[0].GetValue<int>());
            Assert.Equal("value", mixedTypesArray[1].GetValue<string>());
            Assert.True(mixedTypesArray[2].GetValue<bool>());
            Assert.Null(mixedTypesArray[3]);
            Assert.Equal(2.3, mixedTypesArray[4].GetValue<double>());
            Assert.IsType<YamlObject>(mixedTypesArray[5]);

            mixedTypesArray.Add(false);
            mixedTypesArray.Insert(4, "another");
            mixedTypesArray.Add(null);

            Assert.False(mixedTypesArray[7].GetValue<bool>());
            Assert.Equal("another", mixedTypesArray[4].GetValue<string>());
            Assert.Null(mixedTypesArray[8]);
        }

        [Fact]
        public static void OutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlArray()[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlArray()[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlArray()[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var jArray = new YamlArray { 1, 2, 3 };
                jArray.Insert(4, 17);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var jArray = new YamlArray { 1, 2, 3 };
                jArray.Insert(-1, 17);
            });
        }

        [Fact]
        public static void GetYamlArrayIEnumerable()
        {
            IEnumerable jArray = new YamlArray() { 1, "value" };
            IEnumerator jArrayEnumerator = jArray.GetEnumerator();

            Assert.True(jArrayEnumerator.MoveNext());
            Assert.Equal(1, ((YamlValue)jArrayEnumerator.Current).GetValue<int>());
            Assert.True(jArrayEnumerator.MoveNext());
            Assert.Equal("value", ((YamlValue)jArrayEnumerator.Current).GetValue<string>());
        }

        [Fact]
        public static void LazyInitializationIsThreadSafe()
        {
            string arrayText = "[\"elem0\",\"elem1\"]";
            YamlArray node = Assert.IsType<YamlArray>(YamlNode.Parse(arrayText));
            Parallel.For(0, 128, i =>
            {
                Assert.Equal("elem0", (string)node[0]);
                Assert.Equal("elem1", (string)node[1]);
            });
        }

        [Fact]
        public static void DeepClone()
        {
            var nestedArray = new YamlArray("elem0", "elem1");
            var nestedYamlObj = new YamlObject()
            {
                { "Nine", 9 },
                { "Name", "def"}
            };

            var array = new YamlArray();
            array.Add(10);
            array.Add("abcd");
            array.Add(null);
            array.Add(true);
            array.Add(false);
            array.Add(YamlValue.Create(30));
            array.Add(nestedYamlObj);
            array.Add(nestedArray);

            YamlArray clonedArray = array.DeepClone().AsArray();

            YamlNodeTests.AssertDeepEqual(array, clonedArray);
            Assert.Equal(array.Count, clonedArray.Count);
            Assert.Equal(10, array[0].GetValue<int>());
            Assert.Equal("abcd", array[1].GetValue<string>());
            Assert.Null(array[2]);
            Assert.True(array[3].GetValue<bool>());
            Assert.False(array[4].GetValue<bool>());
            Assert.Equal(30, array[5].GetValue<int>());

            YamlObject clonedNestedJObject = array[6].AsObject();
            Assert.Equal(nestedYamlObj.Count, clonedNestedJObject.Count);
            Assert.Equal(9, clonedNestedJObject["Nine"].GetValue<int>());
            Assert.Equal("def", clonedNestedJObject["Name"].GetValue<string>());

            YamlArray clonedNestedArray = clonedArray[7].AsArray();
            Assert.Equal(nestedArray.Count, clonedNestedArray.Count);
            Assert.Equal("elem0", clonedNestedArray[0].GetValue<string>());
            Assert.Equal("elem1", clonedNestedArray[1].GetValue<string>());

            string originalYaml = array.ToYamlString();
            string clonedYaml = clonedArray.ToYamlString();

            Assert.Equal(originalYaml, clonedYaml);
        }

        [Fact]
        public static void DeepCloneFromElement()
        {
            YamlDocument document = YamlDocument.Parse("[\"abc\", 10]");
            YamlArray jArray = YamlArray.Create(document.RootElement);
            var clone = jArray.DeepClone().AsArray();

            YamlNodeTests.AssertDeepEqual(jArray, clone);
            Assert.Equal(10, clone[1].GetValue<int>());
            Assert.Equal("abc", clone[0].GetValue<string>());
        }

        [Fact]
        public static void DeepEquals()
        {
            var array = new YamlArray() { null, 10, "str" };
            var sameArray = new YamlArray() { null, 10, "str" };

            YamlNodeTests.AssertDeepEqual(array, sameArray);
            YamlNodeTests.AssertNotDeepEqual(array, null);

            var diffArray = new YamlArray() { null, 10, "s" };
            YamlNodeTests.AssertNotDeepEqual(array, diffArray);

            diffArray = new YamlArray() { null, 10 };
            YamlNodeTests.AssertNotDeepEqual(array, diffArray);
        }

        [Fact]
        public static void DeepEqualsWithYamlValueArrayType()
        {
            var array = new YamlArray();
            array.Add(YamlValue.Create(2));
            array.Add(YamlValue.Create(3));
            array.Add(YamlValue.Create(4));
            var value = YamlValue.Create(new long[] { 2, 3, 4 });

            YamlNodeTests.AssertDeepEqual(array, value);
        }

        [Fact]
        public static void DeepEqualsFromElement()
        {
            using YamlDocument document = YamlDocument.Parse("[1, 2, 4]");
            YamlArray array = YamlArray.Create(document.RootElement);

            using YamlDocument document2 = YamlDocument.Parse("[1, 2,    4]");
            YamlArray array2 = YamlArray.Create(document2.RootElement);
            YamlNodeTests.AssertDeepEqual(array, array2);

            using YamlDocument document3 = YamlDocument.Parse("[2, 1, 4]");
            YamlArray array3 = YamlArray.Create(document3.RootElement);
            YamlNodeTests.AssertNotDeepEqual(array, array3);
        }

        [Fact]
        public static void UpdateClonedObjectNotAffectOriginal()
        {
            var jArray = new YamlArray(10, 20);

            var clone = jArray.DeepClone().AsArray();
            clone[1] = 3;

            Assert.Equal(20, jArray[1].GetValue<int>());
        }

        [Fact]
        public static void GetValueKind()
        {
            Assert.Equal(YamlValueKind.Array, new YamlArray().GetValueKind());
        }

        [Fact]
        public static void GetElementIndex()
        {
            var trueValue = YamlValue.Create(true);
            var falseValue = YamlValue.Create(false);
            var numberValue = YamlValue.Create(15);
            var stringValue = YamlValue.Create("ssss");
            var nestedObject = new YamlObject();
            var nestedArray = new YamlArray();

            var array = new YamlArray();
            array.Add(trueValue);
            array.Add(falseValue);
            array.Add(numberValue);
            array.Add(stringValue);
            array.Add(nestedObject);
            array.Add(nestedArray);

            Assert.Equal(0, trueValue.GetElementIndex());
            Assert.Equal(1, falseValue.GetElementIndex());
            Assert.Equal(2, numberValue.GetElementIndex());
            Assert.Equal(3, stringValue.GetElementIndex());
            Assert.Equal(4, nestedObject.GetElementIndex());
            Assert.Equal(5, nestedArray.GetElementIndex());
        }

        [Fact]
        public static void GetValues_ValueType()
        {
            YamlArray yamlArray = new YamlArray(1, 2, 3, 2);

            IEnumerable<int> values = yamlArray.GetValues<int>();

            Assert.Equal(yamlArray.Count, values.Count());
            Assert.Equal(1, values.ElementAt(0));
            Assert.Equal(2, values.ElementAt(1));
            Assert.Equal(3, values.ElementAt(2));
            Assert.Equal(2, values.ElementAt(3));

            yamlArray = new YamlArray(1, null);
            Assert.Throws<NullReferenceException>(() => yamlArray.GetValues<int>().Count());
        }

        [Fact]
        public static void GetValues_ReferenceType()
        {
            var student1 = new Student();
            var student2 = new Student();
            YamlArray yamlArray = new YamlArray(YamlValue.Create(student1), null, YamlValue.Create(student2));

            IEnumerable<Student> values = yamlArray.GetValues<Student>();

            Assert.Equal(yamlArray.Count, values.Count());
            Assert.Equal(student1, values.ElementAt(0));
            Assert.Null(values.ElementAt(1));
            Assert.Equal(student2, values.ElementAt(2));
        }

        private class Student
        {
            public string Name { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReplaceWith()
        {
            var jArray = new YamlArray();
            var jValue = YamlValue.Create(10);
            jArray.Add(jValue);
            jArray[0].ReplaceWith(5);

            Assert.Null(jValue.Parent);
            Assert.Equal("[5]", jArray.ToYamlString());
        }

        [Theory]
        [InlineData("null")]
        [InlineData("1")]
        [InlineData("false")]
        [InlineData("\"str\"")]
        [InlineData("""{"test":"hello world"}""")]
        [InlineData("[1,2,3]")]
        [Trait("category", "jsonoutput")]
        public static void AddYamlElement(string yaml)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/94842
            using var jdoc = YamlDocument.Parse(yaml);
            var array = new YamlArray();

            array.Add(jdoc.RootElement);

            YamlNode arrayElement = Assert.Single(array);
            switch (jdoc.RootElement.ValueKind)
            {
                case YamlValueKind.Object:
                    Assert.IsAssignableFrom<YamlObject>(arrayElement);
                    break;
                case YamlValueKind.Array:
                    Assert.IsAssignableFrom<YamlArray>(arrayElement);
                    break;
                case YamlValueKind.Null:
                    Assert.Null(arrayElement);
                    break;
                default:
                    Assert.IsAssignableFrom<YamlValue>(arrayElement);
                    break;
            }
            Assert.Equal($"[{yaml}]", array.ToYamlString());
        }

        [Theory]
        [InlineData("null")]
        [InlineData("1")]
        [InlineData("false")]
        [InlineData("\"str\"")]
        [InlineData("""{"test":"hello world"}""")]
        [InlineData("[1,2,3]")]
        [Trait("category", "jsonoutput")]
        public static void ReplaceWithYamlElement(string yaml)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/94842
            using var jdoc = YamlDocument.Parse(yaml);
            var array = new YamlArray { 1 };

            array[0].ReplaceWith(jdoc.RootElement);

            YamlNode arrayElement = Assert.Single(array);
            switch (jdoc.RootElement.ValueKind)
            {
                case YamlValueKind.Object:
                    Assert.IsAssignableFrom<YamlObject>(arrayElement);
                    break;
                case YamlValueKind.Array:
                    Assert.IsAssignableFrom<YamlArray>(arrayElement);
                    break;
                case YamlValueKind.Null:
                    Assert.Null(arrayElement);
                    break;
                default:
                    Assert.IsAssignableFrom<YamlValue>(arrayElement);
                    break;
            }

            Assert.Equal($"[{yaml}]", array.ToYamlString());
        }

        [Fact]
        public static void RemoveAll_InvalidValuesAndEmptyArray()
        {
            YamlArray testArray = new();
            Assert.Throws<ArgumentNullException>(() => testArray.RemoveAll(null!));
            testArray.RemoveAll(whatever => true); // RemoveAll on empty array always succeeds.
        }

        [Fact]
        public static void RemoveAll_FreshlyDeserialized()
        {
            YamlArray testArray = YamlSerializer.Deserialize<YamlArray>("[1,2,3,4,5]");
            Assert.Equal(3, testArray.RemoveAll(val => val.GetValue<int>() % 2 == 1));
            YamlNodeTests.AssertDeepEqual(new YamlArray { 2, 4 }, testArray);
        }

        [Theory]
        [InlineData(new int[] { 1, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1, 1 }, 0)]
        [InlineData(new int[] { 2, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1 }, 1)]
        [InlineData(new int[] { 1, 1, 1, 1, 2 }, new int[] { 1, 1, 1, 1 }, 1)]
        [InlineData(new int[] { 1, 1, 1, 2, 1 }, new int[] { 1, 1, 1, 1 }, 1)]
        public static void RemoveAll_SpecialValues(int[] original, int[] expected, int removed)
        {
            // This test is to ensure the O(n) scan algorithm of YamlArray.RemoveAll is correct.

            YamlArray testArray = new(ToNodes(original));
            Assert.Equal(removed, testArray.RemoveAll(Filter));
            YamlNodeTests.AssertDeepEqual(new YamlArray(ToNodes(expected)), testArray);

            bool Filter(YamlNode? v) => v.GetValue<int>() > 1;
            YamlNode?[] ToNodes(int[] values) => values.Select(v => YamlValue.Create(v)).ToArray();
        }

        [Fact]
        public static void RemoveAll()
        {
            YamlArray testArray = [1, 2, 3, 4, 5, 7, 8, 8, 8];
            YamlNode[] nodes = testArray.ToArray();

            // Correct amount of nodes are removed / remaining
            Assert.Equal(5, testArray.RemoveAll(Filter));
            Assert.Equal(4, testArray.Count);

            // The order is preserved
            YamlNodeTests.AssertDeepEqual(new YamlArray { 1, 3, 5, 7 }, testArray);

            // Node parents are correctly handled
            foreach (YamlNode node in nodes)
            {
                if (Filter(node))
                {
                    Assert.Null(node.Parent);
                }
                else
                {
                    Assert.Same(testArray, node.Parent);
                }
            }

            static bool Filter(YamlNode node) => node.GetValue<int>() % 2 == 0;
        }

        [Fact]
        public static void RemoveRange_InvalidAndSpecialValues()
        {
            YamlArray emptyArray = new();
            emptyArray.RemoveRange(0, 0);
            Assert.Throws<ArgumentException>(() => emptyArray.RemoveRange(0, 1));
            Assert.Throws<ArgumentException>(() => emptyArray.RemoveRange(1, 0));

            YamlArray testArray = [1, 2, 3, 4, 5, 6, 7, 8];
            Assert.Throws<ArgumentOutOfRangeException>(() => testArray.RemoveRange(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => testArray.RemoveRange(1, -1));
            Assert.Throws<ArgumentException>(() => testArray.RemoveRange(10, 1));
            Assert.Throws<ArgumentException>(() => testArray.RemoveRange(1, 10));
        }

        [Fact]
        public static void RemoveRange()
        {
            YamlArray testArray = [1, 2, 3, 4, 5, 6, 7, 8];
            YamlNode[] nodes = testArray.ToArray();

            const int RemoveStartIndex = 3;
            const int RemoveLength = 3;

            testArray.RemoveRange(RemoveStartIndex, RemoveLength);
            Assert.Equal(8 - RemoveLength, testArray.Count);
            YamlNodeTests.AssertDeepEqual(new YamlArray { 1, 2, 3, 7, 8 }, testArray);

            for (int i = 0; i < nodes.Length; i++)
            {
                if (IsIndexRemoved(i))
                {
                    Assert.Null(nodes[i].Parent);
                }
                else
                {
                    Assert.Same(testArray, nodes[i].Parent);
                }
            }

            static bool IsIndexRemoved(int originalIndex) => originalIndex >= RemoveStartIndex &&
                originalIndex < RemoveStartIndex + RemoveLength;
        }

        [Fact]
        public static void RemoveRange_FreshlyDeserialized()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PrepareData().RemoveRange(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrepareData().RemoveRange(1, -1));
            Assert.Throws<ArgumentException>(() => PrepareData().RemoveRange(10, 1));
            Assert.Throws<ArgumentException>(() => PrepareData().RemoveRange(1, 10));

            YamlArray testArray = PrepareData();
            testArray.RemoveRange(2, 2);
            YamlNodeTests.AssertDeepEqual(new YamlArray { 1, 2, 5 }, testArray);

            static YamlArray PrepareData() => YamlSerializer.Deserialize<YamlArray>("[1,2,3,4,5]");
        }

        [Theory]
        [InlineData("42")]
        [InlineData("{}")]
        public static void Deserialize_WrongType(string yaml)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<YamlArray>(yaml));
        }

        [Fact]
        public static void DeepEquals_WithNestedArrays()
        {
            YamlArray array1 = new YamlArray { 1, 2, new YamlArray { 3, 4 } };
            YamlArray array2 = new YamlArray { 1, 2, new YamlArray { 3, 4 } };
            YamlArray array3 = new YamlArray { 1, 2, new YamlArray { 3, 5 } };
            
            Assert.True(YamlNode.DeepEquals(array1, array2));
            Assert.False(YamlNode.DeepEquals(array1, array3));
            Assert.False(YamlNode.DeepEquals(array1, null));
        }

        [Fact]
        public static void DeepEquals_DifferentLengths()
        {
            YamlArray array1 = new YamlArray { 1, 2, 3 };
            YamlArray array2 = new YamlArray { 1, 2 };
            
            Assert.False(YamlNode.DeepEquals(array1, array2));
        }

        [Fact]
        public static void Clear_RemovesAllItemsAndDetachesParent()
        {
            YamlArray array = new YamlArray { 1, 2, 3 };
            YamlNode item = array[0];
            
            Assert.Equal(3, array.Count);
            Assert.Same(array, item.Parent);
            
            array.Clear();
            
            Assert.Equal(0, array.Count);
            Assert.Null(item.Parent);
        }
    }
}
