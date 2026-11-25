// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Converters;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides options to be used with <see cref="YamlSerializer"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed partial class YamlSerializerOptions
    {
        internal const int BufferSizeDefault = 16 * 1024;

        // For backward compatibility the default max depth for YamlSerializer is 64,
        // the minimum of YamlReaderOptions.DefaultMaxDepth and YamlWriterOptions.DefaultMaxDepth.
        internal const int DefaultMaxDepth = YamlReaderOptions.DefaultMaxDepth;

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="YamlSerializerOptions" /> that uses the default configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="YamlSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        public static YamlSerializerOptions Default
        {
            [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
            get => field ?? GetOrCreateSingleton(ref field, YamlSerializerDefaults.General);
        }

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="YamlSerializerOptions" /> that uses the web configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="YamlSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        public static YamlSerializerOptions Web
        {
            [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
            get => field ?? GetOrCreateSingleton(ref field, YamlSerializerDefaults.Web);
        }

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="YamlSerializerOptions" /> that uses the strict configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="YamlSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        public static YamlSerializerOptions Strict
        {
            [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
            get => field ?? GetOrCreateSingleton(ref field, YamlSerializerDefaults.Strict);
        }

        // For any new option added, consider adding it to the options copied in the copy constructor below
        // and consider updating the EqualtyComparer used for comparing CachingContexts.
        private IYamlTypeInfoResolver? _typeInfoResolver;
        private YamlNamingPolicy? _dictionaryKeyPolicy;
        private YamlNamingPolicy? _yamlPropertyNamingPolicy;
        private YamlCommentHandling _readCommentHandling;
        private bool _disallowImplicitCollectionKeys;
        private bool _acceptLenientScalars;
        private int _maxAliasExpansion;
        private bool _resolveMergeKeys;
        private YamlWriterStyle? _writeStyle;
        private bool _indentSequences;
        private YamlScalarStyle _multiLineStringStyle;
        private YamlScalarStyle _defaultScalarStyle;
        private bool _ignoreSourceStyles;
        private int _maxLineWidth;
        private bool _expandSequenceItems;
        private bool _quoteForLenientReaders;
        private ReferenceHandler? _referenceHandler;
        private JavaScriptEncoder? _encoder;
        private ConverterList? _converters;
        private TypeClassifierList? _typeClassifiers;
        private YamlIgnoreCondition _defaultIgnoreCondition;
        private YamlNumberHandling _numberHandling;
        private YamlObjectCreationHandling _preferredObjectCreationHandling;
        private YamlUnknownTypeHandling _unknownTypeHandling;
        private YamlUnmappedMemberHandling _unmappedMemberHandling;

        private int _defaultBufferSize = BufferSizeDefault;
        private int _maxDepth;
        private bool _allowOutOfOrderMetadataProperties;
        private bool _allowTrailingCommas;
        private bool _respectNullableAnnotations = AppContextSwitchHelper.RespectNullableAnnotationsDefault;
        private bool _respectRequiredConstructorParameters = AppContextSwitchHelper.RespectRequiredConstructorParametersDefault;
        private bool _ignoreReadOnlyProperties;
        private bool _ignoreReadonlyFields;
        private bool _includeFields;
        private string? _newLine;
        private bool _propertyNameCaseInsensitive;
        private bool _writeIndented;
        private char _indentCharacter = YamlConstants.DefaultIndentCharacter;
        private int _indentSize = YamlConstants.DefaultIndentSize;
        private YamlDuplicateKeyHandling? _duplicateKeyHandling; // null: the grammar's default
        private YamlSchemaKind _schema;
        private YamlMappingOrder _mappingOrder; // M5f
        private YamlTagMappingDictionary? _tagMappings; // M5d
        private YamlTagMappingTable? _tagMappingTable;
        private bool _inferClosedTypePolymorphism;

        /// <summary>
        /// Constructs a new <see cref="YamlSerializerOptions"/> instance.
        /// </summary>
        public YamlSerializerOptions()
        {
            TrackOptionsInstance(this);
        }

        /// <summary>
        /// Copies the options from a <see cref="YamlSerializerOptions"/> instance to a new instance.
        /// </summary>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> instance to copy options from.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        public YamlSerializerOptions(YamlSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // The following fields are not copied intentionally:
            // 1. _cachingContext can only be set in immutable options instances.
            // 2. _typeInfoResolverChain can be created lazily as it relies on
            //    _typeInfoResolver as its source of truth.

            _dictionaryKeyPolicy = options._dictionaryKeyPolicy;
            _yamlPropertyNamingPolicy = options._yamlPropertyNamingPolicy;
            _readCommentHandling = options._readCommentHandling;
            _disallowImplicitCollectionKeys = options._disallowImplicitCollectionKeys;
            _acceptLenientScalars = options._acceptLenientScalars;
            _maxAliasExpansion = options._maxAliasExpansion;
            _resolveMergeKeys = options._resolveMergeKeys;
            _writeStyle = options._writeStyle;
            _indentSequences = options._indentSequences;
            _multiLineStringStyle = options._multiLineStringStyle;
            _defaultScalarStyle = options._defaultScalarStyle;
            _ignoreSourceStyles = options._ignoreSourceStyles;
            _maxLineWidth = options._maxLineWidth;
            _expandSequenceItems = options._expandSequenceItems;
            _quoteForLenientReaders = options._quoteForLenientReaders;
            _referenceHandler = options._referenceHandler;
            _converters = options._converters is { } converters ? new(this, converters) : null;
            _typeClassifiers = options._typeClassifiers is { } TypeClassifiers ? new(this, TypeClassifiers) : null;
            _encoder = options._encoder;
            _defaultIgnoreCondition = options._defaultIgnoreCondition;
            _numberHandling = options._numberHandling;
            _preferredObjectCreationHandling = options._preferredObjectCreationHandling;
            _unknownTypeHandling = options._unknownTypeHandling;
            _unmappedMemberHandling = options._unmappedMemberHandling;

            _defaultBufferSize = options._defaultBufferSize;
            _maxDepth = options._maxDepth;
            _allowOutOfOrderMetadataProperties = options._allowOutOfOrderMetadataProperties;
            _allowTrailingCommas = options._allowTrailingCommas;
            _respectNullableAnnotations = options._respectNullableAnnotations;
            _respectRequiredConstructorParameters = options._respectRequiredConstructorParameters;
            _ignoreReadOnlyProperties = options._ignoreReadOnlyProperties;
            _ignoreReadonlyFields = options._ignoreReadonlyFields;
            _includeFields = options._includeFields;
            _newLine = options._newLine;
            _propertyNameCaseInsensitive = options._propertyNameCaseInsensitive;
            _writeIndented = options._writeIndented;
            _indentCharacter = options._indentCharacter;
            _indentSize = options._indentSize;
            _duplicateKeyHandling = options._duplicateKeyHandling;
            _schema = options._schema;
            _mappingOrder = options._mappingOrder;
            _tagMappings = options._tagMappings is { Count: > 0 } source ? new YamlTagMappingDictionary(this, source) : null;
            _inferClosedTypePolymorphism = options._inferClosedTypePolymorphism;
            _typeInfoResolver = options._typeInfoResolver;
            EffectiveMaxDepth = options.EffectiveMaxDepth;
            ReferenceHandlingStrategy = options.ReferenceHandlingStrategy;

            TrackOptionsInstance(this);
        }

        /// <summary>
        /// Constructs a new <see cref="YamlSerializerOptions"/> instance with a predefined set of options determined by the specified <see cref="YamlSerializerDefaults"/>.
        /// </summary>
        /// <param name="defaults"> The <see cref="YamlSerializerDefaults"/> to reason about.</param>
        public YamlSerializerOptions(YamlSerializerDefaults defaults) : this()
        {
            // Should be kept in sync with equivalent overload in YamlSourceGenerationOptionsAttribute

            if (defaults == YamlSerializerDefaults.Web)
            {
                _propertyNameCaseInsensitive = true;
                _yamlPropertyNamingPolicy = YamlNamingPolicy.CamelCase;
                _numberHandling = YamlNumberHandling.AllowReadingFromString;
            }
            else if (defaults == YamlSerializerDefaults.Strict)
            {
                _unmappedMemberHandling = YamlUnmappedMemberHandling.Disallow;
                _duplicateKeyHandling = YamlDuplicateKeyHandling.Error;
                _respectNullableAnnotations = true;
                _respectRequiredConstructorParameters = true;
            }
            else if (defaults != YamlSerializerDefaults.General)
            {
                throw new ArgumentOutOfRangeException(nameof(defaults));
            }
        }

        /// <summary>Tracks the options instance to enable all instances to be enumerated.</summary>
        private static void TrackOptionsInstance(YamlSerializerOptions options) => TrackedOptionsInstances.All.Add(options, null);

        internal static class TrackedOptionsInstances
        {
            /// <summary>Tracks all live YamlSerializerOptions instances.</summary>
            /// <remarks>Instances are added to the table in their constructor.</remarks>
            public static ConditionalWeakTable<YamlSerializerOptions, object?> All { get; } =
                // TODO https://github.com/dotnet/runtime/issues/51159:
                // Look into linking this away / disabling it when hot reload isn't in use.
                new ConditionalWeakTable<YamlSerializerOptions, object?>();
        }

        /// <summary>
        /// Gets or sets the <see cref="YamlTypeInfo"/> contract resolver used by this instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// A <see langword="null"/> setting is equivalent to using the reflection-based <see cref="DefaultYamlTypeInfoResolver" />.
        /// The property will be populated automatically once used with one of the <see cref="YamlSerializer"/> methods.
        ///
        /// This property is kept in sync with the <see cref="TypeInfoResolverChain"/> property.
        /// Any change made to this property will be reflected by <see cref="TypeInfoResolverChain"/> and vice versa.
        /// </remarks>
        public IYamlTypeInfoResolver? TypeInfoResolver
        {
            get
            {
                return _typeInfoResolver;
            }
            set
            {
                VerifyMutable();

                if (_typeInfoResolverChain is { } resolverChain && !ReferenceEquals(resolverChain, value))
                {
                    // User is setting a new resolver; detach the resolver chain if already created.
                    resolverChain.DetachFromOptions();
                    _typeInfoResolverChain = null;
                }

                _typeInfoResolver = value;
            }
        }

        /// <summary>
        /// Gets the list of chained <see cref="YamlTypeInfo"/> contract resolvers used by this instance.
        /// </summary>
        /// <remarks>
        /// The ordering of the chain is significant: <see cref="YamlSerializerOptions "/> will query each
        /// of the resolvers in their specified order, returning the first result that is non-null.
        /// If all resolvers in the chain return null, then <see cref="YamlSerializerOptions"/> will also return null.
        ///
        /// This property is auxiliary to and is kept in sync with the <see cref="TypeInfoResolver"/> property.
        /// Any change made to this property will be reflected by <see cref="TypeInfoResolver"/> and vice versa.
        /// </remarks>
        public IList<IYamlTypeInfoResolver> TypeInfoResolverChain => _typeInfoResolverChain ??= new(this);
        private OptionsBoundYamlTypeInfoResolverChain? _typeInfoResolverChain;

        /// <summary>
        /// Allows YAML metadata properties to be specified after regular properties in a deserialized YAML object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// When set to <see langword="true" />, removes the requirement that YAML metadata properties
        /// such as $id and $type should be specified at the very start of the deserialized YAML object.
        ///
        /// It should be noted that enabling this setting can result in over-buffering
        /// when deserializing large YAML payloads in the context of streaming deserialization.
        /// </remarks>
        public bool AllowOutOfOrderMetadataProperties
        {
            get
            {
                return _allowOutOfOrderMetadataProperties;
            }
            set
            {
                VerifyMutable();
                _allowOutOfOrderMetadataProperties = value;
            }
        }

        /// <summary>
        /// Kept for source compatibility with System.Text.Json; it has no effect. YAML flow collections always allow
        /// (and ignore) one trailing comma before their closing bracket.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// The JSON grammar that read this option was deleted in M7a (decision D8); setting it changes nothing.
        /// </remarks>
        public bool AllowTrailingCommas
        {
            get
            {
                return _allowTrailingCommas;
            }
            set
            {
                VerifyMutable();
                _allowTrailingCommas = value;
            }
        }

        /// <summary>
        /// The default buffer size in bytes used when creating temporary buffers.
        /// </summary>
        /// <remarks>The default size is 16K.</remarks>
        /// <exception cref="System.ArgumentException">Thrown when the buffer size is less than 1.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int DefaultBufferSize
        {
            get
            {
                return _defaultBufferSize;
            }
            set
            {
                VerifyMutable();

                if (value < 1)
                {
                    throw new ArgumentException(SR.SerializationInvalidBufferSize);
                }

                _defaultBufferSize = value;
            }
        }

        /// <summary>
        /// The encoder to use when escaping strings, or <see langword="null" /> to use the default encoder.
        /// </summary>
        public JavaScriptEncoder? Encoder
        {
            get
            {
                return _encoder;
            }
            set
            {
                VerifyMutable();

                _encoder = value;
            }
        }

        /// <summary>
        /// Specifies the policy used to convert a <see cref="System.Collections.IDictionary"/> key's name to another format, such as camel-casing.
        /// </summary>
        /// <remarks>
        /// This property can be set to <see cref="YamlNamingPolicy.CamelCase"/> to specify a camel-casing policy.
        /// It is not used when deserializing.
        /// </remarks>
        public YamlNamingPolicy? DictionaryKeyPolicy
        {
            get
            {
                return _dictionaryKeyPolicy;
            }
            set
            {
                VerifyMutable();
                _dictionaryKeyPolicy = value;
            }
        }

        /// <summary>
        /// Specifies a condition to determine when properties with default values are ignored during serialization or deserialization.
        /// The default value is <see cref="YamlIgnoreCondition.Never" />.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if this property is set to <see cref="YamlIgnoreCondition.Always"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlIgnoreCondition DefaultIgnoreCondition
        {
            get
            {
                return _defaultIgnoreCondition;
            }
            set
            {
                VerifyMutable();

                if (value == YamlIgnoreCondition.Always)
                {
                    throw new ArgumentException(SR.DefaultIgnoreConditionInvalid);
                }

                _defaultIgnoreCondition = value;
            }
        }

        /// <summary>
        /// Specifies how number types should be handled when serializing or deserializing.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlNumberHandling NumberHandling
        {
            get => _numberHandling;
            set
            {
                VerifyMutable();

                if (!YamlSerializer.IsValidNumberHandlingValue(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _numberHandling = value;
            }
        }

        /// <summary>
        /// Specifies preferred object creation handling for properties when deserializing YAML.
        /// When set to <see cref="YamlObjectCreationHandling.Populate"/> all properties which
        /// are capable of reusing the existing instance will be populated.
        /// </summary>
        /// <remarks>
        /// Only property type is taken into consideration. For example if property is of type
        /// <see cref="IEnumerable{T}"/> but it is assigned <see cref="List{T}"/> it will not be populated
        /// because <see cref="IEnumerable{T}"/> is not capable of populating.
        /// Additionally value types require a setter to be populated.
        /// </remarks>
        public YamlObjectCreationHandling PreferredObjectCreationHandling
        {
            get => _preferredObjectCreationHandling;
            set
            {
                VerifyMutable();

                if (!YamlSerializer.IsValidCreationHandlingValue(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _preferredObjectCreationHandling = value;
            }
        }

        /// <summary>
        /// Determines whether read-only properties are ignored during serialization.
        /// A property is read-only if it contains a public getter but not a public setter.
        /// The default value is false.
        /// </summary>
        /// <remarks>
        /// Read-only properties are not deserialized regardless of this setting.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IgnoreReadOnlyProperties
        {
            get
            {
                return _ignoreReadOnlyProperties;
            }
            set
            {
                VerifyMutable();
                _ignoreReadOnlyProperties = value;
            }
        }

        /// <summary>
        /// Determines whether read-only fields are ignored during serialization.
        /// A field is read-only if it is marked with the <c>readonly</c> keyword.
        /// The default value is false.
        /// </summary>
        /// <remarks>
        /// Read-only fields are not deserialized regardless of this setting.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IgnoreReadOnlyFields
        {
            get
            {
                return _ignoreReadonlyFields;
            }
            set
            {
                VerifyMutable();
                _ignoreReadonlyFields = value;
            }
        }

        /// <summary>
        /// Determines whether fields are handled on serialization and deserialization.
        /// The default value is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IncludeFields
        {
            get
            {
                return _includeFields;
            }
            set
            {
                VerifyMutable();
                _includeFields = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum depth allowed when serializing or deserializing YAML, with the default (i.e. 0) indicating a max depth of 64.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the max depth is set to a negative value.
        /// </exception>
        /// <remarks>
        /// Going past this depth will throw a <exception cref="YamlException"/>.
        /// </remarks>
        public int MaxDepth
        {
            get => _maxDepth;
            set
            {
                VerifyMutable();

                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(nameof(value));
                }

                _maxDepth = value;
                EffectiveMaxDepth = (value == 0 ? DefaultMaxDepth : value);
            }
        }

        internal int EffectiveMaxDepth { get; private set; } = DefaultMaxDepth;

        /// <summary>
        /// Specifies the policy used to convert a property's name on an object to another format, such as camel-casing.
        /// The resulting property name is expected to match the YAML payload during deserialization, and
        /// will be used when writing the property name during serialization.
        /// </summary>
        /// <remarks>
        /// The policy is not used for properties that have a <see cref="YamlPropertyNameAttribute"/> applied.
        /// This property can be set to <see cref="YamlNamingPolicy.CamelCase"/> to specify a camel-casing policy.
        /// </remarks>
        public YamlNamingPolicy? PropertyNamingPolicy
        {
            get
            {
                return _yamlPropertyNamingPolicy;
            }
            set
            {
                VerifyMutable();
                _yamlPropertyNamingPolicy = value;
            }
        }

        /// <summary>
        /// Determines whether a property's name uses a case-insensitive comparison during deserialization.
        /// The default value is false.
        /// </summary>
        /// <remarks>There is a performance cost associated when the value is true.</remarks>
        public bool PropertyNameCaseInsensitive
        {
            get
            {
                return _propertyNameCaseInsensitive;
            }
            set
            {
                VerifyMutable();
                _propertyNameCaseInsensitive = value;
            }
        }

        /// <summary>
        /// Defines how the comments are handled during deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (or not within the <see cref="YamlCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// By default (<see cref="YamlCommentHandling.Disallow"/>) <c>#</c> comments are skipped and <c>//</c> and
        /// <c>/* */</c> are not comments, as in standard YAML. With <see cref="YamlCommentHandling.Skip"/> the JSON
        /// comments of a JSON payload are skipped too, as System.Text.Json skips them; see
        /// <see cref="YamlCommentHandling"/> for where they are read. <see cref="YamlCommentHandling.Allow"/> is not
        /// supported here.
        /// </remarks>
        public YamlCommentHandling ReadCommentHandling
        {
            get
            {
                return _readCommentHandling;
            }
            set
            {
                VerifyMutable();

                Debug.Assert(value >= 0);
                if (value > YamlCommentHandling.Skip)
                    throw new ArgumentOutOfRangeException(nameof(value), SR.YamlSerializerDoesNotSupportComments);

                _readCommentHandling = value;
            }
        }

        /// <summary>
        /// Defines how deserializing a type declared as an <see cref="object"/> is handled during deserialization.
        /// </summary>
        public YamlUnknownTypeHandling UnknownTypeHandling
        {
            get => _unknownTypeHandling;
            set
            {
                VerifyMutable();
                _unknownTypeHandling = value;
            }
        }

        /// <summary>
        /// Determines how <see cref="YamlSerializer"/> handles YAML properties that
        /// cannot be mapped to a specific .NET member when deserializing object types.
        /// </summary>
        public YamlUnmappedMemberHandling UnmappedMemberHandling
        {
            get => _unmappedMemberHandling;
            set
            {
                VerifyMutable();
                _unmappedMemberHandling = value;
            }
        }

        /// <summary>
        /// Defines whether YAML should pretty print which includes:
        /// indenting nested YAML tokens, adding new lines, and adding white space between property names and values.
        /// By default, the YAML is serialized without any extra white space.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool WriteIndented
        {
            get
            {
                return _writeIndented;
            }
            set
            {
                VerifyMutable();
                _writeIndented = value;
            }
        }

        /// <summary>
        /// Defines the indentation character being used when <see cref="WriteIndented" /> is enabled. Defaults to the space character.
        /// </summary>
        /// <remarks>Allowed characters are space and horizontal tab.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains an invalid character.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public char IndentCharacter
        {
            get
            {
                return _indentCharacter;
            }
            set
            {
                YamlWriterHelper.ValidateIndentCharacter(value);
                VerifyMutable();
                _indentCharacter = value;
            }
        }

        /// <summary>
        /// Defines the indentation size being used when <see cref="WriteIndented" /> is enabled. Defaults to two.
        /// </summary>
        /// <remarks>Allowed values are all integers between 0 and 127, included.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is out of the allowed range.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int IndentSize
        {
            get
            {
                return _indentSize;
            }
            set
            {
                YamlWriterHelper.ValidateIndentSize(value);
                VerifyMutable();
                _indentSize = value;
            }
        }

        /// <summary>
        /// Configures how object references are handled when reading and writing YAML.
        /// </summary>
        public ReferenceHandler? ReferenceHandler
        {
            get => _referenceHandler;
            set
            {
                VerifyMutable();
                _referenceHandler = value;
                ReferenceHandlingStrategy = value?.HandlingStrategy ?? YamlKnownReferenceHandler.Unspecified;
            }
        }

        /// <summary>
        /// Gets or sets the new line string to use when <see cref="WriteIndented"/> is <see langword="true"/>.
        /// The default is the value of <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the new line string is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the new line string is not <c>\n</c> or <c>\r\n</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public string NewLine
        {
            get
            {
                return _newLine ??= Environment.NewLine;
            }
            set
            {
                YamlWriterHelper.ValidateNewLine(value);
                VerifyMutable();
                _newLine = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether nullability annotations should be respected during serialization and deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// Nullability annotations are resolved from the properties, fields and constructor parameters
        /// that are used by the serializer. This includes annotations stemming from attributes such as
        /// <see cref="NotNullAttribute"/>, <see cref="MaybeNullAttribute"/>,
        /// <see cref="AllowNullAttribute"/> and <see cref="DisallowNullAttribute"/>.
        ///
        /// Due to restrictions in how nullable reference types are represented at run time,
        /// this setting only governs nullability annotations of non-generic properties and fields.
        /// It cannot be used to enforce nullability annotations of root-level types or generic parameters.
        ///
        /// The default setting for this property can be toggled application-wide using the
        /// "System.Text.Yaml.Serialization.RespectNullableAnnotationsDefault" feature switch.
        /// </remarks>
        public bool RespectNullableAnnotations
        {
            get => _respectNullableAnnotations;
            set
            {
                VerifyMutable();
                _respectNullableAnnotations = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether non-optional constructor parameters should be specified during deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// For historical reasons constructor-based deserialization treats all constructor parameters as optional by default.
        /// This flag allows users to toggle that behavior as necessary for each <see cref="YamlSerializerOptions"/> instance.
        ///
        /// The default setting for this property can be toggled application-wide using the
        /// "System.Text.Yaml.Serialization.RespectRequiredConstructorParametersDefault" feature switch.
        /// </remarks>
        public bool RespectRequiredConstructorParameters
        {
            get => _respectRequiredConstructorParameters;
            set
            {
                VerifyMutable();
                _respectRequiredConstructorParameters = value;
            }
        }

        /// <summary>
        /// Defines whether duplicate property names are allowed when deserializing YAML objects: a view of
        /// <see cref="DuplicateKeyHandling"/>, true unless it is <see cref="YamlDuplicateKeyHandling.Error"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Setting it to true selects <see cref="YamlDuplicateKeyHandling.LastWins"/> and false selects
        /// <see cref="YamlDuplicateKeyHandling.Error"/>, which throws a <see cref="YamlException"/> when a duplicate
        /// property name is encountered during deserialization. By default it is false.
        /// </para>
        /// <para>
        /// Duplicate property names are not allowed in serialization.
        /// </para>
        /// </remarks>
        public bool AllowDuplicateProperties
        {
            get => DuplicateKeyHandling != YamlDuplicateKeyHandling.Error;
            set => DuplicateKeyHandling = value ? YamlDuplicateKeyHandling.LastWins : YamlDuplicateKeyHandling.Error;
        }

        /// <summary>
        /// Gets or sets what deserialization does with a mapping key that appears twice
        /// (<see cref="YamlDuplicateKeyHandling"/>). By default, <see cref="YamlDuplicateKeyHandling.Error"/>, as YAML 1.2
        /// requires. A
        /// repeated key that no member maps, or one inside a value that is skipped, is not judged.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="YamlDuplicateKeyHandling"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlDuplicateKeyHandling DuplicateKeyHandling
        {
            get => _duplicateKeyHandling ?? YamlDocumentOptions.DefaultDuplicateKeyHandling;
            set
            {
                VerifyMutable();
                if ((uint)value > (uint)YamlDuplicateKeyHandling.FirstWins)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _duplicateKeyHandling = value;
            }
        }

        /// <summary>
        /// Gets or sets the order in which an object's members are written:
        /// <see cref="YamlMappingOrder.Declaration"/> (the default) or <see cref="YamlMappingOrder.Sorted"/>, by
        /// <see cref="YamlPropertyOrderAttribute"/>'s order and then by the written name, ordinal, as SharpYaml's
        /// <c>MappingOrder = Sorted</c>. Extension data's entries follow the members, sorted among themselves; a type
        /// discriminator stays first. Dictionaries keep their own order. Reading is unaffected.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="YamlMappingOrder"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlMappingOrder MappingOrder
        {
            get => _mappingOrder;
            set
            {
                VerifyMutable();
                if ((uint)value > (uint)YamlMappingOrder.Sorted)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _mappingOrder = value;
            }
        }

        /// <summary>
        /// Gets the YAML tags that select a type when reading, and that the type writes, as YamlDotNet's
        /// <c>WithTagMapping</c>: an explicit allow-list. A
        /// tag never names a CLR type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A key is a local tag (<c>!dog</c>) or a global tag URI (<c>tag:example.com,2000:dog</c>), compared with a node's
        /// expanded tag, so a <c>%TAG</c> handle and a verbatim tag select the same entry. Core tags (<c>!!str</c>),
        /// <c>!</c> and <c>?</c> are refused, as are a tag or a type mapped twice, and a type that cannot be created (open
        /// generic, abstract, an interface) or cannot be a runtime type (<see cref="Nullable{T}"/>).
        /// </para>
        /// <para>
        /// Reading: at an <see cref="object"/> site a mapped tag reads its type, scalars included (<c>!celsius 21.5</c>
        /// mapped to <see cref="double"/>); an unmapped tag keeps the <see cref="YamlElement"/>, with its tag. At a site of a
        /// reference type with no declared polymorphism, a mapped type it can hold is read; one it cannot hold is an error.
        /// A declared hierarchy decides at its own sites. The type comes from the options' type-info resolution, so a
        /// source-generated context must contain it.
        /// </para>
        /// <para>
        /// Writing: one tag per type. A value whose runtime type is mapped is written with the tag where the declared type
        /// differs, so every boxed <see cref="double"/> in an <see cref="object"/> member is written <c>!celsius …</c> with
        /// the entry above; a boxed <c>double?</c> is a <see cref="double"/>. A mapped type with a custom converter gets the
        /// tag before whatever node the converter writes. The JSON-compatible writer cannot write a tag: it writes a mapped
        /// value untagged, so it reads back as an element at an <see cref="object"/> site.
        /// </para>
        /// <para>
        /// Setting entries on options that use a source-generated context turns its serialization fast path off.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">An entry is changed after serialization or deserialization has occurred.</exception>
        public IDictionary<string, Type> TagMappings => _tagMappings ??= new YamlTagMappingDictionary(this);

        /// <summary>Whether any tag is mapped; decided when the options freeze.</summary>
        internal bool HasTagMappings => _tagMappings is { Count: > 0 };

        /// <summary>The frozen lookup of <see cref="TagMappings"/>, or null when there is none.</summary>
        internal YamlTagMappingTable? TagMappingTable
        {
            get
            {
                Debug.Assert(IsReadOnly);
                return _tagMappingTable ??= HasTagMappings ? new YamlTagMappingTable(_tagMappings!.Entries) : null;
            }
        }

        internal void VerifyMutableForTagMappings() => VerifyMutable();

        /// <summary>
        /// Gets or sets the schema that resolves plain scalars in value position when deserializing:
        /// <see cref="YamlSchemaKind.Core"/> (YAML 1.2, the default), <see cref="YamlSchemaKind.Yaml11"/>,
        /// <see cref="YamlSchemaKind.Json"/> or <see cref="YamlSchemaKind.Failsafe"/>. Under <c>Json</c> block output
        /// double-quotes every string value (and every date, time or GUID) it would otherwise write plain, so it reads back
        /// under the same schema (M7c); keys are unchanged, since the JSON schema reads plain keys, and a non-finite number
        /// (<c>.nan</c>, <c>.inf</c>) has no YAML-schema form and is refused on read. Under <c>Failsafe</c> writing does not
        /// change, and plain numbers and booleans read back as strings.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="YamlSchemaKind"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlSchemaKind Schema
        {
            get => _schema;
            set
            {
                VerifyMutable();
                if ((uint)value > (uint)YamlSchemaKind.Failsafe)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _schema = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether polymorphic serialization metadata is inferred for
        /// types that the compiler has marked as closed type hierarchies.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// By default, it's set to <see langword="false"/>. When set to <see langword="true"/>, types that
        /// declare a closed set of derived types (and that do not specify an explicit
        /// <see cref="Serialization.YamlDerivedTypeAttribute"/> list) are treated as polymorphic. Closed
        /// derived types are expanded recursively, and each terminal derived type is registered using its
        /// simple name, equivalent to the result of <c>nameof</c>, as its type discriminator.
        /// If a type declares one or more <see cref="Serialization.YamlDerivedTypeAttribute"/> registrations,
        /// inference is skipped for that type and only the explicitly registered derived types are used.
        /// Polymorphism configuration declared on derived types applies to their respective contracts and does
        /// not affect inference for the base type.
        /// </remarks>
        public bool InferClosedTypePolymorphism
        {
            get => _inferClosedTypePolymorphism;
            set
            {
                VerifyMutable();
                _inferClosedTypePolymorphism = value;
            }
        }

        /// <summary>
        /// Returns true if options uses compatible built-in resolvers or a combination of compatible built-in resolvers.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool CanUseFastPathSerializationLogic
        {
            get
            {
                Debug.Assert(IsReadOnly);
                Debug.Assert(TypeInfoResolver is not null);
                return _canUseFastPathSerializationLogic ??= TypeInfoResolver.IsCompatibleWithOptions(this);
            }
        }

        private bool? _canUseFastPathSerializationLogic;

        // The cached value used to determine if ReferenceHandler should use Preserve or IgnoreCycles semantics or None of them.
        internal YamlKnownReferenceHandler ReferenceHandlingStrategy = YamlKnownReferenceHandler.Unspecified;

        /// <summary>
        /// Specifies whether the current instance has been locked for user modification.
        /// </summary>
        /// <remarks>
        /// A <see cref="YamlSerializerOptions"/> instance can be locked either if
        /// it has been passed to one of the <see cref="YamlSerializer"/> methods,
        /// has been associated with a <see cref="YamlSerializerContext"/> instance,
        /// or a user explicitly called the <see cref="MakeReadOnly()"/> methods on the instance.
        ///
        /// Read-only instances use caching when querying <see cref="YamlConverter"/> and <see cref="YamlTypeInfo"/> metadata.
        /// </remarks>
        public bool IsReadOnly => _isReadOnly;
        private volatile bool _isReadOnly;

        /// <summary>
        /// Marks the current instance as read-only preventing any further user modification.
        /// </summary>
        /// <exception cref="InvalidOperationException">The instance does not specify a <see cref="TypeInfoResolver"/> setting.</exception>
        /// <remarks>This method is idempotent.</remarks>
        public void MakeReadOnly()
        {
            if (_typeInfoResolver is null)
            {
                ThrowHelper.ThrowInvalidOperationException_YamlSerializerOptionsNoTypeInfoResolverSpecified();
            }

            _isReadOnly = true;
        }

        /// <summary>
        /// Marks the current instance as read-only preventing any further user modification.
        /// </summary>
        /// <param name="populateMissingResolver">Populates unconfigured <see cref="TypeInfoResolver"/> properties with the reflection-based default.</param>
        /// <exception cref="InvalidOperationException">
        /// The instance does not specify a <see cref="TypeInfoResolver"/> setting. Thrown if <paramref name="populateMissingResolver"/> is <see langword="false"/>.
        /// -OR-
        /// The <see cref="YamlSerializer.IsReflectionEnabledByDefault"/> feature switch has been turned off.
        /// </exception>
        /// <remarks>
        /// When <paramref name="populateMissingResolver"/> is set to <see langword="true" />, configures the instance following
        /// the semantics of the <see cref="YamlSerializer"/> methods accepting <see cref="YamlSerializerOptions"/> parameters.
        ///
        /// This method is idempotent.
        /// </remarks>
        [RequiresUnreferencedCode("Populating unconfigured TypeInfoResolver properties with the reflection resolver requires unreferenced code.")]
        [RequiresDynamicCode("Populating unconfigured TypeInfoResolver properties with the reflection resolver requires runtime code generation.")]
        public void MakeReadOnly(bool populateMissingResolver)
        {
            if (populateMissingResolver)
            {
                if (!_isConfiguredForYamlSerializer)
                {
                    ConfigureForYamlSerializer();
                }
            }
            else
            {
                MakeReadOnly();
            }

            Debug.Assert(IsReadOnly);
        }

        /// <summary>
        /// Configures the instance for use by the YamlSerializer APIs, applying reflection-based fallback where applicable.
        /// </summary>
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private void ConfigureForYamlSerializer()
        {
            if (YamlSerializer.IsReflectionEnabledByDefault)
            {
                // Even if a resolver has already been specified, we need to root
                // the default resolver to gain access to the default converters.
                DefaultYamlTypeInfoResolver defaultResolver = DefaultYamlTypeInfoResolver.DefaultInstance;

                switch (_typeInfoResolver)
                {
                    case null:
                        // Use the default reflection-based resolver if no resolver has been specified.
                        _typeInfoResolver = defaultResolver;
                        break;

                    case YamlSerializerContext ctx when AppContextSwitchHelper.IsSourceGenReflectionFallbackEnabled:
                        // .NET 6 compatibility mode: enable fallback to reflection metadata for YamlSerializerContext
                        _effectiveYamlTypeInfoResolver = YamlTypeInfoResolver.Combine(ctx, defaultResolver);

                        if (_cachingContext is { } cachingContext)
                        {
                            // A cache has already been created by the source generator.
                            // Repeat the same configuration routine for that options instance, if different.
                            // Invalidate any cache entries that have already been stored.
                            if (cachingContext.Options != this && !cachingContext.Options._isConfiguredForYamlSerializer)
                            {
                                cachingContext.Options.ConfigureForYamlSerializer();
                            }
                            else
                            {
                                cachingContext.Clear();
                            }
                        }
                        break;
                }
            }
            else if (_typeInfoResolver is null or EmptyYamlTypeInfoResolver)
            {
                ThrowHelper.ThrowInvalidOperationException_YamlSerializerIsReflectionDisabled();
            }

            Debug.Assert(_typeInfoResolver is not null);
            // NB preserve write order.
            _isReadOnly = true;
            _isConfiguredForYamlSerializer = true;
        }

        /// <summary>
        /// This flag is supplementary to <see cref="_isReadOnly"/> and is only used to keep track
        /// of source-gen reflection fallback (assuming the IsSourceGenReflectionFallbackEnabled feature switch is on).
        /// This mode necessitates running the <see cref="ConfigureForYamlSerializer"/> method even
        /// for options instances that have been marked as read-only.
        /// </summary>
        private volatile bool _isConfiguredForYamlSerializer;

        // Only populated in .NET 6 compatibility mode encoding reflection fallback in source gen
        private IYamlTypeInfoResolver? _effectiveYamlTypeInfoResolver;

        private YamlTypeInfo? GetTypeInfoNoCaching(Type type)
        {
            IYamlTypeInfoResolver? resolver = _effectiveYamlTypeInfoResolver ?? _typeInfoResolver;
            if (resolver is null)
            {
                return null;
            }

            YamlTypeInfo? info = resolver.GetTypeInfo(type, this);

            if (info is not null)
            {
                if (info.Type != type)
                {
                    ThrowHelper.ThrowInvalidOperationException_ResolverTypeNotCompatible(type, info.Type);
                }

                if (info.Options != this)
                {
                    ThrowHelper.ThrowInvalidOperationException_ResolverTypeInfoOptionsNotCompatible();
                }
            }
            else
            {
                Debug.Assert(_effectiveYamlTypeInfoResolver is null, "an effective resolver always returns metadata");

                if (type == YamlTypeInfo.ObjectType)
                {
                    // If the resolver does not provide a YamlTypeInfo<object> instance, fill
                    // with the serialization-only converter to enable polymorphic serialization.
                    var converter = new SlimObjectConverter(resolver);
                    info = new YamlTypeInfo<object>(converter, this);
                }
            }

            return info;
        }

        /// <summary>
        /// The options of the write-then-parse paths (<c>SerializeToDocument</c>, <c>SerializeToElement</c>,
        /// <c>SerializeToNode</c>): the text is the writer's own, so it is read as Core and with no duplicate check (a
        /// <c>Dictionary&lt;object, T&gt;</c> with keys <c>1</c> and <c>"1"</c> writes both; 5.3, A6).
        /// </summary>
        internal YamlDocumentOptions GetDocumentOptions()
        {
            return new YamlDocumentOptions
            {
                DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = ReadCommentHandling,
                MaxDepth = MaxDepth,
            };
        }

        internal YamlNodeOptions GetNodeOptions()
        {
            return new YamlNodeOptions
            {
                PropertyNameCaseInsensitive = PropertyNameCaseInsensitive
            };
        }

        /// <summary>
        /// Gets or sets whether a flow collection may <em>not</em> be an implicit mapping key when reading
        /// (<see cref="YamlReaderOptions.DisallowImplicitCollectionKeys"/>). <see langword="false"/> by default. Set it to
        /// <see langword="true"/> so that a stream of YAML, for example with <c>DeserializeAsyncEnumerable</c>, yields each
        /// element as soon as it is complete rather than once its line, or 1024 characters, is known.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool DisallowImplicitCollectionKeys
        {
            get => _disallowImplicitCollectionKeys;
            set
            {
                VerifyMutable();
                _disallowImplicitCollectionKeys = value;
            }
        }

        /// <summary>
        /// Gets or sets whether typed reads accept scalars that YamlDotNet or SharpYaml resolve although no YAML standard
        /// does (<see cref="YamlReaderOptions.AcceptLenientScalars"/>, which lists them): booleans and numbers in other
        /// cases, other float names, quoted, block and tagged booleans, numbers and enum numbers (<c>'12'</c>), padded
        /// dates, times and GUIDs (<c>' 2001-01-01 '</c>), <c>null</c> in any case as null, and a null into a
        /// non-nullable value type as its default. A constructor parameter refuses that null, as without the option,
        /// where a property of the same type takes the default. Under <see cref="YamlSchemaKind.Json"/>, an untagged plain
        /// scalar that matches no JSON form (<c>foo</c>, <c>~</c>, the empty scalar) reads as a string rather than an error,
        /// a superset of SharpYaml's reading (<c>True</c> also reads into a <see cref="bool"/>, which SharpYaml's JSON schema
        /// refuses). Off by default. When deserializing from a <see cref="Utf8YamlReader"/>, the reader's own option applies.
        /// A converter of your own receives <c>NuLL</c> as a <see cref="YamlTokenType.String"/> token and a null as the
        /// <see cref="YamlTokenType.Null"/> token, as without the option. Strings such as <c>NuLl</c> are written plain by
        /// default and read back as null with this option; set <see cref="QuoteForLenientReaders"/> where the same
        /// documents are written.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool AcceptLenientScalars
        {
            get => _acceptLenientScalars;
            set
            {
                VerifyMutable();
                _acceptLenientScalars = value;
            }
        }

        /// <summary>
        /// Gets or sets the most nodes that YAML aliases may copy in one deserialization. An alias to an object or a
        /// collection shares the instance read at its anchor and copies nothing; an alias read into a scalar, a value
        /// type, an <see cref="object"/> member or a type the anchored instance does not fit reads the anchored node
        /// again, and those nodes count. Past the limit, a <see cref="YamlException"/> is thrown. The default, 0, allows
        /// 10,000 nodes, or as many as the bytes of the input read so far when that is more.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int MaxAliasExpansion
        {
            get => _maxAliasExpansion;
            set
            {
                VerifyMutable();
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _maxAliasExpansion = value;
            }
        }

        /// <summary>
        /// Gets or sets whether a plain <c>&lt;&lt;</c> key merges mappings into the mapping that holds it (the YAML 1.1
        /// merge type, yaml.org/type/merge): its value is an alias, a mapping or a sequence of them, the mapping's own
        /// keys win over merged ones, and an earlier merged mapping wins over a later one. Off by default, so
        /// <c>&lt;&lt;</c> is an ordinary key.
        /// </summary>
        /// <remarks>
        /// With a <c>&lt;&lt;</c> anywhere in the input, the document is read from its rewritten text: positions in errors refer to that
        /// text; a syntax error surfaces from the rewrite's parse as a <see cref="YamlReaderException"/> with no path; and
        /// the raw text of every <c>object</c> member (a <see cref="YamlElement"/>) is the rewritten spelling (block style,
        /// double quotes), not the source's.
        /// <c>DeserializeAsyncEnumerable</c> refuses the option.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool ResolveMergeKeys
        {
            get => _resolveMergeKeys;
            set
            {
                VerifyMutable();
                _resolveMergeKeys = value;
            }
        }

        /// <summary>
        /// Gets or sets the output style of serialization: <see cref="YamlWriterStyle.Block"/> YAML, or
        /// <see cref="YamlWriterStyle.JsonCompatible"/> JSON. When it is not set, the output is block YAML.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlWriterStyle WriteStyle
        {
            get => _writeStyle ?? default(YamlWriterOptions).Style;
            set
            {
                VerifyMutable();
                _writeStyle = value;
            }
        }

        /// <summary>
        /// Gets or sets whether, in block style, a sequence under a key is indented (<c>key:\n  - a</c>) rather than written
        /// at the key's column (<c>key:\n- a</c>, the default). Ignored in JSON-compatible style.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IndentSequences
        {
            get => _indentSequences;
            set
            {
                VerifyMutable();
                _indentSequences = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, how a string with a line break is written: literal (the default,
        /// <see cref="YamlScalarStyle.None"/> or <see cref="YamlScalarStyle.Literal"/>), folded, or double-quoted
        /// (<see cref="YamlScalarStyle.DoubleQuoted"/>, the default before M5f), where a block scalar can hold it
        /// (<see cref="YamlWriterOptions.MultiLineStringStyle"/>). Ignored in JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not one of those four.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlScalarStyle MultiLineStringStyle
        {
            get => _multiLineStringStyle;
            set
            {
                VerifyMutable();
                if (value is not (YamlScalarStyle.None or YamlScalarStyle.DoubleQuoted or YamlScalarStyle.Literal or YamlScalarStyle.Folded))
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(value));
                }

                _multiLineStringStyle = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, the style for every string value that has no style of its own: no member
        /// <see cref="YamlScalarStyleAttribute"/>, no converter's <see cref="Utf8YamlWriter.WriteScalarStyle"/> and no source
        /// style kept by a <see cref="YamlDocument"/> or a <see cref="Nodes.YamlNode"/>; for a string with a line break, a
        /// <see cref="MultiLineStringStyle"/> that is set goes first. <see cref="YamlScalarStyle.None"/>, the default,
        /// leaves the writer's own choice. The style is applied where the string can be written in it, as a member's
        /// style is, and otherwise the string is written as it would be without it
        /// (<see cref="YamlWriterOptions.DefaultScalarStyle"/>). It reaches every value written as a YAML string: dates,
        /// times, GUIDs, URIs, versions, chars, enum names and Base64 take a single- or double-quoted default. A dictionary's
        /// string keys take it too, as YamlDotNet's do, and member names do not; numbers, booleans and nulls are not
        /// styled. A source-generated context takes its metadata path while it is set, since its generated dictionary
        /// handlers write keys out of the option's reach. Ignored in JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a scalar style.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public YamlScalarStyle DefaultScalarStyle
        {
            get => _defaultScalarStyle;
            set
            {
                VerifyMutable();
                if (value is < YamlScalarStyle.None or > YamlScalarStyle.Folded)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(value));
                }

                _defaultScalarStyle = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, whether a <see cref="YamlDocument"/>, a <see cref="YamlElement"/> or a
        /// <see cref="Nodes.YamlNode"/> is written in these options' styles rather than its source's: collections in
        /// <see cref="WriteStyle"/>, scalars and keys as a typed value of the same text would be written, with
        /// <see cref="DefaultScalarStyle"/> and <see cref="MultiLineStringStyle"/> applying. So a parsed YAML document
        /// is written as block YAML, as its typed graph would be. A converter's or a member's style, and a node's
        /// <see cref="Nodes.YamlObject.CollectionStyle"/> or <see cref="Nodes.YamlArray.CollectionStyle"/> set by the
        /// caller, still apply. Anchors and tags are kept. Off by default: a document keeps its source's styles, as both
        /// YamlDotNet's and SharpYaml's models keep them (<see cref="YamlWriterOptions.IgnoreSourceStyles"/>). Ignored in
        /// JSON-compatible style.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IgnoreSourceStyles
        {
            get => _ignoreSourceStyles;
            set
            {
                VerifyMutable();
                _ignoreSourceStyles = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, the width past which scalars are folded at a space and flow collections continue
        /// on a new line; 0 (the default) means no folding (<see cref="YamlWriterOptions.MaxLineWidth"/>). Ignored in
        /// JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int MaxLineWidth
        {
            get => _maxLineWidth;
            set
            {
                VerifyMutable();
                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxLineWidthMustNotBeNegative(nameof(value));
                }

                _maxLineWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, whether a collection that is a sequence item starts on the line after its dash
        /// (<see cref="YamlWriterOptions.ExpandSequenceItems"/>). Ignored in JSON-compatible style.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool ExpandSequenceItems
        {
            get => _expandSequenceItems;
            set
            {
                VerifyMutable();
                _expandSequenceItems = value;
            }
        }

        /// <summary>
        /// Gets or sets, in block style, whether strings that some readers misread although no YAML standard resolves
        /// them, such as Ruby's Psych, are quoted too (<see cref="YamlWriterOptions.QuoteForLenientReaders"/>). Off by
        /// default. Ignored in JSON-compatible style.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool QuoteForLenientReaders
        {
            get => _quoteForLenientReaders;
            set
            {
                VerifyMutable();
                _quoteForLenientReaders = value;
            }
        }

        internal YamlReaderOptions GetReaderOptions()
        {
            return new YamlReaderOptions
            {
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = ReadCommentHandling,
                MaxDepth = EffectiveMaxDepth,
                DisallowImplicitCollectionKeys = DisallowImplicitCollectionKeys,
                AcceptLenientScalars = _acceptLenientScalars,
                Schema = _schema,
                RecordAnchors = true,
            };
        }

        internal YamlWriterOptions GetWriterOptions()
        {
            var options = new YamlWriterOptions
            {
                Encoder = Encoder,
                Indented = WriteIndented,
                IndentCharacter = IndentCharacter,
                IndentSize = IndentSize,
                MaxDepth = EffectiveMaxDepth,
                NewLine = NewLine,
                IndentSequences = IndentSequences,
                MultiLineStringStyle = _multiLineStringStyle,
                DefaultScalarStyle = _defaultScalarStyle,
                IgnoreSourceStyles = _ignoreSourceStyles,
                QuoteStringsForJsonSchema = _schema == YamlSchemaKind.Json,
                MaxLineWidth = _maxLineWidth,
                ExpandSequenceItems = _expandSequenceItems,
                QuoteForLenientReaders = _quoteForLenientReaders,
#if !DEBUG
                SkipValidation = true
#endif
            };

            if (_writeStyle is YamlWriterStyle style)
            {
                options.Style = style;
            }

            return options;
        }

        // Per JSON Lines spec (https://jsonlines.org/) every value must occupy a single line.
        // Indentation must be suppressed regardless of the user-configured WriteIndented setting,
        // and the YamlWriterOptions.NewLine setting is irrelevant when Indented is false.
        internal YamlWriterOptions GetWriterOptionsForYamlLines()
        {
            // Block style, set or by default (M4c), is one entry per line anyway; the user's layout options apply.
            YamlWriterOptions blockOptions = GetWriterOptions();
            if (blockOptions.IsBlockStyle)
            {
                return blockOptions;
            }

            var options = new YamlWriterOptions
            {
                Encoder = Encoder,
                MaxDepth = EffectiveMaxDepth,
                IndentSequences = IndentSequences,
                MultiLineStringStyle = _multiLineStringStyle,
                DefaultScalarStyle = _defaultScalarStyle,
                IgnoreSourceStyles = _ignoreSourceStyles,
                QuoteStringsForJsonSchema = _schema == YamlSchemaKind.Json,
                MaxLineWidth = _maxLineWidth,
                ExpandSequenceItems = _expandSequenceItems,
                QuoteForLenientReaders = _quoteForLenientReaders,
#if !DEBUG
                SkipValidation = true
#endif
            };

            if (_writeStyle is YamlWriterStyle style)
            {
                options.Style = style;
            }

            return options;
        }

        internal void VerifyMutable()
        {
            if (_isReadOnly)
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerOptionsReadOnly(_typeInfoResolver as YamlSerializerContext);
            }
        }

        private sealed class ConverterList : ConfigurationList<YamlConverter>
        {
            private readonly YamlSerializerOptions _options;

            public ConverterList(YamlSerializerOptions options, IList<YamlConverter>? source = null)
                : base(source)
            {
                _options = options;
            }

            public override bool IsReadOnly => _options.IsReadOnly;
            protected override void OnCollectionModifying() => _options.VerifyMutable();
        }

        private sealed class TypeClassifierList : ConfigurationList<YamlTypeClassifierFactory>
        {
            private readonly YamlSerializerOptions _options;

            public TypeClassifierList(YamlSerializerOptions options, IList<YamlTypeClassifierFactory>? source = null)
                : base(source)
            {
                _options = options;
            }

            public override bool IsReadOnly => _options.IsReadOnly;
            protected override void OnCollectionModifying() => _options.VerifyMutable();
        }

        private sealed class OptionsBoundYamlTypeInfoResolverChain : YamlTypeInfoResolverChain
        {
            private YamlSerializerOptions? _options;

            public OptionsBoundYamlTypeInfoResolverChain(YamlSerializerOptions options)
            {
                _options = options;
                AddFlattened(options._typeInfoResolver);
            }

            public void DetachFromOptions()
            {
                _options = null;
            }

            public override bool IsReadOnly => _options?.IsReadOnly is true;

            protected override void ValidateAddedValue(IYamlTypeInfoResolver item)
            {
                Debug.Assert(item is not null);

                if (ReferenceEquals(item, this) || ReferenceEquals(item, _options?._typeInfoResolver))
                {
                    // Cannot add the instances in TypeInfoResolver or TypeInfoResolverChain to the chain itself.
                    ThrowHelper.ThrowInvalidOperationException_InvalidChainedResolver();
                }
            }

            protected override void OnCollectionModifying()
            {
                _options?.VerifyMutable();
            }

            protected override void OnCollectionModified()
            {
                // Collection modified by the user: replace the main
                // resolver with the resolver chain as our source of truth.
                _options?._typeInfoResolver = this;
            }
        }

        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        private static YamlSerializerOptions GetOrCreateSingleton(
            ref YamlSerializerOptions? location,
            YamlSerializerDefaults defaults)
        {
            var options = new YamlSerializerOptions(defaults)
            {
                // Because we're marking the default instance as read-only,
                // we need to specify a resolver instance for the case where
                // reflection is disabled by default: use one that returns null for all types.

                TypeInfoResolver = YamlSerializer.IsReflectionEnabledByDefault
                    ? DefaultYamlTypeInfoResolver.DefaultInstance
                    : YamlTypeInfoResolver.Empty,

                _isReadOnly = true,
            };

            return Interlocked.CompareExchange(ref location, options, null) ?? options;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"TypeInfoResolver = {(TypeInfoResolver?.ToString() ?? "<null>")}, IsReadOnly = {IsReadOnly}";
    }
}
