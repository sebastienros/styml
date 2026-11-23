// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// The rules of <see cref="YamlReaderOptions.AcceptLenientScalars"/>, numbered as the test names are:
    /// quoted, block and tagged scalars into a boolean or a number (rule 1), null into a non-nullable
    /// value type (rule 2), padded text (rule 6), and what the documentation says about converters and the writer.
    /// The cells against YamlDotNet and SharpYaml are in Styml.Parity.Tests (LenientScalarsParityTests).
    /// </summary>
    public class LenientScalarsRulingsTests
    {
        private static readonly YamlSerializerOptions s_lenient = new() { AcceptLenientScalars = true };

        public class Holder<T>
        {
            public T A { get; set; } = default!;
        }

        public record Positional(int A, bool B);

        public record PositionalNullable(int? A, double B);

        public struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public enum Color { Red = 1, Green }

        private static object? Read(string yaml, Type type, bool lenient) =>
            YamlSerializer.Deserialize(yaml, type, lenient ? s_lenient : null);

        // ------------------------------------------------------------------------------------------------------------
        // Rule 1: quoted, block and tagged scalars into a boolean or a number

        [Theory]
        [InlineData("'12'", 12)]
        [InlineData("\"12\"", 12)]
        [InlineData("!!str 12", 12)]
        [InlineData("|\n  12", 12)]
        [InlineData(">-\n  12", 12)]
        [InlineData("' 12 '", 12)]
        [InlineData("\"\\t12\\n\"", 12)]
        [InlineData("'0x1F'", 31)]
        [InlineData("'0o17'", 15)]
        [InlineData("'+12'", 12)]
        [InlineData("'-12'", -12)]
        [InlineData("!local '12'", 12)]
        public void Ruling1_Integers_EveryPosition(string scalar, int expected)
        {
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<int>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<int?>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<long>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal((short)expected, YamlSerializer.Deserialize<Holder<short>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<double>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<decimal>>("A: " + scalar + "\n", s_lenient)!.A);
            if (!scalar.Contains("0x") && !scalar.Contains("0o"))
            {
                // Int128 and Half read decimal text only, plain or not.
#if NET
                Assert.Equal(expected, (int)YamlSerializer.Deserialize<Holder<Int128>>("A: " + scalar + "\n", s_lenient)!.A);
                Assert.Equal(expected, (int)YamlSerializer.Deserialize<Holder<Half>>("A: " + scalar + "\n", s_lenient)!.A);
#endif
            }
            Assert.Equal(new[] { expected }, YamlSerializer.Deserialize<List<int>>("- " + scalar + "\n", s_lenient));
            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, int>>("A: " + scalar + "\n", s_lenient)!["A"]);
            Assert.Equal(expected, YamlSerializer.Deserialize<Positional>("A: " + scalar + "\nB: true\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<int>(scalar, s_lenient));

            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<int>>("A: " + scalar + "\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<int>(scalar));
        }

        [Theory]
        [InlineData("'1.5'", 1.5)]
        [InlineData("'.5'", 0.5)]
        [InlineData("'1e3'", 1000.0)]
        [InlineData("'.inf'", double.PositiveInfinity)]
        [InlineData("'-.iNf'", double.NegativeInfinity)]
        [InlineData("'NaN'", double.NaN)]
        [InlineData("!!str -Infinity", double.NegativeInfinity)]
        public void Ruling1_Floats(string scalar, double expected)
        {
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<double>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal((float)expected, YamlSerializer.Deserialize<Holder<float>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<double>>("A: " + scalar + "\n"));
            if (double.IsFinite(expected))
            {
                Assert.Equal((decimal)expected, YamlSerializer.Deserialize<Holder<decimal>>("A: " + scalar + "\n", s_lenient)!.A);
            }
            else
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<decimal>>("A: " + scalar + "\n", s_lenient));
            }
        }

        [Theory]
        // A quoted null is a string, in both libraries too.
        [InlineData("''")]
        [InlineData("'  '")]
        [InlineData("'null'")]
        [InlineData("'~'")]
        [InlineData("'NuLL'")]
        [InlineData("\"Null\"")]
        // One plain scalar only.
        [InlineData("'1 # c'")]
        [InlineData("'&a 1'")]
        [InlineData("'!!int 1'")]
        [InlineData("'- 1'")]
        [InlineData("'[1]'")]
        [InlineData("'a: 1'")]
        [InlineData("'1\n\n  2'")]
        [InlineData("'%1'")]
        [InlineData("'[1'")]
        [InlineData("'1_000'")]
        [InlineData("'1.5'")]
        [InlineData("'true'")]
        [InlineData("'12x'")]
        public void Ruling1_TextsThatDoNotQualify(string scalar)
        {
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<int>>("A: " + scalar + "\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<int?>>("A: " + scalar + "\n", s_lenient));
            if (scalar != "'true'")
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<bool>>("A: " + scalar + "\n", s_lenient));
            }
            // A string target still gets the text.
            Assert.NotNull(YamlSerializer.Deserialize<Holder<string>>("A: " + scalar + "\n", s_lenient)!.A);
        }

        [Fact]
        public void Ruling1_LongTextIsNotTried()
        {
            string padded = "'" + new string(' ', 300) + "12'";
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<int>>("A: " + padded + "\n", s_lenient));
            string fits = "'" + new string(' ', 200) + "12'";
            Assert.Equal(12, YamlSerializer.Deserialize<Holder<int>>("A: " + fits + "\n", s_lenient)!.A);
        }

        [Fact]
        public void Ruling1_TheSchemaDecides()
        {
            // '1_000' reads as plain 1_000 would: under YAML 1.1 only (both libraries read it with their defaults).
            var reader = new Utf8YamlReader("A: '1_000'\nB: 'yes'\n"u8, new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = true });
            Yaml11Pair pair = YamlSerializer.Deserialize<Yaml11Pair>(ref reader)!;
            Assert.Equal((1000, true), (pair.A, pair.B));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Yaml11Pair>("A: '1_000'\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Yaml11Pair>("B: 'yes'\n", s_lenient));
        }

        public class Yaml11Pair
        {
            public int A { get; set; }
            public bool B { get; set; }
        }

        [Fact]
        public void Ruling1_NumberHandling()
        {
            // AllowReadingFromString alone keeps System.Text.Json's rule: the exact text, so a padded or hex string
            // throws and 'NaN' reads; with the option too, the lenient text reads whatever NumberHandling says (F).
            var stringsOnly = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            var both = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString, AcceptLenientScalars = true };
            var named = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowNamedFloatingPointLiterals, AcceptLenientScalars = true };
            Assert.Equal(12, YamlSerializer.Deserialize<Holder<int>>("A: '12'\n", stringsOnly)!.A);
            Assert.Equal(double.NaN, YamlSerializer.Deserialize<Holder<double>>("A: 'NaN'\n", stringsOnly)!.A);
            foreach (string scalar in new[] { "' 12'", "'0x1F'", "|\n  12" })
            {
                Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<int>>("A: " + scalar + "\n", stringsOnly));
                Assert.True(YamlSerializer.Deserialize<Holder<int>>("A: " + scalar + "\n", both)!.A is 12 or 31);
            }
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<double>>("A: .iNf\n", stringsOnly));
            Assert.Equal(double.PositiveInfinity, YamlSerializer.Deserialize<Holder<double>>("A: .iNf\n", both)!.A);
            Assert.Equal(double.PositiveInfinity, YamlSerializer.Deserialize<Holder<double>>("A: .iNf\n", named)!.A);
            Assert.Equal(double.NegativeInfinity, YamlSerializer.Deserialize<Holder<float>>("A: -infinity\n", named)!.A);
#if NET
            Assert.True(Half.IsNaN(YamlSerializer.Deserialize<Holder<Half>>("A: nan\n", both)!.A));
#endif
            // Property names do not take the lenient texts: a key is never resolved.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<int, int>>("' 12': 1\n", both));
        }

        [Fact]
        public void Ruling1_TagsTakeTheLenientForms()
        {
            // A matching core tag takes the lenient forms with the option, so the token type is the tag's: the one place
            // the option changes a token, and so what an untyped read sees.
            var options = new YamlReaderOptions { AcceptLenientScalars = true };
            Assert.Equal(new[] { YamlTokenType.True, YamlTokenType.Number, YamlTokenType.Null },
                Tokens("[!!bool tRuE, !!float .iNf, !!null NuLL]", options));
            Assert.Equal(new[] { YamlTokenType.True }, Tokens("[!!bool yEs]", new YamlReaderOptions { AcceptLenientScalars = true, Schema = YamlSchemaKind.Yaml11 }));
            Assert.ThrowsAny<YamlException>(() => Tokens("[!!bool tRuE]", default));
            Assert.ThrowsAny<YamlException>(() => Tokens("[!!bool yEs]", options));
            Assert.ThrowsAny<YamlException>(() => Tokens("[!!int true]", options));
            Assert.ThrowsAny<YamlException>(() => Tokens("[!!bool 1]", options));
            Assert.Equal(double.PositiveInfinity, YamlSerializer.Deserialize<Holder<double>>("A: !!float .iNf\n", s_lenient)!.A);
            Assert.True(YamlSerializer.Deserialize<Holder<YamlNode>>("A: !!bool tRuE\n", s_lenient)!.A!.GetValue<bool>());
        }

        private static YamlTokenType[] Tokens(string yaml, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            var tokens = new List<YamlTokenType>();
            while (reader.Read())
            {
                if (reader.TokenType is not (YamlTokenType.StartArray or YamlTokenType.EndArray))
                {
                    tokens.Add(reader.TokenType);
                }
            }
            return tokens.ToArray();
        }

        // ------------------------------------------------------------------------------------------------------------
        // Rule 2: null into a non-nullable value type is its default

        public static IEnumerable<object[]> ValueTypes() => new Type[]
        {
            typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
            typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(Guid), typeof(Color), typeof(Point), typeof(KeyValuePair<string, int>),
#if NET
            typeof(Half), typeof(Int128), typeof(UInt128), typeof(DateOnly), typeof(TimeOnly),
#endif
        }.Select(t => new object[] { t });

        [Theory]
        [MemberData(nameof(ValueTypes))]
        public async Task Ruling2_NullIsTheDefault_EveryPosition(Type type)
        {
            object expected = Activator.CreateInstance(type)!;
            Type holder = typeof(Holder<>).MakeGenericType(type);
            Type list = typeof(List<>).MakeGenericType(type);
            Type array = type.MakeArrayType();
            Type dictionary = typeof(Dictionary<,>).MakeGenericType(typeof(string), type);
            var stream = new YamlSerializerOptions { AcceptLenientScalars = true, DefaultBufferSize = 1 };

            foreach (string scalar in new[] { "null", "~", "", "NuLL", "Null", "!!null ''" })
            {
                (string Yaml, Type Type, Func<object, object?> Get)[] cases =
                [
                    ("A: " + scalar + "\n", holder, o => holder.GetProperty("A")!.GetValue(o)),
                    ("- " + scalar + "\n", list, o => ((IList)o)[0]),
                    ("[" + scalar + ", " + scalar + "]", array, o => ((IList)o)[1]),
                    ("A: " + scalar + "\n", dictionary, o => ((IDictionary)o)["A"]),
                ];
                foreach ((string yaml, Type target, Func<object, object?> get) in cases)
                {
                    if (yaml.StartsWith("[", StringComparison.Ordinal) && (scalar.Length == 0 || type == typeof(byte)))
                    {
                        continue; // [ , ] is not a sequence of two empty nodes, and byte[] is base64.
                    }

                    Assert.Equal(expected, get(YamlSerializer.Deserialize(yaml, target, s_lenient)!));
                    Assert.Equal(expected, get(YamlSerializer.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), target, stream)!));
                    Assert.Equal(expected, get((await YamlSerializer.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), target, stream))!));
                    Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize(yaml, target));
                }

                if (scalar.Length > 0)
                {
                    Assert.Equal(expected, YamlSerializer.Deserialize(scalar, type, s_lenient));
                }
            }
        }

        [Fact]
        public void Ruling2_WhatKeepsItsBehaviour()
        {
            // A constructor parameter refuses null as without the option (SharpYaml builds the record and refuses it).
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Positional>("A: null\nB: true\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Positional>("A: 1\nB: NuLL\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<PositionalNullable>("A: null\nB: ~\n", s_lenient));
            Assert.Null(YamlSerializer.Deserialize<PositionalNullable>("A: NuLL\nB: 1\n", s_lenient)!.A);

            // Nullable value types get null; YamlElement gets a null element; the option does not change them.
            Assert.Null(YamlSerializer.Deserialize<Holder<int?>>("A: null\n", s_lenient)!.A);
            Assert.Equal(YamlValueKind.Null, YamlSerializer.Deserialize<Holder<YamlElement>>("A: null\n", s_lenient)!.A.ValueKind);

            // A converter of the user's keeps receiving the Null token, with HandleNull or without.
            Assert.Equal(-1, YamlSerializer.Deserialize<WithConverters>("A: null\n", s_lenient)!.A);
            Assert.Equal(-2, YamlSerializer.Deserialize<WithConverters>("B: null\n", s_lenient)!.B);

#if NET
            // A required member set to null is set (to its default).
            Assert.Equal(0, YamlSerializer.Deserialize<WithRequired>("A: null\n", s_lenient)!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<WithRequired>("{}", s_lenient));
#endif

            // An initialised member is set to the default, as it would be by an explicit 0.
            Assert.Equal(0, YamlSerializer.Deserialize<WithInitializer>("A: null\n", s_lenient)!.A);

            // RespectNullableAnnotations concerns reference types only.
            var annotations = new YamlSerializerOptions { AcceptLenientScalars = true, RespectNullableAnnotations = true };
            Assert.Equal(0, YamlSerializer.Deserialize<Holder<int>>("A: null\n", annotations)!.A);
        }

        public class WithConverters
        {
            [Serialization.YamlConverter(typeof(NullSeenConverter))]
            public int A { get; set; }

            [Serialization.YamlConverter(typeof(NullSeenWithoutHandleNullConverter))]
            public int B { get; set; }
        }

#if NET
        public class WithRequired
        {
            public required int A { get; set; }
        }
#endif

        public class WithInitializer
        {
            public int A { get; set; } = 5;
        }

        private sealed class NullSeenConverter : Serialization.YamlConverter<int>
        {
            public override bool HandleNull => true;

            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                reader.TokenType == YamlTokenType.Null ? -1 : reader.GetInt32();

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options) => writer.WriteNumberValue(value);
        }

        private sealed class NullSeenWithoutHandleNullConverter : Serialization.YamlConverter<int>
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                reader.TokenType == YamlTokenType.Null ? -2 : reader.GetInt32();

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options) => writer.WriteNumberValue(value);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Enums and padded text

        [Theory]
        [InlineData("'1'", DayOfWeek.Monday)]
        [InlineData("\"5\"", DayOfWeek.Friday)]
        [InlineData("!!str 2", DayOfWeek.Tuesday)]
        [InlineData("|\n  3", DayOfWeek.Wednesday)]
        [InlineData("' 4 '", DayOfWeek.Thursday)]
        [InlineData("'0x1'", DayOfWeek.Monday)]
        [InlineData("'-3'", (DayOfWeek)(-3))]
        public async Task Ruling6_Enums_QuotedNumbers(string scalar, DayOfWeek expected)
        {
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<DayOfWeek?>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Equal(new[] { expected }, YamlSerializer.Deserialize<List<DayOfWeek>>("- " + scalar + "\n", s_lenient));
            Assert.Equal(expected, YamlSerializer.Deserialize<Dictionary<string, DayOfWeek>>("A: " + scalar + "\n", s_lenient)!["A"]);
            Assert.Equal(expected, YamlSerializer.Deserialize<DayOfWeek>(scalar, s_lenient));
            var oneByte = new YamlSerializerOptions { AcceptLenientScalars = true, DefaultBufferSize = 1 };
            Assert.Equal(expected, (await YamlSerializer.DeserializeAsync<Holder<DayOfWeek>>(new MemoryStream(Encoding.UTF8.GetBytes("A: " + scalar + "\n")), oneByte))!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: " + scalar + "\n"));
        }

        [Fact]
        public void Ruling6_Enums_WhatDoesNotChange()
        {
            // Names need YamlStringEnumConverter, with or without the option (Styml's enums read numbers by default, as
            // System.Text.Json's do); with it, names and quoted numbers already read and still do.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: Monday\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: '1.0'\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: 'null'\n", s_lenient));
            var names = new YamlSerializerOptions { AcceptLenientScalars = true };
            names.Converters.Add(new YamlStringEnumConverter());
            Assert.Equal(DayOfWeek.Monday, YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: ' Monday '\n", names)!.A);
            Assert.Equal(DayOfWeek.Monday, YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: '1'\n", names)!.A);
            // Without AllowNumbers the lenient number is not tried either.
            var namesOnly = new YamlSerializerOptions { AcceptLenientScalars = true };
            namesOnly.Converters.Add(new YamlStringEnumConverter(allowIntegerValues: false));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DayOfWeek>>("A: '1'\n", namesOnly));
            // Enum keys keep their own parse (which trims), the same with the option.
            Assert.Equal(DayOfWeek.Monday, Assert.Single(YamlSerializer.Deserialize<Dictionary<DayOfWeek, int>>("' 1': 1\n")!.Keys));
            Assert.Equal(DayOfWeek.Monday, Assert.Single(YamlSerializer.Deserialize<Dictionary<DayOfWeek, int>>("' 1': 1\n", s_lenient)!.Keys));
        }

        public static IEnumerable<object[]> PaddedTexts()
        {
            foreach ((Type type, string text, object expected) in new (Type, string, object)[]
            {
                (typeof(DateTime), "2001-01-01", new DateTime(2001, 1, 1)),
                (typeof(DateTime), "2001-01-01T10:00:00", new DateTime(2001, 1, 1, 10, 0, 0)),
                (typeof(DateTimeOffset), "2001-01-01T10:00:00+01:00", new DateTimeOffset(2001, 1, 1, 10, 0, 0, TimeSpan.FromHours(1))),
                (typeof(TimeSpan), "01:02:03", new TimeSpan(1, 2, 3)),
                (typeof(Guid), "6f9619ff-8b86-d011-b42d-00c04fc964ff", new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
                (typeof(Version), "1.2.3", new Version(1, 2, 3)),
#if NET
                (typeof(DateOnly), "2001-01-01", new DateOnly(2001, 1, 1)),
                (typeof(TimeOnly), "10:00:00", new TimeOnly(10, 0, 0)),
#endif
            })
            {
                foreach (string form in new[] { "' {0} '", "'{0} '", "' {0}'", "\"{0}\\n\"", "|\n  {0}", "'\t{0}\t'" })
                {
                    yield return new object[] { type, string.Format(CultureInfo.InvariantCulture, form, text), expected };
                }
            }
        }

        [Theory]
        [MemberData(nameof(PaddedTexts))]
        public void Ruling6_PaddedText(Type type, string scalar, object expected)
        {
            Type holder = typeof(Holder<>).MakeGenericType(type);
            Type list = typeof(List<>).MakeGenericType(type);
            Assert.Equal(expected, holder.GetProperty("A")!.GetValue(YamlSerializer.Deserialize("A: " + scalar + "\n", holder, s_lenient)));
            Assert.Equal(expected, ((IList)YamlSerializer.Deserialize("- " + scalar + "\n", list, s_lenient)!)[0]);
            var stream = new YamlSerializerOptions { AcceptLenientScalars = true, DefaultBufferSize = 1 };
            Assert.Equal(expected, holder.GetProperty("A")!.GetValue(YamlSerializer.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes("A: " + scalar + "\n")), holder, stream)));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize("A: " + scalar + "\n", holder));
            if (type.IsValueType)
            {
                Type nullable = typeof(Holder<>).MakeGenericType(typeof(Nullable<>).MakeGenericType(type));
                Assert.Equal(expected, nullable.GetProperty("A")!.GetValue(YamlSerializer.Deserialize("A: " + scalar + "\n", nullable, s_lenient)));
            }
        }

        [Theory]
        [InlineData("'x '", 'x')]
        [InlineData("\"x\\n\"", 'x')]
        [InlineData("|\n  x", 'x')]
        public void Ruling6_Char_TrailingPadOnly(string scalar, char expected)
        {
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<char>>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<char>>("A: " + scalar + "\n"));
        }

        [Theory]
        [InlineData("' x '")]
        [InlineData("'\tx'")]
        [InlineData("'xy '")]
        [InlineData("'  '")]
        public void Ruling6_Char_OtherTextsDoNotQualify(string scalar)
        {
            // YamlDotNet takes the first character (' x ' is the space); neither library reads these as 'x'.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<char>>("A: " + scalar + "\n", s_lenient));
        }

        [Theory]
        [InlineData("' 2001-13-01 '")]
        [InlineData("'2001-01-01 # c'")]
        [InlineData("' 2001-01-01x'")]
        [InlineData("'  '")]
        public void Ruling6_PaddedText_InvalidStaysInvalid(string scalar)
        {
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<DateTime>>("A: " + scalar + "\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<Guid>>("A: " + scalar + "\n", s_lenient));
            // A key is never resolved.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<DateTime, int>>(scalar + ": 1\n", s_lenient));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Transport: the new forms through every segment split, one-byte stream buffers and a generated context

        public class Transported
        {
            public int A { get; set; }
            public int B { get; set; }
            public double C { get; set; }
            public int D { get; set; }
            public bool E { get; set; }
            public bool F { get; set; }
            public double G { get; set; }
            public Dictionary<bool, int>? H { get; set; }
            public Point P { get; set; }
            public List<int>? L { get; set; }
            public bool? K { get; set; }
            public DayOfWeek Day { get; set; }
            public DateTime When { get; set; }
            public TimeSpan Span { get; set; }
            public Guid Id { get; set; }
            public char Ch { get; set; }
        }

        private const string TransportedYaml =
            "A: '12'\nB: \" 0x1F \"\nC: !!str 1.5\nD: |\n  7\nE: 'tRuE'\nF: !!bool tRuE\nG: !!float .iNf\nH:\n  tRuE: 1\nP: null\nL: [null, 3]\n" +
            "Day: '1'\nWhen: ' 2001-01-01 '\nSpan: \"01:02:03\\n\"\nId: |\n  6f9619ff-8b86-d011-b42d-00c04fc964ff\nCh: 'x '\nK: \"yEs\"\n";

        private static void AssertTransported(Transported t, bool yaml11)
        {
            Assert.Equal((12, 31, 1.5, 7), (t.A, t.B, t.C, t.D));
            Assert.True(t.E && t.F);
            Assert.Equal(double.PositiveInfinity, t.G);
            Assert.Equal(1, t.H![true]);
            Assert.Equal(default, t.P);
            Assert.Equal(new[] { 0, 3 }, t.L);
            Assert.Equal(yaml11 ? true : null, t.K);
            Assert.Equal(DayOfWeek.Monday, t.Day);
            Assert.Equal(new DateTime(2001, 1, 1), t.When);
            Assert.Equal(new TimeSpan(1, 2, 3), t.Span);
            Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), t.Id);
            Assert.Equal('x', t.Ch);
        }

        [Fact]
        public void Ruling6_TheReaderMethodsAgree()
        {
            // TryGet and Get read padded text alike with the option, as they do numbers (TryGetInt32 and GetInt32).
            var options = new YamlReaderOptions { AcceptLenientScalars = true };
            var reader = new Utf8YamlReader("[' 2001-01-01 ', \"2001-01-01T10:00:00+01:00\\n\", ' 6f9619ff-8b86-d011-b42d-00c04fc964ff ', ' 12 ']"u8, options);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.TryGetDateTime(out DateTime date) && date == reader.GetDateTime());
            Assert.True(reader.Read());
            Assert.True(reader.TryGetDateTimeOffset(out DateTimeOffset offset) && offset == reader.GetDateTimeOffset());
            Assert.True(reader.Read());
            Assert.True(reader.TryGetGuid(out Guid guid) && guid == reader.GetGuid());
            Assert.True(reader.Read());
            Assert.True(reader.TryGetInt32(out int number) && number == reader.GetInt32() && number == 12);

            var strict = new Utf8YamlReader("[' 2001-01-01 ']"u8);
            Assert.True(strict.Read());
            Assert.True(strict.Read());
            Assert.False(strict.TryGetDateTime(out _));
        }

        [Fact]
        public async Task Transport_StreamsSegmentsAndGeneratedContext()
        {
            // Without "K" under the core schema (K: "yEs" needs YAML 1.1).
            string core = TransportedYaml.Replace("K: \"yEs\"\n", "");
            byte[] bytes = Encoding.UTF8.GetBytes(core);
            AssertTransported(YamlSerializer.Deserialize<Transported>(core, s_lenient)!, false);

            var oneByte = new YamlSerializerOptions { AcceptLenientScalars = true, DefaultBufferSize = 1 };
            AssertTransported(YamlSerializer.Deserialize<Transported>(new MemoryStream(bytes), oneByte)!, false);
            AssertTransported((await YamlSerializer.DeserializeAsync<Transported>(new MemoryStream(bytes), oneByte))!, false);

            // The source-generated context: the wrappers forward ReadsNullAsDefault (the struct member, the list).
            var context = new LenientScalarsRulingsContext(new YamlSerializerOptions { AcceptLenientScalars = true });
            AssertTransported(YamlSerializer.Deserialize(core, context.Transported)!, false);
            AssertTransported((await YamlSerializer.DeserializeAsync(new MemoryStream(bytes), context.Transported))!, false);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize(core, LenientScalarsRulingsContext.Default.Transported));

            // Every split into two segments, and one byte per segment, with the YAML 1.1 schema (K: "yEs", and yEs: as a
            // key) and without.
            foreach (bool yaml11 in new[] { false, true })
            {
                string text = yaml11 ? TransportedYaml.Replace("  tRuE: 1", "  yEs: 1") : core;
                byte[] data = Encoding.UTF8.GetBytes(text);
                var options = new YamlReaderOptions { AcceptLenientScalars = true, Schema = yaml11 ? YamlSchemaKind.Yaml11 : YamlSchemaKind.Core };
                for (int split = 1; split < data.Length; split++)
                {
                    var reader = new Utf8YamlReader(Segments(data, split), options);
                    AssertTransported(YamlSerializer.Deserialize<Transported>(ref reader)!, yaml11);
                }

                var bytewise = new Utf8YamlReader(Segments(data, Enumerable.Range(1, data.Length - 1).ToArray()), options);
                AssertTransported(YamlSerializer.Deserialize<Transported>(ref bytewise)!, yaml11);
            }
        }

        private static System.Buffers.ReadOnlySequence<byte> Segments(byte[] data, params int[] splits)
        {
            var bounds = new List<int> { 0 };
            bounds.AddRange(splits);
            bounds.Add(data.Length);
            var first = new Segment(data.AsMemory(bounds[0], bounds[1] - bounds[0]), 0);
            Segment last = first;
            for (int i = 1; i + 1 < bounds.Count; i++)
            {
                last = last.Append(data.AsMemory(bounds[i], bounds[i + 1] - bounds[i]));
            }
            return new System.Buffers.ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : System.Buffers.ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory, RunningIndex + Memory.Length);
                Next = next;
                return next;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // G: the documentation's two notes

        [Fact]
        public void HandleNullConverter_ReceivesTheStringTokenForNuLL()
        {
            WithStringConverter w = YamlSerializer.Deserialize<WithStringConverter>("A: NuLL\nB: null\n", s_lenient)!;
            Assert.Equal("String:NuLL", w.A);
            Assert.Equal("Null", w.B);
        }

        public class WithStringConverter
        {
            [Serialization.YamlConverter(typeof(TokenNamingConverter))]
            public string? A { get; set; }

            [Serialization.YamlConverter(typeof(TokenNamingConverter))]
            public string? B { get; set; }
        }

        private sealed class TokenNamingConverter : Serialization.YamlConverter<string?>
        {
            public override bool HandleNull => true;

            public override string? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                reader.TokenType == YamlTokenType.String ? "String:" + reader.GetString() : reader.TokenType.ToString();

            public override void Write(Utf8YamlWriter writer, string? value, YamlSerializerOptions options) => writer.WriteStringValue(value);
        }

        /// <summary>
        /// A string that a lenient reader takes as null comes back as null when Styml's default writer wrote it plain
        /// (<c>NuLl</c>), and as itself when the writer quoted for lenient readers. The option's documentation says so.
        /// </summary>
        [Theory]
        [InlineData(YamlSchemaKind.Core)]
        [InlineData(YamlSchemaKind.Yaml11)]
        public void RoundTrip_WithQuoteForLenientReaders(YamlSchemaKind schema)
        {
            var strings = new List<string>();
            foreach (string word in new[] { "null", "true", "false", "yes", "no", "on", "off", "y", "n", ".inf", "-.inf", ".nan", "nan", "infinity", "~", "1", "0x1F", "1.5" })
            {
                for (int mask = 0; mask < 1 << word.Length; mask++)
                {
                    var chars = word.ToCharArray();
                    for (int i = 0; i < chars.Length; i++)
                    {
                        if ((mask & (1 << i)) != 0)
                        {
                            chars[i] = char.ToUpperInvariant(chars[i]);
                        }
                    }
                    strings.Add(new string(chars));
                }
            }
            strings = strings.Distinct().ToList();

            string[] expectedLost = strings.Where(s => s.Equals("null", StringComparison.OrdinalIgnoreCase) && s is not ("null" or "Null" or "NULL")).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.Equal(13, expectedLost.Length);

            foreach (bool quoteForLenientReaders in new[] { false, true })
            {
                var write = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, QuoteForLenientReaders = quoteForLenientReaders };
                var document = new RoundTripped { List = strings, Map = strings.ToDictionary(s => "k" + s, s => s), Member = strings[0] };
                string yaml = YamlSerializer.Serialize(document, write);

                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = schema, AcceptLenientScalars = true });
                RoundTripped back = YamlSerializer.Deserialize<RoundTripped>(ref reader)!;
                string[] lostInList = strings.Where((s, i) => back.List![i] != s).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                string[] lostInMap = strings.Where(s => back.Map!["k" + s] != s).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                Assert.Equal(quoteForLenientReaders ? [] : expectedLost, lostInList);
                Assert.Equal(lostInList, lostInMap);
                Assert.All(lostInList, s => Assert.Null(back.List![strings.IndexOf(s)]));

                foreach (string s in expectedLost)
                {
                    string member = YamlSerializer.Serialize(new RoundTripped { Member = s }, write);
                    var memberReader = new Utf8YamlReader(Encoding.UTF8.GetBytes(member), new YamlReaderOptions { Schema = schema, AcceptLenientScalars = true });
                    Assert.Equal(quoteForLenientReaders ? s : null, YamlSerializer.Deserialize<RoundTripped>(ref memberReader)!.Member);
                }
            }
        }

        public class RoundTripped
        {
            public List<string?>? List { get; set; }
            public Dictionary<string, string?>? Map { get; set; }
            public string? Member { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // H: the converted sites that had no test

        [Fact]
        public void Null_MemoryOfByte_ReadOnlyMemoryOfT_Union()
        {
            Assert.True(YamlSerializer.Deserialize<Holder<Memory<byte>>>("A: NuLL\n", s_lenient)!.A.IsEmpty);
            Assert.True(YamlSerializer.Deserialize<Holder<ReadOnlyMemory<int>>>("A: NuLL\n", s_lenient)!.A.IsEmpty);
            Assert.True(YamlSerializer.Deserialize<Holder<ReadOnlyMemory<string>>>("A: nULL\n", s_lenient)!.A.IsEmpty);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<ReadOnlyMemory<int>>>("A: NuLL\n"));
            // Without the option NuLL is the base64 of three bytes.
            Assert.Equal(3, YamlSerializer.Deserialize<Holder<Memory<byte>>>("A: NuLL\n")!.A.Length);

#if NET11_0_OR_GREATER
            // Union types need .NET 11's IUnion.
            Assert.Null(YamlSerializer.Deserialize<Holder<NullableIntUnion>>("A: NuLL\n", s_lenient)!.A.Value);
            Assert.Null(YamlSerializer.Deserialize<Holder<NullableIntUnion>>("A: null\n")!.A.Value);
            Assert.Equal(42, YamlSerializer.Deserialize<Holder<NullableIntUnion>>("A: 42\n", s_lenient)!.A.Value);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder<NullableIntUnion>>("A: NuLL\n"));
#endif
        }

#if NET11_0_OR_GREATER
        public union NullableIntUnion(int?);
#endif
    }

    [YamlSerializable(typeof(LenientScalarsRulingsTests.Transported))]
    internal partial class LenientScalarsRulingsContext : YamlSerializerContext;
}
