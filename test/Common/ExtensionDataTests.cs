// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Yaml.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class ExtensionDataTests : SerializerTests
    {
        public ExtensionDataTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper) { }

        [Fact]
        public async Task EmptyPropertyName_WinsOver_ExtensionDataEmptyPropertyName()
        {
            string yaml = """{"":1}""";

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            YamlSerializerOptions options = new YamlSerializerOptions();

            // Verify the real property wins over the extension data property.
            obj = await Serializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(yaml, options);
            Assert.Equal(1, obj.MyInt1);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
        public async Task EmptyPropertyNameInExtensionData()
        {
            {
                string yaml = """{"":42}""";
                EmptyClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(yaml, YamlTestSerializerOptions.LastWins);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(42, obj.MyOverflow[""].GetInt32());
            }

            {
                // Verify that last-in wins.
                string yaml = """{"":42, "":43}""";
                EmptyClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(yaml, YamlTestSerializerOptions.LastWins);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(43, obj.MyOverflow[""].GetInt32());
            }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Needs SimpleTestClass support.")]
#endif
        public async Task ExtensionPropertyNotUsed()
        {
            string yaml = @"{""MyNestedClass"":" + SimpleTestClass.s_yaml + "}";
            ClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yaml);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
        public async Task ExtensionPropertyRoundTrip()
        {
            ClassWithExtensionProperty obj;

            {
                string yaml = @"{""MyIntMissing"":2, ""MyInt"":1, ""MyNestedClassMissing"":" + SimpleTestClass.s_yaml + "}";
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yaml);
                Verify();
            }

            // Round-trip the json.
            {
                string yaml = await Serializer.SerializeWrapper(obj);
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yaml);
                Verify();

                // The json should not contain the dictionary name.
                Assert.DoesNotContain(nameof(ClassWithExtensionProperty.MyOverflow), yaml);
            }

            void Verify()
            {
                Assert.NotNull(obj.MyOverflow);
                Assert.Equal(1, obj.MyInt);
                Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());

                YamlProperty[] properties = obj.MyOverflow["MyNestedClassMissing"].EnumerateObject().ToArray();

                // Verify a couple properties
                Assert.Equal(1, properties.Where(prop => prop.Name == "MyInt16").First().Value.GetInt32());
                Assert.True(properties.Where(prop => prop.Name == "MyBooleanTrue").First().Value.GetBoolean());
            }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("Needs SimpleTestClass support.")]
#endif
        public async Task ExtensionFieldNotUsed()
        {
            string yaml = @"{""MyNestedClass"":" + SimpleTestClass.s_yaml + "}";
            ClassWithExtensionField obj = await Serializer.DeserializeWrapper<ClassWithExtensionField>(yaml);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
        public async Task ExtensionFieldRoundTrip()
        {
            ClassWithExtensionField obj;

            {
                string yaml = @"{""MyIntMissing"":2, ""MyInt"":1, ""MyNestedClassMissing"":" + SimpleTestClass.s_yaml + "}";
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionField>(yaml);
                Verify();
            }

            // Round-trip the json.
            {
                string yaml = await Serializer.SerializeWrapper(obj);
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionField>(yaml);
                Verify();

                // The json should not contain the dictionary name.
                Assert.DoesNotContain(nameof(ClassWithExtensionField.MyOverflow), yaml);
            }

            void Verify()
            {
                Assert.NotNull(obj.MyOverflow);
                Assert.Equal(1, obj.MyInt);
                Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());

                YamlProperty[] properties = obj.MyOverflow["MyNestedClassMissing"].EnumerateObject().ToArray();

                // Verify a couple properties
                Assert.Equal(1, properties.Where(prop => prop.Name == "MyInt16").First().Value.GetInt32());
                Assert.True(properties.Where(prop => prop.Name == "MyBooleanTrue").First().Value.GetBoolean());
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyIgnoredWhenWritingDefault()
        {
            string expected = "{}";
            string actual = await Serializer.SerializeWrapper(new ClassWithExtensionPropertyAsObject());
            Assert.Equal(expected, actual);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task MultipleExtensionPropertyIgnoredWhenWritingDefault()
        {
            var obj = new ClassWithMultipleDictionaries();
            string actual = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"ActualDictionary\":null}", actual);

            obj = new ClassWithMultipleDictionaries
            {
                ActualDictionary = new Dictionary<string, object>()
            };
            actual = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"ActualDictionary\":{}}", actual);

            obj = new ClassWithMultipleDictionaries
            {
                MyOverflow = new Dictionary<string, object>
                {
                    { "test", "value" }
                }
            };
            actual = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"ActualDictionary\":null,\"test\":\"value\"}", actual);

            obj = new ClassWithMultipleDictionaries
            {
                ActualDictionary = new Dictionary<string, object>(),
                MyOverflow = new Dictionary<string, object>
                {
                    { "test", "value" }
                }
            };
            actual = await Serializer.SerializeWrapper(obj);
            Assert.Equal("{\"ActualDictionary\":{},\"test\":\"value\"}", actual);
        }

        [Fact]
        public async Task ExtensionPropertyAlreadyInstantiated()
        {
            Assert.NotNull(new ClassWithExtensionPropertyAlreadyInstantiated().MyOverflow);

            string yaml = """{"MyIntMissing":2}""";

            ClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yaml);
            Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());
        }

        [Fact]
        public async Task ExtensionPropertyAsObject()
        {
            string yaml = """{"MyIntMissing":2}""";

            ClassWithExtensionPropertyAsObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);
            Assert.IsType<YamlElement>(obj.MyOverflow["MyIntMissing"]);
            Assert.Equal(2, ((YamlElement)obj.MyOverflow["MyIntMissing"]).GetInt32());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyCamelCasing()
        {
            // Currently we apply no naming policy. If we do (such as a ExtensionPropertyNamingPolicy), we'd also have to add functionality to the YamlDocument.

            ClassWithExtensionProperty obj;
            const string yamlWithProperty = """{"MyIntMissing":1}""";
            const string yamlWithPropertyCamelCased = """{"myIntMissing":1}""";

            {
                // Baseline Pascal-cased json + no casing option.
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yamlWithProperty);
                Assert.Equal(1, obj.MyOverflow["MyIntMissing"].GetInt32());
                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Contains("""
                    "MyIntMissing":1
                    """, yaml);
            }

            {
                // Pascal-cased json + camel casing option.
                YamlSerializerOptions options = new YamlSerializerOptions();
                options.DictionaryKeyPolicy = YamlNamingPolicy.CamelCase;
                options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;

                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yamlWithProperty, options);
                Assert.Equal(1, obj.MyOverflow["MyIntMissing"].GetInt32());
                string yaml = await Serializer.SerializeWrapper(obj, options);
                Assert.Contains("""
                    "MyIntMissing":1
                    """, yaml);
            }

            {
                // Baseline camel-cased json + no casing option.
                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yamlWithPropertyCamelCased);
                Assert.Equal(1, obj.MyOverflow["myIntMissing"].GetInt32());
                string yaml = await Serializer.SerializeWrapper(obj);
                Assert.Contains("""
                    "myIntMissing":1
                    """, yaml);
            }

            {
                // Baseline camel-cased json + camel casing option.
                YamlSerializerOptions options = new YamlSerializerOptions();
                options.DictionaryKeyPolicy = YamlNamingPolicy.CamelCase;
                options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;

                obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yamlWithPropertyCamelCased, options);
                Assert.Equal(1, obj.MyOverflow["myIntMissing"].GetInt32());
                string yaml = await Serializer.SerializeWrapper(obj, options);
                Assert.Contains("""
                    "myIntMissing":1
                    """, yaml);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NullValuesIgnored()
        {
            const string yaml = """{"MyNestedClass":null}""";
            const string yamlMissing = """{"MyNestedClassMissing":null}""";

            {
                // Baseline with no missing.
                ClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yaml);
                Assert.Null(obj.MyOverflow);

                string outYaml = await Serializer.SerializeWrapper(obj);
                Assert.Contains("""
                    "MyNestedClass":null
                    """, outYaml);
            }

            {
                // Baseline with missing.
                ClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<ClassWithExtensionProperty>(yamlMissing);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(YamlValueKind.Null, obj.MyOverflow["MyNestedClassMissing"].ValueKind);
            }
        }

        public class ClassWithInvalidExtensionProperty
        {
            [YamlExtensionData]
            public Dictionary<string, int> MyOverflow { get; set; }
        }

        public class ClassWithTwoExtensionProperties
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow1 { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow2 { get; set; }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("https://github.com/dotnet/runtime/issues/58945")]
#endif
        public async Task InvalidExtensionPropertyFail()
        {
            // Baseline
            await Serializer.DeserializeWrapper<ClassWithExtensionProperty>("{}");
            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>("{}");

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithInvalidExtensionProperty>("{}"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithTwoExtensionProperties>("{}"));
        }

        public class ClassWithIgnoredData
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; }

            [YamlIgnore]
            public int MyInt { get; set; }
        }

        [Fact]
        public async Task IgnoredDataShouldNotBeExtensionData()
        {
            ClassWithIgnoredData obj = await Serializer.DeserializeWrapper<ClassWithIgnoredData>("""{"MyInt":1}""");

            Assert.Equal(0, obj.MyInt);
            Assert.Null(obj.MyOverflow);
        }

        public class ClassWithExtensionData<T>
        {
            [YamlExtensionData]
            public T Overflow { get; set; }
        }

        public class CustomOverflowDictionary<T> : Dictionary<string, T>
        {
        }

        public class DictionaryOverflowConverter : YamlConverter<Dictionary<string, object>>
        {
            public override Dictionary<string, object> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<string, object> value, YamlSerializerOptions options)
            {
                writer.WriteString("MyCustomOverflowWrite", "OverflowValueWrite");
            }
        }

        public class YamlElementOverflowConverter : YamlConverter<Dictionary<string, YamlElement>>
        {
            public override Dictionary<string, YamlElement> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, Dictionary<string, YamlElement> value, YamlSerializerOptions options)
            {
                writer.WriteString("MyCustomOverflowWrite", "OverflowValueWrite");
            }
        }

        public class YamlObjectOverflowConverter : YamlConverter<YamlObject>
        {
            public override YamlObject Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, YamlObject value, YamlSerializerOptions options)
            {
                writer.WriteString("MyCustomOverflowWrite", "OverflowValueWrite");
            }
        }

        public class CustomObjectDictionaryOverflowConverter : YamlConverter<CustomOverflowDictionary<object>>
        {
            public override CustomOverflowDictionary<object> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, CustomOverflowDictionary<object> value, YamlSerializerOptions options)
            {
                writer.WriteString("MyCustomOverflowWrite", "OverflowValueWrite");
            }
        }

        public class CustomYamlElementDictionaryOverflowConverter : YamlConverter<CustomOverflowDictionary<YamlElement>>
        {
            public override CustomOverflowDictionary<YamlElement> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, CustomOverflowDictionary<YamlElement> value, YamlSerializerOptions options)
            {
                writer.WriteString("MyCustomOverflowWrite", "OverflowValueWrite");
            }
        }

        [Theory]
        [MemberData(nameof(GetCustomOverflowConverters))]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionProperty_SupportsWritingToCustomSerializerWithOptions<TDictionary>(YamlConverter<TDictionary> dictionaryConverter)
            where TDictionary : IDictionary, new()
        {
            var root = new ClassWithExtensionData<TDictionary>()
            {
                Overflow = new TDictionary()
            };

            var options = Serializer.CreateOptions(makeReadOnly: false);
            options.Converters.Add(dictionaryConverter);

            string yaml = await Serializer.SerializeWrapper(root, options);
            Assert.Equal("""{"MyCustomOverflowWrite":"OverflowValueWrite"}""", yaml);
        }

        public interface IClassWithOverflow
        {
            public IDictionary GetOverflow();
        }

        public class ClassWithExtensionDataWithAttributedConverter : IClassWithOverflow
        {
            [YamlExtensionData]
            [YamlConverter(typeof(DictionaryOverflowConverter))]
            public Dictionary<string, object> Overflow { get; set; }

            public IDictionary GetOverflow() => Overflow;
        }

        public class ClassWithYamlElementExtensionDataWithAttributedConverter : IClassWithOverflow
        {
            [YamlExtensionData]
            [YamlConverter(typeof(YamlElementOverflowConverter))]
            public Dictionary<string, YamlElement> Overflow { get; set; }

            public IDictionary GetOverflow() => Overflow;
        }

        public class ClassWithCustomElementExtensionDataWithAttributedConverter : IClassWithOverflow
        {
            [YamlExtensionData]
            [YamlConverter(typeof(CustomObjectDictionaryOverflowConverter))]
            public CustomOverflowDictionary<object> Overflow { get; set; }

            public IDictionary GetOverflow() => Overflow;
        }

        public class ClassWithCustomYamlElementExtensionDataWithAttributedConverter : IClassWithOverflow
        {
            [YamlExtensionData]
            [YamlConverter(typeof(CustomYamlElementDictionaryOverflowConverter))]
            public CustomOverflowDictionary<YamlElement> Overflow { get; set; }

            public IDictionary GetOverflow() => Overflow;
        }

        [Theory]
        [MemberData(nameof(GetClassesWithCustomExtensionDataOverflowConverter))]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionProperty_SupportsWritingToCustomSerializerWithExplicitConverter<ClassWithOverflow>(ClassWithOverflow obj) where ClassWithOverflow : IClassWithOverflow
        {
            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("""{"MyCustomOverflowWrite":"OverflowValueWrite"}""", yaml);
        }

        [Theory]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("https://github.com/dotnet/runtime/issues/87005")]
#endif
        [MemberData(nameof(GetCustomOverflowConverters))]
        public async Task ExtensionProperty_IgnoresCustomSerializerWithOptions<TDictionary>(YamlConverter<TDictionary> dictionaryConverter) where TDictionary : IDictionary
        {
            var options = Serializer.CreateOptions(makeReadOnly: false);
            options.Converters.Add(dictionaryConverter);

            ClassWithExtensionData<TDictionary> obj = await Serializer.DeserializeWrapper<ClassWithExtensionData<TDictionary>>("""{"TestKey":"TestValue"}""", options);
            Assert.Equal("TestValue", ((YamlElement)obj.Overflow["TestKey"]).GetString());
        }

        public static IEnumerable<object[]> GetCustomOverflowConverters()
        {
            yield return WrapArgs(new DictionaryOverflowConverter());
            yield return WrapArgs(new YamlElementOverflowConverter());
            yield return WrapArgs(new CustomObjectDictionaryOverflowConverter());
            yield return WrapArgs(new CustomYamlElementDictionaryOverflowConverter());

            static object[] WrapArgs(YamlConverter converter) => new object[] { converter };
        }

        [Fact]
        public async Task ExtensionProperty_IgnoresCustomSerializerWithOptions_YamlObject()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlObjectOverflowConverter());

            // A custom converter for YamlObject is not allowed on an extension property.
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Serializer.DeserializeWrapper<ClassWithExtensionData<YamlObject>>("""{"TestKey":"TestValue"}""", options));

            Assert.Contains("YamlObject", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetClassesWithCustomExtensionDataOverflowConverter))]
        public async Task ExtensionProperty_IgnoresCustomSerializerWithExplicitConverter<ClassWithOverflow>(ClassWithOverflow obj)
            where ClassWithOverflow : IClassWithOverflow
        {
            obj = await Serializer.DeserializeWrapper<ClassWithOverflow>("""{"TestKey":"TestValue"}""");
            Assert.Equal("TestValue", ((YamlElement)obj.GetOverflow()["TestKey"]).GetString());
        }

        public static IEnumerable<object[]> GetClassesWithCustomExtensionDataOverflowConverter()
        {
            yield return WrapArgs(new ClassWithExtensionDataWithAttributedConverter { Overflow = new() });
            yield return WrapArgs(new ClassWithYamlElementExtensionDataWithAttributedConverter { Overflow = new() });
            yield return WrapArgs(new ClassWithCustomElementExtensionDataWithAttributedConverter { Overflow = new() });
            yield return WrapArgs(new ClassWithCustomYamlElementExtensionDataWithAttributedConverter { Overflow = new() });
            static object[] WrapArgs(IClassWithOverflow classWithOverflow) => new object[] { classWithOverflow };
        }

        [Fact]
        public async Task ExtensionProperty_IgnoresCustomSerializerWithExplicitConverter_YamlObject()
        {
            ClassWithExtensionData<YamlObject> obj
                = await Serializer.DeserializeWrapper<ClassWithExtensionData<YamlObject>>("""{"TestKey":"TestValue"}""");

            Assert.Equal("TestValue", obj.Overflow["TestKey"].GetValue<string>());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyObjectValue_Empty()
        {
            ClassWithExtensionPropertyAlreadyInstantiated obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAlreadyInstantiated>("{}");
            Assert.Equal("{}", await Serializer.SerializeWrapper(obj));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyObjectValue_SameAsExtensionPropertyName()
        {
            const string yaml = """{"MyOverflow":{"Key1":"V"}}""";

            // Deserializing directly into the overflow is not supported by design.
            ClassWithExtensionPropertyAsObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);

            // The JSON is treated as normal overflow.
            Assert.NotNull(obj.MyOverflow["MyOverflow"]);
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
        }

        public class ClassWithExtensionPropertyAsObjectAndNameProperty
        {
            public string Name { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; }
        }

        public static IEnumerable<object[]> YamlSerializerOptions()
        {
            yield return new object[] { null };
            yield return new object[] { new YamlSerializerOptions() };
            yield return new object[] { new YamlSerializerOptions { UnknownTypeHandling = YamlUnknownTypeHandling.YamlElement } };
            yield return new object[] { new YamlSerializerOptions { UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode } };
        }

        [Theory]
        [MemberData(nameof(YamlSerializerOptions))]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyDuplicateNames(YamlSerializerOptions options)
        {
            options = options is null ? YamlTestSerializerOptions.LastWins : new YamlSerializerOptions(options) { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
            var obj = new ClassWithExtensionPropertyAsObjectAndNameProperty();
            obj.Name = "Name1";

            obj.MyOverflow = new Dictionary<string, object>();
            obj.MyOverflow["Name"] = "Name2";

            string yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("""{"Name":"Name1","Name":"Name2"}""", yaml);

            // The overflow value comes last in the JSON so it overwrites the original value.
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObjectAndNameProperty>(yaml, options);
            Assert.Equal("Name2", obj.Name);

            // Since there was no overflow, this should be null.
            Assert.Null(obj.MyOverflow);
        }

        [Theory]
        [InlineData("""{ "1": 0   , "1": 1    }""")]
        [InlineData("""{ "1": null, "1": null }""")]
        [InlineData("""{ "1": "a" , "1": null }""")]
        [InlineData("""{ "1": null, "1": "b"  }""")]
        public async Task ExtensionProperty_DuplicatesThrow(string payload)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicateProperties;

            Exception ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(payload, options));
            Assert.Contains("Duplicate", ex.Message);

            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(payload, YamlTestSerializerOptions.LastWins); // Assert no throw

            ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(payload, options));
            Assert.Contains("Duplicate", ex.Message);

            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(payload, YamlTestSerializerOptions.LastWins); // Assert no throw

            ex = await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(payload, options));
            Assert.Contains("Duplicate", ex.Message);

            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(payload, YamlTestSerializerOptions.LastWins); // Assert no throw
        }

        [Theory]
        [InlineData("""{ "a": 0   , "A": 1    }""")]
        [InlineData("""{ "a": null, "A": null }""")]
        [InlineData("""{ "a": "a" , "A": null }""")]
        [InlineData("""{ "a": null, "A": "b"  }""")]
        public async Task ExtensionProperty_CaseInsensitiveDuplicatesNoThrow(string payload)
        {
            YamlSerializerOptions options = YamlTestSerializerOptions.DisallowDuplicatePropertiesIgnoringCase;

            // Dictionary extension properties are always case-sensitive
            ICollection d;
            d = (await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(payload, options)).MyOverflow;
            Assert.Equal(2, d.Count);

            d = (await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(payload, options)).MyOverflow;
            Assert.Equal(2, d.Count);

            // But YamlObject abides by options
            Exception ex = await Assert.ThrowsAsync<YamlException>(() => Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(payload, options));
            Assert.Contains("Duplicate", ex.Message);
        }

        [Theory]
        [MemberData(nameof(YamlSerializerOptions))]
        public async Task Null_SystemObject(YamlSerializerOptions options)
        {
            const string yaml = """{"MissingProperty":null}""";

            {
                ClassWithExtensionPropertyAsObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml, options);

                // A null value maps to <object>, so the value is null.
                object elem = obj.MyOverflow["MissingProperty"];
                Assert.Null(elem);
            }

            {
                ClassWithExtensionPropertyAsYamlObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(yaml, options);

                YamlObject jObject = obj.MyOverflow;
                YamlNode jNode = jObject["MissingProperty"];
                // Since YamlNode is a reference type the value is null.
                Assert.Null(jNode);
            }
        }

        [Fact]
        public async Task Null_YamlElement()
        {
            const string yaml = """{"MissingProperty":null}""";

            ClassWithExtensionPropertyAsYamlElement obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(yaml);
            object elem = obj.MyOverflow["MissingProperty"];
            // Since YamlElement is a struct, it treats null as YamlValueKind.Null.
            Assert.IsType<YamlElement>(elem);
            Assert.Equal(YamlValueKind.Null, ((YamlElement)elem).ValueKind);
        }

        [Fact]
        public async Task Null_YamlObject()
        {
            const string yaml = """{"MissingProperty":null}""";

            ClassWithExtensionPropertyAsYamlObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(yaml);
            object elem = obj.MyOverflow["MissingProperty"];
            // Since YamlNode is a reference type the value is null.
            Assert.Null(elem);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ExtensionPropertyObjectValue()
        {
            // Baseline
            ClassWithExtensionPropertyAlreadyInstantiated obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAlreadyInstantiated>("{}");
            obj.MyOverflow.Add("test", new object());
            obj.MyOverflow.Add("test1", 1);

            Assert.Equal("""{"test":{},"test1":1}""", await Serializer.SerializeWrapper(obj));
        }

        public class DummyObj
        {
            public string Prop { get; set; }
        }

        public struct DummyStruct
        {
            public string Prop { get; set; }
        }

        [Theory]
        [MemberData(nameof(YamlSerializerOptions))]
        public async Task ExtensionPropertyObjectValue_RoundTrip(YamlSerializerOptions options)
        {
            // Baseline
            ClassWithExtensionPropertyAlreadyInstantiated obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAlreadyInstantiated>("{}", options);
            obj.MyOverflow.Add("test", new object());
            obj.MyOverflow.Add("test1", 1);
            obj.MyOverflow.Add("test2", "text");
            obj.MyOverflow.Add("test3", new DummyObj() { Prop = "ObjectProp" });
            obj.MyOverflow.Add("test4", new DummyStruct() { Prop = "StructProp" });
            obj.MyOverflow.Add("test5", new Dictionary<string, object>() { { "Key", "Value" }, { "Key1", "Value1" }, });

            string yaml = await Serializer.SerializeWrapper(obj);
            ClassWithExtensionPropertyAlreadyInstantiated roundTripObj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAlreadyInstantiated>(yaml, options);

            Assert.Equal(6, roundTripObj.MyOverflow.Count);

            if (options?.UnknownTypeHandling == YamlUnknownTypeHandling.YamlNode)
            {
                Assert.IsAssignableFrom<YamlNode>(roundTripObj.MyOverflow["test"]);
                Assert.IsAssignableFrom<YamlNode>(roundTripObj.MyOverflow["test1"]);
                Assert.IsAssignableFrom<YamlNode>(roundTripObj.MyOverflow["test2"]);
                Assert.IsAssignableFrom<YamlNode>(roundTripObj.MyOverflow["test3"]);

                Assert.IsType<YamlObject>(roundTripObj.MyOverflow["test"]);

                Assert.IsAssignableFrom<YamlValue>(roundTripObj.MyOverflow["test1"]);
                Assert.Equal(1, ((YamlValue)roundTripObj.MyOverflow["test1"]).GetValue<int>());
                Assert.Equal(1, ((YamlValue)roundTripObj.MyOverflow["test1"]).GetValue<long>());

                Assert.IsAssignableFrom<YamlValue>(roundTripObj.MyOverflow["test2"]);
                Assert.Equal("text", ((YamlValue)roundTripObj.MyOverflow["test2"]).GetValue<string>());

                Assert.IsType<YamlObject>(roundTripObj.MyOverflow["test3"]);
                Assert.Equal("ObjectProp", ((YamlObject)roundTripObj.MyOverflow["test3"])["Prop"].GetValue<string>());

                Assert.IsType<YamlObject>(roundTripObj.MyOverflow["test4"]);
                Assert.Equal("StructProp", ((YamlObject)roundTripObj.MyOverflow["test4"])["Prop"].GetValue<string>());

                Assert.IsType<YamlObject>(roundTripObj.MyOverflow["test5"]);
                Assert.Equal("Value", ((YamlObject)roundTripObj.MyOverflow["test5"])["Key"].GetValue<string>());
                Assert.Equal("Value1", ((YamlObject)roundTripObj.MyOverflow["test5"])["Key1"].GetValue<string>());
            }
            else
            {
                Assert.IsType<YamlElement>(roundTripObj.MyOverflow["test"]);
                Assert.IsType<YamlElement>(roundTripObj.MyOverflow["test1"]);
                Assert.IsType<YamlElement>(roundTripObj.MyOverflow["test2"]);
                Assert.IsType<YamlElement>(roundTripObj.MyOverflow["test3"]);

                Assert.Equal(YamlValueKind.Object, ((YamlElement)roundTripObj.MyOverflow["test"]).ValueKind);

                Assert.Equal(YamlValueKind.Number, ((YamlElement)roundTripObj.MyOverflow["test1"]).ValueKind);
                Assert.Equal(1, ((YamlElement)roundTripObj.MyOverflow["test1"]).GetInt32());
                Assert.Equal(1, ((YamlElement)roundTripObj.MyOverflow["test1"]).GetInt64());

                Assert.Equal(YamlValueKind.String, ((YamlElement)roundTripObj.MyOverflow["test2"]).ValueKind);
                Assert.Equal("text", ((YamlElement)roundTripObj.MyOverflow["test2"]).GetString());

                Assert.Equal(YamlValueKind.Object, ((YamlElement)roundTripObj.MyOverflow["test3"]).ValueKind);
                Assert.Equal("ObjectProp", ((YamlElement)roundTripObj.MyOverflow["test3"]).GetProperty("Prop").GetString());

                Assert.Equal(YamlValueKind.Object, ((YamlElement)roundTripObj.MyOverflow["test4"]).ValueKind);
                Assert.Equal("StructProp", ((YamlElement)roundTripObj.MyOverflow["test4"]).GetProperty("Prop").GetString());

                Assert.Equal(YamlValueKind.Object, ((YamlElement)roundTripObj.MyOverflow["test5"]).ValueKind);
                Assert.Equal("Value", ((YamlElement)roundTripObj.MyOverflow["test5"]).GetProperty("Key").GetString());
                Assert.Equal("Value1", ((YamlElement)roundTripObj.MyOverflow["test5"]).GetProperty("Key1").GetString());
            }
        }

        [Fact]
        public async Task DeserializeIntoYamlObjectProperty()
        {
            string yaml = """{"MyDict":{"Property1":1}}""";
            ClassWithExtensionPropertyAsYamlObject obj =
                await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(yaml);

            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, obj.MyOverflow["MyDict"]["Property1"].GetValue<int>());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeYamlObjectExtensionData_ProducesValidYaml()
        {
            // YamlSerializer.Serialize was producing invalid JSON for [YamlExtensionData] property of type YamlObject
            // Output was: {"Id":1,{"nested":true}} instead of {"Id":1,"nested":true}

            var obj = new ClassWithYamlObjectExtensionDataAndProperty
            {
                Id = 1,
                Extra = new YamlObject { ["nested"] = true }
            };

            string yaml = await Serializer.SerializeWrapper(obj);

            // Verify the JSON is valid by parsing it
            using YamlDocument doc = YamlDocument.Parse(yaml);

            // Verify the structure is correct: extension data should be flattened, not nested
            Assert.True(doc.RootElement.TryGetProperty("Id", out YamlElement idElement));
            Assert.Equal(1, idElement.GetInt32());

            Assert.True(doc.RootElement.TryGetProperty("nested", out YamlElement nestedElement));
            Assert.True(nestedElement.GetBoolean());

            // Extension data property name should NOT appear in the output
            Assert.False(doc.RootElement.TryGetProperty("Extra", out _));

            // Verify expected JSON format
            Assert.Equal("""{"Id":1,"nested":true}""", yaml);
        }

        [Fact]
        public async Task SerializeYamlObjectExtensionData_RoundTrip()
        {
            var original = new ClassWithYamlObjectExtensionDataAndProperty
            {
                Id = 42,
                Extra = new YamlObject
                {
                    ["string"] = "value",
                    ["number"] = 123,
                    ["boolean"] = false,
                    ["nested"] = new YamlObject { ["inner"] = "data" }
                }
            };

            string yaml = await Serializer.SerializeWrapper(original);

            // Verify round-trip
            var deserialized = await Serializer.DeserializeWrapper<ClassWithYamlObjectExtensionDataAndProperty>(yaml);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.NotNull(deserialized.Extra);
            Assert.Equal(4, deserialized.Extra.Count);
            Assert.Equal("value", deserialized.Extra["string"]!.GetValue<string>());
            Assert.Equal(123, deserialized.Extra["number"]!.GetValue<int>());
            Assert.False(deserialized.Extra["boolean"]!.GetValue<bool>());
            Assert.Equal("data", deserialized.Extra["nested"]!["inner"]!.GetValue<string>());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeYamlObjectExtensionData_EmptyYamlObject()
        {
            var obj = new ClassWithYamlObjectExtensionDataAndProperty
            {
                Id = 1,
                Extra = new YamlObject()
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("""{"Id":1}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeYamlObjectExtensionData_NullYamlObject()
        {
            var obj = new ClassWithYamlObjectExtensionDataAndProperty
            {
                Id = 1,
                Extra = null
            };

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal("""{"Id":1}""", yaml);
        }

        public class ClassWithYamlObjectExtensionDataAndProperty
        {
            public int Id { get; set; }

            [YamlExtensionData]
            public YamlObject? Extra { get; set; }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("https://github.com/dotnet/runtime/issues/58945")]
#endif

        public async Task DeserializeIntoSystemObjectProperty()
        {
            string yaml = """{"MyDict":{"Property1":1}}""";

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsSystemObject>(yaml));

            // Cannot deserialize into System.Object overflow even if UnknownTypeHandling is set to use YamlNode.
            var options = new YamlSerializerOptions { UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode };
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsSystemObject>(yaml));
        }

        public class ClassWithReference
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement> MyOverflow { get; set; }

            public ClassWithExtensionProperty MyReference { get; set; }
        }

        [Theory]
        [InlineData("""{"MyIntMissing":2,"MyReference":{"MyIntMissingChild":3}}""")]
        [InlineData("""{"MyReference":{"MyIntMissingChild":3},"MyIntMissing":2}""")]
        [InlineData("""{"MyReference":{"MyNestedClass":null,"MyInt":0,"MyIntMissingChild":3},"MyIntMissing":2}""")]
        public async Task NestedClass(string yaml)
        {
            ClassWithReference obj;

            void Verify()
            {
                Assert.IsType<YamlElement>(obj.MyOverflow["MyIntMissing"]);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());

                ClassWithExtensionProperty child = obj.MyReference;

                Assert.IsType<YamlElement>(child.MyOverflow["MyIntMissingChild"]);
                Assert.IsType<YamlElement>(child.MyOverflow["MyIntMissingChild"]);
                Assert.Equal(1, child.MyOverflow.Count);
                Assert.Equal(3, child.MyOverflow["MyIntMissingChild"].GetInt32());
                Assert.Null(child.MyNestedClass);
                Assert.Equal(0, child.MyInt);
            }

            obj = await Serializer.DeserializeWrapper<ClassWithReference>(yaml);
            Verify();

            // Round-trip the json and verify.
            yaml = await Serializer.SerializeWrapper(obj);
            obj = await Serializer.DeserializeWrapper<ClassWithReference>(yaml);
            Verify();
        }

        public class ParentClassWithObject
        {
            public string Text { get; set; }
            public ChildClassWithObject Child { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> ExtensionData { get; set; } = new Dictionary<string, object>();
        }

        public class ChildClassWithObject
        {
            public int Number { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> ExtensionData { get; set; } = new Dictionary<string, object>();
        }

        [Fact]
        public async Task NestedClassWithObjectExtensionDataProperty()
        {
            var child = new ChildClassWithObject { Number = 2 };
            child.ExtensionData.Add("SpecialInformation", "I am child class");

            var parent = new ParentClassWithObject { Text = "Hello World" };
            parent.ExtensionData.Add("SpecialInformation", "I am parent class");
            parent.Child = child;

            // The extension data is based on the raw strings added above and not YamlElement.
            Assert.Equal("Hello World", parent.Text);
            Assert.IsType<string>(parent.ExtensionData["SpecialInformation"]);
            Assert.Equal("I am parent class", (string)parent.ExtensionData["SpecialInformation"]);
            Assert.Equal(2, parent.Child.Number);
            Assert.IsType<string>(parent.Child.ExtensionData["SpecialInformation"]);
            Assert.Equal("I am child class", (string)parent.Child.ExtensionData["SpecialInformation"]);

            // Round-trip and verify. Extension data is now based on YamlElement.
            string yaml = await Serializer.SerializeWrapper(parent);
            parent = await Serializer.DeserializeWrapper<ParentClassWithObject>(yaml);

            Assert.Equal("Hello World", parent.Text);
            Assert.IsType<YamlElement>(parent.ExtensionData["SpecialInformation"]);
            Assert.Equal("I am parent class", ((YamlElement)parent.ExtensionData["SpecialInformation"]).GetString());
            Assert.Equal(2, parent.Child.Number);
            Assert.IsType<YamlElement>(parent.Child.ExtensionData["SpecialInformation"]);
            Assert.Equal("I am child class", ((YamlElement)parent.Child.ExtensionData["SpecialInformation"]).GetString());
        }

        public class ParentClassWithYamlElement
        {
            public string Text { get; set; }

            public List<ChildClassWithYamlElement> Children { get; set; } = new List<ChildClassWithYamlElement>();

            [YamlExtensionData]
            // Use SortedDictionary as verification of supporting derived dictionaries.
            public SortedDictionary<string, YamlElement> ExtensionData { get; set; } = new SortedDictionary<string, YamlElement>();
        }

        public class ChildClassWithYamlElement
        {
            public int Number { get; set; }

            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { get; set; } = new Dictionary<string, YamlElement>();
        }

        [Fact]
        public async Task NestedClassWithYamlElementExtensionDataProperty()
        {
            var child = new ChildClassWithYamlElement { Number = 4 };
            child.ExtensionData.Add("SpecialInformation", YamlElement.Parse(await Serializer.SerializeWrapper("I am child class")));

            var parent = new ParentClassWithYamlElement { Text = "Hello World" };
            parent.ExtensionData.Add("SpecialInformation", YamlElement.Parse(await Serializer.SerializeWrapper("I am parent class")));
            parent.Children.Add(child);

            Verify();

            // Round-trip and verify.
            string yaml = await Serializer.SerializeWrapper(parent);
            parent = await Serializer.DeserializeWrapper<ParentClassWithYamlElement>(yaml);
            Verify();

            void Verify()
            {
                Assert.Equal("Hello World", parent.Text);
                Assert.Equal("I am parent class", parent.ExtensionData["SpecialInformation"].GetString());
                Assert.Equal(1, parent.Children.Count);
                Assert.Equal(4, parent.Children[0].Number);
                Assert.Equal("I am child class", parent.Children[0].ExtensionData["SpecialInformation"].GetString());
            }
        }

        [Fact]
        public async Task DeserializeIntoObjectProperty()
        {
            ClassWithExtensionPropertyAsObject obj;
            string yaml;

            // Baseline dictionary.
            yaml = """{"MyDict":{"Property1":1}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, ((YamlElement)obj.MyOverflow["MyDict"]).EnumerateObject().First().Value.GetInt32());

            // Attempt to deserialize directly into the overflow property; this is just added as a normal missing property like MyDict above.
            yaml = """{"MyOverflow":{"Property1":1}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, ((YamlElement)obj.MyOverflow["MyOverflow"]).EnumerateObject().First().Value.GetInt32());

            // Attempt to deserialize null into the overflow property. This is also treated as a missing property.
            yaml = """{"MyOverflow":null}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Null(obj.MyOverflow["MyOverflow"]);

            // Attempt to deserialize object into the overflow property. This is also treated as a missing property.
            yaml = """{"MyOverflow":{}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(YamlValueKind.Object, ((YamlElement)obj.MyOverflow["MyOverflow"]).ValueKind);
        }

        [Fact]
        public async Task DeserializeIntoMultipleDictionaries()
        {
            ClassWithMultipleDictionaries obj;
            string yaml;

            // Baseline dictionary.
            yaml = """{"ActualDictionary":{"Key": {"Property0":-1}},"MyDict":{"Property1":1}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithMultipleDictionaries>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, ((YamlElement)obj.MyOverflow["MyDict"]).EnumerateObject().First().Value.GetInt32());
            Assert.Equal(1, obj.ActualDictionary.Count);
            Assert.Equal(-1, ((YamlElement)obj.ActualDictionary["Key"]).EnumerateObject().First().Value.GetInt32());

            // Attempt to deserialize null into the dictionary and overflow property. This is also treated as a missing property.
            yaml = """{"ActualDictionary":null,"MyOverflow":null}""";
            obj = await Serializer.DeserializeWrapper<ClassWithMultipleDictionaries>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Null(obj.MyOverflow["MyOverflow"]);
            Assert.Null(obj.ActualDictionary);

            // Attempt to deserialize object into the dictionary and overflow property. This is also treated as a missing property.
            yaml = """{"ActualDictionary":{},"MyOverflow":{}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithMultipleDictionaries>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(YamlValueKind.Object, ((YamlElement)obj.MyOverflow["MyOverflow"]).ValueKind);
            Assert.Equal(0, obj.ActualDictionary.Count);
        }

        [Fact]
        public async Task DeserializeIntoYamlElementProperty()
        {
            ClassWithExtensionPropertyAsYamlElement obj;
            string yaml;

            // Baseline dictionary.
            yaml = """{"MyDict":{"Property1":1}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, obj.MyOverflow["MyDict"].EnumerateObject().First().Value.GetInt32());

            // Attempt to deserialize directly into the overflow property; this is just added as a normal missing property like MyDict above.
            yaml = """{"MyOverflow":{"Property1":1}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(1, obj.MyOverflow["MyOverflow"].EnumerateObject().First().Value.GetInt32());

            // Attempt to deserialize null into the overflow property. This is also treated as a missing property.
            yaml = """{"MyOverflow":null}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(YamlValueKind.Null, obj.MyOverflow["MyOverflow"].ValueKind);

            // Attempt to deserialize object into the overflow property. This is also treated as a missing property.
            yaml = """{"MyOverflow":{}}""";
            obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(yaml);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(YamlValueKind.Object, obj.MyOverflow["MyOverflow"].ValueKind);
        }

        [Fact]
        public async Task SerializerOutputRoundtripsWhenEscaping()
        {
            string yamlString = "{\"\u6C49\u5B57\":\"abc\",\"Class\":{\"\u6F22\u5B57\":\"xyz\"},\"\u62DC\u6258\":{\"\u62DC\u6258\u62DC\u6258\":1}}";

            ClassWithEscapedProperty input = await Serializer.DeserializeWrapper<ClassWithEscapedProperty>(yamlString);

            Assert.Equal("abc", input.\u6C49\u5B57);
            Assert.Equal("xyz", input.Class.\u6F22\u5B57);

            string normalizedString = await Serializer.SerializeWrapper(input);

            Assert.Equal(normalizedString, await Serializer.SerializeWrapper(await Serializer.DeserializeWrapper<ClassWithEscapedProperty>(normalizedString)));
        }

        public class ClassWithEscapedProperty
        {
            public string \u6C49\u5B57 { get; set; }
            public NestedClassWithEscapedProperty Class { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> Overflow { get; set; }
        }

        public class NestedClassWithEscapedProperty
        {
            public string \u6F22\u5B57 { get; set; }
        }

        public class ClassWithInvalidExtensionPropertyStringString
        {
            [YamlExtensionData]
            public Dictionary<string, string> MyOverflow { get; set; }
        }

        public class ClassWithInvalidExtensionPropertyObjectString
        {
            [YamlExtensionData]
            public Dictionary<DummyObj, string> MyOverflow { get; set; }
        }

        public class ClassWithInvalidExtensionPropertyStringYamlNode
        {
            [YamlExtensionData]
            public Dictionary<string, YamlNode> MyOverflow { get; set; }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("https://github.com/dotnet/runtime/issues/58945")]
#endif
        public async Task ExtensionProperty_InvalidDictionary()
        {
            var obj1 = new ClassWithInvalidExtensionPropertyStringString();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(obj1));

            var obj2 = new ClassWithInvalidExtensionPropertyObjectString();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(obj2));

            var obj3 = new ClassWithInvalidExtensionPropertyStringYamlNode();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(obj3));
        }

        public class ClassWithExtensionPropertyAlreadyInstantiated
        {
            public ClassWithExtensionPropertyAlreadyInstantiated()
            {
                MyOverflow = new Dictionary<string, object>();
            }

            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyAsObject
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyAsYamlElement
        {
            [YamlExtensionData]
            public Dictionary<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyAsYamlObject
        {
            [YamlExtensionData]
            public YamlObject MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyAsSystemObject
        {
            [YamlExtensionData]
            public object MyOverflow { get; set; }
        }

        public class ClassWithMultipleDictionaries
        {
            [YamlExtensionData]
            public Dictionary<string, object> MyOverflow { get; set; }

            public Dictionary<string, object>? ActualDictionary { get; set; }
        }

        [Fact]
#if BUILDING_SOURCE_GENERATOR_TESTS
        [ActiveIssue("https://github.com/dotnet/runtime/issues/58945")]
#endif
        public async Task DeserializeIntoImmutableDictionaryProperty()
        {
            // baseline
            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsImmutable>("{}");
            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsImmutableYamlElement>("{}");
            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyPrivateConstructor>("{}");
            await Serializer.DeserializeWrapper<ClassWithExtensionPropertyPrivateConstructorYamlElement>("{}");

            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsImmutable>("{\"hello\":\"world\"}"));
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsImmutableYamlElement>("{\"hello\":\"world\"}"));
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyPrivateConstructor>("{\"hello\":\"world\"}"));
            await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyPrivateConstructorYamlElement>("{\"hello\":\"world\"}"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyCustomIImmutable>("{\"hello\":\"world\"}"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithExtensionPropertyCustomIImmutableYamlElement>("{\"hello\":\"world\"}"));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializeIntoImmutableDictionaryProperty()
        {
            // attempt to serialize a null immutable dictionary
            string expectedYaml = "{}";
            var obj = new ClassWithExtensionPropertyAsImmutable();
            var yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(expectedYaml, yaml);

            // attempt to serialize an empty immutable dictionary
            expectedYaml = "{}";
            obj = new ClassWithExtensionPropertyAsImmutable();
            obj.MyOverflow = ImmutableDictionary<string, object>.Empty;
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(expectedYaml, yaml);

            // attempt to serialize a populated immutable dictionary
            expectedYaml = "{\"hello\":\"world\"}";
            obj = new ClassWithExtensionPropertyAsImmutable();
            var dictionaryStringObject = new Dictionary<string, object> { { "hello", "world" } };
            obj.MyOverflow = ImmutableDictionary.CreateRange(dictionaryStringObject);
            yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(expectedYaml, yaml);
        }

        public class ClassWithExtensionPropertyAsImmutable
        {
            [YamlExtensionData]
            public ImmutableDictionary<string, object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyAsImmutableYamlElement
        {
            [YamlExtensionData]
            public ImmutableDictionary<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyPrivateConstructor
        {
            [YamlExtensionData]
            public GenericIDictionaryWrapperPrivateConstructor<string, object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyPrivateConstructorYamlElement
        {
            [YamlExtensionData]
            public GenericIDictionaryWrapperPrivateConstructor<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyCustomIImmutable
        {
            [YamlExtensionData]
            public GenericIImmutableDictionaryWrapper<string, object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyCustomIImmutableYamlElement
        {
            [YamlExtensionData]
            public GenericIImmutableDictionaryWrapper<string, YamlElement> MyOverflow { get; set; }
        }

        [Theory]
        [InlineData(typeof(ClassWithExtensionPropertyNoGenericParameters))]
        [InlineData(typeof(ClassWithExtensionPropertyOneGenericParameter))]
        [InlineData(typeof(ClassWithExtensionPropertyThreeGenericParameters))]
        public async Task DeserializeIntoGenericDictionaryParameterCount([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            object obj = await Serializer.DeserializeWrapper("{\"hello\":\"world\"}", type);

            IDictionary<string, object> extData = (IDictionary<string, object>)type.GetProperty("MyOverflow").GetValue(obj)!;
            Assert.Equal("world", ((YamlElement)extData["hello"]).GetString());
        }

        public class ClassWithExtensionPropertyNoGenericParameters
        {
            [YamlExtensionData]
            public StringToObjectIDictionaryWrapper MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyOneGenericParameter
        {
            [YamlExtensionData]
            public StringToGenericIDictionaryWrapper<object> MyOverflow { get; set; }
        }

        public class ClassWithExtensionPropertyThreeGenericParameters
        {
            [YamlExtensionData]
            public GenericIDictionaryWrapperThreeGenericParameters<string, object, string> MyOverflow { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomObjectConverterInExtensionProperty()
        {
            const string Yaml = "{\"hello\": \"world\"}";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new ObjectConverter());

            ClassWithExtensionPropertyAsObject obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsObject>(Yaml, options);
            object overflowProp = obj.MyOverflow["hello"];
            Assert.IsType<string>(overflowProp);
            Assert.Equal("world!!!", ((string)overflowProp));

            string newYaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"hello\":\"world!!!\"}", newYaml);
        }

        public class ObjectConverter : YamlConverter<object>
        {
            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return reader.GetString() + "!!!";
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                // Since we are in a user-provided (not internal to S.T.Yaml) object converter,
                // this converter will be called, not the internal string converter.
                writer.WriteStringValue((string)value);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomYamlElementConverterInExtensionProperty()
        {
            const string Yaml = "{\"hello\": \"world\"}";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlElementConverter());

            ClassWithExtensionPropertyAsYamlElement obj = await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlElement>(Yaml, options);
            YamlElement overflowProp = obj.MyOverflow["hello"];
            Assert.Equal(YamlValueKind.Undefined, overflowProp.ValueKind);

            string newYaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"hello\":{\"Hi\":\"There\"}}", newYaml);
        }

        public class YamlElementConverter : YamlConverter<YamlElement>
        {
            public override YamlElement Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                // Just return an empty YamlElement.
                reader.Skip();
                return new YamlElement();
            }

            public override void Write(Utf8YamlWriter writer, YamlElement value, YamlSerializerOptions options)
            {
                // Write a string we can test against easily.
                writer.WriteStartObject();
                writer.WriteString("Hi", "There");
                writer.WriteEndObject();
            }
        }

        [Fact]
        public async Task CustomYamlObjectConverterInExtensionProperty()
        {
            const string Yaml = "{\"hello\": \"world\"}";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new YamlObjectConverter());

            // A custom converter for YamlObject is not allowed on an extension property.
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Serializer.DeserializeWrapper<ClassWithExtensionPropertyAsYamlObject>(Yaml, options));

            Assert.Contains("YamlObject", ex.Message);
        }

        public class YamlObjectConverter : YamlConverter<YamlObject>
        {
            public override YamlObject Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                // Just return an empty YamlElement.
                reader.Skip();
                return new YamlObject();
            }

            public override void Write(Utf8YamlWriter writer, YamlObject value, YamlSerializerOptions options)
            {
                // Write a string we can test against easily.
                writer.WriteStartObject();
                writer.WriteString("Hi", "There");
                writer.WriteEndObject();
            }
        }

        [Fact]
        public async Task EmptyPropertyAndExtensionData_PropertyFirst()
        {
            // Verify any caching treats real property (with empty name) differently than a missing property.

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            YamlSerializerOptions options = new YamlSerializerOptions();

            // First use an empty property.
            string yaml = """{"":43}""";
            obj = await Serializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(yaml, options);
            Assert.Equal(43, obj.MyInt1);
            Assert.Null(obj.MyOverflow);

            // Then populate cache with a missing property name.
            yaml = """{"DoesNotExist":42}""";
            obj = await Serializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(yaml, options);
            Assert.Equal(0, obj.MyInt1);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(42, obj.MyOverflow["DoesNotExist"].GetInt32());
        }

        [Fact]
        public async Task EmptyPropertyNameAndExtensionData_ExtDataFirst()
        {
            // Verify any caching treats real property (with empty name) differently than a missing property.

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            YamlSerializerOptions options = new YamlSerializerOptions();

            // First populate cache with a missing property name.
            string yaml = """{"DoesNotExist":42}""";
            obj = await Serializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(yaml, options);
            Assert.Equal(0, obj.MyInt1);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(42, obj.MyOverflow["DoesNotExist"].GetInt32());

            // Then use an empty property.
            yaml = """{"":43}""";
            obj = await Serializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(yaml, options);
            Assert.Equal(43, obj.MyInt1);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
        public async Task ExtensionDataDictionarySerialize_DoesNotHonor()
        {
            var options = new YamlSerializerOptions
            {
                PropertyNamingPolicy = YamlNamingPolicy.CamelCase
            };

            EmptyClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<EmptyClassWithExtensionProperty>("""{"Key1": 1}""", options);

            // Ignore naming policy for extension data properties by default.
            Assert.False(obj.MyOverflow.ContainsKey("key1"));
            Assert.Equal(1, obj.MyOverflow["Key1"].GetInt32());
        }

        [Theory]
        [InlineData(0x1, 'v')]
        [InlineData(0x1, '\u0467')]
        [InlineData(0x10, 'v')]
        [InlineData(0x10, '\u0467')]
        [InlineData(0x100, 'v')]
        [InlineData(0x100, '\u0467')]
        [InlineData(0x1000, 'v')]
        [InlineData(0x1000, '\u0467')]
        [InlineData(0x10000, 'v')]
        [InlineData(0x10000, '\u0467')]
        [Trait("category", "jsonoutput")]
        public async Task LongPropertyNames(int propertyLength, char ch)
        {
            // Although the CLR may limit member length to 1023 bytes, the serializer doesn't have a hard limit.

            string val = new string(ch, propertyLength);
            string yaml = @"{""" + val + @""":1}";

            EmptyClassWithExtensionProperty obj = await Serializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(yaml);

            Assert.True(obj.MyOverflow.ContainsKey(val));

            var options = new YamlSerializerOptions
            {
                // Avoid escaping '\u0467'.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string yamlRoundTripped = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal(yaml, yamlRoundTripped);
        }

        public class EmptyClassWithExtensionProperty
        {
            [YamlExtensionData]
            public IDictionary<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithEmptyPropertyNameAndExtensionProperty
        {
            [YamlPropertyName("")]
            public int MyInt1 { get; set; }

            [YamlExtensionData]
            public IDictionary<string, YamlElement> MyOverflow { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IReadOnlyDictionary_ObjectExtensionPropertyRoundTrip()
        {
            string yaml = """{"MyIntMissing":2, "MyInt":1}""";
            ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty obj = await Serializer.DeserializeWrapper<ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty>(yaml);
            
            Assert.NotNull(obj.MyOverflow);
            Assert.Equal(1, obj.MyInt);
            Assert.IsType<YamlElement>(obj.MyOverflow["MyIntMissing"]);
            Assert.Equal(2, ((YamlElement)obj.MyOverflow["MyIntMissing"]).GetInt32());

            string yamlSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Contains("\"MyIntMissing\"", yamlSerialized);
            Assert.Contains("\"MyInt\"", yamlSerialized);
            Assert.DoesNotContain(nameof(ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty.MyOverflow), yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IReadOnlyDictionary_YamlElementExtensionPropertyRoundTrip()
        {
            string yaml = """{"MyIntMissing":2, "MyInt":1}""";
            ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty obj = await Serializer.DeserializeWrapper<ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty>(yaml);
            
            Assert.NotNull(obj.MyOverflow);
            Assert.Equal(1, obj.MyInt);
            Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());

            string yamlSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Contains("\"MyIntMissing\"", yamlSerialized);
            Assert.Contains("\"MyInt\"", yamlSerialized);
            Assert.DoesNotContain(nameof(ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty.MyOverflow), yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task IReadOnlyDictionary_ExtensionPropertyIgnoredWhenWritingDefault()
        {
            string expected = "{}";
            string actual = await Serializer.SerializeWrapper(new ClassWithIReadOnlyDictionaryExtensionPropertyAsObject());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IReadOnlyDictionary_PrePopulated_SeedsNewInstance()
        {
            string yaml = """{"MyIntMissing":2, "KeyToOverwrite":"NewValue", "MyInt":1}""";
            var obj = await Serializer.DeserializeWrapper<ClassWithIReadOnlyDictionaryAlreadyInstantiated>(yaml);

            Assert.NotNull(obj.MyOverflow);
            Assert.Equal(1, obj.MyInt);

            // Should have the existing key from the initializer
            Assert.True(obj.MyOverflow.ContainsKey("ExistingKey"));
            Assert.Equal("ExistingValue", ((YamlElement)obj.MyOverflow["ExistingKey"]).GetString());

            // Should have the new key from deserialization
            Assert.True(obj.MyOverflow.ContainsKey("MyIntMissing"));
            Assert.Equal(2, ((YamlElement)obj.MyOverflow["MyIntMissing"]).GetInt32());

            // Existing key should be overwritten with new value from deserialization
            Assert.True(obj.MyOverflow.ContainsKey("KeyToOverwrite"));
            Assert.Equal("NewValue", ((YamlElement)obj.MyOverflow["KeyToOverwrite"]).GetString());
        }

        [Fact]
        public async Task IReadOnlyDictionary_PrePopulated_YamlElement_SeedsNewInstance()
        {
            string yaml = """{"MyIntMissing":2, "KeyToOverwrite":"NewValue", "MyInt":1}""";
            var obj = await Serializer.DeserializeWrapper<ClassWithIReadOnlyDictionaryYamlElementAlreadyInstantiated>(yaml);

            Assert.NotNull(obj.MyOverflow);
            Assert.Equal(1, obj.MyInt);

            // Should have the existing key from the initializer
            Assert.True(obj.MyOverflow.ContainsKey("ExistingKey"));
            Assert.Equal("ExistingValue", obj.MyOverflow["ExistingKey"].GetString());

            // Should have the new key from deserialization
            Assert.True(obj.MyOverflow.ContainsKey("MyIntMissing"));
            Assert.Equal(2, obj.MyOverflow["MyIntMissing"].GetInt32());

            // Existing key should be overwritten with new value from deserialization
            Assert.True(obj.MyOverflow.ContainsKey("KeyToOverwrite"));
            Assert.Equal("NewValue", obj.MyOverflow["KeyToOverwrite"].GetString());
        }

        public class ClassWithIReadOnlyDictionaryExtensionPropertyAsObject
        {
            [YamlExtensionData]
            public IReadOnlyDictionary<string, object> MyOverflow { get; set; }
        }

        public class ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElement
        {
            [YamlExtensionData]
            public IReadOnlyDictionary<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty
        {
            public int MyInt { get; set; }

            [YamlExtensionData]
            public IReadOnlyDictionary<string, object> MyOverflow { get; set; }
        }

        public class ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty
        {
            public int MyInt { get; set; }

            [YamlExtensionData]
            public IReadOnlyDictionary<string, YamlElement> MyOverflow { get; set; }
        }

        public class ClassWithIReadOnlyDictionaryAlreadyInstantiated
        {
            public int MyInt { get; set; }

            [YamlExtensionData]
            public IReadOnlyDictionary<string, object> MyOverflow { get; set; } = new Dictionary<string, object>
            {
                ["ExistingKey"] = YamlDocument.Parse("\"ExistingValue\"").RootElement,
                ["KeyToOverwrite"] = YamlDocument.Parse("\"OldValue\"").RootElement
            };
        }

        public class ClassWithIReadOnlyDictionaryYamlElementAlreadyInstantiated
        {
            public int MyInt { get; set; }

            [YamlExtensionData]
            public IReadOnlyDictionary<string, YamlElement> MyOverflow { get; set; } = new Dictionary<string, YamlElement>
            {
                ["ExistingKey"] = YamlDocument.Parse("\"ExistingValue\"").RootElement,
                ["KeyToOverwrite"] = YamlDocument.Parse("\"OldValue\"").RootElement
            };
        }
    }
}
