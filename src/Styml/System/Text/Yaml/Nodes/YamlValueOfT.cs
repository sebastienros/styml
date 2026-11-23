// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml.Nodes
{
    [DebuggerDisplay("{ToYamlString(),nq}")]
    [DebuggerTypeProxy(typeof(YamlValue<>.DebugView))]
    internal abstract class YamlValue<TValue> : YamlValue
    {
        internal readonly TValue Value; // keep as a field for direct access to avoid copies

        protected YamlValue(TValue value, YamlNodeOptions? options) : base(options)
        {
            Debug.Assert(value is not null);
            Debug.Assert(value is not YamlElement or YamlElement { ValueKind: not YamlValueKind.Null });
            Debug.Assert(value is not YamlNode);
            Value = value;
        }

        public override T GetValue<T>()
        {
            // If no conversion is needed, just return the raw value.
            if (Value is T returnValue)
            {
                return returnValue;
            }

            // Currently we do not support other conversions.
            // Generics (and also boxing) do not support standard cast operators say from 'long' to 'int',
            //  so attempting to cast here would throw InvalidCastException.
            ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvert(typeof(TValue), typeof(T));
            return default!;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T value)
        {
            // If no conversion is needed, just return the raw value.
            if (Value is T returnValue)
            {
                value = returnValue;
                return true;
            }

            // Currently we do not support other conversions.
            // Generics (and also boxing) do not support standard cast operators say from 'long' to 'int',
            //  so attempting to cast here would throw InvalidCastException.
            value = default!;
            return false;
        }

        /// <summary>
        /// Whether <typeparamref name="TValue"/> is a built-in type that admits primitive YamlValue representation.
        /// </summary>
        internal static bool TypeIsSupportedPrimitive => s_valueKind.HasValue;
        private static readonly YamlValueKind? s_valueKind = DetermineValueKindForType(typeof(TValue));

        /// <summary>
        /// Determines the YamlValueKind for the value of a built-in type.
        /// </summary>
        private protected static YamlValueKind DetermineValueKind(TValue value)
        {
            Debug.Assert(s_valueKind is not null, "Should only be invoked for types that are supported primitives.");

            if (value is bool boolean)
            {
                // Boolean requires special handling since kind varies by value.
                return boolean ? YamlValueKind.True : YamlValueKind.False;
            }

            return s_valueKind.Value;
        }

        /// <summary>
        /// Precomputes the YamlValueKind for a given built-in type where possible.
        /// </summary>
        private static YamlValueKind? DetermineValueKindForType(Type type)
        {
            if (type.IsEnum)
            {
                return null; // Can vary depending on converter configuration and value.
            }

            if (Nullable.GetUnderlyingType(type) is Type underlyingType)
            {
                // Because YamlNode excludes null values, we can identify with the value kind of the underlying type.
                return DetermineValueKindForType(underlyingType);
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
#if NET
                type == typeof(DateOnly) || type == typeof(TimeOnly) ||
#endif
                type == typeof(Guid) || type == typeof(Uri) || type == typeof(Version))
            {
                return YamlValueKind.String;
            }

#if NET
            if (type == typeof(Half) || type == typeof(UInt128) || type == typeof(Int128))
            {
                return YamlValueKind.Number;
            }
#endif
#if NET11_0_OR_GREATER
            if (type == typeof(System.Numerics.BFloat16) || type == typeof(System.Numerics.Decimal32) ||
                type == typeof(System.Numerics.Decimal64) || type == typeof(System.Numerics.Decimal128))
            {
                return YamlValueKind.Number;
            }
#endif
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => YamlValueKind.Undefined, // Can vary dependending on value.
                TypeCode.SByte => YamlValueKind.Number,
                TypeCode.Byte => YamlValueKind.Number,
                TypeCode.Int16 => YamlValueKind.Number,
                TypeCode.UInt16 => YamlValueKind.Number,
                TypeCode.Int32 => YamlValueKind.Number,
                TypeCode.UInt32 => YamlValueKind.Number,
                TypeCode.Int64 => YamlValueKind.Number,
                TypeCode.UInt64 => YamlValueKind.Number,
                TypeCode.Single => YamlValueKind.Number,
                TypeCode.Double => YamlValueKind.Number,
                TypeCode.Decimal => YamlValueKind.Number,
                TypeCode.String => YamlValueKind.String,
                TypeCode.Char => YamlValueKind.String,
                _ => null,
            };
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        [DebuggerDisplay("{Yaml,nq}")]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public YamlValue<TValue> _node;

            public DebugView(YamlValue<TValue> node)
            {
                _node = node;
            }

            public string Yaml => _node.ToYamlString();
            public string Path => _node.GetPath();
            public TValue? Value => _node.Value;
        }
    }
}
