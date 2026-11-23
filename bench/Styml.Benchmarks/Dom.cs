// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M6b's DOM rows: each library's DOM parse, and the walks of the setup check.
// Every Styml and System.Text.Json document is disposed inside the measured method; the text-input libraries read a
// string decoded in setup (M6a's rule).
using System;
using System.Collections.Generic;
using System.IO;
using YR = YamlDotNet.RepresentationModel;
using SM = SharpYaml.Model;

namespace Styml.Benchmarks
{
    /// <summary>Scalars (keys included), collections and aliases met by a walk.</summary>
    internal record struct DomCounts(long Scalars, long Collections, long Aliases)
    {
        public override string ToString() => $"{Scalars:N0} scalars, {Collections:N0} collections, {Aliases:N0} aliases";
    }

    internal static class Dom
    {
        public static readonly System.Text.Yaml.YamlDocumentOptions YamlOptions = new() { MaxDepth = 256 };
        public static readonly System.Text.Json.JsonDocumentOptions JsonOptions = new() { MaxDepth = 256 };
        private static readonly SharpYaml.Syntax.YamlSyntaxOptions s_syntaxOptions = new();

        // -------------------------------------------------------------------------------------------------------------
        // The measured parses. Each parses, then reads the root's count; the lazy DOMs (YamlNode, JsonNode) walk the tree.

        public static long StymlDocument(ReadOnlyMemory<byte> utf8)
        {
            using System.Text.Yaml.YamlDocument document = System.Text.Yaml.YamlDocument.Parse(utf8, YamlOptions);
            return document.RootElement.GetPropertyCountOrLength();
        }

        public static long StymlNode(ReadOnlySpan<byte> utf8)
        {
            System.Text.Yaml.Nodes.YamlNode? node = System.Text.Yaml.Nodes.YamlNode.Parse(utf8, documentOptions: YamlOptions);
            DomCounts counts = default;
            Walk(node, ref counts);
            return counts.Scalars + counts.Collections;
        }

        public static long YamlDotNet(string text)
        {
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(text));
            return stream.Documents[0].RootNode switch
            {
                YR.YamlMappingNode mapping => mapping.Children.Count,
                YR.YamlSequenceNode sequence => sequence.Children.Count,
                _ => 0,
            };
        }

        public static long SharpYaml(string text)
        {
            SM.YamlStream stream = SM.YamlStream.Load(new StringReader(text));
            return stream[0].Contents switch
            {
                SM.YamlMapping mapping => mapping.Count,
                SM.YamlSequence sequence => sequence.Count,
                _ => 0,
            };
        }

        // The lossless syntax tree: trivia kept, so different work from the other DOMs; it has no public children, so the
        // row returns its token count.
        public static long SharpYamlSyntax(string text) => global::SharpYaml.Syntax.YamlSyntaxTree.Parse(text, s_syntaxOptions).Tokens.Count;

        public static long Stj(ReadOnlyMemory<byte> utf8)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(utf8, JsonOptions);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object ? document.RootElement.GetPropertyCount() : document.RootElement.GetArrayLength();
        }

        public static long StjNode(ReadOnlySpan<byte> utf8)
        {
            System.Text.Json.Nodes.JsonNode? node = System.Text.Json.Nodes.JsonNode.Parse(utf8, documentOptions: JsonOptions);
            DomCounts counts = default;
            Walk(node, ref counts);
            return counts.Scalars + counts.Collections;
        }

        // -------------------------------------------------------------------------------------------------------------
        // The walks. 'expand': descend into an alias as into its anchored node; otherwise count it once.

        public static DomCounts CountStyml(byte[] utf8, bool expand)
        {
            using System.Text.Yaml.YamlDocument document = System.Text.Yaml.YamlDocument.Parse(utf8, YamlOptions);
            DomCounts counts = default;
            Walk(document.RootElement, expand, ref counts);
            return counts;
        }

        public static DomCounts CountStymlNode(byte[] utf8)
        {
            DomCounts counts = default;
            Walk(System.Text.Yaml.Nodes.YamlNode.Parse(utf8, documentOptions: YamlOptions), ref counts);
            return counts;
        }

        public static DomCounts CountYamlDotNet(string text)
        {
            var stream = new YR.YamlStream();
            stream.Load(new StringReader(text));
            DomCounts counts = default;
            Walk(stream.Documents[0].RootNode, new HashSet<YR.YamlNode>(ReferenceEqualityComparer.Instance), ref counts);
            return counts;
        }

        public static DomCounts CountSharpYaml(string text)
        {
            DomCounts counts = default;
            Walk(SM.YamlStream.Load(new StringReader(text))[0].Contents, ref counts);
            return counts;
        }

        public static DomCounts CountStj(byte[] utf8)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(utf8, JsonOptions);
            DomCounts counts = default;
            Walk(document.RootElement, ref counts);
            return counts;
        }

        public static DomCounts CountStjNode(byte[] utf8)
        {
            DomCounts counts = default;
            Walk(System.Text.Json.Nodes.JsonNode.Parse(utf8, documentOptions: JsonOptions), ref counts);
            return counts;
        }

        private static void Walk(System.Text.Yaml.YamlElement element, bool expand, ref DomCounts counts)
        {
            if (!expand && element.IsAlias)
            {
                counts.Aliases++;
                return;
            }
            switch (element.ValueKind)
            {
                case System.Text.Yaml.YamlValueKind.Object:
                    counts.Collections++;
                    foreach (System.Text.Yaml.YamlProperty property in element.EnumerateObject())
                    {
                        counts.Scalars++;
                        Walk(property.Value, expand, ref counts);
                    }
                    break;
                case System.Text.Yaml.YamlValueKind.Array:
                    counts.Collections++;
                    foreach (System.Text.Yaml.YamlElement item in element.EnumerateArray())
                    {
                        Walk(item, expand, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }

        private static void Walk(System.Text.Yaml.Nodes.YamlNode? node, ref DomCounts counts)
        {
            switch (node)
            {
                case System.Text.Yaml.Nodes.YamlObject mapping:
                    counts.Collections++;
                    foreach (KeyValuePair<string, System.Text.Yaml.Nodes.YamlNode?> property in mapping)
                    {
                        counts.Scalars++;
                        Walk(property.Value, ref counts);
                    }
                    break;
                case System.Text.Yaml.Nodes.YamlArray sequence:
                    counts.Collections++;
                    foreach (System.Text.Yaml.Nodes.YamlNode? item in sequence)
                    {
                        Walk(item, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }

        // YamlDotNet resolves an alias on load to the anchored node's instance: a node met again is the alias.
        private static void Walk(YR.YamlNode node, HashSet<YR.YamlNode> seen, ref DomCounts counts)
        {
            if (!seen.Add(node))
            {
                counts.Aliases++;
                return;
            }
            switch (node)
            {
                case YR.YamlMappingNode mapping:
                    counts.Collections++;
                    foreach (KeyValuePair<YR.YamlNode, YR.YamlNode> pair in mapping.Children)
                    {
                        Walk(pair.Key, seen, ref counts);
                        Walk(pair.Value, seen, ref counts);
                    }
                    break;
                case YR.YamlSequenceNode sequence:
                    counts.Collections++;
                    foreach (YR.YamlNode item in sequence.Children)
                    {
                        Walk(item, seen, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }

        // SharpYaml's model expands an alias on load into a copy.
        private static void Walk(SM.YamlElement element, ref DomCounts counts)
        {
            switch (element)
            {
                case SM.YamlMapping mapping:
                    counts.Collections++;
                    foreach (KeyValuePair<SM.YamlElement, SM.YamlElement> pair in mapping)
                    {
                        Walk(pair.Key, ref counts);
                        Walk(pair.Value, ref counts);
                    }
                    break;
                case SM.YamlSequence sequence:
                    counts.Collections++;
                    foreach (SM.YamlElement item in sequence)
                    {
                        Walk(item, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }

        private static void Walk(System.Text.Json.JsonElement element, ref DomCounts counts)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    counts.Collections++;
                    foreach (System.Text.Json.JsonProperty property in element.EnumerateObject())
                    {
                        counts.Scalars++;
                        Walk(property.Value, ref counts);
                    }
                    break;
                case System.Text.Json.JsonValueKind.Array:
                    counts.Collections++;
                    foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
                    {
                        Walk(item, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }

        private static void Walk(System.Text.Json.Nodes.JsonNode? node, ref DomCounts counts)
        {
            switch (node)
            {
                case System.Text.Json.Nodes.JsonObject mapping:
                    counts.Collections++;
                    foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> property in mapping)
                    {
                        counts.Scalars++;
                        Walk(property.Value, ref counts);
                    }
                    break;
                case System.Text.Json.Nodes.JsonArray sequence:
                    counts.Collections++;
                    foreach (System.Text.Json.Nodes.JsonNode? item in sequence)
                    {
                        Walk(item, ref counts);
                    }
                    break;
                default:
                    counts.Scalars++;
                    break;
            }
        }
    }
}
