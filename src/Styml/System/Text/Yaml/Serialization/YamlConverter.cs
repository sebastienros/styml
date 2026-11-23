// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization.Converters;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Converts an object or value to or from YAML.
    /// </summary>
    public abstract partial class YamlConverter
    {
        /// <summary>
        /// M5f: whether <paramref name="value"/> is a collection whose count,
        /// known without enumerating it, is 0; null when this converter cannot tell (it is not a collection converter, or
        /// the collection's count is not known).
        /// </summary>
        internal virtual bool? IsEmptyCollection(object value) => null;

        internal YamlConverter()
        {
            IsInternalConverter = GetType().Assembly == typeof(YamlConverter).Assembly;
            ConverterStrategy = GetDefaultConverterStrategy();
        }

        /// <summary>
        /// Gets the type being converted by the current converter instance.
        /// </summary>
        /// <remarks>
        /// For instances of type <see cref="YamlConverter{T}"/> returns typeof(T),
        /// and for instances of type <see cref="YamlConverterFactory"/> returns <see langword="null" />.
        /// </remarks>
        public abstract Type? Type { get; }

        /// <summary>
        /// Determines whether the type can be converted.
        /// </summary>
        /// <param name="typeToConvert">The type is checked as to whether it can be converted.</param>
        /// <returns>True if the type can be converted, false otherwise.</returns>
        public abstract bool CanConvert(Type typeToConvert);

        internal ConverterStrategy ConverterStrategy
        {
            get;
            init
            {
                CanUseDirectReadOrWrite = value == ConverterStrategy.Value && IsInternalConverter;
                RequiresReadAhead = value == ConverterStrategy.Value;
                field = value;
            }
        }

        /// <summary>
        /// Invoked by the base contructor to populate the initial value of the <see cref="ConverterStrategy"/> property.
        /// Used for declaring the default strategy for specific converter hierarchies without explicitly setting in a constructor.
        /// </summary>
        private protected abstract ConverterStrategy GetDefaultConverterStrategy();

        /// <summary>
        /// Indicates that the converter can consume the <see cref="YamlTypeInfo.CreateObject"/> delegate.
        /// Needed because certain collection converters cannot support arbitrary delegates.
        /// TODO remove once https://github.com/dotnet/runtime/pull/73395/ and
        /// https://github.com/dotnet/runtime/issues/71944 have been addressed.
        /// </summary>
        internal virtual bool SupportsCreateObjectDelegate => false;

        /// <summary>
        /// The converter reads any node as written (<see cref="object"/> and the YAML document and node types), so it
        /// sees a <c>NuLL</c> scalar as the string it is even with <see cref="YamlReaderOptions.AcceptLenientScalars"/>.
        /// Consulted only when <see cref="Utf8YamlReader.IsLenientNull"/> holds.
        /// </summary>
        internal virtual bool IsUntypedConverter => false;

        /// <summary>
        /// With <see cref="YamlReaderOptions.AcceptLenientScalars"/>, a null read through this converter gives the type's
        /// default: a built-in converter of a non-nullable value type that does not
        /// handle null itself and is not untyped (<see cref="YamlElement"/>). A converter of the user's keeps receiving
        /// the Null token. Consulted only when the token is null and the option is on.
        /// </summary>
        internal virtual bool ReadsNullAsDefault => false;

        /// <summary>
        /// Indicates that the converter is compatible with <see cref="YamlObjectCreationHandling.Populate"/>.
        /// </summary>
        internal virtual bool CanPopulate => false;

        /// <summary>
        /// Can direct Read or Write methods be called (for performance).
        /// </summary>
        internal bool CanUseDirectReadOrWrite { get; set; }

        /// <summary>
        /// The converter supports writing and reading metadata.
        /// </summary>
        internal virtual bool CanHaveMetadata => false;

        /// <summary>
        /// The converter supports polymorphic writes; only reserved for System.Object types.
        /// </summary>
        internal bool CanBePolymorphic { get; init; }

        /// <summary>
        /// The converter can read/write multiple YAML value shapes (e.g. both strings and objects).
        /// Relaxes debug validation that non-value converters must always read StartObject/StartArray tokens.
        /// </summary>
        internal bool SupportsMultipleTokenTypes { get; init; }

        /// <summary>
        /// The serializer must read ahead all contents of the next YAML value
        /// before calling into the converter for deserialization.
        /// </summary>
        internal bool RequiresReadAhead { get; private protected set; }

        /// <summary>
        /// Whether the converter is a special root-level value streaming converter.
        /// </summary>
        internal bool IsRootLevelMultiContentStreamingConverter { get; init; }

        /// <summary>
        /// Used to support YamlObject as an extension property in a loosely-typed, trimmable manner.
        /// </summary>
        internal virtual void ReadElementAndSetProperty(
            object obj,
            string propertyName,
            ref Utf8YamlReader reader,
            YamlSerializerOptions options,
            scoped ref ReadStack state)
        {
            Debug.Fail("Should not be reachable.");

            throw new InvalidOperationException();
        }

        internal virtual YamlTypeInfo CreateYamlTypeInfo(YamlSerializerOptions options)
        {
            Debug.Fail("Should not be reachable.");

            throw new InvalidOperationException();
        }

        internal YamlConverter<TTarget> CreateCastingConverter<TTarget>()
        {
            Debug.Assert(this is not YamlConverterFactory);

            if (this is YamlConverter<TTarget> conv)
            {
                return conv;
            }
            else
            {
                YamlSerializerOptions.CheckConverterNullabilityIsSameAsPropertyType(this, typeof(TTarget));

                // Avoid layering casting converters by consulting any source converters directly.
                return
                    SourceConverterForCastingConverter?.CreateCastingConverter<TTarget>()
                    ?? new CastingConverter<TTarget>(this);
            }
        }

        /// <summary>
        /// Tracks whether the YamlConverter&lt;T&gt;.HandleNull property has been overridden by a derived converter.
        /// </summary>
        internal bool UsesDefaultHandleNull { get; private protected set; }

        /// <summary>
        /// Does the converter want to be called when reading null tokens.
        /// When YamlConverter&lt;T&gt;.HandleNull isn't overridden this can still be true for non-nullable structs.
        /// </summary>
        internal bool HandleNullOnRead { get; private protected init; }

        /// <summary>
        /// Does the converter want to be called for null values.
        /// Should always match the precise value of the YamlConverter&lt;T&gt;.HandleNull virtual property.
        /// </summary>
        internal bool HandleNullOnWrite { get; private protected init; }

        /// <summary>
        /// Set if this converter is itself a casting converter.
        /// </summary>
        internal virtual YamlConverter? SourceConverterForCastingConverter => null;

        internal virtual Type? ElementType => null;

        internal virtual Type? KeyType => null;

        internal virtual YamlConverter? NullableElementConverter => null;

        /// <summary>
        /// Cached value of TypeToConvert.IsValueType, which is an expensive call.
        /// </summary>
        internal bool IsValueType { get; init; }

        /// <summary>
        /// Indicates whether this converter handles IEEE 754 floating-point types
        /// (double, float, Half) that may emit anyOf schemas with named floating-point
        /// literals under AllowNamedFloatingPointLiterals.
        /// </summary>
        internal virtual bool IsIeeeFloatingPointConverter => false;

        /// <summary>
        /// Whether the converter is built-in.
        /// </summary>
        internal bool IsInternalConverter { get; init; }

        /// <summary>
        /// Whether the converter is built-in and handles a number type.
        /// </summary>
        internal bool IsInternalConverterForNumberType { get; init; }

        /// <summary>
        /// Whether the converter handles collection deserialization by converting from
        /// an intermediate buffer such as immutable collections, arrays or memory types.
        /// Used in conjunction with <see cref="YamlCollectionConverter{TCollection, TElement}.ConvertCollection(ref ReadStack, YamlSerializerOptions)"/>.
        /// </summary>
        internal virtual bool IsConvertibleCollection => false;

        internal static bool ShouldFlush(ref WriteStack state, Utf8YamlWriter writer)
        {
            Debug.Assert(state.FlushThreshold == 0 || (state.PipeWriter is { CanGetUnflushedBytes: true }),
                "ShouldFlush should only be called by resumable serializers, all of which use the PipeWriter abstraction with CanGetUnflushedBytes == true.");
            // If surpassed flush threshold then return true which will flush stream.
            if (state.PipeWriter is { } pipeWriter)
            {
                return state.FlushThreshold > 0 && pipeWriter.UnflushedBytes > state.FlushThreshold - writer.BytesPending;
            }

            return false;
        }

        internal abstract object? ReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options);
        internal abstract bool OnTryReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out object? value);
        internal abstract bool TryReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out object? value);
        internal abstract object? ReadAsPropertyNameAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options);
        internal abstract object? ReadAsPropertyNameCoreAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options);
        internal abstract object? ReadNumberWithCustomHandlingAsObject(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options);

        internal abstract void WriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options);
        internal abstract bool OnTryWriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, ref WriteStack state);
        internal abstract bool TryWriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, ref WriteStack state);
        internal abstract void WriteAsPropertyNameAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options);
        internal abstract void WriteAsPropertyNameCoreAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, bool isWritingExtensionDataProperty);
        internal abstract void WriteNumberWithCustomHandlingAsObject(Utf8YamlWriter writer, object? value, YamlNumberHandling handling);

        /// <summary>
        /// Gets a schema from the type being converted
        /// </summary>
        internal virtual YamlSchema? GetSchema(YamlNumberHandling numberHandling) => null;

        /// <summary>
        /// Gets the set of leading YAML value shapes that this converter can read
        /// for the given <paramref name="numberHandling"/>. The base implementation returns
        /// <see cref="YamlValueType.Any"/>. Used by union-type dispatch to build a
        /// value-shape-to-case map without re-walking type metadata. Mirrors <see cref="GetSchema"/>.
        /// </summary>
        /// <remarks>
        /// When adding or changing an override on a built-in converter (under
        /// <c>Serialization/Converters/Value/</c>), also update
        /// <c>gen/YamlSourceGenerator.Parser.cs::GetSupportedYamlValueTypes</c> so the source
        /// generator's union ambiguity diagnostic (STYML1227) agrees with the runtime
        /// union value-shape map.
        /// </remarks>
        internal virtual YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) => YamlValueType.Any;

        // Whether a type (ConverterStrategy.Object) is deserialized using a parameterized constructor.
        internal virtual bool ConstructorIsParameterized { get; }

        internal ConstructorInfo? ConstructorInfo { get; set; }

        /// <summary>
        /// Used for hooking custom configuration to a newly created associated YamlTypeInfo instance.
        /// </summary>
        internal virtual void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options) { }

        /// <summary>
        /// Additional reflection-specific configuration required by certain collection converters.
        /// </summary>
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal virtual void ConfigureYamlTypeInfoUsingReflection(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options) { }
    }
}
