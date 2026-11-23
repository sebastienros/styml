// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public abstract class RealWorldContextTests
    {
        protected ITestContext DefaultContext { get; }
        private Func<YamlSerializerOptions, ITestContext> _contextCreator;

        public RealWorldContextTests(ITestContext defaultContext, Func<YamlSerializerOptions, ITestContext> contextCreator)
        {
            DefaultContext = defaultContext;
            _contextCreator = contextCreator;
        }

        public abstract void EnsureFastPathGeneratedAsExpected();

        [Fact]
        public virtual void RoundTripLocation()
        {
            Location expected = CreateLocation();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.Location);
            Location obj = YamlSerializer.Deserialize(yaml, DefaultContext.Location);
            VerifyLocation(expected, obj);
        }

        [Fact]
        public virtual void RoundTripNumberTypes()
        {
            NumberTypes expected = CreateNumberTypes();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.NumberTypes);
            NumberTypes obj = YamlSerializer.Deserialize(yaml, DefaultContext.NumberTypes);
            VerifyNumberTypes(expected, obj);
        }


        [Fact]
        public virtual void RoundTripIndexViewModel()
        {
            IndexViewModel expected = CreateIndexViewModel();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.IndexViewModel);
            IndexViewModel obj = YamlSerializer.Deserialize(yaml, DefaultContext.IndexViewModel);

            VerifyIndexViewModel(expected, obj);
        }

        [Fact]
        public virtual void RoundTripCampaignSummaryViewModel()
        {
            CampaignSummaryViewModel expected = CreateCampaignSummaryViewModel();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.CampaignSummaryViewModel);
            CampaignSummaryViewModel obj = YamlSerializer.Deserialize(yaml, DefaultContext.CampaignSummaryViewModel);

            VerifyCampaignSummaryViewModel(expected, obj);
        }

        [Fact]
        public virtual void RoundTripActiveOrUpcomingEvent()
        {
            ActiveOrUpcomingEvent expected = CreateActiveOrUpcomingEvent();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.ActiveOrUpcomingEvent);
            ActiveOrUpcomingEvent obj = YamlSerializer.Deserialize(yaml, DefaultContext.ActiveOrUpcomingEvent);

            VerifyActiveOrUpcomingEvent(expected, obj);
        }

        [Fact]
        public virtual void RoundTripCollectionsDictionary()
        {
            WeatherForecastWithPOCOs expected = CreateWeatherForecastWithPOCOs();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.WeatherForecastWithPOCOs);
            WeatherForecastWithPOCOs obj = YamlSerializer.Deserialize(yaml, DefaultContext.WeatherForecastWithPOCOs);

            VerifyWeatherForecastWithPOCOs(expected, obj);
        }

        [Fact]
        public virtual void RoundTripEmptyPoco()
        {
            EmptyPoco expected = CreateEmptyPoco();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.EmptyPoco);
            EmptyPoco obj = YamlSerializer.Deserialize(yaml, DefaultContext.EmptyPoco);

            VerifyEmptyPoco(expected, obj);
        }

        [Fact]
        public virtual void RoundTripTypeNameClash()
        {
            RepeatedTypes.Location expected = CreateRepeatedLocation();

            string yaml = YamlSerializer.Serialize(expected, DefaultContext.RepeatedLocation);
            RepeatedTypes.Location obj = YamlSerializer.Deserialize(yaml, DefaultContext.RepeatedLocation);

            VerifyRepeatedLocation(expected, obj);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("\"str\"")]
        [InlineData("[1,2,3]")]
        [InlineData("{ \"key\" : \"value\" }")]
        public void RoundtripYamlDocument(string yaml)
        {
            using YamlDocument yamlDocument = YamlDocument.Parse(yaml);

            string actualYaml = YamlSerializer.Serialize(yamlDocument, DefaultContext.YamlDocument);
            YamlTestHelper.AssertYamlEqual(yaml, actualYaml);

            using YamlDocument actualYamlDocument = YamlSerializer.Deserialize(actualYaml, DefaultContext.YamlDocument);
            YamlTestHelper.AssertYamlEqual(yamlDocument.RootElement, actualYamlDocument.RootElement);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("\"str\"")]
        [InlineData("[1,2,3]")]
        [InlineData("{ \"key\" : \"value\" }")]
        public void RoundtripYamlElement(string yaml)
        {
            using YamlDocument yamlDocument = YamlDocument.Parse(yaml);
            YamlElement yamlElement = yamlDocument.RootElement;

            string actualYaml = YamlSerializer.Serialize(yamlElement, DefaultContext.YamlElement);
            YamlTestHelper.AssertYamlEqual(yaml, actualYaml);

            YamlElement actualYamlElement = YamlSerializer.Deserialize(actualYaml, DefaultContext.YamlElement);
            YamlTestHelper.AssertYamlEqual(yamlElement, actualYamlElement);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripValueTuple()
        {
            bool isIncludeFieldsEnabled = DefaultContext.IsIncludeFieldsEnabled;

            var tuple = (Label1: "string", Label2: 42, true);
            string expectedYaml = isIncludeFieldsEnabled
                ? "{\"Item1\":\"string\",\"Item2\":42,\"Item3\":true}"
                : "{}";

            string yaml = YamlSerializer.Serialize(tuple, DefaultContext.ValueTupleStringInt32Boolean);
            Assert.Equal(expectedYaml, yaml);

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                // Deserialization not supported in fast path serialization only mode
                // but if there are no fields we won't throw because we throw on the property lookup
                if (isIncludeFieldsEnabled)
                {
                    Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize(yaml, DefaultContext.ValueTupleStringInt32Boolean));
                }
                else
                {
                    (string, int, bool) obj = YamlSerializer.Deserialize(yaml, DefaultContext.ValueTupleStringInt32Boolean);
                    Assert.Equal(default((string, int, bool)), obj);
                }
            }
            else
            {
                var deserializedTuple = YamlSerializer.Deserialize(yaml, DefaultContext.ValueTupleStringInt32Boolean);
                Assert.Equal(isIncludeFieldsEnabled ? tuple : default, deserializedTuple);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomConverter_Class()
        {
            const string Yaml = "{\"MyInt\":142}";

            ClassWithCustomConverter obj = new ClassWithCustomConverter()
            {
                MyInt = 42
            };

            string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverter);
            Assert.Equal(Yaml, yaml);

            obj = YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithCustomConverter);
            Assert.Equal(42, obj.MyInt);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomConverterFactory_Class()
        {
            const string Yaml = "{\"MyInt\":142}";

            ClassWithCustomConverterFactory obj = new()
            {
                MyInt = 42
            };

            string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactory);
            Assert.Equal(Yaml, yaml);

            obj = YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithCustomConverterFactory);
            Assert.Equal(42, obj.MyInt);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomConverter_Struct()
        {
            const string Yaml = "{\"MyInt\":142}";

            StructWithCustomConverter obj = new StructWithCustomConverter()
            {
                MyInt = 42
            };

            string yaml = YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverter);
            Assert.Equal(Yaml, yaml);

            obj = YamlSerializer.Deserialize(Yaml, DefaultContext.StructWithCustomConverter);
            Assert.Equal(42, obj.MyInt);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundtripWithCustomConverterProperty_Class()
        {
            const string ExpectedYaml = "{\"Property\":42}";

            ClassWithCustomConverterProperty obj = new()
            {
                Property = new ClassWithCustomConverterProperty.NestedPoco { Value = 42 }
            };

            // Types with properties in custom converters do not support fast path serialization.
            Assert.True(DefaultContext.ClassWithCustomConverterProperty.SerializeHandler is null);

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterProperty);
                Assert.Equal(ExpectedYaml, yaml);

                obj = YamlSerializer.Deserialize<ClassWithCustomConverterProperty>(ExpectedYaml, DefaultContext.ClassWithCustomConverterProperty);
                Assert.Equal(42, obj.Property.Value);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomConverterNullableProperty()
        {
            const string Yaml = "{\"TimeSpan\":42}";

            var obj = new ClassWithCustomConverterNullableProperty
            {
                TimeSpan = TimeSpan.FromSeconds(42)
            };

            // Types with properties in custom converters do not support fast path serialization.
            Assert.True(DefaultContext.ClassWithCustomConverterNullableProperty.SerializeHandler is null);

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterNullableProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterNullableProperty);
                Assert.Equal(Yaml, yaml);

                obj = YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithCustomConverterNullableProperty);
                Assert.Equal(42, obj.TimeSpan.Value.TotalSeconds);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomConverterFactoryNullableProperty()
        {
            const string Yaml = "{\"MyEnum\":\"Two\"}";

            var obj = new ClassWithCustomConverterFactoryNullableProperty
            {
                MyEnum = SourceGenSampleEnum.Two
            };

            // Types with properties in custom converters do not support fast path serialization.
            Assert.True(DefaultContext.ClassWithCustomConverterFactoryNullableProperty.SerializeHandler is null);

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactoryNullableProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactoryNullableProperty);
                Assert.Equal(Yaml, yaml);

                obj = YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithCustomConverterFactoryNullableProperty);
                Assert.Equal(SourceGenSampleEnum.Two, obj.MyEnum.Value);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundtripWithCustomConverterProperty_Struct()
        {
            const string ExpectedYaml = "{\"Property\":42}";

            StructWithCustomConverterProperty obj = new()
            {
                Property = new ClassWithCustomConverterProperty.NestedPoco { Value = 42 }
            };

            // Types with properties in custom converters do not support fast path serialization.
            Assert.True(DefaultContext.StructWithCustomConverterProperty.SerializeHandler is null);

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverterProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverterProperty);
                Assert.Equal(ExpectedYaml, yaml);

                obj = YamlSerializer.Deserialize<StructWithCustomConverterProperty>(ExpectedYaml, DefaultContext.StructWithCustomConverterProperty);
                Assert.Equal(42, obj.Property.Value);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomPropertyConverterFactory_Class()
        {
            const string Yaml = "{\"MyEnum\":\"One\"}";

            ClassWithCustomConverterFactoryProperty obj = new()
            {
                MyEnum = SourceGenSampleEnum.One
            };

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactoryProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactoryProperty);
                Assert.Equal(Yaml, yaml);
            }

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.ClassWithCustomConverterFactoryProperty));
            }
            else
            {
                obj = YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithCustomConverterFactoryProperty);
                Assert.Equal(SourceGenSampleEnum.One, obj.MyEnum);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void RoundTripWithCustomPropertyConverterFactory_Struct()
        {
            const string Yaml = "{\"MyEnum\":\"One\"}";

            StructWithCustomConverterFactoryProperty obj = new()
            {
                MyEnum = SourceGenSampleEnum.One
            };

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverterFactoryProperty));
            }
            else
            {
                string yaml = YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverterFactoryProperty);
                Assert.Equal(Yaml, yaml);
            }

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(obj, DefaultContext.StructWithCustomConverterFactoryProperty));
            }
            else
            {
                obj = YamlSerializer.Deserialize(Yaml, DefaultContext.StructWithCustomConverterFactoryProperty);
                Assert.Equal(SourceGenSampleEnum.One, obj.MyEnum);
            }
        }

        [Fact]
        public virtual void BadCustomConverter_Class()
        {
            const string Yaml = "{\"MyInt\":142}";

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Serialize(new ClassWithBadCustomConverter(), DefaultContext.ClassWithBadCustomConverter));

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Deserialize(Yaml, DefaultContext.ClassWithBadCustomConverter));
        }

        [Fact]
        public virtual void BadCustomConverter_Struct()
        {
            const string Yaml = "{\"MyInt\":142}";

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Serialize(new StructWithBadCustomConverter(), DefaultContext.StructWithBadCustomConverter));

            Assert.Throws<InvalidOperationException>(() =>
                YamlSerializer.Deserialize(Yaml, DefaultContext.StructWithBadCustomConverter));
        }

        protected static Location CreateLocation()
        {
            return new Location
            {
                Id = 1234,
                Address1 = "The Street Name",
                Address2 = "20/11",
                City = "The City",
                State = "The State",
                PostalCode = "abc-12",
                Name = "Nonexisting",
                PhoneNumber = "+0 11 222 333 44",
                Country = "The Greatest"
            };
        }

        protected static void VerifyLocation(Location expected, Location obj)
        {
            Assert.Equal(expected.Address1, obj.Address1);
            Assert.Equal(expected.Address2, obj.Address2);
            Assert.Equal(expected.City, obj.City);
            Assert.Equal(expected.State, obj.State);
            Assert.Equal(expected.PostalCode, obj.PostalCode);
            Assert.Equal(expected.Name, obj.Name);
            Assert.Equal(expected.PhoneNumber, obj.PhoneNumber);
            Assert.Equal(expected.Country, obj.Country);
        }

        protected static NumberTypes CreateNumberTypes()
        {
            return new NumberTypes
            {
                Single = 1.1f,
                Double = 2.2d,
                Decimal = 3.3m,
                SByte = -1,
                Byte = 1,
                UShort = 2,
                Short = -2,
                UInt = 3,
                Int = -3,
                ULong = 4,
                Long = -4,
            };
        }

        protected static void VerifyNumberTypes(NumberTypes expected, NumberTypes obj)
        {
            Assert.Equal(expected.Single, obj.Single);
            Assert.Equal(expected.Double, obj.Double);
            Assert.Equal(expected.Decimal, obj.Decimal);
            Assert.Equal(expected.SByte, obj.SByte);
            Assert.Equal(expected.Byte, obj.Byte);
            Assert.Equal(expected.UShort, obj.UShort);
            Assert.Equal(expected.Short, obj.Short);
            Assert.Equal(expected.UInt, obj.UInt);
            Assert.Equal(expected.Int, obj.Int);
            Assert.Equal(expected.ULong, obj.ULong);
            Assert.Equal(expected.Long, obj.Long);
        }

        protected static ActiveOrUpcomingEvent CreateActiveOrUpcomingEvent()
        {
            return new ActiveOrUpcomingEvent
            {
                Id = 10,
                CampaignManagedOrganizerName = "Name FamilyName",
                CampaignName = "The very new campaign",
                Description = "The .NET Foundation works with Microsoft and the broader industry to increase the exposure of open source projects in the .NET community and the .NET Foundation. The .NET Foundation provides access to these resources to projects and looks to promote the activities of our communities.",
                EndDate = DateTimeTestHelpers.FixedDateTimeValue.AddYears(1),
                Name = "Just a name",
                ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                StartDate = DateTimeTestHelpers.FixedDateTimeValue,
                Offset = TimeSpan.FromHours(2)
            };
        }

        protected static void VerifyActiveOrUpcomingEvent(ActiveOrUpcomingEvent expected, ActiveOrUpcomingEvent obj)
        {
            Assert.Equal(expected.CampaignManagedOrganizerName, obj.CampaignManagedOrganizerName);
            Assert.Equal(expected.CampaignName, obj.CampaignName);
            Assert.Equal(expected.Description, obj.Description);
            Assert.Equal(expected.EndDate, obj.EndDate);
            Assert.Equal(expected.Id, obj.Id);
            Assert.Equal(expected.ImageUrl, obj.ImageUrl);
            Assert.Equal(expected.Name, obj.Name);
            Assert.Equal(expected.StartDate, obj.StartDate);
        }

        protected static CampaignSummaryViewModel CreateCampaignSummaryViewModel()
        {
            return new CampaignSummaryViewModel
            {
                Description = "Very nice campaign",
                Headline = "The Headline",
                Id = 234235,
                OrganizationName = "The Company XYZ",
                ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                Title = "Promoting Open Source"
            };
        }

        protected static void VerifyCampaignSummaryViewModel(CampaignSummaryViewModel expected, CampaignSummaryViewModel obj)
        {
            Assert.Equal(expected.Description, obj.Description);
            Assert.Equal(expected.Headline, obj.Headline);
            Assert.Equal(expected.Id, obj.Id);
            Assert.Equal(expected.ImageUrl, obj.ImageUrl);
            Assert.Equal(expected.OrganizationName, obj.OrganizationName);
            Assert.Equal(expected.Title, obj.Title);
        }

        protected static IndexViewModel CreateIndexViewModel()
        {
            return new IndexViewModel
            {
                IsNewAccount = false,
                FeaturedCampaign = new CampaignSummaryViewModel
                {
                    Description = "Very nice campaign",
                    Headline = "The Headline",
                    Id = 234235,
                    OrganizationName = "The Company XYZ",
                    ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                    Title = "Promoting Open Source"
                },
                ActiveOrUpcomingEvents = Enumerable.Repeat(
                    new ActiveOrUpcomingEvent
                    {
                        Id = 10,
                        CampaignManagedOrganizerName = "Name FamilyName",
                        CampaignName = "The very new campaign",
                        Description = "The .NET Foundation works with Microsoft and the broader industry to increase the exposure of open source projects in the .NET community and the .NET Foundation. The .NET Foundation provides access to these resources to projects and looks to promote the activities of our communities.",
                        EndDate = DateTimeTestHelpers.FixedDateTimeValue.AddYears(1),
                        Name = "Just a name",
                        ImageUrl = "https://www.dotnetfoundation.org/theme/img/carousel/foundation-diagram-content.png",
                        StartDate = DateTimeTestHelpers.FixedDateTimeValue
                    },
                    count: 20).ToList()
            };
        }

        protected static void VerifyIndexViewModel(IndexViewModel expected, IndexViewModel obj)
        {
            Assert.Equal(expected.ActiveOrUpcomingEvents.Count, obj.ActiveOrUpcomingEvents.Count);
            for (int i = 0; i < expected.ActiveOrUpcomingEvents.Count; i++)
            {
                VerifyActiveOrUpcomingEvent(expected.ActiveOrUpcomingEvents[i], obj.ActiveOrUpcomingEvents[i]);
            }

            VerifyCampaignSummaryViewModel(expected.FeaturedCampaign, obj.FeaturedCampaign);
            Assert.Equal(expected.HasFeaturedCampaign, obj.HasFeaturedCampaign);
            Assert.Equal(expected.IsNewAccount, obj.IsNewAccount);
        }

        protected static WeatherForecastWithPOCOs CreateWeatherForecastWithPOCOs()
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

        protected static void VerifyWeatherForecastWithPOCOs(WeatherForecastWithPOCOs expected, WeatherForecastWithPOCOs obj)
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

        protected static RepeatedTypes.Location CreateRepeatedLocation()
        {
            return new RepeatedTypes.Location
            {
                FakeId = 1234,
                FakeAddress1 = "The Street Name",
                FakeAddress2 = "20/11",
                FakeCity = "The City",
                FakeState = "The State",
                FakePostalCode = "abc-12",
                FakeName = "Nonexisting",
                FakePhoneNumber = "+0 11 222 333 44",
                FakeCountry = "The Greatest"
            };
        }

        protected static void VerifyRepeatedLocation(RepeatedTypes.Location expected, RepeatedTypes.Location obj)
        {
            Assert.Equal(expected.FakeAddress1, obj.FakeAddress1);
            Assert.Equal(expected.FakeAddress2, obj.FakeAddress2);
            Assert.Equal(expected.FakeCity, obj.FakeCity);
            Assert.Equal(expected.FakeState, obj.FakeState);
            Assert.Equal(expected.FakePostalCode, obj.FakePostalCode);
            Assert.Equal(expected.FakeName, obj.FakeName);
            Assert.Equal(expected.FakePhoneNumber, obj.FakePhoneNumber);
            Assert.Equal(expected.FakeCountry, obj.FakeCountry);
        }

        protected static EmptyPoco CreateEmptyPoco() => new EmptyPoco();

        protected static void VerifyEmptyPoco(EmptyPoco expected, EmptyPoco obj)
        {
            Assert.NotNull(expected);
            Assert.NotNull(obj);
        }

        [Fact]
        public virtual void NestedSameTypeWorks()
        {
            MyType myType = new() { Type = new() };
            string yaml = YamlSerializer.Serialize(myType, DefaultContext.MyType);
            myType = YamlSerializer.Deserialize(yaml, DefaultContext.MyType);
            Assert.Equal(yaml, YamlSerializer.Serialize(myType, DefaultContext.MyType));

            MyType2 myType2 = new() { Type = new MyIntermediateType() { Type = myType } };
            yaml = YamlSerializer.Serialize(myType2, DefaultContext.MyType2);
            myType2 = YamlSerializer.Deserialize(yaml, DefaultContext.MyType2);
            Assert.Equal(yaml, YamlSerializer.Serialize(myType2, DefaultContext.MyType2));
        }

        [Fact]
        public virtual void SerializeObjectArray()
        {
            IndexViewModel index = CreateIndexViewModel();
            CampaignSummaryViewModel campaignSummary = CreateCampaignSummaryViewModel();

            string yaml = YamlSerializer.Serialize(new object[] { index, campaignSummary }, DefaultContext.ObjectArray);
            object[] arr = YamlSerializer.Deserialize(yaml, DefaultContext.ObjectArray);

            YamlElement indexAsYamlElement = (YamlElement)arr[0];
            YamlElement campaignSummeryAsYamlElement = (YamlElement)arr[1];
            VerifyIndexViewModel(index, YamlSerializer.Deserialize(indexAsYamlElement.GetRawText(), DefaultContext.IndexViewModel));
            VerifyCampaignSummaryViewModel(campaignSummary, YamlSerializer.Deserialize(campaignSummeryAsYamlElement.GetRawText(), DefaultContext.CampaignSummaryViewModel));
        }

        [Fact]
        public virtual void SerializeObjectArray_WithCustomOptions()
        {
            IndexViewModel index = CreateIndexViewModel();
            CampaignSummaryViewModel campaignSummary = CreateCampaignSummaryViewModel();

            YamlSerializerOptions options = new() { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
            ITestContext context = _contextCreator(options);

            string yaml = YamlSerializer.Serialize(new object[] { index, campaignSummary }, context.ObjectArray);
            object[] arr = YamlSerializer.Deserialize(yaml, context.ObjectArray);

            YamlElement indexAsYamlElement = (YamlElement)arr[0];
            YamlElement campaignSummeryAsYamlElement = (YamlElement)arr[1];
            VerifyIndexViewModel(index, YamlSerializer.Deserialize(indexAsYamlElement.GetRawText(), context.IndexViewModel));
            VerifyCampaignSummaryViewModel(campaignSummary, YamlSerializer.Deserialize(campaignSummeryAsYamlElement.GetRawText(), context.CampaignSummaryViewModel));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void SerializeObjectArray_SimpleTypes_WithCustomOptions()
        {
            YamlSerializerOptions options = new() { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
            ITestContext context = _contextCreator(options);

            string yaml = YamlSerializer.Serialize(new object[] { "Hello", "World" }, typeof(object[]), (YamlSerializerContext)context);
            object[] arr = (object[])YamlSerializer.Deserialize(yaml, typeof(object[]), (YamlSerializerContext)context);

            YamlElement hello = (YamlElement)arr[0];
            YamlElement world = (YamlElement)arr[1];
            Assert.Equal("\"Hello\"", hello.GetRawText());
            Assert.Equal("\"World\"", world.GetRawText());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void SerializeByteArray()
        {
            byte[] value = new byte[] { 1, 2, 3 };
            const string expectedYaml = "\"AQID\"";

            string actualYaml = YamlSerializer.Serialize(value, DefaultContext.ByteArray);
            Assert.Equal(expectedYaml, actualYaml);

            byte[] arr = YamlSerializer.Deserialize(actualYaml, DefaultContext.ByteArray);
            Assert.Equal(value, arr);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void HandlesNestedTypes()
        {
            string yaml = """{"MyInt":5}""";
            MyNestedClass obj = YamlSerializer.Deserialize<MyNestedClass>(yaml, DefaultContext.MyNestedClass);
            Assert.Equal(5, obj.MyInt);
            Assert.Equal(yaml, YamlSerializer.Serialize(obj, DefaultContext.MyNestedClass));

            MyNestedClass.MyNestedNestedClass obj2 = YamlSerializer.Deserialize<MyNestedClass.MyNestedNestedClass>(yaml, DefaultContext.MyNestedNestedClass);
            Assert.Equal(5, obj2.MyInt);
            Assert.Equal(yaml, YamlSerializer.Serialize(obj2, DefaultContext.MyNestedNestedClass));
        }

        public class MyNestedClass
        {
            public int MyInt { get; set; }

            public class MyNestedNestedClass
            {
                public int MyInt { get; set; }
            }
        }

        [Fact]
        public void ConstructingFromOptionsKeepsReference()
        {
            YamlConverter converter = new YamlStringEnumConverter<YamlIgnoreCondition>();
            YamlSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { converter }
            };

            YamlSerializerContext context = (YamlSerializerContext)_contextCreator(options);
            Assert.Same(options, context.Options);
            Assert.Equal(options.PropertyNameCaseInsensitive, context.Options.PropertyNameCaseInsensitive);
            Assert.Same(converter, context.Options.Converters[0]);
        }

        [Fact]
        public void YamlContextDefaultClonesDefaultOptions()
        {
            YamlSerializerContext context = (YamlSerializerContext)DefaultContext;
            Assert.Equal(0, context.Options.Converters.Count);
        }

        [Fact]
        public void YamlContextOptionsNotMutableAfterConstruction()
        {
            YamlSerializerContext context = (YamlSerializerContext)DefaultContext;
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => context.Options.PropertyNameCaseInsensitive = true);
            string exAsStr = ex.ToString();
            Assert.Contains("YamlSerializerOptions", exAsStr);
            Assert.Contains("YamlSerializerContext", exAsStr);

            context = (YamlSerializerContext)_contextCreator(new YamlSerializerOptions());
            ex = Assert.Throws<InvalidOperationException>(() => context.Options.PropertyNameCaseInsensitive = true);
            exAsStr = ex.ToString();
            Assert.Contains("YamlSerializerOptions", exAsStr);
            Assert.Contains("YamlSerializerContext", exAsStr);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void ParameterizedConstructor()
        {
            string yaml = YamlSerializer.Serialize(new HighLowTempsImmutable(1, 2), DefaultContext.HighLowTempsImmutable);
            Assert.Contains("""
                "High":1
                """, yaml);
            Assert.Contains("""
                "Low":2
                """, yaml);

            HighLowTempsImmutable obj = YamlSerializer.Deserialize(yaml, DefaultContext.HighLowTempsImmutable);
            Assert.Equal(1, obj.High);
            Assert.Equal(2, obj.Low);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void PositionalRecord()
        {
            string yaml = YamlSerializer.Serialize(new HighLowTempsRecord(1, 2), DefaultContext.HighLowTempsRecord);
            Assert.Contains("""
                "High":1
                """, yaml);
            Assert.Contains("""
                "Low":2
                """, yaml);

            HighLowTempsRecord obj = YamlSerializer.Deserialize(yaml, DefaultContext.HighLowTempsRecord);
            Assert.Equal(1, obj.High);
            Assert.Equal(2, obj.Low);
        }

        [Fact]
        public virtual void EnumAndNullable()
        {
            RunTest(new ClassWithEnumAndNullable() { Day = DayOfWeek.Monday, NullableDay = DayOfWeek.Tuesday });
            RunTest(new ClassWithEnumAndNullable());

            void RunTest(ClassWithEnumAndNullable expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithEnumAndNullable);
                ClassWithEnumAndNullable actual = YamlSerializer.Deserialize(yaml, DefaultContext.ClassWithEnumAndNullable);
                Assert.Equal(expected.Day, actual.Day);
                Assert.Equal(expected.NullableDay, actual.NullableDay);
            }
        }

        public class ClassWithEnumAndNullable
        {
            public DayOfWeek Day { get; set; }
            public DayOfWeek? NullableDay { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public virtual void ClassWithNullableProperties_Roundtrip()
        {
            RunTest(new ClassWithNullableProperties
            {
                Uri = new Uri("http://contoso.com"),
                Array = new int[] { 42 },
                ByteArray = new byte[] { 1, 2, 3 },
                Poco = new ClassWithNullableProperties.MyPoco(),

                NullableUri = new Uri("http://contoso.com"),
                NullableArray = new int[] { 42 },
                NullablePoco = new ClassWithNullableProperties.MyPoco()
            });

            RunTest(new ClassWithNullableProperties());

            void RunTest(ClassWithNullableProperties expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithNullableProperties);

                if (expected.ByteArray is null)
                {
                    Assert.Contains("\"ByteArray\":null", yaml);
                }

                ClassWithNullableProperties actual = YamlSerializer.Deserialize(yaml, DefaultContext.ClassWithNullableProperties);

                Assert.Equal(expected.Uri, actual.Uri);
                Assert.Equal(expected.Array, actual.Array);
                Assert.Equal(expected.ByteArray, actual.ByteArray);
                Assert.Equal(expected.Poco, actual.Poco);

                Assert.Equal(expected.NullableUri, actual.NullableUri);
                Assert.Equal(expected.NullableArray, actual.NullableArray);
                Assert.Equal(expected.NullablePoco, actual.NullablePoco);

                Assert.Equal(expected.NullableUriParameter, actual.NullableUriParameter);
                Assert.Equal(expected.NullableArrayParameter, actual.NullableArrayParameter);
                Assert.Equal(expected.NullablePocoParameter, actual.NullablePocoParameter);
            }
        }

#if NET
        [Fact]
        public virtual void ClassWithDateOnlyAndTimeOnlyValues_Roundtrip()
        {
            RunTest(new ClassWithDateOnlyAndTimeOnlyValues
            {
                DateOnly = DateOnly.Parse("2022-05-10"),
                NullableDateOnly = DateOnly.Parse("2022-05-10"),

                TimeOnly = TimeOnly.Parse("21:51:51"),
                NullableTimeOnly = TimeOnly.Parse("21:51:51"),
            });

            RunTest(new ClassWithDateOnlyAndTimeOnlyValues());

            void RunTest(ClassWithDateOnlyAndTimeOnlyValues expected)
            {
                string yaml = YamlSerializer.Serialize(expected, DefaultContext.ClassWithDateOnlyAndTimeOnlyValues);
                ClassWithDateOnlyAndTimeOnlyValues actual = YamlSerializer.Deserialize(yaml, DefaultContext.ClassWithDateOnlyAndTimeOnlyValues);

                Assert.Equal(expected.DateOnly, actual.DateOnly);
                Assert.Equal(expected.NullableDateOnly, actual.NullableDateOnly);

                Assert.Equal(expected.TimeOnly, actual.TimeOnly);
                Assert.Equal(expected.NullableTimeOnly, actual.NullableTimeOnly);
            }
        }

        public class ClassWithDateOnlyAndTimeOnlyValues
        {
            public DateOnly DateOnly { get; set; }
            public DateOnly? NullableDateOnly { get; set; }

            public TimeOnly TimeOnly { get; set; }
            public TimeOnly? NullableTimeOnly { get; set; }
        }
#endif

        public class ClassWithNullableProperties
        {
            public Uri? Uri { get; set; }
            public int[]? Array { get; set; }
            public byte[]? ByteArray { get; set; }
            public MyPoco? Poco { get; set; }

            public Uri? NullableUri { get; set; }
            public int[]? NullableArray { get; set; }
            public MyPoco? NullablePoco { get; set; }

            // struct types containing nullable reference types as generic parameters
            public GenericStruct<Uri?> NullableUriParameter { get; set; }
            public GenericStruct<int[]?> NullableArrayParameter { get; set; }
            public GenericStruct<MyPoco?> NullablePocoParameter { get; set; }

            public (string? x, int y)? NullableArgumentOfNullableStruct { get; set; }

            public record MyPoco { }
            public struct GenericStruct<T> { }
        }

        private const string ExceptionMessageFromCustomContext = "Exception thrown from custom context.";

        [Fact]
        [Trait("category", "jsonoutput")]
        public void GetTypeInfoCalledDuringPolymorphicSerialization()
        {
            CustomContext context = new(new YamlSerializerOptions());

            // Empty array is fine since we don't need metadata for children.
            Assert.Equal("[]", YamlSerializer.Serialize(Array.Empty<object>(), context.ObjectArray));
            Assert.Equal("[]", YamlSerializer.Serialize(Array.Empty<object>(), typeof(object[]), context));

            // GetTypeInfo method called to get metadata for element run-time type.
            object[] objArr = new object[] { new MyStruct() };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(objArr, context.ObjectArray));
            Assert.Contains(ExceptionMessageFromCustomContext, ex.ToString());

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(objArr, typeof(object[]), context));
            Assert.Contains(ExceptionMessageFromCustomContext, ex.ToString());
        }

        internal class CustomContext : YamlSerializerContext
        {
            public CustomContext(YamlSerializerOptions options) : base(options) { }

            private YamlTypeInfo<object> _object;
            public YamlTypeInfo<object> Object => _object ??= YamlMetadataServices.CreateValueInfo<object>(Options, YamlMetadataServices.ObjectConverter);

            private YamlTypeInfo<object[]> _objectArray;
            public YamlTypeInfo<object[]> ObjectArray => _objectArray ??= YamlMetadataServices.CreateArrayInfo<object>(Options, new YamlCollectionInfoValues<object[]> { ElementInfo = Object });

            protected override YamlSerializerOptions? GeneratedSerializerOptions => null;

            public override YamlTypeInfo GetTypeInfo(Type type)
            {
                if (type == typeof(object[]))
                {
                    return ObjectArray;
                }

                throw new InvalidOperationException(ExceptionMessageFromCustomContext);
            }
        }

        protected static void AssertFastPathLogicCorrect<T>(string expectedYaml, T value, YamlTypeInfo<T> typeInfo)
        {
            using MemoryStream ms = new();
            using Utf8YamlWriter writer = new(ms);
            typeInfo.SerializeHandler!(writer, value);
            writer.Flush();

            YamlTestHelper.AssertYamlEqual(expectedYaml, Encoding.UTF8.GetString(ms.ToArray()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void PropertyOrdering()
        {
            MyTypeWithPropertyOrdering obj = new();
            string yaml = YamlSerializer.Serialize(obj, DefaultContext.MyTypeWithPropertyOrdering);
            Assert.Equal("{\"C\":0,\"B\":0,\"A\":0}", yaml);
        }

        [Fact]
        public virtual void NullableStruct()
        {
            PersonStruct? person = new()
            {
                FirstName = "Jane",
                LastName = "Doe"
            };

            string yaml = YamlSerializer.Serialize(person, DefaultContext.NullablePersonStruct);
            YamlTestHelper.AssertYamlEqual("""{"FirstName":"Jane","LastName":"Doe"}""", yaml);

            person = YamlSerializer.Deserialize(yaml, DefaultContext.NullablePersonStruct);
            Assert.Equal("Jane", person.Value.FirstName);
            Assert.Equal("Doe", person.Value.LastName);
        }

        [Fact]
        public void TypeWithValidationAttributes()
        {
            var instance = new TypeWithValidationAttributes { Name = "Test Name", Email = "email@test.com" };

            string yaml = YamlSerializer.Serialize(instance, DefaultContext.TypeWithValidationAttributes);
            YamlTestHelper.AssertYamlEqual("""{"Name":"Test Name","Email":"email@test.com"}""", yaml);
            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                // Deserialization not supported in fast path serialization only mode
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize(yaml, DefaultContext.TypeWithValidationAttributes));
            }
            else
            {
                instance = YamlSerializer.Deserialize(yaml, DefaultContext.TypeWithValidationAttributes);
                Assert.Equal("Test Name", instance.Name);
                Assert.Equal("email@test.com", instance.Email);
            }
        }

        [Fact]
        public void TypeWithDerivedAttribute()
        {
            var instance = new TypeWithDerivedAttribute();

            string yaml = YamlSerializer.Serialize(instance, DefaultContext.TypeWithDerivedAttribute);
            YamlTestHelper.AssertYamlEqual("{}", yaml);

            // Deserialization not supported in fast path serialization only mode
            // but we can deserialize empty types as we throw only when looking up properties and there are no properties here.
            instance = YamlSerializer.Deserialize(yaml, DefaultContext.TypeWithDerivedAttribute);
            Assert.NotNull(instance);
        }

        [Fact]
        public void PolymorphicClass_Serialization()
        {
            PolymorphicClass value = new PolymorphicClass.DerivedClass { Number = 42, Boolean = true };

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(value, DefaultContext.PolymorphicClass));
            }
            else
            {
                string expectedYaml = """{"$type" : "derivedClass", "Number" : 42, "Boolean" : true }""";
                string actualYaml = YamlSerializer.Serialize(value, DefaultContext.PolymorphicClass);
                YamlTestHelper.AssertYamlEqual(expectedYaml, actualYaml);
            }
        }

        [Fact]
        public void PolymorphicClass_Deserialization()
        {
            string yaml = """{"$type" : "derivedClass", "Number" : 42, "Boolean" : true }""";

            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<PolymorphicClass>(yaml, DefaultContext.PolymorphicClass));
            }
            else
            {
                PolymorphicClass result = YamlSerializer.Deserialize<PolymorphicClass>(yaml, DefaultContext.PolymorphicClass);
                PolymorphicClass.DerivedClass derivedResult = Assert.IsType<PolymorphicClass.DerivedClass>(result);
                Assert.Equal(42, derivedResult.Number);
                Assert.True(derivedResult.Boolean);
            }
        }

        [Fact]
        public void NumberHandlingHonoredOnPoco()
        {
            if (DefaultContext.YamlSourceGenerationMode == YamlSourceGenerationMode.Serialization)
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new PocoWithNumberHandlingAttr(), DefaultContext.PocoWithNumberHandlingAttr));
            }
            else
            {
                YamlTestHelper.AssertYamlEqual("""{"Id":"0"}""", YamlSerializer.Serialize(new PocoWithNumberHandlingAttr(), DefaultContext.PocoWithNumberHandlingAttr));
            }
        }

        [Theory]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.PublicProperty))]
        [InlineData(MemberTypes.Field, nameof(PocoWithMixedVisibilityMembers.PublicField))]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.InternalProperty))]
        [InlineData(MemberTypes.Field, nameof(PocoWithMixedVisibilityMembers.InternalField))]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.PropertyWithCustomName), "customProp")]
        [InlineData(MemberTypes.Field, nameof(PocoWithMixedVisibilityMembers.FieldWithCustomName), "customField")]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.BaseProperty))]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.ShadowProperty))]
        [InlineData(MemberTypes.Property, nameof(PocoWithMixedVisibilityMembers.ExperimentalProperty))]
        public void YamlPropertyInfo_PopulatesAttributeProvider(MemberTypes memberType, string propertyName, string? yamlPropertyName = null)
        {
            if (DefaultContext.YamlSourceGenerationMode is YamlSourceGenerationMode.Serialization)
            {
                return; // No metadata generated
            }

            YamlTypeInfo typeInfo = DefaultContext.PocoWithMixedVisibilityMembers;
            string name = yamlPropertyName ?? propertyName;
            YamlPropertyInfo prop = typeInfo.Properties.FirstOrDefault(prop => prop.Name == name);
            Assert.NotNull(prop);

            MemberInfo memberInfo;
            if (prop.AttributeProvider is null)
            {
                Assert.Equal(typeof(object), prop.PropertyType);
                memberInfo = typeof(PocoWithMixedVisibilityMembers).GetMember(propertyName, memberType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
                Type actualPropertyType = memberInfo is PropertyInfo pInfo ? pInfo.PropertyType : ((FieldInfo)memberInfo).FieldType;
                Assert.False(typeInfo.Options.TryGetTypeInfo(actualPropertyType, out _));
                return;
            }

            memberInfo = Assert.IsAssignableFrom<MemberInfo>(prop.AttributeProvider);
            string? actualYamlPropertyName = memberInfo.GetCustomAttribute<YamlPropertyNameAttribute>()?.Name;

            Assert.True(memberInfo.DeclaringType.IsAssignableFrom(typeInfo.Type));
            Assert.Equal(memberType, memberInfo.MemberType);
            Assert.Equal(prop.PropertyType, memberInfo is PropertyInfo p ? p.PropertyType : ((FieldInfo)memberInfo).FieldType);
            Assert.Equal(propertyName, memberInfo.Name);
            Assert.Equal(yamlPropertyName, actualYamlPropertyName);
        }
    }
}
