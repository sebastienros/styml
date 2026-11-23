// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        // A custom data type representing a point where JSON is "XValue,YValue".
        public struct Point
        {
            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get;}
            public int Y { get;}
        }

        // Converter for a custom data type that has additional state (coordinateOffset).
        private class PointConverter : YamlConverter<Point>
        {
            private int _coordinateOffset;

            public PointConverter() { }

            public PointConverter(int coordinateOffset = 0)
            {
                _coordinateOffset = coordinateOffset;
            }

            public override Point Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.String)
                {
                    throw new YamlException();
                }

                string[] stringValues = reader.GetString().Split(',');
                if (stringValues.Length != 2)
                {
                    throw new YamlException();
                }

                if (!int.TryParse(stringValues[0], out int x) || !int.TryParse(stringValues[1], out int y))
                {
                    throw new YamlException();
                }

                var value = new Point(x + _coordinateOffset, y + _coordinateOffset);
                return value;
            }

            public override void Write(Utf8YamlWriter writer, Point value, YamlSerializerOptions options)
            {
                string stringValue = $"{value.X - _coordinateOffset},{value.Y - _coordinateOffset}";
                writer.WriteStringValue(stringValue);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomValueConverterFromArray()
        {
            const string yaml = """["1,2","3,4"]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter());

            Point[] Points = YamlSerializer.Deserialize<Point[]>(yaml, options);
            Assert.Equal(2, Points.Length);
            Assert.Equal(1, Points[0].X);
            Assert.Equal(2, Points[0].Y);
            Assert.Equal(3, Points[1].X);
            Assert.Equal(4, Points[1].Y);

            string yamlSerialized = YamlSerializer.Serialize(Points, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomValueConverterFromRoot()
        {
            const string yaml = """
                "1,2"
                """;

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter());

            Point obj = YamlSerializer.Deserialize<Point>(yaml, options);
            Assert.Equal(1, obj.X);
            Assert.Equal(2, obj.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomValueConverterFromRootAndOptions()
        {
            const string yaml = """
                "1,2"
                """;

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter(100));

            Point obj = YamlSerializer.Deserialize<Point>(yaml, options);
            Assert.Equal(101, obj.X);
            Assert.Equal(102, obj.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        public static void CustomValueConverterFromRootWithNullable()
        {
            const string yaml = "null";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter());

            Point? obj = YamlSerializer.Deserialize<Point?>(yaml, options);
            Assert.Null(obj);
        }

        [Fact]
        public static void CustomValueConverterFromRootFail()
        {
            // Invalid JSON according to the converter.
            const string yaml = """
                "1"
                """;

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter());

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Point>(yaml, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomValueConverterStructFromRoot()
        {
            const string yaml = """
                "1,2"
                """;

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointConverter());

            Point obj = YamlSerializer.Deserialize<Point>(yaml, options);
            Assert.Equal(1, obj.X);
            Assert.Equal(2, obj.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        /// <summary>
        /// Converter for Point but treats it as an Object in the JSON.
        /// </summary>
        private class PointObjectConverter : YamlConverter<Point>
        {
            public override Point Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                if (reader.TokenType != YamlTokenType.StartObject)
                {
                    throw new YamlException();
                }

                reader.Read();
                if (reader.TokenType != YamlTokenType.PropertyName)
                {
                    throw new YamlException();
                }

                if (reader.GetString() != "COORD")
                {
                    throw new YamlException();
                }

                reader.Read();
                string[] stringValues = reader.GetString().Split(',');
                if (stringValues.Length != 2)
                {
                    throw new YamlException();
                }

                if (!int.TryParse(stringValues[0], out int x) || !int.TryParse(stringValues[1], out int y))
                {
                    throw new YamlException();
                }

                var value = new Point(x, y);

                reader.Read();
                if (reader.TokenType != YamlTokenType.EndObject)
                {
                    throw new YamlException();
                }

                return value;
            }

            public override void Write(Utf8YamlWriter writer, Point value, YamlSerializerOptions options)
            {
                writer.WriteStartObject();

                string stringValue = $"{value.X},{value.Y}";
                writer.WriteString("COORD", stringValue);

                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomObjectConverterInArray()
        {
            const string yaml = """[{"COORD":"1,2"},{"COORD":"3,4"}]""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointObjectConverter());

            Point[] Points = YamlSerializer.Deserialize<Point[]>(yaml, options);
            Assert.Equal(2, Points.Length);
            Assert.Equal(1, Points[0].X);
            Assert.Equal(2, Points[0].Y);
            Assert.Equal(3, Points[1].X);
            Assert.Equal(4, Points[1].Y);

            string yamlSerialized = YamlSerializer.Serialize(Points, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomObjectConverterFromRoot()
        {
            const string yaml = """{"COORD":"1,2"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new PointObjectConverter());

            Point obj = YamlSerializer.Deserialize<Point>(yaml, options);
            Assert.Equal(1, obj.X);
            Assert.Equal(2, obj.Y);

            string yamlSerialized = YamlSerializer.Serialize(obj, options);
            Assert.Equal(yaml, yamlSerialized);
        }
    }
}
