// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.XUnitExtensions;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ValueTests
    {
        [Fact]
        public static void ReadPrimitives()
        {
            int i = YamlSerializer.Deserialize<int>(Encoding.UTF8.GetBytes(@"1"));
            Assert.Equal(1, i);

            int i2 = YamlSerializer.Deserialize<int>("2");
            Assert.Equal(2, i2);

            int? i3 = YamlSerializer.Deserialize<int?>("null");
            Assert.Null(i3);

            long l = YamlSerializer.Deserialize<long>(Encoding.UTF8.GetBytes(long.MaxValue.ToString()));
            Assert.Equal(long.MaxValue, l);

            long l2 = YamlSerializer.Deserialize<long>(long.MaxValue.ToString());
            Assert.Equal(long.MaxValue, l2);

            string s = YamlSerializer.Deserialize<string>(Encoding.UTF8.GetBytes("""
                "Hello"
                """));
            Assert.Equal("Hello", s);

            string s2 = YamlSerializer.Deserialize<string>("""
                "Hello"
                """);
            Assert.Equal("Hello", s2);

            Uri u = YamlSerializer.Deserialize<Uri>("""
                ""
                """);
            Assert.Equal("", u.OriginalString);
        }

        [Fact]
        public static void ReadPrimitivesWithWhitespace()
        {
            int i = YamlSerializer.Deserialize<int>(Encoding.UTF8.GetBytes(@" 1 "));
            Assert.Equal(1, i);

            int i2 = YamlSerializer.Deserialize<int>("2\t");
            Assert.Equal(2, i2);

            int? i3 = YamlSerializer.Deserialize<int?>("\r\nnull");
            Assert.Null(i3);

            long l = YamlSerializer.Deserialize<long>(Encoding.UTF8.GetBytes("\t" + long.MaxValue.ToString()));
            Assert.Equal(long.MaxValue, l);

            long l2 = YamlSerializer.Deserialize<long>(long.MaxValue.ToString() + " \r\n");
            Assert.Equal(long.MaxValue, l2);

            string s = YamlSerializer.Deserialize<string>(Encoding.UTF8.GetBytes("\"Hello\" "));
            Assert.Equal("Hello", s);

            string s2 = YamlSerializer.Deserialize<string>("""  "Hello" """);
            Assert.Equal("Hello", s2);

            bool b = YamlSerializer.Deserialize<bool>(" \ttrue ");
            Assert.True(b);

            bool b2 = YamlSerializer.Deserialize<bool>(" false\n");
            Assert.False(b2);
        }

        [Fact]
        public static void ReadPrimitivesFail()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>(Encoding.UTF8.GetBytes(@"a")));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>(Encoding.UTF8.GetBytes("[1,a]")));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>(@"null"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("""
                ""
                """));

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DateTime>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DateTimeOffset>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TimeSpan>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Guid>("\"abc\""));

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<byte>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<byte>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<sbyte>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<sbyte>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<short>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<short>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ushort>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ushort>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<uint>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<uint>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<long>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<long>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ulong>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ulong>("1.1"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<float>("\"abc\""));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<double>("\"abc\""));
        }

        [Theory]
        [InlineData(typeof(bool))]
        [InlineData(typeof(byte))]
        [InlineData(typeof(char))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(DateTimeOffset))]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(double))]
        [InlineData(typeof(YamlTokenType))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(short))]
        [InlineData(typeof(int))]
        [InlineData(typeof(long))]
        [InlineData(typeof(sbyte))]
        [InlineData(typeof(float))]
        [InlineData(typeof(string))]
        [InlineData(typeof(TimeSpan))]
        [InlineData(typeof(ushort))]
        [InlineData(typeof(uint))]
        [InlineData(typeof(ulong))]
        [InlineData(typeof(Uri))]
        [InlineData(typeof(Version))]
        public static void PrimitivesShouldFailWithArrayOrObjectAssignment(Type primitiveType)
        {
            // This test lines up with the built in YamlConverters
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("[]", primitiveType));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("{}", primitiveType));
        }

        [Fact]
        public static void EmptyStringInput()
        {
            string obj = YamlSerializer.Deserialize<string>("""
                ""
                """);
            Assert.Equal(string.Empty, obj);
        }

        [Fact]
        public static void ReadPrimitiveExtraBytesFail()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>("[2] {3}"));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>(Encoding.UTF8.GetBytes("[2] {3}")));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<string>("""
                "Hello" 42
                """));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<string>(Encoding.UTF8.GetBytes("""
                "Hello" 42
                """)));
        }

        [Fact]
        public static void RangePass()
        {
            Assert.Equal(byte.MaxValue, YamlSerializer.Deserialize<byte>(byte.MaxValue.ToString()));
            Assert.Equal(byte.MaxValue, YamlSerializer.Deserialize<byte?>(byte.MaxValue.ToString()));

            Assert.Equal(sbyte.MaxValue, YamlSerializer.Deserialize<sbyte>(sbyte.MaxValue.ToString()));
            Assert.Equal(sbyte.MaxValue, YamlSerializer.Deserialize<sbyte?>(sbyte.MaxValue.ToString()));

            Assert.Equal(short.MaxValue, YamlSerializer.Deserialize<short>(short.MaxValue.ToString()));
            Assert.Equal(short.MaxValue, YamlSerializer.Deserialize<short?>(short.MaxValue.ToString()));

            Assert.Equal(ushort.MaxValue, YamlSerializer.Deserialize<ushort>(ushort.MaxValue.ToString()));
            Assert.Equal(ushort.MaxValue, YamlSerializer.Deserialize<ushort?>(ushort.MaxValue.ToString()));

            Assert.Equal(int.MaxValue, YamlSerializer.Deserialize<int>(int.MaxValue.ToString()));
            Assert.Equal(int.MaxValue, YamlSerializer.Deserialize<int?>(int.MaxValue.ToString()));

            Assert.Equal(uint.MaxValue, YamlSerializer.Deserialize<uint>(uint.MaxValue.ToString()));
            Assert.Equal(uint.MaxValue, YamlSerializer.Deserialize<uint?>(uint.MaxValue.ToString()));

            Assert.Equal(long.MaxValue, YamlSerializer.Deserialize<long>(long.MaxValue.ToString()));
            Assert.Equal(long.MaxValue, YamlSerializer.Deserialize<long?>(long.MaxValue.ToString()));

            Assert.Equal(ulong.MaxValue, YamlSerializer.Deserialize<ulong>(ulong.MaxValue.ToString()));
            Assert.Equal(ulong.MaxValue, YamlSerializer.Deserialize<ulong?>(ulong.MaxValue.ToString()));

            Assert.Equal(decimal.MaxValue, YamlSerializer.Deserialize<decimal>(decimal.MaxValue.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal(decimal.MaxValue, YamlSerializer.Deserialize<decimal?>(decimal.MaxValue.ToString(CultureInfo.InvariantCulture)));
        }

        [Fact]
        public static void RangePassFloatingPoint()
        {
            // Verify overflow\underflow.
            AssertFloatingPointBehavior(netcoreExpectedValue: float.NegativeInfinity, () => YamlSerializer.Deserialize<float>(float.MinValue.ToString(CultureInfo.InvariantCulture) + "0"));
            AssertFloatingPointBehavior(netcoreExpectedValue: float.PositiveInfinity, () => YamlSerializer.Deserialize<float>(float.MaxValue.ToString(CultureInfo.InvariantCulture) + "0"));
            AssertFloatingPointBehavior(netcoreExpectedValue: float.NegativeInfinity, () => YamlSerializer.Deserialize<float?>(float.MinValue.ToString(CultureInfo.InvariantCulture) + "0").Value);
            AssertFloatingPointBehavior(netcoreExpectedValue: float.PositiveInfinity, () => YamlSerializer.Deserialize<float?>(float.MaxValue.ToString(CultureInfo.InvariantCulture) + "0").Value);

            AssertFloatingPointBehavior(netcoreExpectedValue: double.NegativeInfinity, () => YamlSerializer.Deserialize<double>(double.MinValue.ToString(CultureInfo.InvariantCulture) + "0"));
            AssertFloatingPointBehavior(netcoreExpectedValue: double.PositiveInfinity, () => YamlSerializer.Deserialize<double>(double.MaxValue.ToString(CultureInfo.InvariantCulture) + "0"));
            AssertFloatingPointBehavior(netcoreExpectedValue: double.NegativeInfinity, () => YamlSerializer.Deserialize<double?>(double.MinValue.ToString(CultureInfo.InvariantCulture) + "0").Value);
            AssertFloatingPointBehavior(netcoreExpectedValue: double.PositiveInfinity, () => YamlSerializer.Deserialize<double?>(double.MaxValue.ToString(CultureInfo.InvariantCulture) + "0").Value);

            // Verify sign is correct.
            AssertFloatingPointBehavior(netfxExpectedValue: 0x00000000u, netcoreExpectedValue: 0x00000000u, () => (uint)SingleToInt32Bits(YamlSerializer.Deserialize<float>("0")));
            AssertFloatingPointBehavior(netfxExpectedValue: 0x00000000u, netcoreExpectedValue: 0x80000000u, () => (uint)SingleToInt32Bits(YamlSerializer.Deserialize<float>("-0")));
            AssertFloatingPointBehavior(netfxExpectedValue: 0x00000000u, netcoreExpectedValue: 0x80000000u, () => (uint)SingleToInt32Bits(YamlSerializer.Deserialize<float>("-0.0")));

            AssertFloatingPointBehavior(netfxExpectedValue: 0x0000000000000000ul, netcoreExpectedValue: 0x0000000000000000ul, () => (ulong)BitConverter.DoubleToInt64Bits(YamlSerializer.Deserialize<double>("0")));
            AssertFloatingPointBehavior(netfxExpectedValue: 0x0000000000000000ul, netcoreExpectedValue: 0x8000000000000000ul, () => (ulong)BitConverter.DoubleToInt64Bits(YamlSerializer.Deserialize<double>("-0")));
            AssertFloatingPointBehavior(netfxExpectedValue: 0x0000000000000000ul, netcoreExpectedValue: 0x8000000000000000ul, () => (ulong)BitConverter.DoubleToInt64Bits(YamlSerializer.Deserialize<double>("-0.0")));

            // Verify Round-tripping.
            Assert.Equal(float.MaxValue, YamlSerializer.Deserialize<float>(float.MaxValue.ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture)));
            Assert.Equal(float.MaxValue, YamlSerializer.Deserialize<float?>(float.MaxValue.ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture)));

            Assert.Equal(double.MaxValue, YamlSerializer.Deserialize<double>(double.MaxValue.ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture)));
            Assert.Equal(double.MaxValue, YamlSerializer.Deserialize<double?>(double.MaxValue.ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture)));
        }

        [Theory]
        [InlineData("1.2")]
        [InlineData("\\u0031\\u002e\\u0032", "1.2")]
        [InlineData("1.2.3")]
        [InlineData("\\u0031\\u002e\\u0032\\u002e\\u0033", "1.2.3")]
        [InlineData("1.2.3.4")]
        [InlineData("\\u0031\\u002e\\u0032\\u002e\\u0033\\u002e\\u0034", "1.2.3.4")]
        [InlineData("2147483647.2147483647")]
        [InlineData("\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037",
            "2147483647.2147483647")]
        [InlineData("2147483647.2147483647.2147483647")]
        [InlineData("\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037",
            "2147483647.2147483647.2147483647")]
        [InlineData("2147483647.2147483647.2147483647.2147483647")]
        [InlineData("\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037\\u002e\\u0032\\u0031\\u0034\\u0037\\u0034\\u0038\\u0033\\u0036\\u0034\\u0037",
            "2147483647.2147483647.2147483647.2147483647")]
        [InlineData("1.+1", "1.1")] // Plus in components should work as before
        [InlineData("1 .1", "1.1")] // Whitespace before dot should work as before
        [InlineData("1. 1", "1.1")] // Whitespace after dot should work as before
        [InlineData("1 . +1", "1.1")] // Combined whitespace and plus should work as before
        [InlineData("+1.1", "1.1")] // Leading plus should work
        public static void Version_Read_Success(string yaml, string? actual = null)
        {
            actual ??= yaml;
            Version value = YamlSerializer.Deserialize<Version>($"\"{yaml}\"");

            Assert.Equal(Version.Parse(actual), value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("     ")]
        [InlineData(" ")]
        [InlineData("   1.2.3.4")] // Leading whitespace should be rejected
        [InlineData("1.2.3.4    ")] // Trailing whitespace should be rejected
        [InlineData("  1.2.3.4  ")] // Leading and trailing whitespace should be rejected
        [InlineData("2147483648.2147483648.2147483648.2147483648")] //int.MaxValue + 1
        [InlineData("2147483647.2147483647.2147483647.21474836477")] // Slightly bigger in size than max length of Version
        [InlineData("-2147483648.-2147483648")]
        [InlineData("-2147483648.-2147483648.-2147483648")]
        [InlineData("-2147483648.-2147483648.-2147483648.-2147483648")]
        [InlineData("1.-1")]
        [InlineData("1")]
        [InlineData("{}", false)]
        [InlineData("[]", false)]
        [InlineData("true", false)]
        public static void Version_Read_Failure(string yaml, bool addQuotes = true)
        {
            if (addQuotes)
                yaml = $"\"{yaml}\"";

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Version>(yaml));
        }

        [Fact]
        public static void ReadPrimitiveUri()
        {
            Uri uri = YamlSerializer.Deserialize<Uri>("""
                "https://domain/path"
                """);
            Assert.Equal(@"https://domain/path", uri.ToString());
            Assert.Equal("https://domain/path", uri.OriginalString);

            uri = YamlSerializer.Deserialize<Uri>("""
                "https:\/\/domain\/path"
                """);
            Assert.Equal(@"https://domain/path", uri.ToString());
            Assert.Equal("https://domain/path", uri.OriginalString);

            uri = YamlSerializer.Deserialize<Uri>("""
                "https:\u002f\u002fdomain\u002fpath"
                """);
            Assert.Equal(@"https://domain/path", uri.ToString());
            Assert.Equal("https://domain/path", uri.OriginalString);

            uri = YamlSerializer.Deserialize<Uri>("""
                "~/path"
                """);
            Assert.Equal("~/path", uri.ToString());
            Assert.Equal("~/path", uri.OriginalString);
        }

        private static int SingleToInt32Bits(float value)
        {
#if NET
            return BitConverter.SingleToInt32Bits(value);
#else
            return Unsafe.As<float, int>(ref value);
#endif
        }

        private static void AssertFloatingPointBehavior<T>(T netcoreExpectedValue, Func<T> testCode)
        {
            if (PlatformDetection.IsNetFramework)
            {
                Assert.Throws<YamlException>(() => testCode());
            }
            else
            {
                Assert.Equal(netcoreExpectedValue, testCode());
            }
        }

        private static void AssertFloatingPointBehavior<T>(T netfxExpectedValue, T netcoreExpectedValue, Func<T> testCode)
        {
            if (PlatformDetection.IsNetFramework)
            {
                Assert.Equal(netfxExpectedValue, testCode());
            }
            else
            {
                Assert.Equal(netcoreExpectedValue, testCode());
            }
        }

        private const long ArrayPoolMaxSizeBeforeUsingNormalAlloc = 1024 * 1024;
        private const int MaxExpansionFactorWhileTranscoding = 3;
        private const long Threshold = ArrayPoolMaxSizeBeforeUsingNormalAlloc / MaxExpansionFactorWhileTranscoding;

        [Theory]
        [InlineData(Threshold - 3)]
        [InlineData(Threshold - 2)]
        [InlineData(Threshold - 1)]
        [InlineData(Threshold)]
        [InlineData(Threshold + 1)]
        [InlineData(Threshold + 2)]
        [InlineData(Threshold + 3)]
        public static void LongInputString(int length)
        {
            // Verify boundary conditions in Deserialize() that inspect the size to determine allocation strategy.
            DeserializeLongYamlString(length);
        }

        private const int MaxInt = int.MaxValue / MaxExpansionFactorWhileTranscoding;
        private const int MaximumPossibleStringLength = int.MaxValue / 2 - 32;

        // NOTE: VeryLongInputString test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [InlineData(MaxInt)]
        [InlineData(MaximumPossibleStringLength)]
        [OuterLoop]
        public static void VeryLongInputString(int length)
        {
            try
            {
                // Verify that deserializer does not do any multiplication or addition on the string length
                DeserializeLongYamlString(length);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        private static void DeserializeLongYamlString(int stringLength)
        {
            string yaml;
            char fillChar = 'x';

#if NET
            yaml = string.Create(stringLength, fillChar, (chars, fillChar) =>
            {
                chars.Fill(fillChar);
                chars[0] = '"';
                chars[chars.Length - 1] = '"';
            });
#else
            string repeated = new string(fillChar, stringLength - 2);
            yaml = $"\"{repeated}\"";
#endif
            Assert.Equal(stringLength, yaml.Length);

            string str = YamlSerializer.Deserialize<string>(yaml);
            Assert.True(yaml.AsSpan(1, yaml.Length - 2).SequenceEqual(str.AsSpan()));
        }

        [Theory]
        [InlineData("1:2")]
        [InlineData("01:2")]
        [InlineData("1:02")]
        [InlineData("01:23:1")]
        [InlineData("1.1:1:1.0")]
        [InlineData("1:00:00")]
        [InlineData("1")]
        [InlineData("10")]
        [InlineData("00:01")]
        [InlineData("0:00:02")]
        [InlineData("0:00:00.0000001")]
        [InlineData("0:00:00.0000010")]
        [InlineData("0:00:00.0000100")]
        [InlineData("0:00:00.0001000")]
        [InlineData("0:00:00.0010000")]
        [InlineData("0:00:00.0100000")]
        [InlineData("0:00:00.1000000")]
        [InlineData("23:59:59")]
        [InlineData("\\u002D23:59:59", "-23:59:59")]
        [InlineData("\\u0032\\u0033\\u003A\\u0035\\u0039\\u003A\\u0035\\u0039", "23:59:59")]
        [InlineData("23:59:59.9", "23:59:59.9000000")]
        [InlineData("23:59:59.9999999")]
        [InlineData("9999999.23:59:59.9999999")]
        [InlineData("-9999999.23:59:59.9999999")]
        [InlineData("10675199.02:48:05.4775807")] // TimeSpan.MaxValue
        [InlineData("-10675199.02:48:05.4775808")] // TimeSpan.MinValue
        public static void TimeSpan_Read_Success(string yaml, string? actual = null)
        {
            TimeSpan value = YamlSerializer.Deserialize<TimeSpan>($"\"{yaml}\"");

            Assert.Equal(TimeSpan.Parse(actual ?? yaml), value);
            Assert.Equal(value, JsonConvert.DeserializeObject<TimeSpan>($"\"{yaml}\""));
        }

        [Fact]
        public static void TimeSpan_Read_Nullable_Tests()
        {
            TimeSpan? value = YamlSerializer.Deserialize<TimeSpan?>("null");
            Assert.False(value.HasValue);

            value = YamlSerializer.Deserialize<TimeSpan?>("\"23:59:59\"");
            Assert.True(value.HasValue);
            Assert.Equal(TimeSpan.Parse("23:59:59"), value);
            Assert.Equal(value, JsonConvert.DeserializeObject<TimeSpan>("\"23:59:59\""));

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TimeSpan>("null"));
        }

        [Fact]
        public static void TimeSpan_Read_KnownDifferences()
        {
            string value = "24:00:00";

            // 24:00:00 should be invalid because hours can only be up to 23.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TimeSpan>($"\"{value}\""));

            TimeSpan expectedValue = TimeSpan.Parse("24.00:00:00");

            // TimeSpan.Parse has a quirk where it treats 24:00:00 as 24.00:00:00.
            Assert.Equal(expectedValue, TimeSpan.Parse(value));

            // Newtonsoft uses TimeSpan.Parse so it is subject to the quirk.
            Assert.Equal(expectedValue, JsonConvert.DeserializeObject<TimeSpan>($"\"{value}\""));
        }

#if NET
        [Theory]
        [InlineData("1970-01-01")]
        [InlineData("2002-02-13")]
        [InlineData("2022-05-10")]
        [InlineData("\\u0032\\u0030\\u0032\\u0032\\u002D\\u0030\\u0035\\u002D\\u0031\\u0030", "2022-05-10")]
        [InlineData("0001-01-01")] // DateOnly.MinValue
        [InlineData("9999-12-31")] // DateOnly.MaxValue
        public static void DateOnly_Read_Success(string yaml, string? actual = null)
        {
            DateOnly value = YamlSerializer.Deserialize<DateOnly>($"\"{yaml}\"");
            Assert.Equal(DateOnly.Parse(actual ?? yaml), value);
        }

        [Theory]
        [InlineData("1970-01-01")]
        [InlineData("2002-02-13")]
        [InlineData("2022-05-10")]
        [InlineData("\\u0032\\u0030\\u0032\\u0032\\u002D\\u0030\\u0035\\u002D\\u0031\\u0030", "2022-05-10")]
        [InlineData("0001-01-01")] // DateOnly.MinValue
        [InlineData("9999-12-31")] // DateOnly.MaxValue
        public static void DateOnly_ReadDictionaryKey_Success(string yaml, string? actual = null)
        {
            Dictionary<DateOnly, int> expectedDict = new() { [DateOnly.Parse(actual ?? yaml)] = 0 };
            Dictionary<DateOnly, int> actualDict = YamlSerializer.Deserialize<Dictionary<DateOnly, int>>($@"{{""{yaml}"":0}}");
            Assert.Equal(expectedDict, actualDict);
        }

        [Fact]
        public static void DateOnly_Read_Nullable_Tests()
        {
            DateOnly? value = YamlSerializer.Deserialize<DateOnly?>("null");
            Assert.False(value.HasValue);

            value = YamlSerializer.Deserialize<DateOnly?>("\"2022-05-10\"");
            Assert.True(value.HasValue);
            Assert.Equal(DateOnly.Parse("2022-05-10"), value);
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DateOnly>("null"));
        }

        [Theory]
        [InlineData("05/10/2022")] // 'd' Format
        [InlineData("Tue, 10 May 2022")] // 'r' Format
        [InlineData("\t2022-05-10")] // Otherwise valid but has invalid json character
        [InlineData("\\t2022-05-10")] // Otherwise valid but has leading whitespace
        [InlineData("2022-05-10   ")] // Otherwise valid but has trailing whitespace
        // Fail on arbitrary ISO dates
        [InlineData("2022-05-10T20:53:01")]
        [InlineData("2022-05-10T20:53:01.3552286")]
        [InlineData("2022-05-10T20:53:01.3552286+01:00")]
        [InlineData("2022-05-10T20:53Z")]
        [InlineData("\\u0030\\u0035\\u002F\\u0031\\u0030\\u002F\\u0032\\u0030\\u0032\\u0032")]
        [InlineData("00:00:01")]
        [InlineData("23:59:59")]
        [InlineData("23:59:59.00000009")]
        [InlineData("1.00:00:00")]
        [InlineData("1:2:00:00")]
        [InlineData("+00:00:00")]
        [InlineData("1$")]
        [InlineData("-2020-05-10")]
        [InlineData("0000-12-31")] // DateOnly.MinValue - 1
        [InlineData("10000-01-01")] // DateOnly.MaxValue + 1
        [InlineData("1234", false)]
        [InlineData("{}", false)]
        [InlineData("[]", false)]
        [InlineData("true", false)]
        [InlineData("null", false)]
        [InlineData("05-1\\u0000", true)] // String length 10 before unescaping, less than 10 after escaping
        public static void DateOnly_Read_Failure(string yaml, bool addQuotes = true)
        {
            if (addQuotes)
                yaml = $"\"{yaml}\"";

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DateOnly>(yaml));
        }

        [Theory]
        [InlineData("1:2", "01:02")]
        [InlineData("01:2", "01:02")]
        [InlineData("01:23:1", "01:23:01")]
        [InlineData("1:00:00")] // 'g' Format
        [InlineData("00:00")]
        [InlineData("23:59")]
        [InlineData("23:59:59")]
        [InlineData("23:59:59.9", "23:59:59.9000000")]
        [InlineData("02:48:05.4775807")]
        [InlineData("02:48:05.4775808")]
        [InlineData("\\u0032\\u0033\\u003A\\u0035\\u0039\\u003A\\u0035\\u0039", "23:59:59")]
        [InlineData("00:00:00.0000000", "00:00:00")] // TimeOnly.MinValue
        [InlineData("23:59:59.9999999")] // TimeOnly.MaxValue
        public static void TimeOnly_Read_Success(string yaml, string? actual = null)
        {
            TimeOnly value = YamlSerializer.Deserialize<TimeOnly>($"\"{yaml}\"");
            Assert.Equal(TimeOnly.Parse(actual ?? yaml), value);
        }

        [Fact]
        public static void TimeOnly_Read_Nullable_Tests()
        {
            TimeOnly? value = YamlSerializer.Deserialize<TimeOnly?>("null");
            Assert.False(value.HasValue);

            value = YamlSerializer.Deserialize<TimeOnly?>("\"23:59:59\"");
            Assert.True(value.HasValue);
            Assert.Equal(TimeOnly.Parse("23:59:59"), value);
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TimeOnly>("null"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("01")]
        [InlineData("01:")]
        [InlineData("\t23:59:59")] // Otherwise valid but has invalid json character
        [InlineData("\\t23:59:59")] // Otherwise valid but has leading whitespace
        [InlineData("23:59:59   ")] // Otherwise valid but has trailing whitespace
        [InlineData("\\u0032\\u0034\\u003A\\u0030\\u0030\\u003A\\u0030\\u0030")]
        [InlineData("00:60:00")]
        [InlineData("00:00:60")]
        [InlineData("-00:00:00")]
        [InlineData("00:00:00.00000009")]
        [InlineData("900000000.00:00:00")]
        [InlineData("1.00:00:00")]
        [InlineData("0.00:00:00")]
        [InlineData("1:2:00:00")] // 'g' Format
        [InlineData("+00:00:00")]
        [InlineData("2021-06-18")]
        [InlineData("1$")]
        [InlineData("-00:00:00.0000001")] // TimeOnly.MinValue - 1
        [InlineData("24:00:00.0000000")] // TimeOnly.MaxValue + 1
        [InlineData("10675199.02:48:05.4775807")] // TimeSpan.MaxValue
        [InlineData("-10675199.02:48:05.4775808")] // TimeSpan.MinValue
        [InlineData("1234", false)]
        [InlineData("{}", false)]
        [InlineData("[]", false)]
        [InlineData("true", false)]
        [InlineData("null", false)]
        public static void TimeOnly_Read_Failure(string yaml, bool addQuotes = true)
        {
            if (addQuotes)
                yaml = $"\"{yaml}\"";

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<TimeOnly>(yaml));
        }
#endif
    }
}
