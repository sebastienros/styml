// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// How a polymorphic type writes which derived type a value is (M5d).
    /// Reading accepts the <c>$type</c> property under every style, and a tag under <see cref="Tag"/> and
    /// <see cref="Both"/>.
    /// </summary>
#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    enum YamlTypeDiscriminatorStyle
    {
        /// <summary>The <c>$type</c> property (or the configured name), as System.Text.Json. The default.</summary>
        Property = 0,

        /// <summary>
        /// A YAML tag on the node (<c>!dog</c>): the derived type's <c>Tag</c>, or <c>!</c> followed by its type
        /// discriminator. A polymorphic collection is a tagged sequence, without the <c>$type</c>/<c>$values</c>
        /// wrapper. The JSON-compatible writer, which cannot write a tag, writes the property.
        /// </summary>
        Tag = 1,

        /// <summary>The tag and the property, both; on read, a tag and a property that select different types are an
        /// error.</summary>
        Both = 2,
    }
}
