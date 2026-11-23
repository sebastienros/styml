// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>YamlObjectConverter{T}</cref> that supports the deserialization
    /// of YAML objects using parameterized constructors.
    /// </summary>
    internal class LargeObjectWithParameterizedConstructorConverter<T> : ObjectWithParameterizedConstructorConverter<T> where T : notnull
    {
        protected sealed override bool ReadAndCacheConstructorArgument(scoped ref ReadStack state, ref Utf8YamlReader reader, YamlParameterInfo yamlParameterInfo)
        {
            Debug.Assert(yamlParameterInfo.ShouldDeserialize);
            RefuseNullAsDefault(ref reader, yamlParameterInfo);

            bool success = yamlParameterInfo.EffectiveConverter.TryReadAsObject(ref reader, yamlParameterInfo.ParameterType, yamlParameterInfo.Options, ref state, out object? arg);

            if (success && !(arg is null && yamlParameterInfo.IgnoreNullTokensOnRead))
            {
                if (arg is null && !yamlParameterInfo.IsNullable && yamlParameterInfo.Options.RespectNullableAnnotations)
                {
                    ThrowHelper.ThrowYamlException_ConstructorParameterDisallowNull(yamlParameterInfo.Name, state.Current.YamlTypeInfo.Type);
                }

                ((object[])state.Current.CtorArgumentState!.Arguments)[yamlParameterInfo.Position] = arg!;
            }

            return success;
        }

        protected sealed override object CreateObject(ref ReadStackFrame frame)
        {
            Debug.Assert(frame.CtorArgumentState is not null);
            Debug.Assert(frame.YamlTypeInfo.CreateObjectWithArgs is not null);

            object[] arguments = (object[])frame.CtorArgumentState.Arguments;
            frame.CtorArgumentState.Arguments = null!;

            Func<object[], T> createObject = (Func<object[], T>)frame.YamlTypeInfo.CreateObjectWithArgs;

            object obj = createObject(arguments);

            ArrayPool<object>.Shared.Return(arguments, clearArray: true);
            return obj;
        }

        protected sealed override void InitializeConstructorArgumentCaches(ref ReadStack state, YamlSerializerOptions options)
        {
            YamlTypeInfo typeInfo = state.Current.YamlTypeInfo;

            object?[] arguments = ArrayPool<object>.Shared.Rent(typeInfo.ParameterCache.Length);
            foreach (YamlParameterInfo parameterInfo in typeInfo.ParameterCache)
            {
                arguments[parameterInfo.Position] = parameterInfo.EffectiveDefaultValue;
            }

            state.Current.CtorArgumentState!.Arguments = arguments;
        }
    }
}
