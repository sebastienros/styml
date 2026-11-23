// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class YamlObjectTests
    {
        [Fact]
        public static void KeyValuePair()
        {
            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["Two"] = 2;
            Test();

            jObject = new YamlObject { { "One", 1 }, { "Two", 2 } };
            Test();

            void Test()
            {
                KeyValuePair<string, YamlNode?> kvp1 = default;
                KeyValuePair<string, YamlNode?> kvp2 = default;

                int count = 0;
                foreach (KeyValuePair<string, YamlNode?> kvp in jObject)
                {
                    if (count == 0)
                    {
                        kvp1 = kvp;
                    }
                    else
                    {
                        kvp2 = kvp;
                    }

                    count++;
                }

                Assert.Equal(2, count);

                ICollection<KeyValuePair<string, YamlNode?>> iCollection = jObject;
                Assert.True(iCollection.Contains(kvp1));
                Assert.True(iCollection.Contains(kvp2));
                Assert.False(iCollection.Contains(new KeyValuePair<string, YamlNode?>("?", null)));

                Assert.True(iCollection.Remove(kvp1));
                Assert.Equal(1, jObject.Count);

                Assert.False(iCollection.Remove(new KeyValuePair<string, YamlNode?>("?", null)));
                Assert.Equal(1, jObject.Count);
            }
        }

        [Fact]
        public static void IsReadOnly()
        {
            var yamlObject = new YamlObject();

            ICollection<KeyValuePair<string, YamlNode?>> iCollectionOfKVP = yamlObject;
            Assert.False(iCollectionOfKVP.IsReadOnly);

            IDictionary<string, YamlNode?> iDictionary = yamlObject;
            Assert.True(iDictionary.Keys.IsReadOnly);
            Assert.True(iDictionary.Values.IsReadOnly);
        }

        [Fact]
        public static void KeyAndValueCollections_ThrowsNotSupportedException()
        {
            IDictionary<string, YamlNode?> yamlObject = new YamlObject();

            Assert.Throws<NotSupportedException>(() => yamlObject.Keys.Add("Hello"));
            Assert.Throws<NotSupportedException>(() => yamlObject.Values.Add("Hello"));

            Assert.Throws<NotSupportedException>(() => yamlObject.Keys.Clear());
            Assert.Throws<NotSupportedException>(() => yamlObject.Values.Clear());

            Assert.Throws<NotSupportedException>(() => yamlObject.Keys.Remove("Hello"));
            Assert.Throws<NotSupportedException>(() => yamlObject.Values.Remove("Hello"));
        }

        [Fact]
        public static void NullPropertyValues()
        {
            var jObject = new YamlObject();
            jObject["One"] = null;
            jObject.Add("Two", null);
            Assert.Equal(2, jObject.Count);
            Assert.Null(jObject["One"]);
            Assert.Null(jObject["Two"]);
        }

        [Fact]
        public static void NullPropertyNameFail()
        {
            ArgumentNullException ex;

            var jObject = new YamlObject();
            ex = Assert.Throws<ArgumentNullException>(() => jObject.Add(null, 42));
            Assert.Contains("propertyName", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => jObject[null] = 42);
            Assert.Contains("propertyName", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => jObject.ContainsKey(null));
            Assert.Contains("propertyName", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => jObject.Remove(null));
            Assert.Contains("propertyName", ex.Message);

            var iDictionary = (IDictionary<string, YamlNode?>)jObject;
            ex = Assert.Throws<ArgumentNullException>(() => iDictionary.TryGetValue(null, out YamlNode _));
            Assert.Contains("propertyName", ex.Message);
        }

        [Fact]
        public static void IEnumerable()
        {
            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["Two"] = 2;

            IEnumerable enumerable = jObject;
            int count = 0;
            foreach (KeyValuePair<string, YamlNode?> kvp in enumerable)
            {
                count++;
            }

            Assert.Equal(2, count);
        }

        [Fact]
        public static void MissingProperty()
        {
            var options = new YamlSerializerOptions();
            YamlObject jObject = YamlSerializer.Deserialize<YamlObject>("{}", options);
            Assert.Null(jObject["NonExistingProperty"]);
            Assert.False(jObject.Remove("NonExistingProperty"));
        }

        [Fact]
        public static void IDictionary_KeyValuePair()
        {
            IDictionary<string, YamlNode?> jObject = new YamlObject();
            jObject.Add(new KeyValuePair<string, YamlNode?>("MyProperty", 42));
            Assert.Equal(42, jObject["MyProperty"].GetValue<int>());

            Assert.Equal(1, jObject.Keys.Count);
            Assert.Equal(1, jObject.Values.Count);
        }

        [Fact]
        public static void Clear_ContainsKey()
        {
            var jObject = new YamlObject();
            jObject.Add("One", 1);
            jObject.Add("Two", 2);
            Assert.Equal(2, jObject.Count);

            Assert.True(jObject.ContainsKey("One"));
            Assert.True(jObject.ContainsKey("Two"));
            Assert.False(jObject.ContainsKey("?"));

            jObject.Clear();
            Assert.False(jObject.ContainsKey("One"));
            Assert.False(jObject.ContainsKey("Two"));
            Assert.False(jObject.ContainsKey("?"));
            Assert.Equal(0, jObject.Count);

            jObject.Add("One", 1);
            jObject.Add("Two", 2);
            Assert.Equal(2, jObject.Count);
        }

        [Fact]
        public static void Clear_YamlElement()
        {
            const string Yaml = "{\"MyProperty\":42}";
            YamlObject obj;

            // Baseline
            obj = YamlSerializer.Deserialize<YamlObject>(Yaml);
            Assert.Equal(1, obj.Count);
            obj.Clear();
            Assert.Equal(0, obj.Count);

            // Call clear with only YamlElement (not yet expanded to YamlNodes).
            obj = YamlSerializer.Deserialize<YamlObject>(Yaml);
            obj.Clear();
            // Don't check for Count of 1 since that will create nodes.
            Assert.Equal(0, obj.Count);
        }

        [Fact]
        public static void CaseSensitivity_ReadMode()
        {
            var options = new YamlSerializerOptions();
            YamlObject obj = YamlSerializer.Deserialize<YamlObject>("{\"MyProperty\":42}", options);

            Assert.Equal(42, obj["MyProperty"].GetValue<int>());
            Assert.Null(obj["myproperty"]);
            Assert.Null(obj["MYPROPERTY"]);

            options = new YamlSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            obj = YamlSerializer.Deserialize<YamlObject>("{\"MyProperty\":42}", options);

            Assert.Equal(42, obj["MyProperty"].GetValue<int>());
            Assert.Equal(42, obj["myproperty"].GetValue<int>());
            Assert.Equal(42, obj["MYPROPERTY"].GetValue<int>());
        }

        [Fact]
        public static void CaseInsensitive_Remove()
        {
            var options = new YamlSerializerOptions() { PropertyNameCaseInsensitive = true };
            YamlObject obj = YamlSerializer.Deserialize<YamlObject>("{\"MyProperty\":42}", options);

            Assert.True(obj.ContainsKey("MyProperty"));
            Assert.True(obj.ContainsKey("myproperty"));
            Assert.True(obj.ContainsKey("MYPROPERTY"));

            Assert.True(obj.Remove("myproperty"));
            Assert.False(obj.Remove("myproperty"));
            Assert.False(obj.Remove("MYPROPERTY"));
            Assert.False(obj.Remove("MyProperty"));
        }

        [Fact]
        public static void CaseSensitive_Remove()
        {
            var options = new YamlSerializerOptions() { PropertyNameCaseInsensitive = false };
            YamlObject obj = YamlSerializer.Deserialize<YamlObject>("{\"MYPROPERTY\":42,\"myproperty\":43}", options);

            Assert.False(obj.ContainsKey("MyProperty"));
            Assert.True(obj.ContainsKey("MYPROPERTY"));
            Assert.True(obj.ContainsKey("myproperty"));

            Assert.False(obj.Remove("MyProperty"));

            Assert.True(obj.Remove("MYPROPERTY"));
            Assert.False(obj.Remove("MYPROPERTY"));

            Assert.True(obj.Remove("myproperty"));
            Assert.False(obj.Remove("myproperty"));
        }

        [Fact]
        public static void CaseSensitivity_EditMode()
        {
            var jArray = new YamlArray();
            var jObject = new YamlObject();
            jObject.Add("MyProperty", 42);
            jObject.Add("myproperty", 42); // No exception

            // Options on direct node.
            var options = new YamlNodeOptions { PropertyNameCaseInsensitive = true };
            jArray = new YamlArray();
            jObject = new YamlObject(options);
            jObject.Add("MyProperty", 42);
            jArray.Add(jObject);
            Assert.Throws<ArgumentException>(() => jObject.Add("myproperty", 42));

            // Options on parent node (deferred creation of options until Add).
            jArray = new YamlArray(options);
            jObject = new YamlObject();
            jArray.Add(jObject);
            jObject.Add("MyProperty", 42);
            Assert.Throws<ArgumentException>(() => jObject.Add("myproperty", 42));

            // Dictionary is created when Add is called for the first time, so we need to be added first.
            jArray = new YamlArray(options);
            jObject = new YamlObject();
            jObject.Add("MyProperty", 42);
            jArray.Add(jObject);
            jObject.Add("myproperty", 42); // No exception since options were not set in time.
        }

        [Fact]
        public static void NamingPoliciesAreNotUsed()
        {
            const string Yaml = "{\"myProperty\":42}";

            var options = new YamlSerializerOptions();
            options.PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower;

            YamlObject obj = YamlSerializer.Deserialize<YamlObject>(Yaml, options);
            string yaml = obj.ToYamlString();
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);
        }

        [Fact]
        public static void FromElement()
        {
            using (YamlDocument document = YamlDocument.Parse("{\"myProperty\":42}"))
            {
                YamlObject jObject = YamlObject.Create(document.RootElement);
                Assert.Equal(42, jObject["myProperty"].GetValue<int>());
            }

            using (YamlDocument document = YamlDocument.Parse("null"))
            {
                YamlObject? jObject = YamlObject.Create(document.RootElement);
                Assert.Null(jObject);
            }
        }

        [Theory]
        [InlineData("42")]
        [InlineData("[]")]
        public static void FromElement_WrongNodeTypeThrows(string yaml)
        {
            using (YamlDocument document = YamlDocument.Parse(yaml))
            {
                Assert.Throws<InvalidOperationException>(() => YamlObject.Create(document.RootElement));
            }
        }

        [Fact]
        public static void WriteTo_Validation()
        {
            Assert.Throws<ArgumentNullException>(() => new YamlObject().WriteTo(null));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteTo()
        {
            const string Yaml = "{\"MyProperty\":42}";

            YamlObject jObject = YamlNode.Parse(Yaml).AsObject();
            var stream = new MemoryStream();
            var writer = new Utf8YamlWriter(stream);
            jObject.WriteTo(writer);
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(Yaml, yaml);
        }

        [Fact]
        public static void CopyTo()
        {
            YamlNode node1 = 1;
            YamlNode node2 = 2;

            IDictionary<string, YamlNode?> jObject = new YamlObject();
            jObject["One"] = node1;
            jObject["Two"] = node2;
            jObject["null"] = null;

            var arr = new KeyValuePair<string, YamlNode?>[4];
            jObject.CopyTo(arr, 0);

            Assert.Same(node1, arr[0].Value);
            Assert.Same(node2, arr[1].Value);
            Assert.Null(arr[2].Value);

            arr = new KeyValuePair<string, YamlNode?>[5];
            jObject.CopyTo(arr, 1);
            Assert.Null(arr[0].Key);
            Assert.Null(arr[0].Value);
            Assert.NotNull(arr[1].Key);
            Assert.Same(node1, arr[1].Value);
            Assert.NotNull(arr[2].Key);
            Assert.Same(node2, arr[2].Value);
            Assert.NotNull(arr[3].Key);
            Assert.Null(arr[3].Value);
            Assert.Null(arr[4].Key);
            Assert.Null(arr[4].Value);

            arr = new KeyValuePair<string, YamlNode?>[3];
            Assert.Throws<ArgumentException>(() => jObject.CopyTo(arr, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.CopyTo(arr, -1));
        }

        [Fact]
        public static void CopyTo_KeyAndValueCollections()
        {
            IDictionary<string, YamlNode?> jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["Two"] = 2;

            string[] stringArray = new string[3];
            jObject.Keys.CopyTo(stringArray, 1);
            Assert.Null(stringArray[0]);
            Assert.Equal("One", stringArray[1]);
            Assert.Equal("Two", stringArray[2]);

            YamlNode[] nodeArray = new YamlNode[3];
            jObject.Values.CopyTo(nodeArray, 1);
            Assert.Null(nodeArray[0]);
            Assert.Equal(1, nodeArray[1].GetValue<int>());
            Assert.Equal(2, nodeArray[2].GetValue<int>());

            Assert.Throws<ArgumentException>(() => jObject.Keys.CopyTo(stringArray, 2));
            Assert.Throws<ArgumentException>(() => jObject.Values.CopyTo(nodeArray, 2));

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.Keys.CopyTo(stringArray, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.Values.CopyTo(nodeArray, -1));
        }

        [Fact]
        public static void Contains_KeyAndValueCollections()
        {
            IDictionary<string, YamlNode?> jObject = new YamlObject();
            jObject["One"] = 1;

            Assert.True(jObject.Keys.Contains("One"));
            Assert.False(jObject.Keys.Contains("Two"));

            Assert.True(jObject.Values.Contains(jObject["One"]));
            Assert.False(jObject.Values.Contains(1)); // Reference semantics causes this to be false.
        }

        [Fact]
        public static void IEnumerable_KeyAndValueCollections()
        {
            IDictionary<string, YamlNode?> jObject = new YamlObject();
            jObject["One"] = 1;

            IEnumerable enumerable = jObject.Keys;
            int count = 0;
            foreach (string str in enumerable)
            {
                count++;
            }
            Assert.Equal(1, count);

            enumerable = jObject.Values;
            count = 0;
            foreach (YamlNode node in enumerable)
            {
                count++;
            }
            Assert.Equal(1, count);
        }

        [Fact]
        public static void CreateDom()
        {
            var jObj = new YamlObject
            {
                // Primitives
                ["MyString"] = YamlValue.Create("Hello!"),
                ["MyNull"] = null,
                ["MyBoolean"] = YamlValue.Create(false),

                // Nested array
                ["MyArray"] = new YamlArray
                (
                    YamlValue.Create(2),
                    YamlValue.Create(3),
                    YamlValue.Create(42)
                ),

                // Additional primitives
                ["MyInt"] = YamlValue.Create(43),
                ["MyDateTime"] = YamlValue.Create(new DateTime(2020, 7, 8)),
                ["MyGuid"] = YamlValue.Create(new Guid("ed957609-cdfe-412f-88c1-02daca1b4f51")),

                // Nested objects
                ["MyObject"] = new YamlObject
                {
                    ["MyString"] = YamlValue.Create("Hello!!")
                },

                ["Child"] = new YamlObject
                {
                    ["ChildProp"] = YamlValue.Create(1)
                }
            };

            string yaml = jObj.ToYamlString();
            YamlTestHelper.AssertYamlEqual(YamlNodeTests.ExpectedDomYaml, yaml);
        }

        [Fact]
        public static void CreateDom_ImplicitOperators()
        {
            var jObj = new YamlObject
            {
                // Primitives
                ["MyString"] = "Hello!",
                ["MyNull"] = null,
                ["MyBoolean"] = false,

                // Nested array
                ["MyArray"] = new YamlArray(2, 3, 42),

                // Additional primitives
                ["MyInt"] = 43,
                ["MyDateTime"] = new DateTime(2020, 7, 8),
                ["MyGuid"] = new Guid("ed957609-cdfe-412f-88c1-02daca1b4f51"),

                // Nested objects
                ["MyObject"] = new YamlObject
                {
                    ["MyString"] = "Hello!!"
                },

                ["Child"] = new YamlObject()
                {
                    ["ChildProp"] = 1
                }
            };

            string yaml = jObj.ToYamlString();
            YamlTestHelper.AssertYamlEqual(YamlNodeTests.ExpectedDomYaml, yaml);
        }

        [Fact]
        public static void EditDom()
        {
            const string Yaml =
                "{\"MyString\":\"Hello\",\"MyNull\":null,\"MyBoolean\":true,\"MyArray\":[1,2],\"MyInt\":42,\"MyDateTime\":\"2020-07-08T00:00:00\",\"MyGuid\":\"ed957609-cdfe-412f-88c1-02daca1b4f51\",\"MyObject\":{\"MyString\":\"World\"}}";

            YamlNode obj = YamlSerializer.Deserialize<YamlObject>(Yaml);
            Verify();

            // Verify the values are round-trippable.
            ((YamlArray)obj["MyArray"]).RemoveAt(2);
            Verify();

            void Verify()
            {
                // Change some primitives.
                obj["MyString"] = YamlValue.Create("Hello!");
                obj["MyBoolean"] = YamlValue.Create(false);
                obj["MyInt"] = YamlValue.Create(43);

                // Add nested objects.
                obj["MyObject"] = new YamlObject();
                obj["MyObject"]["MyString"] = YamlValue.Create("Hello!!");

                obj["Child"] = new YamlObject();
                obj["Child"]["ChildProp"] = YamlValue.Create(1);

                // Modify number elements.
                obj["MyArray"][0] = YamlValue.Create(2);
                obj["MyArray"][1] = YamlValue.Create(3);

                // Add an element.
                ((YamlArray)obj["MyArray"]).Add(YamlValue.Create(42));

                string yaml = obj.ToYamlString();
                YamlTestHelper.AssertYamlEqual(YamlNodeTests.ExpectedDomYaml, yaml);
            }
        }

        [Fact]
        public static void ReAddSameNode_Throws()
        {
            var jValue = YamlValue.Create(1);

            var jObject = new YamlObject();
            jObject.Add("Prop", jValue);
            ArgumentException ex = Assert.Throws<ArgumentException>(() => jObject.Add("Prop", jValue));
            Assert.Contains("Prop", ex.Message);
        }

        [Fact]
        public static void ReAddRemovedNode()
        {
            var jValue = YamlValue.Create(1);

            var jObject = new YamlObject();
            jObject.Add("Prop", jValue);
            Assert.Equal(jObject, jValue.Parent);

            // Replace with a new node.
            jObject["Prop"] = 42;

            Assert.Null(jValue.Parent);
            Assert.Equal(1, jObject.Count);
            jObject.Remove("Prop");
            Assert.Equal(0, jObject.Count);
            jObject.Add("Prop", jValue);
            Assert.Equal(1, jObject.Count);
        }

        [Fact]
        public static void DynamicObject_LINQ_Query()
        {
            YamlArray allOrders = YamlSerializer.Deserialize<YamlArray>(YamlNodeTests.Linq_Query_Yaml);
            IEnumerable<YamlNode> orders = allOrders.Where(o => o["Customer"]["City"].GetValue<string>() == "Fargo");

            Assert.Equal(2, orders.Count());
            Assert.Equal(100, orders.ElementAt(0)["OrderId"].GetValue<int>());
            Assert.Equal(300, orders.ElementAt(1)["OrderId"].GetValue<int>());
            Assert.Equal("Customer1", orders.ElementAt(0)["Customer"]["Name"].GetValue<string>());
            Assert.Equal("Customer3", orders.ElementAt(1)["Customer"]["Name"].GetValue<string>());
        }

        private class BlogPost
        {
            public string Title { get; set; }
            public string AuthorName { get; set; }
            public string AuthorTwitter { get; set; }
            public string Body { get; set; }
            public DateTime PostedDate { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DynamicObject_LINQ_Convert()
        {
            string yaml = """

                    [
                      {
                        "Title": "TITLE.",
                        "Author":
                        {
                          "Name": "NAME.",
                          "Mail": "MAIL.",
                          "Picture": "/PICTURE.png"
                        },
                        "Date": "2021-01-20T19:30:00",
                        "BodyHtml": "Content."
                      }
                    ]
                """;

            YamlArray arr = YamlSerializer.Deserialize<YamlArray>(yaml);

            // Convert nested JSON to a flat POCO.
            IList<BlogPost> blogPosts = arr.Select(p => new BlogPost
            {
                Title = p["Title"].GetValue<string>(),
                AuthorName = p["Author"]["Name"].GetValue<string>(),
                AuthorTwitter = p["Author"]["Mail"].GetValue<string>(),
                PostedDate = p["Date"].GetValue<DateTime>(),
                Body = p["BodyHtml"].GetValue<string>()
            }).ToList();

            const string expected = "[{\"Title\":\"TITLE.\",\"AuthorName\":\"NAME.\",\"AuthorTwitter\":\"MAIL.\",\"Body\":\"Content.\",\"PostedDate\":\"2021-01-20T19:30:00\"}]";

            string yaml_out = YamlSerializer.Serialize(blogPosts);
            Assert.Equal(expected, yaml_out);
        }

        [Theory]
        [MemberData(nameof(JObjectCollectionData))]

        [Trait("category", "jsonoutput")]
        public static void ListToDictionaryConversions(YamlObject jObject, int count)
        {
            Assert.Equal(count, jObject.Count);

            int i;

            for (i = 0; i < count; i++)
            {
                Assert.Equal(i, jObject[i.ToString()].GetValue<int>());
            }

            i = 0;
            foreach (KeyValuePair<string, YamlNode?> kvp in jObject)
            {
                Assert.Equal(i.ToString(), kvp.Key);
                Assert.Equal(i, kvp.Value.GetValue<int>());
                i++;
            }

            i = 0;
            foreach (object o in (IEnumerable)jObject)
            {
                var kvp = (KeyValuePair<string, YamlNode?>)o;
                Assert.Equal(i.ToString(), kvp.Key);
                Assert.Equal(i, kvp.Value.GetValue<int>());
                i++;
            }

            var dictionary = (IDictionary<string, YamlNode?>)jObject;

            i = 0;
            foreach (string propertyName in dictionary.Keys)
            {
                Assert.Equal(i.ToString(), propertyName);
                i++;
            }

            i = 0;
            foreach (YamlNode? node in dictionary.Values)
            {
                Assert.Equal(i, node.GetValue<int>());
                i++;
            }

            string expectedYaml = jObject.ToYamlString();

            // Use indexers to replace items.
            for (i = 0; i < count; i++)
            {
                string key = i.ToString();

                // Contains does a reference comparison on YamlNode so it needs to be done before modifying.
                Assert.True(jObject.Contains(new KeyValuePair<string, YamlNode?>(key, jObject[key])));

                jObject[key] = YamlValue.Create(i);
                jObject[key] = jObject[key]; // Should have no effect.

                Assert.False(jObject.Contains(new KeyValuePair<string, YamlNode?>("MISSING", jObject[key])));
                Assert.True(jObject.ContainsKey(key));

                // Remove() should not affect result when missing.
                bool success = jObject.Remove("MISSING");
                Assert.False(success);
            }

            // JSON shouldn't change.
            Assert.Equal(expectedYaml, jObject.ToYamlString());

            // Remove every other entry.
            for (i = 0; i < count; i += 2)
            {
                bool success = dictionary.Remove(i.ToString());
                Assert.True(success);
            }

            Assert.Equal(count / 2, jObject.Count);

            // The new JSON contains half the entries.
            expectedYaml = jObject.ToYamlString();

            // Add back every other entry (to the end)
            for (i = 0; i < count; i += 2)
            {
                jObject.Add(i.ToString(), YamlValue.Create(i));
            }
            Assert.Equal(count, jObject.Count);

            // The beginning part of the JSON should be the same.
            string yaml = jObject.ToYamlString();
            Assert.Contains(expectedYaml.TrimEnd('}'), yaml);

            const int ItemsToAdd = 10;
            for (i = 10000; i < 10000 + ItemsToAdd; i++)
            {
                jObject.Add(i.ToString(), i);
            }

            // Original items should still be in front.
            yaml = jObject.ToYamlString();
            Assert.Contains(expectedYaml.TrimEnd('}'), yaml);

            Assert.Equal(count + ItemsToAdd, jObject.Count);
        }

        public static IEnumerable<object[]> JObjectCollectionData()
        {
            // Ensure that the list-to-dictionary threshold is hit (currently 9).
            for (int i = 0; i < 25; i++)
            {
                yield return CreateArray(i);
                yield return CreateArray_YamlElement(i);
            }

            yield return CreateArray(123);
            yield return CreateArray_YamlElement(122);

            yield return CreateArray(300);
            yield return CreateArray_YamlElement(299);

            object[] CreateArray(int count)
            {
                YamlObject jObject = CreateYamlObject(count);
                return new object[] { jObject, count };
            }

            YamlObject CreateYamlObject(int count)
            {
                var jObject = new YamlObject();

                for (int i = 0; i < count; i++)
                {
                    jObject[i.ToString()] = i;
                }

                return jObject;
            }

            object[] CreateArray_YamlElement(int count)
            {
                YamlObject jObject = CreateYamlObject(count);
                string yaml = jObject.ToYamlString();
                jObject = YamlNode.Parse(yaml).AsObject();
                return new object[] { jObject, count };
            }
        }

        [Theory]
        [MemberData(nameof(JObjectCollectionData))]
        public static void ChangeCollectionWhileEnumeratingFails(YamlObject jObject, int count)
        {
            if (count == 0)
            {
                return;
            }

            Assert.Equal(count, jObject.Count);

            int index = 0;

            // Exception string sample: "Collection was modified; enumeration operation may not execute"
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (KeyValuePair<string, YamlNode?> node in jObject)
                {
                    index++;
                    jObject.Add("New_A", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (KeyValuePair<string, YamlNode?> node in jObject)
                {
                    index++;
                    jObject.Remove(node.Key);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            IEnumerable iEnumerable = jObject;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (KeyValuePair<string, YamlNode?> node in iEnumerable)
                {
                    index++;
                    jObject.Add("New_B", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (KeyValuePair<string, YamlNode?> node in iEnumerable)
                {
                    index++;
                    jObject.Remove(node.Key);
                }
            });
            Assert.Equal(1, index);

            IDictionary<string, YamlNode?> iDictionary = jObject;

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (string str in iDictionary.Keys)
                {
                    index++;
                    jObject.Add("New_C", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (string str in (IEnumerable)iDictionary.Keys)
                {
                    index++;
                    jObject.Add("New_D", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (string str in iDictionary.Keys)
                {
                    index++;
                    jObject.Remove(str);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (YamlNode node in iDictionary.Values)
                {
                    index++;
                    jObject.Add("New_E", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (YamlNode node in (IEnumerable)iDictionary.Values)
                {
                    index++;
                    jObject.Add("New_F", index);
                }
            });
            Assert.Equal(1, index);

            index = 0;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (YamlNode node in iDictionary.Values)
                {
                    index++;
                    jObject.Clear();
                }
            });
            Assert.Equal(1, index);
        }

        [Fact]
        public static void TestYamlNodeOptionsSet()
        {
            var options = new YamlNodeOptions()
            {
                PropertyNameCaseInsensitive = true
            };

            // Ctor that takes just options
            var obj1 = new YamlObject(options);
            obj1["Hello"] = "World";

            // Ctor that takes props IEnumerable + options
            IEnumerable<KeyValuePair<string, YamlNode?>> props = new List<KeyValuePair<string, YamlNode?>>
            {
                new KeyValuePair<string, YamlNode?>("Hello", "World")
            };
            var obj2 = new YamlObject(props, options);

            // Create method
            using YamlDocument doc = YamlDocument.Parse("""{"Hello":"World"}""");
            var obj3 = YamlObject.Create(doc.RootElement, options);

            Test(obj1);
            Test(obj2);
            Test(obj3);

            static void Test(YamlObject jObject)
            {
                Assert.NotNull(jObject.Options);
                Assert.True(jObject.Options.Value.PropertyNameCaseInsensitive);
                Assert.Equal("World", (string)jObject["Hello"]);
                Assert.Equal("World", (string)jObject["hello"]); // Test case insensitivity
            }
        }

        [Fact]
        public static void LazyInitializationIsThreadSafe()
        {
            string arrayText = "{\"prop0\":0,\"prop1\":1}";
            YamlObject jObj = Assert.IsType<YamlObject>(YamlNode.Parse(arrayText));
            Parallel.For(0, 128, i =>
            {
                Assert.Equal(0, (int)jObj["prop0"]);
                Assert.Equal(1, (int)jObj["prop1"]);
            });
        }

        [Fact]
        public static void DeepClone()
        {
            var array = new YamlArray();
            array.Add(5);
            array.Add(7);
            var nestedYamlObj = new YamlObject()
            {
                { "Ten", 10 },
                { "Name", "xyz"}
            };

            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["Two"] = 2;
            jObject["String"] = "ABC";
            jObject["True"] = true;
            jObject["False"] = false;
            jObject["Null"] = null;
            jObject["value"] = YamlValue.Create(10);
            jObject["array"] = array;
            jObject["object"] = nestedYamlObj;

            var clone = jObject.DeepClone().AsObject();

            YamlNodeTests.AssertDeepEqual(jObject, clone);

            Assert.Equal(jObject.Count, clone.Count);
            Assert.Equal(1, clone["One"].GetValue<int>());
            Assert.Equal(2, clone["Two"].GetValue<int>());
            Assert.Equal("ABC", clone["String"].GetValue<string>());
            Assert.True(clone["True"].GetValue<bool>());
            Assert.False(clone["False"].GetValue<bool>());
            Assert.Null(clone["Null"]);
            Assert.Equal(10, clone["value"].GetValue<int>());

            YamlArray clonedArray = clone["array"].AsArray();
            Assert.Equal(array.Count, clonedArray.Count);
            Assert.Equal(5, clonedArray[0].GetValue<int>());
            Assert.Equal(7, clonedArray[1].GetValue<int>());

            YamlObject clonedNestedJObject = clone["object"].AsObject();
            Assert.Equal(nestedYamlObj.Count, clonedNestedJObject.Count);
            Assert.Equal(10, clonedNestedJObject["Ten"].GetValue<int>());
            Assert.Equal("xyz", clonedNestedJObject["Name"].GetValue<string>());

            string originalYaml = jObject.ToYamlString();
            string clonedYaml = clone.ToYamlString();

            Assert.Equal(originalYaml, clonedYaml);
        }

        [Fact]
        public static void DeepClone_FromElement()
        {
            using YamlDocument document = YamlDocument.Parse("{\"One\": 1, \"String\": \"abc\"}");
            YamlObject jObject = YamlObject.Create(document.RootElement);
            var clone = jObject.DeepClone().AsObject();

            YamlNodeTests.AssertDeepEqual(jObject, clone);
            Assert.Equal(1, clone["One"].GetValue<int>());
            Assert.Equal("abc", clone["String"].GetValue<string>());
        }

        [Fact]
        public static void DeepEquals()
        {
            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["array"] = new YamlArray() { "a", "b" };

            var sameJObject = new YamlObject();
            sameJObject["One"] = 1;
            sameJObject["array"] = new YamlArray() { "a", "b" };

            YamlNodeTests.AssertDeepEqual(jObject, sameJObject);
            YamlNodeTests.AssertNotDeepEqual(jObject, null);

            var diffJObject = new YamlObject();
            diffJObject["One"] = 3;

            YamlNodeTests.AssertNotDeepEqual(diffJObject, jObject);
        }

        [Fact]
        public static void DeepEquals_YamlObject_With_YamlValuePOCO()
        {
            var jObject = new YamlObject();
            jObject["Id"] = 1;
            jObject["Name"] = "First";
            var nestedObject = new YamlObject();
            nestedObject["Id"] = 2;
            nestedObject["Name"] = "Last";
            nestedObject["NestedObject"] = null;
            jObject["NestedObject"] = nestedObject;

            var poco = new SimpleClass()
            {
                Id = 1,
                Name = "First",
                NestedObject = new SimpleClass()
                {
                    Id = 2,
                    Name = "Last",
                }
            };

            YamlNodeTests.AssertDeepEqual(jObject, YamlValue.Create(poco));

            var diffPoco = new SimpleClass()
            {
                Id = 1,
                Name = "First",
                NestedObject = new SimpleClass()
                {
                    Id = 3,
                    Name = "Last",
                }
            };

            YamlNodeTests.AssertNotDeepEqual(jObject, YamlValue.Create(diffPoco));
        }

        [Fact]
        public static void DeepEquals_YamlObject_With_Dictionary()
        {
            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["array"] = new YamlArray() { "a", "b" };
            jObject["obj"] = new YamlObject();

            var dictionary = new Dictionary<string, object>()
            {
                { "One", 1 },
                { "array", new string[] { "a", "b" } },
                { "obj", new { } }
            };

            YamlNodeTests.AssertDeepEqual(jObject, YamlValue.Create(dictionary));

            var diffDictionary = new Dictionary<string, object>()
            {
                { "One", 1 },
                { "array", new string[] { "a", "d" } },
                { "obj", new { } }
            };

            YamlNodeTests.AssertNotDeepEqual(jObject, YamlValue.Create(diffDictionary));
        }

        private class SimpleClass
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public SimpleClass? NestedObject { get; set; }
        }

        [Fact]
        public static void DeepEqualFromElement()
        {
            using YamlDocument document = YamlDocument.Parse("{\"One\": 1, \"String\": \"abc\"}");
            YamlObject jObject = YamlObject.Create(document.RootElement);

            using YamlDocument document2 = YamlDocument.Parse("{\"One\":     1, \"String\":     \"abc\"}   ");
            YamlObject jObject2 = YamlObject.Create(document2.RootElement);
            YamlNodeTests.AssertDeepEqual(jObject, jObject2);

            using YamlDocument document3 = YamlDocument.Parse("{\"One\": 3, \"String\": \"abc\"}");
            YamlObject jObject3 = YamlObject.Create(document3.RootElement);
            YamlNodeTests.AssertNotDeepEqual(jObject, jObject3);

            using YamlDocument document4 = YamlDocument.Parse("{\"One\":     1, \"String\":     \"abc2\"}   ");
            YamlObject jObject4 = YamlObject.Create(document4.RootElement);
            YamlNodeTests.AssertNotDeepEqual(jObject, jObject4);
        }

        [Fact]
        public static void UpdateClonedObjectNotAffectOriginal()
        {
            var jObject = new YamlObject();
            jObject["One"] = 1;
            jObject["Two"] = 2;

            var clone = jObject.DeepClone().AsObject();
            clone["One"] = 3;

            Assert.Equal(1, jObject["One"].GetValue<int>());
        }

        [Fact]
        public static void GetValueKind()
        {
            Assert.Equal(YamlValueKind.Object, new YamlObject().GetValueKind());
        }

        [Fact]
        public static void GetPropertyName()
        {
            var jObject = new YamlObject();
            var jValue = YamlValue.Create(10);
            jObject.Add("value", jValue);

            Assert.Equal("value", jValue.GetPropertyName());
            Assert.Equal("value", jObject["value"].GetPropertyName());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ReplaceWith()
        {
            var jObject = new YamlObject();
            var jValue = YamlValue.Create(10);
            jObject["value"] = jValue;
            jObject["value"].ReplaceWith(5);

            Assert.Null(jValue.Parent);
            Assert.Equal("{\"value\":5}", jObject.ToYamlString());
        }

        // List-based APIs

        [Fact]
        public static void IntIndexer_Getter_ReturnsExpectedProperty()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            YamlNode? value;
            value = jObject[0];
            Assert.Equal(1, value.GetValue<int>());

            value = jObject[1];
            Assert.Equal("str", value.GetValue<string>());

            value = jObject[2];
            Assert.Null(value);
        }

        [Fact]
        public static void IntIndexer_Setter_UpdatesProperty()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Null(jObject["Three"]);
            Assert.Equal(3, jObject.Count);

            jObject[2] = -5;

            Assert.Equal(-5, jObject["Three"].GetValue<int>());
            Assert.Equal(3, jObject.Count);
        }

        [Fact]
        public static void IntIndexer_ArgumentOutOfRange_ThrowsException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject[jObject.Count]);
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject[-1] = 42);
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject[jObject.Count] = 42);
        }

        [Fact]
        public static void GetAt_ReturnsExpectedProperty()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            KeyValuePair<string, YamlNode?> kvp;
            kvp = jObject.GetAt(0);
            Assert.Equal("One", kvp.Key);
            Assert.Equal(1, kvp.Value.GetValue<int>());

            kvp = jObject.GetAt(1);
            Assert.Equal("Two", kvp.Key);
            Assert.Equal("str", kvp.Value.GetValue<string>());

            kvp = jObject.GetAt(2);
            Assert.Equal("Three", kvp.Key);
            Assert.Null(kvp.Value);
        }

        [Fact]
        public static void GetAt_InvalidArgument_ThrowsArgumentOutOfRangeException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.GetAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.GetAt(jObject.Count));
        }

        [Fact]
        public static void SetAt_UpdatesProperty()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Null(jObject["Three"]);
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, -5);

            Assert.Equal(-5, jObject["Three"].GetValue<int>());
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, "Three", -33);

            Assert.Equal(-33, jObject["Three"].GetValue<int>());
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, "Four", "str");

            Assert.Equal(3, jObject.Count);
            Assert.DoesNotContain("Three", jObject);
            Assert.Contains("Four", jObject);
            Assert.Equal("str", jObject["Four"].GetValue<string>());
        }

        [Fact]
        public static void SetAt_CaseInsensitive_UpdatesProperty()
        {
            YamlObject jObject = new(new() { PropertyNameCaseInsensitive = true })
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Null(jObject["three"]);
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, -5);

            Assert.Equal(-5, jObject["three"].GetValue<int>());
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, "THREE", -33);

            Assert.Equal(-33, jObject["ThRee"].GetValue<int>());
            Assert.Equal(3, jObject.Count);

            jObject.SetAt(2, "Four", "str");

            Assert.Equal(3, jObject.Count);
            Assert.DoesNotContain("Three", jObject);
            Assert.Contains("FOUR", jObject);
            Assert.Equal("str", jObject["FoUR"].GetValue<string>());
        }

        [Fact]
        public static void SetAt_InvalidArgument_ThrowsArgumentOutOfRangeException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            // Throws exception if the property name is null.
            Assert.Throws<ArgumentNullException>(() => jObject.SetAt(2, propertyName: null, 5));

            // Throws exception if the index is out of range.
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.SetAt(-1, "Four", 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.SetAt(jObject.Count, "Four", 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.SetAt(-1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.SetAt(jObject.Count, 5));

            // Throws exception if the key exists at a different position.
            Assert.Throws<ArgumentException>(() => jObject.SetAt(2, "One", "str"));
        }

        [Fact]
        public static void IndexOf_ReturnsExpectedIndex()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Equal(0, jObject.IndexOf("One"));
            Assert.Equal(1, jObject.IndexOf("Two"));
            Assert.Equal(2, jObject.IndexOf("Three"));

            Assert.Equal(-1, jObject.IndexOf("Four"));
            Assert.Equal(-1, jObject.IndexOf("three"));
        }

        [Fact]
        public static void IndexOf_CaseInsensitive_ReturnsExpectedIndex()
        {
            YamlObject jObject = new(new() { PropertyNameCaseInsensitive = true })
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Equal(0, jObject.IndexOf("One"));
            Assert.Equal(1, jObject.IndexOf("Two"));
            Assert.Equal(2, jObject.IndexOf("Three"));

            Assert.Equal(0, jObject.IndexOf("onE"));
            Assert.Equal(1, jObject.IndexOf("tWo"));
            Assert.Equal(2, jObject.IndexOf("THREE"));

            Assert.Equal(-1, jObject.IndexOf("Four"));
        }

        [Fact]
        public static void IndexOf_NullPropertyName_ThrowsArgumentNullException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentNullException>(() => jObject.IndexOf(propertyName: null));
        }

        [Fact]
        public static void Insert_InsertsProperty()
        {
            YamlObject jObject = new()
            {
                ["Two"] = "str",
            };

            Assert.Equal(1, jObject.Count);

            jObject.Insert(0, "One", 1);

            Assert.Equal(2, jObject.Count);
            Assert.Equal(1, jObject["One"].GetValue<int>());
            Assert.Equal("str", jObject["Two"].GetValue<string>());
            Assert.Equal(0, jObject.IndexOf("One"));
            Assert.Equal(1, jObject.IndexOf("Two"));

            jObject.Insert(1, "Three", null);

            Assert.Equal(3, jObject.Count);
            Assert.Equal(1, jObject["One"].GetValue<int>());
            Assert.Null(jObject["Three"]);
            Assert.Equal("str", jObject["Two"].GetValue<string>());
            Assert.Equal(0, jObject.IndexOf("One"));
            Assert.Equal(1, jObject.IndexOf("Three"));
            Assert.Equal(2, jObject.IndexOf("Two"));

            jObject.Insert(jObject.Count, "Four", 4);

            Assert.Equal(4, jObject.Count);
            Assert.Equal(1, jObject["One"].GetValue<int>());
            Assert.Null(jObject["Three"]);
            Assert.Equal("str", jObject["Two"].GetValue<string>());
            Assert.Equal(4, jObject["Four"].GetValue<int>());
            Assert.Equal(0, jObject.IndexOf("One"));
            Assert.Equal(1, jObject.IndexOf("Three"));
            Assert.Equal(2, jObject.IndexOf("Two"));
            Assert.Equal(3, jObject.IndexOf("Four"));
        }

        [Fact]
        public static void Insert_ExistingKey_ThrowsArgumentException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Equal(3, jObject.Count);

            Assert.Throws<ArgumentException>(() => jObject.Insert(1, "Three", 3));

            Assert.Equal(3, jObject.Count);
        }

        [Fact]
        public static void Insert_NullKey_ThrowsArgumentNullException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Equal(3, jObject.Count);

            Assert.Throws<ArgumentNullException>(() => jObject.Insert(1, propertyName: null, 3));

            Assert.Equal(3, jObject.Count);
        }

        [Fact]
        public static void Insert_InvalidIndex_ThrowsArgumentOutOfRangeException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Equal(3, jObject.Count);

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.Insert(-1, "Four", 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.Insert(jObject.Count + 1, "Four", 4));

            Assert.Equal(3, jObject.Count);
        }

        [Fact]
        public static void RemoveAt_RemovesProperty()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            jObject.RemoveAt(1);

            Assert.Equal(2, jObject.Count);
            Assert.Contains("One", jObject);
            Assert.DoesNotContain("Two", jObject);
            Assert.Contains("Three", jObject);

            jObject.RemoveAt(1);

            Assert.Equal(1, jObject.Count);
            Assert.Contains("One", jObject);
            Assert.DoesNotContain("Two", jObject);
            Assert.DoesNotContain("Three", jObject);

            jObject.RemoveAt(0);

            Assert.Empty(jObject);
            Assert.DoesNotContain("One", jObject);
            Assert.DoesNotContain("Two", jObject);
            Assert.DoesNotContain("Three", jObject);
        }

        [Fact]
        public static void RemoveAt_InvalidIndex_ThrowsArgumentOutOfRangeException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.RemoveAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => jObject.RemoveAt(jObject.Count));
        }

        [Fact]
        public static void YamlObject_IsIList()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            IList<KeyValuePair<string, YamlNode?>> ilist = Assert.IsAssignableFrom<IList<KeyValuePair<string, YamlNode?>>>(jObject);

            // Indexer getter
            KeyValuePair<string, YamlNode?> kvp = ilist[1];
            Assert.Equal("Two", kvp.Key);
            Assert.Equal("str", kvp.Value.GetValue<string>());

            // Indexer setter
            kvp = new("Four", 4);
            ilist[1] = kvp;
            Assert.Contains(kvp, ilist);
            Assert.DoesNotContain("Two", jObject);
            Assert.Contains("Four", jObject);

            // IndexOf
            Assert.Equal(1, ilist.IndexOf(kvp));
            Assert.Equal(-1, ilist.IndexOf(new("Four", 4))); // Different YamlNode instance

            // Insert
            ilist.Insert(1, new("Two", "str"));
            Assert.Equal(4, ilist.Count);
            Assert.Contains("Two", jObject);
            Assert.Equal(1, jObject.IndexOf("Two"));

            // RemoveAt
            ilist.RemoveAt(1);
            Assert.Equal(3, ilist.Count);
            Assert.DoesNotContain("Two", jObject);
            Assert.Equal(-1, jObject.IndexOf("Two"));
        }

        [Fact]
        public static void TryAdd_NewKey_EmptyYamlObject()
        {
            YamlObject jObject = new();

            Assert.True(jObject.TryAdd("First", "value", out var index));
            Assert.Equal(0, index);
        }

        [Fact]
        public static void TryAdd_NewKey()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.True(jObject.TryAdd("Four", 33, out var index));
            Assert.Equal(3, index);
        }

        [Fact]
        public static void TryAdd_ExistingKey()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.False(jObject.TryAdd("Two", 33, out var index));
            Assert.Equal(1, index);
        }

        [Fact]
        public static void TryAdd_ThrowsArgumentNullException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentNullException>(() => { jObject.TryAdd(null, 33); });
        }

        [Fact]
        public static void TryGetPropertyValue_ThrowsArgumentNullException()
        {
            YamlObject jObject = new()
            {
                ["One"] = 1,
                ["Two"] = "str",
                ["Three"] = null,
            };

            Assert.Throws<ArgumentNullException>(() => { jObject.TryGetPropertyValue(null, out var yamlNode, out var index); });
        }

        [Theory]
        [InlineData(10_000)]
        [InlineData(50_000)]
        [InlineData(100_000)]
        public static void YamlObject_ExtensionData_ManyDuplicatePayloads(int size)
        {
            // Generate the payload
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            for (int i = 0; i < size; i++)
            {
                builder.Append($"\"{i}\": 0,");
                builder.Append($"\"{i}\": 0,");
            }
            builder.Length--; // strip trailing comma
            builder.Append("}");

            string yamlPayload = builder.ToString();
            ClassWithObjectExtensionData result = YamlSerializer.Deserialize<ClassWithObjectExtensionData>(yamlPayload, YamlTestSerializerOptions.LastWins);
            Assert.Equal(size, result.ExtensionData.Count);
        }

        class ClassWithObjectExtensionData
        {
            [YamlExtensionData]
            public YamlObject ExtensionData { get; set; }
        }

        [Fact]
        public static void YamlObject_DuplicatePropertyCaseInsensitiveThrows()
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;

            string yamlPayload = """{"a":1,"A":2}""";

            _ = YamlSerializer.Deserialize<YamlObject>(yamlPayload); // Assert no throw
            AssertExtensions.ThrowsContains<ArgumentException>(
                () => YamlSerializer.Deserialize<YamlObject>(yamlPayload, options),
                "An item with the same key has already been added.");

            _ = YamlSerializer.Deserialize<YamlNode>(yamlPayload); // Assert no throw
            AssertExtensions.ThrowsContains<ArgumentException>(
                () => YamlSerializer.Deserialize<YamlNode>(yamlPayload, options),
                "An item with the same key has already been added.");
        }

        [Fact]
        public static void YamlObject_NestedDuplicatePropertyCaseInsensitiveThrows()
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;

            string yamlPayload = """[{"a":1,"A":2}]""";

            _ = YamlSerializer.Deserialize<YamlArray>(yamlPayload); // Assert no throw
            AssertExtensions.ThrowsContains<ArgumentException>(
                () => YamlSerializer.Deserialize<YamlArray>(yamlPayload, options),
                "An item with the same key has already been added.");

            _ = YamlSerializer.Deserialize<YamlNode>(yamlPayload); // Assert no throw
            AssertExtensions.ThrowsContains<ArgumentException>(
                () => YamlSerializer.Deserialize<YamlNode>(yamlPayload, options),
                "An item with the same key has already been added.");
        }

        [Theory]
        [InlineData("42")]
        [InlineData("[]")]
        public static void Deserialize_WrongType(string yaml)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<YamlObject>(yaml));
        }

        [Fact]
        public static void GetPath_IsThreadSafe()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var tree = (YamlNode.Parse(
                    """
                    {
                      "oh": "noes"
                    }
                    """
                ) as YamlObject)!;

                Parallel.ForEach(Enumerable.Range(0, 100), new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
                {
                    string path = tree.First().Value!.GetPath();
                    Assert.Equal("$.oh", path);
                });
            }
        }

        [Fact]
        public static void DeepEquals_WithNestedObjects()
        {
            YamlObject obj1 = new YamlObject { ["a"] = 1, ["b"] = new YamlObject { ["c"] = 2 } };
            YamlObject obj2 = new YamlObject { ["a"] = 1, ["b"] = new YamlObject { ["c"] = 2 } };
            YamlObject obj3 = new YamlObject { ["a"] = 1, ["b"] = new YamlObject { ["c"] = 3 } };
            
            Assert.True(YamlNode.DeepEquals(obj1, obj2));
            Assert.False(YamlNode.DeepEquals(obj1, obj3));
            Assert.False(YamlNode.DeepEquals(obj1, null));
        }

        [Fact]
        public static void DeepEquals_DifferentKeys()
        {
            YamlObject obj1 = new YamlObject { ["a"] = 1, ["b"] = 2 };
            YamlObject obj2 = new YamlObject { ["a"] = 1, ["c"] = 2 };
            
            Assert.False(YamlNode.DeepEquals(obj1, obj2));
        }

        [Fact]
        public static void TryGetPropertyValue_NonExistentProperty()
        {
            YamlObject obj = new YamlObject { ["a"] = 1 };
            
            bool result = obj.TryGetPropertyValue("b", out YamlNode value);
            
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public static void ContainsKey_ChecksForProperty()
        {
            YamlObject obj = new YamlObject { ["a"] = 1, ["b"] = 2 };
            
            Assert.True(obj.ContainsKey("a"));
            Assert.False(obj.ContainsKey("c"));
        }
    }
}
