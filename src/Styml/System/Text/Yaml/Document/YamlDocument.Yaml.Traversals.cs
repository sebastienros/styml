// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // M5a, the traversals that follow alias rows (and the table the
        // design accepts). An alias is never copied while it is navigated or compared; a traversal that copies
        // what an alias names (writing apart from its anchor, cloning, YamlNode, deserializing) counts the rows it adds
        // against MaxAliasExpansion and refuses a recursive alias. Every recursive traversal is bounded by the document's
        // MaxDepth, counted across alias boundaries, so a chain of anchors (&a1 [x], &a2 [*a1], ...) throws a
        // YamlException rather than recursing without bound.

        internal bool HasAliases => _yaml is { HasAliases: true };

        /// <summary>M5e: a mapping of this document has a collection key, whose rows follow its name row.</summary>
        internal bool HasComplexKeys => _yaml is { HasComplexKeys: true };

        /// <summary>
        /// M5e (review 7): the one step from a property's name row to its value's first row. A collection key's
        /// rows sit between them; without collection keys in the document this is the next row, as before M5e.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ValueRowOf(int nameRow)
        {
            if (_yaml is { HasComplexKeys: true } && IsComplexKeyRow(nameRow))
            {
                return GetEndIndex(nameRow + DbRow.Size, includeEndElement: true);
            }

            return nameRow + DbRow.Size;
        }

        /// <summary>
        /// M5e (review 7): the one step from a property value's first row back to its name row. The row before a
        /// collection key's value is the key's last row, whose row count leads back to the key's first row.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int NameRowOf(int valueRow)
        {
            int previous = valueRow - DbRow.Size;
            if (_yaml is { HasComplexKeys: true })
            {
                DbRow row = _parsedData.Get(previous);
                if (row.TokenType != YamlTokenType.PropertyName)
                {
                    // A collection key's end row, or an alias key's one alias row.
                    return row.TokenType == YamlTokenType.Alias ? previous - DbRow.Size : previous - DbRow.Size * (row.NumberOfRows + 1);
                }
            }

            return previous;
        }

        /// <summary>The name row of a collection key (M5e), whose key rows follow it.</summary>
        internal bool IsComplexKeyRow(int nameRow)
        {
            DbRow row = _parsedData.Get(nameRow);
            return row.TokenType == YamlTokenType.PropertyName && (row.ScalarFlags & DbRow.ComplexKeyFlag) != 0;
        }

        /// <summary>The payload is a decoded YAML string, not YAML text (a document made for one scalar by ParseValue).</summary>
        private bool PayloadIsValue => _yaml is { PayloadIsValue: true };

        /// <summary>The depth bound of the traversals: the MaxDepth the document was read with.</summary>
        internal int TraversalMaxDepth => _yaml?.TraversalMaxDepth ?? YamlDocumentOptions.DefaultMaxDepth;

        /// <summary>The length of the source text; the decoded scalars of a YAML document follow it.</summary>
        private int SourceLength => _yaml is { SourceLength: >= 0 } tables ? tables.SourceLength : _utf8Yaml.Length;

        internal int AliasExpansionLimit =>
            MaxAliasExpansion > 0 ? MaxAliasExpansion : Math.Max(10_000, 10 * (_parsedData.Length / DbRow.Size));

        /// <summary>The rows of the node at <paramref name="index"/>, its end row included.</summary>
        internal int SubtreeRows(int index)
        {
            DbRow row = _parsedData.Get(index);
            return row.IsSimpleValue ? 1 : row.NumberOfRows + 1;
        }

        internal string GetAliasName(int aliasIndex) =>
            _yaml is not null && _yaml.TryGet(aliasIndex, out NodeInfo info) && info.Anchor is not null ? info.Anchor : "";

        /// <summary>
        /// Counts what expanding the alias at <paramref name="aliasIndex"/> adds to <paramref name="expanded"/>, or
        /// throws. The budget is one operation's, not the document's: each WriteTo, Clone and Deserialize starts from 0,
        /// and so does each node tree made from the document, so the bound stops one operation from amplifying and a
        /// long-lived document never runs out.
        /// </summary>
        internal void CountAliasExpansion(int aliasIndex, ref int expanded)
        {
            if (IsRecursiveAlias(aliasIndex))
            {
                ThrowHelper.ThrowYamlException_RecursiveAliasExpansion(GetAliasName(aliasIndex));
            }

            int added = SubtreeRows(_parsedData.Get(aliasIndex).SizeOrLength);
            int limit = AliasExpansionLimit;
            if (Interlocked.Add(ref expanded, added) > limit)
            {
                ThrowHelper.ThrowYamlException_AliasExpansionLimit(limit);
            }
        }

        internal void CheckTraversalDepth(int depth)
        {
            if (depth > TraversalMaxDepth)
            {
                ThrowHelper.ThrowYamlException_AliasTraversalTooDeep(TraversalMaxDepth);
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // WriteTo (row 3): a document with YAML tables writes anchors, tags and aliases as read. An alias is written as
        // an alias when its anchored node is inside what is written and was written before it, and the writer is in block
        // style; otherwise the alias is expanded (counted, recursion refused). Nesting, expansions included, is bounded by
        // TraversalMaxDepth; the writer's own MaxDepth also applies.

        private struct YamlWriteState
        {
            internal int RangeStart;
            internal int RangeEnd;
            internal int Depth;
            internal int Expanded; // the operation's alias-expansion budget used so far
            internal bool ResolveMerges; // M5b: mappings with a plain '<<' key are written merged (YamlDocument.Merge.cs)
            internal bool SuppressAnchors; // M5b: a merged copy of an aliased mapping's entries writes no anchors
            internal YamlDuplicateKeyHandling MergeSourceHandling; // M5c: a key repeated inside one merge source
        }

        private void WriteYamlElementTo(int index, Utf8YamlWriter writer)
        {
            var state = new YamlWriteState { RangeStart = index, RangeEnd = GetEndIndex(index, includeEndElement: true) };
            WriteYamlNode(index, writer, ref state);
        }

        // Writes the node at index and returns the row after it.
        private int WriteYamlNode(int index, Utf8YamlWriter writer, ref YamlWriteState state)
        {
            DbRow row = _parsedData.Get(index);
            bool block = writer.IsBlockStyle;

            if (row.TokenType == YamlTokenType.Alias)
            {
                int target = row.SizeOrLength;
                if (block && target >= state.RangeStart && target < index)
                {
                    writer.WriteAliasValue(GetAliasName(index));
                    return index + DbRow.Size;
                }

                CountAliasExpansion(index, ref state.Expanded);
                CheckTraversalDepth(++state.Depth);
                WriteYamlNode(target, writer, ref state);
                state.Depth--;
                return index + DbRow.Size;
            }

            if (block && row.HasNodeInfo && _yaml!.TryGet(index, out NodeInfo info))
            {
                if ((info.DocumentLine & Utf8YamlReader.OnDocumentLine) != 0 && !writer.Options.IgnoreSourceStyles)
                {
                    // A document root whose properties were on its '---' line: there again, right after WriteDocumentStart.
                    writer.KeepRootPropertiesOnMarkerLine();
                }

                // M5d: the tag in the form that reads back without the source's %TAG directives (the same text for a local, a
                // verbatim or a !! tag). The source's order of the two properties is kept.
                WriteNodeProperties(writer, info.Anchor is not null && !state.SuppressAnchors ? info.Anchor : null,
                    info.Tag is null ? null : WritableTag(writer, info.Tag, info.ExpandedTag), info.TagFirst);

                if ((info.DocumentLine & Utf8YamlReader.UnityStripped) != 0)
                {
                    // Unity's stripped marker goes only on a document's '---' line, after the root's tag.
                    writer.TryWriteUnityStrippedMarker();
                }
            }

            switch (row.TokenType)
            {
                case YamlTokenType.StartObject:
                case YamlTokenType.StartArray:
                    {
                        bool isObject = row.TokenType == YamlTokenType.StartObject;
                        if (isObject && state.ResolveMerges && HasMergeKey(index))
                        {
                            return WriteMergedObject(index, writer, ref state);
                        }

                        WriteSourceCollectionStyle(index, writer);
                        if (isObject)
                        {
                            writer.WriteStartObject();
                        }
                        else
                        {
                            writer.WriteStartArray();
                        }

                        CheckTraversalDepth(++state.Depth);
                        int i = index + DbRow.Size;
                        while (true)
                        {
                            DbRow child = _parsedData.Get(i);
                            if (child.TokenType is YamlTokenType.EndObject or YamlTokenType.EndArray)
                            {
                                break;
                            }

                            if (child.TokenType == YamlTokenType.PropertyName && (child.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
                            {
                                // M5e: a collection key, written as an explicit key (block style only).
                                if (!block)
                                {
                                    ThrowHelper.ThrowNotSupportedException_YamlComplexKeyJsonCompatible();
                                }

                                writer.WriteStartPropertyKey();
                                i = WriteYamlNode(i + DbRow.Size, writer, ref state);
                                writer.WriteEndPropertyKey();
                            }
                            else if (child.TokenType == YamlTokenType.PropertyName)
                            {
                                if (block && child.HasNodeInfo && _yaml!.TryGet(i, out NodeInfo keyInfo))
                                {
                                    WriteNodeProperties(writer, keyInfo.Anchor is not null && !state.SuppressAnchors ? keyInfo.Anchor : null,
                                        keyInfo.Tag is null ? null : WritableTag(writer, keyInfo.Tag, keyInfo.ExpandedTag), keyInfo.TagFirst);
                                }

                                WritePropertyName(child, writer);
                                i = ValueRowOf(i);
                            }

                            i = WriteYamlNode(i, writer, ref state);
                        }

                        state.Depth--;
                        if (isObject)
                        {
                            writer.WriteEndObject();
                        }
                        else
                        {
                            writer.WriteEndArray();
                        }

                        return i + DbRow.Size;
                    }

                case YamlTokenType.String:
                    if (state.ResolveMerges && block && row.ScalarStyle != YamlScalarStyle.Plain)
                    {
                        // M5c: a quoted or block string stays quoted, so that the rewrite reads as the source under every
                        // schema (the writer's own choice of plain is Core-safe, and under Json a plain string is an error).
                        writer.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                    }

                    WriteString(row, writer);
                    break;
                case YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null when state.ResolveMerges && block && row.SizeOrLength > 0:
                    // M5b, the merge rewrite: the source spelling (0x1F, True, ~, NULL), so that a member reads what it
                    // would have read from the source.
                    writer.WriteStringValueBlockPlain(_utf8Yaml.Span.Slice(row.Location, row.SizeOrLength));
                    break;
                case YamlTokenType.Number:
                    WriteYamlNumber(index, row, writer);
                    break;
                case YamlTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    break;
                case YamlTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    break;
                default:
                    Debug.Assert(row.TokenType == YamlTokenType.Null);
                    WriteNull(index, row, writer);
                    break;
            }

            return index + DbRow.Size;
        }

        // A node's anchor and tag, in the source's order (tag first when it was so written), unless IgnoreSourceStyles.
        private static void WriteNodeProperties(Utf8YamlWriter writer, string? anchor, string? tag, bool tagFirst)
        {
            if (tagFirst && tag is not null && !writer.Options.IgnoreSourceStyles)
            {
                writer.WriteTag(tag);
                if (anchor is not null)
                {
                    writer.WriteAnchor(anchor);
                }

                return;
            }

            if (anchor is not null)
            {
                writer.WriteAnchor(anchor);
            }

            if (tag is not null)
            {
                writer.WriteTag(tag);
            }
        }

        // A YAML number spelling (0x1F, .inf, +1) is plain YAML for a block writer; a JSON-compatible writer gets its value.
        private void WriteYamlNumber(int index, in DbRow row, Utf8YamlWriter writer)
        {
            ReadOnlySpan<byte> text = _utf8Yaml.Span.Slice(row.Location, row.SizeOrLength);
            // Both writer styles validate raw number bytes as JSON's; a block writer writes the value's YAML form (.nan).
            if (_yaml is not { HasYamlNumbers: true } || YamlReaderHelper.IsJsonNumber(text))
            {
                writer.WriteNumberValue(text);
            }
            else if (writer.WritesYamlText && !writer.Options.IgnoreSourceStyles && ReadsAsTheSameNumber(index, text, writer.Options.ReadSchema))
            {
                // Written as it was read (0x1F, 012, 1_000), as JsonElement.WriteTo keeps a number's text: the output reads
                // to the same number, and a hash or a blob keeps its digits.
                writer.WriteYamlNumberText(text);
            }
            else if (TryGetValue(index, out long l))
            {
                writer.WriteNumberValue(l);
            }
            else if (TryGetValue(index, out ulong ul))
            {
                writer.WriteNumberValue(ul);
            }
            else if (TryGetValue(index, out double d))
            {
                writer.WriteNumberValue(d);
            }
            else
            {
                // Not a number in any spelling (bytes changed after the parse): the writer's own check reports it.
                writer.WriteNumberValue(text);
            }
        }

        // Whether `text`, the number at `index`, reads to the same number under `schema` as under the document's own.
        private bool ReadsAsTheSameNumber(int index, ReadOnlySpan<byte> text, YamlSchemaKind schema)
        {
            if (schema == ParsedSchema)
            {
                return true;
            }

            try
            {
                var reader = new Utf8YamlReader(text, new YamlReaderOptions { Schema = schema });
                if (!reader.Read() || reader.TokenType != YamlTokenType.Number)
                {
                    return false;
                }

                if (TryGetValue(index, out long l))
                {
                    return reader.TryGetInt64(out long other) && other == l;
                }

                if (TryGetValue(index, out ulong ul))
                {
                    return reader.TryGetUInt64(out ulong other) && other == ul;
                }

                return TryGetValue(index, out double d) && reader.TryGetDouble(out double otherDouble) && otherDouble.Equals(d);
            }
            catch (YamlException)
            {
                // The JSON schema refuses the text.
                return false;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Clone (row 5): the clone keeps its rows, scalar flags, decoded scalars, node-table entries and inside aliases,
        // re-indexed to the copy. An alias whose anchored node is outside the element is expanded: the element is written
        // (block style, as WriteTo, counted and bounded) and the copy is parsed.

        private YamlElement CloneYamlElement(int index)
        {
            int endIndex = GetEndIndex(index, includeEndElement: true);
            for (int i = index; i < endIndex; i += DbRow.Size)
            {
                DbRow r = _parsedData.Get(i);
                if (r.TokenType == YamlTokenType.Alias && (r.SizeOrLength < index || r.SizeOrLength >= endIndex))
                {
                    return CloneByWriting(index);
                }
            }

            // The source range: from the element's first byte to its last, decoded rows aside.
            ReadOnlyMemory<byte> raw = GetRawValue(index, includeQuotes: true);
            int sourceStart = SourceOffsetOf(raw);
            int sourceEnd = sourceStart + raw.Length;

            // Rows that point into the source are shifted; decoded rows get their bytes copied after the source copy.
            int rowsLength = endIndex - index;
            byte[] rows = new byte[rowsLength];
            _parsedData.CopyRawRows(index, rows);
            Span<int> ints = MemoryMarshal.Cast<byte, int>(rows.AsSpan());
            var decoded = new List<byte>();
            YamlTables? tables = null;
            for (int i = 0; i < rowsLength; i += DbRow.Size)
            {
                DbRow r = _parsedData.Get(index + i);
                int locationSlot = i / sizeof(int);
                int sign = ints[locationSlot] & int.MinValue;
                if (r.TokenType == YamlTokenType.Alias)
                {
                    ints[locationSlot] = sign | (r.Location - sourceStart);
                    ints[locationSlot + 1] = r.SizeOrLength - index;
                }
                else if (r.IsSimpleValue && (r.ScalarFlags & DbRow.DecodedFlag) != 0)
                {
                    int newLocation = (sourceEnd - sourceStart) + decoded.Count;
                    decoded.AddRange(_utf8Yaml.Span.Slice(r.Location, r.SizeOrLength).ToArray());
                    ints[locationSlot] = sign | newLocation;
                }
                else
                {
                    ints[locationSlot] = sign | (r.Location - sourceStart);
                }

                if (r.HasNodeInfo && _yaml!.TryGet(index + i, out NodeInfo info))
                {
                    if (info.RawLength >= 0)
                    {
                        info.RawStart -= sourceStart;
                    }

                    info.Row = i;
                    (tables ??= new YamlTables()).Add(info);
                }
            }

            tables ??= new YamlTables();
            tables.HasAliases = HasAliases && ContainsAlias(index, endIndex);
            tables.HasComplexKeys = HasComplexKeys; // M5e: the rows' own flags tell which names are collection keys
            tables.HasYamlNumbers = _yaml!.HasYamlNumbers;
            tables.Schema = _yaml.Schema;
            tables.MaxAliasExpansion = _yaml.MaxAliasExpansion;
            tables.TraversalMaxDepth = _yaml.TraversalMaxDepth;

            byte[] payload = new byte[(sourceEnd - sourceStart) + decoded.Count];
            _utf8Yaml.Span.Slice(sourceStart, sourceEnd - sourceStart).CopyTo(payload);
            decoded.CopyTo(payload, sourceEnd - sourceStart);
            if (payload.Length == 0)
            {
                payload = new byte[] { (byte)' ' };
            }

            tables.SourceLength = sourceEnd - sourceStart;
            tables.RootTagOutsideSource = tables.InfoCount > 0 && tables.Infos[0].Row == 0 && tables.Infos[0].Tag is not null;
            var copy = new YamlDocument(payload, new MetadataDb(rows), isDisposable: false, yaml: tables);
            copy.ParsedDuplicateKeyHandling = ParsedDuplicateKeyHandling;
            copy.ParsedSchema = ParsedSchema;

            return copy.RootElement;
        }

        private bool ContainsAlias(int index, int endIndex)
        {
            for (int i = index; i < endIndex; i += DbRow.Size)
            {
                if (_parsedData.Get(i).TokenType == YamlTokenType.Alias)
                {
                    return true;
                }
            }

            return false;
        }

        private int SourceOffsetOf(ReadOnlyMemory<byte> part) =>
            part.IsEmpty ? 0 : (int)Runtime.CompilerServices.Unsafe.ByteOffset(
                ref MemoryMarshal.GetReference(_utf8Yaml.Span), ref MemoryMarshal.GetReference(part.Span));

        private YamlElement CloneByWriting(int index)
        {
            byte[] written = WriteYamlNodeToBytes(index);
            using YamlDocument parsed = Parse(written, new YamlDocumentOptions { MaxDepth = TraversalMaxDepth, Schema = Schema, MaxAliasExpansion = MaxAliasExpansion });
            return parsed.RootElement.Clone();
        }

        /// <summary>The node written in block style, aliases outside it expanded (counted, bounded).</summary>
        internal byte[] WriteYamlNodeToBytes(int index)
        {
            var buffer = new ArrayBufferWriterOfByte();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = Math.Max(TraversalMaxDepth * 2, 64), SkipValidation = true, ReadSchema = Schema }))
            {
                WriteYamlElementTo(index, writer);
            }

            return buffer.ToArray();
        }

        private sealed class ArrayBufferWriterOfByte : Buffers.IBufferWriter<byte>
        {
            private byte[] _buffer = new byte[256];
            private int _count;

            public void Advance(int count) => _count += count;

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                Ensure(sizeHint);
                return _buffer.AsMemory(_count);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                Ensure(sizeHint);
                return _buffer.AsSpan(_count);
            }

            public byte[] ToArray() => _buffer.AsSpan(0, _count).ToArray();

            private void Ensure(int sizeHint)
            {
                int needed = _count + Math.Max(sizeHint, 1);
                if (needed > _buffer.Length)
                {
                    Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Deserialize(YamlElement) and Deserialize(YamlDocument) (row 9): the bytes the serializer reads. The root reads the
        // document's whole source. A nested element reads its source bytes when they stand alone (a flow collection, or a
        // scalar other than a block scalar with an indentation indicator) and no alias in it names a node outside it;
        // otherwise it is written in block style (aliases outside it expanded, counted, bounded) and that is read.

#if DEBUG
        internal static int s_deserializedFromSource;
        internal static int s_deserializedByWriting;
#endif

        internal ReadOnlyMemory<byte> GetDeserializationValue(int index)
        {
            CheckNotDisposed();
            if (index == 0 && !PayloadIsValue)
            {
                CountDeserializationPath(fromSource: true);
                ReadOnlyMemory<byte> source = _utf8Yaml.Slice(0, SourceLength);
                return _yaml is { RootTagOutsideSource: true } ? WithTagInFront(0, source) : source;
            }

            ReadOnlyMemory<byte> raw = GetRawValue(index, includeQuotes: true);
            DbRow row = _parsedData.Get(index);
            bool standAlone = row.IsSimpleValue
                ? !(row.ScalarStyle is YamlScalarStyle.Literal or YamlScalarStyle.Folded && HasIndentationIndicator(raw.Span))
                : _parsedData.Get(GetEndIndex(index, includeEndElement: false)).SizeOrLength != 0;
            if (standAlone && !HasAliasOutside(index))
            {
                CountDeserializationPath(fromSource: true);

                // The source bytes start at the node, after its tag (M5d, 5.4: a re-read sees the tag).
                return WithTagInFront(index, raw);
            }

            CountDeserializationPath(fromSource: false);
            return WriteYamlNodeToBytes(index);
        }

        private ReadOnlyMemory<byte> WithTagInFront(int index, ReadOnlyMemory<byte> bytes)
        {
            if (!_parsedData.Get(index).HasNodeInfo || !TryGetNodeInfo(index, out NodeInfo info) || info.Tag is null)
            {
                return bytes;
            }

            DbRow row = _parsedData.Get(index);
            bool block = !row.IsSimpleValue && _parsedData.Get(GetEndIndex(index, includeEndElement: false)).SizeOrLength == 0;
            byte[] tag = Encoding.UTF8.GetBytes(StandAloneTag(info));
            byte[] result = new byte[tag.Length + 1 + bytes.Length];
            tag.CopyTo(result, 0);
            result[tag.Length] = block ? (byte)'\n' : (byte)' ';
            bytes.Span.CopyTo(result.AsSpan(tag.Length + 1));
            return result;
        }

        [Conditional("DEBUG")]
        private static void CountDeserializationPath(bool fromSource)
        {
#if DEBUG
            if (fromSource)
            {
                Interlocked.Increment(ref s_deserializedFromSource);
            }
            else
            {
                Interlocked.Increment(ref s_deserializedByWriting);
            }
#endif
        }

        private static bool HasIndentationIndicator(ReadOnlySpan<byte> raw)
        {
            for (int i = 1; i < raw.Length && raw[i] is not ((byte)'\n' or (byte)' ' or (byte)'#'); i++)
            {
                if (raw[i] is >= (byte)'1' and <= (byte)'9')
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAliasOutside(int index)
        {
            if (!HasAliases)
            {
                return false;
            }

            int endIndex = GetEndIndex(index, includeEndElement: true);
            for (int i = index; i < endIndex; i += DbRow.Size)
            {
                DbRow r = _parsedData.Get(i);
                if (r.TokenType == YamlTokenType.Alias && r.SizeOrLength < index)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------------------------------------------------
        // GetRawText (the string) of a nested block collection: the source lines with the node's indentation removed from
        // the continuation lines, so that YamlDocument.Parse reads the text back.
        // GetRawValue and YamlMarshal.GetRawUtf8Value stay the source bytes.

        private string GetStandAloneRawText(int index, ReadOnlySpan<byte> raw)
        {
            // A tag can decide the value's kind (! 12 is a string), so the stand-alone text keeps it in front.
            string text = GetStandAloneRawTextCore(index, raw);
            if (TryGetNodeInfo(index, out NodeInfo tagInfo) && tagInfo.Tag is string tag)
            {
                DbRow tagged = _parsedData.Get(index);
                bool block = !tagged.IsSimpleValue && _parsedData.Get(GetEndIndex(index, includeEndElement: false)).SizeOrLength == 0
                    && !tagInfo.FlowPair;
                return tag + (block ? "\n" : " ") + text;
            }

            return text;
        }

        private string GetStandAloneRawTextCore(int index, ReadOnlySpan<byte> raw)
        {
            DbRow row = _parsedData.Get(index);
            if (row.IsSimpleValue)
            {
                // A block scalar's indentation indicator counts from its parent's indentation, which the text alone does
                // not have: the content lines move so that the indicator gives the same content column at the root.
                if (row.ScalarStyle is YamlScalarStyle.Literal or YamlScalarStyle.Folded && HasIndentationIndicator(raw)
                    && TryGetNodeInfo(index, out NodeInfo scalarInfo) && scalarInfo.BlockIndent > 0)
                {
                    int indicator = IndicatorDigit(raw);
                    return Dedent(raw, scalarInfo.BlockIndent - indicator, keepFirstLine: true);
                }

                return YamlReaderHelper.TranscodeHelper(raw);
            }

            if (row.TokenType == YamlTokenType.StartObject && TryGetNodeInfo(index, out NodeInfo pairInfo) && pairInfo.FlowPair)
            {
                // '[a: b]': the pair's text is a flow fragment; as a flow mapping it stands alone.
                return "{" + YamlReaderHelper.TranscodeHelper(raw) + "}";
            }

            if (raw.IndexOf((byte)'\n') < 0)
            {
                return YamlReaderHelper.TranscodeHelper(raw);
            }

            // A block collection's end row has no bytes; a flow collection ends with its bracket and reads as it is.
            DbRow end = _parsedData.Get(GetEndIndex(index, includeEndElement: false));
            if (end.SizeOrLength != 0)
            {
                return YamlReaderHelper.TranscodeHelper(raw);
            }

            int start = row.Location;
            int column = 0;
            ReadOnlySpan<byte> source = _utf8Yaml.Span;
            while (start - column > 0 && source[start - column - 1] != (byte)'\n')
            {
                column++;
            }

            return column == 0 ? YamlReaderHelper.TranscodeHelper(raw) : Dedent(raw, column, keepFirstLine: true);
        }

        private static int IndicatorDigit(ReadOnlySpan<byte> raw)
        {
            for (int i = 1; i < raw.Length && raw[i] is not ((byte)'\n' or (byte)' ' or (byte)'#'); i++)
            {
                if (raw[i] is >= (byte)'1' and <= (byte)'9')
                {
                    return raw[i] - '0';
                }
            }

            return 0;
        }

        // Removes up to `column` leading spaces from every line but the first.
        private static string Dedent(ReadOnlySpan<byte> raw, int column, bool keepFirstLine)
        {
            Debug.Assert(keepFirstLine);
            if (column <= 0)
            {
                return YamlReaderHelper.TranscodeHelper(raw);
            }

            var result = new List<byte>(raw.Length);
            int i = 0;
            while (i < raw.Length)
            {
                int lineEnd = raw.Slice(i).IndexOf((byte)'\n');
                int next = lineEnd < 0 ? raw.Length : i + lineEnd + 1;
                int skip = 0;
                if (i > 0)
                {
                    while (skip < column && i + skip < next && raw[i + skip] == (byte)' ')
                    {
                        skip++;
                    }
                }

                for (int k = i + skip; k < next; k++)
                {
                    result.Add(raw[k]);
                }

                i = next;
            }

            return YamlReaderHelper.TranscodeHelper(result.ToArray());
        }
    }
}
