// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Used to resolve the YAML serialization contract for requested types.
    /// </summary>
    public interface IYamlTypeInfoResolver
    {
        /// <summary>
        /// Resolves a <see cref="YamlTypeInfo"/> contract for the requested type and options.
        /// </summary>
        /// <param name="type">Type to be resolved.</param>
        /// <param name="options">Configuration used when resolving the metadata.</param>
        /// <returns>
        /// A <see cref="YamlTypeInfo"/> instance matching the requested type,
        /// or <see langword="null"/> if no contract could be resolved.
        /// </returns>
        YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options);
    }
}
