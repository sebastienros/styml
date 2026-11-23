// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// A specialized converter implementation used for root-level value
    /// streaming in the YamlSerializer.DeserializeAsyncEnumerable methods.
    /// </summary>
    internal sealed class RootLevelListConverter<T> : YamlResumableConverter<List<T?>>
    {
        private readonly YamlTypeInfo<T> _elementTypeInfo;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Enumerable;
        internal override Type? ElementType => typeof(T);

        public RootLevelListConverter(YamlTypeInfo<T> elementTypeInfo)
        {
            IsRootLevelMultiContentStreamingConverter = true;
            _elementTypeInfo = elementTypeInfo;
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out List<T?>? value)
        {
            Debug.Assert(reader.AllowMultipleValues, "Can only be used by readers allowing trailing content.");

            YamlConverter<T> elementConverter = _elementTypeInfo.EffectiveConverter;
            state.Current.YamlPropertyInfo = _elementTypeInfo.PropertyInfoForTypeInfo;
            var results = (List<T?>?)state.Current.ReturnValue;

            while (true)
            {
                if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                {
                    if (!reader.TryAdvanceToNextRootLevelValueWithOptionalReadAhead(elementConverter.RequiresReadAhead, out bool isAtEndOfStream))
                    {
                        if (isAtEndOfStream)
                        {
                            // No more root-level JSON values in the stream
                            // complete the deserialization process.
                            value = results;
                            return true;
                        }

                        // New root-level JSON value found, need to read more data.
                        value = default;
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadValue;
                }

                // Deserialize the next root-level JSON value.
                if (!elementConverter.TryRead(ref reader, typeof(T), options, ref state, out T? element, out _))
                {
                    value = default;
                    return false;
                }

                if (results is null)
                {
                    state.Current.ReturnValue = results = [];
                }

                results.Add(element);
                state.Current.EndElement();
            }
        }
    }
}
