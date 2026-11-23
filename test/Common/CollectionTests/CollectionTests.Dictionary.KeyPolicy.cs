// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class CollectionTests
    {
        [Fact]
        public async Task CamelCaseDeserialize()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase // e.g. Key1 -> key1.
            };

            const string YamlString = """[{"Key1":1,"Key2":2},{"Key1":3,"Key2":4}]""";

            // Without key policy, deserialize keys as they are.
            Dictionary<string, int>[] obj = await Serializer.DeserializeWrapper<Dictionary<string, int>[]>(YamlString);

            Assert.Equal(2, obj.Length);

            Assert.Equal(2, obj[0].Count);
            Assert.Equal(1, obj[0]["Key1"]);
            Assert.Equal(2, obj[0]["Key2"]);

            Assert.Equal(2, obj[1].Count);
            Assert.Equal(3, obj[1]["Key1"]);
            Assert.Equal(4, obj[1]["Key2"]);

            // Ensure we ignore key policy and deserialize keys as they are.
            obj = await Serializer.DeserializeWrapper<Dictionary<string, int>[]>(YamlString, options);

            Assert.Equal(2, obj.Length);

            Assert.Equal(2, obj[0].Count);
            Assert.Equal(1, obj[0]["Key1"]);
            Assert.Equal(2, obj[0]["Key2"]);

            Assert.Equal(2, obj[1].Count);
            Assert.Equal(3, obj[1]["Key1"]);
            Assert.Equal(4, obj[1]["Key2"]);
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Need extension data support.")]
#endif
        [Trait("category", "jsonoutput")]
        public async Task IgnoreKeyPolicyForExtensionData()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase // e.g. Key1 -> key1.
            };

            // Ensure we ignore key policy for extension data and deserialize keys as they are.
            ClassWithExtensionData myClass = await Serializer.DeserializeWrapper<ClassWithExtensionData>("""{"Key1":1, "Key2":2}""", options);
            Assert.Equal(1, (myClass.ExtensionData["Key1"]).GetInt32());
            Assert.Equal(2, (myClass.ExtensionData["Key2"]).GetInt32());

            // Ensure we ignore key policy for extension data and serialize keys as they are.
            Assert.Equal("""{"Key1":1,"Key2":2}""", await Serializer.SerializeWrapper(myClass, options));
        }

        public class ClassWithExtensionData
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize()
        {
            var options = new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase // e.g. Key1 -> key1.
            };

            Dictionary<string, int>[] obj = new Dictionary<string, int>[]
            {
                new Dictionary<string, int>() { { "Key1", 1 }, { "Key2", 2 } },
                new Dictionary<string, int>() { { "Key1", 3 }, { "Key2", 4 } },
            };

            const string Yaml = """[{"Key1":1,"Key2":2},{"Key1":3,"Key2":4}]""";
            const string YamlCamel = """[{"key1":1,"key2":2},{"key1":3,"key2":4}]""";

            // Without key policy option, serialize keys as they are.
            string yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(Yaml, yaml);

            // With key policy option, serialize keys with camel casing.
            yaml = await Serializer.SerializeWrapper<object>(obj, options);
            Assert.Equal(YamlCamel, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_Null_Values()
        {
            var options = new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase // e.g. Key1 -> key1.
            };

            Dictionary<string, string>[] obj = new Dictionary<string, string>[]
            {
                new Dictionary<string, string>() { { "Key1", null }, { "Key2", null } },
            };

            const string Yaml = """[{"Key1":null,"Key2":null}]""";
            const string YamlCamel = """[{"key1":null,"key2":null}]""";

            // Without key policy option, serialize keys as they are.
            string yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(Yaml, yaml);

            // With key policy option, serialize keys with camel casing.
            yaml = await Serializer.SerializeWrapper<object>(obj, options);
            Assert.Equal(YamlCamel, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_Null_Nullable_Values()
        {
            var options = new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase // e.g. Key1 -> key1.
            };

            Dictionary<string, int?>[] obj = new Dictionary<string, int?>[]
            {
                new Dictionary<string, int?>() { { "Key1", null }, { "Key2", null } },
            };

            const string Yaml = """[{"Key1":null,"Key2":null}]""";
            const string YamlCamel = """[{"key1":null,"key2":null}]""";

            // Without key policy option, serialize keys as they are.
            string yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(Yaml, yaml);

            // With key policy option, serialize keys with camel casing.
            yaml = await Serializer.SerializeWrapper<object>(obj, options);
            Assert.Equal(YamlCamel, yaml);
        }

        [Fact]
        public async Task CustomNameDeserialize()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new UppercaseNamingPolicy() // e.g. myint -> MYINT.
            };


            // Without key policy, deserialize keys as they are.
            Dictionary<string, int> obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>("""{"myint":1}""");
            Assert.Equal(1, obj["myint"]);

            // Ensure we ignore key policy and deserialize keys as they are.
            obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>("""{"myint":1}""", options);
            Assert.Equal(1, obj["myint"]);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomNameSerialize()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new UppercaseNamingPolicy() // e.g. myint -> MYINT.
            };

            Dictionary<string, int> obj = new Dictionary<string, int> { { "myint1", 1 }, { "myint2", 2 } };

            const string Yaml = """{"myint1":1,"myint2":2}""";
            const string YamlCustomKey = """{"MYINT1":1,"MYINT2":2}""";

            // Without key policy option, serialize keys as they are.
            string yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(Yaml, yaml);

            // With key policy option, serialize keys honoring the custom key policy.
            yaml = await Serializer.SerializeWrapper<object>(obj, options);
            Assert.Equal(YamlCustomKey, yaml);
        }

        public enum ETestEnum
        {
            TestValue1 = 1,
            TestValue2 = 2,
        }
      
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task EnumSerialization_DictionaryPolicy_Honored_CamelCase()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase,
            };

            Dictionary<ETestEnum, ETestEnum> dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue1] = ETestEnum.TestValue1 };
            string value = await Serializer.SerializeWrapper(dict, options);
            Assert.Equal("{\"testValue1\":1}", value);

            dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue2] = ETestEnum.TestValue2 };
            value = await Serializer.SerializeWrapper(dict, options);
            Assert.Equal("{\"testValue2\":2}", value);

            dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue1] = ETestEnum.TestValue1, [ETestEnum.TestValue2] = ETestEnum.TestValue2 };
            value = await Serializer.SerializeWrapper(dict, options);
            Assert.Equal("{\"testValue1\":1,\"testValue2\":2}", value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task EnumSerializationAsDictKey_NoDictionaryKeyPolicy()
        {
            Dictionary<ETestEnum, ETestEnum> dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue1] = ETestEnum.TestValue1 };
            string value = await Serializer.SerializeWrapper(dict);
            Assert.Equal("{\"TestValue1\":1}", value);

            dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue2] = ETestEnum.TestValue2 };
            value = await Serializer.SerializeWrapper(dict);
            Assert.Equal("{\"TestValue2\":2}", value);

            dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue1] = ETestEnum.TestValue1, [ETestEnum.TestValue2] = ETestEnum.TestValue2 };
            value = await Serializer.SerializeWrapper(dict);
            Assert.Equal("{\"TestValue1\":1,\"TestValue2\":2}", value);
        }

        public class ClassWithEnumProperties
        {
            public ETestEnum TestEnumProperty1 { get; } = ETestEnum.TestValue2;
            public DayOfWeek TestEnumProperty2 { get; } = DayOfWeek.Monday;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task EnumSerialization_DictionaryPolicy_NotApplied_WhenEnumsAreSerialized()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase,
            };

            string value = await Serializer.SerializeWrapper(DayOfWeek.Friday, options);

            Assert.Equal("5", value);

            value = await Serializer.SerializeWrapper(ETestEnum.TestValue2, options);

            Assert.Equal("2", value);

            value = await Serializer.SerializeWrapper(new ClassWithEnumProperties(), options);

            Assert.Equal("{\"TestEnumProperty1\":2,\"TestEnumProperty2\":1}", value);

            value = await Serializer.SerializeWrapper(new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }, options);

            Assert.Equal("[0,1,2,3,4,5,6]", value);
        }

        public class CustomYamlNamingPolicy : YamlNamingPolicy
        {
            public override string ConvertName(string name) => null;
        }

        [Fact]
        public async Task EnumSerialization_DictionaryPolicy_ThrowsException_WhenNamingPolicyReturnsNull()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new CustomYamlNamingPolicy(),
            };

            Dictionary<ETestEnum, ETestEnum> dict = new Dictionary<ETestEnum, ETestEnum> { [ETestEnum.TestValue1] = ETestEnum.TestValue1 };

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.SerializeWrapper(dict, options));

            Assert.Contains("uses unsupported identifier", ex.Message);
        }

        [Fact]
        public async Task NullNamePolicy()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new NullNamingPolicy()
            };

            // A naming policy that returns null is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new Dictionary<string, int> { { "onlyKey", 1 } }, options));

            // We don't use policy on deserialize, so we populate dictionary.
            Dictionary<string, int> obj = await Serializer.DeserializeWrapper<Dictionary<string, int>>("""{"onlyKey": 1}""", options);

            Assert.Equal(1, obj.Count);
            Assert.Equal(1, obj["onlyKey"]);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomNameSerialize_NullableValue()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new UppercaseNamingPolicy() // e.g. myint -> MYINT.
            };

            Dictionary<string, int?> obj = new Dictionary<string, int?> { { "myint1", 1 }, { "myint2", 2 } };

            const string Yaml = """{"myint1":1,"myint2":2}""";
            const string YamlCustomKey = """{"MYINT1":1,"MYINT2":2}""";

            // Without key policy option, serialize keys as they are.
            string yaml = await Serializer.SerializeWrapper<object>(obj);
            Assert.Equal(Yaml, yaml);

            // With key policy option, serialize keys honoring the custom key policy.
            yaml = await Serializer.SerializeWrapper<object>(obj, options);
            Assert.Equal(YamlCustomKey, yaml);
        }

        [Fact]
        public async Task NullNamePolicy_NullableValue()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = new NullNamingPolicy()
            };

            // A naming policy that returns null is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new Dictionary<string, int?> { { "onlyKey", 1 } }, options));

            // We don't use policy on deserialize, so we populate dictionary.
            Dictionary<string, int?> obj = await Serializer.DeserializeWrapper<Dictionary<string, int?>>("""{"onlyKey": 1}""", options);

            Assert.Equal(1, obj.Count);
            Assert.Equal(1, obj["onlyKey"]);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task KeyConflict_Serialize_WriteAll()
        {
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            };

            // The camel case policy resolves two keys to the same output key.
            Dictionary<string, int> obj = new Dictionary<string, int> { { "myInt", 1 }, { "MyInt", 2 } };
            string yaml = await Serializer.SerializeWrapper(obj, options);

            // Check that we write all.
            Assert.Equal("""{"myInt":1,"myInt":2}""", yaml);
        }
        
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_ApplyDictionaryKeyPolicy()
        {
            const string YamlCamel = """{"keyDict":{"keyString":"text","keyNumber":1000,"keyBool":true},"keyList":[1,2,3]}""";
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            };
            
            var obj = new Dictionary<string, object>();
            obj["KeyDict"] = new Dictionary<string, object>()
            {
                { "KeyString", "text" },
                { "KeyNumber", 1000 },
                { "KeyBool", true }
            };
            obj["KeyList"] = new List<int>() { 1, 2, 3 };
            
            var yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });
            
            Assert.Equal(YamlCamel, yaml);
        }
        
        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Need extension data support.")]
#endif
        [Trait("category", "jsonoutput")]
        public async Task SerializationWithYamlExtensionDataAttribute_IgoneDictionaryKeyPolicy()
        {
            var expectedYaml = """{"KeyInt":1000,"KeyString":"text","KeyBool":true,"KeyObject":{},"KeyList":[],"KeyDictionary":{}}""";
            var obj = new ClassWithExtensionDataProperty();
            obj.Data = new Dictionary<string, object>()
            {
                { "KeyInt", 1000 },
                { "KeyString", "text" },
                { "KeyBool", true },
                { "KeyObject", new object() },
                { "KeyList", new List<string>() },
                { "KeyDictionary", new Dictionary<string, string>() }
            };
            string yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });
            Assert.Equal(expectedYaml, yaml);
        }

        private class ClassWithExtensionDataProperty
        {
           [YamlExtensionData]
            public Dictionary<string, object> Data { get; set; }
        }
        
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_ForTypedDictionary_ApplyDictionaryKeyPolicy()
        {
            const string YamlCamel = """{"keyDict":{"Name":"text","Number":1000,"isValid":true,"Values":[1,2,3]}}""";
            var obj = new Dictionary<string, CustomClass>()
            {
                { "KeyDict", CreateCustomObject() }
            };
            var yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });

            Assert.Equal(YamlCamel, yaml);
        }

        public class CustomClass
        {
            public string Name { get; set; }
            public int Number { get; set; }
            public bool isValid { get; set; }
            public List<int> Values { get; set; }
        }
        
        private static CustomClass CreateCustomObject()
        {
            return new CustomClass { Name = "text", Number = 1000, isValid = true, Values = new List<int>() { 1, 2, 3 } };
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_ForNestedTypedDictionary_ApplyDictionaryKeyPolicy()
        {
            const string YamlCamel = """{"keyDict":{"nestedKeyDict":{"Name":"text","Number":1000,"isValid":true,"Values":[1,2,3]}}}""";
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            };
            var obj = new Dictionary<string, Dictionary<string, CustomClass>>(){
                { "KeyDict", new  Dictionary<string,CustomClass>()
                {{ "NestedKeyDict", CreateCustomObject() }}
            }};
            var yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });

            Assert.Equal(YamlCamel, yaml);
        }

        public class TestClassWithDictionary
        {
           public Dictionary<string, CustomClass> Data { get; set; }
		}

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_ForClassWithDictionaryProperty_ApplyDictionaryKeyPolicy()
        {
            const string YamlCamel = """{"Data":{"keyObj":{"Name":"text","Number":1000,"isValid":true,"Values":[1,2,3]}}}""";
            var obj = new TestClassWithDictionary();
            obj.Data = new Dictionary<string, CustomClass> {
                {"KeyObj", CreateCustomObject() }
            };
            var yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });
            Assert.Equal(YamlCamel, yaml);
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Need KVP support.")]
#endif
        [Trait("category", "jsonoutput")]
        public async Task CamelCaseSerialize_ForKeyValuePairWithDictionaryValue_ApplyDictionaryKeyPolicy()
        {
            const string YamlCamel = """{"Key":"KeyPair","Value":{"keyDict":{"Name":"text","Number":1000,"isValid":true,"Values":[1,2,3]}}}""";
            var options = new YamlSerializerOptions
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            };
            var obj = new KeyValuePair<string, Dictionary<string, CustomClass>>
              ("KeyPair", new Dictionary<string, CustomClass> {
              {"KeyDict", CreateCustomObject() }
            });
            var yaml = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.CamelCase
            });

            Assert.Equal(YamlCamel, yaml);
        }
    }
}
