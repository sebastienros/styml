// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;
using StjException = System.Text.Json.JsonException;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Exit criterion 7: LineNumber / BytePositionInLine, which the JSON-equivalence trace does not cover.
    /// Both are 0-based. Reader errors point at the byte that is wrong; serializer errors point just after the token
    /// being converted, as in System.Text.Json.
    /// </summary>
    public class PositionTests
    {
        [Theory]
        // Quoted scalars: the opening quote, or the offending escape.
        [InlineData("key: \"unterminated\n", 0, 5)]
        [InlineData("a: \"\\q\"\n", 0, 4)]
        // Content after a complete node, on the same line.
        [InlineData("a: 'x' y\n", 0, 7)]
        [InlineData("a: b: c\n", 0, 4)]
        // Indentation: the first content byte of the offending line.
        [InlineData("a:\n  - 1\n  b: 2\n", 2, 2)]
        [InlineData("- 1\n- 2\nx: 3\n", 2, 0)]
        [InlineData("a: 1\nb: 2\n  c: 3\n", 2, 3)]
        [InlineData("a: |\n  x\n y\n", 2, 1)]
        [InlineData("a: 1\n\tb: 2\n", 1, 1)]
        // Properties and aliases: the indicator.
        [InlineData("a: *\n", 0, 3)]
        [InlineData("a: !<x y\n", 0, 3)]
        // Unclosed flow collections: the end of the input, after the last line break.
        [InlineData("a: 1\nb: [1, 2\n", 2, 0)]
        [InlineData("{a: 1\n", 1, 0)]
        [InlineData("a: 1\r\nb: [\r\n", 2, 0)]
        // A second document without AllowMultipleValues: its marker.
        [InlineData("--- a\n--- b\n", 1, 0)]
        public void ReaderErrorInBlockYaml(string yaml, long line, long bytePositionInLine)
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
                while (reader.Read())
                {
                }
            });

            Assert.Equal(line, e.LineNumber);
            Assert.Equal(bytePositionInLine, e.BytePositionInLine);
        }

        [Theory]
        [InlineData("key: \"unterminated\n", 0, 5)]
        [InlineData("a:\n  - 1\n  b: 2\n", 2, 2)]
        [InlineData("a: |\n  x\n y\n", 2, 1)]
        [InlineData("a: 1\nb: [1, 2\n", 2, 0)]
        public void ReaderErrorInBlockYaml_ByteByByte(string yaml, long line, long bytePositionInLine)
        {
            // Positions survive the reader being resumed through CurrentState at every byte.
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
            YamlException e = Assert.ThrowsAny<YamlException>(() =>
            {
                YamlReaderState state = default;
                int offset = 0;
                for (int end = 0; end <= utf8.Length; end++)
                {
                    var reader = new Utf8YamlReader(utf8.AsSpan(offset, end - offset), end == utf8.Length, state);
                    while (reader.Read())
                    {
                    }
                    offset += (int)reader.BytesConsumed;
                    state = reader.CurrentState;
                }
            });

            Assert.Equal(line, e.LineNumber);
            Assert.Equal(bytePositionInLine, e.BytePositionInLine);
        }

        public class Item
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public List<int>? List { get; set; }
            public Item? Child { get; set; }
        }

        [Theory]
        [InlineData("Name: x\nAge: abc\n", 1, 8, "$.Age")]
        [InlineData("Name: x\nAge: 1.5\n", 1, 8, "$.Age")]
        [InlineData("Child:\n  Age: [1]\n", 1, 8, "$.Child.Age")]
        [InlineData("List:\n- 1\n- x\n", 2, 3, "$.List[1]")]
        [InlineData("List:\n  - 1\n  - \"2\"\n", 2, 7, "$.List[1]")]
        [InlineData("Child:\n  List: {a: 1}\n", 1, 9, "$.Child.List")]
        // A block mapping starts with a zero-width token at its first key.
        [InlineData("Name:\n  a: 1\n", 1, 2, "$.Name")]
        // A block scalar ends after its last line break.
        [InlineData("Age: |\n  12\n", 2, 0, "$.Age")]
        public void SerializerErrorInBlockYaml(string yaml, long line, long bytePositionInLine, string path)
        {
            YamlException e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Item>(yaml));
            Assert.Equal(line, e.LineNumber);
            Assert.Equal(bytePositionInLine, e.BytePositionInLine);
            Assert.Equal(path, e.Path);
        }

        public static IEnumerable<object[]> TypeMismatchJson() => new[]
        {
            new object[] { "{\"Name\": \"x\", \"Age\": \"abc\"}" },
            new object[] { "{\"Age\": 1.5}" },
            new object[] { "{\"Age\": 99999999999}" },
            new object[] { "{\"Age\": true}" },
            new object[] { "{\"Name\": {\"a\": 1}}" },
            new object[] { "{\"List\": {\"a\": 1}}" },
            new object[] { "{\"List\": [1, 2, \"3\"]}" },
            new object[] { "{\"Child\": [1]}" },
            new object[] { "[1, 2]" },
            new object[] { "\"text\"" },
            new object[] { "{\n  \"Name\": \"x\",\n  \"Child\": {\n    \"Age\": \"old\"\n  }\n}" },
            new object[] { "{\r\n  \"List\": [\r\n    1,\r\n    null\r\n  ]\r\n}" },
            new object[] { "{\n\t\"Child\": {\n\t\t\"Child\": {\n\t\t\t\"List\": [\n\t\t\t\t1, 2,\n\t\t\t\t{\"x\": 1}\n\t\t\t]\n\t\t}\n\t}\n}" },
            new object[] { "{\"Name\": \"caf\\u00e9 \\ud83d\\ude00\", \"Age\": \"\\u0031\"}" },
            new object[] { "{\"Name\": \"\u00e9\u00e9\u00e9\",\n \"Age\": [\"\u00e9\"]}" },
        };

        [Fact]
        public void PlainNumberIntoStringIsNotAMismatch()
        {
            // Decision D2: a plain scalar is text whatever it resolves to, so {"Name": 12} is not an error under YAML.
            Assert.ThrowsAny<StjException>(() => StjSerializer.Deserialize<Item>("{\"Name\": 12}"));
            Assert.Equal("12", YamlSerializer.Deserialize<Item>("{\"Name\": 12}")!.Name);
        }

        [Theory]
        [MemberData(nameof(TypeMismatchJson))]
        public void SerializerErrorOnValidJson_MatchesSystemTextJson(string json)
        {
            StjException expected = Assert.ThrowsAny<StjException>(() => StjSerializer.Deserialize<Item>(json));
            YamlException actual = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Item>(json));
            AssertSamePosition(expected, actual);
        }

        [Theory]
        [MemberData(nameof(TypeMismatchJson))]
        public void SerializerErrorOnValidJson_FromStream_MatchesSystemTextJson(string json)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(json);
            StjException expected = Assert.ThrowsAny<StjException>(() => StjSerializer.Deserialize<Item>(new MemoryStream(utf8), new System.Text.Json.JsonSerializerOptions { DefaultBufferSize = 1 }));
            YamlException actual = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Item>(new MemoryStream(utf8), new YamlSerializerOptions { DefaultBufferSize = 1 }));
            AssertSamePosition(expected, actual);
        }

        // Readers scoped to one value (YamlSerializer.Deserialize<T>(ref reader)). The imported ReaderPreservesPositionInfo*
        // tests use mismatches that decision D2 absorbs (a number read as a string), so they need the JSON grammar; these
        // use mismatches that stay errors and compare with System.Text.Json's Deserialize<T>(ref Utf8JsonReader).
        public static IEnumerable<object[]> RefReaderMismatches() => new[]
        {
            // json, reads before the call, target type
            new object[] { "[\n    \"x\"\n]", 2, typeof(int) },
            new object[] { "[  \"x\"]", 2, typeof(int) },
            new object[] { "[null]", 2, typeof(int) },
            new object[] { "[true]", 2, typeof(int) },
            new object[] { "[{\"key\":1}]", 2, typeof(int) },
            new object[] { "[\n  {\"key\":1}\n]", 2, typeof(int) },
            new object[] { "[\n  [1, 2]\n]", 2, typeof(int) },
            new object[] { "\"x\"", 0, typeof(int) },
            new object[] { "{\"val\": \"x\"}", 2, typeof(int) },
            new object[] { "{\n  \"val\":\n  \"x\"\n}", 2, typeof(int) },
            new object[] { "{\"a\": {\"b\": [1, \"x\"]}}", 2, typeof(List<int>) },
            new object[] { "{\"a\": {\"b\": [1, \"x\"]}}", 3, typeof(Dictionary<string, List<int>>) },
            new object[] { "[\"\ud83d\ude00\u845b\"]", 2, typeof(int) },
        };

        [Theory]
        [MemberData(nameof(RefReaderMismatches))]
        public void SerializerErrorThroughScopedReader_MatchesSystemTextJson(string json, int reads, Type type)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(json);
            StjException expected = Assert.ThrowsAny<StjException>(() =>
            {
                var reader = new System.Text.Json.Utf8JsonReader(utf8);
                for (int i = 0; i < reads; i++)
                {
                    reader.Read();
                }
                StjSerializer.Deserialize(ref reader, type);
            });
            YamlException actual = Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(utf8);
                for (int i = 0; i < reads; i++)
                {
                    reader.Read();
                }
                YamlSerializer.Deserialize(ref reader, type);
            });
            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.BytePositionInLine, actual.BytePositionInLine);
        }

        [Theory]
        // Block YAML through a scoped reader: the position is just after the converted token, in the whole document.
        [InlineData("a:\n  - 1\n  - x\n", 2, typeof(List<int>), 2, 5)]
        [InlineData("a:\n  b: x\n", 2, typeof(Dictionary<string, int>), 1, 6)]
        [InlineData("- 1\n- b: x\n", 3, typeof(Dictionary<string, int>), 1, 6)]
        [InlineData("a: 'x'\n", 2, typeof(int), 0, 6)]
        public void SerializerErrorThroughScopedReader_BlockYaml(string yaml, int reads, Type type, long line, long bytePositionInLine)
        {
            YamlException e = Assert.ThrowsAny<YamlException>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));
                for (int i = 0; i < reads; i++)
                {
                    reader.Read();
                }
                YamlSerializer.Deserialize(ref reader, type);
            });
            Assert.Equal(line, e.LineNumber);
            Assert.Equal(bytePositionInLine, e.BytePositionInLine);
        }

        private static void AssertSamePosition(StjException expected, YamlException actual)
        {
            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.BytePositionInLine, actual.BytePositionInLine);
            Assert.Equal(expected.Path, actual.Path);
        }
    }
}
