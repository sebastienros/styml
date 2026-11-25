// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Converters;
using System.Threading;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    ///   Represents a mutable YAML array.
    /// </summary>
    /// <remarks>
    /// It is safe to perform multiple concurrent read operations on a <see cref="YamlArray"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("YamlArray[{List.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed partial class YamlArray : YamlNode
    {
        private YamlElement? _yamlElement;
        private List<YamlNode?>? _list;

        internal override YamlElement? UnderlyingElement => _yamlElement;

        // M7b: 0 until set or captured, else the style plus
        // one. A node made from an element captures the element's style before it drops the element. M7c: a style the
        // caller set carries CollectionStyleSet, so that IgnoreSourceStyles drops only a captured one.
        private byte _collectionStyle;
        private const byte CollectionStyleSet = 0x80;

        /// <summary>
        ///   Gets or sets the style this collection is written in by a block-style writer:
        ///   <see cref="YamlCollectionStyle.Flow"/> for <c>[a, b]</c>, <see cref="YamlCollectionStyle.Block"/> otherwise.
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
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public YamlArray(YamlNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that contains items from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="YamlArray"/>.</param>
        public YamlArray(YamlNodeOptions options, params YamlNode?[] items) : base(options)
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that contains items from the specified params span.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="YamlArray"/>.</param>
        public YamlArray(YamlNodeOptions options, params ReadOnlySpan<YamlNode?> items) : base(options)
        {
            InitializeFromSpan(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that contains items from the specified array.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="YamlArray"/>.</param>
        public YamlArray(params YamlNode?[] items) : base()
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that contains items from the specified span.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="YamlArray"/>.</param>
        public YamlArray(params ReadOnlySpan<YamlNode?> items) : base()
        {
            InitializeFromSpan(items);
        }

        private protected override YamlValueKind GetValueKindCore() => YamlValueKind.Array;

        internal override YamlNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<YamlNode?>? list, out YamlElement? yamlElement);

            if (list is null)
            {
                return (yamlElement.HasValue
                    ? new YamlArray(yamlElement.Value.Clone(), Options) { _collectionStyle = _collectionStyle, NoComments = NoComments }
                    : new YamlArray(Options) { _collectionStyle = _collectionStyle, _startComment = _startComment, _endComment = _endComment }).WithPropertiesOf<YamlArray>(this);
            }

            var yamlArray = new YamlArray(Options)
            {
                _list = new List<YamlNode?>(list.Count),
                _collectionStyle = _collectionStyle,
            };

            for (int i = 0; i < list.Count; i++)
            {
                yamlArray.Add(list[i]?.DeepCloneCore());
            }

            if (_itemComments is { } itemComments)
            {
                yamlArray._itemComments = new List<SlotComments?>(itemComments.Count);
                foreach (SlotComments? comments in itemComments)
                {
                    yamlArray._itemComments.Add(comments?.Copy());
                }
            }

            yamlArray._startComment = _startComment;
            yamlArray._endComment = _endComment;
            yamlArray.NoComments = NoComments;
            return yamlArray.WithPropertiesOf<YamlArray>(this);
        }

        internal override bool DeepEqualsCore(YamlNode node)
        {
            switch (node)
            {
                case YamlObject:
                    return false;
                case YamlValue value:
                    // YamlValue instances have special comparison semantics, dispatch to their implementation.
                    return value.DeepEqualsCore(this);
                case YamlArray array:
                    List<YamlNode?> currentList = List;
                    List<YamlNode?> otherList = array.List;

                    if (currentList.Count != otherList.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < currentList.Count; i++)
                    {
                        if (!DeepEquals(currentList[i], otherList[i]))
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

        internal int GetElementIndex(YamlNode? node)
        {
            return List.IndexOf(node);
        }

        /// <summary>
        /// Returns an enumerable that wraps calls to <see cref="YamlNode.GetValue{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to obtain from the <see cref="YamlValue"/>.</typeparam>
        /// <returns>An enumerable iterating over values of the array.</returns>
        public IEnumerable<T> GetValues<T>()
        {
            foreach (YamlNode? item in List)
            {
                yield return item is null ? (T)(object?)null! : item.GetValue<T>();
            }
        }

        private void InitializeFromArray(YamlNode?[] items)
        {
            var list = new List<YamlNode?>(items);

            for (int i = 0; i < list.Count; i++)
            {
                list[i]?.AssignParent(this);
            }

            _list = list;
        }

        private void InitializeFromSpan(ReadOnlySpan<YamlNode?> items)
        {
            List<YamlNode?> list = new(items.Length);

#if NET
            list.AddRange(items);
#else
            foreach (YamlNode? item in items)
            {
                list.Add(item);
            }
#endif

            for (int i = 0; i < list.Count; i++)
            {
                list[i]?.AssignParent(this);
            }

            _list = list;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlArray"/> class that contains items from the specified <see cref="YamlElement"/>.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="YamlArray"/> class that contains items from the specified <see cref="YamlElement"/>.
        /// </returns>
        /// <param name="element">The <see cref="YamlElement"/>.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <exception cref="InvalidOperationException">
        ///   The <paramref name="element"/> is not a <see cref="YamlValueKind.Array"/>.
        /// </exception>
        public static YamlArray? Create(YamlElement element, YamlNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                YamlValueKind.Null => null,
                YamlValueKind.Array => new YamlArray(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(YamlValueKind.Array))),
            };
        }

        internal YamlArray(YamlElement element, YamlNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == YamlValueKind.Array);
            _yamlElement = element;
        }

        /// <summary>
        ///   Adds an object to the end of the <see cref="YamlArray"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to be added.</typeparam>
        /// <param name="value">
        ///   The object to be added to the end of the <see cref="YamlArray"/>.
        /// </param>
        [RequiresUnreferencedCode(YamlValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(YamlValue.CreateDynamicCodeMessage)]
        public void Add<T>(T? value)
        {
            YamlNode? nodeToAdd = ConvertFromValue(value, Options);
            Add(nodeToAdd);
        }

        /// <summary>
        /// Gets or creates the underlying list containing the element nodes of the array.
        /// </summary>
        private List<YamlNode?> List => _list ?? InitializeList();

        private protected override YamlNode? GetItem(int index)
        {
            return List[index];
        }

        private protected override void SetItem(int index, YamlNode? value)
        {
            value?.AssignParent(this);
            DetachParent(List[index]);
            List[index] = value;
        }

        internal override unsafe void GetPath(ref ValueStringBuilder path, YamlNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child is not null)
            {
                int index = List.IndexOf(child);
                Debug.Assert(index >= 0);

                path.Append('[');
#if NET
                Span<char> chars = stackalloc char[YamlConstants.MaximumFormatUInt32Length];
                bool formatted = ((uint)index).TryFormat(chars, out int charsWritten);
                Debug.Assert(formatted);
                path.Append(chars.Slice(0, charsWritten));
#else
                path.Append(index.ToString());
#endif
                path.Append(']');
            }
        }

        /// <inheritdoc/>
        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out List<YamlNode?>? list, out YamlElement? yamlElement);

            if (list is null && yamlElement.HasValue && !HasOwnProperties && _collectionStyle == 0)
            {
                WriteElementTo(yamlElement.Value, writer);
            }
            else
            {
                WritePropertiesTo(writer);
                WriteCollectionStyleTo(writer);
                writer.WriteStartArray();
                WriteOwnLineComment(writer, _startComment);

                List<YamlNode?> items = List;
                List<SlotComments?>? itemComments = writer.IsBlockStyle ? _itemComments : null;
                for (int i = 0; i < items.Count; i++)
                {
                    YamlNode? element = items[i];
                    SlotComments? comments = itemComments is not null && i < itemComments.Count ? itemComments[i] : null;
                    comments?.WriteLeading(writer, element, item: true);
                    if (element is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        element.WriteTo(writer, options);
                    }

                    comments?.WriteAfterValue(writer, element, item: true);
                }

                WriteOwnLineComment(writer, _endComment);
                writer.WriteEndArray();
            }
        }

        private List<YamlNode?> InitializeList()
        {
            GetUnderlyingRepresentation(out List<YamlNode?>? list, out YamlElement? yamlElement);

            if (list is null)
            {
                if (yamlElement.HasValue)
                {
                    YamlElement jElement = yamlElement.Value;
                    Debug.Assert(jElement.ValueKind == YamlValueKind.Array);

                    list = new List<YamlNode?>(jElement.GetArrayLength());

                    // The comments move from the document to the items, unless this node is a copy made through an
                    // alias: then its children are copies too, which keep none.
                    bool captureComments = !NoComments && jElement.HasComments;
                    List<SlotComments?>? itemComments = null;
                    foreach (YamlElement element in jElement.EnumerateArray())
                    {
                        YamlNode? node = YamlNodeConverter.CreateChild(element, Options, this);
                        node?.AssignParent(this);
                        if (NoComments && node is not null)
                        {
                            node.NoComments = true;
                        }

                        if (captureComments && SlotComments.From(element, property: false) is { } comments)
                        {
                            itemComments ??= new List<SlotComments?>(new SlotComments?[list.Count]);
                            itemComments.Add(comments);
                        }
                        else
                        {
                            itemComments?.Add(null);
                        }

                        list.Add(node);
                    }

                    _itemComments = itemComments;

                    // M7b: the element's style is kept before the element is dropped, and so are its comments.
                    CaptureCollectionStyle(jElement);
                    CaptureCollectionComments(jElement);
                }
                else
                {
                    list = new();
                }

                // Ensure _jsonElement is written to after _list
                _list = list;
                Interlocked.MemoryBarrier();
                _yamlElement = null;
            }

            return list;
        }

        /// <summary>
        /// Provides a coherent view of the underlying representation of the current node.
        /// The jsonElement value should be consumed if and only if the list value is null.
        /// </summary>
        private void GetUnderlyingRepresentation(out List<YamlNode?>? list, out YamlElement? yamlElement)
        {
            // Because YamlElement cannot be read atomically there might be torn reads,
            // however the order of read/write operations guarantees that that's only
            // possible if the value of _list is non-null.
            yamlElement = _yamlElement;
            Interlocked.MemoryBarrier();
            list = _list;
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly YamlArray _node;

            public DebugView(YamlArray node)
            {
                _node = node;
            }

            public string Yaml => _node.ToYamlString();
            public string Path => _node.GetPath();

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            private DebugViewItem[] Items
            {
                get
                {
                    DebugViewItem[] properties = new DebugViewItem[_node.List.Count];

                    for (int i = 0; i < _node.List.Count; i++)
                    {
                        properties[i].Value = _node.List[i];
                    }

                    return properties;
                }
            }

            [DebuggerDisplay("{Display,nq}")]
            private struct DebugViewItem
            {
                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public YamlNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string Display
                {
                    get
                    {
                        if (Value is null)
                        {
                            return $"null";
                        }

                        if (Value is YamlValue)
                        {
                            return Value.ToYamlString();
                        }

                        if (Value is YamlObject yamlObject)
                        {
                            return $"YamlObject[{yamlObject.Count}]";
                        }

                        YamlArray yamlArray = (YamlArray)Value;
                        return $"YamlArray[{yamlArray.List.Count}]";
                    }
                }
            }
        }
    }
}
