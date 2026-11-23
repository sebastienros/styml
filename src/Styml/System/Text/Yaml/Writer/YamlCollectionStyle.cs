// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// The style <see cref="Utf8YamlWriter.WriteCollectionStyle"/> requests for the next collection a block-style writer
    /// starts.
    /// </summary>
    public enum YamlCollectionStyle : byte
    {
        /// <summary>A block collection: one entry per line (<c>- a</c>, <c>key: value</c>). The default.</summary>
        Block = 0,

        /// <summary>
        /// A flow collection on one line (<c>[a, b]</c>, <c>{key: value}</c>). Everything inside a flow collection is flow.
        /// </summary>
        Flow = 1,
    }
}
