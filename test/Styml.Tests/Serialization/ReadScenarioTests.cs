// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Catch-all location for combined read scenarios (user reported and otherwise) that aren't
    /// specific to one serialization feature.
    /// </summary>
    public class ReadScenarioTests
    {
        [Fact]
        public void StringEnumUriAndCustomDateTimeConverter()
        {
            // Validating a scenario reported with https://github.com/dotnet/runtime/issues/29894.
            // Our DateTime parsing is ISO 8601 strict, more flexible parsing is possible by
            // writing a simple converter. String based enum parsing is handled by registering
            // a custom built-in parser (YamlStringEnumConverter). Uri is handled implicitly.

            string yaml =
                @"{" +
                    @"""picture"": ""http://placehold.it/32x32""," +
                    @"""eyeColor"": ""Brown""," +
                    @"""registered"": ""2015-05-30T01:50:21 -01:00""" +
                @"}";

            YamlSerializerOptions options = new YamlSerializerOptions
            {
                PropertyNamingPolicy = YamlNamingPolicy.CamelCase,
                Converters =
                {
                    new YamlStringEnumConverter(),
                    new MyDateTimeConverter()
                }
            };

            Model model = YamlSerializer.Deserialize<Model>(yaml, options);
            Assert.Equal(Color.Brown, model.EyeColor);
            Assert.Equal(@"http://placehold.it/32x32", model.Picture.OriginalString);
            Assert.Equal(DateTime.Parse("2015-05-30T01:50:21 -01:00"), model.Registered);
        }

        // The built-in DateTime parser is stricter than DateTime.Parse.
        public class MyDateTimeConverter : YamlConverter<DateTime>
        {
            public override DateTime Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => DateTime.Parse(reader.GetString());

            public override void Write(Utf8YamlWriter writer, DateTime value, YamlSerializerOptions options)
                => writer.WriteStringValue(value.ToString("O"));
        }

        public sealed class Model
        {
            public Color EyeColor { get; set; }
            public Uri Picture { get; set; }
            public DateTime Registered { get; set; }
        }

        public enum Color
        {
            Blue,
            Green,
            Brown
        }
    }
}
