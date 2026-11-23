// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a type declaration, indicates that the specified subtype should be opted into polymorphic serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    public class YamlDerivedTypeAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new attribute with specified parameters.
        /// </summary>
        /// <param name="derivedType">A derived type that should be supported in polymorphic serialization of the declared based type.</param>
        public YamlDerivedTypeAttribute(Type derivedType)
        {
            DerivedType = derivedType;
        }

        /// <summary>
        /// Initializes a new attribute with specified parameters.
        /// </summary>
        /// <param name="derivedType">A derived type that should be supported in polymorphic serialization of the declared base type.</param>
        /// <param name="typeDiscriminator">The type discriminator identifier to be used for the serialization of the subtype.</param>
        public YamlDerivedTypeAttribute(Type derivedType, string typeDiscriminator)
        {
            DerivedType = derivedType;
            TypeDiscriminator = typeDiscriminator;
        }

        /// <summary>
        /// Initializes a new attribute with specified parameters.
        /// </summary>
        /// <param name="derivedType">A derived type that should be supported in polymorphic serialization of the declared base type.</param>
        /// <param name="typeDiscriminator">The type discriminator identifier to be used for the serialization of the subtype.</param>
        public YamlDerivedTypeAttribute(Type derivedType, int typeDiscriminator)
        {
            DerivedType = derivedType;
            TypeDiscriminator = typeDiscriminator;
        }

        /// <summary>
        /// A derived type that should be supported in polymorphic serialization of the declared base type.
        /// </summary>
        public Type DerivedType { get; }

        /// <summary>
        /// The type discriminator identifier to be used for the serialization of the subtype.
        /// </summary>
        public object? TypeDiscriminator { get; }

        /// <summary>
        /// Gets or sets the YAML tag of the derived type under <see cref="YamlTypeDiscriminatorStyle.Tag"/> and
        /// <see cref="YamlTypeDiscriminatorStyle.Both"/>: a local tag (<c>!dog</c>) or a global tag URI
        /// (<c>tag:example.com,2000:dog</c>). When unset, the tag is <c>!</c> followed by the type discriminator. A core tag
        /// (<c>!!str</c>), <c>!</c> and <c>?</c> are refused; under <see cref="YamlTypeDiscriminatorStyle.Property"/> the tag
        /// is checked and otherwise unused.
        /// </summary>
        public string? Tag { get; set; }
    }
}
