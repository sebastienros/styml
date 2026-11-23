// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public static partial class Utf8YamlReaderTests
    {
        [Fact]
        public static void TestTextEqualsBasic()
        {
            bool foundId = false;
            bool foundTransports = false;
            bool foundValue = false;
            bool foundArrayValue = false;

            var yaml = new Utf8YamlReader("{\"conne\\u0063tionId\":\"123\",\"availableTransports\":[\"My name is \\\"Ahson\\\"\"]}"u8, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    if (yaml.ValueTextEquals("connectionId"u8) && yaml.ValueTextEquals("connectionId".AsSpan()))
                    {
                        foundId = true;
                    }
                    else if (yaml.ValueTextEquals("availableTransports"u8) && yaml.ValueTextEquals("availableTransports".AsSpan()))
                    {
                        foundTransports = true;
                    }
                }
                else if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals("123"u8) && yaml.ValueTextEquals("123".AsSpan()))
                    {
                        foundValue = true;
                    }
                    else if (yaml.ValueTextEquals("My name is \"Ahson\""u8) && yaml.ValueTextEquals("My name is \"Ahson\"".AsSpan()))
                    {
                        foundArrayValue = true;
                    }
                }
            }

            Assert.True(foundId);
            Assert.True(foundTransports);
            Assert.True(foundValue);
            Assert.True(foundArrayValue);
        }

        [Theory]
        [InlineData("{\"name\": \"John\"}", false)]
        [InlineData("{\"name\": \"\"}", true)]
        [InlineData("{\"name\": \"Joh\\u006e\"}", false)]
        public static void TextEqualDefault(string yamlString, bool expectedFound)
        {
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(ReadOnlySpan<byte>)));
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(ReadOnlySpan<char>)));
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(string)));
                    break;
                }
            }

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(ReadOnlySpan<byte>)));
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(ReadOnlySpan<char>)));
                    Assert.Equal(expectedFound, yaml.ValueTextEquals(default(string)));
                    break;
                }
            }
        }

        [Theory]
        [InlineData("{\"name\": 1234}", "name", true)]
        [InlineData("{\"name\": 1234}", "namee", false)]
        [InlineData("{\"name\": 1234}", "na\\u006de", false)]
        [InlineData("{\"name\": 1234}", "", false)]
        [InlineData("{\"\": 1234}", "name", false)]
        [InlineData("{\"\": 1234}", "na\\u006de", false)]
        [InlineData("{\"\": 1234}", "", true)]
        [InlineData("{\"na\\u006de\": 1234}", "name", true)]
        [InlineData("{\"na\\u006de\": 1234}", "namee", false)]
        [InlineData("{\"na\\u006de\": 1234}", "na\\u006de", false)]
        [InlineData("{\"na\\u006de\": 1234}", "", false)]
        public static void TestTextEquals(string yamlString, string lookUpString, bool expectedFound)
        {
            byte[] lookup = Encoding.UTF8.GetBytes(lookUpString);
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);
            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    if (yaml.ValueTextEquals(lookup) &&
                        yaml.ValueTextEquals(lookUpString) &&
                        yaml.ValueTextEquals(lookUpString.AsSpan()))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.Equal(expectedFound, found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.PropertyName)
                {
                    if (yaml.ValueTextEquals(lookup) &&
                        yaml.ValueTextEquals(lookUpString) &&
                        yaml.ValueTextEquals(lookUpString.AsSpan()))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.Equal(expectedFound, found);
        }

        [Theory]
        [InlineData("{\"name\": \"John\"}", "John", true)]
        [InlineData("{\"name\": \"John\"}", "Johna", false)]
        [InlineData("{\"name\": \"John\"}", "Joh\\u006e", false)]
        [InlineData("{\"name\": \"John\"}", "", false)]
        [InlineData("{\"name\": \"\"}", "John", false)]
        [InlineData("{\"name\": \"\"}", "Joh\\u006e", false)]
        [InlineData("{\"name\": \"\"}", "", true)]
        [InlineData("{\"name\": \"Joh\\u006e\"}", "John", true)]
        [InlineData("{\"name\": \"Joh\\u006e\"}", "Johna", false)]
        [InlineData("{\"name\": \"Joh\\u006e\"}", "Joh\\u006e", false)]
        [InlineData("{\"name\": \"Joh\\u006e\"}", "", false)]
        public static void TestTextEqualsValue(string yamlString, string lookUpString, bool expectedFound)
        {
            byte[] lookup = Encoding.UTF8.GetBytes(lookUpString);
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);
            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(lookup) &&
                        yaml.ValueTextEquals(lookUpString) &&
                        yaml.ValueTextEquals(lookUpString.AsSpan()))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.Equal(expectedFound, found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(lookup) &&
                        yaml.ValueTextEquals(lookUpString) &&
                        yaml.ValueTextEquals(lookUpString.AsSpan()))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.Equal(expectedFound, found);
        }

        [Fact]
        public static void TestTextEqualsLargeMatch()
        {
            var yamlChars = new char[320];  // Some value larger than 256 (stack threshold)
            yamlChars.AsSpan().Fill('a');
            byte[] lookup = Encoding.UTF8.GetBytes(yamlChars);

            ReadOnlySpan<char> escapedA = new char[6] { '\\', 'u', '0', '0', '6', '1' };

            ReadOnlySpan<byte> lookupSpan = lookup.AsSpan(0, lookup.Length - escapedA.Length + 1);   // remove extra characters that were replaced by escaped bytes
            Span<char> lookupChars = new char[yamlChars.Length];
            yamlChars.CopyTo(lookupChars);
            lookupChars = lookupChars.Slice(0, lookupChars.Length - escapedA.Length + 1);

            // Replacing 'a' with '\u0061', so a net change of 5.
            // escapedA.Length - 1 = 6 - 1 = 5
            for (int i = 0; i < yamlChars.Length - escapedA.Length + 1; i++)
            {
                yamlChars.AsSpan().Fill('a');
                escapedA.CopyTo(yamlChars.AsSpan(i));
                string yamlString = "\"" + new string(yamlChars) + "\"";
                byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

                bool found = false;

                var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
                while (yaml.Read())
                {
                    if (yaml.TokenType == YamlTokenType.String)
                    {
                        if (yaml.ValueTextEquals(lookupSpan) &&
                            yaml.ValueTextEquals(lookupChars) &&
                            yaml.ValueTextEquals(new string(lookupChars.ToArray())))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                Assert.True(found, $"Yaml String: {yamlString}");

                ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
                found = false;

                yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
                while (yaml.Read())
                {
                    if (yaml.TokenType == YamlTokenType.String)
                    {
                        if (yaml.ValueTextEquals(lookupSpan) &&
                            yaml.ValueTextEquals(lookupChars) &&
                            yaml.ValueTextEquals(new string(lookupChars.ToArray())))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                Assert.True(found, $"Yaml String: {yamlString}  | Look up: {Encoding.UTF8.GetString(lookupSpan.ToArray())}");
            }
        }

        [Fact]
        public static void TestTextEqualsLargeMismatch()
        {
            var yamlChars = new char[320];  // Some value larger than 256 (stack threshold)
            yamlChars.AsSpan().Fill('a');
            ReadOnlySpan<char> escapedA = new char[6] { '\\', 'u', '0', '0', '6', '1' };

            byte[] originalLookup = Encoding.UTF8.GetBytes(yamlChars);

            char[] originalLookupChars = new char[yamlChars.Length];
            Array.Copy(yamlChars, originalLookupChars, yamlChars.Length);

            for (int i = 1; i < yamlChars.Length - 6; i++)
            {
                yamlChars.AsSpan().Fill('a');
                escapedA.CopyTo(yamlChars.AsSpan(i));
                string yamlString = "\"" + new string(yamlChars) + "\"";
                byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

                for (int j = 0; j < 3; j++)
                {
                    Span<byte> lookup = new byte[originalLookup.Length];
                    originalLookup.CopyTo(lookup);
                    lookup = lookup.Slice(0, lookup.Length - escapedA.Length + 1);    // remove extra characters that were replaced by escaped bytes

                    Span<char> lookupChars = new char[originalLookupChars.Length];
                    originalLookupChars.CopyTo(lookupChars);
                    lookupChars = lookupChars.Slice(0, lookupChars.Length - escapedA.Length + 1);    // remove extra characters that were replaced by escaped bytes

                    switch (j)
                    {
                        case 0:
                            lookup[i] = (byte)'b';
                            lookupChars[i] = 'b';
                            break;
                        case 1:
                            lookup[i + 1] = (byte)'b';
                            lookupChars[i + 1] = 'b';
                            break;
                        case 2:
                            lookup[i - 1] = (byte)'b';
                            lookupChars[i - 1] = 'b';
                            break;
                    }

                    bool found = false;

                    var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
                    while (yaml.Read())
                    {
                        if (yaml.TokenType == YamlTokenType.String)
                        {
                            if (yaml.ValueTextEquals(lookup) ||
                                yaml.ValueTextEquals(lookupChars) ||
                                yaml.ValueTextEquals(new string(lookupChars.ToArray())))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    Assert.False(found, $"Yaml String: {yamlString}");

                    ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
                    found = false;

                    yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
                    while (yaml.Read())
                    {
                        if (yaml.TokenType == YamlTokenType.String)
                        {
                            if (yaml.ValueTextEquals(lookup) ||
                                yaml.ValueTextEquals(lookupChars) ||
                                yaml.ValueTextEquals(new string(lookupChars.ToArray())))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    Assert.False(found);
                }
            }
        }

        [Theory]
        [InlineData("\"\\u0061\\u0061\"")]
        [InlineData("\"aaaaaaaaaaaa\"")]
        public static void TestTextEqualsTooSmallToMatch(string yamlString)
        {
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(new byte[] { (byte)'a' }) ||
                        yaml.ValueTextEquals(new char[] { 'a' }) ||
                        yaml.ValueTextEquals("a"))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(new byte[] { (byte)'a' }) ||
                        yaml.ValueTextEquals(new char[] { 'a' }) ||
                        yaml.ValueTextEquals("a"))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);
        }

        [Theory]
        [InlineData("\"\\u0061\\u0061\"")]
        [InlineData("\"aaaaaaaaaaaa\"")]
        public static void TestTextEqualsTooLargeToMatch(string yamlString)
        {
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            var lookupString = new string('a', 13);

            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.GetSequence(utf8Data, 1);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);
        }

        [Theory]
        [InlineData("\"aaabbb\"", "aaaaaa")]
        [InlineData("\"bbbaaa\"", "aaaaaa")]
        public static void TextMismatchSameLength(string yamlString, string lookupString)
        {
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8Data);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);
        }

        [Fact]
        public static void TextEqualsEscapedCharAtTheLastSegment()
        {
            string yamlString = "\"aaaaaa\\u0061\"";
            string lookupString = "aaaaaaa";
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.True(found);

            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8Data);
            found = false;

            yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(Encoding.UTF8.GetBytes(lookupString)) ||
                        yaml.ValueTextEquals(lookupString.AsSpan()) ||
                        yaml.ValueTextEquals(lookupString))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.True(found);
        }

        [Fact]
        public static void TestTextEqualsMismatchMultiSegment()
        {
            byte[] utf8Data = "\"Hi, \\\"Ahson\\\"!\""u8.ToArray();
            bool found = false;

            // Segment 1: "Hi, \"A
            // Segment 2: hson\"!"
            ReadOnlySequence<byte> sequence = YamlTestHelper.CreateSegments(utf8Data);

            var yaml = new Utf8YamlReader(sequence, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals("Hello, \"Ahson\""u8) ||
                        yaml.ValueTextEquals("Hello, \"Ahson\"".AsSpan()) ||
                        yaml.ValueTextEquals("Hello, \"Ahson\""))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);
        }

        [Theory]
        [InlineData("\"hello\"", new char[1] { (char)0xDC01 })]    // low surrogate - invalid
        [InlineData("\"hello\"", new char[1] { (char)0xD801 })]    // high surrogate - missing pair
        public static void InvalidUTF16Search(string yamlString, char[] lookup)
        {
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);
            bool found = false;

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            while (yaml.Read())
            {
                if (yaml.TokenType == YamlTokenType.String)
                {
                    if (yaml.ValueTextEquals(lookup))
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.False(found);
        }

        [Fact]
        public static void LargeLookupUTF16()
        {
            string yamlString = "\"hello\"";
            string lookup = new string('a', 1_000);
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.String, yaml.TokenType);
            Assert.False(yaml.ValueTextEquals(lookup.AsSpan()));
        }

        [Fact]
        public static void LargeLookupUTF8()
        {
            string yamlString = "\"hello\"";
            byte[] lookup = new byte[1_000];
            lookup.AsSpan().Fill((byte)'a');
            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.String, yaml.TokenType);
            Assert.False(yaml.ValueTextEquals(lookup));
        }

        // NOTE: LookupOverflow test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
        [OuterLoop]
        public static void LookupOverflow()
        {
            char[] yamlString = new char[800_000_002];

            yamlString.AsSpan().Fill('a');
            yamlString[0] = '"';
            yamlString[yamlString.Length - 1] = '"';

            byte[] utf8Data = Encoding.UTF8.GetBytes(yamlString);

            var yaml = new Utf8YamlReader(utf8Data, isFinalBlock: true, state: default);
            Assert.True(yaml.Read());
            Assert.Equal(YamlTokenType.String, yaml.TokenType);

            try
            {
                yaml.ValueTextEquals(yamlString.AsSpan(1, yamlString.Length - 2));
                Assert.Fail($"Expected OverflowException was not thrown when calling ValueTextEquals with large lookup string");
            }
            catch (OverflowException)
            { }
        }

    }
}
