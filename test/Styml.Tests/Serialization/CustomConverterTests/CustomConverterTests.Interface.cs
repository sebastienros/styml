// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        [YamlConverter(typeof(MyInterfaceConverter))]
        private interface IMyInterface
        {
            int IntValue { get; set; }
            string StringValue { get; set; }
        }

        // A custom converter that writes and reads the string property as a top-level value
        private class MyInterfaceConverter : YamlConverter<IMyInterface>
        {
            public override IMyInterface Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => new MyClass
                {
                    IntValue = 42,
                    StringValue = reader.GetString()
                };

            public override void Write(Utf8YamlWriter writer, IMyInterface value, YamlSerializerOptions options) => writer.WriteStringValue(value.StringValue);
        }

        private class MyClass : IMyInterface
        {
            public int IntValue { get; set; }
            public string StringValue { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void CustomInterfaceConverter_Serialization()
        {
            IMyInterface value = new MyClass { IntValue = 11, StringValue = "myString" };

            string expectedYaml = "\"myString\"";
            string actualYaml = YamlSerializer.Serialize(value);
            Assert.Equal(expectedYaml, actualYaml);
        }

        [Fact]
        public static void CustomInterfaceConverter_Deserialization()
        {
            string yaml = "\"myString\"";

            IMyInterface result = YamlSerializer.Deserialize<IMyInterface>(yaml);

            Assert.IsType<MyClass>(result);
            Assert.Equal("myString", result.StringValue);
            Assert.Equal(42, result.IntValue);
        }
    }
}
