// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class Utf8YamlReaderTests
    {
        [Fact]
        public static void TestingNumbers_TryGetMethods()
        {
            byte[] dataUtf8 = YamlNumberTestData.YamlData;
            List<byte> bytes = YamlNumberTestData.Bytes;
            List<sbyte> sbytes = YamlNumberTestData.SBytes;
            List<short> shorts = YamlNumberTestData.Shorts;
            List<int> ints = YamlNumberTestData.Ints;
            List<long> longs = YamlNumberTestData.Longs;
            List<ushort> ushorts = YamlNumberTestData.UShorts;
            List<uint> uints = YamlNumberTestData.UInts;
            List<ulong> ulongs = YamlNumberTestData.ULongs;
            List<float> floats = YamlNumberTestData.Floats;
            List<double> doubles = YamlNumberTestData.Doubles;
            List<decimal> decimals = YamlNumberTestData.Decimals;

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            string key = "";
            int count = 0;
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    key = yaml.GetString();
                }
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    if (key.StartsWith("byte"))
                    {
                        Assert.True(yaml.TryGetByte(out byte numberByte));
                        if (count >= bytes.Count)
                            count = 0;
                        Assert.Equal(bytes[count], numberByte);
                        count++;
                    }
                    else if (key.StartsWith("sbyte"))
                    {
                        Assert.True(yaml.TryGetSByte(out sbyte numberSByte));
                        if (count >= sbytes.Count)
                            count = 0;
                        Assert.Equal(sbytes[count], numberSByte);
                        count++;
                    }
                    else if (key.StartsWith("short"))
                    {
                        Assert.True(yaml.TryGetInt16(out short numberShort));
                        if (count >= shorts.Count)
                            count = 0;
                        Assert.Equal(shorts[count], numberShort);
                        count++;
                    }
                    else if (key.StartsWith("int"))
                    {
                        Assert.True(yaml.TryGetInt32(out int numberInt));
                        if (count >= ints.Count)
                            count = 0;
                        Assert.Equal(ints[count], numberInt);
                        count++;
                    }
                    else if (key.StartsWith("long"))
                    {
                        Assert.True(yaml.TryGetInt64(out long numberLong));
                        if (count >= longs.Count)
                            count = 0;
                        Assert.Equal(longs[count], numberLong);
                        count++;
                    }
                    else if (key.StartsWith("ushort"))
                    {
                        Assert.True(yaml.TryGetUInt16(out ushort numberUShort));
                        if (count >= ushorts.Count)
                            count = 0;
                        Assert.Equal(ushorts[count], numberUShort);
                        count++;
                    }
                    else if (key.StartsWith("uint"))
                    {
                        Assert.True(yaml.TryGetUInt32(out uint numberUInt));
                        if (count >= ints.Count)
                            count = 0;
                        Assert.Equal(uints[count], numberUInt);
                        count++;
                    }
                    else if (key.StartsWith("ulong"))
                    {
                        Assert.True(yaml.TryGetUInt64(out ulong numberULong));
                        if (count >= ints.Count)
                            count = 0;
                        Assert.Equal(ulongs[count], numberULong);
                        count++;
                    }
                    else if (key.StartsWith("float"))
                    {
                        Assert.True(yaml.TryGetSingle(out float numberFloat));
                        if (count >= floats.Count)
                            count = 0;

                        string roundTripActual = numberFloat.ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                        float actual = float.Parse(roundTripActual, CultureInfo.InvariantCulture);

                        string roundTripExpected = floats[count].ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                        float expected = float.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                        Assert.Equal(expected, actual);
                        count++;
                    }
                    else if (key.StartsWith("double"))
                    {
                        Assert.True(yaml.TryGetDouble(out double numberDouble));
                        if (count >= doubles.Count)
                            count = 0;

                        string roundTripActual = numberDouble.ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                        double actual = double.Parse(roundTripActual, CultureInfo.InvariantCulture);

                        string roundTripExpected = doubles[count].ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                        double expected = double.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                        Assert.Equal(expected, actual);
                        count++;
                    }
                    else if (key.StartsWith("decimal"))
                    {
                        Assert.True(yaml.TryGetDecimal(out decimal numberDecimal));
                        if (count >= decimals.Count)
                            count = 0;

                        var str = string.Format(CultureInfo.InvariantCulture, "{0}", decimals[count]);
                        decimal expected = decimal.Parse(str, CultureInfo.InvariantCulture);
                        Assert.Equal(expected, numberDecimal);
                        count++;
                    }
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Fact]
        public static void TestingNumbers_GetMethods()
        {
            byte[] dataUtf8 = YamlNumberTestData.YamlData;
            List<byte> bytes = YamlNumberTestData.Bytes;
            List<sbyte> sbytes = YamlNumberTestData.SBytes;
            List<short> shorts = YamlNumberTestData.Shorts;
            List<int> ints = YamlNumberTestData.Ints;
            List<long> longs = YamlNumberTestData.Longs;
            List<ushort> ushorts = YamlNumberTestData.UShorts;
            List<uint> uints = YamlNumberTestData.UInts;
            List<ulong> ulongs = YamlNumberTestData.ULongs;
            List<float> floats = YamlNumberTestData.Floats;
            List<double> doubles = YamlNumberTestData.Doubles;
            List<decimal> decimals = YamlNumberTestData.Decimals;

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            string key = "";
            int count = 0;
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    key = yaml.GetString();
                }
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    if (key.StartsWith("byte"))
                    {
                        if (count >= bytes.Count)
                            count = 0;
                        Assert.Equal(bytes[count], yaml.GetByte());
                        count++;
                    }
                    else if (key.StartsWith("sbyte"))
                    {
                        if (count >= sbytes.Count)
                            count = 0;
                        Assert.Equal(sbytes[count], yaml.GetSByte());
                        count++;
                    }
                    else if (key.StartsWith("short"))
                    {
                        if (count >= shorts.Count)
                            count = 0;
                        Assert.Equal(shorts[count], yaml.GetInt16());
                        count++;
                    }
                    else if (key.StartsWith("int"))
                    {
                        if (count >= ints.Count)
                            count = 0;
                        Assert.Equal(ints[count], yaml.GetInt32());
                        count++;
                    }
                    else if (key.StartsWith("long"))
                    {
                        if (count >= longs.Count)
                            count = 0;
                        Assert.Equal(longs[count], yaml.GetInt64());
                        count++;
                    }
                    else if (key.StartsWith("ushort"))
                    {
                        if (count >= ushorts.Count)
                            count = 0;
                        Assert.Equal(ushorts[count], yaml.GetUInt16());
                        count++;
                    }
                    else if (key.StartsWith("uint"))
                    {
                        if (count >= uints.Count)
                            count = 0;
                        Assert.Equal(uints[count], yaml.GetUInt32());
                        count++;
                    }
                    else if (key.StartsWith("ulong"))
                    {
                        if (count >= ulongs.Count)
                            count = 0;
                        Assert.Equal(ulongs[count], yaml.GetUInt64());
                        count++;
                    }
                    else if (key.StartsWith("float"))
                    {
                        if (count >= floats.Count)
                            count = 0;

                        string roundTripActual = yaml.GetSingle().ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                        float actual = float.Parse(roundTripActual, CultureInfo.InvariantCulture);

                        string roundTripExpected = floats[count].ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                        float expected = float.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                        Assert.Equal(expected, actual);
                        count++;
                    }
                    else if (key.StartsWith("double"))
                    {
                        if (count >= doubles.Count)
                            count = 0;

                        string roundTripActual = yaml.GetDouble().ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                        double actual = double.Parse(roundTripActual, CultureInfo.InvariantCulture);

                        string roundTripExpected = doubles[count].ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                        double expected = double.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                        Assert.Equal(expected, actual);
                        count++;
                    }
                    else if (key.StartsWith("decimal"))
                    {
                        try
                        {
                            if (count >= decimals.Count)
                                count = 0;

                            var str = string.Format(CultureInfo.InvariantCulture, "{0}", decimals[count]);
                            decimal expected = decimal.Parse(str, CultureInfo.InvariantCulture);
                            Assert.Equal(expected, yaml.GetDecimal());
                            count++;
                        }
                        catch (Exception except)
                        {
                            Assert.Fail(string.Format("Unexpected exception: {0}. Message: {1}", except.Source, except.Message));
                        }

                    }
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("220.1", 220.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-1", -1)] // byte.MinValue - 1
        public static void TestingNumbersInvalidConversionToByte(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetByte(out byte value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetByte();
                        Assert.Fail("Expected GetByte to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("120.1", 120.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-129", -129)] // sbyte.MinValue - 1
        public static void TestingNumbersInvalidConversionToSByte(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetSByte(out sbyte value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetSByte();
                        Assert.Fail("Expected GetSByte to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-32769", -32769)] // short.MinValue - 1
        public static void TestingNumbersInvalidConversionToInt16(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetInt16(out short value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetInt16();
                        Assert.Fail("Expected GetInt16 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-2147483649", -2147483649)] // int.MinValue - 1
        public static void TestingNumbersInvalidConversionToInt32(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetInt32(out int value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetInt32();
                        Assert.Fail("Expected GetInt32 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-9223372036854775809", -9223372036854775809d)] // long.MinValue - 1
        public static void TestingNumbersInvalidConversionToInt64(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetInt64(out long value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetInt64();
                        Assert.Fail("Expected GetInt64 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-1", -1)] // ushort.MinValue - 1
        public static void TestingNumbersInvalidConversionToUInt16(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetUInt16(out ushort value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetUInt16();
                        Assert.Fail("Expected GetUInt16 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("12345678901", 12345678901)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-1", -1)] // uint.MinValue - 1
        public static void TestingNumbersInvalidConversionToUInt32(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetUInt32(out uint value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetUInt32();
                        Assert.Fail("Expected GetUInt32 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("0.000", 0)]
        [InlineData("1e1", 10)]
        [InlineData("1.1e2", 110)]
        [InlineData("12345.1", 12345.1)]
        [InlineData("123456789012345678901", 123456789012345678901d)]
        [InlineData("-1", -1)] // ulong.MinValue - 1
        public static void TestingNumbersInvalidConversionToUInt64(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetUInt64(out ulong value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetUInt64();
                        Assert.Fail("Expected GetInt64 to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("-4.402823E+38", float.NegativeInfinity, -4.402823E+38)] // float.MinValue - 1
        [InlineData("4.402823E+38", float.PositiveInfinity, 4.402823E+38)]  // float.MaxValue + 1
        public static void TestingTooLargeSingleConversionToInfinity(string yamlString, float expectedFloat, double expectedDouble)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    if (PlatformDetection.IsNetFramework)
                    {
                        // .NET Framework throws for overflow rather than returning Infinity
                        // This was fixed for .NET Core 3.0 in order to be IEEE 754 compliant

                        Assert.False(yaml.TryGetSingle(out float _));

                        try
                        {
                            yaml.GetSingle();
                            Assert.Fail($"Expected {nameof(FormatException)}.");
                        }
                        catch (FormatException)
                        {
                            /* Expected exception */
                        }
                    }
                    else
                    {
                        Assert.True(yaml.TryGetSingle(out float floatValue));
                        Assert.Equal(expectedFloat, floatValue);

                        Assert.Equal(expectedFloat, yaml.GetSingle());
                    }

                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expectedDouble, doubleValue);
                    Assert.Equal(expectedDouble, yaml.GetDouble());
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("-2.79769313486232E+308", double.NegativeInfinity)] // double.MinValue - 1
        [InlineData("2.79769313486232E+308", double.PositiveInfinity)]  // double.MaxValue + 1
        public static void TestingTooLargeDoubleConversionToInfinity(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    if (PlatformDetection.IsNetFramework)
                    {
                        // .NET Framework throws for overflow rather than returning Infinity
                        // This was fixed for .NET Core 3.0 in order to be IEEE 754 compliant

                        Assert.False(yaml.TryGetDouble(out double _));

                        try
                        {
                            yaml.GetDouble();
                            Assert.Fail($"Expected {nameof(FormatException)}.");
                        }
                        catch (FormatException)
                        {
                            /* Expected exception */
                        }
                    }
                    else
                    {
                        Assert.True(yaml.TryGetDouble(out double actual));
                        Assert.Equal(expected, actual);

                        Assert.Equal(expected, yaml.GetDouble());
                    }
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("-79228162514264337593543950336", -79228162514264337593543950336d)] // decimal.MinValue - 1
        [InlineData("79228162514264337593543950336", 79228162514264337593543950336d)]  // decimal.MaxValue + 1
        public static void TestingNumbersInvalidConversionToDecimal(string yamlString, double expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.Number)
                {
                    Assert.False(yaml.TryGetDecimal(out decimal value));
                    Assert.Equal(default, value);
                    Assert.True(yaml.TryGetDouble(out double doubleValue));
                    Assert.Equal(expected, doubleValue);

                    try
                    {
                        yaml.GetDecimal();
                        Assert.Fail("Expected GetDecimal to throw FormatException.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                    doubleValue = yaml.GetDouble();
                    Assert.Equal(expected, doubleValue);
                }
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("{\"message\":\"Hello, I am \\\"Ahson!\\\"\"}")]
        [InlineData("{\"nam\\\"e\":\"ah\\\"son\"}")]
        [InlineData("{\"Here is a string: \\\"\\\"\":\"Here is a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str\":\"\\\"\\\"\"}")]
        [InlineData("[\"\\u0030\\u0031\\u0032\\u0033\\u0034\\u0035\", \"\\u0000\\u002B\", \"a\\u005C\\u0072b\", \"a\\\\u005C\\u0072b\", \"a\\u008E\\u008Fb\", \"a\\uD803\\uDE6Db\", \"a\\uD834\\uDD1Eb\", \"a\\\\uD834\\\\uDD1Eb\"]")]
        [InlineData("{\"message\":\"Hello /a/b/c \\/ \\r\\b\\n\\f\\t\\/\"}")]
        [InlineData(null)]  // Large randomly generated string
        public static void TestingGetString(string? yamlString)
        {
            if (yamlString == null)
            {
                var random = new Random(42);
                var charArray = new char[500];
                charArray[0] = '"';
                for (int i = 1; i < charArray.Length; i++)
                {
                    charArray[i] = (char)random.Next('?', '\\'); // ASCII values (between 63 and 91) that don't need to be escaped.
                }

                charArray[256] = '\\';
                charArray[257] = '"';
                charArray[charArray.Length - 1] = '"';
                yamlString = new string(charArray);
            }

            var expectedPropertyNames = new List<string>();
            var expectedValues = new List<string>();

            var yamlNewtonsoft = new JsonTextReader(new StringReader(yamlString)) { MaxDepth = null };
            while (yamlNewtonsoft.Read())
            {
                if (yamlNewtonsoft.TokenType == JsonToken.String)
                {
                    expectedValues.Add(yamlNewtonsoft.Value.ToString());
                }
                else if (yamlNewtonsoft.TokenType == JsonToken.PropertyName)
                {
                    expectedPropertyNames.Add(yamlNewtonsoft.Value.ToString());
                }
            }

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var actualPropertyNames = new List<string>();
            var actualValues = new List<string>();

            var yaml = new Utf8YamlReader(dataUtf8, true, default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    actualValues.Add(yaml.GetString());
                }
                else if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    actualPropertyNames.Add(yaml.GetString());
                }
            }

            Assert.Equal(expectedPropertyNames.Count, actualPropertyNames.Count);
            for (int i = 0; i < expectedPropertyNames.Count; i++)
            {
                Assert.Equal(expectedPropertyNames[i], actualPropertyNames[i]);
            }

            Assert.Equal(expectedValues.Count, actualValues.Count);
            for (int i = 0; i < expectedValues.Count; i++)
            {
                Assert.Equal(expectedValues[i], actualValues[i]);
            }

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [InlineData("\"a\\uDD1E\"")]
        [InlineData("\"a\\uDD1Eb\"")]
        [InlineData("\"a\\uD834\"")]
        [InlineData("\"a\\uD834\\u0030\"")]
        [InlineData("\"a\\uD834\\uD834\"")]
        [InlineData("\"a\\uD834b\"")]
        [InlineData("\"a\\uDD1E\\uD834b\"")]
        [InlineData("\"a\\\\uD834\\uDD1Eb\"")]
        [InlineData("\"a\\uDD1E\\\\uD834b\"")]
        public static void TestingGetStringInvalidUTF16(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.String, yaml.TokenType);

                YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetString());
                YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.CopyString(new byte[6 * yamlString.Length]));
                YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.CopyString(new char[6 * yamlString.Length]));
            }
        }

        [Theory]
        [MemberData(nameof(InvalidUTF8Strings))]
        public static void TestingGetStringInvalidUTF8(byte[] dataUtf8)
        {
            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                // It is expected that the Utf8YamlReader won't throw an exception here
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.String, yaml.TokenType);

                while (yaml.Read())
                    ;

                yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                while (yaml.Read())
                {
                    if (yaml.TokenType == YamlTokenType.String)
                    {
                        int length = yaml.HasValueSequence ? (int)yaml.ValueSequence.Length : yaml.ValueSpan.Length;

                        InvalidOperationException ex = YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.GetString());
                        if (ex.InnerException is not null)
                        {
                            Assert.IsType<DecoderFallbackException>(ex.InnerException);
                        }

                        ex = YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.CopyString(new byte[length]));
                        if (ex.InnerException is not null)
                        {
                            Assert.IsType<DecoderFallbackException>(ex.InnerException);
                        }

                        ex = YamlTestHelper.AssertThrows<InvalidOperationException>(ref yaml, (ref Utf8YamlReader yaml) => yaml.CopyString(new char[length]));
                        if (ex.InnerException is not null)
                        {
                            Assert.IsType<DecoderFallbackException>(ex.InnerException);
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("1", "1")]
        [InlineData("this is a string without escaping", "this is a string without escaping")]
        [InlineData(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
        [InlineData("""\n\r\"\\\/\t\b\f\u0061""", "\n\r\"\\/\t\b\fa")]
        public static void CopyString_Utf8_SuccessPath(string yamlString, string expectedOutput)
        {
            string yaml = $"\"{yamlString}\"";
            byte[] expectedOutputUtf8 = Encoding.UTF8.GetBytes(expectedOutput);
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Span<byte> destination = new byte[128];
                int bytesWritten = reader.CopyString(destination);
                Assert.Equal(expectedOutputUtf8.Length, bytesWritten);
                Assert.True(expectedOutputUtf8.AsSpan().SequenceEqual(destination.Slice(0, bytesWritten)));
            }
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("1", "1")]
        [InlineData("this is a string without escaping", "this is a string without escaping")]
        [InlineData(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
        [InlineData("""\n\r\"\\\/\t\b\f\u0061""", "\n\r\"\\/\t\b\fa")]
        public static void CopyString_Utf16_SuccessPath(string yamlString, string expectedOutput)
        {
            string yaml = $"\"{yamlString}\"";
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());
                Span<char> destination = new char[128];
                int charsWritten = reader.CopyString(destination);
                Assert.Equal(expectedOutput.Length, charsWritten);
                Assert.True(expectedOutput.AsSpan().SequenceEqual(destination.Slice(0, charsWritten)));
            }
        }

        [Theory]
        [InlineData("this is a string without escaping", "this is a string without escaping")]
        [InlineData(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
        [InlineData("""\n\r\"\\\/\t\b\f\u0061""", "\n\r\"\\/\t\b\fa")]
        public static void CopyString_Utf8_DestinationTooShort_ThrowsArgumentException(string yamlString, string expectedOutput)
        {
            int expectedUtf8Size = Encoding.UTF8.GetByteCount(expectedOutput);
            string yaml = $"\"{yamlString}\"";
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());

                byte[] buffer = new byte[expectedUtf8Size];
                for (int i = 0; i < expectedUtf8Size; i++)
                {
                    YamlTestHelper.AssertThrows<ArgumentException>(ref reader, (ref Utf8YamlReader reader) => reader.CopyString(buffer.AsSpan(0, i)));
                    Assert.All(buffer, static b => Assert.Equal(0, b));
                }

                Assert.Equal(expectedUtf8Size, reader.CopyString(buffer));
            }
        }

        [Theory]
        [InlineData("this is a string without escaping", "this is a string without escaping")]
        [InlineData(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
        [InlineData("""\n\r\"\\\/\t\b\f\u0061""", "\n\r\"\\/\t\b\fa")]
        public static void CopyString_Utf16_DestinationTooShort_ThrowsArgumentException(string yamlString, string expectedOutput)
        {
            int expectedSize = expectedOutput.Length;
            string yaml = $"\"{yamlString}\"";
            YamlTestHelper.AssertWithSingleAndMultiSegmentReader(yaml, Test);

            void Test(ref Utf8YamlReader reader)
            {
                Assert.True(reader.Read());

                char[] buffer = new char[expectedSize];
                for (int i = 0; i < expectedSize; i++)
                {
                    YamlTestHelper.AssertThrows<ArgumentException>(ref reader, (ref Utf8YamlReader reader) => reader.CopyString(buffer.AsSpan(0, i)));
                    Assert.All(buffer, static c => Assert.Equal(0, c));
                }

                Assert.Equal(expectedSize, reader.CopyString(buffer));
            }
        }

        [Fact]
        public static void GetBase64Unescapes()
        {
            string yamlString = "\"\\u0031234\""; // equivalent to "\"1234\""

            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());

            byte[] expected = Convert.FromBase64String("1234"); // new byte[3] { 215, 109, 248 }

            byte[] value = yaml.GetBytesFromBase64();
            Assert.Equal(expected, value);
            Assert.True(yaml.TryGetBytesFromBase64(out value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [MemberData(nameof(YamlBase64TestData.ValidBase64Tests), MemberType = typeof(YamlBase64TestData))]
        public static void ValidBase64(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());

            byte[] expected = Convert.FromBase64String(yamlString.AsSpan(1, yamlString.Length - 2).ToString());

            byte[] value = yaml.GetBytesFromBase64();
            Assert.Equal(expected, value);
            Assert.True(yaml.TryGetBytesFromBase64(out value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [MemberData(nameof(YamlBase64TestData.InvalidBase64Tests), MemberType = typeof(YamlBase64TestData))]
        public static void InvalidBase64(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());
            Assert.False(yaml.TryGetBytesFromBase64(out byte[] value));
            Assert.Null(value);

            try
            {
                byte[] val = yaml.GetBytesFromBase64();
                Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
            }
            catch (FormatException) { }
        }

        [Theory]
        [InlineData("\"a\\uDD1E\"")]
        [InlineData("\"a\\uDD1Eb\"")]
        [InlineData("\"a\\uD834\"")]
        [InlineData("\"a\\uD834\\u0030\"")]
        [InlineData("\"a\\uD834\\uD834\"")]
        [InlineData("\"a\\uD834b\"")]
        [InlineData("\"a\\uDD1E\\uD834b\"")]
        [InlineData("\"a\\\\uD834\\uDD1Eb\"")]
        [InlineData("\"a\\uDD1E\\\\uD834b\"")]
        public static void TestingGetBase64InvalidUTF16(string yamlString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlString);

            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.String, yaml.TokenType);
                try
                {
                    byte[] val = yaml.GetBytesFromBase64();
                    Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
                }
                catch (InvalidOperationException) { }

                try
                {
                    yaml.TryGetBytesFromBase64(out byte[] val);
                    Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
                }
                catch (InvalidOperationException) { }
            }
        }

        [Theory]
        [MemberData(nameof(InvalidUTF8Strings))]
        public static void TestingGetBase64InvalidUTF8(byte[] dataUtf8)
        {
            foreach (YamlCommentHandling commentHandling in Enum.GetValues(typeof(YamlCommentHandling)))
            {
                var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
                var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                // It is expected that the Utf8YamlReader won't throw an exception here
                Assert.True(yaml.Read());
                Assert.Equal(YamlTokenType.String, yaml.TokenType);

                while (yaml.Read())
                    ;

                yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

                while (yaml.Read())
                {
                    if (yaml.TokenType == YamlTokenType.String)
                    {
                        try
                        {
                            byte[] val = yaml.GetBytesFromBase64();
                            Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-8 JSON text.");
                        }
                        catch (FormatException) { }

                        Assert.False(yaml.TryGetBytesFromBase64(out byte[] value));
                        Assert.Null(value);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetCommentTestData))]
        public static void TestingGetComment(string yamlData, string expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlData);
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            var reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);

            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.StartObject, reader.TokenType);

            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.Comment, reader.TokenType);
            Assert.Equal(expected, reader.GetComment());

            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

            Assert.False(reader.Read());
        }

        [Theory]
        [MemberData(nameof(GetCommentUnescapeData))]
        public static void TestGetCommentUnescape(string yamlData, string expected)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(yamlData);
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = YamlCommentHandling.Allow });
            var reader = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state);
            bool commentFound = false;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case YamlTokenType.Comment:
                        commentFound = true;
                        string comment = reader.GetComment();
                        Assert.Equal(expected, comment);
                        Assert.NotEqual(Regex.Unescape(expected), comment);
                        break;
                    default:
                        Assert.Fail();
                        break;
                }
            }
            Assert.True(commentFound);
        }

        [Theory]
        [MemberData(nameof(YamlGuidTestData.ValidGuidTests), MemberType = typeof(YamlGuidTestData))]
        [MemberData(nameof(YamlGuidTestData.ValidHexGuidTests), MemberType = typeof(YamlGuidTestData))]
        public static void TestingStringsConversionToGuid(string testString, string expectedString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);

            Guid expected = new Guid(expectedString);

            Assert.True(yaml.Read(), "Read string value");
            Assert.Equal(YamlTokenType.String, yaml.TokenType);

            Assert.True(yaml.TryGetGuid(out Guid actual));
            Assert.Equal(expected, actual);
            Assert.Equal(expected, yaml.GetGuid());

            Assert.Equal(dataUtf8.Length, yaml.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(YamlGuidTestData.InvalidGuidTests), MemberType = typeof(YamlGuidTestData))]
        public static void TestingStringsInvalidConversionToGuid(string testString)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
            var yaml = new Utf8YamlReader(dataUtf8, isFinalBlock: true, state: default);

            Assert.True(yaml.Read(), "Read string value");
            Assert.Equal(YamlTokenType.String, yaml.TokenType);

            Assert.False(yaml.TryGetGuid(out Guid actual));
            Assert.Equal(default, actual);

            YamlTestHelper.AssertThrows<FormatException>(ref yaml, (ref Utf8YamlReader yamlReader) => yamlReader.GetGuid());
        }

        [Fact]
        public static void TestNumericEdgeCases_Get_Methods()
        {
            byte[] data1 = Encoding.UTF8.GetBytes("0");
            var reader1 = new Utf8YamlReader(data1, isFinalBlock: true, state: default);
            Assert.True(reader1.Read());
            Assert.Equal(0, reader1.GetByte());
            
            byte[] data2 = Encoding.UTF8.GetBytes("255");
            var reader2 = new Utf8YamlReader(data2, isFinalBlock: true, state: default);
            Assert.True(reader2.Read());
            Assert.Equal(255, reader2.GetByte());
            
            byte[] data3 = Encoding.UTF8.GetBytes("-128");
            var reader3 = new Utf8YamlReader(data3, isFinalBlock: true, state: default);
            Assert.True(reader3.Read());
            Assert.Equal(-128, reader3.GetSByte());
            
            byte[] data4 = Encoding.UTF8.GetBytes("127");
            var reader4 = new Utf8YamlReader(data4, isFinalBlock: true, state: default);
            Assert.True(reader4.Read());
            Assert.Equal(127, reader4.GetSByte());
            
            byte[] data5 = Encoding.UTF8.GetBytes("-32768");
            var reader5 = new Utf8YamlReader(data5, isFinalBlock: true, state: default);
            Assert.True(reader5.Read());
            Assert.Equal(-32768, reader5.GetInt16());
            
            byte[] data6 = Encoding.UTF8.GetBytes("32767");
            var reader6 = new Utf8YamlReader(data6, isFinalBlock: true, state: default);
            Assert.True(reader6.Read());
            Assert.Equal(32767, reader6.GetInt16());
            
            byte[] data7 = Encoding.UTF8.GetBytes("-2147483648");
            var reader7 = new Utf8YamlReader(data7, isFinalBlock: true, state: default);
            Assert.True(reader7.Read());
            Assert.Equal(-2147483648, reader7.GetInt32());
            
            byte[] data8 = Encoding.UTF8.GetBytes("2147483647");
            var reader8 = new Utf8YamlReader(data8, isFinalBlock: true, state: default);
            Assert.True(reader8.Read());
            Assert.Equal(2147483647, reader8.GetInt32());
            
            byte[] data9 = Encoding.UTF8.GetBytes("-9223372036854775808");
            var reader9 = new Utf8YamlReader(data9, isFinalBlock: true, state: default);
            Assert.True(reader9.Read());
            Assert.Equal(-9223372036854775808, reader9.GetInt64());
            
            byte[] data10 = Encoding.UTF8.GetBytes("9223372036854775807");
            var reader10 = new Utf8YamlReader(data10, isFinalBlock: true, state: default);
            Assert.True(reader10.Read());
            Assert.Equal(9223372036854775807, reader10.GetInt64());
            
            byte[] data11 = Encoding.UTF8.GetBytes("0");
            var reader11 = new Utf8YamlReader(data11, isFinalBlock: true, state: default);
            Assert.True(reader11.Read());
            Assert.Equal(0, reader11.GetUInt16());
            
            byte[] data12 = Encoding.UTF8.GetBytes("65535");
            var reader12 = new Utf8YamlReader(data12, isFinalBlock: true, state: default);
            Assert.True(reader12.Read());
            Assert.Equal(65535, reader12.GetUInt16());
            
            byte[] data13 = Encoding.UTF8.GetBytes("0");
            var reader13 = new Utf8YamlReader(data13, isFinalBlock: true, state: default);
            Assert.True(reader13.Read());
            Assert.Equal(0u, reader13.GetUInt32());
            
            byte[] data14 = Encoding.UTF8.GetBytes("4294967295");
            var reader14 = new Utf8YamlReader(data14, isFinalBlock: true, state: default);
            Assert.True(reader14.Read());
            Assert.Equal(4294967295u, reader14.GetUInt32());
            
            byte[] data15 = Encoding.UTF8.GetBytes("0");
            var reader15 = new Utf8YamlReader(data15, isFinalBlock: true, state: default);
            Assert.True(reader15.Read());
            Assert.Equal(0ul, reader15.GetUInt64());
            
            byte[] data16 = Encoding.UTF8.GetBytes("18446744073709551615");
            var reader16 = new Utf8YamlReader(data16, isFinalBlock: true, state: default);
            Assert.True(reader16.Read());
            Assert.Equal(18446744073709551615ul, reader16.GetUInt64());
            
            byte[] data17 = Encoding.UTF8.GetBytes("0");
            var reader17 = new Utf8YamlReader(data17, isFinalBlock: true, state: default);
            Assert.True(reader17.Read());
            Assert.Equal(0m, reader17.GetDecimal());
        }

        [Fact]
        public static void TestFloatingPointEdgeCases_Get_Methods()
        {
            byte[] data1 = Encoding.UTF8.GetBytes("0.0");
            var reader1 = new Utf8YamlReader(data1, isFinalBlock: true, state: default);
            Assert.True(reader1.Read());
            Assert.Equal(0.0f, reader1.GetSingle());
            
            byte[] data2 = Encoding.UTF8.GetBytes("3.4028235E+38");
            var reader2 = new Utf8YamlReader(data2, isFinalBlock: true, state: default);
            Assert.True(reader2.Read());
            float result2 = reader2.GetSingle();
            Assert.True(Math.Abs(result2 - 3.4028235E+38f) < 1E+31f);
            
            byte[] data3 = Encoding.UTF8.GetBytes("-3.4028235E+38");
            var reader3 = new Utf8YamlReader(data3, isFinalBlock: true, state: default);
            Assert.True(reader3.Read());
            float result3 = reader3.GetSingle();
            Assert.True(Math.Abs(result3 - (-3.4028235E+38f)) < 1E+31f);
            
            byte[] data4 = Encoding.UTF8.GetBytes("0.0");
            var reader4 = new Utf8YamlReader(data4, isFinalBlock: true, state: default);
            Assert.True(reader4.Read());
            Assert.Equal(0.0, reader4.GetDouble());
            
            byte[] data5 = Encoding.UTF8.GetBytes("1.7976931348623157E+308");
            var reader5 = new Utf8YamlReader(data5, isFinalBlock: true, state: default);
            Assert.True(reader5.Read());
            double result5 = reader5.GetDouble();
            Assert.True(Math.Abs(result5 - 1.7976931348623157E+308) < 1E+300);
            
            byte[] data6 = Encoding.UTF8.GetBytes("-1.7976931348623157E+308");
            var reader6 = new Utf8YamlReader(data6, isFinalBlock: true, state: default);
            Assert.True(reader6.Read());
            double result6 = reader6.GetDouble();
            Assert.True(Math.Abs(result6 - (-1.7976931348623157E+308)) < 1E+300);
        }
    }
}
