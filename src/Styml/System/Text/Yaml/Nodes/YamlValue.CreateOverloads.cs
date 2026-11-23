// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Nodes
{
    public partial class YamlValue
    {
        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(bool value, YamlNodeOptions? options = null) => new YamlValuePrimitive<bool>(value, YamlMetadataServices.BooleanConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(bool? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<bool>(value.Value, YamlMetadataServices.BooleanConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(byte value, YamlNodeOptions? options = null) => new YamlValuePrimitive<byte>(value, YamlMetadataServices.ByteConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(byte? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<byte>(value.Value, YamlMetadataServices.ByteConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(char value, YamlNodeOptions? options = null) => new YamlValuePrimitive<char>(value, YamlMetadataServices.CharConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(char? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<char>(value.Value, YamlMetadataServices.CharConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(DateTime value, YamlNodeOptions? options = null) => new YamlValuePrimitive<DateTime>(value, YamlMetadataServices.DateTimeConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(DateTime? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<DateTime>(value.Value, YamlMetadataServices.DateTimeConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(DateTimeOffset value, YamlNodeOptions? options = null) => new YamlValuePrimitive<DateTimeOffset>(value, YamlMetadataServices.DateTimeOffsetConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(DateTimeOffset? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<DateTimeOffset>(value.Value, YamlMetadataServices.DateTimeOffsetConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(decimal value, YamlNodeOptions? options = null) => new YamlValuePrimitive<decimal>(value, YamlMetadataServices.DecimalConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(decimal? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<decimal>(value.Value, YamlMetadataServices.DecimalConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(double value, YamlNodeOptions? options = null) => new YamlValuePrimitive<double>(value, YamlMetadataServices.DoubleConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(double? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<double>(value.Value, YamlMetadataServices.DoubleConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(Guid value, YamlNodeOptions? options = null) => new YamlValuePrimitive<Guid>(value, YamlMetadataServices.GuidConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(Guid? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<Guid>(value.Value, YamlMetadataServices.GuidConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(short value, YamlNodeOptions? options = null) => new YamlValuePrimitive<short>(value, YamlMetadataServices.Int16Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(short? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<short>(value.Value, YamlMetadataServices.Int16Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(int value, YamlNodeOptions? options = null) => new YamlValuePrimitive<int>(value, YamlMetadataServices.Int32Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(int? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<int>(value.Value, YamlMetadataServices.Int32Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(long value, YamlNodeOptions? options = null) => new YamlValuePrimitive<long>(value, YamlMetadataServices.Int64Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(long? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<long>(value.Value, YamlMetadataServices.Int64Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue Create(sbyte value, YamlNodeOptions? options = null) => new YamlValuePrimitive<sbyte>(value, YamlMetadataServices.SByteConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue? Create(sbyte? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<sbyte>(value.Value, YamlMetadataServices.SByteConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue Create(float value, YamlNodeOptions? options = null) => new YamlValuePrimitive<float>(value, YamlMetadataServices.SingleConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(float? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<float>(value.Value, YamlMetadataServices.SingleConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [return: NotNullIfNotNull(nameof(value))]
        public static YamlValue? Create(string? value, YamlNodeOptions? options = null) => value is not null ? new YamlValuePrimitive<string>(value, YamlMetadataServices.StringConverter!, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue Create(ushort value, YamlNodeOptions? options = null) => new YamlValuePrimitive<ushort>(value, YamlMetadataServices.UInt16Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue? Create(ushort? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<ushort>(value.Value, YamlMetadataServices.UInt16Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue Create(uint value, YamlNodeOptions? options = null) => new YamlValuePrimitive<uint>(value, YamlMetadataServices.UInt32Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue? Create(uint? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<uint>(value.Value, YamlMetadataServices.UInt32Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue Create(ulong value, YamlNodeOptions? options = null) => new YamlValuePrimitive<ulong>(value, YamlMetadataServices.UInt64Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static YamlValue? Create(ulong? value, YamlNodeOptions? options = null) => value.HasValue ? new YamlValuePrimitive<ulong>(value.Value, YamlMetadataServices.UInt64Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(YamlElement value, YamlNodeOptions? options = null) => YamlValue.CreateFromElement(ref value, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="YamlValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create(YamlElement? value, YamlNodeOptions? options = null) => value is YamlElement element ? YamlValue.CreateFromElement(ref element, options) : null;
    }
}
