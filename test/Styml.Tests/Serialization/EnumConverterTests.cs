// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.RemoteExecutor;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class EnumConverterTests
    {
        private static readonly YamlSerializerOptions s_optionsWithStringEnumConverter = new() { Converters = { new YamlStringEnumConverter() } };
        private static readonly YamlSerializerOptions s_optionsWithStringAndNoIntegerEnumConverter = new() { Converters = { new YamlStringEnumConverter(allowIntegerValues: false) } };

        [Theory]
        [InlineData(typeof(YamlStringEnumConverter), typeof(DayOfWeek))]
        [InlineData(typeof(YamlStringEnumConverter), typeof(MyCustomEnum))]
        [InlineData(typeof(YamlStringEnumConverter<DayOfWeek>), typeof(DayOfWeek))]
        [InlineData(typeof(YamlStringEnumConverter<MyCustomEnum>), typeof(MyCustomEnum))]
        public static void YamlStringEnumConverter_SupportedType_WorksAsExpected(Type converterType, Type supportedType)
        {
            var options = new YamlSerializerOptions();
            var factory = (YamlConverterFactory)Activator.CreateInstance(converterType);

            Assert.True(factory.CanConvert(supportedType));

            YamlConverter converter = factory.CreateConverter(supportedType, options);
            Assert.Equal(supportedType, converter.Type);
        }

        [Theory]
        [InlineData(typeof(YamlStringEnumConverter), typeof(int))]
        [InlineData(typeof(YamlStringEnumConverter), typeof(string))]
        [InlineData(typeof(YamlStringEnumConverter), typeof(YamlStringEnumConverter))]
        [InlineData(typeof(YamlStringEnumConverter<DayOfWeek>), typeof(int))]
        [InlineData(typeof(YamlStringEnumConverter<DayOfWeek>), typeof(string))]
        [InlineData(typeof(YamlStringEnumConverter<DayOfWeek>), typeof(YamlStringEnumConverter<MyCustomEnum>))]
        [InlineData(typeof(YamlStringEnumConverter<DayOfWeek>), typeof(MyCustomEnum))]
        [InlineData(typeof(YamlStringEnumConverter<MyCustomEnum>), typeof(DayOfWeek))]
        public static void YamlStringEnumConverter_InvalidType_ThrowsArgumentOutOfRangeException(Type converterType, Type unsupportedType)
        {
            var options = new YamlSerializerOptions();
            var factory = (YamlConverterFactory)Activator.CreateInstance(converterType);

            Assert.False(factory.CanConvert(unsupportedType));
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateConverter(unsupportedType, options));
            Assert.Contains(unsupportedType.FullName, ex.Message);
        }

        [Theory]
        [InlineData(typeof(YamlNumberEnumConverter<DayOfWeek>), typeof(DayOfWeek))]
        [InlineData(typeof(YamlNumberEnumConverter<MyCustomEnum>), typeof(MyCustomEnum))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(YamlNumberEnumConverter<>))]
        public static void YamlNumberEnumConverter_SupportedType_WorksAsExpected(Type converterType, Type supportedType)
        {
            var options = new YamlSerializerOptions();
            var factory = (YamlConverterFactory)Activator.CreateInstance(converterType);

            Assert.True(factory.CanConvert(supportedType));

            YamlConverter converter = factory.CreateConverter(supportedType, options);
            Assert.Equal(supportedType, converter.Type);
        }

        [Theory]
        [InlineData(typeof(YamlNumberEnumConverter<DayOfWeek>), typeof(int))]
        [InlineData(typeof(YamlNumberEnumConverter<DayOfWeek>), typeof(string))]
        [InlineData(typeof(YamlNumberEnumConverter<DayOfWeek>), typeof(YamlStringEnumConverter<MyCustomEnum>))]
        [InlineData(typeof(YamlNumberEnumConverter<DayOfWeek>), typeof(MyCustomEnum))]
        [InlineData(typeof(YamlNumberEnumConverter<MyCustomEnum>), typeof(DayOfWeek))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(YamlNumberEnumConverter<>))]
        public static void YamlNumberEnumConverter_InvalidType_ThrowsArgumentOutOfRangeException(Type converterType, Type unsupportedType)
        {
            var options = new YamlSerializerOptions();
            var factory = (YamlConverterFactory)Activator.CreateInstance(converterType);

            Assert.False(factory.CanConvert(unsupportedType));
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateConverter(unsupportedType, options));
            Assert.Contains(unsupportedType.FullName, ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void ConvertDayOfWeek(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<DayOfWeek>(useGenericVariant);

            WhenClass when = YamlSerializer.Deserialize<WhenClass>("""{"Day":"Monday"}""", options);
            Assert.Equal(DayOfWeek.Monday, when.Day);
            DayOfWeek day = YamlSerializer.Deserialize<DayOfWeek>("""
                "Tuesday"
                """, options);
            Assert.Equal(DayOfWeek.Tuesday, day);

            // We are case insensitive on read
            day = YamlSerializer.Deserialize<DayOfWeek>("""
                "wednesday"
                """, options);
            Assert.Equal(DayOfWeek.Wednesday, day);

            // Numbers work by default
            day = YamlSerializer.Deserialize<DayOfWeek>(@"4", options);
            Assert.Equal(DayOfWeek.Thursday, day);

            string yaml = YamlSerializer.Serialize(DayOfWeek.Friday, options);
            Assert.Equal("""
                "Friday"
                """, yaml);

            // Try a unique naming policy
            options = CreateStringEnumOptionsForType<DayOfWeek>(useGenericVariant, new ToLowerNamingPolicy());

            yaml = YamlSerializer.Serialize(DayOfWeek.Friday, options);
            Assert.Equal("""
                "friday"
                """, yaml);

            // Undefined values should come out as a number (not a string)
            yaml = YamlSerializer.Serialize((DayOfWeek)(-1), options);
            Assert.Equal(@"-1", yaml);

            // Not permitting integers should throw
            options = CreateStringEnumOptionsForType<DayOfWeek>(useGenericVariant, allowIntegerValues: false);
            Assert.Throws<YamlException>(() => YamlSerializer.Serialize((DayOfWeek)(-1), options));

            // Quoted numbers should throw
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("1", options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("-1", options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                "1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                "+1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                "-1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                " 1 "
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                " +1 "
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                " -1 "
                """, options));

            day = YamlSerializer.Deserialize<DayOfWeek>("""
                "Monday"
                """, options);
            Assert.Equal(DayOfWeek.Monday, day);

            // Numbers-formatted json string should first consider naming policy
            options = CreateStringEnumOptionsForType<DayOfWeek>(useGenericVariant, new ToEnumNumberNamingPolicy<DayOfWeek>(), false);
            day = YamlSerializer.Deserialize<DayOfWeek>("""
                "1"
                """, options);
            Assert.Equal(DayOfWeek.Monday, day);

            options = CreateStringEnumOptionsForType<DayOfWeek>(useGenericVariant, new ToLowerNamingPolicy(), false);
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DayOfWeek>("""
                "1"
                """, options));
        }

        public class ToLowerNamingPolicy : YamlNamingPolicy
        {
            public override string ConvertName(string name) => name.ToLowerInvariant();
        }

        public class WhenClass
        {
            public DayOfWeek Day { get; set; }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void ConvertFileAttributes(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<FileAttributes>(useGenericVariant);

            FileState state = YamlSerializer.Deserialize<FileState>("""{"Attributes":"ReadOnly"}""", options);
            Assert.Equal(FileAttributes.ReadOnly, state.Attributes);
            state = YamlSerializer.Deserialize<FileState>("""{"Attributes":"Directory, ReparsePoint"}""", options);
            Assert.Equal(FileAttributes.Directory | FileAttributes.ReparsePoint, state.Attributes);
            FileAttributes attributes = YamlSerializer.Deserialize<FileAttributes>("""
                "Normal"
                """, options);
            Assert.Equal(FileAttributes.Normal, attributes);
            attributes = YamlSerializer.Deserialize<FileAttributes>("""
                "System, SparseFile"
                """, options);
            Assert.Equal(FileAttributes.System | FileAttributes.SparseFile, attributes);

            // We are case insensitive on read
            attributes = YamlSerializer.Deserialize<FileAttributes>("""
                "OFFLINE"
                """, options);
            Assert.Equal(FileAttributes.Offline, attributes);
            attributes = YamlSerializer.Deserialize<FileAttributes>("""
                "compressed, notcontentindexed"
                """, options);
            Assert.Equal(FileAttributes.Compressed | FileAttributes.NotContentIndexed, attributes);

            // Numbers are cool by default
            attributes = YamlSerializer.Deserialize<FileAttributes>(@"131072", options);
            Assert.Equal(FileAttributes.NoScrubData, attributes);
            attributes = YamlSerializer.Deserialize<FileAttributes>(@"3", options);
            Assert.Equal(FileAttributes.Hidden | FileAttributes.ReadOnly, attributes);

            string yaml = YamlSerializer.Serialize(FileAttributes.Hidden, options);
            Assert.Equal("""
                "Hidden"
                """, yaml);
            yaml = YamlSerializer.Serialize(FileAttributes.Temporary | FileAttributes.Offline, options);
            Assert.Equal("""
                "Temporary, Offline"
                """, yaml);

            // Try a unique casing
            options = CreateStringEnumOptionsForType<FileAttributes>(useGenericVariant, new ToLowerNamingPolicy());

            yaml = YamlSerializer.Serialize(FileAttributes.NoScrubData, options);
            Assert.Equal("""
                "noscrubdata"
                """, yaml);
            yaml = YamlSerializer.Serialize(FileAttributes.System | FileAttributes.Offline, options);
            Assert.Equal("""
                "system, offline"
                """, yaml);

            // Undefined values should come out as a number (not a string)
            yaml = YamlSerializer.Serialize((FileAttributes)(-1), options);
            Assert.Equal(@"-1", yaml);

            options = CreateStringEnumOptionsForType<FileAttributes>(useGenericVariant, allowIntegerValues: false);
            // Not permitting integers should throw
            Assert.Throws<YamlException>(() => YamlSerializer.Serialize((FileAttributes)(-1), options));

            // Numbers should throw
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("1", options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("-1", options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                "1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                "+1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                "-1"
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                " 1 "
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                " +1 "
                """, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<FileAttributes>("""
                " -1 "
                """, options));

            attributes = YamlSerializer.Deserialize<FileAttributes>("""
                "ReadOnly"
                """, options);
            Assert.Equal(FileAttributes.ReadOnly, attributes);

            // Flag values honor naming policy correctly
            options = CreateStringEnumOptionsForType<FileAttributes>(useGenericVariant, YamlNamingPolicy.SnakeCaseLower);

            yaml = YamlSerializer.Serialize(
                FileAttributes.Directory | FileAttributes.Compressed | FileAttributes.IntegrityStream,
                options);
            Assert.Equal("""
                "directory, compressed, integrity_stream"
                """, yaml);

            yaml = YamlSerializer.Serialize((FileAttributes)(-1), options);
            Assert.Equal(@"-1", yaml);

            yaml = YamlSerializer.Serialize(FileAttributes.Directory & FileAttributes.Compressed | FileAttributes.IntegrityStream, options);
            Assert.Equal("""
                "integrity_stream"
                """, yaml);
        }

        public class FileState
        {
            public FileAttributes Attributes { get; set; }
        }

        public class Week
        {
            [YamlConverter(typeof(YamlStringEnumConverter))]
            public DayOfWeek WorkStart { get; set; }
            public DayOfWeek WorkEnd { get; set; }
            [YamlConverter(typeof(LowerCaseEnumConverter))]
            public DayOfWeek WeekEnd { get; set; }

            [YamlConverter(typeof(YamlStringEnumConverter<DayOfWeek>))]
            public DayOfWeek WorkStart2 { get; set; }
            [YamlConverter(typeof(LowerCaseEnumConverter<DayOfWeek>))]
            public DayOfWeek WeekEnd2 { get; set; }
        }

        private class LowerCaseEnumConverter : YamlStringEnumConverter
        {
            public LowerCaseEnumConverter() : base(new ToLowerNamingPolicy())
            {
            }
        }

        private class LowerCaseEnumConverter<TEnum> : YamlStringEnumConverter<TEnum>
            where TEnum : struct, Enum
        {
            public LowerCaseEnumConverter() : base(new ToLowerNamingPolicy())
            {
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void ConvertEnumUsingAttributes()
        {
            Week week = new Week {
                WorkStart = DayOfWeek.Monday,
                WorkEnd = DayOfWeek.Friday,
                WeekEnd = DayOfWeek.Saturday,
                WorkStart2 = DayOfWeek.Tuesday,
                WeekEnd2 = DayOfWeek.Thursday,
            };

            string yaml = YamlSerializer.Serialize(week);
            Assert.Equal("""{"WorkStart":"Monday","WorkEnd":5,"WeekEnd":"saturday","WorkStart2":"Tuesday","WeekEnd2":"thursday"}""", yaml);

            week = YamlSerializer.Deserialize<Week>(yaml);
            Assert.Equal(DayOfWeek.Monday, week.WorkStart);
            Assert.Equal(DayOfWeek.Friday, week.WorkEnd);
            Assert.Equal(DayOfWeek.Saturday, week.WeekEnd);
            Assert.Equal(DayOfWeek.Tuesday, week.WorkStart2);
            Assert.Equal(DayOfWeek.Thursday, week.WeekEnd2);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void EnumConverterComposition()
        {
            YamlSerializerOptions options = new YamlSerializerOptions { Converters = { new NoFlagsStringEnumConverter() } };
            string yaml = YamlSerializer.Serialize(DayOfWeek.Monday, options);
            Assert.Equal("""
                "Monday"
                """, yaml);
            yaml = YamlSerializer.Serialize(FileAccess.Read);
            Assert.Equal(@"1", yaml);
        }

        public class NoFlagsStringEnumConverter : YamlConverterFactory
        {
            private static YamlStringEnumConverter s_stringEnumConverter = new YamlStringEnumConverter();

            public override bool CanConvert(Type typeToConvert)
                => typeToConvert.IsEnum && !typeToConvert.IsDefined(typeof(FlagsAttribute), inherit: false);

            public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
                => s_stringEnumConverter.CreateConverter(typeToConvert, options);
        }

        [YamlConverter(typeof(YamlStringEnumConverter))]
        private enum MyCustomEnum
        {
            First = 1,
            Second = 2
        }

        [YamlConverter(typeof(YamlStringEnumConverter<MyCustomEnum2>))]
        private enum MyCustomEnum2
        {
            First = 1,
            Second = 2
        }

        [Theory]
        [InlineData(typeof(MyCustomEnum), MyCustomEnum.Second, "\"Second\"", "2")]
        [InlineData(typeof(MyCustomEnum2), MyCustomEnum2.Second, "\"Second\"", "2")]
        [Trait("category", "jsonoutput")]
        public void EnumWithConverterAttribute(Type enumType, object value, string expectedYaml, string alternativeYaml)
        {
            string yaml = YamlSerializer.Serialize(value, enumType);
            Assert.Equal(expectedYaml, yaml);

            object? result = YamlSerializer.Deserialize(yaml, enumType);
            Assert.Equal(value, result);

            result = YamlSerializer.Deserialize(alternativeYaml, enumType);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void EnumWithNoValues(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<EmptyEnum>(useGenericVariant);

            Assert.Equal("-1", YamlSerializer.Serialize((EmptyEnum)(-1), options));
            Assert.Equal("1", YamlSerializer.Serialize((EmptyEnum)(1), options));
        }

        public enum EmptyEnum { };

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void MoreThan64EnumValuesToSerialize(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<MyEnum>(useGenericVariant);

            for (int i = 0; i < 128; i++)
            {
                MyEnum value = (MyEnum)i;
                string asStr = value.ToString();
                string expected = char.IsLetter(asStr[0]) ? $@"""{asStr}""" : asStr;
                Assert.Equal(expected, YamlSerializer.Serialize(value, options));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void MoreThan64EnumValuesToSerializeWithNamingPolicy(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<MyEnum>(useGenericVariant, new ToLowerNamingPolicy());

            for (int i = 0; i < 128; i++)
            {
                MyEnum value = (MyEnum)i;
                string asStr = value.ToString().ToLowerInvariant();
                string expected = char.IsLetter(asStr[0]) ? $@"""{asStr}""" : asStr;
                Assert.Equal(expected, YamlSerializer.Serialize(value, options));
            }
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsMultithreadingSupported))]
        [OuterLoop]
        public static void VeryLargeAmountOfEnumsToSerialize()
        {
            // Ensure we don't throw OutOfMemoryException.

            var options = new YamlSerializerOptions
            {
                Converters = { new YamlStringEnumConverter() }
            };

            const int MaxValue = 2097152; // value for MyEnum.V

            // Every value between 0 and MaxValue maps to a valid enum
            // identifier, and is a candidate to go into the name cache.

            // Write the first 45 values.
            for (int i = 1; i < 46; i++)
            {
                YamlSerializer.Serialize((MyEnum)i, options);
            }

            // At this point, there are 60 values in the name cache;
            // 22 cached at warm-up, the rest in the above loop.

            // Ensure the approximate size limit for the name cache (a concurrent dictionary) is honored.
            // Use multiple threads to perhaps go over the soft limit of 64, but not by more than a couple.
            Parallel.For(0, 8, i => YamlSerializer.Serialize((MyEnum)(46 + i), options));

            // Write the remaining enum values. The cache is capped to avoid
            // OutOfMemoryException due to having too many cached items.
            for (int i = 54; i <= MaxValue; i++)
            {
                YamlSerializer.Serialize((MyEnum)i, options);
            }
        }

        [Flags]
        public enum MyEnum
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
            H = 1 << 7,
            I = 1 << 8,
            J = 1 << 9,
            K = 1 << 10,
            L = 1 << 11,
            M = 1 << 12,
            N = 1 << 13,
            O = 1 << 14,
            P = 1 << 15,
            Q = 1 << 16,
            R = 1 << 17,
            S = 1 << 18,
            T = 1 << 19,
            U = 1 << 20,
            V = 1 << 21,
        }

        [Fact, OuterLoop]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/42677", platforms: TestPlatforms.Windows, runtimes: TestRuntimes.Mono)]
        public static void VeryLargeAmountOfEnumDictionaryKeysToSerialize()
        {
            // Ensure we don't throw OutOfMemoryException.

            const int MaxValue = (int)MyEnum.V;

            // Every value between 0 and MaxValue maps to a valid enum
            // identifier, and is a candidate to go into the name cache.

            // Write the first 45 values.
            Dictionary<MyEnum, int> dictionary;
            for (int i = 1; i < 46; i++)
            {
                dictionary = new Dictionary<MyEnum, int> { { (MyEnum)i, i } };
                YamlSerializer.Serialize(dictionary);
            }

            // At this point, there are 60 values in the name cache;
            // 22 cached at warm-up, the rest in the above loop.

            // Ensure the approximate size limit for the name cache (a concurrent dictionary) is honored.
            // Use multiple threads to perhaps go over the soft limit of 64, but not by more than a couple.
            Parallel.For(
                0,
                8,
                i =>
                {
                    dictionary = new Dictionary<MyEnum, int> { { (MyEnum)(46 + i), i } };
                    YamlSerializer.Serialize(dictionary);
                }
            );

            // Write the remaining enum values. The cache is capped to avoid
            // OutOfMemoryException due to having too many cached items.
            for (int i = 54; i <= MaxValue; i++)
            {
                dictionary = new Dictionary<MyEnum, int> { { (MyEnum)i, i } };
                YamlSerializer.Serialize(dictionary);
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        [Trait("category", "jsonoutput")]
        public static void NegativeEnumValue_CultureInvariance()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/68600
            RemoteExecutor.Invoke(static () =>
            {
                SampleEnumInt32 value = (SampleEnumInt32)(-2);
                string expectedYaml = "-2";

                var options = new YamlSerializerOptions
                {
                    Converters = { new YamlStringEnumConverter(allowIntegerValues: true) },
                };

                // Sets the minus sign to -
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                string actualYaml = YamlSerializer.Serialize(value, options);
                Assert.Equal(expectedYaml, actualYaml);
                SampleEnumInt32 result = YamlSerializer.Deserialize<SampleEnumInt32>(actualYaml, options);
                Assert.Equal(value, result);

                // Sets the minus sign to U+2212
                CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

                actualYaml = YamlSerializer.Serialize(value, options);
                Assert.Equal(expectedYaml, actualYaml);
                result = YamlSerializer.Deserialize<SampleEnumInt32>(actualYaml, options);
                Assert.Equal(value, result);
            }).Dispose();
        }

        public abstract class NumericEnumKeyDictionaryBase<T>
        {
            public abstract Dictionary<T, int> BuildDictionary(int i);

            [Fact]
            [Trait("category", "jsonoutput")]
            public void SerilizeDictionaryWhenCacheIsFull()
            {
                Dictionary<T, int> dictionary;
                for (int i = 1; i <= 64; i++)
                {
                    dictionary = BuildDictionary(i);
                    YamlSerializer.Serialize(dictionary);
                }

                dictionary = BuildDictionary(0);
                string yaml = YamlSerializer.Serialize(dictionary);
                Assert.Equal($"{{\"0\":0}}", yaml);
            }
        }

        public class Int32EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumInt32>
        {
            public override Dictionary<SampleEnumInt32, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumInt32, int> { { (SampleEnumInt32)i, i } };
        }

        public class UInt32EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumUInt32>
        {
            public override Dictionary<SampleEnumUInt32, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumUInt32, int> { { (SampleEnumUInt32)i, i } };
        }

        public class UInt64EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumUInt64>
        {
            public override Dictionary<SampleEnumUInt64, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumUInt64, int> { { (SampleEnumUInt64)i, i } };
        }

        public class Int64EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumInt64>
        {
            public override Dictionary<SampleEnumInt64, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumInt64, int> { { (SampleEnumInt64)i, i } };
        }

        public class Int16EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumInt16>
        {
            public override Dictionary<SampleEnumInt16, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumInt16, int> { { (SampleEnumInt16)i, i } };
        }

        public class UInt16EnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumUInt16>
        {
            public override Dictionary<SampleEnumUInt16, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumUInt16, int> { { (SampleEnumUInt16)i, i } };
        }

        public class ByteEnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumByte>
        {
            public override Dictionary<SampleEnumByte, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumByte, int> { { (SampleEnumByte)i, i } };
        }

        public class SByteEnumDictionary : NumericEnumKeyDictionaryBase<SampleEnumSByte>
        {
            public override Dictionary<SampleEnumSByte, int> BuildDictionary(int i) =>
                new Dictionary<SampleEnumSByte, int> { { (SampleEnumSByte)i, i } };
        }


        [Flags]
        public enum SampleEnumInt32
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumUInt32 : uint
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumUInt64 : ulong
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumInt64 : long
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumInt16 : short
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumUInt16 : ushort
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumByte : byte
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Flags]
        public enum SampleEnumSByte : sbyte
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Honor_EnumNamingPolicy_On_Deserialization(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<BindingFlags>(useGenericVariant, YamlNamingPolicy.SnakeCaseLower);

            BindingFlags bindingFlags = YamlSerializer.Deserialize<BindingFlags>("""
                "non_public"
                """, options);
            Assert.Equal(BindingFlags.NonPublic, bindingFlags);

            // Flags supported without naming policy.
            bindingFlags = YamlSerializer.Deserialize<BindingFlags>("""
                "NonPublic, Public"
                """, options);
            Assert.Equal(BindingFlags.NonPublic | BindingFlags.Public, bindingFlags);

            // Flags supported with naming policy.
            bindingFlags = YamlSerializer.Deserialize<BindingFlags>("""
                "static, public"
                """, options);
            Assert.Equal(BindingFlags.Static | BindingFlags.Public, bindingFlags);

            // Null not supported.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BindingFlags>("null", options));

            // Null supported for nullable enum.
            Assert.Null(YamlSerializer.Deserialize<BindingFlags?>("null", options));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void EnumDictionaryKeyDeserialization(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<BindingFlags>(useGenericVariant);
            options.DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseLower;

            // Baseline.
            var dict = YamlSerializer.Deserialize<Dictionary<BindingFlags, int>>("""{"NonPublic, Public": 1}""", options);
            Assert.Equal(1, dict[BindingFlags.NonPublic | BindingFlags.Public]);

            // DictionaryKeyPolicy not honored for dict key deserialization.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<BindingFlags, int>>("""{"NonPublic0, Public0": 1}""", options));

            // EnumConverter naming policy not honored.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<BindingFlags, int>>("""{"non_public, static": 0, "NonPublic, Public": 1}""", options));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void StringEnumWithNamingPolicyKeyDeserialization(bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType<BindingFlags>(useGenericVariant, YamlNamingPolicy.SnakeCaseLower);
            options.DictionaryKeyPolicy = YamlNamingPolicy.KebabCaseUpper;

            // DictionaryKeyPolicy not honored for dict key deserialization.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<BindingFlags, int>>("""{"NON-PUBLIC, PUBLIC": 1}""", options));

            // EnumConverter naming policy honored.
            Dictionary<BindingFlags, int> result = YamlSerializer.Deserialize<Dictionary<BindingFlags, int>>("""{"non_public, static": 0, "NonPublic, Public": 1, "create_instance": 2 }""", options);
            Assert.Contains(BindingFlags.NonPublic | BindingFlags.Static, result);
            Assert.Contains(BindingFlags.NonPublic | BindingFlags.Public, result);
            Assert.Contains(BindingFlags.CreateInstance, result);
        }

        [Fact]
        public static void EnumDictionaryKeySerialization()
        {
            YamlSerializerOptions options = new()
            {
                DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseLower
            };

            Dictionary<BindingFlags, int> dict = new()
            {
                [BindingFlags.NonPublic | BindingFlags.Public] = 1,
                [BindingFlags.Static] = 2,
            };

            string expected = """
                {
                    "public, non_public": 1,
                    "static": 2
                }
                """;

            YamlTestHelper.AssertYamlEqual(expected, YamlSerializer.Serialize(dict, options));
        }

        [Theory]
        [InlineData(typeof(SampleEnumByte), true)]
        [InlineData(typeof(SampleEnumByte), false)]
        [InlineData(typeof(SampleEnumSByte), true)]
        [InlineData(typeof(SampleEnumSByte), false)]
        [InlineData(typeof(SampleEnumInt16), true)]
        [InlineData(typeof(SampleEnumInt16), false)]
        [InlineData(typeof(SampleEnumUInt16), true)]
        [InlineData(typeof(SampleEnumUInt16), false)]
        [InlineData(typeof(SampleEnumInt32), true)]
        [InlineData(typeof(SampleEnumInt32), false)]
        [InlineData(typeof(SampleEnumUInt32), true)]
        [InlineData(typeof(SampleEnumUInt32), false)]
        [InlineData(typeof(SampleEnumInt64), true)]
        [InlineData(typeof(SampleEnumInt64), false)]
        [InlineData(typeof(SampleEnumUInt64), true)]
        [InlineData(typeof(SampleEnumUInt64), false)]
        public static void DeserializeNumericStringWithAllowIntegerValuesAsFalse(Type enumType, bool useGenericVariant)
        {
            YamlSerializerOptions options = CreateStringEnumOptionsForType(enumType, useGenericVariant, allowIntegerValues: false);

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                "1"
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                "+1"
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                "-1"
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                " 1 "
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                " +1 "
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("""
                " -1 "
                """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$"""{ulong.MaxValue}""", enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$""" {ulong.MaxValue} """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$"""+{ulong.MaxValue}""", enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$""" +{ulong.MaxValue} """, enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$"""{long.MinValue}""", enumType, options));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(@$""" {long.MinValue} """, enumType, options));
        }

        private class ToEnumNumberNamingPolicy<T> : YamlNamingPolicy where T : struct, Enum
        {
            public override string ConvertName(string name) => Enum.TryParse(name, out T value) ? value.ToString("D") : name;
        }

        private class ZeroAppenderPolicy : YamlNamingPolicy
        {
            public override string ConvertName(string name) => name + "0";
        }

        private static YamlSerializerOptions CreateStringEnumOptionsForType(Type enumType, bool useGenericVariant, YamlNamingPolicy? namingPolicy = null, bool allowIntegerValues = true)
        {
            Debug.Assert(enumType.IsEnum);

            return new YamlSerializerOptions
            {
                Converters =
                {
                    useGenericVariant
                        ? (YamlConverter)Activator.CreateInstance(typeof(YamlStringEnumConverter<>).MakeGenericType(enumType), namingPolicy, allowIntegerValues)
                        : new YamlStringEnumConverter(namingPolicy, allowIntegerValues)
                }
            };
        }

        private static YamlSerializerOptions CreateStringEnumOptionsForType<TEnum>(bool useGenericVariant, YamlNamingPolicy? namingPolicy = null, bool allowIntegerValues = true) where TEnum : struct, Enum
        {
            return CreateStringEnumOptionsForType(typeof(TEnum), useGenericVariant, namingPolicy, allowIntegerValues);
        }

        [Theory]
        [InlineData(EnumWithMemberAttributes.Value1, "CustomValue1")]
        [InlineData(EnumWithMemberAttributes.Value2, "CustomValue2")]
        [InlineData(EnumWithMemberAttributes.Value3, "Value3")]
        [Trait("category", "jsonoutput")]
        public static void EnumWithMemberAttributes_StringEnumConverter_SerializesAsExpected(EnumWithMemberAttributes value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value, s_optionsWithStringEnumConverter);
            Assert.Equal($"\"{expectedYaml}\"", yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumWithMemberAttributes>(yaml, s_optionsWithStringEnumConverter));
        }

        [Theory]
        [InlineData(EnumWithMemberAttributes.Value1)]
        [InlineData(EnumWithMemberAttributes.Value2)]
        [InlineData(EnumWithMemberAttributes.Value3)]
        [Trait("category", "jsonoutput")]
        public static void EnumWithMemberAttributes_NoStringEnumConverter_SerializesAsNumber(EnumWithMemberAttributes value)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal($"{(int)value}", yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumWithMemberAttributes>(yaml));
        }

        [Theory]
        [InlineData(EnumWithMemberAttributes.Value1, "CustomValue1")]
        [InlineData(EnumWithMemberAttributes.Value2, "CustomValue2")]
        [InlineData(EnumWithMemberAttributes.Value3, "value3")]
        [Trait("category", "jsonoutput")]
        public static void EnumWithMemberAttributes_StringEnumConverterWithNamingPolicy_NotAppliedToCustomNames(EnumWithMemberAttributes value, string expectedYaml)
        {
            YamlSerializerOptions options = new() { Converters = { new YamlStringEnumConverter(YamlNamingPolicy.CamelCase) } };

            string yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal($"\"{expectedYaml}\"", yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumWithMemberAttributes>(yaml, options));
        }

        [Fact]
        public static void EnumWithMemberAttributes_NamingPolicyAndDictionaryKeyPolicy_NotAppliedToCustomNames()
        {
            YamlSerializerOptions options = new()
            {
                Converters = { new YamlStringEnumConverter(YamlNamingPolicy.CamelCase) },
                DictionaryKeyPolicy = YamlNamingPolicy.KebabCaseUpper,
            };

            Dictionary<EnumWithMemberAttributes, EnumWithMemberAttributes[]> value = new()
            {
                [EnumWithMemberAttributes.Value1] = [EnumWithMemberAttributes.Value1, EnumWithMemberAttributes.Value2, EnumWithMemberAttributes.Value3 ],
                [EnumWithMemberAttributes.Value2] = [EnumWithMemberAttributes.Value2 ],
                [EnumWithMemberAttributes.Value3] = [EnumWithMemberAttributes.Value3, EnumWithMemberAttributes.Value1 ],
            };

            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""
                {
                    "CustomValue1": ["CustomValue1", "CustomValue2", "value3"],
                    "CustomValue2": ["CustomValue2"],
                    "VALUE3": ["value3", "CustomValue1"]
                }
                """, yaml);
        }

        [Theory]
        [InlineData("\"customvalue1\"")]
        [InlineData("\"CUSTOMVALUE1\"")]
        [InlineData("\"cUSTOMvALUE1\"")]
        [InlineData("\"customvalue2\"")]
        [InlineData("\"CUSTOMVALUE2\"")]
        [InlineData("\"cUSTOMvALUE2\"")]
        public static void EnumWithMemberAttributes_CustomizedValuesAreCaseSensitive(string yaml)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<EnumWithMemberAttributes>(yaml, s_optionsWithStringEnumConverter));
        }

        [Theory]
        [InlineData("\"value3\"", EnumWithMemberAttributes.Value3)]
        [InlineData("\"VALUE3\"", EnumWithMemberAttributes.Value3)]
        [InlineData("\"vALUE3\"", EnumWithMemberAttributes.Value3)]
        public static void EnumWithMemberAttributes_DefaultValuesAreCaseInsensitive(string yaml, EnumWithMemberAttributes expectedValue)
        {
            EnumWithMemberAttributes value = YamlSerializer.Deserialize<EnumWithMemberAttributes>(yaml, s_optionsWithStringEnumConverter);
            Assert.Equal(expectedValue, value);
        }

        public enum EnumWithMemberAttributes
        {
            [YamlStringEnumMemberName("CustomValue1")]
            Value1 = 1,
            [YamlStringEnumMemberName("CustomValue2")]
            Value2 = 2,
            Value3 = 3,
        }

        [Theory]
        [InlineData(EnumFlagsWithMemberAttributes.Value1, "A")]
        [InlineData(EnumFlagsWithMemberAttributes.Value2, "B")]
        [InlineData(EnumFlagsWithMemberAttributes.Value3, "C")]
        [InlineData(EnumFlagsWithMemberAttributes.Value4, "Value4")]
        [InlineData(EnumFlagsWithMemberAttributes.Value1 | EnumFlagsWithMemberAttributes.Value2, "A, B")]
        [InlineData(EnumFlagsWithMemberAttributes.Value1 | EnumFlagsWithMemberAttributes.Value2 | EnumFlagsWithMemberAttributes.Value3 | EnumFlagsWithMemberAttributes.Value4, "A, B, C, Value4")]
        [Trait("category", "jsonoutput")]
        public static void EnumFlagsWithMemberAttributes_SerializesAsExpected(EnumFlagsWithMemberAttributes value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal($"\"{expectedYaml}\"", yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumFlagsWithMemberAttributes>(yaml));
        }

        [Fact]
        public static void EnumFlagsWithMemberAttributes_NamingPolicyAndDictionaryKeyPolicy_NotAppliedToCustomNames()
        {
            YamlSerializerOptions options = new()
            {
                Converters = { new YamlStringEnumConverter(YamlNamingPolicy.CamelCase) },
                DictionaryKeyPolicy = YamlNamingPolicy.KebabCaseUpper,
            };

            Dictionary<EnumFlagsWithMemberAttributes, EnumFlagsWithMemberAttributes> value = new()
            {
                [EnumFlagsWithMemberAttributes.Value1] = EnumFlagsWithMemberAttributes.Value1 | EnumFlagsWithMemberAttributes.Value2 |
                                                         EnumFlagsWithMemberAttributes.Value3 | EnumFlagsWithMemberAttributes.Value4,

                [EnumFlagsWithMemberAttributes.Value1 | EnumFlagsWithMemberAttributes.Value4] = EnumFlagsWithMemberAttributes.Value3,
                [EnumFlagsWithMemberAttributes.Value4] = EnumFlagsWithMemberAttributes.Value2,
            };

            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""
                {
                    "A": "A, B, C, value4",
                    "A, VALUE4": "C",
                    "VALUE4": "B"
                }
                """, yaml);
        }

        [Theory]
        [InlineData("\"a\"")]
        [InlineData("\"b\"")]
        [InlineData("\"A, b\"")]
        [InlineData("\"A, b, C, Value4\"")]
        [InlineData("\"A, B, c, Value4\"")]
        [InlineData("\"a, b, c, Value4\"")]
        [InlineData("\"c, B, A, Value4\"")]
        public static void EnumFlagsWithMemberAttributes_CustomizedValuesAreCaseSensitive(string yaml)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<EnumFlagsWithMemberAttributes>(yaml));
        }

        [Theory]
        [InlineData("\"value4\"", EnumFlagsWithMemberAttributes.Value4)]
        [InlineData("\"value4, VALUE4\"", EnumFlagsWithMemberAttributes.Value4)]
        [InlineData("\"A, value4, VALUE4, A,B,A,A\"", EnumFlagsWithMemberAttributes.Value1 | EnumFlagsWithMemberAttributes.Value2 | EnumFlagsWithMemberAttributes.Value4)]
        [InlineData("\"VALUE4, VAlUE5\"", EnumFlagsWithMemberAttributes.Value4 | EnumFlagsWithMemberAttributes.Value5)]
        public static void EnumFlagsWithMemberAttributes_DefaultValuesAreCaseInsensitive(string yaml, EnumFlagsWithMemberAttributes expectedValue)
        {
            EnumFlagsWithMemberAttributes value = YamlSerializer.Deserialize<EnumFlagsWithMemberAttributes>(yaml);
            Assert.Equal(expectedValue, value);
        }

        [Flags, YamlConverter(typeof(YamlStringEnumConverter<EnumFlagsWithMemberAttributes>))]
        public enum EnumFlagsWithMemberAttributes
        {
            [YamlStringEnumMemberName("A")]
            Value1 = 1,
            [YamlStringEnumMemberName("B")]
            Value2 = 2,
            [YamlStringEnumMemberName("C")]
            Value3 = 4,
            Value4 = 8,
            Value5 = 16,
        }

        [Theory]
        [InlineData(EnumWithConflictingMemberAttributes.Value1)]
        [InlineData(EnumWithConflictingMemberAttributes.Value2)]
        [InlineData(EnumWithConflictingMemberAttributes.Value3)]
        [Trait("category", "jsonoutput")]
        public static void EnumWithConflictingMemberAttributes_IsTolerated(EnumWithConflictingMemberAttributes value)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal("\"Value3\"", yaml);
            Assert.Equal(EnumWithConflictingMemberAttributes.Value1, YamlSerializer.Deserialize<EnumWithConflictingMemberAttributes>(yaml));
        }

        [YamlConverter(typeof(YamlStringEnumConverter<EnumWithConflictingMemberAttributes>))]
        public enum EnumWithConflictingMemberAttributes
        {
            [YamlStringEnumMemberName("Value3")]
            Value1 = 1,
            [YamlStringEnumMemberName("Value3")]
            Value2 = 2,
            Value3 = 3,
        }

        [Theory]
        [InlineData(EnumWithConflictingCaseNames.ValueWithConflictingCase, "\"ValueWithConflictingCase\"")]
        [InlineData(EnumWithConflictingCaseNames.VALUEwithCONFLICTINGcase, "\"VALUEwithCONFLICTINGcase\"")]
        [InlineData(EnumWithConflictingCaseNames.Value3, "\"VALUEWITHCONFLICTINGCASE\"")]
        [Trait("category", "jsonoutput")]
        public static void EnumWithConflictingCaseNames_SerializesAsExpected(EnumWithConflictingCaseNames value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(expectedYaml, yaml);
            EnumWithConflictingCaseNames deserializedValue = YamlSerializer.Deserialize<EnumWithConflictingCaseNames>(yaml);
            Assert.Equal(value, deserializedValue);
        }

        [Theory]
        [InlineData("\"valuewithconflictingcase\"")]
        [InlineData("\"vALUEwITHcONFLICTINGcASE\"")]
        public static void EnumWithConflictingCaseNames_DeserializingMismatchingCaseDefaultsToFirstValue(string yaml)
        {
            EnumWithConflictingCaseNames value = YamlSerializer.Deserialize<EnumWithConflictingCaseNames>(yaml);
            Assert.Equal(EnumWithConflictingCaseNames.ValueWithConflictingCase, value);
        }

        [YamlConverter(typeof(YamlStringEnumConverter))]
        public enum EnumWithConflictingCaseNames
        {
            ValueWithConflictingCase = 1,
            VALUEwithCONFLICTINGcase = 2,
            [YamlStringEnumMemberName("VALUEWITHCONFLICTINGCASE")]
            Value3 = 3,
        }

        [Theory]
        [InlineData(EnumWithValidMemberNames.Value1, "\"Intermediate whitespace\\t is allowed\\r\\nin enums\"")]
        [InlineData(EnumWithValidMemberNames.Value2, "\"Including support for commas, and other punctuation.\"")]
        [InlineData(EnumWithValidMemberNames.Value3, "\"Nice \\uD83D\\uDE80\\uD83D\\uDE80\\uD83D\\uDE80\"")]
        [InlineData(EnumWithValidMemberNames.Value4, "\"5\"")]
        [InlineData(EnumWithValidMemberNames.Value1 | EnumWithValidMemberNames.Value4, "5")]
        [Trait("category", "jsonoutput")]
        public static void EnumWithValidMemberNameOverrides(EnumWithValidMemberNames value, string expectedYamlString)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(expectedYamlString, yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumWithValidMemberNames>(yaml));
        }

        [Fact]
        public static void EnumWithNumberIdentifier_CanDeserializeAsUnderlyingValue()
        {
            EnumWithValidMemberNames value = YamlSerializer.Deserialize<EnumWithValidMemberNames>("\"4\"");
            Assert.Equal(EnumWithValidMemberNames.Value4, value);
        }

        [Fact]
        public static void EnumWithNumberIdentifier_NoNumberSupported_FailsWhenDeserializingUnderlyingValue()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<EnumWithValidMemberNames>("\"4\"", s_optionsWithStringAndNoIntegerEnumConverter));
        }

        [YamlConverter(typeof(YamlStringEnumConverter))]
        public enum EnumWithValidMemberNames
        {
            [YamlStringEnumMemberName("Intermediate whitespace\t is allowed\r\nin enums")]
            Value1 = 1,

            [YamlStringEnumMemberName("Including support for commas, and other punctuation.")]
            Value2 = 2,

            [YamlStringEnumMemberName("Nice 🚀🚀🚀")]
            Value3 = 3,

            [YamlStringEnumMemberName("5")]
            Value4 = 4
        }

        [Theory]
        [InlineData(EnumFlagsWithValidMemberNames.Value1 | EnumFlagsWithValidMemberNames.Value2, "\"Intermediate whitespace\\t is allowed\\r\\nin enums, Including support for some punctuation; except commas.\"")]
        [InlineData(EnumFlagsWithValidMemberNames.Value3 | EnumFlagsWithValidMemberNames.Value4, "\"Nice \\uD83D\\uDE80\\uD83D\\uDE80\\uD83D\\uDE80, 5\"")]
        [InlineData(EnumFlagsWithValidMemberNames.Value4, "\"5\"")]
        [Trait("category", "jsonoutput")]
        public static void EnumFlagsWithValidMemberNameOverrides(EnumFlagsWithValidMemberNames value, string expectedYamlString)
        {
            string yaml = YamlSerializer.Serialize(value);
            Assert.Equal(expectedYamlString, yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<EnumFlagsWithValidMemberNames>(yaml));
        }

        [Theory]
        [InlineData("\"\\r\\n Intermediate whitespace\\t is allowed\\r\\nin enums   ,      Including support for some punctuation; except commas.\\r\\n\"", EnumFlagsWithValidMemberNames.Value1 | EnumFlagsWithValidMemberNames.Value2)]
        [InlineData("\"         5\\t,     \\r\\n      5,\\t          5\"", EnumFlagsWithValidMemberNames.Value4)]
        public static void EnumFlagsWithValidMemberNameOverrides_SupportsWhitespaceSeparatedValues(string yaml, EnumFlagsWithValidMemberNames expectedValue)
        {
            EnumFlagsWithValidMemberNames result = YamlSerializer.Deserialize<EnumFlagsWithValidMemberNames>(yaml);
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData("\"\"")]
        [InlineData("\"    \\r\\n   \"")]
        [InlineData("\",\"")]
        [InlineData("\",,,\"")]
        [InlineData("\", \\r\\n,,\"")]
        [InlineData("\"\\r\\n Intermediate whitespace\\t is allowed\\r\\nin enums   ,  13 ,    Including support for some punctuation; except commas.\\r\\n\"")]
        [InlineData("\"\\r\\n Intermediate whitespace\\t is allowed\\r\\nin enums   ,  ,    Including support for some punctuation; except commas.\\r\\n\"")]
        [InlineData("\"         5\\t,     \\r\\n , UNKNOWN_IDENTIFIER \r\n,     5,\\t          5\"")]
        public static void EnumFlagsWithValidMemberNameOverrides_FailsOnInvalidYamlValues(string yaml)
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<EnumFlagsWithValidMemberNames>(yaml));
        }

        [Flags, YamlConverter(typeof(YamlStringEnumConverter))]
        public enum EnumFlagsWithValidMemberNames
        {
            [YamlStringEnumMemberName("Intermediate whitespace\t is allowed\r\nin enums")]
            Value1 = 1,

            [YamlStringEnumMemberName("Including support for some punctuation; except commas.")]
            Value2 = 2,

            [YamlStringEnumMemberName("Nice 🚀🚀🚀")]
            Value3 = 4,

            [YamlStringEnumMemberName("5")]
            Value4 = 8
        }

        [Theory]
        [InlineData(typeof(EnumWithInvalidMemberName1), "")]
        [InlineData(typeof(EnumWithInvalidMemberName2), "")]
        [InlineData(typeof(EnumWithInvalidMemberName3), "   ")]
        [InlineData(typeof(EnumWithInvalidMemberName4), "   HasLeadingWhitespace")]
        [InlineData(typeof(EnumWithInvalidMemberName5), "HasTrailingWhitespace\n")]
        [InlineData(typeof(EnumWithInvalidMemberName6), "Comma separators not allowed, in flags enums")]
        public static void EnumWithInvalidMemberName_Throws(Type enumType, string memberName)
        {
            object value = Activator.CreateInstance(enumType);
            string expectedExceptionMessage = $"Enum type '{enumType.Name}' uses unsupported identifier '{memberName}'.";
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, enumType, s_optionsWithStringEnumConverter));
            Assert.Contains(expectedExceptionMessage, ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("\"str\"", enumType, s_optionsWithStringEnumConverter));
            Assert.Contains(expectedExceptionMessage, ex.Message);
        }

        public enum EnumWithInvalidMemberName1
        {
            [YamlStringEnumMemberName(null!)]
            Value
        }

        [Flags]
        public enum EnumWithInvalidMemberName2
        {
            [YamlStringEnumMemberName("")]
            Value
        }

        public enum EnumWithInvalidMemberName3
        {
            [YamlStringEnumMemberName("   ")]
            Value
        }

        public enum EnumWithInvalidMemberName4
        {
            [YamlStringEnumMemberName("   HasLeadingWhitespace")]
            Value
        }

        public enum EnumWithInvalidMemberName5
        {
            [YamlStringEnumMemberName("HasTrailingWhitespace\n")]
            Value
        }

        [Flags]
        public enum EnumWithInvalidMemberName6
        {
            [YamlStringEnumMemberName("Comma separators not allowed, in flags enums")]
            Value
        }

        [Theory]
        [InlineData(YesOrNoOrEmpty.Yes, "\"y\"")]
        [InlineData(YesOrNoOrEmpty.No, "\"n\"")]
        [InlineData(YesOrNoOrEmpty.Empty, "\"\"")]
        [Trait("category", "jsonoutput")]
        public static void EnumWithEmptyStringMemberName_NonFlags_RoundtripsAsExpected(YesOrNoOrEmpty value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value, s_optionsWithStringEnumConverter);
            Assert.Equal(expectedYaml, yaml);
            Assert.Equal(value, YamlSerializer.Deserialize<YesOrNoOrEmpty>(yaml, s_optionsWithStringEnumConverter));
        }

        [Theory]
        [InlineData("\"  \"")]
        [InlineData("\"\\t\"")]
        public static void EnumWithEmptyStringMemberName_NonFlags_ParsesWhitespaceAsEmptyMember(string yaml)
        {
            Assert.Equal(YesOrNoOrEmpty.Empty, YamlSerializer.Deserialize<YesOrNoOrEmpty>(yaml, s_optionsWithStringEnumConverter));
        }

        public enum YesOrNoOrEmpty
        {
            [YamlStringEnumMemberName("y")]
            Yes,

            [YamlStringEnumMemberName("n")]
            No,

            [YamlStringEnumMemberName("")]
            Empty,
        }

        [Fact]
        public static void EnumWithEmptyStringMemberName_Flags_Throws()
        {
            string expectedExceptionMessage = $"Enum type '{nameof(YesOrNoOrEmptyFlags)}' uses unsupported identifier ''.";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => YamlSerializer.Serialize(YesOrNoOrEmptyFlags.Yes, s_optionsWithStringEnumConverter));
            Assert.Contains(expectedExceptionMessage, ex.Message);

            ex = Assert.Throws<InvalidOperationException>(
                () => YamlSerializer.Deserialize<YesOrNoOrEmptyFlags>("\"y\"", s_optionsWithStringEnumConverter));
            Assert.Contains(expectedExceptionMessage, ex.Message);
        }

        [Flags]
        public enum YesOrNoOrEmptyFlags
        {
            [YamlStringEnumMemberName("y")]
            Yes = 1,

            [YamlStringEnumMemberName("n")]
            No = 2,

            [YamlStringEnumMemberName("")]
            Empty = 4,
        }

        [Theory]
        [InlineData("\"cAmElCaSe\"", EnumWithVaryingNamingPolicies.camelCase, YamlKnownNamingPolicy.SnakeCaseUpper)]
        [InlineData("\"cAmElCaSe\"", EnumWithVaryingNamingPolicies.camelCase, YamlKnownNamingPolicy.SnakeCaseLower)]
        [InlineData("\"cAmElCaSe\"", EnumWithVaryingNamingPolicies.camelCase, YamlKnownNamingPolicy.KebabCaseUpper)]
        [InlineData("\"cAmElCaSe\"", EnumWithVaryingNamingPolicies.camelCase, YamlKnownNamingPolicy.KebabCaseLower)]
        [InlineData("\"pAsCaLcAsE\"", EnumWithVaryingNamingPolicies.PascalCase, YamlKnownNamingPolicy.SnakeCaseUpper)]
        [InlineData("\"pAsCaLcAsE\"", EnumWithVaryingNamingPolicies.PascalCase, YamlKnownNamingPolicy.SnakeCaseLower)]
        [InlineData("\"pAsCaLcAsE\"", EnumWithVaryingNamingPolicies.PascalCase, YamlKnownNamingPolicy.KebabCaseUpper)]
        [InlineData("\"pAsCaLcAsE\"", EnumWithVaryingNamingPolicies.PascalCase, YamlKnownNamingPolicy.KebabCaseLower)]
        [InlineData("\"sNaKe_CaSe_UpPeR\"", EnumWithVaryingNamingPolicies.SNAKE_CASE_UPPER, YamlKnownNamingPolicy.SnakeCaseUpper)]
        [InlineData("\"sNaKe_CaSe_LoWeR\"", EnumWithVaryingNamingPolicies.snake_case_lower, YamlKnownNamingPolicy.SnakeCaseLower)]
        [InlineData("\"cAmElCaSe\"", EnumWithVaryingNamingPolicies.camelCase, YamlKnownNamingPolicy.CamelCase)]
        [InlineData("\"a\"", EnumWithVaryingNamingPolicies.A, YamlKnownNamingPolicy.CamelCase)]
        [InlineData("\"a\"", EnumWithVaryingNamingPolicies.A, YamlKnownNamingPolicy.SnakeCaseUpper)]
        [InlineData("\"a\"", EnumWithVaryingNamingPolicies.A, YamlKnownNamingPolicy.SnakeCaseLower)]
        [InlineData("\"a\"", EnumWithVaryingNamingPolicies.A, YamlKnownNamingPolicy.KebabCaseUpper)]
        [InlineData("\"a\"", EnumWithVaryingNamingPolicies.A, YamlKnownNamingPolicy.KebabCaseLower)]
        [InlineData("\"B\"", EnumWithVaryingNamingPolicies.b, YamlKnownNamingPolicy.CamelCase)]
        [InlineData("\"B\"", EnumWithVaryingNamingPolicies.b, YamlKnownNamingPolicy.SnakeCaseUpper)]
        [InlineData("\"B\"", EnumWithVaryingNamingPolicies.b, YamlKnownNamingPolicy.SnakeCaseLower)]
        [InlineData("\"B\"", EnumWithVaryingNamingPolicies.b, YamlKnownNamingPolicy.KebabCaseUpper)]
        [InlineData("\"B\"", EnumWithVaryingNamingPolicies.b, YamlKnownNamingPolicy.KebabCaseLower)]
        public static void StringConverterWithNamingPolicyIsCaseInsensitive(string yaml, EnumWithVaryingNamingPolicies expectedValue, YamlKnownNamingPolicy namingPolicy)
        {
            YamlNamingPolicy policy = namingPolicy switch
            {
                YamlKnownNamingPolicy.CamelCase => YamlNamingPolicy.CamelCase,
                YamlKnownNamingPolicy.SnakeCaseLower => YamlNamingPolicy.SnakeCaseLower,
                YamlKnownNamingPolicy.SnakeCaseUpper => YamlNamingPolicy.SnakeCaseUpper,
                YamlKnownNamingPolicy.KebabCaseLower => YamlNamingPolicy.KebabCaseLower,
                YamlKnownNamingPolicy.KebabCaseUpper => YamlNamingPolicy.KebabCaseUpper,
                _ => throw new ArgumentOutOfRangeException(nameof(namingPolicy)),
            };

            YamlSerializerOptions options = new() { Converters = { new YamlStringEnumConverter(policy) } };

            EnumWithVaryingNamingPolicies value = YamlSerializer.Deserialize<EnumWithVaryingNamingPolicies>(yaml, options);
            Assert.Equal(expectedValue, value);
        }

        public enum EnumWithVaryingNamingPolicies
        {
            SNAKE_CASE_UPPER,
            snake_case_lower,
            camelCase,
            PascalCase,
            A,
            b,
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void EnumWithOverlappingBitsTests()
        {
            YamlSerializerOptions options = new() { Converters = { new YamlStringEnumConverter() } };

            EnumWithOverlappingBits e1 = EnumWithOverlappingBits.BITS01 | EnumWithOverlappingBits.BIT3;
            string yaml1 = YamlSerializer.Serialize(e1, options);
            Assert.Equal("\"BITS01, BIT3\"", yaml1);

            EnumWithOverlappingBits2 e2 = EnumWithOverlappingBits2.BITS01 | EnumWithOverlappingBits2.BIT3;
            string yaml2 = YamlSerializer.Serialize(e2, options);
            Assert.Equal("\"BITS01, BIT3\"", yaml2);
        }

        [Flags]
        public enum EnumWithOverlappingBits
        {
            UNKNOWN = 0,
            BIT0 = 1,
            BIT1 = 2,
            BIT2 = 4,
            BIT3 = 8,
            BITS01 = 3,
        }

        [Flags]
        public enum EnumWithOverlappingBits2
        {
            UNKNOWN = 0,
            BIT0 = 1,
            // direct option for bit 1 missing
            BIT2 = 4,
            BIT3 = 8,
            BITS01 = 3,
        }
    }
}
