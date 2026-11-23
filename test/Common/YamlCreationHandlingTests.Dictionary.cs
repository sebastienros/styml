// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests;

public abstract partial class YamlCreationHandlingTests : SerializerTests
{
    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":null,"Property":{"a":1},"Property":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<Dictionary<string, int>>>(yaml, options);
        CheckGenericDictionaryContent(obj.Property, 2);
    }

    internal class ClassWithReadOnlyPropertyDictionaryOfStringToInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyDictionaryOfStringToIntWithAttributeOnType
    {
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Dictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":null,"Property":{"a":1},"Property":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<IDictionary<string, int>>>(yaml, options);
        CheckGenericDictionaryContent(obj.Property, 2);
    }

    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithoutPopulateAttribute() {}

        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithAttributeOnType() {}

        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionaryOfStringToInt_BackedBy_DictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_DictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new Dictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithAttributeOnType
    {
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        ((StructDictionary<string, int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIDictionaryOfStringToInt_BackedBy_StructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IDictionary<string, int> Property { get; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElement>(yaml, options);
        CheckDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElement));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithAttributeOnType>(yaml, options);
        CheckDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElement>(yaml, options);
        CheckDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionary_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":null,"Property":{"a":1},"Property":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<IDictionary>>(yaml, options);
        CheckDictionaryContent(obj.Property, 2);
    }

    internal struct ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElement
    {
        public ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElement() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IDictionary Property { get; } = ParseYamlObject("""{"a":1,"b":2,"c":3}""").ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static IEnumerable<KeyValuePair<string, YamlElement>> ParseYamlObject(string yaml)
    {
        YamlElement doc = YamlElement.Parse(yaml);
        foreach (var entry in doc.EnumerateObject())
        {
            yield return new KeyValuePair<string, YamlElement>(entry.Name, entry.Value);
        }
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionary_BackedBy_DictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithoutPopulateAttribute() {}

        public IDictionary Property { get; } = ParseYamlObject("""{"a":1,"b":2,"c":3}""").ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIDictionary_BackedBy_DictionaryOfStringToYamlElementWithAttributeOnType() {}

        public IDictionary Property { get; } = ParseYamlObject("""{"a":1,"b":2,"c":3}""").ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElement>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElement));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithAttributeOnType>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElement>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElement
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IDictionary Property { get; } = new StructDictionary<string, YamlElement>(ParseYamlObject("""{"a":1,"b":2,"c":3}"""));
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IDictionary_BackedBy_StructDictionaryOfStringToYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute>(yaml, options);
        CheckDictionaryContent(obj.Property);
        ((StructDictionary<string, YamlElement>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithoutPopulateAttribute
    {
        public IDictionary Property { get; } = new StructDictionary<string, YamlElement>(ParseYamlObject("""{"a":1,"b":2,"c":3}"""));
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIDictionary_BackedBy_StructDictionaryOfStringToYamlElementWithAttributeOnType
    {
        public IDictionary Property { get; } = new StructDictionary<string, YamlElement>(ParseYamlObject("""{"a":1,"b":2,"c":3}"""));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructDictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructDictionaryOfStringToInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class StructWithWritablePropertyStructDictionaryOfStringToIntWithAttributeOnType
    {
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandling
    {
        public StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        public StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        public StructWithWritablePropertyStructDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructDictionary<string, int> Property { get; set; } = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritableFieldNullableStructDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulatedFieldCanDeserializeNull_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":null}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToInt>(yaml, options);
        Assert.Null(obj.Field);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":{"d":4},"Field":{"e":5},"Field":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructDictionaryOfStringToInt_FieldOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":{"d":4},"Field":null,"Field":{"a":1},"Field":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value, 2);
        obj.Field.Value.Validate();
    }

    internal struct StructWithWritableFieldNullableStructDictionaryOfStringToInt
    {
        public StructWithWritableFieldNullableStructDictionaryOfStringToInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructDictionary<string, int>? Field = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute)), includeFields: true);
        string yaml = """{"Field":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Field":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_NullableStructDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Field":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Field.Value);
        obj.Field.Value.Validate();
    }

    internal struct StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public StructWithWritableFieldNullableStructDictionaryOfStringToIntWithoutPopulateAttribute() {}

        public StructDictionary<string, int>? Field = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritableFieldNullableStructDictionaryOfStringToIntWithAttributeOnType
    {
        public StructWithWritableFieldNullableStructDictionaryOfStringToIntWithAttributeOnType() {}

        public StructDictionary<string, int>? Field = new StructDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":null,"Property":{"a":1},"Property":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<ConcurrentDictionary<string, int>>>(yaml, options);
        CheckGenericDictionaryContent(obj.Property, 2);
    }

    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithAttributeOnType
    {
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyConcurrentDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentDictionary<string, int> Property { get; } = new ConcurrentDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_SortedDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":{"e":5},"Property":{"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToInt>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"d":4},"Property":null,"Property":{"a":1},"Property":{"b":2}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<SortedDictionary<string, int>>>(yaml, options);
        CheckGenericDictionaryContent(obj.Property, 2);
    }

    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToInt
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_SortedDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_SortedDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_SortedDictionaryOfStringToInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":4,"e":5,"f":6}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithoutPopulateAttribute() {}

        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithAttributeOnType() {}

        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_SortedDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandling>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_SortedDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_SortedDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_SortedDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_SortedDictionaryOfStringToInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":{"d":"4","e":"5","f":"6"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        CheckGenericDictionaryContent(obj.Property);
    }

    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertySortedDictionaryOfStringToIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public SortedDictionary<string, int> Property { get; } = new SortedDictionary<string, int>() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    [Theory]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructDictionary<string, int>>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructDictionary<string, int>?>))]
    public async Task CreationHandlingSetWithAttribute_PopulateWithoutSetterOnValueTypeThrows_Dictionary(Type type)
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper(yaml, type, options));
    }
}
