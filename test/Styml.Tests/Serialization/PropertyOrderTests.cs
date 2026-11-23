// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class PropertyOrderTests
    {
        private class MyPoco_BeforeAndAfter
        {
            public int B { get; set; }

            [YamlPropertyOrder(1)]
            public int A { get; set; }

            [YamlPropertyOrder(-1)]
            [YamlInclude]
            public int C = 0;
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void BeforeAndAfterDefaultOrder()
        {
            string yaml = YamlSerializer.Serialize<MyPoco_BeforeAndAfter>(new MyPoco_BeforeAndAfter());
            Assert.Equal("{\"C\":0,\"B\":0,\"A\":0}", yaml);
        }

        private class MyPoco_After
        {
            [YamlPropertyOrder(2)]
            [YamlInclude]
            public int C = 0;

            public int B { get; set; }
            public int D { get; set; }

            [YamlPropertyOrder(1)]
            public int A { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void AfterDefaultOrder()
        {
            string yaml = YamlSerializer.Serialize<MyPoco_After>(new MyPoco_After());
            Assert.EndsWith("\"A\":0,\"C\":0}", yaml);
            // Order of B and D are not defined except they come before A and C
        }

        private class MyPoco_Before
        {
            [YamlPropertyOrder(-1)]
            [YamlInclude]
            public int C = 0;

            public int B { get; set; }
            public int D { get; set; }

            [YamlPropertyOrder(-2)]
            public int A { get; set; }
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public static void BeforeDefaultOrder()
        {
            string yaml = YamlSerializer.Serialize<MyPoco_Before>(new MyPoco_Before());
            Assert.StartsWith("{\"A\":0,\"C\":0", yaml);
            // Order of B and D are not defined except they come after A and C
        }
    }
}
