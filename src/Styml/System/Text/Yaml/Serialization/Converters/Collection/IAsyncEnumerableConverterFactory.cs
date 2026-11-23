// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization.Converters;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Converter for streaming <see cref="IAsyncEnumerable{T}" /> values.
    /// </summary>
    [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class IAsyncEnumerableConverterFactory : YamlConverterFactory
    {
        public IAsyncEnumerableConverterFactory() { }

        public override bool CanConvert(Type typeToConvert) => GetAsyncEnumerableInterface(typeToConvert) is not null;

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            Type? asyncEnumerableInterface = GetAsyncEnumerableInterface(typeToConvert);
            Debug.Assert(asyncEnumerableInterface is not null, $"{typeToConvert} not supported by converter.");

            Type elementType = asyncEnumerableInterface.GetGenericArguments()[0];
            Type converterType = typeof(IAsyncEnumerableOfTConverter<,>).MakeGenericType(typeToConvert, elementType);
            return (YamlConverter)Activator.CreateInstance(converterType)!;
        }

        private static Type? GetAsyncEnumerableInterface(Type type)
            => type.GetCompatibleGenericInterface(typeof(IAsyncEnumerable<>));
    }
}
