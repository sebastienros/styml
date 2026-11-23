// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Provides serialization metadata about a collection type.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class YamlCollectionInfoValues<TCollection>
    {
        /// <summary>
        /// A <see cref="Func{TResult}"/> to create an instance of the collection when deserializing.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public Func<TCollection>? ObjectCreator { get; init; }

        /// <summary>
        /// If a dictionary type, the <see cref="YamlTypeInfo"/> instance representing the key type.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlTypeInfo? KeyInfo { get; init; }

        /// <summary>
        /// A <see cref="YamlTypeInfo"/> instance representing the element type.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlTypeInfo ElementInfo { get; init; } = null!;

        /// <summary>
        /// The <see cref="YamlNumberHandling"/> option to apply to number collection elements.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlNumberHandling NumberHandling { get; init; }

        /// <summary>
        /// Specifies polymorphism metadata to apply to the collection.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlPolymorphismOptions? PolymorphismOptions { get; init; }

        /// <summary>
        /// An optimized serialization implementation assuming pre-determined <see cref="YamlSourceGenerationOptionsAttribute"/> defaults.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public Action<Utf8YamlWriter, TCollection>? SerializeHandler { get; init; }

        /// <summary>
        /// Gets or sets a factory used to lazily construct a <see cref="YamlTypeClassifier"/>
        /// from the polymorphic type's metadata at <see cref="YamlTypeInfo"/> configuration time.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public YamlTypeClassifierFactory? TypeClassifierFactory { get; init; }
    }
}
