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
        /// Converts the <see cref="YamlElement"/> representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="element">The <see cref="YamlElement"/> to convert.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="YamlException">
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(this YamlElement element, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            ReadOnlySpan<byte> utf8Yaml = element.GetDeserializationValue().Span;
            return ReadFromSpan(utf8Yaml, yamlTypeInfo, schema: element.DocumentSchema);
        }

        /// <summary>
        /// Converts the <see cref="YamlElement"/> representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="element">The <see cref="YamlElement"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// <paramref name="returnType"/> is not compatible with the YAML.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize(this YamlElement element, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            ReadOnlySpan<byte> utf8Yaml = element.GetDeserializationValue().Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: element.DocumentSchema);
        }

        /// <summary>
        /// Converts the <see cref="YamlElement"/> representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="element">The <see cref="YamlElement"/> to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        public static TValue? Deserialize<TValue>(this YamlElement element, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Yaml = element.GetDeserializationValue().Span;
            return ReadFromSpan(utf8Yaml, yamlTypeInfo, schema: element.DocumentSchema);
        }

        /// <summary>
        /// Converts the <see cref="YamlElement"/> representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="element">The <see cref="YamlElement"/> to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static object? Deserialize(this YamlElement element, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Yaml = element.GetDeserializationValue().Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: element.DocumentSchema);
        }

        /// <summary>
        /// Converts the <see cref="YamlElement"/> representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="element">The <see cref="YamlElement"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType" /> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        public static object? Deserialize(this YamlElement element, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, returnType);
            ReadOnlySpan<byte> utf8Yaml = element.GetDeserializationValue().Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: element.DocumentSchema);
        }
    }
}
