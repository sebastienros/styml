// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Encodings.Web;
using Microsoft.DotNet.RemoteExecutor;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ExceptionTests
    {

        [Fact]
        public static void TypeMismatchIDictionaryExceptionWithCustomEscaperThrown()
        {
            var options = new YamlSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            YamlException e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("{\"Key\u0467\":1", options));
            Assert.Equal(0, e.LineNumber);
            Assert.Equal(10, e.BytePositionInLine);
            Assert.Contains("LineNumber: 0 | BytePositionInLine: 10.", e.Message);
            Assert.Contains("$.Key\u0467", e.Path);

            // Verify Path is not repeated.
            Assert.True(e.Message.IndexOf("Path:") == e.Message.LastIndexOf("Path:"));
        }

        [Fact]
        public static void PathForDictionaryFails()
        {
            try
            {
                YamlSerializer.Deserialize<Dictionary<string, int>>("""{"Key1":1, "Key2":bad}""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Key2", e.Path);
            }

            try
            {
                YamlSerializer.Deserialize<Dictionary<string, int>>("""{"Key1":1, "Key2":""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Key2", e.Path);
            }

            try
            {
                YamlSerializer.Deserialize<Dictionary<string, int>>("""
                    {"Key1":1, "Key2"
                    """);
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                // Key2 is not yet a valid key name since there is no : delimiter.
                Assert.Equal("$.Key1", e.Path);
            }
        }

        [Fact]
        public static void DeserializePathForDictionaryFails()
        {
            const string Yaml = "{\"Key1\u0467\":1, \"Key2\u0467\":bad}";
            const string YamlEscaped = "{\"Key1\\u0467\":1, \"Key2\\u0467\":bad}";
            const string Expected = "$.Key2\u0467";

            YamlException e;

            // Without custom escaper.
            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(Yaml));
            Assert.Equal(Expected, e.Path);

            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(YamlEscaped));
            Assert.Equal(Expected, e.Path);

            // Custom escaper should not change Path.
            var options = new YamlSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(Yaml, options));
            Assert.Equal(Expected, e.Path);

            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>(YamlEscaped, options));
            Assert.Equal(Expected, e.Path);
        }

        private class ClassWithUnicodePropertyName
        {
            public int Property\u04671 { get; set; } // contains a trailing "1"
        }

        [Fact]
        public static void DeserializePathForObjectFails()
        {
            const string GoodYaml = "{\"Property\u04671\":1}";
            const string GoodYamlEscaped = "{\"Property\\u04671\":1}";
            const string BadYaml = "{\"Property\u04671\":bad}";
            const string BadYamlEscaped = "{\"Property\\u04671\":bad}";
            const string Expected = "$.Property\u04671";

            ClassWithUnicodePropertyName obj;

            // Baseline.
            obj = YamlSerializer.Deserialize<ClassWithUnicodePropertyName>(GoodYaml);
            Assert.Equal(1, obj.Property\u04671);

            obj = YamlSerializer.Deserialize<ClassWithUnicodePropertyName>(GoodYamlEscaped);
            Assert.Equal(1, obj.Property\u04671);

            YamlException e;

            // Exception.
            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithUnicodePropertyName>(BadYaml));
            Assert.Equal(Expected, e.Path);

            e = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ClassWithUnicodePropertyName>(BadYamlEscaped));
            Assert.Equal(Expected, e.Path);
        }

        [Fact]
        public static void PathForListFails()
        {
            try
            {
                YamlSerializer.Deserialize<List<int>>("[1, bad]");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$[1]", e.Path);
            }
        }

        [Fact]
        public static void PathFor2dArrayFails()
        {
            try
            {
                YamlSerializer.Deserialize<int[][]>("[[1, 2],[3,bad]]");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$[1][1]", e.Path);
            }
        }

        [Fact]
        public static void PathFor2dListFails()
        {
            try
            {
                YamlSerializer.Deserialize<List<List<int>>>("[[1, 2],[3,bad]]");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$[1][1]", e.Path);
            }
        }

        [Fact]
        public static void PathForChildPropertyFails()
        {
            try
            {
                YamlSerializer.Deserialize<RootClass>("""{"Child":{"MyInt":bad]}""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Child.MyInt", e.Path);
            }
        }

        [Fact]
        public static void PathForChildListFails()
        {
            try
            {
                YamlSerializer.Deserialize<RootClass>("""{"Child":{"MyIntArray":[1, bad]}""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Child.MyIntArray[1]", e.Path);
            }
        }

        [Fact]
        public static void PathForChildDictionaryFails()
        {
            try
            {
                YamlSerializer.Deserialize<RootClass>("""{"Child":{"MyDictionary":{"Key": bad]""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Child.MyDictionary.Key", e.Path);
            }
        }

        [Fact]
        public static void PathForSpecialCharacterFails()
        {
            try
            {
                YamlSerializer.Deserialize<RootClass>("""
                    {"Child":{"MyDictionary":{"Key1":{"Children":[{"MyDictionary":{"K.e.y":"
                    """);
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Child.MyDictionary.Key1.Children[0].MyDictionary['K.e.y']", e.Path);
            }
        }

        [Fact]
        public static void PathForSpecialCharacterNestedFails()
        {
            try
            {
                YamlSerializer.Deserialize<RootClass>("""{"Child":{"Children":[{}, {"MyDictionary":{"K.e.y": {"MyInt":bad""");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.Child.Children[1].MyDictionary['K.e.y'].MyInt", e.Path);
            }
        }

        [Fact]
        public static void EscapingFails()
        {
            try
            {
                ClassWithUnicodeProperty obj = YamlSerializer.Deserialize<ClassWithUnicodeProperty>("{\"A\u0467\":bad}");
                Assert.Fail("Expected YamlException was not thrown.");
            }
            catch (YamlException e)
            {
                Assert.Equal("$.A\u0467", e.Path);
            }
        }

        [Fact]
        public static void CaseInsensitiveFails()
        {
            var options = new YamlSerializerOptions();
            options.PropertyNameCaseInsensitive = true;

            // Baseline (no exception)
            {
                SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>("""{"myint32":1}""", options);
                Assert.Equal(1, obj.MyInt32);
            }

            {
                SimpleTestClass obj = YamlSerializer.Deserialize<SimpleTestClass>("""{"MYINT32":1}""", options);
                Assert.Equal(1, obj.MyInt32);
            }

            try
            {
                YamlSerializer.Deserialize<SimpleTestClass>("""{"myint32":bad}""", options);
            }
            catch (YamlException e)
            {
                // The Path should reflect the case even though it is different from the property.
                Assert.Equal("$.myint32", e.Path);
            }

            try
            {
                YamlSerializer.Deserialize<SimpleTestClass>("""{"MYINT32":bad}""", options);
            }
            catch (YamlException e)
            {
                // Verify the previous json property name was not cached.
                Assert.Equal("$.MYINT32", e.Path);
            }
        }

        public class RootClass
        {
            public ChildClass Child { get; set; }
        }

        public class ChildClass
        {
            public int MyInt { get; set; }
            public int[] MyIntArray { get; set; }
            public Dictionary<string, ChildClass> MyDictionary { get; set; }
            public ChildClass[] Children { get; set; }
        }

        private class ClassWithPropertyToClassWithInvalidArray
        {
            public ClassWithInvalidArray Inner { get; set; } = new ClassWithInvalidArray();
        }

        private class ClassWithInvalidArray
        {
            public int[,] UnsupportedArray { get; set; }
        }

        private class ClassWithInvalidDictionary
        {
            public Dictionary<string, int[,]>? UnsupportedDictionary { get; set; }
        }

        [Fact]
        public static void ClassWithUnsupportedArray()
        {
            Exception ex = Assert.Throws<NotSupportedException>(() =>
                YamlSerializer.Deserialize<ClassWithInvalidArray>("""{"UnsupportedArray":[[]]}"""));

            // The exception contains the type.
            Assert.Contains(typeof(int[,]).ToString(), ex.Message);

            ex = Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(new ClassWithInvalidArray { UnsupportedArray = new int[,] { } }));
            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
        }

        [Fact]
        public static void ClassWithUnsupportedArrayInProperty()
        {
            Exception ex = Assert.Throws<NotSupportedException>(() =>
                YamlSerializer.Deserialize<ClassWithPropertyToClassWithInvalidArray>("""{"Inner":{"UnsupportedArray":[[]]}}"""));

            // The exception contains the type and Path.
            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
            Assert.Contains("Path: $.Inner.UnsupportedArray | LineNumber: 0 | BytePositionInLine: 30.", ex.Message);

            ex = Assert.Throws<NotSupportedException>(() =>
                YamlSerializer.Serialize(new ClassWithPropertyToClassWithInvalidArray { Inner = new() { UnsupportedArray = new int[,] { } } }));

            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
            Assert.Contains("Path: $.Inner.UnsupportedArray", ex.Message);

            // The original exception contains the type.
            Assert.NotNull(ex.InnerException);
            Assert.Contains(typeof(int[,]).ToString(), ex.InnerException.Message);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void ClassWithUnsupportedDictionary()
        {
            Exception ex = Assert.Throws<NotSupportedException>(() =>
                YamlSerializer.Deserialize<ClassWithInvalidDictionary>("""{"UnsupportedDictionary":{"key":{}}}"""));

            Assert.Contains("System.Int32[,]", ex.Message);

            // The exception for element types does not contain the parent type and the property name
            // since the verification occurs later and is no longer bound to the parent type.
            Assert.DoesNotContain("ClassWithInvalidDictionary.UnsupportedDictionary", ex.Message);

            // The exception for element types includes Path.
            Assert.Contains("Path: $.UnsupportedDictionary", ex.Message);

            // Serializing works for unsupported types if the property is null; elements are not verified until serialization occurs.
            var obj = new ClassWithInvalidDictionary();
            string yaml = YamlSerializer.Serialize(obj);
            Assert.Equal("""{"UnsupportedDictionary":null}""", yaml);

            obj.UnsupportedDictionary = new Dictionary<string, int[,]> { ["key"] = new int[,] { } };
            ex = Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(obj));

            // The exception contains the type and Path.
            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
            Assert.Contains("Path: $.UnsupportedDictionary", ex.Message);

            // The original exception contains the type.
            Assert.NotNull(ex.InnerException);
            Assert.Contains(typeof(int[,]).ToString(), ex.InnerException.Message);
        }

        [Fact]
        public static void UnsupportedTypeFromRoot()
        {
            Exception ex = Assert.Throws<NotSupportedException>(() =>
                YamlSerializer.Deserialize<int[,]>("[]"));

            Assert.Contains(typeof(int[,]).ToString(), ex.Message);
            Assert.Contains("Path: $", ex.Message);
        }

        [Theory]
        [InlineData(typeof(ClassWithBadCtor))]
        [InlineData(typeof(StructWithBadCtor))]
        [Trait("category", "jsonoutput")]
        public static void TypeWithBadCtorNoProps(Type type)
        {
            var instance = Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { new SerializationInfo(typeof(Type), new FormatterConverter()), new StreamingContext(default) },
                culture: null)!;

            Assert.Equal("{}", YamlSerializer.Serialize(instance, type));

            // Each constructor parameter must bind to an object property or field.
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("{}", type));
            Assert.Contains(type.FullName, ex.Message);
        }

        [Theory]
        [InlineData(typeof(ClassWithBadCtor_WithProps))]
        [InlineData(typeof(StructWithBadCtor_WithProps))]
        public static void TypeWithBadCtorWithPropsInvalid(Type type)
        {
            var instance = Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { new SerializationInfo(typeof(Type), new FormatterConverter()), new StreamingContext(default) },
                culture: null)!;

            string serializationInfoName = typeof(SerializationInfo).FullName;

            // (De)serialization of SerializationInfo type is not supported.

            NotSupportedException ex = Assert.Throws<NotSupportedException>(() => YamlSerializer.Serialize(instance, type));
            string exAsStr = ex.ToString();
            Assert.Contains(serializationInfoName, exAsStr);
            Assert.Contains("$.Info", exAsStr);

            ex = Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize("""{"Info":{}}""", type));
            exAsStr = ex.ToString();
            Assert.Contains(serializationInfoName, exAsStr);
            Assert.Contains("$.Info", exAsStr);

            // Deserialization of null is okay since no data is read.
            object obj = YamlSerializer.Deserialize("""{"Info":null}""", type);
            Assert.Null(type.GetProperty("Info").GetValue(obj));

            // Deserialization of other non-null tokens is not okay.
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize("""{"Info":1}""", type));
            Assert.Throws<NotSupportedException>(() => YamlSerializer.Deserialize("""{"Info":""}""", type));
        }

        public class ClassWithBadCtor
        {
            public ClassWithBadCtor(SerializationInfo info, StreamingContext ctx) { }
        }

        public struct StructWithBadCtor
        {
            [YamlConstructor]
            public StructWithBadCtor(SerializationInfo info, StreamingContext ctx) { }
        }

        public class ClassWithBadCtor_WithProps
        {
            public SerializationInfo? Info { get; set; }

            public StreamingContext Ctx { get; set; }

            public ClassWithBadCtor_WithProps(SerializationInfo? info, StreamingContext ctx) =>
                (Info, Ctx) = (info, ctx);
        }

        public struct StructWithBadCtor_WithProps
        {
            public SerializationInfo? Info { get; set; }

            public StreamingContext Ctx { get; set; }

            [YamlConstructor]
            public StructWithBadCtor_WithProps(SerializationInfo? info, StreamingContext ctx) =>
                (Info, Ctx) = (info, ctx);
        }

        [Fact]
        public static void CustomConverterThrowingYamlException_Serialization_ShouldNotOverwriteMetadata()
        {
            YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(new { Value = new PocoUsingCustomConverterThrowingYamlException() }));
            Assert.Equal(PocoConverterThrowingCustomYamlException.ExceptionMessage, ex.Message);
            Assert.Equal(PocoConverterThrowingCustomYamlException.ExceptionPath, ex.Path);
        }

        [Fact]
        public static void CustomConverterThrowingYamlException_Deserialization_ShouldNotOverwriteMetadata()
        {
            YamlException ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<PocoUsingCustomConverterThrowingYamlException[]>("[{}]"));
            Assert.Equal(PocoConverterThrowingCustomYamlException.ExceptionMessage, ex.Message);
            Assert.Equal(PocoConverterThrowingCustomYamlException.ExceptionPath, ex.Path);
        }

        [YamlConverter(typeof(PocoConverterThrowingCustomYamlException))]
        public class PocoUsingCustomConverterThrowingYamlException
        {
        }

        public class PocoConverterThrowingCustomYamlException : YamlConverter<PocoUsingCustomConverterThrowingYamlException>
        {
            public const string ExceptionMessage = "Custom YamlException mesage";
            public const string ExceptionPath = "$.CustomPath";

            public override PocoUsingCustomConverterThrowingYamlException? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
                => throw new YamlException(ExceptionMessage, ExceptionPath, 0, 0);

            public override void Write(Utf8YamlWriter writer, PocoUsingCustomConverterThrowingYamlException value, YamlSerializerOptions options)
                => throw new YamlException(ExceptionMessage, ExceptionPath, 0, 0);
        }

        [Fact]
        public static void ThrowingMembers_PropagateOriginalException()
        {
            // Exercises the default member accessor (Reflection.Emit on runtimes that compile dynamic code).
            AssertThrowingMembersPropagateOriginalException();
        }

#if NET
        [ConditionalFact(typeof(RemoteExecutor), nameof(RemoteExecutor.IsSupported))]
        public static void ThrowingMembers_PropagateOriginalException_WithReflectionMemberAccessor()
        {
            // Disabling dynamic code support routes serialization through the reflection-based member
            // accessor (as happens on WASM, iOS and the Mono interpreter) instead of the Reflection.Emit
            // accessor. Exceptions thrown by user getters, setters and constructors must still propagate
            // unwrapped rather than being surfaced as a TargetInvocationException.
            var options = new RemoteInvokeOptions
            {
                RuntimeConfigurationOptions =
                {
                    ["System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"] = false
                }
            };

            RemoteExecutor.Invoke(static () =>
            {
                Assert.False(RuntimeFeature.IsDynamicCodeCompiled);
                AssertThrowingMembersPropagateOriginalException();
            }, options).Dispose();
        }
#endif

        private static void AssertThrowingMembersPropagateOriginalException()
        {
            InvalidOperationException ex;

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new ClassWithThrowingGetter()));
            Assert.Equal(ThrowingMember_ExceptionMessage, ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ClassWithThrowingSetter>("""{"Value":1}"""));
            Assert.Equal(ThrowingMember_ExceptionMessage, ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ClassWithThrowingConstructor>("{}"));
            Assert.Equal(ThrowingMember_ExceptionMessage, ex.Message);
        }

        private const string ThrowingMember_ExceptionMessage = "Exception thrown from user code.";

        public class ClassWithThrowingGetter
        {
            public int Value => throw new InvalidOperationException(ThrowingMember_ExceptionMessage);
        }

        public class ClassWithThrowingSetter
        {
            public int Value { get => 0; set => throw new InvalidOperationException(ThrowingMember_ExceptionMessage); }
        }

        public class ClassWithThrowingConstructor
        {
            [YamlConstructor]
            public ClassWithThrowingConstructor(int value)
                => throw new InvalidOperationException(ThrowingMember_ExceptionMessage);

            public int Value { get; }
        }
    }
}
