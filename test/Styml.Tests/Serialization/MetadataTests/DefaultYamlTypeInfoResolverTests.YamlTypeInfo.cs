// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Tests;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class DefaultYamlTypeInfoResolverTests
    {
        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(SomeClass))]
        [InlineData(typeof(StructWithFourArgs))]
        [InlineData(typeof(Dictionary<string, int>))]
        [InlineData(typeof(DictionaryWrapper))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(ListWrapper))]
        public static void TypeInfoPropertiesDefaults(Type type)
        {
            bool usingParameterizedConstructor = type.GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters().Length != 0 && ctor.GetCustomAttribute<YamlConstructorAttribute>() != null) != null;

            DefaultYamlTypeInfoResolver r = new();
            YamlSerializerOptions o = new();
            o.Converters.Add(new CustomThrowingConverter<SomeClass>());

            YamlTypeInfo ti = r.GetTypeInfo(type, o);

            Assert.False(ti.IsReadOnly);
            Assert.Same(o, ti.Options);
            Assert.NotNull(ti.Properties);

            if (ti.Kind == YamlTypeInfoKind.Object && usingParameterizedConstructor)
            {
                Assert.Null(ti.CreateObject);
                Func<object> createObj = () => Activator.CreateInstance(type);
                ti.CreateObject = createObj;
                Assert.Same(createObj, ti.CreateObject);
            }
            else if (ti.Kind == YamlTypeInfoKind.None)
            {
                Assert.Null(ti.CreateObject);
                Assert.Throws<InvalidOperationException>(() => ti.CreateObject = () => Activator.CreateInstance(type));
            }
            else
            {
                Assert.NotNull(ti.CreateObject);
                Func<object> createObj = () => Activator.CreateInstance(type);
                ti.CreateObject = createObj;
                Assert.Same(createObj, ti.CreateObject);
            }

            YamlPropertyInfo property = ti.CreateYamlPropertyInfo(typeof(string), "foo");
            Assert.NotNull(property);

            if (ti.Kind == YamlTypeInfoKind.Object)
            {
                Assert.InRange(ti.Properties.Count, 1, 10);
                Assert.False(ti.Properties.IsReadOnly);
                ti.Properties.Add(property);
                ti.Properties.Remove(property);
            }
            else
            {
                Assert.Equal(0, ti.Properties.Count);
                Assert.True(ti.Properties.IsReadOnly);
                Assert.Throws<InvalidOperationException>(() => ti.Properties.Add(property));
                Assert.Throws<InvalidOperationException>(() => ti.Properties.Insert(0, property));
                Assert.Throws<InvalidOperationException>(() => ti.Properties.Clear());
            }

            Assert.Null(ti.NumberHandling);
            YamlNumberHandling numberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString;
            ti.NumberHandling = numberHandling;
            Assert.Equal(numberHandling, ti.NumberHandling);

            InvokeGeneric(type, nameof(TypeInfoPropertiesDefaults_Generic), ti);
        }

        private static void TypeInfoPropertiesDefaults_Generic<T>(YamlTypeInfo<T> ti)
        {
            if (ti.Kind == YamlTypeInfoKind.None)
            {
                Assert.Null(ti.CreateObject);
                Assert.Throws<InvalidOperationException>(() => ti.CreateObject = () => (T)Activator.CreateInstance(typeof(T)));
            }
            else
            {
                bool createObjCalled = false;
                Assert.NotNull(ti.CreateObject);
                Func<T> createObj = () =>
                {
                    createObjCalled = true;
                    return default(T);
                };

                ti.CreateObject = createObj;
                Assert.Same(createObj, ti.CreateObject);

                YamlTypeInfo untyped = ti;
                if (typeof(T).IsValueType)
                {
                    Assert.NotSame(createObj, untyped.CreateObject);
                }
                else
                {
                    Assert.Same(createObj, untyped.CreateObject);
                }

                Assert.Same(untyped.CreateObject, untyped.CreateObject);
                Assert.Same(createObj, ti.CreateObject);
                untyped.CreateObject();
                Assert.True(createObjCalled);

                ti.CreateObject = null;
                Assert.Null(ti.CreateObject);
                Assert.Null(untyped.CreateObject);

                bool untypedCreateObjCalled = false;
                Func<object> untypedCreateObj = () =>
                {
                    untypedCreateObjCalled = true;
                    return default(T);
                };
                untyped.CreateObject = untypedCreateObj;
                Assert.Same(untypedCreateObj, untyped.CreateObject);
                Assert.Same(ti.CreateObject, ti.CreateObject);
                Assert.NotSame(untypedCreateObj, ti.CreateObject);

                ti.CreateObject();
                Assert.True(untypedCreateObjCalled);

                untyped.CreateObject = null;
                Assert.Null(ti.CreateObject);
                Assert.Null(untyped.CreateObject);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void TypeInfoKindNoneNumberHandlingDirect()
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(int))
                {
                    ti.NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            string yaml = YamlSerializer.Serialize(13, o);
            Assert.Equal("""
                "13"
                """, yaml);

            var deserialized = YamlSerializer.Deserialize<int>(yaml, o);
            Assert.Equal(13, deserialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void TypeInfoKindNoneNumberHandlingDirectThroughObject()
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(int))
                {
                    ti.NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            string yaml = YamlSerializer.Serialize<object>(13, o);
            Assert.Equal("""
                "13"
                """, yaml);

            var deserialized = YamlSerializer.Deserialize<object>(yaml, o);
            Assert.Equal("13", ((YamlElement)deserialized).GetString());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void TypeInfoKindNoneNumberHandling()
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(int) || ti.Type == typeof(object))
                {
                    ti.NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            SomeClass testObj = new SomeClass()
            {
                ObjProp = 45,
                IntProp = 13,
            };

            string yaml = YamlSerializer.Serialize(testObj, o);
            Assert.Equal("""{"ObjProp":"45","IntProp":"13"}""", yaml);

            var deserialized = YamlSerializer.Deserialize<SomeClass>(yaml, o);
            Assert.Equal(testObj.ObjProp.ToString(), ((YamlElement)deserialized.ObjProp).GetString());
            Assert.Equal(testObj.IntProp, deserialized.IntProp);
        }

        [Theory]
        [InlineData((YamlNumberHandling)(-1))]
        [InlineData((YamlNumberHandling)8)]
        [InlineData((YamlNumberHandling)int.MaxValue)]
        public static void NumberHandling_SetInvalidValue_ThrowsArgumentOutOfRangeException(YamlNumberHandling handling)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new());
            Assert.Throws<ArgumentOutOfRangeException>(() => yamlTypeInfo.NumberHandling = handling);
        }

        [Theory]
        [InlineData(typeof(List<int>), YamlTypeInfoKind.Enumerable)]
        [InlineData(typeof(Dictionary<string, int>), YamlTypeInfoKind.Dictionary)]
        [InlineData(typeof(object), YamlTypeInfoKind.None)]
        [InlineData(typeof(string), YamlTypeInfoKind.None)]
        public static void AddingPropertyToNonObjectYamlTypeInfoKindThrows(Type type, YamlTypeInfoKind expectedKind)
        {
            YamlSerializerOptions options = new();
            DefaultYamlTypeInfoResolver resolver = new();
            YamlTypeInfo typeInfo = resolver.GetTypeInfo(type, options);
            Assert.Equal(expectedKind, typeInfo.Kind);

            YamlPropertyInfo property = typeInfo.CreateYamlPropertyInfo(typeof(int), "test");
            Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Add(property));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void RecursiveTypeNumberHandling()
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(SomeRecursiveClass))
                {
                    ti.NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowReadingFromString;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            SomeRecursiveClass testObj = new SomeRecursiveClass()
            {
                IntProp = 13,
                RecursiveProperty = new SomeRecursiveClass()
                {
                    IntProp = 14,
                },
            };

            string yaml = YamlSerializer.Serialize(testObj, o);
            Assert.Equal("""{"IntProp":"13","RecursiveProperty":{"IntProp":"14","RecursiveProperty":null}}""", yaml);

            var deserialized = YamlSerializer.Deserialize<SomeRecursiveClass>(yaml, o);
            Assert.Equal(testObj.IntProp, deserialized.IntProp);
            Assert.NotNull(testObj.RecursiveProperty);
            Assert.Equal(testObj.RecursiveProperty.IntProp, deserialized.RecursiveProperty.IntProp);
            Assert.Null(testObj.RecursiveProperty.RecursiveProperty);
        }

        [Theory]
        [InlineData(typeof(SomeClass), typeof(object))]
        [InlineData(typeof(object), typeof(string))]
        [InlineData(typeof(object), typeof(int))]
        [InlineData(typeof(string), typeof(int))]
        [InlineData(typeof(int), typeof(string))]
        [InlineData(typeof(int), typeof(double))]
        public static void TypeInfoOfWrongTypeOnObject(Type expectedType, Type actualType)
        {
            DefaultYamlTypeInfoResolver dr = new();
            TestResolver r = new((type, options) =>
            {
                if (type == expectedType)
                {
                    return dr.GetTypeInfo(actualType, options);
                }

                return dr.GetTypeInfo(type, options);
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            SomeClass testObj = new()
            {
                ObjProp = "test",
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(testObj, o));
        }

        [Fact]
        public static void TypeInfoOfWrongOptions()
        {
            YamlSerializerOptions wrongOptions = new();
            DefaultYamlTypeInfoResolver dr = new();
            TestResolver r = new((type, options) =>
            {
                if (type == typeof(int))
                {
                    return dr.GetTypeInfo(type, wrongOptions);
                }

                return dr.GetTypeInfo(type, options);
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            SomeClass testObj = new()
            {
                IntProp = 17,
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(testObj, o));
        }

        [Theory]
        [InlineData(typeof(SomeClass), typeof(object))]
        [InlineData(typeof(object), typeof(string))]
        [InlineData(typeof(object), typeof(int))]
        [InlineData(typeof(int), typeof(string))]
        [InlineData(typeof(int), typeof(double))]
        public static void TypeInfoOfWrongTypeDirectCall(Type expectedType, Type actualType)
        {
            DefaultYamlTypeInfoResolver dr = new();
            TestResolver r = new((type, options) =>
            {
                if (type == expectedType)
                {
                    return dr.GetTypeInfo(actualType, options);
                }

                return dr.GetTypeInfo(type, options);
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            object testObj = Activator.CreateInstance(expectedType);

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(testObj, expectedType, o));
        }

        [Theory]
        [MemberData(nameof(GetTypeInfoTestData))]
        public static void TypeInfoIsImmutableAfterFirstUsage<T>(T testObj)
        {
            YamlTypeInfo untyped = null;
            DefaultYamlTypeInfoResolver dr = new();
            TestResolver r = new((typeToResolve, options) =>
            {
                var ret = dr.GetTypeInfo(typeToResolve, options);
                if (typeToResolve == typeof(T))
                {
                    Assert.Null(untyped);
                    untyped = ret;
                }

                return ret;
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            Assert.NotNull(YamlSerializer.Serialize(testObj, typeof(T), o));
            Assert.NotNull(untyped);

            YamlTypeInfo<T> typeInfo = (YamlTypeInfo<T>)untyped;

            if (typeInfo.Kind == YamlTypeInfoKind.None)
            {
                Assert.Null(typeInfo.CreateObject);
                Assert.Null(untyped.CreateObject);
            }
            else
            {
                Assert.NotNull(typeInfo.CreateObject);
                Assert.NotNull(untyped.CreateObject);
            }

            Assert.Null(typeInfo.NumberHandling);

            TestTypeInfoImmutability(typeInfo);
        }

        private static void TestTypeInfoImmutability<T>(YamlTypeInfo<T> typeInfo)
        {
            YamlTypeInfo untyped = typeInfo;
            Assert.Equal(typeof(T), typeInfo.Type);
            Assert.True(typeInfo.Converter.CanConvert(typeof(T)));

            Assert.True(typeInfo.IsReadOnly);
            Assert.True(typeInfo.Properties.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => typeInfo.CreateYamlPropertyInfo(typeof(string), "foo"));
            Assert.Throws<InvalidOperationException>(() => untyped.CreateObject = untyped.CreateObject);
            Assert.Throws<InvalidOperationException>(() => typeInfo.CreateObject = typeInfo.CreateObject);
            Assert.Throws<InvalidOperationException>(() => typeInfo.NumberHandling = typeInfo.NumberHandling);
            Assert.Throws<InvalidOperationException>(() => typeInfo.CreateYamlPropertyInfo(typeof(string), "foo"));
            Assert.Throws<InvalidOperationException>(() => typeInfo.UnmappedMemberHandling = typeInfo.UnmappedMemberHandling);
            Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Clear());
            Assert.Throws<InvalidOperationException>(() => typeInfo.PolymorphismOptions = null);
            Assert.Throws<InvalidOperationException>(() => typeInfo.PolymorphismOptions = new());
            Assert.Throws<InvalidOperationException>(() => typeInfo.PreferredPropertyObjectCreationHandling = null);
            Assert.Throws<InvalidOperationException>(() => typeInfo.OriginatingResolver = new DefaultYamlTypeInfoResolver());

            if (typeInfo.Properties.Count > 0)
            {
                YamlPropertyInfo prop = typeInfo.Properties[0];
                Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Add(prop));
                Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.Insert(0, prop));
                Assert.Throws<InvalidOperationException>(() => typeInfo.Properties.RemoveAt(0));
            }

            if (typeInfo.PolymorphismOptions is YamlPolymorphismOptions jpo)
            {
                Assert.True(jpo.DerivedTypes.IsReadOnly);
                Assert.Throws<InvalidOperationException>(() => jpo.IgnoreUnrecognizedTypeDiscriminators = true);
                Assert.Throws<InvalidOperationException>(() => jpo.TypeDiscriminatorPropertyName = "__case");
                Assert.Throws<InvalidOperationException>(() => jpo.UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToNearestAncestor);
                Assert.Throws<InvalidOperationException>(() => jpo.DerivedTypes.Clear());
                Assert.Throws<InvalidOperationException>(() => jpo.DerivedTypes.Add(default));
                Assert.Throws<InvalidOperationException>(() => jpo.DerivedTypes.Insert(0, default));
            }

            foreach (YamlPropertyInfo property in typeInfo.Properties)
            {
                Assert.NotNull(property.PropertyType);
                Assert.Null(property.CustomConverter);
                Assert.NotNull(property.Name);
                Assert.NotNull(property.Get);
                Assert.NotNull(property.Set);
                Assert.Null(property.ShouldSerialize);
                Assert.Null(typeInfo.NumberHandling);

                foreach (PropertyInfo propertyInfo in typeof(YamlPropertyInfo).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    // We don't have any set only properties, if this ever changes we will need to update this code
                    if (propertyInfo.GetSetMethod() == null)
                    {
                        continue;
                    }

                    TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => propertyInfo.SetValue(property, propertyInfo.GetValue(property)));
                    Assert.NotNull(exception.InnerException);
                    Assert.IsType<InvalidOperationException>(exception.InnerException);
                }

                Assert.Throws<InvalidOperationException>(() => property.Name = null);
                Assert.Throws<InvalidOperationException>(() => property.ShouldSerialize = null);
                Assert.Throws<InvalidOperationException>(() => property.Get = null);
                Assert.Throws<InvalidOperationException>(() => property.Set = null);
                Assert.Throws<InvalidOperationException>(() => property.ObjectCreationHandling = null);
                Assert.Throws<InvalidOperationException>(() => property.IsExtensionData = true);
                Assert.Throws<InvalidOperationException>(() => property.IsRequired = true);
            }
        }

        [Theory]
        [InlineData(typeof(ICollection<int>), typeof(List<int>), false)]
        [InlineData(typeof(IList), typeof(List<int>), false)]
        [InlineData(typeof(IList<int>), typeof(List<int>), false)]
        [InlineData(typeof(List<int>), typeof(List<int>), false)]
        [InlineData(typeof(ISet<int>), typeof(HashSet<int>), false)]
        [InlineData(typeof(Queue<int>), typeof(Queue<int>), false)]
        [InlineData(typeof(Stack<int>), typeof(Stack<int>), false)]
        [InlineData(typeof(IDictionary), typeof(Hashtable), true)]
        [InlineData(typeof(IDictionary<string, int>), typeof(ConcurrentDictionary<string, int>), true)]
        public static void YamlTypeInfo_CreateObject_SupportedCollection(Type collectionType, Type runtimeType, bool isDictionary)
        {
            bool isDelegateInvoked = false;

            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        yamlTypeInfo =>
                        {
                            if (yamlTypeInfo.Type == collectionType)
                            {
                                yamlTypeInfo.CreateObject = () =>
                                {
                                    isDelegateInvoked = true;
                                    return Activator.CreateInstance(runtimeType);
                                };
                            }
                        }
                    }
                }
            };

            string yaml = isDictionary ? "{}" : "[]";
            object result = YamlSerializer.Deserialize(yaml, collectionType, options);
            Assert.IsType(runtimeType, result);
            Assert.True(isDelegateInvoked);
        }

        [Theory]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(ImmutableArray<int>))]
        [InlineData(typeof(IEnumerable))]
        [InlineData(typeof(IEnumerable<int>))]
        [InlineData(typeof(IAsyncEnumerable<int>))]
        [InlineData(typeof(IReadOnlyDictionary<string, int>))]
        [InlineData(typeof(ImmutableDictionary<string, int>))]
        public static void YamlTypeInfo_CreateObject_UnsupportedCollection_ThrowsInvalidOperationException(Type collectionType)
        {
            var options = new YamlSerializerOptions();
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(collectionType, options);
            Assert.Throws<InvalidOperationException>(() => yamlTypeInfo.CreateObject = () => new object());
        }

        [Theory]
        [InlineData(typeof(object), YamlTypeInfoKind.None)]
        [InlineData(typeof(string), YamlTypeInfoKind.None)]
        [InlineData(typeof(int), YamlTypeInfoKind.None)]
        [InlineData(typeof(SomeRecursiveClass) /* custom converter */, YamlTypeInfoKind.None)]
        [InlineData(typeof(SomeClass), YamlTypeInfoKind.Object)]
        [InlineData(typeof(DefaultYamlTypeInfoResolverTests), YamlTypeInfoKind.Object)]
        [InlineData(typeof(StructWithFourArgs), YamlTypeInfoKind.Object)]
        [InlineData(typeof(Dictionary<string, string>), YamlTypeInfoKind.Dictionary)]
        [InlineData(typeof(DictionaryWrapper), YamlTypeInfoKind.Dictionary)]
        [InlineData(typeof(List<int>), YamlTypeInfoKind.Enumerable)]
        [InlineData(typeof(ListWrapper), YamlTypeInfoKind.Enumerable)]
        [InlineData(typeof(int[]), YamlTypeInfoKind.Enumerable)]
        public static void YamlTypeInfoKindIsReportedCorrectly(Type type, YamlTypeInfoKind expectedYamlTypeInfoKind)
        {
            InvokeGeneric(type, nameof(YamlTypeInfoKindIsReportedCorrectly_Generic), expectedYamlTypeInfoKind);
        }

        private static void YamlTypeInfoKindIsReportedCorrectly_Generic<T>(YamlTypeInfoKind expectedYamlTypeInfoKind)
        {
            DefaultYamlTypeInfoResolver r = new();
            YamlSerializerOptions o = new();
            o.Converters.Add(new CustomThrowingConverter<SomeRecursiveClass>());
            YamlTypeInfo ti = r.GetTypeInfo(typeof(T), o);
            Assert.Equal(expectedYamlTypeInfoKind, ti.Kind);

            ti = YamlTypeInfo.CreateYamlTypeInfo(typeof(T), o);
            Assert.Equal(expectedYamlTypeInfoKind, ti.Kind);

            ti = YamlTypeInfo.CreateYamlTypeInfo<T>(o);
            Assert.Equal(expectedYamlTypeInfoKind, ti.Kind);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void YamlTypeInfoAddDuplicatedPropertyNames(bool ignoreDuplicatedProperty)
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(MyClass))
                {
                    YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(uint), ti.Properties[0].Name);
                    uint valueHolder = 7;

                    if (!ignoreDuplicatedProperty)
                    {
                        prop.Get = (o) => valueHolder;
                        prop.Set = (o, val) => valueHolder = (uint)val;
                    }

                    ti.Properties.Add(prop);
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            MyClass obj = new()
            {
                Value = "foo",
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize<MyClass>(obj, o));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void YamlTypeInfoRenameToDuplicatePropertyNames(bool ignoreDuplicatedProperty)
        {
            DefaultYamlTypeInfoResolver r = new();
            r.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(MyClass))
                {
                    if (ignoreDuplicatedProperty)
                    {
                        ti.Properties[1].Get = null;
                        ti.Properties[1].Set = null;
                    }

                    ti.Properties[1].Name = ti.Properties[0].Name;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;

            MyClass obj = new()
            {
                Value = "foo",
            };

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize<MyClass>(obj, o));
        }

        [Fact]
        public static void AddYamlPropertyInfoCreatedFromDifferentYamlTypeInfoInstance()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            YamlSerializerOptions options = new();
            YamlTypeInfo[] typeInfos = new[]
            {
                // we add double so that we check between instances of the same internal type as well
                YamlTypeInfo.CreateYamlTypeInfo<SomeClass>(options),
                YamlTypeInfo.CreateYamlTypeInfo<SomeClass>(options),
                YamlTypeInfo.CreateYamlTypeInfo<SomeOtherClass>(options),
                resolver.GetTypeInfo(typeof(SomeClass), options),
                resolver.GetTypeInfo(typeof(SomeClass), options),
                resolver.GetTypeInfo(typeof(SomeOtherClass), options),
                ((IYamlTypeInfoResolver)new SomeClassContext()).GetTypeInfo(typeof(SomeClass), options),
                ((IYamlTypeInfoResolver)new SomeClassContext()).GetTypeInfo(typeof(SomeClass), options),
                ((IYamlTypeInfoResolver)new SomeClassContext()).GetTypeInfo(typeof(SomeOtherClass), options),
                new SomeClassContext(options).SomeClass // this binds to options and therefore we cannot add more of these
            };

            foreach (var typeInfo1 in typeInfos)
            {
                foreach (var typeInfo2 in typeInfos)
                {
                    if (ReferenceEquals(typeInfo1, typeInfo2))
                        continue;

                    Assert.Throws<InvalidOperationException>(() => typeInfo1.Properties.Add(typeInfo2.CreateYamlPropertyInfo(typeof(int), "test")));
                }
            }
        }

        [Fact]
        public static void AddYamlPropertyInfoFromMetadataServices()
        {
            YamlSerializerOptions options = new();
            YamlTypeInfo typeInfo1 = YamlTypeInfo.CreateYamlTypeInfo<SomeClass>(options);
            YamlTypeInfo typeInfo2 = YamlTypeInfo.CreateYamlTypeInfo<SomeClass>(options);

            YamlPropertyInfo propertyInfo = YamlMetadataServices.CreatePropertyInfo<int>(
                options,
                new YamlPropertyInfoValues<int>()
                {
                    DeclaringType = typeof(SomeClass),
                    PropertyName = "test",
                });

            typeInfo1.Properties.Add(propertyInfo);
            Assert.Equal(1, typeInfo1.Properties.Count);
            Assert.Same(propertyInfo, typeInfo1.Properties[0]);

            Assert.Throws<InvalidOperationException>(() => typeInfo2.Properties.Add(propertyInfo));
            Assert.Equal(0, typeInfo2.Properties.Count);
        }

        [Fact]
        public static void AddRecursiveYamlPropertyInfoFromMetadataServices()
        {
            YamlSerializerOptions options = new()
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        static typeInfo =>
                        {
                            if (typeInfo.Type != typeof(SomeClass))
                            {
                                return;
                            }

                            typeInfo.Properties.Clear();
                            YamlPropertyInfo propertyInfo = YamlMetadataServices.CreatePropertyInfo<SomeClass>(
                                typeInfo.Options,
                                new YamlPropertyInfoValues<SomeClass>()
                                {
                                    DeclaringType = typeof(SomeClass),
                                    PropertyName = "Next",
                                });

                            typeInfo.Properties.Add(propertyInfo);
                            Assert.Equal(YamlTypeInfoKind.Object, typeInfo.Kind);
                        }
                    }
                }
            };

            YamlTypeInfo<SomeClass> yamlTypeInfo = Assert.IsAssignableFrom<YamlTypeInfo<SomeClass>>(options.GetTypeInfo(typeof(SomeClass)));
            Assert.Equal(1, yamlTypeInfo.Properties.Count);

            YamlPropertyInfo propertyInfo = yamlTypeInfo.Properties[0];
            Assert.Equal(typeof(SomeClass), propertyInfo.PropertyType);
            Assert.Equal("Next", propertyInfo.Name);
        }

        [Fact]
        public static void AddingNullYamlPropertyInfoIsNotPossible()
        {
            YamlSerializerOptions options = new();
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo<SomeClass>(options);
            Assert.Throws<ArgumentNullException>(() => typeInfo.Properties.Add(null));
            Assert.Empty(typeInfo.Properties);
            Assert.Throws<ArgumentNullException>(() => typeInfo.Properties.Insert(0, null));
            Assert.Empty(typeInfo.Properties);

            typeInfo.Properties.Add(typeInfo.CreateYamlPropertyInfo(typeof(int), "test"));
            Assert.Throws<ArgumentNullException>(() => typeInfo.Properties[0] = null);
            Assert.Equal(1, typeInfo.Properties.Count);
            Assert.NotNull(typeInfo.Properties[0]);
        }

        [Fact]
        public static void CreateYamlTypeInfoWithNullArgumentsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => YamlTypeInfo.CreateYamlTypeInfo(null, new YamlSerializerOptions()));
            Assert.Throws<ArgumentNullException>(() => YamlTypeInfo.CreateYamlTypeInfo(typeof(string), null));
            Assert.Throws<ArgumentNullException>(() => YamlTypeInfo.CreateYamlTypeInfo(null, null));
            Assert.Throws<ArgumentNullException>(() => YamlTypeInfo.CreateYamlTypeInfo<string>(null));
        }

        [Theory]
        [InlineData(typeof(void))]
        [InlineData(typeof(Dictionary<,>))]
        [InlineData(typeof(List<>))]
        [InlineData(typeof(Nullable<>))]
        [InlineData(typeof(int*))]
        [InlineData(typeof(RefStruct))]
        public static void CreateYamlTypeInfoWithInappropriateTypeThrows(Type type)
        {
            Assert.Throws<ArgumentException>(() => YamlTypeInfo.CreateYamlTypeInfo(type, new YamlSerializerOptions()));
        }

        ref struct RefStruct
        {
            public int Foo { get; set; }
        }

        [Fact]
        public static void CreateYamlTypeInfo_ThrowingConverterFactory_DoesNotWrapException()
        {
            var options = new YamlSerializerOptions { Converters = { new ClassWithThrowingConverterFactory.Converter() } };
            // Should not be wrapped in TargetInvocationException.
            Assert.Throws<NotFiniteNumberException>(() => YamlTypeInfo.CreateYamlTypeInfo(typeof(ClassWithThrowingConverterFactory), options));
        }

        public class ClassWithThrowingConverterFactory
        {
            public class Converter : YamlConverterFactory
            {
                public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(ClassWithThrowingConverterFactory);
                public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options) => throw new NotFiniteNumberException();
            }
        }

        [Fact]
        public static void CreateYamlPropertyInfoWithNullArgumentsThrows()
        {
            YamlTypeInfo ti = YamlTypeInfo.CreateYamlTypeInfo<MyClass>(new YamlSerializerOptions());
            Assert.Throws<ArgumentNullException>(() => ti.CreateYamlPropertyInfo(null, "test"));
            Assert.Throws<ArgumentNullException>(() => ti.CreateYamlPropertyInfo(typeof(string), null));
            Assert.Throws<ArgumentNullException>(() => ti.CreateYamlPropertyInfo(null, null));
        }

        [Theory]
        [InlineData(typeof(void))]
        [InlineData(typeof(Dictionary<,>))]
        [InlineData(typeof(List<>))]
        [InlineData(typeof(Nullable<>))]
        [InlineData(typeof(int*))]
        [InlineData(typeof(RefStruct))]
        public static void CreateYamlPropertyInfoWithInappropriateTypeThrows(Type type)
        {
            YamlTypeInfo ti = YamlTypeInfo.CreateYamlTypeInfo<MyClass>(new YamlSerializerOptions());
            Assert.Throws<ArgumentException>(() => ti.CreateYamlPropertyInfo(type, "test"));
        }

        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(SomeRecursiveClass))]
        [InlineData(typeof(SomeClass))]
        [InlineData(typeof(DefaultYamlTypeInfoResolverTests))]
        [InlineData(typeof(StructWithFourArgs))]
        [InlineData(typeof(Dictionary<string, string>))]
        [InlineData(typeof(DictionaryWrapper))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(ListWrapper))]
        [InlineData(typeof(int[]))]
        public static void CreateYamlTypeInfo(Type type)
        {
            InvokeGeneric(type, nameof(CreateYamlTypeInfo_Generic));
        }

        private static void CreateYamlTypeInfo_Generic<T>()
        {
            TestCreateYamlTypeInfo((o) => (YamlTypeInfo<T>)YamlTypeInfo.CreateYamlTypeInfo(typeof(T), o));
            TestCreateYamlTypeInfo((o) => YamlTypeInfo.CreateYamlTypeInfo<T>(o));

            static void TestCreateYamlTypeInfo(Func<YamlSerializerOptions, YamlTypeInfo<T>> getTypeInfo)
            {
                YamlSerializerOptions o = new() { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
                TestCreateYamlTypeInfoInstance(o, getTypeInfo(o));

                o = new YamlSerializerOptions() { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
                var conv = new DummyConverter<T>();
                o.Converters.Add(conv);
                YamlTypeInfo<T> ti = getTypeInfo(o);
                Assert.Same(conv, ti.Converter);
                Assert.Equal(YamlTypeInfoKind.None, ti.Kind);
                TestCreateYamlTypeInfoInstance(o, ti);
            }

            static void TestCreateYamlTypeInfoInstance(YamlSerializerOptions o, YamlTypeInfo<T> ti)
            {
                Assert.Equal(typeof(T), ti.Type);
                Assert.NotNull(ti.Converter);
                Assert.True(ti.Converter.CanConvert(typeof(T)));

                YamlSerializer.Serialize(default(T), ti);

                YamlTypeInfo untyped = ti;
                Assert.Null(ti.CreateObject);
                Assert.Null(untyped.CreateObject);

                TestTypeInfoImmutability(ti);
            }
        }

        public static IEnumerable<object[]> GetTypeInfoTestData()
        {
            yield return new object[] { "test" };
            yield return new object[] { 13 };
            yield return new object[] { new SomeClass { IntProp = 17 } };
            yield return new object[] { new SomeRecursiveClass() };
            yield return new object[] { new SomePolymorphicClass() };
        }

        [Fact]
        public static void YamlConstructorAttributeIsOverriddenWhenCreateObjectIsSet()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(ClassWithParameterizedConstructorAndReadOnlyProperties))
                {
                    Assert.Null(ti.CreateObject);
                    ti.CreateObject = () => new ClassWithParameterizedConstructorAndReadOnlyProperties(1, "test", dummyParam: true);
                }
            });

            YamlSerializerOptions o = new() { TypeInfoResolver = resolver };
            string yaml = """{"A":2,"B":"foo"}""";
            var deserialized = YamlSerializer.Deserialize<ClassWithParameterizedConstructorAndReadOnlyProperties>(yaml, o);

            Assert.NotNull(deserialized);
            Assert.Equal(1, deserialized.A);
            Assert.Equal("test", deserialized.B);
        }

        private class ClassWithParameterizedConstructorAndReadOnlyProperties
        {
            public int A { get; }
            public string B { get; }

            public ClassWithParameterizedConstructorAndReadOnlyProperties(int a, string b, bool dummyParam)
            {
                A = a;
                B = b;
            }

            [YamlConstructor]
            public ClassWithParameterizedConstructorAndReadOnlyProperties(int a, string b)
            {
                Assert.Fail("this ctor should not be used");
            }
        }

        [Fact]
        public static void YamlConstructorAttributeIsOverriddenAndPropertiesAreSetWhenCreateObjectIsSet()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(ClassWithParameterizedConstructorAndWritableProperties))
                {
                    Assert.Null(ti.CreateObject);
                    ti.CreateObject = () => new ClassWithParameterizedConstructorAndWritableProperties();
                }
            });

            YamlSerializerOptions o = new() { TypeInfoResolver = resolver };

            string yaml = """{"A":2,"B":"foo","C":"bar"}""";
            var deserialized = YamlSerializer.Deserialize<ClassWithParameterizedConstructorAndWritableProperties>(yaml, o);

            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.A);
            Assert.Equal("foo", deserialized.B);
            Assert.Equal("bar", deserialized.C);
        }

        private class ClassWithParameterizedConstructorAndWritableProperties
        {
            public int A { get; set; }
            public string B { get; set; }
            public string C { get; set; }

            public ClassWithParameterizedConstructorAndWritableProperties() { }

            [YamlConstructor]
            public ClassWithParameterizedConstructorAndWritableProperties(int a, string b)
            {
                Assert.Fail("this ctor should not be used");
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializingTypeWithCustomNonSerializablePropertyAndYamlConstructorWorksCorrectly()
        {
            var resolver = new DefaultYamlTypeInfoResolver { Modifiers = { ContractModifier } };
            var options = new YamlSerializerOptions { TypeInfoResolver = resolver };
            string yaml = YamlSerializer.Serialize(new PocoWithConstructor("str"), options);
            Assert.Equal("{}", yaml);

            static void ContractModifier(YamlTypeInfo jti)
            {
                if (jti.Type == typeof(PocoWithConstructor))
                {
                    jti.Properties.Add(jti.CreateYamlPropertyInfo(typeof(string), "someOtherName"));
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializingTypeWithCustomSerializablePropertyAndYamlConstructorWorksCorrectly()
        {
            var resolver = new DefaultYamlTypeInfoResolver { Modifiers = { ContractModifier } };
            var options = new YamlSerializerOptions { TypeInfoResolver = resolver };
            string yaml = YamlSerializer.Serialize(new PocoWithConstructor("str"), options);
            Assert.Equal("""{"test":"asd"}""", yaml);

            static void ContractModifier(YamlTypeInfo jti)
            {
                if (jti.Type == typeof(PocoWithConstructor))
                {
                    YamlPropertyInfo pi = jti.CreateYamlPropertyInfo(typeof(string), "test");
                    pi.Get = (o) => "asd";
                    jti.Properties.Add(pi);
                }
            }
        }

        [Fact]
        public static void SerializingTypeWithCustomPropertyAndYamlConstructorBindsParameter()
        {
            var resolver = new DefaultYamlTypeInfoResolver { Modifiers = { ContractModifier } };
            var options = new YamlSerializerOptions { TypeInfoResolver = resolver };
            string yaml = """{"parameter":"asd"}""";
            PocoWithConstructor deserialized = YamlSerializer.Deserialize<PocoWithConstructor>(yaml, options);
            Assert.Equal("asd", deserialized.ParameterValue);

            static void ContractModifier(YamlTypeInfo jti)
            {
                if (jti.Type == typeof(PocoWithConstructor))
                {
                    jti.Properties.Add(jti.CreateYamlPropertyInfo(typeof(string), "parameter"));
                }
            }
        }

        private class PocoWithConstructor
        {
            internal string ParameterValue { get; set; }

            public PocoWithConstructor(string parameter)
            {
                ParameterValue = parameter;
            }
        }

        [Fact]
        public static void YamlConstructorAttributeIsOverriddenAndPropertiesAreSetWhenCreateObjectIsSet_LargeConstructor()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(ClassWithLargeParameterizedConstructor))
                {
                    Assert.Null(ti.CreateObject);
                    ti.CreateObject = () => new ClassWithLargeParameterizedConstructor();
                }
            });

            YamlSerializerOptions o = new() { TypeInfoResolver = resolver };

            string yaml = """{"A":2,"B":"foo","C":"bar","E":true}""";
            var deserialized = YamlSerializer.Deserialize<ClassWithLargeParameterizedConstructor>(yaml, o);

            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.A);
            Assert.Equal("foo", deserialized.B);
            Assert.Equal("bar", deserialized.C);
            Assert.True(deserialized.E);
        }

        [Theory]
        [InlineData(typeof(ICollection<string>))]
        [InlineData(typeof(IList))]
        [InlineData(typeof(IList<bool>))]
        [InlineData(typeof(IDictionary))]
        [InlineData(typeof(IDictionary<string, bool>))]
        [InlineData(typeof(ISet<Guid>))]
        public static void AbstractCollectionMetadata_SurfacesCreateObjectWhereApplicable(Type type)
        {
            var options = new YamlSerializerOptions();
            var resolver = new DefaultYamlTypeInfoResolver();

            YamlTypeInfo metadata = resolver.GetTypeInfo(type, options);
            Assert.NotNull(metadata.CreateObject);
            Assert.IsAssignableFrom(type, metadata.CreateObject());
        }

        private class ClassWithLargeParameterizedConstructor
        {
            public int A { get; set; }
            public string B { get; set; }
            public string C { get; set; }
            public string D { get; set; }
            public bool E { get; set; }
            public int F { get; set; }

            public ClassWithLargeParameterizedConstructor() { }

            [YamlConstructor]
            public ClassWithLargeParameterizedConstructor(int a, string b, string c, string d, bool e, int f)
            {
                Assert.Fail("this ctor should not be used");
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void PropertyOrderIsRespected()
        {
            YamlSerializerOptions options = new()
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver()
                {
                    Modifiers =
                    {
                        ti =>
                        {
                            if (ti.Type == typeof(ClassWithExplicitOrderOfProperties))
                            {
                                Assert.Equal(5, ti.Properties.Count);

                                Assert.Equal("A", ti.Properties[0].Name);
                                Assert.Equal("B", ti.Properties[1].Name);
                                Assert.Equal("C", ti.Properties[2].Name);
                                Assert.Equal("D", ti.Properties[3].Name);
                                Assert.Equal("E", ti.Properties[4].Name);

                                Assert.Equal(-2, ti.Properties[0].Order);
                                Assert.Equal(-1, ti.Properties[1].Order);
                                Assert.Equal(0, ti.Properties[2].Order);
                                Assert.Equal(1, ti.Properties[3].Order);
                                Assert.Equal(2, ti.Properties[4].Order);

                                // swapping A,B order values
                                (ti.Properties[0].Order, ti.Properties[1].Order) = (ti.Properties[1].Order, ti.Properties[0].Order);

                                // swapping E,C order values
                                (ti.Properties[2].Order, ti.Properties[4].Order) = (ti.Properties[4].Order, ti.Properties[2].Order);

                                // swapping B,D properties (has no effect on contract)
                                (ti.Properties[1], ti.Properties[3]) = (ti.Properties[3], ti.Properties[1]);
                            }
                        }
                    }
                }
            };

            ClassWithExplicitOrderOfProperties obj = new()
            {
                A = "a",
                B = "b",
                C = "c",
                D = "d",
                E = "e",
            };

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"B":"b","A":"a","E":"e","D":"d","C":"c"}""", yaml);
        }

        private class ClassWithExplicitOrderOfProperties
        {
            public string C { get; set; }

            [YamlPropertyOrder(1)]
            public string D { get; set; }

            [YamlPropertyOrder(2)]
            public string E { get; set; }

            [YamlPropertyOrder(-1)]
            public string B { get; set; }

            [YamlPropertyOrder(-2)]
            public string A { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void RecursiveTypeWithResolverResolvingOnlyThatType()
        {
            TestResolver resolver = new(ResolveTypeInfo);

            YamlSerializerOptions options = new();
            options.TypeInfoResolver = resolver;

            string yaml = YamlSerializer.Serialize(new RecursiveType() { Next = new RecursiveType() }, options);
            Assert.Equal("""{"Next":{"Next":null}}""", yaml);

            RecursiveType deserialized = YamlSerializer.Deserialize<RecursiveType>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Value);
            Assert.NotNull(deserialized.Next);
            Assert.Equal(1, deserialized.Next.Value);
            Assert.Null(deserialized.Next.Next);

            static YamlTypeInfo? ResolveTypeInfo(Type type, YamlSerializerOptions options)
            {
                int value = 1;
                if (type == typeof(RecursiveType))
                {
                    YamlTypeInfo<RecursiveType> ti = YamlTypeInfo.CreateYamlTypeInfo<RecursiveType>(options);
                    ti.CreateObject = () => new RecursiveType();
                    YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(RecursiveType), "Next");
                    prop.Get = (obj) => ((RecursiveType)obj).Next;
                    prop.Set = (obj, val) =>
                    {
                        RecursiveType recursiveObj = (RecursiveType)obj;
                        recursiveObj.Next = (RecursiveType)val;
                        recursiveObj.Value = value++;
                    };
                    ti.Properties.Add(prop);
                    return ti;
                }

                return null;
            }
        }

        [Fact]
        public static void RecursiveTypeWithResolverResolvingOnlyThatTypeThrowsWhenPropertyOfDifferentType()
        {
            TestResolver resolver = new(ResolveTypeInfo);

            YamlSerializerOptions options = new();
            options.TypeInfoResolver = resolver;

            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(new RecursiveType() { Next = new RecursiveType() }, options));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<RecursiveType>("""{"Next":{"Next":null}}""", options));

            static YamlTypeInfo? ResolveTypeInfo(Type type, YamlSerializerOptions options)
            {
                if (type == typeof(RecursiveType))
                {
                    YamlTypeInfo<RecursiveType> ti = YamlTypeInfo.CreateYamlTypeInfo<RecursiveType>(options);
                    ti.CreateObject = () => new RecursiveType();
                    {
                        YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(RecursiveType), "Next");
                        prop.Get = (obj) => ((RecursiveType)obj).Next;
                        prop.Set = (obj, val) => ((RecursiveType)obj).Next = (RecursiveType)val;
                        ti.Properties.Add(prop);
                    }
                    {
                        YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(int), "Value");
                        prop.Get = (obj) => ((RecursiveType)obj).Value;
                        prop.Set = (obj, val) => ((RecursiveType)obj).Value = (int)val;
                        ti.Properties.Add(prop);
                    }

                    return ti;
                }

                return null;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void RecursiveTypeWithResolverResolvingOnlyUsedTypes()
        {
            TestResolver resolver = new(ResolveTypeInfo);

            YamlSerializerOptions options = new();
            options.TypeInfoResolver = resolver;

            RecursiveType obj = new RecursiveType()
            {
                Value = 13,
                Next = new RecursiveType() { Value = 7 },
            };
            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Equal("""{"Next":{"Next":null,"Value":7},"Value":13}""", yaml);

            RecursiveType deserialized = YamlSerializer.Deserialize<RecursiveType>(yaml, options);
            Assert.NotNull(deserialized);
            Assert.Equal(13, deserialized.Value);
            Assert.NotNull(deserialized.Next);
            Assert.Equal(7, deserialized.Next.Value);
            Assert.Null(deserialized.Next.Next);

            static YamlTypeInfo? ResolveTypeInfo(Type type, YamlSerializerOptions options)
            {
                if (type == typeof(RecursiveType))
                {
                    YamlTypeInfo<RecursiveType> ti = YamlTypeInfo.CreateYamlTypeInfo<RecursiveType>(options);
                    ti.CreateObject = () => new RecursiveType();
                    {
                        YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(RecursiveType), "Next");
                        prop.Get = (obj) => ((RecursiveType)obj).Next;
                        prop.Set = (obj, val) => ((RecursiveType)obj).Next = (RecursiveType)val;
                        ti.Properties.Add(prop);
                    }
                    {
                        YamlPropertyInfo prop = ti.CreateYamlPropertyInfo(typeof(int), "Value");
                        prop.Get = (obj) => ((RecursiveType)obj).Value;
                        prop.Set = (obj, val) => ((RecursiveType)obj).Value = (int)val;
                        ti.Properties.Add(prop);
                    }
                    return ti;
                }

                if (type == typeof(int))
                {
                    return YamlTypeInfo.CreateYamlTypeInfo<int>(options);
                }

                return null;
            }
        }

        private class RecursiveType
        {
            public int Value { get; set; }
            public RecursiveType? Next { get; set; }
        }

        [Fact]
        public static void CreateYamlTypeInfo_ClassWithConverterAttribute_ShouldNotResolveConverterAttribute()
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(ClassWithConverterAttribute), YamlSerializerOptions.Default);
            Assert.Equal(typeof(ClassWithConverterAttribute), yamlTypeInfo.Type);
            Assert.IsNotType<ClassWithConverterAttribute.CustomConverter>(yamlTypeInfo.Converter);
        }

        [Fact]
        public static void DefaultYamlTypeInfoResolver_ClassWithConverterAttribute_ShouldResolveConverterAttribute()
        {
            var options = YamlSerializerOptions.Default;
            YamlTypeInfo yamlTypeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(ClassWithConverterAttribute), options);
            Assert.Equal(typeof(ClassWithConverterAttribute), yamlTypeInfo.Type);
            Assert.IsType<ClassWithConverterAttribute.CustomConverter>(yamlTypeInfo.Converter);
        }

        [YamlConverter(typeof(CustomConverter))]
        public class ClassWithConverterAttribute
        {
            public class CustomConverter : YamlConverter<ClassWithConverterAttribute>
            {
                public override ClassWithConverterAttribute? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
                public override void Write(Utf8YamlWriter writer, ClassWithConverterAttribute value, YamlSerializerOptions options) => throw new NotImplementedException();
            }
        }

        [Fact]
        public static void ClassWithCallBacks_YamlTypeInfoCallbackDelegatesArePopulated()
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var jti = resolver.GetTypeInfo(typeof(ClassWithCallBacks), new());

            Assert.NotNull(jti.OnSerializing);
            Assert.NotNull(jti.OnSerialized);
            Assert.NotNull(jti.OnDeserializing);
            Assert.NotNull(jti.OnDeserialized);

            var value = new ClassWithCallBacks();
            jti.OnSerializing(value);
            Assert.Equal(1, value.IsOnSerializingInvocations);

            jti.OnSerialized(value);
            Assert.Equal(1, value.IsOnSerializedInvocations);

            jti.OnDeserializing(value);
            Assert.Equal(1, value.IsOnDeserializingInvocations);

            jti.OnDeserialized(value);
            Assert.Equal(1, value.IsOnDeserializedInvocations);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithCallBacks_CanCustomizeCallbacks()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        static jti =>
                        {
                            if (jti.Type == typeof(ClassWithCallBacks))
                            {
                                jti.OnSerializing = null;
                                jti.OnSerialized = (obj => ((ClassWithCallBacks)obj).IsOnSerializedInvocations += 10);

                                jti.OnDeserializing = null;
                                jti.OnDeserialized = (obj => ((ClassWithCallBacks)obj).IsOnDeserializedInvocations += 7);
                            }
                        }
                    }
                }
            };

            var value = new ClassWithCallBacks();
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("{}", yaml);

            Assert.Equal(0, value.IsOnSerializingInvocations);
            Assert.Equal(10, value.IsOnSerializedInvocations);

            value = YamlSerializer.Deserialize<ClassWithCallBacks>(yaml, options);
            Assert.Equal(0, value.IsOnDeserializingInvocations);
            Assert.Equal(7, value.IsOnDeserializedInvocations);
        }

        [Fact]
        public static void CollectionWithCallBacks_YamlTypeInfoCallbackDelegatesArePopulated()
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var jti = resolver.GetTypeInfo(typeof(CollectionWithCallBacks), new());

            Assert.NotNull(jti.OnSerializing);
            Assert.NotNull(jti.OnSerialized);
            Assert.NotNull(jti.OnDeserializing);
            Assert.NotNull(jti.OnDeserialized);

            var value = new CollectionWithCallBacks();
            jti.OnSerializing(value);
            Assert.Equal(1, value.IsOnSerializingInvocations);

            jti.OnSerialized(value);
            Assert.Equal(1, value.IsOnSerializedInvocations);

            jti.OnDeserializing(value);
            Assert.Equal(1, value.IsOnDeserializingInvocations);

            jti.OnDeserialized(value);
            Assert.Equal(1, value.IsOnDeserializedInvocations);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CollectionWithCallBacks_CanCustomizeCallbacks()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        static jti =>
                        {
                            if (jti.Type == typeof(CollectionWithCallBacks))
                            {
                                jti.OnSerializing = null;
                                jti.OnSerialized = (obj => ((CollectionWithCallBacks)obj).IsOnSerializedInvocations += 10);

                                jti.OnDeserializing = null;
                                jti.OnDeserialized = (obj => ((CollectionWithCallBacks)obj).IsOnDeserializedInvocations += 7);
                            }
                        }
                    }
                }
            };

            var value = new CollectionWithCallBacks();
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("[]", yaml);

            Assert.Equal(0, value.IsOnSerializingInvocations);
            Assert.Equal(10, value.IsOnSerializedInvocations);

            value = YamlSerializer.Deserialize<CollectionWithCallBacks>(yaml, options);
            Assert.Equal(0, value.IsOnDeserializingInvocations);
            Assert.Equal(7, value.IsOnDeserializedInvocations);
        }

        [Fact]
        public static void DictionaryWithCallBacks_YamlTypeInfoCallbackDelegatesArePopulated()
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var jti = resolver.GetTypeInfo(typeof(DictionaryWithCallBacks), new());

            Assert.NotNull(jti.OnSerializing);
            Assert.NotNull(jti.OnSerialized);
            Assert.NotNull(jti.OnDeserializing);
            Assert.NotNull(jti.OnDeserialized);

            var value = new DictionaryWithCallBacks();
            jti.OnSerializing(value);
            Assert.Equal(1, value.IsOnSerializingInvocations);

            jti.OnSerialized(value);
            Assert.Equal(1, value.IsOnSerializedInvocations);

            jti.OnDeserializing(value);
            Assert.Equal(1, value.IsOnDeserializingInvocations);

            jti.OnDeserialized(value);
            Assert.Equal(1, value.IsOnDeserializedInvocations);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void DictionaryWithCallBacks_CanCustomizeCallbacks()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        static jti =>
                        {
                            if (jti.Type == typeof(DictionaryWithCallBacks))
                            {
                                jti.OnSerializing = null;
                                jti.OnSerialized = (obj => ((DictionaryWithCallBacks)obj).IsOnSerializedInvocations += 10);

                                jti.OnDeserializing = null;
                                jti.OnDeserialized = (obj => ((DictionaryWithCallBacks)obj).IsOnDeserializedInvocations += 7);
                            }
                        }
                    }
                }
            };

            var value = new DictionaryWithCallBacks();
            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("{}", yaml);

            Assert.Equal(0, value.IsOnSerializingInvocations);
            Assert.Equal(10, value.IsOnSerializedInvocations);

            value = YamlSerializer.Deserialize<DictionaryWithCallBacks>(yaml, options);
            Assert.Equal(0, value.IsOnDeserializingInvocations);
            Assert.Equal(7, value.IsOnDeserializedInvocations);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        public static void SettingCallbacksOnUnsupportedTypes_ThrowsInvalidOperationException(Type type)
        {
            var jti = YamlTypeInfo.CreateYamlTypeInfo(type, new());

            Assert.NotEqual(YamlTypeInfoKind.Object, jti.Kind);
            Assert.Throws<InvalidOperationException>(() => jti.OnSerializing = null);
            Assert.Throws<InvalidOperationException>(() => jti.OnSerializing = (obj => { }));
            Assert.Throws<InvalidOperationException>(() => jti.OnSerialized = null);
            Assert.Throws<InvalidOperationException>(() => jti.OnSerialized = (obj => { }));
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserializing = null);
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserializing = (obj => { }));
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserialized = null);
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserialized = (obj => { }));
        }

        [Theory]
        [InlineData(typeof(ImmutableArray<string>))]
        [InlineData(typeof(ImmutableList<string>))]
        [InlineData(typeof(IImmutableList<string>))]
        [InlineData(typeof(ImmutableStack<string>))]
        [InlineData(typeof(IImmutableStack<string>))]
        [InlineData(typeof(ImmutableQueue<string>))]
        [InlineData(typeof(IImmutableQueue<string>))]
        [InlineData(typeof(ImmutableSortedSet<string>))]
        [InlineData(typeof(ImmutableHashSet<string>))]
        [InlineData(typeof(IImmutableSet<string>))]
        [InlineData(typeof(ImmutableDictionary<string, string>))]
        [InlineData(typeof(ImmutableSortedDictionary<string, string>))]
        [InlineData(typeof(IImmutableDictionary<string, string>))]
        [InlineData(typeof(string[]))]
        [InlineData(typeof(Memory<string>))]
        [InlineData(typeof(ReadOnlyMemory<string>))]
        public static void SettingOnDeserializingCallbackOnImmutableTypes_ThrowsInvalidOperationException(Type type)
        {
            var jti = YamlTypeInfo.CreateYamlTypeInfo(type, new());

            Assert.NotEqual(YamlTypeInfoKind.Object, jti.Kind);
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserializing = null);
            Assert.Throws<InvalidOperationException>(() => jti.OnDeserializing = (obj => { }));
        }

        public class ClassWithCallBacks :
            IYamlOnSerializing, IYamlOnSerialized,
            IYamlOnDeserializing, IYamlOnDeserialized
        {
            [YamlIgnore]
            public int IsOnSerializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnSerializedInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializedInvocations { get; set; }

            public void OnSerializing() => IsOnSerializingInvocations++;
            public void OnSerialized() => IsOnSerializedInvocations++;
            public void OnDeserializing() => IsOnDeserializingInvocations++;
            public void OnDeserialized() => IsOnDeserializedInvocations++;
        }

        public class CollectionWithCallBacks : List<int>,
            IYamlOnSerializing, IYamlOnSerialized,
            IYamlOnDeserializing, IYamlOnDeserialized
        {
            [YamlIgnore]
            public int IsOnSerializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnSerializedInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializedInvocations { get; set; }

            public void OnSerializing() => IsOnSerializingInvocations++;
            public void OnSerialized() => IsOnSerializedInvocations++;
            public void OnDeserializing() => IsOnDeserializingInvocations++;
            public void OnDeserialized() => IsOnDeserializedInvocations++;
        }

        public class DictionaryWithCallBacks : Dictionary<string, int>,
            IYamlOnSerializing, IYamlOnSerialized,
            IYamlOnDeserializing, IYamlOnDeserialized
        {
            [YamlIgnore]
            public int IsOnSerializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnSerializedInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializingInvocations { get; set; }
            [YamlIgnore]
            public int IsOnDeserializedInvocations { get; set; }

            public void OnSerializing() => IsOnSerializingInvocations++;
            public void OnSerialized() => IsOnSerializedInvocations++;
            public void OnDeserializing() => IsOnDeserializingInvocations++;
            public void OnDeserialized() => IsOnDeserializedInvocations++;
        }

        [Theory]
        [InlineData(null)]
        [InlineData(YamlUnmappedMemberHandling.Skip)]
        [InlineData(YamlUnmappedMemberHandling.Disallow)]
        public static void UnmappedMemberHandling_ShouldGetSetValue(YamlUnmappedMemberHandling? handling)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), YamlSerializerOptions.Default);
            yamlTypeInfo.UnmappedMemberHandling = handling;
            Assert.Equal(handling, yamlTypeInfo.UnmappedMemberHandling);
            YamlSerializer.Serialize(new Poco(), yamlTypeInfo);
            Assert.Equal(handling, yamlTypeInfo.UnmappedMemberHandling);
        }

        [Theory]
        [InlineData((YamlUnmappedMemberHandling)(-1))]
        [InlineData((YamlUnmappedMemberHandling)2)]
        [InlineData((YamlUnmappedMemberHandling)int.MaxValue)]
        public static void UnmappedMemberHandling_SetInvalidValue_ThrowsArgumentOutOfRangeException(YamlUnmappedMemberHandling handling)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new());
            Assert.Throws<ArgumentOutOfRangeException>(() => yamlTypeInfo.UnmappedMemberHandling = handling);
        }

        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(int))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(Dictionary<string, int>))]
        public static void PreferredPropertyObjectCreationHandling_NonObjectKind_ThrowsInvalidOperationException(Type type)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(type, new());

            // Invalid kinds default to null.
            Assert.Null(yamlTypeInfo.PreferredPropertyObjectCreationHandling);

            Assert.Throws<InvalidOperationException>(() => yamlTypeInfo.PreferredPropertyObjectCreationHandling = null);
            Assert.Throws<InvalidOperationException>(() => yamlTypeInfo.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate);
            Assert.Throws<InvalidOperationException>(() => yamlTypeInfo.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Replace);
            Assert.Null(yamlTypeInfo.PreferredPropertyObjectCreationHandling);
        }

        [Theory]
        [InlineData((YamlObjectCreationHandling)(-1))]
        [InlineData((YamlObjectCreationHandling)2)]
        [InlineData((YamlObjectCreationHandling)int.MaxValue)]
        public static void PreferredPropertyObjectCreationHandling_SetInvalidValue_ThrowsArgumentOutOfRangeException(YamlObjectCreationHandling handling)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new());
            Assert.Throws<ArgumentOutOfRangeException>(() => yamlTypeInfo.PreferredPropertyObjectCreationHandling = handling);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void UnmappedMemberHandling_InvalidMetadataKind_ThrowsInvalidOperationException(Type type)
        {
            YamlTypeInfo yamlTypeInfo = YamlTypeInfo.CreateYamlTypeInfo(type, new());
            Assert.Throws<InvalidOperationException>(() => yamlTypeInfo.UnmappedMemberHandling = YamlUnmappedMemberHandling.Skip);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void DefaultYamlTypeInfo_OriginatingResolver_GetterReturnsResolver(Type type)
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var options = new YamlSerializerOptions();

            YamlTypeInfo typeInfo = resolver.GetTypeInfo(type, options);
            Assert.Same(resolver, typeInfo.OriginatingResolver);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void OriginatingResolver_GetterReturnsTheSetValue(Type type)
        {
            var resolver = new DefaultYamlTypeInfoResolver();
            var options = new YamlSerializerOptions();

            YamlTypeInfo typeInfo = resolver.GetTypeInfo(type, options);
            typeInfo.OriginatingResolver = null;
            Assert.Null(typeInfo.OriginatingResolver);

            typeInfo.OriginatingResolver = YamlSerializerOptions.Default.TypeInfoResolver;
            Assert.Same(YamlSerializerOptions.Default.TypeInfoResolver, typeInfo.OriginatingResolver);
        }

        [Theory]
        [InlineData(typeof(bool?))]
        [InlineData(typeof(int?))]
        [InlineData(typeof(Guid?))]
        [InlineData(typeof(BigInteger?))]
        [InlineData(typeof(string))]
        [InlineData(typeof(Poco))]
        public static void YamlPropertyInfo_IsNullable_PropertyTypeSupportsNull_SupportsAllConfigurations(Type propertyType)
        {
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new()); // The declaring type shouldn't matter.
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(propertyType, "SomePropertyName");

            Assert.True(propertyInfo.IsGetNullable);
            Assert.True(propertyInfo.IsSetNullable);

            propertyInfo.IsGetNullable = false;
            propertyInfo.IsSetNullable = false;

            Assert.False(propertyInfo.IsGetNullable);
            Assert.False(propertyInfo.IsSetNullable);

            propertyInfo.IsGetNullable = true;
            propertyInfo.IsSetNullable = true;

            Assert.True(propertyInfo.IsGetNullable);
            Assert.True(propertyInfo.IsSetNullable);
        }

        [Theory]
        [InlineData(typeof(bool))]
        [InlineData(typeof(int))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(BigInteger))]
        public static void YamlPropertyInfo_IsNullable_PropertyTypeNotNull_CannotBeMadeNullable(Type propertyType)
        {
            YamlTypeInfo typeInfo = YamlTypeInfo.CreateYamlTypeInfo(typeof(Poco), new()); // The declaring type shouldn't matter.
            YamlPropertyInfo propertyInfo = typeInfo.CreateYamlPropertyInfo(propertyType, "SomePropertyName");

            Assert.False(propertyInfo.IsGetNullable);
            Assert.False(propertyInfo.IsSetNullable);

            Assert.Throws<InvalidOperationException>(() => propertyInfo.IsGetNullable = true);
            Assert.Throws<InvalidOperationException>(() => propertyInfo.IsSetNullable = true);

            Assert.False(propertyInfo.IsGetNullable);
            Assert.False(propertyInfo.IsSetNullable);

            // Setting to false is a no-op.
            propertyInfo.IsGetNullable = false;
            propertyInfo.IsSetNullable = false;

            Assert.False(propertyInfo.IsGetNullable);
            Assert.False(propertyInfo.IsSetNullable);
        }
    }
}
