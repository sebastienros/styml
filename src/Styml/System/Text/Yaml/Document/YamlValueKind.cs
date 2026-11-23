// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    ///   Specifies the data type of a YAML value.
    /// </summary>
    public enum YamlValueKind : byte
    {
        /// <summary>
        ///   Indicates that there is no value (as distinct from <see cref="Null"/>).
        /// </summary>
        Undefined,

        /// <summary>
        ///   Indicates that a value is a YAML object.
        /// </summary>
        Object,

        /// <summary>
        ///   Indicates that a value is a YAML array.
        /// </summary>
        Array,

        /// <summary>
        ///   Indicates that a value is a YAML string.
        /// </summary>
        String,

        /// <summary>
        ///   Indicates that a value is a YAML number.
        /// </summary>
        Number,

        /// <summary>
        ///   Indicates that a value is the YAML value <c>true</c>.
        /// </summary>
        True,

        /// <summary>
        ///   Indicates that a value is the YAML value <c>false</c>.
        /// </summary>
        False,

        /// <summary>
        ///   Indicates that a value is the YAML value <c>null</c>.
        /// </summary>
        Null,
    }
}
