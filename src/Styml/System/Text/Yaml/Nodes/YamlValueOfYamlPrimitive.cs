// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace System.Text.Yaml.Nodes
{
    internal static class YamlValueOfYamlPrimitive
    {
        internal static YamlValue CreatePrimitiveValue(ref Utf8YamlReader reader, YamlNodeOptions options)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.False:
                case YamlTokenType.True:
                    return new YamlValueOfYamlBool(reader.GetBoolean(), options);
                case YamlTokenType.String:
                    byte[] buffer = new byte[reader.ValueLength];
                    ReadOnlyMemory<byte> utf8String = buffer.AsMemory(0, reader.CopyString(buffer));
                    return new YamlValueOfYamlString(utf8String, options);
                case YamlTokenType.Number:
                    byte[] numberValue = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray();
                    return new YamlValueOfYamlNumber(numberValue, options);
                default:
                    Debug.Fail("Only primitives allowed.");
                    ThrowHelper.ThrowYamlException();
                    return null!; // Unreachable, but required for compilation.
            }
        }
    }

    internal sealed class YamlValueOfYamlString : YamlValue
    {
        private readonly ReadOnlyMemory<byte> _value;

        internal YamlValueOfYamlString(ReadOnlyMemory<byte> utf8String, YamlNodeOptions? options)
            : base(options)
        {
            _value = utf8String;
        }

        internal override YamlNode DeepCloneCore() => new YamlValueOfYamlString(_value, Options);
        private protected override YamlValueKind GetValueKindCore() => YamlValueKind.String;

        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            WritePropertiesTo(writer);
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStringValue(_value.Span);
        }

        public override T GetValue<T>()
        {
            if (!TryGetValue(out T? value))
            {
                ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvertElement(YamlValueKind.String, typeof(T));
            }

            return value;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T? value)
            where T : default
        {
            if (typeof(T) == typeof(YamlElement) || typeof(T) == typeof(YamlElement?) || typeof(T) == typeof(object))
            {
                value = (T)(object)YamlWriterHelper.WriteString(_value.Span, static serialized => YamlElement.Parse(serialized));
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                string? result = YamlReaderHelper.TranscodeHelper(_value.Span);

                Debug.Assert(result is not null);
                value = (T)(object)result;
                return true;
            }

            bool success;

            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            {
                success = YamlReaderHelper.TryGetValue(_value.Span, isEscaped: false, out DateTime result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
            {
                success = YamlReaderHelper.TryGetValue(_value.Span, isEscaped: false, out DateTimeOffset result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
            {
                success = YamlReaderHelper.TryGetValue(_value.Span, isEscaped: false, out Guid result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(char) || typeof(T) == typeof(char?))
            {
                string? result = YamlReaderHelper.TranscodeHelper(_value.Span);

                Debug.Assert(result is not null);
                if (result.Length == 1)
                {
                    value = (T)(object)result[0];
                    return true;
                }
            }

            value = default!;
            return false;
        }
    }

    internal sealed class YamlValueOfYamlBool : YamlValue
    {
        private readonly bool _value;

        private YamlValueKind ValueKind => _value ? YamlValueKind.True : YamlValueKind.False;

        internal YamlValueOfYamlBool(bool value, YamlNodeOptions? options)
            : base(options)
        {
            _value = value;
        }

        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null) => writer.WriteBooleanValue(_value);
        internal override YamlNode DeepCloneCore() => new YamlValueOfYamlBool(_value, Options);
        private protected override YamlValueKind GetValueKindCore() => ValueKind;

        public override T GetValue<T>()
        {
            if (!TryGetValue(out T? value))
            {
                ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvertElement(_value ? YamlValueKind.True : YamlValueKind.False, typeof(T));
            }

            return value;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T? value)
            where T : default
        {
            if (typeof(T) == typeof(YamlElement) || typeof(T) == typeof(YamlElement?) || typeof(T) == typeof(object))
            {
                value = (T)(object)YamlElement.Parse(_value ? YamlConstants.TrueValue : YamlConstants.FalseValue);
                return true;
            }

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                value = (T)(object)_value;
                return true;
            }

            value = default!;
            return false;
        }
    }

    internal sealed class YamlValueOfYamlNumber : YamlValue
    {
        // This can be optimized to store the decimal point position and the exponent so that
        // conversion to different numeric types can be done without parsing the string again.
        // Utf8Parser uses an internal ref struct, Number.NumberBuffer, which is really the
        // same functionality that we would want here.
        private readonly byte[] _value;

        internal YamlValueOfYamlNumber(byte[] number, YamlNodeOptions? options)
            : base(options)
        {
            _value = number;
        }

        internal override YamlNode DeepCloneCore() => new YamlValueOfYamlNumber(_value, Options);
        private protected override YamlValueKind GetValueKindCore() => YamlValueKind.Number;

        public override T GetValue<T>()
        {
            if (!TryGetValue(out T? value))
            {
                ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvertElement(YamlValueKind.Number, typeof(T));
            }

            return value;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T? value)
            where T : default
        {
            if (typeof(T) == typeof(YamlElement) || typeof(T) == typeof(YamlElement?) || typeof(T) == typeof(object))
            {
                value = (T)(object)YamlElement.Parse(_value);
                return true;
            }

            bool success;

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                success = Utf8Parser.TryParse(_value, out int result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
            {
                success = Utf8Parser.TryParse(_value, out long result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
            {
                success = Utf8Parser.TryParse(_value, out double result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(short) || typeof(T) == typeof(short?))
            {
                success = Utf8Parser.TryParse(_value, out short result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
            {
                success = Utf8Parser.TryParse(_value, out decimal result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte?))
            {
                success = Utf8Parser.TryParse(_value, out byte result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
            {
                success = Utf8Parser.TryParse(_value, out float result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                success = Utf8Parser.TryParse(_value, out uint result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?))
            {
                success = Utf8Parser.TryParse(_value, out ushort result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?))
            {
                success = Utf8Parser.TryParse(_value, out ulong result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?))
            {
                success = Utf8Parser.TryParse(_value, out sbyte result, out int consumed) &&
                            consumed == _value.Length;

                value = (T)(object)result;
                return success;
            }

            value = default!;
            return false;
        }

        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            WritePropertiesTo(writer);
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteNumberValue(_value);
        }
    }
}
