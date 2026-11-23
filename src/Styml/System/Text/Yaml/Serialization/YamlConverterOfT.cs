// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Converters;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Converts an object or value to or from YAML.
    /// </summary>
    /// <typeparam name="T">The <see cref="System.Type"/> to convert.</typeparam>
    public abstract partial class YamlConverter<T> : YamlConverter
    {
        /// <summary>
        /// When overridden, constructs a new <see cref="YamlConverter{T}"/> instance.
        /// </summary>
        protected internal YamlConverter()
        {
            IsValueType = typeof(T).IsValueType;

            if (HandleNull)
            {
                HandleNullOnRead = true;
                HandleNullOnWrite = true;
            }
            else if (UsesDefaultHandleNull)
            {
                // If the type doesn't support null, allow the converter a chance to modify.
                // These semantics are backwards compatible with 3.0.
                HandleNullOnRead = default(T) is not null;

                // The framework handles null automatically on writes.
                HandleNullOnWrite = false;
            }
        }

        /// <summary>
        /// Determines whether the type can be converted.
        /// </summary>
        /// <remarks>
        /// The default implementation is to return True when <paramref name="typeToConvert"/> equals typeof(T).
        /// </remarks>
        /// <param name="typeToConvert"></param>
        /// <returns>True if the type can be converted, False otherwise.</returns>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(T);
        }

        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Value;

        internal sealed override YamlTypeInfo CreateYamlTypeInfo(YamlSerializerOptions options)
        {
            return new YamlTypeInfo<T>(this, options);
        }

        /// <summary>
        /// Indicates whether <see langword="null"/> should be passed to the converter on serialization,
        /// and whether <see cref="YamlTokenType.Null"/> should be passed on deserialization.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="true"/> for converters based on value types, and <see langword="false"/> for converters based on reference types.
        /// </remarks>
        public virtual bool HandleNull
        {
            get
            {
                UsesDefaultHandleNull = true;
                return false;
            }
        }

        // A converter that handles null itself overrides HandleNull to true, so it has HandleNullOnRead without
        // UsesDefaultHandleNull; the object converters override it to false.
        internal override bool ReadsNullAsDefault =>
            default(T) is not null && IsInternalConverter && (UsesDefaultHandleNull || !HandleNullOnRead) && !IsUntypedConverter;

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override void WriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            Write(writer, valueOfT, options);
        }

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override bool OnTryWriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, ref WriteStack state)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            return OnTryWrite(writer, valueOfT, options, ref state);
        }

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override void WriteAsPropertyNameAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            WriteAsPropertyName(writer, valueOfT, options);
        }

        internal sealed override void WriteAsPropertyNameCoreAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            WriteAsPropertyNameCore(writer, valueOfT, options, isWritingExtensionDataProperty);
        }

        internal sealed override void WriteNumberWithCustomHandlingAsObject(Utf8YamlWriter writer, object? value, YamlNumberHandling handling)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            WriteNumberWithCustomHandling(writer, valueOfT, handling);
        }

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override bool TryWriteAsObject(Utf8YamlWriter writer, object? value, YamlSerializerOptions options, ref WriteStack state)
        {
            T valueOfT = YamlSerializer.UnboxOnWrite<T>(value)!;
            return TryWrite(writer, valueOfT, options, ref state);
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryWrite(Utf8YamlWriter writer,
#nullable disable // T may or may not be nullable depending on the derived converter's HandleNull override.
            T value,
#nullable enable
            YamlSerializerOptions options,
            ref WriteStack state)
        {
            Write(writer, value, options);
            return true;
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out T? value)
        {
            value = Read(ref reader, typeToConvert, options);
            return true;
        }

        /// <summary>
        /// Read and convert the YAML to T.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>YamlException</cref> when the YAML is invalid.
        /// </remarks>
        /// <param name="reader">The <see cref="Utf8YamlReader"/> to read from.</param>
        /// <param name="typeToConvert">The <see cref="System.Type"/> being converted.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> being used.</param>
        /// <returns>The value that was converted.</returns>
        /// <remarks>
        /// <para>Note that the value of <seealso cref="HandleNull"/> determines if the converter handles null YAML tokens.</para>
        /// <para>
        /// The serializer resolves a YAML alias (<c>*name</c>) that is the whole value before it calls the converter, so
        /// the converter reads the anchored node's tokens. An alias inside the value reaches the converter as a
        /// <see cref="YamlTokenType.Alias"/> token (or a property name whose <see cref="Utf8YamlReader.KeyKind"/> is
        /// <see cref="YamlKeyKind.Alias"/>); to read it as the anchored node, call
        /// <see cref="YamlSerializer.Deserialize{TValue}(ref Utf8YamlReader, YamlSerializerOptions?)"/> on the reader at that
        /// token. That nested call shares the operation's anchors and its alias bound.
        /// </para>
        /// </remarks>
        public abstract T? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options);

        internal bool TryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out T? value, out bool isPopulatedValue)
        {
            // M5b: an anchor or an alias on the value. Only the flag test
            // is inline; the handling is in YamlAliasReader.
            YamlAnchorEntry? anchorEntry = null;
            if (reader.YamlHasNodeRef && !state.IsContinuation)
            {
                if (reader.TokenType == YamlTokenType.Alias)
                {
                    value = YamlAliasReader.ReadAlias(this, ref reader, typeToConvert, options, ref state);
                    isPopulatedValue = false;
                    return true;
                }

                if (ConverterStrategy != ConverterStrategy.Value)
                {
                    anchorEntry = YamlAliasReader.FindAnchored(ref reader);
                }
            }

            // For perf and converter simplicity, handle null here instead of forwarding to the converter.
            if (reader.TokenType == YamlTokenType.Null || (reader.IsLenientNull && !IsUntypedConverter))
            {
                if (!IsValueType && !reader.Tag.IsEmpty && !state.IsContinuation)
                {
                    // M5d: a tagged empty node at a site that reads tags is not a null when the tag selects a type.
                    YamlSerializer.CheckTaggedNull(options.GetTypeInfoInternal(typeToConvert), ref reader);
                }

                // AcceptLenientScalars: null into a non-nullable value type is its default, whether the
                // converter would throw itself (primitives) or this method would (structs).
                if (default(T) is not null && !state.IsContinuation && reader.AcceptsLenientScalars && ReadsNullAsDefault)
                {
                    value = default;
                    isPopulatedValue = false;
                    return true;
                }

                if (!HandleNullOnRead && !state.IsContinuation)
                {
                    if (default(T) is not null)
                    {
                        ThrowHelper.ThrowYamlException_DeserializeUnableToConvertValue(Type);
                    }

                    value = default;
                    isPopulatedValue = false;
                    return true;
                }
            }

            if (ConverterStrategy == ConverterStrategy.Value)
            {
                // A value converter should never be within a continuation.
                Debug.Assert(!state.IsContinuation);
#if !DEBUG
                // For performance, only perform validation on internal converters on debug builds.
                if (IsInternalConverter)
                {
                    if (state.Current.NumberHandling != null && IsInternalConverterForNumberType)
                    {
                        value = ReadNumberWithCustomHandling(ref reader, state.Current.NumberHandling.Value, options);
                    }
                    else
                    {
                        value = Read(ref reader, typeToConvert, options);
                    }
                }
                else
#endif
                {
                    YamlTokenType originalPropertyTokenType = reader.TokenType;
                    int originalPropertyDepth = reader.CurrentDepth;
                    long originalPropertyBytesConsumed = reader.BytesConsumed;

                    if (state.Current.NumberHandling is not null && IsInternalConverterForNumberType)
                    {
                        value = ReadNumberWithCustomHandling(ref reader, state.Current.NumberHandling.Value, options);
                    }
                    else
                    {
                        value = Read(ref reader, typeToConvert, options);
                    }

                    VerifyRead(
                        originalPropertyTokenType,
                        originalPropertyDepth,
                        originalPropertyBytesConsumed,
                        isValueConverter: true,
                        ref reader);
                }

                isPopulatedValue = false;
                return true;
            }

            Debug.Assert(IsInternalConverter);
            bool isContinuation = state.IsContinuation;
            bool success;

            if (
#if NET
                !typeof(T).IsValueType &&
#endif
                CanBePolymorphic)
            {
                // Special case object converters since they don't
                // require the expensive ReadStack.Push()/Pop() operations.
                Debug.Assert(this is ObjectConverter);
                success = OnTryRead(ref reader, typeToConvert, options, ref state, out value);
                Debug.Assert(success);
                if (anchorEntry is not null)
                {
                    // M5b: an object site keeps the anchored node's element, which its aliases at object sites return.
                    YamlAliasReader.StoreElement(anchorEntry, value);
                }
                isPopulatedValue = false;
                return true;
            }

            YamlPropertyInfo? propertyInfo = state.Current.YamlPropertyInfo;
            object? parentObj = state.Current.ReturnValue;

#if DEBUG
            // DEBUG: ensure push/pop operations preserve stack integrity
            YamlTypeInfo originalYamlTypeInfo = state.Current.YamlTypeInfo;
#endif
            state.Push();
            Debug.Assert(Type == state.Current.YamlTypeInfo.Type);

            if (!isContinuation)
            {
#if DEBUG
                // For performance, only perform token type validation of converters on debug builds.
                Debug.Assert(state.Current.OriginalTokenType == YamlTokenType.None);
                state.Current.OriginalTokenType = reader.TokenType;
#endif
                Debug.Assert(state.Current.OriginalDepth == 0);
                state.Current.OriginalDepth = reader.CurrentDepth;
            }

            if (parentObj is not null && propertyInfo is not null && !propertyInfo.IsForTypeInfo)
            {
                state.Current.HasParentObject = true;
            }

            if (anchorEntry is not null)
            {
                // The anchored node is open while it is read; a converter stores its instance at creation (M5b). The
                // open nodes are on the operation's table, by frame depth, so frames stay as they were.
                YamlAliasReader.OpenAnchored(ref reader, anchorEntry, state.FrameCount);
            }

            success = OnTryRead(ref reader, typeToConvert, options, ref state, out value);
            if (success && reader.YamlAnchorContext is not null)
            {
                YamlAliasReader.CompleteAnchored(this, ref reader, state.FrameCount, value);
            }
#if DEBUG
            if (success)
            {
                if (state.IsContinuation)
                {
                    // The resumable converter did not forward to the next converter that previously returned false.
                    ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                }

                VerifyRead(
                    state.Current.OriginalTokenType,
                    state.Current.OriginalDepth,
                    bytesConsumed: 0,
                    isValueConverter: false,
                    ref reader);

                // No need to clear state.Current.* since a stack pop will occur.
            }
#endif

            isPopulatedValue = state.Current.IsPopulating;
            state.Pop(success);
#if DEBUG
            Debug.Assert(ReferenceEquals(originalYamlTypeInfo, state.Current.YamlTypeInfo));
#endif
            return success;
        }

        internal sealed override bool OnTryReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out object? value)
        {
            bool success = OnTryRead(ref reader, typeToConvert, options, ref state, out T? typedValue);
            value = typedValue;
            return success;
        }

        internal sealed override bool TryReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out object? value)
        {
            bool success = TryRead(ref reader, typeToConvert, options, ref state, out T? typedValue, out _);
            value = typedValue;
            return success;
        }

        internal sealed override object? ReadAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            T? typedValue = Read(ref reader, typeToConvert, options);
            return typedValue;
        }

        internal sealed override object? ReadAsPropertyNameAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            T typedValue = ReadAsPropertyName(ref reader, typeToConvert, options);
            return typedValue;
        }

        internal sealed override object? ReadAsPropertyNameCoreAsObject(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            T typedValue = ReadAsPropertyNameCore(ref reader, typeToConvert, options);
            return typedValue;
        }

        internal sealed override object? ReadNumberWithCustomHandlingAsObject(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
        {
            T typedValue = ReadNumberWithCustomHandling(ref reader, handling, options);
            return typedValue;
        }

        /// <summary>
        /// Performance optimization.
        /// The 'in' modifier in 'TryWrite(in T Value)' causes boxing for Nullable{T}, so this helper avoids that.
        /// TODO: Remove this work-around once https://github.com/dotnet/runtime/issues/50915 is addressed.
        /// </summary>
        private static bool IsNull(T? value) => value is null;

        internal bool TryWrite(Utf8YamlWriter writer, in T? value, YamlSerializerOptions options, ref WriteStack state)
        {
            if (writer.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowYamlException_SerializerCycleDetected(options.EffectiveMaxDepth);
            }

            if (default(T) is null && !HandleNullOnWrite && IsNull(value))
            {
                // We do not pass null values to converters unless HandleNullOnWrite is true. Null values for properties were
                // already handled in GetMemberAndWriteYaml() so we don't need to check for the ignore conditions here.
                writer.WriteNullValue();
                return true;
            }

            if (ConverterStrategy == ConverterStrategy.Value)
            {
                Debug.Assert(!state.IsContinuation);

                int originalPropertyDepth = writer.CurrentDepth;

                if (state.Current.NumberHandling is not null && IsInternalConverterForNumberType)
                {
                    WriteNumberWithCustomHandling(writer, value, state.Current.NumberHandling.Value);
                }
                else
                {
                    Write(writer, value, options);
                }

                VerifyWrite(originalPropertyDepth, writer);
                return true;
            }

            Debug.Assert(IsInternalConverter);
            bool isContinuation = state.IsContinuation;
            bool success;

            if (
#if NET
                // Short-circuit the check against "is not null"; treated as a constant by recent versions of the JIT.
                !typeof(T).IsValueType &&
#else
                !IsValueType &&
#endif
                value is not null &&
                // Do not handle objects that have already been
                // handled by a polymorphic converter for a base type.
                state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted)
            {
                YamlTypeInfo yamlTypeInfo = state.PeekNestedYamlTypeInfo();
                Debug.Assert(yamlTypeInfo.Converter.Type == Type);

                bool canBePolymorphic = CanBePolymorphic || yamlTypeInfo.PolymorphicTypeResolver is not null;
                YamlConverter? polymorphicConverter = canBePolymorphic ?
                    ResolvePolymorphicConverter(value, yamlTypeInfo, options, ref state) :
                    null;

                if (!isContinuation && options.ReferenceHandlingStrategy != YamlKnownReferenceHandler.Unspecified &&
                    TryHandleSerializedObjectReference(writer, value, options, polymorphicConverter, ref state))
                {
                    // The reference handler wrote reference metadata, serialization complete.
                    return true;
                }

                if (polymorphicConverter is not null)
                {
                    if (!isContinuation && writer.IsBlockStyle)
                    {
                        // M5d: after an anchor, before the derived type's node.
                        YamlSerializer.WritePolymorphicTag(writer, value, polymorphicConverter, yamlTypeInfo, options, ref state);
                    }

                    success = polymorphicConverter.TryWriteAsObject(writer, value, options, ref state);
                    state.Current.ExitPolymorphicConverter(success);

                    if (success)
                    {
                        if (state.Current.IsPushedReferenceForCycleDetection)
                        {
                            state.ReferenceResolver.PopReferenceForCycleDetection();
                            state.Current.IsPushedReferenceForCycleDetection = false;
                        }
                    }

                    return success;
                }
            }

#if DEBUG
            // DEBUG: ensure push/pop operations preserve stack integrity
            YamlTypeInfo originalYamlTypeInfo = state.Current.YamlTypeInfo;
#endif
            state.Push();
            Debug.Assert(Type == state.Current.YamlTypeInfo.Type);

#if DEBUG
            // For performance, only perform validation on internal converters on debug builds.
            if (!isContinuation)
            {
                Debug.Assert(state.Current.OriginalDepth == 0);
                state.Current.OriginalDepth = writer.CurrentDepth;
            }
#endif
            success = OnTryWrite(writer, value, options, ref state);
#if DEBUG
            if (success)
            {
                VerifyWrite(state.Current.OriginalDepth, writer);
            }
#endif
            state.Pop(success);

            if (success && state.Current.IsPushedReferenceForCycleDetection)
            {
                state.ReferenceResolver.PopReferenceForCycleDetection();
                state.Current.IsPushedReferenceForCycleDetection = false;
            }
#if DEBUG
            Debug.Assert(ReferenceEquals(originalYamlTypeInfo, state.Current.YamlTypeInfo));
#endif
            return success;
        }

        internal bool TryWriteDataExtensionProperty(Utf8YamlWriter writer, T value, YamlSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(value is not null);

            if (!IsInternalConverter)
            {
                return TryWrite(writer, value, options, ref state);
            }

            YamlDictionaryConverter<T>? dictionaryConverter = this as YamlDictionaryConverter<T>
                ?? (this as YamlMetadataServicesConverter<T>)?.Converter as YamlDictionaryConverter<T>;

            if (dictionaryConverter is null)
            {
                // If not YamlDictionaryConverter<T> then we are YamlObject.
                // Avoid a type reference to YamlObject and its converter to support trimming.
                // The WriteExtensionDataValue virtual method is overridden by the YamlObject converter.
                Debug.Assert(Type == typeof(Nodes.YamlObject));
                WriteExtensionDataValue(writer, value!, options);

                return true;
            }

            if (writer.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowYamlException_SerializerCycleDetected(options.EffectiveMaxDepth);
            }

            bool isContinuation = state.IsContinuation;
            bool success;

            state.Push();

            if (!isContinuation)
            {
                Debug.Assert(state.Current.OriginalDepth == 0);
                state.Current.OriginalDepth = writer.CurrentDepth;
            }

            // Extension data properties change how dictionary key naming policies are applied.
            state.Current.IsWritingExtensionDataProperty = true;
            state.Current.YamlPropertyInfo = state.Current.YamlTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;

            success = options.MappingOrder == YamlMappingOrder.Sorted
                ? dictionaryConverter.OnWriteExtensionDataSorted(writer, value, options, ref state)
                : dictionaryConverter.OnWriteResume(writer, value, options, ref state);
            if (success)
            {
                VerifyWrite(state.Current.OriginalDepth, writer);
            }

            state.Pop(success);

            return success;
        }

        /// <summary>
        /// Used to support YamlObject as an extension property in a loosely-typed, trimmable manner.
        /// </summary>
        /// <remarks>
        /// Writes the extension data contents without wrapping object braces.
        /// </remarks>
        internal virtual void WriteExtensionDataValue(Utf8YamlWriter writer, T value, YamlSerializerOptions options)
        {
            Debug.Fail("Should not be reachable.");

            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public sealed override Type Type { get; } = typeof(T);

        internal void VerifyRead(YamlTokenType tokenType, int depth, long bytesConsumed, bool isValueConverter, ref Utf8YamlReader reader)
        {
            Debug.Assert(isValueConverter == (ConverterStrategy == ConverterStrategy.Value));

            switch (tokenType)
            {
                case YamlTokenType.StartArray:
                    if (reader.TokenType != YamlTokenType.EndArray)
                    {
                        ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                    }

                    break;

                case YamlTokenType.StartObject:
                    if (reader.TokenType != YamlTokenType.EndObject)
                    {
                        ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                    }

                    break;

                case YamlTokenType.None:
                    Debug.Assert(IsRootLevelMultiContentStreamingConverter);
                    break;

                default:
                    if (isValueConverter)
                    {
                        // A value converter should not make any reads.
                        if (reader.BytesConsumed != bytesConsumed)
                        {
                            ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                        }
                    }
                    else
                    {
                        // A non-value converter (object or collection) should always have Start and End tokens
                        // unless it is polymorphic, supports null value reads, or handles multiple token types.
                        // M7c: AcceptLenientScalars' NuLL is a string token read as null.
                        if (!CanBePolymorphic && !SupportsMultipleTokenTypes && !(HandleNullOnRead && (tokenType == YamlTokenType.Null || reader.IsLenientNull)))
                        {
                            ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
                        }
                    }

                    // Should not be possible to change token type.
                    Debug.Assert(reader.TokenType == tokenType);
                    break;
            }
        }

        internal void VerifyWrite(int originalDepth, Utf8YamlWriter writer)
        {
            if (originalDepth != writer.CurrentDepth)
            {
                ThrowHelper.ThrowYamlException_SerializationConverterWrite(this);
            }
        }

        /// <summary>
        /// Write the value as YAML.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>YamlException</cref> when the YAML
        /// cannot be created.
        /// </remarks>
        /// <param name="writer">The <see cref="Utf8YamlWriter"/> to write to.</param>
        /// <param name="value">The value to convert. Note that the value of <seealso cref="HandleNull"/> determines if the converter handles <see langword="null" /> values.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> being used.</param>
        public abstract void Write(
            Utf8YamlWriter writer,
#nullable disable // T may or may not be nullable depending on the derived converter's HandleNull override.
            T value,
#nullable restore
            YamlSerializerOptions options);

        /// <summary>
        /// Reads a dictionary key from a YAML property name.
        /// </summary>
        /// <param name="reader">The <see cref="Utf8YamlReader"/> to read from.</param>
        /// <param name="typeToConvert">The <see cref="System.Type"/> being converted.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> being used.</param>
        /// <returns>The value that was converted.</returns>
        /// <remarks>Method should be overridden in custom converters of types used in deserialized dictionary keys.</remarks>
        public virtual T ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            // .NET 5 backward compatibility: hardcode the default converter for primitive key serialization.
            YamlConverter<T>? fallbackConverter = GetFallbackConverterForPropertyNameSerialization(options);
            if (fallbackConverter is null)
            {
                ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type, this);
            }

            return fallbackConverter.ReadAsPropertyNameCore(ref reader, typeToConvert, options);
        }

        internal virtual T ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == YamlTokenType.PropertyName);

            long originalBytesConsumed = reader.BytesConsumed;
            T result = ReadAsPropertyName(ref reader, typeToConvert, options);
            if (reader.BytesConsumed != originalBytesConsumed)
            {
                ThrowHelper.ThrowYamlException_SerializationConverterRead(this);
            }

            return result;
        }

        /// <summary>
        /// Writes a dictionary key as a YAML property name.
        /// </summary>
        /// <param name="writer">The <see cref="Utf8YamlWriter"/> to write to.</param>
        /// <param name="value">The value to convert. Note that the value of <seealso cref="HandleNull"/> determines if the converter handles <see langword="null" /> values.</param>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> being used.</param>
        /// <remarks>Method should be overridden in custom converters of types used in serialized dictionary keys.</remarks>
        public virtual void WriteAsPropertyName(Utf8YamlWriter writer, [DisallowNull] T value, YamlSerializerOptions options)
        {
            // .NET 5 backward compatibility: hardcode the default converter for primitive key serialization.
            YamlConverter<T>? fallbackConverter = GetFallbackConverterForPropertyNameSerialization(options);
            if (fallbackConverter is null)
            {
                ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type, this);
            }

            fallbackConverter.WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        internal virtual void WriteAsPropertyNameCore(Utf8YamlWriter writer, [DisallowNull] T value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (isWritingExtensionDataProperty)
            {
                // Extension data is meant as mechanism to gather unused JSON properties;
                // do not apply any custom key conversions and hardcode the default behavior.
                Debug.Assert(!IsInternalConverter && Type == typeof(string));
                writer.WritePropertyName((string)(object)value!);
                return;
            }

            int originalDepth = writer.CurrentDepth;
            WriteAsPropertyName(writer, value, options);
            if (originalDepth != writer.CurrentDepth || writer.TokenType != YamlTokenType.PropertyName)
            {
                ThrowHelper.ThrowYamlException_SerializationConverterWrite(this);
            }
        }

        // .NET 5 backward compatibility: hardcode the default converter for primitive key serialization.
        private YamlConverter<T>? GetFallbackConverterForPropertyNameSerialization(YamlSerializerOptions options)
        {
            YamlConverter<T>? result = null;

            // For consistency do not return any default converters for options instances linked to a
            // YamlSerializerContext, even if the default converters might have been rooted.
            if (!IsInternalConverter && options.TypeInfoResolver is not YamlSerializerContext)
            {
                result = _fallbackConverterForPropertyNameSerialization;

                if (result is null && DefaultYamlTypeInfoResolver.TryGetDefaultSimpleConverter(Type, out YamlConverter? defaultConverter))
                {
                    Debug.Assert(defaultConverter != this);
                    _fallbackConverterForPropertyNameSerialization = result = (YamlConverter<T>)defaultConverter;
                }
            }

            return result;
        }

        private YamlConverter<T>? _fallbackConverterForPropertyNameSerialization;

        internal virtual T ReadNumberWithCustomHandling(ref Utf8YamlReader reader, YamlNumberHandling handling, YamlSerializerOptions options)
            => throw new InvalidOperationException();

        internal virtual void WriteNumberWithCustomHandling(Utf8YamlWriter writer, T? value, YamlNumberHandling handling)
            => throw new InvalidOperationException();
    }
}
