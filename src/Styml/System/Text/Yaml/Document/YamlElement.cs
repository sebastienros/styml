// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Text.Yaml
{
    /// <summary>
    ///   Represents a specific YAML value within a <see cref="YamlDocument"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly partial struct YamlElement
    {
        private readonly YamlDocument _parent;

        /// <summary>How a node made from this element treats a repeated name (M5c): FirstWins or an explicit
        /// LastWins pick; anything else is Error (System.Text.Json's throw on materialization).</summary>
        /// <summary>The schema of the element's document (M5c): deserializing the element re-reads with it.</summary>
        internal YamlSchemaKind DocumentSchema => _parent?.ParsedSchema ?? YamlSchemaKind.Core;

        /// <summary>Sets the handling of nodes made from this element's (private) document: an explicit choice.</summary>
        internal void SetNodeDuplicateKeyHandling(YamlDuplicateKeyHandling handling)
        {
            _parent.ParsedDuplicateKeyHandling = handling;
        }

        internal YamlDuplicateKeyHandling DuplicateKeyHandlingForNodes =>
            _parent?.ParsedDuplicateKeyHandling ?? YamlDuplicateKeyHandling.Error;
        private readonly int _idx;

        // The element's own row: an alias row when the element was reached through an alias (M5a), else _idx. The
        // property name of a member is the row before it, so the name helpers read this row.
        private readonly int _rowIdx;

        internal YamlElement(YamlDocument parent, int idx)
        {
            // parent is usually not null, but the Current property
            // on the enumerators (when initialized as `default`) can
            // get here with a null.
            Debug.Assert(idx >= 0);

            _parent = parent;
            _idx = idx;
            _rowIdx = idx;
        }

        /// <summary>An element reached through the alias row <paramref name="aliasIdx"/>: it is the anchored node.</summary>
        internal YamlElement(YamlDocument parent, int idx, int aliasIdx)
        {
            Debug.Assert(idx >= 0 && aliasIdx > idx);

            _parent = parent;
            _idx = idx;
            _rowIdx = aliasIdx;
        }

        /// <summary>See <see cref="YamlDocument.ThrowIfAliasesForSerializer"/>.</summary>
        internal YamlElement ThrowIfAliasesForSerializer()
        {
            _parent?.ThrowIfAliasesForSerializer();
            return this;
        }

        /// <summary>A node is made from this alias element: counted against the node tree's expansion budget, refused when recursive.</summary>
        internal void CountAliasExpansion(ref int expanded) => _parent.CountAliasExpansion(_rowIdx, ref expanded);

        internal void CheckNodeDepth(int depth) => _parent.CheckTraversalDepth(depth);

        /// <summary>The bytes the serializer reads for this element (M5a, YamlDocument.GetDeserializationValue).</summary>
        internal ReadOnlyMemory<byte> GetDeserializationValue()
        {
            CheckValidInstance();
            return _parent.GetDeserializationValue(_idx);
        }

        /// <summary>The row of the alias this element was reached through, or -1.</summary>
        internal int AliasRowIndex => _rowIdx != _idx ? _rowIdx : -1;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private YamlTokenType TokenType
        {
            get
            {
                return _parent?.GetYamlTokenType(_idx) ?? YamlTokenType.None;
            }
        }
        /// <summary>
        ///   The <see cref="YamlValueKind"/> that the value is.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlValueKind ValueKind => TokenType is YamlTokenType.PropertyName
            ? YamlValueKind.String // an alias to an anchored mapping key (M5a): the key is its string
            : TokenType.ToValueKind();

        /// <summary>
        ///   Get the value at a specified index when the current value is a
        ///   <see cref="YamlValueKind.Array"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Array"/>.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        ///   <paramref name="index"/> is not in the range [0, <see cref="GetArrayLength"/>()).
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlElement this[int index]
        {
            get
            {
                CheckValidInstance();

                return _parent.GetArrayIndexElement(_idx, index);
            }
        }

        /// <summary>
        ///   Get the number of values contained within the current array value.
        /// </summary>
        /// <returns>The number of values contained within the current array value.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public int GetArrayLength()
        {
            CheckValidInstance();

            return _parent.GetArrayLength(_idx);
        }

        /// <summary>
        ///   Get the number of properties contained within the current object value.
        /// </summary>
        /// <returns>The number of properties contained within the current object value.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public int GetPropertyCount()
        {
            CheckValidInstance();

            return _parent.GetPropertyCount(_idx);
        }

        /// <summary>
        ///   Gets a <see cref="YamlElement"/> representing the value of a required property identified
        ///   by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        ///   Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///
        ///   If a property is defined multiple times for the same object, the last such definition is
        ///   what is matched.
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        ///   A <see cref="YamlElement"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlElement GetProperty(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (TryGetProperty(propertyName, out YamlElement property))
            {
                return property;
            }

            throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, propertyName));
        }

        /// <summary>
        ///   Gets a <see cref="YamlElement"/> representing the value of a required property identified
        ///   by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        ///   A <see cref="YamlElement"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlElement GetProperty(ReadOnlySpan<char> propertyName)
        {
            if (TryGetProperty(propertyName, out YamlElement property))
            {
                return property;
            }

            throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, propertyName.ToString()));
        }

        /// <summary>
        ///   Gets a <see cref="YamlElement"/> representing the value of a required property identified
        ///   by <paramref name="utf8PropertyName"/>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        ///   The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <returns>
        ///   A <see cref="YamlElement"/> representing the value of the requested property.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public YamlElement GetProperty(ReadOnlySpan<byte> utf8PropertyName)
        {
            if (TryGetProperty(utf8PropertyName, out YamlElement property))
            {
                return property;
            }

            throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, Encoding.UTF8.GetString(utf8PropertyName)));
        }

        /// <summary>
        ///   Looks for a property named <paramref name="propertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public bool TryGetProperty(string propertyName, out YamlElement value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return TryGetProperty(propertyName.AsSpan(), out value);
        }

        /// <summary>
        ///   Looks for a property named <paramref name="propertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetProperty(ReadOnlySpan<char> propertyName, out YamlElement value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(_idx, propertyName, out value);
        }

        /// <summary>
        ///   Looks for a property named <paramref name="utf8PropertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        ///   The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetProperty(ReadOnlySpan<byte> utf8PropertyName, out YamlElement value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(_idx, utf8PropertyName, out value);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="bool"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="bool"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is neither <see cref="YamlValueKind.True"/> or
        ///   <see cref="YamlValueKind.False"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool GetBoolean()
        {
            // CheckValidInstance is redundant.  Asking for the type will
            // return None, which then throws the same exception in the return statement.

            YamlTokenType type = TokenType;

            return
                type == YamlTokenType.True ? true :
                type == YamlTokenType.False ? false :
                ThrowYamlElementWrongTypeException(type);

            static bool ThrowYamlElementWrongTypeException(YamlTokenType actualType)
            {
                throw ThrowHelper.GetYamlElementWrongTypeException(nameof(Boolean), actualType.ToValueKind());
            }
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not create a string representation of values other than YAML strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="string"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is neither <see cref="YamlValueKind.String"/> nor <see cref="YamlValueKind.Null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public string? GetString()
        {
            CheckValidInstance();

            return _parent.GetString(_idx, YamlTokenType.String);
        }

        /// <summary>
        ///   Attempts to represent the current YAML string as bytes assuming it is Base64 encoded.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///  This method does not create a byte[] representation of values other than base 64 encoded YAML strings.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the entire token value is encoded as valid Base64 text and can be successfully decoded to bytes.
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetBytesFromBase64([NotNullWhen(true)] out byte[]? value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the value of the element as bytes.
        /// </summary>
        /// <remarks>
        ///   This method does not create a byte[] representation of values other than Base64 encoded YAML strings.
        /// </remarks>
        /// <returns>The value decode to bytes.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value is not encoded as Base64 text and hence cannot be decoded to bytes.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public byte[] GetBytesFromBase64()
        {
            if (!TryGetBytesFromBase64(out byte[]? value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as an <see cref="sbyte"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as an <see cref="sbyte"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public bool TryGetSByte(out sbyte value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as an <see cref="sbyte"/>.
        /// </summary>
        /// <returns>The current YAML number as an <see cref="sbyte"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as an <see cref="sbyte"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public sbyte GetSByte()
        {
            if (!TryGetSByte(out sbyte value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="byte"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="byte"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetByte(out byte value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="byte"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="byte"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="byte"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public byte GetByte()
        {
            if (!TryGetByte(out byte value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as an <see cref="short"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as an <see cref="short"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetInt16(out short value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as an <see cref="short"/>.
        /// </summary>
        /// <returns>The current YAML number as an <see cref="short"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as an <see cref="short"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public short GetInt16()
        {
            if (!TryGetInt16(out short value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="ushort"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="ushort"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public bool TryGetUInt16(out ushort value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="ushort"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="ushort"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="ushort"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public ushort GetUInt16()
        {
            if (!TryGetUInt16(out ushort value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as an <see cref="int"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as an <see cref="int"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetInt32(out int value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as an <see cref="int"/>.
        /// </summary>
        /// <returns>The current YAML number as an <see cref="int"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as an <see cref="int"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public int GetInt32()
        {
            if (!TryGetInt32(out int value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="uint"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="uint"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public bool TryGetUInt32(out uint value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="uint"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="uint"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="uint"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public uint GetUInt32()
        {
            if (!TryGetUInt32(out uint value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="long"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="long"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetInt64(out long value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="long"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="long"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="long"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public long GetInt64()
        {
            if (!TryGetInt64(out long value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="ulong"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="ulong"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public bool TryGetUInt64(out ulong value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="ulong"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="ulong"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="ulong"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public ulong GetUInt64()
        {
            if (!TryGetUInt64(out ulong value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="double"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a YAML string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method does not return <see langword="false"/> for values larger than
        ///     <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>),
        ///     instead <see langword="true"/> is returned and <see cref="double.PositiveInfinity"/> (or
        ///     <see cref="double.NegativeInfinity"/>) is emitted.
        ///   </para>
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="double"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetDouble(out double value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="double"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="double"/>.</returns>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a YAML string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method returns <see cref="double.PositiveInfinity"/> (or
        ///     <see cref="double.NegativeInfinity"/>) for values larger than
        ///     <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>).
        ///   </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="double"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public double GetDouble()
        {
            if (!TryGetDouble(out double value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="float"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a YAML string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method does not return <see langword="false"/> for values larger than
        ///     <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>),
        ///     instead <see langword="true"/> is returned and <see cref="float.PositiveInfinity"/> (or
        ///     <see cref="float.NegativeInfinity"/>) is emitted.
        ///   </para>
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="float"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetSingle(out float value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="float"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="float"/>.</returns>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a YAML string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method returns <see cref="float.PositiveInfinity"/> (or
        ///     <see cref="float.NegativeInfinity"/>) for values larger than
        ///     <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>).
        ///   </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="float"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public float GetSingle()
        {
            if (!TryGetSingle(out float value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML number as a <see cref="decimal"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="decimal"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public bool TryGetDecimal(out decimal value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current YAML number as a <see cref="decimal"/>.
        /// </summary>
        /// <returns>The current YAML number as a <see cref="decimal"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a YAML string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="decimal"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public decimal GetDecimal()
        {
            if (!TryGetDecimal(out decimal value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML string as a <see cref="DateTime"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not create a DateTime representation of values other than YAML strings.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the string can be represented as a <see cref="DateTime"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetDateTime(out DateTime value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="DateTime"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not create a DateTime representation of values other than YAML strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="DateTime"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public DateTime GetDateTime()
        {
            if (!TryGetDateTime(out DateTime value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML string as a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not create a DateTimeOffset representation of values other than YAML strings.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the string can be represented as a <see cref="DateTimeOffset"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetDateTimeOffset(out DateTimeOffset value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not create a DateTimeOffset representation of values other than YAML strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="DateTimeOffset"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="DateTimeOffset"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public DateTimeOffset GetDateTimeOffset()
        {
            if (!TryGetDateTimeOffset(out DateTimeOffset value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        ///   Attempts to represent the current YAML string as a <see cref="Guid"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not create a Guid representation of values other than YAML strings.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the string can be represented as a <see cref="Guid"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public bool TryGetGuid(out Guid value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="Guid"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not create a Guid representation of values other than YAML strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="Guid"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="Guid"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public Guid GetGuid()
        {
            if (!TryGetGuid(out Guid value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        internal string GetPropertyName()
        {
            CheckValidInstance();

            return _parent.GetNameOfPropertyValue(_rowIdx);
        }

        internal ReadOnlySpan<byte> GetPropertyNameRaw()
        {
            CheckValidInstance();

            return _parent.GetPropertyNameRaw(_rowIdx);
        }

        /// <summary>
        /// Gets whether this element is a document's root whose <c>---</c> line carries Unity's <c>stripped</c> marker
        /// (<c>--- !u!4 &amp;400000 stripped</c>), read with <see cref="YamlDocumentOptions.AcceptUnityStrippedMarker"/> or
        /// <see cref="YamlReaderOptions.AcceptUnityStrippedMarker"/>. Written back after the root's tag on a
        /// <c>---</c> line (<see cref="Utf8YamlWriter.WriteDocumentStart(bool)"/>).
        /// </summary>
        public bool IsUnityStripped
        {
            get
            {
                CheckValidInstance();
                return _parent.IsUnityStripped(_idx);
            }
        }

        /// <summary>Whether this element is a document root whose properties were on its '---' line.</summary>
        internal bool PropertiesOnDocumentLine => _parent.PropertiesOnDocumentLine(_idx);

        /// <summary>The source style of the scalar key of the property whose value this element is (Plain, quoted).</summary>
        internal YamlScalarStyle PropertyKeyStyle => _parent.GetScalarStyle(_parent.NameRowOf(_rowIdx));

        /// <summary>Whether this element's source wrote its tag before its anchor.</summary>
        internal bool TagBeforeAnchor => _parent.TagBeforeAnchor(_idx);

        /// <summary>
        ///   Gets the anchor of this node (<c>&amp;name</c>), without the <c>&amp;</c>, or <see langword="null"/> when it has none.
        /// </summary>
        /// <remarks>
        ///   An element reached through an alias (<see cref="IsAlias"/>) is the anchored node, so it returns that node's anchor.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public string? GetAnchor()
        {
            CheckValidInstance();

            return _parent.GetAnchor(_idx);
        }

        /// <summary>
        ///   Gets the tag of this node as written (<c>!local</c>, <c>!!str</c>, <c>!&lt;uri&gt;</c>), or <see langword="null"/>
        ///   when it has none.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public string? GetTag()
        {
            CheckValidInstance();

            return _parent.GetTag(_idx, expanded: false);
        }

        /// <summary>
        ///   Gets the tag of this node with its handle expanded by the document's <c>%TAG</c> directives and the standard
        ///   handles (<c>!!str</c> is <c>tag:yaml.org,2002:str</c>), or <see langword="null"/> when it has none.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public string? GetExpandedTag()
        {
            CheckValidInstance();

            return _parent.GetTag(_idx, expanded: true);
        }

        /// <summary>
        ///   Gets the style the scalar was written in, or <see cref="YamlScalarStyle.None"/> for a mapping or a sequence.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlScalarStyle ScalarStyle
        {
            get
            {
                CheckValidInstance();

                return _parent.GetScalarStyle(_idx);
            }
        }

        /// <summary>
        ///   Gets the source style of this collection: <see cref="YamlCollectionStyle.Flow"/> for <c>[…]</c> or <c>{…}</c>
        ///   (JSON's collections among them) and for a single-pair mapping in a flow sequence (<c>[a: b]</c>),
        ///   <see cref="YamlCollectionStyle.Block"/> otherwise; <see langword="null"/> when this element is not an object or
        ///   an array. An alias gives its anchored node's, as <see cref="ValueKind"/> does. Writing the element back with a
        ///   block-style writer keeps it, unless <see cref="YamlWriterOptions.IgnoreSourceStyles"/> is set.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public YamlCollectionStyle? CollectionStyle
        {
            get
            {
                CheckValidInstance();

                return TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray
                    ? _parent.IsFlowCollection(_idx) ? YamlCollectionStyle.Flow : YamlCollectionStyle.Block
                    : null;
            }
        }

        /// <summary>
        ///   Gets a value that indicates whether this element was reached through an alias (<c>*name</c>). Such an element
        ///   is the anchored node: its kind, value and children are that node's.
        /// </summary>
        public bool IsAlias => _rowIdx != _idx;

        /// <summary>
        ///   Gets the original input data backing this value, returning it as a <see cref="string"/>.
        /// </summary>
        /// <returns>
        ///   The original input data backing this value, returning it as a <see cref="string"/>.
        /// </returns>
        /// <remarks>
        ///   For YAML the text stands alone: <see cref="YamlDocument.Parse(string, YamlDocumentOptions)"/> reads it back to
        ///   an equal value. A nested block collection's continuation lines lose the node's indentation, a flow-sequence
        ///   pair (<c>[a: b]</c>) is given its braces, the node's tag is kept in front, and a block scalar's indentation
        ///   indicator keeps its meaning. Three texts do not stand alone: a node with an alias whose anchor is outside the
        ///   node, a node whose tag needs a <c>%TAG</c> directive of its stream, and an empty node, whose text is empty
        ///   (which <see cref="YamlDocument.Parse(string, YamlDocumentOptions)"/> refuses). The source bytes, unchanged,
        ///   are <see cref="System.Runtime.InteropServices.YamlMarshal.GetRawUtf8Value(YamlElement)"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public string GetRawText()
        {
            CheckValidInstance();

            return _parent.GetRawValueAsString(_idx);
        }

        internal ReadOnlyMemory<byte> GetRawValue()
        {
            CheckValidInstance();

            return _parent.GetRawValue(_idx, includeQuotes: true);
        }

        internal string GetPropertyRawText()
        {
            CheckValidInstance();

            return _parent.GetPropertyRawValueAsString(_rowIdx);
        }

        internal bool ValueIsEscaped
        {
            get
            {
                CheckValidInstance();

                return _parent.ValueIsEscaped(_idx, isPropertyName: false);
            }
        }

        internal ReadOnlySpan<byte> ValueSpan
        {
            get
            {
                CheckValidInstance();

                return _parent.GetRawValue(_idx, includeQuotes: false).Span;
            }
        }

        // M7c: the scalar part of DeepEquals (its docs are on DeepEquals).
        private static bool ScalarsEqual(YamlElement element1, YamlElement element2, YamlValueKind kind)
        {
            switch (kind)
            {
                case YamlValueKind.Number:
                    return NumbersEqual(element1, element2);
                case YamlValueKind.String:
                    if (element2.ValueIsEscaped)
                    {
                        if (element1.ValueIsEscaped)
                        {
                            return YamlReaderHelper.UnescapeAndCompareBothInputs(element1.ValueSpan, element2.ValueSpan);
                        }

                        (element1, element2) = (element2, element1);
                    }

                    return element1.ValueEquals(element2.ValueSpan);
                default:
                    Debug.Assert(kind is YamlValueKind.Null or YamlValueKind.False or YamlValueKind.True);
                    return true;
            }
        }

        private static bool NumbersEqual(YamlElement element1, YamlElement element2)
        {
            ReadOnlySpan<byte> left = element1.GetRawValue().Span;
            ReadOnlySpan<byte> right = element2.GetRawValue().Span;
            if (YamlReaderHelper.IsJsonNumber(left) && YamlReaderHelper.IsJsonNumber(right))
            {
                return YamlHelpers.AreEqualYamlNumbers(left, right);
            }

            // A YAML spelling: by value, at any size (an integer by its digits, a decimal by System.Text.Json's digits
            // comparison, NaN equal to itself), as the collection-key identity compares them.
            return string.Equals(element1._parent.NumberNormalForm(element1._idx), element2._parent.NumberNormalForm(element2._idx), StringComparison.Ordinal);
        }

        // M5e: YamlProperty.KeyKind and GetKeyElement, for the property whose value this element is.
        internal YamlKeyKind GetPropertyKeyKind() => _parent.GetKeyKind(_rowIdx);

        /// <summary>M5e: this node's canonical form, with <paramref name="expanded"/> as the alias budget.</summary>
        internal string GetCanonicalForm(ref int expanded) => _parent.GetCanonicalForm(_idx, ref expanded);

        internal YamlElement GetPropertyKeyElement() => _parent.GetKeyElement(_rowIdx);

        /// <summary>
        /// M5e: a property's key for deep equality. A scalar key by its name, a collection key by its canonical
        /// form; the first character keeps the two kinds apart.
        /// </summary>
        internal static string KeyIdentity(YamlProperty property)
        {
            YamlDocument document = property.Value._parent;
            int nameRow = document.NameRowOf(property.Value._rowIdx);
            return document.IsComplexKeyRow(nameRow) ? "c" + document.GetCanonicalKey(nameRow) : "s" + property.Name;
        }

        private static bool KeyedObjectDeepEquals(YamlElement element1, YamlElement element2)
        {
            var right = new Dictionary<string, Queue<YamlElement>>(StringComparer.Ordinal);
            foreach (YamlProperty property in element2.EnumerateObject())
            {
                string name = KeyIdentity(property);
                if (!right.TryGetValue(name, out Queue<YamlElement>? values))
                {
                    right[name] = values = new Queue<YamlElement>();
                }

                values.Enqueue(property.Value);
            }

            foreach (YamlProperty property in element1.EnumerateObject())
            {
                if (!right.TryGetValue(KeyIdentity(property), out Queue<YamlElement>? values) || values.Count == 0
                    || !DeepEquals(property.Value, values.Dequeue()))
                {
                    return false;
                }
            }

            return true;
        }

        // DeepEquals over documents with aliases (M5a): the pairs already found equal, and the pairs on the current path
        // (a recursive alias compares as equal where both sides repeat alike), so each pair is compared once.
        private sealed class AliasDeepEquals
        {
            private readonly int _maxDepth;
            private readonly HashSet<long> _equal = new();
            private readonly HashSet<long> _onPath = new();

            internal AliasDeepEquals(int maxDepth) => _maxDepth = maxDepth;

            internal bool Equal(YamlElement a, YamlElement b, int depth)
            {
                if (depth > _maxDepth)
                {
                    ThrowHelper.ThrowYamlException_AliasTraversalTooDeep(_maxDepth);
                }

                long key = ((long)a._idx << 32) | (uint)b._idx;
                if (_equal.Contains(key) || _onPath.Contains(key))
                {
                    return true;
                }

                YamlValueKind kind = a.ValueKind;
                if (kind != b.ValueKind)
                {
                    return false;
                }

                bool result;
                switch (kind)
                {
                    case YamlValueKind.Array:
                        if (a.GetArrayLength() != b.GetArrayLength())
                        {
                            return false;
                        }

                        _onPath.Add(key);
                        result = true;
                        ArrayEnumerator right = b.EnumerateArray();
                        foreach (YamlElement left in a.EnumerateArray())
                        {
                            right.MoveNext();
                            if (!Equal(left, right.Current, depth + 1))
                            {
                                result = false;
                                break;
                            }
                        }

                        _onPath.Remove(key);
                        break;

                    case YamlValueKind.Object:
                        if (a.GetPropertyCount() != b.GetPropertyCount())
                        {
                            return false;
                        }

                        _onPath.Add(key);
                        result = ObjectsEqual(a, b, depth);
                        _onPath.Remove(key);
                        break;

                    default:
                        result = ScalarsEqual(a, b, kind);
                        break;
                }

                if (result)
                {
                    _equal.Add(key);
                }

                return result;
            }

            // Property names as a multiset, repeated names in order, as the alias-free path.
            private bool ObjectsEqual(YamlElement a, YamlElement b, int depth)
            {
                // M5e: collection keys pair up by their canonical form.
                bool keyed = a._parent.HasComplexKeys || b._parent.HasComplexKeys;
                var right = new Dictionary<string, Queue<YamlElement>>(StringComparer.Ordinal);
                foreach (YamlProperty property in b.EnumerateObject())
                {
                    string name = keyed ? KeyIdentity(property) : property.Name;
                    if (!right.TryGetValue(name, out Queue<YamlElement>? values))
                    {
                        right[name] = values = new Queue<YamlElement>();
                    }

                    values.Enqueue(property.Value);
                }

                foreach (YamlProperty property in a.EnumerateObject())
                {
                    if (!right.TryGetValue(keyed ? KeyIdentity(property) : property.Name, out Queue<YamlElement>? values) || values.Count == 0
                        || !Equal(property.Value, values.Dequeue(), depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Compares the values of two <see cref="YamlElement"/> values for equality, including the values of all descendant elements.
        /// </summary>
        /// <param name="element1">The first <see cref="YamlElement"/> to compare.</param>
        /// <param name="element2">The second <see cref="YamlElement"/> to compare.</param>
        /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Deep equality of two YAML values is defined as follows:
        /// <list type="bullet">
        /// <item>YAML values of different kinds are not equal.</item>
        /// <item>YAML constants <see langword="null"/>, <see langword="false"/>, and <see langword="true"/> only equal themselves.</item>
        /// <item>YAML numbers are equal if and only if they have they have equivalent decimal representations, with no rounding being used.</item>
        /// <item>YAML strings are equal if and only if they are equal using ordinal string comparison.</item>
        /// <item>YAML arrays are equal if and only if they are of equal length and each of their elements are pairwise equal.</item>
        /// <item>
        ///     YAML objects are equal if and only if they have the same number of properties and each property in the first object
        ///     has a corresponding property in the second object with the same name and equal value. The order of properties is not
        ///     significant, with the exception of repeated properties that must be specified in the same order (with interleaving allowed).
        /// </item>
        /// <item>
        ///     YAML (M5a): an alias compares as the node it refers to, and an alias inside the node it refers to compares
        ///     as equal where both sides repeat alike. Anchors, tags and scalar styles are not compared: equality is of
        ///     values, as for YAML. A tag that changes a value's kind shows through <see cref="ValueKind"/>. Numbers in a
        ///     YAML spelling (<c>0x1F</c>, <c>.inf</c>) compare by their value.
        /// </item>
        /// </list>
        /// A document with aliases is compared with a bound on depth (the document's <see cref="YamlDocumentOptions.MaxDepth"/>,
        /// counted across aliases), past which a <see cref="YamlException"/> is thrown.
        /// </remarks>
        public static bool DeepEquals(YamlElement element1, YamlElement element2)
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                ThrowHelper.ThrowInsufficientExecutionStackException_YamlElementDeepEqualsInsufficientExecutionStack();
            }

            element1.CheckValidInstance();
            element2.CheckValidInstance();

            if (element1._parent.HasAliases || element2._parent.HasAliases)
            {
                // M5a: aliases. Alias-free documents take the path below, which allocates nothing new.
                return new AliasDeepEquals(Math.Max(element1._parent.TraversalMaxDepth, element2._parent.TraversalMaxDepth)).Equal(element1, element2, depth: 0);
            }

            YamlValueKind kind = element1.ValueKind;
            if (kind != element2.ValueKind)
            {
                return false;
            }

            switch (kind)
            {
                case YamlValueKind.Null or YamlValueKind.False or YamlValueKind.True:
                    return true;

                case YamlValueKind.Number:
                    return NumbersEqual(element1, element2);

                case YamlValueKind.String:
                    if (element2.ValueIsEscaped)
                    {
                        if (element1.ValueIsEscaped)
                        {
                            // Need to unescape and compare both inputs.
                            return YamlReaderHelper.UnescapeAndCompareBothInputs(element1.ValueSpan, element2.ValueSpan);
                        }

                        // Swap values so that unescaping is handled by the LHS.
                        (element1, element2) = (element2, element1);
                    }

                    return element1.ValueEquals(element2.ValueSpan);

                case YamlValueKind.Array:
                    if (element1.GetArrayLength() != element2.GetArrayLength())
                    {
                        return false;
                    }

                    ArrayEnumerator arrayEnumerator2 = element2.EnumerateArray();
                    foreach (YamlElement e1 in element1.EnumerateArray())
                    {
                        bool success = arrayEnumerator2.MoveNext();
                        Debug.Assert(success, "enumerators must have matching length");

                        if (!DeepEquals(e1, arrayEnumerator2.Current))
                        {
                            return false;
                        }
                    }

                    Debug.Assert(!arrayEnumerator2.MoveNext());
                    return true;

                default:
                    Debug.Assert(kind is YamlValueKind.Object);

                    int count = element1.GetPropertyCount();
                    if (count != element2.GetPropertyCount())
                    {
                        return false;
                    }

                    if (element1._parent.HasComplexKeys || element2._parent.HasComplexKeys)
                    {
                        // M5e: collection keys pair up by their canonical form, not their raw text.
                        return KeyedObjectDeepEquals(element1, element2);
                    }

                    ObjectEnumerator objectEnumerator1 = element1.EnumerateObject();
                    ObjectEnumerator objectEnumerator2 = element2.EnumerateObject();

                    // Two JSON objects are considered equal if they define the same set of properties.
                    // Start optimistically with pairwise comparison, but fall back to unordered
                    // comparison as soon as a mismatch is encountered.

                    while (objectEnumerator1.MoveNext())
                    {
                        bool success = objectEnumerator2.MoveNext();
                        Debug.Assert(success, "enumerators should have matching lengths");

                        YamlProperty prop1 = objectEnumerator1.Current;
                        YamlProperty prop2 = objectEnumerator2.Current;

                        if (!NameEquals(prop1, prop2))
                        {
                            // We have our first mismatch, fall back to unordered comparison.
                            return UnorderedObjectDeepEquals(objectEnumerator1, objectEnumerator2, remainingProps: count);
                        }

                        if (!DeepEquals(prop1.Value, prop2.Value))
                        {
                            return false;
                        }

                        count--;
                    }

                    Debug.Assert(!objectEnumerator2.MoveNext());
                    return true;

                    static bool UnorderedObjectDeepEquals(ObjectEnumerator objectEnumerator1, ObjectEnumerator objectEnumerator2, int remainingProps)
                    {
                        // YamlElement objects allow duplicate property names, which is optional per the JSON RFC.
                        // Even though this implementation of equality does not take property ordering into account,
                        // repeated property names must be specified in the same order (although they may be interleaved).
                        // This is to preserve a degree of coherence with JSON serialization, where either the first
                        // or last occurrence of a repeated property name is used. It also simplifies the implementation
                        // and keeps it at O(n + m) complexity.

                        Dictionary<string, ValueQueue<YamlElement>> properties2 = new(capacity: remainingProps, StringComparer.Ordinal);
                        do
                        {
                            YamlProperty prop2 = objectEnumerator2.Current;
#if NET
                            ref ValueQueue<YamlElement> values = ref CollectionsMarshal.GetValueRefOrAddDefault(properties2, prop2.Name, out bool _);
#else
                            properties2.TryGetValue(prop2.Name, out ValueQueue<YamlElement> values);
#endif
                            values.Enqueue(prop2.Value);
#if !NET
                            properties2[prop2.Name] = values;
#endif
                        }
                        while (objectEnumerator2.MoveNext());

                        do
                        {
                            YamlProperty prop = objectEnumerator1.Current;
#if NET
                            ref ValueQueue<YamlElement> values = ref CollectionsMarshal.GetValueRefOrAddDefault(properties2, prop.Name, out bool exists);
#else
                            bool exists = properties2.TryGetValue(prop.Name, out ValueQueue<YamlElement> values);
#endif
                            if (!exists || !values.TryDequeue(out YamlElement value) || !DeepEquals(prop.Value, value))
                            {
                                return false;
                            }
#if !NET
                            properties2[prop.Name] = values;
#endif
                        }
                        while (objectEnumerator1.MoveNext());

                        return true;
                    }

                    static bool NameEquals(YamlProperty left, YamlProperty right)
                    {
                        if (right.NameIsEscaped)
                        {
                            if (left.NameIsEscaped)
                            {
                                // Need to unescape and compare both inputs.
                                return YamlReaderHelper.UnescapeAndCompareBothInputs(left.NameSpan, right.NameSpan);
                            }

                            // Swap values so that unescaping is handled by the LHS
                            (left, right) = (right, left);
                        }

                        return left.NameEquals(right.NameSpan);
                    }
            }
        }

        /// <summary>
        ///   Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        ///   <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        ///   the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public bool ValueEquals(string? text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == YamlTokenType.Null)
            {
                return text is null;
            }

            return TextEqualsHelper(text.AsSpan(), isPropertyName: false);
        }

        /// <summary>
        ///   Compares the text represented by <paramref name="utf8Text" /> to the string value of this element.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element has the same UTF-8 encoding as
        ///   <paramref name="utf8Text" />, <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of the string produced by UTF-8 decoding
        ///   <paramref name="utf8Text" /> with the result of calling <see cref="GetString" />, but avoids creating the
        ///   string instances.
        /// </remarks>
        public bool ValueEquals(ReadOnlySpan<byte> utf8Text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == YamlTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
                return utf8Text.Slice(0, 0) == default;
#pragma warning restore CA2265
            }

            return TextEqualsHelper(utf8Text, isPropertyName: false, shouldUnescape: true);
        }

        /// <summary>
        ///   Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        ///   <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        ///   the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public bool ValueEquals(ReadOnlySpan<char> text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == YamlTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
                return text.Slice(0, 0) == default;
#pragma warning restore CA2265
            }

            return TextEqualsHelper(text, isPropertyName: false);
        }

        internal bool TextEqualsHelper(ReadOnlySpan<byte> utf8Text, bool isPropertyName, bool shouldUnescape)
        {
            CheckValidInstance();

            return _parent.TextEquals(isPropertyName ? _rowIdx : _idx, utf8Text, isPropertyName, shouldUnescape);
        }

        internal bool TextEqualsHelper(ReadOnlySpan<char> text, bool isPropertyName)
        {
            CheckValidInstance();

            return _parent.TextEquals(isPropertyName ? _rowIdx : _idx, text, isPropertyName);
        }

        internal bool ValueIsEscapedHelper(bool isPropertyName)
        {
            CheckValidInstance();

            return _parent.ValueIsEscaped(isPropertyName ? _rowIdx : _idx, isPropertyName);
        }

        /// <summary>
        ///   Write the element into the provided writer as a YAML value.
        /// </summary>
        /// <remarks>
        ///   A block-style writer keeps the source's styles (each collection's flow or block style, each scalar's and
        ///   key's quoting or block style) where it can hold the text in them, unless
        ///   <see cref="YamlWriterOptions.IgnoreSourceStyles"/> is set, which writes in the writer's own styles.
        /// </remarks>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is <see cref="YamlValueKind.Undefined"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(Utf8YamlWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            CheckValidInstance();

            _parent.WriteElementTo(_idx, writer);
        }

        internal void WritePropertyNameTo(Utf8YamlWriter writer)
        {
            CheckValidInstance();

            _parent.WritePropertyName(_rowIdx, writer);
        }

        /// <summary>
        ///   Get an enumerator to enumerate the values in the YAML array represented by this YamlElement.
        /// </summary>
        /// <returns>
        ///   An enumerator to enumerate the values in the YAML array represented by this YamlElement.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public ArrayEnumerator EnumerateArray()
        {
            CheckValidInstance();

            YamlTokenType tokenType = TokenType;

            if (tokenType != YamlTokenType.StartArray)
            {
                ThrowHelper.ThrowYamlElementWrongTypeException(YamlTokenType.StartArray, tokenType);
            }

            return new ArrayEnumerator(this);
        }

        /// <summary>
        ///   Get an enumerator to enumerate the properties in the YAML object represented by this YamlElement.
        /// </summary>
        /// <returns>
        ///   An enumerator to enumerate the properties in the YAML object represented by this YamlElement.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="YamlValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public ObjectEnumerator EnumerateObject()
        {
            CheckValidInstance();

            YamlTokenType tokenType = TokenType;

            if (tokenType != YamlTokenType.StartObject)
            {
                ThrowHelper.ThrowYamlElementWrongTypeException(YamlTokenType.StartObject, tokenType);
            }

            return new ObjectEnumerator(this);
        }

        /// <summary>
        ///   Gets a string representation for the current value appropriate to the value type.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     For YamlElement built from <see cref="YamlDocument"/>:
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="YamlValueKind.Null"/>, <see cref="string.Empty"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="YamlValueKind.True"/>, <see cref="bool.TrueString"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="YamlValueKind.False"/>, <see cref="bool.FalseString"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="YamlValueKind.String"/>, the value of <see cref="GetString"/>() is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For other types, the value of <see cref="GetRawText"/>() is returned.
        ///   </para>
        /// </remarks>
        /// <returns>
        ///   A string representation for the current value appropriate to the value type.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="YamlDocument"/> has been disposed.
        /// </exception>
        public override string ToString()
        {
            switch (TokenType)
            {
                case YamlTokenType.None:
                case YamlTokenType.Null:
                    return string.Empty;
                case YamlTokenType.True:
                    return bool.TrueString;
                case YamlTokenType.False:
                    return bool.FalseString;
                case YamlTokenType.Number:
                case YamlTokenType.StartArray:
                case YamlTokenType.StartObject:
                    {
                        // null parent should have hit the None case
                        Debug.Assert(_parent is not null);
                        return _parent.GetRawValueAsString(_idx);
                    }
                case YamlTokenType.String:
                    return GetString()!;
                case YamlTokenType.Comment:
                case YamlTokenType.EndArray:
                case YamlTokenType.EndObject:
                default:
                    Debug.Fail($"No handler for {nameof(YamlTokenType)}.{TokenType}");
                    return string.Empty;
            }
        }

        /// <summary>
        ///   Get a YamlElement which can be safely stored beyond the lifetime of the
        ///   original <see cref="YamlDocument"/>.
        /// </summary>
        /// <returns>
        ///   A YamlElement which can be safely stored beyond the lifetime of the
        ///   original <see cref="YamlDocument"/>.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If this YamlElement is itself the output of a previous call to Clone, or
        ///     a value contained within another YamlElement which was the output of a previous
        ///     call to Clone, this method results in no additional memory allocation.
        ///   </para>
        /// </remarks>
        public YamlElement Clone()
        {
            CheckValidInstance();

            if (!_parent.IsDisposable)
            {
                return this;
            }

            return _parent.CloneElement(_idx);
        }

        private void CheckValidInstance()
        {
            if (_parent is null)
            {
                throw new InvalidOperationException();
            }
        }

        internal readonly int MetadataDbIndex => _idx;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ValueKind = {ValueKind} : \"{ToString()}\"";
    }
}
