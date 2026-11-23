// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class SimpleTestClassWithSimpleObject : ITestClass
    {
        public object MyInt16 { get; set; }
        public object MyInt32 { get; set; }
        public object MyInt64 { get; set; }
        public object MyUInt16 { get; set; }
        public object MyUInt32 { get; set; }
        public object MyUInt64 { get; set; }
        public object MyByte { get; set; }
        public object MySByte { get; set; }
        public object MyChar { get; set; }
        public object MyString { get; set; }
        public object MyDecimal { get; set; }
        public object MyBooleanTrue { get; set; }
        public object MyBooleanFalse { get; set; }
        public object MySingle { get; set; }
        public object MyDouble { get; set; }
        public object MyDateTime { get; set; }
        public object MyGuid { get; set; }
        public object MyEnum { get; set; }
        public object MyStruct { get; set; }

        public static readonly string s_yaml =
            @"{" +
                @"""MyInt16"" : 1," +
                @"""MyInt32"" : 2," +
                @"""MyInt64"" : 3," +
                @"""MyUInt16"" : 4," +
                @"""MyUInt32"" : 5," +
                @"""MyUInt64"" : 6," +
                @"""MyByte"" : 7," +
                @"""MySByte"" : 8," +
                @"""MyChar"" : ""a""," +
                @"""MyString"" : ""Hello""," +
                @"""MyBooleanTrue"" : true," +
                @"""MyBooleanFalse"" : false," +
                @"""MySingle"" : 1.1," +
                @"""MyDouble"" : 2.2," +
                @"""MyDecimal"" : 3.3," +
                @"""MyDateTime"" : ""2019-01-30T12:01:02.0000000Z""," +
                @"""MyGuid"" : ""5BB9D872-DA8A-471E-AA70-08E19102683D""," +
                @"""MyEnum"" : 2," + // int by default
                @"""MyStruct"" : { ""One"" : 1, ""Two"" : 3.14 }" +
            @"}";

        public static readonly byte[] s_data = Encoding.UTF8.GetBytes(s_yaml);

        private bool _initialized;

        public virtual void Initialize()
        {
            _initialized = true;
            MyInt16 = (short)1;
            MyInt32 = (int)2;
            MyInt64 = (long)3;
            MyUInt16 = (ushort)4;
            MyUInt32 = (uint)5;
            MyUInt64 = (ulong)6;
            MyByte = (byte)7;
            MySByte = (sbyte)8;
            MyChar = 'a';
            MyString = "Hello";
            MyBooleanTrue = true;
            MyBooleanFalse = false;
            MySingle = 1.1f;
            MyDouble = 2.2d;
            MyDecimal = 3.3m;
            MyDateTime = new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc);
            MyGuid = new Guid("5BB9D872-DA8A-471E-AA70-08E19102683D");
            MyEnum = SampleEnum.Two;
            MyStruct = new SimpleStruct { One = 1, Two = 3.14 };
        }

        public virtual void Verify()
        {
            // Shared test logic verifies state after calling Initialize. In the object
            // case we don't care if the object is initialized with non YamlElement values,
            // they'll still be serialized back in as YamlElement.

            if (_initialized)
                return;

            Assert.IsType<YamlElement>(MyInt16);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyInt16).ValueKind);
            Assert.Equal(1, ((YamlElement)MyInt16).GetInt16());
            Assert.IsType<YamlElement>(MyInt32);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyInt32).ValueKind);
            Assert.Equal(2, ((YamlElement)MyInt32).GetInt32());
            Assert.IsType<YamlElement>(MyInt64);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyInt64).ValueKind);
            Assert.Equal(3L, ((YamlElement)MyInt64).GetInt64());
            Assert.IsType<YamlElement>(MyUInt16);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyUInt16).ValueKind);
            Assert.Equal(4u, ((YamlElement)MyUInt16).GetUInt16());
            Assert.IsType<YamlElement>(MyUInt32);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyUInt32).ValueKind);
            Assert.Equal(5u, ((YamlElement)MyUInt32).GetUInt32());
            Assert.IsType<YamlElement>(MyUInt64);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyUInt64).ValueKind);
            Assert.Equal(6UL, ((YamlElement)MyUInt64).GetUInt64());
            Assert.IsType<YamlElement>(MyByte);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyByte).ValueKind);
            Assert.Equal(7, ((YamlElement)MyByte).GetByte());
            Assert.IsType<YamlElement>(MySByte);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MySByte).ValueKind);
            Assert.Equal(8, ((YamlElement)MySByte).GetSByte());
            Assert.IsType<YamlElement>(MyChar);
            Assert.Equal(YamlValueKind.String, ((YamlElement)MyChar).ValueKind);
            Assert.Equal("a", ((YamlElement)MyChar).GetString());
            Assert.IsType<YamlElement>(MyString);
            Assert.Equal(YamlValueKind.String, ((YamlElement)MyString).ValueKind);
            Assert.Equal("Hello", ((YamlElement)MyString).GetString());
            Assert.IsType<YamlElement>(MyDecimal);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyDecimal).ValueKind);
            Assert.Equal(3.3m, ((YamlElement)MyDecimal).GetDecimal());
            Assert.IsType<YamlElement>(MyBooleanFalse);
            Assert.Equal(YamlValueKind.False, ((YamlElement)MyBooleanFalse).ValueKind);
            Assert.False(((YamlElement)MyBooleanFalse).GetBoolean());
            Assert.IsType<YamlElement>(MyBooleanTrue);
            Assert.Equal(YamlValueKind.True, ((YamlElement)MyBooleanTrue).ValueKind);
            Assert.True(((YamlElement)MyBooleanTrue).GetBoolean());
            Assert.IsType<YamlElement>(MySingle);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MySingle).ValueKind);
            Assert.Equal(1.1f, ((YamlElement)MySingle).GetSingle());
            Assert.IsType<YamlElement>(MyDouble);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyDouble).ValueKind);
            Assert.Equal(2.2d, ((YamlElement)MyDouble).GetDouble());
            Assert.IsType<YamlElement>(MyDateTime);
            Assert.Equal(YamlValueKind.String, ((YamlElement)MyDateTime).ValueKind);
            Assert.Equal(new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc), ((YamlElement)MyDateTime).GetDateTime());
            Assert.IsType<YamlElement>(MyGuid);
            Assert.Equal(YamlValueKind.String, ((YamlElement)MyGuid).ValueKind);
            Assert.Equal(new Guid("5BB9D872-DA8A-471E-AA70-08E19102683D"), ((YamlElement)MyGuid).GetGuid());
            Assert.IsType<YamlElement>(MyEnum);
            Assert.Equal(YamlValueKind.Number, ((YamlElement)MyEnum).ValueKind);
            Assert.Equal(SampleEnum.Two, (SampleEnum)((YamlElement)MyEnum).GetUInt32());
            Assert.Equal(1, ((YamlElement)MyStruct).GetProperty("One").GetInt32());
            Assert.Equal(3.14, ((YamlElement)MyStruct).GetProperty("Two").GetDouble());
        }
    }
}
