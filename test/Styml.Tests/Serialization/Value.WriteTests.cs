// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.DotNet.XUnitExtensions;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ValueTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringWithRelaxedEscaper()
        {
            string inputString = ">><++>>>\">>\\>>&>>>\u6f22\u5B57>>>"; // Non-ASCII text should remain unescaped. \u6f22 = \u6C49, \u5B57 = \u5B57

            string actual = YamlSerializer.Serialize(inputString, new YamlSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            string expected = "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"";
            Assert.Equal(JsonConvert.SerializeObject(inputString), actual);
            Assert.Equal(expected, actual);
            Assert.NotEqual(expected, YamlSerializer.Serialize(inputString));
        }

        // NOTE: WriteExtremelyLargeStrings test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        [OuterLoop]
        [InlineData(120_000_000)]
        public static void WriteExtremelyLargeStrings(int strLength)
        {
            const char InputCharacter = '\u007F';
            const string EscapedCharacter = "\\u007F";

            try
            {
                string value = new string(InputCharacter, strLength);
                string yaml = YamlSerializer.Serialize(value, YamlSerializerOptions.Default);

                int expectedYamlLength = 2 + (strLength * EscapedCharacter.Length);
                int middleSegmentStart = 1 + ((strLength / 2) * EscapedCharacter.Length);
                int lastSegmentStart = 1 + ((strLength - 1) * EscapedCharacter.Length);

                Assert.Equal(expectedYamlLength, yaml.Length);
                Assert.Equal('"', yaml[0]);
                Assert.Equal(EscapedCharacter, yaml.AsSpan(1, EscapedCharacter.Length).ToString());
                Assert.Equal(EscapedCharacter, yaml.AsSpan(middleSegmentStart, EscapedCharacter.Length).ToString());
                Assert.Equal(EscapedCharacter, yaml.AsSpan(lastSegmentStart, EscapedCharacter.Length).ToString());
                Assert.Equal('"', yaml[^1]);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException($"Insufficient memory to run {nameof(WriteExtremelyLargeStrings)} with length {strLength}.");
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritePrimitives()
        {
            {
                string yaml = YamlSerializer.Serialize(1);
                Assert.Equal("1", yaml);
            }

            {
                int? value = 1;
                string yaml = YamlSerializer.Serialize(value);
                Assert.Equal("1", yaml);
            }

            {
                int? value = null;
                string yaml = YamlSerializer.Serialize(value);
                Assert.Equal("null", yaml);
            }

            {
                string yaml = YamlSerializer.Serialize((string)null);
                Assert.Equal("null", yaml);
            }

            {
                Span<byte> yaml = YamlSerializer.SerializeToUtf8Bytes(1);
                Assert.Equal("1"u8.ToArray(), yaml.ToArray());
            }

            {
                string yaml = YamlSerializer.Serialize(long.MaxValue);
                Assert.Equal(long.MaxValue.ToString(), yaml);
            }

            {
                Span<byte> yaml = YamlSerializer.SerializeToUtf8Bytes(long.MaxValue);
                Assert.Equal(Encoding.UTF8.GetBytes(long.MaxValue.ToString()), yaml.ToArray());
            }

            {
                string yaml = YamlSerializer.Serialize("Hello");
                Assert.Equal("""
                    "Hello"
                    """, yaml);
            }

            {
                Span<byte> yaml = YamlSerializer.SerializeToUtf8Bytes("Hello");
                Assert.Equal(Encoding.UTF8.GetBytes("""
                    "Hello"
                    """), yaml.ToArray());
            }

            {
                Uri uri = new Uri("https://domain/path");
                Assert.Equal("""
                    "https://domain/path"
                    """, YamlSerializer.Serialize(uri));
            }

            {
                Uri.TryCreate("~/path", UriKind.RelativeOrAbsolute, out Uri uri);
                Assert.Equal("""
                    "~/path"
                    """, YamlSerializer.Serialize(uri));
            }

            // The next two scenarios validate that we're NOT using Uri.ToString() for serializing Uri. The serializer
            // will escape backslashes and ampersands, but otherwise should be the same as the output of Uri.OriginalString.

            {
                // ToString would collapse the relative segment
                Uri uri = new Uri("http://a/b/../c");
                Assert.Equal("""
                    "http://a/b/../c"
                    """, YamlSerializer.Serialize(uri));
            }

            {
                // "%20" gets turned into a space by Uri.ToString()
                // https://coding.abel.nu/2014/10/beware-of-uri-tostring/
                Uri uri = new Uri("http://localhost?p1=Value&p2=A%20B%26p3%3DFooled!");
                Assert.Equal("""
                    "http://localhost?p1=Value\u0026p2=A%20B%26p3%3DFooled!"
                    """, YamlSerializer.Serialize(uri));
            }

            {
                Version version = new Version(1, 2);
                Assert.Equal("""
                    "1.2"
                    """, YamlSerializer.Serialize(version));
            }

            {
                Version version = new Version(1, 2, 3);
                Assert.Equal("""
                    "1.2.3"
                    """, YamlSerializer.Serialize(version));
            }

            {
                Version version = new Version(1, 2, 3, 4);
                Assert.Equal("""
                    "1.2.3.4"
                    """, YamlSerializer.Serialize(version));
            }

            {
                Version version = new Version(int.MaxValue, int.MaxValue);
                Assert.Equal("""
                    "2147483647.2147483647"
                    """, YamlSerializer.Serialize(version));
            }

            {
                Version version = new Version(int.MaxValue, int.MaxValue, int.MaxValue);
                Assert.Equal("""
                    "2147483647.2147483647.2147483647"
                    """, YamlSerializer.Serialize(version));
            }

            {
                Version version = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
                Assert.Equal("""
                    "2147483647.2147483647.2147483647.2147483647"
                    """, YamlSerializer.Serialize(version));
            }
        }

        [Theory]
        [InlineData("1:59:59", "01:59:59")]
        [InlineData("23:59:59")]
        [InlineData("23:59:59.9", "23:59:59.9000000")]
        [InlineData("23:59:59.9999999")]
        [InlineData("1.23:59:59")]
        [InlineData("9999999.23:59:59.9999999")]
        [InlineData("-9999999.23:59:59.9999999")]
        [InlineData("10675199.02:48:05.4775807")] // TimeSpan.MaxValue
        [InlineData("-10675199.02:48:05.4775808")] // TimeSpan.MinValue
        [Trait("category", "jsonoutput")]
        public static void TimeSpan_Write_Success(string value, string? expectedValue = null)
        {
            TimeSpan ts = TimeSpan.Parse(value);
            string yaml = YamlSerializer.Serialize(ts);

            Assert.Equal($"\"{expectedValue ?? value}\"", yaml);
            Assert.Equal(yaml, JsonConvert.SerializeObject(ts));
        }

#if NET
        [Theory]
        [InlineData("1970-01-01")]
        [InlineData("2002-02-13")]
        [InlineData("2022-05-10")]
        [InlineData("0001-01-01")] // DateOnly.MinValue
        [InlineData("9999-12-31")] // DateOnly.MaxValue
        [Trait("category", "jsonoutput")]
        public static void DateOnly_Write_Success(string value)
        {
            DateOnly ts = DateOnly.Parse(value);
            string yaml = YamlSerializer.Serialize(ts);
            Assert.Equal($"\"{value}\"", yaml);
        }

        [Theory]
        [InlineData("1970-01-01")]
        [InlineData("2002-02-13")]
        [InlineData("2022-05-10")]
        [InlineData("0001-01-01")] // DateOnly.MinValue
        [InlineData("9999-12-31")] // DateOnly.MaxValue
        [Trait("category", "jsonoutput")]
        public static void DateOnly_WriteDictionaryKey_Success(string value)
        {
            var dict = new Dictionary<DateOnly, int> { [DateOnly.Parse(value)] = 0 };
            string yaml = YamlSerializer.Serialize(dict);
            Assert.Equal($@"{{""{value}"":0}}", yaml);
        }

        [Theory]
        [InlineData("1:59:59", "01:59:59")]
        [InlineData("23:59:59")]
        [InlineData("23:59:59.9", "23:59:59.9000000")]
        [InlineData("00:00:00")] // TimeOnly.MinValue
        [InlineData("23:59:59.9999999")] // TimeOnly.MaxValue
        [Trait("category", "jsonoutput")]
        public static void TimeOnly_Write_Success(string value, string? expectedValue = null)
        {
            TimeOnly ts = TimeOnly.Parse(value);
            string yaml = YamlSerializer.Serialize(ts);
            Assert.Equal($"\"{expectedValue ?? value}\"", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Int128_AsDictionaryKey_SerializesCorrectly()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/116855
            var dict = new Dictionary<Int128, string>
            {
                [0] = "Zero",
                [1] = "One",
                [-1] = "MinusOne",
                [Int128.MaxValue] = "Max",
                [Int128.MinValue] = "Min"
            };

            string yaml = YamlSerializer.Serialize(dict);
            Assert.Equal("""{"0":"Zero","1":"One","-1":"MinusOne","170141183460469231731687303715884105727":"Max","-170141183460469231731687303715884105728":"Min"}""", yaml);
            
            var deserialized = YamlSerializer.Deserialize<Dictionary<Int128, string>>(yaml);
            Assert.Equal(dict, deserialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void UInt128_AsDictionaryKey_SerializesCorrectly()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/116855
            var dict = new Dictionary<UInt128, string>
            {
                [0] = "Zero", 
                [1] = "One",
                [UInt128.MaxValue] = "Max"
            };

            string yaml = YamlSerializer.Serialize(dict);
            Assert.Equal("""{"0":"Zero","1":"One","340282366920938463463374607431768211455":"Max"}""", yaml);
            
            var deserialized = YamlSerializer.Deserialize<Dictionary<UInt128, string>>(yaml);
            Assert.Equal(dict, deserialized);
        }
#endif
    }
}
