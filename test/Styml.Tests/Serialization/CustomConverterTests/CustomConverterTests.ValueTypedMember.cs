// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        private class ValueTypeToInterfaceConverter : YamlConverter<IMemberInterface>
        {
            public int ReadCallCount { get; private set; }
            public int WriteCallCount { get; private set; }

            public override bool HandleNull => true;

            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(IMemberInterface).IsAssignableFrom(typeToConvert);
            }

            public override IMemberInterface Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                ReadCallCount++;

                string value = reader.GetString();

                if (value == null)
                {
                    return null;
                }

                if (value.IndexOf("ValueTyped", StringComparison.Ordinal) >= 0)
                {
                    return new ValueTypedMember(value);
                }
                if (value.IndexOf("RefTyped", StringComparison.Ordinal) >= 0)
                {
                    return new RefTypedMember(value);
                }
                if (value.IndexOf("OtherVT", StringComparison.Ordinal) >= 0)
                {
                    return new OtherVTMember(value);
                }
                if (value.IndexOf("OtherRT", StringComparison.Ordinal) >= 0)
                {
                    return new OtherRTMember(value);
                }
                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, IMemberInterface value, YamlSerializerOptions options)
            {
                WriteCallCount++;

                YamlSerializer.Serialize<string>(writer, value == null ? null : value.Value, options);
            }
        }

        private class ValueTypeToObjectConverter : YamlConverter<object>
        {
            public int ReadCallCount { get; private set; }
            public int WriteCallCount { get; private set; }

            public override bool HandleNull => true;

            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(IMemberInterface).IsAssignableFrom(typeToConvert);
            }

            public override object Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                ReadCallCount++;

                string value = reader.GetString();

                if (value == null)
                {
                    return null;
                }

                if (value.IndexOf("ValueTyped", StringComparison.Ordinal) >= 0)
                {
                    return new ValueTypedMember(value);
                }
                if (value.IndexOf("RefTyped", StringComparison.Ordinal) >= 0)
                {
                    return new RefTypedMember(value);
                }
                if (value.IndexOf("OtherVT", StringComparison.Ordinal) >= 0)
                {
                    return new OtherVTMember(value);
                }
                if (value.IndexOf("OtherRT", StringComparison.Ordinal) >= 0)
                {
                    return new OtherRTMember(value);
                }
                throw new YamlException();
            }

            public override void Write(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
            {
                WriteCallCount++;

                YamlSerializer.Serialize<string>(writer, value == null ? null : ((IMemberInterface)value).Value, options);
            }
        }

        [Fact]
        public static void AssignmentToValueTypedMemberInterface()
        {
            var converter = new ValueTypeToInterfaceConverter();
            var options = new YamlSerializerOptions { IncludeFields = true };
            options.Converters.Add(converter);

            Exception ex;
            // Invalid cast OtherVTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":"OtherVTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":"OtherVTField"}""", options));
            // Invalid cast OtherRTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":"OtherRTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":"OtherRTField"}""", options));
            // Invalid null
            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":null}""", options));
            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":null}""", options));
        }

        [Fact]
        public static void AssignmentToValueTypedMemberObject()
        {
            var converter = new ValueTypeToObjectConverter();
            var options = new YamlSerializerOptions { IncludeFields = true };
            options.Converters.Add(converter);

            Exception ex;
            // Invalid cast OtherVTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":"OtherVTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":"OtherVTField"}""", options));
            // Invalid cast OtherRTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":"OtherRTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":"OtherRTField"}""", options));
            // Invalid null
            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedProperty":null}""", options));
            ex = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<TestClassWithValueTypedMember>("""{"MyValueTypedField":null}""", options));
        }

        [Fact]
        public static void AssignmentToNullableValueTypedMemberInterface()
        {
            var converter = new ValueTypeToInterfaceConverter();
            var options = new YamlSerializerOptions { IncludeFields = true };
            options.Converters.Add(converter);

            TestClassWithNullableValueTypedMember obj;
            Exception ex;
            // Invalid cast OtherVTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":"OtherVTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedField":"OtherVTField"}""", options));
            // Invalid cast OtherRTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":"OtherRTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedField":"OtherRTField"}""", options));
            // Valid null
            obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":null,"MyValueTypedField":null}""", options);
            Assert.Null(obj.MyValueTypedProperty);
            Assert.Null(obj.MyValueTypedField);
        }

        [Fact]
        public static void AssignmentToNullableValueTypedMemberObject()
        {
            var converter = new ValueTypeToObjectConverter();
            var options = new YamlSerializerOptions { IncludeFields = true };
            options.Converters.Add(converter);

            TestClassWithNullableValueTypedMember obj;
            Exception ex;
            // Invalid cast OtherVTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":"OtherVTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedField":"OtherVTField"}""", options));
            // Invalid cast OtherRTMember
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":"OtherRTProperty"}""", options));
            ex = Assert.Throws<InvalidCastException>(() => YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedField":"OtherRTField"}""", options));
            // Valid null
            obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>("""{"MyValueTypedProperty":null,"MyValueTypedField":null}""", options);
            Assert.Null(obj.MyValueTypedProperty);
            Assert.Null(obj.MyValueTypedField);
        }

        [Fact]
        public static void ValueTypedMemberToInterfaceConverter()
        {
            const string expected = """{"MyValueTypedProperty":"ValueTypedProperty","MyRefTypedProperty":"RefTypedProperty","MyValueTypedField":"ValueTypedField","MyRefTypedField":"RefTypedField"}""";

            var converter = new ValueTypeToInterfaceConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithValueTypedMember();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithValueTypedMember>(yaml, options);
                obj.Verify();

                Assert.Equal(4, converter.ReadCallCount);
            }
        }

        [Fact]
        public static void ValueTypedMemberToObjectConverter()
        {
            const string expected = """{"MyValueTypedProperty":"ValueTypedProperty","MyRefTypedProperty":"RefTypedProperty","MyValueTypedField":"ValueTypedField","MyRefTypedField":"RefTypedField"}""";

            var converter = new ValueTypeToObjectConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithValueTypedMember();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithValueTypedMember>(yaml, options);
                obj.Verify();

                Assert.Equal(4, converter.ReadCallCount);
            }
        }

        [Fact]
        public static void NullableValueTypedMemberToInterfaceConverter()
        {
            const string expected = """{"MyValueTypedProperty":"ValueTypedProperty","MyRefTypedProperty":"RefTypedProperty","MyValueTypedField":"ValueTypedField","MyRefTypedField":"RefTypedField"}""";

            var converter = new ValueTypeToInterfaceConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithNullableValueTypedMember();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>(yaml, options);
                obj.Verify();

                Assert.Equal(4, converter.ReadCallCount);
            }
        }

        [Fact]
        public static void NullableValueTypedMemberToObjectConverter()
        {
            const string expected = """{"MyValueTypedProperty":"ValueTypedProperty","MyRefTypedProperty":"RefTypedProperty","MyValueTypedField":"ValueTypedField","MyRefTypedField":"RefTypedField"}""";

            var converter = new ValueTypeToObjectConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithNullableValueTypedMember();
                obj.Initialize();
                obj.Verify();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>(yaml, options);
                obj.Verify();

                Assert.Equal(4, converter.ReadCallCount);
            }
        }

        [Fact]
        public static void NullableValueTypedMemberWithNullsToInterfaceConverter()
        {
            const string expected = """{"MyValueTypedProperty":null,"MyRefTypedProperty":null,"MyValueTypedField":null,"MyRefTypedField":null}""";

            var converter = new ValueTypeToInterfaceConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithNullableValueTypedMember();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>(yaml, options);

                Assert.Equal(4, converter.ReadCallCount);
                Assert.Null(obj.MyValueTypedProperty);
                Assert.Null(obj.MyValueTypedField);
                Assert.Null(obj.MyRefTypedProperty);
                Assert.Null(obj.MyRefTypedField);
            }
        }

        [Fact]
        public static void NullableValueTypedMemberWithNullsToObjectConverter()
        {
            const string expected = """{"MyValueTypedProperty":null,"MyRefTypedProperty":null,"MyValueTypedField":null,"MyRefTypedField":null}""";

            var converter = new ValueTypeToObjectConverter();
            var options = new YamlSerializerOptions()
            {
                IncludeFields = true,
            };
            options.Converters.Add(converter);

            string yaml;

            {
                var obj = new TestClassWithNullableValueTypedMember();
                yaml = YamlSerializer.Serialize(obj, options);

                Assert.Equal(4, converter.WriteCallCount);
                YamlTestHelper.AssertYamlEqual(expected, yaml);
            }

            {
                var obj = YamlSerializer.Deserialize<TestClassWithNullableValueTypedMember>(yaml, options);

                Assert.Equal(4, converter.ReadCallCount);
                Assert.Null(obj.MyValueTypedProperty);
                Assert.Null(obj.MyValueTypedField);
                Assert.Null(obj.MyRefTypedProperty);
                Assert.Null(obj.MyRefTypedField);
            }
        }
    }
}
