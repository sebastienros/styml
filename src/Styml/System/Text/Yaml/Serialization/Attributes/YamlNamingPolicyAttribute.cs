// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a type, property, or field, indicates what <see cref="YamlNamingPolicy"/>
    /// should be used to convert property names.
    /// </summary>
    /// <remarks>
    /// When placed on a property or field, the naming policy specified by this attribute
    /// takes precedence over the type-level attribute and the <see cref="YamlSerializerOptions.PropertyNamingPolicy"/>.
    /// When placed on a type, the naming policy specified by this attribute takes precedence
    /// over the <see cref="YamlSerializerOptions.PropertyNamingPolicy"/>.
    /// The <see cref="YamlPropertyNameAttribute"/> takes precedence over this attribute.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
        AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = false)]
    public class YamlNamingPolicyAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlNamingPolicyAttribute"/>
        /// with the specified known naming policy.
        /// </summary>
        /// <param name="namingPolicy">The known naming policy to use for name conversion.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The specified <paramref name="namingPolicy"/> is not a valid known naming policy value.
        /// </exception>
        public YamlNamingPolicyAttribute(YamlKnownNamingPolicy namingPolicy)
        {
            NamingPolicy = ResolveNamingPolicy(namingPolicy);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="YamlNamingPolicyAttribute"/>
        /// with a custom naming policy.
        /// </summary>
        /// <param name="namingPolicy">The naming policy to use for name conversion.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="namingPolicy"/> is <see langword="null"/>.
        /// </exception>
        protected YamlNamingPolicyAttribute(YamlNamingPolicy namingPolicy)
        {
            if (namingPolicy is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(namingPolicy));
            }

            NamingPolicy = namingPolicy;
        }

        /// <summary>
        /// Gets the naming policy to use for name conversion.
        /// </summary>
        public YamlNamingPolicy NamingPolicy { get; }

        internal static YamlNamingPolicy ResolveNamingPolicy(YamlKnownNamingPolicy namingPolicy)
        {
            return namingPolicy switch
            {
                YamlKnownNamingPolicy.CamelCase => YamlNamingPolicy.CamelCase,
                YamlKnownNamingPolicy.SnakeCaseLower => YamlNamingPolicy.SnakeCaseLower,
                YamlKnownNamingPolicy.SnakeCaseUpper => YamlNamingPolicy.SnakeCaseUpper,
                YamlKnownNamingPolicy.KebabCaseLower => YamlNamingPolicy.KebabCaseLower,
                YamlKnownNamingPolicy.KebabCaseUpper => YamlNamingPolicy.KebabCaseUpper,
                YamlKnownNamingPolicy.PascalCase => YamlNamingPolicy.PascalCase,
                _ => throw new ArgumentOutOfRangeException(nameof(namingPolicy)),
            };
        }
    }
}
