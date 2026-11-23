// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml.Serialization.Metadata
{
    internal sealed class YamlTypeInfoResolverWithAddedModifiers : IYamlTypeInfoResolver
    {
        private readonly IYamlTypeInfoResolver _source;
        private readonly Action<YamlTypeInfo>[] _modifiers;

        public YamlTypeInfoResolverWithAddedModifiers(IYamlTypeInfoResolver source, Action<YamlTypeInfo>[] modifiers)
        {
            Debug.Assert(modifiers.Length > 0);
            _source = source;
            _modifiers = modifiers;
        }

        public YamlTypeInfoResolverWithAddedModifiers WithAddedModifier(Action<YamlTypeInfo> modifier)
        {
            var newModifiers = new Action<YamlTypeInfo>[_modifiers.Length + 1];
            _modifiers.CopyTo(newModifiers, 0);
            newModifiers[_modifiers.Length] = modifier;

            return new YamlTypeInfoResolverWithAddedModifiers(_source, newModifiers);
        }

        public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            YamlTypeInfo? typeInfo = _source.GetTypeInfo(type, options);

            if (typeInfo is not null)
            {
                foreach (Action<YamlTypeInfo> modifier in _modifiers)
                {
                    modifier(typeInfo);
                }
            }

            return typeInfo;
        }
    }
}
