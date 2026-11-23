// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Case-insensitive booleans in typed reads, behind
    /// <see cref="YamlReaderOptions.AcceptLenientScalars"/>, since non-standard behaviour is
    /// opt-in: with the option, typed boolean reads accept a plain, untagged
    /// scalar whose text is true or false in any case (<c>tRue</c>), as YamlDotNet and SharpYaml do with default options.
    /// Without it, <c>tRue</c> into a <see cref="bool"/> throws, as a quoted string does. The token stays a String under
    /// the core schema either way, so untyped reads see a string.
    /// </summary>
    public class CaseInsensitiveBooleanTests
    {
        public class Flags
        {
            public bool A { get; set; }
            public bool? B { get; set; }
        }

        public record FlagsRecord(bool A, bool? B);

        private static readonly YamlSerializerOptions s_lenient = new() { AcceptLenientScalars = true };

        private static Utf8YamlReader ValueReader(string scalar, bool lenient = true)
        {
            var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes("a: " + scalar + "\n"), new YamlReaderOptions { AcceptLenientScalars = lenient });
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            Assert.True(reader.Read());
            return reader;
        }

        [Theory]
        [InlineData("tRue", true)]
        [InlineData("TrUe", true)]
        [InlineData("tRUE", true)]
        [InlineData("fALSE", false)]
        [InlineData("FaLsE", false)]
        public void ReaderTypedRead(string scalar, bool expected)
        {
            Utf8YamlReader reader = ValueReader(scalar);
            // The token model is unchanged: under the core schema this is a string.
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.Equal(expected, reader.GetBoolean());
            Assert.Equal(scalar, reader.GetString());
        }

        [Theory]
        [InlineData("tRue")]
        [InlineData("fALSE")]
        public void OffByDefault(string scalar)
        {
            Utf8YamlReader reader = ValueReader(scalar, lenient: false);
            Assert.Equal(YamlTokenType.String, reader.TokenType);
            Assert.Throws<InvalidOperationException>(() => ValueReader(scalar, lenient: false).GetBoolean());
            // As a quoted string into a bool does.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("A: " + scalar + "\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("A: '" + scalar + "'\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("B: " + scalar + "\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize("A: " + scalar + "\n", CaseInsensitiveBooleanContext.Default.Flags));
        }

        [Theory]
        [InlineData("tRuee")]
        [InlineData("t rue")]
        [InlineData("yEs")]
        [InlineData("'tRuee'")]
        [InlineData("'t rue'")]
        public void OtherScalarsDoNotQualify(string scalar)
        {
            Assert.Throws<InvalidOperationException>(() => ValueReader(scalar).GetBoolean());
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("A: " + scalar + "\n", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("A: " + scalar + "\n"));
        }

        /// <summary>
        /// With the option, a quoted, tagged or
        /// block scalar whose text is the plain scalar of a boolean reads too, as both libraries read it. Without the
        /// option it throws, as before. Before M4b these cases asserted the throw with the option on as well.
        /// </summary>
        [Theory]
        [InlineData("'tRue'")]
        [InlineData("\"tRue\"")]
        [InlineData("!!str tRue")]
        [InlineData("!local tRue")]
        [InlineData("|\n  tRue")]
        [InlineData("' true '")]
        public void QuotedTaggedAndBlockScalars_WithTheOption(string scalar)
        {
            Assert.True(ValueReader(scalar).GetBoolean());
            Assert.True(YamlSerializer.Deserialize<Flags>("A: " + scalar + "\n", s_lenient)!.A);
            Assert.Throws<InvalidOperationException>(() => ValueReader(scalar, lenient: false).GetBoolean());
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Flags>("A: " + scalar + "\n"));
        }

        [Fact]
        public void Serializer()
        {
            Flags f = YamlSerializer.Deserialize<Flags>("A: tRue\nB: FALSE\n", s_lenient)!;
            Assert.True(f.A);
            Assert.False(f.B);
            FlagsRecord r = YamlSerializer.Deserialize<FlagsRecord>("B: TrUe\nA: tRUE\n", s_lenient)!;
            Assert.Equal((true, (bool?)true), (r.A, r.B));
            Assert.Equal(new[] { true, false }, YamlSerializer.Deserialize<bool[]>("- tRue\n- fAlse\n", s_lenient));
            Assert.Equal(new[] { true, false }, YamlSerializer.Deserialize<bool[]>("[tRue, fAlse]", s_lenient));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<FlagsRecord>("B: TrUe\nA: tRUE\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<bool[]>("[tRue, fAlse]"));
        }

        [Fact]
        public void SourceGeneratedContext()
        {
            var context = new CaseInsensitiveBooleanContext(new YamlSerializerOptions { AcceptLenientScalars = true });
            Flags f = YamlSerializer.Deserialize("A: tRue\nB: fALSE\n", context.Flags)!;
            Assert.True(f.A);
            Assert.False(f.B);
        }

        [Fact]
        public void DictionaryKeys()
        {
            // A plain key reads as a value would, so tRue: needs the option; the core
            // schema's casings and every quoted key (System.Text.Json's any-case rule for a property name) read without it.
            // Before M4b tRue: read by default.
            // 'tRue' repeats the key true by construction, a duplicate under the default Error (M5c): read with LastWins.
            foreach (YamlSerializerOptions options in new[] { new YamlSerializerOptions(), new YamlSerializerOptions(s_lenient) })
            {
                options.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
                Dictionary<bool, int> d = YamlSerializer.Deserialize<Dictionary<bool, int>>("True: 1\nFALSE: 2\n'tRue': 3\n", options)!;
                Assert.Equal(3, d[true]);
                Assert.Equal(2, d[false]);
            }
            Assert.Equal(1, YamlSerializer.Deserialize<Dictionary<bool, int>>("tRue: 1\n", s_lenient)![true]);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<bool, int>>("tRue: 1\n"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        public async Task Stream(int bufferSize)
        {
            var options = bufferSize == 0 ? new YamlSerializerOptions { AcceptLenientScalars = true } : new YamlSerializerOptions { DefaultBufferSize = bufferSize, AcceptLenientScalars = true };
            byte[] yaml = "A: tRue\nB: FaLsE\n"u8.ToArray();
            Flags f = (await YamlSerializer.DeserializeAsync<Flags>(new MemoryStream(yaml), options))!;
            Assert.True(f.A);
            Assert.False(f.B);
            f = YamlSerializer.Deserialize<Flags>(new MemoryStream(yaml), options)!;
            Assert.True(f.A);
        }

        [Fact]
        public void UntypedReadsAreUnchanged()
        {
            // The reader still reports a String token (ReaderTypedRead); untyped reads (the DOM since M5a) keep it a
            // string, with or without the option.
            Assert.Equal(YamlValueKind.String, YamlDocument.Parse("[tRue]").RootElement[0].ValueKind);
            Assert.Equal("tRue", (string)YamlNode.Parse("[tRue]")![0]!);
            Assert.Equal(YamlValueKind.String, ((YamlElement)((System.Collections.Generic.List<object?>)YamlSerializer.Deserialize<System.Collections.Generic.List<object?>>("[tRue]")!)[0]!).ValueKind);
            Assert.Equal(YamlValueKind.String, ((YamlElement)((System.Collections.Generic.List<object?>)YamlSerializer.Deserialize<System.Collections.Generic.List<object?>>("[tRue]", s_lenient)!)[0]!).ValueKind);
            Assert.Equal(YamlValueKind.True, YamlDocument.Parse("[true]").RootElement[0].ValueKind);
        }
    }

    [YamlSerializable(typeof(CaseInsensitiveBooleanTests.Flags))]
    internal partial class CaseInsensitiveBooleanContext : YamlSerializerContext;
}
