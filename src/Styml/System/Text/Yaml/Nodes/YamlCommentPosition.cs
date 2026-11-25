// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// Where a comment of an entry of a <see cref="Nodes.YamlObject"/> or an item of a <see cref="Nodes.YamlArray"/> is
    /// written in block style.
    /// </summary>
    public enum YamlCommentPosition
    {
        /// <summary>On the lines above the entry or the item, one <c>#</c> line per line of text.</summary>
        Leading = 0,

        /// <summary>
        /// At the end of the entry's line (<c>key: value # text</c>), on one line. When the value is a block collection
        /// that is not empty, the comment follows the key, on the line below it.
        /// </summary>
        Trailing = 1,
    }
}
