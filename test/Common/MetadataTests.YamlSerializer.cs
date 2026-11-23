// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class MetadataTests
    {
        [Fact]
        public async Task RoundTripSerializerOverloads()
        {
            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();
            string yaml = await Serializer.SerializeWrapper(expected, YamlContext.Default.WeatherForecastWithPOCOs);
            WeatherForecastWithPOCOs actual = await Serializer.DeserializeWrapper(yaml, YamlContext.Default.WeatherForecastWithPOCOs);
            VerifyWeatherForecastWithPOCOs(expected, actual);

            yaml = await Serializer.SerializeWrapper(actual, typeof(WeatherForecastWithPOCOs), YamlContext.Default);
            actual = (WeatherForecastWithPOCOs)await Serializer.DeserializeWrapper(yaml, typeof(WeatherForecastWithPOCOs), YamlContext.Default);
            VerifyWeatherForecastWithPOCOs(expected, actual);
        }

        [Fact]
        public async Task RoundTripSerializerOverloads_UntypedMetadata()
        {
            YamlTypeInfo untypedMetadata = YamlContext.Default.WeatherForecastWithPOCOs;

            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();
            string yaml = await Serializer.SerializeWrapper(expected, untypedMetadata);
            WeatherForecastWithPOCOs actual = Assert.IsType<WeatherForecastWithPOCOs>(await Serializer.DeserializeWrapper(yaml, untypedMetadata));
            VerifyWeatherForecastWithPOCOs(expected, actual);
        }

        [Fact]
        public async Task SerializerOverloads_NullUntypedMetadata_ThrowsArgumentNullException()
        {
            YamlTypeInfo nullMetadata = null!;
            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("yamlTypeInfo", () => Serializer.SerializeWrapper(expected, nullMetadata));
            await AssertExtensions.ThrowsAsync<ArgumentNullException>("yamlTypeInfo", () => Serializer.DeserializeWrapper("{}", nullMetadata));
        }

        private static WeatherForecastWithPOCOs CreateWeatherForecastWithPOCOs()
        {
            return new WeatherForecastWithPOCOs
            {
                Date = DateTime.Parse("2019-08-01T00:00:00-07:00"),
                TemperatureCelsius = 25,
                Summary = "Hot",
                DatesAvailable = new List<DateTimeOffset>
                {
                    DateTimeOffset.Parse("2019-08-01T00:00:00-07:00"),
                    DateTimeOffset.Parse("2019-08-02T00:00:00-07:00"),
                },
                TemperatureRanges = new Dictionary<string, HighLowTemps> {
                    {
                        "Cold",
                        new HighLowTemps
                        {
                            High = 20,
                            Low = -10,
                        }
                    },
                    {
                        "Hot",
                        new HighLowTemps
                        {
                            High = 60,
                            Low = 20,
                        }
                    },
                },
                SummaryWords = new string[] { "Cool", "Windy", "Humid" },
            };
        }

        private static void VerifyWeatherForecastWithPOCOs(WeatherForecastWithPOCOs expected, WeatherForecastWithPOCOs obj)
        {
            Assert.Equal(expected.Date, obj.Date);
            Assert.Equal(expected.TemperatureCelsius, obj.TemperatureCelsius);
            Assert.Equal(expected.Summary, obj.Summary);
            Assert.Equal(expected.DatesAvailable.Count, obj.DatesAvailable.Count);
            for (int i = 0; i < expected.DatesAvailable.Count; i++)
            {
                Assert.Equal(expected.DatesAvailable[i], obj.DatesAvailable[i]);
            }
            List<KeyValuePair<string, HighLowTemps>> expectedTemperatureRanges = expected.TemperatureRanges.OrderBy(kv => kv.Key).ToList();
            List<KeyValuePair<string, HighLowTemps>> objTemperatureRanges = obj.TemperatureRanges.OrderBy(kv => kv.Key).ToList();
            Assert.Equal(expectedTemperatureRanges.Count, objTemperatureRanges.Count);
            for (int i = 0; i < expectedTemperatureRanges.Count; i++)
            {
                Assert.Equal(expectedTemperatureRanges[i].Key, objTemperatureRanges[i].Key);
                Assert.Equal(expectedTemperatureRanges[i].Value.Low, objTemperatureRanges[i].Value.Low);
                Assert.Equal(expectedTemperatureRanges[i].Value.High, objTemperatureRanges[i].Value.High);
            }
            Assert.Equal(expected.SummaryWords.Length, obj.SummaryWords.Length);
            for (int i = 0; i < expected.SummaryWords.Length; i++)
            {
                Assert.Equal(expected.SummaryWords[i], obj.SummaryWords[i]);
            }
        }
    }
}
