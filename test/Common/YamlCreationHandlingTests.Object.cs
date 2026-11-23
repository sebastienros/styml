// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests;

public abstract partial class YamlCreationHandlingTests : SerializerTests
{
    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClass()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"StringValue":"NewValue"}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClass_PropertyOccurredMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);

        yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}, "Property":{"IntValue":45}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(45, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateReadOnlyProperty_SimpleClass_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":null}""";
        await Assert.ThrowsAsync<InvalidOperationException>(() => Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClass>(yaml, options));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClass_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":null}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.Null(obj.Property);

        yaml = """{"Property":null,"Property":{}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Equal("SomeValue", obj.Property.StringValue);
        Assert.Equal(42, obj.Property.IntValue);

        yaml = """{"Property":null,"Property":{"StringValue":"NewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(42, obj.Property.IntValue);

        yaml = """{"Property":null,"Property":{"StringValue":"NewValue"},"Property":{"IntValue":45}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(45, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingEffectCancelledWithModifier_ClassWithReadOnlyProperty_SimpleClass()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReadOnlyProperty_SimpleClass))
            {
                Assert.Equal(YamlObjectCreationHandling.Populate, ti.Properties[0].ObjectCreationHandling);
                ti.Properties[0].ObjectCreationHandling = null;
            }
        });

        string yaml = """{"Property":{"StringValue":"NewValue"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("InitialValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingEffectCancelledWithModifier_ClassWithWritableProperty_SimpleClass()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithWritableProperty_SimpleClass))
            {
                Assert.Equal(YamlObjectCreationHandling.Populate, ti.Properties[0].ObjectCreationHandling);
                ti.Properties[0].ObjectCreationHandling = null;
            }
        });

        string yaml = """{"Property":{}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("SomeValue", obj.Property.StringValue);
        Assert.Equal(42, obj.Property.IntValue);

        yaml = """{"Property":{"StringValue":"NewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClass>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(42, obj.Property.IntValue);
    }

    internal class ClassWithReadOnlyProperty_SimpleClass
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClass Property { get; } = new()
        {
            StringValue = "InitialValue",
            IntValue = 43,
        };
    }

    internal class ClassWithWritableProperty_SimpleClass
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClass? Property { get; set; } = new()
        {
            StringValue = "InitialValue",
            IntValue = 43,
        };
    }

    internal class SimpleClass
    {
        public string StringValue { get; set; } = "SomeValue";
        public int IntValue { get; set; } = 42;
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateWritableProperty_SimpleStruct()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"StringValue":"NewValue"}}""";

        var obj = await Serializer.DeserializeWrapper<StructWithWritableProperty_SimpleStruct>(yaml, options);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateWritableProperty_SimpleStruct_PropertyOccurredMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}}""";

        var obj = await Serializer.DeserializeWrapper<StructWithWritableProperty_SimpleStruct>(yaml, options);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);

        yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}, "Property":{"IntValue":45}}""";
        obj = await Serializer.DeserializeWrapper<StructWithWritableProperty_SimpleStruct>(yaml, options);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(45, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleStruct_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":null}""";
        await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithWritableProperty_SimpleStruct>(yaml, options));

        yaml = """{"Property":{}, "Property":null}""";
        await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<StructWithWritableProperty_SimpleStruct>(yaml, options));
    }

    internal struct StructWithReadOnlyProperty_SimpleStruct
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleStruct Property { get; } = new()
        {
            StringValue = "InitialValue",
            IntValue = 43,
        };

        public StructWithReadOnlyProperty_SimpleStruct() { }
    }

    internal struct StructWithWritableProperty_SimpleStruct
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleStruct Property { get; set; } = new()
        {
            StringValue = "InitialValue",
            IntValue = 43,
        };

        public StructWithWritableProperty_SimpleStruct() { }
    }

    internal struct SimpleStruct
    {
        public string StringValue { get; set; } = "SomeValue";
        public int IntValue { get; set; } = 42;

        public SimpleStruct() { }
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithSmallParametrizedCtor()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"StringValue":"NewValue"}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithSmallParametrizedCtor_PropertyOccurredMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);

        yaml = """{"Property":{"StringValue":"NewValue"}, "Property":{}, "Property":{"IntValue":45}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(45, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateReadOnlyProperty_SimpleClassWithSmallParametrizedCtor_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":null}""";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithSmallParametrizedCtor_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":null}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.Null(obj.Property);

        yaml = """{"Property":null,"Property":{}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Null(obj.Property.StringValue);
        Assert.Equal(0, obj.Property.IntValue);

        yaml = """{"Property":null,"Property":{"StringValue":"NewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(0, obj.Property.IntValue);

        yaml = """{"Property":null,"Property":{"StringValue":"NewValue"},"Property":{"IntValue":45}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithSmallParametrizedCtor>(yaml, options);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.StringValue);
        Assert.Equal(45, obj.Property.IntValue);
    }

    internal class ClassWithReadOnlyProperty_SimpleClassWithSmallParametrizedCtor
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClassWithSmallParametrizedCtor Property { get; } = new("InitialValue", 43);
    }

    internal class ClassWithWritableProperty_SimpleClassWithSmallParametrizedCtor
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClassWithSmallParametrizedCtor? Property { get; set; } = new("InitialValue", 43);
    }

    internal class SimpleClassWithSmallParametrizedCtor
    {
        public SimpleClassWithSmallParametrizedCtor(string? stringValue, int intValue)
        {
            StringValue = stringValue;
            IntValue = intValue;
        }

        public string? StringValue { get; set; }
        public int IntValue { get; set; }
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithLargeParametrizedCtor()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":{"A":"NewValue"}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.A);
        Assert.Equal(43, obj.Property.B);
        Assert.Equal("InitialValue2", obj.Property.C);
        Assert.Equal(44, obj.Property.D);
        Assert.Equal("InitialValue3", obj.Property.E);
        Assert.Equal(45, obj.Property.F);
        Assert.Equal("InitialValue4", obj.Property.G);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulateReadOnlyProperty_SimpleClassWithLargeParametrizedCtor_PropertyOccurredMultipleTimes()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":{"A":"NewValue"}, "Property":{}}""";

        var obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.A);
        Assert.Equal(43, obj.Property.B);
        Assert.Equal("InitialValue2", obj.Property.C);
        Assert.Equal(44, obj.Property.D);
        Assert.Equal("InitialValue3", obj.Property.E);
        Assert.Equal(45, obj.Property.F);
        Assert.Equal("InitialValue4", obj.Property.G);

        yaml = """{"Property":{"A":"NewValue"}, "Property":{}, "Property":{"B":99}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.A);
        Assert.Equal(99, obj.Property.B);
        Assert.Equal("InitialValue2", obj.Property.C);
        Assert.Equal(44, obj.Property.D);
        Assert.Equal("InitialValue3", obj.Property.E);
        Assert.Equal(45, obj.Property.F);
        Assert.Equal("InitialValue4", obj.Property.G);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateReadOnlyProperty_SimpleClassWithLargeParametrizedCtor_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """{"Property":null}""";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<ClassWithReadOnlyProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_PopulateWritableProperty_SimpleClassWithLargeParametrizedCtor_DeserializingNull()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        string yaml = """{"Property":null}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.Null(obj.Property);

        yaml = """{"Property":null,"Property":{}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Null(obj.Property.A);
        Assert.Equal(0, obj.Property.B);
        Assert.Null(obj.Property.C);
        Assert.Equal(0, obj.Property.D);
        Assert.Null(obj.Property.E);
        Assert.Equal(0, obj.Property.F);
        Assert.Null(obj.Property.G);

        yaml = """{"Property":null,"Property":{},"Property":{"A":"NewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.A);
        Assert.Equal(0, obj.Property.B);
        Assert.Null(obj.Property.C);
        Assert.Equal(0, obj.Property.D);
        Assert.Null(obj.Property.E);
        Assert.Equal(0, obj.Property.F);
        Assert.Null(obj.Property.G);

        yaml = """{"Property":null,"Property":{},"Property":{"A":"NewValue"},"Property":{"F":99}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithWritableProperty_SimpleClassWithLargeParametrizedCtor>(yaml, options);
        Assert.NotNull(obj);
        Assert.NotNull(obj.Property);
        Assert.Equal("NewValue", obj.Property.A);
        Assert.Equal(0, obj.Property.B);
        Assert.Null(obj.Property.C);
        Assert.Equal(0, obj.Property.D);
        Assert.Null(obj.Property.E);
        Assert.Equal(99, obj.Property.F);
        Assert.Null(obj.Property.G);
    }

    internal class ClassWithReadOnlyProperty_SimpleClassWithLargeParametrizedCtor
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClassWithLargeParametrizedCtor Property { get; } = new("InitialValue1", 43, "InitialValue2", 44, "InitialValue3", 45, "InitialValue4");
    }

    internal class ClassWithWritableProperty_SimpleClassWithLargeParametrizedCtor
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SimpleClassWithLargeParametrizedCtor? Property { get; set; } = new("InitialValue1", 43, "InitialValue2", 44, "InitialValue3", 45, "InitialValue4");
    }

    internal class SimpleClassWithLargeParametrizedCtor
    {
        public SimpleClassWithLargeParametrizedCtor(string? a, int b, string? c, int d, string? e, int f, string? g)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
            G = g;
        }

        public string? A { get; set; }
        public int B { get; set; }
        public string? C { get; set; }
        public int D { get; set; }
        public string? E { get; set; }
        public int F { get; set; }
        public string? G { get; set; }
    }

    [Fact]
    [Trait("category", "jsonoutput")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateAndSerialize_ClassWithEnabledPolymorphismOnSerialization()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins);
        ClassWithProperty_BaseClassWithPolymorphismOnSerializationOnly obj = new();
        string yaml = await Serializer.SerializeWrapper(obj, options);
        Assert.Equal("""{"Property":{"DerivedClassProp":"derived","BaseClassProp":"base"}}""", yaml);

        yaml = """{"Property":{"BaseClassProp":"BaseNewValue","DerivedClassProp":"DerivedNewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithProperty_BaseClassWithPolymorphismOnSerializationOnly>(yaml, options);
        Assert.IsType<DerivedClass_DerivingFrom_BaseClassWithPolymorphismOnSerializationOnly>(obj.Property);
        Assert.Equal("BaseNewValue", obj.Property.BaseClassProp);
        // DerivedNewValue should be ignored because we didn't setup polymorphic deserialization
        Assert.Equal("derived", ((DerivedClass_DerivingFrom_BaseClassWithPolymorphismOnSerializationOnly)obj.Property).DerivedClassProp);

        yaml = """{"Property":null}""";
        obj = await Serializer.DeserializeWrapper<ClassWithProperty_BaseClassWithPolymorphismOnSerializationOnly>(yaml, options);
        Assert.Null(obj.Property);

        yaml = """{"Property":null,"Property":{"BaseClassProp":"BaseNewValue","DerivedClassProp":"DerivedNewValue"}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithProperty_BaseClassWithPolymorphismOnSerializationOnly>(yaml, options);
        Assert.IsType<BaseClassWithPolymorphismOnSerializationOnly>(obj.Property);
        Assert.Equal("BaseNewValue", obj.Property.BaseClassProp);
    }

    public class ClassWithProperty_BaseClassWithPolymorphismOnSerializationOnly
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public BaseClassWithPolymorphismOnSerializationOnly? Property { get; set; } =
            new DerivedClass_DerivingFrom_BaseClassWithPolymorphismOnSerializationOnly()
            {
                BaseClassProp = "base",
                DerivedClassProp = "derived",
            };
    }

    [YamlDerivedType(typeof(DerivedClass_DerivingFrom_BaseClassWithPolymorphismOnSerializationOnly))]
    public class BaseClassWithPolymorphismOnSerializationOnly
    {
        public string BaseClassProp { get; set; } = "BaseInitial";
    }

    public class DerivedClass_DerivingFrom_BaseClassWithPolymorphismOnSerializationOnly : BaseClassWithPolymorphismOnSerializationOnly
    {
        public string DerivedClassProp { get; set; } = "DerivedInitial";
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CannotPopulateOrSerialize_ClassWithEnabledPolymorphismOnBothSerializationAndDeserialization()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        ClassWithProperty_BaseClassWithPolymorphism obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        string yaml = """{"Property":{"$type":"derived","BaseClassProp":"BaseNewValue","DerivedClassProp":"DerivedNewValue"}}""";
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithProperty_BaseClassWithPolymorphism>(yaml, options));
    }

    public class ClassWithProperty_BaseClassWithPolymorphism
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public BaseClassWithPolymorphism Property { get; set; } =
            new DerivedClass_DerivingFrom_BaseClassWithPolymorphism()
            {
                BaseClassProp = "base",
                DerivedClassProp = "derived",
            };
    }

    [YamlDerivedType(typeof(DerivedClass_DerivingFrom_BaseClassWithPolymorphism), "derived")]
    public class BaseClassWithPolymorphism
    {
        public string BaseClassProp { get; set; } = "BaseInitial";
    }

    public class DerivedClass_DerivingFrom_BaseClassWithPolymorphism : BaseClassWithPolymorphism
    {
        public string DerivedClassProp { get; set; } = "DerivedInitial";
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CannotPopulateOrSerialize_ClassWithEnabledPolymorphismOnBothSerializationAndDeserializationRecursive()
    {
        // Verifies that polymorphism check doesn't have ordering issue during Configure.
        // This may happen if polymorphism is setup after properties are configured.
        YamlSerializerOptions options = Serializer.CreateOptions();
        BaseClassRecursive obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        string yaml = """{"Next":{"$type":"derived","BaseClassProp":"BaseNewValue","DerivedClassProp":"DerivedNewValue"}}""";
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<BaseClassRecursive>(yaml, options));

        yaml = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<BaseClassRecursive>(yaml, options));
    }

#pragma warning disable STYML1240 // Derived types are intentionally not assignable to the base type for these tests.
    [YamlDerivedType(typeof(BaseClassWithPolymorphism), "base")]
    [YamlDerivedType(typeof(DerivedClass_DerivingFrom_BaseClassWithPolymorphism), "derived")]
    public class BaseClassRecursive
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public BaseClassRecursive Next { get; set; }

        public string BaseClassProp { get; set; } = "BaseInitial";
    }
#pragma warning restore STYML1240

    public class DerivedClass_DerivingFrom_BaseClassRecursive : BaseClassRecursive
    {
        public string DerivedClassProp { get; set; } = "DerivedInitial";
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CannotPopulateOrSerialize_ClassWithReadOnlyPropertyAndIgnoreReadOnlyProperties()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.IgnoreReadOnlyProperties = true;
        });

        ClassWithReadOnlyInitializedProperty<SimpleClass> obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithReadOnlyInitializedProperty<SimpleClass>>("{}", options));
    }

    [Fact]
    [Trait("category", "jsonoutput")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyFieldAndIgnoreReadOnlyProperties()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            opt.IgnoreReadOnlyProperties = true;
        });

        ClassWithReadOnlyInitializedField<SimpleClass> obj = new();
        string yaml = await Serializer.SerializeWrapper(obj, options);
        Assert.Equal("""{"Field":{"StringValue":"SomeValue","IntValue":42}}""", yaml);

        yaml = """{"Field":{"StringValue":"SomeNewValue"},"Field":{"IntValue":43}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyInitializedField<SimpleClass>>(yaml, options);
        Assert.Equal("SomeNewValue", obj.Field.StringValue);
        Assert.Equal(43, obj.Field.IntValue);
    }

    [Fact]
    [Trait("category", "jsonoutput")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithWritablePropertyAndIgnoreReadOnlyProperties()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            opt.IgnoreReadOnlyProperties = true;
        });

        ClassWithInitializedProperty<SimpleClass> obj = new();
        string yaml = await Serializer.SerializeWrapper(obj, options);
        Assert.Equal("""{"Property":{"StringValue":"SomeValue","IntValue":42}}""", yaml);

        yaml = """{"Property":{"StringValue":"SomeNewValue"},"Property":{"IntValue":43}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithInitializedProperty<SimpleClass>>(yaml, options);
        Assert.Equal("SomeNewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    [Trait("category", "jsonoutput")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithReadOnlyPropertyAndIgnoreReadOnlyFields()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            opt.IgnoreReadOnlyFields = true;
        });

        ClassWithReadOnlyInitializedProperty<SimpleClass> obj = new();
        string yaml = await Serializer.SerializeWrapper(obj, options);
        Assert.Equal("""{"Property":{"StringValue":"SomeValue","IntValue":42}}""", yaml);

        yaml = """{"Property":{"StringValue":"SomeNewValue"},"Property":{"IntValue":43}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithReadOnlyInitializedProperty<SimpleClass>>(yaml, options);
        Assert.Equal("SomeNewValue", obj.Property.StringValue);
        Assert.Equal(43, obj.Property.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CannotPopulateOrSerialize_ClassWithReadOnlyFieldAndIgnoreReadOnlyFields()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.IgnoreReadOnlyFields = true;
        });

        ClassWithReadOnlyInitializedField<SimpleClass> obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithReadOnlyInitializedField<SimpleClass>>("{}", options));
    }

    [Fact]
    [Trait("category", "jsonoutput")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateOrSerialize_ClassWithFieldAndIgnoreReadOnlyFields()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(includeFields: true, configure: (opt) =>
        {
            opt.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            opt.IgnoreReadOnlyFields = true;
        });

        ClassWithInitializedField<SimpleClass> obj = new();
        string yaml = await Serializer.SerializeWrapper(obj, options);
        Assert.Equal("""{"Field":{"StringValue":"SomeValue","IntValue":42}}""", yaml);

        yaml = """{"Field":{"StringValue":"SomeNewValue"},"Field":{"IntValue":43}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithInitializedField<SimpleClass>>(yaml, options);
        Assert.Equal("SomeNewValue", obj.Field.StringValue);
        Assert.Equal(43, obj.Field.IntValue);
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CannotPopulateOrSerialize_ReferenceHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.ReferenceHandler = ReferenceHandler.Preserve;
        });

        ClassWithInitializedProperty<SimpleClass> obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithInitializedProperty<SimpleClass>>("{}", options));
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CannotPopulateOrSerialize_ReferenceHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        ClassWithWritablePropertyWithoutPopulate<SimpleClass> obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithWritablePropertyWithoutPopulate<SimpleClass>>("{}", options));
    }

    [Fact]
    public async Task CreationHandlingSetWithAttributeOnType_CannotPopulateOrSerialize_ReferenceHandling()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.ReferenceHandler = ReferenceHandler.Preserve;
        });

        ClassWithDefaultPopulateAndProperty<SimpleClass> obj = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.SerializeWrapper(obj, options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Serializer.DeserializeWrapper<ClassWithDefaultPopulateAndProperty<SimpleClass>>("{}", options));
    }

    public class ClassWithRequiredProperty
    {
        [YamlRequired]
        public string RequiredValue { get; set; } = "InitialRequiredValue";

        public string OptionalValue { get; set; } = "InitialOptionalValue";
    }

    [Fact]
    public async Task CreationHandlingSetWithOptions_CanPopulateOrSerialize_RequiredPropertiesRecursive()
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: (opt) =>
        {
            opt.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins;
            opt.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;
        });

        string yaml = """{"Value":1,"Next":null}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithRecursiveRequiredProperty>(yaml, options);
        Check(obj, 1);

        yaml = """{"Value":1}""";
        var exception = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithRecursiveRequiredProperty>(yaml, options));
        Assert.Contains(nameof(ClassWithRecursiveRequiredProperty.Next), exception.Message);

        yaml = """{"Value":1,"Next":{"Value":2,"Next":null},"Next":{"Next":{"Value":3,"Next":null}}}""";
        obj = await Serializer.DeserializeWrapper<ClassWithRecursiveRequiredProperty>(yaml, options);
        Check(obj, 3);

        yaml = """{"Value":1,"Next":{"Value":2,"Next":null},"Next":{"Next":{"Value":3}}}""";
        exception = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithRecursiveRequiredProperty>(yaml, options));
        Assert.Contains(nameof(ClassWithRecursiveRequiredProperty.Next), exception.Message);

        yaml = """{"Value":1,"Next":{"Value":2},"Next":{"Next":{"Value":3,"Next":null}}}""";
        exception = await Assert.ThrowsAsync<YamlException>(async () => await Serializer.DeserializeWrapper<ClassWithRecursiveRequiredProperty>(yaml, options));
        Assert.Contains(nameof(ClassWithRecursiveRequiredProperty.Next), exception.Message);

        static void Check(ClassWithRecursiveRequiredProperty obj, int lastNumber)
        {
            Assert.True(lastNumber >= 1);

            for (int i = 1; i <= lastNumber; i++)
            {
                Assert.Equal(obj.Value, i);

                if (i == lastNumber)
                {
                    Assert.Null(obj.Next);
                }
                else
                {
                    Assert.NotNull(obj.Next);
                    obj = obj.Next;
                }
            }
        }
    }

    public class ClassWithRecursiveRequiredProperty
    {
        public int Value { get; set; }

        [YamlRequired]
        public ClassWithRecursiveRequiredProperty? Next { get; set; }
    }

    [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
    [InlineData(typeof(SimpleClass))]
    [InlineData(typeof(SimpleClassWithSmallParametrizedCtor))]
    [InlineData(typeof(SimpleClassWithLargeParametrizedCtor))]
    [RequiresDynamicCode("Uses Type.MakeGenericType to construct the generic type under test.")]
    public async Task CreationHandlingSetWithAttribute_CanPopulateOrSerialize_Callbacks(Type type)
    {
        Type finalType = typeof(ClassWithWritableProperty<>).MakeGenericType(type);
        int timesOnDeserializingCalled = 0;
        int timesOnDeserializedCalled = 0;

        YamlSerializerOptions options = Serializer.CreateOptions(configure: o => o.DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, modifier: ti =>
        {
            if (ti.Type == type)
            {
                ti.OnDeserializing = (obj) =>
                {
                    Assert.Equal(timesOnDeserializingCalled, timesOnDeserializedCalled);
                    timesOnDeserializingCalled++;
                };
                ti.OnDeserialized = (obj) =>
                {
                    timesOnDeserializedCalled++;
                    Assert.Equal(timesOnDeserializingCalled, timesOnDeserializedCalled);
                };
            }
        });

        string yaml = """{"Property": {}}""";
        await DeserializeAndEnsureCallbacksCalled(yaml, 1);

        yaml = """{"Property": null}""";
        await DeserializeAndEnsureCallbacksCalled(yaml, 0);

        yaml = """{"Property": null, "Property":{}}""";
        await DeserializeAndEnsureCallbacksCalled(yaml, 1);

        yaml = """{"Property": null, "Property":{}, "Property":{}, "Property":{}}""";
        await DeserializeAndEnsureCallbacksCalled(yaml, 3);

        async Task DeserializeAndEnsureCallbacksCalled(string yaml, int expectedTimesCallbacksCalled)
        {
            var obj = await Serializer.DeserializeWrapper(yaml, finalType, options);
            Assert.Equal(expectedTimesCallbacksCalled, timesOnDeserializingCalled);
            Assert.Equal(timesOnDeserializingCalled, timesOnDeserializedCalled);
            timesOnDeserializingCalled = 0;
            timesOnDeserializedCalled = 0;
        }
    }

    [Theory]
    [InlineData(typeof(IInterfaceWithInterfacePropertyWithPopulateOnType), typeof(ClassImplementingInterfaceWithPopulateOnTypeWithInterfaceProperty), true)]
    [InlineData(typeof(ClassImplementingInterfaceWithPopulateOnTypeWithInterfaceProperty), null, false)]
    [InlineData(typeof(IInterfaceWithInterfaceProperty), typeof(ClassImplementingInterfaceWithInterfaceProperty), false)]
    [InlineData(typeof(ClassImplementingInterfaceWithInterfaceProperty), null, true)]
    public async Task CreationHandlingSetWithAttributeOnType_Interfaces(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type typeToDeserialize,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? typeToCreate,
        bool shouldPopulate)
    {
        YamlSerializerOptions options = Serializer.CreateOptions(modifier: ti =>
        {
            if (ti.Type == typeToDeserialize)
            {
                if (typeToCreate != null)
                {
                    ti.CreateObject = () => Activator.CreateInstance(typeToCreate);
                }

                if (shouldPopulate)
                {
                    Assert.Equal(YamlObjectCreationHandling.Populate, ti.PreferredPropertyObjectCreationHandling);
                }
                else
                {
                    Assert.Null(ti.PreferredPropertyObjectCreationHandling);
                }
            }
        });

        string yaml = """{"Property":{"StringValue": "NewValue", "IntValue": 43}}""";
        object obj = await Serializer.DeserializeWrapper(yaml, typeToDeserialize, options);
        ISimpleInterface prop = (ISimpleInterface)typeToDeserialize.GetProperty("Property").GetValue(obj);
        Assert.NotNull(prop);

        if (shouldPopulate)
        {
            Assert.Equal("NewValue", prop.StringValue);
            Assert.Equal(43, prop.IntValue);
        }
        else
        {
            // no getter therefore value is unused
            Assert.Equal("InitialValue", prop.StringValue);
            Assert.Equal(42, prop.IntValue);
        }
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal interface IInterfaceWithInterfacePropertyWithPopulateOnType
    {
        public ISimpleInterface Property { get; }
    }

    internal class ClassImplementingInterfaceWithPopulateOnTypeWithInterfaceProperty : IInterfaceWithInterfacePropertyWithPopulateOnType
    {
        public ISimpleInterface Property { get; } = new SimpleClassImplementingInterface() { StringValue = "InitialValue", IntValue = 42 };
    }

    internal interface IInterfaceWithInterfaceProperty
    {
        public ISimpleInterface Property { get; }
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    internal class ClassImplementingInterfaceWithInterfaceProperty : IInterfaceWithInterfaceProperty
    {
        public ISimpleInterface Property { get; } = new SimpleClassImplementingInterface() { StringValue = "InitialValue", IntValue = 42 };
    }

    internal class SimpleClassImplementingInterface : ISimpleInterface
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }
    }

    internal interface ISimpleInterface
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }
    }

    [Theory]
    [MemberData(nameof(CombinationsForPopulatableProperty))]
    public async Task ReplaceOnType_SetWithMetadata_PopulatableProperty_PropagatesCorrectly(bool expectPopulate, YamlObjectCreationHandling optionsValue, YamlObjectCreationHandling? typeValue, YamlObjectCreationHandling? propertyValue)
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: opt =>
        {
            opt.PreferredObjectCreationHandling = optionsValue;
        },
        modifier: ti =>
        {
            if (ti.Type == typeof(ClassWithReplaceOnTypeAndWritableProperty_SimpleClass))
            {
                Assert.Equal(YamlObjectCreationHandling.Replace, ti.PreferredPropertyObjectCreationHandling);
                ti.PreferredPropertyObjectCreationHandling = typeValue;
                ti.Properties[0].ObjectCreationHandling = propertyValue;
            }
        });

        string yaml = """{"Property":{"StringValue":"NewValue"}}""";
        var obj = await Serializer.DeserializeWrapper<ClassWithReplaceOnTypeAndWritableProperty_SimpleClass>(yaml, options);
        Assert.Equal("NewValue", obj.Property.StringValue);

        if (expectPopulate)
        {
            Assert.Equal(43, obj.Property.IntValue);
        }
        else
        {
            Assert.Equal(42, obj.Property.IntValue);
        }
    }

    public static IEnumerable<object[]> CombinationsForPopulatableProperty()
    {
        foreach (YamlObjectCreationHandling optionsSett in new YamlObjectCreationHandling[] { YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
        {
            foreach (YamlObjectCreationHandling? typeSett in new YamlObjectCreationHandling?[] { null, YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
            {
                foreach (YamlObjectCreationHandling? propSett in new YamlObjectCreationHandling?[] { null, YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
                {
                    YamlObjectCreationHandling effectiveHandling = propSett ?? typeSett ?? optionsSett;
                    bool result = effectiveHandling == YamlObjectCreationHandling.Populate;
                    yield return new object[] { result, optionsSett, typeSett, propSett };
                }
            }
        }
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Replace)]
    internal class ClassWithReplaceOnTypeAndWritableProperty_SimpleClass
    {
        public SimpleClass Property { get; set; } = new SimpleClass()
        {
            StringValue = "InitialValue",
            IntValue = 43,
        };
    }

    [Theory]
    [MemberData(nameof(CombinationsForNonPopulatableProperty))]
    public async Task ReplaceOnType_SetWithMetadata_NonPopulatableProperty_PropagatesCorrectly(bool expectError, YamlObjectCreationHandling optionsValue, YamlObjectCreationHandling? typeValue, YamlObjectCreationHandling? propertyValue)
    {
        YamlSerializerOptions options = Serializer.CreateOptions(configure: opt =>
        {
            opt.PreferredObjectCreationHandling = optionsValue;
        },
        modifier: ti =>
        {
            if (ti.Type == typeof(SimpleClassWitNonPopulatableProperty))
            {
                ti.PreferredPropertyObjectCreationHandling = typeValue;
                ti.Properties[0].ObjectCreationHandling = propertyValue;
            }
        });

        string yaml = """{"Property":9}""";
        
        if (expectError)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.DeserializeWrapper<SimpleClassWitNonPopulatableProperty>(yaml, options));
        }
        else
        {
            var obj = await Serializer.DeserializeWrapper<SimpleClassWitNonPopulatableProperty>(yaml, options);
            Assert.Equal(9, obj.Property);
        }
    }

    public static IEnumerable<object[]> CombinationsForNonPopulatableProperty()
    {
        foreach (YamlObjectCreationHandling optionsSett in new YamlObjectCreationHandling[] { YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
        {
            foreach (YamlObjectCreationHandling? typeSett in new YamlObjectCreationHandling?[] { null, YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
            {
                foreach (YamlObjectCreationHandling? propSett in new YamlObjectCreationHandling?[] { null, YamlObjectCreationHandling.Replace, YamlObjectCreationHandling.Populate })
                {
                    bool result = propSett == YamlObjectCreationHandling.Populate;
                    yield return new object[] { result, optionsSett, typeSett, propSett };
                }
            }
        }
    }

    internal class SimpleClassWitNonPopulatableProperty
    {
        public int Property { get; set; } = 7;
    }

    [Fact]
    public async Task CreationHandlingSetWithAttribute_CanPopulate_Class()
    {
        YamlSerializerOptions options = Serializer.CreateOptions();
        string yaml = """
            {
                "PopulatedPropertyReadOnly":
                {
                    "IntValue": 43
                },
                "PopulatedPropertySimple":
                {
                    "IntValue": 44
                }
            }
            """;

        var obj = await Serializer.DeserializeWrapper<ClassWithClassProperty>(yaml, options);
        Assert.NotNull(obj);
        Assert.Equal("InitialForPopulate1", obj.PopulatedPropertyReadOnly.StringValue);
        Assert.Equal(43, obj.PopulatedPropertyReadOnly.IntValue);

        Assert.Equal("InitialForPopulate2", obj.PopulatedPropertySimple.StringValue);
        Assert.Equal(44, obj.PopulatedPropertySimple.IntValue);
    }

    internal class ClassWithClassProperty
    {
        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SomeClass PopulatedPropertyReadOnly { get; } = new() { StringValue = "InitialForPopulate1" };

        private SomeClass _populatedSimple = new() { StringValue = "InitialForPopulate2" };

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SomeClass PopulatedPropertySimple
        {
            get => _populatedSimple;
            set => Assert.Fail("Setter should not be used");
        }

        private SomeClass _populatedWithChildren =
            new()
            {
                StringValue = "InitialForPopulate3",
                ReplacedChild = new()
                {
                    StringValue = "ShouldBeReplaced",
                    IntValue = 123,
                },
                PopulatedChild = new()
                {
                    StringValue = "InitialForPopulate4",
                    IntValue = 43,
                    ReplacedChild = new() { StringValue = "ShouldBeReplaced" }
                },
            };

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SomeClass PopulatedPropertyWithChildren
        {
            get => _populatedWithChildren;
            set => Assert.Fail("Setter should not be used");
        }
    }

    internal class SomeClass
    {
        public string StringValue { get; set; } = "InitialSomeClass";
        public int IntValue { get; set; } = 42;
        public SomeClass? ReplacedChild { get; set; }

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public SomeClass? PopulatedChild { get; set; }
    }

    [Theory]
    [InlineData((YamlObjectCreationHandling)(-1))]
    [InlineData((YamlObjectCreationHandling)2)]
    [InlineData((YamlObjectCreationHandling)int.MaxValue)]
    public void YamlObjectCreationHandlingAttribute_InvalidConstructorArgument_ThrowsArgumentOutOfRangeException(YamlObjectCreationHandling handling)
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new YamlObjectCreationHandlingAttribute(handling));

        Assert.Contains("handling", ex.Message);
    }

    [Fact]
    public async Task YamlObjectCreationHandlingAttribute_InvalidAnnotations_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Serializer.SerializeWrapper(new ClassWithInvalidTypeAnnotation()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Serializer.SerializeWrapper(new ClassWithInvalidPropertyAnnotation()));
    }

    [YamlObjectCreationHandling((YamlObjectCreationHandling)(-1))]
    public class ClassWithInvalidTypeAnnotation
    {
    }

    public class ClassWithInvalidPropertyAnnotation
    {
        [YamlObjectCreationHandling((YamlObjectCreationHandling)(-1))]
        public List<int> Property { get; }
    }

    [Theory]
    [InlineData(typeof(ClassWithParameterizedConstructorWithPopulateProperty))]
    [InlineData(typeof(ClassWithParameterizedConstructorWithPopulateType))]
    public async Task ClassWithParameterizedCtor_UsingPopulateConfiguration_ThrowsNotSupportedException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        object instance = Activator.CreateInstance(type, "Jim");
        string yaml = """{"Username":"Jim","PhoneNumbers":["123456"]}""";

        await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(instance, type));
        await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper(yaml, type));
        Assert.Throws<NotSupportedException>(() => Serializer.GetTypeInfo(type));
    }

    public class ClassWithParameterizedConstructorWithPopulateProperty(string name)
    {
        public string Name { get; } = name;

        [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
        public List<string> PhoneNumbers { get; } = new();
    }

    [YamlObjectCreationHandling(YamlObjectCreationHandling.Populate)]
    public class ClassWithParameterizedConstructorWithPopulateType(string name)
    {
        public string Name { get; } = name;

        public List<string> PhoneNumbers { get; } = new();
    }

    [Fact]
    public async Task ClassWithParameterizedCtor_NoPopulateConfiguration_WorksWithGlobalPopulateConfiguration()
    {
        string yaml = """{"Username":"Jim","PhoneNumbers":["123456"]}""";

        YamlSerializerOptions options = Serializer.CreateOptions(makeReadOnly: false);
        options.PreferredObjectCreationHandling = YamlObjectCreationHandling.Populate;

        ClassWithParameterizedConstructorNoPopulate result = await Serializer.DeserializeWrapper<ClassWithParameterizedConstructorNoPopulate>(yaml, options);
        Assert.Empty(result.PhoneNumbers);
    }

    public class ClassWithParameterizedConstructorNoPopulate(string name)
    {
        public string Name { get; } = name;

        public List<string> PhoneNumbers { get; } = new();
    }
}
