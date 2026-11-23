// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Defines the default, reflection-based YAML contract resolver used by System.Text.Yaml.
    /// </summary>
    /// <remarks>
    /// The contract resolver used by <see cref="YamlSerializerOptions.Default"/>.
    /// </remarks>
    public partial class DefaultYamlTypeInfoResolver : IYamlTypeInfoResolver, IBuiltInYamlTypeInfoResolver
    {
        private bool _mutable;

        /// <summary>
        /// Creates a mutable <see cref="DefaultYamlTypeInfoResolver"/> instance.
        /// </summary>
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        public DefaultYamlTypeInfoResolver() : this(mutable: true)
        {
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private DefaultYamlTypeInfoResolver(bool mutable)
        {
            _mutable = mutable;
        }

        /// <summary>
        /// Resolves a YAML contract for a given <paramref name="type"/> and <paramref name="options"/> configuration.
        /// </summary>
        /// <param name="type">The type for which to resolve a YAML contract.</param>
        /// <param name="options">A <see cref="YamlSerializerOptions"/> instance used to determine contract configuration.</param>
        /// <returns>A <see cref="YamlTypeInfo"/> defining a reflection-derived YAML contract for <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The base implementation of this method will produce a reflection-derived contract
        /// and apply any callbacks from the <see cref="Modifiers"/> list.
        /// </remarks>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
            Justification = "The ctor is marked RequiresDynamicCode.")]
        public virtual YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(options);

            _mutable = false;

            YamlTypeInfo.ValidateType(type);
            YamlTypeInfo typeInfo = CreateYamlTypeInfo(type, options);
            typeInfo.OriginatingResolver = this;

            // We've finished configuring the metadata, brand the instance as user-unmodified.
            // This should be the last update operation in the resolver to avoid resetting the flag.
            typeInfo.IsCustomized = false;

            if (_modifiers is not null)
            {
                foreach (Action<YamlTypeInfo> modifier in _modifiers)
                {
                    modifier(typeInfo);
                }
            }

            return typeInfo;
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlTypeInfo CreateYamlTypeInfo(Type type, YamlSerializerOptions options)
        {
            YamlConverter converter = GetConverterForType(type, options);
            return CreateTypeInfoCore(type, converter, options);
        }

        /// <summary>
        /// Gets a list of user-defined callbacks that can be used to modify the initial contract.
        /// </summary>
        /// <remarks>
        /// The modifier list will be rendered immutable after the first <see cref="GetTypeInfo(Type, YamlSerializerOptions)"/> invocation.
        ///
        /// Modifier callbacks are called consecutively in the order in which they are specified in the list.
        /// </remarks>
        public IList<Action<YamlTypeInfo>> Modifiers => _modifiers ??= new ModifierCollection(this);
        private ModifierCollection? _modifiers;

        private sealed class ModifierCollection : ConfigurationList<Action<YamlTypeInfo>>
        {
            private readonly DefaultYamlTypeInfoResolver _resolver;

            public ModifierCollection(DefaultYamlTypeInfoResolver resolver)
            {
                _resolver = resolver;
            }

            public override bool IsReadOnly => !_resolver._mutable;
            protected override void OnCollectionModifying()
            {
                if (!_resolver._mutable)
                {
                    ThrowHelper.ThrowInvalidOperationException_DefaultTypeInfoResolverImmutable();
                }
            }
        }

        bool IBuiltInYamlTypeInfoResolver.IsCompatibleWithOptions(YamlSerializerOptions _)
            // Metadata generated by the default resolver is compatible by definition,
            // provided that no user extensions have been made on the class.
            => _modifiers is null or { Count: 0 } && GetType() == typeof(DefaultYamlTypeInfoResolver);

        internal static DefaultYamlTypeInfoResolver DefaultInstance
        {
            [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
            get
            {
                if (s_defaultInstance is DefaultYamlTypeInfoResolver result)
                {
                    return result;
                }

                var newInstance = new DefaultYamlTypeInfoResolver(mutable: false);
                return Interlocked.CompareExchange(ref s_defaultInstance, newInstance, comparand: null) ?? newInstance;
            }
        }

        private static DefaultYamlTypeInfoResolver? s_defaultInstance;
    }
}
