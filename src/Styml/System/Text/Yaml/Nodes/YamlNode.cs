// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Converters;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    ///   The base class that represents a single node within a mutable YAML document.
    /// </summary>
    /// <seealso cref="YamlSerializerOptions.UnknownTypeHandling"/> to specify that a type
    /// declared as an <see cref="object"/> should be deserialized as a <see cref="YamlNode"/>.
    public abstract partial class YamlNode
    {
        // Default options instance used when calling built-in YamlNode converters.
        private protected static readonly YamlSerializerOptions s_defaultOptions = new();

        private YamlNode? _parent;
        private YamlNodeOptions? _options;
        private string? _anchor;
        private string? _tag;
        private bool _ownProperties;

        /// <summary>
        ///   Gets or sets the node's anchor (<c>&amp;name</c>, without the <c>&amp;</c>), or <see langword="null"/>.
        /// </summary>
        /// <remarks>
        ///   A node made from a <see cref="YamlDocument"/> keeps its anchor. A node made through an alias is a copy of the
        ///   anchored node and has no anchor of its own. A block-style writer writes the anchor; a JSON-compatible one does not.
        ///   An alias that is still part of an unedited element (inside a child that was not changed, or inside a copy's
        ///   content) is written as that element writes it: as an alias when its anchor is inside the element, otherwise as
        ///   the anchored node with its anchor, which redefines the anchor at that point (YAML allows it; the tree re-reads
        ///   equal).
        /// </remarks>
        /// <exception cref="ArgumentException">The value is empty.</exception>
        public string? Anchor
        {
            get => _anchor;
            set
            {
                if (value is { Length: 0 })
                {
                    throw new ArgumentException(SR.YamlGrammar_InvalidAnchor, nameof(value));
                }

                _anchor = value;
                _ownProperties = true;
            }
        }

        /// <summary>
        ///   Gets or sets the node's tag as written (<c>!local</c>, <c>!!str</c>), or <see langword="null"/>.
        /// </summary>
        /// <remarks>
        ///   A node made from a <see cref="YamlDocument"/> keeps its tag. A block-style writer writes the tag; a
        ///   JSON-compatible one does not.
        /// </remarks>
        /// <exception cref="ArgumentException">The value is empty.</exception>
        public string? Tag
        {
            get => _tag;
            set
            {
                if (value is { Length: 0 })
                {
                    throw new ArgumentException(SR.YamlGrammar_InvalidTag, nameof(value));
                }

                _tag = value;
                _ownProperties = true;
            }
        }

        /// <summary>The anchor or tag was set after the node was made from an element, so the element is not written as it is.</summary>
        internal bool HasOwnProperties => _ownProperties;

        internal void InitializeProperties(YamlElement element)
        {
            if (!element.IsAlias)
            {
                _anchor = element.GetAnchor();
            }
            else
            {
                // M7c: a copy made through an alias has no anchor of its own, so it never writes its element as it is,
                // which would write the anchored node's anchor a second time.
                _ownProperties = true;
            }

            _tag = element.GetTag();
        }

        /// <summary>M7c: a deep clone keeps this node's anchor and tag, set or read, and has no anchor if this node has none.</summary>
        internal T WithPropertiesOf<T>(YamlNode source) where T : YamlNode
        {
            _anchor = source._anchor;
            _tag = source._tag;
            _ownProperties = source._ownProperties;
            return (T)this;
        }

        internal void WritePropertiesTo(Utf8YamlWriter? writer)
        {
            if (writer is null || !writer.IsBlockStyle)
            {
                return;
            }

            if (_anchor is not null)
            {
                writer.WriteAnchor(_anchor);
            }

            if (_tag is not null)
            {
                writer.WriteTag(_tag);
            }
        }

        /// <summary>
        /// The underlying YamlElement if the node is backed by a YamlElement.
        /// </summary>
        internal virtual YamlElement? UnderlyingElement => null;

        /// <summary>
        ///   Options to control the behavior.
        /// </summary>
        public YamlNodeOptions? Options
        {
            get
            {
                if (!_options.HasValue && Parent is not null)
                {
                    // Remember the parent options; if node is re-parented later we still want to keep the
                    // original options since they may have affected the way the node was created as is the case
                    // with YamlObject's case-insensitive dictionary.
                    _options = Parent.Options;
                }

                return _options;
            }
        }

        internal YamlNode(YamlNodeOptions? options = null)
        {
            _options = options;
        }

        /// <summary>
        ///   Casts to the derived <see cref="YamlArray"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="YamlArray"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="YamlArray"/>.
        /// </exception>
        public YamlArray AsArray()
        {
            YamlArray? jArray = this as YamlArray;

            if (jArray is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(YamlArray));
            }

            return jArray;
        }

        /// <summary>
        ///   Casts to the derived <see cref="YamlObject"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="YamlObject"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="YamlObject"/>.
        /// </exception>
        public YamlObject AsObject()
        {
            YamlObject? jObject = this as YamlObject;

            if (jObject is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(YamlObject));
            }

            return jObject;
        }

        /// <summary>
        ///   Casts to the derived <see cref="YamlValue"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="YamlValue"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="YamlValue"/>.
        /// </exception>
        public YamlValue AsValue()
        {
            YamlValue? jValue = this as YamlValue;

            if (jValue is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(YamlValue));
            }

            return jValue;
        }

        /// <summary>
        ///   Gets the parent <see cref="YamlNode"/>.
        ///   If there is no parent, <see langword="null"/> is returned.
        ///   A parent can either be a <see cref="YamlObject"/> or a <see cref="YamlArray"/>.
        /// </summary>
        public YamlNode? Parent
        {
            get
            {
                return _parent;
            }
            internal set
            {
                _parent = value;
            }
        }

        /// <summary>
        ///   Gets the YAML path.
        /// </summary>
        /// <returns>The JSON Path value.</returns>
        public unsafe string GetPath()
        {
            if (Parent is null)
            {
                return "$";
            }

            var path = new ValueStringBuilder(stackalloc char[YamlConstants.StackallocCharThreshold]);
            path.Append('$');
            GetPath(ref path, null);
            return path.ToString();
        }

        internal abstract void GetPath(ref ValueStringBuilder path, YamlNode? child);

        /// <summary>
        ///   Gets the root <see cref="YamlNode"/>.
        /// </summary>
        /// <remarks>
        ///   The current node is returned if it is a root.
        /// </remarks>
        public YamlNode Root
        {
            get
            {
                YamlNode? parent = Parent;
                if (parent is null)
                {
                    return this;
                }

                while (parent.Parent is not null)
                {
                    parent = parent.Parent;
                }

                return parent;
            }
        }

        /// <summary>
        ///   Gets the value for the current <see cref="YamlValue"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to obtain from the <see cref="YamlValue"/>.</typeparam>
        /// <returns>A value converted from the <see cref="YamlValue"/> instance.</returns>
        /// <remarks>
        ///   {T} can be the type or base type of the underlying value.
        ///   If the underlying value is a <see cref="YamlElement"/> then {T} can also be the type of any primitive
        ///   value supported by current <see cref="YamlElement"/>.
        ///   Specifying the <see cref="object"/> type for {T} will always succeed and return the underlying value as <see cref="object"/>.<br />
        ///   The underlying value of a <see cref="YamlValue"/> after deserialization is an instance of <see cref="YamlElement"/>,
        ///   otherwise it's the value specified when the <see cref="YamlValue"/> was created.
        /// </remarks>
        /// <seealso cref="System.Text.Yaml.Nodes.YamlValue.TryGetValue"></seealso>
        /// <exception cref="FormatException">
        ///   The current <see cref="YamlNode"/> cannot be represented as a {T}.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="YamlNode"/> is not a <see cref="YamlValue"/> or
        ///   is not compatible with {T}.
        /// </exception>
        public virtual T GetValue<T>() =>
            throw new InvalidOperationException(SR.Format(SR.NodeWrongType, nameof(YamlValue)));

        /// <summary>
        ///   Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0 or <paramref name="index"/> is greater than the number of properties.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="YamlNode"/> is not a <see cref="YamlArray"/> or <see cref="YamlObject"/>.
        /// </exception>
        public YamlNode? this[int index]
        {
            get => GetItem(index);
            set => SetItem(index, value);
        }

        private protected virtual YamlNode? GetItem(int index)
        {
            ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(YamlArray), nameof(YamlObject));
            return null;
        }

        private protected virtual void SetItem(int index, YamlNode? node) =>
            ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(YamlArray), nameof(YamlObject));

        /// <summary>
        ///   Gets or sets the element with the specified property name.
        ///   If the property is not found, <see langword="null"/> is returned.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="YamlNode"/> is not a <see cref="YamlObject"/>.
        /// </exception>
        public YamlNode? this[string propertyName]
        {
            get
            {
                return AsObject().GetItem(propertyName);
            }
            set
            {
                AsObject().SetItem(propertyName, value);
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="YamlNode"/>. All children nodes are recursively cloned.
        /// </summary>
        /// <returns>A new cloned instance of the current node.</returns>
        public YamlNode DeepClone() => DeepCloneCore();

        internal abstract YamlNode DeepCloneCore();

        /// <summary>
        /// Returns <see cref="YamlValueKind"/> of current instance.
        /// </summary>
        public YamlValueKind GetValueKind() => GetValueKindCore();

        private protected abstract YamlValueKind GetValueKindCore();

        /// <summary>
        /// Returns property name of the current node from the parent object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current parent is not a <see cref="YamlObject"/>.
        /// </exception>
        public string GetPropertyName()
        {
            YamlObject? parentObject = _parent as YamlObject;

            if (parentObject is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeParentWrongType(nameof(YamlObject));
            }

            return parentObject.GetPropertyName(this);
        }

        /// <summary>
        /// Returns index of the current node from the parent <see cref="YamlArray" />.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current parent is not a <see cref="YamlArray"/>.
        /// </exception>
        public int GetElementIndex()
        {
            YamlArray? parentArray = _parent as YamlArray;

            if (parentArray is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeParentWrongType(nameof(YamlArray));
            }

            return parentArray.GetElementIndex(this);
        }

        /// <summary>
        /// Compares the values of two nodes, including the values of all descendant nodes.
        /// </summary>
        /// <param name="node1">The <see cref="YamlNode"/> to compare.</param>
        /// <param name="node2">The <see cref="YamlNode"/> to compare.</param>
        /// <returns><c>true</c> if the tokens are equal; otherwise <c>false</c>.</returns>
        public static bool DeepEquals(YamlNode? node1, YamlNode? node2)
        {
            if (node1 is null)
            {
                return node2 is null;
            }
            else if (node2 is null)
            {
                return false;
            }

            return node1.DeepEqualsCore(node2);
        }

        internal abstract bool DeepEqualsCore(YamlNode node);

        /// <summary>
        /// Replaces this node with a new value.
        /// </summary>
        /// <typeparam name="T">The type of value to be replaced.</typeparam>
        /// <param name="value">Value that replaces this node.</param>
        [RequiresUnreferencedCode(YamlValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlValue.CreateDynamicCodeMessage)]
        public void ReplaceWith<T>(T value)
        {
            YamlNode? node;
            switch (_parent)
            {
                case YamlObject yamlObject:
                    node = ConvertFromValue(value);
                    if (!yamlObject.TryReplaceCollectionKeyChild(this, node))
                    {
                        yamlObject.SetItem(GetPropertyName(), node);
                    }

                    return;
                case YamlArray yamlArray:
                    node = ConvertFromValue(value);
                    yamlArray.SetItem(GetElementIndex(), node);
                    return;
            }
        }

        internal void AssignParent(YamlNode parent)
        {
            if (Parent is not null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeAlreadyHasParent();
            }

            YamlNode? p = parent;
            while (p is not null)
            {
                if (p == this)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodeCycleDetected();
                }

                p = p.Parent;
            }

            Parent = parent;
        }

        /// <summary>
        /// Adaptation of the equivalent YamlValue.Create factory method extended
        /// to support arbitrary <see cref="YamlElement"/> and <see cref="YamlNode"/> values.
        /// TODO consider making public cf. https://github.com/dotnet/runtime/issues/70427
        /// </summary>
        [RequiresUnreferencedCode(YamlSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlSerializer.SerializationRequiresDynamicCodeMessage)]
        internal static YamlNode? ConvertFromValue<T>(T? value, YamlNodeOptions? options = null)
        {
            if (value is null)
            {
                return null;
            }

            if (value is YamlNode node)
            {
                return node;
            }

            if (value is YamlElement element)
            {
                return YamlNodeConverter.Create(element, options);
            }

            var yamlTypeInfo = YamlSerializerOptions.Default.GetTypeInfo<T>();
            return YamlValue.CreateFromTypeInfo(value, yamlTypeInfo, options);
        }
    }
}
