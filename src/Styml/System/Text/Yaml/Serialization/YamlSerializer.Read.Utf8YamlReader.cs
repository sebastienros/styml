// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Reads one YAML value (including objects or arrays) from the provided reader into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="reader">The reader to read.</param>
        /// <param name="options">Options to control the serializer behavior during reading.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or a value could not be read from the reader.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        ///
        ///   <para>
        ///     The <see cref="YamlReaderOptions"/> used to create the instance of the <see cref="Utf8YamlReader"/> take precedence over the <see cref="YamlSerializerOptions"/> when they conflict.
        ///     Hence, <see cref="YamlReaderOptions.AllowTrailingCommas"/>, <see cref="YamlReaderOptions.MaxDepth"/>, and <see cref="YamlReaderOptions.CommentHandling"/> are used while reading.
        ///   </para>
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(ref Utf8YamlReader reader, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return Read<TValue>(ref reader, yamlTypeInfo);
        }

        /// <summary>
        /// Reads one YAML value (including objects or arrays) from the provided reader into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="reader">The reader to read.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the serializer behavior during reading.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <paramref name="returnType"/> is not compatible with the YAML,
        /// or a value could not be read from the reader.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        ///   <para>
        ///     The <see cref="YamlReaderOptions"/> used to create the instance of the <see cref="Utf8YamlReader"/> take precedence over the <see cref="YamlSerializerOptions"/> when they conflict.
        ///     Hence, <see cref="YamlReaderOptions.AllowTrailingCommas"/>, <see cref="YamlReaderOptions.MaxDepth"/>, and <see cref="YamlReaderOptions.CommentHandling"/> are used while reading.
        ///   </para>
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize(ref Utf8YamlReader reader, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            return ReadAsObject(ref reader, yamlTypeInfo);
        }

        /// <summary>
        /// Reads one YAML value (including objects or arrays) from the provided reader into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="reader">The reader to read.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or a value could not be read from the reader.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        ///
        ///   <para>
        ///     The <see cref="YamlReaderOptions"/> used to create the instance of the <see cref="Utf8YamlReader"/> take precedence over the <see cref="YamlSerializerOptions"/> when they conflict.
        ///     Hence, <see cref="YamlReaderOptions.AllowTrailingCommas"/>, <see cref="YamlReaderOptions.MaxDepth"/>, and <see cref="YamlReaderOptions.CommentHandling"/> are used while reading.
        ///   </para>
        /// </remarks>
        public static TValue? Deserialize<TValue>(ref Utf8YamlReader reader, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return Read(ref reader, yamlTypeInfo);
        }

        /// <summary>
        /// Reads one YAML value (including objects or arrays) from the provided reader into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="reader">The reader to read.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <paramref name="yamlTypeInfo"/> is not compatible with the YAML,
        /// or a value could not be read from the reader.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        ///
        ///   <para>
        ///     The <see cref="YamlReaderOptions"/> used to create the instance of the <see cref="Utf8YamlReader"/> take precedence over the <see cref="YamlSerializerOptions"/> when they conflict.
        ///     Hence, <see cref="YamlReaderOptions.AllowTrailingCommas"/>, <see cref="YamlReaderOptions.MaxDepth"/>, and <see cref="YamlReaderOptions.CommentHandling"/> are used while reading.
        ///   </para>
        /// </remarks>
        public static object? Deserialize(ref Utf8YamlReader reader, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadAsObject(ref reader, yamlTypeInfo);
        }

        /// <summary>
        /// Reads one YAML value (including objects or arrays) from the provided reader into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="reader">The reader to read.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="returnType"/> or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <paramref name="returnType"/> is not compatible with the YAML,
        /// or a value could not be read from the reader.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method on the provided <paramref name="context"/>
        /// did not return a compatible <see cref="YamlTypeInfo"/> for <paramref name="returnType"/>.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        ///   <para>
        ///     The <see cref="YamlReaderOptions"/> used to create the instance of the <see cref="Utf8YamlReader"/> take precedence over the <see cref="YamlSerializerOptions"/> when they conflict.
        ///     Hence, <see cref="YamlReaderOptions.AllowTrailingCommas"/>, <see cref="YamlReaderOptions.MaxDepth"/>, and <see cref="YamlReaderOptions.CommentHandling"/> are used while reading.
        ///   </para>
        /// </remarks>
        public static object? Deserialize(ref Utf8YamlReader reader, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            return ReadAsObject(ref reader, GetTypeInfo(context, returnType));
        }

        private static TValue? Read<TValue>(ref Utf8YamlReader reader, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            if (reader.CurrentState.Options.CommentHandling == YamlCommentHandling.Allow)
            {
                ThrowHelper.ThrowArgumentException_SerializerDoesNotSupportComments(nameof(reader));
            }

            ReadStack state = default;
            state.Initialize(yamlTypeInfo);
            Utf8YamlReader restore = reader;

            try
            {
                Utf8YamlReader scopedReader = GetReaderScopedToNextValue(ref reader, ref state);
                if (yamlTypeInfo.Options.ResolveMergeKeys && YamlDocument.RewriteMergeKeysOfValue(scopedReader, yamlTypeInfo.Options.DuplicateKeyHandling) is byte[] merged)
                {
                    // M5b: the value is read from its merged text.
                    return ReadFromSpan(merged, yamlTypeInfo, reread: YamlRereadComments.None);
                }

                return yamlTypeInfo.Deserialize(ref scopedReader, ref state);
            }
            catch (YamlException)
            {
                reader = restore;
                throw;
            }
        }

        internal static object? ReadAsObject(ref Utf8YamlReader reader, YamlTypeInfo yamlTypeInfo)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            if (reader.CurrentState.Options.CommentHandling == YamlCommentHandling.Allow)
            {
                ThrowHelper.ThrowArgumentException_SerializerDoesNotSupportComments(nameof(reader));
            }

            ReadStack state = default;
            state.Initialize(yamlTypeInfo);
            Utf8YamlReader restore = reader;

            try
            {
                Utf8YamlReader scopedReader = GetReaderScopedToNextValue(ref reader, ref state);
                if (yamlTypeInfo.Options.ResolveMergeKeys && YamlDocument.RewriteMergeKeysOfValue(scopedReader, yamlTypeInfo.Options.DuplicateKeyHandling) is byte[] merged)
                {
                    return ReadFromSpanAsObject(merged, yamlTypeInfo, reread: YamlRereadComments.None);
                }

                return yamlTypeInfo.DeserializeAsObject(ref scopedReader, ref state);
            }
            catch (YamlException)
            {
                reader = restore;
                throw;
            }
        }

        private static Utf8YamlReader GetReaderScopedToNextValue(ref Utf8YamlReader reader, scoped ref ReadStack state) =>
            GetYamlReaderScopedToNextValue(ref reader, ref state);
    }
}
