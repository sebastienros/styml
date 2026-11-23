// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// This enum defines the various ways the <see cref="Utf8YamlReader"/> can deal with comments.
    /// </summary>
    public enum YamlCommentHandling : byte
    {
        /// <summary>
        /// The default: YAML <c>#</c> comments are skipped, as with <see cref="Skip"/> (decision D3). The JSON grammar,
        /// where this value rejected comments, was deleted in M7a.
        /// </summary>
        Disallow = 0,
        /// <summary>
        /// Skip <c>#</c> comments: the <see cref="Utf8YamlReader"/> behaves as if no comments were present.
        /// </summary>
        Skip = 1,
        /// <summary>
        /// Return <c>#</c> comments as <see cref="YamlTokenType.Comment"/> tokens, so the caller can read their text.
        /// </summary>
        Allow = 2,
    }
}
