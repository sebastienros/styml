// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class ConstructorTests
    {
        [Theory]
        [MemberData(nameof(ReadSimpleObjectAsync_TestData))]
        public async Task ReadSimpleObjectAsync(Type type, string yaml)
        {
            if (StreamingSerializer is null)
            {
                return;
            }

            using Utf8MemoryStream stream = new(yaml);
            YamlSerializerOptions options = new YamlSerializerOptions
            { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                DefaultBufferSize = 1
            };

            object obj = await StreamingSerializer.DeserializeWrapper(stream, type, options);
            ((ITestClass)obj).Verify();
        }

        public static IEnumerable<object[]> ReadSimpleObjectAsync_TestData()
        {
            // Simple models can be deserialized.
            yield return new object[] { typeof(Parameterized_IndexViewModel_Immutable), Parameterized_IndexViewModel_Immutable.s_yaml };
            // Complex models can be deserialized.
            yield return new object[] { typeof(ObjWCtorMixedParams), ObjWCtorMixedParams.s_yaml };
            yield return new object[] { typeof(Parameterized_Class_With_ComplexTuple), Parameterized_Class_With_ComplexTuple.s_yaml };
            // JSON that doesn't bind to ctor args are matched with properties or ignored (as appropriate).
            yield return new object[] { typeof(Person_Class), Person_Class.s_yaml };
            yield return new object[] { typeof(Person_Struct), Person_Struct.s_yaml };
            // JSON that doesn't bind to ctor args or properties are sent to ext data if available.
            yield return new object[] { typeof(Parameterized_Person), Parameterized_Person.s_yaml };
            yield return new object[] { typeof(Parameterized_Person_ObjExtData), Parameterized_Person_ObjExtData.s_yaml };
            // Up to 64 ctor args are supported.
            yield return new object[] { typeof(Class_With_Ctor_With_64_Params), Class_With_Ctor_With_64_Params.Yaml };
            // Arg deserialization honors attributes on matching property.
            yield return new object[] { typeof(Point_MembersHave_YamlPropertyName), Point_MembersHave_YamlPropertyName.s_yaml };
            yield return new object[] { typeof(Point_MembersHave_YamlConverter), Point_MembersHave_YamlConverter.s_yaml };
            yield return new object[] { typeof(Point_MembersHave_YamlIgnore), Point_MembersHave_YamlIgnore.s_yaml };
            yield return new object[] { typeof(Point_MembersHave_YamlInclude), Point_MembersHave_YamlInclude.s_yaml };
            yield return new object[] { typeof(ClassWithFiveArgs_MembersHave_YamlNumberHandlingAttributes), ClassWithFiveArgs_MembersHave_YamlNumberHandlingAttributes.s_yaml };
            // Complex JSON as last argument works
            yield return new object[] { typeof(Point_With_Array), Point_With_Array.s_yaml };
            yield return new object[] { typeof(Point_With_Dictionary), Point_With_Dictionary.s_yaml };
            yield return new object[] { typeof(Point_With_Object), Point_With_Object.s_yaml };
        }

        [Theory]
        [InlineData(typeof(Class_With_Ctor_With_65_Params))]
        [InlineData(typeof(Struct_With_Ctor_With_65_Params))]
        public async Task Can_DeserializeAsync_ObjectWith_Ctor_With_65_Params(Type type)
        {
            if (StreamingSerializer is null)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            for (int i = 0; i < 64; i++)
            {
                sb.Append($@"""Int{i}"":{i},");
            }
            sb.Append($@"""Int64"":64");
            sb.Append("}");

            string yaml = sb.ToString();

            using (MemoryStream stream = new Utf8MemoryStream(yaml))
            {
                YamlSerializerOptions options = new YamlSerializerOptions
                {
                    DefaultBufferSize = 1
                };

                object? value = await StreamingSerializer.DeserializeWrapper(stream, type, options);
                Assert.NotNull(value);
            }

            using (MemoryStream stream = new MemoryStream("{}"u8.ToArray()))
            {
                YamlSerializerOptions options = new YamlSerializerOptions
                {
                    DefaultBufferSize = 1
                };

                object? value = await StreamingSerializer.DeserializeWrapper(stream, type, options);
                Assert.NotNull(value);
            }
        }

        [Fact]
        public async Task ExerciseStreamCodePaths()
        {
            if (StreamingSerializer is null)
            {
                return;
            }

            static string GetPropertyName(int index) =>
                new string(new char[] { Convert.ToChar(index + 65), 'V', 'a', 'l', 'u', 'e' });

            static byte[] GeneratePayload(int i, string value)
            {
                string whiteSpace = new string(' ', 16);

                StringBuilder sb;

                string prefix = "";

                sb = new StringBuilder();
                sb.Append("{");

                for (int j = 0; j < i; j++)
                {
                    sb.Append(prefix);
                    sb.Append($@"""{GetPropertyName(j)}"":""{value}""");
                    prefix = ",";
                }

                sb.Append(prefix);
                sb.Append($@"{whiteSpace}""{GetPropertyName(i)}"":{whiteSpace}""{value}""");
                prefix = ",";

                for (int j = 0; j < 10; j++)
                {
                    sb.Append(prefix);
                    string keyPair = $@"""rand"":[""{value}""]";
                    sb.Append($@"""Value{j}"":{{{keyPair},{keyPair},{keyPair}}}");
                }

                for (int j = i + 1; j < 20; j++)
                {
                    sb.Append(prefix);
                    sb.Append($@"""{GetPropertyName(j)}"":""{value}""");
                }

                sb.Append("}");

                return Encoding.UTF8.GetBytes(sb.ToString());
            }

            const string value = "ul4Oolt4VgbNm5Y1qPX911wxhyHFEQmmWBcIBR6BfUaNuIn3YOJ8vqtqz2WAh924rEILMzlh6JUhQDcmH00SI6Kv4iGTHQfGXxqWul4Oolt4VgbNm5Y1qPX911wxhyHFEQmmWBcIBR6";

            for (int i = 0; i < 20; i++)
            {
                using (MemoryStream stream = new MemoryStream(GeneratePayload(i, value)))
                {
                    YamlSerializerOptions options = new YamlSerializerOptions
                    { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                        DefaultBufferSize = 1
                    };

                    ClassWithStrings obj = await StreamingSerializer.DeserializeWrapper<ClassWithStrings>(stream, options);
                    obj.Verify(value);
                }
            }
        }

        public class ClassWithStrings
        {
            // Ctor args.

            // Ignored.

            [YamlIgnore]
            public string AValue { get; }
            [YamlIgnore]
            public string EValue { get; }
            [YamlIgnore]
            public string IValue { get; }
            [YamlIgnore]
            public string MValue { get; }
            [YamlIgnore]
            public string QValue { get; }

            // Populated.

            public string CValue { get; }
            public string GValue { get; }
            public string KValue { get; }
            public string OValue { get; }
            public string SValue { get; }

            // Properties.

            // Ignored - no setter.

            public string BValue { get; }
            public string FValue { get; }
            public string JValue { get; }
            public string NValue { get; }
            public string RValue { get; }

            // Populated.

            public string DValue { get; set; }
            public string HValue { get; set; }
            public string LValue { get; set; }
            public string PValue { get; set; }
            public string TValue { get; set; }

            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { get; set; }

            public ClassWithStrings(
                string aValue,
                string cValue,
                string eValue,
                string gValue,
                string iValue,
                string kValue,
                string mValue,
                string oValue,
                string qValue,
                string sValue)
            {
                AValue = aValue;
                CValue = cValue;
                EValue = eValue;
                GValue = gValue;
                IValue = iValue;
                KValue = kValue;
                MValue = mValue;
                OValue = oValue;
                QValue = qValue;
                SValue = sValue;
            }

            public void Verify(string expectedStr)
            {
                // Ctor args.

                // Ignored
                Assert.Null(AValue);
                Assert.Null(EValue);
                Assert.Null(IValue);
                Assert.Null(MValue);
                Assert.Null(QValue);

                Assert.Equal(expectedStr, CValue);
                Assert.Equal(expectedStr, GValue);
                Assert.Equal(expectedStr, KValue);
                Assert.Equal(expectedStr, OValue);
                Assert.Equal(expectedStr, SValue);

                // Getter only members - skipped.
                Assert.Null(BValue);
                Assert.Null(FValue);
                Assert.Null(JValue);
                Assert.Null(NValue);
                Assert.Null(RValue);

                // Members with setters
                Assert.Equal(expectedStr, DValue);
                Assert.Equal(expectedStr, HValue);
                Assert.Equal(expectedStr, LValue);
                Assert.Equal(expectedStr, PValue);
                Assert.Equal(expectedStr, TValue);

                Assert.Equal(10, ExtensionData.Count);

                foreach (YamlElement value in ExtensionData.Values)
                {
                    string keyPair = $@"""rand"":[""{expectedStr}""]";
                    Assert.Equal($@"{{{keyPair},{keyPair},{keyPair}}}", value.GetRawText());
                }
            }
        }
    }
}
