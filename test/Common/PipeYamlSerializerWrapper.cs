// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO.Pipelines;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Base class for abstracting System.IO.Pipelines YamlSerializer method families.
    /// </summary>
    public abstract partial class PipeYamlSerializerWrapper : StreamingYamlSerializerWrapper
    {
        public abstract Task SerializeWrapper(PipeWriter stream, object value, Type inputType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper<T>(PipeWriter stream, T value, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper(PipeWriter stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper<T>(PipeWriter stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task SerializeWrapper(PipeWriter stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(PipeReader utf8Yaml, Type returnType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task<T> DeserializeWrapper<T>(PipeReader utf8Yaml, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(PipeReader utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default);
        public abstract Task<T> DeserializeWrapper<T>(PipeReader utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default);
        public abstract Task<object> DeserializeWrapper(PipeReader utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default);

        public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
        {
            Pipe pipe = new Pipe();
            await SerializeWrapper(pipe.Writer, value, inputType, options);
            ReadResult result = await pipe.Reader.ReadAsync();
            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }

        public override async Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
        {
            Pipe pipe = new Pipe();
            await SerializeWrapper(pipe.Writer, value, options);
            ReadResult result = await pipe.Reader.ReadAsync();
            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }

        public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
        {
            Pipe pipe = new Pipe();
            await SerializeWrapper(pipe.Writer, value, inputType, context);
            ReadResult result = await pipe.Reader.ReadAsync();
            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }

        public override async Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
        {
            Pipe pipe = new Pipe();
            await SerializeWrapper(pipe.Writer, value, yamlTypeInfo);
            ReadResult result = await pipe.Reader.ReadAsync();
            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }

        public override async Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
        {
            Pipe pipe = new Pipe();
            await SerializeWrapper(pipe.Writer, value, yamlTypeInfo);
            ReadResult result = await pipe.Reader.ReadAsync();
            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }

        public override async Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
            pipe.Writer.Complete();
            return await DeserializeWrapper<T>(pipe.Reader, options);
        }

        public override async Task<object> DeserializeWrapper(string yaml, Type returnType, YamlSerializerOptions options = null)
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
            pipe.Writer.Complete();
            return await DeserializeWrapper(pipe.Reader, returnType, options);
        }

        public override async Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
            pipe.Writer.Complete();
            return await DeserializeWrapper(pipe.Reader, yamlTypeInfo);
        }

        public override async Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
            pipe.Writer.Complete();
            return await DeserializeWrapper(pipe.Reader, yamlTypeInfo);
        }

        public override async Task<object> DeserializeWrapper(string yaml, Type returnType, YamlSerializerContext context)
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
            pipe.Writer.Complete();
            return await DeserializeWrapper(pipe.Reader, returnType, context);
        }
    }
}
