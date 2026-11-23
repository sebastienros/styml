// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Provides metadata about a set of types that is relevant to YAML serialization.
    /// </summary>
    public abstract partial class YamlSerializerContext : IYamlTypeInfoResolver, IBuiltInYamlTypeInfoResolver
    {
        private YamlSerializerOptions? _options;

        /// <summary>
        /// Gets the run time specified options of the context. If no options were passed
        /// when instantiating the context, then a new instance is bound and returned.
        /// </summary>
        /// <remarks>
        /// The options instance cannot be mutated once it is bound to the context instance.
        /// </remarks>
        public YamlSerializerOptions Options
        {
            get
            {
                YamlSerializerOptions? options = _options;

                if (options is null)
                {
                    options = new YamlSerializerOptions { TypeInfoResolver = this };
                    options.MakeReadOnly();
                    _options = options;
                }

                return options;
            }
        }

        internal void AssociateWithOptions(YamlSerializerOptions options)
        {
            Debug.Assert(!options.IsReadOnly);
            options.TypeInfoResolver = this;
            options.MakeReadOnly();
            _options = options;
        }

        /// <summary>
        /// Indicates whether pre-generated serialization logic for types in the context
        /// is compatible with the run time specified <see cref="YamlSerializerOptions"/>.
        /// </summary>
        bool IBuiltInYamlTypeInfoResolver.IsCompatibleWithOptions(YamlSerializerOptions options)
        {
            Debug.Assert(options is not null);

            YamlSerializerOptions? generatedSerializerOptions = GeneratedSerializerOptions;

            return
                generatedSerializerOptions is not null &&
                // Guard against unsupported features
                options.Converters.Count == 0 &&
                options.Encoder is null &&
                // Disallow custom number handling we'd need to honor when writing.
                // AllowReadingFromString and Strict are fine since there's no action to take when writing.
                !YamlHelpers.RequiresSpecialNumberHandlingOnWrite(options.NumberHandling) &&
                options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Unspecified &&
                // M7a: run-time tag mappings. The generated handlers
                // write a member's declared type, so a base-typed member holding a mapped type would lose its tag and
                // the derived members. (A context with [YamlTagMapping] has no fast path at all, M5d.)
                !options.HasTagMappings &&
                // M7b (fix A): DefaultScalarStyle styles a dictionary's string keys, which a generated dictionary handler
                // writes with the value in one call, out of the converter's reach; set at run time or by the attribute,
                // it takes the metadata path, which writes every key through its converter.
                options.DefaultScalarStyle == YamlScalarStyle.None &&

                // Ensure options values are consistent with expected defaults.
                options.DefaultIgnoreCondition == generatedSerializerOptions.DefaultIgnoreCondition &&
                options.RespectNullableAnnotations == generatedSerializerOptions.RespectNullableAnnotations &&
                options.IgnoreReadOnlyFields == generatedSerializerOptions.IgnoreReadOnlyFields &&
                options.IgnoreReadOnlyProperties == generatedSerializerOptions.IgnoreReadOnlyProperties &&
                options.IncludeFields == generatedSerializerOptions.IncludeFields &&
                options.PropertyNamingPolicy == generatedSerializerOptions.PropertyNamingPolicy &&
                // M5f: the fast path's member order is the generator's.
                options.MappingOrder == generatedSerializerOptions.MappingOrder &&
                options.DictionaryKeyPolicy is null;
        }

        /// <summary>
        /// The default run time options for the context. Its values are defined at design-time via <see cref="YamlSourceGenerationOptionsAttribute"/>.
        /// </summary>
        protected abstract YamlSerializerOptions? GeneratedSerializerOptions { get; }

        /// <summary>
        /// Creates an instance of <see cref="YamlSerializerContext"/> and binds it with the indicated <see cref="YamlSerializerOptions"/>.
        /// </summary>
        /// <param name="options">The run time provided options for the context instance.</param>
        /// <remarks>
        /// If no instance options are passed, then no options are set until <see cref="Options"/> is called, where a new
        /// options instance is created and bound.
        /// </remarks>
        protected YamlSerializerContext(YamlSerializerOptions? options)
        {
            if (options is not null)
            {
                options.VerifyMutable();
                AssociateWithOptions(options);
            }
        }

        /// <summary>
        /// Returns a <see cref="YamlTypeInfo"/> instance representing the given type.
        /// </summary>
        /// <param name="type">The type to fetch metadata about.</param>
        /// <returns>The metadata for the specified type, or <see langword="null" /> if the context has no metadata for the type.</returns>
        public abstract YamlTypeInfo? GetTypeInfo(Type type);

        YamlTypeInfo? IYamlTypeInfoResolver.GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            if (options is not null && options != _options)
            {
                ThrowHelper.ThrowInvalidOperationException_ResolverTypeInfoOptionsNotCompatible();
            }

            return GetTypeInfo(type);
        }
    }
}
