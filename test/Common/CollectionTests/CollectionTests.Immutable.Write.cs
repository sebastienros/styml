// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class CollectionTests
    {
        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableArrayOfImmutableArray()
        {
            ImmutableArray<ImmutableArray<int>> input = ImmutableArray.CreateRange(new List<ImmutableArray<int>>{
                ImmutableArray.CreateRange(new List<int>() { 1, 2 }),
                ImmutableArray.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableArrayOfArray()
        {
            ImmutableArray<int[]> input = ImmutableArray.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableArray()
        {
            ImmutableArray<int>[] input = new ImmutableArray<int>[2];
            input[0] = ImmutableArray.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableArray.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteSimpleImmutableArray()
        {
            ImmutableArray<int> input = ImmutableArray.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableListTOfIImmutableListT()
        {
            IImmutableList<IImmutableList<int>> input = ImmutableList.CreateRange(new List<IImmutableList<int>>{
                ImmutableList.CreateRange(new List<int>() { 1, 2 }),
                ImmutableList.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableListTOfArray()
        {
            IImmutableList<int[]> input = ImmutableList.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteSimpleClassWithImmutableArray()
        {
            SimpleTestClassWithImmutableArray obj = new SimpleTestClassWithImmutableArray();
            obj.Initialize();

            Assert.Equal(SimpleTestClassWithImmutableArray.s_yaml, await Serializer.SerializeWrapper(obj));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteSimpleClassWithObjectImmutableArray()
        {
            SimpleTestClassWithObjectImmutableArray obj = new SimpleTestClassWithObjectImmutableArray();
            obj.Initialize();

            Assert.Equal(SimpleTestClassWithObjectImmutableArray.s_yaml, await Serializer.SerializeWrapper(obj));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfIImmutableListT()
        {
            IImmutableList<int>[] input = new IImmutableList<int>[2];
            input[0] = ImmutableList.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableList.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveIImmutableListT()
        {
            IImmutableList<int> input = ImmutableList.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);

            StringIImmutableListWrapper input2 = new StringIImmutableListWrapper(new List<string> { "1", "2" });

            yaml = await Serializer.SerializeWrapper(input2);
            Assert.Equal("""["1","2"]""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableStackTOfIImmutableStackT()
        {
            IImmutableStack<IImmutableStack<int>> input = ImmutableStack.CreateRange(new List<IImmutableStack<int>>{
                ImmutableStack.CreateRange(new List<int>() { 1, 2 }),
                ImmutableStack.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[4,3],[2,1]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableStackTOfArray()
        {
            IImmutableStack<int[]> input = ImmutableStack.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[3,4],[1,2]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfIImmutableStackT()
        {
            IImmutableStack<int>[] input = new IImmutableStack<int>[2];
            input[0] = ImmutableStack.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableStack.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[2,1],[4,3]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveIImmutableStackT()
        {
            IImmutableStack<int> input = ImmutableStack.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[2,1]", yaml);

            StringIImmutableStackWrapper input2 = new StringIImmutableStackWrapper(new List<string> { "1", "2" });

            yaml = await Serializer.SerializeWrapper(input2);
            Assert.Equal("""["2","1"]""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableQueueTOfIImmutableQueueT()
        {
            IImmutableQueue<IImmutableQueue<int>> input = ImmutableQueue.CreateRange(new List<IImmutableQueue<int>>{
                ImmutableQueue.CreateRange(new List<int>() { 1, 2 }),
                ImmutableQueue.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableQueueTOfArray()
        {
            IImmutableQueue<int[]> input = ImmutableQueue.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfIImmutableQueueT()
        {
            IImmutableQueue<int>[] input = new IImmutableQueue<int>[2];
            input[0] = ImmutableQueue.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableQueue.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveIImmutableQueueT()
        {
            IImmutableQueue<int> input = ImmutableQueue.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);

            StringIImmutableQueueWrapper input2 = new StringIImmutableQueueWrapper(new List<string> { "1", "2" });

            yaml = await Serializer.SerializeWrapper(input2);
            Assert.Equal("""["1","2"]""", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableSetTOfIImmutableSetT()
        {
            IImmutableSet<IImmutableSet<int>> input = ImmutableHashSet.CreateRange(new List<IImmutableSet<int>>{
                ImmutableHashSet.CreateRange(new List<int>() { 1, 2 }),
                ImmutableHashSet.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("[1,2]", yaml);
            Assert.Contains("[3,4]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteIImmutableSetTOfArray()
        {
            IImmutableSet<int[]> input = ImmutableHashSet.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("[1,2]", yaml);
            Assert.Contains("[3,4]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfIImmutableSetT()
        {
            IImmutableSet<int>[] input = new IImmutableSet<int>[2];
            input[0] = ImmutableHashSet.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableHashSet.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveIImmutableSetT()
        {
            IImmutableSet<int> input = ImmutableHashSet.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);

            StringIImmutableSetWrapper input2 = new StringIImmutableSetWrapper(new List<string> { "1", "2" });

            yaml = await Serializer.SerializeWrapper(input2);
            Assert.True(yaml == """["1","2"]""" || yaml == """["2","1"]""");
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableHashSetTOfImmutableHashSetT()
        {
            ImmutableHashSet<ImmutableHashSet<int>> input = ImmutableHashSet.CreateRange(new List<ImmutableHashSet<int>>{
                ImmutableHashSet.CreateRange(new List<int>() { 1, 2 }),
                ImmutableHashSet.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("[1,2]", yaml);
            Assert.Contains("[3,4]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableHashSetTOfArray()
        {
            ImmutableHashSet<int[]> input = ImmutableHashSet.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("[1,2]", yaml);
            Assert.Contains("[3,4]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableHashSetT()
        {
            ImmutableHashSet<int>[] input = new ImmutableHashSet<int>[2];
            input[0] = ImmutableHashSet.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableHashSet.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveImmutableHashSetT()
        {
            ImmutableHashSet<int> input = ImmutableHashSet.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableListTOfImmutableListT()
        {
            ImmutableList<ImmutableList<int>> input = ImmutableList.CreateRange(new List<ImmutableList<int>>{
                ImmutableList.CreateRange(new List<int>() { 1, 2 }),
                ImmutableList.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableListTOfArray()
        {
            ImmutableList<int[]> input = ImmutableList.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableListT()
        {
            ImmutableList<int>[] input = new ImmutableList<int>[2];
            input[0] = ImmutableList.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableList.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveImmutableListT()
        {
            ImmutableList<int> input = ImmutableList.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableStackTOfImmutableStackT()
        {
            ImmutableStack<ImmutableStack<int>> input = ImmutableStack.CreateRange(new List<ImmutableStack<int>>{
                ImmutableStack.CreateRange(new List<int>() { 1, 2 }),
                ImmutableStack.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[4,3],[2,1]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableStackTOfArray()
        {
            ImmutableStack<int[]> input = ImmutableStack.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[3,4],[1,2]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableStackT()
        {
            ImmutableStack<int>[] input = new ImmutableStack<int>[2];
            input[0] = ImmutableStack.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableStack.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[2,1],[4,3]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveImmutableStackT()
        {
            ImmutableStack<int> input = ImmutableStack.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[2,1]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableQueueTOfImmutableQueueT()
        {
            ImmutableQueue<ImmutableQueue<int>> input = ImmutableQueue.CreateRange(new List<ImmutableQueue<int>>{
                ImmutableQueue.CreateRange(new List<int>() { 1, 2 }),
                ImmutableQueue.CreateRange(new List<int>() { 3, 4 })
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableQueueTOfArray()
        {
            ImmutableQueue<int[]> input = ImmutableQueue.CreateRange(new List<int[]>
            {
                new int[] { 1, 2 },
                new int[] { 3, 4 }
            });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableQueueT()
        {
            ImmutableQueue<int>[] input = new ImmutableQueue<int>[2];
            input[0] = ImmutableQueue.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableQueue.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveImmutableQueueT()
        {
            ImmutableQueue<int> input = ImmutableQueue.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteArrayOfImmutableSortedSetT()
        {
            ImmutableSortedSet<int>[] input = new ImmutableSortedSet<int>[2];
            input[0] = ImmutableSortedSet.CreateRange(new List<int>() { 1, 2 });
            input[1] = ImmutableSortedSet.CreateRange(new List<int>() { 3, 4 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[[1,2],[3,4]]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WritePrimitiveImmutableSortedSetT()
        {
            ImmutableSortedSet<int> input = ImmutableSortedSet.CreateRange(new List<int> { 1, 2 });

            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableCollectionWrappers()
        {
            SimpleTestClassWithIImmutableDictionaryWrapper obj1 = new SimpleTestClassWithIImmutableDictionaryWrapper();
            SimpleTestClassWithImmutableListWrapper obj2 = new SimpleTestClassWithImmutableListWrapper();
            SimpleTestClassWithImmutableStackWrapper obj3 = new SimpleTestClassWithImmutableStackWrapper();
            SimpleTestClassWithImmutableQueueWrapper obj4 = new SimpleTestClassWithImmutableQueueWrapper();
            SimpleTestClassWithImmutableSetWrapper obj5 = new SimpleTestClassWithImmutableSetWrapper();

            obj1.Initialize();
            obj2.Initialize();
            obj3.Initialize();
            obj4.Initialize();
            obj5.Initialize();

            Assert.Equal(SimpleTestClassWithIImmutableDictionaryWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper(obj1));
            Assert.Equal(SimpleTestClassWithIImmutableDictionaryWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper<object>(obj1));

            Assert.Equal(SimpleTestClassWithImmutableListWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper(obj2));
            Assert.Equal(SimpleTestClassWithImmutableListWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper<object>(obj2));

            Assert.Equal(SimpleTestClassWithImmutableStackWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper(obj3));
            Assert.Equal(SimpleTestClassWithImmutableStackWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper<object>(obj3));

            Assert.Equal(SimpleTestClassWithImmutableQueueWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper(obj4));
            Assert.Equal(SimpleTestClassWithImmutableQueueWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper<object>(obj4));

            Assert.Equal(SimpleTestClassWithImmutableSetWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper(obj5));
            Assert.Equal(SimpleTestClassWithImmutableSetWrapper.s_yaml.StripWhitespace(), await Serializer.SerializeWrapper<object>(obj5));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableStack()
        {
            ImmutableStack<int> input = ImmutableStack.Create<int>(1, 2, 3);
            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[3,2,1]", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableQueue()
        {
            ImmutableQueue<int> input = ImmutableQueue.Create<int>(1, 2, 3);
            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Equal("[1,2,3]", yaml);
        }

        [Fact]
        public async Task WriteImmutableHashSet()
        {
            ImmutableHashSet<int> input = ImmutableHashSet.Create<int>(1, 2, 3);
            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("1", yaml);
            Assert.Contains("2", yaml);
            Assert.Contains("3", yaml);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public async Task WriteImmutableDictionaryStringKey()
        {
            ImmutableDictionary<string, int> input = ImmutableDictionary.Create<string, int>()
                .Add("a", 1)
                .Add("b", 2);
            string yaml = await Serializer.SerializeWrapper(input);
            Assert.Contains("\"a\":1", yaml);
            Assert.Contains("\"b\":2", yaml);
        }
    }
}
