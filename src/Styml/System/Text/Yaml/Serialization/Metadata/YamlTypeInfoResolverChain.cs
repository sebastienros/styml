// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization.Metadata
{
    internal class YamlTypeInfoResolverChain : ConfigurationList<IYamlTypeInfoResolver>, IYamlTypeInfoResolver, IBuiltInYamlTypeInfoResolver
    {
        public YamlTypeInfoResolverChain() : base(null) { }
        public override bool IsReadOnly => true;
        protected override void OnCollectionModifying()
            => ThrowHelper.ThrowInvalidOperationException_TypeInfoResolverChainImmutable();

        public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            foreach (IYamlTypeInfoResolver resolver in _list)
            {
                YamlTypeInfo? typeInfo = resolver.GetTypeInfo(type, options);
                if (typeInfo is not null)
                {
                    return typeInfo;
                }
            }

            return null;
        }

        internal void AddFlattened(IYamlTypeInfoResolver? resolver)
        {
            switch (resolver)
            {
                case null or EmptyYamlTypeInfoResolver:
                    break;

                case YamlTypeInfoResolverChain otherChain:
                    _list.AddRange(otherChain);
                    break;

                default:
                    _list.Add(resolver);
                    break;
            }
        }

        bool IBuiltInYamlTypeInfoResolver.IsCompatibleWithOptions(YamlSerializerOptions options)
        {
            foreach (IYamlTypeInfoResolver component in _list)
            {
                if (!component.IsCompatibleWithOptions(options))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("[");
            foreach (IYamlTypeInfoResolver resolver in _list)
            {
                sb.Append(resolver);
                sb.Append(", ");
            }

            if (_list.Count > 0)
                sb.Length -= 2;

            sb.Append(']');
            return sb.ToString();
        }
    }
}
