// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class NullableConverter<T> : YamlConverter<T?> where T : struct // Do not rename FQN (legacy schema generation)
    {
        internal override Type? ElementType => typeof(T);
        internal override YamlConverter? NullableElementConverter => _elementConverter;
        public override bool HandleNull => true;
        internal override bool CanPopulate => _elementConverter.CanPopulate;
        internal override bool ConstructorIsParameterized => _elementConverter.ConstructorIsParameterized;

        // It is possible to cache the underlying converter since this is an internal converter and
        // an instance is created only once for each YamlSerializerOptions instance.
        private readonly YamlConverter<T> _elementConverter; // Do not rename (legacy schema generation)

        public NullableConverter(YamlConverter<T> elementConverter)
        {
            _elementConverter = elementConverter;
            IsInternalConverter = elementConverter.IsInternalConverter;
            IsInternalConverterForNumberType = elementConverter.IsInternalConverterForNumberType;
            ConverterStrategy = elementConverter.ConverterStrategy;
            SupportsMultipleTokenTypes = elementConverter.SupportsMultipleTokenTypes;
            RequiresReadAhead = elementConverter.RequiresReadAhead;
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out T? value)
        {
            if (!state.IsContinuation && (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter)))
            {
                value = null;
                return true;
            }

            YamlTypeInfo previousTypeInfo = state.Current.YamlTypeInfo;
            state.Current.YamlTypeInfo = state.Current.YamlTypeInfo.ElementTypeInfo!;
            if (_elementConverter.OnTryRead(ref reader, typeof(T), options, ref state, out T element))
            {
                value = element;
                state.Current.YamlTypeInfo = previousTypeInfo;
                return true;
            }

            state.Current.YamlTypeInfo = previousTypeInfo;
            value = null;
            return false;
        }

        internal override bool OnTryWrite(Utf8YamlWriter writer, T? value, YamlSerializerOptions options, ref WriteStack state)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return true;
            }

            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, value.Value, options, ref state);
        }

        public override T? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter))
            {
                return null;
            }

            T value = _elementConverter.Read(ref reader, typeof(T), options);
            return value;
        }

        public override void Write(Utf8YamlWriter writer, T? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                _elementConverter.Write(writer, value.Value, options);
            }
        }

        internal override T? ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling numberHandling, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter))
            {
                return null;
            }

            T value = _elementConverter.ReadNumberWithCustomHandling(ref reader, numberHandling, options);
            return value;
        }

        internal override void WriteNumberWithCustomHandling(Utf8YamlWriter writer, T? value, YamlNumberHandling handling)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                _elementConverter.WriteNumberWithCustomHandling(writer, value.Value, handling);
            }
        }

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling numberHandling) =>
            _elementConverter.GetSupportedYamlValueTypes(numberHandling);
    }
}
