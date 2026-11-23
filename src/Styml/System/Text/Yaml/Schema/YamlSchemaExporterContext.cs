// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Schema
{
    /// <summary>
    /// Defines the context for the generated JSON schema for a particular node in a type graph.
    /// </summary>
    public readonly struct YamlSchemaExporterContext
    {
        internal readonly string[] _path;

        internal YamlSchemaExporterContext(
            YamlTypeInfo typeInfo,
            YamlPropertyInfo? propertyInfo,
            YamlTypeInfo? baseTypeInfo,
            string[] path)
        {
            TypeInfo = typeInfo;
            PropertyInfo = propertyInfo;
            BaseTypeInfo = baseTypeInfo;
            _path = path;
        }

        /// <summary>
        /// The <see cref="YamlTypeInfo"/> for the type being processed.
        /// </summary>
        public YamlTypeInfo TypeInfo { get; }

        /// <summary>
        /// The <see cref="YamlPropertyInfo"/> if the schema is being generated for a property.
        /// </summary>
        public YamlPropertyInfo? PropertyInfo { get; }

        /// <summary>
        /// Gets the <see cref="YamlTypeInfo"/> for polymorphic base type if the schema is being generated for a derived type.
        /// </summary>
        public YamlTypeInfo? BaseTypeInfo { get; }

        /// <summary>
        /// The path to the current node in the generated JSON schema.
        /// </summary>
        public ReadOnlySpan<string> Path => _path;
    }
}
