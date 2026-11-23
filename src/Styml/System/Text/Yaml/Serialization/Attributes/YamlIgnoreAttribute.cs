// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Prevents a property or field from being serialized or deserialized.
    /// When placed on a type, specifies the default <see cref="YamlIgnoreCondition"/>
    /// for all properties and fields of the type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class YamlIgnoreAttribute : YamlAttribute
    {
        /// <summary>
        /// Specifies the condition that must be met before a property or field will be ignored.
        /// </summary>
        /// <remarks>The default value is <see cref="YamlIgnoreCondition.Always"/>.</remarks>
        public YamlIgnoreCondition Condition { get; set; } = YamlIgnoreCondition.Always;

        /// <summary>
        /// Initializes a new instance of <see cref="YamlIgnoreAttribute"/>.
        /// </summary>
        public YamlIgnoreAttribute() { }
    }
}
