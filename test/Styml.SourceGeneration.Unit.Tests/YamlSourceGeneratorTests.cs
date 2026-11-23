// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace System.Text.Yaml.SourceGeneration.UnitTests
{
    [ActiveIssue("https://github.com/dotnet/runtime/issues/58226", TestPlatforms.Browser)]
    [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/71962", ~RuntimeConfiguration.Release)]
    [SkipOnMono("https://github.com/dotnet/runtime/issues/92467")]
    [ConditionalClass(typeof(PlatformDetection), nameof(PlatformDetection.IsNotX86Process))] // https://github.com/dotnet/runtime/issues/71962
    public class GeneratorTests(ITestOutputHelper logger)
    {
        [Fact]
        public void TypeDiscoveryPrimitivePOCO()
        {
            string source = """
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        [YamlSerializable(typeof(HelloWorld.MyType))]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }

                        public class MyType
                        {
                            public int PublicPropertyInt { get; set; }
                            public string PublicPropertyString { get; set; }
                            private int PrivatePropertyInt { get; set; }
                            private string PrivatePropertyString { get; set; }

                            public double PublicDouble;
                            public char PublicChar;
                            private double PrivateDouble;
                            private char PrivateChar;

                            public void MyMethod() { }
                            public void MySecondMethod() { }

                            public void UsePrivates()
                            {
                                PrivateDouble = 0;
                                PrivateChar = ' ';
                                double d = PrivateDouble;
                                char c = PrivateChar;
                            }
                        }
                    }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(5, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.MyType");
        }

        [Fact]
        public void TypeDiscoveryPrimitiveExternalPOCO()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLocationCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        [YamlSerializable(typeof(HelloWorld.MyType))]
                        [YamlSerializable(typeof(ReferencedAssembly.Location))]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }

                        public class MyType
                        {
                            public int PublicPropertyInt { get; set; }
                            public string PublicPropertyString { get; set; }
                            private int PrivatePropertyInt { get; set; }
                            private string PrivatePropertyString { get; set; }

                            public double PublicDouble;
                            public char PublicChar;
                            private double PrivateDouble;
                            private char PrivateChar;

                            public void MyMethod() { }
                            public void MySecondMethod() { }
                            public void UsePrivates()
                            {
                                PrivateDouble = 0;
                                PrivateChar = ' ';
                                double x = PrivateDouble;
                                string s = PrivateChar.ToString();
                            }
                        }
                    }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(6, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.MyType");
            result.AssertContainsType("global::ReferencedAssembly.Location");
        }

        [Fact]
        public void TypeDiscoveryWithRenamedAttribute()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLocationCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);
            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            string source = """
                    using System.Text.Yaml.Serialization;

                    using @YamlSerializable = System.Runtime.Serialization.CollectionDataContractAttribute ;
                    using AliasedAttribute = System.Text.Yaml.Serialization.YamlSerializableAttribute;

                    namespace HelloWorld
                    {

                        [AliasedAttribute(typeof(HelloWorld.MyType))]
                        [AliasedAttribute(typeof(ReferencedAssembly.Location))]
                        [@YamlSerializable]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }

                        public class MyType
                        {
                            public int PublicPropertyInt { get; set; }
                            public string PublicPropertyString { get; set; }
                            private int PrivatePropertyInt { get; set; }
                            private string PrivatePropertyString { get; set; }

                            public double PublicDouble;
                            public char PublicChar;
                            private double PrivateDouble;
                            private char PrivateChar;

                            public void MyMethod() { }
                            public void MySecondMethod() { }
                            public void UsePrivates()
                            {
                                PrivateDouble = 0;
                                PrivateChar = ' ';
                                double d = PrivateDouble;
                                char c = PrivateChar;
                            }
                        }
                    }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(6, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.MyType");
            result.AssertContainsType("global::ReferencedAssembly.Location");
        }

        [Theory]
        [InlineData("System.Text.Yaml", true)]
        [InlineData("System.Text.Yaml.Not", true)]
        [InlineData("System.Text.Yaml", false)]
        [InlineData("System.Text.Yaml.Not", false)]
        public static void LocalYamlSerializableAttributeExpectedShape(string assemblyName, bool includeSTJ)
        {
            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                namespace System.Text.Yaml.Serialization
                {
                    [YamlSerializable(typeof(int))]
                    [YamlSerializable(typeof(string), TypeInfoPropertyName = "Str")]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                    public sealed class YamlSerializableAttribute : YamlAttribute
                    {
                        public string TypeInfoPropertyName { get; set; }

                        public YamlSerializableAttribute(Type type) { }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences: null, assemblyName, includeSTJ: includeSTJ);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            if (includeSTJ)
            {
                result.AssertContainsType("int");
                result.AssertContainsType("string");
            }
            else
            {
                Assert.Empty(result.AllGeneratedTypes);
            }

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void NameClashCompilation()
        {
            Compilation compilation = CompilationHelper.CreateRepeatedLocationsCompilation();

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            // Make sure compilation was successful.
            result.Diagnostics.AssertMaxSeverity(DiagnosticSeverity.Warning);
            result.NewCompilation.GetDiagnostics().AssertMaxSeverity(DiagnosticSeverity.Warning);
        }

        [Fact]
        public void CollectionDictionarySourceGeneration()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedHighLowTempsCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                using System;
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;
    
                namespace HelloWorld
                {
                    [YamlSerializable(typeof(HelloWorld.WeatherForecastWithPOCOs))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class WeatherForecastWithPOCOs
                    {
                        public DateTimeOffset Date { get; set; }
                        public int TemperatureCelsius { get; set; }
                        public string Summary { get; set; }
                        public string SummaryField;
                        public List<DateTimeOffset> DatesAvailable { get; set; }
                        public Dictionary<string, HighLowTemps> TemperatureRanges { get; set; }
                        public string[] SummaryWords { get; set; }
                    }
                }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void ContextTypeNotInNamespace()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                [YamlSerializable(typeof(MyType))]
                internal partial class YamlContext : YamlSerializerContext
                {
                }

                public class MyType
                {
                    public int PublicPropertyInt { get; set; }
                    public string PublicPropertyString { get; set; }
                    private int PrivatePropertyInt { get; set; }
                    private string PrivatePropertyString { get; set; }

                    public double PublicDouble;
                    public char PublicChar;
                    private double PrivateDouble;
                    private char PrivateChar;

                    public void MyMethod() { }
                    public void MySecondMethod() { }

                    public void UsePrivates()
                    {
                        PrivateDouble = 0;
                        PrivateChar = ' ';
                        double d = PrivateDouble;
                        char c = PrivateChar;
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(5, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::MyType");
            result.AssertContainsType("int");
            result.AssertContainsType("string");
            result.AssertContainsType("double");
            result.AssertContainsType("char");
        }

        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)] // Netfx lacks IsExternalInit class needed for records
        public void Record()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLibRecordCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(AppRecord))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public record AppRecord(int Id)
                    {
                        public string Address1 { get; set; }
                        public string Address2 { get; set; }
                        public string City { get; set; }
                        public string State { get; set; }
                        public string PostalCode { get; set; }
                        public string Name { get; set; }
                        [YamlInclude]
                        public string PhoneNumber;
                        [YamlInclude]
                        public string Country;
                    }
                }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(3, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.AppRecord");
            result.AssertContainsType("string");
            result.AssertContainsType("int");
        }

        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)] // Netfx lacks IsExternalInit class needed for records
        public void RecordInExternalAssembly()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLibRecordCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(LibRecord))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(3, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::ReferencedAssembly.LibRecord");
            result.AssertContainsType("string");
            result.AssertContainsType("int");
        }

        [Fact]
        public void RecordDerivedFromRecordInExternalAssembly()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedSimpleLibRecordCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(AppRecord))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    internal record AppRecord : LibRecord
                    {
                        public string ExtraData { get; set; }
                    }
                }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(3, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.AppRecord");
            result.AssertContainsType("string");
            result.AssertContainsType("int");
        }

        // TODO: add test guarding against (de)serializing static classes.

        [Fact]
        public void TestMultipleDefinitions()
        {
            // Adding a dependency to an assembly that has internal definitions of public types
            // should not result in a collision and break generation.
            // Verify usage of the extension GetBestTypeByMetadataName(this Compilation) instead of Compilation.GetTypeByMetadataName().
            var referencedSource = """
                namespace System.Text.Yaml.Serialization
                {
                    internal class YamlSerializerContext { }
                    internal class YamlSerializableAttribute { }
                    internal class YamlSourceGenerationOptionsAttribute { }
                }
                """;

            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateCompilation(referencedSource);

            // Obtain the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            // Generate the code
            string source = """
                using System.Text.Yaml.Serialization;
                namespace HelloWorld
                {
                    [YamlSerializable(typeof(HelloWorld.MyType))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class MyType
                    {
                        public int MyInt { get; set; }
                    }
                }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };
            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            // Should find the generated type.
            Assert.Equal(2, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::HelloWorld.MyType");
            result.AssertContainsType("int");
        }

        [Fact]
        public void NoWarningsDueToObsoleteMembers()
        {
            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                namespace Test
                {
                    [YamlSerializable(typeof(ClassWithObsolete))]
                    public partial class YamlContext : YamlSerializerContext { }

                    public class ClassWithObsolete
                    {
                        [Obsolete("This is a test")]
                        public bool Test { get; set; }

                        [Obsolete]
                        public bool Test2 { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void NoErrorsWhenUsingReservedCSharpKeywords()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace Test
                {
                    [YamlSerializable(typeof(ClassWithPropertiesAndFieldsThatAreReservedKeywords))]
                    public partial class YamlContext : YamlSerializerContext { }

                    public class ClassWithPropertiesAndFieldsThatAreReservedKeywords
                    {
                        public int @class;
                        public string @event { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void EquivalentTupleDeclarations_DoNotConflict()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                #nullable enable

                namespace HelloWorld
                {
                    [YamlSerializable(typeof((string? Label1, string Label2, int Integer)))]
                    [YamlSerializable(typeof((string, string, int)))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            // Should find the generated type.
            Assert.Equal(3, result.AllGeneratedTypes.Count());
            result.AssertContainsType("(string, string, int)");
            result.AssertContainsType("string");
            result.AssertContainsType("int");
        }

        [Fact]
        public void NoErrorsWhenUsingTypesWithMultipleEqualsOperators()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/103515
            string source = """
                using System.Text.Yaml.Serialization;
                
                namespace Test
                {
                    public class Foo
                    {
                        public override bool Equals(object obj) => false;
                
                        public static bool operator ==(Foo left, Foo right) => false;
                        public static bool operator !=(Foo left, Foo right) => false;
                    
                        public static bool operator ==(Foo left, string right) => false;
                        public static bool operator !=(Foo left, string right) => false;
                    
                        public override int GetHashCode() => 1;
                    }

                    [YamlSourceGenerationOptions(WriteIndented = true)]
                    [YamlSerializable(typeof(Foo))]
                    internal partial class YamlSourceGenerationContext : YamlSerializerContext
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void NoErrorsWhenUsingIgnoredReservedCSharpKeywords()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace Test
                {
                    [YamlSerializable(typeof(ClassWithPropertyNameThatIsAReservedKeyword))]
                    public partial class YamlContext : YamlSerializerContext { }

                    public class ClassWithPropertyNameThatIsAReservedKeyword
                    {
                        [YamlIgnore]
                        public string @event { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void UseUnderlyingTypeConverterForNullableType()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLocationCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = """
                    using System;
                    using System.Text.Yaml;
                    using System.Text.Yaml.Serialization;

                    namespace Test
                    {
                        [YamlSourceGenerationOptions]
                        [YamlSerializable(typeof(Sample))]
                        public partial class SourceGenerationContext : YamlSerializerContext
                        {
                        }
                        public class Sample
                        {
                            [YamlConverter(typeof(DateTimeOffsetToTimestampYamlConverter))]
                            public DateTimeOffset Start { get; set; }
                            [YamlConverter(typeof(DateTimeOffsetToTimestampYamlConverter))]
                            public DateTimeOffset? End { get; set; } // Without this property, this is fine
                        }
                        public class DateTimeOffsetToTimestampYamlConverter : YamlConverter<DateTimeOffset>
                        {
                            internal const long TicksPerMicroseconds = 10;
                            public override DateTimeOffset Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                            {
                                var value = reader.GetInt64();
                                return new DateTimeOffset(value * TicksPerMicroseconds, TimeSpan.Zero);
                            }
                            public override void Write(Utf8YamlWriter writer, DateTimeOffset value, YamlSerializerOptions options)
                            {
                                writer.WriteNumberValue(value.Ticks / TicksPerMicroseconds);
                            }
                        }
                    }
                """;

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(3, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::Test.Sample");
            result.AssertContainsType("global::System.DateTimeOffset");
            result.AssertContainsType("global::System.DateTimeOffset?");
        }

        [Fact]
        public void VariousGenericSerializableTypesAreSupported()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(Dictionary<string, string>))]
                    [YamlSerializable(typeof(HelloWorld.MyClass.NestedGenericClass<string>))]
                    [YamlSerializable(typeof(HelloWorld.MyGenericClass<string>.NestedClass))]
                    [YamlSerializable(typeof(HelloWorld.MyGenericClass<string>.NestedGenericClass<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class MyClass
                    {
                        public class NestedGenericClass<T>
                        {
                        }
                    }

                    public class MyGenericClass<T1>
                    {
                        public class NestedClass
                        {
                        }
                        public class NestedGenericClass<T2>
                        {
                        }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Equal(5, result.AllGeneratedTypes.Count());
            result.AssertContainsType("global::System.Collections.Generic.Dictionary<string, string>");
            result.AssertContainsType("global::HelloWorld.MyClass.NestedGenericClass<string>");
            result.AssertContainsType("global::HelloWorld.MyGenericClass<string>.NestedClass");
            result.AssertContainsType("global::HelloWorld.MyGenericClass<string>.NestedGenericClass<int>");
            result.AssertContainsType("string");
        }

        [Theory]
        [InlineData("public sealed partial class MySealedClass")]
        [InlineData("public partial class MyGenericClass<T>")]
        [InlineData("public partial interface IMyInterface")]
        [InlineData("public partial interface IMyGenericInterface<T, U>")]
        [InlineData("public partial struct MyStruct")]
        [InlineData("public partial struct MyGenericStruct<T>")]
        [InlineData("public ref partial struct MyRefStruct")]
        [InlineData("public ref partial struct MyGenericRefStruct<T>")]
        [InlineData("public readonly partial struct MyReadOnlyStruct")]
        [InlineData("public readonly ref partial struct MyReadOnlyRefStruct")]
#if ROSLYN4_0_OR_GREATER && NET
        [InlineData("public partial record MyRecord(int x)", LanguageVersion.CSharp10)]
        [InlineData("public partial record struct MyRecordStruct(int x)", LanguageVersion.CSharp10)]
#endif
        public void NestedContextsAreSupported(string containingTypeDeclarationHeader, LanguageVersion? languageVersion = null)
        {
            string source = $$"""
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    {{containingTypeDeclarationHeader}}
                    {
                        [YamlSerializable(typeof(MyClass))]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }
                    }

                    public class MyClass
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: CompilationHelper.CreateParseOptions(languageVersion));
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void DoesNotWarnOnNullabilityMismatch()
        {
            string source = $$"""
                    using System.Collections.Generic;
                    using System.Text.Yaml;
                    using System.Text.Yaml.Serialization;
                    #nullable enable

                    namespace HelloWorld
                    {
                        public static class MyClass
                        {
                            public static string Test()
                            {
                                Dictionary<int, string?> values = new();
                                return YamlSerializer.Serialize(values, YamlContext.Default.DictionaryInt32String);
                            }
                        }

                        [YamlSerializable(typeof(Dictionary<int, string>))]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

#if ROSLYN4_4_OR_GREATER && NET
        [Fact]
        public void ShadowedMemberInitializers()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                public record Base
                {
                    public string Value { get; init; }
                }
                public record Derived : Base
                {
                    public new string Value { get; init; }
                }

                [YamlSerializable(typeof(Derived))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: CompilationHelper.CreateParseOptions(LanguageVersion.CSharp11));
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }
#endif

        [Fact]
        public void FastPathWithReservedKeywordPropertyNames_CompilesSuccessfully()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/98050

            string source = """
                using System.Text.Yaml.Serialization;

                public class Model
                {
                    public string type { get; set; }
                    public string alias { get; set; }
                    public string @class { get; set; }
                    public string @struct { get; set; }
                }

                [YamlSourceGenerationOptions(DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull)]
                [YamlSerializable(typeof(Model))]
                internal partial class ModelContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void InitOnlyPropertyWithReservedKeywordName_CompilesSuccessfully()
        {
            // Verbatim identifiers like @else should be correctly handled in property initializers.

            string source = """
                using System.Text.Yaml.Serialization;

                public class MyClass
                {
                    public string @else { get; init; }
                }

                [YamlSerializable(typeof(MyClass))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void RefStructPropertyWithYamlIgnore_CompilesSuccessfully()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/98590

            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [YamlIgnore]
                    public ReadOnlySpan<char> Values => "abc".AsSpan();
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

#if ROSLYN4_4_OR_GREATER && NET
        [Fact]
        public void PropertyWithExperimentalType_YamlIgnore_CompilesSuccessfully()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP001")]
                public class ExperimentalType
                {
                    public int Value { get; set; }
                }

                public class MyPoco
                {
                    [YamlIgnore]
                #pragma warning disable EXP001
                    public ExperimentalType ExpType { get; set; }
                #pragma warning restore EXP001
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void PocoWithExperimentalProperty_YamlIgnore_CompilesSuccessfully()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [Experimental("EXP001"), YamlIgnore]
                    public int ExperimentalProperty { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Theory]
        [MemberData(nameof(ExperimentalSuppressionSources))]
        public void ExperimentalMemberUsage_IsSuppressedByDefault(string scenario, string source)
        {
            _ = scenario;
            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void ExperimentalConverterTypeArgument_IsSuppressed()
        {
            string source = """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP_TEST")]
                public class MyExperimentalType { }

                public class MyConverter<T> : YamlConverter<object>
                {
                    public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => null;
                    public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options) { }
                }

                public class MyPoco
                {
                    #pragma warning disable EXP_TEST
                    [YamlConverter(typeof(MyConverter<MyExperimentalType>))]
                    #pragma warning restore EXP_TEST
                    public object Prop { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            // Default validation asserts the post-generator compilation is warning/error-free, which is the
            // real proof of suppression; the explicit check documents that EXP_TEST specifically is gone.
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            Assert.Empty(result.NewCompilation.GetDiagnostics().Where(d => d.Id == "EXP_TEST"));
        }

        [Fact]
        public void ExperimentalConverterConstructor_IsSuppressed()
        {
            string source = """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                public class WidgetConverter : YamlConverter<ConvertedWidget>
                {
                    [Experimental("EXP_CONV_CTOR")]
                    public WidgetConverter() { }

                    public override ConvertedWidget Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => new ConvertedWidget();
                    public override void Write(Utf8YamlWriter writer, ConvertedWidget value, YamlSerializerOptions options) { }
                }

                [YamlConverter(typeof(WidgetConverter))]
                public class ConvertedWidget
                {
                    public int X { get; set; }
                }

                [YamlSerializable(typeof(ConvertedWidget))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            TypeGenerationSpec convertedWidget = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "ConvertedWidget");
            Assert.Equal(new[] { "EXP_CONV_CTOR" }, convertedWidget.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void ExperimentalTypeClassifierFactoryConstructor_IsSuppressed()
        {
            string source = """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                [YamlPolymorphic(TypeClassifier = typeof(AnimalClassifierFactory))]
                [YamlDerivedType(typeof(Dog), "dog")]
                public class Animal
                {
                    public int Age { get; set; }
                }

                public class Dog : Animal
                {
                    public int Bark { get; set; }
                }

                public sealed class AnimalClassifierFactory : YamlTypeClassifierFactory
                {
                    [Experimental("EXP_CLS_CTOR")]
                    public AnimalClassifierFactory() { }

                    public override bool CanClassify(YamlTypeClassifierContext context) => true;
                    public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                        (ref Utf8YamlReader reader) => typeof(Dog);
                }

                [YamlSerializable(typeof(Animal))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            TypeGenerationSpec animal = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "Animal");
            Assert.Equal(new[] { "EXP_CLS_CTOR" }, animal.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void NestedGenericOuterTypeArgument_IsSuppressed()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP_NESTED")]
                public class MyExperimentalType
                {
                    public int Value { get; set; }
                }

                public class Outer<T>
                {
                    public class Inner
                    {
                        public int Value { get; set; }
                    }
                }

                public class MyPoco
                {
                    #pragma warning disable EXP_NESTED
                    public Outer<MyExperimentalType>.Inner Value { get; set; }
                    #pragma warning restore EXP_NESTED
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            TypeGenerationSpec myPoco = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "MyPoco");
            Assert.Equal(new[] { "EXP_NESTED" }, myPoco.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void ExperimentalPropertyAccessor_IsSuppressed()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class WithExpAccessors
                {
                    public int GetterValue { [Experimental("EXP_GET_ACCESSOR")] get; set; }
                    public int SetterValue { get; [Experimental("EXP_SET_ACCESSOR")] set; }
                    public int UnusedSetterValue { get; [Experimental("EXP_UNUSED_SETTER")] private set; }
                }

                [YamlSerializable(typeof(WithExpAccessors))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            TypeGenerationSpec type = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "WithExpAccessors");
            Assert.Equal(new[] { "EXP_GET_ACCESSOR", "EXP_SET_ACCESSOR" }, type.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void OptionsLevelExperimentalConverterTypeArgument_IsSuppressedInContextSources()
        {
            string source = """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP_OPTIONS")]
                public class MyExperimentalType { }

                public class MyConverter<T> : YamlConverter<object>
                {
                    public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => null;
                    public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options) { }
                }

                public class MyPoco
                {
                    public object Prop { get; set; }
                }

                #pragma warning disable EXP_OPTIONS
                [YamlSourceGenerationOptions(Converters = new[] { typeof(MyConverter<MyExperimentalType>) })]
                #pragma warning restore EXP_OPTIONS
                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            // EXP_OPTIONS originates from an options-level converter type argument, so it lands on the options
            // spec (not any per-type spec) and the emitter unions it into the aggregate context sources.
            Assert.Equal(new[] { "EXP_OPTIONS" }, result.ContextGenerationSpecs.Single().GeneratedOptionsSpec!.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void MalformedExperimentalDiagnosticIds_AreNotEmitted()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP_VALID")]
                public class ValidExperimentalType { public int Value { get; set; } }

                [Experimental("EXP_BAD\n#error INJECTED_FROM_EXPERIMENTAL_ID")]
                public class MalformedExperimentalType { public int Value { get; set; } }

                public class MyPoco
                {
                    #pragma warning disable EXP_VALID
                    public ValidExperimentalType Valid { get; set; }
                    #pragma warning restore EXP_VALID
                    public MalformedExperimentalType Malformed { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger, disableDiagnosticValidation: true);

            TypeGenerationSpec myPoco = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "MyPoco");
            Assert.Equal(new[] { "EXP_VALID" }, myPoco.ExperimentalDiagnosticIds);
            Assert.Empty(result.NewCompilation.GetDiagnostics()
                .Where(d => d.GetMessage().Contains("INJECTED_FROM_EXPERIMENTAL_ID", StringComparison.Ordinal)));
        }

        public static IEnumerable<object[]> ExperimentalSuppressionSources()
        {
            // Experimental property member: the generated getter/setter is the sole usage.
            yield return new object[]
            {
                "ExperimentalProperty",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [Experimental("EXP001")]
                    public int Value { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Experimental field member (fields included): the generated field access is the sole usage.
            yield return new object[]
            {
                "ExperimentalField",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [Experimental("EXP002")]
                    public int Value;
                }

                [YamlSourceGenerationOptions(IncludeFields = true)]
                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Experimental property type: the user's own declaration is suppressed, leaving generated code.
            yield return new object[]
            {
                "ExperimentalPropertyType",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP003")]
                public class Widget { public int X { get; set; } }

                public class MyPoco
                {
                #pragma warning disable EXP003
                    public Widget W { get; set; }
                #pragma warning restore EXP003
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Experimental serializable type itself: the user's [YamlSerializable] usage is suppressed.
            yield return new object[]
            {
                "ExperimentalSerializableType",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP004")]
                public class Widget { public int X { get; set; } }

                #pragma warning disable EXP004
                [YamlSerializable(typeof(Widget))]
                #pragma warning restore EXP004
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Experimental constructor: the generated 'new Widget()' is the sole usage.
            yield return new object[]
            {
                "ExperimentalConstructor",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class Widget
                {
                    [Experimental("EXP005")]
                    public Widget() { }
                    public int X { get; set; }
                }

                [YamlSerializable(typeof(Widget))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Polymorphic experimental derived type (dotnet/runtime#119451): the [YamlDerivedType] usage is suppressed.
            yield return new object[]
            {
                "ExperimentalDerivedType",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP006")]
                public class Derived : Base { public int Y { get; set; } }

                #pragma warning disable EXP006
                [YamlDerivedType(typeof(Derived), "derived")]
                #pragma warning restore EXP006
                public class Base { public int X { get; set; } }

                [YamlSerializable(typeof(Base))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Experimental converter: the generated 'new WidgetConverter()' is suppressed.
            yield return new object[]
            {
                "ExperimentalConverter",
                """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP007")]
                public class WidgetConverter : YamlConverter<ConvertedWidget>
                {
                    public override ConvertedWidget Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => new ConvertedWidget();
                    public override void Write(Utf8YamlWriter writer, ConvertedWidget value, YamlSerializerOptions options) { }
                }

                #pragma warning disable EXP007
                [YamlConverter(typeof(WidgetConverter))]
                #pragma warning restore EXP007
                public class ConvertedWidget { public int X { get; set; } }

                [YamlSerializable(typeof(ConvertedWidget))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Mixed experimental and obsolete members on the same type; both are suppressed.
            yield return new object[]
            {
                "MixedExperimentalAndObsolete",
                """
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [Experimental("EXP008")]
                    public int Experimental { get; set; }

                    [Obsolete("old")]
                    public int Obsolete { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };

            // Multiple distinct experimental IDs on a single type; each ID is suppressed.
            yield return new object[]
            {
                "MultipleDistinctIds",
                """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    [Experimental("EXP009")]
                    public int A { get; set; }

                    [Experimental("EXP010")]
                    public int B { get; set; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """
            };
        }

        [Fact]
        public void ExperimentalDiagnosticIds_AreScopedPerType()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP100")]
                public class TypeA { public int X { get; set; } }

                [Experimental("EXP200")]
                public class TypeB { public int Y { get; set; } }

                #pragma warning disable EXP100, EXP200
                [YamlSerializable(typeof(TypeA))]
                [YamlSerializable(typeof(TypeB))]
                #pragma warning restore EXP100, EXP200
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            TypeGenerationSpec typeA = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "TypeA");
            TypeGenerationSpec typeB = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "TypeB");

            // Each type's generated file suppresses only its own experimental ID.
            Assert.Equal(new[] { "EXP100" }, typeA.ExperimentalDiagnosticIds);
            Assert.Equal(new[] { "EXP200" }, typeB.ExperimentalDiagnosticIds);

            // The aggregate source files suppress the union of all per-type IDs, reconstituted by the emitter.
            // Default diagnostic validation is enabled above, so an unsuppressed EXP100/EXP200 in those files
            // (both registered types are [Experimental]) would surface as a generation error and fail this test.
        }

        [Fact]
        public void UserDefinedExperimentalAttribute_IsRecognized()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace System.Diagnostics.CodeAnalysis
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    internal sealed class ExperimentalAttribute : System.Attribute
                    {
                        public ExperimentalAttribute(string diagnosticId) { DiagnosticId = diagnosticId; }
                        public string DiagnosticId { get; }
                        public string UrlFormat { get; set; } = "";
                    }
                }

                [System.Diagnostics.CodeAnalysis.Experimental("EXP_USERDEF")]
                public class Widget { public int X { get; set; } }

                #pragma warning disable EXP_USERDEF
                [YamlSerializable(typeof(Widget))]
                #pragma warning restore EXP_USERDEF
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger, disableDiagnosticValidation: true);

            TypeGenerationSpec widget = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "Widget");
            Assert.Equal(new[] { "EXP_USERDEF" }, widget.ExperimentalDiagnosticIds);
        }

        [Fact]
        public void ReferencedExperimentalAttributeSymbolMismatch_IsSuppressed()
        {
            Compilation referencedCompilation = CompilationHelper.CreateReferencedExperimentalPocoWithPolyfillCompilation();
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);
            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            string source = """
                using ReferencedAssembly;
                using System.Text.Yaml.Serialization;

                namespace System.Diagnostics.CodeAnalysis
                {
                    [System.AttributeUsage(System.AttributeTargets.All)]
                    public sealed class ExperimentalAttribute : System.Attribute
                    {
                        public ExperimentalAttribute(string diagnosticId) { DiagnosticId = diagnosticId; }
                        public string DiagnosticId { get; }
                        public string UrlFormat { get; set; } = "";
                    }
                }

                #pragma warning disable EXP_TEST
                [YamlSerializable(typeof(ExperimentalPocoFromLib))]
                #pragma warning restore EXP_TEST
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger, disableDiagnosticValidation: true);

            TypeGenerationSpec poco = result.AllGeneratedTypes.Single(s => s.TypeRef.Name == "ExperimentalPocoFromLib");
            Assert.Equal(new[] { "EXP_TEST" }, poco.ExperimentalDiagnosticIds);
            Assert.Empty(result.NewCompilation.GetDiagnostics().Where(d => d.Id == "EXP_TEST"));
        }

        [Fact]
        public void ExperimentalSuppression_DoesNotLeakToUserCode()
        {
            string source = """
                using System.Diagnostics.CodeAnalysis;
                using System.Text.Yaml.Serialization;

                [Experimental("EXP300")]
                public class Widget { public int X { get; set; } }

                public class MyPoco
                {
                #pragma warning disable EXP300
                    public Widget W { get; set; }
                #pragma warning restore EXP300
                }

                public class Consumer
                {
                    public int Use() => new Widget().X;
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext { }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger, disableDiagnosticValidation: true);

            // The generated code suppresses its own references, but the unguarded user usage in Consumer.Use
            // must still report the diagnostic (suppression is scoped to generated files). Exactly one match
            // also confirms the generated files contributed none.
            Diagnostic diagnostic = Assert.Single(result.NewCompilation.GetDiagnostics().Where(d => d.Id == "EXP300"));
            Assert.False(diagnostic.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) ?? false);
        }
#endif

        [Fact]
        public void NegativeYamlPropertyOrderGeneratesValidCode()
        {
            // Test for https://github.com/dotnet/runtime/issues/121277
            // Verify that negative YamlPropertyOrder values generate compilable code
            // even on locales that use non-ASCII minus signs (e.g., fi_FI uses U+2212)
            string source = """
                using System.Text.Yaml.Serialization;

                namespace Test
                {
                    public class MyClass
                    {
                        [YamlPropertyOrder(-1)]
                        public int FirstProperty { get; set; }

                        [YamlPropertyOrder(0)]
                        public int SecondProperty { get; set; }

                        [YamlPropertyOrder(-100)]
                        public int ThirdProperty { get; set; }
                    }

                    [YamlSerializable(typeof(MyClass))]
                    public partial class MyContext : YamlSerializerContext
                    {
                    }
                }
                """;

            // Test with fi_FI culture which uses U+2212 minus sign for negative numbers
            using (new ThreadCultureChange("fi-FI"))
            {
                Compilation compilation = CompilationHelper.CreateCompilation(source);
                YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

                // The generated code should compile without errors
                // If the bug exists, we'd see CS1525, CS1002, CS1056, or CS0201 errors
                var errors = result.NewCompilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                Assert.Empty(errors);
            }
        }

        [Fact]
        public void PartialContextClassWithAttributesOnMultipleDeclarations()
        {
            // Test for https://github.com/dotnet/runtime/issues/99669
            // When a YamlSerializerContext is defined across multiple partial class declarations
            // with [YamlSerializable] attributes on different declarations, the generator should
            // successfully generate code without duplicate hintName errors.

            string source1 = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    public class MyClass1
                    {
                        public int Value { get; set; }
                    }

                    public class MyClass2
                    {
                        public string Name { get; set; }
                    }

                    [YamlSerializable(typeof(MyClass1))]
                    internal partial class SerializerContext : YamlSerializerContext
                    {
                    }
                }
                """;

            string source2 = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyClass2))]
                    internal partial class SerializerContext
                    {
                    }
                }
                """;

            // Create a base compilation to get proper references (including netfx polyfill attributes).
            // File paths are explicitly set to verify the canonical partial selection (first alphabetically).
            // "File1.cs" comes before "File2.cs" alphabetically, so source1 declares the canonical partial.
            Compilation baseCompilation = CompilationHelper.CreateCompilation("");

            // Add our syntax trees with explicit file paths. Keep any existing polyfill trees from base compilation.
            var polyfillTrees = baseCompilation.SyntaxTrees.Where(t => string.IsNullOrEmpty(t.FilePath) == false || !t.ToString().Contains("namespace HelloWorld"));
            Compilation compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees: baseCompilation.SyntaxTrees.Concat(new[]
                {
                    CSharpSyntaxTree.ParseText(source1, CompilationHelper.CreateParseOptions()).WithFilePath("File1.cs"),
                    CSharpSyntaxTree.ParseText(source2, CompilationHelper.CreateParseOptions()).WithFilePath("File2.cs")
                }),
                references: baseCompilation.References,
                options: (CSharpCompilationOptions)baseCompilation.Options);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);

            // Verify no errors from the source generator
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.Empty(errors);

            // Verify a single combined context was generated containing both types
            // (not two separate contexts, which would cause duplicate hintName errors)
            Assert.Equal(1, result.ContextGenerationSpecs.Length);
            result.AssertContainsType("global::HelloWorld.MyClass1");
            result.AssertContainsType("global::HelloWorld.MyClass2");

            // Verify the generated code compiles without errors
            var compilationErrors = result.NewCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.Empty(compilationErrors);
        }

        [Fact]
        public void GenericTypeWithConstrainedTypeParameters_InitOnlyProperties()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    public class MyBase
                    {
                        public int Id { get; set; }
                    }

                    public class ConstrainedGenericType<T> where T : notnull, MyBase
                    {
                        public T Response { get; init; }
                        public string Name { get; init; }
                    }

                    public class DerivedType : MyBase
                    {
                        public string Extra { get; set; }
                    }

                    [YamlSerializable(typeof(ConstrainedGenericType<DerivedType>))]
                    internal partial class MyContext : YamlSerializerContext { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Theory]
        [InlineData("notnull", "int")]
        [InlineData("class", "string")]
        [InlineData("class, new()", "object")]
        [InlineData("struct", "int")]
        [InlineData("unmanaged", "int")]
        [InlineData("notnull, System.IDisposable", "System.IO.MemoryStream")]
        public void GenericTypeWithConstrainedTypeParameters_VariousConstraints(string constraint, string typeArg)
        {
            string source = $$"""
                using System;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    public class GenericWithConstraint<T> where T : {{constraint}}
                    {
                        public string Label { get; init; }
                    }

                    [YamlSerializable(typeof(GenericWithConstraint<{{typeArg}}>))]
                    internal partial class MyContext : YamlSerializerContext { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void GenericTypeWithConstrainedTypeParameters_MultipleTypeParameters()
        {
            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    public class MultiConstraint<TKey, TValue>
                        where TKey : notnull
                        where TValue : class, new()
                    {
                        public TKey Key { get; init; }
                        public TValue Value { get; init; }
                    }

                    [YamlSerializable(typeof(MultiConstraint<int, object>))]
                    internal partial class MyContext : YamlSerializerContext { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }

        [Fact]
        public void GenericTypeWithConstrainedTypeParameters_BaseClassAndInterface()
        {
            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    public class MyBase
                    {
                        public int Id { get; set; }
                    }

                    public class Derived : MyBase, IDisposable
                    {
                        public void Dispose() { }
                    }

                    public class GenericWithMultiConstraint<T> where T : MyBase, IDisposable
                    {
                        public T Item { get; init; }
                        public string Label { get; init; }
                    }

                    [YamlSerializable(typeof(GenericWithMultiConstraint<Derived>))]
                    internal partial class MyContext : YamlSerializerContext { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation, logger: logger);
        }
    }
}
