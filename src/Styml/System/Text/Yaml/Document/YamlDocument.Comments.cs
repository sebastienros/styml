// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        // Comments, read under CommentHandling.Allow. They are not rows: the YAML tables hold them, sorted by the row
        // they are attached to, and the YAML write path writes them back in block style. Every comment belongs to a slot
        // (a mapping entry, by its key row, or a sequence item, by its row) or to a collection:
        // - Leading: own-line comments above the slot;
        // - AfterKey: a comment after a key whose value starts on a later line ("key: # c", or "key:\n  # c\n  value");
        // - After: an end-of-line comment after the slot's value (a scalar, an alias, a flow collection's end);
        // - Start: comments before a collection's first entry (at the top of the file, the root collection's);
        // - End: comments after a collection's last entry, at or right of its column (at the bottom, the root's).
        // A scalar root, or a flow root, keeps the document's first comments as its Leading and the last as its After.
        // Writing an element writes the comments inside it: its own slot's Leading, AfterKey and After are written by the
        // container that writes the slot, except for the root, which writes the document's.

        internal enum CommentPlacement : byte
        {
            Leading,
            AfterKey,
            Start,
            After,
            End,
        }

        internal struct CommentEntry
        {
            internal int Row;
            internal CommentPlacement Placement;
            internal bool OwnLine;
            internal string Text;
        }

        /// <summary>Collects the comments of one parse (the document's reader, not a collection key's).</summary>
        internal sealed class CommentCollector
        {
            private enum Previous : byte
            {
                None,
                Key,
                Start,
                Value,
            }

            private struct Open
            {
                internal int Row;
                internal int Column;
                internal bool Flow;
            }

            private struct Pending
            {
                internal int Column;
                internal string Text;
            }

            private readonly List<CommentEntry> _entries = new();
            private readonly List<Open> _open = new();
            private readonly List<Pending> _pending = new();
            private Previous _previous;
            private int _previousRow;
            private int _previousEnd;

            /// <summary>A comment token: attached now when it ends a line with content, kept pending otherwise.</summary>
            internal void OnComment(ReadOnlySpan<byte> source, ref Utf8YamlReader reader)
            {
                int start = (int)reader.TokenStartIndex;
                string text = CommentText(source, start, reader.ValueSpan);
                bool ownLine = _previous == Previous.None
                    || (_previousEnd > 0 && source[_previousEnd - 1] == (byte)'\n')
                    || source.Slice(_previousEnd, Math.Max(0, start - _previousEnd)).IndexOf((byte)'\n') >= 0;
                if (!ownLine)
                {
                    CommentPlacement placement = _previous switch
                    {
                        Previous.Key => CommentPlacement.AfterKey,
                        Previous.Start => CommentPlacement.Start,
                        _ => CommentPlacement.After,
                    };
                    Add(_previousRow, placement, ownLine: false, text);
                }
                else
                {
                    _pending.Add(new Pending { Column = ColumnOf(source, start), Text = text });
                }

                _previousEnd = (int)reader.BytesConsumed;
            }

            /// <summary>A token that gets a row (<paramref name="row"/>, for all but an end token, its row).</summary>
            internal void OnToken(ReadOnlySpan<byte> source, ref Utf8YamlReader reader, YamlTokenType tokenType, int row)
            {
                if (tokenType is YamlTokenType.EndObject or YamlTokenType.EndArray)
                {
                    Open open = _open[_open.Count - 1];
                    _open.RemoveAt(_open.Count - 1);
                    if (_pending.Count > 0)
                    {
                        // A block collection's end token comes after the comments that follow its last entry, dedented
                        // ones included: those at or right of its column are its own, the others its parent's. An
                        // indentless sequence shares its parent mapping's column, which takes the comments at it.
                        int taken = 0;
                        if (open.Flow)
                        {
                            taken = _pending.Count;
                        }
                        else
                        {
                            bool parentAtSameColumn = _open.Count > 0 && _open[_open.Count - 1].Column == open.Column && !_open[_open.Count - 1].Flow;
                            while (taken < _pending.Count
                                && (_pending[taken].Column > open.Column || (_pending[taken].Column == open.Column && !parentAtSameColumn)))
                            {
                                taken++;
                            }
                        }

                        for (int i = 0; i < taken; i++)
                        {
                            Add(open.Row, CommentPlacement.End, ownLine: true, _pending[i].Text);
                        }

                        _pending.RemoveRange(0, taken);
                    }

                    _previous = Previous.Value;
                    _previousRow = open.Row;
                    _previousEnd = (int)reader.BytesConsumed;
                    return;
                }

                bool blockStart = tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray && !reader.IsFlow;
                if (_pending.Count > 0)
                {
                    (int target, CommentPlacement placement) = _previous switch
                    {
                        Previous.Start => (_previousRow, CommentPlacement.Start),
                        Previous.Key or Previous.None when blockStart => (row, CommentPlacement.Start),
                        Previous.Key => (_previousRow, CommentPlacement.AfterKey),
                        _ => (row, CommentPlacement.Leading),
                    };
                    foreach (Pending pending in _pending)
                    {
                        Add(target, placement, ownLine: true, pending.Text);
                    }

                    _pending.Clear();
                }

                if (tokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
                {
                    _open.Add(new Open { Row = row, Column = ColumnOf(source, (int)reader.TokenStartIndex), Flow = reader.IsFlow });
                    _previous = Previous.Start;
                }
                else
                {
                    _previous = tokenType == YamlTokenType.PropertyName ? Previous.Key : Previous.Value;
                }

                _previousRow = row;
                _previousEnd = (int)reader.BytesConsumed;
            }

            /// <summary>Stores the comments in <paramref name="tables"/>, the ones left after the root as its After.</summary>
            internal void Complete(ref YamlTables? tables, int rowCount)
            {
                if (rowCount > 0)
                {
                    foreach (Pending pending in _pending)
                    {
                        Add(0, CommentPlacement.After, ownLine: true, pending.Text);
                    }
                }

                if (_entries.Count == 0)
                {
                    return;
                }

                // Stable by row: entries were added in source order, and a collection's Start and End come later than
                // its first rows.
                CommentEntry[] sorted = _entries.ToArray();
                int[] order = new int[sorted.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }

                Array.Sort(order, (a, b) => sorted[a].Row != sorted[b].Row ? sorted[a].Row.CompareTo(sorted[b].Row) : a.CompareTo(b));
                var comments = new CommentEntry[sorted.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    comments[i] = sorted[order[i]];
                }

                tables ??= new YamlTables();
                tables.Comments = comments;
            }

            private void Add(int row, CommentPlacement placement, bool ownLine, string text) =>
                _entries.Add(new CommentEntry { Row = row, Placement = placement, OwnLine = ownLine, Text = text });

            private static int ColumnOf(ReadOnlySpan<byte> source, int index) =>
                index - (source.Slice(0, index).LastIndexOf((byte)'\n') + 1);

            // The text after '#', less the one space that usually follows it, so that the writer's "# " gives it back.
            // A JSON comment (// or /* */) loses the spaces around its text.
            private static string CommentText(ReadOnlySpan<byte> source, int start, ReadOnlySpan<byte> value)
            {
                string text = YamlReaderHelper.TranscodeHelper(value);
                if (start < source.Length && source[start] == (byte)'/')
                {
                    return text.Trim(' ', '\t');
                }

                return text.Length > 0 && text[0] == ' ' ? text.Substring(1) : text;
            }
        }

        /// <summary>The document has comments (it was read with CommentHandling.Allow and had some).</summary>
        internal bool HasComments => _yaml is { Comments: not null };

        /// <summary>
        /// Writes the comments attached to <paramref name="row"/> at <paramref name="placement"/>, own-line ones on their
        /// own lines and the others at the end of the current line where the writer can put them. A JSON-compatible
        /// writer writes none: they are YAML comments.
        /// </summary>
        private void WriteComments(int row, CommentPlacement placement, Utf8YamlWriter writer)
        {
            CommentEntry[]? comments = _yaml?.Comments;
            if (comments is null || !writer.IsBlockStyle)
            {
                return;
            }

            for (int i = FirstComment(comments, row); i < comments.Length && comments[i].Row == row; i++)
            {
                if (comments[i].Placement == placement)
                {
                    writer.WriteCommentValue(comments[i].Text, endOfLine: !comments[i].OwnLine);
                }
            }
        }

        /// <summary>The comments of the slot of the property whose value is at <paramref name="valueIndex"/>.</summary>
        internal void WritePropertyComments(int valueIndex, CommentPlacement placement, Utf8YamlWriter writer)
        {
            CheckNotDisposed();
            if (_yaml?.Comments is null)
            {
                return;
            }

            WriteComments(placement == CommentPlacement.After ? valueIndex : NameRowOf(valueIndex), placement, writer);
        }

        /// <summary>The comments at <paramref name="row"/> and <paramref name="placement"/>, one per line, or null.</summary>
        private string? GetCommentText(int row, CommentPlacement placement)
        {
            CommentEntry[]? comments = _yaml?.Comments;
            if (comments is null)
            {
                return null;
            }

            string? text = null;
            for (int i = FirstComment(comments, row); i < comments.Length && comments[i].Row == row; i++)
            {
                if (comments[i].Placement == placement)
                {
                    text = text is null ? comments[i].Text : text + "\n" + comments[i].Text;
                }
            }

            return text;
        }

        private static string? JoinComments(string? first, string? second) =>
            first is null ? second : second is null ? first : first + "\n" + second;

        /// <summary>
        /// The comments of the slot of the value at <paramref name="valueRow"/> (a node tree's entry or item): above it,
        /// and at the end of its line, after its key when it was read there (a key's comment wins over its value's).
        /// </summary>
        internal void GetSlotComments(int valueRow, bool property, out string? leading, out string? trailing, out bool trailingAfterKey)
        {
            CheckNotDisposed();
            int slotRow = property ? NameRowOf(valueRow) : valueRow;
            leading = GetCommentText(slotRow, CommentPlacement.Leading);
            string? afterKey = property ? GetCommentText(slotRow, CommentPlacement.AfterKey) : null;
            trailingAfterKey = afterKey is not null;
            trailing = afterKey ?? GetCommentText(valueRow, CommentPlacement.After);
        }

        /// <summary>
        /// The comments at the start and the end of the collection at <paramref name="index"/>. The root's take the
        /// document's first and last comments, which a scalar or flow root holds as its own slot's.
        /// </summary>
        internal void GetCollectionComments(int index, out string? start, out string? end)
        {
            CheckNotDisposed();
            start = GetCommentText(index, CommentPlacement.Start);
            end = GetCommentText(index, CommentPlacement.End);
            if (index == 0)
            {
                start = JoinComments(GetCommentText(0, CommentPlacement.Leading), start);
                end = JoinComments(end, GetCommentText(0, CommentPlacement.After));
            }
        }

        private static int FirstComment(CommentEntry[] comments, int row)
        {
            int lo = 0;
            int hi = comments.Length;
            while (lo < hi)
            {
                int mid = (int)((uint)(lo + hi) >> 1);
                if (comments[mid].Row < row)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        /// <summary>
        /// The comments inside the element at <paramref name="index"/> (rows up to <paramref name="endIndex"/>),
        /// re-indexed to a copy whose rows start at 0. The element's own slot comments stay behind, unless it is the
        /// root, whose comments are the document's.
        /// </summary>
        private CommentEntry[]? CopyComments(int index, int endIndex)
        {
            CommentEntry[]? comments = _yaml?.Comments;
            if (comments is null)
            {
                return null;
            }

            List<CommentEntry>? copy = null;
            for (int i = FirstComment(comments, index); i < comments.Length && comments[i].Row < endIndex; i++)
            {
                CommentEntry entry = comments[i];
                if (index > 0 && entry.Row == index && entry.Placement is CommentPlacement.Leading or CommentPlacement.After)
                {
                    continue;
                }

                entry.Row -= index;
                (copy ??= new List<CommentEntry>()).Add(entry);
            }

            Debug.Assert(copy is null || copy.Count > 0);
            return copy?.ToArray();
        }
    }
}
