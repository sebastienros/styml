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
        /// Converts the <see cref="YamlDocument"/> representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="document">The <see cref="YamlDocument"/> to convert.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(this YamlDocument document, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            ReadOnlySpan<byte> utf8Yaml = document.GetDeserializationValue(0, out YamlRereadComments comments).Span;
            return ReadFromSpan(utf8Yaml, yamlTypeInfo, schema: document.Schema, reread: comments);
        }

        /// <summary>
        /// Converts the <see cref="YamlDocument"/> representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="document">The <see cref="YamlDocument"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> or <paramref name="returnType"/> is <see langword="null"/>.
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
        public static object? Deserialize(this YamlDocument document, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(returnType);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            ReadOnlySpan<byte> utf8Yaml = document.GetDeserializationValue(0, out YamlRereadComments comments).Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: document.Schema, reread: comments);
        }

        /// <summary>
        /// Converts the <see cref="YamlDocument"/> representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="document">The <see cref="YamlDocument"/> to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        /// </exception>
        public static TValue? Deserialize<TValue>(this YamlDocument document, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Yaml = document.GetDeserializationValue(0, out YamlRereadComments comments).Span;
            return ReadFromSpan(utf8Yaml, yamlTypeInfo, schema: document.Schema, reread: comments);
        }

        /// <summary>
        /// Converts the <see cref="YamlDocument"/> representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <param name="document">The <see cref="YamlDocument"/> to convert.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static object? Deserialize(this YamlDocument document, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Yaml = document.GetDeserializationValue(0, out YamlRereadComments comments).Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: document.Schema, reread: comments);
        }

        /// <summary>
        /// Converts the <see cref="YamlDocument"/> representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="document">The <see cref="YamlDocument"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
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
        public static object? Deserialize(this YamlDocument document, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, returnType);
            ReadOnlySpan<byte> utf8Yaml = document.GetDeserializationValue(0, out YamlRereadComments comments).Span;
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo, schema: document.Schema, reread: comments);
        }
    }
}
