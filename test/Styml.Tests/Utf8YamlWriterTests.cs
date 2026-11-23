// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XUnitExtensions;
using Newtonsoft.Json;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public partial class Utf8YamlWriterTests
    {
        private const int MaxExpansionFactorWhileEscaping = 6;
        private const int MaxEscapedTokenSize = 1_000_000_000;   // Max size for already escaped value.
        private const int MaxUnescapedTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping;  // 166_666_666 bytes

        private const string ExpectedIndentedCommentYamlOfArray = """
/*Comment at start of doc*/
/*Multiple comment line*/
[
  /*Comment as first array item*/
  /*Multiple comment line*/
  []
  /*Comment in the middle of array*/,
  {}
  /*Comment as the last array item*/
]
/*Comment at end of doc*/
""";

        private const string ExpectedNonIndentedCommentYamlOfArray = "/*Comment at start of doc*//*Multiple comment line*/[/*Comment as first array item*//*Multiple comment line*/[]/*Comment in the middle of array*/,{}/*Comment as the last array item*/]/*Comment at end of doc*/";

        private const string ExpectedIndentedCommentYamlOfObject = """
/*Comment at start of doc*/
/*Multiple comment line*/
{
  /*Comment before first object property*/
  /*Multiple comment line*/
  "property1": 
  /*Comment of string property value*/"stringValue",
  "property2": 
  /*Comment of object property value*/
  {}
  /*Comment in the middle of object*/,
  "property3": 
  /*Comment of array property value*/
  []
  /*Comment after the last property*/
}
/*Comment at end of doc*/
""";

        private const string ExpectedNonIndentedCommentYamlOfObject = """
/*Comment at start of doc*//*Multiple comment line*/{/*Comment before first object property*//*Multiple comment line*/"property1":/*Comment of string property value*/"stringValue","property2":/*Comment of object property value*/{}/*Comment in the middle of object*/,"property3":/*Comment of array property value*/[]/*Comment after the last property*/}/*Comment at end of doc*/
""";

        public static IEnumerable<object[]> YamlOptions_TestData() =>
            from options in YamlOptions()
            select new object[] { options };

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        public void NullCtor(YamlWriterOptions options)
        {
            Assert.Throws<ArgumentNullException>(() => new Utf8YamlWriter((Stream)null));
            Assert.Throws<ArgumentNullException>(() => new Utf8YamlWriter((IBufferWriter<byte>)null));
            Assert.Throws<ArgumentNullException>(() => new Utf8YamlWriter((Stream)null, options));
            Assert.Throws<ArgumentNullException>(() => new Utf8YamlWriter((IBufferWriter<byte>)null, options));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        public void CantWriteToNonWritableStream(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            stream.Dispose();

            Assert.Throws<ArgumentException>(() => new Utf8YamlWriter(stream));
            Assert.Throws<ArgumentException>(() => new Utf8YamlWriter(stream, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritingNullStringsWithCustomEscaping()
        {
            var writerOptions = new YamlWriterOptions();
            WriteNullStringsHelper(writerOptions);

            writerOptions = new YamlWriterOptions { Encoder = JavaScriptEncoder.Default };
            WriteNullStringsHelper(writerOptions);

            writerOptions = new YamlWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            WriteNullStringsHelper(writerOptions);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritingNullStringsWithBuggyJavascriptEncoder()
        {
            var writerOptions = new YamlWriterOptions { Encoder = new BuggyJavaScriptEncoder() };
            WriteNullStringsHelper(writerOptions);
        }

        private static void WriteNullStringsHelper(YamlWriterOptions writerOptions)
        {
            var output = new ArrayBufferWriter<byte>();
            string str = null;

            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(str);
            }
            YamlTestHelper.AssertContents("null", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(str.AsSpan());
            }
            YamlTestHelper.AssertContents("\"\"", output);

            byte[] utf8Str = null;
            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(utf8Str.AsSpan());
            }
            YamlTestHelper.AssertContents("\"\"", output);

            YamlEncodedText yamlText = YamlEncodedText.Encode(utf8Str.AsSpan());
            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(yamlText);
            }
            YamlTestHelper.AssertContents("\"\"", output);
        }

        public class BuggyJavaScriptEncoder : JavaScriptEncoder
        {
            public override int MaxOutputCharactersPerInputCharacter => throw new NotImplementedException();

            public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
            {
                // Access the text pointer even though it might be null and text length is 0.
                return unsafe(*text);
            }

            public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
            {
                numberOfCharactersWritten = 0;
                return false;
            }

            public override bool WillEncode(int unicodeScalar) => false;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritingStringsWithCustomEscaping()
        {
            var output = new ArrayBufferWriter<byte>();
            var writerOptions = new YamlWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStringValue("\u6D4B\u8A6611");
            }
            YamlTestHelper.AssertContents("\"\\u6D4B\\u8A6611\"", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue("\u6D4B\u8A6611");
            }
            YamlTestHelper.AssertContents("\"\u6D4B\u8A6611\"", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStringValue("\u00E9\"");
            }
            YamlTestHelper.AssertContents("\"\\u00E9\\u0022\"", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue("\u00E9\"");
            }
            YamlTestHelper.AssertContents("\"\u00E9\\\"\"", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStringValue("\u2020\"");
            }
            YamlTestHelper.AssertContents("\"\\u2020\\u0022\"", output);

            output.Clear();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue("\u2020\"");
            }
            YamlTestHelper.AssertContents("\"\u2020\\\"\"", output);
        }

        [Theory]
        [MemberData(nameof(EscapingTestData))]
        [Trait("category", "jsonoutput")]
        public void EscapingTestWhileWriting(char replacementChar, JavaScriptEncoder encoder, bool requiresEscaping)
        {
            var writerOptions = new YamlWriterOptions { Encoder = encoder };

            {
                ReadOnlyMemory<byte> written = WriteStringHelper(writerOptions, null);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringHelper(writerOptions, null);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteStringHelper(writerOptions, string.Empty);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringHelper(writerOptions, Array.Empty<byte>());
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteStringSegmentHelper(writerOptions, Array.Empty<char>());
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringSegmentHelper(writerOptions, Array.Empty<byte>());
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));
            }

            var random = new Random(42);
            for (int dataLength = 0; dataLength < 50; dataLength++)
            {
                char[] str = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    str[i] = (char)random.Next(97, 123);
                }
                string baseStr = new string(str);
                byte[] sourceUtf8 = Encoding.UTF8.GetBytes(baseStr);

                ReadOnlyMemory<byte> written = WriteStringHelper(writerOptions, baseStr);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                for (int i = 0; i < dataLength; i++)
                {
                    char[] changed = baseStr.ToCharArray();
                    changed[i] = replacementChar;
                    string newStr = new string(changed);
                    sourceUtf8 = Encoding.UTF8.GetBytes(newStr);

                    written = WriteStringHelper(writerOptions, newStr);
                    int escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(requiresEscaping ? (i + 1) : -1, escapedIndex);  // Account for the start quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(requiresEscaping ? (i + 1) : -1, escapedIndex);  // Account for the start quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(newStr.AsSpan(), writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(requiresEscaping ? (i + 1) : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(requiresEscaping ? (i + 1) : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf8);
                    }
                }

                if (dataLength != 0)
                {
                    char[] changed = baseStr.ToCharArray();
                    changed.AsSpan().Fill(replacementChar);
                    string newStr = new string(changed);
                    sourceUtf8 = Encoding.UTF8.GetBytes(newStr);

                    written = WriteStringHelper(writerOptions, newStr);
                    int escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(requiresEscaping ? 1 : -1, escapedIndex);  // Account for the start quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(requiresEscaping ? 1 : -1, escapedIndex);  // Account for the start quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(changed, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(requiresEscaping ? 1 : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(requiresEscaping ? 1 : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf8);
                    }
                }
            }
        }

        public static IEnumerable<object[]> EscapingTestData
        {
            get
            {
                return new List<object[]>
            {
                new object[] { 'a', null, false },              // ASCII not escaped
                new object[] { '\u001F', null, true },          // control character within single byte range
                new object[] { '\u2000', null, true },          // space character outside single byte range
                new object[] { '\u00A2', null, true },          // non-ASCII but < 255
                new object[] { '\uA686', null, true },          // non-ASCII above short.MaxValue
                new object[] { '\u6C49', null, true },          // non-ASCII from chinese alphabet - multibyte
                new object[] { '"', null, true },               // ASCII but must always be escaped in JSON
                new object[] { '\\', null, true },              // ASCII but must always be escaped in JSON
                new object[] { '<', null, true },               // ASCII but escaped by default
                new object[] { '>', null, true },               // ASCII but escaped by default
                new object[] { '&', null, true },               // ASCII but escaped by default
                new object[] { '`', null, true },               // ASCII but escaped by default
                new object[] { '\'', null, true },              // ASCII but escaped by default
                new object[] { '+', null, true },               // ASCII but escaped by default

                new object[] { 'a', JavaScriptEncoder.Default, false },
                new object[] { '\u001F', JavaScriptEncoder.Default, true },
                new object[] { '\u2000', JavaScriptEncoder.Default, true },
                new object[] { '\u00A2', JavaScriptEncoder.Default, true },
                new object[] { '\uA686', JavaScriptEncoder.Default, true },
                new object[] { '\u6C49', JavaScriptEncoder.Default, true },
                new object[] { '"', JavaScriptEncoder.Default, true },
                new object[] { '\\', JavaScriptEncoder.Default, true },
                new object[] { '<', JavaScriptEncoder.Default, true },
                new object[] { '>', JavaScriptEncoder.Default, true },
                new object[] { '&', JavaScriptEncoder.Default, true },
                new object[] { '`', JavaScriptEncoder.Default, true },
                new object[] { '\'', JavaScriptEncoder.Default, true },
                new object[] { '+', JavaScriptEncoder.Default, true },

                new object[] { 'a', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), false },
                new object[] { '\u001F', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\u2000', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\u00A2', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\uA686', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\u6C49', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '"', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\\', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '<', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '>', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '&', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '`', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '\'', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },
                new object[] { '+', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin), true },

                new object[] { 'a', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\u001F', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\u2000', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\u00A2', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\uA686', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\u6C49', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '"', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\\', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '<', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '>', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '&', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '`', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\'', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '+', JavaScriptEncoder.Create(UnicodeRanges.All), true },

                new object[] { 'a', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\u001F', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\u2000', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\u00A2', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\uA686', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\u6C49', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '"', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\\', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '<', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '>', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '&', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '`', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\'', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '+', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
            };
            }
        }

        [Theory]
        [MemberData(nameof(EscapingTestData_NonAscii))]
        public unsafe void WriteString_NonAscii(char replacementChar, JavaScriptEncoder encoder, bool requiresEscaping)
        {
            var writerOptions = new YamlWriterOptions { Encoder = encoder };
            var random = new Random(42);
            for (int dataLength = 1; dataLength < 50; dataLength++)
            {
                char[] str = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    str[i] = (char)random.Next(0x2E9B, 0x2EF4); // CJK Radicals Supplement characters
                }
                string baseStr = new string(str);
                byte[] sourceUtf8 = Encoding.UTF8.GetBytes(baseStr);

                ReadOnlyMemory<byte> written = WriteStringHelper(writerOptions, baseStr);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                if (dataLength < 10)
                {
                    SplitStringDataHelper(str, writerOptions, output =>
                    {
                        Assert.Equal(-1, output.WrittenSpan.IndexOf((byte)'\\'));
                    }, StringValueEncodingType.Utf16);

                    SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                    {
                        Assert.Equal(-1, output.WrittenSpan.IndexOf((byte)'\\'));
                    }, StringValueEncodingType.Utf8);
                }

                for (int i = 0; i < dataLength; i++)
                {
                    string source = baseStr.Insert(i, new string(replacementChar, 1));
                    sourceUtf8 = Encoding.UTF8.GetBytes(source);

                    written = WriteStringHelper(writerOptions, source);
                    int escapedIndex = written.Span.IndexOf((byte)'\\');
                    // Each CJK character expands to 3 utf-8 bytes.
                    Assert.Equal(requiresEscaping ? ((i * 3) + 1) : -1, escapedIndex);  // Account for the start quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    escapedIndex = written.Span.IndexOf((byte)'\\');
                    // Each CJK character expands to 3 utf-8 bytes.
                    Assert.Equal(requiresEscaping ? ((i * 3) + 1) : -1, escapedIndex);  // Account for the start quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(source.AsSpan(), writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            // Each CJK character expands to 3 utf-8 bytes.
                            Assert.Equal(requiresEscaping ? ((i * 3) + 1) : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            // Each CJK character expands to 3 utf-8 bytes.
                            Assert.Equal(requiresEscaping ? ((i * 3) + 1) : -1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf8);
                    }
                }
            }
        }

        public static IEnumerable<object[]> EscapingTestData_NonAscii
        {
            get
            {
                return new List<object[]>
            {
                new object[] { 'a', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\u001F', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\u2000', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\u00A2', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\uA686', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '\u6C49', JavaScriptEncoder.Create(UnicodeRanges.All), false },
                new object[] { '"', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\\', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '<', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '>', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '&', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '`', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '\'', JavaScriptEncoder.Create(UnicodeRanges.All), true },
                new object[] { '+', JavaScriptEncoder.Create(UnicodeRanges.All), true },

                new object[] { 'a', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\u001F', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\u2000', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\u00A2', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\uA686', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\u6C49', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '"', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '\\', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, true },
                new object[] { '<', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '>', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '&', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '`', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '\'', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
                new object[] { '+', JavaScriptEncoder.UnsafeRelaxedJsonEscaping, false },
            };
            }
        }

        [Theory]
        [MemberData(nameof(JavaScriptEncoders))]
        [Trait("category", "jsonoutput")]
        public void EscapingTestWhileWritingSurrogate(JavaScriptEncoder encoder)
        {
            char highSurrogate = '\uD801';
            char lowSurrogate = '\uDC37';
            var writerOptions = new YamlWriterOptions { Encoder = encoder };
            var random = new Random(42);
            for (int dataLength = 2; dataLength < 50; dataLength++)
            {
                char[] str = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    str[i] = (char)random.Next(97, 123);
                }
                string baseStr = new string(str);
                byte[] sourceUtf8 = Encoding.UTF8.GetBytes(baseStr);

                ReadOnlyMemory<byte> written = WriteStringHelper(writerOptions, baseStr);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                Assert.Equal(-1, written.Span.IndexOf((byte)'\\'));

                if (dataLength < 10)
                {
                    SplitStringDataHelper(str, writerOptions, output =>
                    {
                        Assert.Equal(-1, output.WrittenSpan.IndexOf((byte)'\\'));
                    }, StringValueEncodingType.Utf16);

                    SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                    {
                        Assert.Equal(-1, output.WrittenSpan.IndexOf((byte)'\\'));
                    }, StringValueEncodingType.Utf8);
                }

                for (int i = 0; i < dataLength - 1; i++)
                {
                    char[] changed = baseStr.ToCharArray();
                    changed[i] = highSurrogate;
                    changed[i + 1] = lowSurrogate;
                    string newStr = new string(changed);
                    sourceUtf8 = Encoding.UTF8.GetBytes(newStr);

                    written = WriteStringHelper(writerOptions, newStr);
                    int escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(i + 1, escapedIndex);  // Account for the start quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(i + 1, escapedIndex);  // Account for the start quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(changed, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(i + 1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(i + 1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf8);
                    }
                }

                {
                    char[] changed = baseStr.ToCharArray();

                    for (int i = 0; i < changed.Length - 1; i += 2)
                    {
                        changed[i] = highSurrogate;
                        changed[i + 1] = lowSurrogate;
                    }

                    string newStr = new string(changed);
                    sourceUtf8 = Encoding.UTF8.GetBytes(newStr);

                    written = WriteStringHelper(writerOptions, newStr);
                    int escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(1, escapedIndex);  // Account for the start quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    escapedIndex = written.Span.IndexOf((byte)'\\');
                    Assert.Equal(1, escapedIndex);  // Account for the start quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(changed, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            escapedIndex = output.WrittenSpan.IndexOf((byte)'\\');
                            Assert.Equal(1, escapedIndex);  // Account for the start quote
                        }, StringValueEncodingType.Utf8);
                    }
                }
            }
        }

        public static IEnumerable<object[]> JavaScriptEncoders
        {
            get
            {
                return new List<object[]>
            {
                new object[] { null },
                new object[] { JavaScriptEncoder.Default },
                new object[] { JavaScriptEncoder.Create(UnicodeRanges.BasicLatin) },
                new object[] { JavaScriptEncoder.Create(UnicodeRanges.All) },
                new object[] { JavaScriptEncoder.UnsafeRelaxedJsonEscaping },
            };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidEscapingTestData))]
        [Trait("category", "jsonoutput")]
        public unsafe void WriteStringInvalidCharacter(char replacementChar, JavaScriptEncoder encoder)
        {
            var writerOptions = new YamlWriterOptions { Encoder = encoder };
            var random = new Random(42);
            for (int dataLength = 0; dataLength < 47; dataLength++)
            {
                char[] str = new char[dataLength];
                for (int i = 0; i < dataLength; i++)
                {
                    str[i] = (char)random.Next(97, 123);
                }
                string baseStr = new string(str);
                byte[] baseStrUtf8 = Encoding.UTF8.GetBytes(baseStr);

                for (int i = 0; i < dataLength; i++)
                {
                    char[] changed = baseStr.ToCharArray();
                    changed[i] = replacementChar;
                    string source = new string(changed);
                    byte[] sourceUtf8 = new byte[baseStrUtf8.Length];
                    baseStrUtf8.AsSpan().CopyTo(sourceUtf8);
                    sourceUtf8[i] = 0xC3;   // Invalid, first byte of a 2-byte utf-8 character

                    ReadOnlyMemory<byte> written = WriteStringHelper(writerOptions, source);
                    Assert.True(BeginsWithReplacementCharacter(written.Span.Slice(i + 1))); // +1 to account for starting quote

                    written = WriteUtf8StringHelper(writerOptions, sourceUtf8);
                    Assert.True(BeginsWithReplacementCharacter(written.Span.Slice(i + 1))); // +1 to account for starting quote

                    if (dataLength < 10)
                    {
                        SplitStringDataHelper(changed, writerOptions, output =>
                        {
                            Assert.True(BeginsWithReplacementCharacter(output.WrittenSpan.Slice(i + 1))); // +1 to account for starting quote
                        }, StringValueEncodingType.Utf16);

                        SplitStringDataHelper(sourceUtf8, writerOptions, output =>
                        {
                            Assert.True(BeginsWithReplacementCharacter(output.WrittenSpan.Slice(i + 1))); // +1 to account for starting quote
                        }, StringValueEncodingType.Utf8);
                    }
                }
            }

            static bool BeginsWithReplacementCharacter(ReadOnlySpan<byte> span)
            {
                // Returns true if the span contains the literal UTF-8 representation of the U+FFFD replacement
                // character or the "\uFFFD" JSON-escaped representation of the replacement character.
                // Account for the fact that an encoder might write a literal replacement character or its
                // escaped representation, and both forms are equally valid.

                if (span.StartsWith("\uFFFD"u8)) { return true; }
                if (span.Length >= 6)
                {
                    if (span[0] == (byte)'\\' && span[1] == (byte)'u'
                        && (span[2] == 'F' || span[2] == 'f')
                        && (span[3] == 'F' || span[3] == 'f')
                        && (span[4] == 'F' || span[4] == 'f')
                        && (span[5] == 'D' || span[5] == 'd'))
                    {
                        return true; // "\uFFFD" representation
                    }
                }
                return false; // unknown
            }
        }

        public static IEnumerable<object[]> InvalidEscapingTestData
        {
            get
            {
                return new List<object[]>
            {
                new object[] { '\uD801', JavaScriptEncoder.Default },         // Invalid, high surrogate alone
                new object[] { '\uDC01', JavaScriptEncoder.Default },         // Invalid, low surrogate alone

                new object[] { '\uD801', JavaScriptEncoder.UnsafeRelaxedJsonEscaping },
                new object[] { '\uDC01', JavaScriptEncoder.UnsafeRelaxedJsonEscaping },

                new object[] { '\uD801', JavaScriptEncoder.Create(UnicodeRanges.All) },
                new object[] { '\uDC01', JavaScriptEncoder.Create(UnicodeRanges.All) },

                new object[] { '\uD801', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin) },
                new object[] { '\uDC01', JavaScriptEncoder.Create(UnicodeRanges.BasicLatin) },
            };
            }
        }

        private static ReadOnlyMemory<byte> WriteStringHelper(YamlWriterOptions writerOptions, string str)
        {
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(str);
            }
            return output.WrittenMemory;
        }

        private static ReadOnlyMemory<byte> WriteUtf8StringHelper(YamlWriterOptions writerOptions, byte[] utf8str)
        {
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValue(utf8str);
            }
            return output.WrittenMemory;
        }

        private static ReadOnlyMemory<byte> WriteStringSegmentHelper(YamlWriterOptions writerOptions, ReadOnlySpan<char> str)
        {
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValueSegment(str, true);
            }
            return output.WrittenMemory;
        }

        private static ReadOnlyMemory<byte> WriteUtf8StringSegmentHelper(YamlWriterOptions writerOptions, ReadOnlySpan<byte> utf8str)
        {
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8YamlWriter(output, writerOptions))
            {
                writer.WriteStringValueSegment(utf8str, true);
            }
            return output.WrittenMemory;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteYamlWritesToIBWOnDemand_Dispose()
        {
            var output = new ArrayBufferWriter<byte>();
            var writer = new Utf8YamlWriter(output);
            WriteLargeArrayOfStrings(writer, 1_000);
            Assert.Equal(17347, writer.BytesCommitted);
            Assert.Equal(5544, writer.BytesPending);
            Assert.Equal(0, writer.CurrentDepth);
            Assert.Equal(17347, output.WrittenCount);

            writer.Dispose();

            Assert.Equal(0, writer.BytesPending);
            Assert.Equal(22891, output.WrittenCount);
            YamlTestHelper.AssertContents(GetExpectedLargeArrayOfStrings(1_000), output);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteYamlOnlyWritesToStreamOnDemand_Dispose()
        {
            string expectedString = GetExpectedLargeArrayOfStrings(1_000);
            Assert.Equal(22891, expectedString.Length);
            using (var stream = new MemoryStream())
            {
                var writer = new Utf8YamlWriter(stream);
                WriteLargeArrayOfStrings(writer, 1_000);
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(expectedString.Length, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                // Unless user disposes the writer or calls flush/flushasync explicitly, nothing gets written to the stream
                Assert.Equal(0, stream.Position);

                writer.Dispose();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);
                YamlTestHelper.AssertContents(expectedString, stream);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteYamlOnlyWritesToStreamOnDemand_DisposeAsync()
        {
            string expectedString = GetExpectedLargeArrayOfStrings(1_000);
            Assert.Equal(22891, expectedString.Length);
            using (var stream = new MemoryStream())
            {
                var writer = new Utf8YamlWriter(stream);
                WriteLargeArrayOfStrings(writer, 1_000);
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(expectedString.Length, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                // Unless user disposes the writer or calls flush/flushasync explicitly, nothing gets written to the stream
                Assert.Equal(0, stream.Position);

                await writer.DisposeAsync();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);
                YamlTestHelper.AssertContents(expectedString, stream);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteYamlOnlyWritesToStreamOnDemand_Flush()
        {
            string expectedString = GetExpectedLargeArrayOfStrings(1_000);
            Assert.Equal(22891, expectedString.Length);
            using (var stream = new MemoryStream())
            {
                var writer = new Utf8YamlWriter(stream);
                WriteLargeArrayOfStrings(writer, 1_000);
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(expectedString.Length, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                // Unless user disposes the writer or calls flush/flushasync explicitly, nothing gets written to the stream
                Assert.Equal(0, stream.Position);

                writer.Flush();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);

                writer.Dispose();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);
                YamlTestHelper.AssertContents(expectedString, stream);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteYamlOnlyWritesToStreamOnDemand_FlushAsync()
        {
            string expectedString = GetExpectedLargeArrayOfStrings(1_000);
            Assert.Equal(22891, expectedString.Length);
            using (var stream = new MemoryStream())
            {
                var writer = new Utf8YamlWriter(stream);
                WriteLargeArrayOfStrings(writer, 1_000);
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(expectedString.Length, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                // Unless user disposes the writer or calls flush/flushasync explicitly, nothing gets written to the stream
                Assert.Equal(0, stream.Position);

                await writer.FlushAsync();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);

                await writer.DisposeAsync();

                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(expectedString.Length, stream.Position);
                YamlTestHelper.AssertContents(expectedString, stream);
            }
        }

        private static void WriteLargeArrayOfStrings(Utf8YamlWriter writer, int length)
        {
            writer.WriteStartArray();
            for (int i = 0; i < length; i++)
            {
                writer.WriteStringValue($"some array value {i}");
            }
            writer.WriteEndArray();
        }

        private static string GetExpectedLargeArrayOfStrings(int length)
        {
            var stringBuilder = new StringBuilder();
            using (TextWriter stringWriter = new StringWriter(stringBuilder))
            using (var yaml = new JsonTextWriter(stringWriter))
            {
                yaml.Formatting = Formatting.None;
                yaml.WriteStartArray();
                for (int i = 0; i < length; i++)
                {
                    yaml.WriteValue($"some array value {i}");
                }
                yaml.WriteEnd();
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// This test is constrained to run on Windows and MacOSX because it causes
        /// problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        /// succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        /// time the memory is accessed which triggers the full memory allocation.
        /// Also see <see cref="WriteRawLargeYamlToStreamWithoutFlushing"/>
        /// </summary>
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        public void WriteLargeYamlToStreamWithoutFlushing()
        {
            try
            {
                var largeArray = new char[150_000_000];
                largeArray.AsSpan().Fill('a');

                // Text size chosen so that after several doublings of the underlying buffer we reach ~2 GB (but don't go over)
                YamlEncodedText text1 = YamlEncodedText.Encode(largeArray.AsSpan(0, 7_500));
                YamlEncodedText text2 = YamlEncodedText.Encode(largeArray.AsSpan(0, 5_000));
                YamlEncodedText text3 = YamlEncodedText.Encode(largeArray.AsSpan(0, 150_000_000));

                using (var output = new MemoryStream())
                using (var writer = new Utf8YamlWriter(output))
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(text1);
                    Assert.Equal(7_503, writer.BytesPending);

                    for (int i = 0; i < 30_000; i++)
                    {
                        writer.WriteStringValue(text2);
                    }
                    Assert.Equal(150_097_503, writer.BytesPending);

                    for (int i = 0; i < 13; i++)
                    {
                        writer.WriteStringValue(text3);
                    }
                    Assert.Equal(2_100_097_542, writer.BytesPending);

                    // Next write forces a grow beyond max array length

                    Assert.Throws<OutOfMemoryException>(() => writer.WriteStringValue(text3));

                    Assert.Equal(2_100_097_542, writer.BytesPending);

                    var text4 = YamlEncodedText.Encode(largeArray.AsSpan(0, 1));
                    for (int i = 0; i < 10_000_000; i++)
                    {
                        writer.WriteStringValue(text4);
                    }

                    Assert.Equal(2_100_097_542 + (4 * 10_000_000), writer.BytesPending);
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InitialState(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            using (var writer = new Utf8YamlWriter(stream, options))
            {
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                Assert.Null(writer.Options.Encoder);
                Assert.Equal(options.Indented, writer.Options.Indented);
                Assert.Equal(options.SkipValidation, writer.Options.SkipValidation);
                Assert.Equal(0, stream.Position);
            }

            var output = new FixedSizedBufferWriter(0);
            using (var writer = new Utf8YamlWriter(output, options))
            {
                Assert.Equal(0, writer.BytesCommitted);
                Assert.Equal(0, writer.BytesPending);
                Assert.Equal(0, writer.CurrentDepth);
                Assert.Null(writer.Options.Encoder);
                Assert.Equal(options.Indented, writer.Options.Indented);
                Assert.Equal(options.SkipValidation, writer.Options.SkipValidation);
                Assert.Equal(0, output.FormattedCount);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void Reset(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);

            writeToStream.Reset();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(0, writeToStream.CurrentDepth);
            Assert.Null(writeToStream.Options.Encoder);
            Assert.Equal(options.Indented, writeToStream.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToStream.Options.SkipValidation);
            Assert.True(stream.Position != 0);

            long previousWritten = stream.Position;
            writeToStream.Flush();
            Assert.Equal(previousWritten, stream.Position);

            var output = new FixedSizedBufferWriter(256);
            using var writeToIBW = new Utf8YamlWriter(output, options);
            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);

            writeToIBW.Reset();
            Assert.Equal(0, writeToIBW.BytesCommitted);
            Assert.Equal(0, writeToIBW.BytesPending);
            Assert.Equal(0, writeToIBW.CurrentDepth);
            Assert.Null(writeToIBW.Options.Encoder);
            Assert.Equal(options.Indented, writeToIBW.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToIBW.Options.SkipValidation);
            Assert.True(output.FormattedCount != 0);

            previousWritten = output.FormattedCount;
            writeToIBW.Flush();
            Assert.Equal(previousWritten, output.FormattedCount);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void ResetWithSameOutput(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);

            writeToStream.Reset(stream);
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(0, writeToStream.CurrentDepth);
            Assert.Null(writeToStream.Options.Encoder);
            Assert.Equal(options.Indented, writeToStream.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToStream.Options.SkipValidation);
            Assert.True(stream.Position != 0);

            long previousWritten = stream.Position;
            writeToStream.Flush();
            Assert.Equal(previousWritten, stream.Position);

            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.NotEqual(previousWritten, stream.Position);
            Assert.Equal("11", Encoding.UTF8.GetString(stream.ToArray()));

            var output = new FixedSizedBufferWriter(257);
            using var writeToIBW = new Utf8YamlWriter(output, options);
            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);

            writeToIBW.Reset(output);
            Assert.Equal(0, writeToIBW.BytesCommitted);
            Assert.Equal(0, writeToIBW.BytesPending);
            Assert.Equal(0, writeToIBW.CurrentDepth);
            Assert.Null(writeToIBW.Options.Encoder);
            Assert.Equal(options.Indented, writeToIBW.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToIBW.Options.SkipValidation);
            Assert.True(output.FormattedCount != 0);

            previousWritten = output.FormattedCount;
            writeToIBW.Flush();
            Assert.Equal(previousWritten, output.FormattedCount);

            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.NotEqual(previousWritten, output.FormattedCount);
            Assert.Equal("11", Encoding.UTF8.GetString(output.Formatted));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void ResetChangeOutputMode(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);

            var output = new FixedSizedBufferWriter(256);
            writeToStream.Reset(output);
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(0, writeToStream.CurrentDepth);
            Assert.Null(writeToStream.Options.Encoder);
            Assert.Equal(options.Indented, writeToStream.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToStream.Options.SkipValidation);
            Assert.True(stream.Position != 0);

            long previousWrittenStream = stream.Position;
            long previousWrittenIBW = output.FormattedCount;
            Assert.Equal(0, previousWrittenIBW);
            writeToStream.Flush();
            Assert.Equal(previousWrittenStream, stream.Position);
            Assert.Equal(previousWrittenIBW, output.FormattedCount);

            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);
            Assert.Equal(previousWrittenStream, stream.Position);
            Assert.True(output.FormattedCount != 0);

            output = new FixedSizedBufferWriter(256);
            using var writeToIBW = new Utf8YamlWriter(output, options);
            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);

            stream = new MemoryStream();
            writeToIBW.Reset(stream);
            Assert.Equal(0, writeToIBW.BytesCommitted);
            Assert.Equal(0, writeToIBW.BytesPending);
            Assert.Equal(0, writeToIBW.CurrentDepth);
            Assert.Null(writeToIBW.Options.Encoder);
            Assert.Equal(options.Indented, writeToIBW.Options.Indented);
            Assert.Equal(options.SkipValidation, writeToIBW.Options.SkipValidation);
            Assert.True(output.FormattedCount != 0);

            previousWrittenStream = stream.Position;
            previousWrittenIBW = output.FormattedCount;
            Assert.Equal(0, previousWrittenStream);
            writeToIBW.Flush();
            Assert.Equal(previousWrittenStream, stream.Position);
            Assert.Equal(previousWrittenIBW, output.FormattedCount);

            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);
            Assert.Equal(previousWrittenIBW, output.FormattedCount);
            Assert.True(stream.Position != 0);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidReset(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);

            Assert.Throws<ArgumentNullException>(() => writeToStream.Reset((Stream)null));
            Assert.Throws<ArgumentNullException>(() => writeToStream.Reset((IBufferWriter<byte>)null));
            Assert.Throws<ArgumentNullException>(() => writeToStream.Reset((Stream)null, options));
            Assert.Throws<ArgumentNullException>(() => writeToStream.Reset((IBufferWriter<byte>)null, options));

            stream.Dispose();

            Assert.Throws<ArgumentException>(() => writeToStream.Reset(stream));
            Assert.Throws<ArgumentException>(() => writeToStream.Reset(stream, options));

            var output = new FixedSizedBufferWriter(256);
            using var writeToIBW = new Utf8YamlWriter(output, options);

            Assert.Throws<ArgumentNullException>(() => writeToIBW.Reset((Stream)null));
            Assert.Throws<ArgumentNullException>(() => writeToIBW.Reset((IBufferWriter<byte>)null));
            Assert.Throws<ArgumentNullException>(() => writeToIBW.Reset((Stream)null, options));
            Assert.Throws<ArgumentNullException>(() => writeToIBW.Reset((IBufferWriter<byte>)null, options));

            Assert.Throws<ArgumentException>(() => writeToIBW.Reset(stream));
            Assert.Throws<ArgumentException>(() => writeToIBW.Reset(stream, options));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlSerializerCacheNotPoisonedByDisposedWriter()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new DisposingInt32Converter());

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(1, options));
            Assert.Equal("2", YamlSerializer.Serialize(2));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void ResetWithNewOptions(YamlWriterOptions options)
        {
            var newOptions = new YamlWriterOptions
            {
                Indented = !options.Indented,
                SkipValidation = !options.SkipValidation,
                MaxDepth = 500,
                IndentCharacter = '\t',
                IndentSize = 4,
            };

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);

            writeToStream.Reset(stream, newOptions);
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(0, writeToStream.CurrentDepth);
            Assert.Equal(newOptions.Indented, writeToStream.Options.Indented);
            Assert.Equal(newOptions.SkipValidation, writeToStream.Options.SkipValidation);
            Assert.Equal(500, writeToStream.Options.MaxDepth);
            Assert.Equal('\t', writeToStream.Options.IndentCharacter);
            Assert.Equal(4, writeToStream.Options.IndentSize);

            long previousWritten = stream.Position;
            writeToStream.Flush();
            Assert.Equal(previousWritten, stream.Position);

            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.NotEqual(previousWritten, stream.Position);

            var output = new FixedSizedBufferWriter(257);
            using var writeToIBW = new Utf8YamlWriter(output, options);
            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);

            writeToIBW.Reset(output, newOptions);
            Assert.Equal(0, writeToIBW.BytesCommitted);
            Assert.Equal(0, writeToIBW.BytesPending);
            Assert.Equal(0, writeToIBW.CurrentDepth);
            Assert.Equal(newOptions.Indented, writeToIBW.Options.Indented);
            Assert.Equal(newOptions.SkipValidation, writeToIBW.Options.SkipValidation);
            Assert.Equal(500, writeToIBW.Options.MaxDepth);
            Assert.Equal('\t', writeToIBW.Options.IndentCharacter);
            Assert.Equal(4, writeToIBW.Options.IndentSize);

            previousWritten = output.FormattedCount;
            writeToIBW.Flush();
            Assert.Equal(previousWritten, output.FormattedCount);

            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.NotEqual(previousWritten, output.FormattedCount);
        }

        private sealed class DisposingInt32Converter : System.Text.Yaml.Serialization.YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                throw new NotSupportedException();

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                writer.Dispose();
                throw new InvalidOperationException();
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void ResetChangeOutputModeWithNewOptions(YamlWriterOptions options)
        {
            var newOptions = new YamlWriterOptions
            {
                Indented = !options.Indented,
                SkipValidation = !options.SkipValidation,
            };

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();

            Assert.True(writeToStream.BytesCommitted != 0);

            var output = new FixedSizedBufferWriter(256);
            writeToStream.Reset(output, newOptions);
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(0, writeToStream.CurrentDepth);
            Assert.Equal(newOptions.Indented, writeToStream.Options.Indented);
            Assert.Equal(newOptions.SkipValidation, writeToStream.Options.SkipValidation);

            writeToStream.WriteNumberValue(1);
            writeToStream.Flush();
            Assert.True(writeToStream.BytesCommitted != 0);
            Assert.True(output.FormattedCount != 0);

            output = new FixedSizedBufferWriter(256);
            using var writeToIBW = new Utf8YamlWriter(output, options);
            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();

            Assert.True(writeToIBW.BytesCommitted != 0);

            stream = new MemoryStream();
            writeToIBW.Reset(stream, newOptions);
            Assert.Equal(0, writeToIBW.BytesCommitted);
            Assert.Equal(0, writeToIBW.BytesPending);
            Assert.Equal(0, writeToIBW.CurrentDepth);
            Assert.Equal(newOptions.Indented, writeToIBW.Options.Indented);
            Assert.Equal(newOptions.SkipValidation, writeToIBW.Options.SkipValidation);

            writeToIBW.WriteNumberValue(1);
            writeToIBW.Flush();
            Assert.True(writeToIBW.BytesCommitted != 0);
            Assert.True(stream.Position != 0);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void ResetWithNewOptions_ProducesExpectedOutput()
        {
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, new YamlWriterOptions { Indented = false });

            writer.WriteStartObject();
            writer.WriteNumber("x", 1);
            writer.WriteEndObject();
            writer.Flush();

            string compact = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Equal("""{"x":1}""", compact);

            output.Clear();
            writer.Reset(output, new YamlWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteNumber("x", 1);
            writer.WriteEndObject();
            writer.Flush();

            string indented = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("\n", indented);
            Assert.Contains("\"x\": 1", indented);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FlushEmpty(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(0);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.Flush();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.Flush();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task FlushEmptyAsync(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(0);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            await yamlUtf8.FlushAsync();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            await writeToStream.FlushAsync();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FlushMultipleTimes(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteEndObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(2, yamlUtf8.BytesPending);
            Assert.Equal(0, output.FormattedCount);
            yamlUtf8.Flush();
            Assert.Equal(2, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(2, output.FormattedCount);
            yamlUtf8.Flush();
            Assert.Equal(2, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(2, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            writeToStream.WriteEndObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(2, writeToStream.BytesPending);
            Assert.Equal(0, stream.Position);
            writeToStream.Flush();
            Assert.Equal(2, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(2, stream.Position);
            writeToStream.Flush();
            Assert.Equal(2, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(2, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task FlushMultipleTimesAsync(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteEndObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(2, yamlUtf8.BytesPending);
            Assert.Equal(0, output.FormattedCount);
            await yamlUtf8.FlushAsync();
            Assert.Equal(2, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(2, output.FormattedCount);
            await yamlUtf8.FlushAsync();
            Assert.Equal(2, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(2, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            writeToStream.WriteEndObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(2, writeToStream.BytesPending);
            Assert.Equal(0, stream.Position);
            await writeToStream.FlushAsync();
            Assert.Equal(2, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(2, stream.Position);
            await writeToStream.FlushAsync();
            Assert.Equal(2, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(2, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void DisposeAutoFlushes(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteEndObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, output.FormattedCount);
            yamlUtf8.Dispose();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(2, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            writeToStream.WriteEndObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, stream.Position);
            writeToStream.Dispose();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(2, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task DisposeAutoFlushesAsync(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteEndObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, output.FormattedCount);
            await yamlUtf8.DisposeAsync();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(2, output.FormattedCount);

            var stream = new MemoryStream();
            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            writeToStream.WriteEndObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, stream.Position);
            await writeToStream.DisposeAsync();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(2, stream.Position);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void UseAfterDisposeInvalid(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(1, yamlUtf8.BytesPending);
            Assert.Equal(0, output.FormattedCount);
            yamlUtf8.Dispose();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(1, output.FormattedCount);
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Flush());
            yamlUtf8.Dispose();
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Flush());

            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset());

            var stream = new MemoryStream();
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(stream));
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(stream, options));

            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(1, writeToStream.BytesPending);
            Assert.Equal(0, stream.Position);
            writeToStream.Dispose();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(1, stream.Position);
            Assert.Throws<ObjectDisposedException>(() => writeToStream.Flush());
            writeToStream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => writeToStream.Flush());

            Assert.Throws<ObjectDisposedException>(() => writeToStream.Reset());

            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(output));
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(output, options));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task UseAfterDisposeInvalidAsync(YamlWriterOptions options)
        {
            var output = new FixedSizedBufferWriter(256);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(1, yamlUtf8.BytesPending);
            Assert.Equal(0, output.FormattedCount);
            await yamlUtf8.DisposeAsync();
            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);
            Assert.Equal(1, output.FormattedCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => yamlUtf8.FlushAsync());
            await yamlUtf8.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => yamlUtf8.FlushAsync());

            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset());

            var stream = new MemoryStream();
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(stream));
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(stream, options));

            using var writeToStream = new Utf8YamlWriter(stream, options);
            writeToStream.WriteStartObject();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(1, writeToStream.BytesPending);
            Assert.Equal(0, stream.Position);
            await writeToStream.DisposeAsync();
            Assert.Equal(0, writeToStream.BytesCommitted);
            Assert.Equal(0, writeToStream.BytesPending);
            Assert.Equal(1, stream.Position);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => writeToStream.FlushAsync());
            await writeToStream.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => writeToStream.FlushAsync());

            Assert.Throws<ObjectDisposedException>(() => writeToStream.Reset());

            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(output));
            Assert.Throws<ObjectDisposedException>(() => yamlUtf8.Reset(output, options));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("category", "jsonoutput")]
        public async Task FlushToStreamThrows_WriterRemainsInConsistentState(bool useAsync, bool throwFromDispose)
        {
            var stream = new ThrowingFromWriteMemoryStream();
            var yamlUtf8 = new Utf8YamlWriter(stream);

            // Write and flush some of an object.
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteString("someProp1", "someValue1");
            await yamlUtf8.FlushAsync(useAsync);

            // Write some more, but fail while flushing to write to the underlying stream.
            stream.ExceptionToThrow = new FormatException("uh oh");
            yamlUtf8.WriteString("someProp2", "someValue2");
            Assert.Same(stream.ExceptionToThrow, await Assert.ThrowsAsync<FormatException>(() => yamlUtf8.FlushAsync(useAsync)));

            // Write some more.
            yamlUtf8.WriteEndObject();

            // Dispose, potentially throwing from the subsequent attempt to flush.
            if (throwFromDispose)
            {
                // Disposing should propagate the new exception
                stream.ExceptionToThrow = new FormatException("uh oh again");
                Assert.Same(stream.ExceptionToThrow, await Assert.ThrowsAsync<FormatException>(() => yamlUtf8.DisposeAsync(useAsync)));
                Assert.Equal("{\"someProp1\":\"someValue1\"", Encoding.UTF8.GetString(stream.ToArray()));
            }
            else
            {
                // Disposing should not fail.
                stream.ExceptionToThrow = null;
                await yamlUtf8.DisposeAsync(useAsync);
                Assert.Equal("{\"someProp1\":\"someValue1\",\"someProp2\":\"someValue2\"}", Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        private sealed class ThrowingFromWriteMemoryStream : MemoryStream
        {
            public Exception ExceptionToThrow { get; set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (ExceptionToThrow != null) throw ExceptionToThrow;
                base.Write(buffer, offset, count);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (ExceptionToThrow != null) throw ExceptionToThrow;
                await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidBufferWriter(YamlWriterOptions options)
        {
            var output = new InvalidBufferWriter();

            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteNumberValue((ulong)12345678901));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public async Task WriteLargeToStream(YamlWriterOptions options)
        {
            var stream = new MemoryStream();
            
            await WriteLargeToStreamHelper(stream, options);

            string expectedString = GetExpectedLargeString(options);
            string actualString = Encoding.UTF8.GetString(stream.ToArray());

            Assert.Equal(expectedString, actualString);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void GrowBeyondBufferSize(YamlWriterOptions options)
        {
            const int InitialGrowthSize = 256;
            var output = new FixedSizedBufferWriter(InitialGrowthSize);
            
            byte[] utf8String = "this is a string long enough to overflow the buffer and cause an exception to be thrown."u8.ToArray();

            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartArray();

            while (yamlUtf8.BytesPending < InitialGrowthSize - utf8String.Length)
            {
                yamlUtf8.WriteStringValue(utf8String);
            }

            Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue(utf8String));
        }

        private static async Task WriteLargeToStreamHelper(Stream stream, YamlWriterOptions options)
        {
            const int SyncWriteThreshold = 25_000;

            await using var yamlUtf8 = new Utf8YamlWriter(stream, options);

            byte[] utf8String = "some string 1234"u8.ToArray();

            yamlUtf8.WriteStartArray();
            for (int i = 0; i < 10_000; i++)
            {
                yamlUtf8.WriteStringValue(utf8String);
                if (yamlUtf8.BytesPending > SyncWriteThreshold)
                {
                    await yamlUtf8.FlushAsync();
                }
            }
            yamlUtf8.WriteEndArray();
        }

        private static string GetExpectedLargeString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartArray();
            for (int i = 0; i < 10_000; i++)
            {
                yaml.WriteValue("some string 1234");
            }
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FixedSizeBufferWriter_Guid(YamlWriterOptions options)
        {
            int sizeTooSmall = options.Indented ? options.IndentSize + 229 : 225;
            sizeTooSmall = Math.Max(sizeTooSmall, 256);
            var output = new FixedSizedBufferWriter(sizeTooSmall);

            byte[] utf8String = Encoding.UTF8.GetBytes(new string('a', 215));

            Guid guid = Guid.NewGuid();
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.Flush();
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue(guid));
            }

            sizeTooSmall += options.Indented ? options.IndentSize + 40 : 38;
            output = new FixedSizedBufferWriter(sizeTooSmall);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.WriteStringValue(guid);
                yamlUtf8.Flush();
            }
            string actualStr = Encoding.UTF8.GetString(output.Formatted);

            if (!options.Indented)
            {
                Assert.Equal(257, output.Formatted.Length);
            }
            Assert.Equal($"\"{guid.ToString()}\"", actualStr.Substring(actualStr.Length - 38));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FixedSizeBufferWriter_DateTime(YamlWriterOptions options)
        {
            int sizeTooSmall = options.Indented ? options.IndentSize + 240 : 236;
            sizeTooSmall = Math.Max(sizeTooSmall, 256);
            var output = new FixedSizedBufferWriter(sizeTooSmall);

            byte[] utf8String = Encoding.UTF8.GetBytes(new string('a', 232));

            var date = new DateTime(2019, 1, 1);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.Flush();
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue(date));
            }

            sizeTooSmall += options.Indented ? options.IndentSize + 37 : 35;
            output = new FixedSizedBufferWriter(sizeTooSmall);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.WriteStringValue(date);
                yamlUtf8.Flush();
            }
            string actualStr = Encoding.UTF8.GetString(output.Formatted);

            if (!options.Indented)
            {
                Assert.Equal(257, output.Formatted.Length);
            }
            Assert.Equal($"\"{date.ToString("yyyy-MM-ddTHH:mm:ss")}\"", actualStr.Substring(actualStr.Length - 21));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FixedSizeBufferWriter_DateTimeOffset(YamlWriterOptions options)
        {
            int sizeTooSmall = options.Indented ? options.IndentSize + 240 : 236;
            sizeTooSmall = Math.Max(sizeTooSmall, 256);
            var output = new FixedSizedBufferWriter(sizeTooSmall);

            byte[] utf8String = Encoding.UTF8.GetBytes(new string('a', 226));

            DateTimeOffset date = new DateTime(2019, 1, 1);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.Flush();
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue(date));
            }

            sizeTooSmall += options.Indented ? options.IndentSize + 37 : 35;
            output = new FixedSizedBufferWriter(sizeTooSmall);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValue(utf8String);
                yamlUtf8.WriteStringValue(date);
                yamlUtf8.Flush();
            }
            string actualStr = Encoding.UTF8.GetString(output.Formatted);

            if (!options.Indented)
            {
                Assert.Equal(257, output.Formatted.Length);
            }
            Assert.Equal($"\"{date.ToString("yyyy-MM-ddTHH:mm:ssK")}\"", actualStr.Substring(actualStr.Length - 27));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void FixedSizeBufferWriter_Decimal(YamlWriterOptions options)
        {
            var random = new Random(42);

            for (int i = 0; i < 1_000; i++)
            {
                var output = new FixedSizedBufferWriter(256);
                decimal value = YamlTestHelper.NextDecimal(random, 78E14, -78E14);

                using var yamlUtf8 = new Utf8YamlWriter(output, options);
                yamlUtf8.WriteNumberValue(value);

                yamlUtf8.Flush();
                string actualStr = Encoding.UTF8.GetString(output.Formatted);

                Assert.True(output.Formatted.Length <= 31);
                Assert.Equal(decimal.Parse(actualStr, CultureInfo.InvariantCulture), value);
            }

            for (int i = 0; i < 1_000; i++)
            {
                var output = new FixedSizedBufferWriter(256);
                decimal value = YamlTestHelper.NextDecimal(random, 1_000_000, -1_000_000);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);
                yamlUtf8.WriteNumberValue(value);

                yamlUtf8.Flush();
                string actualStr = Encoding.UTF8.GetString(output.Formatted);

                Assert.True(output.Formatted.Length <= 31);
                Assert.Equal(decimal.Parse(actualStr, CultureInfo.InvariantCulture), value);
            }

            {
                var output = new FixedSizedBufferWriter(256);
                decimal value = 9999999999999999999999999999m;
                using var yamlUtf8 = new Utf8YamlWriter(output, options);
                yamlUtf8.WriteNumberValue(value);

                yamlUtf8.Flush();
                string actualStr = Encoding.UTF8.GetString(output.Formatted);

                Assert.Equal(value.ToString().Length, output.Formatted.Length);
                Assert.Equal(decimal.Parse(actualStr, CultureInfo.InvariantCulture), value);
            }

            {
                var output = new FixedSizedBufferWriter(256);
                decimal value = -9999999999999999999999999999m;
                using var yamlUtf8 = new Utf8YamlWriter(output, options);
                yamlUtf8.WriteNumberValue(value);

                yamlUtf8.Flush();
                string actualStr = Encoding.UTF8.GetString(output.Formatted);

                Assert.Equal(value.ToString().Length, output.Formatted.Length);
                Assert.Equal(decimal.Parse(actualStr, CultureInfo.InvariantCulture), value);
            }

            {
                int sizeTooSmall = options.Indented ? options.IndentSize + 230 : 226;
                sizeTooSmall = Math.Max(sizeTooSmall, 256);
                var output = new FixedSizedBufferWriter(sizeTooSmall);

                byte[] utf8String = Encoding.UTF8.GetBytes(new string('a', 222));

                decimal value = -0.9999999999999999999999999999m;
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartArray();
                    yamlUtf8.WriteStringValue(utf8String);
                    yamlUtf8.Flush();
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteNumberValue(value));
                }

                sizeTooSmall += options.Indented ? options.IndentSize + 33 : 31;
                output = new FixedSizedBufferWriter(sizeTooSmall);
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartArray();
                    yamlUtf8.WriteStringValue(utf8String);
                    yamlUtf8.WriteNumberValue(value);

                    yamlUtf8.Flush();
                }
                string actualStr = Encoding.UTF8.GetString(output.Formatted);

                if (!options.Indented)
                {
                    Assert.Equal(257, output.Formatted.Length);
                }
                Assert.Equal(decimal.Parse(actualStr.Substring(actualStr.Length - 31), CultureInfo.InvariantCulture), value);
            }
        }

        private const YamlValueKind YamlValueKindStringSegment = (YamlValueKind)(1 << 7);
        public static IEnumerable<object[]> InvalidYamlDueToWritingMultipleValues_TestData() =>
            YamlOptionsWith([
                YamlValueKind.Array,
                YamlValueKind.Object,
                YamlValueKind.String,
                YamlValueKind.Number,
                YamlValueKind.True,
                YamlValueKind.False,
                YamlValueKind.Null,
                YamlValueKindStringSegment
            ]);

        [Theory]
        [MemberData(nameof(InvalidYamlDueToWritingMultipleValues_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlDueToWritingMultipleValues(YamlWriterOptions options, YamlValueKind kind)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartObject(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartObject("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartArray(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteEndObject(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteEndArray(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WritePropertyName("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteString("key", "foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValue("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo".AsSpan(), true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo".AsSpan(), false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo"u8, true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo"u8, false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBase64StringSegment("foo"u8, true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBase64StringSegment("foo"u8, false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNumber("key", 123), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNumberValue(123), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBoolean("key", true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBooleanValue(true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBoolean("key", false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBooleanValue(false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNull("key"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNullValue(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind);
                // Writing a comment after any preamable is valid (even when skipValidation is false)
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteCommentValue("some comment"), skipValidation: true);
            }
        }

        [Theory]
        [MemberData(nameof(InvalidYamlDueToWritingMultipleValues_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlDueToWritingMultipleValuesWithComments(YamlWriterOptions options, YamlValueKind kind)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartObject(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartObject("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStartArray(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteEndObject(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteEndArray(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WritePropertyName("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteString("key", "foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValue("foo"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo".AsSpan(), true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo".AsSpan(), false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo"u8, true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteStringValueSegment("foo"u8, false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBase64StringSegment("foo"u8, true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBase64StringSegment("foo"u8, false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNumber("key", 123), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNumberValue(123), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBoolean("key", true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBooleanValue(true), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBoolean("key", false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteBooleanValue(false), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNull("key"), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteNullValue(), options.SkipValidation);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                WritePreamble(yamlUtf8, kind, addComments: true);
                // Writing a comment after any preamable is valid (even when skipValidation is false)
                ValidateAction(yamlUtf8, () => yamlUtf8.WriteCommentValue("some comment"), skipValidation: true);
            }
        }

        private void WritePreamble(Utf8YamlWriter writer, YamlValueKind kind, bool addComments = false)
        {
            Debug.Assert(writer.BytesCommitted == 0 && writer.BytesPending == 0 && writer.CurrentDepth == 0 && kind != YamlValueKind.Undefined);

            if (addComments)
            {
                writer.WriteCommentValue(" comment value before ");
            }

            switch (kind)
            {
                case YamlValueKind.Object:
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                    break;
                case YamlValueKind.Array:
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                    break;
                case YamlValueKind.String:
                    writer.WriteStringValue("foo");
                    break;
                case YamlValueKind.Number:
                    writer.WriteNumberValue(1);
                    break;
                case YamlValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case YamlValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case YamlValueKind.Null:
                    writer.WriteNullValue();
                    break;
                case YamlValueKindStringSegment:
                    writer.WriteStringValueSegment("foo".ToCharArray(), false);
                    writer.WriteStringValueSegment("bar".ToCharArray(), true);
                    break;
                default:
                    Debug.Fail($"Invalid YamlValueKind passed in '{kind}'.");
                    break;
            }

            if (addComments)
            {
                writer.WriteCommentValue(" comment value after ");
            }
        }

        private void ValidateAction(Utf8YamlWriter writer, Action action, bool skipValidation)
        {
            int originalBytesPending = writer.BytesPending;
            if (skipValidation)
            {
                action.Invoke();
                Assert.NotEqual(originalBytesPending, writer.BytesPending);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(action);
                Assert.Equal(originalBytesPending, writer.BytesPending);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlMismatch(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray("property at start");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("property at start"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject("property at start");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject("property at start"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray("property inside array");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("property inside array"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStringValue("value");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue("key"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStringValueSegment(['a', 'b'], true);
                    yamlUtf8.WriteStringValueSegment(['a', 'b'], false);
                    yamlUtf8.WriteStringValueSegment(['a', 'b'], true);

                    yamlUtf8.WriteStringValueSegment([65, 66], true);
                    yamlUtf8.WriteStringValueSegment([65, 66], false);
                    yamlUtf8.WriteStringValueSegment([65, 66], true);

                    yamlUtf8.WriteBase64StringSegment([65, 66], true);
                    yamlUtf8.WriteBase64StringSegment([65, 66], false);
                    yamlUtf8.WriteBase64StringSegment([65, 66], true);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValueSegment(['a', 'b'], true));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValueSegment(['a', 'b'], false));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValueSegment([65, 66], true));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValueSegment([65, 66], false));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteBase64StringSegment([65, 66], true));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteBase64StringSegment([65, 66], false));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteString("key", "value");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteString("key", "value"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteString(YamlEncodedText.Encode("key"), YamlEncodedText.Encode("value"));
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteString(YamlEncodedText.Encode("key"), YamlEncodedText.Encode("value")));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartObject("some object");
                yamlUtf8.WriteEndObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject("some object");
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject("some object"));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartArray("test array");
                yamlUtf8.WriteEndArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartObject("test object");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                    yamlUtf8.WritePropertyName(YamlEncodedText.Encode("test name"));
                    yamlUtf8.WritePropertyName("test name".AsSpan());
                    yamlUtf8.WritePropertyName("test name"u8.ToArray());
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName(YamlEncodedText.Encode("test name")));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name".AsSpan()));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"u8.ToArray()));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteString("another property name", "some value");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteString("another property name", "some value"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteNumber("another property name", 12345);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteNumber("another property name", 12345));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteNull("another property name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteNull("another property name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("first name");
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteBoolean("another property name", true);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteBoolean("another property name", true));
                }
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlIncomplete(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartObject("some object");
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartArray("test array");
                yamlUtf8.WriteEndArray();
                Assert.True(yamlUtf8.CurrentDepth != 0);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlPrimitive(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteNumberValue(12345);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteNumberValue(12345));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartArray("property name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("property name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject("property name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject("property name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteString("property name", "value");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteString("property name", "value"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteString(YamlEncodedText.Encode("property name"), YamlEncodedText.Encode("value"));
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteString(YamlEncodedText.Encode("property name"), YamlEncodedText.Encode("value")));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNumberValue(12345);
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteBooleanValue(true);
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteNullValue();
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStringValue("some string");
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStringValueSegment("a".AsSpan(), true);
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStringValueSegment("a"u8, true);
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteBase64StringSegment("a"u8, true);
                if (options.SkipValidation)
                {
                    yamlUtf8.WritePropertyName("test name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WritePropertyName("test name"));
                }
            }
        }

        // Name is present in the test data to make it easier to identify the test case
        public static IEnumerable<object[]> InvalidYamlStringValueSegment_TestData =>
            from write in new (string methodName, Action<Utf8YamlWriter> method)[] {
                (nameof(Utf8YamlWriter.WriteStartObject), writer => writer.WriteStartObject()),
                (nameof(Utf8YamlWriter.WriteEndObject), writer => writer.WriteEndObject()),
                (nameof(Utf8YamlWriter.WriteStartArray), writer => writer.WriteStartArray()),
                (nameof(Utf8YamlWriter.WriteEndArray), writer => writer.WriteEndArray()),
                (nameof(Utf8YamlWriter.WriteBooleanValue), writer => writer.WriteBooleanValue(true)),
                (nameof(Utf8YamlWriter.WriteBoolean), writer => writer.WriteBoolean("foo", true)),
                (nameof(Utf8YamlWriter.WriteCommentValue), writer => writer.WriteCommentValue("comment")),
                (nameof(Utf8YamlWriter.WriteNullValue), writer => writer.WriteNullValue()),
                (nameof(Utf8YamlWriter.WriteStringValue), writer => writer.WriteStringValue("foo")),
                (nameof(Utf8YamlWriter.WritePropertyName), writer => writer.WritePropertyName("foo")),
            }
            from option in new [] { new YamlWriterOptions { SkipValidation = true }, new YamlWriterOptions { SkipValidation = false } }
            select new object[] { write.methodName, write.method, option };

        [Theory]
        [MemberData(nameof(InvalidYamlStringValueSegment_TestData))]
        public void InvalidYamlStringValueSegment(string _, Action<Utf8YamlWriter> write, YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStringValueSegment("foo"u8, isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteBase64StringSegment("foo"u8, isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStringValueSegment("foo".ToCharArray(), isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValueSegment("foo"u8, isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteStringValueSegment("foo".ToCharArray(), isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("prop");
                yamlUtf8.WriteStringValueSegment("foo"u8, isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WritePropertyName("prop");
                yamlUtf8.WriteStringValueSegment("foo".ToCharArray(), isFinalSegment: false);
                if (options.SkipValidation)
                {
                    write(yamlUtf8);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => write(yamlUtf8));
                }
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void InvalidNumbersYaml(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(double.NegativeInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(double.PositiveInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(double.NaN));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(float.PositiveInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(float.NegativeInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumberValue(float.NaN));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", double.NegativeInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", double.PositiveInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", double.NaN));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", float.PositiveInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", float.NegativeInfinity));
            }

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber("name", float.NaN));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void InvalidYamlContinueShouldSucceed(bool formatted)
        {
            var options = new YamlWriterOptions { Indented = formatted, SkipValidation = true };
            var output = new ArrayBufferWriter<byte>(1024);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            for (int i = 0; i < 100; i++)
            {
                yamlUtf8.WriteEndArray();
            }
            yamlUtf8.WriteStartArray();
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            var sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                if (options.Indented)
                    sb.Append(options.NewLine);
                sb.Append("]");
            }
            sb.Append(",");
            if (options.Indented)
                sb.Append(options.NewLine);
            sb.Append("[]");

            YamlTestHelper.AssertContents(sb.ToString(), output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteSeparateProperties(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            var stringWriter = new StringWriter();
            var yaml = new JsonTextWriter(stringWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName("foo1");
            yaml.WriteValue("bar1");
            yaml.WritePropertyName("foo2");
            yaml.WriteValue("bar2");
            yaml.WritePropertyName("foo3");
            yaml.WriteValue("bar3");
            yaml.WritePropertyName("foo4");
            yaml.WriteValue("bar4");
            yaml.WritePropertyName("array");
            yaml.WriteStartArray();
            yaml.WriteEndArray();
            yaml.WriteEnd();

            yaml.Flush();

            string expectedStr = HandleFormatting(stringWriter.ToString(), options);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);
            yamlUtf8.WriteStartObject();
            yamlUtf8.WritePropertyName("foo1"u8);
            yamlUtf8.WriteStringValue("bar1");
            yamlUtf8.WritePropertyName("foo2");
            yamlUtf8.WriteStringValue("bar2");
            yamlUtf8.WritePropertyName(YamlEncodedText.Encode("foo3"));
            yamlUtf8.WriteStringValue("bar3");
            yamlUtf8.WritePropertyName("foo4".AsSpan());
            yamlUtf8.WriteStringValue("bar4");
            yamlUtf8.WritePropertyName("array");
            yamlUtf8.WriteStartArray();
            yamlUtf8.WriteEndArray();
            yamlUtf8.WriteEndObject();
            yamlUtf8.Flush();
            Assert.Equal(expectedStr, Encoding.UTF8.GetString(output.WrittenMemory.ToArray()));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WritingTooDeep(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(1024);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            for (int i = 0; i < 1000; i++)
            {
                yamlUtf8.WriteStartArray();
            }
            Assert.Equal(1000, yamlUtf8.CurrentDepth);
            Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray());
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WritingTooDeepProperty(YamlWriterOptions options)
        {
            var capacity = 3 + 1000 * (11 + 1001 * options.IndentSize / 2);
            var output = new ArrayBufferWriter<byte>(capacity);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                for (int i = 0; i < 999; i++)
                {
                    yamlUtf8.WriteStartObject("name");
                }
                Assert.Equal(1000, yamlUtf8.CurrentDepth);
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("name"));
            }

            output = new ArrayBufferWriter<byte>(capacity);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                for (int i = 0; i < 999; i++)
                {
                    yamlUtf8.WriteStartObject("name"u8);
                }
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray("name"u8));
            }

            output = new ArrayBufferWriter<byte>(capacity);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                for (int i = 0; i < 999; i++)
                {
                    yamlUtf8.WriteStartObject(YamlEncodedText.Encode("name"));
                }
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartArray(YamlEncodedText.Encode("name")));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(2048)]
        [InlineData(1024 * 1024)]
        public static void CustomMaxDepth_DepthWithinLimit_ShouldSucceed(int maxDepth)
        {
            var options = new YamlWriterOptions { MaxDepth = maxDepth };
            int effectiveMaxDepth = maxDepth == 0 ? 1000 : maxDepth;

            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);

            for (int i = 0; i < effectiveMaxDepth; i++)
            {
                writer.WriteStartArray();
            }

            Assert.Equal(effectiveMaxDepth, writer.CurrentDepth);

            for (int i = 0; i < effectiveMaxDepth; i++)
            {
                writer.WriteEndArray();
            }

            Assert.Equal(0, writer.CurrentDepth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(2048)]
        [InlineData(1024 * 1024)]
        public static void CustomMaxDepth_DepthExceedingLimit_ShouldFail(int maxDepth)
        {
            var options = new YamlWriterOptions { MaxDepth = maxDepth };
            int effectiveMaxDepth = maxDepth == 0 ? 1000 : maxDepth;

            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);

            for (int i = 0; i < effectiveMaxDepth; i++)
            {
                writer.WriteStartArray();
            }

            Assert.Equal(effectiveMaxDepth, writer.CurrentDepth);

            Assert.Throws<InvalidOperationException>(() => writer.WriteStartArray());
        }

        // NOTE: WritingTooLargeProperty test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WritingTooLargeProperty(YamlWriterOptions options)
        {
            try
            {
                byte[] key;
                char[] keyChars;

                key = new byte[MaxUnescapedTokenSize + 1];
                keyChars = new char[MaxUnescapedTokenSize + 1];

                key.AsSpan().Fill((byte)'a');
                keyChars.AsSpan().Fill('a');

                var output = new ArrayBufferWriter<byte>(1024);

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStartArray(keyChars));
                }

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStartArray(key));
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // NOTE: WritingTooLargePropertyStandalone test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WritingTooLargePropertyStandalone(YamlWriterOptions options)
        {
            try
            {
                byte[] key;
                char[] keyChars;

                key = new byte[MaxUnescapedTokenSize + 1];
                keyChars = new char[MaxUnescapedTokenSize + 1];

                key.AsSpan().Fill((byte)'a');
                keyChars.AsSpan().Fill('a');

                    var output = new ArrayBufferWriter<byte>(1024);

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WritePropertyName(keyChars));
                }

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WritePropertyName(key));
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WritingTooLargeBase64Bytes(YamlWriterOptions options)
        {
            try
            {
                byte[] value = new byte[200_000_000];
                value.AsSpan().Fill((byte)'a');

                var output = new ArrayBufferWriter<byte>(value.Length);

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteBase64StringValue(value.AsSpan(0, 125_000_001));
                }
                Assert.InRange(output.WrittenCount, 125_000_001, int.MaxValue);
                output.Clear();

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteBase64String(value.AsSpan(0, 166_666_667), value.AsSpan(0, 1)));
                }

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteBase64String(Encoding.UTF8.GetString(value).ToCharArray().AsSpan(0, 166_666_667), value.AsSpan(0, 1)));
                }

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteBase64StringValue(value);
                }
                Assert.InRange(output.WrittenCount, Base64.GetMaxEncodedToUtf8Length(value.Length), int.MaxValue);
                output.Clear();

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo", value);
                }
                Assert.InRange(output.WrittenCount, Base64.GetMaxEncodedToUtf8Length(value.Length), int.MaxValue);
                output.Clear();

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo"u8, value);
                }
                Assert.InRange(output.WrittenCount, Base64.GetMaxEncodedToUtf8Length(value.Length), int.MaxValue);
                output.Clear();

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo".AsSpan(), value);
                }
                Assert.InRange(output.WrittenCount, Base64.GetMaxEncodedToUtf8Length(value.Length), int.MaxValue);
                output.Clear();

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String(YamlEncodedText.Encode("foo"), value);
                }
                Assert.InRange(output.WrittenCount, Base64.GetMaxEncodedToUtf8Length(value.Length), int.MaxValue);
                output.Clear();
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // NOTE: WritingHugeBase64Bytes test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WritingHugeBase64Bytes(YamlWriterOptions options)
        {
            try
            {
                byte[] value = new byte[1_000_000_000];

                value.AsSpan().Fill(168);

                    var output = new ArrayBufferWriter<byte>(1024);

                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteBase64StringValue(value);
                }

                output.Clear();
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo", value);
                    yamlUtf8.WriteEndObject();
                }

                output.Clear();
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo"u8, value);
                    yamlUtf8.WriteEndObject();
                }

                output.Clear();
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String("foo".AsSpan(), value);
                    yamlUtf8.WriteEndObject();
                }

                output.Clear();
                using (var yamlUtf8 = new Utf8YamlWriter(output, options))
                {
                    yamlUtf8.WriteStartObject();
                    yamlUtf8.WriteBase64String(YamlEncodedText.Encode("foo"), value);
                    yamlUtf8.WriteEndObject();
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // https://github.com/dotnet/runtime/issues/30746
        [Theory, OuterLoop("Very long running test")]
        [MemberData(nameof(YamlOptions_TestData))]
        [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        public void Writing3MBBase64Bytes(YamlWriterOptions options)
        {
            byte[] value = new byte[3 * 1024 * 1024];

            value.AsSpan().Fill(168);

            byte[] base64StringUtf8 = new byte[Base64.GetMaxEncodedToUtf8Length(value.Length)];
            Base64.EncodeToUtf8(value, base64StringUtf8, out _, out int bytesWritten);
            string expectedValue = Encoding.UTF8.GetString(base64StringUtf8.AsSpan(0, bytesWritten).ToArray());

            string expectedYaml = options.Indented ? $"{{{options.NewLine}{GetIndentText(options)}\"foo\": \"{expectedValue}\"{options.NewLine}}}" : $"{{\"foo\":\"{expectedValue}\"}}";

            var output = new ArrayBufferWriter<byte>(1024);

            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteBase64StringValue(value);
            }
            YamlTestHelper.AssertContents($"\"{expectedValue}\"", output);

            output.Clear();
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBase64String("foo", value);
                yamlUtf8.WriteEndObject();
            }
            YamlTestHelper.AssertContents(expectedYaml, output);

            output.Clear();
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBase64String("foo"u8, value);
                yamlUtf8.WriteEndObject();
            }
            YamlTestHelper.AssertContents(expectedYaml, output);

            output.Clear();
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBase64String("foo".AsSpan(), value);
                yamlUtf8.WriteEndObject();
            }
            YamlTestHelper.AssertContents(expectedYaml, output);

            output.Clear();
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBase64String(YamlEncodedText.Encode("foo"), value);
                yamlUtf8.WriteEndObject();
            }
            YamlTestHelper.AssertContents(expectedYaml, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteSingleValue(YamlWriterOptions options)
        {
            string expectedStr = "123456789012345";

            
            var output = new ArrayBufferWriter<byte>(1024);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteNumberValue(123456789012345);

            yamlUtf8.Flush();

            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteHelloWorld(YamlWriterOptions options)
        {
            string propertyName = "message";
            string value = "Hello, World!";
            string expectedStr = GetHelloWorldExpectedString(options, propertyName, value);

            YamlEncodedText encodedPropertyName = YamlEncodedText.Encode(propertyName);
            YamlEncodedText encodedValue = YamlEncodedText.Encode(value);

            ReadOnlySpan<byte> utf8PropertyName = "message"u8;
            ReadOnlySpan<byte> utf8Value = "Hello, World!"u8;


            for (int i = 0; i < 32; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(propertyName, value);
                        yamlUtf8.WriteString(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(propertyName, value.AsSpan());
                        yamlUtf8.WriteString(propertyName, value.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteString(propertyName, utf8Value);
                        yamlUtf8.WriteString(propertyName, utf8Value);
                        break;
                    case 3:
                        yamlUtf8.WriteString(propertyName.AsSpan(), value);
                        yamlUtf8.WriteString(propertyName.AsSpan(), value);
                        break;
                    case 4:
                        yamlUtf8.WriteString(propertyName.AsSpan(), value.AsSpan());
                        yamlUtf8.WriteString(propertyName.AsSpan(), value.AsSpan());
                        break;
                    case 5:
                        yamlUtf8.WriteString(propertyName.AsSpan(), utf8Value);
                        yamlUtf8.WriteString(propertyName.AsSpan(), utf8Value);
                        break;
                    case 6:
                        yamlUtf8.WriteString(utf8PropertyName, value);
                        yamlUtf8.WriteString(utf8PropertyName, value);
                        break;
                    case 7:
                        yamlUtf8.WriteString(utf8PropertyName, value.AsSpan());
                        yamlUtf8.WriteString(utf8PropertyName, value.AsSpan());
                        break;
                    case 8:
                        yamlUtf8.WriteString(utf8PropertyName, utf8Value);
                        yamlUtf8.WriteString(utf8PropertyName, utf8Value);
                        break;
                    case 9:
                        yamlUtf8.WriteString(encodedPropertyName, value);
                        yamlUtf8.WriteString(encodedPropertyName, value);
                        break;
                    case 10:
                        yamlUtf8.WriteString(encodedPropertyName, value.AsSpan());
                        yamlUtf8.WriteString(encodedPropertyName, value.AsSpan());
                        break;
                    case 11:
                        yamlUtf8.WriteString(encodedPropertyName, utf8Value);
                        yamlUtf8.WriteString(encodedPropertyName, utf8Value);
                        break;
                    case 12:
                        yamlUtf8.WriteString(encodedPropertyName, encodedValue);
                        yamlUtf8.WriteString(encodedPropertyName, encodedValue);
                        break;
                    case 13:
                        yamlUtf8.WriteString(propertyName, encodedValue);
                        yamlUtf8.WriteString(propertyName, encodedValue);
                        break;
                    case 14:
                        yamlUtf8.WriteString(propertyName.AsSpan(), encodedValue);
                        yamlUtf8.WriteString(propertyName.AsSpan(), encodedValue);
                        break;
                    case 15:
                        yamlUtf8.WriteString(utf8PropertyName, encodedValue);
                        yamlUtf8.WriteString(utf8PropertyName, encodedValue);
                        break;
                    case 16:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 17:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 18:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        break;
                    case 19:
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 20:
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 21:
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(utf8Value);
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(utf8Value);
                        break;
                    case 22:
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 23:
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 24:
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        break;
                    case 25:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 26:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 27:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(utf8Value);
                        break;
                    case 28:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 29:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 30:
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 31:
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(utf8PropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteHelloWorldEscaped(YamlWriterOptions options)
        {
            string propertyName = "mess><age";
            string value = "Hello,>< World!";
            string expectedStr = GetHelloWorldExpectedString(options, propertyName, value);

            ReadOnlySpan<char> propertyNameSpan = propertyName.AsSpan();
            ReadOnlySpan<char> valueSpan = value.AsSpan();
            ReadOnlySpan<byte> propertyNameSpanUtf8 = Encoding.UTF8.GetBytes(propertyName);
            ReadOnlySpan<byte> valueSpanUtf8 = Encoding.UTF8.GetBytes(value);

            YamlEncodedText encodedPropertyName = YamlEncodedText.Encode(propertyName);
            YamlEncodedText encodedValue = YamlEncodedText.Encode(value);

            for (int i = 0; i < 32; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(propertyName, value);
                        yamlUtf8.WriteString(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(propertyName, valueSpan);
                        yamlUtf8.WriteString(propertyName, valueSpan);
                        break;
                    case 2:
                        yamlUtf8.WriteString(propertyName, valueSpanUtf8);
                        yamlUtf8.WriteString(propertyName, valueSpanUtf8);
                        break;
                    case 3:
                        yamlUtf8.WriteString(propertyNameSpan, value);
                        yamlUtf8.WriteString(propertyNameSpan, value);
                        break;
                    case 4:
                        yamlUtf8.WriteString(propertyNameSpan, valueSpan);
                        yamlUtf8.WriteString(propertyNameSpan, valueSpan);
                        break;
                    case 5:
                        yamlUtf8.WriteString(propertyNameSpan, valueSpanUtf8);
                        yamlUtf8.WriteString(propertyNameSpan, valueSpanUtf8);
                        break;
                    case 6:
                        yamlUtf8.WriteString(propertyNameSpanUtf8, value);
                        yamlUtf8.WriteString(propertyNameSpanUtf8, value);
                        break;
                    case 7:
                        yamlUtf8.WriteString(propertyNameSpanUtf8, valueSpan);
                        yamlUtf8.WriteString(propertyNameSpanUtf8, valueSpan);
                        break;
                    case 8:
                        yamlUtf8.WriteString(propertyNameSpanUtf8, valueSpanUtf8);
                        yamlUtf8.WriteString(propertyNameSpanUtf8, valueSpanUtf8);
                        break;
                    case 9:
                        yamlUtf8.WriteString(encodedPropertyName, value);
                        yamlUtf8.WriteString(encodedPropertyName, value);
                        break;
                    case 10:
                        yamlUtf8.WriteString(encodedPropertyName, value.AsSpan());
                        yamlUtf8.WriteString(encodedPropertyName, value.AsSpan());
                        break;
                    case 11:
                        yamlUtf8.WriteString(encodedPropertyName, valueSpanUtf8);
                        yamlUtf8.WriteString(encodedPropertyName, valueSpanUtf8);
                        break;
                    case 12:
                        yamlUtf8.WriteString(encodedPropertyName, encodedValue);
                        yamlUtf8.WriteString(encodedPropertyName, encodedValue);
                        break;
                    case 13:
                        yamlUtf8.WriteString(propertyName, encodedValue);
                        yamlUtf8.WriteString(propertyName, encodedValue);
                        break;
                    case 14:
                        yamlUtf8.WriteString(propertyName.AsSpan(), encodedValue);
                        yamlUtf8.WriteString(propertyName.AsSpan(), encodedValue);
                        break;
                    case 15:
                        yamlUtf8.WriteString(propertyNameSpanUtf8, encodedValue);
                        yamlUtf8.WriteString(propertyNameSpanUtf8, encodedValue);
                        break;
                    case 16:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 17:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(valueSpan);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(valueSpan);
                        break;
                    case 18:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        break;
                    case 19:
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 20:
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(valueSpan);
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(valueSpan);
                        break;
                    case 21:
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        yamlUtf8.WritePropertyName(propertyNameSpan);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        break;
                    case 22:
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 23:
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(valueSpan);
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(valueSpan);
                        break;
                    case 24:
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        break;
                    case 25:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 26:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 27:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(valueSpanUtf8);
                        break;
                    case 28:
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(encodedPropertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 29:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 30:
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(propertyName.AsSpan());
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 31:
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(encodedValue);
                        yamlUtf8.WritePropertyName(propertyNameSpanUtf8);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }

            // Verify that escaping does not change the input strings/spans.
            Assert.Equal("mess><age", propertyName);
            Assert.Equal("Hello,>< World!", value);
            Assert.True(propertyName.AsSpan().SequenceEqual(propertyNameSpan));
            Assert.True(value.AsSpan().SequenceEqual(valueSpan));
            Assert.True(Encoding.UTF8.GetBytes(propertyName).AsSpan().SequenceEqual(propertyNameSpanUtf8));
            Assert.True(Encoding.UTF8.GetBytes(value).AsSpan().SequenceEqual(valueSpanUtf8));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WritePartialHelloWorld(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartObject();

            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(1, yamlUtf8.BytesPending);

            yamlUtf8.WriteString("message", "Hello, World!");

            Assert.Equal(0, yamlUtf8.BytesCommitted);
            if (options.Indented)
                Assert.Equal(26 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesPending); // new lines, indentation, white space
            else
                Assert.Equal(26, yamlUtf8.BytesPending);

            yamlUtf8.Flush();

            if (options.Indented)
                Assert.Equal(26 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesCommitted); // new lines, indentation, white space
            else
                Assert.Equal(26, yamlUtf8.BytesCommitted);

            Assert.Equal(0, yamlUtf8.BytesPending);

            yamlUtf8.WriteString("message", "Hello, World!");
            yamlUtf8.WriteEndObject();

            if (options.Indented)
                Assert.Equal(26 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesCommitted);
            else
                Assert.Equal(26, yamlUtf8.BytesCommitted);

            if (options.Indented)
                Assert.Equal(27 + options.IndentSize + (2 * options.NewLine.Length) + 1, yamlUtf8.BytesPending); // new lines, indentation, white space
            else
                Assert.Equal(27, yamlUtf8.BytesPending);

            yamlUtf8.Flush();

            if (options.Indented)
                Assert.Equal(53 + (2 * options.IndentSize) + (3 * options.NewLine.Length) + (1 * 2), yamlUtf8.BytesCommitted); // new lines, indentation, white space
            else
                Assert.Equal(53, yamlUtf8.BytesCommitted);

            Assert.Equal(0, yamlUtf8.BytesPending);
        }

        public static IEnumerable<object[]> WriteBase64String_TestData() =>
            YamlOptionsWith([
                "message",
                "escape mess><age",
                "<write base64 string when escape length bigger than given string",
                ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>"
            ]);

        [Theory]
        [MemberData(nameof(WriteBase64String_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteBase64String(YamlWriterOptions options, string inputValue)
        {
            string propertyName = inputValue;
            byte[] value = { 1, 2, 3, 4, 5, 6 };
            string expectedStr = GetBase64ExpectedString(options, propertyName, value);

            ReadOnlySpan<char> propertyNameSpan = propertyName.AsSpan();
            ReadOnlySpan<byte> propertyNameSpanUtf8 = Encoding.UTF8.GetBytes(propertyName);
            YamlEncodedText encodedPropertyName = YamlEncodedText.Encode(propertyName);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteBase64String(propertyName, value);
                        yamlUtf8.WriteBase64String(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteBase64String(propertyNameSpan, value);
                        yamlUtf8.WriteBase64String(propertyNameSpan, value);
                        break;
                    case 2:
                        yamlUtf8.WriteBase64String(propertyNameSpanUtf8, value);
                        yamlUtf8.WriteBase64String(propertyNameSpanUtf8, value);
                        break;
                    case 3:
                        yamlUtf8.WriteBase64String(encodedPropertyName, value);
                        yamlUtf8.WriteBase64String(encodedPropertyName, value);
                        break;
                }

                yamlUtf8.WriteStartArray("array");
                yamlUtf8.WriteBase64StringValue(new byte[] { 1, 2 });
                yamlUtf8.WriteBase64StringValue(new byte[] { 3, 4 });
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }

            // Verify that escaping does not change the input strings/spans.
            Assert.Equal(inputValue, propertyName);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, value);
            Assert.True(propertyName.AsSpan().SequenceEqual(propertyNameSpan));
            Assert.True(Encoding.UTF8.GetBytes(propertyName).AsSpan().SequenceEqual(propertyNameSpanUtf8));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WritePartialBase64String(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartObject();

            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(1, yamlUtf8.BytesPending);

            yamlUtf8.WriteBase64String("message", new byte[] { 201, 153, 199 });

            Assert.Equal(0, yamlUtf8.BytesCommitted);
            if (options.Indented)
                Assert.Equal(17 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesPending); // new lines, indentation, white space
            else
                Assert.Equal(17, yamlUtf8.BytesPending);

            yamlUtf8.Flush();

            if (options.Indented)
                Assert.Equal(17 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesCommitted); // new lines, indentation, white space
            else
                Assert.Equal(17, yamlUtf8.BytesCommitted);

            Assert.Equal(0, yamlUtf8.BytesPending);

            yamlUtf8.WriteBase64String("message", new byte[] { 201, 153, 199 });
            yamlUtf8.WriteEndObject();

            if (options.Indented)
                Assert.Equal(17 + options.IndentSize + options.NewLine.Length + 1, yamlUtf8.BytesCommitted);
            else
                Assert.Equal(17, yamlUtf8.BytesCommitted);

            if (options.Indented)
                Assert.Equal(18 + options.IndentSize + (2 * options.NewLine.Length) + 1, yamlUtf8.BytesPending); // new lines, indentation, white space
            else
                Assert.Equal(18, yamlUtf8.BytesPending);

            yamlUtf8.Flush();

            if (options.Indented)
                Assert.Equal(35 + (2 * options.IndentSize) + (3 * options.NewLine.Length) + (1 * 2), yamlUtf8.BytesCommitted); // new lines, indentation, white space
            else
                Assert.Equal(35, yamlUtf8.BytesCommitted);

            Assert.Equal(0, yamlUtf8.BytesPending);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteInvalidPartialYaml(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartObject();

            Assert.Equal(0, yamlUtf8.BytesCommitted);
            Assert.Equal(1, yamlUtf8.BytesPending);

            yamlUtf8.Flush();

            Assert.Equal(1, yamlUtf8.BytesCommitted);
            Assert.Equal(0, yamlUtf8.BytesPending);

            if (options.SkipValidation)
            {
                yamlUtf8.WriteStringValue("Hello, World!");
                yamlUtf8.WriteEndArray();
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStringValue("Hello, World!"));
                Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteInvalidBase64(YamlWriterOptions options)
        {
            {
                var output = new ArrayBufferWriter<byte>(10);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                Assert.Equal(0, yamlUtf8.BytesCommitted);
                Assert.Equal(1, yamlUtf8.BytesPending);

                yamlUtf8.Flush();

                Assert.Equal(1, yamlUtf8.BytesCommitted);
                Assert.Equal(0, yamlUtf8.BytesPending);

                if (options.SkipValidation)
                {
                    yamlUtf8.WriteBase64StringValue(new byte[] { 1, 2 });
                    yamlUtf8.WriteEndArray();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteBase64StringValue(new byte[] { 1, 2 }));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndArray());
                }
            }
            {
                var output = new ArrayBufferWriter<byte>(10);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartArray();

                Assert.Equal(0, yamlUtf8.BytesCommitted);
                Assert.Equal(1, yamlUtf8.BytesPending);

                yamlUtf8.Flush();

                Assert.Equal(1, yamlUtf8.BytesCommitted);
                Assert.Equal(0, yamlUtf8.BytesPending);

                if (options.SkipValidation)
                {
                    yamlUtf8.WriteBase64String("foo", new byte[] { 1, 2 });
                    yamlUtf8.WriteEndObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteBase64String("foo", new byte[] { 1, 2 }));
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteEndObject());
                }
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteBase64DoesNotEscape()
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output);

            var bytes = new byte[3] { 0xFB, 0xEF, 0xBE };
            yamlUtf8.WriteBase64StringValue(bytes);

            yamlUtf8.Flush();

            YamlTestHelper.AssertContents("\"++++\"", output);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteBase64DoesNotEscapeLarge()
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output);

            var bytes = new byte[200];

            bytes.AsSpan().Fill(100);
            bytes[4] = 0xFB;
            bytes[5] = 0xEF;
            bytes[6] = 0xBE;
            bytes[15] = 0;
            bytes[16] = 0x10;
            bytes[17] = 0xBF;

            yamlUtf8.WriteBase64StringValue(bytes);

            yamlUtf8.Flush();

            var builder = new StringBuilder();
            builder.Append("\"ZGRkZPvvvmRkZGRkZGRkABC/");
            for (int i = 0; i < 60; i++)
            {
                builder.Append("ZGRk");
            }
            builder.Append("ZGQ=\"");
            YamlTestHelper.AssertContents(builder.ToString(), output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteInvalidDepthPartial(YamlWriterOptions options)
        {
            {
                    var output = new ArrayBufferWriter<byte>(10);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                Assert.Equal(0, yamlUtf8.CurrentDepth);

                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject());
                }
            }

            {
                    var output = new ArrayBufferWriter<byte>(10);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                if (options.SkipValidation)
                {
                    yamlUtf8.WriteStartObject("name");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => yamlUtf8.WriteStartObject("name"));
                }
            }
        }

        public static IEnumerable<object[]> WriteComments_TestData() =>
            YamlOptionsWith([
                "comment",
                "comm><ent",
                ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>"
            ]);

        [Theory]
        [MemberData(nameof(WriteComments_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteCommentsInArray(YamlWriterOptions options, string comment)
        {
            string expectedStr = GetCommentInArrayExpectedString(options, comment);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                WriteCommentValue(yamlUtf8, i, comment);
                yamlUtf8.WriteStartArray();

                for (int j = 0; j < 10; j++)
                {
                    WriteCommentValue(yamlUtf8, i, comment);
                }

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStringValue(comment);
                        break;
                    case 1:
                        yamlUtf8.WriteStringValue(comment.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(comment));
                        break;
                }

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(WriteComments_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteCommentsInObject(YamlWriterOptions options, string comment)
        {
            string expectedStr = GetCommentInObjectExpectedString(options, comment);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WriteStartObject();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WritePropertyName("property1");
                WriteCommentValue(yamlUtf8, i, comment);
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WritePropertyName("property2");
                WriteCommentValue(yamlUtf8, i, comment);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, comment);

                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        private static void WriteCommentValue(Utf8YamlWriter yamlUtf8, int i, string comment)
        {
            switch (i)
            {
                case 0:
                    yamlUtf8.WriteCommentValue(comment);
                    break;
                case 1:
                    yamlUtf8.WriteCommentValue(comment.AsSpan());
                    break;
                case 2:
                    yamlUtf8.WriteCommentValue(Encoding.UTF8.GetBytes(comment));
                    break;
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteInvalidComment(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(32);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            string comment = "comment is */ invalid";
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(comment));
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(comment.AsSpan()));
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(Encoding.UTF8.GetBytes(comment)));

            comment = "comment with unpaired surrogate \udc00";
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(comment));
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(comment.AsSpan()));

            var invalidUtf8 = new byte[2] { 0xc3, 0x28 };
            Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(invalidUtf8));
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteCommentsInvalidTextAllowed(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(32);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartArray();

            string comment = "comment is * / valid";
            yamlUtf8.WriteCommentValue(comment);
            yamlUtf8.WriteCommentValue(comment.AsSpan());
            yamlUtf8.WriteCommentValue(Encoding.UTF8.GetBytes(comment));

            comment = "comment is /* valid";
            yamlUtf8.WriteCommentValue(comment);
            yamlUtf8.WriteCommentValue(comment.AsSpan());
            yamlUtf8.WriteCommentValue(Encoding.UTF8.GetBytes(comment));

            comment = "comment is / * valid";
            yamlUtf8.WriteCommentValue(comment);
            yamlUtf8.WriteCommentValue(comment.AsSpan());

            yamlUtf8.Flush();
            string expectedStr = GetCommentExpectedString(options);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        private static string GetCommentExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteStartArray();

            string comment = "comment is * / valid";
            yaml.WriteComment(comment);
            yaml.WriteComment(comment);
            yaml.WriteComment(comment);

            comment = "comment is /* valid";
            yaml.WriteComment(comment);
            yaml.WriteComment(comment);
            yaml.WriteComment(comment);

            comment = "comment is / * valid";
            yaml.WriteComment(comment);
            yaml.WriteComment(comment);

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        [Theory]
        [InlineData(true, ExpectedIndentedCommentYamlOfArray)]
        [InlineData(false, ExpectedNonIndentedCommentYamlOfArray)]
        [Trait("category", "jsonoutput")]
        public void WriteCommentsInArray_ComparedWithStringLiteral(bool formatted, string expectedYaml)
        {
            var options = new YamlWriterOptions { Indented = formatted, SkipValidation = false };

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                WriteCommentValue(yamlUtf8, i, "Comment at start of doc");
                WriteCommentValue(yamlUtf8, i, "Multiple comment line");
                yamlUtf8.WriteStartArray();

                WriteCommentValue(yamlUtf8, i, "Comment as first array item");
                WriteCommentValue(yamlUtf8, i, "Multiple comment line");

                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, "Comment in the middle of array");

                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, "Comment as the last array item");

                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, "Comment at end of doc");

                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedYaml, output);
            }
        }

        [Theory]
        [InlineData(true, ExpectedIndentedCommentYamlOfObject)]
        [InlineData(false, ExpectedNonIndentedCommentYamlOfObject)]
        [Trait("category", "jsonoutput")]
        public void WriteCommentsInObject_ComparedWithStringLiteral(bool formatted, string expectedYaml)
        {
            var options = new YamlWriterOptions { Indented = formatted, SkipValidation = false };

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(32);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                WriteCommentValue(yamlUtf8, i, "Comment at start of doc");
                WriteCommentValue(yamlUtf8, i, "Multiple comment line");
                yamlUtf8.WriteStartObject();

                WriteCommentValue(yamlUtf8, i, "Comment before first object property");
                WriteCommentValue(yamlUtf8, i, "Multiple comment line");

                yamlUtf8.WritePropertyName("property1");
                WriteCommentValue(yamlUtf8, i, "Comment of string property value");
                yamlUtf8.WriteStringValue("stringValue");

                yamlUtf8.WritePropertyName("property2");
                WriteCommentValue(yamlUtf8, i, "Comment of object property value");
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, "Comment in the middle of object");

                yamlUtf8.WritePropertyName("property3");
                WriteCommentValue(yamlUtf8, i, "Comment of array property value");
                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndArray();

                WriteCommentValue(yamlUtf8, i, "Comment after the last property");

                yamlUtf8.WriteEndObject();

                WriteCommentValue(yamlUtf8, i, "Comment at end of doc");

                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedYaml, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStrings(YamlWriterOptions options)
        {
            string value = "temp";
            string expectedStr = GetStringsExpectedString(options, value);
            
            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartArray();

                for (int j = 0; j < 10; j++)
                {
                    switch (i)
                    {
                        case 0:
                            yamlUtf8.WriteStringValue(value);
                            break;
                        case 1:
                            yamlUtf8.WriteStringValue(value.AsSpan());
                            break;
                        case 2:
                            yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(value));
                            break;
                    }
                }

                yamlUtf8.WriteEndArray();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteHelloWorldEscaped_AdditionalCases_TestData() =>
            YamlOptionsWith([
                "mess\nage",
                "message"
            ],
            [
                "Hello, \nWorld!",
                "Hello, World!"
            ]);

        [Theory]
        [MemberData(nameof(WriteHelloWorldEscaped_AdditionalCases_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteHelloWorldEscaped_AdditionalCases(YamlWriterOptions options, string key, string value)
        {
            string expectedStr = GetEscapedExpectedString(options, key, value, StringEscapeHandling.EscapeHtml);

            byte[] keyUtf8 = Encoding.UTF8.GetBytes(key);
            byte[] valueUtf8 = Encoding.UTF8.GetBytes(value);

            YamlEncodedText encodedKey = YamlEncodedText.Encode(key);
            YamlEncodedText encodedValue = YamlEncodedText.Encode(value);

            for (int i = 0; i < 32; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(key, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(key, value.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteString(key, valueUtf8);
                        break;
                    case 3:
                        yamlUtf8.WriteString(key.AsSpan(), value);
                        break;
                    case 4:
                        yamlUtf8.WriteString(key.AsSpan(), value.AsSpan());
                        break;
                    case 5:
                        yamlUtf8.WriteString(key.AsSpan(), valueUtf8);
                        break;
                    case 6:
                        yamlUtf8.WriteString(keyUtf8, value);
                        break;
                    case 7:
                        yamlUtf8.WriteString(keyUtf8, value.AsSpan());
                        break;
                    case 8:
                        yamlUtf8.WriteString(keyUtf8, valueUtf8);
                        break;
                    case 9:
                        yamlUtf8.WriteString(encodedKey, value);
                        break;
                    case 10:
                        yamlUtf8.WriteString(encodedKey, value.AsSpan());
                        break;
                    case 11:
                        yamlUtf8.WriteString(encodedKey, valueUtf8);
                        break;
                    case 12:
                        yamlUtf8.WriteString(encodedKey, encodedValue);
                        break;
                    case 13:
                        yamlUtf8.WriteString(key, encodedValue);
                        break;
                    case 14:
                        yamlUtf8.WriteString(key.AsSpan(), encodedValue);
                        break;
                    case 15:
                        yamlUtf8.WriteString(keyUtf8, encodedValue);
                        break;
                    case 16:
                        yamlUtf8.WritePropertyName(key);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 17:
                        yamlUtf8.WritePropertyName(key);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 18:
                        yamlUtf8.WritePropertyName(key);
                        yamlUtf8.WriteStringValue(valueUtf8);
                        break;
                    case 19:
                        yamlUtf8.WritePropertyName(key.AsSpan());
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 20:
                        yamlUtf8.WritePropertyName(key.AsSpan());
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 21:
                        yamlUtf8.WritePropertyName(key.AsSpan());
                        yamlUtf8.WriteStringValue(valueUtf8);
                        break;
                    case 22:
                        yamlUtf8.WritePropertyName(keyUtf8);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 23:
                        yamlUtf8.WritePropertyName(keyUtf8);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 24:
                        yamlUtf8.WritePropertyName(keyUtf8);
                        yamlUtf8.WriteStringValue(valueUtf8);
                        break;
                    case 25:
                        yamlUtf8.WritePropertyName(encodedKey);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 26:
                        yamlUtf8.WritePropertyName(encodedKey);
                        yamlUtf8.WriteStringValue(value.AsSpan());
                        break;
                    case 27:
                        yamlUtf8.WritePropertyName(encodedKey);
                        yamlUtf8.WriteStringValue(valueUtf8);
                        break;
                    case 28:
                        yamlUtf8.WritePropertyName(encodedKey);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 29:
                        yamlUtf8.WritePropertyName(key);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 30:
                        yamlUtf8.WritePropertyName(key.AsSpan());
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                    case 31:
                        yamlUtf8.WritePropertyName(keyUtf8);
                        yamlUtf8.WriteStringValue(encodedValue);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void EscapeAsciiCharacters(YamlWriterOptions options)
        {
            var propertyArray = new char[128];

            char[] specialCases = { '+', '`', (char)0x7F, '/' };
            for (int i = 0; i < propertyArray.Length; i++)
            {
                if (Array.IndexOf(specialCases, (char)i) != -1)
                {
                    propertyArray[i] = (char)0;
                }
                else
                {
                    propertyArray[i] = (char)i;
                }
            }

            string propertyName = new string(propertyArray);
            string value = new string(propertyArray);

            string expectedStr = GetEscapedExpectedString(options, propertyName, value, StringEscapeHandling.EscapeHtml);

            for (int i = 0; i < 6; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(Encoding.UTF8.GetBytes(propertyName), Encoding.UTF8.GetBytes(value));
                        break;
                    case 2:
                        yamlUtf8.WriteString(YamlEncodedText.Encode(propertyName), YamlEncodedText.Encode(value));
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 4:
                        yamlUtf8.WritePropertyName(Encoding.UTF8.GetBytes(propertyName));
                        yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(value));
                        break;
                    case 5:
                        yamlUtf8.WritePropertyName(YamlEncodedText.Encode(propertyName));
                        yamlUtf8.WriteStringValue(YamlEncodedText.Encode(value));
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [OuterLoop("Too slow", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        [Trait("category", "jsonoutput")]
        public void EscapeCharacters(YamlWriterOptions options)
        {
            // Do not include surrogate pairs.
            var propertyArray = new char[0xD800 + (0xFFFF - 0xE000) + 1];

            for (int i = 128; i < propertyArray.Length; i++)
            {
                if (i < 0xD800 || i > 0xDFFF)
                {
                    propertyArray[i] = (char)i;
                }
            }

            string propertyName = new string(propertyArray);
            string value = new string(propertyArray);

            string expectedStr = GetEscapedExpectedString(options, propertyName, value, StringEscapeHandling.EscapeNonAscii);

            for (int i = 0; i < 6; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(Encoding.UTF8.GetBytes(propertyName), Encoding.UTF8.GetBytes(value));
                        break;
                    case 2:
                        yamlUtf8.WriteString(YamlEncodedText.Encode(propertyName), YamlEncodedText.Encode(value));
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 4:
                        yamlUtf8.WritePropertyName(Encoding.UTF8.GetBytes(propertyName));
                        yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(value));
                        break;
                    case 5:
                        yamlUtf8.WritePropertyName(YamlEncodedText.Encode(propertyName));
                        yamlUtf8.WriteStringValue(YamlEncodedText.Encode(value));
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void HighSurrogateMissingGetsReplaced(YamlWriterOptions options)
        {
            var propertyArray = new char[10] { 'a', (char)0xD800, (char)0xDC00, (char)0xD803, (char)0xDE6D, (char)0xD834, (char)0xDD1E, (char)0xDBFF, (char)0xDFFF, 'a' };

            string propertyName = new string(propertyArray);
            string value = new string(propertyArray);

            string expectedStr = GetEscapedExpectedString(options, propertyName, value, StringEscapeHandling.EscapeNonAscii);

            for (int i = 0; i < 6; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(propertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(Encoding.UTF8.GetBytes(propertyName), Encoding.UTF8.GetBytes(value));
                        break;
                    case 2:
                        yamlUtf8.WriteString(YamlEncodedText.Encode(propertyName), YamlEncodedText.Encode(value));
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(propertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 4:
                        yamlUtf8.WritePropertyName(Encoding.UTF8.GetBytes(propertyName));
                        yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(value));
                        break;
                    case 5:
                        yamlUtf8.WritePropertyName(YamlEncodedText.Encode(propertyName));
                        yamlUtf8.WriteStringValue(YamlEncodedText.Encode(value));
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void EscapeSurrogatePairs()
        {
            string propertyName = "a \uD800\uDC00\uDE6D a";
            string value = propertyName;
            var output = new ArrayBufferWriter<byte>(12);
            using (var yamlUtf8 = new Utf8YamlWriter(output))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(propertyName, value);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{\"a \\uD800\\uDC00\\uFFFD a\":\"a \\uD800\\uDC00\\uFFFD a\"}", output);
        }

        private static readonly byte[] s_InvalidUtf8Input = new byte[2] { 0xc3, 0x28 };
        private const string InvalidUtf8Expected = "\"\\uFFFD(\"";

        private static readonly byte[] s_ValidUtf8Input = new byte[2] { 0xc3, 0xb1 }; // 0xF1
        private const string ValidUtf8Expected = "\"\\u00F1\"";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf8SurrogatePairReplacement_InvalidPropertyName_InvalidValue(bool skipValidation)
        {
            // SkipValidation does not affect whether we write the replacement character or not (we always do, unless we add a new option to control).
            // Comment also applies to other Utf8SurrogatePairReplacement* tests below.
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_InvalidUtf8Input, s_InvalidUtf8Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + InvalidUtf8Expected + ":" + InvalidUtf8Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf8SurrogatePairReplacement_InvalidPropertyName_ValidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_InvalidUtf8Input, s_ValidUtf8Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + InvalidUtf8Expected + ":" + ValidUtf8Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf8SurrogatePairReplacement_ValidPropertyName_InvalidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_ValidUtf8Input, s_InvalidUtf8Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + ValidUtf8Expected + ":" + InvalidUtf8Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf8SurrogatePairReplacement_ValidPropertyName_ValidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_ValidUtf8Input, s_ValidUtf8Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + ValidUtf8Expected + ":" + ValidUtf8Expected + "}", output);
        }

        private static readonly string s_InvalidUtf16Input = new string(new char[2] { (char)0xD801, 'a' });
        private const string InvalidUtf16Expected = "\"\\uFFFDa\"";

        private static readonly string s_ValidUtf16Input = new string(new char[2] { (char)0xD801, (char)0xDC37 }); // 0x10437
        private const string ValidUtf16Expected = "\"\\uD801\\uDC37\"";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf16SurrogatePairReplacement_InvalidPropertyName_InvalidValue(bool skipValidation)
        {
            // SkipValidation does not affect whether we write the replacement character or not (we always do, unless we add a new option to control).
            // Comment also applies to other Utf16SurrogatePairReplacement* tests below.
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_InvalidUtf16Input, s_InvalidUtf16Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + InvalidUtf16Expected + ":" + InvalidUtf16Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf16SurrogatePairReplacement_InvalidPropertyName_ValidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_InvalidUtf16Input, s_ValidUtf16Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + InvalidUtf16Expected + ":" + ValidUtf16Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf16SurrogatePairReplacement_ValidPropertyName_InvalidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_ValidUtf16Input, s_InvalidUtf16Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + ValidUtf16Expected + ":" + InvalidUtf16Expected + "}", output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void Utf16SurrogatePairReplacement_ValidPropertyName_ValidValue(bool skipValidation)
        {
            var options = new YamlWriterOptions { SkipValidation = skipValidation };

            var output = new ArrayBufferWriter<byte>(1024);
            using (var yamlUtf8 = new Utf8YamlWriter(output, options))
            {
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteString(s_ValidUtf16Input, s_ValidUtf16Input);
                yamlUtf8.WriteEndObject();
            }

            YamlTestHelper.AssertContents("{" + ValidUtf16Expected + ":" + ValidUtf16Expected + "}", output);
        }

        // Test case from https://github.com/dotnet/runtime/issues/30727
        [Fact]
        [Trait("category", "jsonoutput")]
        public void OutputConsistentWithYamlEncodedText()
        {
            string yamlEncodedText = $"{{\"{YamlEncodedText.Encode("propertyName+1")}\":\"{YamlEncodedText.Encode("value+1")}\"}}";

            var output = new ArrayBufferWriter<byte>(1024);

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();
                writer.WriteString("propertyName+1", "value+1");
                writer.WriteEndObject();
            }

            YamlTestHelper.AssertContents(yamlEncodedText, output);
        }

        public static IEnumerable<object[]> YamlEncodedTextStringsCustomAll
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA", "{\"Prop\":\"\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA\"}" },
                    new object[] { "a\u0467\u0466a", "{\"Prop\":\"a\u0467\u0466a\"}" },
                };
            }
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStringsCustomAll))]
        [Trait("category", "jsonoutput")]
        public void CustomEscaper(string value, string expectedStr)
        {
            const string PropertyName = "Prop";

            // Allow all unicode values (except forbidden characters which we don't have in test data here)
            JavaScriptEncoder encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

            var options = new YamlWriterOptions();
            options.Encoder = encoder;

            for (int i = 0; i < 6; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(PropertyName, value);
                        break;
                    case 1:
                        yamlUtf8.WriteString(Encoding.UTF8.GetBytes(PropertyName), Encoding.UTF8.GetBytes(value));
                        break;
                    case 2:
                        yamlUtf8.WriteString(YamlEncodedText.Encode(PropertyName), YamlEncodedText.Encode(value, encoder));
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(PropertyName);
                        yamlUtf8.WriteStringValue(value);
                        break;
                    case 4:
                        yamlUtf8.WritePropertyName(Encoding.UTF8.GetBytes(PropertyName));
                        yamlUtf8.WriteStringValue(Encoding.UTF8.GetBytes(value));
                        break;
                    case 5:
                        yamlUtf8.WritePropertyName(YamlEncodedText.Encode(PropertyName, encoder));
                        yamlUtf8.WriteStringValue(YamlEncodedText.Encode(value, encoder));
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                string result = Encoding.UTF8.GetString(
                        output.WrittenSpan
#if NETFRAMEWORK
                        .ToArray()
#endif
                    );

                Assert.Equal(expectedStr, result);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteCustomStrings(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>(10);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartObject();

            for (int i = 0; i < 1_000; i++)
            {
                yamlUtf8.WriteString("message", "Hello, World!");
            }

            yamlUtf8.WriteEndObject();
            yamlUtf8.Flush();

            YamlTestHelper.AssertContents(GetCustomExpectedString(options), output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStartEnd(YamlWriterOptions options)
        {
            string expectedStr = GetStartEndExpectedString(options);

            var output = new ArrayBufferWriter<byte>(1024);

            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartArray();
            yamlUtf8.WriteStartObject();
            yamlUtf8.WriteEndObject();
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("category", "jsonoutput")]
        public void WriteStartEndInvalid(bool formatted)
        {
            {
                string expectedStr = "[}";

                var options = new YamlWriterOptions { Indented = formatted, SkipValidation = true };
                var output = new ArrayBufferWriter<byte>(1024);

                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartArray();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }

            {
                string expectedStr = "{]";

                var options = new YamlWriterOptions { Indented = formatted, SkipValidation = true };
                var output = new ArrayBufferWriter<byte>(1024);

                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteEndArray();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStartEndWithPropertyNameArray(YamlWriterOptions options)
        {
            string expectedStr = GetStartEndWithPropertyArrayExpectedString(options);

            
            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStartArray("property name");
                        break;
                    case 1:
                        yamlUtf8.WriteStartArray("property name".AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStartArray("property name"u8);
                        break;
                }

                yamlUtf8.WriteEndArray();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteStartEndWithPropertyName_TestData() =>
            YamlOptionsWith([
                10,
                100
            ]);

        [Theory]
        [MemberData(nameof(WriteStartEndWithPropertyName_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStartEndWithPropertyNameArrayDifferentKeyLengths(YamlWriterOptions options, int keyLength)
        {
            var keyChars = new char[keyLength];
            for (int i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = '<';
            }
            var key = new string(keyChars);

            string expectedStr = GetStartEndWithPropertyArrayExpectedString(key, options, escape: true);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStartArray(key);
                        break;
                    case 1:
                        yamlUtf8.WriteStartArray(key.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStartArray(Encoding.UTF8.GetBytes(key));
                        break;
                }

                yamlUtf8.WriteEndArray();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStartEndWithPropertyNameObject(YamlWriterOptions options)
        {
            string expectedStr = GetStartEndWithPropertyObjectExpectedString(options);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStartObject("property name");
                        break;
                    case 1:
                        yamlUtf8.WriteStartObject("property name".AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStartObject("property name"u8);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(WriteStartEndWithPropertyName_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteStartEndWithPropertyNameObjectDifferentKeyLengths(YamlWriterOptions options, int keyLength)
        {
            var keyChars = new char[keyLength];
            for (int i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = '<';
            }
            var key = new string(keyChars);

            string expectedStr = GetStartEndWithPropertyObjectExpectedString(key, options, escape: true);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStartObject(key);
                        break;
                    case 1:
                        yamlUtf8.WriteStartObject(key.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStartObject(Encoding.UTF8.GetBytes(key));
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteArrayWithProperty(YamlWriterOptions options)
        {
            string expectedStr = GetArrayWithPropertyExpectedString(options);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);

                using var yamlUtf8 = new Utf8YamlWriter(output, options);
                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteStartArray("message");
                        break;
                    case 1:
                        yamlUtf8.WriteStartArray("message".AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteStartArray("message"u8);
                        break;
                }

                yamlUtf8.WriteEndArray();
                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteBooleanValue_TestData() =>
            YamlOptionsWith([
                true,
                false
            ],
            [
                "message",
                "mess><age",
                ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>"
            ]);

        [Theory]
        [MemberData(nameof(WriteBooleanValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteBooleanValue(YamlWriterOptions options, bool value, string keyString)
        {
            string expectedStr = GetBooleanExpectedString(options, keyString, value, escape: true);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteBoolean(keyString, value);
                        break;
                    case 1:
                        yamlUtf8.WriteBoolean(keyString.AsSpan(), value);
                        break;
                    case 2:
                        yamlUtf8.WriteBoolean(Encoding.UTF8.GetBytes(keyString), value);
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(keyString);
                        yamlUtf8.WriteBooleanValue(value);
                        break;
                }

                yamlUtf8.WriteStartArray("temp");
                yamlUtf8.WriteBooleanValue(true);
                yamlUtf8.WriteBooleanValue(true);
                yamlUtf8.WriteBooleanValue(false);
                yamlUtf8.WriteBooleanValue(false);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteValue_TestData() =>
            YamlOptionsWith([
                "message",
                "mess><age",
                ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>"
            ]);

        [Theory]
        [MemberData(nameof(WriteValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNullValue(YamlWriterOptions options, string keyString)
        {
            string expectedStr = GetNullExpectedString(options, keyString, escape: true);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(16);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteNull(keyString);
                        yamlUtf8.WriteNull(keyString);
                        break;
                    case 1:
                        yamlUtf8.WriteNull(keyString.AsSpan());
                        yamlUtf8.WriteNull(keyString.AsSpan());
                        break;
                    case 2:
                        yamlUtf8.WriteNull(Encoding.UTF8.GetBytes(keyString));
                        yamlUtf8.WriteNull(Encoding.UTF8.GetBytes(keyString));
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName(keyString);
                        yamlUtf8.WriteNullValue();
                        yamlUtf8.WritePropertyName(keyString);
                        yamlUtf8.WriteNullValue();
                        break;
                }

                yamlUtf8.WriteStartArray("temp");
                yamlUtf8.WriteNullValue();
                yamlUtf8.WriteNullValue();
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteIntegerValue_TestData() =>
            YamlOptionsWith([
                0,
               -1,
                1,
                int.MaxValue,
                int.MinValue,
                12345
            ]);

        [Theory]
        [MemberData(nameof(WriteIntegerValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteIntegerValue(YamlWriterOptions options, int value)
        {
            string expectedStr = GetPropertyExpectedString(options, value);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteNumber("message", value);
                        break;
                    case 1:
                        yamlUtf8.WriteNumber("message".AsSpan(), value);
                        break;
                    case 2:
                        yamlUtf8.WriteNumber("message"u8, value);
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName("message");
                        yamlUtf8.WriteNumberValue(value);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteFloatValue_TestData() =>
            YamlOptionsWith([
                float.MinValue,
                float.MaxValue
            ]);

        [Theory]
        [MemberData(nameof(WriteFloatValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteFloatValue(YamlWriterOptions options, float value)
        {
            string expectedStr = GetPropertyExpectedString(options, value);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteNumber("message", value);
                        break;
                    case 1:
                        yamlUtf8.WriteNumber("message".AsSpan(), value);
                        break;
                    case 2:
                        yamlUtf8.WriteNumber("message"u8, value);
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName("message");
                        yamlUtf8.WriteNumberValue(value);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        public static IEnumerable<object[]> WriteDoubleValue_TestData() =>
            YamlOptionsWith([
                double.MinValue,
                double.MaxValue
            ]);

        [Theory]
        [MemberData(nameof(WriteDoubleValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteDoubleValue(YamlWriterOptions options, double value)
        {
            string expectedStr = GetPropertyExpectedString(options, value);

            for (int i = 0; i < 4; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteNumber("message", value);
                        break;
                    case 1:
                        yamlUtf8.WriteNumber("message".AsSpan(), value);
                        break;
                    case 2:
                        yamlUtf8.WriteNumber("message"u8, value);
                        break;
                    case 3:
                        yamlUtf8.WritePropertyName("message");
                        yamlUtf8.WriteNumberValue(value);
                        break;
                }

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [MemberData(nameof(WriteValue_TestData))]
        [OuterLoop("Too slow", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/129973", typeof(PlatformDetection), nameof(PlatformDetection.IsBrowser), nameof(PlatformDetection.IsCoreCLR))]
        [Trait("category", "jsonoutput")]
        public void WriteNumbers(YamlWriterOptions options, string keyString)
        {
            var random = new Random(42);
            const int numberOfItems = 1_000;

            var ints = new int[numberOfItems];
            ints[0] = 0;
            ints[1] = int.MaxValue;
            ints[2] = int.MinValue;
            ints[3] = 12345;
            ints[4] = -12345;
            for (int i = 5; i < numberOfItems; i++)
            {
                ints[i] = random.Next(int.MinValue, int.MaxValue);
            }

            var uints = new uint[numberOfItems];
            uints[0] = uint.MaxValue;
            uints[1] = uint.MinValue;
            uints[2] = 3294967295;
            for (int i = 3; i < numberOfItems; i++)
            {
                uint thirtyBits = (uint)random.Next(1 << 30);
                uint twoBits = (uint)random.Next(1 << 2);
                uint fullRange = (thirtyBits << 2) | twoBits;
                uints[i] = fullRange;
            }

            var longs = new long[numberOfItems];
            longs[0] = 0;
            longs[1] = long.MaxValue;
            longs[2] = long.MinValue;
            longs[3] = 12345678901;
            longs[4] = -12345678901;
            for (int i = 5; i < numberOfItems; i++)
            {
                long value = random.Next(int.MinValue, int.MaxValue);
                value += value < 0 ? int.MinValue : int.MaxValue;
                longs[i] = value;
            }

            var ulongs = new ulong[numberOfItems];
            ulongs[0] = ulong.MaxValue;
            ulongs[1] = ulong.MinValue;
            ulongs[2] = 10446744073709551615;
            for (int i = 3; i < numberOfItems; i++)
            {

            }

            var doubles = new double[numberOfItems * 2];
            doubles[0] = 0.00;
            doubles[1] = double.MaxValue;
            doubles[2] = double.MinValue;
            doubles[3] = 12.345e1;
            doubles[4] = -123.45e1;

            for (int i = 5; i < numberOfItems; i++)
            {
                var value = random.NextDouble();
                if (value < 0.5)
                {
                    doubles[i] = random.NextDouble() * double.MinValue;
                }
                else
                {
                    doubles[i] = random.NextDouble() * double.MaxValue;
                }
            }

            for (int i = numberOfItems; i < numberOfItems * 2; i++)
            {
                var value = random.NextDouble();
                if (value < 0.5)
                {
                    doubles[i] = random.NextDouble() * -1_000_000;
                }
                else
                {
                    doubles[i] = random.NextDouble() * 1_000_000;
                }
            }

            var floats = new float[numberOfItems];
            floats[0] = 0.00f;
            floats[1] = float.MaxValue;
            floats[2] = float.MinValue;
            floats[3] = 12.345e1f;
            floats[4] = -123.45e1f;
            for (int i = 5; i < numberOfItems; i++)
            {
                double mantissa = (random.NextDouble() * 2.0) - 1.0;
                double exponent = Math.Pow(2.0, random.Next(-126, 128));
                floats[i] = (float)(mantissa * exponent);
            }

            var decimals = new decimal[numberOfItems * 2];
            decimals[0] = (decimal)0.00;
            decimals[1] = decimal.MaxValue;
            decimals[2] = decimal.MinValue;
            decimals[3] = (decimal)12.345e1;
            decimals[4] = (decimal)-123.45e1;
            for (int i = 5; i < numberOfItems; i++)
            {
                var value = random.NextDouble();
                if (value < 0.5)
                {
                    decimals[i] = (decimal)(random.NextDouble() * -78E14);
                }
                else
                {
                    decimals[i] = (decimal)(random.NextDouble() * 78E14);
                }
            }

            for (int i = numberOfItems; i < numberOfItems * 2; i++)
            {
                var value = random.NextDouble();
                if (value < 0.5)
                {
                    decimals[i] = (decimal)(random.NextDouble() * -1_000_000);
                }
                else
                {
                    decimals[i] = (decimal)(random.NextDouble() * 1_000_000);
                }
            }

            string expectedStr = GetNumbersExpectedString(options, keyString, ints, uints, longs, ulongs, floats, doubles, decimals, escape: false);

            for (int j = 0; j < 3; j++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                ReadOnlySpan<char> keyUtf16 = keyString.AsSpan();
                ReadOnlySpan<byte> keyUtf8 = Encoding.UTF8.GetBytes(keyString);

                yamlUtf8.WriteStartObject();

                switch (j)
                {
                    case 0:
                        for (int i = 0; i < floats.Length; i++)
                            yamlUtf8.WriteNumber(keyString, floats[i]);
                        for (int i = 0; i < ints.Length; i++)
                            yamlUtf8.WriteNumber(keyString, ints[i]);
                        for (int i = 0; i < uints.Length; i++)
                            yamlUtf8.WriteNumber(keyString, uints[i]);
                        for (int i = 0; i < doubles.Length; i++)
                            yamlUtf8.WriteNumber(keyString, doubles[i]);
                        for (int i = 0; i < longs.Length; i++)
                            yamlUtf8.WriteNumber(keyString, longs[i]);
                        for (int i = 0; i < ulongs.Length; i++)
                            yamlUtf8.WriteNumber(keyString, ulongs[i]);
                        for (int i = 0; i < decimals.Length; i++)
                            yamlUtf8.WriteNumber(keyString, decimals[i]);
                        yamlUtf8.WriteStartArray(keyString);
                        break;
                    case 1:
                        for (int i = 0; i < floats.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, floats[i]);
                        for (int i = 0; i < ints.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, ints[i]);
                        for (int i = 0; i < uints.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, uints[i]);
                        for (int i = 0; i < doubles.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, doubles[i]);
                        for (int i = 0; i < longs.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, longs[i]);
                        for (int i = 0; i < ulongs.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, ulongs[i]);
                        for (int i = 0; i < decimals.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf16, decimals[i]);
                        yamlUtf8.WriteStartArray(keyUtf16);
                        break;
                    case 2:
                        for (int i = 0; i < floats.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, floats[i]);
                        for (int i = 0; i < ints.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, ints[i]);
                        for (int i = 0; i < uints.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, uints[i]);
                        for (int i = 0; i < doubles.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, doubles[i]);
                        for (int i = 0; i < longs.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, longs[i]);
                        for (int i = 0; i < ulongs.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, ulongs[i]);
                        for (int i = 0; i < decimals.Length; i++)
                            yamlUtf8.WriteNumber(keyUtf8, decimals[i]);
                        yamlUtf8.WriteStartArray(keyUtf8);
                        break;
                }

                yamlUtf8.WriteNumberValue(floats[0]);
                yamlUtf8.WriteNumberValue(ints[0]);
                yamlUtf8.WriteNumberValue(uints[0]);
                yamlUtf8.WriteNumberValue(doubles[0]);
                yamlUtf8.WriteNumberValue(longs[0]);
                yamlUtf8.WriteNumberValue(ulongs[0]);
                yamlUtf8.WriteNumberValue(decimals[0]);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueInt32(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            int value = 1234567;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueInt64(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            long value = 1234567;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueUInt32(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            uint value = 1234567;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueUInt64(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            ulong value = 1234567;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(WriteFloatValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueSingle(YamlWriterOptions options, float value)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(WriteDoubleValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueDouble(YamlWriterOptions options, double value)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [MemberData(nameof(YamlOptions_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteNumberValueDecimal(YamlWriterOptions options)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            int numberOfElements = 0;
            yamlUtf8.WriteStartArray();
            int currentCapactiy = output.Capacity;
            decimal value = 1234567.0M;
            while (currentCapactiy == output.Capacity)
            {
                yamlUtf8.WriteNumberValue(value);
                numberOfElements++;
            }
            Assert.Equal(currentCapactiy + 4096, output.Capacity);
            yamlUtf8.WriteEndArray();
            yamlUtf8.Flush();

            string expectedStr = GetNumbersExpectedString(options, numberOfElements, value);
            YamlTestHelper.AssertContents(expectedStr, output);
        }

        [Theory]
        [InlineData(true, true, "message", true)]
        [InlineData(true, false, "message", true)]
        [InlineData(false, true, "message", true)]
        [InlineData(false, false, "message", true)]
        [InlineData(true, true, "mess><age", false)]
        [InlineData(true, false, "mess><age", false)]
        [InlineData(false, true, "mess><age", false)]
        [InlineData(false, false, "mess><age", false)]
        [InlineData(true, true, ">><++>>>\">>\\>>&>>>\u6f22\u5B57>>>", false)]
        [InlineData(true, false, ">><++>>>\">>\\>>&>>>\u6f22\u5B57>>>", false)]
        [InlineData(false, true, ">><++>>>\">>\\>>&>>>\u6f22\u5B57>>>", false)]
        [InlineData(false, false, ">><++>>>\">>\\>>&>>>\u6f22\u5B57>>>", false)]
        [InlineData(true, true, "mess\r\nage\u0008\u0001!", true)]
        [InlineData(true, false, "mess\r\nage\u0008\u0001!", true)]
        [InlineData(false, true, "mess\r\nage\u0008\u0001!", true)]
        [InlineData(false, false, "mess\r\nage\u0008\u0001!", true)]
        [Trait("category", "jsonoutput")]
        public void WriteStringsWithRelaxedEscaping(bool formatted, bool skipValidation, string keyString, bool matchesRelaxedEscaping)
        {
            string expectedStr = GetExpectedString_RelaxedEscaping(prettyPrint: formatted, keyString);

            var options = new YamlWriterOptions { Indented = formatted, SkipValidation = skipValidation, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            WriteStringHelper(options, keyString, expectedStr, shouldMatch: true);

            // Default encoder
            options = new YamlWriterOptions { Indented = formatted, SkipValidation = skipValidation };
            WriteStringHelper(options, keyString, expectedStr, matchesRelaxedEscaping);
        }

        private static void WriteStringHelper(YamlWriterOptions options, string keyString, string expectedStr, bool shouldMatch)
        {
            ReadOnlySpan<char> keyUtf16 = keyString.AsSpan();
            ReadOnlySpan<byte> keyUtf8 = Encoding.UTF8.GetBytes(keyString);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        yamlUtf8.WriteString(keyString, keyString);
                        yamlUtf8.WriteStartArray(keyString);
                        break;
                    case 1:
                        yamlUtf8.WriteString(keyUtf16, keyString);
                        yamlUtf8.WriteStartArray(keyUtf16);
                        break;
                    case 2:
                        yamlUtf8.WriteString(keyUtf8, keyString);
                        yamlUtf8.WriteStartArray(keyUtf8);
                        break;
                }

                yamlUtf8.WriteStringValue(keyString);
                yamlUtf8.WriteStringValue(keyString);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                if (shouldMatch)
                {
                    YamlTestHelper.AssertContents(expectedStr, output);
                }
                else
                {
                    YamlTestHelper.AssertContentsNotEqual(expectedStr, output, skipSpecialRules: true);
                }
            }
        }

        [Theory]
        [MemberData(nameof(WriteValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteGuidsValue(YamlWriterOptions options, string keyString)
        {
            const int numberOfItems = 1_000;

            var guids = new Guid[numberOfItems];
            for (int i = 0; i < numberOfItems; i++)
            {
                guids[i] = Guid.NewGuid();
            }

            string expectedStr = GetGuidsExpectedString(options, keyString, guids, escape: true);

            ReadOnlySpan<char> keyUtf16 = keyString.AsSpan();
            ReadOnlySpan<byte> keyUtf8 = Encoding.UTF8.GetBytes(keyString);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyString, guids[j]);
                        yamlUtf8.WriteStartArray(keyString);
                        break;
                    case 1:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf16, guids[j]);
                        yamlUtf8.WriteStartArray(keyUtf16);
                        break;
                    case 2:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf8, guids[j]);
                        yamlUtf8.WriteStartArray(keyUtf8);
                        break;
                }

                yamlUtf8.WriteStringValue(guids[0]);
                yamlUtf8.WriteStringValue(guids[1]);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(WriteValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteDateTimesValue(YamlWriterOptions options, string keyString)
        {
            var random = new Random(42);
            const int numberOfItems = 1_000;

            var start = new DateTime(1995, 1, 1);
            int range = (DateTime.Today - start).Days;

            var dates = new DateTime[numberOfItems];
            for (int i = 0; i < numberOfItems; i++)
                dates[i] = start.AddDays(random.Next(range));

            string expectedStr = GetDatesExpectedString(options, keyString, dates, escape: true);

            ReadOnlySpan<char> keyUtf16 = keyString.AsSpan();
            ReadOnlySpan<byte> keyUtf8 = Encoding.UTF8.GetBytes(keyString);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyString, dates[j]);
                        yamlUtf8.WriteStartArray(keyString);
                        break;
                    case 1:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf16, dates[j]);
                        yamlUtf8.WriteStartArray(keyUtf16);
                        break;
                    case 2:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf8, dates[j]);
                        yamlUtf8.WriteStartArray(keyUtf8);
                        break;
                }

                yamlUtf8.WriteStringValue(dates[0]);
                yamlUtf8.WriteStringValue(dates[1]);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        [Theory]
        [MemberData(nameof(WriteValue_TestData))]
        [Trait("category", "jsonoutput")]
        public void WriteDateTimeOffsetsValue(YamlWriterOptions options, string keyString)
        {
            var random = new Random(42);
            const int numberOfItems = 1_000;

            var start = new DateTime(1995, 1, 1);
            int range = (DateTime.Today - start).Days;

            var dates = new DateTimeOffset[numberOfItems];
            for (int i = 0; i < numberOfItems; i++)
                dates[i] = new DateTimeOffset(start.AddDays(random.Next(range)));

            string expectedStr = GetDatesExpectedString(options, keyString, dates, escape: true);

            ReadOnlySpan<char> keyUtf16 = keyString.AsSpan();
            ReadOnlySpan<byte> keyUtf8 = Encoding.UTF8.GetBytes(keyString);

            for (int i = 0; i < 3; i++)
            {
                var output = new ArrayBufferWriter<byte>(1024);
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartObject();

                switch (i)
                {
                    case 0:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyString, dates[j]);
                        yamlUtf8.WriteStartArray(keyString);
                        break;
                    case 1:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf16, dates[j]);
                        yamlUtf8.WriteStartArray(keyUtf16);
                        break;
                    case 2:
                        for (int j = 0; j < numberOfItems; j++)
                            yamlUtf8.WriteString(keyUtf8, dates[j]);
                        yamlUtf8.WriteStartArray(keyUtf8);
                        break;
                }

                yamlUtf8.WriteStringValue(dates[0]);
                yamlUtf8.WriteStringValue(dates[1]);
                yamlUtf8.WriteEndArray();

                yamlUtf8.WriteEndObject();
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedStr, output);
            }
        }

        // NOTE: WriteLargeKeyOrValue test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WriteLargeKeyOrValue(YamlWriterOptions options)
        {
            try
            {
                byte[] key;
                byte[] value;

                key = new byte[MaxUnescapedTokenSize + 1];
                value = new byte[MaxUnescapedTokenSize + 1];

                key.AsSpan().Fill((byte)'a');
                value.AsSpan().Fill((byte)'b');

                {
                    var output = new ArrayBufferWriter<byte>(1024);
                    using var yamlUtf8 = new Utf8YamlWriter(output, options);
                    yamlUtf8.WriteStartObject();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(key, DateTimeTestHelpers.FixedDateTimeValue));
                    Assert.Equal(0, output.WrittenCount);
                }

                {
                    var output = new ArrayBufferWriter<byte>(1024);
                    using var yamlUtf8 = new Utf8YamlWriter(output, options);
                    yamlUtf8.WriteStartArray();
                    Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStringValue(value));
                    Assert.Equal(0, output.WrittenCount);
                }
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // NOTE: WriteLargeKeyValue test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WriteLargeKeyValue(YamlWriterOptions options)
        {
            try
            {
                
                Span<byte> key;
                Span<byte> value;

                key = new byte[MaxUnescapedTokenSize + 1];
                value = new byte[MaxUnescapedTokenSize + 1];

                WriteTooLargeHelper(options, key, value);
                WriteTooLargeHelper(options, key.Slice(0, MaxUnescapedTokenSize), value);
                WriteTooLargeHelper(options, key, value.Slice(0, MaxUnescapedTokenSize));
                WriteTooLargeHelper(options, key.Slice(0, 10_000_000 / 3), value.Slice(0, 10_000_000 / 3), noThrow: true);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // NOTE: WriteLargeKeyEscapedValue test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WriteLargeKeyEscapedValue(YamlWriterOptions options)
        {
            try
            {
                
                Span<byte> key;
                Span<byte> value;

                // Since the byte values are 0 they will be escaped and size > MaxUnescapedTokenSize but < MaxEscapedTokenSize.
                key = new byte[MaxUnescapedTokenSize / 2];
                value = new byte[MaxUnescapedTokenSize / 2];

                WriteTooLargeHelper(options, key, value, noThrow: true);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.DateTimeFractionTrimBaseTests), MemberType = typeof(YamlDateTimeTestData))]
        [MemberData(nameof(YamlDateTimeTestData.DateTimeFractionTrimUtcOffsetTests), MemberType = typeof(YamlDateTimeTestData))]
        [Trait("category", "jsonoutput")]
        public void WriteDateTime_TrimsFractionCorrectly(string testStr, string expectedStr)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            using var yamlUtf8 = new Utf8YamlWriter(output);

            yamlUtf8.WriteStringValue(DateTime.ParseExact(testStr, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            yamlUtf8.Flush();

            YamlTestHelper.AssertContents($"\"{expectedStr}\"", output);
        }

        [Theory]
        [MemberData(nameof(YamlDateTimeTestData.DateTimeOffsetFractionTrimTests), MemberType = typeof(YamlDateTimeTestData))]
        [Trait("category", "jsonoutput")]
        public void WriteDateTimeOffset_TrimsFractionCorrectly(string testStr, string expectedStr)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            using var yamlUtf8 = new Utf8YamlWriter(output);

            yamlUtf8.WriteStringValue(DateTimeOffset.ParseExact(testStr, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            yamlUtf8.Flush();

            YamlTestHelper.AssertContents($"\"{expectedStr}\"", output);
        }

        [Fact]
        public void WriteDateTime_TrimsFractionCorrectly_SerializerRoundtrip()
        {
            DateTime utcNow = DateTimeTestHelpers.FixedDateTimeValue;
            Assert.Equal(utcNow, YamlSerializer.Deserialize(YamlSerializer.SerializeToUtf8Bytes(utcNow), typeof(DateTime)));
        }

        private static void WriteTooLargeHelper(YamlWriterOptions options, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, bool noThrow = false)
        {
            // Resizing is too slow, even for outerloop tests, so initialize to a large output size up front.
            var output = new ArrayBufferWriter<byte>(noThrow ? 40_000_000 : 1024);
            using var yamlUtf8 = new Utf8YamlWriter(output, options);

            yamlUtf8.WriteStartObject();

            try
            {
                yamlUtf8.WriteString(key, value);

                if (!noThrow)
                {
                    Assert.Fail($"Expected ArgumentException for data too large wasn't thrown. KeyLength: {key.Length} | ValueLength: {value.Length}");
                }
            }
            catch (ArgumentException)
            {
                if (noThrow)
                {
                    Assert.Fail($"Expected writing large key/value to succeed. KeyLength: {key.Length} | ValueLength: {value.Length}");
                }
            }

            yamlUtf8.WriteEndObject();
            yamlUtf8.Flush();
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_YamlEncodedText_Default()
        {
            YamlEncodedText text = default;
            WriteStringValueHelper(text, "\"\"");
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStrings))]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_YamlEncodedText(string message, string expectedMessage)
        {
            YamlEncodedText text = YamlEncodedText.Encode(message);
            WriteStringValueHelper(text, expectedMessage);
        }

        private static void WriteStringValueHelper(YamlEncodedText text, string expectedMessage)
        {
            var output = new ArrayBufferWriter<byte>();
            using var yamlUtf8 = new Utf8YamlWriter(output);
            yamlUtf8.WriteStringValue(text);
            yamlUtf8.Flush();

            YamlTestHelper.AssertContents($"{expectedMessage}", output);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1_000)]
        [InlineData(10_000)]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_YamlEncodedText_Large(int stringLength)
        {
            {
                var message = new string('a', stringLength);
                var builder = new StringBuilder();
                builder.Append("\"");
                for (int i = 0; i < stringLength; i++)
                {
                    builder.Append("a");
                }
                builder.Append("\"");
                string expectedMessage = builder.ToString();

                YamlEncodedText text = YamlEncodedText.Encode(message);

                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStringValue(text);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedMessage, output);
            }
            {
                var message = new string('>', stringLength);
                var builder = new StringBuilder();
                builder.Append("\"");
                for (int i = 0; i < stringLength; i++)
                {
                    builder.Append("\\u003e");
                }
                builder.Append("\"");
                string expectedMessage = builder.ToString();

                YamlEncodedText text = YamlEncodedText.Encode(message);
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStringValue(text);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents(expectedMessage, output);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStartArrayObject_YamlEncodedText_Default()
        {
            YamlEncodedText text = default;
            WriteArrayObjectHelper(text, "\"\"");
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStrings))]
        [Trait("category", "jsonoutput")]
        public static void WriteStartArrayObject_YamlEncodedText(string message, string expectedMessage)
        {
            YamlEncodedText text = YamlEncodedText.Encode(message);
            WriteArrayObjectHelper(text, expectedMessage);
        }

        private static void WriteArrayObjectHelper(YamlEncodedText text, string expectedMessage)
        {
            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartObject(text);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:{{", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteStartArray(text);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:[", output);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteLiteral_YamlEncodedText_Default()
        {
            YamlEncodedText text = default;
            WriteLiteralHelper(text, "\"\"");
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStrings))]
        [Trait("category", "jsonoutput")]
        public static void WriteLiteral_YamlEncodedText(string message, string expectedMessage)
        {
            YamlEncodedText text = YamlEncodedText.Encode(message);
            WriteLiteralHelper(text, expectedMessage);
        }

        private static void WriteLiteralHelper(YamlEncodedText text, string expectedMessage)
        {
            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBoolean(text, true);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:true", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteBoolean(text, false);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:false", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                yamlUtf8.WriteNull(text);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:null", output);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_YamlEncodedText_Default()
        {
            YamlEncodedText text = default;
            WriteNumberHelper(text, "\"\"");
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStrings))]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_YamlEncodedText(string message, string expectedMessage)
        {
            YamlEncodedText text = YamlEncodedText.Encode(message);
            WriteNumberHelper(text, expectedMessage);
        }

        private static void WriteNumberHelper(YamlEncodedText text, string expectedMessage)
        {
            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                int value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                long value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                uint value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                ulong value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                float value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                double value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                decimal value = 1;
                yamlUtf8.WriteNumber(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:1", output);
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringDateAndGuid_YamlEncodedText_Default()
        {
            YamlEncodedText text = default;
            WriteStringHelper(text, "\"\"");
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStrings))]
        [Trait("category", "jsonoutput")]
        public static void WriteStringDateAndGuid_YamlEncodedText(string message, string expectedMessage)
        {
            YamlEncodedText text = YamlEncodedText.Encode(message);
            WriteStringHelper(text, expectedMessage);
        }

        private static void WriteStringHelper(YamlEncodedText text, string expectedMessage)
        {
            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                DateTime value = new DateTime(2019, 1, 1);
                yamlUtf8.WriteString(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:\"{value.ToString("yyyy-MM-ddTHH:mm:ss")}\"", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                DateTimeOffset value = new DateTime(2019, 1, 1);
                yamlUtf8.WriteString(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:\"{value.ToString("yyyy-MM-ddTHH:mm:ssK")}\"", output);
            }

            {
                var output = new ArrayBufferWriter<byte>();
                using var yamlUtf8 = new Utf8YamlWriter(output);
                yamlUtf8.WriteStartObject();
                Guid value = Guid.NewGuid();
                yamlUtf8.WriteString(text, value);
                yamlUtf8.Flush();

                YamlTestHelper.AssertContents($"{{{expectedMessage}:\"{value.ToString()}\"", output);
            }
        }

        [ConditionalTheory(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        [MemberData(nameof(YamlOptions_TestData))]
        public void WriteTooLargeArguments(YamlWriterOptions options)
        {
            try
            {
                byte[] bytesTooLarge;
                char[] charsTooLarge;
                var bytes = new byte[5];
                var chars = new char[5];

                bytesTooLarge = new byte[400_000_000];
                charsTooLarge = new char[400_000_000];

                bytesTooLarge.AsSpan().Fill((byte)'a');
                charsTooLarge.AsSpan().Fill('a');
                bytes.AsSpan().Fill((byte)'a');
                chars.AsSpan().Fill('a');

                var pipe = new Pipe();
                PipeWriter output = pipe.Writer;
                using var yamlUtf8 = new Utf8YamlWriter(output, options);

                yamlUtf8.WriteStartArray();

                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStartObject(bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytesTooLarge, bytes));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytes, bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytesTooLarge, chars));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(chars, bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytesTooLarge, new DateTime(2015, 11, 9)));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytesTooLarge, new DateTimeOffset(new DateTime(2015, 11, 9))));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytesTooLarge, Guid.NewGuid()));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStringValue(bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(bytesTooLarge, 10m));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(bytesTooLarge, 10.1));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(bytesTooLarge, 10.1f));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(bytesTooLarge, 12345678901));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(bytesTooLarge, (ulong)12345678901));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteBoolean(bytesTooLarge, true));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNull(bytesTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WritePropertyName(bytesTooLarge));

                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStartObject(charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(charsTooLarge, chars));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(chars, charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(charsTooLarge, bytes));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(bytes, charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(charsTooLarge, new DateTime(2015, 11, 9)));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(charsTooLarge, new DateTimeOffset(new DateTime(2015, 11, 9))));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteString(charsTooLarge, Guid.NewGuid()));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteStringValue(charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteCommentValue(charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(charsTooLarge, 10m));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(charsTooLarge, 10.1));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(charsTooLarge, 10.1f));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(charsTooLarge, 12345678901));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNumber(charsTooLarge, (ulong)12345678901));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteBoolean(charsTooLarge, true));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WriteNull(charsTooLarge));
                Assert.Throws<ArgumentException>(() => yamlUtf8.WritePropertyName(charsTooLarge));

                yamlUtf8.Flush();
                Assert.Equal(1, yamlUtf8.BytesCommitted);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteBase64String_NullPropertyName()
        {
            WriteNullPropertyName_Simple(
                new byte[] { 0x01, 0x00, 0x01 },
                "\"AQAB\"",
                (writer, name, value) => writer.WriteBase64String(name, value),
                (writer, name, value) => writer.WriteBase64String(name, value),
                (writer, name, value) => writer.WriteBase64String(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteBoolean_NullPropertyName()
        {
            WriteNullPropertyName_Simple(
                false,
                "false",
                (writer, name, value) => writer.WriteBoolean(name, value),
                (writer, name, value) => writer.WriteBoolean(name, value),
                (writer, name, value) => writer.WriteBoolean(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNull_NullPropertyName()
        {
            WriteNullPropertyName_NoValue(
                "null",
                cleanupAction: null,
                (writer, name) => writer.WriteNull(name),
                (writer, name) => writer.WriteNull(name),
                (writer, name) => writer.WriteNull(name));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_Decimal()
        {
            decimal numericValue = 1.04m;

            WriteNullPropertyName_Simple(
                numericValue,
                "1.04",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_Double()
        {
            double numericValue = 1.05d;

            WriteNullPropertyName_Simple(
                numericValue,
                "1.05",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_Int32()
        {
            int numericValue = 1048576;

            WriteNullPropertyName_Simple(
                numericValue,
                "1048576",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_Int64()
        {
            long numericValue = 0x0100_0000_0000;

            WriteNullPropertyName_Simple(
                numericValue,
                "1099511627776",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_Single()
        {
            float numericValue = 1e3f;

            WriteNullPropertyName_Simple(
                numericValue,
                "1000",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_UInt32()
        {
            uint numericValue = 0x8000_0000;

            WriteNullPropertyName_Simple(
                numericValue,
                "2147483648",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteNumber_NullPropertyName_UInt64()
        {
            ulong numericValue = ulong.MaxValue;

            WriteNullPropertyName_Simple(
                numericValue,
                "18446744073709551615",
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value),
                (writer, name, value) => writer.WriteNumber(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WritePropertyName_NullPropertyName()
        {
            WriteNullPropertyName_NoValue(
                "null",
                writer => writer.WriteNullValue(),
                (writer, name) => writer.WritePropertyName(name),
                (writer, name) => writer.WritePropertyName(name),
                (writer, name) => writer.WritePropertyName(name));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStartArray_NullPropertyName()
        {
            WriteNullPropertyName_NoValue(
                "[]",
                writer => writer.WriteEndArray(),
                (writer, name) => writer.WriteStartArray(name),
                (writer, name) => writer.WriteStartArray(name),
                (writer, name) => writer.WriteStartArray(name));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStartObject_NullPropertyName()
        {
            WriteNullPropertyName_NoValue(
                "{}",
                writer => writer.WriteEndObject(),
                (writer, name) => writer.WriteStartObject(name),
                (writer, name) => writer.WriteStartObject(name),
                (writer, name) => writer.WriteStartObject(name));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_DateTime()
        {
            WriteNullPropertyName_Simple(
                DateTime.MinValue,
                "\"0001-01-01T00:00:00\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_DateTimeOffset()
        {
            WriteNullPropertyName_Simple(
                DateTimeOffset.MinValue,
                "\"0001-01-01T00:00:00+00:00\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_Guid()
        {
            WriteNullPropertyName_Simple(
                Guid.Empty,
                "\"00000000-0000-0000-0000-000000000000\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_ReadOnlySpan_Byte()
        {
            WriteNullPropertyName_Simple(
                "utf8"u8.ToArray(),
                "\"utf8\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_ReadOnlySpan_Char()
        {
            WriteNullPropertyName_Simple(
                "utf16",
                "\"utf16\"",
                (writer, name, value) => writer.WriteString(name, value.AsSpan()),
                (writer, name, value) => writer.WriteString(name, value.AsSpan()),
                (writer, name, value) => writer.WriteString(name, value.AsSpan()));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_String()
        {
            WriteNullPropertyName_Simple(
                "string",
                "\"string\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteString_NullPropertyName_YamlEncodedText()
        {
            WriteNullPropertyName_Simple(
                YamlEncodedText.Encode("jet"),
                "\"jet\"",
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value),
                (writer, name, value) => writer.WriteString(name, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteCommentValue_NullString()
        {
            // WriteCommentValue is sufficiently different (no comma after a legal value)
            // that it doesn't warrant a helper for expansion.
            var output = new ArrayBufferWriter<byte>(1024);
            string nullString = null;

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartArray();

                AssertExtensions.Throws<ArgumentNullException>(
                    "value",
                    () => writer.WriteCommentValue(nullString));

                ReadOnlySpan<char> nullStringSpan = nullString.AsSpan();
                writer.WriteCommentValue(nullStringSpan);

                writer.WriteCommentValue(ReadOnlySpan<byte>.Empty);

                writer.WriteEndArray();
                writer.Flush();
            }

            YamlTestHelper.AssertContents("[/**//**/]", output);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_NullString()
        {
            WriteNullValue_InArray(
                "\"\"",
                "null",
                (writer, value) => writer.WriteStringValue(value),
                (writer, value) => writer.WriteStringValue(value),
                (writer, value) => writer.WriteStringValue(value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_StringProperty_NullString()
        {
            WriteNullValue_InObject(
                "\"propStr\":\"\"",
                "\"propStr\":null",
                (writer, value) => writer.WriteString("propStr", value),
                (writer, value) => writer.WriteString("propStr", value),
                (writer, value) => writer.WriteString("propStr", value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_ReadOnlySpanCharProperty_NullString()
        {
            WriteNullValue_InObject(
                "\"propUtf16\":\"\"",
                "\"propUtf16\":null",
                (writer, value) => writer.WriteString("propUtf16".AsSpan(), value),
                (writer, value) => writer.WriteString("propUtf16".AsSpan(), value),
                (writer, value) => writer.WriteString("propUtf16".AsSpan(), value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_ReadOnlySpanBytesProperty_NullString()
        {
            byte[] propertyName = "propUtf8"u8.ToArray();

            WriteNullValue_InObject(
                "\"propUtf8\":\"\"",
                "\"propUtf8\":null",
                (writer, value) => writer.WriteString(propertyName, value),
                (writer, value) => writer.WriteString(propertyName, value),
                (writer, value) => writer.WriteString(propertyName, value));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStringValue_YamlEncodedTextProperty_NullString()
        {
            YamlEncodedText jet = YamlEncodedText.Encode("propJet");

            WriteNullValue_InObject(
                "\"propJet\":\"\"",
                "\"propJet\":null",
                (writer, value) => writer.WriteString(jet, value),
                (writer, value) => writer.WriteString(jet, value),
                (writer, value) => writer.WriteString(jet, value));
        }

        [Fact]
        public static void WriteStringValue_IndentationOptions()
        {
            var options = new YamlWriterOptions();
            var expectedOutput = GetCustomExpectedString(options);

            options.IndentCharacter = '\t';
            options.IndentSize = 127;

            var output = GetCustomExpectedString(options);

            Assert.Equal(expectedOutput, output);
        }

        private delegate void WriteValueSpanAction<T>(
            Utf8YamlWriter writer,
            ReadOnlySpan<T> value);

        private delegate void WritePropertySpanAction<T>(
            Utf8YamlWriter writer,
            ReadOnlySpan<T> propertyName);

        private delegate void WritePropertySpanAction<T1, T2>(
            Utf8YamlWriter writer,
            ReadOnlySpan<T1> propertyName,
            T2 value);

        private static void WriteNullPropertyName_Simple<T>(
            T value,
            string wireValue,
            Action<Utf8YamlWriter, string, T> stringAction,
            WritePropertySpanAction<char, T> charSpanAction,
            WritePropertySpanAction<byte, T> byteSpanAction)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            string nullString = null;

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();

                AssertExtensions.Throws<ArgumentNullException>(
                    "propertyName",
                    () => stringAction(writer, nullString, value));

                writer.WriteEndObject();
                writer.Flush();
            }

            YamlTestHelper.AssertContents("{}", output);
            output.Clear();

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();

                ReadOnlySpan<char> nullStringSpan = nullString.AsSpan();
                charSpanAction(writer, nullStringSpan, value);

                byteSpanAction(writer, ReadOnlySpan<byte>.Empty, value);

                writer.WriteEndObject();
                writer.Flush();
            }

            YamlTestHelper.AssertContents($"{{\"\":{wireValue},\"\":{wireValue}}}", output);
        }

        private static void WriteNullPropertyName_NoValue(
            string wireValue,
            Action<Utf8YamlWriter> cleanupAction,
            Action<Utf8YamlWriter, string> stringAction,
            WritePropertySpanAction<char> charSpanAction,
            WritePropertySpanAction<byte> byteSpanAction)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            string nullString = null;

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();

                AssertExtensions.Throws<ArgumentNullException>(
                    "propertyName",
                    () => stringAction(writer, nullString));

                writer.WriteEndObject();
                writer.Flush();
            }

            YamlTestHelper.AssertContents("{}", output);
            output.Clear();

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();

                ReadOnlySpan<char> nullStringSpan = nullString.AsSpan();
                charSpanAction(writer, nullStringSpan);
                cleanupAction?.Invoke(writer);

                byteSpanAction(writer, ReadOnlySpan<byte>.Empty);
                cleanupAction?.Invoke(writer);

                writer.WriteEndObject();
                writer.Flush();
            }

            YamlTestHelper.AssertContents($"{{\"\":{wireValue},\"\":{wireValue}}}", output);
        }

        private static void WriteNullValue_InObject(
            string wireValue,
            string nullValue,
            Action<Utf8YamlWriter, string> stringAction,
            WriteValueSpanAction<char> charSpanAction,
            WriteValueSpanAction<byte> byteSpanAction)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            string nullString = null;

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartObject();

                stringAction(writer, nullString);

                ReadOnlySpan<char> nullStringSpan = nullString.AsSpan();
                charSpanAction(writer, nullStringSpan);

                byteSpanAction(writer, ReadOnlySpan<byte>.Empty);

                writer.WriteEndObject();
                writer.Flush();
            }

            YamlTestHelper.AssertContents($"{{{nullValue},{wireValue},{wireValue}}}", output);
        }

        private static void WriteNullValue_InArray(
            string wireValue,
            string nullValue,
            Action<Utf8YamlWriter, string> stringAction,
            WriteValueSpanAction<char> charSpanAction,
            WriteValueSpanAction<byte> byteSpanAction)
        {
            var output = new ArrayBufferWriter<byte>(1024);
            string nullString = null;

            using (var writer = new Utf8YamlWriter(output))
            {
                writer.WriteStartArray();

                stringAction(writer, nullString);

                ReadOnlySpan<char> nullStringSpan = nullString.AsSpan();
                charSpanAction(writer, nullStringSpan);

                byteSpanAction(writer, ReadOnlySpan<byte>.Empty);

                writer.WriteEndArray();
                writer.Flush();
            }

            YamlTestHelper.AssertContents($"[{nullValue},{wireValue},{wireValue}]", output);
        }

        private static string GetHelloWorldExpectedString(YamlWriterOptions options, string propertyName, string value)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(propertyName);
            yaml.WriteValue(value);
            yaml.WritePropertyName(propertyName);
            yaml.WriteValue(value);
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetBase64ExpectedString(YamlWriterOptions options, string propertyName, byte[] value)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(propertyName);
            yaml.WriteValue(value);
            yaml.WritePropertyName(propertyName);
            yaml.WriteValue(value);
            yaml.WritePropertyName("array");
            yaml.WriteStartArray();
            yaml.WriteValue(new byte[] { 1, 2 });
            yaml.WriteValue(new byte[] { 3, 4 });
            yaml.WriteEndArray();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetCommentInArrayExpectedString(YamlWriterOptions options, string comment)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteComment(comment);

            CompensateNewLine(options.Indented, yaml, streamWriter);
            yaml.WriteStartArray();
            for (int j = 0; j < 10; j++)
                yaml.WriteComment(comment);
            yaml.WriteValue(comment);
            yaml.WriteComment(comment);
            yaml.WriteStartArray();
            yaml.WriteEndArray();
            yaml.WriteComment(comment);
            yaml.WriteStartObject();
            yaml.WriteEndObject();
            yaml.WriteComment(comment);
            yaml.WriteEnd();

            CompensateNewLine(options.Indented, yaml, streamWriter);
            yaml.WriteComment(comment);
            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetCommentInObjectExpectedString(YamlWriterOptions options, string comment)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteComment(comment);

            CompensateNewLine(options.Indented, yaml, streamWriter);
            yaml.WriteStartObject();

            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter, 2);
            yaml.WriteComment(comment);

            yaml.WritePropertyName("property1");

            CompensateWhitespaces(options.Indented, yaml, streamWriter); 
            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter);

            yaml.WriteComment(comment);

            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter);

            yaml.WriteStartArray();
            yaml.WriteEndArray();

            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter, 2);

            yaml.WriteComment(comment);

            yaml.WritePropertyName("property2");

            CompensateWhitespaces(options.Indented, yaml, streamWriter);
            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter);

            yaml.WriteComment(comment);

            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter);

            yaml.WriteStartObject();
            yaml.WriteEndObject();

            CompensateNewLine(options.Indented, yaml, streamWriter);
            CompensateWhitespaces(options.Indented, yaml, streamWriter, 2);

            yaml.WriteComment(comment);
            yaml.WriteEnd();

            CompensateNewLine(options.Indented, yaml, streamWriter);
            yaml.WriteComment(comment);
            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStringsExpectedString(YamlWriterOptions options, string value)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartArray();
            for (int j = 0; j < 10; j++)
                yaml.WriteValue(value);
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetEscapedExpectedString(YamlWriterOptions options, string propertyName, string value, StringEscapeHandling escaping, bool escape = true)
        {
            using (TextWriter stringWriter = new StringWriter())
            using (var yaml = new JsonTextWriter(stringWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = escaping
            })
            {
                yaml.WriteStartObject();
                yaml.WritePropertyName(propertyName, escape);
                yaml.WriteValue(value);
                yaml.WriteEnd();

                yaml.Flush();
                return HandleFormatting(stringWriter.ToString(), options);
            }
        }

        private static string GetCustomExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();
            for (int i = 0; i < 1_000; i++)
            {
                yaml.WritePropertyName("message");
                yaml.WriteValue("Hello, World!");
            }
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStartEndExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartArray();
            yaml.WriteStartObject();
            yaml.WriteEnd();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStartEndWithPropertyArrayExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName("property name");
            yaml.WriteStartArray();
            yaml.WriteEnd();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStartEndWithPropertyArrayExpectedString(string key, YamlWriterOptions options, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(key, escape);
            yaml.WriteStartArray();
            yaml.WriteEnd();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStartEndWithPropertyObjectExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName("property name");
            yaml.WriteStartObject();
            yaml.WriteEnd();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetStartEndWithPropertyObjectExpectedString(string key, YamlWriterOptions options, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(key, escape);
            yaml.WriteStartObject();
            yaml.WriteEnd();
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetArrayWithPropertyExpectedString(YamlWriterOptions options)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName("message");
            yaml.WriteStartArray();
            yaml.WriteEndArray();
            yaml.WriteEndObject();
            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetBooleanExpectedString(YamlWriterOptions options, string keyString, bool value, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(keyString, escape);
            yaml.WriteValue(value);

            yaml.WritePropertyName("temp");
            yaml.WriteStartArray();
            yaml.WriteValue(true);
            yaml.WriteValue(true);
            yaml.WriteValue(false);
            yaml.WriteValue(false);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetNullExpectedString(YamlWriterOptions options, string keyString, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName(keyString, escape);
            yaml.WriteNull();
            yaml.WritePropertyName(keyString, escape);
            yaml.WriteNull();

            yaml.WritePropertyName("temp");
            yaml.WriteStartArray();
            yaml.WriteValue((string)null);
            yaml.WriteValue((string)null);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetPropertyExpectedString<T>(YamlWriterOptions options, T value)
        {
            var sb = new StringBuilder();
            StringWriter stringWriter = new StringWriter(sb);

            var yaml = new JsonTextWriter(stringWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();
            yaml.WritePropertyName("message");
            yaml.WriteValue(value);
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(sb.ToString(), options);
        }

        private static string GetNumbersExpectedString(YamlWriterOptions options, string keyString, int[] ints, uint[] uints, long[] longs, ulong[] ulongs, float[] floats, double[] doubles, decimal[] decimals, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None
            };

            yaml.WriteStartObject();

            for (int i = 0; i < floats.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(floats[i]);
            }
            for (int i = 0; i < ints.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(ints[i]);
            }
            for (int i = 0; i < uints.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(uints[i]);
            }
            for (int i = 0; i < doubles.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(doubles[i]);
            }
            for (int i = 0; i < longs.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(longs[i]);
            }
            for (int i = 0; i < ulongs.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(ulongs[i]);
            }
            for (int i = 0; i < decimals.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(decimals[i]);
            }

            yaml.WritePropertyName(keyString, escape);
            yaml.WriteStartArray();
            yaml.WriteValue(floats[0]);
            yaml.WriteValue(ints[0]);
            yaml.WriteValue(uints[0]);
            yaml.WriteValue(doubles[0]);
            yaml.WriteValue(longs[0]);
            yaml.WriteValue(ulongs[0]);
            yaml.WriteValue(decimals[0]);
            yaml.WriteEndArray();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetExpectedString_RelaxedEscaping(bool prettyPrint, string keyString)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
            };

            yaml.WriteStartObject();

            yaml.WritePropertyName(keyString, escape: true);
            yaml.WriteValue(keyString);

            yaml.WritePropertyName(keyString, escape: true);
            yaml.WriteStartArray();
            yaml.WriteValue(keyString);
            yaml.WriteValue(keyString);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static string GetGuidsExpectedString(YamlWriterOptions options, string keyString, Guid[] guids, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            yaml.WriteStartObject();

            for (int i = 0; i < guids.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(guids[i]);
            }

            yaml.WritePropertyName(keyString, escape);
            yaml.WriteStartArray();
            yaml.WriteValue(guids[0]);
            yaml.WriteValue(guids[1]);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetNumbersExpectedString<T>(YamlWriterOptions options, int numberOfElements, T value)
        {
            var sb = new StringBuilder();
            StringWriter stringWriter = new StringWriter(sb);

            var yaml = new JsonTextWriter(stringWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
            };

            yaml.WriteStartArray();
            for (int i = 0; i < numberOfElements; i++)
            {
                yaml.WriteValue(value);
            }
            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(sb.ToString(), options);
        }

        private static string GetDatesExpectedString(YamlWriterOptions options, string keyString, DateTime[] dates, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteStartObject();

            for (int i = 0; i < dates.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(dates[i]);
            }

            yaml.WritePropertyName(keyString, escape);
            yaml.WriteStartArray();
            yaml.WriteValue(dates[0]);
            yaml.WriteValue(dates[1]);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static string GetDatesExpectedString(YamlWriterOptions options, string keyString, DateTimeOffset[] dates, bool escape = false)
        {
            var ms = new MemoryStream();
            TextWriter streamWriter = new StreamWriter(ms, new UTF8Encoding(false), 1024, true);

            var yaml = new JsonTextWriter(streamWriter)
            {
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            };

            yaml.WriteStartObject();

            for (int i = 0; i < dates.Length; i++)
            {
                yaml.WritePropertyName(keyString, escape);
                yaml.WriteValue(dates[i]);
            }

            yaml.WritePropertyName(keyString, escape);
            yaml.WriteStartArray();
            yaml.WriteValue(dates[0]);
            yaml.WriteValue(dates[1]);
            yaml.WriteEnd();

            yaml.WriteEnd();

            yaml.Flush();

            return HandleFormatting(Encoding.UTF8.GetString(ms.ToArray()), options);
        }

        private static void CompensateWhitespaces(bool prettyPrint, JsonTextWriter yaml, TextWriter streamWriter, int whitespaceCount = 1)
        {
            if (prettyPrint)
            {
                yaml.Flush();
                streamWriter.Write(new string(' ', whitespaceCount));
            }
        }

        private static void CompensateNewLine(bool prettyPrint, JsonTextWriter yaml, TextWriter streamWriter)
        {
            if (prettyPrint)
            {
                yaml.Flush();
                streamWriter.WriteLine();
            }
        }

        private static string HandleFormatting(string text, YamlWriterOptions options)
        {
            var normalized = text.Replace("  ", GetIndentText(options));

            if (options.NewLine != Environment.NewLine)
            {
                normalized = normalized.Replace(Environment.NewLine, options.NewLine);
            }

            return normalized;
        }

        private static string GetIndentText(YamlWriterOptions options) => new(options.IndentCharacter, options.IndentSize);

        public static IEnumerable<object[]> YamlEncodedTextStrings
        {
            get
            {
                return new List<object[]>
                {
                    new object[] {"", "\"\"" },
                    new object[] { "message", "\"message\"" },
                    new object[] { "mess\"age", "\"mess\\u0022age\"" },
                    new object[] { "mess\\u0022age", "\"mess\\\\u0022age\"" },
                    new object[] { ">>>>>", "\"\\u003E\\u003E\\u003E\\u003E\\u003E\"" },
                    new object[] { "\\u003E\\u003E\\u003E\\u003E\\u003E", "\"\\\\u003E\\\\u003E\\\\u003E\\\\u003E\\\\u003E\"" },
                };
            }
        }

        private static IEnumerable<YamlWriterOptions> YamlOptions()
        {
            return from indented in new[] { true, false }
                   from skipValidation in new[] { true, false }
                   from indentCharacter in indented ? new char?[] { null, ' ', '\t' } : []
                   from indentSize in indented ? new int?[] { null, 0, 1, 2, 3 } : []
                   from newLine in indented ? new string?[] { null, "\n", "\r\n" } : []
                   select CreateOptions(indented, indentCharacter, indentSize, skipValidation, newLine);

            static YamlWriterOptions CreateOptions(bool indented, char? indentCharacter, int? indentSize, bool skipValidation, string? newLine)
            {
                var options = new YamlWriterOptions { Indented = indented, SkipValidation = skipValidation };
                if (indentCharacter is not null) options.IndentCharacter = (char)indentCharacter;
                if (indentSize is not null) options.IndentSize = (int)indentSize;
                if (newLine is not null) options.NewLine = newLine;
                return options;
            }
        }

        private static IEnumerable<object[]> YamlOptionsWith<T>(IEnumerable<T> others) =>
            from options in YamlOptions()
            from inputValue in others
            select new object[] { options, inputValue };

        private static IEnumerable<object[]> YamlOptionsWith<T, U>(IEnumerable<T> others, IEnumerable<U> anothers) =>
            from options in YamlOptions()
            from inputValue in others
            from anotherValue in anothers
            select new object[] { options, inputValue, anotherValue };

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        [OuterLoop]
        public static void WritePropertyWithExtremelyLongName_ThrowsArgumentException()
        {
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output);
            
            writer.WriteStartObject();
            
            string longName = new string('a', 170_000_000);
            
            Assert.Throws<ArgumentException>(() => writer.WritePropertyName(longName.AsSpan()));
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        [OuterLoop]
        public static void WriteValueWithExtremelyLongValue_ThrowsArgumentException()
        {
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output);
            
            writer.WriteStartArray();
            
            string longValue = new string('a', 170_000_000);
            
            Assert.Throws<ArgumentException>(() => writer.WriteStringValue(longValue.AsSpan()));
        }

        // NOTE: WriteExtremelyLargeEscapedStringValue_Minimized test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        [OuterLoop]
        public static void WriteExtremelyLargeEscapedStringValue_Minimized()
        {
            const char InputCharacter = '\u007F';
            const int EscapedCharacterByteLength = 6;

            try
            {
                char[] value = new char[MaxUnescapedTokenSize];
                value.AsSpan().Fill(InputCharacter);

                int expectedByteLength = 2 + MaxUnescapedTokenSize * EscapedCharacterByteLength;
                var output = new ArrayBufferWriter<byte>(expectedByteLength);
                using var writer = new Utf8YamlWriter(output);
                writer.WriteStringValue(value.AsSpan());
                writer.Flush();

                ReadOnlySpan<byte> written = output.WrittenSpan;
                Assert.Equal(expectedByteLength, written.Length);
                Assert.Equal((byte)'"', written[0]);
                Assert.Equal((byte)'"', written[^1]);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        // NOTE: WriteExtremelyLargeEscapedStringValue_Indented test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        [OuterLoop]
        public static void WriteExtremelyLargeEscapedStringValue_Indented()
        {
            const char InputCharacter = '\u007F';
            const int EscapedCharacterByteLength = 6;
            const int IndentSize = 127;
            const string NewLine = "\n";

            try
            {
                char[] value = new char[MaxUnescapedTokenSize];
                value.AsSpan().Fill(InputCharacter);

                int escapedStrByteLength = 2 + MaxUnescapedTokenSize * EscapedCharacterByteLength;
                int expectedByteLength = 1 + NewLine.Length + IndentSize + escapedStrByteLength + NewLine.Length + 1;

                var options = new YamlWriterOptions { Indented = true, IndentSize = IndentSize, NewLine = NewLine };
                var output = new ArrayBufferWriter<byte>(expectedByteLength);
                using var writer = new Utf8YamlWriter(output, options);
                writer.WriteStartArray();
                writer.WriteStringValue(value.AsSpan());
                writer.WriteEndArray();
                writer.Flush();

                // Layout: [ \n <IndentSize spaces> "escapedStr" \n ]
                ReadOnlySpan<byte> written = output.WrittenSpan;
                Assert.Equal(expectedByteLength, written.Length);
                Assert.Equal((byte)'[', written[0]);
                Assert.Equal((byte)']', written[^1]);
                int stringStart = 1 + NewLine.Length + IndentSize;
                Assert.Equal((byte)'"', written[stringStart]);
                Assert.Equal((byte)'"', written[stringStart + escapedStrByteLength - 1]);
            }
            catch (OutOfMemoryException)
            {
                throw new SkipTestException("Out of memory allocating large objects");
            }
        }

        [Fact]
        public static void WriteRawValueWithInvalidYaml_ValidationDisabled()
        {
            var options = new YamlWriterOptions { SkipValidation = true };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartArray();
            writer.WriteRawValue("invalid json content"u8, skipInputValidation: true);
            writer.WriteEndArray();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("invalid json content", yaml);
        }

        [Fact]
        public static void YamlElement_ToString_EdgeCase()
        {
            using YamlDocument doc = YamlDocument.Parse("null");
            YamlElement element = doc.RootElement;
            string result = element.ToString();
            Assert.Equal("", result);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteWithMinimizedFormatting()
        {
            var options = new YamlWriterOptions { Indented = false };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteStringValue("test");
            writer.WriteEndObject();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Equal("""{"name":"test"}""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteRawValue_WithValidation()
        {
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output);
            
            writer.WriteStartArray();
            writer.WriteRawValue("42"u8, skipInputValidation: false);
            writer.WriteEndArray();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Equal("[42]", yaml);
        }

        [Fact]
        public static void WriteDateTime_MinimizedFormat()
        {
            var options = new YamlWriterOptions { Indented = false };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            DateTime dt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            writer.WriteStartObject();
            writer.WriteString("date", dt);
            writer.WriteEndObject();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("2024-01-01", yaml);
        }

        [Fact]
        public static void WriteGuid_MinimizedFormat()
        {
            var options = new YamlWriterOptions { Indented = false };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            Guid guid = Guid.Parse("12345678-1234-1234-1234-123456789012");
            writer.WriteStartObject();
            writer.WriteString("id", guid);
            writer.WriteEndObject();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("12345678-1234-1234-1234-123456789012", yaml);
        }

        [Fact]
        public static void WriteDecimal_MinimizedFormat()
        {
            var options = new YamlWriterOptions { Indented = false };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartObject();
            writer.WriteNumber("value", 123.456m);
            writer.WriteEndObject();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("123.456", yaml);
        }

        [Fact]
        public static void WriteLargeInteger_MinimizedFormat()
        {
            var options = new YamlWriterOptions { Indented = false };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartObject();
            writer.WriteNumber("value", long.MaxValue);
            writer.WriteEndObject();
            writer.Flush();
            
            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("9223372036854775807", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteBase64String_IndentedFormat()
        {
            var options = new YamlWriterOptions { Indented = true };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            byte[] bytes = new byte[] { 1, 2, 3, 4, 5 };
            writer.WriteStartObject();
            writer.WriteBase64String("data", bytes);
            writer.WriteEndObject();
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("\"data\": \"AQIDBAU=\"", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStartArray_WithPropertyName_Indented()
        {
            var options = new YamlWriterOptions { Indented = true };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartObject();
            writer.WriteStartArray("items");
            writer.WriteNumberValue(1);
            writer.WriteNumberValue(2);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("\"items\":", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteStartObject_WithPropertyName_Indented()
        {
            var options = new YamlWriterOptions { Indented = true };
            var output = new ArrayBufferWriter<byte>();
            using var writer = new Utf8YamlWriter(output, options);
            
            writer.WriteStartObject();
            writer.WriteStartObject("nested");
            writer.WriteString("key", "value");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            string yaml = Encoding.UTF8.GetString(output.WrittenSpan.ToArray());
            Assert.Contains("\"nested\":", yaml);
            Assert.Contains("\"key\": \"value\"", yaml);
        }
    }

    public static class WriterHelpers
    {
        // Normalize comparisons against Yaml.NET.
        // The following is performed unless skipSpecialRules is true:
        // * Uppercases the \u escaped hex characters.
        // * Escapes forward slash, greater than, and less than.
        // * Ignores ".0" for decimal values.
        public static string NormalizeToYamlNetFormat(this string yaml, bool skipSpecialRules)
        {
            var sb = new StringBuilder(yaml.Length);
            int i = 0;
            while (i < yaml.Length)
            {
                if (!skipSpecialRules)
                {
                    if (yaml[i] == '\\')
                    {
                        sb.Append(yaml[i++]);

                        if (i < yaml.Length - 1 && yaml[i] == 'u')
                        {
                            sb.Append(yaml[i++]);

                            if (i < yaml.Length - 4)
                            {
                                string temp = yaml.Substring(i, 4).ToLowerInvariant();
                                sb.Append(temp);
                                i += 4;
                            }
                        }
                        if (i < yaml.Length - 1 && yaml[i] == '/')
                        {
                            // Convert / to u002f
                            i++;
                            sb.Append("u002f");
                        }
                    }
                    // Convert > to \u003e
                    else if (yaml[i] == '>')
                    {
                        i++;
                        sb.Append("\\u003e");
                    }
                    // Convert < to \u003c
                    else if (yaml[i] == '<')
                    {
                        i++;
                        sb.Append("\\u003c");
                    }
                    // Remove .0
                    else if (yaml[i] == '.' && yaml[i + 1] == '0')
                    {
                        // Verify that token after .0 is a delimiter.
                        if (yaml[i + 2] == ',' || yaml[i + 2] == ']' || yaml[i + 2] == '}' ||
                            yaml[i + 2] == ' ' || yaml[i + 2] == '\r' || yaml[i + 2] == '\n')
                        {
                            i += 2;
                        }
                        else
                        {
                            sb.Append(yaml[i++]);
                        }
                    }
                    else
                    {
                        sb.Append(yaml[i++]);
                    }
                }
                else
                {
                    sb.Append(yaml[i++]);
                }
            }

            return sb.ToString();
        }

        public static async Task FlushAsync(this Utf8YamlWriter writer, bool useAsync)
        {
            if (useAsync)
            {
                await writer.FlushAsync();
            }
            else
            {
                writer.Flush();
            }
        }

        public static async Task DisposeAsync(this Utf8YamlWriter writer, bool useAsync)
        {
            if (useAsync)
            {
                await writer.DisposeAsync();
            }
            else
            {
                writer.Dispose();
            }
        }
    }
}
