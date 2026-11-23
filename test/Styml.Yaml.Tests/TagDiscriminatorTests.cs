// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M5d: tags as type discriminators
    /// (<see cref="YamlTypeDiscriminatorStyle"/>, <see cref="YamlDerivedTypeAttribute.Tag"/>) and
    /// <see cref="YamlSerializerOptions.TagMappings"/>. Rows are named after M5d's questions (A1-A10), its combinations
    /// and its review's nine additions (R1-R9).
    /// </summary>
    public class TagDiscriminatorTests
    {
        public class Animal
        {
            public string? Name { get; set; }
        }

        public class Dog : Animal
        {
            public int Bark { get; set; }
        }

        public class Cat : Animal
        {
            public bool Purrs { get; set; }
        }

        public class Other
        {
            public int X { get; set; }
        }

        public class Holder
        {
            public object? Value { get; set; }
        }

        public class AnimalHolder
        {
            public Animal? Pet { get; set; }
        }

        public class DogHolder
        {
            public Dog? Pet { get; set; }
        }

        public class Shelter
        {
            public List<Animal> Pets { get; set; } = new();
        }

        public class Numbers : List<int>
        {
        }

        public class MoreNumbers : Numbers
        {
        }

        public class NumbersHolder
        {
            public Numbers? List { get; set; }
        }

        public class PositionalDog : Animal
        {
            public PositionalDog(int bark) => Bark = bark;

            public int Bark { get; }
        }

        public class RequiredDog : Animal
        {
            public required int Bark { get; set; }
        }

        [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
        [YamlDerivedType(typeof(AttributedDog), "dog")]
        [YamlDerivedType(typeof(AttributedCat), "cat", Tag = "tag:example.com,2000:cat")]
        public class AttributedAnimal
        {
            public string? Name { get; set; }
        }

        public class AttributedDog : AttributedAnimal
        {
        }

        public class AttributedCat : AttributedAnimal
        {
        }

        private static readonly YamlSerializerOptions s_block = new() { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };

        /// <summary>Options where <see cref="Animal"/> is a hierarchy of the given style.</summary>
        private static YamlSerializerOptions Styled(YamlTypeDiscriminatorStyle style, Action<YamlPolymorphismOptions>? configure = null, YamlSerializerOptions? baseOptions = null)
        {
            return new YamlSerializerOptions(baseOptions ?? s_block)
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(Animal))
                            {
                                var polymorphism = new YamlPolymorphismOptions { DiscriminatorStyle = style };
                                if (configure is null)
                                {
                                    polymorphism.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog"));
                                    polymorphism.DerivedTypes.Add(new YamlDerivedType(typeof(Cat), "cat"));
                                }
                                else
                                {
                                    configure(polymorphism);
                                }

                                typeInfo.PolymorphismOptions = polymorphism;
                            }
                            else if (typeInfo.Type == typeof(Numbers))
                            {
                                typeInfo.PolymorphismOptions = new YamlPolymorphismOptions
                                {
                                    DiscriminatorStyle = style,
                                    DerivedTypes = { new YamlDerivedType(typeof(MoreNumbers), "more") },
                                };
                            }
                        },
                    },
                },
            };
        }

        private static YamlSerializerOptions Mapped(params (string Tag, Type Type)[] mappings) => Mapped(s_block, mappings);

        private static YamlSerializerOptions Mapped(YamlSerializerOptions baseOptions, params (string Tag, Type Type)[] mappings)
        {
            var options = new YamlSerializerOptions(baseOptions);
            foreach ((string tag, Type type) in mappings)
            {
                options.TagMappings.Add(tag, type);
            }

            return options;
        }

        private static readonly YamlTypeDiscriminatorStyle[] s_styles = { YamlTypeDiscriminatorStyle.Property, YamlTypeDiscriminatorStyle.Tag, YamlTypeDiscriminatorStyle.Both };

        // ------------------------------------------------------------------------------------------------------------
        // Options and attributes

        [Fact]
        public void Defaults()
        {
            Assert.Equal(YamlTypeDiscriminatorStyle.Property, new YamlPolymorphismOptions().DiscriminatorStyle);
            Assert.Equal(YamlTypeDiscriminatorStyle.Property, new YamlPolymorphicAttribute().DiscriminatorStyle);
            Assert.Null(new YamlDerivedTypeAttribute(typeof(Dog), "dog").Tag);
            Assert.Null(new YamlDerivedType(typeof(Dog), "dog").Tag);
            Assert.Empty(new YamlSerializerOptions().TagMappings);
            Assert.Empty(YamlSerializerOptions.Default.TagMappings);
            var attribute = new YamlTagMappingAttribute("!dog", typeof(Dog));
            Assert.Equal("!dog", attribute.Tag);
            Assert.Equal(typeof(Dog), attribute.Type);
        }

        [Theory]
        [InlineData("!!str")]
        [InlineData("!!map")]
        [InlineData("tag:yaml.org,2002:str")]
        [InlineData("!")]
        [InlineData("?")]
        [InlineData("")]
        [InlineData("dog")]
        [InlineData("!a!b")]
        [InlineData("!a b")]
        [InlineData("<tag:x,1:y>")]
        public void A2_TagMappings_RefusesReservedAndInvalidTags(string tag)
        {
            var options = new YamlSerializerOptions();
            Assert.Throws<ArgumentException>(() => options.TagMappings.Add(tag, typeof(Dog)));
            Assert.Throws<ArgumentException>(() => options.TagMappings[tag] = typeof(Dog));
            Assert.Empty(options.TagMappings);
        }

        [Theory]
        [InlineData(typeof(Animal), true)]
        [InlineData(typeof(IDisposable), false)]
        [InlineData(typeof(Stream), false)]
        [InlineData(typeof(List<>), false)]
        [InlineData(typeof(int?), false)]
        [InlineData(typeof(double), true)]
        public void A5_TagMappings_Types(Type type, bool accepted)
        {
            var options = new YamlSerializerOptions();
            if (accepted)
            {
                options.TagMappings.Add("!t", type);
                Assert.Equal(type, options.TagMappings["!t"]);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => options.TagMappings.Add("!t", type));
            }
        }

        [Fact]
        public void A2_TagMappings_RefusesATagOrATypeMappedTwice()
        {
            var options = new YamlSerializerOptions();
            options.TagMappings.Add("!dog", typeof(Dog));
            Assert.Throws<ArgumentException>(() => options.TagMappings.Add("!dog", typeof(Cat)));
            Assert.Throws<ArgumentException>(() => options.TagMappings.Add("!hound", typeof(Dog)));

            // Replacing an entry's type is not a second tag for it.
            options.TagMappings["!dog"] = typeof(Dog);
            options.TagMappings["!dog"] = typeof(Cat);
            Assert.Equal(typeof(Cat), options.TagMappings["!dog"]);
            Assert.Throws<ArgumentNullException>(() => options.TagMappings.Add("!x", null!));
        }

        [Fact]
        public void A5_TagMappings_CopiedAndFrozen()
        {
            YamlSerializerOptions options = Mapped(("!dog", typeof(Dog)));
            var copy = new YamlSerializerOptions(options);
            Assert.Equal(typeof(Dog), copy.TagMappings["!dog"]);
            copy.TagMappings.Add("!cat", typeof(Cat));
            Assert.False(options.TagMappings.ContainsKey("!cat"));

            YamlSerializer.Serialize(1, options);
            Assert.True(options.TagMappings.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => options.TagMappings.Add("!cat", typeof(Cat)));
            Assert.Throws<InvalidOperationException>(() => options.TagMappings.Remove("!dog"));
            Assert.Throws<InvalidOperationException>(() => options.TagMappings.Clear());
        }

        [Fact]
        public void PolymorphismOptions_StyleFrozenWithTheContract()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag);
            options.MakeReadOnly();
            YamlTypeInfo info = options.GetTypeInfo(typeof(Animal));
            Assert.Equal(YamlTypeDiscriminatorStyle.Tag, info.PolymorphismOptions!.DiscriminatorStyle);
            Assert.Throws<InvalidOperationException>(() => info.PolymorphismOptions.DiscriminatorStyle = YamlTypeDiscriminatorStyle.Both);
            Assert.Throws<InvalidOperationException>(() => info.PolymorphismOptions.IgnoreUnrecognizedTypeDiscriminators = true);
        }

        // ------------------------------------------------------------------------------------------------------------
        // A1: identity, forms, and when tags are computed (R1, R2)

        [Fact]
        public void A1_DefaultTag_FromTheDiscriminator()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
            {
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog"));
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Cat), 1));
            });

            Assert.Equal("!dog\nBark: 2\nName: d\n", YamlSerializer.Serialize<Animal>(new Dog { Name = "d", Bark = 2 }, options));
            Assert.Equal("!1\nPurrs: true\nName: c\n", YamlSerializer.Serialize<Animal>(new Cat { Name = "c", Purrs = true }, options));
            Assert.IsType<Cat>(YamlSerializer.Deserialize<Animal>("!1 {Name: c}", options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!dog {Name: d}", options));
        }

        [Fact]
        public void A1_GlobalTag_ComparedExpanded_WrittenVerbatim()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog") { Tag = "tag:example.com,2000:dog" }));

            Assert.Equal("!<tag:example.com,2000:dog>\nBark: 0\nName: d\n", YamlSerializer.Serialize<Animal>(new Dog { Name = "d" }, options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!<tag:example.com,2000:dog> {Name: d}", options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("%TAG !e! tag:example.com,2000:\n--- !e!dog {Name: d}", options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("%TAG ! tag:example.com,2000:\n--- !dog {Name: d}", options));

            // The local !dog is not the global tag.
            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>("!dog {Name: d}", options));
        }

        [Fact]
        public void A1_Attributes()
        {
            string yaml = YamlSerializer.Serialize<AttributedAnimal>(new AttributedCat { Name = "c" }, s_block);
            Assert.Equal("!<tag:example.com,2000:cat>\nName: c\n", yaml);
            Assert.IsType<AttributedCat>(YamlSerializer.Deserialize<AttributedAnimal>(yaml, s_block));
            Assert.IsType<AttributedDog>(YamlSerializer.Deserialize<AttributedAnimal>("!dog {Name: d}", s_block));
            Assert.IsType<AttributedDog>(YamlSerializer.Deserialize<AttributedAnimal>("{$type: dog, Name: d}", s_block));
        }

        [Theory]
        [InlineData("my dog")]
        [InlineData("a!b")]
        [InlineData("x,y")]
        public void R1_DiscriminatorThatIsNotATag(string discriminator)
        {
            void Configure(YamlPolymorphismOptions p) => p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), discriminator));

            // Property: as today, no tag is computed.
            YamlSerializerOptions property = Styled(YamlTypeDiscriminatorStyle.Property, Configure);
            string yaml = YamlSerializer.Serialize<Animal>(new Dog(), property);
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>(yaml, property));

            foreach (YamlTypeDiscriminatorStyle style in new[] { YamlTypeDiscriminatorStyle.Tag, YamlTypeDiscriminatorStyle.Both })
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize<Animal>(new Dog(), Styled(style, Configure)));
                Assert.Contains("YamlDerivedTypeAttribute.Tag", e.Message);
            }

            // An explicit tag makes it usable.
            YamlSerializerOptions tagged = Styled(YamlTypeDiscriminatorStyle.Both, p => p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), discriminator) { Tag = "!dog" }));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!dog {}", tagged));
        }

        [Fact]
        public void R1_ExplicitTag_ValidatedUnderEveryStyle_UnusedUnderProperty()
        {
            foreach (YamlTypeDiscriminatorStyle style in s_styles)
            {
                foreach (string bad in new[] { "!!map", "!", "!a!b", "dog" })
                {
                    YamlSerializerOptions options = Styled(style, p => p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog") { Tag = bad }));
                    Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<Animal>("{}", options));
                }
            }

            YamlSerializerOptions property = Styled(YamlTypeDiscriminatorStyle.Property, p => p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog") { Tag = "!hound" }));
            Assert.Equal("$type: dog\nBark: 0\nName: null\n", YamlSerializer.Serialize<Animal>(new Dog(), property));
            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!hound {}", property)!.GetType());
        }

        [Fact]
        public void R2_TagWithoutDiscriminator()
        {
            void Configure(YamlPolymorphismOptions p) => p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog)) { Tag = "!dog" });

            foreach (YamlTypeDiscriminatorStyle style in new[] { YamlTypeDiscriminatorStyle.Tag, YamlTypeDiscriminatorStyle.Both })
            {
                Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<Animal>("{}", Styled(style, Configure)));
            }

            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!dog {}", Styled(YamlTypeDiscriminatorStyle.Property, Configure))!.GetType());
        }

        [Fact]
        public void A2_DuplicateTagInAHierarchy()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
            {
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog"));
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Cat), "cat") { Tag = "!dog" });
            });

            Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<Animal>("{}", options));
        }

        // ------------------------------------------------------------------------------------------------------------
        // A3: each style on read

        public static IEnumerable<object?[]> A3Rows()
        {
            // input, Property, Tag, Both (a type, or null for an error)
            yield return new object?[] { "!dog {Name: d}", typeof(Animal), typeof(Dog), typeof(Dog) };
            yield return new object?[] { "{$type: dog, Name: d}", typeof(Dog), typeof(Dog), typeof(Dog) };
            yield return new object?[] { "!dog {$type: dog, Name: d}", typeof(Dog), typeof(Dog), typeof(Dog) };
            yield return new object?[] { "!cat {$type: dog, Name: d}", typeof(Dog), null, null };
            yield return new object?[] { "!wolf {Name: d}", typeof(Animal), null, null };
            yield return new object?[] { "!!map {Name: d}", typeof(Animal), typeof(Animal), typeof(Animal) };
            yield return new object?[] { "{Name: d}", typeof(Animal), typeof(Animal), typeof(Animal) };
            yield return new object?[] { "!dog\nName: d\n", typeof(Animal), typeof(Dog), typeof(Dog) };
            yield return new object?[] { "!dog\n$type: dog\nName: d\n", typeof(Dog), typeof(Dog), typeof(Dog) };
        }

        [Theory]
        [MemberData(nameof(A3Rows))]
        public async Task A3_EachStyleOnRead(string yaml, Type? property, Type? tag, Type? both)
        {
            Type?[] expected = { property, tag, both };
            for (int i = 0; i < 3; i++)
            {
                YamlSerializerOptions options = Styled(s_styles[i]);
                foreach (Func<Task<Animal?>> read in Reads<Animal>(yaml, options))
                {
                    if (expected[i] is null)
                    {
                        await Assert.ThrowsAsync<YamlException>(read);
                    }
                    else
                    {
                        Animal? animal = await read();
                        Assert.Equal(expected[i], animal!.GetType());
                        Assert.Equal("d", animal.Name);
                    }
                }

                // The same in a member and in a list.
                string member = "Pet: " + (yaml.Contains('\n') ? "\n  " + yaml.TrimEnd('\n').Replace("\n", "\n  ") : yaml);
                if (expected[i] is null)
                {
                    Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<AnimalHolder>(member, options));
                }
                else
                {
                    Assert.Equal(expected[i], YamlSerializer.Deserialize<AnimalHolder>(member, options)!.Pet!.GetType());
                }
            }
        }

        [Fact]
        public void A3_UnknownTag_IgnoredWhenTheHierarchySaysSo()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
            {
                p.IgnoreUnrecognizedTypeDiscriminators = true;
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog"));
            });

            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!wolf {Name: d}", options)!.GetType());
        }

        [Fact]
        public void A3_DisagreementNamesBoth()
        {
            YamlException e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>("!cat {$type: dog}", Styled(YamlTypeDiscriminatorStyle.Both)));
            Assert.Contains("dog", e.Message);
            Assert.Contains(nameof(Cat), e.Message);
        }

        [Fact]
        public void A3_TagAtASiteThatIsNotPolymorphic_Ignored()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag);
            Assert.Equal(2, YamlSerializer.Deserialize<DogHolder>("Pet: !cat {Bark: 2}", options)!.Pet!.Bark);
            Assert.Equal(3, YamlSerializer.Deserialize<Other>("!dog {X: 3}", options)!.X);
        }

        [Fact]
        public void A3_ListOfAnimals()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag);
            Shelter shelter = YamlSerializer.Deserialize<Shelter>("Pets:\n- !dog {Name: a}\n- !cat\n  Name: b\n- {Name: c}\n", options)!;
            Assert.Equal(new[] { typeof(Dog), typeof(Cat), typeof(Animal) }, shelter.Pets.Select(p => p.GetType()));
            Assert.Equal(new[] { "a", "b", "c" }, shelter.Pets.Select(p => p.Name));
        }

        // R3: wrong-shaped tagged nodes.
        [Theory]
        [InlineData("!dog")]
        [InlineData("!dog ~")]
        [InlineData("!dog null")]
        [InlineData("!dog \"text\"")]
        [InlineData("!dog text")]
        [InlineData("!dog 12")]
        [InlineData("!dog [1]")]
        public async Task R3_WrongShape_IsAnErrorThatNamesTheTag(string yaml)
        {
            foreach (YamlTypeDiscriminatorStyle style in new[] { YamlTypeDiscriminatorStyle.Tag, YamlTypeDiscriminatorStyle.Both })
            {
                foreach (Func<Task<Animal?>> read in Reads<Animal>(yaml, Styled(style)))
                {
                    YamlException e = await Assert.ThrowsAsync<YamlException>(read);
                    Assert.Contains("!dog", e.Message);
                }

                YamlException member = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<AnimalHolder>("Pet: " + yaml, Styled(style)));
                Assert.Contains("!dog", member.Message);
            }

            // Under Property the tag is ignored, as today.
            if (yaml is "!dog" or "!dog ~" or "!dog null")
            {
                Assert.Null(YamlSerializer.Deserialize<Animal>(yaml, Styled(YamlTypeDiscriminatorStyle.Property)));
            }
        }

        [Fact]
        public void R3_CoreNullTag_IsANull()
        {
            Assert.Null(YamlSerializer.Deserialize<Animal>("!!null ~", Styled(YamlTypeDiscriminatorStyle.Tag)));
            Assert.Null(YamlSerializer.Deserialize<AnimalHolder>("Pet: !!null", Styled(YamlTypeDiscriminatorStyle.Tag))!.Pet);
        }

        [Theory]
        [InlineData("!more {a: 1}")]
        [InlineData("!more x")]
        [InlineData("!more")]
        public void R3_WrongShape_Collection(string yaml)
        {
            YamlException e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Numbers>(yaml, Styled(YamlTypeDiscriminatorStyle.Tag)));
            Assert.Contains("!more", e.Message);
        }

        // ------------------------------------------------------------------------------------------------------------
        // A4 and the write side

        [Fact]
        public void Write_EachStyle()
        {
            var dog = new Dog { Name = "d", Bark = 1 };
            Assert.Equal("$type: dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<Animal>(dog, Styled(YamlTypeDiscriminatorStyle.Property)));
            Assert.Equal("!dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<Animal>(dog, Styled(YamlTypeDiscriminatorStyle.Tag)));
            Assert.Equal("!dog\n$type: dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<Animal>(dog, Styled(YamlTypeDiscriminatorStyle.Both)));

            // The base type and an unlisted derived type are written untagged.
            Assert.Equal("Name: a\n", YamlSerializer.Serialize<Animal>(new Animal { Name = "a" }, Styled(YamlTypeDiscriminatorStyle.Tag)));

            // Members, and a list.
            Assert.Equal("Pet: !dog\n  Bark: 1\n  Name: d\n", YamlSerializer.Serialize(new AnimalHolder { Pet = dog }, Styled(YamlTypeDiscriminatorStyle.Tag)));
            Assert.Equal("Pets:\n- !dog\n  Bark: 1\n  Name: d\n- Name: a\n", YamlSerializer.Serialize(new Shelter { Pets = { dog, new Animal { Name = "a" } } }, Styled(YamlTypeDiscriminatorStyle.Tag)));
        }

        [Fact]
        public void Write_RoundTripsUnderEveryStyle_AndAcrossStyles()
        {
            var shelter = new Shelter { Pets = { new Dog { Name = "d", Bark = 1 }, new Cat { Name = "c", Purrs = true }, new Animal { Name = "a" } } };
            foreach (YamlTypeDiscriminatorStyle writeStyle in s_styles)
            {
                string yaml = YamlSerializer.Serialize(shelter, Styled(writeStyle));
                foreach (YamlTypeDiscriminatorStyle readStyle in s_styles)
                {
                    Shelter read = YamlSerializer.Deserialize<Shelter>(yaml, Styled(readStyle))!;
                    bool typed = writeStyle != YamlTypeDiscriminatorStyle.Tag || readStyle != YamlTypeDiscriminatorStyle.Property;
                    Assert.Equal(typed ? new[] { typeof(Dog), typeof(Cat), typeof(Animal) } : new[] { typeof(Animal), typeof(Animal), typeof(Animal) }, read.Pets.Select(p => p.GetType()));
                }
            }
        }

        [Fact]
        public void A4_Collections()
        {
            var more = new MoreNumbers { 1, 2 };
            Assert.Equal("List: !more\n- 1\n- 2\n", YamlSerializer.Serialize(new NumbersHolder { List = more }, Styled(YamlTypeDiscriminatorStyle.Tag)));
            Assert.Equal("List: !more\n  $type: more\n  $values:\n  - 1\n  - 2\n", YamlSerializer.Serialize(new NumbersHolder { List = more }, Styled(YamlTypeDiscriminatorStyle.Both)));
            Assert.Equal("List:\n  $type: more\n  $values:\n  - 1\n  - 2\n", YamlSerializer.Serialize(new NumbersHolder { List = more }, Styled(YamlTypeDiscriminatorStyle.Property)));

            foreach (YamlTypeDiscriminatorStyle style in new[] { YamlTypeDiscriminatorStyle.Tag, YamlTypeDiscriminatorStyle.Both })
            {
                YamlSerializerOptions options = Styled(style);
                Numbers read = YamlSerializer.Deserialize<NumbersHolder>("List: !more [1, 2]", options)!.List!;
                Assert.IsType<MoreNumbers>(read);
                Assert.Equal(new[] { 1, 2 }, read);
                Assert.IsType<MoreNumbers>(YamlSerializer.Deserialize<Numbers>("!more\n- 1\n", options));
                Assert.IsType<MoreNumbers>(YamlSerializer.Deserialize<Numbers>("!more {$type: more, $values: [1]}", options));
                Assert.IsType<MoreNumbers>(YamlSerializer.Deserialize<Numbers>("{$type: more, $values: [1]}", options));
                Assert.IsType<Numbers>(YamlSerializer.Deserialize<Numbers>("[1]", options));
            }
        }

        // A8: the JSON-compatible writer writes $type under Tag and Both, which reads back under every style.
        [Fact]
        public void A8_JsonCompatibleWriter_WritesTypeProperty()
        {
            var json = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible };
            foreach (YamlTypeDiscriminatorStyle style in s_styles)
            {
                YamlSerializerOptions options = Styled(style, baseOptions: json);
                string written = YamlSerializer.Serialize<Animal>(new Dog { Name = "d" }, options);
                Assert.Equal("{\"$type\":\"dog\",\"Bark\":0,\"Name\":\"d\"}", written);
                Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>(written, options));
                Assert.Equal("{\"List\":{\"$type\":\"more\",\"$values\":[1]}}", YamlSerializer.Serialize(new NumbersHolder { List = new MoreNumbers { 1 } }, options));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // A5: TagMappings

        [Fact]
        public void A5_ObjectSites_Read()
        {
            YamlSerializerOptions options = Mapped(("!dog", typeof(Dog)), ("!celsius", typeof(double)), ("tag:example.com,2000:other", typeof(Other)));

            Dog dog = Assert.IsType<Dog>(YamlSerializer.Deserialize<object>("!dog {Name: d, Bark: 2}", options));
            Assert.Equal(2, dog.Bark);
            Assert.Equal(21.5, Assert.IsType<double>(YamlSerializer.Deserialize<object>("!celsius 21.5", options)));
            Assert.Equal(21.5, Assert.IsType<double>(YamlSerializer.Deserialize<Holder>("Value: !celsius 21.5", options)!.Value));
            Assert.IsType<Other>(YamlSerializer.Deserialize<Holder>("Value: !<tag:example.com,2000:other> {X: 1}", options)!.Value);
            Assert.IsType<Dog>(YamlSerializer.Deserialize<List<object>>("- !dog\n  Name: d\n", options)![0]);
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Dictionary<string, object>>("a: !dog {}", options)!["a"]);

            // An unmapped tag keeps today's result, with its tag.
            YamlElement element = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>("!wolf {Name: w}", options));
            Assert.Equal("!wolf", element.GetTag());
            YamlNode node = Assert.IsAssignableFrom<YamlNode>(YamlSerializer.Deserialize<object>("!wolf {Name: w}", new YamlSerializerOptions(options) { UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode }));
            Assert.Equal("!wolf", node.Tag);
        }

        [Fact]
        public void A5_TypedSites_Read()
        {
            YamlSerializerOptions options = Mapped(("!dog", typeof(Dog)), ("!other", typeof(Other)), ("!celsius", typeof(double)));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!dog {Name: d}", options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<AnimalHolder>("Pet: !dog {Name: d}", options)!.Pet);

            // An unmapped tag is ignored; a mapped type the site cannot hold is an error that names both.
            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!wolf {Name: d}", options)!.GetType());
            YamlException e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>("!other {X: 1}", options));
            Assert.Contains(nameof(Other), e.Message);
            Assert.Contains(nameof(Animal), e.Message);

            // A scalar-typed site ignores the tag, as today.
            Assert.Equal("x", YamlSerializer.Deserialize<string>("!dog x", options));
            Assert.Equal(1.5, YamlSerializer.Deserialize<double>("!celsius 1.5", options));
        }

        [Fact]
        public void A5_NotAtAPolymorphicSite()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Property);
            options.TagMappings.Add("!dog", typeof(Dog));

            // The hierarchy decides there: under Property the tag is ignored.
            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!dog {Name: d}", options)!.GetType());
        }

        [Fact]
        public void A5_Write()
        {
            YamlSerializerOptions options = Mapped(("!dog", typeof(Dog)), ("!celsius", typeof(double)), ("tag:example.com,2000:other", typeof(Other)));
            var dog = new Dog { Name = "d", Bark = 1 };

            Assert.Equal("Value: !dog\n  Bark: 1\n  Name: d\n", YamlSerializer.Serialize(new Holder { Value = dog }, options));
            Assert.Equal("Value: !<tag:example.com,2000:other>\n  X: 1\n", YamlSerializer.Serialize(new Holder { Value = new Other { X = 1 } }, options));
            Assert.Equal("Pet: !dog\n  Bark: 1\n  Name: d\n", YamlSerializer.Serialize(new AnimalHolder { Pet = dog }, options));
            Assert.Equal("!dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<object>(dog, options));
            Assert.Equal("!dog\nBark: 1\nName: d\n", YamlSerializer.Serialize<Animal>(dog, options));

            // Not where the declared type is the runtime type.
            Assert.Equal("Bark: 1\nName: d\n", YamlSerializer.Serialize(dog, options));
            Assert.Equal("Pet:\n  Bark: 1\n  Name: d\n", YamlSerializer.Serialize(new DogHolder { Pet = dog }, options));

            // R7: every boxed double at an object site; a double? boxes as a double.
            double? nullable = 21.5;
            Assert.Equal("Value: !celsius 21.5\n", YamlSerializer.Serialize(new Holder { Value = 21.5 }, options));
            Assert.Equal("Value: !celsius 21.5\n", YamlSerializer.Serialize(new Holder { Value = nullable }, options));
            Assert.Equal("- !celsius 1\n- 2\n", YamlSerializer.Serialize(new List<object> { 1.0, 2 }, options));
            Assert.Equal("21.5\n", YamlSerializer.Serialize(21.5, options));

            // Each reads back.
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Holder>(YamlSerializer.Serialize(new Holder { Value = dog }, options), options)!.Value);
            Assert.IsType<Dog>(YamlSerializer.Deserialize<AnimalHolder>(YamlSerializer.Serialize(new AnimalHolder { Pet = dog }, options), options)!.Pet);
            Assert.IsType<double>(YamlSerializer.Deserialize<List<object>>("- !celsius 1\n- 2\n", options)![0]);
        }

        public class UpperConverter : Serialization.YamlConverter<Other>
        {
            public override Other Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => new Other { X = reader.GetInt32() };

            public override void Write(Utf8YamlWriter writer, Other value, YamlSerializerOptions options) => writer.WriteNumberValue(value.X);
        }

        [Fact]
        public void R7_CustomConverter_GetsTheTagBeforeItsNode()
        {
            YamlSerializerOptions options = Mapped(("!other", typeof(Other)));
            options.Converters.Add(new UpperConverter());
            string yaml = YamlSerializer.Serialize(new Holder { Value = new Other { X = 4 } }, options);
            Assert.Equal("Value: !other 4\n", yaml);
            Assert.Equal(4, Assert.IsType<Other>(YamlSerializer.Deserialize<Holder>(yaml, options)!.Value).X);
        }

        // R8: the JSON-compatible writer writes a mapped type untagged; it reads back as an element.
        [Fact]
        public void R8_JsonCompatibleWriter_WritesMappedTypesUntagged()
        {
            YamlSerializerOptions options = Mapped(new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible }, ("!dog", typeof(Dog)), ("!celsius", typeof(double)));
            Assert.Equal("{\"Value\":{\"Bark\":0,\"Name\":\"d\"}}", YamlSerializer.Serialize(new Holder { Value = new Dog { Name = "d" } }, options));
            Assert.Equal("{\"Pet\":{\"Bark\":0,\"Name\":\"d\"}}", YamlSerializer.Serialize(new AnimalHolder { Pet = new Dog { Name = "d" } }, options));
            Assert.Equal("{\"Value\":1.5}", YamlSerializer.Serialize(new Holder { Value = 1.5 }, options));
            Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Holder>("{\"Value\":{\"Name\":\"d\"}}", options)!.Value);
        }

        // ------------------------------------------------------------------------------------------------------------
        // A7: a tag never names a type

        [Fact]
        public void A7_Refusal_LoadsNothing()
        {
            string[] tags =
            {
                // A ',' ends a tag shorthand, so decision 7's "!Dog,Libraries" is written escaped or verbatim.
                "!Dog%2CLibraries",
                "!<!Dog,Libraries>",
                "!<!System.Diagnostics.Process,System.Diagnostics.Process>",
                "!<tag:yaml.org,2002:python/object:os.system>",
                "!!python/object:os.system",
                "!<!" + typeof(Dog).AssemblyQualifiedName!.Replace(" ", "") + ">",
                "!" + typeof(Dog).FullName!.Replace("+", "%2B"),
            };

            var options = new List<YamlSerializerOptions>();
            foreach (YamlTypeDiscriminatorStyle style in s_styles)
            {
                YamlSerializerOptions styled = Styled(style, p =>
                {
                    p.IgnoreUnrecognizedTypeDiscriminators = true;
                    p.DerivedTypes.Add(new YamlDerivedType(typeof(Cat), "cat"));
                });
                styled.TagMappings.Add("!cat", typeof(Cat));
                options.Add(styled);
            }

            // Warm up, so that what loads during the reads is the reads' own doing.
            foreach (YamlSerializerOptions o in options)
            {
                YamlSerializer.Deserialize<object>("!cat {}", o);
                YamlSerializer.Deserialize<Holder>("Value: !cat {}", o);
                YamlSerializer.Deserialize<AnimalHolder>("Pet: !cat {}", o);
            }

            var loads = new List<string>();
            void OnLoad(object? sender, AssemblyLoadEventArgs args) => loads.Add(args.LoadedAssembly.FullName ?? "?");
            AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
            try
            {
                foreach (YamlSerializerOptions o in options)
                {
                    foreach (string tag in tags)
                    {
                        Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(tag + " {Name: x}", o));
                        Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Holder>("Value: " + tag + " {Name: x}", o)!.Value);
                        Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<AnimalHolder>("Pet: " + tag + " {Name: x}", o)!.Pet!.GetType());
                        Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>(tag + " {Name: x}", o)!.GetType());
                    }
                }
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
            }

            Assert.Empty(loads);

            // Without IgnoreUnrecognizedTypeDiscriminators, an unknown tag at a Tag-style site is the unknown-tag error.
            YamlSerializerOptions strict = Styled(YamlTypeDiscriminatorStyle.Tag);
            foreach (string tag in tags.Where(t => !t.StartsWith("!!", StringComparison.Ordinal) && !t.Contains("yaml.org")))
            {
                Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>(tag + " {Name: x}", strict));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // A10 and R4: elements and nodes keep the tag, and a re-read sees it

        [Theory]
        [InlineData("!dog {a: 1}", "!dog")]
        [InlineData("!dog\na: 1\n", "!dog")]
        [InlineData("!dog [1]", "!dog")]
        [InlineData("!celsius 21.5", "!celsius")]
        [InlineData("!t true", "!t")]
        [InlineData("!t x", "!t")]
        [InlineData("!<tag:example.com,2000:x> {a: 1}", "!<tag:example.com,2000:x>")]
        public void A10_ObjectSite_ElementAndNodeKeepTheTag(string yaml, string tag)
        {
            YamlElement element = Assert.IsType<YamlElement>(YamlSerializer.Deserialize<object>(yaml, s_block));
            Assert.Equal(tag, element.GetTag());
            Assert.Equal(tag, element.Clone().GetTag());
            Assert.Equal(tag, Assert.IsType<YamlElement>(YamlSerializer.Deserialize<Holder>("Value: " + yaml.Replace("\n", "\n  "), s_block)!.Value).GetTag());

            var nodes = new YamlSerializerOptions(s_block) { UnknownTypeHandling = YamlUnknownTypeHandling.YamlNode };
            YamlNode? node = YamlSerializer.Deserialize<object>(yaml, nodes) as YamlNode;
            Assert.Equal(tag, node!.Tag);
        }

        [Fact]
        public void A10_TaggedNull_AtAnObjectSite_IsANull()
        {
            // Recorded choice: no mapping selects a type, so a tagged null stays a null, as before M5d.
            Assert.Null(YamlSerializer.Deserialize<object>("!t ~", s_block));
            Assert.Null(YamlSerializer.Deserialize<Holder>("Value: !t", s_block)!.Value);
        }

        public static IEnumerable<object[]> R4Options()
        {
            yield return new object[] { "Tag" };
            yield return new object[] { "Mapped" };
        }

        private static YamlSerializerOptions R4(string kind) => kind == "Tag" ? Styled(YamlTypeDiscriminatorStyle.Tag) : Mapped(("!dog", typeof(Dog)));

        [Theory]
        [MemberData(nameof(R4Options))]
        public void R4_ElementReReads(string kind)
        {
            YamlSerializerOptions options = R4(kind);
            foreach (string pet in new[] { "!dog {Name: d}", "!dog\n  Name: d\n" })
            {
                using YamlDocument document = YamlDocument.Parse("pet: " + pet);
                YamlElement element = document.RootElement.GetProperty("pet");
                Assert.IsType<Dog>(element.Deserialize<Animal>(options));
                Assert.IsType<Dog>(element.Clone().Deserialize<Animal>(options));
                Assert.IsType<Dog>(element.Deserialize(typeof(Animal), options));
            }

            foreach (string root in new[] { "!dog {Name: d}", "!dog\nName: d\n", "%TAG !e! !\n--- !e!dog {Name: d}" })
            {
                using YamlDocument document = YamlDocument.Parse(root);
                Assert.IsType<Dog>(document.Deserialize<Animal>(options));
                Assert.IsType<Dog>(document.RootElement.Deserialize<Animal>(options));
                Assert.IsType<Dog>(document.RootElement.Clone().Deserialize<Animal>(options));
            }

            using YamlDocument nested = YamlDocument.Parse("%TAG !e! !\n---\npet: !e!dog {Name: d}");
            Assert.IsType<Dog>(nested.RootElement.GetProperty("pet").Deserialize<Animal>(options));
            Assert.IsType<Dog>(nested.RootElement.GetProperty("pet").Clone().Deserialize<Animal>(options));
        }

        [Theory]
        [MemberData(nameof(R4Options))]
        public void R4_DictionaryValueReReads(string kind)
        {
            YamlSerializerOptions options = R4(kind);
            foreach (string text in new[] { "pet: !dog {Name: d}", "pet: !dog\n  Name: d\n" })
            {
                // Read without the mapping, so the value is an element.
                object value = YamlSerializer.Deserialize<Dictionary<string, object>>(text, s_block)!["pet"];
                YamlElement element = Assert.IsType<YamlElement>(value);
                Assert.Equal("!dog", element.GetTag());
                Assert.IsType<Dog>(element.Deserialize<Animal>(options));
                Assert.IsType<Dog>(element.Clone().Deserialize<Animal>(options));
            }
        }

        [Theory]
        [MemberData(nameof(R4Options))]
        public void R4_NodeReReads(string kind)
        {
            YamlSerializerOptions options = R4(kind);
            foreach (string text in new[] { "!dog {Name: d}", "!dog\nName: d\n" })
            {
                YamlNode node = YamlNode.Parse(text)!;
                Assert.Equal("!dog", node.Tag);
                Assert.IsType<Dog>(node.Deserialize<Animal>(options));
                Assert.IsType<Dog>(node.DeepClone().Deserialize<Animal>(options));
                Assert.IsType<Dog>(YamlNode.Parse("pet: " + text.Replace("\n", "\n  "))!["pet"]!.Deserialize<Animal>(options));
            }

            var built = new YamlObject { ["Name"] = "d" };
            built.Tag = "!dog";
            Assert.IsType<Dog>(built.Deserialize<Animal>(options));
        }

        // ------------------------------------------------------------------------------------------------------------
        // R5: the default path does no tag work

        [Fact]
        public void R5_Flags()
        {
            YamlSerializerOptions plain = new YamlSerializerOptions(s_block);
            Assert.Null(plain.GetTypeInfo(typeof(Animal)).PolymorphismOptions);
            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!dog {Name: d}", plain)!.GetType());

            // A Property-style hierarchy does not read tags.
            Assert.Equal(typeof(Animal), YamlSerializer.Deserialize<Animal>("!dog {Name: d}", Styled(YamlTypeDiscriminatorStyle.Property))!.GetType());
        }

        // ------------------------------------------------------------------------------------------------------------
        // R6: run-time options against a source-generated context

        [Fact]
        public void R6_RunTimeMappings_AgainstAContext()
        {
            var options = new YamlSerializerOptions(s_block) { TypeInfoResolver = TagDiscriminatorContext.Default };
            options.TagMappings.Add("!dog", typeof(Dog));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Holder>("Value: !dog {Name: d}", options)!.Value);
            Assert.Equal("Value: !dog\n  Bark: 0\n  Name: d\n", YamlSerializer.Serialize(new Holder { Value = new Dog { Name = "d" } }, options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<AnimalHolder>("Pet: !dog {Name: d}", options)!.Pet);

            // A mapped type the context does not have: the resolver's NotSupportedException, on read and on write.
            var missing = new YamlSerializerOptions(s_block) { TypeInfoResolver = TagDiscriminatorContext.Default };
            missing.TagMappings.Add("!other", typeof(Other));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize<Holder>("Value: !other {X: 1}", missing));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(new Holder { Value = new Other() }, missing));
        }

        // M7a: run-time mappings against a context whose types have a
        // fast path. A base-typed member (not object) holding a mapped type must be written with its tag, as reflection
        // writes it (A5_Write), so the context's fast path must not serve these options.
        [Fact]
        public void R6_RunTimeMappings_AgainstAContext_BaseTypedMember()
        {
            var options = new YamlSerializerOptions(s_block) { TypeInfoResolver = TagDiscriminatorContext.Default };
            options.TagMappings.Add("!dog", typeof(Dog));
            var dog = new Dog { Name = "d", Bark = 1 };
            Assert.Equal("Pet: !dog\n  Bark: 1\n  Name: d\n", YamlSerializer.Serialize(new AnimalHolder { Pet = dog }, options));
            Assert.Equal("Pet: !dog\n  Bark: 1\n  Name: d\n", Encoding.UTF8.GetString(YamlSerializer.SerializeToUtf8Bytes(new AnimalHolder { Pet = dog }, options)));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<AnimalHolder>(YamlSerializer.Serialize(new AnimalHolder { Pet = dog }, options), options)!.Pet);
        }

        [Fact]
        public void R6_TagStyle_ThroughContractCustomization_OnAGeneratedType()
        {
            var options = new YamlSerializerOptions(s_block)
            {
                TypeInfoResolver = TagDiscriminatorContext.Default.WithAddedModifier(typeInfo =>
                {
                    if (typeInfo.Type == typeof(Animal))
                    {
                        typeInfo.PolymorphismOptions = new YamlPolymorphismOptions
                        {
                            DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                            DerivedTypes = { new YamlDerivedType(typeof(Dog), "dog") },
                        };
                    }
                }),
            };

            Assert.Equal("Pet: !dog\n  Bark: 0\n  Name: d\n", YamlSerializer.Serialize(new AnimalHolder { Pet = new Dog { Name = "d" } }, options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<AnimalHolder>("Pet: !dog {Name: d}", options)!.Pet);
        }

        [Fact]
        public void A9_GeneratedContext_WithMappingsAndATagStyleHierarchy()
        {
            // A block-style copy (leg YJ makes JSON-compatible output the default); the resolver is still the context.
            var options = new YamlSerializerOptions(TagMappedContext.Default.Options) { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
            Assert.Same(TagMappedContext.Default, options.TypeInfoResolver);
            Assert.Equal(typeof(Dog), options.TagMappings["!dog"]);
            Assert.Equal(typeof(Other), options.TagMappings["tag:example.com,2000:other"]);

            // No fast path in a context with mappings.
            Assert.Null(TagMappedContext.Default.Dog.SerializeHandler);
            Assert.NotNull(TagDiscriminatorContext.Default.Dog.SerializeHandler);

            Assert.Equal("Value: !dog\n  Bark: 0\n  Name: d\n", YamlSerializer.Serialize(new Holder { Value = new Dog { Name = "d" } }, options));
            Assert.IsType<Other>(YamlSerializer.Deserialize("Value: !<tag:example.com,2000:other> {X: 1}", TagMappedContext.Default.Holder)!.Value);
            Assert.IsType<Dog>(YamlSerializer.Deserialize("Pet: !dog {Name: d}", TagMappedContext.Default.AnimalHolder)!.Pet);

            // The attributes' hierarchy, generated.
            Assert.Equal("!<tag:example.com,2000:cat>\nName: c\n", YamlSerializer.Serialize<AttributedAnimal>(new AttributedCat { Name = "c" }, options));
            Assert.IsType<AttributedDog>(YamlSerializer.Deserialize("!dog {Name: d}", TagMappedContext.Default.AttributedAnimal));
            Assert.IsType<AttributedCat>(YamlSerializer.Deserialize("!<tag:example.com,2000:cat> {}", TagMappedContext.Default.AttributedAnimal));
        }

        [Fact]
        public void A9_SchemaExporter()
        {
            string property = Schema.YamlSchemaExporter.GetYamlSchemaAsNode(Styled(YamlTypeDiscriminatorStyle.Property), typeof(Animal)).ToYamlString(s_block);
            string both = Schema.YamlSchemaExporter.GetYamlSchemaAsNode(Styled(YamlTypeDiscriminatorStyle.Both), typeof(Animal)).ToYamlString(s_block);
            string tag = Schema.YamlSchemaExporter.GetYamlSchemaAsNode(Styled(YamlTypeDiscriminatorStyle.Tag), typeof(Animal)).ToYamlString(s_block);
            Assert.Contains("$type", property);
            Assert.Equal(property, both);
            Assert.Contains("anyOf", tag);
            Assert.DoesNotContain("$type", tag);
            Assert.DoesNotContain("required", tag);

            string list = Schema.YamlSchemaExporter.GetYamlSchemaAsNode(Styled(YamlTypeDiscriminatorStyle.Tag), typeof(Numbers)).ToYamlString(s_block);
            Assert.DoesNotContain("$values", list);
        }

        [Fact]
        public void A1_PercentEncodedLocalTag_ComparedDecoded()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
                p.DerivedTypes.Add(new YamlDerivedType(typeof(Dog), "dog") { Tag = "!a%21b" }));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!a%21b {}", options));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Animal>("!<!a!b> {}", options));
            Assert.Equal("!a%21b\nBark: 0\nName: null\n", YamlSerializer.Serialize<Animal>(new Dog(), options));

            YamlSerializerOptions mapped = Mapped(("!a%21b", typeof(Dog)));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<object>("!a%21b {}", mapped));
        }

        public class Plain
        {
            public int X { get; set; }
        }

        public class Sub : Plain
        {
            public int Y { get; set; }
        }

        public class PlainWithTypeMember
        {
            [YamlPropertyName("$type")]
            public string? Kind { get; set; }

            public int X { get; set; }
        }

        public class SubWithTypeMember : PlainWithTypeMember
        {
        }

        public class PlainWithExtension
        {
            public int X { get; set; }

            [YamlExtensionData]
            public Dictionary<string, object>? Extra { get; set; }
        }

        public class SubWithExtension : PlainWithExtension
        {
        }

        public class PlainPositional
        {
            public PlainPositional(int x) => X = x;

            public int X { get; }
        }

        public class SubPositional : PlainPositional
        {
            public SubPositional(int x) : base(x)
            {
            }
        }

        /// <summary>A site that only TagMappings makes polymorphic reads the tag, and its $-members stay members.</summary>
        [Fact]
        public async Task A5_MappingOnlySite_KeepsDollarMembers()
        {
            YamlSerializerOptions options = Mapped(("!sub", typeof(Sub)), ("!subt", typeof(SubWithTypeMember)), ("!subx", typeof(SubWithExtension)), ("!subp", typeof(SubPositional)));
            YamlSerializerOptions plain = new YamlSerializerOptions(s_block);

            foreach (YamlSerializerOptions o in new[] { plain, options })
            {
                // Reproducers: each reads a Plain, as without the mapping.
                foreach (string yaml in new[] { "a: {$id: '1', X: 1}", "a: {$type: t, X: 1}", "a: {$ref: '1', X: 1}", "a: {$values: [1], X: 1}" })
                {
                    foreach (Func<Task<Dictionary<string, Plain>?>> read in Reads<Dictionary<string, Plain>>(yaml, o))
                    {
                        Plain value = (await read())!["a"];
                        Assert.Equal((typeof(Plain), 1), (value.GetType(), value.X));
                    }
                }

                // A member named $type, and extension data receiving $ref.
                foreach (Func<Task<PlainWithTypeMember?>> read in Reads<PlainWithTypeMember>("{$type: t, X: 2}", o))
                {
                    PlainWithTypeMember value = (await read())!;
                    Assert.Equal(("t", 2), (value.Kind, value.X));
                }

                foreach (Func<Task<PlainWithExtension?>> read in Reads<PlainWithExtension>("{$ref: '#/a', X: 3}", o))
                {
                    PlainWithExtension value = (await read())!;
                    Assert.Equal(3, value.X);
                    Assert.Equal("#/a", ((YamlElement)value.Extra!["$ref"]).GetString());
                }

                // A constructor type.
                foreach (Func<Task<PlainPositional?>> read in Reads<PlainPositional>("{$id: '1', X: 4}", o))
                {
                    Assert.Equal(4, (await read())!.X);
                }
            }

            // The same sites read the mapped tag, and an untagged node as before.
            foreach (Func<Task<Dictionary<string, Plain>?>> read in Reads<Dictionary<string, Plain>>("a: !sub {$id: '1', X: 1, Y: 2}\nb: {X: 3}\n", options))
            {
                Dictionary<string, Plain> value = (await read())!;
                Sub sub = Assert.IsType<Sub>(value["a"]);
                Assert.Equal((1, 2), (sub.X, sub.Y));
                Assert.Equal(typeof(Plain), value["b"].GetType());
            }

            foreach (Func<Task<PlainWithTypeMember?>> read in Reads<PlainWithTypeMember>("!subt {$type: t, X: 2}", options))
            {
                PlainWithTypeMember value = Assert.IsType<SubWithTypeMember>(await read());
                Assert.Equal(("t", 2), (value.Kind, value.X));
            }

            foreach (Func<Task<PlainWithExtension?>> read in Reads<PlainWithExtension>("!subx\n$ref: '#/a'\nX: 3\n", options))
            {
                PlainWithExtension value = Assert.IsType<SubWithExtension>(await read());
                Assert.Equal("#/a", ((YamlElement)value.Extra!["$ref"]).GetString());
            }

            foreach (Func<Task<PlainPositional?>> read in Reads<PlainPositional>("!subp {$id: '1', X: 4}", options))
            {
                Assert.Equal(4, Assert.IsType<SubPositional>(await read()).X);
            }
        }

        [Fact]
        public void A5_MappingOnlySite_UnderPreserve_IdAndRefKeepTheirMeaning()
        {
            YamlSerializerOptions options = Mapped(new YamlSerializerOptions(s_block) { ReferenceHandler = ReferenceHandler.Preserve }, ("!sub", typeof(Sub)));
            List<Plain> list = YamlSerializer.Deserialize<List<Plain>>("- {$id: '1', X: 1}\n- {$ref: '1'}\n- !sub {X: 2, Y: 3}\n", options)!;
            Assert.Same(list[0], list[1]);
            Assert.IsType<Sub>(list[2]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // B: combinations

        [Fact]
        public void B_MergeRewrite()
        {
            const string Yaml = "base: &b !cat {Name: b}\nPets:\n- !dog\n  <<: *b\n  Bark: 2\n- *b\n";
            foreach (bool merge in new[] { false, true })
            {
                YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, baseOptions: new YamlSerializerOptions(s_block) { ResolveMergeKeys = merge });
                if (!merge)
                {
                    // Without the rewrite "<<" is an unknown member, and so is "base".
                    Shelter plain = YamlSerializer.Deserialize<Shelter>(Yaml, options)!;
                    Assert.Equal(new[] { typeof(Dog), typeof(Cat) }, plain.Pets.Select(p => p.GetType()));
                    continue;
                }

                Shelter shelter = YamlSerializer.Deserialize<Shelter>(Yaml, options)!;

                // The merged mapping keeps its own tag; the source's is not copied. The alias keeps the anchored node's.
                Dog dog = Assert.IsType<Dog>(shelter.Pets[0]);
                Assert.Equal(("b", 2), (dog.Name, dog.Bark));
                Assert.IsType<Cat>(shelter.Pets[1]);
            }
        }

        [Fact]
        public void B_AliasOfATaggedNode()
        {
            YamlSerializerOptions options = Mapped(("!dog", typeof(Dog)));
            List<object> list = YamlSerializer.Deserialize<List<object>>("- &a !dog {Name: d}\n- *a\n", options)!;
            Assert.IsType<Dog>(list[0]);
            Assert.IsType<Dog>(list[1]);

            Shelter shelter = YamlSerializer.Deserialize<Shelter>("Pets:\n- &a !dog {Name: d}\n- *a\n", Styled(YamlTypeDiscriminatorStyle.Tag))!;
            Assert.IsType<Dog>(shelter.Pets[1]);
        }

        [Fact]
        public void B_DomRoundTrip_UnderTagStyle()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag);
            var holder = new AnimalHolder { Pet = new Dog { Name = "d", Bark = 3 } };

            YamlElement element = YamlSerializer.SerializeToElement(holder, options);
            Assert.Equal("!dog", element.GetProperty("Pet").GetTag());
            Assert.IsType<Dog>(element.Deserialize<AnimalHolder>(options)!.Pet);

            using YamlDocument document = YamlSerializer.SerializeToDocument(holder, options);
            Assert.IsType<Dog>(document.Deserialize<AnimalHolder>(options)!.Pet);

            YamlNode node = YamlSerializer.SerializeToNode(holder, options)!;
            Assert.Equal("!dog", node["Pet"]!.Tag);
            Assert.IsType<Dog>(node.Deserialize<AnimalHolder>(options)!.Pet);

            // JSON-compatible: $type.
            YamlSerializerOptions json = Styled(YamlTypeDiscriminatorStyle.Tag, baseOptions: new YamlSerializerOptions { WriteStyle = YamlWriterStyle.JsonCompatible });
            YamlElement fromJson = YamlSerializer.SerializeToElement(holder, json);
            Assert.Null(fromJson.GetProperty("Pet").GetTag());
            Assert.IsType<Dog>(fromJson.Deserialize<AnimalHolder>(options)!.Pet);
        }

        [Fact]
        public void B_Preserve()
        {
            foreach (ReferenceHandler handler in new[] { ReferenceHandler.Preserve, ReferenceHandler.PreserveAll })
            {
                YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, baseOptions: new YamlSerializerOptions(s_block) { ReferenceHandler = handler });
                var dog = new Dog { Name = "d" };
                string yaml = YamlSerializer.Serialize(new Shelter { Pets = { dog, dog } }, options);
                Assert.Contains("&", yaml);
                Assert.Contains("!dog", yaml);
                Assert.True(yaml.IndexOf('&') < yaml.IndexOf("!dog", StringComparison.Ordinal), yaml);
                Shelter read = YamlSerializer.Deserialize<Shelter>(yaml, options)!;
                Assert.IsType<Dog>(read.Pets[0]);
                Assert.Same(read.Pets[0], read.Pets[1]);

                Shelter fromText = YamlSerializer.Deserialize<Shelter>("Pets:\n- &1 !dog {Name: d}\n- *1\n", options)!;
                Assert.IsType<Dog>(fromText.Pets[0]);
                Assert.Same(fromText.Pets[0], fromText.Pets[1]);
            }
        }

        [Fact]
        public void B_RepeatedTypeProperty_UnderBoth()
        {
            foreach (YamlDuplicateKeyHandling handling in Enum.GetValues(typeof(YamlDuplicateKeyHandling)))
            {
                YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Both, baseOptions: new YamlSerializerOptions(s_block) { DuplicateKeyHandling = handling });
                Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>("!dog {$type: dog, $type: dog}", options));
            }
        }

        [Fact]
        public async Task B_Streams_AndSequences()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, baseOptions: new YamlSerializerOptions(s_block) { DefaultBufferSize = 1 });
            const string Yaml = "Pets:\n- !dog {Name: a, Bark: 1}\n- !cat\n  Name: b\n- !<tag:yaml.org,2002:map> {Name: c}\n";
            Shelter read = (await YamlSerializer.DeserializeAsync<Shelter>(new MemoryStream(Encoding.UTF8.GetBytes(Yaml)), options))!;
            Assert.Equal(new[] { typeof(Dog), typeof(Cat), typeof(Animal) }, read.Pets.Select(p => p.GetType()));

            ReadOnlySequence<byte> sequence = SequenceFactory.Uniform(Encoding.UTF8.GetBytes(Yaml), 1).Value;
            var reader = new Utf8YamlReader(sequence);
            Shelter fromSequence = YamlSerializer.Deserialize<Shelter>(ref reader, options)!;
            Assert.Equal(new[] { typeof(Dog), typeof(Cat), typeof(Animal) }, fromSequence.Pets.Select(p => p.GetType()));
        }

        [Theory]
        [InlineData(YamlSchemaKind.Json)]
        [InlineData(YamlSchemaKind.Failsafe)]
        public void B_Schemas(YamlSchemaKind schema)
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, baseOptions: new YamlSerializerOptions(s_block) { Schema = schema });
            Animal animal = YamlSerializer.Deserialize<Animal>("!dog {\"Name\": \"d\"}", options)!;
            Assert.Equal(("d", typeof(Dog)), (animal.Name, animal.GetType()));

            YamlSerializerOptions mapped = Mapped(new YamlSerializerOptions(s_block) { Schema = schema }, ("!dog", typeof(Dog)));
            Assert.IsType<Dog>(YamlSerializer.Deserialize<Holder>("\"Value\": !dog {\"Name\": \"d\"}", mapped)!.Value);
        }

        [Fact]
        public async Task B_ConstructorAndRequiredDerivedTypes()
        {
            YamlSerializerOptions options = Styled(YamlTypeDiscriminatorStyle.Tag, p =>
            {
                p.DerivedTypes.Add(new YamlDerivedType(typeof(PositionalDog), "pos"));
                p.DerivedTypes.Add(new YamlDerivedType(typeof(RequiredDog), "req"));
            });

            foreach (Func<Task<Animal?>> read in Reads<Animal>("!pos {Name: a, Bark: 2}", options))
            {
                PositionalDog dog = Assert.IsType<PositionalDog>(await read());
                Assert.Equal(("a", 2), (dog.Name, dog.Bark));
            }

            foreach (Func<Task<Animal?>> read in Reads<Animal>("!req\nName: a\nBark: 3\n", options))
            {
                RequiredDog dog = Assert.IsType<RequiredDog>(await read());
                Assert.Equal(("a", 3), (dog.Name, dog.Bark));
            }

            Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Animal>("!req {Name: a}", options));
        }

        // ------------------------------------------------------------------------------------------------------------

        /// <summary>The same read from a string, a 1-byte stream and a 1-byte-segment sequence.</summary>
        private static IEnumerable<Func<Task<T?>>> Reads<T>(string yaml, YamlSerializerOptions options)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
            yield return () => Task.FromResult(YamlSerializer.Deserialize<T>(yaml, options));
            yield return async () => await YamlSerializer.DeserializeAsync<T>(new MemoryStream(utf8), new YamlSerializerOptions(options) { DefaultBufferSize = 1 });
            yield return () =>
            {
                var reader = new Utf8YamlReader(SequenceFactory.Uniform(utf8, 1).Value);
                return Task.FromResult(YamlSerializer.Deserialize<T>(ref reader, options));
            };
        }
    }

    [YamlSerializable(typeof(TagDiscriminatorTests.Holder))]
    [YamlSerializable(typeof(TagDiscriminatorTests.AnimalHolder))]
    [YamlSerializable(typeof(TagDiscriminatorTests.Dog))]
    internal partial class TagDiscriminatorContext : YamlSerializerContext;

    [YamlTagMapping("!dog", typeof(TagDiscriminatorTests.Dog))]
    [YamlTagMapping("tag:example.com,2000:other", typeof(TagDiscriminatorTests.Other))]
    [YamlSerializable(typeof(TagDiscriminatorTests.Holder))]
    [YamlSerializable(typeof(TagDiscriminatorTests.AnimalHolder))]
    [YamlSerializable(typeof(TagDiscriminatorTests.AttributedAnimal))]
    internal partial class TagMappedContext : YamlSerializerContext;
}
