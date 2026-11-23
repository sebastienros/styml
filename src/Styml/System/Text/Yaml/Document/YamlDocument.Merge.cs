// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // M5b, merge keys (yaml.org/type/merge), opt-in through
        // ResolveMergeKeys. A document whose mappings have a plain '<<' key is written again with each such mapping merged,
        // and that text is what the DOM and the serializer read. In a merged mapping:
        // - merged entries sit at the position of '<<', in the order of their sources;
        // - the mapping's own keys win wherever they appear: an own key before '<<' keeps its place and value, and an
        //   own key after it puts its value at the merged key's place;
        // - an earlier source wins over a later one; a source's own merges apply first.
        // A copy of an aliased mapping's entries writes no anchors (the original keeps them); an inline source, written
        // only here, keeps its anchors. Copied nodes count against MaxAliasExpansion.

        private static ReadOnlySpan<byte> MergeKey => "<<"u8;

        /// <summary>
        /// When <paramref name="options"/> resolves merge keys and the document has one, returns the document read from
        /// its merged text and disposes <paramref name="document"/>; otherwise returns it as is.
        /// </summary>
        internal static YamlDocument ApplyMergeKeys(YamlDocument document, YamlDocumentOptions options)
        {
            if (!options.ResolveMergeKeys || !document.HasAnyMergeKey())
            {
                return document;
            }

            // If writing or parsing the merged text throws, the caller still owns the first document's buffers: it is
            // disposed only once the merged one exists.
            // The merged text is the document's own array, not a pooled one, so the result needs no disposal.
            byte[] merged = document.WriteMerged(options.DuplicateKeyHandling);
            YamlDocument result = ParseUnrented(merged, options.GetReaderOptions(), options.DuplicateKeyHandling, maxAliasExpansion: options.MaxAliasExpansion);
            document.Dispose();
            return result;
        }

        /// <summary>
        /// The merged text of <paramref name="utf8Yaml"/> when it has a merge key, or null (the serializer's pre-pass,
        /// for <c>YamlSerializerOptions.ResolveMergeKeys</c>). The parse keeps repeated keys: the serializer judges them
        /// per consumer, so the rewrite must not make its <c>Error</c> a whole-document check. Only a key repeated inside
        /// one merge source is judged here, with <paramref name="mergeSourceHandling"/>.
        /// </summary>
        internal static byte[]? RewriteMergeKeys(ReadOnlyMemory<byte> utf8Yaml, YamlDocumentOptions options, YamlDuplicateKeyHandling mergeSourceHandling)
        {
            if (utf8Yaml.Span.IndexOf(MergeKey) < 0)
            {
                return null;
            }

            options.ResolveMergeKeys = false;
            options.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            using YamlDocument document = Parse(utf8Yaml, options);
            return document.HasAnyMergeKey() ? document.WriteMerged(mergeSourceHandling) : null;
        }

        /// <summary>The merged text of the value on <paramref name="reader"/>'s next token when it has a merge key, or null.</summary>
        internal static byte[]? RewriteMergeKeysOfValue(Utf8YamlReader reader, YamlDuplicateKeyHandling mergeSourceHandling)
        {
            ReadOnlySequence<byte> sequence = reader.OriginalSequence;
            if ((sequence.IsEmpty ? reader.OriginalSpan : sequence.ToArray()).IndexOf(MergeKey) < 0)
            {
                return null;
            }

            YamlDocument document = ParseValue(ref reader, YamlDuplicateKeyHandling.LastWins);
            try
            {
                return document.HasAnyMergeKey() ? document.WriteMerged(mergeSourceHandling) : null;
            }
            finally
            {
                document.Dispose();
            }
        }

        private bool HasAnyMergeKey()
        {
            for (int i = 0; i < _parsedData.Length; i += DbRow.Size)
            {
                if (IsMergeKey(i))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMergeKey(int index)
        {
            DbRow row = _parsedData.Get(index);
            return row.TokenType == YamlTokenType.PropertyName && row.ScalarStyle == YamlScalarStyle.Plain &&
                row.SizeOrLength == 2 && _utf8Yaml.Span.Slice(row.Location, 2).SequenceEqual(MergeKey);
        }

        /// <summary>Whether the mapping at <paramref name="index"/> has a merge key among its own keys.</summary>
        private bool HasMergeKey(int index)
        {
            for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
            {
                if (IsMergeKey(i))
                {
                    return true;
                }

                int value = ValueRowOf(i);
                i = value + SubtreeRows(value) * DbRow.Size;
            }

            return false;
        }

        private byte[] WriteMerged(YamlDuplicateKeyHandling mergeSourceHandling)
        {
            var buffer = new ArrayBufferWriterOfByte();
            using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = Math.Max(TraversalMaxDepth * 2, 64), SkipValidation = true }))
            {
                var state = new YamlWriteState { RangeStart = 0, RangeEnd = _parsedData.Length, ResolveMerges = true, MergeSourceHandling = mergeSourceHandling };
                WriteYamlNode(0, writer, ref state);
            }

            return buffer.ToArray();
        }

        private readonly struct MergeEntry
        {
            internal MergeEntry(int keyRow, int valueRow, bool copy, bool suppressAnchors)
            {
                KeyRow = keyRow;
                ValueRow = valueRow;
                Copy = copy;
                SuppressAnchors = suppressAnchors;
            }

            internal int KeyRow { get; }

            // M5e: a collection key's rows sit between its name and its value (ValueRowOf).
            internal int ValueRow { get; }

            /// <summary>The entry comes from a merged source, not from the mapping's own keys.</summary>
            internal bool Copy { get; }

            internal bool SuppressAnchors { get; }
        }

        /// <summary>The entries of the mapping at <paramref name="index"/> after its merges, in the order they are written.</summary>
        private List<MergeEntry> GetMergedEntries(int index, ref YamlWriteState state)
        {
            CheckTraversalDepth(++state.Depth);
            var entries = new List<MergeEntry>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = index + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndObject;)
            {
                int value = ValueRowOf(i);
                if (IsMergeKey(i))
                {
                    foreach ((int source, bool suppress) in GetMergeSources(value))
                    {
                        // M5c: the names this source has given so far, with the entry each placed (-1: an earlier source
                        // or an own key had it). A repeat inside the source is the duplicate-key handling's to judge
                        //; across sources and against own keys it is merge order, never a duplicate.
                        var inSource = new Dictionary<string, int>(StringComparer.Ordinal);
                        foreach (MergeEntry merged in GetMergedEntries(source, ref state))
                        {
                            string name = MergeIndexKey(merged.KeyRow);
                            if (inSource.TryGetValue(name, out int placed))
                            {
                                if (state.MergeSourceHandling == YamlDuplicateKeyHandling.Error)
                                {
                                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(KeyName(merged.KeyRow));
                                }

                                if (state.MergeSourceHandling == YamlDuplicateKeyHandling.LastWins && placed >= 0)
                                {
                                    CountMergeCopy(merged.ValueRow, ref state.Expanded);
                                    entries[placed] = new MergeEntry(merged.KeyRow, merged.ValueRow, copy: true, suppress || merged.SuppressAnchors);
                                }

                                continue;
                            }

                            if (!positions.ContainsKey(name))
                            {
                                CountMergeCopy(merged.ValueRow, ref state.Expanded);
                                positions[name] = entries.Count;
                                inSource[name] = entries.Count;
                                entries.Add(new MergeEntry(merged.KeyRow, merged.ValueRow, copy: true, suppress || merged.SuppressAnchors));
                            }
                            else
                            {
                                inSource[name] = -1;
                            }
                        }
                    }
                }
                else
                {
                    string name = MergeIndexKey(i);
                    if (positions.TryGetValue(name, out int position) && entries[position].Copy)
                    {
                        // An own key after a merged one: its value takes the merged key's place.
                        entries[position] = new MergeEntry(i, value, copy: false, suppressAnchors: false);
                    }
                    else
                    {
                        // A repeated own key stays repeated, for the duplicate-key rules to judge.
                        if (!positions.ContainsKey(name))
                        {
                            positions[name] = entries.Count;
                        }

                        entries.Add(new MergeEntry(i, value, copy: false, suppressAnchors: false));
                    }
                }

                i = value + SubtreeRows(value) * DbRow.Size;
            }

            state.Depth--;
            return entries;
        }

        /// <summary>
        /// M5e: the merge index's key for an entry. A scalar key by its decoded text, a collection key by its
        /// canonical form; the first character keeps the two kinds apart.
        /// </summary>
        private string MergeIndexKey(int keyRow) =>
            IsComplexKeyRow(keyRow) ? "c" + GetCanonicalKey(keyRow) : "s" + GetString(keyRow, YamlTokenType.PropertyName);

        /// <summary>The mappings a merge key's value names: an alias, an inline mapping, or a sequence of them.</summary>
        private List<(int Source, bool SuppressAnchors)> GetMergeSources(int value)
        {
            var sources = new List<(int, bool)>();
            DbRow row = _parsedData.Get(value);
            if (row.TokenType == YamlTokenType.StartArray)
            {
                for (int i = value + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndArray; i += SubtreeRows(i) * DbRow.Size)
                {
                    sources.Add(GetMergeSource(i));
                }
            }
            else if (row.TokenType == YamlTokenType.Alias && _parsedData.Get(row.SizeOrLength).TokenType == YamlTokenType.StartArray)
            {
                int target = row.SizeOrLength;
                for (int i = target + DbRow.Size; _parsedData.Get(i).TokenType != YamlTokenType.EndArray; i += SubtreeRows(i) * DbRow.Size)
                {
                    (int source, _) = GetMergeSource(i);
                    sources.Add((source, true));
                }
            }
            else
            {
                sources.Add(GetMergeSource(value));
            }

            return sources;
        }

        private (int Source, bool SuppressAnchors) GetMergeSource(int index)
        {
            DbRow row = _parsedData.Get(index);
            bool alias = row.TokenType == YamlTokenType.Alias;
            if (alias)
            {
                if (IsRecursiveAlias(index))
                {
                    ThrowHelper.ThrowYamlException_RecursiveAliasExpansion(GetAliasName(index));
                }

                index = row.SizeOrLength;
                row = _parsedData.Get(index);
            }

            if (row.TokenType != YamlTokenType.StartObject)
            {
                ThrowHelper.ThrowYamlException_MergeValueNotAMapping();
            }

            return (index, alias);
        }

        private void CountMergeCopy(int valueRow, ref int expanded)
        {
            // The entry's key and its value's nodes.
            int added = 1 + SubtreeRows(valueRow);
            if (System.Threading.Interlocked.Add(ref expanded, added) > AliasExpansionLimit)
            {
                ThrowHelper.ThrowYamlException_AliasExpansionLimit(AliasExpansionLimit);
            }
        }

        private int WriteMergedObject(int index, Utf8YamlWriter writer, ref YamlWriteState state)
        {
            List<MergeEntry> entries = GetMergedEntries(index, ref state);
            writer.WriteStartObject();
            CheckTraversalDepth(++state.Depth);
            bool outer = state.SuppressAnchors;
            foreach (MergeEntry entry in entries)
            {
                state.SuppressAnchors = outer || entry.SuppressAnchors;
                DbRow key = _parsedData.Get(entry.KeyRow);
                if ((key.ScalarFlags & DbRow.ComplexKeyFlag) != 0)
                {
                    // M5e: a collection key in a merged mapping is written back as one.
                    writer.WriteStartPropertyKey();
                    WriteYamlNode(entry.KeyRow + DbRow.Size, writer, ref state);
                    writer.WriteEndPropertyKey();
                    WriteYamlNode(entry.ValueRow, writer, ref state);
                    continue;
                }

                if (key.HasNodeInfo && _yaml!.TryGet(entry.KeyRow, out NodeInfo keyInfo))
                {
                    if (keyInfo.Anchor is not null && !state.SuppressAnchors)
                    {
                        writer.WriteAnchor(keyInfo.Anchor);
                    }

                    if (keyInfo.Tag is not null)
                    {
                        writer.WriteTag(keyInfo.Tag);
                    }
                }

                WritePropertyName(key, writer);
                WriteYamlNode(entry.ValueRow, writer, ref state);
            }

            state.SuppressAnchors = outer;
            state.Depth--;
            writer.WriteEndObject();
            return index + SubtreeRows(index) * DbRow.Size;
        }
    }
}
