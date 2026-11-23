// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Provides a mechanism to invoke "fast-path" serialization logic via
    /// <see cref="YamlTypeInfo{T}.SerializeHandler"/>. This type holds an optional
    /// reference to an actual <see cref="YamlConverter{T}"/> for the type
    /// <typeparamref name="T"/>, to provide a fallback when the fast path cannot be used.
    /// </summary>
    /// <typeparam name="T">The type to converter</typeparam>
    internal sealed class YamlMetadataServicesConverter<T> : YamlResumableConverter<T>
    {
        // A backing converter for when fast-path logic cannot be used.
        internal YamlConverter<T> Converter { get; }

        internal override Type? KeyType => Converter.KeyType;
        internal override Type? ElementType => Converter.ElementType;
        internal override YamlConverter? NullableElementConverter => Converter.NullableElementConverter;
        public override bool HandleNull { get; }

        internal override bool ConstructorIsParameterized => Converter.ConstructorIsParameterized;
        internal override bool SupportsCreateObjectDelegate => Converter.SupportsCreateObjectDelegate;
        internal override bool IsUntypedConverter => Converter.IsUntypedConverter;
        internal override bool ReadsNullAsDefault => Converter.ReadsNullAsDefault;
        internal override bool CanHaveMetadata => Converter.CanHaveMetadata;

        internal override bool CanPopulate => Converter.CanPopulate;

        // M5f: a generated collection contract wraps the library's collection converter.
        internal override bool? IsEmptyCollection(object value) => Converter.IsEmptyCollection(value);

        public YamlMetadataServicesConverter(YamlConverter<T> converter)
        {
            Converter = converter;
            ConverterStrategy = converter.ConverterStrategy;
            IsInternalConverter = converter.IsInternalConverter;
            IsInternalConverterForNumberType = converter.IsInternalConverterForNumberType;
            CanBePolymorphic = converter.CanBePolymorphic;

            // Ensure HandleNull values reflect the exact configuration of the source converter
            HandleNullOnRead = converter.HandleNullOnRead;
            HandleNullOnWrite = converter.HandleNullOnWrite;
            HandleNull = converter.HandleNullOnWrite;
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out T? value)
             => Converter.OnTryRead(ref reader, typeToConvert, options, ref state, out value);

        internal override bool OnTryWrite(Utf8YamlWriter writer, T value, YamlSerializerOptions options, ref WriteStack state)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;
            Debug.Assert(yamlTypeInfo is YamlTypeInfo<T> typeInfo && typeInfo.SerializeHandler is not null);

            if (!state.SupportContinuation &&
                yamlTypeInfo.CanUseSerializeHandler &&
                !YamlHelpers.RequiresSpecialNumberHandlingOnWrite(state.Current.NumberHandling) &&
                !state.CurrentContainsMetadata) // Do not use the fast path if state needs to write metadata.
            {
                ((YamlTypeInfo<T>)yamlTypeInfo).SerializeHandler!(writer, value);
                return true;
            }

            return Converter.OnTryWrite(writer, value, options, ref state);
        }

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
            => Converter.ConfigureYamlTypeInfo(yamlTypeInfo, options);

        internal override YamlSchema? GetSchema(YamlNumberHandling numberHandling)
            => Converter.GetSchema(numberHandling);

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling)
            => Converter.GetSupportedYamlValueTypes(numberHandling);
    }
}
