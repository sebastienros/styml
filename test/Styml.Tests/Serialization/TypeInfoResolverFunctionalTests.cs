// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class TypeInfoResolverFunctionalTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void AddPrefixToEveryPropertyOfClass()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        prop.Name = "renamed_" + prop.Name;
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"renamed_TestProperty":42,"renamed_TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void AppendCharacterWhenSerializingField()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    // Because IncludeFields is false
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo field = ti.CreateYamlPropertyInfo(typeof(string), "TestField");
                    field.Get = (o) =>
                    {
                        var obj = (TestClass)o;
                        return obj.TestField + "X";
                    };
                    field.Set = (o, val) =>
                    {
                        var obj = (TestClass)o;
                        var value = (string)val;
                        // We append 'X' on serialization
                        // therefore on deserialization we remove last character
                        obj.TestField = value.Substring(0, value.Length - 1);
                    };
                    ti.Properties.Add(field);
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":42,"TestField":"test valueX"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DoNotSerializeValue42()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.PropertyType == typeof(int))
                        {
                            prop.ShouldSerialize = (o, val) =>
                            {
                                return (int)val != 42;
                            };
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 43,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":43,"TestField":"test value"}""", yaml);

            originalObj.TestProperty = 42;
            yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(0, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DoNotSerializePropertyWithNameButDeserializeIt()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClass.TestProperty))
                        {
                            prop.Get = null;
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestField":"test value"}""", yaml);

            yaml = """{"TestProperty":42,"TestField":"test value"}""";

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DoNotDeserializePropertyWithNameButSerializeIt()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClass.TestProperty))
                        {
                            prop.Set = null;
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":42,"TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(0, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SetCustomNumberHandlingForAProperty()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClass.TestProperty))
                        {
                            prop.NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString;
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":"42","TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SetCustomConverterForIntProperty()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClass.TestProperty))
                        {
                            prop.CustomConverter = new PlusOneConverter();
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":43,"TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SetCustomConverterForListProperty()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithLists))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClassWithLists.ListProperty1))
                        {
                            prop.CustomConverter = new AddListEntryConverter();
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClassWithLists originalObj = new TestClassWithLists()
            {
                ListProperty1 = new List<int> { 2, 3 },
                ListProperty2 = new List<int> { 4, 5, 6 },
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"ListProperty1":[2,3,-1],"ListProperty2":[4,5,6]}""", yaml);

            TestClassWithLists deserialized = YamlSerializer.Deserialize<TestClassWithLists>(yaml, options);
            Assert.Equal(originalObj.ListProperty1, deserialized.ListProperty1);
            Assert.Equal(originalObj.ListProperty2, deserialized.ListProperty2);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SetCustomConverterForDictionaryProperty()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithDictionaries))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    foreach (var prop in ti.Properties)
                    {
                        if (prop.Name == nameof(TestClassWithDictionaries.DictionaryProperty1))
                        {
                            prop.CustomConverter = new AddDictionaryEntryConverter();
                        }
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClassWithDictionaries originalObj = new TestClassWithDictionaries()
            {
                DictionaryProperty1 = new Dictionary<string, int>
                {
                    ["test1"] = 4,
                    ["test2"] = 5,
                },
                DictionaryProperty2 = new Dictionary<string, int>
                {
                    ["foo"] = 1,
                    ["bar"] = 8,
                },
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"DictionaryProperty1":{"test1":4,"test2":5,"*test*":-1},"DictionaryProperty2":{"foo":1,"bar":8}}""", yaml);

            TestClassWithDictionaries deserialized = YamlSerializer.Deserialize<TestClassWithDictionaries>(yaml, options);
            Assert.Equal(originalObj.DictionaryProperty1, deserialized.DictionaryProperty1);
            Assert.Equal(originalObj.DictionaryProperty2, deserialized.DictionaryProperty2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void CreateObjectWithDefaults(bool useTypedCreateObject)
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Func<TestClass> createObj = () => new TestClass()
                    {
                        TestField = "test value",
                        TestProperty = 42,
                    };

                    if (useTypedCreateObject)
                    {
                        YamlTypeInfo<TestClass> typedTi = ti as YamlTypeInfo<TestClass>;
                        Assert.NotNull(typedTi);
                        typedTi.CreateObject = createObj;
                    }
                    else
                    {
                        // we want to make sure Func is not a cast to the untyped one
                        ti.CreateObject = () => createObj();
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value 2",
                TestProperty = 45,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":45,"TestField":"test value 2"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);

            yaml = "{}";
            deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal("test value", deserialized.TestField);
            Assert.Equal(42, deserialized.TestProperty);

            yaml = """{"TestField":"test value 2"}""";
            deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(42, deserialized.TestProperty);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void CreateObjectForListWithDefaults(bool useTypedCreateObject)
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(List<int>))
                {
                    Func<List<int>> createObj = () => new List<int> { 99 };

                    if (useTypedCreateObject)
                    {
                        YamlTypeInfo<List<int>> typedTi = ti as YamlTypeInfo<List<int>>;
                        Assert.NotNull(typedTi);
                        typedTi.CreateObject = createObj;
                    }
                    else
                    {
                        // we want to make sure Func is not a cast to the untyped one
                        ti.CreateObject = () => createObj();
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClassWithLists originalObj = new TestClassWithLists()
            {
                ListProperty1 = new List<int> { 2, 3 },
                ListProperty2 = new List<int> { },
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"ListProperty1":[2,3],"ListProperty2":[]}""", yaml);

            TestClassWithLists deserialized = YamlSerializer.Deserialize<TestClassWithLists>(yaml, options);
            Assert.Equal(new List<int> { 99, 2, 3 }, deserialized.ListProperty1);
            Assert.Equal(new List<int> { 99 }, deserialized.ListProperty2);

            yaml = "{}";
            deserialized = YamlSerializer.Deserialize<TestClassWithLists>(yaml, options);
            Assert.Null(deserialized.ListProperty1);
            Assert.Null(deserialized.ListProperty2);

            yaml = """{"ListProperty2":[ 123 ]}""";
            deserialized = YamlSerializer.Deserialize<TestClassWithLists>(yaml, options);
            Assert.Null(deserialized.ListProperty1);
            Assert.Equal(new List<int> { 99, 123 }, deserialized.ListProperty2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void CreateObjectForDictionaryWithDefaults(bool useTypedCreateObject)
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(Dictionary<string, int>))
                {
                    Func<Dictionary<string, int>> createObj = () => new Dictionary<string, int> { ["*test*"] = -1 };

                    if (useTypedCreateObject)
                    {
                        YamlTypeInfo<Dictionary<string, int>> typedTi = ti as YamlTypeInfo<Dictionary<string, int>>;
                        Assert.NotNull(typedTi);
                        typedTi.CreateObject = createObj;
                    }
                    else
                    {
                        // we want to make sure Func is not a cast to the untyped one
                        ti.CreateObject = () => createObj();
                    }
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClassWithDictionaries originalObj = new()
            {
                DictionaryProperty1 = new Dictionary<string, int> { ["test1"] = 2, ["test2"] = 3 },
                DictionaryProperty2 = new Dictionary<string, int>(),
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"DictionaryProperty1":{"test1":2,"test2":3},"DictionaryProperty2":{}}""", yaml);

            TestClassWithDictionaries deserialized = YamlSerializer.Deserialize<TestClassWithDictionaries>(yaml, options);
            Assert.Equal(new Dictionary<string, int> { ["*test*"] = -1, ["test1"] = 2, ["test2"] = 3 }, deserialized.DictionaryProperty1);
            Assert.Equal(new Dictionary<string, int> { ["*test*"] = -1 }, deserialized.DictionaryProperty2);

            yaml = "{}";
            deserialized = YamlSerializer.Deserialize<TestClassWithDictionaries>(yaml, options);
            Assert.Null(deserialized.DictionaryProperty1);
            Assert.Null(deserialized.DictionaryProperty2);

            yaml = """{"DictionaryProperty2":{"foo":123}}""";
            deserialized = YamlSerializer.Deserialize<TestClassWithDictionaries>(yaml, options);
            Assert.Null(deserialized.DictionaryProperty1);
            Assert.Equal(new Dictionary<string, int> { ["*test*"] = -1, ["foo"] = 123 }, deserialized.DictionaryProperty2);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SetCustomNumberHandlingForAType()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClass))
                {
                    Assert.Equal(YamlTypeInfoKind.Object, ti.Kind);
                    ti.NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString;
                }
            });

            YamlSerializerOptions options = new YamlSerializerOptions();
            options.IncludeFields = true;
            options.TypeInfoResolver = resolver;

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 42,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"TestProperty":"42","TestField":"test value"}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CombineCustomResolverWithDefault()
        {
            TestResolver resolver = new TestResolver((Type type, YamlSerializerOptions options) =>
            {
                if (type != typeof(TestClass))
                    return null;

                YamlTypeInfo<TestClass> ti = YamlTypeInfo.CreateYamlTypeInfo<TestClass>(options);
                ti.CreateObject = () => new TestClass()
                {
                    TestField = string.Empty,
                    TestProperty = 42,
                };

                YamlPropertyInfo field = ti.CreateYamlPropertyInfo(typeof(string), "MyTestField");
                field.Get = (o) =>
                {
                    TestClass obj = (TestClass)o;
                    return obj.TestField ?? string.Empty;
                };

                field.Set = (o, val) =>
                {
                    TestClass obj = (TestClass)o;
                    string value = (string?)val ?? string.Empty;
                    obj.TestField = value;
                };

                field.ShouldSerialize = (o, val) => (string)val != string.Empty;

                YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(int), "MyTestProperty");
                prop.Get = (o) =>
                {
                    TestClass obj = (TestClass)o;
                    return obj.TestProperty;
                };

                prop.Set = (o, val) =>
                {
                    TestClass obj = (TestClass)o;
                    obj.TestProperty = (int)val;
                };

                prop.ShouldSerialize = (o, val) => (int)val != 42;

                ti.Properties.Add(field);
                ti.Properties.Add(prop);
                return ti;
            });

            YamlSerializerOptions options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            options.IncludeFields = true;
            options.TypeInfoResolver = YamlTypeInfoResolver.Combine(resolver, options.TypeInfoResolver);

            TestClass originalObj = new TestClass()
            {
                TestField = "test value",
                TestProperty = 45,
            };

            string yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"MyTestField":"test value","MyTestProperty":45}""", yaml);

            TestClass deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);

            originalObj.TestField = null;
            yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"MyTestProperty":45}""", yaml);

            originalObj.TestField = string.Empty;
            yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"MyTestProperty":45}""", yaml);

            deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);

            originalObj.TestField = "test value";
            originalObj.TestProperty = 42;
            yaml = YamlSerializer.Serialize(originalObj, options);
            Assert.Equal("""{"MyTestField":"test value"}""", yaml);
            deserialized = YamlSerializer.Deserialize<TestClass>(yaml, options);
            Assert.Equal(originalObj.TestField, deserialized.TestField);
            Assert.Equal(originalObj.TestProperty, deserialized.TestProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DataContractResolverScenario()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DataContractResolver() };

            var value = new DataContractResolver.TestClass { String = "str", Boolean = true, Int = 42, Ignored = "ignored" };
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"intValue":42,"boolValue":true,"stringValue":"str"}""", yaml);

            DataContractResolver.TestClass result = YamlSerializer.Deserialize<DataContractResolver.TestClass>(yaml, options);
            Assert.Equal("str", result.String);
            Assert.Equal(42, result.Int);
            Assert.True(result.Boolean);
        }

        internal class DataContractResolver : DefaultYamlTypeInfoResolver
        {
            [DataContract]
            public class TestClass
            {
                [YamlIgnore] // ignored by the custom resolver
                [DataMember(Name = "stringValue", Order = 2)]
                public string String { get; set; }

                [YamlPropertyName("BOOL_VALUE")] // ignored by the custom resolver
                [DataMember(Name = "boolValue", Order = 1)]
                public bool Boolean { get; set; }

                [YamlPropertyOrder(int.MaxValue)] // ignored by the custom resolver
                [DataMember(Name = "intValue", Order = 0)]
                public int Int { get; set; }

                [IgnoreDataMember]
                public string Ignored { get; set; }
            }

            public override YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                YamlTypeInfo yamlTypeInfo = base.GetTypeInfo(type, options);

                if (yamlTypeInfo.Kind == YamlTypeInfoKind.Object &&
                    type.GetCustomAttribute<DataContractAttribute>() is not null)
                {
                    yamlTypeInfo.Properties.Clear();

                    foreach (PropertyInfo propInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (propInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                        {
                            continue;
                        }

                        DataMemberAttribute? attr = propInfo.GetCustomAttribute<DataMemberAttribute>();
                        YamlPropertyInfo yamlPropertyInfo = yamlTypeInfo.CreateYamlPropertyInfo(propInfo.PropertyType, attr?.Name ?? propInfo.Name);
                        yamlPropertyInfo.Order = attr?.Order ?? 0;
                        yamlPropertyInfo.Get =
                            propInfo.CanRead
                            ? propInfo.GetValue
                            : null;

                        yamlPropertyInfo.Set = propInfo.CanWrite
                            ? propInfo.SetValue
                            : null;

                        yamlTypeInfo.Properties.Add(yamlPropertyInfo);
                    }
                }

                return yamlTypeInfo;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SpecifiedContractResolverScenario()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new SpecifiedContractResolver() };

            var value = new SpecifiedContractResolver.TestClass { String = "str", Int = 42 };
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("{}", yaml);

            value.IntSpecified = true;
            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"Int":42}""", yaml);

            value.StringSpecified = true;
            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"String":"str","Int":42}""", yaml);
        }

        internal class SpecifiedContractResolver : DefaultYamlTypeInfoResolver
        {
            public class TestClass
            {
                public string String { get; set; }
                [YamlIgnore]
                public bool StringSpecified { get; set; }

                public int Int { get; set; }
                [YamlIgnore]
                public bool IntSpecified { get; set; }
            }
            public override YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                YamlTypeInfo yamlTypeInfo = base.GetTypeInfo(type, options);

                foreach (YamlPropertyInfo property in yamlTypeInfo.Properties)
                {
                    PropertyInfo? specifiedProperty = type.GetProperty(property.Name + "Specified", BindingFlags.Instance | BindingFlags.Public);

                    if (specifiedProperty != null && specifiedProperty.CanRead && specifiedProperty.PropertyType == typeof(bool))
                    {
                        property.ShouldSerialize = (obj, _) => (bool)specifiedProperty.GetValue(obj);
                    }
                }

                return yamlTypeInfo;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void FieldContractResolverScenario()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new FieldContractResolver() };

            var value = FieldContractResolver.TestClass.Create("str", 42, true);
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"_string":"str","_int":42,"_bool":true}""", yaml);

            FieldContractResolver.TestClass result = YamlSerializer.Deserialize<FieldContractResolver.TestClass>(yaml, options);
            Assert.Equal(value, result);
        }

        internal class FieldContractResolver : DefaultYamlTypeInfoResolver
        {
            public class TestClass
            {
                private string _string;
                private int _int;
                private bool _bool;

                public static TestClass Create(string @string, int @int, bool @bool)
                    => new TestClass { _string = @string, _int = @int, _bool = @bool };

                // Should be ignored by the serializer
                public bool Boolean
                {
                    get => _bool;
                    set => throw new NotSupportedException();
                }

                public override int GetHashCode() => (_string, _int, _bool).GetHashCode();
                public override bool Equals(object? other)
                    => other is TestClass tc && (_string, _int, _bool) == (tc._string, tc._int, tc._bool);
            }

            public override YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                YamlTypeInfo yamlTypeInfo = base.GetTypeInfo(type, options);

                if (yamlTypeInfo.Kind == YamlTypeInfoKind.Object)
                {
                    yamlTypeInfo.Properties.Clear();

                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        YamlPropertyInfo yamlPropertyInfo = yamlTypeInfo.CreateYamlPropertyInfo(field.FieldType, field.Name);
                        yamlPropertyInfo.Get = field.GetValue;
                        yamlPropertyInfo.Set = field.SetValue;

                        yamlTypeInfo.Properties.Add(yamlPropertyInfo);
                    }
                }

                return yamlTypeInfo;
            }
        }

        internal class TestClass
        {
            public int TestProperty { get; set; }
            public string TestField;
        }

        internal class TestClassWithLists
        {
            public List<int> ListProperty1 { get; set; }
            public List<int> ListProperty2 { get; set; }
        }

        internal class TestClassWithDictionaries
        {
            public Dictionary<string, int> DictionaryProperty1 { get; set; }
            public Dictionary<string, int> DictionaryProperty2 { get; set; }
        }

        // adds one on write, subtracts one on read
        internal class PlusOneConverter : YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(typeof(int), typeToConvert);
                return reader.GetInt32() - 1;
            }

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(value + 1);
            }
        }

        // adds list entry in the end on write, removes one on read
        internal class AddListEntryConverter : YamlConverter<List<int>>
        {
            public override List<int> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(typeof(List<int>), typeToConvert);
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);

                List<int> list = new();
                int? lastEntry = null;
                while (true)
                {
                    Assert.True(reader.Read());

                    if (reader.TokenType == YamlTokenType.EndArray)
                    {
                        break;
                    }

                    if (lastEntry.HasValue)
                    {
                        // note: we never add last entry
                        list.Add(lastEntry.Value);
                    }

                    Assert.Equal(YamlTokenType.Number, reader.TokenType);
                    lastEntry = reader.GetInt32();
                }

                Assert.True(lastEntry.HasValue);
                Assert.Equal(-1, lastEntry.Value);

                return list;
            }

            public override void Write(Utf8YamlWriter writer, List<int> value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (int element in value)
                {
                    writer.WriteNumberValue(element);
                }

                writer.WriteNumberValue(-1);
                writer.WriteEndArray();
            }
        }

        // Adds extra dictionary entry on write, removes it on read
        internal class AddDictionaryEntryConverter : YamlConverter<Dictionary<string, int>>
        {
            public override Dictionary<string, int> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(typeof(Dictionary<string, int>), typeToConvert);
                Assert.Equal(YamlTokenType.StartObject, reader.TokenType);

                Dictionary<string, int> dict = new();
                KeyValuePair<string, int>? lastEntry = null;

                while (true)
                {
                    Assert.True(reader.Read());

                    if (reader.TokenType == YamlTokenType.EndObject)
                    {
                        break;
                    }

                    if (lastEntry.HasValue)
                    {
                        // note: we never add last entry
                        dict.Add(lastEntry.Value.Key, lastEntry.Value.Value);
                    }

                    Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
                    string? key = reader.GetString();
                    Assert.NotNull(key);
                    Assert.True(reader.Read());

                    Assert.Equal(YamlTokenType.Number, reader.TokenType);
                    lastEntry = new KeyValuePair<string, int>(key, reader.GetInt32());
                }

                Assert.True(lastEntry.HasValue);
                Assert.Equal("*test*", lastEntry.Value.Key);
                Assert.Equal(-1, lastEntry.Value.Value);

                return dict;
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<string, int> value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (var kv in value)
                {
                    writer.WritePropertyName(kv.Key);
                    writer.WriteNumberValue(kv.Value);
                }

                writer.WritePropertyName("*test*");
                writer.WriteNumberValue(-1);
                writer.WriteEndObject();
            }
        }
    }
}
