// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    // Converter for F# struct optional values: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpvalueoption-1.html
    // Serializes `ValueSome(value)` using the format of `value` and `ValueNone` values as `null`.
    internal sealed class FSharpValueOptionConverter<TValueOption, TElement> : YamlConverter<TValueOption>
        where TValueOption : struct, IEquatable<TValueOption>
    {
        internal override Type? ElementType => typeof(TElement);
        internal override YamlConverter? NullableElementConverter => _elementConverter;
        // 'ValueNone' is encoded using 'default' at runtime and serialized as 'null' in JSON.
        public override bool HandleNull => true;

        private readonly YamlConverter<TElement> _elementConverter;
        private readonly FSharpCoreReflectionProxy.StructGetter<TValueOption, TElement> _optionValueGetter;
        private readonly Func<TElement?, TValueOption> _optionConstructor;

        [RequiresUnreferencedCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        [RequiresDynamicCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        public FSharpValueOptionConverter(YamlConverter<TElement> elementConverter)
        {
            _elementConverter = elementConverter;
            _optionValueGetter = FSharpCoreReflectionProxy.Instance.CreateFSharpValueOptionValueGetter<TValueOption, TElement>();
            _optionConstructor = FSharpCoreReflectionProxy.Instance.CreateFSharpValueOptionSomeConstructor<TValueOption, TElement>();
            ConverterStrategy = elementConverter.ConverterStrategy;
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out TValueOption value)
        {
            // `null` values deserialize as `ValueNone`
            if (!state.IsContinuation && (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter)))
            {
                value = default;
                return true;
            }

            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            if (_elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
            {
                value = _optionConstructor(element);
                return true;
            }

            value = default;
            return false;
        }

        internal override bool OnTryWrite(Utf8YamlWriter writer, TValueOption value, YamlSerializerOptions options, ref WriteStack state)
        {
            if (value.Equals(default))
            {
                // Write `ValueNone` values as null
                writer.WriteNullValue();
                return true;
            }

            TElement element = _optionValueGetter(ref value);

            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, element, options, ref state);
        }

        // Since this is a hybrid converter (ConverterStrategy depends on the element converter),
        // we need to override the value converter Write and Read methods too.

        public override void Write(Utf8YamlWriter writer, TValueOption value, YamlSerializerOptions options)
        {
            if (value.Equals(default))
            {
                // Write `ValueNone` values as null
                writer.WriteNullValue();
            }
            else
            {
                TElement element = _optionValueGetter(ref value);
                _elementConverter.Write(writer, element, options);
            }
        }

        public override TValueOption Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !_elementConverter.IsUntypedConverter))
            {
                return default;
            }

            TElement? element = _elementConverter.Read(ref reader, typeToConvert, options);
            return _optionConstructor(element);
        }
    }
}
