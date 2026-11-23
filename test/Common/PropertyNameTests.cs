// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file is saved as Unicode in order to test inline (not escaped) unicode characters.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class PropertyNameTests : SerializerTests
    {
        public PropertyNameTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper) { }

        [Fact]
        public async Task BuiltInPolicyDeserializeNoMatch()
        {
            // This is 0 (default value) because the data does not match the property "MyInt16" using the specified policy.
            await DeserializeAndAssert(YamlNamingPolicy.CamelCase, """{"MyInt16":1}""", 0);
            await DeserializeAndAssert(YamlNamingPolicy.SnakeCaseLower, """{"MyInt16":1}""", 0);
            await DeserializeAndAssert(YamlNamingPolicy.SnakeCaseUpper, """{"MyInt16":1}""", 0);
            await DeserializeAndAssert(YamlNamingPolicy.KebabCaseLower, """{"MyInt16":1}""", 0);
            await DeserializeAndAssert(YamlNamingPolicy.KebabCaseUpper, """{"MyInt16":1}""", 0);
        }

        [Fact]
        public async Task BuiltInPolicyDeserializeMatch()
        {
            // This is 1 because the data matches the property "MyInt16" using the specified policy.
            await DeserializeAndAssert(YamlNamingPolicy.CamelCase, """{"myInt16":1}""", 1);
            await DeserializeAndAssert(YamlNamingPolicy.SnakeCaseLower, """{"my_int16":1}""", 1);
            await DeserializeAndAssert(YamlNamingPolicy.SnakeCaseUpper, """{"MY_INT16":1}""", 1);
            await DeserializeAndAssert(YamlNamingPolicy.KebabCaseLower, """{"my-int16":1}""", 1);
            await DeserializeAndAssert(YamlNamingPolicy.KebabCaseUpper, """{"MY-INT16":1}""", 1);
        }

        private async Task DeserializeAndAssert(YamlNamingPolicy policy, string yaml, short expected)
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = policy };
            var obj = await Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options);

            Assert.Equal(expected, obj.MyInt16);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task BuiltInPolicySerialize()
        {
            await SerializeAndAssert(YamlNamingPolicy.CamelCase, """
                "myInt16":0
                """, """
                "myInt32":0
                """);
            await SerializeAndAssert(YamlNamingPolicy.SnakeCaseLower, """
                "my_int16":0
                """, """
                "my_int32":0
                """);
            await SerializeAndAssert(YamlNamingPolicy.SnakeCaseUpper, """
                "MY_INT16":0
                """, """
                "MY_INT32":0
                """);
            await SerializeAndAssert(YamlNamingPolicy.KebabCaseLower, """
                "my-int16":0
                """, """
                "my-int32":0
                """);
            await SerializeAndAssert(YamlNamingPolicy.KebabCaseUpper, """
                "MY-INT16":0
                """, """
                "MY-INT32":0
                """);

            async Task SerializeAndAssert(YamlNamingPolicy policy, string myInt16, string myInt32)
            {
                var options = new YamlSerializerOptions { PropertyNamingPolicy = policy };
                var obj = await Serializer.DeserializeWrapper<SimpleTestClass>("{}", options);

                string yaml = await Serializer.SerializeWrapper(obj, options);

                Assert.Contains(myInt16, yaml);
                Assert.Contains(myInt32, yaml);
            }
        }

        [Fact]
        public async Task CustomNamePolicy()
        {
            var options = new YamlSerializerOptions();
            options.PropertyNamingPolicy = new UppercaseNamingPolicy();

            SimpleTestClass obj = await Serializer.DeserializeWrapper<SimpleTestClass>("""{"MYINT16":1}""", options);

            // This is 1 because the data matches the property "MYINT16" that is uppercase of "myInt16".
            Assert.Equal(1, obj.MyInt16);
        }

        [Fact]
        public async Task NullNamePolicy()
        {
            var options = new YamlSerializerOptions();
            options.PropertyNamingPolicy = new NullNamingPolicy();

            // A policy that returns null is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<SimpleTestClass>("{}", options));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new SimpleTestClass(), options));
        }

        [Fact]
        public async Task IgnoreCase()
        {
            {
                // A non-match scenario with no options (case-sensitive by default).
                SimpleTestClass obj = await Serializer.DeserializeWrapper<SimpleTestClass>("""{"myint16":1}""");
                Assert.Equal(0, obj.MyInt16);
            }

            {
                // A non-match scenario with default options (case-sensitive by default).
                var options = new YamlSerializerOptions();
                SimpleTestClass obj = await Serializer.DeserializeWrapper<SimpleTestClass>("""{"myint16":1}""", options);
                Assert.Equal(0, obj.MyInt16);
            }

            {
                var options = new YamlSerializerOptions();
                options.PropertyNameCaseInsensitive = true;
                SimpleTestClass obj = await Serializer.DeserializeWrapper<SimpleTestClass>("""{"myint16":1}""", options);
                Assert.Equal(1, obj.MyInt16);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlPropertyNameAttribute()
        {
            {
                OverridePropertyNameDesignTime_TestClass obj = await Serializer.DeserializeWrapper<OverridePropertyNameDesignTime_TestClass>("""{"Blah":1}""");
                Assert.Equal(1, obj.myInt);

                obj.myObject = 2;

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Contains("""
                    "Blah":1
                    """, yaml);
                Assert.Contains("""
                    "BlahObject":2
                    """, yaml);
            }

            // The YamlPropertyNameAttribute should be unaffected by YamlNamingPolicy and PropertyNameCaseInsensitive.
            {
                var options = new YamlSerializerOptions();
                options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;
                options.PropertyNameCaseInsensitive = true;

                OverridePropertyNameDesignTime_TestClass obj = await Serializer.DeserializeWrapper<OverridePropertyNameDesignTime_TestClass>("""{"Blah":1}""", options);
                Assert.Equal(1, obj.myInt);

                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Contains("""
                    "Blah":1
                    """, yaml);
            }
        }

        [Fact]
        public async Task YamlNameAttributeDuplicateDesignTimeFail()
        {
            {
                var options = new YamlSerializerOptions();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<DuplicatePropertyNameDesignTime_TestClass>("{}", options));
            }

            {
                var options = new YamlSerializerOptions();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new DuplicatePropertyNameDesignTime_TestClass(), options));
            }
        }

        [Fact]
        public async Task YamlNameConflictOnCamelCasingFail()
        {
            {
                // Baseline comparison - no options set.
                IntPropertyNamesDifferentByCaseOnly_TestClass obj = await Serializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>("{}");
                await Serializer.SerializeWrapper(obj);
            }

            {
                var options = new YamlSerializerOptions();
                options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>("{}", options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new IntPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }

            {
                // Baseline comparison - no options set.
                ObjectPropertyNamesDifferentByCaseOnly_TestClass obj = await Serializer.DeserializeWrapper<ObjectPropertyNamesDifferentByCaseOnly_TestClass>("{}");
                await Serializer.SerializeWrapper(obj);
            }

            {
                var options = new YamlSerializerOptions();
                options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ObjectPropertyNamesDifferentByCaseOnly_TestClass>("{}", options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ObjectPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlOutputNotAffectedByCasingPolicy()
        {
            {
                // Baseline.
                string yaml = await Serializer.SerializeWrapper(new SimpleTestClass());
                Assert.Contains("""
                    "MyInt16":0
                    """, yaml);
            }

            // The JSON output should be unaffected by PropertyNameCaseInsensitive.
            {
                var options = new YamlSerializerOptions();
                options.PropertyNameCaseInsensitive = true;

                string yaml = await Serializer.SerializeWrapper(new SimpleTestClass(), options);
                Assert.Contains("""
                    "MyInt16":0
                    """, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task EmptyPropertyName()
        {
            string yaml = """{"":1}""";

            {
                var obj = new EmptyPropertyName_TestClass();
                obj.MyInt1 = 1;

                string yamlOut = await Serializer.SerializeWrapper(obj);
                Assert.Equal(yaml, yamlOut);
            }

            {
                EmptyPropertyName_TestClass obj = await Serializer.DeserializeWrapper<EmptyPropertyName_TestClass>(yaml);
                Assert.Equal(1, obj.MyInt1);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnicodePropertyNames()
        {
            ClassWithUnicodeProperty obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u0467\":1}");
            Assert.Equal(1, obj.A\u0467);

            // Specifying encoder on options does not impact deserialize.
            var options = new YamlSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u0467\":1}", options);
            Assert.Equal(1, obj.A\u0467);

            string yaml;

            // Verify the name is escaped after serialize.
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "A\u0467":1
                """, yaml);

            // With custom escaper
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("\"A\u0467\":1", yaml);

            // Verify the name is unescaped after deserialize.
            obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>(yaml);
            Assert.Equal(1, obj.A\u0467);

            // With custom escaper
            obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>(yaml, options);
            Assert.Equal(1, obj.A\u0467);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task UnicodePropertyNamesWithPooledAlloc()
        {
            // We want to go over StackallocByteThreshold=256 to force a pooled allocation, so this property is 400 chars and 401 bytes.
            ClassWithUnicodeProperty obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\":1}");
            Assert.Equal(1, obj.A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890);

            // Verify the name is escaped after serialize.
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Contains("""
                "A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890":1
                """, yaml);

            // Verify the name is unescaped after deserialize.
            obj = await Serializer.DeserializeWrapper<ClassWithUnicodeProperty>(yaml);
            Assert.Equal(1, obj.A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890);
        }

        public class ClassWithPropertyNamePermutations
        {
            public int a { get; set; }
            public int aa { get; set; }
            public int aaa { get; set; }
            public int aaaa { get; set; }
            public int aaaaa { get; set; }
            public int aaaaaa { get; set; }

            // 7 characters - caching code only keys up to 7.
            public int aaaaaaa { get; set; }
            public int aaaaaab { get; set; }

            // 8 characters.
            public int aaaaaaaa { get; set; }
            public int aaaaaaab { get; set; }

            // 9 characters.
            public int aaaaaaaaa { get; set; }
            public int aaaaaaaab { get; set; }

            public int \u0467 { get; set; }
            public int \u0467\u0467 { get; set; }
            public int \u0467\u0467a { get; set; }
            public int \u0467\u0467b { get; set; }
            public int \u0467\u0467\u0467 { get; set; }
            public int \u0467\u0467\u0467a { get; set; }
            public int \u0467\u0467\u0467b { get; set; }
            public int \u0467\u0467\u0467\u0467 { get; set; }
            public int \u0467\u0467\u0467\u0467a { get; set; }
            public int \u0467\u0467\u0467\u0467b { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CachingKeys()
        {
            ClassWithPropertyNamePermutations obj;

            void Verify()
            {
                Assert.Equal(1, obj.a);
                Assert.Equal(2, obj.aa);
                Assert.Equal(3, obj.aaa);
                Assert.Equal(4, obj.aaaa);
                Assert.Equal(5, obj.aaaaa);
                Assert.Equal(6, obj.aaaaaa);
                Assert.Equal(7, obj.aaaaaaa);
                Assert.Equal(7, obj.aaaaaab);
                Assert.Equal(8, obj.aaaaaaaa);
                Assert.Equal(8, obj.aaaaaaab);
                Assert.Equal(9, obj.aaaaaaaaa);
                Assert.Equal(9, obj.aaaaaaaab);

                Assert.Equal(2, obj.\u0467);
                Assert.Equal(4, obj.\u0467\u0467);
                Assert.Equal(5, obj.\u0467\u0467a);
                Assert.Equal(5, obj.\u0467\u0467b);
                Assert.Equal(6, obj.\u0467\u0467\u0467);
                Assert.Equal(7, obj.\u0467\u0467\u0467a);
                Assert.Equal(7, obj.\u0467\u0467\u0467b);
                Assert.Equal(8, obj.\u0467\u0467\u0467\u0467);
                Assert.Equal(9, obj.\u0467\u0467\u0467\u0467a);
                Assert.Equal(9, obj.\u0467\u0467\u0467\u0467b);
            }

            obj = new ClassWithPropertyNamePermutations
            {
                a = 1,
                aa = 2,
                aaa = 3,
                aaaa = 4,
                aaaaa = 5,
                aaaaaa = 6,
                aaaaaaa = 7,
                aaaaaab = 7,
                aaaaaaaa = 8,
                aaaaaaab = 8,
                aaaaaaaaa = 9,
                aaaaaaaab = 9,
                \u0467 = 2,
                \u0467\u0467 = 4,
                \u0467\u0467a = 5,
                \u0467\u0467b = 5,
                \u0467\u0467\u0467 = 6,
                \u0467\u0467\u0467a = 7,
                \u0467\u0467\u0467b = 7,
                \u0467\u0467\u0467\u0467 = 8,
                \u0467\u0467\u0467\u0467a = 9,
                \u0467\u0467\u0467\u0467b = 9,
            };

            // Verify baseline.
            Verify();

            string yaml = await Serializer.SerializeWrapper(obj);

            // Verify the length is consistent with a verified value.
            Assert.Equal(354, yaml.Length);

            obj = await Serializer.DeserializeWrapper<ClassWithPropertyNamePermutations>(yaml);

            // Verify round-tripped object.
            Verify();
        }

        [Fact]
        public async Task BadNamingPolicy_ThrowsInvalidOperation()
        {
            var options = new YamlSerializerOptions { DictionaryKeyPolicy = new NullNamingPolicy() };

            var inputPrimitive = new Dictionary<string, int>
            {
                { "validKey", 1 }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(inputPrimitive, options));

            var inputClass = new Dictionary<string, OverridePropertyNameDesignTime_TestClass>
            {
                { "validKey", new OverridePropertyNameDesignTime_TestClass() }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(inputClass, options));
        }

        public class OverridePropertyNameDesignTime_TestClass
        {
            [YamlPropertyName("Blah")]
            public int myInt { get; set; }

            [YamlPropertyName("BlahObject")]
            public object? myObject { get; set; }
        }

        public class DuplicatePropertyNameDesignTime_TestClass
        {
            [YamlPropertyName("Blah")]
            public int MyInt1 { get; set; }

            [YamlPropertyName("Blah")]
            public int MyInt2 { get; set; }
        }

        public class EmptyPropertyName_TestClass
        {
            [YamlPropertyName("")]
            public int MyInt1 { get; set; }
        }

        public class NullPropertyName_TestClass
        {
            [YamlPropertyName(null)]
            public int MyInt1 { get; set; }
        }

        public class IntPropertyNamesDifferentByCaseOnly_TestClass
        {
            public int myInt { get; set; }
            public int MyInt { get; set; }
        }

        public class ObjectPropertyNamesDifferentByCaseOnly_TestClass
        {
            public int myObject { get; set; }
            public int MyObject { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SpecialCharacters()
        {
            ClassWithSpecialCharacters obj = new()
            {
                Baseline = 1,
                Schema = 2,
                SmtpId = 3,
                Emojies = 4,
                \uA000 = 5,
                YiIt_2 = 6,
                PropertyNameWithWhitespace = 7,
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(
                "{\"Baseline\":1," +
                "\"$schema\":2," +
                "\"smtp-id\":3," +
                "\"\\uD83D\\uDE00\\uD83D\\uDE01\":4," +
                "\"\\uA000\":5," +
                "\"\\uA000_2\":6," +
                "\"\\u0022ab \\n\\r\\t\\f\\bc\\u0022\":7}", yaml);

            obj = await Serializer.DeserializeWrapper<ClassWithSpecialCharacters>(yaml);
            Assert.Equal(1, obj.Baseline);
            Assert.Equal(2, obj.Schema);
            Assert.Equal(3, obj.SmtpId);
            Assert.Equal(4, obj.Emojies);
            Assert.Equal(5, obj.\uA000);
            Assert.Equal(6, obj.YiIt_2);
        }

        public class ClassWithSpecialCharacters
        {
            [YamlPropertyOrder(1)]
            public int Baseline { get; set; }

            [YamlPropertyOrder(2)]
            [YamlPropertyName("$schema")] // Invalid C# property name.
            public int Schema { get; set; }

            [YamlPropertyOrder(3)]
            [YamlPropertyName("smtp-id")] // Invalid C# property name.
            public int SmtpId { get; set; }

            [YamlPropertyOrder(4)]
            [YamlPropertyName("\uD83D\uDE00\uD83D\uDE01")] // Invalid C# property name. Unicode:\uD83D\uDE00\uD83D\uDE01
            public int Emojies { get; set; }

            [YamlPropertyOrder(5)]
            public int \uA000 { get; set; } // Valid C# property name. Unicode:\uA000

            [YamlPropertyOrder(6)]
            [YamlPropertyName("\uA000_2")] // Valid C# property name: \uA000_2
            public int YiIt_2 { get; set; }

            [YamlPropertyOrder(7)]
            [YamlPropertyName("\"ab \n\r\t\f\bc\"")] // Regression test for https://github.com/dotnet/runtime/issues/98638
            public int PropertyNameWithWhitespace { get; set; }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public async Task ClassWithIgnoredCaseInsensitiveConflict_RespectsIgnoredMember(bool propertyNameCaseInsensitive)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/93903
            // specifically for propertyNameCaseInsensitive := true

            YamlSerializerOptions options = Serializer.CreateOptions(makeReadOnly: false);
            options.PropertyNameCaseInsensitive = propertyNameCaseInsensitive;

            var value = new ClassWithIgnoredCaseInsensitiveConflict { name = "lowercase", Name = "uppercase" };
            string yaml = await Serializer.SerializeWrapper(value, options);

            Assert.Equal("""{"name":"lowercase"}""", yaml);

            value = await Serializer.DeserializeWrapper<ClassWithIgnoredCaseInsensitiveConflict>(yaml, options);
            Assert.Equal("lowercase", value.name);
            Assert.Null(value.Name);
        }

        public class ClassWithIgnoredCaseInsensitiveConflict
        {
            public string name { get; set; }

            [YamlIgnore]
            public string Name { get; set; }
        }

        [Fact]
        public async Task DuplicatesIgnoreCase()
        {
            string yaml = """{ "myint32":1, "MYINT32":2 }""";

            YamlSerializerOptions options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, PropertyNameCaseInsensitive = true };
            Assert.Equal(2, (await Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options)).MyInt32);

            options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;
            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options));
            Assert.Contains("Duplicate", ex.Message);
        }

        [Fact]
        public async Task DuplicatesByKebabCaseNamingPolicy()
        {
            string yaml = """{ "my-int32":1, "my-int32":2 }""";

            YamlSerializerOptions options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower };
            Assert.Equal(2, (await Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options)).MyInt32);

            options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, AllowDuplicateProperties = false, PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower };
            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options));
            Assert.Contains("Duplicate", ex.Message);
        }

        [Fact]
        public async Task DuplicatesByKebabCaseNamingPolicyIgnoreCase()
        {
            string yaml = """{ "my-int32":1, "MY-int32":2 }""";

            YamlSerializerOptions options = new YamlSerializerOptions
            { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower,
                PropertyNameCaseInsensitive = true,
            };

            Assert.Equal(2, (await Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options)).MyInt32);

            options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                AllowDuplicateProperties = false,
                PropertyNamingPolicy = YamlNamingPolicy.KebabCaseLower,
                PropertyNameCaseInsensitive = true,
            };

            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<SimpleTestClass>(yaml, options));
            Assert.Contains("Duplicate", ex.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_TypeLevel_Serialize()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithCamelCaseNamingPolicyAttribute { MyValue = "test" });
            Assert.Equal("""{"myValue":"test"}""", yaml);
        }

        [Fact]
        public async Task YamlNamingPolicyAttribute_TypeLevel_Deserialize()
        {
            var obj = await Serializer.DeserializeWrapper<ClassWithCamelCaseNamingPolicyAttribute>("""{"myValue":"test"}""");
            Assert.Equal("test", obj.MyValue);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_TypeLevel_OverridesGlobalPolicy()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithCamelCaseNamingPolicyAttribute { MyValue = "test" }, options);
            Assert.Equal("""{"myValue":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_MemberLevel_Serialize()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithMemberNamingPolicyAttribute { MyFirstProperty = "first", MySecondProperty = "second" });
            Assert.Equal("""{"MyFirstProperty":"first","my-second-property":"second"}""", yaml);
        }

        [Fact]
        public async Task YamlNamingPolicyAttribute_MemberLevel_Deserialize()
        {
            var obj = await Serializer.DeserializeWrapper<ClassWithMemberNamingPolicyAttribute>("""{"MyFirstProperty":"first","my-second-property":"second"}""");
            Assert.Equal("first", obj.MyFirstProperty);
            Assert.Equal("second", obj.MySecondProperty);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_MemberLevel_OverridesTypeLevelAndGlobal()
        {
            var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower };
            string yaml = await Serializer.SerializeWrapper(new ClassWithMixedNamingPolicies { MyFirstProperty = "first", MySecondProperty = "second" }, options);
            Assert.Equal("""{"myFirstProperty":"first","my-second-property":"second"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_MemberLevel_OverridesTypeLevel()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithMixedNamingPolicies { MyFirstProperty = "first", MySecondProperty = "second" });
            Assert.Equal("""{"myFirstProperty":"first","my-second-property":"second"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task YamlNamingPolicyAttribute_YamlPropertyNameTakesPrecedence()
        {
            string yaml = await Serializer.SerializeWrapper(new ClassWithNamingPolicyAndPropertyName { MyValue = "test" });
            Assert.Equal("""{"custom_name":"test"}""", yaml);
        }

        [Theory]
        [InlineData(YamlKnownNamingPolicy.CamelCase, @"""myTestProperty""")]
        [InlineData(YamlKnownNamingPolicy.SnakeCaseLower, @"""my_test_property""")]
        [InlineData(YamlKnownNamingPolicy.SnakeCaseUpper, @"""MY_TEST_PROPERTY""")]
        [InlineData(YamlKnownNamingPolicy.KebabCaseLower, @"""my-test-property""")]
        [InlineData(YamlKnownNamingPolicy.KebabCaseUpper, @"""MY-TEST-PROPERTY""")]
        [InlineData(YamlKnownNamingPolicy.PascalCase, @"""MyTestProperty""")]
        public void YamlNamingPolicyAttribute_AllKnownPolicies(YamlKnownNamingPolicy policy, string expectedPropertyName)
        {
            var attribute = new YamlNamingPolicyAttribute(policy);
            string converted = attribute.NamingPolicy.ConvertName("MyTestProperty");
            Assert.Equal(expectedPropertyName.Trim('"'), converted);
        }

        [Fact]
        public void YamlNamingPolicyAttribute_InvalidPolicy_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlNamingPolicyAttribute((YamlKnownNamingPolicy)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlNamingPolicyAttribute(YamlKnownNamingPolicy.Unspecified));
        }
    }

    [YamlNamingPolicyAttribute(YamlKnownNamingPolicy.CamelCase)]
    public class ClassWithCamelCaseNamingPolicyAttribute
    {
        public string MyValue { get; set; }
    }

    public class ClassWithMemberNamingPolicyAttribute
    {
        public string MyFirstProperty { get; set; }

        [YamlNamingPolicyAttribute(YamlKnownNamingPolicy.KebabCaseLower)]
        public string MySecondProperty { get; set; }
    }

    [YamlNamingPolicyAttribute(YamlKnownNamingPolicy.CamelCase)]
    public class ClassWithMixedNamingPolicies
    {
        public string MyFirstProperty { get; set; }

        [YamlNamingPolicyAttribute(YamlKnownNamingPolicy.KebabCaseLower)]
        public string MySecondProperty { get; set; }
    }

    public class ClassWithNamingPolicyAndPropertyName
    {
        [YamlNamingPolicyAttribute(YamlKnownNamingPolicy.CamelCase)]
        [YamlPropertyName("custom_name")]
        public string MyValue { get; set; }
    }

    /// <summary>
    /// A custom naming policy that converts property names to all uppercase.
    /// </summary>
    public class UpperCaseNamingPolicy : YamlNamingPolicy
    {
        public override string ConvertName(string name) => name.ToUpperInvariant();
    }

    /// <summary>
    /// A derived YamlNamingPolicyAttribute that uses a custom naming policy (not a known policy).
    /// </summary>
    public class YamlUpperCaseNamingPolicyAttribute : YamlNamingPolicyAttribute
    {
        public YamlUpperCaseNamingPolicyAttribute() : base(new UpperCaseNamingPolicy()) { }
    }

    [YamlUpperCaseNamingPolicy]
    public class ClassWithCustomDerivedNamingPolicyAttribute
    {
        public string MyValue { get; set; }
    }

    public class ClassWithCustomDerivedMemberNamingPolicyAttribute
    {
        [YamlUpperCaseNamingPolicy]
        public string MyValue { get; set; }
    }
}
