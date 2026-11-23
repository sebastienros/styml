// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Converts the provided value into a <see cref="YamlNode"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="YamlNode"/> representation of the YAML value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static YamlNode? SerializeToNode<TValue>(TValue value, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return WriteNode(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlNode"/>.
        /// </summary>
        /// <returns>A <see cref="YamlNode"/> representation of the value.</returns>
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
        public static YamlNode? SerializeToNode(object? value, Type inputType, YamlSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            YamlTypeInfo typeInfo = GetTypeInfo(options, inputType);
            return WriteNodeAsObject(value, typeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlNode"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="YamlNode"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static YamlNode? SerializeToNode<TValue>(TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteNode(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlNode"/>.
        /// </summary>
        /// <returns>A <see cref="YamlNode"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static YamlNode? SerializeToNode(object? value, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteNodeAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="YamlNode"/>.
        /// </summary>
        /// <returns>A <see cref="YamlNode"/> representation of the value.</returns>
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
        public static YamlNode? SerializeToNode(object? value, Type inputType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);
            return WriteNodeAsObject(value, yamlTypeInfo);
        }

        private static YamlNode? WriteNode<TValue>(in TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            YamlSerializerOptions options = yamlTypeInfo.Options;

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.Serialize(writer, value);
                return YamlNode.Parse(output.WrittenSpan, options.GetNodeOptions(), options.GetDocumentOptions());
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static YamlNode? WriteNodeAsObject(object? value, YamlTypeInfo yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            YamlSerializerOptions options = yamlTypeInfo.Options;

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.SerializeAsObject(writer, value);
                return YamlNode.Parse(output.WrittenSpan, options.GetNodeOptions(), options.GetDocumentOptions());
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
