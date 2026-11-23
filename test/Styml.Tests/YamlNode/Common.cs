// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static partial class YamlNodeTests
    {
        internal const string ExpectedDomYaml = "{\"MyString\":\"Hello!\",\"MyNull\":null,\"MyBoolean\":false,\"MyArray\":[2,3,42]," +
            "\"MyInt\":43,\"MyDateTime\":\"2020-07-08T00:00:00\",\"MyGuid\":\"ed957609-cdfe-412f-88c1-02daca1b4f51\"," +
            "\"MyObject\":{\"MyString\":\"Hello!!\"},\"Child\":{\"ChildProp\":1}}";

        internal const string Linq_Query_Yaml = """

                [
                  {
                    "OrderId":100, "Customer":
                    {
                      "Name":"Customer1",
                      "City":"Fargo"
                    }
                  },
                  {
                    "OrderId":200, "Customer":
                    {
                      "Name":"Customer2",
                      "City":"Redmond"
                    }
                  },
                  {
                    "OrderId":300, "Customer":
                    {
                      "Name":"Customer3",
                      "City":"Fargo"
                    }
                  }
                ]
            """;

        /// <summary>
        /// Helper class simulating external library
        /// </summary>
        internal static class EmployeesDatabase
        {
            private static int s_id = 0;
            public static KeyValuePair<string, YamlNode?> GetNextEmployee()
            {
                var employee = new YamlObject()
                {
                    { "name", "John" } ,
                    { "surname", "Smith"},
                    { "age", 45 }
                };

                return new KeyValuePair<string, YamlNode?>("employee" + s_id++, employee);
            }

            public static IEnumerable<KeyValuePair<string, YamlNode>> GetTenBestEmployees()
            {
                for (int i = 0; i < 10; i++)
                    yield return GetNextEmployee();
            }

            /// <summary>
            /// Returns following YamlObject:
            /// {
            ///     "phone numbers" : { "work" :  "425-555-0123", "home": "425-555-0134"  }
            ///     "reporting employees" : 
            ///     {
            ///         "software developers" :
            ///         {
            ///             "full time employees" : /YamlObject of 3 employees from database/ 
            ///             "intern employees" : /YamlObject of 2 employees from database/ 
            ///         },
            ///         "HR" : /YamlObject of 10 employees from database/ 
            ///     }
            /// }
            /// </summary>
            /// <returns></returns>
            public static YamlObject GetManager()
            {
                var manager = GetNextEmployee().Value as YamlObject;

                manager.Add
                (
                    "phone numbers",
                    new YamlObject()
                    {
                    { "work", "425-555-0123" }, { "home", "425-555-0134" }
                    }
                );

                manager.Add
                (
                    "reporting employees", new YamlObject
                    {
                    {
                        "software developers", new YamlObject
                        {
                            {
                                "full time employees", new YamlObject
                                {
                                    EmployeesDatabase.GetNextEmployee(),
                                    EmployeesDatabase.GetNextEmployee(),
                                    EmployeesDatabase.GetNextEmployee(),
                                }
                            },
                            {
                                "intern employees", new YamlObject
                                {
                                    EmployeesDatabase.GetNextEmployee(),
                                    EmployeesDatabase.GetNextEmployee(),
                                }
                            }
                        }
                    },
                    {
                        "HR", new YamlObject
                        {
                            {
                                "full time employees", new YamlObject(EmployeesDatabase.GetTenBestEmployees())
                            }
                        }
                    }
                    }
                );

                return manager;
            }
        }

        internal static void AssertDeepEqual(YamlNode? left, YamlNode? right)
        {
            // Assert Reflexivity
            Assert.True(YamlNode.DeepEquals(left, left));
            Assert.True(YamlNode.DeepEquals(right, right));

            // Core Assertion
            Assert.True(YamlNode.DeepEquals(left, right));

            // Assert Symmetry
            Assert.True(YamlNode.DeepEquals(right, left));
        }

        internal static void AssertNotDeepEqual(YamlNode? left, YamlNode? right)
        {
            // Assert Reflexivity
            Assert.True(YamlNode.DeepEquals(left, left));
            Assert.True(YamlNode.DeepEquals(right, right));

            // Core assertion
            Assert.False(YamlNode.DeepEquals(left, right));

            // Assert Symmetry
            Assert.False(YamlNode.DeepEquals(right, left));
        }
    }
}
