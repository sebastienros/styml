// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Inherited by built-in converters serializing types as YAML primitives that support property name serialization.
    /// </summary>
    internal abstract class YamlPrimitiveConverter<T> : YamlConverter<T>
    {
        public sealed override void WriteAsPropertyName(Utf8YamlWriter writer, [DisallowNull] T value, YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(value);

            WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        public sealed override T ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (reader.TokenType != YamlTokenType.PropertyName)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedPropertyName(reader.TokenType);
            }

            return ReadAsPropertyNameCore(ref reader, typeToConvert, options);
        }

        private protected static YamlSchema GetSchemaForNumericType(YamlSchemaType schemaType, YamlNumberHandling numberHandling, bool isIeeeFloatingPoint = false)
        {
            Debug.Assert(schemaType is YamlSchemaType.Integer or YamlSchemaType.Number);
            Debug.Assert(!isIeeeFloatingPoint || schemaType is YamlSchemaType.Number);
#if NET11_0_OR_GREATER
            Debug.Assert(isIeeeFloatingPoint == (typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(Half) ||
                typeof(T) == typeof(System.Numerics.BFloat16) || typeof(T) == typeof(System.Numerics.Decimal32) ||
                typeof(T) == typeof(System.Numerics.Decimal64) || typeof(T) == typeof(System.Numerics.Decimal128)));
#elif NET
            Debug.Assert(isIeeeFloatingPoint == (typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(Half)));
#endif
            string? pattern = null;

            if ((numberHandling & (YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)) != 0)
            {
                pattern = schemaType is YamlSchemaType.Integer
                    ? @"^-?(?:0|[1-9]\d*)$"
                    : isIeeeFloatingPoint
                        ? @"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$"
                        : @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$";

                schemaType |= YamlSchemaType.String;
            }

            if (isIeeeFloatingPoint && (numberHandling & YamlNumberHandling.AllowNamedFloatingPointLiterals) != 0)
            {
                return new YamlSchema
                {
                    AnyOf =
                    [
                        new YamlSchema { Type = schemaType, Pattern = pattern },
                        new YamlSchema { Enum = [(YamlNode)"NaN", (YamlNode)"Infinity", (YamlNode)"-Infinity"] },
                    ]
                };
            }

            return new YamlSchema { Type = schemaType, Pattern = pattern };
        }

        private protected static YamlValueType GetSupportedYamlValueTypesForNumericType(YamlNumberHandling numberHandling) =>
            (numberHandling & YamlNumberHandling.AllowReadingFromString) != 0
                ? YamlValueType.Number | YamlValueType.String
                : YamlValueType.Number;
    }
}
