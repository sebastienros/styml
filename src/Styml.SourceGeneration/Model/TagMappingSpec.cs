// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using SourceGenerators;

namespace System.Text.Yaml.SourceGeneration
{
    /// <summary>
    /// M5d: one <c>[YamlTagMapping(tag, type)]</c> on a context, emitted
    /// into the generated options' <c>TagMappings</c>.
    /// </summary>
    /// <remarks>Immutable with structural equality, as a Roslyn incremental value must be.</remarks>
    public sealed record TagMappingSpec
    {
        public required string Tag { get; init; }

        public required TypeRef Type { get; init; }
    }
}
