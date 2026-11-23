// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class CollectionTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfString()
        {
            const string YamlString = """{"Hello":"World","Hello2":"World2"}""";
            const string ReorderedYamlString = """{"Hello2":"World2","Hello":"World"}""";

            {
                IDictionary obj = await Serializer.DeserializeWrapper<IDictionary>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                Dictionary<string, string> obj = await Serializer.DeserializeWrapper<Dictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                SortedDictionary<string, string> obj = await Serializer.DeserializeWrapper<SortedDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                IDictionary<string, string> obj = await Serializer.DeserializeWrapper<IDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                IReadOnlyDictionary<string, string> obj = await Serializer.DeserializeWrapper<IReadOnlyDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                ImmutableDictionary<string, string> obj = await Serializer.DeserializeWrapper<ImmutableDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                IImmutableDictionary<string, string> obj = await Serializer.DeserializeWrapper<IImmutableDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                ImmutableSortedDictionary<string, string> obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml);
            }

            {
                Hashtable obj = await Serializer.DeserializeWrapper<Hashtable>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                SortedList obj = await Serializer.DeserializeWrapper<SortedList>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ImplementsDictionary_DictionaryOfString()
        {
            const string YamlString = """{"Hello":"World","Hello2":"World2"}""";
            const string ReorderedYamlString = """{"Hello2":"World2","Hello":"World"}""";

            {
                WrapperForIDictionary obj = await Serializer.DeserializeWrapper<WrapperForIDictionary>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                StringToStringDictionaryWrapper obj = await Serializer.DeserializeWrapper<StringToStringDictionaryWrapper>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                StringToStringSortedDictionaryWrapper obj = await Serializer.DeserializeWrapper<StringToStringSortedDictionaryWrapper>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                GenericIDictionaryWrapper<string, string> obj = await Serializer.DeserializeWrapper<GenericIDictionaryWrapper<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<GenericIReadOnlyDictionaryWrapper<string, string>>(YamlString));

                GenericIReadOnlyDictionaryWrapper<string, string> obj = new GenericIReadOnlyDictionaryWrapper<string, string>(new Dictionary<string, string>()
                {
                    { "Hello", "World" },
                    { "Hello2", "World2" },
                });
                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<StringToStringIImmutableDictionaryWrapper>(YamlString));

                StringToStringIImmutableDictionaryWrapper obj = new StringToStringIImmutableDictionaryWrapper(new Dictionary<string, string>()
                {
                    { "Hello", "World" },
                    { "Hello2", "World2" },
                });

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                HashtableWrapper obj = await Serializer.DeserializeWrapper<HashtableWrapper>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                SortedListWrapper obj = await Serializer.DeserializeWrapper<SortedListWrapper>(YamlString);
                Assert.Equal("World", ((YamlElement)obj["Hello"]).GetString());
                Assert.Equal("World2", ((YamlElement)obj["Hello2"]).GetString());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                GenericStructIDictionaryWrapper<string, string> obj = await Serializer.DeserializeWrapper<GenericStructIDictionaryWrapper<string, string>>(YamlString);
                Assert.Equal("World", obj["Hello"]);
                Assert.Equal("World2", obj["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                GenericStructIDictionaryWrapper<string, string>? obj = await Serializer.DeserializeWrapper<GenericStructIDictionaryWrapper<string, string>?>(YamlString);
                Assert.True(obj.HasValue);
                Assert.Equal("World", obj.Value["Hello"]);
                Assert.Equal("World2", obj.Value["Hello2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                GenericStructIDictionaryWrapper<string, string>? obj = await Serializer.DeserializeWrapper<GenericStructIDictionaryWrapper<string, string>?>("null");
                Assert.False(obj.HasValue);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("null", yaml);
            }

            {
                GenericStructIDictionaryWrapper<string, string> obj = default;
                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("{}", yaml);
            }

            {
                StructWrapperForIDictionary obj = default;
                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("{}", yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfObject()
        {
            {
                Dictionary<string, object> obj = await Serializer.DeserializeWrapper<Dictionary<string, object>>("""{"Key1":1}""");
                Assert.Equal(1, obj.Count);
                YamlElement element = (YamlElement)obj["Key1"];
                Assert.Equal(YamlValueKind.Number, element.ValueKind);
                Assert.Equal(1, element.GetInt32());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("""{"Key1":1}""", yaml);
            }

            {
                IDictionary<string, object> obj = await Serializer.DeserializeWrapper<IDictionary<string, object>>("""{"Key1":1}""");
                Assert.Equal(1, obj.Count);
                YamlElement element = (YamlElement)obj["Key1"];
                Assert.Equal(YamlValueKind.Number, element.ValueKind);
                Assert.Equal(1, element.GetInt32());

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("""{"Key1":1}""", yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ImplementsIDictionaryOfObject()
        {
            var input = new GenericIDictionaryWrapper<string, object>(new Dictionary<string, object>
            {
                { "Name", "David" },
                { "Age", 32 }
            });

            string yaml = await Serializer.SerializeWrapper(input, typeof(IDictionary<string, object>));
            Assert.Equal("""{"Name":"David","Age":32}""", yaml);

            IDictionary<string, object> obj = await Serializer.DeserializeWrapper<IDictionary<string, object>>(yaml);
            Assert.Equal(2, obj.Count);
            Assert.Equal("David", ((YamlElement)obj["Name"]).GetString());
            Assert.Equal(32, ((YamlElement)obj["Age"]).GetInt32());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ImplementsIDictionaryOfString()
        {
            var input = new GenericIDictionaryWrapper<string, string>(new Dictionary<string, string>
            {
                { "Name", "David" },
                { "Job", "Software Architect" }
            });

            string yaml = await Serializer.SerializeWrapper(input, typeof(IDictionary<string, string>));
            Assert.Equal("""{"Name":"David","Job":"Software Architect"}""", yaml);

            IDictionary<string, string> obj = await Serializer.DeserializeWrapper<IDictionary<string, string>>(yaml);
            Assert.Equal(2, obj.Count);
            Assert.Equal("David", obj["Name"]);
            Assert.Equal("Software Architect", obj["Job"]);
        }

        [Fact]
        public async Task PocoWithDictionaryObject()
        {
            PocoDictionary dict = await Serializer.DeserializeWrapper<PocoDictionary>("{\n\t\"key\" : {\"a\" : \"b\", \"c\" : \"d\"}}");
            Assert.Equal("b", dict.key["a"]);
            Assert.Equal("d", dict.key["c"]);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfObject_NonPrimitiveTypes()
        {
            // https://github.com/dotnet/runtime/issues/29504
            Dictionary<string, object> dictionary = new Dictionary<string, object>
            {
                ["key"] = new Poco { Id = 10 },
            };

            string yaml = await Serializer.SerializeWrapper(dictionary);
            Assert.Equal("""{"key":{"Id":10}}""", yaml);

            dictionary = await Serializer.DeserializeWrapper<Dictionary<string, object>>(yaml);
            Assert.Equal(1, dictionary.Count);
            YamlElement element = (YamlElement)dictionary["key"];
            Assert.Equal("""{"Id":10}""", element.ToString());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfList()
        {
            const string YamlString = """{"Key1":[1,2],"Key2":[3,4]}""";

            {
                IDictionary obj = await Serializer.DeserializeWrapper<IDictionary>(YamlString);

                Assert.Equal(2, obj.Count);

                int expectedNumber = 1;

                YamlElement element = (YamlElement)obj["Key1"];
                foreach (YamlElement value in element.EnumerateArray())
                {
                    Assert.Equal(expectedNumber++, value.GetInt32());
                }

                element = (YamlElement)obj["Key2"];
                foreach (YamlElement value in element.EnumerateArray())
                {
                    Assert.Equal(expectedNumber++, value.GetInt32());
                }

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                IDictionary<string, List<int>> obj = await Serializer.DeserializeWrapper<IDictionary<string, List<int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj["Key1"].Count);
                Assert.Equal(1, obj["Key1"][0]);
                Assert.Equal(2, obj["Key1"][1]);
                Assert.Equal(2, obj["Key2"].Count);
                Assert.Equal(3, obj["Key2"][0]);
                Assert.Equal(4, obj["Key2"][1]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                ImmutableDictionary<string, List<int>> obj = await Serializer.DeserializeWrapper<ImmutableDictionary<string, List<int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj["Key1"].Count);
                Assert.Equal(1, obj["Key1"][0]);
                Assert.Equal(2, obj["Key1"][1]);
                Assert.Equal(2, obj["Key2"].Count);
                Assert.Equal(3, obj["Key2"][0]);
                Assert.Equal(4, obj["Key2"][1]);

                string yaml = await Serializer.SerializeWrapper(obj);
                const string ReorderedYamlString = """{"Key2":[3,4],"Key1":[1,2]}""";
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }

            {
                IImmutableDictionary<string, List<int>> obj = await Serializer.DeserializeWrapper<IImmutableDictionary<string, List<int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj["Key1"].Count);
                Assert.Equal(1, obj["Key1"][0]);
                Assert.Equal(2, obj["Key1"][1]);
                Assert.Equal(2, obj["Key2"].Count);
                Assert.Equal(3, obj["Key2"][0]);
                Assert.Equal(4, obj["Key2"][1]);


                string yaml = await Serializer.SerializeWrapper(obj);
                const string ReorderedYamlString = """{"Key2":[3,4],"Key1":[1,2]}""";
                Assert.True(YamlString == yaml || ReorderedYamlString == yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfArray()
        {
            const string YamlString = """{"Key1":[1,2],"Key2":[3,4]}""";
            Dictionary<string, int[]> obj = await Serializer.DeserializeWrapper<Dictionary<string, int[]>>(YamlString);

            Assert.Equal(2, obj.Count);
            Assert.Equal(2, obj["Key1"].Length);
            Assert.Equal(1, obj["Key1"][0]);
            Assert.Equal(2, obj["Key1"][1]);
            Assert.Equal(2, obj["Key2"].Length);
            Assert.Equal(3, obj["Key2"][0]);
            Assert.Equal(4, obj["Key2"][1]);

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(YamlString, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ListOfDictionary()
        {
            const string YamlString = """[{"Key1":1,"Key2":2},{"Key1":3,"Key2":4}]""";

            {
                List<Dictionary<string, int>> obj = await Serializer.DeserializeWrapper<List<Dictionary<string, int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj[0].Count);
                Assert.Equal(1, obj[0]["Key1"]);
                Assert.Equal(2, obj[0]["Key2"]);
                Assert.Equal(2, obj[1].Count);
                Assert.Equal(3, obj[1]["Key1"]);
                Assert.Equal(4, obj[1]["Key2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }
            {
                List<ImmutableSortedDictionary<string, int>> obj = await Serializer.DeserializeWrapper<List<ImmutableSortedDictionary<string, int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj[0].Count);
                Assert.Equal(1, obj[0]["Key1"]);
                Assert.Equal(2, obj[0]["Key2"]);
                Assert.Equal(2, obj[1].Count);
                Assert.Equal(3, obj[1]["Key1"]);
                Assert.Equal(4, obj[1]["Key2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ArrayOfDictionary()
        {
            const string YamlString = """[{"Key1":1,"Key2":2},{"Key1":3,"Key2":4}]""";

            {
                Dictionary<string, int>[] obj = await Serializer.DeserializeWrapper<Dictionary<string, int>[]>(YamlString);

                Assert.Equal(2, obj.Length);
                Assert.Equal(2, obj[0].Count);
                Assert.Equal(1, obj[0]["Key1"]);
                Assert.Equal(2, obj[0]["Key2"]);
                Assert.Equal(2, obj[1].Count);
                Assert.Equal(3, obj[1]["Key1"]);
                Assert.Equal(4, obj[1]["Key2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                ImmutableSortedDictionary<string, int>[] obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, int>[]>(YamlString);

                Assert.Equal(2, obj.Length);
                Assert.Equal(2, obj[0].Count);
                Assert.Equal(1, obj[0]["Key1"]);
                Assert.Equal(2, obj[0]["Key2"]);
                Assert.Equal(2, obj[1].Count);
                Assert.Equal(3, obj[1]["Key1"]);
                Assert.Equal(4, obj[1]["Key2"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfDictionary()
        {
            const string YamlString = """{"Key1":{"Key1a":1,"Key1b":2},"Key2":{"Key2a":3,"Key2b":4}}""";

            {
                Dictionary<string, Dictionary<string, int>> obj = await Serializer.DeserializeWrapper<Dictionary<string, Dictionary<string, int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj["Key1"].Count);
                Assert.Equal(1, obj["Key1"]["Key1a"]);
                Assert.Equal(2, obj["Key1"]["Key1b"]);
                Assert.Equal(2, obj["Key2"].Count);
                Assert.Equal(3, obj["Key2"]["Key2a"]);
                Assert.Equal(4, obj["Key2"]["Key2b"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }

            {
                ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, int>> obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, int>>>(YamlString);

                Assert.Equal(2, obj.Count);
                Assert.Equal(2, obj["Key1"].Count);
                Assert.Equal(1, obj["Key1"]["Key1a"]);
                Assert.Equal(2, obj["Key1"]["Key1b"]);
                Assert.Equal(2, obj["Key2"].Count);
                Assert.Equal(3, obj["Key2"]["Key2a"]);
                Assert.Equal(4, obj["Key2"]["Key2b"]);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal(YamlString, yaml);

                yaml = await Serializer.SerializeWrapper<object>(obj);
                Assert.Equal(YamlString, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfDictionaryOfDictionary()
        {
            const string YamlString = """{"Key1":{"Key1":{"Key1":1,"Key2":2},"Key2":{"Key1":3,"Key2":4}},"Key2":{"Key1":{"Key1":5,"Key2":6},"Key2":{"Key1":7,"Key2":8}}}""";
            Dictionary<string, Dictionary<string, Dictionary<string, int>>> obj = await Serializer.DeserializeWrapper<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(YamlString);

            Assert.Equal(2, obj.Count);
            Assert.Equal(2, obj["Key1"].Count);
            Assert.Equal(2, obj["Key1"]["Key1"].Count);
            Assert.Equal(2, obj["Key1"]["Key2"].Count);

            Assert.Equal(1, obj["Key1"]["Key1"]["Key1"]);
            Assert.Equal(2, obj["Key1"]["Key1"]["Key2"]);
            Assert.Equal(3, obj["Key1"]["Key2"]["Key1"]);
            Assert.Equal(4, obj["Key1"]["Key2"]["Key2"]);

            Assert.Equal(2, obj["Key2"].Count);
            Assert.Equal(2, obj["Key2"]["Key1"].Count);
            Assert.Equal(2, obj["Key2"]["Key2"].Count);

            Assert.Equal(5, obj["Key2"]["Key1"]["Key1"]);
            Assert.Equal(6, obj["Key2"]["Key1"]["Key2"]);
            Assert.Equal(7, obj["Key2"]["Key2"]["Key1"]);
            Assert.Equal(8, obj["Key2"]["Key2"]["Key2"]);

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(YamlString, yaml);

            // Verify that typeof(object) doesn't interfere.
            yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(YamlString, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfArrayOfDictionary()
        {
            const string YamlString = """{"Key1":[{"Key1":1,"Key2":2},{"Key1":3,"Key2":4}],"Key2":[{"Key1":5,"Key2":6},{"Key1":7,"Key2":8}]}""";
            Dictionary<string, Dictionary<string, int>[]> obj = await Serializer.DeserializeWrapper<Dictionary<string, Dictionary<string, int>[]>>(YamlString);

            Assert.Equal(2, obj.Count);
            Assert.Equal(2, obj["Key1"].Length);
            Assert.Equal(2, obj["Key1"][0].Count);
            Assert.Equal(2, obj["Key1"][1].Count);

            Assert.Equal(1, obj["Key1"][0]["Key1"]);
            Assert.Equal(2, obj["Key1"][0]["Key2"]);
            Assert.Equal(3, obj["Key1"][1]["Key1"]);
            Assert.Equal(4, obj["Key1"][1]["Key2"]);

            Assert.Equal(2, obj["Key2"].Length);
            Assert.Equal(2, obj["Key2"][0].Count);
            Assert.Equal(2, obj["Key2"][1].Count);

            Assert.Equal(5, obj["Key2"][0]["Key1"]);
            Assert.Equal(6, obj["Key2"][0]["Key2"]);
            Assert.Equal(7, obj["Key2"][1]["Key1"]);
            Assert.Equal(8, obj["Key2"][1]["Key2"]);

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(YamlString, yaml);

            // Verify that typeof(object) doesn't interfere.
            yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(YamlString, yaml);
        }

        private interface IClass { }

        private class MyClass : IClass { }

        private class MyNonGenericDictionary : Dictionary<string, int> { }

        private class MyFactory : YamlConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(IClass) || typeToConvert == typeof(MyClass);
            }

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                if (typeToConvert == typeof(IClass))
                {
                    return new MyStuffConverterForIClass();
                }
                else if (typeToConvert == typeof(MyClass))
                {
                    return new MyStuffConverterForMyClass();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private class MyStuffConverterForIClass : YamlConverter<IClass>
        {
            public override IClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new MyClass();
            }

            public override void Write(Utf8YamlWriter writer, IClass value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(1);
            }
        }

        private class MyStuffConverterForMyClass : YamlConverter<MyClass>
        {
            public override MyClass Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new MyClass();
            }

            public override void Write(Utf8YamlWriter writer, MyClass value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(1);
            }
        }

        // This method generates 316 unique test cases for nested dictionaries up to 4
        // levels deep, along with matching JSON, encompassing the various planes of
        // dictionaries that can be combined: generic, non-generic, BCL, user-derived,
        // immutable, mutable, readonly, concurrent, specialized.
        [RequiresUnreferencedCode("Uses Type.MakeGenericType to construct nested dictionary types.")]
        [RequiresDynamicCode("Uses Type.MakeGenericType to construct nested dictionary types.")]
        private static IEnumerable<(Type, string)> NestedDictionaryTypeData()
        {
            string testYaml = """{"Key":1}""";

            List<Type> genericDictTypes = new List<Type>()
            {
                typeof(IDictionary<,>),
                typeof(ConcurrentDictionary<,>),
                typeof(GenericIDictionaryWrapper<,>),
            };

            List<Type> nonGenericDictTypes = new List<Type>()
            {
                typeof(Hashtable),
                typeof(OrderedDictionary),
            };

            List<Type> baseDictionaryTypes = new List<Type>
            {
                typeof(MyNonGenericDictionary),
                typeof(IReadOnlyDictionary<string, MyClass>),
                typeof(ConcurrentDictionary<string, int>),
                typeof(ImmutableDictionary<string, IClass>),
                typeof(GenericIDictionaryWrapper<string, int?>),
            };
            baseDictionaryTypes.AddRange(nonGenericDictTypes);

            // This method has exponential behavior which this depth value significantly impacts.
            // Don't change this value without checking how many test cases are generated and
            // how long the tests run for.
            int maxTestDepth = 4;

            HashSet<(Type, string)> tests = new HashSet<(Type, string)>();

            for (int i = 0; i < maxTestDepth; i++)
            {
                List<Type> newBaseTypes = new List<Type>();

                foreach (Type testType in baseDictionaryTypes)
                {
                    tests.Add((testType, testYaml));

                    foreach (Type genericType in genericDictTypes)
                    {
                        newBaseTypes.Add(genericType.MakeGenericType(typeof(string), testType));
                    }

                    newBaseTypes.AddRange(nonGenericDictTypes);
                }

                baseDictionaryTypes = newBaseTypes;
                testYaml = @"{""Key"":" + testYaml + "}";
            }

            return tests;
        }

#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Too many dynamically generated serializable types to manually add to a serialization context.")]
#endif
        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/66220", TestPlatforms.iOS | TestPlatforms.tvOS | TestPlatforms.MacCatalyst)]
        [RequiresUnreferencedCode("Uses Type.MakeGenericType to construct nested dictionary types.")]
        [RequiresDynamicCode("Uses Type.MakeGenericType to construct nested dictionary types.")]
        [Trait("category", "jsonoutput")]
        public async Task NestedDictionariesRoundtrip()
        {
            YamlSerializerOptions options = new YamlSerializerOptions();
            options.Converters.Add(new MyFactory());

            foreach ((Type dictionaryType, string testYaml) in NestedDictionaryTypeData())
            {
                object dict = await Serializer.DeserializeWrapper(testYaml, dictionaryType, options);
                Assert.Equal(testYaml, await Serializer.SerializeWrapper(dict, options));
            }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Needs full SimpleTestClass support.")]
#endif
        public async Task DictionaryOfClasses()
        {
            {
                IDictionary obj;

                {
                    string yaml = @"{""Key1"":" + SimpleTestClass.s_yaml + @",""Key2"":" + SimpleTestClass.s_yaml + "}";
                    obj = await Serializer.DeserializeWrapper<IDictionary>(yaml);
                    Assert.Equal(2, obj.Count);

                    if (obj["Key1"] is YamlElement element)
                    {
                        SimpleTestClass result = await Serializer.DeserializeWrapper<SimpleTestClass>(element.GetRawText());
                        result.Verify();
                    }
                    else
                    {
                        ((SimpleTestClass)obj["Key1"]).Verify();
                        ((SimpleTestClass)obj["Key2"]).Verify();
                    }
                }

                {
                    // We can't compare against the json string above because property ordering is not deterministic (based on reflection order)
                    // so just round-trip the json and compare.
                    string yaml = await Serializer.SerializeWrapper(obj);
                    obj = await Serializer.DeserializeWrapper<IDictionary>(yaml);
                    Assert.Equal(2, obj.Count);

                    if (obj["Key1"] is YamlElement element)
                    {
                        SimpleTestClass result = await Serializer.DeserializeWrapper<SimpleTestClass>(element.GetRawText());
                        result.Verify();
                    }
                    else
                    {
                        ((SimpleTestClass)obj["Key1"]).Verify();
                        ((SimpleTestClass)obj["Key2"]).Verify();
                    }
                }

                {
                    string yaml = await Serializer.SerializeWrapper<object>(obj);
                    obj = await Serializer.DeserializeWrapper<IDictionary>(yaml);
                    Assert.Equal(2, obj.Count);

                    if (obj["Key1"] is YamlElement element)
                    {
                        SimpleTestClass result = await Serializer.DeserializeWrapper<SimpleTestClass>(element.GetRawText());
                        result.Verify();
                    }
                    else
                    {
                        ((SimpleTestClass)obj["Key1"]).Verify();
                        ((SimpleTestClass)obj["Key2"]).Verify();
                    }
                }
            }

            {
                Dictionary<string, SimpleTestClass> obj;

                {
                    string yaml = @"{""Key1"":" + SimpleTestClass.s_yaml + @",""Key2"":" + SimpleTestClass.s_yaml + "}";
                    obj = await Serializer.DeserializeWrapper<Dictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }

                {
                    // We can't compare against the json string above because property ordering is not deterministic (based on reflection order)
                    // so just round-trip the json and compare.
                    string yaml = await Serializer.SerializeWrapper(obj);
                    obj = await Serializer.DeserializeWrapper<Dictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }

                {
                    string yaml = await Serializer.SerializeWrapper<object>(obj);
                    obj = await Serializer.DeserializeWrapper<Dictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }
            }

            {
                ImmutableSortedDictionary<string, SimpleTestClass> obj;

                {
                    string yaml = @"{""Key1"":" + SimpleTestClass.s_yaml + @",""Key2"":" + SimpleTestClass.s_yaml + "}";
                    obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }

                {
                    // We can't compare against the json string above because property ordering is not deterministic (based on reflection order)
                    // so just round-trip the json and compare.
                    string yaml = await Serializer.SerializeWrapper(obj);
                    obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }

                {
                    string yaml = await Serializer.SerializeWrapper<object>(obj);
                    obj = await Serializer.DeserializeWrapper<ImmutableSortedDictionary<string, SimpleTestClass>>(yaml);
                    Assert.Equal(2, obj.Count);
                    obj["Key1"].Verify();
                    obj["Key2"].Verify();
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnicodePropertyNames()
        {
            var options = new YamlSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            {
                Dictionary<string, int> obj;

                obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>("""{"A\u0467":1}""");
                Assert.Equal(1, obj["A\u0467"]);

                // Specifying encoder on options does not impact deserialize.
                obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>("""{"A\u0467":1}""", options);
                Assert.Equal(1, obj["A\u0467"]);

                string yaml;
                // Verify the name is escaped after serialize.
                yaml = await Serializer.SerializeWrapper(obj);
                Assert.Equal("""{"A\u0467":1}""", yaml);

                // Verify with encoder.
                yaml = await Serializer.SerializeWrapper(obj, options);
                Assert.Equal("{\"A\u0467\":1}", yaml);
            }

            {
                // We want to go over StackallocByteThreshold=256 to force a pooled allocation, so this property is 200 chars and 400 bytes.
                const int charsInProperty = 200;

                string longPropertyName = new string('\u0467', charsInProperty);

                Dictionary<string, int> obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>($"{{\"{longPropertyName}\":1}}");
                Assert.Equal(1, obj[longPropertyName]);

                // Verify the name is escaped after serialize.
                string yaml = await Serializer.SerializeWrapper(obj);

                // Duplicate the unicode character 'charsInProperty' times.
                string longPropertyNameEscaped = new StringBuilder().Insert(0, @"\u0467", charsInProperty).ToString();

                string expectedYaml = $"{{\"{longPropertyNameEscaped}\":1}}";
                Assert.Equal(expectedYaml, yaml);

                // Verify the name is unescaped after deserialize.
                obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>(yaml);
                Assert.Equal(1, obj[longPropertyName]);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomEscapingOnPropertyNameAndValue()
        {
            var dict = new Dictionary<string, string>();
            dict.Add("A\u046701", "Value\u0467");

            // Baseline with no escaping.
            var yaml = await Serializer.SerializeWrapper(dict);
            Assert.Equal("{\"A\\u046701\":\"Value\\u0467\"}", yaml);

            // Enable escaping.
            var options = new YamlSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            yaml = await Serializer.SerializeWrapper(dict, options);
            Assert.Equal("{\"A\u046701\":\"Value\u0467\"}", yaml);
        }

        [Fact]
        public async Task ObjectToStringFail()
        {
            // Baseline
            string yaml = """{"MyDictionary":{"Key":"Value"}}""";
            await Serializer.DeserializeWrapper<Dictionary<string, object>>(yaml);

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml));
        }

        [Fact]
        public async Task ObjectToYamlElement()
        {
            string yaml = """{"MyDictionary":{"Key":"Value"}}""";
            Dictionary<string, YamlElement> result = await Serializer.DeserializeWrapper<Dictionary<string, YamlElement>>(yaml);
            YamlElement element = result["MyDictionary"];
            Assert.Equal(YamlValueKind.Object, element.ValueKind);
            Assert.Equal("Value", element.GetProperty("Key").GetString());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Hashtable()
        {
            const string Yaml = """{"Key":"Value"}""";

            IDictionary ht = new Hashtable();
            ht.Add("Key", "Value");
            string yaml = await Serializer.SerializeWrapper(ht);

            Assert.Equal(Yaml, yaml);

            ht = await Serializer.DeserializeWrapper<IDictionary>(yaml);
            Assert.IsType<YamlElement>(ht["Key"]);
            Assert.Equal("Value", ((YamlElement)ht["Key"]).GetString());

            // Verify round-tripped JSON.
            yaml = await Serializer.SerializeWrapper(ht);
            Assert.Equal(Yaml, yaml);
        }

        [Fact]
        public async Task DeserializeDictionaryWithDuplicateKeys()
        {
            // Non-generic IDictionary case.
            IDictionary iDictionary = await Serializer.DeserializeWrapper<IDictionary>("""{"Hello":"World", "Hello":"NewValue"}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal("NewValue", iDictionary["Hello"].ToString());

            // Generic IDictionary case.
            IDictionary<string, string> iNonGenericDictionary = await Serializer.DeserializeWrapper<IDictionary<string, string>>("""{"Hello":"World", "Hello":"NewValue"}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal("NewValue", iNonGenericDictionary["Hello"]);

            IDictionary<string, object> iNonGenericObjectDictionary = await Serializer.DeserializeWrapper<IDictionary<string, object>>("""{"Hello":"World", "Hello":"NewValue"}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal("NewValue", iNonGenericObjectDictionary["Hello"].ToString());

            // Strongly-typed IDictionary<,> case.
            Dictionary<string, string> dictionary = await Serializer.DeserializeWrapper<Dictionary<string, string>>("""{"Hello":"World", "Hello":"NewValue"}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal("NewValue", dictionary["Hello"]);

            dictionary = await Serializer.DeserializeWrapper<Dictionary<string, string>>("""{"Hello":"World", "myKey" : "myValue", "Hello":"NewValue"}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal("NewValue", dictionary["Hello"]);

            // Weakly-typed IDictionary case.
            Dictionary<string, object> dictionaryObject = await Serializer.DeserializeWrapper<Dictionary<string, object>>("""{"Hello":"World", "Hello": null}""", YamlTestSerializerOptions.LastWins);
            Assert.Null(dictionaryObject["Hello"]);
        }

        [Fact]
        public async Task DeserializeDictionaryWithDuplicateProperties()
        {
            PocoDuplicate foo = await Serializer.DeserializeWrapper<PocoDuplicate>("""{"BoolProperty": false, "BoolProperty": true}""", YamlTestSerializerOptions.LastWins);
            Assert.True(foo.BoolProperty);

            foo = await Serializer.DeserializeWrapper<PocoDuplicate>("""{"BoolProperty": false, "IntProperty" : 1, "BoolProperty": true , "IntProperty" : 2}""", YamlTestSerializerOptions.LastWins);
            Assert.True(foo.BoolProperty);
            Assert.Equal(2, foo.IntProperty);

            foo = await Serializer.DeserializeWrapper<PocoDuplicate>("""{"DictProperty" : {"a" : "b", "c" : "d"},"DictProperty" : {"b" : "b", "c" : "e"}}""", YamlTestSerializerOptions.LastWins);
            Assert.Equal(2, foo.DictProperty.Count); // We don't concat.
            Assert.Equal("e", foo.DictProperty["c"]);
        }

        [Fact]
        public async Task DeserializeDictionaryWithDuplicatePropertiesThrow()
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;

            Exception ex = await Assert.ThrowsAsync<YamlException>(() =>
                Serializer.DeserializeWrapper<PocoDuplicate>("""{"BoolProperty": false, "BoolProperty": true}""", options));
            Assert.Contains("Duplicate", ex.Message);

            ex = await Assert.ThrowsAsync<YamlException>(() =>
                Serializer.DeserializeWrapper<PocoDuplicate>("""{"BoolProperty": false, "IntProperty" : 1, "BoolProperty": true , "IntProperty" : 2}""", options));
            Assert.Contains("Duplicate", ex.Message);

            ex = await Assert.ThrowsAsync<YamlException>(() =>
                Serializer.DeserializeWrapper<PocoDuplicate>("""{"DictProperty" : {"a" : "b", "c" : "d"},"DictProperty" : {"b" : "b", "c" : "e"}}""", options));
            Assert.Contains("Duplicate", ex.Message);
        }

        public class PocoDuplicate
        {
            public bool BoolProperty { get; set; }
            public int IntProperty { get; set; }
            public Dictionary<string, string> DictProperty { get; set; }
        }

        [Fact]
        public async Task DeserializeNestedDictionaryWithDuplicatePropertiesThrow()
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;

            Exception ex = await Assert.ThrowsAsync<YamlException>(() =>
                Serializer.DeserializeWrapper<PocoDictionary>("""{"key": {"A": "a", "A": "a"}}""", options));
            Assert.Contains("Duplicate", ex.Message);

            Assert.Equal("a", (await Serializer.DeserializeWrapper<PocoDictionary>("""{"key": {"A": "a", "A": "a"}}""", YamlTestSerializerOptions.LastWins)).key["A"]);
        }

        public static IEnumerable<object[]> TestDictionaryTypes =
            CollectionTestTypes.DeserializableNonGenericDictionaryTypes()
                .Concat(CollectionTestTypes.DeserializableDictionaryTypes<string, string>())
                .Concat(CollectionTestTypes.DeserializableDictionaryTypes<int, string>())
                .Select<object, object[]>(x => [x]);

        [Theory]
        [InlineData(typeof(ImmutableSortedDictionary<string, string>))]
        [InlineData(typeof(DictionaryThatOnlyImplementsIDictionaryOfStringTValue<string>))]
        [MemberData(nameof(TestDictionaryTypes))]
        public async Task DeserializeBuiltInDictionaryWithDuplicatePropertiesThrow(Type t)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;
            
            Exception ex = await Assert.ThrowsAsync<YamlException>(() =>
                Serializer.DeserializeWrapper("""{"1": "a", "1": "a"}""", t, options));
            Assert.Contains("Duplicate", ex.Message);

            // Assert no throw
            _ = await Serializer.DeserializeWrapper("""{"1": "a", "1": "a"}""", t, YamlTestSerializerOptions.LastWins);
        }

        public static IEnumerable<object[]> TestStringKeyDictionaryTypes =
            CollectionTestTypes.DeserializableNonGenericDictionaryTypes()
                .Concat(CollectionTestTypes.DeserializableDictionaryTypes<string, string>())
                .Select<object, object[]>(x => [x]);

        [Theory]
        [InlineData(typeof(ImmutableSortedDictionary<string, string>))]
        [InlineData(typeof(DictionaryThatOnlyImplementsIDictionaryOfStringTValue<string>))]
        [MemberData(nameof(TestStringKeyDictionaryTypes))]
        public async Task DeserializeCaseInsensitiveBuiltInDictionaryWithDuplicatePropertiesNoThrow(Type t)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;

            await Serializer.DeserializeWrapper("""{"a": "a", "A": "a"}""", t, options); // Assert no throw
        }

        public class ClassWithPopulatedDictionaryAndNoSetter
        {
            public ClassWithPopulatedDictionaryAndNoSetter()
            {
                MyImmutableDictionary = MyImmutableDictionary.Add("Key", "Value");
            }

            public Dictionary<string, string> MyDictionary { get; } = new Dictionary<string, string>() { { "Key", "Value" } };
            public ImmutableDictionary<string, string> MyImmutableDictionary { get; } = ImmutableDictionary.Create<string, string>();
        }

        [Fact]
        public async Task ClassWithNoSetterAndDictionary()
        {
            // We don't attempt to deserialize into dictionaries without a setter.
            string yaml = """{"MyDictionary":{"Key1":"Value1", "Key2":"Value2"}}""";
            ClassWithPopulatedDictionaryAndNoSetter obj = await Serializer.DeserializeWrapper<ClassWithPopulatedDictionaryAndNoSetter>(yaml);
            Assert.Equal(1, obj.MyDictionary.Count);
        }

        [Fact]
        public async Task ClassWithNoSetterAndImmutableDictionary()
        {
            // We don't attempt to deserialize into dictionaries without a setter.
            string yaml = """{"MyImmutableDictionary":{"Key1":"Value1", "Key2":"Value2"}}""";
            ClassWithPopulatedDictionaryAndNoSetter obj = await Serializer.DeserializeWrapper<ClassWithPopulatedDictionaryAndNoSetter>(yaml);
            Assert.Equal(1, obj.MyImmutableDictionary.Count);
        }

        public class ClassWithIgnoredDictionary1
        {
            public Dictionary<string, int> Parsed1 { get; set; }
            public Dictionary<string, int> Parsed2 { get; set; }
            public Dictionary<string, int>? Skipped3 { get; }
        }

        public class ClassWithIgnoredDictionary2
        {
            public IDictionary<string, int> Parsed1 { get; set; }
            public IDictionary<string, int>? Skipped2 { get; }
            public IDictionary<string, int> Parsed3 { get; set; }
        }

        public class ClassWithIgnoredDictionary3
        {
            public Dictionary<string, int> Parsed1 { get; set; }
            public Dictionary<string, int>? Skipped2 { get; }
            public Dictionary<string, int>? Skipped3 { get; }
        }

        public class ClassWithIgnoredDictionary4
        {
            public Dictionary<string, int>? Skipped1 { get; }
            public Dictionary<string, int> Parsed2 { get; set; }
            public Dictionary<string, int> Parsed3 { get; set; }
        }

        public class ClassWithIgnoredDictionary5
        {
            public Dictionary<string, int>? Skipped1 { get; }
            public Dictionary<string, int> Parsed2 { get; set; }
            public Dictionary<string, int>? Skipped3 { get; }
        }

        public class ClassWithIgnoredDictionary6
        {
            public Dictionary<string, int>? Skipped1 { get; }
            public Dictionary<string, int>? Skipped2 { get; }
            public Dictionary<string, int> Parsed3 { get; set; }
        }

        public class ClassWithIgnoredDictionary7
        {
            public Dictionary<string, int>? Skipped1 { get; }
            public Dictionary<string, int>? Skipped2 { get; }
            public Dictionary<string, int>? Skipped3 { get; }
        }

        public class ClassWithIgnoredIDictionary
        {
            public IDictionary<string, int> Parsed1 { get; set; }
            public IDictionary<string, int>? Skipped2 { get; }
            public IDictionary<string, int> Parsed3 { get; set; }
        }

        public class ClassWithIgnoreAttributeDictionary
        {
            public Dictionary<string, int> Parsed1 { get; set; }
            [YamlIgnore] public Dictionary<string, int>? Skipped2 { get; set; } // Note this has a setter.
            public Dictionary<string, int> Parsed3 { get; set; }
        }

        public class ClassWithIgnoredImmutableDictionary
        {
            public ImmutableDictionary<string, int> Parsed1 { get; set; }
            public ImmutableDictionary<string, int>? Skipped2 { get; }
            public ImmutableDictionary<string, int> Parsed3 { get; set; }
        }

        [Theory]
        [InlineData("""{"Parsed1":{"Key":1},"Parsed3":{"Key":2}}""")] // No value for skipped property
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":{}, "Parsed3":{"Key":2}}""")] // Empty object {} skipped
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":null, "Parsed3":{"Key":2}}""")] // null object skipped
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":{"Key":9}, "Parsed3":{"Key":2}}""")] // Valid "int" values skipped
        // Invalid "int" values:
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":{"Key":[1,2,3]}, "Parsed3":{"Key":2}}""")]
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":{"Key":{}}, "Parsed3":{"Key":2}}""")]
        [InlineData("""{"Parsed1":{"Key":1},"Skipped2":{"Key":null}, "Parsed3":{"Key":2}}""")]
        public async Task IgnoreDictionaryProperty(string yaml)
        {
            // Verify deserialization
            ClassWithIgnoredDictionary2 obj = await Serializer.DeserializeWrapper<ClassWithIgnoredDictionary2>(yaml);
            Assert.Equal(1, obj.Parsed1.Count);
            Assert.Equal(1, obj.Parsed1["Key"]);
            Assert.Null(obj.Skipped2);
            Assert.Equal(1, obj.Parsed3.Count);
            Assert.Equal(2, obj.Parsed3["Key"]);

            // Round-trip and verify.
            string yamlRoundTripped = await Serializer.SerializeWrapper(obj);
            ClassWithIgnoredDictionary2 objRoundTripped = await Serializer.DeserializeWrapper<ClassWithIgnoredDictionary2>(yamlRoundTripped);
            Assert.Equal(1, objRoundTripped.Parsed1.Count);
            Assert.Equal(1, objRoundTripped.Parsed1["Key"]);
            Assert.Null(objRoundTripped.Skipped2);
            Assert.Equal(1, objRoundTripped.Parsed3.Count);
            Assert.Equal(2, objRoundTripped.Parsed3["Key"]);
        }

        [Fact]
        public async Task IgnoreDictionaryPropertyWithDifferentOrdering()
        {
            // Verify all combinations of 3 properties with at least one ignore.
            await VerifyIgnore<ClassWithIgnoredDictionary1>(false, false, true);
            await VerifyIgnore<ClassWithIgnoredDictionary2>(false, true, false);
            await VerifyIgnore<ClassWithIgnoredDictionary3>(false, true, true);
            await VerifyIgnore<ClassWithIgnoredDictionary4>(true, false, false);
            await VerifyIgnore<ClassWithIgnoredDictionary5>(true, false, true);
            await VerifyIgnore<ClassWithIgnoredDictionary6>(true, true, false);
            await VerifyIgnore<ClassWithIgnoredDictionary7>(true, true, true);

            // Verify single case for IDictionary, [Ignore] and ImmutableDictionary.
            // Also specify addMissing to add additional skipped JSON that does not have a corresponding property.
            await VerifyIgnore<ClassWithIgnoredIDictionary>(false, true, false, addMissing: true);
            await VerifyIgnore<ClassWithIgnoreAttributeDictionary>(false, true, false, addMissing: true);
            await VerifyIgnore<ClassWithIgnoredImmutableDictionary>(false, true, false, addMissing: true);
        }

        private async Task VerifyIgnore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(bool skip1, bool skip2, bool skip3, bool addMissing = false)
        {
            static IDictionary<string, int> GetProperty(T objectToVerify, string propertyName)
            {
                return (IDictionary<string, int>)typeof(T).GetProperty(propertyName).GetValue(objectToVerify);
            }

            void Verify(T objectToVerify)
            {
                if (skip1)
                {
                    Assert.Null(GetProperty(objectToVerify, "Skipped1"));
                }
                else
                {
                    Assert.Equal(1, GetProperty(objectToVerify, "Parsed1")["Key"]);
                }

                if (skip2)
                {
                    Assert.Null(GetProperty(objectToVerify, "Skipped2"));
                }
                else
                {
                    Assert.Equal(2, GetProperty(objectToVerify, "Parsed2")["Key"]);
                }

                if (skip3)
                {
                    Assert.Null(GetProperty(objectToVerify, "Skipped3"));
                }
                else
                {
                    Assert.Equal(3, GetProperty(objectToVerify, "Parsed3")["Key"]);
                }
            }

            // Tests that the parser picks back up after skipping/draining ignored elements.
            StringBuilder yaml = new StringBuilder(@"{");

            if (addMissing)
            {
                yaml.Append("""
                    "MissingProp1": {},
                    """);
            }

            if (skip1)
            {
                yaml.Append("""
                    "Skipped1":{},
                    """);
            }
            else
            {
                yaml.Append("""
                    "Parsed1":{"Key":1},
                    """);
            }

            if (addMissing)
            {
                yaml.Append("""
                    "MissingProp2": null,
                    """);
            }

            if (skip2)
            {
                yaml.Append("""
                    "Skipped2":{},
                    """);
            }
            else
            {
                yaml.Append("""
                    "Parsed2":{"Key":2},
                    """);
            }

            if (addMissing)
            {
                yaml.Append("""
                    "MissingProp3": {"ABC":{}},
                    """);
            }

            if (skip3)
            {
                yaml.Append("""
                    "Skipped3":{}}
                    """);
            }
            else
            {
                yaml.Append("""
                    "Parsed3":{"Key":3}}
                    """);
            }

            // Deserialize and verify.
            string yamlString = yaml.ToString();
            T obj = await Serializer.DeserializeWrapper<T>(yamlString);
            Verify(obj);

            // Round-trip and verify.
            // Any skipped properties due to lack of a setter will now be "null" when serialized instead of "{}".
            string yamlStringRoundTripped = await Serializer.SerializeWrapper(obj);
            T objRoundTripped = await Serializer.DeserializeWrapper<T>(yamlStringRoundTripped);
            Verify(objRoundTripped);
        }

        public class ClassWithPopulatedDictionaryAndSetter
        {
            public ClassWithPopulatedDictionaryAndSetter()
            {
                MyImmutableDictionary = MyImmutableDictionary.Add("Key", "Value");
            }

            public Dictionary<string, string> MyDictionary { get; set; } = new Dictionary<string, string>() { { "Key", "Value" } };
            public ImmutableDictionary<string, string> MyImmutableDictionary { get; set; } = ImmutableDictionary.Create<string, string>();
        }

        [Fact]
        public async Task ClassWithPopulatedDictionary()
        {
            // We replace the contents.
            string yaml = """{"MyDictionary":{"Key1":"Value1", "Key2":"Value2"}}""";
            ClassWithPopulatedDictionaryAndSetter obj = await Serializer.DeserializeWrapper<ClassWithPopulatedDictionaryAndSetter>(yaml);
            Assert.Equal(2, obj.MyDictionary.Count);
        }

        [Fact]
        public async Task ClassWithPopulatedImmutableDictionary()
        {
            // We replace the contents.
            string yaml = """{"MyImmutableDictionary":{"Key1":"Value1", "Key2":"Value2"}}""";
            ClassWithPopulatedDictionaryAndSetter obj = await Serializer.DeserializeWrapper<ClassWithPopulatedDictionaryAndSetter>(yaml);
            Assert.Equal(2, obj.MyImmutableDictionary.Count);
        }

        [Fact]
        public async Task DictionaryNotSupported()
        {
            string yaml = """{"MyDictionary":{"Key":"Value"}}""";

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithNotSupportedDictionary>(yaml));

            // The exception contains the key type.
            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
            Assert.Contains("$.MyDictionary", ex.Message);
        }

        [Fact]
        public async Task DictionaryNotSupportedButIgnored()
        {
            string yaml = """{"MyDictionary":{"Key":1}}""";
            ClassWithNotSupportedDictionaryButIgnored obj = await Serializer.DeserializeWrapper<ClassWithNotSupportedDictionaryButIgnored>(yaml);
            Assert.Null(obj.MyDictionary);
        }

        // https://github.com/dotnet/runtime/issues/29933
        [Fact]
        public async Task Serialize_IDictionaryOfPoco()
        {
            // Arrange
            var value = new AllSingleUpperPropertiesParent()
            {
                Child = new Dictionary<string, AllSingleUpperProperties_Child>()
                {
                    ["1"] = new AllSingleUpperProperties_Child()
                    {
                        A = "1",
                        B = string.Empty,
                        C = Array.Empty<string>(),
                        D = Array.Empty<string>(),
                        F = Array.Empty<string>(),
                        K = Array.Empty<string>(),
                    }
                }
            };

            var actual = await Serializer.SerializeWrapper(value, new YamlSerializerOptions() { PropertyNamingPolicy = YamlNamingPolicy.CamelCase });

            // Assert
            Assert.NotNull(actual);
            Assert.NotEmpty(actual);
        }

        // https://github.com/dotnet/runtime/issues/29933
        [Fact]
        public async Task Deserialize_IDictionaryOfPoco()
        {
            // Arrange
            string yaml = "{\"child\":{\"1\":{\"a\":\"1\",\"b\":\"\",\"c\":[],\"d\":[],\"e\":null,\"f\":[],\"g\":null,\"h\":null,\"i\":null,\"j\":null,\"k\":[]}}}";

            var actual = await Serializer.DeserializeWrapper<AllSingleUpperPropertiesParent>(yaml, new YamlSerializerOptions() { PropertyNamingPolicy = YamlNamingPolicy.CamelCase });

            // Assert
            Assert.NotNull(actual);
            Assert.NotNull(actual.Child);
            Assert.Equal(1, actual.Child.Count);
            Assert.True(actual.Child.ContainsKey("1"));
            Assert.Equal("1", actual.Child["1"].A);
        }

        // https://github.com/dotnet/runtime/issues/29893
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ShouldHandleNullInDictionaries_Serialize()
        {
            var value = new ClassWithDictionaryOfString_ChildWithDictionaryOfString()
            {
                Test = "value1",
                Child = new ClassWithDictionaryOfString()
            };

            var actual = await Serializer.SerializeWrapper(value);
            Assert.Equal("{\"Test\":\"value1\",\"Dict\":null,\"Child\":{\"Test\":null,\"Dict\":null}}", actual);
        }

        // https://github.com/dotnet/runtime/issues/29893
        [Fact]
        public async Task ShouldHandleNullInDictionaries_Deserialize()
        {
            var yaml = "{\"Test\":\"value1\",\"Dict\":null,\"Child\":{\"Test\":null,\"Dict\":null}}";
            ClassWithDictionaryOfString_ChildWithDictionaryOfString actual = await Serializer.DeserializeWrapper<ClassWithDictionaryOfString_ChildWithDictionaryOfString>(yaml);

            Assert.Equal("value1", actual.Test);
            Assert.Null(actual.Dict);
            Assert.NotNull(actual.Child);
            Assert.Null(actual.Child.Dict);
            Assert.Null(actual.Child.Test);
        }

        // https://github.com/dotnet/runtime/issues/29893
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ShouldHandleNullInDictionaries_Serialize_IgnoreNullValues()
        {
            var value = new ClassWithDictionaryOfString_ChildWithDictionaryOfString()
            {
                Test = "value1",
                Child = new ClassWithDictionaryOfString()
            };

            var actual = await Serializer.SerializeWrapper(value, new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull });
            Assert.Equal("{\"Test\":\"value1\",\"Child\":{}}", actual);
        }

        // https://github.com/dotnet/runtime/issues/29893
        [Fact]
        public async Task ShouldHandleNullInDictionaries_Deserialize_IgnoreNullValues()
        {
            var yaml = "{\"Test\":\"value1\",\"Child\":{}}";
            ClassWithDictionaryOfString_ChildWithDictionaryOfString actual = await Serializer.DeserializeWrapper<ClassWithDictionaryOfString_ChildWithDictionaryOfString>(yaml);

            Assert.Equal("value1", actual.Test);
            Assert.Null(actual.Dict);
            Assert.NotNull(actual.Child);
            Assert.Null(actual.Child.Dict);
            Assert.Null(actual.Child.Test);
        }

        // https://github.com/dotnet/runtime/issues/29888
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryWithNullShouldPreserveOrder_Serialize()
        {
            var dictionaryFirst = new ClassWithDictionaryAndProperty_DictionaryFirst()
            {
                Test = "value1"
            };

            var actual = await Serializer.SerializeWrapper(dictionaryFirst);
            Assert.Equal("{\"Dict\":null,\"Test\":\"value1\"}", actual);

            var dictionaryLast = new ClassWithDictionaryAndProperty_DictionaryLast()
            {
                Test = "value1"
            };

            actual = await Serializer.SerializeWrapper(dictionaryLast);
            Assert.Equal("{\"Test\":\"value1\",\"Dict\":null}", actual);
        }

        // https://github.com/dotnet/runtime/issues/29888
        [Fact]
        public async Task DictionaryWithNullShouldPreserveOrder_Deserialize()
        {
            var yaml = "{\"Dict\":null,\"Test\":\"value1\"}";
            ClassWithDictionaryAndProperty_DictionaryFirst dictionaryFirst = await Serializer.DeserializeWrapper<ClassWithDictionaryAndProperty_DictionaryFirst>(yaml);

            Assert.Equal("value1", dictionaryFirst.Test);
            Assert.Null(dictionaryFirst.Dict);

            yaml = "{\"Test\":\"value1\",\"Dict\":null}";
            ClassWithDictionaryAndProperty_DictionaryLast dictionaryLast = await Serializer.DeserializeWrapper<ClassWithDictionaryAndProperty_DictionaryLast>(yaml);

            Assert.Equal("value1", dictionaryLast.Test);
            Assert.Null(dictionaryLast.Dict);
        }

        // https://github.com/dotnet/runtime/issues/29888
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryWithNullShouldPreserveOrder_Serialize_IgnoreNullValues()
        {
            var dictionaryFirst = new ClassWithDictionaryAndProperty_DictionaryFirst()
            {
                Test = "value1"
            };

            var actual = await Serializer.SerializeWrapper(dictionaryFirst, new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull });
            Assert.Equal("{\"Test\":\"value1\"}", actual);

            var dictionaryLast = new ClassWithDictionaryAndProperty_DictionaryLast()
            {
                Test = "value1"
            };

            actual = await Serializer.SerializeWrapper(dictionaryLast, new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull });
            Assert.Equal("{\"Test\":\"value1\"}", actual);
        }

        // https://github.com/dotnet/runtime/issues/29888
        [Fact]
        public async Task DictionaryWithNullShouldPreserveOrder_Deserialize_IgnoreNullValues()
        {
            var yaml = "{\"Test\":\"value1\"}";
            ClassWithDictionaryAndProperty_DictionaryFirst dictionaryFirst = await Serializer.DeserializeWrapper<ClassWithDictionaryAndProperty_DictionaryFirst>(yaml);

            Assert.Equal("value1", dictionaryFirst.Test);
            Assert.Null(dictionaryFirst.Dict);

            yaml = "{\"Test\":\"value1\"}";
            ClassWithDictionaryAndProperty_DictionaryLast dictionaryLast = await Serializer.DeserializeWrapper<ClassWithDictionaryAndProperty_DictionaryLast>(yaml);

            Assert.Equal("value1", dictionaryLast.Test);
            Assert.Null(dictionaryLast.Dict);
        }

        [Fact]
        public async Task NullDictionaryValuesShouldDeserializeAsNull()
        {
            const string yaml =
                    @"{" +
                        @"""StringVals"":{" +
                            @"""key"":null" +
                        @"}," +
                        @"""ObjectVals"":{" +
                            @"""key"":null" +
                        @"}," +
                        @"""StringDictVals"":{" +
                            @"""key"":null" +
                        @"}," +
                        @"""ObjectDictVals"":{" +
                            @"""key"":null" +
                        @"}," +
                        @"""ClassVals"":{" +
                            @"""key"":null" +
                        @"}" +
                    @"}";

            SimpleClassWithDictionaries obj = await Serializer.DeserializeWrapper<SimpleClassWithDictionaries>(yaml);
            Assert.Null(obj.StringVals["key"]);
            Assert.Null(obj.ObjectVals["key"]);
            Assert.Null(obj.StringDictVals["key"]);
            Assert.Null(obj.ObjectDictVals["key"]);
            Assert.Null(obj.ClassVals["key"]);
        }

        public class ClassWithNotSupportedDictionary
        {
            public Dictionary<int[,], int> MyDictionary { get; set; }
        }

        public class ClassWithNotSupportedDictionaryButIgnored
        {
            [YamlIgnore] public Dictionary<int[,], int> MyDictionary { get; set; }
        }

        public class AllSingleUpperPropertiesParent
        {
            public IDictionary<string, AllSingleUpperProperties_Child> Child { get; set; }
        }

        public class AllSingleUpperProperties_Child
        {
            public string A { get; set; }
            public string B { get; set; }
            public string[] C { get; set; }
            public string[] D { get; set; }
            public bool? E { get; set; }
            public string[] F { get; set; }
            public DateTimeOffset? G { get; set; }
            public DateTimeOffset? H { get; set; }
            public int? I { get; set; }
            public int? J { get; set; }
            public string[] K { get; set; }
        }

        public class ClassWithDictionaryOfString_ChildWithDictionaryOfString
        {
            public string Test { get; set; }
            public Dictionary<string, string>? Dict { get; set; }
            public ClassWithDictionaryOfString Child { get; set; }
        }

        public class ClassWithDictionaryOfString
        {
            public string? Test { get; set; }
            public Dictionary<string, string>? Dict { get; set; }
        }

        public class ClassWithDictionaryAndProperty_DictionaryLast
        {
            public string Test { get; set; }
            public Dictionary<string, string>? Dict { get; set; }
        }

        public class ClassWithDictionaryAndProperty_DictionaryFirst
        {
            public Dictionary<string, string>? Dict { get; set; }
            public string Test { get; set; }
        }

        public class SimpleClassWithDictionaries
        {
            public Dictionary<string, string> StringVals { get; set; }
            public Dictionary<string, object> ObjectVals { get; set; }
            public Dictionary<string, Dictionary<string, string>> StringDictVals { get; set; }
            public Dictionary<string, Dictionary<string, object>> ObjectDictVals { get; set; }
            public Dictionary<string, SimpleClassWithDictionaries> ClassVals { get; set; }
        }

        public class DictionaryThatOnlyImplementsIDictionaryOfStringTValue<TValue> : IDictionary<string, TValue>
        {
            IDictionary<string, TValue> _inner = new Dictionary<string, TValue>();

            public TValue this[string key]
            {
                get
                {
                    return _inner[key];
                }
                set
                {
                    _inner[key] = value;
                }
            }

            public ICollection<string> Keys => _inner.Keys;

            public ICollection<TValue> Values => _inner.Values;

            public int Count => _inner.Count;

            public bool IsReadOnly => _inner.IsReadOnly;

            public void Add(string key, TValue value)
            {
                _inner.Add(key, value);
            }

            public void Add(KeyValuePair<string, TValue> item)
            {
                _inner.Add(item);
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(KeyValuePair<string, TValue> item)
            {
                return _inner.Contains(item);
            }

            public bool ContainsKey(string key)
            {
                return _inner.ContainsKey(key);
            }

            public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
            {
                // CopyTo should not be called.
                throw new NotImplementedException();
            }

            public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
            {
                // Don't return results directly from _inner since that will return an enumerator that returns
                // IDictionaryEnumerator which should not require.
                foreach (KeyValuePair<string, TValue> keyValuePair in _inner)
                {
                    yield return keyValuePair;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                // This GetEnumerator() should not be called.
                throw new NotImplementedException();
            }

            public bool Remove(string key)
            {
                // Remove should not be called.
                throw new NotImplementedException();
            }

            public bool Remove(KeyValuePair<string, TValue> item)
            {
                // Remove should not be called.
                throw new NotImplementedException();
            }

            public bool TryGetValue(string key, out TValue value)
            {
                return _inner.TryGetValue(key, out value);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfTOnlyWithStringTValueAsInt()
        {
            const string Yaml = """{"One":1,"Two":2}""";

            DictionaryThatOnlyImplementsIDictionaryOfStringTValue<int> dictionary;

            dictionary = await Serializer.DeserializeWrapper<DictionaryThatOnlyImplementsIDictionaryOfStringTValue<int>>(Yaml);
            Assert.Equal(1, dictionary["One"]);
            Assert.Equal(2, dictionary["Two"]);

            string yaml = await Serializer.SerializeWrapper(dictionary);
            Assert.Equal(Yaml, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfTOnlyWithStringTValueAsPoco()
        {
            const string Yaml = """{"One":{"Id":1},"Two":{"Id":2}}""";

            DictionaryThatOnlyImplementsIDictionaryOfStringTValue<Poco> dictionary;

            dictionary = await Serializer.DeserializeWrapper<DictionaryThatOnlyImplementsIDictionaryOfStringTValue<Poco>>(Yaml);
            Assert.Equal(1, dictionary["One"].Id);
            Assert.Equal(2, dictionary["Two"].Id);

            string yaml = await Serializer.SerializeWrapper(dictionary);
            Assert.Equal(Yaml, yaml);
        }

        public class DictionaryThatOnlyImplementsIDictionaryOfStringPoco : DictionaryThatOnlyImplementsIDictionaryOfStringTValue<Poco>
        {
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryOfTOnlyWithStringPoco()
        {
            const string Yaml = """{"One":{"Id":1},"Two":{"Id":2}}""";

            DictionaryThatOnlyImplementsIDictionaryOfStringPoco dictionary;

            dictionary = await Serializer.DeserializeWrapper<DictionaryThatOnlyImplementsIDictionaryOfStringPoco>(Yaml);
            Assert.Equal(1, dictionary["One"].Id);
            Assert.Equal(2, dictionary["Two"].Id);

            string yaml = await Serializer.SerializeWrapper(dictionary);
            Assert.Equal(Yaml, yaml);
        }

        public class DictionaryThatHasIncompatibleEnumerator : IDictionary
        {
            Hashtable _inner = new Hashtable();

            public object this[string key]
            {
                get
                {
                    return _inner[key];
                }
                set
                {
                    _inner[key] = value;
                }
            }

            public ICollection Keys => _inner.Keys;

            public ICollection Values => _inner.Values;

            public int Count => _inner.Count;

            public bool IsReadOnly => _inner.IsReadOnly;

            public bool IsFixedSize => _inner.IsFixedSize;

            public bool IsSynchronized => throw new NotImplementedException();

            public object SyncRoot => throw new NotImplementedException();

            public object this[object key]
            {
                get
                {
                    return _inner[key];
                }
                set
                {
                    _inner[key] = value;
                }
            }

            public void Add(object key, object value)
            {
                _inner.Add(key, value);
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool Contains(object key)
            {
                // Styml (M5c): over the fixture's storage, which the Error default's duplicate check reads; the
                // incompatible enumerator, the fixture's point, is unchanged.
                return _inner.Contains(key);
            }

            public IDictionaryEnumerator GetEnumerator()
            {
                // Throw NotSupportedException so we can detect this GetEnumerator was called.
                throw new NotSupportedException();
            }

            public void Remove(object key)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task VerifyDictionaryThatHasIncomatibleEnumeratorWithInt()
        {
            const string Yaml = """{"One":1,"Two":2}""";

            DictionaryThatHasIncompatibleEnumerator dictionary;
            dictionary = await Serializer.DeserializeWrapper<DictionaryThatHasIncompatibleEnumerator>(Yaml);
            Assert.Equal(1, ((YamlElement)dictionary["One"]).GetInt32());
            Assert.Equal(2, ((YamlElement)dictionary["Two"]).GetInt32());
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(dictionary));
        }

        [Fact]
        public async Task VerifyDictionaryThatHasIncomatibleEnumeratorWithPoco()
        {
            const string Yaml = """{"One":{"Id":1},"Two":{"Id":2}}""";

            DictionaryThatHasIncompatibleEnumerator dictionary;
            dictionary = await Serializer.DeserializeWrapper<DictionaryThatHasIncompatibleEnumerator>(Yaml);
            Assert.Equal(1, ((YamlElement)dictionary["One"]).GetProperty("Id").GetInt32());
            Assert.Equal(2, ((YamlElement)dictionary["Two"]).GetProperty("Id").GetInt32());
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(dictionary));
        }

        public class ClassWithoutParameterlessCtor
        {
            public ClassWithoutParameterlessCtor(int num) { }
            public string Name { get; set; }
        }

        public class ClassWithInternalParameterlessConstructor
        {
            internal ClassWithInternalParameterlessConstructor() { }
            public string Name { get; set; }
        }

        public class ClassWithPrivateParameterlessConstructor
        {
            private ClassWithPrivateParameterlessConstructor() { }
            public string Name { get; set; }
        }

        [Fact]
        public async Task DictionaryWith_ObjectWithNoParameterlessCtor_AsValue_Throws()
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, ClassWithInternalParameterlessConstructor>>("""{"key":{}}"""));
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, ClassWithPrivateParameterlessConstructor>>("""{"key":{}}"""));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task DictionaryWith_ObjectWithNoParameterlessCtor_Serialize_Works()
        {
            var noParameterless = new Dictionary<string, ClassWithoutParameterlessCtor>()
            {
                ["key"] = new ClassWithoutParameterlessCtor(5)
                {
                    Name = "parameterless"
                }
            };

            string yaml = await Serializer.SerializeWrapper(noParameterless);
            Assert.Equal("{\"key\":{\"Name\":\"parameterless\"}}", yaml);

            var onlyInternal = new Dictionary<string, ClassWithInternalParameterlessConstructor>()
            {
                ["key"] = new ClassWithInternalParameterlessConstructor()
                {
                    Name = "internal"
                }
            };

            yaml = await Serializer.SerializeWrapper(onlyInternal);
            Assert.Equal("{\"key\":{\"Name\":\"internal\"}}", yaml);

            var onlyPrivate = new Dictionary<string, ClassWithPrivateParameterlessConstructor>()
            {
                ["key"] = null
            };

            yaml = await Serializer.SerializeWrapper(onlyPrivate);
            Assert.Equal("{\"key\":null}", yaml);
        }
    }
}
