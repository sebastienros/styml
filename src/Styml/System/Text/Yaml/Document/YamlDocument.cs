// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace System.Text.Yaml
{
    /// <summary>
    ///   Represents the structure of a YAML value in a lightweight, read-only form.
    /// </summary>
    /// <remarks>
    ///   This class utilizes resources from pooled memory to minimize the garbage collector (GC)
    ///   impact in high-usage scenarios. Failure to properly Dispose this object will result in
    ///   the memory not being returned to the pool, which will cause an increase in GC impact across
    ///   various parts of the framework.
    /// </remarks>
    public sealed partial class YamlDocument : IDisposable
    {
        private ReadOnlyMemory<byte> _utf8Yaml;
        private MetadataDb _parsedData;

        // M5c: what the document was parsed with, in one byte, so that the object
        // keeps its size (an object member is a document of its own): the duplicate-key handling (bits 0-1; under FirstWins
        // a property lookup returns the first match, otherwise the last), whether it was set rather than the grammar's
        // default (bit 2, for YamlObject), and the schema (bits 3-4), with which deserializing an element re-reads its raw
        // text, also for a JSON-shaped document that has no YAML tables.
        private byte _parsedWith;

        internal YamlDuplicateKeyHandling ParsedDuplicateKeyHandling
        {
            get => (YamlDuplicateKeyHandling)(_parsedWith & 0b11);
            set => _parsedWith = (byte)((_parsedWith & ~0b11) | (int)value);
        }

        internal YamlSchemaKind ParsedSchema
        {
            get => (YamlSchemaKind)((_parsedWith >> 3) & 0b11);
            set => _parsedWith = (byte)((_parsedWith & ~0b11000) | ((int)value << 3));
        }

        // The rented array or the pooled writer that holds the document's bytes (never both): one field, so that a
        // JSON-shaped document's object is the size it was before M5a's YAML tables reference was added.
        private object? _extraRented;

        internal bool IsDisposable { get; }

        /// <summary>
        ///   The <see cref="YamlElement"/> representing the value of the document.
        /// </summary>
        public YamlElement RootElement => new YamlElement(this, 0);

        private YamlDocument(
            ReadOnlyMemory<byte> utf8Yaml,
            MetadataDb parsedData,
            byte[]? extraRentedArrayPoolBytes = null,
            PooledByteBufferWriter? extraPooledByteBufferWriter = null,
            bool isDisposable = true,
            YamlTables? yaml = null)
        {
            Debug.Assert(!utf8Yaml.IsEmpty);

            // Both rented values better be null if we're not disposable.
            Debug.Assert(isDisposable ||
                (extraRentedArrayPoolBytes is null && extraPooledByteBufferWriter is null));

            // Both rented values can't be specified.
            Debug.Assert(extraRentedArrayPoolBytes is null || extraPooledByteBufferWriter is null);

            _utf8Yaml = utf8Yaml;
            _parsedData = parsedData;
            _extraRented = (object?)extraRentedArrayPoolBytes ?? extraPooledByteBufferWriter;
            IsDisposable = isDisposable;
            _yaml = yaml;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            int length = SourceLength;
            int combinedLength = _utf8Yaml.Length;
            if (combinedLength == 0 || !IsDisposable)
            {
                return;
            }

            _parsedData.Dispose();
            _utf8Yaml = ReadOnlyMemory<byte>.Empty;

            byte[]? combined = _yaml is null ? null : Interlocked.Exchange<byte[]?>(ref _yaml.CombinedRented, null);
            if (combined is not null)
            {
                combined.AsSpan(0, combinedLength).Clear();
                ArrayPool<byte>.Shared.Return(combined);
            }

            switch (Interlocked.Exchange<object?>(ref _extraRented, null))
            {
                case byte[] extraRentedBytes:
                    // When "extra rented bytes exist" it contains the document,
                    // and thus needs to be cleared before being returned.
                    extraRentedBytes.AsSpan(0, length).Clear();
                    ArrayPool<byte>.Shared.Return(extraRentedBytes);
                    break;
                case PooledByteBufferWriter extraBufferWriter:
                    extraBufferWriter.Dispose();
                    break;
            }
        }

        /// <summary>
        ///  Write the document into the provided writer as a YAML value.
        /// </summary>
        /// <remarks>
        ///   A block-style writer keeps the source's styles (each collection's flow or block style, each scalar's and
        ///   key's quoting or block style) where it can hold the text in them, unless
        ///   <see cref="YamlWriterOptions.IgnoreSourceStyles"/> is set, which writes in the writer's own styles.
        /// </remarks>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   This <see cref="RootElement"/>'s <see cref="YamlElement.ValueKind"/> would result in an invalid YAML.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(Utf8YamlWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            RootElement.WriteTo(writer);
        }

        internal YamlTokenType GetYamlTokenType(int index)
        {
            CheckNotDisposed();

            return _parsedData.GetYamlTokenType(index);
        }

        internal bool ValueIsEscaped(int index, bool isPropertyName)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? NameRowOf(index) : index;
            DbRow row = _parsedData.Get(matchIndex);
            Debug.Assert(!isPropertyName || row.TokenType is YamlTokenType.PropertyName);

            return row.HasComplexChildren;
        }

        internal int GetArrayLength(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.StartArray, row.TokenType);

            return row.SizeOrLength;
        }

        internal int GetPropertyCount(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.StartObject, row.TokenType);

            return row.SizeOrLength;
        }

        internal YamlElement GetArrayIndexElement(int currentIndex, int arrayIndex)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(currentIndex);

            CheckExpectedType(YamlTokenType.StartArray, row.TokenType);

            int arrayLength = row.SizeOrLength;

            if ((uint)arrayIndex >= (uint)arrayLength)
            {
                throw new IndexOutOfRangeException();
            }

            if (!row.HasComplexChildren)
            {
                // Since we wouldn't be here without having completed the document parse, and we
                // already vetted the index against the length, this new index will always be
                // within the table.
                return CreateElement(currentIndex + ((arrayIndex + 1) * DbRow.Size));
            }

            int elementCount = 0;
            int objectOffset = currentIndex + DbRow.Size;

            for (; objectOffset < _parsedData.Length; objectOffset += DbRow.Size)
            {
                if (arrayIndex == elementCount)
                {
                    return CreateElement(objectOffset);
                }

                row = _parsedData.Get(objectOffset);

                if (!row.IsSimpleValue)
                {
                    objectOffset += DbRow.Size * row.NumberOfRows;
                }

                elementCount++;
            }

            Debug.Fail(
                $"Ran out of database searching for array index {arrayIndex} from {currentIndex} when length was {arrayLength}");
            throw new IndexOutOfRangeException();
        }

        internal int GetEndIndex(int index, bool includeEndElement)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                return index + DbRow.Size;
            }

            int endIndex = index + DbRow.Size * row.NumberOfRows;

            if (includeEndElement)
            {
                endIndex += DbRow.Size;
            }

            return endIndex;
        }

        internal ReadOnlyMemory<byte> GetRootRawValue()
        {
            return GetRawValue(0, includeQuotes: true);
        }

        internal ReadOnlyMemory<byte> GetRawValue(int index, bool includeQuotes)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                if (includeQuotes && row.HasNodeInfo && _yaml!.TryGet(index, out NodeInfo info) && info.RawLength >= 0)
                {
                    // A decoded scalar or an alias: its source text. Without quotes, the value bytes (ValueSpan).
                    return _utf8Yaml.Slice(info.RawStart, info.RawLength);
                }

                if (includeQuotes && row.TokenType is YamlTokenType.String or YamlTokenType.PropertyName && (row.ScalarFlags & DbRow.PlainFlag) == 0)
                {
                    // Start one character earlier than the value (the open quote)
                    // End one character after the value (the close quote)
                    return _utf8Yaml.Slice(row.Location - 1, row.SizeOrLength + 2);
                }

                return _utf8Yaml.Slice(row.Location, row.SizeOrLength);
            }

            int endElementIdx = GetEndIndex(index, includeEndElement: false);
            int start = row.Location;
            row = _parsedData.Get(endElementIdx);
            return _utf8Yaml.Slice(start, row.Location - start + row.SizeOrLength);
        }

        private ReadOnlyMemory<byte> GetPropertyRawValue(int valueIndex)
        {
            CheckNotDisposed();

            // The property name is stored one row before the value (M5e: or before a collection key's rows)
            int nameRow = NameRowOf(valueIndex);
            DbRow row = _parsedData.Get(nameRow);
            Debug.Assert(row.TokenType == YamlTokenType.PropertyName);

            // Subtract one for the open quote (a YAML name may be plain, or decoded with its own raw range).
            int start = row.HasNodeInfo && _yaml!.TryGet(nameRow, out NodeInfo nameInfo) && nameInfo.RawLength >= 0
                ? nameInfo.RawStart
                : (row.ScalarFlags & DbRow.PlainFlag) != 0 ? row.Location : row.Location - 1;
            int end;

            row = _parsedData.Get(valueIndex);

            if (row.IsSimpleValue)
            {
                if (row.HasNodeInfo && _yaml!.TryGet(valueIndex, out NodeInfo valueInfo) && valueInfo.RawLength >= 0)
                {
                    end = valueInfo.RawStart + valueInfo.RawLength;
                }
                else
                {
                    end = row.Location + row.SizeOrLength;

                    // If the value was a quoted string, pick up the terminating quote.
                    if (row.TokenType == YamlTokenType.String && (row.ScalarFlags & DbRow.PlainFlag) == 0)
                    {
                        end++;
                    }
                }

                return _utf8Yaml.Slice(start, end - start);
            }

            int endElementIdx = GetEndIndex(valueIndex, includeEndElement: false);
            row = _parsedData.Get(endElementIdx);
            end = row.Location + row.SizeOrLength;
            return _utf8Yaml.Slice(start, end - start);
        }

        internal string? GetString(int index, YamlTokenType expectedType)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            YamlTokenType tokenType = row.TokenType;

            if (tokenType == YamlTokenType.Null)
            {
                return null;
            }

            CheckExpectedType(expectedType, tokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return row.HasComplexChildren
                ? YamlReaderHelper.GetUnescapedString(segment)
                : YamlReaderHelper.TranscodeHelper(segment);
        }

        internal unsafe bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
        {
            CheckNotDisposed();

            byte[]? otherUtf8TextArray = null;

            int length = checked(otherText.Length * YamlConstants.MaxExpansionFactorWhileTranscoding);
            Span<byte> otherUtf8Text = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length));

            OperationStatus status = YamlWriterHelper.ToUtf8(otherText, otherUtf8Text, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            bool result;
            if (status == OperationStatus.InvalidData)
            {
                result = false;
            }
            else
            {
                Debug.Assert(status == OperationStatus.Done);
                result = TextEquals(index, otherUtf8Text.Slice(0, written), isPropertyName, shouldUnescape: true);
            }

            if (otherUtf8TextArray is not null)
            {
                otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        internal bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? NameRowOf(index) : index;

            DbRow row = _parsedData.Get(matchIndex);

            CheckExpectedType(
                isPropertyName ? YamlTokenType.PropertyName : YamlTokenType.String,
                row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (otherUtf8Text.Length > segment.Length || (!shouldUnescape && otherUtf8Text.Length != segment.Length))
            {
                return false;
            }

            if (row.HasComplexChildren && shouldUnescape)
            {
                if (otherUtf8Text.Length < segment.Length / YamlConstants.MaxExpansionFactorWhileEscaping)
                {
                    return false;
                }

                int idx = segment.IndexOf(YamlConstants.BackSlash);
                Debug.Assert(idx != -1);

                if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
                {
                    return false;
                }

                return YamlReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
            }

            return segment.SequenceEqual(otherUtf8Text);
        }

        internal string GetNameOfPropertyValue(int index)
        {
            // The property name is one row before the property value (M5e: or before a collection key's rows)
            return GetString(NameRowOf(index), YamlTokenType.PropertyName)!;
        }

        internal ReadOnlySpan<byte> GetPropertyNameRaw(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(NameRowOf(index));
            Debug.Assert(row.TokenType is YamlTokenType.PropertyName);

            return _utf8Yaml.Span.Slice(row.Location, row.SizeOrLength);
        }

        internal bool TryGetValue(int index, [NotNullWhen(true)] out byte[]? value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return YamlReaderHelper.TryGetUnescapedBase64Bytes(segment, out value);
            }

            Debug.Assert(!segment.Contains(YamlConstants.BackSlash));
            return YamlReaderHelper.TryDecodeBase64(segment, out value);
        }

        internal bool TryGetValue(int index, out sbyte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out sbyte v) => r.TryGetSByte(out v), out value);
        }

        internal bool TryGetValue(int index, out byte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out byte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out byte v) => r.TryGetByte(out v), out value);
        }

        internal bool TryGetValue(int index, out short value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out short tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out short v) => r.TryGetInt16(out v), out value);
        }

        internal bool TryGetValue(int index, out ushort value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out ushort tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out ushort v) => r.TryGetUInt16(out v), out value);
        }

        internal bool TryGetValue(int index, out int value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out int tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out int v) => r.TryGetInt32(out v), out value);
        }

        internal bool TryGetValue(int index, out uint value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out uint tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out uint v) => r.TryGetUInt32(out v), out value);
        }

        internal bool TryGetValue(int index, out long value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out long tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out long v) => r.TryGetInt64(out v), out value);
        }

        internal bool TryGetValue(int index, out ulong value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out ulong tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out ulong v) => r.TryGetUInt64(out v), out value);
        }

        internal bool TryGetValue(int index, out double value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out double v) => r.TryGetDouble(out v), out value);
        }

        internal bool TryGetValue(int index, out float value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out float v) => r.TryGetSingle(out v), out value);
        }

        internal bool TryGetValue(int index, out decimal value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (JsonNumberFastPath(segment) && Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // M5a: a YAML number spelling (0x1F, .inf, +1, and the YAML 1.1 forms under that schema) through the
            // reader's own slow path.
            return TryGetYamlNumber(segment, static (ref Utf8YamlReader r, out decimal v) => r.TryGetDecimal(out v), out value);
        }

        internal bool TryGetValue(int index, out DateTime value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return YamlReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
        }

        internal bool TryGetValue(int index, out DateTimeOffset value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return YamlReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
        }

        internal bool TryGetValue(int index, out Guid value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(YamlTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Yaml.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return YamlReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
        }

        private delegate bool YamlNumberGetter<T>(ref Utf8YamlReader reader, out T value);

        /// <summary>The schema the document was read with, for the YAML number forms its getters accept (M5a).</summary>
        internal YamlSchemaKind Schema => ParsedSchema;

        /// <summary><see cref="YamlDocumentOptions.MaxAliasExpansion"/> (0: the default rule).</summary>
        internal int MaxAliasExpansion => _yaml?.MaxAliasExpansion ?? 0;

        // The JSON number parser reads a YAML 1.1 leading-zero octal (012) as decimal: under that schema such a number
        // goes to the reader, as Utf8YamlReader's own getters do.
        private bool JsonNumberFastPath(ReadOnlySpan<byte> segment) =>
            _yaml is not { HasYamlNumbers: true } || ParsedSchema != YamlSchemaKind.Yaml11 || !YamlReaderHelper.IsYaml11LegacyOctal(segment);

        private bool TryGetYamlNumber<T>(ReadOnlySpan<byte> segment, YamlNumberGetter<T> get, out T value)
        {
            value = default!;
            if (segment.IsEmpty || _yaml is not { HasYamlNumbers: true })
            {
                return false;
            }

            var reader = new Utf8YamlReader(segment, new YamlReaderOptions { Schema = Schema });
            return reader.Read() && reader.TokenType == YamlTokenType.Number && get(ref reader, out value);
        }

        internal string GetRawValueAsString(int index)
        {
            ReadOnlyMemory<byte> segment = GetRawValue(index, includeQuotes: true);
            return GetStandAloneRawText(index, segment.Span);
        }

        internal string GetPropertyRawValueAsString(int valueIndex)
        {
            ReadOnlyMemory<byte> segment = GetPropertyRawValue(valueIndex);
            return YamlReaderHelper.TranscodeHelper(segment.Span);
        }

        internal YamlElement CloneElement(int index)
        {
            if (_yaml is not null)
            {
                return CloneYamlElement(index);
            }

            int endIndex = GetEndIndex(index, true);
            MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
            ReadOnlyMemory<byte> segmentCopy = GetRawValue(index, includeQuotes: true).ToArray();

            YamlDocument newDocument =
                new YamlDocument(
                    segmentCopy,
                    newDb,
                    extraRentedArrayPoolBytes: null,
                    extraPooledByteBufferWriter: null,
                    isDisposable: false);
            newDocument.ParsedDuplicateKeyHandling = ParsedDuplicateKeyHandling;
            newDocument.ParsedSchema = ParsedSchema;

            return newDocument.RootElement;
        }

        internal void WriteElementTo(
            int index,
            Utf8YamlWriter writer)
        {
            CheckNotDisposed();

            if (_yaml is not null)
            {
                // Anchors, tags, aliases or decoded scalars (M5a).
                WriteYamlElementTo(index, writer);
                return;
            }

            DbRow row = _parsedData.Get(index);

            switch (row.TokenType)
            {
                case YamlTokenType.StartObject:
                    WriteSourceCollectionStyle(index, writer);
                    writer.WriteStartObject();
                    WriteComplexElement(index, writer);
                    return;
                case YamlTokenType.StartArray:
                    WriteSourceCollectionStyle(index, writer);
                    writer.WriteStartArray();
                    WriteComplexElement(index, writer);
                    return;
                case YamlTokenType.String:
                    WriteString(row, writer);
                    return;
                case YamlTokenType.Number:
                    WriteNumber(index, row, writer);
                    return;
                case YamlTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    return;
                case YamlTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    return;
                case YamlTokenType.Null:
                    writer.WriteNullValue();
                    return;
            }

            Debug.Fail($"Unexpected encounter with YamlTokenType {row.TokenType}");
        }

        // A YAML number spelling (0x1F, .inf) is written by its value (M5a): both writer styles check raw bytes as JSON's.
        private void WriteNumber(int index, in DbRow row, Utf8YamlWriter writer)
        {
            ReadOnlySpan<byte> text = _utf8Yaml.Span.Slice(row.Location, row.SizeOrLength);
            if (_yaml is not { HasYamlNumbers: true } || YamlReaderHelper.IsJsonNumber(text))
            {
                writer.WriteNumberValue(text);
            }
            else
            {
                WriteYamlNumber(index, row, writer);
            }
        }

        private void WriteComplexElement(int index, Utf8YamlWriter writer)
        {
            int endIndex = GetEndIndex(index, true);

            for (int i = index + DbRow.Size; i < endIndex; i += DbRow.Size)
            {
                DbRow row = _parsedData.Get(i);

                // All of the types which don't need the value span
                switch (row.TokenType)
                {
                    case YamlTokenType.String:
                        WriteString(row, writer);
                        continue;
                    case YamlTokenType.Number:
                        WriteNumber(i, row, writer);
                        continue;
                    case YamlTokenType.True:
                        writer.WriteBooleanValue(value: true);
                        continue;
                    case YamlTokenType.False:
                        writer.WriteBooleanValue(value: false);
                        continue;
                    case YamlTokenType.Null:
                        writer.WriteNullValue();
                        continue;
                    case YamlTokenType.StartObject:
                        WriteSourceCollectionStyle(i, writer);
                        writer.WriteStartObject();
                        continue;
                    case YamlTokenType.EndObject:
                        writer.WriteEndObject();
                        continue;
                    case YamlTokenType.StartArray:
                        WriteSourceCollectionStyle(i, writer);
                        writer.WriteStartArray();
                        continue;
                    case YamlTokenType.EndArray:
                        writer.WriteEndArray();
                        continue;
                    case YamlTokenType.PropertyName:
                        WritePropertyName(row, writer);
                        continue;
                }

                Debug.Fail($"Unexpected encounter with YamlTokenType {row.TokenType}");
            }
        }

        private ReadOnlySpan<byte> UnescapeString(in DbRow row, out ArraySegment<byte> rented)
        {
            Debug.Assert(row.TokenType == YamlTokenType.String || row.TokenType == YamlTokenType.PropertyName);
            int loc = row.Location;
            int length = row.SizeOrLength;
            ReadOnlySpan<byte> text = _utf8Yaml.Slice(loc, length).Span;

            if (!row.HasComplexChildren)
            {
                rented = default;
                return text;
            }

            byte[] rent = ArrayPool<byte>.Shared.Rent(length);
            YamlReaderHelper.Unescape(text, rent, out int written);
            rented = new ArraySegment<byte>(rent, 0, written);
            return rented.AsSpan();
        }

        private static void ClearAndReturn(ArraySegment<byte> rented)
        {
            if (rented.Array is not null)
            {
                rented.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(rented.Array);
            }
        }

        internal void WritePropertyName(int index, Utf8YamlWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(NameRowOf(index));
            Debug.Assert(row.TokenType == YamlTokenType.PropertyName);
            WritePropertyName(row, writer);
        }

        private void WritePropertyName(in DbRow row, Utf8YamlWriter writer)
        {
            if (!writer.KeepsSourceStyles)
            {
                // M7c: under IgnoreSourceStyles a document's key is styled as a YamlObject's (DefaultScalarStyle).
                writer.HintDefaultScalarStyleForNextKey();
            }
            else if (row.ScalarStyle is YamlScalarStyle.SingleQuoted or YamlScalarStyle.DoubleQuoted)
            {
                // M5f: a quoted source key is written quoted again, where the writer can.
                writer.HintSourceScalarStyle(row.ScalarStyle);
            }

            ArraySegment<byte> rented = default;

            try
            {
                writer.WritePropertyName(UnescapeString(row, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }

        private void WriteString(in DbRow row, Utf8YamlWriter writer)
        {
            if (row.ScalarStyle == YamlScalarStyle.Plain)
            {
                // M5a: a plain source scalar, read as a string under the document's schema, is written back plain where
                // the syntax allows it (the writer skips its resolution check for it).
                writer.HintPlainFromSource();
            }
            else if (row.ScalarStyle is not YamlScalarStyle.None)
            {
                // M5f: the source's style, single- or double-quoted, literal or folded, where
                // the writer can hold the value in it; its default choice otherwise. The merge rewrite's own hint goes first.
                writer.HintSourceScalarStyle(row.ScalarStyle);
            }

            ArraySegment<byte> rented = default;

            try
            {
                writer.WriteStringValue(UnescapeString(row, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }

        private static void Parse(
            ReadOnlySpan<byte> utf8YamlSpan,
            YamlReaderOptions readerOptions,
            ref MetadataDb database,
            ref StackRowStack stack,
            out YamlTables? tables)
        {
            ParseYaml(utf8YamlSpan, readerOptions, ref database, ref stack, out tables);
        }

        private static void ValidateNoDuplicateProperties(YamlDocument document)
        {
            if (document.RootElement.ValueKind is YamlValueKind.Array or YamlValueKind.Object)
            {
                ValidateDuplicatePropertiesCore(document);
            }
        }

        /// <summary>
        /// The duplicate-key check of <see cref="YamlDuplicateKeyHandling.Error"/> (M5c):
        /// one linear pass over the rows, after the parse has attached the decoded scalars. Each object's own
        /// names are compared once: up to 16 against each other, above that through a hash table rented from the array
        /// pool. Names compare by their decoded text; a YAML-escaped one is unescaped first.
        /// </summary>
        private static void ValidateDuplicatePropertiesCore(YamlDocument document)
        {
            Debug.Assert(document.RootElement.ValueKind is YamlValueKind.Array or YamlValueKind.Object);

            // M7d (item 2): each small object's key locations (the first 16) and lengths (the next 16).
            Span<int> small = stackalloc int[32];
            for (int index = 0; index < document._parsedData.Length; index += DbRow.Size)
            {
                // M7d (item 2, attempt 2): the token type alone, and an object's stored property count, which skips one
                // with fewer than two keys without walking it.
                if (document._parsedData.GetYamlTokenType(index) == YamlTokenType.StartObject
                    && document._parsedData.Get(index).SizeOrLength >= 2)
                {
                    document.ValidateObjectKeys(index, small);
                }
            }
        }

        private const int SmallObjectKeys = 16;

        private void ValidateObjectKeys(int index, Span<int> small)
        {
            int count = 0;
            bool escaped = false;
            bool complexKeys = _yaml is { HasComplexKeys: true };
            for (int i = index + DbRow.Size; _parsedData.GetYamlTokenType(i) != YamlTokenType.EndObject;)
            {
                DbRow key = _parsedData.Get(i);
                escaped |= key.HasComplexChildren;
                if (count < SmallObjectKeys)
                {
                    small[count] = key.Location;
                    small[SmallObjectKeys + count] = key.SizeOrLength;
                }

                count++;
                int value = complexKeys ? ValueRowOf(i) : i + DbRow.Size;
                i = value + SubtreeRows(value) * DbRow.Size;
            }

            if (count < 2)
            {
                return;
            }

            if (escaped || (HasComplexKeys && HasComplexKey(index)))
            {
                // M5e: a collection key is compared by its canonical form, apart from the scalar keys' texts.
                ValidateEscapedObjectKeys(index, count);
                return;
            }

            if (count <= SmallObjectKeys)
            {
                // M7d (item 2): the names from the first pass, compared by length first; the text is read once.
                ReadOnlySpan<byte> text = _utf8Yaml.Span;
                for (int i = 1; i < count; i++)
                {
                    int length = small[SmallObjectKeys + i];
                    ReadOnlySpan<byte> name = text.Slice(small[i], length);
                    for (int j = 0; j < i; j++)
                    {
                        if (small[SmallObjectKeys + j] == length && text.Slice(small[j], length).SequenceEqual(name))
                        {
                            ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(name);
                        }
                    }
                }

                return;
            }

            // Open addressing over key row indices (plus one, so that 0 is empty), with a power-of-two size.
            int size = 32;
            while (size < count * 2)
            {
                size <<= 1;
            }

            int[] table = ArrayPool<int>.Shared.Rent(size);
            try
            {
                Array.Clear(table, 0, size);
                for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
                {
                    ReadOnlySpan<byte> name = KeyName(i);
                    int slot = Marvin.ComputeHash32(name, Marvin.DefaultSeed) & (size - 1);
                    while (table[slot] != 0)
                    {
                        if (KeyName(table[slot] - 1).SequenceEqual(name))
                        {
                            ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(name);
                        }

                        slot = (slot + 1) & (size - 1);
                    }

                    table[slot] = i + 1;
                    int value = ValueRowOf(i);
                    i = value + SubtreeRows(value) * DbRow.Size;
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(table);
            }
        }

        private ReadOnlySpan<byte> KeyName(int keyRow)
        {
            DbRow row = _parsedData.Get(keyRow);
            return _utf8Yaml.Span.Slice(row.Location, row.SizeOrLength);
        }

        // An object with a JSON-escaped name (rare in YAML): the names are unescaped into a set, which allocates. M5e: and an
        // object with a collection key, whose canonical forms go into a set of their own, so a collection key
        // never equals a scalar key whose text is the same.
        private void ValidateEscapedObjectKeys(int index, int count)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string>? complexKeys = null;
            for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
            {
                if (IsComplexKeyRow(i))
                {
                    if (!(complexKeys ??= new HashSet<string>(StringComparer.Ordinal)).Add(GetCanonicalKey(i)))
                    {
                        ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(KeyName(i));
                    }

                    int complexValue = ValueRowOf(i);
                    i = complexValue + SubtreeRows(complexValue) * DbRow.Size;
                    continue;
                }

                DbRow key = _parsedData.Get(i);
                ReadOnlySpan<byte> name = KeyName(i);
                byte[]? unescaped = key.HasComplexChildren ? YamlReaderHelper.GetUnescaped(name) : null;
                if (!names.Add(YamlReaderHelper.TranscodeHelper(unescaped ?? name)))
                {
                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(unescaped ?? name);
                }

                int value = ValueRowOf(i);
                i = value + SubtreeRows(value) * DbRow.Size;
            }
        }

        private void CheckNotDisposed()
        {
            if (_utf8Yaml.IsEmpty)
            {
                ThrowHelper.ThrowObjectDisposedException_YamlDocument();
            }
        }

        private static void CheckExpectedType(YamlTokenType expected, YamlTokenType actual)
        {
            // An alias to an anchored mapping key reaches the key's row (M5a): the key is its string.
            if (expected != actual && !(expected == YamlTokenType.String && actual == YamlTokenType.PropertyName))
            {
                ThrowHelper.ThrowYamlElementWrongTypeException(expected, actual);
            }
        }

        private static void CheckSupportedOptions(
            YamlReaderOptions readerOptions,
            string paramName)
        {
            // Since these are coming from a valid instance of Utf8YamlReader, the YamlReaderOptions must already be valid
            Debug.Assert(readerOptions.CommentHandling >= 0 && readerOptions.CommentHandling <= YamlCommentHandling.Allow);

            if (readerOptions.CommentHandling == YamlCommentHandling.Allow)
            {
                throw new ArgumentException(SR.YamlDocumentDoesNotSupportComments, paramName);
            }
        }
    }
}
