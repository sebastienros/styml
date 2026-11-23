// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Yaml.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5b, decision 6: <see cref="ReferenceHandler.Preserve"/> anchors only repeated objects, after a
    /// first pass; <see cref="ReferenceHandler.PreserveAll"/> anchors every object. Both write YAML anchors and aliases in
    /// block style, and $id/$ref in the JSON-compatible style.
    /// </summary>
    public class SerializerPreserveTests
    {
        public class C { public int A { get; set; } }

        public class Three { public C X { get; set; } = null!; public C Y { get; set; } = null!; public C Z { get; set; } = null!; }

        public class Cyc { public string? Name { get; set; } public Cyc? Self { get; set; } }

        // Block style explicitly: leg YJ makes the JSON-compatible writer the default.
        private static readonly YamlSerializerOptions s_preserve = new() { ReferenceHandler = ReferenceHandler.Preserve, WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
        private static readonly YamlSerializerOptions s_all = new() { ReferenceHandler = ReferenceHandler.PreserveAll, WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        private static Three Sample()
        {
            var shared = new C { A = 1 };
            return new Three { X = shared, Y = shared, Z = new C { A = 3 } };
        }

        /// <summary>Every anchor name in <paramref name="yaml"/> is defined once.</summary>
        private static void AssertAnchorsUnique(string yaml)
        {
            string[] names = Regex.Matches(yaml, @"&(\S+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(names.Length, names.Distinct().Count());
        }

        [Fact]
        public void PreserveAnchorsRepeatedObjects_PreserveAllAnchorsEveryObject()
        {
            Assert.Equal("X: &1\n  A: 1\n\"Y\": *1\nZ:\n  A: 3\n", YamlSerializer.Serialize(Sample(), s_preserve));
            Assert.Equal("&1\nX: &2\n  A: 1\n\"Y\": *2\nZ: &3\n  A: 3\n", YamlSerializer.Serialize(Sample(), s_all));

            // Collections take the anchor directly, with no $values wrapper.
            var list = new List<int> { 1, 2 };
            Assert.Equal("a: &1\n- 1\n- 2\nb: *1\n", YamlSerializer.Serialize(new Dictionary<string, List<int>> { ["a"] = list, ["b"] = list }, s_preserve));

            // Both read back to the same shape.
            foreach (YamlSerializerOptions options in new[] { s_preserve, s_all })
            {
                Three back = YamlSerializer.Deserialize<Three>(YamlSerializer.Serialize(Sample(), options), options)!;
                Assert.Same(back.X, back.Y);
                Assert.NotSame(back.X, back.Z);
            }
        }

        [Fact]
        public void PolymorphicValuesKeepTheirDiscriminator()
        {
            // The block counterpart of the imported ReferencePreservation_*_Serialization tests: the discriminator is a
            // property of the anchored mapping, and the alias reads back as the same derived instance.
            var dog = new SerializerAliasTests.Dog { Name = "d" };
            string yaml = YamlSerializer.Serialize(new List<SerializerAliasTests.Animal> { dog, dog }, s_preserve);
            Assert.Equal("- &1\n  $type: dog\n  Name: d\n- *1\n", yaml);
            List<SerializerAliasTests.Animal> back = YamlSerializer.Deserialize<List<SerializerAliasTests.Animal>>(yaml, s_preserve)!;
            Assert.IsType<SerializerAliasTests.Dog>(back[0]);
            Assert.Same(back[0], back[1]);
        }

        [Fact]
        public void TheJsonCompatibleStyleKeepsSystemTextJsonMetadata()
        {
            foreach (ReferenceHandler handler in new[] { ReferenceHandler.Preserve, ReferenceHandler.PreserveAll })
            {
                var options = new YamlSerializerOptions { ReferenceHandler = handler, WriteStyle = YamlWriterStyle.JsonCompatible };
                Assert.Equal("{\"$id\":\"1\",\"X\":{\"$id\":\"2\",\"A\":1},\"Y\":{\"$ref\":\"2\"},\"Z\":{\"$id\":\"3\",\"A\":3}}", YamlSerializer.Serialize(Sample(), options));
            }
        }

        [Fact]
        public void Cycles()
        {
            var cyc = new Cyc { Name = "n" };
            cyc.Self = cyc;
            string yaml = YamlSerializer.Serialize(cyc, s_preserve);
            Assert.Equal("&1\nName: \"n\"\nSelf: *1\n", yaml);
            Cyc back = YamlSerializer.Deserialize<Cyc>(yaml, s_preserve)!;
            Assert.Same(back, back.Self);
        }

        [Fact]
        public void IgnoreCyclesReadsAnchoredCyclesAsRecursiveAliases()
        {
            // The by-design difference behind the jsonoutput tag of IgnoreCycles_DoesNotSupportPreserveSemantics: block
            // Preserve output has no $id/$ref to land in extension data, and its cycle needs Preserve to be read.
            var cyc = new Cyc { Name = "n" };
            cyc.Self = cyc;
            string yaml = YamlSerializer.Serialize(cyc, s_preserve);
            var ignoreCycles = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles };
            Assert.Contains("ReferenceHandler.Preserve", Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Cyc>(yaml, ignoreCycles)).Message);
        }

        [Fact]
        public void ACustomHandlerKeepsIdAndRef()
        {
            var options = new YamlSerializerOptions { ReferenceHandler = new ReferenceHandler<PreserveReferenceResolverForTests>(), WriteStyle = YamlWriterStyle.Block };
            string yaml = YamlSerializer.Serialize(Sample(), options);
            Assert.Contains("$ref", yaml);
            Assert.DoesNotContain("*", yaml);
            Three back = YamlSerializer.Deserialize<Three>(yaml, options)!;
            Assert.Same(back.X, back.Y);
        }

        public sealed class PreserveReferenceResolverForTests : ReferenceResolver
        {
            private readonly Dictionary<string, object> _byId = new();
            private readonly Dictionary<object, string> _byObject = new(ReferenceEqualityComparer.Instance);

            public override void AddReference(string referenceId, object value) => _byId[referenceId] = value;

            public override string GetReference(object value, out bool alreadyExists)
            {
                alreadyExists = _byObject.TryGetValue(value, out string? id);
                return id ?? (_byObject[value] = "r" + _byObject.Count);
            }

            public override object ResolveReference(string referenceId) => _byId[referenceId];
        }

        // The first pass's blind spots, each in the order that can fail: the object is met once where the
        // pass can see it, written, and met again where it cannot.

        public class WithAsync { public C First { get; set; } = null!; public IAsyncEnumerable<C> Items { get; set; } = null!; }

        private static async IAsyncEnumerable<C> Yield(C c)
        {
            await Task.Yield();
            yield return c;
        }

        [Fact]
        public async Task BlindSpot_AsyncEnumerableItem()
        {
            var shared = new C { A = 5 };
            var stream = new MemoryStream();
            await YamlSerializer.SerializeAsync(stream, new WithAsync { First = shared, Items = Yield(shared) }, s_preserve);
            string yaml = Encoding.UTF8.GetString(stream.ToArray());

            // The pass could not enter the items, so every object is anchored, and the item is an alias.
            Assert.Equal("&1\nFirst: &2\n  A: 5\nItems:\n- *2\n", yaml);
        }

        public class WithLazy { public C First { get; set; } = null!; public IEnumerable<C> Items { get; set; } = null!; }

        private static IEnumerable<C> Lazy(C c)
        {
            yield return c;
        }

        [Fact]
        public void BlindSpot_LazyEnumerable()
        {
            var shared = new C { A = 6 };
            string yaml = YamlSerializer.Serialize(new WithLazy { First = shared, Items = Lazy(shared) }, s_preserve);
            // The enumerable is an object too, so it is anchored with the rest.
            Assert.Equal("&1\nFirst: &2\n  A: 6\nItems: &3\n- *2\n", yaml);
            WithLazy back = YamlSerializer.Deserialize<WithLazy>(yaml, s_preserve)!;
            Assert.Same(back.First, back.Items.Single());
        }

        public class WithCustom { public C First { get; set; } = null!; public Boxed Inner { get; set; } = null!; }

        [YamlConverter(typeof(BoxedConverter))]
        public class Boxed { public C Value { get; set; } = null!; }

        private sealed class BoxedConverter : YamlConverter<Boxed>
        {
            public override Boxed Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
                new Boxed { Value = YamlSerializer.Deserialize<C>(ref reader, options)! };

            // A nested write: its own reference scope.
            public override void Write(Utf8YamlWriter writer, Boxed value, YamlSerializerOptions options) =>
                YamlSerializer.Serialize(writer, value.Value, options);
        }

        [Fact]
        public void BlindSpot_CustomConverter()
        {
            var shared = new C { A = 7 };
            var value = new WithCustom { First = shared, Inner = new Boxed { Value = shared } };

            // Preserve: the pass saw the object once; inside the converter it is written in full, in its own scope.
            Assert.Equal("First:\n  A: 7\nInner:\n  A: 7\n", YamlSerializer.Serialize(value, s_preserve));

            // PreserveAll: the nested scope anchors too, with names that never re-define an outer anchor.
            string all = YamlSerializer.Serialize(value, s_all);
            AssertAnchorsUnique(all);
            Assert.Equal("&1\nFirst: &2\n  A: 7\nInner: &3\n  A: 7\n", all);
        }

        public class Many { public List<C> Items { get; set; } = new(); }

        [Fact]
        public async Task AcrossAFlush()
        {
            var shared = new C { A = 8 };
            var many = new Many();
            for (int i = 0; i < 2000; i++)
            {
                many.Items.Add(i % 2 == 0 ? shared : new C { A = i });
            }

            var stream = new MemoryStream();
            await YamlSerializer.SerializeAsync(stream, many, new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve, DefaultBufferSize = 1, WriteStyle = YamlWriterStyle.Block });
            string yaml = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Single(Regex.Matches(yaml, "&1"));
            Assert.Equal(999, Regex.Matches(yaml, @"\*1\b").Count);
            Many back = YamlSerializer.Deserialize<Many>(yaml, s_preserve)!;
            Assert.All(back.Items.Where((_, i) => i % 2 == 0), item => Assert.Same(back.Items[0], item));
        }

        public class FreshGetter { public C Value => new C { A = 9 }; public C Again => Value; }

        [Fact]
        public void PreserveAllThroughSourceGeneration()
        {
            Assert.Same(ReferenceHandler.PreserveAll, PreserveAllContext.Default.Options.ReferenceHandler);
            Three value = Sample();
            Three back = YamlSerializer.Deserialize(YamlSerializer.Serialize(value, PreserveAllContext.Default.Three), PreserveAllContext.Default.Three)!;
            Assert.Same(back.X, back.Y);
        }

        [Fact]
        public void AGetterThatReturnsNewInstancesIsWrittenInFull()
        {
            Assert.Equal("Value:\n  A: 9\nAgain:\n  A: 9\n", YamlSerializer.Serialize(new FreshGetter(), s_preserve));
        }
    }

    [YamlSourceGenerationOptions(ReferenceHandler = YamlKnownReferenceHandler.PreserveAll)]
    [YamlSerializable(typeof(SerializerPreserveTests.Three))]
    internal partial class PreserveAllContext : YamlSerializerContext
    {
    }
}
