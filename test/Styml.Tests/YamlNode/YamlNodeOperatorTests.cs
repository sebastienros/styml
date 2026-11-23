// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Nodes.Tests
{
    public static class OperatorTests
    {
        private const string ExpectedPrimitiveYaml =
                @"{" +
                @"""MyInt16"":1," +
                @"""MyInt32"":2," +
                @"""MyInt64"":3," +
                @"""MyUInt16"":4," +
                @"""MyUInt32"":5," +
                @"""MyUInt64"":6," +
                @"""MyByte"":7," +
                @"""MySByte"":8," +
                @"""MyChar"":""a""," +
                @"""MyString"":""Hello""," +
                @"""MyBooleanTrue"":true," +
                @"""MyBooleanFalse"":false," +
                @"""MySingle"":1.1," +
                @"""MyDouble"":2.2," +
                @"""MyDecimal"":3.3," +
                @"""MyDateTime"":""2019-01-30T12:01:02Z""," +
                @"""MyDateTimeOffset"":""2019-01-30T12:01:02+01:00""," +
                @"""MyGuid"":""1b33498a-7b7d-4dda-9c13-f6aa4ab449a6""" + // note lowercase
                @"}";

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ImplicitOperators_FromProperties()
        {
            var jObject = new YamlObject();
            jObject["MyInt16"] = (short)1;
            jObject["MyInt32"] = 2;
            jObject["MyInt64"] = (long)3;
            jObject["MyUInt16"] = (ushort)4;
            jObject["MyUInt32"] = (uint)5;
            jObject["MyUInt64"] = (ulong)6;
            jObject["MyByte"] = (byte)7;
            jObject["MySByte"] = (sbyte)8;
            jObject["MyChar"] = 'a';
            jObject["MyString"] = "Hello";
            jObject["MyBooleanTrue"] = true;
            jObject["MyBooleanFalse"] = false;
            jObject["MySingle"] = 1.1f;
            jObject["MyDouble"] = 2.2d;
            jObject["MyDecimal"] = 3.3m;
            jObject["MyDateTime"] = new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc);
            jObject["MyDateTimeOffset"] = new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0));
            jObject["MyGuid"] = new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6");

            string expected = ExpectedPrimitiveYaml;

            string yaml = jObject.ToYamlString();

            // Adjust for non-Core frameworks which do not have round-trippable floating point strings.
            yaml = yaml.Replace("1.10000002", "1.1").Replace("2.2000000000000002", "2.2");

            Assert.Equal(expected, yaml);
        }

        [Fact]
        public static void ExplicitOperators_FromProperties()
        {
            YamlObject jObject = YamlNode.Parse(ExpectedPrimitiveYaml).AsObject();
            Assert.Equal(1, (short)jObject["MyInt16"]);
            Assert.Equal(2, (int)jObject["MyInt32"]);
            Assert.Equal(3, (long)jObject["MyInt64"]);
            Assert.Equal(4, (ushort)jObject["MyUInt16"]);
            Assert.Equal<uint>(5, (uint)jObject["MyUInt32"]);
            Assert.Equal<ulong>(6, (ulong)jObject["MyUInt64"]);
            Assert.Equal(7, (byte)jObject["MyByte"]);
            Assert.Equal(8, (sbyte)jObject["MySByte"]);
            Assert.Equal('a', (char)jObject["MyChar"]);
            Assert.Equal("Hello", (string)jObject["MyString"]);
            Assert.True((bool)jObject["MyBooleanTrue"]);
            Assert.False((bool)jObject["MyBooleanFalse"]);
            Assert.Equal(1.1f, (float)jObject["MySingle"]);
            Assert.Equal(2.2d, (double)jObject["MyDouble"]);
            Assert.Equal(3.3m, (decimal)jObject["MyDecimal"]);
            Assert.Equal(new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc), (DateTime)jObject["MyDateTime"]);
            Assert.Equal(new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0)), (DateTimeOffset)jObject["MyDateTimeOffset"]);
            Assert.Equal(new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6"), (Guid)jObject["MyGuid"]);
        }

        [Fact]
        public static void ExplicitOperators_FromValues()
        {
            Assert.Equal(1, (short)(YamlNode)(short)1);
            Assert.Equal(2, (int)(YamlNode)2);
            Assert.Equal(3, (long)(YamlNode)(long)3);
            Assert.Equal(4, (ushort)(YamlNode)(ushort)4);
            Assert.Equal<uint>(5, (uint)(YamlNode)(uint)5);
            Assert.Equal<ulong>(6, (ulong)(YamlNode)(ulong)6);
            Assert.Equal(7, (byte)(YamlNode)(byte)7);
            Assert.Equal(8, (sbyte)(YamlNode)(sbyte)8);
            Assert.Equal('a', (char)(YamlNode)'a');
            Assert.Equal("Hello", (string)(YamlNode)"Hello");
            Assert.True((bool)(YamlNode)true);
            Assert.False((bool)(YamlNode)false);
            Assert.Equal(1.1f, (float)(YamlNode)1.1f);
            Assert.Equal(2.2d, (double)(YamlNode)2.2d);
            Assert.Equal(3.3m, (decimal)(YamlNode)3.3m);
            Assert.Equal(new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc),
                (DateTime)(YamlNode)new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc));
            Assert.Equal(new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0)),
                (DateTimeOffset)(YamlNode)new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0)));
            Assert.Equal(new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6"),
                (Guid)(YamlNode)new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6"));
        }

        [Fact]
        public static void ExplicitOperators_FromNullValues()
        {
            Assert.Null((byte?)(YamlValue)null);
            Assert.Null((short?)(YamlValue)null);
            Assert.Null((int?)(YamlValue)null);
            Assert.Null((long?)(YamlValue)null);
            Assert.Null((sbyte?)(YamlValue)null);
            Assert.Null((ushort?)(YamlValue)null);
            Assert.Null((uint?)(YamlValue)null);
            Assert.Null((ulong?)(YamlValue)null);
            Assert.Null((char?)(YamlValue)null);
            Assert.Null((string)(YamlValue)null);
            Assert.Null((bool?)(YamlValue)null);
            Assert.Null((float?)(YamlValue)null);
            Assert.Null((double?)(YamlValue)null);
            Assert.Null((decimal?)(YamlValue)null);
            Assert.Null((DateTime?)(YamlValue)null);
            Assert.Null((DateTimeOffset?)(YamlValue)null);
            Assert.Null((Guid?)(YamlValue)null);
        }

        [Fact]
        public static void ExplicitOperators_FromNullableValues()
        {
            Assert.NotNull((byte?)(YamlValue)(byte)42);
            Assert.NotNull((short?)(YamlValue)(short)42);
            Assert.NotNull((int?)(YamlValue)42);
            Assert.NotNull((long?)(YamlValue)(long)42);
            Assert.NotNull((sbyte?)(YamlValue)(sbyte)42);
            Assert.NotNull((ushort?)(YamlValue)(ushort)42);
            Assert.NotNull((uint?)(YamlValue)(uint)42);
            Assert.NotNull((ulong?)(YamlValue)(ulong)42);
            Assert.NotNull((char?)(YamlValue)'a');
            Assert.NotNull((string)(YamlValue)"");
            Assert.NotNull((bool?)(YamlValue)true);
            Assert.NotNull((float?)(YamlValue)(float)42);
            Assert.NotNull((double?)(YamlValue)(double)42);
            Assert.NotNull((decimal?)(YamlValue)(decimal)42);
            Assert.NotNull((DateTime?)(YamlValue)new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc));
            Assert.NotNull((DateTimeOffset?)(YamlValue)new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0)));
            Assert.NotNull((Guid?)(YamlValue)new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6"));
        }

        [Fact]
        public static void ImplicitOperators_FromNullValues()
        {
            Assert.Null((YamlValue?)(byte?)null);
            Assert.Null((YamlValue?)(short?)null);
            Assert.Null((YamlValue?)(int?)null);
            Assert.Null((YamlValue?)(long?)null);
            Assert.Null((YamlValue?)(sbyte?)null);
            Assert.Null((YamlValue?)(ushort?)null);
            Assert.Null((YamlValue?)(uint?)null);
            Assert.Null((YamlValue?)(ulong?)null);
            Assert.Null((YamlValue?)(char?)null);
            Assert.Null((YamlValue?)(string)null);
            Assert.Null((YamlValue?)(bool?)null);
            Assert.Null((YamlValue?)(float?)null);
            Assert.Null((YamlValue?)(double?)null);
            Assert.Null((YamlValue?)(decimal?)null);
            Assert.Null((YamlValue?)(DateTime?)null);
            Assert.Null((YamlValue?)(DateTimeOffset?)null);
            Assert.Null((YamlValue?)(Guid?)null);
        }

        [Fact]
        public static void ImplicitOperators_FromNullableValues()
        {
            #pragma warning disable xUnit2002
            Assert.NotNull((YamlValue?)(byte?)42);
            Assert.NotNull((YamlValue?)(short?)42);
            Assert.NotNull((YamlValue?)(int?)42);
            Assert.NotNull((YamlValue?)(long?)42);
            Assert.NotNull((YamlValue?)(sbyte?)42);
            Assert.NotNull((YamlValue?)(ushort?)42);
            Assert.NotNull((YamlValue?)(uint?)42);
            Assert.NotNull((YamlValue?)(ulong?)42);
            Assert.NotNull((YamlValue?)(char?)'a');
            Assert.NotNull((YamlValue?)(bool?)true);
            Assert.NotNull((YamlValue?)(float?)42);
            Assert.NotNull((YamlValue?)(double?)42);
            Assert.NotNull((YamlValue?)(decimal?)42);
            #pragma warning restore xUnit2002
            Assert.NotNull((YamlValue?)(DateTime?)new DateTime(2019, 1, 30, 12, 1, 2, DateTimeKind.Utc));
            Assert.NotNull((YamlValue?)(DateTimeOffset?)new DateTimeOffset(2019, 1, 30, 12, 1, 2, new TimeSpan(1, 0, 0)));
            Assert.NotNull((YamlValue?)(Guid?)new Guid("1B33498A-7B7D-4DDA-9C13-F6AA4AB449A6"));
        }

        [Fact]
        public static void CastsNotSupported()
        {
            // Since generics and boxing do not support casts, we get InvalidCastExceptions here.
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => (byte)(YamlNode)(long)3); // narrowing
            // "A value of type 'System.Int64' cannot be converted to a 'System.Byte'."
            Assert.Contains(typeof(long).ToString(), ex.Message);
            Assert.Contains(typeof(byte).ToString(), ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => (long)(YamlNode)(byte)3); // widening
            // "A value of type 'System.Byte' cannot be converted to a 'System.Int64'."
            Assert.Contains(typeof(byte).ToString(), ex.Message);
            Assert.Contains(typeof(long).ToString(), ex.Message);
        }

        [Fact]
        public static void Boxing()
        {
            var node = YamlValue.Create(42);
            Assert.Equal(42, node.GetValue<int>());

            Assert.Equal<object>(42, node.GetValue<object>());
        }
    }
}
