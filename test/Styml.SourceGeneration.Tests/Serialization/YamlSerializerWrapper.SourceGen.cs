// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    internal sealed class StringSerializerWrapper : YamlSerializerWrapper
    {
        private readonly YamlSerializerContext _defaultContext;

        public StringSerializerWrapper(YamlSerializerContext defaultContext)
        {
            _defaultContext = defaultContext ?? throw new ArgumentNullException(nameof(defaultContext));
        }

        public override YamlSerializerOptions DefaultOptions => _defaultContext.Options;

        public override Task<string> SerializeWrapper(object value, Type type, YamlSerializerOptions? options = null)
            => Task.FromResult(YamlSerializer.Serialize(value, GetOptions(options).GetTypeInfo(type)));

        public override Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions? options = null)
            => Task.FromResult(YamlSerializer.Serialize(value, GetOptions(options).GetTypeInfo<T>()));

        public override Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context)
            => Task.FromResult(YamlSerializer.Serialize(value, inputType, context));

        public override Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo)
            => Task.FromResult(YamlSerializer.Serialize(value, yamlTypeInfo));

        public override Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo)
            => Task.FromResult(YamlSerializer.Serialize(value, yamlTypeInfo));

        public override Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions? options = null)
            => Task.FromResult(YamlSerializer.Deserialize(yaml, GetOptions(options).GetTypeInfo<T>()));

        public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions? options = null)
            => Task.FromResult(YamlSerializer.Deserialize(yaml, GetOptions(options).GetTypeInfo(type)));

        public override Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo)
            => Task.FromResult(YamlSerializer.Deserialize(yaml, yamlTypeInfo));

        public override Task<object> DeserializeWrapper(string yaml, YamlTypeInfo yamlTypeInfo)
            => Task.FromResult(YamlSerializer.Deserialize(yaml, yamlTypeInfo));

        public override Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context)
            => Task.FromResult(YamlSerializer.Deserialize(yaml, type, context));

        public override YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions? options = null, bool mutable = false) => base.GetTypeInfo(type, GetOptions(options), mutable);

        private YamlSerializerOptions GetOptions(YamlSerializerOptions? options = null)
        {
            if (options is null)
            {
                return _defaultContext.Options;
            }

            if (options.TypeInfoResolver is null or DefaultYamlTypeInfoResolver { Modifiers.Count: 0 })
            {
                return new YamlSerializerOptions(options) { TypeInfoResolver = _defaultContext };
            }

            return options;
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, GetOptions(options).GetTypeInfo<T>(), cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, yamlTypeInfo, cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, yamlTypeInfo, topLevelValues, cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, GetOptions(options).GetTypeInfo<T>(), topLevelValues, cancellationToken);
        }
    }

    internal sealed class AsyncStreamSerializerWrapper : StreamingYamlSerializerWrapper
    {
        private readonly YamlSerializerContext _defaultContext;

        public override YamlSerializerOptions DefaultOptions => _defaultContext.Options;
        public override bool IsAsyncSerializer => true;

        public AsyncStreamSerializerWrapper(YamlSerializerContext defaultContext)
        {
            _defaultContext = defaultContext ?? throw new ArgumentNullException(nameof(defaultContext));
        }

        public override async Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return await YamlSerializer.DeserializeAsync<T>(utf8Yaml, GetOptions(options).GetTypeInfo<T>(), cancellationToken);
        }

        public override async Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return await YamlSerializer.DeserializeAsync(utf8Yaml, GetOptions(options).GetTypeInfo(returnType), cancellationToken);
        }

        public override async Task<T> DeserializeWrapper<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
        {
            return await YamlSerializer.DeserializeAsync<T>(utf8Yaml, yamlTypeInfo, cancellationToken);
        }

        public override async Task<object> DeserializeWrapper(Stream utf8Yaml, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
        {
            return await YamlSerializer.DeserializeAsync(utf8Yaml, yamlTypeInfo, cancellationToken);
        }

        public override async Task<object> DeserializeWrapper(Stream utf8Yaml, Type returnType, YamlSerializerContext context, CancellationToken cancellationToken = default)
        {
            return await YamlSerializer.DeserializeAsync(utf8Yaml, returnType, context, cancellationToken);
        }

        public override Task SerializeWrapper<T>(Stream stream, T value, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            => YamlSerializer.SerializeAsync<T>(stream, value, GetOptions(options).GetTypeInfo<T>(), cancellationToken);

        public override Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
            => YamlSerializer.SerializeAsync(stream, value, GetOptions(options).GetTypeInfo(inputType), cancellationToken);

        public override Task SerializeWrapper<T>(Stream stream, T value, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
            => YamlSerializer.SerializeAsync(stream, value, yamlTypeInfo, cancellationToken);

        public override Task SerializeWrapper(Stream stream, object value, YamlTypeInfo yamlTypeInfo, CancellationToken cancellationToken = default)
            => YamlSerializer.SerializeAsync(stream, value, yamlTypeInfo, cancellationToken);

        public override Task SerializeWrapper(Stream stream, object value, Type inputType, YamlSerializerContext context, CancellationToken cancellationToken = default)
            => YamlSerializer.SerializeAsync(stream, value, inputType, context, cancellationToken);

        public override YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions? options = null, bool mutable = false) => base.GetTypeInfo(type, GetOptions(options), mutable);

        private YamlSerializerOptions GetOptions(YamlSerializerOptions? options = null)
        {
            if (options is null)
            {
                return _defaultContext.Options;
            }

            if (options.TypeInfoResolver is null or DefaultYamlTypeInfoResolver { Modifiers.Count: 0 })
            {
                return new YamlSerializerOptions(options) { TypeInfoResolver = _defaultContext };
            }

            return options;
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, GetOptions(options).GetTypeInfo<T>(), cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, yamlTypeInfo, cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, YamlTypeInfo<T> yamlTypeInfo, bool topLevelValues, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, yamlTypeInfo, topLevelValues, cancellationToken);
        }

        public override IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream utf8Yaml, bool topLevelValues, YamlSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return YamlSerializer.DeserializeAsyncEnumerable<T>(utf8Yaml, GetOptions(options).GetTypeInfo<T>(), topLevelValues, cancellationToken);
        }
    }
}
