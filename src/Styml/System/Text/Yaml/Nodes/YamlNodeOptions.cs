// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    ///   Options to control <see cref="YamlNode"/> behavior.
    /// </summary>
    public struct YamlNodeOptions
    {
        /// <summary>
        ///   Specifies whether property names on <see cref="YamlObject"/> are case insensitive.
        /// </summary>
        public bool PropertyNameCaseInsensitive { get; set; }
    }
}
