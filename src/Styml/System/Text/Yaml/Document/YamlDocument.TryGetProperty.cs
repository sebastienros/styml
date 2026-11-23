// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        internal unsafe bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out YamlElement value)
        {
            CheckNotDisposed();

            if (ParsedDuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                return TryGetFirstNamedPropertyValue(index, Encoding.UTF8.GetBytes(propertyName.ToString()), out value);
            }

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.StartObject, row.TokenType);

            // Only one row means it was EndObject.
            if (row.NumberOfRows == 1)
            {
                value = default;
                return false;
            }

            int maxBytes = YamlReaderHelper.s_utf8Encoding.GetMaxByteCount(propertyName.Length);
            int startIndex = index + DbRow.Size;
            int endIndex = checked(row.NumberOfRows * DbRow.Size + index);

            if (maxBytes < YamlConstants.StackallocByteThreshold)
            {
                Span<byte> utf8Name = stackalloc byte[YamlConstants.StackallocByteThreshold];
                int len = YamlReaderHelper.GetUtf8FromText(propertyName, utf8Name);
                utf8Name = utf8Name.Slice(0, len);

                return TryGetNamedPropertyValue(
                    startIndex,
                    endIndex,
                    utf8Name,
                    out value);
            }

            // Unescaping the property name will make the string shorter (or the same)
            // So the first viable candidate is one whose length in bytes matches, or
            // exceeds, our length in chars.
            //
            // The maximal escaping seems to be 6 -> 1 ("\u0030" => "0"), but just transcode
            // and switch once one viable long property is found.

            int minBytes = propertyName.Length;
            // Move to the row before the EndObject
            int candidateIndex = endIndex - DbRow.Size;

            while (candidateIndex > index)
            {
                int passedIndex = candidateIndex;

                row = _parsedData.Get(candidateIndex);
                Debug.Assert(row.TokenType != YamlTokenType.PropertyName);

                // Move before the value
                if (row.IsSimpleValue)
                {
                    candidateIndex -= DbRow.Size;
                }
                else
                {
                    Debug.Assert(row.NumberOfRows > 0);
                    candidateIndex -= DbRow.Size * (row.NumberOfRows + 1);
                }

                // M5e: past a collection key's rows to its name (NameRowOf).
                candidateIndex = NameRowOf(candidateIndex + DbRow.Size);
                row = _parsedData.Get(candidateIndex);
                Debug.Assert(row.TokenType == YamlTokenType.PropertyName);

                if (row.SizeOrLength >= minBytes)
                {
                    byte[] tmpUtf8 = ArrayPool<byte>.Shared.Rent(maxBytes);
                    Span<byte> utf8Name = default;

                    try
                    {
                        int len = YamlReaderHelper.GetUtf8FromText(propertyName, tmpUtf8);
                        utf8Name = tmpUtf8.AsSpan(0, len);

                        return TryGetNamedPropertyValue(
                            startIndex,
                            passedIndex + DbRow.Size,
                            utf8Name,
                            out value);
                    }
                    finally
                    {
                        // While property names aren't usually a secret, they also usually
                        // aren't long enough to end up in the rented buffer transcode path.
                        //
                        // On the basis that this is user data, go ahead and clear it.
                        utf8Name.Clear();
                        ArrayPool<byte>.Shared.Return(tmpUtf8);
                    }
                }

                // Move to the previous value
                candidateIndex -= DbRow.Size;
            }

            // None of the property names were within the range that the UTF-8 encoding would have been.
            value = default;
            return false;
        }

        internal bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out YamlElement value)
        {
            CheckNotDisposed();

            if (ParsedDuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                return TryGetFirstNamedPropertyValue(index, propertyName, out value);
            }

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.StartObject, row.TokenType);

            // Only one row means it was EndObject.
            if (row.NumberOfRows == 1)
            {
                value = default;
                return false;
            }

            int endIndex = checked(row.NumberOfRows * DbRow.Size + index);

            return TryGetNamedPropertyValue(
                index + DbRow.Size,
                endIndex,
                propertyName,
                out value);
        }

        private unsafe bool TryGetNamedPropertyValue(
            int startIndex,
            int endIndex,
            ReadOnlySpan<byte> propertyName,
            out YamlElement value)
        {
            ReadOnlySpan<byte> documentSpan = _utf8Yaml.Span;
            Span<byte> utf8UnescapedStack = stackalloc byte[YamlConstants.StackallocByteThreshold];

            // Move to the row before the EndObject
            int index = endIndex - DbRow.Size;

            // M5e (review 4): a collection key matches by its raw text only when no scalar key of the mapping does.
            int complexMatch = -1;

            while (index > startIndex)
            {
                DbRow row = _parsedData.Get(index);
                Debug.Assert(row.TokenType != YamlTokenType.PropertyName);

                // Move before the value
                if (row.IsSimpleValue)
                {
                    index -= DbRow.Size;
                }
                else
                {
                    Debug.Assert(row.NumberOfRows > 0);
                    index -= DbRow.Size * (row.NumberOfRows + 1);
                }

                // M5e: past a collection key's rows to its name (NameRowOf).
                int valueStart = index + DbRow.Size;
                index = NameRowOf(valueStart);
                row = _parsedData.Get(index);
                Debug.Assert(row.TokenType == YamlTokenType.PropertyName);

                ReadOnlySpan<byte> currentPropertyName = documentSpan.Slice(row.Location, row.SizeOrLength);

                if ((row.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
                {
                    if (complexMatch < 0 && currentPropertyName.SequenceEqual(propertyName))
                    {
                        complexMatch = valueStart;
                    }
                }
                else if (row.HasComplexChildren)
                {
                    // An escaped property name will be longer than an unescaped candidate, so only unescape
                    // when the lengths are compatible.
                    if (currentPropertyName.Length > propertyName.Length)
                    {
                        int idx = currentPropertyName.IndexOf(YamlConstants.BackSlash);
                        Debug.Assert(idx >= 0);

                        // If everything up to where the property name has a backslash matches, keep going.
                        if (propertyName.Length > idx &&
                            currentPropertyName.Slice(0, idx).SequenceEqual(propertyName.Slice(0, idx)))
                        {
                            int remaining = currentPropertyName.Length - idx;
                            int written = 0;
                            byte[]? rented = null;

                            try
                            {
                                Span<byte> utf8Unescaped = remaining <= utf8UnescapedStack.Length ?
                                    utf8UnescapedStack :
                                    (rented = ArrayPool<byte>.Shared.Rent(remaining));

                                // Only unescape the part we haven't processed.
                                YamlReaderHelper.Unescape(currentPropertyName.Slice(idx), utf8Unescaped, 0, out written);

                                // If the unescaped remainder matches the input remainder, it's a match.
                                if (utf8Unescaped.Slice(0, written).SequenceEqual(propertyName.Slice(idx)))
                                {
                                    // If the property name is a match, the answer is the next element.
                                    value = CreateElement(valueStart);
                                    return true;
                                }
                            }
                            finally
                            {
                                if (rented is not null)
                                {
                                    rented.AsSpan(0, written).Clear();
                                    ArrayPool<byte>.Shared.Return(rented);
                                }
                            }
                        }
                    }
                }
                else if (currentPropertyName.SequenceEqual(propertyName))
                {
                    // If the property name is a match, the answer is the next element.
                    value = CreateElement(valueStart);
                    return true;
                }

                // Move to the previous value
                index -= DbRow.Size;
            }

            if (complexMatch >= 0)
            {
                value = CreateElement(complexMatch);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>FirstWins (M5c): a forward search, which returns the first of repeated names.</summary>
        private bool TryGetFirstNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out YamlElement value)
        {
            DbRow start = _parsedData.Get(index);
            CheckExpectedType(YamlTokenType.StartObject, start.TokenType);
            ReadOnlySpan<byte> documentSpan = _utf8Yaml.Span;
            int complexMatch = -1;
            for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
            {
                DbRow key = _parsedData.Get(i);
                Debug.Assert(key.TokenType == YamlTokenType.PropertyName);
                ReadOnlySpan<byte> name = documentSpan.Slice(key.Location, key.SizeOrLength);
                int valueIndex = ValueRowOf(i);
                if ((key.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
                {
                    // M5e (review 4): a collection key matches only when no scalar key does.
                    if (complexMatch < 0 && name.SequenceEqual(propertyName))
                    {
                        complexMatch = valueIndex;
                    }
                }
                else if (key.HasComplexChildren ? YamlReaderHelper.GetUnescaped(name).AsSpan().SequenceEqual(propertyName) : name.SequenceEqual(propertyName))
                {
                    value = CreateElement(valueIndex);
                    return true;
                }

                i = valueIndex + SubtreeRows(valueIndex) * DbRow.Size;
            }

            if (complexMatch >= 0)
            {
                value = CreateElement(complexMatch);
                return true;
            }

            value = default;
            return false;
        }
    }
}
