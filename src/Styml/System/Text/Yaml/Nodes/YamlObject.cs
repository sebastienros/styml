// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    ///   Represents a mutable YAML object.
    /// </summary>
    /// <remarks>
    /// It's safe to perform multiple concurrent read operations on a <see cref="YamlObject"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("YamlObject[{Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed partial class YamlObject : YamlNode
    {
        private YamlElement? _yamlElement;

        /// <summary>
        /// M5e: the entries whose key is a collection. Any string can
        /// be a scalar key's text, so they are not in the string dictionary: each keeps its key node, its value and its
        /// place among the string entries (the count of string entries before it), and WriteTo, DeepClone and DeepEquals
        /// put them back in that order. The string-keyed API does not see them.
        /// </summary>
        private List<ComplexKeyEntry>? _complexKeys;

        internal sealed class ComplexKeyEntry
        {
            internal ComplexKeyEntry(YamlNode key, YamlNode? value, int position)
            {
                Key = key;
                Value = value;
                Position = position;
            }

            internal YamlNode Key { get; set; }

            internal YamlNode? Value { get; set; }

            // M7b: kept in step with the string entries when one is inserted or removed (ShiftCollectionKeys).
            internal int Position { get; set; }

            // The entry's comments, from the document or set.
            internal SlotComments? Comments { get; set; }
        }

        /// <summary>
        ///   Gets the entries whose key is a collection (<c>? [a, b]</c>, <c>? {x: 1}</c>), in their order among this
        ///   object's entries. The string-keyed members (the indexer, <see cref="Count"/>, enumeration,
        ///   <see cref="ContainsKey"/>) do not see them; <see cref="YamlNode.DeepEquals"/>, <see cref="YamlNode.DeepClone"/>
        ///   and writing do.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   A parsed object keeps its source order across string keys and collection keys when written back. An entry
        ///   added by <see cref="AddCollectionKey"/> goes after every existing entry, and a string key added later goes
        ///   after it; each entry stays between the same string entries when one is inserted or removed. YamlDotNet's
        ///   <c>YamlMappingNode</c> keys its children by node, so its count includes these entries.
        ///   </para>
        ///   <para>
        ///   An entry's key and value have this object as their parent. Their <see cref="YamlNode.GetPath()"/> is
        ///   <c>…[?n]</c> for the value and <c>…[?n:key]</c> for the key, where n is the entry's index in this list, and
        ///   <see cref="YamlNode.ReplaceWith{T}(T)"/> replaces them in the entry (a key under the rules of
        ///   <see cref="AddCollectionKey"/>). They have no property name.
        ///   </para>
        ///   <para>
        ///   Keys are checked for duplicates when an entry is added. A key changed afterwards (a node added to it, say) is
        ///   the caller's to keep distinct: two deeply equal keys are written as they are, and reading them back is refused
        ///   under the default <see cref="YamlDocumentOptions.DuplicateKeyHandling"/>.
        ///   </para>
        /// </remarks>
        public IReadOnlyList<KeyValuePair<YamlNode, YamlNode?>> CollectionKeys
        {
            get
            {
                _ = Dictionary;
                return _complexKeys is { Count: > 0 } complexKeys
                    ? complexKeys.ConvertAll(static entry => new KeyValuePair<YamlNode, YamlNode?>(entry.Key, entry.Value))
                    : Array.Empty<KeyValuePair<YamlNode, YamlNode?>>();
            }
        }

        /// <summary>
        ///   Adds an entry whose key is a collection, after every existing entry. Writing it needs a block-style writer.
        /// </summary>
        /// <remarks>
        ///   The duplicate check is made here, against the keys as they are now. A key changed after it was added is the
        ///   caller's to keep distinct from the others (see <see cref="CollectionKeys"/>).
        /// </remarks>
        /// <param name="key">The key: a <see cref="YamlObject"/> or a <see cref="YamlArray"/>.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="key"/> is not a collection (a scalar key is a string key: use the indexer or
        ///   <see cref="Add(string, YamlNode)"/>), or an entry with a deeply equal key exists.
        /// </exception>
        /// <exception cref="InvalidOperationException"><paramref name="key"/> or <paramref name="value"/> already has a parent.</exception>
        public void AddCollectionKey(YamlNode key, YamlNode? value)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key is not (YamlObject or YamlArray))
            {
                ThrowHelper.ThrowArgumentException_CollectionKeyNotACollection(nameof(key));
            }

            _ = Dictionary;
            if (FindCollectionKey(key) >= 0)
            {
                ThrowHelper.ThrowArgumentException_DuplicateKey(nameof(key), key.ToYamlString());
            }

            key.AssignParent(this);
            value?.AssignParent(this);
            (_complexKeys ??= new List<ComplexKeyEntry>()).Add(new ComplexKeyEntry(key, value, Dictionary.Count));
        }

        /// <summary>Removes the entry whose collection key is deeply equal to <paramref name="key"/>.</summary>
        /// <param name="key">The key to find.</param>
        /// <returns><see langword="true"/> if an entry was removed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool RemoveCollectionKey(YamlNode key)
        {
            ArgumentNullException.ThrowIfNull(key);
            _ = Dictionary;
            int index = FindCollectionKey(key);
            if (index < 0)
            {
                return false;
            }

            ComplexKeyEntry entry = _complexKeys![index];
            _complexKeys.RemoveAt(index);
            DetachParent(entry.Key);
            DetachParent(entry.Value);
            return true;
        }

        private int FindCollectionKey(YamlNode key)
        {
            if (_complexKeys is { } complexKeys)
            {
                for (int i = 0; i < complexKeys.Count; i++)
                {
                    if (DeepEquals(complexKeys[i].Key, key))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        // M7b: the collection-key entry whose key or value is this child, or -1.
        private int FindCollectionKeyChild(YamlNode child, out bool isKey)
        {
            isKey = false;
            if (_complexKeys is { } complexKeys)
            {
                for (int i = 0; i < complexKeys.Count; i++)
                {
                    if (ReferenceEquals(complexKeys[i].Key, child))
                    {
                        isKey = true;
                        return i;
                    }

                    if (ReferenceEquals(complexKeys[i].Value, child))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// M7b: <see cref="YamlNode.ReplaceWith{T}(T)"/> on a collection-key entry's key or value replaces it in the entry,
        /// under <see cref="AddCollectionKey"/>'s rules for a key. False when the child is not in such an entry.
        /// </summary>
        internal bool TryReplaceCollectionKeyChild(YamlNode child, YamlNode? replacement)
        {
            int index = FindCollectionKeyChild(child, out bool isKey);
            if (index < 0)
            {
                return false;
            }

            if (ReferenceEquals(child, replacement))
            {
                return true;
            }

            ComplexKeyEntry entry = _complexKeys![index];
            if (isKey)
            {
                if (replacement is not (YamlObject or YamlArray))
                {
                    ThrowHelper.ThrowArgumentException_CollectionKeyNotACollection("value");
                }

                int existing = FindCollectionKey(replacement);
                if (existing >= 0 && existing != index)
                {
                    ThrowHelper.ThrowArgumentException_DuplicateKey("value", replacement.ToYamlString());
                }

                replacement.AssignParent(this);
                DetachParent(child);
                entry.Key = replacement;
            }
            else
            {
                replacement?.AssignParent(this);
                DetachParent(child);
                entry.Value = replacement;
            }

            return true;
        }

        // M7b: a string entry inserted (delta 1) or removed (delta -1) at 'index' moves the collection-key entries placed
        // after it, so that each stays between the same string entries. An entry placed at 'index' itself stays before
        // the string entry now there, as one added after every entry stays before a string entry added next.
        private void ShiftCollectionKeys(int index, int delta)
        {
            if (_complexKeys is { } complexKeys)
            {
                foreach (ComplexKeyEntry entry in complexKeys)
                {
                    if (entry.Position > index)
                    {
                        entry.Position += delta;
                    }
                }
            }
        }

        /// <summary>M5e: an entry whose key is a collection (the serializer's YamlObject extension data, node reads).</summary>
        internal void AddComplexKeyEntry(YamlNode key, YamlNode? value)
        {
            int position = Dictionary.Count;
            key.Parent = this;
            value?.Parent = this;
            (_complexKeys ??= new List<ComplexKeyEntry>()).Add(new ComplexKeyEntry(key, value, position));
        }

        internal override YamlElement? UnderlyingElement => _yamlElement;

        // M7b: 0 until set or captured, else the style plus
        // one. A node made from an element captures the element's style before it drops the element. M7c: a style the
        // caller set carries CollectionStyleSet, so that IgnoreSourceStyles drops only a captured one.
        private byte _collectionStyle;
        private const byte CollectionStyleSet = 0x80;

        /// <summary>
        ///   Gets or sets the style this collection is written in by a block-style writer:
        ///   <see cref="YamlCollectionStyle.Flow"/> for <c>{key: value}</c>, <see cref="YamlCollectionStyle.Block"/> otherwise.
        ///   A node parsed from YAML or JSON has its source's style (JSON's collections are flow); a new node is block.
        ///   Inside a flow collection every collection is flow, and a JSON-compatible writer writes flow whatever is set. A
        ///   source's style is not written under <see cref="YamlWriterOptions.IgnoreSourceStyles"/>; a style set here is.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a collection style.</exception>
        public YamlCollectionStyle CollectionStyle
        {
            get => _collectionStyle != 0 ? (YamlCollectionStyle)((_collectionStyle & ~CollectionStyleSet) - 1)
                : _yamlElement is { } element ? element.CollectionStyle ?? YamlCollectionStyle.Block
                : YamlCollectionStyle.Block;
            set
            {
                if (value is not (YamlCollectionStyle.Block or YamlCollectionStyle.Flow))
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_CollectionStyle(nameof(value));
                }

                _collectionStyle = (byte)(((int)value + 1) | CollectionStyleSet);
            }
        }

        // Before the element is dropped (the node materializes or is cleared), its style is kept.
        private void CaptureCollectionStyle(YamlElement element)
        {
            if (_collectionStyle == 0)
            {
                _collectionStyle = (byte)((int)(element.CollectionStyle ?? YamlCollectionStyle.Block) + 1);
            }
        }

        // Before the collection's start: the style it was set to, or its source's, for a block-style writer; a source's
        // style is not asked for under IgnoreSourceStyles (M7c).
        private void WriteCollectionStyleTo(Utf8YamlWriter writer)
        {
            if (((_collectionStyle & CollectionStyleSet) != 0 ? writer.IsBlockStyle : writer.KeepsSourceStyles)
                && CollectionStyle == YamlCollectionStyle.Flow)
            {
                writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
            }
        }


        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlObject"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public YamlObject(YamlNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlObject"/> class that contains the specified <paramref name="properties"/>.
        /// </summary>
        /// <param name="properties">The properties to be added.</param>
        /// <param name="options">Options to control the behavior.</param>
        public YamlObject(IEnumerable<KeyValuePair<string, YamlNode?>> properties, YamlNodeOptions? options = null) : this(options)
        {
            int capacity = properties is ICollection<KeyValuePair<string, YamlNode?>> propertiesCollection ? propertiesCollection.Count : 0;
            OrderedDictionary<string, YamlNode?> dictionary = CreateDictionary(options, capacity);

            foreach (KeyValuePair<string, YamlNode?> node in properties)
            {
                dictionary.Add(node.Key, node.Value);
                node.Value?.AssignParent(this);
            }

            _dictionary = dictionary;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlObject"/> class that contains properties from the specified <see cref="YamlElement"/>.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="YamlObject"/> class that contains properties from the specified <see cref="YamlElement"/>.
        /// </returns>
        /// <param name="element">The <see cref="YamlElement"/>.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>A <see cref="YamlObject"/>.</returns>
        public static YamlObject? Create(YamlElement element, YamlNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                YamlValueKind.Null => null,
                YamlValueKind.Object => new YamlObject(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(YamlValueKind.Object)))
            };
        }

        internal YamlObject(YamlElement element, YamlNodeOptions? options = null) : this(options)
        {
            Debug.Assert(element.ValueKind == YamlValueKind.Object);
            _yamlElement = element;
        }

        /// <summary>
        /// Gets or creates the underlying dictionary containing the properties of the object.
        /// </summary>
        private OrderedDictionary<string, YamlNode?> Dictionary => _dictionary ?? InitializeDictionary();

        private protected override YamlNode? GetItem(int index) => GetAt(index).Value;
        private protected override void SetItem(int index, YamlNode? value) => SetAt(index, value);

        internal override YamlNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, YamlNode?>? dictionary, out YamlElement? yamlElement);

            if (dictionary is null)
            {
                return (yamlElement.HasValue
                    ? new YamlObject(yamlElement.Value.Clone(), Options) { _collectionStyle = _collectionStyle, NoComments = NoComments }
                    : new YamlObject(Options) { _collectionStyle = _collectionStyle, _startComment = _startComment, _endComment = _endComment }).WithPropertiesOf<YamlObject>(this);
            }

            var jObject = new YamlObject(Options)
            {
                _dictionary = CreateDictionary(Options, Count),
                _collectionStyle = _collectionStyle,
            };

            foreach (KeyValuePair<string, YamlNode?> item in dictionary)
            {
                jObject.Add(item.Key, item.Value?.DeepCloneCore());
            }

            if (_complexKeys is { } complexKeys)
            {
                foreach (ComplexKeyEntry entry in complexKeys)
                {
                    YamlNode? value = entry.Value?.DeepCloneCore();
                    value?.Parent = jObject;
                    YamlNode key = entry.Key.DeepCloneCore();
                    key.Parent = jObject; // M7b: a collection key's parent is its object, as its value's is
                    (jObject._complexKeys ??= new List<ComplexKeyEntry>()).Add(new ComplexKeyEntry(key, value, entry.Position) { Comments = entry.Comments?.Copy() });
                }
            }

            if (_slotComments is { } slotComments)
            {
                jObject._slotComments = CreateSlotComments(Options);
                foreach (KeyValuePair<string, SlotComments> item in slotComments)
                {
                    jObject._slotComments[item.Key] = item.Value.Copy();
                }
            }

            jObject._startComment = _startComment;
            jObject._endComment = _endComment;
            jObject.NoComments = NoComments;
            return jObject.WithPropertiesOf<YamlObject>(this);
        }

        internal string GetPropertyName(YamlNode? node)
        {
            KeyValuePair<string, YamlNode?>? item = FindValue(node);
            return item.HasValue ? item.Value.Key : string.Empty;
        }

        /// <summary>
        ///   Returns the value of a property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        /// <param name="yamlNode">The YAML value of the property with the specified name.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <returns>
        ///   <see langword="true"/> if a property with the specified name was found; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyValue(string propertyName, out YamlNode? yamlNode) => TryGetPropertyValue(propertyName, out yamlNode, out _);

        /// <summary>
        ///   Gets the value associated with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name of the value to get.</param>
        /// <param name="yamlNode">
        ///   When this method returns, it contains the value associated with the specified property name, if the property name is found;
        ///   otherwise <see langword="null"/>.
        /// </param>
        /// <param name="index">The index of <paramref name="propertyName"/> if found; otherwise, -1.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="YamlObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyValue(string propertyName, out YamlNode? yamlNode, out int index)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

#if NET9_0
            index = Dictionary.IndexOf(propertyName);
            if (index < 0)
            {
                yamlNode = null;
                return false;
            }

            yamlNode = Dictionary.GetAt(index).Value;
            return true;
#else
            return Dictionary.TryGetValue(propertyName, out yamlNode, out index);
#endif
        }

        /// <inheritdoc/>
        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out OrderedDictionary<string, YamlNode?>? dictionary, out YamlElement? yamlElement);

            if (dictionary is null && yamlElement.HasValue && !HasOwnProperties && _collectionStyle == 0)
            {
                // Write the element without converting to nodes (it writes its own source styles and comments).
                WriteElementTo(yamlElement.Value, writer);
            }
            else
            {
                WritePropertiesTo(writer);
                WriteCollectionStyleTo(writer);
                writer.WriteStartObject();
                WriteOwnLineComment(writer, _startComment);
                WriteContentsTo(writer, options);
                WriteOwnLineComment(writer, _endComment);
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Writes the properties of this YamlObject to the writer without the surrounding braces.
        /// This is used for extension data serialization where the properties should be flattened
        /// into the parent object.
        /// </summary>
        internal void WriteContentsTo(Utf8YamlWriter writer, YamlSerializerOptions? options)
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, YamlNode?>? dictionary, out YamlElement? yamlElement);

            if (dictionary is null && yamlElement.HasValue)
            {
                // Write properties from the underlying YamlElement without converting to nodes.
                foreach (YamlProperty property in yamlElement.Value.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
            }
            else
            {
                OrderedDictionary<string, YamlNode?> entries = Dictionary;
                List<ComplexKeyEntry>? complexKeys = _complexKeys;
                Dictionary<string, SlotComments>? slotComments = writer.IsBlockStyle ? _slotComments : null;
                int position = 0;
                int next = 0;
                foreach (KeyValuePair<string, YamlNode?> entry in entries)
                {
                    WriteComplexKeys(writer, options, complexKeys, position, ref next);
                    SlotComments? comments = null;
                    slotComments?.TryGetValue(entry.Key, out comments);
                    comments?.WriteLeading(writer, entry.Value, item: false);
                    writer.HintDefaultScalarStyleForNextKey(); // M7b (fix A): a YamlObject's keys are styled as a dictionary's
                    writer.WritePropertyName(entry.Key);
                    comments?.WriteAfterKey(writer, entry.Value);

                    if (entry.Value is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        entry.Value.WriteTo(writer, options);
                    }

                    comments?.WriteAfterValue(writer, entry.Value, item: false);
                    position++;
                }

                WriteComplexKeys(writer, options, complexKeys, int.MaxValue, ref next);
            }
        }

        // M5e: the collection-key entries placed before the string entry at 'position' (int.MaxValue: the rest).
        private static void WriteComplexKeys(Utf8YamlWriter writer, YamlSerializerOptions? options, List<ComplexKeyEntry>? complexKeys, int position, ref int next)
        {
            while (complexKeys is not null && next < complexKeys.Count && complexKeys[next].Position <= position)
            {
                if (!writer.IsBlockStyle)
                {
                    ThrowHelper.ThrowNotSupportedException_YamlComplexKeyJsonCompatible();
                }

                ComplexKeyEntry entry = complexKeys[next++];
                entry.Comments?.WriteLeading(writer, entry.Value, item: false);
                writer.WriteStartPropertyKey();
                entry.Key.WriteTo(writer, options);
                writer.WriteEndPropertyKey();
                entry.Comments?.WriteAfterKey(writer, entry.Value);
                if (entry.Value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    entry.Value.WriteTo(writer, options);
                }

                entry.Comments?.WriteAfterValue(writer, entry.Value, item: false);
            }
        }

        private protected override YamlValueKind GetValueKindCore() => YamlValueKind.Object;

        internal override bool DeepEqualsCore(YamlNode node)
        {
            switch (node)
            {
                case YamlArray:
                    return false;
                case YamlValue value:
                    // YamlValue instances have special comparison semantics, dispatch to their implementation.
                    return value.DeepEqualsCore(this);
                case YamlObject yamlObject:
                    OrderedDictionary<string, YamlNode?> currentDict = Dictionary;
                    OrderedDictionary<string, YamlNode?> otherDict = yamlObject.Dictionary;

                    if (currentDict.Count != otherDict.Count || !ComplexKeysDeepEqual(_complexKeys, yamlObject._complexKeys))
                    {
                        return false;
                    }

                    foreach (KeyValuePair<string, YamlNode?> item in currentDict)
                    {
                        if (!otherDict.TryGetValue(item.Key, out YamlNode? yamlNode) || !DeepEquals(item.Value, yamlNode))
                        {
                            return false;
                        }
                    }

                    return true;
                default:
                    Debug.Fail("Impossible case");
                    return false;
            }
        }

        // M5e: the collection-key entries pair up by key (unordered, as mappings) with equal values.
        private static bool ComplexKeysDeepEqual(List<ComplexKeyEntry>? left, List<ComplexKeyEntry>? right)
        {
            int count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            var used = new bool[count];
            foreach (ComplexKeyEntry entry in left!)
            {
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    if (!used[i] && DeepEquals(entry.Key, right![i].Key) && DeepEquals(entry.Value, right[i].Value))
                    {
                        used[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        internal YamlNode? GetItem(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (TryGetPropertyValue(propertyName, out YamlNode? value))
            {
                return value;
            }

            // Return null for missing properties.
            return null;
        }

        internal override void GetPath(ref ValueStringBuilder path, YamlNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child is not null && FindValue(child) is null)
            {
                // M7b: a collection-key entry's value is at [?n], its key at [?n:key] (n, its index in CollectionKeys).
                int entry = FindCollectionKeyChild(child, out bool isKey);
                Debug.Assert(entry >= 0);
                path.Append("[?");
                path.Append(entry.ToString(CultureInfo.InvariantCulture));
                path.Append(isKey ? ":key]" : "]");
            }
            else if (child is not null)
            {
                string propertyName = FindValue(child)!.Value.Key;
                if (propertyName.AsSpan().ContainsSpecialCharacters())
                {
                    path.Append("['");
                    path.AppendEscapedPropertyName(propertyName);
                    path.Append("']");
                }
                else
                {
                    path.Append('.');
                    path.Append(propertyName);
                }
            }
        }

        internal void SetItem(string propertyName, YamlNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            OrderedDictionary<string, YamlNode?> dict = Dictionary;

            if (
#if NET9_0
                !dict.TryAdd(propertyName, value)
#else
                !dict.TryAdd(propertyName, value, out int index)
#endif
                )
            {
#if NET9_0
                int index = dict.IndexOf(propertyName);
#endif
                Debug.Assert(index >= 0);
                YamlNode? replacedValue = dict.GetAt(index).Value;

                if (ReferenceEquals(value, replacedValue))
                {
                    return;
                }

                DetachParent(replacedValue);
                dict.SetAt(index, value);
            }

            value?.AssignParent(this);
        }

        private void DetachParent(YamlNode? item)
        {
            Debug.Assert(_dictionary is not null, "Cannot have detachable nodes without a materialized dictionary.");

            item?.Parent = null;
        }

        private KeyValuePair<string, YamlNode?>? FindValue(YamlNode? value)
        {
            foreach (KeyValuePair<string, YamlNode?> item in Dictionary)
            {
                if (ReferenceEquals(item.Value, value))
                {
                    return item;
                }
            }

            return null;
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly YamlObject _node;

            public DebugView(YamlObject node)
            {
                _node = node;
            }

            public string Yaml => _node.ToYamlString();
            public string Path => _node.GetPath();

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            private DebugViewProperty[] Items
            {
                get
                {
                    DebugViewProperty[] properties = new DebugViewProperty[_node.Count];

                    int i = 0;
                    foreach (KeyValuePair<string, YamlNode?> item in _node)
                    {
                        properties[i].PropertyName = item.Key;
                        properties[i].Value = item.Value;
                        i++;
                    }

                    return properties;
                }
            }

            [DebuggerDisplay("{Display,nq}")]
            private struct DebugViewProperty
            {
                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public YamlNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string PropertyName;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string Display
                {
                    get
                    {
                        if (Value is null)
                        {
                            return $"{PropertyName} = null";
                        }

                        if (Value is YamlValue)
                        {
                            return $"{PropertyName} = {Value.ToYamlString()}";
                        }

                        if (Value is YamlObject yamlObject)
                        {
                            return $"{PropertyName} = YamlObject[{yamlObject.Count}]";
                        }

                        YamlArray yamlArray = (YamlArray)Value;
                        return $"{PropertyName} = YamlArray[{yamlArray.Count}]";
                    }
                }

            }
        }
    }
}
