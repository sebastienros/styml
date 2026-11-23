// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // ------------------------------------------------------------------------------------------------------------
        // M5e: collection keys compare structurally. The canonical form of a
        // node is its kind, its expanded tag and its content: a scalar's decoded text (a number's value), a sequence's
        // items in order, a mapping's entries sorted (YAML mappings are unordered). Every part is length-prefixed, so two forms are equal
        // exactly when the nodes are. It is computed only where it is needed (the Error check, the merge rewrite's index,
        // the serializer's object-key set, deep equality), never at parse. An alias is followed to its node's content:
        // each expansion counts against the alias bound, the nesting against TraversalMaxDepth, and a recursive alias is
        // refused, so a key cannot be an expansion bomb.

        /// <summary>Whether the mapping at <paramref name="objectIndex"/> has a collection key.</summary>
        internal bool HasComplexKey(int objectIndex)
        {
            Debug.Assert(_parsedData.Get(objectIndex).TokenType == YamlTokenType.StartObject);
            for (int i = objectIndex + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
            {
                if (IsComplexKeyRow(i))
                {
                    return true;
                }

                int value = ValueRowOf(i);
                i = value + SubtreeRows(value) * DbRow.Size;
            }

            return false;
        }

        /// <summary>The kind of the key of the property whose value is at <paramref name="valueRow"/>.</summary>
        internal YamlKeyKind GetKeyKind(int valueRow)
        {
            CheckNotDisposed();
            int nameRow = NameRowOf(valueRow);
            DbRow row = _parsedData.Get(nameRow);
            if ((row.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
            {
                return _parsedData.Get(nameRow + DbRow.Size).TokenType == YamlTokenType.Alias ? YamlKeyKind.Alias : YamlKeyKind.Complex;
            }

            return row.HasNodeInfo && _yaml!.TryGet(nameRow, out NodeInfo info) && info.AliasKeyTarget > 0 ? YamlKeyKind.Alias : YamlKeyKind.Scalar;
        }

        /// <summary>
        /// The key node of the property whose value is at <paramref name="valueRow"/>: a collection key's node, an
        /// alias key's anchored node, or a scalar key as a String element over its text.
        /// </summary>
        internal YamlElement GetKeyElement(int valueRow)
        {
            CheckNotDisposed();
            int nameRow = NameRowOf(valueRow);
            DbRow row = _parsedData.Get(nameRow);
            if ((row.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
            {
                return CreateElement(nameRow + DbRow.Size);
            }

            if (row.HasNodeInfo && _yaml!.TryGet(nameRow, out NodeInfo info) && info.AliasKeyTarget > 0)
            {
                return CreateElement(info.AliasKeyTarget - 1);
            }

            return CreateElement(nameRow);
        }

        /// <summary>The canonical form of the collection key whose name row is <paramref name="nameRow"/>.</summary>
        internal string GetCanonicalKey(int nameRow)
        {
            Debug.Assert(IsComplexKeyRow(nameRow));
            int expanded = 0;
            return GetCanonicalForm(nameRow + DbRow.Size, ref expanded);
        }

        /// <summary>The canonical form of the node at <paramref name="index"/>; <paramref name="expanded"/> is the operation's alias budget.</summary>
        internal string GetCanonicalForm(int index, ref int expanded)
        {
            var builder = new StringBuilder();
            AppendCanonical(builder, index, depth: 0, ref expanded);
            return builder.ToString();
        }

        private void AppendCanonical(StringBuilder builder, int index, int depth, ref int expanded)
        {
            CheckTraversalDepth(depth);
            DbRow row = _parsedData.Get(index);
            if (row.TokenType == YamlTokenType.Alias)
            {
                CountAliasExpansion(index, ref expanded);
                AppendCanonical(builder, row.SizeOrLength, depth + 1, ref expanded);
                return;
            }

            if (row.HasNodeInfo && GetTag(index, expanded: true) is string tag)
            {
                AppendPart(builder, '!', tag);
            }

            switch (row.TokenType)
            {
                case YamlTokenType.StartArray:
                    builder.Append('[');
                    for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndArray; i += SubtreeRows(i) * DbRow.Size)
                    {
                        AppendCanonical(builder, i, depth + 1, ref expanded);
                    }

                    builder.Append(']');
                    return;

                case YamlTokenType.StartObject:
                {
                    var entries = new List<string>();
                    for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
                    {
                        var entry = new StringBuilder();
                        if (IsComplexKeyRow(i))
                        {
                            entry.Append('?');
                            AppendCanonical(entry, i + DbRow.Size, depth + 1, ref expanded);
                        }
                        else
                        {
                            AppendPart(entry, 'k', GetString(i, YamlTokenType.PropertyName) ?? "");
                        }

                        int value = ValueRowOf(i);
                        entry.Append('=');
                        AppendCanonical(entry, value, depth + 1, ref expanded);
                        entries.Add(entry.ToString());
                        i = value + SubtreeRows(value) * DbRow.Size;
                    }

                    entries.Sort(StringComparer.Ordinal);
                    builder.Append('{');
                    foreach (string entry in entries)
                    {
                        AppendPart(builder, 'e', entry);
                    }

                    builder.Append('}');
                    return;
                }

                case YamlTokenType.String:
                case YamlTokenType.PropertyName:
                    AppendPart(builder, 's', GetString(index, row.TokenType) ?? "");
                    return;

                case YamlTokenType.Number:
                    AppendPart(builder, 'n', CanonicalNumber(index));
                    return;

                default:
                    // Booleans and nulls: their kind and their text (the DOM keeps a scalar's text, 5.3 C10).
                    char kind = row.TokenType switch
                    {
                        YamlTokenType.True => 't',
                        YamlTokenType.False => 'f',
                        _ => 'z',
                    };
                    AppendPart(builder, kind, YamlReaderHelper.TranscodeHelper(GetRawValue(index, includeQuotes: false).Span));
                    return;
            }
        }

        // A number by its value, as YamlElement.DeepEquals compares numbers: a JSON spelling by its exact decimal value
        // (1.0 is 1), any other spelling (0x1F, 012 under YAML 1.1, .inf) by its integer value, or else its double.
        private string CanonicalNumber(int index)
        {
            ReadOnlySpan<byte> text = GetRawValue(index, includeQuotes: false).Span;
            if (YamlReaderHelper.IsJsonNumber(text))
            {
                try
                {
                    return YamlHelpers.GetNormalFormText(text);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // An exponent past int's range: the duplicate check runs at parse, so the text stands for it.
                    return YamlReaderHelper.TranscodeHelper(text);
                }
            }

            string? value = null;
            if (TryGetValue(index, out long l))
            {
                value = l.ToString(CultureInfo.InvariantCulture);
            }
            else if (TryGetValue(index, out ulong ul))
            {
                value = ul.ToString(CultureInfo.InvariantCulture);
            }
            else if (TryGetValue(index, out double d))
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    return d.ToString(CultureInfo.InvariantCulture);
                }

                value = d.ToString("R", CultureInfo.InvariantCulture);
            }

            // A number no getter reads (one past double's range) keeps its text.
            return value is null ? YamlReaderHelper.TranscodeHelper(text) : YamlHelpers.GetNormalFormText(Encoding.UTF8.GetBytes(value));
        }

        private static void AppendPart(StringBuilder builder, char kind, string text) =>
            builder.Append(kind).Append(text.Length).Append(':').Append(text);
    }
}
