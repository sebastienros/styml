// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When implemented in a derived class, creates a <see cref="YamlTypeClassifier"/>
    /// delegate that classifies YAML payloads to determine the target type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory is the extension point for users who want to customize how YAML payloads
    /// are matched to union case types or polymorphic derived types during deserialization.
    /// It is referenced by the <see cref="YamlUnionAttribute.TypeClassifier"/> and
    /// <see cref="YamlPolymorphicAttribute.TypeClassifier"/> properties.
    /// </para>
    /// <para>
    /// The factory's <see cref="CreateYamlClassifier"/> method is called once during
    /// <see cref="YamlTypeInfo"/> configuration, and the returned delegate is invoked
    /// on every deserialization call.
    /// </para>
    /// <para>
    /// A single factory may produce type classifiers for multiple declaring types — analogous to
    /// <see cref="YamlConverterFactory"/>. Implementations report supported metadata shapes via
    /// <see cref="CanClassify"/>. For the common case of a factory pinned to a single declaring
    /// type, derive from <see cref="YamlTypeClassifierFactory{T}"/> instead.
    /// </para>
    /// <para>
    /// Factories may be registered globally on <see cref="YamlSerializerOptions.TypeClassifiers"/>
    /// or per-type via <see cref="YamlUnionAttribute.TypeClassifier"/> /
    /// <see cref="YamlPolymorphicAttribute.TypeClassifier"/>. When both are present the attribute wins.
    /// </para>
    /// <para>
    /// For union types, the produced classifier delegate is <b>not</b> invoked when the
    /// payload is a YAML <see cref="YamlTokenType.Null"/> token. The converter handles
    /// null payloads directly: it produces the canonical null union value when at least
    /// one case is nullable, and otherwise throws a <see cref="YamlException"/>. See
    /// <see cref="YamlTypeClassifier"/> for details.
    /// </para>
    /// </remarks>
    public abstract class YamlTypeClassifierFactory
    {
        /// <summary>
        /// When overridden, indicates whether this factory can produce a classifier for
        /// the specified metadata context.
        /// </summary>
        /// <param name="context">The metadata context for the type being configured.</param>
        /// <returns>
        /// <see langword="true"/> if this factory can produce a classifier for
        /// <paramref name="context"/>; otherwise <see langword="false"/>.
        /// </returns>
        public abstract bool CanClassify(YamlTypeClassifierContext context);

        /// <summary>
        /// Creates a delegate that classifies YAML payloads to determine the target type.
        /// </summary>
        /// <param name="context">
        /// An immutable snapshot of metadata including the declaring type, candidate types
        /// (with optional discriminator values), and the discriminator property name.
        /// </param>
        /// <param name="options">The serializer options for the current context.</param>
        /// <returns>A <see cref="YamlTypeClassifier"/> delegate.</returns>
        public abstract YamlTypeClassifier CreateYamlClassifier(
            YamlTypeClassifierContext context,
            YamlSerializerOptions options);
    }

    /// <summary>
    /// Convenience base class for a <see cref="YamlTypeClassifierFactory"/> that produces
    /// type classifiers for a single declaring type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The declaring type that this factory classifies.</typeparam>
    /// <remarks>
    /// Seals <see cref="CanClassify"/> to compare the context declaring type against
    /// <typeparamref name="T"/> using reference equality. Subclasses only need to implement
    /// <see cref="YamlTypeClassifierFactory.CreateYamlClassifier"/>.
    /// </remarks>
    public abstract class YamlTypeClassifierFactory<T> : YamlTypeClassifierFactory
    {
        /// <inheritdoc/>
        public sealed override bool CanClassify(YamlTypeClassifierContext context) => context.DeclaringType == typeof(T);
    }
}
