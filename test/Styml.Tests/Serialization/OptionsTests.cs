// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Tests;
using System.Text.Unicode;
using Microsoft.DotNet.RemoteExecutor;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class OptionsTests
    {
        private class TestConverter : YamlConverter<bool>
        {
            public override bool Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8YamlWriter writer, bool value, YamlSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public static void NewDefaultOptions_TypeInfoResolverIsNull()
        {
            var options = new YamlSerializerOptions();
            Assert.Null(options.TypeInfoResolver);
        }

        [Fact]
        public static void NewDefaultOptions_IsNotReadOnly()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            Assert.False(options.IsReadOnly);

            options.MakeReadOnly();
            Assert.True(options.IsReadOnly);
            options.MakeReadOnly(); // Is idempotent operation
            Assert.True(options.IsReadOnly);

            Assert.Throws<InvalidOperationException>(() => options.MaxDepth = 13);
            Assert.Throws<InvalidOperationException>(() => options.Converters.Add(new YamlStringEnumConverter()));
            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolver = null);

            options.MakeReadOnly(); // Is still idempotent operation
            Assert.True(options.IsReadOnly);
        }

        [Fact]
        public static void NewDefaultOptions_MakeReadOnly_NoTypeInfoResolver_ThrowsInvalidOperation()
        {
            var options = new YamlSerializerOptions();
            Assert.False(options.IsReadOnly);

            Assert.Throws<InvalidOperationException>(() => options.MakeReadOnly());
            Assert.False(options.IsReadOnly);

            options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
            options.MakeReadOnly();
            Assert.True(options.IsReadOnly);
        }

        [Fact]
        public static void NewDefaultOptions_MakeReadOnlyWithPopulateParameterFalse_NoTypeInfoResolver_ThrowsInvalidOperation()
        {
            var options = new YamlSerializerOptions();
            Assert.False(options.IsReadOnly);

            Assert.Throws<InvalidOperationException>(() => options.MakeReadOnly(populateMissingResolver: false));
            Assert.Null(options.TypeInfoResolver);
            Assert.False(options.IsReadOnly);
        }

        [Fact]
        public static void NewDefaultOptions_MakeReadOnlyWithPopulateParameterTrue_NoTypeInfoResolver_UsesDefaultTypeInfoResolver()
        {
            var options = new YamlSerializerOptions();
            Assert.Null(options.TypeInfoResolver);
            Assert.False(options.IsReadOnly);

            options.MakeReadOnly(populateMissingResolver: true);

            DefaultYamlTypeInfoResolver resolver = Assert.IsType<DefaultYamlTypeInfoResolver>(options.TypeInfoResolver);
            Assert.True(options.IsReadOnly);

            // Method is idempotent
            options.MakeReadOnly(populateMissingResolver: false);
            Assert.True(options.IsReadOnly);
            Assert.Same(resolver, options.TypeInfoResolver);

            options.MakeReadOnly(populateMissingResolver: true);
            Assert.True(options.IsReadOnly);
            Assert.Same(resolver, options.TypeInfoResolver);

            options.MakeReadOnly();
            Assert.True(options.IsReadOnly);
            Assert.Same(resolver, options.TypeInfoResolver);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void NewDefaultOptions_MakeReadOnlyWithPopulateParameter_HasTypeInfoResolver_DoesNotReplaceResolver(bool populateMissingResolver)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = YamlContext.Default };
            Assert.False(options.IsReadOnly);

            options.MakeReadOnly(populateMissingResolver);

            Assert.True(options.IsReadOnly);
            Assert.Same(YamlContext.Default, options.TypeInfoResolver);
        }

        [Fact]
        public static void TypeInfoResolverCannotBeSetAfterAddingContext()
        {
            var options = new YamlSerializerOptions();
            Assert.False(options.IsReadOnly);

            new YamlContext(options);
            Assert.True(options.IsReadOnly);

            Assert.IsType<YamlContext>(options.TypeInfoResolver);
            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolver = new DefaultYamlTypeInfoResolver());
        }

        [Fact]
        public static void TypeInfoResolverCannotBeSetOnOptionsCreatedFromContext()
        {
            var context = new YamlContext();
            var options = context.Options;
            Assert.Same(context, options.TypeInfoResolver);
            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolver = new DefaultYamlTypeInfoResolver());
        }

        [Fact]
        public static void WhenAddingContextTypeInfoResolverAsContextOptionsAreSameAsOptions()
        {
            var options = new YamlSerializerOptions();
            new YamlContext(options);
            Assert.Same(options, (options.TypeInfoResolver as YamlContext).Options);
        }

        [Fact]
        public static void WhenAddingContext_SettingResolverToNullThrowsInvalidOperationException()
        {
            var options = new YamlSerializerOptions();
            new YamlContext(options);
            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolver = null);
        }

        [Fact]
        public static void TypeInfoResolverCanBeSetAfterContextIsSetThroughTypeInfoResolver()
        {
            var options = new YamlSerializerOptions();
            IYamlTypeInfoResolver resolver = new YamlContext();
            options.TypeInfoResolver = resolver;
            Assert.Same(resolver, options.TypeInfoResolver);

            resolver = new DefaultYamlTypeInfoResolver();
            options.TypeInfoResolver = resolver;
            Assert.Same(resolver, options.TypeInfoResolver);
        }

        [Fact]
        public static void DefaultBufferSizeFail()
        {
            Assert.Throws<ArgumentException>(() => new YamlSerializerOptions().DefaultBufferSize = 0);
            Assert.Throws<ArgumentException>(() => new YamlSerializerOptions().DefaultBufferSize = -1);
        }

        [Fact]
        public static void DefaultBufferSize()
        {
            var options = new YamlSerializerOptions();

            Assert.Equal(16 * 1024, options.DefaultBufferSize);

            options.DefaultBufferSize = 1;
            Assert.Equal(1, options.DefaultBufferSize);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void WriteIndented()
        {
            var obj = new BasicCompany();
            obj.Initialize();

            // Verify default value.
            string yaml = YamlSerializer.Serialize(obj);
            Assert.DoesNotContain(Environment.NewLine, yaml);

            // Verify default value on options.
            var options = new YamlSerializerOptions();
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.DoesNotContain(Environment.NewLine, yaml);

            // Change the value on options.
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(Environment.NewLine, yaml);
        }

        [Theory]
        [InlineData('\f')]
        [InlineData('\n')]
        [InlineData('\r')]
        [InlineData('\0')]
        [InlineData('a')]
        public static void IndentCharacters_WithInvalidChartacters_ThrowsArgumentOutOfRangeException(char character)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.IndentCharacter = character);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(128)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public static void IndentSize_WithInvalidSize_ThrowsArgumentOutOfRangeException(int size)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.IndentSize = size);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void IndentCharacter()
        {
            var obj = new BasicCompany();
            obj.Initialize();

            // Verify default value.
            var defaultIndent = "  ";
            string yaml = YamlSerializer.Serialize(obj);
            Assert.DoesNotContain(defaultIndent, yaml);

            // Verify default value on options.
            var options = new YamlSerializerOptions();
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.DoesNotContain(defaultIndent, yaml);

            // Enable default indentation.
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(defaultIndent, yaml);

            // Set custom indentCharacter without enabling indentation
            var tab = '\t';
            Assert.DoesNotContain(tab, yaml);
            options = new YamlSerializerOptions();
            options.IndentCharacter = tab;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.DoesNotContain(tab, yaml);

            // Set custom indentCharacter with indentation enabled
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            options.IndentCharacter = tab;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(tab, yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void IndentSize()
        {
            var obj = new BasicCompany();
            obj.Initialize();

            var tab = '\t';
            // Set custom indentSize without enabling indentation
            var options = new YamlSerializerOptions();
            options.IndentCharacter = tab;
            options.IndentSize = 1;
            var yaml = YamlSerializer.Serialize(obj, options);
            Assert.DoesNotContain(tab, yaml);

            // Set custom indentSize with indentation enabled
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            options.IndentCharacter = tab;
            options.IndentSize = 4;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(new string(tab, 4), yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ExtensionDataUsesWriterOptions()
        {
            // We just verify whitespace.

            ClassWithExtensionProperty obj = YamlSerializer.Deserialize<ClassWithExtensionProperty>("""{"MyIntMissing":2}""");

            // Verify baseline without options.
            string yaml = YamlSerializer.Serialize(obj);
            Assert.False(HasNewLine());

            // Verify baseline with options.
            var options = new YamlSerializerOptions();
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.False(HasNewLine());

            // Set AllowTrailingCommas to true.
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.True(HasNewLine());

            bool HasNewLine()
            {
                int iEnd = yaml.IndexOf("2", yaml.IndexOf("MyIntMissing"));
                return yaml.Substring(iEnd + 1).StartsWith(Environment.NewLine);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData((int)YamlCommentHandling.Allow)]
        [InlineData(3)]
        [InlineData(byte.MaxValue)]
        [InlineData(byte.MaxValue + 3)] // Other values, like byte.MaxValue + 1 overflows to 0 (i.e. YamlCommentHandling.Disallow), which is valid.
        [InlineData(byte.MaxValue + 4)]
        public static void ReadCommentHandlingDoesNotSupportAllow(int enumValue)
        {
            var options = new YamlSerializerOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value", () => options.ReadCommentHandling = (YamlCommentHandling)enumValue);
        }

        [Theory]
        [InlineData(-1)]
        public static void TestDepthInvalid(int depth)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentOutOfRangeException>("value", () => options.MaxDepth = depth);
        }

        [Fact]
        public static void MaxDepthRead()
        {
            YamlSerializer.Deserialize<BasicCompany>(BasicCompany.s_data);

            var options = new YamlSerializerOptions();

            YamlSerializer.Deserialize<BasicCompany>(BasicCompany.s_data, options);

            options = new YamlSerializerOptions();
            options.MaxDepth = 1;

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<BasicCompany>(BasicCompany.s_data, options));
        }

        [Theory]
        [InlineData(new char[] { '\n' }, '\r')]
        [InlineData(new char[] { '\r', '\n' }, null)]
        [Trait("category", "jsonoutput")]
        public static void NewLine(char[] newLineChars, char? notExpectedNewLineChars)
        {
            var obj = new BasicCompany();
            obj.Initialize();

            var newLine = new string(newLineChars);
            var options = new YamlSerializerOptions();
            var yaml = YamlSerializer.Serialize(obj, options);
            Assert.DoesNotContain(newLine, yaml);

            // Set custom newLine with indentation enabled
            options = new YamlSerializerOptions();
            options.WriteIndented = true;
            options.NewLine = newLine;
            yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(newLine, yaml);

            if (notExpectedNewLineChars is { } notExpected)
            {
                Assert.DoesNotContain(notExpected, yaml);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\r")]
        [InlineData("\n\n")]
        [InlineData("\r\n\r\n")]
        [InlineData("0")]
        [InlineData("a")]
        [InlineData("foo")]
        [InlineData("$")]
        [InlineData(".")]
        [InlineData("\u03b1")]
        public static void TestNewLineInvalid(string value)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentOutOfRangeException>("value", () => options.NewLine = value);
        }

        [Fact]
        public static void TestNewLineNullThrows()
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentNullException>("value", () => options.NewLine = null);
        }

        private class TestClassForEncoding
        {
            public string MyString { get; set; }
        }

        // This is a copy of the test data in System.Text.Yaml.Tests.YamlEncodedTextTests.YamlEncodedTextStringsCustom
        public static IEnumerable<object[]> YamlEncodedTextStringsCustom
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { "age", "\\u0061\\u0067\\u0065" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\"\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\u0022\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u0022\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0032\\u0032\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9>>>>>\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003E\\u003E\\u003E\\u003E\\u003E\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003e\\u003e\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0033\\u0065\\\\\\u0075\\u0030\\u0030\\u0033\\u0065\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003E\\u003E\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0033\\u0045\\\\\\u0075\\u0030\\u0030\\u0033\\u0045\u00EA\u00EA\u00EA\u00EA\u00EA" },
                };
            }
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStringsCustom))]
        [Trait("category", "jsonoutput")]
        public static void CustomEncoderAllowLatin1Supplement(string message, string expectedMessage)
        {
            // Latin-1 Supplement block starts from U+0080 and ends at U+00FF
            JavaScriptEncoder encoder = JavaScriptEncoder.Create(UnicodeRanges.Latin1Supplement);

            var options = new YamlSerializerOptions();
            options.Encoder = encoder;

            var obj = new TestClassForEncoding();
            obj.MyString = message;

            string baselineYaml = YamlSerializer.Serialize(obj);
            Assert.DoesNotContain(expectedMessage, baselineYaml);

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(expectedMessage, yaml);

            obj = YamlSerializer.Deserialize<TestClassForEncoding>(yaml);
            Assert.Equal(obj.MyString, message);
        }

        public static IEnumerable<object[]> YamlEncodedTextStringsCustomAll
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA" },
                    new object[] { "a\u0467\u0466a", "a\u0467\u0466a" },
                };
            }
        }

        [Theory]
        [MemberData(nameof(YamlEncodedTextStringsCustomAll))]
        [Trait("category", "jsonoutput")]
        public static void YamlEncodedTextStringsCustomAllowAll(string message, string expectedMessage)
        {
            // Allow all unicode values (except forbidden characters which we don't have in test data here)
            JavaScriptEncoder encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

            var options = new YamlSerializerOptions();
            options.Encoder = encoder;

            var obj = new TestClassForEncoding();
            obj.MyString = message;

            string baselineYaml = YamlSerializer.Serialize(obj);
            Assert.DoesNotContain(expectedMessage, baselineYaml);

            string yaml = YamlSerializer.Serialize(obj, options);
            Assert.Contains(expectedMessage, yaml);

            obj = YamlSerializer.Deserialize<TestClassForEncoding>(yaml);
            Assert.Equal(obj.MyString, message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Options_GetConverterForObjectYamlElement_GivesCorrectConverter()
        {
            GenericObjectOrYamlElementConverterTestHelper<object>("DefaultObjectConverter", new object(), "{}");
            YamlElement element = YamlElement.Parse("[3]");
            GenericObjectOrYamlElementConverterTestHelper<YamlElement>("YamlElementConverter", element, "[3]");
        }

        [Fact]
        public static void Options_YamlSerializerContext_DoesNotFallbackToReflection()
        {
            var options = YamlContext.Default.Options;
            YamlSerializer.Serialize(new WeatherForecastWithPOCOs(), options); // type supported by context should succeed serialization

            var unsupportedValue = new MyClass();
            Assert.Null(YamlContext.Default.GetTypeInfo(unsupportedValue.GetType()));
            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(unsupportedValue, unsupportedValue.GetType(), YamlContext.Default));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(unsupportedValue, options));
        }

        [Fact]
        public static void YamlSerializer_IsReflectionEnabledByDefault_DefaultsToTrue()
        {
            Assert.True(YamlSerializer.IsReflectionEnabledByDefault);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Options_DisablingIsReflectionEnabledByDefaultSwitch_DefaultOptionsDoesNotSupportReflection()
        {
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault"] = false
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                Assert.False(YamlSerializer.IsReflectionEnabledByDefault);

                var options = YamlSerializerOptions.Default;
                Assert.True(options.IsReadOnly);

                Assert.NotNull(options.TypeInfoResolver);
                Assert.True(options.TypeInfoResolver is not DefaultYamlTypeInfoResolver);
                Assert.Empty(options.TypeInfoResolverChain);

                Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(string)));
                Assert.Throws<NotSupportedException>(() => options.GetConverter(typeof(string)));
                Assert.False(options.TryGetTypeInfo(typeof(string), out YamlTypeInfo? typeInfo));
                Assert.Null(typeInfo);

                InvalidOperationException ex;

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize("string"));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize("string", options));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<string>("\"string\""));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<string>("\"string\"", options));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

            }, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Options_DisablingIsReflectionEnabledByDefaultSwitch_NewOptionsDoesNotSupportReflection()
        {
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault"] = false
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                Assert.False(YamlSerializer.IsReflectionEnabledByDefault);

                var options = new YamlSerializerOptions();
                Assert.False(options.IsReadOnly);

                Assert.Null(options.TypeInfoResolver);
                Assert.Empty(options.TypeInfoResolverChain);

                Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(string)));
                Assert.Throws<NotSupportedException>(() => options.GetConverter(typeof(string)));

                InvalidOperationException ex;

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize("string", options));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<string>("\"string\"", options));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                ex = Assert.Throws<InvalidOperationException>(() => options.MakeReadOnly(populateMissingResolver: true));
                Assert.Contains("YamlSerializerOptions.TypeInfoResolver", ex.Message);

                Assert.False(options.TryGetTypeInfo(typeof(string), out YamlTypeInfo? typeInfo));
                Assert.Null(typeInfo);

                Assert.False(options.IsReadOnly); // failed operations should not lock the instance
                Assert.Empty(options.TypeInfoResolverChain);
                Assert.Null(options.TypeInfoResolver);

                // Can still use reflection via explicit configuration
                options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
                Assert.Equal(new[] { options.TypeInfoResolver }, options.TypeInfoResolverChain);

                Assert.NotNull(options.GetTypeInfo(typeof(string)));
                Assert.NotNull(options.GetConverter(typeof(string)));

                string yaml = YamlSerializer.Serialize("string", options);
                string value = YamlSerializer.Deserialize<string>(yaml, options);
                Assert.Equal("string", value);

            }, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Options_DisablingIsReflectionEnabledByDefaultSwitch_CanUseSourceGen()
        {
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault"] = false
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                Assert.False(YamlSerializer.IsReflectionEnabledByDefault);

                var options = new YamlSerializerOptions();
                options.TypeInfoResolverChain.Add(YamlContext.Default);

                string yaml = YamlSerializer.Serialize(new WeatherForecastWithPOCOs(), options);
                WeatherForecastWithPOCOs result = YamlSerializer.Deserialize<WeatherForecastWithPOCOs>(yaml, options);
                Assert.NotNull(result);

            }, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalTheory(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        [InlineData(false)]
        [InlineData(true)]
        public static void Options_YamlSerializerContext_GetConverter_DoesNotFallBackToReflectionConverter(bool isCompatibilitySwitchExplicitlyDisabled)
        {
            var options = new RemoteInvokeOptions();

            if (isCompatibilitySwitchExplicitlyDisabled)
            {
                options.RuntimeConfigurationOptions.Add("System.Text.Yaml.Serialization.EnableSourceGenReflectionFallback", false);
            }

            RemoteExecutor.Invoke(static () =>
            {
                YamlContext context = YamlContext.Default;
                var unsupportedValue = new MyClass();

                // Default converters have not been rooted yet
                Assert.Null(context.GetTypeInfo(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => context.Options.GetConverter(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(unsupportedValue, context.Options));

                // Root converters process-wide using a default options instance
                var options = new YamlSerializerOptions();
                YamlConverter converter = options.GetConverter(typeof(MyClass));
                Assert.IsAssignableFrom<YamlConverter<MyClass>>(converter);

                // We still can't resolve metadata for MyClass or get a converter using the rooted converters.
                Assert.Null(context.GetTypeInfo(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => context.Options.GetConverter(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(unsupportedValue, context.Options));

            }, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Options_YamlSerializerContext_Net6CompatibilitySwitch_FallsBackToReflectionResolver()
        {
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Text.Yaml.Serialization.EnableSourceGenReflectionFallback"] = true
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                var unsupportedValue = new MyClass { Value = "value" };

                // YamlSerializerContext does not return metadata for the type
                Assert.Null(YamlContext.Default.GetTypeInfo(typeof(MyClass)));

                // Serialization fails using the YamlSerializerContext overload
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(unsupportedValue, unsupportedValue.GetType(), YamlContext.Default));

                // Serialization uses reflection fallback using the YamlSerializerOptions overload
                string yaml = YamlSerializer.Serialize(unsupportedValue, YamlContext.Default.Options);
                YamlTestHelper.AssertYamlEqual("""{"Value":"value", "Thing":null}""", yaml);

                // A converter can be resolved when looking up YamlSerializerOptions
                YamlConverter converter = YamlContext.Default.Options.GetConverter(typeof(MyClass));
                Assert.IsAssignableFrom<YamlConverter<MyClass>>(converter);

                // Serialization using YamlSerializerContext now uses the reflection fallback.
                yaml = YamlSerializer.Serialize(unsupportedValue, unsupportedValue.GetType(), YamlContext.Default);
                YamlTestHelper.AssertYamlEqual("""{"Value":"value", "Thing":null}""", yaml);

            }, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void Options_YamlSerializerContext_Net6CompatibilitySwitch_IsOverriddenByDisablingIsReflectionEnabledByDefault()
        {
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault"] = false,
                    ["System.Text.Yaml.Serialization.EnableSourceGenReflectionFallback"] = true
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                Assert.False(YamlSerializer.IsReflectionEnabledByDefault);

                YamlContext context = YamlContext.Default;
                var unsupportedValue = new MyClass();

                Assert.Null(context.GetTypeInfo(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => context.Options.GetConverter(typeof(MyClass)));
                Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(unsupportedValue, context.Options));

            }, options).Dispose();
        }

        [Fact]
        public static void Options_YamlSerializerContext_Combine_FallbackToReflection()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = YamlTypeInfoResolver.Combine(YamlContext.Default, new DefaultYamlTypeInfoResolver())
            };

            var value = new MyClass();
            string yaml = YamlSerializer.Serialize(value, options);
            YamlTestHelper.AssertYamlEqual("""{"Value":null,"Thing":null}""", yaml);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalTheory(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("category", "jsonoutput")]
        public static void Options_RespectNullableAnnotationsDefault_FeatureSwitch(bool? state)
        {
            var options = new RemoteInvokeOptions();
            if (state.HasValue)
            {
                options.RuntimeConfigurationOptions["System.Text.Yaml.Serialization.RespectNullableAnnotationsDefault"] = state.Value;
            }

            string arg = state ?? false ? "true" : "false";
            RemoteExecutor.Invoke(static arg =>
            {
                bool shouldRespectNullableAnnotations = bool.Parse(arg);

                var yamlOptions = new YamlSerializerOptions();
                Assert.Equal(shouldRespectNullableAnnotations, yamlOptions.RespectNullableAnnotations);
                Assert.Equal(shouldRespectNullableAnnotations, YamlSerializerOptions.Default.RespectNullableAnnotations);

                var value = new NullableAnnotationsTests.NotNullablePropertyClass();
                string expectedYaml = """{"Property":null}""";

                Assert.Null(value.Property);

                if (shouldRespectNullableAnnotations)
                {
                    Assert.Throws<YamlException>(() => YamlSerializer.Serialize(value));
                    Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<NullableAnnotationsTests.NotNullablePropertyClass>(expectedYaml));
                }
                else
                {
                    string yaml = YamlSerializer.Serialize(value, yamlOptions);
                    Assert.Equal(expectedYaml, yaml);
                    value = YamlSerializer.Deserialize<NullableAnnotationsTests.NotNullablePropertyClass>(yaml, yamlOptions);
                    Assert.Null(value.Property);
                }

            }, arg, options).Dispose();
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalTheory(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public static void Options_RespectRequiredConstructorParameters_FeatureSwitch(bool? state)
        {
            var options = new RemoteInvokeOptions();
            if (state.HasValue)
            {
                options.RuntimeConfigurationOptions["System.Text.Yaml.Serialization.RespectRequiredConstructorParametersDefault"] = state.Value;
            }

            string arg = state ?? false ? "true" : "false";
            RemoteExecutor.Invoke(static arg =>
            {
                bool shouldRespectRequiredConstructorParameters = bool.Parse(arg);

                var yamlOptions = new YamlSerializerOptions();
                Assert.Equal(shouldRespectRequiredConstructorParameters, yamlOptions.RespectRequiredConstructorParameters);
                Assert.Equal(shouldRespectRequiredConstructorParameters, YamlSerializerOptions.Default.RespectRequiredConstructorParameters);

                string yaml = """{"X":1,"Z":3}""";

                if (shouldRespectRequiredConstructorParameters)
                {
                    YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Point_3D>(yaml));
                    Assert.Contains("'Y'", ex.Message);
                }
                else
                {
                    Point_3D result = YamlSerializer.Deserialize<Point_3D>(yaml);
                    Assert.Equal(1, result.X);
                    Assert.Equal(0, result.Y);
                    Assert.Equal(3, result.Z);
                }

            }, arg, options).Dispose();
        }

        private static void GenericObjectOrYamlElementConverterTestHelper<T>(string converterName, object objectValue, string stringValue)
        {
            var options = new YamlSerializerOptions();

            YamlConverter<T> converter = (YamlConverter<T>)options.GetConverter(typeof(T));
            Assert.Equal(converterName, converter.GetType().Name);

            ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes(stringValue);
            Utf8YamlReader reader = new Utf8YamlReader(data);
            reader.Read();
            T readValue = converter.Read(ref reader, typeof(T), options);

            if (readValue is YamlElement element)
            {
                YamlTestHelper.AssertYamlEqual(stringValue, element.ToString());
            }
            else
            {
                Assert.Fail("Must be YamlElement");
            }

            using (var stream = new MemoryStream())
            using (var writer = new Utf8YamlWriter(stream))
            {
                converter.Write(writer, (T)objectValue, options);
                writer.Flush();
                Assert.Equal(stringValue, Encoding.UTF8.GetString(stream.ToArray()));

                writer.Reset(stream);
                converter.Write(writer, (T)objectValue, null); // Test with null option
                writer.Flush();
                Assert.Equal(stringValue + stringValue, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Options_GetConverter_GivesCorrectDefaultConverterAndReadWriteSuccess()
        {
            var options = new YamlSerializerOptions();
            GenericConverterTestHelper<bool>("BooleanConverter", true, "true", options);
            GenericConverterTestHelper<byte>("ByteConverter", (byte)128, "128", options);
            GenericConverterTestHelper<char>("CharConverter", 'A', "\"A\"", options);
            GenericConverterTestHelper<double>("DoubleConverter", 15.1d, "15.1", options);
            GenericConverterTestHelper<SampleEnum>("EnumConverter`1", SampleEnum.Two, "2", options);
            GenericConverterTestHelper<short>("Int16Converter", (short)5, "5", options);
            GenericConverterTestHelper<int>("Int32Converter", -100, "-100", options);
            GenericConverterTestHelper<long>("Int64Converter", (long)11111, "11111", options);
            GenericConverterTestHelper<sbyte>("SByteConverter", (sbyte)-121, "-121", options);
            GenericConverterTestHelper<float>("SingleConverter", 14.5f, "14.5", options);
            GenericConverterTestHelper<string>("StringConverter", "Hello", "\"Hello\"", options);
            GenericConverterTestHelper<ushort>("UInt16Converter", (ushort)1206, "1206", options);
            GenericConverterTestHelper<uint>("UInt32Converter", (uint)3333, "3333", options);
            GenericConverterTestHelper<ulong>("UInt64Converter", (ulong)44444, "44444", options);
            GenericConverterTestHelper<decimal>("DecimalConverter", 3.3m, "3.3", options);
            GenericConverterTestHelper<byte[]>("ByteArrayConverter", new byte[] { 1, 2, 3, 4 }, "\"AQIDBA==\"", options);
            GenericConverterTestHelper<DateTime>("DateTimeConverter", new DateTime(2018, 12, 3), "\"2018-12-03T00:00:00\"", options);
            GenericConverterTestHelper<DateTimeOffset>("DateTimeOffsetConverter", new DateTimeOffset(new DateTime(2018, 12, 3, 00, 00, 00, DateTimeKind.Utc)), "\"2018-12-03T00:00:00+00:00\"", options);
            Guid testGuid = new Guid();
            GenericConverterTestHelper<Guid>("GuidConverter", testGuid, $"\"{testGuid}\"", options);
            GenericConverterTestHelper<Uri>("UriConverter", new Uri("http://test.com"), "\"http://test.com\"", options);
        }

        [Fact]
        // KeyValuePair converter is not a primitive YamlConverter<T>, so there's no way to properly flow the ReadStack state in the direct call to the serializer.
        [ActiveIssue("https://github.com/dotnet/runtime/issues/50205")]
        public static void Options_GetConverter_GivesCorrectKeyValuePairConverter()
        {
            GenericConverterTestHelper<KeyValuePair<string, string>>(
                converterName: "KeyValuePairConverter`2",
                objectValue: new KeyValuePair<string, string>("key", "value"),
                stringValue: """{"Key":"key","Value":"value"}""",
                options: new YamlSerializerOptions(),
                nullOptionOkay: false);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void Options_GetConverter_GivesCorrectCustomConverterAndReadWriteSuccess()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new CustomConverterTests.LongArrayConverter());
            GenericConverterTestHelper<long[]>("LongArrayConverter", new long[] { 1, 2, 3, 4 }, "\"1,2,3,4\"", options);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(Poco))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void Options_GetConverter_CustomResolver_DoesNotReturnConverterForUnsupportedType(Type type)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new NullResolver() };
            Assert.Throws<NotSupportedException>(() => options.GetConverter(type));
        }

        public class NullResolver : IYamlTypeInfoResolver
        {
            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options) => null;
        }

        private static void GenericConverterTestHelper<T>(string converterName, object objectValue, string stringValue, YamlSerializerOptions options, bool nullOptionOkay = true)
        {
            YamlConverter<T> converter = (YamlConverter<T>)options.GetConverter(typeof(T));

            Assert.True(converter.CanConvert(typeof(T)));
            Assert.Equal(converterName, converter.GetType().Name);

            ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes(stringValue);
            Utf8YamlReader reader = new Utf8YamlReader(data);
            reader.Read();

            T valueRead = converter.Read(ref reader, typeof(T), nullOptionOkay ? null: options);
            Assert.Equal(objectValue, valueRead);

            if (reader.TokenType != YamlTokenType.EndObject)
            {
                valueRead = converter.Read(ref reader, typeof(T), options);  // Test with given option if reader position haven't advanced.
                Assert.Equal(objectValue, valueRead);
            }

            using (var stream = new MemoryStream())
            using (var writer = new Utf8YamlWriter(stream))
            {
                converter.Write(writer, (T)objectValue, options);
                writer.Flush();
                Assert.Equal(stringValue, Encoding.UTF8.GetString(stream.ToArray()));

                writer.Reset(stream);
                converter.Write(writer, (T)objectValue, nullOptionOkay ? null : options);
                writer.Flush();
                Assert.Equal(stringValue + stringValue, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        [Fact]
        public static void CopyConstructor_OriginalLocked()
        {
            YamlSerializerOptions options = new YamlSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            Assert.False(options.IsReadOnly);

            // Perform serialization with options, after which it will be locked.
            YamlSerializer.Serialize("1", options);

            Assert.True(options.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => options.ReferenceHandler = ReferenceHandler.Preserve);

            var newOptions = new YamlSerializerOptions(options);
            Assert.False(newOptions.IsReadOnly);
            YamlTestHelper.AssertOptionsEqual(options, newOptions);

            // No exception is thrown on mutating the new options instance because it is "unlocked".
            newOptions.ReferenceHandler = ReferenceHandler.Preserve;
        }

        [Fact]
        public static void CopyConstructor_MaxDepth()
        {
            static void RunTest(int maxDepth, int effectiveMaxDepth)
            {
                var options = new YamlSerializerOptions { MaxDepth = maxDepth };
                var newOptions = new YamlSerializerOptions(options);

                Assert.Equal(maxDepth, options.MaxDepth);
                Assert.Equal(maxDepth, newOptions.MaxDepth);

                // Test for default effective max depth in exception message.
                var myList = new List<object>();
                myList.Add(myList);

                string effectiveMaxDepthAsStr = effectiveMaxDepth.ToString();

                YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(myList, options));
                Assert.Contains(effectiveMaxDepthAsStr, ex.Message);

                ex = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(myList, newOptions));
                Assert.Contains(effectiveMaxDepthAsStr, ex.Message);
            }

            // Zero max depth
            RunTest(0, 64);

            // Specified max depth
            RunTest(25, 25);
        }

        [Fact]
        public static void CopyConstructor_CopiesAllPublicProperties()
        {
            YamlSerializerOptions options = GetFullyPopulatedOptionsInstance();
            var newOptions = new YamlSerializerOptions(options);
            YamlTestHelper.AssertOptionsEqual(options, newOptions);
        }

        [Fact]
        public static void CopyConstructor_CopiesYamlSerializerContext()
        {
            YamlSerializerOptions options = new YamlSerializerOptions();
            new YamlContext(options);
            YamlContext original = Assert.IsType<YamlContext>(options.TypeInfoResolver);

            // copy constructor copies the YamlSerializerContext
            var newOptions = new YamlSerializerOptions(options);
            Assert.Same(original, newOptions.TypeInfoResolver);

            // resolving metadata returns metadata tied to the new options
            YamlTypeInfo typeInfo = newOptions.TypeInfoResolver.GetTypeInfo(typeof(int), newOptions);
            Assert.Same(typeInfo.Options, newOptions);

            // it is possible to reset the resolver
            newOptions.TypeInfoResolver = null;
            Assert.Null(newOptions.TypeInfoResolver);
        }

        [Fact]
        public static void CopyConstructor_NullInput()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new YamlSerializerOptions(null));
            Assert.Contains("options", ex.Message);
        }

        [Fact]
        public static void YamlSerializerOptions_Default_MatchesDefaultConstructorWithDefaultResolver()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = YamlSerializerOptions.Default.TypeInfoResolver };
            YamlSerializerOptions optionsSingleton = YamlSerializerOptions.Default;
            YamlTestHelper.AssertOptionsEqual(options, optionsSingleton);
        }

        [Fact]
        public static void YamlSerializerOptions_Default_ReturnsSameInstance()
        {
            Assert.Same(YamlSerializerOptions.Default, YamlSerializerOptions.Default);
        }

        [Fact]
        public static void YamlSerializerOptions_Default_IsReadOnly()
        {
            var optionsSingleton = YamlSerializerOptions.Default;
            Assert.True(optionsSingleton.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.IncludeFields = true);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.Converters.Add(new YamlStringEnumConverter()));
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.TypeInfoResolverChain.Add(new DefaultYamlTypeInfoResolver()));
            Assert.Throws<InvalidOperationException>(() => new YamlContext(optionsSingleton));

            DefaultYamlTypeInfoResolver resolver = Assert.IsType<DefaultYamlTypeInfoResolver>(optionsSingleton.TypeInfoResolver);
            Assert.Throws<InvalidOperationException>(() => resolver.Modifiers.Clear());
            Assert.Throws<InvalidOperationException>(() => resolver.Modifiers.Add(ti => { }));
            Assert.Throws<InvalidOperationException>(() => resolver.Modifiers.Insert(0, ti => { }));

            // MakeReadOnly is idempotent.
            optionsSingleton.MakeReadOnly();
            optionsSingleton.MakeReadOnly(populateMissingResolver: false);
            optionsSingleton.MakeReadOnly(populateMissingResolver: true);
            Assert.Same(resolver, optionsSingleton.TypeInfoResolver);
        }

        [Fact]
        public static void YamlSerializerOptions_Web_MatchesConstructorWithYamlSerializerDefaults()
        {
            var options = new YamlSerializerOptions(YamlSerializerDefaults.Web)
            {
                TypeInfoResolver = YamlSerializerOptions.Default.TypeInfoResolver
            };

            YamlSerializerOptions optionsSingleton = YamlSerializerOptions.Web;

            Assert.Equal(YamlNamingPolicy.CamelCase, options.PropertyNamingPolicy);
            Assert.True(options.PropertyNameCaseInsensitive);
            Assert.Equal(YamlNumberHandling.AllowReadingFromString, options.NumberHandling);

            YamlTestHelper.AssertOptionsEqual(options, optionsSingleton);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void YamlSerializerOptions_Web_SerializesWithExpectedSettings()
        {
            string yaml = YamlSerializer.Serialize(new { PropertyName = 42 }, YamlSerializerOptions.Web);
            Assert.Equal("""{"propertyName":42}""", yaml);
        }

        [Fact]
        public static void YamlSerializerOptions_Web_ReturnsSameInstance()
        {
            Assert.Same(YamlSerializerOptions.Web, YamlSerializerOptions.Web);
        }

        [Fact]
        public static void YamlSerializerOptions_Web_IsReadOnly()
        {
            var optionsSingleton = YamlSerializerOptions.Web;
            Assert.True(optionsSingleton.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.PropertyNameCaseInsensitive = true);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.PropertyNamingPolicy = YamlNamingPolicy.CamelCase);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.NumberHandling = YamlNumberHandling.AllowReadingFromString);
            Assert.Throws<InvalidOperationException>(() => new YamlContext(optionsSingleton));
        }

        [Fact]
        public static void YamlSerializerOptions_Strict_MatchesConstructorWithYamlSerializerDefaults()
        {
            var options = new YamlSerializerOptions(YamlSerializerDefaults.Strict)
            {
                TypeInfoResolver = YamlSerializerOptions.Default.TypeInfoResolver
            };

            YamlSerializerOptions optionsSingleton = YamlSerializerOptions.Strict;

            AssertExtensions.FalseExpression(options.AllowDuplicateProperties);
            AssertExtensions.FalseExpression(options.PropertyNameCaseInsensitive);
            AssertExtensions.TrueExpression(options.RespectNullableAnnotations);
            AssertExtensions.TrueExpression(options.RespectRequiredConstructorParameters);
            Assert.Equal(YamlUnmappedMemberHandling.Disallow, options.UnmappedMemberHandling);

            YamlTestHelper.AssertOptionsEqual(options, optionsSingleton);
        }

        [Fact]
        public static void YamlSerializerOptions_Strict_SerializesWithExpectedSettings()
        {
            YamlSerializerOptions options = YamlSerializerOptions.Strict;
            AssertExtensions.ThrowsContains<YamlException>(
                () => YamlSerializer.Deserialize<Dictionary<string, int>>("""{"foo":1, "foo":2}""", options),
                "Duplicate");
        }

        [Fact]
        public static void YamlSerializerOptions_Strict_ReturnsSameInstance()
        {
            Assert.Same(YamlSerializerOptions.Strict, YamlSerializerOptions.Strict);
        }

        [Fact]
        public static void YamlSerializerOptions_Strict_IsReadOnly()
        {
            var optionsSingleton = YamlSerializerOptions.Strict;
            Assert.True(optionsSingleton.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.AllowDuplicateProperties = true);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.PropertyNameCaseInsensitive = true);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.RespectNullableAnnotations = false);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.RespectRequiredConstructorParameters = false);
            Assert.Throws<InvalidOperationException>(() => optionsSingleton.UnmappedMemberHandling = YamlUnmappedMemberHandling.Skip);

            Assert.Throws<InvalidOperationException>(() => new YamlContext(optionsSingleton));
        }

        [Theory]
        [MemberData(nameof(GetInitialTypeInfoResolversAndExpectedChains))]
        public static void TypeInfoResolverChain_SetTypeInfoResolver_ReturnsExpectedChain(
            IYamlTypeInfoResolver? initialResolver,
            IYamlTypeInfoResolver[] expectedChainValues)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = initialResolver };

            Assert.Equal(expectedChainValues, options.TypeInfoResolverChain);

            // We materialized the resolver chain without mutating it.
            // This should not have impact on the reported resolver
            Assert.Same(initialResolver, options.TypeInfoResolver);
        }

        [Theory]
        [MemberData(nameof(GetInitialTypeInfoResolversAndExpectedChains))]
        public static void TypeInfoResolverChain_Add_UpdatesTypeInfoResolver(
            IYamlTypeInfoResolver? initialResolver,
            IYamlTypeInfoResolver[] expectedChainValues)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = initialResolver };
            Assert.Equal(expectedChainValues, options.TypeInfoResolverChain);
            Assert.Same(initialResolver, options.TypeInfoResolver);

            var appendedResolver = new DefaultYamlTypeInfoResolver();
            options.TypeInfoResolverChain.Add(appendedResolver);

            Assert.Equal(expectedChainValues.Concat(new[] { appendedResolver }), options.TypeInfoResolverChain);
            Assert.NotSame(initialResolver, options.TypeInfoResolver);
            Assert.Same(options.TypeInfoResolverChain, options.TypeInfoResolver);
        }

        [Theory]
        [MemberData(nameof(GetInitialTypeInfoResolversAndExpectedChains))]
        public static void TypeInfoResolverChain_Clear_UpdatesTypeInfoResolver(
            IYamlTypeInfoResolver? initialResolver,
            IYamlTypeInfoResolver[] expectedChainValues)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = initialResolver };
            Assert.Equal(expectedChainValues, options.TypeInfoResolverChain);
            Assert.Same(initialResolver, options.TypeInfoResolver);

            options.TypeInfoResolverChain.Clear();

            Assert.Empty(options.TypeInfoResolverChain);
            Assert.Same(options.TypeInfoResolverChain, options.TypeInfoResolver);
        }

        public static IEnumerable<object[]> GetInitialTypeInfoResolversAndExpectedChains()
        {
            yield return WrapArgs(null, Array.Empty<IYamlTypeInfoResolver>());

            var defaultResolver = new DefaultYamlTypeInfoResolver();
            yield return WrapArgs(defaultResolver, new[] { defaultResolver });
            yield return WrapArgs(YamlContext.Default, new[] { YamlContext.Default });
            yield return WrapArgs(YamlTypeInfoResolver.Combine(), Array.Empty<IYamlTypeInfoResolver>());
            yield return WrapArgs(
                YamlTypeInfoResolver.Combine(YamlContext.Default, defaultResolver, null, YamlContext.Default),
                new IYamlTypeInfoResolver[] { YamlContext.Default, defaultResolver, YamlContext.Default });

            var optionsWithChain = new YamlSerializerOptions { TypeInfoResolverChain = { YamlContext.Default, defaultResolver, YamlContext.Default } };
            yield return WrapArgs(optionsWithChain.TypeInfoResolver, new IYamlTypeInfoResolver[] { YamlContext.Default, defaultResolver, YamlContext.Default });

            static object[] WrapArgs(IYamlTypeInfoResolver? yamlTypeInfoResolver, IYamlTypeInfoResolver[] expectedChainValues)
                => new object[] { yamlTypeInfoResolver, expectedChainValues };
        }

        [Fact]
        public static void TypeInfoResolverChain_SetToOtherOptions_UsesCopy()
        {
            var defaultResolver = new DefaultYamlTypeInfoResolver();
            var originalOptions = new YamlSerializerOptions
            {
                TypeInfoResolverChain = { YamlContext.Default, YamlContext.Default, defaultResolver }
            };

            // The TypeInfoResolver of the original options should be the chain itself
            Assert.Same(originalOptions.TypeInfoResolver, originalOptions.TypeInfoResolverChain);
            Assert.Equal(new IYamlTypeInfoResolver[] { YamlContext.Default, YamlContext.Default, defaultResolver }, originalOptions.TypeInfoResolverChain);

            var optionsCopy = new YamlSerializerOptions
            {
                TypeInfoResolver = originalOptions.TypeInfoResolver
            };

            // The new options should preserve the original TypeInfoResolver but use a copy for the chain that it owns
            Assert.Same(originalOptions.TypeInfoResolver, optionsCopy.TypeInfoResolver);
            Assert.Equal(new IYamlTypeInfoResolver[] { YamlContext.Default, YamlContext.Default, defaultResolver }, optionsCopy.TypeInfoResolverChain);
            Assert.NotSame(originalOptions.TypeInfoResolverChain, optionsCopy.TypeInfoResolverChain);

            // Mutating the resolver chain should update the TypeInfoResolver property but not impact the original options
            var appendedResolver = new DefaultYamlTypeInfoResolver();
            optionsCopy.TypeInfoResolverChain.Add(appendedResolver);

            Assert.NotSame(originalOptions.TypeInfoResolver, optionsCopy.TypeInfoResolver);
            Assert.Equal(new IYamlTypeInfoResolver[] { YamlContext.Default, YamlContext.Default, defaultResolver }, originalOptions.TypeInfoResolverChain);
            Assert.Equal(new IYamlTypeInfoResolver[] { YamlContext.Default, YamlContext.Default, defaultResolver, appendedResolver }, optionsCopy.TypeInfoResolverChain);
        }

        [Fact]
        public static void TypeInfoResolverChain_UpdateTypeInfoResolver_ResetsResolverChain()
        {
            var defaultResolver = new DefaultYamlTypeInfoResolver();
            var options = new YamlSerializerOptions { TypeInfoResolverChain = { YamlContext.Default, YamlContext.Default, defaultResolver } };
            Assert.Equal(new IYamlTypeInfoResolver[] { YamlContext.Default, YamlContext.Default, defaultResolver }, options.TypeInfoResolverChain);

            options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();

            Assert.Equal(new IYamlTypeInfoResolver[] { options.TypeInfoResolver }, options.TypeInfoResolverChain);
        }

        [Fact]
        public static void TypeInfoResolverChain_AppendTypeInfoResolverToChain_ThrowsInvalidOperationException()
        {
            var defaultResolver = new DefaultYamlTypeInfoResolver();
            var options = new YamlSerializerOptions { TypeInfoResolver = defaultResolver };

            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolverChain.Add(options.TypeInfoResolver));
        }

        [Fact]
        public static void TypeInfoResolverChain_AddsModifier_WorksAsExpected()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/112735
            bool isModifierInvoked = false;
            var defaultResolver = new DefaultYamlTypeInfoResolver();
            var options = new YamlSerializerOptions { TypeInfoResolver = YamlContext.Default };
            options.TypeInfoResolverChain.Add(new DefaultYamlTypeInfoResolver());
            options.TypeInfoResolver = options.TypeInfoResolver!.WithAddedModifier(_ => isModifierInvoked = true);
            options.MakeReadOnly();

            options.GetTypeInfo(typeof(List<List<List<string>>>)); // A type not in YamlContext.Default

            Assert.True(isModifierInvoked);
        }

        [Fact]
        public static void TypeInfoResolverChain_AppendTypeInfoResolverChainToChain_ThrowsInvalidOperationException()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolverChain = { YamlContext.Default, YamlContext.Default }
            };

            var castChain = (IYamlTypeInfoResolver)options.TypeInfoResolverChain;
            Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolverChain.Add(castChain));
        }

        [Fact]
        public static void TypeInfoResolverChain_IsSameAsResolver_SettingChainToResolver_PreservesConfiguration()
        {
            var options = new YamlSerializerOptions
            {
                TypeInfoResolverChain = { YamlContext.Default, YamlContext.Default }
            };

            Assert.Same(options.TypeInfoResolverChain, options.TypeInfoResolver);

            options.TypeInfoResolver = options.TypeInfoResolver;

            Assert.Equal(new[] { YamlContext.Default, YamlContext.Default }, options.TypeInfoResolverChain);
            Assert.Same(options.TypeInfoResolverChain, options.TypeInfoResolver);
        }

        [Fact]
        public static void TypeInfoResolverChain_ResolvesInExpectedOrder()
        {
            int resolver1Invocations = 0;
            int resolver2Invocations = 0;
            int resolver3Invocations = 0;
            int resolver4Invocations = 0;

            var resolver1 = new TestResolver((type, options) =>
            {
                resolver1Invocations++;
                return null;
            });

            var resolver2 = new TestResolver((type, options) =>
            {
                resolver2Invocations++;
                return (type == typeof(int)) ? YamlTypeInfo.CreateYamlTypeInfo(type, options) : null;
            });

            var resolver3 = new TestResolver((type, options) =>
            {
                resolver3Invocations++;
                return (type == typeof(int) || type == typeof(string)) ? YamlTypeInfo.CreateYamlTypeInfo(type, options) : null;
            });

            var resolver4 = new TestResolver((type, options) =>
            {
                resolver4Invocations++;
                return (type == typeof(int) || type == typeof(string) || type == typeof(double)) ? YamlTypeInfo.CreateYamlTypeInfo(type, options) : null;
            });

            var options = new YamlSerializerOptions
            {
                TypeInfoResolverChain = { resolver1, resolver2, resolver3, resolver4 }
            };

            options.GetTypeInfo(typeof(int));
            Assert.Equal(1, resolver1Invocations);
            Assert.Equal(1, resolver2Invocations);
            Assert.Equal(0, resolver3Invocations);
            Assert.Equal(0, resolver4Invocations);

            options.GetTypeInfo(typeof(string));
            Assert.Equal(2, resolver1Invocations);
            Assert.Equal(2, resolver2Invocations);
            Assert.Equal(1, resolver3Invocations);
            Assert.Equal(0, resolver4Invocations);

            options.GetTypeInfo(typeof(double));
            Assert.Equal(3, resolver1Invocations);
            Assert.Equal(3, resolver2Invocations);
            Assert.Equal(2, resolver3Invocations);
            Assert.Equal(1, resolver4Invocations);
        }

        [Fact]
        public static void DefaultSerializerOptions_General()
        {
            var options = new YamlSerializerOptions();
            var newOptions = new YamlSerializerOptions(YamlSerializerDefaults.General);
            Assert.False(newOptions.IsReadOnly);
            YamlTestHelper.AssertOptionsEqual(options, newOptions);
        }

        [Fact]
        public static void PredefinedSerializerOptions_Web()
        {
            var options = new YamlSerializerOptions(YamlSerializerDefaults.Web);
            Assert.True(options.PropertyNameCaseInsensitive);
            Assert.Same(YamlNamingPolicy.CamelCase, options.PropertyNamingPolicy);
            Assert.Equal(YamlNumberHandling.AllowReadingFromString, options.NumberHandling);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public static void PredefinedSerializerOptions_UnhandledDefaults(int enumValue)
        {
            var outOfRangeSerializerDefaults = (YamlSerializerDefaults)enumValue;
            Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions(outOfRangeSerializerDefaults));
        }

        private static YamlSerializerOptions GetFullyPopulatedOptionsInstance()
        {
            var options = new YamlSerializerOptions();

            foreach (PropertyInfo property in typeof(YamlSerializerOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Type propertyType = property.PropertyType;

                if (propertyType == typeof(bool))
                {
                    if (property.Name is not nameof(YamlSerializerOptions.IsReadOnly)) // Property is not structural and cannot be set.
                    {
                        property.SetValue(options, true);
                    }
                }
                if (propertyType == typeof(int))
                {
                    property.SetValue(options, 32);
                }
                else if (propertyType == typeof(string))
                {
                    if (property.Name is nameof(YamlSerializerOptions.NewLine))
                    {
                        property.SetValue(options, "\n");
                    }
                    else
                    {
                        property.SetValue(options, "\t");
                    }
                }
                else if (propertyType == typeof(IList<YamlConverter>))
                {
                    options.Converters.Add(new YamlStringEnumConverter());
                    options.Converters.Add(new ConverterForInt32());
                }
                else if (propertyType == typeof(IList<YamlTypeClassifierFactory>))
                {
                    options.TypeClassifiers.Add(new SampleClassifierForCopyTest());
                }
                else if (propertyType == typeof(JavaScriptEncoder))
                {
                    options.Encoder = JavaScriptEncoder.Default;
                }
                else if (propertyType == typeof(YamlNamingPolicy))
                {
                    options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase;
                    options.DictionaryKeyPolicy = YamlNamingPolicy.SnakeCaseLower;
                }
                else if (propertyType == typeof(ReferenceHandler))
                {
                    options.ReferenceHandler = ReferenceHandler.Preserve;
                }
                else if (propertyType == typeof(IYamlTypeInfoResolver))
                {
                    options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
                }
                else if (propertyType == typeof(IList<IYamlTypeInfoResolver>))
                {
                    // Do nothing -- setting the TypeInfoResolver property should ensure this is populated.
                }
                else if (propertyType == typeof(IDictionary<string, Type>))
                {
                    // Styml M5d: TagMappings (get-only, as Converters is).
                    options.TagMappings.Add("!copied", typeof(Version));
                }
                else if (propertyType.IsValueType)
                {
                    options.ReadCommentHandling = YamlCommentHandling.Disallow;
                    options.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault;
                    options.NumberHandling = YamlNumberHandling.AllowReadingFromString;
                    options.UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode;
                    options.UnmappedMemberHandling = YamlUnmappedMemberHandling.Disallow;
                    options.MappingOrder = YamlMappingOrder.Sorted; // Styml M5f: so that the copy constructor's copy is checked.
                }
                else
                {
                    // An exception thrown from here means this test should be updated
                    // to reflect any newly added properties on YamlSerializerOptions.
                    property.SetValue(options, Activator.CreateInstance(propertyType));
                }
            }

            return options;
        }

        [Fact]
        public static void CannotSet_DefaultIgnoreCondition_To_Always()
        {
            Assert.Throws<ArgumentException>(() => new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.Always });
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/36605")]
        public static void ConverterRead_VerifyInvalidTypeToConvertFails()
        {
            var options = new YamlSerializerOptions();
            Type typeToConvert = typeof(KeyValuePair<int, int>);
            byte[] bytes = Encoding.UTF8.GetBytes("""{"Key":1,"Value":2}""");

            YamlConverter<KeyValuePair<int, int>> converter =
                (YamlConverter<KeyValuePair<int, int>>)options.GetConverter(typeToConvert);

            // Baseline
            var reader = new Utf8YamlReader(bytes);
            reader.Read();
            KeyValuePair<int, int> kvp = converter.Read(ref reader, typeToConvert, options);
            Assert.Equal(1, kvp.Key);
            Assert.Equal(2, kvp.Value);

            // Test
            reader = new Utf8YamlReader(bytes);
            reader.Read();
            try
            {
                converter.Read(ref reader, typeof(Dictionary<string, int>), options);
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidOperationException))
                {
                    throw ex;
                }
            }
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void GetTypeInfo_MutableOptionsInstance(Type type)
        {
            var options = new YamlSerializerOptions();

            // An unset resolver results in NotSupportedException.
            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(type));
            // And returns false in the Try-method
            Assert.False(options.TryGetTypeInfo(type, out YamlTypeInfo? typeInfo));
            Assert.Null(typeInfo);

            options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
            typeInfo = options.GetTypeInfo(type);
            Assert.Equal(type, typeInfo.Type);
            Assert.False(typeInfo.IsReadOnly);

            YamlTypeInfo typeInfo2 = options.GetTypeInfo(type);
            Assert.Equal(type, typeInfo2.Type);
            Assert.False(typeInfo2.IsReadOnly);

            Assert.NotSame(typeInfo, typeInfo2);

            Assert.True(options.TryGetTypeInfo(type, out YamlTypeInfo? typeInfo3));
            Assert.Equal(type, typeInfo3.Type);
            Assert.False(typeInfo3.IsReadOnly);

            Assert.NotSame(typeInfo, typeInfo3);

            options.WriteIndented = true; // can mutate without issue
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(Dictionary<int, string>))]
        public static void GetTypeInfo_ImmutableOptionsInstance(Type type)
        {
            var options = new YamlSerializerOptions();
            YamlSerializer.Serialize(42, options);

            YamlTypeInfo typeInfo = options.GetTypeInfo(type);
            Assert.Equal(type, typeInfo.Type);
            Assert.True(typeInfo.IsReadOnly);

            YamlTypeInfo typeInfo2 = options.GetTypeInfo(type);
            Assert.Same(typeInfo, typeInfo2);

            Assert.True(options.TryGetTypeInfo(type, out YamlTypeInfo? typeInfo3));
            Assert.Same(typeInfo, typeInfo3);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GetTypeInfo_MutableOptions_CanModifyMetadata()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            YamlTypeInfo<TestClassForEncoding> jti = options.GetTypeInfo<TestClassForEncoding>();

            Assert.False(jti.IsReadOnly);
            Assert.Equal(1, jti.Properties.Count);
            jti.Properties.Clear();

            var value = new TestClassForEncoding { MyString = "SomeValue" };
            Assert.False(options.IsReadOnly);

            string yaml = YamlSerializer.Serialize(value, jti);
            Assert.Equal("{}", yaml);

            // Using YamlTypeInfo will lock YamlSerializerOptions
            Assert.True(options.IsReadOnly);
            Assert.True(jti.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => options.IncludeFields = false);

            // Getting YamlTypeInfo now should return a fresh immutable instance
            YamlTypeInfo<TestClassForEncoding> jti2 = options.GetTypeInfo<TestClassForEncoding>();
            Assert.NotSame(jti, jti2);
            Assert.True(jti2.IsReadOnly);
            Assert.Equal(1, jti2.Properties.Count);
            Assert.Throws<InvalidOperationException>(() => jti2.Properties.Clear());

            // Subsequent requests return the same cached value
            Assert.Same(jti2, options.GetTypeInfo<TestClassForEncoding>());

            // Default contract should produce expected JSON
            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"MyString":"SomeValue"}""", yaml);

            // Default contract should not impact contract of original YamlTypeInfo
            yaml = YamlSerializer.Serialize(value, jti);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        public static void GetTypeInfo_NullInput_ThrowsArgumentNullException()
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentNullException>(() => options.GetTypeInfo(null));
            Assert.Throws<ArgumentNullException>(() => options.TryGetTypeInfo(null, out YamlTypeInfo? _));
        }

        [Fact]
        public static void GetTypeInfo_RecursiveResolver_StackOverflows()
        {
            var resolver = new RecursiveResolver();
            var options = new YamlSerializerOptions { TypeInfoResolver = resolver };

            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(TestClassForEncoding)));
            Assert.True(resolver.IsThresholdReached);
        }

        private class RecursiveResolver : IYamlTypeInfoResolver
        {
            private const int MaxDepth = 10;

            [ThreadStatic]
            private int _isResolverEntered = 0;

            public bool IsThresholdReached { get; private set; }

            public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
            {
                if (_isResolverEntered == MaxDepth)
                {
                    IsThresholdReached = true;
                    return null;
                }

                _isResolverEntered++;
                try
                {
                    return options.GetTypeInfo(type);
                }
                finally
                {
                    _isResolverEntered--;
                }
            }
        }

        [Theory]
        [InlineData(typeof(void))]
        [InlineData(typeof(Dictionary<,>))]
        [InlineData(typeof(List<>))]
        [InlineData(typeof(Nullable<>))]
        [InlineData(typeof(int*))]
        [InlineData(typeof(Span<int>))]
        public static void GetTypeInfo_InvalidInput_ThrowsArgumentException(Type type)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentException>(() => options.GetTypeInfo(type));
            Assert.Throws<ArgumentException>(() => options.TryGetTypeInfo(type, out YamlTypeInfo? _));
        }

        [Fact]
        public static void GetTypeInfo_ResolverWithoutMetadata_ThrowsNotSupportedException()
        {
            var options = new YamlSerializerOptions();
            new YamlContext(options);

            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(BasicCompany)));

            Assert.False(options.TryGetTypeInfo(typeof(BasicCompany), out YamlTypeInfo? typeInfo));
            Assert.Null(typeInfo);
        }

        [Theory]
        [MemberData(nameof(GetTypeInfo_ResultsAreGeneric_Values))]
        [Trait("category", "jsonoutput")]
        public static void GetTypeInfo_ResultsAreGeneric<T>(T value, string expectedYaml)
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            YamlTypeInfo<T> yamlTypeInfo = options.GetTypeInfo<T>();
            string yaml = YamlSerializer.Serialize(value, yamlTypeInfo);
            Assert.Equal(expectedYaml, yaml);
            YamlSerializer.Deserialize(yaml, yamlTypeInfo);
        }

        public static IEnumerable<object[]> GetTypeInfo_ResultsAreGeneric_Values()
        {
            yield return WrapArgs(42, "42");
            yield return WrapArgs("string", "\"string\"");
            yield return WrapArgs(new { Value = 42, String = "str" }, """{"Value":42,"String":"str"}""");
            yield return WrapArgs(new List<int> { 1, 2, 3, 4, 5 }, "[1,2,3,4,5]");
            yield return WrapArgs(new Dictionary<string, int> { ["key"] = 42 }, """{"key":42}""");

            static object[] WrapArgs<T>(T value, string yaml) => new object[] { value, yaml };
        }

        [Fact]
        public static void GetTypeInfoGeneric_MutableOptionsInstance()
        {
            var options = new YamlSerializerOptions();

            // An unset resolver results in NotSupportedException.
            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo<int>());
            Assert.False(options.TryGetTypeInfo<int>(out YamlTypeInfo<int>? typeInfo));
            Assert.Null(typeInfo);

            options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
            YamlTypeInfo<int> typeInfo1 = options.GetTypeInfo<int>();
            Assert.Equal(typeof(int), typeInfo1.Type);
            Assert.False(typeInfo1.IsReadOnly);

            YamlTypeInfo<int> typeInfo2 = options.GetTypeInfo<int>();
            Assert.Equal(typeof(int), typeInfo2.Type);
            Assert.False(typeInfo2.IsReadOnly);

            Assert.NotSame(typeInfo1, typeInfo2);

            Assert.True(options.TryGetTypeInfo<int>(out YamlTypeInfo<int>? typeInfo3));
            Assert.Equal(typeof(int), typeInfo3.Type);
            Assert.False(typeInfo3.IsReadOnly);

            Assert.NotSame(typeInfo1, typeInfo3);

            options.WriteIndented = true; // can mutate without issue
        }

        [Fact]
        public static void GetTypeInfoGeneric_ImmutableOptionsInstance()
        {
            var options = new YamlSerializerOptions();
            YamlSerializer.Serialize(42, options);

            YamlTypeInfo<int> typeInfo = options.GetTypeInfo<int>();
            Assert.Equal(typeof(int), typeInfo.Type);
            Assert.True(typeInfo.IsReadOnly);

            YamlTypeInfo<int> typeInfo2 = options.GetTypeInfo<int>();
            Assert.Same(typeInfo, typeInfo2);

            Assert.True(options.TryGetTypeInfo<int>(out YamlTypeInfo<int>? typeInfo3));
            Assert.Same(typeInfo, typeInfo3);
        }

        [Fact]
        public static void GetTypeInfoGeneric_EquivalentToNonGenericVersion()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            YamlSerializer.Serialize(42, options); // Make options immutable to get caching

            YamlTypeInfo nonGenericTypeInfo = options.GetTypeInfo(typeof(int));
            YamlTypeInfo<int> genericTypeInfo = options.GetTypeInfo<int>();

            Assert.Same(nonGenericTypeInfo, genericTypeInfo);

            Assert.True(options.TryGetTypeInfo(typeof(string), out YamlTypeInfo? nonGenericTypeInfo2));
            Assert.True(options.TryGetTypeInfo<string>(out YamlTypeInfo<string>? genericTypeInfo2));

            Assert.Same(nonGenericTypeInfo2, genericTypeInfo2);
        }

        [Fact]
        public static void GetTypeInfoGeneric_WorksWithVariousTypes()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };

            YamlTypeInfo<string> stringTypeInfo = options.GetTypeInfo<string>();
            Assert.Equal(typeof(string), stringTypeInfo.Type);

            YamlTypeInfo<List<int>> listTypeInfo = options.GetTypeInfo<List<int>>();
            Assert.Equal(typeof(List<int>), listTypeInfo.Type);

            YamlTypeInfo<Dictionary<string, object>> dictTypeInfo = options.GetTypeInfo<Dictionary<string, object>>();
            Assert.Equal(typeof(Dictionary<string, object>), dictTypeInfo.Type);

            YamlTypeInfo<object> objectTypeInfo = options.GetTypeInfo<object>();
            Assert.Equal(typeof(object), objectTypeInfo.Type);
        }

        [Fact]
        public static void GetTypeInfoGeneric_ResolverWithoutMetadata_ThrowsNotSupportedException()
        {
            var options = new YamlSerializerOptions();
            new YamlContext(options);

            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo<BasicCompany>());

            Assert.False(options.TryGetTypeInfo<BasicCompany>(out YamlTypeInfo<BasicCompany>? typeInfo));
            Assert.Null(typeInfo);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void GetTypeInfoGeneric_MutableOptions_CanModifyMetadata()
        {
            var options = new YamlSerializerOptions { TypeInfoResolver = new DefaultYamlTypeInfoResolver() };
            YamlTypeInfo<TestClassForEncoding> jti = options.GetTypeInfo<TestClassForEncoding>();

            Assert.False(jti.IsReadOnly);
            Assert.Equal(1, jti.Properties.Count);
            jti.Properties.Clear();

            var value = new TestClassForEncoding { MyString = "SomeValue" };
            Assert.False(options.IsReadOnly);

            string yaml = YamlSerializer.Serialize(value, jti);
            Assert.Equal("{}", yaml);

            // Using YamlTypeInfo will lock YamlSerializerOptions
            Assert.True(options.IsReadOnly);
            Assert.True(jti.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => options.IncludeFields = false);

            // Getting YamlTypeInfo now should return a fresh immutable instance
            YamlTypeInfo<TestClassForEncoding> jti2 = options.GetTypeInfo<TestClassForEncoding>();
            Assert.NotSame(jti, jti2);
            Assert.True(jti2.IsReadOnly);
            Assert.Equal(1, jti2.Properties.Count);
            Assert.Throws<InvalidOperationException>(() => jti2.Properties.Clear());

            // Subsequent requests return the same cached value
            Assert.Same(jti2, options.GetTypeInfo<TestClassForEncoding>());

            // Default contract should produce expected JSON
            yaml = YamlSerializer.Serialize(value, options);
            Assert.Equal("""{"MyString":"SomeValue"}""", yaml);

            // Default contract should not impact contract of original YamlTypeInfo
            yaml = YamlSerializer.Serialize(value, jti);
            Assert.Equal("{}", yaml);
        }

        [Fact]
        public static void AllowDuplicateProperties_RespectsSetting()
        {
            var options = new YamlSerializerOptions { AllowDuplicateProperties = false };
            string yaml = "{\"a\":1,\"a\":2}";
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options));

            options = new YamlSerializerOptions { AllowDuplicateProperties = true };
            var result = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options);
            Assert.Single(result);
            Assert.Equal(2, result["a"]);
        }

        [Fact]
        public static void PreferredObjectCreationHandling_CanBeSet()
        {
            var options = new YamlSerializerOptions();
            Assert.Equal(YamlObjectCreationHandling.Replace, options.PreferredObjectCreationHandling);
            
            options.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
            Assert.Equal(YamlObjectCreationHandling.Populate, options.PreferredObjectCreationHandling);
            
            options.TypeInfoResolver = new DefaultYamlTypeInfoResolver();
            options.MakeReadOnly();
            Assert.Throws<InvalidOperationException>(() => options.PreferredObjectCreationHandling = YamlObjectCreationHandling.Replace);
        }

        [Fact]
        public static void PreferredObjectCreationHandling_InvalidValue_ThrowsArgumentOutOfRangeException()
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.PreferredObjectCreationHandling = (YamlObjectCreationHandling)999);
        }

        [Fact]
        public static void TypeInfoResolverChain_CanAddMultipleResolvers()
        {
            var options = new YamlSerializerOptions();
            var resolver1 = new DefaultYamlTypeInfoResolver();
            var resolver2 = new DefaultYamlTypeInfoResolver();
            
            options.TypeInfoResolverChain.Add(resolver1);
            options.TypeInfoResolverChain.Add(resolver2);
            
            Assert.Equal(2, options.TypeInfoResolverChain.Count);
            Assert.Same(resolver1, options.TypeInfoResolverChain[0]);
            Assert.Same(resolver2, options.TypeInfoResolverChain[1]);
        }

        [Fact]
        public static void TypeInfoResolverChain_ToString_ReturnsChainDescription()
        {
            var options = new YamlSerializerOptions();
            options.TypeInfoResolverChain.Add(new DefaultYamlTypeInfoResolver());
            
            string result = options.TypeInfoResolver.ToString();
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public static void YamlSerializerOptions_WriteIndented()
        {
            var options = new YamlSerializerOptions { WriteIndented = true };
            Assert.True(options.WriteIndented);
            
            string yaml = YamlSerializer.Serialize(new { name = "test" }, options);
            Assert.Contains("\n", yaml);
        }

        [Fact]
        public static void YamlSerializerOptions_DefaultBufferSize()
        {
            var options = new YamlSerializerOptions();
            Assert.Equal(16384, options.DefaultBufferSize);
            
            options.DefaultBufferSize = 8192;
            Assert.Equal(8192, options.DefaultBufferSize);
        }

        private sealed class SampleClassifierForCopyTest : YamlTypeClassifierFactory
        {
            public override bool CanClassify(YamlTypeClassifierContext context) => false;
            public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                throw new NotSupportedException();
        }
    }
}
