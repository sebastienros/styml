// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// What reading does with a mapping key that appears twice (M5c).
    /// Keys are compared as their consumer holds them: by decoded text in a <see cref="YamlDocument"/>, a node, an
    /// <see cref="object"/> or a string-keyed dictionary; by the converted key in a typed dictionary; and by the member
    /// they match in a typed object. A repeated key that no member maps, or one inside a value that is skipped, is not
    /// judged.
    /// </summary>
    public enum YamlDuplicateKeyHandling
    {
        /// <summary>A repeated key is an error, as YAML 1.2 requires. The default under the YAML
        /// grammar.</summary>
        Error = 0,

        /// <summary>The last occurrence wins: each occurrence is applied in order, as System.Text.Json and YamlDotNet do.</summary>
        LastWins = 1,

        /// <summary>The first occurrence wins; a later one is read for syntax and skipped, as SharpYaml's
        /// <c>FirstWins</c>.</summary>
        FirstWins = 2,
    }
}
