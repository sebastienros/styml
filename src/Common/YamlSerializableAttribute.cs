// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !BUILDING_SOURCE_GENERATOR
using System.Text.Yaml.Serialization.Metadata;
#endif

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Instructs the System.Text.Yaml source generator to generate source code to help optimize performance
    /// when serializing and deserializing instances of the specified type and types in its object graph.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]

#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class YamlSerializableAttribute : YamlAttribute
    {
#pragma warning disable IDE0060
        /// <summary>
        /// Initializes a new instance of <see cref="YamlSerializableAttribute"/> with the specified type.
        /// </summary>
        /// <param name="type">The type to generate source code for.</param>
        public YamlSerializableAttribute(Type type) { }
#pragma warning restore IDE0060

        /// <summary>
        /// The name of the property for the generated <see cref="YamlTypeInfo{T}"/> for
        /// the type on the generated, derived <see cref="YamlSerializerContext"/> type.
        /// </summary>
        /// <remarks>
        /// Useful to resolve a name collision with another type in the compilation closure.
        /// </remarks>
        public string? TypeInfoPropertyName { get; set; }

        /// <summary>
        /// Determines what the source generator should generate for the type. If the value is <see cref="YamlSourceGenerationMode.Default"/>,
        /// then the setting specified on <see cref="YamlSourceGenerationOptionsAttribute.GenerationMode"/> will be used.
        /// </summary>
        public YamlSourceGenerationMode GenerationMode { get; set; }
    }
}
