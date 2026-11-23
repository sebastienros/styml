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
        /// Converts the provided value into a <see cref="string"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes{TValue}(TValue, YamlSerializerOptions?)"/>
        /// and <see cref="SerializeAsync{TValue}(IO.Stream, TValue, YamlSerializerOptions?, Threading.CancellationToken)"/>.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static string Serialize<TValue>(TValue value, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return WriteString(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes(object?, Type, YamlSerializerOptions?)"/>
        /// and <see cref="SerializeAsync(IO.Stream, object?, Type, YamlSerializerOptions?, Threading.CancellationToken)"/>.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static string Serialize(
            object? value,
            Type inputType,
            YamlSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, inputType);
            return WriteStringAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="string"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes{TValue}(TValue, YamlTypeInfo{TValue})"/>
        /// and <see cref="SerializeAsync{TValue}(IO.Stream, TValue, YamlTypeInfo{TValue}, Threading.CancellationToken)"/>.
        /// </remarks>
        public static string Serialize<TValue>(TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteString(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="yamlTypeInfo"/>.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes(object?, YamlTypeInfo)"/>
        /// and <see cref="SerializeAsync(IO.Stream, object?, YamlTypeInfo, Threading.CancellationToken)"/>.
        /// </remarks>
        public static string Serialize(object? value, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return WriteStringAsObject(value, yamlTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
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
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes(object?, Type, YamlSerializerContext)"/>
        /// and <see cref="SerializeAsync(IO.Stream, object?, Type, YamlSerializerContext, Threading.CancellationToken)"/>.
        /// </remarks>
        public static string Serialize(object? value, Type inputType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, inputType);
            return WriteStringAsObject(value, yamlTypeInfo);
        }

        private static string WriteString<TValue>(in TValue value, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.Serialize(writer, value);
                return YamlReaderHelper.TranscodeHelper(output.WrittenSpan);
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static string WriteStringAsObject(object? value, YamlTypeInfo yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(yamlTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                yamlTypeInfo.SerializeAsObject(writer, value);
                return YamlReaderHelper.TranscodeHelper(output.WrittenSpan);
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
