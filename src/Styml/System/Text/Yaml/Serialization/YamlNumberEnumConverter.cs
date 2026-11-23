// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Converters;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Converter to convert enums to and from numeric values.
    /// </summary>
    /// <typeparam name="TEnum">The enum type that this converter targets.</typeparam>
    /// <remarks>
    /// This is the default converter for enums and can be used to override
    /// <see cref="YamlSourceGenerationOptionsAttribute.UseStringEnumConverter"/>
    /// on individual types or properties.
    /// </remarks>
    public sealed class YamlNumberEnumConverter<TEnum> : YamlConverterFactory
        where TEnum : struct, Enum
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlNumberEnumConverter{TEnum}"/>.
        /// </summary>
        public YamlNumberEnumConverter() { }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TEnum);

        /// <inheritdoc />
        public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            if (typeToConvert != typeof(TEnum))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_YamlConverterFactory_TypeNotSupported(typeToConvert);
            }

            return EnumConverterFactory.Helpers.Create<TEnum>(EnumConverterOptions.AllowNumbers, options);
        }
    }
}
