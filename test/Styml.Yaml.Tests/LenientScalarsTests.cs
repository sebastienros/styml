// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// <see cref="YamlReaderOptions.AcceptLenientScalars"/> and <see cref="YamlSerializerOptions.AcceptLenientScalars"/>:
    /// off by default; with it, typed reads take <c>tRue</c>, <c>yEs</c> (YAML
    /// 1.1 schema only), <c>.iNf</c> and <c>NuLL</c> as YamlDotNet or SharpYaml do. M4b first limited it to plain,
    /// untagged scalars and kept every token type; M4b added quoted, block and
    /// tagged booleans and numbers, core tags that take the lenient forms (and so change the token), null into value
    /// types and the key rules: LenientScalarsRulingsTests. The cells against the libraries are in Styml.Parity.Tests
    /// (LenientScalarsParityTests).
    /// </summary>
    public class LenientScalarsTests
    {
        private static readonly YamlSerializerOptions s_lenient = new() { AcceptLenientScalars = true };

        public class Holder<T>
        {
            public T A { get; set; } = default!;
        }

        public record Positional(string? A, int? B);

        public class NotNullable
        {
            public string A { get; set; } = "";
        }

        private static T Read<T>(string yaml, bool lenient) => YamlSerializer.Deserialize<T>(yaml, lenient ? s_lenient : null)!;

        private static Utf8YamlReader ValueReader(string yaml, YamlReaderOptions options)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), options);
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            return reader;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Options

        [Fact]
        public void Options_OffByDefault_CopiedAndLocked()
        {
            Assert.False(default(YamlReaderOptions).AcceptLenientScalars);
            Assert.False(new YamlReaderOptions().AcceptLenientScalars);
            Assert.False(new YamlSerializerOptions().AcceptLenientScalars);
            Assert.False(YamlSerializerOptions.Default.AcceptLenientScalars);
            Assert.False(new YamlSerializerOptions(YamlSerializerDefaults.Web).AcceptLenientScalars);

            var options = new YamlSerializerOptions { AcceptLenientScalars = true };
            Assert.True(new YamlSerializerOptions(options).AcceptLenientScalars);
            Assert.True(options.GetReaderOptions().AcceptLenientScalars);
            Assert.False(new YamlSerializerOptions().GetReaderOptions().AcceptLenientScalars);

            options.MakeReadOnly(populateMissingResolver: true);
            Assert.Throws<InvalidOperationException>(() => options.AcceptLenientScalars = false);
            Assert.True(new YamlSerializerOptions(options) { AcceptLenientScalars = false } is { AcceptLenientScalars: false, IsReadOnly: false });
        }

        [Fact]
        public void Options_SerializerCachesDistinguishTheOption()
        {
            // Two otherwise equal options instances must not share metadata or converters across the option.
            Assert.Equal("NuLL", Read<Holder<string?>>("A: NuLL\n", lenient: false).A);
            Assert.Null(Read<Holder<string?>>("A: NuLL\n", lenient: true).A);
            Assert.Equal("NuLL", Read<Holder<string?>>("A: NuLL\n", lenient: false).A);
        }

        [Fact]
        public void Options_TheReadersOwnOptionApplies()
        {
            // Deserialize(ref reader): the reader's options take precedence over the serializer's (as for
            // AllowTrailingCommas and the others), for every behaviour of the option alike.
            byte[] yaml = "A: NuLL\nB: tRue\nC: .iNf\n"u8.ToArray();

            var reader = new Utf8YamlReader(yaml, new YamlReaderOptions { AcceptLenientScalars = true });
            Mixed m = YamlSerializer.Deserialize<Mixed>(ref reader)!;
            Assert.Equal((null, true, double.PositiveInfinity), (m.A, m.B, m.C));

            reader = new Utf8YamlReader(yaml, new YamlReaderOptions { AcceptLenientScalars = true });
            m = YamlSerializer.Deserialize<Mixed>(ref reader, new YamlSerializerOptions())!;
            Assert.Equal((null, true, double.PositiveInfinity), (m.A, m.B, m.C));

            Assert.ThrowsAny<YamlException>(() =>
            {
                var strict = new Utf8YamlReader(yaml, new YamlReaderOptions());
                YamlSerializer.Deserialize<Mixed>(ref strict, s_lenient);
            });
            var strictNull = new Utf8YamlReader("A: NuLL\n"u8, new YamlReaderOptions());
            Assert.Equal("NuLL", YamlSerializer.Deserialize<Mixed>(ref strictNull, s_lenient)!.A);
        }

        public class Mixed
        {
            public string? A { get; set; }
            public bool B { get; set; }
            public double C { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // NuLL: a typed read takes it as it takes null; untyped reads see the string, or refuse it, exactly as before.

        [Theory]
        [InlineData("NuLL")]
        [InlineData("nULL")]
        [InlineData("nulL")]
        [InlineData("NUll")]
        public void Null_TypedTargets(string scalar)
        {
            string value = "A: " + scalar + "\n";
            string item = "- " + scalar + "\n";

            Assert.Equal(scalar, Read<Holder<string?>>(value, false).A);
            Assert.Null(Read<Holder<string?>>(value, true).A);

            Assert.ThrowsAny<YamlException>(() => Read<Holder<int?>>(value, false));
            Assert.Null(Read<Holder<int?>>(value, true).A);
            Assert.Null(Read<Holder<double?>>(value, true).A);
            Assert.Null(Read<Holder<bool?>>(value, true).A);

            // A non-nullable value type reads it as its default with the option, as it reads null (until M4b both threw), and refuses both without it.
            Assert.ThrowsAny<YamlException>(() => Read<Holder<int>>(value, false));
            Assert.Equal(0, Read<Holder<int>>(value, true).A);
            Assert.Equal(0, Read<Holder<int>>("A: null\n", true).A);

            Assert.Equal(new[] { scalar }, Read<List<string?>>(item, false));
            Assert.Equal(new string?[] { null }, Read<List<string?>>(item, true));
            Assert.Equal(new string?[] { null }, Read<string?[]>(item, true));
            Assert.ThrowsAny<YamlException>(() => Read<List<int?>>(item, false));
            Assert.Equal(new int?[] { null }, Read<List<int?>>(item, true));
            Assert.Equal(new int?[] { null }, Read<int?[]>("[" + scalar + "]", true));

            Assert.Equal(scalar, Read<Dictionary<string, string?>>(value, false)["A"]);
            Assert.Null(Read<Dictionary<string, string?>>(value, true)["A"]);
            Assert.Null(Read<Dictionary<string, int?>>(value, true)["A"]);
            Assert.Null(Read<Dictionary<string, Holder<int>?>>(value, true)["A"]);

            Assert.Null(Read<Holder<Holder<int>?>>(value, true).A);
            Assert.Null(Read<Holder<int[]?>>(value, true).A);
            Assert.Null(Read<Holder<List<int>?>>(value, true).A);
            Assert.Null(Read<Holder<Uri?>>(value, true).A);
            Assert.Equal(scalar, Read<Holder<Uri?>>(value, false).A!.OriginalString);
            Assert.Null(Read<Holder<Version?>>(value, true).A);
            Assert.Null(Read<Holder<byte[]?>>(value, true).A);
            Assert.True(Read<Holder<ReadOnlyMemory<byte>>>(value, true).A.IsEmpty);
            Assert.True(Read<Holder<Memory<int>>>(value, true).A.IsEmpty);

            // Constructor parameters.
            Positional p = Read<Positional>("A: " + scalar + "\nB: " + scalar + "\n", true);
            Assert.Equal((null, null), (p.A, p.B));
            Assert.ThrowsAny<YamlException>(() => Read<Positional>("B: " + scalar + "\n", false));

            // The root.
            Assert.Null(Read<string?>(scalar, true));
            Assert.Equal(scalar, Read<string?>(scalar, false));
            Assert.Null(Read<int?>(scalar, true));
        }

        [Fact]
        public void Null_NullableAnnotationsApplyAsToNull()
        {
            var options = new YamlSerializerOptions { AcceptLenientScalars = true, RespectNullableAnnotations = true };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<NotNullable>("A: null\n", options));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<NotNullable>("A: NuLL\n", options));
            options = new YamlSerializerOptions { RespectNullableAnnotations = true };
            Assert.Equal("NuLL", YamlSerializer.Deserialize<NotNullable>("A: NuLL\n", options)!.A);
        }

        [Fact]
        public void Null_UntypedTargetsAreUnchanged()
        {
            foreach (bool lenient in new[] { false, true })
            {
                // The DOM (which reads YAML since M5a) keeps the string, with or without the option.
                Assert.Equal("NuLL", ((YamlElement)Read<Holder<object?>>("A: NuLL\n", lenient).A!).GetString());
                Assert.Equal("NuLL", Read<Holder<YamlElement>>("A: NuLL\n", lenient).A.GetString());
                Assert.Equal("NuLL", Read<Holder<YamlElement?>>("A: NuLL\n", lenient).A!.Value.GetString());
                Assert.Equal("NuLL", Read<Holder<YamlDocument?>>("A: NuLL\n", lenient).A!.RootElement.GetString());
                Assert.Equal("NuLL", ((YamlElement)Read<List<object?>>("- NuLL\n", lenient)[0]!).GetString());
                Assert.Equal("NuLL", ((YamlElement)Read<Dictionary<string, object?>>("A: NuLL\n", lenient)["A"]!).GetString());

                // The node model sees the string.
                Assert.Equal("NuLL", Read<Holder<YamlNode?>>("A: NuLL\n", lenient).A!.GetValue<string>());
                Assert.Equal("NuLL", Read<Holder<YamlValue?>>("A: NuLL\n", lenient).A!.GetValue<string>());
                Assert.Equal("NuLL", Read<List<YamlNode?>>("- NuLL\n", lenient)[0]!.GetValue<string>());
                Assert.ThrowsAny<YamlException>(() => Read<Holder<YamlObject?>>("A: NuLL\n", lenient));
                Assert.ThrowsAny<YamlException>(() => Read<Holder<YamlArray?>>("A: NuLL\n", lenient));

                // As null does, for comparison.
                Assert.Null(Read<Holder<object?>>("A: null\n", lenient).A);
                Assert.Null(Read<Holder<YamlNode?>>("A: null\n", lenient).A);
            }

            // Extension data is untyped too: the option changes nothing there.
            Assert.Equal(DescribeExtensionData(false), DescribeExtensionData(true));
            Assert.Equal("null", DescribeExtensionData(false, "null"));
        }

        private static string DescribeExtensionData(bool lenient, string scalar = "NuLL")
        {
            try
            {
                return Read<WithExtensionData>("A: 1\nB: " + scalar + "\n", lenient).Rest!["B"]?.ToString() ?? "null";
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }

        public class WithExtensionData
        {
            public int A { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object?>? Rest { get; set; }
        }

        [Theory]
        [InlineData("'NuLL'")]
        [InlineData("\"NuLL\"")]
        [InlineData("!!str NuLL")]
        [InlineData("!local NuLL")]
        [InlineData("|-\n  NuLL")]
        [InlineData(">-\n  NuLL")]
        [InlineData("NuLLL")]
        [InlineData("NuL")]
        [InlineData("Nu\n  LL")]
        public void Null_OtherScalarsDoNotQualify(string scalar)
        {
            string text = Read<Holder<string?>>("A: " + scalar + "\n", false).A!;
            Assert.NotNull(text);
            Assert.Equal(text, Read<Holder<string?>>("A: " + scalar + "\n", true).A);
            Assert.ThrowsAny<YamlException>(() => Read<Holder<int?>>("A: " + scalar + "\n", true));
            Assert.False(ValueReader("a: " + scalar + "\n", new YamlReaderOptions { AcceptLenientScalars = true }).IsLenientNull);
        }

        [Fact]
        public void Null_TagTakesTheLenientForm()
        {
            // !!null NuLL is a tag mismatch without the option. With it, the core tag takes the lenient form, as SharpYaml
            // reads it (until M4b it stayed a mismatch with the option too).
            Assert.Null(Read<Holder<int?>>("A: !!null NuLL\n", true).A);
            Assert.Null(Read<Holder<string?>>("A: !!null NuLL\n", true).A);
            Assert.ThrowsAny<YamlException>(() => Read<Holder<int?>>("A: !!null NuLL\n", false));
            Assert.ThrowsAny<YamlException>(() => Read<Holder<int?>>("A: !!null NuLLL\n", true));
        }

        [Fact]
        public void Null_ReaderPredicate()
        {
            Utf8YamlReader reader = ValueReader("a: NuLL\n", new YamlReaderOptions { AcceptLenientScalars = true });
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.True(reader.IsLenientNull);
            Assert.Equal("NuLL", reader.GetString());

            reader = ValueReader("a: NuLL\n", default);
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.False(reader.IsLenientNull);

            // The core schema's forms are Null tokens, not lenient nulls.
            reader = ValueReader("a: Null\n", new YamlReaderOptions { AcceptLenientScalars = true });
            Assert.Equal(YamlTokenType.Null, reader.TokenType);
            Assert.False(reader.IsLenientNull);

            // Keys are never null.
            reader = new Utf8YamlReader("NuLL: 1\n"u8, new YamlReaderOptions { AcceptLenientScalars = true });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.Equal(YamlTokenType.PropertyName, reader.TokenType);
            Assert.False(reader.IsLenientNull);
            Assert.Equal(1, Read<Dictionary<string, int>>("NuLL: 1\n", true)["NuLL"]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // yEs: only with the option and the YAML 1.1 schema.

        [Theory]
        [InlineData("yEs", true)]
        [InlineData("YeS", true)]
        [InlineData("yES", true)]
        [InlineData("oN", true)]
        [InlineData("nO", false)]
        [InlineData("oFF", false)]
        [InlineData("ofF", false)]
        [InlineData("tRue", true)]
        [InlineData("FaLSE", false)]
        public void Yaml11Boolean_OptionAndSchema(string scalar, bool expected)
        {
            string yaml = "a: " + scalar + "\n";
            var both = new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = true };
            Utf8YamlReader reader = ValueReader(yaml, both);
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.Equal(expected, reader.GetBoolean());

            reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("A: " + scalar + "\n"), both);
            Assert.Equal(expected, YamlSerializer.Deserialize<Holder<bool>>(ref reader)!.A);

            // The YAML 1.1 schema alone does not read them.
            Assert.Throws<InvalidOperationException>(() => ValueReader(yaml, new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11 }).GetBoolean());
        }

        [Theory]
        [InlineData("yEs")]
        [InlineData("yes")]
        [InlineData("Y")]
        [InlineData("y")]
        [InlineData("oN")]
        [InlineData("off")]
        public void Yaml11Boolean_TheOptionDoesNotBringInYaml11(string scalar)
        {
            Assert.Throws<InvalidOperationException>(() => ValueReader("a: " + scalar + "\n", new YamlReaderOptions { AcceptLenientScalars = true }).GetBoolean());
            Assert.ThrowsAny<YamlException>(() => Read<Holder<bool>>("A: " + scalar + "\n", true));
            Assert.ThrowsAny<YamlException>(() => Read<Holder<bool?>>("A: " + scalar + "\n", true));
        }

        [Theory]
        [InlineData("'yEs'")]
        [InlineData("\"oN\"")]
        [InlineData("!!str yEs")]
        [InlineData("!!bool yEs")]
        [InlineData("|-\n  yEs")]
        public void Yaml11Boolean_QuotedTaggedAndBlock(string scalar)
        {
            // With the option and the YAML 1.1 schema these read (YamlDotNet); until then they
            // did not qualify. Under the core schema they still throw.
            var both = new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = true };
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("A: " + scalar + "\n"), both);
            Assert.True(YamlSerializer.Deserialize<Holder<bool>>(ref reader)!.A);
            Assert.ThrowsAny<YamlException>(() => Read<Holder<bool>>("A: " + scalar + "\n", true));
        }

        [Theory]
        [InlineData("yEss")]
        [InlineData("o n")]
        [InlineData("'yEss'")]
        [InlineData("!!bool yEss")]
        public void Yaml11Boolean_OtherScalarsDoNotQualify(string scalar)
        {
            var both = new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = true };
            Assert.ThrowsAny<Exception>(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("A: " + scalar + "\n"), both);
                YamlSerializer.Deserialize<Holder<bool>>(ref reader);
            });
        }

        [Fact]
        public void Booleans_PropertyNames()
        {
            // Since M4b (before, tRuE: read by default and no YAML 1.1 word was a key): a quoted key
            // is text, true/false in any case, as System.Text.Json reads a property name; a plain key reads as a value
            // would, so tRuE: needs the option, yes: the YAML 1.1 schema, and yEs: both.
            foreach (bool lenient in new[] { false, true })
            {
                Dictionary<bool, int> d = Read<Dictionary<bool, int>>("TRUE: 1\n'fAlse': 2\n", lenient);
                Assert.Equal((1, 2), (d[true], d[false]));
            }
            Assert.Equal(1, Read<Dictionary<bool, int>>("tRuE: 1\n", true)[true]);
            Assert.ThrowsAny<YamlException>(() => Read<Dictionary<bool, int>>("tRuE: 1\n", false));

            static Dictionary<bool, int> Yaml11(string yaml, bool lenient)
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = lenient });
                return YamlSerializer.Deserialize<Dictionary<bool, int>>(ref reader)!;
            }

            foreach (bool lenient in new[] { false, true })
            {
                Dictionary<bool, int> d = Yaml11("yes: 1\nOff: 2\n", lenient);
                Assert.Equal((1, 2), (d[true], d[false]));
                Assert.ThrowsAny<YamlException>(() => Yaml11("'yes': 1\n", lenient));
            }
            Assert.Equal(1, Yaml11("yEs: 1\n", true)[true]);
            Assert.ThrowsAny<YamlException>(() => Yaml11("yEs: 1\n", false));
            Assert.ThrowsAny<YamlException>(() => Read<Dictionary<bool, int>>("yes: 1\n", true));
        }

        // ------------------------------------------------------------------------------------------------------------
        // .iNf, .nAn

        [Theory]
        [InlineData(".iNf", double.PositiveInfinity)]
        [InlineData(".INf", double.PositiveInfinity)]
        [InlineData("+.inF", double.PositiveInfinity)]
        [InlineData("-.iNF", double.NegativeInfinity)]
        [InlineData("-.InF", double.NegativeInfinity)]
        [InlineData(".nAn", double.NaN)]
        [InlineData(".NAn", double.NaN)]
        [InlineData(".naN", double.NaN)]
        public void Floats_NamesInAnyCase(string scalar, double expected)
        {
            Utf8YamlReader reader = ValueReader("a: " + scalar + "\n", new YamlReaderOptions { AcceptLenientScalars = true });
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.Equal(expected, reader.GetDouble());
            Assert.Equal((float)expected, reader.GetSingle());
            Assert.True(reader.TryGetDouble(out double d) && d.Equals(expected));
            Assert.Throws<InvalidOperationException>(() => ValueReader("a: " + scalar + "\n", default).GetDouble());

            Assert.Equal(expected, Read<Holder<double>>("A: " + scalar + "\n", true).A);
            Assert.Equal((float)expected, Read<Holder<float?>>("A: " + scalar + "\n", true).A);
            Assert.ThrowsAny<YamlException>(() => Read<Holder<double>>("A: " + scalar + "\n", false));
            // decimal has neither infinity nor NaN.
            Assert.ThrowsAny<YamlException>(() => Read<Holder<decimal>>("A: " + scalar + "\n", true));
        }

        [Theory]
        [InlineData("'.iNf'", double.PositiveInfinity)]
        [InlineData("!!str .iNf", double.PositiveInfinity)]
        [InlineData("!!float .iNf", double.PositiveInfinity)]
        [InlineData("-.nAn", double.NaN)]
        [InlineData("+.nan", double.NaN)]
        [InlineData("-.nan", double.NaN)]
        [InlineData("NaN", double.NaN)]
        [InlineData("nAN", double.NaN)]
        [InlineData("Infinity", double.PositiveInfinity)]
        [InlineData("-iNfinity", double.NegativeInfinity)]
        [InlineData("+INFINITY", double.PositiveInfinity)]
        [InlineData("'NaN'", double.NaN)]
        public void Floats_OtherLibrariesForms(string scalar, double expected)
        {
            // Quoted and tagged forms, and the parity rule (YamlDotNet's signed .nan, SharpYaml's .NET names); before
            // M4b these did not qualify. Without the option they throw.
            Assert.Equal(expected, Read<Holder<double>>("A: " + scalar + "\n", true).A);
            Assert.Equal((float)expected, Read<Holder<float>>("A: " + scalar + "\n", true).A);
            Assert.ThrowsAny<YamlException>(() => Read<Holder<double>>("A: " + scalar + "\n", false));
        }

        [Theory]
        [InlineData(".iNfinity")]
        [InlineData("iNf")]
        [InlineData("-inf")]
        [InlineData("+nan")]
        [InlineData("-nan")]
        [InlineData("\u221E")]
        [InlineData("'iNf'")]
        [InlineData("!!float iNf")]
        public void Floats_OtherScalarsDoNotQualify(string scalar)
        {
            Assert.ThrowsAny<YamlException>(() => Read<Holder<double>>("A: " + scalar + "\n", true));
            Assert.ThrowsAny<YamlException>(() => Read<Holder<float>>("A: " + scalar + "\n", true));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Streams with a small buffer, multi-segment input, source generation.

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(0)]
        public async Task Stream_SmallBuffers(int bufferSize)
        {
            var options = new YamlSerializerOptions { AcceptLenientScalars = true };
            if (bufferSize > 0)
            {
                options.DefaultBufferSize = bufferSize;
            }
            byte[] yaml = "A: NuLL\nB: tRuE\nC: -.iNf\nD:\n- NuLL\n- nuLL\n"u8.ToArray();
            Streamed s = (await YamlSerializer.DeserializeAsync<Streamed>(new MemoryStream(yaml), options))!;
            AssertStreamed(s);
            AssertStreamed(YamlSerializer.Deserialize<Streamed>(new MemoryStream(yaml), options)!);

            var strict = new YamlSerializerOptions { DefaultBufferSize = bufferSize > 0 ? bufferSize : 16 * 1024 };
            await Assert.ThrowsAnyAsync<YamlException>(async () => await YamlSerializer.DeserializeAsync<Streamed>(new MemoryStream(yaml), strict));
        }

        private static void AssertStreamed(Streamed s)
        {
            Assert.Null(s.A);
            Assert.True(s.B);
            Assert.Equal(double.NegativeInfinity, s.C);
            Assert.Equal(new int?[] { null, null }, s.D);
        }

        public class Streamed
        {
            public string? A { get; set; }
            public bool B { get; set; }
            public double C { get; set; }
            public List<int?>? D { get; set; }
        }

        [Fact]
        public void MultiSegment_EverySplit()
        {
            byte[] yaml = "A: NuLL\nB: tRuE\nC: -.iNf\nD: [NuLL, nULL]\nE: yEs\n"u8.ToArray();
            var options = new YamlReaderOptions { AcceptLenientScalars = true, Schema = YamlSchemaKind.Yaml11 };
            for (int split = 1; split < yaml.Length; split++)
            {
                var reader = new Utf8YamlReader(Segments(yaml, split), options);
                Segmented s = YamlSerializer.Deserialize<Segmented>(ref reader)!;
                Assert.Null(s.A);
                Assert.True(s.B);
                Assert.Equal(double.NegativeInfinity, s.C);
                Assert.Equal(new int?[] { null, null }, s.D);
                Assert.True(s.E);
            }

            // One byte per segment.
            var bytewise = new Utf8YamlReader(Segments(yaml, Enumerable.Range(1, yaml.Length - 1).ToArray()), options);
            Assert.True(YamlSerializer.Deserialize<Segmented>(ref bytewise)!.E);
        }

        public class Segmented
        {
            public string? A { get; set; }
            public bool B { get; set; }
            public double C { get; set; }
            public int?[]? D { get; set; }
            public bool E { get; set; }
        }

        private static ReadOnlySequence<byte> Segments(byte[] data, params int[] splits)
        {
            var bounds = new List<int> { 0 };
            bounds.AddRange(splits);
            bounds.Add(data.Length);
            Segment first = new(data.AsMemory(bounds[0], bounds[1] - bounds[0]), 0);
            Segment last = first;
            for (int i = 1; i + 1 < bounds.Count; i++)
            {
                last = last.Append(data.AsMemory(bounds[i], bounds[i + 1] - bounds[i]));
            }
            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
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

        [Fact]
        public void SourceGeneratedContext()
        {
            // The attribute has no member for it, as for M4's other options: it is set on the options instance.
            var context = new LenientScalarsContext(new YamlSerializerOptions { AcceptLenientScalars = true });
            Streamed s = YamlSerializer.Deserialize("A: NuLL\nB: tRuE\nC: -.iNf\nD: [NuLL]\n", context.Streamed)!;
            Assert.Null(s.A);
            Assert.True(s.B);
            Assert.Equal(double.NegativeInfinity, s.C);
            Assert.Equal(new int?[] { null }, s.D);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize("B: tRuE\n", LenientScalarsContext.Default.Streamed));
            Assert.Equal("NuLL", YamlSerializer.Deserialize("A: NuLL\n", LenientScalarsContext.Default.Streamed)!.A);
        }
    }

    [YamlSerializable(typeof(LenientScalarsTests.Streamed))]
    internal partial class LenientScalarsContext : YamlSerializerContext;
}
