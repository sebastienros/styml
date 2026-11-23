// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a type, property, or field, indicates what <see cref="YamlNumberHandling"/>
    /// settings should be used when serializing or deserializing numbers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class YamlNumberHandlingAttribute : YamlAttribute
    {
        /// <summary>
        /// Indicates what settings should be used when serializing or deserializing numbers.
        /// </summary>
        public YamlNumberHandling Handling { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="YamlNumberHandlingAttribute"/>.
        /// </summary>
        public YamlNumberHandlingAttribute(YamlNumberHandling handling)
        {
            if (!YamlSerializer.IsValidNumberHandlingValue(handling))
            {
                throw new ArgumentOutOfRangeException(nameof(handling));
            }
            Handling = handling;
        }
    }
}
