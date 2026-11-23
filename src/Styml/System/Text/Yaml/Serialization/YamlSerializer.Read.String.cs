// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides functionality to serialize objects or value types to YAML and
    /// deserialize YAML into objects or value types.
    /// </summary>
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Parses the text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] string yaml, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(yaml);

            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(yaml.AsSpan(), yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into an instance of the type specified by a generic type parameter.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="yaml">The YAML text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the span beyond a single YAML value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a UTF-16 span is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> or <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType"/> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string yaml, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            ArgumentNullException.ThrowIfNull(returnType);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(yaml.AsSpan(), yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into an instance of a specified type.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="yaml">The YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType"/> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the span beyond a single YAML value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a UTF-16 span is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            // default/null span is treated as empty

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] string yaml, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpan(yaml.AsSpan(), yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the YAML.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpan(yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string yaml, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(yaml.AsSpan(), yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yamlTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single YAML value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> or <paramref name="returnType"/> is <see langword="null"/>.
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
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string yaml, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, returnType);
            return ReadFromSpanAsObject(yaml.AsSpan(), yamlTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="yaml"/> or <paramref name="returnType"/> is <see langword="null"/>.
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
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(context, returnType);
            return ReadFromSpanAsObject(yaml, yamlTypeInfo);
        }

        private static unsafe TValue? ReadFromSpan<TValue>(ReadOnlySpan<char> yaml, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8 =
                // Use stack memory
                yaml.Length <= (YamlConstants.StackallocByteThreshold / YamlConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[YamlConstants.StackallocByteThreshold] :
                // Use a pooled array
                yaml.Length <= (YamlConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / YamlConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(yaml.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[YamlReaderHelper.GetUtf8ByteCount(yaml)];

            try
            {
                int actualByteCount = YamlReaderHelper.GetUtf8FromText(yaml, utf8);
                utf8 = utf8.Slice(0, actualByteCount);
                return ReadFromSpan(utf8, yamlTypeInfo, actualByteCount);
            }
            finally
            {
                if (tempArray is not null)
                {
                    utf8.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }

        private static unsafe object? ReadFromSpanAsObject(ReadOnlySpan<char> yaml, YamlTypeInfo yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8 =
                // Use stack memory
                yaml.Length <= (YamlConstants.StackallocByteThreshold / YamlConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[YamlConstants.StackallocByteThreshold] :
                // Use a pooled array
                yaml.Length <= (YamlConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / YamlConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(yaml.Length * YamlConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[YamlReaderHelper.GetUtf8ByteCount(yaml)];

            try
            {
                int actualByteCount = YamlReaderHelper.GetUtf8FromText(yaml, utf8);
                utf8 = utf8.Slice(0, actualByteCount);
                return ReadFromSpanAsObject(utf8, yamlTypeInfo, actualByteCount);
            }
            finally
            {
                if (tempArray is not null)
                {
                    utf8.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }
    }
}
