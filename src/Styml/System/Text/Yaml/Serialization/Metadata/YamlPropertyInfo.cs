// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace System.Text.Yaml.Serialization.Metadata
{
    /// <summary>
    /// Provides YAML serialization-related metadata about a property or field defined in an object.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class YamlPropertyInfo
    {
        internal static readonly YamlPropertyInfo s_missingProperty = GetPropertyPlaceholder();

        internal YamlTypeInfo? DeclaringTypeInfo { get; private set; }

        /// <summary>
        /// Converter after applying CustomConverter (i.e. YamlConverterAttribute)
        /// </summary>
        internal YamlConverter EffectiveConverter
        {
            get
            {
                Debug.Assert(_effectiveConverter is not null);
                return _effectiveConverter;
            }
        }

        private protected YamlConverter? _effectiveConverter;

        /// <summary>
        /// Gets or sets a custom converter override for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// It is possible to use <see cref="YamlConverterFactory"/> instances with this property.
        ///
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/>, the value of
        /// <see cref="CustomConverter"/> will be mapped from <see cref="YamlConverterAttribute" /> annotations.
        /// </remarks>
        public YamlConverter? CustomConverter
        {
            get;
            set
            {
                VerifyMutable();
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets a getter delegate for the property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Setting to <see langword="null"/> will result in the property being skipped on serialization.
        /// </remarks>
        public Func<object, object?>? Get
        {
            get => _untypedGet;
            set
            {
                VerifyMutable();
                SetGetter(value);
            }
        }

        /// <summary>
        /// Gets or sets a setter delegate for the property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Setting to <see langword="null"/> will result in the property being skipped on deserialization.
        /// </remarks>
        public Action<object, object?>? Set
        {
            get => _untypedSet;
            set
            {
                VerifyMutable();
                SetSetter(value);
                _isUserSpecifiedSetter = true;
            }
        }

        private protected Func<object, object?>? _untypedGet;
        private protected Action<object, object?>? _untypedSet;
        private bool _isUserSpecifiedSetter;

        private protected abstract void SetGetter(Delegate? getter);
        private protected abstract void SetSetter(Delegate? setter);

        /// <summary>
        /// Gets or sets a predicate deciding whether the current property value should be serialized.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// The first parameter denotes the parent object, the second parameter denotes the property value.
        ///
        /// Setting the predicate to <see langword="null"/> is equivalent to always serializing the property value.
        ///
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/>,
        /// the value of <see cref="YamlIgnoreAttribute.Condition"/> will map to this predicate.
        /// </remarks>
        public Func<object, object?, bool>? ShouldSerialize
        {
            get => _shouldSerialize;
            set
            {
                VerifyMutable();
                SetShouldSerialize(value);
                // Invalidate any YamlIgnore configuration if delegate set manually by user
                _isUserSpecifiedShouldSerialize = true;
                IgnoreDefaultValuesOnWrite = false;
            }
        }

        private protected Func<object, object?, bool>? _shouldSerialize;
        private bool _isUserSpecifiedShouldSerialize;
        private protected abstract void SetShouldSerialize(Delegate? predicate);

        internal YamlIgnoreCondition? IgnoreCondition
        {
            get => _ignoreCondition;
            set
            {
                Debug.Assert(!IsConfigured);
                ConfigureIgnoreCondition(value);
                _ignoreCondition = value;
            }
        }

        private YamlIgnoreCondition? _ignoreCondition;
        private protected abstract void ConfigureIgnoreCondition(YamlIgnoreCondition? ignoreCondition);

        /// <summary>
        /// Gets or sets a custom attribute provider for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// When resolving metadata via the built-in resolvers this
        /// will be populated with the underlying <see cref="MemberInfo" /> of the serialized property or field.
        ///
        /// Setting a custom attribute provider will have no impact on the contract model,
        /// but serves as metadata for downstream contract modifiers.
        /// </remarks>
        public ICustomAttributeProvider? AttributeProvider
        {
            get
            {
                Func<ICustomAttributeProvider>? attributeProviderFactory = Volatile.Read(ref AttributeProviderFactory);
                ICustomAttributeProvider? attributeProvider = _attributeProvider;

                if (attributeProvider is null && attributeProviderFactory is not null)
                {
                    _attributeProvider = attributeProvider = attributeProviderFactory();
                    Volatile.Write(ref AttributeProviderFactory, null);
                }

                return attributeProvider;
            }
            set
            {
                VerifyMutable();

                _attributeProvider = value;
                Volatile.Write(ref AttributeProviderFactory, null);
            }
        }

        // Metadata emanating from the source generator use delayed attribute provider initialization
        // ensuring that reflection metadata resolution remains pay-for-play and is trimmable.
        internal Func<ICustomAttributeProvider>? AttributeProviderFactory;
        private ICustomAttributeProvider? _attributeProvider;

        /// <summary>
        /// Gets or sets a value indicating if the property or field should be replaced or populated during deserialization.
        /// </summary>
        /// <remarks>
        /// Initial value for this property is based on the presence of <see cref="YamlObjectCreationHandlingAttribute"/> attribute on the property.
        /// When <see langword="null"/> effective handling will be resolved based on
        /// capability of property converter to populate, containing type's <see cref="YamlTypeInfo.PreferredPropertyObjectCreationHandling"/>.
        /// and <see cref="YamlSerializerOptions.PreferredObjectCreationHandling"/> value.
        /// </remarks>
        public YamlObjectCreationHandling? ObjectCreationHandling
        {
            get;
            set
            {
                VerifyMutable();

                if (value is not null)
                {
                    if (!YamlSerializer.IsValidCreationHandlingValue(value.Value))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                }

                field = value;
            }
        }

        internal YamlObjectCreationHandling EffectiveObjectCreationHandling { get; private set; }

        internal string? MemberName { get; set; } // Do not rename (legacy schema generation)
        internal MemberTypes MemberType { get; set; }
        internal bool IsVirtual { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the return type of the getter is annotated as nullable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not a reference type or <see cref="Nullable{T}"/>.
        /// </exception>
        /// <remarks>
        /// Contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// derive the value of this property from nullable reference type annotations, including annotations
        /// from attributes such as <see cref="NotNullAttribute"/> or <see cref="MaybeNullAttribute"/>.
        ///
        /// This property has no effect on serialization unless the <see cref="YamlSerializerOptions.RespectNullableAnnotations"/>
        /// property has been enabled, in which case the serializer will reject any <see langword="null"/> values returned by the getter.
        /// </remarks>
        public bool IsGetNullable
        {
            get => _isGetNullable;
            set
            {
                VerifyMutable();

                if (value && !PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowInvalidOperationException_PropertyTypeNotNullable(this);
                }

                _isGetNullable = value;
            }
        }

        private bool _isGetNullable;

        /// <summary>
        /// Gets or sets a value indicating whether the input type of the setter is annotated as nullable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not a reference type or <see cref="Nullable{T}"/>.
        /// </exception>
        /// <remarks>
        /// Contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// derive the value of this property from nullable reference type annotations, including annotations
        /// from attributes such as <see cref="AllowNullAttribute"/> or <see cref="DisallowNullAttribute"/>.
        ///
        /// This property has no effect on deserialization unless the <see cref="YamlSerializerOptions.RespectNullableAnnotations"/>
        /// property has been enabled, in which case the serializer will reject any <see langword="null"/> deserialization results.
        ///
        /// If the property has been associated with a deserialization constructor parameter,
        /// this setting reflected the nullability annotation of the parameter and not the property setter.
        /// </remarks>
        public bool IsSetNullable
        {
            get => _isSetNullable;
            set
            {
                VerifyMutable();

                if (value && !PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowInvalidOperationException_PropertyTypeNotNullable(this);
                }

                _isSetNullable = value;
            }
        }

        private protected bool _isSetNullable;

        /// <summary>
        /// Specifies whether the current property is a special extension data property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not valid for use with extension data.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlExtensionDataAttribute"/> annotations.
        /// </remarks>
        public bool IsExtensionData
        {
            get;
            set
            {
                VerifyMutable();

                if (value && !YamlTypeInfo.IsValidExtensionDataProperty(PropertyType))
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(this);
                }

                field = value;
            }
        }

        /// <summary>
        /// Specifies whether the current property is required for deserialization to be successful.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlRequiredAttribute"/> annotations.
        ///
        /// For contracts using <see cref="DefaultYamlTypeInfoResolver"/>, properties using the <see langword="required"/> keyword
        /// will also map to this setting, unless deserialization uses a SetsRequiredMembersAttribute on a constructor that populates all required properties.
        /// <see langword="required"/> keyword is currently not supported in <see cref="YamlSerializerContext"/> contracts.
        /// </remarks>
        public bool IsRequired
        {
            get => _isRequired;
            set
            {
                VerifyMutable();
                _isRequired = value;
            }
        }

        private protected bool _isRequired;

        /// <summary>
        /// Gets the constructor parameter associated with the current property.
        /// </summary>
        /// <remarks>
        /// Returns the <see cref="YamlParameterInfo"/> metadata for the parameter in the
        /// deserialization constructor that has been associated with the current property.
        ///
        /// A constructor parameter is matched to a property or field if they are of the
        /// same type and have the same name, up to case insensitivity. Each constructor
        /// parameter must be matched to exactly one property of field.
        /// </remarks>
        public YamlParameterInfo? AssociatedParameter { get; internal set; }

        internal YamlPropertyInfo(Type declaringType, Type propertyType, YamlTypeInfo? declaringTypeInfo, YamlSerializerOptions options)
        {
            Debug.Assert(declaringTypeInfo is null || declaringType.IsAssignableFrom(declaringTypeInfo.Type));

            DeclaringType = declaringType;
            PropertyType = propertyType;
            DeclaringTypeInfo = declaringTypeInfo; // null declaringTypeInfo means it's not tied yet
            Options = options;

            _isGetNullable = _isSetNullable = PropertyTypeCanBeNull;
        }

        internal static YamlPropertyInfo GetPropertyPlaceholder()
        {
            YamlPropertyInfo info = new YamlPropertyInfo<object>(typeof(object), declaringTypeInfo: null, options: null!);

            Debug.Assert(!info.IsForTypeInfo);
            Debug.Assert(!info.CanSerialize);
            Debug.Assert(!info.CanDeserialize);

            info.Name = string.Empty;

            return info;
        }

        /// <summary>
        /// Gets the declaring type of the property.
        /// </summary>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets the type of the current property.
        /// </summary>
        public Type PropertyType { get; }

        private protected void VerifyMutable()
        {
            DeclaringTypeInfo?.VerifyMutable();
        }

        internal bool IsConfigured { get; private set; }

        internal void Configure()
        {
            Debug.Assert(DeclaringTypeInfo is not null);
            Debug.Assert(!IsConfigured);

            if (IsIgnored)
            {
                // Avoid configuring YamlIgnore.Always properties
                // to avoid failing on potentially unsupported types.
                CanSerialize = false;
                CanDeserialize = false;
            }
            else
            {
                _yamlTypeInfo ??= Options.GetTypeInfoInternal(PropertyType);
                _yamlTypeInfo.EnsureConfigured();

                DetermineEffectiveConverter(_yamlTypeInfo);
                DetermineNumberHandlingForProperty();
                DetermineEffectiveObjectCreationHandlingForProperty();
                DetermineSerializationCapabilities();
                DetermineIgnoreCondition();

                if (_emptyCollectionCondition && !PropertyTypeCanBeNull && !HasCollectionConverter)
                {
                    // As WhenWritingNull on a value type: the condition can never apply.
                    ThrowHelper.ThrowInvalidOperationException_EmptyCollectionConditionOnValueTypeInvalid(MemberName!, DeclaringType);
                }
            }

            ValidateCommentAndStyle();

            if (IsForTypeInfo)
            {
                DetermineNumberHandlingForTypeInfo();
            }
            else
            {
                ValidateAndCachePropertyName();
            }

            if (IsRequired)
            {
                if (!CanDeserialize &&
                    !(AssociatedParameter?.IsRequiredParameter is true &&
                      Options.RespectRequiredConstructorParameters))
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlPropertyRequiredAndNotDeserializable(this);
                }

                if (IsExtensionData)
                {
                    ThrowHelper.ThrowInvalidOperationException_YamlPropertyRequiredAndExtensionData(this);
                }

                Debug.Assert(!IgnoreNullTokensOnRead);
            }

            IsConfigured = true;
        }

        private protected abstract void DetermineEffectiveConverter(YamlTypeInfo yamlTypeInfo);

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal abstract void DetermineReflectionPropertyAccessors(MemberInfo memberInfo, bool useNonPublicAccessors);

        private void ValidateAndCachePropertyName()
        {
            Debug.Assert(Name is not null);

            if (Options.ReferenceHandlingStrategy is YamlKnownReferenceHandler.Preserve &&
                this is { DeclaringType.IsValueType: false, IsIgnored: false, IsExtensionData: false } &&
                Name is YamlSerializer.IdPropertyName or YamlSerializer.RefPropertyName)
            {
                // Validate potential conflicts with reference preservation metadata property names.
                // Conflicts with polymorphic type discriminators are contextual and need to be
                // handled separately by the PolymorphicTypeResolver type.

                ThrowHelper.ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(DeclaringType, Name);
            }

            NameAsUtf8Bytes = Encoding.UTF8.GetBytes(Name);
            EscapedNameSection = YamlHelpers.GetEscapedPropertyNameSection(NameAsUtf8Bytes, Options.Encoder);
            BlockNameSection = YamlHelpers.GetBlockPropertyNameSection(NameAsUtf8Bytes, Options.Encoder, Options.QuoteForLenientReaders);
        }

        private void DetermineIgnoreCondition()
        {
            if (_ignoreCondition is not null)
            {
                // Do not apply global policy if already configured on the property level.
                return;
            }

            if (Options.DefaultIgnoreCondition == YamlIgnoreCondition.WhenWritingNull)
            {
                if (PropertyTypeCanBeNull)
                {
                    IgnoreDefaultValuesOnWrite = ShouldSerialize is null;
                }
            }
            else if (Options.DefaultIgnoreCondition == YamlIgnoreCondition.WhenWritingDefault)
            {
                IgnoreDefaultValuesOnWrite = ShouldSerialize is null;
            }
            else if (Options.DefaultIgnoreCondition == YamlIgnoreCondition.WhenWritingEmptyCollection)
            {
                // M5f: nulls and empty collections; a value type that is not a collection is always written, as
                // under WhenWritingNull.
                if (ShouldSerialize is null && (PropertyTypeCanBeNull || HasCollectionConverter))
                {
                    IgnoreEmptyCollectionsOnWrite = true;
                }
            }
        }

        // M5f (review 4; B5): checked once, so that an error comes before any output.
        private void ValidateCommentAndStyle()
        {
            if (Comment is string comment && !YamlWriterHelper.IsValidBlockComment(Encoding.UTF8.GetBytes(comment)))
            {
                ThrowHelper.ThrowInvalidOperationException_YamlCommentInvalid(MemberName ?? Name, DeclaringType);
            }

            if (ScalarStyleFromAttribute && ScalarStyle == YamlScalarStyle.None)
            {
                ThrowHelper.ThrowInvalidOperationException_YamlScalarStyleAttributeNone(MemberName ?? Name, DeclaringType);
            }
        }

        private void DetermineSerializationCapabilities()
        {
            Debug.Assert(EffectiveConverter is not null, "Must have calculated the effective converter.");
            CanSerialize = HasGetter;
            CanDeserialize = HasSetter;

            Debug.Assert(MemberType is 0 or MemberTypes.Field or MemberTypes.Property);
            if (MemberType == 0 || _ignoreCondition is not null)
            {
                // No policy to be applied if either:
                // 1. YamlPropertyInfo is a custom instance (not generated via reflection or sourcegen).
                // 2. A YamlIgnoreCondition has been specified on the property level.
                CanDeserializeOrPopulate = CanDeserialize || EffectiveObjectCreationHandling == YamlObjectCreationHandling.Populate;
                return;
            }

            if ((EffectiveConverter.ConverterStrategy & (ConverterStrategy.Enumerable | ConverterStrategy.Dictionary)) != 0)
            {
                // Properties of collections types that only have setters are not supported.
                if (Get is null && Set is not null && !_isUserSpecifiedSetter)
                {
                    CanDeserialize = false;
                }
            }
            else
            {
                // For read-only properties of non-collection types, apply IgnoreReadOnlyProperties/Fields policy,
                // unless a `ShouldSerialize` predicate has been explicitly applied by the user (null or non-null).
                if (Get is not null && Set is null && IgnoreReadOnlyMember && !_isUserSpecifiedShouldSerialize)
                {
                    CanSerialize = false;
                }
            }

            CanDeserializeOrPopulate = CanDeserialize || EffectiveObjectCreationHandling == YamlObjectCreationHandling.Populate;
        }

        private void DetermineNumberHandlingForTypeInfo()
        {
            Debug.Assert(DeclaringTypeInfo is not null, "We should have ensured parent is assigned in YamlTypeInfo");
            Debug.Assert(!DeclaringTypeInfo.IsConfigured);

            YamlNumberHandling? declaringTypeNumberHandling = DeclaringTypeInfo.NumberHandling;

            if (declaringTypeNumberHandling is not null && declaringTypeNumberHandling != YamlNumberHandling.Strict && !EffectiveConverter.IsInternalConverter)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }

            if (NumberHandingIsApplicable())
            {
                // This logic is to honor YamlNumberHandlingAttribute placed on
                // custom collections e.g. public class MyNumberList : List<int>.

                // Priority 1: Get handling from the type (parent type in this case is the type itself).
                EffectiveNumberHandling = declaringTypeNumberHandling;

                // Priority 2: Get handling from YamlSerializerOptions instance.
                if (!EffectiveNumberHandling.HasValue && Options.NumberHandling != YamlNumberHandling.Strict)
                {
                    EffectiveNumberHandling = Options.NumberHandling;
                }
            }
        }

        private void DetermineNumberHandlingForProperty()
        {
            Debug.Assert(DeclaringTypeInfo is not null, "We should have ensured parent is assigned in YamlTypeInfo");
            Debug.Assert(!IsConfigured, "Should not be called post-configuration.");
            Debug.Assert(_yamlTypeInfo is not null, "Must have already been determined on configuration.");

            bool numberHandlingIsApplicable = NumberHandingIsApplicable();

            if (numberHandlingIsApplicable)
            {
                // Priority 1: Get handling from attribute on property/field, its parent class type or property type.
                YamlNumberHandling? handling = NumberHandling ?? DeclaringTypeInfo.NumberHandling ?? _yamlTypeInfo.NumberHandling;

                // Priority 2: Get handling from YamlSerializerOptions instance.
                if (!handling.HasValue && Options.NumberHandling != YamlNumberHandling.Strict)
                {
                    handling = Options.NumberHandling;
                }

                EffectiveNumberHandling = handling;
            }
            else if (NumberHandling.HasValue && NumberHandling != YamlNumberHandling.Strict)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }
        }

        private void DetermineEffectiveObjectCreationHandlingForProperty()
        {
            Debug.Assert(EffectiveConverter is not null, "Must have calculated the effective converter.");
            Debug.Assert(DeclaringTypeInfo is not null, "We should have ensured parent is assigned in YamlTypeInfo");
            Debug.Assert(!IsConfigured, "Should not be called post-configuration.");

            YamlObjectCreationHandling effectiveObjectCreationHandling = YamlObjectCreationHandling.Replace;
            if (ObjectCreationHandling is null)
            {
                // Consult type-level configuration, then global configuration.
                // Ignore global configuration if we're using a parameterized constructor.
                YamlObjectCreationHandling preferredCreationHandling =
                    DeclaringTypeInfo.PreferredPropertyObjectCreationHandling
                    ?? (DeclaringTypeInfo.DetermineUsesParameterizedConstructor()
                        ? YamlObjectCreationHandling.Replace
                        : Options.PreferredObjectCreationHandling);

                bool canPopulate =
                    preferredCreationHandling == YamlObjectCreationHandling.Populate &&
                    EffectiveConverter.CanPopulate &&
                    Get is not null &&
                    (!PropertyType.IsValueType || Set is not null) &&
                    !DeclaringTypeInfo.SupportsPolymorphicDeserialization &&
                    !(Set is null && IgnoreReadOnlyMember);

                effectiveObjectCreationHandling = canPopulate ? YamlObjectCreationHandling.Populate : YamlObjectCreationHandling.Replace;
            }
            else if (ObjectCreationHandling == YamlObjectCreationHandling.Populate)
            {
                if (!EffectiveConverter.CanPopulate)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPopulateNotSupportedByConverter(this);
                }

                if (Get is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyMustHaveAGetter(this);
                }

                if (PropertyType.IsValueType && Set is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyValueTypeMustHaveASetter(this);
                }

                Debug.Assert(_yamlTypeInfo is not null);
                Debug.Assert(_yamlTypeInfo.IsConfigurationStarted);
                if (YamlTypeInfo.SupportsPolymorphicDeserialization)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization(this);
                }

                if (Set is null && IgnoreReadOnlyMember)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReadOnlyMember(this);
                }

                effectiveObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }

            if (effectiveObjectCreationHandling is YamlObjectCreationHandling.Populate)
            {
                if (DeclaringTypeInfo.DetermineUsesParameterizedConstructor())
                {
                    ThrowHelper.ThrowNotSupportedException_ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors();
                }

                if (Options.ReferenceHandlingStrategy != YamlKnownReferenceHandler.Unspecified)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReferenceHandling();
                }
            }

            // Validation complete, commit configuration.
            EffectiveObjectCreationHandling = effectiveObjectCreationHandling;
        }

        private bool NumberHandingIsApplicable()
        {
            if (EffectiveConverter.IsInternalConverterForNumberType)
            {
                return true;
            }

            Type potentialNumberType;
            if (!EffectiveConverter.IsInternalConverter ||
                ((ConverterStrategy.Enumerable | ConverterStrategy.Dictionary) & EffectiveConverter.ConverterStrategy) == 0)
            {
                potentialNumberType = PropertyType;
            }
            else
            {
                Debug.Assert(EffectiveConverter.ElementType != null);
                potentialNumberType = EffectiveConverter.ElementType;
            }

            potentialNumberType = Nullable.GetUnderlyingType(potentialNumberType) ?? potentialNumberType;

            return potentialNumberType == typeof(byte) ||
                potentialNumberType == typeof(decimal) ||
                potentialNumberType == typeof(double) ||
                potentialNumberType == typeof(short) ||
                potentialNumberType == typeof(int) ||
                potentialNumberType == typeof(long) ||
                potentialNumberType == typeof(sbyte) ||
                potentialNumberType == typeof(float) ||
                potentialNumberType == typeof(ushort) ||
                potentialNumberType == typeof(uint) ||
                potentialNumberType == typeof(ulong) ||
#if NET
                potentialNumberType == typeof(Half) ||
#endif
#if NET
                potentialNumberType == typeof(Int128) ||
                potentialNumberType == typeof(UInt128) ||
#endif
#if NET11_0_OR_GREATER
                potentialNumberType == typeof(System.Numerics.BFloat16) ||
                potentialNumberType == typeof(System.Numerics.Decimal32) ||
                potentialNumberType == typeof(System.Numerics.Decimal64) ||
                potentialNumberType == typeof(System.Numerics.Decimal128) ||
#endif
                potentialNumberType == YamlTypeInfo.ObjectType;
        }

        /// <summary>
        /// Creates a <see cref="YamlPropertyInfo"/> instance whose type matches that of the current property.
        /// </summary>
        internal abstract void AddYamlParameterInfo(YamlParameterInfoValues parameterInfoValues);

        internal abstract bool GetMemberAndWriteYaml(object obj, ref WriteStack state, Utf8YamlWriter writer);
        internal abstract bool GetMemberAndWriteYamlExtensionData(object obj, ref WriteStack state, Utf8YamlWriter writer);

        internal abstract object? GetValueAsObject(object obj);

        internal bool HasGetter => _untypedGet is not null;
        internal bool HasSetter => _untypedSet is not null;
        internal bool IgnoreNullTokensOnRead { get; private protected set; }
        internal bool IgnoreDefaultValuesOnWrite { get; private protected set; }

        internal bool IgnoreReadOnlyMember
        {
            get
            {
                Debug.Assert(MemberType == MemberTypes.Property || MemberType == MemberTypes.Field || MemberType == default);
                return MemberType switch
                {
                    MemberTypes.Property => Options.IgnoreReadOnlyProperties,
                    MemberTypes.Field => Options.IgnoreReadOnlyFields,
                    _ => false,
                };
            }
        }

        /// <summary>
        /// True if the corresponding cref="YamlTypeInfo.PropertyInfoForTypeInfo"/> is this instance.
        /// </summary>
        internal bool IsForTypeInfo { get; init; }

        // There are 3 copies of the property name:
        // 1) Name. The unescaped property name.
        // 2) NameAsUtf8Bytes. The Utf8 version of Name. Used during deserialization for property lookup.
        // 3) EscapedNameSection. The escaped version of NameAsUtf8Bytes plus the wrapping quotes and a trailing colon. Used during serialization.

        /// <summary>
        /// Gets or sets the YAML property name used when serializing the property.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// The value of <see cref="Name"/> cannot conflict with that of other <see cref="YamlPropertyInfo"/> defined in the declaring <see cref="YamlTypeInfo"/>.
        ///
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value typically reflects the underlying .NET member name, the name derived from <see cref="YamlSerializerOptions.PropertyNamingPolicy" />,
        /// or the value specified in <see cref="YamlPropertyNameAttribute" />.
        /// </remarks>
        public string Name
        {
            get
            {
                Debug.Assert(_name is not null);
                return _name;
            }
            set
            {
                VerifyMutable();

                ArgumentNullException.ThrowIfNull(value);

                _name = value;
            }
        }

        private string? _name;

        /// <summary>
        /// Utf8 version of Name.
        /// </summary>
        internal byte[] NameAsUtf8Bytes { get; private set; } = null!;

        /// <summary>
        /// The escaped name passed to the writer.
        /// </summary>
        internal byte[] EscapedNameSection { get; private set; } = null!;

        /// <summary>
        /// The block form of the name section (<c>name:</c>, or a double-quoted name with a colon), or null when the
        /// name needs an explicit key.
        /// </summary>
        internal byte[]? BlockNameSection { get; private set; }

        /// <summary>
        /// Gets the <see cref="YamlSerializerOptions"/> value associated with the current contract instance.
        /// </summary>
        public YamlSerializerOptions Options { get; }

        /// <summary>
        /// Gets or sets the comment written above the property in block style:
        /// one <c># line</c> per line, at the key's indentation. Written for a null value too, and not when an ignore
        /// condition leaves the property out; dropped in JSON-compatible style.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlCommentAttribute"/> annotations. The text holds
        /// printable characters and tabs, its lines separated by CR, LF or CRLF; anything else is an
        /// <see cref="InvalidOperationException"/> when the contract is configured.
        /// </remarks>
        public string? Comment
        {
            get;
            set
            {
                VerifyMutable();
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the scalar style of the property's string value in block style;
        /// <see cref="YamlScalarStyle.None"/> (the default) lets the writer choose.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="YamlScalarStyle"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlScalarStyleAttribute"/> annotations. The style
        /// applies where it holds the value and reads back as the same string; otherwise the writer's default choice is
        /// written. It does not reach numbers, booleans, nulls, a collection's items, nested objects or keys.
        /// </remarks>
        public YamlScalarStyle ScalarStyle
        {
            get;
            set
            {
                VerifyMutable();
                if ((uint)value > (uint)YamlScalarStyle.Folded)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(value));
                }

                field = value;
            }
        }

        // M5f: the style came from [YamlScalarStyle], where None is refused (an attribute that says nothing).
        internal bool ScalarStyleFromAttribute { get; set; }

        // M5f: DefaultIgnoreCondition is WhenWritingEmptyCollection and applies to this property.
        internal bool IgnoreEmptyCollectionsOnWrite { get; private protected set; }

        // M5f: the property's IgnoreCondition is WhenWritingEmptyCollection, checked against the effective
        // converter when the property is configured.
        private protected bool _emptyCollectionCondition;

        /// <summary>Whether the value is null or a collection whose count, known without enumerating, is 0.</summary>
        internal abstract bool IsNullOrEmptyCollection(object? value);

        /// <summary>Whether the effective converter is a collection or dictionary converter, so that its emptiness can be tested.</summary>
        internal bool HasCollectionConverter =>
            EffectiveConverter.IsUntypedConverter ||
            (EffectiveConverter.ConverterStrategy & (ConverterStrategy.Enumerable | ConverterStrategy.Dictionary)) != 0;

        /// <summary>
        /// Gets or sets the serialization order for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlPropertyOrderAttribute"/> annotations.
        /// </remarks>
        public int Order
        {
            get;
            set
            {
                VerifyMutable();
                field = value;
            }
        }

        internal bool ReadYamlAndAddExtensionProperty(
            object obj,
            scoped ref ReadStack state,
            ref Utf8YamlReader reader)
        {
            object propValue = GetValueAsObject(obj)!;
            HashSet<string>? documentKeys = ExtensionDocumentKeys(propValue, ref state, Options);

            if (Options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins && state.Current.YamlPropertyNameAsString is string firstWinsKey
                && (documentKeys is not null ? documentKeys.Contains(firstWinsKey) : ExtensionContainsKey(propValue, firstWinsKey)))
            {
                // M5c, FirstWins: a later occurrence is read for syntax and skipped.
                reader.TrySkip();
                return true;
            }

            if (propValue is IDictionary<string, object?> dictionaryObjectValue)
            {
                if (reader.TokenType == YamlTokenType.Null)
                {
                    // A null JSON value is treated as a null object reference.
                    AddProperty(in state.Current, dictionaryObjectValue, null);
                }
                else
                {
                    YamlConverter<object> converter = GetDictionaryValueConverter<object>();
                    object value;
                    if (reader.YamlHasNodeRef)
                    {
                        // M5b: an anchor or an alias goes through TryRead.
                        converter.TryRead(ref reader, YamlTypeInfo.ObjectType, Options, ref state, out object? read, out _);
                        value = read!;
                    }
                    else
                    {
                        value = converter.Read(ref reader, YamlTypeInfo.ObjectType, Options)!;
                    }

                    AddProperty(in state.Current, dictionaryObjectValue, value);
                }
            }
            else if (propValue is IDictionary<string, YamlElement> dictionaryElementValue)
            {
                YamlConverter<YamlElement> converter = GetDictionaryValueConverter<YamlElement>();
                YamlElement value;
                if (reader.YamlHasNodeRef)
                {
                    converter.TryRead(ref reader, typeof(YamlElement), Options, ref state, out value, out _);
                }
                else
                {
                    value = converter.Read(ref reader, typeof(YamlElement), Options);
                }
                AddProperty(in state.Current, dictionaryElementValue, value);
            }
            else
            {
                // Avoid a type reference to YamlObject and its converter to support trimming.
                Debug.Assert(propValue is Nodes.YamlObject);
                if (state.PendingComplexKey is { } complexKey)
                {
                    // M5e: a collection key, held as one by the YamlObject.
                    state.PendingComplexKey = null;
                    Converters.YamlNodeConverter.Instance.TryRead(ref reader, typeof(Nodes.YamlNode), Options, ref state, out Nodes.YamlNode? node, out _);
                    ((Nodes.YamlObject)propValue).AddComplexKeyEntry(complexKey, node);
                    return true;
                }

                EffectiveConverter.ReadElementAndSetProperty(propValue, state.Current.YamlPropertyNameAsString!, ref reader, Options, ref state);
            }

            return true;

            YamlConverter<TValue> GetDictionaryValueConverter<TValue>()
            {
                YamlTypeInfo dictionaryValueInfo =
                    YamlTypeInfo.ElementTypeInfo
                    // Slower path for non-generic types that implement IDictionary<,>.
                    // It is possible to cache this converter on YamlTypeInfo if we assume the property value
                    // will always be the same type for all instances.
                    ?? Options.GetTypeInfoInternal(typeof(TValue));

                Debug.Assert(dictionaryValueInfo is YamlTypeInfo<TValue>);
                return ((YamlTypeInfo<TValue>)dictionaryValueInfo).EffectiveConverter;
            }

            void AddProperty<TValue>(ref readonly ReadStackFrame current, IDictionary<string, TValue> d, TValue value)
            {
                string property = current.YamlPropertyNameAsString!;
                if (documentKeys is not null)
                {
                    // Seeded extension data: the document's keys are judged, and a key that appears once overrides the seed.
                    if (!documentKeys.Add(property) && Options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                    {
                        ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(current.YamlPropertyInfo!);
                    }

                    d[property] = value;
                }
                else if (Options.AllowDuplicateProperties)
                {
                    d[property] = value;
                }
                else
                {
                    if (!d.TryAdd(property, value))
                    {
                        ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(current.YamlPropertyInfo!);
                    }
                }
            }
        }

        /// <summary>
        /// M5c: the document's keys of extension data that held entries when this object's read first met it,
        /// under Error and FirstWins; null otherwise (its entries are then the document's).
        /// </summary>
        internal static HashSet<string>? ExtensionDocumentKeys(object extension, scoped ref ReadStack state, YamlSerializerOptions options)
        {
            if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins)
            {
                return null;
            }

            (int count, IEqualityComparer<string>? comparer) = extension switch
            {
                Dictionary<string, object?> d => (d.Count, d.Comparer),
                Dictionary<string, YamlElement> d => (d.Count, d.Comparer),
                ICollection<KeyValuePair<string, object?>> c => (c.Count, null),
                ICollection<KeyValuePair<string, YamlElement>> c => (c.Count, null),
                Nodes.YamlObject o => (o.Count, o.Options?.PropertyNameCaseInsensitive == true ? StringComparer.OrdinalIgnoreCase : null),
                _ => (0, (IEqualityComparer<string>?)null),
            };

            return state.SeededDocumentKeys(extension, count, reset: false, comparer, record: true);
        }

        internal static bool ExtensionContainsKey(object extension, string key) => extension switch
        {
            IDictionary<string, object?> d => d.ContainsKey(key),
            IDictionary<string, YamlElement> d => d.ContainsKey(key),
            Nodes.YamlObject o => o.ContainsKey(key),
            _ => false,
        };

        internal abstract bool ReadYamlAndSetMember(object obj, scoped ref ReadStack state, ref Utf8YamlReader reader);

        internal abstract bool ReadYamlAsObject(scoped ref ReadStack state, ref Utf8YamlReader reader, out object? value);

        internal bool ReadYamlExtensionDataValue(scoped ref ReadStack state, ref Utf8YamlReader reader, out object? value)
        {
            Debug.Assert(this == state.Current.YamlTypeInfo.ExtensionDataProperty);

            if (YamlTypeInfo.ElementType == YamlTypeInfo.ObjectType && reader.TokenType == YamlTokenType.Null)
            {
                value = null;
                return true;
            }

            YamlConverter<YamlElement> converter = (YamlConverter<YamlElement>)Options.GetConverterInternal(typeof(YamlElement));
            if (!converter.TryRead(ref reader, typeof(YamlElement), Options, ref state, out YamlElement yamlElement, out _))
            {
                // YamlElement is a struct that must be read in full.
                value = null;
                return false;
            }

            value = yamlElement;
            return true;
        }

        internal void EnsureChildOf(YamlTypeInfo parent)
        {
            if (DeclaringTypeInfo is null)
            {
                DeclaringTypeInfo = parent;
            }
            else if (DeclaringTypeInfo != parent)
            {
                ThrowHelper.ThrowInvalidOperationException_YamlPropertyInfoIsBoundToDifferentYamlTypeInfo(this);
            }

            DeclaringTypeInfo.ResolveMatchingParameterInfo(this);
        }

        /// <summary>
        /// Tries to get pre-populated value from the property if populating is enabled.
        /// If property value is <see langword="null"/> this method will return false.
        /// </summary>
        internal bool TryGetPrePopulatedValue(scoped ref ReadStack state)
        {
            if (EffectiveObjectCreationHandling != YamlObjectCreationHandling.Populate)
                return false;

            Debug.Assert(EffectiveConverter.CanPopulate, "Property is marked with Populate but converter cannot populate. This should have been validated in Configure");
            Debug.Assert(state.Parent.ReturnValue is not null, "Parent object is null");
            Debug.Assert(!state.Current.IsPopulating, "We've called TryGetPrePopulatedValue more than once");
            object? value = Get!(state.Parent.ReturnValue);
            state.Current.ReturnValue = value;
            state.Current.IsPopulating = value is not null;
            return value is not null;
        }

        internal YamlTypeInfo YamlTypeInfo
        {
            get
            {
                Debug.Assert(_yamlTypeInfo?.IsConfigurationStarted == true);
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider YamlTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                YamlTypeInfo yamlTypeInfo = _yamlTypeInfo;
                yamlTypeInfo.EnsureConfigured();
                return yamlTypeInfo;
            }
            set
            {
                _yamlTypeInfo = value;
            }
        }

        private YamlTypeInfo? _yamlTypeInfo;

        /// <summary>
        /// Returns true if <see cref="YamlTypeInfo"/> has been configured.
        /// This might be false even if <see cref="IsConfigured"/> is true
        /// in cases of recursive types or <see cref="IsIgnored"/> is true.
        /// </summary>
        internal bool IsPropertyTypeInfoConfigured => _yamlTypeInfo?.IsConfigured == true;

        /// <summary>
        /// Property was marked YamlIgnoreCondition.Always and also hasn't been configured by the user.
        /// </summary>
        internal bool IsIgnored => _ignoreCondition is YamlIgnoreCondition.Always && Get is null && Set is null;

        /// <summary>
        /// Reflects the value of <see cref="HasGetter"/> combined with any additional global ignore policies.
        /// </summary>
        internal bool CanSerialize { get; private set; }
        /// <summary>
        /// Reflects the value of <see cref="HasSetter"/> combined with any additional global ignore policies.
        /// </summary>
        internal bool CanDeserialize { get; private set; }

        /// <summary>
        /// Reflects the value can be deserialized or populated
        /// </summary>
        internal bool CanDeserializeOrPopulate { get; private set; }

        /// <summary>
        /// Relevant to source generated metadata: did the property have the <see cref="YamlIncludeAttribute"/>?
        /// </summary>
        internal bool SrcGen_HasYamlInclude { get; set; }

        /// <summary>
        /// Relevant to source generated metadata: is the property public?
        /// </summary>
        internal bool SrcGen_IsPublic { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="YamlNumberHandling"/> applied to the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="YamlPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultYamlTypeInfoResolver"/> or <see cref="YamlSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="YamlNumberHandlingAttribute"/> annotations.
        /// </remarks>
        public YamlNumberHandling? NumberHandling
        {
            get;
            set
            {
                VerifyMutable();
                field = value;
            }
        }

        /// <summary>
        /// Number handling after considering options and declaring type number handling
        /// </summary>
        internal YamlNumberHandling? EffectiveNumberHandling { get; private set; }

        //  Whether the property type can be null.
        internal abstract bool PropertyTypeCanBeNull { get; }

        /// <summary>
        /// Default value used for parameterized ctor invocation.
        /// </summary>
        internal abstract object? DefaultValue { get; }

        /// <summary>
        /// Property index on the list of YamlTypeInfo properties.
        /// It is used as a unique identifier for properties.
        /// It is set just before property is configured and does not change afterward.
        /// It is not equivalent to index on the properties list
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal int PropertyIndex
        {
            get
            {
                Debug.Assert(IsConfigured);
                return _propertyIndex;
            }
            set
            {
                Debug.Assert(!IsConfigured);
                _propertyIndex = value;
            }
        }

        private int _propertyIndex;

        internal bool IsOverriddenOrShadowedBy(YamlPropertyInfo other)
            => MemberName == other.MemberName && DeclaringType.IsAssignableFrom(other.DeclaringType);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Name = {Name}, PropertyType = {PropertyType}";
    }
}
