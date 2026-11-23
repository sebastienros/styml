// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides options to be used with <see cref="YamlSerializer"/>.
    /// </summary>
    public sealed partial class YamlSerializerOptions
    {
        /// <summary>
        /// The list of custom converters.
        /// </summary>
        /// <remarks>
        /// Once serialization or deserialization occurs, the list cannot be modified.
        /// </remarks>
        public IList<YamlConverter> Converters => _converters ??= new(this);

        /// <summary>
        /// Gets the list of custom type classifier factories.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each <see cref="YamlTypeClassifierFactory"/> in the list is consulted (in declaration order) when
        /// configuring a union or polymorphic type for which no per-type classifier was set via
        /// <see cref="YamlUnionAttribute.TypeClassifier"/> or <see cref="YamlPolymorphicAttribute.TypeClassifier"/>.
        /// The first factory whose
        /// <see cref="YamlTypeClassifierFactory.CanClassify(YamlTypeClassifierContext)"/> returns <see langword="true"/>
        /// is used; otherwise the built-in type resolution for that metadata shape applies.
        /// </para>
        /// <para>
        /// Once serialization or deserialization occurs, the list cannot be modified.
        /// </para>
        /// </remarks>
        public IList<YamlTypeClassifierFactory> TypeClassifiers => _typeClassifiers ??= new(this);

        /// <summary>
        /// Returns the converter for the specified type.
        /// </summary>
        /// <param name="typeToConvert">The type to return a converter for.</param>
        /// <returns>
        /// The converter for the given type.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The configured <see cref="YamlConverter"/> for <paramref name="typeToConvert"/> returned an invalid converter.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="typeToConvert"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode("Getting a converter for a type may require reflection which depends on unreferenced code.")]
        [RequiresDynamicCode("Getting a converter for a type may require reflection which depends on runtime code generation.")]
        public YamlConverter GetConverter(Type typeToConvert)
        {
            ArgumentNullException.ThrowIfNull(typeToConvert);

            if (YamlSerializer.IsReflectionEnabledByDefault)
            {
                // Backward compatibility -- root & query the default reflection converters
                // but do not populate the TypeInfoResolver setting.
                if (_typeInfoResolver is null)
                {
                    return DefaultYamlTypeInfoResolver.GetConverterForType(typeToConvert, this);
                }
            }

            return GetConverterInternal(typeToConvert);
        }

        /// <summary>
        /// Same as GetConverter but without defaulting to reflection converters.
        /// </summary>
        internal YamlConverter GetConverterInternal(Type typeToConvert)
        {
            YamlTypeInfo yamlTypeInfo = GetTypeInfoInternal(typeToConvert, ensureConfigured: false, resolveIfMutable: true);
            return yamlTypeInfo.Converter;
        }

        internal YamlConverter? GetConverterFromList(Type typeToConvert)
        {
            if (_converters is { } converterList)
            {
                foreach (YamlConverter item in converterList)
                {
                    if (item.CanConvert(typeToConvert))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        internal YamlTypeClassifierFactory? GetTypeClassifierFromList(YamlTypeClassifierContext context)
        {
            if (_typeClassifiers is { } classifierList)
            {
                foreach (YamlTypeClassifierFactory item in classifierList)
                {
                    if (item.CanClassify(context))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        [return: NotNullIfNotNull(nameof(converter))]
        internal YamlConverter? ExpandConverterFactory(YamlConverter? converter, Type typeToConvert)
        {
            if (converter is YamlConverterFactory factory)
            {
                converter = factory.GetConverterInternal(typeToConvert, this);
            }

            return converter;
        }

        internal static void CheckConverterNullabilityIsSameAsPropertyType(YamlConverter converter, Type propertyType)
        {
            // User has indicated that either:
            //   a) a non-nullable-struct handling converter should handle a nullable struct type or
            //   b) a nullable-struct handling converter should handle a non-nullable struct type.
            // User should implement a custom converter for the underlying struct and remove the unnecessary CanConvert method override.
            // The serializer will automatically wrap the custom converter with NullableConverter<T>.
            //
            // We also throw to avoid passing an invalid argument to setters for nullable struct properties,
            // which would cause an InvalidProgramException when the generated IL is invoked.
            if (propertyType.IsValueType && converter.IsValueType &&
                (propertyType.IsNullableOfT() ^ converter.Type!.IsNullableOfT()))
            {
                ThrowHelper.ThrowInvalidOperationException_ConverterCanConvertMultipleTypes(propertyType, converter);
            }
        }
    }
}
