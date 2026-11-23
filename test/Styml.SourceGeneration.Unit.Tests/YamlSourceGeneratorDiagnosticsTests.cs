// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.UnitTests
{
    [ActiveIssue("https://github.com/dotnet/runtime/issues/58226", TestPlatforms.Browser)]
    [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/71962", ~RuntimeConfiguration.Release)]
    [SkipOnMono("https://github.com/dotnet/runtime/issues/92467")]
    [ConditionalClass(typeof(PlatformDetection), nameof(PlatformDetection.IsNotX86Process))] // https://github.com/dotnet/runtime/issues/71962
    public class YamlSourceGeneratorDiagnosticsTests
    {
        /// <summary>
        /// https://github.com/dotnet/runtime/issues/61379
        /// </summary>
        [Fact]
        public void EmitsDocumentationOnPublicMembersAndDoesNotCauseCS1591()
        {
            // Compile the referenced assembly first.
            Compilation documentedCompilation = CompilationHelper.CreateReferencedModelWithFullyDocumentedProperties();

            // Emit the image of the referenced assembly.
            byte[] documentedImage = CompilationHelper.CreateAssemblyImage(documentedCompilation);

            // Main source for current compilation.
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;

                namespace YamlSourceGenerator
                {
                    /// <summary>
                    /// Documentation
                    /// </summary>
                    [YamlSerializable(typeof(DocumentedModel))]
                    [YamlSerializable(typeof(DocumentedModel2<string>))]
                    public partial class YamlContext : YamlSerializerContext
                    {
                    }

                    /// <summary>
                    /// Documentation
                    /// </summary>
                    public class DocumentedModel2<T>
                    {
                        /// <summary>
                        /// Documentation
                        /// </summary>
                        public List<Model> Models { get; set; }
                        /// documentation
                        public T Prop { get; set; }
                    }

                    /// <summary>
                    /// Documentation
                    /// </summary>
                    public class DocumentedModel
                    {
                        /// <summary>
                        /// Documentation
                        /// </summary>
                        public List<Model> Models { get; set; }
                    }
                }
                """;

            MetadataReference[] additionalReferences = {
                MetadataReference.CreateFromImage(documentedImage),
            };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences, parseOptions: CompilationHelper.CreateParseOptions(documentationMode: DocumentationMode.Diagnose));
            YamlSourceGeneratorResult sourceGenResult = CompilationHelper.RunYamlSourceGenerator(compilation);

            using var emitStream = new MemoryStream();
            using var xmlStream = new MemoryStream();
            var result = sourceGenResult.NewCompilation.Emit(emitStream, xmlDocumentationStream: xmlStream);
        }

        [Fact]
        public void SuccessfulSourceGeneration()
        {
            // Compile the referenced assembly first.
            Compilation campaignCompilation = CompilationHelper.CreateCampaignSummaryViewModelCompilation();
            Compilation eventCompilation = CompilationHelper.CreateActiveOrUpcomingEventCompilation();

            // Emit the image of the referenced assembly.
            byte[] campaignImage = CompilationHelper.CreateAssemblyImage(campaignCompilation);
            byte[] eventImage = CompilationHelper.CreateAssemblyImage(eventCompilation);

            // Main source for current compilation.
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;

                namespace YamlSourceGenerator
                {
                    [YamlSerializable(typeof(YamlSourceGenerator.IndexViewModel))]
                    public partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class IndexViewModel
                    {
                        public List<ActiveOrUpcomingEvent> ActiveOrUpcomingEvents { get; set; }
                        public CampaignSummaryViewModel FeaturedCampaign { get; set; }
                        public bool IsNewAccount { get; set; }
                        public bool HasFeaturedCampaign => FeaturedCampaign != null;
                    }
                }
                """;

            MetadataReference[] additionalReferences = {
                MetadataReference.CreateFromImage(campaignImage),
                MetadataReference.CreateFromImage(eventImage),
            };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MultiDimensionArrayDoesNotProduceWarnings(bool explicitRef)
        {
            // Compile the referenced assembly first.
            Compilation campaignCompilation = CompilationHelper.CreateCampaignSummaryViewModelCompilation();
            Compilation eventCompilation = CompilationHelper.CreateActiveOrUpcomingEventCompilation();

            // Emit the image of the referenced assembly.
            byte[] campaignImage = CompilationHelper.CreateAssemblyImage(campaignCompilation);
            byte[] eventImage = CompilationHelper.CreateAssemblyImage(eventCompilation);

            string optionalAttribute = explicitRef ? "[YamlSerializable(typeof(ActiveOrUpcomingEvent[,]))]" : null;

            // Main source for current compilation.
            string source = $$"""
                using System.Text.Yaml.Serialization;
                using ReferencedAssembly;

                namespace YamlSourceGenerator
                {
                    {{optionalAttribute}}
                    [YamlSerializable(typeof(YamlSourceGenerator.IndexViewModel))]
                    public partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class IndexViewModel
                    {
                        public ActiveOrUpcomingEvent[,] ActiveOrUpcomingEvents { get; set; }
                        public CampaignSummaryViewModel FeaturedCampaign { get; set; }
                        public bool IsNewAccount { get; set; }
                        public bool HasFeaturedCampaign => FeaturedCampaign != null;
                    }
                }
                """;

            MetadataReference[] additionalReferences = {
                MetadataReference.CreateFromImage(campaignImage),
                MetadataReference.CreateFromImage(eventImage),
            };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void NameClashSourceGeneration()
        {
            // Without resolution.
            Compilation compilation = CompilationHelper.CreateRepeatedLocationsCompilation();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location location = compilation.GetSymbolsWithName("YamlContext").FirstOrDefault().GetAttributes()[1].GetLocation();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, location, "There are multiple types named 'Location'. Source was generated for the first one detected. Use 'YamlSerializableAttribute.TypeInfoPropertyName' to resolve this collision.")
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void ProgramsThatDontUseGeneratorCompile()
        {
            // No STJ usage.
            string source = """
                using System;

                public class Program
                {
                    public static void Main()
                    {
                        Console.WriteLine("Hello World");
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation);

            // With STJ usage.
            source = """
                using System.Text.Yaml;

                public class Program
                {
                    public static void Main()
                    {
                        YamlSerializer.Serialize("Hello World");
                    }
                }
                """;

            compilation = CompilationHelper.CreateCompilation(source);
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void DoNotWarnOnClassesWithInitOnlyProperties()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithInitOnlyProperties();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void DoNotWarnOnClassesWithConstructorInitOnlyProperties()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithConstructorInitOnlyProperties();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void DoNotWarnOnClassesWithMixedInitOnlyProperties()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithMixedInitOnlyProperties();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void DoNotWarnOnRecordsWithInitOnlyPositionalParameters()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithRecordPositionalParameters();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

#if ROSLYN4_4_OR_GREATER
        [Fact]
        public void DoNotWarnOnClassesWithRequiredProperties()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithRequiredProperties();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }
#endif

        [Fact]
        public void DoNotWarnOnClassesWithInaccessibleYamlIncludeProperties()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithInaccessibleYamlIncludeProperties();
            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Fact]
        public void PolymorphicClassWarnsOnFastPath()
        {
            Compilation compilation = CompilationHelper.CreatePolymorphicClassOnFastPathContext();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location myBaseClassLocation = compilation.GetSymbolsWithName("MyBaseClass").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, myBaseClassLocation, "Type 'HelloWorld.MyBaseClass' is annotated with 'YamlDerivedTypeAttribute' which is not supported in 'YamlSourceGenerationMode.Serialization'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void YamlStringEnumConverterWarns()
        {
            Compilation compilation = CompilationHelper.CreateTypesAnnotatedWithYamlStringEnumConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location enum2PropLocation = compilation.GetSymbolsWithName("Enum2Prop").First().GetAttributes()[0].GetLocation();
            Location enum1TypeLocation = compilation.GetSymbolsWithName("Enum1").First().GetAttributes()[0].GetLocation();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, enum1TypeLocation, "The member 'HelloWorld.Enum1' has been annotated with 'YamlStringEnumConverter' which is not supported in native AOT. Consider using the generic 'YamlStringEnumConverter<TEnum>' instead."),
                new(DiagnosticSeverity.Warning, enum2PropLocation, "The member 'HelloWorld.MyClass.Enum2Prop' has been annotated with 'YamlStringEnumConverter' which is not supported in native AOT. Consider using the generic 'YamlStringEnumConverter<TEnum>' instead."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void InvalidYamlConverterAttributeTypeWarns()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithInvalidYamlConverterAttributeType();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location value1PropLocation = compilation.GetSymbolsWithName("Value1").First().GetAttributes()[0].GetLocation();
            Location value2PropLocation = compilation.GetSymbolsWithName("Value2").First().GetAttributes()[0].GetLocation();
            Location value3PropLocation = compilation.GetSymbolsWithName("Value3").First().GetAttributes()[0].GetLocation();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, value1PropLocation, "The 'YamlConverterAttribute' type 'null' specified on member 'HelloWorld.MyClass.Value1' is not a converter type or does not contain an accessible parameterless constructor."),
                new(DiagnosticSeverity.Warning, value2PropLocation, "The 'YamlConverterAttribute' type 'int' specified on member 'HelloWorld.MyClass.Value2' is not a converter type or does not contain an accessible parameterless constructor."),
                new(DiagnosticSeverity.Warning, value3PropLocation, "The 'YamlConverterAttribute' type 'HelloWorld.InacessibleConverter' specified on member 'HelloWorld.MyClass.Value3' is not a converter type or does not contain an accessible parameterless constructor."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void DerivedYamlConverterAttributeTypeWarns()
        {
            Compilation compilation = CompilationHelper.CreateCompilationWithDerivedYamlConverterAttributeAnnotations();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location typeAttrLocation = compilation.GetSymbolsWithName("ClassWithConverterDeclaration").First().GetAttributes()[0].GetLocation();
            Location yamlSerializableAttrLocation = compilation.GetSymbolsWithName("YamlContext").First().GetAttributes()[0].GetLocation();
            Location propAttrLocation = compilation.GetSymbolsWithName("Value").First().GetAttributes()[0].GetLocation();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, typeAttrLocation, "The custom attribute 'HelloWorld.MyYamlConverterAttribute' deriving from YamlConverterAttribute is not supported by the source generator."),
                new(DiagnosticSeverity.Warning, yamlSerializableAttrLocation, "Did not generate serialization metadata for type 'HelloWorld.ClassWithConverterDeclaration'."),
                new(DiagnosticSeverity.Warning, propAttrLocation, "The custom attribute 'HelloWorld.MyYamlConverterAttribute' deriving from YamlConverterAttribute is not supported by the source generator."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void UnboundGenericTypeDeclarationWarns()
        {
            Compilation compilation = CompilationHelper.CreateContextWithUnboundGenericTypeDeclarations();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            INamedTypeSymbol symbol = (INamedTypeSymbol)compilation.GetSymbolsWithName("YamlContext").FirstOrDefault();
            Collections.Immutable.ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, attributes[0].GetLocation(), "Did not generate serialization metadata for type 'System.Collections.Generic.List<>'."),
                new(DiagnosticSeverity.Warning, attributes[1].GetLocation(), "Did not generate serialization metadata for type 'System.Collections.Generic.Dictionary<,>'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void ErrorTypeDeclarationWarns()
        {
            Compilation compilation = CompilationHelper.CreateContextWithErrorTypeDeclarations();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            INamedTypeSymbol symbol = (INamedTypeSymbol)compilation.GetSymbolsWithName("YamlContext").FirstOrDefault();
            Collections.Immutable.ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, attributes[0].GetLocation(), "Did not generate serialization metadata for type 'BogusType'."),
                new(DiagnosticSeverity.Warning, attributes[1].GetLocation(), "Did not generate serialization metadata for type 'BogusType<int>'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Theory]
        [InlineData(LanguageVersion.Default)]
        [InlineData(LanguageVersion.Preview)]
        [InlineData(LanguageVersion.Latest)]
        [InlineData(LanguageVersion.LatestMajor)]
        [InlineData(LanguageVersion.CSharp9)]
#if ROSLYN4_4_OR_GREATER
        [InlineData(LanguageVersion.CSharp10)]
        [InlineData(LanguageVersion.CSharp11)]
#endif
        public void SupportedLanguageVersions_SucceedCompilation(LanguageVersion langVersion)
        {
            string source = """
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        public class MyClass
                        {
                            public MyClass(int value)
                            {
                                Value = value;
                            }

                            public int Value { get; set; }
                        }

                        [YamlSerializable(typeof(MyClass))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            CSharpParseOptions parseOptions = CompilationHelper.CreateParseOptions(langVersion);
            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: parseOptions);

            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Theory]
        [InlineData(LanguageVersion.Default)]
        [InlineData(LanguageVersion.Preview)]
        [InlineData(LanguageVersion.Latest)]
        [InlineData(LanguageVersion.LatestMajor)]
        [InlineData(LanguageVersion.CSharp9)]
#if ROSLYN4_4_OR_GREATER
        [InlineData(LanguageVersion.CSharp10)]
        [InlineData(LanguageVersion.CSharp11)]
#endif
        public void SupportedLanguageVersions_Memory_SucceedCompilation(LanguageVersion langVersion)
        {
            string source = """
                    using System;
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        public class MyClass<T>
                        {
                            public MyClass(
                                Memory<T> memoryOfT,
                                Memory<byte> memoryByte,
                                ReadOnlyMemory<T> readOnlyMemoryOfT,
                                ReadOnlyMemory<byte> readOnlyMemoryByte)
                            {
                                MemoryOfT = memoryOfT;
                                MemoryByte = memoryByte;
                                ReadOnlyMemoryOfT = readOnlyMemoryOfT;
                                ReadOnlyMemoryByte = readOnlyMemoryByte;
                            }

                            public Memory<T> MemoryOfT { get; set; }
                            public Memory<byte> MemoryByte { get; set; }
                            public ReadOnlyMemory<T> ReadOnlyMemoryOfT { get; set; }
                            public ReadOnlyMemory<byte> ReadOnlyMemoryByte { get; set; }
                        }

                        [YamlSerializable(typeof(MyClass<int>))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            CSharpParseOptions parseOptions = CompilationHelper.CreateParseOptions(langVersion);
            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: parseOptions);

            CompilationHelper.RunYamlSourceGenerator(compilation);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp1)]
        [InlineData(LanguageVersion.CSharp2)]
        [InlineData(LanguageVersion.CSharp3)]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.CSharp7_3)]
        [InlineData(LanguageVersion.CSharp8)]
        public void UnsupportedLanguageVersions_FailCompilation(LanguageVersion langVersion)
        {
            string source = """
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        public class MyClass
                        {
                            public MyClass(int value)
                            {
                                Value = value;
                            }

                            public int Value { get; set; }
                        }

                        [YamlSerializable(typeof(MyClass))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            CSharpParseOptions parseOptions = CompilationHelper.CreateParseOptions(langVersion);
            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: parseOptions);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location contextLocation = compilation.GetSymbolsWithName("MyYamlContext").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Error, contextLocation, $"The System.Text.Yaml source generator is not available in C# {langVersion.ToDisplayString()}. Please use language version 9.0 or greater.")
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void TypesWithYamlConstructorAnnotations_WarnAsExpected()
        {
            // Inaccessible [YamlConstructor] constructors are now supported via UnsafeAccessor or reflection fallback.
            // No diagnostics should be emitted.
            Compilation compilation = CompilationHelper.CreateCompilationWithYamlConstructorAttributeAnnotations();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            CompilationHelper.AssertEqualDiagnosticMessages([], result.Diagnostics);
        }

        [Fact]
        public void DiagnosticOnMemberFromReferencedAssembly_LocationDefaultsToContextClass()
        {
            Compilation referencedCompilation = CompilationHelper.CreateCompilation("""
                using System.Text.Yaml.Serialization;

                namespace Library
                {
                    public class MyPoco
                    {
                        [YamlConverter(typeof(int))]
                        public int Value { get; set; }
                    }
                }
                """);

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);
            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation("""
                using Library;
                using System.Text.Yaml.Serialization;

                namespace Application
                {
                    [YamlSerializable(typeof(MyPoco))]
                    public partial class MyContext : YamlSerializerContext
                    { }
                }
                """, additionalReferences);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location contextLocation = compilation.GetSymbolsWithName("MyContext").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, contextLocation, "The 'YamlConverterAttribute' type 'int' specified on member 'Library.MyPoco.Value' is not a converter type or does not contain an accessible parameterless constructor."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void YamlSerializableAttributeOnNonContextClass()
        {
            Compilation compilation = CompilationHelper.CreateCompilation("""
                using System.Text.Yaml.Serialization;

                namespace Application
                {
                    [YamlSerializable(typeof(MyPoco))]
                    public partial class MyContext : IDisposable
                    {
                        public void Dispose() { }
                    }

                    public class MyPoco { }
                }
                """);

            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location contextLocation = compilation.GetSymbolsWithName("MyContext").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, contextLocation, "The type 'Application.MyContext' has been annotated with YamlSerializableAttribute but does not derive from YamlSerializerContext. No source code will be generated."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void RefStructPropertyWithoutYamlIgnore_CompilesWithWarning()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/98590

            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    public ReadOnlySpan<char> Values => "abc".AsSpan();
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location propertyLocation = ((INamedTypeSymbol)compilation.GetSymbolsWithName("MyPoco").First()).GetMembers("Values").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, propertyLocation, "The type 'MyPoco' includes the ref like property, field or constructor parameter 'Values'. No source code will be generated for the property, field or constructor."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void RefStructCtorParam_CompilesWithWarning()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/98590

            string source = """
                using System;
                using System.Text.Yaml.Serialization;

                public class MyPoco
                {
                    public MyPoco(ReadOnlySpan<char> value)
                    {
                        Value = value.ToString();
                    }

                    public string Value { get; }
                }

                [YamlSerializable(typeof(MyPoco))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            ITypeSymbol type = (INamedTypeSymbol)compilation.GetSymbolsWithName("MyPoco").First();
            IMethodSymbol ctor = (IMethodSymbol)type.GetMembers(".ctor").First();
            IParameterSymbol param = ctor.Parameters.First();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, param.Locations.First(), "The type 'MyPoco' includes the ref like property, field or constructor parameter 'value'. No source code will be generated for the property, field or constructor."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void UnionWithAmbiguousCaseTypes_CompilesWithWarning()
        {
            // Two case types serialize as the same JSON value type (Number).
            string source = """
                using System.Runtime.CompilerServices;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                namespace TestApp
                {
                    [YamlSerializable(typeof(IntOrLongUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    public readonly struct IntOrLongUnion : IUnion
                    {
                        public IntOrLongUnion(int value) { Value = value; }
                        public IntOrLongUnion(long value) { Value = value; }
                        public object? Value { get; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location unionLocation = compilation.GetSymbolsWithName("IntOrLongUnion").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, unionLocation, "Union type 'IntOrLongUnion': case types 'int', 'long' all serialize as JSON value type 'Number'. Set YamlUnionAttribute.TypeClassifier to provide a custom classifier that can disambiguate."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Theory]
        [InlineData("", null, false)]
        [InlineData("", "YamlNumberHandling.AllowReadingFromString", true)]
        [InlineData("NumberHandling = YamlNumberHandling.Strict", null, false)]
        [InlineData("NumberHandling = YamlNumberHandling.AllowReadingFromString", null, true)]
        [InlineData("YamlSerializerDefaults.General", null, false)]
        [InlineData("YamlSerializerDefaults.Strict", null, false)]
        [InlineData("YamlSerializerDefaults.Web", null, true)]
        [InlineData("YamlSerializerDefaults.Web", "YamlNumberHandling.Strict", false)]
        [InlineData("YamlSerializerDefaults.General", "YamlNumberHandling.AllowReadingFromString", true)]
        [InlineData("YamlSerializerDefaults.Web", "YamlNumberHandling.WriteAsString", false)]
        [InlineData("YamlSerializerDefaults.Web, NumberHandling = YamlNumberHandling.Strict", null, false)]
        [InlineData("YamlSerializerDefaults.Web, NumberHandling = YamlNumberHandling.Strict", "YamlNumberHandling.AllowReadingFromString", true)]
        [InlineData("YamlSerializerDefaults.General, NumberHandling = YamlNumberHandling.AllowReadingFromString", null, true)]
        [InlineData("YamlSerializerDefaults.General, NumberHandling = YamlNumberHandling.AllowReadingFromString", "YamlNumberHandling.Strict", false)]
        [InlineData("YamlSerializerDefaults.Web, NumberHandling = YamlNumberHandling.WriteAsString", null, false)]
        [InlineData("YamlSerializerDefaults.General, NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString", null, true)]
        public void UnionNumberHandling_ReportsExpectedAmbiguityDiagnostics(string contextOptions, string? unionNumberHandling, bool expectWarning)
        {
            string numberHandlingAttribute = unionNumberHandling is null ? "" : $"[YamlNumberHandling({unionNumberHandling})]";
            string source = $$"""
                using System.Runtime.CompilerServices;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    [YamlSourceGenerationOptions({{contextOptions}})]
                    [YamlSerializable(typeof(IntOrStringUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    {{numberHandlingAttribute}}
                    public readonly struct IntOrStringUnion : IUnion
                    {
                        public IntOrStringUnion(int value) { Value = value; }
                        public IntOrStringUnion(string value) { Value = value; }
                        public object Value { get; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            if (expectWarning)
            {
                Diagnostic diagnostic = Assert.Single(result.Diagnostics);
                Assert.Equal("STYML1227", diagnostic.Id);
                Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
                Assert.Contains("case types 'int', 'string' all serialize as JSON value type 'String'", diagnostic.GetMessage());
            }
            else
            {
                Assert.Empty(result.Diagnostics);
            }
#if NET
            // These compiler versions do not support the generated union patterns.
            Assert.Equal(new[] { "CS0037", "CS8121", "CS8121" }, result.NewCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Id)
                .OrderBy(id => id, StringComparer.Ordinal));
#endif
        }

        [Fact]
        public void UnionNumberHandling_ContextDefaultsDoNotLeak()
        {
            foreach (bool webContextFirst in new[] { true, false })
            {
                const string WebOptions = "[YamlSourceGenerationOptions(YamlSerializerDefaults.Web)]";
                string source = $$"""
                    using System.Runtime.CompilerServices;
                    using System.Text.Yaml;
                    using System.Text.Yaml.Serialization;

                    namespace TestApp
                    {
                        {{(webContextFirst ? WebOptions : "")}}
                        [YamlSerializable(typeof(IntOrStringUnion))]
                        internal partial class FirstContext : YamlSerializerContext { }

                        {{(webContextFirst ? "" : WebOptions)}}
                        [YamlSerializable(typeof(IntOrStringUnion))]
                        internal partial class SecondContext : YamlSerializerContext { }

                        [Union]
                        public readonly struct IntOrStringUnion : IUnion
                        {
                            public IntOrStringUnion(int value) { Value = value; }
                            public IntOrStringUnion(string value) { Value = value; }
                            public object Value { get; }
                        }
                    }
                    """;

                Compilation compilation = CompilationHelper.CreateCompilation(source);
                YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

                Assert.Contains(result.NewCompilation.SyntaxTrees, tree => Path.GetFileName(tree.FilePath) == "FirstContext.g.cs");
                Assert.Contains(result.NewCompilation.SyntaxTrees, tree => Path.GetFileName(tree.FilePath) == "SecondContext.g.cs");
                Diagnostic diagnostic = Assert.Single(result.Diagnostics);
                Assert.Equal("STYML1227", diagnostic.Id);
                Assert.Contains("case types 'int', 'string' all serialize as JSON value type 'String'", diagnostic.GetMessage());
            }
        }

        [Fact]
        public void UnionWithCustomConverter_CompilesWithoutWarning()
        {
            string source = """
                using System;
                using System.Runtime.CompilerServices;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    [YamlSerializable(typeof(IntOrLongUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [YamlConverter(typeof(IntOrLongUnionConverter))]
                    [Union]
                    public readonly struct IntOrLongUnion : IUnion
                    {
                        public IntOrLongUnion(int value) { Value = value; }
                        public IntOrLongUnion(long value) { Value = value; }
                        public object? Value { get; }
                    }

                    public sealed class IntOrLongUnionConverter : YamlConverter<IntOrLongUnion>
                    {
                        public override IntOrLongUnion Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) => default;
                        public override void Write(Utf8YamlWriter writer, IntOrLongUnion value, YamlSerializerOptions options) { }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "STYML1227");
        }

        [Fact]
        public void UnionWithListAndDictionaryCases_CompilesWithoutWarning()
        {
            string source = """
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    [YamlSerializable(typeof(ListOrDictionaryUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    public readonly struct ListOrDictionaryUnion : IUnion
                    {
                        public ListOrDictionaryUnion(List<int> value) { Value = value; }
                        public ListOrDictionaryUnion(Dictionary<string, int> value) { Value = value; }
                        public object? Value { get; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "STYML1227");
        }

        [Fact]
        public void UnionWithAmbiguousCaseTypesAndAttributeClassifier_CompilesWithoutWarning()
        {
            string source = """
                using System;
                using System.Runtime.CompilerServices;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                namespace TestApp
                {
                    [YamlSerializable(typeof(IntOrLongUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [YamlUnion(TypeClassifier = typeof(IntOrLongClassifier))]
                    [Union]
                    public readonly struct IntOrLongUnion : IUnion
                    {
                        public IntOrLongUnion(int value) { Value = value; }
                        public IntOrLongUnion(long value) { Value = value; }
                        public object? Value { get; }
                    }

                    public sealed class IntOrLongClassifier : YamlTypeClassifierFactory
                    {
                        public override bool CanClassify(YamlTypeClassifierContext context) => true;
                        public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                            (ref Utf8YamlReader reader) => typeof(int);
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "STYML1227");
        }

        [Fact]
        public void UnionWithAmbiguousCaseTypesAndOptionsClassifier_CompilesWithoutWarning()
        {
            string source = """
                using System;
                using System.Runtime.CompilerServices;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                namespace TestApp
                {
                    [YamlSourceGenerationOptions(TypeClassifiers = new[] { typeof(IntOrLongClassifier) })]
                    [YamlSerializable(typeof(IntOrLongUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    public readonly struct IntOrLongUnion : IUnion
                    {
                        public IntOrLongUnion(int value) { Value = value; }
                        public IntOrLongUnion(long value) { Value = value; }
                        public object? Value { get; }
                    }

                    public sealed class IntOrLongClassifier : YamlTypeClassifierFactory
                    {
                        public override bool CanClassify(YamlTypeClassifierContext context) => true;
                        public override YamlTypeClassifier CreateYamlClassifier(YamlTypeClassifierContext context, YamlSerializerOptions options) =>
                            (ref Utf8YamlReader reader) => typeof(int);
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void UnionWithCustomConverterCaseType_CompilesWithWarning()
        {
            // User-defined converters are conservatively classified as potentially representing
            // every JSON value type, so the object shape conflicts with the other object case.
            string source = """
                using System.Runtime.CompilerServices;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace TestApp
                {
                    [YamlSerializable(typeof(MyUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [YamlConverter(typeof(CustomConverter))]
                    public class CustomCase
                    {
                    }

                    public class CustomConverter : YamlConverter<CustomCase>
                    {
                        public override CustomCase? Read(ref Utf8YamlReader reader, System.Type typeToConvert, YamlSerializerOptions options) => null;
                        public override void Write(Utf8YamlWriter writer, CustomCase value, YamlSerializerOptions options) { }
                    }

                    public class OtherCase
                    {
                    }

                    [Union]
                    public readonly struct MyUnion : IUnion
                    {
                        public MyUnion(CustomCase value) { Value = value; }
                        public MyUnion(OtherCase value) { Value = value; }
                        public object? Value { get; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location unionLocation = compilation.GetSymbolsWithName("MyUnion").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, unionLocation, "Union type 'MyUnion': case types 'CustomCase', 'OtherCase' all serialize as JSON value type 'Object'. Set YamlUnionAttribute.TypeClassifier to provide a custom classifier that can disambiguate."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void UnionAttributeWithoutValueProperty_GeneratesDiagnosticWarning()
        {
            string source = """
                using System.Runtime.CompilerServices;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                namespace TestApp
                {
                    [YamlSerializable(typeof(MissingShapeUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    public readonly struct MissingShapeUnion : IUnion
                    {
                        public MissingShapeUnion(int value) { }
                        object IUnion.Value => throw new System.NotSupportedException();
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location unionLocation = compilation.GetSymbolsWithName("MissingShapeUnion").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, unionLocation, "Union type 'MissingShapeUnion' does not match the C# union shape convention: it must declare at least one public single-parameter case constructor and a public instance property named 'Value' with type 'object'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void UnionWithValuePropertyButNoCaseConstructor_GeneratesDiagnosticWarning()
        {
            string source = """
                using System.Runtime.CompilerServices;
                using System.Text.Yaml.Serialization;
                using System.Text.Yaml.Serialization.Metadata;

                namespace TestApp
                {
                    [YamlSerializable(typeof(MissingShapeUnion))]
                    internal partial class MyContext : YamlSerializerContext { }

                    [Union]
                    public readonly struct MissingShapeUnion : IUnion
                    {
                        public object? Value { get; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location unionLocation = compilation.GetSymbolsWithName("MissingShapeUnion").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, unionLocation, "Union type 'MissingShapeUnion' does not match the C# union shape convention: it must declare at least one public single-parameter case constructor and a public instance property named 'Value' with type 'object'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

#if NET
        [Fact]
        public void CollectionWithRefStructElement_CompilesWithWarning()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/98590

            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                public class CollectionWithRefStructElement : IEnumerable<ReadOnlySpan<char>>
                {
                    private List<string> _values = new();
                    public void Add(ReadOnlySpan<char> value) => _values.Add(value.ToString());
                    IEnumerator<ReadOnlySpan<char>> IEnumerable<ReadOnlySpan<char>>.GetEnumerator() => new SpanEnumerator(_values.GetEnumerator());
                    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

                    private sealed class SpanEnumerator(IEnumerator<string> inner) : IEnumerator<ReadOnlySpan<char>>
                    {
                        public ReadOnlySpan<char> Current => inner.Current.AsSpan();
                        object IEnumerator.Current => throw new NotSupportedException();
                        public void Dispose() => inner.Dispose();
                        public bool MoveNext() => inner.MoveNext();
                        public void Reset() => inner.Reset();
                    }
                }

                [YamlSerializable(typeof(CollectionWithRefStructElement))]
                public partial class MyContext : YamlSerializerContext
                {
                }
                """;

            CSharpParseOptions parseOptions = CompilationHelper.CreateParseOptions((LanguageVersion)1300); // C# 13 required for ref struct collection elements.
            Compilation compilation = CompilationHelper.CreateCompilation(source, parseOptions: parseOptions);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            ISymbol contextSymbol = compilation.GetSymbolsWithName("MyContext").First();
            Collections.Immutable.ImmutableArray<AttributeData> attributes = contextSymbol.GetAttributes();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, attributes[0].GetLocation(), "Did not generate serialization metadata for type 'CollectionWithRefStructElement'."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }
#endif

        [Fact]
        public void YamlIgnoreConditionAlwaysOnTypeWarns()
        {
            Compilation compilation = CompilationHelper.CreateTypeAnnotatedWithYamlIgnoreAlways();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location myClassLocation = compilation.GetSymbolsWithName("MyClass").First().Locations[0];

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, myClassLocation, "The type 'HelloWorld.MyClass' has been annotated with 'YamlIgnoreAttribute' using 'YamlIgnoreCondition.Always' which is not valid on type declarations. The attribute will be ignored."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void Diagnostic_HasPragmaSuppressibleLocation()
        {
            // STYML1039: Polymorphism not supported for fast-path serialization (Warning, configurable).
            string source = """
                #pragma warning disable STYML1039
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBaseClass), GenerationMode = YamlSourceGenerationMode.Serialization)]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerivedClass), "derived")]
                    public class MyBaseClass
                    {
                    }

                    public class MyDerivedClass : MyBaseClass
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            var effective = CompilationWithAnalyzers.GetEffectiveDiagnostics(result.Diagnostics, compilation);
            Diagnostic diagnostic = Assert.Single(effective, d => d.Id == "STYML1039");
            Assert.True(diagnostic.IsSuppressed);
        }

        [Fact]
        public void GenericConverterArityMismatch_WarnsAsExpected()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithGenericConverterArityMismatch();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Location converterAttrLocation = compilation.GetSymbolsWithName("TypeWithArityMismatch").First().GetAttributes()[0].GetLocation();
            INamedTypeSymbol contextSymbol = (INamedTypeSymbol)compilation.GetSymbolsWithName("YamlContext").First();
            Location yamlSerializableAttrLocation = contextSymbol.GetAttributes()[0].GetLocation();

            var expectedDiagnostics = new DiagnosticData[]
            {
                new(DiagnosticSeverity.Warning, yamlSerializableAttrLocation, "Did not generate serialization metadata for type 'HelloWorld.TypeWithArityMismatch<int>'."),
                new(DiagnosticSeverity.Warning, converterAttrLocation, "The 'YamlConverterAttribute' type 'HelloWorld.ConverterWithTwoParams<,>' specified on member 'HelloWorld.TypeWithArityMismatch<int>' is not a converter type or does not contain an accessible parameterless constructor."),
            };

            CompilationHelper.AssertEqualDiagnosticMessages(expectedDiagnostics, result.Diagnostics);
        }

        [Fact]
        public void GenericConverterTypeMismatch_NoSourceGeneratorWarning_FailsAtRuntime()
        {
            // Note: The source generator cannot detect at compile time that the converter
            // converts the wrong type (DifferentType<T> vs TypeWithConverterMismatch<T>).
            // The DifferentTypeConverter<int> is a valid YamlConverter with a parameterless constructor,
            // so the source generator accepts it. The mismatch will cause a runtime error.
            Compilation compilation = CompilationHelper.CreateTypesWithGenericConverterTypeMismatch();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            // Should compile without diagnostics - the converter is technically valid
            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithConverterMismatch<int>");
        }

        [Fact]
        public void NestedGenericConverter_CompileSuccessfully()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithNestedGenericConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            // Should compile without diagnostics
            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithNestedConverter<int, string>");
        }

        [Fact]
        public void ConstrainedGenericConverter_WithSatisfiedConstraint_CompileSuccessfully()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithConstrainedGenericConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithSatisfiedConstraint<string>");
        }

        [Fact]
        public void DeeplyNestedGenericConverter_CompileSuccessfully()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithDeeplyNestedGenericConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithDeeplyNestedConverter<int, string>");
        }

        [Fact]
        public void NonGenericOuterGenericConverter_CompileSuccessfully()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithNonGenericOuterGenericConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithNonGenericOuterConverter<int>");
        }

        [Fact]
        public void ManyParamsAsymmetricNestedConverter_CompileSuccessfully()
        {
            Compilation compilation = CompilationHelper.CreateTypesWithManyParamsAsymmetricNestedConverter();
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.TypeWithManyParams<int, string, bool, double, long>");
        }

        [Fact]
        public void OpenGenericDerivedType_SupportedPattern_CompileSuccessfully()
        {
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                        public T? Value { get; set; }
                    }

                    public class MyDerived<T> : MyBase<T>
                    {
                        public T? Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics);
            result.AssertContainsType("global::HelloWorld.MyBase<int>");
        }

        [Fact]
        public void OpenGenericDerivedType_WrappedTypeArgs_WarnsWithSYSLIB1229()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                        public T? Value { get; set; }
                    }

                    public class MyDerived<T> : MyBase<List<T>>
                    {
                        public T? Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("MyDerived<>", diagnostic.GetMessage());
            Assert.Contains("MyBase<int>", diagnostic.GetMessage());
        }

        [Fact]
        public void OpenGenericDerivedType_GroundMismatchAgainstClosedBase_WarnsWithSYSLIB1229()
        {
            // Derived<T> : Base<T, int> registered on Base<int, string>:
            // position 0 (T) unifies with int, but position 1 (the concrete `int` in the
            // derived's base spec) contradicts `string` in the closed base. The derived is
            // well-formed in isolation -- it just does not apply to this particular closed
            // base. The resolver surfaces a STYML1229 diagnostic.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int, string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T1, T2>
                    {
                        public T1 Value1 { get; set; }
                        public T2 Value2 { get; set; }
                    }

                    public class MyDerived<T> : MyBase<T, int>
                    {
                        public T Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void OpenGenericDerivedType_PartiallyConcrete_Resolves()
        {
            // Derived<T> : Base<T, int> registered on Base<string, int>:
            // position 0 (T) unifies with string, position 1 (concrete int) matches.
            // Expected: closed type is MyDerived<string>, no diagnostic.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<string, int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T1, T2>
                    {
                        public T1 Value1 { get; set; }
                        public T2 Value2 { get; set; }
                    }

                    public class MyDerived<T> : MyBase<T, int>
                    {
                        public T Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);

            Assert.Empty(result.Diagnostics.Where(d => d.Id == "STYML1229"));
        }

        [Fact]
        public void OpenGenericDerivedType_NonGenericBase_WarnsWithSYSLIB1229()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase
                    {
                        public int Value { get; set; }
                    }

                    public class MyDerived<T> : MyBase
                    {
                        public T? Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void OpenGenericDerivedType_SYSLIB1229_IsPragmaSuppressible()
        {
            string source = """
                #pragma warning disable STYML1229
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                        public T? Value { get; set; }
                    }

                    public class MyDerived<T> : MyBase<List<T>>
                    {
                        public T? Extra { get; set; }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            var effective = CompilationWithAnalyzers.GetEffectiveDiagnostics(result.Diagnostics, compilation);
            Diagnostic diagnostic = Assert.Single(effective, d => d.Id == "STYML1229");
            Assert.True(diagnostic.IsSuppressed);
        }

        [Fact]
        public void OpenGenericDerivedType_WrappedArgWithMatchingBase_CompilesSuccessfully()
        {
            // Derived<T> : Base<List<T>> registered on Base<List<int>> unifies to Derived<int>.
            string source = """
                #nullable enable
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<List<int>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                        public T? Value { get; set; }
                    }

                    public class MyDerived<T> : MyBase<List<T>>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_DeepJaggedArgMismatch_WarnsWithSYSLIB1229()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(Animal<List<int[][]>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Silly<>), "silly")]
                    public class Animal<T>
                    {
                    }

                    public class Silly<T> : Animal<List<T[][][]>>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1229", diagnostic.Id);
            Assert.Contains("Silly<>", diagnostic.GetMessage());
            Assert.Contains("Animal<", diagnostic.GetMessage());
            Assert.Contains("List<int[][]>", diagnostic.GetMessage());
        }

        [Fact]
        public void OpenGenericDerivedType_ReorderedParameters_CompilesSuccessfully()
        {
            // Derived<T1, T2> : Base<T2, T1> registered on Base<int, string> unifies to Derived<string, int>.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int, string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<,>), "derived")]
                    public class MyBase<T1, T2>
                    {
                    }

                    public class MyDerived<T1, T2> : MyBase<T2, T1>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_PartialConcretization_CompilesSuccessfully()
        {
            // Derived<T> : Base<T, int> registered on Base<string, int> unifies to Derived<string>.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<string, int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T1, T2>
                    {
                    }

                    public class MyDerived<T> : MyBase<T, int>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_ArrayTypeArg_CompilesSuccessfully()
        {
            // Derived<T> : Base<T[]> registered on Base<int[]> unifies to Derived<int>.
            // Exercises the IArrayTypeSymbol branch of structural unification.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int[]>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                    }

                    public class MyDerived<T> : MyBase<T[]>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_UnboundParameter_WarnsWithSYSLIB1229()
        {
            // Derived<T1, T2> : Base<T1> — T2 is not bound by the base type's arguments.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<,>), "derived")]
                    public class MyBase<T>
                    {
                    }

                    public class MyDerived<T1, T2> : MyBase<T1>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void OpenGenericDerivedType_AmbiguousMatch_WarnsWithSYSLIB1229()
        {
            // Impl<T> : IBase<T>, IBase<List<T>> registered on IBase<List<int>>.
            // Both interface ancestors unify, which is ambiguous.
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IBase<List<int>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBase<T> { }

                    public class Impl<T> : IBase<T>, IBase<List<T>> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void OpenGenericDerivedType_ConstraintViolation_WarnsWithSYSLIB1229()
        {
            // Derived<T> : Base<T> where T : struct, registered on Base<string>.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                    }

                    public class MyDerived<T> : MyBase<T> where T : struct
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "STYML1229");
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("MyDerived<>", diagnostic.GetMessage());
            Assert.Contains("MyBase<string>", diagnostic.GetMessage());
        }

        [Fact]
        public void OpenGenericDerivedType_ReferenceTypeConstraintSatisfied_CompilesSuccessfully()
        {
            // Derived<T> : Base<T> where T : class, registered on Base<string>.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                    }

                    public class MyDerived<T> : MyBase<T> where T : class
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_InterfaceConstraintSatisfied_CompilesSuccessfully()
        {
            // Derived<T> : Base<T> where T : System.IComparable<T>, registered on Base<int>.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<>), "derived")]
                    public class MyBase<T>
                    {
                    }

                    public class MyDerived<T> : MyBase<T> where T : System.IComparable<T>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_ArrayInsideGenericConstraint_CompilesSuccessfully()
        {
            // where T : IEnumerable<U[]> with T=List<int[]>, U=int.
            // Exercises array substitution into a generic constraint type.
            string source = """
                #nullable enable
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyBase<List<int[]>, int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(MyDerived<,>), "derived")]
                    public class MyBase<T1, T2>
                    {
                    }

                    public class MyDerived<T, U> : MyBase<T, U> where T : IEnumerable<U[]>
                    {
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_NestedInGenericOuter_CompilesSuccessfully()
        {
            // Outer<T>.Middle.Leaf<U> : Base<(T, U)> registered on Base<(int, string)>.
            // Exercises ConstructEnclosing rebinding a non-generic intermediate type
            // through a constructed generic outer.
            string source = """
                #nullable enable
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(Base<(int, string)>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Outer<>.Middle.Leaf<>), "leaf")]
                    public class Base<T>
                    {
                    }

                    public class Outer<T>
                    {
                        public class Middle
                        {
                            public class Leaf<U> : Base<(T, U)>
                            {
                            }
                        }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_MultipleGenericInterfaceBases_EachResolvesIndependently()
        {
            // Impl<T> implements two unrelated generic interfaces, each carrying its own
            // open-generic [YamlDerivedType(typeof(Impl<>))]. Each base interface's
            // polymorphism metadata must resolve Impl<> independently against its closed form.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IBaseA<int>))]
                    [YamlSerializable(typeof(IBaseB<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBaseA<T> { }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBaseB<T> { }

                    public class Impl<T> : IBaseA<T>, IBaseB<T> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_GenericInterfaceDiamond_CompilesSuccessfully()
        {
            // Diamond inheritance via two generic interface legs. Impl<T> reaches IBaseA<T>
            // and IBaseB<T> through the intermediate IDerived<T>. Resolution through each
            // diamond leg must independently succeed.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IBaseA<int>))]
                    [YamlSerializable(typeof(IBaseB<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBaseA<T> { }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBaseB<T> { }

                    public interface IDerived<T> : IBaseA<T>, IBaseB<T> { }

                    public class Impl<T> : IDerived<T> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_MultipleInterfaceConstructions_NonAmbiguousResolution_CompilesSuccessfully()
        {
            // Impl<T> reaches IBase<> twice: once via its own type-parameterized base (IBase<T>)
            // and once via inheritance from the non-generic IntBase (IBase<int>). When resolved
            // against the closed base IBase<string>, only the IBase<T> leg unifies (T=string);
            // the IBase<int> leg is incompatible. Resolution must succeed and produce
            // Impl<string>. The both-legs-match scenario (which IS ambiguous) is covered by
            // OpenGenericDerivedType_AmbiguousMatch. Indirecting the IBase<int> leg through a
            // non-generic base class avoids C# CS0695 -- a class cannot directly declare two
            // constructions of the same generic interface that could unify under any
            // substitution.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IBase<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(Impl<>), "impl")]
                    public interface IBase<T> { }

                    public class IntBase : IBase<int> { }

                    public class Impl<T> : IntBase, IBase<T> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_CovariantInterface_CompilesSuccessfully()
        {
            // Open generic VarCovImpl<T> registered on a covariant interface base.
            // Unification is purely structural and ignores 'out'; the resolver closes T
            // to whatever the closed base specifies. No diagnostic expected.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IVarCovBase<Animal>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class Animal { }

                    [YamlDerivedType(typeof(VarCovImpl<>), "covImpl")]
                    public interface IVarCovBase<out T> { }

                    public class VarCovImpl<T> : IVarCovBase<T> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_ContravariantInterface_CompilesSuccessfully()
        {
            // Open generic VarContraImpl<T> registered on a contravariant interface base.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IVarContraBase<Animal>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class Animal { }

                    [YamlDerivedType(typeof(VarContraImpl<>), "contraImpl")]
                    public interface IVarContraBase<in T> { }

                    public class VarContraImpl<T> : IVarContraBase<T> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_BivariantInterface_CompilesSuccessfully()
        {
            // Mixed-variance interface with both 'in' and 'out' parameters.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(IVarBivariantBase<Dog, Animal>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class Animal { }
                    public class Dog : Animal { }

                    [YamlDerivedType(typeof(VarBivariantImpl<,>), "bvImpl")]
                    public interface IVarBivariantBase<in TIn, out TOut> { }

                    public class VarBivariantImpl<TIn, TOut> : IVarBivariantBase<TIn, TOut> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_NestedGenericEnclosingMismatch_WarnsWithSYSLIB1229()
        {
            // Pattern: NestedDerived<T> : NestedBase<NestedOuter<int>.NestedBox<T>>.
            // Closed base: NestedBase<NestedOuter<string>.NestedBox<int>>.
            // The enclosing argument differs (int vs string); unification MUST fail.
            // Pre-fix source-gen ignored ContainingType arguments in TryUnifyWith and would
            // have false-accepted T=int. With the ContainingType walk in place, the
            // mismatch is detected and STYML1229 is reported, matching the reflection
            // resolver's InvalidOperationException at runtime.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(NestedBase<NestedOuter<string>.NestedBox<int>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(NestedDerivedMismatch<>), "nested")]
                    public class NestedBase<T> { }

                    public class NestedOuter<TOuter>
                    {
                        public class NestedBox<TInner> { }
                    }

                    public class NestedDerivedMismatch<T> : NestedBase<NestedOuter<int>.NestedBox<T>> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1229", diagnostic.Id);
        }

        [Fact]
        public void OpenGenericDerivedType_NestedGenericTypeParameterInEnclosing_CompilesSuccessfully()
        {
            // Pattern: NestedDerived<T> : NestedBase<NestedOuter<T>.NestedBox<int>>.
            // Closed base: NestedBase<NestedOuter<string>.NestedBox<int>>.
            // T appears only in the ENCLOSING type's argument list. Pre-fix source-gen
            // ignored ContainingType and reported STYML1229 because T was never bound by
            // the leaf-only TryUnifyWith walk. With the ContainingType walk in place,
            // unification succeeds with T=string and the resolver closes NestedDerived to
            // NestedDerived<string>. No diagnostic expected.
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(NestedBaseB<NestedOuterB<string>.NestedBoxB<int>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(NestedDerivedParamInEnclosing<>), "nestedB")]
                    public class NestedBaseB<T> { }

                    public class NestedOuterB<TOuter>
                    {
                        public class NestedBoxB<TInner> { }
                    }

                    public class NestedDerivedParamInEnclosing<T> : NestedBaseB<NestedOuterB<T>.NestedBoxB<int>> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void OpenGenericDerivedType_CovariantInterfaceConstraintSatisfied_CompilesSuccessfully()
        {
            // where T : IEnumerable<object>. Closing T to List<string> satisfies the
            // constraint ONLY via IEnumerable<out T> covariance (IEnumerable<string> is
            // assignable to IEnumerable<object> only by virtue of 'out T'). Pre-fix
            // source-gen used identity-based interface containment for the constraint
            // check and would have reported STYML1229. With Compilation.HasImplicitConversion
            // in place, source-gen matches reflection's behavior and accepts.
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(ConstraintBase<List<string>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(ConstraintImpl<>), "impl")]
                    public class ConstraintBase<T> { }

                    public class ConstraintImpl<T> : ConstraintBase<T> where T : IEnumerable<object> { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void ClosedTypeInference_UnresolvableOpenGenericDerivedType_ProducesSYSLIB1229()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(GenericBase<int, string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class GenericBase<T1, T2> { }
                    public sealed class GenericDerived<T> : GenericBase<T, int> { }
                    public sealed class GenericFallback : GenericBase<int, string> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "GenericBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1229", diagnostic.Id);
            Assert.Contains("GenericDerived<>", diagnostic.GetMessage());
            Assert.Contains("GenericBase<int, string>", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_NestedGenericHierarchy_CompilesSuccessfully()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(Root<List<int[]>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class Root<T> { }
                    public closed abstract class Middle<T> : Root<List<T>> { }
                    public sealed class Leaf<T> : Middle<T[]> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedTypes(source, "Root", "Middle");
            YamlSourceGeneratorResult result = CompilationHelper.RunYamlSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void ClosedTypeInference_SerializationMode_ProducesSYSLIB1039()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(
                        typeof(ClosedBase),
                        GenerationMode = YamlSourceGenerationMode.Serialization)]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class ClosedBase { }
                    public sealed class ClosedDerived : ClosedBase { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "ClosedBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1039", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void ClosedTypeInference_ConstraintViolation_ProducesSYSLIB1229()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(GenericBase<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class GenericBase<T> { }
                    public sealed class GenericDerived<T> : GenericBase<T> where T : struct { }
                    public sealed class GenericFallback : GenericBase<string> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "GenericBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1229", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("GenericDerived<>", diagnostic.GetMessage());
            Assert.Contains("GenericBase<string>", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_DeepJaggedArgMismatch_ProducesSYSLIB1229()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(Animal<List<int[][]>>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class Animal<T> { }
                    public sealed class Silly<T> : Animal<List<T[][][]>> { }
                    public sealed class Fallback : Animal<List<int[][]>> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "Animal");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1229", diagnostic.Id);
            Assert.Contains("Silly<>", diagnostic.GetMessage());
            Assert.Contains("Animal<", diagnostic.GetMessage());
            Assert.Contains("List<int[][]>", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_IncompatibleConstructedDerivedType_ProducesSYSLIB1240()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(ConcreteMismatchBase<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class ConcreteMismatchBase<T> { }
                    public sealed class ConcreteMismatchIntDerived : ConcreteMismatchBase<int> { }
                    public sealed class ConcreteMismatchStringDerived : ConcreteMismatchBase<string> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "ConcreteMismatchBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1240", diagnostic.Id);
            Assert.Contains("ConcreteMismatchIntDerived", diagnostic.GetMessage());
            Assert.Contains("ConcreteMismatchBase<string>", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_InaccessibleDerivedType_ProducesSYSLIB1241()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(InaccessibleBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class InaccessibleBase { }
                    internal sealed class InaccessibleDerived : InaccessibleBase { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "InaccessibleBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1241", diagnostic.Id);
            Assert.Contains("InaccessibleDerived", diagnostic.GetMessage());
            Assert.Contains("InaccessibleBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_YamlPolymorphicAttribute_InaccessibleDerivedType_ProducesSYSLIB1241()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(InaccessibleBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlPolymorphic(InferClosedTypePolymorphism = true)]
                    public closed abstract class InaccessibleBase { }
                    internal sealed class InaccessibleDerived : InaccessibleBase { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "InaccessibleBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1241", diagnostic.Id);
            Assert.Contains("InaccessibleDerived", diagnostic.GetMessage());
            Assert.Contains("InaccessibleBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_NestedInaccessibleDerivedType_ProducesSYSLIB1241()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(InaccessibleBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class InaccessibleBase { }
                    public closed abstract class InaccessibleMiddle : InaccessibleBase { }
                    internal sealed class InaccessibleDerived : InaccessibleMiddle { }
                }
                """;

            Compilation compilation =
                CreateCompilationWithClosedTypes(source, "InaccessibleBase", "InaccessibleMiddle");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1241", diagnostic.Id);
            Assert.Contains("InaccessibleDerived", diagnostic.GetMessage());
            Assert.Contains("InaccessibleBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_YamlPolymorphicAttribute_OnNonClosedType_ProducesSYSLIB1243()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(NonClosedBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlPolymorphic(InferClosedTypePolymorphism = true)]
                    public abstract class NonClosedBase { }
                    public sealed class NonClosedDerived : NonClosedBase { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1243", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("NonClosedBase", diagnostic.GetMessage());

            Assert.Equal(
                "YamlPolymorphic(InferClosedTypePolymorphism = true)",
                diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        }

        [Fact]
        public void ClosedTypeInference_YamlPolymorphicAttribute_OnNonClosedTypeWithExplicitDerivedTypes_ProducesSYSLIB1243()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(NonClosedBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlPolymorphic(InferClosedTypePolymorphism = true)]
                    [YamlDerivedType(typeof(NonClosedDerived), "derived")]
                    public abstract class NonClosedBase { }
                    public sealed class NonClosedDerived : NonClosedBase { }
                }
                """;

            // Explicit registrations must not mask the opt-in being meaningless on a type that is not closed.
            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1243", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("NonClosedBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_YamlPolymorphicAttribute_OnClosedTypeWithoutDerivedTypes_DoesNotProduceSYSLIB1243()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(EmptyClosedBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlPolymorphic(InferClosedTypePolymorphism = true)]
                    public closed abstract class EmptyClosedBase { }
                }
                """;

            // A closed type declaring no derived types is a valid declaration, so opting in is not a mistake.
            Compilation compilation = CreateCompilationWithClosedType(source, "EmptyClosedBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "STYML1243");
        }

        [Fact]
        public void ClosedTypeInference_YamlPolymorphicAttribute_WithExplicitDerivedTypes_ProducesSYSLIB1244()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(ClosedBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlPolymorphic(InferClosedTypePolymorphism = true)]
                    [YamlDerivedType(typeof(ClosedDerived), "derived")]
                    public closed abstract class ClosedBase { }
                    public sealed class ClosedDerived : ClosedBase { }
                    public sealed class ClosedUnregistered : ClosedBase { }
                }
                """;

            // Explicit registrations replace inference, so ClosedUnregistered is silently dropped.
            Compilation compilation = CreateCompilationWithClosedType(source, "ClosedBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1244", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("ClosedBase", diagnostic.GetMessage());

            Assert.Equal(
                "YamlPolymorphic(InferClosedTypePolymorphism = true)",
                diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        }

        [Fact]
        public void ClosedTypeInference_ContextWideOptIn_WithExplicitDerivedTypes_DoesNotProduceSYSLIB1244()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(ClosedBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(ClosedDerived), "derived")]
                    public closed abstract class ClosedBase { }
                    public sealed class ClosedDerived : ClosedBase { }
                }
                """;

            // A context-wide opt-in is meant to be overridden by explicit registrations, so reporting it
            // would fire on every explicitly registered hierarchy in the context.
            Compilation compilation = CreateCompilationWithClosedType(source, "ClosedBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "STYML1244");
        }

        [Theory]
        [InlineData("\"duplicate\"")]
        [InlineData("42")]
        public void YamlDerivedTypeAttribute_DerivedTypeDiscriminatorCollision_ProducesSYSLIB1242AtAttribute(string discriminator)
        {
            string source = $$"""
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(CollisionBase))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlDerivedType(typeof(A), {{discriminator}})]
                    [YamlDerivedType(typeof(B), {{discriminator}})]
                    public abstract class CollisionBase { }
                    public sealed class A : CollisionBase { }
                    public sealed class B : CollisionBase { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1242", diagnostic.Id);
            Assert.Contains(discriminator.Trim('"'), diagnostic.GetMessage());
            Assert.Contains("CollisionBase", diagnostic.GetMessage());

            string expectedAttribute = $"YamlDerivedType(typeof(B), {discriminator})";
            Assert.Equal(
                expectedAttribute,
                diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        }

        [Fact]
        public void ClosedTypeInference_DerivedTypeDiscriminatorCollision_ProducesSYSLIB1242()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(CollisionBase))]
                    [YamlSerializable(typeof(CollisionA.Node), TypeInfoPropertyName = "CollisionANode")]
                    [YamlSerializable(typeof(CollisionB.Node), TypeInfoPropertyName = "CollisionBNode")]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class CollisionBase { }
                    public static class CollisionA
                    {
                        public sealed class Node : CollisionBase { }
                    }
                    public static class CollisionB
                    {
                        public sealed class Node : CollisionBase { }
                    }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "CollisionBase");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1242", diagnostic.Id);
            Assert.Contains("Node", diagnostic.GetMessage());
            Assert.Contains("CollisionBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_NestedDerivedTypeDiscriminatorCollision_ProducesSYSLIB1242()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(CollisionBase))]
                    [YamlSerializable(typeof(CollisionA.Node), TypeInfoPropertyName = "CollisionANode")]
                    [YamlSerializable(typeof(CollisionB.Node), TypeInfoPropertyName = "CollisionBNode")]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class CollisionBase { }
                    public static class CollisionA
                    {
                        public sealed class Node : CollisionBase { }
                    }
                    public closed abstract class CollisionMiddle : CollisionBase { }
                    public static class CollisionB
                    {
                        public sealed class Node : CollisionMiddle { }
                    }
                }
                """;

            Compilation compilation =
                CreateCompilationWithClosedTypes(source, "CollisionBase", "CollisionMiddle");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1242", diagnostic.Id);
            Assert.Contains("Node", diagnostic.GetMessage());
            Assert.Contains("CollisionBase", diagnostic.GetMessage());
        }

        [Fact]
        public void ClosedTypeInference_GenericDerivedTypesWithSameNameDifferentArities_ProduceSYSLIB1242()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSourceGenerationOptions(InferClosedTypePolymorphism = true)]
                    [YamlSerializable(typeof(Animal<int, int>))]
                    [YamlSerializable(typeof(Cat<int>), TypeInfoPropertyName = "CatOne")]
                    [YamlSerializable(typeof(Cat<int, int>), TypeInfoPropertyName = "CatTwo")]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public closed abstract class Animal<T1, T2> { }
                    public sealed class Cat<T> : Animal<T, T> { }
                    public sealed class Cat<T1, T2> : Animal<T1, T2> { }
                }
                """;

            Compilation compilation = CreateCompilationWithClosedType(source, "Animal");
            YamlSourceGeneratorResult result =
                CompilationHelper.RunYamlSourceGenerator(compilation, disableDiagnosticValidation: true);

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("STYML1242", diagnostic.Id);
            Assert.Contains("Cat", diagnostic.GetMessage());
            Assert.Contains("Animal<int, int>", diagnostic.GetMessage());
        }

        private static Compilation CreateCompilationWithClosedType(string source, string closedTypeName)
        {
            const string ClosedModifier = "closed";
            const string BinderCompatibleModifier = "partial";

            string closedDeclaration = $"{ClosedModifier} abstract class {closedTypeName}";
            Assert.Contains(closedDeclaration, source);

            // The unit-test harness uses Roslyn 4.8, which predates the closed modifier. Parse the
            // declaration as partial, then restore its text while retaining the binder-recognized
            // token kind so the generator's compatibility polyfill observes a closed type.
            source = source.Replace(
                closedDeclaration,
                $"{BinderCompatibleModifier} abstract class {closedTypeName}");

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            SyntaxTree syntaxTree = compilation.SyntaxTrees.First();
            SyntaxNode root = syntaxTree.GetRoot();
            ClassDeclarationSyntax declaration = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(declaration => declaration.Identifier.ValueText == closedTypeName);

            SyntaxToken partialModifier =
                declaration.Modifiers.Single(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
            SyntaxToken closedModifier = SyntaxFactory.Token(
                partialModifier.LeadingTrivia,
                SyntaxKind.PartialKeyword,
                ClosedModifier,
                ClosedModifier,
                partialModifier.TrailingTrivia);
            SyntaxNode updatedRoot = root.ReplaceNode(
                declaration,
                declaration.WithModifiers(declaration.Modifiers.Replace(partialModifier, closedModifier)));

            return compilation.ReplaceSyntaxTree(
                syntaxTree,
                syntaxTree.WithRootAndOptions(updatedRoot, syntaxTree.Options));
        }

        private static Compilation CreateCompilationWithClosedTypes(string source, params string[] closedTypeNames)
        {
            const string ClosedModifier = "closed";
            const string BinderCompatibleModifier = "partial";

            foreach (string closedTypeName in closedTypeNames)
            {
                string closedDeclaration = $"{ClosedModifier} abstract class {closedTypeName}";
                Assert.Contains(closedDeclaration, source);
                source = source.Replace(
                    closedDeclaration,
                    $"abstract {BinderCompatibleModifier} class {closedTypeName}");
            }

            Compilation compilation = CompilationHelper.CreateCompilation(source);
            SyntaxTree syntaxTree = compilation.SyntaxTrees.First();
            SyntaxNode root = syntaxTree.GetRoot();

            foreach (string closedTypeName in closedTypeNames)
            {
                ClassDeclarationSyntax declaration = root
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Single(declaration => declaration.Identifier.ValueText == closedTypeName);
                SyntaxToken partialModifier =
                    declaration.Modifiers.Single(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
                SyntaxToken closedModifier = SyntaxFactory.Token(
                    partialModifier.LeadingTrivia,
                    SyntaxKind.PartialKeyword,
                    ClosedModifier,
                    ClosedModifier,
                    partialModifier.TrailingTrivia);
                root = root.ReplaceNode(
                    declaration,
                    declaration.WithModifiers(declaration.Modifiers.Replace(partialModifier, closedModifier)));
            }

            return compilation.ReplaceSyntaxTree(
                syntaxTree,
                syntaxTree.WithRootAndOptions(root, syntaxTree.Options));
        }
    }
}
