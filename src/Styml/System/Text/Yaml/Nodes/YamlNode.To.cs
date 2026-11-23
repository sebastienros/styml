// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Nodes
{
    public abstract partial class YamlNode
    {
        /// <summary>
        ///   Converts the current instance to a string in YAML, in the style the options give.
        /// </summary>
        /// <remarks>
        ///   A block-style writer keeps the source's styles (each collection's flow or block style, each scalar's and
        ///   key's quoting or block style) where it can hold the text in them, unless
        ///   <see cref="YamlSerializerOptions.IgnoreSourceStyles"/> is set, which writes in the options' styles.
        /// </remarks>
        /// <param name="options">Options to control the serialization behavior.</param>
        /// <returns>YAML representation of current instance.</returns>
        public string ToYamlString(YamlSerializerOptions? options = null)
        {
            YamlWriterOptions writerOptions = default;
            int defaultBufferSize = YamlSerializerOptions.BufferSizeDefault;
            if (options is not null)
            {
                writerOptions = options.GetWriterOptions();
                defaultBufferSize = options.DefaultBufferSize;
            }

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(writerOptions, defaultBufferSize, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer, options);
                writer.Flush();
                return Encoding.UTF8.GetString(output.WrittenSpan);
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        /// <summary>
        ///   Gets a string representation for the current value appropriate to the node type.
        /// </summary>
        /// <returns>A string representation for the current value appropriate to the node type.</returns>
        public override string ToString()
        {
            // Special case for string; don't quote it.
            if (this is YamlValue)
            {
                switch (this)
                {
                    case YamlValuePrimitive<string> yamlString:
                        return yamlString.Value;
                    case YamlValueOfElement { Value.ValueKind: YamlValueKind.String } yamlElement:
                        return yamlElement.Value.GetString()!;
                    case YamlValueOfYamlString yamlValueOfYamlString:
                        return yamlValueOfYamlString.GetValue<string>()!;
                }
            }

            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(new YamlWriterOptions { Indented = true }, YamlSerializerOptions.BufferSizeDefault, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer);
                writer.Flush();
                return Encoding.UTF8.GetString(output.WrittenSpan);
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        /// <summary>
        ///   Write the <see cref="YamlNode"/> into the provided <see cref="Utf8YamlWriter"/> as YAML.
        /// </summary>
        /// <remarks>
        ///   A block-style writer keeps the source's styles (each collection's flow or block style, each scalar's and
        ///   key's quoting or block style) where it can hold the text in them, unless
        ///   <see cref="YamlWriterOptions.IgnoreSourceStyles"/> is set, which writes in the writer's own styles.
        /// </remarks>
        /// <param name="writer">The <see cref="Utf8YamlWriter"/>.</param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <param name="options">Options to control the serialization behavior.</param>
        public abstract void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null);
    }
}
