// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    /// A YamlValue encapsulating a primitive value using a built-in converter for the type.
    /// </summary>
    internal sealed class YamlValuePrimitive<TValue> : YamlValue<TValue>
    {
        private readonly YamlConverter<TValue> _converter;
        private readonly YamlValueKind _valueKind;

        public YamlValuePrimitive(TValue value, YamlConverter<TValue> converter, YamlNodeOptions? options) : base(value, options)
        {
            Debug.Assert(TypeIsSupportedPrimitive, $"The type {typeof(TValue)} is not a supported primitive.");
            Debug.Assert(converter is { IsInternalConverter: true, ConverterStrategy: ConverterStrategy.Value });

            _converter = converter;
            _valueKind = DetermineValueKind(value);
        }

        private protected override YamlValueKind GetValueKindCore() => _valueKind;
        internal override YamlNode DeepCloneCore() => new YamlValuePrimitive<TValue>(Value, _converter, Options);

        internal override bool DeepEqualsCore(YamlNode otherNode)
        {
            if (otherNode is YamlValue otherValue && otherValue.TryGetValue(out TValue? v))
            {
                // Because TValue is equatable and otherNode returns a matching
                // type we can short circuit the comparison in this case.
                return EqualityComparer<TValue>.Default.Equals(Value, v);
            }

            return base.DeepEqualsCore(otherNode);
        }

        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            WritePropertiesTo(writer);
            ArgumentNullException.ThrowIfNull(writer);

            YamlConverter<TValue> converter = _converter;
            options ??= s_defaultOptions;

            if (converter.IsInternalConverterForNumberType)
            {
                converter.WriteNumberWithCustomHandling(writer, Value, options.NumberHandling);
            }
            else
            {
                converter.Write(writer, Value, options);
            }
        }
    }
}
