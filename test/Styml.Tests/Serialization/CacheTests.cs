// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;
using Microsoft.DotNet.RemoteExecutor;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class CacheTests
    {
        [Fact, OuterLoop]
        public static async Task MultipleThreads_SameType_DifferentYaml_Looping()
        {
            const int Iterations = 100;

            for (int i = 0; i < Iterations; i++)
            {
                await MultipleThreads_SameType_DifferentYaml();
            }
        }

        [Fact]
        public static async Task MultipleThreads_SameType_DifferentYaml()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            var options = new YamlSerializerOptions();

            // Verify the test class has >64 properties since that is a threshold for using the fallback dictionary.
            Assert.True(typeof(SimpleTestClass).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 64);

            void DeserializeObjectMinimal()
            {
                SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>("""{"MyDecimal" : 3.3}""", options);
            };

            void DeserializeObjectFlipped()
            {
                SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(SimpleTestClass.s_yaml_flipped, options);
                obj.Verify();
            };

            void DeserializeObjectNormal()
            {
                SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(SimpleTestClass.s_yaml, options);
                obj.Verify();
            };

            void SerializeObject()
            {
                var obj = new SimpleTestClass();
                obj.Initialize();
                YamlSerializer.Serialize(obj, options);
            };

            const int ThreadCount = 8;
            const int ConcurrentTestsCount = 4;
            Task[] tasks = new Task[ThreadCount * ConcurrentTestsCount];

            for (int i = 0; i < tasks.Length; i += ConcurrentTestsCount)
            {
                // Create race condition to populate the sorted property cache with different json ordering.
                tasks[i + 0] = Task.Run(() => DeserializeObjectMinimal());
                tasks[i + 1] = Task.Run(() => DeserializeObjectFlipped());
                tasks[i + 2] = Task.Run(() => DeserializeObjectNormal());

                // Ensure no exceptions on serialization
                tasks[i + 3] = Task.Run(() => SerializeObject());
            };

            await Task.WhenAll(tasks);
        }

        [Fact, OuterLoop]
        public static async Task MultipleThreads_DifferentTypes_Looping()
        {
            const int Iterations = 100;

            for (int i = 0; i < Iterations; i++)
            {
                await MultipleThreads_DifferentTypes();
            }
        }

        [Fact]
        public static async Task MultipleThreads_DifferentTypes()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            var options = new YamlSerializerOptions();

            const int TestClassCount = 2;

            var testObjects = new ITestClass[TestClassCount]
            {
                new SimpleTestClassWithNulls(),
                new SimpleTestClass(),
            };

            foreach (ITestClass obj in testObjects)
            {
                obj.Initialize();
            }

            void Test(int i)
            {
                Type testClassType = testObjects[i].GetType();

                string yaml = YamlSerializer.Serialize(testObjects[i], testClassType, options);

                ITestClass obj = (ITestClass)YamlSerializer.Deserialize(yaml, testClassType, options);
                obj.Verify();
            };

            const int OuterCount = 12;
            Task[] tasks = new Task[OuterCount * TestClassCount];

            for (int i = 0; i < tasks.Length; i += TestClassCount)
            {
                tasks[i + 0] = Task.Run(() => Test(TestClassCount - 1));
                tasks[i + 1] = Task.Run(() => Test(TestClassCount - 2));
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public static void PropertyCacheWithMinInputsFirst()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            var options = new YamlSerializerOptions();

            string yaml = "{}";
            YamlSerializer.Deserialize<SimpleTestClass>(yaml, options);

            SimpleTestClass testObj = new SimpleTestClass();
            testObj.Initialize();
            testObj.Verify();

            yaml = YamlSerializer.Serialize(testObj, options);
            testObj = YamlSerializer.Deserialize<SimpleTestClass>(yaml, options);
            testObj.Verify();
        }

        [Fact]
        public static void PropertyCacheWithMinInputsLast()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            var options = new YamlSerializerOptions();

            SimpleTestClass testObj = new SimpleTestClass();
            testObj.Initialize();
            testObj.Verify();

            string yaml = YamlSerializer.Serialize(testObj, options);
            testObj = YamlSerializer.Deserialize<SimpleTestClass>(yaml, options);
            testObj.Verify();

            yaml = "{}";
            YamlSerializer.Deserialize<SimpleTestClass>(yaml, options);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public static void PropertyCache_NamesWithSameKeyButDifferentLength_AreDistinct(int shortNameLength)
        {
            string shortName = new('a', shortNameLength);
            string longName =
                shortName +
                new string('\0', 7 - shortNameLength) +
                new string('b', 249 + shortNameLength);

            Assert.Equal(256, longName.Length - shortName.Length);

            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        // Customize the name so the theory can exercise every length with one POCO type.
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PropertyKeyLengthPoco))
                            {
                                typeInfo.Properties[0].Name = shortName;
                            }
                        }
                    }
                }
            };

            string yaml = YamlSerializer.Serialize(new Dictionary<string, string> { [longName] = "42" });
            Assert.Null(YamlSerializer.Deserialize<PropertyKeyLengthPoco>(yaml, options).Value);

            yaml = YamlSerializer.Serialize(new Dictionary<string, string> { [shortName] = "42" });
            Assert.Equal("42", YamlSerializer.Deserialize<PropertyKeyLengthPoco>(yaml, options).Value);
        }

        private class PropertyKeyLengthPoco
        {
            // The declared name is irrelevant because the resolver replaces it.
            public string Value { get; set; }
        }

        // Use a common options instance to encourage additional metadata collisions across types. Also since
        // this options is not the default options instance the tests will not use previously cached metadata.
        private static YamlSerializerOptions s_options = new YamlSerializerOptions { IncludeFields = true };

        [Theory]
        [MemberData(nameof(WriteSuccessCases))]
        public static async Task MultipleTypes(ITestClass testObj)
        {
            Type type = testObj.GetType();

            // Get the test json with the default options to avoid cache pollution of Deserialize() below.
            testObj.Initialize();
            testObj.Verify();
            var options = new YamlSerializerOptions { IncludeFields = true };
            string yaml = YamlSerializer.Serialize(testObj, type, options);

            void Serialize()
            {
                ITestClass localTestObj = (ITestClass)Activator.CreateInstance(type);
                localTestObj.Initialize();
                localTestObj.Verify();
                string yaml = YamlSerializer.Serialize(localTestObj, type, s_options);
            };

            void Deserialize()
            {
                ITestClass obj = (ITestClass)YamlSerializer.Deserialize(yaml, type, s_options);
                obj.Verify();
            };

            const int ThreadCount = 12;
            const int ConcurrentTestsCount = 2;
            Task[] tasks = new Task[ThreadCount * ConcurrentTestsCount];

            for (int i = 0; i < tasks.Length; i += ConcurrentTestsCount)
            {
                tasks[i + 0] = Task.Run(() => Deserialize());
                tasks[i + 1] = Task.Run(() => Serialize());
            };

            await Task.WhenAll(tasks);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void YamlSerializerOptionsUpdateHandler_ClearingDoesntPreventSerialization()
        {
            // This test uses reflection to:
            // - Access YamlSerializerOptions._cachingContext.Count
            // - Access YamlSerializerOptionsUpdateHandler.ClearCache
            //
            // If either of them changes, this test will need to be kept in sync.

            RemoteExecutor.Invoke(static () =>
                {
                    var options = new YamlSerializerOptions();

                    Func<YamlSerializerOptions, int> getCount = CreateCacheCountAccessor();

                    Assert.Equal(0, getCount(options));

                    SimpleTestClass testObj = new SimpleTestClass();
                    testObj.Initialize();
                    YamlSerializer.Serialize<SimpleTestClass>(testObj, options);
                    Assert.NotEqual(0, getCount(options));

                    Type updateHandler = typeof(YamlSerializerOptions).Assembly.GetType("System.Text.Yaml.YamlSerializerOptionsUpdateHandler", throwOnError: true, ignoreCase: false);
                    MethodInfo clearCache = updateHandler.GetMethod("ClearCache");
                    Assert.NotNull(clearCache);
                    clearCache.Invoke(null, new object[] { null });
                    Assert.Equal(0, getCount(options));

                    YamlSerializer.Serialize<SimpleTestClass>(testObj, options);
                    Assert.NotEqual(0, getCount(options));
                }, new RemoteInvokeOptions() {
                    TimeOut = RemoteExecutor.FailWaitTimeoutMilliseconds * PlatformDetection.SlowRuntimeTimeoutModifier
                }).Dispose();

            static Func<YamlSerializerOptions, int> CreateCacheCountAccessor()
            {
                FieldInfo cacheField = typeof(YamlSerializerOptions).GetField("_cachingContext", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(cacheField);
                PropertyInfo countProperty = cacheField.FieldType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(countProperty);
                return options =>
                {
                    object? cache = cacheField.GetValue(options);
                    return cache is null ? 0 : (int)countProperty.GetValue(cache);
                };
            }
        }

        [ActiveIssue("https://github.com/dotnet/runtime/issues/66232", TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void YamlSerializerOptions_ReuseConverterCaches()
        {
            // This test uses reflection to:
            // - Access YamlSerializerOptions._cachingContext._options
            // - Access YamlSerializerOptions.EqualityComparer.AreEquivalent
            //
            // If either of them changes, this test will need to be kept in sync.

            RemoteExecutor.Invoke(static () =>
            {
                Func<YamlSerializerOptions, YamlSerializerOptions?> getCacheOptions = CreateCacheOptionsAccessor();
                IEqualityComparer<YamlSerializerOptions> equalityComparer = CreateEqualityComparerAccessor();

                foreach (var args in GetYamlSerializerOptions())
                {
                    var options = (YamlSerializerOptions)args[0];
                    Assert.Null(getCacheOptions(options));

                    YamlSerializer.Serialize(42, options);

                    YamlSerializerOptions originalCacheOptions = getCacheOptions(options);
                    Assert.NotNull(originalCacheOptions);
                    Assert.True(equalityComparer.Equals(options, originalCacheOptions));
                    Assert.Equal(equalityComparer.GetHashCode(options), equalityComparer.GetHashCode(originalCacheOptions));

                    for (int i = 0; i < 5; i++)
                    {
                        var options2 = new YamlSerializerOptions(options);
                        Assert.Null(getCacheOptions(options2));

                        YamlSerializer.Serialize(42, options2);

                        Assert.True(equalityComparer.Equals(options2, originalCacheOptions));
                        Assert.Equal(equalityComparer.GetHashCode(options2), equalityComparer.GetHashCode(originalCacheOptions));
                        Assert.Same(originalCacheOptions, getCacheOptions(options2));
                    }
                }
            }, new RemoteInvokeOptions() {
                TimeOut = RemoteExecutor.FailWaitTimeoutMilliseconds * PlatformDetection.SlowRuntimeTimeoutModifier
            }).Dispose();

            static Func<YamlSerializerOptions, YamlSerializerOptions?> CreateCacheOptionsAccessor()
            {
                FieldInfo cacheField = typeof(YamlSerializerOptions).GetField("_cachingContext", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(cacheField);
                PropertyInfo optionsField = cacheField.FieldType.GetProperty("Options", BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(optionsField);
                return options =>
                {
                    object? cache = cacheField.GetValue(options);
                    return cache is null ? null : (YamlSerializerOptions)optionsField.GetValue(cache);
                };
            }
        }

        public static IEnumerable<object[]> GetYamlSerializerOptions()
        {
            yield return new[] { new YamlSerializerOptions() };
            yield return new[] { new YamlSerializerOptions(YamlSerializerDefaults.Web) };
            yield return new[] { new YamlSerializerOptions { WriteIndented = true } };
            yield return new[] { new YamlSerializerOptions { Converters = { new YamlStringEnumConverter() } } };
        }

        [Fact]
        public static void YamlSerializerOptions_EqualityComparer_ChangingAnySettingShouldReturnFalse()
        {
            // This test uses reflection to:
            // - Access YamlSerializerOptions.EqualityComparer.AreEquivalent
            // - All public setters in YamlSerializerOptions
            //
            // If either of them changes, this test will need to be kept in sync.
            IEqualityComparer<YamlSerializerOptions> equalityComparer = CreateEqualityComparerAccessor();

            (PropertyInfo prop, object value)[] propertySettersAndValues = GetPropertiesWithSettersAndNonDefaultValues().ToArray();

            // Ensure we're testing equality for all YamlSerializerOptions settings
            foreach (PropertyInfo prop in GetAllPublicPropertiesWithSetters().Except(propertySettersAndValues.Select(x => x.prop)))
            {
                Assert.Fail($"{nameof(GetPropertiesWithSettersAndNonDefaultValues)} missing property declaration for {prop.Name}, please update the method.");
            }

            Assert.True(equalityComparer.Equals(YamlSerializerOptions.Default, YamlSerializerOptions.Default));
            Assert.Equal(equalityComparer.GetHashCode(YamlSerializerOptions.Default), equalityComparer.GetHashCode(YamlSerializerOptions.Default));

            foreach ((PropertyInfo prop, object? value) in propertySettersAndValues)
            {
                var options = new YamlSerializerOptions();
                prop.SetValue(options, value);

                Assert.True(equalityComparer.Equals(options, options));
                Assert.Equal(equalityComparer.GetHashCode(options), equalityComparer.GetHashCode(options));

                Assert.False(equalityComparer.Equals(YamlSerializerOptions.Default, options));
                Assert.NotEqual(equalityComparer.GetHashCode(YamlSerializerOptions.Default), equalityComparer.GetHashCode(options));
            }

            static IEnumerable<(PropertyInfo, object)> GetPropertiesWithSettersAndNonDefaultValues()
            {
                yield return (GetProp(nameof(YamlSerializerOptions.AllowOutOfOrderMetadataProperties)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.AllowTrailingCommas)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.DisallowImplicitCollectionKeys)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.InheritTagDirectives)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.AcceptUnityStrippedMarker)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.AcceptLenientScalars)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.MaxAliasExpansion)), 5);
                yield return (GetProp(nameof(YamlSerializerOptions.ResolveMergeKeys)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.DuplicateKeyHandling)), YamlDuplicateKeyHandling.FirstWins);
                yield return (GetProp(nameof(YamlSerializerOptions.Schema)), YamlSchemaKind.Yaml11);
                yield return (GetProp(nameof(YamlSerializerOptions.MappingOrder)), YamlMappingOrder.Sorted); // Styml M5f
                yield return (GetProp(nameof(YamlSerializerOptions.WriteStyle)), YamlWriterStyle.Block);
                yield return (GetProp(nameof(YamlSerializerOptions.IndentSequences)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.MultiLineStringStyle)), YamlScalarStyle.Literal);
                yield return (GetProp(nameof(YamlSerializerOptions.DefaultScalarStyle)), YamlScalarStyle.SingleQuoted);
                yield return (GetProp(nameof(YamlSerializerOptions.IgnoreSourceStyles)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.MaxLineWidth)), 80);
                yield return (GetProp(nameof(YamlSerializerOptions.ExpandSequenceItems)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.QuoteForLenientReaders)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.DefaultBufferSize)), 42);
                yield return (GetProp(nameof(YamlSerializerOptions.Encoder)), JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
                yield return (GetProp(nameof(YamlSerializerOptions.DictionaryKeyPolicy)), YamlNamingPolicy.CamelCase);
                yield return (GetProp(nameof(YamlSerializerOptions.DefaultIgnoreCondition)), YamlIgnoreCondition.WhenWritingDefault);
                yield return (GetProp(nameof(YamlSerializerOptions.NumberHandling)), YamlNumberHandling.AllowReadingFromString);
                yield return (GetProp(nameof(YamlSerializerOptions.PreferredObjectCreationHandling)), YamlObjectCreationHandling.Populate);
                yield return (GetProp(nameof(YamlSerializerOptions.UnmappedMemberHandling)), YamlUnmappedMemberHandling.Disallow);
                yield return (GetProp(nameof(YamlSerializerOptions.IgnoreReadOnlyProperties)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.IgnoreReadOnlyFields)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.IncludeFields)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.MaxDepth)), 11);
                yield return (GetProp(nameof(YamlSerializerOptions.NewLine)), Environment.NewLine.Length is 1 ? "\r\n" : "\n");
                yield return (GetProp(nameof(YamlSerializerOptions.PropertyNamingPolicy)), YamlNamingPolicy.CamelCase);
                yield return (GetProp(nameof(YamlSerializerOptions.PropertyNameCaseInsensitive)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.ReadCommentHandling)), YamlCommentHandling.Skip);
                yield return (GetProp(nameof(YamlSerializerOptions.UnknownTypeHandling)), YamlUnknownTypeHandling.YamlNode);
                yield return (GetProp(nameof(YamlSerializerOptions.WriteIndented)), true);
                yield return (GetProp(nameof(YamlSerializerOptions.RespectNullableAnnotations)), !YamlSerializerOptions.Default.RespectNullableAnnotations);
                yield return (GetProp(nameof(YamlSerializerOptions.RespectRequiredConstructorParameters)), !YamlSerializerOptions.Default.RespectRequiredConstructorParameters);
                yield return (GetProp(nameof(YamlSerializerOptions.IndentCharacter)), '\t');
                yield return (GetProp(nameof(YamlSerializerOptions.IndentSize)), 1);
                yield return (GetProp(nameof(YamlSerializerOptions.ReferenceHandler)), ReferenceHandler.Preserve);
                yield return (GetProp(nameof(YamlSerializerOptions.TypeInfoResolver)), new DefaultYamlTypeInfoResolver());
                yield return (GetProp(nameof(YamlSerializerOptions.AllowDuplicateProperties)), false /* true is default */);
                yield return (GetProp(nameof(YamlSerializerOptions.InferClosedTypePolymorphism)), true);

                static PropertyInfo GetProp(string name)
                {
                    PropertyInfo property = typeof(YamlSerializerOptions).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    Assert.True(property.CanWrite);
                    return property;
                }
            }

            static IEnumerable<PropertyInfo> GetAllPublicPropertiesWithSetters()
                => typeof(YamlSerializerOptions)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite);
        }

        [Fact]
        public static void YamlSerializerOptions_EqualityComparer_ApplyingYamlSerializerContextShouldReturnFalse()
        {
            // This test uses reflection to:
            // - Access YamlSerializerOptions.EqualityComparer
            //
            // If either of them changes, this test will need to be kept in sync.

            IEqualityComparer<YamlSerializerOptions> equalityComparer = CreateEqualityComparerAccessor();
            var options1 = new YamlSerializerOptions { WriteIndented = true };
            var options2 = new YamlSerializerOptions { WriteIndented = true };

            Assert.True(equalityComparer.Equals(options1, options2));
            Assert.Equal(equalityComparer.GetHashCode(options1), equalityComparer.GetHashCode(options2));

            _ = new MyYamlContext(options1); // Associate copy with a YamlSerializerContext
            Assert.False(equalityComparer.Equals(options1, options2));
            Assert.NotEqual(equalityComparer.GetHashCode(options1), equalityComparer.GetHashCode(options2));
        }

        private class MyYamlContext : YamlSerializerContext
        {
            public MyYamlContext(YamlSerializerOptions options) : base(options) { }

            public override YamlTypeInfo? GetTypeInfo(Type _) => null;

            protected override YamlSerializerOptions? GeneratedSerializerOptions => Options;
        }

        public static IEqualityComparer<YamlSerializerOptions> CreateEqualityComparerAccessor()
        {
            Type equalityComparerType = typeof(YamlSerializerOptions).GetNestedType("EqualityComparer", BindingFlags.NonPublic);
            Assert.NotNull(equalityComparerType);
            return (IEqualityComparer<YamlSerializerOptions>)Activator.CreateInstance(equalityComparerType, nonPublic: true);
        }

        public static IEnumerable<object[]> WriteSuccessCases
        {
            get
            {
                return TestData.WriteSuccessCases;
            }
        }
    }
}
