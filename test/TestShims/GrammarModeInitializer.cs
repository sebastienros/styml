// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Fixes the writer's default style for the whole test process before any writer is created (M4's decision 4 and
    /// the test legs): block, the product's default from M4c (leg Y), or JSON-compatible
    /// through the writer's switch when STYML_TEST_WRITER=json (leg YJ). It also checks that the reader reads YAML.
    /// The reader's JSON grammar (decision D1) and its leg J were deleted in M7a (decision D8); the switch that chose it,
    /// <see cref="DeletedReaderSwitchName"/>, is no longer read.
    /// </summary>
    internal static class GrammarModeInitializer
    {
        internal const string DeletedReaderSwitchName = "System.Text.Yaml.Utf8YamlReader.UseJsonGrammar";
        internal const string WriterSwitchName = "System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle";
        internal const string WriterEnvironmentVariable = "STYML_TEST_WRITER";

        internal static bool ExpectJsonCompatibleWriter { get; private set; }

        [ModuleInitializer]
        internal static void Initialize()
        {
            bool jsonWriter = string.Equals(Environment.GetEnvironmentVariable(WriterEnvironmentVariable), "json", StringComparison.OrdinalIgnoreCase);
            ExpectJsonCompatibleWriter = jsonWriter;
            AppContext.SetSwitch(WriterSwitchName, jsonWriter);

            // The imported projects keep their upstream test counts, so instead of an extra [Fact] the modes are asserted
            // here: a wrong mode fails every test of the process.
            AssertReadsYaml();
            AssertWriterMode(jsonWriter);
        }

        /// <summary>Checks the writer's default style: <c>{"a":1}</c> when JSON-compatible, <c>a: 1</c> in block style.</summary>
        internal static void AssertWriterMode(bool expectJsonCompatible)
        {
            // A MemoryStream, not ArrayBufferWriter, which projects on .NET Framework cannot reach.
            var buffer = new System.IO.MemoryStream();
            using (var writer = new Utf8YamlWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("a", 1);
                writer.WriteEndObject();
            }

            bool isJson = buffer.ToArray().AsSpan().SequenceEqual("{\"a\":1}"u8);
            if (isJson != expectJsonCompatible)
            {
                throw new InvalidOperationException($"Expected the {(expectJsonCompatible ? "JSON-compatible" : "block")} writer default, but the writer wrote {Encoding.UTF8.GetString(buffer.ToArray())}.");
            }
        }

        /// <summary>Checks that the reader reads YAML: "a: b" is a mapping.</summary>
        internal static void AssertReadsYaml()
        {
            var reader = new Utf8YamlReader("a: b"u8);
            // The token type by name: YamlTokenType is ambiguous in the generator's unit-test project.
            if (!reader.Read() || reader.TokenType.ToString() != "StartObject")
            {
                throw new InvalidOperationException("Expected the reader to read \"a: b\" as a YAML mapping.");
            }
        }
    }
}

#if !NET
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
