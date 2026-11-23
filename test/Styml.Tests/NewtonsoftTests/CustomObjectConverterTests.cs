// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using Xunit;
using System.Text.Yaml.Serialization;

namespace System.Text.Yaml.Tests
{
    public class CustomObjectConverterTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeAndConvertNullValue()
        {
            NullInterfaceTestClass initial = new NullInterfaceTestClass
            {
                Company = "Company!",
                DecimalRange = new Range<decimal> { First = 0, Last = 1 },
                Id = new Guid(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11),
                IntRange = new Range<int> { First = int.MinValue, Last = int.MaxValue },
                Year = 2010,
                NullDecimalRange = null
            };

            string yaml = YamlSerializer.Serialize(initial, new YamlSerializerOptions { WriteIndented = true });

            Assert.Equal("""
                {
                  "Id": "00000001-0002-0003-0405-060708090a0b",
                  "Year": 2010,
                  "Company": "Company!",
                  "DecimalRange": {
                    "First": 0,
                    "Last": 1
                  },
                  "IntRange": {
                    "First": -2147483648,
                    "Last": 2147483647
                  },
                  "NullDecimalRange": null
                }
                """.NormalizeLineEndings(), yaml);
        }

        [Fact]
        public void DeserializeAndConvertNullValue()
        {
            string yaml = """
                {
                  "Id": "00000001-0002-0003-0405-060708090a0b",
                  "Year": 2010,
                  "Company": "Company!",
                  "DecimalRange": {
                    "First": 0,
                    "Last": 1
                  },
                  "IntRange": {
                    "First": -2147483648,
                    "Last": 2147483647
                  },
                  "NullDecimalRange": null
                }
                """;

            YamlSerializer.Serialize(yaml, new YamlSerializerOptions { WriteIndented = true });

            NullInterfaceTestClass deserialized = YamlSerializer.Deserialize<NullInterfaceTestClass>(yaml);

            Assert.Equal("Company!", deserialized.Company);
            Assert.Equal(new Guid(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11), deserialized.Id);
            Assert.Equal(0, deserialized.DecimalRange.First);
            Assert.Equal(1, deserialized.DecimalRange.Last);
            Assert.Equal(int.MinValue, deserialized.IntRange.First);
            Assert.Equal(int.MaxValue, deserialized.IntRange.Last);
            Assert.Null(deserialized.NullDecimalRange);
            Assert.Equal(2010, deserialized.Year);
        }

        [Fact]
        public void DeserializeByteArrayFromYamlArray()
        {
            string yaml = """
                {
                  "ByteArray": "AAECAw==",
                  "NullByteArray": null
                }
                """;

            ByteArrayClass c = YamlSerializer.Deserialize<ByteArrayClass>(yaml);
            Assert.NotNull(c.ByteArray);
            Assert.Equal(4, c.ByteArray.Length);
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, c.ByteArray);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void SerializeByteArrayClass()
        {
            ByteArrayClass byteArrayClass = new ByteArrayClass();
            byteArrayClass.ByteArray = s_testData;
            byteArrayClass.NullByteArray = null;

            string yaml = YamlSerializer.Serialize(byteArrayClass, new YamlSerializerOptions { WriteIndented = true });

            Assert.Equal("""
                {
                  "ByteArray": "VGhpcyBpcyBzb21lIHRlc3QgZGF0YSEhIQ==",
                  "NullByteArray": null
                }
                """.NormalizeLineEndings(), yaml);
        }

        [Fact]
        public void DeserializeByteArrayClass()
        {
            string yaml = """
                {
                  "ByteArray": "VGhpcyBpcyBzb21lIHRlc3QgZGF0YSEhIQ==",
                  "NullByteArray": null
                }
                """;

            ByteArrayClass byteArrayClass = YamlSerializer.Deserialize<ByteArrayClass>(yaml);

            Assert.Equal(s_testData, byteArrayClass.ByteArray);
            Assert.Null(byteArrayClass.NullByteArray);
        }

        private static readonly byte[] s_testData = "This is some test data!!!"u8.ToArray();

        [Fact]
        [Trait("category", "jsonoutput")]
        public void AssertShouldSerializeTest()
        {
            MyClass myClass = new MyClass
            {
                Value = "Foo",
                Thing = new MyThing { Number = 456, }
            };
            string yaml = YamlSerializer.Serialize(myClass);

            const string expected = """{"Value":"Foo","Thing":{"Number":456}}""";
            Assert.Equal(expected, yaml);
        }

        [Fact]
        public void AssertDoesNotDeserializeInterface()
        {
            const string validYaml =
                """
                        [{
                                            "Value": "A value",
                                            "Thing": {
                                                "Number": 123
                                            }
                                        }]
                    """;

            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<List<MyClass>>(validYaml));

            const string invalidYaml =
                """
                        {
                                            "Value": "A value",
                                            "Thing": {
                                                "Number": 123
                                            }
                                        }
                    """;

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<List<MyClass>>(invalidYaml));
        }
    }

    internal class Range<T>
    {
        public T First { get; set; }
        public T Last { get; set; }
    }

    internal class NullInterfaceTestClass
    {
        public virtual Guid Id { get; set; }
        public virtual int Year { get; set; }
        public virtual string Company { get; set; }
        public virtual Range<decimal> DecimalRange { get; set; }
        public virtual Range<int> IntRange { get; set; }
        public virtual Range<decimal>? NullDecimalRange { get; set; }
    }

    internal class MyClass
    {
        public string? Value { get; set; }

        public IThing? Thing { get; set; }
    }

    internal interface IThing
    {
        int Number { get; set; }
    }

    internal class MyThing : IThing
    {
        public int Number { get; set; }
    }

    internal class ByteArrayClass
    {
        public byte[] ByteArray { get; set; }
        public byte[]? NullByteArray { get; set; }
    }
}
