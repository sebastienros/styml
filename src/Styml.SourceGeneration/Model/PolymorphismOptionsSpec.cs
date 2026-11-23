// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;
using SourceGenerators;

namespace System.Text.Yaml.SourceGeneration
{
    /// <summary>
    /// Models polymorphism options for source-generated metadata.
    /// </summary>
    /// <remarks>
    /// Type needs to be cacheable as a Roslyn incremental value so it must be
    ///
    /// 1) immutable and
    /// 2) implement structural (pointwise) equality comparison.
    /// </remarks>
    public sealed record PolymorphismOptionsSpec
    {
        public required ImmutableEquatableArray<DerivedTypeSpec> DerivedTypes { get; init; }

        public required bool IgnoreUnrecognizedTypeDiscriminators { get; init; }

        public required TypeRef? TypeClassifierFactoryType { get; init; }

        public required string? TypeDiscriminatorPropertyName { get; init; }

        public required YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; init; }

        /// <summary>M5d: <c>YamlPolymorphicAttribute.DiscriminatorStyle</c>.</summary>
        internal YamlTypeDiscriminatorStyle DiscriminatorStyle { get; init; }
    }
}
