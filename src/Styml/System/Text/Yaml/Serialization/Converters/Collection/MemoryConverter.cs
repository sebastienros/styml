// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class MemoryConverter<T> : YamlCollectionConverter<Memory<T>, T>
    {
        internal override bool CanHaveMetadata => false;
        public override bool HandleNull => true;

        internal override bool OnTryRead(
            ref Utf8YamlReader reader,
            Type typeToConvert,
            YamlSerializerOptions options,
            scoped ref ReadStack state,
            out Memory<T> value)
        {
            if ((reader.TokenType is YamlTokenType.Null || reader.IsLenientNull) && state.Current.ReturnValue is null)
            {
                value = default;
                return true;
            }

            return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
        }

        protected override void Add(in T value, ref ReadStack state)
        {
            ((List<T>)state.Current.ReturnValue!).Add(value);
        }

        protected override void CreateCollection(ref Utf8YamlReader reader, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            state.Current.ReturnValue = new List<T>();
        }

        internal sealed override bool IsConvertibleCollection => true;
        protected override void ConvertCollection(ref ReadStack state, YamlSerializerOptions options)
        {
            Memory<T> memory = ((List<T>)state.Current.ReturnValue!).ToArray().AsMemory();
            state.Current.ReturnValue = memory;
        }

        protected override bool OnWriteResume(Utf8YamlWriter writer, Memory<T> value, YamlSerializerOptions options, ref WriteStack state)
        {
            return ReadOnlyMemoryConverter<T>.OnWriteResume(writer, value.Span, options, ref state);
        }
    }
}
