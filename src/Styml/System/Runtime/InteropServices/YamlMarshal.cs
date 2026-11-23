// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of YAML types.
    /// </summary>
    public static class YamlMarshal
    {
        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw YAML data of the given <see cref="YamlElement"/>.
        /// </summary>
        /// <param name="element">The YAML element from which to extract the span.</param>
        /// <returns>The span containing the raw YAML data of<paramref name="element"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="YamlDocument"/> has been disposed.</exception>
        /// <remarks>
        /// While the method itself does check for disposal of the underlying <see cref="YamlDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8Value(YamlElement element)
        {
            return element.GetRawValue().Span;
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw YAML data of the given <see cref="YamlProperty"/> name.
        /// </summary>
        /// <param name="property">The YAML property from which to extract the span.</param>
        /// <returns>The span containing the raw YAML data of the <paramref name="property"/> name. This will not include the enclosing quotes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="YamlDocument"/> has been disposed.</exception>
        /// <remarks>
        /// <para>
        /// While the method itself does check for disposal of the underlying <see cref="YamlDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </para>
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8PropertyName(YamlProperty property)
        {
            return property.NameSpan;
        }
    }
}
