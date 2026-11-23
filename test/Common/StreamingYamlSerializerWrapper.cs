// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Base class for abstracting streaming YamlSerializer method families.
    /// </summary>
    public abstract partial class StreamingYamlSerializerWrapper : YamlSerializerWrapper
    {
        /// <summary>
        /// True if the serializer is streaming data synchronously.
        /// </summary>
        public abstract bool IsAsyncSerializer { get; }
        public virtual bool ForceSmallBufferInOptions { get; } = false;

        public abstract Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper<T>(Stream stream, T value, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper<T>(Stream stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper(Stream stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default);
        public abstract Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(Stream utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default);

        public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
        {
            using var utf8Stream = new Utf8MemoryStream();
            await SerializeWrapper(utf8Stream, value, inputType, options);
            return utf8Stream.AsString();
        }

        public override async Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
        {
            using var utf8Stream = new Utf8MemoryStream();
            await SerializeWrapper(utf8Stream, value, options);
            return utf8Stream.AsString();
        }

        public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
        {
            using var utf8Stream = new Utf8MemoryStream();
            await SerializeWrapper(utf8Stream, value, inputType, context);
            return utf8Stream.AsString();
        }

        public override async Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
        {
            using var utf8Stream = new Utf8MemoryStream();
            await SerializeWrapper(utf8Stream, value, yamlTypeInfo);
            return utf8Stream.AsString();
        }

        public override async Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
        {
            using var utf8Stream = new Utf8MemoryStream();
            await SerializeWrapper(utf8Stream, value, yamlTypeInfo);
            return utf8Stream.AsString();
        }

        public override async Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
        {
            using var utf8Stream = new Utf8MemoryStream(yaml);
            return await DeserializeWrapper<T>(utf8Stream, options);
        }

        public override async Task<object> DeserializeWrapper(string yaml, Type returnType, YamlSerializerOptions options = null)
        {
            using var utf8Stream = new Utf8MemoryStream(yaml);
            return await DeserializeWrapper(utf8Stream, returnType, options);
        }

        public override async Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
        {
            using var utf8Stream = new Utf8MemoryStream(yaml);
            return await DeserializeWrapper(utf8Stream, yamlTypeInfo);
        }

        public override async Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
        {
            using var utf8Stream = new Utf8MemoryStream(yaml);
            return await DeserializeWrapper(utf8Stream, yamlTypeInfo);
        }

        public override async Task<object> DeserializeWrapper(string yaml, Type returnType, YamlSerializerContext context)
        {
            using var utf8Stream = new Utf8MemoryStream(yaml);
            return await DeserializeWrapper(utf8Stream, returnType, context);
        }
    }
}
