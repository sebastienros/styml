// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Text.Yaml.Serialization.Converters;
using System.Threading;

namespace System.Text.Yaml.Nodes
{
    public partial class YamlObject : IDictionary<string, YamlNode?>
    {
        private OrderedDictionary<string, YamlNode?>? _dictionary;

        /// <summary>
        ///   Adds an element with the provided property name and value to the <see cref="YamlObject"/>.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/>is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   An element with the same property name already exists in the <see cref="YamlObject"/>.
        /// </exception>
        public void Add(string propertyName, YamlNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            Dictionary.Add(propertyName, value);
            value?.AssignParent(this);
        }

        /// <summary>
        ///   Adds an element with the provided name and value to the <see cref="YamlObject"/>, if a property named <paramref name="propertyName"/> doesn't already exist.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        /// <returns>
        ///   <see langword="true"/> if the property didn't exist and the element was added; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAdd(string propertyName, YamlNode? value) => TryAdd(propertyName, value, out _);

        /// <summary>
        ///   Adds an element with the provided name and value to the <see cref="YamlObject"/>, if a property named <paramref name="propertyName"/> doesn't already exist.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <param name="index">The index of the added or existing <paramref name="propertyName"/>. This is always a valid index into the <see cref="YamlObject"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        /// <returns>
        ///   <see langword="true"/> if the property didn't exist and the element was added; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAdd(string propertyName, YamlNode? value, out int index)
        {
            if (propertyName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(propertyName));
            }
#if NET9_0
            bool success = Dictionary.TryAdd(propertyName, value);
            index = success ? Dictionary.Count - 1 : Dictionary.IndexOf(propertyName);
#else
            bool success = Dictionary.TryAdd(propertyName, value, out index);
#endif
            if (success)
            {
                value?.AssignParent(this);
            }
            return success;
        }

        /// <summary>
        ///   Adds the specified property to the <see cref="YamlObject"/>.
        /// </summary>
        /// <param name="property">
        ///   The KeyValuePair structure representing the property name and value to add to the <see cref="YamlObject"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   An element with the same property name already exists in the <see cref="YamlObject"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   The property name of <paramref name="property"/> is <see langword="null"/>.
        /// </exception>
        public void Add(KeyValuePair<string, YamlNode?> property) => Add(property.Key, property.Value);

        /// <summary>
        ///   Removes all elements from the <see cref="YamlObject"/>.
        /// </summary>
        public void Clear()
        {
            OrderedDictionary<string, YamlNode?>? dictionary = _dictionary;

            if (dictionary is null)
            {
                if (_yamlElement is { } element)
                {
                    CaptureCollectionStyle(element);
                    CaptureCollectionComments(element);
                }

                _yamlElement = null;
                return;
            }

            foreach (YamlNode? node in dictionary.Values)
            {
                DetachParent(node);
            }

            dictionary.Clear();
            SlotCommentMap = null;

            // M7b: the collection-key entries go too.
            if (_complexKeys is { } complexKeys)
            {
                foreach (ComplexKeyEntry entry in complexKeys)
                {
                    DetachParent(entry.Key);
                    DetachParent(entry.Value);
                }

                _complexKeys = null;
            }
        }

        /// <summary>
        ///   Determines whether the <see cref="YamlObject"/> contains an element with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name to locate in the <see cref="YamlObject"/>.</param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="YamlObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        public bool ContainsKey(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return Dictionary.ContainsKey(propertyName);
        }

        /// <summary>
        ///   Gets the number of elements contained in <see cref="YamlObject"/>.
        /// </summary>
        public int Count => Dictionary.Count;

        /// <summary>
        ///   Removes the element with the specified property name from the <see cref="YamlObject"/>.
        /// </summary>
        /// <param name="propertyName">The property name of the element to remove.</param>
        /// <returns>
        ///   <see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        public bool Remove(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            // M7b: the collection-key entries after the removed entry move up with the string entries (the dictionary
            // first, which reads a parsed object's collection keys).
            OrderedDictionary<string, YamlNode?> dictionary = Dictionary;
            int index = _complexKeys is { Count: > 0 } ? dictionary.IndexOf(propertyName) : -1;
            bool success = dictionary.Remove(propertyName, out YamlNode? removedNode);
            if (success)
            {
                SlotCommentMap?.Remove(propertyName);
                DetachParent(removedNode);
                if (index >= 0)
                {
                    ShiftCollectionKeys(index, -1);
                }
            }

            return success;
        }

        /// <summary>
        ///   Determines whether the <see cref="YamlObject"/> contains a specific property name and <see cref="YamlNode"/> reference.
        /// </summary>
        /// <param name="item">The element to locate in the <see cref="YamlObject"/>.</param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="YamlObject"/> contains an element with the property name; otherwise, <see langword="false"/>.
        /// </returns>
        bool ICollection<KeyValuePair<string, YamlNode?>>.Contains(KeyValuePair<string, YamlNode?> item) =>
            ((IDictionary<string, YamlNode?>)Dictionary).Contains(item);

        /// <summary>
        ///   Copies the elements of the <see cref="YamlObject"/> to an array of type KeyValuePair starting at the specified array index.
        /// </summary>
        /// <param name="array">
        ///   The one-dimensional Array that is the destination of the elements copied from <see cref="YamlObject"/>.
        /// </param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The number of elements in the source ICollection is greater than the available space from <paramref name="index"/>
        ///   to the end of the destination <paramref name="array"/>.
        /// </exception>
        void ICollection<KeyValuePair<string, YamlNode?>>.CopyTo(KeyValuePair<string, YamlNode?>[] array, int index) =>
            ((IDictionary<string, YamlNode?>)Dictionary).CopyTo(array, index);

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="YamlObject"/>.
        /// </summary>
        /// <returns>
        ///   An enumerator that iterates through the <see cref="YamlObject"/>.
        /// </returns>
        public IEnumerator<KeyValuePair<string, YamlNode?>> GetEnumerator() => Dictionary.GetEnumerator();

        /// <summary>
        ///   Removes a key and value from the <see cref="YamlObject"/>.
        /// </summary>
        /// <param name="item">
        ///   The KeyValuePair structure representing the property name and value to remove from the <see cref="YamlObject"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.
        /// </returns>
        bool ICollection<KeyValuePair<string, YamlNode?>>.Remove(KeyValuePair<string, YamlNode?> item) => Remove(item.Key);

        /// <summary>
        ///   Gets a collection containing the property names in the <see cref="YamlObject"/>.
        /// </summary>
        ICollection<string> IDictionary<string, YamlNode?>.Keys => Dictionary.Keys;

        /// <summary>
        ///   Gets a collection containing the property values in the <see cref="YamlObject"/>.
        /// </summary>
        ICollection<YamlNode?> IDictionary<string, YamlNode?>.Values => Dictionary.Values;

        /// <summary>
        ///   Gets the value associated with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name of the value to get.</param>
        /// <param name="yamlNode">
        ///   When this method returns, contains the value associated with the specified property name, if the property name is found;
        ///   otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="YamlObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        bool IDictionary<string, YamlNode?>.TryGetValue(string propertyName, out YamlNode? yamlNode)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return Dictionary.TryGetValue(propertyName, out yamlNode);
        }

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<KeyValuePair<string, YamlNode?>>.IsReadOnly => false;

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="YamlObject"/>.
        /// </summary>
        /// <returns>
        ///   An enumerator that iterates through the <see cref="YamlObject"/>.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => Dictionary.GetEnumerator();

        private OrderedDictionary<string, YamlNode?> InitializeDictionary()
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, YamlNode?>? dictionary, out YamlElement? yamlElement);

            if (dictionary is null)
            {
                OrderedDictionary<string, YamlNode?> newDictionary = CreateDictionary(Options);

                if (yamlElement.HasValue)
                {
                    // M5c: a repeated name keeps the first under FirstWins and
                    // the last under LastWins; under Error it throws here, as in System.Text.Json.
                    YamlDuplicateKeyHandling handling = yamlElement.Value.DuplicateKeyHandlingForNodes;
                    List<ComplexKeyEntry>? complexKeys = null;

                    // The comments move from the document to the entries, unless this node is a copy made through an
                    // alias: then its children are copies too, which keep none.
                    bool captureComments = !NoComments && yamlElement.Value.HasComments;
                    Dictionary<string, SlotComments>? slotComments = null;
                    Dictionary<string, int>? complexKeyIndex = null;
                    foreach (YamlProperty jElementProperty in yamlElement.Value.EnumerateObject())
                    {
                        if (jElementProperty.KeyKind != YamlKeyKind.Scalar
                            && jElementProperty.GetKeyElement() is { ValueKind: YamlValueKind.Object or YamlValueKind.Array } keyElement)
                        {
                            // M5e (review 4): a collection key's entry, apart from the string keys; repeats (by the
                            // key's canonical form) follow the same handling as repeated names.
                            YamlNode keyNode = YamlNodeConverter.CreateChild(keyElement, Options, this)!;
                            keyNode.Parent = this;
                            YamlNode? valueNode = YamlNodeConverter.CreateChild(jElementProperty.Value, Options, this);
                            valueNode?.Parent = this;
                            InheritNoComments(keyNode);
                            InheritNoComments(valueNode);
                            var entry = new ComplexKeyEntry(keyNode, valueNode, newDictionary.Count)
                            {
                                Comments = captureComments ? SlotComments.From(jElementProperty.Value, property: true) : null,
                            };
                            string identity = YamlElement.KeyIdentity(jElementProperty);
                            if ((complexKeyIndex ??= new Dictionary<string, int>(StringComparer.Ordinal)).TryGetValue(identity, out int existing))
                            {
                                if (handling == YamlDuplicateKeyHandling.LastWins)
                                {
                                    complexKeys![existing] = new ComplexKeyEntry(keyNode, valueNode, complexKeys[existing].Position) { Comments = entry.Comments };
                                }

                                continue;
                            }

                            complexKeyIndex[identity] = (complexKeys ??= new List<ComplexKeyEntry>()).Count;
                            complexKeys.Add(entry);
                            continue;
                        }

                        if (handling == YamlDuplicateKeyHandling.FirstWins && newDictionary.ContainsKey(jElementProperty.Name))
                        {
                            continue;
                        }

                        YamlNode? node = YamlNodeConverter.CreateChild(jElementProperty.Value, Options, this);
                        node?.Parent = this;
                        InheritNoComments(node);

                        if (handling == YamlDuplicateKeyHandling.LastWins)
                        {
                            newDictionary[jElementProperty.Name] = node;
                        }
                        else
                        {
                            newDictionary.Add(jElementProperty.Name, node);
                        }

                        if (captureComments)
                        {
                            SlotComments? comments = SlotComments.From(jElementProperty.Value, property: true);
                            if (comments is not null)
                            {
                                (slotComments ??= CreateSlotComments(Options))[jElementProperty.Name] = comments;
                            }
                            else
                            {
                                slotComments?.Remove(jElementProperty.Name); // a repeated name read LastWins
                            }
                        }
                    }

                    _complexKeys = complexKeys;
                    SlotCommentMap = slotComments;
                }

                // M7b: the element's style is kept before the element is dropped, and so are its comments.
                if (yamlElement.HasValue)
                {
                    CaptureCollectionStyle(yamlElement.Value);
                    CaptureCollectionComments(yamlElement.Value);
                }

                // Ensure only one dictionary instance is published using CompareExchange
                OrderedDictionary<string, YamlNode?>? exchangedDictionary = Interlocked.CompareExchange(ref _dictionary, newDictionary, null);
                if (exchangedDictionary is null)
                {
                    // We won the race and published our dictionary
                    // Ensure _jsonElement is written to after _dictionary
                    _yamlElement = null;
                    dictionary = newDictionary;
                }
                else
                {
                    // Another thread won the race, use their dictionary
                    dictionary = exchangedDictionary;
                }
            }

            return dictionary;
        }

        private static OrderedDictionary<string, YamlNode?> CreateDictionary(YamlNodeOptions? options, int capacity = 0)
        {
            StringComparer comparer = options?.PropertyNameCaseInsensitive ?? false
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

            return new(capacity, comparer);
        }

        /// <summary>
        /// Provides a coherent view of the underlying representation of the current node.
        /// The jsonElement value should be consumed if and only if dictionary value is null.
        /// </summary>
        private void GetUnderlyingRepresentation(out OrderedDictionary<string, YamlNode?>? dictionary, out YamlElement? yamlElement)
        {
            // Because YamlElement cannot be read atomically there might be torn reads,
            // however the order of read/write operations guarantees that that's only
            // possible if the value of _dictionary is non-null.
            yamlElement = _yamlElement;
            Interlocked.MemoryBarrier();
            dictionary = _dictionary;
        }
    }
}
