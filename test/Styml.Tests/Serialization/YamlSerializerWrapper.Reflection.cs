// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class YamlSerializerWrapper
    {
        // Ensure that the reflection-based serializer testing abstraction roots KeyValuePair<,>
        // which is required by many tests in the reflection test suite.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(KeyValuePair<,>))]
        protected YamlSerializerWrapper()
        {
        }

        public static YamlSerializerWrapper SpanSerializer { get; } = new SpanSerializerWrapper();
        public static YamlSerializerWrapper StringSerializer { get; } = new StringSerializerWrapper();
        public static StreamingYamlSerializerWrapper AsyncStreamSerializer { get; } = new AsyncStreamSerializerWrapper();
        public static StreamingYamlSerializerWrapper AsyncStreamSerializerWithSmallBuffer { get; } = new AsyncStreamSerializerWrapper(forceSmallBufferInOptions: true, forceBomInsertions: true);
        public static StreamingYamlSerializerWrapper SyncStreamSerializer { get; } = new SyncStreamSerializerWrapper();
        public static StreamingYamlSerializerWrapper SyncStreamSerializerWithSmallBuffer { get; } = new SyncStreamSerializerWrapper(forceSmallBufferInOptions: true, forceBomInsertions: true);
        public static YamlSerializerWrapper ReaderWriterSerializer { get; } = new ReaderWriterSerializerWrapper();
        public static YamlSerializerWrapper DocumentSerializer { get; } = new DocumentSerializerWrapper();
        public static YamlSerializerWrapper ElementSerializer { get; } = new ElementSerializerWrapper();
        public static YamlSerializerWrapper NodeSerializer { get; } = new NodeSerializerWrapper();
        public static PipeYamlSerializerWrapper AsyncPipeSerializer { get; } = new AsyncPipelinesSerializerWrapper();
        public static PipeYamlSerializerWrapper AsyncPipeSerializerWithSmallBuffer { get; } = new AsyncPipelinesSerializerWrapper(forceSmallBufferInOptions: true);

        private class SpanSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool SupportsNullValueOnDeserialize => true; // a 'null' value is supported via implicit operator.

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                byte[] result = YamlSerializer.SerializeToUtf8Bytes(value, inputType, options);
                return Task.FromResult(Encoding.UTF8.GetString(result));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                byte[] result = YamlSerializer.SerializeToUtf8Bytes<T>(value, options);
                return Task.FromResult(Encoding.UTF8.GetString(result));
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                byte[] result = YamlSerializer.SerializeToUtf8Bytes(value, inputType, context);
                return Task.FromResult(Encoding.UTF8.GetString(result));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                byte[] result = YamlSerializer.SerializeToUtf8Bytes(value, yamlTypeInfo);
                return Task.FromResult(Encoding.UTF8.GetString(result));
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                byte[] result = YamlSerializer.SerializeToUtf8Bytes(value, yamlTypeInfo);
                return Task.FromResult(Encoding.UTF8.GetString(result));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Deserialize<T>(yaml.AsSpan(), options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml.AsSpan(), type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml.AsSpan(), yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml.AsSpan(), type, context));
            }

            public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml.AsSpan(), yamlTypeInfo));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class StringSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool SupportsNullValueOnDeserialize => true;

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Serialize(value, inputType, options));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Serialize(value, options));
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                return Task.FromResult(YamlSerializer.Serialize(value, inputType, context));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Serialize(value, yamlTypeInfo));
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Serialize(value, yamlTypeInfo));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Deserialize<T>(yaml, options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml, type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml, yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                return Task.FromResult(YamlSerializer.Deserialize(yaml, type, context));
            }

            public override Task<object> DeserializeWrapper(string value, YamlTypeInfo yamlTypeInfo)
            {
                return Task.FromResult(YamlSerializer.Deserialize(value, yamlTypeInfo));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class AsyncStreamSerializerWrapper : StreamingYamlSerializerWrapper
        {
            private readonly bool _forceSmallBufferInOptions;
            private readonly bool _forceBomInsertions;

            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool IsAsyncSerializer => true;
            public override bool ForceSmallBufferInOptions => _forceSmallBufferInOptions;

            public AsyncStreamSerializerWrapper(bool forceSmallBufferInOptions = false, bool forceBomInsertions = false)
            {
                _forceSmallBufferInOptions = forceSmallBufferInOptions;
                _forceBomInsertions = forceBomInsertions;
            }

            private YamlSerializerOptions? ResolveOptionsInstance(YamlSerializerOptions? options)
                => _forceSmallBufferInOptions ? YamlSerializerOptionsSmallBufferMapper.ResolveOptionsInstanceWithSmallBuffer(options) : options;

            private Stream ResolveReadStream(Stream stream)
                => stream is not null && _forceBomInsertions ? new Utf8BomInsertingStream(stream) : stream;

            public override Task SerializeWrapper<T>(Stream utf8Yaml, T value, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.SerializeAsync<T>(utf8Yaml, value, ResolveOptionsInstance(options), cancellationToken);
            }

            public override Task SerializeWrapper(Stream utf8Yaml, object value, Type inputType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.SerializeAsync(utf8Yaml, value, inputType, ResolveOptionsInstance(options), cancellationToken);
            }

            public override Task SerializeWrapper<T>(Stream stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.SerializeAsync(stream, value, yamlTypeInfo, cancellationToken);
            }

            public override Task SerializeWrapper(Stream stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.SerializeAsync(stream, value, yamlTypeInfo, cancellationToken);
            }

            public override Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.SerializeAsync(stream, value, inputType, context, cancellationToken);
            }

            public override async Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                return await YamlSerializer.DeserializeAsync<T>(ResolveReadStream(utf8Yaml), ResolveOptionsInstance(options), cancellationToken);
            }

            public override async Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                return await YamlSerializer.DeserializeAsync(ResolveReadStream(utf8Yaml), returnType, ResolveOptionsInstance(options), cancellationToken);
            }

            public override async Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return await YamlSerializer.DeserializeAsync<T>(ResolveReadStream(utf8Yaml), yamlTypeInfo, cancellationToken);
            }

            public override async Task<object> DeserializeWrapper(Stream utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return await YamlSerializer.DeserializeAsync(ResolveReadStream(utf8Yaml), yamlTypeInfo, cancellationToken);
            }

            public override async Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                return await YamlSerializer.DeserializeAsync(ResolveReadStream(utf8Yaml), returnType, context, cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsyncEnumerable<T>(ResolveReadStream(utf8Yaml), ResolveOptionsInstance(options), cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsyncEnumerable<T>(ResolveReadStream(utf8Yaml), yamlTypeInfo, cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsyncEnumerable<T>(ResolveReadStream(utf8Yaml), yamlTypeInfo, topLevelValues, cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsyncEnumerable<T>(ResolveReadStream(utf8Yaml), topLevelValues, ResolveOptionsInstance(options), cancellationToken);
            }
        }

        private class SyncStreamSerializerWrapper : StreamingYamlSerializerWrapper
        {
            private readonly bool _forceSmallBufferInOptions;
            private readonly bool _forceBomInsertions;

            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool IsAsyncSerializer => false;
            public override bool ForceSmallBufferInOptions => _forceSmallBufferInOptions;

            public SyncStreamSerializerWrapper(bool forceSmallBufferInOptions = false, bool forceBomInsertions = false)
            {
                _forceSmallBufferInOptions = forceSmallBufferInOptions;
                _forceBomInsertions = forceBomInsertions;
            }

            private YamlSerializerOptions? ResolveOptionsInstance(YamlSerializerOptions? options)
                => _forceSmallBufferInOptions ? YamlSerializerOptionsSmallBufferMapper.ResolveOptionsInstanceWithSmallBuffer(options) : options;

            private Stream ResolveReadStream(Stream stream)
                => stream is not null && _forceBomInsertions ? new Utf8BomInsertingStream(stream) : stream;

            public override Task SerializeWrapper<T>(Stream utf8Yaml, T value, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                YamlSerializer.Serialize(utf8Yaml, value, ResolveOptionsInstance(options));
                return Task.CompletedTask;
            }

            public override Task SerializeWrapper(Stream utf8Yaml, object value, Type inputType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                YamlSerializer.Serialize(utf8Yaml, value, inputType, ResolveOptionsInstance(options));
                return Task.CompletedTask;
            }

            public override Task SerializeWrapper<T>(Stream stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                YamlSerializer.Serialize(stream, value, yamlTypeInfo);
                return Task.CompletedTask;
            }

            public override Task SerializeWrapper(Stream stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                YamlSerializer.Serialize(stream, value, yamlTypeInfo);
                return Task.CompletedTask;
            }

            public override Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                YamlSerializer.Serialize(stream, value, inputType, context);
                return Task.CompletedTask;
            }

            public override Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                T result = YamlSerializer.Deserialize<T>(ResolveReadStream(utf8Yaml), ResolveOptionsInstance(options));
                return Task.FromResult(result);
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                object result = YamlSerializer.Deserialize(ResolveReadStream(utf8Yaml), returnType, ResolveOptionsInstance(options));
                return Task.FromResult(result);
            }

            public override Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                T result = YamlSerializer.Deserialize<T>(ResolveReadStream(utf8Yaml), yamlTypeInfo);
                return Task.FromResult(result);
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                object result = YamlSerializer.Deserialize(ResolveReadStream(utf8Yaml), yamlTypeInfo);
                return Task.FromResult(result);
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                object result = YamlSerializer.Deserialize(ResolveReadStream(utf8Yaml), returnType, context);
                return Task.FromResult(result);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class ReaderWriterSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(options)))
                {
                    YamlSerializer.Serialize(writer, value, inputType, options);
                }

                return Task.FromResult(Encoding.UTF8.GetString(stream.ToArray()));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(options)))
                {
                    YamlSerializer.Serialize<T>(writer, value, options);
                }

                return Task.FromResult(Encoding.UTF8.GetString(stream.ToArray()));
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(context?.Options)))
                {
                    YamlSerializer.Serialize(writer, value, inputType, context);
                }

                return Task.FromResult(Encoding.UTF8.GetString(stream.ToArray()));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(yamlTypeInfo?.Options)))
                {
                    YamlSerializer.Serialize(writer, value, yamlTypeInfo);
                }

                return Task.FromResult(Encoding.UTF8.GetString(stream.ToArray()));
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(yamlTypeInfo?.Options)))
                {
                    YamlSerializer.Serialize(writer, value, yamlTypeInfo);
                }

                return Task.FromResult(Encoding.UTF8.GetString(stream.ToArray()));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), OptionsHelpers.GetReaderOptions(options));
                return Task.FromResult(YamlSerializer.Deserialize<T>(ref reader, options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), OptionsHelpers.GetReaderOptions(options));
                return Task.FromResult(YamlSerializer.Deserialize(ref reader, type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), OptionsHelpers.GetReaderOptions(yamlTypeInfo?.Options));
                return Task.FromResult(YamlSerializer.Deserialize(ref reader, yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            {
                Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), OptionsHelpers.GetReaderOptions(yamlTypeInfo?.Options));
                return Task.FromResult(YamlSerializer.Deserialize(ref reader, yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                Utf8YamlReader reader = new(Encoding.UTF8.GetBytes(yaml), OptionsHelpers.GetReaderOptions(context?.Options));
                return Task.FromResult(YamlSerializer.Deserialize(ref reader, type, context));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class DocumentSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                YamlDocument document = YamlSerializer.SerializeToDocument(value, inputType, options);
                return Task.FromResult(GetStringFromDocument(document));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                YamlDocument document = YamlSerializer.SerializeToDocument(value, options);
                return Task.FromResult(GetStringFromDocument(document));
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                YamlDocument document = YamlSerializer.SerializeToDocument(value, inputType, context);
                return Task.FromResult(GetStringFromDocument(document));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                YamlDocument document = YamlSerializer.SerializeToDocument(value, yamlTypeInfo);
                return Task.FromResult(GetStringFromDocument(document));
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                YamlDocument document = YamlSerializer.SerializeToDocument(value, yamlTypeInfo);
                return Task.FromResult(GetStringFromDocument(document));
            }

            private string GetStringFromDocument(YamlDocument document)
            {
                // Emulate a null return value.
                if (document is null)
                {
                    return "null";
                }

                using MemoryStream stream = new();
                using (Utf8YamlWriter writer = new(stream))
                {
                    document.WriteTo(writer);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize<T>(document: null, options));
                }

                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(document.Deserialize<T>(options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(document: null, type, options));
                }

                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(document.Deserialize(type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize<T>(document: null, yamlTypeInfo));
                }

                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(document.Deserialize(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(document: null, yamlTypeInfo));
                }

                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(document.Deserialize(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(document: null, type, context));
                }

                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(context?.Options));
                return Task.FromResult(document.Deserialize(type, context));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class ElementSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                YamlElement element = YamlSerializer.SerializeToElement(value, inputType, options);
                return Task.FromResult(GetStringFromElement(element, options));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                YamlElement element = YamlSerializer.SerializeToElement(value, options);
                return Task.FromResult(GetStringFromElement(element, options));
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                YamlElement element = YamlSerializer.SerializeToElement(value, inputType, context);
                return Task.FromResult(GetStringFromElement(element, context?.Options));
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                YamlElement element = YamlSerializer.SerializeToElement(value, yamlTypeInfo);
                return Task.FromResult(GetStringFromElement(element, yamlTypeInfo?.Options));
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                YamlElement element = YamlSerializer.SerializeToElement(value, yamlTypeInfo);
                return Task.FromResult(GetStringFromElement(element, yamlTypeInfo?.Options));
            }

            private string GetStringFromElement(YamlElement element, YamlSerializerOptions options)
            {
                using MemoryStream stream = new MemoryStream();
                using (Utf8YamlWriter writer = new(stream, OptionsHelpers.GetWriterOptions(options)))
                {
                    element.WriteTo(writer);
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(document.RootElement.Deserialize<T>(options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(document.RootElement.Deserialize(type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(document.RootElement.Deserialize<T>(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            {
                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(document.RootElement.Deserialize(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                using YamlDocument document = YamlDocument.Parse(yaml, OptionsHelpers.GetDocumentOptions(context?.Options));
                return Task.FromResult(document.RootElement.Deserialize(type, context));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class NodeSerializerWrapper : YamlSerializerWrapper
        {
            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool SupportsNullValueOnDeserialize => true;

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                YamlNode node = YamlSerializer.SerializeToNode(value, inputType, options);

                // Emulate a null return value.
                if (node is null)
                {
                    return Task.FromResult("null");
                }

                return Task.FromResult(node.ToYamlString());
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                YamlNode node = YamlSerializer.SerializeToNode(value, options);

                // Emulate a null return value.
                if (node is null)
                {
                    return Task.FromResult("null");
                }

                return Task.FromResult(node.ToYamlString());
            }

            public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                YamlNode node = YamlSerializer.SerializeToNode(value, inputType, context);

                // Emulate a null return value.
                if (node is null)
                {
                    return Task.FromResult("null");
                }

                return Task.FromResult(node.ToYamlString());
            }

            public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                YamlNode node = YamlSerializer.SerializeToNode(value, yamlTypeInfo);

                // Emulate a null return value.
                if (node is null)
                {
                    return Task.FromResult("null");
                }

                return Task.FromResult(node.ToYamlString());
            }

            public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                YamlNode node = YamlSerializer.SerializeToNode(value, yamlTypeInfo);

                // Emulate a null return value.
                if (node is null)
                {
                    return Task.FromResult("null");
                }

                return Task.FromResult(node.ToYamlString());
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                if (yaml is null)
                {
                    // Emulate a null node for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize<T>(node: null, options));
                }

                YamlNode node = YamlNode.Parse(yaml, OptionsHelpers.GetNodeOptions(options), OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(node.Deserialize<T>(options));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                if (yaml is null)
                {
                    // Emulate a null node for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(node: null, type, options));
                }

                YamlNode node = YamlNode.Parse(yaml, OptionsHelpers.GetNodeOptions(options), OptionsHelpers.GetDocumentOptions(options));
                return Task.FromResult(node.Deserialize(type, options));
            }

            public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                if (yaml is null)
                {
                    // Emulate a null node for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(node: null, yamlTypeInfo));
                }

                YamlNode node = YamlNode.Parse(yaml, OptionsHelpers.GetNodeOptions(yamlTypeInfo?.Options), OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(node.Deserialize<T>(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            {
                if (yaml is null)
                {
                    // Emulate a null node for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(node: null, yamlTypeInfo));
                }

                YamlNode node = YamlNode.Parse(yaml, OptionsHelpers.GetNodeOptions(yamlTypeInfo?.Options), OptionsHelpers.GetDocumentOptions(yamlTypeInfo?.Options));
                return Task.FromResult(node.Deserialize(yamlTypeInfo));
            }

            public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                if (yaml is null)
                {
                    // Emulate a null document for API validation tests.
                    return Task.FromResult(YamlSerializer.Deserialize(node: null, type, context?.Options));
                }

                YamlNode node = YamlNode.Parse(yaml, OptionsHelpers.GetNodeOptions(context?.Options), OptionsHelpers.GetDocumentOptions(context?.Options));
                return Task.FromResult(node.Deserialize(type, context));
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private static class OptionsHelpers
        {
            public static YamlWriterOptions GetWriterOptions(YamlSerializerOptions? options)
            {
                return options is null
                    ? default
                    : new YamlWriterOptions
                    {
                        Encoder = options.Encoder,
                        Indented = options.WriteIndented,
                        MaxDepth = GetEffectiveMaxDepth(options),
    #if !DEBUG
                            SkipValidation = true
    #endif
                    };
            }

            public static YamlReaderOptions GetReaderOptions(YamlSerializerOptions? options)
            {
                return options is null
                    ? default
                    : new YamlReaderOptions
                    {
                        AllowTrailingCommas = options.AllowTrailingCommas,
                        CommentHandling = options.ReadCommentHandling,
                        MaxDepth = GetEffectiveMaxDepth(options),
                    };
            }

            public static YamlDocumentOptions GetDocumentOptions(YamlSerializerOptions? options)
            {
                // Styml (M5c): the document keeps repeated keys (LastWins), so that the serializer under test judges
                // them, not the DOM's parse under the Error default.
                return options is null
                    ? new YamlDocumentOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }
                    : new YamlDocumentOptions
                    {
                        MaxDepth = GetEffectiveMaxDepth(options),
                        AllowTrailingCommas = options.AllowTrailingCommas,
                        CommentHandling = options.ReadCommentHandling,
                        DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                    };
            }

            public static YamlNodeOptions GetNodeOptions(YamlSerializerOptions? options)
            {
                return options is null
                    ? default
                    : new YamlNodeOptions
                    {
                        PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive
                    };
            }

            private static int GetEffectiveMaxDepth(YamlSerializerOptions options) => options.MaxDepth == 0 ? 64 : options.MaxDepth;
        }

        /// <summary>
        /// Maintains an index of equivalent YamlSerializerOptions instances with DefaultBufferSize = 1
        /// </summary>
        private static class YamlSerializerOptionsSmallBufferMapper
        {
            private static readonly ConditionalWeakTable<YamlSerializerOptions, YamlSerializerOptions> s_smallBufferMap = new();
            private static readonly YamlSerializerOptions s_DefaultOptionsWithSmallBuffer = new YamlSerializerOptions { DefaultBufferSize = 1 };

            public static YamlSerializerOptions ResolveOptionsInstanceWithSmallBuffer(YamlSerializerOptions? options)
            {
                if (options == null || options == YamlSerializerOptions.Default)
                {
                    return s_DefaultOptionsWithSmallBuffer;
                }

                if (options.DefaultBufferSize == 1)
                {
                    return options;
                }

                return s_smallBufferMap.GetValue(
                    options,
                    static src => new YamlSerializerOptions(src) { DefaultBufferSize = 1 });
            }
        }

        private sealed class Utf8BomInsertingStream : Stream
        {
            private const int Utf8BomLength = 3;
            private readonly static byte[] s_utf8Bom = Encoding.UTF8.GetPreamble();

            private readonly Stream _source;
            private byte[]? _prefixBytes;
            private int _prefixBytesOffset = 0;
            private int _prefixBytesCount = 0;

            public Utf8BomInsertingStream(Stream source)
            {
                Debug.Assert(source.CanRead);
                _source = source;
            }

            public override bool CanRead => _source.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_prefixBytes is null)
                {
                    // This is the first read operation; read the first 3 bytes
                    // from the source to determine if it already includes a BOM.
                    // Only insert a BOM if it's missing from the source stream.

                    _prefixBytes = new byte[2 * Utf8BomLength];
                    int bytesRead = ReadExactlyFromSource(_prefixBytes, Utf8BomLength, Utf8BomLength);

                    if (_prefixBytes.AsSpan(Utf8BomLength).SequenceEqual(s_utf8Bom))
                    {
                        _prefixBytesOffset = Utf8BomLength;
                        _prefixBytesCount = Utf8BomLength;
                    }
                    else
                    {
                        s_utf8Bom.CopyTo(_prefixBytes, 0);
                        _prefixBytesOffset = 0;
                        _prefixBytesCount = Utf8BomLength + bytesRead;
                    }
                }

                int prefixBytesToWrite = Math.Min(_prefixBytesCount, count);
                if (prefixBytesToWrite > 0)
                {
                    _prefixBytes.AsSpan(_prefixBytesOffset, prefixBytesToWrite).CopyTo(buffer.AsSpan(offset, count));
                    _prefixBytesOffset += prefixBytesToWrite;
                    _prefixBytesCount -= prefixBytesToWrite;
                    offset += prefixBytesToWrite;
                    count -= prefixBytesToWrite;
                }

                return prefixBytesToWrite + _source.Read(buffer, offset, count);
            }

            private int ReadExactlyFromSource(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;

                while (totalRead < count)
                {
                    int read = _source.Read(buffer, offset + totalRead, count - totalRead);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                return totalRead;
            }

            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class AsyncPipelinesSerializerWrapper : PipeYamlSerializerWrapper
        {
            private readonly bool _forceSmallBufferInOptions;

            public override YamlSerializerOptions DefaultOptions => YamlSerializerOptions.Default;
            public override bool SupportsNullValueOnDeserialize => true;

            public override bool IsAsyncSerializer => true;

            public AsyncPipelinesSerializerWrapper(bool forceSmallBufferInOptions = false)
            {
                _forceSmallBufferInOptions = forceSmallBufferInOptions;
            }

            private YamlSerializerOptions? ResolveOptionsInstance(YamlSerializerOptions? options)
                => _forceSmallBufferInOptions ? YamlSerializerOptionsSmallBufferMapper.ResolveOptionsInstanceWithSmallBuffer(options) : options;

            public override async Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
                pipe.Writer.Complete();
                return await YamlSerializer.DeserializeAsync<T>(pipe.Reader, ResolveOptionsInstance(options));
            }

            public override async Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
                pipe.Writer.Complete();
                return await YamlSerializer.DeserializeAsync(pipe.Reader, type, ResolveOptionsInstance(options));
            }

            public override async Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
                pipe.Writer.Complete();
                return await YamlSerializer.DeserializeAsync(pipe.Reader, yamlTypeInfo);
            }

            public override async Task<object> DeserializeWrapper(string value, YamlTypeInfo yamlTypeInfo)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(value));
                pipe.Writer.Complete();
                return await YamlSerializer.DeserializeAsync(pipe.Reader, yamlTypeInfo);
            }

            public override async Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(yaml));
                pipe.Writer.Complete();
                return await YamlSerializer.DeserializeAsync(pipe.Reader, type, context);
            }

            public override Task<object> DeserializeWrapper(PipeReader utf8Yaml, Type returnType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsync(utf8Yaml, returnType, ResolveOptionsInstance(options), cancellationToken).AsTask();
            }

            public override Task<T> DeserializeWrapper<T>(PipeReader utf8Yaml, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsync<T>(utf8Yaml, ResolveOptionsInstance(options), cancellationToken).AsTask();
            }

            public override Task<object> DeserializeWrapper(PipeReader utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsync(utf8Yaml, returnType, context, cancellationToken).AsTask();
            }

            public override Task<T> DeserializeWrapper<T>(PipeReader utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsync(utf8Yaml, yamlTypeInfo, cancellationToken).AsTask();
            }

            public override Task<object> DeserializeWrapper(PipeReader utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                return YamlSerializer.DeserializeAsync(utf8Yaml, yamlTypeInfo, cancellationToken).AsTask();
            }

            public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await YamlSerializer.SerializeAsync(pipe.Writer, value, inputType, ResolveOptionsInstance(options));
                ReadResult result = await pipe.Reader.ReadAsync();

                string stringResult = Encoding.UTF8.GetString(result.Buffer.ToArray());
                pipe.Reader.AdvanceTo(result.Buffer.End);
                return stringResult;
            }

            public override async Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await YamlSerializer.SerializeAsync(pipe.Writer, value, ResolveOptionsInstance(options));
                ReadResult result = await pipe.Reader.ReadAsync();

                string stringResult = Encoding.UTF8.GetString(result.Buffer.ToArray());
                pipe.Reader.AdvanceTo(result.Buffer.End);
                return stringResult;
            }

            public override async Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await YamlSerializer.SerializeAsync(pipe.Writer, value, inputType, context);
                ReadResult result = await pipe.Reader.ReadAsync();

                string stringResult = Encoding.UTF8.GetString(result.Buffer.ToArray());
                pipe.Reader.AdvanceTo(result.Buffer.End);
                return stringResult;
            }

            public override async Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await YamlSerializer.SerializeAsync(pipe.Writer, value, yamlTypeInfo);
                ReadResult result = await pipe.Reader.ReadAsync();

                string stringResult = Encoding.UTF8.GetString(result.Buffer.ToArray());
                pipe.Reader.AdvanceTo(result.Buffer.End);
                return stringResult;
            }

            public override async Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            {
                Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
                await YamlSerializer.SerializeAsync(pipe.Writer, value, yamlTypeInfo);
                ReadResult result = await pipe.Reader.ReadAsync();

                string stringResult = Encoding.UTF8.GetString(result.Buffer.ToArray());
                pipe.Reader.AdvanceTo(result.Buffer.End);
                return stringResult;
            }

            public override async Task SerializeWrapper(PipeWriter utf8Yaml, object value, Type inputType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                try
                {
                    await YamlSerializer.SerializeAsync(utf8Yaml, value, inputType, ResolveOptionsInstance(options), cancellationToken);
                }
                finally
                {
                    await utf8Yaml.FlushAsync(cancellationToken);
                }
            }

            public override async Task SerializeWrapper<T>(PipeWriter utf8Yaml, T value, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                try
                {
                    await YamlSerializer.SerializeAsync(utf8Yaml, value, ResolveOptionsInstance(options), cancellationToken);
                }
                finally
                {
                    await utf8Yaml.FlushAsync(cancellationToken);
                }
            }

            public override async Task SerializeWrapper(PipeWriter utf8Yaml, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                try
                {
                    await YamlSerializer.SerializeAsync(utf8Yaml, value, inputType, context, cancellationToken);
                }
                finally
                {
                    await utf8Yaml.FlushAsync(cancellationToken);
                }
            }

            public override async Task SerializeWrapper<T>(PipeWriter utf8Yaml, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                try
                {
                    await YamlSerializer.SerializeAsync(utf8Yaml, value, yamlTypeInfo, cancellationToken);
                }
                finally
                {
                    await utf8Yaml.FlushAsync(cancellationToken);
                }
            }

            public override async Task SerializeWrapper(PipeWriter utf8Yaml, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                try
                {
                    await YamlSerializer.SerializeAsync(utf8Yaml, value, yamlTypeInfo, cancellationToken);
                }
                finally
                {
                    await utf8Yaml.FlushAsync(cancellationToken);
                }
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true, bufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1));
                return YamlSerializer.DeserializeAsyncEnumerable<T>(pipeReader, ResolveOptionsInstance(options), cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true, bufferSize: yamlTypeInfo.Options.DefaultBufferSize));
                return YamlSerializer.DeserializeAsyncEnumerable<T>(pipeReader, yamlTypeInfo, cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true, bufferSize: yamlTypeInfo.Options.DefaultBufferSize));
                return YamlSerializer.DeserializeAsyncEnumerable<T>(pipeReader, yamlTypeInfo, topLevelValues, cancellationToken);
            }

            public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true, bufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1));
                return YamlSerializer.DeserializeAsyncEnumerable<T>(pipeReader, topLevelValues, ResolveOptionsInstance(options), cancellationToken);
            }

            public override async Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                PipeWriter writer = PipeWriter.Create(stream,
                    new StreamPipeWriterOptions(minimumBufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1, leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsync(writer, value, inputType, ResolveOptionsInstance(options), cancellationToken);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }

            public override async Task SerializeWrapper<T>(Stream stream, T value, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                PipeWriter writer = PipeWriter.Create(stream,
                    new StreamPipeWriterOptions(minimumBufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1, leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsync(writer, value, ResolveOptionsInstance(options), cancellationToken);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }

            public override async Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsync(writer, value, inputType, context, cancellationToken);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }

            public override async Task SerializeWrapper<T>(Stream stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsync(writer, value, yamlTypeInfo, cancellationToken);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }

            public override async Task SerializeWrapper(Stream stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
                try
                {
                    await YamlSerializer.SerializeAsync(writer, value, yamlTypeInfo, cancellationToken);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml,
                    new StreamPipeReaderOptions(leaveOpen: true, bufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1));
                return YamlSerializer.DeserializeAsync(pipeReader, returnType, ResolveOptionsInstance(options), cancellationToken).AsTask();
            }

            public override Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml,
                    new StreamPipeReaderOptions(leaveOpen: true, bufferSize: ResolveOptionsInstance(options)?.DefaultBufferSize ?? -1));
                return YamlSerializer.DeserializeAsync<T>(pipeReader, ResolveOptionsInstance(options), cancellationToken).AsTask();
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true));
                return YamlSerializer.DeserializeAsync(pipeReader, returnType, context, cancellationToken).AsTask();
            }

            public override Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true));
                return YamlSerializer.DeserializeAsync<T>(pipeReader, yamlTypeInfo, cancellationToken).AsTask();
            }

            public override Task<object> DeserializeWrapper(Stream utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            {
                var pipeReader = PipeReader.Create(utf8Yaml, new StreamPipeReaderOptions(leaveOpen: true));
                return YamlSerializer.DeserializeAsync(pipeReader, yamlTypeInfo, cancellationToken).AsTask();
            }
        }
    }
}
