// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Base class for abstracting YamlSerializer method families behind an async/string serialization facade.
    /// </summary>
    public abstract partial class YamlSerializerWrapper
    {
        /// <summary>
        /// Either YamlSerializerOptions.Default for reflection or the YamlSerializerContext.Options for source gen.
        /// </summary>
        public abstract YamlSerializerOptions DefaultOptions { get; }

        public bool IsSourceGeneratedSerializer => DefaultOptions.TypeInfoResolver is YamlSerializerContext;

        /// <summary>
        /// Do the deserialize methods allow a value of 'null'.
        /// For example, deserializing JSON to a String supports null by returning a 'null' String reference from a literal value of "null".
        /// </summary>
        public virtual bool SupportsNullValueOnDeserialize => false;

        public abstract Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerOptions options = null);

        public abstract Task<string> SerializeWrapper<T>(T value, YamlSerializerOptions options = null);

        public abstract Task<string> SerializeWrapper(object value, Type inputType, YamlSerializerContext context);

        public abstract Task<string> SerializeWrapper<T>(T value, YamlTypeInfo<T> yamlTypeInfo);

        public abstract Task<string> SerializeWrapper(object value, YamlTypeInfo yamlTypeInfo);

        public abstract Task<T> DeserializeWrapper<T>(string yaml, YamlSerializerOptions options = null);

        public abstract Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerOptions options = null);

        public abstract Task<T> DeserializeWrapper<T>(string yaml, YamlTypeInfo<T> yamlTypeInfo);

        public abstract Task<object> DeserializeWrapper(string value, YamlTypeInfo yamlTypeInfo);

        public abstract Task<object> DeserializeWrapper(string yaml, Type type, YamlSerializerContext context);


        public abstract IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
            Stream utf8Yaml,
            YamlSerializerOptions options = null,
            CancellationToken cancellationToken = default);

        public abstract IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
            Stream utf8Yaml,
            YamlTypeInfo<T> yamlTypeInfo,
            CancellationToken cancellationToken = default);

        public abstract IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
            Stream utf8Yaml,
            YamlTypeInfo<T> yamlTypeInfo,
            bool topLevelValues,
            CancellationToken cancellationToken = default);

        public abstract IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
            Stream utf8Yaml,
            bool topLevelValues,
            YamlSerializerOptions? options = null,
            CancellationToken cancellationToken = default);

        public virtual YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions? options = null, bool mutable = false)
        {
            options ??= DefaultOptions;
#if BUILDING_SOURCE_GENERATOR_TESTS
            // In the source generator test project the options are always backed by a
            // YamlSerializerContext resolver, so there is never a missing resolver to populate.
            // Use the parameterless overload which is safe for trimming and Native AOT.
            options.MakeReadOnly();
#else
            options.MakeReadOnly(populateMissingResolver: true);
#endif
            return mutable ? options.TypeInfoResolver.GetTypeInfo(type, options) : options.GetTypeInfo(type);
        }

        public YamlTypeInfo<T> GetTypeInfo<T>(YamlSerializerOptions? options = null, bool mutable = false)
            => (YamlTypeInfo<T>)GetTypeInfo(typeof(T), options, mutable);

        public YamlSerializerOptions GetDefaultOptionsWithMetadataModifier(Action<YamlTypeInfo> modifier)
        {
            YamlSerializerOptions defaultOptions = DefaultOptions;
            return new YamlSerializerOptions(defaultOptions)
            {
                TypeInfoResolver = defaultOptions.TypeInfoResolver.WithAddedModifier(modifier)
            };
        }

        public YamlSerializerOptions CreateOptions(
            Action<YamlSerializerOptions> configure = null,
            bool? includeFields = false,
            List<YamlConverter>? customConverters = null,
            Action<YamlTypeInfo>? modifier = null,
            bool makeReadOnly = true)
        {
            var options = new YamlSerializerOptions(DefaultOptions);

            if (includeFields != null)
            {
                options.IncludeFields = includeFields.Value;
            }

            if (modifier != null && options.TypeInfoResolver != null)
            {
                options.TypeInfoResolver = DefaultOptions.TypeInfoResolver.WithAddedModifier(modifier);
            }

            if (customConverters != null)
            {
                foreach (YamlConverter converter in customConverters)
                {
                    options.Converters.Add(converter);
                }
            }

            configure?.Invoke(options);

            if (makeReadOnly)
            {
                options.MakeReadOnly();
            }

            return options;
        }
    }
}
