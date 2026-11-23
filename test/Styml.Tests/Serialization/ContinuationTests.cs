// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ContinuationTests
    {
        private static readonly (Func<string, string>, Func<string, int>, int)[] s_payloadTweaks = new (Func<string, string>, Func<string, int>, int)[]
        {
            (payload => payload, payload => -1, 0),
            (payload => payload.Replace("null", "nullX"), payload => payload.IndexOf("nullX"), "null".Length),
            (payload => payload.Replace("true", "trueX"), payload => payload.IndexOf("trueX"), "true".Length),
            (payload => payload.Replace("false", "falseX"), payload => payload.IndexOf("falseX"), "false".Length),
            (payload => payload.Replace("E+17", "E+-17"), payload => payload.IndexOf("E+-17"), "E+".Length)
        };

        private static IEnumerable<(ITestObject, INestedObject)> TestObjects
            => new (ITestObject, INestedObject)[]
            {
                (new TestClass<NestedClass>(), new NestedClass()),
                (new TestClass<NestedValueType>(), new NestedValueType()),
                (new TestValueType<NestedClass>(), new NestedClass()),
                (new TestValueType<NestedValueType>(), new NestedValueType()),
                (new TestClass<NestedClassWithParamCtor>(), new NestedClassWithParamCtor(null)),
                (new DictionaryTestClass<NestedClass>(), new NestedClass()),
            };

        private static IEnumerable<bool> WriteIndented
            => new[] { true, false };

        public static IEnumerable<object[]> TestData(bool enumeratePayloadTweaks)
        {
            // The serialized json gets padded with leading ' ' chars. The length of the
            // incrementing paddings, leads to continuations at every position of the payload.
            // The complete strings (padding + payload) are then passed to the test method.

            // <------min-padding------>[{--payload--}]               min-padding = buffer - payload + 1
            // <-----------2^n byte buffer----------->
            // <-------------max-padding------------>[{--payload--}]  max-padding = buffer - 1

            foreach ((ITestObject TestObject, INestedObject Nested) in TestObjects.Take(enumeratePayloadTweaks ? 1 : TestObjects.Count()))
            {
                Type testObjectType = TestObject.GetType();
                TestObject.Initialize(Nested);

                foreach (bool writeIndented in WriteIndented)
                {
                    string payload = YamlSerializer.Serialize(TestObject, testObjectType, new YamlSerializerOptions { WriteIndented = writeIndented });

                    foreach ((Func<string, string> Tweak, Func<string, int> Position, int Offset) tweak in enumeratePayloadTweaks ? s_payloadTweaks.Skip(1) : s_payloadTweaks.Take(1))
                    {
                        string tweaked = tweak.Tweak(payload);

                        // Wrap the payload inside an array to have something to read before/after.
                        tweaked = '[' + tweaked + ']';
                        Type arrayType = Type.GetType(testObjectType.FullName + "[]");

                        (int Line, int Col) failurePosition = GetExpectedFailure(tweaked, tweak.Position(tweaked), tweak.Offset);

                        // Determine the DefaultBufferSize that is required to contain the complete json.
                        int bufferSize = 16;
                        while (tweaked.Length > bufferSize)
                        {
                            bufferSize *= 2;
                        }
                        int minPaddingLength = bufferSize - tweaked.Length + 1;
                        int maxPaddingLength = bufferSize - 1;

                        foreach (int length in Enumerable.Range(minPaddingLength, maxPaddingLength - minPaddingLength + 1))
                        {
                            (int Line, int Col) paddedFailurePosition = failurePosition;
                            if (failurePosition != default && failurePosition.Line == 0)
                                paddedFailurePosition = (failurePosition.Line, failurePosition.Col + length);

                            yield return new object[]
                            {
                                new string(' ', length) + tweaked,
                                bufferSize,
                                arrayType,
                                paddedFailurePosition
                            };
                        }
                    }
                }
            }

            static (int Line, int Col) GetExpectedFailure(string payload, int position, int offset)
            {
                if (position < 0)
                    return default;

                position += offset;
                ReadOnlySpan<byte> utf8 = Encoding.UTF8.GetBytes(payload);
                utf8 = utf8.Slice(0, position);
                int positionInLine = position;
                int lastNewLine;
                int newLineCount = 0;
                while ((lastNewLine = utf8.LastIndexOf((byte)'\n')) >= 0)
                {
                    if (newLineCount == 0)
                        positionInLine -= lastNewLine + 1;
                    newLineCount++;
                    utf8 = utf8.Slice(0, lastNewLine);
                }

                return (newLineCount, positionInLine);
            }
        }

        [Theory]
        [MemberData(nameof(TestData), /* enumeratePayloadTweaks: */ false)]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/42677", platforms: TestPlatforms.Windows, runtimes: TestRuntimes.Mono)]
        [Trait("category", "jsonoutput")]
        public static async Task ShouldWorkAtAnyPosition_Stream(
            string yaml,
            int bufferSize,
            Type type,
            (int Line, int Column) expectedFailure)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            {
                var readOptions = new YamlSerializerOptions
                {
                    DefaultBufferSize = bufferSize,
                };

                var array = (ITestObject[])await YamlSerializer.DeserializeAsync(stream, type, readOptions);

                Assert.NotNull(array);
                Assert.Equal(1, array.Length);
                array[0].Verify();
            }
            Assert.Equal(default, expectedFailure);
        }

        [Theory]
        [MemberData(nameof(TestData), /* enumeratePayloadTweaks: */ false)]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/42677", platforms: TestPlatforms.Windows, runtimes: TestRuntimes.Mono)]
        [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
        [Trait("category", "jsonoutput")]
        public static void ShouldWorkAtAnyPosition_Sequence(
            string yaml,
            int bufferSize,
            Type type,
            (int Line, int Column) expectedFailure)
        {
            var readOptions = new YamlSerializerOptions();

            var chunk = new Chunk(yaml, bufferSize);
            var sequence = new ReadOnlySequence<byte>(chunk, 0, chunk.Next, chunk.Next.Memory.Length);

            var reader = new Utf8YamlReader(sequence);
            var array = (ITestObject[])YamlSerializer.Deserialize(ref reader, type, readOptions);

            Assert.NotNull(array);
            Assert.Equal(1, array.Length);
            array[0].Verify();
            Assert.Equal(default, expectedFailure);
        }

        [Fact]
        public static async Task BomHandlingRegressionTest()
        {
            byte[] utf8Bom = Encoding.UTF8.GetPreamble();
            byte[] yaml = """{ "Value" :                    "Hello" }"""u8.ToArray();

            using var stream = new MemoryStream();
            stream.Write(utf8Bom, 0, utf8Bom.Length);
            stream.Write(yaml, 0, yaml.Length);
            stream.Position = 0;

            var options = new YamlSerializerOptions
            {
                DefaultBufferSize = 32
            };

            Test result = await YamlSerializer.DeserializeAsync<Test>(stream, options);
            Assert.Equal("Hello", result.Value);
        }

        private class Test
        {
            public string Value { get; set; }
        }

        private class Chunk : ReadOnlySequenceSegment<byte>
        {
            public Chunk(string yaml, int firstSegmentLength)
            {
                Memory<byte> bytes = Encoding.UTF8.GetBytes(yaml);
                Memory = bytes.Slice(0, firstSegmentLength);
                RunningIndex = 0;
                Next = new Chunk()
                {
                    Memory = bytes.Slice(firstSegmentLength),
                    RunningIndex = firstSegmentLength,
                    Next = null,
                };
            }
            private Chunk()
            { }
        }

        private interface ITestObject
        {
            void Initialize(INestedObject nested);
            void Verify();
        }

        private interface INestedObject
        {
            void Initialize();
            void Verify();
        }

        private class TestClass<TNested> : ITestObject where TNested : INestedObject
        {
            public string A { get; set; }
            public string? B { get; set; }
            public int C { get; set; }
            public int? D { get; set; }
            public float E { get; set; }
            public bool G { get; set; }
            public int[] I { get; set; }
            public TNested J { get; set; }

            void ITestObject.Initialize(INestedObject nested)
            {
                A = "Hello";
                B = null;
                C = 42;
                D = null;
                E = 3.14e+17f;
                G = true;
                I = new int[] { 42, 17 };
                nested.Initialize();
                J = (TNested)nested;
            }

            void ITestObject.Verify()
            {
                Assert.Equal("Hello", A);
                Assert.Null(B);
                Assert.Equal(42, C);
                Assert.Null(D);
                Assert.Equal(3.14e17f, E);
                Assert.True(G);
                Assert.NotNull(I);
                Assert.True(I.SequenceEqual(new[] { 42, 17 }));
                Assert.NotNull(J);
                J.Verify();
            }
        }

        private class TestValueType<TNested> : ITestObject where TNested : INestedObject
        {
            public string A { get; set; }
            public string? B { get; set; }
            public int C { get; set; }
            public int? D { get; set; }
            public float E { get; set; }
            public bool G { get; set; }
            public int[] I { get; set; }
            public TNested J { get; set; }

            void ITestObject.Initialize(INestedObject nested)
            {
                A = "Hello";
                B = null;
                C = 42;
                D = null;
                E = 3.14e+17f;
                G = true;
                I = new int[] { 42, 17 };
                nested.Initialize();
                J = (TNested)nested;
            }

            void ITestObject.Verify()
            {
                Assert.Equal("Hello", A);
                Assert.Null(B);
                Assert.Equal(42, C);
                Assert.Null(D);
                Assert.Equal(3.14e17f, E);
                Assert.True(G);
                Assert.NotNull(I);
                Assert.True(I.SequenceEqual(new[] { 42, 17 }));
                Assert.NotNull(J);
                J.Verify();
            }
        }

        private class NestedClass : INestedObject
        {
            public string? A { get; set; }
            public int B { get; set; }

            void INestedObject.Initialize()
            {
                A = null;
                B = 7;
            }

            void INestedObject.Verify()
            {
                Assert.Null(A);
                Assert.Equal(7, B);
            }
        }

        private struct NestedValueType : INestedObject
        {
            public string? A { get; set; }
            public int B { get; set; }

            void INestedObject.Initialize()
            {
                A = null;
                B = 7;
            }

            void INestedObject.Verify()
            {
                Assert.Null(A);
                Assert.Equal(7, B);
            }
        }

        private class NestedClassWithParamCtor : NestedClass
        {
            public NestedClassWithParamCtor(string? a)
                => A = a;
        }

        private class DictionaryTestClass<TNested> : ITestObject where TNested : INestedObject
        {
            public Dictionary<string, TNested> A { get; set; }

            void ITestObject.Initialize(INestedObject nested)
            {
                nested.Initialize();
                A = new Dictionary<string, TNested>() { { "a", (TNested)nested }, { "b", (TNested)nested } };
            }

            void ITestObject.Verify()
            {
                Assert.NotNull(A);
                Assert.Collection(A,
                    kv =>
                    {
                        Assert.Equal("a", kv.Key);
                        kv.Value.Verify();
                    },
                    kv =>
                    {
                        Assert.Equal("b", kv.Key);
                        kv.Value.Verify();
                    });
            }
        }
    }
}
