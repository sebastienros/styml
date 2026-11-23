// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization.Converters;

namespace System.Text.Yaml.Serialization.Metadata
{
    public partial class DefaultYamlTypeInfoResolver
    {
        private static Dictionary<Type, YamlConverter>? s_defaultSimpleConverters;
        private static YamlConverterFactory[]? s_defaultFactoryConverters;

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlConverterFactory[] GetDefaultFactoryConverters()
        {
            return
            [
                // Check for disallowed types.
                new UnsupportedTypeConverterFactory(),
                // Nullable converter should always be next since it forwards to any nullable type.
                new NullableConverterFactory(),
                new EnumConverterFactory(),
                new YamlNodeConverterFactory(),
                new FSharpTypeConverterFactory(),
                new MemoryConverterFactory(),
                // IAsyncEnumerable takes precedence over IEnumerable.
                new IAsyncEnumerableConverterFactory(),
                // IEnumerable should always be second to last since they can convert any IEnumerable.
                new IEnumerableConverterFactory(),
                // Union converter should be before Object since it converts [YamlUnion] types.
                new YamlUnionConverterFactory(),
                // Object should always be last since it converts any type.
                new ObjectConverterFactory()
            ];
        }

        private static Dictionary<Type, YamlConverter> GetDefaultSimpleConverters()
        {
            const int NumberOfSimpleConverters = 35;
            var converters = new Dictionary<Type, YamlConverter>(NumberOfSimpleConverters);

            // Use a dictionary for simple converters.
            // When adding to this, update NumberOfSimpleConverters above.
            //
            // When adding/removing a built-in converter here, also update
            // gen/YamlSourceGenerator.Parser.cs::GetSupportedYamlValueTypes so the union
            // ambiguity diagnostic (STYML1227) agrees with YamlTypeInfo.BuildUnionValueTypeMap.
            Add(YamlMetadataServices.BooleanConverter);
            Add(YamlMetadataServices.ByteConverter);
            Add(YamlMetadataServices.ByteArrayConverter);
            Add(YamlMetadataServices.CharConverter);
            Add(YamlMetadataServices.DateTimeConverter);
            Add(YamlMetadataServices.DateTimeOffsetConverter);
#if NET
            Add(YamlMetadataServices.DateOnlyConverter);
            Add(YamlMetadataServices.TimeOnlyConverter);
            Add(YamlMetadataServices.HalfConverter);
#endif
            Add(YamlMetadataServices.DoubleConverter);
            Add(YamlMetadataServices.DecimalConverter);
            Add(YamlMetadataServices.GuidConverter);
            Add(YamlMetadataServices.Int16Converter);
            Add(YamlMetadataServices.Int32Converter);
            Add(YamlMetadataServices.Int64Converter);
            Add(YamlMetadataServices.YamlElementConverter);
            Add(YamlMetadataServices.YamlDocumentConverter);
            Add(YamlMetadataServices.MemoryByteConverter);
            Add(YamlMetadataServices.ReadOnlyMemoryByteConverter);
            Add(YamlMetadataServices.ObjectConverter);
            Add(YamlMetadataServices.SByteConverter);
            Add(YamlMetadataServices.SingleConverter);
            Add(YamlMetadataServices.StringConverter);
            Add(YamlMetadataServices.TimeSpanConverter);
            Add(YamlMetadataServices.UInt16Converter);
            Add(YamlMetadataServices.UInt32Converter);
            Add(YamlMetadataServices.UInt64Converter);
#if NET
            Add(YamlMetadataServices.Int128Converter);
            Add(YamlMetadataServices.UInt128Converter);
#endif
#if NET11_0_OR_GREATER
            Add(YamlMetadataServices.BFloat16Converter);
            Add(YamlMetadataServices.Decimal32Converter);
            Add(YamlMetadataServices.Decimal64Converter);
            Add(YamlMetadataServices.Decimal128Converter);
#endif
            Add(YamlMetadataServices.UriConverter);
            Add(YamlMetadataServices.VersionConverter);

            Debug.Assert(converters.Count <= NumberOfSimpleConverters);

            return converters;

            void Add(YamlConverter converter) =>
                converters.Add(converter.Type!, converter);
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlConverter GetBuiltInConverter(Type typeToConvert)
        {
            s_defaultSimpleConverters ??= GetDefaultSimpleConverters();
            s_defaultFactoryConverters ??= GetDefaultFactoryConverters();

            if (s_defaultSimpleConverters.TryGetValue(typeToConvert, out YamlConverter? converter))
            {
                return converter;
            }
            else
            {
                foreach (YamlConverterFactory factory in s_defaultFactoryConverters)
                {
                    if (factory.CanConvert(typeToConvert))
                    {
                        converter = factory;
                        break;
                    }
                }

                // Since the object and IEnumerable converters cover all types, we should have a converter.
                Debug.Assert(converter is not null);
                return converter;
            }
        }

        internal static bool TryGetDefaultSimpleConverter(Type typeToConvert, [NotNullWhen(true)] out YamlConverter? converter)
        {
            if (s_defaultSimpleConverters is null)
            {
                converter = null;
                return false;
            }

            return s_defaultSimpleConverters.TryGetValue(typeToConvert, out converter);
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlConverter? GetCustomConverterForMember(Type typeToConvert, MemberInfo memberInfo, YamlSerializerOptions options)
        {
            Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
            Debug.Assert(typeToConvert != null);

            YamlConverterAttribute? converterAttribute = memberInfo.GetUniqueCustomAttribute<YamlConverterAttribute>(inherit: false);
            return converterAttribute is null ? null : GetConverterFromAttribute(converterAttribute, typeToConvert, memberInfo, options);
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal static YamlConverter GetConverterForType(Type typeToConvert, YamlSerializerOptions options, bool resolveYamlConverterAttribute = true)
        {
            // Priority 1: Attempt to get custom converter from the Converters list.
            YamlConverter? converter = options.GetConverterFromList(typeToConvert);

            // Priority 2: Attempt to get converter from [YamlConverter] on the type being converted.
            if (resolveYamlConverterAttribute && converter is null)
            {
                YamlConverterAttribute? converterAttribute = typeToConvert.GetUniqueCustomAttribute<YamlConverterAttribute>(inherit: false);
                if (converterAttribute is not null)
                {
                    converter = GetConverterFromAttribute(converterAttribute, typeToConvert: typeToConvert, memberInfo: null, options);
                }
            }

            // Priority 3: Query the built-in converters.
            converter ??= GetBuiltInConverter(typeToConvert);

            // Expand if factory converter & validate.
            converter = options.ExpandConverterFactory(converter, typeToConvert);
            if (!converter.Type!.IsInSubtypeRelationshipWith(typeToConvert))
            {
                ThrowHelper.ThrowInvalidOperationException_SerializationConverterNotCompatible(converter.GetType(), typeToConvert);
            }

            YamlSerializerOptions.CheckConverterNullabilityIsSameAsPropertyType(converter, typeToConvert);
            return converter;
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlConverter GetConverterFromAttribute(YamlConverterAttribute converterAttribute, Type typeToConvert, MemberInfo? memberInfo, YamlSerializerOptions options)
        {
            YamlConverter? converter;

            Type declaringType = memberInfo?.DeclaringType ?? typeToConvert;
            Type? converterType = converterAttribute.ConverterType;
            if (converterType == null)
            {
                // Allow the attribute to create the converter.
                converter = converterAttribute.CreateConverter(typeToConvert);
                if (converter is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(declaringType, memberInfo, typeToConvert);
                }
            }
            else
            {
                // Handle open generic converter types (e.g., OptionConverter<> on Option<T>).
                // If the converter type is an open generic and the type to convert is a closed generic
                // with matching type arity, construct the closed converter type.
                if (converterType.IsGenericTypeDefinition)
                {
                    if (typeToConvert.IsGenericType &&
                        converterType.GetGenericArguments().Length == typeToConvert.GetGenericArguments().Length)
                    {
                        converterType = converterType.MakeGenericType(typeToConvert.GetGenericArguments());
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeOpenGenericNotCompatible(declaringType, memberInfo, converterType);
                    }
                }

                ConstructorInfo? ctor = converterType.GetConstructor(Type.EmptyTypes);
                if (!typeof(YamlConverter).IsAssignableFrom(converterType) || ctor == null || !ctor.IsPublic)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(declaringType, memberInfo);
                }

                converter = (YamlConverter)Activator.CreateInstance(converterType)!;
            }

            Debug.Assert(converter is not null);
            if (!converter.CanConvert(typeToConvert))
            {
                Type? underlyingType = Nullable.GetUnderlyingType(typeToConvert);
                if (underlyingType != null && converter.CanConvert(underlyingType))
                {
                    if (converter is YamlConverterFactory converterFactory)
                    {
                        converter = converterFactory.GetConverterInternal(underlyingType, options);
                    }

                    // Allow nullable handling to forward to the underlying type's converter.
                    return NullableConverterFactory.CreateValueConverter(underlyingType, converter);
                }

                ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(declaringType, memberInfo, typeToConvert);
            }

            return converter;
        }
    }
}
