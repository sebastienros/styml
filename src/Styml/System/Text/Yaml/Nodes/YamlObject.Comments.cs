// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Nodes
{
    public sealed partial class YamlObject
    {
        // The comments of the entries, by property name (the entries' own comparer), and of the collection's start and
        // end. A node made from a document read with CommentHandling.Allow takes them from the document when it
        // materializes; until then the document holds them, and writes them with the element.
        private Dictionary<string, SlotComments>? _slotComments;
        private string? _startComment;
        private string? _endComment;
        private bool _collectionCommentsCaptured;

        /// <summary>
        ///   Gets or sets the comment written on the lines after this mapping's start, above its first entry, in block
        ///   style; <see langword="null"/> for none. Lines are separated by <c>\n</c>.
        /// </summary>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has the comments above its
        ///   first entry; the root's are the comments at the top of the file. They stay when the first entry is removed
        ///   or another is inserted before it.
        /// </remarks>
        /// <exception cref="ArgumentException">The text holds a character a YAML comment cannot hold.</exception>
        public string? StartComment
        {
            get
            {
                EnsureCollectionComments();
                return _startComment;
            }
            set
            {
                ValidateComment(value, singleLine: false, nameof(value));
                EnsureCollectionComments();
                _ = Dictionary; // the element, which would write its own comments, is dropped
                _startComment = value;
            }
        }

        /// <summary>
        ///   Gets or sets the comment written after this mapping's last entry, in block style; <see langword="null"/> for
        ///   none. Lines are separated by <c>\n</c>.
        /// </summary>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has the comments after its
        ///   last entry; the root's are the comments at the bottom of the file.
        /// </remarks>
        /// <exception cref="ArgumentException">The text holds a character a YAML comment cannot hold.</exception>
        public string? EndComment
        {
            get
            {
                EnsureCollectionComments();
                return _endComment;
            }
            set
            {
                ValidateComment(value, singleLine: false, nameof(value));
                EnsureCollectionComments();
                _ = Dictionary;
                _endComment = value;
            }
        }

        /// <summary>Gets the comment of the entry named <paramref name="propertyName"/> at <paramref name="position"/>.</summary>
        /// <param name="propertyName">The entry's property name.</param>
        /// <param name="position">Above the entry, or at the end of its line.</param>
        /// <returns>The comment, lines separated by <c>\n</c>, or <see langword="null"/> when the entry has none or
        ///   there is no such entry.</returns>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has its source's comments.
        ///   An entry keeps its comments when its value is replaced, and loses them when it is removed.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a defined value.</exception>
        public string? GetComment(string propertyName, YamlCommentPosition position)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            ValidatePosition(position);

            if (_dictionary is null && _yamlElement is { } element && !(Options?.PropertyNameCaseInsensitive ?? false))
            {
                // Read from the document, so that reading a comment does not materialize the node.
                if (NoComments || !element.HasComments || !element.TryGetProperty(propertyName, out YamlElement value))
                {
                    return null;
                }

                return SlotComments.From(value, property: true)?.Get(position);
            }

            return Dictionary.ContainsKey(propertyName) && _slotComments is { } slots && slots.TryGetValue(propertyName, out SlotComments? comments)
                ? comments.Get(position)
                : null;
        }

        /// <summary>Sets the comment of the entry named <paramref name="propertyName"/> at <paramref name="position"/>.</summary>
        /// <param name="propertyName">The entry's property name.</param>
        /// <param name="position">Above the entry, or at the end of its line.</param>
        /// <param name="comment">The comment, lines separated by <c>\n</c> (one line at <see cref="YamlCommentPosition.Trailing"/>),
        ///   or <see langword="null"/> to remove it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a defined value.</exception>
        /// <exception cref="ArgumentException">The comment holds a character a YAML comment cannot hold, or a trailing
        ///   comment holds a line break.</exception>
        /// <exception cref="KeyNotFoundException">The object has no entry named <paramref name="propertyName"/>.</exception>
        public void SetComment(string propertyName, YamlCommentPosition position, string? comment)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            ValidatePosition(position);
            ValidateComment(comment, singleLine: position == YamlCommentPosition.Trailing, nameof(comment));

            OrderedDictionary<string, YamlNode?> dictionary = Dictionary;
            int index = dictionary.IndexOf(propertyName);
            if (index < 0)
            {
                ThrowHelper.ThrowKeyNotFoundException_NodeCommentProperty(propertyName);
            }

            string name = dictionary.GetAt(index).Key;
            if (_slotComments is { } slots && slots.TryGetValue(name, out SlotComments? comments))
            {
                comments.Set(position, comment);
                if (comments.IsEmpty)
                {
                    slots.Remove(name);
                }
            }
            else if (comment is not null)
            {
                comments = new SlotComments();
                comments.Set(position, comment);
                (_slotComments ??= CreateSlotComments(Options))[name] = comments;
            }
        }

        internal override bool WritesBelowItsKey =>
            CollectionStyle == YamlCollectionStyle.Block
            && (_dictionary is { } dictionary ? dictionary.Count > 0 || _complexKeys is { Count: > 0 }
                : _yamlElement is { } element && element.EnumerateObject().MoveNext());

        private static Dictionary<string, SlotComments> CreateSlotComments(YamlNodeOptions? options) =>
            new(options?.PropertyNameCaseInsensitive ?? false ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        private void InheritNoComments(YamlNode? child)
        {
            if (NoComments && child is not null)
            {
                child.NoComments = true;
            }
        }

        // Before the element is dropped (the node materializes or is cleared), its start and end comments are kept.
        private void CaptureCollectionComments(YamlElement element)
        {
            if (!_collectionCommentsCaptured)
            {
                if (!NoComments && element.HasComments)
                {
                    element.GetCollectionComments(out _startComment, out _endComment);
                }

                _collectionCommentsCaptured = true;
            }
        }

        private void EnsureCollectionComments()
        {
            if (_yamlElement is { } element)
            {
                CaptureCollectionComments(element);
            }
        }

        // An entry renamed in place keeps its comments.
        private void MoveSlotComments(string oldName, string newName)
        {
            if (_slotComments is { } slots && slots.Comparer.Equals(oldName, newName) is false && slots.TryGetValue(oldName, out SlotComments? comments))
            {
                slots.Remove(oldName);
                slots[newName] = comments;
            }
        }

        private static void ValidatePosition(YamlCommentPosition position)
        {
            if (position is not (YamlCommentPosition.Leading or YamlCommentPosition.Trailing))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
        }
    }
}
