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
        /// Parses the UTF-8 encoded text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> utf8Yaml, YamlSerializerOptions? options = null)
        {
            YamlTypeInfo<TValue> yamlTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(utf8Yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <paramref name="returnType"/> is not compatible with the YAML,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize(ReadOnlySpan<byte> utf8Yaml, Type returnType, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            YamlTypeInfo yamlTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single YAML value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the YAML value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the YAML,
        /// or when there is remaining data in the buffer.
        /// </exception>
        public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> utf8Yaml, YamlTypeInfo<TValue> yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpan(utf8Yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single YAML value into an instance specified by the <paramref name="yamlTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="yamlTypeInfo"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="yamlTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// or there is remaining data in the buffer.
        /// </exception>
        public static object? Deserialize(ReadOnlySpan<byte> utf8Yaml, YamlTypeInfo yamlTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            yamlTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(utf8Yaml, yamlTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single YAML value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the YAML value.</returns>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        /// The YAML is invalid,
        /// <paramref name="returnType"/> is not compatible with the YAML,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Yaml.Serialization.YamlConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlSerializerContext.GetTypeInfo(Type)"/> method on the provided <paramref name="context"/>
        /// did not return a compatible <see cref="YamlTypeInfo"/> for <paramref name="returnType"/>.
        /// </exception>
        public static object? Deserialize(ReadOnlySpan<byte> utf8Yaml, Type returnType, YamlSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            return ReadFromSpanAsObject(utf8Yaml, GetTypeInfo(context, returnType));
        }

        internal static TValue? ReadFromSpan<TValue>(ReadOnlySpan<byte> utf8Yaml, YamlTypeInfo<TValue> yamlTypeInfo, int? actualByteCount = null, YamlSchemaKind? schema = null, YamlRereadComments? reread = null)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            if (yamlTypeInfo.Options.ResolveMergeKeys && RewriteMergeKeys(utf8Yaml.Slice(0, actualByteCount ?? utf8Yaml.Length), yamlTypeInfo.Options, schema, reread) is byte[] merged)
            {
                // The merged text is the writer's: it has no JSON comments, and its '//' is text.
                utf8Yaml = merged;
                actualByteCount = null;
                reread = YamlRereadComments.None;
            }

            YamlReaderOptions readerOptions = yamlTypeInfo.Options.GetReaderOptions();
            if (schema is YamlSchemaKind kind)
            {
                // M5c: a re-read of a node (Core) or of an element (its document's schema).
                readerOptions.Schema = kind;
            }

            YamlReaderState readerState = CreateReadState(ref readerOptions, reread);
            var reader = new Utf8YamlReader(utf8Yaml, isFinalBlock: true, readerState);

            ReadStack state = default;
            state.Initialize(yamlTypeInfo);

            TValue? value = yamlTypeInfo.Deserialize(ref reader, ref state);

            // The reader should have thrown if we have remaining bytes, unless AllowMultipleValues is true.
            Debug.Assert(reader.BytesConsumed == (actualByteCount ?? utf8Yaml.Length) || reader.CurrentState.Options.AllowMultipleValues);
            return value;
        }

        internal static object? ReadFromSpanAsObject(ReadOnlySpan<byte> utf8Yaml, YamlTypeInfo yamlTypeInfo, int? actualByteCount = null, YamlSchemaKind? schema = null, YamlRereadComments? reread = null)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);

            if (yamlTypeInfo.Options.ResolveMergeKeys && RewriteMergeKeys(utf8Yaml.Slice(0, actualByteCount ?? utf8Yaml.Length), yamlTypeInfo.Options, schema, reread) is byte[] merged)
            {
                // The merged text is the writer's: it has no JSON comments, and its '//' is text.
                utf8Yaml = merged;
                actualByteCount = null;
                reread = YamlRereadComments.None;
            }

            YamlReaderOptions readerOptions = yamlTypeInfo.Options.GetReaderOptions();
            if (schema is YamlSchemaKind kind)
            {
                // M5c: a re-read of a node (Core) or of an element (its document's schema).
                readerOptions.Schema = kind;
            }

            YamlReaderState readerState = CreateReadState(ref readerOptions, reread);
            var reader = new Utf8YamlReader(utf8Yaml, isFinalBlock: true, readerState);

            ReadStack state = default;
            state.Initialize(yamlTypeInfo);

            object? value = yamlTypeInfo.DeserializeAsObject(ref reader, ref state);

            // The reader should have thrown if we have remaining bytes, unless AllowMultipleValues is true.
            Debug.Assert(reader.BytesConsumed == (actualByteCount ?? utf8Yaml.Length) || reader.CurrentState.Options.AllowMultipleValues);
            return value;
        }

        /// <summary>
        /// M5b, merge keys: the input's merged text when
        /// <see cref="YamlSerializerOptions.ResolveMergeKeys"/> is set and the input has a plain <c>&lt;&lt;</c> key, or null.
        /// The DOM reads the input and writes it again with each merged mapping merged, copies counted against
        /// <see cref="YamlSerializerOptions.MaxAliasExpansion"/>.
        /// </summary>
        /// <summary>
        /// The state of the reader of a span. A re-read of bytes the library holds (an element's or a document's
        /// source, a node's written text) looks for JSON comments as those bytes were read the first time, not as
        /// the options passed for the re-read say; a value sliced out of a document is not at its document level.
        /// </summary>
        private static YamlReaderState CreateReadState(ref YamlReaderOptions readerOptions, YamlRereadComments? reread)
        {
            if (reread is not YamlRereadComments comments)
            {
                return new YamlReaderState(readerOptions);
            }

            readerOptions.CommentHandling = comments == YamlRereadComments.None ? YamlCommentHandling.Disallow : YamlCommentHandling.Skip;
            if (comments != YamlRereadComments.Slice)
            {
                return new YamlReaderState(readerOptions);
            }

            return new YamlReaderState(
                lineNumber: 0,
                bytePositionInLine: 0,
                inObject: default,
                valueIsEscaped: default,
                tokenType: default,
                previousTokenType: default,
                readerOptions: readerOptions,
                bitStack: default,
                yaml: new YamlParseState { Flags = YamlParseFlags.ScopedValue });
        }

        internal static byte[]? RewriteMergeKeys(ReadOnlySpan<byte> utf8Yaml, YamlSerializerOptions options, YamlSchemaKind? schema = null, YamlRereadComments? reread = null)
        {
            if (utf8Yaml.IndexOf("<<"u8) < 0)
            {
                return null;
            }

            return YamlDocument.RewriteMergeKeys(utf8Yaml.ToArray(), new YamlDocumentOptions
            {
                MaxDepth = options.EffectiveMaxDepth,
                MaxAliasExpansion = options.MaxAliasExpansion,
                AllowTrailingCommas = options.AllowTrailingCommas,
                CommentHandling = reread switch
                {
                    null => options.ReadCommentHandling,
                    YamlRereadComments.None => YamlCommentHandling.Disallow,
                    _ => YamlCommentHandling.Skip,
                },
                Schema = schema ?? options.Schema,
                AcceptLenientScalars = options.AcceptLenientScalars,
            }, options.DuplicateKeyHandling);
        }
    }
}
