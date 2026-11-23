// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using System.Buffers;
using System.IO;
using System.Text.Encodings.Web;

namespace System.Text.Yaml.Tests
{
    public sealed class YamlDocumentWriteTests : YamlDomWriteTests
    {
        protected override YamlDocument PrepareDocument(string yamlIn)
        {
            var yamlDocument = YamlDocument.Parse(yamlIn, s_options);
            return yamlDocument;
        }

        protected override void WriteSingleValue(YamlDocument document, Utf8YamlWriter writer)
        {
            document.WriteTo(writer);
        }

        protected override void WriteDocument(YamlDocument document, Utf8YamlWriter writer)
        {
            document.WriteTo(writer);
        }

        [Fact]
        public static void CheckByPassingNullWriter()
        {
            using (YamlDocument doc = YamlDocument.Parse("true", default))
            {
                AssertExtensions.Throws<ArgumentNullException>("writer", () => doc.WriteTo(null));
            }
        }
    }

    public sealed class YamlElementWriteTests : YamlDomWriteTests
    {
        protected override YamlDocument PrepareDocument(string yamlIn)
        {
            return YamlDocument.Parse($" [  {yamlIn}  ]", s_options);
        }

        protected override void WriteSingleValue(YamlDocument document, Utf8YamlWriter writer)
        {
            document.RootElement[0].WriteTo(writer);
        }

        protected override void WriteDocument(YamlDocument document, Utf8YamlWriter writer)
        {
            document.RootElement.WriteTo(writer);
        }

        [Fact]
        public void CheckByPassingNullWriter()
        {
            using (YamlDocument doc = YamlDocument.Parse("true", default))
            {
                YamlElement root = doc.RootElement;
                AssertExtensions.Throws<ArgumentNullException>("writer", () => root.WriteTo(null));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WritePropertyOutsideObject(bool skipValidation)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            using (var doc = YamlDocument.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
            {
                YamlElement root = doc.RootElement;
                var options = new YamlWriterOptions
                {
                    SkipValidation = skipValidation,
                };

                const string CharLabel = "char";
                using var writer = new Utf8YamlWriter(buffer, options);

                if (skipValidation)
                {
                    foreach (YamlElement val in root.EnumerateArray())
                    {
                        writer.WritePropertyName(CharLabel);
                        val.WriteTo(writer);
                        writer.WritePropertyName(CharLabel.AsSpan());
                        val.WriteTo(writer);
                        writer.WritePropertyName("byte"u8);
                        val.WriteTo(writer);
                        writer.WritePropertyName(YamlEncodedText.Encode(CharLabel));
                        val.WriteTo(writer);
                    }

                    writer.Flush();

                    YamlTestHelper.AssertContents(
                        "\"char\":null,\"char\":null,\"byte\":null,\"char\":null," +
                            "\"char\":false,\"char\":false,\"byte\":false,\"char\":false," +
                            "\"char\":true,\"char\":true,\"byte\":true,\"char\":true," +
                            "\"char\":\"hi\",\"char\":\"hi\",\"byte\":\"hi\",\"char\":\"hi\"," +
                            "\"char\":5,\"char\":5,\"byte\":5,\"char\":5," +
                            "\"char\":{},\"char\":{},\"byte\":{},\"char\":{}," +
                            "\"char\":[],\"char\":[],\"byte\":[],\"char\":[]",
                        buffer);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName(CharLabel));
                    Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName(CharLabel.AsSpan()));
                    Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName("byte"u8));
                    Assert.Throws<InvalidOperationException>(() => writer.WritePropertyName(YamlEncodedText.Encode(CharLabel)));

                    writer.Flush();

                    YamlTestHelper.AssertContents("", buffer);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteValueInsideObject(bool skipValidation)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            using (var doc = YamlDocument.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
            {
                YamlElement root = doc.RootElement;
                var options = new YamlWriterOptions
                {
                    SkipValidation = skipValidation,
                };

                using var writer = new Utf8YamlWriter(buffer, options);
                writer.WriteStartObject();

                if (skipValidation)
                {
                    foreach (YamlElement val in root.EnumerateArray())
                    {
                        val.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                    writer.Flush();

                    YamlTestHelper.AssertContents(
                        "{null,false,true,\"hi\",5,{},[]}",
                        buffer);
                }
                else
                {
                    foreach (YamlElement val in root.EnumerateArray())
                    {
                        Assert.Throws<InvalidOperationException>(() => val.WriteTo(writer));
                    }

                    writer.WriteEndObject();
                    writer.Flush();

                    YamlTestHelper.AssertContents("{}", buffer);
                }
            }
        }
    }

    public abstract class YamlDomWriteTests
    {
        protected static readonly YamlDocumentOptions s_options =
            new YamlDocumentOptions
            {
                CommentHandling = YamlCommentHandling.Skip,
            };

        protected abstract YamlDocument PrepareDocument(string yamlIn);
        protected abstract void WriteSingleValue(YamlDocument document, Utf8YamlWriter writer);
        protected abstract void WriteDocument(YamlDocument document, Utf8YamlWriter writer);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumber(bool indented)
        {
            WriteSimpleValue(indented, "42");
        }

        [Theory]
        [InlineData("12E-3", false)]
        [InlineData("1e6", false)]
        [InlineData("1e6", true)]
        [InlineData("1e+6", false)]
        [InlineData("1e+6", true)]
        [InlineData("1e-6", false)]
        [InlineData("1e-6", true)]
        [InlineData("-1e6", false)]
        [InlineData("-1e6", true)]
        [InlineData("-1e+6", false)]
        [InlineData("-1e+6", true)]
        [InlineData("-1e-6", false)]
        [InlineData("-1e-6", true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberScientific(string value, bool indented)
        {
            WriteSimpleValue(indented, value);
        }

        [Theory]
        [InlineData("1.2E+3", false)]
        [InlineData("5.012e-20", false)]
        [InlineData("5.012e-20", true)]
        [InlineData("5.012e20", false)]
        [InlineData("5.012e20", true)]
        [InlineData("5.012e+20", false)]
        [InlineData("5.012e+20", true)]
        [InlineData("-5.012e-20", false)]
        [InlineData("-5.012e-20", true)]
        [InlineData("-5.012e20", false)]
        [InlineData("-5.012e20", true)]
        [InlineData("-5.012e+20", false)]
        [InlineData("-5.012e+20", true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberDecimalScientific(string value, bool indented)
        {
            WriteSimpleValue(indented, value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberOverprecise(bool indented)
        {
            // This value is a reference "potential interoperability problem" from
            // https://tools.ietf.org/html/rfc7159#section-6
            const string PrecisePi = "3.141592653589793238462643383279";

            // To confirm that this test is doing what it intends, one could
            // confirm the printing precision of double, like
            //
            //double precisePi = double.Parse(PrecisePi);
            //Assert.NotEqual(PrecisePi, precisePi.ToString(YamlTestHelper.DoubleFormatString));

            WriteSimpleValue(indented, PrecisePi);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberTooLargeScientific(bool indented)
        {
            // This value is a reference "potential interoperability problem" from
            // https://tools.ietf.org/html/rfc7159#section-6
            const string OneQuarticGoogol = "1e400";

            // This just validates we write the literal number 1e400 even though it is too
            // large to be represented by System.Double and would be converted to
            // PositiveInfinity instead (or throw if using double.Parse on frameworks
            // older than .NET Core 3.0).
            WriteSimpleValue(indented, OneQuarticGoogol);
        }

        [Theory]
        [InlineData("6.022e+23", "6,022e+23")]
        [InlineData("6.022e+23", "6.022f+23")]
        [InlineData("6.022e+23", "6.022e+ 3")]
        [InlineData("6.022e+23", "6e022e+23")]
        [InlineData("6.022e+23", "6.022e+f3")]
        [InlineData("1", "-")]
        [InlineData("12", "+2")]
        [InlineData("12", "1e")]
        [InlineData("12", "1.")]
        [InlineData("12", "02")]
        [InlineData("123", "1e+")]
        [InlineData("123", "1e-")]
        [InlineData("0.12", "0.1e")]
        [InlineData("0.123", "0.1e+")]
        [InlineData("0.123", "0.1e-")]
        [InlineData("10", "+0")]
        [InlineData("101", "-01")]
        [InlineData("12", "1a")]
        [InlineData("10", "00")]
        [InlineData("11", "01")]
        [InlineData("10.5e-012", "10.5e-0.2")]
        [InlineData("10.5e012", "10.5.012")]
        [InlineData("0.123", "0.-23")]
        [InlineData("12345", "hello")]
        public void WriteCorruptedNumber(string parseYaml, string overwriteYaml)
        {
            if (overwriteYaml.Length != parseYaml.Length)
            {
                throw new InvalidOperationException("Invalid test, parseYaml and overwriteYaml must have the same length");
            }

            byte[] utf8Data = Encoding.UTF8.GetBytes(parseYaml);

            using (YamlDocument document = YamlDocument.Parse(utf8Data))
            using (MemoryStream stream = new MemoryStream(Array.Empty<byte>()))
            using (Utf8YamlWriter writer = new Utf8YamlWriter(stream))
            {
                // Use fixed and the older version of GetBytes-in-place because of the .NET Framework build.
                unsafe
                {
                    fixed (byte* dataPtr = utf8Data)
                    fixed (char* inputPtr = overwriteYaml)
                    {
                        // Overwrite the number in the memory buffer still referenced by the document.
                        // If it doesn't hit a 100% overlap then we're not testing what we thought we were.
                        Assert.Equal(
                            utf8Data.Length,
                            Encoding.UTF8.GetBytes(inputPtr, overwriteYaml.Length, dataPtr, utf8Data.Length));
                    }
                }

                YamlElement rootElement = document.RootElement;

                Assert.Equal(overwriteYaml, rootElement.GetRawText());

                AssertExtensions.Throws<ArgumentException>(
                    "utf8FormattedNumber",
                    () => WriteDocument(document, writer));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteAsciiString(bool indented)
        {
            WriteSimpleValue(indented, "\"pizza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEscapedString(bool indented)
        {
            WriteSimpleValue(indented, "\"p\\u0069zza\"", "\"pizza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNonAsciiString(bool indented)
        {
            // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
            // therefore is ingested as the UTF-8 sequence [ C3 AD ].
            //
            // When writing it back out, the writer turns it into a JSON string
            // using escaped codepoint syntax.
            //
            // The subtlety of the input vs output is the number of backslashes (and
            // the hex casing is different to show the difference more aggressively).
            WriteSimpleValue(indented, "\"p\u00cdzza\"", "\"p\\u00CDzza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEscapedNonAsciiString(bool indented)
        {
            // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
            // therefore is ingested as the UTF-8 sequence [ C3 AD ].
            //
            // When writing it back out, the writer turns it into a JSON string
            // using escaped codepoint syntax.
            //
            // The subtlety of the input vs output is the number of backslashes (and
            // the hex casing is different to show the difference more aggressively).
            //
            // The U+007A (lowercase z) is just to make sure nothing weird happens
            // between the de-escape and the UTF-8.
            WriteSimpleValue(indented, "\"p\u00cdz\\u007Aa\"", "\"p\\u00CDzza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteTrue(bool indented)
        {
            WriteSimpleValue(indented, "true");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteFalse(bool indented)
        {
            WriteSimpleValue(indented, "false");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNull(bool indented)
        {
            WriteSimpleValue(indented, "null");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEmptyArray(bool indented)
        {
            WriteComplexValue(
                indented,
                "[        ]",
                "[]",
                "[]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEmptyObject(bool indented)
        {
            WriteComplexValue(
                indented,
                "{     }",
                "{}",
                "{}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteSimpleArray(bool indented)
        {
            WriteComplexValue(
                indented,
                """
                    [ 2, 4,
                    6                       , 0


                    , 1       ]
                    """.NormalizeLineEndings(),
                """
                    [
                      2,
                      4,
                      6,
                      0,
                      1
                    ]
                    """.NormalizeLineEndings(),
                "[2,4,6,0,1]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteSimpleObjectNeedsEscaping(bool indented)
        {
            WriteComplexValue(
                indented,
                """
                        { "prop><erty"   : 3,
                                    "> This is one long & unusual property name. <":
                        4
                        }
                    """,
                """
                    {
                      "prop\u003E\u003Certy": 3,
                      "\u003E This is one long \u0026 unusual property name. \u003C": 4
                    }
                    """,
                "{\"prop\\u003E\\u003Certy\":3,\"\\u003E This is one long \\u0026 unusual property name. \\u003C\":4}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void ReadWriteEscapedPropertyNames(bool indented)
        {
            const string yamlIn = " { \"p\\u0069zza\": 1, \"hello\\u003c\\u003e\": 2, \"normal\": 3 }";

            WriteComplexValue(
                indented,
                yamlIn,
                """
                    {
                      "pizza": 1,
                      "hello\u003c\u003e": 2,
                      "normal": 3
                    }
                    """,
                "{\"pizza\":1,\"hello\\u003c\\u003e\":2,\"normal\":3}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "ectoplasm",
                "42",
                """
                    {
                      "ectoplasm": 42
                    }
                    """.NormalizeLineEndings(),
                "{\"ectoplasm\":42}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberAsPropertyWithLargeName(bool indented)
        {
            var charArray = new char[300];
            charArray.AsSpan().Fill('a');
            charArray[0] = (char)0xEA;
            var propertyName = new string(charArray);

            WritePropertyValueBothForms(
                indented,
                propertyName,
                "42",
                (@"{
  ""\u00EA" + propertyName.Substring(1) + @""": 42
}").NormalizeLineEndings(),
                $"{{\"\\u00EA{propertyName.Substring(1)}\":42}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberScientificAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "m\u00EAl\u00E9e",
                "1e6",
                """
                    {
                      "m\u00EAl\u00E9e": 1e6
                    }
                    """.NormalizeLineEndings(),
                "{\"m\\u00EAl\\u00E9e\":1e6}");
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteValueSurrogatesEscapeString()
        {
            string unicodeString = "\uD800\uDC00\uD803\uDE6D \uD834\uDD1E\uDBFF\uDFFF";
            string expectedStr = "\"\\uD800\\uDC00\\uD803\\uDE6D \\uD834\\uDD1E\\uDBFF\\uDFFF\"";
            string yaml = $"\"{unicodeString}\"";
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yaml))
            {
                using (var writer = new Utf8YamlWriter(buffer))
                {
                    WriteSingleValue(doc, writer);
                }

                YamlTestHelper.AssertContents(expectedStr, buffer);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberOverpreciseAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "test property",
                "3.141592653589793238462643383279",
                """
                    {
                      "test property": 3.141592653589793238462643383279
                    }
                    """.NormalizeLineEndings(),
                "{\"test property\":3.141592653589793238462643383279}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNumberTooLargeAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                // Arabic "kabir" => "big"
                "\u0643\u0628\u064A\u0631",
                "1e400",
                """
                    {
                      "\u0643\u0628\u064A\u0631": 1e400
                    }
                    """.NormalizeLineEndings(),
                "{\"\\u0643\\u0628\\u064A\\u0631\":1e400}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "dinner",
                "\"pizza\"",
                """
                    {
                      "dinner": "pizza"
                    }
                    """.NormalizeLineEndings(),
                "{\"dinner\":\"pizza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEscapedStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "dinner",
                "\"p\\u0069zza\"",
                """
                    {
                      "dinner": "pizza"
                    }
                    """.NormalizeLineEndings(),
                "{\"dinner\":\"pizza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNonAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "lunch",
                "\"p\u00CDzza\"",
                """
                    {
                      "lunch": "p\u00CDzza"
                    }
                    """.NormalizeLineEndings(),
                "{\"lunch\":\"p\\u00CDzza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEscapedNonAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "lunch",
                "\"p\u00CDz\\u007Aa\"",
                """
                    {
                      "lunch": "p\u00CDzza"
                    }
                    """.NormalizeLineEndings(),
                "{\"lunch\":\"p\\u00CDzza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteTrueAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                " boolean ",
                "true",
                """
                    {
                      " boolean ": true
                    }
                    """.NormalizeLineEndings(),
                "{\" boolean \":true}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteFalseAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                " boolean ",
                "false",
                """
                    {
                      " boolean ": false
                    }
                    """.NormalizeLineEndings(),
                "{\" boolean \":false}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteNullAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "someProp",
                "null",
                """
                    {
                      "someProp": null
                    }
                    """.NormalizeLineEndings(),
                "{\"someProp\":null}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEmptyArrayAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "arr",
                "[        ]",
                """
                    {
                      "arr": []
                    }
                    """.NormalizeLineEndings(),
                "{\"arr\":[]}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public void WriteEmptyObjectAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "obj",
                "{       }",
                """
                    {
                      "obj": {}
                    }
                    """.NormalizeLineEndings(),
                "{\"obj\":{}}");
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void WriteIncredibleDepth()
        {
            const int TargetDepth = 500;
            YamlDocumentOptions optionsCopy = s_options;
            optionsCopy.MaxDepth = TargetDepth + 1;
            const int SpacesPre = 12;
            const int SpacesSplit = 85;
            const int SpacesPost = 4;

            byte[] yamlIn = new byte[SpacesPre + TargetDepth + SpacesSplit + TargetDepth + SpacesPost];
            yamlIn.AsSpan(0, SpacesPre).Fill((byte)' ');
            Span<byte> openBrackets = yamlIn.AsSpan(SpacesPre, TargetDepth);
            openBrackets.Fill((byte)'[');
            yamlIn.AsSpan(SpacesPre + TargetDepth, SpacesSplit).Fill((byte)' ');
            Span<byte> closeBrackets = yamlIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit, TargetDepth);
            closeBrackets.Fill((byte)']');
            yamlIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit + TargetDepth).Fill((byte)' ');

            var buffer = new ArrayBufferWriter<byte>(yamlIn.Length);
            using (YamlDocument doc = YamlDocument.Parse(yamlIn, optionsCopy))
            {
                using (var writer = new Utf8YamlWriter(buffer))
                {
                    WriteDocument(doc, writer);
                }

                ReadOnlySpan<byte> formatted = buffer.WrittenSpan;

                Assert.Equal(TargetDepth + TargetDepth, formatted.Length);
                Assert.True(formatted.Slice(0, TargetDepth).SequenceEqual(openBrackets), "OpenBrackets match");
                Assert.True(formatted.Slice(TargetDepth).SequenceEqual(closeBrackets), "CloseBrackets match");
            }
        }

        [Theory]
        [InlineData(false, "\"message\"", "\"message\"", true)]
        [InlineData(true, "\"message\"", "\"message\"", true)]
        [InlineData(false, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
        [InlineData(true, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
        [InlineData(false, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
        [InlineData(true, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
        [Trait("category", "jsonoutput")]
        public void WriteWithRelaxedEscaper(bool indented, string yamlIn, string yamlOut, bool matchesRelaxedEscaping)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                {
                    var options = new YamlWriterOptions
                    {
                        Indented = indented,
                    };

                    using (var writer = new Utf8YamlWriter(buffer, options))
                    {
                        WriteSingleValue(doc, writer);
                    }

                    if (matchesRelaxedEscaping)
                    {
                        YamlTestHelper.AssertContents(yamlOut, buffer);
                    }
                    else
                    {
                        YamlTestHelper.AssertContentsNotEqual(yamlOut, buffer);
                    }
                }

                buffer.Clear();

                {
                    var options = new YamlWriterOptions
                    {
                        Indented = indented,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    };

                    using (var writer = new Utf8YamlWriter(buffer, options))
                    {
                        WriteSingleValue(doc, writer);
                    }

                    YamlTestHelper.AssertContents(yamlOut, buffer);
                }
            }
        }

        private void WriteSimpleValue(bool indented, string yamlIn, string yamlOut = null)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                var options = new YamlWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8YamlWriter(buffer, options))
                {
                    WriteSingleValue(doc, writer);
                }

                YamlTestHelper.AssertContents(yamlOut ?? yamlIn, buffer);
            }
        }

        private void WriteComplexValue(
            bool indented,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            byte[] bufferOutput;

            var options = new YamlWriterOptions
            {
                Indented = indented
            };

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                using (var writer = new Utf8YamlWriter(buffer, options))
                {
                    WriteSingleValue(doc, writer);
                }

                YamlTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);

                bufferOutput = buffer.WrittenSpan.ToArray();
            }

            // After reading the output and writing it again, it should be byte-for-byte identical.
            {
                string bufferString = Encoding.UTF8.GetString(bufferOutput);
                buffer.Clear();

                using (YamlDocument doc2 = PrepareDocument(bufferString))
                {
                    using (var writer = new Utf8YamlWriter(buffer, options))
                    {
                        WriteSingleValue(doc2, writer);
                    }
                }

                Assert.True(buffer.WrittenSpan.SequenceEqual(bufferOutput));
            }
        }

        private void WritePropertyValueBothForms(
            bool indented,
            string propertyName,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            WritePropertyValue(
                indented,
                propertyName,
                yamlIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                propertyName.AsSpan(),
                yamlIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                Encoding.UTF8.GetBytes(propertyName ?? ""),
                yamlIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                YamlEncodedText.Encode(propertyName.AsSpan()),
                yamlIn,
                expectedIndent,
                expectedMinimal);
        }

        private void WritePropertyValue(
            bool indented,
            string propertyName,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                var options = new YamlWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8YamlWriter(buffer, options))
                {

                    writer.WriteStartObject();
                    writer.WritePropertyName(propertyName);
                    WriteSingleValue(doc, writer);
                    writer.WriteEndObject();
                }

                YamlTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private void WritePropertyValue(
            bool indented,
            ReadOnlySpan<char> propertyName,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                var options = new YamlWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8YamlWriter(buffer, options))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(propertyName);
                    WriteSingleValue(doc, writer);
                    writer.WriteEndObject();
                }

                YamlTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private void WritePropertyValue(
            bool indented,
            ReadOnlySpan<byte> propertyName,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                var options = new YamlWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8YamlWriter(buffer, options))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(propertyName);
                    WriteSingleValue(doc, writer);
                    writer.WriteEndObject();
                }

                YamlTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private void WritePropertyValue(
            bool indented,
            YamlEncodedText propertyName,
            string yamlIn,
            string expectedIndent,
            string expectedMinimal)
        {
            var buffer = new ArrayBufferWriter<byte>(1024);

            using (YamlDocument doc = PrepareDocument(yamlIn))
            {
                var options = new YamlWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8YamlWriter(buffer, options))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(propertyName);
                    WriteSingleValue(doc, writer);
                    writer.WriteEndObject();
                }

                YamlTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }
    }
}
