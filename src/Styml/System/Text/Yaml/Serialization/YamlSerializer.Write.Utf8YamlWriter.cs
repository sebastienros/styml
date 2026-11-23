// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Writes one YAML value (including objects or arrays) to the provided writer.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="writer">The writer to write.</param>
        /// <param name="value">The value to convert and write.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="writer"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static void Serialize<TValue>(
            Utf8YamlWriter writer,
            TValue value,
            YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            yamlTypeInfo.Serialize(writer, value);
        }

        /// <summary>
        /// Writes one YAML value (including objects or arrays) to the provided writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value">The value to convert and write.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="writer"/> or <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static void Serialize(
            Utf8YamlWriter writer,
            object? value,
            Type inputType,
            YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);
            yamlTypeInfo.SerializeAsObject(writer, value);
        }

        /// <summary>
        /// Writes one YAML value (including objects or arrays) to the provided writer.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="writer">The writer to write.</param>
        /// <param name="value">The value to convert and write.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="writer"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static void Serialize<TValue>(Utf8YamlWriter writer, TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            yamlTypeInfo.Serialize(writer, value);
        }

        /// <summary>
        /// Writes one YAML value (including objects or arrays) to the provided writer.
        /// </summary>
        /// <param name="writer">The writer to write.</param>
        /// <param name="value">The value to convert and write.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="writer"/> or <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static void Serialize(Utf8YamlWriter writer, object? value, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            yamlTypeInfo.SerializeAsObject(writer, value);
        }

        /// <summary>
        /// Writes one YAML value (including objects or arrays) to the provided writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value">The value to convert and write.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="writer"/> or <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        public static void Serialize(Utf8YamlWriter writer, object? value, Type inputType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);
            yamlTypeInfo.SerializeAsObject(writer, value);
        }
    }
}
