// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class CollectionTests
    {
        [Fact]
        public async Task Read_SpecializedCollection()
        {
            BitVector32 bv32 = await Serializer.DeserializeWrapper<BitVector32>("""{"Data":4}""");
            // Data property is skipped because it doesn't have a setter.
            Assert.Equal(0, bv32.Data);

            HybridDictionary hd = await Serializer.DeserializeWrapper<HybridDictionary>("""{"key":"value"}""");
            Assert.Equal(1, hd.Count);
            Assert.Equal("value", ((YamlElement)hd["key"]).GetString());

            IOrderedDictionary iod = await Serializer.DeserializeWrapper<OrderedDictionary>("""{"key":"value"}""");
            Assert.Equal(1, iod.Count);
            Assert.Equal("value", ((YamlElement)iod["key"]).GetString());

            ListDictionary ld = await Serializer.DeserializeWrapper<ListDictionary>("""{"key":"value"}""");
            Assert.Equal(1, ld.Count);
            Assert.Equal("value", ((YamlElement)ld["key"]).GetString());
        }

    }
}
