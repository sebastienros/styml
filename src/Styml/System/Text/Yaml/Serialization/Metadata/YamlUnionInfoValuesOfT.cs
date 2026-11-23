// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Provides serialization metadata about a union type.
    /// </summary>
    /// <typeparam name="T">The union type to serialize or deserialize.</typeparam>
    /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class YamlUnionInfoValues<T>
    {
        /// <summary>
        /// Gets or sets the list of union case type metadata for the current union type.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public IList<YamlUnionCaseInfo>? UnionCases { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to construct a union instance from a case type and case value.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public Func<Type, object?, T>? UnionConstructor { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to deconstruct a union instance into its case type and case value.
        /// </summary>
        /// <remarks>
        /// This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.
        /// Refer to <see cref="YamlTypeInfo.UnionDeconstructor"/> for the full <c>(CaseType, CaseValue)</c>
        /// contract — including the role of a <see langword="null"/> <c>CaseType</c> as the
        /// discriminator for the canonical null-union state.
        /// </remarks>
        public Func<T, (Type? CaseType, object? CaseValue)>? UnionDeconstructor { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to classify YAML payloads during union deserialization.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlTypeClassifier? TypeClassifier { get; init; }

        /// <summary>
        /// Gets or sets a factory used to lazily construct a <see cref="YamlTypeClassifier"/>
        /// from the union's metadata at <see cref="YamlTypeInfo"/> configuration time.
        /// </summary>
        /// <remarks>
        /// This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.
        /// Resolved from <see cref="YamlUnionAttribute.TypeClassifier"/> at compile time.
        /// When both this property and <see cref="TypeClassifier"/> are set, the explicit
        /// classifier delegate wins and the factory is ignored.
        /// </remarks>
        public YamlTypeClassifierFactory? TypeClassifierFactory { get; init; }
    }
}
