// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ReadValueTests
    {
        [Fact]
        public static void NullReturnTypeThrows()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                Utf8YamlReader reader = default;
                YamlSerializer.Deserialize(ref reader, returnType: null);
            });

            Assert.Contains("returnType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize("", returnType: null));
            Assert.Contains("returnType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize(new char[] { '1' }, returnType: null));
            Assert.Contains("returnType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize(new byte[] { 1 }, returnType: null));
            Assert.Contains("returnType", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.DeserializeAsync(new MemoryStream(), returnType: null));
            Assert.Contains("returnType", ex.Message);
        }

        [Fact]
        public static void NullYamlThrows()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.Deserialize(yaml: (string)null, returnType: typeof(string)));
            Assert.Contains("yaml", ex.Message);

            ex = Assert.Throws<ArgumentNullException>(() => YamlSerializer.DeserializeAsync(utf8Yaml: (Stream?)null, returnType: null));
            Assert.Contains("utf8Yaml", ex.Message);
        }

        [Fact]
        public static void SerializerOptionsStillApply()
        {
            var options = new YamlSerializerOptions();
            options.PropertyNameCaseInsensitive = true;

            byte[] utf8 = Encoding.UTF8.GetBytes("""{"myint16":1}""");
            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);

            SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>(ref reader, options);
            Assert.Equal(1, obj.MyInt16);

            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);
        }

        [Fact]
        public static void ReaderOptionsWinMaxDepth()
        {
            byte[] utf8 = "[[]]"u8.ToArray();

            var readerOptions = new YamlReaderOptions
            {
                MaxDepth = 1,
            };

            var serializerOptions = new YamlSerializerOptions
            {
                MaxDepth = 5,
            };

            Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8, readerOptions);
                YamlSerializer.Deserialize(ref reader, typeof(int[][]), serializerOptions);
            });

            var state = new YamlReaderState(readerOptions);

            Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8, isFinalBlock: false, state);
                YamlSerializer.Deserialize(ref reader, typeof(int[][]), serializerOptions);
            });


            readerOptions = new YamlReaderOptions
            {
                MaxDepth = 5,
            };

            serializerOptions = new YamlSerializerOptions
            {
                MaxDepth = 1,
            };

            {
                var reader = new Utf8YamlReader(utf8, readerOptions);
                int[][] result = YamlSerializer.Deserialize<int[][]>(ref reader);
                Assert.Equal(1, result.Length);
            }

            {
                var reader = new Utf8YamlReader(utf8, readerOptions);
                int[][] result = YamlSerializer.Deserialize<int[][]>(ref reader, serializerOptions);
                Assert.Equal(1, result.Length);
            }
        }

        [Fact]
        public static void ReaderOptionsWinComments()
        {
            byte[] utf8 = "[1, 2, /* some comment */ 3]"u8.ToArray();

            var serializerOptions = new YamlSerializerOptions
            {
                ReadCommentHandling = YamlCommentHandling.Skip,
            };

            Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8);
                YamlSerializer.Deserialize(ref reader, typeof(int[]), serializerOptions);
            });

            Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8, isFinalBlock: false, state: default);
                YamlSerializer.Deserialize(ref reader, typeof(int[]), serializerOptions);
            });

            var readerOptions = new YamlReaderOptions { CommentHandling = YamlCommentHandling.Skip };

            ReadAndVerify(utf8, serializerOptions: null, readerOptions, utf8.Length);
            ReadAndVerify(utf8, serializerOptions, readerOptions, utf8.Length);

            byte[] utf8_CommentsAfter = "[1, 2, 3]/* some comment */"u8.ToArray();

            ReadAndVerify(utf8_CommentsAfter, serializerOptions, readerOptions: default, expectedLength: "[1, 2, 3]".Length);
            ReadAndVerify(utf8_CommentsAfter, serializerOptions, readerOptions, expectedLength: "[1, 2, 3]".Length);

            static void ReadAndVerify(byte[] utf8, YamlSerializerOptions serializerOptions, YamlReaderOptions readerOptions, int expectedLength)
            {
                var reader = new Utf8YamlReader(utf8, readerOptions);
                int[] result = YamlSerializer.Deserialize<int[]>(ref reader, serializerOptions);
                Assert.Equal(3, result.Length);
                Assert.Equal(YamlTokenType.EndArray, reader.TokenType);
                Assert.Equal(expectedLength, reader.BytesConsumed);
            }
        }

        [Fact]
        public static void OnInvalidReaderIsRestored()
        {
            byte[] utf8 = "[1, 2, 3}"u8.ToArray();

            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            long previous = reader.BytesConsumed;

            try
            {
                YamlSerializer.Deserialize(ref reader, typeof(int[]));
                Assert.Fail("Expected ReadValue to throw YamlException for invalid JSON.");
            }
            catch (YamlException) { }

            Assert.Equal(previous, reader.BytesConsumed);
            Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
        }

        [Fact]
        public static void DataRemaining()
        {
            byte[] utf8 = "{\"Foo\":\"abc\", \"Bar\":123}"u8.ToArray();

            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();

            SimpleType instance = YamlSerializer.Deserialize<SimpleType>(ref reader);
            Assert.Equal("abc", instance.Foo);

            Assert.Equal(utf8.Length, reader.BytesConsumed);
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);
        }

        public class SimpleType
        {
            public string Foo { get; set; }
        }

        [Fact]
        public static void ReadPropertyName()
        {
            byte[] utf8 = "{\"Foo\":[1, 2, 3]}"u8.ToArray();

            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            reader.Read();
            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);

            try
            {
                YamlSerializer.Deserialize<SimpleTypeWithArray>(ref reader);
                Assert.Fail("Expected ReadValue to throw YamlException for type mismatch.");
            }
            catch (YamlException) { }

            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);

            int[] instance = YamlSerializer.Deserialize<int[]>(ref reader);
            Assert.Equal(new int[] { 1, 2, 3 }, instance);

            Assert.Equal(utf8.Length - 1, reader.BytesConsumed);
            Assert.Equal(YamlTokenType.EndArray, reader.TokenType);

            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);
            Assert.False(reader.Read());
        }

        [Fact]
        public static void EndObjectOrArrayIsInvalid()
        {
            byte[] utf8 = "[{}]"u8.ToArray();

            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            reader.Read();
            reader.Read();
            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

            try
            {
                YamlSerializer.Deserialize<SimpleTypeWithArray>(ref reader);
                Assert.Fail("Expected ReadValue to throw YamlException for invalid token.");
            }
            catch (YamlException ex)
            {
                Assert.Equal(0, ex.LineNumber);
                Assert.Equal(3, ex.BytePositionInLine);
                Assert.Equal("$", ex.Path);
            }

            Assert.Equal(YamlTokenType.EndObject, reader.TokenType);

            reader.Read();
            Assert.Equal(YamlTokenType.EndArray, reader.TokenType);

            try
            {
                YamlSerializer.Deserialize<SimpleTypeWithArray>(ref reader);
                Assert.Fail("Expected ReadValue to throw YamlException for invalid token.");
            }
            catch (YamlException ex)
            {
                Assert.Equal(0, ex.LineNumber);
                Assert.Equal(4, ex.BytePositionInLine);
                Assert.Equal("$", ex.Path);
            }

            Assert.Equal(YamlTokenType.EndArray, reader.TokenType);
        }

        public class SimpleTypeWithArray
        {
            public int[] Foo { get; set; }
        }

        [Theory]
        [InlineData("1234", typeof(int), 1234)]
        [InlineData("null", typeof(string), null)]
        [InlineData("true", typeof(bool), true)]
        [InlineData("false", typeof(bool), false)]
        [InlineData("\"my string\"", typeof(string), "my string")]
        public static void Primitives(string yamlString, Type type, object? expected)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);

            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            Assert.Equal(YamlTokenType.None, reader.TokenType);

            object obj = YamlSerializer.Deserialize(ref reader, type);
            Assert.False(reader.HasValueSequence);
            Assert.Equal(utf8.Length, reader.BytesConsumed);
            Assert.Equal(expected, obj);

            Assert.False(reader.Read());
        }

        [Theory]
        [InlineData("1234", typeof(int), 1234)]
        [InlineData("null", typeof(string), null)]
        [InlineData("true", typeof(bool), true)]
        [InlineData("false", typeof(bool), false)]
        [InlineData("\"my string\"", typeof(string), "my string")]
        public static void PrimitivesMultiSegment(string yamlString, Type type, object? expected)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yamlString);
            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8);

            var reader = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            Assert.Equal(YamlTokenType.None, reader.TokenType);

            object obj = YamlSerializer.Deserialize(ref reader, type);
            Assert.True(reader.HasValueSequence);
            Assert.Equal(utf8.Length, reader.BytesConsumed);
            Assert.Equal(expected, obj);

            Assert.False(reader.Read());
        }

        [Fact]
        public static void EnableComments()
        {
            string yaml = "3";

            var options = new YamlReaderOptions
            {
                CommentHandling = YamlCommentHandling.Allow,
            };

            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            AssertExtensions.Throws<ArgumentException>(
                "reader",
                () =>
                {
                    var reader = new Utf8YamlReader(utf8, options);
                    YamlSerializer.Deserialize(ref reader, typeof(int));
                });

            AssertExtensions.Throws<ArgumentException>(
                "reader",
                () =>
                {
                    var state = new YamlReaderState(options);
                    var reader = new Utf8YamlReader(utf8, isFinalBlock: false, state);
                    YamlSerializer.Deserialize(ref reader, typeof(int));
                });

            AssertExtensions.Throws<ArgumentException>(
                "reader",
                () =>
                {
                    var state = new YamlReaderState(options);
                    var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                    YamlSerializer.Deserialize(ref reader, typeof(int));
                });

            AssertExtensions.Throws<ArgumentException>(
               "reader",
               () =>
               {
                   var state = new YamlReaderState(options);
                   var reader = new Utf8YamlReader(utf8, isFinalBlock: false, state);
                   YamlSerializer.Deserialize<int>(ref reader);
               });

            AssertExtensions.Throws<ArgumentException>(
                "reader",
                () =>
                {
                    var state = new YamlReaderState(options);
                    var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state);
                    YamlSerializer.Deserialize<int>(ref reader);
                });
        }

        [Fact]
        public static void ReadDefaultReader()
        {
            Assert.ThrowsAny<YamlException>(() =>
            {
                Utf8YamlReader reader = default;
                YamlSerializer.Deserialize(ref reader, typeof(int));
            });

            Assert.ThrowsAny<YamlException>(() =>
            {
                Utf8YamlReader reader = default;
                YamlSerializer.Deserialize<int>(ref reader);
            });
        }

        [Fact]
        public static void ReadSimpleStruct()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(SimpleTestStruct.s_yaml);
            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            SimpleTestStruct testStruct = YamlSerializer.Deserialize<SimpleTestStruct>(ref reader);
            testStruct.Verify();

            reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            object obj = YamlSerializer.Deserialize(ref reader, typeof(SimpleTestStruct));
            ((SimpleTestStruct)obj).Verify();
        }

        [Fact]
        public static void ReadClasses()
        {
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(TestClassWithNestedObjectInner.s_yaml);
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
                TestClassWithNestedObjectInner testStruct = YamlSerializer.Deserialize<TestClassWithNestedObjectInner>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithNestedObjectInner));
                ((TestClassWithNestedObjectInner)obj).Verify();
            }

            {
                var reader = new Utf8YamlReader(TestClassWithNestedObjectOuter.s_data, isFinalBlock: true, state: default);
                TestClassWithNestedObjectOuter testStruct = YamlSerializer.Deserialize<TestClassWithNestedObjectOuter>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(TestClassWithNestedObjectOuter.s_data, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithNestedObjectOuter));
                ((TestClassWithNestedObjectOuter)obj).Verify();
            }

            {
                var reader = new Utf8YamlReader(TestClassWithObjectList.s_data, isFinalBlock: true, state: default);
                TestClassWithObjectList testStruct = YamlSerializer.Deserialize<TestClassWithObjectList>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(TestClassWithObjectList.s_data, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithObjectList));
                ((TestClassWithObjectList)obj).Verify();
            }

            {
                var reader = new Utf8YamlReader(TestClassWithObjectArray.s_data, isFinalBlock: true, state: default);
                TestClassWithObjectArray testStruct = YamlSerializer.Deserialize<TestClassWithObjectArray>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(TestClassWithObjectArray.s_data, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithObjectArray));
                ((TestClassWithObjectArray)obj).Verify();
            }

            {
                var reader = new Utf8YamlReader(TestClassWithObjectIEnumerableT.s_data, isFinalBlock: true, state: default);
                TestClassWithObjectIEnumerableT testStruct = YamlSerializer.Deserialize<TestClassWithObjectIEnumerableT>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(TestClassWithObjectIEnumerableT.s_data, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithObjectIEnumerableT));
                ((TestClassWithObjectIEnumerableT)obj).Verify();
            }

            {
                var reader = new Utf8YamlReader(TestClassWithStringToPrimitiveDictionary.s_data, isFinalBlock: true, state: default);
                TestClassWithStringToPrimitiveDictionary testStruct = YamlSerializer.Deserialize<TestClassWithStringToPrimitiveDictionary>(ref reader);
                testStruct.Verify();

                reader = new Utf8YamlReader(TestClassWithStringToPrimitiveDictionary.s_data, isFinalBlock: true, state: default);
                object obj = YamlSerializer.Deserialize(ref reader, typeof(TestClassWithStringToPrimitiveDictionary));
                ((TestClassWithStringToPrimitiveDictionary)obj).Verify();
            }
        }

        [Fact]
        public static void ReadPartial()
        {
            byte[] utf8 = "[1, 2, 3]"u8.ToArray();
            var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            int[] array = YamlSerializer.Deserialize<int[]>(ref reader);
            var expected = new int[3] { 1, 2, 3 };
            Assert.Equal(expected, array);

            reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            object obj = YamlSerializer.Deserialize(ref reader, typeof(int[]));
            Assert.Equal(expected, obj);

            reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            reader.Read();
            int number = YamlSerializer.Deserialize<int>(ref reader);
            Assert.Equal(1, number);

            reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
            reader.Read();
            reader.Read();
            obj = YamlSerializer.Deserialize(ref reader, typeof(int));
            Assert.Equal(1, obj);
        }

        [Theory]
        [InlineData("[")]
        [InlineData("[0")]
        [InlineData("[0,")]
        public static void TooLittleYamlForIntArray(string yaml)
        {
            byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>(yaml));
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int[]>(yamlBytes));
            Assert.Throws<YamlException>(() => YamlSerializer.DeserializeAsync<int[]>(new MemoryStream(yamlBytes)).Result);

            // Using a reader directly throws since it can't read full int[].
            Utf8YamlReader reader = new Utf8YamlReader(yamlBytes);
            try
            {
                YamlSerializer.Deserialize<int[]>(ref reader);
                Assert.Fail("Expected exception.");
            }
            catch (YamlException) { }

            Assert.Equal(0, reader.BytesConsumed);
        }

        // From https://github.com/dotnet/runtime/issues/882
        [Fact]
        public static void OptionsFlowToConverter()
        {
            var builder = new StringBuilder();
            builder.Append("{\"type\": \"array\", \"array\": ");

            for (int i = 0; i < 128; i++)
            {
                builder.Append("[");
            }
            builder.Append("1");
            for (int i = 0; i < 128; i++)
            {
                builder.Append("]");
            }
            builder.Append("}");

            string yaml = builder.ToString();
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            {
                var options = new YamlSerializerOptions();
                options.Converters.Add(new CustomConverter());

                Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<DeepArray>(yaml, options));
                Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<IContent>(yaml, options));
            }

            {
                var options = new YamlSerializerOptions { MaxDepth = 256 };
                options.Converters.Add(new CustomConverter());

                DeepArray direct = YamlSerializer.Deserialize<DeepArray>(yaml, options);
                Assert.Equal(1, direct.array.GetArrayLength());

                IContent custom = YamlSerializer.Deserialize<IContent>(yaml, options);
                Assert.True(custom is DeepArray);
                Assert.Equal(1, ((DeepArray)custom).array.GetArrayLength());
            }

            {
                var options = new YamlSerializerOptions();
                ReadAndValidate(utf8, options, new YamlReaderOptions { MaxDepth = 65 }, expectedThrow: true);
            }

            {
                var options = new YamlSerializerOptions { MaxDepth = 256 };
                ReadAndValidate(utf8, options, new YamlReaderOptions { MaxDepth = 65 }, expectedThrow: true);
            }

            {
                var options = new YamlSerializerOptions();
                ReadAndValidate(utf8, options, new YamlReaderOptions { MaxDepth = 256 }, expectedThrow: false);
            }

            static void ReadAndValidate(byte[] utf8, YamlSerializerOptions options, YamlReaderOptions readerOptions, bool expectedThrow)
            {
                options.Converters.Add(new CustomConverter());

                if (expectedThrow)
                {
                    Assert.Throws<YamlException>(() =>
                    {
                        var reader = new Utf8YamlReader(utf8, readerOptions);
                        YamlSerializer.Deserialize<DeepArray>(ref reader, options);
                    });

                    Assert.Throws<YamlException>(() =>
                    {
                        var reader = new Utf8YamlReader(utf8, readerOptions);
                        YamlSerializer.Deserialize<IContent>(ref reader, options);
                    });
                }
                else
                {
                    var reader = new Utf8YamlReader(utf8, readerOptions);
                    DeepArray direct = YamlSerializer.Deserialize<DeepArray>(ref reader, options);
                    Assert.Equal(1, direct.array.GetArrayLength());

                    reader = new Utf8YamlReader(utf8, readerOptions);
                    IContent custom = YamlSerializer.Deserialize<IContent>(ref reader, options);
                    Assert.True(custom is DeepArray);
                    Assert.Equal(1, ((DeepArray)custom).array.GetArrayLength());
                }
            }
        }

        [Fact]
        public static void ReadSimpleList_AllowMultipleValues_TrailingYaml()
        {
            var options = new YamlReaderOptions { AllowMultipleValues = true };
            ReadOnlySpan<byte> yaml = "[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]             null"u8;
            var reader = new Utf8YamlReader(yaml, options);

            List<int> result = YamlSerializer.Deserialize<List<int>>(ref reader);
            Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20], result);
        }

        [Fact]
        public static void ReadSimpleList_AllowMultipleValues_TrailingContent()
        {
            var options = new YamlReaderOptions { AllowMultipleValues = true };
            ReadOnlySpan<byte> yaml = "[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]             !!<NotYaml/>!!"u8;
            var reader = new Utf8YamlReader(yaml, options);

            List<int> result = YamlSerializer.Deserialize<List<int>>(ref reader);
            Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20], result);
        }

        [Fact]
        public static void ReaderPreservesPositionInfoMultiByteUtf8String()
        {
            // "😀葛🀄" occupies 11 bytes in UTF-8 (4 + 3 + 4),
            // so the closing quote sits at byte index 13 and BytePositionInLine after the token is 14.
            byte[] utf8 = Encoding.UTF8.GetBytes("[\"😀葛🀄\"]");

            YamlException ex = Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
                reader.Read();
                reader.Read();
                Assert.Equal(YamlTokenType.String, reader.TokenType);

                YamlSerializer.Deserialize<int>(ref reader);
            });

            Assert.Equal(0, ex.LineNumber);
            Assert.Equal(14, ex.BytePositionInLine);
        }

        [Theory]
        [InlineData("[\n  {\"key\":1}\n]", typeof(string), 1, 3)]
        [InlineData("[\n  [1, 2]\n]", typeof(string), 1, 3)]
        public static void ReaderPreservesPositionInfoMultiLineContainer(
            string yaml, Type deserializeType, long expectedLine, long expectedBytePosition)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            YamlException ex = Assert.Throws<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8, isFinalBlock: true, state: default);
                reader.Read();
                reader.Read();
                Assert.True(reader.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray);

                YamlSerializer.Deserialize(ref reader, deserializeType);
            });

            Assert.Equal(expectedLine, ex.LineNumber);
            Assert.Equal(expectedBytePosition, ex.BytePositionInLine);
        }

    }

    // From https://github.com/dotnet/runtime/issues/882
    public interface IContent { }

    public class DeepArray : IContent
    {
        public YamlElement array { get; set; }
    }

    public class CustomConverter : YamlConverter<IContent>
    {
        public override IContent Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Utf8YamlReader sideReader = reader;

            sideReader.Read();
            sideReader.Read();
            string type = sideReader.GetString();

            return type switch
            {
                "array" => YamlSerializer.Deserialize<DeepArray>(ref reader, options),
                _ => throw new YamlException()
            };
        }

        public override void Write(Utf8YamlWriter writer, IContent value, YamlSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "array");
            writer.WritePropertyName("array");
            YamlSerializer.Serialize(writer, ((DeepArray)value).array, options);
            writer.WriteEndObject();
        }
    }
}
