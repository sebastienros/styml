// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class WriteValueTests
    {
        [Fact]
        public static void NullWriterThrows()
        {
            ArgumentNullException ex;

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(writer: null, 1));
            Assert.Contains("writer", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(writer: null, 1, typeof(int)));
            Assert.Contains("writer", ex.Message);
        }

        [Fact]
        public async static Task NullInputTypeThrows()
        {
            ArgumentException ex;
            Utf8YamlWriter writer = new Utf8YamlWriter(new MemoryStream());

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(writer: writer, value: null, inputType: null));
            Assert.Contains("inputType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(writer, value: null, inputType: null));
            Assert.Contains("inputType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Serialize(1, inputType: null));
            Assert.Contains("inputType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.SerializeToUtf8Bytes(null, inputType: null));
            Assert.Contains("inputType", ex.Message);

            ex = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YamlSerializer.SerializeAsync(new MemoryStream(), null, inputType: null));
            Assert.Contains("inputType", ex.Message);
        }

        [Fact]
        public async static Task NullValueWithValueTypeThrows()
        {
            YamlException ex;

            Utf8YamlWriter writer = new Utf8YamlWriter(new MemoryStream());
            ex = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(writer: writer, value: null, inputType: typeof(int)));
            Assert.Contains(typeof(int).ToString(), ex.Message);

            ex = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(value: null, inputType: typeof(int)));
            Assert.Contains(typeof(int).ToString(), ex.Message);

            ex = Assert.Throws<YamlException>(() => YamlSerializer.SerializeToUtf8Bytes(value: null, inputType: typeof(int)));
            Assert.Contains(typeof(int).ToString(), ex.Message);

            ex = await Assert.ThrowsAsync<YamlException>(async () => await YamlSerializer.SerializeAsync(new MemoryStream(), value: null, inputType: typeof(int)));
            Assert.Contains(typeof(int).ToString(), ex.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async static Task NullValueWithNullableSuccess()
        {
            byte[] nullUtf8Literal = "null"u8.ToArray();

            var stream = new MemoryStream();
            Utf8YamlWriter writer = new Utf8YamlWriter(stream);
            YamlSerializer.Serialize(writer: writer, value: null, inputType: typeof(int?));
            byte[] yamlBytes = stream.ToArray();
            Assert.Equal(nullUtf8Literal, yamlBytes);

            string yamlString = YamlSerializer.Serialize(value: null, inputType: typeof(int?));
            Assert.Equal("null", yamlString);

            yamlBytes = YamlSerializer.SerializeToUtf8Bytes(value: null, inputType: typeof(int?));
            Assert.Equal(nullUtf8Literal, yamlBytes);

            stream = new MemoryStream();
            await YamlSerializer.SerializeAsync(stream, value: null, inputType: typeof(int?));
            Assert.Equal(nullUtf8Literal, stream.ToArray());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CanWriteValueToYamlArray()
        {
            using MemoryStream memoryStream = new MemoryStream();
            using Utf8YamlWriter writer = new Utf8YamlWriter(memoryStream);

            writer.WriteStartObject();
            writer.WriteStartArray("test");
            YamlSerializer.Serialize<int>(writer, 1);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.Equal("{\"test\":[1]}", yaml);
        }

        public class CustomClassWithEscapedProperty
        {
            public int pizza { get; set; }
            public int hello\u6C49\u5B57 { get; set; }
            public int normal { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void SerializeToWriterRoundTripEscaping()
        {
            const string yamlIn = " { \"p\\u0069zza\": 1, \"hello\\u6C49\\u5B57\": 2, \"normal\": 3 }";

            CustomClassWithEscapedProperty input = YamlSerializer.Deserialize<CustomClassWithEscapedProperty>(yamlIn);

            Assert.Equal(1, input.pizza);
            Assert.Equal(2, input.hello\u6C49\u5B57);
            Assert.Equal(3, input.normal);

            string normalizedString = YamlSerializer.Serialize(input);
            Assert.Equal("{\"pizza\":1,\"hello\\u6C49\\u5B57\":2,\"normal\":3}", normalizedString);

            CustomClassWithEscapedProperty inputNormalized = YamlSerializer.Deserialize<CustomClassWithEscapedProperty>(normalizedString);
            Assert.Equal(1, inputNormalized.pizza);
            Assert.Equal(2, inputNormalized.hello\u6C49\u5B57);
            Assert.Equal(3, inputNormalized.normal);

            using MemoryStream memoryStream = new MemoryStream();
            using Utf8YamlWriter writer = new Utf8YamlWriter(memoryStream);
            YamlSerializer.Serialize(writer, inputNormalized);
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(memoryStream.ToArray());

            Assert.Equal(normalizedString, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriterOptionsWinIndented()
        {
            int[] input = new int[3] { 1, 2, 3 };

            var serializerOptions = new YamlSerializerOptions
            {
                WriteIndented = true,
            };

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream))
                {
                    YamlSerializer.Serialize(writer, input, serializerOptions);
                }
                Assert.Equal("[1,2,3]", Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Indented = false }))
                {
                    YamlSerializer.Serialize(writer, input, serializerOptions);
                }
                Assert.Equal("[1,2,3]", Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Indented = true }))
                {
                    YamlSerializer.Serialize(writer, input);
                }
                Assert.Equal($"[{Environment.NewLine}  1,{Environment.NewLine}  2,{Environment.NewLine}  3{Environment.NewLine}]", Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriterOptionsWinEncoder()
        {
            string input = "abcd+<>&";

            var serializerOptions = new YamlSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream))
                {
                    YamlSerializer.Serialize(writer, input, serializerOptions);
                }
                Assert.Equal("\"abcd\\u002B\\u003C\\u003E\\u0026\"", Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Encoder = JavaScriptEncoder.Default }))
                {
                    YamlSerializer.Serialize(writer, input, serializerOptions);
                }
                Assert.Equal("\"abcd\\u002B\\u003C\\u003E\\u0026\"", Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    YamlSerializer.Serialize(writer, input);
                }
                Assert.Equal("\"abcd+<>&\"", Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriterOptionsSkipValidation()
        {
            int[] input = new int[3] { 1, 2, 3 };

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream))
                {
                    writer.WriteStartObject();
                    Assert.Throws<YamlException>(() => YamlSerializer.Serialize(writer, input));
                }
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { SkipValidation = true }))
                {
                    writer.WriteStartObject();
                    YamlSerializer.Serialize(writer, input);
                }
                Assert.Equal("{[1,2,3]", Encoding.UTF8.GetString(stream.ToArray()));
            }

            var serializerOptions = new YamlSerializerOptions
            {
                Converters = { new InvalidArrayConverter() },
            };

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream))
                {
                    Assert.Throws<YamlException>(() => YamlSerializer.Serialize(writer, input, serializerOptions));
                }
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { SkipValidation = true }))
                {
                    YamlSerializer.Serialize(writer, input, serializerOptions);
                }
                Assert.Equal("[}", Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        public class InvalidArrayConverter : YamlConverter<int[]>
        {
            public override int[] Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, int[] value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();
                writer.WriteEndObject();
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void OptionsFollowToConverter()
        {
            string yaml = "{\"type\":\"array\",\"array\":[1]}";
            string yamlFormatted =
"""
    {
      "type": "array",
      "array": [
        1
      ]
    }
    """;
            string expectedInner = "{\"array\":[1]}";

            var tempOptions = new YamlSerializerOptions();
            tempOptions.Converters.Add(new CustomConverter());
            DeepArray direct = YamlSerializer.Deserialize<DeepArray>(yaml, tempOptions);
            IContent custom = YamlSerializer.Deserialize<IContent>(yaml, tempOptions);

            {
                var options = new YamlSerializerOptions();
                options.Converters.Add(new CustomConverter());

                Assert.Equal(expectedInner, YamlSerializer.Serialize(direct, options));
                Assert.Equal(yaml, YamlSerializer.Serialize(custom, options));
            }

            {
                var options = new YamlSerializerOptions
                {
                    Converters = { new CustomConverter() }
                };
                WriteAndValidate(direct, typeof(DeepArray), expectedInner, options, writerOptions: default);
                WriteAndValidate(custom, typeof(IContent), yaml, options, writerOptions: default);
            }

            {
                var options = new YamlSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new CustomConverter() }
                };
                var writerOptions = new YamlWriterOptions { Indented = false };
                WriteAndValidate(direct, typeof(DeepArray), expectedInner, options, writerOptions);
                WriteAndValidate(custom, typeof(IContent), yaml, options, writerOptions);
            }

            {
                var options = new YamlSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new CustomConverter() }
                };
                WriteAndValidate(direct, typeof(DeepArray), expectedInner, options, writerOptions: default);
                WriteAndValidate(custom, typeof(IContent), yaml, options, writerOptions: default);
            }

            {
                var options = new YamlSerializerOptions
                {
                    Converters = { new CustomConverter() }
                };
                var writerOptions = new YamlWriterOptions { Indented = true };
                WriteAndValidate(direct, typeof(DeepArray), $"{{{Environment.NewLine}  \"array\": [{Environment.NewLine}    1{Environment.NewLine}  ]{Environment.NewLine}}}", options, writerOptions);
                WriteAndValidate(custom, typeof(IContent), yamlFormatted, options, writerOptions);
            }

            static void WriteAndValidate(object input, Type type, string expected, YamlSerializerOptions options, YamlWriterOptions writerOptions)
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8YamlWriter(stream, writerOptions))
                    {
                        YamlSerializer.Serialize(writer, input, type, options);
                    }
                    Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()), ignoreLineEndingDifferences: true);
                }
            }
        }

        public class CustomClassToExceedMaxBufferSize
        {
            private static readonly string s_name = new string('a', 100_000_000);//Large enough value to cause integer overflow exception when allocating buffer and small enought to not cause a "The YAML value of length X is too large and not supported."
            public string GetName1 => s_name;
            public string GetName2 => s_name;
            public string GetName3 => s_name;
            public string GetName4 => s_name;
            public string GetName5 => s_name;
            public string GetName6 => s_name;
            public string GetName7 => s_name;
            public string GetName8 => s_name;
            public string GetName9 => s_name;
            public string GetName10 => s_name;
            public string GetName11 => s_name;
            public string GetName12 => s_name;
            public string GetName13 => s_name;
            public string GetName14 => s_name;
            public string GetName15 => s_name;
            public string GetName16 => s_name;
            public string GetName17 => s_name;
            public string GetName18 => s_name;
            public string GetName19 => s_name;
            public string GetName20 => s_name;
        }

        // NOTE: SerializeExceedMaximumBufferSize test is constrained to run on Windows and MacOSX because it causes 
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can 
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the 
        //       time the memory is accessed which triggers the full memory allocation. 
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        public static void SerializeExceedMaximumBufferSize()
        {
            CustomClassToExceedMaxBufferSize temp = new CustomClassToExceedMaxBufferSize();

            Assert.Throws<OutOfMemoryException>(() => YamlSerializer.Serialize(temp, typeof(CustomClassToExceedMaxBufferSize)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(500)]
        public static void MaxDepthFlowsToConverter(int maxDepth)
        {
            var converter = new InstrumentedConverter();
            var options = new YamlSerializerOptions { Converters = { converter }, MaxDepth = maxDepth };
            int effectiveMaxDepth = maxDepth == 0 ? 64 : maxDepth;

            YamlSerializer.Serialize(value: 42, options);

            Assert.Equal(effectiveMaxDepth, converter.WriterOptions.MaxDepth);
        }

        private class InstrumentedConverter : YamlConverter<int>
        {
            public YamlWriterOptions WriterOptions { get; private set; }

            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => throw new NotImplementedException();
            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                WriterOptions = writer.Options;
                writer.WriteNumberValue(value);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public static void CustomMaxDepth_DepthWithinLimit_Succeeds(int maxDepth)
        {
            var options = new YamlSerializerOptions { MaxDepth = maxDepth };
            int effectiveMaxDepth = maxDepth == 0 ? 64 : maxDepth;

            Peano? value = Peano.CreateFromNumber(effectiveMaxDepth);
            YamlSerializer.Serialize(value, options);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public static void CustomMaxDepth_DepthExceedsLimit_Fails(int maxDepth)
        {
            var options = new YamlSerializerOptions { MaxDepth = maxDepth };
            int effectiveMaxDepth = maxDepth == 0 ? 64 : maxDepth;

            Peano value = Peano.CreateFromNumber(effectiveMaxDepth + 1);
            YamlException exn = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(value, options));
            Assert.Contains("A possible object cycle was detected", exn.Message);
        }

        public class Peano
        {
            public Peano? Successor { get; init; }

            public static Peano? CreateFromNumber(int value)
            {
                return value == 0 ? null : new Peano { Successor = CreateFromNumber(value - 1) };
            }
        }
    }
}
