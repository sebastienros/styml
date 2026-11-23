// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        // A custom long[] converter as comma-delimited string "1,2,3".
        internal class LongArrayConverter : YamlConverter<long[]>
        {
            public LongArrayConverter() { }

            public override long[] Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                string yaml = reader.GetString();

                var list = new List<long>();

                foreach (string str in yaml.Split(','))
                {
                    if (!long.TryParse(str, out long l))
                    {
                        throw new YamlException("Too big for a long");
                    }

                    list.Add(l);
                }

                return list.ToArray();
            }

            public override void Write(Utf8YamlWriter writer, long[] value, YamlSerializerOptions options)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < value.Length; i++)
                {
                    builder.Append(value[i].ToString());

                    if (i != value.Length - 1)
                    {
                        builder.Append(",");
                    }
                }

                writer.WriteStringValue(builder.ToString());
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomArrayConverterAsRoot()
        {
            const string yaml = """
                "1,2,3"
                """;

            var options = new YamlSerializerOptions();
            options.Converters.Add(new LongArrayConverter());

            long[] arr = YamlSerializer.Deserialize<long[]>(yaml, options);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
            Assert.Equal(3, arr[2]);

            string yamlSerialized = YamlSerializer.Serialize(arr, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Fact]
        public static void CustomArrayConverterFail()
        {
            string yaml = $"\"{long.MaxValue}0\"";

            var options = new YamlSerializerOptions { Converters = { new LongArrayConverter() } };
            YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<long[]>(yaml, options));

            Assert.Null(ex.InnerException);
            Assert.Equal("$", ex.Path);
            Assert.Equal("Too big for a long", ex.Message);
        }

        private class ClassWithProperty
        {
            public long[] Array1 { get; set; }
            public long[] Array2 { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomArrayConverterInProperty()
        {
            const string yaml = """{"Array1":"1,2,3","Array2":"4,5"}""";

            var options = new YamlSerializerOptions();
            options.Converters.Add(new LongArrayConverter());

            ClassWithProperty obj = YamlSerializer.Deserialize<ClassWithProperty>(yaml, options);
            Assert.Equal(1, obj.Array1[0]);
            Assert.Equal(2, obj.Array1[1]);
            Assert.Equal(3, obj.Array1[2]);
            Assert.Equal(4, obj.Array2[0]);
            Assert.Equal(5, obj.Array2[1]);

            string yamlSerialized = YamlSerializer.Serialize(obj, options);
            Assert.Equal(yaml, yamlSerialized);
        }

        [Theory]
        [InlineData(typeof(ProxyClassConverter))]
        [InlineData(typeof(ProxyClassWithConstructorConverter))]
        public static async Task ClassWithProxyConverter_WorksWithAsyncDeserialization(Type converterType)
        {
            // Regression test for https://github.com/dotnet/runtime/issues/74108

            const int Count = 1000;

            var stream = new MemoryStream();
            var data = Enumerable.Range(1, Count).Select(_ => new { OtherValue = "otherValue", Value = "value" });
            await YamlSerializer.SerializeAsync(stream, data);
            stream.Position = 0;

            var options = new YamlSerializerOptions
            {
                Converters = { (YamlConverter)Activator.CreateInstance(converterType) },
            };

            PocoWithValue[] result = await YamlSerializer.DeserializeAsync<PocoWithValue[]>(stream, options);
            Assert.Equal(Count, result.Length);
            Assert.All(result, entry => Assert.Equal("value", entry.Value));
        }

        class PocoWithValue
        {
            public string? Value { get; set; }
        }

        class ProxyClass
        {
            public string? Value { get; set; }
        }

        class ProxyClassConverter : YamlConverter<PocoWithValue>
        {
            private YamlConverter<ProxyClass> GetConverter(YamlSerializerOptions options)
                => (YamlConverter<ProxyClass>)options.GetConverter(typeof(ProxyClass));

            public override PocoWithValue? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                var proxy = GetConverter(options).Read(ref reader, typeof(ProxyClass), options);
                return new PocoWithValue { Value = proxy.Value };
            }

            public override void Write(Utf8YamlWriter writer, PocoWithValue value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }

        class ProxyClassWithConstructor
        {
            public ProxyClassWithConstructor(string? value) => Value = value;
            public string? Value { get; }
        }

        class ProxyClassWithConstructorConverter : YamlConverter<PocoWithValue>
        {
            private YamlConverter<ProxyClassWithConstructor> GetConverter(YamlSerializerOptions options)
                => (YamlConverter<ProxyClassWithConstructor>)options.GetConverter(typeof(ProxyClassWithConstructor));

            public override PocoWithValue? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                var proxy = GetConverter(options).Read(ref reader, typeof(ProxyClassWithConstructor), options);
                return new PocoWithValue { Value = proxy.Value };
            }

            public override void Write(Utf8YamlWriter writer, PocoWithValue value, YamlSerializerOptions options)
                => throw new NotImplementedException();
        }
    }
}
