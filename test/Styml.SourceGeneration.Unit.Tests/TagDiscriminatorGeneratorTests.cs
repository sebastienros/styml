// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.UnitTests
{
    /// <summary>
    /// M5d's own tests, not imported ones: the generator reads
    /// <c>DiscriminatorStyle</c>, <c>YamlDerivedTypeAttribute.Tag</c> and <c>[YamlTagMapping]</c>, emits them only when
    /// set, reports reserved, invalid and duplicate tags and unsupported mapped types, and gives a context with mappings
    /// no serialization fast path.
    /// </summary>
    [ConditionalClass(typeof(PlatformDetection), nameof(PlatformDetection.IsNotX86Process))]
    public class TagDiscriminatorGeneratorTests
    {
        private static YamlSourceGeneratorResult Run(string body, bool validate = false)
        {
            string source = $$"""
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                {{body}}
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            return CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: !validate);
        }

        private static string Generated(YamlSourceGeneratorResult result) =>
            string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));

        private static void AssertCompiles(YamlSourceGeneratorResult result) =>
            Assert.Empty(result.NewCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        [Fact]
        public void StyleTagsAndMappings_AreEmitted()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlTagMapping("!dog", typeof(Dog))]
                    [YamlTagMapping("tag:example.com,2000:cat", typeof(Cat))]
                    [YamlSerializable(typeof(Animal))]
                    [YamlSerializable(typeof(Holder))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Both)]
                    [YamlDerivedType(typeof(Dog), "dog")]
                    [YamlDerivedType(typeof(Cat), "cat", Tag = "tag:example.com,2000:cat")]
                    public class Animal { public string Name { get; set; } }
                    public class Dog : Animal { }
                    public class Cat : Animal { }
                    public class Holder { public object Value { get; set; } }
                """);

            Assert.Empty(result.Diagnostics);
            AssertCompiles(result);
            string generated = Generated(result);
            Assert.Contains("DiscriminatorStyle = global::System.Text.Yaml.Serialization.YamlTypeDiscriminatorStyle.Both,", generated);
            Assert.Contains("{ Tag = \"tag:example.com,2000:cat\" }", generated);
            Assert.Contains("TagMappings =", generated);
            Assert.Contains("{ \"!dog\", typeof(global::HelloWorld.Dog) },", generated);

            // No fast path in a context with mappings.
            Assert.DoesNotContain("SerializeHandler(", generated);

            // The mapped types are in the context.
            result.AssertContainsType("global::HelloWorld.Dog");
            result.AssertContainsType("global::HelloWorld.Cat");
        }

        [Fact]
        public void NothingNew_WhenUnset()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSerializable(typeof(Animal))]
                    [YamlSerializable(typeof(Holder))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    [YamlDerivedType(typeof(Dog), "dog")]
                    public class Animal { public string Name { get; set; } }
                    public class Dog : Animal { }
                    public class Holder { public object Value { get; set; } }
                """);

            Assert.Empty(result.Diagnostics);
            string generated = Generated(result);
            Assert.DoesNotContain("DiscriminatorStyle", generated);
            Assert.DoesNotContain("Tag =", generated);
            Assert.DoesNotContain("TagMappings", generated);
            Assert.Contains("SerializeHandler(", generated);
        }

        [Theory]
        [InlineData("[YamlTagMapping(\"!!str\", typeof(Dog))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"!\", typeof(Dog))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"tag:yaml.org,2002:map\", typeof(Dog))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"dog\", typeof(Dog))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"!a!b\", typeof(Dog))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"!dog\", typeof(Dog))][YamlTagMapping(\"!dog\", typeof(Animal))]", "STYML1251")]
        [InlineData("[YamlTagMapping(\"!dog\", typeof(Dog))][YamlTagMapping(\"!<!dog>\", typeof(Animal))]", "STYML1250")]
        [InlineData("[YamlTagMapping(\"!a\", typeof(IAnimal))]", "STYML1252")]
        [InlineData("[YamlTagMapping(\"!a\", typeof(AbstractAnimal))]", "STYML1252")]
        [InlineData("[YamlTagMapping(\"!a\", typeof(Box<>))]", "STYML1252")]
        [InlineData("[YamlTagMapping(\"!a\", typeof(int?))]", "STYML1252")]
        [InlineData("[YamlTagMapping(\"!dog\", typeof(Dog))][YamlTagMapping(\"!hound\", typeof(Dog))]", "STYML1253")]
        public void TagMappingDiagnostics(string attributes, string id)
        {
            YamlSourceGeneratorResult result = Run($$"""
                    {{attributes}}
                    [YamlSerializable(typeof(Animal))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    public class Animal { public string Name { get; set; } }
                    public class Dog : Animal { }
                    public interface IAnimal { }
                    public abstract class AbstractAnimal { }
                    public class Box<T> { }
                """);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(id, diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.StartsWith("YamlTagMapping(", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        }

        [Theory]
        [InlineData("Property", "[YamlDerivedType(typeof(Dog), \"dog\", Tag = \"!!map\")]", "STYML1250")]
        [InlineData("Tag", "[YamlDerivedType(typeof(Dog), \"dog\", Tag = \"!a!b\")]", "STYML1250")]
        [InlineData("Tag", "[YamlDerivedType(typeof(Dog), \"my dog\")]", "STYML1250")]
        [InlineData("Both", "[YamlDerivedType(typeof(Dog), Tag = \"!dog\")]", "STYML1250")]
        [InlineData("Tag", "[YamlDerivedType(typeof(Dog), \"dog\")][YamlDerivedType(typeof(Cat), \"cat\", Tag = \"!dog\")]", "STYML1251")]
        [InlineData("Both", "[YamlDerivedType(typeof(Dog), \"dog\", Tag = \"!%61\")][YamlDerivedType(typeof(Cat), \"a\")]", "STYML1251")]
        public void HierarchyDiagnostics(string style, string attributes, string id)
        {
            YamlSourceGeneratorResult result = Run($$"""
                    [YamlSerializable(typeof(Animal))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.{{style}})]
                    {{attributes}}
                    public class Animal { public string Name { get; set; } }
                    public class Dog : Animal { }
                    public class Cat : Animal { }
                """);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(id, diagnostic.Id);
        }

        [Fact]
        public void Property_DiscriminatorsThatAreNotTags_AreFine()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSerializable(typeof(Animal))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    [YamlDerivedType(typeof(Dog), "my dog")]
                    [YamlDerivedType(typeof(Cat), "a!b")]
                    public class Animal { public string Name { get; set; } }
                    public class Dog : Animal { }
                    public class Cat : Animal { }
                """);

            Assert.Empty(result.Diagnostics);
        }
    }
}
