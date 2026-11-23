// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct ReadStackFrame
    {
        // Current property values.
        public YamlPropertyInfo? YamlPropertyInfo;
        public StackFramePropertyState PropertyState;
        public bool UseExtensionProperty;

        // Support JSON Path on exceptions and non-string Dictionary keys.
        // This is Utf8 since we don't want to convert to string until an exception is thrown.
        // For dictionary keys we don't want to convert to TKey until we have both key and value when parsing the dictionary elements on stream cases.
        public byte[]? YamlPropertyName;
        public string? YamlPropertyNameAsString; // This is used for string dictionary keys and re-entry cases that specify a property name.

        // Stores the non-string dictionary keys for continuation.
        public object? DictionaryKey;

        /// <summary>
        /// Records the Utf8YamlReader Depth at the start of the current value.
        /// </summary>
        public int OriginalDepth;
#if DEBUG
        /// <summary>
        /// Records the Utf8YamlReader TokenType at the start of the current value.
        /// Only used to validate debug builds.
        /// </summary>
        public YamlTokenType OriginalTokenType;
#endif

        // Current object (POCO or IEnumerable).
        public object? ReturnValue; // The current return value used for re-entry.
        public YamlTypeInfo YamlTypeInfo;
        public StackFrameObjectState ObjectState; // State tracking the current object.

        // Current object can contain metadata
        public bool CanContainMetadata;
        public MetadataPropertyName LatestMetadataPropertyName;
        public MetadataPropertyName MetadataPropertyNames;

        // Serialization state for value serialized by the current frame.
        public PolymorphicSerializationState PolymorphicSerializationState;

        // Holds any entered polymorphic YamlTypeInfo metadata.
        public YamlTypeInfo? PolymorphicYamlTypeInfo;

        // Gets the initial YamlTypeInfo metadata used when deserializing the current value.
        public YamlTypeInfo BaseYamlTypeInfo
            => PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntryStarted
                ? PolymorphicYamlTypeInfo!
                : YamlTypeInfo;

        // For performance, we order the properties by the first deserialize and PropertyIndex helps find the right slot quicker.
        public int PropertyIndex;

        // Tracks newly encounentered UTF-8 encoded properties during the current deserialization, to be appended to the cache.
        public PropertyRefCacheBuilder? PropertyRefCacheBuilder;

        // Holds relevant state when deserializing objects with parameterized constructors.
        public ArgumentState? CtorArgumentState;

        // Whether to use custom number handling.
        public YamlNumberHandling? NumberHandling;

        // Represents known (non-extension) properties which have value assigned.
        // Each bit corresponds to a property.
        // False means that property is not set (not yet occurred in the payload).
        // Length of the BitArray is equal to number of non-extension properties.
        // Every YamlPropertyInfo has PropertyIndex property which maps to an index in this BitArray.
        public BitArray? AssignedProperties;

        // M5c: the members read so far, one bit per PropertyIndex, when the
        // duplicate-key handling is Error or FirstWins and the type has at most 64 properties and no required ones; above
        // that, or with required members, AssignedProperties holds them. A mask allocates nothing per object.
        public ulong AssignedMask;

        // Whether AssignedMask tracks the current object: set by InitializePropertiesValidationState, cleared by
        // ValidateAllRequiredPropertiesAreRead as AssignedProperties is (the constructor converter replays its buffered
        // members after that, and they were marked when first read).
        public bool TracksAssignedMask;

        // Tracks state related to property population.
        public bool HasParentObject;
        public bool IsPopulating;

        public void EndConstructorParameter()
        {
            CtorArgumentState!.YamlParameterInfo = null;
            YamlPropertyName = null;
            PropertyState = StackFramePropertyState.None;
        }

        public void EndProperty()
        {
            YamlPropertyInfo = null!;
            YamlPropertyName = null;
            YamlPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;

            // No need to clear these since they are overwritten each time:
            //  NumberHandling
            //  UseExtensionProperty
        }

        public void EndElement()
        {
            YamlPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
        }

        /// <summary>
        /// Is the current object a Dictionary.
        /// </summary>
        public bool IsProcessingDictionary() => YamlTypeInfo.Kind is YamlTypeInfoKind.Dictionary;

        /// <summary>
        /// Is the current object an Enumerable.
        /// </summary>
        public bool IsProcessingEnumerable() => YamlTypeInfo.Kind is YamlTypeInfoKind.Enumerable;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkPropertyAsRead(YamlPropertyInfo propertyInfo)
        {
            if (AssignedProperties is { })
            {
                if (propertyInfo.Options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                {
                    if (AssignedProperties[propertyInfo.PropertyIndex])
                    {
                        ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(propertyInfo);
                    }
                }

                AssignedProperties[propertyInfo.PropertyIndex] = true;
            }
            else if (TracksAssignedMask)
            {
                Debug.Assert(propertyInfo.PropertyIndex < 64);
                ulong bit = 1UL << propertyInfo.PropertyIndex;
                if ((AssignedMask & bit) != 0 && propertyInfo.Options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                {
                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(propertyInfo);
                }

                AssignedMask |= bit;
            }
        }

        /// <summary>Whether the member was read before in the current object (FirstWins: a later occurrence is skipped).</summary>
        internal readonly bool IsPropertyAssigned(YamlPropertyInfo propertyInfo) =>
            AssignedProperties is { } assigned
                ? assigned[propertyInfo.PropertyIndex]
                : TracksAssignedMask && (AssignedMask & (1UL << propertyInfo.PropertyIndex)) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializePropertiesValidationState(YamlTypeInfo typeInfo)
        {
            Debug.Assert(AssignedProperties is null);

            AssignedMask = 0;
            bool tracksDuplicates = typeInfo.Options.DuplicateKeyHandling != YamlDuplicateKeyHandling.LastWins;
            if (typeInfo.ShouldTrackRequiredProperties || (tracksDuplicates && typeInfo.Properties.Count > 64))
            {
                // This may be slightly larger than required (e.g. if there's an extension property)
                AssignedProperties = new BitArray(typeInfo.Properties.Count);
                TracksAssignedMask = false;
            }
            else
            {
                TracksAssignedMask = tracksDuplicates;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ValidateAllRequiredPropertiesAreRead(YamlTypeInfo typeInfo)
        {
            if (typeInfo.ShouldTrackRequiredProperties)
            {
                Debug.Assert(AssignedProperties is not null);
                Debug.Assert(typeInfo.OptionalPropertiesMask is not null);

                // All properties must be either assigned or optional
                BitArray assignedOrNotRequiredPropertiesSet = AssignedProperties.Or(typeInfo.OptionalPropertiesMask);

                if (!assignedOrNotRequiredPropertiesSet.HasAllSet())
                {
                    ThrowHelper.ThrowYamlException_YamlRequiredPropertyMissing(typeInfo, assignedOrNotRequiredPropertiesSet);
                }
            }

            AssignedProperties = null;
            TracksAssignedMask = false;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ConverterStrategy.{YamlTypeInfo?.Converter.ConverterStrategy}, {YamlTypeInfo?.Type.Name}";
    }
}
