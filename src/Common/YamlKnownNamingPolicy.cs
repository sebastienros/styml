// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The <see cref="Yaml.YamlNamingPolicy"/> to be used at run time.
    /// </summary>
    public enum YamlKnownNamingPolicy
    {
        /// <summary>
        /// Specifies that YAML property names should not be converted.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.CamelCase"/> be used to convert YAML property names.
        /// </summary>
        CamelCase = 1,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.SnakeCaseLower"/> be used to convert YAML property names.
        /// </summary>
        SnakeCaseLower = 2,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.SnakeCaseUpper"/> be used to convert YAML property names.
        /// </summary>
        SnakeCaseUpper = 3,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.KebabCaseLower"/> be used to convert YAML property names.
        /// </summary>
        KebabCaseLower = 4,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.KebabCaseUpper"/> be used to convert YAML property names.
        /// </summary>
        KebabCaseUpper = 5,

        /// <summary>
        /// Specifies that the built-in <see cref="Yaml.YamlNamingPolicy.PascalCase"/> be used to convert YAML property names.
        /// </summary>
        PascalCase = 6
    }
}
