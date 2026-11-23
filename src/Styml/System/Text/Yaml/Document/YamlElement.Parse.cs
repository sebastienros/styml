// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace System.Text.Yaml
{
    public readonly partial struct YamlElement
    {
        /// <summary>
        ///   Parses one YAML value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <returns>
        ///   A YamlElement representing the value (and nested values) read from the reader.
        /// </returns>
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
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="YamlException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static YamlElement ParseValue(ref Utf8YamlReader reader)
        {
            bool ret = YamlDocument.TryParseValue(ref reader, out YamlDocument? document, shouldThrow: true, useArrayPools: false);

            Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
            Debug.Assert(document is not null, "null document returned with shouldThrow: true.");
            return document.RootElement;
        }

        internal static YamlElement ParseValue(ref Utf8YamlReader reader, YamlDuplicateKeyHandling duplicateKeyHandling)
        {
            bool ret = YamlDocument.TryParseValue(
                ref reader,
                out YamlDocument? document,
                shouldThrow: true,
                useArrayPools: false,
                duplicateKeyHandling: duplicateKeyHandling);

            Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
            Debug.Assert(document is not null, "null document returned with shouldThrow: true.");
            return document.RootElement;
        }

        internal static YamlElement ParseValue(Stream utf8Yaml, YamlDocumentOptions options)
        {
            YamlDocument document = YamlDocument.ParseValue(utf8Yaml, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses UTF8-encoded text representing a single YAML value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <param name="utf8Yaml">The YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="YamlElement"/> representation of the YAML value.</returns>
        /// <exception cref="YamlException"><paramref name="utf8Yaml"/> does not represent a valid single YAML value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static YamlElement Parse([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<byte> utf8Yaml, YamlDocumentOptions options = default)
        {
            YamlDocument document = YamlDocument.ParseValue(utf8Yaml, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses text representing a single YAML value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <param name="yaml">The YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="YamlElement"/> representation of the YAML value.</returns>
        /// <exception cref="YamlException"><paramref name="yaml"/> does not represent a valid single YAML value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static YamlElement Parse([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> yaml, YamlDocumentOptions options = default)
        {
            YamlDocument document = YamlDocument.ParseValue(yaml, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses text representing a single YAML value into a <see cref="YamlElement"/>.
        /// </summary>
        /// <param name="yaml">The YAML text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="YamlElement"/> representation of the YAML value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
        /// <exception cref="YamlException"><paramref name="yaml"/> does not represent a valid single YAML value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static YamlElement Parse([StringSyntax(StringSyntaxAttribute.Json)] string yaml, YamlDocumentOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(yaml);

            YamlDocument document = YamlDocument.ParseValue(yaml, options);
            return document.RootElement;
        }

        /// <summary>
        ///   Attempts to parse one YAML value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <param name="element">Receives the parsed element.</param>
        /// <returns>
        ///   <see langword="true"/> if a value was read and parsed into a YamlElement;
        ///   <see langword="false"/> if the reader ran out of data while parsing.
        ///   All other situations result in an exception being thrown.
        /// </returns>
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
        ///     final token in the YAML value.  If an exception is thrown, or <see langword="false"/>
        ///     is returned, the reader is reset to the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="YamlException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static bool TryParseValue(ref Utf8YamlReader reader, [NotNullWhen(true)] out YamlElement? element)
        {
            bool ret = YamlDocument.TryParseValue(ref reader, out YamlDocument? document, shouldThrow: false, useArrayPools: false);
            element = document?.RootElement;
            return ret;
        }
    }
}
