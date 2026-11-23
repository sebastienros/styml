// Copyright (c) Sébastien Ros. Licensed under the MIT license.

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
    /// M5b: the serializer reads anchors and aliases. An alias shares the
    /// instance a built-in object or collection converter read at the anchor; anything else replays the anchored node
    /// through the site's converter, counted against the operation's bound.
    /// </summary>
    public class SerializerAliasTests
    {
        public class C { public int A { get; set; } }

        public class Pair { public C X { get; set; } = null!; public C Y { get; set; } = null!; }

        public class Three { public C X { get; set; } = null!; public C Y { get; set; } = null!; public C Z { get; set; } = null!; }

        public record CtorPair(C X, C Y);

        public interface IShape { }

        public class Square : IShape { public int Side { get; set; } }

        public class Shapes { public List<int> L { get; set; } = null!; public IList<int> I { get; set; } = null!; }

        [YamlPolymorphic]
        [YamlDerivedType(typeof(Dog), "dog")]
        public class Animal { public string? Name { get; set; } }

        public class Dog : Animal { }

        public class Zoo { public Animal A { get; set; } = null!; public Animal B { get; set; } = null!; }

        public class Cyc { public string? Name { get; set; } public Cyc? Self { get; set; } }

        public record CycRecord(string Name, CycRecord? Self);

        public class Populated { public List<int> L { get; } = new() { 0 }; }

        public class PopulatedHolder { public List<int>? X { get; set; } public Populated? T { get; set; } }

        public class Tree { public List<Tree> Children { get; set; } = new(); }

        public class TreeHolder { public Tree? T { get; set; } }

        private static readonly YamlSerializerOptions s_populate = new() { PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate };

        private static T Stream<T>(string yaml, int bufferSize = 1, YamlSerializerOptions? options = null)
        {
            options = new YamlSerializerOptions(options ?? YamlSerializerOptions.Default) { DefaultBufferSize = bufferSize };
            return YamlSerializer.Deserialize<T>(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), options)!;
        }

        [Fact]
        public void CollectionsAndObjectsAreShared()
        {
            var lists = YamlSerializer.Deserialize<Dictionary<string, List<int>>>("a: &l [1, 2]\nb: *l\n")!;
            Assert.Same(lists["a"], lists["b"]);

            var pair = YamlSerializer.Deserialize<Pair>("X: &o {A: 1}\nY: *o\n")!;
            Assert.Same(pair.X, pair.Y);

            // An interface site resolved to a collection converter, and a polymorphic base.
            var shapes = YamlSerializer.Deserialize<Shapes>("L: &l [1]\nI: *l\n")!;
            Assert.Same(shapes.L, shapes.I);
            var zoo = YamlSerializer.Deserialize<Zoo>("A: &d {$type: dog, Name: n}\nB: *d\n")!;
            Assert.IsType<Dog>(zoo.A);
            Assert.Same(zoo.A, zoo.B);
        }

        [Fact]
        public void ScalarsReplayThroughTheSiteConverter()
        {
            Assert.Equal(new[] { 1, 1, 2 }, YamlSerializer.Deserialize<List<int>>("[&a 1, *a, 2]"));
            Assert.Equal(new[] { "1", "1" }, YamlSerializer.Deserialize<List<string>>("[&a 1, *a]"));

            // One anchored scalar read as an int at its anchor and as a string at the alias.
            var mixed = YamlSerializer.Deserialize<Dictionary<string, YamlElement>>("a: &x 7\nb: *x\n")!;
            Assert.Equal(7, mixed["b"].GetInt32());
            Assert.Equal(new KeyValuePair<int, string>(7, "7"), YamlSerializer.Deserialize<KeyValuePair<int, string>>("Key: &x 7\nValue: *x\n"));

            // Block scalars and multi-line strings replay whole.
            Assert.Equal(new[] { "a\nb\n", "a\nb\n" }, YamlSerializer.Deserialize<List<string>>("- &s |\n  a\n  b\n- *s\n"));
        }

        [Fact]
        public void ObjectSitesReadElements()
        {
            // An object member is a YamlElement whether or not it is an alias, so its type never depends on where the
            // anchor was read.
            var values = YamlSerializer.Deserialize<Dictionary<string, object>>("a: &x [1, 2]\nb: *x\nc: &y 7\nd: *y\n")!;
            Assert.All(values.Values, v => Assert.IsType<YamlElement>(v));
            Assert.Equal("[1, 2]", ((YamlElement)values["b"]).GetRawText());

            // An alias inside an object value to an anchor outside it is expanded into the value.
            var outside = YamlSerializer.Deserialize<Dictionary<string, object>>("a: &x [1, 2]\nb: [*x, *x]\n")!;
            YamlElement b = (YamlElement)outside["b"];
            Assert.Equal(2, b.GetArrayLength());
            Assert.Equal(2, b[1][1].GetInt32());
        }

        [Fact]
        public void AnchorsInSkippedValuesReplay()
        {
            // The compose-file pattern: an anchor in a member the type does not have.
            var pair = YamlSerializer.Deserialize<Pair>("x-base: &o {A: 3}\nX: *o\nY: *o\n")!;
            Assert.Equal(3, pair.X.A);

            // The first replay stores its instance, so later aliases share it.
            Assert.Same(pair.X, pair.Y);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(0)]
        public void StreamsGiveTheSameIdentityAsStrings(int bufferSize)
        {
            const string Yaml = "X: &o {A: 1}\nY: *o\nZ: *o\n";
            var fromString = YamlSerializer.Deserialize<Three>(Yaml)!;
            var fromStream = bufferSize == 0 ? YamlSerializer.Deserialize<Three>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)))! : Stream<Three>(Yaml, bufferSize);
            Assert.True(ReferenceEquals(fromString.X, fromString.Y) && ReferenceEquals(fromString.Y, fromString.Z));
            Assert.True(ReferenceEquals(fromStream.X, fromStream.Y) && ReferenceEquals(fromStream.Y, fromStream.Z));

            // A parameterized constructor re-reads its arguments from saved states; a polymorphic member reads ahead.
            const string Ctor = "X: &o {A: 2}\nY: *o\n";
            var ctorString = YamlSerializer.Deserialize<CtorPair>(Ctor)!;
            var ctorStream = Stream<CtorPair>(Ctor, Math.Max(bufferSize, 1));
            Assert.Same(ctorString.X, ctorString.Y);
            Assert.Same(ctorStream.X, ctorStream.Y);

            const string Poly = "A: &d {Name: n, $type: dog}\nB: *d\n";
            var options = new YamlSerializerOptions { AllowOutOfOrderMetadataProperties = true };
            var zooString = YamlSerializer.Deserialize<Zoo>(Poly, options)!;
            var zooStream = Stream<Zoo>(Poly, Math.Max(bufferSize, 1), options);
            Assert.Same(zooString.A, zooString.B);
            Assert.Same(zooStream.A, zooStream.B);
            Assert.IsType<Dog>(zooStream.B);

            // Multi-segment input and a PipeReader.
            byte[] utf8 = Encoding.UTF8.GetBytes(Yaml);
            var reader = new Utf8YamlReader(SequenceFactory.Uniform(utf8, size: Math.Max(bufferSize, 1)).Value);
            var fromSequence = YamlSerializer.Deserialize<Three>(ref reader)!;
            Assert.True(ReferenceEquals(fromSequence.X, fromSequence.Y) && ReferenceEquals(fromSequence.Y, fromSequence.Z));
            var pipe = new System.IO.Pipelines.Pipe();
            pipe.Writer.WriteAsync(utf8).AsTask().GetAwaiter().GetResult();
            pipe.Writer.Complete();
            var fromPipe = YamlSerializer.DeserializeAsync<Three>(pipe.Reader).AsTask().GetAwaiter().GetResult()!;
            Assert.True(ReferenceEquals(fromPipe.X, fromPipe.Y) && ReferenceEquals(fromPipe.Y, fromPipe.Z));

            // Scalars across buffer boundaries.
            Assert.Equal(new Dictionary<string, int> { ["a"] = 7, ["b"] = 1, ["c"] = 7 }, Stream<Dictionary<string, int>>("a: &x 7\nb: 1\nc: *x\n", Math.Max(bufferSize, 1)));
        }

        [Fact]
        public void AliasKeys()
        {
            // An alias key repeats its anchored key: a duplicate under the default Error (M5c), the last value under LastWins.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("&k a: 1\n*k : 2\n"));
            Assert.Equal(new Dictionary<string, int> { ["a"] = 2 }, YamlSerializer.Deserialize<Dictionary<string, int>>("&k a: 1\n*k : 2\n", new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }));
            Assert.Equal(new Dictionary<int, string> { [1] = "5", [5] = "y" }, YamlSerializer.Deserialize<Dictionary<int, string>>("1: &k 5\n*k : y\n"));

            // An alias to an anchored key reads the key's bytes as a value.
            Assert.Equal("key", YamlSerializer.Deserialize<Dictionary<string, string>>("&k key: v\nother: *k\n")!["other"]);

            // An alias to a collection is a collection key (M5e), which a string key refuses.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("a: &c [1]\n*c : 2\n"));
        }

        [Fact]
        public void RecursiveAliases()
        {
            // No handler: a cycle is refused, and the message names ReferenceHandler.Preserve.
            YamlException e = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Cyc>("&a {Name: n, Self: *a}"));
            Assert.Contains("ReferenceHandler.Preserve", e.Message);

            // Preserve and PreserveAll: the cycle.
            foreach (ReferenceHandler handler in new[] { ReferenceHandler.Preserve, ReferenceHandler.PreserveAll })
            {
                var cyc = YamlSerializer.Deserialize<Cyc>("&a {Name: n, Self: *a}", new YamlSerializerOptions { ReferenceHandler = handler })!;
                Assert.Same(cyc, cyc.Self);
                var tree = YamlSerializer.Deserialize<Tree>("&n {Children: [*n]}", new YamlSerializerOptions { ReferenceHandler = handler })!;
                Assert.Same(tree, tree.Children[0]);

                // With Preserve, an object item takes the anchored instance, as System.Text.Json's $ref does: the list
                // holds itself. With no handler it would read a YamlElement, which cannot contain itself.
                List<object> self = YamlSerializer.Deserialize<List<object>>("&l [1, *l]", new YamlSerializerOptions { ReferenceHandler = handler })!;
                Assert.Same(self, self[1]);
            }

            // No instance before the content: a parameterized constructor, an array.
            var preserve = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            Assert.DoesNotContain("ReferenceHandler.Preserve", Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<CycRecord>("&a {Name: n, Self: *a}", preserve)).Message);
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<List<int[]>>("- &a [1, *a]", preserve));

            // A recursive alias in a replayed node (here skipped first) is caught as recursive, not by depth.
            const string Skipped = "x-skipped: &a {Children: [*a]}\nT: *a\n";
            Assert.Contains("inside the node", Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<TreeHolder>(Skipped)).Message);
            Tree replayed = YamlSerializer.Deserialize<TreeHolder>(Skipped, preserve)!.T!;
            Assert.Same(replayed, replayed.Children[0]);

            // An object member inside the node it refers to, with no handler.
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>("&a {x: [*a]}"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<List<object>>("&l [1, *l]"));
        }

        [Fact]
        public void RecursionThroughACustomConverterIsRecursiveNotDepth()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new NestedCConverter());
            YamlException e = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Holder>("x: &a {A: 1, Self: *a}\nC: *a\n", options));
            Assert.Contains("inside the node", e.Message);
        }

        public class Holder { public CWithSelf? C { get; set; } }

        public class CWithSelf { public int A { get; set; } public CWithSelf? Self { get; set; } }

        /// <summary>A converter that reads the value through a nested Deserialize, as the converter documentation says to.</summary>
        private sealed class NestedCConverter : YamlConverter<CWithSelf>
        {
            public override CWithSelf? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                var copy = new YamlSerializerOptions(options);
                copy.Converters.Clear();
                return YamlSerializer.Deserialize<CWithSelf>(ref reader, copy);
            }

            public override void Write(Utf8YamlWriter writer, CWithSelf value, YamlSerializerOptions options) => throw new NotSupportedException();
        }

        [Fact]
        public void ACustomConverterResolvesAliasesThroughANestedDeserialize()
        {
            var options = new YamlSerializerOptions();
            options.Converters.Add(new NestedCConverter());
            var holder = YamlSerializer.Deserialize<Holder>("x: &a {A: 4}\nC: *a\n", options)!;
            Assert.Equal(4, holder.C!.A);
        }

        [Fact]
        public void UnknownAnchors()
        {
            Assert.Contains("'*nope'", Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<List<int>>("[*nope]")).Message);

            // Deserialize(ref reader) in the middle of a document gets a new table: an anchor before the value is unknown.
            // On "a: &x 1\nb: *x": the value of a (after 2 tokens) reads; the value of b (after 4) does not.
            byte[] utf8 = Encoding.UTF8.GetBytes("a: &x 1\nb: *x\n");
            Assert.Equal(1, DeserializeAtReader(utf8, skipTokens: 2));
            Assert.Contains("'*x'", Assert.ThrowsAny<YamlException>(() => DeserializeAtReader(utf8, skipTokens: 4)).Message);
        }

        private static int DeserializeAtReader(byte[] utf8, int skipTokens)
        {
            var reader = new Utf8YamlReader(utf8);
            for (int i = 0; i < skipTokens; i++)
            {
                reader.Read();
            }

            return YamlSerializer.Deserialize<int>(ref reader);
        }

        [Fact]
        public async Task AnAliasToAnEarlierDocumentIsUnknown()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("--- &a 1\n--- *a\n"));
            YamlException e = await Assert.ThrowsAnyAsync<YamlException>(async () =>
            {
                await foreach (int item in YamlSerializer.DeserializeAsyncEnumerable<int>(stream, topLevelValues: true))
                {
                }
            });
            Assert.Contains("'*a'", e.Message);
        }

        [Fact]
        public void PopulateSitesReplayIntoTheExistingInstance()
        {
            var populated = YamlSerializer.Deserialize<Dictionary<string, Populated>>("a: &p {L: [1]}\nb: *p\n", s_populate)!;
            Assert.Equal(new[] { 0, 1 }, populated["a"].L);
            Assert.Same(populated["a"], populated["b"]);

            // A Populate member aliased to a list: the list's items go into the member's own instance.
            var target = YamlSerializer.Deserialize<PopulatedHolder>("X: &l [5]\nT: {L: *l}\n", s_populate)!;
            Assert.Equal(new[] { 0, 5 }, target.T!.L);
            Assert.Equal(new[] { 5 }, target.X);
        }

        [Fact]
        public void MetadataValuesAreScalars()
        {
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Zoo>("x: &t dog\nA: {$type: *t}\nB: {}\n"));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Pair>("x: &i '1'\nX: {$id: *i, A: 1}\nY: {$ref: '1'}\n", options));
        }

        [Fact]
        public void DeserializeElement_ReadsAnchorsInsideAndOutside()
        {
            // An inconsistency of M5a, closed: an anchor inside the element now reads too.
            using YamlDocument document = YamlDocument.Parse("x: &x 7\nout: [*x, 2]\ninside: [&y 1, *y]\n");
            Assert.Equal(new[] { 7, 2 }, document.RootElement.GetProperty("out").Deserialize<int[]>());
            Assert.Equal(new[] { 1, 1 }, document.RootElement.GetProperty("inside").Deserialize<int[]>());
        }

        [Fact]
        public void AChainOfReplaysMeetsTheDepthLimit()
        {
            // a0: &a0 [x], a1: &a1 [*a0], ...: each object value expands the chain below it, one level deeper each time.
            var chain = new StringBuilder("a0: &a0 [x]\n");
            for (int i = 1; i < 100; i++)
            {
                chain.Append($"a{i}: &a{i} [*a{i - 1}]\n");
            }

            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>(chain.ToString()));
        }

        [Fact]
        public void TheBound()
        {
            // The libraries' bomb: 6 levels of 9 aliases over a 9-item list.
            string bomb = Bomb();

            // Object members are YamlElements, so every level is expanded, until the bound.
            YamlException e = Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, object>>(bomb));
            Assert.Contains("MaxAliasExpansion", e.Message);

            // Typed lists share: 7 distinct lists, and the 9^7 leaves are reachable.
            BombLists lists = YamlSerializer.Deserialize<BombLists>(bomb)!;
            var distinct = new HashSet<object>(ReferenceEqualityComparer.Instance);
            long leaves = Walk(lists.a6, distinct);
            Assert.Equal(4_782_969, leaves);
            Assert.Equal(7, distinct.Count);
            Assert.Same(lists.a5, lists.a6[8]);

            // An explicit limit counts each scalar replay as one node.
            var tight = new YamlSerializerOptions { MaxAliasExpansion = 2 };
            Assert.Equal(new[] { 1, 1, 1 }, YamlSerializer.Deserialize<List<int>>("[&a 1, *a, *a]", tight));
            Assert.ThrowsAny<YamlException>(() => YamlSerializer.Deserialize<List<int>>("[&a 1, *a, *a, *a]", tight));

            // The budget is per operation: one options instance reads the same document many times.
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(new[] { 1, 1, 1 }, YamlSerializer.Deserialize<List<int>>("[&a 1, *a, *a]", tight));
            }
        }

        internal static string Bomb()
        {
            var bomb = new StringBuilder("a0: &a0 [x, x, x, x, x, x, x, x, x]\n");
            for (int i = 1; i <= 6; i++)
            {
                bomb.Append($"a{i}: &a{i} [" + string.Join(", ", Enumerable.Repeat($"*a{i - 1}", 9)) + "]\n");
            }

            return bomb.ToString();
        }

        private static long Walk(object node, HashSet<object> distinct)
        {
            if (node is not System.Collections.IList list || node is string)
            {
                return 1;
            }

            distinct.Add(list);
            long leaves = 0;
            foreach (object item in list)
            {
                leaves += Walk(item, distinct);
            }

            return leaves;
        }

        public class BombLists
        {
            public List<string> a0 { get; set; } = null!;
            public List<List<string>> a1 { get; set; } = null!;
            public List<List<List<string>>> a2 { get; set; } = null!;
            public List<List<List<List<string>>>> a3 { get; set; } = null!;
            public List<List<List<List<List<string>>>>> a4 { get; set; } = null!;
            public List<List<List<List<List<List<string>>>>>> a5 { get; set; } = null!;
            public List<List<List<List<List<List<List<string>>>>>>> a6 { get; set; } = null!;
        }
    }
}
