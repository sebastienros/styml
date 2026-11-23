// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// The output style of <see cref="Utf8YamlWriter"/>.
    /// </summary>
    public enum YamlWriterStyle : byte
    {
        /// <summary>
        /// Block YAML: block mappings and sequences, plain scalars where no YAML 1.1 or 1.2 reader can read them as
        /// anything other than the same string, double quotes otherwise, and <c>{}</c> and <c>[]</c> for empty
        /// collections.
        /// </summary>
        Block = 0,

        /// <summary>
        /// JSON, which is also YAML flow: the output of the JSON-compatible writer, with <see cref="YamlWriterOptions.Indented"/>,
        /// <see cref="YamlWriterOptions.IndentCharacter"/>, <see cref="YamlWriterOptions.IndentSize"/> and
        /// <see cref="YamlWriterOptions.NewLine"/> applying as they do to YAML.
        /// </summary>
        JsonCompatible = 1,
    }
}
