// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Yaml.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Nodes
{
    public abstract partial class YamlNode
    {
        /// <summary>
        ///   Parses one YAML value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <param name="nodeOptions">Options to control the behavior.</param>
        /// <returns>
        ///   The <see cref="YamlNode"/> from the reader.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8YamlReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="YamlTokenType.PropertyName"/> or <see cref="YamlTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8YamlReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the YAML value.  If an exception is thrown, the reader is reset to the state it was in when the method was called.
        ///   </para>
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
        public static YamlNode? Parse(
            ref Utf8YamlReader reader,
            YamlNodeOptions? nodeOptions = null)
        {
            YamlElement element = YamlElement.ParseValue(ref reader);
            return YamlNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parses text representing a single YAML value.
        /// </summary>
        /// <param name="yaml">YAML text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="YamlNode"/> representation of the YAML value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="yaml"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="YamlException">
        ///   <paramref name="yaml"/> does not represent a valid single YAML value.
        /// </exception>
        public static YamlNode? Parse(
            [StringSyntax(StringSyntaxAttribute.Json)] string yaml,
            YamlNodeOptions? nodeOptions = null,
            YamlDocumentOptions documentOptions = default(YamlDocumentOptions))
        {
            ArgumentNullException.ThrowIfNull(yaml);

            YamlElement element;
            try
            {
                element = YamlElement.Parse(yaml, documentOptions);
            }
            catch (YamlException e) when (e.IsEmptyInput)
            {
                // Decision 2 (M5c): empty input is a null node, as Deserialize<YamlNode>("") gives.
                return null;
            }

            return YamlNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parses text representing a single YAML value.
        /// </summary>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="YamlNode"/> representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        public static YamlNode? Parse(
            ReadOnlySpan<byte> utf8Yaml,
            YamlNodeOptions? nodeOptions = null,
            YamlDocumentOptions documentOptions = default(YamlDocumentOptions))
        {
            YamlElement element;
            try
            {
                element = YamlElement.Parse(utf8Yaml, documentOptions);
            }
            catch (YamlException e) when (e.IsEmptyInput)
            {
                // Decision 2 (M5c): empty input is a null node, as Deserialize<YamlNode>("") gives.
                return null;
            }

            return YamlNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single YAML value into a
        ///   <see cref="YamlNode"/>.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="YamlNode"/> representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        public static YamlNode? Parse(
            Stream utf8Yaml,
            YamlNodeOptions? nodeOptions = null,
            YamlDocumentOptions documentOptions = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            YamlElement element;
            try
            {
                element = YamlElement.ParseValue(utf8Yaml, documentOptions);
            }
            catch (YamlException e) when (e.IsEmptyInput)
            {
                // Decision 2 (M5c): empty input is a null node, as Deserialize<YamlNode>("") gives.
                return null;
            }

            return YamlNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single YAML value into a
        ///   <see cref="YamlNode"/>.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Yaml">YAML text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        ///   A <see cref="Task"/> to produce a <see cref="YamlNode"/> representation of the YAML value.
        /// </returns>
        /// <exception cref="YamlException">
        ///   <paramref name="utf8Yaml"/> does not represent a valid single YAML value.
        /// </exception>
        public static async Task<YamlNode?> ParseAsync(
            Stream utf8Yaml,
            YamlNodeOptions? nodeOptions = null,
            YamlDocumentOptions documentOptions = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            YamlDocument document;
            try
            {
                document = await YamlDocument.ParseAsyncCoreUnrented(utf8Yaml, documentOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (YamlException e) when (e.IsEmptyInput)
            {
                return null;
            }

            return YamlNodeConverter.Create(document.RootElement, nodeOptions);
        }
    }
}
