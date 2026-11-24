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

        // A number by its value, as YamlElement.DeepEquals compares numbers (NumberNormalForm).
        private string CanonicalNumber(int index) => NumberNormalForm(index);

        /// <summary>
        /// The number at <paramref name="index"/> as text that two numbers share exactly when they are equal: a JSON
        /// spelling's normal form (System.Text.Json's digits comparison, so 1.0 is 1), an integer spelling (decimal with
        /// leading zeros, hex, octal, binary, '_' separators, sexagesimal, under the document's schema) by its exact
        /// decimal digits at any size, a decimal spelling (.5, 1., +1.5, 1_0.5) by the same digits comparison, and the
        /// non-finite values by name.
        /// </summary>
        internal string NumberNormalForm(int index)
        {
            ReadOnlySpan<byte> text = GetRawValue(index, includeQuotes: false).Span;
            if (YamlReaderHelper.IsJsonNumber(text))
            {
                return NormalForm(text);
            }

            string written = YamlReaderHelper.TranscodeHelper(text);
            bool yaml11 = ParsedSchema == YamlSchemaKind.Yaml11;
            bool negative = written.Length > 0 && written[0] == '-';
            string body = written.Length > 0 && written[0] is '-' or '+' ? written.Substring(1) : written;
            if (yaml11)
            {
                body = body.Replace("_", "");
            }

            switch (body.ToLowerInvariant())
            {
                case ".inf":
                    return negative ? "-inf" : "inf";
                case ".nan":
                    return "nan";
            }

            if (IntegerDigits(body, yaml11, index) is string digits)
            {
                return NormalForm(Encoding.UTF8.GetBytes(negative && digits != "0" ? "-" + digits : digits));
            }

            // A decimal spelling, in JSON's form: a leading 0 for .5, a trailing 0 for 1., no leading zeros.
            string decimalForm = body;
            if (decimalForm.StartsWith(".", StringComparison.Ordinal))
            {
                decimalForm = "0" + decimalForm;
            }

            decimalForm = decimalForm.Replace(".e", ".0e").Replace(".E", ".0E");
            if (decimalForm.EndsWith(".", StringComparison.Ordinal))
            {
                decimalForm += "0";
            }

            int integralEnd = decimalForm.IndexOfAny(new[] { '.', 'e', 'E' });
            int zeros = 0;
            while (zeros < (integralEnd < 0 ? decimalForm.Length : integralEnd) - 1 && decimalForm[zeros] == '0')
            {
                zeros++;
            }

            decimalForm = (negative ? "-" : "") + decimalForm.Substring(zeros);
            byte[] utf8 = Encoding.UTF8.GetBytes(decimalForm);
            if (YamlReaderHelper.IsJsonNumber(utf8))
            {
                return NormalForm(utf8);
            }

            // Anything else the schema reads as a number (sexagesimal floats): by its double, or its text.
            return TryGetValue(index, out double d) ? d.ToString("R", CultureInfo.InvariantCulture) : written;
        }

        // The decimal digits of an integer spelling (no sign, no leading zeros), or null when it is not one.
        private string? IntegerDigits(string body, bool yaml11, int index)
        {
            if (body.Length == 0)
            {
                return null;
            }

            if (yaml11 && body.IndexOf(':') > 0)
            {
                // Sexagesimal: its value fits a long wherever it reads as an integer.
                return TryGetValue(index, out long l) ? Math.Abs(l).ToString(CultureInfo.InvariantCulture) : null;
            }

            if (body.Length > 2 && body[0] == '0' && body[1] is 'x' or 'X')
            {
                return Radix(body.Substring(2), 16);
            }

            if (body.Length > 2 && body[0] == '0' && body[1] is 'o' or 'O')
            {
                return Radix(body.Substring(2), 8);
            }

            if (yaml11 && body.Length > 2 && body[0] == '0' && body[1] is 'b' or 'B')
            {
                return Radix(body.Substring(2), 2);
            }

            if (yaml11 && body.Length > 1 && body[0] == '0' && Radix(body.Substring(1), 8) is string octal)
            {
                return octal;
            }

            foreach (char c in body)
            {
                if (c is < '0' or > '9')
                {
                    return null;
                }
            }

            string trimmed = body.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        private static string? Radix(string digits, int radix)
        {
            if (digits.Length == 0)
            {
                return null;
            }

            var value = System.Numerics.BigInteger.Zero;
            int shift = radix switch { 2 => 1, 8 => 3, _ => 4 };
            foreach (char c in digits)
            {
                int digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'f' => c - 'a' + 10,
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => int.MaxValue,
                };
                if (digit >= radix)
                {
                    return null;
                }

                value = (value << shift) | digit;
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalForm(ReadOnlySpan<byte> jsonNumber)
        {
            try
            {
                return YamlHelpers.GetNormalFormText(jsonNumber);
            }
            catch (ArgumentOutOfRangeException)
            {
                // An exponent past int's range: the text stands for itself (the duplicate check runs at parse).
                return YamlReaderHelper.TranscodeHelper(jsonNumber);
            }
        }

        private static void AppendPart(StringBuilder builder, char kind, string text) =>
            builder.Append(kind).Append(text.Length).Append(':').Append(text);
    }
}
