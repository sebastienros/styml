// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Writes a comment above the member in block style, one <c># line</c> per line of <see cref="Comment"/>, as
    /// YamlDotNet's <c>[YamlMember(Description = …)]</c>. The comment is
    /// written for a null member too, and not for a member left out by an ignore condition; it is dropped in
    /// JSON-compatible style, and comments are skipped when reading.
    /// </summary>
    /// <remarks>
    /// The text holds printable characters and tabs; its lines are separated by CR, LF or CRLF. Another line break (NEL,
    /// LS, PS) or a control character is an <see cref="InvalidOperationException"/> when the contract is configured, since
    /// a reader could take what follows it as content.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class YamlCommentAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlCommentAttribute"/> with the comment's text.
        /// </summary>
        /// <param name="comment">The comment's text.</param>
        public YamlCommentAttribute(string comment)
        {
            Comment = comment;
        }

        /// <summary>
        /// The comment's text.
        /// </summary>
        public string Comment { get; }
    }
}
