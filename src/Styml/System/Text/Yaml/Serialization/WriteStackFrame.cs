// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct WriteStackFrame
    {
        /// <summary>
        /// The enumerator for resumable collections.
        /// </summary>
        public IEnumerator? CollectionEnumerator;

        /// <summary>
        /// The async enumerator for resumable async enumerable collections.
        /// </summary>
        public object? AsyncEnumerator;

        /// <summary>
        /// The state of the async enumerator for the current stack frame.
        /// </summary>
        public AsyncEnumeratorState AsyncEnumeratorState;

        /// <summary>
        /// The original YamlPropertyInfo that is not changed. It contains all properties.
        /// </summary>
        /// <remarks>
        /// For objects, it is either the actual (real) YamlPropertyInfo or the <see cref="YamlTypeInfo.PropertyInfoForTypeInfo"/> for the class.
        /// For collections, it is the <see cref="YamlTypeInfo.PropertyInfoForTypeInfo"/> for the class and current element.
        /// </remarks>
        public YamlPropertyInfo? YamlPropertyInfo;

        /// <summary>
        /// Used when processing extension data dictionaries.
        /// </summary>
        public bool IsWritingExtensionDataProperty;

        /// <summary>
        /// The class (POCO or IEnumerable) that is being populated.
        /// </summary>
        public YamlTypeInfo YamlTypeInfo;

        /// <summary>
        /// Validation state for a class.
        /// </summary>
        public int OriginalDepth;

        // Class-level state for collections.
        public bool ProcessedStartToken;
        public bool ProcessedEndToken;

        /// <summary>
        /// Property or Element state.
        /// </summary>
        public StackFramePropertyState PropertyState;

        /// <summary>
        /// The enumerator index for resumable collections.
        /// </summary>
        public int EnumeratorIndex;

        // This is used for re-entry cases for exception handling.
        public string? YamlPropertyNameAsString;

        // Preserve Reference
        public MetadataPropertyName MetadataPropertyName;

        // Serialization state for the child value serialized by the current frame.
        public PolymorphicSerializationState PolymorphicSerializationState;
        // Holds the entered polymorphic type info and acts as an LRU cache for element/field serializations.
        public YamlTypeInfo? PolymorphicTypeInfo;

        // Whether to use custom number handling.
        public YamlNumberHandling? NumberHandling;

        public bool IsPushedReferenceForCycleDetection;

        public void EndCollectionElement()
        {
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        public void EndDictionaryEntry()
        {
            PropertyState = StackFramePropertyState.None;
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        public void EndProperty()
        {
            YamlPropertyInfo = null!;
            YamlPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        /// <summary>
        /// Returns the YamlTypeInfo instance for the nested value we are trying to access.
        /// </summary>
        public readonly YamlTypeInfo GetNestedYamlTypeInfo()
        {
            return PolymorphicSerializationState is PolymorphicSerializationState.PolymorphicReEntryStarted
                ? PolymorphicTypeInfo!
                : YamlPropertyInfo!.YamlTypeInfo;
        }

        /// <summary>
        /// Configures the next stack frame for a polymorphic converter.
        /// </summary>
        public YamlTypeInfo InitializePolymorphicReEntry(Type runtimeType, YamlSerializerOptions options)
        {
            Debug.Assert(PolymorphicSerializationState == PolymorphicSerializationState.None);

            // For perf, avoid the dictionary lookup in GetTypeInfoInternal() for every element of a collection
            // if the current element is the same type as the previous element.
            if (PolymorphicTypeInfo?.Type != runtimeType)
            {
                // To determine the contract for an object value:
                // 1. Find the YamlTypeInfo for the runtime type with fallback to the nearest ancestor, if not available.
                // 2. If the resolved type is deriving from a polymorphic type, use the contract of the polymorphic type instead.
                YamlTypeInfo typeInfo = options.GetTypeInfoInternal(runtimeType, fallBackToNearestAncestorType: true);
                PolymorphicTypeInfo = typeInfo.AncestorPolymorphicType ?? typeInfo;
            }

            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return PolymorphicTypeInfo;
        }

        /// <summary>
        /// Configures the next stack frame for a polymorphic converter.
        /// </summary>
        public YamlConverter InitializePolymorphicReEntry(YamlTypeInfo derivedYamlTypeInfo)
        {
            Debug.Assert(PolymorphicSerializationState is PolymorphicSerializationState.None or PolymorphicSerializationState.PolymorphicReEntryStarted);

            PolymorphicTypeInfo = derivedYamlTypeInfo;
            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return derivedYamlTypeInfo.Converter;
        }

        /// <summary>
        /// Configures the next frame for a continuation of a polymorphic converter.
        /// </summary>
        public YamlConverter ResumePolymorphicReEntry()
        {
            Debug.Assert(PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntrySuspended);
            Debug.Assert(PolymorphicTypeInfo is not null);
            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return PolymorphicTypeInfo.Converter;
        }

        /// <summary>
        /// Updates frame state after a polymorphic converter has returned.
        /// </summary>
        public void ExitPolymorphicConverter(bool success)
        {
            PolymorphicSerializationState = success ? PolymorphicSerializationState.None : PolymorphicSerializationState.PolymorphicReEntrySuspended;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string DebuggerDisplay => $"ConverterStrategy.{YamlTypeInfo?.Converter.ConverterStrategy}, {YamlTypeInfo?.Type.Name}";
    }
}
