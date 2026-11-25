// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // M5a: YamlDocument reads YAML. JSON-shaped input gets the rows the
        // JSON grammar gave it (deleted in M7a), which are System.Text.Json's; YAML adds, only where a document has them:
        // - scalar flags in a simple row's row-count bits (DbRow.PlainFlag, DecodedFlag, RecursiveAliasFlag);
        // - decoded scalars (folded, single-quoted with '', block scalars, YAML-only escapes), decoded once at parse by
        //   YamlReaderHelper.ProcessYamlScalar, the reader's own processing, and stored after the source bytes;
        // - a node table, sorted by row: anchors, tags and the raw source range of rows whose raw text the row does not
        //   give (decoded scalars), flagged by DbRow.HasNodeInfo;
        // - alias rows (YamlTokenType.Alias), whose SizeOrLength is the anchored node's row.

        /// <summary>An entry of the node table.</summary>
        internal struct NodeInfo
        {
            internal int Row;
            internal int RawStart;
            internal int RawLength; // -1: the row gives its own raw range (a collection, or a scalar with properties only)
            internal string? Anchor;
            internal string? Tag;
            internal string? ExpandedTag;

            /// <summary>A decoded block scalar's content column (for its stand-alone raw text), or 0.</summary>
            internal int BlockIndent;

            /// <summary>A single-pair mapping in a flow sequence (<c>[a: b]</c>), whose text is a flow fragment.</summary>
            internal bool FlowPair;

            /// <summary>M5e: on an alias key to a scalar (a copy of the scalar's row), the anchored row plus one; 0 otherwise.</summary>
            internal int AliasKeyTarget;
        }

        internal sealed class YamlTables
        {
            internal NodeInfo[] Infos = Array.Empty<NodeInfo>();
            internal int InfoCount;
            internal byte[]? Decoded;
            internal int DecodedLength;
            internal bool HasAliases;

            /// <summary>M5e: a mapping has a collection key, whose rows follow its name row.</summary>
            internal bool HasComplexKeys;

            /// <summary>A number was read in a spelling YAML does not have (0x1F, .inf, +1), so writers convert it.</summary>
            internal bool HasYamlNumbers;

            // The document's YAML-only state lives here, so that a JSON-shaped document (no tables) allocates exactly as
            // before M5a: the schema of its YAML number forms, the traversal bounds, the combined copy of source and
            // decoded scalars, and whether the payload is one decoded value.
            internal YamlSchemaKind Schema;
            internal int MaxAliasExpansion;
            internal int TraversalMaxDepth = YamlDocumentOptions.DefaultMaxDepth;
            internal byte[]? CombinedRented;
            internal int SourceLength = -1;
            internal bool PayloadIsValue;

            /// <summary>
            /// M5d (review addition 4): the root's tag is in the node table but not in the source (a value copied from
            /// a reader, or a clone, starts at the node's first byte), so a re-read puts it back in front.
            /// </summary>
            internal bool RootTagOutsideSource;

            /// <summary>The comments, sorted by row, when the document was read with CommentHandling.Allow (YamlDocument.Comments.cs).</summary>
            internal CommentEntry[]? Comments;

            internal void Add(in NodeInfo info)
            {
                Debug.Assert(InfoCount == 0 || Infos[InfoCount - 1].Row < info.Row);
                if (InfoCount == Infos.Length)
                {
                    Array.Resize(ref Infos, Math.Max(8, InfoCount * 2));
                }

                Infos[InfoCount++] = info;
            }

            internal Span<byte> ReserveDecoded(int maxLength)
            {
                int needed = checked(DecodedLength + maxLength);
                if (Decoded is null || Decoded.Length < needed)
                {
                    byte[] grown = new byte[Math.Max(needed, Math.Max(256, (Decoded?.Length ?? 0) * 2))];
                    Decoded?.AsSpan(0, DecodedLength).CopyTo(grown);
                    Decoded = grown;
                }

                return Decoded.AsSpan(DecodedLength, maxLength);
            }

            internal bool TryGet(int row, out NodeInfo info)
            {
                int lo = 0;
                int hi = InfoCount - 1;
                while (lo <= hi)
                {
                    int mid = (int)((uint)(lo + hi) >> 1);
                    int r = Infos[mid].Row;
                    if (r == row)
                    {
                        info = Infos[mid];
                        return true;
                    }

                    if (r < row)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }

                info = default;
                return false;
            }
        }

        /// <summary>The YAML tables, or null for a document whose rows are all JSON-shaped.</summary>
        private YamlTables? _yaml;


        /// <summary>The document has rows that only the YAML tables explain (node table entries, decoded scalars, aliases).</summary>
        internal bool HasYamlTables => _yaml is not null;

        internal bool TryGetNodeInfo(int index, out NodeInfo info)
        {
            if (_yaml is not null && _parsedData.Get(index).HasNodeInfo)
            {
                return _yaml.TryGet(index, out info);
            }

            info = default;
            return false;
        }

        /// <summary>The element at a row, through an alias row to the node it names.</summary>
        internal YamlElement CreateElement(int index)
        {
            DbRow row = _parsedData.Get(index);
            return row.TokenType == YamlTokenType.Alias
                ? new YamlElement(this, row.SizeOrLength, index)
                : new YamlElement(this, index);
        }

        internal string? GetAnchor(int index)
        {
            CheckNotDisposed();
            return TryGetNodeInfo(index, out NodeInfo info) ? info.Anchor : null;
        }

        internal string? GetTag(int index, bool expanded)
        {
            CheckNotDisposed();
            return TryGetNodeInfo(index, out NodeInfo info) ? (expanded ? info.ExpandedTag : info.Tag) : null;
        }

        /// <summary>
        /// M7b: whether the collection at
        /// <paramref name="index"/> was a flow collection. Its end row says so: a flow collection's end holds its bracket
        /// (one byte), a block collection's end has no text. So has the end of a single-pair mapping inside a flow sequence
        /// (<c>[a: b]</c>), which is flow too: the full loop records it (<see cref="NodeInfo.FlowPair"/>, which the
        /// YAML-shaped loop hands over), and a clone keeps the record.
        /// </summary>
        internal bool IsFlowCollection(int index)
        {
            CheckNotDisposed();
            Debug.Assert(_parsedData.Get(index).TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray);
            return _parsedData.Get(GetEndIndex(index, includeEndElement: false)).SizeOrLength > 0
                || (_parsedData.Get(index).TokenType == YamlTokenType.StartObject && TryGetNodeInfo(index, out NodeInfo info) && info.FlowPair);
        }

        /// <summary>M7b: before a collection's start, a block-style writer is asked for the collection's source style.</summary>
        internal void WriteSourceCollectionStyle(int index, Utf8YamlWriter writer)
        {
            if (writer.KeepsSourceStyles && IsFlowCollection(index))
            {
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
            }
        }

        internal YamlScalarStyle GetScalarStyle(int index)
        {
            CheckNotDisposed();
            DbRow row = _parsedData.Get(index);
            if (!row.IsSimpleValue)
            {
                return YamlScalarStyle.None;
            }

            YamlScalarStyle style = row.ScalarStyle;
            if (style != YamlScalarStyle.None)
            {
                return style;
            }

            // A row with no recorded style: its strings are double-quoted and its other scalars plain.
            return row.TokenType is YamlTokenType.String or YamlTokenType.PropertyName ? YamlScalarStyle.DoubleQuoted : YamlScalarStyle.Plain;
        }

        /// <summary>
        /// The serializer reads aliases in M5b (decision D5); until then it refuses a document with an alias or an alias key
        /// when it reads one into object, YamlElement, YamlDocument or a node type, as before M5a. The DOM's own APIs
        /// (YamlDocument.Parse, YamlElement.ParseValue, YamlNode.Parse) read them since M5a.
        /// </summary>
        internal void ThrowIfAliasesForSerializer()
        {
            if (_yaml is { HasAliases: true })
            {
                ThrowHelper.ThrowNotSupportedException_YamlAlias();
            }
        }

        internal bool IsRecursiveAlias(int aliasIndex)
        {
            DbRow row = _parsedData.Get(aliasIndex);
            return row.TokenType == YamlTokenType.Alias && (row.ScalarFlags & DbRow.RecursiveAliasFlag) != 0;
        }

        /// <summary>
        /// Appends the decoded scalars after the source, so that every row's location is an offset into one buffer.
        /// Rented when the document is disposable (returned in Dispose), allocated otherwise.
        /// </summary>
        private static ReadOnlyMemory<byte> AttachDecoded(ReadOnlyMemory<byte> utf8Yaml, YamlTables? tables, bool rent, out byte[]? combinedRented)
        {
            combinedRented = null;
            if (tables is null || tables.DecodedLength == 0)
            {
                return utf8Yaml;
            }

            tables.SourceLength = utf8Yaml.Length;

            int total = checked(utf8Yaml.Length + tables.DecodedLength);
            byte[] combined = rent ? ArrayPool<byte>.Shared.Rent(total) : new byte[total];
            utf8Yaml.Span.CopyTo(combined);
            tables.Decoded.AsSpan(0, tables.DecodedLength).CopyTo(combined.AsSpan(utf8Yaml.Length));
            tables.Decoded = null;
            if (rent)
            {
                combinedRented = combined;
                tables.CombinedRented = combined;
            }

            return combined.AsMemory(0, total);
        }

        /// <summary>The counters the JSON-shaped loop hands to the full loop at the first YAML-only token.</summary>
        private struct YamlLoopState
        {
            internal bool InArray;
            internal int ArrayItemsOrPropertyCount;
            internal int NumberOfRowsForMembers;
            internal int NumberOfRowsForValues;
            internal int LastEnd;
        }

        // The JSON-shaped loop: the tokens JSON can spell (no anchor, tag or alias, no flow pair, no complex or alias key,
        // nothing to decode), with the full loop's rows, in a loop small enough for the JIT to inline the reader's
        // Read into it, as it did into the JSON grammar's loop before M5a. At the first other
        // token it hands its counters to ParseYamlFull, which goes on from that token to the end.
        private static void ParseYaml(
            ReadOnlySpan<byte> utf8YamlSpan,
            YamlReaderOptions readerOptions,
            ref MetadataDb database,
            ref StackRowStack stack,
            out YamlTables? tables)
        {
            tables = null;
            bool inArray = false;
            int arrayItemsOrPropertyCount = 0;
            int numberOfRowsForMembers = 0;
            int numberOfRowsForValues = 0;
            int lastEnd = 0;

            var reader = new Utf8YamlReader(utf8YamlSpan, isFinalBlock: true, new YamlReaderState(options: readerOptions));

            if (readerOptions.CommentHandling == YamlCommentHandling.Allow && !readerOptions.YamlCommentsOnly)
            {
                // Comments are kept: the full loop reads every token, so that this loop stays as it is for the others.
                if (reader.Read())
                {
                    var state = default(YamlLoopState);
                    Dictionary<string, int>? anchors = null;
                    var comments = new CommentCollector();
                    ParseYamlFull(utf8YamlSpan, ref reader, ref database, ref stack, ref tables, ref state, ref anchors, baseOffset: 0, comments: comments);
                    comments.Complete(ref tables, database.Length);
                }

                ThrowIfNoYamlValue(ref reader, database.Length);
                database.CompleteAllocations();
                return;
            }

            while (reader.Read())
            {
                YamlTokenType tokenType = reader.TokenType;
                if (tokenType == YamlTokenType.Comment)
                {
                    continue;
                }

                if (!reader.Anchor.IsEmpty || !reader.Tag.IsEmpty || tokenType == YamlTokenType.Alias
                    || (tokenType == YamlTokenType.StartObject && reader.IsFlow && reader.ValueSpan.IsEmpty)
                    || (tokenType == YamlTokenType.PropertyName && reader.KeyKind is YamlKeyKind.Complex or YamlKeyKind.Alias)
                    || (tokenType is >= YamlTokenType.PropertyName and <= YamlTokenType.Null && reader.YamlNeedsProcessing))
                {
                    var state = new YamlLoopState
                    {
                        InArray = inArray,
                        ArrayItemsOrPropertyCount = arrayItemsOrPropertyCount,
                        NumberOfRowsForMembers = numberOfRowsForMembers,
                        NumberOfRowsForValues = numberOfRowsForValues,
                        LastEnd = lastEnd,
                    };
                    Dictionary<string, int>? anchors = null;
                    ParseYamlFull(utf8YamlSpan, ref reader, ref database, ref stack, ref tables, ref state, ref anchors, baseOffset: 0, comments: null);
                    break;
                }

                Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
                int tokenStart = (int)reader.TokenStartIndex;

                if (tokenType == YamlTokenType.StartObject)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForValues++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize, scalarFlags: 0, hasNodeInfo: false);
                    stack.Push(new StackRow(arrayItemsOrPropertyCount, numberOfRowsForMembers + 1));
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForMembers = 0;
                    lastEnd = tokenStart + reader.ValueSpan.Length;
                }
                else if (tokenType == YamlTokenType.EndObject)
                {
                    int rowIndexOfStart = database.FindIndexOfFirstUnsetSizeOrLength(YamlTokenType.StartObject);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndexOfStart, arrayItemsOrPropertyCount);

                    int newRowIndex = database.Length;
                    AppendEnd(ref database, ref reader, tokenType, tokenStart, ref lastEnd);
                    database.SetNumberOfRows(rowIndexOfStart, numberOfRowsForMembers);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForMembers += row.NumberOfRows;
                }
                else if (tokenType == YamlTokenType.StartArray)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForMembers++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize, scalarFlags: 0, hasNodeInfo: false);
                    stack.Push(new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1));
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForValues = 0;
                    lastEnd = tokenStart + reader.ValueSpan.Length;
                }
                else if (tokenType == YamlTokenType.EndArray)
                {
                    int rowIndexOfStart = database.FindIndexOfFirstUnsetSizeOrLength(YamlTokenType.StartArray);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndexOfStart, arrayItemsOrPropertyCount);
                    database.SetNumberOfRows(rowIndexOfStart, numberOfRowsForValues);
                    if (arrayItemsOrPropertyCount + 1 != numberOfRowsForValues)
                    {
                        database.SetHasComplexChildren(rowIndexOfStart);
                    }

                    int newRowIndex = database.Length;
                    AppendEnd(ref database, ref reader, tokenType, tokenStart, ref lastEnd);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForValues += row.NumberOfRows;
                }
                else
                {
                    Debug.Assert(tokenType is >= YamlTokenType.PropertyName and <= YamlTokenType.Null);
                    Debug.Assert(tokenType != YamlTokenType.PropertyName || !inArray);
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    if (inArray || tokenType == YamlTokenType.PropertyName)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    AppendSimpleScalar(utf8YamlSpan, ref database, ref reader, ref tables, tokenType, tokenStart, ref lastEnd);
                }

                inArray = reader.IsInArray;
            }

            ThrowIfNoYamlValue(ref reader, database.Length);
            database.CompleteAllocations();
        }

        // The full loop, from the reader's current token (the first one the JSON-shaped loop does not take) to the end.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ParseYamlFull(
            ReadOnlySpan<byte> utf8YamlSpan,
            ref Utf8YamlReader reader,
            ref MetadataDb database,
            ref StackRowStack stack,
            ref YamlTables? tables,
            ref YamlLoopState state,
            ref Dictionary<string, int>? anchors,
            int baseOffset,
            CommentCollector? comments)
        {
            // baseOffset: M5e, a collection key's own rows are read by its key reader, whose positions count from the
            // key's first byte; the document's reader passes 0. comments: the document reader's collector under
            // CommentHandling.Allow; a collection key's comments are not kept.
            bool inArray = state.InArray;
            int arrayItemsOrPropertyCount = state.ArrayItemsOrPropertyCount;
            int numberOfRowsForMembers = state.NumberOfRowsForMembers;
            int numberOfRowsForValues = state.NumberOfRowsForValues;
            int lastEnd = state.LastEnd;

            do
            {
                YamlTokenType tokenType = reader.TokenType;
                if (tokenType == YamlTokenType.Comment)
                {
                    comments?.OnComment(utf8YamlSpan, ref reader);
                    continue;
                }

                Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
                int tokenStart = checked((int)reader.TokenStartIndex + baseOffset);
                int rowIndex = database.Length;
                comments?.OnToken(utf8YamlSpan, ref reader, tokenType, rowIndex);
                string? anchor = reader.Anchor.IsEmpty ? null : YamlReaderHelper.TranscodeHelper(reader.Anchor);
                string? tag = reader.Tag.IsEmpty ? null : YamlReaderHelper.TranscodeHelper(reader.Tag);
                string? expandedTag = tag is null ? null : reader.GetExpandedTag();
                bool hasProperties = anchor is not null || tag is not null;

                if (tokenType == YamlTokenType.StartObject)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForValues++;
                    bool flowPair = reader.IsFlow && reader.ValueSpan.IsEmpty;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize, scalarFlags: 0, hasProperties || flowPair);
                    stack.Push(new StackRow(arrayItemsOrPropertyCount, numberOfRowsForMembers + 1));
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForMembers = 0;
                    if (flowPair)
                    {
                        (tables ??= new YamlTables()).Add(new NodeInfo { Row = rowIndex, RawStart = -1, RawLength = -1, Anchor = anchor, Tag = tag, ExpandedTag = expandedTag, FlowPair = true });
                    }
                    else
                    {
                        AddProperties(ref tables, rowIndex, anchor, tag, expandedTag);
                    }
                    lastEnd = tokenStart + reader.ValueSpan.Length;
                }
                else if (tokenType == YamlTokenType.EndObject)
                {
                    int rowIndexOfStart = database.FindIndexOfFirstUnsetSizeOrLength(YamlTokenType.StartObject);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndexOfStart, arrayItemsOrPropertyCount);

                    int newRowIndex = database.Length;
                    AppendEnd(ref database, ref reader, tokenType, tokenStart, ref lastEnd);
                    database.SetNumberOfRows(rowIndexOfStart, numberOfRowsForMembers);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForMembers += row.NumberOfRows;
                }
                else if (tokenType == YamlTokenType.StartArray)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForMembers++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize, scalarFlags: 0, hasProperties);
                    stack.Push(new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1));
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForValues = 0;
                    AddProperties(ref tables, rowIndex, anchor, tag, expandedTag);
                    lastEnd = tokenStart + reader.ValueSpan.Length;
                }
                else if (tokenType == YamlTokenType.EndArray)
                {
                    int rowIndexOfStart = database.FindIndexOfFirstUnsetSizeOrLength(YamlTokenType.StartArray);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndexOfStart, arrayItemsOrPropertyCount);
                    database.SetNumberOfRows(rowIndexOfStart, numberOfRowsForValues);

                    // As in the JSON loop: a simple-valued array has one row per item plus its end row.
                    if (arrayItemsOrPropertyCount + 1 != numberOfRowsForValues)
                    {
                        database.SetHasComplexChildren(rowIndexOfStart);
                    }

                    int newRowIndex = database.Length;
                    AppendEnd(ref database, ref reader, tokenType, tokenStart, ref lastEnd);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForValues += row.NumberOfRows;
                }
                else if (tokenType == YamlTokenType.PropertyName)
                {
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    arrayItemsOrPropertyCount++;

                    switch (reader.KeyKind)
                    {
                        case YamlKeyKind.Complex:
                        {
                            // M5e: the name row over the key's raw text, then the key node's own rows.
                            int keyRows = AppendComplexKey(utf8YamlSpan, ref database, ref reader, ref stack, ref tables, ref anchors, tokenStart, ref lastEnd);
                            numberOfRowsForValues += keyRows;
                            numberOfRowsForMembers += keyRows;
                            break;
                        }

                        case YamlKeyKind.Alias:
                        {
                            int keyRows = AppendAliasKey(ref database, ref reader, ref tables, anchors, rowIndex, tokenStart, ref lastEnd);
                            numberOfRowsForValues += keyRows;
                            numberOfRowsForMembers += keyRows;
                            break;
                        }

                        default:
                            AppendScalar(utf8YamlSpan, ref database, ref reader, ref tables, tokenType, tokenStart, rowIndex, anchor, tag, expandedTag, ref lastEnd);
                            break;
                    }

                    Debug.Assert(!inArray);
                }
                else if (tokenType == YamlTokenType.Alias)
                {
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    string name = YamlReaderHelper.TranscodeHelper(reader.ValueSpan);
                    int target = ResolveAnchor(anchors, name);
                    bool recursive = database.Get(target).IsUnknownSize; // the anchored collection is still open
                    database.Append(YamlTokenType.Alias, tokenStart, target, recursive ? DbRow.RecursiveAliasFlag : 0, hasNodeInfo: true);
                    tables ??= new YamlTables();
                    tables.HasAliases = true;
                    lastEnd = tokenStart + 1 + reader.ValueSpan.Length;
                    tables.Add(new NodeInfo { Row = rowIndex, RawStart = tokenStart, RawLength = lastEnd - tokenStart, Anchor = name });
                }
                else
                {
                    Debug.Assert(tokenType is >= YamlTokenType.String and <= YamlTokenType.Null);
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    AppendScalar(utf8YamlSpan, ref database, ref reader, ref tables, tokenType, tokenStart, rowIndex, anchor, tag, expandedTag, ref lastEnd);
                }

                if (anchor is not null)
                {
                    // Registered at the node's first row, so an alias inside the node it names is seen as recursive.
                    (anchors ??= new Dictionary<string, int>(StringComparer.Ordinal))[anchor] = rowIndex;
                }

                inArray = reader.IsInArray;
            }
            while (reader.Read());
        }

        private static int ResolveAnchor(Dictionary<string, int>? anchors, string name)
        {
            if (anchors is null || !anchors.TryGetValue(name, out int target))
            {
                ThrowHelper.ThrowYamlUnknownAliasException(name);
                return 0;
            }

            return target;
        }

        /// <summary>
        /// M5d: the tag of a value copied from a reader, which the copy (from
        /// the node's first byte) does not hold.
        /// </summary>
        internal void AttachRootTag(string tag, string? expandedTag)
        {
            _yaml ??= new YamlTables();
            if (_yaml.InfoCount > 0 && _yaml.Infos[0].Row == 0)
            {
                if (_yaml.Infos[0].Tag is not null)
                {
                    // A block collection's copy starts on its first line, which can hold the tag: it is in the source.
                    return;
                }

                _yaml.Infos[0].Tag = tag;
                _yaml.Infos[0].ExpandedTag = expandedTag;
            }
            else
            {
                if (_yaml.InfoCount == _yaml.Infos.Length)
                {
                    Array.Resize(ref _yaml.Infos, Math.Max(8, _yaml.InfoCount * 2));
                }

                Array.Copy(_yaml.Infos, 0, _yaml.Infos, 1, _yaml.InfoCount);
                _yaml.Infos[0] = new NodeInfo { Row = 0, RawStart = -1, RawLength = -1, Tag = tag, ExpandedTag = expandedTag };
                _yaml.InfoCount++;
            }

            _parsedData.SetHasNodeInfo(0);
            _yaml.RootTagOutsideSource = true;
        }

        /// <summary>
        /// The tag text that reads back alone, without the source's %TAG directives: a local tag as is, a core tag as
        /// <c>!!x</c>, any other global tag verbatim.
        /// </summary>
        internal static string StandAloneTag(in NodeInfo info)
        {
            string? expanded = info.ExpandedTag;
            if (expanded is null || expanded.Length == 0)
            {
                return info.Tag!;
            }

            if (expanded[0] == '!')
            {
                return expanded;
            }

            return expanded.StartsWith(Serialization.YamlTags.CorePrefix, StringComparison.Ordinal)
                ? "!!" + expanded.Substring(Serialization.YamlTags.CorePrefix.Length)
                : "!<" + expanded + ">";
        }

        private static void AddProperties(ref YamlTables? tables, int rowIndex, string? anchor, string? tag, string? expandedTag)
        {
            if (anchor is not null || tag is not null)
            {
                (tables ??= new YamlTables()).Add(new NodeInfo { Row = rowIndex, RawStart = -1, RawLength = -1, Anchor = anchor, Tag = tag, ExpandedTag = expandedTag });
            }
        }

        // A block collection's end token has no bytes: its row ends where the collection's last node ends, so that
        // GetRawText (start row to end row) covers the collection's text only. A flow end keeps its one byte.
        private static void AppendEnd(ref MetadataDb database, ref Utf8YamlReader reader, YamlTokenType tokenType, int tokenStart, ref int lastEnd)
        {
            int length = reader.ValueSpan.Length;
            int location = length > 0 ? tokenStart : lastEnd;
            database.Append(tokenType, location, length);
            lastEnd = location + length;
        }

        // AppendScalar's rows for a scalar with no anchor, no tag and nothing to decode: every scalar of the JSON-shaped
        // loop.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendSimpleScalar(
            ReadOnlySpan<byte> utf8YamlSpan,
            ref MetadataDb database,
            ref Utf8YamlReader reader,
            ref YamlTables? tables,
            YamlTokenType tokenType,
            int tokenStart,
            ref int lastEnd)
        {
            ReadOnlySpan<byte> value = reader.ValueSpan;
            YamlScalarStyle style = reader.ScalarStyle;
            bool quoted = style is YamlScalarStyle.DoubleQuoted or YamlScalarStyle.SingleQuoted;
            int valueStart = value.IsEmpty
                ? tokenStart + (quoted ? 1 : 0)
                : (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(utf8YamlSpan), ref MemoryMarshal.GetReference(value));
            int flags = !quoted && tokenType is YamlTokenType.String or YamlTokenType.PropertyName ? DbRow.PlainFlag : 0;
            if (tokenType == YamlTokenType.Number && !YamlReaderHelper.IsJsonNumber(value))
            {
                (tables ??= new YamlTables()).HasYamlNumbers = true;
            }

            database.Append(tokenType, valueStart, value.Length, flags | DbRow.StyleBits(style), hasNodeInfo: false);
            if (style == YamlScalarStyle.DoubleQuoted && reader.ValueIsEscaped)
            {
                database.SetHasComplexChildren(database.Length - DbRow.Size);
            }

            lastEnd = valueStart + value.Length + (quoted ? 1 : 0);
        }

        private static void AppendScalar(
            ReadOnlySpan<byte> utf8YamlSpan,
            ref MetadataDb database,
            ref Utf8YamlReader reader,
            ref YamlTables? tables,
            YamlTokenType tokenType,
            int tokenStart,
            int rowIndex,
            string? anchor,
            string? tag,
            string? expandedTag,
            ref int lastEnd)
        {
            ReadOnlySpan<byte> value = reader.ValueSpan;
            YamlScalarStyle style = reader.ScalarStyle;
            bool quoted = style is YamlScalarStyle.DoubleQuoted or YamlScalarStyle.SingleQuoted;

            // An empty value's span may not point into the input: a quoted one starts after its quote.
            int valueStart = value.IsEmpty
                ? tokenStart + (quoted ? 1 : 0)
                : (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(utf8YamlSpan), ref MemoryMarshal.GetReference(value));
            int rawStart = tokenStart;
            int rawEnd = valueStart + value.Length + (quoted ? 1 : 0);
            bool stringLike = tokenType is YamlTokenType.String or YamlTokenType.PropertyName;

            int location;
            int length;
            int flags = 0;
            bool escaped = false;
            bool hasNodeInfo = anchor is not null || tag is not null;
            if (reader.YamlNeedsProcessing && stringLike)
            {
                tables ??= new YamlTables();
                ReadOnlySpan<byte> raw = reader.YamlValueBytes;
                Span<byte> destination = tables.ReserveDecoded(YamlReaderHelper.GetMaxYamlProcessedLength(raw.Length));
                length = YamlReaderHelper.ProcessYamlScalar(raw, destination, style, reader.YamlBlockScalarIndent, reader.YamlBlockChomping);
                location = checked(utf8YamlSpan.Length + tables.DecodedLength);
                tables.DecodedLength += length;
                flags = DbRow.DecodedFlag;
                hasNodeInfo = true;
            }
            else
            {
                location = valueStart;
                length = value.Length;
                if (style == YamlScalarStyle.DoubleQuoted)
                {
                    // Not processed, so its escapes are JSON's.
                    escaped = reader.ValueIsEscaped;
                }
                else if (!quoted && stringLike)
                {
                    flags = DbRow.PlainFlag;
                }
            }

            if (tokenType == YamlTokenType.Number && !YamlReaderHelper.IsJsonNumber(value))
            {
                (tables ??= new YamlTables()).HasYamlNumbers = true;
            }

            database.Append(tokenType, location, length, flags | DbRow.StyleBits(style), hasNodeInfo);
            if (escaped)
            {
                database.SetHasComplexChildren(database.Length - DbRow.Size);
            }

            if (hasNodeInfo)
            {
                bool decoded = (flags & DbRow.DecodedFlag) != 0;
                (tables ??= new YamlTables()).Add(new NodeInfo
                {
                    Row = rowIndex,
                    RawStart = decoded ? rawStart : -1,
                    RawLength = decoded ? rawEnd - rawStart : -1,
                    Anchor = anchor,
                    Tag = tag,
                    ExpandedTag = expandedTag,
                    BlockIndent = decoded && style is YamlScalarStyle.Literal or YamlScalarStyle.Folded ? reader.YamlBlockScalarIndent : 0,
                });
            }

            lastEnd = rawEnd;
        }

        /// <summary>
        /// M5e: a collection key. Its name row covers the key's raw text (plain,
        /// so the name is that text verbatim) and carries <see cref="DbRow.ComplexKeyFlag"/>; the key node's rows follow,
        /// read by the key reader with the document's anchors, so an anchor in a key is seen by later aliases and an
        /// alias in a key resolves to an earlier anchor. Returns the key's rows (the name row aside).
        /// </summary>
        private static int AppendComplexKey(
            ReadOnlySpan<byte> utf8YamlSpan,
            ref MetadataDb database,
            ref Utf8YamlReader reader,
            ref StackRowStack stack,
            ref YamlTables? tables,
            ref Dictionary<string, int>? anchors,
            int tokenStart,
            ref int lastEnd)
        {
            ReadOnlySpan<byte> raw = reader.ValueSpan;
            int keyStart = raw.IsEmpty
                ? tokenStart
                : (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(utf8YamlSpan), ref MemoryMarshal.GetReference(raw));
            database.Append(YamlTokenType.PropertyName, keyStart, raw.Length, DbRow.PlainFlag | DbRow.ComplexKeyFlag, hasNodeInfo: false);
            tables ??= new YamlTables();
            tables.HasComplexKeys = true;

            int before = database.Length;
            Utf8YamlReader keyReader = reader.CreateKeyReader();
            if (keyReader.Read())
            {
                var state = new YamlLoopState { LastEnd = keyStart };
                ParseYamlFull(utf8YamlSpan, ref keyReader, ref database, ref stack, ref tables, ref state, ref anchors, baseOffset: keyStart, comments: null);
            }

            lastEnd = keyStart + raw.Length;
            return (database.Length - before) / DbRow.Size;
        }

        // An alias key ('*k : v') is the anchored scalar's text: the key row is a copy of that scalar's row. M5e: an alias
        // to a collection is a collection key, whose name is the anchored node's raw text and whose one key row is the
        // alias itself. Returns the key's rows (0 for a scalar).
        private static int AppendAliasKey(
            ref MetadataDb database,
            ref Utf8YamlReader reader,
            ref YamlTables? tables,
            Dictionary<string, int>? anchors,
            int rowIndex,
            int tokenStart,
            ref int lastEnd)
        {
            string name = YamlReaderHelper.TranscodeHelper(reader.ValueSpan);
            int target = ResolveAnchor(anchors, name);
            DbRow targetRow = database.Get(target);
            if (!targetRow.IsSimpleValue)
            {
                bool recursive = targetRow.IsUnknownSize; // the anchored collection is still open
                int nameStart = tokenStart;
                int nameLength = 1 + reader.ValueSpan.Length;
                if (!recursive)
                {
                    DbRow end = database.Get(target + DbRow.Size * targetRow.NumberOfRows);
                    nameStart = targetRow.Location;
                    nameLength = end.Location + end.SizeOrLength - nameStart;
                }

                database.Append(YamlTokenType.PropertyName, nameStart, nameLength, DbRow.PlainFlag | DbRow.ComplexKeyFlag, hasNodeInfo: false);
                database.Append(YamlTokenType.Alias, tokenStart, target, recursive ? DbRow.RecursiveAliasFlag : 0, hasNodeInfo: true);
                tables ??= new YamlTables();
                tables.HasAliases = true;
                tables.HasComplexKeys = true;
                lastEnd = tokenStart + 1 + reader.ValueSpan.Length;
                tables.Add(new NodeInfo { Row = rowIndex + DbRow.Size, RawStart = tokenStart, RawLength = lastEnd - tokenStart, Anchor = name });
                return 1;
            }

            database.AppendCopy(target, YamlTokenType.PropertyName);
            (tables ??= new YamlTables()).HasAliases = true;

            // M5e: the alias is recorded, so that KeyKind is Alias and GetKeyElement gives the anchored node.
            NodeInfo info = default;
            bool targetInfo = targetRow.HasNodeInfo && tables!.TryGet(target, out info);
            tables.Add(new NodeInfo
            {
                Row = rowIndex,
                RawStart = targetInfo ? info.RawStart : -1,
                RawLength = targetInfo ? info.RawLength : -1,
                AliasKeyTarget = target + 1,
            });
            if (!targetRow.HasNodeInfo)
            {
                database.SetHasNodeInfo(rowIndex);
            }

            lastEnd = tokenStart + 1 + reader.ValueSpan.Length;
            return 0;
        }

        /// <summary>
        /// Decision D4: the YAML grammar reads empty, whitespace-only or comment-only input as zero tokens, which
        /// System.Text.Json reports as an error. A document needs a value, so the document keeps that error; without it the
        /// empty buffer would make the new document look disposed.
        /// </summary>
        private static void ThrowIfNoYamlValue(ref Utf8YamlReader reader, int databaseLength)
        {
            if (databaseLength == 0)
            {
                YamlException exception = ThrowHelper.GetYamlReaderException(ref reader, ExceptionResource.ExpectedYamlTokens, default, default);
                exception.IsEmptyInput = true;
                throw exception;
            }
        }
    }
}
