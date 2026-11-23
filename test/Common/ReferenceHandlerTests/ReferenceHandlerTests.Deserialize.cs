// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class ReferenceHandlerTests : SerializerTests
    {
        private static readonly YamlSerializerOptions s_deserializerOptionsPreserve = new() { ReferenceHandler = ReferenceHandler.Preserve };

        public class EmployeeWithContacts
        {
            public string Name { get; set; }
            public EmployeeWithContacts Manager { get; set; }
            public List<EmployeeWithContacts> Subordinates { get; set; }
            public Dictionary<string, EmployeeWithContacts> Contacts { get; set; }
        }

        #region Root Object
        [Fact]
        public async Task ObjectWithoutMetadata()
        {
            string yaml = "{}";
            Employee employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.NotNull(employee);
        }

        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public async Task ObjectReferenceLoop()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$ref": "1"
                                    }
                                }
                """;

            Employee angela = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(angela, angela.Manager);
        }

        [Fact] // Employee whose subordinates is a preserved list. EmployeeListEmployee
        public async Task ObjectReferenceLoopInList()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Subordinates": {
                                        "$id": "2",
                                        "$values": [
                                            {
                                                "$ref": "1"
                                            }
                                        ]
                                    }
                                }
                """;

            Employee employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(1, employee.Subordinates.Count);
            Assert.Same(employee, employee.Subordinates[0]);
        }

        [Fact] // Employee whose subordinates is a preserved list. EmployeeListEmployee
        public async Task ObjectReferenceLoopInDictionary()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Contacts":{
                                        "$id": "2",
                                        "Angela":{
                                            "$ref": "1"
                                        }
                                    }
                                }
                """;

            EmployeeWithContacts employee = await Serializer.DeserializeWrapper<EmployeeWithContacts>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(employee, employee.Contacts["Angela"]);
        }

        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public async Task ObjectWithArrayReferenceDeeper()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Subordinates": {
                                        "$id": "2",
                                        "$values": [
                                            {
                                                "$id": "3",
                                                "Name": "Angela",
                                                "Subordinates":{
                                                    "$ref": "2"
                                                }
                                            }
                                        ]
                                    }
                                }
                """;

            Employee employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(employee.Subordinates, employee.Subordinates[0].Subordinates);
        }

        [Fact]
        public async Task ObjectWithDictionaryReferenceDeeper()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Contacts": {
                                        "$id": "2",
                                        "Angela": {
                                            "$id": "3",
                                            "Name": "Angela",
                                            "Contacts": {
                                                "$ref": "2"
                                            }
                                        }
                                    }
                                }
                """;

            EmployeeWithContacts employee = await Serializer.DeserializeWrapper<EmployeeWithContacts>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(employee.Contacts, employee.Contacts["Angela"].Contacts);
        }

        public class ClassWithSubsequentListProperties
        {
            public List<int> MyList { get; set; }
            public List<int> MyListCopy { get; set; }
        }

        [Fact]
        public async Task PreservedArrayIntoArrayProperty()
        {
            string yaml = """

                    {
                        "MyList": {
                            "$id": "1",
                            "$values": [
                                10,
                                20,
                                30,
                                40
                            ]
                        },
                        "MyListCopy": { "$ref": "1" }
                    }
                """;

            ClassWithSubsequentListProperties instance = await Serializer.DeserializeWrapper<ClassWithSubsequentListProperties>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(4, instance.MyList.Count);
            Assert.Same(instance.MyList, instance.MyListCopy);
        }

        [Fact]
        public async Task PreservedArrayIntoInitializedProperty()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "SubordinatesString": {
                                        "$id": "2",
                                        "$values": [
                                        ]
                                    },
                                    "Manager": {
                                        "SubordinatesString":{
                                            "$ref": "2"
                                        }
                                    }
                                }
                """;

            Employee employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            // presereved array.
            Assert.Empty(employee.SubordinatesString);
            // reference to preserved array.
            Assert.Empty(employee.Manager.SubordinatesString);
            Assert.Same(employee.Manager.SubordinatesString, employee.SubordinatesString);
        }

        [Fact] // Verify ReadStackFrame.DictionaryPropertyIsPreserved is being reset properly.
        public async Task DictionaryPropertyOneAfterAnother()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "Contacts": {
                                        "$id": "2"
                                    },
                                    "Contacts2": {
                                        "$ref": "2"
                                    }
                                }
                """;

            Employee employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(employee.Contacts, employee.Contacts2);

            yaml = """
                    {
                                    "$id": "1",
                                    "Contacts": {
                                        "$id": "2"
                                    },
                                    "Contacts2": {
                                        "$id": "3"
                                    }
                                }
                """;

            employee = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(0, employee.Contacts.Count);
            Assert.Equal(0, employee.Contacts2.Count);
        }

        [Fact]
        public async Task ObjectPropertyLengthZero()
        {
            string yaml = """
                    {
                                    "": 1
                                }
                """;

            ClassWithZeroLengthProperty<int> root = await Serializer.DeserializeWrapper<ClassWithZeroLengthProperty<int>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(1, root.ZeroLengthProperty);
        }

        private async Task TestIdTask()
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>("""{"$id":1}""", s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);
        }

        private async Task TestRefTask()
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>("""{"$ref":1}""", s_deserializerOptionsPreserve));
            Assert.Equal("$['$ref']", ex.Path);
        }
        #endregion

        #region Root Dictionary
        [Fact]
        public async Task DictionaryWithoutMetadata()
        {
            string yaml = "{}";
            Dictionary<string, string> dictionary = await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml, s_deserializerOptionsPreserve);
            Assert.NotNull(dictionary);
        }

        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public async Task DictionaryReferenceLoop()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Angela": {
                                        "$id": "2",
                                        "Name": "Angela",
                                        "Contacts": {
                                            "$ref": "1"
                                        }
                                    }
                                }
                """;

            Dictionary<string, EmployeeWithContacts> dictionary = await Serializer.DeserializeWrapper<Dictionary<string, EmployeeWithContacts>>(yaml, s_deserializerOptionsPreserve);

            Assert.Same(dictionary, dictionary["Angela"].Contacts);
        }

        [Fact]
        public async Task DictionaryReferenceLoopInList()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Angela": {
                                        "$id": "2",
                                        "Name": "Angela",
                                        "Subordinates": {
                                            "$id": "3",
                                            "$values": [
                                                {
                                                    "$id": "4",
                                                    "Name": "Bob",
                                                    "Contacts": {
                                                        "$ref": "1"
                                                    }
                                                }
                                            ]
                                        }
                                    }
                                }
                """;

            Dictionary<string, EmployeeWithContacts> dictionary = await Serializer.DeserializeWrapper<Dictionary<string, EmployeeWithContacts>>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(dictionary, dictionary["Angela"].Subordinates[0].Contacts);
        }

        [Fact]
        public async Task DictionaryDuplicatedObject()
        {
            string yaml =
            """
                    {
                                  "555": { "$id": "1", "Name": "Angela" },
                                  "556": { "Name": "Bob" },
                                  "557": { "$ref": "1" }
                                }
                """;

            Dictionary<string, Employee> directory = await Serializer.DeserializeWrapper<Dictionary<string, Employee>>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(directory["555"], directory["557"]);
        }

        [Fact]
        public async Task DictionaryOfArrays()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Array1": {
                                        "$id": "2",
                                        "$values": []
                                    },
                                    "Array2": {
                                        "$ref": "2"
                                    }
                                }
                """;

            Dictionary<string, List<int>> dict = await Serializer.DeserializeWrapper<Dictionary<string, List<int>>>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(dict["Array1"], dict["Array2"]);
        }

        [Fact]
        public async Task DictionaryOfDictionaries()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "Dictionary1": {
                                        "$id": "2",
                                        "value1": 1,
                                        "value2": 2,
                                        "value3": 3
                                    },
                                    "Dictionary2": {
                                        "$ref": "2"
                                    }
                                }
                """;

            Dictionary<string, Dictionary<string, int>> root = await Serializer.DeserializeWrapper<Dictionary<string, Dictionary<string, int>>>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(root["Dictionary1"], root["Dictionary2"]);
        }

        [Fact]
        public async Task DictionaryKeyLengthZero()
        {
            string yaml = """
                    {
                                    "": 1
                                }
                """;

            Dictionary<string, int> root = await Serializer.DeserializeWrapper<Dictionary<string, int>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(1, root[""]);
        }
        #endregion

        #region Root Array
        [Fact]
        public async Task PreservedArrayIntoRootArray()
        {
            string yaml = """

                    {
                        "$id": "1",
                        "$values": [
                            10,
                            20,
                            30,
                            40
                        ]
                    }
                """;

            List<int> myList = await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(4, myList.Count);
        }

        [Fact] // Preserved list that contains an employee whose subordinates is a reference to the root list.
        public async Task ArrayNestedArray()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "$values":[
                                        {
                                            "$id":"2",
                                            "Name": "Angela",
                                            "Subordinates": {
                                                "$ref": "1"
                                            }
                                        }
                                    ]
                                }
                """;

            List<Employee> employees = await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve);

            Assert.Same(employees, employees[0].Subordinates);
        }

        [Fact]
        public async Task EmptyArray()
        {
            string yaml =
            """
                    {
                                  "$id": "1",
                                  "Subordinates": {
                                    "$id": "2",
                                    "$values": []
                                  },
                                  "Name": "Angela"
                                }
                """;

            Employee angela = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);

            Assert.NotNull(angela);
            Assert.NotNull(angela.Subordinates);
            Assert.Equal(0, angela.Subordinates.Count);
        }

        [Fact]
        public async Task ArrayWithDuplicates() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "$values":[
                                        {
                                            "$id": "2",
                                            "Name": "Angela"
                                        },
                                        {
                                            "$id": "3",
                                            "Name": "Bob"
                                        },
                                        {
                                            "$ref": "2"
                                        },
                                        {
                                            "$ref": "3"
                                        },
                                        {
                                            "$id": "4"
                                        },
                                        {
                                            "$ref": "4"
                                        }
                                    ]
                                }
                """;

            List<Employee> employees = await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(6, employees.Count);
            Assert.Same(employees[0], employees[2]);
            Assert.Same(employees[1], employees[3]);
            Assert.Same(employees[4], employees[5]);
        }

        [Fact]
        public async Task ArrayNotPreservedWithDuplicates() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string yaml =
            """
                    [
                                    {
                                        "$id": "2",
                                        "Name": "Angela"
                                    },
                                    {
                                        "$id": "3",
                                        "Name": "Bob"
                                    },
                                    {
                                        "$ref": "2"
                                    },
                                    {
                                        "$ref": "3"
                                    },
                                    {
                                        "$id": "4"
                                    },
                                    {
                                        "$ref": "4"
                                    }
                                ]
                """;

            Employee[] employees = await Serializer.DeserializeWrapper<Employee[]>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(6, employees.Length);
            Assert.Same(employees[0], employees[2]);
            Assert.Same(employees[1], employees[3]);
            Assert.Same(employees[4], employees[5]);
        }

        [Fact]
        public async Task ArrayWithNestedPreservedArray()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": [
                                        {
                                            "$id": "2",
                                            "$values": [ 1, 2, 3 ]
                                        }
                                    ]
                                }
                """;

            List<List<int>> root = await Serializer.DeserializeWrapper<List<List<int>>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(1, root.Count);
            Assert.Equal(3, root[0].Count);
        }

        [Fact]
        public async Task ArrayWithNestedPreservedArrayAndReference()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": [
                                        {
                                            "$id": "2",
                                            "$values": [ 1, 2, 3 ]
                                        },
                                        { "$ref": "2" }
                                    ]
                                }
                """;

            List<List<int>> root = await Serializer.DeserializeWrapper<List<List<int>>>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(2, root.Count);
            Assert.Equal(3, root[0].Count);
            Assert.Same(root[0], root[1]);
        }

        public class ListWrapper
        {
            public List<List<int>> NestedList { get; set; } = new List<List<int>> { new List<int> { 1 } };
        }

        [Fact]
        public async Task ArrayWithNestedPreservedArrayAndDefaultValues()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "NestedList": {
                                        "$id": "2",
                                        "$values": [
                                            {
                                                "$id": "3",
                                                "$values": [
                                                    1,
                                                    2,
                                                    3
                                                ]
                                            }
                                        ]
                                    }
                                }
                """;

            ListWrapper root = await Serializer.DeserializeWrapper<ListWrapper>(yaml, s_deserializerOptionsPreserve);
            Assert.Equal(1, root.NestedList.Count);
            Assert.Equal(3, root.NestedList[0].Count);
        }

        [Fact]
        public async Task ArrayWithMetadataWithinArray_UsingPreserve()
        {
            const string yaml =
            """
                    [
                                    {
                                        "$id": "1",
                                        "$values": []
                                    }
                                ]
                """;

            List<List<Employee>> root = await Serializer.DeserializeWrapper<List<List<Employee>>>(yaml, s_serializerOptionsPreserve);
            Assert.Equal(1, root.Count);
            Assert.Equal(0, root[0].Count);
        }

        [Fact]
        public async Task ObjectWithinArray_UsingDefault()
        {
            const string yaml =
            """
                    [
                                    {
                                        "$id": "1",
                                        "$values": []
                                    }
                                ]
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<List<Employee>>>(yaml));
            Assert.Equal("$[0]", ex.Path);
        }
        #endregion

        #region Converter
        [Fact] //This only demonstrates that behavior with converters remain the same.
        public async Task DeserializeWithListConverter()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Subordinates": {
                                        "$id": "2",
                                        "$values": [
                                            {
                                                "$ref": "1"
                                            }
                                        ]
                                    },
                                    "Name": "Angela",
                                    "Manager": {
                                        "Subordinates": {
                                            "$ref": "2"
                                        }
                                    }
                                }
                """;

            var options = new YamlSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve,
                Converters = { new ListOfEmployeeConverter() }
            };
            Employee angela = await Serializer.DeserializeWrapper<Employee>(yaml, options);
            Assert.Equal(0, angela.Subordinates.Count);
            Assert.Equal(0, angela.Manager.Subordinates.Count);
        }

        //NOTE: If you implement a converter, you are on your own when handling metadata properties and therefore references.Newtonsoft does the same.
        //However; is there a way to recall preserved references previously found in the payload and to store new ones found in the converter's payload? that would be a cool enhancement.
        public class ListOfEmployeeConverter : YamlConverter<List<Employee>>
        {
            public override List<Employee> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                int startObjectCount = 0;
                int endObjectCount = 0;

                while (true)
                {
                    switch (reader.TokenType)
                    {
                        case YamlTokenType.StartObject:
                            startObjectCount++; break;
                        case YamlTokenType.EndObject:
                            endObjectCount++; break;
                    }

                    if (startObjectCount == endObjectCount)
                    {
                        break;
                    }

                    reader.Read();
                }

                return new List<Employee>();
            }

            public override void Write(Utf8YamlWriter writer, List<Employee> value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Null/non-existent reference
        [Fact]
        public async Task ObjectNull()
        {
            string yaml =
            """
                    {
                                    "$ref": "1"
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$", ex.Path);
        }

        [Fact()]
        public async Task ArrayNull()
        {
            string yaml =
            """
                    {
                                    "$ref": "1"
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$", ex.Path);
        }

        [Fact]
        public async Task DictionaryNull()
        {
            string yaml =
            """
                    {
                                    "$ref": "1"
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, Employee>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$", ex.Path);
        }

        [Fact]
        public async Task ObjectPropertyNull()
        {
            string yaml =
            """
                    {
                                    "Manager": {
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Manager", ex.Path);
        }

        [Fact]
        public async Task ArrayPropertyNull()
        {
            string yaml =
            """
                    {
                                    "Subordinates": {
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Subordinates", ex.Path);
        }

        [Fact]
        public async Task DictionaryPropertyNull()
        {
            string yaml =
            """
                    {
                                    "Contacts": {
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Contacts", ex.Path);
        }

        #endregion

        #region Throw cases

        [Fact]
        public async Task YamlPathObject()
        {
            string yaml = """{ "Name": "A""";
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.Name", ex.Path);
        }

        [Fact]
        public async Task YamlPathIncompletePropertyAfterMetadata()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Nam
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            // Since $id was parsed correctly, the path should just be "$".
            Assert.Equal("$", ex.Path);
        }

        [Fact]
        public async Task YamlPathIncompleteMetadataAfterProperty()
        {
            string yaml =
            """
                    {
                                    "Name": "Angela",
                                    "$i
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            // Since "Name" was parsed correctly, the path should just be "$".
            Assert.Equal("$", ex.Path);
        }

        [Fact]
        public async Task YamlPathCompleteMetadataButNotValue()
        {
            string yaml =
            """
                    {
                                    "$id":
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$id']", ex.Path);
        }

        [Fact]
        public async Task YamlPathIncompleteMetadataValue()
        {
            string yaml =
            """
                    {
                                    "$id": "1
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$id']", ex.Path);
        }

        [Fact]
        public async Task YamlPathNestedObject()
        {
            string yaml = """{ "Name": "A", "Manager": { "Name": "B""";
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.Manager.Name", ex.Path);
        }

        [Fact]
        public async Task YamlPathNestedArray()
        {
            string yaml = """{ "Subordinates":""";
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.Subordinates", ex.Path);
        }

        [Fact]
        public async Task YamlPathIncompleteArrayId()
        {
            string yaml =
            """
                    {
                                    "$id": "1
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$id']", ex.Path);
        }

        [Fact]
        public async Task YamlPathIncompleteArrayValues()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "$values":
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$values']", ex.Path);
        }

        [Fact]
        public async Task YamlPathCurlyBraceOnArray()
        {
            string yaml = "{";
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$", ex.Path);
        }

        [Fact]
        public async Task ThrowOnStructWithReference()
        {
            string yaml =
            """
                    [
                                    {
                                        "$id": "1",
                                        "Name": "Angela"
                                    },
                                    {
                                        "$ref": "1"
                                    }
                                ]
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<EmployeeStruct>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$[1]['$ref']", ex.Path);
            Assert.Contains($"'{typeof(EmployeeStruct)}'", ex.Message);
        }

        [Theory]
        [InlineData("""{"$iz": "1"}""", "$['$iz']")]
        [InlineData("""{"$rez": "1"}""", "$['$rez']")]
        [InlineData("""{"$valuez": []}""", "$['$valuez']")]
        [InlineData("""{"$type": "derivedType"}""", "$['$type']")]
        [InlineData("""{"$PropertyWithDollarSign": "1"}""", "$['$PropertyWithDollarSign']")]
        [InlineData("""{"$id" : "1", "$iz": "1"}""", "$['$iz']")]
        [InlineData("""{"$id" : "1", "$rez": "1"}""", "$['$rez']")]
        [InlineData("""{"$id" : "1", "$id" : 1 }""", "$['$id']")]
        [InlineData("""{"$id" : "1", "$ref" : 1 }""", "$['$ref']")]
        [InlineData("""{"$id" : "1", "$valuez": "[]"}""", "$['$valuez']")]
        [InlineData("""{"$id" : "1", "$type": "derivedType"}""", "$['$type']")]
        [InlineData("""{"$id" : "1", "$PropertyWithDollarSign": "1"}""", "$['$PropertyWithDollarSign']")]
        [InlineData("""{"$id" : "1", "NonMetadataProperty" : 42, "$iz": "1"}""", "$['$iz']")]
        [InlineData("""{"$id" : "1", "NonMetadataProperty" : 42, "$rez": "1"}""", "$['$rez']")]
        [InlineData("""{"$id" : "1", "NonMetadataProperty" : 42, "$valuez": "[]"}""", "$['$valuez']")]
        [InlineData("""{"$id" : "1", "NonMetadataProperty" : 42, "$type": "derivedType"}""", "$['$type']")]
        [InlineData("""{"$id" : "1", "NonMetadataProperty" : 42, "$PropertyWithDollarSign": "1"}""", "$['$PropertyWithDollarSign']")]
        public async Task InvalidMetadataPropertyNameIsRejected(string yaml, string expectedPath)
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal(expectedPath, ex.Path);
        }
        #endregion

        #region Throw on immutables
        public class EmployeeWithImmutable
        {
            public ImmutableList<EmployeeWithImmutable> Subordinates { get; set; }
            public EmployeeWithImmutable[] SubordinatesArray { get; set; }
            public ImmutableDictionary<string, EmployeeWithImmutable> Contacts { get; set; }
        }

        [Fact]
        public async Task ImmutableEnumerableAsRoot()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "$values": []
                                }
                """;

            YamlException ex;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ImmutableList<EmployeeWithImmutable>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);
            Assert.Contains($"'{typeof(ImmutableList<EmployeeWithImmutable>)}'", ex.Message);

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeWithImmutable[]>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);
            Assert.Contains($"'{typeof(EmployeeWithImmutable[])}'", ex.Message);
        }

        [Fact]
        public async Task ImmutableDictionaryAsRoot()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Employee1": {}
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ImmutableDictionary<string, EmployeeWithImmutable>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);
            Assert.Contains($"'{typeof(ImmutableDictionary<string, EmployeeWithImmutable>)}'", ex.Message);
        }

        [Fact]
        public async Task ImmutableEnumerableAsProperty()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Subordinates": {
                                        "$id": "2",
                                        "$values": []
                                    }
                                }
                """;

            YamlException ex;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeWithImmutable>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Subordinates['$id']", ex.Path);
            Assert.Contains($"'{typeof(ImmutableList<EmployeeWithImmutable>)}'", ex.Message);

            yaml =
            """
                    {
                                    "$id": "1",
                                    "SubordinatesArray": {
                                        "$id": "2",
                                        "$values": []
                                    }
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeWithImmutable>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.SubordinatesArray['$id']", ex.Path);
            Assert.Contains($"'{typeof(EmployeeWithImmutable[])}'", ex.Message);
        }

        [Fact]
        public async Task ImmutableDictionaryAsProperty()
        {
            string yaml =
            """
                    {
                                    "$id": "1",
                                    "Contacts": {
                                        "$id": "2",
                                        "Employee1": {}
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeWithImmutable>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Contacts['$id']", ex.Path);
            Assert.Contains($"'{typeof(ImmutableDictionary<string, EmployeeWithImmutable>)}'", ex.Message);
        }

        [Fact]
        public async Task ImmutableDictionaryPreserveNestedObjects()
        {
            string yaml =
            """
                    {
                                    "Angela": {
                                        "$id": "1",
                                        "Name": "Angela",
                                        "Subordinates": {
                                            "$id": "2",
                                            "$values": [
                                                {
                                                    "$id": "3",
                                                    "Name": "Carlos",
                                                    "Manager": {
                                                        "$ref": "1"
                                                    }
                                                }
                                            ]
                                        }
                                    },
                                    "Bob": {
                                        "$id": "4",
                                        "Name": "Bob"
                                    },
                                    "Carlos": {
                                        "$ref": "3"
                                    }
                                }
                """;

            // Must not throw since the references are to nested objects, not the immutable dictionary itself.
            ImmutableDictionary<string, Employee> dictionary = await Serializer.DeserializeWrapper<ImmutableDictionary<string, Employee>>(yaml, s_deserializerOptionsPreserve);
            Assert.Same(dictionary["Angela"], dictionary["Angela"].Subordinates[0].Manager);
            Assert.Same(dictionary["Carlos"], dictionary["Angela"].Subordinates[0]);
        }


        [Theory]
        [InlineData("""{"$id":"1","$values":{}}""", YamlTokenType.StartObject)]
        [InlineData("""{"$id":"1","$values":null}""", YamlTokenType.Null)]
        [InlineData("""{"$id":"1","$values":true}""", YamlTokenType.True)]
        [InlineData("""{"$id":"1","$values":false}""", YamlTokenType.False)]
        [InlineData("""{"$id":"1","$values":9}""", YamlTokenType.Number)]
        [InlineData("""{"$id":"1","$values":"9"}""", YamlTokenType.String)]
        // Invalid JSON, the reader will throw before we reach the serializer validation.
        [InlineData("""{"$id":"1","$values":}""", YamlTokenType.None)]
        [InlineData("""{"$id":"1","$values":]""", YamlTokenType.None)]
        public async Task MetadataValues_StartsWithInvalidToken(string yaml, YamlTokenType incorrectToken)
        {
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));
            Assert.True(incorrectToken == YamlTokenType.None || ex.Message.Contains($"'{incorrectToken}'"));
            Assert.Equal("$['$values']", ex.Path);
        }
        #endregion

        #region Ground Rules/Corner cases

        public class Order
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        #region Reference objects ($ref)
        [Fact]
        public async Task ReferenceObjectsShouldNotContainMoreProperties()
        {
            //Regular property before $ref
            string yaml = """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "Name": "Bob",
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Manager['$ref']", ex.Path);

            //Regular dictionary key before $ref
            yaml = """
                    {
                                    "Angela": {
                                        "Name": "Angela",
                                        "$ref": "1"
                                    }
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, Dictionary<string, string>>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Angela['$ref']", ex.Path);

            //Regular property after $ref
            yaml = """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$ref": "1",
                                        "Name": "Bob"
                                    }
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Manager.Name", ex.Path);

            //Metadata property before $ref
            yaml = """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$id": "2",
                                        "$ref": "1"
                                    }
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Manager['$ref']", ex.Path);

            //Metadata property after $ref
            yaml = """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$ref": "1",
                                        "$id": "2"
                                    }
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$.Manager['$id']", ex.Path);
        }

        [Fact]
        public async Task ReferenceObjectBeforePreservedObject()
        {
            string yaml = """
                    [
                                    {
                                        "$ref": "1"
                                    },
                                    {
                                        "$id": "1",
                                        "Name": "Angela"
                                    }
                                ]
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));
            Assert.Contains("'1'", ex.Message);
            Assert.Equal("$[0]", ex.Path);
        }

        [Theory]
        [MemberData(nameof(ReadSuccessCases))]
        public async Task ReadTestClassesWithExtensionOption(Type classType, byte[] data)
        {
            var options = new YamlSerializerOptions { IncludeFields = true, ReferenceHandler = ReferenceHandler.Preserve };
            object obj = await Serializer.DeserializeWrapper(Encoding.UTF8.GetString(data), classType, options);
            Assert.IsAssignableFrom<ITestClass>(obj);
            ((ITestClass)obj).Verify();
        }

        public static IEnumerable<object[]> ReadSuccessCases
        {
            get
            {
                return TestData.ReadSuccessCases;
            }
        }

        #endregion

        #region Preserved objects ($id)
        [Fact]
        public async Task MoreThanOneId()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$id": "2",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$id']", ex.Path);
        }

        [Fact]
        public async Task IdIsNotFirstProperty()
        {
            string yaml = """
                    {
                                    "Name": "Angela",
                                    "$id": "1",
                                    "Manager": {
                                        "$ref": "1"
                                    }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);

            yaml = """
                    {
                                    "Name": "Angela",
                                    "$id": "1"
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$id']", ex.Path);
        }

        [Fact]
        public async Task DuplicatedId()
        {
            string yaml = """
                    [
                                    {
                                        "$id": "1",
                                        "Name": "Angela"
                                    },
                                    {
                                        "$id": "1",
                                        "Name": "Bob"
                                    }
                                ]
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$[1]", ex.Path);
            Assert.Contains("'1'", ex.Message);
        }

        public class ClassWithTwoListProperties
        {
            public List<string> List1 { get; set; }
            public List<string> List2 { get; set; }
        }

        [Fact]
        public async Task DuplicatedIdArray()
        {
            string yaml = """
                    {
                                    "List1": {
                                            "$id": "1",
                                            "$values": []
                                        },
                                    "List2": {
                                            "$id": "1",
                                            "$values": []
                                        }
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithTwoListProperties>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.List2['$values']", ex.Path);
            Assert.Contains("'1'", ex.Message);
        }

        [Theory]
        [InlineData("""{"$id":"A", "Manager":{"$ref":"A"}}""")]
        [InlineData("""{"$id":"00000000-0000-0000-0000-000000000000", "Manager":{"$ref":"00000000-0000-0000-0000-000000000000"}}""")]
        [InlineData("{\"$id\":\"A\u0467\", \"Manager\":{\"$ref\":\"A\u0467\"}}")]
        public async Task TestOddStringsInMetadata(string yaml)
        {
            Employee root = await Serializer.DeserializeWrapper<Employee>(yaml, s_deserializerOptionsPreserve);
            Assert.NotNull(root);
            Assert.Same(root, root.Manager);
        }
        #endregion

        #region Preserved arrays ($id and $values)
        [Fact]
        public async Task PreservedArrayWithoutMetadata()
        {
            string yaml = "{}";
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$", ex.Path);
            Assert.Contains(typeof(List<int>).ToString(), ex.Message);
        }

        [Fact]
        public async Task PreservedArrayWithoutValues()
        {
            string yaml = """
                    {
                                    "$id": "1"
                                }
                """;
            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$", ex.Path);
            Assert.Contains("$values", ex.Message);
            Assert.Contains(typeof(List<int>).ToString(), ex.Message);
        }

        [Fact]
        public async Task PreservedArrayWithoutId()
        {
            string yaml = """
                    {
                                    "$values": []
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$values']", ex.Path);
        }

        [Fact]
        public async Task PreservedArrayValuesContainsNull()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": null
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<int>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$values']", ex.Path);
        }

        [Fact]
        public async Task PreservedArrayValuesContainsValue()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": 1
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$values']", ex.Path);
        }

        [Fact]
        public async Task PreservedArrayValuesContainsObject()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": {}
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$['$values']", ex.Path);
        }

        [Fact]
        public async Task PreservedArrayExtraProperties()
        {
            string yaml = """
                    {
                                    "LeadingProperty": 0
                                    "$id": "1",
                                    "$values": []
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.LeadingProperty", ex.Path);
            Assert.Contains(typeof(List<Employee>).ToString(), ex.Message);

            yaml = """
                    {
                                    "$id": "1",
                                    "$values": [],
                                    "TrailingProperty": 0
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<Employee>>(yaml, s_deserializerOptionsPreserve));

            Assert.Equal("$.TrailingProperty", ex.Path);
            Assert.Contains(typeof(List<Employee>).ToString(), ex.Message);
            Assert.Contains("TrailingProperty", ex.Message);
        }
        #endregion

        #region JSON Objects if not collection
        public class EmployeeExtensionData : Employee
        {
            [YamlExtensionData]
            [Newtonsoft.Json.JsonExtensionData]
            public IDictionary<string, object> ExtensionData { get; set; }
        }

        [Fact]
        public async Task YamlObjectNonCollectionTest()
        {
            // $values Not Valid
            string yaml = """
                    {
                                    "$id": "1",
                                    "$values": "test"
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeExtensionData>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$values']", ex.Path);

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$values']", ex.Path);

            // $.* Not valid (i.e: $test)
            yaml = """
                    {
                                    "$id": "1",
                                    "$test": "test"
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeExtensionData>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$test']", ex.Path);

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$test']", ex.Path);

            yaml = """
                    {
                                    "$id": "1",
                                    "\u0024test": "test"
                                }
                """;

            // Escaped JSON properties still not valid
            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<EmployeeExtensionData>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$test']", ex.Path);

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Dictionary<string, string>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$['$test']", ex.Path);
        }
        #endregion

        #region JSON Objects if collection
        [Fact]
        public async Task YamlObjectCollectionTest()
        {

            // $ref Valid under conditions: must be the only property in the object.
            string yaml = """
                    {
                                    "$ref": "1"
                                }
                """;

            YamlException ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<string>>(yaml, s_deserializerOptionsPreserve));
            Assert.Equal("$", ex.Path);

            // $id Valid under conditions: must be the first property in the object.
            // $values Valid under conditions: must be after $id.
            yaml = """
                    {
                                    "$id": "1",
                                    "$values": []
                                }
                """;

            List<string> root = await Serializer.DeserializeWrapper<List<string>>(yaml, s_deserializerOptionsPreserve);
            Assert.NotNull(root);
            Assert.Equal(0, root.Count);

            // $.* Not valid (i.e: $test)
            yaml = """
                    {
                                    "$id": "1",
                                    "$test": "test"
                                }
                """;

            ex = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<string>>(yaml, s_deserializerOptionsPreserve));
        }
        #endregion
        #endregion

        [Fact]
        public async Task ReferenceIsAssignableFrom()
        {
            const string yaml = """{"Derived":{"$id":"my_id_1"},"Base":{"$ref":"my_id_1"}}""";
            BaseAndDerivedWrapper root = await Serializer.DeserializeWrapper<BaseAndDerivedWrapper>(yaml, s_serializerOptionsPreserve);

            Assert.Same(root.Base, root.Derived);
        }

        [Fact]
        public async Task ReferenceIsNotAssignableFrom()
        {
            const string yaml = """{"Base":{"$id":"my_id_1"},"Derived":{"$ref":"my_id_1"}}""";
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<BaseAndDerivedWrapper>(yaml, s_serializerOptionsPreserve));

            Assert.Contains("my_id_1", ex.Message);
            Assert.Contains(typeof(Derived).ToString(), ex.Message);
            Assert.Contains(typeof(Base).ToString(), ex.Message);
        }

        public class BaseAndDerivedWrapper
        {
            public Base Base { get; set; }
            public Derived Derived { get; set; }
        }

        public class Derived : Base { }
        public class Base { }

        [Theory]
        [InlineData(YamlUnknownTypeHandling.YamlElement)]
        [InlineData(YamlUnknownTypeHandling.YamlNode)]
        public async Task ObjectConverter_ShouldHandleReferenceMetadata(YamlUnknownTypeHandling typehandling)
        {
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve, UnknownTypeHandling = typehandling };
            string yaml = """[{ "$id" : "1" },{ "$ref" : "1"}]""";
            object[] deserialized = await Serializer.DeserializeWrapper<object[]>(yaml, options);
            Assert.Same(deserialized[0], deserialized[1]);
        }

        [Theory]
        [InlineData("""{ "$id"  : 42 }""", YamlUnknownTypeHandling.YamlElement)]
        [InlineData("""{ "$id"  : 42 }""", YamlUnknownTypeHandling.YamlNode)]
        [InlineData("""{ "$ref" : 42 }""", YamlUnknownTypeHandling.YamlElement)]
        [InlineData("""{ "$ref" : 42 }""", YamlUnknownTypeHandling.YamlNode)]
        public async Task ObjectConverter_InvalidMetadataPropertyType_ShouldThrowYamlException(string yaml, YamlUnknownTypeHandling typehandling)
        {
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve, UnknownTypeHandling = typehandling };
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<object>(yaml, options));
        }

        [Theory]
        [InlineData(YamlUnknownTypeHandling.YamlElement)]
        [InlineData(YamlUnknownTypeHandling.YamlNode)]
        public async Task ObjectConverter_PropertyTrailingRefMetadata_ShouldThrowYamlException(YamlUnknownTypeHandling typehandling)
        {
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve, UnknownTypeHandling = typehandling };
            string yaml = """[{ "$id" : "1" }, { "$ref" : "1", "trailingProperty" : true }]""";
            await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<object[]>(yaml, options));
        }

        [Fact]
        public async Task ConstructorDeserialization_ReferencePreservation()
        {
            string yaml = """[{ "$id" : "1", "Value" : 42, "Next" : null }, { "$ref" : "1" }]""";
            LinkedList<int>[] deserialized = await Serializer.DeserializeWrapper<LinkedList<int>[]>(yaml, s_serializerOptionsPreserve);

            Assert.Equal(2, deserialized.Length);
            Assert.Equal(42, deserialized[0].Value);
            Assert.Null(deserialized[0].Next);
            Assert.Same(deserialized[0], deserialized[1]);
        }

        [Theory]
        [InlineData("""{ "Value" : 1, "Next" : { "$id" : "1", "Value" : 2, "Next" : null }}""", typeof(LinkedList<int>))]
        [InlineData("""[{ "$id" : "1", "Value" : 2, "Next" : null }, { "Value" : 1, "Next" : { "$ref" : "1"}}]""", typeof(LinkedList<int>[]))]
        public Task ConstructorDeserialization_NestedConstructorArgumentReference_SupportedScenaria(string yaml, Type type)
            => Serializer.DeserializeWrapper(yaml, type, s_serializerOptionsPreserve);

        [Theory]
        [InlineData("""{ "$id" : "1", "Value" : 1, "Next" : { "$id" : "2", "Value" : 2, "Next" : null }}""", typeof(LinkedList<int>))]
        [InlineData("""{ "$id" : "1", "Value" : 1, "Next" : { "$ref" : "1" }}""", typeof(LinkedList<int>))]
        [InlineData("""[{ "$id" : "1", "Value" : 2, "Next" : null }, { "$id" : "2", "Value" : 1, "Next" : { "$ref" : "1"}}]""", typeof(LinkedList<int>[]))]
        [InlineData("""{ "$id" : "1", "Value" : [{"$id" : "2"}], "Next" : null }""", typeof(LinkedList<Base[]>))]
        [InlineData("""{ "$id" : "1", "Value" : [[{"$id" : "2"}]], "Next" : null }""", typeof(LinkedList<Base[][]>))]
        [InlineData("""{ "$id" : "1", "Value" : [{"$ref" : "1"}], "Next" : null }""", typeof(LinkedList<object[]>))]
        [InlineData("""{ "$id" : "1", "PropertyWithSetter" : { "$id" : "2" }}""", typeof(LinkedList<object?>))]
        [InlineData("""{ "$id" : "1", "PropertyWithSetter" : { "$ref" : "1" }}""", typeof(LinkedList<object?>))]
        public async Task ConstructorDeserialization_NestedConstructorArgumentReference_ThrowsNotSupportedException(string yaml, Type type)
        {
            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper(yaml, type, s_serializerOptionsPreserve));
            Assert.Contains("LinkedList", ex.Message);
        }

        public class LinkedList<T>
        {
            [YamlConstructor]
            public LinkedList(T value, LinkedList<T>? next)
            {
                Value = value;
                Next = next;
            }

            public T Value { get; }
            public LinkedList<T>? Next { get; }

            public T? PropertyWithSetter { get; set; }
        }

        [Fact]
        public async Task DuplicateNonReferenceProperties_Preserve()
        {
            string yaml = """
                    {
                                    "$id": "1",
                                    "Name": "Angela",
                                    "Manager": {
                                        "$ref": "1"
                                    },
                                    "Name": "Angela"
                                }
                """;

            // Baseline
            YamlSerializerOptions options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, ReferenceHandler = ReferenceHandler.Preserve };
            Employee angela = await Serializer.DeserializeWrapper<Employee>(yaml, options);
            Assert.Same(angela, angela.Manager);

            options = new YamlSerializerOptions
            { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                ReferenceHandler = ReferenceHandler.Preserve,
                AllowDuplicateProperties = false
            };

            YamlException ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<Employee>(yaml, options));
            Assert.Contains("Duplicate", ex.Message);
        }
    }
}
