// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// The presentation style of the scalar (or mapping key) that produced the current <see cref="Utf8YamlReader"/> token.
    /// </summary>
    public enum YamlScalarStyle : byte
    {
        /// <summary>The token is not a scalar.</summary>
        None = 0,

        /// <summary>A plain (unquoted) scalar, including an empty node.</summary>
        Plain = 1,

        /// <summary>A single-quoted scalar (<c>'text'</c>).</summary>
        SingleQuoted = 2,

        /// <summary>A double-quoted scalar (<c>"text"</c>).</summary>
        DoubleQuoted = 3,

        /// <summary>A literal block scalar (<c>|</c>).</summary>
        Literal = 4,

        /// <summary>A folded block scalar (<c>&gt;</c>).</summary>
        Folded = 5,
    }
}
