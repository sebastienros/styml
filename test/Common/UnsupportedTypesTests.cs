// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract class UnsupportedTypesTests : SerializerTests
    {
        private bool SupportsYamlPathOnSerialize { get; init; }

        public UnsupportedTypesTests(
            YamlSerializerWrapper serializerWrapper,
            bool supportsYamlPathOnSerialize) : base(serializerWrapper)
        {
            SupportsYamlPathOnSerialize = supportsYamlPathOnSerialize;
        }

        [Theory]
        [MemberData(nameof(GetUnsupportedValues))]
        public async Task DeserializeUnsupportedType<T>(ValueWrapper<T> wrapper)
        {
            _ = wrapper; // only used to instantiate T

            Assert.NotNull(Serializer.GetTypeInfo(typeof(T))); // It should be possible to obtain metadata for the type.

            string yaml = """
                "Some string"
                """; // Any test payload is fine.

            Type type = GetNullableOfTUnderlyingType(typeof(T), out bool isNullableOfT);
            string fullName = type.FullName;

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<T>(yaml));
            string exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);
            Assert.Contains("$", exAsStr);

            yaml = $@"{{""Prop"":{yaml}}}";

            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.DeserializeWrapper<ClassWithType<T>>(yaml));
            exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);
            Assert.Contains("$.Prop", exAsStr);

            // Verify Nullable<> semantics. NSE is not thrown because the serializer handles null.
            if (isNullableOfT)
            {
                Assert.Null(await Serializer.DeserializeWrapper<T>("null"));

                yaml = $@"{{""Prop"":null}}";
                ClassWithType<T> obj = await Serializer.DeserializeWrapper<ClassWithType<T>>(yaml);
                Assert.Null(obj.Prop);
            }
        }

        [Theory]
        [MemberData(nameof(GetUnsupportedValues))]
        [Trait("category", "jsonoutput")]
        public async Task SerializeUnsupportedType<T>(ValueWrapper<T> wrapper)
        {
            T value = wrapper.value;

            Assert.NotNull(Serializer.GetTypeInfo(typeof(T))); // It should be possible to obtain metadata for the type.

            Type type = GetNullableOfTUnderlyingType(typeof(T), out bool isNullableOfT);
            string fullName = type.FullName;

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(value));
            string exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);
            Assert.Contains("$", exAsStr);

            ClassWithType<T> obj = new ClassWithType<T> { Prop = value };
            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(obj));
            exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);

            if (SupportsYamlPathOnSerialize)
            {
                Assert.Contains("$.Prop", exAsStr);
            }
            else
            {
                Assert.Contains("$.", exAsStr);
                Assert.DoesNotContain("$.Prop", exAsStr);
            }

            // Verify null semantics. NSE is not thrown because the serializer handles null.
            if (!type.IsValueType || isNullableOfT)
            {
                string serialized = await Serializer.SerializeWrapper<T>((T)(object)null);
                Assert.Equal("null", serialized);

                obj.Prop = (T)(object)null;
                serialized = await Serializer.SerializeWrapper(obj);
                Assert.Equal("""{"Prop":null}""", serialized);

                serialized = await Serializer.SerializeWrapper(obj, new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull });
                Assert.Equal("{}", serialized);
            }

#if !BUILDING_SOURCE_GENERATOR_TESTS
            // The reflection-based serializer will report the runtime type and not the declared type.
            fullName = GetNullableOfTUnderlyingType(value.GetType(), out bool _).FullName;
#endif

            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper<object>(value));
            exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);
            Assert.Contains("$", exAsStr);

            ClassWithType<object> polyObj = new ClassWithType<object> { Prop = value };
            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(polyObj));
            exAsStr = ex.ToString();
            Assert.Contains(fullName, exAsStr);
        }

        public static IEnumerable<object[]> GetUnsupportedValues()
        {
            yield return WrapArgs(new int[,] { { 1, 0 }, { 0, 1 } });
            yield return WrapArgs(new bool[,,,] { { { { false } } } });
            yield return WrapArgs(typeof(int));
            yield return WrapArgs(typeof(ClassWithExtensionProperty).GetConstructor(Array.Empty<Type>()));
            yield return WrapArgs(typeof(ClassWithExtensionProperty).GetProperty(nameof(ClassWithExtensionProperty.MyInt)));
            yield return WrapArgs(new SerializationInfo(typeof(Type), new FormatterConverter()));
            yield return WrapArgs((IntPtr)123);
            yield return WrapArgs<IntPtr?>(new IntPtr(123)); // One nullable variation.
            yield return WrapArgs((UIntPtr)123);

            static object[] WrapArgs<T>(T value) => new object[] { new ValueWrapper<T>(value) };
        }

        // Helper record used to path both value & type information to generic theories.
        // This is needed e.g. when passing System.Type instances whose runtime type
        // actually is System.Reflection.RuntimeType.
        public record ValueWrapper<T>(T value)
        {
            public override string ToString() => value.ToString();
        }

        public class ClassWithIntPtr
        {
            public IntPtr MyIntPtr { get; set; }
        }

        public class ClassWithIntPtrConverter
        {
            [YamlConverter(typeof(IntPtrConverter))]
            public IntPtr MyIntPtr { get; set; }
        }

        public class IntPtrConverter : YamlConverter<IntPtr>
        {
            public override IntPtr Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                int value = reader.GetInt32();
                return new IntPtr(value);
            }

            public override void Write(Utf8YamlWriter writer, IntPtr value, YamlSerializerOptions options)
            {
                writer.WriteNumberValue(value.ToInt32());
            }
        }

        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "NullableContextAttribute is public in corelib in .NET 8+")]
        [RequiresUnreferencedCode("Uses reflection to construct and serialize an arbitrary runtime type.")]
        [RequiresDynamicCode("Uses reflection to construct and serialize an arbitrary runtime type.")]
        public async Task TypeWithNullConstructorParameterName_ThrowsNotSupportedException()
        {
            // Regression test for https://github.com/dotnet/runtime/issues/58690
            Type type = Assembly.GetExecutingAssembly().GetType("System.Runtime.CompilerServices.NullableContextAttribute")!;
            ConstructorInfo ctorInfo = type.GetConstructor(new Type[] { typeof(byte) });
            Assert.True(string.IsNullOrEmpty(ctorInfo.GetParameters()[0].Name));
            object value = ctorInfo.Invoke(new object[] { (byte)0 });

            await Assert.ThrowsAnyAsync<NotSupportedException>(() => Serializer.SerializeWrapper(value));
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => Serializer.DeserializeWrapper("{}", type));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RuntimeConverterIsSupported_IntPtr()
        {
            const string Yaml = "{\"MyIntPtr\":42}";
            string serialized;
            YamlSerializerOptions options = new();
            options.Converters.Add(new IntPtrConverter());

            serialized = await Serializer.SerializeWrapper(new IntPtr(42), options);
            Assert.Equal("42", serialized);

            IntPtr intPtr = await Serializer.DeserializeWrapper<IntPtr>("42", options);
            Assert.Equal(42, intPtr.ToInt32());

            ClassWithIntPtr obj = new() { MyIntPtr = new IntPtr(42) };
            serialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal(Yaml, serialized);

            obj = await Serializer.DeserializeWrapper<ClassWithIntPtr>(Yaml, options);
            Assert.Equal(42, obj.MyIntPtr.ToInt32());
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CompileTimeConverterIsSupported_IntPtr()
        {
            const string Yaml = "{\"MyIntPtr\":42}";

            ClassWithIntPtrConverter obj = new() { MyIntPtr = new IntPtr(42) };
            string serialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(Yaml, serialized);

            obj = await Serializer.DeserializeWrapper<ClassWithIntPtrConverter>(Yaml);
            Assert.Equal(42, obj.MyIntPtr.ToInt32());
        }

        public class ClassWithAsyncEnumerableConverter
        {
            [YamlConverter(typeof(AsyncEnumerableConverter))]
            public ClassThatImplementsIAsyncEnumerable MyAsyncEnumerable { get; set; }
        }

        public class ClassThatImplementsIAsyncEnumerable : IAsyncEnumerable<int>
        {
            public string Status { get; set; } = "Created";

            // Should not be called.
            IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken) => throw new NotImplementedException();
        }

        public class AsyncEnumerableConverter : YamlConverter<ClassThatImplementsIAsyncEnumerable>
        {
            public override ClassThatImplementsIAsyncEnumerable Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                Assert.Equal(YamlTokenType.StartArray, reader.TokenType);
                reader.Read();
                Assert.Equal(YamlTokenType.EndArray, reader.TokenType);
                return new ClassThatImplementsIAsyncEnumerable { Status = "Read" };
            }

            public override void Write(Utf8YamlWriter writer, ClassThatImplementsIAsyncEnumerable value, YamlSerializerOptions options)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                value.Status = "Write";
            }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task RuntimeConverterIsSupported_AsyncEnumerable()
        {
            const string Yaml = "{\"MyAsyncEnumerable\":[]}";

            string serialized;
            YamlSerializerOptions options = new();
            options.Converters.Add(new AsyncEnumerableConverter());

            ClassThatImplementsIAsyncEnumerable obj = new();
            Assert.Equal("Created", obj.Status);
            serialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal("[]", serialized);
            Assert.Equal("Write", obj.Status);
            obj = await Serializer.DeserializeWrapper<ClassThatImplementsIAsyncEnumerable>("[]", options);
            Assert.Equal("Read", obj.Status);

            ClassWithAsyncEnumerableConverter poco = new();
            poco.MyAsyncEnumerable = new ClassThatImplementsIAsyncEnumerable();
            Assert.Equal("Created", poco.MyAsyncEnumerable.Status);
            serialized = await Serializer.SerializeWrapper(poco, options);
            Assert.Equal(Yaml, serialized);
            Assert.Equal("Write", poco.MyAsyncEnumerable.Status);
            poco = await Serializer.DeserializeWrapper<ClassWithAsyncEnumerableConverter>(Yaml, options);
            Assert.Equal("Read", poco.MyAsyncEnumerable.Status);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task CompileTimeConverterIsSupported_AsyncEnumerable()
        {
            const string Yaml = "{\"MyAsyncEnumerable\":[]}";

            ClassWithAsyncEnumerableConverter obj = new();
            obj.MyAsyncEnumerable = new ClassThatImplementsIAsyncEnumerable();
            Assert.Equal("Created", obj.MyAsyncEnumerable.Status);

            string serialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(Yaml, serialized);
            Assert.Equal("Write", obj.MyAsyncEnumerable.Status);

            obj = await Serializer.DeserializeWrapper<ClassWithAsyncEnumerableConverter>(Yaml);
            Assert.Equal("Read", obj.MyAsyncEnumerable.Status);
        }

        public static Type GetNullableOfTUnderlyingType(Type type, out bool isNullableOfT)
        {
            isNullableOfT = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
            return isNullableOfT ? type.GetGenericArguments()[0] : type;
        }
    }
}
