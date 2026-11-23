// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Exit criterion 6: the serializer over block YAML, from a string and from a Stream: a class, a record with a
    /// parameterized constructor, and a custom converter that calls YamlSerializer.Deserialize&lt;T&gt;(ref reader)
    /// (the readers scoped to one value).
    /// </summary>
    public class SerializerTests
    {
        public class Address
        {
            public string? City { get; set; }
            public int Zip { get; set; }
        }

        public class Person
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public string? Version { get; set; }
            public bool Active { get; set; }
            public double Score { get; set; }
            public List<string>? Tags { get; set; }
            public Address? Home { get; set; }
            public Dictionary<string, int>? Counts { get; set; }
            public string? Note { get; set; }
            public string? Summary { get; set; }
            public string? Missing { get; set; }
        }

        public record Point(int X, int Y, string Label);

        public record Shape(string Name, List<Point> Points, Address? Origin);

        private const string PersonYaml = """
            # A person, in block style
            Name: Ada Lovelace
            Age: 36
            Version: 1.10            # a plain Number read as text (D2)
            Active: True
            Score: .5
            Tags:
              - math
              - 'engines'
              - "poetry\tscience"
            Home:
              City: London
              Zip: 0x3039
            Counts: {a: 1, b: 2}
            Note: |
              first line
                indented
              last line
            Summary: >-
              folded
              text
            Missing:
            """;

        private static void AssertPerson(Person? p)
        {
            Assert.NotNull(p);
            Assert.Equal("Ada Lovelace", p!.Name);
            Assert.Equal(36, p.Age);
            Assert.Equal("1.10", p.Version);
            Assert.True(p.Active);
            Assert.Equal(0.5, p.Score);
            Assert.Equal(new[] { "math", "engines", "poetry\tscience" }, p.Tags);
            Assert.Equal("London", p.Home!.City);
            Assert.Equal(12345, p.Home.Zip);
            Assert.Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, p.Counts);
            Assert.Equal("first line\n  indented\nlast line\n", p.Note);
            Assert.Equal("folded text", p.Summary);
            Assert.Null(p.Missing);
        }

        public static IEnumerable<object[]> BufferSizes() => new[] { 1, 2, 7, 16, 4096 }.Select(n => new object[] { n });

        [Fact]
        public void Class_FromString() => AssertPerson(YamlSerializer.Deserialize<Person>(PersonYaml));

        [Fact]
        public void Class_FromUtf8() => AssertPerson(YamlSerializer.Deserialize<Person>(Encoding.UTF8.GetBytes(PersonYaml)));

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public void Class_FromStream(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertPerson(YamlSerializer.Deserialize<Person>(new MemoryStream(Encoding.UTF8.GetBytes(PersonYaml)), options));
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task Class_FromStreamAsync(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertPerson(await YamlSerializer.DeserializeAsync<Person>(new MemoryStream(Encoding.UTF8.GetBytes(PersonYaml)), options));
        }

        private const string ShapeYaml = """
            Name: triangle
            Points:
            - X: 1
              Y: 2
              Label: a
            - {X: 3, Y: 4, Label: "b"}
            - Label: >
                folded
                label
              Y: 6
              X: 0x5
            Origin:
              City: Paris
              Zip: 75000
            """;

        private static void AssertShape(Shape? shape)
        {
            Assert.NotNull(shape);
            Assert.Equal("triangle", shape!.Name);
            Assert.Equal(new[] { new Point(1, 2, "a"), new Point(3, 4, "b"), new Point(5, 6, "folded label\n") }, shape.Points);
            Assert.Equal("Paris", shape.Origin!.City);
        }

        [Fact]
        public void Record_FromString() => AssertShape(YamlSerializer.Deserialize<Shape>(ShapeYaml));

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public void Record_FromStream(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertShape(YamlSerializer.Deserialize<Shape>(new MemoryStream(Encoding.UTF8.GetBytes(ShapeYaml)), options));
        }

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public async Task Record_FromStreamAsync(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertShape(await YamlSerializer.DeserializeAsync<Shape>(new MemoryStream(Encoding.UTF8.GetBytes(ShapeYaml)), options));
        }

        /// <summary>Constructor-bound, plus settable members that appear before the constructor arguments.</summary>
        public record Mixed(string Name, int Count)
        {
            public Address? Home { get; set; }
            public List<int>? Numbers { get; set; }
            public string? Text { get; set; }
            public Dictionary<string, List<string>>? Groups { get; set; }
        }

        // The converter records CurrentState and BytesConsumed at each non-constructor member and re-reads it with a
        // new reader over the rest of the buffer once the constructor has run.
        private const string MixedYaml = """
            Home:
              City: Oslo
              Zip: 150
            Numbers:
              - 1
              - 0x2
            Text: |
              line one
                line two
            Groups:
              a:
              - x
              - 'y'
              b: [z]
            Name: mixed
            Count: 3
            """;

        private static void AssertMixed(Mixed? m)
        {
            Assert.NotNull(m);
            Assert.Equal("mixed", m!.Name);
            Assert.Equal(3, m.Count);
            Assert.Equal("Oslo", m.Home!.City);
            Assert.Equal(150, m.Home.Zip);
            Assert.Equal(new[] { 1, 2 }, m.Numbers);
            Assert.Equal("line one\n  line two\n", m.Text);
            Assert.Equal(new[] { "x", "y" }, m.Groups!["a"]);
            Assert.Equal(new[] { "z" }, m.Groups["b"]);
        }

        [Fact]
        public void RecordWithMembersBeforeConstructorArguments_FromString() => AssertMixed(YamlSerializer.Deserialize<Mixed>(MixedYaml));

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public void RecordWithMembersBeforeConstructorArguments_FromStream(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertMixed(YamlSerializer.Deserialize<Mixed>(new MemoryStream(Encoding.UTF8.GetBytes(MixedYaml)), options));
        }

        [Fact]
        public async Task RecordWithMembersBeforeConstructorArguments_FromStreamAsync()
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = 1 };
            AssertMixed(await YamlSerializer.DeserializeAsync<Mixed>(new MemoryStream(Encoding.UTF8.GetBytes(MixedYaml)), options));
        }

        [Theory]
        [InlineData(4096)]
        [InlineData(1)]
        public void RecordWithMembersBeforeConstructorArguments_ErrorPosition(int bufferSize)
        {
            // A type mismatch in a member that is replayed after the constructor still reports its own position.
            const string Yaml = "Home:\n  City: Oslo\n  Zip: north\nName: n\nCount: 1\n";
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            YamlException e = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Mixed>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)), options));
            Assert.Equal("$.Home.Zip", e.Path);
            Assert.Equal(2, e.LineNumber);
            Assert.Equal(12, e.BytePositionInLine);
        }

        public class Holder<T>
        {
            public T? Value { get; set; }
        }

        /// <summary>Reads its value with YamlSerializer.Deserialize&lt;T&gt;(ref reader).</summary>
        public class HolderConverter<T> : YamlConverter<Holder<T>>
        {
            public override Holder<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => new Holder<T> { Value = YamlSerializer.Deserialize<T>(ref reader, options) };

            public override void Write(Utf8YamlWriter writer, Holder<T> value, YamlSerializerOptions options) => throw new NotSupportedException();
        }

        public class Scoped
        {
            [YamlConverter(typeof(HolderConverter<Address>))] public Holder<Address>? Mapping { get; set; }
            [YamlConverter(typeof(HolderConverter<Address>))] public Holder<Address>? AnchoredMapping { get; set; }
            [YamlConverter(typeof(HolderConverter<List<int>>))] public Holder<List<int>>? Sequence { get; set; }
            [YamlConverter(typeof(HolderConverter<List<int>>))] public Holder<List<int>>? IndentedSequence { get; set; }
            [YamlConverter(typeof(HolderConverter<List<Address>>))] public Holder<List<Address>>? SequenceOfMappings { get; set; }
            [YamlConverter(typeof(HolderConverter<Point>))] public Holder<Point>? Record { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? Plain { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? MultiLinePlain { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? SingleQuoted { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? DoubleQuoted { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? Literal { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? Folded { get; set; }
            [YamlConverter(typeof(HolderConverter<string>))] public Holder<string>? Empty { get; set; }
            [YamlConverter(typeof(HolderConverter<int>))] public Holder<int>? Number { get; set; }
            public string? After { get; set; }
        }

        private const string ScopedYaml = """
            Mapping:
              City: Paris
              Zip: 75000
            AnchoredMapping: &home
              City: Rome
              Zip: 100
            Sequence:
            - 1
            - 2
            IndentedSequence:
              - 3
              - 4
            SequenceOfMappings:
            - City: A
              Zip: 1
            - City: B
              Zip: 2
            Record:
              X: 7
              Y: 8
              Label: r
            Plain: plain text
            MultiLinePlain: multi
              line
            SingleQuoted: 'it''s'
            DoubleQuoted: "a\tb\
              c"
            Literal: |2
                two
              one
            Folded: >-
              a
              b
            Empty:
            Number: 0x1F
            After: end
            """;

        private static void AssertScoped(Scoped? s)
        {
            Assert.NotNull(s);
            Assert.Equal("Paris", s!.Mapping!.Value!.City);
            Assert.Equal(75000, s.Mapping.Value.Zip);
            Assert.Equal("Rome", s.AnchoredMapping!.Value!.City);
            Assert.Equal(new[] { 1, 2 }, s.Sequence!.Value);
            Assert.Equal(new[] { 3, 4 }, s.IndentedSequence!.Value);
            Assert.Equal(new[] { "A", "B" }, s.SequenceOfMappings!.Value!.Select(a => a.City));
            Assert.Equal(new Point(7, 8, "r"), s.Record!.Value);
            Assert.Equal("plain text", s.Plain!.Value);
            Assert.Equal("multi line", s.MultiLinePlain!.Value);
            Assert.Equal("it's", s.SingleQuoted!.Value);
            Assert.Equal("a\tbc", s.DoubleQuoted!.Value);
            Assert.Equal("  two\none\n", s.Literal!.Value);
            Assert.Equal("a b", s.Folded!.Value);
            Assert.Null(s.Empty); // a null token skips converters that do not set HandleNull, as in STJ
            Assert.Equal(31, s.Number!.Value);
            Assert.Equal("end", s.After);
        }

        [Fact]
        public void CustomConverter_ScopedReaders_FromString() => AssertScoped(YamlSerializer.Deserialize<Scoped>(ScopedYaml));

        [Theory]
        [MemberData(nameof(BufferSizes))]
        public void CustomConverter_ScopedReaders_FromStream(int bufferSize)
        {
            var options = new YamlSerializerOptions { DefaultBufferSize = bufferSize };
            AssertScoped(YamlSerializer.Deserialize<Scoped>(new MemoryStream(Encoding.UTF8.GetBytes(ScopedYaml)), options));
        }

        [Fact]
        public void CustomConverter_OnSequenceEntry_CompactMapping()
        {
            // A reader scoped to a compact mapping that starts after '- ' (not at a line start).
            var list = YamlSerializer.Deserialize<List<Holder<Address>>>("- City: X\n  Zip: 1\n- City: Y\n  Zip: 2\n",
                new YamlSerializerOptions { Converters = { new HolderConverter<Address>() } });
            Assert.Equal(new[] { "X", "Y" }, list!.Select(h => h.Value!.City));
        }

        [Fact]
        public void SourceGeneratedContext_ReadsBlockYaml()
        {
            Person? p = YamlSerializer.Deserialize(PersonYaml, SerializerTestsContext.Default.Person);
            AssertPerson(p);
            AssertShape(YamlSerializer.Deserialize(ShapeYaml, SerializerTestsContext.Default.Shape));
        }

        [Fact]
        public void EmptyInput_ReadsAsNull()
        {
            // Decision 2 (M5c): the reader yields no token (D4), and the serializer reads zero tokens as a null.
            Assert.Null(YamlSerializer.Deserialize<Person>(""));
            Assert.Null(YamlSerializer.Deserialize<Person>("# only a comment\n"));
            Assert.Null(YamlSerializer.Deserialize<Person>(new MemoryStream()));
        }

        [Fact]
        public void Alias_Throws()
        {
            // Decision D5: aliases are read as Alias tokens, which the serializer rejects until M5.
            Assert.ThrowsAny<Exception>(() => YamlSerializer.Deserialize<Address>("City: &c Paris\nZip: *c\n"));
            Assert.ThrowsAny<Exception>(() => YamlSerializer.Deserialize<Address>("City: *c\n"));
        }

        [Fact]
        public void QuotedNumber_NeedsAllowReadingFromString()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Address>("Zip: \"12\"\n"));
            var options = new YamlSerializerOptions { NumberHandling = YamlNumberHandling.AllowReadingFromString };
            Assert.Equal(12, YamlSerializer.Deserialize<Address>("Zip: \"12\"\n", options)!.Zip);
        }

        [Fact]
        public void MultipleDocuments_NeedAllowMultipleValues()
        {
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Address>("City: A\n---\nCity: B\n"));
            Assert.Equal("A", YamlSerializer.Deserialize<Address>("--- \nCity: A\n...\n")!.City);
        }
    }

    [YamlSerializable(typeof(SerializerTests.Person))]
    [YamlSerializable(typeof(SerializerTests.Shape))]
    internal partial class SerializerTestsContext : YamlSerializerContext;
}
