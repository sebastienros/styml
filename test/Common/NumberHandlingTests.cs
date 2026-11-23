// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Yaml.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class NumberHandlingTests : SerializerTests
    {
        public NumberHandlingTests(YamlSerializerWrapper serializerWrapper) : base(serializerWrapper) { }

        private static readonly YamlSerializerOptions s_optionReadFromStr = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.AllowReadingFromString
        };

        private static readonly YamlSerializerOptions s_optionWriteAsStr = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.WriteAsString
        };

        private static readonly YamlSerializerOptions s_optionReadAndWriteFromStr = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString
        };

        private static readonly YamlSerializerOptions s_optionsAllowFloatConstants = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals
        };

        private static readonly YamlSerializerOptions s_optionReadFromStrAllowFloatConstants = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.AllowNamedFloatingPointLiterals
        };

        private static readonly YamlSerializerOptions s_optionWriteAsStrAllowFloatConstants = new YamlSerializerOptions
        {
            NumberHandling = YamlNumberHandling.WriteAsString | YamlNumberHandling.AllowNamedFloatingPointLiterals
        };

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Number_AsRootType_RoundTrip()
        {
            await RunAsRootTypeTest(YamlNumberTestData.Bytes);
            await RunAsRootTypeTest(YamlNumberTestData.SBytes);
            await RunAsRootTypeTest(YamlNumberTestData.Shorts);
            await RunAsRootTypeTest(YamlNumberTestData.Ints);
            await RunAsRootTypeTest(YamlNumberTestData.Longs);
            await RunAsRootTypeTest(YamlNumberTestData.UShorts);
            await RunAsRootTypeTest(YamlNumberTestData.UInts);
            await RunAsRootTypeTest(YamlNumberTestData.ULongs);
            await RunAsRootTypeTest(YamlNumberTestData.Floats);
            await RunAsRootTypeTest(YamlNumberTestData.Doubles);
            await RunAsRootTypeTest(YamlNumberTestData.Decimals);
#if NET
            await RunAsRootTypeTest(YamlNumberTestData.Int128s);
            await RunAsRootTypeTest(YamlNumberTestData.UInt128s);
            await RunAsRootTypeTest(YamlNumberTestData.Halfs);
#endif
            await RunAsRootTypeTest(YamlNumberTestData.NullableBytes);
            await RunAsRootTypeTest(YamlNumberTestData.NullableSBytes);
            await RunAsRootTypeTest(YamlNumberTestData.NullableShorts);
            await RunAsRootTypeTest(YamlNumberTestData.NullableInts);
            await RunAsRootTypeTest(YamlNumberTestData.NullableLongs);
            await RunAsRootTypeTest(YamlNumberTestData.NullableUShorts);
            await RunAsRootTypeTest(YamlNumberTestData.NullableUInts);
            await RunAsRootTypeTest(YamlNumberTestData.NullableULongs);
            await RunAsRootTypeTest(YamlNumberTestData.NullableFloats);
            await RunAsRootTypeTest(YamlNumberTestData.NullableDoubles);
            await RunAsRootTypeTest(YamlNumberTestData.NullableDecimals);
#if NET
            await RunAsRootTypeTest(YamlNumberTestData.NullableInt128s);
            await RunAsRootTypeTest(YamlNumberTestData.NullableUInt128s);
            await RunAsRootTypeTest(YamlNumberTestData.NullableHalfs);
#endif
        }

        private async Task RunAsRootTypeTest<T>(List<T> numbers)
        {
            foreach (T number in numbers)
            {
                string numberAsString = GetNumberAsString(number);
                string yaml = $"{numberAsString}";
                string yamlWithNumberAsString = @$"""{numberAsString}""";
                await PerformAsRootTypeSerialization(number, yaml, yamlWithNumberAsString);
            }
        }

        private static string GetNumberAsString<T>(T number)
        {
            return number switch
            {
                double @double => @double.ToString(YamlTestHelper.DoubleFormatString, CultureInfo.InvariantCulture),
                float @float => @float.ToString(YamlTestHelper.SingleFormatString, CultureInfo.InvariantCulture),
                decimal @decimal => @decimal.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(number, CultureInfo.InvariantCulture)
            };
        }

        private async Task PerformAsRootTypeSerialization<T>(T number, string yamlWithNumberAsNumber, string yamlWithNumberAsString)
        {
            // Option: read from string

            // Deserialize
            Assert.Equal(number, await Serializer.DeserializeWrapper<T>(yamlWithNumberAsNumber, s_optionReadFromStr));
            Assert.Equal(number, await Serializer.DeserializeWrapper<T>(yamlWithNumberAsString, s_optionReadFromStr));

            // Serialize
            Assert.Equal(yamlWithNumberAsNumber, await Serializer.SerializeWrapper(number, s_optionReadFromStr));

            // Option: write as string

            // Deserialize
            Assert.Equal(number, await Serializer.DeserializeWrapper<T>(yamlWithNumberAsNumber, s_optionWriteAsStr));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(yamlWithNumberAsString, s_optionWriteAsStr));

            // Serialize
            Assert.Equal(yamlWithNumberAsString, await Serializer.SerializeWrapper(number, s_optionWriteAsStr));

            // Option: read and write from/to string

            // Deserialize
            Assert.Equal(number, await Serializer.DeserializeWrapper<T>(yamlWithNumberAsNumber, s_optionReadAndWriteFromStr));
            Assert.Equal(number, await Serializer.DeserializeWrapper<T>(yamlWithNumberAsString, s_optionReadAndWriteFromStr));

            // Serialize
            Assert.Equal(yamlWithNumberAsString, await Serializer.SerializeWrapper(number, s_optionReadAndWriteFromStr));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Number_AsBoxed_RootType()
        {
            string numberAsString = """
                "2"
                """;

            int @int = 2;
            float @float = 2;
            int? nullableInt = 2;
            float? nullableFloat = 2;

            Assert.Equal(numberAsString, await Serializer.SerializeWrapper((object)@int, s_optionReadAndWriteFromStr));
            Assert.Equal(numberAsString, await Serializer.SerializeWrapper((object)@float, s_optionReadAndWriteFromStr));
            Assert.Equal(numberAsString, await Serializer.SerializeWrapper((object)nullableInt, s_optionReadAndWriteFromStr));
            Assert.Equal(numberAsString, await Serializer.SerializeWrapper((object)nullableFloat, s_optionReadAndWriteFromStr));

            Assert.Equal(2, (int)await Serializer.DeserializeWrapper(numberAsString, typeof(int), s_optionReadAndWriteFromStr));
            Assert.Equal(2, (float)await Serializer.DeserializeWrapper(numberAsString, typeof(float), s_optionReadAndWriteFromStr));
            Assert.Equal(2, (int?)await Serializer.DeserializeWrapper(numberAsString, typeof(int?), s_optionReadAndWriteFromStr));
            Assert.Equal(2, (float?)await Serializer.DeserializeWrapper(numberAsString, typeof(float?), s_optionReadAndWriteFromStr));
        }

        [Fact]
        public async Task Number_AsBoxed_Property()
        {
            int @int = 1;
            float? nullableFloat = 2;

            string expected = """{"MyInt":"1","MyNullableFloat":"2"}""";

            var obj = new Class_With_BoxedNumbers
            {
                MyInt = @int,
                MyNullableFloat = nullableFloat
            };

            string serialized = await Serializer.SerializeWrapper(obj);
            YamlTestHelper.AssertYamlEqual(expected, serialized);

            obj = await Serializer.DeserializeWrapper<Class_With_BoxedNumbers>(serialized);

            YamlElement el = Assert.IsType<YamlElement>(obj.MyInt);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("1", el.GetString());

            el = Assert.IsType<YamlElement>(obj.MyNullableFloat);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("2", el.GetString());
        }

        public class Class_With_BoxedNumbers
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public object MyInt { get; set; }

            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public object MyNullableFloat { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Number_AsBoxed_CollectionRootType_Element()
        {
            int @int = 1;
            float? nullableFloat = 2;

            string expected = """["1"]""";

            var obj = new List<object> { @int };
            string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
            Assert.Equal(expected, serialized);

            obj = await Serializer.DeserializeWrapper<List<object>>(serialized, s_optionReadAndWriteFromStr);

            YamlElement el = Assert.IsType<YamlElement>(obj[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("1", el.GetString());

            expected = """["2"]""";

            IList obj2 = new object[] { nullableFloat };
            serialized = await Serializer.SerializeWrapper(obj2, s_optionReadAndWriteFromStr);
            Assert.Equal(expected, serialized);

            obj2 = await Serializer.DeserializeWrapper<IList>(serialized, s_optionReadAndWriteFromStr);

            el = Assert.IsType<YamlElement>(obj2[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("2", el.GetString());
        }

        [Fact]
        public async Task Number_AsBoxed_CollectionProperty_Element()
        {
            int @int = 2;
            float? nullableFloat = 2;

            string expected = """{"MyInts":["2"],"MyNullableFloats":["2"]}""";

            var obj = new Class_With_ListsOfBoxedNumbers
            {
                MyInts = new List<object> { @int },
                MyNullableFloats = new object[] { nullableFloat }
            };

            string serialized = await Serializer.SerializeWrapper(obj);
            YamlTestHelper.AssertYamlEqual(expected, serialized);

            obj = await Serializer.DeserializeWrapper<Class_With_ListsOfBoxedNumbers>(serialized);

            YamlElement el = Assert.IsType<YamlElement>(obj.MyInts[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("2", el.GetString());

            el = Assert.IsType<YamlElement>(obj.MyNullableFloats[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal("2", el.GetString());
        }

        public class Class_With_ListsOfBoxedNumbers
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public List<object> MyInts { get; set; }

            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public IList MyNullableFloats { get; set; }
        }

        [Fact]
        public async Task NonNumber_AsBoxed_Property()
        {
            DateTime dateTime = DateTimeTestHelpers.FixedDateTimeValue;
            Guid? nullableGuid = Guid.NewGuid();

            string expected = @$"{{""MyDateTime"":{await Serializer.SerializeWrapper(dateTime)},""MyNullableGuid"":{await Serializer.SerializeWrapper(nullableGuid)}}}";

            var obj = new Class_With_BoxedNonNumbers
            {
                MyDateTime = dateTime,
                MyNullableGuid = nullableGuid
            };

            string serialized = await Serializer.SerializeWrapper(obj);
            YamlTestHelper.AssertYamlEqual(expected, serialized);

            obj = await Serializer.DeserializeWrapper<Class_With_BoxedNonNumbers>(serialized);

            YamlElement el = Assert.IsType<YamlElement>(obj.MyDateTime);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(dateTime, el.GetDateTime());

            el = Assert.IsType<YamlElement>(obj.MyNullableGuid);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(nullableGuid.Value, el.GetGuid());
        }

        public class Class_With_BoxedNonNumbers
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public object MyDateTime { get; set; }

            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public object MyNullableGuid { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NonNumber_AsBoxed_CollectionRootType_Element()
        {
            DateTime dateTime = DateTimeTestHelpers.FixedDateTimeValue;
            Guid? nullableGuid = Guid.NewGuid();

            string expected = @$"[{await Serializer.SerializeWrapper(dateTime)}]";

            var obj = new List<object> { dateTime };
            string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
            Assert.Equal(expected, serialized);

            obj = await Serializer.DeserializeWrapper<List<object>>(serialized, s_optionReadAndWriteFromStr);

            YamlElement el = Assert.IsType<YamlElement>(obj[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(dateTime, el.GetDateTime());

            expected = @$"[{await Serializer.SerializeWrapper(nullableGuid)}]";

            IList obj2 = new object[] { nullableGuid };
            serialized = await Serializer.SerializeWrapper(obj2, s_optionReadAndWriteFromStr);
            Assert.Equal(expected, serialized);

            obj2 = await Serializer.DeserializeWrapper<IList>(serialized, s_optionReadAndWriteFromStr);

            el = Assert.IsType<YamlElement>(obj2[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(nullableGuid.Value, el.GetGuid());
        }

        [Fact]
        public async Task NonNumber_AsBoxed_CollectionProperty_Element()
        {
            DateTime dateTime = DateTimeTestHelpers.FixedDateTimeValue;
            Guid? nullableGuid = Guid.NewGuid();

            string expected = @$"{{""MyDateTimes"":[{await Serializer.SerializeWrapper(dateTime)}],""MyNullableGuids"":[{await Serializer.SerializeWrapper(nullableGuid)}]}}";

            var obj = new Class_With_ListsOfBoxedNonNumbers
            {
                MyDateTimes = new List<object> { dateTime },
                MyNullableGuids = new object[] { nullableGuid }
            };

            string serialized = await Serializer.SerializeWrapper(obj);
            YamlTestHelper.AssertYamlEqual(expected, serialized);

            obj = await Serializer.DeserializeWrapper<Class_With_ListsOfBoxedNonNumbers>(serialized);

            YamlElement el = Assert.IsType<YamlElement>(obj.MyDateTimes[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(dateTime, el.GetDateTime());

            el = Assert.IsType<YamlElement>(obj.MyNullableGuids[0]);
            Assert.Equal(YamlValueKind.String, el.ValueKind);
            Assert.Equal(nullableGuid, el.GetGuid());
        }

        public class Class_With_ListsOfBoxedNonNumbers
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public List<object> MyDateTimes { get; set; }

            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public IList MyNullableGuids { get; set; }
        }

        [Fact]
        [RequiresUnreferencedCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        [RequiresDynamicCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        [Trait("category", "jsonoutput")]
        public async Task Number_AsCollectionElement_RoundTrip()
        {
            await RunAsCollectionElementTest(YamlNumberTestData.Bytes);
            await RunAsCollectionElementTest(YamlNumberTestData.SBytes);
            await RunAsCollectionElementTest(YamlNumberTestData.Shorts);
            await RunAsCollectionElementTest(YamlNumberTestData.Ints);
            await RunAsCollectionElementTest(YamlNumberTestData.Longs);
            await RunAsCollectionElementTest(YamlNumberTestData.UShorts);
            await RunAsCollectionElementTest(YamlNumberTestData.UInts);
            await RunAsCollectionElementTest(YamlNumberTestData.ULongs);
            await RunAsCollectionElementTest(YamlNumberTestData.Floats);
            await RunAsCollectionElementTest(YamlNumberTestData.Doubles);
            await RunAsCollectionElementTest(YamlNumberTestData.Decimals);
#if NET
            await RunAsCollectionElementTest(YamlNumberTestData.Int128s);
            await RunAsCollectionElementTest(YamlNumberTestData.UInt128s);
            await RunAsCollectionElementTest(YamlNumberTestData.Halfs);
#endif

            // https://github.com/dotnet/runtime/issues/66220
            if (!PlatformDetection.IsAppleMobile)
            {
                await RunAsCollectionElementTest(YamlNumberTestData.NullableBytes);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableSBytes);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableShorts);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableInts);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableLongs);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableUShorts);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableUInts);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableULongs);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableFloats);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableDoubles);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableDecimals);
#if NET
                await RunAsCollectionElementTest(YamlNumberTestData.NullableInt128s);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableUInt128s);
                await RunAsCollectionElementTest(YamlNumberTestData.NullableHalfs);
#endif
            }
        }

        [RequiresUnreferencedCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        [RequiresDynamicCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        private async Task RunAsCollectionElementTest<T>(List<T> numbers)
        {
            StringBuilder yamlBuilder_NumbersAsNumbers = new StringBuilder();
            StringBuilder yamlBuilder_NumbersAsStrings = new StringBuilder();
            StringBuilder yamlBuilder_NumbersAsNumbersAndStrings = new StringBuilder();
            StringBuilder yamlBuilder_NumbersAsNumbersAndStrings_Alternate = new StringBuilder();
            bool asNumber = false;

            yamlBuilder_NumbersAsNumbers.Append("[");
            yamlBuilder_NumbersAsStrings.Append("[");
            yamlBuilder_NumbersAsNumbersAndStrings.Append("[");
            yamlBuilder_NumbersAsNumbersAndStrings_Alternate.Append("[");

            foreach (T number in numbers)
            {
                string numberAsString = GetNumberAsString(number);

                string yamlWithNumberAsString = @$"""{numberAsString}""";

                yamlBuilder_NumbersAsNumbers.Append($"{numberAsString},");
                yamlBuilder_NumbersAsStrings.Append($"{yamlWithNumberAsString},");
                yamlBuilder_NumbersAsNumbersAndStrings.Append(asNumber
                    ? $"{numberAsString},"
                    : $"{yamlWithNumberAsString},");
                yamlBuilder_NumbersAsNumbersAndStrings_Alternate.Append(!asNumber
                    ? $"{numberAsString},"
                    : $"{yamlWithNumberAsString},");

                asNumber = !asNumber;
            }

            yamlBuilder_NumbersAsNumbers.Remove(yamlBuilder_NumbersAsNumbers.Length - 1, 1);
            yamlBuilder_NumbersAsStrings.Remove(yamlBuilder_NumbersAsStrings.Length - 1, 1);
            yamlBuilder_NumbersAsNumbersAndStrings.Remove(yamlBuilder_NumbersAsNumbersAndStrings.Length - 1, 1);
            yamlBuilder_NumbersAsNumbersAndStrings_Alternate.Remove(yamlBuilder_NumbersAsNumbersAndStrings_Alternate.Length - 1, 1);

            yamlBuilder_NumbersAsNumbers.Append("]");
            yamlBuilder_NumbersAsStrings.Append("]");
            yamlBuilder_NumbersAsNumbersAndStrings.Append("]");
            yamlBuilder_NumbersAsNumbersAndStrings_Alternate.Append("]");

            string yamlNumbersAsStrings = yamlBuilder_NumbersAsStrings.ToString();

            await PerformAsCollectionElementSerialization(
                numbers,
                yamlBuilder_NumbersAsNumbers.ToString(),
                yamlNumbersAsStrings,
                yamlBuilder_NumbersAsNumbersAndStrings.ToString(),
                yamlBuilder_NumbersAsNumbersAndStrings_Alternate.ToString());

            // Reflection based tests for every collection type.
            await RunAllCollectionsRoundTripTest<T>(yamlNumbersAsStrings);
        }

        private async Task PerformAsCollectionElementSerialization<T>(
            List<T> numbers,
            string yaml_NumbersAsNumbers,
            string yaml_NumbersAsStrings,
            string yaml_NumbersAsNumbersAndStrings,
            string yaml_NumbersAsNumbersAndStrings_Alternate)
        {
            List<T> deserialized;

            // Option: read from string

            // Deserialize
            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbers, s_optionReadFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsStrings, s_optionReadFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings, s_optionReadFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings_Alternate, s_optionReadFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            // Serialize
            Assert.Equal(yaml_NumbersAsNumbers, await Serializer.SerializeWrapper(numbers, s_optionReadFromStr));

            // Option: write as string

            // Deserialize
            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbers, s_optionWriteAsStr);
            AssertIEnumerableEqual(numbers, deserialized);

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsStrings, s_optionWriteAsStr));

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings, s_optionWriteAsStr));

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings_Alternate, s_optionWriteAsStr));

            // Serialize
            Assert.Equal(yaml_NumbersAsStrings, await Serializer.SerializeWrapper(numbers, s_optionWriteAsStr));

            // Option: read and write from/to string

            // Deserialize
            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbers, s_optionReadAndWriteFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsStrings, s_optionReadAndWriteFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings, s_optionReadAndWriteFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            deserialized = await Serializer.DeserializeWrapper<List<T>>(yaml_NumbersAsNumbersAndStrings_Alternate, s_optionReadAndWriteFromStr);
            AssertIEnumerableEqual(numbers, deserialized);

            // Serialize
            Assert.Equal(yaml_NumbersAsStrings, await Serializer.SerializeWrapper(numbers, s_optionReadAndWriteFromStr));
        }

        private void AssertIEnumerableEqual<T>(IEnumerable<T> list1, IEnumerable<T> list2)
        {
            IEnumerator<T> enumerator1 = list1.GetEnumerator();
            IEnumerator<T> enumerator2 = list2.GetEnumerator();

            while (enumerator1.MoveNext())
            {
                enumerator2.MoveNext();
                Assert.Equal(enumerator1.Current, enumerator2.Current);
            }

            Assert.False(enumerator2.MoveNext());
        }

        [RequiresUnreferencedCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        [RequiresDynamicCode("Uses Activator.CreateInstance to construct non-generic collection types.")]
        private async Task RunAllCollectionsRoundTripTest<T>(string yaml)
        {
            foreach (Type type in CollectionTestTypes.DeserializableGenericEnumerableTypes<T>())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    HashSet<T> obj1 = (HashSet<T>)await Serializer.DeserializeWrapper(yaml, type, s_optionReadAndWriteFromStr);
                    string serialized = await Serializer.SerializeWrapper(obj1, s_optionReadAndWriteFromStr);

                    HashSet<T> obj2 = (HashSet<T>)await Serializer.DeserializeWrapper(serialized, type, s_optionReadAndWriteFromStr);

                    Assert.Equal(obj1.Count, obj2.Count);
                    foreach (T element in obj1)
                    {
                        Assert.True(obj2.Contains(element));
                    }
                }
                else if (type != typeof(byte[]))
                {
                    object obj = await Serializer.DeserializeWrapper(yaml, type, s_optionReadAndWriteFromStr);
                    string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
                    Assert.Equal(yaml, serialized);
                }
            }

            foreach (Type type in CollectionTestTypes.DeserializableNonGenericEnumerableTypes())
            {
                // Deserialized as collection of YamlElements.
                object obj = await Serializer.DeserializeWrapper(yaml, type, s_optionReadAndWriteFromStr);
                // Serialized as strings with escaping.
                string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);

                // Ensure escaped values were serialized accurately
                List<T> list = await Serializer.DeserializeWrapper<List<T>>(serialized, s_optionReadAndWriteFromStr);
                serialized = await Serializer.SerializeWrapper(list, s_optionReadAndWriteFromStr);
                Assert.Equal(yaml, serialized);

                if (PlatformDetection.IsNotNativeAot)
                {
                    // Serialize instance which is a collection of numbers (not YamlElements).
                    obj = Activator.CreateInstance(type, new[] { list });
                    serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
                    Assert.Equal(yaml, serialized);
                }
            }
        }

        [Fact]
        public async Task Number_AsDictionaryElement_RoundTrip()
        {
            var dict = new Dictionary<int, float>();
            for (int i = 0; i < 10; i++)
            {
                dict[YamlNumberTestData.Ints[i]] = YamlNumberTestData.Floats[i];
            }

            // Serialize
            string serialized = await Serializer.SerializeWrapper(dict, s_optionReadAndWriteFromStr);
            AssertDictionaryElements_StringValues(serialized);

            // Deserialize
            dict = await Serializer.DeserializeWrapper<Dictionary<int, float>>(serialized, s_optionReadAndWriteFromStr);

            // Test roundtrip
            YamlTestHelper.AssertYamlEqual(serialized, await Serializer.SerializeWrapper(dict, s_optionReadAndWriteFromStr));
        }

        private void AssertDictionaryElements_StringValues(string serialized)
        {
            Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(serialized));
            reader.Read();
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndObject)
                {
                    break;
                }
                else if (reader.TokenType == YamlTokenType.String)
                {
                    Assert.True(reader.ValueSpan.IndexOf((byte)'\\') == -1);
                }
                else
                {
                    Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
                }
            }
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/39674", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoInterpreter))]
        [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
        [RequiresUnreferencedCode("Uses Activator.CreateInstance to construct non-generic dictionary types.")]
        [RequiresDynamicCode("Uses Activator.CreateInstance to construct non-generic dictionary types.")]
        public async Task DictionariesRoundTrip()
        {
            await RunAllDictionariessRoundTripTest(YamlNumberTestData.ULongs);
            await RunAllDictionariessRoundTripTest(YamlNumberTestData.Floats);
            await RunAllDictionariessRoundTripTest(YamlNumberTestData.Doubles);
        }

        [RequiresUnreferencedCode("Uses Activator.CreateInstance to construct non-generic dictionary types.")]
        [RequiresDynamicCode("Uses Activator.CreateInstance to construct non-generic dictionary types.")]
        private async Task RunAllDictionariessRoundTripTest<T>(List<T> numbers)
        {
            StringBuilder yamlBuilder_NumbersAsStrings = new StringBuilder();

            yamlBuilder_NumbersAsStrings.Append("{");

            foreach (T number in numbers)
            {
                string numberAsString = GetNumberAsString(number);
                string yamlWithNumberAsString = @$"""{numberAsString}""";

                yamlBuilder_NumbersAsStrings.Append($"{yamlWithNumberAsString}:");
                yamlBuilder_NumbersAsStrings.Append($"{yamlWithNumberAsString},");
            }

            yamlBuilder_NumbersAsStrings.Remove(yamlBuilder_NumbersAsStrings.Length - 1, 1);
            yamlBuilder_NumbersAsStrings.Append("}");

            string yamlNumbersAsStrings = yamlBuilder_NumbersAsStrings.ToString();

            foreach (Type type in CollectionTestTypes.DeserializableDictionaryTypes<string, T>())
            {
                object obj = await Serializer.DeserializeWrapper(yamlNumbersAsStrings, type, s_optionReadAndWriteFromStr);
                YamlTestHelper.AssertYamlEqual(yamlNumbersAsStrings, await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
            }

            if (PlatformDetection.IsNotNativeAot)
            {
                foreach (Type type in CollectionTestTypes.DeserializableNonGenericDictionaryTypes())
                {
                    Dictionary<T, T> dict = await Serializer.DeserializeWrapper<Dictionary<T, T>>(yamlNumbersAsStrings, s_optionReadAndWriteFromStr);

                    // Serialize instance which is a dictionary of numbers (not YamlElements).
                    object obj = Activator.CreateInstance(type, new[] { dict });
                    string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
                    YamlTestHelper.AssertYamlEqual(yamlNumbersAsStrings, serialized);
                }
            }
        }

        [Fact]
        public async Task Number_AsPropertyValue_RoundTrip()
        {
            var obj = new Class_With_NullableUInt64_And_Float()
            {
                NullableUInt64Number = YamlNumberTestData.NullableULongs.LastOrDefault(),
                FloatNumbers = YamlNumberTestData.Floats
            };

            // Serialize
            string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);

            // Deserialize
            obj = await Serializer.DeserializeWrapper<Class_With_NullableUInt64_And_Float>(serialized, s_optionReadAndWriteFromStr);

            // Test roundtrip
            YamlTestHelper.AssertYamlEqual(serialized, await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
        }

        public class Class_With_NullableUInt64_And_Float
        {
            public ulong? NullableUInt64Number { get; set; }
            [YamlInclude]
            public List<float> FloatNumbers;
        }

        [Fact]
        public async Task Number_AsKeyValuePairValue_RoundTrip()
        {
            var obj = new KeyValuePair<ulong?, List<float>>(YamlNumberTestData.NullableULongs.LastOrDefault(), YamlNumberTestData.Floats);

            // Serialize
            string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);

            // Deserialize
            obj = await Serializer.DeserializeWrapper<KeyValuePair<ulong?, List<float>>>(serialized, s_optionReadAndWriteFromStr);

            // Test roundtrip
            YamlTestHelper.AssertYamlEqual(serialized, await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
        }

        [Fact]
        public async Task Number_AsObjectWithParameterizedCtor_RoundTrip()
        {
            var obj = new MyClassWithNumbers(YamlNumberTestData.NullableULongs.LastOrDefault(), YamlNumberTestData.Floats);

            // Serialize
            string serialized = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);

            // Deserialize
            obj = await Serializer.DeserializeWrapper<MyClassWithNumbers>(serialized, s_optionReadAndWriteFromStr);

            // Test roundtrip
            YamlTestHelper.AssertYamlEqual(serialized, await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
        }

        public class MyClassWithNumbers
        {
            public ulong? Ulong { get; }
            public List<float> ListOfFloats { get; }

            public MyClassWithNumbers(ulong? @ulong, List<float> listOfFloats)
            {
                Ulong = @ulong;
                ListOfFloats = listOfFloats;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Number_AsObjectWithParameterizedCtor_PropHasAttribute()
        {
            string yaml = """{"ListOfFloats":["1"]}""";
            // Strict handling on property overrides loose global policy.
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<MyClassWithNumbers_PropsHasAttribute>(yaml, s_optionReadFromStr));

            // Serialize
            yaml = """{"ListOfFloats":[1]}""";
            MyClassWithNumbers_PropsHasAttribute obj = await Serializer.DeserializeWrapper<MyClassWithNumbers_PropsHasAttribute>(yaml);

            // Number serialized as JSON number due to strict handling on property which overrides loose global policy.
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
        }

        public class MyClassWithNumbers_PropsHasAttribute
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public List<float> ListOfFloats { get; }

            public MyClassWithNumbers_PropsHasAttribute(List<float> listOfFloats)
            {
                ListOfFloats = listOfFloats;
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task FloatingPointConstants_Pass()
        {
            // Valid values
            await PerformFloatingPointSerialization("NaN");
            await PerformFloatingPointSerialization("Infinity");
            await PerformFloatingPointSerialization("-Infinity");

            await PerformFloatingPointSerialization("\u004EaN"); // NaN
            await PerformFloatingPointSerialization("Inf\u0069ni\u0074y"); // Infinity
            await PerformFloatingPointSerialization("\u002DInf\u0069nity"); // -Infinity

            async Task PerformFloatingPointSerialization(string testString)
            {
                string testStringAsYaml = $@"""{testString}""";
#if NET
                string testYaml = @$"{{""HalfNumber"":{testStringAsYaml},""FloatNumber"":{testStringAsYaml},""DoubleNumber"":{testStringAsYaml}}}";
#else
                string testYaml = @$"{{""FloatNumber"":{testStringAsYaml},""DoubleNumber"":{testStringAsYaml}}}";
#endif

                StructWithNumbers obj;
                switch (testString)
                {
                    case "NaN":
                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants);
#if NET
                        Assert.Equal(Half.NaN, obj.HalfNumber);
#endif
                        Assert.Equal(float.NaN, obj.FloatNumber);
                        Assert.Equal(double.NaN, obj.DoubleNumber);

                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr);
#if NET
                        Assert.Equal(Half.NaN, obj.HalfNumber);
#endif
                        Assert.Equal(float.NaN, obj.FloatNumber);
                        Assert.Equal(double.NaN, obj.DoubleNumber);
                        break;
                    case "Infinity":
                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants);
#if NET
                        Assert.Equal(Half.PositiveInfinity, obj.HalfNumber);
#endif
                        Assert.Equal(float.PositiveInfinity, obj.FloatNumber);
                        Assert.Equal(double.PositiveInfinity, obj.DoubleNumber);

                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr);
#if NET
                        Assert.Equal(Half.PositiveInfinity, obj.HalfNumber);
#endif
                        Assert.Equal(float.PositiveInfinity, obj.FloatNumber);
                        Assert.Equal(double.PositiveInfinity, obj.DoubleNumber);
                        break;
                    case "-Infinity":
                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants);
#if NET
                        Assert.Equal(Half.NegativeInfinity, obj.HalfNumber);
#endif
                        Assert.Equal(float.NegativeInfinity, obj.FloatNumber);
                        Assert.Equal(double.NegativeInfinity, obj.DoubleNumber);

                        obj = await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr);
#if NET
                        Assert.Equal(Half.NegativeInfinity, obj.HalfNumber);
#endif
                        Assert.Equal(float.NegativeInfinity, obj.FloatNumber);
                        Assert.Equal(double.NegativeInfinity, obj.DoubleNumber);
                        break;
                    default:
                        await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants));
                        return;
                }

                YamlTestHelper.AssertYamlEqual(testYaml, await Serializer.SerializeWrapper(obj, s_optionsAllowFloatConstants));
                YamlTestHelper.AssertYamlEqual(testYaml, await Serializer.SerializeWrapper(obj, s_optionWriteAsStr));
            }
        }

        [Theory]
        [InlineData("naN")]
        [InlineData("Nan")]
        [InlineData("NAN")]
        [InlineData("+Infinity")]
        [InlineData("+infinity")]
        [InlineData("infinity")]
        [InlineData("infinitY")]
        [InlineData("INFINITY")]
        [InlineData("+INFINITY")]
        [InlineData("-infinity")]
        [InlineData("-infinitY")]
        [InlineData("-INFINITY")]
        [InlineData(" NaN")]
        [InlineData("NaN ")]
        [InlineData(" Infinity")]
        [InlineData(" -Infinity")]
        [InlineData("Infinity ")]
        [InlineData("-Infinity ")]
        [InlineData("a-Infinity")]
        [InlineData("NaNa")]
        [InlineData("Infinitya")]
        [InlineData("-Infinitya")]
#pragma warning disable xUnit1025 // Theory method 'FloatingPointConstants_Fail' on test class 'NumberHandlingTests' has InlineData duplicate(s)
        [InlineData("\u006EaN")] // "naN"
        [InlineData("\u0020Inf\u0069ni\u0074y")] // " Infinity"
        [InlineData("\u002BInf\u0069nity")] // "+Infinity"
#pragma warning restore xUnit1025
        public async Task FloatingPointConstants_Fail(string testString)
        {
            string testStringAsYaml = $@"""{testString}""";
            string testYaml;
#if NET
            testYaml = @$"{{""HalfNumber"":{testStringAsYaml}}}";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr));
#endif
            testYaml = @$"{{""FloatNumber"":{testStringAsYaml}}}";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr));

            testYaml = @$"{{""DoubleNumber"":{testStringAsYaml}}}";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithNumbers>(testYaml, s_optionReadFromStr));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AllowFloatingPointConstants_WriteAsNumber_IfNotConstant()
        {
#if NET
            Half half = (Half)1;
            // Not written as "1"
            Assert.Equal("1", await Serializer.SerializeWrapper(half, s_optionsAllowFloatConstants));
#endif
            float @float = 1;
            // Not written as "1"
            Assert.Equal("1", await Serializer.SerializeWrapper(@float, s_optionsAllowFloatConstants));

            double @double = 1;
            // Not written as "1"
            Assert.Equal("1", await Serializer.SerializeWrapper(@double, s_optionsAllowFloatConstants));
        }

        public struct StructWithNumbers
        {
#if NET
            public Half HalfNumber { get; set; }
#endif
            public float FloatNumber { get; set; }
            public double DoubleNumber { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task ReadFromString_AllowFloatingPoint()
        {
            string yaml = """{"IntNumber":"1","FloatNumber":"NaN"}""";
            ClassWithNumbers obj = await Serializer.DeserializeWrapper<ClassWithNumbers>(yaml, s_optionReadFromStrAllowFloatConstants);

            Assert.Equal(1, obj.IntNumber);
            Assert.Equal(float.NaN, obj.FloatNumber);

            YamlTestHelper.AssertYamlEqual("""{"IntNumber":1,"FloatNumber":"NaN"}""", await Serializer.SerializeWrapper(obj, s_optionReadFromStrAllowFloatConstants));
        }

        [Fact]
        public async Task WriteAsString_AllowFloatingPoint()
        {
            string yaml = """{"IntNumber":"1","FloatNumber":"NaN"}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithNumbers>(yaml, s_optionWriteAsStrAllowFloatConstants));

            var obj = new ClassWithNumbers
            {
                IntNumber = 1,
                FloatNumber = float.NaN
            };

            YamlTestHelper.AssertYamlEqual(yaml, await Serializer.SerializeWrapper(obj, s_optionWriteAsStrAllowFloatConstants));
        }

        public class ClassWithNumbers
        {
            public int IntNumber { get; set; }
            public float FloatNumber { get; set; }
        }

        [Fact]
        public async Task FloatingPointConstants_IncompatibleNumber()
        {
            await AssertFloatingPointIncompatible_Fails<byte>();
            await AssertFloatingPointIncompatible_Fails<sbyte>();
            await AssertFloatingPointIncompatible_Fails<short>();
            await AssertFloatingPointIncompatible_Fails<int>();
            await AssertFloatingPointIncompatible_Fails<long>();
            await AssertFloatingPointIncompatible_Fails<ushort>();
            await AssertFloatingPointIncompatible_Fails<uint>();
            await AssertFloatingPointIncompatible_Fails<ulong>();
            await AssertFloatingPointIncompatible_Fails<decimal>();
            await AssertFloatingPointIncompatible_Fails<byte?>();
            await AssertFloatingPointIncompatible_Fails<sbyte?>();
            await AssertFloatingPointIncompatible_Fails<short?>();
            await AssertFloatingPointIncompatible_Fails<int?>();
            await AssertFloatingPointIncompatible_Fails<long?>();
            await AssertFloatingPointIncompatible_Fails<ushort?>();
            await AssertFloatingPointIncompatible_Fails<uint?>();
            await AssertFloatingPointIncompatible_Fails<ulong?>();
            await AssertFloatingPointIncompatible_Fails<decimal?>();
#if NET
            await AssertFloatingPointIncompatible_Fails<Int128>();
            await AssertFloatingPointIncompatible_Fails<UInt128>();
            await AssertFloatingPointIncompatible_Fails<Int128?>();
            await AssertFloatingPointIncompatible_Fails<UInt128?>();
#endif
        }

        private async Task AssertFloatingPointIncompatible_Fails<T>()
        {
            string[] testCases = new[]
            {
                """
                    "NaN"
                    """,
                """
                    "Infinity"
                    """,
                """
                    "-Infinity"
                    """,
            };

            foreach (string test in testCases)
            {
                await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(test, s_optionReadFromStrAllowFloatConstants));
            }
        }

        private async Task AssertUnsupportedFormatThrows<T>()
        {
            string[] testCases = new[]
            {
                "01", // leading zeroes
                "$123.46", // Currency
                "100.00 %", // Percent
                 "1234,57", // Fixed point
                 "00FF", // Hexadecimal
            };

            foreach (string test in testCases)
            {
                await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(test, s_optionReadFromStr));
            }
        }

#if NET
        [Fact]
        public async Task InvalidNumberFormatThrows()
        {
            await AssertInvalidNumberFormatThrows<Int128>("170141183460469231731687303715884105728"); // MaxValue + 1
            await AssertInvalidNumberFormatThrows<Int128>("-170141183460469231731687303715884105729"); // MaxValue - 1
            await AssertInvalidNumberFormatThrows<Int128>("3.14");
            await AssertInvalidNumberFormatThrows<UInt128>("340282366920938463463374607431768211456"); // MaxValue + 1
            await AssertInvalidNumberFormatThrows<Int128>("3.14");
            await AssertInvalidNumberFormatThrows<UInt128>("-1");
            await AssertInvalidNumberFormatThrows<Half>("65520");
            await AssertInvalidNumberFormatThrows<Half>("-65520");
        }

        private async Task AssertInvalidNumberFormatThrows<T>(string testString)
        {
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(testString));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>($@"""{testString}""", s_optionReadFromStr));
        }
#endif

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task EscapingTest()
        {
            // Cause all characters to be escaped.
            var encoderSettings = new TextEncoderSettings();
            encoderSettings.ForbidCharacters('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '+', '-', 'e', 'E');

            JavaScriptEncoder encoder = JavaScriptEncoder.Create(encoderSettings);
            var options = new YamlSerializerOptions(s_optionReadAndWriteFromStr)
            {
                Encoder = encoder
            };

            await PerformEscapingTest(YamlNumberTestData.Bytes, options);
            await PerformEscapingTest(YamlNumberTestData.SBytes, options);
            await PerformEscapingTest(YamlNumberTestData.Shorts, options);
            await PerformEscapingTest(YamlNumberTestData.Ints, options);
            await PerformEscapingTest(YamlNumberTestData.Longs, options);
            await PerformEscapingTest(YamlNumberTestData.UShorts, options);
            await PerformEscapingTest(YamlNumberTestData.UInts, options);
            await PerformEscapingTest(YamlNumberTestData.ULongs, options);
            await PerformEscapingTest(YamlNumberTestData.Floats, options);
            await PerformEscapingTest(YamlNumberTestData.Doubles, options);
            await PerformEscapingTest(YamlNumberTestData.Decimals, options);
#if NET
            await PerformEscapingTest(YamlNumberTestData.Int128s, options);
            await PerformEscapingTest(YamlNumberTestData.UInt128s, options);
            await PerformEscapingTest(YamlNumberTestData.Halfs, options);
#endif

        }

        private async Task PerformEscapingTest<T>(List<T> numbers, YamlSerializerOptions options)
        {
            // All input characters are escaped
            IEnumerable<string> numbersAsStrings = numbers.Select(num => GetNumberAsString(num));
            string input = await Serializer.SerializeWrapper(numbersAsStrings, options);
            AssertListNumbersEscaped(input);

            // Unescaping works
            List<T> deserialized = await Serializer.DeserializeWrapper<List<T>>(input, options);
            Assert.Equal(numbers.Count, deserialized.Count);
            for (int i = 0; i < numbers.Count; i++)
            {
                Assert.Equal(numbers[i], deserialized[i]);
            }

            // Every number is written as a string, and custom escaping is not honored.
            string serialized = await Serializer.SerializeWrapper(deserialized, options);
            AssertListNumbersUnescaped(serialized);
        }

        private void AssertListNumbersEscaped(string yaml)
        {
            Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml));
            reader.Read();
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndArray)
                {
                    break;
                }
                else
                {
                    Assert.Equal(YamlTokenType.String, reader.TokenType);
                    Assert.True(reader.ValueSpan.IndexOf((byte)'\\') != -1);
                }
            }
        }

        private void AssertListNumbersUnescaped(string yaml)
        {
            Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml));
            reader.Read();
            while (reader.Read())
            {
                if (reader.TokenType == YamlTokenType.EndArray)
                {
                    break;
                }
                else
                {
                    Assert.Equal(YamlTokenType.String, reader.TokenType);
                    Assert.True(reader.ValueSpan.IndexOf((byte)'\\') == -1);
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Number_RoundtripNull()
        {
            await Perform_Number_RoundTripNull_Test<byte>();
            await Perform_Number_RoundTripNull_Test<sbyte>();
            await Perform_Number_RoundTripNull_Test<short>();
            await Perform_Number_RoundTripNull_Test<int>();
            await Perform_Number_RoundTripNull_Test<long>();
            await Perform_Number_RoundTripNull_Test<ushort>();
            await Perform_Number_RoundTripNull_Test<uint>();
            await Perform_Number_RoundTripNull_Test<ulong>();
            await Perform_Number_RoundTripNull_Test<float>();
            await Perform_Number_RoundTripNull_Test<decimal>();
#if NET
            await Perform_Number_RoundTripNull_Test<Int128>();
            await Perform_Number_RoundTripNull_Test<UInt128>();
            await Perform_Number_RoundTripNull_Test<Half>();
#endif
        }

        private async Task Perform_Number_RoundTripNull_Test<T>()
        {
            string nullAsYaml = "null";
            string nullAsQuotedYaml = $@"""{nullAsYaml}""";

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(nullAsYaml, s_optionReadAndWriteFromStr));
            Assert.Equal("0", await Serializer.SerializeWrapper(default(T)));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(nullAsQuotedYaml, s_optionReadAndWriteFromStr));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NullableNumber_RoundtripNull()
        {
            await Perform_NullableNumber_RoundTripNull_Test<byte?>();
            await Perform_NullableNumber_RoundTripNull_Test<sbyte?>();
            await Perform_NullableNumber_RoundTripNull_Test<short?>();
            await Perform_NullableNumber_RoundTripNull_Test<int?>();
            await Perform_NullableNumber_RoundTripNull_Test<long?>();
            await Perform_NullableNumber_RoundTripNull_Test<ushort?>();
            await Perform_NullableNumber_RoundTripNull_Test<uint?>();
            await Perform_NullableNumber_RoundTripNull_Test<ulong?>();
            await Perform_NullableNumber_RoundTripNull_Test<float?>();
            await Perform_NullableNumber_RoundTripNull_Test<decimal?>();
#if NET
            await Perform_NullableNumber_RoundTripNull_Test<Int128?>();
            await Perform_NullableNumber_RoundTripNull_Test<UInt128?>();
            await Perform_NullableNumber_RoundTripNull_Test<Half?>();
#endif
        }

        private async Task Perform_NullableNumber_RoundTripNull_Test<T>()
        {
            string nullAsYaml = "null";
            string nullAsQuotedYaml = $@"""{nullAsYaml}""";

            Assert.Null(await Serializer.DeserializeWrapper<T>(nullAsYaml, s_optionReadAndWriteFromStr));
            Assert.Equal(nullAsYaml, await Serializer.SerializeWrapper(default(T)));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<T>(nullAsQuotedYaml, s_optionReadAndWriteFromStr));
        }

        [Fact]
        public async Task Disallow_ArbritaryStrings_On_AllowFloatingPointConstants()
        {
            string yaml = """
                "12345"
                """;

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<byte>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<sbyte>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<short>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<int>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<long>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ushort>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<uint>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ulong>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<float>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<double>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<decimal>(yaml, s_optionsAllowFloatConstants));
#if NET
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Int128>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<UInt128>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Half>(yaml, s_optionsAllowFloatConstants));
#endif
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<byte?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<sbyte?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<short?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<int?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<long?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ushort?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<uint?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ulong?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<float?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<double?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<decimal?>(yaml, s_optionsAllowFloatConstants));
#if NET
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Int128?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<UInt128?>(yaml, s_optionsAllowFloatConstants));
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Half?>(yaml, s_optionsAllowFloatConstants));
#endif
        }

        [Fact]
        public async Task Attributes_OnMembers_Work()
        {
            // Bad JSON because Int should not be string.
            string intIsString = """{"Float":"1234.5","Int":"12345"}""";

            // Good JSON because Float can be string.
            string floatIsString = """{"Float":"1234.5","Int":12345}""";

            // Good JSON because Float can be number.
            string floatIsNumber = """{"Float":1234.5,"Int":12345}""";

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWith_Attribute_OnNumber>(intIsString));

            ClassWith_Attribute_OnNumber obj = await Serializer.DeserializeWrapper<ClassWith_Attribute_OnNumber>(floatIsString);
            Assert.Equal(1234.5, obj.Float);
            Assert.Equal(12345, obj.Int);

            obj = await Serializer.DeserializeWrapper<ClassWith_Attribute_OnNumber>(floatIsNumber);
            Assert.Equal(1234.5, obj.Float);
            Assert.Equal(12345, obj.Int);

            // Per options, float should be written as string.
            YamlTestHelper.AssertYamlEqual(floatIsString, await Serializer.SerializeWrapper(obj));
        }

        public class ClassWith_Attribute_OnNumber
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public float Float { get; set; }

            public int Int { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Attribute_OnRootType_Works()
        {
            // Not allowed
            string floatIsString = """{"Float":"1234","Int":123}""";

            // Allowed
            string floatIsNan = """{"Float":"NaN","Int":123}""";

            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<Type_AllowFloatConstants>(floatIsString));

            Type_AllowFloatConstants obj = await Serializer.DeserializeWrapper<Type_AllowFloatConstants>(floatIsNan);
            Assert.Equal(float.NaN, obj.Float);
            Assert.Equal(123, obj.Int);

            YamlTestHelper.AssertYamlEqual(floatIsNan, await Serializer.SerializeWrapper(obj));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowNamedFloatingPointLiterals)]
        public class Type_AllowFloatConstants
        {
            public float Float { get; set; }

            public int Int { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AttributeOnType_WinsOver_GlobalOption()
        {
            // Global options strict, type options loose
            string yaml = """{"Float":"12345"}""";
            var obj1 = await Serializer.DeserializeWrapper<ClassWith_LooseAttribute>(yaml);

            Assert.Equal("""{"Float":"12345"}""", await Serializer.SerializeWrapper(obj1));

            // Global options loose, type options strict
            yaml = """{"Float":"12345"}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWith_StrictAttribute>(yaml, s_optionReadAndWriteFromStr));

            var obj2 = new ClassWith_StrictAttribute() { Float = 12345 };
            Assert.Equal("""{"Float":12345}""", await Serializer.SerializeWrapper(obj2, s_optionReadAndWriteFromStr));
        }

        [YamlNumberHandling(YamlNumberHandling.Strict)]
        public class ClassWith_StrictAttribute
        {
            public float Float { get; set; }
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public class ClassWith_LooseAttribute
        {
            public float Float { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AttributeOnMember_WinsOver_AttributeOnType()
        {
            string yaml = """{"Double":"NaN"}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWith_Attribute_On_TypeAndMember>(yaml));

            var obj = new ClassWith_Attribute_On_TypeAndMember { Double = float.NaN };
            await Assert.ThrowsAsync<ArgumentException>(async () => await Serializer.SerializeWrapper(obj));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowNamedFloatingPointLiterals)]
        public class ClassWith_Attribute_On_TypeAndMember
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public double Double { get; set; }
        }

        [Fact]
        public async Task Attribute_OnNestedType_Works()
        {
            string yamlWithShortProperty = """{"Short":"1"}""";
            ClassWith_ReadAsStringAttribute obj = await Serializer.DeserializeWrapper<ClassWith_ReadAsStringAttribute>(yamlWithShortProperty);
            Assert.Equal(1, obj.Short);

            string yamlWithMyObjectProperty = """{"MyObject":{"Float":"1"}}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWith_ReadAsStringAttribute>(yamlWithMyObjectProperty));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString)]
        public class ClassWith_ReadAsStringAttribute
        {
            public short Short { get; set; }

            public ClassWith_StrictAttribute MyObject { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task MemberAttributeAppliesToCollection_SimpleElements()
        {
            await RunTest<int[]>();
            await RunTest<ConcurrentQueue<int>>();
            await RunTest<GenericICollectionWrapper<int>>();
            await RunTest<IEnumerable<int>>();
            await RunTest<Collection<int>>();
            await RunTest<ImmutableList<int>>();
            await RunTest<HashSet<int>>();
            await RunTest<List<int>>();
            await RunTest<IList<int>>();
            await RunTest<IList>();
            await RunTest<Queue<int>>();

            async Task RunTest<T>()
            {
                string yaml = """{"MyList":["1","2"]}""";
                ClassWithSimpleCollectionProperty<T> obj = await Serializer.DeserializeWrapper<ClassWithSimpleCollectionProperty<T>>(yaml);
                Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
            }
        }

        public class ClassWithSimpleCollectionProperty<T>
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public T MyList { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NestedCollectionElementTypeHandling_Overrides_GlobalOption()
        {
            // Strict policy on the collection element type overrides read-as-string on the collection property
            string yaml = """{"MyList":[{"Float":"1"}]}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithComplexListProperty>(yaml, s_optionReadAndWriteFromStr));

            // Strict policy on the collection element type overrides write-as-string on the collection property
            var obj = new ClassWithComplexListProperty
            {
                MyList = new List<ClassWith_StrictAttribute> { new ClassWith_StrictAttribute { Float = 1 } }
            };
            Assert.Equal("""{"MyList":[{"Float":1}]}""", await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr));
        }

        public class ClassWithComplexListProperty
        {
            public List<ClassWith_StrictAttribute> MyList { get; set; }
        }

        [Fact]
        public async Task NumberHandlingAttribute_NotAllowedOn_CollectionOfNonNumbers()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWith_AttributeOnComplexListProperty>(""));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ClassWith_AttributeOnComplexListProperty()));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWith_AttributeOnComplexDictionaryProperty>(""));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ClassWith_AttributeOnComplexDictionaryProperty()));
        }

        public class ClassWith_AttributeOnComplexListProperty
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public List<ClassWith_StrictAttribute> MyList { get; set; }
        }

        public class ClassWith_AttributeOnComplexDictionaryProperty
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString)]
            public Dictionary<string, ClassWith_StrictAttribute> MyDictionary { get; set; }
        }

        [Fact]
        public async Task MemberAttributeAppliesToDictionary_SimpleElements()
        {
            string yaml = """{"First":"1","Second":"2"}""";
            ClassWithSimpleDictionaryProperty obj = await Serializer.DeserializeWrapper<ClassWithSimpleDictionaryProperty>(yaml);
        }

        public class ClassWithSimpleDictionaryProperty
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public Dictionary<string, int> MyDictionary { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task NestedDictionaryElementTypeHandling_Overrides_GlobalOption()
        {
            // Strict policy on the dictionary element type overrides read-as-string on the collection property.
            string yaml = """{"MyDictionary":{"Key":{"Float":"1"}}}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithComplexDictionaryProperty>(yaml, s_optionReadFromStr));

            // Strict policy on the collection element type overrides write-as-string on the collection property
            var obj = new ClassWithComplexDictionaryProperty
            {
                MyDictionary = new Dictionary<string, ClassWith_StrictAttribute> { ["Key"] = new ClassWith_StrictAttribute { Float = 1 } }
            };
            Assert.Equal("""{"MyDictionary":{"Key":{"Float":1}}}""", await Serializer.SerializeWrapper(obj, s_optionReadFromStr));
        }

        public class ClassWithComplexDictionaryProperty
        {
            public Dictionary<string, ClassWith_StrictAttribute> MyDictionary { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task TypeAttributeAppliesTo_CustomCollectionElements()
        {
            string yaml = """["1"]""";
            MyCustomList obj = await Serializer.DeserializeWrapper<MyCustomList>(yaml);
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public class MyCustomList : List<int> { }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task TypeAttributeAppliesTo_CustomCollectionElements_HonoredWhenProperty()
        {
            string yaml = """{"List":["1"]}""";
            ClassWithCustomList obj = await Serializer.DeserializeWrapper<ClassWithCustomList>(yaml);
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
        }

        public class ClassWithCustomList
        {
            public MyCustomList List { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task TypeAttributeAppliesTo_CustomDictionaryElements()
        {
            string yaml = """{"Key":"1"}""";
            MyCustomDictionary obj = await Serializer.DeserializeWrapper<MyCustomDictionary>(yaml);
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public class MyCustomDictionary : Dictionary<string, int> { }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task TypeAttributeAppliesTo_CustomDictionaryElements_HonoredWhenProperty()
        {
            string yaml = """{"Dictionary":{"Key":"1"}}""";
            ClassWithCustomDictionary obj = await Serializer.DeserializeWrapper<ClassWithCustomDictionary>(yaml);
            Assert.Equal(yaml, await Serializer.SerializeWrapper(obj));
        }

        public class ClassWithCustomDictionary
        {
            public MyCustomDictionary Dictionary { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Attribute_OnType_NotRecursive()
        {
            // Recursive behavior, where number handling setting on a property is applied to subsequent
            // properties in its type closure, would allow a string number. This is not supported.
            string yaml = """{"NestedClass":{"MyInt":"1"}}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<AttributeAppliedToFirstLevelProp>(yaml));

            var obj = new AttributeAppliedToFirstLevelProp
            {
                NestedClass = new NonNumberType { MyInt = 1 }
            };
            Assert.Equal("""{"NestedClass":{"MyInt":1}}""", await Serializer.SerializeWrapper(obj));
        }

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public class AttributeAppliedToFirstLevelProp
        {
            public NonNumberType NestedClass { get; set; }
        }

        public class NonNumberType
        {
            public int MyInt { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task HandlingOnMemberOverridesHandlingOnType_Enumerable()
        {
            string yaml = """{"List":["1"]}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<MyCustomListWrapper>(yaml));

            var obj = new MyCustomListWrapper
            {
                List = new MyCustomList { 1 }
            };
            Assert.Equal("""{"List":[1]}""", await Serializer.SerializeWrapper(obj));
        }

        public class MyCustomListWrapper
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public MyCustomList List { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task HandlingOnMemberOverridesHandlingOnType_Dictionary()
        {
            string yaml = """{"Dictionary":{"Key":"1"}}""";
            await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<MyCustomDictionaryWrapper>(yaml));

            var obj1 = new MyCustomDictionaryWrapper
            {
                Dictionary = new MyCustomDictionary { ["Key"] = 1 }
            };
            Assert.Equal("""{"Dictionary":{"Key":1}}""", await Serializer.SerializeWrapper(obj1));
        }

        public class MyCustomDictionaryWrapper
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public MyCustomDictionary Dictionary { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task Attribute_Allowed_On_NonNumber_NonCollection_Property()
        {
            const string Yaml = """{"MyProp":{"MyInt":1}}""";

            ClassWith_NumberHandlingOn_ObjectProperty obj = await Serializer.DeserializeWrapper<ClassWith_NumberHandlingOn_ObjectProperty>(Yaml);
            Assert.Equal(1, obj.MyProp.MyInt);

            string yaml = await Serializer.SerializeWrapper(obj);
            Assert.Equal(Yaml, yaml);
        }

        public class ClassWith_NumberHandlingOn_ObjectProperty
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            public NonNumberType MyProp { get; set; }
        }

        [Fact]
        public async Task Attribute_Allowed_On_Property_WithCustomConverter()
        {
            string yaml = """{"Prop":1}""";

            // Converter returns 25 regardless of input.
            var obj = await Serializer.DeserializeWrapper<ClassWith_NumberHandlingOn_Property_WithCustomConverter>(yaml);
            Assert.Equal(25, obj.Prop);

            // Converter throws this exception regardless of input.
            NotImplementedException ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(obj));
            Assert.Equal("Converter was called", ex.Message);
        }

        public class ClassWith_NumberHandlingOn_Property_WithCustomConverter
        {
            [YamlNumberHandling(YamlNumberHandling.Strict)]
            [YamlConverter(typeof(ConverterForInt32))]
            public int Prop { get; set; }
        }

        [Fact]
        public async Task Attribute_Allowed_On_Type_WithCustomConverter()
        {
            string yaml = "{}";
            NotImplementedException ex;

            // Assert regular Read/Write methods on custom converter are called.
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.DeserializeWrapper<ClassWith_NumberHandlingOn_Type_WithCustomConverter>(yaml));
            Assert.Equal("Converter was called", ex.Message);

            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(new ClassWith_NumberHandlingOn_Type_WithCustomConverter()));
            Assert.Equal("Converter was called", ex.Message);
        }

        [YamlNumberHandling(YamlNumberHandling.Strict)]
        [YamlConverter(typeof(ConverterForMyType))]
        public class ClassWith_NumberHandlingOn_Type_WithCustomConverter
        {
        }

        public class ConverterForMyType : YamlConverter<ClassWith_NumberHandlingOn_Type_WithCustomConverter>
        {
            public override ClassWith_NumberHandlingOn_Type_WithCustomConverter Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException("Converter was called");
            }

            public override void Write(Utf8YamlWriter writer, ClassWith_NumberHandlingOn_Type_WithCustomConverter value, YamlSerializerOptions options)
            {
                throw new NotImplementedException("Converter was called");
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CustomConverterOverridesBuiltInLogic()
        {
            var options = new YamlSerializerOptions(s_optionReadAndWriteFromStr)
            {
                Converters = { new ConverterForInt32(), new ConverterForFloat() }
            };

            string yaml = """
                "32"
                """;

            // Converter returns 25 regardless of input.
            Assert.Equal(25, await Serializer.DeserializeWrapper<int>(yaml, options));

            // Converter throws this exception regardless of input.
            NotImplementedException ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(4, options));
            Assert.Equal("Converter was called", ex.Message);

            yaml = """
                "NaN"
                """;

            // Converter returns 25 if NaN.
            Assert.Equal(25, await Serializer.DeserializeWrapper<float?>(yaml, options));

            // Converter writes 25 if NaN.
            Assert.Equal("25", await Serializer.SerializeWrapper((float?)float.NaN, options));
        }

        public class ConverterForFloat : YamlConverter<float?>
        {
            public override float? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType == YamlTokenType.String && reader.GetString() == "NaN")
                {
                    return 25;
                }

                throw new NotSupportedException();
            }

            public override void Write(Utf8YamlWriter writer, float? value, YamlSerializerOptions options)
            {
                if (float.IsNaN(value.Value))
                {
                    writer.WriteNumberValue(25);
                    return;
                }

                throw new NotSupportedException();
            }
        }

        [Fact]
        public static void YamlNumberHandling_ArgOutOfRangeFail()
        {
            // Global options
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new YamlSerializerOptions { NumberHandling = (YamlNumberHandling)(-1) });
            Assert.Contains("value", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new YamlSerializerOptions { NumberHandling = (YamlNumberHandling)(8) });

            ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new YamlNumberHandlingAttribute((YamlNumberHandling)(-1)));
            Assert.Contains("handling", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new YamlNumberHandlingAttribute((YamlNumberHandling)(8)));
        }

        [Fact]
        public async Task InternalCollectionConverter_CustomNumberConverter_GlobalOption()
        {
            NotImplementedException ex;

            var list = new List<int> { 1 };
            var options = new YamlSerializerOptions(s_optionReadAndWriteFromStr)
            {
                Converters = { new ConverterForInt32() }
            };

            // Assert converter methods are called and not Read/WriteWithNumberHandling (which would throw InvalidOperationException).
            // Converter returns 25 regardless of input.
            Assert.Equal(25, (await Serializer.DeserializeWrapper<List<int>>("""["1"]""", options))[0]);
            // Converter throws this exception regardless of input.
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(list, options));
            Assert.Equal("Converter was called", ex.Message);

            var list2 = new List<int?> { 1 };
            Assert.Equal(25, (await Serializer.DeserializeWrapper<List<int?>>("""["1"]""", options))[0]);
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(list2, options));
            Assert.Equal("Converter was called", ex.Message);

            // Okay to set number handling for number collection property when number is handled with custom converter;
            // converter Read/Write methods called.
            ClassWithListPropAndAttribute obj1 = await Serializer.DeserializeWrapper<ClassWithListPropAndAttribute>("""{"Prop":["1"]}""", options);
            Assert.Equal(25, obj1.Prop[0]);
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(obj1, options));
            Assert.Equal("Converter was called", ex.Message);

            ClassWithDictPropAndAttribute obj2 = await Serializer.DeserializeWrapper<ClassWithDictPropAndAttribute>("""{"Prop":{"1":"1"}}""", options);
            Assert.Equal(25, obj2.Prop[1]);
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(obj2, options));
            Assert.Equal("Converter was called", ex.Message);
        }

        public class ClassWithListPropAndAttribute
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public List<int> Prop { get; set; }
        }

        public class ClassWithDictPropAndAttribute
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            public Dictionary<int, int?> Prop { get; set; }
        }

        [Fact]
        public async Task InternalCollectionConverter_CustomNumberConverter_OnProperty()
        {
            // Invalid to set number handling for number collection property when number is handled with custom converter.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithListPropAndAttribute_ConverterOnProp>(""));
            Assert.Contains(nameof(ClassWithListPropAndAttribute_ConverterOnProp), ex.Message);
            Assert.Contains("IntProp", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ClassWithListPropAndAttribute_ConverterOnProp()));
            Assert.Contains(nameof(ClassWithListPropAndAttribute_ConverterOnProp), ex.Message);
            Assert.Contains("IntProp", ex.Message);

#if !BUILDING_SOURCE_GENERATOR_TESTS
            // Source-gen isn't currently validating that the converter on the test prop
            // is invalid so YamlException is being thrown instead due to invalid JSON.
            // [ActiveIssue("https://github.com/dotnet/runtime/issues/73714"]
            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithDictPropAndAttribute_ConverterOnProp>(""));
            Assert.Contains(nameof(ClassWithDictPropAndAttribute_ConverterOnProp), ex.Message);
            Assert.Contains("IntProp", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ClassWithDictPropAndAttribute_ConverterOnProp()));
            Assert.Contains(nameof(ClassWithDictPropAndAttribute_ConverterOnProp), ex.Message);
            Assert.Contains("IntProp", ex.Message);
#endif
        }

        public class ClassWithListPropAndAttribute_ConverterOnProp
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            [YamlConverter(typeof(ListOfIntConverter))]
            public List<int> IntProp { get; set; }
        }

        public class ClassWithDictPropAndAttribute_ConverterOnProp
        {
            [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
            [YamlConverter(typeof(ClassWithDictPropAndAttribute_ConverterOnProp))]
            public Dictionary<int, int?> IntProp { get; set; }
        }

        public class ListOfIntConverter : YamlConverter<List<int>>
        {
            public override List<int> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
            public override void Write(Utf8YamlWriter writer, List<int> value, YamlSerializerOptions options) => throw new NotImplementedException();
        }

        [Fact]
        public async Task InternalCollectionConverter_CustomNullableNumberConverter()
        {
            NotImplementedException ex;

            var dict = new Dictionary<int, int?> { [1] = 1 };
            var options = new YamlSerializerOptions(s_optionReadAndWriteFromStr)
            {
                Converters = { new ConverterForNullableInt32() }
            };

            // Assert converter methods are called and not Read/WriteWithNumberHandling (which would throw InvalidOperationException).
            // Converter returns 25 regardless of input.
            Assert.Equal(25, (await Serializer.DeserializeWrapper<Dictionary<int, int?>>("""{"1":"1"}""", options))[1]);
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(dict, options));
            Assert.Equal("Converter was called", ex.Message);

            var obj = await Serializer.DeserializeWrapper<ClassWithDictPropAndAttribute>("""{"Prop":{"1":"1"}}""", options);
            Assert.Equal(25, obj.Prop[1]);
            ex = await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(obj, options));
            await Assert.ThrowsAsync<NotImplementedException>(async () => await Serializer.SerializeWrapper(dict, options));
            Assert.Equal("Converter was called", ex.Message);
        }

        public class ConverterForNullableInt32 : YamlConverter<int?>
        {
            public override int? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                return 25;
            }

            public override void Write(Utf8YamlWriter writer, int? value, YamlSerializerOptions options)
            {
                throw new NotImplementedException("Converter was called");
            }
        }

        /// <summary>
        /// Example of a custom converter that uses the options to determine behavior.
        /// </summary>
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task AdaptableCustomConverter()
        {
            // Baseline without custom converter
            PlainClassWithList obj = new() { Prop = new List<int>() { 1 } };
            string yaml = await Serializer.SerializeWrapper(obj, s_optionReadAndWriteFromStr);
            Assert.Equal("{\"Prop\":[\"1\"]}", yaml);

            obj = await Serializer.DeserializeWrapper<PlainClassWithList>(yaml, s_optionReadAndWriteFromStr);
            Assert.Equal(1, obj.Prop[0]);

            // First with numbers
            YamlSerializerOptions options = new()
            {
                Converters = { new AdaptableInt32Converter() }
            };

            obj = new PlainClassWithList() { Prop = new List<int>() { 1 } };
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"Prop\":[101]}", yaml);

            obj = await Serializer.DeserializeWrapper<PlainClassWithList>(yaml, options);
            Assert.Equal(1, obj.Prop[0]);

            // Then with strings
            options = new YamlSerializerOptions()
            {
                NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString,
                Converters = { new AdaptableInt32Converter() }
            };

            obj = new PlainClassWithList() { Prop = new List<int>() { 1 } };
            yaml = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("{\"Prop\":[\"101\"]}", yaml);

            obj = await Serializer.DeserializeWrapper<PlainClassWithList>(yaml, options);
            Assert.Equal(1, obj.Prop[0]);
        }

        public class PlainClassWithList
        {
            public List<int> Prop { get; set; }
        }

        public class AdaptableInt32Converter : YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if ((YamlNumberHandling.AllowReadingFromString & options.NumberHandling) != 0)
                {
                    // Assume it's a string; don't use TryParse().
                    return int.Parse(reader.GetString(), CultureInfo.InvariantCulture) - 100;
                }
                else
                {
                    return reader.GetInt32() - 100;
                }
            }

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                if ((YamlNumberHandling.WriteAsString & options.NumberHandling) != 0)
                {
                    writer.WriteStringValue((value + 100).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteNumberValue(value + 100);
                }
            }
        }

        [Theory]
        // -2 for Quotes
        [InlineData(32 - 2)] // Common ArrayPool buffer size for numbers
        [InlineData(256 - 2)] // Common stackalloc constant
        [InlineData(512 - 2)] // Less common stackalloc constant
        [InlineData(700)] // Random large number
        public async Task YamlExceptionForVariousSizedValues(int size)
        {
            await DeserializeWithInvalidValue<byte>(size);
            await DeserializeWithInvalidValue<sbyte>(size);
            await DeserializeWithInvalidValue<short>(size);
            await DeserializeWithInvalidValue<int>(size);
            await DeserializeWithInvalidValue<long>(size);
            await DeserializeWithInvalidValue<ushort>(size);
            await DeserializeWithInvalidValue<uint>(size);
            await DeserializeWithInvalidValue<ulong>(size);
            await DeserializeWithInvalidValue<float>(size);
            await DeserializeWithInvalidValue<decimal>(size);
            await DeserializeWithInvalidValue<byte?>(size);
            await DeserializeWithInvalidValue<sbyte?>(size);
            await DeserializeWithInvalidValue<short?>(size);
            await DeserializeWithInvalidValue<int?>(size);
            await DeserializeWithInvalidValue<long?>(size);
            await DeserializeWithInvalidValue<ushort?>(size);
            await DeserializeWithInvalidValue<uint?>(size);
            await DeserializeWithInvalidValue<ulong?>(size);
            await DeserializeWithInvalidValue<float?>(size);
            await DeserializeWithInvalidValue<decimal?>(size);
#if NET
            await DeserializeWithInvalidValue<Int128>(size);
            await DeserializeWithInvalidValue<UInt128>(size);
            await DeserializeWithInvalidValue<Half>(size);
            await DeserializeWithInvalidValue<Int128?>(size);
            await DeserializeWithInvalidValue<UInt128?>(size);
            await DeserializeWithInvalidValue<Half?>(size);
#endif
        }

        private async Task DeserializeWithInvalidValue<T>(int size)
        {
            await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<T>(
                    $"\"{string.Concat(Enumerable.Repeat("\\uFFFF", size / 6))}\"",
                    s_optionReadFromStr));

            await Assert.ThrowsAsync<YamlException>(
                () => Serializer.DeserializeWrapper<T>(
                    $"\"{string.Concat(Enumerable.Repeat("a", size))}\"",
                    s_optionReadFromStr));
        }

#if !BUILDING_SOURCE_GENERATOR_TESTS
        [Fact]
        public void GetConverter_Int32_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // GetConverter requires reflection and is not supported in source generation scenarios
                return;
            }

            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<int>)options.GetConverter(typeof(int));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); // StartObject
            reader.Read(); // PropertyName
            reader.Read(); // String with number
            var value = converter.Read(ref reader, typeof(int), options);
            Assert.Equal(123, value);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_Int32_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // GetConverter requires reflection and is not supported in source generation scenarios
                return;
            }

            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<int>)options.GetConverter(typeof(int));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            converter.Write(writer, 123, options);
            writer.WriteEndObject();
            writer.Flush();
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("{\"x\":\"123\"}", yaml);
        }

        [Fact]
        public void GetConverter_Double_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // GetConverter requires reflection and is not supported in source generation scenarios
                return;
            }

            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<double>)options.GetConverter(typeof(double));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123.45\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); // StartObject
            reader.Read(); // PropertyName
            reader.Read(); // String with number
            var value = converter.Read(ref reader, typeof(double), options);
            Assert.Equal(123.45, value);
        }

        [Fact]
        public void GetConverter_Double_RespectsNumberHandling_AllowNamedFloatingPointLiterals()
        {
            if (Serializer.IsSourceGeneratedSerializer)
            {
                // GetConverter requires reflection and is not supported in source generation scenarios
                return;
            }

            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals };
            var converter = (YamlConverter<double>)options.GetConverter(typeof(double));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"NaN\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); // StartObject
            reader.Read(); // PropertyName
            reader.Read(); // String with constant
            var value = converter.Read(ref reader, typeof(double), options);
            Assert.True(double.IsNaN(value));
        }

        // Tests for all integer types
        [Fact]
        public void GetConverter_Byte_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<byte>)options.GetConverter(typeof(byte));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((byte)123, converter.Read(ref reader, typeof(byte), options));
        }

        [Fact]
        public void GetConverter_SByte_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<sbyte>)options.GetConverter(typeof(sbyte));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((sbyte)123, converter.Read(ref reader, typeof(sbyte), options));
        }

        [Fact]
        public void GetConverter_Int16_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<short>)options.GetConverter(typeof(short));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((short)123, converter.Read(ref reader, typeof(short), options));
        }

        [Fact]
        public void GetConverter_Int64_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<long>)options.GetConverter(typeof(long));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal(123L, converter.Read(ref reader, typeof(long), options));
        }

        [Fact]
        public void GetConverter_UInt16_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<ushort>)options.GetConverter(typeof(ushort));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((ushort)123, converter.Read(ref reader, typeof(ushort), options));
        }

        [Fact]
        public void GetConverter_UInt32_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<uint>)options.GetConverter(typeof(uint));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal(123U, converter.Read(ref reader, typeof(uint), options));
        }

        [Fact]
        public void GetConverter_UInt64_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<ulong>)options.GetConverter(typeof(ulong));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal(123UL, converter.Read(ref reader, typeof(ulong), options));
        }

        [Fact]
        public void GetConverter_Single_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<float>)options.GetConverter(typeof(float));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123.5\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal(123.5f, converter.Read(ref reader, typeof(float), options));
        }

        [Fact]
        public void GetConverter_Decimal_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<decimal>)options.GetConverter(typeof(decimal));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123.45\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal(123.45m, converter.Read(ref reader, typeof(decimal), options));
        }

        // Write tests for key types
        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_Byte_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<byte>)options.GetConverter(typeof(byte));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject(); writer.WritePropertyName("x");
            converter.Write(writer, (byte)123, options);
            writer.WriteEndObject(); writer.Flush();
            Assert.Equal("{\"x\":\"123\"}", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_Int64_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<long>)options.GetConverter(typeof(long));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject(); writer.WritePropertyName("x");
            converter.Write(writer, 123L, options);
            writer.WriteEndObject(); writer.Flush();
            Assert.Equal("{\"x\":\"123\"}", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_UInt64_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<ulong>)options.GetConverter(typeof(ulong));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject(); writer.WritePropertyName("x");
            converter.Write(writer, 123UL, options);
            writer.WriteEndObject(); writer.Flush();
            Assert.Equal("{\"x\":\"123\"}", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_Single_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<float>)options.GetConverter(typeof(float));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject(); writer.WritePropertyName("x");
            converter.Write(writer, 123.5f, options);
            writer.WriteEndObject(); writer.Flush();
            Assert.Equal("{\"x\":\"123.5\"}", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetConverter_Decimal_RespectsNumberHandling_WriteAsString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.WriteAsString };
            var converter = (YamlConverter<decimal>)options.GetConverter(typeof(decimal));
            using var stream = new MemoryStream();
            using var writer = new Utf8YamlWriter(stream);
            writer.WriteStartObject(); writer.WritePropertyName("x");
            converter.Write(writer, 123.45m, options);
            writer.WriteEndObject(); writer.Flush();
            Assert.Equal("{\"x\":\"123.45\"}", Encoding.UTF8.GetString(stream.ToArray()));
        }

#if NET
        [Fact]
        public void GetConverter_Int128_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<Int128>)options.GetConverter(typeof(Int128));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((Int128)123, converter.Read(ref reader, typeof(Int128), options));
        }

        [Fact]
        public void GetConverter_UInt128_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<UInt128>)options.GetConverter(typeof(UInt128));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"123\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((UInt128)123, converter.Read(ref reader, typeof(UInt128), options));
        }

        [Fact]
        public void GetConverter_Half_RespectsNumberHandling_AllowReadingFromString()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var converter = (YamlConverter<Half>)options.GetConverter(typeof(Half));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"1.5\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.Equal((Half)1.5, converter.Read(ref reader, typeof(Half), options));
        }

        [Fact]
        public void GetConverter_Half_RespectsNumberHandling_AllowNamedFloatingPointLiterals()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals };
            var converter = (YamlConverter<Half>)options.GetConverter(typeof(Half));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"NaN\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.True(Half.IsNaN(converter.Read(ref reader, typeof(Half), options)));
        }
#endif

        [Fact]
        public void GetConverter_Single_RespectsNumberHandling_AllowNamedFloatingPointLiterals()
        {
            if (Serializer.IsSourceGeneratedSerializer) return;
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals };
            var converter = (YamlConverter<float>)options.GetConverter(typeof(float));
            ReadOnlySpan<byte> yamlReadOnlySpan = "{\"x\": \"NaN\"}"u8;
            var reader = new Utf8YamlReader(yamlReadOnlySpan);
            reader.Read(); reader.Read(); reader.Read();
            Assert.True(float.IsNaN(converter.Read(ref reader, typeof(float), options)));
        }
#endif
    }
}
