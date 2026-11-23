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
    internal struct ReadStack
    {
        /// <summary>The absolute position, in a stream's input, of the current buffer's first byte (M5b anchor table).</summary>
        internal long StreamPosition;

        // M5c: the document's keys of a collection that held entries before the
        // read (a populated or pre-instantiated dictionary, seeded extension data), by frame depth, under Error or
        // FirstWins. A duplicate is a property of the document: a key that appears once overrides what the target held.
        // Allocated only when such a collection is met; an empty one keeps the allocation-free path.
        private SeededKeys[]? _seededKeys;

        private struct SeededKeys
        {
            internal object? Target;
            internal object? Keys;
        }

        /// <summary>
        /// The document's keys read so far into the target at the current depth, or null when the target held nothing
        /// when this read first met it (its own entries are then the document's). A reset starts a new read of a
        /// collection; otherwise the entry is kept while the target is the same instance, and the count at the start is
        /// taken only when the target is met for the first time.
        /// </summary>
        private (object? Target, Dictionary<string, object>? Keys)[]? _complexObjectKeys;

        /// <summary>
        /// M5e: the collection keys an <c>object</c>-keyed dictionary at this depth has met, by canonical form;
        /// a new dictionary (another <paramref name="target"/>) starts empty. Allocated only when such a key is met.
        /// </summary>
        internal Dictionary<string, object> ComplexObjectKeys(object? target)
        {
            int depth = _count;
            if (_complexObjectKeys is null)
            {
                _complexObjectKeys = new (object?, Dictionary<string, object>?)[Math.Max(4, depth + 1)];
            }
            else if (depth >= _complexObjectKeys.Length)
            {
                Array.Resize(ref _complexObjectKeys, Math.Max(_complexObjectKeys.Length * 2, depth + 1));
            }

            ref (object? Target, Dictionary<string, object>? Keys) entry = ref _complexObjectKeys[depth];
            if (entry.Keys is null || !ReferenceEquals(entry.Target, target))
            {
                entry = (target, new Dictionary<string, object>(StringComparer.Ordinal));
            }

            return entry.Keys!;
        }

        internal HashSet<TKey>? SeededDocumentKeys<TKey>(object target, int countAtStart, bool reset, IEqualityComparer<TKey>? comparer = null, bool record = false)
        {
            int depth = _count;
            if (_seededKeys is null)
            {
                // A collection read from its start (reset) needs no entry when empty; extension data, met key by key,
                // records its first meeting so that its own entries are not later taken for seeds.
                if (countAtStart == 0 && !record)
                {
                    return null;
                }

                _seededKeys = new SeededKeys[Math.Max(4, depth + 1)];
            }
            else if (depth >= _seededKeys.Length)
            {
                Array.Resize(ref _seededKeys, Math.Max(_seededKeys.Length * 2, depth + 1));
            }

            ref SeededKeys entry = ref _seededKeys[depth];
            if (reset || !ReferenceEquals(entry.Target, target))
            {
                entry.Target = target;
                entry.Keys = countAtStart > 0 ? new HashSet<TKey>(comparer) : null;
            }

            return entry.Keys as HashSet<TKey>;
        }

        /// <summary>
        /// Exposes the stack frame that is currently active.
        /// </summary>
        public ReadStackFrame Current;

        /// <summary>
        /// Gets the parent stack frame, if it exists.
        /// </summary>
        public readonly ref ReadStackFrame Parent
        {
            get
            {
                Debug.Assert(_count > 1);
                Debug.Assert(_stack is not null);
                return ref _stack[_count - 2];
            }
        }

        public readonly YamlPropertyInfo? ParentProperty
            => Current.HasParentObject ? Parent.YamlPropertyInfo : null;

        /// <summary>
        /// Buffer containing all frames in the stack. For performance it is only populated for serialization depths > 1.
        /// </summary>
        private ReadStackFrame[] _stack;

        /// <summary>
        /// Tracks the current depth of the stack.
        /// </summary>
        private int _count;

        /// <summary>The number of frames pushed (M5b: the key of the anchored node a frame reads).</summary>
        internal readonly int FrameCount => _count;

        /// <summary>
        /// If not zero, indicates that the stack is part of a re-entrant continuation of given depth.
        /// </summary>
        private int _continuationCount;

        /// <summary>
        /// Indicates that the state still contains suspended frames waiting re-entry.
        /// </summary>
        public readonly bool IsContinuation => _continuationCount != 0;

        // The bag of preservable references.
        public ReferenceResolver ReferenceResolver;

        /// <summary>
        /// Whether we need to read ahead in the inner read loop.
        /// </summary>
        public bool SupportContinuation;

        /// <summary>
        /// Holds the value of $id or $ref of the currently read object
        /// </summary>
        public string? ReferenceId;

        /// <summary>
        /// Holds the value of $type of the currently read object
        /// </summary>
        public object? PolymorphicTypeDiscriminator;

        /// <summary>
        /// Holds the resolved Type when a custom type classifier is used
        /// instead of discriminator-based resolution.
        /// </summary>
        public Type? PolymorphicResolvedType;

        /// <summary>M5d: the resolved type came from a tag, so a <c>$type</c> must agree with it.</summary>
        public bool PolymorphicResolvedByTag;

        /// <summary>M5e: a collection key met at a member site, for a YamlObject extension member to hold.</summary>
        public Nodes.YamlNode? PendingComplexKey;

        /// <summary>
        /// Global flag indicating whether we can read preserved references.
        /// </summary>
        public bool PreserveReferences;

        /// <summary>
        /// Ensures that the stack buffer has sufficient capacity to hold an additional frame.
        /// </summary>
        private void EnsurePushCapacity()
        {
            if (_stack is null)
            {
                _stack = new ReadStackFrame[4];
            }
            else if (_count - 1 == _stack.Length)
            {
                Array.Resize(ref _stack, 2 * _stack.Length);
            }
        }

        internal void Initialize(YamlTypeInfo yamlTypeInfo, bool supportContinuation = false)
        {
            YamlSerializerOptions options = yamlTypeInfo.Options;
            if (options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve)
            {
                ReferenceResolver = options.ReferenceHandler!.CreateResolver(writing: false);
                PreserveReferences = true;
            }

            Current.YamlTypeInfo = yamlTypeInfo;
            Current.YamlPropertyInfo = yamlTypeInfo.PropertyInfoForTypeInfo;
            Current.NumberHandling = Current.YamlPropertyInfo.EffectiveNumberHandling;
            Current.CanContainMetadata = PreserveReferences || yamlTypeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators == true
                || (yamlTypeInfo.PolymorphicTypeResolver is not null && yamlTypeInfo.TypeClassifier is not null);
            SupportContinuation = supportContinuation;
        }

        public void Push()
        {
            if (_continuationCount == 0)
            {
                if (_count == 0)
                {
                    // Performance optimization: reuse the first stack frame on the first push operation.
                    // NB need to be careful when making writes to Current _before_ the first `Push`
                    // operation is performed.
                    _count = 1;
                }
                else
                {
                    YamlTypeInfo yamlTypeInfo = Current.YamlPropertyInfo?.YamlTypeInfo ?? Current.CtorArgumentState!.YamlParameterInfo!.YamlTypeInfo;
                    YamlNumberHandling? numberHandling = Current.NumberHandling;

                    EnsurePushCapacity();
                    _stack[_count - 1] = Current;
                    Current = default;
                    _count++;

                    Current.YamlTypeInfo = yamlTypeInfo;
                    Current.YamlPropertyInfo = yamlTypeInfo.PropertyInfoForTypeInfo;
                    // Allow number handling on property to win over handling on type.
                    Current.NumberHandling = numberHandling ?? Current.YamlPropertyInfo.EffectiveNumberHandling;
                    Current.CanContainMetadata = PreserveReferences || yamlTypeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators == true
                        || (yamlTypeInfo.PolymorphicTypeResolver is not null && yamlTypeInfo.TypeClassifier is not null);
                }
            }
            else
            {
                // We are re-entering a continuation, adjust indices accordingly.

                if (_count++ > 0)
                {
                    _stack[_count - 2] = Current;
                    Current = _stack[_count - 1];
                }

                // check if we are done
                if (_continuationCount == _count)
                {
                    _continuationCount = 0;
                }
            }

            SetConstructorArgumentState();
#if DEBUG
            // Ensure the method is always exercised in debug builds.
            _ = YamlPath();
#endif
        }

        public void Pop(bool success)
        {
            Debug.Assert(_count > 0);
            Debug.Assert(YamlPath() is not null);

            if (!success)
            {
                // Check if we need to initialize the continuation.
                if (_continuationCount == 0)
                {
                    if (_count == 1)
                    {
                        // No need to copy any frames here.
                        _continuationCount = 1;
                        _count = 0;
                        return;
                    }

                    // Need to push the Current frame to the stack,
                    // ensure that we have sufficient capacity.
                    EnsurePushCapacity();
                    _continuationCount = _count--;
                }
                else if (--_count == 0)
                {
                    // reached the root, no need to copy frames.
                    return;
                }

                _stack[_count] = Current;
                Current = _stack[_count - 1];
            }
            else
            {
                Debug.Assert(_continuationCount == 0);

                if (--_count > 0)
                {
                    Current = _stack[_count - 1];
                }
            }
        }

        /// <summary>
        /// Configures the current stack frame for a polymorphic converter.
        /// </summary>
        public YamlConverter InitializePolymorphicReEntry(YamlTypeInfo derivedYamlTypeInfo)
        {
            Debug.Assert(!IsContinuation);
            Debug.Assert(Current.PolymorphicYamlTypeInfo is null);
            Debug.Assert(Current.PolymorphicSerializationState == PolymorphicSerializationState.None);

            Current.PolymorphicYamlTypeInfo = Current.YamlTypeInfo;
            Current.YamlTypeInfo = derivedYamlTypeInfo;
            Current.YamlPropertyInfo = derivedYamlTypeInfo.PropertyInfoForTypeInfo;
            Current.NumberHandling ??= Current.YamlPropertyInfo.NumberHandling;
            Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            SetConstructorArgumentState();

            return derivedYamlTypeInfo.Converter;
        }


        /// <summary>
        /// Configures the current frame for a continuation of a polymorphic converter.
        /// </summary>
        public YamlConverter ResumePolymorphicReEntry()
        {
            Debug.Assert(Current.PolymorphicYamlTypeInfo is not null);
            Debug.Assert(Current.PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntrySuspended);

            // Swap out the two values as we resume the polymorphic converter
            (Current.YamlTypeInfo, Current.PolymorphicYamlTypeInfo) = (Current.PolymorphicYamlTypeInfo, Current.YamlTypeInfo);
            Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return Current.YamlTypeInfo.Converter;
        }

        /// <summary>
        /// Updates frame state after a polymorphic converter has returned.
        /// </summary>
        public void ExitPolymorphicConverter(bool success)
        {
            Debug.Assert(Current.PolymorphicYamlTypeInfo is not null);
            Debug.Assert(Current.PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntryStarted);

            // Swap out the two values as we exit the polymorphic converter
            (Current.YamlTypeInfo, Current.PolymorphicYamlTypeInfo) = (Current.PolymorphicYamlTypeInfo, Current.YamlTypeInfo);
            Current.PolymorphicSerializationState = success ? PolymorphicSerializationState.None : PolymorphicSerializationState.PolymorphicReEntrySuspended;
        }

        // Return a JSONPath using simple dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y[0].z
        // $['PropertyName.With.Special.Chars']
        public string YamlPath()
        {
            StringBuilder sb = new StringBuilder("$");

            (int frameCount, bool includeCurrentFrame) = _continuationCount switch
            {
                0 => (_count - 1, true), // Not a continuation, report previous frames and Current.
                1 => (0, true), // Continuation of depth 1, just report Current frame.
                int c => (c, false) // Continuation of depth > 1, report the entire stack.
            };

            for (int i = 0; i < frameCount; i++)
            {
                AppendStackFrame(sb, ref _stack[i]);
            }

            if (includeCurrentFrame)
            {
                AppendStackFrame(sb, ref Current);
            }

            return sb.ToString();

            static void AppendStackFrame(StringBuilder sb, ref ReadStackFrame frame)
            {
                // Append the property name.
                string? propertyName = GetPropertyName(ref frame);
                AppendPropertyName(sb, propertyName);

                if (frame.YamlTypeInfo is not null && frame.IsProcessingEnumerable())
                {
                    if (frame.ReturnValue is not IEnumerable enumerable)
                    {
                        return;
                    }

                    // For continuation scenarios only, before or after all elements are read, the exception is not within the array.
                    if (frame.ObjectState == StackFrameObjectState.None ||
                        frame.ObjectState == StackFrameObjectState.CreatedObject ||
                        frame.ObjectState == StackFrameObjectState.ReadElements)
                    {
                        sb.Append('[');
                        sb.Append(GetCount(enumerable));
                        sb.Append(']');
                    }
                }
            }

            static int GetCount(IEnumerable enumerable)
            {
                if (enumerable is ICollection collection)
                {
                    return collection.Count;
                }

                int count = 0;
                IEnumerator enumerator = enumerable.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    count++;
                }

                return count;
            }

            static void AppendPropertyName(StringBuilder sb, string? propertyName)
            {
                if (propertyName is not null)
                {
                    if (propertyName.AsSpan().ContainsSpecialCharacters())
                    {
                        sb.Append(@"['");
                        sb.AppendEscapedPropertyName(propertyName);
                        sb.Append(@"']");
                    }
                    else
                    {
                        sb.Append('.');
                        sb.Append(propertyName);
                    }
                }
            }

            static string? GetPropertyName(ref ReadStackFrame frame)
            {
                string? propertyName = null;

                // Attempt to get the JSON property name from the frame.
                byte[]? utf8PropertyName = frame.YamlPropertyName;
                if (utf8PropertyName is null)
                {
                    if (frame.YamlPropertyNameAsString is not null)
                    {
                        // Attempt to get the JSON property name set manually for dictionary
                        // keys and KeyValuePair property names.
                        propertyName = frame.YamlPropertyNameAsString;
                    }
                    else
                    {
                        // Attempt to get the JSON property name from the YamlPropertyInfo or YamlParameterInfo.
                        utf8PropertyName = frame.YamlPropertyInfo?.NameAsUtf8Bytes ??
                            frame.CtorArgumentState?.YamlParameterInfo?.YamlNameAsUtf8Bytes;
                    }
                }

                if (utf8PropertyName is not null)
                {
                    propertyName = Encoding.UTF8.GetString(utf8PropertyName);
                }

                return propertyName;
            }
        }

        // Traverses the stack for the outermost object being deserialized using constructor parameters
        // Only called when calculating exception information.
        public YamlTypeInfo GetTopYamlTypeInfoWithParameterizedConstructor()
        {
            Debug.Assert(!IsContinuation);

            for (int i = 0; i < _count - 1; i++)
            {
                if (_stack[i].YamlTypeInfo.UsesParameterizedConstructor)
                {
                    return _stack[i].YamlTypeInfo;
                }
            }

            Debug.Assert(Current.YamlTypeInfo.UsesParameterizedConstructor);
            return Current.YamlTypeInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetConstructorArgumentState()
        {
            if (Current.YamlTypeInfo.UsesParameterizedConstructor)
            {
                Current.CtorArgumentState ??= new();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Path = {YamlPath()}, Current = ConverterStrategy.{Current.YamlTypeInfo?.Converter.ConverterStrategy}, {Current.YamlTypeInfo?.Type.Name}";
    }
}
