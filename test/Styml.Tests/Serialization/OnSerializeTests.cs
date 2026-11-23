// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class OnSerializeTests
    {
        private class MyClass :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public int MyInt { get; set; }

            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, MyInt);
                MyInt++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Equal(0, MyInt);
                MyInt = 100; // Gets replaced by the serializer.
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyClass()
        {
            MyClass obj = new();
            obj.MyInt = 1;

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("{\"MyInt\":2}", yaml);
            Assert.Equal(3, obj.MyInt);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyClass>("{\"MyInt\":1}");
            Assert.Equal(2, obj.MyInt);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private struct MyStruct :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public int MyInt { get; set; }

            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                MyInt++;    // should not affect serialization
            }

            public void OnDeserializing()
            {
                Assert.Equal(0, MyInt);
                _onDeserializingCount++;
                MyInt = 100; // Gets replaced by the serializer.
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                Assert.Equal(1, MyInt);
                _onDeserializedCount++;
            }
        }


        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyStruct()
        {
            MyStruct obj = new();
            obj.MyInt = 1;

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("{\"MyInt\":2}", yaml);

            // Although the OnSerialize* callbacks are invoked, a struct is passed to the serializer byvalue.
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);

            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyStruct>("{\"MyInt\":1}");
            Assert.Equal(1, obj.MyInt);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private class MyClassWithSmallConstructor :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public int MyInt { get; set; }

            public MyClassWithSmallConstructor(int myInt)
            {
                MyInt = myInt;
                _constructorCalled = true;
            }

            internal bool _constructorCalled;
            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, MyInt);
                MyInt++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Equal(1, MyInt);
                MyInt++; // Does not get replaced by the serializer since it was passed into the ctor.
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(2, MyInt);
                MyInt++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyClassWithSmallConstructor()
        {
            MyClassWithSmallConstructor obj = new(1);
            Assert.Equal(1, obj.MyInt);

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("{\"MyInt\":2}", yaml);
            Assert.Equal(3, obj.MyInt);
            Assert.True(obj._constructorCalled);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyClassWithSmallConstructor>("{\"MyInt\":1}");
            Assert.True(obj._constructorCalled);
            Assert.Equal(3, obj.MyInt);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private class MyClassWithLargeConstructor :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public int MyInt1 { get; set; }
            public int MyInt2 { get; set; }
            public int MyInt3 { get; set; }
            public int MyInt4 { get; set; }
            public int MyInt5 { get; set; }

            public MyClassWithLargeConstructor(int myInt1, int myInt2, int myInt3, int myInt4, int myInt5)
            {
                MyInt1 = myInt1;
                MyInt2 = myInt2;
                MyInt3 = myInt3;
                MyInt4 = myInt4;
                MyInt5 = myInt5;
                _constructorCalled = true;
            }

            internal bool _constructorCalled;
            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, MyInt1);
                MyInt1++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, MyInt1);
                MyInt1++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Equal(1, MyInt1);
                MyInt1++; // Does not get replaced by the serializer since it was passed into the ctor.
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(2, MyInt1);
                MyInt1++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyClassWithLargeConstructor()
        {
            const string Yaml = "{\"MyInt1\":1,\"MyInt2\":2,\"MyInt3\":3,\"MyInt4\":4,\"MyInt5\":5}";

            MyClassWithLargeConstructor obj = new(1, 2, 3, 4, 5);
            Assert.Equal(1, obj.MyInt1);
            Assert.Equal(2, obj.MyInt2);
            Assert.Equal(3, obj.MyInt3);
            Assert.Equal(4, obj.MyInt4);
            Assert.Equal(5, obj.MyInt5);

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Contains("\"MyInt1\":2", yaml);
            Assert.Equal(3, obj.MyInt1); // Is updated in the callback
            Assert.True(obj._constructorCalled);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyClassWithLargeConstructor>(Yaml);
            Assert.True(obj._constructorCalled);
            Assert.Equal(3, obj.MyInt1);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private class MyCyclicClass :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public int MyInt { get; set; }
            public MyCyclicClass Cycle { get; set; }

            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, MyInt);
                MyInt++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Equal(0, MyInt);
                MyInt = 100; // Gets replaced by the serializer.
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(1, MyInt);
                MyInt++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyCyclicClass()
        {
            const string Yaml = "{\"$id\":\"1\",\"MyInt\":1,\"Cycle\":{\"$ref\":\"1\"}}";

            MyCyclicClass obj = new();
            obj.MyInt = 1;
            obj.Cycle = obj;

            YamlSerializerOptions options = new();
            options.ReferenceHandler = ReferenceHandler.Preserve;

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains("\"MyInt\":2", yaml);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyCyclicClass>(Yaml, options);
            Assert.Equal(2, obj.MyInt);
            Assert.Equal(obj, obj.Cycle);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private class MyCollection : List<int>,
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, this[0]);
                this[0]++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, this[0]);
                this[0]++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Empty(this);
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(1, this[0]);
                this[0]++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyCollection()
        {
            MyCollection obj = new() { 1 };

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("[2]", yaml);
            Assert.Equal(3, obj[0]);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyCollection>("[1]");
            Assert.Equal(2, obj[0]);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        private class MyDictionary : Dictionary<string, int>,
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            internal int _onSerializingCount;
            internal int _onSerializedCount;
            internal int _onDeserializingCount;
            internal int _onDeserializedCount;

            public void OnSerializing()
            {
                _onSerializingCount++;
                Assert.Equal(1, this["A"]);
                this["A"]++;
            }

            public void OnSerialized()
            {
                Assert.Equal(1, _onSerializingCount);
                _onSerializedCount++;
                Assert.Equal(2, this["A"]);
                this["A"]++;
            }

            public void OnDeserializing()
            {
                _onDeserializingCount++;
                Assert.Empty(this);
            }

            public void OnDeserialized()
            {
                Assert.Equal(1, _onDeserializingCount);
                _onDeserializedCount++;
                Assert.Equal(1, this["A"]);
                this["A"]++;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Test_MyDictionary()
        {
            MyDictionary obj = new() { ["A"] = 1 };

            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("{\"A\":2}", yaml);
            Assert.Equal(3, obj["A"]);
            Assert.Equal(1, obj._onSerializingCount);
            Assert.Equal(1, obj._onSerializedCount);
            Assert.Equal(0, obj._onDeserializingCount);
            Assert.Equal(0, obj._onDeserializedCount);

            obj = YamlSerializer.Deserialize<MyDictionary>("{\"A\":1}");
            Assert.Equal(2, obj["A"]);
            Assert.Equal(0, obj._onSerializingCount);
            Assert.Equal(0, obj._onSerializedCount);
            Assert.Equal(1, obj._onDeserializingCount);
            Assert.Equal(1, obj._onDeserializedCount);
        }

        [YamlConverter(converterType: typeof(MyValueConverter))]
        private class MyValue :
            IYamlOnDeserializing,
            IYamlOnDeserialized,
            IYamlOnSerializing,
            IYamlOnSerialized
        {
            public void OnDeserialized() => Assert.Fail("Not expected");
            public void OnDeserializing() => Assert.Fail("Not expected");
            public void OnSerialized() => Assert.Fail("Not expected");
            public void OnSerializing() => Assert.Fail("Not expected");
        }

        private class MyValueConverter : YamlConverter<MyValue>
        {
            public override MyValue? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return new MyValue();
            }

            public override void Write(Utf8YamlWriter writer, MyValue value, YamlSerializerOptions options)
            {
                writer.WriteStringValue("dummy");
            }
        }

        [Fact]
        public static void NonPocosIgnored()
        {
            YamlSerializer.Serialize(new MyValue());
        }
    }
}
