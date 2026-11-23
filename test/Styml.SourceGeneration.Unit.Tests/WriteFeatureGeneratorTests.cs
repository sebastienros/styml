// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.UnitTests
{
    /// <summary>
    /// M5f's own tests, not imported ones: the generator reads the
    /// attribute's YAML options and emits each only when set (DuplicateKeyHandling after AllowDuplicateProperties), reads
    /// <c>[YamlComment]</c> and <c>[YamlScalarStyle]</c> into the property values, sorts the fast path's members under
    /// <c>MappingOrder = Sorted</c>, and gives a type that uses M5f's member features no fast path.
    /// </summary>
    [ConditionalClass(typeof(PlatformDetection), nameof(PlatformDetection.IsNotX86Process))]
    public class WriteFeatureGeneratorTests
    {
        private static YamlSourceGeneratorResult Run(string body)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                {{body}}
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            return CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
        }

        private static string Generated(YamlSourceGeneratorResult result) =>
            string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));

        private static void AssertCompiles(YamlSourceGeneratorResult result) =>
            Assert.Empty(result.NewCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        [Fact]
        public void AttributeOptions_AreEmitted()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSourceGenerationOptions(
                        WriteStyle = YamlWriterStyle.Block,
                        IndentSequences = true,
                        MultiLineStringStyle = YamlScalarStyle.DoubleQuoted,
                        DefaultScalarStyle = YamlScalarStyle.SingleQuoted,
                        IgnoreSourceStyles = true,
                        MaxLineWidth = 40,
                        ExpandSequenceItems = true,
                        QuoteForLenientReaders = true,
                        Schema = YamlSchemaKind.Yaml11,
                        AllowDuplicateProperties = true,
                        DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins,
                        MappingOrder = YamlMappingOrder.Sorted,
                        MaxAliasExpansion = 7,
                        ResolveMergeKeys = true,
                        AcceptLenientScalars = true,
                        DisallowImplicitCollectionKeys = true,
                        DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
                    [YamlSerializable(typeof(Poco))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    public class Poco { public int B { get; set; } public int A { get; set; } }
                """);

            AssertCompiles(result);
            string generated = Generated(result);
            Assert.Contains("WriteStyle = global::System.Text.Yaml.YamlWriterStyle.Block,", generated);
            Assert.Contains("IndentSequences = true,", generated);
            Assert.Contains("MultiLineStringStyle = global::System.Text.Yaml.YamlScalarStyle.DoubleQuoted,", generated);
            Assert.Contains("DefaultScalarStyle = global::System.Text.Yaml.YamlScalarStyle.SingleQuoted,", generated);
            Assert.Contains("IgnoreSourceStyles = true,", generated);
            Assert.Contains("MaxLineWidth = 40,", generated);
            Assert.Contains("ExpandSequenceItems = true,", generated);
            Assert.Contains("QuoteForLenientReaders = true,", generated);
            Assert.Contains("Schema = global::System.Text.Yaml.YamlSchemaKind.Yaml11,", generated);
            Assert.Contains("MappingOrder = global::System.Text.Yaml.Serialization.YamlMappingOrder.Sorted,", generated);
            Assert.Contains("MaxAliasExpansion = 7,", generated);
            Assert.Contains("ResolveMergeKeys = true,", generated);
            Assert.Contains("AcceptLenientScalars = true,", generated);
            Assert.Contains("DisallowImplicitCollectionKeys = true,", generated);
            Assert.Contains("DefaultIgnoreCondition = global::System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingEmptyCollection,", generated);

            // The enum wins when both are set: it is assigned after the bool.
            int allow = generated.IndexOf("AllowDuplicateProperties = true,", StringComparison.Ordinal);
            int handling = generated.IndexOf("DuplicateKeyHandling = global::System.Text.Yaml.YamlDuplicateKeyHandling.FirstWins,", StringComparison.Ordinal);
            Assert.True(allow >= 0 && handling > allow);

            // A context whose default ignore condition omits empty collections has no fast path.
            Assert.DoesNotContain("PocoSerializeHandler(", generated);
        }

        [Fact]
        public void NothingNew_WhenUnset()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSourceGenerationOptions(WriteIndented = true)]
                    [YamlSerializable(typeof(Poco))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    public class Poco { public string Name { get; set; } }
                """);

            AssertCompiles(result);
            string generated = Generated(result);
            foreach (string name in new[] { "WriteStyle", "IndentSequences", "MultiLineStringStyle", "DefaultScalarStyle", "IgnoreSourceStyles", "MaxLineWidth", "ExpandSequenceItems", "QuoteForLenientReaders", "Schema =", "DuplicateKeyHandling", "MappingOrder", "MaxAliasExpansion", "ResolveMergeKeys", "AcceptLenientScalars", "DisallowImplicitCollectionKeys", "Comment =", "ScalarStyle =" })
            {
                Assert.DoesNotContain(name, generated);
            }

            Assert.Contains("PocoSerializeHandler(", generated);
        }

        [Fact]
        public void MemberAttributes_InThePropertyValues_AndNoFastPath()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSerializable(typeof(Commented))]
                    [YamlSerializable(typeof(Plain))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    public class Commented
                    {
                        [YamlComment("the \"name\"\nsecond line")]
                        [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
                        public string Name { get; set; }

                        [YamlIgnore(Condition = YamlIgnoreCondition.WhenWritingEmptyCollection)]
                        public List<int> Items { get; set; }
                    }

                    public class Plain { public string Name { get; set; } }
                """);

            AssertCompiles(result);
            string generated = Generated(result);
            Assert.Contains("Comment = \"the \\\"name\\\"\\nsecond line\",", generated);
            Assert.Contains("ScalarStyle = global::System.Text.Yaml.YamlScalarStyle.SingleQuoted,", generated);
            Assert.Contains("IgnoreCondition = global::System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingEmptyCollection,", generated);
            Assert.DoesNotContain("CommentedSerializeHandler(", generated);
            Assert.Contains("PlainSerializeHandler(", generated);
        }

        [Fact]
        public void Sorted_FastPathMembers()
        {
            YamlSourceGeneratorResult result = Run("""
                    [YamlSourceGenerationOptions(MappingOrder = YamlMappingOrder.Sorted)]
                    [YamlSerializable(typeof(Poco))]
                    internal partial class YamlContext : YamlSerializerContext { }

                    public class Poco
                    {
                        public int b { get; set; }
                        public int B { get; set; }
                        public int a { get; set; }
                        [YamlPropertyOrder(-1)] public int z { get; set; }
                    }
                """);

            AssertCompiles(result);
            string handler = Generated(result);
            handler = handler.Substring(handler.IndexOf("PocoSerializeHandler(", StringComparison.Ordinal));
            int z = handler.IndexOf("((global::HelloWorld.Poco)value).z", StringComparison.Ordinal);
            int upperB = handler.IndexOf("((global::HelloWorld.Poco)value).B", StringComparison.Ordinal);
            int a = handler.IndexOf("((global::HelloWorld.Poco)value).a", StringComparison.Ordinal);
            int b = handler.IndexOf("((global::HelloWorld.Poco)value).b", StringComparison.Ordinal);
            if (z < 0)
            {
                z = handler.IndexOf("value.z", StringComparison.Ordinal);
                upperB = handler.IndexOf("value.B", StringComparison.Ordinal);
                a = handler.IndexOf("value.a", StringComparison.Ordinal);
                b = handler.IndexOf("value.b", StringComparison.Ordinal);
            }

            Assert.True(z >= 0 && z < upperB && upperB < a && a < b, $"{z} {upperB} {a} {b}");
        }
    }
}
