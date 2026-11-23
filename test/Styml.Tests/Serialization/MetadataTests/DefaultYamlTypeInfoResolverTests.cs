// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class DefaultYamlTypeInfoResolverTests
    {
        [Fact]
        public static void GetTypeInfoNullArguments()
        {
            DefaultYamlTypeInfoResolver r = new();
            Assert.Throws<ArgumentNullException>(() => r.GetTypeInfo(null, null));
            Assert.Throws<ArgumentNullException>(() => r.GetTypeInfo(null, new YamlSerializerOptions()));
            Assert.Throws<ArgumentNullException>(() => r.GetTypeInfo(typeof(string), null));
        }

        [Fact]
        public static void ModifiersIsEmptyNonCastableIList()
        {
            DefaultYamlTypeInfoResolver r = new();
            Assert.NotNull(r.Modifiers);
            Assert.Null(r.Modifiers as List<Action<YamlTypeInfo>>);
            Assert.False(r.Modifiers.GetType().IsPublic);
            Assert.Empty(r.Modifiers);
            Assert.Equal(0, r.Modifiers.Count);
        }

        [Fact]
        public static void ModifiersAreMutableAndInterfaceIsImplementedCorrectly()
        {
            DefaultYamlTypeInfoResolver r = new();
            Assert.Same(r.Modifiers, r.Modifiers);
            var mods = r.Modifiers;

            Action<YamlTypeInfo> el0 = (ti) => { };
            Action<YamlTypeInfo> el1 = (ti) => { };
            Action<YamlTypeInfo> el2 = (ti) => { };
            Assert.NotSame(el0, el1);
            Assert.NotSame(el1, el2);
            IEnumerator<Action<YamlTypeInfo>> enumerator;

            Assert.Equal(0, mods.Count);
            Assert.False(mods.IsReadOnly);
            Assert.Throws<ArgumentOutOfRangeException>(() => mods[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mods[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mods[1]);

            using (enumerator = mods.GetEnumerator())
            {
                Assert.False(enumerator.MoveNext());
            }

            mods.Add(el0);
            Assert.Equal(1, mods.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => mods[-1]);
            Assert.Same(el0, mods[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mods[1]);

            using (enumerator = mods.GetEnumerator())
            {
                Assert.True(enumerator.MoveNext());
                Assert.Same(el0, enumerator.Current);
                Assert.False(enumerator.MoveNext());
            }

            mods.Clear();
            Assert.Equal(0, mods.Count);

            using (enumerator = mods.GetEnumerator())
            {
                Assert.False(enumerator.MoveNext());
            }

            mods.Insert(0, el0);
            Assert.Equal(1, mods.Count);
            Assert.Same(el0, mods[0]);
            Assert.True(mods.Remove(el0));
            Assert.Equal(0, mods.Count);

            mods.Insert(0, el1);
            mods.Insert(0, el0);
            Assert.Equal(2, mods.Count);
            Assert.Same(el0, mods[0]);
            Assert.Same(el1, mods[1]);
            Assert.False(mods.Remove(el2));
            mods.RemoveAt(1);
            Assert.Equal(1, mods.Count);
            Assert.Same(el0, mods[0]);
        }

        [Fact]
        public static void EmptyModifiersAreImmutableAfterFirstUsage()
        {
            DefaultYamlTypeInfoResolver r = new();
            IList<Action<YamlTypeInfo>> mods = r.Modifiers;

            Assert.NotNull(r.GetTypeInfo(typeof(string), new YamlSerializerOptions()));

            Assert.True(mods.IsReadOnly);
            Assert.Same(mods, r.Modifiers);
            Assert.Equal(0, mods.Count);

            Assert.Throws<InvalidOperationException>(() => mods.Add((ti) => { }));
            Assert.Throws<InvalidOperationException>(() => mods.Insert(0, (ti) => { }));
        }

        [Fact]
        public static void NonEmptyModifiersAreImmutableAfterFirstUsage()
        {
            DefaultYamlTypeInfoResolver r = new();
            IList<Action<YamlTypeInfo>> mods = r.Modifiers;
            Action<YamlTypeInfo> el0 = (ti) => { };
            Action<YamlTypeInfo> el1 = (ti) => { };
            mods.Add(el0);
            mods.Add(el1);

            Assert.NotNull(r.GetTypeInfo(typeof(string), new YamlSerializerOptions()));

            Assert.True(mods.IsReadOnly);
            Assert.Same(mods, r.Modifiers);
            Assert.Equal(2, mods.Count);

            Assert.Throws<InvalidOperationException>(() => mods.Add((ti) => { }));
            Assert.Throws<InvalidOperationException>(() => mods.Insert(0, (ti) => { }));
            Assert.Throws<InvalidOperationException>(() => mods.Remove(el0));
            Assert.Throws<InvalidOperationException>(() => mods.RemoveAt(0));
        }

        [Fact]
        public static void ModifiersAreCalledAndModifyTypeInfos()
        {
            DefaultYamlTypeInfoResolver r = new();
            YamlTypeInfo storedTypeInfo = null;
            bool createObjectCalled = false;
            bool secondModifierCalled = false;
            r.Modifiers.Add((ti) =>
            {
                Assert.Null(storedTypeInfo);
                storedTypeInfo = ti;

                // marker that test has modified something
                ti.CreateObject = () =>
                {
                    Assert.False(createObjectCalled);
                    createObjectCalled = true;

                    // we don't care what's returned as it won't be used by deserialization
                    return null;
                };
            });

            r.Modifiers.Add((ti) =>
            {
                // this proves we've been called after first modifier
                Assert.NotNull(storedTypeInfo);
                Assert.Same(storedTypeInfo, ti);
                secondModifierCalled = true;
            });

            YamlTypeInfo returnedTypeInfo = r.GetTypeInfo(typeof(InvalidOperationException), new YamlSerializerOptions());

            Assert.NotNull(storedTypeInfo);
            Assert.Same(storedTypeInfo, returnedTypeInfo);

            Assert.False(createObjectCalled);
            // we call our previously set marker
            storedTypeInfo.CreateObject();

            Assert.True(createObjectCalled);
            Assert.True(secondModifierCalled);
        }

        [Fact]
        public static void AddingNullModifierThrows()
        {
            DefaultYamlTypeInfoResolver r = new();
            Assert.Throws<ArgumentNullException>(() => r.Modifiers.Add(null));
            Assert.Throws<ArgumentNullException>(() => r.Modifiers.Insert(0, null));
        }

        private static void InvokeGeneric(Type type, string methodName, params object[] args)
        {
            typeof(DefaultYamlTypeInfoResolverTests)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(type)
                .Invoke(null, args);
        }

        private class SomeClass
        {
            public object? ObjProp { get; set; }
            public int IntProp { get; set; }
        }

        private class SomeOtherClass
        {
            public object ObjProp { get; set; }
            public int IntProp { get; set; }
        }

        [YamlSerializable(typeof(SomeClass))]
        [YamlSerializable(typeof(SomeOtherClass))]
        private partial class SomeClassContext : YamlSerializerContext
        {
        }

        private class CustomThrowingConverter<T> : YamlConverter<T>
        {
            public override T? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
            public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options) => throw new NotImplementedException();
        }

        private class DummyConverter<T> : YamlConverter<T>
        {
            public override T? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => default(T);
            public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options) { }
        }

        private class SomeRecursiveClass
        {
            public int IntProp { get; set; }
            public SomeRecursiveClass? RecursiveProperty { get; set; }
        }

        [YamlDerivedType(typeof(DerivedClass))]
        private class SomePolymorphicClass
        {
            public class DerivedClass : SomePolymorphicClass
            {
            }
        }
    }
}
