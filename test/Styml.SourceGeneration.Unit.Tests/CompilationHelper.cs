// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Encodings.Web;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace System.Text.Yaml.SourceGeneration.UnitTests
{
    public record YamlSourceGeneratorResult
    {
        public Compilation NewCompilation { get; set; }
        public ImmutableArray<ContextGenerationSpec> ContextGenerationSpecs { get; set; }
        public ImmutableArray<Diagnostic> Diagnostics { get; set; }

        public IEnumerable<TypeGenerationSpec> AllGeneratedTypes
            => ContextGenerationSpecs.SelectMany(ctx => ctx.GeneratedTypes);

        public void AssertContainsType(string fullyQualifiedName)
            => Assert.Contains(
                    AllGeneratedTypes,
                    spec => spec.TypeRef.FullyQualifiedName == fullyQualifiedName);
    }

    public static class CompilationHelper
    {
        private readonly static CSharpParseOptions s_defaultParseOptions = CreateParseOptions();

        public static CSharpParseOptions CreateParseOptions(
            LanguageVersion? version = null,
            DocumentationMode? documentationMode = null)
        {
            return new CSharpParseOptions(
                kind: SourceCodeKind.Regular,
                languageVersion: version ?? LanguageVersion.CSharp9, // C# 9 is the minimum supported lang version by the source generator.
                documentationMode: documentationMode ?? DocumentationMode.Parse);
        }

#if NET
        private static readonly Assembly systemRuntimeAssembly = Assembly.Load(new AssemblyName("System.Runtime"));
#endif

        public static Compilation CreateCompilation(
            string source,
            MetadataReference[] additionalReferences = null,
            string assemblyName = "TestAssembly",
            bool includeSTJ = true,
            CSharpParseOptions? parseOptions = null)
        {

            List<MetadataReference> references = new() {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Type).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(KeyValuePair<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ContractNamespaceAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JavaScriptEncoder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(GeneratedCodeAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ReadOnlySpan<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
#if NET
                MetadataReference.CreateFromFile(typeof(LinkedList<>).Assembly.Location),
                MetadataReference.CreateFromFile(systemRuntimeAssembly.Location),
#else
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location),
                // Styml: the net481 leg runs the netstandard2.0 build of the library (upstream: its net462 build), whose
                // metadata references netstandard.dll; add the .NET Framework facade so the test compilations bind.
                MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51").Location),
#endif
            };

            if (includeSTJ)
            {
                references.Add(MetadataReference.CreateFromFile(typeof(YamlSerializerOptions).Assembly.Location));
            }

            // Add additional references as needed.
            if (additionalReferences != null)
            {
                foreach (MetadataReference reference in additionalReferences)
                {
                    references.Add(reference);
                }
            }

            parseOptions ??= s_defaultParseOptions;
            SyntaxTree[] syntaxTrees = new[]
            {
                CSharpSyntaxTree.ParseText(source, parseOptions),
#if !NET
                CSharpSyntaxTree.ParseText(NetfxPolyfillAttributes, parseOptions),
#elif !NET11_0_OR_GREATER // Styml: net10.0 test leg; the union types are new in .NET 11
                CSharpSyntaxTree.ParseText(Net10UnionPolyfillAttributes, parseOptions),
#endif
            };

            return CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
#if !NET
                    // Styml: the netstandard2.0 library references the netstandard2.0 assets of System.Memory & co.,
                    // whose assembly versions are lower than the net462 assets loaded in this process (e.g. System.Memory
                    // 4.0.2.0 vs 4.0.5.0). Roslyn reports that unification as warning CS1702; upstream's net462 build of
                    // the library references the net462 assets, so it never sees it.
                    .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { ["CS1702"] = ReportDiagnostic.Suppress })
#endif
            );
        }

        public static SyntaxTree ParseSource(string source, CSharpParseOptions? options = null)
            => CSharpSyntaxTree.ParseText(source, options ?? s_defaultParseOptions);

        public static CSharpGeneratorDriver CreateYamlSourceGeneratorDriver(Compilation compilation, YamlSourceGenerator? generator = null)
        {
            generator ??= new();
            CSharpParseOptions parseOptions = compilation.SyntaxTrees
                .OfType<CSharpSyntaxTree>()
                .Select(tree => tree.Options)
                .FirstOrDefault() ?? s_defaultParseOptions;

            return
#if ROSLYN4_0_OR_GREATER
                CSharpGeneratorDriver.Create(
                    generators: new ISourceGenerator[] { generator.AsSourceGenerator() },
                    parseOptions: parseOptions,
                    driverOptions: new GeneratorDriverOptions(
                        disabledOutputs: IncrementalGeneratorOutputKind.None,
                        trackIncrementalGeneratorSteps: true));
#else
                CSharpGeneratorDriver.Create(
                    generators: new ISourceGenerator[] { generator },
                    parseOptions: parseOptions);
#endif
        }

        public static YamlSourceGeneratorResult RunYamlSourceGenerator(
            Compilation compilation,
            bool disableDiagnosticValidation = false,
            ITestOutputHelper? logger = null)
        {
            var generatedSpecs = ImmutableArray<ContextGenerationSpec>.Empty;
            var generator = new YamlSourceGenerator
            {
                OnSourceEmitting = specs => generatedSpecs = specs
            };

            CSharpGeneratorDriver driver = CreateYamlSourceGeneratorDriver(compilation, generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outCompilation, out ImmutableArray<Diagnostic> diagnostics);

            if (logger is not null)
            {
                foreach (Diagnostic diagnostic in outCompilation.GetDiagnostics().Concat(diagnostics))
                {
                    logger.WriteLine(diagnostic.ToString());
                }

                foreach (var tree in outCompilation.SyntaxTrees)
                {
                    LogGeneratedCode(tree, logger);
                }
            }

            if (!disableDiagnosticValidation)
            {
                outCompilation.GetDiagnostics().AssertMaxSeverity(DiagnosticSeverity.Info);
                diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);
            }

            return new()
            {
                NewCompilation = outCompilation,
                Diagnostics = diagnostics,
                ContextGenerationSpecs = generatedSpecs,
            };
        }

        public static byte[] CreateAssemblyImage(Compilation compilation)
        {
            MemoryStream ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);
            if (!emitResult.Success)
            {
                throw new InvalidOperationException();
            }
            return ms.ToArray();
        }

#if NET && !NET11_0_OR_GREATER
        // Styml: net10.0 test leg. Same union declarations as NetfxPolyfillAttributes below; .NET 10 has the rest.
        private const string Net10UnionPolyfillAttributes = """
            namespace System.Runtime.CompilerServices
            {
                internal interface IUnion
                {
                    object Value { get; }
                }

                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                internal sealed class UnionAttribute : Attribute { }
            }
            """;
#endif

#if !NET
        private const string NetfxPolyfillAttributes = """
            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit { }

                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
                internal sealed class RequiredMemberAttribute : Attribute { }

                [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
                internal sealed class CompilerFeatureRequiredAttribute : Attribute
                {
                    public CompilerFeatureRequiredAttribute(string featureName) { }
                }

                internal interface IUnion
                {
                    object Value { get; }
                }

                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                internal sealed class UnionAttribute : Attribute { }
            }
            """;
#endif

        public static Compilation CreateReferencedLocationCompilation()
        {
            string source = """
                namespace ReferencedAssembly
                {
                    public class Location
                    {
                        public int Id { get; set; }
                        public string Address1 { get; set; }
                        public string Address2 { get; set; }
                        public string City { get; set; }
                        public string State { get; set; }
                        public string PostalCode { get; set; }
                        public string Name { get; set; }
                        public string PhoneNumber { get; set; }
                        public string Country { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCampaignSummaryViewModelCompilation()
        {
            string source = """
                namespace ReferencedAssembly
                {
                    public class CampaignSummaryViewModel
                    {
                        public int Id { get; set; }
                        public string Title { get; set; }
                        public string Description { get; set; }
                        public string ImageUrl { get; set; }
                        public string OrganizationName { get; set; }
                        public string Headline { get; set; }
                    }
                }
                """;

            return CreateCompilation(source, assemblyName: "CampaignSummaryAssembly");
        }

        public static Compilation CreateActiveOrUpcomingEventCompilation()
        {
            string source = """
                using System;
                namespace ReferencedAssembly
                {
                    public class ActiveOrUpcomingEvent
                    {
                        public int Id { get; set; }
                        public string ImageUrl { get; set; }
                        public string Name { get; set; }
                        public string CampaignName { get; set; }
                        public string CampaignManagedOrganizerName { get; set; }
                        public string Description { get; set; }
                        public DateTimeOffset StartDate { get; set; }
                        public DateTimeOffset EndDate { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateReferencedExperimentalPocoWithPolyfillCompilation()
        {
            string source = """
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

                namespace ReferencedAssembly
                {
                    [System.Diagnostics.CodeAnalysis.Experimental("EXP_TEST")]
                    public class ExperimentalPocoFromLib
                    {
                        public int Value { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateReferencedHighLowTempsCompilation()
        {
            string source = """
                namespace ReferencedAssembly
                {
                    public class HighLowTemps
                    {
                        public int High { get; set; }
                        public int Low { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateRepeatedLocationsCompilation()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace YamlSourceGeneration
                {
                    [YamlSerializable(typeof(Fake.Location))]
                    [YamlSerializable(typeof(HelloWorld.Location))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }
                }

                namespace Fake
                {
                    public class Location
                    {
                        public int FakeId { get; set; }
                        public string FakeAddress1 { get; set; }
                        public string FakeAddress2 { get; set; }
                        public string FakeCity { get; set; }
                        public string FakeState { get; set; }
                        public string FakePostalCode { get; set; }
                        public string FakeName { get; set; }
                        public string FakePhoneNumber { get; set; }
                        public string FakeCountry { get; set; }
                    }
                }

                namespace HelloWorld
                {                
                    public class Location
                    {
                        public int Id { get; set; }
                        public string Address1 { get; set; }
                        public string Address2 { get; set; }
                        public string City { get; set; }
                        public string State { get; set; }
                        public string PostalCode { get; set; }
                        public string Name { get; set; }
                        public string PhoneNumber { get; set; }
                        public string Country { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithInitOnlyProperties()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {                
                    public class Location
                    {
                        public int Id { get; init; }
                        public string Address1 { get; init; }
                        public string Address2 { get; init; }
                        public string City { get; init; }
                        public string State { get; init; }
                        public string PostalCode { get; init; }
                        public string Name { get; init; }
                        public string PhoneNumber { get; init; }
                        public string Country { get; init; }
                    }

                    [YamlSerializable(typeof(Location))]
                    public partial class MyYamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithConstructorInitOnlyProperties()
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

                            public int Value { get; init; }
                        }

                        [YamlSerializable(typeof(MyClass))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithMixedInitOnlyProperties()
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

                            public int Value { get; init; }
                            public string Orphaned { get; init; }
                        }

                        [YamlSerializable(typeof(MyClass))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            return CreateCompilation(source);
        }

#if ROSLYN4_4_OR_GREATER
        public static Compilation CreateCompilationWithRequiredProperties()
        {
            string source = """
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        public class MyClass
                        {
                            public required string Required1 { get; set; }
                            public required string Required2 { get; set; }

                            public MyClass(string required1)
                            {
                                Required1 = required1;
                            }
                        }

                        [YamlSerializable(typeof(MyClass))]
                        public partial class MyYamlContext : YamlSerializerContext
                        {
                        }
                    }
                """;

            CSharpParseOptions parseOptions = CreateParseOptions(LanguageVersion.CSharp11);
            return CreateCompilation(source, parseOptions: parseOptions);
        }
#endif

        public static Compilation CreateCompilationWithRecordPositionalParameters()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {                
                    public record Location
                    (
                        int Id,
                        string Address1,
                        string Address2,
                        string City,
                        string State,
                        string PostalCode,
                        string Name,
                        string PhoneNumber,
                        string Country
                    );

                    [YamlSerializable(typeof(Location))]
                    public partial class MyYamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithInaccessibleYamlIncludeProperties()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {                
                    public class Location
                    {
                        [YamlInclude]
                        public int publicField = 1;
                        [YamlInclude]
                        internal int internalField = 2;
                        [YamlInclude]
                        private int privateField = 4;
                        [YamlInclude]
                        protected int protectedField = 8;

                        [YamlInclude]
                        public int Id { get; private set; }
                        [YamlInclude]
                        public string Address1 { get; internal set; }
                        [YamlInclude]
                        private string Address2 { get; set; }
                        [YamlInclude]
                        public string PhoneNumber { internal get; set; }
                        [YamlInclude]
                        public string Country { private get; set; }
                        [YamlInclude]
                        internal string InternalProperty { get; set; }
                        [YamlInclude]
                        protected string ProtectedProperty { get; set; }
                        [YamlInclude]
                        internal string InternalPropertyWithPrivateGetter { private get; set; }
                        [YamlInclude]
                        internal string InternalPropertyWithPrivateSetter { get; private set; }

                        public int GetPrivateField() => privateField;
                    }

                    [YamlSerializable(typeof(Location))]
                    public partial class MyYamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateReferencedLibRecordCompilation()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace ReferencedAssembly
                {
                    public record LibRecord(int Id)
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

            return CreateCompilation(source);
        }

        public static Compilation CreateReferencedSimpleLibRecordCompilation()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace ReferencedAssembly
                {
                    public record LibRecord
                    {
                        public int Id { get; set; }
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

            return CreateCompilation(source);
        }

        public static Compilation CreateReferencedModelWithFullyDocumentedProperties()
        {
            string source = """
                namespace ReferencedAssembly
                {
                    /// <summary>
                    /// Documentation
                    /// </summary>
                    public class Model
                    {
                        /// <summary>
                        /// Documentation
                        /// </summary>
                        public int Property1 { get; set; }

                        /// <summary>
                        /// Documentation
                        /// </summary>
                        public int Property2 { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreatePolymorphicClassOnFastPathContext()
        {
            string source = """
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

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesAnnotatedWithYamlStringEnumConverter()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyClass))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    public class MyClass
                    {
                        public Enum1 Enum1Prop { get; set; }

                        [YamlConverter(typeof(YamlStringEnumConverter))]
                        public Enum2 Enum2Prop { get; set; }
                    }

                    [YamlConverter(typeof(YamlStringEnumConverter))]
                    public enum Enum1 { A, B, C };
                    
                    public enum Enum2 { A, B, C };
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithInvalidYamlConverterAttributeType()
        {
            string source = """
                    using System;
                    using System.Text.Yaml;
                    using System.Text.Yaml.Serialization;

                    namespace HelloWorld
                    {
                        [YamlSerializable(typeof(MyClass))]
                        internal partial class YamlContext : YamlSerializerContext
                        {
                        }

                        public class MyClass
                        {
                            [YamlConverter(null)]
                            public int Value1 { get; set; }

                            [YamlConverter(typeof(int))]
                            public int Value2 { get; set; }

                            [YamlConverter(typeof(InacessibleConverter))]
                            public int Value3 { get; set; }
                        }

                        public class InacessibleConverter : YamlConverter<int>
                        {
                            private InacessibleConverter()
                            { }

                            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                            {
                                throw new NotImplementedException();
                            }

                            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
                            {
                                throw new NotImplementedException();
                            }
                        }
                    }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithDerivedYamlConverterAttributeAnnotations()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(ClassWithConverterDeclaration))]
                    [YamlSerializable(typeof(ClassWithPropertyConverterDeclaration))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [MyYamlConverter]
                    public class ClassWithConverterDeclaration
                    {
                    }

                    public partial class ClassWithPropertyConverterDeclaration : YamlSerializerContext
                    {
                        [MyYamlConverter]
                        public int Value { get; set; }
                    }

                    public class MyYamlConverterAttribute : YamlConverterAttribute
                    {
                        public override YamlConverter? CreateConverter(Type typeToConvert) => null;
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateContextWithUnboundGenericTypeDeclarations()
        {
            string source = """
                using System.Collections.Generic;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(List<>))]
                    [YamlSerializable(typeof(Dictionary<,>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateContextWithErrorTypeDeclarations()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(BogusType))]
                    [YamlSerializable(typeof(BogusType<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateCompilationWithYamlConstructorAttributeAnnotations()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {                
                    public class ClassWithPublicCtor
                    {
                        [YamlConstructor]
                        public ClassWithPublicCtor() { }
                    }

                    public class ClassWithInternalCtor
                    {
                        [YamlConstructor]
                        internal ClassWithInternalCtor() { }
                    }

                    public class ClassWithProtectedCtor
                    {
                        [YamlConstructor]
                        protected ClassWithProtectedCtor() { }
                    }

                    public class ClassWithPrivateCtor
                    {
                        [YamlConstructor]
                        private ClassWithPrivateCtor() { }
                    }

                    [YamlSerializable(typeof(ClassWithPublicCtor))]
                    [YamlSerializable(typeof(ClassWithInternalCtor))]
                    [YamlSerializable(typeof(ClassWithProtectedCtor))]
                    [YamlSerializable(typeof(ClassWithPrivateCtor))]
                    public partial class MyYamlContext : YamlSerializerContext
                    {
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypeAnnotatedWithYamlIgnoreAlways()
        {
            string source = """
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(MyClass))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlIgnore(Condition = YamlIgnoreCondition.Always)]
                    public class MyClass
                    {
                        public string? MyString { get; set; }
                        public int MyInt { get; set; }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        internal static void AssertEqualDiagnosticMessages(
            IEnumerable<DiagnosticData> expectedDiags,
            IEnumerable<Diagnostic> actualDiags)
        {
            HashSet<DiagnosticData> expectedSet = new(expectedDiags);
            HashSet<DiagnosticData> actualSet = new(actualDiags.Select(d => new DiagnosticData(d.Severity, d.Location, d.GetMessage())));
            AssertExtensions.Equal(expectedSet, actualSet);
        }

        internal static Location? GetLocation(this AttributeData attributeData)
        {
            SyntaxReference? reference = attributeData.ApplicationSyntaxReference;
            return reference?.SyntaxTree.GetLocation(reference.Span);
        }

        internal static void AssertMaxSeverity(this IEnumerable<Diagnostic> diagnostics, DiagnosticSeverity maxSeverity)
        {
            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity > maxSeverity);
        }

        private static void LogGeneratedCode(SyntaxTree tree, ITestOutputHelper logger)
        {
            logger.WriteLine(FileSeparator);
            logger.WriteLine($"{tree.FilePath} content:");
            logger.WriteLine(FileSeparator);
            using NumberedSourceFileWriter lineWriter = new(logger);
            tree.GetRoot().WriteTo(lineWriter);
            lineWriter.WriteLine(string.Empty);
        }

        public static Compilation CreateTypesWithGenericConverterArityMismatch()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithArityMismatch<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(ConverterWithTwoParams<,>))]
                    public class TypeWithArityMismatch<T>
                    {
                        public T Value { get; set; }
                    }

                    public sealed class ConverterWithTwoParams<T1, T2> : YamlConverter<TypeWithArityMismatch<T1>>
                    {
                        public override TypeWithArityMismatch<T1> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                            => throw new NotImplementedException();

                        public override void Write(Utf8YamlWriter writer, TypeWithArityMismatch<T1> value, YamlSerializerOptions options)
                            => throw new NotImplementedException();
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithGenericConverterTypeMismatch()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithConverterMismatch<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(DifferentTypeConverter<>))]
                    public class TypeWithConverterMismatch<T>
                    {
                        public T Value { get; set; }
                    }

                    public class DifferentType<T>
                    {
                        public T Value { get; set; }
                    }

                    public sealed class DifferentTypeConverter<T> : YamlConverter<DifferentType<T>>
                    {
                        public override DifferentType<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                            => throw new NotImplementedException();

                        public override void Write(Utf8YamlWriter writer, DifferentType<T> value, YamlSerializerOptions options)
                            => throw new NotImplementedException();
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithNestedGenericConverter()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithNestedConverter<int, string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(Container<>.NestedConverter<>))]
                    public class TypeWithNestedConverter<T1, T2>
                    {
                        public T1 Value1 { get; set; }
                        public T2 Value2 { get; set; }
                    }

                    public class Container<T>
                    {
                        public sealed class NestedConverter<U> : YamlConverter<TypeWithNestedConverter<T, U>>
                        {
                            public override TypeWithNestedConverter<T, U> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                            {
                                if (reader.TokenType != YamlTokenType.StartObject)
                                    throw new YamlException();

                                var result = new TypeWithNestedConverter<T, U>();
                                while (reader.Read())
                                {
                                    if (reader.TokenType == YamlTokenType.EndObject)
                                        break;

                                    if (reader.TokenType != YamlTokenType.PropertyName)
                                        throw new YamlException();

                                    string propertyName = reader.GetString()!;
                                    reader.Read();

                                    if (propertyName == "Value1")
                                        result.Value1 = YamlSerializer.Deserialize<T>(ref reader, options)!;
                                    else if (propertyName == "Value2")
                                        result.Value2 = YamlSerializer.Deserialize<U>(ref reader, options)!;
                                }
                                return result;
                            }

                            public override void Write(Utf8YamlWriter writer, TypeWithNestedConverter<T, U> value, YamlSerializerOptions options)
                            {
                                writer.WriteStartObject();
                                writer.WritePropertyName("Value1");
                                YamlSerializer.Serialize(writer, value.Value1, options);
                                writer.WritePropertyName("Value2");
                                YamlSerializer.Serialize(writer, value.Value2, options);
                                writer.WriteEndObject();
                            }
                        }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithConstrainedGenericConverter()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithSatisfiedConstraint<string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(ConverterWithClassConstraint<>))]
                    public class TypeWithSatisfiedConstraint<T>
                    {
                        public T Value { get; set; }
                    }

                    public sealed class ConverterWithClassConstraint<T> : YamlConverter<TypeWithSatisfiedConstraint<T>> where T : class
                    {
                        public override TypeWithSatisfiedConstraint<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                        {
                            if (reader.TokenType != YamlTokenType.StartObject)
                                throw new YamlException();

                            var result = new TypeWithSatisfiedConstraint<T>();
                            while (reader.Read())
                            {
                                if (reader.TokenType == YamlTokenType.EndObject)
                                    break;

                                if (reader.TokenType != YamlTokenType.PropertyName)
                                    throw new YamlException();

                                string propertyName = reader.GetString()!;
                                reader.Read();

                                if (propertyName == "Value")
                                    result.Value = YamlSerializer.Deserialize<T>(ref reader, options)!;
                            }
                            return result;
                        }

                        public override void Write(Utf8YamlWriter writer, TypeWithSatisfiedConstraint<T> value, YamlSerializerOptions options)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("Value");
                            YamlSerializer.Serialize(writer, value.Value, options);
                            writer.WriteEndObject();
                        }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithDeeplyNestedGenericConverter()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithDeeplyNestedConverter<int, string>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(OuterGeneric<>.MiddleNonGeneric.InnerConverter<>))]
                    public class TypeWithDeeplyNestedConverter<T1, T2>
                    {
                        public T1 Value1 { get; set; }
                        public T2 Value2 { get; set; }
                    }

                    public class OuterGeneric<T>
                    {
                        public class MiddleNonGeneric
                        {
                            public sealed class InnerConverter<U> : YamlConverter<TypeWithDeeplyNestedConverter<T, U>>
                            {
                                public override TypeWithDeeplyNestedConverter<T, U> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                                    => throw new NotImplementedException();

                                public override void Write(Utf8YamlWriter writer, TypeWithDeeplyNestedConverter<T, U> value, YamlSerializerOptions options)
                                    => throw new NotImplementedException();
                            }
                        }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithNonGenericOuterGenericConverter()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithNonGenericOuterConverter<int>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(NonGenericOuter.SingleLevelConverter<>))]
                    public class TypeWithNonGenericOuterConverter<T>
                    {
                        public T Value { get; set; }
                    }

                    public class NonGenericOuter
                    {
                        public sealed class SingleLevelConverter<T> : YamlConverter<TypeWithNonGenericOuterConverter<T>>
                        {
                            public override TypeWithNonGenericOuterConverter<T> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                                => throw new NotImplementedException();

                            public override void Write(Utf8YamlWriter writer, TypeWithNonGenericOuterConverter<T> value, YamlSerializerOptions options)
                                => throw new NotImplementedException();
                        }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        public static Compilation CreateTypesWithManyParamsAsymmetricNestedConverter()
        {
            string source = """
                using System;
                using System.Text.Yaml;
                using System.Text.Yaml.Serialization;

                namespace HelloWorld
                {
                    [YamlSerializable(typeof(TypeWithManyParams<int, string, bool, double, long>))]
                    internal partial class YamlContext : YamlSerializerContext
                    {
                    }

                    [YamlConverter(typeof(Level1<,,>.Level2<>.Level3Converter<>))]
                    public class TypeWithManyParams<T1, T2, T3, T4, T5>
                    {
                        public T1 Value1 { get; set; }
                        public T2 Value2 { get; set; }
                        public T3 Value3 { get; set; }
                        public T4 Value4 { get; set; }
                        public T5 Value5 { get; set; }
                    }

                    public class Level1<A, B, C>
                    {
                        public class Level2<D>
                        {
                            public sealed class Level3Converter<E> : YamlConverter<TypeWithManyParams<A, B, C, D, E>>
                            {
                                public override TypeWithManyParams<A, B, C, D, E> Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                                    => throw new NotImplementedException();

                                public override void Write(Utf8YamlWriter writer, TypeWithManyParams<A, B, C, D, E> value, YamlSerializerOptions options)
                                    => throw new NotImplementedException();
                            }
                        }
                    }
                }
                """;

            return CreateCompilation(source);
        }

        private static readonly string FileSeparator = new string('=', 140);

        private sealed class NumberedSourceFileWriter : TextWriter
        {
            private readonly ITestOutputHelper _logger;
            private readonly StringBuilder _lineBuilder = new StringBuilder();
            private int _lineNumber;

            internal NumberedSourceFileWriter(ITestOutputHelper logger)
            {
                _logger = logger;
            }

            public override Encoding Encoding => Encoding.Unicode;

            public override void WriteLine(string? value)
            {
                _logger.WriteLine($"{++_lineNumber,6}: {_lineBuilder}{value}");
                _lineBuilder.Clear();
            }

            public override void Write(string? value)
            {
                if (value is null)
                {
                    return;
                }

                if (value.EndsWith("\r\n", StringComparison.Ordinal))
                {
                    WriteLine(value.Substring(0, value.Length - 2));
                }
                else if (value.EndsWith("\n", StringComparison.Ordinal))
                {
                    WriteLine(value.Substring(0, value.Length - 1));
                }
                else
                {
                    _lineBuilder.Append(value);
                }
            }
        }
    }

    public record struct DiagnosticData(
        DiagnosticSeverity Severity,
        string FilePath,
        LinePositionSpan LinePositionSpan,
        string Message)
    {
        public DiagnosticData(DiagnosticSeverity severity, Location location, string message)
            : this(severity, location.SourceTree?.FilePath ?? "", location.GetLineSpan().Span, TrimCultureSensitiveMessage(message))
        {
        }

        // for non-English runs, trim the message content since it might be translated.
        private static string TrimCultureSensitiveMessage(string message) => s_IsEnglishCulture ? message : "";
        private readonly static bool s_IsEnglishCulture = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        public override string ToString() => $"{Severity}, {Message}, {FilePath}@{LinePositionSpan}";
    }
}
