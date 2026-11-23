// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a type, determines the <see cref="YamlUnmappedMemberHandling"/> configuration
    /// for the specific type, overriding the global <see cref="YamlSerializerOptions.UnmappedMemberHandling"/> setting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    public class YamlUnmappedMemberHandlingAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlUnmappedMemberHandlingAttribute"/>.
        /// </summary>
        /// <param name="unmappedMemberHandling">The handling to apply to the current member.</param>
        public YamlUnmappedMemberHandlingAttribute(YamlUnmappedMemberHandling unmappedMemberHandling)
        {
            UnmappedMemberHandling = unmappedMemberHandling;
        }

        /// <summary>
        /// Specifies the unmapped member handling setting for the attribute.
        /// </summary>
        public YamlUnmappedMemberHandling UnmappedMemberHandling { get; }
    }
}
