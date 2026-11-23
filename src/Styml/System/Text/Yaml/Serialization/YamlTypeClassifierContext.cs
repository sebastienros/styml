// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Provides immutable metadata to a <see cref="YamlTypeClassifierFactory"/> when
    /// creating a <see cref="YamlTypeClassifier"/> delegate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The context carries a <see cref="Kind"/> value indicating whether the classifier
    /// is being created for a union type or a polymorphic type. Exactly one of the two
    /// candidate lists is populated for any given context.
    /// </para>
    /// <para>
    /// Instances are created internally by the serialization infrastructure. Users
    /// interact with the context through a <see cref="YamlTypeClassifierFactory"/>
    /// implementation.
    /// </para>
    /// </remarks>
    public sealed class YamlTypeClassifierContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YamlTypeClassifierContext"/> class.
        /// </summary>
        /// <param name="kind">The type of classifier metadata being configured.</param>
        /// <param name="declaringTypeInfo">The contract being configured for classification.</param>
        /// <param name="unionCases">The union cases of the declaring type, or an empty list.</param>
        /// <param name="derivedTypes">The derived types of the declaring type, or an empty list.</param>
        /// <param name="typeDiscriminatorPropertyName">The YAML property name used for type discrimination, or <see langword="null"/>.</param>
        internal YamlTypeClassifierContext(
            YamlTypeClassifierKind kind,
            YamlTypeInfo declaringTypeInfo,
            IReadOnlyList<YamlUnionCaseInfo> unionCases,
            IReadOnlyList<YamlDerivedType> derivedTypes,
            string? typeDiscriminatorPropertyName)
        {
            Kind = kind;
            DeclaringTypeInfo = declaringTypeInfo;
            UnionCases = unionCases;
            DerivedTypes = derivedTypes;
            TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
        }

        /// <summary>
        /// Gets the type of classifier metadata being configured.
        /// </summary>
        public YamlTypeClassifierKind Kind { get; }

        /// <summary>
        /// Gets the type being configured for classification.
        /// </summary>
        /// <remarks>
        /// For polymorphic types, this is the base class (e.g., <c>Animal</c>).
        /// For union types, this is the union type (e.g., <c>IntOrString</c>).
        /// </remarks>
        public Type DeclaringType => DeclaringTypeInfo.Type;

        // The contract may still be mutable when a modifier reads TypeClassifier.
        internal YamlTypeInfo DeclaringTypeInfo { get; }

        /// <summary>
        /// Gets the union cases of <see cref="DeclaringType"/>.
        /// </summary>
        /// <remarks>
        /// Non-empty when <see cref="DeclaringType"/> is configured as a union type. The
        /// list mirrors <see cref="YamlTypeInfo.UnionCases"/> on the resolved
        /// <see cref="YamlTypeInfo"/>.
        /// </remarks>
        public IReadOnlyList<YamlUnionCaseInfo> UnionCases { get; }

        /// <summary>
        /// Gets the derived types of <see cref="DeclaringType"/>.
        /// </summary>
        /// <remarks>
        /// Non-empty when <see cref="DeclaringType"/> is configured as a polymorphic
        /// type. The list mirrors
        /// <see cref="Metadata.YamlPolymorphismOptions.DerivedTypes"/>; each entry may
        /// carry a <see cref="YamlDerivedType.TypeDiscriminator"/> string or integer.
        /// </remarks>
        public IReadOnlyList<YamlDerivedType> DerivedTypes { get; }

        /// <summary>
        /// Gets the YAML property name used for type discrimination (e.g., <c>"$type"</c>, <c>"kind"</c>).
        /// </summary>
        /// <remarks>
        /// Populated from <see cref="Metadata.YamlPolymorphismOptions.TypeDiscriminatorPropertyName"/>
        /// for polymorphic types. <see langword="null"/> for union types (unions don't use
        /// discriminator properties by default).
        /// </remarks>
        public string? TypeDiscriminatorPropertyName { get; }
    }
}
