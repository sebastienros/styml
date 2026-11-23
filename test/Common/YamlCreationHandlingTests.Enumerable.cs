// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests;

#pragma warning disable xUnit2027 // Comparison of sets to linear containers have undefined results

public abstract partial class YamlCreationHandlingTests : SerializerTests
{
    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<List<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyListOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyListOfIntWithoutPopulateAttribute
    {
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyListOfIntWithAttributeOnType
    {
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyListOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyListOfIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyListOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyListOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public List<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IListOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<IList<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfInt
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IListOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IListOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IListOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithoutPopulateAttribute() {}

        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithAttributeOnType() {}

        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IListOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IListOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IListOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IListOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_ListOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IListOfInt_BackedBy_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_StructListOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IListOfInt_BackedBy_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IListOfInt_BackedBy_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IListOfInt_BackedBy_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithoutPopulateAttribute
    {
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithAttributeOnType
    {
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IListOfInt_BackedBy_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IListOfInt_BackedBy_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IListOfInt_BackedBy_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IListOfInt_BackedBy_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IListOfInt_BackedBy_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructList<int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIListOfInt_BackedBy_StructListOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public IList<int> Property { get; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_ListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElement>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElement));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IList_BackedBy_ListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_ListOfYamlElement_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElement>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IList_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<IList>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElement
    {
        public ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElement() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IList Property { get; } = new List<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    private static IEnumerable<YamlElement> ParseYamlArray(string yaml)
    {
        YamlElement doc = YamlElement.Parse(yaml);
        foreach (YamlElement element in doc.EnumerateArray())
        {
            yield return element;
        }
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IList_BackedBy_ListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IList_BackedBy_ListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IList_BackedBy_ListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithoutPopulateAttribute() {}

        public IList Property { get; } = new List<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyIList_BackedBy_ListOfYamlElementWithAttributeOnType() {}

        public IList Property { get; } = new List<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_StructListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElement>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElement));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_IList_BackedBy_StructListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_IList_BackedBy_StructListOfYamlElement_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElement>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElement
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public IList Property { get; } = new StructList<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_IList_BackedBy_StructListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_IList_BackedBy_StructListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_IList_BackedBy_StructListOfYamlElement()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        ((StructList<YamlElement>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithoutPopulateAttribute
    {
        public IList Property { get; } = new StructList<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyIList_BackedBy_StructListOfYamlElementWithAttributeOnType
    {
        public IList Property { get; } = new StructList<YamlElement>(ParseYamlArray("[1,2,3]"));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructListOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructListOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructListOfIntWithoutPopulateAttribute
    {
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class StructWithWritablePropertyStructListOfIntWithAttributeOnType
    {
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructListOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructListOfIntWithNumberHandling
    {
        public StructWithWritablePropertyStructListOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public StructWithWritablePropertyStructListOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritablePropertyStructListOfIntWithNumberHandlingWithAttributeOnType
    {
        public StructWithWritablePropertyStructListOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructList<int> Property { get; set; } = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritableFieldNullableStructListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":[5],"Field":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructListOfInt_FieldOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":null,"Field":[1],"Field":[2]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulatedFieldCanDeserializeNull_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":null}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfInt>(yaml, options);
        Assert.Null(obj.Field);
    }

    internal struct StructWithWritableFieldNullableStructListOfInt
    {
        public StructWithWritableFieldNullableStructListOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructList<int>? Field = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute)), includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_NullableStructListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    internal struct StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute
    {
        public StructWithWritableFieldNullableStructListOfIntWithoutPopulateAttribute() {}

        public StructList<int>? Field = new StructList<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritableFieldNullableStructListOfIntWithAttributeOnType
    {
        public StructWithWritableFieldNullableStructListOfIntWithAttributeOnType() {}

        public StructList<int>? Field = new StructList<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyQueueOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_QueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<Queue<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyQueueOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_QueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_QueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_QueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyQueueOfIntWithoutPopulateAttribute
    {
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyQueueOfIntWithAttributeOnType
    {
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_QueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyQueueOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_QueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyQueueOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_QueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_QueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_QueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyQueueOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Queue<int> Property { get; } = new Queue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Queue()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueue>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyQueue));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_Queue()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueue>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Queue_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<Queue>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyQueue
    {
        public ClassWithReadOnlyPropertyQueue() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public Queue Property { get; } = new Queue(ParseYamlArray("[1,2,3]").ToArray());
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_Queue()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_Queue()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_Queue()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyQueueWithoutPopulateAttribute() {}

        public Queue Property { get; } = new Queue(ParseYamlArray("[1,2,3]").ToArray());
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyQueueWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyQueueWithAttributeOnType() {}

        public Queue Property { get; } = new Queue(ParseYamlArray("[1,2,3]").ToArray());
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentQueueOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentQueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<ConcurrentQueue<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyConcurrentQueueOfInt
    {
        public ClassWithReadOnlyPropertyConcurrentQueueOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentQueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentQueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentQueueOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyConcurrentQueueOfIntWithoutPopulateAttribute() {}

        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyConcurrentQueueOfIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyConcurrentQueueOfIntWithAttributeOnType() {}

        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentQueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentQueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentQueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentQueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentQueueOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyConcurrentQueueOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentQueue<int> Property { get; } = new ConcurrentQueue<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyStackOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<Stack<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyStackOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyStackOfIntWithoutPopulateAttribute
    {
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyStackOfIntWithAttributeOnType
    {
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyStackOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyStackOfIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyStackOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyStackOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public Stack<int> Property { get; } = new Stack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Stack()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStack>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyStack));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_Stack()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStack>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Stack_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<Stack>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyStack
    {
        public ClassWithReadOnlyPropertyStack() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public Stack Property { get; } = new Stack(ParseYamlArray("[1,2,3]").ToArray());
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_Stack()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyStackWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_Stack()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyStackWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_Stack()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyStackWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property.Cast<YamlElement>().Select(x => x.GetInt32()));
    }

    internal struct ClassWithReadOnlyPropertyStackWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyStackWithoutPopulateAttribute() {}

        public Stack Property { get; } = new Stack(ParseYamlArray("[1,2,3]").ToArray());
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyStackWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyStackWithAttributeOnType() {}

        public Stack Property { get; } = new Stack(ParseYamlArray("[1,2,3]").ToArray());
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentStackOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentStackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<ConcurrentStack<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentStackOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentStackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentStackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentStackOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentStackOfIntWithoutPopulateAttribute
    {
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyConcurrentStackOfIntWithAttributeOnType
    {
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ConcurrentStackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ConcurrentStackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ConcurrentStackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ConcurrentStackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ConcurrentStackOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyConcurrentStackOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ConcurrentStack<int> Property { get; } = new ConcurrentStack<int>(new int[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<ICollection<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfInt
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithoutPopulateAttribute() {}

        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithAttributeOnType() {}

        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ICollectionOfInt_BackedBy_ListOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_ListOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new List<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithoutPopulateAttribute
    {
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithAttributeOnType
    {
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ICollectionOfInt_BackedBy_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructCollection<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyICollectionOfInt_BackedBy_StructCollectionOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ICollection<int> Property { get; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructCollectionOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructCollectionOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructCollectionOfInt
    {
        public StructWithWritablePropertyStructCollectionOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructCollection<int> Property { get; set; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute
    {
        public StructWithWritablePropertyStructCollectionOfIntWithoutPopulateAttribute() {}

        public StructCollection<int> Property { get; set; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritablePropertyStructCollectionOfIntWithAttributeOnType
    {
        public StructWithWritablePropertyStructCollectionOfIntWithAttributeOnType() {}

        public StructCollection<int> Property { get; set; } = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldStructCollectionOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field);
        obj.Field.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritableFieldStructCollectionOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field);
        obj.Field.Validate();
    }

    internal struct StructWithWritableFieldStructCollectionOfIntWithNumberHandling
    {
        public StructWithWritableFieldStructCollectionOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructCollection<int> Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute)), includeFields: true);
        string yaml = """{"Field":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field);
        obj.Field.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Field":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field);
        obj.Field.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructCollectionOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Field":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field);
        obj.Field.Validate();
    }

    internal struct StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructCollection<int> Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithAttributeOnType
    {
        public StructWithWritableFieldStructCollectionOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructCollection<int> Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritableFieldNullableStructCollectionOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":[5],"Field":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructCollectionOfInt_FieldOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":null,"Field":[1],"Field":[2]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulatedFieldCanDeserializeNull_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":null}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfInt>(yaml, options);
        Assert.Null(obj.Field);
    }

    internal class StructWithWritableFieldNullableStructCollectionOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructCollection<int>? Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute)), includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_NullableStructCollectionOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    internal class StructWithWritableFieldNullableStructCollectionOfIntWithoutPopulateAttribute
    {
        public StructCollection<int>? Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class StructWithWritableFieldNullableStructCollectionOfIntWithAttributeOnType
    {
        public StructCollection<int>? Field = new StructCollection<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_PropertyOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":null,"Property":[1],"Property":[2]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty<ISet<int>>>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithoutPopulateAttribute
    {
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithAttributeOnType
    {
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandling
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ISetOfInt_BackedBy_HashSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
    }

    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_HashSetOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new HashSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfInt
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithoutPopulateAttribute() {}

        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithAttributeOnType
    {
        public ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithAttributeOnType() {}

        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandling
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_ISetOfInt_BackedBy_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        ((StructSet<int>)obj.Property).Validate();
    }

    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassWithReadOnlyPropertyISetOfInt_BackedBy_StructSetOfIntWithNumberHandlingWithAttributeOnType
    {
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public ISet<int> Property { get; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructSetOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructSetOfInt_PropertyOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":[4],"Property":[5],"Property":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructSetOfInt
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute)));
        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal class StructWithWritablePropertyStructSetOfIntWithoutPopulateAttribute
    {
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class StructWithWritablePropertyStructSetOfIntWithAttributeOnType
    {
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithNumberHandling>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritablePropertyStructSetOfIntWithNumberHandling));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructSetOfIntWithNumberHandling
    {
        public StructWithWritablePropertyStructSetOfIntWithNumberHandling() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute)));
        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_StructSetOfInt_WithNumberHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Property":["4","5","6"]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Property);
        obj.Property.Validate();
    }

    internal struct StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute
    {
        public StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithoutPopulateAttribute() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithAttributeOnType
    {
        public StructWithWritablePropertyStructSetOfIntWithNumberHandlingWithAttributeOnType() {}

        [YamlNumberHandling(YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString)]
        public StructSet<int> Property { get; set; } = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
        CheckTypeHasSinglePropertyWithPopulateHandling(options, typeof(StructWithWritableFieldNullableStructSetOfInt));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CanPopulate_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfIntWithAttributeOnType>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":[5],"Field":[6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_NullableStructSetOfInt_FieldOccurringMultipleTimes_NullInBetween()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, includeFields: true);
        string yaml = """{"Field":[4],"Field":null,"Field":[1],"Field":[2]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfInt>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 2), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulatedFieldCanDeserializeNull_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true);
        string yaml = """{"Field":null}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfInt>(yaml, options);
        Assert.Null(obj.Field);
    }

    internal struct StructWithWritableFieldNullableStructSetOfInt
    {
        public StructWithWritableFieldNullableStructSetOfInt() {}

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public StructSet<int>? Field = new StructSet<int>() { 1, 2, 3 };
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadata_CanPopulate_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: GetFirstPropertyToPopulateForTypeModifier(typeof(StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute)), includeFields: true);
        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithMetadataOnType_CanPopulate_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, modifier: ti =>
        {
            if (ti.Type == typeof(StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute))
            {
                ti.PreferredPropertyObjectCreationHandling = YamlObjectCreationHandling.Populate;
            }
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulate_NullableStructSetOfInt()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Field":[4,5,6]}""";
        var obj = await Serializer.DeserializeWrapper<StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute>(yaml, options);
        Assert.Equal(Enumerable.Range(1, 6), obj.Field.Value);
        obj.Field.Value.Validate();
    }

    internal struct StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute
    {
        public StructWithWritableFieldNullableStructSetOfIntWithoutPopulateAttribute() {}

        public StructSet<int>? Field = new StructSet<int>() { 1, 2, 3 };
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal struct StructWithWritableFieldNullableStructSetOfIntWithAttributeOnType
    {
        public StructWithWritableFieldNullableStructSetOfIntWithAttributeOnType() {}

        public StructSet<int>? Field = new StructSet<int>() { 1, 2, 3 };
    }

    [Theory]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructList<int>>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructList<int>?>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructCollection<int>>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructCollection<int>?>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructSet<int>>))]
    [InlineData(typeof(ClassWithReadOnlyProperty<StructSet<int>?>))]
    public async Task CreationHandlingSetWithAttribute_PopulateWithoutSetterOnValueTypeThrows_Enumerable(Type type)
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper(yaml, type, options));
    }
}
