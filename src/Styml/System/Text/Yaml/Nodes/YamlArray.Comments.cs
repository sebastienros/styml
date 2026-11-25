// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Nodes
{
    public sealed partial class YamlArray
    {
        // The comments of the items, kept in step with them (null when no item has any), and of the collection's start
        // and end. A node made from a document read with CommentHandling.Allow takes them from the document when it
        // materializes; until then the document holds them, and writes them with the element.
        private List<SlotComments?>? _itemComments;
        private string? _startComment;
        private string? _endComment;
        private bool _collectionCommentsCaptured;

        /// <summary>
        ///   Gets or sets the comment written on the lines after this sequence's start, above its first item, in block
        ///   style; <see langword="null"/> for none. Lines are separated by <c>\n</c>.
        /// </summary>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has the comments above its
        ///   first item; the root's are the comments at the top of the file.
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
                _ = List; // the element, which would write its own comments, is dropped
                _startComment = value;
            }
        }

        /// <summary>
        ///   Gets or sets the comment written after this sequence's last item, in block style; <see langword="null"/> for
        ///   none. Lines are separated by <c>\n</c>.
        /// </summary>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has the comments after its
        ///   last item; the root's are the comments at the bottom of the file.
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
                _ = List;
                _endComment = value;
            }
        }

        /// <summary>Gets the comment of the item at <paramref name="index"/> at <paramref name="position"/>.</summary>
        /// <param name="index">The item's index.</param>
        /// <param name="position">Above the item, or at the end of its line.</param>
        /// <returns>The comment, lines separated by <c>\n</c>, or <see langword="null"/>.</returns>
        /// <remarks>
        ///   A node parsed from a document read with <see cref="YamlCommentHandling.Allow"/> has its source's comments.
        ///   An item keeps its comments when it is replaced, and loses them when it is removed; items inserted before it
        ///   move them with it.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item's index, or
        ///   <paramref name="position"/> is not a defined value.</exception>
        public string? GetComment(int index, YamlCommentPosition position)
        {
            ValidatePosition(position);

            if (_list is null && _yamlElement is { } element)
            {
                // Read from the document, so that reading a comment does not materialize the node.
                if ((uint)index >= (uint)element.GetArrayLength())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return NoComments || !element.HasComments ? null : SlotComments.From(element[index], property: false)?.Get(position);
            }

            List<YamlNode?> items = List;
            if ((uint)index >= (uint)items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _itemComments is { } comments && index < comments.Count ? comments[index]?.Get(position) : null;
        }

        /// <summary>Sets the comment of the item at <paramref name="index"/> at <paramref name="position"/>.</summary>
        /// <param name="index">The item's index.</param>
        /// <param name="position">Above the item, or at the end of its line.</param>
        /// <param name="comment">The comment, lines separated by <c>\n</c> (one line at <see cref="YamlCommentPosition.Trailing"/>),
        ///   or <see langword="null"/> to remove it.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an item's index, or
        ///   <paramref name="position"/> is not a defined value.</exception>
        /// <exception cref="ArgumentException">The comment holds a character a YAML comment cannot hold, or a trailing
        ///   comment holds a line break.</exception>
        public void SetComment(int index, YamlCommentPosition position, string? comment)
        {
            ValidatePosition(position);
            ValidateComment(comment, singleLine: position == YamlCommentPosition.Trailing, nameof(comment));

            List<YamlNode?> items = List;
            if ((uint)index >= (uint)items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_itemComments is null)
            {
                if (comment is null)
                {
                    return;
                }

                _itemComments = new List<SlotComments?>(new SlotComments?[items.Count]);
            }

            while (_itemComments.Count < items.Count)
            {
                _itemComments.Add(null);
            }

            SlotComments comments = _itemComments[index] ??= new SlotComments();
            comments.Set(position, comment);
            if (comments.IsEmpty)
            {
                _itemComments[index] = null;
            }
        }

        internal override bool WritesBelowItsKey =>
            CollectionStyle == YamlCollectionStyle.Block
            && (_list is { } list ? list.Count > 0 : _yamlElement is { } element && element.GetArrayLength() > 0);

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

        private static void ValidatePosition(YamlCommentPosition position)
        {
            if (position is not (YamlCommentPosition.Leading or YamlCommentPosition.Trailing))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
        }
    }
}
