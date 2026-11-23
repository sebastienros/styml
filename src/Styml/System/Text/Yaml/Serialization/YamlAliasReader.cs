// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The serializer's side of YAML anchors and aliases: an alias at a
    /// value site shares the anchored instance or replays the anchored node through the site's converter, counted
    /// against the operation's bound. Everything here is off the hot path: the converters test
    /// <see cref="Utf8YamlReader.YamlHasNodeRef"/> inline and call in only when it is set.
    /// </summary>
    internal static class YamlAliasReader
    {
        internal const int DefaultExpansionFloor = 10_000;

        /// <summary>The entry of the anchor on the current token, for a site that may store its instance; null otherwise.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static YamlAnchorEntry? FindAnchored(ref Utf8YamlReader reader)
        {
            Debug.Assert(reader.TokenType != YamlTokenType.Alias);
            return reader.YamlAnchorContext?.Table.FindAt(reader.YamlAbsoluteTokenStart);
        }

        /// <summary>An object site read the anchored node as a <see cref="YamlElement"/>: its box is what the node's aliases at object sites return.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void StoreElement(YamlAnchorEntry entry, object? value)
        {
            if (entry.Element is null && value is YamlElement)
            {
                entry.Element = value;
            }
        }

        /// <summary>The anchored node read by the frame at <paramref name="depth"/> is open until the frame completes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void OpenAnchored(ref Utf8YamlReader reader, YamlAnchorEntry entry, int depth)
        {
            reader.YamlAnchorContext!.Table.PushOpen(depth, entry);
            entry.OpenCount++;
        }

        /// <summary>A frame completed: if it read an anchored node, the node closes, and the converter's instance is stored unless it was at creation.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CompleteAnchored(YamlConverter converter, ref Utf8YamlReader reader, int depth, object? value)
        {
            if (reader.YamlAnchorContext!.Table.PopOpen(depth) is not { } entry)
            {
                return;
            }

            entry.OpenCount--;
            if (entry.Instance is null && value is not null && CanStore(converter))
            {
                entry.Instance = value;
            }
        }

        /// <summary>A converter that made <paramref name="instance"/> and has not read its content yet stores it, so that an alias inside it can refer to it.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreAtCreation(ref Utf8YamlReader reader, scoped ref ReadStack state, object instance)
        {
            if (reader.YamlAnchorContext is not null)
            {
                StoreAtCreationSlow(ref reader, state.FrameCount, instance);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StoreAtCreationSlow(ref Utf8YamlReader reader, int depth, object instance)
        {
            if (reader.YamlAnchorContext!.Table.PeekOpen(depth) is { Instance: null } entry)
            {
                entry.Instance = instance;
            }
        }

        /// <summary>Only what the write side anchors is stored: an instance of a built-in object or collection converter.</summary>
        private static bool CanStore(YamlConverter converter) =>
            !converter.IsValueType && converter.IsInternalConverter && !converter.IsUntypedConverter &&
            converter.ConverterStrategy is ConverterStrategy.Object or ConverterStrategy.Enumerable or ConverterStrategy.Dictionary;

        /// <summary>Whether a site whose converter is <paramref name="converter"/> takes an anchored instance as is.</summary>
        internal static bool CanShare(YamlConverter converter, scoped ref ReadStack state)
        {
            if (!CanStore(converter))
            {
                return false;
            }

            // A Populate site fills the instance it already has, so the alias replays into it.
            return state.Current.YamlPropertyInfo is not { EffectiveObjectCreationHandling: YamlObjectCreationHandling.Populate };
        }

        /// <summary>Reads the alias on the current token into a <typeparamref name="T"/> site.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T? ReadAlias<T>(YamlConverter<T> converter, ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state)
        {
            // M5e: also an alias key to a collection, which reads as an alias value does.
            Debug.Assert(reader.TokenType == YamlTokenType.Alias || reader.KeyKind == YamlKeyKind.Alias);
            YamlAnchorEntry entry = Resolve(ref reader, out YamlAnchorTable table);

            // With Preserve or PreserveAll, an object member takes the anchored instance too, as System.Text.Json's
            // $ref into an object member does, so a Preserve round trip keeps every reference. With no handler an object
            // member is always a YamlElement.
            bool share = CanShare(converter, ref state) ||
                (typeof(T) == typeof(object) && converter.IsUntypedConverter && IsPreserve(options));
            if (entry.OpenCount > 0)
            {
                // The alias is inside the node it names.
                if (options.ReferenceHandlingStrategy is YamlKnownReferenceHandler.Preserve or YamlKnownReferenceHandler.PreserveAll &&
                    share && entry.Instance is T cyclic)
                {
                    return cyclic;
                }

                ThrowRecursive(entry, typeToConvert, options);
            }

            if (share && entry.Instance is T instance)
            {
                return instance;
            }

            if (typeof(T) == typeof(object) && converter.IsUntypedConverter && entry.Element is { } element)
            {
                // An immutable DOM: object sites share the anchored node's element, boxed once, so that every alias
                // gives the same instance, as System.Text.Json's Preserve does for an object member.
                return (T)element;
            }

            T? value = Replay(converter, entry, table, ref reader, typeToConvert, options, ref state);
            if (typeof(T) == typeof(object) && converter.IsUntypedConverter)
            {
                StoreElement(entry, value);
            }

            return value;
        }

        /// <summary>The entry the alias on the current token (a value or a key) resolves to; throws when there is none.</summary>
        internal static YamlAnchorEntry Resolve(ref Utf8YamlReader reader, out YamlAnchorTable table)
        {
            ReadOnlySpan<byte> name = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
            long position = reader.YamlAbsoluteTokenStart;
            YamlAnchorContext? context = reader.YamlAnchorContext;
            YamlAnchorEntry? entry = context?.Table.Find(name, position);
            if (entry is null)
            {
                ThrowHelper.ThrowYamlException_UnknownAlias(YamlReaderHelper.TranscodeHelper(name));
            }

            table = context!.Table;
            if (position > table.Furthest)
            {
                table.Furthest = position;
            }

            return entry;
        }

        /// <summary>
        /// Reads the anchored node of <paramref name="entry"/> again, from the table's store, with
        /// <paramref name="converter"/>: a reader scoped to the node's bytes, with its recorded indentation and %TAG
        /// directives, and the depth left at the alias site.
        /// </summary>
        internal static T? Replay<T>(YamlConverter<T> converter, YamlAnchorEntry entry, YamlAnchorTable table, ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state)
        {
            Utf8YamlReader replay = CreateReplayReader(entry, table, ref reader, options, state.StreamPosition);
            entry.OpenCount++;
            try
            {
                bool success = converter.TryRead(ref replay, typeToConvert, options, ref state, out T? value, out _);
                Debug.Assert(success, "A replay reads complete data.");
                return value;
            }
            finally
            {
                entry.OpenCount--;
            }
        }

        /// <summary>A reader positioned on the first token of <paramref name="entry"/>'s node, its nodes counted against the bound.</summary>
        internal static Utf8YamlReader CreateReplayReader(YamlAnchorEntry entry, YamlAnchorTable table, ref Utf8YamlReader reader, YamlSerializerOptions? options, long streamPosition = 0, bool count = true)
        {
            // A buffer of a stream may hold bytes the store does not have yet.
            reader.YamlStoreSource();
            if (!table.IsStored(entry.Position))
            {
                Debug.Fail("An anchor's bytes are stored from its first byte on.");
                ThrowHelper.ThrowYamlException_UnknownAlias(entry.Name);
            }

            ReadOnlySpan<byte> stored = table.GetStored(entry.Position);
            if (entry.Limit > entry.Position && entry.Limit - entry.Position < stored.Length)
            {
                // M5e: the anchor was met inside a slice (a collection key): its node ends within it.
                stored = stored.Slice(0, checked((int)(entry.Limit - entry.Position)));
            }
            YamlReaderOptions readerOptions = reader.CurrentState.Options;
            if (entry.Length < 0)
            {
                Measure(entry, stored, readerOptions);
            }

            if (count)
            {
                Count(table, entry.NodeCount, options, streamPosition);
            }

            int maxDepth = readerOptions.MaxDepth == 0 ? YamlReaderOptions.DefaultMaxDepth : readerOptions.MaxDepth;
            int remaining = maxDepth - reader.CurrentDepth;
            if (remaining <= 0)
            {
                ThrowHelper.ThrowYamlException_AliasTraversalTooDeep(maxDepth);
            }

            readerOptions.MaxDepth = remaining;
            readerOptions.RecordAnchors = true;
            var state = new YamlReaderState(
                lineNumber: entry.Line,
                bytePositionInLine: entry.Column,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: readerOptions,
                bitStack: default,
                yaml: YamlParseState.ForScopedValue(entry.ParentIndent, entry.TagDirectives, new YamlAnchorContext(table, entry.Position, isSource: false)));

            var replay = new Utf8YamlReader(stored.Slice(0, entry.Length), isFinalBlock: true, state);
            bool read = replay.Read();
            Debug.Assert(read);
            return replay;
        }

        /// <summary>Finds where the anchored node ends and how many nodes it has (scalars and collections, aliases included).</summary>
        private static void Measure(YamlAnchorEntry entry, ReadOnlySpan<byte> stored, YamlReaderOptions readerOptions)
        {
            if (entry.FixedLength > 0)
            {
                // A scalar, or an anchored key, whose bytes read as one scalar node.
                entry.Length = entry.FixedLength;
                entry.NodeCount = 1;
                return;
            }

            readerOptions.RecordAnchors = false;
            var state = new YamlReaderState(
                lineNumber: entry.Line,
                bytePositionInLine: entry.Column,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: readerOptions,
                bitStack: default,
                yaml: YamlParseState.ForScopedValue(entry.ParentIndent, entry.TagDirectives));

            // The node is complete in the store (it precedes the alias), so the final-block flag only stops the probe
            // from waiting for more data after it.
            var probe = new Utf8YamlReader(stored, isFinalBlock: true, state);
            bool read = probe.Read();
            Debug.Assert(read);
            int nodes = 1;
            if (probe.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                int depth = probe.CurrentDepth;
                while (probe.Read() && depth < probe.CurrentDepth)
                {
                    if (probe.TokenType is not (YamlTokenType.PropertyName or YamlTokenType.Comment or YamlTokenType.EndObject or YamlTokenType.EndArray))
                    {
                        nodes++;
                    }
                }
            }

            entry.Length = checked((int)probe.BytesConsumed);
            entry.NodeCount = nodes;
        }

        /// <summary>
        /// Parses the value on the current token into a <see cref="YamlElement"/> for an <see cref="object"/> or DOM
        /// site. An alias inside the value whose anchor is outside it cannot be a row of the element's own document, so
        /// the value is then written again with each such alias expanded (counted against the bound) and parsed from
        /// that text.
        /// </summary>
        internal static YamlElement ParseElement(ref Utf8YamlReader reader, YamlDuplicateKeyHandling duplicateKeyHandling, YamlSerializerOptions? options)
        {
            if (reader.YamlAnchorContext is null)
            {
                // No anchor before the value, so every alias in it refers to an anchor inside it.
                return YamlElement.ParseValue(ref reader, duplicateKeyHandling);
            }

            YamlDocumentOptions expandedOptions = ExpandedOptions(reader.CurrentState.Options, duplicateKeyHandling);
            ExpandedSource source = new(ref reader);
            try
            {
                return YamlElement.ParseValue(ref reader, duplicateKeyHandling);
            }
            catch (YamlException e) when (e.IsUnknownAlias)
            {
                // The reader has moved past the value; it is read again from the store below.
            }

            using YamlDocument document = YamlDocument.Parse(source.Expand(options), expandedOptions);
            return document.RootElement.Clone();
        }

        /// <inheritdoc cref="ParseElement"/>
        internal static YamlDocument ParseDocument(ref Utf8YamlReader reader, YamlDuplicateKeyHandling duplicateKeyHandling, YamlSerializerOptions? options)
        {
            if (reader.YamlAnchorContext is null)
            {
                return YamlDocument.ParseValue(ref reader, duplicateKeyHandling);
            }

            YamlDocumentOptions expandedOptions = ExpandedOptions(reader.CurrentState.Options, duplicateKeyHandling);
            ExpandedSource source = new(ref reader);
            try
            {
                return YamlDocument.ParseValue(ref reader, duplicateKeyHandling);
            }
            catch (YamlException e) when (e.IsUnknownAlias)
            {
            }

            return YamlDocument.Parse(source.Expand(options), expandedOptions);
        }

        // M5c: the expanded text is read as the original value was, with the reader's schema, depth and commas
        // (A6; before, only the duplicate handling was passed).
        private static YamlDocumentOptions ExpandedOptions(YamlReaderOptions readerOptions, YamlDuplicateKeyHandling duplicateKeyHandling) => new()
        {
            DuplicateKeyHandling = duplicateKeyHandling,
            Schema = readerOptions.Schema,
            MaxDepth = readerOptions.MaxDepth,
            AllowTrailingCommas = readerOptions.AllowTrailingCommas,
            CommentHandling = readerOptions.CommentHandling == YamlCommentHandling.Allow ? YamlCommentHandling.Skip : readerOptions.CommentHandling,
            AcceptLenientScalars = readerOptions.AcceptLenientScalars,
        };

        /// <summary>Where a value starts in the operation's store, to write it again with outside aliases expanded.</summary>
        private readonly struct ExpandedSource
        {
            private readonly YamlAnchorTable _table;
            private readonly YamlAnchorEntry _start;
            private readonly YamlReaderOptions _readerOptions;

            internal ExpandedSource(ref Utf8YamlReader reader)
            {
                reader.YamlStoreSource();
                _table = reader.YamlAnchorContext!.Table;
                _start = new YamlAnchorEntry(string.Empty, reader.YamlAbsoluteTokenStart, reader.YamlRawTokenLine, reader.YamlRawTokenColumn, reader.YamlTokenParentIndent, reader.YamlTagDirectives, -1);
                _readerOptions = reader.CurrentState.Options;
            }

            internal byte[] Expand(YamlSerializerOptions? options)
            {
                if (!_table.IsStored(_start.Position))
                {
                    // Nothing was stored at or before the value, so its alias has no anchor at all.
                    ThrowHelper.ThrowYamlException_UnknownAlias("?");
                }

                var stream = new MemoryStream();
                using (var writer = new Utf8YamlWriter(stream, new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", MaxDepth = 1000, SkipValidation = true }))
                {
                    int maxDepth = _readerOptions.MaxDepth == 0 ? YamlReaderOptions.DefaultMaxDepth : _readerOptions.MaxDepth;
                    YamlReaderState state = CreateScopedState(_start, _readerOptions, maxDepth, new YamlAnchorContext(_table, _start.Position, isSource: false), recordAnchors: false);
                    var copy = new Utf8YamlReader(_table.GetStored(_start.Position), isFinalBlock: true, state);
                    copy.Read();
                    CopyNode(ref copy, writer, _table, options, new HashSet<string>(StringComparer.Ordinal), writeAnchors: true);
                }

                return stream.ToArray();
            }
        }

        private static YamlReaderState CreateScopedState(YamlAnchorEntry entry, YamlReaderOptions readerOptions, int maxDepth, YamlAnchorContext? context, bool recordAnchors)
        {
            readerOptions.MaxDepth = maxDepth;
            readerOptions.RecordAnchors = recordAnchors;
            return new YamlReaderState(
                lineNumber: entry.Line,
                bytePositionInLine: entry.Column,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: readerOptions,
                bitStack: default,
                yaml: YamlParseState.ForScopedValue(entry.ParentIndent, entry.TagDirectives, context));
        }

        /// <summary>
        /// Writes the node on <paramref name="reader"/>'s current token, leaving the reader on its last token. An alias to
        /// an anchor this copy wrote stays an alias; any other is replaced by its anchored node, counted.
        /// </summary>
        private static void CopyNode(ref Utf8YamlReader reader, Utf8YamlWriter writer, YamlAnchorTable table, YamlSerializerOptions? options, HashSet<string> anchors, bool writeAnchors)
        {
            if (reader.TokenType is YamlTokenType.Alias)
            {
                string name = AliasName(ref reader);
                if (anchors.Contains(name))
                {
                    writer.WriteAliasValue(name);
                    return;
                }

                YamlAnchorEntry entry = Resolve(ref reader, out _);
                if (entry.OpenCount > 0)
                {
                    ThrowHelper.ThrowYamlException_RecursiveAliasExpansion(entry.Name);
                }

                Utf8YamlReader replay = CreateReplayReader(entry, table, ref reader, options);
                entry.OpenCount++;
                try
                {
                    // Anchors inside an expansion are not written: an alias to one is expanded too.
                    CopyNode(ref replay, writer, table, options, anchors, writeAnchors: false);
                }
                finally
                {
                    entry.OpenCount--;
                }

                return;
            }

            if (writeAnchors && !reader.Anchor.IsEmpty)
            {
                anchors.Add(YamlReaderHelper.TranscodeHelper(reader.Anchor));
                writer.WriteAnchor(reader.Anchor);
            }

            if (!reader.Tag.IsEmpty)
            {
                writer.WriteTag(reader.Tag);
            }

            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                    writer.WriteStartObject();
                    while (ReadSkippingComments(ref reader) && reader.TokenType != YamlTokenType.EndObject)
                    {
                        Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);
                        switch (reader.KeyKind)
                        {
                            case YamlKeyKind.Alias:
                                string name = AliasName(ref reader);
                                if (anchors.Contains(name))
                                {
                                    writer.WriteAliasPropertyName(name);
                                }
                                else if (AnchorsACollection(ref reader, options, streamPosition: 0))
                                {
                                    // M5e: an alias key to a collection outside the copy is written as the node it names.
                                    YamlAnchorEntry keyEntry = Resolve(ref reader, out YamlAnchorTable keyTable);
                                    Utf8YamlReader replay = CreateReplayReader(keyEntry, keyTable, ref reader, options);
                                    writer.WriteStartPropertyKey();
                                    CopyNode(ref replay, writer, table, options, anchors, writeAnchors);
                                    writer.WriteEndPropertyKey();
                                }
                                else
                                {
                                    writer.WritePropertyName(ReadAliasKeyText(ref reader, options));
                                }

                                break;
                            case YamlKeyKind.Complex:
                            {
                                // M5e: a collection key is copied as an explicit key, with its anchors and aliases.
                                Utf8YamlReader keyReader = reader.CreateKeyReaderWithAnchors();
                                ReadSkippingComments(ref keyReader);
                                writer.WriteStartPropertyKey();
                                CopyNode(ref keyReader, writer, table, options, anchors, writeAnchors);
                                writer.WriteEndPropertyKey();
                                break;
                            }
                            default:
                                if (writeAnchors && !reader.Anchor.IsEmpty)
                                {
                                    anchors.Add(YamlReaderHelper.TranscodeHelper(reader.Anchor));
                                    writer.WriteAnchor(reader.Anchor);
                                }

                                if (!reader.Tag.IsEmpty)
                                {
                                    writer.WriteTag(reader.Tag);
                                }

                                writer.WritePropertyName(reader.GetString()!);
                                break;
                        }

                        ReadSkippingComments(ref reader);
                        CopyNode(ref reader, writer, table, options, anchors, writeAnchors);
                    }

                    writer.WriteEndObject();
                    break;
                case YamlTokenType.StartArray:
                    writer.WriteStartArray();
                    while (ReadSkippingComments(ref reader) && reader.TokenType != YamlTokenType.EndArray)
                    {
                        CopyNode(ref reader, writer, table, options, anchors, writeAnchors);
                    }

                    writer.WriteEndArray();
                    break;
                case YamlTokenType.String:
                    if (reader.ScalarStyle != YamlScalarStyle.Plain)
                    {
                        // M5c: a quoted or block string stays quoted, so that the expanded text reads as the source under
                        // every schema (under Json a plain string is an error).
                        writer.WriteScalarStyle(YamlScalarStyle.DoubleQuoted);
                    }

                    writer.WriteStringValue(reader.GetString());
                    break;
                case YamlTokenType.Number:
                    writer.WriteStringValueBlockPlain(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                    break;
                case YamlTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;
                case YamlTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;
                default:
                    Debug.Assert(reader.TokenType == YamlTokenType.Null);
                    writer.WriteNullValue();
                    break;
            }
        }

        private static bool ReadSkippingComments(ref Utf8YamlReader reader)
        {
            bool read;
            do
            {
                read = reader.Read();
            }
            while (read && reader.TokenType == YamlTokenType.Comment);
            return read;
        }

        private static string AliasName(ref Utf8YamlReader reader) =>
            YamlReaderHelper.TranscodeHelper(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);

        /// <summary>Whether the alias key on the current token names a collection (a peek, not counted against the bound).</summary>
        private static bool AnchorsACollection(ref Utf8YamlReader reader, YamlSerializerOptions? options, long streamPosition)
        {
            YamlAnchorEntry entry = Resolve(ref reader, out YamlAnchorTable table);
            if (entry.FixedLength > 0)
            {
                return false;
            }

            Utf8YamlReader peek = CreateReplayReader(entry, table, ref reader, options, streamPosition, count: false);
            return peek.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray;
        }

        /// <summary>The text of the anchored scalar an alias key (<c>*k : v</c>) names; an alias to a collection is a complex key.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string ReadAliasKeyText(ref Utf8YamlReader reader, YamlSerializerOptions? options, long streamPosition = 0)
        {
            Debug.Assert(reader.KeyKind == YamlKeyKind.Alias);
            YamlAnchorEntry entry = Resolve(ref reader, out YamlAnchorTable table);
            Utf8YamlReader replay = CreateReplayReader(entry, table, ref reader, options, streamPosition);
            if (replay.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                // A collection key is M5e's.
                ThrowHelper.ThrowInvalidOperationException_YamlComplexKey();
            }

            return replay.TokenType == YamlTokenType.Null ? string.Empty : replay.GetString()!;
        }

        /// <summary>The UTF-8 member name an alias key names.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static byte[] ReadAliasPropertyName(ref Utf8YamlReader reader, YamlSerializerOptions options, long streamPosition) =>
            Encoding.UTF8.GetBytes(ReadAliasKeyText(ref reader, options, streamPosition));

        /// <summary>A dictionary key read from an alias key, through the key converter as for any key.</summary>
        internal static TKey ReadAliasKey<TKey>(YamlConverter<TKey> keyConverter, ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            if (AnchorsACollection(ref reader, options, state.StreamPosition))
            {
                // M5e (review 3): an alias key to a collection follows M5b's rule for alias values: at a site that
                // can share, the anchored instance (so the same node twice as a key is a repeated key); elsewhere a replay.
                state.Current.YamlPropertyNameAsString = "*" + YamlReaderHelper.TranscodeHelper(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                if (!YamlComplexKeys.CanRead(keyConverter))
                {
                    ThrowHelper.ThrowYamlException_ComplexKeyNotReadable(state.Current.YamlPropertyNameAsString, typeof(TKey));
                }

                return ReadAlias(keyConverter, ref reader, typeof(TKey), options, ref state)!;
            }

            string text = ReadAliasKeyText(ref reader, options, state.StreamPosition);
            state.Current.YamlPropertyNameAsString = text;
            if (keyConverter.IsInternalConverter && keyConverter.Type == typeof(string))
            {
                return (TKey)(object)text;
            }

            // The key converter reads a property name token: one made of the text, as a double-quoted flow key.
            var builder = new StringBuilder(text.Length + 8).Append("{\"");
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    default:
                        if (ch < ' ')
                        {
                            builder.Append("\\u").Append(((int)ch).ToString("X4", Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(ch);
                        }

                        break;
                }
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(builder.Append("\": ~}").ToString());
            var keyReader = new Utf8YamlReader(utf8, isFinalBlock: true, new YamlReaderState(reader.CurrentState.Options));
            keyReader.Read();
            keyReader.Read();
            Debug.Assert(keyReader.TokenType == YamlTokenType.PropertyName);
            return keyConverter.ReadAsPropertyNameCore(ref keyReader, keyConverter.Type, options);
        }

        /// <summary>Counts <paramref name="nodes"/> copied by an alias against the operation's bound.</summary>
        internal static void Count(YamlAnchorTable table, int nodes, YamlSerializerOptions? options, long streamPosition = 0)
        {
            table.Expanded += nodes;

            // The furthest position as a source offset: a table a stream's reader made in its current buffer still has
            // positions relative to that buffer, whose own offset is the stream position.
            long furthest = table.Furthest + (table.Unrebased ? streamPosition : 0);
            int limit = options is { MaxAliasExpansion: > 0 }
                ? options.MaxAliasExpansion
                : (int)Math.Min(int.MaxValue, Math.Max(DefaultExpansionFloor, furthest));
            if (table.Expanded > limit)
            {
                ThrowHelper.ThrowYamlException_SerializerAliasExpansionLimit(limit);
            }
        }

        private static bool IsPreserve(YamlSerializerOptions options) =>
            options.ReferenceHandlingStrategy is YamlKnownReferenceHandler.Preserve or YamlKnownReferenceHandler.PreserveAll;

        [DoesNotReturn]
        private static void ThrowRecursive(YamlAnchorEntry entry, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options.ReferenceHandlingStrategy is YamlKnownReferenceHandler.Preserve or YamlKnownReferenceHandler.PreserveAll)
            {
                ThrowHelper.ThrowYamlException_RecursiveAliasNoInstance(entry.Name, typeToConvert);
            }

            ThrowHelper.ThrowYamlException_RecursiveAliasNeedsPreserve(entry.Name);
        }
    }
}
