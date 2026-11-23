// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    // Converter for F# optional values: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-option-1.html
    // Serializes `Some(value)` using the format of `value` and `None` values as `null`.
    internal sealed class FSharpOptionConverter<TOption, TElement> : YamlConverter<TOption>
        where TOption : class
    {
        internal override Type? ElementType => typeof(TElement);
        internal override YamlConverter? NullableElementConverter => _elementConverter;
        // 'None' is encoded using 'null' at runtime and serialized as 'null' in JSON.
        public override bool HandleNull => true;

        private readonly YamlConverter<TElement> _elementConverter;
        private readonly Func<TOption, TElement> _optionValueGetter;
        private readonly Func<TElement?, TOption> _optionConstructor;

        [RequiresUnreferencedCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        [RequiresDynamicCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        public FSharpOptionConverter(YamlConverter<TElement> elementConverter)
        {
            _elementConverter = elementConverter;
            _optionValueGetter = FSharpCoreReflectionProxy.Instance.CreateFSharpOptionValueGetter<TOption, TElement>();
            _optionConstructor = FSharpCoreReflectionProxy.Instance.CreateFSharpOptionSomeConstructor<TOption, TElement>();
            ConverterStrategy = elementConverter.ConverterStrategy;
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out TOption? value)
        {
            // `null` values deserialize as `None`
            if (!state.IsContinuation && (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter)))
            {
                value = null;
                return true;
            }

            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            if (_elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
            {
                value = _optionConstructor(element);
                return true;
            }

            value = null;
            return false;
        }

        internal override bool OnTryWrite(Utf8YamlWriter writer, TOption value, YamlSerializerOptions options, ref WriteStack state)
        {
            if (value is null)
            {
                // Write `None` values as null
                writer.WriteNullValue();
                return true;
            }

            TElement element = _optionValueGetter(value);
            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, element, options, ref state);
        }

        // Since this is a hybrid converter (ConverterStrategy depends on the element converter),
        // we need to override the value converter Write and Read methods too.

        public override void Write(Utf8YamlWriter writer, TOption value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                TElement element = _optionValueGetter(value);
                _elementConverter.Write(writer, element, options);
            }
        }

        public override TOption? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter))
            {
                return null;
            }

            TElement? element = _elementConverter.Read(ref reader, typeToConvert, options);
            return _optionConstructor(element);
        }
    }
}
