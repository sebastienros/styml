// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace System.Text.Yaml.Tests
{
    public static class YamlElementParseTests
    {
        public static IEnumerable<object[]> ElementParseCases
        {
            get
            {
                yield return new object[] { "null", YamlValueKind.Null };
                yield return new object[] { "true", YamlValueKind.True };
                yield return new object[] { "false", YamlValueKind.False };
                yield return new object[] { "\"MyString\"", YamlValueKind.String };
                yield return new object[] { """
                    "\u0033\u002e\u0031"
                    """, YamlValueKind.String }; // "3.12"
                yield return new object[] { "1", YamlValueKind.Number };
                yield return new object[] { "3.125e7", YamlValueKind.Number };
                yield return new object[] { "{}", YamlValueKind.Object };
                yield return new object[] { "[]", YamlValueKind.Array };
            }
        }

        [Theory]
        [MemberData(nameof(ElementParseCases))]
        public static void ParseValue(string yaml, YamlValueKind kind)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));

            YamlElement element = YamlElement.ParseValue(ref reader);
            Assert.Equal(kind, element.ValueKind);
            Assert.Equal(yaml.Length, reader.BytesConsumed);
            Assert.False(element.SniffDocument().IsDisposable());
        }

        [Theory]
        [MemberData(nameof(ElementParseCases))]
        public static void Parse_Valid(string yaml, YamlValueKind kind)
        {
            Validate(YamlElement.Parse(yaml));
            Validate(YamlElement.Parse(yaml.AsSpan()));
            Validate(YamlElement.Parse(Encoding.UTF8.GetBytes(yaml).AsSpan()));

            void Validate(YamlElement element)
            {
                Assert.Equal(kind, element.ValueKind);
                Assert.False(element.SniffDocument().IsDisposable());
            }
        }

        [Theory]
        [MemberData(nameof(ElementParseCases))]
        public static void TryParseValue(string yaml, YamlValueKind kind)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));

            bool success = YamlElement.TryParseValue(ref reader, out YamlElement? element);
            Assert.True(success);
            Assert.Equal(kind, element!.Value.ValueKind);
            Assert.Equal(yaml.Length, reader.BytesConsumed);
            Assert.False(element!.Value.SniffDocument().IsDisposable());
        }


        [Fact]
        public static void ParseValue_AllowMultipleValues_TrailingContent()
        {
            var options = new YamlReaderOptions { AllowMultipleValues = true };
            var reader = new Utf8YamlReader("[null,false,42,{},[1]]             <NotYaml/>"u8, options);

            YamlElement element = YamlElement.ParseValue(ref reader);
            Assert.Equal("[null,false,42,{},[1]]", element.GetRawText());
            Assert.Equal(YamlTokenType.EndArray, reader.TokenType);

            YamlTestHelper.AssertThrows<YamlException>(ref reader, (ref Utf8YamlReader reader) => reader.Read());
        }

        public static IEnumerable<object[]> ElementParsePartialDataCases
        {
            get
            {
                yield return new object[] { "\"MyString"};
                yield return new object[] { "{" };
                yield return new object[] { "[" };
                yield return new object[] { " \n" };
            }
        }

        [Theory]
        [MemberData(nameof(ElementParsePartialDataCases))]
        public static void ParseValuePartialDataFail(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml));

            Exception ex;
            try
            {
                YamlElement.ParseValue(ref reader);
                ex = null;
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.NotNull(ex);
            Assert.IsAssignableFrom<YamlException>(ex);

            Assert.Equal(0, reader.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(ElementParsePartialDataCases))]
        public static void Parse_Invalid(string yaml)
        {
            Assert.ThrowsAny<YamlException>(() => YamlElement.Parse(yaml));
            Assert.ThrowsAny<YamlException>(() => YamlElement.Parse(yaml.AsSpan()));
            Assert.ThrowsAny<YamlException>(() => YamlElement.Parse(Encoding.UTF8.GetBytes(yaml).AsSpan()));
        }

        [Fact]
        public static void Parse_NullString_Throws()
        {
            AssertExtensions.Throws<ArgumentNullException>("yaml", () => YamlElement.Parse((string)null));
        }

        [Theory]
        [MemberData(nameof(ElementParsePartialDataCases))]
        public static void ParseValueOutOfData(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), isFinalBlock: false, new YamlReaderState());

            Exception ex;
            try
            {
                YamlElement.ParseValue(ref reader);
                ex = null;
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.NotNull(ex);
            Assert.IsAssignableFrom<YamlException>(ex);

            Assert.Equal(0, reader.BytesConsumed);
        }

        [Theory]
        [MemberData(nameof(ElementParsePartialDataCases))]
        public static void TryParseValueOutOfData(string yaml)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), isFinalBlock: false, new YamlReaderState());
            Assert.False(YamlElement.TryParseValue(ref reader, out YamlElement? element));
            Assert.Null(element);
            Assert.Equal(0, reader.BytesConsumed);
        }

        public static IEnumerable<object[]> ElementParseInvalidDataCases
        {
            get
            {
                yield return new object[] { "nul" };
                yield return new object[] { "{]" };
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\r\n    null ")]
        [InlineData("false")]
        [InlineData("true ")]
        [InlineData("   42.0 ")]
        [InlineData(" \"str\" \r\n")]
        [InlineData(" \"string with escaping: \\u0041\\u0042\\u0043\" \r\n")]
        [InlineData(" [     ]")]
        [InlineData(" [null, true, 42.0, \"str\", [], {}, ]")]
        [InlineData(" {  } ")]
        public static void YamlMarshal_GetRawUtf8Value_RootValue_ReturnsFullValue(string yaml)
        {
            YamlDocumentOptions options = new YamlDocumentOptions { AllowTrailingCommas = true, CommentHandling = YamlCommentHandling.Skip };
            using YamlDocument jDoc = YamlDocument.Parse(yaml, options);
            YamlElement element = jDoc.RootElement;

            ReadOnlySpan<byte> rawValue = YamlMarshal.GetRawUtf8Value(element);
            Assert.Equal(yaml.Trim(), Encoding.UTF8.GetString(rawValue.ToArray()));
        }

        [Fact]
        public static void YamlMarshal_GetRawUtf8Value_DisposedDocument_ThrowsObjectDisposedException()
        {
            YamlDocument jDoc = YamlDocument.Parse("{}");
            YamlElement element = jDoc.RootElement;
            jDoc.Dispose();

            Assert.Throws<ObjectDisposedException>(() => YamlMarshal.GetRawUtf8Value(element));
        }
    }
}
