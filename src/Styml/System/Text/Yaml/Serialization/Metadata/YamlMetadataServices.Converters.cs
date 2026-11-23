// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Converters;

namespace System.Text.Yaml.Serialization.Metadata
{
    public static partial class YamlMetadataServices
    {
        // When adding/removing a built-in YamlConverter property below, also update
        // gen/YamlSourceGenerator.Parser.cs::GetSupportedYamlValueTypes so the union ambiguity
        // diagnostic (STYML1227) agrees with YamlTypeInfo.BuildUnionValueTypeMap.

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="bool"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<bool> BooleanConverter => field ??= new BooleanConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts byte array values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<byte[]?> ByteArrayConverter => field ??= new ByteArrayConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="byte"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<byte> ByteConverter => field ??= new ByteConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="char"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<char> CharConverter => field ??= new CharConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="DateTime"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<DateTime> DateTimeConverter => field ??= new DateTimeConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="DateTimeOffset"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<DateTimeOffset> DateTimeOffsetConverter => field ??= new DateTimeOffsetConverter();

#if NET
        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="DateOnly"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<DateOnly> DateOnlyConverter => field ??= new DateOnlyConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="TimeOnly"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<TimeOnly> TimeOnlyConverter => field ??= new TimeOnlyConverter();
#endif

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="decimal"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<decimal> DecimalConverter => field ??= new DecimalConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="double"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<double> DoubleConverter => field ??= new DoubleConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Guid"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Guid> GuidConverter => field ??= new GuidConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="short"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<short> Int16Converter => field ??= new Int16Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="int"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<int> Int32Converter => field ??= new Int32Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="long"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<long> Int64Converter => field ??= new Int64Converter();

#if NET
        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Int128"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Int128> Int128Converter => field ??= new Int128Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="UInt128"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static YamlConverter<UInt128> UInt128Converter => field ??= new UInt128Converter();
#endif

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlArray"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlArray?> YamlArrayConverter => field ??= new YamlArrayConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlElement"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlElement> YamlElementConverter => field ??= new YamlElementConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlNode"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlNode?> YamlNodeConverter => field ??= new YamlNodeConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlObject"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlObject?> YamlObjectConverter => field ??= new YamlObjectConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlArray"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlValue?> YamlValueConverter => field ??= new YamlValueConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="YamlDocument"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<YamlDocument?> YamlDocumentConverter => field ??= new YamlDocumentConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Memory{Byte}"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Memory<byte>> MemoryByteConverter => field ??= new MemoryByteConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="ReadOnlyMemory{Byte}"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<ReadOnlyMemory<byte>> ReadOnlyMemoryByteConverter => field ??= new ReadOnlyMemoryByteConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="object"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<object?> ObjectConverter => field ??= new DefaultObjectConverter();

#if NET
        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Half"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Half> HalfConverter => field ??= new HalfConverter();
#endif

#if NET11_0_OR_GREATER
        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="System.Numerics.BFloat16"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<System.Numerics.BFloat16> BFloat16Converter =>
            field ??= new Ieee754FloatingPointConverter<System.Numerics.BFloat16>(NumericType.BFloat16);

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="System.Numerics.Decimal32"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<System.Numerics.Decimal32> Decimal32Converter =>
            field ??= new Ieee754FloatingPointConverter<System.Numerics.Decimal32>(NumericType.Decimal32);

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="System.Numerics.Decimal64"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<System.Numerics.Decimal64> Decimal64Converter =>
            field ??= new Ieee754FloatingPointConverter<System.Numerics.Decimal64>(NumericType.Decimal64);

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="System.Numerics.Decimal128"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<System.Numerics.Decimal128> Decimal128Converter =>
            field ??= new Ieee754FloatingPointConverter<System.Numerics.Decimal128>(NumericType.Decimal128);
#endif

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="float"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<float> SingleConverter => field ??= new SingleConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="sbyte"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static YamlConverter<sbyte> SByteConverter => field ??= new SByteConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="string"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<string?> StringConverter => field ??= new StringConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="TimeSpan"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<TimeSpan> TimeSpanConverter => field ??= new TimeSpanConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="ushort"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static YamlConverter<ushort> UInt16Converter => field ??= new UInt16Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="uint"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static YamlConverter<uint> UInt32Converter => field ??= new UInt32Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="ulong"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static YamlConverter<ulong> UInt64Converter => field ??= new UInt64Converter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Uri"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Uri?> UriConverter => field ??= new UriConverter();

        /// <summary>
        /// Returns a <see cref="YamlConverter{T}"/> instance that converts <see cref="Version"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<Version?> VersionConverter => field ??= new VersionConverter();

        /// <summary>
        /// Creates a <see cref="YamlConverter{T}"/> instance that throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <typeparam name="T">The generic definition for the type.</typeparam>
        /// <returns>A <see cref="YamlConverter{T}"/> instance that throws <see cref="NotSupportedException"/></returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<T> GetUnsupportedTypeConverter<T>()
            => new UnsupportedTypeConverter<T>();

        /// <summary>
        /// Creates a <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the enum type.</typeparam>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <returns>A <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T"/> values.</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<T> GetEnumConverter<T>(YamlSerializerOptions options) where T : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(options);

            return EnumConverterFactory.Helpers.Create<T>(EnumConverterOptions.AllowNumbers, options);
        }

        /// <summary>
        /// Creates a <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T?"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the underlying nullable type.</typeparam>
        /// <param name="underlyingTypeInfo">Serialization metadata for the underlying nullable type.</param>
        /// <returns>A <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T?"/> values</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<T?> GetNullableConverter<T>(YamlTypeInfo<T> underlyingTypeInfo) where T : struct
        {
            ArgumentNullException.ThrowIfNull(underlyingTypeInfo);

            YamlConverter<T> underlyingConverter = GetTypedConverter<T>(underlyingTypeInfo.Converter);

            return new NullableConverter<T>(underlyingConverter);
        }

        /// <summary>
        /// Creates a <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T?"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the underlying nullable type.</typeparam>
        /// <param name="options">The <see cref="YamlSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <returns>A <see cref="YamlConverter{T}"/> instance that converts <typeparamref name="T?"/> values</returns>
        /// <remarks>This API is for use by the output of the System.Text.Yaml source generator and should not be called directly.</remarks>
        public static YamlConverter<T?> GetNullableConverter<T>(YamlSerializerOptions options) where T : struct
        {
            ArgumentNullException.ThrowIfNull(options);

            YamlConverter<T> underlyingConverter = GetTypedConverter<T>(options.GetConverterInternal(typeof(T)));

            return new NullableConverter<T>(underlyingConverter);
        }

        internal static YamlConverter<T> GetTypedConverter<T>(YamlConverter converter)
        {
            YamlConverter<T>? typedConverter = converter as YamlConverter<T>;
            if (typedConverter is null)
            {
                throw new InvalidOperationException(SR.Format(SR.SerializationConverterNotCompatible, typedConverter, typeof(T)));
            }

            return typedConverter;
        }
    }
}
