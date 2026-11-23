// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Tests.SourceGenRegressionTests.Net60;
using Xunit;
using HighLowTemps = System.Text.Yaml.Tests.SourceGenRegressionTests.Net60.HighLowTemps;
using WeatherForecastWithPOCOs = System.Text.Yaml.Tests.SourceGenRegressionTests.Net60.WeatherForecastWithPOCOs;

namespace System.Text.Yaml.Tests.SourceGenRegressionTests
{
    public static class Net60RegressionTests
    {
        [Theory]
        [MemberData(nameof(GetSupportedTypeRoundtripData))]
        public static void SupportedTypeRoundtrip<T>(YamlTypeInfo<T> yamlTypeInfo, T value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value, yamlTypeInfo);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            T deserializedValue = YamlSerializer.Deserialize(yaml, yamlTypeInfo);
            yaml = YamlSerializer.Serialize(deserializedValue, yamlTypeInfo);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);
        }

        public static IEnumerable<object[]> GetSupportedTypeRoundtripData()
        {
            var ctx = Net60GeneratedContext.Default;
            yield return Wrap(ctx.Int32, 42, "42");
            yield return Wrap(ctx.DateTimeOffset, DateTimeOffset.MinValue, "\"0001-01-01T00:00:00+00:00\"");
            yield return Wrap(ctx.String, "I am a string", "\"I am a string\"");
            yield return Wrap(ctx.HighLowTemps, new HighLowTemps { Low = 0, High = 5 }, """{"Low":0,"High":5}""");
            yield return Wrap(ctx.ListDateTimeOffset, new List<DateTimeOffset> { DateTimeOffset.MinValue }, "[\"0001-01-01T00:00:00+00:00\"]");
            yield return Wrap(ctx.ClassWithCustomConverter, new ClassWithCustomConverter { Value = 41 }, "42");
            yield return Wrap(ctx.WeatherForecastWithPOCOs, new WeatherForecastWithPOCOs
            {
                Date = DateTimeOffset.MinValue,
                TemperatureCelsius = 10,
                Summary = "I am a string",
                DatesAvailable = new List<DateTimeOffset> { DateTimeOffset.MinValue },
                TemperatureRanges = new Dictionary<string, HighLowTemps>
                {
                    ["key"] = new HighLowTemps { Low = 0, High = 5}
                },
                SummaryWords = new[] { "word1", "word2" },
            },
            """
            {
                "Date" : "0001-01-01T00:00:00+00:00",
                "TemperatureCelsius" : 10,
                "Summary" : "I am a string",
                "DatesAvailable" : [ "0001-01-01T00:00:00+00:00" ],
                "TemperatureRanges" :
                {
                    "key" : { "Low" : 0, "High" : 5 }
                },
                "SummaryWords" : [ "word1", "word2" ]
            }
            """);

            static object[] Wrap<T>(YamlTypeInfo<T> yamlTypeInfo, T value, string expectedYaml) => new object[] { yamlTypeInfo, value, expectedYaml };
        }

        [Theory]
        [MemberData(nameof(GetSupportedTypeRoundtripData_OptionsBased))]
        public static void SupportedTypeRoundtrip_OptionsBased<T>(T value, string expectedYaml)
        {
            string yaml = YamlSerializer.Serialize(value, Net60GeneratedContext.Default.Options);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);

            T deserializedValue = YamlSerializer.Deserialize<T>(yaml, Net60GeneratedContext.Default.Options);
            yaml = YamlSerializer.Serialize(deserializedValue, Net60GeneratedContext.Default.Options);
            YamlTestHelper.AssertYamlEqual(expectedYaml, yaml);
        }

        [Fact]
        public static void UnsupportedType_ThrowsInvalidOperationException()
        {
            DateTime value = DateTime.MinValue;
            string yaml = YamlSerializer.Serialize(value);

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, value.GetType(), Net60GeneratedContext.Default));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize(yaml, value.GetType(), Net60GeneratedContext.Default));
        }

        public static IEnumerable<object[]> GetSupportedTypeRoundtripData_OptionsBased()
        {
            yield return Wrap(new ClassWithCustomConverter { Value = 41 }, "42");
            yield return Wrap(new WeatherForecastWithPOCOs
            {
                Date = DateTimeOffset.MinValue,
                TemperatureCelsius = 10,
                Summary = "I am a string",
                DatesAvailable = new List<DateTimeOffset> { DateTimeOffset.MinValue },
                TemperatureRanges = new Dictionary<string, HighLowTemps>
                {
                    ["key"] = new HighLowTemps { Low = 0, High = 5 }
                },
                SummaryWords = new[] { "word1", "word2" },
            },
            """
            {
                "Date" : "0001-01-01T00:00:00+00:00",
                "TemperatureCelsius" : 10,
                "Summary" : "I am a string",
                "DatesAvailable" : [ "0001-01-01T00:00:00+00:00" ],
                "TemperatureRanges" :
                {
                    "key" : { "Low" : 0, "High" : 5 }
                },
                "SummaryWords" : [ "word1", "word2" ]
            }
            """);

            static object[] Wrap<T>(T value, string expectedYaml) => new object[] { value, expectedYaml };
        }

        [Fact]
        public static void HighLowTemps_ContextReportsCorrectMetadata()
        {
            YamlTypeInfo<HighLowTemps> yamlTypeInfo = Net60GeneratedContext.Default.HighLowTemps;

            HighLowTemps instance = yamlTypeInfo.CreateObject();
            Assert.Equal(0, instance.Low);
            Assert.Equal(0, instance.High);

            Assert.Equal(2, yamlTypeInfo.Properties.Count);

            YamlPropertyInfo yamlPropertyInfo = yamlTypeInfo.Properties[0];
            Assert.Equal("High", yamlPropertyInfo.Name);
            yamlPropertyInfo.Set(instance, 1);
            Assert.Equal(1, instance.High);
            Assert.Equal(1, yamlPropertyInfo.Get(instance));

            yamlPropertyInfo = yamlTypeInfo.Properties[1];
            Assert.Equal("Low", yamlPropertyInfo.Name);
            yamlPropertyInfo.Set(instance, 2);
            Assert.Equal(2, instance.Low);
            Assert.Equal(2, yamlPropertyInfo.Get(instance));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SupportsRecursiveTypeSerialization()
        {
            YamlTypeInfo<MyLinkedList> yamlTypeInfo = Net60GeneratedContext.Default.MyLinkedList;

            MyLinkedList linkedList = new(
                value: 0,
                nested: new(
                    value: 1,
                    nested: new(
                        value: 2,
                        nested: null)));

            string yaml = YamlSerializer.Serialize(linkedList, yamlTypeInfo);
            Assert.Equal("""{"Value":0,"Nested":{"Value":1,"Nested":{"Value":2,"Nested":null}}}""", yaml);

            linkedList = YamlSerializer.Deserialize(yaml, yamlTypeInfo);
            Assert.Equal(2, linkedList.Nested.Nested.Value);
        }

        [Fact]
        public static void CombinedContexts_ThrowsInvalidOperationException()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = YamlTypeInfoResolver.Combine(Net60GeneratedContext.Default, new DefaultYamlTypeInfoResolver())
            };

            // v6 Contexts do not implement IYamlTypeInfoResolver so combined resolvers will throw by default.
            // We're fine with this since it doesn't introduce any regressions to existing code.
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new HighLowTemps(), options));
        }
    }
}
