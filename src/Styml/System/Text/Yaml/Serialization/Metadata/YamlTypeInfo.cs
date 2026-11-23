// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Provides YAML serialization-related metadata about a type.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract partial class YamlTypeInfo
    {
        internal const string MetadataFactoryRequiresUnreferencedCode = "YAML serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Yaml source generation for native AOT applications.";

        internal const string YamlObjectTypeName = "System.Text.Yaml.Nodes.YamlObject";

        internal delegate T ParameterizedConstructorDelegate<T, TArg0, TArg1, TArg2, TArg3>(TArg0? arg0, TArg1? arg1, TArg2? arg2, TArg3? arg3);

        /// <summary>
        /// Negated bitmask of the required properties, indexed by <see cref="YamlPropertyInfo.PropertyIndex"/>.
        /// </summary>
        internal BitArray? OptionalPropertiesMask { get; private set; }
        internal bool ShouldTrackRequiredProperties => OptionalPropertiesMask is not null;

        internal YamlTypeInfo(Type type, YamlConverter converter, YamlSerializerOptions options)
        {
            Type = type;
            Options = options;
            Converter = converter;
            Kind = GetTypeInfoKind(type, converter);
            PropertyInfoForTypeInfo = CreatePropertyInfoForTypeInfo();
            ElementType = converter.ElementType;
            KeyType = converter.KeyType;
        }

        /// <summary>
        /// Gets the element type corresponding to an enumerable, dictionary or optional type.
        /// </summary>
        /// <remarks>
        /// Returns the element type for enumerable types, the value type for dictionary types,
        /// and the underlying type for <see cref="Nullable{T}"/> or F# optional types.
        ///
        /// Returns <see langword="null"/> for all other types or types using custom converters.
        /// </remarks>
        public Type? ElementType { get; }

        /// <summary>
        /// Gets the key type corresponding to a dictionary type.
        /// </summary>
        /// <remarks>
        /// Returns the key type for dictionary types.
        ///
        /// Returns <see langword="null"/> for all other types or types using custom converters.
        /// </remarks>
        public Type? KeyType { get; }

        /// <summary>
        /// Gets or sets a parameterless factory to be used on deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// A parameterless factory is not supported for the current metadata <see cref="Kind"/>.
        /// </exception>
        /// <remarks>
        /// If set to <see langword="null" />, any attempt to deserialize instances of the given type will result in an exception.
        ///
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// types with a single default constructor or default constructors annotated with <see cref="YamlConstructorAttribute"/>
        /// will be mapped to this delegate.
        /// </remarks>
        public Func<object>? CreateObject
        {
            get => _createObject;
            set
            {
                SetCreateObject(value);
            }
        }

        private protected abstract void SetCreateObject(Delegate? createObject);
        private protected Func<object>? _createObject;

        internal Func<object>? CreateObjectForExtensionDataProperty { get; set; }

        /// <summary>
        /// Gets or sets a callback to be invoked before serialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="YamlTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IYamlOnSerializing"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnSerializing
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind is not (YamlTypeInfoKind.Object or YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked after serialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="YamlTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IYamlOnSerialized"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnSerialized
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind is not (YamlTypeInfoKind.Object or YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked before deserialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="YamlTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IYamlOnDeserializing"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnDeserializing
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind is not (YamlTypeInfoKind.Object or YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (Converter.IsConvertibleCollection)
                {
                    // The values for convertible collections aren't available at the start of deserialization.
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOnDeserializingCallbacksNotSupported(Type);
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked after deserialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="YamlTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IYamlOnDeserialized"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnDeserialized
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind is not (YamlTypeInfoKind.Object or YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets the list of <see cref="YamlPropertyInfo"/> metadata corresponding to the current type.
        /// </summary>
        /// <remarks>
        /// Property is only applicable to metadata of kind <see cref="YamlTypeInfoKind.Object"/>.
        /// For other kinds an empty, read-only list will be returned.
        ///
        /// The order of <see cref="YamlPropertyInfo"/> entries in the list determines the serialization order,
        /// unless either of the entries specifies a non-zero <see cref="YamlPropertyInfo.Order"/> value,
        /// in which case the properties will be stable sorted by <see cref="YamlPropertyInfo.Order"/>.
        ///
        /// It is required that added <see cref="YamlPropertyInfo"/> entries are unique up to <see cref="YamlPropertyInfo.Name"/>,
        /// however this will only be validated on serialization, once the metadata instance gets locked for further modification.
        /// </remarks>
        public IList<YamlPropertyInfo> Properties => PropertyList;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal YamlPropertyInfoList PropertyList
        {
            get
            {
                return _properties ?? CreatePropertyList();
                YamlPropertyInfoList CreatePropertyList()
                {
                    var list = new YamlPropertyInfoList(this);
                    if (_sourceGenDelayedPropertyInitializer is { } propInit)
                    {
                        // .NET 6 source gen backward compatibility -- ensure that the
                        // property initializer delegate is invoked lazily.
                        YamlMetadataServices.PopulateProperties(this, list, propInit);
                    }

                    YamlPropertyInfoList? result = Interlocked.CompareExchange(ref _properties, list, null);
                    _sourceGenDelayedPropertyInitializer = null;
                    return result ?? list;
                }
            }
        }

        /// <summary>
        /// Stores the .NET 6-style property initialization delegate for delayed evaluation.
        /// </summary>
        internal Func<YamlSerializerContext, YamlPropertyInfo[]>? SourceGenDelayedPropertyInitializer
        {
            get => _sourceGenDelayedPropertyInitializer;
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(_properties is null, "must not be set if a property list has been initialized.");
                _sourceGenDelayedPropertyInitializer = value;
            }
        }

        private Func<YamlSerializerContext, YamlPropertyInfo[]>? _sourceGenDelayedPropertyInitializer;
        private YamlPropertyInfoList? _properties;

        /// <summary>
        /// Gets or sets a configuration object specifying polymorphism metadata.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="value" /> has been associated with a different <see cref="YamlTypeInfo"/> instance.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Polymorphic serialization is not supported for the current metadata <see cref="Kind"/>.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the configuration of this setting will be mapped from any <see cref="YamlDerivedTypeAttribute"/> or <see cref="YamlPolymorphicAttribute"/> annotations.
        /// </remarks>
        public YamlPolymorphismOptions? PolymorphismOptions
        {
            get => _polymorphismOptions;
            set
            {
                VerifyMutable();

                if (value is not null)
                {
                    if (Kind == YamlTypeInfoKind.None)
                    {
                        ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                    }

                    if (value.DeclaringTypeInfo is not null && value.DeclaringTypeInfo != this)
                    {
                        ThrowHelper.ThrowArgumentException_YamlPolymorphismOptionsAssociatedWithDifferentYamlTypeInfo(nameof(value));
                    }

                    value.DeclaringTypeInfo = this;
                }

                _polymorphismOptions = value;
            }
        }

        internal void SetPolymorphismOptions(YamlPolymorphismOptions options)
        {
            Debug.Assert(!IsReadOnly);
            Debug.Assert(options.DeclaringTypeInfo is null || options.DeclaringTypeInfo == this);

            options.DeclaringTypeInfo = this;
            _polymorphismOptions = options;
        }

        /// <summary>
        /// Specifies whether the current instance has been locked for modification.
        /// </summary>
        /// <remarks>
        /// A <see cref="YamlTypeInfo"/> instance can be locked either if
        /// it has been passed to one of the <see cref="YamlSerializer"/> methods,
        /// has been associated with a <see cref="YamlSerializerContext"/> instance,
        /// or a user explicitly called the <see cref="MakeReadOnly"/> method on the instance.
        /// </remarks>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Locks the current instance for further modification.
        /// </summary>
        /// <remarks>This method is idempotent.</remarks>
        public void MakeReadOnly() => IsReadOnly = true;

        private protected YamlPolymorphismOptions? _polymorphismOptions;

        /// <summary>
        /// Gets the list of union case type metadata for this type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is only meaningful when <see cref="Kind"/> is <see cref="YamlTypeInfoKind.Union"/>.
        /// The list is mutable during configuration and frozen at finalization time.
        /// </para>
        /// <para>
        /// For types recognized as unions via <c>System.Runtime.CompilerServices.UnionAttribute</c>,
        /// the list is automatically populated. For contract customization, users can
        /// populate this list manually.
        /// </para>
        /// </remarks>
        public IList<YamlUnionCaseInfo> UnionCases => UnionCaseList;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal YamlUnionCaseInfoList UnionCaseList
        {
            get
            {
                return _unionCases ?? CreateUnionCaseList();
                YamlUnionCaseInfoList CreateUnionCaseList()
                {
                    var list = new YamlUnionCaseInfoList(this);
                    YamlUnionCaseInfoList? result = Interlocked.CompareExchange(ref _unionCases, list, null);
                    return result ?? list;
                }
            }
        }

        private YamlUnionCaseInfoList? _unionCases;

        internal sealed class YamlUnionCaseInfoList : ConfigurationList<YamlUnionCaseInfo>
        {
            private readonly YamlTypeInfo _parent;

            public YamlUnionCaseInfoList(YamlTypeInfo parent, IEnumerable<YamlUnionCaseInfo>? source = null) : base(source)
            {
                _parent = parent;
            }

            public override bool IsReadOnly => _parent.IsReadOnly;
            protected override void OnCollectionModifying() => _parent.VerifyMutable();
        }

        /// <summary>
        /// Gets or sets the delegate that classifies YAML payloads to determine the target type
        /// during deserialization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the shared classification property for both polymorphic types and union types.
        /// </para>
        /// <para>
        /// For <strong>polymorphic types</strong>: when set, bypasses the standard discriminator-based
        /// type resolution (scanning for a <c>$type</c> property). When <see langword="null"/>
        /// (default), the existing discriminator-based approach is used unchanged.
        /// </para>
        /// <para>
        /// For <strong>union types</strong>: determines which case type matches the YAML payload.
        /// When set by the user, replaces the default token-based matching.
        /// </para>
        /// </remarks>
        public YamlTypeClassifier? TypeClassifier
        {
            get
            {
                if (_typeClassifierResolutionPending)
                {
                    ConfigureTypeClassifier();
                }

                return _typeClassifier;
            }
            set
            {
                VerifyMutable();
                // Explicit user assignment wins; discard any pending factory.
                _typeClassifier = value;
                _typeClassifierFactory = null;
                _typeClassifierResolutionPending = false;
            }
        }

        private YamlTypeClassifier? _typeClassifier;

        /// <summary>
        /// Gets or sets the weakly-typed delegate that deconstructs a union instance into
        /// its case type and case value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The delegate combines object classification and value extraction in a single call,
        /// returning a tuple of <c>(Type? CaseType, object? CaseValue)</c>.
        /// </para>
        /// <para>
        /// <c>CaseType</c> doubles as the discriminator for the union's null state:
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       A non-<see langword="null"/> <c>CaseType</c> instructs the converter to serialize
        ///       <c>CaseValue</c> using the <see cref="YamlTypeInfo"/> registered for that case
        ///       type. <c>CaseValue</c> may be <see langword="null"/>, in which case the YAML
        ///       output is <c>null</c> rendered through that case type's contract (this preserves
        ///       per-case null annotations). The <c>CaseType</c> must be one of the union's
        ///       declared case types; returning a runtime type that is more derived than any
        ///       declared case violates STJ contract invariants.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       A <see langword="null"/> <c>CaseType</c> signals that the instance is the
        ///       canonical null union value. The converter writes YAML <c>null</c> directly,
        ///       bypassing all per-case contracts; <c>CaseValue</c> is ignored.
        ///     </description>
        ///   </item>
        /// </list>
        /// <para>
        /// Prefer setting the strongly-typed <see cref="YamlTypeInfo{T}.UnionDeconstructor"/>
        /// on <see cref="YamlTypeInfo{T}"/> to avoid boxing when the union type is a value type.
        /// </para>
        /// </remarks>
        public Func<object, (Type? CaseType, object? CaseValue)>? UnionDeconstructor
        {
            get => _unionDeconstructor;
            set
            {
                VerifyMutable();
                SetUnionDeconstructor(value);
            }
        }

        private protected virtual void SetUnionDeconstructor(Delegate? deconstructor)
        {
            Debug.Assert(deconstructor is null or Func<object, (Type?, object?)>);
            _unionDeconstructor = (Func<object, (Type?, object?)>?)deconstructor;
        }

        private protected Func<object, (Type?, object?)>? _unionDeconstructor;

        /// <summary>
        /// Gets or sets the weakly-typed delegate that constructs a union instance from
        /// a case type and case value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The delegate takes a <see cref="Type"/> parameter naming the resolved case type
        /// (as produced by classification) and an <c>object?</c> case value, returning the
        /// constructed union instance. The <c>caseType</c> parameter is always non-<see langword="null"/>
        /// and disambiguates among overlapping case types (e.g., <c>Labrador : Dog</c>).
        /// </para>
        /// <para>
        /// The delegate also encapsulates the union's null-handling policy. When the
        /// converter encounters a YAML <see cref="YamlTokenType.Null"/> token, it bypasses
        /// any configured <see cref="TypeClassifier"/> and invokes the delegate with a
        /// <see langword="null"/> case value. Implementations should produce the canonical
        /// null-holding union instance when at least one case is nullable, and otherwise
        /// throw a <see cref="YamlException"/>. On the null path the <c>caseType</c> argument
        /// is set to one of the declared nullable case types but its specific value should
        /// be ignored; per union semantics, all nullable cases collapse to the same null
        /// instance.
        /// </para>
        /// <para>
        /// Prefer setting the strongly-typed <see cref="YamlTypeInfo{T}.UnionConstructor"/>
        /// on <see cref="YamlTypeInfo{T}"/> to avoid boxing when the union type is a value type.
        /// </para>
        /// </remarks>
        public Func<Type, object?, object>? UnionConstructor
        {
            get => _unionConstructor;
            set
            {
                VerifyMutable();
                SetUnionConstructor(value);
            }
        }

        private protected virtual void SetUnionConstructor(Delegate? constructor)
        {
            Debug.Assert(constructor is null or Func<Type, object?, object>);
            _unionConstructor = (Func<Type, object?, object>?)constructor;
        }

        private protected Func<Type, object?, object>? _unionConstructor;

        /// <summary>
        /// Pre-built YAML value shape to type map for default union deserialization (no custom classifier).
        /// Maps each <see cref="YamlValueType"/> to the first-declared case type that
        /// serializes as that value shape. Populated at configuration time from <see cref="UnionCases"/>.
        /// </summary>
        internal Dictionary<YamlValueType, Type>? UnionValueTypeMap { get; set; }

        /// <summary>
        /// Bitmask of <see cref="YamlValueType"/> shapes that two or more declared case types both
        /// serialize as. Populated alongside <see cref="UnionValueTypeMap"/>; used at deserialize
        /// time to surface a precise "ambiguous case" error when one of these value shapes appears.
        /// <see cref="YamlValueType.None"/> means no ambiguous shapes were detected.
        /// </summary>
        internal YamlValueType UnionAmbiguousValueTypes { get; set; }

        /// <summary>
        /// First nullable union case type, if any. Populated at configuration time from <see cref="UnionCases"/>.
        /// </summary>
        internal Type? UnionNullableCaseType { get; set; }

        /// <summary>
        /// Optional per-type factory captured during metadata creation (resolver phase)
        /// from <see cref="YamlUnionAttribute.TypeClassifier"/> or <see cref="YamlPolymorphicAttribute.TypeClassifier"/>.
        /// </summary>
        internal YamlTypeClassifierFactory? TypeClassifierFactory
        {
            get => _typeClassifierFactory;
            set
            {
                Debug.Assert(!IsReadOnly);
                _typeClassifierFactory = value;
            }
        }

        /// <summary>
        /// Indicates whether type classifier resolution should run lazily from the
        /// <see cref="TypeClassifier"/> getter or during metadata configuration.
        /// </summary>
        internal bool TypeClassifierResolutionPending
        {
            get => _typeClassifierResolutionPending;
            set
            {
                Debug.Assert(!IsReadOnly);
                _typeClassifierResolutionPending = value;
            }
        }

        private YamlTypeClassifierFactory? _typeClassifierFactory;
        private volatile bool _typeClassifierResolutionPending;

        internal object? CreateObjectWithArgs { get; set; }

        // Add method delegate for non-generic Stack and Queue; and types that derive from them.
        internal object? AddMethodDelegate { get; set; }

        internal YamlPropertyInfo? ExtensionDataProperty { get; private set; }

        internal PolymorphicTypeResolver? PolymorphicTypeResolver { get; private set; }

        // Indicates that SerializeHandler is populated.
        internal bool HasSerializeHandler { get; private protected set; }

        // Indicates that SerializeHandler is populated and is compatible with the associated contract metadata.
        internal bool CanUseSerializeHandler { get; private set; }

        // Configure would normally have thrown why initializing properties for source gen but type had SerializeHandler
        // so it is allowed to be used for fast-path serialization but it will throw if used for metadata-based serialization
        internal bool PropertyMetadataSerializationNotSupported { get; set; }

        internal bool IsNullable => Converter.NullableElementConverter is not null;
        internal bool CanBeNull => PropertyInfoForTypeInfo.PropertyTypeCanBeNull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ValidateCanBeUsedForPropertyMetadataSerialization()
        {
            if (PropertyMetadataSerializationNotSupported)
            {
                ThrowHelper.ThrowInvalidOperationException_NoMetadataForTypeProperties(Options.TypeInfoResolver, Type);
            }
        }

        /// <summary>
        /// Return the YamlTypeInfo for the element type, or null if the type is not an enumerable or dictionary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal YamlTypeInfo? ElementTypeInfo
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_elementTypeInfo is null or { IsConfigurationStarted: true });
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider YamlTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                YamlTypeInfo? elementTypeInfo = _elementTypeInfo;
                elementTypeInfo?.EnsureConfigured();
                return elementTypeInfo;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(value is null || value.Type == ElementType);
                _elementTypeInfo = value;
            }
        }

        /// <summary>
        /// Return the YamlTypeInfo for the key type, or null if the type is not a dictionary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal YamlTypeInfo? KeyTypeInfo
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_keyTypeInfo is null or { IsConfigurationStarted: true });
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider YamlTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                YamlTypeInfo? keyTypeInfo = _keyTypeInfo;
                keyTypeInfo?.EnsureConfigured();
                return keyTypeInfo;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(value is null || value.Type == KeyType);
                _keyTypeInfo = value;
            }
        }

        private YamlTypeInfo? _elementTypeInfo;
        private YamlTypeInfo? _keyTypeInfo;

        /// <summary>
        /// Gets the <see cref="YamlSerializerOptions"/> value associated with the current <see cref="YamlTypeInfo" /> instance.
        /// </summary>
        public YamlSerializerOptions Options { get; }

        /// <summary>
        /// Gets the <see cref="Type"/> for which the YAML serialization contract is being defined.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the <see cref="YamlConverter"/> associated with the current type.
        /// </summary>
        /// <remarks>
        /// The <see cref="YamlConverter"/> associated with the type determines the value of <see cref="Kind"/>,
        /// and by extension the types of metadata that are configurable in the current YAML contract.
        /// As such, the value of the converter cannot be changed once a <see cref="YamlTypeInfo"/> instance has been created.
        /// </remarks>
        public YamlConverter Converter { get; }

        /// <summary>
        /// Determines the kind of contract metadata that the current instance is specifying.
        /// </summary>
        /// <remarks>
        /// The value of <see cref="Kind"/> determines what aspects of the YAML contract are configurable.
        /// For example, it is only possible to configure the <see cref="Properties"/> list for metadata
        /// of kind <see cref="YamlTypeInfoKind.Object"/>.
        ///
        /// The value of <see cref="Kind"/> is determined exclusively by the <see cref="YamlConverter"/>
        /// resolved for the current type, and cannot be changed once resolution has happened.
        /// User-defined custom converters (specified either via <see cref="YamlConverterAttribute"/> or <see cref="YamlSerializerOptions.Converters"/>)
        /// are metadata-agnostic and thus always resolve to <see cref="YamlTypeInfoKind.None"/>.
        /// </remarks>
        public YamlTypeInfoKind Kind { get; }

        /// <summary>
        /// Dummy <see cref="YamlPropertyInfo"/> instance corresponding to the declaring type of this <see cref="YamlTypeInfo"/>.
        /// </summary>
        /// <remarks>
        /// Used as convenience in cases where we want to serialize property-like values that do not define property metadata, such as:
        /// 1. a collection element type,
        /// 2. a dictionary key or value type or,
        /// 3. the property metadata for the root-level value.
        /// For example, for a property returning <see cref="List{T}"/> where T is a string,
        /// a YamlTypeInfo will be created with .Type=typeof(string) and .PropertyInfoForTypeInfo=YamlPropertyInfo{string}.
        /// </remarks>
        internal YamlPropertyInfo PropertyInfoForTypeInfo { get; }

        private protected abstract YamlPropertyInfo CreatePropertyInfoForTypeInfo();

        /// <summary>
        /// Gets or sets the type-level <see cref="YamlSerializerOptions.NumberHandling"/> override.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="YamlNumberHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="YamlNumberHandlingAttribute"/> annotations.
        /// </remarks>
        public YamlNumberHandling? NumberHandling
        {
            get => _numberHandling;
            set
            {
                VerifyMutable();

                if (value is not null && !YamlSerializer.IsValidNumberHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _numberHandling = value;
            }
        }

        internal YamlNumberHandling EffectiveNumberHandling => _numberHandling ?? Options.NumberHandling;
        private YamlNumberHandling? _numberHandling;

        /// <summary>
        /// Gets or sets the type-level <see cref="YamlUnmappedMemberHandling"/> override.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Unmapped member handling only supported for <see cref="YamlTypeInfoKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="YamlUnmappedMemberHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="YamlUnmappedMemberHandlingAttribute"/> annotations.
        /// </remarks>
        public YamlUnmappedMemberHandling? UnmappedMemberHandling
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind != YamlTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (value is not null && !YamlSerializer.IsValidUnmappedMemberHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                field = value;
            }
        }

        internal YamlUnmappedMemberHandling EffectiveUnmappedMemberHandling { get; private set; }

        /// <summary>
        /// Gets or sets the preferred <see cref="YamlObjectCreationHandling"/> value for properties contained in the type.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Unmapped member handling only supported for <see cref="YamlTypeInfoKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="YamlObjectCreationHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this callback will be mapped from <see cref="YamlObjectCreationHandlingAttribute"/> annotations on types.
        /// </remarks>
        public YamlObjectCreationHandling? PreferredPropertyObjectCreationHandling
        {
            get;
            set
            {
                VerifyMutable();

                if (Kind != YamlTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (value is not null && !YamlSerializer.IsValidCreationHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IYamlTypeInfoResolver"/> from which this metadata instance originated.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Metadata used to determine the <see cref="YamlSerializerContext.GeneratedSerializerOptions"/>
        /// configuration for the current metadata instance.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IYamlTypeInfoResolver? OriginatingResolver
        {
            get;
            set
            {
                VerifyMutable();

                if (value is YamlSerializerContext)
                {
                    // The source generator uses this property setter to brand the metadata instance as user-unmodified.
                    // Even though users could call the same property setter to unset this flag, this is generally speaking fine.
                    // This flag is only used to determine fast-path invalidation, worst case scenario this would lead to a false negative.
                    IsCustomized = false;
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets an attribute provider corresponding to the deserialization constructor.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// When resolving metadata via the built-in resolvers this will be populated with
        /// the underlying <see cref="ConstructorInfo" /> of the serialized property or field.
        /// </remarks>
        public ICustomAttributeProvider? ConstructorAttributeProvider
        {
            get
            {
                Func<ICustomAttributeProvider>? ctorAttrProviderFactory = Volatile.Read(ref ConstructorAttributeProviderFactory);
                ICustomAttributeProvider? ctorAttrProvider = _constructorAttributeProvider;

                if (ctorAttrProvider is null && ctorAttrProviderFactory is not null)
                {
                    _constructorAttributeProvider = ctorAttrProvider = ctorAttrProviderFactory();
                    Volatile.Write(ref ConstructorAttributeProviderFactory, null);
                }

                return ctorAttrProvider;
            }
            internal set
            {
                Debug.Assert(!IsReadOnly);

                _constructorAttributeProvider = value;
                Volatile.Write(ref ConstructorAttributeProviderFactory, null);
            }
        }

        // Metadata emanating from the source generator use delayed attribute provider initialization
        // ensuring that reflection metadata resolution remains pay-for-play and is trimmable.
        internal Func<ICustomAttributeProvider>? ConstructorAttributeProviderFactory;
        private ICustomAttributeProvider? _constructorAttributeProvider;

        internal void VerifyMutable()
        {
            if (IsReadOnly)
            {
                ThrowHelper.ThrowInvalidOperationException_TypeInfoImmutable();
            }

            IsCustomized = true;
        }

        /// <summary>
        /// Indicates that the current YamlTypeInfo might contain user modifications.
        /// Defaults to true, and is only unset by the built-in contract resolvers.
        /// </summary>
        internal bool IsCustomized { get; set; } = true;

        internal bool IsConfigured => _configurationState == ConfigurationState.Configured;
        internal bool IsConfigurationStarted => _configurationState is not ConfigurationState.NotConfigured;
        private volatile ConfigurationState _configurationState;
        private enum ConfigurationState : byte
        {
            NotConfigured = 0,
            Configuring = 1,
            Configured = 2
        };

        private ExceptionDispatchInfo? _cachedConfigureError;

        internal void EnsureConfigured()
        {
            if (!IsConfigured)
                ConfigureSynchronized();

            void ConfigureSynchronized()
            {
                Options.MakeReadOnly();
                MakeReadOnly();

                _cachedConfigureError?.Throw();

                lock (Options.CacheContext)
                {
                    if (_configurationState != ConfigurationState.NotConfigured)
                    {
                        // The value of _configurationState is either
                        //    'Configuring': recursive instance configured by this thread or
                        //    'Configured' : instance already configured by another thread.
                        // We can safely yield the configuration operation in both cases.
                        return;
                    }

                    _cachedConfigureError?.Throw();

                    try
                    {
                        _configurationState = ConfigurationState.Configuring;
                        Configure();
                        _configurationState = ConfigurationState.Configured;
                    }
                    catch (Exception e)
                    {
                        _cachedConfigureError = ExceptionDispatchInfo.Capture(e);
                        _configurationState = ConfigurationState.NotConfigured;
                        throw;
                    }
                }
            }
        }

        private void Configure()
        {
            Debug.Assert(Monitor.IsEntered(Options.CacheContext), "Configure called directly, use EnsureConfigured which synchronizes access to this method");
            Debug.Assert(Options.IsReadOnly);
            Debug.Assert(IsReadOnly);

            PropertyInfoForTypeInfo.Configure();

            if (PolymorphismOptions is not null)
            {
                // This needs to be done before ConfigureProperties() is called
                // YamlPropertyInfo.Configure() must have this value available in order to detect Polymoprhic + cyclic class case
                PolymorphicTypeResolver = new PolymorphicTypeResolver(Options, PolymorphismOptions, Type, Converter.CanHaveMetadata);
                ConfigureTypeClassifier();
            }
            else if (Options.HasTagMappings && !Type.IsValueType)
            {
                // M5d: a reference-type site with no declared hierarchy reads the mapped types it can hold.
                PolymorphicTypeResolver = PolymorphicTypeResolver.CreateForTagMappings(Options, Type, Converter.CanHaveMetadata);
            }

            if (Kind == YamlTypeInfoKind.Object)
            {
                ConfigureProperties();

                if (DetermineUsesParameterizedConstructor())
                {
                    ConfigureConstructorParameters();
                }
            }

            if (ElementType != null)
            {
                _elementTypeInfo ??= Options.GetTypeInfoInternal(ElementType);
                _elementTypeInfo.EnsureConfigured();
            }

            if (KeyType != null)
            {
                _keyTypeInfo ??= Options.GetTypeInfoInternal(KeyType);
                _keyTypeInfo.EnsureConfigured();
            }

            DetermineIsCompatibleWithCurrentOptions();
            CanUseSerializeHandler = HasSerializeHandler && IsCompatibleWithCurrentOptions;

            // Validate union metadata before sealing. By this point, every modifier has run
            // and any contract customization that the user wanted to apply has been applied,
            // so the final shape of the YamlTypeInfo can be checked authoritatively.
            if (Kind is YamlTypeInfoKind.Union)
            {
                ValidateUnionContract();
                CacheUnionNullableCaseType();
                ConfigureTypeClassifier();
            }

            // Build the union dispatch map after modifiers have had a chance to install
            // a custom TypeClassifier. The classifier path bypasses the value-shape map entirely.
            if (Kind is YamlTypeInfoKind.Union &&
                _typeClassifier is null &&
                UnionCases.Count > 0)
            {
                BuildUnionValueTypeMap(UnionCases, Options, this);
            }
        }

        /// <summary>
        /// Validates that a union <see cref="YamlTypeInfo"/> has a well-formed shape after
        /// all modifiers have run: at least one declared case, plus both the constructor
        /// and the deconstructor delegates.
        /// </summary>
        private void ValidateUnionContract()
        {
            Debug.Assert(Kind is YamlTypeInfoKind.Union);

            if (UnionCases.Count == 0)
            {
                ThrowHelper.ThrowInvalidOperationException_UnionCasesNotPopulated(Type);
            }

            if (UnionConstructor is null)
            {
                ThrowHelper.ThrowInvalidOperationException_UnionCannotCreateValue(Type);
            }

            if (UnionDeconstructor is null)
            {
                ThrowHelper.ThrowInvalidOperationException_UnionCannotReadValue(Type);
            }
        }

        private void CacheUnionNullableCaseType()
        {
            Debug.Assert(Kind is YamlTypeInfoKind.Union);

            foreach (YamlUnionCaseInfo unionCase in UnionCases)
            {
                if (unionCase.IsNullable)
                {
                    UnionNullableCaseType = unionCase.CaseType;
                    return;
                }
            }

            UnionNullableCaseType = null;
        }

        private void ConfigureTypeClassifier()
        {
            Debug.Assert(Kind is YamlTypeInfoKind.Union || PolymorphismOptions is not null);

            // The classifier factory may throw, which is fine --
            // If invoked by the getter ahead of configure we rethrow directly,
            // but if invoked by the configuration pipeline it captures and caches the exception.

            YamlTypeClassifierFactory? factory = Volatile.Read(ref _typeClassifierFactory);
            if (!_typeClassifierResolutionPending)
            {
                return;
            }

            YamlTypeClassifierContext ctx;
            if (Kind is YamlTypeInfoKind.Union)
            {
                Debug.Assert(UnionCases.Count > 0);
                ctx = new YamlTypeClassifierContext(
                    YamlTypeClassifierKind.Union,
                    this,
                    new List<YamlUnionCaseInfo>(UnionCases),
                    Array.Empty<YamlDerivedType>(),
                    typeDiscriminatorPropertyName: null);
            }
            else
            {
                YamlPolymorphismOptions? polymorphismOptions = PolymorphismOptions;
                Debug.Assert(polymorphismOptions is not null);

                ctx = new YamlTypeClassifierContext(
                    YamlTypeClassifierKind.PolymorphicType,
                    this,
                    Array.Empty<YamlUnionCaseInfo>(),
                    new List<YamlDerivedType>(polymorphismOptions.DerivedTypes),
                    polymorphismOptions.TypeDiscriminatorPropertyName);
            }

            if (factory is not null)
            {
                if (!factory.CanClassify(ctx))
                {
                    ThrowHelper.ThrowInvalidOperationException_TypeClassifierNotSupported(factory.GetType(), Type);
                }
            }
            else
            {
                factory = Options.GetTypeClassifierFromList(ctx);
            }

            if (factory is not null)
            {
                YamlTypeClassifier classifier = factory.CreateYamlClassifier(ctx, Options);
                Interlocked.CompareExchange(ref _typeClassifier, classifier, null);
            }

            _typeClassifierFactory = null;
            _typeClassifierResolutionPending = false;
        }

        /// <summary>
        /// Builds the union dispatch metadata: a value-shape-to-type map for default deserialization
        /// plus diagnostic state for cases that cannot be unambiguously classified by value shape.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This helper is intentionally lenient at configuration time so that union types are
        /// always serializable (serialization is driven by <see cref="UnionDeconstructor"/> and
        /// doesn't need the value-shape map). Diagnostic state is recorded on the type info and
        /// surfaced as a <see cref="YamlException"/> only at deserialization time when a token
        /// is actually encountered that cannot be unambiguously routed to a single case.
        /// </para>
        /// <para>
        /// Each case is categorized by its supported YAML value shapes via
        /// <see cref="YamlConverter.GetSupportedYamlValueTypes"/>. User-defined converters
        /// are conservatively classified as potentially representing every YAML value shape.
        /// </para>
        /// <para>
        /// This helper operates purely on already-resolved <see cref="YamlTypeInfo"/> /
        /// <see cref="YamlConverter"/> metadata and does not perform any reflection, so it is
        /// safe to call from the trim-safe configuration pipeline.
        /// </para>
        /// </remarks>
        private static void BuildUnionValueTypeMap(IList<YamlUnionCaseInfo> unionCases, YamlSerializerOptions options, YamlTypeInfo target)
        {
            var map = new Dictionary<YamlValueType, Type>();
            YamlValueType ambiguousValueTypes = YamlValueType.None;

            foreach (YamlUnionCaseInfo info in unionCases)
            {
                Type caseType = info.CaseType;
                YamlTypeInfo caseTypeInfo = options.GetTypeInfoInternal(caseType);
                YamlConverter converter = caseTypeInfo.Converter;
                if (converter.ConverterStrategy is ConverterStrategy.Union)
                {
                    continue;
                }

                YamlNumberHandling effectiveNumberHandling =
                    target.NumberHandling ?? caseTypeInfo.NumberHandling ?? options.NumberHandling;
                YamlValueType valueTypes = converter.GetSupportedYamlValueTypes(effectiveNumberHandling);

                AddUnionValueTypes(valueTypes, caseType, map, ref ambiguousValueTypes);
            }

            target.UnionValueTypeMap = map;
            target.UnionAmbiguousValueTypes = ambiguousValueTypes;

            static void AddUnionValueTypes(
                YamlValueType valueTypes,
                Type caseType,
                Dictionary<YamlValueType, Type> map,
                ref YamlValueType ambiguousValueTypes)
            {
                ReadOnlySpan<YamlValueType> allValueTypes =
                [
                    YamlValueType.Object,
                    YamlValueType.Array,
                    YamlValueType.String,
                    YamlValueType.Number,
                    YamlValueType.Boolean,
                ];

                foreach (YamlValueType valueType in allValueTypes)
                {
                    if ((valueTypes & valueType) == 0)
                    {
                        continue;
                    }

                    if (!map.TryAdd(valueType, caseType))
                    {
                        // Two declared cases share this JSON value shape. First-wins for the dispatch
                        // map (so unrelated values still deserialize), but record the ambiguity
                        // so deserialize-time can throw a precise error if this shape shows up.
                        ambiguousValueTypes |= valueType;
                    }
                }
            }
        }

        /// <summary>
        /// Gets any ancestor polymorphic types that declare
        /// a type discriminator for the current type. Consulted
        /// when serializing polymorphic values as objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal YamlTypeInfo? AncestorPolymorphicType
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(Type != typeof(object));

                if (!_isAncestorPolymorphicTypeResolved)
                {
                    _ancestorPolymorhicType = PolymorphicTypeResolver.FindNearestPolymorphicBaseType(this);
                    _isAncestorPolymorphicTypeResolved = true;
                }

                return _ancestorPolymorhicType;
            }
        }

        private YamlTypeInfo? _ancestorPolymorhicType;
        private volatile bool _isAncestorPolymorphicTypeResolved;

        /// <summary>
        /// Determines if the transitive closure of all YamlTypeInfo metadata referenced
        /// by the current type (property types, key types, element types, ...) are
        /// compatible with the settings as specified in YamlSerializerOptions.
        /// </summary>
        private void DetermineIsCompatibleWithCurrentOptions()
        {
            // Defines a recursive algorithm validating that the `IsCurrentNodeCompatible`
            // predicate is valid for every node in the type graph. This method only checks
            // the immediate children, with recursion being driven by the Configure() method.
            // Therefore, this method must be called _after_ the child nodes have been configured.

            Debug.Assert(IsReadOnly);
            Debug.Assert(!IsConfigured);

            if (!IsCurrentNodeCompatible())
            {
                IsCompatibleWithCurrentOptions = false;
                return;
            }

            if (_properties is not null)
            {
                foreach (YamlPropertyInfo property in _properties)
                {
                    Debug.Assert(property.IsConfigured);

                    if (!property.IsPropertyTypeInfoConfigured)
                    {
                        // Either an ignored property or property is part of a cycle.
                        // In both cases we can ignore these instances.
                        continue;
                    }

                    if (!property.YamlTypeInfo.IsCompatibleWithCurrentOptions)
                    {
                        IsCompatibleWithCurrentOptions = false;
                        return;
                    }
                }
            }

            if (_elementTypeInfo?.IsCompatibleWithCurrentOptions == false ||
                _keyTypeInfo?.IsCompatibleWithCurrentOptions == false)
            {
                IsCompatibleWithCurrentOptions = false;
                return;
            }

            Debug.Assert(IsCompatibleWithCurrentOptions);

            // Defines the core predicate that must be checked for every node in the type graph.
            bool IsCurrentNodeCompatible()
            {
                if (IsCustomized)
                {
                    // Return false if we have detected contract customization by the user.
                    return false;
                }

                if (Options.CanUseFastPathSerializationLogic)
                {
                    // Simple case/backward compatibility: options uses a combination of compatible built-in converters.
                    return true;
                }

                return OriginatingResolver.IsCompatibleWithOptions(Options);
            }
        }

        /// <summary>
        /// Holds the result of the above algorithm -- NB must default to true
        /// to establish a base case for recursive types and any YamlIgnored property types.
        /// </summary>
        private bool IsCompatibleWithCurrentOptions { get; set; } = true;

        /// <summary>
        /// Determine if the current configuration is compatible with using a parameterized constructor.
        /// </summary>
        internal bool DetermineUsesParameterizedConstructor()
            => Converter.ConstructorIsParameterized && CreateObject is null;

        /// <summary>
        /// Creates a blank <see cref="YamlTypeInfo{T}"/> instance.
        /// </summary>
        /// <typeparam name="T">The type for which contract metadata is specified.</typeparam>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> instance the metadata is associated with.</param>
        /// <returns>A blank <see cref="YamlTypeInfo{T}"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <remarks>
        /// The returned <see cref="YamlTypeInfo{T}"/> will be blank, with the exception of the
        /// <see cref="Converter"/> property which will be resolved either from
        /// <see cref="YamlSerializerOptions.Converters"/> or the built-in converters for the type.
        /// Any converters specified via <see cref="YamlConverterAttribute"/> on the type declaration
        /// will not be resolved by this method.
        ///
        /// What converter does get resolved influences the value of <see cref="Kind"/>,
        /// which constrains the type of metadata that can be modified in the <see cref="YamlTypeInfo"/> instance.
        /// </remarks>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public static YamlTypeInfo<T> CreateYamlTypeInfo<T>(YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            YamlConverter converter = DefaultYamlTypeInfoResolver.GetConverterForType(typeof(T), options, resolveYamlConverterAttribute: false);
            return new YamlTypeInfo<T>(converter, options);
        }

        /// <summary>
        /// Creates a blank <see cref="YamlTypeInfo"/> instance.
        /// </summary>
        /// <param name="type">The type for which contract metadata is specified.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> instance the metadata is associated with.</param>
        /// <returns>A blank <see cref="YamlTypeInfo"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> cannot be used for serialization.</exception>
        /// <remarks>
        /// The returned <see cref="YamlTypeInfo"/> will be blank, with the exception of the
        /// <see cref="Converter"/> property which will be resolved either from
        /// <see cref="YamlSerializerOptions.Converters"/> or the built-in converters for the type.
        /// Any converters specified via <see cref="YamlConverterAttribute"/> on the type declaration
        /// will not be resolved by this method.
        ///
        /// What converter does get resolved influences the value of <see cref="Kind"/>,
        /// which constrains the type of metadata that can be modified in the <see cref="YamlTypeInfo"/> instance.
        /// </remarks>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public static YamlTypeInfo CreateYamlTypeInfo(Type type, YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(options);

            if (IsInvalidForSerialization(type))
            {
                ThrowHelper.ThrowArgumentException_CannotSerializeInvalidType(nameof(type), type, null, null);
            }

            YamlConverter converter = DefaultYamlTypeInfoResolver.GetConverterForType(type, options, resolveYamlConverterAttribute: false);
            return CreateYamlTypeInfo(type, converter, options);
        }

        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        internal static YamlTypeInfo CreateYamlTypeInfo(Type type, YamlConverter converter, YamlSerializerOptions options)
        {
            YamlTypeInfo yamlTypeInfo;

            if (converter.Type == type)
            {
                // For performance, avoid doing a reflection-based instantiation
                // if the converter type matches that of the declared type.
                yamlTypeInfo = converter.CreateYamlTypeInfo(options);
            }
            else
            {
                Type yamlTypeInfoType = typeof(YamlTypeInfo<>).MakeGenericType(type);
                yamlTypeInfo = (YamlTypeInfo)yamlTypeInfoType.CreateInstanceNoWrapExceptions(
                    parameterTypes: [typeof(YamlConverter), typeof(YamlSerializerOptions)],
                    parameters: new object[] { converter, options })!;
            }

            Debug.Assert(yamlTypeInfo.Type == type);
            return yamlTypeInfo;
        }

        /// <summary>
        /// Creates a blank <see cref="YamlPropertyInfo"/> instance for the current <see cref="YamlTypeInfo"/>.
        /// </summary>
        /// <param name="propertyType">The declared type for the property.</param>
        /// <param name="name">The property name used in YAML serialization and deserialization.</param>
        /// <returns>A blank <see cref="YamlPropertyInfo"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="propertyType"/> or <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="propertyType"/> cannot be used for serialization.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="YamlTypeInfo"/> instance has been locked for further modification.</exception>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public YamlPropertyInfo CreateYamlPropertyInfo(Type propertyType, string name)
        {
            ArgumentNullException.ThrowIfNull(propertyType);
            ArgumentNullException.ThrowIfNull(name);

            if (IsInvalidForSerialization(propertyType))
            {
                ThrowHelper.ThrowArgumentException_CannotSerializeInvalidType(nameof(propertyType), propertyType, Type, name);
            }

            VerifyMutable();
            YamlPropertyInfo propertyInfo = CreatePropertyUsingReflection(propertyType, declaringType: null);
            propertyInfo.Name = name;

            return propertyInfo;
        }

        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        internal YamlPropertyInfo CreatePropertyUsingReflection(Type propertyType, Type? declaringType)
        {
            YamlPropertyInfo yamlPropertyInfo;

            if (Options.TryGetTypeInfoCached(propertyType, out YamlTypeInfo? yamlTypeInfo))
            {
                // If a YamlTypeInfo has already been cached for the property type,
                // avoid reflection-based initialization by delegating construction
                // of YamlPropertyInfo<T> construction to the property type metadata.
                yamlPropertyInfo = yamlTypeInfo.CreateYamlPropertyInfo(declaringTypeInfo: this, declaringType, Options);
            }
            else
            {
                // Metadata for `propertyType` has not been registered yet.
                // Use reflection to instantiate the correct YamlPropertyInfo<T>
                Type propertyInfoType = typeof(YamlPropertyInfo<>).MakeGenericType(propertyType);
                yamlPropertyInfo = (YamlPropertyInfo)propertyInfoType.CreateInstanceNoWrapExceptions(
                    parameterTypes: [typeof(Type), typeof(YamlTypeInfo), typeof(YamlSerializerOptions)],
                    parameters: new object[] { declaringType ?? Type, this, Options })!;
            }

            Debug.Assert(yamlPropertyInfo.PropertyType == propertyType);
            return yamlPropertyInfo;
        }

        /// <summary>
        /// Creates a YamlPropertyInfo whose property type matches the type of this YamlTypeInfo instance.
        /// </summary>
        private protected abstract YamlPropertyInfo CreateYamlPropertyInfo(YamlTypeInfo declaringTypeInfo, Type? declaringType, YamlSerializerOptions options);

        private protected Dictionary<ParameterLookupKey, YamlParameterInfoValues>? _parameterInfoValuesIndex;

        // Untyped, root-level serialization methods
        internal abstract void SerializeAsObject(Utf8YamlWriter writer, object? rootValue);
        internal abstract Task<FlushResult> SerializeAsObjectAsync(PipeWriter pipeWriter, object? rootValue, int flushThreshold, CancellationToken cancellationToken, Utf8YamlWriter? yamlLineWriter = null);
        internal abstract Task SerializeAsObjectAsync(Stream utf8Yaml, object? rootValue, CancellationToken cancellationToken);
        internal abstract Task SerializeAsObjectAsync(PipeWriter utf8Yaml, object? rootValue, CancellationToken cancellationToken);
        internal abstract void SerializeAsObject(Stream utf8Yaml, object? rootValue);

        // Untyped, root-level deserialization methods
        internal abstract object? DeserializeAsObject(ref Utf8YamlReader reader, ref ReadStack state);
        internal abstract ValueTask<object?> DeserializeAsObjectAsync(PipeReader utf8Yaml, CancellationToken cancellationToken);
        internal abstract ValueTask<object?> DeserializeAsObjectAsync(Stream utf8Yaml, CancellationToken cancellationToken);
        internal abstract object? DeserializeAsObject(Stream utf8Yaml);

        internal ref struct PropertyHierarchyResolutionState(YamlSerializerOptions options)
        {
            public Dictionary<string, (YamlPropertyInfo, int index)> AddedProperties = new(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            public Dictionary<string, YamlPropertyInfo>? IgnoredProperties;
            public bool IsPropertyOrderSpecified;
        }

        private protected readonly struct ParameterLookupKey(Type type, string name) : IEquatable<ParameterLookupKey>
        {
            public Type Type { get; } = type;
            public string Name { get; } = name;
            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
            public bool Equals(ParameterLookupKey other) => Type == other.Type && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            public override bool Equals([NotNullWhen(true)] object? obj) => obj is ParameterLookupKey key && Equals(key);
        }

        internal void ConfigureProperties()
        {
            Debug.Assert(Kind == YamlTypeInfoKind.Object);
            Debug.Assert(_propertyCache is null);
            Debug.Assert(_propertyIndex is null);
            Debug.Assert(ExtensionDataProperty is null);

            YamlPropertyInfoList properties = PropertyList;
            StringComparer comparer = Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            Dictionary<string, YamlPropertyInfo> propertyIndex = new(properties.Count, comparer);
            List<YamlPropertyInfo> propertyCache = new(properties.Count);

            bool arePropertiesSorted = true;
            int previousPropertyOrder = int.MinValue;
            BitArray? requiredPropertiesMask = null;

            for (int i = 0; i < properties.Count; i++)
            {
                YamlPropertyInfo property = properties[i];
                Debug.Assert(property.DeclaringTypeInfo == this);

                if (property.IsExtensionData)
                {
                    if (UnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
                    {
                        ThrowHelper.ThrowInvalidOperationException_ExtensionDataConflictsWithUnmappedMemberHandling(Type, property);
                    }

                    if (ExtensionDataProperty is not null)
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type, typeof(YamlExtensionDataAttribute));
                    }

                    ExtensionDataProperty = property;
                }
                else
                {
                    property.PropertyIndex = i;

                    if (property.IsRequired)
                    {
                        (requiredPropertiesMask ??= new BitArray(properties.Count))[i] = true;
                    }

                    if (arePropertiesSorted)
                    {
                        arePropertiesSorted = previousPropertyOrder <= property.Order;
                        previousPropertyOrder = property.Order;
                    }

                    if (!propertyIndex.TryAdd(property.Name, property))
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, property.Name);
                    }

                    propertyCache.Add(property);
                }

                property.Configure();
            }

            if (!arePropertiesSorted)
            {
                // Properties have been configured by the user and require sorting.
                properties.SortProperties();
                propertyCache.StableSortByKey(static propInfo => propInfo.Order);
            }

            if (Options.MappingOrder == YamlMappingOrder.Sorted)
            {
                // M5f: the write order, by YamlPropertyOrder and then by the written name, ordinal (the names are
                // unique, so the order is total). Computed once here; the property list keeps its order.
                propertyCache.Sort(static (x, y) => x.Order != y.Order ? x.Order.CompareTo(y.Order) : string.CompareOrdinal(x.Name, y.Name));
            }

            OptionalPropertiesMask = requiredPropertiesMask?.Not();
            _propertyCache = propertyCache.ToArray();
            _propertyIndex = propertyIndex;

            // Override global UnmappedMemberHandling configuration
            // if type specifies an extension data property.
            EffectiveUnmappedMemberHandling = UnmappedMemberHandling ??
                (ExtensionDataProperty is null
                    ? Options.UnmappedMemberHandling
                    : YamlUnmappedMemberHandling.Skip);
        }

        internal void PopulateParameterInfoValues(YamlParameterInfoValues[] parameterInfoValues)
        {
            Dictionary<ParameterLookupKey, YamlParameterInfoValues> parameterIndex = new(parameterInfoValues.Length);
            foreach (YamlParameterInfoValues parameterInfoValue in parameterInfoValues)
            {
                ParameterLookupKey paramKey = new(parameterInfoValue.ParameterType, parameterInfoValue.Name);
                parameterIndex.TryAdd(paramKey, parameterInfoValue); // Ignore conflicts since they are reported at serialization time.
            }

            ParameterCount = parameterInfoValues.Length;
            _parameterInfoValuesIndex = parameterIndex;
        }

        internal void ResolveMatchingParameterInfo(YamlPropertyInfo propertyInfo)
        {
            Debug.Assert(
                CreateObjectWithArgs is null || _parameterInfoValuesIndex is not null,
                "Metadata with parameterized constructors must have populated parameter info metadata.");

            if (_parameterInfoValuesIndex is not { } index)
            {
                return;
            }

            string propertyName = propertyInfo.MemberName ?? propertyInfo.Name;
            ParameterLookupKey propKey = new(propertyInfo.PropertyType, propertyName);
            if (index.TryGetValue(propKey, out YamlParameterInfoValues? matchingParameterInfoValues))
            {
                propertyInfo.AddYamlParameterInfo(matchingParameterInfoValues);
            }
        }

        internal void ConfigureConstructorParameters()
        {
            Debug.Assert(Kind == YamlTypeInfoKind.Object);
            Debug.Assert(DetermineUsesParameterizedConstructor());
            Debug.Assert(_propertyCache is not null);
            Debug.Assert(_parameterCache is null);

            List<YamlParameterInfo> parameterCache = new(ParameterCount);
            Dictionary<ParameterLookupKey, YamlParameterInfo> parameterIndex = new(ParameterCount);

            foreach (YamlPropertyInfo propertyInfo in _propertyCache)
            {
                YamlParameterInfo? parameterInfo = propertyInfo.AssociatedParameter;
                if (parameterInfo is null)
                {
                    continue;
                }

                string propertyName = propertyInfo.MemberName ?? propertyInfo.Name;
                ParameterLookupKey paramKey = new(propertyInfo.PropertyType, propertyName);
                if (!parameterIndex.TryAdd(paramKey, parameterInfo))
                {
                    // Multiple object properties cannot bind to the same constructor parameter.
                    ThrowHelper.ThrowInvalidOperationException_MultiplePropertiesBindToConstructorParameters(
                        Type,
                        parameterInfo.Name,
                        propertyInfo.Name,
                        parameterIndex[paramKey].MatchingProperty.Name);
                }

                parameterCache.Add(parameterInfo);
            }

            if (ExtensionDataProperty is { AssociatedParameter: not null })
            {
                Debug.Assert(ExtensionDataProperty.MemberName is not null, "Custom property info cannot be data extension property");
                ThrowHelper.ThrowInvalidOperationException_ExtensionDataCannotBindToCtorParam(ExtensionDataProperty.MemberName, ExtensionDataProperty);
            }

            _parameterCache = parameterCache.ToArray();
            _parameterInfoValuesIndex = null;
        }

        internal static void ValidateType(Type type)
        {
            if (IsInvalidForSerialization(type))
            {
                ThrowHelper.ThrowInvalidOperationException_CannotSerializeInvalidType(type, declaringType: null, memberInfo: null);
            }
        }

        internal static bool IsInvalidForSerialization(Type type)
        {
            return type == typeof(void) || type.IsPointer || type.IsByRef || IsByRefLike(type) || type.ContainsGenericParameters;
        }

        internal void MapInterfaceTypesToCallbacks()
        {
            Debug.Assert(!IsReadOnly);

            if (Kind is YamlTypeInfoKind.Object or YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary)
            {
                if (typeof(IYamlOnSerializing).IsAssignableFrom(Type))
                {
                    OnSerializing = static obj => ((IYamlOnSerializing)obj).OnSerializing();
                }

                if (typeof(IYamlOnSerialized).IsAssignableFrom(Type))
                {
                    OnSerialized = static obj => ((IYamlOnSerialized)obj).OnSerialized();
                }

                if (typeof(IYamlOnDeserializing).IsAssignableFrom(Type))
                {
                    OnDeserializing = static obj => ((IYamlOnDeserializing)obj).OnDeserializing();
                }

                if (typeof(IYamlOnDeserialized).IsAssignableFrom(Type))
                {
                    OnDeserialized = static obj => ((IYamlOnDeserialized)obj).OnDeserialized();
                }
            }
        }

        internal void SetCreateObjectIfCompatible(Delegate? createObject)
        {
            Debug.Assert(!IsReadOnly);

            // Guard against the reflection resolver/source generator attempting to pass
            // a CreateObject delegate to converters/metadata that do not support it.
            if (Converter.SupportsCreateObjectDelegate && !Converter.ConstructorIsParameterized)
            {
                SetCreateObject(createObject);
            }
        }

        private static bool IsByRefLike(Type type)
        {
#if NET
            return type.IsByRefLike;
#else
            if (!type.IsValueType)
            {
                return false;
            }

            object[] attributes = type.GetCustomAttributes(inherit: false);

            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
                {
                    return true;
                }
            }

            return false;
#endif
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool SupportsPolymorphicDeserialization
        {
            get
            {
                Debug.Assert(IsConfigurationStarted);
                return PolymorphicTypeResolver?.UsesTypeDiscriminators == true;
            }
        }

        internal static bool IsValidExtensionDataProperty(Type propertyType)
        {
            return typeof(IDictionary<string, object>).IsAssignableFrom(propertyType) ||
                typeof(IDictionary<string, YamlElement>).IsAssignableFrom(propertyType) ||
                propertyType == typeof(IReadOnlyDictionary<string, object>) ||
                propertyType == typeof(IReadOnlyDictionary<string, YamlElement>) ||
                // Avoid a reference to typeof(YamlNode) to support trimming.
                (propertyType.FullName == YamlObjectTypeName && ReferenceEquals(propertyType.Assembly, typeof(YamlTypeInfo).Assembly));
        }

        private static YamlTypeInfoKind GetTypeInfoKind(Type type, YamlConverter converter)
        {
            if (type == typeof(object) && converter.CanBePolymorphic)
            {
                // System.Object is polymorphic and will not respect Properties
                Debug.Assert(converter is ObjectConverter);
                return YamlTypeInfoKind.None;
            }

            switch (converter.ConverterStrategy)
            {
                case ConverterStrategy.Value: return YamlTypeInfoKind.None;
                case ConverterStrategy.Object: return YamlTypeInfoKind.Object;
                case ConverterStrategy.Union:
                    // Nullable<TUnion> delegates to the underlying union converter, but the
                    // nullable wrapper does not own union case metadata.
                    return Nullable.GetUnderlyingType(type) is null ? YamlTypeInfoKind.Union : YamlTypeInfoKind.None;
                case ConverterStrategy.Enumerable: return YamlTypeInfoKind.Enumerable;
                case ConverterStrategy.Dictionary: return YamlTypeInfoKind.Dictionary;
                case ConverterStrategy.None:
                    Debug.Assert(converter is YamlConverterFactory);
                    ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(type);
                    return default;
                default:
                    Debug.Fail($"Unexpected class type: {converter.ConverterStrategy}");
                    throw new InvalidOperationException();
            }
        }

        internal sealed class YamlPropertyInfoList : ConfigurationList<YamlPropertyInfo>
        {
            private readonly YamlTypeInfo _yamlTypeInfo;

            public YamlPropertyInfoList(YamlTypeInfo yamlTypeInfo)
            {
                _yamlTypeInfo = yamlTypeInfo;
            }

            public override bool IsReadOnly => _yamlTypeInfo._properties == this && _yamlTypeInfo.IsReadOnly || _yamlTypeInfo.Kind != YamlTypeInfoKind.Object;
            protected override void OnCollectionModifying()
            {
                if (_yamlTypeInfo._properties == this)
                {
                    _yamlTypeInfo.VerifyMutable();
                }

                if (_yamlTypeInfo.Kind != YamlTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(_yamlTypeInfo.Kind);
                }
            }

            protected override void ValidateAddedValue(YamlPropertyInfo item)
            {
                item.EnsureChildOf(_yamlTypeInfo);
            }

            public void SortProperties()
            {
                _list.StableSortByKey(static propInfo => propInfo.Order);
            }

            /// <summary>
            /// Used by the built-in resolvers to add property metadata applying conflict resolution.
            /// </summary>
            public void AddPropertyWithConflictResolution(YamlPropertyInfo yamlPropertyInfo, ref PropertyHierarchyResolutionState state)
            {
                Debug.Assert(!_yamlTypeInfo.IsConfigured);
                Debug.Assert(yamlPropertyInfo.MemberName is not null, "MemberName can be null in custom YamlPropertyInfo instances and should never be passed in this method");

                // Algorithm should be kept in sync with the Roslyn equivalent in YamlSourceGenerator.Parser.cs
                string memberName = yamlPropertyInfo.MemberName;

                if (state.AddedProperties.TryAdd(yamlPropertyInfo.Name, (yamlPropertyInfo, Count)))
                {
                    Add(yamlPropertyInfo);
                    state.IsPropertyOrderSpecified |= yamlPropertyInfo.Order != 0;
                }
                else
                {
                    // The YamlPropertyNameAttribute or naming policy resulted in a collision.
                    (YamlPropertyInfo other, int index) = state.AddedProperties[yamlPropertyInfo.Name];

                    if (other.IsIgnored)
                    {
                        // Overwrite previously cached property since it has [YamlIgnore].
                        state.AddedProperties[yamlPropertyInfo.Name] = (yamlPropertyInfo, index);
                        this[index] = yamlPropertyInfo;
                        state.IsPropertyOrderSpecified |= yamlPropertyInfo.Order != 0;
                    }
                    else
                    {
                        bool ignoreCurrentProperty =
                            // Does the current property have `YamlIgnoreAttribute`?
                            yamlPropertyInfo.IsIgnored ||
                            // Is the current property hidden by the previously cached property
                            // (with `new` keyword, or by overriding)?
                            yamlPropertyInfo.IsOverriddenOrShadowedBy(other) ||
                            // Was a property with the same CLR name ignored? That property hid the current property,
                            // thus, if it was ignored, the current property should be ignored too.
                            (state.IgnoredProperties?.TryGetValue(memberName, out YamlPropertyInfo? ignored) == true && yamlPropertyInfo.IsOverriddenOrShadowedBy(ignored));

                        if (!ignoreCurrentProperty)
                        {
                            ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(_yamlTypeInfo.Type, yamlPropertyInfo.Name);
                        }
                    }
                }

                if (yamlPropertyInfo.IsIgnored)
                {
                    (state.IgnoredProperties ??= new())[memberName] = yamlPropertyInfo;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Type = {Type.Name}, Kind = {Kind}";
    }
}
