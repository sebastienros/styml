// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Converts the provided value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="YamlElement"/> representation of the YAML value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static YamlElement SerializeToElement<TValue>(TValue value, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return WriteElement(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <returns>A <see cref="YamlElement"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <exception cref="ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static YamlElement SerializeToElement(object? value, Type inputType, YamlSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);
            return WriteElementAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="YamlElement"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static YamlElement SerializeToElement<TValue>(TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteElement(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <returns>A <see cref="YamlElement"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static YamlElement SerializeToElement(object? value, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteElementAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <returns>A <see cref="YamlElement"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="inputType"/> or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        public static YamlElement SerializeToElement(object? value, Type inputType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo typeInfo = GetTypeInfo(context, inputType);
            return WriteElementAsObject(value, typeInfo);
        }

        private static YamlElement WriteElement<TValue>(in TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            YamlSerializerOptions options = yamlTypeInfo.Options;

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.Serialize(writer, value);
                return YamlElement.Parse(output.WrittenSpan, options.GetDocumentOptions());
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static YamlElement WriteElementAsObject(object? value, YamlTypeInfo yamlTypeInfo)
        {
            YamlSerializerOptions options = yamlTypeInfo.Options;
            Debug.Assert(options is not null);

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.SerializeAsObject(writer, value);
                return YamlElement.Parse(output.WrittenSpan, options.GetDocumentOptions());
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
