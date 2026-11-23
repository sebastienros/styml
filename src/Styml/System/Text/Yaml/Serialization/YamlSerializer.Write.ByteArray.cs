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
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static byte[] SerializeToUtf8Bytes<TValue>(
            TValue value,
            YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return WriteBytes(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static byte[] SerializeToUtf8Bytes(
            object? value,
            Type inputType,
            YamlSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);
            return WriteBytesAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes<TValue>(TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteBytes(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes(object? value, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteBytesAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes(object? value, Type inputType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);
            return WriteBytesAsObject(value, yamlTypeInfo);
        }

        private static byte[] WriteBytes<TValue>(in TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.Serialize(writer, value);
                return output.WrittenSpan.ToArray();
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static byte[] WriteBytesAsObject(object? value, YamlTypeInfo yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.SerializeAsObject(writer, value);
                return output.WrittenSpan.ToArray();
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
