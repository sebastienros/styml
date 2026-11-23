// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// What the current <see cref="YamlTokenType.PropertyName"/> token of a <see cref="Utf8YamlReader"/> stands for.
    /// </summary>
    public enum YamlKeyKind : byte
    {
        /// <summary>The current token is not a mapping key.</summary>
        None = 0,

        /// <summary>A scalar key: <see cref="Utf8YamlReader.ValueSpan"/> holds its text, read with the string accessors.</summary>
        Scalar = 1,

        /// <summary>
        /// An alias key (<c>*name : value</c>): <see cref="Utf8YamlReader.ValueSpan"/> holds the anchor name, which is
        /// not the key's text, so the string accessors throw <see cref="InvalidOperationException"/>.
        /// </summary>
        Alias = 2,

        /// <summary>
        /// A collection used as a key (<c>? [a, b]</c>). Reserved: the reader does not produce it yet, and rejects such
        /// keys instead.
        /// </summary>
        Complex = 3,
    }
}
