// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Converters;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        internal const string SerializationUnreferencedCodeMessage = "YAML serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a YamlTypeInfo or YamlSerializerContext, or make sure all of the required types are preserved.";
        internal const string SerializationRequiresDynamicCodeMessage = "YAML serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Yaml source generation for native AOT applications.";

        /// <summary>
        /// Indicates whether unconfigured <see cref="YamlSerializerOptions"/> instances
        /// should be set to use the reflection-based <see cref="DefaultYamlTypeInfoResolver"/>.
        /// </summary>
        /// <remarks>
        /// The value of the property is backed by the "System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault"
        /// <see cref="AppContext"/> setting and defaults to <see langword="true"/> if unset.
        /// </remarks>
        [FeatureSwitchDefinition("System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault")]
        public static bool IsReflectionEnabledByDefault { get; } =
            AppContext.TryGetSwitch(
                switchName: "System.Text.Yaml.YamlSerializer.IsReflectionEnabledByDefault",
                isEnabled: out bool value)
            ? value : true;

        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        private static YamlTypeInfo GetTypeInfo(YamlSerializerOptions? options, Type inputType)
        {
            Debug.Assert(inputType != null);

            options ??= YamlSerializerOptions.Default;
            options.MakeReadOnly(populateMissingResolver: true);

            // In order to improve performance of polymorphic root-level object serialization,
            // we bypass GetTypeInfoForRootType and cache YamlTypeInfo<object> in a dedicated property.
            // This lets any derived types take advantage of the cache in GetTypeInfoForRootType themselves.
            return inputType == YamlTypeInfo.ObjectType
                ? options.ObjectTypeInfo
                : options.GetTypeInfoForRootType(inputType);
        }

        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        private static YamlTypeInfo<T> GetTypeInfo<T>(YamlSerializerOptions? options)
            => (YamlTypeInfo<T>)GetTypeInfo(options, typeof(T));

        private static YamlTypeInfo GetTypeInfo(YamlSerializerContext context, Type inputType)
        {
            Debug.Assert(context is not null);
            Debug.Assert(inputType != null);

            YamlTypeInfo? info = context.GetTypeInfo(inputType);
            if (info is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NoMetadataForType(inputType, context);
            }

            info.EnsureConfigured();
            return info;
        }

        private static void ValidateInputType(object? value, Type inputType)
        {
            ArgumentNullException.ThrowIfNull(inputType);

            if (value is not null)
            {
                Type runtimeType = value.GetType();
                if (!inputType.IsAssignableFrom(runtimeType))
                {
                    ThrowHelper.ThrowArgumentException_DeserializeWrongType(inputType, value);
                }
            }
        }

        internal static bool IsValidNumberHandlingValue(YamlNumberHandling handling) =>
            YamlHelpers.IsInRangeInclusive((int)handling, 0,
                (int)(
                YamlNumberHandling.Strict |
                YamlNumberHandling.AllowReadingFromString |
                YamlNumberHandling.WriteAsString |
                YamlNumberHandling.AllowNamedFloatingPointLiterals));

        internal static bool IsValidCreationHandlingValue(YamlObjectCreationHandling handling) =>
            handling is YamlObjectCreationHandling.Replace or YamlObjectCreationHandling.Populate;

        internal static bool IsValidUnmappedMemberHandlingValue(YamlUnmappedMemberHandling handling) =>
            handling is YamlUnmappedMemberHandling.Skip or YamlUnmappedMemberHandling.Disallow;

        [return: NotNullIfNotNull(nameof(value))]
        internal static T? UnboxOnRead<T>(object? value)
        {
            if (value is null)
            {
                if (default(T) is not null)
                {
                    // Casting null values to a non-nullable struct throws NullReferenceException.
                    ThrowUnableToCastValue(value);
                }

                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            ThrowUnableToCastValue(value);
            return default!;

            static void ThrowUnableToCastValue(object? value)
            {
                if (value is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_DeserializeUnableToAssignNull(declaredType: typeof(T));
                }
                else
                {
                    ThrowHelper.ThrowInvalidCastException_DeserializeUnableToAssignValue(typeOfValue: value.GetType(), declaredType: typeof(T));
                }
            }
        }

        [return: NotNullIfNotNull(nameof(value))]
        internal static T? UnboxOnWrite<T>(object? value)
        {
            if (default(T) is not null && value is null)
            {
                // Casting null values to a non-nullable struct throws NullReferenceException.
                ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(typeof(T));
            }

            return (T?)value;
        }

        private static YamlTypeInfo<List<T?>> GetOrAddListTypeInfoForRootLevelValueMode<T>(YamlTypeInfo<T> elementTypeInfo)
        {
            if (elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo is not null)
            {
                return (YamlTypeInfo<List<T?>>)elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo;
            }

            var converter = new RootLevelListConverter<T>(elementTypeInfo);
            var listTypeInfo = new YamlTypeInfo<List<T?>>(converter, elementTypeInfo.Options)
            {
                ElementTypeInfo = elementTypeInfo,
            };

            listTypeInfo.EnsureConfigured();
            elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo = listTypeInfo;
            return listTypeInfo;
        }

        private static YamlTypeInfo<List<T?>> GetOrAddListTypeInfoForArrayMode<T>(YamlTypeInfo<T> elementTypeInfo)
        {
            if (elementTypeInfo._asyncEnumerableArrayTypeInfo is not null)
            {
                return (YamlTypeInfo<List<T?>>)elementTypeInfo._asyncEnumerableArrayTypeInfo;
            }

            var converter = new ListOfTConverter<List<T>, T>();
            var listTypeInfo = new YamlTypeInfo<List<T?>>(converter, elementTypeInfo.Options)
            {
                CreateObject = static () => new List<T?>(),
                ElementTypeInfo = elementTypeInfo,
            };

            listTypeInfo.EnsureConfigured();
            elementTypeInfo._asyncEnumerableArrayTypeInfo = listTypeInfo;
            return listTypeInfo;
        }

        private static YamlTypeInfo<IAsyncEnumerable<T>> GetOrAddIAsyncEnumerableTypeInfoForSerialize<T>(YamlTypeInfo<T> elementTypeInfo)
        {
            if (elementTypeInfo._asyncEnumerableRootLevelSerializer is not null)
            {
                return (YamlTypeInfo<IAsyncEnumerable<T>>)elementTypeInfo._asyncEnumerableRootLevelSerializer;
            }

            // Synthesize the IAsyncEnumerable<T> type info from the element type info so we don't depend on the
            // resolver (e.g. a source-generated context) having registered IAsyncEnumerable<T> explicitly.
            var converter = new IAsyncEnumerableOfTConverter<IAsyncEnumerable<T>, T>();
            var asyncEnumerableTypeInfo = new YamlTypeInfo<IAsyncEnumerable<T>>(converter, elementTypeInfo.Options)
            {
                ElementTypeInfo = elementTypeInfo,
            };

            asyncEnumerableTypeInfo.EnsureConfigured();
            elementTypeInfo._asyncEnumerableRootLevelSerializer = asyncEnumerableTypeInfo;
            return asyncEnumerableTypeInfo;
        }
    }
}
