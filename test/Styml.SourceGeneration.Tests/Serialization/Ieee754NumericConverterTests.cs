// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Numerics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class Ieee754NumericConverterTests_Metadata : Ieee754NumericConverterTests
    {
        public Ieee754NumericConverterTests_Metadata()
            : base(new StringSerializerWrapper(Ieee754NumericConverterTestsContext_Metadata.Default))
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(object))]
        [YamlSerializable(typeof(YamlNode))]
        [YamlSerializable(typeof(YamlObject))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(GenericPoco<object>))]
        [YamlSerializable(typeof(BFloat16))]
        [YamlSerializable(typeof(List<BFloat16>))]
        [YamlSerializable(typeof(Dictionary<string, BFloat16>))]
        [YamlSerializable(typeof(Dictionary<BFloat16, int>))]
        [YamlSerializable(typeof(GenericPoco<BFloat16>))]
        [YamlSerializable(typeof(Decimal32))]
        [YamlSerializable(typeof(List<Decimal32>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal32>))]
        [YamlSerializable(typeof(Dictionary<Decimal32, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal32>))]
        [YamlSerializable(typeof(Decimal64))]
        [YamlSerializable(typeof(List<Decimal64>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal64>))]
        [YamlSerializable(typeof(Dictionary<Decimal64, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal64>))]
        [YamlSerializable(typeof(Decimal128))]
        [YamlSerializable(typeof(List<Decimal128>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal128>))]
        [YamlSerializable(typeof(Dictionary<Decimal128, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal128>))]
        [YamlSerializable(typeof(Ieee754Poco))]
        internal sealed partial class Ieee754NumericConverterTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed partial class Ieee754NumericConverterTests_Default : Ieee754NumericConverterTests
    {
        public Ieee754NumericConverterTests_Default()
            : base(new StringSerializerWrapper(Ieee754NumericConverterTestsContext_Default.Default))
        {
        }

        [YamlSerializable(typeof(object))]
        [YamlSerializable(typeof(YamlNode))]
        [YamlSerializable(typeof(YamlObject))]
        [YamlSerializable(typeof(List<object>))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(GenericPoco<object>))]
        [YamlSerializable(typeof(BFloat16))]
        [YamlSerializable(typeof(List<BFloat16>))]
        [YamlSerializable(typeof(Dictionary<string, BFloat16>))]
        [YamlSerializable(typeof(Dictionary<BFloat16, int>))]
        [YamlSerializable(typeof(GenericPoco<BFloat16>))]
        [YamlSerializable(typeof(Decimal32))]
        [YamlSerializable(typeof(List<Decimal32>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal32>))]
        [YamlSerializable(typeof(Dictionary<Decimal32, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal32>))]
        [YamlSerializable(typeof(Decimal64))]
        [YamlSerializable(typeof(List<Decimal64>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal64>))]
        [YamlSerializable(typeof(Dictionary<Decimal64, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal64>))]
        [YamlSerializable(typeof(Decimal128))]
        [YamlSerializable(typeof(List<Decimal128>))]
        [YamlSerializable(typeof(Dictionary<string, Decimal128>))]
        [YamlSerializable(typeof(Dictionary<Decimal128, int>))]
        [YamlSerializable(typeof(GenericPoco<Decimal128>))]
        [YamlSerializable(typeof(Ieee754Poco))]
        internal sealed partial class Ieee754NumericConverterTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
