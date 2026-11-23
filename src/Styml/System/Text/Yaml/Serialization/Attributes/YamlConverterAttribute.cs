// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When placed on a property, field, or type, specifies the converter type to use.
    /// </summary>
    /// <remarks>
    /// The specified converter type must derive from <see cref="YamlConverter"/>.
    /// When placed on a property or field, the specified converter will always be used.
    /// When placed on a type, the specified converter will be used unless a compatible converter is added to
    /// <see cref="YamlSerializerOptions.Converters"/> or there is another <see cref="YamlConverterAttribute"/> on a property or field
    /// of the same type.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class YamlConverterAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlConverterAttribute"/> with the specified converter type.
        /// </summary>
        /// <param name="converterType">The type of the converter.</param>
        public YamlConverterAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type converterType)
        {
            ConverterType = converterType;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="YamlConverterAttribute"/>.
        /// </summary>
        protected YamlConverterAttribute() { }

        /// <summary>
        /// The type of the converter to create, or null if <see cref="CreateConverter(Type)"/> should be used to obtain the converter.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? ConverterType { get; }

        /// <summary>
        /// If overridden and <see cref="ConverterType"/> is null, allows a custom attribute to create the converter in order to pass additional state.
        /// </summary>
        /// <returns>
        /// The custom converter.
        /// </returns>
        public virtual YamlConverter? CreateConverter(Type typeToConvert)
        {
            return null;
        }
    }
}
