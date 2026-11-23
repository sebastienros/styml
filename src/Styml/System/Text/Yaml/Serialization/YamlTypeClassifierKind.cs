// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Identifies the type of metadata being configured for a <see cref="YamlTypeClassifierFactory"/>.
    /// </summary>
    public enum YamlTypeClassifierKind
    {
        /// <summary>
        /// No classifier kind is specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The classifier is being created for a union type.
        /// </summary>
        Union = 1,

        /// <summary>
        /// The classifier is being created for a polymorphic type.
        /// </summary>
        PolymorphicType = 2,
    }
}
