// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for YamlNode-derived types. The {T} value must be Object and not YamlNode
    /// since we allow Object-declared members\variables to deserialize as {YamlNode}.
    /// </summary>
    internal sealed class YamlNodeConverter : YamlConverter<YamlNode?>
    {
        internal override bool IsUntypedConverter => true;

        internal static YamlNodeConverter Instance { get; } = new YamlNodeConverter();

        public override void Write(Utf8YamlWriter writer, YamlNode? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer, options);
            }
        }

        public override YamlNode? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            return ReadAsBuiltNode(ref reader, options);
        }

        /// <summary>
        /// The node read (M5c; since M7a the only one, the lazy path having served only the JSON grammar's default): the
        /// value is parsed into an element with no duplicate check, and its nodes take the handling when they are filled. Under
        /// Error they are filled here, so that a repeated name, compared with the node's comparer, throws System.Text.Json's
        /// ArgumentException at the read.
        /// </summary>
        internal static YamlNode? ReadAsBuiltNode(ref Utf8YamlReader reader, YamlSerializerOptions options)
        {
            YamlElement element = YamlAliasReader.ParseElement(ref reader, YamlDuplicateKeyHandling.LastWins, options);
            YamlDuplicateKeyHandling handling = options.DuplicateKeyHandling;
            element.SetNodeDuplicateKeyHandling(handling);
            YamlNode? node = Create(element, options.GetNodeOptions());
            if (handling == YamlDuplicateKeyHandling.Error)
            {
                Fill(node);
            }

            return node;

            static void Fill(YamlNode? node)
            {
                if (node is YamlObject obj)
                {
                    foreach (KeyValuePair<string, YamlNode?> property in obj)
                    {
                        Fill(property.Value);
                    }
                }
                else if (node is YamlArray array)
                {
                    foreach (YamlNode? item in array)
                    {
                        Fill(item);
                    }
                }
            }
        }

        // The alias-expansion budget of each node tree made from a document, kept against the tree's root node and only
        // for trees that expanded an alias, so that no node grows a field. A tree's budget is its own: two trees over one
        // document, or one long-lived document read again and again, do not share one (M5a).
        private static readonly ConditionalWeakTable<YamlNode, StrongBox<int>> s_aliasBudgets = new();

        /// <summary>
        /// A child node of a node made from a document (M5a): an alias becomes a copy of what it names, so its
        /// materialization is counted against the tree's budget (<see cref="YamlDocumentOptions.MaxAliasExpansion"/>),
        /// refused when recursive, and bounded in depth, counted through the parent chain, which is walked only for an
        /// alias.
        /// </summary>
        internal static YamlNode? CreateChild(YamlElement element, YamlNodeOptions? options, YamlNode parent)
        {
            if (element.IsAlias)
            {
                int depth = 1;
                YamlNode root = parent;
                for (YamlNode? p = parent; p is not null; p = p.Parent)
                {
                    depth++;
                    root = p;
                }

                element.CheckNodeDepth(depth);
                element.CountAliasExpansion(ref s_aliasBudgets.GetValue(root, static _ => new StrongBox<int>()).Value);
            }

            return CreateCore(element, options);
        }

        public static YamlNode? Create(YamlElement element, YamlNodeOptions? options)
        {
            if (!element.IsAlias)
            {
                return CreateCore(element, options);
            }

            // The root of a new tree is itself an alias: the tree's budget starts with that expansion.
            int expanded = 0;
            element.CountAliasExpansion(ref expanded);
            YamlNode? node = CreateCore(element, options);
            if (node is not null)
            {
                s_aliasBudgets.Add(node, new StrongBox<int>(expanded)); // a new node: never a key yet
            }

            return node;
        }

        private static YamlNode? CreateCore(YamlElement element, YamlNodeOptions? options)
        {
            YamlNode? node;

            switch (element.ValueKind)
            {
                case YamlValueKind.Null:
                    node = null;
                    break;
                case YamlValueKind.Object:
                    node = new YamlObject(element, options);
                    break;
                case YamlValueKind.Array:
                    node = new YamlArray(element, options);
                    break;
                default:
                    node = new YamlValueOfElement(element, options);
                    break;
            }

            node?.InitializeProperties(element);
            return node;
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
