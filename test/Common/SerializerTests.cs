// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    /// <summary>
    /// Base class abstracting the serialization System Under Test.
    /// </summary>
    public abstract class SerializerTests
    {
        protected SerializerTests(YamlSerializerWrapper serializerUnderTest)
        {
            Serializer = serializerUnderTest;
            StreamingSerializer = serializerUnderTest as StreamingYamlSerializerWrapper;
        }

        /// <summary>
        /// The serialization System Under Test to be targeted by deriving test suites.
        /// </summary>
        protected YamlSerializerWrapper Serializer { get; }

        /// <summary>
        /// For Systems Under Test that support streaming, exposes the relevant API surface.
        /// </summary>
        protected StreamingYamlSerializerWrapper? StreamingSerializer { get; }

        [Flags]
        protected enum SerializedValueContext
        {
            None = 0,
            RootValue = 1,
            ObjectProperty = 2,
            CollectionElement = 4,
            DictionaryValue = 8,
            YamlNode = 16,
            BoxedValue = 32,
            All = RootValue | ObjectProperty | CollectionElement | DictionaryValue | YamlNode | BoxedValue,
        }

        /// <summary>
        /// Tests serialization of a given value within the context of multiple types:
        /// root values, object properties, collection elements, dictionary values, etc.
        /// </summary>
        protected async Task TestMultiContextSerialization<TValue>(
            TValue value,
            string expectedYaml,
            Type? expectedExceptionType = null,
            SerializedValueContext contexts = SerializedValueContext.All,
            YamlSerializerOptions? options = null)
        {
            Assert.True((contexts & SerializedValueContext.All) != SerializedValueContext.None);

            string actualYaml;

            if (contexts.HasFlag(SerializedValueContext.RootValue))
            {
                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(value, options));
                }
                else
                {
                    actualYaml = await Serializer.SerializeWrapper(value, options);
                    YamlTestHelper.AssertYamlEqual(expectedYaml, actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.RootValue | SerializedValueContext.BoxedValue))
            {
                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper<object>(value, options));
                }
                else
                {
                    actualYaml = await Serializer.SerializeWrapper<object>(value, options);
                    YamlTestHelper.AssertYamlEqual(expectedYaml, actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.ObjectProperty))
            {
                var poco = new GenericPoco<TValue> { Property = value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(poco, options));
                }
                else
                {
                    string propertyName = options?.PropertyNamingPolicy is YamlNamingPolicy policy
                        ? policy.ConvertName(nameof(GenericPoco<TValue>.Property))
                        : nameof(GenericPoco<TValue>.Property);

                    actualYaml = await Serializer.SerializeWrapper(poco, options);
                    YamlTestHelper.AssertYamlEqual($@"{{ ""{propertyName}"" : {expectedYaml} }}", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.ObjectProperty | SerializedValueContext.BoxedValue))
            {
                var poco = new GenericPoco<object> { Property = value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(poco, options));
                }
                else
                {
                    string propertyName = options?.PropertyNamingPolicy is YamlNamingPolicy policy
                        ? policy.ConvertName(nameof(GenericPoco<object>.Property))
                        : nameof(GenericPoco<object>.Property);

                    actualYaml = await Serializer.SerializeWrapper(poco, options);
                    YamlTestHelper.AssertYamlEqual($@"{{ ""{propertyName}"" : {expectedYaml} }}", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.CollectionElement))
            {
                var list = new List<TValue> { value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(list, options));
                }
                else
                {
                    actualYaml = await Serializer.SerializeWrapper(list, options);
                    YamlTestHelper.AssertYamlEqual($"[{expectedYaml}]", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.CollectionElement | SerializedValueContext.BoxedValue))
            {
                var list = new List<object> { value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(list, options));
                }
                else
                {
                    actualYaml = await Serializer.SerializeWrapper(list, options);
                    YamlTestHelper.AssertYamlEqual($"[{expectedYaml}]", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.DictionaryValue))
            {
                const string key = "key";
                var dictionary = new Dictionary<string, TValue> { [key] = value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(dictionary, options));
                }
                else
                {
                    string yamlKey = options?.DictionaryKeyPolicy is YamlNamingPolicy policy
                        ? policy.ConvertName(key)
                        : key;

                    actualYaml = await Serializer.SerializeWrapper(dictionary, options);
                    YamlTestHelper.AssertYamlEqual($@"{{ ""{yamlKey}"" : {expectedYaml} }}", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.DictionaryValue | SerializedValueContext.BoxedValue))
            {
                const string key = "key";
                var dictionary = new Dictionary<string, object> { [key] = value };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(dictionary, options));
                }
                else
                {
                    string yamlKey = options?.DictionaryKeyPolicy is YamlNamingPolicy policy
                        ? policy.ConvertName(key)
                        : key;

                    actualYaml = await Serializer.SerializeWrapper(dictionary, options);
                    YamlTestHelper.AssertYamlEqual($@"{{ ""{yamlKey}"" : {expectedYaml} }}", actualYaml);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.YamlNode))
            {
                const string key = "key";
                YamlTypeInfo<TValue> typeInfo = Serializer.GetTypeInfo<TValue>(options);
                var yamlObject = new YamlObject { [key] = YamlValue.Create(value, typeInfo) };

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.SerializeWrapper(yamlObject, options));
                }
                else
                {
                    actualYaml = await Serializer.SerializeWrapper(yamlObject, options);
                    YamlTestHelper.AssertYamlEqual($@"{{ ""{key}"" : {expectedYaml} }}", actualYaml);
                }
            }
        }

        /// <summary>
        /// Tests serialization of a given list of values within the context of multiple types:
        /// root values, object properties, collection elements, dictionary values, etc.
        /// </summary>
        protected async Task TestMultiContextSerialization<TValue>(
            IEnumerable<(TValue Value, string ExpectedYaml)> inputs,
            SerializedValueContext contexts = SerializedValueContext.All,
            YamlSerializerOptions? options = null)
        {
            inputs = inputs.ToList();
            string expectedYaml = $"[{string.Join(", ", inputs.Select(x => x.ExpectedYaml))}]";
            List<TValue> values = inputs.Select(x => x.Value).ToList();
            await TestMultiContextSerialization(
                values,
                expectedYaml,
                expectedExceptionType: null,
                contexts,
                options);
        }

        /// <summary>
        /// Tests deserialization of a given value within the context of multiple types:
        /// root values, object properties, collection elements, dictionary values, etc.
        /// </summary>
        protected async Task TestMultiContextDeserialization<TValue>(
            string yaml,
            TValue? expectedValue = default,
            Type? expectedExceptionType = null,
            SerializedValueContext contexts = SerializedValueContext.All,
            YamlSerializerOptions? options = null,
            IEqualityComparer<TValue>? equalityComparer = null)
        {
            Assert.True((contexts & SerializedValueContext.All) != SerializedValueContext.None);

            string wrappedYaml;
            equalityComparer ??= expectedValue is IEquatable<TValue>
                ? EqualityComparer<TValue>.Default
                : new YamlEqualityComparer<TValue>(Serializer.GetTypeInfo<TValue>(options));

            if (contexts.HasFlag(SerializedValueContext.RootValue))
            {
                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.DeserializeWrapper<TValue>(yaml, options));
                }
                else
                {
                    TValue value = await Serializer.DeserializeWrapper<TValue>(yaml, options);
                    Assert.Equal(expectedValue, value, equalityComparer);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.ObjectProperty))
            {
                string propertyName =
                    options?.PropertyNamingPolicy is YamlNamingPolicy policy
                    ? policy.ConvertName(nameof(GenericPoco<TValue>.Property))
                    : nameof(GenericPoco<TValue>.Property);

                wrappedYaml = $@"{{ ""{propertyName}"" : {yaml} }}";

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.DeserializeWrapper<GenericPoco<TValue>>(wrappedYaml, options));
                }
                else
                {

                    GenericPoco<TValue> poco = await Serializer.DeserializeWrapper<GenericPoco<TValue>>(wrappedYaml, options);
                    Assert.Equal(expectedValue, poco.Property, equalityComparer);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.CollectionElement))
            {
                wrappedYaml = $@"[{yaml}]";

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.DeserializeWrapper<List<TValue>>(wrappedYaml, options));
                }
                else
                {
                    List<TValue> list = await Serializer.DeserializeWrapper<List<TValue>>(wrappedYaml, options);
                    Assert.Equal(1, list.Count);
                    Assert.Equal(expectedValue, list[0], equalityComparer);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.DictionaryValue))
            {
                const string key = "key";
                wrappedYaml = $@"{{ ""{key}"" : {yaml} }}";

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType, () => Serializer.DeserializeWrapper<Dictionary<string, TValue>>(wrappedYaml, options));
                }
                else
                {
                    Dictionary<string, TValue> dictionary = await Serializer.DeserializeWrapper<Dictionary<string, TValue>>(wrappedYaml, options);
                    Assert.Equal(1, dictionary.Count);
                    Assert.True(dictionary.ContainsKey(key));
                    Assert.Equal(expectedValue, dictionary[key], equalityComparer);
                }
            }

            if (contexts.HasFlag(SerializedValueContext.YamlNode))
            {
                const string key = "key";
                wrappedYaml = $@"{{ ""{key}"" : {yaml} }}";

                if (expectedExceptionType != null)
                {
                    await Assert.ThrowsAsync(expectedExceptionType,
                        async () =>
                        {
                            YamlNode yamlNode = await Serializer.DeserializeWrapper<YamlNode>(wrappedYaml, options);
                            YamlTypeInfo<TValue> typeInfo = Serializer.GetTypeInfo<TValue>(options);
                            YamlSerializer.Deserialize(yamlNode[key], typeInfo);
                        });
                }
                else
                {
                    YamlNode yamlNode = await Serializer.DeserializeWrapper<YamlNode>(wrappedYaml, options);
                    YamlTypeInfo<TValue> typeInfo = Serializer.GetTypeInfo<TValue>(options);
                    TValue value = YamlSerializer.Deserialize(yamlNode[key], typeInfo);
                    Assert.Equal(expectedValue, value, equalityComparer);
                }
            }
        }

        /// <summary>
        /// Tests deserialization of a given list of values within the context of multiple types:
        /// root values, object properties, collection elements, dictionary values, etc.
        /// </summary>
        protected async Task TestMultiContextDeserialization<TValue>(
            IEnumerable<(string Yaml, TValue ExpectedValue)> inputs,
            SerializedValueContext contexts = SerializedValueContext.All,
            YamlSerializerOptions? options = null,
            IEqualityComparer<TValue>? equalityComparer = null)
        {
            inputs = inputs.ToList();
            List<TValue> expectedValues = inputs.Select(x => x.ExpectedValue).ToList();
            string yaml = $"[{string.Join(",", inputs.Select(x => x.Yaml))}]";
            var listEqualityComparer = new ListAssertionEqualityComparer<TValue>(equalityComparer);
            await TestMultiContextDeserialization<List<TValue>>(yaml, expectedValues, expectedExceptionType: null, contexts, options, listEqualityComparer);
        }

        internal class GenericPoco<T>
        {
            public T Property { get; set; }
        }

        // Creates a trim/AOT-safe equality comparer that compares values by serializing them
        // using the resolved (polymorphism-aware) YamlTypeInfo and comparing the resulting JSON.
        // Values of differing runtime types are never considered equal, preserving the behavior of
        // the reflection-based structural comparer this replaced (so that, for example, a nearest-
        // ancestor fallback regression cannot slip through when a derived type happens to serialize
        // identically under its base contract).
        protected IEqualityComparer<T> CreateYamlEqualityComparer<T>(YamlSerializerOptions? options = null)
            => new YamlEqualityComparer<T>(Serializer.GetTypeInfo<T>(options), compareRuntimeType: true);

        private class YamlEqualityComparer<TValue> : IEqualityComparer<TValue>
        {
            private readonly YamlTypeInfo<TValue> _yamlTypeInfo;
            private readonly bool _compareRuntimeType;

            public YamlEqualityComparer(YamlTypeInfo<TValue> yamlTypeInfo, bool compareRuntimeType = false)
            {
                _yamlTypeInfo = yamlTypeInfo;
                _compareRuntimeType = compareRuntimeType;
            }

            public bool Equals(TValue? x, TValue? y)
            {
                // Object.GetType() is trim/AOT-safe and lets us reject differing runtime types before
                // falling back to structural (serialized JSON) comparison.
                if (_compareRuntimeType && x is not null && y is not null && x.GetType() != y.GetType())
                {
                    return false;
                }

                return YamlSerializer.Serialize(x, _yamlTypeInfo) == YamlSerializer.Serialize(y, _yamlTypeInfo);
            }

            public int GetHashCode([DisallowNull] TValue obj) => YamlSerializer.Serialize(obj, _yamlTypeInfo).GetHashCode();
        }

        private class ListAssertionEqualityComparer<TValue> : IEqualityComparer<IList<TValue>>
        {
            private readonly IEqualityComparer<TValue>? _elementComparer;

            public ListAssertionEqualityComparer(IEqualityComparer<TValue>? elementComparer)
            {
                _elementComparer = elementComparer ?? EqualityComparer<TValue>.Default;
            }

            public bool Equals(IList<TValue>? x, IList<TValue>? y)
            {
                Assert.Equal(x, y, _elementComparer);
                return true;
            }

            public int GetHashCode([DisallowNull] IList<TValue> obj) => throw new NotImplementedException();
        }
    }
}
