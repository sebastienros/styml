// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// Every library's read calls, with instances built once.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Styml.Benchmarks.Model;
using SJ = System.Text.Json.JsonSerializer;
using SY = SharpYaml;
using VY = VYaml.Serialization.YamlSerializer;
using YD = YamlDotNet.Serialization;
using YS = System.Text.Yaml.YamlSerializer;

namespace Styml.Benchmarks
{
    internal static class Libraries
    {
        // Styml
        public static readonly System.Text.Yaml.YamlSerializerOptions StymlOptions = new() { PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase };
        public static readonly System.Text.Yaml.YamlSerializerOptions StymlOptionsNoKeys = new() { PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase, DisallowImplicitCollectionKeys = true };
        public static readonly StymlContext StymlGenerated = StymlContext.Default;
        public static readonly StymlContext StymlGeneratedNoKeys = new(new System.Text.Yaml.YamlSerializerOptions { PropertyNamingPolicy = System.Text.Yaml.YamlNamingPolicy.CamelCase, DisallowImplicitCollectionKeys = true });
        public static readonly System.Text.Yaml.YamlReaderOptions StymlReader = default;
        public static readonly System.Text.Yaml.YamlReaderOptions StymlReaderNoKeys = new() { DisallowImplicitCollectionKeys = true };

        // System.Text.Json
        public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        public static readonly JsonContext JsonGenerated = JsonContext.Default;

        // YamlDotNet
        public static readonly YD.IDeserializer YamlDotNetReflection = new YD.DeserializerBuilder().WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance).Build();
        public static readonly YD.IDeserializer YamlDotNetStatic = new YD.StaticDeserializerBuilder(new YamlDotNetContext()).WithNamingConvention(YD.NamingConventions.CamelCaseNamingConvention.Instance).Build();

        // SharpYaml
        public static readonly SY.YamlSerializerOptions SharpYamlOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        public static readonly SharpYamlContext SharpYamlGenerated = SharpYamlContext.Default;

        // VYaml (naming from [YamlObject(NamingConvention.LowerCamelCase)] on the model types)
        public static readonly global::VYaml.Serialization.YamlSerializerOptions VYamlOptions = global::VYaml.Serialization.YamlSerializerOptions.Standard;

        // -------------------------------------------------------------------------------------------------------------
        // Deserialization, by document family.

        public static object? Styml(ReadOnlySpan<byte> utf8, Family family, System.Text.Yaml.YamlSerializerOptions options) => family switch
        {
            Family.K8s => YS.Deserialize<K8sList>(utf8, options),
            Family.Compose => YS.Deserialize<ComposeFile>(utf8, options),
            Family.Workflow => YS.Deserialize<Workflow>(utf8, options),
            Family.OpenApi => YS.Deserialize<OpenApiDocument>(utf8, options),
            _ => YS.Deserialize<List<Record>>(utf8, options),
        };

        public static object? StymlText(string text, Family family, StymlContext context) => family switch
        {
            Family.K8s => YS.Deserialize(text, context.K8sList),
            Family.Compose => YS.Deserialize(text, context.ComposeFile),
            Family.Workflow => YS.Deserialize(text, context.Workflow),
            Family.OpenApi => YS.Deserialize(text, context.OpenApiDocument),
            _ => YS.Deserialize(text, context.ListRecord),
        };

        public static object? StymlGeneratedRead(ReadOnlySpan<byte> utf8, Family family, StymlContext context) => family switch
        {
            Family.K8s => YS.Deserialize(utf8, context.K8sList),
            Family.Compose => YS.Deserialize(utf8, context.ComposeFile),
            Family.Workflow => YS.Deserialize(utf8, context.Workflow),
            Family.OpenApi => YS.Deserialize(utf8, context.OpenApiDocument),
            _ => YS.Deserialize(utf8, context.ListRecord),
        };

        public static object? Json(ReadOnlySpan<byte> utf8, Family family) => family switch
        {
            Family.K8s => SJ.Deserialize<K8sList>(utf8, JsonOptions),
            Family.Compose => SJ.Deserialize<ComposeFile>(utf8, JsonOptions),
            Family.Workflow => SJ.Deserialize<Workflow>(utf8, JsonOptions),
            Family.OpenApi => SJ.Deserialize<OpenApiDocument>(utf8, JsonOptions),
            _ => SJ.Deserialize<List<Record>>(utf8, JsonOptions),
        };

        public static object? JsonGeneratedRead(ReadOnlySpan<byte> utf8, Family family) => family switch
        {
            Family.K8s => SJ.Deserialize(utf8, JsonGenerated.K8sList),
            Family.Compose => SJ.Deserialize(utf8, JsonGenerated.ComposeFile),
            Family.Workflow => SJ.Deserialize(utf8, JsonGenerated.Workflow),
            Family.OpenApi => SJ.Deserialize(utf8, JsonGenerated.OpenApiDocument),
            _ => SJ.Deserialize(utf8, JsonGenerated.ListRecord),
        };

        public static object? JsonGeneratedText(string text, Family family) => family switch
        {
            Family.K8s => SJ.Deserialize(text, JsonGenerated.K8sList),
            Family.Compose => SJ.Deserialize(text, JsonGenerated.ComposeFile),
            Family.Workflow => SJ.Deserialize(text, JsonGenerated.Workflow),
            Family.OpenApi => SJ.Deserialize(text, JsonGenerated.OpenApiDocument),
            _ => SJ.Deserialize(text, JsonGenerated.ListRecord),
        };

        public static object? YamlDotNet(string text, Family family, YD.IDeserializer deserializer) => family switch
        {
            Family.K8s => deserializer.Deserialize<K8sList>(text),
            Family.Compose => deserializer.Deserialize<ComposeFile>(text),
            Family.Workflow => deserializer.Deserialize<Workflow>(text),
            Family.OpenApi => deserializer.Deserialize<OpenApiDocument>(text),
            _ => deserializer.Deserialize<List<Record>>(text),
        };

        public static object? SharpYaml(string text, Family family) => family switch
        {
            Family.K8s => SY.YamlSerializer.Deserialize<K8sList>(text, SharpYamlOptions),
            Family.Compose => SY.YamlSerializer.Deserialize<ComposeFile>(text, SharpYamlOptions),
            Family.Workflow => SY.YamlSerializer.Deserialize<Workflow>(text, SharpYamlOptions),
            Family.OpenApi => SY.YamlSerializer.Deserialize<OpenApiDocument>(text, SharpYamlOptions),
            _ => SY.YamlSerializer.Deserialize<List<Record>>(text, SharpYamlOptions),
        };

        public static object? SharpYamlGeneratedRead(string text, Family family) => family switch
        {
            Family.K8s => SY.YamlSerializer.Deserialize<K8sList>(text, SharpYamlGenerated),
            Family.Compose => SY.YamlSerializer.Deserialize<ComposeFile>(text, SharpYamlGenerated),
            Family.Workflow => SY.YamlSerializer.Deserialize<Workflow>(text, SharpYamlGenerated),
            Family.OpenApi => SY.YamlSerializer.Deserialize<OpenApiDocument>(text, SharpYamlGenerated),
            _ => SY.YamlSerializer.Deserialize<List<Record>>(text, SharpYamlGenerated),
        };

        public static object? VYaml(ReadOnlyMemory<byte> utf8, Family family) => family switch
        {
            Family.K8s => VY.Deserialize<K8sList>(utf8, VYamlOptions),
            Family.Compose => VY.Deserialize<ComposeFile>(utf8, VYamlOptions),
            Family.Workflow => VY.Deserialize<Workflow>(utf8, VYamlOptions),
            Family.OpenApi => VY.Deserialize<OpenApiDocument>(utf8, VYamlOptions),
            _ => VY.Deserialize<List<Record>>(utf8, VYamlOptions),
        };

        // -------------------------------------------------------------------------------------------------------------
        // Tokenizing. Each returns a count so that the loop cannot be optimised away; with 'text', the scalar texts are
        // materialised as strings and their lengths summed.

        public static long StymlTokens(ReadOnlySpan<byte> utf8, System.Text.Yaml.YamlReaderOptions options, bool text)
        {
            var reader = new System.Text.Yaml.Utf8YamlReader(utf8, options);
            long n = 0;
            while (reader.Read())
            {
                n++;
                if (text)
                {
                    switch (reader.TokenType)
                    {
                        case System.Text.Yaml.YamlTokenType.PropertyName:
                        case System.Text.Yaml.YamlTokenType.String:
                        case System.Text.Yaml.YamlTokenType.Number:
                        case System.Text.Yaml.YamlTokenType.True:
                        case System.Text.Yaml.YamlTokenType.False:
                            n += reader.GetString()!.Length;
                            break;
                        case System.Text.Yaml.YamlTokenType.Null:
                            n += Encoding.UTF8.GetString(reader.ValueSpan).Length;
                            break;
                    }
                }
            }
            return n;
        }

        public static long JsonTokens(ReadOnlySpan<byte> utf8, bool text)
        {
            var reader = new Utf8JsonReader(utf8);
            long n = 0;
            while (reader.Read())
            {
                n++;
                if (text)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                        case JsonTokenType.String:
                            n += reader.GetString()!.Length;
                            break;
                        case JsonTokenType.Number:
                        case JsonTokenType.True:
                        case JsonTokenType.False:
                        case JsonTokenType.Null:
                            n += Encoding.UTF8.GetString(reader.ValueSpan).Length;
                            break;
                    }
                }
            }
            return n;
        }

        public static long YamlDotNetEvents(string text)
        {
            var parser = new global::YamlDotNet.Core.Parser(new StringReader(text));
            long n = 0;
            while (parser.MoveNext())
            {
                n++;
                if (parser.Current is global::YamlDotNet.Core.Events.Scalar scalar)
                {
                    n += scalar.Value.Length;
                }
            }
            return n;
        }

        public static long SharpYamlEvents(string text)
        {
            SY.IParser parser = SY.Parser.CreateParser(new StringReader(text));
            long n = 0;
            while (parser.MoveNext())
            {
                n++;
                if (parser.Current is SY.Events.Scalar scalar)
                {
                    n += scalar.Value.Length;
                }
            }
            return n;
        }

        public static long VYamlEvents(ReadOnlyMemory<byte> utf8, bool text)
        {
            var parser = global::VYaml.Parser.YamlParser.FromBytes(System.Runtime.InteropServices.MemoryMarshal.AsMemory(utf8));
            long n = 0;
            while (parser.Read())
            {
                n++;
                if (text && parser.CurrentEventType == global::VYaml.Parser.ParseEventType.Scalar)
                {
                    n += parser.GetScalarAsString()?.Length ?? 0;
                }
            }
            return n;
        }
    }
}
