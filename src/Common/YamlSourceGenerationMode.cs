// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The generation mode for the System.Text.Yaml source generator.
    /// </summary>
    [Flags]
    public enum YamlSourceGenerationMode
    {
        /// <summary>
        /// When specified on <see cref="YamlSourceGenerationOptionsAttribute.GenerationMode"/>, indicates that both type-metadata initialization logic
        /// and optimized serialization logic should be generated for all types. When specified on <see cref="YamlSerializableAttribute.GenerationMode"/>,
        /// indicates that the setting on <see cref="YamlSourceGenerationOptionsAttribute.GenerationMode"/> should be used.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Instructs the YAML source generator to generate type-metadata initialization logic.
        /// </summary>
        /// <remarks>
        /// This mode supports all <see cref="YamlSerializer"/> features.
        /// </remarks>
        Metadata = 1,

        /// <summary>
        /// Instructs the YAML source generator to generate optimized serialization logic.
        /// </summary>
        /// <remarks>
        /// This mode supports only a subset of <see cref="YamlSerializer"/> features.
        /// </remarks>
        Serialization = 2
    }
}
