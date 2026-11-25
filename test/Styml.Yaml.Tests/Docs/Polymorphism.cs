// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;
using static System.Text.Yaml.Tests.Docs.DocSample;

namespace System.Text.Yaml.Tests.Docs
{
    // The samples of guide/polymorphism.md and guide/references.md.
    public partial class PolymorphismSamples
    {
        // docs: poly-types
        [YamlDerivedType(typeof(Email), "email")]
        [YamlDerivedType(typeof(Sms), "sms")]
        public abstract class Notification
        {
            public string To { get; set; } = "";
        }

        public class Email : Notification
        {
            public string Subject { get; set; } = "";
        }

        public class Sms : Notification
        {
            public bool Flash { get; set; }
        }
        // docs: end

        [Fact]
        public void Property()
        {
            // docs: poly-property yaml
            Notification[] notifications =
            [
                new Email { To = "ada@example.org", Subject = "Build passed" },
                new Sms { To = "+15550100", Flash = true },
            ];
            string yaml = YamlSerializer.Serialize(notifications);
            Notification[] back = YamlSerializer.Deserialize<Notification[]>(yaml)!;
            string types = string.Join(", ", back.Select(n => n.GetType().Name));
            // docs: end
            Check("poly-property", yaml + "\n# " + types + "\n");
        }

        // docs: poly-tag-types
        [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
        [YamlDerivedType(typeof(Http), "http")]
        [YamlDerivedType(typeof(Exec), "exec", Tag = "!command")]
        public abstract record Check;

        public record Http(string Path, int Port) : Check;

        public record Exec(string[] Command) : Check;
        // docs: end

        [Fact]
        public void Tag()
        {
            // docs: poly-tag yaml
            var checks = new Dictionary<string, Check>
            {
                ["liveness"] = new Http("/health", 8080),
                ["readiness"] = new Exec(["cat", "/tmp/ready"]),
            };
            string yaml = YamlSerializer.Serialize(checks);
            var back = YamlSerializer.Deserialize<Dictionary<string, Check>>(yaml)!;
            // docs: end
            Assert.IsType<Exec>(back["readiness"]);
            Check("poly-tag", yaml);
        }

        // docs: poly-name-types
        [YamlPolymorphic(TypeDiscriminatorPropertyName = "kind",
            UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBaseType)]
        [YamlDerivedType(typeof(Circle), "circle")]
        public class Shape
        {
            public string Color { get; set; } = "black";
        }

        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        public class Square : Shape
        {
            public double Side { get; set; }
        }
        // docs: end

        [Fact]
        public void Name()
        {
            // docs: poly-name yaml
            Shape[] shapes = [new Circle { Radius = 2 }, new Square { Side = 3, Color = "red" }];
            string yaml = YamlSerializer.Serialize(shapes);
            // docs: end
            Check("poly-name", yaml);
        }

        // docs: poly-contract-types
        // Types of a library you don't own: no attributes on them.
        public class Vehicle { public int Wheels { get; set; } }

        public class Bike : Vehicle { public bool Electric { get; set; } }

        public class Truck : Vehicle { public double Load { get; set; } }
        // docs: end

        [Fact]
        public void Contract()
        {
            // docs: poly-contract yaml
            var options = new YamlSerializerOptions
            {
                TypeInfoResolver = new DefaultYamlTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(Vehicle))
                            {
                                typeInfo.PolymorphismOptions = new YamlPolymorphismOptions
                                {
                                    DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                                    DerivedTypes =
                                    {
                                        new YamlDerivedType(typeof(Bike), "bike"),
                                        new YamlDerivedType(typeof(Truck), "truck"),
                                    },
                                };
                            }
                        },
                    },
                },
            };
            string yaml = YamlSerializer.Serialize<Vehicle[]>([new Bike { Wheels = 2, Electric = true }, new Truck { Wheels = 6, Load = 7.5 }], options);
            // docs: end
            Check("poly-contract", yaml);
        }

        // docs: tag-mappings-types
        public record Coordinate(double Lat, double Lon);

        public record Color(string Hex);
        // docs: end

        [Fact]
        public void TagMappings()
        {
            // docs: tag-mappings yaml
            var options = new YamlSerializerOptions();
            options.TagMappings.Add("!geo", typeof(Coordinate));
            options.TagMappings.Add("tag:example.org,2026:color", typeof(Color));

            var values = YamlSerializer.Deserialize<List<object>>("""
                - !geo {Lat: 48.85, Lon: 2.35}
                - !<tag:example.org,2026:color> {Hex: '#7c3aed'}
                - !System.IO.FileInfo {FileName: /etc/passwd}
                - plain text
                """, options)!;
            string types = string.Join(", ", values.Select(v => v.GetType().Name));
            string yaml = YamlSerializer.Serialize<object[]>([new Coordinate(51.5, -0.12), new Color("#2563eb")], options);
            // docs: end
            Check("tag-mappings", "# " + types + "\n" + yaml);
        }

        // docs: tag-context
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(Coordinate))]
        [YamlTagMapping("!geo", typeof(Coordinate))]
        public partial class GeoContext : YamlSerializerContext;
        // docs: end

        [Fact]
        public void TagContext()
        {
            List<object> values = YamlSerializer.Deserialize("- !geo {Lat: 1, Lon: 2}", GeoContext.Default.ListObject)!;
            Assert.IsType<Coordinate>(values[0]);
        }

        // docs: refs-types
        public class Person
        {
            public string Name { get; set; } = "";

            public Address? Home { get; set; }

            public Person? Manager { get; set; }
        }

        public class Address
        {
            public string City { get; set; } = "";
        }
        // docs: end

        [Fact]
        public void Preserve()
        {
            if (BlockDefaultOnly())
            {
                return;
            }

            // docs: refs-preserve yaml
            var paris = new Address { City = "Paris" };
            var boss = new Person { Name = "Grace", Home = paris };
            var team = new List<Person> { boss, new() { Name = "Ada", Home = paris, Manager = boss } };
            var options = new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            string yaml = YamlSerializer.Serialize(team, options);
            List<Person> back = YamlSerializer.Deserialize<List<Person>>(yaml)!;
            bool shared = ReferenceEquals(back[1].Manager, back[0]) && ReferenceEquals(back[1].Home, back[0].Home);
            // docs: end
            Check("refs-preserve", yaml + "\n# shared after reading: " + shared + "\n");
        }

        [Fact]
        public void Cycles()
        {
            // docs: refs-cycles text
            var ada = new Person { Name = "Ada" };
            ada.Manager = ada;
            string error = Describe(() => YamlSerializer.Serialize(ada));
            string ignored = YamlSerializer.Serialize(ada, new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles });
            string preserved = YamlSerializer.Serialize(ada, new YamlSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve });
            // docs: end
            Check("refs-cycles", "# " + error.Substring(0, error.IndexOf('.') + 1) + "\n" + ignored + "---\n" + preserved);
        }

        [Fact]
        public void Aliases()
        {
            // docs: refs-aliases text
            string yaml = """
                Defaults: &office {City: Lyon}
                People:
                - {Name: Ada, Home: *office}
                - {Name: Alan, Home: *office}
                """;
            Team team = YamlSerializer.Deserialize<Team>(yaml)!;
            bool same = ReferenceEquals(team.People[0].Home, team.People[1].Home) && ReferenceEquals(team.Defaults, team.People[0].Home);
            // docs: end
            Check("refs-aliases", $"{team.People[0].Home!.City}, {team.People[1].Home!.City}, same instance: {same}\n", defaultStyle: false);
        }

        // docs: refs-team
        public record Team(Address Defaults, List<Person> People);
        // docs: end

        [Fact]
        public void AliasBomb()
        {
            // docs: refs-bomb text
            string bomb = """
                a: &a [lol, lol, lol, lol, lol, lol, lol, lol, lol]
                b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a]
                c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b]
                d: &d [*c, *c, *c, *c, *c, *c, *c, *c, *c]
                e: [*d, *d, *d, *d, *d, *d, *d, *d, *d]
                """;
            string error = Describe(() => YamlSerializer.Deserialize<Dictionary<string, object>>(bomb));
            // docs: end
            Check("refs-bomb", error + "\n", defaultStyle: false);
        }

        [Fact]
        public void MergeKeys()
        {
            // docs: merge-keys yaml
            string yaml = """
                base: &base
                  image: nginx:1.27
                  replicas: 2
                services:
                  web:
                    <<: *base
                    replicas: 4
                  admin:
                    <<: [*base, {port: 8081}]
                """;
            var options = new YamlSerializerOptions { ResolveMergeKeys = true };
            YamlElement root = YamlSerializer.Deserialize<YamlElement>(yaml, options);
            string merged = YamlSerializer.Serialize(root.GetProperty("services"));
            // docs: end
            Check("merge-keys", merged);
        }
    }
}
