// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SourceGenerators;

namespace System.Text.Yaml.SourceGeneration
{
    /// <summary>
    /// Models a single derived type entry for source-generated polymorphism metadata.
    /// </summary>
    /// <remarks>
    /// Type needs to be cacheable as a Roslyn incremental value so it must be
    ///
    /// 1) immutable and
    /// 2) implement structural (pointwise) equality comparison.
    /// </remarks>
    public sealed record DerivedTypeSpec
    {
        public required TypeRef DerivedType { get; init; }

        public required object? TypeDiscriminator { get; init; }

        /// <summary>M5d: <c>YamlDerivedTypeAttribute.Tag</c>, or null.</summary>
        public string? Tag { get; init; }
    }
}
