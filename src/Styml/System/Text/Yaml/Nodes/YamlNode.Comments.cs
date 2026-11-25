// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Nodes
{
    public abstract partial class YamlNode
    {
        /// <summary>
        /// A copy made through an alias, or a node inside one: it keeps none of the comments of the node the alias names,
        /// which the anchored node writes already.
        /// </summary>
        private bool _noComments;

        internal bool NoComments
        {
            get => _noComments;
            set => _noComments = value;
        }

        /// <summary>Whether a block writer writes this node on the lines below its key: a block collection with entries.</summary>
        internal virtual bool WritesBelowItsKey => false;

        /// <summary>Writes the element this node was made from, with its comments unless the node is a copy.</summary>
        private protected void WriteElementTo(YamlElement element, Utf8YamlWriter writer)
        {
            if (_noComments)
            {
                element.WriteToWithoutComments(writer);
            }
            else
            {
                element.WriteTo(writer);
            }
        }

        /// <summary>
        /// A collection's comments, allocated only when it has some, so that a node without comments carries one null
        /// reference: its start and end, and its entries' (by name) or items' (in step with them).
        /// </summary>
        internal sealed class CollectionComments
        {
            internal string? Start;
            internal string? End;
            internal Dictionary<string, SlotComments>? Slots;
            internal List<SlotComments?>? Items;

            /// <summary>The start and end were taken from the element.</summary>
            internal bool Captured;
        }

        /// <summary>The comments of an entry or an item: above it, and at the end of its line.</summary>
        internal sealed class SlotComments
        {
            internal string? Leading;
            internal string? Trailing;

            /// <summary>The trailing comment follows the key, as read from <c>key: # text</c>.</summary>
            internal bool TrailingAfterKey;

            internal bool IsEmpty => Leading is null && Trailing is null;

            internal SlotComments Copy() => new() { Leading = Leading, Trailing = Trailing, TrailingAfterKey = TrailingAfterKey };

            /// <summary>The slot comments of a property's or an item's element, or null when it has none.</summary>
            internal static SlotComments? From(YamlElement element, bool property)
            {
                element.GetSlotComments(property, out string? leading, out string? trailing, out bool afterKey);
                return leading is null && trailing is null ? null : new SlotComments { Leading = leading, Trailing = trailing, TrailingAfterKey = afterKey };
            }

            internal string? Get(YamlCommentPosition position) => position == YamlCommentPosition.Leading ? Leading : Trailing;

            internal void Set(YamlCommentPosition position, string? comment)
            {
                if (position == YamlCommentPosition.Leading)
                {
                    Leading = comment;
                }
                else
                {
                    Trailing = comment;
                    TrailingAfterKey = false;
                }
            }

            /// <summary>Before the key or the item.</summary>
            internal void WriteLeading(Utf8YamlWriter writer, YamlNode? value, bool item)
            {
                if (Leading is not null)
                {
                    writer.WriteCommentValue(Leading, endOfLine: false);
                }

                // A sequence item has no key to follow: a trailing comment that can't end the item's line goes above it.
                if (item && Trailing is not null && value is { WritesBelowItsKey: true })
                {
                    writer.WriteCommentValue(Trailing, endOfLine: false);
                }
            }

            /// <summary>After the key, for a value written on the lines below it.</summary>
            internal void WriteAfterKey(Utf8YamlWriter writer, YamlNode? value)
            {
                if (Trailing is not null && (TrailingAfterKey || value is { WritesBelowItsKey: true }))
                {
                    writer.WriteCommentValue(Trailing, endOfLine: true);
                }
            }

            /// <summary>After the value, at the end of its line.</summary>
            internal void WriteAfterValue(Utf8YamlWriter writer, YamlNode? value, bool item)
            {
                if (Trailing is not null && !(item ? value is { WritesBelowItsKey: true } : TrailingAfterKey || value is { WritesBelowItsKey: true }))
                {
                    writer.WriteCommentValue(Trailing, endOfLine: true);
                }
            }
        }

        /// <summary>Checks a comment set on a node: text a block comment can hold, and one line for a trailing one.</summary>
        internal static void ValidateComment(string? comment, bool singleLine, string paramName)
        {
            if (comment is null)
            {
                return;
            }

            if ((singleLine && comment.AsSpan().IndexOfAny('\r', '\n') >= 0) || !YamlWriterHelper.IsValidBlockComment(Encoding.UTF8.GetBytes(comment)))
            {
                ThrowHelper.ThrowArgumentException_InvalidNodeComment(paramName);
            }
        }

        /// <summary>A comment on its own lines, in block style only.</summary>
        internal static void WriteOwnLineComment(Utf8YamlWriter writer, string? comment)
        {
            if (comment is not null && writer.IsBlockStyle)
            {
                writer.WriteCommentValue(comment, endOfLine: false);
            }
        }
    }
}
