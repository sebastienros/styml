// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Nodes.Tests;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Tests;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class DefaultYamlTypeInfoResolverTests
    {
        [Fact]
        public static void YamlPropertyInfoOptionsAreSet()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(MyClass), options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            typeInfo = YamlTypeInfo.CreateYamlTypeInfo<MyClass>(options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            static void CreatePropertyAndCheckOptions(YamlSerializerOptions expectedOptions, YamlTypeInfo typeInfo)
            {
                YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(typeof(string), "test");
                Assert.Same(expectedOptions, propertyInfo.Options);
            }
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(MyClass))]
        public static void YamlPropertyInfoPropertyTypeIsSetWhenUsingCreateYamlPropertyInfo(Type propertyType)
        {
            YamlSerializerOptions options = new();
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(MyClass), options);
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(propertyType, "test");

            Assert.Equal(propertyType, propertyInfo.PropertyType);
        }

        [Fact]
        public static void YamlPropertyInfoPropertyTypeIsSet()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            Assert.Equal(2, typeInfo.Properties.Count);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];
            Assert.Equal(typeof(string), propertyInfo.PropertyType);
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(MyClass))]
        public static void YamlPropertyInfoNameIsSetAndIsMutableWhenUsingCreateYamlPropertyInfo(Type propertyType)
        {
            YamlSerializerOptions options = new();
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(MyClass), options);
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(propertyType, "test");

            Assert.Equal("test", propertyInfo.Name);

            propertyInfo.Name = "foo";
            Assert.Equal("foo", propertyInfo.Name);

            Assert.Throws<ArgumentNullException>(() => propertyInfo.Name = null);
        }

        [Fact]
        public static void YamlPropertyInfoNameIsSetAndIsMutableForDefaultResolver()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            Assert.Equal(2, typeInfo.Properties.Count);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.Equal(nameof(MyClass.Value), propertyInfo.Name);

            propertyInfo.Name = "foo";
            Assert.Equal("foo", propertyInfo.Name);

            Assert.Throws<ArgumentNullException>(() => propertyInfo.Name = null);
        }

        [Fact]
        public static void YamlPropertyInfoForDefaultResolverHasNamingPoliciesRulesApplied()
        {
            YamlSerializerOptions options = new() { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            Assert.Equal(2, typeInfo.Properties.Count);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.Equal(nameof(MyClass.Value).ToLowerInvariant(), propertyInfo.Name);

            // explicitly setting does not change casing
            propertyInfo.Name = "Foo";
            Assert.Equal("Foo", propertyInfo.Name);
        }

        [Fact]
        public static void YamlPropertyInfoCustomConverterIsNullWhenUsingCreateYamlPropertyInfo()
        {
            YamlSerializerOptions options = new() { TypeInfoResolver = YamlSerializerOptions.Default.TypeInfoResolver };
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(typeof(MyClass), "test");

            Assert.Null(propertyInfo.CustomConverter);
        }

        [Fact]
        public static void YamlPropertyInfoCustomConverterIsNotNullForPropertyWithCustomConverter()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            Assert.Equal(1, typeInfo.Properties.Count);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.CustomConverter);
            Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoCustomConverterSetToNullIsRespected()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.CustomConverter);
                    Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
                    propertyInfo.CustomConverter = null;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":{"Value":"SomeValue","Thing":null}}""", yaml);

            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoCustomConverterIsRespected()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.CustomConverter);
                    Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
                    propertyInfo.CustomConverter = new MyClassCustomConverter("test_");
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"test_SomeValue"}""", yaml);

            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoCustomConverterFactoryIsNotExpanded()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            YamlConverter? expectedConverter = null;
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterFactoryOnProperty))
                {
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.CustomConverter);
                    Assert.IsType<MyClassCustomConverterFactory>(propertyInfo.CustomConverter);
                    expectedConverter = ((MyClassCustomConverterFactory)propertyInfo.CustomConverter).ConverterInstance;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterFactoryOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"test_SomeValue"}""", yaml);
            Assert.NotNull(expectedConverter);
            Assert.IsType<MyClassCustomConverter>(expectedConverter);

            TestClassWithCustomConverterFactoryOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterFactoryOnProperty>(yaml, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoCustomConverterFactoryIsNotExpandedWhenSetInResolver()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            MyClassCustomConverterFactory converterFactory = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithProperty))
                {
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.Null(propertyInfo.CustomConverter);
                    propertyInfo.CustomConverter = converterFactory;
                    Assert.Same(converterFactory, propertyInfo.CustomConverter);
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"test_SomeValue"}""", yaml);

            TestClassWithProperty deserialized = YamlSerializer.Deserialize<TestClassWithProperty>(yaml, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }


        [Fact]
        public static void YamlPropertyInfoGetIsNullAndMutableWhenUsingCreateYamlPropertyInfo()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(typeof(MyClass), "test");
            Assert.Null(propertyInfo.Get);
            Func<object, object> get = (obj) =>
            {
                throw new NotImplementedException();
            };

            propertyInfo.Get = get;
            Assert.Same(get, propertyInfo.Get);
        }

        [Fact]
        public static void YamlPropertyInfoGetIsNotNullForDefaultResolver()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.Get);

            TestClassWithCustomConverterOnProperty obj = new();

            Assert.Null(propertyInfo.Get(obj));

            obj.MyClassProperty = new MyClass();
            Assert.Same(obj.MyClassProperty, propertyInfo.Get(obj));

            MyClass sentinel = new();
            Func<object, object> get = (obj) => sentinel;
            propertyInfo.Get = get;
            Assert.Same(get, propertyInfo.Get);
            Assert.Same(sentinel, propertyInfo.Get(obj));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoGetPropertyNotSerializableButDeserializableWhenNull()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    propertyInfo.Get = null;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("{}", yaml);

            yaml = """{"MyClassProperty":"SomeValue"}""";
            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoGetIsRespected(bool useCustomConverter)
        {
            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            MyClass substitutedValue = new MyClass() { Value = "SomeOtherValue" };

            bool getterCalled = false;

            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    if (!useCustomConverter)
                    {
                        propertyInfo.CustomConverter = null;
                    }

                    propertyInfo.Get = (o) =>
                    {
                        Assert.Same(obj, o);
                        Assert.False(getterCalled);
                        getterCalled = true;
                        return substitutedValue;
                    };
                }
            });

            options.TypeInfoResolver = r;

            string yaml = YamlSerializer.Serialize(obj, options);
            if (useCustomConverter)
            {
                Assert.Equal("""{"MyClassProperty":"SomeOtherValue"}""", yaml);
            }
            else
            {
                Assert.Equal("""{"MyClassProperty":{"Value":"SomeOtherValue","Thing":null}}""", yaml);
            }

            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Equal(substitutedValue.Value, deserialized.MyClassProperty.Value);

            Assert.True(getterCalled);
        }

        [Fact]
        public static void YamlPropertyInfoSetIsNullAndMutableWhenUsingCreateYamlPropertyInfo()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(typeof(MyClass), "test");
            Assert.Null(propertyInfo.Set);
            Action<object, object> set = (obj, val) =>
            {
                throw new NotImplementedException();
            };

            propertyInfo.Set = set;
            Assert.Same(set, propertyInfo.Set);
        }

        [Fact]
        public static void YamlPropertyInfoSetIsNotNullForDefaultResolver()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Default;
            YamlTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            Assert.Equal(1, typeInfo.Properties.Count);
            YamlPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.Set);

            TestClassWithCustomConverterOnProperty obj = new();

            MyClass value = new MyClass();
            propertyInfo.Set(obj, value);
            Assert.Same(value, obj.MyClassProperty);

            MyClass sentinel = new();
            Action<object, object> set = (o, value) =>
            {
                Assert.Same(obj, o);
                Assert.Same(sentinel, value);
                obj.MyClassProperty = sentinel;
            };

            propertyInfo.Set = set;
            Assert.Same(set, propertyInfo.Set);

            propertyInfo.Set(obj, sentinel);
            Assert.Same(obj.MyClassProperty, sentinel);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoSetPropertyDeserializableButNotSerializableWhenNull()
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.Set);
                    propertyInfo.Set = null;
                    Assert.Null(propertyInfo.Set);
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"SomeValue"}""", yaml);

            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Null(deserialized.MyClassProperty);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public static void YamlPropertyInfoSetIsRespected(bool useCustomConverter)
        {
            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            MyClass substitutedValue = new MyClass() { Value = "SomeOtherValue" };
            bool setterCalled = false;

            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    YamlPropertyInfo propertyInfo = ti.Properties[0];
                    if (!useCustomConverter)
                    {
                        propertyInfo.CustomConverter = null;
                    }

                    Assert.NotNull(propertyInfo.Set);

                    Action<object, object?> setter = (o, val) =>
                    {
                        var testClass = (TestClassWithCustomConverterOnProperty)o;
                        Assert.IsType<MyClass>(val);
                        MyClass myClass = (MyClass)val;
                        Assert.Equal(obj.MyClassProperty.Value, myClass.Value);

                        testClass.MyClassProperty = substitutedValue;
                        Assert.False(setterCalled);
                        setterCalled = true;
                    };

                    propertyInfo.Set = setter;
                    Assert.Same(setter, propertyInfo.Set);
                }
            });

            options.TypeInfoResolver = r;

            string yaml = YamlSerializer.Serialize(obj, options);
            if (useCustomConverter)
            {
                Assert.Equal("""{"MyClassProperty":"SomeValue"}""", yaml);
            }
            else
            {
                Assert.Equal("""{"MyClassProperty":{"Value":"SomeValue","Thing":null}}""", yaml);
            }

            TestClassWithCustomConverterOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(yaml, options);
            Assert.Same(substitutedValue, deserialized.MyClassProperty);
            Assert.True(setterCalled);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void AddingNumberHandlingToPropertyIsRespected()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Null(ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
                    Assert.Equal(YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString, ti.Properties[0].NumberHandling);
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumber obj = new()
            {
                IntProperty = 37,
            };

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":"37"}""", yaml);

            TestClassWithNumber deserialized = YamlSerializer.Deserialize<TestClassWithNumber>(yaml, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        private class TestClassWithNumber
        {
            public int IntProperty { get; set; }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(YamlNumberHandling.Strict)]
        [Trait("category", "jsonoutput")]
        public static void RemovingOrChangingNumberHandlingFromPropertyIsRespected(YamlNumberHandling? numberHandling)
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumberHandlingOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Equal(YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString, ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = numberHandling;
                    Assert.Equal(numberHandling, ti.Properties[0].NumberHandling);
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumberHandlingOnProperty obj = new()
            {
                IntProperty = 37,
            };

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            TestClassWithNumberHandlingOnProperty deserialized = YamlSerializer.Deserialize<TestClassWithNumberHandlingOnProperty>(yaml, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        private class TestClassWithNumberHandlingOnProperty
        {
            [YamlNumberHandling(YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString)]
            public int IntProperty { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NumberHandlingFromTypeDoesntFlowToPropertyAndOverrideIsRespected()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumberHandling))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Null(ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = YamlNumberHandling.Strict;
                    Assert.Equal(YamlNumberHandling.Strict, ti.Properties[0].NumberHandling);
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumberHandling obj = new()
            {
                IntProperty = 37,
            };

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            TestClassWithNumberHandling deserialized = YamlSerializer.Deserialize<TestClassWithNumberHandling>(yaml, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        [YamlNumberHandling(YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString)]
        private class TestClassWithNumberHandling
        {
            public int IntProperty { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void NumberHandlingFromOptionsDoesntFlowToPropertyAndOverrideIsRespected()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Null(ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = YamlNumberHandling.Strict;
                    Assert.Equal(YamlNumberHandling.Strict, ti.Properties[0].NumberHandling);
                }
            });

            YamlSerializerOptions o = new();
            o.NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
            o.TypeInfoResolver = resolver;

            TestClassWithNumber obj = new()
            {
                IntProperty = 37,
            };

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            TestClassWithNumber deserialized = YamlSerializer.Deserialize<TestClassWithNumber>(yaml, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        [Fact]
        public static void ShouldSerializeShouldReportBackAssignedValue()
        {
            YamlSerializerOptions o = new();

            YamlTypeInfo ti = YamlTypeInfo.CreateYamlTypeInfo(typeof(MyClass), o);
            YamlPropertyInfo pi = ti.CreateYamlPropertyInfo(typeof(string), "test");

            Assert.Null(pi.ShouldSerialize);

            Func<object, object?, bool> value = (o, val) => throw new NotImplementedException();
            pi.ShouldSerialize = value;
            Assert.Same(value, pi.ShouldSerialize);

            pi.ShouldSerialize = null;
            Assert.Null(pi.ShouldSerialize);
        }

        [Fact]
        public static void CreateYamlPropertyInfo_ReturnsCorrectTypeOnPolymorphicConverter()
        {
            YamlSerializerOptions o = new()
            {
                Converters = { new PolymorphicConverter() }
            };

            YamlTypeInfo jti = YamlTypeInfo.CreateYamlTypeInfo(typeof(MyClass), o);

            Assert.IsType<PolymorphicConverter>(jti.Converter);
            Assert.IsAssignableFrom<YamlTypeInfo<MyClass>>(jti);
            Assert.Equal(typeof(MyClass), jti.Type);

            YamlPropertyInfo jpi = jti.CreateYamlPropertyInfo(typeof(string), "test");

            // The generic parameter of the property metadata type
            // should match that of property type and not the converter type.
            Assert.Equal(typeof(MyClass), jpi.DeclaringType);
            Assert.Equal(typeof(string), jpi.GetType().GetGenericArguments()[0]); 
            Assert.Equal(typeof(string), jpi.PropertyType);
        }

        public class PolymorphicConverter : YamlConverter<object>
        {
            public override bool CanConvert(Type _) => true;
            public override object? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options) => throw new NotImplementedException();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void AddingShouldSerializeToPropertyIsRespected()
        {
            TestClassWithNumber obj = new()
            {
                IntProperty = 3,
            };

            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Null(ti.Properties[0].ShouldSerialize);
                    ti.Properties[0].ShouldSerialize = (o, val) =>
                    {
                        Assert.Same(obj, o);
                        int intValue = (int)val;
                        Assert.Equal(obj.IntProperty, intValue);
                        return intValue != 3;
                    };
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("{}", yaml);

            obj.IntProperty = 37;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public static void RemovingOrChangingShouldSerializeFromPropertyWithIgnoreConditionIsRespected(bool removeShouldSerialize)
        {
            TestClassWithNumberAndIgnoreConditionOnProperty obj = new()
            {
                IntProperty = 37,
            };

            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumberAndIgnoreConditionOnProperty))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.NotNull(ti.Properties[0].ShouldSerialize);
                    Assert.False(ti.Properties[0].ShouldSerialize(null, 0));
                    Assert.True(ti.Properties[0].ShouldSerialize(null, 1));
                    Assert.True(ti.Properties[0].ShouldSerialize(null, -1));
                    Assert.True(ti.Properties[0].ShouldSerialize(null, 3));

                    if (removeShouldSerialize)
                    {
                        ti.Properties[0].ShouldSerialize = null;
                    }
                    else
                    {
                        ti.Properties[0].ShouldSerialize = (o, val) =>
                        {
                            Assert.Same(obj, o);
                            int intValue = (int)val;
                            Assert.Equal(obj.IntProperty, intValue);
                            return intValue != 3;
                        };
                    }
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            obj.IntProperty = default;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":0}""", yaml);

            obj.IntProperty = 3;
            yaml = YamlSerializer.Serialize(obj, o);
            if (removeShouldSerialize)
            {
                Assert.Equal("""{"IntProperty":3}""", yaml);
            }
            else
            {
                Assert.Equal("{}", yaml);
            }
        }

        private class TestClassWithNumberAndIgnoreConditionOnProperty
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public int IntProperty { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DefaultIgnoreConditionFromOptionsDoesntFlowToShouldSerializePropertyAndOverrideIsRespected()
        {
            TestClassWithNumber obj = new()
            {
                IntProperty = 37,
            };

            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Null(ti.Properties[0].ShouldSerialize);
                    ti.Properties[0].ShouldSerialize = (o, val) =>
                    {
                        Assert.Same(obj, o);
                        int intValue = (int)val;
                        Assert.Equal(obj.IntProperty, intValue);
                        return intValue != 3;
                    };
                }
            });

            YamlSerializerOptions o = new();
            o.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault;
            o.TypeInfoResolver = resolver;

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            obj.IntProperty = default;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":0}""", yaml);

            obj.IntProperty = 3;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DefaultIgnoreConditionFromOptionsIsRespectedWhenShouldSerializePropertyIsAssignedAndCleared()
        {
            TestClassWithNumberAndIgnoreConditionOnProperty obj = new()
            {
                IntProperty = 37,
            };

            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Equal(1, ti.Properties.Count);
                    Assert.Null(ti.Properties[0].ShouldSerialize);
                    ti.Properties[0].ShouldSerialize = (o, val) =>
                    {
                        Assert.Same(obj, o);
                        int intValue = (int)val;
                        Assert.Equal(obj.IntProperty, intValue);
                        return intValue != 3;
                    };

                    ti.Properties[0].ShouldSerialize = null;
                }
            });

            YamlSerializerOptions o = new();
            o.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault;
            o.TypeInfoResolver = resolver;

            string yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", yaml);

            obj.IntProperty = default;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("{}", yaml);

            obj.IntProperty = 3;
            yaml = YamlSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":3}""", yaml);
        }

        public enum ModifyYamlIgnore
        {
            DontModify,
            NeverSerialize,
            AlwaysSerialize,
            DontSerializeNumber3OrStringAsd,
        }

        [Theory]
        [InlineData(YamlIgnoreCondition.WhenWritingDefault, ModifyYamlIgnore.DontModify)]
        [InlineData(YamlIgnoreCondition.WhenWritingNull, ModifyYamlIgnore.DontModify)]
        [InlineData(YamlIgnoreCondition.Never, ModifyYamlIgnore.DontModify)]
        [InlineData(YamlIgnoreCondition.WhenWritingDefault, ModifyYamlIgnore.NeverSerialize)]
        [InlineData(YamlIgnoreCondition.WhenWritingNull, ModifyYamlIgnore.NeverSerialize)]
        [InlineData(YamlIgnoreCondition.WhenWriting, ModifyYamlIgnore.NeverSerialize)]
        [InlineData(YamlIgnoreCondition.Never, ModifyYamlIgnore.NeverSerialize)]
        [InlineData(YamlIgnoreCondition.WhenWritingDefault, ModifyYamlIgnore.AlwaysSerialize)]
        [InlineData(YamlIgnoreCondition.WhenWritingNull, ModifyYamlIgnore.AlwaysSerialize)]
        [InlineData(YamlIgnoreCondition.Never, ModifyYamlIgnore.AlwaysSerialize)]
        [InlineData(YamlIgnoreCondition.WhenWritingDefault, ModifyYamlIgnore.DontSerializeNumber3OrStringAsd)]
        [InlineData(YamlIgnoreCondition.WhenWritingNull, ModifyYamlIgnore.DontSerializeNumber3OrStringAsd)]
        [InlineData(YamlIgnoreCondition.Never, ModifyYamlIgnore.DontSerializeNumber3OrStringAsd)]
        [Trait("category", "jsonoutput")]
        public static void YamlIgnoreConditionIsCorrectlyTranslatedToShouldSerializeDelegateAndChangingShouldSerializeIsRespected(YamlIgnoreCondition defaultIgnoreCondition, ModifyYamlIgnore modify)
        {
            TestClassWithEveryPossibleYamlIgnore obj = new()
            {
                AlwaysProperty = "Always",
                WhenWritingDefaultProperty = 37,
                WhenWritingNullProperty = "WhenWritingNull",
                NeverProperty = "Never",
                Property = "None",
            };

            // sanity check
            bool modifierTestRun = false;

            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(ti =>
            {
                if (ti.Type != typeof(TestClassWithEveryPossibleYamlIgnore))
                    return;

                Assert.Equal(5, ti.Properties.Count);
                Assert.False(modifierTestRun);
                modifierTestRun = true;
                foreach (var property in ti.Properties)
                {
                    string yamlIgnoreValue = property.Name.Substring(0, property.Name.Length - "Property".Length);
                    YamlIgnoreCondition? ignoreConditionOnProperty = string.IsNullOrEmpty(yamlIgnoreValue) ? null : (YamlIgnoreCondition)Enum.Parse(typeof(YamlIgnoreCondition), yamlIgnoreValue);
                    TestYamlIgnoreConditionDelegate(defaultIgnoreCondition, ignoreConditionOnProperty, property, modify);
                }
            });

            YamlSerializerOptions options = new();
            options.TypeInfoResolver = resolver;
            options.DefaultIgnoreCondition = defaultIgnoreCondition;


            // - delegate correctly returns value
            // - nulling out delegate removes behavior
            // - check every options default
            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.True(modifierTestRun);

            switch (modify)
            {
                case ModifyYamlIgnore.DontModify:
                    Assert.Equal("""{"WhenWritingDefaultProperty":37,"WhenWritingNullProperty":"WhenWritingNull","NeverProperty":"Never","Property":"None"}""", yaml);
                    break;
                case ModifyYamlIgnore.NeverSerialize:
                    Assert.Equal("{}", yaml);
                    break;
                case ModifyYamlIgnore.AlwaysSerialize:
                case ModifyYamlIgnore.DontSerializeNumber3OrStringAsd:
                    Assert.Equal("""{"AlwaysProperty":"Always","WhenWritingDefaultProperty":37,"WhenWritingNullProperty":"WhenWritingNull","NeverProperty":"Never","Property":"None"}""", yaml);
                    break;
            }

            obj.AlwaysProperty = default;
            obj.WhenWritingDefaultProperty = default;
            obj.WhenWritingNullProperty = default;
            obj.NeverProperty = default;
            obj.Property = default;

            yaml = YamlSerializer.Serialize(obj, options);

            switch (modify)
            {
                case ModifyYamlIgnore.DontModify:
                    {
                        string noYamlIgnoreProperty = defaultIgnoreCondition == YamlIgnoreCondition.Never ? ""","Property":null""" : null;
                        Assert.Equal($@"{{""NeverProperty"":null{noYamlIgnoreProperty}}}", yaml);
                        break;
                    }
                case ModifyYamlIgnore.NeverSerialize:
                    Assert.Equal("{}", yaml);
                    break;
                case ModifyYamlIgnore.AlwaysSerialize:
                case ModifyYamlIgnore.DontSerializeNumber3OrStringAsd:
                    Assert.Equal("""{"AlwaysProperty":null,"WhenWritingDefaultProperty":0,"WhenWritingNullProperty":null,"NeverProperty":null,"Property":null}""", yaml);
                    break;
            }

            obj.AlwaysProperty = "asd";
            obj.WhenWritingDefaultProperty = 3;
            obj.WhenWritingNullProperty = "asd";
            obj.NeverProperty = "asd";
            obj.Property = "asd";

            yaml = YamlSerializer.Serialize(obj, options);

            switch (modify)
            {
                case ModifyYamlIgnore.DontModify:
                    Assert.Equal("""{"WhenWritingDefaultProperty":3,"WhenWritingNullProperty":"asd","NeverProperty":"asd","Property":"asd"}""", yaml);
                    break;
                case ModifyYamlIgnore.AlwaysSerialize:
                    Assert.Equal("""{"AlwaysProperty":"asd","WhenWritingDefaultProperty":3,"WhenWritingNullProperty":"asd","NeverProperty":"asd","Property":"asd"}""", yaml);
                    break;
                case ModifyYamlIgnore.NeverSerialize:
                case ModifyYamlIgnore.DontSerializeNumber3OrStringAsd:
                    Assert.Equal("{}", yaml);
                    break;
            }

            static void TestYamlIgnoreConditionDelegate(YamlIgnoreCondition defaultIgnoreCondition, YamlIgnoreCondition? ignoreConditionOnProperty, YamlPropertyInfo property, ModifyYamlIgnore modify)
            {
                // defaultIgnoreCondition is not taken into account, we might expect null if defaultIgnoreCondition == ignoreConditionOnProperty
                switch (ignoreConditionOnProperty)
                {
                    case null:
                        Assert.Null(property.ShouldSerialize);
                        break;

                    case YamlIgnoreCondition.Always:
                        Assert.NotNull(property.ShouldSerialize);
                        Assert.False(property.ShouldSerialize(null, null));
                        Assert.False(property.ShouldSerialize(null, ""));
                        Assert.False(property.ShouldSerialize(null, "asd"));
                        Assert.Throws<InvalidCastException>(() => property.ShouldSerialize(null, 0));

                        Assert.Null(property.Get);
                        Assert.Null(property.Set);
                        break;
                    case YamlIgnoreCondition.WhenWritingDefault:
                        Assert.NotNull(property.ShouldSerialize);
                        Assert.False(property.ShouldSerialize(null, 0));
                        Assert.True(property.ShouldSerialize(null, 1));
                        Assert.True(property.ShouldSerialize(null, -1));
                        Assert.Throws<NullReferenceException>(() => property.ShouldSerialize(null, null));
                        Assert.Throws<InvalidCastException>(() => property.ShouldSerialize(null, "string"));
                        break;
                    case YamlIgnoreCondition.WhenWritingNull:
                        Assert.NotNull(property.ShouldSerialize);
                        Assert.False(property.ShouldSerialize(null, null));
                        Assert.True(property.ShouldSerialize(null, ""));
                        Assert.True(property.ShouldSerialize(null, "asd"));
                        Assert.Throws<InvalidCastException>(() => property.ShouldSerialize(null, 0));
                        break;
                    case YamlIgnoreCondition.Never:
                        Assert.NotNull(property.ShouldSerialize);
                        Assert.True(property.ShouldSerialize(null, null));
                        Assert.True(property.ShouldSerialize(null, ""));
                        Assert.True(property.ShouldSerialize(null, "asd"));
                        Assert.Throws<InvalidCastException>(() => property.ShouldSerialize(null, 0));
                        break;
                }

                if (modify != ModifyYamlIgnore.DontModify && ignoreConditionOnProperty == YamlIgnoreCondition.Always)
                {
                    property.Get = (o) => ((TestClassWithEveryPossibleYamlIgnore)o).AlwaysProperty;
                }

                switch (modify)
                {
                    case ModifyYamlIgnore.AlwaysSerialize:
                        property.ShouldSerialize = (o, v) => true;
                        break;
                    case ModifyYamlIgnore.NeverSerialize:
                        property.ShouldSerialize = (o, v) => false;
                        break;
                    case ModifyYamlIgnore.DontSerializeNumber3OrStringAsd:
                        property.ShouldSerialize = (o, v) =>
                        {
                            if (v is null)
                            {
                                return true;
                            }
                            else if (v is int intVal)
                            {
                                return intVal != 3;
                            }
                            else if (v is string stringVal)
                            {
                                return stringVal != "asd";
                            }

                            Assert.Fail("ShouldSerialize set for value which is not int or string");
                            return false;
                        };
                        break;
                }
            }
        }

        private class TestClassWithEveryPossibleYamlIgnore
        {
            [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
            public string? AlwaysProperty { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingDefault)]
            public int WhenWritingDefaultProperty { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingNull)]
            public string? WhenWritingNullProperty { get; set; }

            [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
            public string? NeverProperty { get; set; }

            public string? Property { get; set; }
        }

        private class TestClassWithProperty
        {
            public MyClass MyClassProperty { get; set; }
        }

        private class TestClassWithCustomConverterOnProperty
        {
            [YamlConverter(typeof(MyClassConverterOriginal))]
            public MyClass MyClassProperty { get; set; }
        }

        private class TestClassWithCustomConverterFactoryOnProperty
        {
            [YamlConverter(typeof(MyClassCustomConverterFactory))]
            public MyClass MyClassProperty { get; set; }
        }

        private class MyClassConverterOriginal : YamlConverter<MyClass>
        {
            public override MyClass? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.String)
                    throw new InvalidOperationException($"Wrong token type: {reader.TokenType}");

                MyClass myClass = new MyClass();
                myClass.Value = reader.GetString();
                return myClass;
            }

            public override void Write(Utf8YamlWriter writer, MyClass value, YamlSerializerOptions options)
            {
                writer.WriteStringValue(value.Value);
            }
        }

        private class MyClassCustomConverter : YamlConverter<MyClass>
        {
            private string _prefix;

            public MyClassCustomConverter(string prefix)
            {
                _prefix = prefix;
            }

            public override MyClass? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.String)
                    throw new InvalidOperationException($"Wrong token type: {reader.TokenType}");

                MyClass myClass = new MyClass();
                myClass.Value = reader.GetString().Substring(_prefix.Length);
                return myClass;
            }

            public override void Write(Utf8YamlWriter writer, MyClass value, YamlSerializerOptions options)
            {
                writer.WriteStringValue(_prefix + value.Value);
            }
        }

        private class MyClassCustomConverterFactory : YamlConverterFactory
        {
            internal YamlConverter<MyClass> ConverterInstance { get; } = new MyClassCustomConverter("test_");
            public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(MyClass);

            public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(typeof(MyClass), typeToConvert);
                return ConverterInstance;
            }
        }

        [Fact]
        public static void ClassWithExtensionDataAttribute_IsReturnedInMetadata()
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var jti = resolver.GetTypeInfo(typeof(ClassWithExtensionDataAttribute), new());

            Assert.Equal(3, jti.Properties.Count);
            Assert.False(jti.Properties[0].IsExtensionData);
            Assert.False(jti.Properties[1].IsExtensionData);

            YamlPropertyInfo lastProperty = jti.Properties[2];
            Assert.Equal("ExtensionData", lastProperty.Name);
            Assert.Equal(typeof(Dictionary<string, object>), lastProperty.PropertyType);
            Assert.True(lastProperty.IsExtensionData);
        }

        [Fact]
        public static void ClassWithExtensionDataAttribute_ChangingExtensionDataFlagDisablesExtensionData()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        jti =>
                        {
                            if (jti.Type == typeof(ClassWithExtensionDataAttribute))
                            {
                                Assert.Equal(3, jti.Properties.Count);
                                Assert.True(jti.Properties[2].IsExtensionData);
                                jti.Properties[2].IsExtensionData = false;
                            }
                        }
                    }
                }
            };

            var value = new ClassWithExtensionDataAttribute
            {
                Value1 = 1,
                Value2 = 2,
                ExtensionData = new Dictionary<string, object>
                {
                    ["Value3"] = 3
                }
            };

            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Value1":1,"Value2":2,"ExtensionData":{"Value3":3}}""", yaml);

            value = YamlSerializer.Deserialize<ClassWithExtensionDataAttribute>("""{"unrecognizedValue":42}""", options);
            Assert.Null(value.ExtensionData);
        }

        [Fact]
        public static void ClassWithExtensionDataAttribute_RemovingExtensionDataPropertyIgnoresExtensionData()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        jti =>
                        {
                            if (jti.Type == typeof(ClassWithExtensionDataAttribute))
                            {
                                Assert.Equal(3, jti.Properties.Count);
                                Assert.True(jti.Properties[2].IsExtensionData);
                                jti.Properties.RemoveAt(2);
                            }
                        }
                    }
                }
            };

            var value = new ClassWithExtensionDataAttribute
            {
                Value1 = 1,
                Value2 = 2,
                ExtensionData = new Dictionary<string, object>
                {
                    ["Value3"] = 3
                }
            };

            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Value1":1,"Value2":2}""", yaml);

            value = YamlSerializer.Deserialize<ClassWithExtensionDataAttribute>("""{"unrecognizedValue":42}""", options);
            Assert.Null(value.ExtensionData);
        }

        [Theory]
        [InlineData(typeof(IDictionary<string, object>))]
        [InlineData(typeof(IDictionary<string, YamlElement>))]
        [InlineData(typeof(IReadOnlyDictionary<string, object>))]
        [InlineData(typeof(IReadOnlyDictionary<string, YamlElement>))]
        [InlineData(typeof(Dictionary<string, object>))]
        [InlineData(typeof(Dictionary<string, YamlElement>))]
        [InlineData(typeof(ConcurrentDictionary<string, YamlElement>))]
        [InlineData(typeof(YamlObject))]
        public static void EnablingExtensionData_SupportedPropertyType(Type type)
        {
            var jti = YamlTypeInfo.CreateYamlTypeInfo(typeof(ClassWithExtensionDataAttribute), new());
            var jpi = jti.CreateYamlPropertyInfo(type, "ExtensionData");

            Assert.False(jpi.IsExtensionData);
            jpi.IsExtensionData = true;
            Assert.True(jpi.IsExtensionData);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(ClassWithExtensionDataAttribute))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(IDictionary<string, string>))]
        [InlineData(typeof(Dictionary<string, int>))]
        public static void EnablingExtensionData_UnsupportedPropertyType_ThrowsInvalidOperationException(Type type)
        {
            var jti = YamlTypeInfo.CreateYamlTypeInfo(typeof(ClassWithExtensionDataAttribute), new());
            var jpi = jti.CreateYamlPropertyInfo(type, "ExtensionData");

            Assert.False(jpi.IsExtensionData);
            Assert.Throws<InvalidOperationException>(() => jpi.IsExtensionData = true);
        }

        public class ClassWithExtensionDataAttribute
        {
            public int Value1 { get; set; }
            public int Value2 { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object> ExtensionData { get; set; }
        }

        [Fact]
        public static void ClassWithoutExtensionDataAttribute_CanHaveExtensionDataEnabled()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        jti =>
                        {
                            if (jti.Type == typeof(ClassWithTwoExtensionDataLikeProperties))
                            {
                                YamlPropertyInfo propertyInfo = jti.Properties.First(prop => prop.Name == "ExtensionData2");
                                propertyInfo.IsExtensionData = true;
                            }
                        }
                    }
                }
            };

            var value = new ClassWithTwoExtensionDataLikeProperties
            {
                ExtensionData1 = new Dictionary<string, object> { ["extension1"] = 1 },
                ExtensionData2 = new Dictionary<string, object> { ["extension2"] = 2 },
            };

            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""{"ExtensionData1":{"extension1":1},"extension2":2}""", yaml);

            value = YamlSerializer.Deserialize<ClassWithTwoExtensionDataLikeProperties>("""{"unrecognizedData":3}""", options);
            Assert.Null(value.ExtensionData1);
            Assert.NotNull(value.ExtensionData2);
            Assert.True(value.ExtensionData2.ContainsKey("unrecognizedData"));
        }

        [Fact]
        public static void ClassWithoutExtensionDataAttribute_SettingDuplicateExtensionDataProperties_ThrowsInvalidOperationException()
        {
            bool resolverRanToCompletion = false;
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        jti =>
                        {
                            if (jti.Type == typeof(ClassWithTwoExtensionDataLikeProperties))
                            {
                                Assert.Equal(2, jti.Properties.Count);
                                jti.Properties[0].IsExtensionData = true;
                                jti.Properties[1].IsExtensionData = true;
                                resolverRanToCompletion = true;
                            }
                        }
                    }
                }
            };

            var value = new ClassWithTwoExtensionDataLikeProperties();
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, options));
            Assert.True(resolverRanToCompletion);
        }

        public class ClassWithTwoExtensionDataLikeProperties
        {
            public Dictionary<string, object> ExtensionData1 { get; set; }
            public Dictionary<string, object> ExtensionData2 { get; set; }
        }

        [Fact]
        public static void DefaultYamlTypeInfoResolver_YamlPropertyInfo_ReturnsMemberInfoAsAttributeProvider()
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            YamlTypeInfo jti = resolver.GetTypeInfo(typeof(ClassWithFieldsAndProperties), new());

            Assert.Equal(2, jti.Properties.Count);

            YamlPropertyInfo fieldPropInfo = jti.Properties.First(prop => prop.Name == "Field");
            FieldInfo fieldInfo = typeof(ClassWithFieldsAndProperties).GetField("Field");
            Assert.NotNull(fieldInfo);
            Assert.Same(fieldInfo, fieldPropInfo.AttributeProvider);

            YamlPropertyInfo propertyPropInfo = jti.Properties.First(prop => prop.Name == "Property");
            PropertyInfo propInfo = typeof(ClassWithFieldsAndProperties).GetProperty("Property");
            Assert.NotNull(propInfo);
            Assert.Same(propInfo, propertyPropInfo.AttributeProvider);
        }

        [Fact]
        public static void YamlPropertyInfo_AttributeProvider_ReassigningValuesHasNoEffect()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        jti =>
                        {
                            if (jti.Type == typeof(ClassWithFieldsAndProperties))
                            {
                                Assert.Equal(2, jti.Properties.Count);

                                jti.Properties[0].AttributeProvider = jti.Properties[1].AttributeProvider;
                                Assert.Same(jti.Properties[0].AttributeProvider, jti.Properties[1].AttributeProvider);

                                jti.Properties[1].AttributeProvider = null;
                                Assert.Null(jti.Properties[1].AttributeProvider);
                            }
                        }
                    }
                }
            };

            var value = new ClassWithFieldsAndProperties { Field = "Field", Property = 42 };
            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Field":"Field","Property":42}""", yaml);
        }

        public class ClassWithFieldsAndProperties
        {
            [YamlInclude]
            public string Field;
            public int Property { get; set; }
        }

        [Fact]
        public static void CanEnableDeserializationOnAlwaysIgnoreProperty()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        yamlTypeInfo =>
                        {
                            if (yamlTypeInfo.Type != typeof(ClassWithYamlIgnoreAlwaysProperty))
                            {
                                return;
                            }

                            Assert.Equal(1, yamlTypeInfo.Properties.Count);
                            YamlPropertyInfo yamlPropertyInfo = yamlTypeInfo.Properties[0];

                            Assert.NotNull(yamlPropertyInfo.ShouldSerialize);
                            Assert.Null(yamlPropertyInfo.Get);
                            Assert.Null(yamlPropertyInfo.Set);

                            yamlPropertyInfo.Set = (obj, value) => ((ClassWithYamlIgnoreAlwaysProperty)obj).Value = (int)value;
                        }
                    }
                }
            };

            ClassWithYamlIgnoreAlwaysProperty value = YamlSerializer.Deserialize<ClassWithYamlIgnoreAlwaysProperty>("""{"Value":42}""", options);
            Assert.Equal(42, value.Value);
        }

        public class ClassWithYamlIgnoreAlwaysProperty
        {
            [YamlIgnore]
            public int Value { get; set; }
        }

        [Theory]
        [MemberData(nameof(ClassWithSetterOnlyCollectionProperty_SupportedWhenSetManually_MemberData))]
        public static void ClassWithSetterOnlyCollectionProperty_SupportedWhenSetManually<T>(T value, string yaml)
        {
            _ = value; // parameter not used

            string fullYaml = $@"{{""Value"":{yaml}}}";

            // sanity check: deserialization is not supported using the default contract
            ClassWithSetterOnlyProperty<T> result = YamlSerializer.Deserialize<ClassWithSetterOnlyProperty<T>>(fullYaml);
            Assert.Null(result.GetValue());

            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        yamlTypeInfo =>
                        {
                            if (yamlTypeInfo.Type != typeof(ClassWithSetterOnlyProperty<T>))
                            {
                                return;
                            }

                            Assert.Equal(1, yamlTypeInfo.Properties.Count);

                            YamlPropertyInfo jpi = yamlTypeInfo.Properties[0];
                            Assert.NotNull(jpi.Set);
                            jpi.Set = (obj, value) => ((ClassWithSetterOnlyProperty<T>)obj)._value = (T)value;
                        }
                    }
                }
            };

            result = YamlSerializer.Deserialize<ClassWithSetterOnlyProperty<T>>(fullYaml, options);
            Assert.NotNull(result.GetValue());
        }

        public static IEnumerable<object[]> ClassWithSetterOnlyCollectionProperty_SupportedWhenSetManually_MemberData()
        {
            yield return Wrap(new List<int>(), "[1,2,3]");
            yield return Wrap(new Dictionary<string, string>(), """{"key":"value"}""");

            static object[] Wrap<T>(T value, string yaml) => new object[] { value, yaml };
        }

        public class ClassWithSetterOnlyProperty<T>
        {
            internal T _value;
            public T Value { set => _value = value; }
            public T GetValue() => _value;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithReadOnlyMembers_SupportedWhenSetManually()
        {
            var value = new ClassWithReadOnlyMembers();

            // Sanity check -- default contract does not support serialization
            var options = new YamlSerializerOptions { IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true };
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("{}", yaml);

            // Use a constract resolver that updates getters manually
            options = new YamlSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,

                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        yamlTypeInfo =>
                        {
                            if (yamlTypeInfo.Type != typeof(ClassWithReadOnlyMembers))
                            {
                                return;
                            }

                            Assert.Equal(2, yamlTypeInfo.Properties.Count);

                            YamlPropertyInfo jpi = yamlTypeInfo.Properties[0];
                            Assert.Equal("Field", jpi.Name); // YamlPropertyOrder should ensure correct ordering of properties.
                            Assert.NotNull(jpi.Get);
                            jpi.ShouldSerialize = null;

                            jpi = yamlTypeInfo.Properties[1];
                            Assert.Equal("Property", jpi.Name); // YamlPropertyOrder should ensure correct ordering of properties.
                            Assert.NotNull(jpi.Get);
                            jpi.ShouldSerialize = null;

                            // No policy is applied to custom read-only properties
                            // regardless of `ShouldSerialize` configuration.
                            jpi = yamlTypeInfo.CreateYamlPropertyInfo(typeof(int), "CustomProperty");
                            jpi.Get = static _ => 42;
                            jpi.Order = 2;
                            yamlTypeInfo.Properties.Add(jpi);
                        }
                    }
                }
            };

            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"Field":1,"Property":2,"CustomProperty":42}""", yaml);
        }

        public class ClassWithReadOnlyMembers
        {
            [YamlInclude, YamlPropertyOrder(0)]
            public readonly int Field = 1;
            [YamlPropertyOrder(1)]
            public int Property => 2;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void IgnoreNullValues_Serialization_CanBeInvalidatedByCustomContracts()
        {
            var value = new ClassWithNullableProperty();

            // Sanity check -- property is ignored using default contract
            var options = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull };
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("{}", yaml);

            // Property can be re-enabled using a custom contract
            options = new YamlSerializerOptions
            {
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        yamlTypeInfo =>
                        {
                            if (yamlTypeInfo.Type != typeof(ClassWithNullableProperty))
                            {
                                return;
                            }

                            Assert.Equal(1, yamlTypeInfo.Properties.Count);

                            YamlPropertyInfo jpi = yamlTypeInfo.Properties[0];
                            Assert.Null(jpi.ShouldSerialize);
                            Assert.NotNull(jpi.Get);

                            // Because null `ShouldSerialize` gets overridden by
                            // `DefaultIgnoreCondition`/`IgnoreNullValues` post-configuration,
                            // we need to explicitly set a delegate that always returns true.
                            // This is a design quirk we might want to consider changing.
                            jpi.ShouldSerialize = (_,value) => true;
                        }
                    }
                }
            };

            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"Value":null}""", yaml);
        }

        public class ClassWithNullableProperty
        {
            private string? _value;

            public string? Value
            {
                get => _value;
                set => _value = value ?? "NULL";
            }
        }

        [Fact]
        public static void TypeWithIgnoredUnsupportedType_ShouldBeSupported()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/76807

            // Sanity check -- metadata resolution for the unsupported type is failing
            Assert.Throws<InvalidOperationException>(() => YamlSerializerOptions.Default.GetTypeInfo(typeof(UnsupportedType)));

            // Serialization works as expected
            string yaml = YamlSerializer.Serialize(new PocoWithIgnoredUnsupportedType());
            YamlTestHelper.AssertYamlEqual("{}", yaml);

            // Metadata is reported as expected
            YamlTypeInfo jti = YamlSerializerOptions.Default.GetTypeInfo(typeof(PocoWithIgnoredUnsupportedType));
            Assert.Equal(1, jti.Properties.Count);
            YamlPropertyInfo propertyInfo = jti.Properties[0];
            Assert.Null(propertyInfo.Get);
            Assert.Null(propertyInfo.Set);
        }

        public class PocoWithIgnoredUnsupportedType
        {
            [YamlIgnore]
            public UnsupportedType UnsuportedProperty { get; set; }
        }

        [Fact]
        public static void TypeWithUnIgnoredUnsupportedType_CanModifyUnsupportedType()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        static jti =>
                        {
                            if (jti.Type == typeof(PocoWithUnIgnoredUnsupportedType))
                            {
                                Assert.Equal(1, jti.Properties.Count);
                                YamlPropertyInfo propertyInfo = jti.Properties[0];
                                Assert.Equal(typeof(UnsupportedType), propertyInfo.PropertyType);

                                jti.Properties.Clear();
                            }
                        }
                    }
                }
            };

            string yaml = YamlSerializer.Serialize(new PocoWithUnIgnoredUnsupportedType(), options);
            YamlTestHelper.AssertYamlEqual("{}", yaml);
        }

        public class PocoWithUnIgnoredUnsupportedType
        {
            public UnsupportedType UnsuportedProperty { get; set; }
        }

        public class UnsupportedType
        {
            public ReadOnlySpan<byte> Span => Array.Empty<byte>();
        }

        [Fact]
        public static void YamlCreationHandlingAttributeIsShownInMetadata()
        {
            bool typeResolved = false;
            DefaultYamlTypeInfoResolver resolver = new()
            {
                Modifiers =
                {
                    (ti) =>
                    {
                        if (ti.Type == typeof(TestClassWithYamlCreationHandlingOnProperty))
                        {
                            Assert.Equal(3, ti.Properties.Count);
                            Assert.Null(ti.Properties[0].ObjectCreationHandling);
                            Assert.Equal(YamlObjectCreationHandling.Replace, ti.Properties[1].ObjectCreationHandling);
                            Assert.Equal(YamlObjectCreationHandling.Populate, ti.Properties[2].ObjectCreationHandling);
                            typeResolved = true;
                        }
                    }
                }
            };

            YamlSerializerOptions o = new()
            {
                TypeInfoResolver = resolver
            };

            var deserialized = YamlSerializer.Deserialize<TestClassWithYamlCreationHandlingOnProperty>("{}", o);
            Assert.True(typeResolved);
        }

        [Theory]
        [InlineData((YamlObjectCreationHandling)(-1))]
        [InlineData((YamlObjectCreationHandling)2)]
        [InlineData((YamlObjectCreationHandling)int.MaxValue)]
        public static void ObjectCreationHandling_SetInvalidValue_ThrowsArgumentOutOfRangeException(YamlObjectCreationHandling handling)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new());
            YamlPropertyInfo propertyInfo = yamlTypeInfo.CreateYamlPropertyInfo(typeof(List<int>), "List");
            Assert.Throws<ArgumentOutOfRangeException>(() => propertyInfo.ObjectCreationHandling = handling);
        }

        private class TestClassWithYamlCreationHandlingOnProperty
        {
            [YamlPropertyOrder(0)]
            public Poco PropertyWitoutAttribute { get; set; }

            [YamlPropertyOrder(1)]
            [YamlObjectCreationHandling(YamlObjectCreationHandling.Replace)]
            public Poco PropertyWithReplace { get; set; }

            [YamlPropertyOrder(2)]
            [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
            public Poco PropertyWithPopulate { get; set; }
        }
    }
}
