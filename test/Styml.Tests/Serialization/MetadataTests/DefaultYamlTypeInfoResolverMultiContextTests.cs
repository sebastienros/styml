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
    public partial class DefaultYamlTypeInfoResolverMultiContextTests : SerializerTests
    {
        public DefaultYamlTypeInfoResolverMultiContextTests()
            : base(YamlSerializerWrapper.StringSerializer)
        {
        }

        [Fact]
        public async Task TypeInfoWithNullCreateObjectFailsDeserialization()
        {
            DefaultYamlTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(Poco))
                {
                    ti.CreateObject = null;
                }
            });

            YamlSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            string yaml = """{"StringProperty":"test"}""";
            await TestMultiContextDeserialization<Poco>(yaml, new Poco() { StringProperty = "test" });
            await TestMultiContextDeserialization<Poco>(yaml, options: o, expectedExceptionType: typeof(NotSupportedException));

            Assert.Throws<InvalidOperationException>(() => resolver.Modifiers.Add(ti => { }));
        }

        [Theory]
        [MemberData(nameof(YamlSerializerSerializeWithTypeInfoOfT_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task YamlSerializerSerializeWithTypeInfoOfT<T>(T testObj, string expectedYaml)
        {
            DefaultYamlTypeInfoResolver r = new();
            YamlSerializerOptions o = new();
            o.TypeInfoResolver = r;
            YamlTypeInfo<T> typeInfo = (YamlTypeInfo<T>)r.GetTypeInfo(typeof(T), o);
            string yaml = await Serializer.SerializeWrapper(testObj, typeInfo);
            Assert.Equal(expectedYaml, yaml);
        }

        [Fact]
        public async Task SerializationWithYamlTypeInfoWithoutSettingTypeInfoResolverThrows()
        {
            YamlSerializerOptions o = new();
            DefaultYamlTypeInfoResolver r = new();
            // note: TypeInfoResolver not set
            YamlTypeInfo<SomeClass> ti = (YamlTypeInfo<SomeClass>)r.GetTypeInfo(typeof(SomeClass), o);
            SomeClass obj = new()
            {
                ObjProp = "test",
                IntProp = 42,
            };

            // TODO: reassess if this is expected behavior
            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.SerializeWrapper(obj, ti));
        }

        [Fact]
        public async Task DeserializationWithYamlTypeInfoWithoutSettingTypeInfoResolverThrows()
        {
            YamlSerializerOptions o = new();
            DefaultYamlTypeInfoResolver r = new();
            // note: TypeInfoResolver not set
            YamlTypeInfo<SomeClass> ti = (YamlTypeInfo<SomeClass>)r.GetTypeInfo(typeof(SomeClass), o);

            // TODO: reassess if this is expected behavior
            string yaml = """{"ObjProp":"test","IntProp":42}""";
            await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.DeserializeWrapper(yaml, ti));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task SerializationWithYamlTypeInfoWhenTypeInfoResolverSetIsPossible()
        {
            YamlSerializerOptions o = new();
            DefaultYamlTypeInfoResolver r = new();
            o.TypeInfoResolver = r;
            YamlTypeInfo<SomeClass> ti = (YamlTypeInfo<SomeClass>)r.GetTypeInfo(typeof(SomeClass), o);
            SomeClass obj = new()
            {
                ObjProp = "test",
                IntProp = 42,
            };

            string yaml = await Serializer.SerializeWrapper(obj, ti);
            Assert.Equal("""{"ObjProp":"test","IntProp":42}""", yaml);
        }

        [Fact]
        public async Task DeserializationWithYamlTypeInfoWhenTypeInfoResolverSetIsPossible()
        {
            YamlSerializerOptions o = new();
            DefaultYamlTypeInfoResolver r = new();
            o.TypeInfoResolver = r;
            YamlTypeInfo<SomeClass> ti = (YamlTypeInfo<SomeClass>)r.GetTypeInfo(typeof(SomeClass), o);
            string yaml = """{"ObjProp":"test","IntProp":42}""";
            SomeClass deserialized = await Serializer.DeserializeWrapper(yaml, ti);
            Assert.IsType<YamlElement>(deserialized.ObjProp);
            Assert.Equal("test", ((YamlElement)deserialized.ObjProp).GetString());
            Assert.Equal(42, deserialized.IntProp);
        }

        public static IEnumerable<object[]> YamlSerializerSerializeWithTypeInfoOfT_TestData()
        {
            yield return new object[] { "value", """
                "value"
                """ };
            yield return new object[] { 5, @"5" };
            yield return new object[] { new SomeClass() { IntProp = 15, ObjProp = 17m }, """{"ObjProp":17,"IntProp":15}""" };
        }

        private class Poco
        {
            public string StringProperty { get; set; }
        }

        private class SomeClass
        {
            public object ObjProp { get; set; }
            public int IntProp { get; set; }
        }
    }
}
