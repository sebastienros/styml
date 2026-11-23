// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// Determines the naming policy used to convert a string-based name to another format, such as a camel-casing format.
    /// </summary>
#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    abstract class YamlNamingPolicy
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlNamingPolicy"/>.
        /// </summary>
        protected YamlNamingPolicy() { }

        /// <summary>
        /// Returns the naming policy for camel-casing.
        /// </summary>
        public static YamlNamingPolicy CamelCase { get; } = new YamlCamelCaseNamingPolicy();

        /// <summary>
        /// Returns the naming policy for lower snake-casing.
        /// </summary>
        public static YamlNamingPolicy SnakeCaseLower { get; } = new YamlSnakeCaseLowerNamingPolicy();

        /// <summary>
        /// Returns the naming policy for upper snake-casing.
        /// </summary>
        public static YamlNamingPolicy SnakeCaseUpper { get; } = new YamlSnakeCaseUpperNamingPolicy();

        /// <summary>
        /// Returns the naming policy for lower kebab-casing.
        /// </summary>
        public static YamlNamingPolicy KebabCaseLower { get; } = new YamlKebabCaseLowerNamingPolicy();

        /// <summary>
        /// Returns the naming policy for upper kebab-casing.
        /// </summary>
        public static YamlNamingPolicy KebabCaseUpper { get; } = new YamlKebabCaseUpperNamingPolicy();

        /// <summary>
        /// Returns the naming policy for PascalCasing.
        /// </summary>
        public static YamlNamingPolicy PascalCase { get; } = new YamlPascalCaseNamingPolicy();

        /// <summary>
        /// When overridden in a derived class, converts the specified name according to the policy.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>The converted name.</returns>
        public abstract string ConvertName(string name);
    }
}
