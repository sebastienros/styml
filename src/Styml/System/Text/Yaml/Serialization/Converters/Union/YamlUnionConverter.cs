// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter for union types. Fully stateless — reads/writes using the classifier,
    /// deconstructor, and constructor delegates configured on <see cref="YamlTypeInfo{T}"/>.
    /// All configuration is performed by the resolver, not the converter.
    /// </summary>
    internal sealed class YamlUnionConverter<TUnion> : YamlResumableConverter<TUnion>
    {
        public YamlUnionConverter()
        {
            // The union converter dispatches across all JSON value shapes (the actual case
            // type determines which shape is valid). The non-Value converter machinery
            // verifies post-Read positioning per token kind; flagging SupportsMultipleTokenTypes
            // lets value-token cases (string/number/bool) satisfy the verification.
            SupportsMultipleTokenTypes = true;
        }

        // Override the framework's default null short-circuit so that JSON null
        // is delivered to Read where it is dispatched to the case constructor
        // accepting null (or rejected when no case opts in).
        public override bool HandleNull => true;

        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Union;

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out TUnion? value)
        {
            YamlTypeInfo<TUnion> typeInfo = (YamlTypeInfo<TUnion>)state.Current.YamlTypeInfo;

            Func<Type, object?, TUnion>? constructor = typeInfo.UnionConstructor;
            if (constructor is null)
            {
                ThrowHelper.ThrowYamlException_UnionCannotCreateValue(typeToConvert);
            }

            if (reader.TokenType is YamlTokenType.Null || reader.IsLenientNull)
            {
                Type? nullableCaseType = typeInfo.UnionNullableCaseType;
                if (nullableCaseType is null)
                {
                    // Round-trip symmetry with the serializer: default(union) writes
                    // JSON null (deconstructor returns (null, null)), so JSON null must
                    // read back as default(union). For value-type unions this is the
                    // zero-init struct; for reference-type unions this is null.
                    // See https://github.com/dotnet/runtime/issues/128834.
                    value = default;
                    return true;
                }

                value = constructor(nullableCaseType, null);
                return true;
            }

            YamlTypeInfo caseTypeInfo;
            Type caseType;

            if (state.IsContinuation)
            {
                caseTypeInfo = state.Current.YamlPropertyInfo!.YamlTypeInfo;
                caseType = caseTypeInfo.Type;
            }
            else
            {
                if (!TryResolveCaseType(ref reader, typeToConvert, typeInfo, out caseType))
                {
                    value = default;
                    return false;
                }

                caseTypeInfo = options.GetTypeInfoInternal(caseType);
                state.Current.YamlPropertyInfo = caseTypeInfo.PropertyInfoForTypeInfo;
                state.Current.NumberHandling = typeInfo.NumberHandling ?? caseTypeInfo.PropertyInfoForTypeInfo.EffectiveNumberHandling;
            }

            YamlConverter caseConverter = caseTypeInfo.Converter;
            if (!caseConverter.TryReadAsObject(ref reader, caseType, options, ref state, out object? caseValue))
            {
                value = default;
                return false;
            }

            value = constructor(caseType, caseValue);
            return true;
        }

        private static bool TryResolveCaseType(ref Utf8YamlReader reader, Type typeToConvert, YamlTypeInfo<TUnion> typeInfo, out Type caseType)
        {
            caseType = null!;
            YamlTypeClassifier? classifier = typeInfo.TypeClassifier;
            if (classifier is not null)
            {
                // Ensure the entire value has been pre-buffered.
                Utf8YamlReader readerCopy = reader;
                if (!readerCopy.TrySkipPartial())
                {
                    return false;
                }

                // Pass a copy of the reader to the classifier.
                readerCopy = reader;
                caseType = classifier(ref readerCopy)!;

                if (caseType is null)
                {
                    ThrowHelper.ThrowYamlException_UnionTypeClassifierReturnedNull(typeToConvert, reader.TokenType);
                }
            }
            else
            {
                // Default path: JSON value shape matching. Diagnostics recorded at configure time
                // surface here so that ambiguous / custom-converter cases fail with a precise
                // message ONLY when a deserialization is actually attempted (config never throws).
                YamlTokenType tokenType = reader.TokenType;
                YamlValueType valueType = GetYamlValueType(tokenType);

                if ((typeInfo.UnionAmbiguousValueTypes & valueType) != 0)
                {
                    ThrowHelper.ThrowYamlException_UnionAmbiguousYamlValueType(typeToConvert, valueType);
                }

                Type? resolvedCaseType = null;
                typeInfo.UnionValueTypeMap?.TryGetValue(valueType, out resolvedCaseType);
                caseType = resolvedCaseType!;
            }

            if (caseType is null)
            {
                ThrowHelper.ThrowYamlException_UnionYamlTokenTypeNotSupported(typeToConvert, reader.TokenType);
            }

            return true;
        }

        private static YamlValueType GetYamlValueType(YamlTokenType tokenType) =>
            tokenType switch
            {
                YamlTokenType.StartObject => YamlValueType.Object,
                YamlTokenType.StartArray => YamlValueType.Array,
                YamlTokenType.String => YamlValueType.String,
                YamlTokenType.Number => YamlValueType.Number,
                YamlTokenType.True or YamlTokenType.False => YamlValueType.Boolean,
                YamlTokenType.Null => YamlValueType.Null,
                _ => YamlValueType.None,
            };

        internal override bool OnTryWrite(Utf8YamlWriter writer, TUnion value, YamlSerializerOptions options, ref WriteStack state)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return true;
            }

            // Union converters delegate inline to the case converter without emitting
            // structural JSON tokens (StartObject/StartArray), so the writer's depth
            // does not increase across union-to-union recursion. Guard against cyclic
            // union references using the WriteStack frame depth instead.
            if (state.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowYamlException_SerializerCycleDetected(options.EffectiveMaxDepth);
            }

            YamlTypeInfo<TUnion> typeInfo = (YamlTypeInfo<TUnion>)state.Current.YamlTypeInfo;

            Func<TUnion, (Type?, object?)>? deconstructor = typeInfo.UnionDeconstructor;
            if (deconstructor is null)
            {
                ThrowHelper.ThrowYamlException_UnionCannotReadValue(typeof(TUnion));
            }

            (Type? caseType, object? caseValue) = deconstructor(value);

            if (caseType is null)
            {
                // A null case type signals the canonical "null union" state. Both members
                // of the deconstructor tuple convey that signal: caseType doubles as the
                // discriminator, and a non-null caseValue here is ignored.
                writer.WriteNullValue();
                return true;
            }

            YamlTypeInfo caseTypeInfo = options.GetTypeInfoInternal(caseType);
            state.Current.YamlPropertyInfo = caseTypeInfo.PropertyInfoForTypeInfo;
            state.Current.NumberHandling = typeInfo.NumberHandling ?? caseTypeInfo.PropertyInfoForTypeInfo.EffectiveNumberHandling;
            return caseTypeInfo.Converter.TryWriteAsObject(writer, caseValue, options, ref state);
        }
    }
}
