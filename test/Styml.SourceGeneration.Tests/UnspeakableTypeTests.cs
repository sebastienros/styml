// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class UnspeakableTypeTests : SerializerTests
    {
        public UnspeakableTypeTests()
            : base(new AsyncStreamSerializerWrapper(UnspeakableTypeContext.Default))
        {
        }

        [Theory]
        [MemberData(nameof(GetUnspeakableTypes))]
        [Trait("category", "jsonoutput")]
        public async Task CanSerializeUnspeakableRootTypes<T>(Envelope<T> envelope, string expectedYaml, bool isBaseTypeDeserializable)
        {
            YamlSerializerOptions options = UnspeakableTypeContext.Default.Options;

            // Context returns a YamlTypeInfo for the declared type
            Assert.NotNull(options.GetTypeInfo(typeof(T)));
            // But fails when resolving one for the runtime type
            Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(envelope.Value.GetType()));

            // Serializing using the declared type works as expected
            string yaml = await Serializer.SerializeWrapper(envelope.Value, options);
            Assert.Equal(expectedYaml, yaml);

            // But it can also be serialized as an object
            yaml = await Serializer.SerializeWrapper<object>(envelope.Value, options);
            Assert.Equal(expectedYaml, yaml);

            // But fails if you pass in its runtime type 
            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(envelope.Value, envelope.Value.GetType(), options));

            if (isBaseTypeDeserializable)
            {
                // And it can be deserialized using the declared type
                await Serializer.DeserializeWrapper<T>(yaml, options);

                // But will still fail if you attempt to deserialize using the runtime type
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper(yaml, envelope.Value.GetType(), options));
            }
        }

        [Theory]
        [MemberData(nameof(GetUnspeakableTypes))]
        [Trait("category", "jsonoutput")]
        public async Task CanSerializeUnspeakableTypesAsBoxedProperties<T>(Envelope<T> envelope, string expectedYaml, bool isBaseTypeDeserializable)
        {
            _ = isBaseTypeDeserializable;
            var boxedEnvelope = new Envelope<object>(envelope.Value);
            string expectedEnvelopeYaml = $$"""{"Value":{{expectedYaml}}}""";

            string yaml = await Serializer.SerializeWrapper(boxedEnvelope, UnspeakableTypeContext.Default.Options);
            Assert.Equal(expectedEnvelopeYaml, yaml);
        }

        [Theory]
        [MemberData(nameof(GetUnspeakableTypes))]
        [Trait("category", "jsonoutput")]
        public async Task CanSerializeUnspeakableTypesAsBoxedCollectionElements<T>(Envelope<T> envelope, string expectedYaml, bool isBaseTypeDeserializable)
        {
            _ = isBaseTypeDeserializable;
            var boxedEnvelope = new List<object> { envelope.Value };
            string expectedCollectionYaml = $"[{expectedYaml}]";

            string yaml = await Serializer.SerializeWrapper(boxedEnvelope, UnspeakableTypeContext.Default.Options);
            Assert.Equal(expectedCollectionYaml, yaml);
        }

        [Theory]
        [MemberData(nameof(GetUnspeakableTypes))]
        [Trait("category", "jsonoutput")]
        public async Task CanSerializeUnspeakableTypesAsBoxedDictionaryValues<T>(Envelope<T> envelope, string expectedYaml, bool isBaseTypeDeserializable)
        {
            _ = isBaseTypeDeserializable;
            var boxedEnvelope = new Dictionary<string, object> { ["key"] = envelope.Value };
            string expectedCollectionYaml = $$"""{"key":{{expectedYaml}}}""";

            string yaml = await Serializer.SerializeWrapper(boxedEnvelope, UnspeakableTypeContext.Default.Options);
            Assert.Equal(expectedCollectionYaml, yaml);
        }

        [Fact]
        public async Task TypeWithDiamondAmbiguityThrowsNotSupportedException()
        {
            object value = new TypeWithDiamondAmbiguity();

            NotSupportedException exn = await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(value, UnspeakableTypeContext.Default.Options));
            Assert.Contains("TypeWithDiamondAmbiguity", exn.Message);
            Assert.Contains("BasePoco", exn.Message);
            Assert.Contains("IEnumerable", exn.Message);
        }

        public static IEnumerable<object[]> GetUnspeakableTypes()
        {
            yield return Wrap<IEnumerable<int>>(new int[] { 1, 2, 3 }, "[1,2,3]");
            yield return Wrap(Enumerable.Range(1, 3), "[1,2,3]");
            yield return Wrap(Enumerable.Range(1, 3).Select(x => x - 1), "[0,1,2]");
            yield return Wrap(IteratorMethod(), "[1,2,3]");
            yield return Wrap(AsyncIteratorMethod(), "[1,2,3]");

            yield return Wrap<BasePoco>(new PrivateDerivedPoco { BaseValue = 1, DerivedValue = 2 }, """{"BaseValue":1}""");
            yield return Wrap<IMyInterface>(new MyImplementation { Value = 1, AnotherValue = 2 }, """{"Value":1}""", isBaseTypeDeserializable: false);

            static object[] Wrap<T>(T value, string expectedYaml, bool isBaseTypeDeserializable = true)
                => new object[] { new Envelope<T>(value), expectedYaml, isBaseTypeDeserializable };

            static IEnumerable<int> IteratorMethod()
            {
                yield return 1; yield return 2; yield return 3;
            }

            static async IAsyncEnumerable<int> AsyncIteratorMethod()
            {
                yield return 1; yield return 2; yield return 3;
                await Task.CompletedTask;
            }
        }

        public class BasePoco
        {
            public int BaseValue { get; set;  }
        }

        private class PrivateDerivedPoco : BasePoco
        {
            public int DerivedValue { get; set; }
        }

        public interface IMyInterface
        {
            public int Value { get; set;  }
        }

        private class MyImplementation : IMyInterface
        {
            public int Value { get; set; }
            public int AnotherValue { get; set; }
        }

        private class TypeWithDiamondAmbiguity : BasePoco, IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { yield return 42; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public record Envelope<T>(T Value);

        [YamlSerializable(typeof(BasePoco))]
        [YamlSerializable(typeof(IMyInterface))]
        [YamlSerializable(typeof(IEnumerable))]
        [YamlSerializable(typeof(IEnumerable<int>))]
        [YamlSerializable(typeof(IAsyncEnumerable<int>))]
        [YamlSerializable(typeof(Envelope<object>))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        public partial class UnspeakableTypeContext : YamlSerializerContext
        {
        }
    }
}
