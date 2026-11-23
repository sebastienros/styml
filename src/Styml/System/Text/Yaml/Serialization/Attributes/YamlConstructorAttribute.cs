// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a constructor, indicates that the constructor should be used to create
    /// instances of the type on deserialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class YamlConstructorAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlConstructorAttribute"/>.
        /// </summary>
        public YamlConstructorAttribute() { }
    }
}
