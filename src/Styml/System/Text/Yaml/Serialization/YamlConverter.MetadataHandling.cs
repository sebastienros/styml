// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization
{
    public partial class YamlConverter
    {
        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate derived converter.
        /// </summary>
        internal YamlConverter? ResolvePolymorphicConverter(YamlTypeInfo yamlTypeInfo, ref ReadStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(CanHaveMetadata);
            // M7c: a type resolved by a tag or a classifier (PolymorphicResolvedType) is consumed on the first entry, so a
            // suspended re-entry, as the caller's condition allows, has neither.
            Debug.Assert((state.Current.MetadataPropertyNames & MetadataPropertyName.Type) != 0 || state.PolymorphicResolvedType is not null
                || state.Current.PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntrySuspended);
            Debug.Assert(state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted);

            YamlConverter? polymorphicConverter = null;

            switch (state.Current.PolymorphicSerializationState)
            {
                case PolymorphicSerializationState.None:
                    Debug.Assert(!state.IsContinuation);

                    PolymorphicTypeResolver resolver = yamlTypeInfo.PolymorphicTypeResolver!;
                    YamlTypeInfo? resolvedType;

                    if (state.PolymorphicResolvedType is Type classifiedType)
                    {
                        // Type resolved by custom TypeClassifier, or by a tag (M5d) — resolve directly.
                        resolver.TryResolveDerivedYamlTypeInfo(classifiedType, out resolvedType);
                        state.PolymorphicResolvedType = null;
                        if (state.PolymorphicResolvedByTag)
                        {
                            state.PolymorphicResolvedByTag = false;
                            if (state.PolymorphicTypeDiscriminator is object discriminator)
                            {
                                // 5.4, A3: a tag and a $type that select different types are an error.
                                resolver.TryGetDerivedYamlTypeInfo(discriminator, out YamlTypeInfo? byDiscriminator);
                                if (byDiscriminator?.Type != classifiedType)
                                {
                                    ThrowHelper.ThrowYamlException(SR.Format(SR.YamlTagDisagreesWithDiscriminator, discriminator, classifiedType, byDiscriminator?.Type));
                                }
                            }
                        }
                    }
                    else
                    {
                        // Standard discriminator-based resolution.
                        Debug.Assert(state.PolymorphicTypeDiscriminator is not null);
                        Debug.Assert(resolver.UsesTypeDiscriminators);
                        resolver.TryGetDerivedYamlTypeInfo(state.PolymorphicTypeDiscriminator, out resolvedType);
                    }

                    if (resolvedType is not null)
                    {
                        Debug.Assert(Type!.IsAssignableFrom(resolvedType.Type));

                        polymorphicConverter = state.InitializePolymorphicReEntry(resolvedType);
                        if (!polymorphicConverter.CanHaveMetadata)
                        {
                            ThrowHelper.ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(resolvedType.Type);
                        }
                    }
                    else
                    {
                        state.Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryNotFound;
                    }

                    state.PolymorphicTypeDiscriminator = null;
                    break;

                case PolymorphicSerializationState.PolymorphicReEntrySuspended:
                    polymorphicConverter = state.ResumePolymorphicReEntry();
                    Debug.Assert(Type!.IsAssignableFrom(polymorphicConverter.Type));
                    break;

                case PolymorphicSerializationState.PolymorphicReEntryNotFound:
                    Debug.Assert(state.Current.PolymorphicYamlTypeInfo is null);
                    break;

                default:
                    Debug.Fail("Unexpected PolymorphicSerializationState.");
                    break;
            }

            return polymorphicConverter;
        }

        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate derived converter.
        /// </summary>
        internal YamlConverter? ResolvePolymorphicConverter(object value, YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(value is not null && Type!.IsAssignableFrom(value.GetType()));
            Debug.Assert(CanBePolymorphic || yamlTypeInfo.PolymorphicTypeResolver is not null);
            Debug.Assert(state.PolymorphicTypeDiscriminator is null);

            YamlConverter? polymorphicConverter = null;

            switch (state.Current.PolymorphicSerializationState)
            {
                case PolymorphicSerializationState.None:
                    Debug.Assert(!state.IsContinuation);

                    Type runtimeType = value.GetType();

                    if (CanBePolymorphic && runtimeType != Type)
                    {
                        Debug.Assert(Type == typeof(object));
                        yamlTypeInfo = state.Current.InitializePolymorphicReEntry(runtimeType, options);
                        polymorphicConverter = yamlTypeInfo.Converter;
                    }

                    if (yamlTypeInfo.PolymorphicTypeResolver is PolymorphicTypeResolver resolver)
                    {
                        Debug.Assert(yamlTypeInfo.Converter.CanHaveMetadata);

                        if (resolver.TryGetDerivedYamlTypeInfo(runtimeType, out YamlTypeInfo? derivedYamlTypeInfo, out object? typeDiscriminator))
                        {
                            polymorphicConverter = state.Current.InitializePolymorphicReEntry(derivedYamlTypeInfo);

                            if (typeDiscriminator is not null)
                            {
                                if (!polymorphicConverter.CanHaveMetadata)
                                {
                                    ThrowHelper.ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(derivedYamlTypeInfo.Type);
                                }

                                state.PolymorphicTypeDiscriminator = typeDiscriminator;
                                state.PolymorphicTypeResolver = resolver;
                            }
                        }
                    }

                    if (polymorphicConverter is null)
                    {
                        state.Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryNotFound;
                    }

                    break;

                case PolymorphicSerializationState.PolymorphicReEntrySuspended:
                    Debug.Assert(state.IsContinuation);
                    polymorphicConverter = state.Current.ResumePolymorphicReEntry();
                    Debug.Assert(Type.IsAssignableFrom(polymorphicConverter.Type));
                    break;

                case PolymorphicSerializationState.PolymorphicReEntryNotFound:
                    Debug.Assert(state.IsContinuation);
                    break;

                default:
                    Debug.Fail("Unexpected PolymorphicSerializationState.");
                    break;
            }

            return polymorphicConverter;
        }

        internal bool TryHandleSerializedObjectReference(Utf8YamlWriter writer, object value, YamlSerializerOptions options, YamlConverter? polymorphicConverter, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(value is not null);

            switch (options.ReferenceHandlingStrategy)
            {
                case YamlKnownReferenceHandler.IgnoreCycles:
                    ReferenceResolver resolver = state.ReferenceResolver;
                    if (resolver.ContainsReferenceForCycleDetection(value))
                    {
                        writer.WriteNullValue();

                        if (polymorphicConverter is not null)
                        {
                            // Clear out any polymorphic state.
                            state.PolymorphicTypeDiscriminator = null;
                            state.PolymorphicTypeResolver = null;
                        }
                        return true;
                    }

                    resolver.PushReferenceForCycleDetection(value);
                    // WriteStack reuses root-level stack frames for its children as a performance optimization;
                    // we want to avoid writing any data for the root-level object to avoid corrupting the stack.
                    // This is fine since popping the root object at the end of serialization is not essential.
                    state.Current.IsPushedReferenceForCycleDetection = state.CurrentDepth > 0;
                    break;

                case YamlKnownReferenceHandler.Preserve:
                    bool canHaveIdMetadata = polymorphicConverter?.CanHaveMetadata ?? CanHaveMetadata;
                    if (canHaveIdMetadata)
                    {
                        // M5b: the built-in handlers write YAML anchors and aliases in block style; a custom handler, and
                        // the JSON-compatible style, keep $id/$ref.
                        if (options.ReferenceHandler!.AnchorMode != YamlAnchorMode.None && writer.IsBlockStyle)
                        {
                            return YamlAnchorWriter.TryWriteAliasOrAnchor(value, state.PeekNestedYamlTypeInfoForAnchors(), writer, options, ref state);
                        }

                        if (YamlSerializer.TryGetReferenceForValue(value, ref state, writer))
                        {
                            // We found a repeating reference and wrote the relevant metadata; serialization complete.
                            return true;
                        }
                    }
                    break;

                default:
                    Debug.Fail("Unexpected ReferenceHandlingStrategy.");
                    break;
            }

            return false;
        }
    }
}
