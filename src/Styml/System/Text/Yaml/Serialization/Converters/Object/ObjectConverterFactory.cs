// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Converter factory for all object-based types (non-enumerable and non-primitive).
    /// </summary>
    [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class ObjectConverterFactory : YamlConverterFactory
    {
        // Need to toggle this behavior when generating converters for F# struct records.
        private readonly bool _useDefaultConstructorInUnannotatedStructs;

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        public ObjectConverterFactory(bool useDefaultConstructorInUnannotatedStructs = true)
        {
            _useDefaultConstructorInUnannotatedStructs = useDefaultConstructorInUnannotatedStructs;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            // This is the last built-in factory converter, so if the IEnumerableConverterFactory doesn't
            // support it, then it is not IEnumerable.
            Debug.Assert(!typeof(IEnumerable).IsAssignableFrom(typeToConvert));
            return true;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067:UnrecognizedReflectionPattern",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            YamlConverter converter;
            Type converterType;

            bool useDefaultConstructorInUnannotatedStructs = _useDefaultConstructorInUnannotatedStructs && !typeToConvert.IsKeyValuePair();
            if (!typeToConvert.TryGetDeserializationConstructor(useDefaultConstructorInUnannotatedStructs, out ConstructorInfo? constructor))
            {
                ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateTypeAttribute<YamlConstructorAttribute>(typeToConvert);
            }

            ParameterInfo[]? parameters = constructor?.GetParameters();

            if (constructor == null || typeToConvert.IsAbstract || parameters!.Length == 0)
            {
                converterType = typeof(ObjectDefaultConverter<>).MakeGenericType(typeToConvert);
            }
            else
            {
                int parameterCount = parameters.Length;

                foreach (ParameterInfo parameter in parameters)
                {
                    // Skip out parameters — they don't receive values from JSON
                    // and may reference unsupported types (e.g. Task).
                    if (parameter.IsOut)
                    {
                        continue;
                    }

                    // Every argument must be of supported type.
                    // For byref parameters (in/ref), validate the underlying element type.
                    Type parameterType = parameter.ParameterType;
                    if (parameterType.IsByRef)
                    {
                        parameterType = parameterType.GetElementType()!;
                    }

                    YamlTypeInfo.ValidateType(parameterType);
                }

                if (parameterCount <= YamlConstants.UnboxedParameterCountThreshold)
                {
                    Type placeHolderType = YamlTypeInfo.ObjectType;
                    Type[] typeArguments = new Type[YamlConstants.UnboxedParameterCountThreshold + 1];

                    typeArguments[0] = typeToConvert;
                    for (int i = 0; i < YamlConstants.UnboxedParameterCountThreshold; i++)
                    {
                        if (i < parameterCount)
                        {
                            // out parameters use placeholder type — they aren't deserialized
                            // and may reference types that can't be used as generic arguments.
                            if (parameters[i].IsOut)
                            {
                                typeArguments[i + 1] = placeHolderType;
                            }
                            else
                            {
                                // For byref parameters (in/ref), use the underlying element type
                                // since byref types cannot be used as generic type arguments.
                                Type parameterType = parameters[i].ParameterType;
                                if (parameterType.IsByRef)
                                {
                                    parameterType = parameterType.GetElementType()!;
                                }

                                typeArguments[i + 1] = parameterType;
                            }
                        }
                        else
                        {
                            // Use placeholder arguments if there are less args than the threshold.
                            typeArguments[i + 1] = placeHolderType;
                        }
                    }

                    converterType = typeof(SmallObjectWithParameterizedConstructorConverter<,,,,>).MakeGenericType(typeArguments);
                }
                else
                {
                    converterType = typeof(LargeObjectWithParameterizedConstructorConverterWithReflection<>).MakeGenericType(typeToConvert);
                }
            }

            converter = (YamlConverter)Activator.CreateInstance(
                    converterType,
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: null,
                    culture: null)!;

            converter.ConstructorInfo = constructor!;
            return converter;
        }
    }
}
