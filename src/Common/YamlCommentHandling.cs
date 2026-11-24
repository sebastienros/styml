// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// This enum defines the various ways the <see cref="Utf8YamlReader"/> can deal with comments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// YAML's <c>#</c> comments are always read: skipped with <see cref="Disallow"/> and <see cref="Skip"/>, returned
    /// as tokens with <see cref="Allow"/>.
    /// </para>
    /// <para>
    /// JSON comments (<c>//</c> to the end of the line, and <c>/* */</c>) are not YAML. They are read with
    /// <see cref="Skip"/> and <see cref="Allow"/>, as System.Text.Json reads them under the same values, in a JSON
    /// payload: inside flow collections (<c>[ ]</c> and <c>{ }</c>) and at the document level, before and after
    /// the root node. They are never read inside a block collection, where <c>url: http://x // y</c> keeps its text.
    /// A comment starts where a token may start, after white space, or directly after a JSON number,
    /// <c>true</c>, <c>false</c> or <c>null</c> (<c>[1//c</c>), as in JSON. Other text keeps its slashes:
    /// <c>[http://x, a//b]</c> holds two scalars, as <c>a#b</c> is a scalar in YAML.
    /// </para>
    /// </remarks>
    public enum YamlCommentHandling : byte
    {
        /// <summary>
        /// The default, standard YAML: <c>#</c> comments are skipped, and <c>//</c> and <c>/* */</c> are not
        /// comments. They are read as YAML reads them: text in a plain scalar (<c>{"a": 1 // c}</c> holds the string
        /// <c>1 // c</c>), or an error where no scalar may stand. System.Text.Json rejects comments under this value.
        /// </summary>
        Disallow = 0,
        /// <summary>
        /// Skip comments: <c>#</c> comments, and JSON comments in a JSON payload. The <see cref="Utf8YamlReader"/>
        /// behaves as if no comments were present.
        /// </summary>
        Skip = 1,
        /// <summary>
        /// Return comments as <see cref="YamlTokenType.Comment"/> tokens, so the caller can read their text:
        /// <c>#</c> comments, and JSON comments in a JSON payload. The token's value is the text after <c>#</c> or
        /// <c>//</c>, or between <c>/*</c> and <c>*/</c>.
        /// </summary>
        Allow = 2,
    }
}
