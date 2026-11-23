// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Indicates that the annotated member must bind to a YAML property on deserialization.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> token in YAML will not trigger a validation error.
    /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
    /// this attribute will be mapped to <see cref="YamlPropertyInfo.IsRequired"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class YamlRequiredAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlRequiredAttribute"/>.
        /// </summary>
        public YamlRequiredAttribute() { }
    }
}
