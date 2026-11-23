// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Text.Yaml.Serialization.Converters;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Provides helpers to create and initialize metadata for YAML-serializable types.
    /// </summary>
    /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class YamlMetadataServices
    {
        /// <summary>
        /// Creates metadata for a property or field.
        /// </summary>
        /// <typeparam name="T">The type that the converter for the property returns or accepts when converting YAML data.</typeparam>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> to initialize the metadata with.</param>
        /// <param name="propertyInfo">Provides serialization metadata about the property or field.</param>
        /// <returns>A <see cref="YamlPropertyInfo"/> instance initialized with the provided metadata.</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlPropertyInfo CreatePropertyInfo<T>(YamlSerializerOptions options, YamlPropertyInfoValues<T> propertyInfo)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(propertyInfo);

            Type? declaringType = propertyInfo.DeclaringType;
            if (declaringType == null)
            {
                throw new ArgumentException(nameof(propertyInfo.DeclaringType));
            }

            string? propertyName = propertyInfo.PropertyName;
            if (propertyName is null)
            {
                throw new ArgumentException(nameof(propertyInfo.PropertyName));
            }

            if (!propertyInfo.IsProperty && propertyInfo.IsVirtual)
            {
                throw new InvalidOperationException(SR.Format(SR.FieldCannotBeVirtual, nameof(propertyInfo.IsProperty), nameof(propertyInfo.IsVirtual)));
            }

            return CreatePropertyInfoCore(propertyInfo, options);
        }

        /// <summary>
        /// Creates metadata for a complex class or struct.
        /// </summary>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> to initialize the metadata with.</param>
        /// <param name="objectInfo">Provides serialization metadata about an object type with constructors, properties, and fields.</param>
        /// <typeparam name="T">The type of the class or struct.</typeparam>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="objectInfo"/> is null.</exception>
        /// <returns>A <see cref="YamlTypeInfo{T}"/> instance representing the class or struct.</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlTypeInfo<T> CreateObjectInfo<T>(YamlSerializerOptions options, YamlObjectInfoValues<T> objectInfo) where T : notnull
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(objectInfo);

            return CreateCore(options, objectInfo);
        }

        /// <summary>
        /// Creates metadata for a union type.
        /// </summary>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> to initialize the metadata with.</param>
        /// <param name="unionInfo">Provides serialization metadata about a union type and its cases.</param>
        /// <typeparam name="T">The type of the union.</typeparam>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="unionInfo"/> is null.</exception>
        /// <returns>A <see cref="YamlTypeInfo{T}"/> instance representing the union type.</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlTypeInfo<T> CreateUnionInfo<T>(YamlSerializerOptions options, YamlUnionInfoValues<T> unionInfo) where T : notnull
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(unionInfo);
            ArgumentNullException.ThrowIfNull(unionInfo.UnionCases);

            YamlConverter<T> converter = new YamlUnionConverter<T>();
            var typeInfo = new YamlTypeInfo<T>(converter, options)
            {
                UnionConstructor = unionInfo.UnionConstructor,
                UnionDeconstructor = unionInfo.UnionDeconstructor,
                TypeClassifier = unionInfo.TypeClassifier,
            };

            foreach (YamlUnionCaseInfo caseInfo in unionInfo.UnionCases)
            {
                typeInfo.UnionCases.Add(caseInfo);
            }

            typeInfo.TypeClassifierFactory = unionInfo.TypeClassifierFactory;
            typeInfo.TypeClassifierResolutionPending = true;

            typeInfo.IsCustomized = false;
            return typeInfo;
        }

        /// <summary>
        /// Creates metadata for a primitive or a type with a custom converter.
        /// </summary>
        /// <typeparam name="T">The generic type definition.</typeparam>
        /// <returns>A <see cref="YamlTypeInfo{T}"/> instance representing the type.</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlTypeInfo<T> CreateValueInfo<T>(YamlSerializerOptions options, YamlConverter converter)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(converter);

            YamlTypeInfo<T> info = CreateCore<T>(converter, options);
            return info;
        }
    }
}
