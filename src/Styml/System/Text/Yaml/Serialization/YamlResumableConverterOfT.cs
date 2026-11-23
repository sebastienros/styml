// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Base class for converters that are able to resume after reading or writing to a buffer.
    /// This is used when the Stream-based serialization APIs are used.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class YamlResumableConverter<T> : YamlConverter<T>
    {
        public override bool HandleNull => false;

        public sealed override T? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Bridge from resumable to value converters.

            ReadStack state = default;
            YamlTypeInfo yamlTypeInfo = options.GetTypeInfoInternal(typeToConvert);
            state.Initialize(yamlTypeInfo);

            TryRead(ref reader, typeToConvert, options, ref state, out T? value, out _);
            return value;
        }

        public sealed override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Bridge from resumable to value converters.
            WriteStack state = default;
            YamlTypeInfo typeInfo = options.GetTypeInfoInternal(typeof(T));
            state.Initialize(typeInfo);

            try
            {
                TryWrite(writer, value, options, ref state);
            }
            catch
            {
                state.DisposePendingDisposablesOnException();
                throw;
            }
        }
    }
}
