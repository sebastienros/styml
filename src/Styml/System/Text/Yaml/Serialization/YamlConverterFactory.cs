// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Supports converting several types by using a factory pattern.
    /// </summary>
    /// <remarks>
    /// This is useful for converters supporting generics, such as a converter for <see cref="System.Collections.Generic.List{T}"/>.
    /// </remarks>
    public abstract class YamlConverterFactory : YamlConverter
    {
        /// <summary>
        /// When overridden, constructs a new <see cref="YamlConverterFactory"/> instance.
        /// </summary>
        protected YamlConverterFactory() { }

        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.None;

        /// <summary>
        /// Create a converter for the provided <see cref="System.Type"/>.
        /// </summary>
        /// <param name="typeToConvert">The <see cref="System.Type"/> being converted.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> being used.</param>
        /// <returns>
        /// An instance of a <see cref="YamlConverter{T}"/> where T is compatible with <paramref name="typeToConvert"/>.
        /// If <see langword="null"/> is returned, a <see cref="NotSupportedException"/> will be thrown.
        /// </returns>
        public abstract YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options);

        internal YamlConverter GetConverterInternal(Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(CanConvert(typeToConvert));

            YamlConverter? converter = CreateConverter(typeToConvert, options);
            switch (converter)
            {
                case null:
                    ThrowHelper.ThrowInvalidOperationException_SerializerConverterFactoryReturnsNull(GetType());
                    break;
                case YamlConverterFactory:
                    ThrowHelper.ThrowInvalidOperationException_SerializerConverterFactoryReturnsYamlConverterFactory(GetType());
                    break;
            }

            return converter;
        }

        internal sealed override object? ReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool OnTryReadAsObject(
            ref Utf8YamlReader reader,
            Type typeToConvert,
            YamlSerializerOptions options,
            scoped ref ReadStack state,
            out object? value)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool TryReadAsObject(
            ref Utf8YamlReader reader,
            Type typeToConvert,
            YamlSerializerOptions options,
            scoped ref ReadStack state,
            out object? value)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadAsPropertyNameAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadAsPropertyNameCoreAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadNumberWithCustomHandlingAsObject(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool OnTryWriteAsObject(
            Utf8YamlWriter writer,
            object? value,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool TryWriteAsObject(
            Utf8YamlWriter writer,
            object? value,
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteAsPropertyNameAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public sealed override Type? Type => null;

        internal sealed override void WriteAsPropertyNameCoreAsObject(
            Utf8YamlWriter writer,
            object? value,
            YamlSerializerOptions options,
            bool isWritingExtensionDataProperty)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteNumberWithCustomHandlingAsObject(Utf8YamlWriter writer, object? value, YamlNumberHandling handling)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }
    }
}
