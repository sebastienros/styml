// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;
using SY = SharpYaml;
using SYS = SharpYaml.Serialization;
using YD = YamlDotNet.Serialization;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// M5d, row by row against YamlDotNet 18.1.0
    /// (<c>WithTagMapping</c>) and SharpYaml 3.17.0 (<c>YamlTypeDiscriminatorStyle</c>), run live. Each row gives what
    /// each library yields and what Styml yields; a departure is one of M5d's recorded choices and is
    /// named.
    /// </summary>
    public class TagDiscriminatorParityTests
    {
        [SYS.YamlPolymorphic]
        [SYS.YamlDerivedType(typeof(Dog), "dog")]
        [SYS.YamlDerivedType(typeof(Cat), "cat", Tag = "!kitty")]
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
        }

        public class Holder
        {
            public Animal? A { get; set; }
        }

        public class ObjectHolder
        {
            public object? O { get; set; }
        }

        private static SY.YamlSerializerOptions SharpYaml(string style) => new()
        {
            PolymorphismOptions = new SY.YamlPolymorphismOptions
            {
                DiscriminatorStyle = Enum.Parse<SY.YamlTypeDiscriminatorStyle>(style),
                UnknownDerivedTypeHandling = SY.YamlUnknownDerivedTypeHandling.Fail,
            },
        };

        /// <summary>Styml with the same hierarchy as SharpYaml's attributes (Cat's tag is !kitty).</summary>
        private static YamlSerializerOptions Styml(YamlTypeDiscriminatorStyle style) => new()
        {
            WriteStyle = YamlWriterStyle.Block,
            NewLine = "\n",
            TypeInfoResolver = new DefaultYamlTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(Animal))
                        {
                            typeInfo.PolymorphismOptions = new YamlPolymorphismOptions
                            {
                                DiscriminatorStyle = style,
                                DerivedTypes =
                                {
                                    new YamlDerivedType(typeof(Dog), "dog"),
                                    new YamlDerivedType(typeof(Cat), "cat") { Tag = "!kitty" },
                                },
                            };
                        }
                    },
                },
            },
        };

        private static YamlSerializerOptions StymlMapped(params (string Tag, Type Type)[] mappings)
        {
            var options = new YamlSerializerOptions { WriteStyle = YamlWriterStyle.Block, NewLine = "\n" };
            foreach ((string tag, Type type) in mappings)
            {
                options.TagMappings.Add(tag, type);
            }

            return options;
        }

        /// <summary>The libraries' own output with line feeds (they may follow the platform's line break).</summary>
        private static string Lf(string text) => text.Replace("\r\n", "\n");

        private static string Try(Func<object?> read)
        {
            try
            {
                object? value = read();
                return value switch
                {
                    null => "null",
                    Animal animal => animal.GetType().Name + ":" + animal.Name,
                    string s => s,
                    _ => value.GetType().Name,
                };
            }
            catch (Exception)
            {
                return "throws";
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // SharpYaml's styles

        [Theory]
        // input, style, SharpYaml, Styml (equal unless a departure is named)
        [InlineData("A: !kitty {Name: n}", "Tag", "Cat:n", "Cat:n")]
        [InlineData("A: !kitty {Name: n}", "Both", "Cat:n", "Cat:n")]
        [InlineData("A: !kitty {Name: n}", "Property", "Animal:n", "Animal:n")]
        [InlineData("A: {$type: dog, Name: n}", "Property", "Dog:n", "Dog:n")]
        [InlineData("A: {$type: dog, Name: n}", "Both", "Dog:n", "Dog:n")]
        [InlineData("A: !dog {$type: dog, Name: n}", "Both", "Dog:n", "Dog:n")]
        [InlineData("A: !wolf {Name: n}", "Tag", "throws", "throws")]
        [InlineData("A: !wolf {Name: n}", "Property", "Animal:n", "Animal:n")]
        [InlineData("A: {Name: n}", "Tag", "Animal:n", "Animal:n")]
        [InlineData("A: !<!kitty> {Name: n}", "Tag", "Cat:n", "Cat:n")]
        // Departure: an unset Tag is "!" and the discriminator; SharpYaml has no tag for Dog.
        [InlineData("A: !dog {Name: n}", "Tag", "throws", "Dog:n")]
        // Departure: $type reads under every style; SharpYaml's Tag style ignores it.
        [InlineData("A: {$type: dog, Name: n}", "Tag", "Animal:n", "Dog:n")]
        // Departure: a tag and a $type that disagree are an error; SharpYaml's $type wins.
        [InlineData("A: !kitty {$type: dog, Name: n}", "Both", "Dog:n", "throws")]
        public void SharpYamlStyles_Read(string yaml, string style, string sharpYaml, string styml)
        {
            Assert.Equal(sharpYaml, Try(() => SY.YamlSerializer.Deserialize<Holder>(yaml, SharpYaml(style))!.A));
            Assert.Equal(styml, Try(() => YamlSerializer.Deserialize<Holder>(yaml, Styml(Enum.Parse<YamlTypeDiscriminatorStyle>(style)))!.A));
        }

        [Theory]
        // style, what SharpYaml and Styml write for a Cat (Tag "!kitty") in an Animal member
        [InlineData("Property", "A:\n  $type: cat\n  Name: c\n", "A:\n  $type: cat\n  Name: c\n")]
        [InlineData("Tag", "A: !kitty\n  Name: c\n", "A: !kitty\n  Name: c\n")]
        [InlineData("Both", "A: !kitty\n  $type: cat\n  Name: c\n", "A: !kitty\n  $type: cat\n  Name: c\n")]
        public void SharpYamlStyles_Write(string style, string sharpYaml, string styml)
        {
            Assert.Equal(sharpYaml, Lf(SY.YamlSerializer.Serialize(new Holder { A = new Cat { Name = "c" } }, SharpYaml(style))));
            Assert.Equal(styml, YamlSerializer.Serialize(new Holder { A = new Cat { Name = "c" } }, Styml(Enum.Parse<YamlTypeDiscriminatorStyle>(style))));
        }

        // ------------------------------------------------------------------------------------------------------------
        // YamlDotNet's WithTagMapping and Styml's TagMappings

        [Theory]
        // input, target, YamlDotNet, Styml
        [InlineData("O: !dog {Name: n, Bark: 1}", "object", "Dog:n", "Dog:n")]
        [InlineData("A: !dog {Name: n, Bark: 1}", "Animal", "Dog:n", "Dog:n")]
        [InlineData("O: !<!dog> {Name: n}", "object", "Dog:n", "Dog:n")]
        [InlineData("O: !celsius 21.5", "object", "Double", "Double")]
        [InlineData("O: !<tag:example.com,2000:ex> {Name: n}", "object", "Cat:n", "Cat:n")]
        [InlineData("O: !dog [1]", "object", "throws", "throws")]
        // Departure: an unmapped tag at an object site keeps today's element; YamlDotNet throws.
        [InlineData("O: !wolf {Name: n}", "object", "throws", "YamlElement")]
        public void YamlDotNetMappings_Read(string yaml, string target, string yamlDotNet, string styml)
        {
            YD.IDeserializer ydn = new YD.DeserializerBuilder()
                .WithTagMapping("!dog", typeof(Dog))
                .WithTagMapping("!celsius", typeof(double))
                .WithTagMapping("tag:example.com,2000:ex", typeof(Cat))
                .IgnoreUnmatchedProperties()
                .Build();
            YamlSerializerOptions options = StymlMapped(("!dog", typeof(Dog)), ("!celsius", typeof(double)), ("tag:example.com,2000:ex", typeof(Cat)));

            // SharpYaml's attributes make Animal a hierarchy for SharpYaml only; the Styml options here declare none.
            if (target == "object")
            {
                Assert.Equal(yamlDotNet, Try(() => ydn.Deserialize<ObjectHolder>(yaml).O));
                Assert.Equal(styml, Try(() => YamlSerializer.Deserialize<ObjectHolder>(yaml, options)!.O));
            }
            else
            {
                Assert.Equal(yamlDotNet, Try(() => ydn.Deserialize<Holder>(yaml).A));
                Assert.Equal(styml, Try(() => YamlSerializer.Deserialize<Holder>(yaml, options)!.A));
            }
        }

        [Fact]
        public void YamlDotNetMappings_RefusedEntries()
        {
            // Both refuse a core tag and a tag mapped twice.
            Assert.Throws<ArgumentException>(() => new YD.DeserializerBuilder().WithTagMapping("tag:yaml.org,2002:str", typeof(Dog)));
            Assert.Throws<ArgumentException>(() => StymlMapped(("tag:yaml.org,2002:str", typeof(Dog))));
            Assert.Throws<ArgumentException>(() => new YD.DeserializerBuilder().WithTagMapping("!dog", typeof(Dog)).WithTagMapping("!dog", typeof(Cat)));
            Assert.Throws<ArgumentException>(() => StymlMapped(("!dog", typeof(Dog)), ("!dog", typeof(Cat))));
        }

        [Fact]
        public void YamlDotNetMappings_Write()
        {
            YD.ISerializer ydn = new YD.SerializerBuilder().WithTagMapping("!dog", typeof(Dog)).Build();
            YamlSerializerOptions options = StymlMapped(("!dog", typeof(Dog)));
            var dog = new Dog { Name = "rex" };

            // Agree: an object member and a list of object.
            Assert.Equal("O: !dog\n  Bark: 0\n  Name: rex\n", Lf(ydn.Serialize(new ObjectHolder { O = dog })));
            Assert.Equal("O: !dog\n  Bark: 0\n  Name: rex\n", YamlSerializer.Serialize(new ObjectHolder { O = dog }, options));
            Assert.Equal("- !dog\n  Bark: 0\n  Name: rex\n- 1\n", Lf(ydn.Serialize(new List<object> { dog, 1 })));
            Assert.Equal("- !dog\n  Bark: 0\n  Name: rex\n- 1\n", YamlSerializer.Serialize(new List<object> { dog, 1 }, options));

            // Departure: the tag only where the declared type differs; YamlDotNet writes it at a root Dog too.
            Assert.Equal("!dog\nBark: 0\nName: rex\n", Lf(ydn.Serialize(dog)));
            Assert.Equal("Bark: 0\nName: rex\n", YamlSerializer.Serialize(dog, options));
        }

        // A7: neither library's default nor Styml loads a type a tag names.
        [Fact]
        public void TypeNameTags_AreNotResolved()
        {
            string tag = "!<!" + typeof(Dog).AssemblyQualifiedName!.Replace(" ", "") + ">";
            Assert.Equal("throws", Try(() => new YD.DeserializerBuilder().Build().Deserialize<ObjectHolder>("O: " + tag + " {Name: n}").O));
            Assert.Equal("Animal:n", Try(() => SY.YamlSerializer.Deserialize<Holder>("A: " + tag + " {Name: n}", SharpYaml("Property"))!.A));
            Assert.Equal("YamlElement", Try(() => YamlSerializer.Deserialize<ObjectHolder>("O: " + tag + " {Name: n}", StymlMapped(("!dog", typeof(Dog))))!.O));
            Assert.Equal("Animal:n", Try(() => YamlSerializer.Deserialize<Holder>("A: " + tag + " {Name: n}", Styml(YamlTypeDiscriminatorStyle.Property))!.A));
        }
    }
}
