// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Defines polymorphic configuration for a specified base type.
    /// </summary>
    public class YamlPolymorphismOptions
    {
        private DerivedTypeList? _derivedTypes;
        private bool _isConfigured;

        /// <summary>
        /// Creates an empty <see cref="YamlPolymorphismOptions"/> instance.
        /// </summary>
        public YamlPolymorphismOptions()
        {
        }

        /// <summary>
        /// Gets the list of derived types supported in the current polymorphic type configuration.
        /// </summary>
        public IList<YamlDerivedType> DerivedTypes => _derivedTypes ??= new(this);

        /// <summary>
        /// When set to <see langword="true"/>, instructs the serializer to ignore any
        /// unrecognized type discriminator id's and reverts to the contract of the base type.
        /// Otherwise, it will fail the deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public bool IgnoreUnrecognizedTypeDiscriminators
        {
            get;
            set
            {
                VerifyMutable();
                _isConfigured = true;
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the behavior when serializing an undeclared derived runtime type.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling
        {
            get;
            set
            {
                VerifyMutable();
                _isConfigured = true;
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets a custom type discriminator property name for the polymorhic type.
        /// Uses the default '$type' property name if left unset.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        [AllowNull]
        public string TypeDiscriminatorPropertyName
        {
            get => field ?? YamlSerializer.TypePropertyName;
            set
            {
                VerifyMutable();
                _isConfigured = true;
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets how a value says which derived type it is: the <c>$type</c> property (the default), a YAML tag,
        /// or both (<see cref="YamlTypeDiscriminatorStyle"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public YamlTypeDiscriminatorStyle DiscriminatorStyle
        {
            get;
            set
            {
                VerifyMutable();
                _isConfigured = true;
                field = value;
            }
        }

        private void VerifyMutable() => DeclaringTypeInfo?.VerifyMutable();

        internal YamlTypeInfo? DeclaringTypeInfo { get; set; }

        internal bool IsEmpty => !_isConfigured && _derivedTypes is not { Count: > 0 };

        internal static YamlPolymorphismOptions? CreateFromAttributeDeclarations(
            Type baseType,
            out YamlPolymorphicAttribute? polymorphicAttribute)
        {
            YamlPolymorphismOptions? options = null;
            polymorphicAttribute = baseType.GetCustomAttribute<YamlPolymorphicAttribute>(inherit: false);

            if (polymorphicAttribute is not null)
            {
                options = new()
                {
                    IgnoreUnrecognizedTypeDiscriminators = polymorphicAttribute.IgnoreUnrecognizedTypeDiscriminators,
                    UnknownDerivedTypeHandling = polymorphicAttribute.UnknownDerivedTypeHandling,
                    TypeDiscriminatorPropertyName = polymorphicAttribute.TypeDiscriminatorPropertyName,
                    DiscriminatorStyle = polymorphicAttribute.DiscriminatorStyle,
                };
            }

            foreach (YamlDerivedTypeAttribute attr in baseType.GetCustomAttributes<YamlDerivedTypeAttribute>(inherit: false))
            {
                (options ??= new()).DerivedTypes.Add(new YamlDerivedType(attr.DerivedType, attr.TypeDiscriminator) { Tag = attr.Tag });
            }

            return options;
        }

        private sealed class DerivedTypeList : ConfigurationList<YamlDerivedType>
        {
            private readonly YamlPolymorphismOptions _parent;

            public DerivedTypeList(YamlPolymorphismOptions parent)
            {
                _parent = parent;
            }

            public override bool IsReadOnly => _parent.DeclaringTypeInfo?.IsReadOnly == true;
            protected override void OnCollectionModifying() => _parent.DeclaringTypeInfo?.VerifyMutable();
        }

    }
}
